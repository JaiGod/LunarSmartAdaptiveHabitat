// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/C_Base_Crew_Systems/HunchFrontier16.cs
//  Section : 06_Frontier/C_Base_Crew_Systems
//  Folder  : Systems inside the central base: pick-ups + battery bank, NASA HUNCH mode, physics
//            view, Autopilot Smooth, crew of four + sleep pods, The Show, self-working crew,
//            airlocks/lifts, crew self-care.
//  Purpose : Sixteenth part of Moon Frontier: Autopilot Smooth. The AI plays the real mission and
//            goes for the best score, and it moves like a player does:
//  Contains: HunchFrontier
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// Sixteenth part of Moon Frontier: Autopilot Smooth.
/// The AI plays the real mission and goes for the best score, and it moves like a player does:
/// it walks, runs, slips round furniture, uses the doors and the lift. It never jumps straight to a zone
/// unless it has been stuck for a long time (that is counted and shown, so you can see how often it happened).
///
/// How it decides (the "AI"):
///   - It makes a silent copy of the whole habitat simulation (HabitatSim.Clone).
///   - For every job it could do next, and every job it could do after that, it plays the copy forward
///     70 seconds, with the real walking times, and reads the score the mission's own rubric would give.
///   - It does the first job of the best pair, then thinks again. This is look-ahead search over a model of the game
///     (model-predictive control). It is not a chatbot and it does not use the internet.
/// Start: main menu > Autopilot Smooth, F9, or Menu > Base systems. SPACE (A in the headset) hands the controls back.
public partial class HunchFrontier
{
    [NonSerialized] public bool autoSmooth;
    const float SmWalk = 5.0f, SmRun = 10.0f, SmHorizon = 70f;      // the same walking and running speeds a player has on the keyboard
    int smNudges, smPasses0, smLifts0, smHops, smTransfers, smPlans, smJobs, smTrips, smFives0, smPlanZone = -1; float smSpeed = 7f, smWalked, smPlanMs, smPlanGain, smStarted; string smPlanWhy = "", smResult = "", smTrace = "", smHopLog = "";
    readonly List<int> smOpt = new List<int>(), smOpt2 = new List<int>(); Vector3 smHub; readonly List<Vector3> smPath = new List<Vector3>(); int smPathTrips, smDoorTrips;

    public void StartAutopilotSmooth()
    {
        if (!ready) return;
        if (autoOn) StopAutopilot(null);
        if (sim.phase == HabPhase.Briefing) game.StartMission(false);      // the real mission: the one that can be won
        if (!Playing) return;
        if (mapNow != 0) GoMap(0);
        game.flyMode = false;
        autoOn = true; autoSmooth = true; game.autoBusy = true; autoSince = Time.unscaledTime; autoLine = "Starting..."; autoWasA = true;
        if (autoTake == null) { autoTake = new InputAction("takeOver", InputActionType.Button); foreach (var b in new[] { "<Gamepad>/buttonSouth", "<Gamepad>/start" }) autoTake.AddBinding(b); }
        autoTake.Enable();
        smPasses0 = nav ? nav.passes : 0; smLifts0 = nav ? nav.lifts : 0; smFives0 = sim.highFives; smHops = 0; smTransfers = 0; smPathTrips = 0; smDoorTrips = 0; smPlans = 0; smJobs = 0; smTrips = 0; smWalked = 0f; smStarted = Time.time; smResult = ""; smTrace = ""; smHopLog = "";
        autoCo = StartCoroutine(AutoSmoothRun());
    }

    string SmoothHudLine()
    {
        if (sim.phase != HabPhase.Mission || Creative) return "the AI walks, runs and plays to win";
        return "day " + sim.Day + " of " + HabitatSim.MissionDays + "   score " + sim.Overall().ToString("F0") + "   walked " + Mathf.RoundToInt(smWalked) + " m";
    }

