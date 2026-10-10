using System;
using System.Collections.Generic;
using KCAccess.Core;
using Rewired;
using UnityEngine;

namespace KCAccess
{
    /// <summary>
    /// Gamepad support for the mod. Reads any controller Rewired knows (Xbox, PlayStation and most others, through
    /// Rewired's gamepad template) and turns presses into the keyboard keys of Core.ControllerMap, delivered like
    /// typed keys (mod keys and the game's own hotkeys alike). Held directions repeat like a held arrow key.
    /// The game's own console-controller mode is kept off meanwhile: it is built for sighted players and moves
    /// focus around the screen on its own.
    /// </summary>
    internal static class ControllerInput
    {
        private const float RepeatDelay = 0.45f, RepeatEvery = 0.12f, StickThreshold = 0.6f, TriggerThreshold = 0.5f;

        private static readonly Dictionary<PadButton, float> nextRepeat = new Dictionary<PadButton, float>();
        private static readonly HashSet<PadButton> heldDirs = new HashSet<PadButton>();
        // Reused every frame (this runs once per frame, also with no controller in the menus).
        private static readonly List<PadButton> pressed = new List<PadButton>();
        private static readonly HashSet<PadButton> dirs = new HashSet<PadButton>();
        private static readonly PadButton[] Directions = { PadButton.Up, PadButton.Down, PadButton.Left, PadButton.Right };
        private static int lastCount = -1;
        private static bool failed;

        internal static bool Enabled => Plugin.CfgController != null && Plugin.CfgController.Value;

        internal static void Tick(bool onMap)
        {
            if (!Enabled || failed) return;
            try
            {
                if (!ReInput.isReady) return;
                var sticks = ReInput.controllers.Joysticks;
                AnnounceConnections(sticks);
                if (!Application.isFocused) return;
                if (sticks.Count == 0 && heldDirs.Count == 0) return; // nothing plugged in: no work every frame
                bool lt = false, rt = false;
                pressed.Clear();
                dirs.Clear();
                // Index loops: foreach over the IList allocated an enumerator every frame.
                for (int si = 0; si < sticks.Count; si++)
                {
                    var t = sticks[si].GetTemplate<IGamepadTemplate>();
                    if (t == null) continue;
                    lt |= t.leftTrigger != null && t.leftTrigger.value > TriggerThreshold;
                    rt |= t.rightTrigger != null && t.rightTrigger.value > TriggerThreshold;
                    Check(t.a, PadButton.A, pressed);
                    Check(t.b, PadButton.B, pressed);
                    Check(t.x, PadButton.X, pressed);
                    Check(t.y, PadButton.Y, pressed);
                    Check(t.leftShoulder1, PadButton.LB, pressed);
                    Check(t.rightShoulder1, PadButton.RB, pressed);
                    Check(t.back, PadButton.Back, pressed);
                    Check(t.start, PadButton.Start, pressed);
                    if (t.leftStick != null) Check(t.leftStick.press, PadButton.L3, pressed);
                    if (t.rightStick != null) Check(t.rightStick.press, PadButton.R3, pressed);
                    // Directions: D-pad and left stick, held ones repeat.
                    if (t.dPad != null)
                    {
                        if (t.dPad.up != null && t.dPad.up.value) dirs.Add(PadButton.Up);
                        if (t.dPad.down != null && t.dPad.down.value) dirs.Add(PadButton.Down);
                        if (t.dPad.left != null && t.dPad.left.value) dirs.Add(PadButton.Left);
                        if (t.dPad.right != null && t.dPad.right.value) dirs.Add(PadButton.Right);
                    }
                    if (t.leftStick != null)
                    {
                        Vector2 v = t.leftStick.value;
                        if (Mathf.Abs(v.y) >= Mathf.Abs(v.x))
                        {
                            if (v.y > StickThreshold) dirs.Add(PadButton.Up);
                            else if (v.y < -StickThreshold) dirs.Add(PadButton.Down);
                        }
                        else
                        {
                            if (v.x > StickThreshold) dirs.Add(PadButton.Right);
                            else if (v.x < -StickThreshold) dirs.Add(PadButton.Left);
                        }
                    }
                }
                foreach (var d in Directions)
                {
                    if (!dirs.Contains(d))
                    {
                        heldDirs.Remove(d);
                        continue;
                    }
                    if (heldDirs.Add(d))
                    {
                        pressed.Add(d);
                        nextRepeat[d] = Time.unscaledTime + RepeatDelay;
                    }
                    else if (Time.unscaledTime >= nextRepeat[d])
                    {
                        pressed.Add(d);
                        nextRepeat[d] = Time.unscaledTime + RepeatEvery;
                    }
                }
                for (int i = 0; i < pressed.Count; i++) Fire(pressed[i], lt, rt, onMap);
            }
            catch (Exception e)
            {
                failed = true; // never break the game because of a controller; keyboard keeps working
                Plugin.Log.LogError("Controller support stopped: " + e.Message);
            }
        }

        private static void Check(IControllerTemplateButton button, PadButton b, List<PadButton> pressed)
        {
            if (button != null && button.justPressed) pressed.Add(b);
        }

        /// <summary>Test only (debug command file): press a gamepad button through the real mapping.</summary>
        internal static void Simulate(PadButton b, bool lt, bool rt) =>
            Fire(b, lt, rt, AccessController.Inst != null && AccessController.Inst.OnMapForPad);

        private static void Fire(PadButton b, bool lt, bool rt, bool onMap)
        {
            var action = KCAccess.Core.ControllerMap.Map(b, lt, rt, onMap);
            string layer = lt && rt ? "both triggers + " : lt ? "LT + " : rt ? "RT + " : string.Empty;
            if (!action.HasValue)
            {
                A.Cue(Cue.Edge);
                Plugin.Log.LogInfo("[pad] " + layer + b + ": nothing");
                return;
            }
            var a = action.Value;
            Chord chord;
            if (a.Binding != null) chord = Plugin.Keys.Get(a.Binding);
            else Chord.TryParse(a.Key, out chord);
            Plugin.Log.LogInfo("[pad] " + layer + b + " -> " + chord);
            if (!KInput.TryKeyCode(chord.Key, out var key)) return;
            // A controller press must not also submit whatever the game's UI happens to have selected.
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null && !(AccessController.Inst != null && AccessController.Inst.Nav.IsEditing)) es.SetSelectedGameObject(null);
            OsKeyboard.Deliver(key, chord.Shift, chord.Ctrl, chord.Alt, "controller");
        }

        private static void AnnounceConnections(IList<Joystick> sticks)
        {
            int n = 0;
            string name = null;
            for (int i = 0; i < sticks.Count; i++)
            {
                var j = sticks[i];
                if (j.GetTemplate<IGamepadTemplate>() == null) continue;
                n++;
                name = j.name;
            }
            if (n == lastCount) return;
            if (lastCount >= 0 || n > 0)
            {
                if (n > lastCount && n > 0) A.Say((string.IsNullOrEmpty(name) ? Loc.T("Controller connected.") : Loc.F("Controller connected: {0}.", name)) + " " + Loc.T("Start gives help, hold Right Trigger and press Start for the controller layout in the key list."), Priority.High);
                else if (n < lastCount) A.Say(Loc.T("Controller disconnected"), Priority.High);
                Plugin.Log.LogInfo("[pad] gamepads connected: " + n + (name != null ? " (" + name + ")" : ""));
            }
            lastCount = n;
        }
    }
}
