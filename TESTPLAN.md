# KCAccess test plan

A release is published only when every line here is ticked for it, every bug found is fixed, every language is
complete, and the README and the unit tests are up to date. If testing finds nothing to fix, nothing is released.

A line is ticked only after it was really tried in the running game (or the running setup program), on the code
of that release. Clear all ticks when a new round of testing starts. Each line follows the README, section by section.

How to test is in the README under "Building from source" (debug commands) and in PLAN.md. Remember that the speech
log lists every line sent, also one that later speech cut off: check the order and the priority, not only the text.

Round in progress: for 1.29.0. Game 124r3s, Steam build 26512598 (the game was updated on 10 November 2026).

## Setup program
- [x] Window: status box, language list, both check boxes, all buttons (setup runs elevated: roles read through MSAA, keys sent as window messages)
- [x] Language list switches the window at once; "Use this language for the mod" sets the mod's language
- [x] Install or update on an existing install, with the settings kept
- [x] Install on a game folder without BepInEx
- [x] Silent: `--quiet`, exit codes, `--mod-language th`, `--start-game` (exit 0 and 3 tried; exit 2 needs a PC without Steam)
- [ ] Update available window with the notes, Yes and No
- [x] Uninstall, then install again
- [x] Bundle zip and mod-only zip: contents, extracted over the game, the game starts and speaks

## Keys everywhere
- [x] F1 on every kind of screen names only keys that work there
- [x] Shift+F1: Up, Down, Page Up, Page Down, Enter on a sound, Escape
- [x] Ctrl+Shift+F5 searches for the screen reader again
- [x] Ctrl+Shift+F10 resets the mod and speech comes back
- [x] Ctrl+Shift+F11 keyboard report
- [x] Ctrl+Shift+M sound cues off and on
- [x] Ctrl+Shift+O opens from the map, a panel, a menu and the main menu

## Menus, dialogs and panels
- [x] Up, Down, Tab, Shift+Tab, Home, End
- [x] Enter and Space on a button and a check box
- [x] Left, Right on a slider, a combo box and radio buttons; Page Up, Page Down on a slider
- [x] Letters jump, Ctrl+R reads the screen, F5 repeats with the position, Escape goes back
- [x] Text reader: Up, Down, Page Up, Page Down, Home, End, letters, Ctrl+R, Escape
- [x] Very long text says "Long text, N lines"
- [x] Text fields: Enter edits, typing echoes, Backspace, Enter confirms, Escape cancels, Up, Down, F2 read
- [x] Job priority: rows, Space, Shift+Up, Shift+Down, Left, Right, limits, builders, closed buildings

- [x] Ctrl+C on a button, a text, a text field and the share code; Ctrl+Shift+C copies the screen; checked in the Windows clipboard
- [x] Ctrl+C and Ctrl+Shift+C in the mod's lists: status, notifications, key list, mod settings, text reader
- [x] Ctrl+V in a text field: a fresh field is replaced, after typing it goes in at the caret, spaces and line breaks dropped, an amount field takes digits only, the length limit holds, an empty clipboard is said; Ctrl+C copies the field; with real keys nothing is pasted twice

## Map keys
- [x] Arrows, Shift+Arrows, Ctrl+Arrows
- [x] Enter on a building and an empty tile; Shift+Enter on soldiers, catapult, dragon, ship, cart, villager, cycling
- [x] Ctrl+Shift+Enter adds units to the selection
- [x] I, Shift+I, F5, G, O, T, K, L, Shift+L
- [x] L: Enter jumps, Shift+Enter makes it the target
- [x] Shift+C chop mode with an area
- [x] B, F6, Shift+F6 through every panel group and the toolbar
- [x] Home, End
- [x] Bookmarks: Ctrl+Shift+1, Ctrl+1, Alt+1
- [x] Page Up, Page Down through every scan category; [ and ]; items are right
- [x] N, Ctrl+N, \, Shift+\, Shift+N; an arrow and Escape stop a walk
- [x] V while placing: valid, invalid, tower range, working area, water, fountain
- [x] Shift+Page Up, Shift+Page Down castle levels, repeated building, stopping early
- [x] M: soldiers, catapult, dragon, troop ship, exploration ship, boarding, unloading, a route stop
- [x] Ctrl+E: advisor news, envoy, stopped cart, ship needing orders
- [x] Delete, the panel's Demolish, demolish mode with an area, the keep refused, over 25 buildings confirmation
- [x] Game keys: Space, 1, 2, 3, R, Delete, C, J, Escape; U is blocked and F6 shows a hidden interface

