using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>
    /// The water system: a water wheel pumps river water through aqueduct pieces into reservoirs, bathhouses and dragon
    /// baths; buildings of it work only when they touch one another in a chain from a staffed water wheel (side by side,
    /// not corner to corner), and fountains only on land a reservoir with water irrigates. The game shows the pipes and
    /// the water in them only; these texts say it.
    /// </summary>
    public static class WaterText
    {
        public enum Link
        {
            /// <summary>A built water building with water in it.</summary>
            Water,
            /// <summary>A built water building without water (not connected, or the water is too weak to reach it).</summary>
            Dry,
            /// <summary>Still a construction site: not part of the system until finished.</summary>
            Unbuilt
        }

        public struct Neighbour
        {
            public string Name;
            /// <summary>Compass direction from the building ("north").</summary>
            public string Direction;
            public Link State;
        }

        /// <summary>
        /// Said on every tile while a water building is held: what it would join there. <paramref name="source"/> is a
        /// water wheel (the start of the chain); <paramref name="nearest"/> ("Aqueduct, 6 tiles north east, ...") is said
        /// when nothing is next to it.
        /// </summary>
        public static string Placing(IList<Neighbour> touching, bool source, string nearest)
        {
            if (touching == null || touching.Count == 0)
            {
                string s = source ? Loc.T("no pipes connected yet") : Loc.T("not connected to the water system");
                return TextUtil.HasContent(nearest) ? s + ", " + Loc.F("nearest water building: {0}", nearest) : s;
            }
            var parts = new List<string>();
            bool water = false;
            foreach (var n in touching)
            {
                string state = n.State == Link.Water ? Loc.T("has water") : n.State == Link.Dry ? Loc.T("no water") : Loc.T("still being built");
                parts.Add(Loc.F("{0} to the {1}, {2}", n.Name, n.Direction, state));
                if (n.State == Link.Water) water = true;
            }
            string text = Loc.F("connects to {0}", string.Join("; ", parts.ToArray()));
            if (!source) text += ". " + (water ? Loc.T("Water will reach it") : Loc.T("No water reaches it yet"));
            return text;
        }

        /// <summary>Said on every tile while a fountain is held.</summary>
        public static string FountainPlacing(bool irrigated) =>
            irrigated ? Loc.T("irrigated land, the fountain will work here") : Loc.T("not irrigated, the fountain needs land a reservoir with water irrigates");

        /// <summary>Said after a water building finished construction: whether it works.</summary>
        /// <param name="pumping">A water wheel of the island pumps (it has all its workers); without one a connected
        /// building is dry because of the wheel, not because the water is too weak.</param>
        public static string Built(string name, bool source, bool connected, bool hasWater, bool pumping = true)
        {
            if (source) return hasWater ? Loc.F("{0} pumps water.", name) : Loc.F("{0} pumps water once all its workers are there.", name);
            if (connected && hasWater) return Loc.F("{0} has water and works.", name);
            if (connected && !pumping) return Loc.F("{0} is connected, but no water wheel pumps yet: a water wheel needs all its workers.", name);
            if (connected) return Loc.F("{0} is connected, but the water is too weak to reach it. Connect another water wheel or shorten the pipes.", name);
            return Loc.F("{0} is not connected to a water wheel and has no water. Connect it with aqueduct pieces side by side.", name);
        }

        /// <summary>Said after several pieces of one kind (a line of aqueduct) finished together.</summary>
        public static string BuiltMany(string name, int count, int withWater)
        {
            if (withWater >= count) return Loc.F("{0}: all {1} pieces have water.", name, count);
            if (withWater <= 0) return Loc.F("{0}: none of the {1} pieces has water. Connect them to a water wheel.", name, count);
            return Loc.F("{0}: {1} of {2} pieces have water.", name, withWater, count);
        }

        /// <summary>Said after a fountain finished construction.</summary>
        public static string FountainBuilt(string name, bool irrigated) =>
            irrigated ? Loc.F("{0} is on irrigated land and works.", name) : Loc.F("{0} is not on irrigated land and does not work. Build a reservoir with water near it.", name);
    }
}
