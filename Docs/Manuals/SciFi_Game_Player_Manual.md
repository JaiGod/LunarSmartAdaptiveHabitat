# Lunar Habitat Commander: Player Manual (Sci-Fi Game)

Project ARES · NASA HUNCH 2025–26 · Unity project "Nasa_Project" (Lunar Habitat Commander)
© 2026 Jaineil R., with thanks to Angel C. and Farhan K.

![Mission card on the left side of the screen](e2_taskcard)

## 1. What this game is

Lunar Habitat Commander is the bigger, sci-fi version of our habitat. You run a south-pole base with a crew of four for 30 days. The habitat AI is called **ARIA**. It watches each astronaut's mood, stress, sleep and heart rate, tells you when someone needs help, and adjusts the rooms to support them.

It has the same Project ARES AI engine as the realistic game (same sensors, same six conditions, same three tiers), plus a bigger base with a central dorm, portals, mini-games, mining outside, and aliens you can switch off for a calm visit.

The crew:
- **Cmdr. J. Rana (you)**: Commander
- **Dr. L. Okafor**: Chief Medical Officer (CMO)
- **Eng. K. Tanaka**: Flight Engineer
- **Dr. S. Rivera**: Mission Specialist (Geology)

## 2. Installing and starting

- **PC:** open the project in Unity Hub (Unity 6000.3.21f1), open the scene `Lunar_Habitat_Commander` and press Play, or run the built game (also on the Meta Horizon Store).
- **VR (Meta Quest):** start Quest Link or Air Link first. The game switches to VR controls automatically.
- On the start screen, choose **Start**. You can turn **Kid mode** on with K for more time on every alert and gentler rules.

## 3. The easy way to play

