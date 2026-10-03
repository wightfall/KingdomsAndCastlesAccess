using System;
using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>
    /// A* over the tile grid with 8 directions. Diagonal moves are only allowed when both side tiles are
    /// passable (no squeezing between two blocked corners). Costs are per tile entered (>= 1).
    /// </summary>
    public static class GridPathfinder
    {
        private static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>
        /// Returns the tiles to step on from start (exclusive) to goal (inclusive), or null when unreachable.
        /// Searching stops after <paramref name="maxNodes"/> expansions.
        /// </summary>
        public static List<GridPos> Find(int width, int height, GridPos start, GridPos goal, Func<int, int, bool> passable, Func<int, int, int> cost = null, int maxNodes = 200000)
        {
            if (width <= 0 || height <= 0) return null;
            if (!In(start, width, height) || !In(goal, width, height)) return null;
            if (start == goal) return new List<GridPos>();
            if (!passable(goal.X, goal.Z)) return null;
            int n = width * height;
            var g = new Dictionary<int, int>();
            var parent = new Dictionary<int, int>();
            var closed = new HashSet<int>();
            var open = new SortedSet<(int f, int h, int id)>();
            int startId = start.Z * width + start.X, goalId = goal.Z * width + goal.X;
            g[startId] = 0;
            open.Add((H(start, goal), H(start, goal), startId));
            int expanded = 0;
            while (open.Count > 0 && expanded < maxNodes)
            {
                var cur = open.Min;
                open.Remove(cur);
                int id = cur.id;
                if (closed.Contains(id)) continue;
                closed.Add(id);
                expanded++;
                if (id == goalId) return Build(parent, startId, goalId, width);
                int cx = id % width, cz = id / width;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + DX[d], nz = cz + DZ[d];
                    if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;
                    if (!passable(nx, nz)) continue;
                    bool diagonal = d >= 4;
                    if (diagonal && (!passable(cx + DX[d], cz) || !passable(cx, cz + DZ[d]))) continue;
                    int nid = nz * width + nx;
                    if (closed.Contains(nid)) continue;
                    int step = Math.Max(1, cost != null ? cost(nx, nz) : 1) * (diagonal ? 14 : 10);
                    int ng = g[id] + step;
                    if (g.TryGetValue(nid, out int old) && old <= ng) continue;
                    g[nid] = ng;
                    parent[nid] = id;
                    int h = H(new GridPos(nx, nz), goal);
                    open.Add((ng + h, h, nid));
                }
            }
            return null;
        }

        /// <summary>Octile distance heuristic (x10 scale), admissible because every tile costs at least 1.</summary>
        private static int H(GridPos a, GridPos b)
        {
            int dx = Math.Abs(a.X - b.X), dz = Math.Abs(a.Z - b.Z);
            return 10 * Math.Max(dx, dz) + 4 * Math.Min(dx, dz);
        }

        private static bool In(GridPos p, int w, int h) => p.X >= 0 && p.Z >= 0 && p.X < w && p.Z < h;

        private static List<GridPos> Build(Dictionary<int, int> parent, int startId, int goalId, int width)
        {
            var path = new List<GridPos>();
            int id = goalId;
            while (id != startId)
            {
                path.Add(new GridPos(id % width, id / width));
                id = parent[id];
            }
            path.Reverse();
            return path;
        }
    }
}
