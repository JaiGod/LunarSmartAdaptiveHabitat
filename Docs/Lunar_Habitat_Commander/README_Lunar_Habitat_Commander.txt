LUNAR HABITAT COMMANDER  (unity2 - the new game)
================================================
A healthcare / habitat simulation built on the HUNCH proposal "Lunar Smart Adaptive Habitat Design with AI
Psychological Monitoring". Unity 6000.3.21f1, URP, VR template (OpenXR). Works with a VR headset and without one.

THE GAME
You run a lunar south-pole habitat with four crew for 30 days (one game day = 24 seconds, about 12 minutes total).
ARIA, the habitat AI, monitors each astronaut's heart rate, stress, mood and sleep and classes them as
Neutral / Underlying issue / Suspected illness (the three classes in the proposal).
 1. Commission the 8 NASA zones in adjacency order: Airlock, Hygiene, Crew Quarters, Galley, Workstations,
    Storage, ECLSS, Medical.
 2. Answer ARIA's alerts in the right zone before the timer runs out: stress alerts, medical alerts, sleep
    disruption, EVA dust, CO2 scrubber / water recycler faults (need a spare part), pressure leak, solar storm
    (shelter the crew), lunar night (start the 10 kW fission backup), dusty solar arrays, lost Earth link.
 3. Between alerts look after the crew: shared meals, exercise, greenhouse, VR relaxation, calls home, Earth view.
At the end you get a mission report scored against 7 rubric areas.

OPEN IT
- Unity Hub > Add > Add project from disk > official projects for nasa\unity2\Nasa_Project
- Scene: Assets/HUNCH/Scenes/Lunar_Habitat_Commander.unity (opens by itself). Press Play.
- Or run the built game: unity2\Lunar_Habitat_Commander_Build\Lunar_Habitat_Commander.exe

MENU PAGE
 The game opens with a story introduction ("Once upon a time, not so long from now..." and then gold words
 drifting away over the stars). Any key, click or trigger skips it. It plays once each time the game is started.
 Then the menu, with three ways to play:
   Survival mission - the real game: 30 days, alerts, score, plus your own health and hunger (see SURVIVAL below)
   Creative mode    - no hunger, no damage, unlimited blocks, free upgrades, no alerts, no score
   Autopilot        - the AI plays instead of you. It does the jobs in every zone, fixes broken panels, goes to
                      team meetings and votes, answers crew-health offers, collects battery modules and food,
                      picks up things to read, mines, and plays each mini-game map in turn. It keeps going
                      until you press SPACE (keyboard) or A (headset); nothing else stops it. It can also be
                      switched on in a Survival mission (menu > Base systems, or F6)
 and Kid mode, Voice chat, Options, How to play, Quit. Esc opens the pause menu during play (VR: left menu button).
 Options lets you show or hide: dashboard and panels, zone rings and labels, crew info tags, mood faces, robot buddy.
 It also switches camera effects, particle effects, automatic doors, sound effects, realistic space silence, music,
 spoken voices, voice chat, ARIA auto-care and kid mode. Choices are remembered. F1 = photo mode (hide everything).

