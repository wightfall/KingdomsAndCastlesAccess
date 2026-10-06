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
            Assert.Equal("your ", UnitText.Owner(true, false));
            Assert.Equal("enemy ", UnitText.Owner(false, true));
            Assert.Equal("foreign ", UnitText.Owner(false, false));
        }

        [Fact]
        public void CargoListsOnlyWhatIsCarried()
        {
            Assert.Equal("wood 20, stone 5", UnitText.Cargo(new[] { "Wheat", "Tree", "Stone" }, new[] { 0, 20, 5 }));
            Assert.Null(UnitText.Cargo(new[] { "Wheat" }, new[] { 0 }));
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
