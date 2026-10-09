using System;
using System.Collections.Generic;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// Navigation to a chosen target (from the scanner, notifications or bookmarks):
    /// an audio beacon that tells direction and distance, auto-walk along a route villagers could walk,
    /// straight-line walking, and a direct jump.
    /// </summary>
    internal sealed class Navigator
    {
        /// <summary>Footpath cost at or above which a tile counts as not walkable (buildings, walls, rock, deep water).</summary>
        private const int BlockedCost = 250;

        /// <summary>Seconds per step: slow, normal, fast (mod settings).</summary>
        private static float StepSeconds => Plugin.CfgWalkSpeed.Value <= 1 ? 0.3f : (Plugin.CfgWalkSpeed.Value >= 3 ? 0.08f : 0.16f);

        internal GridPos? Target { get; private set; }
        internal string TargetLabel { get; private set; }
        internal bool BeaconOn { get; private set; }

        private float nextPing;
        private bool arrivedAnnounced;

        private List<GridPos> walk;
        private int walkIndex;
        private float nextStep;
        private string lastSignature;
        private bool walkStraight;

        internal bool Walking => walk != null;

        internal void SetTarget(GridPos pos, string label)
        {
            Target = pos;
            TargetLabel = label;
            arrivedAnnounced = false;
            nextPing = 0f;
        }

        internal void ClearTarget()
        {
            Target = null;
            TargetLabel = null;
            BeaconOn = false; // a beacon left on from the previous kingdom would start pinging at the next target
            StopWalk(announce: false);
        }

        internal string Describe(GridPos from)
        {
            if (!Target.HasValue) return KCAccess.Core.KeyHelp.Resolve(Loc.T("No target. Choose one with {PrevCategory}, {NextCategory} and {PrevItem} or {NextItem}."));
            return TargetLabel + ", " + Directions.Relative(from, Target.Value);
        }

        internal void ToggleBeacon(GridPos from)
        {
            if (!Target.HasValue)
            {
                A.Cue(Cue.Error);
                A.Say(KCAccess.Core.KeyHelp.Resolve(Loc.T("No target for the beacon. Choose one with {PrevCategory}, {NextCategory} and {PrevItem} or {NextItem}.")));
                return;
            }
            BeaconOn = !BeaconOn;
            arrivedAnnounced = false;
            nextPing = 0f;
            A.Say(BeaconOn ? Loc.F("Beacon on. {0}. Higher pitch means north, lower means south, left and right ear give west and east, faster pings mean closer.", Describe(from)) : Loc.T("Beacon off"), force: true);
        }

        /// <summary>Called every frame on the map.</summary>
        internal void Tick(MapController map)
        {
            if (walk != null) TickWalk(map);
            if (!BeaconOn || !Target.HasValue) return;
            var from = map.CursorPos;
            var s = BeaconMath.Compute(Target.Value.X - from.X, Target.Value.Z - from.Z);
            if (s.Arrived)
            {
                if (!arrivedAnnounced)
                {
                    arrivedAnnounced = true;
                    A.Cue(Cue.Placed);
                }
                return;
            }
            arrivedAnnounced = false;
            if (Time.unscaledTime < nextPing) return;
            nextPing = Time.unscaledTime + s.Interval;
            AudioCues.PlayPanned(Cue.Beacon, s.Pan, s.Pitch);
        }

        // ------------------------------------------------------------------ walking

        /// <summary>Walks the cursor to the target along a walkable route (or straight when <paramref name="straight"/>).</summary>
        internal void StartWalk(MapController map, bool straight)
        {
            if (!Target.HasValue)
            {
                A.Cue(Cue.Error);
                A.Say(KCAccess.Core.KeyHelp.Resolve(Loc.T("No target. Choose one with {PrevCategory}, {NextCategory} and {PrevItem} or {NextItem}.")));
                return;
            }
            var from = map.CursorPos;
            var to = Target.Value;
            if (from == to)
            {
                A.Say(Loc.F("Already at {0}", TargetLabel));
                return;
            }
            List<GridPos> route;
            if (straight)
            {
                route = PathSteps.Expand(from, new[] { to });
            }
            else
            {
                route = FindRoute(from, to);
                if (route == null)
                {
                    A.Cue(Cue.Error);
                    var a = World.inst.GetCellData(from.X, from.Z);
                    var b = World.inst.GetCellData(to.X, to.Z);
                    string why;
                    if (a != null && a.Type == ResourceType.Water) why = Loc.T("The cursor is on water; move onto land first.");
                    else if (a != null && b != null && a.landMassIdx >= 0 && b.landMassIdx >= 0 && a.landMassIdx != b.landMassIdx) why = Loc.T("It is on another island, across water.");
                    else why = Loc.T("It may be across water or behind walls.");
                    A.Say(Loc.F("No walkable route to {0}.", TargetLabel) + " " + why + " " + KCAccess.Core.KeyHelp.Resolve(Loc.T("{WalkStraight} walks in a straight line, {JumpTarget} jumps there.")));
                    return;
                }
            }
            if (route.Count == 0)
            {
                A.Say(Loc.F("Already next to {0}", TargetLabel));
                return;
            }
            walk = route;
            walkIndex = 0;
            walkStraight = straight;
            nextStep = Time.unscaledTime + 0.1f;
            lastSignature = CellInfo.Signature(World.inst.GetCellData(from.X, from.Z));
            A.Cue(Cue.Open);
            A.Say((straight ? Loc.F("Walking straight to {0}", TargetLabel) : Loc.F("Walking to {0}", TargetLabel)) + ", " + Loc.P(route.Count, "{0} step", "{0} steps") + ". " + Loc.T("Any arrow key or Escape stops."));
        }

        internal void StopWalk(bool announce)
        {
            if (walk == null) return;
            walk = null;
            if (announce)
            {
                A.Cue(Cue.Close);
                A.Say(Loc.T("Walk stopped"));
            }
        }

        private void TickWalk(MapController map)
        {
            if (Time.unscaledTime < nextStep) return;
            nextStep = Time.unscaledTime + StepSeconds;
            var next = walk[walkIndex];
            map.StepTo(next);
            A.Cue(Cue.Step);
            walkIndex++;
            var cell = World.inst.GetCellData(next.X, next.Z);
            string sig = CellInfo.Signature(cell);
            bool last = walkIndex >= walk.Count;
            if (last)
            {
                walk = null;
                A.Cue(Cue.Placed);
                bool exact = Target.HasValue && next == Target.Value;
                A.Say((exact ? Loc.F("Arrived at {0}", TargetLabel) : Loc.F("Arrived next to {0}", TargetLabel)) + ". " + CellInfo.Brief(cell, false), force: true);
                return;
            }
            // Speak only when the terrain changes, plus a progress note every 10 steps.
            if (sig != lastSignature)
            {
                lastSignature = sig;
                A.Say(CellInfo.Brief(cell, false));
            }
            else if (walkIndex % 10 == 0)
            {
                A.SayQueued(Loc.P(walk.Count - walkIndex, "{0} step to go", "{0} steps to go"));
            }
        }

        /// <summary>
        /// Walkable for the route: during play the game's own footpath cost (buildings, walls, rock and deep water
        /// are far too expensive for villagers); before the game starts the costs are not set up yet, so terrain decides.
        /// </summary>
        private static bool Walkable(Cell c)
        {
            if (c == null) return false;
            if (GameState.inst != null && GameState.inst.IsPlayMode())
            {
                var pc = World.inst.GetPathCell(c);
                return pc != null && PathCell.GetFootPathCost(pc, 0) < BlockedCost;
            }
            if (c.Type == ResourceType.Water && c.deepWater) return false;
            if (c.Type == ResourceType.Stone || c.Type == ResourceType.UnusableStone || c.Type == ResourceType.IronDeposit) return false;
            return c.OccupyingStructure.Count == 0;
        }

        private static int Cost(Cell c)
        {
            if (c == null) return 1000;
            if (GameState.inst != null && GameState.inst.IsPlayMode())
            {
                var pc = World.inst.GetPathCell(c);
                return pc != null ? System.Math.Max(1, PathCell.GetFootPathCost(pc, 0)) : 1;
            }
            return c.TreeAmount > 0 ? 2 : 1;
        }

        /// <summary>
        /// Route over walkable tiles. If the target itself is not walkable (inside a building, on rock),
        /// the route ends on the closest walkable tile next to it.
        /// </summary>
        private static List<GridPos> FindRoute(GridPos from, GridPos to)
        {
            var w = World.inst;
            Func<int, int, bool> passable = (x, z) => (x == from.X && z == from.Z) || Walkable(w.GetCellData(x, z));
            Func<int, int, int> cost = (x, z) => Cost(w.GetCellData(x, z));
            var goals = new List<GridPos>();
            if (Walkable(w.GetCellData(to.X, to.Z))) goals.Add(to);
            else
            {
                for (int r = 1; r <= 3 && goals.Count == 0; r++)
                {
                    for (int dz = -r; dz <= r; dz++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz)) != r) continue;
                            if (Walkable(w.GetCellData(to.X + dx, to.Z + dz))) goals.Add(new GridPos(to.X + dx, to.Z + dz));
                        }
                }
                goals.Sort((a, b) => Directions.Euclid(from, a).CompareTo(Directions.Euclid(from, b)));
            }
            int tries = 0;
            foreach (var g in goals)
            {
                if (g == from) return new List<GridPos>();
                var route = GridPathfinder.Find(w.GridWidth, w.GridHeight, from, g, passable, cost);
                if (route != null) return route;
                if (++tries >= 4) break; // the neighbours share the same reachability; do not search the whole map again and again
            }
            return null;
        }
    }
}
