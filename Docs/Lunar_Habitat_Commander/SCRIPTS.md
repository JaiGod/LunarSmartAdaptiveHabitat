# Lunar Habitat Commander (sci-fi) — script index

Every script, how big it is and what it is for (the "can I explain every script?" check).

| Script | Lines | What it does |
|---|---|---|
| `01_Core/HabitatGame.cs` | 663 | Lunar Habitat Commander: keep four crew and the habitat healthy for 30 days. Rules |
| `01_Core/HabitatSim.cs` | 438 | The habitat simulation. No Unity scene objects in here, only numbers and rules taken |
| `01_Core/HunchBoot.cs` | 50 | Most parts of the game add themselves to the scene when the game starts. "Restart" |
| `01_Core/HunchCheats.cs` | 57 | Every shortcut that bends the rules, in one place (Menu > Cheats and anti-cheat). Each |
| `01_Core/HunchLink.cs` | 138 | The live link between the game and the NASA HUNCH Crew Health System web page. While |
| `01_Core/HunchQuiet.cs` | 51 | Keeps the Console clean of notices that come from Unity's own packages and are not |
| `01_Core/HunchSelfTest.cs` | 143 | Developer check and demo: run the app with -hunchSelfTest <folder> [-hunchIdle] |
| `02_ARES_AI/AresDocs.cs` | 569 | Project ARES deliverables, inside the game, page by page (Menu > Project ARES, or |
| `02_ARES_AI/AresEngine.cs` | 794 | The link between this engine and the game that hosts it (Lunar Habitat Commander). |
| `02_ARES_AI/EthicsReport.cs` | 44 | D4 written ethics report (800–1,200 words). Shown in the game over three pages and |
| `02_ARES_AI/HunchAria.cs` | 238 | ARIA, the habitat AI. Three jobs, matching the proposal's "AI psychological monitoring |
| `02_ARES_AI/Validation.cs` | 154 | D1 threshold check: runs the REAL AI engine headless many times. • 10 "control" |
| `03_Player_Controls/HunchDesktopFallback.cs` | 175 | When no VR headset is running, lets the player explore with keyboard and mouse |
| `03_Player_Controls/HunchGuide.cs` | 412 | Finding your way: - GREEN ARROWS on the floor flow from you toward where you are |
| `03_Player_Controls/HunchNavAssist.cs` | 191 | Makes sure the player can always get into every room, in VR and on desktop: - a small |
| `03_Player_Controls/HunchPath.cs` | 179 | Real paths through the base: round the walls, through the openings, up the stairs. A |
| `03_Player_Controls/HunchTouch.cs` | 138 | Mobile controls: the game on a touch screen with very few buttons. - left thumb: a |
| `03_Player_Controls/HunchVr.cs` | 351 | VR controls with one job per button. The laser pointer, the trigger click and the |
| `04_UI_Menus_Audio/HunchIntro.cs` | 146 | The story introduction: "Once upon a time..." in blue, then gold words drifting away |
| `04_UI_Menus_Audio/HunchLook.cs` | 133 | Picture quality and fonts. - Fonts: the cleaner technical font that ships with Windows |
| `04_UI_Menus_Audio/HunchMenu.cs` | 941 | Menu page, options (including everything you can hide), and talking: - Main menu at |
| `04_UI_Menus_Audio/HunchMusic.cs` | 224 | Looping background music with a choice of styles. Starts by itself in every scene; M |
| `04_UI_Menus_Audio/HunchStore.cs` | 184 | The STAR STORE: four one-time (durable) add-ons sold through the Meta Horizon Store. |
| `04_UI_Menus_Audio/HunchUi.cs` | 74 | One place for the friendly look of every flat panel on screen: soft rounded corners |
| `04_UI_Menus_Audio/HunchVoice.cs` | 229 | Voice for the game, using what Windows already has built in: - ARIA and the astronauts |
| `05_World_FX/HunchFxLibrary.cs` | 93 | The effect prefabs taken from imported asset packs (for example Hovl Studio "Magic |
| `05_World_FX/HunchKidFeatures.cs` | 352 | Kid-friendly extras: a robot buddy that shows the way, gold stars with Moon facts, |
| `05_World_FX/HunchWorldFX.cs` | 621 | Living-world effects. Sound, Sun, doors and camera effects are generated in code; |
| `06_Frontier/A_Adventure_Core/HunchFrontier.cs` | 1698 | Marks things the tools and the hand can use. kind: 1 alien, 2 mineral node, 3 placed |
| `06_Frontier/A_Adventure_Core/HunchFrontier2.cs` | 503 | Second half of Moon Frontier: the things that make it easy and friendly to play. - |
| `06_Frontier/A_Adventure_Core/HunchFrontier3.cs` | 930 | Third part of Moon Frontier: - Survival: the player's health and hunger, food packs, |
| `06_Frontier/A_Adventure_Core/HunchFrontier4.cs` | 345 | Fourth part of Moon Frontier: Autopilot. The third way to play (after Survival and |
| `06_Frontier/A_Adventure_Core/HunchFrontier5.cs` | 160 | Fifth part of Moon Frontier: ten 3D models from Jaineil's NASA HUNCH model collection |
| `06_Frontier/B_Maps_Minigames/HunchFrontier10.cs` | 426 | Tenth part of Moon Frontier: CREW RADIO talk into the microphone: the game shows |
| `06_Frontier/B_Maps_Minigames/HunchFrontier11.cs` | 400 | Eleventh part of Moon Frontier: the SCIENCE ACADEMY (map 11) PHYSICS FORCE LAB |
| `06_Frontier/B_Maps_Minigames/HunchFrontier12.cs` | 673 | Twelfth part of Moon Frontier: MY ASTRONAUT experience points and levels. A level-up |
| `06_Frontier/B_Maps_Minigames/HunchFrontier13.cs` | 248 | Thirteenth part of Moon Frontier: real 3-D models in place of simple shapes. FACTORY |
| `06_Frontier/B_Maps_Minigames/HunchFrontier19.cs` | 578 | Nineteenth part of Moon Frontier (full game only; NASA HUNCH mode hides all of it): - |
| `06_Frontier/B_Maps_Minigames/HunchFrontier21.cs` | 493 | Twenty-first part of Moon Frontier: - Moon ground that never ends: past the edge of |
| `06_Frontier/B_Maps_Minigames/HunchFrontier24.cs` | 319 | Twenty-fourth part of Moon Frontier: the STAR STORE pages and the things they sell. - |
| `06_Frontier/B_Maps_Minigames/HunchFrontier6.cs` | 841 | Sixth part of Moon Frontier: extra maps. The central base is untouched; each map is |
| `06_Frontier/B_Maps_Minigames/HunchFrontier8.cs` | 437 | Eighth part of Moon Frontier: FACTORY LAND (map 7). A production chain on its own |
| `06_Frontier/B_Maps_Minigames/HunchFrontier9.cs` | 461 | Ninth part of Moon Frontier: STAR PATROL (map 8) fly a star fighter in open space high |
| `06_Frontier/C_Base_Crew_Systems/HunchFrontier14.cs` | 522 | Fourteenth part of Moon Frontier: NASA HUNCH MODE. Menu > "NASA HUNCH mode". ENABLED = |
| `06_Frontier/C_Base_Crew_Systems/HunchFrontier15.cs` | 130 | Fifteenth part of Moon Frontier: PHYSICS VIEW (Menu > Base systems > Physics view, or |
| `06_Frontier/C_Base_Crew_Systems/HunchFrontier16.cs` | 370 | Sixteenth part of Moon Frontier: Autopilot Smooth. The AI plays the real mission and |
| `06_Frontier/C_Base_Crew_Systems/HunchFrontier17.cs` | 435 | Seventeenth part of Moon Frontier: - a crew of four that counts you: you are the |
| `06_Frontier/C_Base_Crew_Systems/HunchFrontier18.cs` | 232 | Eighteenth part of Moon Frontier: THE SHOW. The mission as a cartoon you watch: the |
| `06_Frontier/C_Base_Crew_Systems/HunchFrontier20.cs` | 399 | Twentieth part of Moon Frontier: - a crew that works by itself: the three crew members |
| `06_Frontier/C_Base_Crew_Systems/HunchFrontier22.cs` | 222 | Twenty-second part of Moon Frontier: real ways through the base. The Blender model has |
| `06_Frontier/C_Base_Crew_Systems/HunchFrontier23.cs` | 183 | Twenty-third part of Moon Frontier: - the crew look after their own health: a calm |
| `06_Frontier/C_Base_Crew_Systems/HunchFrontier7.cs` | 1227 | Seventh part of Moon Frontier: the systems inside the central base. - things to pick |
| `06_Frontier/D_ARES_Layer/HunchFrontier25.cs` | 499 | Project ARES layer (the Student Brief rubric, 200 points) inside Lunar Habitat |
| `06_Frontier/D_ARES_Layer/HunchFrontier26.cs` | 297 | EASY MODE for Lunar Habitat Commander — ARES missions, in-person health check-ins and |
| `06_Frontier/D_ARES_Layer/HunchFrontier27.cs` | 231 | PROJECT ARES IN THE WORLD (D2 spatial design) — the six zone types, their lighting and |
| `07_Debug/HunchDebug.cs` | 57 | DEBUG TOOLS — tagged logging (HLog) + a debug panel on the ` key (left of 1): |
| `Bridge/Editor/HunchBridge.cs` | 127 | Development helper. Lets the assistant drive this open Editor through small text files |
| `Editor/HunchAssetPacks.cs` | 127 | Finds Asset Store packages that were downloaded on this PC but not yet imported into |
| `Editor/HunchAssetPostprocessor.cs` | 70 | Import settings for everything under Assets/HUNCH. |
| `Editor/HunchConsole.cs` | 49 | Development helper: writes what the Unity Console window is showing to |
| `Editor/HunchFactoryKit.cs` | 170 | The "Unity Factory" pack from the Asset Store is made for HDRP; this project uses URP. |
| `Editor/HunchFxSetup.cs` | 203 | Makes imported effect packs usable in this URP project: 1. converts their old built- |
| `Editor/HunchLocalServe.cs` | 99 | Tiny read-only file server on this computer only (127.0.0.1), used to hand the store |
| `Editor/HunchLookSetup.cs` | 89 | Picture quality set-up that has to be done in the Editor (it changes the renderer |
| `Editor/HunchModelsKit.cs` | 88 | Models made in Blender for this game live in Assets/HUNCH/Models as .fbx files (the |
| `Editor/HunchOpen.cs` | 23 | Menu shortcut that opens the main game scene. |
| `Editor/HunchOrganize.cs` | 81 | Moves scripts into the organised folder structure with AssetDatabase.MoveAsset, so every |
| `Editor/HunchPortalKit.cs` | 52 | The "Stone portal" pack by Ordoss (Unity Asset Store). Build() lists its prefabs in |
| `Editor/HunchProbe.cs` | 135 | Development helper used together with HunchBridge: while the game is playing in the |
| `Editor/HunchPublish.cs` | 91 | Packs the Windows build into one zip that can be uploaded to a game page (itch.io and |
| `Editor/HunchQuestAudit.cs` | 82 | Finds which of the project's optional packages the game's own scripts actually use, |
| `Editor/HunchQuestBuild.cs` | 208 | Builds the Meta Quest version as an .apk (the Meta Horizon Store does not take .aab |
| `Editor/HunchQuestClean.cs` | 118 | One-time clean-up so the Meta Quest build only contains what the game uses: takes out |
| `Editor/HunchQuietEditor.cs` | 18 | the same Console filter while the Editor is not playing |
| `Editor/HunchReadable.cs` | 44 | Development helper: the walkable map (HunchPath) is built while the game runs, from |
| `Editor/HunchSave.cs` | 30 | Saves the open scenes and every changed asset, and can close the Editor afterwards. |
| `Editor/HunchSceneList.cs` | 45 | Lists the big parts of the open scene (name, path, size, centre) to |
| `Editor/HunchSceneOpener.cs` | 48 | Opens the explorer scene when the project starts. The VR template's welcome tutorial |
| `Editor/HunchStoreSetup.cs` | 67 | Finds and installs Meta's Platform SDK (store purchases) from the Unity package |
| `Editor/HunchUsb.cs` | 201 | VR check + USB copy helpers, driven through the HunchBridge (invoke HunchUsb.Method). |
| `Editor/HunchVRBuilder.cs` | 675 | Builds the HUNCH Lunar Dome VR Explorer scene from the Blender export. Menu: HUNCH > |
| `Editor/HunchVrCheck.cs` | 98 | Checks whether this PC is ready to show the game in a VR headset, and writes the |
| `Editor/HunchVrFeatures.cs` | 83 | Brings in the VR interaction sample sets that ship inside the XR packages already |
| `Editor/HunchWebBuild.cs` | 119 | Web version for Unity Play (play.unity.com). Install(): adds Unity's WebGL Publisher |
| `Editor/HunchWebCheck.cs` | 28 | Checks whether this Unity install can make a WebGL build (needed for Unity Play) and |

**85 scripts · 24366 lines**

© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.