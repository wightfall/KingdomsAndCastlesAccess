using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>F1 help for every screen. Keys are screen identifiers used by the plugin.</summary>
    public static class HelpText
    {
        public const string MenuKeys =
            "Up and Down arrows move through everything on the screen. Tab and Shift Tab move between controls only. " +
            "Enter or Space activates. Left and Right arrows change sliders, option lists and radio buttons. " +
            "Home and End jump to the first and last item. Escape goes back. Control R reads the whole screen. F5 repeats the current item.";

        public const string GlobalKeys =
            "Global keys: F1 help for the current screen. Shift F1 lists every mod key. Control Shift F5 searches again for your screen reader. Control Shift M mutes or unmutes sound cues.";

        private static readonly Dictionary<string, string> Screens = new Dictionary<string, string>
        {
            { "Menu", "Main menu. Choose New to start a kingdom, Load to continue a saved game, Settings for options, or Quit. Letters jump to items starting with that letter. " + MenuKeys },
            { "ChooseMode", "Choose a game mode. Standard is the normal game with difficulty levels, Creative mode gives unlimited resources. " + MenuKeys },
            { "ChooseDifficulty", "Choose a difficulty, then choose Accept. Each difficulty describes how often raiders attack and how hard survival is. " + MenuKeys },
            { "NameAndBanner", "Name your kingdom and choose a banner. Move to the name field and press Enter to type, Enter again to confirm. Then choose Accept. " + MenuKeys },
            { "NewMap", "Map setup. Choose map size, land mass type and rival kingdoms, or generate a new random map. The map cursor works here too: press Control M to explore the map with arrow keys, Control M again to return to the menu. When ready choose Start. " + MenuKeys },
            { "RivalChoiceUI", "Choose rival AI kingdoms and their difficulty. " + MenuKeys },
            { "PauseMenu", "Pause menu. Resume returns to your kingdom. You can save, load, change settings, or quit to the main menu. " + MenuKeys },
            { "SettingsMenu", "Settings. Tabs at the top switch between graphics, audio, controls and gameplay options. Use Left and Right on sliders and option lists. " + MenuKeys },
            { "Save", "Save game. Choose an existing slot to overwrite it, or choose the new save option. You will be asked to confirm. " + MenuKeys },
            { "Load", "Load game. Saved games are listed newest first with kingdom name, year and date. Press Enter to load, confirm with Yes. " + MenuKeys },
            { "QuitConfirm", "Do you want to save before leaving? Choose yes to save first or no to leave without saving. " + MenuKeys },
            { "ExitConfirm", "Do you want to save before exiting the game? " + MenuKeys },
            { "LoadError", "The save could not be loaded. " + MenuKeys },
            { "Credits", "Credits are playing. Press Escape or choose Back to return. " + MenuKeys },
            { "Failure", "Your kingdom has fallen. Choose an option to return to the main menu or load a save. " + MenuKeys },
            { "KeepDestroyed", "Your keep was destroyed. " + MenuKeys },
            { "BannerSelect", "Choose a banner for your kingdom. Left and Right arrows or Up and Down move between banners, Enter selects. " + MenuKeys },
            { "GameWorkshopUI", "Mods and workshop. " + MenuKeys },
            { "KingdomShare", "Kingdom share lets you upload or visit shared kingdoms. " + MenuKeys },
            { "Confirm", "A question needs an answer. Choose yes or no. " + MenuKeys },
            { "Dialog", "A window is open. " + MenuKeys },
            { "Panel", "You are inside an information panel. " + MenuKeys + " F6 and Shift F6 move to the next or previous panel, Escape returns to the map." },
            { "BuildMenu",
                "Build menu. Left and Right arrows switch category, Up and Down choose a building. Each building lists cost, size and whether you can build it now. " +
                "Enter picks the building up for placement. I reads the full description. Letters jump to buildings by name. Escape closes the menu." },
            { "Placement",
                "Placing a building. Move with the arrow keys, the building follows the cursor and you hear whether the spot is valid. " +
                "Enter places it. R rotates. V reads why the spot is invalid. For roads, walls and fields press Shift Enter to mark a start point, move to the end point, then press Enter to build the whole line or area. " +
                "Escape cancels placement." },
            { "Map",
                "Kingdom map. Arrow keys move the cursor one tile, Shift plus arrows moves 5 tiles, Control plus arrows jumps to where the terrain changes. " +
                "Enter selects what is under the cursor, like a mouse click. I gives full details of the tile. B opens the build menu. " +
                "F6 moves focus into the open information panel. K kingdom status, T date and season, G cursor coordinates. " +
                "Home jumps to your keep. Control Shift 1 to 9 stores a bookmark at the cursor, Control 1 to 9 jumps back to it. Page Up and Page Down choose a scan category, open and close bracket jump to the previous or next thing of that category. " +
                "L opens the notification history. Delete demolishes the selected building, C orders trees on the selected tile chopped. " +
                "M moves selected soldiers to the cursor. With a creative mode brush selected, Enter applies it and Escape turns it off. Space pauses, 1, 2 and 3 set game speed, J opens decrees, Escape opens the pause menu." },
            { "Decrees", "Job priority. Each job row tells its priority, name, filled workers and how many workers are allowed. Space turns a job on or off, Shift Up and Shift Down move it up or down in priority, Left and Right change the allowed workers. " + MenuKeys },
            { "Diplomacy", "Talking with another kingdom. Each line of the conversation is read aloud, then the number of replies. Up and Down choose a reply, Enter answers. Gift, trade and request options appear as buttons. " + MenuKeys },
            { "LevelUp", "Your town has grown to a new size. Read the message and choose the button to continue. " + MenuKeys },
            { "Advisor", "Your advisors give tips about what your kingdom needs. Choose an advisor to hear their advice. " + MenuKeys },
            { "Witch", "The witch offers powerful services for a price. " + MenuKeys },
            { "Research", "Research at the great library. Each technology tells its gold cost or that it is already researched; Enter starts researching it. While research runs, the window shows progress and years remaining. " + MenuKeys },
            { "DemolishWarning", "Demolishing these buildings needs confirmation. Choose to demolish or cancel. " + MenuKeys },
            { "Banner", "An announcement. Read it and choose the button to dismiss it. " + MenuKeys },
            { "SurvivalIntro", "Survival mode introduction. " + MenuKeys },
            { "SurvivalSuccess", "You survived! " + MenuKeys },
            { "General", "General details. " + MenuKeys },
            { "Log", "Notification history, newest first. Up and Down move through notifications, Enter jumps the map cursor to where it happened, Escape closes." },
            { "Status", "Kingdom status list. Up and Down move through the lines, Escape closes." },
        };

        public static string For(string screenId)
        {
            if (screenId != null && Screens.TryGetValue(screenId, out var text)) return text;
            return Screens["Dialog"];
        }

        public static bool Has(string screenId) => screenId != null && Screens.ContainsKey(screenId);

        public static IEnumerable<string> Ids => Screens.Keys;

        /// <summary>The complete key reference spoken with Shift+F1.</summary>
        public static string AllKeys() =>
            GlobalKeys + " Menus: " + MenuKeys + " Map: " + Screens["Map"] + " Build menu: " + Screens["BuildMenu"] + " Placement: " + Screens["Placement"];
    }
}
