namespace KCAccess.Core
{
    /// <summary>Spoken names for the game's FreeResourceType values (by enum name).</summary>
    public static class ResourceNames
    {
        public static string Name(string enumName)
        {
            switch (enumName)
            {
                case "Tree": return Loc.T("wood");
                case "IronOre": return Loc.T("iron");
                case "Armament": return Loc.T("armaments");
                case "Wheat": return Loc.T("wheat");
                case "Apples": return Loc.T("apples");
                case "Fish": return Loc.T("fish");
                case "Pork": return Loc.T("pork");
                case "Stone": return Loc.T("stone");
                case "Gold": return Loc.T("gold");
                case "Charcoal": return Loc.T("charcoal");
                case "Tools": return Loc.T("tools");
                case "DeadVillager": return Loc.T("bodies");
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
                if (have != null && have[i] < need[i]) p += " " + Loc.F("(have {0})", have[i]);
                parts.Add(p);
            }
            return parts.Count == 0 ? Loc.T("free") : string.Join(", ", parts.ToArray());
        }
    }
}
