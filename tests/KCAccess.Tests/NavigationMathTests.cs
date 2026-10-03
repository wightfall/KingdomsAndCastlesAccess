using System.Collections.Generic;
using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class BeaconMathTests
    {
        [Fact]
        public void EastIsRightEar()
        {
            var b = BeaconMath.Compute(10, 0);
            Assert.True(b.Pan > 0.99f);
            Assert.Equal(1f, b.Pitch, 2);
        }

        [Fact]
        public void WestIsLeftEar()
        {
            Assert.True(BeaconMath.Compute(-4, 0).Pan < -0.99f);
        }

        [Fact]
        public void NorthIsCentredAndHigher()
        {
            var b = BeaconMath.Compute(0, 7);
            Assert.Equal(0f, b.Pan, 2);
            Assert.True(b.Pitch > 1.2f);
        }

        [Fact]
        public void SouthIsCentredAndLower()
        {
            var b = BeaconMath.Compute(0, -7);
            Assert.Equal(0f, b.Pan, 2);
            Assert.True(b.Pitch < 0.85f);
        }

        [Fact]
        public void DiagonalsPanPartly()
        {
            var ne = BeaconMath.Compute(5, 5);
            Assert.InRange(ne.Pan, 0.6f, 0.8f);
            Assert.True(ne.Pitch > 1f);
            var sw = BeaconMath.Compute(-5, -5);
            Assert.InRange(sw.Pan, -0.8f, -0.6f);
            Assert.True(sw.Pitch < 1f);
        }

        [Fact]
        public void CloserIsFaster()
        {
            Assert.True(BeaconMath.Compute(2, 0).Interval < BeaconMath.Compute(40, 0).Interval);
            Assert.InRange(BeaconMath.Compute(500, 500).Interval, BeaconMath.MinInterval, BeaconMath.MaxInterval);
        }

        [Fact]
        public void SameTileIsArrived()
        {
            var b = BeaconMath.Compute(0, 0);
            Assert.True(b.Arrived);
            Assert.Equal(0, b.Distance);
        }

        [Fact]
        public void DistanceIsKingMoves()
        {
            Assert.Equal(9, BeaconMath.Compute(3, -9).Distance);
        }
    }

    public class PathStepsTests
    {
        [Fact]
        public void LineIncludesEndpoints()
        {
            var line = new List<GridPos>(PathSteps.Line(new GridPos(0, 0), new GridPos(3, 0)));
            Assert.Equal(new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0), new GridPos(3, 0) }, line.ToArray());
        }

        [Fact]
        public void DiagonalLineIsEightConnected()
        {
            var line = new List<GridPos>(PathSteps.Line(new GridPos(0, 0), new GridPos(3, 3)));
            Assert.Equal(4, line.Count);
            for (int i = 1; i < line.Count; i++) Assert.Equal(1, Directions.Distance(line[i - 1], line[i]));
        }

        [Fact]
        public void ExpandFollowsWaypointsTileByTile()
        {
            var steps = PathSteps.Expand(new GridPos(0, 0), new[] { new GridPos(2, 0), new GridPos(2, 3) });
            Assert.Equal(new[] { new GridPos(1, 0), new GridPos(2, 0), new GridPos(2, 1), new GridPos(2, 2), new GridPos(2, 3) }, steps.ToArray());
            for (int i = 1; i < steps.Count; i++) Assert.Equal(1, Directions.Distance(steps[i - 1], steps[i]));
        }

        [Fact]
        public void ExpandSkipsStartAndDuplicates()
        {
            var steps = PathSteps.Expand(new GridPos(5, 5), new[] { new GridPos(5, 5), new GridPos(6, 5), new GridPos(6, 5) });
            Assert.Equal(new[] { new GridPos(6, 5) }, steps.ToArray());
        }

        [Fact]
        public void EmptyWaypointsGiveNoSteps()
        {
            Assert.Empty(PathSteps.Expand(new GridPos(1, 1), new GridPos[0]));
        }

        [Fact]
        public void StepDirectionNames()
        {
            Assert.Equal("north east", PathSteps.StepDirection(new GridPos(0, 0), new GridPos(1, 1)));
            Assert.Equal("west", PathSteps.StepDirection(new GridPos(3, 3), new GridPos(2, 3)));
        }
    }
}

namespace KCAccess.Tests
{
    public class GridPathfinderTests
    {
        private static System.Func<int, int, bool> Map(string[] rows) => (x, z) => rows[rows.Length - 1 - z][x] != '#';

        [Fact]
        public void StraightLineOnOpenGround()
        {
            var p = GridPathfinder.Find(10, 10, new GridPos(0, 0), new GridPos(4, 0), (x, z) => true);
            Assert.Equal(4, p.Count);
            Assert.Equal(new GridPos(4, 0), p[p.Count - 1]);
        }

        [Fact]
        public void DiagonalWhenFree()
        {
            var p = GridPathfinder.Find(10, 10, new GridPos(0, 0), new GridPos(3, 3), (x, z) => true);
            Assert.Equal(3, p.Count);
        }

        [Fact]
        public void GoesAroundAWall()
        {
            string[] rows =
            {
                ".....",
                ".###.",
                ".#...",
                ".#...",
                ".....",
            };
            // start bottom-left (0,0), goal inside the pocket (2,2)
            var p = GridPathfinder.Find(5, 5, new GridPos(0, 2), new GridPos(2, 2), Map(rows));
            Assert.NotNull(p);
            foreach (var s in p) Assert.True(Map(rows)(s.X, s.Z), "stepped on wall at " + s);
            for (int i = 1; i < p.Count; i++) Assert.Equal(1, Directions.Distance(p[i - 1], p[i]));
            Assert.Equal(new GridPos(2, 2), p[p.Count - 1]);
        }

        [Fact]
        public void NoCornerCutting()
        {
            string[] rows =
            {
                "#.",
                ".#",
            };
            // (0,0) to (1,1) diagonally squeezes between two walls: not allowed, and no other route.
            Assert.Null(GridPathfinder.Find(2, 2, new GridPos(0, 0), new GridPos(1, 1), Map(rows)));
        }

        [Fact]
        public void UnreachableGoalReturnsNull()
        {
            string[] rows =
            {
                "..#..",
                "..#..",
                "..#..",
            };
            Assert.Null(GridPathfinder.Find(5, 3, new GridPos(0, 1), new GridPos(4, 1), Map(rows)));
        }

        [Fact]
        public void BlockedGoalReturnsNull()
        {
            Assert.Null(GridPathfinder.Find(3, 3, new GridPos(0, 0), new GridPos(2, 2), (x, z) => !(x == 2 && z == 2)));
        }

        [Fact]
        public void PrefersCheapTiles()
        {
            // Middle row is expensive except a road along the top.
            System.Func<int, int, int> cost = (x, z) => z == 2 ? 1 : (x == 2 ? 50 : 1);
            var p = GridPathfinder.Find(5, 3, new GridPos(0, 0), new GridPos(4, 0), (x, z) => true, cost);
            Assert.DoesNotContain(new GridPos(2, 0), p);
        }

        [Fact]
        public void SameTileIsEmptyPath()
        {
            Assert.Empty(GridPathfinder.Find(3, 3, new GridPos(1, 1), new GridPos(1, 1), (x, z) => true));
        }

        [Fact]
        public void OutOfBoundsIsNull()
        {
            Assert.Null(GridPathfinder.Find(3, 3, new GridPos(0, 0), new GridPos(5, 5), (x, z) => true));
        }
    }
}
