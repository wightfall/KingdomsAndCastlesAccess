using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace KCAccess.Installer
{
    /// <summary>
    /// The mod's BepInEx config file (BepInEx\config\kcaccess.screenreader.cfg): the setup writes the mod language there
    /// when the player asks for it. BepInEx keeps a value that is already in the file and adds its descriptions on start.
    /// </summary>
    public static class ModConfig
    {
        public const string FileName = "kcaccess.screenreader.cfg";
        private const string Section = "[Language]";

        public static string PathIn(string gameDir) => Path.Combine(gameDir, "BepInEx", "config", FileName);

        /// <summary>
        /// The config text with "Language = <paramref name="code"/>" in its [Language] section: the line is replaced
        /// when it exists, otherwise added (with the section when that is missing too). Everything else is kept.
        /// </summary>
        public static string SetLanguage(string cfgText, string code)
        {
            var lines = new List<string>((cfgText ?? string.Empty).Replace("\r\n", "\n").Split('\n'));
            if (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            string entry = "Language = " + code;
            int section = lines.FindIndex(l => l.Trim().Equals(Section, StringComparison.OrdinalIgnoreCase));
            if (section < 0)
            {
                if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0) lines.Add(string.Empty);
                lines.Add(Section);
                lines.Add(string.Empty);
                lines.Add(entry);
            }
            else
            {
                int end = lines.FindIndex(section + 1, l => l.TrimStart().StartsWith("[", StringComparison.Ordinal));
                if (end < 0) end = lines.Count;
                int line = -1;
                for (int i = section + 1; i < end; i++)
                {
                    string t = lines[i].TrimStart();
                    if (t.StartsWith("Language", StringComparison.Ordinal) && t.Substring("Language".Length).TrimStart().StartsWith("=", StringComparison.Ordinal)) { line = i; break; }
                }
                if (line >= 0) lines[line] = entry;
                else lines.Insert(section + 1, entry);
            }
            return string.Join("\r\n", lines.ToArray()) + "\r\n";
        }

        /// <summary>Saves the mod language (a language file code, "en" or "auto") in the game's config file.</summary>
        public static void WriteLanguage(string gameDir, string code)
        {
            string path = PathIn(gameDir);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string text = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            File.WriteAllText(path, SetLanguage(text, code), new UTF8Encoding(true));
        }
    }

    /// <summary>Starts Kingdoms and Castles after setup: through Steam for a Steam copy, otherwise the game's program.</summary>
    public static class GameLauncher
    {
        public const string SteamAppId = "569480";

        public static void Start(string gameDir)
        {
            bool steam = gameDir != null && (gameDir.IndexOf(@"\steamapps\", StringComparison.OrdinalIgnoreCase) >= 0
                                             || File.Exists(Path.Combine(gameDir, "steam_appid.txt")));
            if (steam)
                Process.Start(new ProcessStartInfo("steam://rungameid/" + SteamAppId) { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo(Path.Combine(gameDir, SteamLibrary.GameExe)) { WorkingDirectory = gameDir, UseShellExecute = true });
        }
    }
}
