# Crew Health Sim (Godot 4)

**Play in the browser:** https://claude.ai/artifact/XvN6tgr64TKRe2oBUM24Yn

This is the third Project ARES simulator. It's a 3D first-person game (no VR) about one thing: **how the habitat can tell how an astronaut feels.** You live a mission day in a lunar module. Six health devices watch over you, and your choices (sleep, exercise, EVA, meals, calls home) change your body and your mood.

![Fusion screen](../Docs/Godot_Crew_Health_Sim/screenshots/feel.jpg)

## What's in it

| Device / system | Where | What you do | What it measures |
|---|---|---|---|
| Smart sleep bunk | Crew quarters (private) | **E** to sleep. Time runs fast (**F** even faster, **Space** to wake) | Sleep stages from the mattress sensor, plus the circadian mood light |
| Night wristband | Shelf by the bunk | Put it on before bed | Light, deep (N3) and REM sleep, wake-ups, sleep score, hypnogram |
| Crew Health Station | Medical | Daily check-in | Live 2-lead ECG, blood pressure, SpO2, temperature, HRV |
| Zone sensor pod | Wardroom ceiling | Mood detector panel | Voice + face give valence and arousal. It only runs in shared zones, on the device, with a consent switch |
| Cortisol patch | Wardroom table | Replace it every 48 h | Stress hormone vs. the expected value for the time of day |
| Crew beacon | Worn on the chest (dock on the shelf) | Always on | Heart rate, HRV, skin conductance |
| EVA suit | Airlock | Suit up and go outside | Suit heart rate + blood pressure detector, O2 tank, helmet CO2, suit temperature, metabolic rate |
| **Fusion screen** | **TAB**, anywhere | Read it | All devices weighed into one read-out ("Calm and steady", "Tired", "Stressed", "Low mood", "Working hard on EVA"…), with a confidence score, how many devices agree, why, and a suggestion |

The habitat itself has:
- **Wires**: a ceiling cable tray running all the way round, with colour-coded power, data, fibre and sensor-bus cables, cable ties, hangers, and service loops dropping to wall junction boxes.
- Cables from the tray down to every device: the bunk, the health station (plus its ECG lead and BP hose), the console, the suit umbilical and the treadmill bungees.
- Power conduit to the wall outlets, and a solar-mast power cable on the surface.
- Circadian LED rings (cool white by day, amber at night) and a red CO2 alarm light.
- A treadmill, a crew console (meal, call home, work block, CO2 drill), and a lunar surface at 1.62 m/s² with 3 rock samples to collect.

## Project ARES rubric in the game (P = ARES panel, R = rubric checklist)

| Rubric part | Where to try it |
|---|---|
| D1 sensors | Dashboard tab: 9 sensors, weight table, thresholds, mass/power. Dock the beacon or let the patch expire to see degradation. Threshold check tab: the same AI runs 36 simulated 72-hour missions and shows what it caught, how fast, and the unwanted offers on healthy days |
| D2 spatial design | Habitat tab: floor plan, shared vs private zones. Configuration A/B only with consent. Each zone has its own light (warm wardroom, cool work corner, bright exercise). The quarters door light only shows free / resting, never mood |
| D3 response protocol | Protocols tab: every condition from detect to monitor, with evidence. Tiers 0.35 / 0.55 / 0.75 run live; Tier 2 offers appear on screen (Y / N). Tier 3 escalation chain with response times |
| D4 ethics | Hard limits tab (8 limits, Test buttons). O = AI off switch. Your data tab: log, pause, sensor opt-out, delete |
| Board Q1–Q6 | Board tab: each question sets up the situation in the game |
| Bonus A / B | Tabletop tab (72-hour run) · Your data tab (crew interface) |
| Research proposal | Daily journal language check (crew console) · Model tab (LR / RF / SVM results, sensitivity test) · Habitat tab (zones and why each one is there) · Report tab · greenhouse and VR countermeasures |

## The body model (`scripts/astro.gd`)

Simulated minute by minute:
- Sleep pressure and the circadian clock.
- Sleep debt.
- Stress (acute and chronic).
- Mood (valence/arousal).
- Hunger, connection with home, and cabin CO2.

From these it derives:
- HR, HRV, BP, SpO2, breathing rate, core and skin temperature, and skin conductance.
- Cortisol, with an awakening response and a daily curve.

Sleep runs in 90-minute cycles: deep sleep comes early in the night and REM later. High CO2, stress or the wrong time of day cause wake-ups.

`scripts/fusion.gd` turns each device's reading into stress, fatigue and mood votes. Each vote is weighted by how trustworthy the device is right now: a worn beacon counts more than a stale check-in, and the mood pod only counts with consent. The result is one answer.

## Controls

WASD move · mouse look (click to capture) · **E** use · **TAB** how you feel · **J** tasks · **H** help · **Shift** run · **Space** jump · **[ ]** time speed · **Esc** free the mouse

## Build

- Godot **4.3**, Compatibility renderer, so it runs in WebGL 2.
- Open `project.godot`, or export from the command line:

```
godot --headless --path . --export-release "Web" ../godot/index.html
```

The web export uses the no-threads template, so it runs on GitHub Pages without special headers.

© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.

## Testing

`godot --path . -- --autotest` plays the game by itself: it aims at and uses every object, opens every panel and tab, presses the buttons, plays all 5 health cases, runs the threshold check, does two EVAs, falls off the map, sleeps and wakes. It prints `BUG:` lines and ends with `bugs=0` when everything works.
