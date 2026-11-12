// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/D_ARES_Layer/HunchFrontier26.cs
//  Section : 06_Frontier/D_ARES_Layer
//  Folder  : Project ARES rubric layer: floors fix + ARES pages, easy-mode missions and check-ins,
//            ARES zone lights and sensor clusters in the world.
//  Purpose : EASY MODE for Lunar Habitat Commander — ARES missions, in-person health check-ins and
//            bedtime rules. - MISSIONS: six short story missions about the crew's mental health,
//            each tied to a part of the Project ARES rubric.
//  Why     : Chat: make it easy — missions with one next step (G).
//  Contains: HunchFrontier, AStep, AMission
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
#pragma warning disable 0414, 0219
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using AR = HunchAres;

/// EASY MODE for Lunar Habitat Commander — ARES missions, in-person health check-ins and bedtime rules.
///  - MISSIONS: six short story missions about the crew's mental health, each tied to a part of the Project ARES rubric.
///    The objective bar at the top always shows the ONE next step. G ("go where you are needed") does that step with you
///    whenever no alarm is waiting (an alarm always comes first). Menu > Project ARES > ARES missions lists them all.
///  - HEALTH CHECK-IN: you walk over to a crewmate, ask, and read their wristband, chest beacon, skin patch and the room's
///    sensors — then the six mental-health levels the AI computes from them, and the support that would help most.
///  - BEDTIME RULES (Human > AI): while you sleep the AI may only dim lights and watch vital signs. If it breaks a rule it
///    wakes you and F12 switches it off. If no human answers within 30 s the hardware interlock switches it off by itself.
public partial class HunchFrontier
{
    class AStep { public string text, how; public Func<bool> done; public Action act; }
    class AMission { public string name, rubric, story; public Action setup; public List<AStep> steps = new List<AStep>(); }
    readonly List<AMission> amissions = new List<AMission>();
    int amIndex, amStep; bool amStarted; float amStartH, amTick; readonly HashSet<string> amFlags = new HashSet<string>();
    readonly Dictionary<int, float> checkAt = new Dictionary<int, float>();
    bool nightTest, wasSleeping; float sleptSince = -1f;
    HunchMenu.ExtraPage pageAresMissions, pageAresCheck;
    Light overstepLight;

    AMission AmCur => amIndex >= 0 && amIndex < amissions.Count ? amissions[amIndex] : null;
    string CrewName(int id) => id >= 1 && id <= sim.crew.Count ? sim.crew[id - 1].name : "crewmate";
    string Last(int id) => CrewName(id).Split(' ').Last();
    bool Checked(int id) { float h; return checkAt.TryGetValue(id, out h) && h >= amStartH; }
    bool LogSince(string kind, Func<AR.LogEntry, bool> ok = null) => ares != null && ares.log.Any(l => l.simHour >= amStartH && l.kind == kind && (ok == null || ok(l)));
    AStep St(string text, string how, Func<bool> done, Action act) => new AStep { text = text, how = how, done = done, act = act };
    AStep CheckStep(int id) => St("Check on " + CrewName(id), "Walk over to " + Last(id) + " — ARIA reads their wristband, skin patch and the room's sensors after they say yes.", () => Checked(id), () => CheckOn(id));
    void GoZone(params string[] keys) { int z = ZoneIndex(keys); if (z >= 0) game.GoTo(z); }
    bool NearZone(params string[] keys) { int z = ZoneIndex(keys); return z >= 0 && game.NearZone == z; }

