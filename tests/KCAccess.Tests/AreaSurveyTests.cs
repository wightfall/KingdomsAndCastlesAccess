using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class AreaSurveyTests
    {
        [Fact]
        public void FormatsCountsAndSkipsEmptyKinds()
        {
            var s = new AreaSurvey { Radius = 6, Land = 150, Open = 90, Fertile = 40, VeryFertile = 10, Forest = 30, Water = 19 };
            string t = s.Format();
            Assert.StartsWith("Area 13 by 13 around the cursor. 150 land tiles. 90 open to build. 50 fertile, 10 of them very fertile. 30 forest", t);
            Assert.Contains("19 shallow water", t);
            Assert.DoesNotContain("stone", t);
            Assert.DoesNotContain("iron", t);
        }

        [Fact]
        public void MentionsNearestResourcesOnlyWhenNoneInside()
        {
            var s = new AreaSurvey { Radius = 2, Land = 25, Stone = 3, NearestStone = "9 tiles east, 9 east", NearestIron = "20 tiles north, 20 north", NearestWater = "8 tiles south, 8 south" };
            string t = s.Format();
            Assert.Contains("3 stone", t);
            Assert.DoesNotContain("nearest stone", t);
            Assert.Contains("nearest iron 20 tiles north", t);
            Assert.Contains("nearest water 8 tiles south", t);
            Assert.Contains("no fertile land", t);
        }

        [Fact]
        public void AllWaterSaysNoLandOnly()
        {
            var s = new AreaSurvey { Radius = 1, DeepWater = 9 };
            Assert.Equal("Area 3 by 3 around the cursor. 0 land tiles. 9 deep water.", s.Format());
        }
    }
}
