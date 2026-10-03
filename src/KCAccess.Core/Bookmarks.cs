using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KCAccess.Core
{
    /// <summary>
    /// Map bookmarks: up to 9 numbered positions per kingdom, saved as simple text lines
    /// "kingdom|slot|x|z" so they survive game restarts.
    /// </summary>
    public sealed class Bookmarks
    {
        public const int Slots = 9;

        private readonly Dictionary<string, GridPos?[]> byKingdom = new Dictionary<string, GridPos?[]>(StringComparer.OrdinalIgnoreCase);

        private GridPos?[] For(string kingdom, bool create)
        {
            kingdom = kingdom ?? string.Empty;
            if (!byKingdom.TryGetValue(kingdom, out var arr) && create)
            {
                arr = new GridPos?[Slots];
                byKingdom[kingdom] = arr;
            }
            return arr;
        }

        public void Set(string kingdom, int slot, GridPos pos)
        {
            if (slot < 1 || slot > Slots) throw new ArgumentOutOfRangeException(nameof(slot));
            For(kingdom, true)[slot - 1] = pos;
        }

        public GridPos? Get(string kingdom, int slot)
        {
            if (slot < 1 || slot > Slots) return null;
            var arr = For(kingdom, false);
            return arr?[slot - 1];
        }

        public void Clear(string kingdom, int slot)
        {
            var arr = For(kingdom, false);
            if (arr != null && slot >= 1 && slot <= Slots) arr[slot - 1] = null;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var kv in byKingdom)
            {
                for (int i = 0; i < Slots; i++)
                {
                    var p = kv.Value[i];
                    if (!p.HasValue) continue;
                    sb.Append(kv.Key.Replace("|", "/")).Append('|').Append(i + 1).Append('|')
                      .Append(p.Value.X.ToString(CultureInfo.InvariantCulture)).Append('|')
                      .Append(p.Value.Z.ToString(CultureInfo.InvariantCulture)).Append('\n');
                }
            }
            return sb.ToString();
        }

        /// <summary>Loads lines written by Serialize; malformed lines are ignored.</summary>
        public static Bookmarks Parse(string text)
        {
            var b = new Bookmarks();
            if (string.IsNullOrEmpty(text)) return b;
            foreach (var raw in text.Split('\n'))
            {
                var parts = raw.Trim('\r').Split('|');
                if (parts.Length != 4) continue;
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int slot) || slot < 1 || slot > Slots) continue;
                if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)) continue;
                if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z)) continue;
                b.Set(parts[0], slot, new GridPos(x, z));
            }
            return b;
        }
    }
}