    // ------------------------------------------------------------------ build (called from BuildAres)
    void BuildAresMissions()
    {
        amissions.Clear();
        var m = new AMission { name = "First shift: meet your crew", rubric = "D1 · how the health data is collected",
            story = "Before anything else, check on each crewmate in person. ARIA reads their wristband, skin patch and the room's sensors — after they say yes." };
        for (int id = 2; id <= Mathf.Min(4, sim.crew.Count); id++) m.steps.Add(CheckStep(id));
        m.steps.Add(St("See the same data on the AI dashboard", "Menu > Project ARES > AI dashboard. ARIA turns 9 sensors into 6 mental-health scores.", () => amFlags.Contains("dash"), () => { menu.OpenExtra(pageAresDash); }));
        amissions.Add(m);

        m = new AMission { name = Last(3) + "'s rough night", rubric = "D2 · Cognitive fatigue → support",
            story = Last(3) + " slept badly and the air is stuffy. High CO2 makes everyone tired and foggy.",
            setup = () => { var c = sim.crew[2]; c.sleep = 38f; c.stress = Mathf.Max(c.stress, 55f); var a = ares.C(3); a.fatigue = 0.82f; a.sleepQ = 0.5f; ares.Log(3, "story", Last(3) + " slept badly; CO2 rising."); } };
        m.steps.Add(CheckStep(3));
        m.steps.Add(St("Send " + Last(3) + " to rest", "Rest helps fatigue. G asks " + Last(3) + " — they agree, and walk to their pod.", () => amFlags.Contains("rest"),
            () => { var c = sim.crew[2]; c.sleep = Mathf.Min(100f, c.sleep + 25f); c.stress -= 15f; c.Clamp(); var a = ares.C(3); a.fatigue = Mathf.Max(0f, a.fatigue - 0.3f); ares.Log(3, "job", Last(3) + " agreed to rest (Commander asked; they could have said no)."); amFlags.Add("rest"); sim.Say("ARIA: " + Last(3) + " is taking a rest. Rest is a job too."); }));
        m.steps.Add(St("Clean the CO2 scrubber yourself", "Go to Life Support (G takes you), then G again to clean the scrubber.", () => amFlags.Contains("scrub"),
            () => { if (!NearZone("eclss", "life", "oxygen")) { GoZone("eclss", "life", "oxygen"); sim.Say("ARIA: Life Support. Press G again to clean the CO2 scrubber."); return; } amFlags.Add("scrub"); foreach (var c in sim.crew) { c.stress -= 4f; c.Clamp(); } ares.Log(1, "eclss", "Commander cleaned the CO2 scrubber — CO2 back under 600 ppm."); Banner("SCRUBBER CLEANED", "CO2 is dropping in every zone — the crew will feel sharper", false); }));
        amissions.Add(m);

        m = new AMission { name = "Words at dinner", rubric = "D2/D3 · Interpersonal conflict",
            story = Last(4) + " and " + Last(3) + " argued about the lab schedule. ARIA sees less time together and tense voices — but it never takes sides.",
            setup = () => { ares.C(3).tension[4] = 0.7f; ares.C(4).tension[3] = 0.7f; sim.crew[3].stress = Mathf.Max(sim.crew[3].stress, 50f); ares.Log(0, "story", "Two crewmates disagreed about the lab schedule."); } };
        m.steps.Add(CheckStep(4));
        m.steps.Add(St("Deploy Configuration B (conflict protocol)", "Menu > Project ARES > Configuration (G does it). Acoustic screens, a second dining spot and a lit alternate route: nobody is forced together, nobody is named.", () => AR.AresHost.ConfigB, () => SetConfigB(true)));
        m.steps.Add(St("Hold a team meal in the galley", "Go to the galley (G takes you). Eating together rebuilds trust.", () => amFlags.Contains("meal"),
            () => { GoZone("galley"); foreach (var c in ares.crew) { c.autonomy += 0.1f; for (int i = 0; i < c.tension.Length; i++) c.tension[i] *= 0.6f; } foreach (var c in sim.crew) { c.mood += 6f; c.stress -= 6f; c.Clamp(); } ares.Log(0, "meeting", "Team meal in the galley — everyone chose their own seat and task order."); amFlags.Add("meal"); sim.Say("ARIA: Team meal in the galley. Nobody was singled out."); }));
        amissions.Add(m);

        m = new AMission { name = Last(4) + " feels low", rubric = "D2 · Depression & monotony · consent",
            story = Last(4) + " has been quiet for days — flat voice, more time alone. The grey walls don't help.",
            setup = () => { sim.crew[3].mood = 35f; var a = ares.C(4); a.mood = 0.35f; a.novelty = 0.3f; ares.Log(4, "story", Last(4) + " has been withdrawn for several days."); } };
        m.steps.Add(CheckStep(4));
        m.steps.Add(St("Help " + Last(4) + ": video call home", "Family contact is the strongest mood lift on a long mission. G sets it up — " + Last(4) + " says yes.", () => amFlags.Contains("call"),
            () => { var c = sim.crew[3]; c.mood += 15f; c.Clamp(); var a = ares.C(4); a.mood += 0.12f; a.lastSocialHour = AR.SimClock.hours; ares.Log(4, "job", Last(4) + " chose a video call home."); amFlags.Add("call"); sim.Say("ARIA: " + Last(4) + " is calling home. Nobody else sees how they feel unless they share it."); }));
        m.steps.Add(St("Your turn: ARIA offers YOU support", "Answer yes or no in Menu > Project ARES > Crew interface (G says yes). The AI never decides for you.", () => aresShown.Contains("accept") || aresShown.Contains("refuse") || LogSince("accept", l => l.crew == 1) || LogSince("refuse", l => l.crew == 1), OfferMe));
        amissions.Add(m);

        m = new AMission { name = "Bedtime: humans stay in charge", rubric = "D4 · Human override & hard limits",
            story = "At night ARIA may only dim lights and watch vital signs. If anything goes wrong while you sleep, the AI stops and wakes you — the human takes over.",
            setup = () => { if (AR.AresHost.AiOff) AresAiOff(false); nightTest = true; } };
        m.steps.Add(St("Go to bed", "Go to your sleep pod in Crew Quarters and lie down (G takes you and lies you down).", () => amFlags.Contains("bed"),
            () => { if (sleepMe) { Note("You're asleep… time runs fast until morning."); return; } if (sleepZone >= 0 && game.NearZone != sleepZone) { game.GoTo(sleepZone); sim.Say("ARIA: Crew Quarters — your sleep pod is here. Press G again to lie down."); return; } SleepMe(); }));
        m.steps.Add(St("Sleep — if something goes wrong, ARIA stops and you take over", "Tonight ARIA will break a bedtime rule. Watch: it STOPS itself, undoes the change and wakes you. You are in charge.", () => amFlags.Contains("overstep") && AR.AresHost.AiOff,
            () => { if (!amFlags.Contains("overstep")) { if (!sleepMe) { nightTest = true; if (sleepZone >= 0 && game.NearZone != sleepZone) { game.GoTo(sleepZone); sim.Say("ARIA: Back to your pod. Press G again to lie down."); } else SleepMe(); } else Note("Still asleep — wait a moment."); return; } AresAiOff(true); }));
        m.steps.Add(St("You decide: allow ARIA again", "Everything ARIA did is in its log (Crew interface > What the AI did for me). Press F12 again when you're ready.", () => amFlags.Contains("overstep") && !AR.AresHost.AiOff,
            () => { if (AR.AresHost.AiOff) AresAiOff(false); }));
        amissions.Add(m);

        m = new AMission { name = "Report to the review board", rubric = "D4 · Board questions Q1–Q6 · tabletop",
            story = "Mission control wants proof the system is safe. Run a board question, the 72-hour test, and export your own data." };
        m.steps.Add(St("Answer all 6 board questions (Q1–Q6)", "Menu > Project ARES > Board questions. G runs the next one live and shows how the design answers it.", () => Enumerable.Range(1, 6).All(q => aresShown.Contains("q" + q)),
            () => { int q = Enumerable.Range(1, 6).First(i => !aresShown.Contains("q" + i)); AR.Scenarios.Run(q); aresShown.Add("q" + q); AresSheet("Board question Q" + q + "  (" + Enumerable.Range(1, 6).Count(i => aresShown.Contains("q" + i)) + "/6)", AR.Scenarios.lastExplain); }));
        m.steps.Add(St("Run the 72-hour tabletop test", "Menu > Project ARES > Bonus A. It saves a full report to the Reports folder.", () => aresShown.Contains("tabletop"), () => { aresShown.Add("tabletop"); AR.TabletopSim.Run(); AresSheet("72-hour tabletop simulation", AR.TabletopSim.lastReport); }));
        m.steps.Add(St("Export your own data log", "Crew interface > Export my log. Your data belongs to you.", () => amFlags.Contains("export"), () => { amFlags.Add("export"); AresSheet("Exported", "Saved: " + AresExportLog()); }));
        amissions.Add(m);

        amIndex = 0; amStep = 0; amStarted = false;
        HabitatGame.ObjectiveExtra = AresObjective;
        HabitatGame.UrgentExtra = () => { if (sim.phase != HabPhase.Mission || AmCur == null) return false; AresDoStep(); return true; };
        BuildAresMissionMenu();
    }

