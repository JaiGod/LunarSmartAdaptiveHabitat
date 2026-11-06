# Lunar Smart Adaptive Habitat: Player Manual (Realistic Game)

Project ARES · NASA HUNCH 2025–26 · Unity project "Moon_Base_Final"
© 2026 Jaineil R., with thanks to Angel C. and Farhan K.

![The first mission starts as soon as you load in](e3_start_mission)

## 1. What this game is

You are the Commander of a 4-person lunar habitat at the Moon's south pole. The habitat has an AI that watches how the crew is doing (stress, mood, sleep, tiredness, getting along) and changes the rooms to help: lighting, sound, temperature, scent, air and the daily schedule.

Your job is to look after your crew, keep the base running, and stay in charge of the AI. The AI helps, but a human always has the final say.

The crew:
- **Cmdr. J. Rana (you)**: Commander
- **Dr. L. Okafor**: Chief Medical Officer (CMO)
- **Eng. K. Tanaka**: Flight Engineer
- **Dr. S. Rivera**: Mission Specialist (Geology)

## 2. Installing and starting

- **PC:** open the project in Unity Hub (Unity 6000.3.21f1), open the scene `Assets/Unity3/Scenes/00_Menu_VRBox` and press Play, or run the built game.
- **VR (Meta Quest):** connect the headset with Quest Link or Air Link before pressing Play. The game picks VR controls automatically.
- On the main menu, pick **Start mission**. You load in at the habitat with the first mission already running.

## 3. The easy way to play (only 5 keys)

The bottom of the screen shows the 5 keys you need. Everything else is optional.

- **ENTER:** do my next step. The banner at the top always shows ONE next step, and ENTER does it with you (it walks you there and does the action).
- **I:** check on the crewmate next to you (health check-in). Away from people, it opens the crew wellness screen.
- **O:** the AI OFF switch. Press it any time and the AI stops and undoes its changes.
- **Q:** menu (7 big buttons).
- **H:** help screen. **K:** show every key.

![Simple menu](e3_menu)

## 4. Moving around

**Keyboard and mouse**
- WASD or arrow keys to move, Shift to run, Space to jump (1/6 gravity, so you float a bit)
- Hold the right mouse button (or press L to lock) to look around
- E or click to use things, pick up boxes and tools
- B shows a gold path to your bed, M opens the big map, N navigates to any room
- U gets you unstuck if you ever get trapped

**VR (Meta Quest)**
- Left stick moves, right stick turns, Grip teleports
- Trigger uses things (buttons, ladders, your bed)
- Y opens the crew screen, X opens the map, A accepts an AI offer, B turns on autopilot
- Click the left stick for the menu. Raise your hand and shake it to wave.

## 5. Missions (the main story)

There are 7 missions and they run in order. Each one teaches one part of our design. Just follow the banner, or keep pressing ENTER.

1. **First shift: meet your crew.** Check on Okafor, Tanaka and Rivera, then look at the AI dashboard (F1). This shows how the health data is collected.
2. **Tanaka's rough night.** Tanaka slept badly and the CO2 is high. Check on him, send him to rest, then clean the CO2 scrubber in Life Support yourself.
3. **Words at dinner.** Rivera and Tanaka argued. Bring them together for a team meal, then hold a short team meeting (F3). The AI never takes sides.
4. **Rivera feels low.** Rivera has been quiet for days. Set up a video call home, then answer the AI's offer to you (Y for yes, R for no).
5. **Bedtime: humans stay in charge.** Go to bed. During the night the AI breaks one of its bedtime rules on purpose. Watch it stop itself, undo the change and wake you up. You're in charge until you allow it again.
6. **Emergency drill.** Press F6, go to the muster point and wait until all 4 crew are safe.
7. **Report to the review board.** Run board question Q1 (F5), the 72-hour tabletop test (F4), and export your own data.

![A mission step card pops up for each step](e3_daily)

## 6. Checking on a crewmate (how the health data is collected)

Walk up to a crewmate and press **I** (VR: point and pull the trigger, or use the menu).

1. They agree first ("Sure, go ahead"). Nobody gets checked without saying yes.
2. You see their readings and where each one comes from:
   - **Wristband:** heart rate and HRV
   - **Chest beacon (UWB):** where they are and how much time they spend with others
   - **Skin patch:** skin response (GSR) and the cortisol patch
   - **Room sensors:** CO2 and air (voice and face analysis only happen in shared rooms, never in pods)
   - **Task data:** how fast and accurately they work
