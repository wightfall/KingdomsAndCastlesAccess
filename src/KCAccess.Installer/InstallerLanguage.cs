using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using KCAccess.Core;

namespace KCAccess.Installer
{
    /// <summary>
    /// The setup program speaks the mod's language: the Language setting in the mod's config when it is not "auto",
    /// otherwise the Windows display language. The player can pick another one in the setup window (every language the
    /// mod has: the files shipped in the built-in package and the player's own files in the game folder). Texts come
    /// from the player's language file in the game folder, or from the shipped file inside the built-in package.
    /// English when neither exists.
    /// </summary>
    public static class InstallerLanguage
    {
        private static readonly Regex SettingLine = new Regex(@"^\s*Language\s*=\s*(\S.*?)\s*$", RegexOptions.CultureInvariant);
        private const string PayloadLanguages = "BepInEx/plugins/KCAccess/Languages/default/";
        private const string EnglishCode = "en", AutoSetting = "auto", TemplateName = "template.txt";

        /// <summary>A language the setup and the mod can use. Code null is English.</summary>
        public sealed class Choice
        {
            public string Code;
            public string Name;

            public override string ToString() => Name;
        }

        /// <summary>The language the setup window uses now (file code), null for English.</summary>
        public static string Current { get; private set; }

        public static void Init(string gameDir)
        {
            try
            {
                Apply(gameDir, Choose(gameDir, CultureInfo.CurrentUICulture));
            }
            catch (Exception)
            {
                // English
            }
        }

        /// <summary>Switches the setup's texts to a language (null: English). False when its file could not be read.</summary>
        public static bool Apply(string gameDir, string code)
        {
            if (code == null || string.Equals(code, EnglishCode, StringComparison.OrdinalIgnoreCase))
            {
                Loc.Use(null);
                Current = null;
                return true;
            }
            string text = ReadUserFile(gameDir, code) ?? ReadPayloadFile(code);
            if (text == null) return false;
            Loc.Use(LocTable.Parse(text));
            Current = code;
            return true;
        }

        /// <summary>File code to use, or null for English.</summary>
        public static string Choose(string gameDir, CultureInfo culture)
        {
            string setting = null;
            if (gameDir != null)
            {
                string cfg = ModConfig.PathIn(gameDir);
                if (File.Exists(cfg)) setting = SettingFromConfig(File.ReadAllText(cfg));
            }
            if (setting != null && !string.Equals(setting, AutoSetting, StringComparison.OrdinalIgnoreCase))
                return string.Equals(setting, EnglishCode, StringComparison.OrdinalIgnoreCase) ? null : setting;
            return CodeForCulture(culture);
        }

        /// <summary>The value of "Language = ..." in the mod's BepInEx config, or null.</summary>
        public static string SettingFromConfig(string cfgText)
        {
            if (cfgText == null) return null;
            foreach (var line in cfgText.Replace("\r", "").Split('\n'))
            {
                var m = SettingLine.Match(line);
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }

        /// <summary>Language file code for a Windows display language ("th-TH" → "th", "zh-TW" → "zh-Hant"), null for English.</summary>
        public static string CodeForCulture(CultureInfo culture)
        {
            if (culture == null) return null;
            string name = culture.Name ?? string.Empty;
            string two = culture.TwoLetterISOLanguageName;
            switch (two)
            {
                case "en":
                case "iv":
                    return null;
                case "zh":
                    return name.EndsWith("TW") || name.EndsWith("HK") || name.EndsWith("MO") || name.Contains("Hant") ? "zh-Hant" : "zh-Hans";
                case "pt":
                    return "pt-BR";
                case "nb":
                case "nn":
                    return "no";
                default:
                    return two;
            }
        }

        /// <summary>
        /// Every language to offer: English first, then the shipped files of the built-in package and the player's own
        /// files in the game folder (a language added by the player appears too), sorted by name.
        /// </summary>
        public static List<Choice> Available(string gameDir)
        {
            var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");
                if (stream != null)
                {
                    using (stream)
                    using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        foreach (var entry in zip.Entries)
                        {
                            if (!entry.FullName.StartsWith(PayloadLanguages, StringComparison.OrdinalIgnoreCase)) continue;
                            string code = LanguageCode(entry.Name);
                            if (code == null) continue;
                            using (var r = new StreamReader(entry.Open(), Encoding.UTF8)) found[code] = NameFromText(r.ReadToEnd(), code);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // no package: only the player's files
            }
            try
            {
                string dir = gameDir != null ? Path.Combine(gameDir, ModInstaller.PluginRelative, "Languages") : null;
                if (dir != null && Directory.Exists(dir))
                    foreach (var path in Directory.GetFiles(dir, "*.txt"))
                    {
                        string code = LanguageCode(Path.GetFileName(path));
                        if (code != null) found[code] = NameFromText(File.ReadAllText(path, Encoding.UTF8), code);
                    }
            }
            catch (Exception)
            {
                // unreadable folder: the shipped languages are still offered
            }
            return Sorted(found);
        }

        /// <summary>English first, then the languages by name.</summary>
        public static List<Choice> Sorted(IDictionary<string, string> codesToNames)
        {
            var list = new List<Choice>();
            foreach (var kv in codesToNames) list.Add(new Choice { Code = kv.Key, Name = kv.Value });
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            list.Insert(0, new Choice { Code = null, Name = "English" });
            return list;
        }

        /// <summary>The code of a language file name ("th.txt" → "th"), null for the template or anything else.</summary>
        public static string LanguageCode(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) return null;
            if (string.Equals(fileName, TemplateName, StringComparison.OrdinalIgnoreCase)) return null;
            string code = fileName.Substring(0, fileName.Length - 4);
            return code.Length == 0 || string.Equals(code, EnglishCode, StringComparison.OrdinalIgnoreCase) ? null : code;
        }

        /// <summary>The name in a language file's "# Language:" line, or the code when there is none.</summary>
        public static string NameFromText(string text, string code)
        {
            string name = LocTable.Parse(text ?? string.Empty).LanguageName;
            return string.IsNullOrWhiteSpace(name) ? code : name;
        }

        private static string ReadUserFile(string gameDir, string code)
        {
            if (gameDir == null) return null;
            string path = Path.Combine(gameDir, ModInstaller.PluginRelative, "Languages", code + ".txt");
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }

        private static string ReadPayloadFile(string code)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");
            if (stream == null) return null;
            using (stream)
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var entry = zip.GetEntry(PayloadLanguages + code + ".txt");
                if (entry == null) return null;
                using (var r = new StreamReader(entry.Open(), Encoding.UTF8)) return r.ReadToEnd();
            }
        }
    }
}
