using System;
using System.Collections.Generic;
using HarmonyLib;
using KCAccess.Core;

namespace KCAccess.Game
{
    /// <summary>
    /// Viking raids bring thieves: they take up to 100 gold (200 in survival) from a Treasure Room or a Dragon Roost and
    /// carry it to their ship; killing them on land gives it back. The game shows the theft and the return only as
    /// floating numbers and a sack on the thief. Say when your gold is stolen, recovered or carried off.
    /// </summary>
    internal static class ThiefWatch
    {
        /// <summary>Thieves carrying gold stolen from you.</summary>
        private static readonly HashSet<UnitSystem.Army> fromYou = new HashSet<UnitSystem.Army>();

        /// <summary>Set while a sinking ship takes its passengers down with it.</summary>
        internal static bool Sinking;

        internal static void Reset()
        {
            fromYou.Clear();
            Sinking = false;
        }

        internal static void Stolen(UnitSystem.Army army)
        {
            try
            {
                if (army == null || army.stolenResourceAmount <= 0 || Player.inst == null) return;
                var b = army.moveTarget as Building;
                if (b == null || !Player.inst.LandMassIsAPlayerLandMass(b.LandMass())) return;
                fromYou.Add(army);
                var c = b.GetCell();
                Announce(Loc.F("Thieves stole {0} gold from your {1}! Kill them before they reach their ship to get it back.", army.stolenResourceAmount, b.FriendlyName),
                    c != null ? new GridPos(c.x, c.z) : (GridPos?)null, Severity.Danger);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Theft announcement failed: " + e.Message);
            }
        }

        /// <summary>A thief carrying your gold falls: on land the gold goes to whoever owns that land.</summary>
        internal static void Died(UnitSystem.Army army, UnitSystem.Army.GeneralStatus status)
        {
            try
            {
                if (army == null || !fromYou.Contains(army)) return;
                if (status != UnitSystem.Army.GeneralStatus.Dying && status != UnitSystem.Army.GeneralStatus.FallingDeath) return;
                if (army.generalStatus == status) return; // the game ignores a repeat
                fromYou.Remove(army);
                if (army.stolenResourceAmount <= 0 || army.stolenResourceType != FreeResourceType.Gold) return;
                var c = World.inst.GetCellDataClamped(army.generalPos);
                if (c == null || c.landMassIdx == -1)
                    Announce(Loc.F("The thieves fell at sea: the {0} gold they stole is lost.", army.stolenResourceAmount), null, Severity.Warning);
                else if (Player.inst.LandMassIsAPlayerLandMass(c.landMassIdx))
                    Announce(Loc.F("Thieves killed: {0} stolen gold recovered.", army.stolenResourceAmount), new GridPos(c.x, c.z), Severity.Info);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Theft announcement failed: " + e.Message);
            }
        }

        /// <summary>A thief carrying your gold leaves the game alive: its ship sailed off or sank.</summary>
        internal static void Gone(UnitSystem.Army army)
        {
            try
            {
                if (army == null || !fromYou.Remove(army)) return;
                if (army.generalStatus == UnitSystem.Army.GeneralStatus.Dying || army.generalStatus == UnitSystem.Army.GeneralStatus.FallingDeath) return;
                if (army.stolenResourceAmount <= 0) return;
                Announce(Sinking ? Loc.F("The viking ship sank with the {0} gold the thieves stole.", army.stolenResourceAmount)
                    : Loc.F("The thieves got away with {0} gold.", army.stolenResourceAmount), null, Severity.Warning);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Theft announcement failed: " + e.Message);
            }
        }

        private static void Announce(string text, GridPos? where, Severity severity)
        {
            GameEvents.Log.Add(text, severity, Player.inst != null ? Player.inst.CurrYear : 0, where);
            if (!Plugin.CfgAnnounceLog.Value) return;
            A.Cue(severity == Severity.Info ? Cue.Notify : Cue.Alert);
            A.SayQueued(text); // where it happened: in the notification history (L), which jumps there
        }
    }

    [HarmonyPatch(typeof(RaiderSystem), nameof(RaiderSystem.OnPerformedTheft))]
    internal static class Patch_ThiefStole
    {
        private static void Postfix(IMoveableUnit unit) => ThiefWatch.Stolen(unit as UnitSystem.Army);
    }

    [HarmonyPatch(typeof(UnitSystem), "SetStatus", new[] { typeof(UnitSystem.Army), typeof(UnitSystem.Army.GeneralStatus) })]
    internal static class Patch_ThiefFell
    {
        private static void Prefix(UnitSystem.Army army, UnitSystem.Army.GeneralStatus newStatus) => ThiefWatch.Died(army, newStatus);
    }

    [HarmonyPatch(typeof(UnitSystem), nameof(UnitSystem.ReleaseArmy))]
    internal static class Patch_ThiefGone
    {
        private static void Prefix(UnitSystem.Army army) => ThiefWatch.Gone(army);
    }

    [HarmonyPatch(typeof(TroopTransportShip), nameof(TroopTransportShip.DestroyUnit))]
    internal static class Patch_TransportGone
    {
        private static void Prefix(TroopTransportShip __instance) => ThiefWatch.Sinking = __instance.state == ShipBase.State.Sinking;
        private static void Finalizer() => ThiefWatch.Sinking = false;
    }
}
