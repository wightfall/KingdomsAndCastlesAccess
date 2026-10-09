using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>Gamepad buttons, named after the Xbox layout (PlayStation: A cross, B circle, X square, Y triangle).</summary>
    public enum PadButton
    {
        A, B, X, Y, LB, RB, Back, Start, L3, R3, Up, Down, Left, Right
    }

    /// <summary>What a gamepad press does: a mod action (binding id, so rebinding keys follows) or a fixed key chord.</summary>
    public struct PadAction
    {
        public string Binding;   // e.g. "Walk"; null when Key is used
        public string Key;       // chord text, e.g. "Shift+Return"

        public static PadAction Bind(string id) => new PadAction { Binding = id };
        public static PadAction Chord(string chord) => new PadAction { Key = chord };

        public override string ToString() => Binding != null ? "{" + Binding + "}" : Key;
    }

    /// <summary>
    /// Controller layout for blind players. Every press becomes the keyboard key that does the same thing, so the
    /// whole mod (and the game's own keys) works with a gamepad. Holding a trigger switches layer:
    /// no trigger = moving and choosing, LT = finding and going places, RT = information and menus,
    /// both triggers = game speed and actions.
    /// </summary>
    public static class ControllerMap
    {
        /// <summary>The action for a press, or null when the button does nothing in that layer.</summary>
        public static PadAction? Map(PadButton b, bool lt, bool rt, bool onMap)
        {
            if (lt && rt)
            {
                switch (b)
                {
                    case PadButton.Left: return PadAction.Chord("Alpha1");      // normal speed
                    case PadButton.Up: return PadAction.Chord("Alpha2");        // fast
                    case PadButton.Right: return PadAction.Chord("Alpha3");     // fastest
                    case PadButton.Down: return PadAction.Chord("Space");       // pause
                    case PadButton.A: return PadAction.Bind("ChopMode");
                    case PadButton.B: return PadAction.Bind("MoveSoldiers");
                    case PadButton.X: return PadAction.Chord("Delete");         // demolish the selected building
                    case PadButton.Y: return PadAction.Bind("Validity");
                    case PadButton.Start: return PadAction.Bind("Settings");
                    case PadButton.Back: return PadAction.Bind("Reset");
                    default: return null;
                }
            }
            if (lt)
            {
                switch (b)
                {
                    case PadButton.Up: return PadAction.Chord("Ctrl+UpArrow");
                    case PadButton.Down: return PadAction.Chord("Ctrl+DownArrow");
                    case PadButton.Left: return PadAction.Chord("Ctrl+LeftArrow");
                    case PadButton.Right: return PadAction.Chord("Ctrl+RightArrow");
                    case PadButton.LB: return PadAction.Bind("PrevItem");
                    case PadButton.RB: return PadAction.Bind("NextItem");
                    case PadButton.A: return PadAction.Bind("Walk");
                    case PadButton.B: return PadAction.Bind("JumpTarget");
                    case PadButton.X: return PadAction.Bind("Beacon");
                    case PadButton.Y: return PadAction.Bind("WhereTarget");
                    case PadButton.Start: return PadAction.Chord("Space");      // pause or resume
                    case PadButton.Back: return PadAction.Chord("Shift+F6");
                    case PadButton.L3: return PadAction.Bind("WalkStraight");
                    default: return null;
                }
            }
            if (rt)
            {
                switch (b)
                {
                    case PadButton.Up: return PadAction.Chord("Shift+UpArrow");
                    case PadButton.Down: return PadAction.Chord("Shift+DownArrow");
                    case PadButton.Left: return PadAction.Chord("Shift+LeftArrow");
                    case PadButton.Right: return PadAction.Chord("Shift+RightArrow");
                    case PadButton.A: return PadAction.Bind("BuildMenu");
                    case PadButton.B: return PadAction.Bind("Status");
                    case PadButton.X: return PadAction.Bind("Log");
                    case PadButton.Y: return PadAction.Bind("Survey");
                    case PadButton.LB: return onMap ? PadAction.Bind("Keep") : PadAction.Chord("Home");
                    case PadButton.RB: return onMap ? PadAction.Bind("SelectedBuilding") : PadAction.Chord("End");
                    case PadButton.Start: return PadAction.Bind("KeyList");
                    case PadButton.Back: return PadAction.Bind("Alert");
                    case PadButton.L3: return PadAction.Bind("Coordinates");
                    case PadButton.R3: return PadAction.Bind("LastNotification");
                    default: return null;
                }
            }
            switch (b)
            {
                case PadButton.Up: return PadAction.Chord("UpArrow");
                case PadButton.Down: return PadAction.Chord("DownArrow");
                case PadButton.Left: return PadAction.Chord("LeftArrow");
                case PadButton.Right: return PadAction.Chord("RightArrow");
                case PadButton.A: return PadAction.Chord("Return");
                case PadButton.B: return PadAction.Chord("Escape");
                case PadButton.X: return PadAction.Chord("Shift+Return");
                case PadButton.Y: return onMap ? PadAction.Bind("TileInfo") : PadAction.Chord("F5");
                case PadButton.LB: return onMap ? PadAction.Bind("PrevCategory") : PadAction.Chord("PageUp");
                case PadButton.RB: return onMap ? PadAction.Bind("NextCategory") : PadAction.Chord("PageDown");
                case PadButton.Back: return PadAction.Chord("F6");
                case PadButton.Start: return PadAction.Chord("F1");
                case PadButton.L3: return onMap ? PadAction.Bind("Keep") : PadAction.Chord("Home");
                case PadButton.R3: return onMap ? PadAction.Bind("Date") : PadAction.Chord("Ctrl+R");
                default: return null;
            }
        }

        /// <summary>The layout as key-list lines (Shift+F1 and README).</summary>
        public static readonly List<KeyLine> Lines = new List<KeyLine>
        {
            new KeyLine(Loc.N("D-pad or left stick"), Loc.N("arrow keys: move the cursor, move through menus")),
            new KeyLine("A", Loc.N("Enter: select, activate, build")),
            new KeyLine("B", Loc.N("Escape: back, cancel, pause menu")),
            new KeyLine("X", Loc.N("Shift Enter: select soldiers, ships or villagers, mark the start of a line or area")),
            new KeyLine("Y", Loc.N("on the map describe the tile, in menus repeat the item")),
            new KeyLine(Loc.N("LB and RB"), Loc.N("on the map previous and next scan category, in lists Page Up and Page Down")),
            new KeyLine(Loc.N("Back or View"), Loc.N("F6: move into the panels")),
            new KeyLine(Loc.N("Start or Menu"), Loc.N("F1: help for this screen")),
            new KeyLine(Loc.N("Left stick click"), Loc.N("jump to your keep, in menus the first item")),
            new KeyLine(Loc.N("Right stick click"), Loc.N("date and weather, in menus read the whole screen")),
            new KeyLine(Loc.N("Hold LT with D-pad"), Loc.N("jump to where the terrain changes")),
            new KeyLine(Loc.N("Hold LT with LB and RB"), Loc.N("previous and next target")),
            new KeyLine(Loc.N("Hold LT with A, B, X, Y"), Loc.N("walk to the target, jump to it, beacon on or off, where is it")),
            new KeyLine(Loc.N("Hold LT with Start"), Loc.N("pause or resume")),
            new KeyLine(Loc.N("Hold LT with left stick click"), Loc.N("walk to the target in a straight line")),
            new KeyLine(Loc.N("Hold RT with D-pad"), Loc.N("move 5 tiles")),
            new KeyLine(Loc.N("Hold RT with A, B, X, Y"), Loc.N("build menu, kingdom status, notifications, area survey")),
            new KeyLine(Loc.N("Hold RT with LB and RB"), Loc.N("jump to your keep or the selected building, in menus first and last item")),
            new KeyLine(Loc.N("Hold RT with Start"), Loc.N("key list")),
            new KeyLine(Loc.N("Hold RT with Back"), Loc.N("respond to the nearest alert")),
            new KeyLine(Loc.N("Hold RT with stick clicks"), Loc.N("coordinates, repeat the last notification")),
            new KeyLine(Loc.N("Hold both triggers with D-pad"), Loc.N("left normal speed, up fast, right fastest, down pause")),
            new KeyLine(Loc.N("Hold both triggers with A, B, X, Y"), Loc.N("chop trees mode, send soldiers, demolish the selected building, why the placing spot is valid or not")),
            new KeyLine(Loc.N("Hold both triggers with Start"), Loc.N("mod settings")),
            new KeyLine(Loc.N("Hold both triggers with Back"), Loc.N("reset the mod")),
        };
    }
}
