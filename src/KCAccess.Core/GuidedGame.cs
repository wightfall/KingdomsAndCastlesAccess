using System;
using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>
    /// A spoken guide through a first kingdom: the buildings a new kingdom needs before its first winters, one step at
    /// a time. The game's own tutorial is pictures and arrows only. Steps are worked out from what is built, so the
    /// guide needs no saved state: a loaded kingdom that has everything is simply complete.
    /// </summary>
    public static class GuidedGame
    {
        public sealed class Step
        {
            /// <summary>Unique names of the buildings that count (any of them).</summary>
            public readonly string[] Buildings;
            /// <summary>How many of them must be finished.</summary>
            public readonly int Needed;
            /// <summary>Short name, said when the step is done ("Houses").</summary>
            public readonly string Name;
            /// <summary>What to do; {BindingId} names the player's keys.</summary>
            public readonly string Text;

            public Step(string name, string text, int needed, params string[] buildings)
            {
                Name = name;
                Text = text;
                Needed = needed;
                Buildings = buildings;
            }

            public bool Done(Func<string, int> built)
            {
                int n = 0;
                foreach (var b in Buildings) n += built(b);
                return n >= Needed;
            }
        }

        public static readonly List<Step> Steps = new List<Step>
        {
            new Step(Loc.N("Keep"), Loc.N("build your keep. {BuildMenu} opens the build menu, the keep is in the Castle category. Put it near fertile land, trees and stone; {Survey} surveys the area."), 1, "keep"),
            new Step(Loc.N("Roads"), Loc.N("build roads from the keep. New buildings must be near a road. Road is in the Town category; Shift Enter marks the start and Enter at the end builds the whole line. Build at least 5 pieces."), 5, "road", "stoneroad"),
            new Step(Loc.N("Houses"), Loc.N("build two houses near the keep. The Hovel is in the Town category. Villagers need homes, and new people only come when there is room."), 2, "smallhouse", "largehouse", "manorhouse"),
            new Step(Loc.N("Farms"), Loc.N("build farms on fertile land, at least 4 farm tiles. Farm is in the Food category; Shift Enter marks one corner and Enter the other to build a whole field."), 4, "farm", "orchard"),
            new Step(Loc.N("Granary"), Loc.N("build a granary. Small Granary is in the Food category. Food that is not stored when winter ends is lost."), 1, "smallgranary", "largegranary"),
            new Step(Loc.N("Stockpile"), Loc.N("build a stockpile for wood and stone. Small Stockpile is in the Industry category."), 1, "smallstockpile", "largestockpile"),
            new Step(Loc.N("Wood"), Loc.N("build a forester near a forest for a steady supply of wood. It is in the Industry category. {ChopMode} chop trees mode also lets villagers cut trees you mark."), 1, "forester"),
            new Step(Loc.N("Stone"), Loc.N("build a quarry on a stone deposit. It is in the Industry category; {NextCategory} reaches the Stone scan category and {NextItem} picks the nearest deposit."), 1, "quarry", "largequarry"),
            new Step(Loc.N("Charcoal"), Loc.N("build a charcoal maker near trees. Homes need charcoal to stay warm in winter. It is in the Industry category."), 1, "charcoalmaker", "largecharcoalmaker"),
            new Step(Loc.N("Well"), Loc.N("build a well among the houses. It gives drinking water and helps put out fires. It is in the Town category."), 1, "well", "fountain"),
            new Step(Loc.N("Happiness"), Loc.N("build a tavern (Town category) or a chapel (Advanced town category). Happy villagers work better and pay their taxes. {Status} shows how happy your kingdom is."), 1, "tavern", "largetavern", "chapel", "church", "cathedral"),
        };

        public static int Count => Steps.Count;

        /// <summary>Index of the first step that is not done, or <see cref="Count"/> when all are.</summary>
        public static int Current(Func<string, int> built)
        {
            if (built == null) return 0;
            for (int i = 0; i < Steps.Count; i++)
                if (!Steps[i].Done(built)) return i;
            return Steps.Count;
        }

        /// <summary>"Step 3 of 10: build farms ..." (keys still as {BindingId}).</summary>
        public static string Describe(int index)
        {
            if (index < 0) index = 0;
            if (index >= Steps.Count) return Loc.T("Guided game complete: your kingdom has what it needs for its first years. Grow it with more houses, food and happiness. You can turn the guided game off in Mod settings.");
            return Loc.F("Step {0} of {1}: {2}", index + 1, Steps.Count, Loc.T(Steps[index].Text));
        }

        /// <summary>What to say when the guide moved from step <paramref name="from"/> to <paramref name="to"/>, or null.</summary>
        public static string Progress(int from, int to)
        {
            if (to <= from) return null;
            var done = new List<string>();
            for (int i = Math.Max(0, from); i < to && i < Steps.Count; i++) done.Add(Loc.T(Steps[i].Name));
            string head = done.Count > 0 ? Loc.F("Guided game: {0} done.", string.Join(", ", done.ToArray())) : null;
            return TextUtil.Sentences(head, Describe(to));
        }
    }
}