TALKING
 T (VR: Y button) talks to the nearest astronaut, or to ARIA if nobody is near.
  - Pick a question (click, 1-7, or Up/Down + Enter), or
  - type a message and press Enter ("how are you", "what do you need", "status report", "go to the greenhouse"), or
  - turn on Voice chat in the menu and speak. With the talk panel open your speech is turned into text (shown in
    green as you talk) and answered. This free-speech part uses Windows online speech recognition: it needs internet,
    Windows Settings > Privacy > Speech > Online speech recognition switched on, and the audio goes to Microsoft.
 What an astronaut answers is built from their real stress, mood, sleep and health numbers. Talking gives a small
 real boost (social contact is one of the proposal's interventions).
 Voice chat phrases: "what should I do", "status report", "how is the crew", "go to the <zone>", "take me there",
 "next zone", "do it", "how are you", "what would help", "high five", "tell me a fact", "open menu", "resume".
 Spoken voices and the fixed command phrases run on this PC. Only free speech-to-text in the talk panel is online.
 If a PC has no microphone or Windows speech is switched off, the game stays text-only.

ARIA (the AI features)   Assets/HUNCH/Scripts/HunchAria.cs
 - Monitoring: classes each astronaut Neutral / Underlying issue / Suspected illness from mood, stress and sleep.
 - Forecast: fits a trend line to each astronaut's wellbeing over the last game day and predicts who will drop
   a class and in how many hours.
 - Advice: ranks what to do next (alerts first, then crew, then systems) and gives the reason.
 - Adaptive lighting: habitat lights follow an Earth day and night, and shift to help sleep.
 - Auto-care (optional): sends a service robot to do routine care when there is no alert.
 ARIA is a rule-and-trend system written for this game. It is not a neural network or a chatbot service, so it
 works offline and gives the same answer for the same situation. Typed chat uses keyword matching.

LIVING WORLD   Assets/HUNCH/Scripts/HunchWorldFX.cs
 - Sun: circles low on the horizon as at the lunar south pole, sets for the lunar night (days 10-19), rises again.
   Sunlight stays white (no air to redden it). Earthshine and brighter stars in the dark.
 - Sound like the Moon: inside you hear the habitat hum, footsteps, doors, alarms. Outside there is no air, so the
   habitat goes silent and muffled and you hear your suit's breathing and fan. Rocket launches are felt as a rumble.
 - Automatic sliding glass doors at doorways the game finds by measuring the building when it starts.
 - Warnings: red pulse at the screen edge and a flashing red beacon in the zone that needs you, alarm tone in the
   last 10 seconds, static and radiation clicks during a solar storm, controller buzz in VR.
 - Supply rocket launches on days 5, 15 and 25 and lands two days later. Satellites cross the sky.
 - Effects from the Hovl Studio "Magic Effects FREE" pack: Moon dust on footsteps outside, dust blast on the launch
   pad, sparks on broken equipment, venting smoke at a leak, healing glow and circle when an astronaut is treated,
   hearts and stars for high-fives, a blue shield on the storm shelter while the crew rides out a solar storm.

FINDING YOUR WAY   Assets/HUNCH/Scripts/HunchGuide.cs
 - Green arrows flow along the floor toward where you are needed (the most urgent alert, or a place you asked for).
 - A green marker shows the distance on screen, or an arrow points the way when the place is behind you.
 - Minimap (bottom right, desktop): a live floor plan sliced just above your head, north at the top, with compass
   letters, your heading, a GPS-style position (metres east / north of the spire, height) and the distance to go.
 - N opens the full base map. Click a zone and the arrows lead you there. Or ask ARIA "where is the greenhouse".
 - In VR the arrows are in the world and the direction, distance and floor are written on the dashboard card.
 - Distances are straight-line. Walls in the way can be passed by pushing against them (auto-door).
 - Respawn: fall for more than 5 seconds, or drop below the base, and you are put back where you last stood.

DAY AND NIGHT, ROCKET
 - Habitat lights follow an Earth day (dim and warm from 22:00, bright from 07:00).
 - The Sun circles low and sets for the lunar night on day 10, rising again on day 19. This also runs in Free
   explore, where the pause menu can skip to sunset or sunrise.
 - L (or the pause menu, or "launch the rocket") launches the supply rocket. It also flies on days 5, 15 and 25.

PICTURE AND FONTS   Assets/HUNCH/Scripts/HunchLook.cs
 - Font: Bahnschrift, which ships with Windows 10 and 11 (falls back to Segoe UI). No font file is in the project.
 - Filmic (ACES) tone mapping, high-quality bloom, anisotropic texture filtering, SMAA on top of 4x MSAA on desktop.
   Options > Cinematic picture switches the tone mapping off.

KID-FRIENDLY EXTRAS   Assets/HUNCH/Scripts/HunchKidFeatures.cs
 Robot buddy that flies ahead to show the way, 32 gold stars with Moon facts, high-fives, mood faces, a garden that
 grows when you tend it, 10 badges, confetti. Kid mode (K) gives more time and gentler rules.

SURVIVAL, REPAIR PANELS, HELPERS, VIEWS, SKY   (HunchFrontier3.cs, HunchFrontier4.cs, HunchIntro.cs)
 - Simple screen (default): top left shows Day, Health, Hunger, Oxygen, Power and one line with crew, solar power,
   radiation, Earth link and food. Top centre shows the one thing to do now, and a red line when something needs
   fixing. U switches to the full dashboard with every panel.
 - Health and hunger (Survival only): a full stomach lasts 15 minutes. X eats a food pack (+40 hunger, +8 health).
   You start with 3; jobs at the Galley and Greenhouse and the food rover give more. Easy mode eats for you when
   hunger is under 30. An alien bump costs 8 health (4 in kid mode), at most once every 3 seconds. A solar flare
   hurts while you stand outside. At 0 health you are carried back to the airlock and keep your backpack.
 - Five repair panels, one mini-game each (aim at a part and click / pull the trigger):
     Power Plant        Wiring          pick a wire on the left, then the socket of the same colour on the right
     ECLSS              Ventilation     press LOCK while the white marker is on the green part, three times
     Medical            Health monitor  press all nine buttons
     Workstations       Light switches  flip all five switches up
     Mission Control    Computer        watch four colours flash, then press them in the same order
   In Survival one panel breaks every 80 to 130 seconds (never two at once) with a red WARNING. While broken:
   wiring drains power, the health monitor raises crew stress, lights go dark, fans stop, the minimap goes off.
   Click a working panel to practise. These are the only things borrowed from Among Us - there is no impostor.
 - Messages: gold "YOU DID IT!" for an accomplishment or a repair, red "WARNING" for a fault, hunger, low health,
   dust storm, solar flare or meteor shower. 38 accomplishments (menu > Moon Frontier > Accomplishments).
 - Helper rovers (menu > Helpers, food, view and dance): food rover (2 food packs), medic rover (full health),
   repair rover (fixes the broken panel for you). Each needs 45 seconds to recharge.
 - First person is the default. V switches to third person (keyboard and mouse only) with an astronaut you can see.
   B dances for 8 seconds. In the headset a copy of your astronaut dances in front of you.
 - Sky: a relay satellite with a signal line from the telecom tower to the satellite and on to Earth, two orbital
   cannons that shoot down meteorites, and every minute or two one of: dust storm (solar arrays get dirty),
   solar flare (radiation rises outside), meteor shower (ore and ice land in the mining field). Twinkling stars
   and coloured nebulas (Options: Colourful sky).
 - Dyson swarm: the affordable kind of Dyson sphere. Not a shell round the Sun, just a few collector panels in
   orbit round it (you can see them circling the Sun disc). They beam power to the gold receiver dish beside the
   mining field. Each panel adds about 1 kW against the habitat's 9 kW load, day and night; when the Sun is down
   the beam is relayed by the satellite. You start with 3 panels; the "Dyson swarm panel" upgrade adds one
   (3 ore, 2 helium-3, 2 crystals), up to 8.
 - Jaineil's own 3D models (HunchFrontier5.cs, files in Assets/HUNCH/Resources/DriveModels): ten models from the
   NASA HUNCH "BEST MODELS" Google Drive folder, the small optimized versions, converted from .glb to .obj.
     in the sky      Red Ship, Futuristic Plane and Blue Supersonic Jet circle the base; UFO Stars hovers high up;
                     UFO Neon is the aliens' UFO; Orbital Satellite is the relay satellite
     on the ground   the rocket on its pad (Missle Launch), the Moon outpost (MoonBase), the telescope;
                     the Mars rover is the body of the two rovers you can ride
   About 150,000 triangles and 13 MB in all. Each ground model has a name plate.
 - Autopilot is a fixed tour, not a learning AI: panels, four zones, a high five, a dance, the mining field
   (zap, make a friend, drill, build, drive a rover), the food rover, the sky, the Dyson swarm, the light switch.
   Then it repeats.

MOON FRONTIER (adventure layer, HunchFrontier.cs - everything is built in code from simple shapes)
 - Mining field outside the base (Esc > Explorer > Go to the mining field, or walk out; look for the green beacon).
   Glowing rocks: water ice, ilmenite ore, helium-3. Hold the Mining drill on a rock to mine it. Rocks grow back.
 - Backpack (I): ice -> water +12%, ilmenite ore -> oxygen +10% (real ISRU idea), helium-3 -> power +15%,
   power cell -> power +25%. Crystals and ore buy upgrades. Walk over crates and crystals to pick them up.
 - Aliens: a UFO beams down up to six friendly-looking aliens. Zap one and it is beamed home (nobody is hurt)
   and leaves a crystal. If one bumps you, power drops 2% (not in kid mode or free explore). They can be turned off.
 - Tools (R, VR: B): Hands, Zapper, Freeze ray, Plasma launcher (upgrade), Mining drill, Block builder.
   Left click / right trigger uses the tool. With Hands, click still acts in a zone ring; E always does.
 - Blocks: dig the ground outside with the drill to get regolith blocks, place them with the Block builder
   (right click or the drill removes one). Up to 400 blocks. You start with 12.
 - Upgrades: Zapper Mk2, Plasma launcher, Drill Mk2, Bigger backpack, Jetpack boost, Auto-miner rig (up to 3),
   Solar array boost, Guard turret, Dyson swarm panel.
 - Power, lights and air: light switch (O), a cable network from the power plant to every zone with moving
   current lights (turns red in a blackout), a ventilation fan near the ceiling of every zone. Fans off = oxygen
   slowly falls during a mission.
 - Sky: Sun disc and glow that follow the real Sun direction, shooting stars. Two rovers drive round the field.
 - VR movement: left stick walk (click = run), right stick turn, right grip teleport, A jump, B next tool.
   The jetpack is the JETPACK button under the dashboard card (hold A to rise while it is on).
 - Finding the aliens: J (or the menu button "Go to the aliens") takes you to the field. A tall green light beam
   stands over it, and a line on screen says how far the nearest alien is and which way.
 - Things to use with the hand (aim at them; a prompt appears; click / right trigger):
     make friends with an alien (Hands tool) - up to 3 friends follow you outside and fetch crystals and crates
     ride a rover - it drives wherever you walk, 2x faster; look down and click to get off
     pick up a moon rock and throw it - a hit sends an alien home (VR: rocks can also be grabbed with the grip)
     the console beside every zone - one button for that zone's lamps, one for its ventilation fan
 - Guard turret upgrade: zaps aliens within 28 m, never your friends. Uses the imported Motion Sensor Gun model.
 - The gun in your hand is the imported Alien Blaster model on keyboard and mouse. It has 2.7 million triangles,
   so in the headset the simple block gun is used instead to keep VR smooth.

BASE SYSTEMS AND MAPS (added 10 Oct 2026 - HunchFrontier6.cs, HunchFrontier7.cs, HunchMusic.cs)
 Everything here is sized for the NASA HUNCH limit of FOUR crew: four health cards, four meeting seats, rations
 for four, and up to four players taking turns on one computer (menu > Base systems > Player; each player keeps
 their own mini-game best scores). It is not an online multiplayer game.

 Menu > Base systems opens all of these. Keys: Y maps, Z cameras, F2 health, F3 reading, F4 meeting, F5 music,
 F6 autopilot. In VR they are the second row of big buttons under the dashboard card.

 - Crew Health System. The NASA HUNCH crew-health web page rebuilt inside the game: a wall screen in the Medical
   zone with a card for each of the four crew (heart rate, blood oxygen, CO2, breathing, temperature), live pulse
   and breathing waves, and the same alert limits as the page. It is fed by the simulator, so low oxygen, fans
   off or a stressed crew member really change the numbers. The AI answers in three steps, as on the page:
   silent adjustment, options offered (you choose one), escalation (calls a team meeting). The scenario simulator
   (inject mild / moderate / severe) is in the menu.
 - LIVE LINK to the Crew Health web page (HunchLink.cs). While the game is running, open
   http://127.0.0.1:8765/ in a browser on the same computer (or menu > Base systems > Crew Health System >
   Open the full web version). The page then shows THIS game's four crew, their vitals, the habitat's
   oxygen, water and power, and the mission clock, refreshed every second; its header chip says
   "LIVE - linked to the Unity game". It works both ways: accepting an option, clearing a crew member or
   injecting a scenario on the page changes the game. The address 127.0.0.1 can only be reached from this
   computer, so nothing leaves it. When the game is not running the page (StreamingAssets/
   hunch-crew-health.html) falls back to its own simulation and the chip says "Offline - onboard".
 - Security cameras: one in every zone, one over the mining field and one on each map. Z (or a right stick click
   in VR) shows the live picture; arrows or clicks change camera.
 - Team meetings: every few minutes, when a crew member needs support, or when you ring the gold bell in the
   Galley, an alarm calls everyone together. Each crew member reports, then the team votes on a plan (rest hour,
   maintenance blitz or stock up on supplies). Your vote counts double. Nobody is voted out.
 - Battery Bank: 13 glowing green battery modules are spread round the zones and the mining field. Carry up to
   three at a time to the eight-slot rack at the Power Plant. Each one adds power; a full bank trickle-charges the
   habitat and switches in if the batteries ever run flat.
 - Things to pick up and read: seven tablets, including the Physics and Calculus Reference Sheet (Moon gravity,
   jump height, projectile arcs, energy as the integral of power, the best solar-panel angle from dP/d(theta) = 0,
   ore grade with depth, the pulse-wave formula). The numbers at the bottom of the sheet are measured live from
   the game: your speed, the battery's rate of change and the energy collected so far.
 - Astronaut food: canned peaches, stew pouches, freeze-dried ice cream and more in the zones. Each is a food pack.
 - Music: Space ambient, Happy EDM, Lo-fi chill and Chiptune (the last three are made by the game itself, so they
   are original), or "My music folder": put your own .mp3 / .ogg / .wav files in a folder called Music next to
   Lunar_Habitat_Commander.exe and the game plays them.
 - Systems reset: if oxygen, water, food or a crew member's health reaches 0%, or the batteries stay flat for 20
   seconds, the mission no longer ends. Everything is restored, you are brought back to the airlock, and the same
   day carries on. Your own health reaching 0 still brings you back to the airlock as before.
 - Deep veins: four clusters round the edge of the mining field with a big ore vein that is drilled in three
   stages, a crystal geode, helium-3 and ice.

 MAPS (the Map Gate at the mining field, the Y key, or MAPS on the VR dashboard). Thirteen maps; the central base is unchanged.
   1 Solar farm       tilt six panels to follow the Sun for 80 seconds; score = energy = the integral of power
   2 Moon mining pit  drill a 147-block rock mound in 90 seconds; ore gets richer with depth; you keep what you mine
   3 Alien zap range  60 seconds of hopping aliens to zap; their UFO beams them home, nobody is hurt
   4 Play park        a dance floor that makes you dance, bounce pads and big moon balls; no score, just fun
   5 Beat Asteroid    64 seconds of asteroids flying at you on the beat. VR: swing the two light sticks (blue is
                      left, gold is right). PC: aim and click
   6 Tower defense    aliens walk a path to the snack table; build and upgrade beam towers that send them home;
                      five waves
 Every gate is a stone portal (the Stone portal pack by Ordoss): green goes BACK TO BASE, yellow is PLAY AGAIN, blue is
 NEXT WORLD and red is LAST WORLD, so you can walk from world to world without going home; the purple one at the
 mining field is the MAP GATE. The stones float and turn, runes and sparks glow, a three-layer vortex spins in the
 opening, sparks orbit it, and everything speeds up as you walk close. Going through plays a warp: the portal
 spins up, the view washes violet, and you arrive in a burst. Choosing a map from the menu plays the same warp.
 The X button or a job in the base takes you home automatically.
   7 Factory Land     drill coal, iron, gold and stone; a power totem burns coal; smelters, a gear press, an engine
                      works and two factories turn ore into bars, gears, engines, tool kits and base panels; chests
                      show what has been made; a rocket pad ships parts to the base. A garden grows five crops in
                      soil, watered by a fish tank you feed. The machines, conveyors, robot arms and control cabinets
                      standing around the land are real models from the Unity Factory pack. No timer.
   8 Star Patrol      fly a star fighter in open space for 150 seconds; alien scout ships warp home when zapped;
                      then light the Dust Titan's five beacons to turn it away. Nobody is hurt.
   9 Story Studio     paint on a big wall (pictures are saved in a Drawings folder next to the game), play the
                      story "Cadet Nova and the Dust Titan" with three endings, read four alien stories, and train
                      with a light sword on glowing orbs. No timer.
  10 Star Clicker     click the star to make stardust, buy six kinds of helper that make it by themselves, catch
                      lucky comets, grow the star from protostar to galaxy heart, and go SUPERNOVA to start again
                      50% stronger. It keeps earning while you play other maps. Saved for each player. No timer.
  11 Science Academy  PHYSICS FORCE LAB: thirteen free-body diagrams from Physics 1, Physics 2 and Physics C
                      (table, rope, ramp, friction, projectile, circle, spring, buoyancy, Coulomb's law, electric
                      field, magnetic force on a charge and on a wire, parallel wires). The known forces are drawn;
                      you choose which way the missing one points, then it is drawn in gold with its equation.
                      SPACE BIOLOGY LAB: ten specimens and questions (plants, bones, muscles, fluids, DNA, cells,
                      tardigrades, the body clock, microbes, eyes).
                      HALL OF SCIENCE: ten famous scientists, the twelve people who walked on the Moon, everyone who
                      flew there on Apollo, and the NASA HUNCH exhibit about this project. Click a frame to read.
                      A frame shows a photo if you put a picture with the right name in the Pictures folder next
                      to the game (Pictures\READ_ME.txt lists the names). No pictures are shipped with the game.
  12 Clinic and Pet House   120 seconds: pick up the SCANNER and click a patient to find out what is wrong, then
                      bring what they need (water, bandage, oxygen, cool pack, warm blanket or a kind talk) before
                      their patience runs out. Next door lives Lumi the moon pup: FEED, PLAY, WASH and SLEEP. Lumi's
                      needs run down slowly while you play, Lumi grows from baby to grown-up, and caring earns
                      care points, experience and star coins. Saved for each player.
  13 Boss Arena       the Grumpy Moon Golem guards a Star Diamond. Zap its glowing tickle spots for three rounds in
                      100 seconds. Nobody is hurt: it laughs and hands the diamond over. The first battle is free,
                      after that a ticket costs 3 star coins in the Star shop.

 MY ASTRONAUT, STAR COINS AND THE STAR SHOP (Menu > Base systems)
 - Almost everything earns experience: accomplishments, finishing a mini-game, right answers in the Academy,
   patients, caring for Lumi, the boss. Level N needs 100 x N points. Each new level shows your astronaut in front
   of a green screen with the level number, a PRESENT to open (star coins and sometimes crystals, a food pack or
   stardust) and a NEXT box. The first time you play, the level 1 screen and a welcome present appear after about
   25 seconds. "My astronaut" in the menu shows the screen at any time.
 - Star shop: instant level-up (5 coins), boss battle ticket (3), stardust boost x2 for two minutes (2), and
   costumes. A costume changes your suit colour and may add a hat; you see it in third-person view (V) and on the
   level screen. Seasonal costumes (Halloween, winter, spring, summer) can only be bought in their season and
   are then yours for ever.
 - REAL MONEY IS NOT CONNECTED. One costume ("Golden Pumpkin King 2026") and "Buy star coins with real money"
   are marked as real-money items and cannot be bought in this version. The single place a store plug-in would
   go is StoreBuy() in HunchFrontier12.cs. Selling things needs your own developer account with a store (Steam,
   Meta, itch.io, Apple or Google), their in-app purchase kit, and their rules for games played by children.
 - Levels, coins, costumes, Lumi and the Star Clicker are saved for each of the four players on this computer.

 CREW RADIO (F7, or Menu > Base systems > Crew radio)
 - Talk into your microphone (the headset's in VR). The game shows RADIO: TRANSMITTING with a level meter, and
   when you stop a crew member or ARIA answers by voice and on screen; the answer depends on what is going on
   (a waiting meeting, low oxygen or power, a crew member who needs support, the map you are on). Checking in
   lifts that crew member's mood a little.
 - The game measures only how loud you are. It does not understand the words, and nothing is recorded, saved or
   sent anywhere. Windows shows its microphone-in-use icon while the radio is on. Turn it off with F7.

 SEASONS
 - The game reads the date from this computer and decorates for the season on Earth (northern half): leaves in
   autumn, snow in winter, petals in spring, sparkles in summer, and pumpkins from 1 October to 2 November.
   Decorations stand by the Map Gate and on every map. Menu > Base systems > Season effects turns them off.

 HOW REAL IT LOOKS
 - Every shaded object has a fine grain and bump map and a metal / paint / rock response chosen from its colour.
   Shadows are soft, sharper and reach further; colours are graded; on a screen there is ambient occlusion
   (soft contact shadows). In the headset ambient occlusion is switched off to keep VR smooth.
 - Models made in Blender for this game: the astronaut (level screen and patients), Lumi, the hospital bed, the
   pumpkin and the gift box. The Blender file is unity2\Blender\hunch_models.blend; the .fbx files are in
   Assets/HUNCH/Models. Menu HUNCH > Models: build the kit after changing them.
 - Most other things on the extra maps are still built from simple shapes.

NASA HUNCH MODE (Menu > NASA HUNCH mode; DISABLED when the game is first opened, so it starts as the full game;
the choice is remembered)
 ENABLED shows only what the NASA HUNCH project sheet asks for. Disabled is the full game, exactly as before.
 When it is enabled:
 - Only the main base is shown. The outer glass dome, the tall central spire and every site outside the dome
   (the far sites, keeps, spaceport, telecom tower, ocean) are hidden.
 - The crew habitat is under a regolith cover for radiation protection. Three greenhouses stand outside in sunlight.
   The aquaponics fish tank is inside the habitat by Life Support: fish waste feeds the plants outside.
 - No aliens, no UFO, no turrets or orbital cannons, no tools in the hand, no portals or maps, no mini-games, shop,
   costumes, pet, level-ups, season decorations or accomplishment pop-ups. Autopilot stays in the base.
 - ARIA's automatic service robots are switched on (autonomy with little help from Earth).
 - The spaceport is outside the main base, so it is hidden and the supply rocket does not launch in this mode.
 - The regolith cover is shown part-built at first: the entrance end of the crew habitat stands in the open with a
   low berm round it, so the habitat can be seen. Menu > Base systems > Regolith cover switches between part-built,
   complete (all of it buried) and hidden.
 - Also hidden, because no first crew could build them: the helix, the telecom mast and the floors of the central
   hub above Mission Control. The hub keeps three floors under a low dome roof modelled in Blender. The Habitat facts sheet gives the true size (the walk-through is drawn at about twice life size) and a
   three-phase build plan: hard, not impossible.
 - Teleport in NASA HUNCH mode (second line of the menu, ON at first): ON adds the Travel page and the jump to
   the rovers at the mining field.
 - The helper rovers (food, medic, repair) can be called from Base systems in this mode.
 - The checklist and documents page stays in the menu when the mode is disabled too, and boxes still tick there.
 - Switching the mode off brings back every menu page and every part of the world. (Fixed 10 Oct, 10 pm: the
   "Helpers, food, view and dance" page, with the first / third person switch, used to be lost.)
 - First / third person: V on the keyboard, or Menu > Base systems > View, or Menu > Helpers, food, view and dance.
   In a headset the view is always first person, as before.
 - Menu > NASA HUNCH checklist and documents: one box for each item the project sheet says must be there
   (deliverables, key considerations, lunar constraints, rooms). A box ticks itself when you show that item, for
   example by opening the Crew Health System or walking into the Crew Quarters; the top line of the screen says
   what to show next. "Untick all" clears it for a new audience.
 - Documents you can open in the game, also in the headset: Habitat facts and requirements, Sensors and what they
   detect, AI response protocol with consent and hard limits, the live Smart systems dashboard, and the live Crew
   health report (also saved as a text file in a Reports folder next to the game).
 - Privacy and consent: each crew member can switch AI monitoring off; the AI then stops watching that person and
   writes it in the log.
 - Still there, because they are part of the project: the Crew Health System and its web page, team meetings, the
   crew radio, security cameras, the Battery Bank, the tablets and reference sheet, lights and fans, VR.
 - The short hop-teleport on the right grip stays for comfort in the headset; walking with the left stick works too.
 - Honest notes: the wearable data and the AI's classes are simulated, not a trained model; the volume in Habitat
   facts is an estimate from where the rooms are; the regolith cover is a smooth mound sized from the habitat
   building, not sculpted soil.

FOOD, WATER AND THE BATHROOM (both modes)
 - No food lies loose. Every meal is a sealed can or pouch on a pantry rack with a lip on each shelf. The racks
   are placed by the game where there is free floor and a clear wall, so they do not sit inside other things.
 - The Hygiene zone has a space toilet (air flow, sealed solid waste, urine sent to water recovery, foot bars and
   handholds) and a sealed wash station (hand ports, rinseless soap, every drop recovered). Aim at either and click
   to read "Bathroom and hygiene on the Moon".

PORTAL PLAZA (NASA HUNCH mode disabled)
 - Beside the mining field where the aliens hop, a ring of 13 portals, one to every map. They use the portal
   modelled in Blender (LegendPortal: stone frame, two counter-turning rings, four crystals), each in its own
   colour with a five-layer vortex, orbiting sparks, a light and a hum you hear from its own place.
 - Every earlier portal is still there: the map gate, and NEXT WORLD / LAST WORLD / HOME on each map.
 - Note: Unity did not import the animation baked in Blender, so the rings and crystals are turned by the game.

REAL WALKING PATHS AND THE GUIDE ARROWS
 - A few seconds after the game starts it maps where you can really walk in the main base (a NavMesh made from the
   same solid shapes you bump into; it takes under a second). Autopilot Smooth and The Show follow that map: round
   the walls, through the openings, up the stairs. Inside the crew habitat every room is joined on the map.
 - Where the model has no opening between two buildings, the map stops. Only there does the autopilot use the
   game's auto-door (the same push-through a player gets), or the lift between floors.
 - The green guide arrows follow the same mapped way, turn the corners with it and tilt on stairs. The label gives
   the walking distance and the height, for example "36 m walk, 6 m up". With no mapped way it gives the
   straight-line distance and the height.

MOBILE CONTROLS (Menu > Base systems > Mobile controls; on by itself on a phone or tablet)
 - For a touch screen, with three buttons only: a walking stick under the left thumb (push it fully to run), drag
   anywhere on the right to look, ACT (the job of the zone you are in, or the thing you aim at), JUMP and MENU.
   Everything else is reached through MENU, where a tap chooses a line.
 - On a computer the mouse plays the part of one finger, which is how it was tested.
 - NOT DONE: there is no phone build yet. This project is built for Windows; an Android or iPhone build needs
   Unity's mobile build module, smaller textures and testing on a real phone.

THE SHOW (main menu > The Show; or Menu > The Show)
 - The mission as a cartoon you watch. The Commander strolls through the real 30-day mission (it is Autopilot Smooth
   underneath, at a walking pace) with episode title cards, speech bubbles, comic bursts when a job is done, changing
   camera angles, a LIVE strip and a closing card with the score. With voices on, each scene has a spoken line.
 - Nothing is scripted in advance: the story is whatever happens in that mission. SPACE (A in the headset) stops it
   and you play on from there. In a headset there are no drawn overlays: you ride along and the lines appear as notes.

CREW OF FOUR, COUNTING YOU
 - You are the Commander, with three crewmates: Dr. Okafor, Eng. Tanaka and Sci. Rivera. That is four people, the
   most the project sheet allows. There is no separate Commander character any more.

SLEEP (Menu > Base systems > Sleep mode; or point at a sleep pod and click)
 - Crew Quarters has one bedroom: a small room with its own door and two double bunks, four sleep pods in all,
   one each. They are the only beds the game adds.
 - The crew lie down in their pods from 22:00 to 06:00, and whenever you switch Sleep mode on (lights dim; ARIA
   wakes everyone if the habitat needs them; a rest period lasts ten game hours).
 - You can sleep in your own pod: stand in Crew Quarters and choose it. The clock runs eight times faster until
   06:00. Move or press a key to get up.

THE CENTRAL HUB (Menu > Base systems > Central hub)
 - Cleared removes the trees, planters, desks, screens and couches from the central hall (off at first: the trees stay).
 - The trees in the centre dome are artificial, and signs there say so: lifelike models that lower stress, with no
   water, soil or pests to look after. They are one
   baked piece of the Blender model, so it is all or nothing; the hall, stairs and floors stay. The other choice
   brings them back. The change is made the next time the game starts.

AUTOPILOT SMOOTH (main menu > Autopilot Smooth; or F9; or Menu > Base systems > Autopilot Smooth)
 - The AI plays the real Survival mission and goes for the best score. From the main menu it starts the mission
   itself: the commissioning walk through the 8 zones in order, then all 30 days.
 - It moves like a player: it walks (5 m/s) and runs (10 m/s, the same speeds as the keyboard), slips round things
   in its way, pushes through doors with the game's auto-door and rides the lift between floors. On a screen you
   watch your astronaut from behind. Only when it has been stuck for several tries does it hop to the zone; the
   result sheet counts how often that happened.
 - How it decides: before every job it makes a silent copy of the habitat simulation and plays the next 70 seconds
   ahead for every job it could do, and every job it could do after that, with the real walking times. It reads the
   score the mission's own rubric would give and takes the best first step. (Look-ahead search on a model of the
   game. It is not a chatbot and uses no internet.)
 - It also eats when hungry, repairs broken panels, walks to team meetings and votes for the choice that helps the
   mission most, accepts the Crew Health System's offers, and high-fives crew it passes.
 - The top of the screen shows the day, the score so far, the metres walked and the reason for the current job.
   SPACE (A in the headset) hands the controls back. The result is saved in the Reports folder next to the game.
 - In free explore there is no score, so it only walks a round of the zones.
 - The old Autopilot (F6) is unchanged: it jumps from place to place and also plays the mini-games.

