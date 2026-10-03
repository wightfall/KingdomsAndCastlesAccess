using System;
using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>
    /// Sorts on-screen elements into natural reading order: top to bottom, then left to right.
    /// Elements whose vertical centres are within <c>rowTolerance</c> pixels count as one row.
    /// Coordinates are screen space with Y growing upwards (Unity convention).
    /// </summary>
    public static class ReadingOrder
    {
        public static List<T> Sort<T>(IEnumerable<T> items, Func<T, float> centerX, Func<T, float> centerY, float rowTolerance = 12f)
        {
            var list = new List<T>(items);
            // First sort strictly top-to-bottom.
            list.Sort((a, b) => centerY(b).CompareTo(centerY(a)));
            var result = new List<T>(list.Count);
            int i = 0;
            while (i < list.Count)
            {
                float rowY = centerY(list[i]);
                var row = new List<T>();
                while (i < list.Count && Math.Abs(centerY(list[i]) - rowY) <= rowTolerance)
                {
                    row.Add(list[i]);
                    i++;
                }
                row.Sort((a, b) => centerX(a).CompareTo(centerX(b)));
                result.AddRange(row);
            }
            return result;
        }
    }
}
