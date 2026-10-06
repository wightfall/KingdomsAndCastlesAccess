using System.Collections.Generic;
using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class TwitchTextTests
    {
        private static List<VoteOption> Options(int a, int b) => new List<VoteOption>
        {
            new VoteOption(4, "Bountiful harvest", a),
            new VoteOption(5, "Plague", b),
        };

        [Theory]
        [InlineData(0, "0 seconds")]
        [InlineData(1, "1 second")]
        [InlineData(45, "45 seconds")]
        [InlineData(60, "1 minute")]
        [InlineData(125, "2 minutes 5 seconds")]
        [InlineData(-3, "0 seconds")]
        public void Durations(int seconds, string expected) => Assert.Equal(expected, TwitchText.Duration(seconds));

        [Fact]
        public void NewVoteListsNumbersViewersType() =>
            Assert.Equal("New Twitch vote, ends in 2 minutes: 4, Bountiful harvest; 5, Plague", TwitchText.NewVote(Options(0, 0), 120));

        [Fact]
        public void NewVoteWithoutOptionsExplains() => Assert.Contains("turned off", TwitchText.NewVote(new List<VoteOption>(), 120));

        [Fact]
        public void StatusCountsVotes() =>
            Assert.Equal("Twitch vote: 4 Bountiful harvest 3 votes, 5 Plague 1 vote, 45 seconds left", TwitchText.Status(Options(3, 1), 45));

        [Fact]
        public void LeadingAndTies()
        {
            Assert.Equal("No votes yet", TwitchText.Leading(Options(0, 0)));
            Assert.Equal("Leading: 5, Plague, 2 votes", TwitchText.Leading(Options(1, 2)));
            Assert.Equal("Tied at 1 vote: 4, Bountiful harvest and 5, Plague", TwitchText.Leading(Options(1, 1)));
        }

        [Fact]
        public void CountdownSaysTimeAndLeader() =>
            Assert.Equal("10 seconds left in the Twitch vote. Leading: 4, Bountiful harvest, 3 votes", TwitchText.Countdown(Options(3, 0), 10));

        [Fact]
        public void WinnerNamesVoter()
        {
            Assert.Equal("Twitch viewers chose Plague, picked by anna", TwitchText.Winner("Plague", "anna"));
            Assert.Equal("Twitch viewers chose Plague", TwitchText.Winner("Plague", ""));
        }
    }

    public class ChatFilterTests
    {
        private double now;

        private ChatFilter Make(int max = 3) => new ChatFilter(() => now, max, 10, 200);

        [Theory]
        [InlineData("#2", true)]
        [InlineData("# 3 please", true)]
        [InlineData("1", true)]
        [InlineData("hello #2", false)]
        [InlineData("nice castle", false)]
        public void VotesAreRecognisedLikeTheGame(string msg, bool vote) => Assert.Equal(vote, ChatFilter.IsVote(msg));

        [Fact]
        public void SpeaksNameAndMessage() => Assert.Equal("anna: nice castle", Make().Accept("anna", "nice castle"));

        [Fact]
        public void SkipsVotesAndEmpty()
        {
            var f = Make();
            Assert.Null(f.Accept("anna", "#2"));
            Assert.Null(f.Accept("anna", "   "));
            Assert.Equal(0, f.Dropped);
        }

        [Fact]
        public void LongMessagesAreCut()
        {
            string longMsg = new string('a', 150) + " " + new string('b', 150);
            string spoken = Make().Accept("x", longMsg);
            Assert.Equal("x: " + new string('a', 150), spoken);
        }

        [Fact]
        public void BusyChatIsSummarised()
        {
            var f = Make(2);
            Assert.NotNull(f.Accept("a", "one"));
            Assert.NotNull(f.Accept("b", "two"));
            Assert.Null(f.Accept("c", "three"));
            Assert.Null(f.Accept("d", "four"));
            Assert.Equal(2, f.Dropped);
            Assert.Null(f.Flush()); // still busy
            now = 11;
            Assert.Equal("and 2 more chat messages", f.Flush());
            Assert.Null(f.Flush());
            Assert.NotNull(f.Accept("e", "five"));
        }
    }

    public class CaptionTests
    {
        [Fact]
        public void KeepsLastThreeLines()
        {
            var c = new Captions(3, 6, 1);
            c.Add("one", 0);
            c.Add("two", 0);
            c.Add("three", 0);
            c.Add("four", 0);
            var v = c.Visible(1);
            Assert.Equal(3, v.Count);
            Assert.Equal("two", v[0].Text);
            Assert.Equal("four", v[2].Text);
        }

        [Fact]
        public void LinesFadeThenDisappear()
        {
            var c = new Captions(3, 6, 1);
            c.Add("hello", 0);
            Assert.Equal(1f, c.Visible(4)[0].Alpha);
            Assert.InRange(c.Visible(5.5)[0].Alpha, 0.4f, 0.6f);
            Assert.Empty(c.Visible(6));
            Assert.Equal(0, c.Count);
        }

        [Fact]
        public void EmptyTextIgnoredAndLongTextCut()
        {
            var c = new Captions { MaxLength = 20 };
            c.Add("  ", 0);
            Assert.Equal(0, c.Count);
            c.Add("aaaa bbbb cccc dddd eeee ffff", 0);
            Assert.Equal("aaaa bbbb cccc dddd ...", c.Visible(0)[0].Text);
        }
    }

    public class StreamOverlayTests
    {
        [Fact]
        public void BuildsLinesAndSkipsMissing()
        {
            string s = StreamOverlay.Build("Sommern", "Year 12, summer", "Population 40", "Gold 210", null);
            Assert.Equal("Kingdom: Sommern\r\nYear 12, summer\r\nPopulation 40\r\nGold 210\r\n", s);
        }

        [Fact]
        public void IncludesLastNotification() =>
            Assert.Contains("Last: A fire broke out", StreamOverlay.Build(null, "Year 1", null, null, "A fire broke out"));
    }
}
