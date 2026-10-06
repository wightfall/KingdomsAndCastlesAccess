using System;
using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>Spoken words for units, unit panel tabs, trade negotiation moods and ship / cart route stops.</summary>
    public static class UnitText
    {
        /// <summary>"your dragon", "enemy dragon" or "foreign dragon", from the team's relation to the player (team 0).</summary>
        public static string Owned(bool mine, bool enemy, string what) =>
            mine ? Loc.F("your {0}", what) : enemy ? Loc.F("enemy {0}", what) : Loc.F("foreign {0}", what);

        /// <summary>
        /// The unit panel's tabs (UnitUI.UnitTabs) are pictures with a number: name them.
        /// Null for unknown tab names.
        /// </summary>
        public static string TabName(string tab)
        {
            switch (tab)
            {
                case "All": return Loc.T("All selected units");
                case "Knights": return Loc.T("Knights");
                case "Archers": return Loc.T("Archers");
                case "Settlers": return Loc.T("Settlers");
                case "Envoys": return Loc.T("Envoys");
                case "Catapults": return Loc.T("Siege catapults");
                case "TransportShips": return Loc.T("Troop ships");
                case "Dragons": return Loc.T("Dragons");
                case "Individual": return Loc.T("Single unit");
                default: return null;
            }
        }

        /// <summary>
        /// How the other kingdom feels about a negotiated price (the face next to each slider in the diplomacy price
        /// editor, ResourceNegotiationEntryUI): 1 very happy, up to 2 happy, up to 5 neutral, up to 8 angry, more very angry.
        /// </summary>
        public static string NegotiationMood(float value)
        {
            if (value == 1f) return Loc.T("very happy");
            if (value > 1f && value <= 2f) return Loc.T("happy");
            if (value > 2f && value <= 5f) return Loc.T("neutral");
            if (value > 5f && value <= 8f) return Loc.T("angry");
            return Loc.T("very angry");
        }

        /// <summary>
        /// Shooting range of a tower (IMaxRangeDisplay: archer tower, ballista, keep), shown in the game only as rings on
        /// the ground. The range grows with every castle level the tower stands on. <paramref name="height"/> -1 = not
        /// built yet (build menu): only the range on the ground and at full height.
        /// </summary>
        public static string Range(float baseRange, float perLevel, int height, int maxLevels)
        {
            bool grows = perLevel > 0f && maxLevels > 0;
            int full = (int)Math.Round(baseRange + maxLevels * perLevel);
            if (height < 0)
                return Loc.P((int)Math.Round(baseRange), "range {0} tile", "range {0} tiles") + (grows ? ", " + Loc.F("{0} on {1} castle levels", full, maxLevels) : string.Empty);
            int h = Math.Min(height, Math.Max(0, maxLevels));
            string s = Loc.P((int)Math.Round(baseRange + (grows ? h * perLevel : 0f)), "range {0} tile", "range {0} tiles");
            if (!grows) return s;
            return s + ", " + (h >= maxLevels ? Loc.T("full height") : Loc.F("{0} of {1} castle levels high, {2} tiles at full height", h, maxLevels, full));
        }

        /// <summary>"wood 20, stone 5" for the non-zero amounts, or null when everything is zero.</summary>
        public static string Cargo(IList<string> names, IList<int> amounts)
        {
            var parts = new List<string>();
            for (int i = 0; i < names.Count && i < amounts.Count; i++)
            {
                if (amounts[i] <= 0) continue;
                parts.Add(ResourceNames.Name(names[i]) + " " + amounts[i]);
            }
            return parts.Count == 0 ? null : string.Join(", ", parts.ToArray());
        }

        /// <summary>
        /// One stop of a ship or cart route: "Stop 2 of 3: Northport dock, 4 tiles east, pick up wood 20, drop off nothing".
        /// <paramref name="waypoint"/> is a stop on open water or ground (no building), <paramref name="sells"/> a merchant sale.
        /// </summary>
        public static string RouteStop(int index, int count, string place, string where, bool waypoint, bool sells, string pickUp, string dropOff)
        {
            var parts = new List<string> { Loc.F("Stop {0} of {1}: {2}", index, count, string.IsNullOrEmpty(place) ? (waypoint ? Loc.T("waypoint") : Loc.T("building")) : place) };
            if (!string.IsNullOrEmpty(where)) parts.Add(where);
            if (waypoint) parts.Add(Loc.T("passes by"));
            else if (sells) parts.Add(Loc.T("sells the cargo here"));
            else
            {
                parts.Add(Loc.F("pick up {0}", pickUp ?? Loc.T("nothing")));
                parts.Add(Loc.F("drop off {0}", dropOff ?? Loc.T("nothing")));
            }
            return string.Join(", ", parts.ToArray());
        }
    }
}