    string AresObjective()
    {
        if (sim.phase != HabPhase.Mission) return null;
        var m = AmCur; if (m == null || !amStarted || amStep >= m.steps.Count) return null;
        string key = game.HeadsetActive ? "menu: Do my next step" : "press G";
        return "Mission " + (amIndex + 1) + "/" + amissions.Count + ": <b>► " + m.steps[amStep].text + "</b>  <color=#7CFC9A>(" + key + ")</color>";
    }

    /// the task card on the desktop screen (plain IMGUI rich text: sizes in pixels, no %)
    string AresTaskCard()
    {
        if (sim == null || sim.phase != HabPhase.Mission) return null;
        var m = AmCur; if (m == null || !amStarted || amStep >= m.steps.Count) return null;
        var st = m.steps[amStep];
        return "<color=#FFD54F><b>MISSION " + (amIndex + 1) + "/" + amissions.Count + " · " + m.name.ToUpperInvariant() + "</b></color>   <color=#9FB3C8>step " + (amStep + 1) + "/" + m.steps.Count + "</color>\n"
             + "<b>► " + st.text + "</b>\n<color=#C9D6E8>" + st.how + "</color>\n<color=#7CFC9A><b>Press G — I'll do it with you</b></color>   <color=#9FB3C8>(" + m.rubric + ")</color>";
    }

