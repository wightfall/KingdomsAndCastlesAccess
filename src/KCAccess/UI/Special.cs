using System.Collections.Generic;
using KCAccess.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KCAccess.UI
{
    /// <summary>
    /// Game-specific tweaks to the generic navigator for controls that only work with a mouse
    /// (drag to reorder job priorities, hover arrows for worker limits, …).
    /// </summary>
    internal static class Special
    {
        /// <summary>
        /// Job priority rows: the logic (BuildPriorityItem) lives on hidden layout objects while the visible
        /// row is a separate "follower" object under DecreeUI.ContentContainer. Map a visible object to its row.
        /// </summary>
        internal static BuildPriorityItem RowFor(GameObject go)
        {
            var ui = DecreeUI.inst;
            if (ui == null || ui.ContentContainer == null || ui.prioritized == null || go == null) return null;
            Transform t = go.transform;
            if (!t.IsChildOf(ui.ContentContainer) || t == ui.ContentContainer) return null;
            while (t.parent != null && t.parent != ui.ContentContainer) t = t.parent;
            foreach (Transform layout in ui.prioritized.transform)
            {
                var drag = layout.GetComponent<DragableDecreeItem>();
                if (drag != null && drag.follower == t.gameObject) return layout.GetComponent<BuildPriorityItem>();
            }
            return null;
        }

        /// <summary>Children of t in the order they should be read, or null for hierarchy order.</summary>
        internal static List<Transform> OrderedChildren(Transform t)
        {
            var ui = DecreeUI.inst;
            if (ui == null || t != ui.ContentContainer || ui.prioritized == null) return null;
            var list = new List<Transform>();
            foreach (Transform layout in ui.prioritized.transform)
            {
                var drag = layout.GetComponent<DragableDecreeItem>();
                if (drag != null && drag.follower != null && drag.follower.transform.parent == t) list.Add(drag.follower.transform);
            }
            // Anything not driven by a layout row keeps its place at the end.
            foreach (Transform c in t) if (!list.Contains(c)) list.Add(c);
            return list;
        }

        /// <summary>Advisor portraits live in containers named after their field.</summary>
        private static string AdvisorName(Transform t)
        {
            if (AdvisorUI.inst == null || !t.IsChildOf(AdvisorUI.inst.transform) || t.parent == null) return null;
            switch (t.parent.name)
            {
                case "Food": return Loc.T("Agriculture advisor");
                case "City": return Loc.T("City advisor");
                case "Military": return Loc.T("Military advisor");
                default: return null;
            }
        }

        /// <summary>Research option row (name, research button, gold cost, "researched" marker) and its upgrade.</summary>
        private static bool ResearchRow(GameObject go, out Transform row, out Player.UpgradeType upgrade)
        {
            row = null;
            upgrade = Player.UpgradeType.None;
            var ui = GameUI.inst != null ? GameUI.inst.researchUI : null;
            if (ui == null || ui.optionContainer == null || !go.transform.IsChildOf(ui.optionContainer) || go.transform == ui.optionContainer) return false;
            Transform t = go.transform;
            while (t.parent != null && t.parent != ui.optionContainer) t = t.parent;
            foreach (var kv in ui.optionMap)
            {
                if (kv.Value == t)
                {
                    row = t;
                    upgrade = kv.Key;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Name of a great library technology in the game's language (the research window's term "SU_" + upgrade).</summary>
        internal static string UpgradeName(Player.UpgradeType upgrade)
        {
            string id = upgrade.ToString();
            string t = null;
            try { t = I2.Loc.LocalizationManager.GetTranslation("SU_" + id); } catch { }
            return TextUtil.HasContent(t) ? TextUtil.Clean(t) : TextUtil.Humanize(id);
        }

        private static string ResearchDescription(Transform row, Player.UpgradeType upgrade)
        {
            string name = TextUtil.Clean(UIText.TextOf(row.GetChild(0).GetComponent<TMPro.TMP_Text>()));
            if (string.IsNullOrEmpty(name)) name = UpgradeName(upgrade);
            if (row.childCount > 3 && row.GetChild(3).gameObject.activeSelf) return name + ", " + Loc.T("already researched");
            var b = GameUI.inst.GetBuildingSelected();
            var lib = b != null ? b.GetComponent<GreatLibrary>() : null;
            string cost = lib != null ? ", " + Loc.F("costs {0} gold", lib.GetCost(upgrade)) : string.Empty;
            string tip = UIText.TooltipOf(row.gameObject);
            return TextUtil.Join(", ", name + cost, Loc.T("button, Enter starts the research"), tip);
        }

        /// <summary>Merchant buy / sell line: the resource is only an icon, so build the description from the row data.</summary>
        private static string MerchantRow(ResourceLineItemUI row)
        {
            string res = ResourceNames.Name(row.rtype.ToString());
            int order = row.GetOrderAmount();
            return res + ": " + Loc.F("{0} gold each, {1} available", row.price, row.availableAmount) + ", " + (row.buy ? Loc.F("buy {0}", order) : Loc.F("sell {0}", order))
                   + (order > 0 ? ", " + (row.buy ? Loc.F("costs {0} gold", row.GetCost()) : Loc.F("earns {0} gold", row.GetCost())) : string.Empty)
                   + ". " + Loc.T("Left and Right change the amount by 1, Page Up and Page Down by 10");
        }

        /// <summary>
        /// Many rows show the resource only as an icon. If a sibling image (one or two levels up) uses one of the
        /// game's resource icons, return the resource name ("wood"), else null.
        /// </summary>
        internal static string ResourceIconName(Transform t)
        {
            var textures = Player.inst != null ? Player.inst.resourceTextures : null;
            if (textures == null || t.parent == null) return null;
            // Only the item's own row: images that are siblings of the item (or of its parent when the item
            // sits in a small wrapper). Never look inside buttons or deeper containers.
            var levels = new System.Collections.Generic.List<Transform> { t.parent };
            if (t.parent.parent != null && t.parent.childCount <= 3) levels.Add(t.parent.parent);
            foreach (var level in levels)
            {
                if (level.childCount > 8) continue; // a list or panel, not a row
                for (int c = 0; c < level.childCount; c++)
                {
                    var child = level.GetChild(c);
                    if (!child.gameObject.activeInHierarchy || child.GetComponent<Selectable>() != null) continue;
                    var img = child.GetComponent<Image>();
                    if (img == null || img.sprite == null) continue;
                    for (int i = 0; i < textures.Count; i++)
                    {
                        if (textures[i] != null && textures[i] == img.sprite) return ResourceNames.Name(((FreeResourceType)i).ToString());
                    }
                }
            }
            return null;
        }

        /// <summary>Resource name for one of the game's resource icons, or null.</summary>
        private static string ResourceOfSprite(Sprite sprite)
        {
            var textures = Player.inst != null ? Player.inst.resourceTextures : null;
            if (textures == null || sprite == null) return null;
            for (int i = 0; i < textures.Count; i++)
                if (textures[i] != null && textures[i] == sprite) return ResourceNames.Name(((FreeResourceType)i).ToString());
            return null;
        }

        /// <summary>
        /// Diplomacy price editor (AINegotiationUI): one slider per resource, shown only with the resource's icon and a
        /// face that shows how the other kingdom feels about the price. Returns the resource ("Wood"), or null.
        /// </summary>
        private static string NegotiationResource(Slider s)
        {
            var entry = s != null ? s.GetComponentInParent<ResourceNegotiationEntryUI>() : null;
            if (entry == null) return null;
            string res = null;
            foreach (var img in entry.GetComponentsInChildren<Image>(true))
            {
                res = ResourceOfSprite(img.sprite);
                if (res != null) break;
            }
            return TextUtil.Capitalize(res ?? Loc.T("resource"));
        }

        private static string NegotiationValue(Slider s) => (int)s.value + ", " + Loc.F("they are {0}", UnitText.NegotiationMood(s.value));

        /// <summary>Value spoken after a slider was changed, when the slider needs more than the number; null otherwise.</summary>
        internal static string SliderValue(Slider s) => NegotiationResource(s) != null ? NegotiationValue(s) : null;

        /// <summary>
        /// The army panel (UnitUI): its tabs and its list of selected units are pictures only. Names them, or returns null.
        /// </summary>
        private static string UnitPanelItem(GameObject go)
        {
            var ui = UnitUI.inst;
            if (ui == null || go == null || !go.transform.IsChildOf(ui.transform)) return null;
            if (ui.tabRoot != null)
            {
                for (int i = 0; i < ui.tabRoot.Length; i++)
                {
                    var root = ui.tabRoot[i];
                    if (root == null || !(go.transform == root.transform || go.transform.IsChildOf(root.transform))) continue;
                    string name = UnitText.TabName(((UnitUI.UnitTabs)i).ToString()) ?? TextUtil.Humanize(((UnitUI.UnitTabs)i).ToString());
                    var rt = root.transform;
                    string count = rt.childCount > 0 ? TextUtil.Clean(UIText.TextOf(rt.GetChild(rt.childCount - 1).GetComponent<TMPro.TMP_Text>())) : string.Empty;
                    bool selected = rt.childCount > 1 && rt.GetChild(1).gameObject.activeSelf && ui.currentTabSelected == (UnitUI.UnitTabs)i;
                    return TextUtil.Join(", ", name, count, Loc.T("tab"), selected ? Loc.T("selected") : null);
                }
            }
            if (ui.unitOptionContainer != null && go.transform.parent == ui.unitOptionContainer.transform)
            {
                int idx = go.transform.GetSiblingIndex();
                var units = ui.units;
                if (units != null && idx >= 0 && idx < units.Count && units.data[idx] != null)
                    return TextUtil.Capitalize(Game.CellInfo.WithoutSelected(Game.CellInfo.UnitName(units.data[idx]))) + ", " + Loc.T("button, Enter shows this unit's details");
            }
            return null;
        }

        /// <summary>Dock / stockpile "desired amount" rows: resource icon, desired amount field, stored amount, transport-only toggle.</summary>
        private static bool OrderRow(GameObject go, out Transform row, out FreeResourceType type)
        {
            row = null;
            type = FreeResourceType.None;
            var form = go.GetComponentInParent<ResourceOrderFormUI>();
            if (form == null) return false;
            for (int k = 0; k < form.resourceOrders.Count; k++)
            {
                var o = form.resourceOrders[k];
                if (o == null || o.parent == null || !go.transform.IsChildOf(o.parent)) continue;
                row = o.parent;
                type = (FreeResourceType)(k < 8 ? k : k + 1);
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- key rebinding (Settings > Keyboard)

        private static KeyButton pendingKeyButton;
        private static KeyButton capturingKeyButton;
        private static float captureStarted;

        /// <summary>True while the game is waiting for the new key; the mod must not use any key then.</summary>
        internal static bool CapturingKey => pendingKeyButton != null || capturingKeyButton != null;

        /// <summary>A real key binding row (the "Restore Defaults" button is a KeyButton too, but has no action text).</summary>
        private static bool IsKeyRow(KeyButton kb)
        {
            if (kb == null) return false;
            foreach (var t in kb.GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (t.gameObject.name == "KeyActionText") return true;
            return false;
        }

        /// <summary>Emergency reset: forget any pending key capture.</summary>
        internal static void ResetCapture()
        {
            pendingKeyButton = null;
            capturingKeyButton = null;
            SettingsMenuUI.isListeningToKey = false;
        }

        private static string KeyAction(KeyButton kb)
        {
            // The KeyButton sits on the row itself; its texts are the action name and the key.
            var row = kb.transform;
            {
                foreach (var t in row.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    if (t.gameObject.name == "KeyActionText") return TextUtil.Clean(UIText.TextOf(t));
                }
            }
            return TextUtil.Capitalize(Game.ModSettingsMenu.GameActionName((InputActions)kb.buttonNum));
        }

        private static string KeyName(KeyButton kb)
        {
            var t = kb.myButton != null ? kb.myButton.GetComponentInChildren<TMPro.TMP_Text>() : null;
            string k = t != null ? TextUtil.Clean(UIText.TextOf(t)) : string.Empty;
            return string.IsNullOrEmpty(k) ? Loc.T("no key") : k.ToLowerInvariant().Replace("alpha", "");
        }

        /// <summary>Runs every frame before anything else while a key binding is being changed.</summary>
        internal static void UpdateCapture()
        {
            if (pendingKeyButton != null)
            {
                // Start listening only after Enter is released, or the game would bind Enter itself.
                if (Input.anyKey) return;
                capturingKeyButton = pendingKeyButton;
                pendingKeyButton = null;
                capturingKeyButton.SetCurrentButton();
                captureStarted = Time.unscaledTime;
                A.Cue(Cue.Open);
                A.Say(Loc.F("Press the new key for {0}. You can hold Control, Shift or Alt with it.", KeyAction(capturingKeyButton)), force: true);
                return;
            }
            if (capturingKeyButton != null)
            {
                var ui = SettingsMenuUI.inst;
                if (ui == null || !SettingsMenuUI.isListeningToKey || Time.unscaledTime - captureStarted > 15f)
                {
                    var kb = capturingKeyButton;
                    capturingKeyButton = null;
                    if (SettingsMenuUI.isListeningToKey) SettingsMenuUI.isListeningToKey = false;
                    A.Cue(Cue.Placed);
                    A.Say(Loc.F("{0} is now {1}", KeyAction(kb), KeyName(kb)), force: true);
                }
            }
        }

        /// <summary>Hide parts of composite rows; the row is represented by one control.</summary>
        internal static bool Exclude(GameObject go)
        {
            // The fire risk bar is a picture with "Low" and "High" at its ends; the title row speaks the risk instead.
            var fire = go.GetComponentInParent<FireRiskUI>();
            if (fire != null && go.transform != fire.transform && go.name != "SummaryTitle") return true;
            if (go.name == "KeyActionText" && go.transform.parent != null && go.transform.parent.GetComponentInChildren<KeyButton>(true) != null) return true;
            var merchantRow = go.GetComponentInParent<ResourceLineItemUI>();
            if (merchantRow != null && merchantRow.orderAmt != null)
            {
                var input = merchantRow.orderAmt.transform;
                return go.transform != input && !input.IsChildOf(go.transform);
            }
            if (ResearchRow(go, out var rrow, out _) && go.transform != rrow)
            {
                var btn = rrow.GetComponentInChildren<Button>(false);
                // Keep the research button; when it is hidden (researched) keep the name text instead.
                if (btn != null) return go.transform != btn.transform && !btn.transform.IsChildOf(go.transform);
                return go.transform != rrow.GetChild(0);
            }
            // Save slots: the small delete button is reached with the Delete key on the slot instead.
            var saveSlot = go.GetComponentInParent<Assets.Code.UI.SaveLoadOption>();
            if (saveSlot != null && saveSlot.deleteButton != null && (go.transform == saveSlot.deleteButton.transform || go.transform.IsChildOf(saveSlot.deleteButton.transform))) return true;
            // Villager lists: the camera-only "find everyone" glass, and names already spoken by each row's button.
            if (go.name == "FindVillagersButton" && go.GetComponentInParent<VillagerListUI>() != null) return true;
            var person = go.GetComponentInParent<PersonListItemUI>();
            if (person != null && person.MagGlass != null && go.transform != person.MagGlass.transform && !person.MagGlass.transform.IsChildOf(go.transform)) return true;
            // Portrait captions duplicate the advisor buttons' names.
            if (AdvisorUI.inst != null && go.name.StartsWith("Text") && go.GetComponent<TMPro.TMP_Text>() != null && go.transform.parent != null
                && go.transform.IsChildOf(AdvisorUI.inst.transform) && go.transform.parent.Find("bottomgradient") != null) return true;
            // Each advisor has two overlapping buttons (portrait and speech bubble): keep the first.
            if (AdvisorName(go.transform) != null && go.GetComponent<Button>() != null)
            {
                for (int i = 0; i < go.transform.GetSiblingIndex(); i++)
                {
                    var sib = go.transform.parent.GetChild(i);
                    if (sib.gameObject.activeInHierarchy && sib.GetComponent<Button>() != null) return true;
                }
            }
            var row = RowFor(go);
            if (row == null) return false;
            var toggle = row.DisableToggle.transform;
            // Keep the toggle and the objects above it (so the walk can reach it); hide the rest of the row.
            return go.transform != toggle && !toggle.IsChildOf(go.transform);
        }

        /// <summary>Custom spoken description, or null to use the default.</summary>
        /// <summary>The language list's entry for a game language (labels are native names: "Deutsch, ( German )").</summary>
        private static Button LanguageButton(ChangeLanguage cl, string i2Name)
        {
            switch (i2Name)
            {
                case "English": return cl.englishButton;
                case "German": return cl.germanButton;
                case "French": return cl.frenchButton;
                case "Simplified Chinese": return cl.sChineseButton;
                case "Traditional Chinese": return cl.tChineseButton;
                case "Dutch": return cl.dutchButton;
                case "Japanese": return cl.japeneseButton;
                case "Romanian": return cl.romanianButton;
                case "Portuguese (Brazil)": return cl.brazilianPortugueseButton;
                case "Spanish": return cl.spanishButton;
                case "Korean": return cl.koreanButton;
                case "Italian": return cl.italianButton;
                case "Polish": return cl.polishButton;
                case "Russian": return cl.russianButton;
                case "Norwegian": return cl.norwegianButton;
                case "Ukrainian": return cl.ukrainianButton;
                case "Swedish": return cl.swedishButton;
                case "Turkish": return cl.turkishButton;
                default: return null;
            }
        }

        /// <summary>The toolbar's speed buttons are pictures only (their handler, OnControlChanged, named all four the same).</summary>
        private static string SpeedToggleName(Selectable s)
        {
            var ui = SpeedControlUI.inst;
            if (ui == null || !(s is Toggle) || s == null) return null;
            if (s == ui.pauseButton) return Loc.T("Pause");
            if (s == ui.playButton1) return Loc.T("Normal speed");
            if (s == ui.playButton2) return Loc.T("Fast speed");
            if (s == ui.playButton3) return Loc.T("Fastest speed");
            return null;
        }

        /// <summary>A troop ship's cargo slot in the army panel: a picture of the unit and a lock; null for other controls.</summary>
        private static string OnBoardSlot(Selectable s)
        {
            var ui = UnitUI.inst;
            if (ui == null || ui.shipOnBoardOptions == null || !(s is Toggle tg)) return null;
            for (int i = 0; i < ui.shipOnBoardOptions.Length; i++)
            {
                if (ui.shipOnBoardOptions[i].toggle != tg) continue;
                var ship = ui.currentUnitSelected as TroopTransportShip;
                string who = ship != null && i < ship.CarryCount() && ship.GetCarried(i) != null
                    ? Game.CellInfo.WithoutSelected(Game.CellInfo.UnitName(ship.GetCarried(i))) : Loc.T("unit");
                return Loc.F("On board: {0}. Stays on the ship when it unloads, check box, {1}", who, tg.isOn ? Loc.T("checked") : Loc.T("not checked"));
            }
            return null;
        }

        /// <summary>
        /// On / off buttons of the storage filters and the blacksmith (ResourceToggleButton): the state is a tick or a cross
        /// picture, and the resource often only an icon. Null for other controls.
        /// </summary>
        private static ResourceToggleButton ResourceToggle(Selectable s, out string name)
        {
            name = null;
            var r = s is Button ? s.GetComponentInParent<ResourceToggleButton>() : null;
            if (r == null || r.button != s) return null;
            var f = GameUI.inst != null ? GameUI.inst.resourceFilterUI : null;
            var smith = GameUI.inst != null ? GameUI.inst.blacksmithUI : null;
            if (f != null)
            {
                if (r == f.WoodToggleButton) name = ResourceNames.Name("Tree");
                else if (r == f.StoneToggleButton) name = ResourceNames.Name("Stone");
                else if (r == f.CharcoalToggleButton) name = ResourceNames.Name("Charcoal");
                else if (r == f.IronToggleButton) name = ResourceNames.Name("IronOre");
                else if (r == f.ToolsToggleButton) name = ResourceNames.Name("Tools");
                else if (r == f.ArmamentToggleButton) name = ResourceNames.Name("Armament");
            }
            if (name == null && smith != null)
            {
                if (r == smith.ToolToggleButton) name = Loc.T("Make tools");
                else if (r == smith.WeaponToggleButton) name = Loc.T("Make armaments");
            }
            if (name == null) name = UIText.LabelOf(s);
            name = TextUtil.Capitalize(name);
            return r;
        }

        /// <summary>
        /// Amount fields of a ResourceAmountUI (route stop pick up / drop off amounts, an envoy's gift): one field per
        /// resource, named only by an icon. The container's children are in FreeResourceType order. Null otherwise.
        /// </summary>
        private static string ResourceAmountField(Selectable s)
        {
            if (!(s is TMPro.TMP_InputField field)) return null;
            var ui = s.GetComponentInParent<ResourceAmountUI>();
            var container = ui != null ? (ui.resourceContainer != null ? ui.resourceContainer : ui.transform) : null;
            if (container == null) return null;
            for (int i = 0; i < container.childCount && i < (int)FreeResourceType.NumTypes; i++)
            {
                if (!s.transform.IsChildOf(container.GetChild(i))) continue;
                string res = TextUtil.Capitalize(ResourceNames.Name(((FreeResourceType)i).ToString()));
                string value = string.IsNullOrEmpty(field.text) ? "0" : field.text;
                if (field.interactable) return Loc.F("{0}, edit, {1}", res, value);
                // The reason ("this storage does not accept it") is the tooltip of the cover drawn over the field, not of the field.
                var hover = field.GetComponentInParent<HoverHide>();
                string why = hover != null && hover.toHide != null ? UIText.TooltipOf(hover.toHide.gameObject) : null;
                return TextUtil.Join(", ", res, value, Loc.T("unavailable"), why ?? UIText.TooltipOf(field.gameObject));
            }
            return null;
        }

        private static bool HasText(UIItem item, Component text) =>
            text != null && item.Texts != null && item.Texts.Contains(text);

        /// <summary>
        /// A dragon's health, happiness and bond (dragon nest panel, army panel): what affects them is only in a tooltip the
        /// panel draws itself on hover (an EventTrigger, no TooltipHook). Returns that text for those items, else null.
        /// </summary>
        internal static string ExtraTooltip(UIItem item)
        {
            if (item == null || item.Go == null) return null;
            try
            {
                DragonNest.DragonSlot slot = null;
                int which = -1; // 0 health, 1 happiness, 2 bond
                RectTransform rect = null;
                TMPro.TextMeshProUGUI desc = null, warning = null;
                System.Action hide = null;
                var nestUI = GameUI.inst != null && GameUI.inst.workerUI != null ? GameUI.inst.workerUI.dragonNestUI : null;
                if (nestUI != null && nestUI.slots != null && nestUI.dragonNest != null && item.Go.transform.IsChildOf(nestUI.transform))
                {
                    for (int i = 0; i < nestUI.slots.Length && i < nestUI.dragonNest.slots.Length; i++)
                    {
                        var s = nestUI.slots[i];
                        if (s == null || !item.Go.transform.IsChildOf(s.transform)) continue;
                        if (HasText(item, s.healthTMP) || HasText(item, s.healthDescriptionTMP)) which = 0;
                        else if (HasText(item, s.happinessTMP) || (s.happinessSlider != null && item.Control == s.happinessSlider)) which = 1;
                        else if (HasText(item, s.bondProgressTMP) || HasText(item, s.bondSliderTMP) || (s.bondSlider != null && item.Control == s.bondSlider)) which = 2;
                        slot = nestUI.dragonNest.slots[i];
                        break;
                    }
                    rect = nestUI.tooltipRectT;
                    desc = nestUI.tooltipTMP;
                    warning = nestUI.tooltipWarningTMP;
                    hide = () => nestUI.SetTooltipVisibility(false);
                }
                var unitUI = UnitUI.inst;
                if (which < 0 && unitUI != null && item.Go.transform.IsChildOf(unitUI.transform))
                {
                    if (unitUI.currentUnitSelected is Dragon d) slot = d.currSlot;
                    else if (unitUI.currentUnitSelected is DragonPuppet p) slot = p.dSlot;
                    if (HasText(item, unitUI.dragonHealthTMP) || HasText(item, unitUI.dragonHealthDescriptionTMP)) which = 0;
                    else if (HasText(item, unitUI.dragonHappinessTMP) || (unitUI.dragonHappinessSlider != null && item.Control == unitUI.dragonHappinessSlider)) which = 1;
                    else if (HasText(item, unitUI.dragonBondTMP) || HasText(item, unitUI.dragonBondProgressTMP) || (unitUI.dragonBondSlider != null && item.Control == unitUI.dragonBondSlider)) which = 2;
                    rect = unitUI.dragonTooltipRect;
                    desc = unitUI.dragonTooltipTMP;
                    warning = unitUI.dragonTooltipWarningTMP;
                    hide = () => { if (unitUI.dragonTooltipRect != null) unitUI.dragonTooltipRect.gameObject.SetActive(false); };
                }
                if (which < 0 || slot == null || slot.currNest == null || rect == null || desc == null || warning == null) return null;
                if (which == 0) DragonNestUI.UpdateHealthTooltip(slot, rect, desc, warning, rect.position);
                else if (which == 1) DragonNestUI.UpdateHappinessTooltip(slot, rect, desc, warning, rect.position);
                else DragonNestUI.UpdateBondTooltip(slot, rect, desc, warning, rect.position);
                string text = TextUtil.Sentences(desc.text, warning.enabled ? warning.text : null);
                hide();
                return text.Length > 0 ? text : null;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Dragon tooltip failed: " + e.Message);
                return null;
            }
        }

        internal static string Describe(UIItem item)
        {
            if (item == null || item.Go == null) return null;
            if (item.IsControl && ResourceAmountField(item.Control) is string amountField) return amountField;
            if (item.IsControl && ResourceToggle(item.Control, out var toggleName) is ResourceToggleButton rtb)
                return toggleName + ", " + Loc.T("check box") + ", " + (rtb.State ? Loc.T("checked") : Loc.T("not checked"));
            if (item.IsControl && SpeedToggleName(item.Control) is string speed)
                return speed + ", " + Loc.T("radio button") + ", " + (((Toggle)item.Control).isOn ? Loc.T("selected") : Loc.T("not selected"));
            if (item.IsControl && OnBoardSlot(item.Control) is string onBoard) return onBoard;
            if (item.IsControl && item.Go.GetComponentInParent<LogisticsDestUI>() is LogisticsDestUI stop)
            {
                string d = Game.Routes.Describe(stop, item.Control);
                if (d != null) return d;
            }
            if (item.IsControl && item.Control is Slider ns && NegotiationResource(ns) is string nres)
                return Loc.F("{0} price, slider, {1}", nres, NegotiationValue(ns));
            string unit = item.IsControl ? UnitPanelItem(item.Go) : null;
            if (unit != null) return unit;
            if (item.IsControl && item.Go.GetComponentInParent<PersonListItemUI>() is PersonListItemUI pli && pli.MagGlass != null && item.Control == pli.MagGlass)
            {
                string who = pli.Description != null ? TextUtil.Clean(UIText.TextOf(pli.Description)) : Loc.T("villager");
                bool hoh = pli.HeadOfHouseholdIcon != null && pli.HeadOfHouseholdIcon.activeSelf;
                return who + (hoh ? ", " + Loc.T("head of household") : string.Empty) + ", " + Loc.T("button, Enter selects this villager");
            }
            if (item.Go.name == "SummaryTitle" && item.Go.transform.parent != null && item.Go.transform.parent.GetComponent<FireRiskUI>() is FireRiskUI fr)
            {
                string risk = fr.tooltip != null ? TextUtil.Clean(fr.tooltip.toolTipText ?? string.Empty) : string.Empty;
                return UIText.TextOf(item.Go.GetComponent<TMPro.TMP_Text>()) + ": " + (risk.Length > 0 ? risk : Loc.T("unknown"));
            }
            var row = RowFor(item.Go);
            if (row != null && item.Control == row.DisableToggle) return PriorityRow(row);
            if (item.Control is Button && item.Go.GetComponentInParent<PickNameUI>() != null && UIText.LabelOf(item.Control) == UIText.Unlabelled)
                return Loc.T("Choose banner, button");
            if (item.IsControl && StreamerUI.inst != null && item.Go.transform.IsChildOf(StreamerUI.inst.transform) && UIText.LabelOf(item.Control) == "Toggle Visible")
                return Game.Twitch.SettingsVisible ? Loc.T("Hide the Twitch voting settings, button") : Loc.T("Show the Twitch voting settings, button");
            // Route panel: the run / pause toggles have no text (the status next to them, "Waiting", was read as their name),
            // and the cycle slider's value is only shown as text ("as fast as possible", "every 2 years").
            var route = GameUI.inst != null ? GameUI.inst.shipLogisticsUI : null;
            if (route != null && route.gameObject.activeInHierarchy && item.Control != null)
            {
                string status = route.statusText != null ? TextUtil.Clean(UIText.TextOf(route.statusText)) : string.Empty;
                if (item.Control == route.play || item.Control == route.pause)
                {
                    var tg = (Toggle)item.Control;
                    string name = item.Control == route.play ? Loc.T("Run the route") : Loc.T("Pause the route");
                    return name + ", " + Loc.T("radio button") + ", " + (tg.isOn ? Loc.T("selected") : Loc.T("not selected"))
                        + (TextUtil.HasContent(status) ? ". " + Loc.F("Now: {0}", status) : string.Empty);
                }
                if (item.Control == route.routeCycleSlider && route.routeEveryYearsText != null)
                    return Loc.F("Run this route: {0}, slider. Left and Right change it", TextUtil.Clean(UIText.TextOf(route.routeEveryYearsText)));
            }
            // Army panel stance buttons have no text of their own; their name is the start of the tooltip
            // ("Hold Stance: Unit will remain stationary ...").
            if (item.Control is Toggle stance && UnitUI.inst != null
                && (stance == UnitUI.inst.unitAttackBehaviorHold || stance == UnitUI.inst.unitAttackBehaviorAttack || stance == UnitUI.inst.unitAttackBehaviorPursue))
            {
                string tip = UIText.TooltipOf(item.Go) ?? string.Empty;
                int colon = tip.IndexOf(':');
                string name = colon > 0 ? tip.Substring(0, colon).Trim() : tip;
                if (name.Length == 0)
                    name = stance == UnitUI.inst.unitAttackBehaviorHold ? Loc.T("Hold stance") : stance == UnitUI.inst.unitAttackBehaviorAttack ? Loc.T("Attack stance") : Loc.T("Pursue stance");
                return name + ", " + Loc.T("radio button") + ", " + (stance.isOn ? Loc.T("selected") : Loc.T("not selected")); // the navigator adds the tooltip
            }
            // Building panel: the demolish, rename and close buttons are pictures, and the name field is greyed out
            // until the rename button is pressed.
            var worker = GameUI.inst != null ? GameUI.inst.workerUI : null;
            if (item.IsControl && worker != null)
            {
                if (item.Control == worker.trashButton) return Loc.T("Demolish, button");
                if (item.Control == worker.editTitleButton) return Loc.T("Rename building, button");
                if (worker.close != null && item.Go == worker.close) return Loc.T("Close, button");
                if (item.Control == worker.titleText)
                {
                    string name = TextUtil.Clean(worker.titleText.text);
                    if (worker.titleText.interactable) return Loc.F("Building name, edit, {0}", name.Length > 0 ? name : UIText.Blank);
                    var b = GameUI.inst.GetBuildingSelected();
                    if (name.Length == 0 && b != null) name = b.FriendlyName;
                    return Loc.F("Building name: {0}", name) + (worker.editTitleButton != null && worker.editTitleButton.gameObject.activeInHierarchy ? ". " + Loc.T("The rename button changes it") : string.Empty);
                }
            }
            // Construction site panel: run / pause toggles and the demolish button are pictures; the builder count is a bare "3 / 5".
            var construct = GameUI.inst != null ? GameUI.inst.constructUI : null;
            if (construct != null)
            {
                if (item.IsControl && (item.Control == construct.play || item.Control == construct.pause))
                {
                    var tg = (Toggle)item.Control;
                    return (item.Control == construct.play ? Loc.T("Keep building") : Loc.T("Pause construction")) + ", " + Loc.T("radio button") + ", " + (tg.isOn ? Loc.T("selected") : Loc.T("not selected"));
                }
                if (item.IsControl && construct.trashUI != null && item.Go == construct.trashUI) return Loc.T("Demolish, button");
                if (item.Control is Slider cs && construct.progressBar != null && item.Go == construct.progressBar)
                    return Loc.F("Construction progress, {0}", TextUtil.Percent(cs.value));
                if (!item.IsControl && construct.infoTextUI != null && item.Go == construct.infoTextUI.gameObject)
                    return Loc.F("Builders {0}", UIText.JoinTexts(item.Texts));
            }
            // Stone slab: the text style buttons are pictures (their handlers are added in code, so no handler name either).
            var slab = item.IsControl && GameUI.inst != null ? GameUI.inst.slabUI : null;
            if (slab != null && item.Control is Button && item.Go.transform.IsChildOf(slab.transform) && UIText.JoinTexts(UIText.VisibleTexts(item.Go.transform)).Length <= 2)
            {
                string style = item.Control == slab.largeTextButton ? Loc.T("Larger text")
                    : item.Control == slab.normalTextButton ? Loc.T("Normal text size")
                    : item.Control == slab.smallTextButton ? Loc.T("Smaller text")
                    : item.Control == slab.alignLeftButton ? Loc.T("Align left")
                    : item.Control == slab.alignCenterButton ? Loc.T("Align centre")
                    : item.Control == slab.alignRightButton ? Loc.T("Align right")
                    : item.Control == slab.italicsButton ? Loc.T("Italic") : null;
                if (style != null) return Loc.F("{0}, button: text typed after it gets this style", style);
            }
            // Barracks, archery range, siege workshop, keep: the training bar has no caption.
            var barracks = GameUI.inst != null ? GameUI.inst.barracksUI : null;
            if (barracks != null && barracks.progressBar != null && item.Control == barracks.progressBar)
                return Loc.F("Training progress, {0}", TextUtil.Percent(barracks.progressBar.value));
            // Kingdom share: the code fields have no caption, and some buttons are only pictures.
            var share = item.IsControl && GameState.inst != null && GameState.inst.mainMenuMode != null ? GameState.inst.mainMenuMode.kingdomShareUI : null;
            if (share != null && item.Go.transform.IsChildOf(share.transform))
            {
                if (item.Control == share.L_codeInput) return Loc.F("Code of the kingdom to visit, edit, {0}", UIText.ValueOf(share.L_codeInput));
                if (item.Control == share.U_codeOutput) return Loc.F("Share code of your kingdom: {0}", UIText.ValueOf(share.U_codeOutput));
                bool pictureOnly = UIText.VisibleTexts(item.Go.transform).Count == 0;
                if (pictureOnly && item.Control == share.close) return Loc.T("Close, button");
                if (pictureOnly && item.Control == share.U_refreshCode) return Loc.T("Get a new share code, button");
                if (pictureOnly && item.Control == share.U_takeScreenshot) return Loc.T("Take a new screenshot, button");
            }
            // Kingdom overview: the island name field is greyed out until its Edit button is pressed.
            var region = item.IsControl ? item.Go.GetComponentInParent<RegionNameUI>() : null;
            if (region != null && region.regionNameInput != null && item.Go == region.regionNameInput.gameObject)
            {
                string name = TextUtil.Clean(region.regionNameInput.text);
                if (region.regionNameInput.interactable) return Loc.F("Island name, edit, {0}", name);
                return Loc.F("Island name: {0}", name) + (region.editButton != null && region.editButton.gameObject.activeInHierarchy ? ". " + Loc.T("The next button renames it") : string.Empty);
            }
            if (region != null && region.editButton != null && item.Go == region.editButton.gameObject)
                return Loc.T("Rename island, button");
            // Kingdom overview: the kingdom name works the same way, its rename button is a picture.
            var town = item.IsControl ? TownNameUI.inst : null;
            if (town != null && town.cityNameInput != null && item.Control == town.cityNameInput)
            {
                string name = TextUtil.Clean(town.cityNameInput.text);
                if (town.cityNameInput.interactable) return Loc.F("Kingdom name, edit, {0}", name.Length > 0 ? name : UIText.Blank);
                return Loc.F("Kingdom name: {0}", name) + (town.editButton != null && town.editButton.gameObject.activeInHierarchy ? ". " + Loc.T("The next button renames it") : string.Empty);
            }
            if (town != null && town.editButton != null && item.Control == town.editButton)
                return Loc.T("Rename kingdom, button");
            if (item.IsControl && item.Go.GetComponent<ChangeLanguage>() is ChangeLanguage cl)
                return Loc.F("Language: {0}, button, Enter opens the list", TextUtil.Clean(UIText.TextOf(cl.languageButtonText)))
                       + (ModLanguage.ActiveModOnly ? ". " + Loc.F("Screen reader language: {0}", ModLanguage.ActiveName) : string.Empty);
            var kbd = item.IsControl ? item.Go.GetComponentInParent<KeyButton>() : null;
            if (IsKeyRow(kbd)) return KeyAction(kbd) + ": " + KeyName(kbd) + ", " + Loc.T("button, Enter to change");
            // Map setup: the arrows around the seed field.
            var seed = item.IsControl ? item.Go.GetComponentInParent<SeedButtonUI>() : null;
            if (seed != null && UIText.VisibleTexts(item.Go.transform).Count == 0)
            {
                if (item.Control == seed.previousSeed) return Loc.T("Previous map, button");
                if (item.Control == seed.nextSeed) return Loc.T("Next map, button");
            }
            if (item.IsControl && item.Go.name == "SeedInput" && !UIText.LabelOf(item.Control).ToLowerInvariant().Contains("seed"))
                return Loc.F("Map seed, edit, {0}", UIText.ValueOf(item.Control));
            var mrow = item.Go.GetComponentInParent<ResourceLineItemUI>();
            if (mrow != null && item.Control != null && item.Control == mrow.orderAmt) return MerchantRow(mrow);
            if (item.IsControl && OrderRow(item.Go, out var orow, out var otype))
            {
                string res = ResourceNames.Name(otype.ToString());
                string stored = TextUtil.Clean(UIText.TextOf(orow.GetChild(2).GetComponent<TMPro.TMP_Text>()));
                if (item.Control is TMPro.TMP_InputField f)
                    return res + ": " + Loc.F("{0} stored, desired amount {1}, edit, Enter to type a new amount", stored, string.IsNullOrEmpty(f.text) ? "0" : f.text);
                if (item.Control is Toggle tg)
                    return res + ": " + Loc.F("keep for transport only, check box, {0}", tg.isOn ? Loc.T("checked") : Loc.T("not checked")) + (UIText.TooltipOf(item.Go) is string tip ? ". " + tip : string.Empty);
            }
            if (ResearchRow(item.Go, out var researchRow, out var upgrade)) return ResearchDescription(researchRow, upgrade);
            if (item.IsControl && item.Go.GetComponentInParent<RivalItemUI>() is RivalItemUI rival)
            {
                int slot = 1;
                var all = RivalKingdomSettingsUI.inst != null ? RivalKingdomSettingsUI.inst.rivalItems : null;
                if (all != null) for (int i = 0; i < all.Length; i++) if (all[i] == rival) slot = i + 1;
                string rivalName = rival.rivalName != null ? TextUtil.Clean(UIText.TextOf(rival.rivalName)) : null;
                if (item.Control == rival.enableButton) return Loc.F("Add AI kingdom, slot {0}, button", slot);
                if (item.Control == rival.removeButton) return Loc.F("Remove AI kingdom {0}, slot {1}, button", rivalName ?? string.Empty, slot);
                if (item.Control == rival.personalityDropdown) return Loc.F("AI kingdom {0} skill level, combo box, {1}", rivalName, UIText.ValueOf(rival.personalityDropdown));
            }
            if (item.IsControl && item.Go.GetComponentInParent<Assets.Code.UI.Confirmation>() is Assets.Code.UI.Confirmation conf
                && conf.GetComponentInParent<Assets.Code.UI.SaveLoadUI>() != null && conf.yesButton != null && item.Control == conf.yesButton)
            {
                // Save screen confirmations: say what "yes" does ("It's toast" = delete, or overwrite / load). Told apart by
                // which confirmation it is: the question text is in the game's language.
                string yes = UIText.LabelOf(conf.yesButton);
                var saveUi = conf.GetComponentInParent<Assets.Code.UI.SaveLoadUI>();
                if (conf == saveUi.deleteConfirmation) return yes + ", " + Loc.T("yes, delete the save, button");
                if (conf == saveUi.saveConfirmation) return yes + ", " + Loc.T("yes, save over this game, button");
                if (conf == saveUi.loadConfirmation) return yes + ", " + Loc.T("yes, load it, progress since your last save is lost, button");
            }
            // Save slots: the autosave mark is a picture.
            var saveSlot = item.IsControl && item.Control is Button ? item.Go.GetComponentInParent<Assets.Code.UI.SaveLoadOption>() : null;
            if (saveSlot != null && item.Control != saveSlot.deleteButton && saveSlot.autosave != null && UIText.IsVisible(saveSlot.autosave)
                && UIText.VisibleTexts(saveSlot.autosave.transform).Count == 0)
                return TextUtil.Join(", ", UIText.LabelOf(item.Control), Loc.T("autosave"), UIText.ButtonRole);
            if (item.IsControl && item.Go.GetComponentInParent<DemolishWarningUI>() != null)
            {
                if (item.Go.name == "Yes") return Loc.T("Yes, demolish, button");
                if (item.Go.name == "No") return Loc.T("No, cancel, button");
            }
            if (item.IsControl && item.Go.GetComponent<PixelCrushers.DialogueSystem.StandardUIContinueButtonFastForward>() != null)
                return Loc.T("Continue, button");
            var tile = item.Go.GetComponent<BannerTile>();
            if (tile != null)
            {
                var ui = item.Go.GetComponentInParent<ChooseBannerUI>();
                var parent = tile.transform.parent;
                int idx = 0, count = 0;
                for (int i = 0; i < parent.childCount; i++)
                {
                    var t = parent.GetChild(i);
                    if (!t.gameObject.activeInHierarchy || t.GetComponent<BannerTile>() == null) continue;
                    count++;
                    if (t == tile.transform) idx = count;
                }
                bool selected = ui != null && ui.highlightedTile == tile;
                string banner = ui != null && parent == ui.customBannerContainer ? Loc.F("Custom banner {0} of {1}", idx, count)
                    : ui != null && parent == ui.workshopBannerContainer ? Loc.F("Workshop banner {0} of {1}", idx, count) : Loc.F("Banner {0} of {1}", idx, count);
                return banner + (selected ? ", " + Loc.T("selected") : string.Empty) + ". " + Loc.T("Enter selects it, then choose Accept");
            }
            if (item.IsControl && item.Go.name == "FolderButton" && item.Go.GetComponentInParent<ChooseBannerUI>() != null)
                return Loc.T("Open custom banner folder, button");
            string advisor = item.IsControl ? AdvisorName(item.Go.transform) : null;
            if (advisor != null) return advisor + ", " + Loc.T("button, press Enter to hear their advice");
            if (item.IsControl && AdvisorUI.inst != null && item.Go.transform.IsChildOf(AdvisorUI.inst.transform) && UIText.LabelOf(item.Control) == UIText.Unlabelled || item.IsControl && AdvisorUI.inst != null && item.Go.transform.IsChildOf(AdvisorUI.inst.transform) && UIText.LabelOf(item.Control) == "Button")
                return Loc.T("Close, button");
            var info = item.Go.GetComponentInParent<InfoBase>();
            if (info != null && !item.IsControl)
            {
                return TextUtil.Join(" ", ResourceName(info), UIText.JoinTexts(item.Texts)) + ", " + Loc.T("press Enter for the yearly report");
            }
            if (item.IsControl)
            {
                string n = item.Go.name;
                string value = UIText.JoinTexts(UIText.VisibleTexts(item.Go.transform));
                if (n == "HappinessButton") return Loc.F("Happiness {0}, button, shows what affects happiness", value);
                if (n == "HealthButton") return Loc.F("Health {0}, button", value);
                if (n == "StructuralIntegrityButton") return Loc.F("Building integrity {0}, button", value);
            }
            if (!item.IsControl && item.Go.name == "TotalText" && item.Go.GetComponentInParent<PopulationUI>() != null)
                return Loc.F("Population {0}", UIText.JoinTexts(item.Texts));
            var tax = item.Go.GetComponentInParent<TaxRateUI>();
            if (tax != null && item.Control != null)
            {
                string which = item.Control == tax.increase ? Loc.T("Increase tax rate") : (item.Control == tax.decrease ? Loc.T("Decrease tax rate") : null);
                if (which != null) return TextUtil.Join(", ", which, Game.Status.TaxLine(), item.Control.interactable ? null : Loc.T("unavailable, build a Treasure Room first, Castle category"));
            }
            return null;
        }

        private static string PriorityRow(BuildPriorityItem row)
        {
            bool on = row.DisableToggle.isOn;
            string prio = on && !string.IsNullOrEmpty(row.Priority.text) ? Loc.F("Priority {0}", row.Priority.text) : Loc.T("Disabled");
            string name = TextUtil.Clean(row.Name != null ? row.Name.text : row.Category.ToString());
            string filled = row.FilledWorkers != null ? TextUtil.Clean(row.FilledWorkers.text) : string.Empty;
            string allowed = row.AvailableWorkersInput != null ? row.AvailableWorkersInput.text : string.Empty;
            return TextUtil.Join(", ", prio, name, filled.Length > 0 ? Loc.F("{0} workers", filled) : null, allowed.Length > 0 ? Loc.F("allowed {0} of {1}", allowed, row.MaxAvailableJobs) : null);
        }

        private static string ResourceName(InfoBase info)
        {
            switch (info)
            {
                case WoodInfo _: return Loc.T("Wood");
                case StoneInfo _: return Loc.T("Stone");
                case CharcoalInfo _: return Loc.T("Charcoal");
                case FoodInfo _: return Loc.T("Food");
                case IronInfo _: return Loc.T("Iron");
                case ToolInfo _: return Loc.T("Tools");
                case ArmamentInfo _: return Loc.T("Armaments");
                case GoldInfo _: return Loc.T("Gold");
                default: return TextUtil.Humanize(info.GetType().Name.Replace("Info", ""));
            }
        }

        /// <summary>Extra keys for special rows. Returns true when handled.</summary>
        internal static bool HandleKey(UINavigator nav, UIItem item)
        {
            if (item == null || item.Go == null) return false;
            var stop = item.Go.GetComponentInParent<LogisticsDestUI>();
            if (stop != null && Game.Routes.HandleKey(stop, item)) return true;
            if (KInput.Plain(KeyCode.Delete))
            {
                // Save / load slots: Delete removes the save (the game asks for confirmation).
                var slot = item.Go.GetComponentInParent<Assets.Code.UI.SaveLoadOption>();
                if (slot != null)
                {
                    KInput.Consume(KeyCode.Delete);
                    if (slot.deleteButton != null && slot.deleteButton.gameObject.activeInHierarchy && slot.deleteButton.interactable)
                    {
                        A.Cue(Cue.Activate);
                        UINavigator.Click(slot.deleteButton.gameObject);
                        // Start the confirmation on the safe answer.
                        AccessController.Inst.Nav.RequestFocus(g =>
                        {
                            var c = g.GetComponentInParent<Assets.Code.UI.Confirmation>();
                            return c != null && c.noButton != null && g == c.noButton.gameObject;
                        });
                    }
                    else
                    {
                        A.Cue(Cue.Error);
                        A.Say(Loc.T("This entry cannot be deleted"));
                    }
                    return true;
                }
            }
            if (KInput.Plain(KeyCode.Escape))
            {
                // Escape closes the language list (the game only closes it with a click outside it).
                var picker = UnityEngine.Object.FindObjectOfType<ChangeLanguage>();
                if (picker != null && picker.dropdownList != null && picker.dropdownList.activeInHierarchy && item.Go.transform.IsChildOf(picker.dropdownList.transform))
                {
                    KInput.Consume(KeyCode.Escape);
                    picker.dropdownList.SetActive(false);
                    A.Cue(Cue.Close);
                    var btn = picker.gameObject;
                    nav.RequestFocus(g => g == btn);
                    return true;
                }
            }
            var lang = item.IsControl ? item.Go.GetComponent<ChangeLanguage>() : null;
            if (lang != null && lang.dropdownList != null && (KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.Space)))
            {
                // The language picker opens a list elsewhere on the screen: open it and put focus on the current language.
                KInput.Consume(KeyCode.Return);
                KInput.Consume(KeyCode.Space);
                UINavigator.Click(item.Go);
                ModLanguage.AddModOnlyButtons(lang);
                string current = I2.Loc.LocalizationManager.CurrentLanguage;
                string modButton = ModLanguage.ActiveModOnly ? ModLanguage.ButtonName(ModLanguage.ActiveCode) : null;
                var list = lang.dropdownList.transform;
                A.Cue(Cue.Open);
                A.Say(Loc.T("Language list. Up and Down choose, Enter switches the game to that language. Languages marked screen reader only change only what the mod says."), force: true);
                var currentButton = LanguageButton(lang, current);
                nav.RequestFocus(g => g.transform.IsChildOf(list) && g.GetComponent<Button>() != null
                    && (modButton != null ? g.name == modButton
                        : currentButton != null ? g == currentButton.gameObject : UIText.LabelOf(g.GetComponent<Button>()).StartsWith(current)));
                return true;
            }
            if (item.IsControl && SpeedToggleName(item.Control) is string speedName && (KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.Space) || KInput.Plain(KeyCode.KeypadEnter)))
            {
                // Like a radio button: Enter selects it (turning the selected one off again left no speed chosen).
                KInput.Consume(KeyCode.Return);
                KInput.Consume(KeyCode.Space);
                var tg = (Toggle)item.Control;
                if (!tg.interactable)
                {
                    A.Cue(Cue.Error);
                    A.Say(speedName + ", " + Loc.T("unavailable"));
                }
                else if (tg.isOn) A.Say(speedName + ", " + Loc.T("already selected"));
                else
                {
                    A.Cue(Cue.Activate);
                    tg.isOn = true; // the game's handler sets the speed; the change is announced by GameEvents
                }
                return true;
            }
            if (item.IsControl && ResourceToggle(item.Control, out _) is ResourceToggleButton rt && (KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.Space) || KInput.Plain(KeyCode.KeypadEnter)))
            {
                KInput.Consume(KeyCode.Return);
                KInput.Consume(KeyCode.Space);
                if (!item.Control.interactable)
                {
                    A.Cue(Cue.Error);
                    A.Say(Loc.T("unavailable"));
                    return true;
                }
                UINavigator.Click(item.Go);
                A.Cue(rt.State ? Cue.ToggleOn : Cue.ToggleOff);
                A.Say(rt.State ? Loc.T("checked") : Loc.T("not checked"), force: true);
                return true;
            }
            var keyBtn = item.IsControl ? item.Go.GetComponentInParent<KeyButton>() : null;
            if (IsKeyRow(keyBtn) && (KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.Space) || KInput.Plain(KeyCode.KeypadEnter)))
            {
                KInput.Consume(KeyCode.Return);
                KInput.Consume(KeyCode.Space);
                pendingKeyButton = keyBtn;
                return true;
            }
            if ((KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.Space)) && item.IsControl && ResearchRow(item.Go, out _, out var up))
            {
                var b = GameUI.inst.GetBuildingSelected();
                var lib = b != null ? b.GetComponent<GreatLibrary>() : null;
                int gold = Player.inst.PlayerLandmassOwner.Gold;
                if (lib != null && gold < lib.GetCost(up))
                {
                    KInput.Consume(KeyCode.Space);
                    KInput.Consume(KeyCode.Return);
                    A.Cue(Cue.Error);
                    A.Say(Loc.F("Not enough gold: costs {0}, you have {1}", lib.GetCost(up), gold), force: true);
                    return true;
                }
                return false; // normal click starts the research; the new state is read automatically
            }
            var mrow = item.Go.GetComponentInParent<ResourceLineItemUI>();
            if (mrow != null && item.Control == mrow.orderAmt && KInput.NoMods)
            {
                int delta = 0;
                if (KInput.Down(KeyCode.RightArrow)) delta = 1;
                else if (KInput.Down(KeyCode.LeftArrow)) delta = -1;
                else if (KInput.Down(KeyCode.PageUp)) delta = 10;
                else if (KInput.Down(KeyCode.PageDown)) delta = -10;
                if (delta != 0)
                {
                    int before = mrow.GetOrderAmount();
                    mrow.orderAmt.text = Mathf.Max(0, before + delta).ToString();
                    mrow.ClampOrder();
                    int after = mrow.GetOrderAmount();
                    if (after == before)
                    {
                        A.Cue(Cue.Edge);
                        string why = after == 0 ? Loc.T("none") : (after >= mrow.availableAmount ? Loc.T("all available") : Loc.T("not enough gold for more"));
                        A.Say(after + ", " + why);
                    }
                    else
                    {
                        A.Cue(Cue.Value);
                        A.Say(after + (after > 0 ? ", " + (mrow.buy ? Loc.F("costs {0} gold", mrow.GetCost()) : Loc.F("earns {0} gold", mrow.GetCost())) : string.Empty));
                    }
                    return true;
                }
            }
            var info = item.Go.GetComponentInParent<InfoBase>();
            if (info != null && !item.IsControl && (KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.Space)))
            {
                KInput.Consume(KeyCode.Space);
                KInput.Consume(KeyCode.Return);
                Game.StatusMenu.Examine(info);
                return true;
            }
            var row = RowFor(item.Go);
            if (row == null || item.Control != row.DisableToggle) return false;

            if (KInput.WithShift(KeyCode.UpArrow) || KInput.WithShift(KeyCode.DownArrow))
            {
                int dir = KInput.Down(KeyCode.UpArrow) ? -1 : 1;
                Transform t = row.transform;
                Transform parent = t.parent;
                int idx = t.GetSiblingIndex();
                int target = idx + dir;
                while (target >= 0 && target < parent.childCount && (parent.GetChild(target).GetComponent<BuildPriorityItem>() == null || !parent.GetChild(target).gameObject.activeSelf)) target += dir;
                if (target < 0 || target >= parent.childCount)
                {
                    A.Cue(Cue.Edge);
                    A.Say(row.Priority.text.Length > 0 ? Loc.F("Already priority {0}", row.Priority.text) : Loc.T("Cannot move further"));
                    return true;
                }
                t.SetSiblingIndex(target);
                var ui = DecreeUI.inst;
                ui.UpdatePriorityNumbers();
                ui.SavePriority();
                ui.ForceItemPositions();
                A.Cue(Cue.Navigate);
                nav.Refresh(force: true);
                A.Say(PriorityRow(row));
                return true;
            }
            if (KInput.Plain(KeyCode.RightArrow) || KInput.Plain(KeyCode.LeftArrow))
            {
                int delta = KInput.Down(KeyCode.RightArrow) ? 1 : -1;
                row.AddToAvailable(delta);
                A.Cue(Cue.Value);
                A.Say(Loc.F("allowed {0} of {1}", row.AvailableWorkersInput.text, row.MaxAvailableJobs));
                return true;
            }
            if (KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.Space))
            {
                KInput.Consume(KeyCode.Space);
                KInput.Consume(KeyCode.Return);
                row.DisableToggle.isOn = !row.DisableToggle.isOn;
                DecreeUI.inst.UpdatePriorityNumbers();
                DecreeUI.inst.SavePriority();
                A.Cue(row.DisableToggle.isOn ? Cue.ToggleOn : Cue.ToggleOff);
                A.Say(PriorityRow(row));
                return true;
            }
            return false;
        }

        /// <summary>Extra help for a focused special row, appended to F1.</summary>
        internal static string Help(UIItem item)
        {
            if (item == null || item.Go == null) return null;
            var stop = item.Go.GetComponentInParent<LogisticsDestUI>();
            if (stop != null) return Game.Routes.Help(stop);
            if (item.Control is Slider ns && NegotiationResource(ns) != null)
                return Loc.T("On a price: Left and Right change it, the other kingdom's mood is said with it. Lower prices make them happier.");
            if (item.Go.GetComponentInParent<ResourceLineItemUI>() != null)
                return Loc.T("On a trade line: Left and Right change the amount by 1, Page Up and Page Down by 10, Enter lets you type an amount. Then choose the complete transaction button.");
            if (item.Go.GetComponentInParent<Assets.Code.UI.SaveLoadOption>() != null)
                return Loc.T("On a saved game: Enter loads or overwrites it, Delete deletes it after a confirmation.");
            if (RowFor(item.Go) != null)
                return Loc.T("On a job row: Space toggles the job on or off, Shift Up and Shift Down change its priority, Left and Right change how many workers are allowed.");
            return null;
        }
    }
}
