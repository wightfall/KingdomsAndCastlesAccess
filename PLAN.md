# KCAccess – Implementation Plan & Tracker

Screen reader accessibility mod for **Kingdoms and Castles** (Unity 2019.4.40f1, Mono, x64)
built on **BepInEx 5.4.23.5** and **Prism 0.18.3** (screen reader / TTS abstraction).

Status legend: `[x]` done and verified in the running game · `[~]` implemented, only partly verified · `[ ]` todo

## 0. Research (game inspection)
- [x] Identify engine / runtime (Unity 2019.4.40f1, Mono, x64, ships Harmony 1.2 for its own mod loader – BepInEx's HarmonyXInterop keeps it working)
- [x] Decompile `Assembly-CSharp.dll` and map core systems
  - `GameState` (modes), `MainMenuMode` (menu state machine), `PlayingMode`
  - `PointingSystem` / `IPointer` – every world click goes through a swappable pointer → the keyboard cursor drives the game's own placement/selection logic
  - `GameUI` (selection, placement, cursor modes, panels), `PlacementMode`, `BuildUI` / `BuildTab` / `BuildingCostUpdater`
  - `World` / `Cell` (grid data), `Building`, `Player`, `KingdomLog` (notifications), `ConfigurableControls` / `KeyChord` (key bindings)
- [x] Dump the live UI hierarchy (Ctrl+Shift+F12 dev tool) to learn how windows are built
- [x] Confirm the default game key bindings to avoid conflicts (WASD/QE camera, Space pause, 1-3 speed, R rotate, Del demolish, C chop, J job priority, U hide UI, Esc menu, `.` music)

## 1. Infrastructure
- [x] Repo layout: `KCAccess.Core` (pure, unit-testable logic), `KCAccess` (BepInEx plugin), `KCAccess.Tests`
- [x] Prism P/Invoke wrapper (UTF-8 marshalling, best backend, re-detect, log fallback) – verified with NVDA
- [x] Speech announcer: interrupt / queue, duplicate suppression, log of everything spoken
- [x] Procedural sound cues (no external audio files)
- [x] Harmony patches + input gate so keys used by the mod do not reach the game (mod ticks before the game's KeyboardControl)
- [x] Config file (BepInEx `ConfigFile`): cues, volume, coordinates, verbosity, camera follow, notifications, speech log
- [x] Developer tools: UI dump, command file for automated key injection (`key`, `nav`, `state`, `place`, `menu`)

## 2. Menus (keyboard UI navigation)
- [x] Generic UI navigator for uGUI `Selectable`s (Button, Toggle, radio groups, Slider, Dropdown, InputField, TMP variants)
- [x] Label discovery (child text, parent caption, sibling text, tooltip, onClick method name, sprite name), rich text stripping, duplicate labels get their group caption
- [x] Text entry with character echo (kingdom name, seed)
- [x] Main menu, choose mode, difficulty, name & banner, map setup (dropdowns), start game
- [x] Pause menu, save, load, settings, quit confirmation, load-error dialog
- [x] Credits, banner select (numbered tiles, selected state), mods, kingdom share
- [x] Failure / keep destroyed screens: whole message read on open (v1.3.0)
- [x] In-game modal windows: job priority (keyboard reorder replaces drag), confirmations
- [x] Advisors (named advisors, advice read out), witch hut (quests, spells), demolish warning (>25 buildings)
- [x] Diplomacy (v1.3.0): Dialogue System hooks speak every line and the number of replies, focus jumps to the first reply / Continue; rival setup slots labelled; envoys sent with M to a keep under the cursor
- [x] Research (v1.3.0): technologies with effect and gold cost or "already researched", missing gold explained, progress view
- [x] Merchant trading (v1.3.0): trade lines with resource names, Left/Right/Page Up/Page Down change amounts, cost spoken; merchant ships selectable with Shift+Enter
- [x] Dock / stockpile desired-resource lines named; icon-only numbers get their resource name
- [x] Level-up and announcement windows read in full when they open
- [x] F6 / Shift+F6 cycle: selected building/tile → kingdom overview (tax buttons) → toolbar (speed, cursor modes, menu); Ctrl+R read all
- [x] Escape = back/close (clicks the window's Back/Close/No button, otherwise the game's own Escape)

## 3. Gameplay
- [x] Keyboard map cursor (arrows, Shift = 5, Ctrl = jump to next change), virtual pointer, camera follows
- [x] Tile description (terrain, trees, resources, fertility, water type, ownership, buildings + construction %, villagers, units, fire, fog, road coverage, chop marks)
- [x] Accessible build menu (categories → buildings with cost / missing amounts, size, availability, prerequisites, description, wages)
- [x] Placement: validity cue + reason + blocking footprint tiles, rotate, drag placement for roads (Shift+Enter … Enter), "Placed X, N pieces"
- [x] Select buildings / tiles via Enter, read the panel with F6, operate its buttons
- [x] Cursor modes (chop verified; demolish / rebuild same code path) incl. Shift+Enter area marking
- [x] Kingdom status list (K) with the game's detailed yearly resource / happiness / health reports
- [x] Notifications spoken with direction + history browser (L) with jump-to-location
- [x] Scanner: buildings, construction sites, own units, threats, stone, iron, forests, fresh water, foreign kingdoms, special places
- [x] Map exploration from the map setup screen (Ctrl+M) before starting
- [x] Armies: select with Shift+Enter, move with M (verified with creative-mode knights)
- [x] Creative mode: options menu, check boxes, brushes applied with Enter, Escape turns brush off; map editor painting from map setup
- [x] F1 context help for every screen, Shift+F1 all keys
- [x] Announcements: speed / pause changes, seasons, selection cleared, cursor mode changes, threats (vikings, dragons), merchants, manual saves
- [x] Navigation (v1.4.0): [ / ] choose a scanner target; N auto-walks along a route villagers can walk (own A* on the game's footpath costs, works before the game starts too, ends next to blocked targets, tells when the target is on another island); Ctrl+N straight walk; \ jump; Shift+\ where is it; Shift+N audio beacon (pan = east/west, pitch = north/south, faster = closer, chime on arrival); Alt+1-9 bookmark as target; Shift+Enter in the notification log sets the target
- [x] O area survey (land, open, fertile, forest, stone, iron, water within 6 tiles, nearest stone / iron / water when none is close)
- [x] Scanner category "Fertile land" (open fertile ground for farms); scan list refreshes itself
- [x] Shift+C chop trees mode; C on a tile says whether chopping was ordered or cancelled; area chop says how many tree tiles were marked
- [x] Villager selection announced (name, age, job, home, thought), Shift+Enter cycles villagers on a tile, villager panel reachable with F6

## 4. Quality
- [x] Unit tests for Core logic (200 tests, xUnit)
- [x] v1.0.0 released; v1.1.0 with HUD counter fix, kingdom overview descriptions, creative mode support
- [x] v1.2.0: setup exe in the release, dialog fix, advisors/banners/demolish labels, bookmarks
- [x] Updater verified end to end: the v1.1.0 updater found 1.2.0 on GitHub, asked, downloaded, installed it and replaced itself
- [x] v1.3.0: diplomacy, research, merchant trading, defeat screen verified; ships selectable; keep placement no longer blocked when the camera starts over water
- [x] v1.4.0: full no-cheat playthrough on Sommern (keep → hamlet → small village: roads, hovels, cottages, farms, quarry, granary, wells, Treasure Room and taxes, advisors, chopping, saving / loading); navigation, area survey, settings keyboard tab rebinding
- [x] Release rule: every release ships KCAccess-Setup-vX.Y.Z.exe, the bundle zip and the mod-only zip (tools/package.ps1 builds all three)
- [x] Bug fixes in the game discovered during inspection (see below)
- [x] README (install, keys, cues, settings, troubleshooting, building), third-party notices, MIT license
- [x] `.gitignore`, `tools/fetch-deps.ps1`, `tools/package.ps1` (mod zip + bundle with BepInEx)
- [x] GitHub repo, push, release
- [x] Setup / updater exe (KCAccess-Setup-vX.Y.Z.exe): finds the game via Steam, offline install, update check with Yes/No prompt, copies itself as KCAccess-Updater.exe; part of every release from v1.1.0

## Game bugs found & fixed by the mod
| # | Bug | Fix |
|---|-----|-----|
| 1 | `KeyChord.GetKeyDown/GetKeyUp` compare Ctrl/Alt/Shift with `GetKeyDown` (pressed *this frame*) instead of `GetKey` (held). Bindings with modifiers (e.g. Ctrl+S) practically never fire, and Ctrl+key also triggers the plain binding (Ctrl+1/2/3 changed speed) | Prefix replaces both methods with a held-modifier check |
| 2 | `KeyboardControl.UpdatePlaymodeKeys` toggles **creative mode** on Ctrl+C in normal play (developer shortcut outside the cheat check) | Postfix restores the mode unless cheats are enabled |

## Mod bugs found while testing (fixed)
- Visibility test used `lossyScale` of screen-space-camera canvases → nearly every control looked invisible.
- Screen-position ordering interleaved columns → switched to hierarchy order (matches layout groups).
- `Plugin.Instance` is destroyed by the game after start-up → input gate silently allowed all keys; now uses a static flag.
- Placement announcements were missed because the game restages the next piece in the same frame → hooked `PlacementMode.AcceptPlacement`.
- Job priority rows: logic lives on hidden layout objects, visible rows are "followers" → mapped and read in priority order.
- `TextUtil.Clean` produced ".." (found by unit tests).
- Counters (population, resources, save list) read stale placeholder values: the game uses TMP `SetText(format, number)`, which does not update `.text` → read the rendered text instead (v1.1.0).
- Navigator kept a stale item list right after a click that opened part of a panel → refresh immediately after activation.
- Force-killing the game during development truncated an autosave (dev script now closes the game gracefully).
- After closing the Advisors window from a building panel the panel focus cleared the dialog's items → keys were swallowed by an empty dialog. Dialogs now replace panel focus; empty "windows" never capture the keyboard.
- Starting camera over water made the keep "unavailable, build your keep first" (the game only offers it while the camera is over land) → the cursor starts on valid land and the build menu moves there if needed.
- Escape after placing a building did not end the game's "place another" mode → handled by the mod.
- Read-only bars (opinion, busy timers) were offered as adjustable sliders → shown as progress bars.
- Foreign kingdoms were missing from the scanner (AI buildings are not in the player's list) → read from the AI kingdom data, including unexplored ones.
- Key rebinding in Settings → Keyboard pressed the game's capture with the Enter that opened it; now waits until keys are released, then announces "X is now Y". "Restore Defaults" was read as a key row.
- Tax buttons said "build a throne room first": the game's building is the Treasure Room; disabled tooltips (stale "build a treasure room" text) are no longer read.
- Line / area placement only said "Placed N pieces": now says how many were skipped and why (barren soil, outside road coverage, trees…), and while dragging how many of the planned pieces can be built.
- "Outside road coverage" now names the nearest free tile inside coverage; "needs clear land" names trees and growing / falling trees in the footprint.
- "Placing X…" instructions repeated after every "place another" → said once.
- Fire risk bar read as "Low High" → reads the risk level; advisor portraits' captions and duplicate resident names removed; camera-only "find villagers" glass hidden.
- Saving from the save screen is confirmed ("Game saved"); F5 no longer repeats a tooltip equal to the label.
- Game text bug: some strings show "<!-Missing Translation [text]-!>" (e.g. the demolish warning) → the marker is removed before speaking.

## Notes / decisions
- Map directions are absolute: North = +Z (top of the map), East = +X. A building's cursor tile is its south west corner.
- The camera follows the keyboard cursor so sighted helpers can see what the player is doing.
- Moving the mouse gives control back to the mouse pointer; any cursor key returns to keyboard control.
- Non-modal panels are reached with F6 so map keys keep working while a building is selected.

## Extras
- [x] Map bookmarks: Ctrl+Shift+1-9 store, Ctrl+1-9 jump (saved per kingdom)

## Next ideas
- Keep playing long games to find windows where the generic reader is weak (e.g. AI negotiation price editor, ship logistics).
- Late-game threats on harder regions (dragons, siege) with real armies; castle wall stacking by keyboard.

