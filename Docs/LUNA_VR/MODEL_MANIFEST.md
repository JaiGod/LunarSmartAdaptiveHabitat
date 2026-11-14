# LUNA VR · Model manifest

Every model in LUNA VR has to answer one question: **why does it exist?** If we couldn't answer it in a sentence, we cut the model.

Each model below plugs into a real system in the LUNA engine, the same simulation that runs the 2D LUNA game. That engine covers CO₂, dust, sleep, power, crops and each crew member's wellbeing. So using a model changes the game.

All models are built at real scale (metres) and exported as glTF 2.0 (`.glb`) files in [`Models/LUNA_VR_GLB/`](../../Models/LUNA_VR_GLB/). To open one in Blender: **File → Import → glTF 2.0**. The parts of each model are grouped by material.

| Model · file | NASA requirement it answers | Gameplay purpose | How astronauts interact | Why it's necessary |
|---|---|---|---|---|
| **Dust Decontamination Airlock** · `Dust_Decontamination_Airlock.glb` | Lunar dust irritated Apollo crews' eyes and lungs and wore down seals. Suitports and dust removal are NASA's fixes | EVAs start and end here. Dust that comes back in collects here and leaks into the cabin air | Climb into the suit through a suitport, brush your boots, read the particle counter, run the cleaning cycle (electrostatic curtain + HEPA vacuum) | Skip the cleaning and cabin particulates rise, which the engine turns into fatigue |
| ↳ **EVA Prep Bay** (between the suitports) | Suits are serviced between EVAs (O₂ recharge, cooling water, batteries), with a readiness check before going out | Before any EVA you run the pre-EVA check: suit consumables plus LUNA's read on your fatigue, stress and sleep. LUNA advises go or wait; you decide | Prep panel screen; umbilical hoses run to both suitports | EVA is the riskiest thing the crew does, so wellness is checked before it, not after |
| **Personal Crew Pod** ×4 · `Crew_Pod_1.glb` | Private crew quarters and protected sleep (NASA-STD-3001) | Sleep, wake, recover, set your own light and pod colour, call home | Smart bed, desk screen, wardrobe, light and colour controls | Sleep loss and no privacy feed fatigue and conflict |
| ↳ Sleep Quality Analysis Station (pod desk) | Circadian disruption is a known spaceflight risk | Shows last night's sleep stages (hypnogram), hours and wake-ups | Read it; switch the smart alarm and dawn light on or off | The bed is LUNA's best sensor for fatigue and low mood |
| ↳ Personal Hydroponics Cabinet | Plant care helped ISS crews (Veggie) | Private gardening that lifts mood | Tend it | A way to feel better on your own, with no AI involved |
| **Mood Check-in Kiosk** · `Mood_Checkin_Kiosk.glb` | Self-report keeps the AI honest when sensors fail (board question Q4) | Daily 1–5 check-in. LUNA suggests an activity and shows you its read on you | Touch screen inside an acoustic privacy booth. You choose private or shared with the medical officer | Data only by consent, and it makes LUNA's estimate of you more accurate |
| **Reconfigurable Social Hub** · `Social_Hub.glb` | Shared meals and group time build crew cohesion | Meals together, movie night, and splitting the room with the partition when two crew are in conflict (Floor plan B) | Sit at the table or sofa; hub panel switches dining / movie / split | Where connection is rebuilt, and where conflict is managed without forcing people |
| ↳ **Earth Connection Corner** (east half of the hub) | Isolation from Earth is a core long-duration stressor; contact with home protects mood | Sit by the virtual window (live Earth view) and read or answer family messages | Armchairs by the window; family message station screen | The brief's connection lever, made into a place you choose to go |
| **Greenhouse Therapy Racks** + light-pipe dome · `Greenhouse_Therapy_Racks.glb` | Food, O₂ and CO₂ uptake for life support; plant care as therapy | Tend and harvest 8 racks. The engine grows them and counts the food | Tend, harvest, sit on the bench among the plants | Fresh food, cleaner air, and the brief's treatment for low mood |
| **Medical · Water-wall Storm Shelter · VR Recovery Chair** · `Medical_Storm_Shelter.glb` | Medical care far from Earth; water as radiation shielding | Solar-flare event (everyone shelters here). LUNA's offers can send you to the chair, which suggests a calming scene from your stress level | Take shelter in time; accept or decline LUNA's offer; sit in the chair | Emergency response, plus Tier 2 support you can refuse |
| **ECLSS racks + Circadian Lighting Calibration Wall** · `ECLSS_and_Lighting_Calibration.glb` | CO₂ removal, O₂ generation and water recovery (as on the ISS); lighting that protects sleep (the ISS switched to LED lights for this reason) | Scrubber-fault event: swap the sorbent cartridge yourself. One live colour tile per zone shows LUNA's lighting | Read the rack screen, swap the cartridge (uses a spare part), check any zone's light against the day plan | High CO₂ slows thinking; lighting is LUNA's main silent lever and needs a human check |
| **Maintenance Robot Bay + Stores** · `Robot_Bay_and_Stores.glb` | Spares and stowage far from resupply (ISS cargo transfer bags) | The spare-part count is real (repairs use parts); dispatch the robots | Robot bay screen | A repair has to cost something, and you can see what is left |
| **Ops: Digital Twin Table · Trend & Forecast Wall · Emergency Coordination Console · LUNA compute rack** · `Ops_LUNA.glb` | Habitat health monitoring and prediction; humans decide, the AI advises | Find faults on the twin. Read forecasts (habitat data and *anonymous* crew data). Make every event decision with LUNA's predicted outcome per option | Touch the twin; choose at the console | Prediction before failure, without breaking hard limit 1: nobody sees a crewmate's data |
| ↳ **Wellness Operations Center**: live 3D twin plinth + intervention planning station | Crew health monitoring and planning: the AI shows its work and humans plan the response | A 1:30 live twin of the habitat (crew pucks move, rooms glow with their real lighting) and a crew-wide planner (movie night, shared meal, free afternoon, greenhouse session). Also shows whether SELENE is connected | Watch the twin; plan crew-wide interventions at the standing station | The room where the whole project is visible: sensing → AI → human decisions |
| **Exercise Bay** · `Exercise_Bay.glb` | Daily exercise against bone and muscle loss (ISS: T2 treadmill, CEVIS bike, ARED) | Your exercise slot; LUNA's "scenic ride" offer against monotony | Ride, run, lift | Exercise treats low mood, fatigue and conflict in the engine (and in life) |
| ↳ **Wellness Dock**: posture camera arch, finger-clip heart rate / SpO₂, Reaction Self Test | ISS crews take a short reaction-time test (the Reaction Self Test, a psychomotor vigilance task) to catch fatigue | A 1-minute scan plus a 5-trial reaction test. Your result updates LUNA's fatigue estimate, because you chose to take it | Step on the dock; press the trigger (or click) when the light turns green | A direct, measurable fatigue signal that the person controls |
| **Hygiene** · `Hygiene.glb` | Hygiene + water recovery; no voice or camera sensors (privacy rule) | Morning routine | Wash up | Privacy where the brief demands it |
| **Six-legged repair robot** · `athlete_robot.glb` | Based on NASA JPL's six-limbed ATHLETE robot; dust mitigation on the solar arrays | Dust storms coat the arrays and power drops; the robot cleans them | Send it from the robot bay, or do the EVA yourself | Ties power, dust and EVA risk together |
| **Pressurised rover** · `pressurised_rover.glb` | Long surface traverses with suitport entry | Parked asset (future traverse missions) | Seen on EVA | Shows suitports on a vehicle as well as the habitat |
| **Habitat shell modular kit** · `Habitat_Shell_Modular_Kit.glb` | Real habitats are built from modules | Every wall, floor, ceiling, door, cable tray, handrail and sensor in the game | Doors slide when someone is near; lights follow LUNA's lighting simulation | One kit, reused everywhere (how professional games are built) |

