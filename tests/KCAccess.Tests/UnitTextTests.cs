using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class UnitTextTests
    {
        [Theory]
        [InlineData(1f, "very happy")]
        [InlineData(2f, "happy")]
        [InlineData(1.5f, "happy")]
        [InlineData(3f, "neutral")]
        [InlineData(5f, "neutral")]
        [InlineData(6f, "angry")]
        [InlineData(8f, "angry")]
        [InlineData(9f, "very angry")]
        [InlineData(0f, "very angry")]
        public void NegotiationMoodMatchesTheGamesFaces(float value, string mood)
        {
            Assert.Equal(mood, UnitText.NegotiationMood(value));
        }

        [Fact]
        public void EveryUnitTabHasAName()
        {
            // Values of UnitUI.UnitTabs.
            foreach (var t in new[] { "All", "Knights", "Archers", "Settlers", "Envoys", "Catapults", "TransportShips", "Dragons", "Individual" })
                Assert.False(string.IsNullOrEmpty(UnitText.TabName(t)), t);
            Assert.Null(UnitText.TabName("Nope"));
        }

        [Fact]
        public void OwnerWords()
        {
            Assert.Equal("your dragon", UnitText.Owned(true, false, "dragon"));
            Assert.Equal("enemy dragon", UnitText.Owned(false, true, "dragon"));
            Assert.Equal("foreign dragon", UnitText.Owned(false, false, "dragon"));
        }

        [Fact]
        public void CargoListsOnlyWhatIsCarried()
        {
            Assert.Equal("wood 20, stone 5", UnitText.Cargo(new[] { "Wheat", "Tree", "Stone" }, new[] { 0, 20, 5 }));
            Assert.Null(UnitText.Cargo(new[] { "Wheat" }, new[] { 0 }));
        }

        [Fact]
        public void TowerRangeGrowsWithCastleLevels()
        {
            Assert.Equal("range 8 tiles, 1 of 4 castle levels high, 14 tiles at full height", UnitText.Range(6f, 2f, 1, 4));
            Assert.Equal("range 12 tiles, full height", UnitText.Range(6f, 1.5f, 4, 4));
            Assert.Equal("range 6 tiles, 12 on 4 castle levels", UnitText.Range(6f, 1.5f, -1, 4));
            Assert.Equal("range 10 tiles", UnitText.Range(10f, 0f, 2, 0));
            Assert.Equal("range 1 tile", UnitText.Range(1f, 0f, -1, 0));
        }

        [Fact]
        public void RouteStopDescribesPlaceAndCargo()
        {
            Assert.Equal("Stop 2 of 3: Northport, 4 tiles east, pick up wood 20, drop off nothing",
                UnitText.RouteStop(2, 3, "Northport", "4 tiles east", waypoint: false, sells: false, pickUp: "wood 20", dropOff: null));
            Assert.Equal("Stop 1 of 2: waypoint, passes by", UnitText.RouteStop(1, 2, null, null, waypoint: true, sells: false, pickUp: null, dropOff: null));
            Assert.Contains("sells the cargo here", UnitText.RouteStop(3, 3, "Dock", null, waypoint: false, sells: true, pickUp: null, dropOff: null));
        }
    }
}