3. The six mental health levels are shown as coloured bars: anxiety, depression, conflict, fatigue, autonomy and monotony.
4. Two big buttons: **Check health** and **Help**. Help picks the activity that fits whatever that person is struggling with most (a rest, a team meal, a VR nature walk, the greenhouse, a call home…).

![Health check-in on a crewmate](e3_checkin)

## 7. Daily wellness tasks

Every morning at 07:00 you get a short list of 5 things that are good for the crew's mental health: check on someone, help someone, look after yourself (exercise), a team meal, and going to bed on time. Finishing them raises everyone's wellbeing. Press ENTER to do the next one.

![Today's wellness list](e3_today)

## 8. How the AI helps (three tiers)

- **Tier 1 (silent):** small changes you might not notice, like warmer light or calm sounds. They can always be undone, there are at most 3 a day per person, and every one is written in your log.
- **Tier 2 (offer):** the AI asks in plain words, like "want to take a 20 minute break?" Press Y for yes or R for no. If you say no, it stops and doesn't ask again for 6 hours.
- **Tier 3 (doctor):** for serious cases. The person is told first, then the CMO (Dr. Okafor) gets the alert on the CMO console (F2). If the CMO is out on EVA, the Commander is the backup.

![A Tier 2 offer](e3_tier2)

## 9. You are always in charge

- **O = AI OFF switch.** It stops every AI action and undoes everything it changed. Only a human can turn it back on.
- **Tab = Crew interface:** see your own data, everything the AI did for you, pause monitoring for 1 or 3 days, turn sensors off (privacy), and export or delete your data.
- **Night watch:** the AI is the only one "awake" at night, so it has bedtime rules. If anything goes wrong while you sleep (an alarm, the AI breaking a rule, an emergency, or a broken sensor), the AI turns itself off and wakes you, and you're in charge.
- **Hard limits:** some things the AI can never do, like sharing someone's data, touching life support, or telling mission control before the person knows. If it tries, it's blocked and counted on the dashboard.

![Night watch: the AI stops and you take over](e3_nightwatch)

## 10. Every screen and shortcut

- **F1** AI dashboard (live scores, sensors, tiers, hard limits)
- **F2** CMO console (Tier 3 cases)
- **F3** Team meeting (one-tap team choices)
- **F4** 72-hour tabletop simulation (Bonus A) with a saved report
- **F5** Board questions Q1–Q6, run live
- **F6** Safety drill / alarm
- **F11** Project ARES deliverables D1–D6 (all the design pages, with the drawn flowcharts and the dimensioned floor plan)
- **Tab** Crew interface (Bonus B)
- **J** Rubric checklist (25 tasks)
- **C** switches Config A and Config B (conflict layout)
- **T** time speed, **Z** zone info cards, **F** third-person view, **F12** screenshot, **Esc** closes things

![Project ARES deliverables screen (F11)](e3_flow_depression)

## 11. Rooms in the base

- **Core hub (two decks):** hygiene below, wardroom/galley and 4 sleep pods above
- **Crew quarters ×4:** private rooms, no cameras or mics
- **Commons, command, maintenance, medical / storm shelter, hygiene 2**
- **Lab, greenhouse, exercise + VR room, airlock with suitports**
- Every room has an ARES zone type (communal, private, work, exercise, nature, transition) that sets its lighting and what sensors are allowed.

![Dimensioned floor plan (F11 → D2)](e3_floorplan_dim)

## 12. Going outside and sleeping

- **Sleep:** press E on your bed (or B to find it). One real minute is the whole night, the lights go out, and you wake up to a sunrise.
- **Ladders:** press E on the ladder, then W and S.
- **EVA:** go to the suit room in the airlock module, use a suitport hatch and follow the 6-step suit-up. Come back to your suit's platform and press E to dock.

## 13. Exporting for the review board

- **F11 → Export all** saves every design page, the D4 ethics report and the threshold check to the `Reports` folder.
- **F4** saves the tabletop report. **Tab → Privacy → Export my data** saves your own log.

## 14. Troubleshooting

- **Stuck in a wall:** press U.
- **Can't find the next step:** look at the banner at the top or press ENTER.
- **Too many keys on screen:** press K to switch back to the simple key bar.
- **VR not starting:** make sure Quest Link is on before pressing Play.
- **The AI is doing too much:** press O. You're in charge.
