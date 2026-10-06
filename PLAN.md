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
- [~] v1.11.0 audit: fixed a false "Game saved" when the overwrite confirmation opened, a text edit whose window closed keeping every key, the window search running every frame on the map, stale announcement state / envoys / weather / beacon after loading a save, map exploration staying on when the map setup screen was left, the build menu ignoring a rebound build menu key, help texts and hints naming fixed letters instead of the player's keys, speech found by re-detection not being used when Prism started without a backend, M not attacking the building under the cursor while the mouse rests over a panel, and a game bug (holding End made every AI kingdom plan a farm at the cursor). New: siege catapults (yours and vikings'), your dragons vs wild dragons, transport carts and hunting wolves in tiles, scanner and selection; Control Shift Enter adds soldiers to the selection; ship and cart routes edited by keyboard (stops read with place and cargo, M moves a stop to the cursor, Shift Enter reopens cargo amounts); army panel tabs and unit list named; diplomacy price sliders with the other kingdom's mood; tower ranges; map editor paint feedback; the build menu says archer towers go on top of castle blocks. Verified in the game: save overwrite (no false "Game saved"), target cleared after loading, threats list fires and wolf dens, B closes the build menu, F1 per screen, V gives the tower range on a castle block, no errors in the log. Not yet seen in the game (no ships, carts or soldiers in the test kingdom): routes, army panel tabs, diplomacy sliders, map editor painting
- [x] v1.10.0 (written in a Claude Code cloud session on branch feature/streaming, built and tested in the game locally): streaming. Twitch chat voting (StreamerUI) as an F6 panel group ("Twitch vote" / "Twitch voting" with the channel field, interval, vote count and option check boxes), channel connect / failure announced, every new vote announced with the numbers viewers type, K line with vote counts and time left (Enter opens the settings), optional 10-seconds countdown with the leader, result banner titled "Twitch viewers chose X, picked by Y", "Nobody voted"; optional reading of Twitch chat (Core.ChatFilter: votes skipped, 200 characters, 3 messages per 10 s then "and N more chat messages"); optional on-screen speech captions for viewers (Core.Captions, IMGUI, last 3 lines fading after 6 s); optional OBS status file BepInEx/kcaccess_stream.txt every 2 s. Also: K lists active timed effects (StatusEffectsManager: Twitch results, witch spells, Chamber of War) that were icons only; effect banners no longer speak their title twice (notification + banner); F1 in a panel group can have its own help (Panel.Twitch). Not yet tested in the game
- [x] v1.9.0: controller support through Rewired's gamepad template (Xbox, PlayStation and others): Core.ControllerMap layers (none / LT / RT / both) map every button to a mod key or binding, delivered like typed keys (mod and game hotkeys), held directions repeat, connect / disconnect announced, the game's console-controller mode kept off while on, setting to turn it off; "pad" test command (no hardware on the dev PC). Main menu extras: language picker labelled with focus moved into its list; Discord / Twitter / Twitch / news / shop / feedback document / open folder announce that they leave the game; "Back in the game" when focus returns. Fixed: a delivered Escape (controller or key fallback) that the mod used could still trigger the game a frame later (pause menu opened after closing the key list); pause / settings Escape only acts when that menu has the keyboard
- [x] v1.8.0: second decompile pass for visual-only information: thought bubbles (ThoughtBubbleSystem: hunger, starving, plague, wages, homeless, rats, dead bodies, buildings that cannot work) in a Problems scanner category, tile descriptions, K summary and critical announcements; FishSystem fish per water tile in tiles, survey and a Fishing grounds category; villager sickness; heavy rain / thunderstorm announcements; achievements (Assets.Achievements.Try) announced when newly unlocked; stacked castle blocks "stacked N high". No quests or missions exist in the game; remaining unhandled classes are VR, console, Twitch, minimap and camera-only
- [x] v1.7.0: full decompile scan (723 files) by system: raids (RaiderSystem), dragons, ogres, fire, wolves, witch, merchants, envoys, tourism, rubble, ships, carts, dragon nest (inside the building panel), unit dragons (inside the unit panel). Gaps fixed: years until the next viking raid / dragon attack (the on-screen timers) in K, current raid size computed instead of a stale counter; kingdom share (tourism) popups announced with their code and focused. Mod settings: Page Up / Page Down between groups with first / last announced; new "say the position in menus and lists" check box for every navigator, list menu and the build menu
- [x] v1.6.0: mod settings menu (Ctrl+Shift+O): speech / sound / map / keyboard options, cue volume, walk speed, season announcements, hints; every mod key rebindable with conflict checks against the mod's fixed keys, other mod keys (by scope) and the game's key bindings, Delete restores one key, Reset everything; help, key list and spoken hints use the current keys (KeyHelp.Resolve). Game code audit: every GameUI window is handled; the diplomacy notification banner was silent (now announced), the share and record overlays are visual only; the game's click-only exclamation marks (advisor news, envoy waiting at the keep, stopped cart, ship, witch hut) are now announced, listed in the scanner's Alerts category and answered with the Alert key (Ctrl+E); envoys on the way are announced
- [x] v1.5.1: Alt+F4 silenced the mod for the rest of the session: the game cancels quitting in World.OnApplicationQuit (Application.CancelQuit, "Exit game" dialog) but the mod closed the screen reader connection in its own OnApplicationQuit. No shutdown there any more; Ctrl+Shift+F10 and Ctrl+Shift+F5 reconnect even a closed connection; the keyboard report includes the speech backend; key list says when there are no more groups
- [x] v1.5.0: F1 names only the keys that work on the current screen (built from the screen's controls: sliders / option lists / text fields / letters / save slots / panels) and on the map from the current state (placing, tool mode, brush, selection, soldiers, target, walking, no keep, setup exploration); Shift+F1 opens a key list (line by line, groups with Page Up / Page Down) with sound cue previews; demolishing tested (Delete, panel button, demolish mode area) and announced ("Demolished Road, 2 pieces", "Keep cannot be demolished"); fixed: Escape used by the mod in the pause / settings menus (closing a mod list, cancelling a text edit) also resumed the game or reverted settings; pause help said "Resume" for the Return button; building Health bars were offered as adjustable sliders; "Escape goes back" no longer claimed on the main menu
- [x] v1.4.6: audit of README keys in game (bookmarks, bookmark target, Shift+\, K reports, J, text field F2, Shift+F1, Ctrl+Shift+M, G, End, Shift+I, Ctrl+arrows, Shift+L) and a Large Island map (132 by 132: size, survey, scanner, walking all from the real map). Fixes: brackets explain that they only set the target (first 3 times); exploration on island maps starts on land; "no route" says when the cursor is on water; fertility no longer read on rock / stone / iron; area marking reached the map edge one tile short; raid hint updated for the target keys; ticking counters without names no longer spoken after clicks (numbers beside the pressed control still are)
- [x] v1.4.5: user log: closing a panel with its own Close button left the mod "inside" an empty panel (all keys silent until Escape / F6) because the panel check shared a timer with the per-frame dialog check and never ran; own timer now. U (hide interface) made every panel unreadable and turned the first Escape into "show interface"; U is blocked with an explanation, F6 re-shows a hidden interface
- [x] v1.4.4: "Shift+Tab freezes the mod" = Steam overlay (Shift+Tab) taking the keyboard. Overlay open / close detected through Steamworks and announced; mod ignores keys while it is open; key fallback suspended, never delivers Shift+Tab, no longer uses the unreliable "pressed since last poll" bit (phantom letter bursts) and tracks held keys while unfocused so Alt+Tab / Shift+Tab releases are not new presses
- [x] v1.4.3: friend's Windows 11 log (build 26200, US layout, 1.4.1) showed arrows / Enter / F6 arriving but Escape, Space, 1 2 3 and L never reaching Unity, and no stuck modifiers. Added a Windows key-state fallback (GetAsyncKeyState, held and tapped) that delivers missed presses to the mod and forwards matching game hotkeys (Escape menu, pause, speed); stale forwards dropped after 3 frames; WindowsKeyFallback config. Enter on plain text says "text, not a button". README: Steam Input / overlay advice
- [x] v1.4.2: Ctrl+Shift+F10 resets the mod (windows, panels, menus, edits, key capture, game hotkeys) without quitting; key log on by default for bug reports. Pause menu "Escape, Enter, arrows silent" not reproducible on the dev PC with real scan-code keys; waiting for the reporter's log
- [x] v1.4.1 hotfix: delete saved games (Delete key, confirmation starts on Cancel); stuck-modifier protection via the Windows key state (Windows 11 / NVDA Remote report: Escape, Space, 1 2 3 and L dead); keyboard guard (IME off, stray focused text fields released or adopted as edits, game hotkeys re-enabled); Ctrl+Shift+F11 keyboard report and LogKeys option
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
| 3 | `AIKingdom.Update` adds an `Intention_Test` ("build a farm at the pointer") for every AI kingdom each frame **End** is held (developer test outside any debug check); End is the mod's jump-to-selection key and the pointer is the keyboard cursor | Prefix on `Intention_Test.Tick` marks it done without doing anything |

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

