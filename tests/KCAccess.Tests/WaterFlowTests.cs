using System.Collections.Generic;
using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    /// <summary>
    /// WaterFlow copies the game's water flood (124r3s). The cases marked "seen in the game" were built in a test
    /// kingdom on that version and read back from the game's own HasWater flags.
    /// </summary>
    public class WaterFlowTests
    {
        private sealed class Map
        {
            private readonly Dictionary<long, WaterFlow.Node> tiles = new Dictionary<long, WaterFlow.Node>();
            public readonly List<WaterFlow.Node> Wheels = new List<WaterFlow.Node>();

            public WaterFlow.Node Add(WaterFlow.Kind kind, int x, int z, int width = 1, int depth = 1, bool pumps = false)
            {
                var n = new WaterFlow.Node { Kind = kind, Pumps = pumps, MinX = x, MinZ = z, MaxX = x + width - 1, MaxZ = z + depth - 1 };
                for (int i = x; i < x + width; i++) for (int j = z; j < z + depth; j++) tiles[Key(i, j)] = n;
                if (kind == WaterFlow.Kind.Wheel) Wheels.Add(n);
                return n;
            }

            public WaterFlow.Node Wheel(int x, int z, bool pumps = true) => Add(WaterFlow.Kind.Wheel, x, z, pumps: pumps);

            public WaterFlow.Node Pipe(int x, int z) => Add(WaterFlow.Kind.Aqueduct, x, z);

            public List<WaterFlow.Node> Pipes(int fromX, int toX, int z)
            {
                var l = new List<WaterFlow.Node>();
                for (int x = fromX; x <= toX; x++) l.Add(Pipe(x, z));
                return l;
            }

            public WaterFlow.Node Reservoir(int x, int z) => Add(WaterFlow.Kind.Other, x, z);

            public void Flood() => WaterFlow.Flood(Wheels, (x, z) => tiles.TryGetValue(Key(x, z), out var n) ? n : null, 200, 200);

            private static long Key(int x, int z) => x * 100000L + z;
        }

        [Fact]
        public void AWheelWithAllItsWorkersFillsThePipesBesideIt()
        {
            var m = new Map();
            var wheel = m.Wheel(10, 10);
            var pipes = m.Pipes(11, 14, 10);
            var reservoir = m.Reservoir(15, 10);
            m.Flood();
            Assert.True(wheel.HasWater);
            Assert.All(pipes, p => Assert.True(p.HasWater && p.Connected && p.ReachedByPump));
            Assert.True(reservoir.HasWater);
            Assert.Equal(WaterText.Reach.Water, WaterText.ReachOf(reservoir));
        }

        [Fact]
        public void AWheelWaitingForWorkersLinksButGivesNoWater()
        {
            // Seen in the game: every piece behind it is connected and dry.
            var m = new Map();
            var wheel = m.Wheel(10, 10, pumps: false);
            var pipes = m.Pipes(11, 14, 10);
            var reservoir = m.Reservoir(15, 10);
            m.Flood();
            Assert.False(wheel.HasWater);
            Assert.All(pipes, p => Assert.True(p.Connected && !p.HasWater && !p.ReachedByPump));
            Assert.Equal(WaterText.Reach.NoPump, WaterText.ReachOf(reservoir));
        }

        [Fact]
        public void TheWaterReachesTwentyFourPipesAndNoFurther()
        {
            var m = new Map();
            m.Wheel(10, 10);
            var pipes = m.Pipes(11, 40, 10);
            m.Flood();
            for (int i = 0; i < pipes.Count; i++) Assert.Equal(i < 24, pipes[i].HasWater);
            Assert.All(pipes, p => Assert.True(p.Connected));
            Assert.Equal(WaterText.Reach.TooWeak, WaterText.ReachOf(pipes[24]));
        }

        [Fact]
        public void AReservoirBesideTheLastWetPipeStillGetsWater()
        {
            // Seen in the game: the reservoir beside the last pipe with water works, the one beside the first dry pipe
            // does not, although it touches the working reservoir too.
            var m = new Map();
            m.Wheel(10, 10);
            var pipes = m.Pipes(11, 36, 10);
            var besideLastWet = m.Reservoir(34, 11);
            var besideFirstDry = m.Reservoir(35, 11);
            m.Flood();
            Assert.True(pipes[23].HasWater);
            Assert.False(pipes[24].HasWater);
            Assert.True(besideLastWet.HasWater);
            Assert.False(besideFirstDry.HasWater);
            Assert.Equal(WaterText.Reach.TooWeak, WaterText.ReachOf(besideFirstDry));
        }

        [Fact]
        public void APipeBesideTheLastWetPipeStaysDry()
        {
            var m = new Map();
            m.Wheel(10, 10);
            m.Pipes(11, 34, 10);
            var branch = m.Pipe(34, 11);
            m.Flood();
            Assert.False(branch.HasWater);
            Assert.True(branch.ReachedByPump);
        }

        [Fact]
        public void BathHousesAndDragonBathsTakeMorePressure()
        {
            Assert.Equal(2, WaterFlow.Cost(WaterFlow.Kind.Aqueduct));
            Assert.Equal(2, WaterFlow.Cost(WaterFlow.Kind.Other));
            Assert.Equal(6, WaterFlow.Cost(WaterFlow.Kind.Bathhouse));
            Assert.Equal(8, WaterFlow.Cost(WaterFlow.Kind.DragonBath));
            // A bath house (3 by 3) right behind the wheel takes 6 of the 48 points: 21 pipes behind it get water.
            var m = new Map();
            m.Wheel(10, 11);
            m.Add(WaterFlow.Kind.Bathhouse, 11, 10, 3, 3);
            var pipes = m.Pipes(14, 40, 11);
            m.Flood();
            for (int i = 0; i < pipes.Count; i++) Assert.Equal(i < 21, pipes[i].HasWater);
        }

        [Fact]
        public void CornerToCornerDoesNotLink()
        {
            var m = new Map();
            m.Wheel(10, 10);
            var diagonal = m.Pipe(11, 11);
            m.Flood();
            Assert.False(diagonal.Connected);
            Assert.Equal(WaterText.Reach.NotLinked, WaterText.ReachOf(diagonal));
        }

        [Fact]
        public void ASecondWheelDoesNotPayAgainForWhatTheFirstFilled()
        {
            // 40 pipes between two wheels: the first fills 24 from its end, the second the other 16 and has pressure left.
            var m = new Map();
            m.Wheel(10, 10);
            var pipes = m.Pipes(11, 50, 10);
            m.Wheel(51, 10);
            m.Flood();
            Assert.All(pipes, p => Assert.True(p.HasWater));
        }

        [Fact]
        public void OneWheelIsTooWeakForFortyPipes()
        {
            var m = new Map();
            m.Wheel(10, 10);
            var pipes = m.Pipes(11, 50, 10);
            m.Wheel(51, 10, pumps: false);
            m.Flood();
            Assert.True(pipes[23].HasWater);
            Assert.False(pipes[24].HasWater);
            Assert.False(pipes[39].HasWater);
        }

        [Fact]
        public void AWheelWithoutWorkersBehindAWorkingOneGetsWaterAndCostsPressure()
        {
            var m = new Map();
            m.Wheel(10, 10);
            var idle = m.Wheel(11, 10, pumps: false);
            var pipes = m.Pipes(12, 40, 10);
            m.Flood();
            Assert.True(idle.HasWater);
            for (int i = 0; i < pipes.Count; i++) Assert.Equal(i < 23, pipes[i].HasWater);
        }

        [Fact]
        public void PiecesNotBuiltYetAreNoPartOfTheChain()
        {
            // The mod leaves construction sites out of the map it hands over, as the game's flood skips them.
            var m = new Map();
            m.Wheel(10, 10);
            m.Pipe(11, 10);
            var beyondTheGap = m.Pipe(13, 10);
            m.Flood();
            Assert.False(beyondTheGap.Connected);
        }

        [Fact]
        public void ALargeBuildingPassesWaterFromEverySide()
        {
            var m = new Map();
            m.Wheel(10, 11);
            m.Add(WaterFlow.Kind.Other, 11, 10, 3, 3); // dragon roost or large reservoir
            var north = m.Pipe(12, 13);
            var east = m.Pipe(14, 12);
            var corner = m.Pipe(14, 13);
            m.Flood();
            Assert.True(north.HasWater);
            Assert.True(east.HasWater);
            Assert.True(corner.HasWater); // through the pipes beside it, not through the corner
        }

        [Fact]
        public void TheResultsOfAnEarlierFloodAreNotReused()
        {
            var m = new Map();
            var wheel = m.Wheel(10, 10);
            var pipe = m.Pipe(11, 10);
            m.Flood();
            Assert.True(pipe.HasWater);
            // The same nodes again: nothing is left marked as visited, the flood runs as the first time.
            pipe.HasWater = false;
            wheel.HasWater = false;
            m.Flood();
            Assert.True(pipe.HasWater);
        }

        [Theory]
        [InlineData(WaterText.Reach.Water, "Water will reach it")]
        [InlineData(WaterText.Reach.TooWeak, "The water is too weak to reach it here. Connect another water wheel or shorten the pipes")]
        [InlineData(WaterText.Reach.NoPump, "No water reaches it yet: no water wheel pumps, a water wheel needs all its workers")]
        [InlineData(WaterText.Reach.NotLinked, "No water reaches it yet")]
        public void ThePlacingHintSaysWhatWillBecomeOfThePiece(WaterText.Reach reach, string said)
        {
            var touching = new List<WaterText.Neighbour> { new WaterText.Neighbour { Name = "Aqueduct", Direction = "west", State = WaterText.Link.Water } };
            Assert.Equal("connects to Aqueduct to the west, has water. " + said, WaterText.Placing(touching, false, null, reach));
        }

        [Fact]
        public void WithoutAForecastTheHintGoesByTheNeighbours()
        {
            var wet = new List<WaterText.Neighbour> { new WaterText.Neighbour { Name = "Aqueduct", Direction = "west", State = WaterText.Link.Water } };
            var dry = new List<WaterText.Neighbour> { new WaterText.Neighbour { Name = "Aqueduct", Direction = "west", State = WaterText.Link.Dry } };
            Assert.EndsWith("Water will reach it", WaterText.Placing(wet, false, null));
            Assert.EndsWith("No water reaches it yet", WaterText.Placing(dry, false, null));
            Assert.Equal("connects to Aqueduct to the west, has water", WaterText.Placing(wet, true, null, WaterText.Reach.Water));
        }
    }
}
