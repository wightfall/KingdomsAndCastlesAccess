using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class ClipTextTests
    {
        [Theory]
        [InlineData("T1003", "Copied: T1003")]
        [InlineData("", "Nothing to copy here")]
        [InlineData(null, "Nothing to copy here")]
        public void ShortCopiesAreReadBack(string copied, string said) => Assert.Equal(said, ClipText.Announce(copied));

        [Fact]
        public void LongOrManyLineCopiesAreCounted()
        {
            Assert.Equal("Copied, 81 characters", ClipText.Announce(new string('a', 81)));
            Assert.Equal("Copied, 3 characters", ClipText.Announce("a\nb"));
            Assert.Equal("Copied: " + new string('a', 80), ClipText.Announce(new string('a', 80)));
        }

        [Theory]
        [InlineData("  T1003\r\n", "T1003")]
        [InlineData("my\tkingdom\r\ncode", "my kingdom code")]
        [InlineData("a  b", "a b")]
        [InlineData("a\u0007b", "ab")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void PastedTextBecomesOneLine(string clip, string expected) => Assert.Equal(expected, ClipText.OneLine(clip));

        [Fact]
        public void AmountFieldsTakeOnlyDigits()
        {
            Assert.Equal("1250", ClipText.OneLine(" 1,250 gold\n", digitsOnly: true));
            Assert.Equal(string.Empty, ClipText.OneLine("abc", digitsOnly: true));
        }

        [Fact]
        public void PasteReplacesTheSelection()
        {
            // A field just opened has everything selected, as typing would replace it.
            string text = ClipText.Insert("OLD", 0, 3, "T1003", 0, false, out string pasted, out int caret);
            Assert.Equal("T1003", text);
            Assert.Equal("T1003", pasted);
            Assert.Equal(5, caret);
        }

        [Fact]
        public void PasteGoesInAtTheCaret()
        {
            string text = ClipText.Insert("ab", 1, 1, "XY", 0, false, out _, out int caret);
            Assert.Equal("aXYb", text);
            Assert.Equal(3, caret);
        }

        [Fact]
        public void PasteTakesASelectionDraggedBackwards()
        {
            Assert.Equal("aXd", ClipText.Insert("abcd", 3, 1, "X", 0, false, out _, out int caret));
            Assert.Equal(2, caret);
        }

        [Fact]
        public void PasteKeepsToTheLimitOfTheField()
        {
            string text = ClipText.Insert("ab", 2, 2, "cdefgh", 5, false, out string pasted, out int caret);
            Assert.Equal("abcde", text);
            Assert.Equal("cde", pasted);
            Assert.Equal(5, caret);
            Assert.Equal("abcde", ClipText.Insert("abcde", 5, 5, "x", 5, false, out pasted, out _));
            Assert.Equal(string.Empty, pasted);
        }

        [Fact]
        public void PasteSurvivesPositionsOutsideTheText()
        {
            Assert.Equal("abX", ClipText.Insert("ab", 9, 12, "X", 0, false, out _, out _));
            Assert.Equal("Xab", ClipText.Insert("ab", -3, -1, "X", 0, false, out _, out _));
            Assert.Equal("X", ClipText.Insert(null, 0, 0, "X", 0, false, out _, out _));
        }

        [Theory]
        [InlineData("Key list. Up and Down read one key at a time, Escape closes.", "Key list")]
        [InlineData("Text reader. 604 lines, 143 groups. Up and Down read", "Text reader")]
        [InlineData("Kingdom status", "Kingdom status")]
        [InlineData("Notifications, 3 messages", "Notifications, 3 messages")]
        [InlineData("Notifications, 3 messages.", "Notifications, 3 messages")]
        [InlineData("\u30ad\u30fc\u4e00\u89a7\u3002\u4e0a\u4e0b\u3067\u8aad\u307f\u4e0a\u3052", "\u30ad\u30fc\u4e00\u89a7")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void ACopiedListStartsWithItsNameOnly(string title, string heading) => Assert.Equal(heading, ClipText.Heading(title));

        [Fact]
        public void PasteIsAnnounced()
        {
            Assert.Equal("The clipboard is empty", ClipText.AnnouncePaste("", "abc", true));
            Assert.Equal("Nothing was pasted: the field is full or takes no such text", ClipText.AnnouncePaste("", "abc", false));
            Assert.Equal("Pasted T1003", ClipText.AnnouncePaste("T1003", "T1003", false));
            Assert.Equal("Pasted XY. The field now reads aXYb", ClipText.AnnouncePaste("XY", "aXYb", false));
        }
    }
}
