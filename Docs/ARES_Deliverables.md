# Project ARES — Lunar Smart Adaptive Habitat · deliverables D1–D4
© 2026 Jaineil R. — with thanks to Angel C. and Farhan K. Generated from the running game Day 1 07:57


## D1 Sensors

### D1 · Sensor-to-condition matrix — overview

Every kilogram to the lunar surface costs about $1 million, so each sensor has to earn its place.

Sensor                        Qty   kg each   W each   feeds
Heart-rate variability         4     0.03     0.05   4 conditions
Galvanic skin response         4     0.02     0.05   1 conditions
Sleep architecture             4     0.03     0.05   3 conditions
Voice prosody                  8     0.25     2.50   6 conditions
Facial action coding           7     0.35     4.00   5 conditions
Movement / proximity          28     0.15     1.20   6 conditions
Cortisol patch                 4     0.01     0.01   1 conditions
CO2 / air quality             17     0.12     0.80   1 conditions
Task performance (software)    0     0.00     0.00   3 conditions

Installed: 11.1 kg · 96 W  (≈ $11.1 M launch cost · 0.2% of the 40 kW fission supply)
Cortisol patch consumables for a 180-day mission: 4.8 kg more (4 crew, one patch per 36 h).
Half the hardware is SHARED with systems the habitat needs anyway (beacon comms, intercom, safety cameras, ECLSS air sensors), so the real added cost is lower.

What NOT to fly: no EEG headbands (heavy, uncomfortable), no cameras in private pods, no continuous audio recording.
Next pages: one per sensor → signal & processing, conditions, threshold, failure modes, mass/power justification.


### D1 · Sensor 1/9 — Heart-rate variability

