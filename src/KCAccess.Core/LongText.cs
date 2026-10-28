using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace KCAccess.Core
{
    /// <summary>
    /// Long texts (the main menu's news, the credits, a load error's details) split into lines a screen reader user
    /// steps through, instead of one speech that cannot be paused or skipped. Lines are the text's own line breaks; a line
    /// longer than <see cref="MaxLine"/> is split at sentence ends; an empty line starts a new group (a paragraph), which
    /// Page Up and Page Down jump between.
    /// </summary>
    public static class LongText
    {
        /// <summary>From this length (or <see cref="ReaderLines"/> lines) Enter on a text opens the reader.</summary>
        public const int ReaderLength = 300;

        public const int ReaderLines = 3;

        /// <summary>Longer texts are only summarised when the focus reaches them.</summary>
        public const int SummaryLength = 2000;

        public const int MaxLine = 200;

        public struct Line
        {
            public string Text;
            /// <summary>The first line of a group (a paragraph, or a title in the credits).</summary>
            public bool StartsGroup;
        }

        private static readonly Regex Breaks = new Regex(@"<br\s*/?>|\\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex SentenceEnd = new Regex(@"(?<=[.!?\u3002\uFF01\uFF1F])\s+", RegexOptions.CultureInvariant);

        public static List<Line> Lines(string raw)
        {
            var lines = new List<Line>();
            if (string.IsNullOrEmpty(raw)) return lines;
            bool group = true;
            foreach (string part in Breaks.Replace(raw.Replace("\r\n", "\n").Replace('\r', '\n'), "\n").Split('\n'))
            {
                string clean = TextUtil.Clean(part);
                if (!TextUtil.HasContent(clean))
                {
                    group = true;
                    continue;
                }
                foreach (string piece in Pieces(clean))
                {
                    lines.Add(new Line { Text = piece, StartsGroup = group });
                    group = false;
                }
            }
            return lines;
        }

        /// <summary>A long line as its sentences; a short one as it is.</summary>
        private static IEnumerable<string> Pieces(string line)
        {
            if (line.Length <= MaxLine)
            {
                yield return line;
                yield break;
            }
            foreach (string s in SentenceEnd.Split(line))
                if (TextUtil.HasContent(s)) yield return s.Trim();
        }

        /// <summary>Whether Enter on this text should open the reader.</summary>
        public static bool WorthReading(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return false;
            return TextUtil.Clean(raw).Length >= ReaderLength || Lines(raw).Count >= ReaderLines;
        }

        /// <summary>
        /// What the focus says on a very long text: its first line and how to read the rest, or null when the text is
        /// short enough to be read whole.
        /// </summary>
        public static string Summary(string raw)
        {
            if (string.IsNullOrEmpty(raw) || TextUtil.Clean(raw).Length <= SummaryLength) return null;
            var lines = Lines(raw);
            if (lines.Count == 0) return null;
            return TextUtil.Sentences(lines[0].Text, Loc.P(lines.Count, "Long text, {0} line. Enter reads it line by line", "Long text, {0} lines. Enter reads it line by line"));
        }

        /// <summary>Counts how many lines follow each group start ("The Brave Knights, 268 names").</summary>
        public static int LinesInGroup(IList<Line> lines, int start)
        {
            int n = 0;
            for (int i = start + 1; i < lines.Count && !lines[i].StartsGroup; i++) n++;
            return n;
        }
    }
}
