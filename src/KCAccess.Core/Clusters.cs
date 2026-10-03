using System;
using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>Groups matching grid cells into connected regions (4-neighbour flood fill).</summary>
    public static class Clusters
    {
        public static List<List<GridPos>> Find(int width, int height, Func<int, int, bool> match, int maxClusters = 2000)
        {
            var result = new List<List<GridPos>>();
            if (width <= 0 || height <= 0) return result;
            var seen = new bool[width * height];
            var stack = new Stack<GridPos>();
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = z * width + x;
                    if (seen[idx]) continue;
                    seen[idx] = true;
                    if (!match(x, z)) continue;
                    var cluster = new List<GridPos>();
                    stack.Push(new GridPos(x, z));
                    while (stack.Count > 0)
                    {
                        var p = stack.Pop();
                        cluster.Add(p);
                        Visit(p.X + 1, p.Z);
                        Visit(p.X - 1, p.Z);
                        Visit(p.X, p.Z + 1);
                        Visit(p.X, p.Z - 1);
                    }
                    result.Add(cluster);
                    if (result.Count >= maxClusters) return result;
                }
            }
            return result;

            void Visit(int nx, int nz)
            {
                if (nx < 0 || nz < 0 || nx >= width || nz >= height) return;
                int i = nz * width + nx;
                if (seen[i]) return;
                seen[i] = true;
                if (match(nx, nz)) stack.Push(new GridPos(nx, nz));
            }
        }

        /// <summary>The cell of the cluster closest to origin.</summary>
        public static GridPos Nearest(List<GridPos> cluster, GridPos origin)
        {
            GridPos best = cluster[0];
            double bestD = double.MaxValue;
            foreach (var p in cluster)
            {
                double d = Directions.Euclid(origin, p);
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }
            return best;
        }
    }
}
