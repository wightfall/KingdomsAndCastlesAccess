using HarmonyLib;
using KCAccess.Core;
using UnityEngine;
using UnityEngine.UI;
using KCAccess.UI;

namespace KCAccess.Game
{
    /// <summary>
    /// Visits of the player's envoy to a foreign keep. The game opens the diplomacy window when the envoy arrives only if
    /// the envoy is on screen in that frame (Envoy.Update tests the camera frustum) and then forgets the visit either way,
    /// so with the camera elsewhere (the usual case without sight) the walk was wasted. The visit opens now wherever the
    /// camera is.
    /// </summary>
    internal static class Diplomacy
    {
        /// <summary>The foreign keep or hall of diplomacy the player's envoy was sent to, or null.</summary>
        internal static Building VisitTarget(Envoy e)
        {
            if (e == null || e.general == null || e.general.army == null || e.general.army.teamId != 0) return null;
            var b = e.general.army.moveTarget as Building;
            if (b == null || b.TeamID() == e.general.army.TeamID()) return null;
            return b.uniqueNameHash == World.keepHash || b.uniqueNameHash == World.hallofdiplomacyHash ? b : null;
        }

        /// <summary>Opens the diplomacy window for the envoy's visit, as the game does when it sees the envoy arrive.</summary>
        internal static bool OpenVisit(Envoy e, Building target)
        {
            if (e == null || target == null || GameUI.inst == null || GameUI.inst.diplomacyUI == null) return false;
            int lm = target.LandMass();
            var kingdom = AIKingdom.GetKingdomByLandmass(lm);
            if (kingdom == null) return false;
            GameUI.inst.diplomacyUI.Show(e, World.GetLandmassOwner(lm), lm, kingdom);
            Cam.inst.SetDesiredTrackingPos(target.Center());
            return true;
        }

        /// <summary>
        /// The diplomacy window's own controls and texts that are pictures or bare numbers: the mission and price
        /// buttons (icons), the other kingdom's price list (numbers next to resource icons) and the Propose prices
        /// button of the price editor (with the overall verdict). Null for anything else.
        /// </summary>
        internal static string Describe(UIItem item)
        {
            if (ForeignIslandButton(item) is string foreign) return foreign;
            var ui = GameUI.inst != null ? GameUI.inst.diplomacyUI : null;
            if (ui == null || item == null || item.Go == null || !ui.Visible()) return null;
            // The opinion bar is the progress towards the next level ("Neutral, 80 percent" was read as an 80 percent opinion).
            if (item.Control is Slider bar && bar == ui.statusProgress && ui.kingdom != null && Player.inst != null)
            {
                var lmo = ui.kingdom.LandmassOwner;
                int team = Player.inst.PlayerLandmassOwner.teamId;
                int points = Mathf.Clamp(lmo.GetPointsStandingFor(team), 0, 100);
                // One whole sentence per level, so every language can use the right grammar for each level word.
                switch (lmo.GetStandingFor(team))
                {
                    case LandmassOwner.Standing.VeryUnfavorable: return Loc.F("Opinion of you: very unfavorable, {0} percent of the way to unfavorable", points);
                    case LandmassOwner.Standing.Unfavorable: return Loc.F("Opinion of you: unfavorable, {0} percent of the way to neutral", points);
                    case LandmassOwner.Standing.Neutral: return Loc.F("Opinion of you: neutral, {0} percent of the way to favorable", points);
                    case LandmassOwner.Standing.Favorable: return Loc.F("Opinion of you: favorable, {0} percent of the way to very favorable", points);
                    default: return Loc.T("Opinion of you: very favorable, the best there is");
                }
            }
            if (item.Control is Button b)
            {
                if (b == ui.missionInfoToggle)
                    return Loc.T("Their request to you") + ", " + Loc.T("button") + ", " + (ui.missionRoot.activeSelf ? Loc.T("shown") : Loc.T("hidden"));
                if (b == ui.resourceInfoToggle)
                    return Loc.T("Their trade prices") + ", " + Loc.T("button") + ", " + (ui.resourceCostRoot.activeSelf ? Loc.T("shown") : Loc.T("hidden"));
                if (ui.feastMenu != null && b.transform.IsChildOf(ui.feastMenu.transform))
                {
                    var menu = ui.feastMenu.transform;
                    for (int i = 0; i < menu.childCount && i < FeastFoods.Length; i++)
                    {
                        if (!b.transform.IsChildOf(menu.GetChild(i))) continue;
                        string food = ResourceNames.Name(FeastFoods[i].ToString());
                        return Loc.F("{0}, button, costs 50 {1}", FeastName(i), food) + (b.interactable ? string.Empty : ", " + Loc.T("you do not have enough"));
                    }
                }
                if (ui.negotiationUI != null && b.transform.IsChildOf(ui.negotiationUI.transform))
                    return Loc.T("Propose prices") + ", " + Loc.T("button") + ". " + OfferVerdict();
            }
            if (!item.IsControl && item.Texts != null && ui.resourceCostUI != null)
            {
                foreach (var c in item.Texts)
                {
                    int idx = ui.resourceCostUI.texts.IndexOf(c as TMPro.TMP_Text);
                    if (idx < 0) continue;
                    return Loc.F("{0}: {1} gold", ResourceAt(idx), TextUtil.Clean(UIText.TextOf(c)));
                }
            }
            return null;
        }

