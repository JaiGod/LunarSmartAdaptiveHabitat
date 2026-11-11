# Lunar Habitat Commander — script map (science-fiction game)

All C# code for the science-fiction NASA HUNCH habitat game. It is a copy of `Assets/HUNCH/Scripts` from the Unity project, laid out exactly as it is in the project.

Every script begins with the same header block: file, section, what the folder is for, what the script does, **why it exists** (the chat request or rubric line it answers) and the credits. Scripts were moved inside Unity with `AssetDatabase.MoveAsset`, so every script keeps its GUID and no scene or prefab reference broke. Both projects compile with 0 errors.


## Folder map

| Folder | What lives here |
|---|---|
| `01_Core/` | Game rules and the habitat simulation (crew, systems, alerts), start-up, self-test, cheats and the live link to the Crew Health web page. |
| `02_ARES_AI/` | Project ARES AI: ARIA the habitat AI, the shared mood/sensor engine, design pages D1–D6, the D4 ethics report and the threshold check. |
| `03_Player_Controls/` | How you move and find your way: VR controllers, keyboard/mouse, phone touch controls, doorway help, NavMesh paths, green guide arrows + minimap. |
| `04_UI_Menus_Audio/` | Menus, rounded UI, story intro, Star Store, music, voices (TTS/commands) and picture quality. |
| `05_World_FX/` | Living-world effects (Sun, earthshine, storms, doors, rockets) and the friendly extras (robot buddy, stars, badges). |
| `06_Frontier/A_Adventure_Core/` | Moon Frontier adventure layer: mining, aliens, tools, easy mode, survival + repair mini-games, Autopilot, the 3D models from the Drive collection. |
| `06_Frontier/B_Maps_Minigames/` | Extra maps and mini-games: solar farm, mining pit, Factory Land, Star Patrol, crew radio + Star Clicker, Science Academy, My Astronaut, model kits, Fun Zone, endless ground, Star Store pages. |
| `06_Frontier/C_Base_Crew_Systems/` | Systems inside the central base: pick-ups + battery bank, NASA HUNCH mode, physics view, Autopilot Smooth, crew of four + sleep pods, The Show, self-working crew, airlocks/lifts, crew self-care. |
| `06_Frontier/D_ARES_Layer/` | Project ARES rubric layer: floors fix + ARES pages, easy-mode missions and check-ins, ARES zone lights and sensor clusters in the world. |
| `Editor/` | Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model kits, store setup, probes, the HunchBridge automation and USB helpers. |


