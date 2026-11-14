using System.Collections.Generic;
using KCAccess.Core;
using KCAccess.UI;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>In-game windows: modal ones take focus automatically, panel groups are cycled with F6.</summary>
    internal static class GameScreens
    {
        /// <summary>Returns the topmost modal window that is open during play, or null.</summary>
        internal static ScreenInfo Detect()
        {
            if (GameState.inst == null || !GameState.inst.IsPlayMode()) return null;
            var ui = GameUI.inst;
            if (ui == null) return null;

            // Order matters: the first match is the one on top.
            ScreenInfo s;
            if (Is(ui.demolishWarningUI, out s, "DemolishWarning", Loc.T("Demolish warning"))) return s;
            if (Is(ui.levelUpUI, out s, "LevelUp", Loc.T("Your town grew"))) return s;
            if (EffectBanner.inst != null && EffectBanner.inst.Showing() && Is(EffectBanner.inst, out s, "Banner", Loc.T("Announcement")))
            {
                string winner = Twitch.BannerTitle(); // a Twitch vote result: say who won first
                if (winner != null) s.Title = winner;
                return s;
            }
            if (ui.survivalIntro != null && Is(ui.survivalIntro, out s, "SurvivalIntro", Loc.T("Survival mode"))) return s;
            if (ui.survivalSuccess != null && Is(ui.survivalSuccess, out s, "SurvivalSuccess", Loc.T("Survival success"))) return s;
            // AdvisorUI.IsVisible() only checks the outer object, which stays active after Hide(); the real window is containerRect.
            if (AdvisorUI.inst != null && AdvisorUI.inst.containerRect != null && UIText.IsVisible(AdvisorUI.inst.containerRect.gameObject))
            {
                return new ScreenInfo { Id = "Advisor", Title = Loc.T("Advisors"), Root = AdvisorUI.inst.containerRect, Modal = true };
            }
            // The witch's dialogs (asleep at the door, a confrontation) cover her window: only they can be used, and the
            // mission behind them (with its Refuse button) must not be reached.
            var witch = ui.witchUI;
            if (witch != null && UIText.IsVisible(witch.gameObject) && witch.dialogContainer != null && witch.dialogContainer.activeInHierarchy && HasContent(witch.dialogContainer.transform))
                return new ScreenInfo { Id = "WitchDialog", Title = Loc.T("Witch hut"), Root = witch.dialogContainer.transform, Modal = true, ReadAllOnOpen = true };
            if (Is(witch, out s, "Witch", Loc.T("Witch hut")))
            {
                // Start on what she asks for, not on Refuse.
                s.InitialFocus = witch.sendContainer != null && witch.sendContainer.activeInHierarchy ? "missiondesc" : "";
                return s;
            }
            if (ui.decreeUI != null && ui.decreeUI.Visible && Is(ui.decreeUI, out s, "Decrees", Loc.T("Decrees"))) return s;
            if (ui.diplomacyUI != null && ui.diplomacyUI.Visible() && Is(ui.diplomacyUI, out s, "Diplomacy", Loc.T("Diplomacy"))) return s;
            if (Is(ui.researchUI, out s, "Research", Loc.T("Research"))) return s;
            if (ui.generalLargeUI != null && Is(ui.generalLargeUI, out s, "General", Loc.T("General"))) return s;
            // Confirmations shown on top of anything in play mode.
            foreach (var c in Confirmations())
            {
                if (c != null && UIText.IsVisible(c.gameObject)) return new ScreenInfo { Id = "Confirm", Title = ScreenDetector.ConfirmTitle(c), Root = c.transform, Modal = true, InitialFocus = ScreenDetector.SafeButton(c) };
            }
            return null;
        }

        private static Assets.Code.UI.Confirmation[] confirmations;

        /// <summary>
        /// The game's confirmation windows, shown or hidden. They are a dozen objects that stay in the scene. Searching all
        /// the game's objects for them 10 times a second took a third of the frame time on a large map on a slow PC, and
        /// one search every 30 seconds still held an Extra Large map for a fifth of a second each time. So the list is
        /// searched once, again when a new game replaced the windows, and when one is shown that is not in it (the game
        /// shows every confirmation through ShowConfirmation, see ConfirmationShown).
        /// </summary>
        private static Assets.Code.UI.Confirmation[] Confirmations()
        {
            bool gone = confirmations == null;
            for (int i = 0; !gone && i < confirmations.Length; i++) gone = confirmations[i] == null; // a new game replaced them
            if (gone) confirmations = FindConfirmations();
            return confirmations;
        }

        /// <summary>The search itself (debug command findcost times it).</summary>
        internal static Assets.Code.UI.Confirmation[] FindConfirmations() => Resources.FindObjectsOfTypeAll<Assets.Code.UI.Confirmation>();

        /// <summary>A window that was made after the last search is found as soon as it is shown.</summary>
        internal static void ConfirmationShown(Assets.Code.UI.Confirmation shown)
        {
            if (confirmations != null && System.Array.IndexOf(confirmations, shown) < 0) confirmations = null;
        }

        private static bool Is(Component c, out ScreenInfo info, string id, string title)
        {
            info = null;
            if (c == null || !UIText.IsVisible(c.gameObject) || !HasContent(c.transform)) return false;
            info = new ScreenInfo { Id = id, Title = WindowTitle(c.transform) ?? title, Root = c.transform, Modal = true,
                ReadAllOnOpen = id == "LevelUp" || id == "Banner" || id == "SurvivalIntro" || id == "SurvivalSuccess" || id == "DemolishWarning" };
            return true;
        }

        /// <summary>
        /// Safety net: a "window" whose object is active but shows nothing (hidden children, closed sub-panel)
        /// must not capture the keyboard.
        /// </summary>
        internal static bool HasContent(Transform root)
        {
            foreach (var sel in root.GetComponentsInChildren<UnityEngine.UI.Selectable>(false))
            {
                if (sel.enabled && UIText.IsVisible(sel.gameObject)) return true;
            }
            return UIText.VisibleTexts(root).Count > 0;
        }

        /// <summary>The window's own heading: the first visible text on an object called "Title".</summary>
        internal static string WindowTitle(Transform root)
        {
            foreach (var t in UIText.VisibleTexts(root))
            {
                string n = t.gameObject.name;
                if (!n.Equals("Title", System.StringComparison.OrdinalIgnoreCase) && !n.Equals("Header", System.StringComparison.OrdinalIgnoreCase) && !n.Equals("TitleText", System.StringComparison.OrdinalIgnoreCase)) continue;
                string s = KCAccess.Core.TextUtil.Clean(UIText.TextOf(t));
                if (s.Length > 0 && s.Length < 60) return s;
            }
            return null;
        }

        private static GameObject headerCanvas;
        private static float nextHeaderSearch;

        /// <summary>Non-modal panel groups available right now, in F6 order.</summary>
        internal static List<ScreenInfo> PanelGroups()
        {
            var groups = new List<ScreenInfo>();
            var ui = GameUI.inst;
            if (ui == null) return groups;

            var selection = new List<Transform>();
            AddIfVisible(selection, ui.workerUI);
            AddIfVisible(selection, ui.constructUI);
            AddIfVisible(selection, ui.tileInfoUI);
            // The villager panel's root has no size of its own, so the on-screen test cannot be used for it.
            if (ui.personUI != null && ui.personUI.Visible && ui.personUI.villager != null) selection.Add(ui.personUI.transform);
            AddIfVisible(selection, ui.shipUI);
            AddIfVisible(selection, ui.shipLogisticsUI);
            AddIfVisible(selection, ui.merchantUI);
            AddIfVisible(selection, ui.unitUI);
            AddIfVisible(selection, ui.outputUI);
            AddIfVisible(selection, ui.resourceFilterUI);
            AddIfVisible(selection, ui.resourceOrderFormUI);
            AddIfVisible(selection, ui.destructionCrewUI);
            AddIfVisible(selection, ui.dockUI);
            AddIfVisible(selection, ui.blacksmithUI);
            AddIfVisible(selection, ui.barracksUI);
            AddIfVisible(selection, ui.foreignMinistryUI);
            AddIfVisible(selection, ui.chamberOfWarUI);
            AddIfVisible(selection, ui.rubbleUI);
            AddIfVisible(selection, ui.keepUI);
            AddIfVisible(selection, ui.slabUI);
            AddIfVisible(selection, ui.catapultUI);
            AddIfVisible(selection, ui.militaryShipUI);
            AddIfVisible(selection, ui.seedShipUI);
            AddIfVisible(selection, ui.envoyUI);
            AddIfVisible(selection, ui.settlersUI);
            AddIfVisible(selection, ui.foreignIslandInfoUI);
            if (selection.Count > 0) groups.Add(Group("Selection", SelectionTitle(), selection));

            var kingdom = new List<Transform>();
            AddIfVisible(kingdom, ui.islandInfoUI);
            if (TownNameUI.inst != null) AddIfVisible(kingdom, TownNameUI.inst);
            if (kingdom.Count > 0) groups.Add(Group("Kingdom", Loc.T("Kingdom overview"), kingdom));

            if (ui.creativeModeOptions != null && Player.inst != null && Player.inst.creativeMode)
            {
                var creative = new List<Transform>();
                AddIfVisible(creative, ui.creativeModeOptions.transform);
                if (creative.Count > 0) groups.Add(Group("Creative", Loc.T("Creative mode options"), creative));
            }

            var twitch = Twitch.PanelRoots();
            if (twitch.Count > 0) groups.Add(Group("Twitch", Twitch.SettingsVisible ? Loc.T("Twitch voting") : Loc.T("Twitch vote"), twitch));

            var toolbar = new List<Transform>();
            if (SpeedControlUI.inst != null) AddIfVisible(toolbar, SpeedControlUI.inst);
            if (ui.cursorModeButtonContainer != null) AddIfVisible(toolbar, ui.cursorModeButtonContainer);
            // GameObject.Find walks the whole scene, several times a second on a map with thousands of objects.
            if (headerCanvas == null && Time.unscaledTime >= nextHeaderSearch)
            {
                headerCanvas = GameObject.Find("HeaderUICanvas");
                nextHeaderSearch = Time.unscaledTime + 5f;
            }
            if (headerCanvas != null) AddIfVisible(toolbar, headerCanvas.transform);
            if (toolbar.Count > 0) groups.Add(Group("Toolbar", Loc.T("Toolbar"), toolbar));
            return groups;
        }

        private static ScreenInfo Group(string key, string title, List<Transform> roots) =>
            new ScreenInfo { Id = "Panel", Key = key, Title = title, Root = roots[0], Roots = roots, Modal = false };

        private static string SelectionTitle()
        {
            var ui = GameUI.inst;
            var b = ui.GetBuildingSelected();
            if (b != null) return CellInfo.RubbleOf(b) is string was ? Loc.F("Rubble of {0}", was) : b.FriendlyName;
            // A ship's or cart's route panel is named after the ship; a merchant's window starts with its own title.
            var route = ui.shipLogisticsUI;
            if (route != null && route.Visible && route.shipName != null && TextUtil.HasContent(route.shipName.text)) return TextUtil.Clean(route.shipName.text);
            if (ui.merchantUI != null && ui.merchantUI.Visible) return Loc.T("Trade window");
            if (ui.personUI != null && ui.personUI.Visible) return Loc.T("Villager");
            if (ui.unitUI != null && ui.unitUI.Visible) return Loc.T("Army");
            if (ui.GetCellSelected() != null) return Loc.T("Tile");
            return Loc.T("Selection");
        }

        private static void AddIfVisible(List<Transform> list, Component c)
        {
            if (c != null && UIText.IsVisible(c.gameObject) && UIText.IsOnScreen(c.transform)) list.Add(c.transform);
        }
    }
}
