using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// This file is also compiled into the setup program (KCAccess.Installer, INSTALLER defined), where the types are
// internal so the test project, which references both, sees only one copy.
namespace KCAccess.Core
{
    /// <summary>
    /// One language file: "English text = translated text" lines (gettext style, the English text is the key).
    /// Header comments "# Language: ไทย (Thai)" and "# Game language: Thai" name the language; other lines starting
    /// with # are comments. Escapes: \n is a line break, \= an equals sign, \\ a backslash.
    /// </summary>
#if INSTALLER
    internal
#else
    public
#endif
    sealed class LocTable
    {
        private readonly Dictionary<string, string> entries = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> keys = new List<string>();
        private readonly HashSet<string> warned = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Name from the "# Language:" header, e.g. "ไทย (Thai)"; empty when missing.</summary>
        public string LanguageName = string.Empty;

        /// <summary>The game's language (I2 name, e.g. "German") from the "# Game language:" header; empty for mod-only languages.</summary>
        public string GameLanguage = string.Empty;

        /// <summary>The language code: the file name without .txt ("ru", "pt-BR"), set by whoever loads the file.</summary>
        public string Code = string.Empty;

        /// <summary>A language code from a "# Plural rule:" header, for a file whose name is not a language code.</summary>
        public string PluralRule = string.Empty;

        /// <summary>The language whose plural rule chooses among "form|form|form" translations.</summary>
        public string RuleLanguage => PluralRule.Length > 0 ? PluralRule : Code;

        /// <summary>Called once per broken translation (placeholder mismatch), for the log.</summary>
        public Action<string> Warn;

        /// <summary>Keys in file order (each key once).</summary>
        public IList<string> Keys => keys;

        public int Count => keys.Count;

        public bool Has(string key) => key != null && entries.ContainsKey(key);

        /// <summary>The raw value written in the file ("" when the translation is blank), or null when the key is missing.</summary>
        public string Raw(string key) => key != null && entries.TryGetValue(key, out var v) ? v : null;

        public void Set(string key, string value)
        {
            if (key == null) return;
            if (!entries.ContainsKey(key)) keys.Add(key);
            entries[key] = value ?? string.Empty;
        }

        /// <summary>
        /// The translation of <paramref name="english"/>, or null when it is missing, blank, or its {placeholders}
        /// do not match the English text (then English is used and the problem is reported once).
        /// </summary>
        public string Lookup(string english)
        {
            if (english == null || !entries.TryGetValue(english, out var t) || t.Length == 0) return null;
            if (!Loc.PlaceholdersMatch(english, t))
            {
                if (warned.Add(english))
                    Warn?.Invoke("Translation ignored, its {placeholders} do not match the English text: \"" + english + "\" = \"" + t + "\"");
                return null;
            }
            return t;
        }

        public static LocTable Parse(string text)
        {
            var table = new LocTable();
            if (string.IsNullOrEmpty(text)) return table;
            foreach (var raw in SplitLines(text))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#')
                {
                    if (TryHeader(line, "Language:", out var name)) table.LanguageName = name;
                    else if (TryHeader(line, "Game language:", out var game)) table.GameLanguage = game;
                    else if (TryHeader(line, "Plural rule:", out var rule)) table.PluralRule = rule;
                    continue;
                }
                if (TrySplit(line, out var key, out var value)) table.Set(key, value);
            }
            return table;
        }

        /// <summary>Lines of a file, without line breaks and without a byte order mark.</summary>
        public static List<string> SplitLines(string text)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            if (text[0] == '﻿') text = text.Substring(1);
            lines.AddRange(text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
            return lines;
        }

        private static bool TryHeader(string line, string label, out string value)
        {
            value = null;
            string s = line.TrimStart('#').TrimStart();
            if (!s.StartsWith(label, StringComparison.OrdinalIgnoreCase)) return false;
            value = s.Substring(label.Length).Trim();
            return true;
        }

