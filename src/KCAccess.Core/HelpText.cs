using System.Collections.Generic;

namespace KCAccess.Core
{
    /// <summary>F1 help for every screen. Keys are screen identifiers used by the plugin.</summary>
    public static class HelpText
    {
        private static readonly Dictionary<string, string> Screens = new Dictionary<string, string>
        {
            { "Menu", Loc.N("Main menu. Choose New to start a kingdom, Load to continue a saved game, Settings for options, or Quit.") },
            { "ChooseMode", Loc.N("Choose a game mode. Standard is the normal game with difficulty levels, Creative mode gives unlimited resources.") },
            { "ChooseDifficulty", Loc.N("Choose a difficulty, then choose Accept. Each difficulty describes how often raiders attack and how hard survival is.") },
            { "NameAndBanner", Loc.N("Name your kingdom and choose a banner. Move to the name field and press Enter to type, Enter again to confirm. Then choose Accept.") },
            { "NewMap", Loc.N("Map setup. Choose map size, land mass type and rival kingdoms, or generate a new random map. The map cursor works here too: press Control M to explore the map with arrow keys, Control M again to return to the menu. When ready choose Start.") },
            { "RivalChoiceUI", Loc.N("Choose rival AI kingdoms and their difficulty.") },
            { "PauseMenu", Loc.N("Pause menu. Return goes back to your kingdom. You can save, load, change settings, or quit to the main menu.") },
            { "SettingsMenu", Loc.N("Settings. Tabs at the top switch between graphics, audio, controls and gameplay options.") },
            { "Save", Loc.N("Save game. Choose an existing slot to overwrite it, or choose the new save option. You will be asked to confirm.") },
            { "Load", Loc.N("Load game. Saved games are listed newest first with kingdom name, year and date. Press Enter to load, confirm with Yes.") },
            { "QuitConfirm", Loc.N("Do you want to save before leaving? Choose yes to save first or no to leave without saving.") },
            { "ExitConfirm", Loc.N("Do you want to save before exiting the game?") },
            { "LoadError", Loc.N("The save could not be loaded.") },
            { "Credits", Loc.N("Credits are playing. Choose Back to return.") },
            { "Failure", Loc.N("Your kingdom has fallen. Choose an option to return to the main menu or load a save.") },
            { "KeepDestroyed", Loc.N("Your keep was destroyed.") },
            { "BannerSelect", Loc.N("Choose a banner for your kingdom. Left and Right arrows or Up and Down move between banners, Enter selects.") },
            { "GameWorkshopUI", Loc.N("Mods and workshop.") },
            { "KingdomShare", Loc.N("Kingdom share lets you upload or visit shared kingdoms.") },
            { "Confirm", Loc.N("A question needs an answer. Choose yes or no.") },
            { "Dialog", Loc.N("A window is open.") },
            { "Panel", Loc.N("You are inside an information panel of the game.") },
            { "Panel.Twitch",
                Loc.N("Twitch chat voting. Type your Twitch channel name in the text field and press Enter; you hear when the chat is connected. " +
                "The interval and maximum votes lists set how often a vote runs and how many options it offers, and the check boxes turn single vote options on or off. " +
                "Each new vote is announced with the numbers viewers type in chat, for example #2. {Status} shows the running vote counts and the time left, Enter on that line opens or closes these settings. " +
                "The result banner says what the viewers chose. Escape closes the settings and returns to the map. Captions, reading chat aloud and the overlay status file are in the mod settings, group Streaming.") },
            { "BuildMenu",
                Loc.N("Build menu. Left and Right arrows switch category, Up and Down choose a building. Each building lists cost, size and whether you can build it now. " +
                "Enter picks the building up for placement. I reads the full description. Letters jump to buildings by name. Escape or {BuildMenu} closes the menu.") },
            { "Placement",
                Loc.N("Placing a building. Move with the arrow keys, the building follows the cursor and you hear whether the spot is valid. " +
                "Enter places it. R rotates. {Validity} reads why the spot is invalid. For roads, walls and fields press Shift Enter to mark a start point, move to the end point, then press Enter to build the whole line or area. " +
                "Escape cancels placement.") },
            { "Map",
                Loc.N("Kingdom map. Arrow keys move the cursor one tile, Shift plus arrows moves 5 tiles, Control plus arrows jumps to where the terrain changes. " +
                "Enter selects what is under the cursor, like a mouse click. {TileInfo} gives full details of the tile. {Survey} surveys the area around the cursor: land, fertile soil, forest, stone, iron and water, useful to choose where to build. {BuildMenu} opens the build menu. " +
                "F6 moves focus into the open information panel. {Status} kingdom status, {Date} date and season, {Coordinates} cursor coordinates. " +
                "{Keep} jumps to your keep. Control Shift 1 to 9 stores a bookmark at the cursor, Control 1 to 9 jumps back to it. {PrevCategory} and {NextCategory} choose a scan category, {PrevItem} and {NextItem} choose the previous or next thing of that category as your target, nearest first. {Walk} walks the cursor to the target along a route villagers can walk, {WalkStraight} walks in a straight line, {JumpTarget} jumps straight there, {WhereTarget} says where the target is. {Beacon} turns the target beacon on or off: pings come from the target's side, higher pitch means north, lower means south, faster means closer. Any arrow key or Escape stops a walk. Alt 1 to 9 makes a bookmark the target. " +
                "{Log} opens the notification history. Delete demolishes the selected building, C orders trees on the selected tile chopped, or cancels it. {ChopMode} turns chop trees mode on or off: Shift Enter marks one corner, Enter the other, and every tree in between is marked. " +
                "{MoveSoldiers} moves selected soldiers to the cursor, Control Shift Enter adds more soldiers to the selection. With a creative mode brush selected, Enter applies it and Escape turns it off. Space pauses, 1, 2 and 3 set game speed, J opens decrees, Escape opens the pause menu.") },
            { "Decrees", Loc.N("Job priority. Each job row tells its priority, name, filled workers and how many workers are allowed. Space turns a job on or off, Shift Up and Shift Down move it up or down in priority, Left and Right change the allowed workers.") },
            { "Diplomacy", Loc.N("Talking with another kingdom. Each line of the conversation is read aloud, then the number of replies. Up and Down choose a reply, Enter answers. Gift, trade and request options appear as buttons.") },
            { "LevelUp", Loc.N("Your town has grown to a new size. Read the message and choose the button to continue.") },
            { "Advisor", Loc.N("Your advisors give tips about what your kingdom needs. Choose an advisor to hear their advice.") },
            { "Witch", Loc.N("The witch offers powerful services for a price.") },
            { "Research", Loc.N("Research at the great library. Each technology tells its gold cost or that it is already researched; Enter starts researching it. While research runs, the window shows progress and years remaining.") },
            { "DemolishWarning", Loc.N("Demolishing these buildings needs confirmation. Choose to demolish or cancel.") },
            { "Banner", Loc.N("An announcement. Read it and choose the button to dismiss it.") },
            { "SurvivalIntro", Loc.N("Survival mode introduction.") },
            { "SurvivalSuccess", Loc.N("You survived!") },
            { "General", Loc.N("General details.") },
            { "Log", Loc.N("Notification history, newest first. Up and Down move through notifications, Enter jumps the map cursor to where it happened, Shift Enter makes that place your navigation target, Escape closes.") },
            { "ModSettings", Loc.N("Mod settings. Up and Down move through the settings, Enter or Space toggles a check box, Left and Right change a value such as the mod language, the sound volume or the walking speed. On a mod key, Enter waits for the new key: press it with Control, Shift or Alt if you like, Escape cancels. A key already used by the mod or the game is refused and you are told what uses it. Delete restores that key's default. The last line resets everything. Escape closes.") },
            { "Keys", Loc.N("Key list. Up and Down read one key at a time, Page Up and Page Down jump between groups, Home and End go to the first and last line, letters jump to a line. The sound cues are at the end: Enter on a sound plays it. Escape closes.") },
            { "Status", Loc.N("Kingdom status list. Up and Down move through the lines, Enter on a resource, happiness or health line reads the game's report, Enter on the Twitch vote line opens the Twitch voting settings. Escape closes.") },
        };

        public static string For(string screenId)
        {
            // Texts name rebindable keys as {BindingId}: always speak the player's current keys.
            if (screenId != null && Screens.TryGetValue(screenId, out var text)) return KeyHelp.Resolve(Loc.T(text));
            return Loc.T(Screens["Dialog"]);
        }

        public static bool Has(string screenId) => screenId != null && Screens.ContainsKey(screenId);

        public static IEnumerable<string> Ids => Screens.Keys;

        /// <summary>The complete key reference spoken with Shift+F1.</summary>
        public static string AllKeys() => KeyHelp.AllText();
    }
}
