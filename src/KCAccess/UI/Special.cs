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
                case "Food": return "Agriculture advisor";
                case "City": return "City advisor";
                case "Military": return "Military advisor";
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

        private static string ResearchDescription(Transform row, Player.UpgradeType upgrade)
        {
            string name = TextUtil.Clean(UIText.TextOf(row.GetChild(0).GetComponent<TMPro.TMP_Text>()));
            if (string.IsNullOrEmpty(name)) name = TextUtil.Humanize(upgrade.ToString());
            if (row.childCount > 3 && row.GetChild(3).gameObject.activeSelf) return name + ", already researched";
            var b = GameUI.inst.GetBuildingSelected();
            var lib = b != null ? b.GetComponent<GreatLibrary>() : null;
            string cost = lib != null ? ", costs " + lib.GetCost(upgrade) + " gold" : string.Empty;
            string tip = UIText.TooltipOf(row.gameObject);
            return TextUtil.Join(", ", name + cost, "button, Enter starts the research", tip);
        }

        /// <summary>Merchant buy / sell line: the resource is only an icon, so build the description from the row data.</summary>
        private static string MerchantRow(ResourceLineItemUI row)
        {
            string res = ResourceNames.Name(row.rtype.ToString());
            int order = row.GetOrderAmount();
            string verb = row.buy ? "buy" : "sell";
            return res + ": " + row.price + " gold each, " + row.availableAmount + " available, " + verb + " " + order
                   + (order > 0 ? ", " + (row.buy ? "costs " : "earns ") + row.GetCost() + " gold" : string.Empty)
                   + ". Left and Right change the amount by 1, Page Up and Page Down by 10";
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
            return TextUtil.Humanize(((InputActions)kb.buttonNum).ToString());
        }

        private static string KeyName(KeyButton kb)
        {
            var t = kb.myButton != null ? kb.myButton.GetComponentInChildren<TMPro.TMP_Text>() : null;
            string k = t != null ? TextUtil.Clean(UIText.TextOf(t)) : string.Empty;
            return string.IsNullOrEmpty(k) ? "no key" : k.ToLowerInvariant().Replace("alpha", "");
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
                A.Say("Press the new key for " + KeyAction(capturingKeyButton) + ". You can hold Control, Shift or Alt with it.", force: true);
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
                    A.Say(KeyAction(kb) + " is now " + KeyName(kb), force: true);
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
        internal static string Describe(UIItem item)
        {
            if (item == null || item.Go == null) return null;
            if (item.IsControl && item.Go.GetComponentInParent<PersonListItemUI>() is PersonListItemUI pli && pli.MagGlass != null && item.Control == pli.MagGlass)
            {
                string who = pli.Description != null ? TextUtil.Clean(UIText.TextOf(pli.Description)) : "villager";
                bool hoh = pli.HeadOfHouseholdIcon != null && pli.HeadOfHouseholdIcon.activeSelf;
                return who + (hoh ? ", head of household" : string.Empty) + ", button, Enter selects this villager";
            }
            if (item.Go.name == "SummaryTitle" && item.Go.transform.parent != null && item.Go.transform.parent.GetComponent<FireRiskUI>() is FireRiskUI fr)
            {
                string risk = fr.tooltip != null ? TextUtil.Clean(fr.tooltip.toolTipText ?? string.Empty) : string.Empty;
                return UIText.TextOf(item.Go.GetComponent<TMPro.TMP_Text>()) + ": " + (risk.Length > 0 ? risk : "unknown");
            }
            var row = RowFor(item.Go);
            if (row != null && item.Control == row.DisableToggle) return PriorityRow(row);
            if (item.Control is Button && item.Go.GetComponentInParent<PickNameUI>() != null && UIText.LabelOf(item.Control) == "unlabelled")
                return "Choose banner, button";
            var kbd = item.IsControl ? item.Go.GetComponentInParent<KeyButton>() : null;
            if (IsKeyRow(kbd)) return KeyAction(kbd) + ": " + KeyName(kbd) + ", button, Enter to change";
            if (item.IsControl && item.Go.name == "SeedInput" && !UIText.LabelOf(item.Control).ToLowerInvariant().Contains("seed"))
                return "Map seed, edit, " + UIText.ValueOf(item.Control);
            var mrow = item.Go.GetComponentInParent<ResourceLineItemUI>();
            if (mrow != null && item.Control != null && item.Control == mrow.orderAmt) return MerchantRow(mrow);
            if (item.IsControl && OrderRow(item.Go, out var orow, out var otype))
            {
                string res = ResourceNames.Name(otype.ToString());
                string stored = TextUtil.Clean(UIText.TextOf(orow.GetChild(2).GetComponent<TMPro.TMP_Text>()));
                if (item.Control is TMPro.TMP_InputField f)
                    return res + ": " + stored + " stored, desired amount " + (string.IsNullOrEmpty(f.text) ? "0" : f.text) + ", edit, Enter to type a new amount";
                if (item.Control is Toggle tg)
                    return res + ": keep for transport only, check box, " + (tg.isOn ? "checked" : "not checked") + (UIText.TooltipOf(item.Go) is string tip ? ". " + tip : string.Empty);
            }
            if (ResearchRow(item.Go, out var researchRow, out var upgrade)) return ResearchDescription(researchRow, upgrade);
            if (item.IsControl && item.Go.GetComponentInParent<RivalItemUI>() is RivalItemUI rival)
            {
                int slot = 1;
                var all = RivalKingdomSettingsUI.inst != null ? RivalKingdomSettingsUI.inst.rivalItems : null;
                if (all != null) for (int i = 0; i < all.Length; i++) if (all[i] == rival) slot = i + 1;
                string rivalName = rival.rivalName != null ? TextUtil.Clean(UIText.TextOf(rival.rivalName)) : null;
                if (item.Control == rival.enableButton) return "Add AI kingdom, slot " + slot + ", button";
                if (item.Control == rival.removeButton) return "Remove AI kingdom " + (rivalName ?? string.Empty) + ", slot " + slot + ", button";
                if (item.Control == rival.personalityDropdown) return "AI kingdom " + rivalName + " skill level, combo box, " + UIText.ValueOf(rival.personalityDropdown);
            }
            if (item.IsControl && item.Go.GetComponentInParent<DemolishWarningUI>() != null)
            {
                if (item.Go.name == "Yes") return "Yes, demolish, button";
                if (item.Go.name == "No") return "No, cancel, button";
            }
            if (item.IsControl && item.Go.GetComponent<PixelCrushers.DialogueSystem.StandardUIContinueButtonFastForward>() != null)
                return "Continue, button";
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
                string group = ui != null && parent == ui.customBannerContainer ? "Custom banner " : (ui != null && parent == ui.workshopBannerContainer ? "Workshop banner " : "Banner ");
                return group + idx + " of " + count + (selected ? ", selected" : string.Empty) + ". Enter selects it, then choose Accept";
            }
            if (item.IsControl && item.Go.name == "FolderButton" && item.Go.GetComponentInParent<ChooseBannerUI>() != null)
                return "Open custom banner folder, button";
            string advisor = item.IsControl ? AdvisorName(item.Go.transform) : null;
            if (advisor != null) return advisor + ", button, press Enter to hear their advice";
            if (item.IsControl && AdvisorUI.inst != null && item.Go.transform.IsChildOf(AdvisorUI.inst.transform) && UIText.LabelOf(item.Control) == "unlabelled" || item.IsControl && AdvisorUI.inst != null && item.Go.transform.IsChildOf(AdvisorUI.inst.transform) && UIText.LabelOf(item.Control) == "Button")
                return "Close, button";
            var info = item.Go.GetComponentInParent<InfoBase>();
            if (info != null && !item.IsControl)
            {
                return TextUtil.Join(" ", ResourceName(info), UIText.JoinTexts(item.Texts)) + ", press Enter for the yearly report";
            }
            if (item.IsControl)
            {
                string n = item.Go.name;
                string value = UIText.JoinTexts(UIText.VisibleTexts(item.Go.transform));
                if (n == "HappinessButton") return "Happiness " + value + ", button, shows what affects happiness";
                if (n == "HealthButton") return "Health " + value + ", button";
                if (n == "StructuralIntegrityButton") return "Building integrity " + value + ", button";
            }
            if (!item.IsControl && item.Go.name == "TotalText" && item.Go.GetComponentInParent<PopulationUI>() != null)
                return "Population " + UIText.JoinTexts(item.Texts);
            var tax = item.Go.GetComponentInParent<TaxRateUI>();
            if (tax != null && item.Control != null)
            {
                string which = item.Control == tax.increase ? "Increase tax rate" : (item.Control == tax.decrease ? "Decrease tax rate" : null);
                if (which != null) return TextUtil.Join(", ", which, Game.Status.TaxLine(), item.Control.interactable ? null : "unavailable, build a Treasure Room first, Castle category");
            }
            return null;
        }

        private static string PriorityRow(BuildPriorityItem row)
        {
            bool on = row.DisableToggle.isOn;
            string prio = on && !string.IsNullOrEmpty(row.Priority.text) ? "Priority " + row.Priority.text : "Disabled";
            string name = TextUtil.Clean(row.Name != null ? row.Name.text : row.Category.ToString());
            string filled = row.FilledWorkers != null ? TextUtil.Clean(row.FilledWorkers.text) : string.Empty;
            string allowed = row.AvailableWorkersInput != null ? row.AvailableWorkersInput.text : string.Empty;
            return TextUtil.Join(", ", prio, name, filled.Length > 0 ? filled + " workers" : null, allowed.Length > 0 ? "allowed " + allowed + " of " + row.MaxAvailableJobs : null);
        }

        private static string ResourceName(InfoBase info)
        {
            switch (info)
            {
                case WoodInfo _: return "Wood";
                case StoneInfo _: return "Stone";
                case CharcoalInfo _: return "Charcoal";
                case FoodInfo _: return "Food";
                case IronInfo _: return "Iron";
                case ToolInfo _: return "Tools";
                case ArmamentInfo _: return "Armaments";
                case GoldInfo _: return "Gold";
                default: return TextUtil.Humanize(info.GetType().Name.Replace("Info", ""));
            }
        }

        /// <summary>Extra keys for special rows. Returns true when handled.</summary>
        internal static bool HandleKey(UINavigator nav, UIItem item)
        {
            if (item == null || item.Go == null) return false;
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
                    A.Say("Not enough gold: costs " + lib.GetCost(up) + ", you have " + gold, force: true);
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
                        string why = after == 0 ? "none" : (after >= mrow.availableAmount ? "all available" : "not enough gold for more");
                        A.Say(after + ", " + why);
                    }
                    else
                    {
                        A.Cue(Cue.Value);
                        A.Say(after + (after > 0 ? ", " + (mrow.buy ? "costs " : "earns ") + mrow.GetCost() + " gold" : string.Empty));
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
                    A.Say(row.Priority.text.Length > 0 ? "Already priority " + row.Priority.text : "Cannot move further");
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
                A.Say("allowed " + row.AvailableWorkersInput.text + " of " + row.MaxAvailableJobs);
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
            if (item.Go.GetComponentInParent<ResourceLineItemUI>() != null)
                return "On a trade line: Left and Right change the amount by 1, Page Up and Page Down by 10, Enter lets you type an amount. Then choose the complete transaction button.";
            if (RowFor(item.Go) != null)
                return "On a job row: Space toggles the job on or off, Shift Up and Shift Down change its priority, Left and Right change how many workers are allowed.";
            return null;
        }
    }
}
