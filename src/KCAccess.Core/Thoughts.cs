using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>
    /// The game's thought bubbles (icons over villagers and buildings) are its main warning signs and are purely
    /// visual. Names are ThoughtBubbleSystem.Thought values.
    /// </summary>
    public static class Thoughts
    {
        private static readonly Dictionary<string, string> Meanings = new Dictionary<string, string>
        {
            { "House", "homeless" },
            { "Food", "hungry" },
            { "FoodCritical", "starving" },
            { "Plague", "sick with plague" },
            { "PlagueCritical", "badly sick with plague" },
            { "Wage", "wages not paid" },
            { "WageCritical", "wages not paid for a long time, soldiers may desert" },
            { "GeneralError", "cannot work, select it and read its panel for the reason" },
            { "Rats", "rats are eating the stored food" },
            { "DeadBody", "a dead villager waits for burial" },
            { "DeadBodyWarning", "an unburied body, plague may spread" },
            { "DeadBodySafe", "a dead villager, safe for now" },
        };

        /// <summary>Spoken meaning of a thought, or null for ones that are not a problem (Satisfied).</summary>
        public static string Meaning(string thought) => thought != null && Meanings.TryGetValue(thought, out var m) ? m : null;

        /// <summary>Problems urgent enough to announce when they appear.</summary>
        public static bool IsCritical(string thought) =>
            thought == "FoodCritical" || thought == "PlagueCritical" || thought == "WageCritical" || thought == "DeadBodyWarning" || thought == "Rats";

        /// <summary>"3 starving, 1 sick with plague" from counts per thought, most urgent first.</summary>
        public static string Summary(IDictionary<string, int> counts)
        {
            var order = new[] { "FoodCritical", "PlagueCritical", "WageCritical", "DeadBodyWarning", "Rats", "Plague", "Food", "Wage", "DeadBody", "House", "GeneralError", "DeadBodySafe" };
            var parts = new List<string>();
            foreach (var t in order)
                if (counts.TryGetValue(t, out int n) && n > 0) parts.Add(n + " " + Meaning(t));
            return string.Join(", ", parts.ToArray());
        }
    }
}
