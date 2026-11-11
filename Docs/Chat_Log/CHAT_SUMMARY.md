# Project ARES — what was asked for, and where it lives

This summarises every request in the build chat for the two NASA HUNCH 2025–26 games (*Lunar Smart Adaptive Habitat Design with AI Psychological Monitoring for Crew Adaptive Positive Mental Health*).

There are two games built from one rubric:

| | **Realistic** — *Moon Base Final* (`unity 3`) | **Science fiction** — *Lunar Habitat Commander* (`unity2`) |
|---|---|---|
| Feel | Something real astronauts could train on, at 1/6 g | Big bases, the ARIA AI bot, portals, aliens and mini-games |
| Crew | Up to 4 crew, including the player | The commander plus crew in the **centre dorm (kept on purpose)** |
| Code folder | `Assets/Unity3/Scripts/01_Core … 07_Deliverables` | `Assets/HUNCH/Scripts/01_Core … 06_Frontier` |

## 1. Habitat design (Blender, then Unity)
- **Base:** a connected base, *"small but big enough"*, scaled up 2×. It has hallways, airlocks and every room the rubric asks for, including a **lavatory** researched from real space toilets.
- **Sleeping:** **4 private sleep domes** off the hallway, one per crew member. The lights blink and change colour with each astronaut's mood and mental health.
- **Doors and stairs:** doorways wide enough to walk through without snagging (no sliding hatch blocking the way), working ladders and stairs both ways, sealed windows, and nothing floating or out of place.
- **Spacewalks:** suits are worn only outside, there's a suit locker (up to 5 suits) and a proper suit-up flow through the airlock. No jumping out of the base.
- **Printable models:** pieces come apart so a teammate can rebuild them. There are open and closed STL versions, a print guide and a parts list (`3D_Print_Kits/`).

## 2. AI wellness and health monitoring (the core of the rubric)
- A **mood simulator** tracks the "big 6" feelings, plus a **comm/biometric unit strapped to each suit**, which is how the data is actually collected.
- A **real-time crew health dashboard** that a normal person can read.
- The **AI runs sleep lighting and music**. If something goes wrong during sleep, or the AI is being "bad" to a human, **the AI stops and the human takes over** (authority hand-off, opt-out, override).
- **Team meetings**, a daily check-in, journal prompts, and flowcharts for anxiety, depression, conflict, fatigue, monotony and autonomy.

## 3. Gameplay and usability
- **Start-up:** a vibrant VR "box" menu at the start, keyboard shortcuts on screen, and a map, navigation arrows and stats in the top-left. **Autopilot is optional, never automatic.**
- **Tasks:** **simple tasks with a pop-up task bar** that tie back to mental health, and crew orders that assign tasks to teammates.
- **Look:** a kid-friendly UI with rounded rainbow buttons, a sine-wave animation, and bold, colour-coded text. Third-person view is available.
- **Realism:** dust storms, alerts and realistic props (plants, cargo, tools) that you can push and that sometimes float. The sci-fi game's best features were carried over **without aliens or portals**.
- **Play anywhere:** in VR (Meta Quest), on desktop, on mobile with touch, and **in the browser** through the website and Unity Play.

## 4. Deliverables and grading
- **Rubric:** go through every line of the NASA HUNCH rubric, be harsh when self-grading, and aim for 90–100.
- **Annotated copies:** copies of the official docs and slides with *what we did* written under each instruction, with screenshots, in a human voice.
- **Supporting documents:** the Bonus A tabletop report, the threshold validation report, the D2 floor plans, the D4 ethics report, and a player manual for each game.
- **Publishing:** GitHub (JaiGod/LunarSmartAdaptiveHabitat, pushed as SuperCoolJai), the website with a Rubric page and a Final Papers section, the "complete designs" download, and a mission-control site redesign. Unity Play pages for both games have a title, description, thumbnail, trailer and a mobile toggle.
- **Model for a friend:** the realistic 3D model with its colours and textures, as `.blend` and `.glb`, plus a base-only copy with a patch of ground.

## 5. Ground rules that applied the whole time
- Credits: **© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.**
- Don't touch anything connected to Supabase or `idle-mining-empire`. Never break the Unity projects (scripts were moved with their GUIDs kept, and backups were taken first).
- Folders unrelated to NASA were moved out of `Downloads\Nasa` into `C:\Users\jaine\Unrelated_Projects`.
