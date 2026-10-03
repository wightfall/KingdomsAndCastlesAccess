using System;

namespace KCAccess.Core
{
    /// <summary>
    /// Collects typed letters into a search prefix; the prefix resets after a pause.
    /// Typing the same letter repeatedly cycles through items starting with that letter.
    /// </summary>
    public sealed class TypeAhead
    {
        private readonly Func<double> clock;
        private double lastTime = double.MinValue;

        public TypeAhead(Func<double> clock, double timeoutSeconds = 1.0)
        {
            this.clock = clock;
            Timeout = timeoutSeconds;
        }

        public double Timeout { get; set; }

        public string Prefix { get; private set; } = string.Empty;

        /// <summary>Adds a character and returns the prefix to search for.</summary>
        public string Add(char c)
        {
            double now = clock();
            if (now - lastTime > Timeout) Prefix = string.Empty;
            lastTime = now;
            string next = Prefix + char.ToLowerInvariant(c);
            // Repeating one letter ("aaa") means "cycle through items starting with a".
            bool allSame = true;
            foreach (char ch in next)
            {
                if (ch != next[0])
                {
                    allSame = false;
                    break;
                }
            }
            Prefix = allSame ? next.Substring(0, 1) : next;
            return Prefix;
        }

        public void Reset() => Prefix = string.Empty;
    }
}
