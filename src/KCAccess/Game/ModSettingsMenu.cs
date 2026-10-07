using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// Ctrl+Shift+O: every mod setting in one list. Enter or Space toggles a check box, Left and Right change a value,
    /// Enter on a key starts rebinding it (conflicts with the mod's or the game's keys are refused with the reason),
    /// Delete restores one key, and "Reset everything" restores all defaults.
    /// </summary>
    internal sealed class ModSettingsMenu : ListMenu
    {
        private sealed class Setting
        {
            public Func<string> Label;
            public Action Enter;
            public Action<int> Adjust;
            public Action Delete;
            public bool IsHeader;
        }

        private readonly bool fromMap;
        private readonly List<Setting> settings = new List<Setting>();
        private string capturing;     // binding id waiting for a key
        private bool waitRelease;     // the Enter that started capturing must be released first
        private float resetArmedUntil = -1f;

        internal bool Capturing => capturing != null;

        internal ModSettingsMenu(bool fromMap)
        {
            this.fromMap = fromMap;
        }

        public override string HelpId => "ModSettings";

        protected override string Title => Loc.T("Mod settings. Up and Down move, Page Up and Page Down jump between groups, Enter or Space toggles, Left and Right change values, Enter on a key changes it, Escape closes");

        // ------------------------------------------------------------------ building the list

        protected override void Build()
        {
            settings.Clear();
            ModLanguage.Refresh();
            Header(Loc.N("Language"));
            Number(LanguageLabel, ChangeLanguage, enter: true);
            Header(Loc.N("Speech"));
            Toggle(Loc.N("Announce notifications"), Plugin.CfgAnnounceLog);
            Toggle(Loc.N("Announce seasons and new years"), Plugin.CfgAnnounceSeasons);
            Toggle(Loc.N("Detailed tile descriptions while moving (fertility and road coverage)"), Plugin.CfgVerboseCells);
            Toggle(Loc.N("Speak cursor coordinates after each tile"), Plugin.CfgCoordinates);
            Toggle(Loc.N("Hints for new players"), Plugin.CfgHints);
            Toggle(Loc.N("Say the position in menus and lists, for example 3 of 19"), Plugin.CfgPositions);
            Header(Loc.N("Sound"));
            Toggle(Loc.N("Sound cues"), Plugin.CfgCues);
            Number(() => Loc.F("Sound cue volume: {0} percent", Mathf.RoundToInt(Plugin.CfgCueVolume.Value * 100)),
                d =>
                {
                    Plugin.CfgCueVolume.Value = Mathf.Clamp01(Mathf.Round((Plugin.CfgCueVolume.Value + d * 0.1f) * 10f) / 10f);
                    A.Cue(Cue.Value, 0.6f + Plugin.CfgCueVolume.Value);
                });
            Header(Loc.N("Map"));
            Toggle(Loc.N("Camera follows the cursor"), Plugin.CfgCameraFollow);
            Number(() => Loc.F("Auto-walk speed: {0}", Plugin.CfgWalkSpeed.Value <= 1 ? Loc.T("slow") : Plugin.CfgWalkSpeed.Value >= 3 ? Loc.T("fast") : Loc.T("normal")),
                d => Plugin.CfgWalkSpeed.Value = Mathf.Clamp(Plugin.CfgWalkSpeed.Value + d, 1, 3));
            Header(Loc.N("Keyboard and logs"));
            Toggle(Loc.N("Controller support: play with a gamepad (turn off to use the game's own controller mode)"), Plugin.CfgController);
            Toggle(Loc.N("Deliver key presses the game missed (for Windows 11, NVDA Remote and Steam Input problems)"), Plugin.CfgKeyFallback);
            Toggle(Loc.N("Write key presses to the log (bug reports)"), Plugin.CfgLogKeys);
            Toggle(Loc.N("Write everything spoken to the log (bug reports)"), Plugin.CfgLogSpeech);
            Header(Loc.N("Streaming"));
            Toggle(Loc.N("Announce when 10 seconds are left in a Twitch vote"), Plugin.CfgTwitchCountdown);
            Toggle(Loc.N("Read Twitch chat aloud (vote messages are skipped)"), Plugin.CfgTwitchChat);
            Toggle(Loc.N("Show speech captions on screen for stream viewers"), Plugin.CfgCaptions);
            Toggle(Loc.N("Write a status file for streaming overlays, kcaccess_stream.txt in the BepInEx folder"), Plugin.CfgStreamFile);
            Header(Loc.N("Mod keys. Enter changes a key, Delete restores its default"));
            foreach (var def in Bindings.Defs)
            {
                var d = def;
                Add(new Setting
                {
                    Label = () => d.SpokenName + ": " + Plugin.Keys.Spoken(d.Id) + (Plugin.Keys.Get(d.Id).Equals(d.Default) ? "" : ", " + Loc.T("changed")) + ", " + (d.Scope == BindingScope.Map ? Loc.T("on the map") : Loc.T("everywhere")),
                    Enter = () => StartCapture(d),
                    Delete = () => RestoreDefault(d)
                });
            }
            Header(Loc.N("Reset"));
            Add(new Setting { Label = () => Loc.T("Reset everything to the defaults, all settings and keys. Press Enter twice"), Enter = ResetAll });
        }

        // ------------------------------------------------------------------ language

        /// <summary>The choices of the mod language setting: automatic, English and every language file.</summary>
        private static List<string> LanguageChoices()
        {
            var l = new List<string> { LanguageFiles.Auto, LanguageFiles.English };
            foreach (var info in ModLanguage.Available) l.Add(info.Code);
            return l;
        }

        private static string LanguageLabel()
        {
            string setting = ModLanguage.Setting;
            string value = string.Equals(setting, LanguageFiles.Auto, StringComparison.OrdinalIgnoreCase)
                ? Loc.F("Automatic, follows the game, now {0}", ModLanguage.ActiveName)
                : ModLanguage.NameOf(setting);
            return Loc.F("Mod language: {0}. Left and Right choose", value);
        }

        private static void ChangeLanguage(int delta)
        {
            var choices = LanguageChoices();
            int i = choices.FindIndex(c => string.Equals(c, ModLanguage.Setting, StringComparison.OrdinalIgnoreCase));
            if (i < 0) i = 0;
            i = (i + delta + choices.Count) % choices.Count;
            Plugin.CfgLanguage.Value = choices[i];
            ModLanguage.Apply(announce: false);
        }

        private void Add(Setting s)
        {
            settings.Add(s);
            var st = s;
            Add(() => st.Label(), st.Enter);
        }

        /// <summary>Group header; <paramref name="text"/> is English (marked with Loc.N) and translated when spoken.</summary>
        private void Header(string text) => Add(new Setting { Label = () => Loc.T(text), IsHeader = true });

        /// <summary>A check box; <paramref name="name"/> is English (marked with Loc.N) and translated when spoken.</summary>
        private void Toggle(string name, ConfigEntry<bool> entry)
        {
            Add(new Setting
            {
                Label = () => Loc.F("{0}, check box, {1}", Loc.T(name), entry.Value ? Loc.T("checked") : Loc.T("not checked")),
                Enter = () =>
                {
                    entry.Value = !entry.Value;
                    A.Cue(entry.Value ? Cue.ToggleOn : Cue.ToggleOff);
                    A.Say(entry.Value ? Loc.T("checked") : Loc.T("not checked"), force: true);
                }
            });
        }

        /// <summary>A value changed with Left and Right (and with Enter too when <paramref name="enter"/>).</summary>
        private void Number(Func<string> label, Action<int> change, bool enter = false)
        {
            Action<int> adjust = d =>
            {
                change(d);
                A.Say(label(), force: true);
            };
            Add(new Setting
            {
                Label = label,
                Adjust = adjust,
                Enter = enter ? () => adjust(1) : (Action)null
            });
        }

        private Setting Current => List.Index >= 0 && List.Index < settings.Count ? settings[List.Index] : null;

        // ------------------------------------------------------------------ keys

        public override void HandleInput()
        {
            if (Capturing)
            {
                Capture();
                return;
            }
            base.HandleInput();
        }

        protected override bool HandleExtraKeys()
        {
            if (KInput.Plain(KeyCode.PageDown) || KInput.Plain(KeyCode.PageUp))
            {
                // Jump between the groups (Speech, Sound, Map, Keyboard, Streaming, Mod keys, Reset), like the Shift+F1 list.
                int dir = KInput.Down(KeyCode.PageDown) ? 1 : -1;
                for (int i = List.Index + dir; i >= 0 && i < settings.Count; i += dir)
                {
                    if (!settings[i].IsHeader) continue;
                    List.SelectIndex(i);
                    Speak(NavResult.Moved);
                    return true;
                }
                A.Cue(Cue.Edge);
                A.Say(dir > 0 ? Loc.T("No more groups, this is the last") : Loc.T("No more groups, this is the first"));
                return true;
            }
            var cur = Current;
            if (cur == null) return false;
            if (KInput.Plain(KeyCode.Space) && cur.Enter != null && cur.Adjust == null && cur.Delete == null)
            {
                cur.Enter();
                return true;
            }
            if (cur.Adjust != null && (KInput.Plain(KeyCode.RightArrow) || KInput.Plain(KeyCode.LeftArrow)))
            {
                cur.Adjust(KInput.Down(KeyCode.RightArrow) ? 1 : -1);
                return true;
            }
            if (cur.Delete != null && KInput.Plain(KeyCode.Delete))
            {
                KInput.Consume(KeyCode.Delete);
                cur.Delete();
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ rebinding

        private void StartCapture(BindingDef d)
        {
            capturing = d.Id;
            waitRelease = true;
            A.Cue(Cue.Open);
            A.Say(Loc.F("Press the new key for {0}. You can hold Control, Shift or Alt with it. Escape cancels.", d.SpokenName), force: true);
        }

        private static readonly KeyCode[] Candidates = BuildCandidates();

        private static KeyCode[] BuildCandidates()
        {
            var l = new List<KeyCode>();
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                if (k == KeyCode.None || k >= KeyCode.Mouse0) continue;
                switch (k)
                {
                    case KeyCode.LeftShift: case KeyCode.RightShift:
                    case KeyCode.LeftControl: case KeyCode.RightControl:
                    case KeyCode.LeftAlt: case KeyCode.RightAlt:
                    case KeyCode.LeftCommand: case KeyCode.RightCommand:
                    case KeyCode.LeftWindows: case KeyCode.RightWindows:
                    case KeyCode.AltGr: case KeyCode.CapsLock: case KeyCode.Numlock: case KeyCode.ScrollLock:
                        continue;
                }
                if (!l.Contains(k)) l.Add(k);
            }
            return l.ToArray();
        }

        private void Capture()
        {
            InputGate.BlockGameKeys = true;
            if (waitRelease)
            {
                // Ignore the Enter that started capturing; wait until every key is up.
                if (!Input.anyKey) waitRelease = false;
                return;
            }
            KeyCode pressed = KeyCode.None;
            foreach (var k in Candidates)
            {
                if (Input.GetKeyDown(k)) { pressed = k; break; }
            }
            if (pressed == KeyCode.None) return;
            KInput.Consume(pressed);
            var def = Bindings.Def(capturing);
            if (pressed == KeyCode.Escape && !KInput.Ctrl && !KInput.Shift && !KInput.Alt)
            {
                capturing = null;
                A.Cue(Cue.Close);
                A.Say(Loc.F("Cancelled. {0} stays {1}", def.SpokenName, Plugin.Keys.Spoken(def.Id)), force: true);
                return;
            }
            var chord = new Chord(pressed.ToString(), KInput.Ctrl, KInput.Shift, KInput.Alt);
            capturing = null;
            Apply(def, chord);
        }

        private void Apply(BindingDef def, Chord chord)
        {
            if (Plugin.Keys.Get(def.Id).Equals(chord))
            {
                A.Say(Loc.F("{0} is already {1}", def.SpokenName, chord.Spoken()), force: true);
                return;
            }
            string why = Plugin.Keys.Conflict(def.Id, chord, GameUse);
            if (why != null)
            {
                A.Cue(Cue.Error);
                A.Say(Loc.F("{0} cannot be used: {1}. {2} stays {3}.", chord.Spoken(), why, def.SpokenName, Plugin.Keys.Spoken(def.Id)), force: true);
                return;
            }
            Plugin.Keys.Set(def.Id, chord);
            Save();
            A.Cue(Cue.Placed);
            A.Say(Loc.F("{0} is now {1}", def.SpokenName, chord.Spoken()), force: true);
        }

        private void RestoreDefault(BindingDef def)
        {
            if (Plugin.Keys.Get(def.Id).Equals(def.Default))
            {
                A.Say(Loc.F("{0} already has its default key, {1}", def.SpokenName, def.Default.Spoken()), force: true);
                return;
            }
            Apply(def, def.Default);
        }

        private static void Save() => Plugin.CfgBindings.Value = Plugin.Keys.Serialize();

        /// <summary>Name of the game's own action on this chord (its keyboard settings), or null.</summary>
        internal static string GameUse(Chord chord)
        {
            try
            {
                var settings = Assets.Settings.inst.KeyboardSettings;
                for (int i = 0; i < (int)InputActions.NumActions && i < settings.Length; i++)
                {
                    var k = settings[i];
                    if (string.Equals(k.key.ToString(), chord.Key, StringComparison.OrdinalIgnoreCase) && k.ctrl == chord.Ctrl && k.shift == chord.Shift && k.alt == chord.Alt)
                        return GameActionName((InputActions)i);
                }
            }
            catch (Exception)
            {
                // settings not loaded yet
            }
            // Keys the game reads directly.
            if (!chord.Ctrl && !chord.Alt && !chord.Shift && chord.Key == "Delete") return Loc.T("demolish");
            return null;
        }

        internal static string GameActionName(InputActions a)
        {
            switch (a)
            {
                case InputActions.CameraRotateLeft: return Loc.T("rotating the camera left");
                case InputActions.CameraRotateRight: return Loc.T("rotating the camera right");
                case InputActions.CameraMoveForward: return Loc.T("moving the camera forward");
                case InputActions.CameraMoveBack: return Loc.T("moving the camera back");
                case InputActions.CameraMoveLeft: return Loc.T("moving the camera left");
                case InputActions.CameraMoveRight: return Loc.T("moving the camera right");
                case InputActions.CameraMoveFast: return Loc.T("moving the camera fast");
                case InputActions.CameraZoomOut: return Loc.T("zooming out");
                case InputActions.CameraZoomIn: return Loc.T("zooming in");
                case InputActions.CameraRecenter: return Loc.T("centring the camera");
                case InputActions.SetSpeedControlUI1: return Loc.T("pause");
                case InputActions.SetSpeedControlUI2: return Loc.T("normal speed");
                case InputActions.SetSpeedControlUI3: return Loc.T("fast speed");
                case InputActions.SetSpeedControlUI4: return Loc.T("fastest speed");
                case InputActions.ToggleUIDisplay: return Loc.T("hiding the interface");
                case InputActions.ChopShortcut: return Loc.T("chopping the selected trees");
                case InputActions.EscapeButtonBehavior: return Loc.T("the pause menu and closing windows");
                case InputActions.ToggleDecreeUI: return Loc.T("job priority");
                case InputActions.RotateBuilding: return Loc.T("rotating a building");
                case InputActions.DeleteBuilding: return Loc.T("demolishing");
                case InputActions.ChangeMusic: return Loc.T("changing the music");
                default: return TextUtil.Humanize(a.ToString()).ToLowerInvariant();
            }
        }

        // ------------------------------------------------------------------ reset

        private void ResetAll()
        {
            if (Time.unscaledTime > resetArmedUntil)
            {
                resetArmedUntil = Time.unscaledTime + 5f;
                A.Say(Loc.T("Press Enter again within 5 seconds to reset all mod settings and keys"), force: true);
                return;
            }
            resetArmedUntil = -1f;
            foreach (var kv in Plugin.ConfigRef)
            {
                var entry = kv.Value;
                if (entry.Definition.Key == "CommandFile") continue; // developer switch, leave alone
                entry.BoxedValue = entry.DefaultValue;
            }
            Plugin.Keys.Reset();
            Plugin.ConfigRef.Save();
            ModLanguage.Apply(announce: false);
            A.Cue(Cue.Placed);
            A.Say(Loc.T("All mod settings and keys are back to their defaults"), force: true);
        }

        protected override void OnClosed()
        {
            Plugin.ConfigRef.Save();
            if (fromMap) MapController.Inst.OnReturnToMap();
            else AccessController.Inst.Nav.SpeakCurrent();
        }
    }
}
