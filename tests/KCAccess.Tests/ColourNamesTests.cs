using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class ColourNamesTests
    {
        [Theory]
        [InlineData(0.85f, 0.1f, 0.1f, "red")]
        [InlineData(0.95f, 0.55f, 0.1f, "orange")]
        [InlineData(0.95f, 0.9f, 0.2f, "yellow")]
        [InlineData(0.2f, 0.7f, 0.25f, "green")]
        [InlineData(0.1f, 0.65f, 0.65f, "teal")]
        [InlineData(0.15f, 0.3f, 0.85f, "blue")]
        [InlineData(0.5f, 0.2f, 0.8f, "purple")]
        [InlineData(0.9f, 0.4f, 0.7f, "pink")]
        [InlineData(0.45f, 0.28f, 0.12f, "brown")]
        [InlineData(0.95f, 0.95f, 0.93f, "white")]
        [InlineData(0.5f, 0.5f, 0.52f, "grey")]
        [InlineData(0.05f, 0.05f, 0.08f, "black")]
        public void NamesTheNearestBasicColour(float r, float g, float b, string expected)
        {
            Loc.Use(null);
            Assert.Equal(expected, ColourNames.Name(r, g, b));
        }
    }
}
