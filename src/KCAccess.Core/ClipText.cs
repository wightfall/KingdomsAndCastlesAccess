using System.Text;

namespace KCAccess.Core
{
    /// <summary>
    /// Text going to and coming from the Windows clipboard: what is said after a copy, and what of a pasted text
    /// may go into a one-line field (a share code copied from a chat often carries spaces or a line break).
    /// </summary>
    public static class ClipText
    {
        /// <summary>Longer copies are not read back, only counted.</summary>
        public const int SpokenLimit = 80;

        /// <summary>"Copied: T1003", "Copied, 240 characters", or that there was nothing to copy.</summary>
        public static string Announce(string copied)
        {
            if (string.IsNullOrEmpty(copied)) return Loc.T("Nothing to copy here");
            if (copied.Length <= SpokenLimit && copied.IndexOf('\n') < 0) return Loc.F("Copied: {0}", copied);
            return Loc.P(copied.Length, "Copied, {0} character", "Copied, {0} characters");
        }

        /// <summary>
        /// The first sentence of a list's title, for the top of a copied list: the titles of the mod's lists go on to
        /// say which keys work in them ("Key list. Up and Down read one key at a time ...").
        /// </summary>
        public static string Heading(string title)
        {
            if (string.IsNullOrEmpty(title)) return string.Empty;
            int end = -1;
            foreach (string stop in new[] { ". ", "\u3002", "\uff0e" })
            {
                int at = title.IndexOf(stop, System.StringComparison.Ordinal);
                if (at > 0 && (end < 0 || at < end)) end = at;
            }
            return end > 0 ? title.Substring(0, end) : title.TrimEnd('.');
        }

        /// <summary>
        /// The clipboard text as one line: line breaks and tabs become one space, other control characters go, the
        /// ends are trimmed. With <paramref name="digitsOnly"/> everything but digits goes (amount fields).
        /// </summary>
        public static string OneLine(string clip, bool digitsOnly = false)
        {
            if (string.IsNullOrEmpty(clip)) return string.Empty;
            var sb = new StringBuilder(clip.Length);
            bool space = false;
            foreach (char c in clip)
            {
                if (digitsOnly)
                {
                    if (c >= '0' && c <= '9') sb.Append(c);
                    continue;
                }
                if (c == '\r' || c == '\n' || c == '\t' || c == ' ')
                {
                    space = sb.Length > 0;
                    continue;
                }
                if (char.IsControl(c)) continue;
                if (space) sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// <paramref name="current"/> with the clipboard text put in place of the selection (or at the caret when
        /// <paramref name="start"/> equals <paramref name="end"/>), cut so the field's limit holds (0 = no limit).
        /// </summary>
        public static string Insert(string current, int start, int end, string clip, int limit, bool digitsOnly, out string pasted, out int caret)
        {
            current = current ?? string.Empty;
            if (start > end) { int t = start; start = end; end = t; }
            start = System.Math.Max(0, System.Math.Min(start, current.Length));
            end = System.Math.Max(start, System.Math.Min(end, current.Length));
            pasted = OneLine(clip, digitsOnly);
            if (limit > 0)
            {
                int room = limit - (current.Length - (end - start));
                if (pasted.Length > room) pasted = pasted.Substring(0, System.Math.Max(0, room));
            }
            caret = start + pasted.Length;
            return current.Substring(0, start) + pasted + current.Substring(end);
        }

        /// <summary>Said after a paste: what arrived, or why nothing did.</summary>
        public static string AnnouncePaste(string pasted, string after, bool clipboardEmpty)
        {
            if (clipboardEmpty) return Loc.T("The clipboard is empty");
            if (string.IsNullOrEmpty(pasted)) return Loc.T("Nothing was pasted: the field is full or takes no such text");
            return pasted == after ? Loc.F("Pasted {0}", pasted) : Loc.F("Pasted {0}. The field now reads {1}", pasted, after);
        }
    }
}
