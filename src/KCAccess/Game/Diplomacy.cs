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
            var ui = GameUI.inst != null ? GameUI.inst.diplomacyUI : null;
            if (ui == null || item == null || item.Go == null || !ui.Visible()) return null;
            if (item.Control is Button b)
            {
                if (b == ui.missionInfoToggle)
                    return Loc.T("Their request to you") + ", " + Loc.T("button") + ", " + (ui.missionRoot.activeSelf ? Loc.T("shown") : Loc.T("hidden"));
                if (b == ui.resourceInfoToggle)
                    return Loc.T("Their trade prices") + ", " + Loc.T("button") + ", " + (ui.resourceCostRoot.activeSelf ? Loc.T("shown") : Loc.T("hidden"));
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

        /// <summary>Resource of the n-th entry of the game's price lists (every resource but gold and the dead).</summary>
        private static string ResourceAt(int n)
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
