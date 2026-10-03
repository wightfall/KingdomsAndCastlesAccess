using Assets.Code;
using Assets;
using System.Collections.Generic;
using KCAccess.Core;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// Keyboard play on the map: a tile cursor that drives the game's pointer, selection, building
    /// placement with validity feedback, cursor modes, status readouts and jumping around the map.
    /// </summary>
    internal sealed class MapController
    {
        internal static readonly MapController Inst = new MapController();

        private readonly GridCursor cursor = new GridCursor(1, 1);
        private readonly Scanner scanner = new Scanner();
        internal readonly Navigator Nav = new Navigator();
        private bool initialized;

        /// <summary>Exploring the generated map from the map setup screen (Ctrl+M).</summary>
        internal bool MenuMapMode;

        // State tracking for automatic announcements.
        private bool wasPlacing;
        private string lastHeldName;
        private CursorMode lastCursorMode;
        private Building lastSelected;
        private int validityCheckFrame = -1;
        private PlacementValidationResult lastValidity = (PlacementValidationResult)(-1);
        private GridPos? areaStart;
        private float placedSayTime;
        private bool hadSelection;
        private GameUI.CursorBrushes lastBrush;

        internal GridPos CursorPos => cursor.Pos;

        internal string HelpId => IsPlacing ? "Placement" : "Map";

        private static bool IsPlacing => GameUI.inst != null && GameUI.inst.CurrPlacementMode != null && GameUI.inst.CurrPlacementMode.IsPlacing();

        internal Cell CurrentCell => World.inst.GetCellData(cursor.Pos.X, cursor.Pos.Z);

        private void EnsureInit()
        {
            var w = World.inst;
            if (cursor.Width != w.GridWidth || cursor.Height != w.GridHeight) cursor.Resize(w.GridWidth, w.GridHeight);
            VirtualPointer.Install();
            if (initialized) return;
            initialized = true;
            CenterOnStart();
        }

        /// <summary>Put the cursor on the keep, or at the camera focus when there is no keep yet.</summary>
        internal void CenterOnStart()
        {
            var w = World.inst;
            cursor.Resize(w.GridWidth, w.GridHeight);
            if (Player.inst != null && Player.inst.keep != null)
            {
                var c = Player.inst.keep.GetComponent<Building>().GetCell();
                if (c != null)
                {
                    cursor.Set(c.x, c.z);
                    return;
                }
            }
            Vector3 focus = Cam.inst != null ? Cam.inst.DesiredTrackingPos : new Vector3(w.GridWidth / 2f, 0, w.GridHeight / 2f);
            cursor.Set((int)focus.x, (int)focus.z);
            // No keep yet: start on land where a keep may be built (the game only offers the keep while the camera is over land).
            if (Player.inst != null && Player.inst.keep == null && !MenuMapMode)
            {
                var land = NearestStartLand(cursor.Pos);
                if (land.HasValue) cursor.Set(land.Value.X, land.Value.Z);
            }
        }

        /// <summary>Nearest tile on a landmass that is valid for the keep (searching outward in rings).</summary>
        internal GridPos? NearestStartLand(GridPos from)
        {
            var w = World.inst;
            bool Good(int x, int z)
            {
                var c = w.GetCellData(x, z);
                if (c == null || c.landMassIdx < 0 || c.Type == ResourceType.Water) return false;
                try
                {
                    return w.IsValidStartLandmass(c.landMassIdx);
                }
                catch
                {
                    return true;
                }
            }
            if (Good(from.X, from.Z)) return from;
            int max = Mathf.Max(w.GridWidth, w.GridHeight);
            for (int r = 1; r < max; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Good(from.X + dx, from.Z + r)) return new GridPos(from.X + dx, from.Z + r);
                    if (Good(from.X + dx, from.Z - r)) return new GridPos(from.X + dx, from.Z - r);
                }
                for (int dz = -r + 1; dz < r; dz++)
                {
                    if (Good(from.X + r, from.Z + dz)) return new GridPos(from.X + r, from.Z + dz);
                    if (Good(from.X - r, from.Z + dz)) return new GridPos(from.X - r, from.Z + dz);
                }
            }
            return null;
        }

        /// <summary>Before the keep exists the game needs the camera over land; jump there if it is not. Returns true when moved.</summary>
        internal bool EnsureCameraOnStartLand()
        {
            if (Player.inst == null || Player.inst.keep != null || Player.inst.FocusedLandMass != -1) return false;
            var land = NearestStartLand(cursor.Pos);
            if (!land.HasValue) return false;
            cursor.Set(land.Value.X, land.Value.Z);
            UpdatePointer(follow: true);
            if (Cam.inst != null) Cam.inst.SetTrackingPos(CurrentCell.Center);
            A.Say("Moved the cursor to the nearest land where you can build your keep: " + CellInfo.Brief(CurrentCell, false));
            return true;
        }

        internal void OnEnterPlayMode()
        {
            initialized = false;
            MenuMapMode = false;
            enteredPlayAt = Time.unscaledTime;
            EnsureInit();
            scanner.Invalidate();
            Nav.ClearTarget();
            UpdatePointer(follow: Player.inst.keep == null);
            string intro = Player.inst.keep == null
                ? "Your kingdom begins. Build your keep first: press B, choose the keep in the Castle category, then move to a good spot near fertile land, trees and stone (O surveys the area around the cursor), and press Enter. Press F1 for help."
                : "Kingdom loaded. Press F1 for help, K for status.";
            A.Say(intro + " Cursor at " + CellInfo.Brief(CurrentCell, false));
        }

        internal void OnResume()
        {
            EnsureInit();
            enteredPlayAt = Time.unscaledTime;
            UpdatePointer(follow: false);
            A.Say("Resumed. " + CellInfo.Brief(CurrentCell, false));
        }

        private float enteredPlayAt;

        internal void OnReturnToMap()
        {
            if (Time.unscaledTime - enteredPlayAt < 1.5f) return;
            A.Say("Map, " + CellInfo.Brief(CurrentCell, false));
        }

        /// <summary>Called every frame while the map has the keyboard.</summary>
        internal void Tick()
        {
            EnsureInit();
            var vp = VirtualPointer.Inst;
            if (vp != null)
            {
                vp.Tick();
                if (vp.KeyboardActive) vp.Target = CurrentCell != null ? CurrentCell.Center : Vector3.zero;
            }
            if (!MenuMapMode) TrackGameState();

            if (MenuMapMode)
            {
                if (KInput.Plain(KeyCode.Escape) || (KInput.Down(KeyCode.M) && KInput.Ctrl))
                {
                    KInput.Consume(KeyCode.Escape);
                    MenuMapMode = false;
                    A.Cue(Cue.Close);
                    A.Say("Back to map setup menu");
                    return;
                }
            }

            if (Nav.Walking && (KInput.Down(KeyCode.UpArrow) || KInput.Down(KeyCode.DownArrow) || KInput.Down(KeyCode.LeftArrow) || KInput.Down(KeyCode.RightArrow) || KInput.Down(KeyCode.Escape)))
            {
                KInput.Consume(KeyCode.Escape);
                Nav.StopWalk(announce: true);
                return;
            }
            Nav.Tick(this);
            if (HandleMovement()) return;
            if (MenuMapMode)
            {
                if ((KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.KeypadEnter)) && MapEditBrushActive())
                {
                    VirtualPointer.Inst?.Click();
                    A.Cue(Cue.Activate);
                    A.Say("Painted at " + cursor.Pos);
                    return;
                }
                HandleInfoKeys();
                return;
            }
            if (HandleInfoKeys()) return;
            HandleActionKeys();
        }

        // ------------------------------------------------------------------ movement

        private bool HandleMovement()
        {
            int dx = 0, dz = 0;
            if (KInput.Down(KeyCode.UpArrow)) dz = 1;
            else if (KInput.Down(KeyCode.DownArrow)) dz = -1;
            else if (KInput.Down(KeyCode.RightArrow)) dx = 1;
            else if (KInput.Down(KeyCode.LeftArrow)) dx = -1;
            else return false;
            if (KInput.Alt) return false;

            bool moved;
            if (KInput.Ctrl)
            {
                int n = cursor.MoveUntilChange(dx, dz, (x, z) => CellInfo.Signature(World.inst.GetCellData(x, z)));
                moved = n > 0;
                if (moved) A.Say(TextUtil.Plural(n, "tile") + ", " + CellInfo.Brief(CurrentCell, Plugin.CfgVerboseCells.Value), force: true);
            }
            else
            {
                moved = cursor.Move(dx, dz, KInput.Shift ? 5 : 1) == MoveResult.Moved;
                if (moved) A.Say(CellInfo.Brief(CurrentCell, Plugin.CfgVerboseCells.Value), force: true);
            }
            if (!moved)
            {
                A.Cue(Cue.Edge);
                A.Say("edge of the map");
                return true;
            }
            StepCue(CurrentCell);
            UpdatePointer(follow: true);
            if (IsPlacing) validityCheckFrame = Time.frameCount + 2;
            return true;
        }

        private static void StepCue(Cell c)
        {
            if (c == null) return;
            if (!CellInfo.Explored(c)) A.Cue(Cue.StepFog);
            else if (CellInfo.IsWater(c)) A.Cue(Cue.StepWater);
            else if (c.OccupyingStructure.Count > 0 || c.Type == ResourceType.Stone || c.Type == ResourceType.IronDeposit || c.Type == ResourceType.UnusableStone) A.Cue(Cue.StepOccupied);
            else A.Cue(Cue.StepLand);
        }

        /// <summary>Point the game's pointer at the cursor and (optionally) move the camera there.</summary>
        private void UpdatePointer(bool follow)
        {
            var c = CurrentCell;
            if (c == null) return;
            var vp = VirtualPointer.Inst;
            if (vp != null)
            {
                vp.KeyboardActive = true;
                vp.Target = c.Center;
            }
            if (follow && Plugin.CfgCameraFollow.Value && Cam.inst != null) Cam.inst.SetDesiredTrackingPos(c.Center);
        }

        private static string BrushName(GameUI.CursorBrushes b)
        {
            switch (b)
            {
                case GameUI.CursorBrushes.General: return "Spawn knights";
                case GameUI.CursorBrushes.ArcherGeneral: return "Spawn archers";
                case GameUI.CursorBrushes.Settlers: return "Spawn settlers";
                case GameUI.CursorBrushes.Envoy: return "Spawn envoy";
                case GameUI.CursorBrushes.Catapult: return "Spawn catapult";
                case GameUI.CursorBrushes.Viking: return "Spawn vikings";
                case GameUI.CursorBrushes.EliteViking: return "Spawn stronger vikings";
                case GameUI.CursorBrushes.Ogre: return "Spawn ogre";
                case GameUI.CursorBrushes.Villager: return "Spawn peasants";
                case GameUI.CursorBrushes.SmallDragon: return "Spawn dragon";
                case GameUI.CursorBrushes.SiegeDragon: return "Spawn siege dragon";
                case GameUI.CursorBrushes.LargeDragon: return "Spawn momma dragon";
                case GameUI.CursorBrushes.Delete: return "Removal tool";
                case GameUI.CursorBrushes.Fire: return "Create fire";
                case GameUI.CursorBrushes.Trees: return "Create trees";
                default: return TextUtil.Humanize(b.ToString()) + " stack";
            }
        }

        /// <summary>Creative mode map editor (map setup screen) has a brush selected.</summary>
        private static bool MapEditBrushActive()
        {
            var mm = GameState.inst.mainMenuMode;
            if (mm == null || mm.mapEditUI == null || !mm.mapEditUI.activeInHierarchy) return false;
            var edit = mm.mapEditUI.GetComponent<MapEdit>();
            return edit != null && edit.brushMode != MapEdit.BrushMode.None;
        }

        /// <summary>Moves the cursor one step during auto-walk (camera follows, placement re-checked).</summary>
        internal void StepTo(GridPos p)
        {
            cursor.Set(p.X, p.Z);
            UpdatePointer(follow: true);
            if (IsPlacing) validityCheckFrame = Time.frameCount + 2;
        }

        internal void PointAtCursor()
        {
            EnsureInit();
            UpdatePointer(follow: true);
        }

        internal void JumpTo(GridPos p, bool announce = true)
        {
            cursor.Set(p.X, p.Z);
            UpdatePointer(follow: true);
            if (IsPlacing) validityCheckFrame = Time.frameCount + 2;
            if (announce)
            {
                StepCue(CurrentCell);
                A.SayQueued(CellInfo.Brief(CurrentCell, false));
            }
        }

        // ------------------------------------------------------------------ information keys

        private bool HandleInfoKeys()
        {
            if (KInput.Plain(KeyCode.I))
            {
                A.Say(CellInfo.Full(CurrentCell), force: true);
                return true;
            }
            if (KInput.Plain(KeyCode.O))
            {
                A.Say(CellInfo.Survey(cursor.Pos, 6).Format(), force: true);
                return true;
            }
            if (KInput.Plain(KeyCode.G))
            {
                A.Say("Cursor at " + cursor.Pos.X + ", " + cursor.Pos.Z + ". Map is " + cursor.Width + " by " + cursor.Height + ".", force: true);
                return true;
            }
            if (KInput.Plain(KeyCode.PageUp) || KInput.Plain(KeyCode.PageDown))
            {
                scanner.ChangeCategory(KInput.Down(KeyCode.PageDown) ? 1 : -1, cursor.Pos);
                return true;
            }
            if (KInput.Plain(KeyCode.RightBracket) || KInput.Plain(KeyCode.LeftBracket))
            {
                var item = scanner.Step(KInput.Down(KeyCode.RightBracket) ? 1 : -1, cursor.Pos);
                if (item != null) Nav.SetTarget(item.Pos, item.Label);
                return true;
            }
            if (KInput.Plain(KeyCode.Backslash))
            {
                if (!Nav.Target.HasValue)
                {
                    A.Cue(Cue.Error);
                    A.Say("No target. Choose one with Page Up, Page Down and the bracket keys.");
                }
                else
                {
                    A.Say(Nav.TargetLabel);
                    JumpTo(Nav.Target.Value);
                }
                return true;
            }
            if (KInput.WithShift(KeyCode.Backslash))
            {
                A.Say(Nav.Describe(cursor.Pos), force: true);
                return true;
            }
            if (KInput.Plain(KeyCode.N))
            {
                Nav.StartWalk(this, straight: false);
                return true;
            }
            if (KInput.WithCtrl(KeyCode.N))
            {
                Nav.StartWalk(this, straight: true);
                return true;
            }
            if (KInput.WithShift(KeyCode.N))
            {
                Nav.ToggleBeacon(cursor.Pos);
                return true;
            }
            if (MenuMapMode) return false;
            if (KInput.Plain(KeyCode.K))
            {
                AccessController.Inst.ActiveMenu = new StatusMenu();
                return true;
            }
            if (KInput.Plain(KeyCode.T))
            {
                A.Say(Status.DateLine(), force: true);
                return true;
            }
            if (KInput.Plain(KeyCode.L))
            {
                AccessController.Inst.ActiveMenu = new LogBrowser();
                return true;
            }
            if (KInput.WithShift(KeyCode.L))
            {
                var n = GameEvents.Log.Count > 0 ? GameEvents.Log.Items[0] : null;
                A.Say(n != null ? n.Describe() : "No notifications yet", force: true);
                return true;
            }
            if (KInput.Plain(KeyCode.Home))
            {
                if (Player.inst.keep != null)
                {
                    var c = Player.inst.keep.GetComponent<Building>().GetCell();
                    A.Say("Keep");
                    JumpTo(new GridPos(c.x, c.z));
                }
                else
                {
                    A.Cue(Cue.Error);
                    A.Say("You have no keep yet");
                }
                return true;
            }
            if (KInput.Plain(KeyCode.End))
            {
                var b = GameUI.inst.GetBuildingSelected();
                var cell = b != null ? b.GetCell() : GameUI.inst.GetCellSelected();
                if (cell != null)
                {
                    A.Say("Selection");
                    JumpTo(new GridPos(cell.x, cell.z));
                }
                else A.Say("Nothing selected");
                return true;
            }
            if (KInput.Plain(KeyCode.F5))
            {
                A.Say(CellInfo.Brief(CurrentCell, true), force: true);
                return true;
            }
            if (HandleBookmarks()) return true;
            return false;
        }

        // ------------------------------------------------------------------ bookmarks

        private static Bookmarks bookmarks;

        private static string BookmarkFile => System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "kcaccess_bookmarks.txt");

        private static string KingdomKey => MenuMapMode_Static ? "map setup" : (TownNameUI.inst != null && !string.IsNullOrEmpty(TownNameUI.inst.townName) ? TownNameUI.inst.townName : "kingdom");

        private static bool MenuMapMode_Static => Inst.MenuMapMode;

        private static void LoadBookmarks()
        {
            if (bookmarks != null) return;
            try
            {
                bookmarks = Bookmarks.Parse(System.IO.File.Exists(BookmarkFile) ? System.IO.File.ReadAllText(BookmarkFile) : null);
            }
            catch (System.Exception)
            {
                bookmarks = new Bookmarks();
            }
        }

        /// <summary>Ctrl+1..9 jumps to a bookmark, Ctrl+Shift+1..9 stores the cursor position there.</summary>
        private bool HandleBookmarks()
        {
            if (KInput.Alt && !KInput.Ctrl && !KInput.Shift)
            {
                for (int i = 1; i <= Bookmarks.Slots; i++)
                {
                    if (!(KInput.Down(KeyCode.Alpha0 + i) || KInput.Down(KeyCode.Keypad0 + i))) continue;
                    LoadBookmarks();
                    var bp = bookmarks.Get(KingdomKey, i);
                    if (!bp.HasValue)
                    {
                        A.Cue(Cue.Error);
                        A.Say("Bookmark " + i + " is empty", force: true);
                    }
                    else
                    {
                        Nav.SetTarget(bp.Value, "bookmark " + i);
                        A.Say("Target bookmark " + i + ", " + Directions.Relative(cursor.Pos, bp.Value), force: true);
                    }
                    return true;
                }
                return false;
            }
            if (!KInput.Ctrl || KInput.Alt) return false;
            int slot = 0;
            for (int i = 1; i <= Bookmarks.Slots; i++)
            {
                if (KInput.Down(KeyCode.Alpha0 + i) || KInput.Down(KeyCode.Keypad0 + i))
                {
                    slot = i;
                    break;
                }
            }
            if (slot == 0) return false;
            LoadBookmarks();
            if (KInput.Shift)
            {
                bookmarks.Set(KingdomKey, slot, cursor.Pos);
                try
                {
                    System.IO.File.WriteAllText(BookmarkFile, bookmarks.Serialize());
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning("Could not save bookmarks: " + e.Message);
                }
                A.Cue(Cue.Placed);
                A.Say("Bookmark " + slot + " set at " + cursor.Pos, force: true);
                return true;
            }
            var p = bookmarks.Get(KingdomKey, slot);
            if (!p.HasValue)
            {
                A.Cue(Cue.Error);
                A.Say("Bookmark " + slot + " is empty. Control Shift " + slot + " stores the cursor position.", force: true);
                return true;
            }
            A.Say("Bookmark " + slot);
            JumpTo(p.Value);
            return true;
        }

        // ------------------------------------------------------------------ actions

        private void HandleActionKeys()
        {
            var ui = GameUI.inst;
            if (ui == null) return;

            if (KInput.Plain(KeyCode.Escape) && ui.WaitingToPlaceAgain() && !IsPlacing)
            {
                // The game only ends "place another" when Escape is pressed while the building is held;
                // after a placement the building is not held yet, so end it here.
                KInput.Consume(KeyCode.Escape);
                ui.CancelWaitToPlace();
                A.Cue(Cue.Close);
                A.Say("Placement ended");
                return;
            }
            if (KInput.Plain(KeyCode.Escape) && ui.brushMode != GameUI.CursorBrushes.None)
            {
                KInput.Consume(KeyCode.Escape);
                ui.brushMode = GameUI.CursorBrushes.None;
                return;
            }
            if (KInput.WithShift(KeyCode.C) && !IsPlacing)
            {
                // Shortcut for the toolbar's chop mode: mark whole areas with Shift Enter.
                KInput.Consume(KeyCode.C);
                if (ui.currCursorMode == ui.chopCursorMode) ui.ReturnToDefaultCursorMode();
                else ui.SetCursorModeChop();
                return;
            }
            if (KInput.Plain(KeyCode.C) && !IsPlacing && (ui.currCursorMode == null || ui.currCursorMode == ui.consoleCursorMode))
            {
                // The game's chop shortcut works on the selected tile and stays silent when it cannot.
                var sel = ui.GetCellSelected();
                if (sel == null) A.Say("Select a forest tile with Enter first, then press C. For a large area use chop trees mode in the toolbar, F6.");
                else if (sel.TreeAmount == 0) A.Say("No trees on the selected tile");
            }
            if (KInput.Plain(KeyCode.B))
            {
                AccessController.Inst.ActiveMenu = new BuildMenu();
                return;
            }
            if (KInput.Down(KeyCode.F6) && !KInput.Ctrl && !KInput.Alt)
            {
                if (!AccessController.Inst.FocusPanel(KInput.Shift ? -1 : 1))
                {
                    A.Cue(Cue.Error);
                    A.Say("No panel is open");
                }
                return;
            }
            if (KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.KeypadEnter))
            {
                KInput.Consume(KeyCode.Return);
                Activate();
                return;
            }
            if (KInput.WithShift(KeyCode.Return))
            {
                ShiftActivate();
                return;
            }
            if (KInput.Plain(KeyCode.V) && IsPlacing)
            {
                SpeakValidity(always: true);
                return;
            }
            if (KInput.Plain(KeyCode.M))
            {
                MoveUnits();
                return;
            }
            if (KInput.WithShift(KeyCode.I))
            {
                SpeakSelection(detailed: true);
                return;
            }
            if (IsPlacing && KInput.Plain(KeyCode.R))
            {
                // The game rotates on R itself; announce afterwards.
                validityCheckFrame = Time.frameCount + 2;
                lastValidity = (PlacementValidationResult)(-1);
                A.Say("Rotated");
            }
        }

        /// <summary>Enter: place / act with the cursor mode / select what is on the tile.</summary>
        private void Activate()
        {
            var ui = GameUI.inst;
            var vp = VirtualPointer.Inst;
            var cell = CurrentCell;
            UpdatePointer(follow: false);
            if (IsPlacing)
            {
                if (vp.IsHeld)
                {
                    vp.Release();
                    return;
                }
                var result = CurrentValidity();
                if (result.HasValue && result.Value != PlacementValidationResult.Valid && ui.CurrPlacementMode.PlacementCount() <= 1)
                {
                    A.Cue(Cue.PlaceInvalid);
                    A.Say("Cannot build here: " + Explain(result.Value));
                    return;
                }
                vp.Click();
                return;
            }
            if (ui.brushMode != GameUI.CursorBrushes.None)
            {
                vp.Click();
                A.Cue(Cue.Activate);
                A.Say(BrushName(ui.brushMode) + " at " + cursor.Pos);
                return;
            }
            if (ui.currCursorMode != null && ui.currCursorMode != ui.consoleCursorMode)
            {
                if (areaStart.HasValue)
                {
                    FinishArea();
                    return;
                }
                bool handled = ui.currCursorMode.DoPrimaryClick(cell != null ? cell.TopMostStructure : null, cell);
                A.Cue(handled ? Cue.Activate : Cue.Error);
                A.SayQueued(CellInfo.Brief(cell, false));
                return;
            }
            SelectAt(cell);
        }

        /// <summary>Shift+Enter: start/finish a drag line (roads, walls), an area for cursor modes, or select a unit.</summary>
        private void ShiftActivate()
        {
            var ui = GameUI.inst;
            var vp = VirtualPointer.Inst;
            UpdatePointer(follow: false);
            if (IsPlacing)
            {
                if (!vp.IsHeld)
                {
                    vp.Press();
                    A.Cue(Cue.Open);
                    A.Say("Start point set at " + cursor.Pos + ". Move to the end point and press Enter to build, Escape to cancel.");
                }
                else
                {
                    vp.Release();
                }
                return;
            }
            if (ui.currCursorMode != null && ui.currCursorMode != ui.consoleCursorMode)
            {
                if (!areaStart.HasValue)
                {
                    areaStart = cursor.Pos;
                    A.Cue(Cue.Open);
                    A.Say("Area corner set. Move to the opposite corner and press Enter.");
                }
                else FinishArea();
                return;
            }
            SelectUnitAt(CurrentCell);
        }

        private void FinishArea()
        {
            var ui = GameUI.inst;
            var a = areaStart.Value;
            var b = cursor.Pos;
            areaStart = null;
            int minX = Mathf.Min(a.X, b.X), maxX = Mathf.Max(a.X, b.X) + 1;
            int minZ = Mathf.Min(a.Z, b.Z), maxZ = Mathf.Max(a.Z, b.Z) + 1;
            minX = Mathf.Clamp(minX, 0, World.inst.GridWidth - 1);
            maxX = Mathf.Clamp(maxX, 0, World.inst.GridWidth - 1);
            minZ = Mathf.Clamp(minZ, 0, World.inst.GridHeight - 1);
            maxZ = Mathf.Clamp(maxZ, 0, World.inst.GridHeight - 1);
            var mode = ui.currCursorMode;
            mode.HandleDragPrimaryUp(minX, minZ, maxX, maxZ, returnedToOriginal: false);
            A.Cue(Cue.Activate);
            string size = (maxX - minX) + " by " + (maxZ - minZ) + " tiles";
            if (mode is ChopCursorMode || mode is ChopCancelCursorMode)
            {
                int trees = 0;
                for (int z = minZ; z < maxZ; z++)
                    for (int x = minX; x < maxX; x++)
                    {
                        var c = World.inst.GetCellData(x, z);
                        if (c != null && c.TreeAmount > 0) trees++;
                    }
                A.Say(trees == 0 ? "No trees in this " + size + " area"
                    : (mode is ChopCursorMode ? TextUtil.Plural(trees, "tile") + " of trees marked for chopping in " : "Chopping cancelled on " + TextUtil.Plural(trees, "tile") + " of trees in ") + size);
                return;
            }
            A.Say("Applied to " + size);
        }

        /// <summary>Select the building or tile under the cursor (like a mouse click, but deterministic).</summary>
        private void SelectAt(Cell cell)
        {
            var ui = GameUI.inst;
            if (cell == null) return;
            if (!CellInfo.Explored(cell))
            {
                A.Cue(Cue.Error);
                A.Say("Unexplored");
                return;
            }
            Building b = null;
            if (cell.OccupyingStructure.Count > 0) b = cell.OccupyingStructure[cell.OccupyingStructure.Count - 1];
            else if (cell.SubStructure.Count > 0) b = cell.TopSubStructure;
            if (b != null && (!b.IsVisibleForFog() || b.Life <= 0f)) b = null;
            if (World.IsAILandmass(cell.landMassIdx) && !ui.diplomacyUI.Visible())
            {
                A.Say(CellInfo.Owner(cell) + ". " + (b != null ? CellInfo.BuildingSummary(b, false) : CellInfo.Terrain(cell)));
                return;
            }
            ui.ClearSelection();
            ui.ClearUIForClick();
            if (b != null)
            {
                ui.SetSelectedBuilding(b);
                ui.SelectCell(b.GetCell());
            }
            else
            {
                ui.selectedBuilding = null;
                ui.SelectCell(cell);
            }
            lastSelected = ui.GetBuildingSelected();
            A.Cue(Cue.Activate);
            SpeakSelection(detailed: false);
        }

        /// <summary>Shift+Enter outside placement: select soldiers / villagers on the tile.</summary>
        private void SelectUnitAt(Cell cell)
        {
            var ui = GameUI.inst;
            if (cell == null) return;
            var armies = UnitSystem.inst.armies;
            var here = new List<UnitSystem.Army>();
            for (int i = 0; i < armies.Count; i++)
            {
                var a = armies.data[i];
                if (a == null) continue;
                Vector3 p = a.GetPos();
                if (Mathf.Abs(p.x - cell.Center.x) <= 1.5f && Mathf.Abs(p.z - cell.Center.z) <= 1.5f && ui.IsUnitSelectable(a)) here.Add(a);
            }
            if (here.Count > 0)
            {
                // Cycle through the armies here on repeated presses; Ctrl adds to the selection.
                int idx = 0;
                for (int i = 0; i < here.Count; i++) if (ui.IsSelected(here[i])) idx = i + 1;
                var army = here[idx % here.Count];
                if (!KInput.Ctrl) ui.ClearSelection();
                if (ui.AddToSelection(army))
                {
                    if (ui.DisplayIMovableUI(army)) ui.ClearCellSelected();
                }
                A.Cue(Cue.Activate);
                A.Say("Selected " + CellInfo.Units_ArmyName(army).Replace(", selected", "") + ". Move the cursor and press M to send them there. F6 opens the army panel.");
                return;
            }
            // Ships: merchants open the trade window, your own ships can then be sent with M.
            var ships = ShipSystem.inst.ships;
            var shipsHere = new List<ShipBase>();
            for (int i = 0; i < ships.Count; i++)
            {
                var sh = ships.data[i];
                if (sh == null || !(sh is ISelectable)) continue;
                Vector3 p = sh.GetPos();
                if (Mathf.Abs(p.x - cell.Center.x) <= 1.5f && Mathf.Abs(p.z - cell.Center.z) <= 1.5f) shipsHere.Add(sh);
            }
            if (shipsHere.Count > 0)
            {
                int idx = 0;
                for (int i = 0; i < shipsHere.Count; i++) if (ui.IsSelected((ISelectable)shipsHere[i])) idx = i + 1;
                var ship = shipsHere[idx % shipsHere.Count];
                ui.ClearSelection();
                ui.AddToSelection((ISelectable)ship);
                A.Cue(Cue.Activate);
                bool merchant = ship.type == ShipBase.ShipType.Merchant || ship.type == ShipBase.ShipType.PlayerMerchant;
                A.Say("Selected " + CellInfo.ShipName(ship).Replace(", selected", "") + (merchant ? ". F6 opens the trade window." : ship.teamID == 0 ? ". M sends it to the cursor, F6 opens its panel." : "."));
                return;
            }
            var villagers = World.inst.GetVillagersAt(cell.x, cell.z);
            if (villagers != null && villagers.Count > 0)
            {
                // Pressing again on the same tile moves to the next villager there.
                int vi = 0;
                var current = ui.personUI != null && ui.personUI.Visible ? ui.personUI.villager : null;
                for (int i = 0; i < villagers.Count; i++) if (villagers.data[i] == current) vi = i + 1;
                var v = villagers.data[vi % villagers.Count];
                ui.ClearUIForClick();
                ui.SelectPerson(v);
                A.Cue(Cue.Activate);
                A.Say("Selected " + CellInfo.VillagerSummary(v) + (villagers.Count > 1 ? ". " + (vi % villagers.Count + 1) + " of " + villagers.Count + " here, Shift Enter for the next" : string.Empty) + ". F6 for details.");
                return;
            }
            A.Cue(Cue.Error);
            A.Say("No soldiers, ships or villagers here");
        }

        private void MoveUnits()
        {
            var ui = GameUI.inst;
            if (!ui.IsUnitSelected())
            {
                A.Cue(Cue.Error);
                A.Say("No soldiers selected. Use Shift Enter on soldiers to select them.");
                return;
            }
            ui.MoveUnitsToPosition(CurrentCell.Center);
            A.Cue(Cue.Activate);
            A.Say("Moving to " + cursor.Pos);
        }

        internal void SpeakSelection(bool detailed)
        {
            var ui = GameUI.inst;
            var b = ui.GetBuildingSelected();
            string text;
            if (b != null)
            {
                text = CellInfo.BuildingSummary(b, brief: false);
                if (detailed) text = TextUtil.Sentences(text, b.Description);
                text = TextUtil.Sentences(text, "F6 for the building panel");
            }
            else if (ui.GetCellSelected() != null)
            {
                text = TextUtil.Sentences("Selected tile", CellInfo.Brief(ui.GetCellSelected(), true), "F6 for tile actions");
            }
            else if (ui.IsUnitSelected())
            {
                text = "Soldiers selected. M moves them to the cursor.";
            }
            else text = "Nothing selected";
            A.Say(text, force: true);
        }

        // ------------------------------------------------------------------ placement feedback

        private PlacementValidationResult? CurrentValidity()
        {
            var ui = GameUI.inst;
            var hover = ui.CurrPlacementMode.GetHoverBuilding();
            if (hover == null) return null;
            try
            {
                return World.inst.CanPlace(hover);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Which footprint tiles block the building, relative to the cursor ("forest 1 north, 2 east").</summary>
        private string FootprintProblems()
        {
            var hover = GameUI.inst.CurrPlacementMode.GetHoverBuilding();
            if (hover == null) return string.Empty;
            var problems = new List<string>();
            var origin = cursor.Pos;
            hover.ForEachTileInBounds((x, z, c) =>
            {
                if (problems.Count >= 3 || c == null) return;
                string what = null;
                if (c.Type != ResourceType.None) what = CellInfo.Terrain(c);
                else if (c.TreeAmount > 0) what = "trees";
                else if (TreeSystem.inst != null && TreeSystem.inst.AnimCount(c) > 0) what = "a tree growing or being felled";
                else if (c.OccupyingStructure.Count > 0 && c.TopMostStructure != null) what = c.TopMostStructure.FriendlyName;
                if (what == null) return;
                problems.Add(what + " " + Directions.Offset(x - origin.X, z - origin.Z));
            });
            return problems.Count == 0 ? string.Empty : "blocked by " + string.Join(", ", problems.ToArray());
        }

        private static GridPos? NearestCoveredFreeTile(GridPos at, int maxRadius)
        {
            var w = World.inst;
            for (int r = 1; r <= maxRadius; r++)
            {
                GridPos? best = null;
                double bestD = double.MaxValue;
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        var c = w.GetCellData(at.X + dx, at.Z + dz);
                        if (c == null || c.Type != ResourceType.None || c.TreeAmount > 0 || c.OccupyingStructure.Count > 0) continue;
                        if (c.RoadCoverage <= 0 && c.RoadConnectCoverage <= 0) continue;
                        var p = new GridPos(c.x, c.z);
                        double d = Directions.Euclid(at, p);
                        if (d < bestD) { bestD = d; best = p; }
                    }
                if (best.HasValue) return best;
            }
            return null;
        }

        private string Explain(PlacementValidationResult r)
        {
            string text = PlacementText.Explain(r.ToString());
            if (r == PlacementValidationResult.OutsideOfTerritory || r == PlacementValidationResult.RoadCoverage)
            {
                var near = NearestCoveredFreeTile(cursor.Pos, 15);
                if (near.HasValue) text += ". Nearest free tile inside road coverage: " + Directions.Relative(cursor.Pos, near.Value);
            }
            if (r == PlacementValidationResult.MustBeOnFlatLand || r == PlacementValidationResult.ExistingStructure || r == PlacementValidationResult.RoadNotOnLand)
            {
                string fp = FootprintProblems();
                if (fp.Length > 0) text += ", " + fp;
            }
            return text;
        }

        private string lastPlacedName;
        private bool waitingAgainLastFrame;

        private void SpeakValidity(bool always)
        {
            var r = CurrentValidity();
            if (!r.HasValue) return;
            bool changed = r.Value != lastValidity;
            lastValidity = r.Value;
            if (r.Value == PlacementValidationResult.Valid)
            {
                A.Cue(Cue.PlaceValid);
                if (always) A.Say("Can build here", force: true);
                else if (changed) A.SayQueued("can build");
            }
            else
            {
                A.Cue(Cue.PlaceInvalid);
                if (always || changed) A.SayQueued(Explain(r.Value));
            }
            int count = GameUI.inst.CurrPlacementMode.PlacementCount();
            if (count > 1)
            {
                int ok = 0;
                foreach (var b in GameUI.inst.CurrPlacementMode.buildings)
                    if (b != null && World.inst.CanPlace(b) == PlacementValidationResult.Valid) ok++;
                A.SayQueued(TextUtil.Plural(count, "piece") + " planned" + (ok < count ? ", " + ok + " can be built" : string.Empty));
            }
        }

        /// <summary>Keeps announcements running while a panel has the keyboard. Returns true when a cursor mode or placement started.</summary>
        internal bool TrackWhileInPanel()
        {
            EnsureInit();
            var ui = GameUI.inst;
            if (ui == null) return false;
            bool modeBefore = ui.currCursorMode != lastCursorMode;
            bool placingStarted = IsPlacing && !wasPlacing;
            bool brushStarted = ui.brushMode != lastBrush && ui.brushMode != GameUI.CursorBrushes.None;
            TrackGameState();
            return modeBefore || placingStarted || brushStarted;
        }

        /// <summary>Announces placement start/end, cursor-mode changes and selection changes made by the game.</summary>
        private void TrackGameState()
        {
            var ui = GameUI.inst;
            if (ui == null) return;
            bool placing = IsPlacing;
            if (placing)
            {
                var hover = ui.CurrPlacementMode.GetHoverBuilding();
                string name = hover != null ? hover.FriendlyName : null;
                if (!wasPlacing || name != lastHeldName)
                {
                    lastHeldName = name;
                    lastValidity = (PlacementValidationResult)(-1);
                    validityCheckFrame = Time.frameCount + 2;
                    // "Place another" picks the same building up again: the player already knows, stay quiet.
                    bool again = name == lastPlacedName && waitingAgainLastFrame;
                    if (!wasPlacing && !again && Time.unscaledTime - placedSayTime > 0.5f)
                    {
                        UpdatePointer(follow: true);
                        string size = hover != null && (hover.size.x > 1 || hover.size.z > 1) ? " The cursor is the south west corner, the building covers " + (int)hover.size.x + " by " + (int)hover.size.z + " tiles." : string.Empty;
                        A.SayQueued("Placing " + name + "." + size + " Move with arrows, Enter to build, R to rotate, Escape to cancel.");
                    }
                }
                if (validityCheckFrame > 0 && Time.frameCount >= validityCheckFrame)
                {
                    validityCheckFrame = -1;
                    SpeakValidity(always: false);
                }
            }
            else if (wasPlacing)
            {
                var vp = VirtualPointer.Inst;
                if (vp != null && vp.IsHeld) vp.Release();
                if (Time.unscaledTime - placedSayTime > 0.3f && !ui.WaitingToPlaceAgain())
                {
                    A.Cue(Cue.Close);
                    A.Say("Placement ended");
                }
                lastSelected = ui.GetBuildingSelected();
            }
            wasPlacing = placing;
            waitingAgainLastFrame = ui.WaitingToPlaceAgain();

            // Escape (game key) clears the selection: say so, since nothing visible tells a blind player.
            bool hasSelection = ui.GetBuildingSelected() != null || ui.GetCellSelected() != null || ui.IsUnitSelected() || (ui.personUI != null && ui.personUI.Visible);
            if (hadSelection && !hasSelection && !placing && Time.unscaledTime - placedSayTime > 0.5f) A.Say("Selection cleared");
            hadSelection = hasSelection;

            if (ui.brushMode != lastBrush)
            {
                lastBrush = ui.brushMode;
                if (ui.brushMode == GameUI.CursorBrushes.None) A.Say("Brush off");
                else A.Say("Brush: " + BrushName(ui.brushMode) + ". Enter applies it at the cursor, Escape turns the brush off.");
            }

            var mode = ui.currCursorMode;
            if (mode != lastCursorMode)
            {
                lastCursorMode = mode;
                areaStart = null;
                if (mode == null || mode == ui.consoleCursorMode) A.Say("Normal mode");
                else A.Say(CursorModeName(mode) + ". Enter on a tile applies it, Shift Enter marks an area. Escape returns to normal mode.");
            }
        }

        /// <summary>Called by the AcceptPlacement hook.</summary>
        internal void OnPlacementAccepted(bool success, string name, int pieces, string skipped = null)
        {
            placedSayTime = Time.unscaledTime;
            if (success && pieces > 0)
            {
                lastPlacedName = name;
                A.Cue(Cue.Placed);
                string what = pieces > 1 ? name + ", " + pieces + " pieces" : name;
                if (skipped != null) what += ", " + skipped;
                bool again = GameUI.inst != null && GameUI.inst.CanPlaceAgain();
                A.Say("Placed " + what + (again ? ". Move to place another, Escape to stop." : "."));
                if (name == GameState.inst.GetPlaceableByUniqueName("keep")?.FriendlyName)
                    A.SayQueued("Next build roads out from your keep: B, Town category, Road. Other buildings must be inside road coverage. I on a tile tells you whether it is inside road coverage.");
            }
            else
            {
                var r = CurrentValidity();
                A.Cue(Cue.PlaceInvalid);
                A.Say("Not built" + (r.HasValue && r.Value != PlacementValidationResult.Valid ? ": " + Explain(r.Value) : string.Empty));
            }
        }

        private static string CursorModeName(CursorMode m)
        {
            switch (m)
            {
                case ChopCursorMode _: return "Chop trees mode";
                case ChopCancelCursorMode _: return "Cancel chopping mode";
                case DemolishCursorMode _: return "Demolish mode";
                case RebuildCursorMode _: return "Rebuild mode";
                case DockRouteCursorMode _: return "Ship route mode";
                default: return TextUtil.Humanize(m.GetType().Name.Replace("CursorMode", "")) + " mode";
            }
        }
    }
}
