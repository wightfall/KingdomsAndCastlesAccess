using System.Collections.Generic;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// A troop ship shows who is on board only as pictures, and an exploration ship only greys out its Settle button:
    /// say when units board or leave one of your troop ships and when an exploration ship comes in or out of reach of
    /// unclaimed land (SeedShip.CanSettle).
    /// </summary>
    internal static class ShipWatch
    {
        private static readonly Dictionary<TroopTransportShip, List<IMoveableUnit>> aboard = new Dictionary<TroopTransportShip, List<IMoveableUnit>>();
        private static readonly Dictionary<SeedShip, bool> canSettle = new Dictionary<SeedShip, bool>();
        private static float nextCheck;

        internal static void Reset()
        {
            aboard.Clear();
            canSettle.Clear();
        }

        internal static void Tick()
        {
            if (Time.unscaledTime < nextCheck || ShipSystem.inst == null || ShipSystem.inst.ships == null || GameUI.inst == null) return;
            nextCheck = Time.unscaledTime + 0.5f;
            var ships = ShipSystem.inst.ships;
            var troops = new HashSet<TroopTransportShip>();
            var seeds = new HashSet<SeedShip>();
            for (int i = 0; i < ships.Count; i++)
            {
                var s = ships.data[i];
                if (s == null || s.teamID != 0) continue;
                if (s is TroopTransportShip t)
                {
                    troops.Add(t);
                    var now = new List<IMoveableUnit>();
                    for (int j = 0; j < t.CarryCount(); j++) if (Alive(t.GetCarried(j))) now.Add(t.GetCarried(j));
                    if (aboard.TryGetValue(t, out var before))
                    {
                        string ship = CellInfo.WithoutSelected(CellInfo.ShipName(t));
                        var boarded = now.FindAll(u => !before.Contains(u));
                        var left = before.FindAll(u => !now.Contains(u) && Alive(u));
                        if (boarded.Count > 0)
                            A.SayQueued(TextUtil.Sentences(Loc.F("{0} boarded {1}", TextUtil.Capitalize(Names(boarded)), ship) + ", " + Loc.F("{0} of {1} places taken", now.Count, t.maxUnitsCarryable),
                                GameUI.inst.IsSelected(t) ? Loc.T("The ship is selected.") : null));
                        if (left.Count > 0)
                            A.SayQueued(TextUtil.Sentences(Loc.F("{0} unloaded {1}", TextUtil.Capitalize(ship), Names(left)),
                                left.Exists(u => u is ISelectable sel && GameUI.inst.IsSelected(sel)) ? Loc.T("They are selected.") : null));
                    }
                    aboard[t] = now;
                }
                else if (s is SeedShip seed)
                {
                    seeds.Add(seed);
                    if (canSettle.TryGetValue(seed, out bool was) && was != seed.CanSettle)
                    {
                        A.Cue(Cue.Notify);
                        A.SayQueued(seed.CanSettle
                            ? Loc.T("Your exploration ship is in reach of unclaimed land: Settle in its panel places an outpost.")
                            : Loc.T("Your exploration ship is out of reach of unclaimed land."));
                    }
                    canSettle[seed] = seed.CanSettle;
                }
            }
            // Forget ships that sank, were disbanded or settled.
            foreach (var gone in new List<TroopTransportShip>(aboard.Keys)) if (!troops.Contains(gone)) aboard.Remove(gone);
            foreach (var gone in new List<SeedShip>(canSettle.Keys)) if (!seeds.Contains(gone)) canSettle.Remove(gone);
        }

        private static bool Alive(IMoveableUnit u) => u != null && !u.Equals(null);

        private static string Names(List<IMoveableUnit> units)
        {
            var names = new List<string>();
            foreach (var u in units) names.Add(CellInfo.WithoutSelected(CellInfo.UnitName(u)));
            return string.Join(", ", names.ToArray());
        }
    }
}
