namespace KCAccess.Core
{
    /// <summary>Spoken names for the game's FreeResourceType values (by enum name).</summary>
    public static class ResourceNames
    {
        public static string Name(string enumName)
        {
            switch (enumName)
            {
                case "Tree": return "wood";
                case "IronOre": return "iron";
                case "Armament": return "armaments";
                case "Wheat": return "wheat";
                case "Apples": return "apples";
                case "Fish": return "fish";
                case "Pork": return "pork";
                case "Stone": return "stone";
                case "Gold": return "gold";
                case "Charcoal": return "charcoal";
                case "Tools": return "tools";
                case "DeadVillager": return "bodies";
                default: return TextUtil.Humanize(enumName ?? string.Empty).ToLowerInvariant();
            }
        }

        /// <summary>Formats a cost like "wood 10, stone 5"; missing amounts get "(have 3)".</summary>
        public static string Cost(string[] names, int[] need, int[] have)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                if (need[i] <= 0) continue;
                string p = Name(names[i]) + " " + need[i];
                if (have != null && have[i] < need[i]) p += " (have " + have[i] + ")";
                parts.Add(p);
            }
            return parts.Count == 0 ? "free" : string.Join(", ", parts.ToArray());
        }
    }
}
