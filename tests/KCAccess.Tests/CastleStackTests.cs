using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class CastleStackTests
    {
        [Fact]
        public void LevelsStayBetweenOneAndMax()
        {
            var s = new CastleStack();
            Assert.Equal(1, s.Levels);
            Assert.Equal("Already 1 level, the fewest", s.Change(-1));
            Assert.Equal("2 levels per placement of castle blocks", s.Change(1));
            for (int i = 0; i < 20; i++) s.Change(1);
            Assert.Equal(CastleStack.MaxLevels, s.Levels);
            Assert.StartsWith("Already 10 levels", s.Change(1));
            s.Reset();
            Assert.Equal(1, s.Levels);
        }

        [Fact]
        public void WhereNamesTheLevels()
        {
            Assert.Equal("level 1", CastleStack.Where(0, 1));
            Assert.Equal("level 3", CastleStack.Where(2, 1));
            Assert.Equal("levels 3 to 5", CastleStack.Where(2, 3));
        }

        [Fact]
        public void SummaryTellsLevelsPiecesAndWhyItStopped()
        {
            Assert.Equal("Placed Stone Wall, 3 levels", CastleStack.Summary("Stone Wall", 3, 3, 3, null));
            Assert.Equal("Placed Stone Wall, 3 levels, 12 pieces", CastleStack.Summary("Stone Wall", 3, 3, 12, null));
            Assert.Equal("Placed Wooden Wall, 2 levels, stopped before level 3 of 4: wooden walls cannot be stacked higher",
                CastleStack.Summary("Wooden Wall", 2, 4, 2, "wooden walls cannot be stacked higher"));
        }
    }
}
