using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace KCAccess.Core
{
    /// <summary>Helpers that turn on-screen game text into something a screen reader can speak.</summary>
    public static class TextUtil
    {
        private static readonly Regex RichTag = new Regex(@"<\/?[a-zA-Z#][^<>]*>", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex(@"[ \t ]+", RegexOptions.Compiled);
        private static readonly Regex Newlines = new Regex(@"\s*[\r\n]+\s*", RegexOptions.Compiled);
        private static readonly Regex RepeatedStops = new Regex(@"\.(\s*\.)+", RegexOptions.Compiled);
        private static readonly Regex Rules = new Regex(@"[-=_]{3,}", RegexOptions.Compiled);
        private static readonly Regex SpriteTag = new Regex(@"<sprite[^>]*name=""?([^"" >]+)""?[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Removes Unity / TextMeshPro rich text tags (&lt;b&gt;, &lt;color=red&gt;, &lt;size=..&gt;),
        /// keeps sprite names as words, collapses whitespace, and turns line breaks into sentence breaks.
        /// </summary>
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string s = SpriteTag.Replace(text, m => " " + SpriteWord(m.Groups[1].Value) + " ");
            s = RichTag.Replace(s, string.Empty);
            s = Rules.Replace(s, ". ");
            s = s.Replace("\\n", "\n");
            s = Newlines.Replace(s.Trim(), ". ");
            s = Spaces.Replace(s, " ");
            s = s.Replace(" .", ".");
            s = RepeatedStops.Replace(s, ".");
            s = s.Replace(":.", ":").Replace("!.", "!").Replace("?.", "?").Replace(",.", ",");
            s = Spaces.Replace(s, " ");
            s = s.Trim();
            if (s.StartsWith(".")) s = s.Substring(1).TrimStart();
            return s;
        }

        /// <summary>Spoken word for an inline TextMeshPro sprite ("icon_wood" → "wood"). Decorative icons become empty.</summary>
        public static string SpriteWord(string sprite)
        {
            if (string.IsNullOrEmpty(sprite)) return string.Empty;
            string n = sprite.ToLowerInvariant();
            if (n.StartsWith("icon_")) n = n.Substring(5);
            switch (n)
            {
                case "magglass":
                case "arrow":
                case "bullet":
                    return string.Empty;
                case "armaments":
                    return "armaments";
                case "apple":
                    return "apples";
            }
            return n.Replace('_', ' ');
        }

        /// <summary>Joins non-empty cleaned parts with the given separator.</summary>
        public static string Join(string separator, IEnumerable<string> parts)
        {
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                var c = Clean(p);
                if (c.Length == 0) continue;
                if (sb.Length > 0) sb.Append(separator);
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static string Join(string separator, params string[] parts) => Join(separator, (IEnumerable<string>)parts);

        /// <summary>Joins parts as sentences: "A. B. C".</summary>
        public static string Sentences(params string[] parts)
        {
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                var c = Clean(p);
                if (c.Length == 0) continue;
                if (sb.Length > 0)
                {
                    char last = sb[sb.Length - 1];
                    if (last != '.' && last != '!' && last != '?' && last != ':') sb.Append('.');
                    sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>"1 tree", "3 trees". Handles simple English plurals.</summary>
        public static string Plural(int count, string singular, string plural = null)
        {
            if (count == 1) return "1 " + singular;
            return count + " " + (plural ?? singular + "s");
        }

        public static string Percent(float fraction)
        {
            if (float.IsNaN(fraction) || float.IsInfinity(fraction)) fraction = 0f;
            int p = (int)Math.Round(fraction * 100f);
            return p + " percent";
        }

        /// <summary>Converts identifiers like "smallhouse", "AdvTown", "wood_castle_block" into spoken words.</summary>
        public static string Humanize(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (c == '_' || c == '-')
                {
                    sb.Append(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(c) && char.IsLower(id[i - 1])) sb.Append(' ');
                sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
            }
            return Spaces.Replace(sb.ToString(), " ").Trim();
        }

        /// <summary>True when the text contains something worth speaking (letters or digits).</summary>
        public static bool HasContent(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c)) return true;
            }
            return false;
        }

        /// <summary>Case-insensitive "starts with" used for type-ahead search.</summary>
        public static bool StartsWithIgnoreCase(string text, string prefix)
        {
            if (prefix == null) return true;
            if (text == null) return false;
            return Clean(text).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
