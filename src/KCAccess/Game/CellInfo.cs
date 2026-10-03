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
            foreach (var n in order) result.Add(counts[n] > 1 ? n + " times " + counts[n] : n);
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
            int team = b.TeamID();
            if (team != 0)
            {
                var rel = World.inst.RelationBetween(0, team);
                parts.Add(rel == World.Relations.Enemy ? "enemy" : "foreign");
            }
            return TextUtil.Join(", ", parts);
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
                    result.Add((s.teamID == 0 ? "your " : (World.inst.RelationBetween(0, s.teamID) == World.Relations.Enemy ? "enemy " : "foreign ")) + "ship");
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
                    if ((int)p.x == c.x && (int)p.z == c.z) result.Add("dragon");
                }
            }
            return result;
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
            if (verbose && !IsWater(c)) parts.Add(Fertility(c));
            int villagers = VillagerCount(c);
            if (villagers > 0) parts.Add(TextUtil.Plural(villagers, "villager"));
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
    }
}
