using System;
using System.Collections.Generic;
using KCAccess.Core;
using KCAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace KCAccess
{
    /// <summary>
    /// Main loop of the mod. Runs before the game's scripts each frame, works out what is on
    /// screen and routes keys to: global keys → mod menus → modal windows → focused panel → map.
    /// </summary>
    [DefaultExecutionOrder(-30000)]
    internal sealed class AccessController : MonoBehaviour
    {
        internal static AccessController Inst;

        internal readonly UINavigator Nav = new UINavigator();

        /// <summary>Modal screen (menus, dialogs) that owns the keyboard, if any.</summary>
        private ScreenInfo modal;

        /// <summary>Non-modal panel group the player moved into with F6, if any.</summary>
        private ScreenInfo panel;

        private string helpId = "Menu";
        private float nextDetect;
        private float nextPanelCheck;
        private bool wasPlaying;

        /// <summary>A mod-owned modal menu (build menu, log browser, status list).</summary>
        internal IModalMenu ActiveMenu;

        internal bool PanelFocus => panel != null;

        private void Awake()
        {
            Inst = this;
        }

        private int tickedFrame = -1;

        // No speech shutdown in OnApplicationQuit: the game cancels Alt+F4 there (World.OnApplicationQuit calls
        // Application.CancelQuit to show its "Exit game" dialog), so closing the screen reader connection left the
        // mod silent for the rest of the session. The connection ends with the process when the game really exits.

        private void Update() => EnsureTick();

        private float lostFocusAt = -1f;

        /// <summary>Coming back with Alt+Tab (from a web page, File Explorer, another program): say where you are.</summary>
        private void OnApplicationFocus(bool focus)
        {
            if (!focus)
            {
                lostFocusAt = Time.unscaledTime;
                return;
            }
            if (lostFocusAt < 0f || Time.unscaledTime - lostFocusAt < 1f || !Plugin.Loaded) return;
            lostFocusAt = -1f;
            string where = modal != null ? modal.Title : panel != null ? panel.Title : (GameState.inst != null && GameState.inst.IsPlayMode() ? Loc.T("the map") : null);
            A.Say(where != null ? Loc.F("Back in the game, {0}", where) : Loc.T("Back in the game"), force: true);
        }

        /// <summary>
        /// Runs the mod once per frame. Also called from a prefix on the game's KeyboardControl.Update so the
        /// mod always sees (and can consume) keys before the game reacts to them, whatever the script order.
        /// </summary>
        internal void EnsureTick()
        {
            if (tickedFrame == Time.frameCount) return;
            tickedFrame = Time.frameCount;
            SteamOverlay.Ensure();
            OsKeyboard.Tick(Nav.IsEditing || SteamOverlay.Active);
            if (!SteamOverlay.Active && !Nav.IsEditing) ControllerInput.Tick(OnMapForPad);
            KInput.BeginFrame();
            Diagnostics.PollCommands();
            if (Plugin.CfgLogKeys.Value) Diagnostics.LogKeys();
            try
            {
                ModLanguage.Tick();
                Tick();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("KCAccess update error: " + e);
            }
        }

        private void Tick()
        {
            if (SteamOverlay.Active) return; // the Steam overlay has the keyboard; keys are not meant for the game
            Patch_Demolish.Flush();
            if (ActiveMenu is Game.ModSettingsMenu ms && ms.Capturing)
            {
                InputGate.BlockGameKeys = true;
                ms.HandleInput();
                return;
            }
            if (Special.CapturingKey)
            {
                InputGate.BlockGameKeys = true;
                Special.UpdateCapture();
                return;
            }
            if (GlobalKeys()) return;
            if (GameState.inst == null || World.inst == null) return;
            KeyboardGuard.Tick(Nav, modal == null && panel == null && ActiveMenu == null);

            bool playing = GameState.inst.IsPlayMode();
            if (playing != wasPlaying)
            {
                wasPlaying = playing;
                if (playing) Game.GameEvents.OnEnterPlayMode();
                else
                {
                    panel = null;
                    if (ActiveMenu != null) ActiveMenu = null;
                }
            }
            if (playing)
            {
                Game.GameEvents.Tick();
                Game.Alerts.Tick();
                Game.Problems.Tick();
                Game.Twitch.Tick();
            }
            Game.StreamStatusFile.Tick(playing);

            if (ActiveMenu != null)
            {
                if (!ActiveMenu.IsOpen) ActiveMenu = null;
                else
                {
                    helpId = ActiveMenu.HelpId;
                    InputGate.BlockGameKeys = true;
                    InputGate.EscapePassThrough = false;
                    ActiveMenu.HandleInput();
                    return;
                }
            }
            InputGate.BlockGameKeys = false;
            InputGate.EscapePassThrough = false;

            DetectModal();
            // Exploring the map belongs to the map setup screen only: any other screen or a confirmation on top of it
            // (back to the main menu, an error) must get the keyboard, not the map cursor.
            if (!playing && Game.MapController.Inst.MenuMapMode && (modal == null || modal.Id != "NewMap"))
                Game.MapController.Inst.MenuMapMode = false;
            if (modal != null)
            {
                helpId = modal.Id;
                if (playing)
                {
                    InputGate.BlockGameKeys = true;
                    InputGate.EscapePassThrough = true;
                }
                if (Nav.IsEditing)
                {
                    InputGate.BlockGameKeys = true;
                    Nav.HandleInput();
                    return;
                }
                if (!playing && modal.Id == "NewMap" && KInput.Down(KeyCode.M) && KInput.Ctrl && !Game.MapController.Inst.MenuMapMode)
                {
                    Game.MapController.Inst.MenuMapMode = true;
                    Game.MapController.Inst.CenterOnStart();
                    A.Cue(Cue.Open);
                    A.Say(KCAccess.Core.KeyHelp.Resolve(Loc.T("Exploring the map. Arrow keys move, {TileInfo} describes a tile, {PrevCategory} and {NextCategory} choose a scan category, {PrevItem} and {NextItem} choose a target, {Walk} walks to it, {JumpTarget} jumps there, {Beacon} turns on a sound beacon. Control M or Escape returns to the menu.")));
                    return;
                }
                bool mapInMenu = !playing && Game.MapController.Inst.MenuMapMode;
                if (!mapInMenu)
                {
                    HandleNavigatorKeys();
                    return;
                }
            }
            else if (panel != null)
            {
                // A toolbar button started a cursor mode or building placement: hand the keyboard back to the map.
                if (Game.MapController.Inst.TrackWhileInPanel())
                {
                    LeavePanel(announce: false);
                    A.Cue(Cue.Close);
                    return;
                }
                UpdatePanel();
                if (panel != null)
                {
                    helpId = "Panel";
                    if (Nav.IsEditing)
                    {
                        InputGate.BlockGameKeys = true;
                        Nav.HandleInput();
                        return;
                    }
                    InputGate.BlockGameKeys = true;
                    HandleNavigatorKeys();
                    return;
                }
            }

            if (playing || Game.MapController.Inst.MenuMapMode)
            {
                Game.MapController.Inst.Tick();
                helpId = Game.MapController.Inst.HelpId;
            }
        }

        private void HandleNavigatorKeys()
        {
            if (KInput.Down(KeyCode.R) && KInput.Ctrl && !KInput.Shift)
            {
                Nav.ReadAll();
                return;
            }
            if (KInput.Plain(KeyCode.F5))
            {
                Nav.SpeakCurrent();
                return;
            }
            if (panel != null && KInput.Down(KeyCode.F6) && !KInput.Ctrl && !KInput.Alt)
            {
                CyclePanel(KInput.Shift ? -1 : 1);
                return;
            }
            if (KInput.Plain(KeyCode.Escape))
            {
                if (panel != null)
                {
                    KInput.Consume(KeyCode.Escape);
                    if (panel.Key == "Twitch") Game.Twitch.Hide(); // the settings slide over the map; Escape closes them too
                    LeavePanel(announce: true);
                    return;
                }
                if (TryGoBack()) return;
            }
            Nav.HandleInput();
        }

        // ---------------------------------------------------------------- modal screens

        /// <summary>
        /// Ctrl+Shift+F10: if the mod ever goes silent, forget every window, panel, menu, edit and key capture it
        /// thinks is open, give the keyboard back to the game and announce the current screen again.
        /// </summary>
        private void ResetAccessibility()
        {
            Plugin.Log.LogInfo("[reset] before: " + Diagnostics.KeyboardReport() + ", speech: " + A.BackendName);
            if (A.BackendName == "log only") Plugin.Log.LogInfo("[reset] screen reader reconnected: " + A.Redetect());
            Special.ResetCapture();
            Nav.EndEdit(false);
            ActiveMenu = null;
            panel = null;
            modal = null;
            Nav.Clear();
            nextDetect = 0f;
            InputGate.BlockGameKeys = false;
            InputGate.EscapePassThrough = false;
            if (GameState.inst != null) GameState.inst.AlphaNumericHotkeysEnabled = true;
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null) es.SetSelectedGameObject(null);
            A.Cue(Cue.Open);
            DetectModal();
            if (modal == null)
            {
                bool play = GameState.inst != null && GameState.inst.IsPlayMode();
                A.Say(play ? Loc.T("Accessibility reset. Back on the map.") : Loc.T("Accessibility reset."), force: true);
                if (play) Game.MapController.Inst.OnReturnToMap();
            }
        }

        private void DetectModal()
        {
            // With nothing open the search ran every frame on the map (GameScreens.Detect walks every game window and
            // calls FindObjectsOfType for confirmations): look 10 times a second instead.
            bool modalOk = modal == null || (modal.Root != null && modal.Root.gameObject.activeInHierarchy && Nav.Count > 0);
            if (Time.unscaledTime < nextDetect && modalOk) return;
            nextDetect = Time.unscaledTime + 0.1f;
            ScreenInfo next = ScreenDetector.DetectMainMenu() ?? Game.GameScreens.Detect();
            if (next == null)
            {
                if (modal != null)
                {
                    modal = null;
                    Nav.Clear();
                    if (GameState.inst.IsPlayMode())
                    {
                        if (panel != null) RestorePanel();
                        else Game.MapController.Inst.OnReturnToMap();
                    }
                }
                return;
            }
            if (next.SameAs(modal))
            {
                modal.Title = next.Title; // the mod language may have changed since the screen opened
                if (Nav.Root == null) Nav.SetRoot(next.Root, next.Title, announce: false); // recover a cleared navigator
                return;
            }
            modal = next;
            panel = null; // a dialog replaces any panel focus (the panel usually closes behind it)
            Nav.TypeAheadEnabled = next.TypeAhead;
            A.Cue(Cue.Open);
            Nav.SetRoot(next.Root, next.Title, announce: !next.ReadAllOnOpen, next.Intro, next.InitialFocus);
            if (next.ReadAllOnOpen) Nav.ReadAll();
        }

        /// <summary>Escape on menus without their own Escape handling: press a Back / Close / No button.</summary>
        private bool TryGoBack()
        {
            if (modal == null || modal.Root == null) return false;
            if (GameState.inst.IsMainMenuMode())
            {
                var st = GameState.inst.mainMenuMode.GetState();
                if (modal.Id != "Confirm" && (st == MainMenuMode.State.PauseMenu || st == MainMenuMode.State.SettingsMenu)) return false; // game handles Escape
                if (modal.Id == "Menu") return false;
            }
            Button best = null;
            int bestScore = 0;
            foreach (var b in modal.Root.GetComponentsInChildren<Button>(false))
            {
                if (!b.interactable || !UIText.IsVisible(b.gameObject)) continue;
                int score = BackScore(b);
                if (score > bestScore)
                {
                    best = b;
                    bestScore = score;
                }
            }
            if (best == null) return false;
            KInput.Consume(KeyCode.Escape);
            A.Cue(Cue.Close);
            UINavigator.Click(best.gameObject);
            return true;
        }

        private static int BackScore(Button b)
        {
            string name = b.gameObject.name.ToLowerInvariant();
            string methods = string.Empty;
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++) methods += b.onClick.GetPersistentMethodName(i).ToLowerInvariant() + " ";
            string label = UIText.LabelOf(b).ToLowerInvariant();
            int score = 0;
            if (methods.Contains("back") || methods.Contains("close") || methods.Contains("cancel") || methods.Contains("returntogame") || methods.Contains("hide") || methods.Contains("dismiss")) score += 3;
            if (name.Contains("back") || name.Contains("close") || name.Contains("cancel") || name == "x" || name.Contains("exit") || name.Contains("dismiss")) score += 2;
            if (label == "back" || label == "close" || label == "cancel" || label == "no" || label == "x" || label == "resume" || label == "ok") score += 2;
            var confirm = b.GetComponentInParent<Assets.Code.UI.Confirmation>();
            if (confirm != null && confirm.noButton == b) score += 4;
            return score;
        }

        // ---------------------------------------------------------------- panels (F6)

        /// <summary>F6 from the map: move focus into the first panel group. Returns false when no panel is open.</summary>
        internal bool FocusPanel(int direction = 1)
        {
            var groups = Game.GameScreens.PanelGroups();
            if (groups.Count == 0) return false;
            int idx = direction >= 0 ? 0 : groups.Count - 1;
            EnterPanel(groups[idx]);
            return true;
        }

        /// <summary>Moves focus into the panel group with this key (e.g. "Twitch"). False when it is not open.</summary>
        internal bool FocusPanelGroup(string key)
        {
            var g = Game.GameScreens.PanelGroups().Find(x => x.Key == key);
            if (g == null) return false;
            EnterPanel(g);
            return true;
        }

        private void CyclePanel(int direction)
        {
            var groups = Game.GameScreens.PanelGroups();
            int idx = groups.FindIndex(g => g.Key == panel.Key);
            int next = idx + direction;
            if (next < 0 || next >= groups.Count)
            {
                LeavePanel(announce: true);
                return;
            }
            EnterPanel(groups[next]);
        }

        private void EnterPanel(ScreenInfo group)
        {
            panel = group;
            A.Cue(Cue.Open);
            Nav.TypeAheadEnabled = false;
            Nav.SetRoots(group.Roots, group.Title, announce: true, initialFocus: group.Key == "Selection" ? "" : null);
        }

        private void RestorePanel()
        {
            var groups = Game.GameScreens.PanelGroups();
            var g = groups.Find(x => x.Key == panel.Key);
            if (g == null)
            {
                LeavePanel(announce: true);
                return;
            }
            EnterPanel(g);
        }

        /// <summary>Keeps the focused panel group in sync with what the game shows.</summary>
        private void UpdatePanel()
        {
            // Own timer: DetectModal moves nextDetect forward on its own schedule, so sharing it meant this check
            // never ran and a panel closed by its Close button kept the keyboard (every key silent).
            if (Time.unscaledTime < nextPanelCheck) return;
            nextPanelCheck = Time.unscaledTime + 0.15f;
            var groups = Game.GameScreens.PanelGroups();
            var g = groups.Find(x => x.Key == panel.Key);
            if (g == null)
            {
                LeavePanel(announce: true);
                return;
            }
            bool same = g.Roots.Count == panel.Roots.Count;
            for (int i = 0; same && i < g.Roots.Count; i++) same = g.Roots[i] == panel.Roots[i];
            if (!same)
            {
                bool titleChanged = g.Title != panel.Title;
                panel = g;
                Nav.SetRoots(g.Roots, g.Title, announce: titleChanged);
            }
        }

        internal void LeavePanel(bool announce)
        {
            if (panel == null) return;
            panel = null;
            if (modal != null) return; // never clear an open dialog's items
            Nav.Clear();
            if (announce)
            {
                A.Cue(Cue.Close);
                Game.MapController.Inst.OnReturnToMap();
            }
        }

        internal ScreenInfo CurrentScreen => modal ?? panel;

        // ---------------------------------------------------------------- global keys & help

        /// <summary>The map has the keyboard (playing, or exploring from the map setup screen).</summary>
        internal bool OnMapForPad => OnMapNow || (Game.MapController.Inst != null && Game.MapController.Inst.MenuMapMode && ActiveMenu == null);

        private bool OnMapNow => modal == null && panel == null && ActiveMenu == null && GameState.inst != null && GameState.inst.IsPlayMode();

        private bool GlobalKeys()
        {
            if (KInput.Down(KeyCode.F12) && KInput.Ctrl && KInput.Shift)
            {
                Diagnostics.DumpUI(true);
                return true;
            }
            if (KInput.Pressed("KeyList"))
            {
                KInput.Consume(KeyCode.F1);
                if (!(ActiveMenu is Game.KeysMenu)) ActiveMenu = new Game.KeysMenu(OnMapNow);
                return true;
            }
            if (KInput.Pressed("Settings"))
            {
                if (!(ActiveMenu is Game.ModSettingsMenu)) ActiveMenu = new Game.ModSettingsMenu(OnMapNow);
                return true;
            }
            if (KInput.Plain(KeyCode.F1))
            {
                KInput.Consume(KeyCode.F1);
                A.Say(ContextHelp(), force: true);
                return true;
            }
            if (KInput.Pressed("Reset"))
            {
                ResetAccessibility();
                return true;
            }
            if (KInput.Pressed("Report"))
            {
                string report = Diagnostics.KeyboardReport();
                Plugin.Log.LogInfo("[report] " + report);
                A.Say(report + ". " + Loc.T("Written to the BepInEx log."), force: true);
                return true;
            }
            if (KInput.Pressed("Redetect"))
            {
                bool ok = A.Redetect();
                A.Say(ok ? Loc.F("Speech: {0}", A.BackendName) : Loc.T("No screen reader found, using log only"), force: true);
                return true;
            }
            if (KInput.Pressed("Mute"))
            {
                Plugin.CfgCues.Value = !Plugin.CfgCues.Value;
                A.Say(Plugin.CfgCues.Value ? Loc.T("Sound cues on") : Loc.T("Sound cues off"), force: true);
                return true;
            }
            return false;
        }

        private string ContextHelp()
        {
            if (ActiveMenu != null) return HelpText.For(ActiveMenu.HelpId);
            bool playing = GameState.inst != null && GameState.inst.IsPlayMode();
            if (modal != null && !Game.MapController.Inst.MenuMapMode)
            {
                string id = HelpText.Has(modal.Id) ? modal.Id : "Dialog";
                string desc = HelpText.For(id);
                string title = desc.StartsWith(modal.Title ?? " ") ? null : modal.Title; // "Settings. Settings." otherwise
                var features = Nav.Features(false, playing);
                features.CanGoBack = modal.Id != "Menu";
                return TextUtil.Sentences(title, desc, Special.Help(Nav.Current), KeyHelp.NavKeys(features));
            }
            if (panel != null)
            {
                string pid = HelpText.Has("Panel." + panel.Key) ? "Panel." + panel.Key : "Panel";
                return TextUtil.Sentences(panel.Title, HelpText.For(pid), Special.Help(Nav.Current), KeyHelp.NavKeys(Nav.Features(true, playing)));
            }
            if (playing || Game.MapController.Inst.MenuMapMode) return KeyHelp.MapHelp(Game.MapController.Inst.State());
            return HelpText.For(helpId);
        }
    }

    /// <summary>A menu owned by the mod that takes all keys while open.</summary>
    internal interface IModalMenu
    {
        bool IsOpen { get; }
        string HelpId { get; }
        void HandleInput();
    }
}