PHYSICS VIEW (F8, or Menu > Physics view; off when the game starts; works in both modes)
 - Arrows on you: velocity, weight (m g, Moon gravity 1.62 m/s2) and the normal force from the floor.
 - A jump draws its parabola; a 5 x 5 field of arrows shows the gravity vector field.
 - Lines from every zone's sensors to Mission Control with moving packets show the computer network.
 - A board that follows you plots height, speed and acceleration against time, with an energy line.
 - Menu > Read: Maths and physics explains the ideas in order: mechanics, vector fields, calculus 1 to 3,
   differential equations, linear algebra, abstract algebra and networking, each tied to something in the base.

CONTROLS WITHOUT A HEADSET
 E / left click  act in the zone you are standing in     Tab / Q  next / previous zone     1-0  zone number
 G  jump to the most urgent alert      WASD + mouse  move and look      T talk      Esc menu      F1 photo mode
 N  full map      L  launch the rocket
 R  change tool     click  use the tool / the thing you aim at     I  backpack     O  lights     J  go to the aliens
 X  eat a food pack     V  first / third person     B  dance     U  simple screen / full dashboard
 Keep walking into a door or wall for a moment to pass through it      PageUp / PageDown  lift to the next floor
 Shift run   Space jump   F fly   P pause   M music   H help   K kid mode

