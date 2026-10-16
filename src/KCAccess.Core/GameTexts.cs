using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace KCAccess.Core
{
    /// <summary>A text of the game: its I2 Localization term and its English text.</summary>
    public sealed class GameTerm
    {
        public string Term;
        public string English;
    }

    /// <summary>
    /// Translations of the game's own texts (menus, windows, tooltips, buildings, events, places, dialogue) in
    /// Languages/game: one file per language code, "term = translation" lines with the game's English text above each
    /// line as a "# English:" comment. They are optional: a term without a usable translation keeps the game's text
    /// in the game's language. Shipped files only carry terms and translations; the English comments are written on
    /// the player's computer from the installed game (<see cref="Build"/>).
    /// </summary>
    public static class GameTexts
    {
        public const string Folder = "game";
        public const string EnglishMark = "# English: ";
        public const string OwnSection = "# ===== Texts the installed game does not have (kept) =====";

        // {0}, {[PARAM]}, inline sprites and dialogue [var=...] values must survive translation; the game fills or draws them.
        private static readonly Regex Token = new Regex(@"\{\[?[A-Za-z0-9_]+\]?\}|<sprite[^>]*>|\[var=[^\]]*\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// The term of a dialogue text (diplomacy): conversation 10, entry 1, "Dialogue Text" →
        /// "Dialogue System/Conversation/D1010/Entry/D1001/Dialogue Text", as the game's texts name them.
        /// </summary>
        public static string DialogueTerm(int conversationId, int entryId, string field) =>
            "Dialogue System/Conversation/D" + (1000 + conversationId) + "/Entry/D" + (1000 + entryId) + "/" + field;

        /// <summary>The placeholder and sprite tokens of a game text, sorted (a token can occur more than once).</summary>
        public static List<string> Tokens(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (Match m in Token.Matches(text)) list.Add(m.Value.ToLowerInvariant().Replace(" ", string.Empty));
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>A translation is usable when it keeps every token of the English text and adds none.</summary>
        public static bool Matches(string english, string translation)
        {
            var a = Tokens(english);
            var b = Tokens(translation);
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>
        /// The usable translations of a file: term → text, only for terms the game has, non-blank, tokens matching the
        /// English. <paramref name="warn"/> is told about each ignored translation. Looked up without case: some windows
        /// ask for a term in another case ("Dragons" for the term "dragons").
        /// </summary>
        public static Dictionary<string, string> Usable(LocTable table, IDictionary<string, string> english, Action<string> warn = null)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (table == null || english == null) return result;
            foreach (var term in table.Keys)
            {
                string t = table.Raw(term);
                if (string.IsNullOrEmpty(t) || !english.TryGetValue(term, out var en)) continue;
                if (!Matches(en, t))
                {
                    warn?.Invoke("Translation of " + term + " ignored: its {placeholders} or sprites differ from the English \"" + en + "\"");
                    continue;
                }
                result[term] = t;
            }
            return result;
        }

        /// <summary>
        /// The player's file for the installed game: every game term in the game's order with its English as a comment.
        /// Per term the player's translation is kept when they changed it (it differs from <paramref name="previousShipped"/>,
        /// or there is no previous file and it is not blank); otherwise the shipped one is used. Translations of terms
        /// the game does not have are kept at the end. With no files at all this is the blank template.
        /// </summary>
        public static string Build(IList<GameTerm> terms, IEnumerable<string> header, LocTable user, LocTable shipped, LocTable previousShipped)
        {
            var lines = new List<string>();
            if (header != null) lines.AddRange(header);
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var gt in terms)
            {
                if (gt == null || string.IsNullOrEmpty(gt.Term) || !known.Add(gt.Term)) continue;
                string mine = user != null ? user.Raw(gt.Term) : null;
                string fresh = shipped != null ? shipped.Raw(gt.Term) : null;
                string value = fresh ?? string.Empty;
                if (mine != null)
                {
                    bool changedByPlayer = previousShipped != null ? mine != (previousShipped.Raw(gt.Term) ?? string.Empty) : mine.Length > 0;
                    if (changedByPlayer || fresh == null) value = mine;
                }
                lines.Add(string.Empty);
                lines.Add(EnglishMark + LocTable.Escape(gt.English ?? string.Empty));
                lines.Add(LocTable.Line(gt.Term, value));
            }
            var own = new List<string>();
            if (user != null)
                foreach (var key in user.Keys)
                {
                    string v = user.Raw(key);
                    if (!known.Contains(key) && !string.IsNullOrEmpty(v)) own.Add(LocTable.Line(key, v));
                }
            if (own.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add(OwnSection);
                lines.AddRange(own);
            }
            return string.Join("\r\n", lines.ToArray()) + "\r\n";
        }

        /// <summary>The header of a game text file: its language name, then how the file works.</summary>
        public static List<string> Header(string languageName)
        {
            var h = new List<string>();
            if (!string.IsNullOrEmpty(languageName)) h.Add("# Language: " + languageName);
            h.Add("# Translations of the game's own texts (menus, windows, tooltips, buildings, events, places, dialogue).");
            h.Add("# Each text: its English above, then \"term = translation\". Leave a translation blank to keep the game's");
            h.Add("# own text in the game's language. Keep {0}, {[NAME]}, <sprite ...> and [var=...] as they are. \\n is a line break.");
            h.Add("# The mod reads this file when its language (Ctrl+Shift+O) has the same code as the file name.");
            return h;
        }
    }
}
