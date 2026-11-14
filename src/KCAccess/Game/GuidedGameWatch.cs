using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// The guided first game (<see cref="GuidedGame"/>): looks every two seconds at what the player has finished and
    /// says the next step when one is done. Nothing is saved: a kingdom that has every step is complete and silent.
    /// </summary>
    internal static class GuidedGameWatch
    {
        /// <summary>The current step, or -1 before the first look at this world.</summary>
        private static int step = -1;
        private static float next;

        internal static bool Enabled => Plugin.CfgGuidedGame != null && Plugin.CfgGuidedGame.Value;

        /// <summary>A new world (loaded or generated): look at it afresh.</summary>
        internal static void Reset()
        {
            step = -1;
            next = 0f;
        }

        /// <summary>Finished buildings of this kind on the player's islands.</summary>
        internal static int Built(string uniqueName)
        {
            var owner = Player.inst != null ? Player.inst.PlayerLandmassOwner : null;
            if (owner == null || owner.ownedLandMasses == null) return 0;
            int hash = uniqueName.GetHashCode(); // the game keys its building lists the same way (Building.uniqueNameHash)
            int n = 0;
            for (int i = 0; i < owner.ownedLandMasses.Count; i++)
            {
                var list = Player.inst.GetBuildingListForLandMass(owner.ownedLandMasses.data[i], hash);
                if (list == null) continue;
                for (int j = 0; j < list.Count; j++)
                    if (list.data[j] != null && list.data[j].IsBuilt()) n++;
            }
            return n;
        }

        internal static void Tick()
        {
            if (!Enabled || Time.unscaledTime < next) return;
            next = Time.unscaledTime + 2f;
            int now = GuidedGame.Current(Built);
            if (step < 0)
            {
                // A kingdom loaded part way through: where it stands. A new one (no keep yet) already hears how to
                // build the keep, and a complete one hears nothing.
                step = now;
                if (now >= 1 && now < GuidedGame.Count) A.SayQueued(KeyHelp.Resolve(Loc.T("Guided game.") + " " + GuidedGame.Describe(now)));
                return;
            }
            if (now > step)
            {
                A.Cue(Cue.Placed);
                A.SayQueued(KeyHelp.Resolve(GuidedGame.Progress(step, now)));
            }
            step = now; // also when a building was demolished: the step is simply open again
        }

        /// <summary>The kingdom status line ("Guided game, step 3 of 10: ..."), or null when off or complete.</summary>
        internal static string StatusLine()
        {
            if (!Enabled) return null;
            int now = step >= 0 ? step : GuidedGame.Current(Built);
            if (now >= GuidedGame.Count) return null;
            return KeyHelp.Resolve(Loc.T("Guided game.") + " " + GuidedGame.Describe(now));
        }
    }
}
