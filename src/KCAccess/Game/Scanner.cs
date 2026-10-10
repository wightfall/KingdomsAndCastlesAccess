using Assets.Code;
using Assets;
using System;
using System.Collections.Generic;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// Finds things on the map by category so the player can jump straight to them:
    /// Page Up / Page Down choose the category, [ and ] move to the previous / next item (nearest first).
    /// </summary>
    internal sealed class Scanner
    {
        internal sealed class Item
        {
            public string Label;
            public GridPos Pos;
        }

        private static readonly string[] Categories =
        {
            Loc.N("Your buildings"), Loc.N("Construction sites"), Loc.N("Your soldiers and ships"), Loc.N("Alerts"), Loc.N("Problems"), Loc.N("Threats"), Loc.N("Stone"), Loc.N("Iron"),
            Loc.N("Fertile land"), Loc.N("Forests"), Loc.N("Fresh water"), Loc.N("Fishing grounds"), Loc.N("Foreign kingdoms"), Loc.N("Special places")
        };

        private int category;
        private List<Item> items = new List<Item>();
        private int index = -1;
        private bool stale = true;
        private GridPos lastOrigin;
        private float refreshedAt;

        internal string CategoryName => Loc.T(Categories[category]);

        internal void ChangeCategory(int delta, GridPos origin)
        {
            category = (category + delta + Categories.Length) % Categories.Length;
            stale = true;
            Refresh(origin);
            A.Cue(Cue.Navigate);
            A.Say(CategoryName + ", " + (items.Count == 0 ? Loc.T("none found") : Loc.P(items.Count, "{0} item", "{0} items")));
        }

        /// <summary>Move to the next / previous item. Returns it, or null when the category is empty.</summary>
        internal Item Step(int delta, GridPos origin)
        {
            if (stale || origin != lastOrigin)
            {
                // The cursor moved: list again nearest first from the new spot. When it moved onto the current item
                // (jump or walk to the target), that item is the nearest now: count from it, or "next" named it again.
                var current = !stale && index >= 0 && index < items.Count ? items[index].Pos : (GridPos?)null;
                Refresh(origin);
                if (current.HasValue && current.Value == origin) index = items.FindIndex(i => i.Pos == origin);
            }
            else if (Time.unscaledTime - refreshedAt > 5f)
            {
                // Same spot but the kingdom may have changed: rebuild, keep our place in the list.
                var current = index >= 0 && index < items.Count ? items[index].Pos : (GridPos?)null;
                Refresh(origin);
                if (current.HasValue) index = items.FindIndex(i => i.Pos == current.Value);
            }
            if (items.Count == 0)
            {
                A.Cue(Cue.Edge);
                A.Say(CategoryName + ", " + Loc.T("none found"));
                return null;
            }
            index += delta;
            if (index >= items.Count)
            {
                index = 0;
                A.Cue(Cue.Wrap);
            }
            else if (index < 0)
            {
                index = items.Count - 1;
                A.Cue(Cue.Wrap);
            }
            var it = items[index];
            A.Say(it.Label + ", " + Directions.Relative(origin, it.Pos) + ", " + Loc.F("{0} of {1}", index + 1, items.Count));
            return it;
        }

        /// <summary>Forget the list so it is rebuilt (nearest first) on the next step.</summary>
        internal void Invalidate() => stale = true;

        private void Refresh(GridPos origin)
        {
            stale = false;
            lastOrigin = origin;
            refreshedAt = Time.unscaledTime;
            index = -1;
            var found = new List<Item>();
            try
            {
                Collect(Categories[category], found);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Scanner failed: " + e);
            }
            items = Directions.SortByDistance(found, i => i.Pos, origin);
        }

        private static void Collect(string cat, List<Item> found)
        {
            var w = World.inst;
            switch (cat)
            {
                case "Your buildings":
                case "Construction sites":
                {
                    bool sites = cat == "Construction sites";
                    var seen = new HashSet<Building>();
                    var list = Player.inst.Buildings;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var b = list.data[i];
                        if (b == null || b.TeamID() != 0 || !seen.Add(b)) continue;
                        if (sites == b.IsBuilt()) continue;
                        if (!sites && (b.categoryHash == World.pathHash || b.categoryHash == World.castleblockHash)) continue;
                        var c = b.GetCell();
                        if (c == null) continue;
                        found.Add(new Item { Label = CellInfo.BuildingSummary(b, brief: false), Pos = new GridPos(c.x, c.z) });
                    }
                    break;
                }
                case "Your soldiers and ships":
                case "Threats":
                {
                    bool mine = cat == "Your soldiers and ships";
                    var armies = UnitSystem.inst.armies;
                    for (int i = 0; i < armies.Count; i++)
                    {
                        var a = armies.data[i];
                        if (a == null) continue;
                        bool enemy = a.teamId != 0 && w.RelationBetween(0, a.teamId) == World.Relations.Enemy;
                        if (mine ? a.teamId != 0 : !enemy) continue;
                        Vector3 p = a.GetPos();
                        found.Add(new Item { Label = CellInfo.Units_ArmyName(a), Pos = new GridPos((int)p.x, (int)p.z) });
                    }
                    var ships = ShipSystem.inst.ships;
                    for (int i = 0; i < ships.Count; i++)
                    {
                        var s = ships.data[i];
                        if (s == null) continue;
                        int team = s.teamID;
                        bool merchant = s.type == ShipBase.ShipType.Merchant || s.type == ShipBase.ShipType.PlayerMerchant;
                        bool enemy = !merchant && team != 0 && w.RelationBetween(0, team) == World.Relations.Enemy;
                        if (mine ? (team != 0 && !merchant) : !enemy) continue;
                        Vector3 p = s.GetPos();
                        found.Add(new Item { Label = CellInfo.ShipName(s), Pos = new GridPos((int)p.x, (int)p.z) });
                    }
                    // Siege catapults: yours from the siege workshop, the vikings' as threats.
                    var catapults = SiegeCatapultSystem.siegeCatapults;
                    for (int i = 0; catapults != null && i < catapults.Count; i++)
                    {
                        var sc = catapults.data[i];
                        if (sc == null || !sc.ValidToSelect()) continue;
                        int team = sc.TeamID();
                        bool enemy = team != 0 && w.RelationBetween(0, team) == World.Relations.Enemy;
                        if (mine ? team != 0 : !enemy) continue;
                        Vector3 p = sc.GetPos();
                        found.Add(new Item { Label = CellInfo.CatapultName(sc), Pos = new GridPos((int)p.x, (int)p.z) });
                    }
                    // Dragons: your own (dragon nest) are not threats.
                    var dragons = DragonSpawn.inst != null ? DragonSpawn.inst.currentDragons : null;
                    for (int i = 0; dragons != null && i < dragons.Count; i++)
                    {
                        var d = dragons.data[i];
                        if (d == null) continue;
                        bool enemy = d.teamId != 0 && w.RelationBetween(0, d.teamId) == World.Relations.Enemy;
                        if (mine ? d.teamId != 0 : !enemy) continue;
                        Vector3 p = d.transform.position;
                        found.Add(new Item { Label = CellInfo.DragonName(d), Pos = new GridPos((int)p.x, (int)p.z) });
                    }
                    if (mine && CartSystem.inst != null)
                    {
                        foreach (var cart in CartSystem.inst.carts)
                        {
                            if (cart == null || cart.TeamID() != 0) continue;
                            Vector3 p = cart.GetPos();
                            found.Add(new Item { Label = CellInfo.CartName(cart), Pos = new GridPos((int)p.x, (int)p.z) });
                        }
                    }
                    if (!mine)
                    {
                        for (int i = 0; i < SiegeMonster.monsters.Count; i++)
                        {
                            var m = SiegeMonster.monsters.data[i];
                            if (m == null) continue;
                            Vector3 p = m.GetPos();
                            found.Add(new Item { Label = Loc.T("ogre"), Pos = new GridPos((int)p.x, (int)p.z) });
                        }
                        // Wolves chasing or attacking someone (wandering ones stay near their den, listed below).
                        for (int i = 0; i < WolfDen.wolves.Count; i++)
                        {
                            var wolf = WolfDen.wolves.data[i];
                            if (wolf == null || wolf.IsInvalid() || (wolf.status != WolfDen.Status.Chase && wolf.status != WolfDen.Status.Attack)) continue;
                            found.Add(new Item { Label = Loc.T("hunting wolf"), Pos = new GridPos((int)wolf.pos.x, (int)wolf.pos.z) });
                        }
                        if (FireManager.inst != null && FireManager.inst.fireContainer != null)
                        {
                            foreach (Transform f in FireManager.inst.fireContainer.transform)
                            {
                                if (!f.gameObject.activeInHierarchy) continue;
                                Vector3 p = f.position;
                                found.Add(new Item { Label = Loc.T("fire"), Pos = new GridPos((int)p.x, (int)p.z) });
                            }
                        }
                        AddClusters(found, c => c.Type == ResourceType.WolfDen && CellInfo.Explored(c), n => Loc.T("wolf den"));
                    }
                    break;
                }
                case "Stone":
                    AddClusters(found, c => c.Type == ResourceType.Stone && CellInfo.Explored(c), n => Loc.T("stone deposit") + ", " + Loc.P(n, "{0} tile", "{0} tiles"));
                    break;
                case "Iron":
                    AddClusters(found, c => c.Type == ResourceType.IronDeposit && CellInfo.Explored(c), n => Loc.T("iron deposit") + ", " + Loc.P(n, "{0} tile", "{0} tiles"));
                    break;
                case "Problems":
                    foreach (var p in Problems.All()) found.Add(new Item { Label = p.Label, Pos = p.Pos });
                    break;
                case "Fishing grounds":
                {
                    var fc = FishSystem.inst != null ? FishSystem.inst.fishCells : null;
                    if (fc == null) break;
                    AddClusters(found, c => CellInfo.FishAt(c) > 0 && CellInfo.Explored(c), n => Loc.T("fishing ground") + ", " + Loc.P(n, "{0} tile", "{0} tiles"));
                    break;
                }
                case "Alerts":
                    foreach (var e in Alerts.Active()) found.Add(new Item { Label = Alerts.Describe(e), Pos = Alerts.PosOf(e) });
                    break;
                case "Fertile land":
                    // Open fertile ground where farms can go (no trees, rock, water or buildings).
                    AddClusters(found, c => c.Type == ResourceType.None && c.TreeAmount == 0 && c.OccupyingStructure.Count == 0 && c.GetEffectiveFertility() >= 1 && CellInfo.Explored(c),
                        n => Loc.T("open fertile land") + ", " + Loc.P(n, "{0} tile", "{0} tiles"), minSize: 4);
                    break;
                case "Forests":
                    AddClusters(found, c => c.TreeAmount > 0 && c.OccupyingStructure.Count == 0 && CellInfo.Explored(c), n => Loc.T("forest") + ", " + Loc.P(n, "{0} tile", "{0} tiles"), minSize: 3);
                    break;
                case "Fresh water":
                    AddClusters(found, c => c.Type == ResourceType.Water && !c.saltWater && CellInfo.Explored(c), n => Loc.T("fresh water") + ", " + Loc.P(n, "{0} tile", "{0} tiles"));
                    break;
                case "Foreign kingdoms":
                {
                    if (AIBrainsContainer.inst == null) break;
                    var landmasses = new List<int>();
                    foreach (var k in AIBrainsContainer.inst.kingdoms)
                    {
                        var lo = k != null ? k.LandmassOwner : null;
                        if (lo == null || lo.ownedLandMasses == null) continue;
                        for (int i = 0; i < lo.ownedLandMasses.Count; i++) landmasses.Add(lo.ownedLandMasses.data[i]);
                    }
                    // Kingdoms that have not settled yet are still planned on a start landmass.
                    var start = AIBrainsContainer.inst.aiStartInfo?.startData;
                    if (start != null) foreach (var sd in start) if (sd != null && !landmasses.Contains(sd.landmass)) landmasses.Add(sd.landmass);
                    foreach (int lm in landmasses)
                    {
                        if (lm < 0) continue;
                        string name = lm < Player.inst.LandMassNames.Count && !string.IsNullOrEmpty(Player.inst.LandMassNames[lm]) ? Player.inst.LandMassNames[lm] : Loc.T("foreign kingdom");
                        var keeps = Player.inst.GetBuildingListForLandMass(lm, World.keepHash);
                        if (keeps != null && keeps.Count > 0 && keeps.data[0] != null && keeps.data[0].GetCell() != null)
                        {
                            var c = keeps.data[0].GetCell();
                            found.Add(new Item { Label = Loc.F("{0} keep", name) + (CellInfo.Explored(c) ? string.Empty : ", " + Loc.T("unexplored")), Pos = new GridPos(c.x, c.z) });
                        }
                        else
                        {
                            var centre = LandmassCentre(lm);
                            if (centre.HasValue) found.Add(new Item { Label = Loc.F("{0} island, no keep yet", name), Pos = centre.Value });
                        }
                    }
                    break;
                }
                case "Special places":
                    AddClusters(found, c => c.Type == ResourceType.WitchHut && CellInfo.Explored(c), n => Loc.T("witch hut"));
                    AddClusters(found, c => c.Type == ResourceType.EmptyCave && CellInfo.Explored(c), n => Loc.T("cave"));
                    AddClusters(found, c => c.Type == ResourceType.WolfDen && CellInfo.Explored(c), n => Loc.T("wolf den"));
                    break;
            }
        }

        /// <summary>A land tile near the middle of a landmass.</summary>
        private static GridPos? LandmassCentre(int lm)
        {
            var w = World.inst;
            long sx = 0, sz = 0, n = 0;
            for (int z = 0; z < w.GridHeight; z++)
                for (int x = 0; x < w.GridWidth; x++)
                {
                    var c = w.GetCellDataUnsafe(x, z);
                    if (c != null && c.landMassIdx == lm) { sx += x; sz += z; n++; }
                }
            if (n == 0) return null;
            var mid = new GridPos((int)(sx / n), (int)(sz / n));
            // The average may fall in a lake: take the nearest tile of the landmass.
            GridPos best = mid;
            double bestD = double.MaxValue;
            for (int z = 0; z < w.GridHeight; z++)
                for (int x = 0; x < w.GridWidth; x++)
                {
                    var c = w.GetCellDataUnsafe(x, z);
                    if (c == null || c.landMassIdx != lm) continue;
                    double d = Directions.Euclid(mid, new GridPos(x, z));
                    if (d < bestD) { bestD = d; best = new GridPos(x, z); }
                }
            return best;
        }

        private static void AddClusters(List<Item> found, Func<Cell, bool> match, Func<int, string> label, int minSize = 1)
        {
            var w = World.inst;
            var clusters = Clusters.Find(w.GridWidth, w.GridHeight, (x, z) =>
            {
                var c = w.GetCellDataUnsafe(x, z);
                return c != null && match(c);
            });
            GridPos origin = MapController.Inst.CursorPos;
            foreach (var cl in clusters)
            {
                if (cl.Count < minSize) continue;
                found.Add(new Item { Label = label(cl.Count), Pos = Clusters.Nearest(cl, origin) });
            }
        }
    }
}
