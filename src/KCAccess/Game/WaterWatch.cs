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
        /// The water buildings linked with the held one as IrrigationManager passes water on (WaterText.InReach, from each
        /// building to the ones in its reach): for a water wheel the ones it would feed, for anything else the ones that
        /// would feed it, a dragon roost included.
        /// </summary>
        private static List<WaterText.Neighbour> Touching(Building held, HashSet<GridPos> tiles)
        {
            held.CalculateBoundingFootprint(out int minX, out int minZ, out int maxX, out int maxZ);
            int midX = (minX + maxX) / 2, midZ = (minZ + maxZ) / 2;
            bool source = IsSource(held);
            var found = new List<WaterText.Neighbour>();
            var seen = new HashSet<Building>();
            // The largest water buildings are 3 by 3, so every candidate stands within 4 tiles of the held box.
            for (int x = minX - 4; x <= maxX + 4; x++)
                for (int z = minZ - 4; z <= maxZ + 4; z++)
                {
                    var c = World.inst.GetCellData(x, z);
                    var o = c != null ? c.StructureFind<IWaterSystemUser>() : null;
                    if (o == null || o == held || !seen.Add(o)) continue;
                    o.GetBoundingFootprint(out int oMinX, out int oMinZ, out int oMaxX, out int oMaxZ);
                    if (source ? !Covers(o, minX, minZ, maxX, maxZ) : !AnyInReach(tiles, oMinX, oMinZ, oMaxX, oMaxZ)) continue;
                    var w = o.GetComponent<IWaterSystemUser>();
                    found.Add(new WaterText.Neighbour
                    {
                        Name = o.FriendlyName,
                        Direction = Directions.Compass8((oMinX + oMaxX) / 2 - midX, (oMinZ + oMaxZ) / 2 - midZ),
                        State = !o.IsBuilt() ? WaterText.Link.Unbuilt : w.HasWater ? WaterText.Link.Water : WaterText.Link.Dry
                    });
                }
            return found;
        }

        /// <summary>Whether <paramref name="o"/> stands on a tile the given box reaches.</summary>
        private static bool Covers(Building o, int minX, int minZ, int maxX, int maxZ)
        {
            bool hit = false;
            o.ForEachTileInBounds((x, z, c) => { if (WaterText.InReach(x, z, minX, minZ, maxX, maxZ)) hit = true; });
            return hit;
        }

        private static bool AnyInReach(HashSet<GridPos> tiles, int minX, int minZ, int maxX, int maxZ)
        {
            foreach (var t in tiles) if (WaterText.InReach(t.X, t.Z, minX, minZ, maxX, maxZ)) return true;
            return false;
        }

        /// <summary>
        /// Whether a water wheel with all its workers reaches the building: IrrigationManager floods from every built
        /// wheel to the built water buildings in its reach, and on from theirs.
        /// </summary>
        private static bool ChainPumps(Building target)
        {
            var wheels = Player.inst.GetBuildingList("noria");
            for (int i = 0; wheels != null && i < wheels.Count; i++)
            {
                var wheel = wheels.data[i];
                var noria = wheel != null ? wheel.GetComponent<Noria>() : null;
                if (noria == null || !wheel.IsBuilt() || !noria.ShouldIrrigate()) continue;
                if (wheel == target || Reaches(wheel, target)) return true;
            }
            return false;
        }

        private static bool Reaches(Building from, Building target)
        {
            var seen = new HashSet<Building> { from };
            var queue = new Queue<Building>();
            queue.Enqueue(from);
            while (queue.Count > 0 && seen.Count < 5000)
            {
                var b = queue.Dequeue();
                b.GetBoundingFootprint(out int minX, out int minZ, out int maxX, out int maxZ);
                for (int x = minX - 1; x <= maxX + 1; x++)
                    for (int z = minZ - 1; z <= maxZ + 1; z++)
                    {
                        if (!WaterText.InReach(x, z, minX, minZ, maxX, maxZ)) continue;
                        var c = World.inst.GetCellData(x, z);
                        var o = c != null ? c.StructureFind<IWaterSystemUser>() : null;
                        if (o == null || !o.IsBuilt() || !seen.Add(o)) continue;
                        if (o == target) return true;
                        queue.Enqueue(o);
                    }
            }
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
                    int wet = 0, linked = 0;
                    var connected = new List<Building>();
                    foreach (var b in g)
                    {
                        if (Works(b)) wet++;
                        var w = b.GetComponent<IWaterSystemUser>();
                        if (w != null && w.IsConnected) { linked++; connected.Add(b); }
                    }
                    // Why connected pieces are dry: their own chain's wheel, or the distance.
                    bool pumping = false;
                    if (wet == 0) foreach (var b in connected) if (ChainPumps(b)) { pumping = true; break; }
                    A.SayQueued(WaterText.BuiltMany(name, g.Count, wet, linked, pumping));
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
            // Only looked up when it matters: connected and dry, because of its own chain's wheel or the distance.
            bool pumping = !w.IsConnected || w.HasWater || ChainPumps(b);
            return WaterText.Built(b.FriendlyName, IsSource(b), w.IsConnected, w.HasWater, pumping);
        }
    }
}
