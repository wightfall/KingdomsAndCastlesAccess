using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess
{
    /// <summary>
    /// Keeps one failing part of the mod from stopping the others. Each part of the frame runs on its own: when it throws,
    /// the error is logged once, the part rests for a moment (longer each time) and everything else carries on. Speech,
    /// menus and the keyboard must survive whatever a game update did to one feature.
    /// </summary>
    internal static class Safe
    {
        private const int FailuresToTell = 3;

        private sealed class Part
        {
            public int Failures;
            public float RestUntil;
        }

        private static readonly Dictionary<string, Part> parts = new Dictionary<string, Part>();
        private static readonly HashSet<string> logged = new HashSet<string>();
        private static bool told;

        /// <summary>Runs one part. False when it failed or is resting.</summary>
        internal static bool Run(string name, Action part)
        {
            if (!parts.TryGetValue(name, out var state)) parts[name] = state = new Part();
            if (state.Failures > 0 && Time.unscaledTime < state.RestUntil) return false;
            try
            {
                part();
                state.Failures = 0;
                return true;
            }
            catch (Exception e)
            {
                state.Failures++;
                state.RestUntil = Time.unscaledTime + CompatState.RestSeconds(state.Failures);
                Report(name, e);
                if (state.Failures >= FailuresToTell) Tell();
                return false;
            }
        }

        /// <summary>The part fails again and again.</summary>
        internal static bool Failing(string name) => parts.TryGetValue(name, out var state) && state.Failures >= FailuresToTell;

        /// <summary>Logs an error once per place and kind.</summary>
        internal static void Report(string where, Exception e)
        {
            if (logged.Count < 200 && logged.Add(where + "|" + e.GetType().Name + "|" + e.Message))
                Plugin.Log.LogError("[" + where + "] " + e);
        }

        private static void Tell()
        {
            if (told) return;
            told = true;
            A.SayQueued(Loc.T("Part of KCAccess stopped working. The rest keeps running. Details are in the BepInEx log."));
        }
    }

    /// <summary>
    /// The mod's hooks run inside the game's own methods, so an error in a hook would break the game's method, and could
    /// stop the game for everyone, not only the mod. Every hooked method gets a finalizer that takes such an error out of
    /// the game's way. The game's own errors pass through untouched.
    /// </summary>
    internal static class PatchGuard
    {
        internal static void Protect(Harmony harmony)
        {
            var finalizer = new HarmonyMethod(typeof(PatchGuard), nameof(Finalizer));
            var watch = Stopwatch.StartNew();
            int failed = 0, guarded = 0;
            foreach (var method in harmony.GetPatchedMethods().ToList())
            {
                try
                {
                    harmony.Patch(method, finalizer: finalizer);
                    guarded++;
                }
                catch (Exception e)
                {
                    failed++;
                    Plugin.Log.LogWarning("Could not guard " + method.DeclaringType?.Name + "." + method.Name + ": " + e.Message);
                }
            }
            Plugin.Log.LogInfo(guarded + " hooks guarded in " + watch.ElapsedMilliseconds + " ms.");
            if (failed > 0) Plugin.Log.LogWarning(failed + " hooks run without a guard.");
        }

        /// <summary>Developer test (debug command hooktest), on a game method that is harmless to hook for a moment.</summary>
        internal static void SelfTest()
        {
            var target = AccessTools.Method(typeof(Util), nameof(Util.IsSubclassOfSaveClass));
            var harmony = new Harmony("kcaccess.hooktest");
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(PatchGuard), nameof(ThrowingHook)), finalizer: new HarmonyMethod(typeof(PatchGuard), nameof(Finalizer)));
            try
            {
                testThrow = true;
                bool result = Util.IsSubclassOfSaveClass(typeof(int));
                Plugin.Log.LogInfo("[dbg] hook error kept from the game, result " + result);
                testThrow = false;
                try
                {
                    Util.IsSubclassOfSaveClass(null);
                    Plugin.Log.LogInfo("[dbg] the game raised nothing");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogInfo("[dbg] the game's own error passed through: " + e.GetType().Name);
                }
            }
            finally
            {
                testThrow = false;
                harmony.UnpatchSelf();
            }
        }

        private static bool testThrow;

        private static void ThrowingHook()
        {
            if (testThrow) throw new InvalidOperationException("test hook failure");
        }

        private static Exception Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            if (!FromTheMod(__exception)) return __exception;
            Safe.Report("hook", __exception);
            return null;
        }

        /// <summary>
        /// Whether mod code threw. Only frames between the hooked method and the throw are in the trace at this point, so a
        /// mod frame means a hook failed, not that the mod called a game method that failed on its own.
        /// </summary>
        private static bool FromTheMod(Exception e)
        {
            if (e is MissingMemberException || e is TypeLoadException) return true; // the method never started: no frame of its own
            var trace = new StackTrace(e, false);
            for (int i = 0; i < trace.FrameCount; i++)
            {
                var assembly = trace.GetFrame(i).GetMethod()?.DeclaringType?.Assembly;
                if (assembly == typeof(PatchGuard).Assembly || assembly == typeof(Loc).Assembly) return true;
            }
            return false;
        }
    }
}
