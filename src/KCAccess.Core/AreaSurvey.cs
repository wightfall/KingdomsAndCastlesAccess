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
            var parts = new List<string> { "Area " + side + " by " + side + " around the cursor" };
            if (Fog > 0) parts.Add(TextUtil.Plural(Fog, "unexplored tile"));
            parts.Add(TextUtil.Plural(Land, "land tile"));
            if (Land > 0) parts.Add(Open + " open to build");
            if (Fertile + VeryFertile > 0) parts.Add((Fertile + VeryFertile) + " fertile" + (VeryFertile > 0 ? ", " + VeryFertile + " of them very fertile" : string.Empty));
            else if (Land > 0) parts.Add("no fertile land");
            if (Forest > 0) parts.Add(Forest + " forest");
            if (Stone > 0) parts.Add(Stone + " stone");
            if (Iron > 0) parts.Add(Iron + " iron");
            if (Water > 0) parts.Add(Water + " shallow water");
            if (DeepWater > 0) parts.Add(DeepWater + " deep water");
            if (FishTiles > 0) parts.Add(TextUtil.Plural(FishTiles, "water tile") + " with fish");
            if (Buildings > 0) parts.Add(TextUtil.Plural(Buildings, "tile") + " with buildings");
            if (Stone == 0 && NearestStone != null) parts.Add("nearest stone " + NearestStone);
            if (Iron == 0 && NearestIron != null) parts.Add("nearest iron " + NearestIron);
            if (Water == 0 && DeepWater == 0 && NearestWater != null) parts.Add("nearest water " + NearestWater);
            return string.Join(". ", parts) + ".";
        }
    }
}
