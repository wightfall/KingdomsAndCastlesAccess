using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace KCAccess.Installer
{
    /// <summary>Installs, updates and removes the mod in a game folder.</summary>
    public sealed class ModInstaller
    {
        public const string PluginRelative = @"BepInEx\plugins\KCAccess";
        public const string UpdaterName = "KCAccess-Updater.exe";

        public string GameDir { get; }

        public ModInstaller(string gameDir)
        {
            GameDir = gameDir ?? throw new ArgumentNullException(nameof(gameDir));
        }

        public string PluginDir => Path.Combine(GameDir, PluginRelative);

        public bool BepInExInstalled => File.Exists(Path.Combine(GameDir, @"BepInEx\core\BepInEx.dll")) && File.Exists(Path.Combine(GameDir, "winhttp.dll"));

        /// <summary>Version of the installed mod (from KCAccess.dll), or null.</summary>
        public Version InstalledVersion
        {
            get
            {
                string dll = Path.Combine(PluginDir, "KCAccess.dll");
                if (!File.Exists(dll)) return null;
                try
                {
                    var info = FileVersionInfo.GetVersionInfo(dll);
                    return Versions.Parse(info.ProductVersion) ?? Versions.Parse(info.FileVersion);
                }
                catch
                {
                    return null;
                }
            }
        }

        public static bool GameRunning() => Process.GetProcessesByName("KingdomsAndCastles").Length > 0;

        /// <summary>
        /// Which zip entries to extract. The mod folder always; BepInEx itself only when requested
        /// (so an existing BepInEx install and other mods are left alone). BepInEx/config is never touched.
        /// </summary>
        public static bool ShouldExtract(string entryPath, bool includeLoader)
        {
            if (string.IsNullOrEmpty(entryPath)) return false;
            string p = entryPath.Replace('\\', '/').TrimStart('/');
            if (p.EndsWith("/")) return false; // directory entry
            if (p.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase)) return false;
            if (p.StartsWith("BepInEx/plugins/KCAccess/", StringComparison.OrdinalIgnoreCase)) return true;
            if (p.Equals("KCAccess-README.md", StringComparison.OrdinalIgnoreCase)) return true;
            return includeLoader;
        }

        /// <summary>
        /// Files of the player in Languages: their own copies (Languages/xx.txt) and the record of what they were merged
        /// with (Languages/default/.base). The shipped Languages/default/*.txt are replaced by every install.
        /// </summary>
        public bool IsPlayerLanguageFile(string path)
        {
            string languages = Path.Combine(PluginDir, "Languages") + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(languages, StringComparison.OrdinalIgnoreCase)) return false;
            string rel = full.Substring(languages.Length);
            if (rel.IndexOf(Path.DirectorySeparatorChar) < 0) return !string.Equals(rel, "template.txt", StringComparison.OrdinalIgnoreCase);
            return rel.StartsWith("default" + Path.DirectorySeparatorChar + ".base" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Combines safely: rejects entries that would escape the target folder ("../").</summary>
        public static string SafeCombine(string root, string entryPath)
        {
            string full = Path.GetFullPath(Path.Combine(root, entryPath.Replace('/', Path.DirectorySeparatorChar)));
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(KCAccess.Core.Loc.F("Unsafe path in package: {0}", entryPath));
            return full;
        }

        /// <summary>
        /// Installs from a release zip. BepInEx is installed when it is missing (or when forced).
        /// Returns the number of files written.
        /// </summary>
        public int Install(Stream zipStream, bool forceLoader = false, Action<string> log = null)
        {
            bool includeLoader = forceLoader || !BepInExInstalled;
            int written = 0;
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                bool hasMod = false;
                foreach (var e in zip.Entries)
                {
                    if (e.FullName.Replace('\\', '/').StartsWith("BepInEx/plugins/KCAccess/", StringComparison.OrdinalIgnoreCase)) hasMod = true;
                }
                if (!hasMod) throw new InvalidDataException(KCAccess.Core.Loc.T("This package does not contain KCAccess."));
                if (includeLoader && zip.GetEntry("winhttp.dll") == null && zip.GetEntry("BepInEx/core/BepInEx.dll") == null)
                    throw new InvalidDataException(KCAccess.Core.Loc.T("BepInEx is not installed and this package does not contain it. Use the -with-BepInEx package."));

                // Start from a clean mod folder so files removed in a new version do not linger. The player's own
                // language files (Languages/*.txt and Languages/default/.base) are kept: the mod merges updates into them.
                if (Directory.Exists(PluginDir))
                {
                    foreach (var f in Directory.GetFiles(PluginDir, "*", SearchOption.AllDirectories))
                    {
                        if (string.Equals(Path.GetFileName(f), UpdaterName, StringComparison.OrdinalIgnoreCase)) continue;
                        if (IsPlayerLanguageFile(f)) continue;
                        File.Delete(f);
                    }
                }
                foreach (var e in zip.Entries)
                {
                    if (!ShouldExtract(e.FullName, includeLoader)) continue;
                    string target = SafeCombine(GameDir, e.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    e.ExtractToFile(target, overwrite: true);
                    written++;
                    log?.Invoke(e.FullName);
                }
            }
            return written;
        }

        /// <summary>Removes the mod; optionally BepInEx as well (which also disables any other BepInEx mods).</summary>
        public void Uninstall(bool removeLoader)
        {
            if (Directory.Exists(PluginDir)) Directory.Delete(PluginDir, recursive: true);
            DeleteIfExists(Path.Combine(GameDir, "KCAccess-README.md"));
            string cfg = Path.Combine(GameDir, @"BepInEx\config\kcaccess.screenreader.cfg");
            DeleteIfExists(cfg);
            if (!removeLoader) return;
            foreach (var f in new[] { "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt" }) DeleteIfExists(Path.Combine(GameDir, f));
            string bep = Path.Combine(GameDir, "BepInEx");
            if (Directory.Exists(bep)) Directory.Delete(bep, recursive: true);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>
        /// Copies the running setup program into the game folder as KCAccess-Updater.exe so the player can
        /// check for updates later. A running copy is renamed out of the way first (Windows allows that).
        /// </summary>
        public string InstallUpdaterCopy(string sourceExe)
        {
            string target = Path.Combine(GameDir, UpdaterName);
            if (string.Equals(Path.GetFullPath(sourceExe), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) return target;
            ReplaceFile(sourceExe, target);
            return target;
        }

        /// <summary>Replaces target with source even if target is the running program.</summary>
        public static void ReplaceFile(string source, string target)
        {
            string old = target + ".old";
            if (File.Exists(old))
            {
                try
                {
                    File.Delete(old);
                }
                catch (IOException)
                {
                    // Still in use by an older updater; it is removed next time.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            if (File.Exists(target))
            {
                try
                {
                    File.Delete(target);
                }
                catch (Exception)
                {
                    if (!File.Exists(old)) File.Move(target, old);
                }
            }
            File.Copy(source, target, overwrite: true);
        }
    }
}