CONTROLS IN VR (PC VR through OpenXR: Quest + Link, SteamVR...)  -- runs on a Meta Quest through Meta Horizon Link
 Every button has ONE job, and it is the same everywhere:

   RIGHT hand                                   LEFT hand
   Trigger   select / use / fire                Trigger   select too
   Grip      teleport: hold to aim the arc,     Grip      grab the recreation objects
             let go to jump there               X         go to the next job (the zone that needs you)
   A         jump (in a menu: choose)           Y         menu (in a menu: close)
   B         next tool (in a menu: back)        Stick     walk, click it to run
   Stick     turn in steps

 A thin laser comes out of the right hand. It turns gold on anything you can press: a glowing zone orb, a
 menu button, a repair-panel part, a rover, or the big buttons under the dashboard card
 (MENU, ALIENS, TOOL, JETPACK, TALK, RUN, URGENT, EAT). Pull the trigger to press it.
 The teleport arc is green where you can land and red where you cannot.
 Jetpack: the JETPACK button under the card switches it on and off (never by accident). While it is on, push
 the left stick and look where you want to fly; hold A to rise.
 Autopilot keeps playing until you press A. Triggers, grips and sticks do not interrupt it.
 Easy mode (on by default) picks the right tool for what you aim at and does the job when you stand in the
 urgent zone ring, so most of the game needs only the trigger.
 In a menu: point the laser at a button and pull the trigger, or move the highlight with a stick and press A.
 The menu card stays still while it is open so the buttons are easy to aim at.
 Keep pushing the stick against a door or wall to pass through. Another floor: press X, or Y and then Travel.
 If something feels wrong in the headset: the game writes vr_log.txt next to Lunar_Habitat_Commander.exe
 (what the headset and every button reported, once a second). Send that file with the bug report.
 Dashboard on your wrist: menu > Explorer     Grab the recreation objects with the grip button
 Touch an astronaut's raised hand for a high five. The controllers buzz on alerts and when an action works.
 The dashboard card moves with you and never sits inside your body. The robot buddy circles round you on a
 ring, stays at least 1.2 m away and keeps clear of the card.

