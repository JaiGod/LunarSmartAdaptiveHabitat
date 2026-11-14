# LUNA VR

You are the Commander, one of four crew at the lunar south pole. LUNA, the habitat's AI, looks after all of you, including you. It changes light and sound quietly, makes offers you can refuse, and calls a human when something is serious.

LUNA VR runs on the **same engine as the 2D LUNA game** ([`LUNA_2D_Game`](../LUNA_2D_Game)). That engine handles sensing, triage, consent-based offers, forecasts, the lighting simulation, smart beds, dust and the events. In the 2D game you play LUNA. Here you live inside the habitat it runs.

- **Play in the browser:** https://jaigod.github.io/LunarSmartAdaptiveHabitat/vr/
- **VR on Meta Quest:** open that link in the Quest browser and press **Enter VR**.
- **PC VR (Quest Link / Air Link, any OpenXR headset):** [`vr/LUNA_VR_Windows.zip`](../vr/LUNA_VR_Windows.zip)
- **Why every model exists:** [`Docs/LUNA_VR/MODEL_MANIFEST.md`](../Docs/LUNA_VR/MODEL_MANIFEST.md). Blender-ready `.glb` files are in [`Models/LUNA_VR_GLB`](../Models/LUNA_VR_GLB).

## What happens in the 3-day mission

| Day | You decide | Where |
|---|---|---|
| 1 | Lander plume dust storm: dust shield, robot, your own EVA, or nothing · Movie night | Ops console · Social hub |
| 2 | CO₂ scrubber fault (swap the cartridge yourself?) · Leo and Ana argue at lunch (Floor plan B?) | Ops console · Social hub |
| 3 | Weekly human review of LUNA · "You changed my lights?" · Solar particle event (shelter!) · Mission control asks for Sam's data (the "send without telling him" option is blocked by hard limit 1) | Ops console · Medical |

Every day you can also check in at the mood kiosk, eat with the crew, tend crops, exercise and read your sleep. Throughout, LUNA's offers pop up and you can say yes, no, or *"Why are you asking?"* to see the sensor evidence.

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
Tools: `tools/simtest.gd` (headless 3-day run), `tools/export_glb.gd` (model export), `--autoplay=<dir>` (the whole mission played by script), `--shots=<dir>` (screenshots of every room).

© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
