# LUNA — Lunar Adaptive Neural Assistant

A 2D pixel-art simulation for NASA HUNCH / Project ARES. You are LUNA, the AI of a lunar south-pole habitat. Keep four astronauts psychologically well for 90 days without becoming the thing that makes them feel watched.

Godot 4.3, GDScript. All art, the pixel font and the simulation are original and live in this folder.

## How it maps to the Project ARES brief

| Brief | In the game |
|---|---|
| 6 conditions | Anxiety, depression, conflict, cognitive fatigue, autonomy deprivation, sensory monotony. Each crew member has a hidden true state; LUNA only sees sensor estimates with a confidence |
| D1 sensor matrix | Launch screen: fly sensors within 8 kg / 60 W ($1M per kg). Each has a signal, a threshold and 2 failure modes. Design book → D1 |
| Failure modes | Cortisol patches fail on day 45 (Q4); cameras go blind under 150 lux; voice only in shared zones; HR/GSR confused by exercise unless activity context is on (Q1) |
| D2 zones + A/B plans | Rooms in the research order (Airlock → Hygiene → Quarters → Galley → Work → Storage → Life support → Medical) plus exercise, greenhouse and a transition corridor. Floor plan B splits the wardroom and staggers meals |
| Lighting lever | Every zone has a real colour temperature (1800–6500 K) and illuminance (up to 10,000 lux) that fades smoothly; 20:00 blue-light cut-off; 24-hour circadian plan per room; Light overlay shows K and lux |
| Smart beds | Sleep stages in 90-min cycles (N1/N2/N3/REM), hypnogram, wake-ups; dawn light in the pod for 30 min, then a smart alarm that wakes you in light sleep (waking from deep sleep costs alertness) |
| D3 protocols | Launch screen: choose the Tier 1 levers and Tier 2 offer for each condition; the game runs your protocol. Design book shows all 9 flowchart stages, live |
| Tiers | 0.35 / 0.55 / 0.75 with persistence windows (anxiety minutes, depression a day). Tier 1 silent, Tier 2 an offer the crew can refuse, Tier 3 told first then the medical officer → Commander → flight surgeon |
| Prediction | Each person's estimates are tracked and projected forward: "Depression risk in 14 days: 78%". Prevention offers before Tier 2. Uncertainty shown (the proposal's models drop from ~0.83 to ~0.52 accuracy on shifted data, so LUNA never acts on a forecast alone) |
| D4 hard limits | 8 limits, enforced: there is no button that breaks one (locked options show why) |
| Board Q1–Q6 | Day 4 EVA (Q1), day 27 depression + conflict (Q2), day 33 furious crew member (Q3), day 45 patch failure (Q4), day 55 opt-out request (Q5), day 70 Tier 3 while the medical officer is on EVA (Q6) |
| Bonus A | 72-hour tabletop mode with two simultaneous events and an automatic report |
| Bonus B | Crew interface: own data, what LUNA did, stop any change, preferences, per-sensor opt-out, pause, full log |
| Smart habitat | Power (solar + fission + battery), CO₂/O₂, water, food, Mission Garden (greenhouse racks), equipment, robot repairs |
| Dust storms | No wind on the Moon: lander plumes, electrostatic dust at local sunrise and EVA dust. Dust covers the arrays (less power) and leaks in through the airlock |
| Emergencies | Shadow period, scrubber failure, micrometeoroid, solar flare, comms blackout, blight: options with LUNA's predicted outcome |
| Digital twin | Blueprint view with live power, air, water and greenhouse flows and 48-hour graphs |

## Run

- Play: open the project in Godot 4.3 and press F5, or export the "Web" preset.
- Self-test: `godot --headless --path . -s tools/simtest.gd -- mission` plays 90 days with LUNA's recommended choices and prints the outcome.
- Screenshots: `godot --path . -- --shots=<folder>`.
- Art and font are generated: `python3 tools/make_art.py art/` and `python3 tools/make_font.py fonts/luna_pixel.ttf`.

© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