**Also outside, built into the scene rather than exported:**
- **Vertical solar array towers** (NASA's VSAT concept for the south pole). Their panels visibly collect the engine's dust.
- **Regolith-bag berms** for radiation and micrometeoroid shielding.
- **Comm dish** pointed at Earth.

### SELENE link: a real sensor for LUNA

LUNA VR can read SELENE, the team's face + voice wellness app, as a live sensor. Run `LUNA_VR_Game/tools/selene_bridge.py` next to SELENE (or `--demo` to test without a camera). It serves three scores on this computer only (`127.0.0.1:8765`), and only the scores leave SELENE: no images, no audio. When it's connected, the screens show "SELENE live" and the scores feed LUNA's stress, fatigue and mood estimate for you. A high stress reading leads to warmer, dimmer light, a breathing offer, and, if it stays high, a Tier 3 escalation that tells you first.

### Blender

Every model above is also built as a native Blender scene, generated from this game's own model code so the two always match. That's 353 objects, one collection per model, each carrying these four answers as custom properties. The pipeline is in [`LUNA_VR_Game/tools/blender/`](../../LUNA_VR_Game/tools/blender/).

### Cut as "AI slop"

- **A glowing "neural sphere".** The AI is software, so it became a plain computer rack: 2 redundant flight computers, 140 W.
- **A rotating "lighting core" machine.** It became the calibration wall, where a human checks every zone.
- **A waterfall wall.** Water is too precious on the Moon.
- **Floating holograms.** They became touch screens and a tilted twin table.

### Built-in privacy

- Shared screens show only habitat data and anonymous crew data.
- Your own readings appear only on your pod desk, the kiosk and your wrist panel.
- Pods and hygiene carry a visible "no cameras · no microphones" label.