        /// <summary>
        /// The foreign island panel's Declare war / Break alliance button: the relation and what it means are shown only
        /// while the mouse rests on the panel (relationExplanationText).
        /// </summary>
        private static string ForeignIslandButton(UIItem item)
        {
            var panel = GameUI.inst != null ? GameUI.inst.foreignIslandInfoUI : null;
            if (panel == null || item == null) return null;
            // The relation and opinion values come before their captions in the panel; read them together.
            if (!item.IsControl && item.Texts != null)
            {
                if (item.Texts.Contains(panel.relationStatusTxt)) return Loc.F("Relationship: {0}", TextUtil.Clean(UIText.TextOf(panel.relationStatusTxt)));
                if (item.Texts.Contains(panel.opinionStatusTxt)) return Loc.F("Their opinion of you: {0}", TextUtil.Clean(UIText.TextOf(panel.opinionStatusTxt)));
            }
            if (item.Control == null || item.Control != panel.hostilityButton) return null;
            string label = TextUtil.Clean(UIText.TextOf(panel.relationButtonTxt));
            string status = TextUtil.Clean(UIText.TextOf(panel.relationStatusTxt));
            string why = TextUtil.Clean(panel.relationExplanationText != null ? panel.relationExplanationText.text : null);
            return TextUtil.Join(". ", label + ", " + Loc.T("button"), TextUtil.HasContent(status) ? Loc.F("Now: {0}", status) : null, TextUtil.HasContent(why) ? why : null);
        }

        private static float confirmUntil;
        private static Selectable confirmFor;

        /// <summary>
        /// Declaring war or breaking an alliance happens on the first click in the game and cannot be undone: the first
        /// Enter only asks, a second Enter within 5 seconds does it. True when the press was held back.
        /// </summary>
        internal static bool HoldBack(Selectable s)
        {
            var panel = GameUI.inst != null ? GameUI.inst.foreignIslandInfoUI : null;
            if (panel == null || s == null || s != panel.hostilityButton) return false;
            if (confirmFor == s && Time.unscaledTime <= confirmUntil)
            {
                confirmFor = null;
                return false;
            }
            confirmFor = s;
            confirmUntil = Time.unscaledTime + 5f;
            A.Cue(Cue.Error);
            A.Say(Loc.F("{0}? This cannot be undone. Press Enter again within 5 seconds to confirm.", TextUtil.Clean(UIText.TextOf(panel.relationButtonTxt))), force: true);
            return true;
        }

