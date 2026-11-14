using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>
    /// How this copy of the mod fits the installed game. It lives here because both the mod and its repaired copy
    /// (SelfRepair) load this one assembly, so the copy can read what the check before it found.
    /// </summary>
    public static class CompatState
    {
        /// <summary>
        /// The Assembly-CSharp.dll this release was built and tested with (its module id, Steam build 26512598 of 10 November
        /// 2026). A unit test compares it with the installed game, so a release cannot go out without testing a new build.
        /// </summary>
        public const string TestedGameBuild = "1d731f8f-6e41-42f0-b6d3-18fb0294a40d";

        /// <summary>The installed game's build, or null when it could not be read.</summary>
        public static string GameBuild;

        /// <summary>Filled in by the check before the mod starts; null when the game is the tested build.</summary>
        public static RepairReport Report;

        /// <summary>The mod runs from its repaired copy.</summary>
        public static bool Repaired;

        /// <summary>Hooks into game methods that no longer exist.</summary>
        public static int FailedHooks;

        /// <summary>The player was already told about this game build in an earlier session.</summary>
        public static bool ToldBefore;

        public static bool UntestedGame => GameBuild != TestedGameBuild;

        /// <summary>Seconds a failing part of the mod rests before it is tried again: half a second, doubling up to 30.</summary>
        public static float RestSeconds(int failures) => System.Math.Min(30f, 0.5f * (1 << System.Math.Min(System.Math.Max(failures, 1) - 1, 8)));

        /// <summary>What the player loses until the mod is updated.</summary>
        public static int Lost => (Report != null ? Report.Lost : 0) + FailedHooks;

        /// <summary>The sentence for this situation, or null when there is nothing to say.</summary>
        public static string Notice()
        {
            if (!UntestedGame) return null;
            if (Lost > 0) return Loc.F("Kingdoms and Castles was updated after this version of KCAccess came out. KCAccess adapted itself so you can keep playing, but {0} things it reads from the game are not available until KCAccess is updated. To look for a new version, run KCAccess-Updater.exe in the game folder.", Lost);
            if (ToldBefore) return null;
            if (Repaired) return Loc.T("Kingdoms and Castles was updated after this version of KCAccess came out. KCAccess adapted itself and everything should keep working, but some screens may read differently until KCAccess is updated. To look for a new version, run KCAccess-Updater.exe in the game folder.");
            return Loc.T("Kingdoms and Castles was updated after this version of KCAccess came out. KCAccess found nothing broken, but some screens may read differently until KCAccess is updated. To look for a new version, run KCAccess-Updater.exe in the game folder.");
        }

        /// <summary>Lines for the log.</summary>
        public static IEnumerable<string> Details()
        {
            if (Report == null) yield break;
            foreach (var line in Report.Adapted) yield return "adapted: " + line;
            foreach (var line in Report.Missing) yield return "missing: " + line;
            foreach (var line in Report.Silenced) yield return "silenced: " + line;
        }
    }
}
