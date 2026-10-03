using System;
using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>How the navigation beacon should sound for a target at (dx east, dz north) from the cursor.</summary>
    public struct BeaconSound
    {
        /// <summary>Stereo pan: -1 = target is west (left ear), +1 = east (right ear).</summary>
        public float Pan;
        /// <summary>Pitch multiplier: higher when the target is north (ahead), lower when south (behind).</summary>
        public float Pitch;
        /// <summary>Seconds between pings: faster when closer.</summary>
        public float Interval;
        /// <summary>Distance in tiles (king moves).</summary>
        public int Distance;
        public bool Arrived;
    }

    public static class BeaconMath
    {
        public const float MinInterval = 0.18f;
        public const float MaxInterval = 1.2f;

        public static BeaconSound Compute(int dx, int dz)
        {
            int dist = Math.Max(Math.Abs(dx), Math.Abs(dz));
            if (dist == 0) return new BeaconSound { Pan = 0f, Pitch = 1f, Interval = MaxInterval, Distance = 0, Arrived = true };
            double angle = Math.Atan2(dx, dz); // 0 = north, +pi/2 = east
            float pan = (float)Math.Sin(angle);
            // North = 1.25x pitch, south = 0.8x pitch, east/west in between.
            double c = Math.Cos(angle);
            float pitch = (float)(c >= 0 ? 1.0 + 0.225 * c : 1.0 + 0.2 * c);
            // Interval grows with distance (log-ish), clamped.
            float interval = (float)(MinInterval + (MaxInterval - MinInterval) * Math.Min(1.0, Math.Log(1 + dist) / Math.Log(1 + 60)));
            return new BeaconSound { Pan = Clamp(pan, -1f, 1f), Pitch = pitch, Interval = interval, Distance = dist, Arrived = false };
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    public static class PathSteps
    {
        /// <summary>
        /// Expands waypoints into a tile-by-tile route (8-connected lines between consecutive waypoints),
        /// starting after <paramref name="start"/> and without duplicates.
        /// </summary>
        public static List<GridPos> Expand(GridPos start, IList<GridPos> waypoints)
        {
            var steps = new List<GridPos>();
            GridPos prev = start;
            foreach (var wp in waypoints)
            {
                foreach (var p in Line(prev, wp))
                {
                    if (p == prev) continue;
                    if (steps.Count > 0 && steps[steps.Count - 1] == p) continue;
                    if (steps.Count == 0 && p == start) continue;
                    steps.Add(p);
                }
                prev = wp;
            }
            return steps;
        }

        /// <summary>Bresenham line from a to b inclusive.</summary>
        public static IEnumerable<GridPos> Line(GridPos a, GridPos b)
        {
            int x0 = a.X, z0 = a.Z, x1 = b.X, z1 = b.Z;
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dz = -Math.Abs(z1 - z0), sz = z0 < z1 ? 1 : -1;
            int err = dx + dz;
            while (true)
            {
                yield return new GridPos(x0, z0);
                if (x0 == x1 && z0 == z1) yield break;
                int e2 = 2 * err;
                if (e2 >= dz)
                {
                    err += dz;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    z0 += sz;
                }
            }
        }

        /// <summary>Compass direction of one step ("north east"), used while walking.</summary>
        public static string StepDirection(GridPos from, GridPos to) => Directions.Compass8(to.X - from.X, to.Z - from.Z);
    }
}
