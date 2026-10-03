using System;
using KCAccess.Core;
using KCAccess.Speech;

namespace KCAccess
{
    /// <summary>Short global facade used everywhere in the mod: A.Say(...), A.Cue(...).</summary>
    internal static class A
    {
        internal static Announcer Announcer { get; private set; }

        private static PrismSpeech prism;

        internal static void Init(Announcer announcer, PrismSpeech prismSpeech)
        {
            Announcer = announcer;
            prism = prismSpeech;
        }

        internal static void Say(string text, Priority priority = Priority.Normal, bool force = false)
        {
            if (Announcer == null) return;
            try
            {
                Announcer.Say(text, priority, force);
            }
            catch (Exception e)
            {
                Plugin.Log?.LogError("Speech failed: " + e.Message);
            }
        }

        /// <summary>Queue text after whatever is being spoken (never interrupts).</summary>
        internal static void SayQueued(string text) => Say(text, Priority.Low);

        internal static void Cue(Cue cue) => AudioCues.Play(cue);

        internal static void Cue(Cue cue, float pitchScale) => AudioCues.Play(cue, pitchScale);

        internal static string BackendName => prism != null && prism.IsReady ? prism.Name : "log only";

        internal static bool Redetect() => prism != null && prism.Redetect();

        internal static void LogDebug(string msg) => Plugin.Log?.LogDebug(msg);

        internal static void LogError(string msg) => Plugin.Log?.LogError(msg);
    }
}
