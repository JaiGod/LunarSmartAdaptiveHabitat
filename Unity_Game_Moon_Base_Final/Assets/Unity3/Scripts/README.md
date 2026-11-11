# Moon Base Final — script map (realistic game)

All C# code for the realistic NASA HUNCH habitat game (`Assets/Unity3/Scripts`), grouped by what each script does.

Every script begins with the same header block: file, section, what the folder is for, what the script does, **why it exists** (the chat request or rubric line it answers) and the credits. Scripts were moved inside Unity with `AssetDatabase.MoveAsset`, so every script keeps its GUID and no scene or prefab reference broke. Both projects compile with 0 errors.


## Folder map

| Folder | What lives here |
|---|---|
| `01_Core/` | Start-up, shared state, menu scene, render settings and automated self-test. |
| `02_AI_Wellness/` | The AI psychological-monitoring engine: 9 sensors -> 6 condition scores -> 3 response tiers, hard limits, the human OFF switch, the 70-mission threshold check and the 72-hour tabletop. |
| `03_Crew/` | The 4-person crew (you + 3 AI crewmates): schedules, jobs, waving, sleep pods with mood lights, and the robotic health station. |
| `04_Habitat/` | The base itself: zones and their light/sound/temperature/air, dome status lights, doors and hatches, ladders, loose props in 1/6 g, Moon environment, layout export. |
| `05_Player_UI/` | The player rig (VR + desktop), the HUD and every pop-up panel. |
| `06_Missions/` | Seven story missions and the rubric task list — the 'one next step' banner. |
| `07_Deliverables/` | Project ARES D1–D6 inside the game: design pages, D4 ethics report, dimensioned floor plan. |
| `Editor/` | Editor tools (not in the built game): scene/material builder, Blender import, the HunchBridge automation, probes, USB/VR/web build helpers, the VR audit + fix (`HunchVrAudit`, `HunchVrFix`: controller profiles and build scenes), `HunchBlenderRun` (runs Blender from Unity) and `HunchSitePreview` (serves the website locally). |


## `01_Core/`
_Start-up, shared state, menu scene, render settings and automated self-test._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `Core.cs` | Properties exported from Blender (interact, collider, zone, sensor_type ...) attached on import. |  |
| `Bootstrap.cs` | Makes the game start no matter how the scene's component links were saved. (A scene can lose its link to a MonoBehaviour that lives in a file with a different name — that caused the black screen.) |  |
| `Menu.cs` | Main menu: the player stands inside a vibrant glowing box with floating panels. |  |
| `Diagnostics.cs` | Writes where Unity actually placed key objects (empties vs meshes) so frame mismatches can be fixed with data. |  |
| `RenderFix.cs` | Neutral, realistic colour for the habitat: overrides any project-wide URP volume profile (the VR template ships a teal-tinted default) with a top-priority global volume that resets every colour-grading effect, and writes a |  |
| `SelfTest.cs` | Automated beta test: run the build with -selftest. Visits every feature, captures screenshots, records errors, quits. |  |

## `02_AI_Wellness/`
_The AI psychological-monitoring engine: 9 sensors -> 6 condition scores -> 3 response tiers, hard limits, the human OFF switch, the 70-mission threshold check and the 72-hour tabletop._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `Brain.cs` | Hard limits: what the AI is NEVER allowed to do without human authorisation. | Chat: 'include the mood simulator and attachment to astronaut' — wearables feed this engine. |
| `Authority.cs` | THE HUMAN'S OFF SWITCH (rubric D4 — human override / hard limits). Press O (VR: menu → AI off switch, or Crew Interface → Overrides) whenever the AI oversteps. | Rubric D4: human override + hard limits. |
| `Validation.cs` | D1 threshold check: runs the REAL AI engine headless many times. • 10 "control" missions (nothing wrong, but with the things that fool sensors: daily exercise, an EVA, a noisy mic) | Rubric D1: thresholds checked in 70 simulated missions (chat: 'score 100/100'). |
| `Scenarios.cs` | Headless 72-hour run of the real engine with scripted simultaneous events. | Rubric Bonus A: 72-hour tabletop simulation. |

