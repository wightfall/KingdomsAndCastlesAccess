using KCAccess.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KCAccess
{
    /// <summary>
    /// Keeps the keyboard usable when the player is not typing into a field.
    /// Reported on Windows 11: Escape, Space, 1 2 3 and letter keys did nothing while arrows and F6 worked.
    /// That happens when an input method editor (IME) composes the key presses, when a text field
    /// is focused behind the scenes and swallows them, or when the game's hotkeys stay switched off after a
    /// text field. Each frame without a mod edit session: IME off, stray focused fields released, hotkeys back on.
    /// </summary>
    internal static class KeyboardGuard
    {
        private static bool editingBefore;

        internal static void Tick(UINavigator nav, bool onMap)
        {
            bool modEditing = nav != null && nav.IsEditing;
            if (modEditing)
            {
                // Typing a name or amount: let IMEs work (Chinese, Japanese, Korean ...).
                if (!editingBefore) Input.imeCompositionMode = IMECompositionMode.Auto;
                editingBefore = true;
                return;
            }
            editingBefore = false;
            if (Input.imeCompositionMode != IMECompositionMode.Off) Input.imeCompositionMode = IMECompositionMode.Off;

            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            bool fieldFocused = false;
            if (sel != null)
            {
                var tmp = sel.GetComponent<TMP_InputField>();
                var legacy = sel.GetComponent<InputField>();
                if ((tmp != null && tmp.isFocused) || (legacy != null && legacy.isFocused))
                {
                    Selectable field = tmp != null ? (Selectable)tmp : legacy;
                    bool mouseUser = Game.VirtualPointer.Inst != null && !Game.VirtualPointer.Inst.KeyboardActive;
                    if (mouseUser)
                    {
                        fieldFocused = true; // a sighted helper clicked into the field and types on purpose
                    }
                    else if (!onMap && nav.AdoptFocusedField(field))
                    {
                        // The game focused a field of the current window (e.g. the kingdom name Edit button).
                        Plugin.Log.LogInfo("[guard] editing text field " + sel.name);
                        return;
                    }
                    else if (!onMap && UIText.IsVisible(sel))
                    {
                        fieldFocused = true;
                    }
                    else
                    {
                        // On the map, or a hidden field: it would swallow letters, Space, digits and Escape.
                        if (tmp != null) tmp.DeactivateInputField();
                        if (legacy != null) legacy.DeactivateInputField();
                        es.SetSelectedGameObject(null);
                        Plugin.Log.LogInfo("[guard] released focused text field " + sel.name);
                    }
                }
            }
            var gs = GameState.inst;
            if (gs != null && !gs.AlphaNumericHotkeysEnabled && !fieldFocused)
            {
                gs.AlphaNumericHotkeysEnabled = true;
                Plugin.Log.LogInfo("[guard] game hotkeys were left disabled; enabled again");
            }
        }
    }
}
