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
        private static void Prefix(PlacementMode __instance, out KeyValuePair<string, int> __state)
        {
            int valid = 0;
            string name = null;
            try
            {
                foreach (var b in __instance.buildings)
                {
                    if (b == null) continue;
                    name = b.FriendlyName;
                    if (World.inst.CanPlace(b) == PlacementValidationResult.Valid) valid++;
                }
            }
            catch
            {
                // Never break placement because of the announcement.
            }
            __state = new KeyValuePair<string, int>(name, valid);
        }

        private static void Postfix(bool __result, KeyValuePair<string, int> __state)
        {
            if (__state.Key == null) return;
            MapController.Inst.OnPlacementAccepted(__result, __state.Key, __state.Value);
        }
    }

    /// <summary>Confirms manual saves (autosaves stay silent).</summary>
    [HarmonyPatch(typeof(Assets.Code.UI.SaveLoadUI), nameof(Assets.Code.UI.SaveLoadUI.ClickSaveItem))]
    internal static class Patch_Save
    {
        private static void Postfix() => GameEvents.OnSaved();
    }
}
