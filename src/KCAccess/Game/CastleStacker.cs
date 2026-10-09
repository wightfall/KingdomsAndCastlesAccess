using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// Builds castle blocks several levels high with one Enter (or one Shift Enter line). The game stacks a block on
    /// top of the blocks already on a tile, one level per placement; this repeats the player's click or drag at the
    /// same tiles once the game holds the block again ("place another"), until the chosen number of levels is built
    /// or a round builds nothing. The game's own placement code does every round, so costs and limits are its own.
    /// </summary>
    internal sealed class CastleStacker
    {
        private enum Phase { Idle, WaitAccept, WaitRestage, Pressing, Releasing }

        internal readonly CastleStack Setting = new CastleStack();

        private Phase phase;
        private GridPos start;
        private GridPos? end;
        private GridPos? lineStart;
        private string name;
        private int built;
        private int pieces;
        private int nextFrame;
        private float deadline;

        /// <summary>Where the game's pointer must point during a repeated round, instead of the cursor.</summary>
        internal Vector3? PointerOverride { get; private set; }

        internal bool Active => phase != Phase.Idle;

        internal static bool IsCastleBlock(Building b) => b != null && b.categoryHash == World.castleblockHash;

        /// <summary>Castle blocks already on the tile (the level a new block goes on is this plus one).</summary>
        internal static int BlocksOn(Cell c)
        {
            if (c == null) return 0;
            int n = 0;
            foreach (var b in c.OccupyingStructure)
                if (IsCastleBlock(b)) n++;
            return n;
        }

        private static Building Held()
        {
            var ui = GameUI.inst;
            if (ui == null || ui.CurrPlacementMode == null || !ui.CurrPlacementMode.IsPlacing()) return null;
            return ui.CurrPlacementMode.GetHoverBuilding();
        }

        /// <summary>Shift Enter started a drag line at <paramref name="at"/>.</summary>
        internal void LineStarted(GridPos at) => lineStart = at;

        /// <summary>
        /// The player clicked (single tile) or released a line. With more than one level per placement and a castle
        /// block held, the rounds after the game's first placement are repeated here.
        /// </summary>
        internal void Placing(GridPos at, bool line)
        {
            Stop();
            var held = Held();
            if (Setting.Levels <= 1 || !IsCastleBlock(held)) return;
            if (line && !lineStart.HasValue) return;
            start = line ? lineStart.Value : at;
            end = line ? at : (GridPos?)null;
            name = held.FriendlyName;
            built = 0;
            pieces = 0;
            phase = Phase.WaitAccept;
            deadline = Time.unscaledTime + 2f;
        }

        /// <summary>From the AcceptPlacement hook. Returns true when the stacker speaks about this placement itself.</summary>
        internal bool OnAccepted(bool success, string placedName, int placedPieces, string skipped)
        {
            if (phase != Phase.WaitAccept || placedName != name) return false;
            if (success && placedPieces > 0)
            {
                built++;
                pieces += placedPieces;
                if (built >= Setting.Levels)
                {
                    Finish(null);
                    return true;
                }
                A.Cue(Cue.Placed);
                phase = Phase.WaitRestage;
                nextFrame = Time.frameCount + 2;
                deadline = Time.unscaledTime + 2f;
                return true;
            }
            if (built == 0)
            {
                // The first placement failed: the usual "Not built" explains it.
                Stop();
                return false;
            }
            Finish(MapController.Inst.InvalidReason() ?? skipped);
            return true;
        }

        /// <summary>Runs every map frame before the pointer is aimed.</summary>
        internal void Tick()
        {
            if (phase == Phase.Idle) return;
            if (KInput.Down(KeyCode.Escape))
            {
                // Escape goes on to the game / map code, which ends placing.
                if (built > 0) Finish(Loc.T("stopped with Escape"));
                else Stop();
                return;
            }
            var vp = VirtualPointer.Inst;
            if (vp == null)
            {
                Stop();
                return;
            }
            switch (phase)
            {
                case Phase.WaitAccept:
                    if (Time.unscaledTime > deadline)
                    {
                        if (built > 0) Finish(Loc.T("the game did not build the next level"));
                        else Stop();
                    }
                    break;
                case Phase.WaitRestage:
                    var held = Held();
                    if (Time.frameCount >= nextFrame && held != null && held.FriendlyName == name)
                    {
                        PointerOverride = Center(start);
                        if (end.HasValue)
                        {
                            vp.Press();
                            phase = Phase.Pressing;
                            nextFrame = Time.frameCount + 3;
                        }
                        else
                        {
                            vp.Click();
                            phase = Phase.WaitAccept;
                            deadline = Time.unscaledTime + 2f;
                        }
                    }
                    else if (Time.unscaledTime > deadline)
                    {
                        Finish(Loc.T("the game did not pick the block up again, press Enter to build the next level yourself"));
                    }
                    break;
                case Phase.Pressing:
                    if (Time.frameCount >= nextFrame)
                    {
                        PointerOverride = Center(end.Value);
                        phase = Phase.Releasing;
                        nextFrame = Time.frameCount + 3;
                    }
                    break;
                case Phase.Releasing:
                    if (Time.frameCount >= nextFrame)
                    {
                        vp.Release();
                        phase = Phase.WaitAccept;
                        deadline = Time.unscaledTime + 2f;
                    }
                    break;
            }
        }

        private void Finish(string stopReason)
        {
            string text = CastleStack.Summary(name, built, Setting.Levels, pieces, stopReason);
            Stop();
            bool again = GameUI.inst != null && GameUI.inst.CanPlaceAgain();
            A.Cue(Cue.Placed);
            A.Say(text + "." + (again ? " " + Loc.T("Move to place another, Escape to stop.") : string.Empty));
        }

        /// <summary>Enter or Shift Enter while levels are still being built: stop after the levels already built.</summary>
        internal void Interrupt()
        {
            if (built > 0) Finish(Loc.T("stopped"));
            else Stop();
        }

        /// <summary>Ends a stacking run without a word (reset, new placement, first round failed).</summary>
        internal void Stop()
        {
            if (phase == Phase.Pressing || phase == Phase.Releasing) VirtualPointer.Inst?.Release();
            phase = Phase.Idle;
            PointerOverride = null;
        }

        /// <summary>Forget a half-made line (placement ended or was reset).</summary>
        internal void ClearLine() => lineStart = null;

        private static Vector3 Center(GridPos p)
        {
            var c = World.inst.GetCellData(p.X, p.Z);
            return c != null ? c.Center : new Vector3(p.X + 0.5f, 0f, p.Z + 0.5f);
        }
    }
}
