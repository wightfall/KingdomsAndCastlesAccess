using System.Collections.Generic;
using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class ThoughtsTests
    {
        [Fact]
        public void SatisfiedIsNotAProblem() => Assert.Null(Thoughts.Meaning("Satisfied"));

        [Theory]
        [InlineData("FoodCritical", "starving")]
        [InlineData("Plague", "sick with plague")]
        [InlineData("Wage", "wages not paid")]
        [InlineData("House", "homeless")]
        public void MeaningsAreSpoken(string thought, string meaning) => Assert.Equal(meaning, Thoughts.Meaning(thought));

        [Fact]
        public void SummaryListsMostUrgentFirst()
        {
            var counts = new Dictionary<string, int> { { "Food", 4 }, { "FoodCritical", 1 }, { "Satisfied", 9 } };
            Assert.Equal("1 starving, 4 hungry", Thoughts.Summary(counts));
        }

        [Fact]
        public void CriticalOnlyForUrgentOnes()
        {
            Assert.True(Thoughts.IsCritical("FoodCritical"));
            Assert.True(Thoughts.IsCritical("DeadBodyWarning"));
            Assert.False(Thoughts.IsCritical("Food"));
            Assert.False(Thoughts.IsCritical("Satisfied"));
        }

        [Fact]
        public void SurveyMentionsFish()
        {
            var s = new AreaSurvey { Radius = 1, Water = 9, FishTiles = 2 };
            Assert.Contains("2 water tiles with fish", s.Format());
        }
    }
}
