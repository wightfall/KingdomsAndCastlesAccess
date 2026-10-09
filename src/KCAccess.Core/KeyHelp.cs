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
        public bool CastleBlock;      // placing a castle block (stackable)
        public bool CursorMode;       // chop / demolish / rebuild ...
        public bool RouteMode;        // editing a ship or cart route (its panel is open)
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

        public override string ToString() => KeyHelp.Resolve(Loc.T(Keys)) + ": " + KeyHelp.Resolve(Loc.T(Action));
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
            new KeySection(Loc.N("Everywhere"),
                K("F1", Loc.N("help for the current screen, only the keys that work there")),
                K("{KeyList}", Loc.N("this key list, with sound cue previews at the end")),
                K("{Redetect}", Loc.N("search again for your screen reader")),
                K("{Mute}", Loc.N("sound cues on or off")),
                K("{Settings}", Loc.N("mod settings: options, sound volume and changing these keys")),
                K("{Reset}", Loc.N("reset the mod if speech stops responding")),
                K("{Report}", Loc.N("keyboard report for bug reports"))),
            new KeySection(Loc.N("Menus, dialogs and panels"),
                K(Loc.N("Up and Down arrows"), Loc.N("move through everything on the screen")),
                K(Loc.N("Tab and Shift Tab"), Loc.N("move between controls only")),
                K(Loc.N("Home and End"), Loc.N("first and last item")),
                K(Loc.N("Enter or Space"), Loc.N("activate a button, toggle a check box, start typing in a text field")),
                K(Loc.N("Left and Right arrows"), Loc.N("change a slider, option list or radio button group")),
                K(Loc.N("Page Up and Page Down"), Loc.N("change a slider in big steps")),
                K(Loc.N("Letters"), Loc.N("jump to an item starting with that letter, on menu screens")),
                K(Loc.N("Control R"), Loc.N("read the whole screen")),
                K("F5", Loc.N("repeat the current item")),
                K(Loc.N("Delete"), Loc.N("on a saved game: delete it")),
                K(Loc.N("F6 and Shift F6"), Loc.N("inside the game: next or previous panel")),
                K(Loc.N("Escape"), Loc.N("back, close or cancel"))),
            new KeySection(Loc.N("Typing in a text field"),
                K(Loc.N("Letters and digits"), Loc.N("type, each character is spoken")),
                K(Loc.N("Backspace"), Loc.N("delete, says what was deleted")),
                K(Loc.N("F2, Up or Down"), Loc.N("read the field")),
                K(Loc.N("Enter"), Loc.N("confirm")),
                K(Loc.N("Escape"), Loc.N("cancel"))),
            new KeySection(Loc.N("Map: moving and looking"),
                K(Loc.N("Arrow keys"), Loc.N("move the cursor one tile, north is up")),
                K(Loc.N("Shift arrows"), Loc.N("move 5 tiles")),
                K(Loc.N("Control arrows"), Loc.N("jump to where the terrain changes")),
                K("{TileInfo}", Loc.N("everything about the tile")),
                K("F5", Loc.N("repeat the tile")),
                K("{Survey}", Loc.N("survey the area around the cursor")),
                K("{Coordinates}", Loc.N("cursor coordinates and map size")),
                K("{Keep}", Loc.N("jump to your keep")),
                K("{SelectedBuilding}", Loc.N("jump to the selected building")),
                K("{Date}", Loc.N("year, season, weather and speed")),
                K("{Status}", Loc.N("kingdom status")),
                K("{Log}", Loc.N("notification history")),
                K("{LastNotification}", Loc.N("repeat the last notification"))),
            new KeySection(Loc.N("Map: finding and going places"),
                K(Loc.N("{PrevCategory} and {NextCategory}"), Loc.N("choose a scan category")),
                K(Loc.N("{PrevItem} and {NextItem}"), Loc.N("choose the previous or next thing of that category as your target")),
                K("{JumpTarget}", Loc.N("jump to the target")),
                K("{WhereTarget}", Loc.N("where the target is")),
                K("{Walk}", Loc.N("walk to the target along a walkable route")),
                K("{WalkStraight}", Loc.N("walk to the target in a straight line")),
                K("{Beacon}", Loc.N("target beacon on or off")),
                K(Loc.N("Any arrow or Escape"), Loc.N("stop walking")),
                K(Loc.N("Control Shift 1 to 9"), Loc.N("store a bookmark")),
                K(Loc.N("Control 1 to 9"), Loc.N("jump to a bookmark")),
                K(Loc.N("Alt 1 to 9"), Loc.N("make a bookmark the target"))),
            new KeySection(Loc.N("Map: acting"),
                K(Loc.N("Enter"), Loc.N("select what is under the cursor")),
                K(Loc.N("Shift Enter"), Loc.N("select soldiers, a siege catapult, a dragon, a ship, a cart or a villager on the tile")),
                K(Loc.N("Control Shift Enter"), Loc.N("add the soldiers on the tile to the selection")),
                K("F6", Loc.N("move into the open panels")),
                K("{BuildMenu}", Loc.N("build menu")),
                K(Loc.N("Delete"), Loc.N("demolish the selected building")),
                K("C", Loc.N("chop trees on the selected tile, or cancel it")),
                K("{ChopMode}", Loc.N("chop trees mode on or off")),
                K("{MoveSoldiers}", Loc.N("send selected soldiers to the cursor")),
                K("{Alert}", Loc.N("respond to the nearest alert: advisor news, a waiting envoy, a stopped cart")),
                K("J", Loc.N("job priority")),
                K(Loc.N("Space"), Loc.N("pause")),
                K(Loc.N("1, 2 and 3"), Loc.N("game speed")),
                K(Loc.N("Escape"), Loc.N("pause menu"))),
            new KeySection(Loc.N("Placing a building"),
                K(Loc.N("Arrow keys"), Loc.N("move the building, you hear whether the spot is valid")),
                K(Loc.N("Enter"), Loc.N("build here")),
                K(Loc.N("Shift Enter"), Loc.N("for roads, walls and fields: mark the start, Enter at the end builds the line or area")),
                K("R", Loc.N("rotate")),
                K("{Validity}", Loc.N("why the spot is valid or not")),
                K(Loc.N("{StackMore} and {StackLess}"), Loc.N("castle blocks: build more or fewer levels on top of each other with each Enter or line")),
                K(Loc.N("Escape"), Loc.N("stop placing"))),
            new KeySection(Loc.N("Build menu"),
                K(Loc.N("Left and Right arrows"), Loc.N("category")),
                K(Loc.N("Up and Down arrows"), Loc.N("building")),
                K(Loc.N("I or F5"), Loc.N("full description")),
                K(Loc.N("Letters"), Loc.N("jump to a building")),
                K(Loc.N("Enter"), Loc.N("pick the building up")),
                K(Loc.N("Escape or {BuildMenu}"), Loc.N("close"))),
            new KeySection(Loc.N("Ship and cart routes"),
                K(Loc.N("Shift Enter on your ship or cart"), Loc.N("select it and open its route panel")),
                K("F6", Loc.N("move into the route panel, each stop is read with its place and cargo")),
                K(Loc.N("{MoveSoldiers} on a stop"), Loc.N("move that stop to the dock, building or tile under the map cursor")),
                K(Loc.N("Enter on the map"), Loc.N("say whether a stop can go on the tile under the cursor"))),
        };

        static KeyHelp()
        {
            var pad = new KeySection(Loc.N("Controller"));
            pad.Lines.AddRange(ControllerMap.Lines);
            Sections.Add(pad);
        }

        /// <summary>The whole reference as one text (used for tests and the README check).</summary>
        public static string AllText()
        {
            var parts = new List<string>();
            foreach (var s in Sections)
            {
                parts.Add(Loc.T(s.Title));
                foreach (var l in s.Lines) parts.Add(l.ToString());
            }
            return string.Join(". ", parts) + ".";
        }

        /// <summary>Keys for a menu, dialog or panel, naming only what the screen supports.</summary>
        public static string NavKeys(ScreenFeatures f)
        {
            var p = new List<string>();
            if (f.Items > 1) p.Add(f.Texts && f.Controls ? Loc.T("Up and Down arrows move through everything on the screen, Tab and Shift Tab through the controls only") : Loc.T("Up and Down arrows move between the items"));
            if (f.Items > 3) p.Add(Loc.T("Home and End jump to the first and last item"));
            if (f.Controls) p.Add(Loc.T("Enter or Space activates"));
            if (f.TextFields) p.Add(Loc.T("on a text field Enter starts typing"));
            if (f.Adjustables) p.Add(Loc.T("Left and Right arrows change sliders, option lists and radio buttons"));
            if (f.Sliders) p.Add(Loc.T("Page Up and Page Down change a slider in big steps"));
            if (f.TypeAhead && f.Items > 3) p.Add(Loc.T("letters jump to an item starting with that letter"));
            if (f.SaveSlots) p.Add(Loc.T("Delete deletes the saved game under the cursor"));
            if (f.Items > 1) p.Add(Loc.T("Control R reads the whole screen"));
            p.Add(Loc.T("F5 repeats the current item"));
            if (f.InPanel) p.Add(Loc.T("F6 and Shift F6 move to the next or previous panel, Escape returns to the map"));
            else if (f.CanGoBack) p.Add(Loc.T("Escape goes back"));
            return string.Join(". ", p.ConvertAll(Capitalize)) + ".";
        }

        /// <summary>Map help for the current state: what you are doing now first, then the keys that apply.</summary>
        public static string MapHelp(MapState s)
        {
            var p = new List<string>();
            if (s.Walking)
                return Resolve(Loc.T("Walking to your target. Any arrow key or Escape stops. {Beacon} turns the beacon on or off."));
            if (s.Placing)
            {
                p.Add(Loc.T("Placing a building. Arrow keys move it and you hear whether the spot is valid. Enter builds here, R rotates, {Validity} says why the spot is valid or not"));
                if (s.LinePlacement) p.Add(Loc.T("Shift Enter marks the start of a line or area, move to the end, then Enter builds all of it"));
                if (s.CastleBlock) p.Add(Loc.T("Castle blocks stack: on a tile with blocks the new one goes on top, and you hear its level. {StackMore} and {StackLess} set how many levels each Enter or line builds"));
                p.Add(Loc.T("Escape stops placing"));
                return Resolve(string.Join(". ", p) + ".");
            }
            if (s.Brush)
                return Loc.T("A creative mode brush is selected. Move with the arrow keys, Enter applies it at the cursor, Escape turns the brush off.");
            if (s.RouteMode)
                return Resolve(Loc.T("Editing a ship or cart route. Move the cursor to a dock, a building or a tile, then press F6, choose a stop in the route panel and press {MoveSoldiers} to move that stop to the cursor. Enter on the map says whether a stop can go there. The panel also adds, removes, pauses and names the route. Escape returns to normal mode."));
            if (s.CursorMode)
                return Loc.T("A tool mode is on, such as chop trees or demolish. Enter applies it to the tile under the cursor. Shift Enter marks one corner of an area, move, then Enter applies it to the whole area. Escape returns to normal mode.");

            p.Add(s.MenuMap ? Loc.T("Exploring the generated map before the game starts") : Loc.T("Kingdom map"));
            p.Add(Loc.T("Arrow keys move one tile, Shift arrows 5 tiles, Control arrows jump to where the terrain changes. {TileInfo} describes the tile, {Survey} surveys the area, {Coordinates} gives the coordinates"));
            p.Add(Loc.T("{PrevCategory} and {NextCategory} choose a scan category, {PrevItem} and {NextItem} choose the previous or next thing in it as your target"));
            if (s.HasTarget) p.Add(Loc.T("{JumpTarget} jumps to the target, {WhereTarget} says where it is, {Walk} walks there, {WalkStraight} walks in a straight line, {Beacon} turns the beacon on or off"));
            if (s.MenuMap)
            {
                p.Add(Loc.T("Control M or Escape returns to the map setup menu"));
                return Resolve(string.Join(". ", p) + ".");
            }
            if (!s.HasKeep)
                p.Add(Loc.T("You have no keep yet: press {BuildMenu}, choose the Keep in the Castle category, move to a good spot and press Enter"));
            else
                p.Add(Loc.T("Enter selects what is under the cursor, Shift Enter selects soldiers, a siege catapult, a dragon, a ship, a cart or a villager. {BuildMenu} opens the build menu, {ChopMode} turns chop trees mode on"));
            if (s.HasSelection)
            {
                p.Add(s.BuildingSelected
                    ? Loc.T("F6 reads the selected building's panel, Delete demolishes it, {SelectedBuilding} jumps back to it")
                    : Loc.T("F6 reads the selected tile's panel, C orders its trees chopped"));
            }
            if (s.SoldiersSelected) p.Add(Loc.T("{MoveSoldiers} sends the selected soldiers to the cursor, Control Shift Enter adds the soldiers on the tile to the selection"));
            if (s.HasKeep) p.Add(Loc.T("{Keep} jumps to your keep. {Status} kingdom status, {Date} date and weather, {Log} notifications, J job priority. Control Shift 1 to 9 stores a bookmark, Control 1 to 9 jumps to it"));
            p.Add(Loc.T("Space pauses, 1, 2 and 3 set the speed, Escape opens the pause menu"));
            return Resolve(string.Join(". ", p) + ".");
        }

        private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
