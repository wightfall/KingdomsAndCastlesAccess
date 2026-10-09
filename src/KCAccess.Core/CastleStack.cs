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
            if (next < 1) return Loc.T("Already 1 level, the fewest");
            if (next > MaxLevels) return Loc.F("Already {0} levels, the most", MaxLevels);
            Levels = next;
            return LevelsText(Levels);
        }

        public void Reset() => Levels = 1;

        public static string LevelsText(int levels) => Loc.P(levels, "{0} level per placement of castle blocks", "{0} levels per placement of castle blocks");

        /// <summary>Where the next block goes: "level 3", or "levels 3 to 5" when several levels are built at once.</summary>
        public static string Where(int blocksBelow, int levels)
        {
            int first = blocksBelow + 1;
            return levels > 1 ? Loc.F("levels {0} to {1}", first, first + levels - 1) : Loc.F("level {0}", first);
        }

        /// <summary>"Placed Stone Wall, 3 levels, 12 pieces", with why it stopped early when it did.</summary>
        public static string Summary(string name, int levelsBuilt, int levelsWanted, int pieces, string stopReason)
        {
            string text = Loc.P(levelsBuilt, "Placed {1}, {0} level", "Placed {1}, {0} levels", name);
            if (pieces > levelsBuilt) text += ", " + Loc.P(pieces, "{0} piece", "{0} pieces");
            if (levelsBuilt < levelsWanted)
                text += ", " + Loc.F("stopped before level {0} of {1}", levelsBuilt + 1, levelsWanted) + (string.IsNullOrEmpty(stopReason) ? string.Empty : ": " + stopReason);
            return text;
        }
    }
}
