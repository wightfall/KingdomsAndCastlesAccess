using System.Collections.Generic;
using System.Linq;
using System.Text;
using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class LongTextTests
    {
        [Fact]
        public void LinesFollowTheTextAndEmptyLinesStartGroups()
        {
            var lines = LongText.Lines("Below are notes.\n\n5/11/2025 - version 123r4\r\nNew Research\n<b>Dragon Nest</b>\n\n\n- Small fixes");
            Assert.Equal(new[] { "Below are notes.", "5/11/2025 - version 123r4", "New Research", "Dragon Nest", "- Small fixes" }, lines.Select(l => l.Text));
            Assert.Equal(new[] { true, true, false, false, true }, lines.Select(l => l.StartsGroup));
        }

        [Fact]
        public void BreakTagsAndEscapedBreaksAreLineBreaks()
        {
            var lines = LongText.Lines("One<br>Two<BR/>Three\\nFour");
            Assert.Equal(new[] { "One", "Two", "Three", "Four" }, lines.Select(l => l.Text));
        }

        [Fact]
        public void LongLinesAreSplitIntoSentences()
        {
            string sentence = "This dragon nest provides a home for two dragons and one offspring. ";
            var lines = LongText.Lines(sentence + sentence + sentence + "Short end!");
            Assert.Equal(4, lines.Count);
            Assert.True(lines[0].StartsGroup);
            Assert.False(lines[1].StartsGroup);
            Assert.Equal("Short end!", lines[3].Text);
            Assert.All(lines, l => Assert.True(l.Text.Length <= LongText.MaxLine));
        }

        [Fact]
        public void OnlyLongOrManyLineTextsOpenTheReader()
        {
            Assert.False(LongText.WorthReading("A farm."));
            Assert.False(LongText.WorthReading("One\nTwo"));
            Assert.True(LongText.WorthReading("One\nTwo\nThree"));
            Assert.True(LongText.WorthReading(new string('a', LongText.ReaderLength)));
            Assert.False(LongText.WorthReading(null));
        }

        [Fact]
        public void OnlyVeryLongTextsAreSummarised()
        {
            Assert.Null(LongText.Summary(new string('a', 500)));
            var sb = new StringBuilder("Below are notes.\n");
            for (int i = 0; i < 100; i++) sb.Append("- Fixed problem number ").Append(i).Append(" in the game.\n");
            Assert.Equal("Below are notes. Long text, 101 lines. Enter reads it line by line", LongText.Summary(sb.ToString()));
        }

        [Fact]
        public void CountsTheLinesOfAGroup()
        {
            var lines = new List<LongText.Line>
            {
                new LongText.Line { Text = "Credits", StartsGroup = true },
                new LongText.Line { Text = "Peter" },
                new LongText.Line { Text = "Michael" },
                new LongText.Line { Text = "Very special thanks to our translators!", StartsGroup = true },
                new LongText.Line { Text = "German", StartsGroup = true },
                new LongText.Line { Text = "Alexander" },
            };
            Assert.Equal(2, LongText.LinesInGroup(lines, 0));
            Assert.Equal(0, LongText.LinesInGroup(lines, 3));
            Assert.Equal(1, LongText.LinesInGroup(lines, 4));
        }
    }

    public class ModReferencesTests
    {
        [Fact]
        public void BareNameIsDroppedWhenTheSameFileIsReferencedByPath()
        {
            // As the game's mod compiler has them with BepInEx installed.
            var refs = new List<string>
            {
                "mscorlib.dll", "0Harmony.dll",
                @"C:/Program Files (x86)/Steam/steamapps/common/Kingdoms and Castles/KingdomsAndCastles_Data\Managed\0Harmony.dll",
                @"C:/Program Files (x86)/Steam/steamapps/common/Kingdoms and Castles/KingdomsAndCastles_Data\Managed\Assembly-CSharp.dll",
            };
            Assert.Equal(1, ModReferences.DropBareDuplicates(refs));
            Assert.DoesNotContain("0Harmony.dll", refs);
            Assert.Contains("mscorlib.dll", refs); // no path reference: kept
            Assert.Equal(3, refs.Count);
            Assert.Equal(0, ModReferences.DropBareDuplicates(refs));
            Assert.Equal(0, ModReferences.DropBareDuplicates(null));
        }
    }

    public class BannerLooksTests
    {
        [Fact]
        public void TheTwoBlueBannersAreToldApart()
        {
            Assert.Equal("blue, a white crowned lion's head", BannerLooks.Describe("banner_pete_sprite", "blue"));
            Assert.Equal("blue with an orange diagonal band, a white crowned lion's head", BannerLooks.Describe("banner_original_sprite", "blue"));
            Assert.Equal("green, a white twenty-sided die", BannerLooks.Describe("Banner_Bret", "green"));
            Assert.Equal("red", BannerLooks.Describe("some_new_banner", "red"));
            Assert.Null(BannerLooks.Describe(null, null));
        }
    }
}
