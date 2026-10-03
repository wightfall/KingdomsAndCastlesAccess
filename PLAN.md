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
- [~] Credits, failure screen, banner select, workshop/mods, kingdom share (generic navigator, not walked through)
- [x] In-game modal windows: job priority (keyboard reorder replaces drag), confirmations
- [~] Diplomacy, level-up, advisor, demolish warning, witch, research, merchant (generic navigator; need a long game to reach)
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
- [~] Armies: select with Shift+Enter, move with M (needs barracks + soldiers to verify)
- [x] F1 context help for every screen, Shift+F1 all keys
- [x] Announcements: speed / pause changes, seasons, selection cleared, cursor mode changes, threats (vikings, dragons), merchants, manual saves

## 4. Quality
- [x] Unit tests for Core logic (133 tests, xUnit)
- [x] Bug fixes in the game discovered during inspection (see below)
- [x] README (install, keys, cues, settings, troubleshooting, building), third-party notices, MIT license
- [x] `.gitignore`, `tools/fetch-deps.ps1`, `tools/package.ps1` (mod zip + bundle with BepInEx)
- [x] GitHub repo, push, release

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
- Force-killing the game during development truncated an autosave (dev script now closes the game gracefully).

## Notes / decisions
- Map directions are absolute: North = +Z (top of the map), East = +X. A building's cursor tile is its south west corner.
- The camera follows the keyboard cursor so sighted helpers can see what the player is doing.
- Moving the mouse gives control back to the mouse pointer; any cursor key returns to keyboard control.
- Non-modal panels are reached with F6 so map keys keep working while a building is selected.

## Next ideas
- Verify diplomacy, merchant trading, armies and ships in a long game; add special descriptions where the generic reader is weak.
- Optional spoken coordinates grid / bookmarks for favourite places.
