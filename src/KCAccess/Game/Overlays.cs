using HarmonyLib;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// The happiness, health and building integrity maps (the three buttons of the kingdom overview, F6). The game only
    /// colours the ground and the buildings and switches a map off silently (Escape, right click, another map). The mod
    /// says when a map turns on or off, and while one is on the cursor also reads what it shows on each tile and
    /// Ctrl+arrows stop where that value changes.
    /// </summary>
    internal static class Overlays
    {
        internal enum Kind { None, Happiness, Health, Integrity }

        /// <summary>Set while a map button switches maps, so the map it turns off on the way is not announced too.</summary>
        private static bool toggling;

        internal static Kind Active
        {
            get
            {
                if (HappinessUI.inst != null && HappinessUI.inst.overlayOn) return Kind.Happiness;
                if (HealthUI.inst != null && HealthUI.inst.overlayOn) return Kind.Health;
                if (IntegrityUI.inst != null && IntegrityUI.inst.overlayOn) return Kind.Integrity;
                return Kind.None;
            }
        }

        private static string OnText(Kind k)
        {
            switch (k)
            {
                case Kind.Happiness: return Loc.T("Happiness map on: the cursor reads the happiness bonus of each tile and the happiness of each house. Escape turns it off.");
                case Kind.Health: return Loc.T("Health map on: the cursor reads the health of the residents of each house. Escape turns it off.");
                default: return Loc.T("Building integrity map on: the cursor reads the integrity of each building that needs repairs from masons. Escape turns it off.");
            }
        }

        private static string OffText(Kind k)
        {
            switch (k)
            {
                case Kind.Happiness: return Loc.T("Happiness map off");
                case Kind.Health: return Loc.T("Health map off");
                default: return Loc.T("Building integrity map off");
            }
        }

        /// <summary>What the map button of the kingdom overview does now.</summary>
        internal static string ButtonHint(Kind k) => Active == k ? Loc.T("its map is on, Enter turns it off") : Loc.T("Enter shows its map");

        internal static void BeginToggle() => toggling = true;

        internal static void EndToggle() => toggling = false;

        internal static void Toggled(Kind k, bool on) => A.Say(on ? OnText(k) : OffText(k), force: true);

        /// <summary>A map was switched off by something other than its own button (Escape, right click, closing the panel).</summary>
        internal static void Hidden(Kind k)
        {
            if (!toggling) A.Say(OffText(k));
        }

        /// <summary>
        /// Escape on the map: the game itself only switches the happiness map off with Escape and opens the pause menu
        /// over the other two, so Escape switches any map off first. True when it did.
        /// </summary>
        internal static bool HideOnEscape()
        {
            switch (Active)
            {
                case Kind.Happiness: HappinessUI.inst.HideOverlay(); return true;
                case Kind.Health: HealthUI.inst.HideOverlay(); return true;
                case Kind.Integrity: IntegrityUI.inst.HideOverlay(); return true;
                default: return false;
            }
        }

        /// <summary>What the map that is on shows on this tile, or null (no map on, or nothing coloured there).</summary>
        internal static string TileText(Cell c)
        {
            var kind = Active;
            if (kind == Kind.None || c == null || Player.inst == null) return null;
            try
            {
                switch (kind)
                {
                    case Kind.Happiness:
                    {
                        var home = PlayerHome(c);
                        if (home != null) return home.Residents.Count == 0 ? Loc.T("no residents") : Loc.F("house happiness {0}", home.GetHappiness());
                        int bonus;
                        return HappinessBonus(c, out bonus) ? Loc.F("happiness bonus {0}", bonus) : null;
                    }
                    case Kind.Health:
                    {
                        var home = PlayerHome(c);
                        if (home == null) return null;
                        return home.Residents.Count == 0 ? Loc.T("no residents") : Loc.F("residents' health {0}", Mathf.RoundToInt(home.GetHealth() * 100f));
                    }
                    default:
                    {
                        var b = DecayingBuilding(c);
                        return b == null ? null : Loc.F("integrity {0}", TextUtil.Percent(Integrity(b)));
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Map value failed: " + e.Message);
                return null;
            }
        }

        /// <summary>The value of the map that is on, for Ctrl+arrows (they stop where it changes); empty when no map is on.</summary>
        internal static string SignaturePart(Cell c)
        {
            if (Active == Kind.None) return string.Empty;
            return "|" + (TileText(c) ?? string.Empty);
        }

        /// <summary>
        /// The happiness that buildings around (churches, gardens, taverns and so on) give a house on this tile; the
        /// happiness map colours the ground with it. False when no such building reaches the tile.
        /// </summary>
        internal static bool HappinessBonus(Cell c, out int bonus)
        {
            bonus = 0;
            if (c.Type == ResourceType.Water && c.OccupyingStructure.Count == 0 || !Player.inst.LandMassIsAPlayerLandMass(c.landMassIdx)) return false;
            bool any;
            bonus = RadiusBonus.GetHappinessAt(c.x, c.z, out any);
            return any;
        }

        /// <summary>Life left as a share of the most a building can have (stacked castle blocks have more).</summary>
        internal static float Integrity(Building b) => b.ModifiedMaxLife > 0f ? Mathf.Clamp01(b.Life / b.ModifiedMaxLife) : b.Life;

        private static Home PlayerHome(Cell c)
        {
            foreach (var b in c.OccupyingStructure)
            {
                if (b == null || !b.IsBuilt() || b.TeamID() != 0 || !Player.inst.LandMassIsAPlayerLandMass(b.LandMass())) continue;
                var home = b.GetComponent<Home>();
                if (home != null) return home;
            }
            return null;
        }

        /// <summary>The player's building on the tile that wears down and is coloured by the integrity map, top first.</summary>
        private static Building DecayingBuilding(Cell c)
        {
            for (int i = c.OccupyingStructure.Count - 1; i >= 0; i--)
            {
                var b = c.OccupyingStructure[i];
                if (b != null && b.decayChance != 0 && b.Life > 0f && b.TeamID() == 0 && Player.inst.LandMassIsAPlayerLandMass(b.LandMass())) return b;
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(HappinessUI), nameof(HappinessUI.OnClickedShowOverlay))]
    internal static class Patch_HappinessMapButton
    {
        private static void Prefix() => Overlays.BeginToggle();
        private static void Postfix(HappinessUI __instance) => Overlays.Toggled(Overlays.Kind.Happiness, __instance.overlayOn);
        private static void Finalizer() => Overlays.EndToggle();
    }

    [HarmonyPatch(typeof(HealthUI), nameof(HealthUI.OnClickedShowOverlay))]
    internal static class Patch_HealthMapButton
    {
        private static void Prefix() => Overlays.BeginToggle();
        private static void Postfix(HealthUI __instance) => Overlays.Toggled(Overlays.Kind.Health, __instance.overlayOn);
        private static void Finalizer() => Overlays.EndToggle();
    }

    [HarmonyPatch(typeof(IntegrityUI), nameof(IntegrityUI.OnClickedShowOverlay))]
    internal static class Patch_IntegrityMapButton
    {
        private static void Prefix() => Overlays.BeginToggle();
        private static void Postfix(IntegrityUI __instance) => Overlays.Toggled(Overlays.Kind.Integrity, __instance.overlayOn);
        private static void Finalizer() => Overlays.EndToggle();
    }

    [HarmonyPatch(typeof(HappinessUI), nameof(HappinessUI.HideOverlay))]
    internal static class Patch_HappinessMapHidden
    {
        private static void Prefix(HappinessUI __instance, out bool __state) => __state = __instance.overlayOn;
        private static void Postfix(HappinessUI __instance, bool __state)
        {
            if (__state && !__instance.overlayOn) Overlays.Hidden(Overlays.Kind.Happiness);
        }
    }

    [HarmonyPatch(typeof(HealthUI), nameof(HealthUI.HideOverlay))]
    internal static class Patch_HealthMapHidden
    {
        private static void Prefix(HealthUI __instance, out bool __state) => __state = __instance.overlayOn;
        private static void Postfix(HealthUI __instance, bool __state)
        {
            if (__state && !__instance.overlayOn) Overlays.Hidden(Overlays.Kind.Health);
        }
    }

    [HarmonyPatch(typeof(IntegrityUI), nameof(IntegrityUI.HideOverlay))]
    internal static class Patch_IntegrityMapHidden
    {
        private static void Prefix(IntegrityUI __instance, out bool __state) => __state = __instance.overlayOn;
        private static void Postfix(IntegrityUI __instance, bool __state)
        {
            if (__state && !__instance.overlayOn) Overlays.Hidden(Overlays.Kind.Integrity);
        }
    }
}
