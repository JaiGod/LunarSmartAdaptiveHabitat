# LUNA VR

You are the Commander, one of four crew at the lunar south pole. LUNA, the habitat's AI, looks after all of you, including you. It changes light and sound quietly, makes offers you can refuse, and calls a human when something is serious.

LUNA VR runs on the **same engine as the 2D LUNA game** ([`LUNA_2D_Game`](../LUNA_2D_Game)). That engine handles sensing, triage, consent-based offers, forecasts, the lighting simulation, smart beds, dust and the events. In the 2D game you play LUNA. Here you live inside the habitat it runs.

- **Play in the browser:** https://jaigod.github.io/LunarSmartAdaptiveHabitat/vr/
- **VR on Meta Quest:** open that link in the Quest browser and press **Enter VR**.
- **PC VR (Quest Link / Air Link, any OpenXR headset):** [`vr/LUNA_VR_Windows.zip`](../vr/LUNA_VR_Windows.zip)
- **Why every model exists:** [`Docs/LUNA_VR/MODEL_MANIFEST.md`](../Docs/LUNA_VR/MODEL_MANIFEST.md). Blender-ready `.glb` files are in [`Models/LUNA_VR_GLB`](../Models/LUNA_VR_GLB), and [`tools/blender`](tools/blender) rebuilds the whole habitat as a native Blender scene.

## What happens in the 3-day mission

| Day | You decide | Where |
|---|---|---|
| 1 | Lander plume dust storm: dust shield, robot, your own EVA, or nothing · Movie night | Ops console · Social hub |
| 2 | CO₂ scrubber fault (swap the cartridge yourself?) · Leo and Ana argue at lunch (Floor plan B?) | Ops console · Social hub |
| 3 | Weekly human review of LUNA · "You changed my lights?" · Solar particle event (shelter!) · Mission control asks for Sam's data (the "send without telling him" option is blocked by hard limit 1) | Ops console · Medical |

Every day you can also check in at the mood kiosk, eat with the crew, tend crops, exercise and read your sleep. Throughout, LUNA's offers pop up and you can say yes, no, or *"Why are you asking?"* to see the sensor evidence.

## The wellness rooms

- **Wellness Operations Center** (Ops): a live 1:30 twin of the habitat on a plinth (crew pucks move, rooms glow with their real lighting) and a planning station for crew-wide plans: movie night, a shared meal, a free afternoon, a greenhouse session. Plans apply to everyone, so nobody is singled out.
- **Wellness Dock** (exercise bay): a 1-minute scan plus the Reaction Self Test used on the ISS. Your result updates LUNA's fatigue estimate because you chose to take it.
- **EVA Prep Bay** (airlock): before any EVA, check the suit's consumables and LUNA's read on your fatigue, stress and sleep. LUNA advises go or wait; you decide.
- **Earth Connection Corner** (social hub): sit by the live Earth window and read or answer messages from home.

## Round 3: every object does something

Everything you can use has a glowing ring: **cyan** = press or touch, **green** = grab, sit or use, **amber** = a crewmate you can check on. Point at it and the prompt says what it does (in VR the label floats beside it). This round also brings back the realistic features of the team's earlier NASA HUNCH games, Unity 3 (*Moon_Base_Final*) and *Lunar Habitat Commander*. The aliens, portals and weapons stay out.

