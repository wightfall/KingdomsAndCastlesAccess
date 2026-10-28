namespace KCAccess.Core
{
    /// <summary>
    /// What the game's six kingdom banners show (looked at in the game's own pictures, keyed by their texture names):
    /// two are blue with the same crowned lion, told apart only by the second one's orange band.
    /// </summary>
    public static class BannerLooks
    {
        /// <summary>The banner's colour and emblem, or <paramref name="fallbackColour"/> for a picture not known here.</summary>
        public static string Describe(string textureName, string fallbackColour)
        {
            switch ((textureName ?? string.Empty).ToLowerInvariant())
            {
                case "banner_pete_sprite": return Loc.T("blue, a white crowned lion's head");
                case "banner_vladir_sprite": return Loc.T("yellow, a black sword with two dragons");
                case "banner_daniel": return Loc.T("orange, a pale eagle with spread wings");
                case "banner_bret": return Loc.T("green, a white twenty-sided die");
                case "banner_michael": return Loc.T("purple, a white snake coiled around a sword");
                case "banner_original_sprite": return Loc.T("blue with an orange diagonal band, a white crowned lion's head");
                default: return fallbackColour;
            }
        }
    }
}
