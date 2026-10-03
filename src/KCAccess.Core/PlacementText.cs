using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>Friendly explanations for the game's PlacementValidationResult values (by enum name).</summary>
    public static class PlacementText
    {
        private static readonly Dictionary<string, string> Reasons = new Dictionary<string, string>
        {
            { "Valid", "can build here" },
            { "OutsideOfTerritory", "outside your territory, buildings must be within road coverage, so build a road closer first" },
            { "ExistingStructure", "something is already built here" },
            { "BridgeNotInWater", "bridges must be over water" },
            { "PierNotInWater", "piers must be on water" },
            { "RoadNotOnLand", "roads need clear land without trees, rock or water" },
            { "RoadCoverage", "too far from a road, build a road nearby first" },
            { "ForesterNotNearTrees", "foresters need trees nearby" },
            { "QuarryMustBeAdjacentToStone", "quarries must be next to a stone deposit" },
            { "QuarryMustBeOnTopOfStone", "quarries must be on top of stone" },
            { "MustBeOnFlatLand", "needs clear land with no trees, rock or water under it" },
            { "MustBeOnFertileLand", "must be on fertile land" },
            { "OnlyStackOnCastleBlocks", "can only be stacked on castle blocks" },
            { "IronMineMustBeAdjacentToIron", "iron mines must be next to an iron deposit" },
            { "IronMineMustBeOnTopOfIron", "iron mines must be on top of iron" },
            { "ForesterTooCloseToForester", "too close to another forester" },
            { "MustBeUpAgainstRock", "must be against rock" },
            { "TooManyQuarries", "too many quarries on this deposit" },
            { "TooManyIronMines", "too many iron mines on this deposit" },
            { "CastleBlockNoSupport", "castle block has nothing to support it" },
            { "QuarryMustFaceRock", "the quarry must face the stone, rotate it with R" },
            { "IronMineMustFaceRock", "the iron mine must face the iron, rotate it with R" },
            { "CastleBlocksNotLikeType", "cannot mix castle block types" },
            { "NoriaBaseOnLand", "the noria base must be on land" },
            { "NoriaWheelOnWater", "the noria wheel must be on water, rotate it with R" },
            { "WoodCastleWallLimit", "wooden walls cannot be stacked higher" },
            { "NoriaTooCloseToExisting", "too close to another noria" },
            { "NoriaWheelOnFreshWater", "the noria wheel must be on fresh water" },
            { "MustBeOnShallowWater", "must be on shallow water" },
            { "WellOnGround", "wells must be on the ground" },
            { "BadStartingArea", "poor starting area, not enough land" },
            { "DockNearWater", "docks must be next to water" },
            { "ShipNotInWater", "ships must be placed on water" },
            { "MustBeOnWaterOrClearLand", "must be on water or clear land" },
            { "MustBeOnCorrectLandMass", "must be on the selected island" },
            { "BuildAnOutpostOnUnclaimedLand", "outposts must be on unclaimed land" },
            { "OneOutpostPerLandmass", "only one outpost per island" },
            { "OutpostTooFarFromSeedShip", "too far from your seed ship" },
            { "CannotBuildOnWater", "cannot build on water" },
            { "TooCloseToWolves", "too close to wolves" },
            { "CannotBuildOnOthersIsland", "this island belongs to another kingdom" },
            { "NotNearFish", "no fish nearby" },
            { "SeaGateNotOnWater", "sea gates must be on water" },
            { "OnPlannedAIIsland", "this island is reserved for an AI kingdom" },
        };

        public static string Explain(string resultName)
        {
            if (resultName != null && Reasons.TryGetValue(resultName, out var text)) return text;
            return TextUtil.Humanize(resultName ?? "unknown").ToLowerInvariant();
        }

        public static bool Has(string resultName) => resultName != null && Reasons.ContainsKey(resultName);
    }
}
