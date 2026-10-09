using System.Collections.Generic;
using Assets.Code.UI;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.UI
{
    /// <summary>What the player is looking at right now.</summary>
    internal sealed class ScreenInfo
    {
        public string Id;
        public string Title;
        public Transform Root;
        /// <summary>Extra panels navigated together with Root (F6 panel groups).</summary>
        public List<Transform> Roots;
        /// <summary>Stable key for panel groups ("Selection", "Kingdom", "Toolbar").</summary>
        public string Key;
        /// <summary>Modal screens take all keys; non-modal panels need F6 to get focus.</summary>
        public bool Modal = true;
        public bool TypeAhead;
        public string Intro;
        /// <summary>Read every text of the screen when it opens (messages such as defeat or level-up).</summary>
        public bool ReadAllOnOpen;
        /// <summary>Name of the GameObject to focus first.</summary>
        public string InitialFocus;

        public bool SameAs(ScreenInfo other) => other != null && other.Id == Id && other.Root == Root;
    }

    /// <summary>Works out the current menu screen from the game's own state machines.</summary>
    internal static class ScreenDetector
    {
        private static readonly Dictionary<MainMenuMode.State, string> Titles = new Dictionary<MainMenuMode.State, string>
        {
            { MainMenuMode.State.Menu, Loc.N("Main menu") },
            { MainMenuMode.State.ChooseMode, Loc.N("Choose game mode") },
            { MainMenuMode.State.ChooseDifficulty, Loc.N("Choose difficulty") },
            { MainMenuMode.State.NewMap, Loc.N("Map setup") },
            { MainMenuMode.State.NameAndBanner, Loc.N("Kingdom name and banner") },
            { MainMenuMode.State.PauseMenu, Loc.N("Paused") },
            { MainMenuMode.State.SettingsMenu, Loc.N("Settings") },
            { MainMenuMode.State.Save, Loc.N("Save game") },
            { MainMenuMode.State.Load, Loc.N("Load game") },
            { MainMenuMode.State.QuitConfirm, Loc.N("Return to main menu") },
            { MainMenuMode.State.ExitConfirm, Loc.N("Exit game") },
            { MainMenuMode.State.LoadError, Loc.N("Load error") },
            { MainMenuMode.State.SendSave, Loc.N("Send save") },
            { MainMenuMode.State.Credits, Loc.N("Credits") },
            { MainMenuMode.State.Failure, Loc.N("Overthrown") },
            { MainMenuMode.State.KeepDestroyed, Loc.N("Keep destroyed") },
            { MainMenuMode.State.BannerSelect, Loc.N("Choose banner") },
            { MainMenuMode.State.GameWorkshopUI, Loc.N("Mods") },
            { MainMenuMode.State.RivalChoiceUI, Loc.N("Rival kingdoms") },
            { MainMenuMode.State.KingdomShareFromMenu, Loc.N("Kingdom share") },
            { MainMenuMode.State.KingdomShareFromGame, Loc.N("Kingdom share") },
        };

        internal static ScreenInfo DetectMainMenu()
        {
            var mm = GameState.inst != null ? GameState.inst.mainMenuMode : null;
            if (mm == null || !GameState.inst.IsMainMenuMode()) return null;
            var state = mm.GetState();
            GameObject root = RootFor(mm, state);
            string id = state.ToString();
            if (state == MainMenuMode.State.KingdomShareFromGame || state == MainMenuMode.State.KingdomShareFromMenu) id = "KingdomShare";
            Titles.TryGetValue(state, out var title);
            var info = new ScreenInfo { Id = id, Title = title != null ? Loc.T(title) : TextUtil.Humanize(id), Root = root != null ? root.transform : null, TypeAhead = true };
            // A confirmation popped up inside the screen (load / save / delete confirmations, bad map warning).
            var confirm = FindActiveConfirmation(info.Root);
            if (confirm == null && mm.confirm != null && mm.confirm.gameObject.activeInHierarchy) confirm = mm.confirm;
            if (state == MainMenuMode.State.Menu) info.InitialFocus = "New";
            if (state == MainMenuMode.State.Failure || state == MainMenuMode.State.KeepDestroyed || state == MainMenuMode.State.LoadError || state == MainMenuMode.State.ChooseDifficulty || state == MainMenuMode.State.ChooseMode) info.ReadAllOnOpen = true;
            if (confirm != null && confirm.transform != info.Root)
            {
                info = new ScreenInfo { Id = "Confirm", Title = ConfirmTitle(confirm), Root = confirm.transform, TypeAhead = true };
            }
            if (info.Root == null) return null;
            return info;
        }

        private static GameObject RootFor(MainMenuMode mm, MainMenuMode.State state)
        {
            switch (state)
            {
                case MainMenuMode.State.Menu: return mm.topLevelUI;
                case MainMenuMode.State.ChooseMode: return mm.chooseModeUI;
                case MainMenuMode.State.ChooseDifficulty: return mm.chooseDifficultyUI;
                case MainMenuMode.State.NewMap: return mm.newMapUI;
                case MainMenuMode.State.NameAndBanner: return mm.nameBannerUI;
                case MainMenuMode.State.PauseMenu: return mm.pauseMenuUI;
                case MainMenuMode.State.SettingsMenu: return mm.settingsUI;
                case MainMenuMode.State.Save:
                case MainMenuMode.State.Load: return mm.saveLoadUI != null ? mm.saveLoadUI.gameObject : null;
                case MainMenuMode.State.QuitConfirm:
                case MainMenuMode.State.ExitConfirm: return mm.QuitConfirmation != null ? mm.QuitConfirmation.gameObject : null;
                case MainMenuMode.State.LoadError: return mm.LoadErrorSendSave != null ? mm.LoadErrorSendSave.gameObject : null;
                case MainMenuMode.State.SendSave: return mm.SendSaveUI != null ? mm.SendSaveUI.gameObject : null;
                case MainMenuMode.State.Credits: return mm.creditsUI;
                case MainMenuMode.State.Failure:
                case MainMenuMode.State.KeepDestroyed: return mm.failureUI;
                case MainMenuMode.State.BannerSelect: return mm.chooseBannerUI;
                case MainMenuMode.State.GameWorkshopUI: return mm.gameWorkshopUI;
                case MainMenuMode.State.RivalChoiceUI: return mm.rivalSettingsUI != null ? mm.rivalSettingsUI.gameObject : null;
                case MainMenuMode.State.KingdomShareFromMenu:
                case MainMenuMode.State.KingdomShareFromGame: return mm.kingdomShareUI != null ? mm.kingdomShareUI.gameObject : null;
            }
            return null;
        }

        internal static Confirmation FindActiveConfirmation(Transform root)
        {
            if (root == null) return null;
            foreach (var c in root.GetComponentsInChildren<Confirmation>(false))
            {
                if (c.transform != root && UIText.IsVisible(c.gameObject)) return c;
            }
            return null;
        }

        internal static string ConfirmTitle(Confirmation c)
        {
            // The question is the confirmation's text that is not inside the yes / no buttons.
            var texts = UIText.VisibleTexts(c.transform);
            texts.RemoveAll(t => t.GetComponentInParent<UnityEngine.UI.Selectable>() != null);
            string q = UIText.JoinTexts(texts);
            return string.IsNullOrEmpty(q) ? Loc.T("Confirm") : q;
        }
    }
}
