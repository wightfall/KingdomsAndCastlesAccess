using System;
using System.Collections.Generic;
using HarmonyLib;
using KCAccess.Game;
using UnityEngine;

namespace KCAccess
{
    /// <summary>While the keyboard drives the pointer, the mouse resting over a UI panel must not swallow clicks.</summary>
    [HarmonyPatch(typeof(GameUI), nameof(GameUI.PointerOverUI))]
    internal static class Patch_PointerOverUI
    {
        private static bool Prefix(ref bool __result)
        {
            var vp = VirtualPointer.Inst;
            if (vp != null && vp.KeyboardActive && !GamepadControl.isControllerActive)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    /// <summary>Speaks and records every kingdom notification.</summary>
    [HarmonyPatch(typeof(KingdomLog), nameof(KingdomLog.TryLog))]
    internal static class Patch_KingdomLog
    {
        private static void Prefix(out int __state)
        {
            __state = KingdomLog.inst != null ? KingdomLog.inst.log.Count : -1;
        }

        private static void Postfix(string id, string message, KingdomLog.LogStatus status, object GameObjectOrVector3, int __state)
        {
            try
            {
                if (KingdomLog.inst == null || __state < 0 || KingdomLog.inst.log.Count <= __state) return; // suppressed duplicate
                Vector3? pos = null;
                if (GameObjectOrVector3 is ICamTrackable t) pos = t.GetDesiredTrackingPos();
                else if (GameObjectOrVector3 is Vector3 v) pos = v;
                else if (GameObjectOrVector3 is GameObject go && go != null) pos = go.transform.position;
                GameEvents.OnNotification(id, message, status, pos);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Notification hook failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(VikingNotification), nameof(VikingNotification.ShowBanner))]
    internal static class Patch_VikingBanner
    {
        private static void Postfix() => GameEvents.OnThreat("Vikings are attacking!");
    }

    [HarmonyPatch(typeof(DragonNotification), nameof(DragonNotification.ShowBanner))]
    internal static class Patch_DragonBanner
    {
        private static void Postfix() => GameEvents.OnThreat("Dragons are attacking!");
    }

    [HarmonyPatch(typeof(MerchantNotification), nameof(MerchantNotification.ShowBanner))]
    internal static class Patch_MerchantBanner
    {
        private static void Postfix() => GameEvents.OnInfo("A merchant ship has arrived.");
    }

    /// <summary>Announces how many pieces were built (the game restages the next piece instantly, so polling misses it).</summary>
    [HarmonyPatch(typeof(PlacementMode), nameof(PlacementMode.AcceptPlacement))]
    internal static class Patch_AcceptPlacement
    {
        internal sealed class State
        {
            public string Name;
            public int Valid;
            public readonly Dictionary<string, int> Skipped = new Dictionary<string, int>();
        }

        private static void Prefix(PlacementMode __instance, out State __state)
        {
            var st = new State();
            try
            {
                foreach (var b in __instance.buildings)
                {
                    if (b == null) continue;
                    st.Name = b.FriendlyName;
                    var r = World.inst.CanPlace(b);
                    if (r == PlacementValidationResult.Valid) { st.Valid++; continue; }
                    string why = KCAccess.Core.PlacementText.Explain(r.ToString());
                    st.Skipped[why] = st.Skipped.TryGetValue(why, out int n) ? n + 1 : 1;
                }
            }
            catch
            {
                // Never break placement because of the announcement.
            }
            __state = st;
        }

        private static void Postfix(bool __result, State __state)
        {
            if (__state == null || __state.Name == null) return;
            string skipped = null;
            if (__state.Valid > 0 && __state.Skipped.Count > 0)
            {
                var parts = new List<string>();
                foreach (var kv in __state.Skipped) parts.Add(kv.Value + " skipped, " + kv.Key);
                skipped = string.Join("; ", parts.ToArray());
            }
            MapController.Inst.OnPlacementAccepted(__result, __state.Name, __state.Valid, skipped);
        }
    }

    /// <summary>Confirms manual saves (autosaves stay silent).</summary>
    [HarmonyPatch(typeof(Assets.Code.UI.SaveLoadUI), nameof(Assets.Code.UI.SaveLoadUI.ClickSaveItem))]
    internal static class Patch_Save
    {
        private static void Postfix() => GameEvents.OnSaved();
    }
}

namespace KCAccess
{
    /// <summary>The C key (or the tile panel's chop button) toggles chopping on the selected tile: say which way it went.</summary>
    [HarmonyPatch(typeof(TileInfoUI), nameof(TileInfoUI.ClickedChop))]
    internal static class Patch_ClickedChop
    {
        private static void Prefix(TileInfoUI __instance, out int __state)
        {
            var c = GameUI.inst != null ? GameUI.inst.GetCellSelected() : null;
            if (c == null || c.TreeAmount == 0) { __state = 0; return; }
            __instance.UpdateChopStatus();
            __state = __instance.clearCutter == null ? 1 : 2;
        }

        private static void Postfix(int __state)
        {
            if (__state == 0) return;
            A.Cue(__state == 1 ? KCAccess.Core.Cue.Activate : KCAccess.Core.Cue.Close);
            A.Say(__state == 1 ? "Trees marked for chopping. Idle villagers will cut them for wood." : "Chopping cancelled on this tile.", force: true);
        }
    }
}

namespace KCAccess
{
    /// <summary>Saving from the save screen gives no visible confirmation either; say it (autosaves stay quiet).</summary>
    [HarmonyPatch(typeof(LoadSave), nameof(LoadSave.Save))]
    internal static class Patch_ManualSave
    {
        private static void Postfix()
        {
            try
            {
                if (GameState.inst != null && GameState.inst.IsMainMenuMode() && GameState.inst.mainMenuMode.GetState() == MainMenuMode.State.Save)
                {
                    A.Cue(KCAccess.Core.Cue.Placed);
                    A.SayQueued("Game saved");
                }
            }
            catch
            {
                // Announcement only.
            }
        }
    }
}

namespace KCAccess
{
    /// <summary>Villagers picked from a house or workplace list (the magnifying glass) are selected silently by the game.</summary>
    [HarmonyPatch(typeof(PersonListItemUI), "OnMagClick")]
    internal static class Patch_PersonListMagClick
    {
        private static void Postfix(PersonListItemUI __instance)
        {
            if (__instance == null || __instance.Villager == null) return;
            A.Cue(KCAccess.Core.Cue.Activate);
            A.Say("Selected " + Game.CellInfo.VillagerSummary(__instance.Villager) + ". Villager details are added at the end of this panel.", force: true);
        }
    }
}

namespace KCAccess
{
    /// <summary>Deleting a save removes its slot silently; confirm it.</summary>
    [HarmonyPatch(typeof(LoadSave), nameof(LoadSave.Delete))]
    internal static class Patch_DeleteSave
    {
        private static void Postfix()
        {
            A.Cue(KCAccess.Core.Cue.Close);
            A.SayQueued("Save deleted");
        }
    }
}