    // ------------------------------------------------------------------ moving like a player
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    /// last resort when the way is truly blocked: counted, so the result can say how often it was needed
    void SmoothHop(Vector3 target, int zone, string why)
    {
        smHops++; if (smHopLog.Length < 900) smHopLog += (zone >= 0 ? game.zones[zone].id : "spot") + ":" + why + " from " + Feet().ToString("F0") + "; ";
        if (zone >= 0) game.GoTo(zone); else TeleportTo(target, cam.eulerAngles.y);
    }

    /// walk (or run) to a place. Slips round things in the way, pushes through doors, and takes the lift between floors.
    IEnumerator SmoothTo(Vector3 target, float arrive, bool run, Func<bool> abort, int zone)
    {
        if (!nav) { SmoothHop(target, zone, "no body"); yield break; }
        bool vr = game.HeadsetActive; float t0 = Time.time, startDist = Flat(target - Feet()).magnitude, bestDist = 1e9f, lastGain = Time.time, holdUntil = 0f, fiveT = 0f; int tries = 0, lifts = 0; Vector3 hold = Vector3.zero, lastFeet = Feet(); RaycastHit hit;
        smTrips++; int pi = 1; float pathAt = 0f, stuckT = Time.time; bool pathOk = false, pathComplete = false, usedPath = false, usedDoor = false; Vector3 stuckAt = Feet(); float directUntil = 0f;
        while (autoOn && Playing)
        {
            if (abort != null && abort()) break;
            Vector3 f = Feet(), d = target - f; float dy = d.y; d.y = 0f; float dist = d.magnitude; bool level = Mathf.Abs(dy) < 2.2f, drop = dy < 0f && dy > -3.4f;      // a step or a ledge down is walked off, not a lift ride
            smWalked += Flat(f - lastFeet).magnitude < 3f ? Flat(f - lastFeet).magnitude : 0f; lastFeet = f;
            if (level && (dist < arrive || (zone >= 0 && game.NearZone == zone && dist < 2.2f))) break;
            // the real way there, round the walls and up the stairs (HunchPath); straight lines are only the fallback
            Vector3 aim = target; bool onPath = false;
            if (HunchPath.I && HunchPath.I.Ready && Time.time >= directUntil)
            {
                if (Time.time >= pathAt) { pathAt = Time.time + 0.7f; float plen; pathOk = HunchPath.I.Find(f, target, smPath, out plen, out pathComplete); pi = 1; }
                if (pathOk && !pathComplete && HunchCheats.Walls) { directUntil = Time.time + 3f; usedDoor = true; }
                else if (pathOk && !pathComplete) { nav.debugIntent = Vector3.zero; yield return new WaitForSeconds(0.4f); SmoothHop(target, zone, "no doorway on the model: transfer"); smTransfers++; smHops--; break; }      // walls are solid (the cheat is off) and the model has no way through: a short transfer, as a player would use Travel      // no mapped way all the way there (the model has no opening between these two places): go straight, and the auto-door lets you through, as it does for a player
                else if (pathOk && smPath.Count >= 2)
                {
                    while (pi < smPath.Count - 1 && Flat(smPath[pi] - f).sqrMagnitude < 0.45f * 0.45f && Mathf.Abs(smPath[pi].y - f.y) < 1.2f) pi++;
                    Vector3 c = smPath[Mathf.Min(pi, smPath.Count - 1)]; bool last = pi >= smPath.Count - 1;
                    { Airlock lk; bool fromA; if (airBuilt && AirAt(f, c, out lk, out fromA)) { nav.debugIntent = Vector3.zero; yield return StartCoroutine(AirGlide(lk, fromA)); pathAt = 0f; stuckAt = Feet(); stuckT = Time.time; lastFeet = Feet(); usedPath = true; continue; } }      // the next stretch is an airlock door or a lift: ride it
                    if (!(last && Flat(c - f).sqrMagnitude < 0.6f * 0.6f)) { aim = c; onPath = true; usedPath = true; }
                    else if (!pathComplete && !HunchCheats.Walls) { nav.debugIntent = Vector3.zero; yield return new WaitForSeconds(0.25f); SmoothHop(target, zone, "no doorway on the model here: transfer"); smTransfers++; smHops--; break; }      // the walk-through-walls cheat is off: walk as far as the mapped way goes, then transfer
                    else if (!pathComplete) { directUntil = Time.time + 4.5f; usedDoor = true; bestDist = 1e9f; lastGain = Time.time; tries = 0; }      // the mapped way stops short here: for a few seconds the door or the lift takes over, then the map is asked again
                }
            }
            nav.noAutoPass = onPath;
            if (onPath) { d = aim - f; d.y = 0f; }
            if (Time.time - t0 > 180f) { SmoothHop(target, zone, "took too long"); break; }
            // floors: go down first, go up once you are underneath
            if (!onPath && !level && ((dy < 0f && !drop) || dist < 1.6f))
            {
                nav.debugIntent = Vector3.zero; yield return new WaitForSeconds(0.3f);
                if (lifts >= 6 || !nav.Lift(dy > 0f ? 1 : -1)) { SmoothHop(target, zone, lifts >= 6 ? "too many lift rides" : (dy > 0f ? "no floor above" : "no floor below")); break; }
                lifts++;
                yield return new WaitForSeconds(0.35f); lastFeet = Feet(); bestDist = 1e9f; lastGain = Time.time; continue;
            }
            Vector3 want = d.sqrMagnitude > 0.0001f ? d.normalized : FlatForward(), go = want;
            if (onPath) { }
            else if (Time.time < holdUntil) go = hold;
            else
            {
                // something in the way? slip round it on whichever side is open
                Vector3 chest = f + Vector3.up * 0.9f;
                if (dist > 1.2f && Physics.SphereCast(chest, 0.2f, want, out hit, Mathf.Min(nav.debugSpeed > 6f ? 2.2f : 1.3f, dist), ~0, QueryTriggerInteraction.Ignore))
                {
                    float bestDot = -2f; Vector3 pick = Vector3.zero;
                    foreach (float ang in new[] { 35f, -35f, 65f, -65f })
                    {
                        Vector3 dir = Quaternion.Euler(0f, ang, 0f) * want;
                        if (Physics.SphereCast(chest, 0.2f, dir, out hit, 1.7f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        float dot = Vector3.Dot(dir, want); if (dot > bestDot) { bestDot = dot; pick = dir; }
                    }
                    if (pick != Vector3.zero) { go = hold = pick; holdUntil = Time.time + 0.45f; }
                }
            }
            nav.debugSpeed = showOn ? (sim.alerts.Count > 0 && dist > 2.5f ? 7.5f : 4.2f) : ((run || dist > 12f) && dist > 2.5f ? SmRun : SmWalk); nav.debugIntent = go;      // The Show strolls, and hurries only for an alert
            if (!vr && desk) desk.SetLook(Mathf.LerpAngle(cam.eulerAngles.y, Mathf.Atan2(go.x, go.z) * Mathf.Rad2Deg, 1f - Mathf.Exp(-5f * Time.deltaTime)), 9f);
            // getting nowhere: push through the door, and only after several tries give up and hop
            if ((f - stuckAt).sqrMagnitude > 0.5f * 0.5f) { stuckAt = f; stuckT = Time.time; if (onPath) tries = 0; }
            if (onPath)
            {
                bestDist = 1e9f; lastGain = Time.time;
                if (Time.time - stuckT > 1.8f) { Airlock nk; bool nA; if (airBuilt && AirNearby(f, target, out nk, out nA)) { nav.debugIntent = Vector3.zero; yield return StartCoroutine(AirGlide(nk, nA)); pathAt = 0f; stuckAt = Feet(); stuckT = Time.time; lastFeet = Feet(); continue; } tries++; stuckT = Time.time - 0.9f;
                    // caught on an edge: step smoothly along the mapped way to the next corner (a few metres at most), never a jump across the base
                    if (pathOk && pi < smPath.Count && tries <= 4) { Vector3 nc = smPath[pi]; if (Flat(nc - f).magnitude < 4.5f && Mathf.Abs(nc.y - f.y) < 1.3f) { smNudges++; yield return StartCoroutine(GlideTo(nc, 2.4f)); pathAt = 0f; stuckAt = Feet(); stuckT = Time.time; lastFeet = Feet(); continue; } }
                    if (tries > (HunchCheats.Walls ? 5 : 4)) { SmoothHop(target, zone, "blocked on the path"); break; } nav.TryPass(want); usedDoor = true; }
            }
            else if (dist < bestDist - 0.3f) { bestDist = dist; lastGain = Time.time; tries = 0; }
            else if (Time.time - lastGain > 1.8f)
            {
                tries++; lastGain = Time.time - 0.9f;
                if (tries > 5) { SmoothHop(target, zone, "blocked"); break; }
                nav.TryPass(want); bestDist = 1e9f;
            }
            // a high five for any crew member you pass
            fiveT -= Time.deltaTime;
            if (fiveT <= 0f && kid && game.crewAvatars != null && sim.phase == HabPhase.Mission)
            {
                fiveT = 0.5f;
                for (int k = 0; k < game.crewAvatars.Length && k < sim.crew.Count; k++) if (game.crewAvatars[k] && (game.crewAvatars[k].position - f).sqrMagnitude < 4.5f * 4.5f) kid.HighFive(k);
            }
            yield return null;
        }
        if (nav) { nav.debugIntent = Vector3.zero; nav.debugSpeed = 3f; nav.noAutoPass = false; }
        if (usedPath) smPathTrips++; if (usedDoor) smDoorTrips++;
        float took = Time.time - t0; if (startDist > 10f && took > 0.5f) smSpeed = Mathf.Lerp(smSpeed, Mathf.Clamp(startDist / took, 1.5f, SmRun), 0.3f);
    }

    IEnumerator SmoothToZone(int zi, bool run, Func<bool> abort)
    {
        if (zi < 0 || zi >= game.zones.Count) yield break;
        yield return SmoothTo(game.zones[zi].position, 1.1f, run, abort, zi);
        if (!autoOn || (abort != null && abort())) yield break;
        yield return null; yield return null;
        if (game.NearZone != zi) { SmoothHop(game.zones[zi].position, zi, "not in the ring"); yield return null; }
    }

    // ------------------------------------------------------------------ the AI: look-ahead on a copy of the habitat
    float SmoothTravel(Vector3 from, Vector3 to)
    {
        float floors = Mathf.Round(Mathf.Abs(to.y - from.y) / 5.5f);
        return Flat(to - from).magnitude / Mathf.Max(1.5f, smSpeed) + floors * 1.6f + 1.2f;
    }
    static void SmoothAdvance(HabitatSim s, float seconds) { while (seconds > 0.01f) { float dt = Mathf.Min(1f, seconds); s.Tick(dt); seconds -= dt; } }

    /// is doing the job in this zone worth considering at all? (never switches the reactor off, never wastes a spare)
    static bool SmoothUseful(HabitatSim s, string id)
    {
        foreach (var a in s.alerts) if (a.zoneId == id) return !(a.needsPart && s.parts <= 0);
        if (s.CooldownAt(id) > 0f) return false;
        switch (id)
        {
            case "power": return !s.fissionOn;
            case "storage": return s.parts < 2 && s.storageStock > 0;
            case "fab": return s.parts < 2;
            case "control": return s.commsOk;
            case "galley": return s.food >= 3f;
        }
        return true;
    }

    /// how good a future is: the rubric's own score, plus what the state at the end is still worth for the rest of the mission
    static float SmoothEval(HabitatSim s)
    {
        float e = s.Overall(), left = Mathf.Clamp01(1f - s.missionSeconds / (HabitatSim.SecondsPerDay * HabitatSim.MissionDays));
        float wb = 0f, hl = 0f; foreach (var c in s.crew) { wb += c.Wellbeing; hl += c.health; }
        wb /= s.crew.Count; hl /= s.crew.Count;
        float pot = 0.6f * wb + 0.55f * hl + Mathf.Min(s.oxygen, s.water) - Mathf.Max(0f, s.dust - 12f) * 1.3f + 0.45f * s.battery;
        e += 0.8f * left * pot / 7f;
        e += s.parts >= 1 ? 0.8f + (s.parts >= 2 ? 0.1f : 0f) : 0f;                 // one spare in the pocket saves a long detour when something breaks; a second is only worth a short walk
        if (!s.fissionOn && s.missionSeconds > HabitatSim.SecondsPerDay * 7f) e -= 1.2f;      // the lunar night is coming: have the reactor running before it starts
        foreach (var a in s.alerts) e -= a.overdue ? 1.0f : 1.6f * (1f - Mathf.Clamp01(a.secondsLeft / Mathf.Max(1f, a.total)));
        return e;
    }

    /// chooses the next job. Spread over a few frames so the game never stutters.
    IEnumerator SmoothPlan()
    {
        float clock = Time.realtimeSinceStartup; smPlans++; smPlanZone = -1; smPlanWhy = ""; smPlanGain = 0f;
        Vector3 here = Feet(); int n = game.zones.Count;
        if (smHub == Vector3.zero) { int hz = game.ZoneIndex("medical"); smHub = hz >= 0 ? game.zones[hz].position : here; }
        var idle = sim.Clone(); SmoothAdvance(idle, SmHorizon); float baseE = SmoothEval(idle) - 0.004f * Flat(here - smHub).magnitude, bestE = baseE + 0.015f; int best = -1;
        smOpt.Clear(); for (int a = 0; a < n; a++) if (SmoothUseful(sim, game.zones[a].id)) smOpt.Add(a);
        foreach (int a in smOpt)
        {
            var za = game.zones[a]; float ta = SmoothTravel(here, za.position);
            var s1 = sim.Clone(); SmoothAdvance(s1, Mathf.Min(ta, SmHorizon)); if (ta < SmHorizon) s1.Interact(za.id);
            float left1 = SmHorizon - ta, eA;
            { var r = s1.Clone(); SmoothAdvance(r, Mathf.Max(0f, left1)); eA = SmoothEval(r) - 0.004f * Flat(za.position - smHub).magnitude; }      // ending far from where most jobs come up costs a little
            if (left1 > 4f)
            {
                smOpt2.Clear(); for (int b = 0; b < n; b++) if (SmoothUseful(s1, game.zones[b].id)) smOpt2.Add(b);
                foreach (int b in smOpt2)
                {
                    var zb = game.zones[b]; float tb = SmoothTravel(za.position, zb.position); if (tb >= left1) continue;
                    var s2 = s1.Clone(); SmoothAdvance(s2, tb); s2.Interact(zb.id); SmoothAdvance(s2, left1 - tb);
                    float e2 = SmoothEval(s2) - 0.004f * Flat(zb.position - smHub).magnitude; if (e2 > eA) eA = e2;
                }
            }
            if (eA > bestE) { bestE = eA; best = a; }
            if (Time.realtimeSinceStartup - clock > 0.004f) { smPlanMs += (Time.realtimeSinceStartup - clock) * 1000f; yield return null; clock = Time.realtimeSinceStartup; }
        }
        smPlanMs += (Time.realtimeSinceStartup - clock) * 1000f;
        smPlanZone = best; smPlanGain = best >= 0 ? bestE - baseE : 0f;
        if (best >= 0)
        {
            var z = game.zones[best]; HabAlert al = null; foreach (var a in sim.alerts) if (a.zoneId == z.id) al = a;
            string p = sim.Prompt(z.id);
            smPlanWhy = al != null ? "ALERT at " + z.title + ": " + p + "  (" + Mathf.CeilToInt(al.secondsLeft) + " s left)" : z.title + ": " + p + "  (look-ahead: +" + smPlanGain.ToString("F2") + " score)";
        }
    }

    /// which meeting choice helps the mission most: each one is tried on a copy of the habitat
    int SmoothMeetingChoice()
    {
        int best = 0; float bestE = -1e9f;
        for (int k = 0; k < 3; k++)
        {
            var c = sim.Clone();
            if (k == 0) foreach (var m in c.crew) { m.mood += 8f; m.stress -= 10f; m.Clamp(); }
            else if (k == 1) { c.dust = Mathf.Max(0f, c.dust - 15f); c.arrays = Mathf.Min(100f, c.arrays + 10f); c.battery = Mathf.Min(100f, c.battery + 10f); }
            else { c.food = Mathf.Min(100f, c.food + 12f); c.water = Mathf.Min(100f, c.water + 12f); }
            SmoothAdvance(c, 30f); float e = SmoothEval(c); if (e > bestE) { bestE = e; best = k; }
        }
        return best;
    }

    /// seconds to spare on the most pressing alert, after the walk there
    float SmoothSlack()
    {
        float s = 999f; Vector3 here = Feet();
        foreach (var a in sim.alerts) { if (a.overdue) continue; int zi = game.ZoneIndex(a.zoneId); if (zi < 0) continue; s = Mathf.Min(s, a.secondsLeft - SmoothTravel(here, game.zones[zi].position)); }
        return s;
    }

    IEnumerator AutoSmoothRun()
    {
        bool vr = game.HeadsetActive; int tour = 0; bool toldCreative = false;
        yield return new WaitForSeconds(1.0f);
        AutoSay("Autopilot Smooth. I walk and run everywhere, and I think one minute ahead before every job. Press " + (vr ? "A" : "Space") + " to take over.");
        if (!vr && !thirdPerson) SetThird(true);
        yield return new WaitForSeconds(1.6f);
        while (autoOn)
        {
            if (mapNow != 0) { GoMap(0); yield return new WaitForSeconds(1.0f); }
            if (readOpen >= 0) CloseRead();
            if (!vr && !thirdPerson && Time.time > danceUntil) SetThird(true);
            if (!Creative && hunger < 55f && food > 0) { AutoSay("Eating a food pack."); Eat(); yield return new WaitForSeconds(0.8f); }

            // ---- the commissioning walk: eight zones in NASA adjacency order
            if (sim.phase == HabPhase.Commissioning)
            {
                int zi = game.ZoneIndex(HabitatSim.AdjacencyOrder[Mathf.Min(sim.tutorialIndex, HabitatSim.AdjacencyOrder.Length - 1)]);
                if (zi < 0) { yield return null; continue; }
                AutoSay("Commissioning " + game.zones[zi].title + "  (" + (sim.tutorialIndex + 1) + " of 8, in NASA adjacency order)."); ShowLine(game.zones[zi].id, null);
                yield return SmoothToZone(zi, false, null); if (!autoOn) yield break;
                game.DoInteract(); smJobs++; ShowPop(); yield return new WaitForSeconds(showOn ? 1.6f : 0.9f); continue;
            }
            // ---- free explore has no clock and no score: just show the walking
            if (Creative)
            {
                if (!toldCreative) { toldCreative = true; AutoSay("Free explore has no score to win. I will walk a round of the zones. For the real mission choose Autopilot Smooth on the main menu (Menu > Restart)."); yield return new WaitForSeconds(3.5f); }
                int zi = (tour++ * 3) % game.zones.Count; AutoSay("Walking to " + game.zones[zi].title + ".");
                yield return SmoothToZone(zi, tour % 2 == 0, null); if (!autoOn) yield break;
                game.DoInteract(); yield return new WaitForSeconds(1.6f); continue;
            }

            float slack = SmoothSlack();
            // ---- a team meeting has been called
            if ((mtState == 1 || mtState == 2) && slack > 25f)
            {
                if (mtState == 1) { AutoSay("Team meeting called. Walking to the meeting."); yield return SmoothTo(mtSpot, 2.4f, true, () => mtState != 1, mtZone); if (!autoOn) yield break; if (mtState == 1) StartMeeting(); yield return new WaitForSeconds(1.2f); }
                if (mtState == 2) { int v = SmoothMeetingChoice(); AutoSay("The crew have reported. Voting for: " + MtOption[v] + "."); yield return new WaitForSeconds(1.2f); MeetingVote(v); yield return new WaitForSeconds(1.5f); }
                continue;
            }
            // ---- a panel that really is broken
            Station broken = null; foreach (var st in stations) if (st.broken && !st.practice) { broken = st; break; }
            if (broken != null && slack > 30f) { yield return AutoStation(broken); if (!autoOn) yield break; continue; }
            // ---- the Crew Health System is offering help for someone
            if (hs != null && hsZone >= 0 && slack > 20f)
            {
                int who = -1; for (int i = 0; i < hs.Length; i++) if (hs[i].state == 2 && hs[i].cond >= 0) { who = i; break; }
                if (who >= 0)
                {
                    AutoSay("Crew health: " + sim.crew[who].name + " shows signs of " + HsCond[hs[who].cond].ToLower() + ". Walking to the health wall.");
                    yield return SmoothToZone(hsZone, true, null); if (!autoOn) yield break;
                    if (hsWall) yield return Turn(hsWall.position + Vector3.up * 1.5f, 0.4f);
                    if (hs[who].state == 2 && hs[who].cond >= 0) { HsAccept(who, 0); smJobs++; }
                    yield return new WaitForSeconds(0.8f); continue;
                }
            }
            // ---- think ahead, then do the best job
            yield return SmoothPlan();
            if (!autoOn) yield break;
            if (smPlanZone < 0) { autoLine = "Everything is in hand. Waiting for the next job."; yield return new WaitForSeconds(0.5f); continue; }
            int go = smPlanZone, alertsNow = sim.alerts.Count; AutoSay(smPlanWhy); { HabAlert sa = null; foreach (var a in sim.alerts) if (a.zoneId == game.zones[go].id) sa = a; ShowLine(game.zones[go].id, sa); }
            if (smTrace.Length < 6000) smTrace += "d" + sim.Day + " " + game.zones[go].id + (sim.HasAlertAt(game.zones[go].id) ? "!" : "") + " ";
            yield return SmoothToZone(go, alertsNow > 0 || Flat(game.zones[go].position - Feet()).magnitude > 12f, () => sim.alerts.Count > alertsNow || sim.phase != HabPhase.Mission);
            if (!autoOn) yield break;
            if (sim.alerts.Count > alertsNow) continue;                      // something new came up on the way: think again
            if (game.NearZone == go && SmoothUseful(sim, game.zones[go].id)) { game.DoInteract(); smJobs++; ShowPop(); yield return new WaitForSeconds(showOn ? 1.8f : 0.6f); }
        }
    }

    /// the mission is over: say how it went and save the result
    void SmoothFinish()
    {
        if (sim.phase != HabPhase.Report) return;
        var s = sim.Scores(); float all = sim.Overall(); string grade = HabitatSim.Grade(all);
        var sb = new StringBuilder();
        sb.Append("AUTOPILOT SMOOTH RESULT   ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append('\n');
        sb.Append(sim.missionFailed ? "Mission ended early: " + sim.failReason : "Mission complete: " + HabitatSim.MissionDays + " days").Append('\n');
        sb.Append("Overall score ").Append(all.ToString("F1")).Append(" / 100   ").Append(grade).Append('\n');
        for (int i = 0; i < s.Length; i++) sb.Append("  ").Append(s[i].ToString("F1").PadLeft(5)).Append("  ").Append(HabitatSim.Criteria[i]).Append('\n');
        sb.Append("Alerts handled ").Append(sim.Handled).Append(" of ").Append(sim.Raised).Append('\n');
        sb.Append("Jobs done ").Append(smJobs).Append(", decisions ").Append(smPlans).Append(" (thinking time ").Append((smPlanMs / Mathf.Max(1, smPlans)).ToString("F1")).Append(" ms each)\n");
        sb.Append("Walked and ran ").Append(Mathf.RoundToInt(smWalked)).Append(" m in ").Append(smTrips).Append(" trips (").Append(smPathTrips).Append(" on a mapped path); doors pushed through ").Append(nav ? nav.passes - smPasses0 : 0).Append(", lift rides ").Append(nav ? nav.lifts - smLifts0 : 0).Append(", transfers where the model has no doorway ").Append(smTransfers).Append(", stuck-and-hopped ").Append(smHops).Append('\n');
        if (smHops > 0) sb.Append("Hops: ").Append(smHopLog).Append('\n');
        sb.Append("High fives ").Append(sim.highFives - smFives0).Append('\n');
        smResult = sb.ToString();
        try { string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Reports"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "autopilot_smooth_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"), smResult.Replace("\n", "\r\n")); } catch { }
        if (showOn) ShowFinish(all, grade); else Banner(sim.missionFailed ? "MISSION ENDED" : "MISSION COMPLETE", "Autopilot Smooth scored " + all.ToString("F0") + " of 100  -  " + grade, sim.missionFailed);
    }

    // ------------------------------------------------------------------ test hooks
    public string ProbeSmooth() { StartAutopilotSmooth(); return "smooth=" + autoSmooth + " phase=" + sim.phase; }
    public string ProbeSmoothFast() { Time.timeScale = Mathf.Clamp(probeY, 1f, 8f); return "timeScale " + Time.timeScale; }
    public string ProbeSmoothInfo()
    {
        return "stop=[" + autoStopWhy + "] " + "smooth=" + autoSmooth + " auto=" + autoOn + " phase=" + sim.phase + " day=" + sim.Day + " t=" + sim.missionSeconds.ToString("F0") + " score=" + sim.Overall().ToString("F1") + " alerts=" + sim.alerts.Count + " handled=" + sim.Handled + "/" + sim.Raised
            + " wb=" + sim.AvgWellbeing.ToString("F0") + " o2=" + sim.oxygen.ToString("F0") + " water=" + sim.water.ToString("F0") + " dust=" + sim.dust.ToString("F0") + " bat=" + sim.battery.ToString("F0") + " fission=" + sim.fissionOn + " parts=" + sim.parts
            + " | jobs=" + smJobs + " plans=" + smPlans + " planMs=" + (smPlanMs / Mathf.Max(1, smPlans)).ToString("F1") + " walked=" + Mathf.RoundToInt(smWalked) + " trips=" + smTrips + " speed=" + smSpeed.ToString("F1") + " pathTrips=" + smPathTrips + " path=" + (HunchPath.I ? HunchPath.I.ProbeInfo() : "none") + " passes=" + (nav ? nav.passes - smPasses0 : 0) + " lifts=" + (nav ? nav.lifts - smLifts0 : 0) + " hops=" + smHops + " nudges=" + smNudges + " fives=" + (sim.highFives - smFives0)
            + " hunger=" + Mathf.RoundToInt(hunger) + " hp=" + Mathf.RoundToInt(health) + " respawns=" + respawns + " feet=" + Feet().ToString("F0") + " near=" + game.NearZone + " | " + autoLine;
    }
    public string ProbeSmoothResult() { var s = sim.Scores(); var sb = new StringBuilder(); foreach (var v in s) sb.Append(v.ToString("F0")).Append(' '); return "phase=" + sim.phase + " failed=" + sim.missionFailed + " overall=" + sim.Overall().ToString("F1") + " scores=" + sb + "| " + smResult.Replace("\n", " / ") + " | hops " + smHopLog + " | trace " + smTrace; }
}
