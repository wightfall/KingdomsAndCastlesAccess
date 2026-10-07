using System;
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
    /// otherwise the Windows display language. Texts come from the player's language file in the game folder, or from
    /// the shipped file inside the built-in package. English when neither exists.
    /// </summary>
    public static class InstallerLanguage
    {
        private static readonly Regex SettingLine = new Regex(@"^\s*Language\s*=\s*(\S.*?)\s*$", RegexOptions.CultureInvariant);

        public static void Init(string gameDir)
        {
            try
            {
                string code = Choose(gameDir, CultureInfo.CurrentUICulture);
                if (code == null) return;
                string text = ReadUserFile(gameDir, code) ?? ReadPayloadFile(code);
                if (text != null) Loc.Use(LocTable.Parse(text));
            }
            catch (Exception)
            {
                // English
            }
        }

        /// <summary>File code to use, or null for English.</summary>
        public static string Choose(string gameDir, CultureInfo culture)
        {
            string setting = null;
            if (gameDir != null)
            {
                string cfg = Path.Combine(gameDir, @"BepInEx\config\kcaccess.screenreader.cfg");
                if (File.Exists(cfg)) setting = SettingFromConfig(File.ReadAllText(cfg));
            }
            if (setting != null && !string.Equals(setting, "auto", StringComparison.OrdinalIgnoreCase))
                return string.Equals(setting, "en", StringComparison.OrdinalIgnoreCase) ? null : setting;
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
                var entry = zip.GetEntry("BepInEx/plugins/KCAccess/Languages/default/" + code + ".txt");
                if (entry == null) return null;
                using (var r = new StreamReader(entry.Open(), Encoding.UTF8)) return r.ReadToEnd();
            }
        }
    }
}
