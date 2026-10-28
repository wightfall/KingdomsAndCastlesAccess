using System.Collections.Generic;
using KCAccess.Core;
using KCAccess.Installer;
using Xunit;

namespace KCAccess.Tests
{
    public class IslandAttacksTests
    {
        private readonly List<int> started = new List<int>(), ended = new List<int>();

        [Fact]
        public void EachIslandStartsOnceAndEndsAfterQuietTime()
        {
            var t = new IslandAttacks(5);
            t.Update(new[] { 2 }, 0, started, ended);
            Assert.Equal(new[] { 2 }, started);
            Assert.Empty(ended);

            // A second kingdom attacked: only it is new.
            t.Update(new[] { 2, 0, 0 }, 1, started, ended);
            Assert.Equal(new[] { 0 }, started);
            Assert.Empty(ended);

            // Island 2 unseen for less than the quiet time: still attacked.
            t.Update(new[] { 0 }, 4, started, ended);
            Assert.Empty(started);
            Assert.Empty(ended);

            t.Update(new[] { 0 }, 6, started, ended);
            Assert.Empty(started);
            Assert.Equal(new[] { 2 }, ended);
            Assert.Equal(new[] { 0 }, t.Active);

            t.Update(new int[0], 20, started, ended);
            Assert.Equal(new[] { 0 }, ended);
            Assert.Empty(t.Active);
        }

        [Fact]
        public void ComingBackAfterTheEndIsANewAttack()
        {
            var t = new IslandAttacks(5);
            t.Update(new[] { 1 }, 0, started, ended);
            t.Update(null, 10, started, ended);
            Assert.Equal(new[] { 1 }, ended);
            t.Update(new[] { 1 }, 11, started, ended);
            Assert.Equal(new[] { 1 }, started);
        }

        [Fact]
        public void NegativeIslandsAndClearAreIgnored()
        {
            var t = new IslandAttacks(5);
            t.Update(new[] { -1 }, 0, started, ended);
            Assert.Empty(started);
            t.Update(new[] { 3 }, 0, started, ended);
            t.Clear();
            t.Update(new[] { 3 }, 1, started, ended);
            Assert.Equal(new[] { 3 }, started);
        }

        [Theory]
        [InlineData("Longvale", true, true, "Longvale, your island")]
        [InlineData("", true, true, "your island")]
        [InlineData("Stot", false, true, "Stot kingdom")]
        [InlineData(null, false, true, "foreign kingdom")]
        [InlineData("Wild", false, false, "unclaimed island")]
        public void DescribesTheIslandsOwner(string name, bool mine, bool owned, string expected)
        {
            Assert.Equal(expected, IslandAttacks.Describe(name, mine, owned));
        }
    }

    public class WaterTextTests
    {
        private static WaterText.Neighbour N(string name, string dir, WaterText.Link state) =>
            new WaterText.Neighbour { Name = name, Direction = dir, State = state };

        [Fact]
        public void NothingNextToItSaysNotConnectedAndTheNearest()
        {
            Assert.Equal("not connected to the water system", WaterText.Placing(new List<WaterText.Neighbour>(), false, null));
            Assert.Equal("not connected to the water system, nearest water building: Aqueduct, 3 tiles north, 3 north",
                WaterText.Placing(null, false, "Aqueduct, 3 tiles north, 3 north"));
            Assert.Equal("no pipes connected yet", WaterText.Placing(null, true, null));
        }

        [Fact]
        public void ListsWhatItConnectsToAndWhetherWaterReachesIt()
        {
            var wet = new[] { N("Aqueduct", "north", WaterText.Link.Water), N("Reservoir", "east", WaterText.Link.Dry) };
            Assert.Equal("connects to Aqueduct to the north, has water; Reservoir to the east, no water. Water will reach it", WaterText.Placing(wet, false, "ignored"));
            var dry = new[] { N("Aqueduct", "west", WaterText.Link.Unbuilt) };
            Assert.Equal("connects to Aqueduct to the west, still being built. No water reaches it yet", WaterText.Placing(dry, false, null));
            // A water wheel is the source: no "water will reach it".
            Assert.Equal("connects to Aqueduct to the west, still being built", WaterText.Placing(dry, true, null));
        }

        [Fact]
        public void SaysWhetherAFinishedBuildingWorks()
        {
            Assert.Equal("Bathhouse has water and works.", WaterText.Built("Bathhouse", false, true, true));
            Assert.StartsWith("Reservoir is connected, but the water is too weak", WaterText.Built("Reservoir", false, true, false));
            Assert.StartsWith("Reservoir is connected, but no water wheel pumps yet", WaterText.Built("Reservoir", false, true, false, pumping: false));
            Assert.StartsWith("Aqueduct is not connected to a water wheel", WaterText.Built("Aqueduct", false, false, false));
            Assert.Equal("Noria pumps water.", WaterText.Built("Noria", true, true, true));
            Assert.Equal("Noria pumps water once all its workers are there.", WaterText.Built("Noria", true, true, false));
        }

        [Fact]
        public void PiecesAndFountains()
        {
            Assert.Equal("Aqueduct: all 5 pieces have water.", WaterText.BuiltMany("Aqueduct", 5, 5));
            Assert.Equal("Aqueduct: 2 of 5 pieces have water.", WaterText.BuiltMany("Aqueduct", 5, 2));
            Assert.StartsWith("Aqueduct: none of the 5 pieces has water.", WaterText.BuiltMany("Aqueduct", 5, 0));
            Assert.Equal("Fountain is on irrigated land and works.", WaterText.FountainBuilt("Fountain", true));
            Assert.StartsWith("Fountain is not on irrigated land", WaterText.FountainBuilt("Fountain", false));
            Assert.StartsWith("irrigated land", WaterText.FountainPlacing(true));
            Assert.StartsWith("not irrigated", WaterText.FountainPlacing(false));
        }
    }

    public class ReleaseNotesTests
    {
        [Fact]
        public void KeepsEveryLineWithoutMarkdown()
        {
            string md = "## What's new\r\n\r\n- **Update window** shows the notes\r\n* `V` says the [nearest](https://x) pipe\r\n\r\n\r\n---\r\nSee https://github.com/a/b\r\n  - nested\r\n![img](pic.png)\r\n<br>\r\n";
            Assert.Equal(new[]
            {
                "What's new",
                "",
                "- Update window shows the notes",
                "- V says the nearest pipe",
                "",
                "See https://github.com/a/b",
                "  - nested",
                "img"
            }, ReleaseNotes.Lines(md));
        }

        [Fact]
        public void LongNotesAreNotCut()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 200; i++) sb.Append("- line ").Append(i).Append('\n');
            var lines = ReleaseNotes.Lines(sb.ToString());
            Assert.Equal(200, lines.Count);
            Assert.Equal("- line 199", lines[199]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  \n \n")]
        public void EmptyNotesHaveNoLines(string md)
        {
            Assert.Empty(ReleaseNotes.Lines(md));
        }
    }
}
