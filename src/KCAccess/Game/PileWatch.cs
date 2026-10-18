using System.Collections.Generic;
using Assets.Code;
using HarmonyLib;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// Loose resources dropped on the ground (a demolished building's stock, rock removal's stone, full storage, disbanded
    /// soldiers' armaments, goods with no room at the dock) just appear. New piles on your islands are announced together
    /// once nothing else dropped for two seconds ("On the ground: stone 100, 4 tiles north"); villagers carry them to
    /// storage that has room.
    /// </summary>
    internal static class PileWatch
    {
        private struct Drop
        {
            public GameObject Pile;
            public FreeResourceType Type;
            public int Count;
            public int Frame;
        }

        private static readonly List<Drop> fresh = new List<Drop>();
        private static ResourceAmount batch;
        private static bool batchAny;
        private static GridPos? batchAt;
        private static float quietAt;

        internal static void Reset()
        {
            fresh.Clear();
            ClearBatch();
        }

        private static void ClearBatch()
        {
            batch = default(ResourceAmount);
            batchAny = false;
            batchAt = null;
        }

        /// <summary>A pile was made (Patch_PileDropped); piles remade while a save loads are not news.</summary>
        internal static void Record(GameObject pile, FreeResourceType type, int count)
        {
            if (pile == null || count <= 0 || GameState.inst == null || !GameState.inst.IsPlayMode() || LoadSave.Loading) return;
            fresh.Add(new Drop { Pile = pile, Type = type, Count = count, Frame = Time.frameCount });
        }

        internal static void Tick()
        {
            // The game places a pile right after making it: its position is read a frame later.
            for (int i = fresh.Count - 1; i >= 0; i--)
            {
                var d = fresh[i];
                if (d.Frame >= Time.frameCount) continue;
                fresh.RemoveAt(i);
                if (d.Pile == null) continue;
                var c = World.inst.GetCellData(d.Pile.transform.position);
                if (c == null || c.landMassIdx < 0 || !Player.inst.LandMassIsAPlayerLandMass(c.landMassIdx) || !CellInfo.Explored(c)) continue;
                batch.Add(d.Type, d.Count);
                batchAny = true;
                if (!batchAt.HasValue) batchAt = new GridPos(c.x, c.z);
                quietAt = Time.unscaledTime + 2f;
            }
            if (!batchAny || Time.unscaledTime < quietAt) return;
            string list = CellInfo.Amounts(batch);
            var at = batchAt;
            ClearBatch();
            if (list == null || !Plugin.CfgAnnounceLog.Value) return;
            A.SayQueued(TextUtil.WithPlace(Loc.F("On the ground: {0}", list), at.HasValue ? Directions.Relative(MapController.Inst.CursorPos, at.Value) : null));
        }
    }

    [HarmonyPatch(typeof(FreeResourceManager), nameof(FreeResourceManager.GetAutoStackFor))]
    internal static class Patch_PileDropped
    {
        private static void Postfix(GameObject __result, FreeResourceType t, int count)
        {
            try
            {
                PileWatch.Record(__result, t, count);
            }
            catch
            {
                // announcement only
            }
        }
    }
}