        /// <summary>The feast menu's buttons in order (DiplomacyUI.ShowFoodMenu: bread, apples, fish, pork).</summary>
        internal static readonly FreeResourceType[] FeastFoods = { FreeResourceType.Wheat, FreeResourceType.Apples, FreeResourceType.Fish, FreeResourceType.Pork };

        internal static string FeastName(int i)
        {
            switch (i)
            {
                case 0: return Loc.T("Bread feast");
                case 1: return Loc.T("Apple feast");
                case 2: return Loc.T("Fish feast");
                default: return Loc.T("Pork feast");
            }
        }

        /// <summary>Resource of the n-th entry of the game's price lists (every resource but gold and the dead).</summary>
        internal static string ResourceAt(int n)
        {
            int k = 0;
            for (int i = 0; i < 12; i++)
            {
                var r = (FreeResourceType)i;
                if (r == FreeResourceType.Gold || r == FreeResourceType.DeadVillager) continue;
                if (k++ == n) return TextUtil.Capitalize(ResourceNames.Name(r.ToString()));
            }
            return Loc.T("resource");
        }

        /// <summary>
        /// How the other kingdom takes the prices set in the editor, the way the game decides it
        /// (AINegotiationUI.CalculateFavor: below their usual prices pleases them, above annoys them).
        /// </summary>
        internal static string OfferVerdict()
        {
            var ui = GameUI.inst != null ? GameUI.inst.diplomacyUI : null;
            if (ui == null || ui.negotiationUI == null || !ui.negotiationUI.gameObject.activeInHierarchy) return string.Empty;
            int favor = ui.negotiationUI.CalculateFavor();
            if (favor > 0) return Loc.F("Overall they like these prices, opinion plus {0}", favor);
            if (favor == 0) return Loc.T("Overall these are their usual prices");
            return Loc.F("Overall the prices are too high for them, opinion minus {0}", -favor);
        }

        /// <summary>
        /// Debug command "visit": the player's first envoy visits the first foreign kingdom at once (testing diplomacy
        /// without walking there). Returns what happened, for the log.
        /// </summary>
        internal static string DebugVisit()
        {
            Envoy envoy = null;
            foreach (var e in Object.FindObjectsOfType<Envoy>())
                if (e.general != null && e.general.army != null && e.general.army.teamId == 0) { envoy = e; break; }
            if (envoy == null) return "no envoy of the player (spawn one in creative mode)";
            if (AIBrainsContainer.inst == null || AIBrainsContainer.inst.kingdoms == null || AIBrainsContainer.inst.kingdoms.Count == 0) return "no AI kingdom";
            var kingdom = AIBrainsContainer.inst.kingdoms[0];
            int lm = kingdom.LandmassOwner.ownedLandMasses.Count > 0 ? kingdom.LandmassOwner.ownedLandMasses.data[0] : -1;
            if (lm < 0) return "the AI kingdom has no land";
            GameUI.inst.diplomacyUI.Show(envoy, World.GetLandmassOwner(lm), lm, kingdom);
            return "visit opened at " + kingdom.GetLocalizedName();
        }
    }

    /// <summary>
    /// The price editor opens in the middle of a trade conversation with nothing said: explain it and start on the
    /// first price.
    /// </summary>
    [HarmonyPatch(typeof(DiplomacyUI), nameof(DiplomacyUI.DisplayNegotiationUI))]
    internal static class Patch_NegotiationOpened
    {
        private static void Postfix(DiplomacyUI __instance)
        {
            var editor = __instance.negotiationUI;
            if (editor == null) return;
            A.Cue(Cue.Open);
            A.Say(Loc.T("Price editor: what they pay for each of your resources. Up and Down choose a resource, Left and Right change its price; lower prices please them, higher ones annoy them. Then choose Propose prices."), force: true);
            var first = editor.GetComponentInChildren<Slider>();
            if (first != null && AccessController.Inst != null) AccessController.Inst.Nav.RequestFocus(g => g == first.gameObject);
        }
    }