## `01_Core/`
_Game rules and the habitat simulation (crew, systems, alerts), start-up, self-test, cheats and the live link to the Crew Health web page._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `HabitatGame.cs` | Lunar Habitat Commander: keep four crew and the habitat healthy for 30 days. Rules come from the HUNCH proposal. |  |
| `HabitatSim.cs` | The habitat simulation. No Unity scene objects in here, only numbers and rules taken from the proposal. |  |
| `HunchBoot.cs` | Most parts of the game add themselves to the scene when the game starts. "Restart" loads the scene again, and without this they would not come back: the menu lost Explorer, Base systems and Maps, and the adventure layer was gone. |  |
| `HunchSelfTest.cs` | Developer check and demo: run the app with -hunchSelfTest <folder> [-hunchIdle] [-hunchSpeed 8] A bot plays the whole mission, saves screenshots and a score report, then quits. |  |
| `HunchQuiet.cs` | Keeps the Console clean of notices that come from Unity's own packages and are not faults of this game: - "no eye tracking device" and "hand tracking subsystem not running" (the headset simply has none switched on) |  |
| `HunchLink.cs` | The live link between the game and the NASA HUNCH Crew Health System web page. While the game runs it answers on this computer only (http://127.0.0.1:8765/): |  |
| `HunchCheats.cs` | Every shortcut that bends the rules, in one place (Menu > Cheats and anti-cheat). Each way of playing keeps its own four switches: |  |

## `02_ARES_AI/`
_Project ARES AI: ARIA the habitat AI, the shared mood/sensor engine, design pages D1–D6, the D4 ethics report and the threshold check._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `HunchAria.cs` | ARIA, the habitat AI. Three jobs, matching the proposal's "AI psychological monitoring with adaptive interventions": 1. FORECAST - watches every astronaut's wellbeing trend and predicts who will slip into "Underlying issue" and wh | Proposal: AI psychological monitoring with adaptive interventions. |
| `AresDocs.cs` | Project ARES deliverables, inside the game, page by page (Menu > Project ARES, or F11). Every rubric bullet has a page, and most pages show LIVE numbers from the running AI engine so the judges | Chat: rubric pages D1–D6 with drawn flowcharts and to-scale floor plans. |
| `AresEngine.cs` | The link between this engine and the game that hosts it (Lunar Habitat Commander). |  |
| `EthicsReport.cs` | D4 written ethics report (800–1,200 words). Shown in the game over three pages and saved to Reports/D4_Ethics_Report.md. | Rubric D4 written report. |
| `Validation.cs` | D1 threshold check: runs the REAL AI engine headless many times. • 10 "control" missions (nothing wrong, but with the things that fool sensors: daily exercise, an EVA, a noisy mic) | Rubric D1: 70-mission threshold check. |

## `03_Player_Controls/`
_How you move and find your way: VR controllers, keyboard/mouse, phone touch controls, doorway help, NavMesh paths, green guide arrows + minimap._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `HunchVr.cs` | VR controls with one job per button. The laser pointer, the trigger click and the teleport are done here, read straight from the controllers, so they do not depend on the XR Interaction Toolkit's own pointer. |  |
| `HunchDesktopFallback.cs` | When no VR headset is running, lets the player explore with keyboard and mouse instead. |  |
| `HunchTouch.cs` | Mobile controls: the game on a touch screen with very few buttons. - left thumb: a stick to walk (push it all the way to run) | Chat (Unity Play): mobile controls switched on for this game. |
| `HunchNavAssist.cs` | Makes sure the player can always get into every room, in VR and on desktop: - a small body (1.5 m tall, 0.18 m radius) that fits doorways and tight corridors |  |
| `HunchPath.cs` | Real paths through the base: round the walls, through the openings, up the stairs. A few seconds after the game starts this builds a walkable map (a NavMesh) of the main base from the same solid |  |
| `HunchGuide.cs` | Finding your way: - GREEN ARROWS on the floor flow from you toward where you are needed (the most urgent alert, or a place you asked for) |  |

## `04_UI_Menus_Audio/`
_Menus, rounded UI, story intro, Star Store, music, voices (TTS/commands) and picture quality._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `HunchMenu.cs` | Menu page, options (including everything you can hide), and talking: - Main menu at the start, pause menu on Esc (VR: left menu button) |  |
| `HunchUi.cs` | One place for the friendly look of every flat panel on screen: soft rounded corners instead of sharp rectangles. Used by the menus, the dashboards, the guide panels and the bars. Thin lines and full-screen fades stay square. |  |
| `HunchIntro.cs` | The story introduction: "Once upon a time..." in blue, then gold words drifting away over the stars. Any key, click, trigger or button skips it. It plays once each time the game is started (not again after Restart). |  |
| `HunchStore.cs` | The STAR STORE: four one-time (durable) add-ons sold through the Meta Horizon Store. costume_golden_pumpkin Golden Pumpkin King costume |  |
| `HunchMusic.cs` | Looping background music with a choice of styles. Starts by itself in every scene; M switches it on or off. 0 Space ambient the original recorded track |  |
| `HunchVoice.cs` | Voice for the game, using what Windows already has built in: - ARIA and the astronauts SPEAK their lines through the Windows text-to-speech voices (on this PC) |  |
| `HunchLook.cs` | Picture quality and fonts. - Fonts: the cleaner technical font that ships with Windows 10 and 11 (Bahnschrift, falling back to Segoe UI) for every |  |

## `05_World_FX/`
_Living-world effects (Sun, earthshine, storms, doors, rockets) and the friendly extras (robot buddy, stars, badges)._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `HunchWorldFX.cs` | Living-world effects. Sound, Sun, doors and camera effects are generated in code; particle effects come from the imported Hovl Studio "Magic Effects FREE" pack when it is in the project (see HunchFxLibrary), with simple built-in o |  |
| `HunchFxLibrary.cs` | The effect prefabs taken from imported asset packs (for example Hovl Studio "Magic Effects FREE"). Filled in by the menu HUNCH > Use imported effect packs. Lives in Resources so the built game includes it. |  |
| `HunchKidFeatures.cs` | Kid-friendly extras: a robot buddy that shows the way, gold stars with Moon facts, high-fives with the crew, mood faces, a garden that grows, badges and confetti. Works the same with or without a headset. |  |

## `06_Frontier/A_Adventure_Core/`
_Moon Frontier adventure layer: mining, aliens, tools, easy mode, survival + repair mini-games, Autopilot, the 3D models from the Drive collection._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `HunchFrontier.cs` | Marks things the tools and the hand can use. kind: 1 alien, 2 mineral node, 3 placed block, 10 rover, 11 light button, 12 fan button, 13 moon rock. |  |
| `HunchFrontier2.cs` | Second half of Moon Frontier: the things that make it easy and friendly to play. - Easy mode: a "smart hand" (the trigger zaps an alien, drills a rock or uses whatever you aim at, without choosing a tool), |  |
| `HunchFrontier3.cs` | Third part of Moon Frontier: - Survival: the player's health and hunger, food packs, alien bumps that hurt, respawn at the airlock |  |
| `HunchFrontier4.cs` | Fourth part of Moon Frontier: Autopilot. The third way to play (after Survival and Creative): the game plays itself in Creative mode so you can watch. |  |
| `HunchFrontier5.cs` | Fifth part of Moon Frontier: ten 3D models from Jaineil's NASA HUNCH model collection (Google Drive, "BEST MODELS"). They were converted from .glb to .obj and live in Assets/HUNCH/Resources/DriveModels (mesh + one colour texture e |  |

## `06_Frontier/B_Maps_Minigames/`
_Extra maps and mini-games: solar farm, mining pit, Factory Land, Star Patrol, crew radio + Star Clicker, Science Academy, My Astronaut, model kits, Fun Zone, endless ground, Star Store pages._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `HunchFrontier6.cs` | Sixth part of Moon Frontier: extra maps. The central base is untouched; each map is its own flat piece of Moon far away, reached through the Map Gate at the mining field, the menu, the Y key or the MAPS button on the VR dashboard. |  |
| `HunchFrontier8.cs` | Eighth part of Moon Frontier: FACTORY LAND (map 7). A production chain on its own piece of Moon: drill coal, iron, gold and stone from deposits (by hand with the Mining drill, or build drill rigs) |  |
| `HunchFrontier9.cs` | Ninth part of Moon Frontier: STAR PATROL (map 8) fly a star fighter in open space high above the Moon. Alien scout ships circle the station; |  |
| `HunchFrontier10.cs` | Tenth part of Moon Frontier: CREW RADIO talk into the microphone: the game shows TRANSMITTING, and a crew member or ARIA answers when you stop. |  |
| `HunchFrontier11.cs` | Eleventh part of Moon Frontier: the SCIENCE ACADEMY (map 11) PHYSICS FORCE LAB thirteen set-ups from Physics 1, Physics 2 and Physics C (mechanics, fluids, electricity and magnetism). |  |
| `HunchFrontier12.cs` | Twelfth part of Moon Frontier: MY ASTRONAUT experience points and levels. A level-up shows your astronaut in front of a green screen with a PRESENT |  |
| `HunchFrontier13.cs` | Thirteenth part of Moon Frontier: real 3-D models in place of simple shapes. FACTORY KIT pieces of the "Unity Factory" Asset Store pack (machines, conveyors, robot arms, control cabinets, carts), |  |
| `HunchFrontier19.cs` | Nineteenth part of Moon Frontier (full game only; NASA HUNCH mode hides all of it): - four more tools, each with a model made in Blender: Plasma repeater, Laser cannon, Star minigun, Comet sword. |  |
| `HunchFrontier21.cs` | Twenty-first part of Moon Frontier: - Moon ground that never ends: past the edge of the model the game lays its own terrain in tiles around you, |  |
| `HunchFrontier24.cs` | Twenty-fourth part of Moon Frontier: the STAR STORE pages and the things they sell. - a gold STAR STORE button near the top of the main menu and the pause menu (HunchMenu.SetStore) |  |

## `06_Frontier/C_Base_Crew_Systems/`
_Systems inside the central base: pick-ups + battery bank, NASA HUNCH mode, physics view, Autopilot Smooth, crew of four + sleep pods, The Show, self-working crew, airlocks/lifts, crew self-care._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `HunchFrontier7.cs` | Seventh part of Moon Frontier: the systems inside the central base. - things to pick up: battery modules, astronaut food (cans and pouches) and things to READ (tablets with notes, |  |
| `HunchFrontier14.cs` | Fourteenth part of Moon Frontier: NASA HUNCH MODE. Menu > "NASA HUNCH mode". ENABLED = only what the NASA HUNCH project sheet asks for; disabled = everything (the fun version). | NASA HUNCH mode: only what the HUNCH project sheet asks for. |
| `HunchFrontier15.cs` | Fifteenth part of Moon Frontier: PHYSICS VIEW (Menu > Base systems > Physics view, or F8). Off when the game starts. It draws the physics and maths of what is happening, as pictures rather than formulas: |  |
| `HunchFrontier16.cs` | Sixteenth part of Moon Frontier: Autopilot Smooth. The AI plays the real mission and goes for the best score, and it moves like a player does: |  |
| `HunchFrontier17.cs` | Seventeenth part of Moon Frontier: - a crew of four that counts you: you are the Commander, with three crewmates (never five people) | Chat: '4 crew members max INCLUDING himself' + sleep pods. |
| `HunchFrontier18.cs` | Eighteenth part of Moon Frontier: THE SHOW. The mission as a cartoon you watch: the Commander strolls through the real 30-day mission (it is Autopilot Smooth |  |
| `HunchFrontier20.cs` | Twentieth part of Moon Frontier: - a crew that works by itself: the three crew members you are not playing walk to where their day takes them |  |
| `HunchFrontier22.cs` | Twenty-second part of Moon Frontier: real ways through the base. The Blender model has rooms and buildings with no doorway between them. Until now the autopilot crossed those with a |  |
| `HunchFrontier23.cs` | Twenty-third part of Moon Frontier: - the crew look after their own health: a calm break in the Sanctuary when stressed, a check-up in the Medical Bay, |  |

## `06_Frontier/D_ARES_Layer/`
_Project ARES rubric layer: floors fix + ARES pages, easy-mode missions and check-ins, ARES zone lights and sensor clusters in the world._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `HunchFrontier25.cs` | Project ARES layer (the Student Brief rubric, 200 points) inside Lunar Habitat Commander, plus two clean-ups of the base model: - FLOORS: several floors of the Blender model sit at exactly the same height (the hub's second floor a | Chat: 'score 100/100' — Project ARES rubric layer. |
| `HunchFrontier26.cs` | EASY MODE for Lunar Habitat Commander — ARES missions, in-person health check-ins and bedtime rules. - MISSIONS: six short story missions about the crew's mental health, each tied to a part of the Project ARES rubric. | Chat: make it easy — missions with one next step (G). |
| `HunchFrontier27.cs` | PROJECT ARES IN THE WORLD (D2 spatial design) — the six zone types, their lighting and their sensors are visible IN the base, not only in the documents. Nothing is moved: the central dorm and every module stay exactly where they a | Chat: 'KEEP THE CENTER DORM for the unrealistic game' — nothing is moved, zones are only lit and labelled. |

---
© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
