using System;
using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>What the current menu, dialog or panel actually contains, so F1 only names keys that do something.</summary>
    public sealed class ScreenFeatures
    {
        public int Items;
        public bool Controls;
        public bool Texts;
        public bool Adjustables;   // sliders, option lists, radio groups: Left / Right
        public bool Sliders;       // Page Up / Page Down
        public bool TextFields;
        public bool TypeAhead;
        public bool InPanel;       // F6 / Shift F6, Escape back to the map
        public bool SaveSlots;     // Delete
        public bool Playing;       // a dialog during play: Escape closes it
        public bool CanGoBack = true; // false on the main menu, where Escape does nothing
    }

    /// <summary>State of the map, so the map help names only the keys that apply right now.</summary>
    public sealed class MapState
    {
        public bool MenuMap;          // exploring from the map setup screen
        public bool HasKeep = true;
        public bool Placing;
        public bool LinePlacement;    // roads, walls, fields
        public bool CursorMode;       // chop / demolish / rebuild ...
        public bool Brush;            // creative mode brush
        public bool HasSelection;
        public bool BuildingSelected;
        public bool SoldiersSelected;
        public bool HasTarget;
        public bool Walking;
    }

    public sealed class KeyLine
    {
        public readonly string Keys;
        public readonly string Action;

        public KeyLine(string keys, string action)
        {
            Keys = keys;
            Action = action;
        }

        public override string ToString() => KeyHelp.Resolve(Keys) + ": " + Action;
    }

    public sealed class KeySection
    {
        public readonly string Title;
        public readonly List<KeyLine> Lines = new List<KeyLine>();

        public KeySection(string title, params KeyLine[] lines)
        {
            Title = title;
            Lines.AddRange(lines);
        }
    }

    /// <summary>The complete key reference (Shift+F1 list) and the context help builders.</summary>
    public static class KeyHelp
    {
        private static KeyLine K(string keys, string action) => new KeyLine(keys, action);

        /// <summary>Spoken key for a binding id; the plugin points this at the player's current bindings.</summary>
        public static Func<string, string> KeyNameOf = id =>
        {
            var d = Bindings.Def(id);
            return d != null ? d.Default.Spoken() : id;
        };

        /// <summary>Replaces {BindingId} with the key currently bound to it.</summary>
        public static string Resolve(string text) =>
            text == null ? null : System.Text.RegularExpressions.Regex.Replace(text, @"\{(\w+)\}", m => KeyNameOf(m.Groups[1].Value));

        public static readonly List<KeySection> Sections = new List<KeySection>
        {
            new KeySection("Everywhere",
                K("F1", "help for the current screen, only the keys that work there"),
                K("{KeyList}", "this key list, with sound cue previews at the end"),
                K("{Redetect}", "search again for your screen reader"),
                K("{Mute}", "sound cues on or off"),
                K("{Settings}", "mod settings: options, sound volume and changing these keys"),
                K("{Reset}", "reset the mod if speech stops responding"),
                K("{Report}", "keyboard report for bug reports")),
            new KeySection("Menus, dialogs and panels",
                K("Up and Down arrows", "move through everything on the screen"),
                K("Tab and Shift Tab", "move between controls only"),
                K("Home and End", "first and last item"),
                K("Enter or Space", "activate a button, toggle a check box, start typing in a text field"),
                K("Left and Right arrows", "change a slider, option list or radio button group"),
                K("Page Up and Page Down", "change a slider in big steps"),
                K("Letters", "jump to an item starting with that letter, on menu screens"),
                K("Control R", "read the whole screen"),
                K("F5", "repeat the current item"),
                K("Delete", "on a saved game: delete it"),
                K("F6 and Shift F6", "inside the game: next or previous panel"),
                K("Escape", "back, close or cancel")),
            new KeySection("Typing in a text field",
                K("Letters and digits", "type, each character is spoken"),
                K("Backspace", "delete, says what was deleted"),
                K("F2, Up or Down", "read the field"),
                K("Enter", "confirm"),
                K("Escape", "cancel")),
            new KeySection("Map: moving and looking",
                K("Arrow keys", "move the cursor one tile, north is up"),
                K("Shift arrows", "move 5 tiles"),
                K("Control arrows", "jump to where the terrain changes"),
                K("{TileInfo}", "everything about the tile"),
                K("F5", "repeat the tile"),
                K("{Survey}", "survey the area around the cursor"),
                K("{Coordinates}", "cursor coordinates and map size"),
                K("{Keep}", "jump to your keep"),
                K("{SelectedBuilding}", "jump to the selected building"),
                K("{Date}", "year, season, weather and speed"),
                K("{Status}", "kingdom status"),
                K("{Log}", "notification history"),
                K("{LastNotification}", "repeat the last notification")),
            new KeySection("Map: finding and going places",
                K("{PrevCategory} and {NextCategory}", "choose a scan category"),
                K("{PrevItem} and {NextItem}", "choose the previous or next thing of that category as your target"),
                K("{JumpTarget}", "jump to the target"),
                K("{WhereTarget}", "where the target is"),
                K("{Walk}", "walk to the target along a walkable route"),
                K("{WalkStraight}", "walk to the target in a straight line"),
                K("{Beacon}", "target beacon on or off"),
                K("Any arrow or Escape", "stop walking"),
                K("Control Shift 1 to 9", "store a bookmark"),
                K("Control 1 to 9", "jump to a bookmark"),
                K("Alt 1 to 9", "make a bookmark the target")),
            new KeySection("Map: acting",
                K("Enter", "select what is under the cursor"),
                K("Shift Enter", "select soldiers, a ship or a villager on the tile"),
                K("F6", "move into the open panels"),
                K("{BuildMenu}", "build menu"),
                K("Delete", "demolish the selected building"),
                K("C", "chop trees on the selected tile, or cancel it"),
                K("{ChopMode}", "chop trees mode on or off"),
                K("{MoveSoldiers}", "send selected soldiers to the cursor"),
                K("{Alert}", "respond to the nearest alert: advisor news, a waiting envoy, a stopped cart"),
                K("J", "job priority"),
                K("Space", "pause"),
                K("1, 2 and 3", "game speed"),
                K("Escape", "pause menu")),
            new KeySection("Placing a building",
                K("Arrow keys", "move the building, you hear whether the spot is valid"),
                K("Enter", "build here"),
                K("Shift Enter", "for roads, walls and fields: mark the start, Enter at the end builds the line or area"),
                K("R", "rotate"),
                K("{Validity}", "why the spot is valid or not"),
                K("Escape", "stop placing")),
            new KeySection("Build menu",
                K("Left and Right arrows", "category"),
                K("Up and Down arrows", "building"),
                K("I or F5", "full description"),
                K("Letters", "jump to a building"),
                K("Enter", "pick the building up"),
                K("Escape or B", "close")),
        };

        static KeyHelp()
        {
            var pad = new KeySection("Controller");
            pad.Lines.AddRange(ControllerMap.Lines);
            Sections.Add(pad);
        }

        /// <summary>The whole reference as one text (used for tests and the README check).</summary>
        public static string AllText()
        {
            var parts = new List<string>();
            foreach (var s in Sections)
            {
                parts.Add(s.Title);
                foreach (var l in s.Lines) parts.Add(l.ToString());
            }
            return string.Join(". ", parts) + ".";
        }

        /// <summary>Keys for a menu, dialog or panel, naming only what the screen supports.</summary>
        public static string NavKeys(ScreenFeatures f)
        {
            var p = new List<string>();
            if (f.Items > 1) p.Add(f.Texts && f.Controls ? "Up and Down arrows move through everything on the screen, Tab and Shift Tab through the controls only" : "Up and Down arrows move between the items");
            if (f.Items > 3) p.Add("Home and End jump to the first and last item");
            if (f.Controls) p.Add("Enter or Space activates");
            if (f.TextFields) p.Add("on a text field Enter starts typing");
            if (f.Adjustables) p.Add("Left and Right arrows change sliders, option lists and radio buttons");
            if (f.Sliders) p.Add("Page Up and Page Down change a slider in big steps");
            if (f.TypeAhead && f.Items > 3) p.Add("letters jump to an item starting with that letter");
            if (f.SaveSlots) p.Add("Delete deletes the saved game under the cursor");
            if (f.Items > 1) p.Add("Control R reads the whole screen");
            p.Add("F5 repeats the current item");
            if (f.InPanel) p.Add("F6 and Shift F6 move to the next or previous panel, Escape returns to the map");
            else if (f.CanGoBack) p.Add("Escape goes back");
            return string.Join(". ", p.ConvertAll(Capitalize)) + ".";
        }

        /// <summary>Map help for the current state: what you are doing now first, then the keys that apply.</summary>
        public static string MapHelp(MapState s)
        {
            var p = new List<string>();
            if (s.Walking)
                return Resolve("Walking to your target. Any arrow key or Escape stops. {Beacon} turns the beacon on or off.");
            if (s.Placing)
            {
                p.Add("Placing a building. Arrow keys move it and you hear whether the spot is valid. Enter builds here, R rotates, {Validity} says why the spot is valid or not");
                if (s.LinePlacement) p.Add("Shift Enter marks the start of a line or area, move to the end, then Enter builds all of it");
                p.Add("Escape stops placing");
                return Resolve(string.Join(". ", p) + ".");
            }
            if (s.Brush)
                return "A creative mode brush is selected. Move with the arrow keys, Enter applies it at the cursor, Escape turns the brush off.";
            if (s.CursorMode)
                return "A tool mode is on, such as chop trees or demolish. Enter applies it to the tile under the cursor. Shift Enter marks one corner of an area, move, then Enter applies it to the whole area. Escape returns to normal mode.";

            p.Add(s.MenuMap ? "Exploring the generated map before the game starts" : "Kingdom map");
            p.Add("Arrow keys move one tile, Shift arrows 5 tiles, Control arrows jump to where the terrain changes. {TileInfo} describes the tile, {Survey} surveys the area, {Coordinates} gives the coordinates");
            p.Add("{PrevCategory} and {NextCategory} choose a scan category, {PrevItem} and {NextItem} choose the previous or next thing in it as your target");
            if (s.HasTarget) p.Add("{JumpTarget} jumps to the target, {WhereTarget} says where it is, {Walk} walks there, {WalkStraight} walks in a straight line, {Beacon} turns the beacon on or off");
            if (s.MenuMap)
            {
                p.Add("Control M or Escape returns to the map setup menu");
                return Resolve(string.Join(". ", p) + ".");
            }
            if (!s.HasKeep)
                p.Add("You have no keep yet: press {BuildMenu}, choose the Keep in the Castle category, move to a good spot and press Enter");
            else
                p.Add("Enter selects what is under the cursor, Shift Enter selects soldiers, a ship or a villager. {BuildMenu} opens the build menu, {ChopMode} turns chop trees mode on");
            if (s.HasSelection)
            {
                p.Add("F6 reads the selected " + (s.BuildingSelected ? "building's" : "tile's") + " panel" + (s.BuildingSelected ? ", Delete demolishes it, {SelectedBuilding} jumps back to it" : ", C orders its trees chopped"));
            }
            if (s.SoldiersSelected) p.Add("{MoveSoldiers} sends the selected soldiers to the cursor");
            if (s.HasKeep) p.Add("{Keep} jumps to your keep. {Status} kingdom status, {Date} date and weather, {Log} notifications, J job priority. Control Shift 1 to 9 stores a bookmark, Control 1 to 9 jumps to it");
            p.Add("Space pauses, 1, 2 and 3 set the speed, Escape opens the pause menu");
            return Resolve(string.Join(". ", p) + ".");
        }

        private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