MOVING AROUND (so nobody gets stuck)
 - The building is 2x the original size (HUNCH_Final_Habitat.blend, scene property hunch_scale = 2.0), so every
   room, door and corridor is roomier.
 - The player body is small: 1.5 m tall, 0.18 m radius, steps up 0.45 m.
 - Auto-door: push against any door, wall, stair or furniture for 0.3 s and you are moved to the next free floor.
 - Lift: go straight to the storey above or below.
 - Zone jump keys always work.

EDITING
 - Zones (names, text, positions in Blender coordinates): Assets/HUNCH/stations.json, then menu HUNCH > Rebuild scene only.
 - Rules, events and scoring: HabitatSim.cs.   Controls and screens: HabitatGame.cs.   Menu, options, talking: HunchMenu.cs.
 - ARIA: HunchAria.cs.   Voices: HunchVoice.cs.   Sun, sound, doors, rocket, effects: HunchWorldFX.cs.
 - Menu HUNCH > Build Windows app (.exe).   Menu HUNCH > Use imported effect packs (after importing or updating a pack).
 - The model the game imports is unity2\Model_Export\HUNCH_Lunar_Base_x2.fbx.
 - Assets/HUNCH/Bridge and Assets/HUNCH/Editor/HunchProbe.cs are development helpers used to test the game from
   outside the Editor. They are not part of the game and can be deleted.

