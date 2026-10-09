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
        /// Three-way merge of the player's file with a new shipped file. Keys missing in the player's file are appended;
        /// a translation the player never changed (still equal to <paramref name="previousShipped"/>, or blank) takes the
        /// new shipped translation; translations the player changed are kept, and so are comments, order and the player's
        /// own keys. Returns <paramref name="user"/> unchanged when there is nothing to do.
        /// </summary>
        public static string Merge(string user, string shipped, string previousShipped)
        {
            var newer = LocTable.Parse(shipped);
            var old = previousShipped != null ? LocTable.Parse(previousShipped) : null;
            var lines = LocTable.SplitLines(user ?? string.Empty);
            bool bom = !string.IsNullOrEmpty(user) && user[0] == '﻿';
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool changed = false;
            for (int i = 0; i < lines.Count; i++)
            {
                if (!LocTable.TrySplit(lines[i], out var key, out var value)) continue;
                seen.Add(key);
                string fresh = newer.Raw(key);
                if (fresh == null || fresh == value) continue;
                // The player's own translation wins; without a previous shipped file only blank translations are filled.
                string before = old != null ? old.Raw(key) ?? string.Empty : string.Empty;
                if (value != before) continue;
                lines[i] = LocTable.Line(key, fresh);
                changed = true;
            }
            var added = new List<string>();
            foreach (var key in newer.Keys)
                if (!seen.Contains(key)) added.Add(LocTable.Line(key, newer.Raw(key)));
            if (added.Count > 0)
            {
                while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
                lines.Add(string.Empty);
                lines.Add("# Texts added by a mod update:");
                lines.AddRange(added);
                lines.Add(string.Empty);
                changed = true;
            }
            if (!changed) return user;
            return (bom ? "﻿" : string.Empty) + string.Join("\r\n", lines.ToArray());
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
