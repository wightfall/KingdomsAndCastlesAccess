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

        /// <summary>What is going on on the map right now, for F1.</summary>
        internal MapState State()
        {
            var ui = GameUI.inst;
            var st = new MapState { MenuMap = MenuMapMode, Walking = Nav.Walking, HasTarget = Nav.Target.HasValue };
            if (ui == null || MenuMapMode) return st;
            st.HasKeep = Player.inst != null && Player.inst.keep != null;
            st.Placing = IsPlacing;
            if (st.Placing)
            {
                var hover = ui.CurrPlacementMode.GetHoverBuilding();
                st.LinePlacement = hover != null && hover.dragPlacementMode != Building.DragPlacementMode.None;
            }
            st.RouteMode = ui.currCursorMode is DockRouteCursorMode;
            st.CursorMode = ui.currCursorMode != null && ui.currCursorMode != ui.consoleCursorMode && !st.RouteMode;
            st.Brush = ui.brushMode != GameUI.CursorBrushes.None;
            st.BuildingSelected = ui.GetBuildingSelected() != null;
            st.HasSelection = st.BuildingSelected || ui.GetCellSelected() != null;
            st.SoldiersSelected = ui.IsUnitSelected();
            return st;
        }

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
            if (Player.inst != null && Player.inst.keep == null)
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
            A.Say(Loc.F("Moved the cursor to the nearest land where you can build your keep: {0}", CellInfo.Brief(CurrentCell, false)));
            return true;
        }

        internal void OnEnterPlayMode()
        {
            initialized = false;
            MenuMapMode = false;
            enteredPlayAt = Time.unscaledTime;
            // A new or loaded world: the announcement tracking must not compare with the previous kingdom
            // (it said "Selection cleared" right after loading a save in which something had been selected).
            wasPlacing = false;
            lastHeldName = null;
            lastPlacedName = null;
            lastSelected = null;
            hadSelection = false;
            areaStart = null;
            lastValidity = (PlacementValidationResult)(-1);
            validityCheckFrame = -1;
            if (GameUI.inst != null)
            {
                lastCursorMode = GameUI.inst.currCursorMode;
                lastBrush = GameUI.inst.brushMode;
            }
            EnsureInit();
            scanner.Invalidate();
            Nav.ClearTarget();
            UpdatePointer(follow: Player.inst.keep == null);
            string intro = Player.inst.keep == null
                ? KCAccess.Core.KeyHelp.Resolve(Loc.T("Your kingdom begins. Build your keep first: press {BuildMenu}, choose the keep in the Castle category, then move to a good spot near fertile land, trees and stone ({Survey} surveys the area around the cursor), and press Enter. Press F1 for help."))
                : KCAccess.Core.KeyHelp.Resolve(Loc.T("Kingdom loaded. Press F1 for help, {Status} for status."));
            A.Say(intro + " " + Loc.F("Cursor at {0}", CellInfo.Brief(CurrentCell, false)));
        }

        internal void OnResume()
        {
            EnsureInit();
            enteredPlayAt = Time.unscaledTime;
            UpdatePointer(follow: false);
            A.Say(Loc.T("Resumed.") + " " + CellInfo.Brief(CurrentCell, false));
        }

        private float enteredPlayAt;

        internal void OnReturnToMap()
        {
            if (Time.unscaledTime - enteredPlayAt < 1.5f) return;
            A.Say(Loc.F("Map, {0}", CellInfo.Brief(CurrentCell, false)));
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
                    A.Say(Loc.T("Back to map setup menu"));
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
                if (KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.KeypadEnter))
                {
                    var edit = MapEditor();
                    if (edit != null && edit.brushMode != MapEdit.BrushMode.None)
                    {
                        VirtualPointer.Inst?.Click();
                        A.Cue(Cue.Activate);
                        // Say what was painted and how big the brush is (the game shows both only on screen).
                        A.Say(Loc.F("Painted {0}, brush size {1}, at {2}", Loc.T(TextUtil.Humanize(edit.brushMode.ToString()).ToLowerInvariant()), Mathf.RoundToInt(edit.radius + 1f), cursor.Pos));
                        return;
                    }
                    if (edit != null)
                    {
                        A.Cue(Cue.Error);
                        A.Say(Loc.T("No map editor brush is selected. Control M returns to the menu, where the brushes are."));
                        return;
                    }
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
                if (moved) A.Say(Loc.P(n, "{0} tile", "{0} tiles") + ", " + CellInfo.Brief(CurrentCell, Plugin.CfgVerboseCells.Value), force: true);
            }
            else
            {
                moved = cursor.Move(dx, dz, KInput.Shift ? 5 : 1) == MoveResult.Moved;
                if (moved) A.Say(CellInfo.Brief(CurrentCell, Plugin.CfgVerboseCells.Value), force: true);
            }
            if (!moved)
            {
                A.Cue(Cue.Edge);
                A.Say(Loc.T("edge of the map"));
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
                case GameUI.CursorBrushes.General: return Loc.T("Spawn knights");
                case GameUI.CursorBrushes.ArcherGeneral: return Loc.T("Spawn archers");
                case GameUI.CursorBrushes.Settlers: return Loc.T("Spawn settlers");
                case GameUI.CursorBrushes.Envoy: return Loc.T("Spawn envoy");
                case GameUI.CursorBrushes.Catapult: return Loc.T("Spawn catapult");
                case GameUI.CursorBrushes.Viking: return Loc.T("Spawn vikings");
                case GameUI.CursorBrushes.EliteViking: return Loc.T("Spawn stronger vikings");
                case GameUI.CursorBrushes.Ogre: return Loc.T("Spawn ogre");
                case GameUI.CursorBrushes.Villager: return Loc.T("Spawn peasants");
                case GameUI.CursorBrushes.SmallDragon: return Loc.T("Spawn dragon");
                case GameUI.CursorBrushes.SiegeDragon: return Loc.T("Spawn siege dragon");
                case GameUI.CursorBrushes.LargeDragon: return Loc.T("Spawn momma dragon");
                case GameUI.CursorBrushes.Delete: return Loc.T("Removal tool");
                case GameUI.CursorBrushes.Fire: return Loc.T("Create fire");
                case GameUI.CursorBrushes.Trees: return Loc.T("Create trees");
                default: return Loc.F("{0} stack", TextUtil.Humanize(b.ToString()));
            }
        }

        /// <summary>The creative mode map editor of the map setup screen, when it is shown; otherwise null.</summary>
        private static MapEdit MapEditor()
        {
            var mm = GameState.inst.mainMenuMode;
            if (mm == null || mm.mapEditUI == null || !mm.mapEditUI.activeInHierarchy) return null;
            return mm.mapEditUI.GetComponent<MapEdit>();
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
            if (KInput.Pressed("TileInfo"))
            {
                A.Say(CellInfo.Full(CurrentCell), force: true);
                return true;
            }
            if (KInput.Pressed("Survey"))
            {
                A.Say(CellInfo.Survey(cursor.Pos, 6).Format(), force: true);
                return true;
            }
            if (KInput.Pressed("Coordinates"))
            {
                A.Say(Loc.F("Cursor at {0}, {1}. Map is {2} by {3}.", cursor.Pos.X, cursor.Pos.Z, cursor.Width, cursor.Height), force: true);
                return true;
            }
            if (KInput.Pressed("PrevCategory") || KInput.Pressed("NextCategory"))
            {
                scanner.ChangeCategory(KInput.Pressed("NextCategory") ? 1 : -1, cursor.Pos);
                return true;
            }
            if (KInput.Pressed("NextItem") || KInput.Pressed("PrevItem"))
            {
                var item = scanner.Step(KInput.Pressed("NextItem") ? 1 : -1, cursor.Pos);
                if (item != null)
                {
                    Nav.SetTarget(item.Pos, item.Label);
                    // The cursor does not move: say so the first few times, players expected a jump.
                    if (targetHints < 3 && Plugin.CfgHints.Value)
                    {
                        targetHints++;
                        A.SayQueued(KCAccess.Core.KeyHelp.Resolve(Loc.T("Target set, the cursor stays here. {JumpTarget} jumps there, {Walk} walks there.")));
                    }
                }
                return true;
            }
            if (KInput.Pressed("JumpTarget"))
            {
                if (!Nav.Target.HasValue)
                {
                    A.Cue(Cue.Error);
                    A.Say(KCAccess.Core.KeyHelp.Resolve(Loc.T("No target. Choose one with {PrevCategory}, {NextCategory} and {PrevItem} or {NextItem}.")));
                }
                else
                {
                    A.Say(Nav.TargetLabel);
                    JumpTo(Nav.Target.Value);
                }
                return true;
            }
            if (KInput.Pressed("WhereTarget"))
            {
                A.Say(Nav.Describe(cursor.Pos), force: true);
                return true;
            }
            if (KInput.Pressed("Walk"))
            {
                Nav.StartWalk(this, straight: false);
                return true;
            }
            if (KInput.Pressed("WalkStraight"))
            {
                Nav.StartWalk(this, straight: true);
                return true;
            }
            if (KInput.Pressed("Beacon"))
            {
                Nav.ToggleBeacon(cursor.Pos);
                return true;
            }
            if (MenuMapMode) return false;
            if (KInput.Pressed("Status"))
            {
                AccessController.Inst.ActiveMenu = new StatusMenu();
                return true;
            }
            if (KInput.Pressed("Date"))
            {
                A.Say(Status.DateLine(), force: true);
                return true;
            }
            if (KInput.Pressed("Log"))
            {
                AccessController.Inst.ActiveMenu = new LogBrowser();
                return true;
            }
            if (KInput.Pressed("LastNotification"))
            {
                var n = GameEvents.Log.Count > 0 ? GameEvents.Log.Items[0] : null;
                A.Say(n != null ? n.Describe() : Loc.T("No notifications yet"), force: true);
                return true;
            }
            if (KInput.Pressed("Keep"))
            {
                if (Player.inst.keep != null)
                {
                    var c = Player.inst.keep.GetComponent<Building>().GetCell();
                    A.Say(Loc.T("Keep"));
                    JumpTo(new GridPos(c.x, c.z));
                }
                else
                {
                    A.Cue(Cue.Error);
                    A.Say(Loc.T("You have no keep yet"));
                }
                return true;
            }
            if (KInput.Pressed("SelectedBuilding"))
            {
                var b = GameUI.inst.GetBuildingSelected();
                var cell = b != null ? b.GetCell() : GameUI.inst.GetCellSelected();
                if (cell != null)
                {
                    A.Say(Loc.T("Selection"));
                    JumpTo(new GridPos(cell.x, cell.z));
                }
                else A.Say(Loc.T("Nothing selected"));
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
                        A.Say(Loc.F("Bookmark {0} is empty", i), force: true);
                    }
                    else
                    {
                        Nav.SetTarget(bp.Value, Loc.F("bookmark {0}", i));
                        A.Say(Loc.F("Target bookmark {0}, {1}", i, Directions.Relative(cursor.Pos, bp.Value)), force: true);
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
                A.Say(Loc.F("Bookmark {0} set at {1}", slot, cursor.Pos), force: true);
                return true;
            }
            var p = bookmarks.Get(KingdomKey, slot);
            if (!p.HasValue)
            {
                A.Cue(Cue.Error);
                A.Say(Loc.F("Bookmark {0} is empty. Control Shift {0} stores the cursor position.", slot), force: true);
                return true;
            }
            A.Say(Loc.F("Bookmark {0}", slot));
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
                A.Say(Loc.T("Placement ended"));
                return;
            }
            if (KInput.Plain(KeyCode.Escape) && ui.brushMode != GameUI.CursorBrushes.None)
            {
                KInput.Consume(KeyCode.Escape);
                ui.brushMode = GameUI.CursorBrushes.None;
                return;
            }
            if (KInput.Pressed("ChopMode") && !IsPlacing)
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
                if (sel == null) A.Say(KeyHelp.Resolve(Loc.T("Select a forest tile with Enter first, then press C. For a large area use chop trees mode, {ChopMode}.")));
                else if (sel.TreeAmount == 0) A.Say(Loc.T("No trees on the selected tile"));
            }
            if (KInput.Pressed("BuildMenu"))
            {
                AccessController.Inst.ActiveMenu = new BuildMenu();
                return;
            }
            if (KInput.Plain(KeyCode.U))
            {
                A.Cue(Cue.Error);
                A.Say(Loc.T("U would hide the game interface, which makes every panel unreadable, so the mod blocks it."));
                return;
            }
            if (KInput.Down(KeyCode.F6) && !KInput.Ctrl && !KInput.Alt)
            {
                if (GameState.inst.IsPlayMode() && !ui.gameObject.activeSelf)
                {
                    // The interface was hidden (by the game's U key or another mod): bring it back first.
                    ui.gameObject.SetActive(true);
                    A.Say(Loc.T("The game interface was hidden; shown again."));
                }
                if (!AccessController.Inst.FocusPanel(KInput.Shift ? -1 : 1))
                {
                    A.Cue(Cue.Error);
                    A.Say(Loc.T("No panel is open"));
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
            if (KInput.Down(KeyCode.Return) && KInput.Ctrl && KInput.Shift && !KInput.Alt && !IsPlacing)
            {
                // Control Shift Enter: add the soldiers on this tile to the ones already selected.
                UpdatePointer(follow: false);
                SelectUnitAt(CurrentCell, add: true);
                return;
            }
            if (KInput.Pressed("Validity") && IsPlacing)
            {
                SpeakValidity(always: true);
                return;
            }
            if (KInput.Pressed("Alert"))
            {
                Alerts.RespondNearest();
                return;
            }
            if (KInput.Pressed("MoveSoldiers"))
            {
                MoveUnits();
                return;
            }
            if (KInput.Pressed("BuildingDetails"))
            {
                SpeakSelection(detailed: true);
                return;
            }
            if (IsPlacing && KInput.Plain(KeyCode.R))
            {
                // The game rotates on R itself; announce afterwards.
                validityCheckFrame = Time.frameCount + 2;
                lastValidity = (PlacementValidationResult)(-1);
                A.Say(Loc.T("Rotated"));
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
                    A.Say(Loc.F("Cannot build here: {0}", Explain(result.Value)));
                    return;
                }
                vp.Click();
                return;
            }
            if (ui.brushMode != GameUI.CursorBrushes.None)
            {
                vp.Click();
                A.Cue(Cue.Activate);
                A.Say(Loc.F("{0} at {1}", BrushName(ui.brushMode), cursor.Pos));
                return;
            }
            if (ui.currCursorMode is DockRouteCursorMode)
            {
                // Route stops are dragged with the mouse in the game; here they are moved from the route panel.
                Routes.SpeakTileForStop(cell);
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
                    A.Say(Loc.F("Start point set at {0}. Move to the end point and press Enter to build, Escape to cancel.", cursor.Pos));
                }
                else
                {
                    vp.Release();
                }
                return;
            }
            // The route mode (a ship or cart panel is open) has no areas: Shift Enter still selects units there.
            if (ui.currCursorMode != null && ui.currCursorMode != ui.consoleCursorMode && !(ui.currCursorMode is DockRouteCursorMode))
            {
                if (!areaStart.HasValue)
                {
                    areaStart = cursor.Pos;
                    A.Cue(Cue.Open);
                    A.Say(Loc.T("Area corner set. Move to the opposite corner and press Enter."));
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
            maxX = Mathf.Clamp(maxX, 0, World.inst.GridWidth); // exclusive bound, like the game's drag
            minZ = Mathf.Clamp(minZ, 0, World.inst.GridHeight - 1);
            maxZ = Mathf.Clamp(maxZ, 0, World.inst.GridHeight);
            var mode = ui.currCursorMode;
            mode.HandleDragPrimaryUp(minX, minZ, maxX, maxZ, returnedToOriginal: false);
            A.Cue(Cue.Activate);
            string size = Loc.F("{0} by {1} tiles", maxX - minX, maxZ - minZ);
            if (mode is ChopCursorMode || mode is ChopCancelCursorMode)
            {
                int trees = 0;
                for (int z = minZ; z < maxZ; z++)
                    for (int x = minX; x < maxX; x++)
                    {
                        var c = World.inst.GetCellData(x, z);
                        if (c != null && c.TreeAmount > 0) trees++;
                    }
                A.Say(trees == 0 ? Loc.F("No trees in this {0} area", size)
                    : mode is ChopCursorMode ? Loc.P(trees, "{0} tile of trees marked for chopping in {1}", "{0} tiles of trees marked for chopping in {1}", size)
                    : Loc.P(trees, "Chopping cancelled on {0} tile of trees in {1}", "Chopping cancelled on {0} tiles of trees in {1}", size));
                return;
            }
            A.Say(Loc.F("Applied to {0}", size));
        }

        /// <summary>Select the building or tile under the cursor (like a mouse click, but deterministic).</summary>
        private void SelectAt(Cell cell)
        {
            var ui = GameUI.inst;
            if (cell == null) return;
            if (!CellInfo.Explored(cell))
            {
                A.Cue(Cue.Error);
                A.Say(Loc.T("Unexplored"));
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

        private static bool Near(Vector3 p, Cell cell) => Mathf.Abs(p.x - cell.Center.x) <= 1.5f && Mathf.Abs(p.z - cell.Center.z) <= 1.5f;

        /// <summary>
        /// Shift+Enter outside placement: select soldiers, siege catapults, dragons, ships, carts or villagers on the tile.
        /// With <paramref name="add"/> (Control Shift Enter) the soldiers are added to the current selection.
        /// </summary>
        private void SelectUnitAt(Cell cell, bool add = false)
        {
            var ui = GameUI.inst;
            if (cell == null) return;
            // Everything the game lets you select and send with a right click: armies, siege catapults, your dragons.
            var here = new List<IMoveableUnit>();
            var armies = UnitSystem.inst.armies;
            for (int i = 0; i < armies.Count; i++)
            {
                var a = armies.data[i];
                if (a != null && Near(a.GetPos(), cell) && ui.IsUnitSelectable(a)) here.Add(a);
            }
            var catapults = SiegeCatapultSystem.siegeCatapults;
            for (int i = 0; catapults != null && i < catapults.Count; i++)
            {
                var sc = catapults.data[i];
                if (sc != null && Near(sc.GetPos(), cell) && ui.IsUnitSelectable(sc)) here.Add(sc);
            }
            var dragons = DragonSpawn.inst != null ? DragonSpawn.inst.currentDragons : null;
            for (int i = 0; dragons != null && i < dragons.Count; i++)
            {
                var d = dragons.data[i];
                if (d != null && Near(d.transform.position, cell) && ui.IsUnitSelectable(d)) here.Add(d);
            }
            if (here.Count > 0)
            {
                // Cycle through the units here on repeated presses; Control Shift Enter adds to the selection.
                int idx = 0;
                for (int i = 0; i < here.Count; i++) if (ui.IsSelected((ISelectable)here[i])) idx = i + 1;
                if (add)
                {
                    // Take the first one here that is not selected yet.
                    idx = here.FindIndex(u => !ui.IsSelected((ISelectable)u));
                    if (idx < 0)
                    {
                        A.Cue(Cue.Edge);
                        A.Say(Loc.T("Everything on this tile is already selected"));
                        return;
                    }
                }
                var unit = here[idx % here.Count];
                if (!add) ui.ClearSelection();
                if (ui.AddToSelection((ISelectable)unit))
                {
                    if (ui.DisplayIMovableUI(unit)) ui.ClearCellSelected();
                }
                A.Cue(Cue.Activate);
                int selected = 0;
                foreach (var s in ui.selectedObjs) if (s is IMoveableUnit) selected++;
                string what = CellInfo.WithoutSelected(CellInfo.UnitName(unit));
                A.Say((add ? Loc.P(selected, "Added {1}, {0} unit selected", "Added {1}, {0} units selected", what) : Loc.F("Selected {0}", what))
                    + ". " + KeyHelp.Resolve(Loc.T("Move the cursor and press {MoveSoldiers} to send them there. F6 opens the army panel.")));
                return;
            }
            if (add)
            {
                A.Cue(Cue.Error);
                A.Say(Loc.T("No soldiers of yours here to add to the selection"));
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
                string how;
                if (merchant) how = Loc.T("F6 opens the trade window.");
                else if (ship.teamID != 0) how = null;
                else if (ship is ILogisticTransport) how = Loc.T("F6 opens its route panel: each stop with its cargo, and {MoveSoldiers} on a stop moves it to the cursor.");
                else how = Loc.T("{MoveSoldiers} sends it to the cursor, F6 opens its panel.");
                A.Say(Loc.F("Selected {0}", CellInfo.WithoutSelected(CellInfo.ShipName(ship))) + ". " + (how != null ? KeyHelp.Resolve(how) : string.Empty));
                return;
            }
            // Transport carts drive routes between stockpiles, markets and other buildings; selecting one opens its route.
            if (CartSystem.inst != null)
            {
                var carts = CartSystem.inst.carts.FindAll(c => c != null && c.TeamID() == 0 && Near(c.GetPos(), cell));
                if (carts.Count > 0)
                {
                    int idx = 0;
                    for (int i = 0; i < carts.Count; i++) if (ui.IsSelected(carts[i])) idx = i + 1;
                    var cart = carts[idx % carts.Count];
                    ui.ClearSelection();
                    ui.AddToSelection(cart);
                    A.Cue(Cue.Activate);
                    A.Say(Loc.F("Selected {0}", CellInfo.WithoutSelected(CellInfo.CartName(cart))) + ". " + KeyHelp.Resolve(Loc.T("F6 opens its route panel: each stop with its cargo, and {MoveSoldiers} on a stop moves it to the cursor.")));
                    return;
                }
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
                A.Say(Loc.F("Selected {0}", CellInfo.VillagerSummary(v)) + (villagers.Count > 1 ? ". " + Loc.F("{0} of {1} here, Shift Enter for the next", vi % villagers.Count + 1, villagers.Count) : string.Empty) + ". " + Loc.T("F6 for details."));
                return;
            }
            A.Cue(Cue.Error);
            A.Say(Loc.T("No soldiers, catapults, dragons, ships, carts or villagers here"));
        }

        private void MoveUnits()
        {
            var ui = GameUI.inst;
            if (!ui.IsUnitSelected())
            {
                A.Cue(Cue.Error);
                A.Say(ui.currCursorMode is DockRouteCursorMode
                    ? KeyHelp.Resolve(Loc.T("Ships and carts follow their route. F6 opens the route panel; {MoveSoldiers} on a stop moves that stop to the cursor."))
                    : Loc.T("No soldiers selected. Use Shift Enter on soldiers to select them."));
                return;
            }
            var cell = CurrentCell;
            // The game takes the attack or visit target (an enemy building, a keep for an envoy) from what the pointer
            // hovers, but hovers no building while the real mouse rests over a panel; then M only walked there.
            // Pick the building on the cursor tile exactly like the game's own hover does.
            if (ui.highlightBuilding == null && cell != null)
            {
                if (cell.OccupyingStructure.Count > 0 && cell.OccupyingStructure[0].IsVisibleForFog()) ui.highlightBuilding = cell.OccupyingStructure[0];
                else if (cell.SubStructure.Count > 0 && cell.TopSubStructure.IsVisibleForFog()) ui.highlightBuilding = cell.TopSubStructure;
                if (ui.highlightBuilding != null && ui.highlightBuilding.Life <= 0f) ui.highlightBuilding = null;
            }
            string target = null;
            foreach (var h in ui.highlightedObjs)
            {
                if (h is IMoveableUnit mu && !ui.IsSelected(h) && mu.TeamID() != 0 && World.inst.RelationBetween(0, mu.TeamID()) == World.Relations.Enemy)
                {
                    target = Loc.F("Attacking {0}", CellInfo.UnitName(mu));
                    break;
                }
            }
            var b = ui.highlightBuilding;
            if (target == null && b != null && b.TeamID() != 0)
                target = World.inst.RelationBetween(0, b.TeamID()) == World.Relations.Enemy ? Loc.F("Attacking {0}", CellInfo.BuildingSummary(b, brief: true)) : Loc.F("Going to {0}", CellInfo.BuildingSummary(b, brief: true));
            ui.MoveUnitsToPosition(cell.Center);
            A.Cue(Cue.Activate);
            A.Say(target ?? Loc.F("Moving to {0}", cursor.Pos));
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
                text = TextUtil.Sentences(text, Loc.T("F6 for the building panel"));
            }
            else if (ui.GetCellSelected() != null)
            {
                text = TextUtil.Sentences(Loc.T("Selected tile"), CellInfo.Brief(ui.GetCellSelected(), true), Loc.T("F6 for tile actions"));
            }
            else if (ui.IsUnitSelected())
            {
                text = KeyHelp.Resolve(Loc.T("Soldiers selected. {MoveSoldiers} moves them to the cursor."));
            }
            else text = Loc.T("Nothing selected");
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
                else if (c.TreeAmount > 0) what = Loc.T("trees");
                else if (TreeSystem.inst != null && TreeSystem.inst.AnimCount(c) > 0) what = Loc.T("a tree growing or being felled");
                else if (c.OccupyingStructure.Count > 0 && c.TopMostStructure != null) what = c.TopMostStructure.FriendlyName;
                if (what == null) return;
                problems.Add(what + " " + Directions.Offset(x - origin.X, z - origin.Z));
            });
            return problems.Count == 0 ? string.Empty : Loc.F("blocked by {0}", string.Join(", ", problems.ToArray()));
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
                if (near.HasValue) text += ". " + Loc.F("Nearest free tile inside road coverage: {0}", Directions.Relative(cursor.Pos, near.Value));
            }
            if (r == PlacementValidationResult.MustBeOnFlatLand || r == PlacementValidationResult.ExistingStructure || r == PlacementValidationResult.RoadNotOnLand)
            {
                string fp = FootprintProblems();
                if (fp.Length > 0) text += ", " + fp;
            }
            return text;
        }

        private string lastPlacedName;
        private int targetHints;
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
                // Towers: the range rings (which grow on castle walls) are drawn only; V tells them.
                string range = always ? CellInfo.RangeText(GameUI.inst.CurrPlacementMode.GetHoverBuilding(), placed: true) : null;
                if (always) A.Say(Loc.T("Can build here") + (range != null ? ", " + range : string.Empty), force: true);
                else if (changed) A.SayQueued(Loc.T("can build"));
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
                A.SayQueued(Loc.P(count, "{0} piece planned", "{0} pieces planned") + (ok < count ? ", " + Loc.F("{0} can be built", ok) : string.Empty));
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
                        string size = hover != null && (hover.size.x > 1 || hover.size.z > 1) ? " " + Loc.F("The cursor is the south west corner, the building covers {0} by {1} tiles.", (int)hover.size.x, (int)hover.size.z) : string.Empty;
                        A.SayQueued(Loc.F("Placing {0}.", name) + size + " " + Loc.T("Move with arrows, Enter to build, R to rotate, Escape to cancel."));
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
                    A.Say(Loc.T("Placement ended"));
                }
                lastSelected = ui.GetBuildingSelected();
            }
            wasPlacing = placing;
            waitingAgainLastFrame = ui.WaitingToPlaceAgain();

            // Escape (game key) clears the selection: say so, since nothing visible tells a blind player.
            bool hasSelection = ui.GetBuildingSelected() != null || ui.GetCellSelected() != null || ui.IsUnitSelected() || (ui.personUI != null && ui.personUI.Visible);
            if (hadSelection && !hasSelection && !placing && Time.unscaledTime - placedSayTime > 0.5f && Time.unscaledTime - Patch_Demolish.LastDemolishTime > 1f) A.Say(Loc.T("Selection cleared"));
            hadSelection = hasSelection;

            if (ui.brushMode != lastBrush)
            {
                lastBrush = ui.brushMode;
                if (ui.brushMode == GameUI.CursorBrushes.None) A.Say(Loc.T("Brush off"));
                else A.Say(Loc.F("Brush: {0}. Enter applies it at the cursor, Escape turns the brush off.", BrushName(ui.brushMode)));
            }

            var mode = ui.currCursorMode;
            if (mode != lastCursorMode)
            {
                lastCursorMode = mode;
                areaStart = null;
                if (mode == null || mode == ui.consoleCursorMode) A.Say(Loc.T("Normal mode"));
                else if (mode is DockRouteCursorMode) A.SayQueued(Routes.ModeIntro());
                else A.Say(Loc.F("{0}. Enter on a tile applies it, Shift Enter marks an area. Escape returns to normal mode.", CursorModeName(mode)));
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
                string what = pieces > 1 ? Loc.F("{0}, {1} pieces", name, pieces) : name;
                if (skipped != null) what += ", " + skipped;
                bool again = GameUI.inst != null && GameUI.inst.CanPlaceAgain();
                A.Say(Loc.F("Placed {0}.", what) + (again ? " " + Loc.T("Move to place another, Escape to stop.") : string.Empty));
                if (name == GameState.inst.GetPlaceableByUniqueName("keep")?.FriendlyName)
                    A.SayQueued(KeyHelp.Resolve(Loc.T("Next build roads out from your keep: {BuildMenu}, Town category, Road. Other buildings must be inside road coverage. {TileInfo} on a tile tells you whether it is inside road coverage.")));
            }
            else
            {
                var r = CurrentValidity();
                A.Cue(Cue.PlaceInvalid);
                A.Say(r.HasValue && r.Value != PlacementValidationResult.Valid ? Loc.F("Not built: {0}", Explain(r.Value)) : Loc.T("Not built"));
            }
        }

        private static string CursorModeName(CursorMode m)
        {
            switch (m)
            {
                case ChopCursorMode _: return Loc.T("Chop trees mode");
                case ChopCancelCursorMode _: return Loc.T("Cancel chopping mode");
                case DemolishCursorMode _: return Loc.T("Demolish mode");
                case RebuildCursorMode _: return Loc.T("Rebuild mode");
                case DockRouteCursorMode _: return Loc.T("Ship route mode");
                default: return Loc.F("{0} mode", TextUtil.Humanize(m.GetType().Name.Replace("CursorMode", "")));
            }
        }
    }
}