ASSETS USED
 - Unity's VR template assets and XR Interaction Toolkit samples (rig, teleport pads, grabbable objects).
 - Magic Effects FREE by Hovl Studio (Unity Asset Store, Standard Unity Asset Store EULA). Its materials were
   converted from the built-in pipeline to URP by HUNCH > Use imported effect packs.
 - High-Detail Alien Blaster Weapon by OlegVerenko and Motion Sensor Gun by Duanes Mind (Unity Asset Store,
   Standard Unity Asset Store EULA).
 - Ten 3D models by Jaineil Rana from his NASA HUNCH collection (made with an AI 3D model generator; the mesh
   names say Tripo). Check that tool's terms before selling the game.
 - TELEPORTER effect by M31, Hyper Casual FX by Lana Studio, Procedural fire by Hovl Studio (Unity Asset Store,
   Standard Unity Asset Store EULA).
 - Unity Factory by Unity Technologies Japan (Unity Asset Store, Standard Unity Asset Store EULA): sixteen pieces
   (machines, conveyors, robot arm, control cabinets, cart, wagon, product) are used in Factory Land. The pack is
   made for HDRP; HUNCH > Factory pack: convert materials to URP switched its materials to URP Lit.
 - Astronaut, Lumi, hospital bed, pumpkin, gift box and the LegendPortal of the portal plaza: modelled in Blender
   for this game (Blender files: unity2\Blender\hunch_models.blend and hunch_legend_portal.blend).
 - Stone portal by Ordoss (Unity Asset Store, Standard Unity Asset Store EULA): the five portals are the map gates.
   Its effect shader (StonePortal_BlendHDR.shader) was given single-pass VR support so the runes and sparks draw in
   both eyes of a headset; a note at the top of that file says so.
 - Music: "Out There" by yd, opengameart.org, CC0.
 - Sound effects are generated in code. Voices are the ones installed in Windows.

