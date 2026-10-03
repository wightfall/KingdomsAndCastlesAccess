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
  a scanner to jump to buildings, resources and threats, and army control.
* **F1** on every screen tells you what you can do there.
* Procedurally generated sound cues (no audio files needed).

## Installation

Requirements: Windows 10 or newer, the Steam version of Kingdoms and Castles (64-bit), and a screen
reader (Prism falls back to Windows speech if none is running).

### Easiest: the bundle

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

Delete `BepInEx\plugins\KCAccess`. To remove BepInEx completely also delete `winhttp.dll`,
`doorstop_config.ini`, `.doorstop_version` and the `BepInEx` folder.

## Keys

Press **F1** at any time for help about the current screen and **Shift+F1** for this whole list.

### Everywhere

| Key | Action |
|-----|--------|
| F1 | Help for the current screen |
| Shift+F1 | Every mod key |
| Ctrl+Shift+F5 | Search for the screen reader again (if you started it after the game) |
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
| Shift+Enter | While placing roads, walls or fields: set the start point, then Enter at the end point builds the whole line or area. In chop / demolish mode: mark one corner of an area. Otherwise: select soldiers or a villager on the tile (press again to cycle) |
| I | Everything about the tile (terrain, fertility, road coverage, workers …) |
| Shift+I | Details of the selected building |
| F5 | Repeat the tile description |
| G | Cursor coordinates and map size |
| B | Build menu |
| F6 / Shift+F6 | Move into the open panels: selected building or tile → kingdom overview (population, happiness, tax buttons, resources) → toolbar (speed, chop / demolish / rebuild modes, menu). F6 at the end, or Escape, returns to the map |
| K | Kingdom status list (Enter on a line reads the game's detailed report) |
| T | Year, season, weather and game speed |
| L | Notification history; Enter jumps to where it happened |
| Shift+L | Repeat the last notification |
| Home | Jump to your keep |
| End | Jump to the selected building |
| Page Up / Page Down | Choose a scan category: your buildings, construction sites, your soldiers and ships, threats, stone, iron, forests, fresh water, foreign kingdoms, special places |
| [ and ] | Jump to the previous / next item of that category, nearest first |
| \ | Rebuild the scan list from the current cursor position |
| V | While placing: why the spot is (in)valid |
| M | Send the selected soldiers to the cursor |

The game's own keys keep working: **Space** pause, **1 2 3** speed, **R** rotate while placing,
**Delete** demolish the selected building, **C** chop trees on the selected tile, **J** job priority,
**Escape** cancel / close / pause menu, **WASD QE** camera. Avoid **U**: it hides the game interface,
and hidden panels cannot be read (press U again to bring it back).

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
   trees, stone and fertile soil, and press Enter. Then build roads, houses and farms.

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
