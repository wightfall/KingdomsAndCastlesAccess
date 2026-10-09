using System;
using System.Collections.Generic;
using System.Text;

namespace KCAccess.Core
{
    /// <summary>Integer grid position. X grows to the east, Z grows to the north.</summary>
    public struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Z;

        public GridPos(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(GridPos other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is GridPos p && Equals(p);
        public override int GetHashCode() => (X * 397) ^ Z;
        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);
        public override string ToString() => X + ", " + Z;
    }

    /// <summary>Movement result for the map cursor.</summary>
    public enum MoveResult
    {
        Moved,
        Blocked
    }

    /// <summary>A keyboard cursor clamped to a rectangular grid.</summary>
    public sealed class GridCursor
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public GridPos Pos { get; private set; }

        public GridCursor(int width, int height)
        {
            Resize(width, height);
        }

        public void Resize(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            Pos = Clamp(Pos);
        }

        public GridPos Clamp(GridPos p) =>
            new GridPos(Math.Min(Math.Max(p.X, 0), Width - 1), Math.Min(Math.Max(p.Z, 0), Height - 1));

        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Height;

        public void Set(int x, int z) => Pos = Clamp(new GridPos(x, z));

        /// <summary>Move by (dx, dz) times steps. Stops at the map edge and reports Blocked when it could not move at all.</summary>
        public MoveResult Move(int dx, int dz, int steps = 1)
        {
            var target = Clamp(new GridPos(Pos.X + dx * steps, Pos.Z + dz * steps));
            if (target == Pos) return MoveResult.Blocked;
            Pos = target;
            return MoveResult.Moved;
        }

        /// <summary>
        /// Move in a direction until the signature of a cell changes (e.g. grass → forest).
        /// Returns the number of cells moved; 0 when already at the edge.
        /// </summary>
        public int MoveUntilChange(int dx, int dz, Func<int, int, string> signature, int maxSteps = 512)
        {
            string start = signature(Pos.X, Pos.Z);
            int moved = 0;
            int x = Pos.X, z = Pos.Z;
            while (moved < maxSteps)
            {
                int nx = x + dx, nz = z + dz;
                if (!InBounds(nx, nz)) break;
                x = nx;
                z = nz;
                moved++;
                if (signature(x, z) != start) break;
            }
            Pos = new GridPos(x, z);
            return moved;
        }
    }

    public static class Directions
    {
        private static readonly string[] Names8 = { Loc.N("north"), Loc.N("north east"), Loc.N("east"), Loc.N("south east"), Loc.N("south"), Loc.N("south west"), Loc.N("west"), Loc.N("north west") };

        /// <summary>Compass name of the vector (dx east, dz north).</summary>
        public static string Compass8(int dx, int dz)
        {
            if (dx == 0 && dz == 0) return Loc.T("here");
            double angle = Math.Atan2(dx, dz) * 180.0 / Math.PI; // 0 = north, 90 = east
            if (angle < 0) angle += 360.0;
            int idx = (int)Math.Round(angle / 45.0) % 8;
            return Loc.T(Names8[idx]);
        }

        /// <summary>"3 north, 2 east" style description of an offset.</summary>
        public static string Offset(int dx, int dz)
        {
            if (dx == 0 && dz == 0) return Loc.T("here");
            var sb = new StringBuilder();
            if (dz != 0) sb.Append(dz > 0 ? Loc.F("{0} north", Math.Abs(dz)) : Loc.F("{0} south", Math.Abs(dz)));
            if (dx != 0)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(dx > 0 ? Loc.F("{0} east", Math.Abs(dx)) : Loc.F("{0} west", Math.Abs(dx)));
            }
            return sb.ToString();
        }

        /// <summary>Chebyshev distance (king moves), matches how many cursor presses are needed with diagonals.</summary>
        public static int Distance(GridPos a, GridPos b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));

        public static double Euclid(GridPos a, GridPos b)
        {
            double dx = a.X - b.X, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Description of where target is relative to origin: "12 tiles north east (5 north, 11 east)".</summary>
        public static string Relative(GridPos from, GridPos to)
        {
            int dx = to.X - from.X, dz = to.Z - from.Z;
            if (dx == 0 && dz == 0) return Loc.T("at the cursor");
            int d = Distance(from, to);
            return Loc.P(d, "{0} tile {1}", "{0} tiles {1}", Compass8(dx, dz)) + ", " + Offset(dx, dz);
        }

        /// <summary>Sorts positions by distance from origin (nearest first), stable by Z then X.</summary>
        public static List<T> SortByDistance<T>(IEnumerable<T> items, Func<T, GridPos> pos, GridPos origin)
        {
            var list = new List<T>(items);
            list.Sort((a, b) =>
            {
                var pa = pos(a);
                var pb = pos(b);
                int c = Euclid(origin, pa).CompareTo(Euclid(origin, pb));
                if (c != 0) return c;
                c = pb.Z.CompareTo(pa.Z);
                return c != 0 ? c : pa.X.CompareTo(pb.X);
            });
            return list;
        }
    }
}
