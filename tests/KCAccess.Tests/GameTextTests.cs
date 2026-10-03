using KCAccess.Core;
using Xunit;

namespace KCAccess.Tests
{
    public class PlacementTextTests
    {
        // Every value of the game's PlacementValidationResult enum (Kingdoms and Castles 123r6s).
        public static readonly string[] GameValues =
        {
            "Valid", "OutsideOfTerritory", "ExistingStructure", "BridgeNotInWater", "PierNotInWater", "RoadNotOnLand", "RoadCoverage",
            "ForesterNotNearTrees", "QuarryMustBeAdjacentToStone", "QuarryMustBeOnTopOfStone", "MustBeOnFlatLand", "MustBeOnFertileLand",
            "OnlyStackOnCastleBlocks", "IronMineMustBeAdjacentToIron", "IronMineMustBeOnTopOfIron", "ForesterTooCloseToForester",
            "MustBeUpAgainstRock", "TooManyQuarries", "TooManyIronMines", "CastleBlockNoSupport", "QuarryMustFaceRock", "IronMineMustFaceRock",
            "CastleBlocksNotLikeType", "NoriaBaseOnLand", "NoriaWheelOnWater", "WoodCastleWallLimit", "NoriaTooCloseToExisting",
            "NoriaWheelOnFreshWater", "MustBeOnShallowWater", "WellOnGround", "BadStartingArea", "DockNearWater", "ShipNotInWater",
            "MustBeOnWaterOrClearLand", "MustBeOnCorrectLandMass", "BuildAnOutpostOnUnclaimedLand", "OneOutpostPerLandmass",
            "OutpostTooFarFromSeedShip", "CannotBuildOnWater", "TooCloseToWolves", "CannotBuildOnOthersIsland", "NotNearFish",
            "SeaGateNotOnWater", "OnPlannedAIIsland"
        };

        [Fact]
        public void EveryGameResultHasAnExplanation()
        {
            foreach (var v in GameValues) Assert.True(PlacementText.Has(v), v + " has no explanation");
        }

        [Fact]
        public void UnknownResultIsHumanized()
        {
            Assert.Equal("some new reason", PlacementText.Explain("SomeNewReason"));
            Assert.Equal("unknown", PlacementText.Explain(null));
        }

        [Fact]
        public void ExplanationsAreLowerCaseSentences()
        {
            foreach (var v in GameValues)
            {
                string text = PlacementText.Explain(v);
                Assert.False(string.IsNullOrWhiteSpace(text));
                Assert.Equal(char.ToLowerInvariant(text[0]), text[0]);
            }
        }
    }

    public class ResourceNamesTests
    {
        [Theory]
        [InlineData("Tree", "wood")]
        [InlineData("IronOre", "iron")]
        [InlineData("Armament", "armaments")]
        [InlineData("Gold", "gold")]
        [InlineData("SomethingNew", "something new")]
        public void NamesResources(string enumName, string expected)
        {
            Assert.Equal(expected, ResourceNames.Name(enumName));
        }

        [Fact]
        public void CostListsOnlyNeededResources()
        {
            var names = new[] { "Tree", "Stone", "Gold" };
            Assert.Equal("wood 10, gold 5", ResourceNames.Cost(names, new[] { 10, 0, 5 }, null));
        }

        [Fact]
        public void CostMarksMissingAmounts()
        {
            var names = new[] { "Tree", "Stone" };
            Assert.Equal("wood 10 (have 3), stone 2", ResourceNames.Cost(names, new[] { 10, 2 }, new[] { 3, 9 }));
        }

        [Fact]
        public void EmptyCostIsFree()
        {
            Assert.Equal("free", ResourceNames.Cost(new[] { "Tree" }, new[] { 0 }, null));
        }
    }

    public class HelpTextTests
    {
        [Theory]
        [InlineData("Menu")]
        [InlineData("ChooseMode")]
        [InlineData("ChooseDifficulty")]
        [InlineData("NameAndBanner")]
        [InlineData("NewMap")]
        [InlineData("PauseMenu")]
        [InlineData("SettingsMenu")]
        [InlineData("Save")]
        [InlineData("Load")]
        [InlineData("QuitConfirm")]
        [InlineData("Map")]
        [InlineData("BuildMenu")]
        [InlineData("Placement")]
        [InlineData("Decrees")]
        [InlineData("Log")]
        [InlineData("Status")]
        [InlineData("Panel")]
        [InlineData("Confirm")]
        public void KnownScreensHaveHelp(string id)
        {
            Assert.True(HelpText.Has(id));
            Assert.True(HelpText.For(id).Length > 40);
        }

        [Fact]
        public void UnknownScreenFallsBackToDialogHelp()
        {
            Assert.Equal(HelpText.For("Dialog"), HelpText.For("NoSuchScreen"));
            Assert.Equal(HelpText.For("Dialog"), HelpText.For(null));
        }

        [Fact]
        public void EveryMainMenuStateHasHelp()
        {
            // Values of MainMenuMode.State that the mod reports as screen ids.
            string[] states =
            {
                "Menu", "ChooseMode", "ChooseDifficulty", "NewMap", "NameAndBanner", "PauseMenu", "SettingsMenu", "Save", "Load",
                "QuitConfirm", "ExitConfirm", "LoadError", "Credits", "Failure", "KeepDestroyed", "BannerSelect", "GameWorkshopUI",
                "RivalChoiceUI", "KingdomShare"
            };
            foreach (var s in states) Assert.True(HelpText.Has(s), s + " has no help");
        }

        [Fact]
        public void AllKeysMentionsEveryArea()
        {
            string all = HelpText.AllKeys();
            Assert.Contains("F1", all);
            Assert.Contains("build menu", all, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Shift Enter", all);
        }

        [Fact]
        public void MapHelpMentionsCoreKeys()
        {
            string map = HelpText.For("Map");
            foreach (var key in new[] { "Arrow", "Enter", " B ", "F6", "Page Up", "bracket", "Home" }) Assert.Contains(key, map);
        }
    }

    public class NotificationLogTests
    {
        [Fact]
        public void NewestFirstAndCapped()
        {
            var log = new NotificationLog(capacity: 2);
            log.Add("one", Severity.Info, 1);
            log.Add("two", Severity.Info, 1);
            log.Add("<b>three</b>", Severity.Warning, 2, new GridPos(4, 5));
            Assert.Equal(2, log.Count);
            Assert.Equal("three", log.Items[0].Text);
            Assert.Equal(new GridPos(4, 5), log.Items[0].Where.Value);
            Assert.Equal("two", log.Items[1].Text);
        }

        [Fact]
        public void DescribeIncludesSeverityAndYear()
        {
            var n = new Notification { Text = "Fire", Severity = Severity.Danger, Year = 3 };
            Assert.Equal("Danger: Fire, year 3", n.Describe());
            Assert.Equal("Hello", new Notification { Text = "Hello" }.Describe());
        }

        [Theory]
        [InlineData("FireWarning", false, Severity.Danger)]
        [InlineData("plague", false, Severity.Danger)]
        [InlineData("foodshortage", true, Severity.Warning)]
        [InlineData("year", false, Severity.Info)]
        [InlineData(null, false, Severity.Info)]
        public void ClassifiesGameIds(string id, bool warning, Severity expected)
        {
            Assert.Equal(expected, NotificationLog.Classify(id, warning, false));
        }

        [Fact]
        public void ClearEmpties()
        {
            var log = new NotificationLog();
            log.Add("x", Severity.Info, 0);
            log.Clear();
            Assert.Equal(0, log.Count);
        }
    }
}
