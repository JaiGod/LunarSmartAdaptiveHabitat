# UNITY 3 — GAME RUBRIC CHECKLIST (self-grading)  — Lunar Smart Adaptive Habitat VR Simulator
Rule: an item is [x] only after it is built AND verified in the self-test (log/screenshot).
Points per ARES slide (240 total). Crew = 4 (player Commander + 3 AI). 1/6 g. Single-player VR + desktop fallback.

## U0  Start experience
[ ] U0.1  Main menu = vibrant "inside a glowing box" VR room, world-space panels, works with controllers + mouse
[ ] U0.2  Menu options: Start Mission · Guided Zone Tour · Board Scenarios Q1–Q6 · Tabletop Simulation · Crew Interface Demo · Settings · Credits (Jaineil R., Angel C., Farhan K.) · Quit
[ ] U0.3  Settings: smooth/teleport, snap turn, time speed, comfort vignette, sim crew count fixed at 4

## U1  Sensor architecture (35)  → detection layer in code
[ ] U1.1  8 sensors modelled (HRV, GSR, sleep, voice prosody, FACS, motion/proximity, cortisol, CO2) with signal, thresholds, mass & power
[ ] U1.2  Sensor-to-condition weight matrix drives detection (visible in AI Dashboard)
[ ] U1.3  False-positive handling: exercise/EVA context suppresses HRV/GSR (board Q1)
[ ] U1.4  ≥2 failure modes per sensor injectable (dropout, drift/noise) + graceful degradation (weights renormalise, confidence ↓, never false alarm) (Q4)
[ ] U1.5  Sensor health matrix on dashboard

## U2  Habitat spatial design (35)
[ ] U2.1  All 6 ARES zones exist as Unity zones with psychological purpose text + correct lighting (2800K communal, 6500K work, energise exercise, grow bay, private occupant-controlled, corridor gradient)
[ ] U2.2  Player zone detection + zone info card (tour mode)
[ ] U2.3  Configuration A ↔ B (conflict protocol) toggle live (acoustic screens, second dining point, alternate route light)
[ ] U2.4  Floor-plan view (overhead) with sensor coverage overlay

## U3  AI response protocols (50)
[ ] U3.1  All 6 conditions detected per crew: anxiety, depression, conflict (dyad), cognitive fatigue, autonomy deprivation, sensory monotony
[ ] U3.2  Every protocol has all 9 stages: inputs · weighting · duration · triage · per-tier interventions · notification/consent · monitoring window · resolution · escalation
[ ] U3.3  Three tiers: T1 silent/subtle, T2 offer-not-alert notification w/ options, T3 CMO escalation + crew always informed
[ ] U3.4  Monitoring windows: anxiety 20 min, depression 48–72 h, others defined
[ ] U3.5  Environmental levers actually change the world: lighting CCT/lux, audio (soundscape/binaural), thermal, scent, air/CO2, social/routine (meals, meetings, Earth calls)
[ ] U3.6  Depression uses behavioural activation (greenhouse/exercise nudges), not just calming
[ ] U3.7  Escalation fast + backup chain if CMO unavailable (Q6) with max response times per tier
[ ] U3.8  Simultaneous conditions handled (Q2)

## U4  Ethics & hard limits (40)
[ ] U4.1  EthicsGuard enforces hard limits in code (no data sharing w/o consent, refusal stops & logs, no medical/safety-system actions, inform crew before mission control, only reversible changes, human review every 7 sim-days)
[ ] U4.2  Pods: no camera, AI acts only on invitation (AI-access switch), door sensor only
[ ] U4.3  Refusal → stop + log, at every tier; transparency log viewable; "why did the AI do this" explanation (Q3 recovery)
[ ] U4.4  Opt-out controls with safety floor + explanation (Q5)
[ ] U4.5  Individual vs crew: AI protects others without revealing private state (e.g., room re-routing, schedule changes)
[ ] U4.6  Public beacons/pod-door lights never reveal mood (only comm/occupied status unless wearer shares)

## U5  Presentation support (25)
[ ] U5.1  Guided tour narrates each zone + design rationale
[ ] U5.2  Board scenario mode runs Q1–Q6 live with on-screen explanation
[ ] U5.3  Spectator/desktop view works without headset

## U6  Tabletop simulation ★ (20)
[ ] U6.1  72-hour window, two simultaneous scripted events (+ sensor failure + CMO on EVA), fast-forward
[ ] U6.2  Timeline log, what succeeded/failed, redesign notes → report file written (Reports/)

## U7  Interface mockup ★ (20)
[ ] U7.1  Crew Interface panel (pod screen + wrist/Tab): my wellness data, AI actions on my behalf, override any active intervention, preferences
[ ] U7.2  Privacy controls, complete override, opt-out pathway, request my full log

## U8  Required features from Jaineil
[ ] U8.1  4 sleep pods, mood lights blink by mood + physical health countermeasure (exercise/band)
[ ] U8.2  Team meetings (daily planning conference + player-called) at wardroom table
[ ] U8.3  Crew wear Wellness & Comm Beacon; EVA suits only for outside (don at suitport, 4 + spare)
[ ] U8.4  1/6 gravity, VR single player, AI crewmates move on schedules
[ ] U8.5  Exports & builds: Windows .exe (desktop + PC-VR) and Quest .apk path

## U9  Quality gate
[ ] U9.1  0 compile errors, 0 runtime exceptions in self-test log
[ ] U9.2  Self-test screenshots: menu, habitat interior, pod lights, dashboard, crew interface, tabletop report
