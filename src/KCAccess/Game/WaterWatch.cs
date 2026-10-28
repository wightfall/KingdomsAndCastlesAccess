using System.Collections.Generic;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// The water system (water wheel, aqueduct pieces, reservoirs, bathhouse, dragon bath) and fountains work only when
    /// connected, which the game shows as pipes and water on the models. While one is held, every tile says what it would
    /// connect to (as a farm says the soil) and V also says the nearest water building; when one finishes construction,
    /// whether it got water (IrrigationManager floods the chain a moment later).
    /// </summary>
    internal static class WaterWatch
    {
        private static readonly List<Building> pending = new List<Building>();

        internal static bool IsWaterBuilding(Building b) => b != null && b.GetComponent<IWaterSystemUser>() != null;

        private static bool IsFountain(Building b) => b != null && b.GetComponent<Fountain>() != null;

        private static bool IsSource(Building b) => b.GetComponent<Noria>() != null;

        /// <summary>What the held building would connect to where it stands, or null for other buildings.</summary>
        internal static string PlacingHint(Building held, bool withNearest)
        {
            if (held == null || World.inst == null) return null;
            try
            {
                if (IsFountain(held))
                {
                    bool irrigated = false;
                    held.ForEachTileInBounds((x, z, c) => { if (c != null && c.IrrigationCoverage > 0) irrigated = true; });
                    return WaterText.FountainPlacing(irrigated);
                }
                if (!IsWaterBuilding(held)) return null;
                var tiles = Footprint(held);
                var touching = Touching(held, tiles);
                string nearest = touching.Count == 0 && withNearest ? Nearest(held, tiles) : null;
                return WaterText.Placing(touching, IsSource(held), nearest);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Water hint failed: " + e.Message);
                return null;
            }
        }

        private static HashSet<GridPos> Footprint(Building b)
        {
            var tiles = new HashSet<GridPos>();
            b.ForEachTileInBounds((x, z, c) => tiles.Add(new GridPos(x, z)));
            return tiles;
        }

        private static GridPos Middle(HashSet<GridPos> tiles)
        {
            int sx = 0, sz = 0;
            foreach (var t in tiles) { sx += t.X; sz += t.Z; }
            return tiles.Count > 0 ? new GridPos(sx / tiles.Count, sz / tiles.Count) : new GridPos(0, 0);
        }

        /// <summary>
        /// Water buildings side by side with the footprint, as IrrigationManager links them: the tiles around it without the
        /// corners. A water wheel's wheel tiles in the river do not link.
        /// </summary>
        private static List<WaterText.Neighbour> Touching(Building b, HashSet<GridPos> tiles)
        {
            var found = new List<WaterText.Neighbour>();
            var seen = new HashSet<Building>();
            var mid = Middle(tiles);
            foreach (var t in tiles)
            {
                foreach (var d in new[] { new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1) })
                {
                    var p = new GridPos(t.X + d.X, t.Z + d.Z);
                    if (tiles.Contains(p)) continue;
                    var c = World.inst.GetCellData(p.X, p.Z);
                    if (c == null) continue;
                    var o = c.StructureFind<IWaterSystemUser>();
                    if (o == null || o == b || !seen.Add(o) || OnWheelTile(o, c)) continue;
                    var w = o.GetComponent<IWaterSystemUser>();
                    found.Add(new WaterText.Neighbour
                    {
                        Name = o.FriendlyName,
                        Direction = Directions.Compass8(p.X - mid.X, p.Z - mid.Z),
                        State = !o.IsBuilt() ? WaterText.Link.Unbuilt : w.HasWater ? WaterText.Link.Water : WaterText.Link.Dry
                    });
                }
            }
            return found;
        }

        /// <summary>A cell under a water wheel's wheel (Aqueduct.ShouldConnect does not link those).</summary>
        private static bool OnWheelTile(Building o, Cell c)
        {
            var noria = o.GetComponent<Noria>();
            if (noria == null || noria.waterPos == null) return false;
            foreach (var t in noria.waterPos)
                if (t != null && World.inst.GetCellDataClamped(t.position) == c) return true;
            return false;
        }

        /// <summary>The nearest of the player's water buildings on this island: "Aqueduct, 6 tiles north east, ...".</summary>
        private static string Nearest(Building held, HashSet<GridPos> tiles)
        {
            var mid = Middle(tiles);
            var c0 = World.inst.GetCellData(mid.X, mid.Z);
            int lm = c0 != null ? c0.landMassIdx : -1;
            if (lm < 0) return null;
            var list = Player.inst.GetBuildingListForLandMass(lm);
            Building best = null;
            GridPos bestPos = mid;
            double bestD = double.MaxValue;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                var o = list.data[i];
                if (o == null || o == held || !IsWaterBuilding(o)) continue;
                var oc = o.GetCell();
                if (oc == null) continue;
                var p = new GridPos(oc.x, oc.z);
                double d = Directions.Euclid(mid, p);
                if (d < bestD) { bestD = d; best = o; bestPos = p; }
            }
            return best != null ? best.FriendlyName + ", " + Directions.Relative(mid, bestPos) : null;
        }

        /// <summary>A building of the player finished construction (GameEvents.OnBuilt).</summary>
        internal static void OnBuilt(Building b)
        {
            // A dragon nest links to the system too, but works without water.
            if (b == null || b.GetComponent<DragonNest>() != null || (!IsWaterBuilding(b) && !IsFountain(b))) return;
            // The irrigation flood and a fountain's refresh run when the building is added; give them a moment, say a
            // line of aqueduct pieces finishing one after another once, and after "Construction finished" (3 s quiet).
            pending.Add(b);
            dueAt = Time.unscaledTime + 4f;
        }

        private static float dueAt;

        internal static void Reset() => pending.Clear();

        internal static void Tick()
        {
            if (pending.Count == 0 || Time.unscaledTime < dueAt) return;
            var done = new List<Building>(pending);
            pending.Clear();
            if (!Plugin.CfgAnnounceLog.Value) return;
            try
            {
                var groups = new Dictionary<string, List<Building>>();
                var order = new List<string>();
                foreach (var b in done)
                {
                    if (b == null) continue;
                    if (!groups.TryGetValue(b.FriendlyName, out var g)) { groups[b.FriendlyName] = g = new List<Building>(); order.Add(b.FriendlyName); }
                    g.Add(b);
                }
                foreach (var name in order)
                {
                    var g = groups[name];
                    if (g.Count == 1 || IsFountain(g[0]))
                    {
                        foreach (var b in g)
                        {
                            string text = Status(b);
                            if (text != null) A.SayQueued(text);
                        }
                        continue;
                    }
                    int wet = 0;
                    foreach (var b in g) if (Works(b)) wet++;
                    A.SayQueued(WaterText.BuiltMany(name, g.Count, wet));
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Water status failed: " + e.Message);
            }
        }

        private static bool Works(Building b)
        {
            var fountain = b.GetComponent<Fountain>();
            if (fountain != null) return fountain.hasIrrigation;
            var w = b.GetComponent<IWaterSystemUser>();
            return w != null && w.HasWater;
        }

        /// <summary>Whether a water building or fountain works, or null for other buildings.</summary>
        internal static string Status(Building b)
        {
            if (b == null) return null;
            var fountain = b.GetComponent<Fountain>();
            if (fountain != null) return WaterText.FountainBuilt(b.FriendlyName, fountain.hasIrrigation);
            var w = b.GetComponent<IWaterSystemUser>();
            if (w == null) return null;
            return WaterText.Built(b.FriendlyName, IsSource(b), w.IsConnected, w.HasWater, Pumping(b.LandMass()));
        }

        /// <summary>A water wheel on the island has all its workers (IrrigationManager gives it pressure only then).</summary>
        private static bool Pumping(int lm)
        {
            var list = lm >= 0 ? Player.inst.GetBuildingListForLandMass(lm) : null;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                var noria = list.data[i] != null ? list.data[i].GetComponent<Noria>() : null;
                if (noria != null && noria.ShouldIrrigate()) return true;
            }
            return false;
        }
    }
}
