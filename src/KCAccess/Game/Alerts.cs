using System.Collections.Generic;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// The game's bouncing exclamation marks (InWorldExclamation) are click-only prompts: advisor news over the keep,
    /// a foreign envoy waiting to speak, a stopped transport cart, a ship needing orders, the witch hut.
    /// New ones are announced with their direction, listed in the scanner's Alerts category, and the Alert key
    /// (Ctrl+E by default) responds to the nearest one exactly like clicking it.
    /// </summary>
    internal static class Alerts
    {
        private static readonly HashSet<InWorldExclamation> known = new HashSet<InWorldExclamation>();
        private static float nextCheck;

        internal static List<InWorldExclamation> Active()
        {
            var l = new List<InWorldExclamation>();
            foreach (var e in InWorldExclamation.exclamations)
                if (e != null && e.gameObject.activeInHierarchy && e.onClick != null) l.Add(e);
            return l;
        }

        internal static string Describe(InWorldExclamation e)
        {
            if (e == null) return "alert";
            if (e.GetComponentInParent<Keep>() != null) return "your advisors have news at the keep";
            var envoy = e.GetComponentInParent<Envoy>();
            if (envoy != null) return "an envoy from " + EnvoyWatch.KingdomOf(envoy) + " waits to speak with you";
            if (e.GetComponentInParent<TransportCart>() != null) return "a transport cart has stopped and needs orders";
            if (e.GetComponentInParent<WitchHut>() != null) return "the witch hut";
            if (e.GetComponentInParent<ShipBase>() != null) return "a ship needs attention";
            return "something needs your attention";
        }

        internal static GridPos PosOf(InWorldExclamation e)
        {
            var p = e.transform.position;
            return new GridPos(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
        }

        /// <summary>Called every frame in play: announce exclamation marks that just appeared.</summary>
        internal static void Tick()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;
            var active = Active();
            known.RemoveWhere(e => e == null || !active.Contains(e));
            foreach (var e in active)
            {
                if (!known.Add(e)) continue;
                A.Cue(Cue.Notify);
                A.SayQueued(TextUtil.Capitalize(Describe(e)) + ", " + Directions.Relative(MapController.Inst.CursorPos, PosOf(e)) + KeyHelp.Resolve(". {Alert} responds."));
            }
        }

        /// <summary>The Alert key: click the exclamation mark nearest the cursor.</summary>
        internal static void RespondNearest()
        {
            var active = Active();
            if (active.Count == 0)
            {
                A.Cue(Cue.Error);
                A.Say("No alerts right now", force: true);
                return;
            }
            var origin = MapController.Inst.CursorPos;
            InWorldExclamation best = null;
            double bestD = double.MaxValue;
            foreach (var e in active)
            {
                double d = Directions.Euclid(origin, PosOf(e));
                if (d < bestD) { bestD = d; best = e; }
            }
            A.Cue(Cue.Activate);
            A.Say("Responding: " + Describe(best), force: true);
            best.Click();
        }
    }
}