    void AresStartMission()
    {
        var m = AmCur; if (m == null || ares == null) return;
        amStarted = true; amStep = 0; amFlags.Clear(); amStartH = AR.SimClock.hours - 0.001f;
        try { m.setup?.Invoke(); } catch (Exception e) { HLog.W("ARES", "mission setup: " + e.Message); }
        ares.Log(0, "mission", "Mission " + (amIndex + 1) + " started: " + m.name);
        Banner("MISSION " + (amIndex + 1), m.name, false);
        sim.Say("ARIA: Mission " + (amIndex + 1) + " — " + m.name + ". " + m.story + " First: " + m.steps[0].text + ". (G = I'll do it with you.)");
    }

    void AresDoStep()
    {
        var m = AmCur; if (m == null) return;
        if (!amStarted) { AresStartMission(); return; }
        if (amStep < m.steps.Count) try { m.steps[amStep].act?.Invoke(); } catch (Exception e) { Note("Couldn't do that step automatically: " + e.Message); }
    }

    /// every frame from UpdateAres
    void UpdateAresMissions(float dt)
    {
        BedtimeRules();
        amTick += dt; if (amTick < 0.5f) return; amTick = 0f;
        if (aresShown.Contains("dash")) amFlags.Add("dash");
        var m = AmCur; if (m == null || sim.phase != HabPhase.Mission) return;
        if (!amStarted) { AresStartMission(); return; }
        while (amStep < m.steps.Count) { bool ok = false; try { ok = m.steps[amStep].done(); } catch { } if (!ok) break; amStep++; if (amStep < m.steps.Count) { Banner("NEXT TASK", m.steps[amStep].text, false); sim.Say("ARIA: Done. Next: " + m.steps[amStep].text + ". " + m.steps[amStep].how); } }
        if (amStep >= m.steps.Count)
        {
            ares.Log(0, "mission", "Mission " + (amIndex + 1) + " complete: " + m.name);
            Banner("MISSION COMPLETE ✓", m.name, false);
            amIndex++; amStep = 0; amStarted = false; aresShown.Add("mission" + amIndex);
            if (AmCur == null) sim.Say("ARIA: Every ARES mission is done, Commander. The rubric self-check is on Menu > Project ARES.");
        }
    }

