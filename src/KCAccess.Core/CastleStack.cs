namespace KCAccess.Core
{
    /// <summary>
    /// How many castle levels one placement of castle blocks builds (the game builds one; the mod repeats the
    /// same click or line for more), and the texts spoken about it.
    /// </summary>
    public sealed class CastleStack
    {
        public const int MaxLevels = 10;

        public int Levels { get; private set; } = 1;

        /// <summary>One more (+1) or one fewer (-1) level per placement; returns what to say.</summary>
        public string Change(int delta)
        {
            int next = Levels + delta;
            if (next < 1) return "Already 1 level, the fewest";
            if (next > MaxLevels) return "Already " + MaxLevels + " levels, the most";
            Levels = next;
            return LevelsText(Levels);
        }

        public void Reset() => Levels = 1;

        public static string LevelsText(int levels) => TextUtil.Plural(levels, "level") + " per placement of castle blocks";

        /// <summary>Where the next block goes: "level 3", or "levels 3 to 5" when several levels are built at once.</summary>
        public static string Where(int blocksBelow, int levels)
        {
            int first = blocksBelow + 1;
            return levels > 1 ? "levels " + first + " to " + (first + levels - 1) : "level " + first;
        }

        /// <summary>"Placed Stone Wall, 3 levels, 12 pieces", with why it stopped early when it did.</summary>
        public static string Summary(string name, int levelsBuilt, int levelsWanted, int pieces, string stopReason)
        {
            string text = "Placed " + name + ", " + TextUtil.Plural(levelsBuilt, "level");
            if (pieces > levelsBuilt) text += ", " + TextUtil.Plural(pieces, "piece");
            if (levelsBuilt < levelsWanted)
                text += ", stopped before level " + (levelsBuilt + 1) + " of " + levelsWanted + (string.IsNullOrEmpty(stopReason) ? string.Empty : ": " + stopReason);
            return text;
        }
    }
}
