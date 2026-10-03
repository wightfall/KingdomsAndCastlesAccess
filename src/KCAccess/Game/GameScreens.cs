using System.Collections.Generic;
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
            if (Is(ui.demolishWarningUI, out s, "DemolishWarning", "Demolish warning")) return s;
            if (Is(ui.levelUpUI, out s, "LevelUp", "Your town grew")) return s;
            if (EffectBanner.inst != null && EffectBanner.inst.Showing() && Is(EffectBanner.inst, out s, "Banner", "Announcement")) return s;
            if (ui.survivalIntro != null && Is(ui.survivalIntro, out s, "SurvivalIntro", "Survival mode")) return s;
            if (ui.survivalSuccess != null && Is(ui.survivalSuccess, out s, "SurvivalSuccess", "Survival success")) return s;
            // AdvisorUI.IsVisible() only checks the outer object, which stays active after Hide(); the real window is containerRect.
            if (AdvisorUI.inst != null && AdvisorUI.inst.containerRect != null && UIText.IsVisible(AdvisorUI.inst.containerRect.gameObject))
            {
                return new ScreenInfo { Id = "Advisor", Title = "Advisors", Root = AdvisorUI.inst.containerRect, Modal = true };
            }
            if (Is(ui.witchUI, out s, "Witch", "Witch hut")) return s;
            if (ui.decreeUI != null && ui.decreeUI.Visible && Is(ui.decreeUI, out s, "Decrees", "Decrees")) return s;
            if (ui.diplomacyUI != null && ui.diplomacyUI.Visible() && Is(ui.diplomacyUI, out s, "Diplomacy", "Diplomacy")) return s;
            if (Is(ui.researchUI, out s, "Research", "Research")) return s;
            if (ui.generalLargeUI != null && Is(ui.generalLargeUI, out s, "General", "General")) return s;
            // Confirmations shown on top of anything in play mode.
            foreach (var c in Object.FindObjectsOfType<Assets.Code.UI.Confirmation>())
            {
                if (UIText.IsVisible(c.gameObject)) return new ScreenInfo { Id = "Confirm", Title = ScreenDetector.ConfirmTitle(c), Root = c.transform, Modal = true };
            }
            return null;
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
            AddIfVisible(selection, ui.personUI);
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
            if (kingdom.Count > 0) groups.Add(Group("Kingdom", "Kingdom overview", kingdom));

            if (ui.creativeModeOptions != null && Player.inst != null && Player.inst.creativeMode)
            {
                var creative = new List<Transform>();
                AddIfVisible(creative, ui.creativeModeOptions.transform);
                if (creative.Count > 0) groups.Add(Group("Creative", "Creative mode options", creative));
            }

            var toolbar = new List<Transform>();
            if (SpeedControlUI.inst != null) AddIfVisible(toolbar, SpeedControlUI.inst);
            if (ui.cursorModeButtonContainer != null) AddIfVisible(toolbar, ui.cursorModeButtonContainer);
            var header = GameObject.Find("HeaderUICanvas");
            if (header != null) AddIfVisible(toolbar, header.transform);
            if (toolbar.Count > 0) groups.Add(Group("Toolbar", "Toolbar", toolbar));
            return groups;
        }

        private static ScreenInfo Group(string key, string title, List<Transform> roots) =>
            new ScreenInfo { Id = "Panel", Key = key, Title = title, Root = roots[0], Roots = roots, Modal = false };

        private static string SelectionTitle()
        {
            var ui = GameUI.inst;
            var b = ui.GetBuildingSelected();
            if (b != null) return b.FriendlyName;
            if (ui.personUI != null && ui.personUI.Visible) return "Villager";
            if (ui.unitUI != null && ui.unitUI.Visible) return "Army";
            if (ui.GetCellSelected() != null) return "Tile";
            return "Selection";
        }

        private static void AddIfVisible(List<Transform> list, Component c)
        {
            if (c != null && UIText.IsVisible(c.gameObject) && UIText.IsOnScreen(c.transform)) list.Add(c.transform);
        }
    }
}
