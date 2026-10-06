using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>Tile counts around the cursor, summarised for the O key (helps choose where to settle and build).</summary>
    public sealed class AreaSurvey
    {
        public int Radius;
        public int Land, Open, Fertile, VeryFertile, Forest, Stone, Iron, Water, DeepWater, Buildings, Fog, FishTiles;

        /// <summary>Nearest resources outside the counted square, as already-phrased directions ("12 tiles east, ..."); may be null.</summary>
        public string NearestStone, NearestIron, NearestWater;

        public string Format()
        {
            int side = Radius * 2 + 1;
            var parts = new List<string> { Loc.F("Area {0} by {0} around the cursor", side) };
            if (Fog > 0) parts.Add(Loc.P(Fog, "{0} unexplored tile", "{0} unexplored tiles"));
            parts.Add(Loc.P(Land, "{0} land tile", "{0} land tiles"));
            if (Land > 0) parts.Add(Loc.F("{0} open to build", Open));
            if (Fertile + VeryFertile > 0) parts.Add(Loc.F("{0} fertile", Fertile + VeryFertile) + (VeryFertile > 0 ? ", " + Loc.F("{0} of them very fertile", VeryFertile) : string.Empty));
            else if (Land > 0) parts.Add(Loc.T("no fertile land"));
            if (Forest > 0) parts.Add(Loc.F("{0} forest", Forest));
            if (Stone > 0) parts.Add(Loc.F("{0} stone", Stone));
            if (Iron > 0) parts.Add(Loc.F("{0} iron", Iron));
            if (Water > 0) parts.Add(Loc.F("{0} shallow water", Water));
            if (DeepWater > 0) parts.Add(Loc.F("{0} deep water", DeepWater));
            if (FishTiles > 0) parts.Add(Loc.P(FishTiles, "{0} water tile with fish", "{0} water tiles with fish"));
            if (Buildings > 0) parts.Add(Loc.P(Buildings, "{0} tile with buildings", "{0} tiles with buildings"));
            if (Stone == 0 && NearestStone != null) parts.Add(Loc.F("nearest stone {0}", NearestStone));
            if (Iron == 0 && NearestIron != null) parts.Add(Loc.F("nearest iron {0}", NearestIron));
            if (Water == 0 && DeepWater == 0 && NearestWater != null) parts.Add(Loc.F("nearest water {0}", NearestWater));
            return string.Join(". ", parts) + ".";
        }
    }
}
