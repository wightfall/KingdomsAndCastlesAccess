using System;
using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>Something that can actually output speech/braille (Prism, logger, test fake).</summary>
    public interface ISpeechBackend
    {
        string Name { get; }
        bool Speak(string text, bool interrupt);
        void Stop();
    }

    /// <summary>Announcement priority. Higher priority messages may interrupt lower ones.</summary>
    public enum Priority
    {
        /// <summary>Background info (queued, never interrupts).</summary>
        Low = 0,
        /// <summary>Normal navigation feedback (interrupts previous navigation speech).</summary>
        Normal = 1,
        /// <summary>Important alerts such as raids (always spoken, interrupts).</summary>
        High = 2
    }

    /// <summary>
    /// Front door for all speech. Cleans text, suppresses rapid duplicates, keeps a history
    /// that the player can review, and forwards to the backend.
    /// </summary>
    public sealed class Announcer
    {
        private readonly ISpeechBackend backend;
        private readonly Func<double> clock;
        private readonly LinkedList<string> history = new LinkedList<string>();
        private string lastText;
        private double lastTime = double.MinValue;

        public int HistoryLimit { get; set; } = 100;

        /// <summary>Identical messages within this many seconds are dropped (unless forced).</summary>
        public double DuplicateWindow { get; set; } = 0.35;

        public bool Enabled { get; set; } = true;

        /// <summary>Raised for every message actually sent to the backend (used for logging).</summary>
        public event Action<string> Spoken;

        public Announcer(ISpeechBackend backend, Func<double> clock)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public string BackendName => backend.Name;

        public IEnumerable<string> History => history;

        public string LastSpoken => lastText;

        /// <summary>Speak text. Returns true if it was sent to the backend.</summary>
        public bool Say(string text, Priority priority = Priority.Normal, bool force = false)
        {
            if (!Enabled) return false;
            string clean = TextUtil.Clean(text);
            if (!TextUtil.HasContent(clean)) return false;
            double now = clock();
            if (!force && clean == lastText && now - lastTime < DuplicateWindow) return false;
            lastText = clean;
            lastTime = now;
            history.AddLast(clean);
            while (history.Count > HistoryLimit) history.RemoveFirst();
            bool interrupt = priority != Priority.Low;
            bool ok = backend.Speak(clean, interrupt);
            Spoken?.Invoke(clean);
            return ok;
        }

        /// <summary>Repeat the last message even if it was just spoken.</summary>
        public void Repeat()
        {
            if (lastText != null) Say(lastText, Priority.Normal, force: true);
        }

        public void Stop() => backend.Stop();
    }
}