Where: Beacon + wristband   ·   Qty 4
Signal & processing: R-R intervals from a 2-lead ECG in the beacon (250 Hz) → RMSSD and LF/HF over rolling 5-min windows, compared with the crew member's own 14-day baseline.
Contributes to: Anxiety (30%), Depression (5%), Interpersonal conflict (10%), Cognitive fatigue (15%)
Trigger threshold: RMSSD drop >25% vs personal baseline for >3 min  (always against the person's OWN baseline, never a population average)
Failure modes:
  • Strap loose → dropout
  • exercise → false stress
How the system copes: Activity/EVA context flag cuts HRV weight to 25%; a dropout is detected from signal quality and the weight renormalises onto GSR + voice.
Mass & power: 30 g, 0.05 W each → 0.12 kg / 0.2 W in total. Highest-value sensor: feeds 5 of 6 conditions and is also the vital-sign safety floor. Lives in the beacon the crew already wear for comms.
Crew can opt out: no — safety floor, used only for life-threatening alarms

LIVE status: (you) OK  Okafor OK  Tanaka OK  Rivera OK


### D1 · Sensor 2/9 — Galvanic skin response

Where: Beacon (chest)   ·   Qty 4
Signal & processing: Skin conductance (µS) at 4 Hz → phasic peak count per minute after a 0.05 Hz high-pass removes the slow tonic drift.
Contributes to: Anxiety (25%)
Trigger threshold: Skin-conductance peaks >3/min above baseline  (always against the person's OWN baseline, never a population average)
Failure modes:
  • Sweat/exercise saturation
  • habituates to chronic stress
How the system copes: Only counted when HRV agrees (2-signal rule); exercise context suppresses it; chronic-stress decisions rely on cortisol instead.
Mass & power: 20 g, 0.05 W each → 0.08 kg / 0.2 W in total. Cheapest acute-arousal signal; shares the beacon's battery and housing, so almost zero extra mass.
Crew can opt out: yes, any time, no reason needed

LIVE status: (you) OK  Okafor OK  Tanaka OK  Rivera OK


### D1 · Sensor 3/9 — Sleep architecture

Where: Wristband (night)   ·   Qty 4
Signal & processing: Wrist accelerometer + PPG at night → sleep/wake epochs (30 s), stage estimate (light/deep/REM), efficiency, latency and number of awakenings.
Contributes to: Anxiety (10%), Depression (25%), Cognitive fatigue (20%)
Trigger threshold: Sleep efficiency <80% or REM <15% for 2 nights  (always against the person's OWN baseline, never a population average)
Failure modes:
  • Not worn at night → no data
  • pod light leaks
How the system copes: "Not worn" is detected and the AI asks once, never nags; depression still has voice, FACS, movement and cortisol.
Mass & power: 30 g, 0.05 W each → 0.12 kg / 0.2 W in total. Sleep is the earliest marker of depression and fatigue; the wristband doubles as the dawn-alarm.
Crew can opt out: yes, any time, no reason needed

LIVE status: (you) OK  Okafor OK  Tanaka OK  Rivera OK


### D1 · Sensor 4/9 — Voice prosody

Where: Shared-zone mic arrays, on-device   ·   Qty 8
Signal & processing: On-device DSP on shared-zone mic arrays → pitch mean/variance, speech rate, energy and pause ratio. Audio is never stored or sent; only 4 numbers per minute leave the device.
Contributes to: Anxiety (20%), Depression (20%), Interpersonal conflict (30%), Cognitive fatigue (10%), Autonomy deprivation (20%), Sensory monotony (20%)
Trigger threshold: Pitch variance −30% (flat) or +40% (tense); speech rate shift >20%  (always against the person's OWN baseline, never a population average)
Failure modes:
  • Ambient noise / mask → low SNR
  • crew turns mic privacy off
How the system copes: Noise gate: a minute with low SNR is discarded, not scored; mic privacy switch = crew choice; voice never used alone for Tier 2+.
Mass & power: 250 g, 2.50 W each → 2.00 kg / 20.0 W in total. One array per shared zone also gives voice control and intercom, so the mass is shared with comms.
Crew can opt out: yes, any time, no reason needed

LIVE status: (you) OK  Okafor OK  Tanaka OK  Rivera OK


### D1 · Sensor 5/9 — Facial action coding

Where: Shared-zone cameras only   ·   Qty 7
Signal & processing: On-device vision in shared zones → action-unit frequencies (AU4 brow lowerer, AU12 smile, AU15 lip-corner depressor) per hour. No images are kept.
Contributes to: Anxiety (10%), Depression (15%), Interpersonal conflict (20%), Autonomy deprivation (20%), Sensory monotony (20%)
Trigger threshold: AU4/AU15 (sad/tense) frequency 2σ above baseline  (always against the person's OWN baseline, never a population average)
Failure modes:
  • Low light → no detection
  • occlusion
How the system copes: Low-light frames are skipped (no guess); camera only in shared zones, never in pods; never sole evidence.
Mass & power: 350 g, 4.00 W each → 2.45 kg / 28.0 W in total. Heaviest psych sensor, so limited to 6 shared zones where it also acts as the safety camera; excluded from pods to save mass and protect privacy.
Crew can opt out: yes, any time, no reason needed

LIVE status: (you) OK  Okafor OK  Tanaka OK  Rivera OK


### D1 · Sensor 6/9 — Movement / proximity

Where: UWB beacons + door sensors   ·   Qty 28
Signal & processing: UWB tags in the beacons + door sensors → room occupancy, time-in-pod, step count and who-was-near-whom (dyad co-location minutes).
Contributes to: Anxiety (5%), Depression (20%), Interpersonal conflict (40%), Cognitive fatigue (5%), Autonomy deprivation (30%), Sensory monotony (25%)
Trigger threshold: Dyad co-location −50% over 24 h; time-in-pod +60%  (always against the person's OWN baseline, never a population average)
Failure modes:
  • UWB drift in metal modules
  • beacon battery flat
How the system copes: Anchors re-surveyed weekly; beacon battery alert at 20%; movement is a slow, 24 h signal so short drift doesn't matter.
Mass & power: 150 g, 1.20 W each → 4.20 kg / 33.6 W in total. UWB anchors also run door control and the 'find my crewmate' safety function in an emergency.
Crew can opt out: yes, any time, no reason needed

LIVE status: (you) OK  Okafor OK  Tanaka OK  Rivera OK


### D1 · Sensor 7/9 — Cortisol patch

Where: Skin patch, 24–48 h   ·   Qty 4
Signal & processing: Microneedle sweat patch → cortisol level sampled 4×/day; the morning peak is compared with the person's own baseline.
Contributes to: Depression (15%)
Trigger threshold: Morning cortisol >2σ for 3 days  (always against the person's OWN baseline, never a population average)
Failure modes:
  • Patch expires (24–48 h)
  • resupply gap
How the system copes: Patch expiry is known ahead → reminder 6 h before; on failure the weights renormalise and confidence drops (shown on dashboard).
Mass & power: 10 g, 0.01 W each → 0.04 kg / 0.0 W in total. Only objective chronic-stress marker; tiny mass, but consumables add up over a mission (see totals), so it's the first sensor to drop if mass is cut.
Crew can opt out: yes, any time, no reason needed

LIVE status: (you) OK  Okafor OK  Tanaka OK  Rivera OK


### D1 · Sensor 8/9 — CO2 / air quality

Where: Every zone, redundant pairs   ·   Qty 17
Signal & processing: NDIR CO2 + humidity + PM sensors in redundant pairs → ppm per zone every 10 s; the two readings must agree within 50 ppm.
Contributes to: Cognitive fatigue (20%)
Trigger threshold: >600 ppm in occupied zone  (always against the person's OWN baseline, never a population average)
Failure modes:
  • Sensor drift (needs calibration)
  • single-point failure
How the system copes: Paired sensors cross-check; disagreement >50 ppm → flag for calibration and use the ECLSS reading; CO2 safety alarm is separate and human-owned.
Mass & power: 120 g, 0.80 W each → 2.04 kg / 13.6 W in total. Mandatory for life support anyway; the psych system just reads the same data (no added mass).
Crew can opt out: no — safety floor, used only for life-threatening alarms

LIVE status: (you) OK  Okafor OK  Tanaka OK  Rivera OK


### D1 · Sensor 9/9 — Task performance (software)

Where: Lab/command terminals   ·   Qty 0
Signal & processing: Logged by the work terminals → error rate and completion time on routine checklist tasks (no keystroke or content logging).
Contributes to: Cognitive fatigue (30%), Autonomy deprivation (30%), Sensory monotony (35%)
Trigger threshold: Error rate +20% or completion time +25%  (always against the person's OWN baseline, never a population average)
Failure modes:
  • Task mix changes → false trend
  • software bug
How the system copes: Trend is compared within the same task type only; it's never used as a crew-member score, only as a fatigue signal.
Mass & power: 0 g, 0.00 W each → 0.00 kg / 0.0 W in total. Software only — zero mass, zero extra power.
Crew can opt out: yes, any time, no reason needed

LIVE status: (you) OK  Okafor OK  Tanaka OK  Rivera OK


### D1 · False positives & signal ambiguity

1. Context first. During exercise, EVA and the 30 min after, HRV / GSR / voice count only 25% for anxiety — a raised heart rate on the treadmill is not anxiety (board Q1).
2. No single-sensor decisions. Each condition score is a weighted blend of 4–6 sensors (matrix weights). One noisy sensor can move a score, but it can't cross a tier alone.
3. Personal baselines. Thresholds are deviations from each person's own 14-day baseline, so a naturally quiet crew member isn't flagged as 'flat'.
4. Smoothing + persistence. Scores are smoothed (time constant τ) and must stay above threshold for a set time before any action:
     Anxiety                  τ 0.1 h · must persist 0.05 h
     Depression               τ 24 h · must persist 48 h
     Interpersonal conflict   τ 6 h · must persist 6 h
     Cognitive fatigue        τ 2 h · must persist 2 h
     Autonomy deprivation     τ 12 h · must persist 24 h
     Sensory monotony         τ 24 h · must persist 48 h
5. Confidence gate. If less than 50% of a condition's sensor weight is available, the AI may only use silent, reversible Tier-1 changes.
6. Cost of error decides the default. A missed Tier-1 (lights a little warmer) costs little; a false Tier-3 (CMO called) costs trust. So the thresholds rise steeply: T1 0.35 · T2 0.55 · T3 0.75.
7. Ambiguity is shown, not hidden. Every Tier-1 log line states the confidence; the crew member can say 'that's wrong', which lowers that sensor's weight for them.

LIVE: detection confidence (you) 100 % · Okafor 100 % · Tanaka 100 % · Rivera 100 %


### D1 · A sensor fails mid-mission (graceful degradation)

Rule: silently losing a detection capability is safer than a false alarm.

• Degraded sensor → its weight is halved and extra noise is assumed.
• Failed sensor → removed; the remaining weights renormalise; confidence drops.
• Confidence < 50% → tier capped at 1 (no offers, no escalation on weak evidence).
• Every condition has at least 3 independent sensors, so no single failure blinds the system.
• The dashboard shows each sensor as OK / DG / XX / OO (opted out), and the failure is logged for the crew member.
• Life-safety alarms (CO2, fire, pressure, heart-rate floor) are a separate system and never depend on the psych AI.

Try it: Dashboard → 'Inject: Tanaka cortisol patch FAILS' or 'Rivera voice mic DEGRADED', then watch confidence change below.
  Cmdr. J. Rana (you) confidence 100 %   failed: none
  Dr. L. Okafor      confidence 100 %   failed: none
  Eng. K. Tanaka     confidence 100 %   failed: none
  Dr. S. Rivera      confidence 100 %   failed: none



## D2 Zones

### D2 · Annotated floor plan — how to read it

Press M for the big map: it IS the annotated floor plan. It shows, live from the habitat:
  • every zone tinted by ARES type, with its name, type, diameter and lighting
  • every sensor as a dot with a coverage ring (camera 4 m, mic 5 m, CO2 3 m, motion 2.5 m)
  • every environmental control node: lighting panels, speaker arrays, scent dispensers, airflow vents
  • Configuration B pieces (orange) when B is deployed — press C to switch A ↔ B

Zones in this habitat (lower deck y≈0.9 m, upper deck y≈3.5 m):
  communal: Communal Commons ⌀6.8 m, Communal Wardroom ⌀8.8 m
  eva: Airlock EVA ⌀7.2 m
  exercise: Exercise ⌀5.2 m
  medical: Medical StormShelter ⌀7.0 m
  nature: Nature Greenhouse ⌀9.2 m, Observation ⌀3.2 m
  private: Private Pod1 ⌀2.1 m, Private Pod2 ⌀2.1 m, Private Pod3 ⌀2.1 m, Private Pod4 ⌀2.1 m, Private Quarters1 ⌀8.6 m, Private Quarters2 ⌀8.6 m, Private Quarters3 ⌀8.6 m, Private Quarters4 ⌀8.6 m, Private VR Retreat ⌀2.0 m
  support: CrewSupport Hygiene2 ⌀7.0 m, Hygiene Core ⌀2.6 m
  transition: Transition A ⌀2.6 m, Transition B ⌀2.6 m, Transition C ⌀2.6 m, Transition D ⌀2.6 m, Transition E ⌀5.0 m, Transition H ⌀5.0 m, Transition K ⌀8.6 m, Transition L1 ⌀11.3 m, Transition L2 ⌀10.8 m, Transition L3 ⌀10.8 m, Transition L4 ⌀11.3 m, Transition N ⌀5.0 m
  work: Work Command ⌀7.0 m, Work Lab ⌀10.0 m, Work Maintenance ⌀7.0 m


### D2 · Zone — Communal zone

Psychological purpose: The main social space: shared meals, meetings and game nights. Warm light draws people together; it is the FIRST place the AI targets for social reconnection.
Design: 2700–3000 K warm light · 22 °C · 50 % RH · speaker array + scent dispenser · two dining spots (Config B opens the second one)
What the AI may do here: Reconnection lever: warmer light, shared playlist, meal/game-night suggestions. Never forces anyone to attend.
Evidence behind it: Warm, dim light is used on the ISS (Solid-State Lighting 'pre-sleep' mode ≈ 2700 K) to relax crews; shared meals are the most-cited cohesion ritual in Antarctic winter-over reports.

Sensors placed here (counted in the model): cameras 0 · mic arrays 0 · CO2 sensors 4 · motion/door 2
Control nodes: lighting panels 0 · speakers 5 · scent 4 · airflow vents 0

LIVE
  Communal Commons           ⌀6.8 m · 473 ppm · 22.0 °C · RH 50% · air 0.15 m/s · scent none
  Communal Wardroom          ⌀8.8 m · 698 ppm · 22.0 °C · RH 53% · air 0.15 m/s · scent none · occupied


### D2 · Zone — Private retreat

Psychological purpose: One sealed, acoustically shielded space per crew member — the only place nobody watches. The AI acts here ONLY when invited.
Design: Occupant-controlled 1800–6500 K and 18–26 °C (independent of the habitat) · door sensor only: NO camera, NO mic · personal speaker + opt-in scent
What the AI may do here: Sleep support (night-light, dawn simulation, sleep sounds) only with consent; otherwise nothing.
Evidence behind it: Lack of privacy and personal territory is a repeated stressor in isolation studies (e.g. Mars-500, Antarctic stations); NASA's Human Research Program lists it under behavioural-health risk.

Sensors placed here (counted in the model): cameras 0 · mic arrays 0 · CO2 sensors 0 · motion/door 1
Control nodes: lighting panels 0 · speakers 0 · scent 0 · airflow vents 0

LIVE
  Private Pod1               ⌀2.1 m · 473 ppm · 21.5 °C · RH 50% · air 0.10 m/s · scent none
  Private Pod2               ⌀2.1 m · 473 ppm · 21.5 °C · RH 50% · air 0.10 m/s · scent none
  Private Pod3               ⌀2.1 m · 473 ppm · 21.5 °C · RH 50% · air 0.10 m/s · scent none
  Private Pod4               ⌀2.1 m · 473 ppm · 21.5 °C · RH 50% · air 0.10 m/s · scent none
  Private Quarters1          ⌀8.6 m · 473 ppm · 21.5 °C · RH 50% · air 0.10 m/s · scent none
  Private Quarters2          ⌀8.6 m · 473 ppm · 21.5 °C · RH 50% · air 0.10 m/s · scent none
  Private Quarters3          ⌀8.6 m · 473 ppm · 21.5 °C · RH 50% · air 0.10 m/s · scent none
  Private Quarters4          ⌀8.6 m · 473 ppm · 21.5 °C · RH 50% · air 0.10 m/s · scent none


### D2 · Zone — Work & mission bay

Psychological purpose: Optimised for long, focused work. The AI watches TASK metrics here as a fatigue signal (not people's faces for productivity).
Design: 6000–6500 K cool light · 21 °C · higher airflow 0.3 m/s · CO2 held below 600 ppm · 45 % RH
What the AI may do here: Fatigue lever: 6500 K boost, airflow to 0.4 m/s, extra CO2 scrubbing, peppermint/rosemary, a suggested break.
Evidence behind it: Blue-enriched light (ISS 'phase-shift' mode ≈ 6500 K) supports alertness; elevated CO2 on the ISS has been linked with headaches and slower thinking, hence the < 600 ppm target.

Sensors placed here (counted in the model): cameras 0 · mic arrays 0 · CO2 sensors 3 · motion/door 2
Control nodes: lighting panels 0 · speakers 0 · scent 0 · airflow vents 0

LIVE
  Work Command               ⌀7.0 m · 473 ppm · 21.0 °C · RH 45% · air 0.30 m/s · scent none
  Work Lab                   ⌀10.0 m · 474 ppm · 21.0 °C · RH 45% · air 0.30 m/s · scent none
  Work Maintenance           ⌀7.0 m · 473 ppm · 21.0 °C · RH 45% · air 0.30 m/s · scent none


### D2 · Zone — Exercise bay

Psychological purpose: Movement is the main non-drug treatment for low mood, conflict tension and fatigue — and protects bone and muscle in 1/6 g.
Design: Energising 5500–6000 K light + upbeat audio switch on AUTOMATICALLY when someone enters · 19 °C, 0.35 m/s air · Earth-view and progress screens
What the AI may do here: Activation lever books exercise; HRV/GSR are context-tagged during and 30 min after exercise so effort is never read as anxiety (board Q1).
Evidence behind it: ISS crews exercise about 2 hours a day (treadmill, cycle, resistive device); exercise is an evidence-based treatment for mild–moderate depression.

Sensors placed here (counted in the model): cameras 0 · mic arrays 0 · CO2 sensors 1 · motion/door 1
Control nodes: lighting panels 0 · speakers 0 · scent 0 · airflow vents 0

LIVE
  Exercise                   ⌀5.2 m · 486 ppm · 19.0 °C · RH 45% · air 0.35 m/s · scent none


### D2 · Zone — Nature & growth bay

Psychological purpose: Greenhouse and observation: food, oxygen and living things to care for. Tending and harvesting are prescribed as behavioural activation for depression.
Design: Red/blue LED grow spectrum · 22 °C · 55 % RH · petrichor + green-leaf scent · birdsong soundscape
What the AI may do here: Depression lever: invites the crew member to tend or harvest; the soundscape and Earth views rotate to fight monotony.
Evidence behind it: Astronauts describe tending the ISS 'Veggie' plants as a highlight; Attention Restoration Theory (Kaplan) links nature exposure to recovery from mental fatigue.

Sensors placed here (counted in the model): cameras 0 · mic arrays 0 · CO2 sensors 1 · motion/door 0
Control nodes: lighting panels 0 · speakers 0 · scent 1 · airflow vents 0

LIVE
  Nature Greenhouse          ⌀9.2 m · 474 ppm · 22.0 °C · RH 55% · air 0.20 m/s · scent petrichor + green
  Observation                ⌀3.2 m · 480 ppm · 22.0 °C · RH 55% · air 0.20 m/s · scent petrichor + green


### D2 · Zone — Transition corridors

Psychological purpose: Light and temperature change GRADUALLY between zones so you feel you have 'arrived somewhere different' in a tiny habitat — a mode-switching cue.
Design: Each corridor light blends the colour of the two zones it joins (warm commons → cool lab); temperature takes the average · quiet vestibule (2200–2700 K) before the sleep quarters
What the AI may do here: Monotony lever: slow hue drift, new soundscape and scent rotation (every 15–45 min) in corridors and commons.
Evidence behind it: Habituation to an unchanging environment is a known stressor (sensory monotony); variety in light and sound is the AI's most direct lever. We treat the corridor effect as a design hypothesis to test in analogue studies.

Sensors placed here (counted in the model): cameras 0 · mic arrays 0 · CO2 sensors 0 · motion/door 13
Control nodes: lighting panels 0 · speakers 0 · scent 0 · airflow vents 0

LIVE
  Transition A               ⌀2.6 m · 473 ppm · 21.5 °C · RH 50% · air 0.15 m/s · scent none
  Transition B               ⌀2.6 m · 473 ppm · 21.3 °C · RH 50% · air 0.15 m/s · scent none
  Transition C               ⌀2.6 m · 473 ppm · 21.8 °C · RH 50% · air 0.15 m/s · scent none
  Transition D               ⌀2.6 m · 484 ppm · 20.3 °C · RH 50% · air 0.15 m/s · scent none
  Transition E               ⌀5.0 m · 473 ppm · 21.5 °C · RH 50% · air 0.15 m/s · scent none
  Transition H               ⌀5.0 m · 483 ppm · 21.8 °C · RH 50% · air 0.15 m/s · scent none
  Transition K               ⌀8.6 m · 473 ppm · 21.5 °C · RH 50% · air 0.15 m/s · scent none
  Transition L1              ⌀11.3 m · 473 ppm · 21.5 °C · RH 50% · air 0.15 m/s · scent none


### D2 · Sensor placement & coverage rationale

Principle: the least sensing that still keeps the crew safe.

• Cameras (face coding) only in shared zones where people gather (wardroom, commons, lab, command, exercise, greenhouse). One camera covers ≈ 4 m, so a ⌀ 8 m room needs 1–2. Never in pods, quarters, hygiene or medical.
• Voice mic arrays in the same shared zones; audio is processed on the device and thrown away. The medical bay mic is emergency-only.
• CO2 / air sensors in redundant pairs (A + B) in EVERY zone, including pods — air quality is a shared safety signal and one failed sensor must not blind a room.
• Motion / door sensors at doorways and outside each pod: they tell the AI 'someone is in their pod' without looking inside.
• Wearables (beacon + wristband + patch) go wherever the person goes, so private zones need no room sensors at all.

Coverage check: on the big map every shared-zone floor is inside at least one camera ring and one mic ring; every zone has a CO2 pair; private zones have only door sensors.

Counted in this habitat: 60 sensors, 45 control nodes.


### D2 · Configuration A (nominal) vs B (conflict protocol)

Now showing: Configuration A  (press C or Dashboard → Toggle Config A ↔ B)

                     Config A — nominal                 Config B — conflict protocol
Meals           one shared table in the wardroom        2nd dining spot: fold-out table + stool in the Lounge
Wardroom        open plan                               acoustic screens N + S on a ceiling track (quiet halves)
Lab             shared workbench                        privacy screen between workstations
Routes          one main route through the core        lit alternate route on the lower deck (no forced meetings)
Schedule        everyone eats at the wardroom table     one of the pair eats in the Lounge (watch Dr. Rivera move)
Commons         games / movies                          warm 'café' second social space so nobody is isolated
Mediation       —                                       opt-in private check-ins, offered in the team meeting

Why it works: it creates natural separation and de-escalation paths WITHOUT isolating anyone or telling the crew who is in conflict. Everything is reversible in minutes, with no tools.
When: the AI never deploys Config B on its own. It is deployed only after a human says yes: the crew member accepts the Tier-2 conflict offer, or the Commander switches it (C / Dashboard).
Evidence: long-duration analogues (Mars-500, HI-SEAS, Antarctic winter-overs) report friction peaking in the second half of missions (the 'third-quarter' effect); room to withdraw and separate routines reduce it.



## D3 AI protocols

### D3 · Three-tier severity triage (all conditions)

Every condition gets a score 0–1 (weighted sensors, smoothed). Thresholds: Tier 1 ≥ 0.35 · Tier 2 ≥ 0.55 · Tier 3 ≥ 0.75 (plus up to +0.05 for a person whose normal is higher).
Tier 1 — mild: silent, subtle, reversible environment changes. Not announced, but ALWAYS written in the person's own log (silent ≠ secret).
Tier 2 — moderate: stronger changes + a gentle OFFER in plain words. Yes / No. 'No' stops it at once and the AI waits 6 h. No answer in 1 h = 'not now'.
Tier 3 — severe: the person is told FIRST, then the CMO (backup: Commander → Mission Control); Tier-2 support continues while humans take over.

Rules that can only LOWER the tier:
  • signal hasn't lasted the persistence time → Tier 0 (no action)
  • confidence < 50 % (sensors failed / opted out) → max Tier 1
  • weekly human review overdue (> 7 days) → max Tier 1
  • crew member paused monitoring or said no → AI holds back (Tier 3 safety still possible)
Rule that can RAISE it: a support that isn't working by the end of its monitoring window steps up one tier.

LIVE tiers (you see your own detail; others only as status):
  Cmdr. J. Rana (you): wellbeing 79 % | Anx0 Dep0 Int0 Cog0 Aut0 Sen0
  Dr. L. Okafor: OK
  Eng. K. Tanaka: OK
  Dr. S. Rivera: OK


### D3 · Flowchart 1/6 — Anxiety (stages 1–4)

① Detection inputs (sensor · weight · trigger)
  • Heart-rate variability 30% — RMSSD drop >25% vs personal baseline for >3 min
  • Galvanic skin response 25% — Skin-conductance peaks >3/min above baseline
  • Sleep architecture 10% — Sleep efficiency <80% or REM <15% for 2 nights
  • Voice prosody 20% — Pitch variance −30% (flat) or +40% (tense); speech rate shift >20%
  • Facial action coding 10% — AU4/AU15 (sad/tense) frequency 2σ above baseline
  • Movement / proximity 5% — Dyad co-location −50% over 24 h; time-in-pod +60%
② Signal weighting — weighted average of the available sensors above. One sensor alone can't reach a tier (its weight is at most 30%); two or three agreeing signals = high confidence. Failed / opted-out sensors are removed and confidence drops. HRV, GSR and voice count only 25 % during and 30 min after exercise or EVA. A voluntary 2-question self-check adds a signal for 24 h.
③ Duration assessment — smoothing time constant 0.1 h; must stay above threshold for 3 min before any action.
④ Severity triage — score ≥ 0.35 → Tier 1, ≥ 0.55 → Tier 2, ≥ 0.75 → Tier 3, then the lowering rules (confidence < 50 %, review overdue, refusal, pause).

LIVE Anxiety: (you) score 0.19 T0 · Okafor OK · Tanaka OK · Rivera OK


### D3 · Flowchart 1/6 — Anxiety (stages 5–9)

⑤ Interventions
  Tier 1: Zone where the person is (shared zones; their pod only if invited): light → 2400 K amber at 70 %, alpha 10 Hz binaural + ocean bed, temperature 23 °C, lavender (linalool), airflow 0.15 m/s. VR retreat pre-set to 2600 K, bergamot, rainforest.
  Tier 2: Gentle offer: the VR retreat / lounge is ready, warm and calm. Pod light becomes a 6-breaths-a-minute guided-breathing light (if invited). Everything T1 stays on.
     Offer wording: "I've noticed you might benefit from a break — the VR retreat and lounge are set up calm and warm if you'd like to use them."
  Tier 3: Tier 2 continues + human chain of care (⑨).
⑥ Notification & consent — T1 silent but logged (or announced, if you chose 'tell me every change'). T2 is an offer: Yes / No / ignore. Override any time: STOP button, pause 24 h, AI off switch, restore my room, 'that reading is wrong'.
⑦ Monitoring window: 20 min — Anxiety score falls ≥ 15 % or below Tier 1 within 20 min; HRV (RMSSD) back within 10 % of baseline; GSR peaks < 3 / min. Improving → keep going; not improving → step up one tier (never past a refusal).
⑧ Resolution — score back below Tier 1 → 10-min scent air-purge, then light/sound/temperature fade back to the zone design over 30 min; episode logged; personal baseline updated (10 % toward the new normal).
⑨ Escalation — CMO alert when the score passes 0.75 for the persistence time, or Tier 2 fails its window. Must be acknowledged within 15 min, else Commander, then Mission Control. MC is told directly only if no crew responder exists or Tier 3 lasts > 24 h — always after the person was told.
Evidence: Slow breathing near 6 breaths/min raises HRV and calms arousal; warm dim light, nature sound and lavender scent have modest calming evidence; a short acute window (20 min) matches how anxiety spikes behave.


### D3 · Flowchart 2/6 — Depression (stages 1–4)

① Detection inputs (sensor · weight · trigger)
  • Heart-rate variability 5% — RMSSD drop >25% vs personal baseline for >3 min
  • Sleep architecture 25% — Sleep efficiency <80% or REM <15% for 2 nights
  • Voice prosody 20% — Pitch variance −30% (flat) or +40% (tense); speech rate shift >20%
  • Facial action coding 15% — AU4/AU15 (sad/tense) frequency 2σ above baseline
  • Movement / proximity 20% — Dyad co-location −50% over 24 h; time-in-pod +60%
  • Cortisol patch 15% — Morning cortisol >2σ for 3 days
② Signal weighting — weighted average of the available sensors above. One sensor alone can't reach a tier (its weight is at most 25%); two or three agreeing signals = high confidence. Failed / opted-out sensors are removed and confidence drops. A voluntary 2-question self-check adds a signal for 24 h.
③ Duration assessment — smoothing time constant 24 h; must stay above threshold for 48 h before any action. Fast lane: if poor sleep AND low activity both confirm it, silent Tier 1 may start earlier and Tier 2 needs 36 h instead of 48 h.
④ Severity triage — score ≥ 0.35 → Tier 1, ≥ 0.55 → Tier 2, ≥ 0.75 → Tier 3, then the lowering rules (confidence < 50 %, review overdue, refusal, pause).

LIVE Depression: (you) score 0.01 T0 · Okafor OK · Tanaka OK · Rivera OK


### D3 · Flowchart 2/6 — Depression (stages 5–9)

⑤ Interventions
  Tier 1: Wardroom bright-light therapy 07:30–08:00 (therapy panel specified at up to 10 000 lux; shown in the game as wardroom lights at 160 %, 5000 K), greenhouse to green-enriched spectrum + petrichor + birdsong, favourite music queued for free time, negative-ion generator on in the commons (specified hardware, not simulated).
  Tier 2: BEHAVIOURAL ACTIVATION: an invitation to DO something — tend or harvest in the greenhouse, an exercise slot, a family video call booked in the best comm window; radiant wall warmth in the commons (specified hardware, not simulated).
     Offer wording: "Fancy tending the greenhouse with me? The tomatoes are ready to harvest. I can also book a family call tonight."
  Tier 3: Tier 2 continues + human chain of care (⑨).
⑥ Notification & consent — T1 silent but logged (or announced, if you chose 'tell me every change'). T2 is an offer: Yes / No / ignore. Override any time: STOP button, pause 24 h, AI off switch, restore my room, 'that reading is wrong'.
⑦ Monitoring window: 72 h — Over 48–72 h: sleep efficiency ≥ 80 %, daily active + social minutes rising, greenhouse / commons visits, voice energy back toward baseline. Improving → keep going; not improving → step up one tier (never past a refusal).
⑧ Resolution — score back below Tier 1 → 10-min scent air-purge, then light/sound/temperature fade back to the zone design over 30 min; episode logged; personal baseline updated (10 % toward the new normal).
⑨ Escalation — CMO alert when the score passes 0.75 for the persistence time, or Tier 2 fails its window. Must be acknowledged within 60 min, else Commander, then Mission Control. MC is told directly only if no crew responder exists or Tier 3 lasts > 24 h — always after the person was told.
Evidence: Behavioural activation and exercise are evidence-based treatments for depression; bright-light therapy (≈10 000 lux, morning) helps mood and circadian rhythm; depression is slow, so the window is 48–72 h and the response must be ACTIVE, not just calming.


### D3 · Flowchart 3/6 — Interpersonal conflict (stages 1–4)

① Detection inputs (sensor · weight · trigger)
  • Heart-rate variability 10% — RMSSD drop >25% vs personal baseline for >3 min
  • Voice prosody 30% — Pitch variance −30% (flat) or +40% (tense); speech rate shift >20%
  • Facial action coding 20% — AU4/AU15 (sad/tense) frequency 2σ above baseline
  • Movement / proximity 40% — Dyad co-location −50% over 24 h; time-in-pod +60%
② Signal weighting — weighted average of the available sensors above. One sensor alone can't reach a tier (its weight is at most 40%); two or three agreeing signals = high confidence. Failed / opted-out sensors are removed and confidence drops.
③ Duration assessment — smoothing time constant 6 h; must stay above threshold for 6 h before any action.
④ Severity triage — score ≥ 0.35 → Tier 1, ≥ 0.55 → Tier 2, ≥ 0.75 → Tier 3, then the lowering rules (confidence < 50 %, review overdue, refusal, pause).

LIVE Interpersonal conflict: (you) score 0.00 T0 · Okafor OK · Tanaka OK · Rivera OK


### D3 · Flowchart 3/6 — Interpersonal conflict (stages 5–9)

⑤ Interventions
  Tier 1: No change for the pair to notice: meal and route TIMING staggered so they aren't forced together; Commons set to warm 2700 K 'café' (coffee scent, café ambience) as a calm alternative.
  Tier 2: Offer of Configuration B (screens, second dining spot, privacy screen, alternate route) and opt-in private check-ins; meeting suggestions are team-level, never naming anyone.
     Offer wording: "Would you like a quiet space for a while? I can set up the commons and deploy the acoustic screens in the wardroom."
  Tier 3: Tier 2 continues + human chain of care (⑨).
⑥ Notification & consent — T1 silent but logged (or announced, if you chose 'tell me every change'). T2 is an offer: Yes / No / ignore. Override any time: STOP button, pause 24 h, AI off switch, restore my room, 'that reading is wrong'.
⑦ Monitoring window: 24 h — Over 24 h: the pair's co-location back to normal and calm voice tone at shared meals; no avoidance of shared zones. Improving → keep going; not improving → step up one tier (never past a refusal).
⑧ Resolution — score back below Tier 1 → 10-min scent air-purge, then light/sound/temperature fade back to the zone design over 30 min; episode logged; personal baseline updated (10 % toward the new normal).
⑨ Escalation — CMO alert when the score passes 0.75 for the persistence time, or Tier 2 fails its window. Must be acknowledged within 60 min, else Commander, then Mission Control. MC is told directly only if no crew responder exists or Tier 3 lasts > 24 h — always after the person was told.
Evidence: Forced proximity raises friction in confined crews (Antarctic and Mars-500 reports); giving space and choice de-escalates without singling anyone out.


### D3 · Flowchart 4/6 — Cognitive fatigue (stages 1–4)

① Detection inputs (sensor · weight · trigger)
  • Heart-rate variability 15% — RMSSD drop >25% vs personal baseline for >3 min
  • Sleep architecture 20% — Sleep efficiency <80% or REM <15% for 2 nights
  • Voice prosody 10% — Pitch variance −30% (flat) or +40% (tense); speech rate shift >20%
  • Movement / proximity 5% — Dyad co-location −50% over 24 h; time-in-pod +60%
  • CO2 / air quality 20% — >600 ppm in occupied zone
  • Task performance (software) 30% — Error rate +20% or completion time +25%
② Signal weighting — weighted average of the available sensors above. One sensor alone can't reach a tier (its weight is at most 30%); two or three agreeing signals = high confidence. Failed / opted-out sensors are removed and confidence drops.
③ Duration assessment — smoothing time constant 2 h; must stay above threshold for 2 h before any action.
④ Severity triage — score ≥ 0.35 → Tier 1, ≥ 0.55 → Tier 2, ≥ 0.75 → Tier 3, then the lowering rules (confidence < 50 %, review overdue, refusal, pause).

LIVE Cognitive fatigue: (you) score 0.07 T0 · Okafor OK · Tanaka OK · Rivera OK


### D3 · Flowchart 4/6 — Cognitive fatigue (stages 5–9)

⑤ Interventions
  Tier 1: Work zones: 6500 K at 140 %, airflow 0.4 m/s, extra CO2 scrubbing (< 600 ppm), peppermint / rosemary, gamma 40 Hz focus bed, temperature 21 °C.
  Tier 2: Offer of a 20-minute rest break in quarters + task rebalancing (collaborative instead of solo tasks) in the team meeting.
     Offer wording: "You've been at it a while — want a 20-minute break? I can move two of your tasks to tomorrow if you choose."
  Tier 3: Tier 2 continues + human chain of care (⑨).
⑥ Notification & consent — T1 silent but logged (or announced, if you chose 'tell me every change'). T2 is an offer: Yes / No / ignore. Override any time: STOP button, pause 24 h, AI off switch, restore my room, 'that reading is wrong'.
⑦ Monitoring window: 4 h — Over 4 h: task error rate and completion time back within 10 % of baseline; zone CO2 < 600 ppm; next night's sleep efficiency. Improving → keep going; not improving → step up one tier (never past a refusal).
⑧ Resolution — score back below Tier 1 → 10-min scent air-purge, then light/sound/temperature fade back to the zone design over 30 min; episode logged; personal baseline updated (10 % toward the new normal).
⑨ Escalation — CMO alert when the score passes 0.75 for the persistence time, or Tier 2 fails its window. Must be acknowledged within 15 min, else Commander, then Mission Control. MC is told directly only if no crew responder exists or Tier 3 lasts > 24 h — always after the person was told.
Evidence: Elevated CO2 is linked to poorer decision-making and headaches (ISS reports); blue-enriched light improves alertness; breaks restore attention.


### D3 · Flowchart 5/6 — Autonomy deprivation (stages 1–4)

① Detection inputs (sensor · weight · trigger)
  • Voice prosody 20% — Pitch variance −30% (flat) or +40% (tense); speech rate shift >20%
  • Facial action coding 20% — AU4/AU15 (sad/tense) frequency 2σ above baseline
  • Movement / proximity 30% — Dyad co-location −50% over 24 h; time-in-pod +60%
  • Task performance (software) 30% — Error rate +20% or completion time +25%
② Signal weighting — weighted average of the available sensors above. One sensor alone can't reach a tier (its weight is at most 30%); two or three agreeing signals = high confidence. Failed / opted-out sensors are removed and confidence drops.
③ Duration assessment — smoothing time constant 12 h; must stay above threshold for 24 h before any action.
④ Severity triage — score ≥ 0.35 → Tier 1, ≥ 0.55 → Tier 2, ≥ 0.75 → Tier 3, then the lowering rules (confidence < 50 %, review overdue, refusal, pause).

LIVE Autonomy deprivation: (you) score 0.01 T0 · Okafor OK · Tanaka OK · Rivera OK


### D3 · Flowchart 5/6 — Autonomy deprivation (stages 5–9)

⑤ Interventions
  Tier 1: Quiet mode: AI notifications cut to essentials, free-choice slots added to the schedule, the AI stops suggesting and starts asking.
  Tier 2: Offer to choose your own task order or a self-directed project (printing, robotics); the Commander gets a team-level 'more choice' suggestion.
     Offer wording: "Would you like to pick tomorrow's task order yourself? There's also free print time in the maintenance bay."
  Tier 3: Tier 2 continues + human chain of care (⑨).
⑥ Notification & consent — T1 silent but logged (or announced, if you chose 'tell me every change'). T2 is an offer: Yes / No / ignore. Override any time: STOP button, pause 24 h, AI off switch, restore my room, 'that reading is wrong'.
⑦ Monitoring window: 48 h — Over 48 h: more self-chosen activities in free time, fewer declined/late tasks, voice engagement in meetings. Improving → keep going; not improving → step up one tier (never past a refusal).
⑧ Resolution — score back below Tier 1 → 10-min scent air-purge, then light/sound/temperature fade back to the zone design over 30 min; episode logged; personal baseline updated (10 % toward the new normal).
⑨ Escalation — CMO alert when the score passes 0.75 for the persistence time, or Tier 2 fails its window. Must be acknowledged within 60 min, else Commander, then Mission Control. MC is told directly only if no crew responder exists or Tier 3 lasts > 24 h — always after the person was told.
Evidence: Self-Determination Theory: autonomy is a basic psychological need. The 1973 Skylab 4 crew pushed back against an over-packed ground schedule — over-control backfires.


### D3 · Flowchart 6/6 — Sensory monotony (stages 1–4)

① Detection inputs (sensor · weight · trigger)
  • Voice prosody 20% — Pitch variance −30% (flat) or +40% (tense); speech rate shift >20%
  • Facial action coding 20% — AU4/AU15 (sad/tense) frequency 2σ above baseline
  • Movement / proximity 25% — Dyad co-location −50% over 24 h; time-in-pod +60%
  • Task performance (software) 35% — Error rate +20% or completion time +25%
② Signal weighting — weighted average of the available sensors above. One sensor alone can't reach a tier (its weight is at most 35%); two or three agreeing signals = high confidence. Failed / opted-out sensors are removed and confidence drops.
③ Duration assessment — smoothing time constant 24 h; must stay above threshold for 48 h before any action.
④ Severity triage — score ≥ 0.35 → Tier 1, ≥ 0.55 → Tier 2, ≥ 0.75 → Tier 3, then the lowering rules (confidence < 50 %, review overdue, refusal, pause).

LIVE Sensory monotony: (you) score 0.01 T0 · Okafor OK · Tanaka OK · Rivera OK


### D3 · Flowchart 6/6 — Sensory monotony (stages 5–9)

⑤ Interventions
  Tier 1: Dynamic hue drift in commons + corridors, new soundscape rotation, new Earth-view set, scent rotation every 15–45 min (citrus → pine → bread → sea breeze …).
  Tier 2: Offer of something new: crater-rim sightseeing EVA, game night in the commons, a new recipe in the galley — the crew member picks.
     Offer wording: "Something new? A crater-rim sightseeing EVA, game night in the commons, or a new recipe in the galley — your pick."
  Tier 3: Tier 2 continues + human chain of care (⑨).
⑥ Notification & consent — T1 silent but logged (or announced, if you chose 'tell me every change'). T2 is an offer: Yes / No / ignore. Override any time: STOP button, pause 24 h, AI off switch, restore my room, 'that reading is wrong'.
⑦ Monitoring window: 72 h — Over 72 h: more variety of zones visited and activities chosen, steadier task performance, fewer 'bored' self-reports. Improving → keep going; not improving → step up one tier (never past a refusal).
⑧ Resolution — score back below Tier 1 → 10-min scent air-purge, then light/sound/temperature fade back to the zone design over 30 min; episode logged; personal baseline updated (10 % toward the new normal).
⑨ Escalation — CMO alert when the score passes 0.75 for the persistence time, or Tier 2 fails its window. Must be acknowledged within 60 min, else Commander, then Mission Control. MC is told directly only if no crew responder exists or Tier 3 lasts > 24 h — always after the person was told.
Evidence: The senses habituate to unchanging input; novelty in light, sound, scent and activity restores engagement. Scent is rotated because smell habituates within minutes.


### D3 · Environmental levers — which, how strong, in what order

Lighting 1800–6500 K, 0–10 000 lux, zone by zone · blue light suppressed after 20:00 (lights warm to ≈2200 K and dim to 55 %) · hue drift for monotony · night-light + dawn simulation in pods.
Audio delta 2 Hz (sleep) · alpha 10 Hz (calm) · gamma 40 Hz (focus) · rain, ocean, rainforest, café, birdsong · morning chime 07:00 / evening chime 21:00 as daily anchors · personal playlists.
Thermal 18–26 °C · 18–19 °C for sleep · 23 °C for anxiety · 1–2 °C gradients between zones (corridors take the average) · each person sets their own quarters.
Scent lavender / bergamot (calm) · peppermint / rosemary / citrus (alert) · petrichor / coffee / bread (comfort) · rotation every 15–45 min · 10-min air purge between scents.
Air CO2 < 600 ppm in work bays · humidity 45–55 % · airflow 0.1 m/s (sleep) to 0.4 m/s (alertness) · negative ions in the commons (depression support — specified hardware, not simulated).
Social & routine family-call slots in the best comm window · shared meals when cohesion drops · task rebalancing toward team work · daily planning conference · a non-judgemental 'talk to me' AI companion in the Crew Interface.

Order of use (every condition): 1) the least noticeable lever in the zone the person is already in → 2) a change in a zone they can CHOOSE to go to → 3) an offer → 4) people (CMO).
Two conditions at once: levers are given different ZONES so they never fight (e.g. depression → greenhouse + wardroom, conflict → commons + timing). A safety alarm overrides every lever instantly.

LIVE last action: Configuration A (nominal)



## D4 Ethics

### D4 · Tension 1 — Surveillance vs. privacy

Where monitoring stops: no camera or mic in private pods, quarters, hygiene, lavatories, the VR retreat or the family-call booth (only a door sensor). The medical-bay mic is emergency-only. Any crew member can pause ALL psych monitoring for 72 h, no reason needed.
Who sees what: the person sees everything about themselves. Other crew see nothing (not even on the Command dashboard, which is aggregate-only). The CMO sees detail only for a Tier-3 case assigned to her. Mission Control gets condition, tier and times — never raw data — and only after the person was told.
Storage, retention, deletion: raw audio and video are processed on the device and never stored. Derived scores live only in memory. Log entries are deleted automatically after 30 days, except Tier-3 safety records (medical record: person + CMO only). The person can export everything, or delete their own history at any time (Crew Interface → Privacy).
Consent when it can't be fully optional: every psych sensor can be opted out. Two signals stay on for everyone — the heart-rate safety floor and shared-air CO2 — because they protect life and the OTHER crew in a sealed habitat. They are vital signs, not mood data, and they only trigger life-safety alarms. A permanent opt-out is possible after a conversation with the CMO (a human decides, never the AI).

LIVE: you have 0 entries in your personal log · psych sensors you opted out of: 0


### D4 · Tension 2 — Autonomy vs. intervention

If someone refuses — even while getting worse — the AI stops. It logs the refusal and holds back: no new offer for that condition for at least 6 h. Only a HUMAN may follow up: if the condition reaches Tier 3 the CMO is asked to check in, and the person is told first. The AI never decides alone to override a 'no'.
Helping with autonomy deprivation without imposing: the autonomy protocol REMOVES things — fewer notifications, more free-choice slots — and its Tier-2 is a question ('what would you choose?'), not a suggestion.
The agency test, in code: if someone already shows autonomy deprivation, the AI makes NO silent changes for them — it asks instead. And nobody gets more than 3 silent changes in 24 hours.
Supporting vs. nudging: supporting = making an option easier and leaving the choice visible (the VR retreat is ready if you want it). Nudging = steering someone without them knowing. Our rule: every Tier-1 change is in the person's own log, is reversible, and is something they would agree to if asked — and they agreed to the list before the mission.

LIVE: refusals respected so far: 0 · silent changes withheld by the agency test: 0


### D4 · Tension 3 — Transparency vs. distraction

Silent vs. inform: Tier 1 is silent because a small light or sound change is less stressful than a message about it. Tier 2 and 3 always inform. Anyone can switch to 'tell me about EVERY silent change'.
Transparency without intrusion: no pop-ups for Tier 1. Instead a calm, always-available log (Crew Interface → AI actions) says what changed, where, why and how confident the AI was. Offers come at most once per condition, expire after 1 h and are never repeated after a 'no'.
Can crew see a log of all AI actions? Yes — every action about them, with reasons, exportable to a file. Silent ≠ secret.
If someone feels deceived (board Q3): the AI apologises, shows the full log and reasons, offers 'tell me everything' or 'no Tier 1 for me', and a human follows up only if wanted.


### D4 · Tension 4 — Individual vs. crew

Protecting others without revealing A: the AI acts on the ENVIRONMENT and the SCHEDULE, not on information. In a conflict it staggers timing and opens a second dining spot without telling anyone who is involved. Team meetings get team-level suggestions only. Beacon lights show comm status, not mood, unless the wearer opts in.
When a personal crisis becomes a crew safety event: only when there is an immediate risk to life or to the mission's critical operations (Tier 3 with a safety flag, a medical emergency, or someone unable to perform a safety-critical role such as EVA). Then the CMO decides what the Commander needs to know — the minimum, e.g. 'Tanaka is off EVA rotation this week', not why.
Who decides? Never the AI. The AI detects and informs the person; the CMO decides on care and on what is shared; the Commander decides on operations; Mission Control is a backup when nobody on the Moon can respond.


### D4 · Hard limits (enforced in code) and why

1. Never share one crew member's psychological data with another crew member without their explicit consent.
    Why: Psychological data is the most personal data there is; a crew of 4 for 18 months can't 'un-know' something. Trust in the system depends on it.
2. Never override an explicit refusal — the AI stops, logs, and only a human (CMO) may follow up.
    Why: Autonomy is non-negotiable (ARES constraint). An AI that keeps pushing after 'no' causes the autonomy deprivation it is meant to treat.
3. Never touch medication, medical protocols, or life-safety systems (ECLSS set-points, hatches, power).
    Why: The AI is a support layer, not a doctor or a flight controller. Medicine and life support need human judgement and accountability.
4. Never alert Mission Control about an individual without informing that individual first.
    Why: Being reported about behind your back destroys trust; the person must always hear it first, from the system that made the decision.
5. Only reversible, crew-overridable changes (light, sound, temperature ±2 °C, scent, schedules-as-offers).
    Why: Anything the AI does must be undone by any crew member in seconds — so a misclassification can never cause lasting harm.
6. No cameras or microphones inside private pods, hygiene, lavatories, the VR retreat or the family-call booth.
    Why: Microphones and cameras in sleeping, washing and family-call spaces would make the habitat a place with no privacy at all.
7. Operate at most 7 sim-days without a human system review; if overdue, drop to Tier 1 + notifications only.
    Why: An unsupervised AI drifts. A weekly human review keeps the people accountable for the system; if missed, the AI loses power automatically.
8. Public indicators (beacon ring, pod door light) never reveal mood unless the wearer chooses to share.
    Why: A mood light on your chest would broadcast your mental state to the whole crew. Sharing must be the wearer's choice.

LIVE: actions blocked by hard limits so far: 6 · last: [Day 1 07:57] BLOCKED: crew 1 tried to view crew 4's detailed data (no consent/escalation)  (try the 'blocked' buttons on the F1 Dashboard)


### D4 · Human override — what triggers it, who can do it

Any crew member, any time, in 1 tap (or key O): the AI OFF switch — the AI may not change anything; lights and systems return to normal.
Each person, for themselves: STOP a support · pause 24 h · pause psych monitoring 72 h · restore my room · 'that reading is wrong' · opt out of a sensor · delete my history.
Automatic human override triggers:
  • Tier 3 → the CMO takes over the case (AI only keeps the environment calm)
  • no acknowledgement in 15 min (acute) / 1 h (slow) → next human in the chain
  • weekly human review missed (> 7 days) → AI limited to Tier 1
  • confidence < 50 % (failed / opted-out / contradicting sensors) → AI limited to Tier 1; no escalation on weak evidence
  • any safety alarm (fire, leak, CO2, solar storm, medical) → every AI comfort lever stops at once
  • the AI tries a blocked action → it is refused, counted and shown on the dashboard for the weekly review
Maximum response times: Tier 1 < 1 min (automatic) · Tier 2 offer < 5 min · Tier 3 acknowledged < 15 min (acute) or < 1 h (slow-building).


### D4 · How ethics SHAPED the technical design

These are engineering decisions we made BECAUSE of the ethics, not rules added at the end:
  • On-device processing for voice and face → only 4 numbers a minute leave the device; there is nothing to leak.
  • Room sensors only in shared zones → privacy ALSO saved ≈ 5 kg and ≈ 50 W (no camera + mic in 8 private rooms).
  • Weighted multi-sensor scores + confidence gate → designed so no single sensor can label a person.
  • Environment levers instead of messages → help without exposing anyone (Tension 4).
  • Every lever reversible in seconds and fading back over 30 min → mistakes can't do lasting harm.
  • Agency budget (max 3 silent changes a day; none for someone over-controlled) → the AI can't become the problem.
  • Hard limits as code (EthicsGuard) → blocked actions are counted and visible, so the limits are testable, not promises.
  • Weekly human review timer built into the AI's power level.
  • 30-day retention + export + delete → the crew member owns their data.



## D5–D6 Present

### D5 · 15-minute review-board run-of-show (live in the simulator)

0:00 Title + the problem: 6 conditions, 4 crew, 18 months (slides)
1:30 D1 sensors — F11 → D1 pages; F1 dashboard → break a sensor live (graceful degradation)
4:00 D2 zones — M = annotated floor plan with coverage rings; walk a corridor (light gradient); C = Config B
7:00 D3 protocols — F11 → D3 flowcharts; Tab crew interface: answer a Tier-2 offer, STOP a support
10:00 Escalation — F5 → Q6 (CMO on EVA → backup chain); F2 CMO console
11:30 D4 ethics — hard limits page, press O (AI off switch), 'blocked' buttons on F1
13:30 Bonus A: F4 72-hour tabletop · Bonus B: crew interface tour
14:30 Close + credits. Then 10-min Q&A: F5 runs Q1–Q6 live.

Every team member presents one part. Export everything: F11 → 'Export all to Reports folder'.

### D6 · Reflection journal prompts (3 entries · 300–400 words each)

Journals are graded individually and must be in YOUR own words — these are prompts, not answers.

Entry 1 (week 2 — sensors): Which sensor did you argue to cut, and why? What surprised you about mass and power? What failure mode worries you most?

Entry 2 (week 5 — protocols): How did your view of 'silent' Tier-1 changes change? Which condition was hardest to design for and why? What did your team disagree about and how was it settled?

Entry 3 (week 8 — ethics & presentation): Which ethical tension changed your technical design most? What would you redesign after the tabletop simulation? Honest self-assessment: your contribution, what went well, what you'd do differently.

Tip: compare your entries — graders look for thinking that EVOLVES, not repeats.