## Selecting a building
- [x] Every building's panel, details and Enter line, including Iron Mine and Large Iron Mine finished
- [x] Farm crop, orchard, harvest waiting, pigs, graves, gate shut, drawbridge raised, sea gate open
- [x] Construction site: waiting for materials, finishing up, paused, no free villager
- [x] Empty tile hint; floating numbers while selected; line and area planning with cost

## Problems, fish, plague, weather
- [x] Problems category, I on a tile, K summary, critical ones announced
- [x] Cannot work reasons; fish on water, Fishing grounds, O counts; sick villagers
- [x] Heavy rain and thunderstorm announced
- [x] Achievements announced, the 11 new ones too, without unlocking them on Steam
- [x] Stacked castle blocks; hurt units; food storage note after building
- [x] Loose resources: announced, tile, I, scanner
- [x] Viking thieves: theft, tile, recovered, got away
- [x] AI requests announced; tithe, feast, festival cost and greyed reasons; mason target button
- [x] Rubble: tile, panel, Rebuild cost, rebuild mode
- [x] Attacks by island, AI landings, other kingdoms' fires and claims, wars, peace, alliances
- [x] The three check boxes each switch only their own messages; Announce notifications off silences them
- [x] Fires: start, spreading not repeated, last one out

## Streaming
- [x] Twitch voting panel, K line, settings, offline vote, countdown, result, nobody voted
- [x] Read chat aloud: votes skipped, long cut, busy summarised
- [x] Speech captions on screen; status file; active effects in K

## News, credits, mods, leaving the game
- [x] News in the text reader; credits as a list
- [x] Mods: local mod compiles and loads, Workshop rows (loaded, restart, incompatible), buttons
- [x] Discord, Twitter, Twitch, news links, shop, open folder open outside the game; "Back in the game" on return
- [x] Language button and its list

## Trading, diplomacy, research
- [x] Merchant: buy, sell, not enough gold, Enter types an amount, special item tab
- [x] Your own merchant and cargo ships
- [x] Visit a kingdom with a real envoy (M on the keep), opinion line
- [x] Conversation: lines, Continue, replies, the envoy's first line heard
- [x] Trade deal: price editor, verdict, Propose, Can you do better
- [x] Feast served, how they liked it
- [x] Train buttons: costs and what is missing, in every training building
- [x] Gift: amounts typed, Make Gift, envoy picks it up, panel, Drop Gift, handing it over
- [x] Research: start, not enough gold, progress, Accept, Cancel, already researched
- [x] Witch: door, request, Send with and without goods, spells, attack-only spells, Refuse

## Routes
- [x] Cargo ship and cart: Shift+Enter, Route editing, Enter on the map, stop lines
- [x] M moves a stop, add stops, Remove stop, pick up and drop off boxes, Shift+Enter amounts

## Water
- [x] Placing hints on every tile, V nearest, fountain irrigated and not irrigated
- [x] Built messages: works, no wheel pumps, too weak, not connected, a line of pieces said once
- [x] Panel problem lines; Dragon Roost as part of the chain; bath house

- [x] Forecast while placing: water will reach it, too weak here, no water wheel pumps, not linked; it matches what the built piece gets
- [x] Game 124r3s: a reservoir beside the last pipe with water works, one beside the first dry pipe does not
- [x] A wheel without all its workers: dry pieces say "no water wheel pumps yet" on Enter and in the panel, not the game's "Not enough water power"

## Islands
- [x] Islands category, ship in reach, Settle, Outpost placing, outpost built, naming, unnamed island messages
- [x] Train Settlers, boarding, unloading, Settle turns them into villagers, troop ship panel