        /// <summary>Splits "English = translation" at the first " = " (an "English =" line has a blank translation).</summary>
        public static bool TrySplit(string line, out string key, out string value)
        {
            key = null;
            value = null;
            if (line == null) return false;
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#') return false;
            int i = line.IndexOf(" = ", StringComparison.Ordinal);
            string k, v;
            if (i >= 0)
            {
                k = line.Substring(0, i);
                v = line.Substring(i + 3);
            }
            else if (line.EndsWith(" =", StringComparison.Ordinal))
            {
                k = line.Substring(0, line.Length - 2);
                v = string.Empty;
            }
            else return false;
            k = Unescape(k.Trim());
            if (k.Length == 0) return false;
            key = k;
            value = Unescape(v.Trim());
            return true;
        }

        public static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0) return s ?? string.Empty;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[i + 1];
                    if (n == 'n') { sb.Append('\n'); i++; continue; }
                    if (n == '=') { sb.Append('='); i++; continue; }
                    if (n == '\\') { sb.Append('\\'); i++; continue; }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Escapes a key for writing: backslashes, line breaks and every equals sign.</summary>
        public static string EscapeKey(string s) => Escape(s).Replace("=", "\\=");

        /// <summary>Escapes a translation for writing: backslashes and line breaks.</summary>
        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n");
        }

        /// <summary>One file line for a key and its translation.</summary>
        public static string Line(string key, string value)
        {
            string v = Escape(value);
            return EscapeKey(key) + " =" + (v.Length > 0 ? " " + v : string.Empty);
        }
    }

    /// <summary>
    /// Translation of everything the mod itself says or shows. The English text is the key:
    /// <c>Loc.T("Selection cleared")</c>, <c>Loc.F("{0} tiles north", n)</c>, <c>Loc.P(n, "{0} tree", "{0} trees")</c>.
    /// <c>Loc.N("...")</c> only marks a text for the language files where it is stored now and translated later with
    /// <c>Loc.T(variable)</c>. Without a language table every call returns the English text.
    /// </summary>
#if INSTALLER
    internal
#else
    public
#endif
    static class Loc
    {
        private static readonly Regex Placeholder = new Regex(@"\{[A-Za-z0-9_]+\}", RegexOptions.CultureInvariant);
        private static readonly Regex Numbered = new Regex(@"\{([0-9]+)\}", RegexOptions.CultureInvariant);

        /// <summary>The active language, or null for English.</summary>
        public static LocTable Table { get; private set; }

        public static void Use(LocTable table) => Table = table;

        /// <summary>Translated text, or the English text itself.</summary>
        public static string T(string english)
        {
            if (english == null) return null;
            var t = Table;
            return t != null ? t.Lookup(english) ?? english : english;
        }

        /// <summary>Marks a text for translation without translating it now (translate it later with T).</summary>
        public static string N(string english) => english;

        /// <summary>Translated format text with {0}, {1} … filled in (word order is up to the translation).</summary>
        public static string F(string english, params object[] args) => Format(T(english), args);

        /// <summary>
        /// Count with the right form: <paramref name="one"/> for 1, otherwise <paramref name="many"/>. {0} is the count,
        /// {1} … are <paramref name="more"/>. Languages without plurals translate both the same. A translation can list
        /// several forms separated by | ("{0} клетка|{0} клетки|{0} клеток"); the language's plural rule picks one.
        /// </summary>
        public static string P(int count, string one, string many, params object[] more)
        {
            var args = new object[1 + (more != null ? more.Length : 0)];
            args[0] = count;
            if (more != null) Array.Copy(more, 0, args, 1, more.Length);
            return Format(Form(T(count == 1 ? one : many), count), args);
        }

        /// <summary>
        /// The form of a "form|form|form" text for <paramref name="count"/> by the active language's plural rule (a
        /// missing form falls back to the last one). A text without | is returned unchanged.
        /// </summary>
        public static string Form(string text, int count)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('|') < 0) return text;
            var forms = text.Split('|');
            int i = Plurals.Index(Table != null ? Table.RuleLanguage : null, count);
            if (i >= forms.Length) i = forms.Length - 1;
            return forms[i].Trim();
        }

        /// <summary>
        /// Fills {0}, {1} … with the arguments (numbers in invariant culture). Anything else in braces, such as the
        /// key names {Walk} or a missing argument, stays as it is, so a translation can never make this throw.
        /// </summary>
        public static string Format(string format, params object[] args)
        {
            if (string.IsNullOrEmpty(format) || format.IndexOf('{') < 0) return format ?? string.Empty;
            return Numbered.Replace(format, m =>
            {
                if (args == null || !int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int i) || i >= args.Length) return m.Value;
                var a = args[i];
                if (a == null) return string.Empty;
                return a is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : a.ToString();
            });
        }

        /// <summary>The {placeholders} of a text ({0}, {Walk} …).</summary>
        public static HashSet<string> Placeholders(string text)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return set;
            foreach (Match m in Placeholder.Matches(text)) set.Add(m.Value);
            return set;
        }

        /// <summary>A translation must use exactly the {placeholders} of the English text (in any order).</summary>
        public static bool PlaceholdersMatch(string english, string translation) => Placeholders(english).SetEquals(Placeholders(translation));
    }

    /// <summary>
    /// Plural rules for whole numbers (from the Unicode CLDR): which form of a "form|form|form" translation a count
    /// takes. Forms are numbered in the order one, few, many (or one, other for two-form languages).
    /// </summary>