    /// <summary>
    /// "Can you do better?": the other kingdom lowers some of its prices in the price list, silently. Say which ones.
    /// </summary>
    [HarmonyPatch(typeof(DiplomacyUI), nameof(DiplomacyUI.UpdateAISellPriceUI))]
    internal static class Patch_TheirPricesChanged
    {
        private static void Prefix(DiplomacyUI __instance, out string[] __state)
        {
            var texts = __instance.resourceCostUI != null ? __instance.resourceCostUI.texts : null;
            __state = texts != null ? texts.ConvertAll(t => t != null ? t.text : null).ToArray() : null;
        }

        private static void Postfix(DiplomacyUI __instance, string[] __state)
        {
            var texts = __instance.resourceCostUI != null ? __instance.resourceCostUI.texts : null;
            if (__state == null || texts == null) return;
            var changes = new System.Collections.Generic.List<string>();
            for (int i = 0; i < texts.Count && i < __state.Length; i++)
            {
                string now = texts[i] != null ? texts[i].text : null;
                if (now != null && __state[i] != null && now != __state[i])
                    changes.Add(Loc.F("{0} {1} gold, was {2}", Diplomacy.ResourceAt(i), now, __state[i]));
            }
            if (changes.Count > 0) A.SayQueued(Loc.F("Their new prices: {0}", string.Join(", ", changes.ToArray())));
        }
    }

    /// <summary>The feast menu opens in the middle of a conversation: say so and start on the first food.</summary>
    [HarmonyPatch(typeof(DiplomacyUI), nameof(DiplomacyUI.ShowFoodMenu))]
    internal static class Patch_FeastMenuOpened
    {
        private static void Postfix(DiplomacyUI __instance)
        {
            if (__instance.feastMenu == null) return;
            A.Cue(Cue.Open);
            A.Say(Loc.T("Feast: choose the food to serve. Their favourite food pleases them most."), force: true);
            var first = __instance.feastMenu.GetComponentInChildren<Button>();
            if (first != null && AccessController.Inst != null) AccessController.Inst.Nav.RequestFocus(g => g == first.gameObject);
        }
    }

    /// <summary>
    /// The feast: how the guest liked the food was only shown by the eating animation (all of it for their favourite
    /// food, nothing for the one they dislike), and it changes their opinion.
    /// </summary>
    [HarmonyPatch(typeof(DiplomacyUI), nameof(DiplomacyUI.DoFoodAnim))]
    internal static class Patch_FeastEaten
    {
        private static void Postfix(DiplomacyUI __instance)
        {
            var k = __instance.kingdom;
            if (k == null) return;
            FreeResourceType food;
            switch (__instance.feastType)
            {
                case "bread": food = FreeResourceType.Wheat; break;
                case "apples": food = FreeResourceType.Apples; break;
                case "fish": food = FreeResourceType.Fish; break;
                case "pork": food = FreeResourceType.Pork; break;
                default: return;
            }
            if (k.FavoriteFood == food) A.SayQueued(Loc.T("They eat it all: it is their favourite food."));
            else if (k.LeastFavoriteFood == food) A.SayQueued(Loc.T("They do not touch it: they dislike this food."));
            else A.SayQueued(Loc.T("They eat some of it."));
        }
    }

    [HarmonyPatch(typeof(Envoy), "Update")]
    internal static class Patch_EnvoyVisitOffScreen
    {
        private static void Prefix(Envoy __instance)
        {
            if (!__instance.rightClickNeedsConsumed) return;
            var army = __instance.general != null ? __instance.general.army : null;
            if (army == null || army.teamId != 0 || army.moveStatus != UnitSystem.MoveStatus.Complete) return;
            var target = Diplomacy.VisitTarget(__instance);
            if (target == null) return;
            var r = __instance.GetComponentInChildren<Renderer>();
            if (r != null && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(Cam.inst.GetActiveCamera()), r.bounds)) return; // the game opens it
            if (Diplomacy.OpenVisit(__instance, target)) __instance.rightClickNeedsConsumed = false;
        }
    }
}
