using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using KCAccess.Core;
using KCAccess.Installer;
using Xunit;

// Loc.Table is global: tests that switch languages must not run next to tests that expect English.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace KCAccess.Tests
{
    /// <summary>
    /// Finds every translatable text in the sources: the first string literal argument of Loc.T, Loc.F and Loc.N, and the
    /// two literals of Loc.P (adjacent literals joined with + count as one). Also writes template.txt and keeps the
    /// shipped language files in the template's order (tools/loc-extract.ps1).
    /// </summary>
    public static class LocExtractor
    {
        private static readonly Regex Call = new Regex(@"\bLoc\.(T|F|N|P)\s*\(", RegexOptions.CultureInvariant);

        public static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "KCAccess.sln"))) dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("KCAccess.sln not found above " + AppContext.BaseDirectory);
            return dir.FullName;
        }

        public static string LanguagesDir => Path.Combine(RepoRoot(), "src", "KCAccess", "Languages");

        public static IEnumerable<string> SourceFiles()
        {
            string src = Path.Combine(RepoRoot(), "src");
            return Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) && !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                .OrderBy(p => p, StringComparer.Ordinal);
        }

        /// <summary>Keys in order of appearance, each once, with the file it first appears in.</summary>
        public static List<KeyValuePair<string, string>> Extract()
        {
            var result = new List<KeyValuePair<string, string>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string root = RepoRoot();
            foreach (var file in SourceFiles())
            {
                string rel = file.Substring(root.Length + 1).Replace('\\', '/');
                string category = Category(rel);
                foreach (var key in ExtractFrom(StripComments(File.ReadAllText(file))))
                    if (seen.Add(key)) result.Add(new KeyValuePair<string, string>(category, key));
            }
            // Group by category in the order players meet them (stable: source order inside a category).
            var ordered = new List<KeyValuePair<string, string>>();
            foreach (var c in Categories)
                foreach (var kv in result)
                    if (kv.Key == c[0]) ordered.Add(kv);
            foreach (var kv in result)
                if (!Array.Exists(Categories, c => c[0] == kv.Key)) ordered.Add(kv);
            return ordered;
        }

        /// <summary>Section names of the language files (what a translator looks for) and the source files in them.</summary>
        public static readonly string[][] Categories =
        {
            new[] { "Speech, screens and menus", "AccessController", "UINavigator", "UIText", "Special", "ScreenDetector", "GameScreens", "ListMenu", "NavList", "TextUtil", "SteamOverlay", "Diagnostics", "DialoguePatches", "Plugin", "KeyboardGuard", "OsKeyboard", "WebLinks" },
            new[] { "Help and key lists", "HelpText", "KeyHelp", "KeysMenu", "Bindings" },
            new[] { "Mod settings, languages and sound cues", "ModSettingsMenu", "ModLanguage", "Cues", "AudioCues" },
            new[] { "Map, tiles and cursor", "MapController", "CellInfo", "AreaSurvey", "GridMath", "ResourceNames", "VirtualPointer" },
            new[] { "Scanner, navigation and problems", "Scanner", "Navigator", "Navigation", "Problems", "Alerts", "Thoughts" },
            new[] { "Building and castle walls", "BuildMenu", "PlacementText", "CastleStacker", "CastleStack" },
            new[] { "Kingdom status, notifications and events", "StatusMenu", "GameEvents", "NotificationLog", "LogBrowser", "GamePatches" },
            new[] { "Army, ships, carts, routes and diplomacy", "UnitText", "Routes", "Diplomacy" },
            new[] { "Controller", "ControllerMap", "ControllerInput" },
            new[] { "Streaming and Twitch", "Streaming" },
            new[] { "Setup program", "KCAccess.Installer/" },
        };

        public static string Category(string relPath)
        {
            string name = Path.GetFileNameWithoutExtension(relPath);
            foreach (var c in Categories)
                for (int i = 1; i < c.Length; i++)
                    if (c[i].EndsWith("/") ? relPath.Contains(c[i]) : name == c[i]) return c[0];
            return "Other";
        }

        /// <summary>Removes whole-line // and /// comments (examples in documentation are not texts).</summary>
        public static string StripComments(string code)
        {
            var lines = code.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++) if (lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal)) lines[i] = string.Empty;
            return string.Join("\n", lines);
        }

        public static List<string> ExtractFrom(string code)
        {
            var keys = new List<string>();
            foreach (Match m in Call.Matches(code))
            {
                int i = m.Index + m.Length;
                if (m.Groups[1].Value == "P" && !SkipArgument(code, ref i)) continue; // the count comes first
                string first = ReadLiteral(code, ref i);
                if (first == null) continue;
                if (m.Groups[1].Value == "P")
                {
                    keys.Add(first);
                    SkipSpace(code, ref i);
                    if (i < code.Length && code[i] == ',')
                    {
                        i++;
                        string second = ReadLiteral(code, ref i);
                        if (second != null) keys.Add(second);
                    }
                }
                else keys.Add(first);
            }
            return keys;
        }

        /// <summary>Moves past one argument and its comma (brackets and strings inside it are skipped).</summary>
        private static bool SkipArgument(string s, ref int i)
        {
            int depth = 0;
            for (; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"')
                {
                    for (i++; i < s.Length && s[i] != '"'; i++) if (s[i] == '\\') i++;
                    continue;
                }
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth == 0) return false;
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    i++;
                    return true;
                }
            }
            return false;
        }

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        /// <summary>One or more string literals joined with +; null when the argument is not a literal.</summary>
        private static string ReadLiteral(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length || s[i] != '"') return null;
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length || s[i] != '"') return null;
                i++;
                while (i < s.Length && s[i] != '"')
                {
                    char c = s[i];
                    if (c == '\\' && i + 1 < s.Length)
                    {
                        char n = s[i + 1];
                        switch (n)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case '0': sb.Append('\0'); break;
                            case 'u':
                                sb.Append((char)int.Parse(s.Substring(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                                i += 4;
                                break;
                            default: sb.Append(n); break;
                        }
                        i += 2;
                        continue;
                    }
                    sb.Append(c);
                    i++;
                }
                i++; // closing quote
                int save = i;
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == '+')
                {
                    int j = i + 1;
                    SkipSpace(s, ref j);
                    if (j < s.Length && s[j] == '"')
                    {
                        i = j;
                        continue;
                    }
                }
                SkipSpace(s, ref save);
                // Only a whole literal argument counts ("text" followed by , or ) ).
                if (save < s.Length && (s[save] == ',' || s[save] == ')')) { i = save; return sb.ToString(); }
                return null;
            }
        }

        public const string TemplateHeader =
            "# Language:\r\n" +
            "# Game language:\r\n" +
            "#\r\n" +
            "# KCAccess language template: every text the mod speaks or shows, with an empty translation.\r\n" +
            "# To make a new language, copy this file to a new name in this Languages folder, for example eo.txt\r\n" +
            "# (any name except template.txt), and open the copy in Notepad:\r\n" +
            "#   1. After \"# Language:\" write the language's name, for example: # Language: Esperanto\r\n" +
            "#   2. Leave \"# Game language:\" empty unless the game itself has this language.\r\n" +
            "#   3. Write the translation after \" = \" on each line. Lines left empty stay English.\r\n" +
            "#   4. Save as UTF-8 (Notepad does that by default).\r\n" +
            "# Then choose it in the mod settings (Control Shift O, Mod language) or in the game's language list,\r\n" +
            "# where languages the game does not have are marked \"screen reader only\".\r\n" +
            LanguageRules;

        public const string LanguageRules =
            "# Rules: keep {0}, {1} ... and words in braces such as {Walk} exactly as they are, the mod fills them in;\r\n" +
            "# a translation with different braces is ignored and English is used. \\n is a line break.\r\n" +
            "# While the language is active, saving this file reloads it in the game within 2 seconds.\r\n" +
            "# Lines starting with # are comments.\r\n";

        /// <summary>Header of a language file.</summary>
        public static string LanguageHeader(string name, string game) =>
            "# Language: " + name + "\r\n" +
            "# Game language: " + game + "\r\n" +
            "#\r\n" +
            "# KCAccess translation. Each line: English text = translation. An empty translation means English.\r\n" +
            "# Edit the copy in BepInEx\\plugins\\KCAccess\\Languages, not the one in the default folder: updates of the\r\n" +
            "# mod add new texts to your copy and keep your changes.\r\n" +
            LanguageRules;

        /// <summary>Body of a file in template order: "# file" comments and "key = value" lines.</summary>
        public static string Body(List<KeyValuePair<string, string>> keys, Func<string, string> value)
        {
            var sb = new StringBuilder();
            string file = null;
            foreach (var kv in keys)
            {
                if (kv.Key != file)
                {
                    file = kv.Key;
                    sb.Append("\r\n# ===== ").Append(file).Append(" =====\r\n");
                }
                sb.Append(LocTable.Line(kv.Value, value(kv.Value))).Append("\r\n");
            }
            return sb.ToString();
        }

        public static readonly string[][] Languages =
        {
            new[] { "th", "ไทย (Thai)", "" },
            new[] { "de", "Deutsch (German)", "German" },
            new[] { "fr", "Français (French)", "French" },
            new[] { "zh-Hans", "简体中文 (Simplified Chinese)", "Simplified Chinese" },
            new[] { "zh-Hant", "繁體中文 (Traditional Chinese)", "Traditional Chinese" },
            new[] { "nl", "Nederlands (Dutch)", "Dutch" },
            new[] { "ja", "日本語 (Japanese)", "Japanese" },
            new[] { "ro", "Română (Romanian)", "Romanian" },
            new[] { "pt-BR", "Português do Brasil (Portuguese, Brazil)", "Portuguese (Brazil)" },
            new[] { "es", "Español (Spanish)", "Spanish" },
            new[] { "ko", "한국어 (Korean)", "Korean" },
            new[] { "it", "Italiano (Italian)", "Italian" },
            new[] { "pl", "Polski (Polish)", "Polish" },
            new[] { "ru", "Русский (Russian)", "Russian" },
            new[] { "no", "Norsk (Norwegian)", "Norwegian" },
            new[] { "uk", "Українська (Ukrainian)", "Ukrainian" },
            new[] { "sv", "Svenska (Swedish)", "Swedish" },
            new[] { "tr", "Türkçe (Turkish)", "Turkish" },
        };

        public static string TemplateText() => TemplateHeader + Body(Extract(), k => string.Empty);

        /// <summary>Rewrites template.txt and every language file in template order, keeping their translations.</summary>
        public static void WriteAll()
        {
            var keys = Extract();
            string dir = LanguagesDir;
            Directory.CreateDirectory(dir);
            // Texts that left the mod are listed in retired.keys, so updates remove them from the players' files.
            string templatePath = Path.Combine(dir, "template.txt");
            var current = new HashSet<string>(keys.ConvertAll(k => k.Value), StringComparer.Ordinal);
            var retired = new SortedSet<string>(LanguageFiles.ReadRetired(Path.Combine(dir, LanguageFiles.RetiredName)), StringComparer.Ordinal);
            if (File.Exists(templatePath))
                foreach (var k in LocTable.Parse(File.ReadAllText(templatePath, Encoding.UTF8)).Keys)
                    if (!current.Contains(k)) retired.Add(k);
            retired.RemoveWhere(k => current.Contains(k));
            File.WriteAllText(Path.Combine(dir, LanguageFiles.RetiredName), LanguageFiles.RetiredHeader + string.Join("\r\n", retired) + "\r\n", LanguageFiles.FileEncoding);
            File.WriteAllText(templatePath, TemplateHeader + Body(keys, k => string.Empty), LanguageFiles.FileEncoding);
            foreach (var lang in Languages)
            {
                string path = Path.Combine(dir, lang[0] + ".txt");
                var old = File.Exists(path) ? LocTable.Parse(File.ReadAllText(path, Encoding.UTF8)) : new LocTable();
                string name = old.LanguageName.Length > 0 ? old.LanguageName : lang[1];
                string game = File.Exists(path) ? old.GameLanguage : lang[2];
                File.WriteAllText(path, LanguageHeader(name, game) + Body(keys, k => old.Raw(k) ?? string.Empty), LanguageFiles.FileEncoding);
            }
        }
    }

    public class LocalizationFilesTests
    {
        private static bool Updating => Environment.GetEnvironmentVariable("KCACCESS_UPDATE_LANGUAGES") == "1";

        [Fact]
        public void TemplateListsEveryTextInTheSources()
        {
            if (Updating) LocExtractor.WriteAll();
            string path = Path.Combine(LocExtractor.LanguagesDir, "template.txt");
            Assert.True(File.Exists(path), "template.txt is missing: run tools/loc-extract.ps1");
            var template = LocTable.Parse(File.ReadAllText(path, Encoding.UTF8));
            var missing = LocExtractor.Extract().Select(k => k.Value).Where(k => !template.Has(k)).ToList();
            Assert.True(missing.Count == 0, "Texts missing from template.txt (run tools/loc-extract.ps1):\n" + string.Join("\n", missing));
            Assert.Equal(LocExtractor.TemplateText().Replace("\r\n", "\n"), File.ReadAllText(path, Encoding.UTF8).TrimStart('﻿').Replace("\r\n", "\n"));
        }

        [Fact]
        public void EveryGameLanguageAndThaiHasAFile()
        {
            foreach (var lang in LocExtractor.Languages)
                Assert.True(File.Exists(Path.Combine(LocExtractor.LanguagesDir, lang[0] + ".txt")), lang[0] + ".txt is missing");
            foreach (var kv in LanguageFiles.GameLanguages)
                if (kv.Value != "en") Assert.Contains(LocExtractor.Languages, l => l[0] == kv.Value && l[2] == kv.Key);
        }

        [Fact]
        public void ShippedLanguageFilesAreClean()
        {
            var template = LocTable.Parse(File.ReadAllText(Path.Combine(LocExtractor.LanguagesDir, "template.txt"), Encoding.UTF8));
            var problems = new List<string>();
            foreach (var path in Directory.GetFiles(LocExtractor.LanguagesDir, "*.txt"))
            {
                string file = Path.GetFileName(path);
                if (file == "template.txt") continue;
                var t = LocTable.Parse(File.ReadAllText(path, Encoding.UTF8));
                if (t.LanguageName.Length == 0) problems.Add(file + ": no \"# Language:\" header");
                foreach (var key in t.Keys)
                {
                    if (!template.Has(key)) problems.Add(file + ": unknown key \"" + key + "\"");
                    else if (t.Raw(key).Length > 0 && !Loc.PlaceholdersMatch(key, t.Raw(key))) problems.Add(file + ": placeholders differ in \"" + key + "\"");
                }
                if (!t.Keys.SequenceEqual(template.Keys)) problems.Add(file + ": keys are not all there or not in the template's order (run tools/loc-extract.ps1)");
            }
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        [Fact]
        public void ThaiIsComplete()
        {
            var t = LocTable.Parse(File.ReadAllText(Path.Combine(LocExtractor.LanguagesDir, "th.txt"), Encoding.UTF8));
            var empty = t.Keys.Where(k => t.Raw(k).Length == 0).ToList();
            Assert.True(empty.Count == 0, "Untranslated in th.txt:\n" + string.Join("\n", empty));
            Assert.Equal("", t.GameLanguage);
        }

        [Fact]
        public void ExtractorReadsCallsAndJoinedLiterals()
        {
            var keys = LocExtractor.ExtractFrom(
                "Loc.T(\"a\"); Loc.F(\"b {0}\", x); KCAccess.Core.Loc.N(\"c \" +\n \"d\"); Loc.P(n, \"{0} e\", \"{0} es\"); Loc.T(variable); Loc.T(\"q\\\"x\\n\");");
            Assert.Equal(new[] { "a", "b {0}", "c d", "{0} e", "{0} es", "q\"x\n" }, keys);
        }
    }

    public class LocTests : IDisposable
    {
        public void Dispose() => Loc.Use(null);

        [Fact]
        public void ParsesEntriesHeadersCommentsAndEscapes()
        {
            var t = LocTable.Parse("﻿# Language: ไทย (Thai)\r\n# Game language:\r\n# comment = not an entry\r\n\r\nHello = สวัสดี\r\nOne\\=two = หนึ่ง\\=สอง\r\nLine\\nbreak = บรรทัด\\nใหม่\r\nBlank =\r\nAlso blank = \r\nback\\\\slash = x\\\\y\r\nno separator here\r\n");
            Assert.Equal("ไทย (Thai)", t.LanguageName);
            Assert.Equal("", t.GameLanguage);
            Assert.Equal("สวัสดี", t.Raw("Hello"));
            Assert.Equal("หนึ่ง=สอง", t.Raw("One=two"));
            Assert.Equal("บรรทัด\nใหม่", t.Raw("Line\nbreak"));
            Assert.Equal("", t.Raw("Blank"));
            Assert.Equal("", t.Raw("Also blank"));
            Assert.Equal("x\\y", t.Raw("back\\slash"));
            Assert.False(t.Has("comment"));
            Assert.Equal(6, t.Count);
        }

        [Fact]
        public void SplitsAtTheFirstSeparator()
        {
            Assert.True(LocTable.TrySplit("a = b = c", out var k, out var v));
            Assert.Equal("a", k);
            Assert.Equal("b = c", v);
        }

        [Fact]
        public void LineRoundTrips()
        {
            foreach (var key in new[] { "plain", "x = y", "two\nlines", "back\\slash", "{0} of {1}" })
            {
                Assert.True(LocTable.TrySplit(LocTable.Line(key, "v " + key), out var k, out var v));
                Assert.Equal(key, k);
                Assert.Equal("v " + key, v);
            }
            Assert.Equal("key =", LocTable.Line("key", ""));
        }

        [Fact]
        public void FallsBackToEnglish()
        {
            Assert.Equal("Selection cleared", Loc.T("Selection cleared"));
            Loc.Use(LocTable.Parse("Selection cleared = ล้างการเลือกแล้ว\nEmpty =\n"));
            Assert.Equal("ล้างการเลือกแล้ว", Loc.T("Selection cleared"));
            Assert.Equal("Empty", Loc.T("Empty"));
            Assert.Equal("Not in the file", Loc.T("Not in the file"));
            Loc.Use(null);
            Assert.Equal("Selection cleared", Loc.T("Selection cleared"));
        }

        [Fact]
        public void FormatsAndReorders()
        {
            Assert.Equal("3 of 7", Loc.F("{0} of {1}", 3, 7));
            Loc.Use(LocTable.Parse("{0} of {1} = {1} อันที่ {0}\n"));
            Assert.Equal("7 อันที่ 3", Loc.F("{0} of {1}", 3, 7));
        }

        [Fact]
        public void PluralPicksTheForm()
        {
            Assert.Equal("1 tree", Loc.P(1, "{0} tree", "{0} trees"));
            Assert.Equal("0 trees", Loc.P(0, "{0} tree", "{0} trees"));
            Assert.Equal("5 tiles north", Loc.P(5, "{0} tile {1}", "{0} tiles {1}", "north"));
            Loc.Use(LocTable.Parse("{0} tree = ต้นไม้ {0} ต้น\n{0} trees = ต้นไม้ {0} ต้น\n"));
            Assert.Equal("ต้นไม้ 1 ต้น", Loc.P(1, "{0} tree", "{0} trees"));
            Assert.Equal("ต้นไม้ 4 ต้น", Loc.P(4, "{0} tree", "{0} trees"));
        }

        [Fact]
        public void PlaceholderMismatchIsIgnoredAndReportedOnce()
        {
            var warnings = new List<string>();
            var t = LocTable.Parse("{0} of {1} = {0} จาก {2}\nWalk with {Walk} = เดินด้วย {Wlak}\nGood {0} = ดี {0}\n");
            t.Warn = warnings.Add;
            Loc.Use(t);
            Assert.Equal("3 of 7", Loc.F("{0} of {1}", 3, 7));
            Assert.Equal("3 of 7", Loc.F("{0} of {1}", 3, 7));
            Assert.Equal("Walk with {Walk}", Loc.T("Walk with {Walk}"));
            Assert.Equal("ดี 1", Loc.F("Good {0}", 1));
            Assert.Equal(2, warnings.Count);
        }

        [Fact]
        public void FormatNeverThrows()
        {
            Assert.Equal("a {5} {Walk} b", Loc.Format("a {5} {Walk} {0}", "b"));
            Assert.Equal("{0}", Loc.Format("{0}"));
            Assert.Equal("1.5", Loc.Format("{0}", 1.5f));
            Assert.Equal("x { y", Loc.Format("x { y"));
        }

        [Fact]
        public void HelpTextsAndKeyListAreTranslated()
        {
            Loc.Use(LocTable.Parse("Mod settings = การตั้งค่ามอด\nEverywhere = ทุกที่\n"));
            Assert.Equal("การตั้งค่ามอด", Bindings.Def("Settings").SpokenName);
            Assert.Contains("ทุกที่", KeyHelp.AllText());
        }
    }

    public class LanguageMergeTests
    {
        private const string Shipped1 = "# Language: X\nA = a1\nB = b1\nC =\n";

        [Fact]
        public void UnchangedTranslationsTakeTheNewShippedOnes()
        {
            string user = Shipped1;
            string shipped2 = "# Language: X\nA = a2\nB = b2\nC = c2\nD = d2\n";
            var merged = LocTable.Parse(LanguageFiles.Merge(user, shipped2, Shipped1));
            Assert.Equal("a2", merged.Raw("A"));
            Assert.Equal("b2", merged.Raw("B"));
            Assert.Equal("c2", merged.Raw("C"));
            Assert.Equal("d2", merged.Raw("D"));
        }

        [Fact]
        public void PlayerChangesAreKept()
        {
            string user = "# Language: X\n# my note\nA = mine\nB = b1\nC = my own\nZ = player's extra\n";
            string shipped2 = "# Language: X\nA = a2\nB = b2\nC = c2\n";
            string text = LanguageFiles.Merge(user, shipped2, Shipped1);
            var merged = LocTable.Parse(text);
            Assert.Equal("mine", merged.Raw("A"));
            Assert.Equal("b2", merged.Raw("B"));
            Assert.Equal("my own", merged.Raw("C"));
            Assert.Equal("player's extra", merged.Raw("Z"));
            Assert.Contains("# my note", text);
        }

        [Fact]
        public void MissingKeysAreAppended()
        {
            string user = "# Language: X\nA = mine\n";
            string text = LanguageFiles.Merge(user, "# Language: X\nA = a1\nNew {0} = neu {0}\n", "# Language: X\nA = a1\n");
            var merged = LocTable.Parse(text);
            Assert.Equal("mine", merged.Raw("A"));
            Assert.Equal("neu {0}", merged.Raw("New {0}"));
            Assert.True(text.IndexOf("A = mine", StringComparison.Ordinal) < text.IndexOf("New {0}", StringComparison.Ordinal));
        }

        [Fact]
        public void OldAppendedBlocksMoveIntoTheShippedSections()
        {
            string user = "# Language: X\n# src/KCAccess/Game/Map.cs\nA = mine\n\n# Texts added by a mod update:\nB = b1\n";
            string shipped = "# Language: X\n\n# ===== Map =====\nA = a1\nB = b1\n\n# ===== Build =====\nC = c1\n";
            string text = LanguageFiles.Merge(user, shipped, "# Language: X\nA = a1\nB = b1\n");
            Assert.DoesNotContain("Texts added by a mod update", text);
            Assert.DoesNotContain("# src/", text);
            Assert.True(text.IndexOf("# ===== Build =====", StringComparison.Ordinal) < text.IndexOf("C = c1", StringComparison.Ordinal));
            Assert.Equal("mine", LocTable.Parse(text).Raw("A"));
            Assert.DoesNotContain(LanguageFiles.OwnSection, text);
        }

        [Fact]
        public void RetiredAndUntranslatedUnknownLinesAreDropped()
        {
            string user = "# Language: X\nA = mine\nOld text = alt\nUnused =\nMy brush = pinsel\n";
            string text = LanguageFiles.Merge(user, "# Language: X\nA = a1\n", "# Language: X\nA = a1\n", new HashSet<string> { "Old text" });
            var merged = LocTable.Parse(text);
            Assert.Null(merged.Raw("Old text"));
            Assert.Null(merged.Raw("Unused"));
            Assert.Equal("pinsel", merged.Raw("My brush"));
            Assert.Equal("mine", merged.Raw("A"));
        }

        [Fact]
        public void WithoutBaseOnlyBlankTranslationsAreFilled()
        {
            string user = "A = mine\nB =\n";
            var merged = LocTable.Parse(LanguageFiles.Merge(user, "A = a2\nB = b2\n", null));
            Assert.Equal("mine", merged.Raw("A"));
            Assert.Equal("b2", merged.Raw("B"));
        }

        [Fact]
        public void NothingToDoLeavesTheFileUntouched()
        {
            Assert.Same(Shipped1, LanguageFiles.Merge(Shipped1, Shipped1, Shipped1));
        }

        [Fact]
        public void SyncCopiesMergesAndKeepsABase()
        {
            using (var tmp = new TempDir())
            {
                var files = new LanguageFiles(tmp.Path);
                Directory.CreateDirectory(files.DefaultDir);
                File.WriteAllText(Path.Combine(files.DefaultDir, "xx.txt"), "# Language: Test\n# Game language:\nA = a1\nB = b1\n");
                File.WriteAllText(Path.Combine(files.DefaultDir, "template.txt"), "A =\nB =\n");
                Assert.Equal(new[] { "xx" }, files.Sync());
                Assert.True(File.Exists(Path.Combine(tmp.Path, "template.txt")));
                Assert.True(File.Exists(Path.Combine(files.BaseDir, "xx.txt")));

                // The player edits A; an update changes A and B and adds C.
                File.WriteAllText(files.UserPath("xx"), "# Language: Test\n# Game language:\nA = mine\nB = b1\n");
                File.WriteAllText(Path.Combine(files.DefaultDir, "xx.txt"), "# Language: Test\n# Game language:\nA = a2\nB = b2\nC = c2\n");
                Assert.Equal(new[] { "xx" }, files.Sync());
                var t = files.Load("xx");
                Assert.Equal("mine", t.Raw("A"));
                Assert.Equal("b2", t.Raw("B"));
                Assert.Equal("c2", t.Raw("C"));
                Assert.Empty(files.Sync());

                var list = files.Available();
                Assert.Single(list);
                Assert.Equal("Test", list[0].Name);
                Assert.True(list[0].ModOnly);
            }
        }

        [Fact]
        public void ResolveFollowsSettingAndGameLanguage()
        {
            var langs = new List<LanguageInfo>
            {
                new LanguageInfo { Code = "th", Name = "ไทย (Thai)", GameLanguage = "" },
                new LanguageInfo { Code = "de", Name = "Deutsch", GameLanguage = "German" },
                new LanguageInfo { Code = "pt-BR", Name = "Português", GameLanguage = "" },
            };
            Assert.Null(LanguageFiles.Resolve("auto", "English", langs));
            Assert.Equal("de", LanguageFiles.Resolve("auto", "German", langs));
            Assert.Equal("pt-BR", LanguageFiles.Resolve("auto", "Portuguese (Brazil)", langs)); // by code when the header is empty
            Assert.Null(LanguageFiles.Resolve("auto", "Korean", langs));
            Assert.Equal("th", LanguageFiles.Resolve("th", "German", langs));
            Assert.Equal("th", LanguageFiles.Resolve("TH", null, langs));
            Assert.Null(LanguageFiles.Resolve("en", "German", langs));
            Assert.Null(LanguageFiles.Resolve("xx", "German", langs));
            Assert.Null(LanguageFiles.Resolve("", "English", langs));
        }
    }

    public class InstallerLanguageTests
    {
        [Theory]
        [InlineData("th-TH", "th")]
        [InlineData("de-DE", "de")]
        [InlineData("zh-CN", "zh-Hans")]
        [InlineData("zh-TW", "zh-Hant")]
        [InlineData("pt-PT", "pt-BR")]
        [InlineData("nb-NO", "no")]
        [InlineData("en-US", null)]
        public void WindowsLanguageToFile(string culture, string expected)
        {
            Assert.Equal(expected, InstallerLanguage.CodeForCulture(new CultureInfo(culture)));
        }

        [Fact]
        public void ReadsTheModSetting()
        {
            Assert.Equal("th", InstallerLanguage.SettingFromConfig("[Language]\r\n\r\n## Language the mod speaks\r\n# Setting type: String\r\nLanguage = th\r\n"));
            Assert.Null(InstallerLanguage.SettingFromConfig("[Audio]\nSoundCues = true\n"));
        }

        [Fact]
        public void UpdateKeepsThePlayersLanguageFiles()
        {
            using (var tmp = new TempDir())
            {
                string game = tmp.Sub("game");
                Directory.CreateDirectory(game);
                File.WriteAllText(Path.Combine(game, SteamLibrary.GameExe), "x");
                var inst = new ModInstaller(game);
                string langs = Path.Combine(inst.PluginDir, "Languages");
                Directory.CreateDirectory(Path.Combine(langs, "default", ".base"));
                File.WriteAllText(Path.Combine(langs, "th.txt"), "mine");
                File.WriteAllText(Path.Combine(langs, "template.txt"), "old template");
                File.WriteAllText(Path.Combine(langs, "default", "th.txt"), "old shipped");
                File.WriteAllText(Path.Combine(langs, "default", ".base", "th.txt"), "base");

                var ms = new MemoryStream();
                using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var name in new[] { "winhttp.dll", "BepInEx/core/BepInEx.dll", "BepInEx/plugins/KCAccess/KCAccess.dll", "BepInEx/plugins/KCAccess/Languages/default/th.txt" })
                        using (var w = new StreamWriter(zip.CreateEntry(name).Open())) w.Write("new");
                }
                ms.Position = 0;
                inst.Install(ms);

                Assert.Equal("mine", File.ReadAllText(Path.Combine(langs, "th.txt")));
                Assert.Equal("base", File.ReadAllText(Path.Combine(langs, "default", ".base", "th.txt")));
                Assert.Equal("new", File.ReadAllText(Path.Combine(langs, "default", "th.txt")));
                Assert.False(File.Exists(Path.Combine(langs, "template.txt"))); // the mod writes a fresh one

                inst.Uninstall(removeLoader: false);
                Assert.False(Directory.Exists(langs));
            }
        }
    }
}
