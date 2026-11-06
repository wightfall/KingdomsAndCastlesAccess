using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using KCAccess.Core;

namespace KCAccess
{
    /// <summary>
    /// Runs first, before anything that touches the game. On the game build this release was tested with it does nothing.
    /// On any other build it checks the mod against the game and, when something no longer fits, starts a repaired copy of
    /// the mod (see SelfRepair) in place of this one, so a game update does not leave the player without speech.
    /// Everything here must stay free of game types: this is the code that has to work when they have changed.
    /// </summary>
    internal static class Compat
    {
        private const string RepairedName = "KCAccess.Repaired";

        /// <summary>True when a repaired copy took over and this copy must stop here.</summary>
        internal static bool HandedOver(BaseUnityPlugin host, ManualLogSource log)
        {
            if (typeof(Compat).Assembly.GetName().Name == RepairedName) return false; // this is the copy
            try
            {
                CompatState.GameBuild = GameBuild();
                if (!CompatState.UntestedGame) return false;
                log.LogWarning("[compat] Game build " + (CompatState.GameBuild ?? "unknown") + ", tested with " + CompatState.TestedGameBuild + ".");

                // The player is told about a build that fits only once.
                string memo = Path.Combine(Paths.CachePath, "kcaccess_compat.txt");
                CompatState.ToldBefore = File.Exists(memo) && File.ReadAllText(memo).Trim() == MemoKey();

                var watch = Stopwatch.StartNew();
                string dll = host.Info.Location;
                var report = SelfRepair.Repair(File.ReadAllBytes(dll), SearchDirs(dll), RepairedName, Path.GetDirectoryName(dll), out byte[] repaired);
                CompatState.Report = report;
                log.LogWarning("[compat] Checked against the game in " + watch.ElapsedMilliseconds + " ms: " + report + ".");
                foreach (var line in CompatState.Details()) log.LogWarning("[compat] " + line);
                if (repaired == null) return false;

                var assembly = Assembly.Load(repaired);
                var plugin = assembly.GetType("KCAccess.Plugin", throwOnError: true);
                CompatState.Repaired = true;
                host.gameObject.AddComponent(plugin);
                var loaded = plugin.GetField("Loaded", BindingFlags.Static | BindingFlags.NonPublic);
                if (loaded != null && (bool)loaded.GetValue(null))
                {
                    log.LogWarning("[compat] Running the repaired copy.");
                    return true;
                }
                log.LogError("[compat] The repaired copy did not start; starting the mod as it is.");
            }
            catch (Exception e)
            {
                log.LogError("[compat] Self-repair failed, starting the mod as it is: " + e);
            }
            CompatState.Repaired = false;
            return false;
        }

        /// <summary>The player has heard about this game build.</summary>
        internal static void Remember()
        {
            try
            {
                File.WriteAllText(Path.Combine(Paths.CachePath, "kcaccess_compat.txt"), MemoKey());
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[compat] Could not remember the game build: " + e.Message);
            }
        }

        /// <summary>This game build with this mod version: a new mod version on the same build is worth a word again.</summary>
        private static string MemoKey() => CompatState.GameBuild + " " + Plugin.Version;

        private static string GameBuild()
        {
            try
            {
                return Assembly.Load("Assembly-CSharp").ManifestModule.ModuleVersionId.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string[] SearchDirs(string dll) => new[] { Paths.ManagedPath, Paths.BepInExAssemblyDirectory, Path.GetDirectoryName(dll) };
    }
}
