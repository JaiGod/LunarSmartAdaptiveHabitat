# BLENDER RUBRIC CHECKLIST — Lunar Smart Adaptive Habitat (unity 3 / final prototype)
Sources: [HUNCH] proposal + req doc, [SLIDES] HUNCH slides, [ARES] Project ARES slides+brief, [ANGEL] team notes
Status: [x] done  [~] structure done, detail pending  [ ] to do

## A. Habitat architecture
[x] A1  Rigid metallic lower deck + inflatable upper deck ("pill"/two-phase)            [HUNCH][SLIDES]
[x] A2  2 crew standard, expandable to 4 (4 sleep pods; player = 1 of 4)               [HUNCH]
[x] A3  Net volume 20–120 m3/crew  (built ≈210 m3/crew incl. nodes)                     [SLIDES][ARES]
[x] A4  Lunar south pole site, low sun, Earth on horizon, crater terrain                [HUNCH]
[x] A5  Modular + expandable: berthing ports, 3 nodes + hallways                         [HUNCH]
[x] A6  Two egress paths from every zone (emergency hatches)                             [HUNCH redundancy]
[x] A7  Radiation shielding: printed regolith domes/arches, removable segments           [HUNCH][ANGEL]
[x] A8  Storm shelter: water/polyethylene walls (Medical node + pod water jackets)       [HUNCH][ANGEL]
[x] A9  Dust mitigation: suitports, dust lock w/ EDS panels, sticky mat, porch mat      [HUNCH]
[x] A10 EVA suit port + vehicle (pressurized rover) port                                 [SLIDES]
[x] A11 Power: solar towers + fission surface power ~1 km + PMAD + fuel-cell tanks       [HUNCH]
[x] A12 Comms tower to Earth + LunaNet; thermal radiators                                [HUNCH]
[x] A13 Autonomous maintenance: inspection robot, robot arm, spare-part 3D printer        [HUNCH]

## B. Rooms (first floor / second floor + NASA dimensions, L x W x H)
[x] B1  Airlock 2.0 x 2.0 x 2.4                                                          [SLIDES]
[x] B2  Storage 2.0 x 2.0 x 2.4 (CTB bag racks)                                          [SLIDES]
[x] B3  Laboratory/Workspace 4.0 x 3.0 x 2.4                                             [SLIDES]
[x] B4  Hygiene 2.0 x 2.0 x 2.4 (toilet/UWMS, sink, shower)                              [SLIDES]
[x] B5  Crew Quarters x4, 2.1 x 2.1 x 2.4, with BEDS                                     [SLIDES][USER]
[x] B6  Galley/Wardroom 3.0 x 3.0 x 2.4 + team-meeting table + display                   [SLIDES][USER]
[x] B7  Additional storage on 2nd floor                                                  [SLIDES]
[x] B8  Medical / first aid (adjacent ECLSS)                                             [HUNCH]
[x] B9  ECLSS racks: air revitalization, water recovery, O2 generation, waste           [HUNCH]
[x] B10 Noisy (work/lab) separated from quiet (sleep); hygiene + galley easy access      [SLIDES]
[x] B11 Separate control room / computer room (Command & Comms node)                     [HUNCH]

## C. ARES six psychological zones
[x] C1  Communal zone – warm 2700–3000 K light, shared meals, conflict-protocol layout    [ARES]
[x] C2  Private retreat – acoustically shielded, occupant-controlled, NO camera inside,
        lockable door (lock mechanism modelled), door sensor only                         [ARES]
[x] C3  Work & mission bay – cool 6000–6500 K, airflow, CO2 sensor                        [ARES]
[x] C4  Exercise bay – treadmill+harness, cycle, resistance device, Earth-view screens    [ARES][USER physical health]
[x] C5  Nature/growth bay – red/blue LED racks, hydroponics, scent diffuser               [ARES][HUNCH greenhouse]
[x] C6  Transition corridors – lighting gradient warm→cool                                [ARES]
[x] C7  Observation / virtual windows, VR sanctuary alcove                                [HUNCH][SLIDES]

## D. Greenhouse requirements doc
[x] D1  Personal greenhouse (small unit in each pod)                                      [HUNCH greenhouse]
[x] D2  Communal greenhouse in common area + standalone module                            [HUNCH greenhouse]
[x] D3  ECLSS tie-in (O2/CO2/water ducts), LED lighting, sensors                          [HUNCH greenhouse]

