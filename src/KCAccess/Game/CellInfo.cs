using Assets.Code;
using Assets;
using System.Collections.Generic;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>Turns a map tile into speech.</summary>
    internal static class CellInfo
    {
        /// <summary>Is the tile explored (not covered by fog of war)?</summary>
        internal static bool Explored(Cell c)
        {
            if (c == null) return false;
            if (FogOfWar.inst == null) return true;
            try
            {
                return FogOfWar.inst.IsVisible(c.x, c.z);
            }
            catch
            {
                return true;
            }
        }

        internal static bool IsWater(Cell c) => c != null && c.Type == ResourceType.Water;

        /// <summary>Short terrain word: "grass", "forest", "sea" …</summary>
        internal static string Terrain(Cell c)
        {
            switch (c.Type)
            {
                case ResourceType.Water:
                    if (c.deepWater) return c.saltWater ? "deep sea" : "deep water";
                    return c.saltWater ? "shallow sea" : "shallow water";
                case ResourceType.Wood:
                    return c.TreeAmount > 0 ? "forest" : "grass";
                case ResourceType.Stone: return "stone deposit";
                case ResourceType.UnusableStone: return "rock";
                case ResourceType.IronDeposit: return "iron deposit";
                case ResourceType.EmptyCave: return "empty cave";
                case ResourceType.WolfDen: return "wolf den";
                case ResourceType.WitchHut: return "witch hut";
                default:
                    if (c.TreeAmount > 0) return "forest";
                    if (TreeSystem.inst != null && TreeSystem.inst.AnimCount(c) > 0) return "grass, tree growing or falling";
                    return "grass";
            }
        }

        internal static string Fertility(Cell c)
        {
            if (IsWater(c)) return string.Empty;
            switch (c.GetEffectiveFertility())
            {
                case 0: return "barren";
                case 1: return "fertile";
                case 2: return "very fertile";
                default: return "irrigated very fertile";
            }
        }

        /// <summary>Signature used by Ctrl+arrows to find where the map changes.</summary>
        internal static string Signature(Cell c)
        {
            if (c == null) return "edge";
            if (!Explored(c)) return "fog";
            var b = c.TopMostStructure;
            if (b != null) return "b:" + b.UniqueName;
            return "t:" + Terrain(c);
        }

        /// <summary>Building names on the tile, top to bottom, merging stacked castle blocks.</summary>
        internal static List<string> Structures(Cell c)
        {
            var result = new List<string>();
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            var all = new List<Building>();
            all.AddRange(c.OccupyingStructure);
            all.AddRange(c.SubStructure);
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var b = all[i];
                if (b == null || b.Life <= 0f && !b.IsBuilt()) continue;
                string name = BuildingSummary(b, brief: true);
                if (!counts.ContainsKey(name))
                {
                    counts[name] = 0;
                    order.Add(name);
                }
                counts[name]++;
            }
            foreach (var n in order) result.Add(counts[n] > 1 ? n + ", stacked " + counts[n] + " high" : n);
            return result;
        }

        /// <summary>"Small House, under construction 40 percent, yours".</summary>
        internal static string BuildingSummary(Building b, bool brief)
        {
            var parts = new List<string> { b.FriendlyName };
            if (!b.IsBuilt()) parts.Add("under construction " + TextUtil.Percent(Mathf.Clamp01(b.constructionProgress)));
            else
            {
                if (b.Life < 0.999f && b.Life > 0f) parts.Add("damaged, " + TextUtil.Percent(b.Life) + " health");
                if (!b.Open) parts.Add("closed");
                if (!brief && b.WorkersForFullYield > 0 && b.CategoryName != "house")
                {
                    parts.Add(b.WorkersAllocated + " of " + b.WorkersForFullYield + " workers");
                }
            }
            if (!brief && b.IsBuilt())
            {
                string range = RangeText(b, placed: true);
                if (range != null) parts.Add(range);
            }
            int team = b.TeamID();
            if (team != 0)
            {
                var rel = World.inst.RelationBetween(0, team);
                parts.Add(rel == World.Relations.Enemy ? "enemy" : "foreign");
            }
            return TextUtil.Join(", ", parts);
        }

        /// <summary>
        /// Shooting range of towers (the rings the game draws on the ground). <paramref name="placed"/>: the building
        /// stands on the map (built or being placed), so its castle height counts; otherwise (build menu) only the
        /// range on the ground and at full height.
        /// </summary>
        internal static string RangeText(Building b, bool placed)
        {
            var r = b != null ? b.GetComponent<IMaxRangeDisplay>() : null;
            if (r == null) return null;
            try
            {
                return UnitText.Range(r.GetInitRadius(), r.GetUnitsPerRing(), placed ? r.GetCurrHeight() : -1, r.GetNumRings());
            }
            catch (System.Exception)
            {
                return null; // not set up yet
            }
        }

        internal static string Owner(Cell c)
        {
            if (c.landMassIdx < 0) return string.Empty;
            if (Player.inst.LandMassIsAPlayerLandMass(c.landMassIdx)) return string.Empty;
            var owner = World.GetLandmassOwner(c.landMassIdx);
            if (owner == null) return IsWater(c) || Player.inst.keep == null ? string.Empty : "unclaimed land";
            string name = c.landMassIdx < Player.inst.LandMassNames.Count ? Player.inst.LandMassNames[c.landMassIdx] : null;
            return string.IsNullOrEmpty(name) ? "foreign kingdom" : name + " kingdom";
        }

        internal static bool OnFire(Cell c)
        {
            if (FireManager.inst == null) return false;
            try
            {
                return FireManager.inst.ClosestFireWithinRadius(c.Center, 0.75f, includeFullFires: true) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Units standing on or right next to the tile.</summary>
        internal static List<string> Units(Cell c)
        {
            var result = new List<string>();
            if (UnitSystem.inst != null)
            {
                var armies = UnitSystem.inst.armies;
                for (int i = 0; i < armies.Count; i++)
                {
                    var a = armies.data[i];
                    if (a == null) continue;
                    Vector3 p = a.GetPos();
                    if ((int)p.x != c.x || (int)p.z != c.z) continue;
                    result.Add(Units_ArmyName(a));
                }
            }
            if (ShipSystem.inst != null)
            {
                var ships = ShipSystem.inst.GetShipsAt(c.x, c.z);
                for (int i = 0; i < ships.Count; i++)
                {
                    var s = ships.data[i];
                    if (s == null) continue;
                    result.Add(ShipName(s));
                }
            }
            for (int i = 0; i < SiegeMonster.monsters.Count; i++)
            {
                var m = SiegeMonster.monsters.data[i];
                if (m == null) continue;
                Vector3 p = m.GetPos();
                if ((int)p.x == c.x && (int)p.z == c.z) result.Add("ogre");
            }
            if (DragonSpawn.inst != null)
            {
                var dragons = DragonSpawn.inst.currentDragons;
                for (int i = 0; i < dragons.Count; i++)
                {
                    var d = dragons.data[i];
                    if (d == null) continue;
                    Vector3 p = d.transform.position;
                    if ((int)p.x == c.x && (int)p.z == c.z) result.Add(DragonName(d));
                }
            }
            // Siege catapults (yours from the siege workshop, and the vikings') were never mentioned.
            var catapults = SiegeCatapultSystem.siegeCatapults;
            for (int i = 0; catapults != null && i < catapults.Count; i++)
            {
                var sc = catapults.data[i];
                if (sc == null || !sc.ValidToSelect()) continue; // dead, or carried on a ship
                Vector3 p = sc.GetPos();
                if ((int)p.x == c.x && (int)p.z == c.z) result.Add(CatapultName(sc));
            }
            if (CartSystem.inst != null)
            {
                foreach (var cart in CartSystem.inst.carts)
                {
                    if (cart == null) continue;
                    Vector3 p = cart.GetPos();
                    if ((int)p.x == c.x && (int)p.z == c.z) result.Add(CartName(cart));
                }
            }
            int wolves = 0, hunting = 0;
            for (int i = 0; i < WolfDen.wolves.Count; i++)
            {
                var w = WolfDen.wolves.data[i];
                if (w == null || w.IsInvalid()) continue;
                if ((int)w.pos.x != c.x || (int)w.pos.z != c.z) continue;
                wolves++;
                if (w.status == WolfDen.Status.Chase || w.status == WolfDen.Status.Attack) hunting++;
            }
            if (wolves > 0) result.Add(TextUtil.Plural(wolves, "wolf", "wolves") + (hunting > 0 ? ", hunting" : string.Empty));
            return result;
        }

        private static bool Enemy(int team) => team != 0 && World.inst.RelationBetween(0, team) == World.Relations.Enemy;

        private static string Selected(ISelectable s) => GameUI.inst != null && s != null && GameUI.inst.IsSelected(s) ? ", selected" : string.Empty;

        /// <summary>"your dragon" (from a dragon nest), "wild dragon" (an attacking one) or "enemy dragon".</summary>
        internal static string DragonName(Dragon d)
        {
            if (d.teamId == 0) return "your dragon" + Selected(d);
            if (d.teamId == -1) return "wild dragon";
            return UnitText.Owner(false, Enemy(d.teamId)) + "dragon";
        }

        internal static string CatapultName(SiegeCatapult sc) => UnitText.Owner(sc.TeamID() == 0, Enemy(sc.TeamID())) + "siege catapult" + Selected(sc);

        internal static string CartName(TransportCart cart) => UnitText.Owner(cart.TeamID() == 0, Enemy(cart.TeamID())) + "transport cart" + Selected(cart);

        /// <summary>Any unit the player can select and send somewhere.</summary>
        internal static string UnitName(IMoveableUnit u)
        {
            switch (u)
            {
                case UnitSystem.Army a: return Units_ArmyName(a);
                case SiegeCatapult sc: return CatapultName(sc);
                case Dragon d: return DragonName(d);
                case ShipBase s: return ShipName(s);
                case SiegeMonster _: return "ogre";
                case DragonPuppet _: return "your dragon";
                default: return "unit";
            }
        }

        /// <summary>"your transport ship", "merchant ship", "enemy viking ship" …</summary>
        internal static string ShipName(ShipBase s)
        {
            string kind;
            switch (s.type)
            {
                case ShipBase.ShipType.Merchant:
                case ShipBase.ShipType.PlayerMerchant: kind = "merchant ship"; break;
                case ShipBase.ShipType.Fishing: kind = "fishing ship"; break;
                case ShipBase.ShipType.TroopTransport: kind = "troop ship"; break;
                case ShipBase.ShipType.VikingTroopTransport: kind = "viking ship"; break;
                case ShipBase.ShipType.OgreTroopTransport: kind = "ogre ship"; break;
                case ShipBase.ShipType.SeedShip: kind = "seed ship"; break;
                default: kind = "transport ship"; break;
            }
            if (s.type == ShipBase.ShipType.Merchant || s.type == ShipBase.ShipType.PlayerMerchant) return kind;
            string who = s.teamID == 0 ? "your " : (World.inst.RelationBetween(0, s.teamID) == World.Relations.Enemy ? "enemy " : "foreign ");
            bool selected = GameUI.inst != null && s is ISelectable sel && GameUI.inst.IsSelected(sel);
            return who + kind + (selected ? ", selected" : string.Empty);
        }

        internal static string Units_ArmyName(UnitSystem.Army a)
        {
            string type;
            switch (a.armyType)
            {
                case UnitSystem.ArmyType.Archer: type = "archers"; break;
                case UnitSystem.ArmyType.Thief: type = "thieves"; break;
                case UnitSystem.ArmyType.Envoy: type = "envoy"; break;
                case UnitSystem.ArmyType.Settler: type = "settlers"; break;
                case UnitSystem.ArmyType.Elite: type = "elite soldiers"; break;
                default: type = "soldiers"; break;
            }
            string who = a.teamId == 0 ? "your " : (World.inst.RelationBetween(0, a.teamId) == World.Relations.Enemy ? "enemy " : "foreign ");
            bool selected = GameUI.inst != null && GameUI.inst.IsSelected(a);
            return who + type + (selected ? ", selected" : string.Empty);
        }

        /// <summary>Description spoken while moving the cursor.</summary>
        internal static string Brief(Cell c, bool verbose)
        {
            if (c == null) return "outside the map";
            if (!Explored(c)) return "unexplored";
            var parts = new List<string>();
            if (OnFire(c)) parts.Add("on fire");
            parts.AddRange(Units(c));
            var structures = Structures(c);
            parts.AddRange(structures);
            string terrain = Terrain(c);
            if (c.Type == ResourceType.Wood || c.TreeAmount > 0)
            {
                if (c.TreeAmount > 0) terrain = "forest, " + TextUtil.Plural(c.TreeAmount, "tree");
            }
            if (structures.Count == 0 || c.Type != ResourceType.None) parts.Add(terrain);
            if (c.TreeAmount > 0 && GameUI.inst != null && GameUI.inst.GetClearCutterJob(c) != null) parts.Add("marked for chopping");
            if (verbose && !IsWater(c) && c.Type == ResourceType.None) parts.Add(Fertility(c)); // soil only matters on open ground
            int villagers = VillagerCount(c);
            if (villagers > 0)
            {
                int sick = SickCount(c);
                parts.Add(TextUtil.Plural(villagers, "villager") + (sick > 0 ? ", " + sick + " sick" : string.Empty));
            }
            if (IsWater(c))
            {
                int fish = FishAt(c);
                if (fish > 0) parts.Add(TextUtil.Plural(fish, "fish", "fish"));
            }
            if (verbose)
            {
                string problem = Problems.At(c.x, c.z);
                if (problem != null) parts.Add(problem);
            }
            parts.Add(Owner(c));
            if (Plugin.CfgCoordinates.Value) parts.Add(c.x + ", " + c.z);
            return TextUtil.Join(", ", parts);
        }

        /// <summary>Everything about a tile (I key).</summary>
        internal static string Full(Cell c)
        {
            if (c == null) return "outside the map";
            var parts = new List<string> { "Tile " + c.x + ", " + c.z };
            if (!Explored(c))
            {
                parts.Add("unexplored");
                return TextUtil.Sentences(parts.ToArray());
            }
            parts.Add(Brief(c, verbose: true));
            if (!IsWater(c))
            {
                parts.Add(c.RoadCoverage > 0 || c.RoadConnectCoverage > 0 ? "inside road coverage" : "outside road coverage");
                if (c.IrrigationCoverage > 0) parts.Add("irrigated");
            }
            if (c.wolfTerritory) parts.Add("wolf territory");
            if (c.Danger > 0) parts.Add("dangerous area");
            foreach (var b in c.OccupyingStructure)
            {
                if (b != null && b.IsBuilt() && b.WorkersForFullYield > 0 && b.CategoryName != "house") parts.Add(b.FriendlyName + ": " + b.WorkersAllocated + " of " + b.WorkersForFullYield + " workers");
            }
            return TextUtil.Sentences(parts.ToArray());
        }

        /// <summary>Fish swimming in this water tile (fishing huts need them nearby).</summary>
        internal static int FishAt(Cell c)
        {
            try
            {
                var fc = FishSystem.inst != null ? FishSystem.inst.fishCells : null;
                if (c == null || fc == null || !IsWater(c)) return 0; // fish swim over the shoreline; only water tiles count
                int i = c.z * World.inst.GridWidth + c.x;
                return i >= 0 && i < fc.Length && fc[i] != null ? fc[i].fish.Count : 0;
            }
            catch (System.Exception)
            {
                return 0;
            }
        }

        internal static int SickCount(Cell c)
        {
            var v = World.inst.GetVillagersAt(c.x, c.z);
            int n = 0;
            if (v != null) for (int i = 0; i < v.Count; i++) if (v.data[i] != null && v.data[i].sick) n++;
            return n;
        }

        internal static int VillagerCount(Cell c)
        {
            try
            {
                var v = World.inst.GetVillagersAt(c.x, c.z);
                return v != null ? v.Count : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>Counts what lies within <paramref name="radius"/> tiles of the cursor (O key).</summary>
        internal static AreaSurvey Survey(GridPos at, int radius)
        {
            var w = World.inst;
            var s = new AreaSurvey { Radius = radius };
            for (int z = at.Z - radius; z <= at.Z + radius; z++)
                for (int x = at.X - radius; x <= at.X + radius; x++)
                {
                    if (x < 0 || z < 0 || x >= w.GridWidth || z >= w.GridHeight) continue;
                    var c = w.GetCellData(x, z);
                    if (c == null) continue;
                    if (!Explored(c)) { s.Fog++; continue; }
                    if (IsWater(c))
                    {
                        if (c.deepWater) s.DeepWater++; else s.Water++;
                        if (FishAt(c) > 0) s.FishTiles++;
                        continue;
                    }
                    s.Land++;
                    if (c.Type == ResourceType.Stone) s.Stone++;
                    else if (c.Type == ResourceType.IronDeposit) s.Iron++;
                    int fert = c.GetEffectiveFertility();
                    if (fert >= 2) s.VeryFertile++; else if (fert == 1) s.Fertile++;
                    if (c.TreeAmount > 0) s.Forest++;
                    if (c.OccupyingStructure.Count > 0) s.Buildings++;
                    else if (c.TreeAmount == 0 && c.Type == ResourceType.None) s.Open++;
                }
            if (s.Stone == 0) s.NearestStone = Nearest(at, radius, c => c.Type == ResourceType.Stone);
            if (s.Iron == 0) s.NearestIron = Nearest(at, radius, c => c.Type == ResourceType.IronDeposit);
            if (s.Water == 0 && s.DeepWater == 0) s.NearestWater = Nearest(at, radius, IsWater);
            return s;
        }

        private static string Nearest(GridPos at, int skipRadius, System.Func<Cell, bool> match)
        {
            var w = World.inst;
            for (int r = skipRadius + 1; r <= 40; r++)
            {
                GridPos? best = null;
                double bestD = double.MaxValue;
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz)) != r) continue;
                        int x = at.X + dx, z = at.Z + dz;
                        if (x < 0 || z < 0 || x >= w.GridWidth || z >= w.GridHeight) continue;
                        var c = w.GetCellData(x, z);
                        if (c == null || !Explored(c) || !match(c)) continue;
                        var p = new GridPos(x, z);
                        double d = Directions.Euclid(at, p);
                        if (d < bestD) { bestD = d; best = p; }
                    }
                if (best.HasValue) return Directions.Relative(at, best.Value);
            }
            return null;
        }

        /// <summary>Name, age, job and current thought of a villager (what the villager panel shows).</summary>
        internal static string VillagerSummary(Villager v)
        {
            if (v == null) return "villager";
            var parts = new List<string> { string.IsNullOrEmpty(v.name) ? "villager" : v.name };
            try
            {
                parts.Add(string.Format(I2.Loc.ScriptLocalization.PersonUIYearsOld, Mathf.FloorToInt(v.timeAlive / Weather.inst.TimeInYear())));
            }
            catch (System.Exception)
            {
                // age is optional
            }
            string job = v.job != null ? TextUtil.Clean(v.job.GetDescription() ?? string.Empty) : string.Empty;
            parts.Add(job.Length > 0 ? job : "no job");
            if (v.Residence == null) parts.Add("homeless");
            if (v.sick) parts.Add("sick with plague");
            string thought = TextUtil.Clean(v.GetThought() ?? string.Empty);
            if (thought.Length > 0) parts.Add("thinks: " + thought);
            return string.Join(", ", parts.ToArray());
        }
    }
}
