using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>
    /// Which islands attackers (vikings on foot, dragons in the air) are on, so a world with several kingdoms hears each
    /// island's attack start and end on its own. An island counts as attacked from the first time it is seen and as free
    /// again only after it went unseen for <see cref="QuietSeconds"/> (a dragon circling out over the water is not the
    /// end of its attack).
    /// </summary>
    public sealed class IslandAttacks
    {
        private readonly Dictionary<int, double> lastSeen = new Dictionary<int, double>();

        public double QuietSeconds { get; }

        public IslandAttacks(double quietSeconds) => QuietSeconds = quietSeconds;

        /// <summary>Islands under attack now.</summary>
        public IEnumerable<int> Active => lastSeen.Keys;

        public void Clear() => lastSeen.Clear();

        /// <summary>
        /// The islands with attackers at <paramref name="now"/> (seconds). Returns the islands whose attack just began
        /// (in the order seen) and those quiet long enough to call it over (by island number).
        /// </summary>
        public void Update(IEnumerable<int> seen, double now, List<int> started, List<int> ended)
        {
            started.Clear();
            ended.Clear();
            if (seen != null)
                foreach (int lm in seen)
                {
                    if (lm < 0) continue;
                    if (!lastSeen.ContainsKey(lm) && !started.Contains(lm)) started.Add(lm);
                    lastSeen[lm] = now;
                }
            foreach (var kv in lastSeen)
                if (now - kv.Value >= QuietSeconds) ended.Add(kv.Key);
            ended.Sort();
            foreach (int lm in ended) lastSeen.Remove(lm);
        }

        /// <summary>"Longvale, your island", "Stot kingdom", "your island", "foreign kingdom" or "unclaimed island".</summary>
        public static string Describe(string name, bool mine, bool owned)
        {
            bool named = TextUtil.HasContent(name);
            if (mine) return named ? Loc.F("{0}, your island", name) : Loc.T("your island");
            if (owned) return named ? Loc.F("{0} kingdom", name) : Loc.T("foreign kingdom");
            return Loc.T("unclaimed island");
        }
    }
}