## `03_Crew/`
_The 4-person crew (you + 3 AI crewmates): schedules, jobs, waving, sleep pods with mood lights, and the robotic health station._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `Agents.cs` | Waypoint graph exported from Blender (WP_* empties with "links"). | Chat: '2-4 astronauts, single player'. |
| `CrewOrders.cs` | CREW JOBS (I, or Menu > Crew jobs, or walk up to a crewmate and press E). The Commander gives the three AI crewmates a job with ONE tap: Okafor, Tanaka and Rivera each have a column of big | Chat: '2-4 astronauts, single player' — you command three AI crewmates. |
| `Social.cs` | WAVING — small social moments that also feed the AI's interpersonal-conflict and depression signals. • An AI crewmate who sees you within ~4 m waves and says hello (once a minute at most). | Chat: 'team meetings should exist' + social cues feeding the AI. |
| `Sleep.cs` | SLEEP — you really sleep: one real minute = a full night (8 h sim). Pod lights go OUT (1-lux amber night-light only), the wristband records sleep architecture live, and the last 10% of the minute runs a dawn-simulation sunrise. | Chat: 'specific small pods for the astronaut to sleep in where lights blink based on their mood / mental health'. |
| `HealthBot.cs` | AI ROBOTIC HEALTH STATION (one per private quarters, SQ1..SQ4). A wall-folded 3-joint arm with a blood-pressure cuff / pulse-oximeter tool, an instrument dock (thermometer, AED) | Chat: 'a way to combat their physical health'. |

## `04_Habitat/`
_The base itself: zones and their light/sound/temperature/air, dome status lights, doors and hatches, ladders, loose props in 1/6 g, Moon environment, layout export._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `Environment.cs` | used by the "AI misbehaves" demo: the AI forces a lever into a zone, ignoring the pod rules | Rubric D2/D3: each zone's lighting, sound, temperature, scent and air levers. |
| `DomeLights.cs` | DOME STATUS LIGHTS + AI CONTROL MODE | Chat: pod/dome lights that show each astronaut's mood and rest cycle. |
| `DoorPassage.cs` | Guarantees every doorway / hatch / hallway in the habitat is walkable. 1. Slims the player's CharacterController to a realistic shoulder width and raises its step height |  |
| `Interact.cs` | Every tagged object in the habitat gets one of these; VR (trigger/select) and desktop (E / click) both call Activate(). |  |
| `LadderClimb.cs` | Real ladder climbing between the two decks (no more teleport). Desktop: walk up to the ladder, press E → W / S (or ↑ ↓) climb, reaching the top/bottom steps you off. |  |
| `Props.cs` | LOOSE PROPS WITH REAL LUNAR PHYSICS Cargo transfer bags (NASA CTB sizes), food containers, sample bags, tools (NASA public-domain models: hammer, wrench, | Chat: 1/6 gravity — loose props fall and bounce like on the Moon. |
| `SpaceRealism.cs` | SPACE REALISM — the Moon's environment, ported and corrected from the unity2 "HunchWorldFX/HabitatSim" systems (no aliens, no portals, no weapons). Everything is procedural: no audio files or particle packs needed. | Chat: 'VERY VERY REALISTIC … Moon is 1/6 gravity'. |
| `LayoutExport.cs` | Exports the real habitat layout (zones, walls footprint, sensors, environmental control nodes, waypoints, Config-B objects) to Assets/Unity3/layout_export.json so the D2 floor plans are drawn to scale from the actual model. |  |

## `05_Player_UI/`
_The player rig (VR + desktop), the HUD and every pop-up panel._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `Player.cs` | quick-travel (desktop F7 / VR menu): cycles ladder top → quarters vestibule → quarters 1-4 | Chat: VR single-player kit + desktop; suit with communication attachment. |
| `UI.cs` | kept for callers that still ask for a per-label colour: now always the console base (no rainbow) |  |
| `Panels.cs` | Opens panels in front of the player (world-space in VR, screen overlay on desktop). |  |

## `06_Missions/`
_Seven story missions and the rubric task list — the 'one next step' banner._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `Missions.cs` | MISSIONS — the friendly way to play. Seven short story missions, 3–4 easy steps each, all about the crew's mental health and each one tied to a part of the HUNCH rubric. The banner at the top always shows the ONE next step, and | Chat: make it easy to play — one key does the next step. |
| `Tasks.cs` | RUBRIC TASKS — 20 concrete things to do in the habitat, one or more for every rubric area (D1–D5 + both bonuses). Each task ticks itself off from what really happened in the simulation (the AI's event log, the panels you opened, |  |

## `07_Deliverables/`
_Project ARES D1–D6 inside the game: design pages, D4 ethics report, dimensioned floor plan._

| Script | What it does | Why (chat or rubric) |
|---|---|---|
| `DesignDocs.cs` | Project ARES deliverables, inside the game, page by page (menu → "ARES deliverables D1–D4", or key K). Every rubric bullet has a page, and most pages show LIVE numbers from the running AI engine so the judges | Chat: 'answer all the questions based on the rubric' — every rubric bullet has a page. |
| `EthicsReport.cs` | D4 written ethics report (800–1,200 words). Shown in the game over three pages and saved to Reports/D4_Ethics_Report.md. | Rubric D4 written report (1,155 words). |
| `FloorPlan.cs` | D2 ANNOTATED FLOOR PLAN — drawn on top of the big map (M), never in the 3D view (layer 31 = map cameras only). • every zone: tinted area in its ARES colour + label "name · ARES type · ⌀ size · lighting" | Rubric D2: annotated, dimensioned floor plans. |

---
© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