| Feature | Where | Rubric |
|---|---|---|
| **Check on a crewmate.** They choose whether to share. Then you see their readings with the sensor each one comes from, the six condition bars, and a **Help** button that suggests the activity that fits | walk up to them, or press **I** | D1, D3, D4 (consent) |
| **AI OFF switch.** LUNA stops, undoes every change it made, and keeps only its safety sensing. Only a human turns it back on | Ops governance wall, **O**, wrist | D4 hard limits, human override |
| **Night watch.** On night 2 the hardware watchdog catches LUNA breaking a bedtime rule, switches it off and wakes you | your pod | D4 |
| **AI dashboard.** Sensor health and mass/power budget, confidence, anonymous tier counts. Break a sensor (graceful degradation, Q4) or test any of the 8 hard limits | Ops | D1, D4 |
| **Review board.** Q1–Q6 run live, D1–D6 pages, rubric checklist, 72-hour tabletop (Bonus A) | Ops, **J** | D5, Bonus A |
| **Team meeting.** The crew comes to the table and makes one-tap team choices | wardroom table | D3 |
| **Safety drill.** Klaxon, and everyone musters in the storm shelter. The time goes in the log | Ops alarm button | D3 |
| **Floor plan A/B.** The conflict protocol: two dining points and a second lit route | Ops selector, **C** | D2 |
| **Room controls.** Light, sound (rain, forest, ocean, café), temperature and scent. LUNA leaves the room alone until you hand it back | social hub, greenhouse, Ops, exercise, medical | D3 levers, D4 override |
| **CMO console.** Tier 3 cases (the person is told first) and the escalation chain when the medical officer is on EVA (Q6) | Medical | D3 |
| **Crew interface.** Your read, opting out of sensors, pausing LUNA for you, exporting or deleting your own log | pod desk → **My data** | Bonus B |
| **Daily wellness tasks**, **zone cards** (each room's ARES zone, light and allowed sensors), **guide me** (a gold path to your next step, **G**; to your bed, **B**) | everywhere | D2, D5 |

New things to use: coffee machine, water station, food warmer, bookshelf, team-meeting terminal, shower, self-check exam bed, the locked medication cabinet (hard limit 3), the storm-shelter kit and muster point, the O₂ generator, spare parts, EVA tools, the greenhouse irrigation pump, your pod's "LUNA may adjust my pod" switch, music and photo from home.

Test it headless: `godot --headless -- --r3test` presses every new control through the real handlers. To take screenshots of every new object, run `godot -- --r3shots=<dir>`.

## Day, evening, sleep, emergency

The whole habitat follows the crew:

- **Awake:** lo-fi music plays (synthesized for the game; **M** turns it off).
- **Evening:** warmer, dimmer light, and the music gets quieter.
- **Sleep:** clicking your bed always means sleep: the whole night after 20:00, a 25-minute nap in the day. The main lights go off and your pod goes fully dark (its screens too, and the grow lights follow a 16 h on / 8 h off cycle). Dim purple path lights come on near the floor, and the music stops. You hear only the air vents, or silence if you choose it. Messages that can wait are held until morning.
- **When everyone is asleep**, the shared rooms switch to night mode to save power.
- **Emergency** (drill, solar particle event): the lights flash red and the music stops.
- **Safety stations** beside the hatches of eight rooms: a water-mist fire extinguisher and a breathing mask (about 15 minutes of oxygen), with a smoke detector overhead, like on the ISS. Click one to check it. The storm-shelter muster point is a painted floor marking.
- **Status lights mean one thing everywhere:** green working, blue standby, yellow warning, red emergency, purple night. Nothing glows just to look cool.

The panel by your pillow sets your pod's light (auto, off, dim, warm, bright), the temperature (18–24 °C; 18–20 °C helps you fall asleep), and the sleep sound. On desktop you watch your pod go dark from your bed, and in the morning the dawn light rises.

## Autopilot, demo and human override

- **T: guided demo.** Ten stops through the whole habitat, one short card each. It moves on by itself after 10 seconds, or press Next.
- **P: autopilot (the AI plays).** It lives an ISS-style day: desk work, two exercise sessions, greenhouse and life-support checks, meals with the crew, a little free time, and bed by 22:00. It checks in on a crewmate and answers LUNA.
- **The person is always in charge.** Any key, click or stick movement takes control back at once. The AI OFF switch stops the autopilot too, and the autopilot can never switch LUNA back on: only a person can. Before it makes a decision for you it shows a 6-second veto window ("Stop, I'll decide").
- **Some things only you can do.** The autopilot never fills in your mood check-in, never takes the reaction test for you (it would fake your fatigue data), and never goes on EVA. It leaves those on your list and tells you.
- **It learns.** It remembers where it got stuck and how long each walk took, and next time picks a better place to stand. Press **L** to see what it has learned (saved on your computer between sessions).

## Realism pass

Realism comes first. Anything that looked good but had no engineering job was redesigned or removed. Everything you can use now states:

- what it does
- why it is aboard (NASA practice, the ARES brief or our research proposal)
- its upkeep
- its resource cost (power, water, mass, consumables)
- its honest limits

The prompt shows the resource line. Press **X** for the info card: job, uses, care, limit, all in short plain words. This follows the brief's §7 rule that every sensor and actuator must be justified in mass, power and mission value.

- **No dust storms.** The Moon has no weather. Dust comes from lander plumes, rover traffic, EVA boots and electrostatic lofting at the terminator.
- **E-readers, not paper books.** Each one is 0.3 kg, and every kilogram costs about $1M to land.
- **A 3 L shower.** That is about what Skylab used per shower.
- **An espresso machine.** One like it, the ISSpresso, flew in 2015.
- **A 3D printer in the maintenance bay**, as in our research proposal. It prints non-structural spare parts from recycled plastic, and the feedstock runs out.
- **The Earth view is a virtual window.** The habitat sits under regolith shielding, so it is a display fed by a mast camera.

**Known gap:** the 3D floor plan is about 700 m², several times the size of real analogs. CHAPEA is about 158 m² for 4 crew. A compact two-deck layout is the next step.

## SELENE link (real face + voice sensing)

LUNA VR can use SELENE, the team's face + voice wellness app, as a live sensor.

1. Copy [`tools/selene_bridge.py`](tools/selene_bridge.py) into the SELENE folder (next to `selene_app.py`).
2. Run `python selene_bridge.py` instead of `python selene_app.py`, and do a check-in in SELENE.
3. Play LUNA VR on the same computer (browser or PC build). The screens show **SELENE live** and LUNA reacts to your real readings.

No SELENE or camera? Run `python selene_bridge.py --demo` (or `--demo --stress 0.85`). Only three scores leave SELENE, with no images and no audio, and only on this computer (`127.0.0.1:8765`).

## Controls

| | Desktop | VR |
|---|---|---|
| Move | WASD (Shift to run) | left stick |
| Look / turn | mouse | head, right stick snap-turn |
| Use things, click screens | click or E | trigger |
| Answer LUNA | Y / N / H (why?) | A = yes, B = no, or point and click |
| Guide me · bed path · check in | G · B · I | wrist panel: Guide me |
| AI OFF · floor plan A/B · rubric · zone card · all keys | O · C · J · Z · K | wrist panel: AI OFF, Rubric |
| Fast-forward | F | Y button / wrist panel |
| Guided demo · autopilot · what it learned | T · P · L | wrist panel: Demo, Autopilot |

## Build

Godot 4.3, Compatibility renderer (needed for WebXR). Export presets: **Web** (no threads, so it runs on GitHub Pages) and **Windows Desktop** (OpenXR on).
Tools: `tools/simtest.gd` (headless 3-day run), `tools/export_glb.gd` (model export), `tools/selene_bridge.py` (SELENE link), `tools/blender/` (game → Blender scene), `--autoplay=<dir>` (the whole mission played by script), `--shots=<dir>` (screenshots of every room), `--selenetest` (SELENE link check).

© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
