# Bug list — both games

Fix HIGH first. Tick a box only after it was seen working in Play mode.
R = realistic sim (Moon_Base_Final) · C = Commander (unity2)

## HIGH
- [ ] R · Compile check after the Nov 11 batch (task board, LEDs, dressing, debug panel, sleep-in-bed) — waits for Play to stop
- [ ] R · Ceiling lights hanging below curved roofs → run `invoke HunchCeilingSnap.Run` with Play OFF, check `HunchBridge/ceiling_snap.txt`
- [ ] C · Crew must sleep in the 4 existing centre-dorm beds — check `sleepPlaced` (debug) says "dorm beds: 4 found", no generated bedroom
- [ ] R · Interaction card / toast / crew card overlap at 1920×1080 and 1366×768 (clean HUD on)
- [ ] R · Ladder E only when facing the ladder; never steals E from a nearby object
- [ ] R · "Inside but shows outside" zone fallback — walk every module once, debug panel F9 shows the room
- [ ] Website · GitHub Pages stale since Nov 7 → Actions → Request Pages build → Run workflow (needs a click on github.com)

## MEDIUM
- [ ] R · LED pathway strips: check none float in mid-air on curved dome walls (F9 → screenshot each module)
- [ ] R · Habitat dressing: no item blocks a doorway or overlaps a smart object
- [ ] R · Wardroom table refit: touch display sits flush, props don't clip
- [ ] R · VR: trigger runs the selected action, stick-click cycles, card readable at 1.1 m
- [ ] C · Pod LEDs: amber night light while asleep, sunrise 05:15–06:00, red in a storm
- [ ] C · Zone signs only within 9 m; corridor strips blue, red in an alarm
- [ ] Both · Lighting pass (materials too flat/white in places)

## LOW
- [ ] R · Bed redesign: compact wall-integrated sleep pod instead of a free-standing bed
- [ ] Both · Ambient sound pass
- [ ] Both · More decorative props only where they have a job

## Debug tools
- R: **F9** debug panel (FPS, position, room, aimed object, O2/CO2/power, memory, crew & object counts, last logs).
  Dev tests are on the panel only: 1 ladder ride · 2 door walk · 3 repair alert · 4 reward · 5 next smart object.
  Scene view gizmos: cyan zones · blue routes + crew paths · green interaction reach · yellow faults.
- C: **`** (left of 1) debug panel (FPS, camera, zone, day/hour, O2/water/food/battery, systems, alerts, last logs).
- Logs are tagged: `[Lighting]`, `[Interaction]`, `[Doors]`, `[Ladder]`, `[Missions]`, `[Crew]`, `[Render]`… via `HLog.I/W/E`.
- Script indexes: `Unity_Game_Moon_Base_Final/SCRIPTS.md`, `Docs/Lunar_Habitat_Commander/SCRIPTS.md`.
