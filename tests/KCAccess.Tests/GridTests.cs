using System.Collections.Generic;
using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class GridCursorTests
    {
        [Fact]
        public void MoveClampsToMap()
        {
            var c = new GridCursor(10, 8);
            c.Set(0, 0);
            Assert.Equal(MoveResult.Blocked, c.Move(-1, 0));
            Assert.Equal(MoveResult.Moved, c.Move(1, 0, 5));
            Assert.Equal(new GridPos(5, 0), c.Pos);
            Assert.Equal(MoveResult.Moved, c.Move(1, 0, 50));
            Assert.Equal(new GridPos(9, 0), c.Pos);
            Assert.Equal(MoveResult.Blocked, c.Move(1, 0));
        }

        [Fact]
        public void SetClamps()
        {
            var c = new GridCursor(10, 8);
            c.Set(-3, 100);
            Assert.Equal(new GridPos(0, 7), c.Pos);
        }

        [Fact]
        public void ResizeKeepsCursorInside()
        {
            var c = new GridCursor(100, 100);
            c.Set(90, 90);
            c.Resize(50, 60);
            Assert.Equal(new GridPos(49, 59), c.Pos);
        }

        [Fact]
        public void MoveUntilChangeStopsOnFirstDifferentCell()
        {
            // Grass for x < 6, forest from x = 6.
            var c = new GridCursor(20, 1);
            c.Set(1, 0);
            int moved = c.MoveUntilChange(1, 0, (x, z) => x < 6 ? "grass" : "forest");
            Assert.Equal(5, moved);
            Assert.Equal(new GridPos(6, 0), c.Pos);
        }

        [Fact]
        public void MoveUntilChangeStopsAtEdge()
        {
            var c = new GridCursor(5, 1);
            c.Set(2, 0);
            Assert.Equal(2, c.MoveUntilChange(1, 0, (x, z) => "same"));
            Assert.Equal(new GridPos(4, 0), c.Pos);
            Assert.Equal(0, c.MoveUntilChange(1, 0, (x, z) => "same"));
        }
    }

    public class DirectionsTests
    {
        [Theory]
        [InlineData(0, 1, "north")]
        [InlineData(1, 1, "north east")]
        [InlineData(1, 0, "east")]
        [InlineData(1, -1, "south east")]
        [InlineData(0, -5, "south")]
        [InlineData(-3, -3, "south west")]
        [InlineData(-2, 0, "west")]
        [InlineData(-1, 1, "north west")]
        [InlineData(0, 0, "here")]
        [InlineData(10, 1, "east")]
        public void Compass8(int dx, int dz, string expected)
        {
            Assert.Equal(expected, Directions.Compass8(dx, dz));
        }

        [Theory]
        [InlineData(2, 3, "3 north, 2 east")]
        [InlineData(-4, 0, "4 west")]
        [InlineData(0, -1, "1 south")]
        [InlineData(0, 0, "here")]
        public void Offset(int dx, int dz, string expected)
        {
            Assert.Equal(expected, Directions.Offset(dx, dz));
        }

        [Fact]
        public void DistanceIsChebyshev()
        {
            Assert.Equal(5, Directions.Distance(new GridPos(0, 0), new GridPos(5, 3)));
        }

        [Fact]
        public void RelativeDescribesDistanceAndDirection()
        {
            Assert.Equal("12 tiles north east, 6 north, 12 east", Directions.Relative(new GridPos(10, 10), new GridPos(22, 16)));
            Assert.Equal("1 tile south, 1 south", Directions.Relative(new GridPos(3, 3), new GridPos(3, 2)));
            Assert.Equal("at the cursor", Directions.Relative(new GridPos(3, 3), new GridPos(3, 3)));
        }

        [Fact]
        public void SortByDistanceNearestFirst()
        {
            var items = new List<GridPos> { new GridPos(10, 10), new GridPos(1, 1), new GridPos(5, 5) };
            var sorted = Directions.SortByDistance(items, p => p, new GridPos(0, 0));
            Assert.Equal(new[] { new GridPos(1, 1), new GridPos(5, 5), new GridPos(10, 10) }, sorted.ToArray());
        }

        [Fact]
        public void GridPosEquality()
        {
            Assert.True(new GridPos(1, 2) == new GridPos(1, 2));
            Assert.True(new GridPos(1, 2) != new GridPos(2, 1));
            Assert.Equal("1, 2", new GridPos(1, 2).ToString());
        }
    }

    public class ClustersTests
    {
        [Fact]
        public void FindsConnectedRegions()
        {
            string[] map =
            {
                "SS..",
                "S...",
                "..SS",
                "...S",
            };
            var clusters = Clusters.Find(4, 4, (x, z) => map[z][x] == 'S');
            Assert.Equal(2, clusters.Count);
            Assert.Equal(3, clusters[0].Count);
            Assert.Equal(3, clusters[1].Count);
        }

        [Fact]
        public void DiagonalsAreNotConnected()
        {
            string[] map = { "S.", ".S" };
            Assert.Equal(2, Clusters.Find(2, 2, (x, z) => map[z][x] == 'S').Count);
        }

        [Fact]
        public void EmptyGridHasNoClusters()
        {
            Assert.Empty(Clusters.Find(0, 0, (x, z) => true));
            Assert.Empty(Clusters.Find(3, 3, (x, z) => false));
        }

        [Fact]
        public void NearestPicksClosestCell()
        {
            var cluster = new List<GridPos> { new GridPos(10, 10), new GridPos(2, 3), new GridPos(7, 7) };
            Assert.Equal(new GridPos(2, 3), Clusters.Nearest(cluster, new GridPos(0, 0)));
        }
    }
}

namespace KCAccess.Tests
{
    public class BookmarksTests
    {
        [Fact]
        public void SetGetPerKingdom()
        {
            var b = new Bookmarks();
            b.Set("Castleburg", 1, new GridPos(5, 6));
            b.Set("Ashton", 1, new GridPos(9, 9));
            Assert.Equal(new GridPos(5, 6), b.Get("castleburg", 1));
            Assert.Equal(new GridPos(9, 9), b.Get("Ashton", 1));
            Assert.Null(b.Get("Castleburg", 2));
            Assert.Null(b.Get("Nowhere", 1));
            Assert.Null(b.Get("Castleburg", 0));
            Assert.Null(b.Get("Castleburg", 10));
        }

        [Fact]
        public void RoundTripsThroughText()
        {
            var b = new Bookmarks();
            b.Set("Castle|burg", 3, new GridPos(12, 40));
            b.Set("Ashton", 9, new GridPos(0, 1));
            var c = Bookmarks.Parse(b.Serialize());
            Assert.Equal(new GridPos(12, 40), c.Get("Castle/burg", 3));
            Assert.Equal(new GridPos(0, 1), c.Get("Ashton", 9));
        }

        [Fact]
        public void IgnoresMalformedLines()
        {
            var b = Bookmarks.Parse("garbage\nA|x|1|2\nA|12|1|2\nA|2|3|4\r\n");
            Assert.Equal(new GridPos(3, 4), b.Get("A", 2));
            Assert.Null(b.Get("A", 1));
        }

        [Fact]
        public void ClearRemoves()
        {
            var b = new Bookmarks();
            b.Set("A", 4, new GridPos(1, 1));
            b.Clear("A", 4);
            Assert.Null(b.Get("A", 4));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => b.Set("A", 0, new GridPos(1, 1)));
        }
    }
}
