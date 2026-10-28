using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>
    /// What happens in the other kingdoms of the map, which a sighted player sees on it, and which of the three settings
    /// lets each message through. The settings never overlap: your islands' attacks, other kingdoms' attacks and other
    /// kingdoms' events (fires, new islands) each have their own.
    /// </summary>
    public static class KingdomNews
    {
        /// <summary>Whether an attack on an island is said: <paramref name="mine"/> follows the "your islands" setting,
        /// any other island the "other kingdoms" one.</summary>
        public static bool AnnounceAttack(bool mine, bool yourIslands, bool otherKingdoms) => mine ? yourIslands : otherKingdoms;

        /// <summary>
        /// Islands a kingdom owns now but did not at the last look (kingdom → its islands). Kingdoms seen for the first
        /// time count as known, so loading a game announces nothing.
        /// </summary>
        public static List<KeyValuePair<int, int>> NewIslands(Dictionary<int, HashSet<int>> known, IDictionary<int, IEnumerable<int>> now)
        {
            var found = new List<KeyValuePair<int, int>>();
            foreach (var kv in now)
            {
                var islands = new HashSet<int>(kv.Value);
                if (known.TryGetValue(kv.Key, out var before))
                {
                    var added = new List<int>();
                    foreach (int lm in islands) if (!before.Contains(lm)) added.Add(lm);
                    added.Sort();
                    foreach (int lm in added) found.Add(new KeyValuePair<int, int>(kv.Key, lm));
                }
                known[kv.Key] = islands;
            }
            return found;
        }

        public enum Stance
        {
            War,
            Peace,
            Alliance
        }

        /// <summary>
        /// Pairs of kingdoms (key: the two team numbers) whose stance differs from the last look. Pairs seen for the first
        /// time count as known, so loading a game announces nothing.
        /// </summary>
        public static List<KeyValuePair<long, Stance>> StanceChanges(Dictionary<long, Stance> known, IDictionary<long, Stance> now)
        {
            var found = new List<KeyValuePair<long, Stance>>();
            var keys = new List<long>(now.Keys);
            keys.Sort();
            foreach (long k in keys)
            {
                if (known.TryGetValue(k, out var before) && before != now[k]) found.Add(new KeyValuePair<long, Stance>(k, now[k]));
                known[k] = now[k];
            }
            return found;
        }

        /// <summary>The key of a pair of teams, the same whichever comes first.</summary>
        public static long PairKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        public static void PairTeams(long key, out int a, out int b)
        {
            a = (int)(key >> 32);
            b = (int)(uint)key;
        }

        /// <summary>A new stance between the player and <paramref name="kingdom"/>.</summary>
        public static string StanceWithYou(string kingdom, Stance s)
        {
            switch (s)
            {
                case Stance.War: return Loc.F("{0} is now at war with you.", kingdom);
                case Stance.Alliance: return Loc.F("{0} is now your ally.", kingdom);
                default: return Loc.F("{0} is now at peace with you.", kingdom);
            }
        }

        /// <summary>A new stance between two other kingdoms.</summary>
        public static string StanceBetween(string a, string b, Stance s)
        {
            switch (s)
            {
                case Stance.War: return Loc.F("{0} and {1} are now at war.", a, b);
                case Stance.Alliance: return Loc.F("{0} and {1} are now allies.", a, b);
                default: return Loc.F("{0} and {1} are now at peace.", a, b);
            }
        }

        public static string ArmyLanded(string kingdom, string island) => Loc.F("An army of {0} landed on {1}.", kingdom, island);

        public static string ArmyLeft(string kingdom, string island) => Loc.F("The army of {0} no longer attacks {1}.", kingdom, island);

        public static string FireIn(string island, string building) =>
            TextUtil.HasContent(building) ? Loc.F("Fire in {0}, at {1}", island, building) : Loc.F("Fire in {0}", island);

        public static string Claimed(string kingdom) => Loc.F("{0} claimed a new island.", kingdom);
    }
}
