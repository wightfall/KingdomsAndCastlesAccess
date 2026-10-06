using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class TextUtilTests
    {
        [Theory]
        [InlineData("<b>Keep</b>", "Keep")]
        [InlineData("<color=#26F200>100</color>", "100")]
        [InlineData("<color=red>Wood 5</color> <size=12>needed</size>", "Wood 5 needed")]
        [InlineData("  lots   of\tspace  ", "lots of space")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Clean_RemovesRichTextAndWhitespace(string input, string expected)
        {
            Assert.Equal(expected, TextUtil.Clean(input));
        }

        [Fact]
        public void Clean_TurnsLineBreaksIntoSentences()
        {
            Assert.Equal("Line one. Line two", TextUtil.Clean("Line one\nLine two"));
            Assert.Equal("Line one. Line two", TextUtil.Clean("Line one\r\n\r\nLine two"));
        }

        [Fact]
        public void Clean_ReplacesSpritesWithWords()
        {
            Assert.Equal("wood 10 stone 5", TextUtil.Clean("<sprite name=icon_wood> 10 <sprite name=\"icon_stone\"> 5"));
        }

        [Fact]
        public void Clean_DropsDecorativeSprites()
        {
            Assert.Equal("Fire at the farm", TextUtil.Clean("<sprite name=icon_magglass> Fire at the farm"));
        }

        [Fact]
        public void Clean_UnwrapsMissingTranslationMarkers()
        {
            Assert.Equal("You are about to demolish 28 buildings. Are you sure?",
                TextUtil.Clean("<!-Missing Translation [You are about to demolish 28 buildings. Are you sure?]-!>"));
        }

        [Fact]
        public void Clean_TurnsRulesIntoBreaks()
        {
            Assert.Equal("Report. Total 5", TextUtil.Clean("Report\n--------------------\nTotal 5"));
        }

        [Theory]
        [InlineData(0, "0 trees")]
        [InlineData(1, "1 tree")]
        [InlineData(4, "4 trees")]
        public void Plural_UsesEnglishPlural(int n, string expected)
        {
            Assert.Equal(expected, Loc.P(n, "{0} tree", "{0} trees"));
        }

        [Fact]
        public void Plural_AcceptsIrregularPlural()
        {
            Assert.Equal("2 enemy armies", Loc.P(2, "{0} enemy army", "{0} enemy armies"));
        }

        [Theory]
        [InlineData(0.5f, "50 percent")]
        [InlineData(0f, "0 percent")]
        [InlineData(1f, "100 percent")]
        [InlineData(float.NaN, "0 percent")]
        public void Percent_Rounds(float f, string expected)
        {
            Assert.Equal(expected, TextUtil.Percent(f));
        }

        [Theory]
        [InlineData("smallhouse", "Smallhouse")]
        [InlineData("AdvTown", "Adv Town")]
        [InlineData("wood_castle_block", "Wood castle block")]
        [InlineData("BackToTopLevel", "Back To Top Level")]
        public void Humanize_SplitsIdentifiers(string id, string expected)
        {
            Assert.Equal(expected, TextUtil.Humanize(id));
        }

        [Fact]
        public void Join_SkipsEmptyParts()
        {
            Assert.Equal("a, b", TextUtil.Join(", ", "a", "", null, " ", "<b></b>", "b"));
        }

        [Fact]
        public void Sentences_AddsPeriodsOnlyWhenMissing()
        {
            Assert.Equal("Title. Body text! Last", TextUtil.Sentences("Title", "Body text!", "", "Last"));
        }

        [Theory]
        [InlineData("---", false)]
        [InlineData("A", true)]
        [InlineData("7", true)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void HasContent_NeedsLetterOrDigit(string text, bool expected)
        {
            Assert.Equal(expected, TextUtil.HasContent(text));
        }

        [Theory]
        [InlineData("icon_wood", "wood")]
        [InlineData("icon_apple", "apples")]
        [InlineData("icon_magglass", "")]
        [InlineData("some_icon", "some icon")]
        public void SpriteWord_MapsIconNames(string sprite, string expected)
        {
            Assert.Equal(expected, TextUtil.SpriteWord(sprite));
        }

        [Fact]
        public void StartsWithIgnoreCase_CleansFirst()
        {
            Assert.True(TextUtil.StartsWithIgnoreCase("<b>Load</b>", "lo"));
            Assert.False(TextUtil.StartsWithIgnoreCase("Quit", "lo"));
        }
    }
}
