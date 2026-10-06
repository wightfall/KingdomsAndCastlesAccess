using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>Spoken words for units, unit panel tabs, trade negotiation moods and ship / cart route stops.</summary>
    public static class UnitText
    {
        /// <summary>"your ", "enemy " or "foreign " for a team, from its relation to the player (team 0).</summary>
        public static string Owner(bool mine, bool enemy) => mine ? "your " : enemy ? "enemy " : "foreign ";

        /// <summary>
        /// The unit panel's tabs (UnitUI.UnitTabs) are pictures with a number: name them.
        /// Null for unknown tab names.
        /// </summary>
        public static string TabName(string tab)
        {
            switch (tab)
            {
                case "All": return "All selected units";
                case "Knights": return "Knights";
                case "Archers": return "Archers";
                case "Settlers": return "Settlers";
                case "Envoys": return "Envoys";
                case "Catapults": return "Siege catapults";
                case "TransportShips": return "Troop ships";
                case "Dragons": return "Dragons";
                case "Individual": return "Single unit";
                default: return null;
            }
        }

        /// <summary>
        /// How the other kingdom feels about a negotiated price (the face next to each slider in the diplomacy price
        /// editor, ResourceNegotiationEntryUI): 1 very happy, up to 2 happy, up to 5 neutral, up to 8 angry, more very angry.
        /// </summary>
        public static string NegotiationMood(float value)
        {
            if (value == 1f) return "very happy";
            if (value > 1f && value <= 2f) return "happy";
            if (value > 2f && value <= 5f) return "neutral";
            if (value > 5f && value <= 8f) return "angry";
            return "very angry";
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
            var parts = new List<string> { "Stop " + index + " of " + count + ": " + (string.IsNullOrEmpty(place) ? (waypoint ? "waypoint" : "building") : place) };
            if (!string.IsNullOrEmpty(where)) parts.Add(where);
            if (waypoint) parts.Add("passes by");
            else if (sells) parts.Add("sells the cargo here");
            else
            {
                parts.Add("pick up " + (pickUp ?? "nothing"));
                parts.Add("drop off " + (dropOff ?? "nothing"));
            }
            return string.Join(", ", parts.ToArray());
        }
    }
}
