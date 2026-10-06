using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>Friendly explanations for the game's PlacementValidationResult values (by enum name).</summary>
    public static class PlacementText
    {
        private static readonly Dictionary<string, string> Reasons = new Dictionary<string, string>
        {
            { "Valid", Loc.N("can build here") },
            { "OutsideOfTerritory", Loc.N("outside road coverage, build a road closer first") },
            { "ExistingStructure", Loc.N("something is already built here") },
            { "BridgeNotInWater", Loc.N("bridges must be over water") },
            { "PierNotInWater", Loc.N("piers must be on water") },
            { "RoadNotOnLand", Loc.N("roads need clear land without trees, rock or water") },
            { "RoadCoverage", Loc.N("too far from a road, build a road nearby first") },
            { "ForesterNotNearTrees", Loc.N("foresters need trees nearby") },
            { "QuarryMustBeAdjacentToStone", Loc.N("quarries must be next to a stone deposit") },
            { "QuarryMustBeOnTopOfStone", Loc.N("quarries must be on top of stone") },
            { "MustBeOnFlatLand", Loc.N("needs clear land with no trees, rock or water under it") },
            { "MustBeOnFertileLand", Loc.N("must be on fertile land") },
            { "OnlyStackOnCastleBlocks", Loc.N("can only be stacked on castle blocks") },
            { "IronMineMustBeAdjacentToIron", Loc.N("iron mines must be next to an iron deposit") },
            { "IronMineMustBeOnTopOfIron", Loc.N("iron mines must be on top of iron") },
            { "ForesterTooCloseToForester", Loc.N("too close to another forester") },
            { "MustBeUpAgainstRock", Loc.N("must be against rock") },
            { "TooManyQuarries", Loc.N("too many quarries on this deposit") },
            { "TooManyIronMines", Loc.N("too many iron mines on this deposit") },
            { "CastleBlockNoSupport", Loc.N("castle block has nothing to support it") },
            { "QuarryMustFaceRock", Loc.N("the quarry must face the stone, rotate it with R") },
            { "IronMineMustFaceRock", Loc.N("the iron mine must face the iron, rotate it with R") },
            { "CastleBlocksNotLikeType", Loc.N("cannot mix castle block types") },
            { "NoriaBaseOnLand", Loc.N("the noria base must be on land") },
            { "NoriaWheelOnWater", Loc.N("the noria wheel must be on water, rotate it with R") },
            { "WoodCastleWallLimit", Loc.N("wooden walls cannot be stacked higher") },
            { "NoriaTooCloseToExisting", Loc.N("too close to another noria") },
            { "NoriaWheelOnFreshWater", Loc.N("the noria wheel must be on fresh water") },
            { "MustBeOnShallowWater", Loc.N("must be on shallow water") },
            { "WellOnGround", Loc.N("wells must be on the ground") },
            { "BadStartingArea", Loc.N("poor starting area, not enough land") },
            { "DockNearWater", Loc.N("docks must be next to water") },
            { "ShipNotInWater", Loc.N("ships must be placed on water") },
            { "MustBeOnWaterOrClearLand", Loc.N("must be on water or clear land") },
            { "MustBeOnCorrectLandMass", Loc.N("must be on the selected island") },
            { "BuildAnOutpostOnUnclaimedLand", Loc.N("outposts must be on unclaimed land") },
            { "OneOutpostPerLandmass", Loc.N("only one outpost per island") },
            { "OutpostTooFarFromSeedShip", Loc.N("too far from your seed ship") },
            { "CannotBuildOnWater", Loc.N("cannot build on water") },
            { "TooCloseToWolves", Loc.N("too close to wolves") },
            { "CannotBuildOnOthersIsland", Loc.N("this island belongs to another kingdom") },
            { "NotNearFish", Loc.N("no fish nearby") },
            { "SeaGateNotOnWater", Loc.N("sea gates must be on water") },
            { "OnPlannedAIIsland", Loc.N("this island is reserved for an AI kingdom") },
        };

        public static string Explain(string resultName)
        {
            if (resultName != null && Reasons.TryGetValue(resultName, out var text)) return Loc.T(text);
            return TextUtil.Humanize(resultName ?? "unknown").ToLowerInvariant();
        }

        public static bool Has(string resultName) => resultName != null && Reasons.ContainsKey(resultName);
    }
}
