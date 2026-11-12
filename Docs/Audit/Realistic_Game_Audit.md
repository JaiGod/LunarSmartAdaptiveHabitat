# Realistic game · object audit (keep / merge / cut)

The test for every object: **which line in the research proposal does it answer?** Nothing here has been deleted yet. Raw inventory of the scene (4,497 parts, read-only scan): [`realistic_scene_inventory.txt`](realistic_scene_inventory.txt).

Proposal zones: Crew Quarters · Galley · Hygiene · Workstations · Medical · Storage · ECLSS · Airlock / dust mitigation. Crew: **2 standard, expandable to 4**.

## Cut or merge (needs your OK)

| # | What | Why it's a problem | Proposal says | Recommendation |
|---|---|---|---|---|
| 1 | **8 sleeping places for 4 crew**: Q1–Q4 quarters in the main hab (bed, desk, mini-greenhouse, locker…) **and** SQ1–SQ4 sleep domes (sleep pod, rest area, desk, health robot, exercise…) | Two complete sets of bedrooms | Private quarters for 2, expandable to 4 | **Keep SQ1–SQ4 domes** (they are the "four private domes" on the website, have the health robot and match the floor plan). **Turn Q1–Q4 off.** Point the smart-bed wraps and the bed task at the SQ pods |
| 2 | Extra beds added by scripts: smart **sleep bunk** (health rounds) + **smart_bed** wraps | A 3rd and 4th kind of bed | Same | Drop the spawned sleep bunk; the sleep-bunk round reads the SQ1 pod instead |
| 3 | **Exercise ×4 places**: D40 treadmill, D45 bike, D48 resistive device, plus resistance bands in Q1–Q4, plus an exercise corner in every SQ dome | Duplicate gym | Exercise as a countermeasure | Keep the D-module gym (treadmill + bike + resistive = the ISS set). Cut the bands (go with Q1–Q4) and the SQ exercise corners |
| 4 | **Lounges ×3**: upper lounge (W32–34), CB10–12 lounge set, communal area (W40–42) | Three lounges for four people | Scheduled group activities | Keep **Recreation (R module)**: game table, family call booth, garden wall. Cut the upper lounge and the CB lounge. Merge W40–42 into the wardroom |
| 5 | Recreation extras: sofas ×2 (R33–34), movie screen (R30–31), guitar (R41), music (R39–40) | Furniture with no sensor or protocol link | — | Keep **one sofa + the screen** (used by the Tier 2 "movie night" offer). Cut the guitar and music props |
| 6 | **134 unnamed parts** (pCube, pCylinder, polySurface, group13…group75) | Leftover Maya imports, nobody knows what they are | — | Cut (first I'll screenshot where they are) |
| 7 | **Spare suits**: EVA5 spare + A93/A94 spare suits | 3 spares for 4 crew | — | Keep **one** spare (EVA5) |
| 8 | **Health devices doubled**: heart-rate monitor, BP monitor, medical scanner, AI health hub, health station, health robot ×4 | Six things measure the same vitals | Medical zone + biomedical monitoring | Keep the **Crew Health Station** (vitals) + **H40 medical bay / exam couch** + **SQ health robot** (morning check). Fold the heart-rate and BP monitors into the health station |
| 9 | **Mood devices doubled**: mood tracker (wall) + zone sensor pod + hologram companion | Three mood check-ins | AI mood tracking | Keep the **zone sensor pod** (sensing) + **hologram companion** (the virtual companion from proposal §2A). Cut the wall mood tracker |
| 10 | **Wristbands ×2**: AI wristband display + night wristband | Same device twice | One wearable | Keep the **night wristband**; cut the AI wristband display |
| 11 | **Maintenance robots ×3**: E41 inspection robot, E38 robot arm, spawned maintenance drone | | Robotic maintenance (§1C) | Keep the **E41 inspection robot** + **E38 arm**. Cut the spawned drone |
| 12 | **Consoles**: N30/N33 command consoles + spawned habitat console + AI health hub | | AI dashboard + smart building | Keep the **N-module command consoles** (they become the smart-habitat dashboard). Cut the spawned habitat console |
| 13 | **Hygiene ×2**: G30–48 "Hygiene2" + LH hygiene (WHC, UWMS, shower) | Two full bathrooms | Hygiene zone | Keep **LH** (ISS-style WHC + UWMS, next to ECLSS for water recovery). Keep G-module laundry + recycler. Cut the Hygiene2 stalls |
| 14 | **HoloMap**: a hologram copy of the whole base (2,038 parts) | Costs about 45 % of all parts | Workflow / wayfinding | Keep, but swap it for a low-detail version (≈150 parts) |

## Keep: directly tied to the proposal

| Keep | Proposal line |
|---|---|
| ECLSS racks LE/LF (CDRA, OGA, trace contaminant, Sabatier, water) | §5c ECLSS: air revitalization, water recovery, O2 |
| Greenhouse C40–C48 (grow racks, hydroponics, harvest, scent, condensate to ECLSS) | Greenhouse requirements §2, §4 |
| Airlock A50–A73: suitports, dust lock, airlock, suit maintenance LS20–22 | Airlock / dust mitigation |
| Storage W30–31, LS CTBs, logistics LOG | Storage / logistics |
| Workshop E30–E40: 3D printer, spare parts, workbench | §1C automated maintenance, 3D printing |
| Lab B40–B50 (benches, glovebox, microscope, workstations) | Workstations |
| Medical H40–H49 + CMO station | Medical / first aid |
| Storm shelter H30–H35 | Radiation protection |
| Wardroom + galley W01–W19 | Galley, scheduled group activities |
| VR retreat D61–D65, Earth view D51–52 | VR for relaxation (§5d) |
| Family call booth R50–R55 | Countermeasure for isolation |
| Sensors X_* (CO2, FACS, motion, voice, thermal, lighting, scent) | AI monitoring; FACS + voice in shared zones only |
| Lighting controllers (28 actuators) | Adaptive lighting (§2A) |
| Power: solar towers, fission (FSP), regenerative fuel cells, radiators | §5c power management |
| Comms tower, rovers (LTV, pressurized rover), RASSOR | EVA + ISRU (regolith), outside the habitat |

## After the cleanup

- Every remaining object keeps a "Why it's here" line (the same as the Godot game), shown on its info card.
- Fewer parts → faster in VR and in the browser.
- Nothing is deleted from the original model files. Objects are switched off in the scene and stay in the .blend / .fbx, so all of this can be undone.
