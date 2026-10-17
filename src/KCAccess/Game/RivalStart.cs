using System.Collections.Generic;
using KCAccess.Core;
using KCAccess.UI;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// On the rival kingdoms screen the game lets you drag an AI kingdom's banner onto an island to choose where it
    /// starts; otherwise it picks one. The keyboard way: I and Shift I on the kingdom's row step through "random" and
    /// every island an AI kingdom can start on that no other AI kingdom has.
    /// </summary>
    internal static class RivalStart
    {
        /// <summary>"Stot starts on an island of 1200 tiles in the north west of the map, around 30, 80".</summary>
        internal static string Text(RivalItemUI rival)
        {
            string name = rival.rivalName != null ? TextUtil.Clean(UIText.TextOf(rival.rivalName)) : string.Empty;
            int lm = rival.flag != null ? rival.flag.landmass : -1;
            var w = World.inst;
            if (lm < 0 || w == null || w.cellsToLandmass == null || lm >= w.cellsToLandmass.Length) return Loc.F("{0} starts on a random island", name);
            var middle = Middle(lm);
            int size = w.cellsToLandmass[lm].Count;
            if (middle == null) return Loc.F("{0} starts on an island of {1} tiles", name, size);
            var at = new GridPos(middle.x, middle.z);
            int dx = middle.x - w.GridWidth / 2, dz = middle.z - w.GridHeight / 2;
            if (Mathf.Abs(dx) <= w.GridWidth / 6 && Mathf.Abs(dz) <= w.GridHeight / 6)
                return Loc.F("{0} starts on an island of {1} tiles in the middle of the map, around {2}", name, size, at);
            return Loc.F("{0} starts on an island of {1} tiles in the {2} of the map, around {3}", name, size, Directions.Compass8(dx, dz), at);
        }

        /// <summary>Moves the kingdom's start to the next (or previous) choice and says it.</summary>
        internal static void Cycle(RivalItemUI rival, int dir)
        {
            var s = RivalKingdomSettingsUI.inst;
            var w = World.inst;
            if (s == null || w == null || rival == null || rival.flag == null) return;
            var options = Options(s, rival);
            int cur = Mathf.Max(0, options.IndexOf(rival.flag.landmass));
            int next = (cur + dir + options.Count) % options.Count;
            int target = options[next];
            if (target < 0) rival.flag.ClearFlag();
            else
            {
                // As dropping the dragged banner on the island does.
                rival.flag.gameObject.SetActive(true);
                if (rival.bannerIdx >= 0 && rival.bannerIdx < w.liverySets.Count) rival.flag.SetFlagMaterial(w.liverySets[rival.bannerIdx].bannerMaterial);
                var middle = Middle(target);
                if (middle != null) rival.flag.StopHolding(middle.Center);
                if (rival.flag.landmass < 0) rival.flag.ClearFlag();
            }
            s.UpdateLandColors();
            A.Cue(options.Count > 1 ? Cue.Value : Cue.Edge);
            A.Say(Text(rival) + ", " + Loc.F("{0} of {1}", next + 1, options.Count), force: true);
        }

        /// <summary>Random first, then the islands an AI kingdom can start on and no other one has.</summary>
        private static List<int> Options(RivalKingdomSettingsUI s, RivalItemUI rival)
        {
            var w = World.inst;
            var options = new List<int> { -1 };
            for (int lm = 0; lm < w.NumLandMasses; lm++)
            {
                if (!w.IsValidStartLandmassForAI(lm)) continue;
                bool taken = false;
                if (s.rivalItems != null)
                    foreach (var other in s.rivalItems)
                        if (other != null && other != rival && other.Enabled && other.flag != null && other.flag.landmass == lm) taken = true;
                if (!taken) options.Add(lm);
            }
            return options;
        }

        /// <summary>The island's tile nearest the middle of all its tiles (the middle itself may be a bay).</summary>
        private static Cell Middle(int lm)
        {
            var cells = World.inst.cellsToLandmass[lm];
            if (cells == null || cells.Count == 0) return null;
            float sx = 0f, sz = 0f;
            for (int i = 0; i < cells.Count; i++)
            {
                sx += cells.data[i].x;
                sz += cells.data[i].z;
            }
            sx /= cells.Count;
            sz /= cells.Count;
            Cell best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells.data[i];
                float d = (c.x - sx) * (c.x - sx) + (c.z - sz) * (c.z - sz);
                if (d < bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
            return best;
        }
    }
}
