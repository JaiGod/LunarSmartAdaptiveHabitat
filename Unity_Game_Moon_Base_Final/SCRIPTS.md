# Lunar Smart Adaptive Habitat (realistic) — script index

Every script, how big it is and what it is for (the "can I explain every script?" check).

| Script | Lines | What it does |
|---|---|---|
| `01_Core/Bootstrap.cs` | 71 | Makes the game start no matter how the scene's component links were saved. (A scene |
| `01_Core/Core.cs` | 194 | Properties exported from Blender (interact, collider, zone, sensor_type ...) attached |
| `01_Core/Diagnostics.cs` | 43 | Writes where Unity actually placed key objects (empties vs meshes) so frame mismatches |
| `01_Core/Menu.cs` | 197 | Main menu: the player stands inside a vibrant glowing box with floating panels. |
| `01_Core/RenderFix.cs` | 106 | Neutral, realistic colour for the habitat: overrides any project-wide URP volume |
| `01_Core/SelfTest.cs` | 128 | Automated beta test: run the build with -selftest. Visits every feature, captures |
| `02_AI_Wellness/Authority.cs` | 185 | THE HUMAN'S OFF SWITCH (rubric D4 — human override / hard limits). Press O (VR: menu → |
| `02_AI_Wellness/Brain.cs` | 515 | Hard limits: what the AI is NEVER allowed to do without human authorisation. |
| `02_AI_Wellness/Scenarios.cs` | 202 | Headless 72-hour run of the real engine with scripted simultaneous events. |
| `02_AI_Wellness/Validation.cs` | 155 | D1 threshold check: runs the REAL AI engine headless many times. • 10 "control" |
| `03_Crew/Agents.cs` | 224 | Waypoint graph exported from Blender (WP_* empties with "links"). |
| `03_Crew/CrewOrders.cs` | 253 | CREW JOBS (I, or Menu > Crew jobs, or walk up to a crewmate and press E). The |
| `03_Crew/HealthBot.cs` | 153 | AI ROBOTIC HEALTH STATION (one per private quarters, SQ1..SQ4). A wall-folded 3-joint |
| `03_Crew/Sleep.cs` | 262 | SLEEP — you really sleep: one real minute = a full night (8 h sim). Pod lights go OUT |
| `03_Crew/Social.cs` | 211 | WAVING — small social moments that also feed the AI's interpersonal-conflict and |
| `04_Habitat/DomeLights.cs` | 185 | DOME STATUS LIGHTS + AI CONTROL MODE |
| `04_Habitat/DoorPassage.cs` | 420 | Guarantees every doorway / hatch / hallway in the habitat is walkable. 1. Slims the |
| `04_Habitat/Dressing.cs` | 294 | FUNCTION-FIRST HABITAT DRESSING |
| `04_Habitat/Environment.cs` | 445 | used by the "AI misbehaves" demo: the AI forces a lever into a zone, ignoring the pod |
| `04_Habitat/Interact.cs` | 341 | Every tagged object in the habitat gets one of these; VR (trigger/select) and desktop |
| `04_Habitat/LEDs.cs` | 197 | FUNCTIONAL LED SYSTEM (ISS + aircraft cabin, not cyberpunk). Every strip has a job: |
| `04_Habitat/LadderClimb.cs` | 160 | Real ladder climbing between the two decks (no more teleport). Desktop: walk up to the |
| `04_Habitat/LayoutExport.cs` | 57 | Exports the real habitat layout (zones, walls footprint, sensors, environmental |
| `04_Habitat/Props.cs` | 324 | LOOSE PROPS WITH REAL LUNAR PHYSICS Cargo transfer bags (NASA CTB sizes), food |
| `04_Habitat/SmartObjects.cs` | 744 | Every wellness / medical / life-support asset is a SYSTEM, not a prop. Look at one and it |
| `04_Habitat/SpaceRealism.cs` | 442 | SPACE REALISM — the Moon's environment, ported and corrected from the unity2 |
| `05_Player_UI/Panels.cs` | 367 | Opens panels in front of the player (world-space in VR, screen overlay on desktop). |
| `05_Player_UI/Player.cs` | 286 | quick-travel (desktop F7 / VR menu): cycles ladder top → quarters vestibule → quarters |
| `05_Player_UI/UI.cs` | 495 | kept for callers that still ask for a per-label colour: now always the console base |
| `06_Missions/HabitatOps.cs` | 273 | Makes the smart systems PLAYABLE with clear instructions: |
| `06_Missions/Missions.cs` | 339 | MISSIONS — the friendly way to play. Seven short story missions, 3–4 easy steps each, |
| `06_Missions/Tasks.cs` | 677 | RUBRIC TASKS — 20 concrete things to do in the habitat, one or more for every rubric |
| `07_Deliverables/DesignDocs.cs` | 563 | Project ARES deliverables, inside the game, page by page (menu → "ARES deliverables |
| `07_Deliverables/EthicsReport.cs` | 44 | D4 written ethics report (800–1,200 words). Shown in the game over three pages and |
| `07_Deliverables/FloorPlan.cs` | 158 | D2 ANNOTATED FLOOR PLAN — drawn on top of the big map (M), never in the 3D view (layer |
| `08_Debug/DebugTools.cs` | 96 | DEBUG TOOLS — one place for logging, the in-game debug panel and the Scene-view gizmos. |
| `Editor/Builder.cs` | 378 | Contact shadows + ambient occlusion so objects read as touching the floor/walls they |
| `Editor/HabImport.cs` | 43 | Copies Blender custom properties (interact, collider, zone, links …) onto a HabTag |
| `Editor/HunchBlenderRun.cs` | 43 | Runs Blender in the background (no window) with a Python script, e.g. to make a light |
| `Editor/HunchBridge.cs` | 127 | Development helper. Lets the assistant drive this open Editor through small text files |
| `Editor/HunchCeilingSnap.cs` | 60 | mounts every ceiling light fixture flush against the ceiling above it. |
| `Editor/HunchHousekeep.cs` | 87 | Tidies the Downloads\Nasa folder. List() writes what each top-level folder holds; |
| `Editor/HunchOrganize.cs` | 81 | Moves scripts into the organised folder structure with AssetDatabase.MoveAsset, so every |
| `Editor/HunchSitePreview.cs` | 59 | Tiny read-only web server for previewing the project website on THIS computer only |
| `Editor/HunchU3Probe.cs` | 111 | Development helper used with HunchBridge (Unity 3): while playing in the Editor, runs |
| `Editor/HunchUsb.cs` | 226 | VR check + USB copy helpers, driven through the HunchBridge (invoke HunchUsb.Method). |
| `Editor/HunchVrAudit.cs` | 95 | Checks that the VR setup is complete: XR loaders per platform (PC + Quest/Android), |
| `Editor/HunchVrFix.cs` | 83 | Restores the OpenXR feature set (controller profiles, hand tracking, Quest support) in the |
| `Editor/HunchWellnessSetup.cs` | 30 | tidies the wellness asset import (Resources/Wellness is the only copy the game loads). |
| `Editor/OpenStartScene.cs` | 39 | Opens the start scene automatically when the project is opened in the Editor. |

**50 scripts · 11268 lines**

© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.