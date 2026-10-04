# KCAccess – screen reader accessibility for Kingdoms and Castles

KCAccess is a [BepInEx 5](https://github.com/BepInEx/BepInEx) mod that makes **Kingdoms and Castles**
playable with a screen reader and the keyboard only. Speech and braille go through
[Prism](https://github.com/ethindp/prism), which talks to NVDA, JAWS, ZoomText, Narrator / OneCore,
SAPI and other screen readers automatically.

* Every menu and dialog is readable and operable with the keyboard (main menu, new game setup,
  pause menu, settings, save / load, confirmations, in-game windows and building panels).
* A keyboard **map cursor** lets you explore the world tile by tile, hear what is on each tile, and
  drives the game's own mouse logic, so selecting, placing buildings, drag-building roads and walls,
  chopping and demolishing all work exactly like they do with a mouse.
* An accessible **build menu** with costs, sizes, availability and the reason a building is locked.
* Placement feedback: sound cues for valid / invalid spots, the reason a spot is invalid and which
  tiles of the footprint are blocked.
* Kingdom status, the game's detailed resource reports, notifications with location and history,
  a scanner to find buildings, resources, fertile land and threats, and army control.
* **Navigation:** pick a target from the scanner, notifications or bookmarks, then auto-walk there along
  a route villagers can walk (N), walk in a straight line, jump, or follow a stereo **audio beacon**.
* **O** surveys the area around the cursor (fertile soil, forest, stone, iron, water) to choose where to build.
* **F1** on every screen tells you what you can do there.
* Procedurally generated sound cues (no audio files needed).

## Installation

Requirements: Windows 10 or newer, the Steam version of Kingdoms and Castles (64-bit), and a screen
reader (Prism falls back to Windows speech if none is running).

### Easiest: the setup program

1. Download `KCAccess-Setup-vX.Y.Z.exe` from the
   [releases page](https://github.com/wightfall/KingdomsAndCastlesAccess/releases) and run it.
   (Windows SmartScreen may warn about an unknown publisher: choose More info, then Run anyway.)
2. It finds Kingdoms and Castles through Steam (or press **Choose game folder**), shows what is
   installed, and **Install or update** installs BepInEx (if needed) and the mod. Everything is
   built into the exe, so it also works offline.
3. It checks GitHub for a newer version and asks before installing it. It also copies itself into
   the game folder as **KCAccess-Updater.exe**: run that any time to check for updates. It keeps
   your settings and any existing BepInEx install.

The window is made of standard Windows controls: the status text box has focus when it opens,
Tab moves between the buttons, and questions are Yes / No message boxes. Silent install:
`KCAccess-Setup-vX.Y.Z.exe --quiet` (exit code 0 = installed, 2 = game not found, 3 = game running).

### Alternative: the bundle zip

1. Download `KCAccess-vX.Y.Z-with-BepInEx.zip` from the
   [releases page](https://github.com/wightfall/KingdomsAndCastlesAccess/releases).
2. Extract **everything** into the game folder, normally
   `C:\Program Files (x86)\Steam\steamapps\common\Kingdoms and Castles`, so that `winhttp.dll`
   and the `BepInEx` folder sit next to `KingdomsAndCastles.exe`.
3. Start the game. After a few seconds you hear "Main menu".

### If you already use BepInEx 5.4

Download `KCAccess-vX.Y.Z.zip` and extract it into the game folder. It only contains
`BepInEx\plugins\KCAccess\` (`KCAccess.dll`, `KCAccess.Core.dll`, `prism.dll`, licenses).

### Uninstall

Run KCAccess-Updater.exe and choose **Uninstall**, or delete `BepInEx\plugins\KCAccess`. To remove BepInEx completely also delete `winhttp.dll`,
`doorstop_config.ini`, `.doorstop_version` and the `BepInEx` folder.

## Keys

Press **F1** at any time for help about the current screen and **Shift+F1** for this whole list.

### Everywhere

| Key | Action |
|-----|--------|
| F1 | Help for the current screen |
| Shift+F1 | Every mod key |
| Ctrl+Shift+F5 | Search for the screen reader again (if you started it after the game) |
| Ctrl+Shift+F10 | Reset the mod if it ever stops speaking or responding (no need to quit the game) |
| Ctrl+Shift+F11 | Keyboard report: what owns the keyboard, held modifiers, keyboard layout (also written to the log) |
| Ctrl+Shift+M | Sound cues on / off |

### Menus, dialogs and panels

| Key | Action |
|-----|--------|
| Up / Down | Move through everything on the screen (buttons and text) |
| Tab / Shift+Tab | Move between controls only |
| Home / End | First / last item |
| Enter or Space | Activate the button, toggle the check box |
| Left / Right | Change a slider, option list (combo box) or radio button group |
| Page Up / Page Down | Change a slider in big steps |
| Letters | Jump to items starting with that letter (menu screens) |
| Ctrl+R | Read the whole screen |
| F5 | Repeat the current item with its position |
| Escape | Back / close / cancel |

**Text fields** (kingdom name, map seed): press Enter to start editing, type (each character is
echoed, Backspace says what was deleted), Enter to confirm, Escape to cancel. Up, Down or F2 read
the field.

**Job priority window** (J): each job is one row – "Priority 2, Castle jobs, 3 workers, allowed 5 of 8".
Space turns the job on or off, **Shift+Up / Shift+Down** change its priority (this replaces the
mouse drag), Left / Right change how many workers are allowed.

### Map

| Key | Action |
|-----|--------|
| Arrow keys | Move the cursor one tile (north is up) |
| Shift+Arrows | Move 5 tiles |
| Ctrl+Arrows | Jump to the next tile that is different (edge of a forest, a building, water …) |
| Enter | Select the building or tile under the cursor; while placing: build; in chop / demolish mode: apply |
| Shift+Enter | While placing roads, walls or fields: set the start point, then Enter at the end point builds the whole line or area. In chop / demolish mode: mark one corner of an area. Otherwise: select soldiers, a ship (merchant ships open the trade window) or a villager on the tile (press again to cycle) |
| I | Everything about the tile (terrain, fertility, road coverage, workers …) |
| Shift+I | Details of the selected building |
| F5 | Repeat the tile description |
| G | Cursor coordinates and map size |
| O | Area survey: land, open, fertile, forest, stone, iron and water tiles within 6 tiles, plus the nearest stone, iron or water when none is close |
| Shift+C | Chop trees mode on / off (Shift+Enter one corner, Enter the other) |
| B | Build menu |
| F6 / Shift+F6 | Move into the open panels: selected building or tile → kingdom overview (population, happiness, tax buttons, resources) → toolbar (speed, chop / demolish / rebuild modes, menu). F6 at the end, or Escape, returns to the map |
| K | Kingdom status list (Enter on a line reads the game's detailed report) |
| T | Year, season, weather and game speed |
| L | Notification history; Enter jumps to where it happened, Shift+Enter makes it the navigation target |
| Shift+L | Repeat the last notification |
| Home | Jump to your keep |
| End | Jump to the selected building |
| Ctrl+Shift+1 … 9 | Store a bookmark at the cursor (saved per kingdom) |
| Ctrl+1 … 9 | Jump to a bookmark |
| Page Up / Page Down | Choose a scan category: your buildings, construction sites, your soldiers and ships, threats, stone, iron, open fertile land, forests, fresh water, foreign kingdoms, special places |
| [ and ] | Choose the previous / next item of that category as the navigation target, nearest first (the list refreshes itself) |
| N | Auto-walk the cursor to the target along a route villagers can walk (ends next to it if the target itself is blocked) |
| Ctrl+N | Walk to the target in a straight line, over water and buildings |
| \ | Jump straight to the target |
| Shift+\ | Say where the target is from the cursor |
| Shift+N | Target beacon on / off: pings panned to the target's side, higher pitch north, lower south, faster when closer, a chime on arrival |
| Alt+1 … 9 | Make a bookmark the navigation target |
| Any arrow / Escape | Stop an auto-walk |
| V | While placing: why the spot is (in)valid |
| M | Send the selected soldiers to the cursor |

The game's own keys keep working: **Space** pause, **1 2 3** speed, **R** rotate while placing,
**Delete** demolish the selected building, **C** chop trees on the selected tile, **J** job priority,
**Escape** cancel / close / pause menu, **WASD QE** camera. Avoid **U**: it hides the game interface,
and hidden panels cannot be read (press U again to bring it back).

### Trading, diplomacy and research

* **Merchants:** when "A merchant ship has arrived" is announced, find it with the scanner (Your soldiers
  and ships), press Shift+Enter on it and then F6. Each trade line reads "Wood: 2 gold each, 40 available,
  buy 5, costs 10 gold"; Left / Right change the amount by 1, Page Up / Page Down by 10, Enter lets you
  type it. Then choose the complete-transaction button.
* **Diplomacy:** every line an envoy or ruler says is read aloud, followed by the number of replies;
  focus jumps to the first reply. Up / Down choose, Enter answers. To visit a kingdom, select your envoy
  (Shift+Enter), put the cursor on the other kingdom's keep and press M.
* **Research (Great Library):** select the library, F6, Research. Each technology reads its effect and
  gold cost, or "already researched"; Enter starts it, or tells you how much gold is missing. While research
  runs the window reads progress and years remaining.

### Creative mode

F6 reaches **Creative mode options**: press the Creative Mode button to open the menu with its check
boxes (Build For Free, Hide Fog of War, Viking Attacks …) and brushes (spawn knights, archers, vikings,
dragons, resource stacks, fire, trees, removal tool). Picking a brush returns you to the map; **Enter**
applies it at the cursor, **Escape** turns the brush off. On the map setup screen the map editor
brushes work the same way: pick a brush, press Ctrl+M, move the cursor and press Enter to paint.

### Build menu (B)

| Key | Action |
|-----|--------|
| Left / Right or Tab | Previous / next category (Castle, Town, Advanced town, Food, Industry, Maritime) |
| Up / Down, Home / End | Choose a building: name, cost (what you are missing), size, available or why not |
| I or F5 | Full description, workers and wages |
| Letters | Jump to a building by name |
| Enter or Space | Pick up the building; cemeteries, statues and parks open their own list |
| Escape or B | Close |

When you pick up a building, the cursor marks its **south west corner**; a 3 by 3 building covers the
cursor tile and the tiles to the north and east. Most buildings must be inside **road coverage**:
build roads out from your keep first (I tells you whether a tile is covered).

### Starting a new game

1. New → Standard Mode Accept → choose a region (difficulty) with Previous / Next → Accept.
2. Kingdom name (Enter to edit) → Accept.
3. Map setup: size, type and rivers; New Map generates another map. **Ctrl+M** lets you explore the
   generated map with the arrow keys and the scanner before you start; Ctrl+M again returns.
4. Accept starts the game. Press B, pick the Keep (first item in Castle), move to clear land near
   trees, stone and fertile soil (press O to survey the area), and press Enter.

### The first years (tested walkthrough)

1. **Roads:** B, Town, Road. Shift+Enter next to the keep, move, Enter builds the line. Tiles with trees,
   rock or water are skipped and you are told how many and why.
2. **Homes:** a Hovel (Town) unlocks the Industry category; homeless visitors are reported as notifications.
3. **Wood:** villagers chop marked trees. Shift+C turns chop trees mode on: Shift+Enter on one corner,
   Enter on the other ("12 tiles of trees marked for chopping"). Shift+C again turns it off.
4. **Food:** Page Down to "Fertile land", ] picks the nearest patch, N walks there. B, Food, Farm,
   Shift+Enter, move, Enter. Barren tiles and tiles outside road coverage are skipped and reported.
   A Granary stores the harvest.
5. **Stone:** Page Down to "Stone", ] and N, then place a Quarry on a free tile next to the stone.
6. **Gold:** build a Treasure Room (Castle), then F6 to the kingdom overview and raise the tax rate.
7. Advisors in the keep (select the keep, F6, Advisors) tell you what the kingdom needs; a house panel
   lists happiness, food, fire risk and its residents (Enter on a resident selects the villager).
8. Save from the pause menu (Escape, Save, Make New Save); "Game saved" confirms it.

### Saved games

On the Load and Save screens, **Delete** on a saved game deletes it. The confirmation starts on
Cancel; choose "It's toast" to delete. "Save deleted" confirms it.

## Sound cues

| Sound | Meaning |
|-------|---------|
| Short tick | Moved to another item / grass tile |
| Rising chirp | List wrapped around |
| Low thud | Edge of a list or of the map |
| Square blip | Tile with a building, stone or iron |
| Bubbly glide | Water tile |
| Soft hiss | Unexplored (fogged) tile |
| High beep / buzz | Placement spot valid / invalid |
| Three rising notes | Building placed |
| Two notes up / down | Toggle on / off |
| Glide up / down | Window opened / closed |
| Two bright notes | Notification |
| Siren | Danger: raid, dragon, fire, plague |
| Two-tone ping (panned, pitched) | Navigation beacon: left / right ear = west / east, higher = north, lower = south, faster = closer |
| Soft footstep | One step of an auto-walk |

## Settings

`BepInEx\config\kcaccess.screenreader.cfg` (created on first start):

| Setting | Default | Meaning |
|---------|---------|---------|
| Audio / SoundCues | true | Play sound cues |
| Audio / CueVolume | 0.5 | Cue volume 0 – 1 |
| Map / SpeakCoordinates | false | Add X, Z to every tile description |
| Map / VerboseCells | false | Speak fertility for every tile while moving |
| Map / CameraFollowsCursor | true | Camera follows the keyboard cursor (helps sighted helpers) |
| Speech / AnnounceNotifications | true | Speak kingdom notifications automatically |
| Speech / LogSpeech | true | Write everything spoken to `BepInEx\LogOutput.log` |

## Game bugs fixed by the mod

* **Key bindings with modifiers never worked.** `KeyChord.GetKeyDown/Up` compared Ctrl/Alt/Shift with
  "pressed this frame" instead of "held", so a binding like Ctrl+S could practically never fire and
  Ctrl+key also triggered the plain key's action. Modifiers are now checked as held.
* **Ctrl+C silently switched the game into creative mode** during normal play (a developer shortcut
  outside the cheat check). It now only works when cheats are enabled.

## Troubleshooting

* **No speech:** check `BepInEx\LogOutput.log`. "Prism … ready, backend: NVDA" means speech works.
  If it says Prism could not start, make sure `prism.dll` is in `BepInEx\plugins\KCAccess`. If you
  start your screen reader after the game, press Ctrl+Shift+F5.
* **Nothing happens at all:** BepInEx is not loading – `winhttp.dll` must be next to
  `KingdomsAndCastles.exe`. Anti-virus programs sometimes quarantine it.
* **The mouse takes over:** moving the mouse gives control back to the mouse pointer; any cursor key
  returns to keyboard control.
* Please attach `BepInEx\LogOutput.log` to bug reports.

### Keyboard problems (keys like Escape, Space, 1 2 3 or L do nothing)

* **Shift+Tab opens the Steam overlay** (Steam's default shortcut), which takes every key away from the game
  until it is closed. The mod says "Steam overlay opened" and "Back in the game"; press Shift+Tab or Escape
  to close it. To use Shift+Tab in the mod's menus, turn the overlay off (game Properties, General, "Enable
  the Steam Overlay while in-game") or change its shortcut in Steam Settings, In Game. Up arrow also moves
  to the previous item.

* Press **Ctrl+Shift+F11** in the game: it speaks what currently owns the keyboard, which modifier keys
  the game and Windows think are held, and the keyboard layout, and writes it to `BepInEx\LogOutput.log`.
* For a detailed report set `KeyLog = true` (on by default since 1.4.2) in `BepInEx\config\kcaccess.screenreader.cfg`, reproduce
  the problem, then send `BepInEx\LogOutput.log`.
* Since 1.4.3 the mod also watches the Windows keyboard itself: when Windows reports a key press the game
  never received (seen on a Windows 11 laptop, with NVDA Remote), the mod delivers it, including the game's
  own keys (Escape menu, Space pause, 1 2 3 speed). The log then shows `[fallback]` lines. It is on by
  default; `WindowsKeyFallback = false` in the `[Keyboard]` section of the config turns it off.
* If keys still go missing, turn off Steam's input layer for this game, which the mod cannot change itself:
  in the Steam library, open the game's Properties, Controller, choose "Disable Steam Input", and under
  General turn off "Enable the Steam Overlay while in-game".
* Since 1.4.1 the mod ignores Shift / Ctrl / Alt that the game believes are held when Windows says they
  are not (this happened with NVDA Remote and after Alt+Tab), turns the input method editor off while you
  are not typing, and frees text fields that grab the keyboard in the background.

## Building from source

Requirements: .NET SDK 6 or newer, the game with BepInEx 5.4 installed.

```
powershell -ExecutionPolicy Bypass -File tools\fetch-deps.ps1     # downloads prism.dll into lib\
dotnet build src\KCAccess -c Release                               # builds and copies into the game
dotnet test tests\KCAccess.Tests                                   # unit tests
powershell -ExecutionPolicy Bypass -File tools\package.ps1         # release zips in dist\
```

If the game is not installed in the default Steam folder pass `-p:GameDir="D:\path\to\Kingdoms and Castles"`.

Project layout:

* `src/KCAccess.Core` – game-independent logic (text cleaning, navigation lists, grid cursor and
  directions, sound synthesis, help texts, notification log …), fully unit tested.
* `src/KCAccess` – the BepInEx plugin: Prism speech, UI navigator, screen detection, map controller,
  build menu, Harmony patches.
* `tests/KCAccess.Tests` – xUnit tests for the Core library.
* `PLAN.md` – the implementation plan and progress tracker.

Developer helpers: with `[Debug] CommandFile = true` in the config the mod reads test commands from
`BepInEx\kcaccess_commands.txt` (`key DownArrow`, `key Return shift`, `nav`, `state`, `place`, `dump`),
and Ctrl+Shift+F12 writes the live UI hierarchy to `BepInEx\kcaccess_ui_dump.txt`.

## License

MIT, see `LICENSE`. Third-party components are listed in `THIRD-PARTY-NOTICES.md`.
