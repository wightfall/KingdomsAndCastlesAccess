using System.Collections.Generic;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// Reads the game's thought bubbles (hungry, starving, plague, unpaid wages, homeless, rats, unburied dead,
    /// buildings that cannot work). Used by the Problems scanner category, tile descriptions, the K status line and
    /// announcements of critical problems.
    /// </summary>
    internal static class Problems
    {
        internal struct Problem
        {
            public string Thought;
            public GridPos Pos;
            public string Label;
        }

        private static readonly HashSet<ThoughtBubbleSystem.ThoughtBubble> announced = new HashSet<ThoughtBubbleSystem.ThoughtBubble>();
        private static float nextCheck;

        internal static List<Problem> All()
        {
            var list = new List<Problem>();
            var bubbles = ThoughtBubbleSystem.thoughts;
            if (bubbles == null) return list;
            for (int i = 0; i < bubbles.Count; i++)
            {
                var b = bubbles.data[i];
                if (b == null || !b.visible) continue;
                string t = b.thought.ToString();
                string meaning = Thoughts.Meaning(t);
                if (meaning == null) continue;
                var pos = new GridPos(Mathf.FloorToInt(b.pos.x), Mathf.FloorToInt(b.pos.z));
                list.Add(new Problem { Thought = t, Pos = pos, Label = Owner(pos, t) + ": " + meaning });
            }
            return list;
        }

        /// <summary>What the bubble is over: the building on the tile, or villagers.</summary>
        private static string Owner(GridPos p, string thought)
        {
            var c = World.inst.GetCellData(p.X, p.Z);
            Building b = c != null && c.OccupyingStructure.Count > 0 ? c.OccupyingStructure[c.OccupyingStructure.Count - 1] : null;
            // Hunger, plague and homelessness are shown over villagers (a house shows hunger for its own pantry).
            bool villagerThought = thought == "House" || thought == "FoodCritical" || thought == "Plague" || thought == "PlagueCritical"
                || (thought == "Food" && (b == null || b.CategoryName != "house"));
            if (villagerThought) return b != null ? "villager at " + b.FriendlyName : "villager";
            return b != null ? b.FriendlyName : "building";
        }

        /// <summary>Problems on one tile, for the tile description.</summary>
        internal static string At(int x, int z)
        {
            var counts = new Dictionary<string, int>();
            foreach (var p in All())
                if (p.Pos.X == x && p.Pos.Z == z) counts[p.Thought] = counts.TryGetValue(p.Thought, out int n) ? n + 1 : 1;
            if (counts.Count == 0) return null;
            if (counts.Count == 1)
                foreach (var kv in counts) if (kv.Value == 1) return "problem: " + Thoughts.Meaning(kv.Key);
            return "problems: " + Thoughts.Summary(counts);
        }

        /// <summary>Kingdom-wide summary for K.</summary>
        internal static string SummaryLine()
        {
            var counts = new Dictionary<string, int>();
            foreach (var p in All()) counts[p.Thought] = counts.TryGetValue(p.Thought, out int n) ? n + 1 : 1;
            string s = Thoughts.Summary(counts);
            return s.Length == 0 ? "No problems shown" : "Problems: " + s + KeyHelp.Resolve(". {NextCategory} to the Problems category finds them");
        }

        /// <summary>Announce critical problems once when they appear.</summary>
        internal static void Tick()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 2f;
            var bubbles = ThoughtBubbleSystem.thoughts;
            if (bubbles == null) return;
            var live = new HashSet<ThoughtBubbleSystem.ThoughtBubble>();
            var fresh = new Dictionary<string, int>();
            GridPos? first = null;
            for (int i = 0; i < bubbles.Count; i++)
            {
                var b = bubbles.data[i];
                if (b == null || !b.visible) continue;
                string t = b.thought.ToString();
                if (!Thoughts.IsCritical(t)) continue;
                live.Add(b);
                if (!announced.Add(b)) continue;
                fresh[t] = fresh.TryGetValue(t, out int n) ? n + 1 : 1;
                if (!first.HasValue) first = new GridPos(Mathf.FloorToInt(b.pos.x), Mathf.FloorToInt(b.pos.z));
            }
            announced.RemoveWhere(b => !live.Contains(b));
            if (fresh.Count == 0 || !Plugin.CfgAnnounceLog.Value) return;
            A.Cue(Cue.Alert);
            A.SayQueued("Problem: " + Thoughts.Summary(fresh) + (first.HasValue ? ", " + Directions.Relative(MapController.Inst.CursorPos, first.Value) : string.Empty));
        }
    }
}
