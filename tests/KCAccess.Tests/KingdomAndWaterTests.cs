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

    public class KingdomNewsTests
    {
        [Theory]
        // mine, your islands setting, other kingdoms setting, said
        [InlineData(true, true, true, true)]
        [InlineData(true, false, true, false)]
        [InlineData(true, true, false, true)]
        [InlineData(false, true, true, true)]
        [InlineData(false, true, false, false)]
        [InlineData(false, false, true, true)]
        [InlineData(false, false, false, false)]
        public void EachSettingOnlySwitchesItsOwnIslands(bool mine, bool yours, bool others, bool said)
        {
            Assert.Equal(said, KingdomNews.AnnounceAttack(mine, yours, others));
        }

        [Fact]
        public void NewIslandsAreFoundAfterTheFirstLook()
        {
            var known = new Dictionary<int, HashSet<int>>();
            // Loading: every kingdom's islands are known, nothing is new.
            Assert.Empty(KingdomNews.NewIslands(known, new Dictionary<int, IEnumerable<int>> { { 3, new[] { 1 } }, { 4, new[] { 2 } } }));
            Assert.Empty(KingdomNews.NewIslands(known, new Dictionary<int, IEnumerable<int>> { { 3, new[] { 1 } }, { 4, new[] { 2 } } }));
            var found = KingdomNews.NewIslands(known, new Dictionary<int, IEnumerable<int>> { { 3, new[] { 5, 1, 0 } }, { 4, new[] { 2 } } });
            Assert.Equal(new[] { new KeyValuePair<int, int>(3, 0), new KeyValuePair<int, int>(3, 5) }, found);
            // Losing an island is not news; a kingdom appearing later starts as known.
            Assert.Empty(KingdomNews.NewIslands(known, new Dictionary<int, IEnumerable<int>> { { 3, new[] { 1 } }, { 7, new[] { 6 } } }));
            Assert.Equal(new[] { new KeyValuePair<int, int>(7, 8) }, KingdomNews.NewIslands(known, new Dictionary<int, IEnumerable<int>> { { 7, new[] { 6, 8 } } }));
        }

        [Fact]
        public void StanceChangesAfterTheFirstLook()
        {
            var known = new Dictionary<long, KingdomNews.Stance>();
            long youA = KingdomNews.PairKey(0, 2), ab = KingdomNews.PairKey(3, 2);
            Assert.Equal(KingdomNews.PairKey(2, 3), ab);
            KingdomNews.PairTeams(ab, out int a, out int b);
            Assert.Equal(2, a);
            Assert.Equal(3, b);
            // Loading: known, nothing announced; unchanged: nothing.
            var now = new Dictionary<long, KingdomNews.Stance> { { youA, KingdomNews.Stance.Peace }, { ab, KingdomNews.Stance.Peace } };
            Assert.Empty(KingdomNews.StanceChanges(known, now));
            Assert.Empty(KingdomNews.StanceChanges(known, now));
            now = new Dictionary<long, KingdomNews.Stance> { { youA, KingdomNews.Stance.War }, { ab, KingdomNews.Stance.Alliance } };
            Assert.Equal(new[] { new KeyValuePair<long, KingdomNews.Stance>(youA, KingdomNews.Stance.War), new KeyValuePair<long, KingdomNews.Stance>(ab, KingdomNews.Stance.Alliance) },
                KingdomNews.StanceChanges(known, now));
            Assert.Empty(KingdomNews.StanceChanges(known, now));
        }

        [Fact]
        public void Texts()
        {
            Assert.Equal("Halsbeck is now at war with you.", KingdomNews.StanceWithYou("Halsbeck", KingdomNews.Stance.War));
            Assert.Equal("Halsbeck is now at peace with you.", KingdomNews.StanceWithYou("Halsbeck", KingdomNews.Stance.Peace));
            Assert.Equal("Halsbeck is now your ally.", KingdomNews.StanceWithYou("Halsbeck", KingdomNews.Stance.Alliance));
            Assert.Equal("Halsbeck and Stot are now at war.", KingdomNews.StanceBetween("Halsbeck", "Stot", KingdomNews.Stance.War));
            Assert.Equal("Halsbeck and Stot are now allies.", KingdomNews.StanceBetween("Halsbeck", "Stot", KingdomNews.Stance.Alliance));
            Assert.Equal("Halsbeck and Stot are now at peace.", KingdomNews.StanceBetween("Halsbeck", "Stot", KingdomNews.Stance.Peace));
            Assert.Equal("An army of Halsbeck landed on Longvale, your island.", KingdomNews.ArmyLanded("Halsbeck", "Longvale, your island"));
            Assert.Equal("The army of Halsbeck no longer attacks Stot kingdom.", KingdomNews.ArmyLeft("Halsbeck", "Stot kingdom"));
            Assert.Equal("Fire in Stot kingdom, at Hovel", KingdomNews.FireIn("Stot kingdom", "Hovel"));
            Assert.Equal("Fire in Stot kingdom", KingdomNews.FireIn("Stot kingdom", null));
            Assert.Equal("Halsbeck claimed a new island.", KingdomNews.Claimed("Halsbeck"));
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
        public void ReachIsTheBoxGrownByOneWithoutCorners()
        {
            // Tested in the game: a water wheel's box 81,73 to 82,73 (base, then the wheel in the river).
            Assert.True(WaterText.InReach(81, 72, 81, 73, 82, 73));  // south of the base
            Assert.True(WaterText.InReach(80, 73, 81, 73, 82, 73));  // west of the base
            Assert.True(WaterText.InReach(83, 73, 81, 73, 82, 73));  // east of the wheel
            Assert.False(WaterText.InReach(80, 72, 81, 73, 82, 73)); // corner to corner
            Assert.False(WaterText.InReach(81, 71, 81, 73, 82, 73)); // two tiles away
            // ... and the dragon roost beside it, 76,71 to 80,75: a pipe at 81,71 was fed through the roost.
            Assert.True(WaterText.InReach(81, 71, 76, 71, 80, 75));
            Assert.False(WaterText.InReach(81, 70, 76, 71, 80, 75)); // corner
            // A 1 by 1 pipe: its four sides only.
            Assert.True(WaterText.InReach(5, 6, 5, 5, 5, 5));
            Assert.False(WaterText.InReach(6, 6, 5, 5, 5, 5));
        }

        [Fact]
        public void PiecesAndFountains()
        {
            Assert.Equal("Aqueduct: all 5 pieces have water.", WaterText.BuiltMany("Aqueduct", 5, 5));
            Assert.Equal("Aqueduct: 2 of 5 pieces have water.", WaterText.BuiltMany("Aqueduct", 5, 2));
            Assert.Equal("Aqueduct: none of the 5 pieces has water. Connect them to a water wheel.", WaterText.BuiltMany("Aqueduct", 5, 0));
            Assert.StartsWith("Aqueduct: none of the 5 pieces has water yet: no water wheel pumps", WaterText.BuiltMany("Aqueduct", 5, 0, connected: 2, pumping: false));
            Assert.StartsWith("Aqueduct: none of the 5 pieces has water: the water is too weak", WaterText.BuiltMany("Aqueduct", 5, 0, connected: 2, pumping: true));
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