NOT DONE / KNOWN LIMITS
 - NASA HUNCH mode, the portal plaza, the Physics view, the pantry racks and the bathroom (10 Oct 2026, evening)
   were tested by letting the game drive itself on a flat screen, with no errors. They have NOT been tried in a
   real headset.
 - Not yet in NASA HUNCH mode: the document's model-accuracy numbers on the health wall, and an uncrewed-mode demo.
 - Maps 7 to 13, the radio, levels, shop, costumes, pet and seasons (10 Oct 2026) were tested by letting the game
   drive itself on a flat screen: every map opens, every game can be played to the end, no errors. They have NOT
   been tried in a real headset. The radio was tested with a pretend microphone level, not with a real voice.
 - The Hall of Science has no photographs until you add picture files (see Pictures\READ_ME.txt).
 - The Unity Factory pieces lost their metal and smoothness maps in the conversion (colour, normal and occlusion
   maps were kept), so they are a little plainer than in the pack's own demo scene.
 - Real-money purchases are not connected (see the Star shop notes above).
 - The base systems and the six maps above were tested by letting the game play itself with keyboard-and-mouse
   input and with the headset view on a flat screen (menus, pick-ups, bank, health wall, meeting, cameras, each
   map, autopilot, systems reset). They have NOT been tried in a real headset; Beat Asteroid's light sticks in
   particular need a real pair of controllers to judge.
 - The VR controls were rewritten on 10 Oct 2026 (one job per button, own laser, own teleport, own walking).
   The menus, the chat card, the dashboard buttons, X and the teleport were checked on a flat screen with the
   headset view switched on; they have NOT yet been tried in a real headset since the rewrite.
 - The repair panels, survival, helper rovers, third person, dance, sky events and Autopilot were tested with
   keyboard and mouse only. In the headset they are wired to the trigger but have not been tried yet.
 - Third-person view is keyboard and mouse only. The green waypoint arrow is a little off while it is on.
 - Health, hunger and food are not saved between runs.
 - Throwing a rock at an alien, the turret firing and the VR grab of rocks were built but not watched working.
 - Moon Frontier was tested with keyboard and mouse only. The VR run, jetpack, tool and fire buttons compile and
   are wired up but have not been tried on the headset yet.
 - Backpack, blocks and upgrades are not saved between runs.
 - VR was connected on a Meta Quest with Link (headset and both Touch controllers tracked, about 70 fps in the Editor).
   The pointable VR menu buttons are new and still need a full play-through on the headset. Hand tracking is untested.
   On the Quest, Meta Horizon Link needs Settings > General > Unknown sources switched on to run it from Unity.
 - Voice chat has not been tested with a microphone. Commands are a fixed list; free speech needs Windows online
   speech recognition and is matched to answers by keywords.
 - The green arrows point in a straight line and do not route around walls.
 - Doors are found automatically. Only openings with a lintel, a wall beside them and clear floor on both sides
   get a sliding door: 6 doors, all in the corridor door frames. Wide openings without a frame have none.
 - Medical treatment is one action, not yet a multi-step check-in / vitals / record / treatment sequence.
 - Gamepad can act, talk and change zones but cannot walk or look yet.
 - About 6 million triangles: PC VR only, too heavy for a standalone Quest.


CHANGES OF 11 OCT 2026
 - The game starts with NASA HUNCH mode disabled (the full game). Switch it on in the menu.
 - Walk through walls (cheat): Menu > Base systems. OFF at first: walls, doors and furniture are solid for you and
   for the autopilots. Where the model has no doorway between two buildings, use Travel (or G); the autopilots make
   a short "transfer" there and count it in their report. ON gives back the old auto-door (push a wall, pass it).
 - The central hub is the original tall tower in both modes. A shortened hub and a new shell were tried and removed.
 - The regolith cover over the crew habitat is hidden unless you choose it (Menu > Base systems > Regolith cover).
   When shown, it shrinks so that it never overlaps a neighbouring dome, and loose Moon rocks are moved out of it.
 - In NASA HUNCH mode the ships flying overhead and the rocket prop are hidden as well.
 - Crew Quarters: the four sleep pods are two double bunks inside one small crew bedroom. No beds in the corridor.
 - The centre dome has four ARTIFICIAL trees (modelled in Blender), each with a sign saying it is not a real plant:
   lifelike models placed there to help the crew's mood.
 - The walkable map now also works in the built game (the base model is imported with Read/Write on). Before this,
   Unity printed "does not allow read access" errors and the map would have been missing in the .exe.
 - The Unity Console is clean while playing. The lines that remain are Unity's own: "NoSubscription" comes from the
   Unity AI Assistant package (it needs a paid Unity subscription) and is not part of this game.
 - Autopilot Smooth, measured in the Editor at 6x speed: 95 / 100 with the wall cheat on; with walls solid see
   Reports/autopilot_smooth_*.txt for the latest figure.

