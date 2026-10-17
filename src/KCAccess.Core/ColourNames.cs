using System;

namespace KCAccess.Core
{
    /// <summary>
    /// A plain colour word for a colour the game shows only as a coloured square (banner colour swatches, kingdom
    /// colours): the nearest of twelve basic names, by hue, saturation and brightness.
    /// </summary>
    public static class ColourNames
    {
        /// <summary>Components 0 to 1.</summary>
        public static string Name(float r, float g, float b)
        {
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            float value = max, chroma = max - min;
            float saturation = max > 0f ? chroma / max : 0f;
            if (value < 0.15f) return Loc.T("black");
            if (saturation < 0.18f) return value > 0.85f ? Loc.T("white") : Loc.T("grey");
            float hue;
            if (max == r) hue = 60f * (((g - b) / chroma) % 6f);
            else if (max == g) hue = 60f * ((b - r) / chroma + 2f);
            else hue = 60f * ((r - g) / chroma + 4f);
            if (hue < 0f) hue += 360f;
            if (hue < 15f || hue >= 345f) return value < 0.45f ? Loc.T("brown") : Loc.T("red");
            if (hue < 45f) return value < 0.65f ? Loc.T("brown") : Loc.T("orange");
            if (hue < 70f) return value < 0.5f ? Loc.T("brown") : Loc.T("yellow");
            if (hue < 165f) return Loc.T("green");
            if (hue < 195f) return Loc.T("teal");
            if (hue < 255f) return Loc.T("blue");
            if (hue < 290f) return Loc.T("purple");
            return Loc.T("pink");
        }
    }
}
