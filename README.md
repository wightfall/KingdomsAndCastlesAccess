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
* The mod speaks 19 languages, and the game's own texts (menus, windows, tooltips, buildings, events, places,
  diplomacy dialogue) can be translated for languages the game lacks: **Thai** is shipped complete, see
  [Translating the game's own texts](#translating-the-games-own-texts).
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
Tab moves on to the language list, the two check boxes and the buttons, and questions are Yes / No
message boxes.

* **Language** (Alt+L): the setup speaks the mod's language (your setting in the mod, otherwise the Windows
  display language). The list has every language the mod has, including languages you added yourself in the
  game folder; choosing another one switches the window to it at once.
* **Use this language for the mod in the game** (off by default): after installing, the chosen language
  becomes the mod's language (the same as choosing it in the mod settings, Ctrl+Shift+O).
* **Start Kingdoms and Castles when setup finishes** (off by default): the game starts through Steam when
  you close the "Installation complete" message.

Silent install: `KCAccess-Setup-vX.Y.Z.exe --quiet` (exit code 0 = installed, 2 = game not found,
3 = game running). Add `--mod-language th` (a language file name, `en` or `auto`) to set the mod's language
and `--start-game` to start the game afterwards.

If your antivirus blocks or removes the setup program (Malwarebytes' machine-learning check reports new,
unsigned programs as "MachineLearning/Anomalous"), use the zip below instead: it contains the same mod.

### Alternative: the bundle zip

