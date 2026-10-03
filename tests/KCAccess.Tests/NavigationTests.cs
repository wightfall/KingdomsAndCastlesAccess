using System.Collections.Generic;
using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class NavListTests
    {
        private static NavList<string> Make(bool wrap = true, params string[] items)
        {
            var list = new NavList<string>(s => s, wrap);
            list.SetItems(items.Length > 0 ? items : new[] { "New", "Load", "Settings", "Lords", "Quit" }, keepFocus: false);
            return list;
        }

        [Fact]
        public void SetItems_FocusesFirst()
        {
            var list = Make();
            Assert.Equal("New", list.Current);
            Assert.Equal("1 of 5", list.PositionText);
        }

        [Fact]
        public void Next_WrapsAround()
        {
            var list = Make();
            list.Last();
            Assert.Equal(NavResult.Wrapped, list.Next());
            Assert.Equal("New", list.Current);
            Assert.Equal(NavResult.Wrapped, list.Previous());
            Assert.Equal("Quit", list.Current);
        }

        [Fact]
        public void Next_StopsAtEdgeWithoutWrap()
        {
            var list = Make(wrap: false);
            Assert.Equal(NavResult.HitEdge, list.Previous());
            Assert.Equal("New", list.Current);
            list.Last();
            Assert.Equal(NavResult.HitEdge, list.Next());
            Assert.Equal("Quit", list.Current);
        }

        [Fact]
        public void Empty_ReportsEmpty()
        {
            var list = new NavList<string>(s => s);
            Assert.Equal(NavResult.Empty, list.Next());
            Assert.False(list.HasCurrent);
            Assert.Equal(string.Empty, list.CurrentLabel);
            Assert.Equal(NavResult.Empty, list.First());
        }

        [Fact]
        public void SetItems_KeepsFocusOnSameItem()
        {
            var list = Make();
            list.Select("Settings");
            list.SetItems(new[] { "Continue", "New", "Load", "Settings", "Quit" });
            Assert.Equal("Settings", list.Current);
        }

        [Fact]
        public void SetItems_ClampsWhenFocusedItemDisappears()
        {
            var list = Make();
            list.Last();
            list.SetItems(new[] { "A", "B" });
            Assert.Equal("B", list.Current);
        }

        [Fact]
        public void FindNext_SingleLetterCycles()
        {
            var list = Make();
            Assert.True(list.FindNext("l"));
            Assert.Equal("Load", list.Current);
            Assert.True(list.FindNext("l"));
            Assert.Equal("Lords", list.Current);
            Assert.True(list.FindNext("l"));
            Assert.Equal("Load", list.Current);
        }

        [Fact]
        public void FindNext_MultiLetterKeepsCurrentWhenStillMatching()
        {
            var list = Make();
            list.FindNext("l");
            Assert.True(list.FindNext("lo"));
            Assert.Equal("Load", list.Current);
            Assert.True(list.FindNext("lor"));
            Assert.Equal("Lords", list.Current);
        }

        [Fact]
        public void FindNext_NoMatchKeepsPosition()
        {
            var list = Make();
            list.Select("Settings");
            Assert.False(list.FindNext("z"));
            Assert.Equal("Settings", list.Current);
        }

        [Fact]
        public void Step_FromNoSelectionStartsAtEnds()
        {
            var list = new NavList<string>(s => s);
            list.SetItems(new[] { "a", "b", "c" });
            list.SelectIndex(-5);
            Assert.Equal("a", list.Current);
            list.SelectIndex(99);
            Assert.Equal("c", list.Current);
        }
    }

    public class TypeAheadTests
    {
        [Fact]
        public void BuildsPrefixWithinTimeout()
        {
            double t = 0;
            var ta = new TypeAhead(() => t, 1.0);
            Assert.Equal("s", ta.Add('S'));
            t = 0.3;
            Assert.Equal("se", ta.Add('e'));
        }

        [Fact]
        public void ResetsAfterTimeout()
        {
            double t = 0;
            var ta = new TypeAhead(() => t, 1.0);
            ta.Add('s');
            t = 2.0;
            Assert.Equal("q", ta.Add('q'));
        }

        [Fact]
        public void RepeatedLetterStaysSingle()
        {
            double t = 0;
            var ta = new TypeAhead(() => t, 1.0);
            ta.Add('l');
            t = 0.1;
            Assert.Equal("l", ta.Add('l'));
            t = 0.2;
            Assert.Equal("l", ta.Add('l'));
        }
    }

    public class ReadingOrderTests
    {
        private struct Box
        {
            public string Name;
            public float X, Y;
        }

        [Fact]
        public void SortsTopToBottomThenLeftToRight()
        {
            var items = new List<Box>
            {
                new Box { Name = "bottom", X = 10, Y = 10 },
                new Box { Name = "top-right", X = 300, Y = 500 },
                new Box { Name = "top-left", X = 20, Y = 505 },
                new Box { Name = "middle", X = 50, Y = 250 },
            };
            var sorted = ReadingOrder.Sort(items, b => b.X, b => b.Y, 12f);
            Assert.Equal(new[] { "top-left", "top-right", "middle", "bottom" }, sorted.ConvertAll(b => b.Name).ToArray());
        }
    }
}