#if INSTALLER
    internal
#else
    public
#endif
    static class Plurals
    {
        /// <summary>Number of forms the rule of <paramref name="language"/> uses (1 for languages without plurals).</summary>
        public static int FormCount(string language)
        {
            switch (Base(language))
            {
                case "ru": case "uk": case "be": case "pl": case "cs": case "sk": case "hr": case "sr": case "bs": case "ro": case "lt": case "lv":
                    return 3;
                case "ja": case "ko": case "zh": case "th": case "vi": case "id": case "ms":
                    return 1;
                default:
                    return 2;
            }
        }

        /// <summary>Index of the form a count takes in <paramref name="language"/> (a code such as "ru" or "pt-BR").</summary>
        public static int Index(string language, int n)
        {
            if (n < 0) n = -n;
            int d = n % 10, h = n % 100;
            bool teen = h >= 11 && h <= 19;
            switch (Base(language))
            {
                case "ru": case "uk": case "be": case "hr": case "sr": case "bs":
                    // 1, 21, 31 … | 2-4, 22-24 … | 0, 5-20, 25-30 …
                    if (d == 1 && h != 11) return 0;
                    return d >= 2 && d <= 4 && (h < 12 || h > 14) ? 1 : 2;
                case "pl":
                    // 1 | 2-4, 22-24 … | 0, 5-21, 25-31 …
                    if (n == 1) return 0;
                    return d >= 2 && d <= 4 && (h < 12 || h > 14) ? 1 : 2;
                case "cs": case "sk":
                    // 1 | 2-4 | everything else
                    if (n == 1) return 0;
                    return n >= 2 && n <= 4 ? 1 : 2;
                case "ro":
                    // 1 | 0, 2-19, 101-119 … | 20 and more ("20 de ...")
                    if (n == 1) return 0;
                    return n == 0 || h >= 1 && h <= 19 ? 1 : 2;
                case "lt":
                    // 1, 21 … | 2-9, 22-29 … | 0, 10-20, 30 …
                    if (d == 1 && !teen) return 0;
                    return d >= 2 && !teen ? 1 : 2;
                case "lv":
                    // 0, 10-20, 30 … | 1, 21 … | everything else
                    if (d == 0 || teen) return 0;
                    return d == 1 && h != 11 ? 1 : 2;
                case "fr": case "pt":
                    // 0 and 1 take the singular
                    return n <= 1 ? 0 : 1;
                case "ja": case "ko": case "zh": case "th": case "vi": case "id": case "ms":
                    return 0;
                default:
                    return n == 1 ? 0 : 1;
            }
        }

        /// <summary>"pt-BR" → "pt", "zh-Hans" → "zh"; lower case.</summary>
        private static string Base(string language)
        {
            if (string.IsNullOrEmpty(language)) return "en";
            string s = language.Trim().ToLowerInvariant();
            int i = s.IndexOfAny(new[] { '-', '_' });
            return i > 0 ? s.Substring(0, i) : s;
        }
    }
}
