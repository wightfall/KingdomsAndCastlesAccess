using System.Collections.Generic;
using Assets.Code;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// The game logs a fire only once it has burnt a building down ("Fire has claimed a Hovel"); the flames and smoke
    /// before that are drawn only. Say when a fire breaks out in your kingdom (not every tile it spreads to) and when
    /// the last fire is out.
    /// </summary>
    internal static class FireWatch
    {
        private static readonly List<Vector3> burning = new List<Vector3>();
        private static float nextCheck;

        internal static void Reset()
        {
            burning.Clear();
            nextCheck = 0f;
        }

        internal static void Tick()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 1f;
            var fm = FireManager.inst;
            if (fm == null || fm.fireContainer == null || Player.inst == null || World.inst == null) return;
            var now = new List<Vector3>();
            foreach (var f in fm.fireContainer.GetComponentsInChildren<Fire>())
            {
                if (f == null) continue;
                var c = World.inst.GetCellDataClamped(f.transform.position);
                if (c != null && Player.inst.LandMassIsAPlayerLandMass(c.landMassIdx)) now.Add(f.transform.position);
            }
            // A fire spreading to the next tiles is the same fire: only one farther than 3 tiles from every fire burning
            // a second ago is a new outbreak.
            var outbreaks = new List<Vector3>();
            foreach (var p in now)
            {
                bool near = false;
                foreach (var q in burning) if (Mathf.Abs(p.x - q.x) <= 3f && Mathf.Abs(p.z - q.z) <= 3f) { near = true; break; }
                foreach (var q in outbreaks) if (Mathf.Abs(p.x - q.x) <= 3f && Mathf.Abs(p.z - q.z) <= 3f) { near = true; break; }
                if (!near) outbreaks.Add(p);
            }
            if (Plugin.CfgAnnounceLog.Value && GameState.inst != null && GameState.inst.IsPlayMode())
            {
                foreach (var p in outbreaks)
                {
                    var c = World.inst.GetCellDataClamped(p);
                    var b = c.TopMostStructure;
                    string text = b != null ? Loc.F("Fire at {0}", b.FriendlyName) : Loc.T("Fire");
                    // With more than one island of your own, which one ("Fire at Hovel, Longvale, your island").
                    var own = Player.inst.PlayerLandmassOwner;
                    if (own != null && own.ownedLandMasses != null && own.ownedLandMasses.Count > 1) text += ", " + GameEvents.Island(c.landMassIdx);
                    var where = new GridPos(c.x, c.z);
                    GameEvents.Log.Add(text, Severity.Danger, Player.inst.CurrYear, where);
                    A.Cue(Cue.Alert);
                    A.SayQueued(text + ", " + Directions.Relative(MapController.Inst.CursorPos, where));
                }
                if (burning.Count > 0 && now.Count == 0) A.SayQueued(Loc.T("All fires are out."));
            }
            burning.Clear();
            burning.AddRange(now);
        }
    }
}
