using System;
using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>
    /// How the game passes water through its water system, copied step by step from IrrigationManager.FloodFillAqueducts
    /// of game 124r3s (10 November 2026), so that the mod can say before a piece is built whether water will reach it.
    ///
    /// From every built water wheel the game walks to the built water buildings in reach (WaterText.InReach), nearest
    /// first. A wheel with all its workers starts with 48 points of pressure; every building that gets water takes 2 of
    /// them (a bath house 6, a dragon bath 8), except one that an earlier wheel filled already. A pipe gets water only
    /// while pressure is left. Any other building (reservoir, bath house, dragon bath, dragon roost) also gets water when
    /// the building it was reached from had water or pressure, which is one step further than the pipes go.
    /// </summary>
    public static class WaterFlow
    {
        public enum Kind
        {
            Wheel,
            Aqueduct,
            Bathhouse,
            DragonBath,
            /// <summary>Reservoir, large reservoir, dragon roost.</summary>
            Other
        }

        public sealed class Node
        {
            public Kind Kind;
            /// <summary>A water wheel with all its workers (Noria.ShouldIrrigate).</summary>
            public bool Pumps;
            /// <summary>The building's bounding box in tiles.</summary>
            public int MinX, MinZ, MaxX, MaxZ;

            /// <summary>Result: water is in it.</summary>
            public bool HasWater;
            /// <summary>Result: linked to a built water wheel, with or without water.</summary>
            public bool Connected;
            /// <summary>Result: linked to a water wheel that pumps. Without water then, the water is too weak.</summary>
            public bool ReachedByPump;

            internal bool Visited;
        }

        /// <summary>Pressure of a wheel with all its workers (Noria.WaterPressure 24, doubled by the flood).</summary>
        public const int FullPressure = 48;

        public static int Cost(Kind kind) => kind == Kind.Bathhouse ? 6 : kind == Kind.DragonBath ? 8 : 2;

        /// <summary>
        /// Fills the results of every node the wheels reach.
        /// </summary>
        /// <param name="wheels">The built water wheels, in the order of the game's building list.</param>
        /// <param name="nodeAt">The built water building standing on a tile, or null.</param>
        public static void Flood(IList<Node> wheels, Func<int, int, Node> nodeAt, int gridWidth, int gridHeight)
        {
            var open = new Queue<Node>();
            var nextToWater = new Queue<bool>();
            var dirty = new List<Node>();
            foreach (var wheel in wheels)
            {
                dirty.Clear();
                int pressure = wheel.Pumps ? FullPressure : 0;
                open.Clear();
                nextToWater.Clear();
                open.Enqueue(wheel);
                nextToWater.Enqueue(false);
                wheel.Visited = true;
                dirty.Add(wheel);
                while (open.Count > 0)
                {
                    var node = open.Dequeue();
                    bool fromWater = nextToWater.Dequeue();
                    node.Connected = true;
                    if (wheel.Pumps) node.ReachedByPump = true;
                    bool left = pressure > 0;
                    bool had;
                    if (node.Kind == Kind.Wheel)
                    {
                        had = node.Pumps;
                        if (left) node.HasWater = true;
                    }
                    else
                    {
                        had = node.HasWater;
                        if (node.Kind == Kind.Aqueduct ? left : (left || fromWater)) node.HasWater = true;
                    }
                    if (left && !had) pressure -= Cost(node.Kind);
                    bool passesOn = left || had;
                    int minX = Clamp(node.MinX - 1, 0, gridWidth - 1), minZ = Clamp(node.MinZ - 1, 0, gridHeight - 1);
                    int maxX = Clamp(node.MaxX + 1, 0, gridWidth - 1), maxZ = Clamp(node.MaxZ + 1, 0, gridHeight - 1);
                    for (int x = minX; x <= maxX; x++)
                    {
                        for (int z = minZ; z <= maxZ; z++)
                        {
                            if ((x == minX || x == maxX) && (z == minZ || z == maxZ)) continue; // the four corners
                            var other = nodeAt(x, z);
                            if (other == null || other.Visited) continue;
                            other.Visited = true;
                            dirty.Add(other);
                            open.Enqueue(other);
                            nextToWater.Enqueue(passesOn);
                        }
                    }
                }
                foreach (var n in dirty) n.Visited = false;
            }
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
