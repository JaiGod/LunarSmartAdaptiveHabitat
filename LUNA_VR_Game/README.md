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
| Fast-forward | F | Y button / wrist panel |

## Build

Godot 4.3, Compatibility renderer (needed for WebXR). Export presets: **Web** (no threads, so it runs on GitHub Pages) and **Windows Desktop** (OpenXR on).
Tools: `tools/simtest.gd` (headless 3-day run), `tools/export_glb.gd` (model export), `tools/selene_bridge.py` (SELENE link), `tools/blender/` (game → Blender scene), `--autoplay=<dir>` (the whole mission played by script), `--shots=<dir>` (screenshots of every room), `--selenetest` (SELENE link check).

© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
