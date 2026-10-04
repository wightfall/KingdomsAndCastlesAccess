using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace KCAccess
{
    /// <summary>
    /// Fallback for key presses Unity never receives. Reported on a Windows 11 laptop: Escape, Space, 1 2 3 and
    /// letters did nothing while arrows and F-keys worked (Unity's keyboard reading is picky about synthesized
    /// input such as NVDA Remote, and Steam's input layer can interfere too). The Windows key state is polled;
    /// a press Windows saw but Unity did not is handed to the mod as if Unity had seen it, and keys the game
    /// handles itself (pause, speed, Escape) are forwarded to the game's hotkey check.
    /// </summary>
    internal static class OsKeyboard
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private struct Watched
        {
            public KeyCode Key;
            public int Vk;
        }

        private static readonly List<Watched> keys = new List<Watched>();
        private static readonly Dictionary<int, bool> wasDown = new Dictionary<int, bool>();
        private static readonly Dictionary<int, int> pending = new Dictionary<int, int>();
        /// <summary>Keys Unity reported down during the last few frames (from the mod's own key log of the frame).</summary>
        private static readonly HashSet<KeyCode> seenByUnity = new HashSet<KeyCode>();
        private static int seenFrame = -1;
        private static bool available = Application.platform == RuntimePlatform.WindowsPlayer;

        /// <summary>Game hotkey actions to report as pressed (the game reads them on its next keyboard update).</summary>
        private static readonly Dictionary<InputActions, int> forced = new Dictionary<InputActions, int>();

        static OsKeyboard()
        {
            for (int i = 0; i < 26; i++) keys.Add(new Watched { Key = KeyCode.A + i, Vk = 0x41 + i });
            for (int i = 0; i < 10; i++) keys.Add(new Watched { Key = KeyCode.Alpha0 + i, Vk = 0x30 + i });
            keys.Add(new Watched { Key = KeyCode.Space, Vk = 0x20 });
            keys.Add(new Watched { Key = KeyCode.Escape, Vk = 0x1B });
            keys.Add(new Watched { Key = KeyCode.Return, Vk = 0x0D });
            keys.Add(new Watched { Key = KeyCode.Tab, Vk = 0x09 });
            keys.Add(new Watched { Key = KeyCode.Backspace, Vk = 0x08 });
            keys.Add(new Watched { Key = KeyCode.Delete, Vk = 0x2E });
            keys.Add(new Watched { Key = KeyCode.Home, Vk = 0x24 });
            keys.Add(new Watched { Key = KeyCode.End, Vk = 0x23 });
            keys.Add(new Watched { Key = KeyCode.PageUp, Vk = 0x21 });
            keys.Add(new Watched { Key = KeyCode.PageDown, Vk = 0x22 });
            keys.Add(new Watched { Key = KeyCode.UpArrow, Vk = 0x26 });
            keys.Add(new Watched { Key = KeyCode.DownArrow, Vk = 0x28 });
            keys.Add(new Watched { Key = KeyCode.LeftArrow, Vk = 0x25 });
            keys.Add(new Watched { Key = KeyCode.RightArrow, Vk = 0x27 });
            keys.Add(new Watched { Key = KeyCode.LeftBracket, Vk = 0xDB });
            keys.Add(new Watched { Key = KeyCode.RightBracket, Vk = 0xDD });
            keys.Add(new Watched { Key = KeyCode.Backslash, Vk = 0xDC });
            keys.Add(new Watched { Key = KeyCode.Period, Vk = 0xBE });
            for (int i = 0; i < 12; i++) keys.Add(new Watched { Key = KeyCode.F1 + i, Vk = 0x70 + i });
        }

        /// <summary>Called once per frame before the mod reads keys.</summary>
        internal static void Tick(bool typing)
        {
            if (!available || Plugin.CfgKeyFallback == null || !Plugin.CfgKeyFallback.Value) return;
            if (forced.Count > 0)
            {
                // A forwarded hotkey the game did not read within a few frames is dropped, never fired later.
                var stale = new List<InputActions>();
                foreach (var kv in forced) if (Time.frameCount - kv.Value > 3) stale.Add(kv.Key);
                foreach (var a in stale) forced.Remove(a);
            }
            if (!Application.isFocused)
            {
                wasDown.Clear();
                pending.Clear();
                return;
            }
            if (seenFrame < Time.frameCount - 3)
            {
                seenByUnity.Clear();
                seenFrame = Time.frameCount;
            }
            foreach (var w in keys) if (Input.GetKeyDown(w.Key)) seenByUnity.Add(w.Key);
            foreach (var w in keys)
            {
                bool down, tapped;
                try
                {
                    short state = GetAsyncKeyState(w.Vk);
                    down = (state & 0x8000) != 0;
                    tapped = (state & 0x0001) != 0; // pressed since the last poll (catches very short taps)
                }
                catch (Exception)
                {
                    available = false;
                    return;
                }
                wasDown.TryGetValue(w.Vk, out bool before);
                wasDown[w.Vk] = down;
                bool unitySees = Input.GetKey(w.Key) || Input.GetKeyDown(w.Key) || Input.GetKeyUp(w.Key);
                if (((down && !before) || (tapped && !down)) && !unitySees) pending[w.Vk] = Time.frameCount;
                if (!pending.TryGetValue(w.Vk, out int since)) continue;
                if (unitySees || seenByUnity.Contains(w.Key))
                {
                    pending.Remove(w.Vk); // Unity caught up: nothing to do
                    continue;
                }
                if (Time.frameCount - since < 2) continue; // give Unity a frame to deliver it
                pending.Remove(w.Vk);
                if (typing) continue; // text fields get their characters from Unity only
                Deliver(w.Key);
            }
        }

        internal static void Deliver(KeyCode key)
        {
            bool shift = Modifiers.Shift, ctrl = Modifiers.Ctrl, alt = Modifiers.Alt;
            Plugin.Log.LogInfo("[fallback] " + key + " delivered from Windows (Unity missed it)" + (shift ? " +shift" : "") + (ctrl ? " +ctrl" : "") + (alt ? " +alt" : ""));
            KInput.Inject(key, shift, ctrl, alt);
            bool menuEscape = key == KeyCode.Escape && GameState.inst != null && GameState.inst.IsMainMenuMode();
            if (menuEscape)
            {
                // In the pause and settings menus the game reads Escape directly instead of as a hotkey.
                var mm = GameState.inst.mainMenuMode;
                var st = mm.GetState();
                if (st == MainMenuMode.State.PauseMenu) mm.OnClickedReturnToGame();
                else if (st == MainMenuMode.State.SettingsMenu) mm.settingsUI.GetComponent<SettingsMenuUI>().RevertChanges();
                return;
            }
            // Let the game's own hotkeys see it too (pause, speed, Escape menu, chop, rotate ...).
            try
            {
                var settings = Assets.Settings.inst.KeyboardSettings;
                for (int i = 0; i < (int)InputActions.NumActions && i < settings.Length; i++)
                {
                    var chord = settings[i];
                    if (chord.key == key && chord.ctrl == ctrl && chord.alt == alt && chord.shift == shift)
                        forced[(InputActions)i] = Time.frameCount;
                }
            }
            catch (Exception)
            {
                // settings not ready yet
            }
        }

        /// <summary>Used by the hotkey patch: is this game action pressed by a fallback delivery?</summary>
        internal static bool Forced(InputActions action)
        {
            if (forced.Count == 0 || !forced.TryGetValue(action, out int frame)) return false;
            forced.Remove(action);
            return Time.frameCount - frame <= 3;
        }
    }
}