    // ------------------------------------------------------------------ health check-in (in person)
    public void CheckOn(int id)
    {
        if (ares == null || id < 2 || id > sim.crew.Count) return;
        int k = id - 1; var g = sim.crew[k]; var c = ares.C(id); string first = Last(id);
        Transform av = game.crewAvatars != null && k < game.crewAvatars.Length ? game.crewAvatars[k] : null;
        if (av && av.gameObject.activeInHierarchy && (av.position - Feet()).sqrMagnitude > 3f * 3f)
        {   // a check-in is done in person: walk over (teleport beside them, facing them)
            Vector3 dir = Feet() - av.position; dir.y = 0f; if (dir.sqrMagnitude < 0.01f) dir = av.forward; dir.Normalize();
            Vector3 at = av.position + dir * 1.4f, look = -dir;
            for (int t = 0; t < 8; t++)
            {   // first free spot around them (never inside a wall), starting on your side
                var d = Quaternion.Euler(0f, 45f * t, 0f) * dir; var p = av.position + d * 1.4f;
                if (!Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.6f, 0.28f, ~0, QueryTriggerInteraction.Ignore)) { at = p; look = -d; break; }
            }
            TeleportTo(at, Quaternion.LookRotation(look).eulerAngles.y);
        }
        bool asleep = false; try { asleep = CrewAsleep(k); } catch { }
        var sb = new StringBuilder();
        sb.Append("<b>").Append(g.name).Append(" — ").Append(AR.SimClock.Now).Append("</b>\n");
        sb.Append(asleep ? first + " is asleep — only the night wristband (sleep) is read. Nobody is woken.\n\n" : "You: \"Quick check-in, ok?\"   " + first + ": \"Sure.\"   (consent given — they can say no any time)\n\n");
        string R(int s, string val) { if (asleep && s != 2) return null; if (c.optOut[s] && AR.SensorSpec.OptOutAllowed[s]) return AR.SensorSpec.Name[s] + ": OFF (opted out)"; if (c.sensors[s] == AR.SensorHealth.Failed) return AR.SensorSpec.Name[s] + ": NO DATA (sensor failed)"; return AR.SensorSpec.Name[s] + ": " + val + (c.sensors[s] == AR.SensorHealth.Degraded ? " (noisy)" : "") + "   <size=80%><color=#2F5F96>← " + AR.SensorSpec.Where[s] + "</color></size>"; }
        var rows = new[] {
            R(0, g.HeartRate + " bpm · HRV " + Mathf.RoundToInt(58 - 0.34f * g.stress) + " ms"), R(1, (1 + g.stress / 25f).ToString("0.0") + " peaks/min"), R(2, Mathf.RoundToInt(g.sleep) + "% sleep score"),
            R(3, g.mood < 45f ? "flat" : g.stress > 50f ? "tense" : "normal"), R(4, g.mood < 45f ? "low affect" : "normal"), R(5, asleep ? "in pod" : "moving normally"),
            R(6, (8f + 14f * c.cortisol).ToString("0.0") + " µg/dL"), R(7, (450 + Mathf.RoundToInt(g.stress * 2f)) + " ppm"), R(8, Mathf.RoundToInt(100f - 35f * c.fatigue) + "% accuracy") }.Where(x => x != null).ToList();
        foreach (var r in rows) sb.Append("  ").Append(r).Append("\n");
        if (!asleep)
        {
            sb.Append("\n<b>What the AI computes from it (six conditions):</b>\n");
            string worst = null; float wv = 0f;
            for (int q = 0; q < 6; q++) { float v = c.smooth[q]; string lvl = v >= AR.CondSpec.T3 ? "<color=#B3261E><b>HIGH (Tier 3)</b></color>" : v >= AR.CondSpec.T2 ? "<color=#B85C00><b>watch (Tier 2)</b></color>" : v >= AR.CondSpec.T1 ? "<color=#8A6D00>mild (Tier 1)</color>" : "<color=#1E7A35>ok</color>"; sb.Append("  ").Append(AR.CondSpec.Name[q]).Append(": ").Append(lvl).Append("\n"); if (v > wv) { wv = v; worst = AR.CondSpec.Name[q]; } }
            float[] need = { c.stress, 1f - c.mood, c.tension.Max(), c.fatigue, 1f - c.autonomy, 1f - c.novelty }; int wi = 0; for (int q = 1; q < 6; q++) if (need[q] > need[wi]) wi = q;
            string[] tips = { "a breathing break at the observation window", "a video call home", "a team meal in the galley", "a rest in their pod", "planning their own afternoon", "a VR nature walk / time in the greenhouse" };
            sb.Append("\n<b><color=#1E7A35>").Append(wv < AR.CondSpec.T1 ? first + " looks well. If anything: " + tips[wi] + "." : "Suggest: " + tips[wi] + ".").Append("</color></b>");
            sb.Append("\n<size=80%>Only ").Append(first).Append(" and the CMO see the details; you see them because ").Append(first).Append(" said yes. Raw audio and video never leave the room — only the numbers do.</size>");
        }
        checkAt[id] = AR.SimClock.hours; aresShown.Add("check" + id);
        ares.Log(id, "checkin", "In-person health check-in by the Commander (consented).");
        AresSheet("Health check-in: " + g.name, sb.ToString());
        sim.Say("ARIA: Check-in with " + first + " done. Their readings are on the sheet.");
    }

    void OfferMe()
    {
        if (ares.offers.Any(o => o.crew == 1 && !o.done)) { var o0 = ares.offers.First(o => o.crew == 1 && !o.done); o0.done = true; o0.onAccept?.Invoke(); aresShown.Add("accept"); sim.Say("ARIA: Thank you. Your support starts now — you chose it."); return; }
        var me = ares.C(1); me.fatigue = Mathf.Max(me.fatigue, 0.75f);
        var off = new AR.Offer { crew = 1, cond = (int)AR.Cond.Fatigue, tier = 2, text = "You've been working a long stretch, Commander. Take a 20-minute rest break in your quarters?", expires = AR.SimClock.hours + 48f };      // time runs fast here: the offer waits two days for your answer
        off.onAccept = () => ares.Accept(me, (int)AR.Cond.Fatigue); off.onDecline = () => ares.Refuse(me, (int)AR.Cond.Fatigue);
        ares.Log(1, "tier2", "T2 fatigue: offer sent — \"" + off.text + "\"", 2, (int)AR.Cond.Fatigue);
        ares.offers.Add(off); ares.onOffer?.Invoke(off);
        Banner("ARIA OFFERS YOU SUPPORT", "Yes or no in Menu > Project ARES > Crew interface (G again = yes)", false);
    }

    // ------------------------------------------------------------------ bedtime rules (Human > AI)
    int alertsAtBed;
    void BedtimeRules()
    {
        if (ares == null) return;
        if (sleepMe && !wasSleeping)
        {
            sleptSince = Time.unscaledTime; amFlags.Add("bed"); alertsAtBed = sim.alerts.Count;
            ares.Log(0, "night", "Night watch ON: the AI may only dim lights and watch vital signs. If ANYTHING goes wrong while people sleep, the AI stops itself and wakes a human, who takes over.");
            sim.Say(AR.AresHost.AiOff ? "ARIA is OFF tonight. Only the life-safety alarms are on." : "ARIA: Night watch is on. If anything goes wrong while you sleep, I stop myself and wake you — you take over.");
        }
        string what = null;
        if ((sleepMe || Time.unscaledTime - sleptSince < 3f) && !AR.AresHost.AiOff && sleptSince > 0f)
        {
            if (sleepMe && nightTest && Time.unscaledTime - sleptSince > 0.5f)      // test: ARIA breaks a bedtime rule (a night only lasts a second or two here)
            {
                nightTest = false;
                int qz = sleepZone >= 0 ? sleepZone : ZoneIndex("quarters", "sleep");
                if (qz >= 0) { overstepLight = new GameObject("ARES overstep light").AddComponent<Light>(); overstepLight.transform.SetParent(aresRoot, false); overstepLight.transform.position = game.zones[qz].position + Vector3.up * 2.6f; overstepLight.type = LightType.Point; overstepLight.range = 12f; overstepLight.intensity = 6f; overstepLight.color = AR.AresHost.Cct(6500); aresLights["overstep"] = overstepLight; }
                what = "ARIA broke a bedtime rule (bright light and music in the quarters)";
            }
            else if (sleepMe && sim.alerts.Count > alertsAtBed) what = "an alarm went off: " + sim.alerts[sim.alerts.Count - 1].title;
            else if (sleepMe && ares.escalatedTo.Count > 0 && ares.log.Count > 0 && ares.log[ares.log.Count - 1].tier >= 3) what = "a crewmate needs urgent help (Tier 3)";
        }
        if (what != null)
        {
            amFlags.Add("overstep"); sleptSince = -1f;
            ares.Log(1, "night", "NIGHT WATCH: " + what + ". AI STOPPED itself (every change undone). Commander woken and in charge.", 2);
            AresAiOff(true);
            if (sleepMe) SleepMeEnd("Something went wrong — ARIA stopped and woke you.");
            Banner("AI STOPPED — YOU ARE IN CHARGE", what, true);
            sim.Say("ARIA: While you slept, " + what + ". I have stopped myself and undone my changes. You are in charge. Press F12 when you want me back.");
        }
        wasSleeping = sleepMe;
    }

    // ------------------------------------------------------------------ menu pages
    void BuildAresMissionMenu()
    {
        if (!menu) return;
        pageAresMissions = new HunchMenu.ExtraPage { title = "ARES MISSIONS — mental health, step by step", parent = pageAres };
        pageAresCheck = new HunchMenu.ExtraPage { title = "CHECK ON YOUR CREW (health check-in)", parent = pageAres };
        pageAresMissions.build = mm =>
        {
            var cur = AmCur;
            mm.AddItem(cur == null ? "<color=#7CFC9A>All missions done ✓</color>" : "<color=#FFD070>Do my next step:</color> " + (amStarted && amStep < cur.steps.Count ? cur.steps[amStep].text : "start mission " + (amIndex + 1)), "The same as G when no alarm is waiting.", () => { menu.Close(); AresDoStep(); });
            for (int i = 0; i < amissions.Count; i++)
            {
                var mi = amissions[i]; string st = i < amIndex ? "<color=#7CFC9A>✓ done</color>" : i == amIndex ? "<color=#FFD070>▶ now</color>" : "<color=#8899AA>next</color>";
                mm.AddItem((i + 1) + ". " + mi.name + "   " + st, mi.rubric + " — " + mi.story + "  Steps: " + string.Join(" → ", mi.steps.Select(s => s.text)), () => { });
            }
            mm.AddItem("Skip this mission", "", () => { if (AmCur != null) { amIndex++; amStep = 0; amStarted = false; } mm.Rebuild(); });
            mm.AddItem("Restart all missions", "", () => { amIndex = 0; amStep = 0; amStarted = false; mm.Rebuild(); });
            mm.AddItem("Test: ARIA breaks a bedtime rule tonight", "Go to bed afterwards and see how a human stops it (F12). The interlock switches it off if nobody answers in 30 s.", () => { nightTest = true; sim.Say("ARIA: Tonight I will break a bedtime rule. Go to bed and see how a human stops me."); });
        };
        pageAresCheck.build = mm =>
        {
            for (int id = 2; id <= Mathf.Min(4, sim.crew.Count); id++) { int ii = id; mm.AddItem("Check on " + CrewName(ii) + (checkAt.ContainsKey(ii) ? "   <color=#7CFC9A>✓ checked " + AR.SimClock.Format(checkAt[ii]) + "</color>" : ""), "You walk over, ask, and read their wristband, skin patch and room sensors — then the six mental-health levels.", () => { menu.Close(); CheckOn(ii); }); }
            mm.AddItem("Where does the data come from? (D1 sensors)", "Wristband, chest beacon, skin patch, room mic/camera arrays (processed on-device), UWB beacons, CO2 pairs, terminals.", () => AresDoc(Mathf.Max(0, AR.DesignDocs.Pages.FindIndex(p => p.section.StartsWith("D1")))));
        };
        var prev = pageAres.build;
        pageAres.build = mm =>
        {
            mm.AddItem("<color=#FFD070>ARES missions (G = do my next step)</color>   " + (AmCur == null ? "all done ✓" : (amIndex + 1) + "/" + amissions.Count), "Six short missions about the crew's mental health, each tied to the rubric. G does the next step with you.", () => mm.OpenExtra(pageAresMissions));
            mm.AddItem("Check on your crew (health check-in)", "Walk over to a crewmate and read their sensors — with their consent.", () => mm.OpenExtra(pageAresCheck));
            prev(mm);
        };
    }

    public string ProbeMission() { var m = AmCur; return "mission " + (amIndex + 1) + "/" + amissions.Count + " " + (m != null ? m.name : "-") + " step " + (amStep + 1) + " :: " + (m != null && amStep < m.steps.Count ? m.steps[amStep].text : "-") + "  phase " + sim.phase + " sleepMe " + sleepMe + " aiOff " + AR.AresHost.AiOff + " day " + sim.Day + " " + sim.Hour.ToString("F1"); }
    public string ProbeAresLog() { return "nightTest=" + nightTest + " sleptSince=" + sleptSince.ToString("F1") + " now=" + Time.unscaledTime.ToString("F1") + " flags=" + string.Join(",", amFlags) + "\n  " + string.Join("\n  ", ares.log.Skip(Math.Max(0, ares.log.Count - 10)).Select(l => l.Time + " [" + l.kind + "] " + l.text)); }
    public string ProbeSkip4() { amIndex = 4; amStep = 0; amStarted = false; return "skipped to mission 5"; }
    public string ProbeToMission() { sim.phase = HabPhase.Mission; sim.timeFrozen = false; sim.alerts.Clear(); return "phase " + sim.phase; }
    public string ProbeQuiet() { sim.alerts.Clear(); return "alerts cleared"; }
    public string ProbeG() { sim.alerts.Clear(); game.GoToUrgent(); return ProbeMission(); }
    public string ProbeMissionStep() { string before = ProbeMission(); if (sim.phase == HabPhase.Mission) AresDoStep(); return before; }
}
