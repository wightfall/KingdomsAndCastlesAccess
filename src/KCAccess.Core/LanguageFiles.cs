using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KCAccess.Core
{
    /// <summary>A language file found in the Languages folder.</summary>
    public sealed class LanguageInfo
    {
        /// <summary>File name without ".txt", e.g. "th", "pt-BR"; also the value of the Language setting.</summary>
        public string Code;
        /// <summary>From the "# Language:" header ("ไทย (Thai)"), or the code when the header is missing.</summary>
        public string Name;
        /// <summary>I2 language name of the game from "# Game language:", empty for languages only the mod speaks.</summary>
        public string GameLanguage;
        public string Path;

        public bool ModOnly => string.IsNullOrEmpty(GameLanguage);
    }

    /// <summary>
    /// The mod's language files in BepInEx/plugins/KCAccess/Languages:
    /// <c>default/&lt;code&gt;.txt</c> are shipped (replaced by every update), <c>&lt;code&gt;.txt</c> are the player's copies
    /// (never overwritten, updates are merged in), <c>default/.base/&lt;code&gt;.txt</c> remember the shipped text the
    /// player's copy was last merged with, and <c>template.txt</c> lists every text for a new language.
    /// </summary>
    public sealed class LanguageFiles
    {
        public const string Auto = "auto";
        public const string English = "en";
        public const string TemplateName = "template.txt";

        /// <summary>The game's languages (I2 names) and the file code the mod uses for each.</summary>
        public static readonly KeyValuePair<string, string>[] GameLanguages =
        {
            new KeyValuePair<string, string>("English", "en"),
            new KeyValuePair<string, string>("German", "de"),
            new KeyValuePair<string, string>("French", "fr"),
            new KeyValuePair<string, string>("Simplified Chinese", "zh-Hans"),
            new KeyValuePair<string, string>("Traditional Chinese", "zh-Hant"),
            new KeyValuePair<string, string>("Dutch", "nl"),
            new KeyValuePair<string, string>("Japanese", "ja"),
            new KeyValuePair<string, string>("Romanian", "ro"),
            new KeyValuePair<string, string>("Portuguese (Brazil)", "pt-BR"),
            new KeyValuePair<string, string>("Spanish", "es"),
            new KeyValuePair<string, string>("Korean", "ko"),
            new KeyValuePair<string, string>("Italian", "it"),
            new KeyValuePair<string, string>("Polish", "pl"),
            new KeyValuePair<string, string>("Russian", "ru"),
            new KeyValuePair<string, string>("Norwegian", "no"),
            new KeyValuePair<string, string>("Ukrainian", "uk"),
            new KeyValuePair<string, string>("Swedish", "sv"),
            new KeyValuePair<string, string>("Turkish", "tr"),
        };

        public static readonly Encoding FileEncoding = new UTF8Encoding(true); // with BOM: every Notepad reads it as UTF-8

        public LanguageFiles(string languagesDir)
        {
            Dir = languagesDir ?? throw new ArgumentNullException(nameof(languagesDir));
        }

        public string Dir { get; }
        public string DefaultDir => Path.Combine(Dir, "default");
        public string BaseDir => Path.Combine(DefaultDir, ".base");

        public string UserPath(string code) => Path.Combine(Dir, code + ".txt");

        /// <summary>
        /// Brings the player's files up to date with the shipped ones: a missing file is copied, an existing one is
        /// merged (<see cref="Merge"/>), template.txt is replaced. Returns the codes whose file was created or changed.
        /// </summary>
        public List<string> Sync(Action<string> log = null)
        {
            var changed = new List<string>();
            if (!Directory.Exists(DefaultDir)) return changed;
            Directory.CreateDirectory(Dir);
            foreach (var shippedPath in Directory.GetFiles(DefaultDir, "*.txt"))
            {
                string file = Path.GetFileName(shippedPath);
                try
                {
                    string shipped = File.ReadAllText(shippedPath, Encoding.UTF8);
                    if (string.Equals(file, TemplateName, StringComparison.OrdinalIgnoreCase))
                    {
                        string target = Path.Combine(Dir, TemplateName);
                        if (!File.Exists(target) || File.ReadAllText(target, Encoding.UTF8) != shipped) File.WriteAllText(target, shipped, FileEncoding);
                        continue;
                    }
                    string code = Path.GetFileNameWithoutExtension(file);
                    string user = UserPath(code);
                    string basePath = Path.Combine(BaseDir, file);
                    if (!File.Exists(user))
                    {
                        File.WriteAllText(user, shipped, FileEncoding);
                        changed.Add(code);
                        log?.Invoke("Language file created: " + user);
                    }
                    else
                    {
                        string current = File.ReadAllText(user, Encoding.UTF8);
                        string previous = File.Exists(basePath) ? File.ReadAllText(basePath, Encoding.UTF8) : null;
                        string merged = Merge(current, shipped, previous);
                        if (merged != current)
                        {
                            File.WriteAllText(user, merged, FileEncoding);
                            changed.Add(code);
                            log?.Invoke("Language file updated with the new shipped texts: " + user);
                        }
                    }
                    Directory.CreateDirectory(BaseDir);
                    File.WriteAllText(basePath, shipped, FileEncoding);
                }
                catch (Exception e)
                {
                    log?.Invoke("Could not update the language file " + file + ": " + e.Message);
                }
            }
            return changed;
        }

        /// <summary>
        /// Three-way merge of the player's file with a new shipped file. The result follows the shipped file's layout
        /// (its sections and order), so texts a mod update adds sit in their proper section. A translation the player
        /// never changed (still equal to <paramref name="previousShipped"/>, or blank when there is no previous file)
        /// takes the new shipped one; translations the player changed are kept. The player's own lines and comments
        /// that the shipped file does not have are kept in a section at the end. Returns <paramref name="user"/>
        /// unchanged when there is nothing to do.
        /// </summary>
        public static string Merge(string user, string shipped, string previousShipped)
        {
            var mine = LocTable.Parse(user ?? string.Empty);
            var old = previousShipped != null ? LocTable.Parse(previousShipped) : null;
            bool bom = !string.IsNullOrEmpty(user) && user[0] == '﻿';
            var lines = new List<string>();
            var known = new HashSet<string>(StringComparer.Ordinal);
            var shippedComments = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in LocTable.SplitLines((shipped ?? string.Empty).TrimStart('﻿')))
            {
                if (!LocTable.TrySplit(line, out var key, out var fresh))
                {
                    lines.Add(line);
                    shippedComments.Add(line.Trim());
                    continue;
                }
                known.Add(key);
                string have = mine.Raw(key);
                string value = fresh;
                if (have != null)
                {
                    bool changedByPlayer = old != null ? have != (old.Raw(key) ?? string.Empty) : have.Length > 0;
                    if (changedByPlayer) value = have;
                }
                lines.Add(LocTable.Line(key, value));
            }
            // The player's own lines: keys this version does not ship, and comments they wrote.
            var own = new List<string>();
            foreach (var line in LocTable.SplitLines((user ?? string.Empty).TrimStart('﻿')))
            {
                string t = line.Trim();
                if (LocTable.TrySplit(line, out var key, out _))
                {
                    if (!known.Contains(key)) own.Add(line);
                }
                else if (t.StartsWith("#", StringComparison.Ordinal) && !shippedComments.Contains(t) && !IsGeneratedComment(t)) own.Add(line);
            }
            if (own.Count > 0)
            {
                while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
                lines.Add(string.Empty);
                lines.Add(OwnSection);
                lines.AddRange(own);
            }
            string result = string.Join("\r\n", lines.ToArray());
            if (Normalize(result) == Normalize(user)) return user;
            return (bom ? "﻿" : string.Empty) + result;
        }

        public const string OwnSection = "# ===== Your own lines (kept by mod updates) =====";

        /// <summary>Comments the mod itself wrote (sections, update markers of older versions, the header).</summary>
        private static bool IsGeneratedComment(string t) =>
            t == OwnSection || t.StartsWith("# Language:", StringComparison.Ordinal) || t.StartsWith("# Game language:", StringComparison.Ordinal)
            || t.StartsWith("# =====", StringComparison.Ordinal) || t.StartsWith("# src/", StringComparison.Ordinal)
            || t == "# Texts added by a mod update:";

        private static string Normalize(string s)
        {
            var parts = LocTable.SplitLines((s ?? string.Empty).TrimStart('﻿'));
            while (parts.Count > 0 && parts[parts.Count - 1].Trim().Length == 0) parts.RemoveAt(parts.Count - 1);
            return string.Join("\n", parts.ToArray());
        }

        /// <summary>Every language file the player can choose (not the template), sorted by name.</summary>
        public List<LanguageInfo> Available()
        {
            var list = new List<LanguageInfo>();
            if (!Directory.Exists(Dir)) return list;
            foreach (var path in Directory.GetFiles(Dir, "*.txt"))
            {
                string file = Path.GetFileName(path);
                if (string.Equals(file, TemplateName, StringComparison.OrdinalIgnoreCase)) continue;
                string code = Path.GetFileNameWithoutExtension(file);
                if (string.Equals(code, English, StringComparison.OrdinalIgnoreCase) || string.Equals(code, Auto, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var t = ReadHeader(path);
                    list.Add(new LanguageInfo { Code = code, Name = t.LanguageName.Length > 0 ? t.LanguageName : code, GameLanguage = t.GameLanguage, Path = path });
                }
                catch (Exception)
                {
                    // unreadable file: not offered
                }
            }
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static LocTable ReadHeader(string path)
        {
            var sb = new StringBuilder();
            using (var r = new StreamReader(path, Encoding.UTF8))
            {
                for (int i = 0; i < 40; i++)
                {
                    string line = r.ReadLine();
                    if (line == null) break;
                    if (line.TrimStart().StartsWith("#", StringComparison.Ordinal)) sb.AppendLine(line);
                }
            }
            return LocTable.Parse(sb.ToString());
        }

        public LocTable Load(string code) => LocTable.Parse(File.ReadAllText(UserPath(code), Encoding.UTF8));

        /// <summary>
        /// Which file to use: null means English. <paramref name="setting"/> is "auto" (follow the game's language
        /// <paramref name="gameLanguage"/>), "en", or a file code. Unknown codes fall back to English.
        /// </summary>
        public static string Resolve(string setting, string gameLanguage, IList<LanguageInfo> available)
        {
            string s = string.IsNullOrWhiteSpace(setting) ? Auto : setting.Trim();
            if (string.Equals(s, English, StringComparison.OrdinalIgnoreCase) || string.Equals(s, "English", StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.Equals(s, Auto, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var l in available) if (string.Equals(l.Code, s, StringComparison.OrdinalIgnoreCase)) return l.Code;
                return null;
            }
            if (string.IsNullOrEmpty(gameLanguage) || string.Equals(gameLanguage, "English", StringComparison.OrdinalIgnoreCase)) return null;
            foreach (var l in available) if (string.Equals(l.GameLanguage, gameLanguage, StringComparison.OrdinalIgnoreCase)) return l.Code;
            string code = CodeForGameLanguage(gameLanguage);
            if (code != null) foreach (var l in available) if (string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) return l.Code;
            return null;
        }

        public static string CodeForGameLanguage(string gameLanguage)
        {
            foreach (var kv in GameLanguages) if (string.Equals(kv.Key, gameLanguage, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return null;
        }
    }
}
