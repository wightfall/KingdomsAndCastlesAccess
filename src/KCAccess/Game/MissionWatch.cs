using System.Collections.Generic;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// Requests of AI kingdoms (resources for a building, clearing wolves, fighting vikings) end without a message:
    /// fulfilled when you do it, failed when the time runs out at a new year. The game shows the result only in that
    /// kingdom's panel. Say both; visiting them afterwards raises or lowers their opinion of you.
    /// </summary>
    internal static class MissionWatch
    {
        private static readonly Dictionary<AIKingdom, KeyValuePair<Mission, Mission.Status>> last = new Dictionary<AIKingdom, KeyValuePair<Mission, Mission.Status>>();
        private static float nextCheck;

        internal static void Reset()
        {
            last.Clear();
            nextCheck = 0f;
        }

        internal static void Tick()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 2f;
            var brains = AIBrainsContainer.inst;
            if (brains == null || brains.kingdoms == null || Player.inst == null) return;
            try
            {
                foreach (var k in brains.kingdoms)
                {
                    if (k == null) continue;
                    var m = k.currMission;
                    if (m == null)
                    {
                        last.Remove(k);
                        continue;
                    }
                    bool known = last.TryGetValue(k, out var prev);
                    last[k] = new KeyValuePair<Mission, Mission.Status>(m, m.status);
                    // A request accepted in a conversation, or one loaded with the world, is no news.
                    if (!known || prev.Key != m || prev.Value != Mission.Status.InProgress || m.status == prev.Value) continue;
                    string text = null;
                    if (m.status == Mission.Status.Complete) text = Loc.F("You fulfilled the request of {0}. Visit them to tell them: their opinion of you rises.", k.GetLocalizedName());
                    else if (m.status == Mission.Status.Failed) text = Loc.F("You failed the request of {0}. Their opinion of you drops when you next speak with them.", k.GetLocalizedName());
                    if (text == null) continue;
                    var severity = m.status == Mission.Status.Complete ? Severity.Info : Severity.Warning;
                    GameEvents.Log.Add(text, severity, Player.inst.CurrYear);
                    if (!Plugin.CfgAnnounceLog.Value) continue;
                    A.Cue(Cue.Notify);
                    A.SayQueued(text);
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Request check failed: " + e.Message);
            }
        }
    }
}