1. Download `KCAccess-vX.Y.Z-with-BepInEx.zip` from the
   [releases page](https://github.com/wightfall/KingdomsAndCastlesAccess/releases).
2. Extract **everything** into the game folder, normally
   `C:\Program Files (x86)\Steam\steamapps\common\Kingdoms and Castles`, so that `winhttp.dll`
   and the `BepInEx` folder sit next to `KingdomsAndCastles.exe`.
3. Start the game. After a few seconds you hear "Main menu".

### If you already use BepInEx 5.4

Download `KCAccess-vX.Y.Z.zip` and extract it into the game folder. It only contains
`BepInEx\plugins\KCAccess\` (`KCAccess.dll`, `KCAccess.Core.dll`, `prism.dll`, the `Languages` folder,
licenses).

### Uninstall

Run KCAccess-Updater.exe and choose **Uninstall**, or delete `BepInEx\plugins\KCAccess`. To remove BepInEx completely also delete `winhttp.dll`,
`doorstop_config.ini`, `.doorstop_version` and the `BepInEx` folder.

## Keys

Press **F1** at any time for help about the current screen: what it is and only the keys that work there right now (for example, Left and Right are only mentioned when the screen has sliders or option lists, Delete only when a building is selected). **Shift+F1** opens the key list: every key, one line at a time, grouped, with previews of every sound cue at the end.

### Everywhere

| Key | Action |
|-----|--------|
| F1 | Help for the current screen, naming only the keys that work there |
| Shift+F1 | Key list: Up / Down read one key at a time, Page Up / Page Down jump between groups, Enter on a sound plays it, Escape closes |
| Ctrl+Shift+F5 | Search for the screen reader again (if you started it after the game) |
| Ctrl+Shift+F10 | Reset the mod and reconnect the screen reader if it ever stops speaking or responding (no need to quit the game) |
| Ctrl+Shift+F11 | Keyboard report: what owns the keyboard, held modifiers, keyboard layout (also written to the log) |
| Ctrl+Shift+M | Sound cues on / off |
| Ctrl+Shift+O | Mod settings: options, sound volume, walking speed and changing the mod's keys |

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

**Job priority window** (J): each job is one row – "Priority 2, Farmers, 3 workers, 8 job positions";
with a limit you set, "limited to 5 of 8 job positions" (the number in brackets on screen, which the game
explains only when the mouse rests on it); builders say "up to 3 builders"; jobs in closed buildings are
counted at the end. Space turns the job on or off, **Shift+Up / Shift+Down** change its priority (this
replaces the mouse drag), Left / Right change the limit, from 0 up to the job positions there are.

### Map

| Key | Action |
|-----|--------|
| Arrow keys | Move the cursor one tile (north is up) |
| Shift+Arrows | Move 5 tiles |
| Ctrl+Arrows | Jump to the next tile that is different (edge of a forest, a building, water …) |
| Enter | Select the building or tile under the cursor; while placing: build; in chop / demolish mode: apply |
| Shift+Enter | While placing roads, walls or fields: set the start point, then Enter at the end point builds the whole line or area. In chop / demolish mode: mark one corner of an area. Otherwise: select soldiers, a siege catapult, your dragon, a ship (merchant ships open the trade window; troop and exploration ships open their panel), a transport cart or a villager on the tile (press again to cycle) |
| Ctrl+Shift+Enter | Add the soldiers (siege catapults, dragons) on the tile to the selection, so several armies move together with M |
| I | Everything about the tile (terrain, fertility, road coverage, workers …) |
| Shift+I | Details of the selected building |
| F5 | Repeat the tile description |
| G | Cursor coordinates and map size |
| O | Area survey: land, open, fertile, forest, stone, iron and water tiles within 6 tiles, plus the nearest stone, iron or water when none is close |
| Shift+C | Chop trees mode on / off (Shift+Enter one corner, Enter the other) |
| B | Build menu |
| F6 / Shift+F6 | Move into the open panels: selected building or tile → kingdom overview (population, the happiness, health and building integrity map buttons, tax buttons, resources) → toolbar (speed, chop / demolish / rebuild modes, menu). F6 at the end, or Escape, returns to the map |
| K | Kingdom status list (happiness, health, building integrity …), including years until the next viking raid and dragon attack, a summary of the problems the game shows as thought bubbles, active timed effects and the running Twitch vote (Enter on a line reads the game's detailed report; on the Twitch line it opens the voting settings) |
| T | Year, season, weather and game speed |
| L | Notification history; Enter jumps to where it happened, Shift+Enter makes it the navigation target |
| Shift+L | Repeat the last notification |
| Home | Jump to your keep |
| End | Jump to the selected building |
| Ctrl+Shift+1 … 9 | Store a bookmark at the cursor (saved per kingdom) |
| Ctrl+1 … 9 | Jump to a bookmark |
| Page Up / Page Down | Choose a scan category: your buildings, construction sites, your soldiers and ships (also siege catapults, your dragons and transport carts), alerts, problems (hungry, starving, plague, unpaid wages, homeless, rats, unburied dead, buildings that cannot work), threats (enemy armies and ships, viking siege catapults, wild dragons, ogres, fires, hunting wolves, wolf dens), stone, iron, open fertile land, forests, fresh water, fishing grounds, foreign kingdoms, islands (every island with its owner, size and whether it is explored), special places |
| [ and ] | Choose the previous / next item of that category as the navigation target, nearest first (the list refreshes itself) |
| N | Auto-walk the cursor to the target along a route villagers can walk (ends next to it if the target itself is blocked) |
| Ctrl+N | Walk to the target in a straight line, over water and buildings |
| \ | Jump straight to the target |
| Shift+\ | Say where the target is from the cursor |
| Shift+N | Target beacon on / off: pings panned to the target's side, higher pitch north, lower south, faster when closer, a chime on arrival |
| Alt+1 … 9 | Make a bookmark the navigation target |
| Any arrow / Escape | Stop an auto-walk |
| V | While placing: why the spot is (in)valid; for towers also their shooting range, which grows with every castle level they stand on |
| Shift+Page Up / Shift+Page Down | Castle blocks (stone and wooden walls): build more / fewer levels on top of each other with each Enter or Shift+Enter line (1 to 10). The mod repeats the placement at the same tiles until the levels are built or the game refuses (for example wooden walls reach their height limit), then says how many levels and pieces were built. Enter or Escape stops early |
| M | Send the selected soldiers (also siege catapults, your dragons, troop and exploration ships) to the cursor. With soldiers, settlers or an envoy selected and the cursor on your troop ship: board it. With a loaded troop ship selected and the cursor on land: unload there. On a route stop in a ship or cart panel: move the stop to the cursor |
| Ctrl+E | Respond to the nearest alert (the game's exclamation marks): advisor news at the keep, a foreign envoy waiting to speak, a stopped transport cart, a ship needing orders |

**Demolishing:** select a building and press Delete (or the panel's Demolish button), or turn on
demolish mode in the toolbar (F6) and mark an area with Shift+Enter and Enter. The mod says what was
demolished ("Demolished Road, 2 pieces"), or that a building such as the keep cannot be demolished.
More than 25 buildings at once ask for confirmation.

The game's own keys keep working: **Space** pause, **1 2 3** speed, **R** rotate while placing,
**Delete** demolish the selected building, **C** chop trees on the selected tile, **J** job priority,
**Escape** cancel / close / pause menu, **WASD QE** camera. **U** (hide the game interface) is
blocked by the mod, because hidden panels cannot be read; if the interface was hidden anyway, F6 shows it again.

### Problems, fish, plague and weather

The game warns with small pictures over villagers and buildings (thought bubbles). The mod reads them:
the **Problems** scanner category lists each one ("villager at Farm: hungry", "Hovel: problems: 2 hungry"),
I on a tile says them, K gives a summary, and critical ones (starving, bad plague, unpaid soldiers,
unburied bodies, rats) are announced when they appear. Water tiles say how many fish swim there, the
**Fishing grounds** category finds them for fishing huts, and O counts them. Sick villagers are mentioned
on their tile and in the villager description. Heavy rain (farms may flood) and thunderstorms (fires) are
announced, as are newly unlocked achievements. Stacked castle blocks are read as "stacked 4 high".

**Fires** are announced when they break out in your kingdom ("Fire at Hovel", with the direction; the fire
spreading to the next tiles is not repeated) and when the last one is out; the game itself only reports a
building that burnt down. Fires are in the Threats category and in the notification history (L).

### Streaming

For players who stream on Twitch or record videos. Everything is off by default except the Twitch countdown;
change it in the mod settings (Ctrl+Shift+O, group **Streaming**).

* **Twitch chat voting** (the game's Settings: Enable Twitch Chat Voting). Viewers vote in chat for effects
  such as a good harvest or a plague. The voting panel is a panel group: F6 from the map reaches **Twitch
  vote** (the current options) or, while its settings are open, **Twitch voting** (channel name field,
  vote interval, number of options and a check box for every vote option). K has a **Twitch vote** line with
  the vote counts and the time left; Enter on it opens or closes the settings and moves you into them.
  Escape closes them again. After you type your channel name you hear "Connected to the Twitch chat" or
  that the channel could not be joined.
* Every new vote is announced with the numbers viewers type: "New Twitch vote, ends in 2 minutes: 4, Bountiful
  harvest; 5, Plague; 6, Rain". Single votes are not spoken (K has the counts). **Announce when 10 seconds are
  left in a Twitch vote** (on by default) says once per vote how much time is left and which option leads.
  The result banner starts with "Twitch viewers chose Plague, picked by name"; "Nobody voted" when no one did.
* **Read Twitch chat aloud** (off): chat messages are spoken as "name: message" after whatever is being said.
  Votes like "#2" are skipped, long messages are cut at 200 characters, and when chat is busy at most 3
  messages per 10 seconds are spoken, followed by "and 7 more chat messages".
* **Show speech captions on screen** (off): your viewers cannot hear your screen reader, so the last 3 spoken
  lines are shown at the bottom of the screen in large white text on a dark box; each fades after 6 seconds.
* **Write a status file for streaming overlays** (off): every 2 seconds the mod writes
  `BepInEx\kcaccess_stream.txt` with the kingdom name, year and season, population, gold, the last
  notification and the running Twitch vote. In OBS add a Text source, tick "Read from file" and choose that file.
* K also lists **active effects** with their time left (Twitch vote results, witch blessings and curses,
  Chamber of War orders), which the game shows only as icons.

### Leaving the game

The main menu's Discord, Twitter and Twitch buttons, news links, the shop and "open folder" buttons open your
web browser or File Explorer. The mod says where you are going; Alt+Tab brings you back, and the mod then says
"Back in the game" and where you are. The language button reads "Language: English" and opens a list with
focus on the current language; Enter switches language, Escape closes the list.

### Trading, diplomacy and research

* **Store and workshop links:** "DLC Available", "Wishlist our next game!" and workshop pages open in your web
  browser instead of the Steam overlay, which screen readers cannot read.
* **Merchants:** when "A merchant ship has arrived" is announced, find it with the scanner (Your soldiers
  and ships), press Shift+Enter on it and then F6. Each trade line reads "Wood: 2 gold each, 40 available,
  buy 5, costs 10 gold"; Left / Right change the amount by 1, Page Up / Page Down by 10, Enter lets you
  type it. Then choose the complete-transaction button.
* **Diplomacy:** every line an envoy or ruler says is read aloud, followed by the number of replies;
  focus jumps to the first reply. Up / Down choose, Enter answers. To visit a kingdom, select your envoy
  (Shift+Enter), put the cursor on the other kingdom's keep and press M: the visit opens when the envoy
  arrives, wherever the camera is. "Opinion of you: neutral, 80 percent of the way to favorable" tells their
  opinion level and how close the next one is; a trade deal needs at least favorable.
* **Trade deal (Let's talk trade):** the price editor explains itself and starts on the first resource. Each
  slider reads its resource, the price and how they feel about it ("Wood price, slider, 3, they are
  neutral"); after a change you also hear the effect of the whole offer ("Overall they like these prices,
  opinion plus 10"), and so does the Propose prices button. Their own price list reads "Wood: 2 gold", and
  when you ask "Can you do better?" the prices they lower are announced ("Armaments 4 gold, was 5").
* **Feasts:** the food buttons say the feast and its cost ("Apple feast, costs 50 apples"); afterwards you
  hear how they liked it (their favourite food, one they dislike, or some of it).
* **Army panel (F6 with soldiers selected):** the unit type tabs and the list of selected units are named
  ("Siege catapults, 2, tab"); Enter on a unit shows its details. **Settle** and **Disband** say how many
  villagers the units became.
* **Training:** the train buttons of the barracks, archer school, siege workshop, Hall of Diplomacy and the
  keep (settlers) say what one squad costs and what is missing ("Each squad costs 17 villagers (have 5),
  gold 34, armaments 17 (have 0)"), which the game shows only when hovering.
* **Gifts (Hall of Diplomacy):** Make Gift opens a row of amount fields and starts on the first ("Wheat, edit,
  0"); Enter, type the amount, Enter. Then the second Make Gift. Send an envoy to the hall with M: "Your envoy
  picked up the gift: gold 10". The envoy's panel then shows the gift ("Gold, 10") and Drop Gift; take the
  envoy to a foreign keep to hand it over.
* **Research (Great Library):** select the library, F6, Research. Each technology reads its effect and
  gold cost, or "already researched"; Enter starts it, or tells you how much gold is missing. While research
  runs the window reads progress and years remaining.

### Ship and cart routes

Transport ships and transport carts carry goods along a route of stops, which the game edits by dragging
coloured markers. With the mod:

1. Find your ship or cart (scanner category "Your soldiers and ships"), press **Shift+Enter** on it. Its route
   panel opens and you hear "Route editing".
2. Move the cursor to the dock, stockpile, market or tile where a stop should be. **Enter** on the map says whether
   a stop can go there.
3. **F6** into the route panel. Each stop reads "Stop 2 of 3: Northport, Dock, 4 tiles east, pick up wood 20,
   drop off nothing". Press **M** on a stop to move it to the cursor. The add buttons at the start and end of the
   list add stops, "Remove stop" deletes one, and the pick up / drop off check boxes choose the cargo; on a checked
   one, **Shift+Enter** opens the amounts again for changes.

### Islands, exploration ships and settlers

On maps with more than one island you can claim new islands:

1. **Find islands:** the scanner category **Islands** lists every island ("unclaimed island, 1484 tiles,
   unexplored, 43 tiles north east"); [ and ] choose one, \ jumps there.
2. **Exploration Ship** (build menu, Maritime, next to water): select it with Shift+Enter (its panel opens),
   put the cursor on or near the unclaimed island and press M. "Your exploration ship is in reach of unclaimed
   land" tells when it is close enough.
3. F6 into its panel and press **Settle**: you place an **Outpost**. You hear how far it may be from the ship and
   the nearest spot where the whole outpost fits ("Nearest: 4 tiles west, 1 south"); V and the "too far" reason
   repeat it. The outpost claims the island; name it in the kingdom overview (Island name). Messages about an
   island without a name start with "Unnamed island".
4. **Settlers:** once you have an outpost, the keep's panel can **Train Settlers**. Settlers (or soldiers) go
   to another island on a **troop ship**: select them with Shift+Enter, put the cursor on your troop ship and
   press M ("Boarding your troop ship", then "Your settlers boarded your troop ship, 1 of 3 places taken. The
   ship is selected."). Put the cursor on land at the other island and press M ("Unloading at 108, 109"); when
   they are ashore you hear "Your troop ship unloaded your settlers. They are selected." F6 and **Settle** turn
   them into villagers of that island ("Settled: 16 new villagers").
5. A troop ship's panel lists who is on board, each with a check box "Stays on the ship when it unloads".

### Happiness, health and building integrity maps

The kingdom overview (F6) has a button for each of the game's three colour maps: **Happiness**, **Health** and
**Building integrity**. Enter turns a map on (the others go off) and the mod says what it shows. While a map
is on, moving the cursor adds its value to each tile:

* **Happiness:** "happiness bonus 12" on open ground (what churches, gardens, statues and taverns around give a
  house built there) and "house happiness 56" on houses.
* **Health:** "residents' health 74" on houses.
* **Building integrity:** "integrity 80 percent" on buildings that wear down and need repairs from masons.

Ctrl+Arrows stop where the value changes, and **Escape** on the map turns the map off ("Happiness map off").
The K status list has the kingdom's building integrity next to happiness and health; Enter reads the game's
explanation.

### Dragons (Dragon Roost)

A **Dragon Roost** (Advanced town) houses two adult dragons and their young. In its panel (select it, F6) each slot
has an **Add a gold pile** button (1000 gold; it says when you do not have enough): a slot with a gold pile waits
for the next dragon that flies in ("A dragon will try nesting here during the next invasion in 11 years"). The
needs (pork or fish, aqueduct water, dragon doctor, dragon bath, obstacle course) are read with what they give.
A dragon's slot reads its name (an edit field; the rename button next to it), status, age, **health**,
**happiness** and **bond**, each followed by the game's explanation (what raises or lowers it, warnings such as
starving); **Release** asks for confirmation, because a released dragon attacks the kingdom.

Your dragons resting on the roost (or at a bath, doctor or obstacle course) are named in the tile description
("your dragon Smaug, resting") and **Shift+Enter** on the roost selects one; F6 then opens its army panel with
status, energy, health, happiness, bond and **Return To Roost**. A dragon follows orders only once its bond is
complete: until then selecting it says so; afterwards **M** sends it to the cursor like soldiers.

### Creative mode

F6 reaches **Creative mode options**: press the Creative Mode button to open the menu with its check
boxes (Build For Free, Hide Fog of War, Viking Attacks …) and brushes (spawn knights, archers, vikings,
dragons, resource stacks, fire, trees, removal tool). Picking a brush returns you to the map; **Enter**
applies it at the cursor, **Escape** turns the brush off. The team buttons choose who spawned units belong
to (your kingdom or a foreign island) and say the new team when pressed. On the map setup screen the map editor
brushes work the same way: pick a brush, press Ctrl+M, move the cursor and press Enter to paint; you hear
what was painted and the brush size ("Painted very fertile land, brush size 2").

### Build menu (B)

| Key | Action |
|-----|--------|
| Left / Right or Tab | Previous / next category (Castle, Town, Advanced town, Food, Industry, Maritime) |
| Up / Down, Home / End | Choose a building: name, cost (what you are missing), size, available or why not |
| I or F5 | Full description, workers, wages and the range of towers |
| Letters | Jump to a building by name |
| Enter or Space | Pick up the building (archer towers and ballistas go on top of castle blocks); cemeteries, statues and parks open their own list |
| Escape or B (your build menu key) | Close |

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
8. Save from the pause menu (Escape, Save, Make New Save); "Game saved" confirms it once the file is written
   ("Saving failed" when the game could not write it).

### Kingdom share

The toolbar's **Share** button (and **Kingdom Share** on the main menu) uploads your kingdom or visits another
player's. Share Kingdom: your share code is read in the code field; **Take Picture** switches to picture mode
(said; **Accept Picture** takes the current camera view), then **Upload**, and the code is read in the result.
Visit Kingdom: type the code in the code field, **Check Code**: you hear "Share code found", then the kingdom's
summary once it is downloaded, and **Visit** loads it ("Kingdom loaded"). To return to your own kingdom, load your
save.

### Saved games

On the Load and Save screens, **Delete** on a saved game deletes it. The confirmation starts on
Cancel; choose "It's toast" to delete. "Save deleted" confirms it.

## Playing with a controller

Xbox, PlayStation and most other gamepads work (through the game's Rewired input). Every button does the
same as a keyboard key, so all of the mod works with a controller; the mod says "Controller connected" when
one is plugged in. Hold a trigger to switch layer. (PlayStation: A is cross, B circle, X square, Y triangle,
LB/RB L1/R1, LT/RT L2/R2, Back Share, Start Options.)

| Button | Without triggers | Hold LT | Hold RT | Hold both triggers |
|--------|------------------|---------|---------|--------------------|
| D-pad / left stick | Arrow keys (held = repeat) | Jump to the next change (Ctrl+arrows) | Move 5 tiles | Left normal speed, up fast, right fastest, down pause |
| A | Enter | Walk to the target (N) | Build menu (B) | Chop trees mode (Shift+C) |
| B | Escape | Jump to the target (\) | Kingdom status (K) | Send soldiers (M) |
| X | Shift+Enter | Beacon (Shift+N) | Notifications (L) | Demolish the selected building (Delete) |
| Y | Map: describe the tile (I); menus: repeat (F5) | Where is the target (Shift+\) | Survey (O) | Why the placing spot is (in)valid (V) |
| LB / RB | Map: scan category; lists: Page Up / Down | Previous / next target ([ ]) | Map: keep / selected building; menus: first / last | |
| Back / View | F6 (panels) | Shift+F6 | Respond to the nearest alert (Ctrl+E) | Reset the mod |
| Start / Menu | F1 (help) | Pause / resume (Space) | Key list (Shift+F1) | Mod settings |
| Left stick click | Map: jump to the keep; menus: first item | Walk in a straight line (Ctrl+N) | Coordinates (G) | |
| Right stick click | Map: date (T); menus: read the screen | | Repeat the last notification | |

Buttons follow your key bindings: if you change a mod key in the settings, its controller button does the new
key's action. The layout is also in the Shift+F1 key list (group "Controller"). Typing names needs a keyboard.
The game's own console-controller mode is kept off while this is on; turn "Controller support" off in the mod
settings to use the game's mode instead.

## Mod settings (Ctrl+Shift+O)

Everything about the mod can be changed in the game, from any screen. Up and Down move, Page Up and
Page Down jump between the groups (you hear when you reach the first or last group):

* **Language:** the language the mod speaks (Left / Right: Automatic, English and every language file, see
  [Languages](#languages)). It changes right away and is said in the new language. Below it, **game texts in
  the mod language too** (on by default): the game's own texts also appear in the mod's language where that
  language has a game text file (see [Translating the game's own texts](#translating-the-games-own-texts)).
* **Speech:** announce notifications, announce seasons and new years, detailed tile descriptions while
  moving, speak coordinates after each tile, hints for new players, and **say the position in menus
  and lists** ("Load, 4 of 19"; off by default, F5 always says it).
* **Sound:** sound cues on or off, sound cue volume (Left / Right in steps of 10 percent; you hear the new
  volume).
* **Map:** camera follows the cursor, auto-walk speed (slow, normal, fast).
* **Keyboard and logs:** deliver key presses the game missed, key log, speech log.
* **Streaming:** Twitch vote countdown, read Twitch chat aloud, speech captions on screen, status file for
  streaming overlays (see [Streaming](#streaming)).
* **Mod keys:** every mod key is listed with its current key. Enter waits for a new key (hold Control,
  Shift or Alt with it if you like; Escape cancels). A key that the mod already uses, or that is one of the
  game's own keys, is refused and you are told what uses it, for example "J cannot be used: it is the
  game's key for job priority". Delete restores that key's default. F1 help and the Shift+F1 key list
  always name your current keys. Arrows, Enter, Escape, Tab, F1, F5, F6 and the bookmark keys are fixed.
* **Reset everything** (press Enter twice) restores all settings and keys if something feels wrong.

Settings are saved in `BepInEx\config\kcaccess.screenreader.cfg` right away.

## Languages

Everything the mod itself says (help, key list, tile descriptions, menus, settings, notifications it builds and
the setup program) can be translated. Texts that come from the game itself (menus, windows, building names,
events, dialogue) are in the game's language, unless the mod's language also has a game text file (see
[Translating the game's own texts](#translating-the-games-own-texts)). Shipped, all complete: every language the game has (German, French, Simplified and
Traditional Chinese, Dutch, Japanese, Romanian, Portuguese (Brazil), Spanish, Korean, Italian, Polish, Russian,
Norwegian, Ukrainian, Swedish, Turkish) and **Thai**. The files are sorted into sections by topic ("Map, tiles and
cursor", "Building and castle walls", "Setup program" ...), so a text is easy to find.

**Choosing the language**

* **Automatic** (default): the mod follows the game's language (Settings, language list). When there is no file
  for it, or the file is empty, the mod speaks English. Changing the game's language changes the mod's language
  too, and the mod says the new language.
* **Mod settings** (Ctrl+Shift+O), first line "Mod language": Left and Right choose Automatic, English or any
  language file, by the name in its `# Language:` line.
* **The game's language list** (Settings): languages the game does not have, such as Thai or ones you add,
  appear at the end as "ไทย (Thai, screen reader only)". Choosing one changes only what the mod says; the game
  keeps its language.
* Config file: `Language / Language` = `auto`, `en` or a file name without `.txt`, for example `th`.

**Editing a translation**

The files are in `BepInEx\plugins\KCAccess\Languages` and open in Notepad. Each line is
`English text = translation`. An empty translation (or a missing line) means English. Lines starting with `#`
are comments. Keep `{0}`, `{1}` and words in braces like `{Walk}` exactly as they are: the mod fills in numbers,
names and your current keys there. A translation whose braces do not match the English text is ignored (English
is used) and noted in the BepInEx log, so a typo never breaks the mod. `\n` is a line break.

**Numbers and plural forms.** Texts with a count come in pairs, for example `{0} tree` and `{0} trees`: the
first is used for 1, the second for every other number. When your language has more forms, write all of them in
the second line, separated by `|`, in the order of your language's rule: Russian, Ukrainian, Polish, Czech,
Slovak, Croatian and Serbian one|few|many (`{0} дерево|{0} дерева|{0} деревьев`), Romanian 1|2 to 19|20 and more
(`{0} copac|{0} copaci|{0} de copaci`), French and Portuguese 0 and 1|more. The mod picks the form for each number
by the file's language code (`ru.txt`); a file with another name can name its rule in a `# Plural rule: ru` line.

While a language is active, the mod checks its file every 2 seconds: save it in Notepad and you hear
"Language file reloaded" (in that language) with your changes in effect, no restart needed.

**Updates keep your changes.** The shipped files are in `Languages\default` and are replaced by every update.
At start the mod copies a shipped file to `Languages` when you do not have it yet; when you have it, your file is
rebuilt in the shipped layout: new texts appear in their own section, texts you never changed get the new shipped
translation, every text you changed stays yours, and texts the mod no longer uses are removed (they are
listed in `Languages\default\retired.keys`). Translated lines and comments you added yourself are kept in a
"Your own lines" section at the end. So always edit the files directly in `Languages`, never in `Languages\default`. The
setup program keeps them when it updates the mod; uninstalling removes them.

**Adding a language**

1. Copy `Languages\template.txt` (every text with an empty translation, regenerated by each update) to a new
   name in the same folder, for example `eo.txt`.
2. In the copy, write the language's name after `# Language:`, for example `# Language: Esperanto`. Leave
   `# Game language:` empty unless the game itself has this language (then write the game's name for it, for
   example `German`, so Automatic picks the file).
3. Translate as many lines as you like and save as UTF-8 (Notepad's default).
4. Choose it in the mod settings or in the game's language list (marked "screen reader only").

Translations to share are welcome as a pull request or issue on GitHub. For developers: every text in the code
goes through `Loc.T("English")`, `Loc.F("{0} tiles", n)`, `Loc.P(n, "{0} tree", "{0} trees")` or `Loc.N(...)`;
`tools\loc-extract.ps1` regenerates `src\KCAccess\Languages\template.txt` and puts the language files in the
same order, and a unit test fails when a text is missing from the template or a language file has unknown keys
or wrong placeholders.

### Translating the game's own texts

The game translates its own texts (menus, windows, tooltips, building names and descriptions, events and
notifications, place names, the diplomacy dialogue) into its 17 languages. For any other language the mod can
show and read them as well, from a **game text file**: `Languages\game\<code>.txt`, with the same code as the
mod's language file (`th.txt` for Thai). **Thai is shipped complete**: 2890 of the game's 2937 texts, the rest
being names, credits and font names that stay as they are.

* A game text file is **optional**, and so is every line in it: a text without a translation stays in the
  game's own language. A new language can start with the mod's file alone and add game texts later, a few at a
  time.
* The mod uses the game text file of the language it speaks. Mod settings (Ctrl+Shift+O), **Game texts in the
  mod language too**, switches this off and on (config `Language / GameTexts`).
* Each line is `term = translation`, with the game's English text above it as a `# English:` comment. Keep
  `{0}`, `{[NAME]}`, `<sprite ...>` and `[var=Var1]` exactly as they are: the game fills them in. A translation
  that changes them is ignored and noted in the BepInEx log. `\n` is a line break.
* To start one, copy `Languages\game\template.txt` (every game text with its English, written from your installed
  game each time the mod starts) to `<code>.txt`, write the language's name after `# Language:` and translate
  what you like. While the mod speaks that language it checks the file every 2 seconds: save it and the game's
  texts change right away.
* Updates keep your changes as with the mod's own files: the shipped files are in `Languages\game\default`; texts
  you changed stay yours, the others get the new shipped translation, and texts a game update adds appear in your
  file with their English.
* The game's font has no Thai letters, so Thai game texts are drawn as boxes on screen; the screen reader reads
  them correctly. Names stored in a saved game (your kingdom, a building you renamed) stay as they were saved.

## Sound cues

Every sound can be previewed: Shift+F1, End, then Up through the sounds and press Enter.

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
| Map / WalkSpeed | 2 | Auto-walk speed: 1 slow, 2 normal, 3 fast |
| Speech / AnnounceNotifications | true | Speak kingdom notifications automatically |
| Speech / LogSpeech | true | Write everything spoken to `BepInEx\LogOutput.log` |
| Speech / AnnounceSeasons | true | Speak the start of every summer, winter and new year (and heavy rain / thunderstorms) |
| Speech / Hints | true | Short hints for new players |
| Language / Language | auto | Language the mod speaks: `auto` follows the game, `en` English, or a file name in `Languages` such as `th` |
| Language / GameTexts | true | Show and read the game's own texts in the mod's language where `Languages\game\<code>.txt` translates them |
| Speech / SayPositions | false | Say the position after each item in menus and lists ("3 of 19") |
| Keyboard / Bindings | (empty) | Changed mod keys, easier to change in the game with Ctrl+Shift+O |
| Keyboard / ControllerSupport | true | Gamepad support through the mod (off: the game's own controller mode) |
| Keyboard / WindowsKeyFallback | true | Deliver key presses Windows saw but the game missed |
| Debug / KeyLog | true | Write key names and modifier state to the log for keyboard bug reports |
| Debug / CommandFile | false | Developer option: read test commands from `BepInEx\kcaccess_commands.txt` |
| Streaming / AnnounceTwitchCountdown | true | Say once when 10 seconds are left in a Twitch vote |
| Streaming / ReadTwitchChat | false | Read Twitch chat aloud (votes skipped, busy chat summarised) |
| Streaming / SpeechCaptions | false | Show the last spoken lines on screen for stream viewers |
| Streaming / StatusFile | false | Write `BepInEx\kcaccess_stream.txt` every 2 seconds for OBS |

## Game bugs fixed by the mod

* **Key bindings with modifiers never worked.** `KeyChord.GetKeyDown/Up` compared Ctrl/Alt/Shift with
  "pressed this frame" instead of "held", so a binding like Ctrl+S could practically never fire and
  Ctrl+key also triggered the plain key's action. Modifiers are now checked as held.
* **Ctrl+C silently switched the game into creative mode** during normal play (a developer shortcut
  outside the cheat check). It now only works when cheats are enabled.
* **Holding End made every AI kingdom plan a farm at your cursor.** A developer test left in the AI's
  update adds a "build a farm at the pointer" job each frame End is held; End is the mod's "jump to the
  selected building" key. That test job now ends at once without doing anything.

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
and Ctrl+Shift+F12 writes the live UI hierarchy to `BepInEx\kcaccess_ui_dump.txt`. Commands that set up
situations for testing at the cursor: `devbuild <name> [any] [clear]` (place a finished building), `army
knight|archer|settler|envoy`, `dragon [mama]`, `nest egg|hatch|mature|happy|bond`, `fire`, `ogre`, `viking
[elite]`, `plague [cure]`, `weather none|rain|heavy|storm|snow`, `twitch show|connect|vote <name> <n>|chat <name>
<text>|end|hide` (Twitch voting without Twitch), `log <text>`, `threatyears`, `buildnow`, `tracespeech <words>`, and for diplomacy `visit`, `convo <name>`
(GoldDemand, AIGift ...), `dialogue` (the current line's conversation and entry ids).

## License

MIT, see `LICENSE`. Third-party components are listed in `THIRD-PARTY-NOTICES.md`.