## E. Sensors, smart systems, environment control nodes
[x] E1  FACS cameras in communal/work zones only (coverage cones)                        [ARES]
[x] E2  Voice-prosody microphones in shared zones                                         [ARES]
[x] E3  Motion/proximity sensors at doors + zones                                         [ARES]
[x] E4  CO2 / air-quality sensors per zone                                                [ARES]
[x] E5  Lighting panels, speaker arrays, scent dispensers per zone (control nodes)        [ARES]
[x] E6  Smart-house wiring/sensor network routed visibly (cable trays)                    [HUNCH][ANGEL]
[x] E7  Crew interface panels (wellness / override screen) in pods + wardroom             [ARES bonus B]

## F. Crew & wearables
[x] F1  4 crew figures (IVA) with chest-strapped Wellness/Comm Beacon (HRV, GSR, mic, LED) [USER][ARES]
[x] F2  4 EVA suits docked at suitports, each with beacon + role-colour stripes           [USER]
[x] F3  Wrist band (sleep/HRV) + cortisol patch representation                            [ARES]
[x] F4  Sleep-pod mood lights (emissive strips named per crew for Unity control)          [USER]

## G. Configurations (ARES D2)
[x] G1  Configuration A – nominal layout (collection)                                     [ARES]
[x] G2  Configuration B – conflict protocol: deployable acoustic partitions, split dining [ARES]

## H. Build quality / delivery
[x] H1  Every part a separate, numbered object, grouped in collections                    [USER]
[x] H2  Texture-baked materials (realistic + a bit unique: gold MLI / bronze accents)     [USER]
[x] H3  Export: full FBX for Unity + per-part kit (FBX/STL) + assembly/rebuild guide      [USER]
[x] H4  3D-print version on 2 ft x 2 ft board, removable roofs/floors, separate
        electronics box, Arduino kit list                                                 [SLIDES][ANGEL]
[x] H5  Renders for presentation (exterior, cutaway, each room)                           [ARES D5]
[x] H6  Real-world scale (1 unit = 1 m), 1/6 g handled in Unity                           [USER]


## POINTS MAP (240 total — slide version supersedes brief's 200+bonus)
| Rubric category          | Pts | What Blender must supply                                                  |
|--------------------------|-----|---------------------------------------------------------------------------|
| AI response protocols    | 50  | Control nodes per zone (E5) so every intervention (light/audio/thermal/   |
|                          |     | scent/air/social) has a visible actuator; mood lights F4; wardroom B6     |
| Ethics & limits          | 40  | No-camera private pods + lock + door-only sensor (C2); visible override   |
|                          |     | panels (E7); camera coverage limited to shared zones (E1)                 |
| Sensor architecture      | 35  | All 8 ARES sensors physically placed: wearables on crew (F1,F3), mics,    |
|                          |     | FACS cams, motion, CO2 (E1–E4) + redundant pairs for graceful degradation |
| Spatial design           | 35  | 6 ARES zones (C1–C7) w/ dimensions, Config A + Config B (G1,G2),          |
|                          |     | top-down floor-plan renders w/ sensor coverage + annotations              |
| Presentation             | 25  | H5 renders: exterior, cutaways, each room, conflict-protocol comparison   |
| Reflection journals      | 15  | (written by each student — not a Blender item)                            |
| Tabletop simulation ★    | 20  | Scene snapshots of 2 simultaneous events (pod lights + wardroom state)    |
| Interface mockup ★       | 20  | In-world crew interface screens (E7) matching the Unity UI                |


## FINAL STATUS (model)
All A–H items built and verified.
Extra: 5th spare EVA suit; Recreation Commons + Crew Support/Hygiene-2 nodes; loop hallways;
every door ≥0.9 m wide x 2.1 m, hatches 1.2 x 2.1 m, floor-flush; walk test passed on all routes;
lower deck clear height 2.43 m; 560 objects tagged for Unity interaction; crew = 4 (player + 3 AI).
Remaining (outside Blender): Unity 3 project build (waiting for go-ahead), written deliverables D1–D6 + tabletop sim + interface mockup.
