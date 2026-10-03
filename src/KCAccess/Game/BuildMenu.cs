using System.Collections.Generic;
using Assets.Code;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// B: the build menu as a two-level list. Left/Right = category, Up/Down = building,
    /// Enter = pick up for placement, I = full description. Mirrors the game's own build buttons.
    /// </summary>
    internal sealed class BuildMenu : IModalMenu
    {
        private static readonly Dictionary<string, string> CategoryNames = new Dictionary<string, string>
        {
            { "Castle", "Castle" }, { "Town", "Town" }, { "AdvTown", "Advanced town" }, { "Food", "Food" },
            { "Industry", "Industry" }, { "Maritime", "Maritime" }, { "Cemetery", "Cemeteries" }, { "Statue", "Statues" }, { "Park", "Parks" }
        };

        private static string lastCategory = "Castle";
        private static readonly Dictionary<string, int> lastIndex = new Dictionary<string, int>();

        private readonly List<BuildTab> tabs = new List<BuildTab>();
        private readonly NavList<BuildingCostUpdater> items;
        private readonly TypeAhead typeAhead = new TypeAhead(() => Time.realtimeSinceStartup);
        private int tabIndex;
        private bool open = true;
        private bool started;
        private int waitFrames;

        public BuildMenu()
        {
            items = new NavList<BuildingCostUpdater>(b => b.prefab != null ? b.prefab.FriendlyName : "?", wrap: false);
        }

        public bool IsOpen => open;

        public string HelpId => "BuildMenu";

        private BuildTab CurrentTab => tabIndex >= 0 && tabIndex < tabs.Count ? tabs[tabIndex] : null;

        private static bool IsSubTab(string title) => title == "Cemetery" || title == "Statue" || title == "Park";

        private void Start()
        {
            started = true;
            tabs.Clear();
            foreach (var t in BuildUI.inst.tabs) tabs.Add(t);
            if (tabs.Count == 0)
            {
                A.Say("Build menu is not available");
                open = false;
                return;
            }
            int idx = tabs.FindIndex(t => t.title == lastCategory);
            OpenTab(idx < 0 ? 0 : idx, announceCategory: true, intro: "Build menu. ");
        }

        private void OpenTab(int index, bool announceCategory, string intro = "")
        {
            tabIndex = Mathf.Clamp(index, 0, tabs.Count - 1);
            var tab = CurrentTab;
            lastCategory = tab.title;
            // Show the game's own build panel too, so sighted helpers can follow along.
            try
            {
                if (GameUI.inst.CurrPlacementMode.IsPlacing()) GameUI.inst.AbortCursorObjPlacement();
                BuildUI.inst.SetVisible(true);
                BuildUI.inst.SetTabActive(tab);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Could not show build tab: " + e.Message);
            }
            var list = new List<BuildingCostUpdater>();
            foreach (var b in tab.buttons)
            {
                if (b == null || b.prefab == null || !b.gameObject.activeSelf) continue;
                list.Add(b);
            }
            items.SetItems(list, keepFocus: false);
            if (lastIndex.TryGetValue(tab.title, out int li)) items.SelectIndex(li);
            string catName = CategoryName(tab);
            bool locked = tab.buildButton != null && !tab.buildButton.unlocked;
            lockedCategory = locked;
            string text;
            if (locked)
            {
                text = intro + catName + ", locked. " + LockReason(tab);
            }
            else if (items.Count == 0) text = intro + catName + ", empty";
            else text = intro + (announceCategory ? catName + ", " + TextUtil.Plural(items.Count, "building") + ". " : string.Empty) + Describe(items.Current, brief: true);
            A.Say(text);
        }

        private static string CategoryName(BuildTab tab) => CategoryNames.TryGetValue(tab.title, out var n) ? n : TextUtil.Humanize(tab.title);

        private static string LockReason(BuildTab tab)
        {
            if (Player.inst.FocusedLandMass == -1) return "Build your keep first.";
            var tip = tab.buildButton != null ? tab.buildButton.preqTipMessage : null;
            if (tip != null && !string.IsNullOrEmpty(tip.toolTipText) && tip.toolTipText != "missing tip") return TextUtil.Clean(tip.toolTipText);
            return "Requires other buildings first.";
        }

        public void HandleInput()
        {
            if (!started)
            {
                if (waitFrames == 0 && MapController.Inst.EnsureCameraOnStartLand())
                {
                    waitFrames = 3; // let the game notice the new focused land before listing buildings
                    return;
                }
                if (waitFrames > 1)
                {
                    waitFrames--;
                    return;
                }
                A.Cue(Cue.Open);
                Start();
                return;
            }
            if (KInput.Plain(KeyCode.Escape) || KInput.Plain(KeyCode.B))
            {
                KInput.Consume(KeyCode.Escape);
                Close();
                A.Cue(Cue.Close);
                MapController.Inst.OnReturnToMap();
                return;
            }
            if (KInput.Plain(KeyCode.RightArrow) || KInput.Plain(KeyCode.Tab)) StepTab(1);
            else if (KInput.Plain(KeyCode.LeftArrow) || KInput.WithShift(KeyCode.Tab)) StepTab(-1);
            else if (KInput.Plain(KeyCode.DownArrow)) Move(items.Next());
            else if (KInput.Plain(KeyCode.UpArrow)) Move(items.Previous());
            else if (KInput.Plain(KeyCode.Home)) Move(items.First());
            else if (KInput.Plain(KeyCode.End)) Move(items.Last());
            else if (KInput.Plain(KeyCode.I) || KInput.Plain(KeyCode.F5))
            {
                if (items.HasCurrent) A.Say(Describe(items.Current, brief: false), force: true);
            }
            else if (KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.KeypadEnter) || KInput.Plain(KeyCode.Space))
            {
                KInput.Consume(KeyCode.Return);
                KInput.Consume(KeyCode.Space);
                Pick();
            }
            else
            {
                char? c = KInput.LetterDown();
                if (c.HasValue && !KInput.Shift && c.Value != 'i' && c.Value != 'b')
                {
                    if (items.FindNext(typeAhead.Add(c.Value))) Move(NavResult.Moved);
                    else A.Cue(Cue.Edge);
                }
            }
        }

        private void StepTab(int delta)
        {
            // Sub categories (cemeteries, statues, parks) are reached from their entries in Advanced town.
            int i = tabIndex;
            for (int n = 0; n < tabs.Count; n++)
            {
                i = (i + delta + tabs.Count) % tabs.Count;
                if (!IsSubTab(tabs[i].title)) break;
            }
            A.Cue(Cue.Navigate);
            OpenTab(i, announceCategory: true);
        }

        private void Move(NavResult r)
        {
            if (r == NavResult.Empty)
            {
                A.Cue(Cue.Edge);
                A.Say("empty");
                return;
            }
            A.Cue(r == NavResult.HitEdge ? Cue.Edge : Cue.Navigate);
            lastIndex[CurrentTab.title] = items.Index;
            A.Say(Describe(items.Current, brief: true));
        }

        private static bool IsSubMenuEntry(BuildingCostUpdater b, out string tab)
        {
            tab = null;
            if (b.prefab.GetComponent<CemeteryBuildDummy>() != null) tab = "Cemetery";
            else if (b.prefab.GetComponent<StatueBuildDummy>() != null) tab = "Statue";
            else if (b.prefab.UniqueName == World.parkDummyName) tab = "Park";
            return tab != null;
        }

        /// <summary>Set while the shown category is locked, so its buildings are not called "available".</summary>
        private static bool lockedCategory;

        /// <summary>"Small House, wood 2, 1 by 1, available" or "…, unavailable: cannot afford".</summary>
        private static string Describe(BuildingCostUpdater b, bool brief)
        {
            if (b == null) return string.Empty;
            var building = b.prefab;
            if (IsSubMenuEntry(b, out var sub)) return building.FriendlyName + ", opens " + CategoryNames[sub] + " list";
            int lm = Player.inst.FocusedLandMass;
            bool canAfford = BuildInfoFloating.CanAfford(lm, building);
            bool ok = BuildInfoFloating.inst.CheckPrereqs(building, canAfford, b.PreReq, out var prereq);
            var parts = new List<string> { building.FriendlyName };
            parts.Add(CostText(building, lm));
            parts.Add((int)building.size.x + " by " + (int)building.size.z);
            if (lockedCategory) parts.Add("category locked");
            else if (ok) parts.Add("available");
            else
            {
                string why;
                switch (prereq)
                {
                    case BuildInfoFloating.SpecialBuildingPrereq.Building:
                        why = "requires " + (b.PreReq != null ? b.PreReq.FriendlyName : "another building");
                        break;
                    case BuildInfoFloating.SpecialBuildingPrereq.ChamberOfWarOne:
                        why = "you can only have one";
                        break;
                    case BuildInfoFloating.SpecialBuildingPrereq.ShipTwoDocks:
                        why = "requires two docks";
                        break;
                    case BuildInfoFloating.SpecialBuildingPrereq.NotPlayerLandmass:
                        why = "this island is not yours";
                        break;
                    default:
                        why = lm == -1 ? "build your keep first" : "cannot afford";
                        break;
                }
                parts.Add("unavailable, " + why);
            }
            if (building.dragPlacementMode == Building.DragPlacementMode.Path) parts.Add("built in lines with Shift Enter");
            else if (building.dragPlacementMode == Building.DragPlacementMode.Rectangle) parts.Add("built in areas with Shift Enter");
            if (!brief)
            {
                parts.Add(building.Description);
                if (building.CategoryName != "house" && building.CategoryName != "ship" && building.WorkersForFullYield > 0) parts.Add(TextUtil.Plural(building.WorkersForFullYield, "worker"));
                var wage = building.GetComponent<WagePayer>();
                if (wage != null)
                {
                    float perYear = wage.GetGoldWage(Player.inst.PlayerLandmassOwner) * wage.PaydaysPerYear();
                    parts.Add("wages " + perYear + " gold per year");
                }
            }
            return TextUtil.Join(", ", parts);
        }

        private static string CostText(Building building, int lm)
        {
            ResourceAmount cost = building.GetCost(Player.inst.PlayerLandmassOwner);
            ResourceAmount have = BuildInfoFloating.GetResourceAvailableForBuilding(lm, building);
            const int n = 12;
            var names = new string[n];
            var need = new int[n];
            var got = new int[n];
            for (int i = 0; i < n; i++)
            {
                names[i] = ((FreeResourceType)i).ToString();
                need[i] = cost.Get((FreeResourceType)i);
                got[i] = have.Get((FreeResourceType)i);
            }
            return "cost " + ResourceNames.Cost(names, need, lm == -1 ? null : got);
        }

        private void Pick()
        {
            if (!items.HasCurrent) return;
            var tab = CurrentTab;
            if (tab.buildButton != null && !tab.buildButton.unlocked)
            {
                A.Cue(Cue.Error);
                A.Say("Locked. " + LockReason(tab));
                return;
            }
            var b = items.Current;
            if (IsSubMenuEntry(b, out var sub))
            {
                int idx = tabs.FindIndex(t => t.title == sub);
                if (idx >= 0)
                {
                    A.Cue(Cue.Open);
                    OpenTab(idx, announceCategory: true);
                }
                return;
            }
            int lm = Player.inst.FocusedLandMass;
            bool canAfford = BuildInfoFloating.CanAfford(lm, b.prefab);
            if (!BuildInfoFloating.inst.CheckPrereqs(b.prefab, canAfford, b.PreReq, out _))
            {
                A.Cue(Cue.Error);
                A.Say(Describe(b, brief: true));
                return;
            }
            Close();
            // Same as clicking the build button: the building follows the (keyboard) pointer.
            MapController.Inst.PointAtCursor();
            GameUI.inst.OnBuild(b);
        }

        private void Close()
        {
            open = false;
            try
            {
                BuildUI.inst.SetVisible(false);
                BuildInfoFloating.inst.HideInstant();
            }
            catch
            {
                // UI already gone (leaving play mode).
            }
        }
    }
}