## Colour maps
- [x] Happiness: bonus on open ground, house happiness
- [x] Health on houses; building integrity; Ctrl+Arrows stop on a change; Escape turns off; K explanation

## Dragons
- [x] Add gold pile, not enough gold, needs, slot lines with explanations, Release confirmation
- [x] Tile names the dragon, Shift+Enter selects, army panel, Return To Roost, bond rule, M

## Creative mode
- [x] Menu check boxes, every brush, team buttons, Escape turns a brush off
- [x] Map editor brushes on map setup, brush size

## Build menu
- [x] Left, Right, Tab, Up, Down, Home, End, I, F5, letters, Enter, Space, Escape, B
- [x] Cemeteries, statues and parks lists; locked reasons; fertility while holding a farm
- [x] Large Quarry and Large Iron Mine rule and nearest spot; south west corner message

## New game and first years
- [x] New game flow with difficulty, name, banners, custom banner, map setup, AI kingdoms, I and Shift+I on a kingdom
- [x] Every map size, type and rivers; New Map; Ctrl+M exploring
- [x] A real standard game by the walkthrough: keep, roads, homes, chop, farm, granary, quarry, treasury, tax
- [x] Advisors, house panel, Enter on a resident
- [x] Saving and "Game saved"; Saving failed path

## Extra Large
- [x] Generate, AI kingdoms, explore, keep, scan, walk, save, load
- [x] A long game: many years with a built city, no errors, speed stays fine

## Kingdom share
The game's server cannot delete an upload, so these run against the private test server (`tools/share-test-server.py`, `sharelocal`); nothing goes online.
- [x] Share: code read, Take Picture, Accept Picture, Upload
- [x] Visit: code, Check Code, kingdom summary, Visit, back to your own save

## Saved games
- [x] Delete on a save, confirmation starts on Cancel, "Save deleted"

## Controller
Simulated with `pad` and `padcount`: every press goes through the real mapping and delivery, only the physical pad is left out.
- [x] Controller connected and disconnected
- [x] Every button on every layer

## Mod settings
- [x] Every line of every group: Language, Speech, Sound, Volume of each sound, Map, Keyboard and logs, Streaming
- [x] Mod keys: change, refused key, Delete restores, F1 and Shift+F1 follow
- [x] Reset everything; settings saved to the file at once

## Languages
- [x] Every language chosen in Mod settings: no errors, spoken texts in that language
- [x] Game's language list: every game language switches, Automatic follows; mod-only language marked
- [x] Language file reloaded on save; a broken translation ignored and logged
- [x] Thai game texts read in the game, new texts included; Game texts in the mod language off and on
- [x] Updates keep changed lines

## Sound cues
- [x] Every cue plays from the key list and from Mod settings; volumes act

## Game bugs fixed by the mod
- [x] Key bindings with modifiers work; Ctrl+C does not switch to creative mode
- [x] A game mod compiles with BepInEx installed

## When the game updates
- [x] On the tested game nothing extra; on a changed game: repair, notice, Lost count, once only
- [x] A broken part keeps the rest running; a hook error is kept from the game; plain list for unknown screens

- [x] On a new game version of this round: decompiled and compared with the last one, every change looked at, the mod's self-check reports nothing adapted or missing
- [x] Speed: `timing` on a built city and on an Extra Large map, no part of the mod stands out; `findcost` shows what the mod no longer does every 30 seconds

## Troubleshooting
- [x] Steam overlay opened and Back in the game; keys after the overlay (Steam did not open it on the test PC: Steam's report simulated with `overlay on|off`)
- [x] The mouse takes over and a cursor key returns control
- [x] Windows key fallback delivers a missed key (delivery tried with `deliver`; a real missed key could not be produced)

## Language and documents, for every release
- [x] Every language file complete: unit tests pass, nothing left in English that should not be
- [x] Game texts: Thai complete, the game's own languages checked for new terms
- [x] README and PLAN.md up to date; README numbers right
- [x] Unit tests up to date and passing; infection check clean
