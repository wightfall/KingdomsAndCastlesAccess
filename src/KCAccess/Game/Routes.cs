using System.Collections.Generic;
using Assets.Code;
using KCAccess.Core;
using KCAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace KCAccess.Game
{
    /// <summary>
    /// Ship and transport cart routes (ShipLogisticsUI with DockRouteCursorMode). In the game the route's stops are
    /// coloured markers dragged with the mouse onto docks, buildings or tiles. Here every stop is read in the route panel
    /// (F6) with its place and cargo, and the Move soldiers key (M) on a stop moves it to the map cursor's tile.
    /// </summary>
    internal static class Routes
    {
        private static ShipLogisticsUI Panel => GameUI.inst != null ? GameUI.inst.shipLogisticsUI : null;

        /// <summary>The ship or cart whose route panel is open, or null.</summary>
        internal static ILogisticTransport Transport
        {
            get
            {
                var p = Panel;
                return p != null && p.gameObject.activeInHierarchy ? p.transport : null;
            }
        }

        private static string Kind(ILogisticTransport t) => t is TransportCart ? "cart" : "ship";

        internal static string ModeIntro() => KeyHelp.Resolve(
            "Route editing. Move the cursor to a dock, a building or a tile, press F6 for the route panel, choose a stop and press {MoveSoldiers} to move it to the cursor. Enter on the map says whether a stop can go there. Escape returns to normal mode.");

        /// <summary>
        /// Where a stop of <paramref name="t"/> could go on <paramref name="cell"/>: a building (like the game, the nearest valid
        /// one close to the cursor) or the tile itself. False with the reason when neither works.
        /// </summary>
        internal static bool TryTarget(ILogisticTransport t, Cell cell, out Building building, out string why)
        {
            building = null;
            why = null;
            var mode = Panel != null ? Panel.cursorMode : null;
            if (t == null || mode == null)
            {
                why = "no route panel is open";
                return false;
            }
            if (cell == null)
            {
                why = "outside the map";
                return false;
            }
            Building best = null;
            float bestD = float.MaxValue;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var c = World.inst.GetCellData(cell.x + dx, cell.z + dz);
                    var b = c != null ? c.TopStructure : null;
                    if (b == null || !mode.ValidBuilding(b)) continue;
                    // The building under the cursor always wins over a neighbour.
                    float d = dx == 0 && dz == 0 ? -1f : Mathff.DistSqrdXZ(b.Center(), cell.Center);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = b;
                    }
                }
            if (best != null)
            {
                if (t.ValidForDocking(best))
                {
                    building = best;
                    return true;
                }
                why = best.FriendlyName + " cannot be used by this " + Kind(t) + ", for example the docks of that kingdom are closed to you";
                return false;
            }
            if (mode.ValidCell(cell) && t.ValidForOrder(cell)) return true;
            why = t is TransportCart
                ? "carts stop at stockpiles, granaries, markets and other storage or production buildings, or on roads and open ground of their own island"
                : "ships stop at docks or on water they can reach";
            return false;
        }

        /// <summary>Enter on the map in route mode: can a stop go on this tile, and what would it be?</summary>
        internal static void SpeakTileForStop(Cell cell)
        {
            var t = Transport;
            if (t == null)
            {
                A.Cue(Cue.Error);
                A.Say("No ship or cart is selected. Shift Enter on your ship or cart opens its route.");
                return;
            }
            if (TryTarget(t, cell, out var b, out var why))
            {
                A.Cue(Cue.PlaceValid);
                A.Say(KeyHelp.Resolve("A stop can go here: " + (b != null ? PlaceName(b) : "this tile, " + CellInfo.Brief(cell, false)) + ". F6, choose a stop, {MoveSoldiers} moves it here."), force: true);
            }
            else
            {
                A.Cue(Cue.PlaceInvalid);
                A.Say("No stop can go here: " + why + ". " + CellInfo.Brief(cell, false), force: true);
            }
        }

        private static string PlaceName(Building b)
        {
            if (b == null) return null;
            string custom = TextUtil.Clean(b.customName ?? string.Empty);
            return custom.Length > 0 && custom != b.FriendlyName ? custom + ", " + b.FriendlyName : b.FriendlyName;
        }

        private static string Cargo(ResourceAmount amount)
        {
            int n = (int)FreeResourceType.NumTypes;
            var names = new string[n];
            var amounts = new int[n];
            for (int i = 0; i < n; i++)
            {
                names[i] = ((FreeResourceType)i).ToString();
                amounts[i] = amount.Get((FreeResourceType)i);
            }
            return UnitText.Cargo(names, amounts);
        }

        /// <summary>"Stop 2 of 3: Northport, Dock, 4 tiles east …, pick up wood 20, drop off nothing".</summary>
        internal static string StopText(LogisticsDestUI row)
        {
            var t = Transport;
            var order = row != null ? row.logisticOrder : null;
            if (t == null || order == null) return null;
            var orders = t.GetOrders();
            int index = orders.IndexOf(order) + 1;
            bool waypoint = order.endType == Ship.LogisticsOrderType.Position;
            string place;
            Vector3 end;
            try
            {
                end = order.GetEnd();
            }
            catch (System.Exception)
            {
                end = order.endPosition; // the building of the stop is gone
            }
            var cell = World.inst.GetCellData(end);
            if (waypoint) place = "waypoint" + (cell != null ? " on " + CellInfo.Terrain(cell) : string.Empty);
            else place = PlaceName(order.GetBuilding());
            string where = Directions.Relative(MapController.Inst.CursorPos, new GridPos(Mathf.FloorToInt(end.x), Mathf.FloorToInt(end.z)));
            bool sells = false;
            var ship = (t as MonoBehaviour) != null ? (t as MonoBehaviour).GetComponent<ShipBase>() : null;
            try
            {
                sells = !waypoint && ship != null && Ship.IsSellOrder(order, ship);
            }
            catch (System.Exception)
            {
                // merchant details are optional
            }
            return UnitText.RouteStop(index, orders.Count, place, where, waypoint, sells, Cargo(order.loadFromStart), Cargo(order.unloadAtEnd));
        }

        private static string Checked(Toggle t) => t.isOn ? "checked" : "not checked";

        /// <summary>Spoken description of a control in a route stop row, or null for the default.</summary>
        internal static string Describe(LogisticsDestUI row, Selectable control)
        {
            if (row == null || control == null || Transport == null) return null;
            int index = Transport.GetOrders().IndexOf(row.logisticOrder) + 1;
            if (control == row.HeaderDockTrack)
                return KeyHelp.Resolve(StopText(row) + ". Button, Enter shows it, {MoveSoldiers} moves this stop to the map cursor");
            if (control == row.PickUpToggle)
                return "Stop " + index + ", pick up, check box, " + Checked(row.PickUpToggle)
                       + (row.PickUpToggle.isOn && !row.PickUpConfirm.activeSelf ? ", " + (Cargo(row.logisticOrder.loadFromStart) ?? "nothing") + ", Shift Enter changes the amounts" : string.Empty);
            if (control == row.DropOffToggle)
                return "Stop " + index + ", drop off, check box, " + Checked(row.DropOffToggle)
                       + (row.DropOffToggle.isOn && !row.DropOffConfirm.activeSelf ? ", " + (Cargo(row.logisticOrder.unloadAtEnd) ?? "nothing") + ", Shift Enter changes the amounts" : string.Empty);
            if (control == row.DeleteBtn) return "Remove stop " + index + ", button";
            return null;
        }

        /// <summary>Keys on a route stop row. True when handled.</summary>
        internal static bool HandleKey(LogisticsDestUI row, UIItem item)
        {
            if (row == null || Transport == null) return false;
            if (KInput.Pressed("MoveSoldiers"))
            {
                MoveStop(row);
                return true;
            }
            if (KInput.WithShift(KeyCode.Return))
            {
                // The game's edit buttons only appear while the mouse hovers over the confirmed amounts.
                Button edit = item.Control == row.PickUpToggle ? row.PickUpEditBtn : item.Control == row.DropOffToggle ? row.DropOffEditBtn : null;
                Toggle toggle = item.Control as Toggle;
                if (edit == null || toggle == null || !toggle.isOn) return false;
                edit.onClick.Invoke();
                A.Cue(Cue.Open);
                A.Say("Amounts open for changes: move down to the resource fields, type the amounts, then choose the confirm button", force: true);
                return true;
            }
            return false;
        }

        private static void MoveStop(LogisticsDestUI row)
        {
            var t = Transport;
            var order = row.logisticOrder;
            var cell = MapController.Inst.CurrentCell;
            if (order == null) return;
            if (!TryTarget(t, cell, out var b, out var why))
            {
                A.Cue(Cue.Error);
                A.Say("Cannot move the stop to the cursor: " + why, force: true);
                return;
            }
            if (b != null) order.SetEnd(b);
            else order.SetEnd(cell.Center);
            Panel.PopulateUI(); // redraws the route, re-reads every stop and checks the orders like a mouse drag does
            A.Cue(Cue.Placed);
            A.Say(StopText(row) ?? "Stop moved", force: true);
        }

        internal static string Help(LogisticsDestUI row) => row == null ? null : KeyHelp.Resolve(
            "On a route stop: {MoveSoldiers} moves the stop to the dock, building or tile under the map cursor. On the pick up and drop off check boxes Shift Enter changes the amounts. The add buttons at the start and end of the list add stops.");
    }
}
