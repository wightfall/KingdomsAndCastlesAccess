using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace KCAccess.Installer
{
    /// <summary>Finds the Kingdoms and Castles install folder from Steam's library files.</summary>
    public static class SteamLibrary
    {
        public const string AppId = "569480";
        public const string GameExe = "KingdomsAndCastles.exe";

        private static readonly Regex PathEntry = new Regex("\"path\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex InstallDir = new Regex("\"installdir\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Library folder paths listed in steamapps/libraryfolders.vdf (backslashes unescaped).</summary>
        public static List<string> ParseLibraryFolders(string vdf)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(vdf)) return result;
            foreach (Match m in PathEntry.Matches(vdf))
            {
                string p = Unescape(m.Groups[1].Value);
                if (p.Length > 0 && !result.Contains(p)) result.Add(p);
            }
            return result;
        }

        /// <summary>The "installdir" value of an appmanifest_*.acf file, or null.</summary>
        public static string ParseInstallDir(string acf)
        {
            if (string.IsNullOrEmpty(acf)) return null;
            var m = InstallDir.Match(acf);
            return m.Success ? Unescape(m.Groups[1].Value) : null;
        }

        private static string Unescape(string s) => s.Replace("\\\\", "\\").Replace("\\\"", "\"");

        /// <summary>True when the folder contains the game executable.</summary>
        public static bool IsGameFolder(string dir)
        {
            try
            {
                return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, GameExe));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Searches every Steam library for the game. Returns null when it is not found.</summary>
        public static string FindGame(IEnumerable<string> steamRoots)
        {
            foreach (var root in steamRoots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                var libraries = new List<string> { root };
                string vdfPath = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                try
                {
                    if (File.Exists(vdfPath)) libraries.AddRange(ParseLibraryFolders(File.ReadAllText(vdfPath)));
                }
                catch (IOException)
                {
                    // Unreadable library file: just try the main library.
                }
                foreach (var lib in libraries)
                {
                    string apps = Path.Combine(lib, "steamapps");
                    string dirName = "Kingdoms and Castles";
                    try
                    {
                        string acf = Path.Combine(apps, "appmanifest_" + AppId + ".acf");
                        if (File.Exists(acf)) dirName = ParseInstallDir(File.ReadAllText(acf)) ?? dirName;
                    }
                    catch (IOException)
                    {
                    }
                    string candidate = Path.Combine(apps, "common", dirName);
                    if (IsGameFolder(candidate)) return candidate;
                }
            }
            return null;
        }

        /// <summary>Likely Steam install folders: registry values first, then the defaults.</summary>
        public static List<string> SteamRoots()
        {
            var roots = new List<string>();
            void Add(string p)
            {
                if (string.IsNullOrEmpty(p)) return;
                p = p.Replace('/', '\\').TrimEnd('\\');
                if (!roots.Exists(r => string.Equals(r, p, StringComparison.OrdinalIgnoreCase))) roots.Add(p);
            }
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) Add(k?.GetValue("SteamPath") as string);
                using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")) Add(k?.GetValue("InstallPath") as string);
                using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam")) Add(k?.GetValue("InstallPath") as string);
            }
            catch
            {
                // No registry access: fall back to the defaults.
            }
            Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Steam");
            Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Steam");
            return roots;
        }
    }
}
