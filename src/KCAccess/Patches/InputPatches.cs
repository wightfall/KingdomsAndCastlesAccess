using HarmonyLib;
using UnityEngine;

namespace KCAccess
{
    /// <summary>Decides which game hotkeys are allowed through while the mod is using the keyboard.</summary>
    internal static class InputGate
    {
        /// <summary>Set by the controller while a mod menu / modal panel owns the keyboard.</summary>
        internal static bool BlockGameKeys;

        /// <summary>While blocking, still let the game's Escape behaviour run unless the mod consumed Escape.</summary>
        internal static bool EscapePassThrough;

        internal static bool Allow(InputActions action)
        {
            // Note: BepInEx's plugin object is destroyed by this game after start-up, so never test Plugin.Instance.
            if (!Plugin.Loaded) return true;
            KeyChord chord;
            try
            {
                chord = Assets.Settings.inst.KeyboardSettings[(int)action];
            }
            catch
            {
                return true;
            }
            if (KInput.IsConsumed(chord.key)) return false;
            if (action == InputActions.ToggleUIDisplay) return false; // hiding the interface makes every panel unreadable
            if (!BlockGameKeys) return true;
            if (EscapePassThrough && action == InputActions.EscapeButtonBehavior) return true;
            // While a mod menu is open only let camera keys and speed keys through.
            switch (action)
            {
                case InputActions.CameraMoveFast:
                    return true;
                default:
                    return false;
            }
        }
    }

    [HarmonyPatch(typeof(ConfigurableControls), nameof(ConfigurableControls.GetInputActionKeyDown))]
    internal static class Patch_GetInputActionKeyDown
    {
        private static bool Prefix(InputActions inputAction, ref bool __result)
        {
            if (InputGate.Allow(inputAction) && OsKeyboard.Forced(inputAction))
            {
                __result = true; // a press Unity missed, delivered from Windows
                return false;
            }
            if (InputGate.Allow(inputAction)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ConfigurableControls), nameof(ConfigurableControls.GetInputActionKey))]
    internal static class Patch_GetInputActionKey
    {
        private static bool Prefix(InputActions inputAction, ref bool __result)
        {
            if (InputGate.Allow(inputAction)) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>
    /// Game bug fix: KeyChord.GetKeyDown/GetKeyUp compared the modifier keys with GetKeyDown/GetKeyUp,
    /// so a binding like Ctrl+S only fired if Ctrl was pressed in the very same frame (practically never),
    /// and Ctrl/Shift/Alt + key still triggered the plain binding. Modifiers must be *held*.
    /// </summary>
    [HarmonyPatch(typeof(KeyChord), nameof(KeyChord.GetKeyDown))]
    internal static class Patch_KeyChordDown
    {
        private static bool Prefix(ref KeyChord __instance, ref bool __result)
        {
            __result = Input.GetKeyDown(__instance.key) && ModifiersMatch(__instance);
            return false;
        }

        internal static bool ModifiersMatch(KeyChord k)
        {
            bool ctrl = Modifiers.Ctrl;
            bool alt = Modifiers.Alt;
            bool shift = Modifiers.Shift;
            bool ok = true;
            if (k.key != KeyCode.LeftControl && k.key != KeyCode.RightControl) ok &= ctrl == k.ctrl;
            if (k.key != KeyCode.LeftAlt && k.key != KeyCode.RightAlt) ok &= alt == k.alt;
            if (k.key != KeyCode.LeftShift && k.key != KeyCode.RightShift) ok &= shift == k.shift;
            return ok;
        }
    }

    /// <summary>Held bindings (camera keys) use the same modifier check, with the stuck-key protection.</summary>
    [HarmonyPatch(typeof(KeyChord), nameof(KeyChord.GetKey), new[] { typeof(bool), typeof(bool), typeof(bool) })]
    internal static class Patch_KeyChordHeld
    {
        private static bool Prefix(ref KeyChord __instance, bool ignoreCtrl, bool ignoreAlt, bool ignoreShift, ref bool __result)
        {
            var k = __instance;
            bool ok = Input.GetKey(k.key);
            if (ok && k.key != KeyCode.LeftControl && k.key != KeyCode.RightControl && !ignoreCtrl) ok = Modifiers.Ctrl == k.ctrl;
            if (ok && k.key != KeyCode.LeftAlt && k.key != KeyCode.RightAlt && !ignoreAlt) ok = Modifiers.Alt == k.alt;
            if (ok && k.key != KeyCode.LeftShift && k.key != KeyCode.RightShift && !ignoreShift) ok = Modifiers.Shift == k.shift;
            __result = ok;
            return false;
        }
    }

    [HarmonyPatch(typeof(KeyChord), nameof(KeyChord.GetKeyUp))]
    internal static class Patch_KeyChordUp
    {
        private static bool Prefix(ref KeyChord __instance, ref bool __result)
        {
            __result = Input.GetKeyUp(__instance.key) && Patch_KeyChordDown.ModifiersMatch(__instance);
            return false;
        }
    }

    /// <summary>
    /// Game bug fix: Ctrl+C silently toggled creative mode during normal play (a developer
    /// shortcut left outside the cheat check). Only allow it when cheats are enabled.
    /// </summary>
    [HarmonyPatch(typeof(Assets.Code.KeyboardControl), "UpdatePlaymodeKeys")]
    internal static class Patch_CreativeToggle
    {
        private static void Prefix(out bool __state)
        {
            __state = Player.inst != null && Player.inst.creativeMode;
        }

        private static void Postfix(Assets.Code.KeyboardControl __instance, bool __state)
        {
            if (Player.inst == null || Player.inst.creativeMode == __state) return;
            bool ctrlC = Input.GetKeyDown(KeyCode.C) && Input.GetKey(KeyCode.LeftControl);
            if (ctrlC && !__instance.CheatsEnabled && !Application.isEditor)
            {
                Player.inst.creativeMode = __state;
            }
        }
    }

    /// <summary>Let the mod handle the keyboard before the game's own hotkeys run this frame.</summary>
    [HarmonyPatch(typeof(Assets.Code.KeyboardControl), "Update")]
    internal static class Patch_KeyboardControlUpdate
    {
        private static void Prefix()
        {
            if (AccessController.Inst != null) AccessController.Inst.EnsureTick();
        }
    }
}