ADDED LATER ON 11 OCT 2026
 - Play as any of the four: F6 (or Menu > Base systems > Play as) steps you into the next crew member. You appear where
   they are standing. The one you leave gets their body back and carries on with their own day by themselves.
 - Cheats and anti-cheat (its own menu page): Anti-cheat ON switches every cheat off at once and locks them. Otherwise
   each one has its own switch: walk through walls (off at first), fly, jump to zones, skip time (on at first).
 - Friendlier screens: every panel, menu line and bar has rounded corners, menu lines that are on/off show a little
   switch, and the letters are a round kid-friendly style (Comic Sans, part of Windows). Menu > Base systems > Letters
   switches back to the technical letters; the change shows the next time the game starts.
 - G always does something: it takes you to the alert, or to the next zone of the commissioning walk.
 - The artificial trees in the centre dome stand on the floor you can see.
 - NASA HUNCH mode has its own cheat switches and every one starts off (walls, flying, time skip). Jumping to zones
   stays on in both modes as a helper, because the base model has no doorways between its buildings.
 - Menu lines are colour-coded by how often they are used: green at the top, yellow in the middle, pink at the bottom.
 - Four more tools (full game), modelled in Blender with baked textures (brushed silver, hammered gold, gem facets):
   Plasma repeater, Laser cannon, Star minigun, Comet sword. Like the zapper, a hit beams an alien home; nobody is hurt.
 - The Fun Zone by the mining field (full game; Menu > Base systems > Fun Zone):
     Emote stage: ten buttons, one move each.   Zero-G dome: you float, shapes drift, eight stars to catch.
     Space Slicer: 45 seconds of asteroids, ice and comets to slice with the Comet sword; satellites cost 3 points.
 - Team meeting table in the centre dome (both modes): four chairs, four desk screens and one big round button.
   Pressing it calls the crew to their chairs; the clock stops while everyone reports and you vote.
 - Live tracking board behind the table: height, position, head direction, movement and what you look at, saved once
   a second to Reports/tracking_*.csv. Eye direction only on a headset with eye tracking. Face tracking and heart
   rate are NOT measured: a Quest 2 or 3 has no sensors for them.
 - VR mirror astronaut (Menu > Base systems, in the headset): copies your two arms from the controllers.
 - A calmer clock (36 seconds a day instead of 24) and a forgiving score curve with letter grades:
   A 93+, B 78+, C 62+, D 45+, F below. A steady mission lands in B; an A needs nearly everything right.
 - Any jump to another place takes you off a rover first.
 - Telecom tower and its Earth link stay visible in NASA HUNCH mode.

==============================================================================
NEW IN THE 5:12 PM BUILD (Sunday 11 October 2026)
==============================================================================
Full game only (NASA HUNCH mode OFF):
  - FIVE GIANT PORTALS stand outside the main base. Walk into one (or click it)
    to travel to another moonbase. Each base has its own game:
      Slicer Base: Space Slicer      Zero-G Base: Zero-G Dome
      Dance Base: Emote Stage        Star Shower Base: Star Shower
      Target Base: Target Tower
    A portal at each base brings you back. A rover lets you off by itself
    when you travel.
  - NINJA STARS: tool number 11 (press R to change tool). They fly with Moon
    gravity, spin, leave a trail and bounce once.
  - ASTEROID BLASTER: press the red button, sit in the gunner's seat, aim at
    the sky and hold the mouse button (VR: right trigger). 60 seconds.
  - The Moon ground never ends. A ring of blue lights 1,500 m out is the
    border; past it you are turned gently back.
Both modes:
  - The crew works by itself. The three you are not playing walk to their
    own jobs and answer alerts. F6 (or "Switch player" in the menu) puts you
    in the next crew member.
  - You cannot fall through the floor: you are put back on safe ground.
  - Menus: scroll with the mouse wheel when the list is long, round X to
    close.
  - All settings and cheats go back to their start values in every new game.
  - Accomplishments earned in an earlier game (your saved level, saved star
    clicks) no longer pop up as "YOU DID IT!" the moment the game begins.
Not tried in a headset yet: everything in this list.

==============================================================================
NEW IN THE SUNDAY NIGHT BUILD (11 October 2026)
==============================================================================
Ways through the base
  - The Blender model has rooms and buildings with no doorway between them.
    The game now finds every such place and puts a blue AIRLOCK DOOR (walk
    into it) or a green LIFT pad (stand on it for a moment) there. You, the
    crew, the autopilot and the green arrows all use them. Every zone can be
    walked to with every cheat off.
  - The autopilot no longer walks through walls.
Team meeting
  - Press the big round button on the table in the centre dome, or F4. You
    are brought to the table (the one jump that always works).
  - Four votes in all: the three crew and you. Nothing is decided until you
    vote; a tie goes your way. Close the page and the meeting waits. Walk or
    teleport away and it ends with nothing decided.
Crew
  - The crew walk smoothly (no more popping in and out).
  - They look after themselves: a calm break in the Sanctuary, a check-up in
    the Medical Bay, and they call the medic or food rover to themselves.
  - F6 switches you into the next crew member, right where they stand.
Floors
  - Centre dome: a raised, tiled floor that meets the bottom stair.
  - Each game at the other moonbases stands on a level, raised play floor,
    and the Moon ground is flat around every base.
Sky
  - Stars glow, twinkle and sometimes sparkle. Once in a while a far-away
    star flares up as a small supernova and fades.
Smooth on every device
  - If the frame rate drops, shadows, detail and the far ground step down a
    little (in the headset also the picture size), and back up when there is
    room.
Console
  - No errors and no warnings from the game. Notices from Unity's own XR and
    AI packages (no eye tracker, hand tracking off, no AI subscription) are
    filtered out.
