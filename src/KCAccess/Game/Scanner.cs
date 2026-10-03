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
            "Your buildings", "Construction sites", "Your soldiers and ships", "Threats", "Stone", "Iron", "Forests", "Fresh water", "Foreign kingdoms", "Special places"
        };

        private int category;
        private List<Item> items = new List<Item>();
        private int index = -1;
        private bool stale = true;

        internal string CategoryName => Categories[category];

        internal void ChangeCategory(int delta, GridPos origin)
        {
            category = (category + delta + Categories.Length) % Categories.Length;
            stale = true;
            Refresh(origin);
            A.Cue(Cue.Navigate);
            A.Say(CategoryName + ", " + (items.Count == 0 ? "none found" : TextUtil.Plural(items.Count, "item")));
        }

        /// <summary>Move to the next / previous item. Returns its position, or null when the category is empty.</summary>
        internal GridPos? Step(int delta, GridPos origin)
        {
            if (stale) Refresh(origin);
            if (items.Count == 0)
            {
                A.Cue(Cue.Edge);
                A.Say(CategoryName + ", none found");
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
            A.Say(it.Label + ", " + Directions.Relative(origin, it.Pos) + ", " + (index + 1) + " of " + items.Count);
            return it.Pos;
        }

        /// <summary>Forget the list so it is rebuilt (nearest first) on the next step.</summary>
        internal void Invalidate() => stale = true;

        private void Refresh(GridPos origin)
        {
            stale = false;
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
                        bool enemy = team != 0 && w.RelationBetween(0, team) == World.Relations.Enemy;
                        if (mine ? team != 0 : !enemy) continue;
                        Vector3 p = s.GetPos();
                        found.Add(new Item { Label = (mine ? "your ship" : "enemy ship"), Pos = new GridPos((int)p.x, (int)p.z) });
                    }
                    if (!mine)
                    {
                        for (int i = 0; i < SiegeMonster.monsters.Count; i++)
                        {
                            var m = SiegeMonster.monsters.data[i];
                            if (m == null) continue;
                            Vector3 p = m.GetPos();
                            found.Add(new Item { Label = "ogre", Pos = new GridPos((int)p.x, (int)p.z) });
                        }
                        var dragons = DragonSpawn.inst.currentDragons;
                        for (int i = 0; i < dragons.Count; i++)
                        {
                            var d = dragons.data[i];
                            if (d == null) continue;
                            Vector3 p = d.transform.position;
                            found.Add(new Item { Label = "dragon", Pos = new GridPos((int)p.x, (int)p.z) });
                        }
                        if (FireManager.inst != null && FireManager.inst.fireContainer != null)
                        {
                            foreach (Transform f in FireManager.inst.fireContainer.transform)
                            {
                                if (!f.gameObject.activeInHierarchy) continue;
                                Vector3 p = f.position;
                                found.Add(new Item { Label = "fire", Pos = new GridPos((int)p.x, (int)p.z) });
                            }
                        }
                        AddClusters(found, c => c.Type == ResourceType.WolfDen && CellInfo.Explored(c), n => "wolf den");
                    }
                    break;
                }
                case "Stone":
                    AddClusters(found, c => c.Type == ResourceType.Stone && CellInfo.Explored(c), n => "stone deposit, " + TextUtil.Plural(n, "tile"));
                    break;
                case "Iron":
                    AddClusters(found, c => c.Type == ResourceType.IronDeposit && CellInfo.Explored(c), n => "iron deposit, " + TextUtil.Plural(n, "tile"));
                    break;
                case "Forests":
                    AddClusters(found, c => c.TreeAmount > 0 && c.OccupyingStructure.Count == 0 && CellInfo.Explored(c), n => "forest, " + TextUtil.Plural(n, "tile"), minSize: 3);
                    break;
                case "Fresh water":
                    AddClusters(found, c => c.Type == ResourceType.Water && !c.saltWater && CellInfo.Explored(c), n => "fresh water, " + TextUtil.Plural(n, "tile"));
                    break;
                case "Foreign kingdoms":
                {
                    var list = Player.inst.Buildings;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var b = list.data[i];
                        if (b == null || b.TeamID() == 0) continue;
                        if (b.uniqueNameHash != World.keepHash && b.uniqueNameHash != World.outpostHash) continue;
                        var c = b.GetCell();
                        if (c == null || !CellInfo.Explored(c)) continue;
                        found.Add(new Item { Label = CellInfo.Owner(c) + ", " + b.FriendlyName, Pos = new GridPos(c.x, c.z) });
                    }
                    break;
                }
                case "Special places":
                    AddClusters(found, c => c.Type == ResourceType.WitchHut && CellInfo.Explored(c), n => "witch hut");
                    AddClusters(found, c => c.Type == ResourceType.EmptyCave && CellInfo.Explored(c), n => "cave");
                    AddClusters(found, c => c.Type == ResourceType.WolfDen && CellInfo.Explored(c), n => "wolf den");
                    break;
            }
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