- **G:** do my next step. The mission card on the left always shows the next step, and G does it with you (it takes you there and does the action).
- **Esc:** menu. **Menu > Project ARES** (or **F11**) has every AI and rubric screen.
- **F10:** crew interface (your data, the AI's offers, privacy, pause, export).
- **F12:** the AI OFF switch. Humans always outrank the AI.
- **H:** help.

## 4. Moving around

**Keyboard and mouse**
- WASD and mouse to move, click or E to use things
- Tab / Q for the next or previous zone, 1–0 jump to zones, N for the map
- Push against a door or wall for a moment to pass through
- PageUp / PageDown take the lift to the next floor, F turns on the jetpack (fly)
- T talks to the nearest astronaut or to ARIA (type or use your voice)

**VR (Meta Quest)**
- Left stick walks (click it to run), right stick turns
- Right Grip teleports (hold to aim, let go), right Trigger selects and uses
- A jumps, B changes tool, X goes to the next job, Y opens the menu
- Point the laser at a glowing orb, button or repair panel and pull the trigger
- Two rows of big buttons under the wrist card do everything else (maps, cameras, meeting, health, reading, music, autopilot, jetpack, talk, eat)

## 5. Missions

The missions run in order and each one teaches a part of the Project ARES design. Follow the card on the left or keep pressing G.

1. **First shift: meet your crew.** Check on each crewmate, then open the AI dashboard. This shows how the health data is collected.
2. **Tanaka's rough night.** Send Tanaka to rest (he agrees and walks to his pod), then clean the CO2 scrubber in Life Support.
3. **Words at dinner.** Turn on Configuration B (the conflict layout with sound screens, a second dining spot and a lit second route), then hold a team meal in the galley.
4. **Rivera feels low.** Set up a video call home for Rivera, then answer ARIA's offer to you in the crew interface (yes or no). The AI never decides for you.
5. **Bedtime: humans stay in charge.** Go to your sleep pod in the crew quarters. During the night ARIA breaks a bedtime rule on purpose. Watch it stop itself, undo the change and wake you. Press F12 when you're ready to let it back on.
6. **Report to the review board.** Answer all 6 board questions (G runs the next one live), run the 72-hour tabletop test, and export your own data log.

![The six missions](e2_missions)

## 6. Checking on a crewmate (how the health data is collected)

Walk up to a crewmate (or press G on that step). They agree first, then you see a check-in sheet with:
- **Wristband:** heart rate and HRV
- **Chest beacon (UWB):** location and time spent with others
- **Skin patch:** skin response (GSR) and cortisol
- **Room sensors:** CO2 and air (voice and face analysis only in shared rooms, never in pods)
- **Task data:** speed and errors
- The six mental health levels as coloured bars, and what ARIA suggests

![Health check-in sheet](e2_checkin)

## 7. How ARIA helps (three tiers)

- **Tier 1 (silent):** small room changes you can always undo, at most 3 a day per person, all of them logged.
- **Tier 2 (offer):** ARIA asks in plain words. Answer in the crew interface (F10). A "no" stops it right away, and it waits 6 hours.
- **Tier 3 (doctor):** the person is told first, then the CMO console lights up. If the CMO is out on EVA, the Commander is the backup.

![An offer in the crew interface](e2_offer)

## 8. You are always in charge

- **F12 = AI OFF switch.** ARIA stops every action and undoes its changes. Only a human can turn it back on. If nobody answers within 30 seconds during a problem, a hardware interlock switches it off by itself.
- **Night watch:** if anything goes wrong while you sleep (an alarm, ARIA breaking a bedtime rule, an emergency or a failed sensor), ARIA turns itself off and wakes you.
- **Crew interface (F10):** what ARIA did for you, stop every support, pause monitoring for 72 hours, switch sensors off, export or delete your log.
- **Hard limits:** try them yourself in Menu > Project ARES > AI dashboard ("Test a hard limit"). They're always blocked and counted.

![Night watch: ARIA stopped, you're in charge](e2_nightwatch)

## 9. Project ARES menu (F11)

- **Deliverables D1–D6:** every design page with live numbers, including the drawn flowcharts, the dimensioned floor plan, the threshold check and the written ethics report
- **AI dashboard:** live tiers, sensors, confidence, hard limits, breaking a sensor
- **Crew interface (F10)** and **CMO console**
- **Board questions Q1–Q6**, run live
- **Bonus A:** the 72-hour tabletop simulation
- **Configuration A / B**
- **AI OFF switch (F12)**
- **Rubric self-check**
- **Export everything** to the Reports folder

![Deliverables reading sheet](e2_doc_matrix)

## 10. Zones in the base

The base is built around the **central crew dorm**. Every module has an ARES zone type, and you can see it in the world:
- **Zone lights:** warm light in communal rooms, cool bright light in work rooms, green in the greenhouse
- **Ceiling sensor clusters:** CO2 and a UWB beacon in every room; mic and camera only in shared rooms
- **13 transition hallways:** the floor lights fade from one zone's colour to the next
- Zones: crew quarters, galley, hygiene, workstations/lab, medical, storage, ECLSS, airlock, greenhouse, exercise and observation lounge

![Greenhouse (nature zone)](e2_zone_greenhouse)

## 11. Extra things to do

- **Repair mini-games** on the task stations (join the wires, lock the vent…), or call the repair rover
- **Outside:** the mining field (ice, ore, helium-3, crystals), upgrades for your tools, and aliens (switch them off in the Explorer menu for a calm visit)
- **Y:** maps and mini-games, **Z:** security cameras, **F2:** crew health, **F3:** things to read, **F4:** team meeting, **F6:** autopilot (the AI plays for you)
- **I:** backpack, **O:** habitat lights, **M:** music, **P:** pause

## 12. Exporting for the review board

- **Menu > Project ARES > Export everything** saves D1–D6, the D4 ethics report, your AI log and the tabletop report to the `Reports` folder.

## 13. Troubleshooting

- **Can't find the next step:** look at the mission card on the left or press G.
- **Stuck:** push against the wall for a moment, or use Tab to jump to the next zone.
- **Too much going on:** turn the aliens off in the Explorer menu, or press P to pause.
- **VR not starting:** start Quest Link before pressing Play.
- **ARIA is doing too much:** press F12. You're in charge.
