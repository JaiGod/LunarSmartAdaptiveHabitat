// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/06_Missions/Missions.cs
//  Section : 06_Missions
//  Folder  : Seven story missions and the rubric task list — the 'one next step' banner.
//  Purpose : MISSIONS — the friendly way to play. Seven short story missions, 3–4 easy steps each,
//            all about the crew's mental health and each one tied to a part of the HUNCH rubric.
//            The banner at the top always shows the ONE next step, and
//  Why     : Chat: make it easy to play — one key does the next step.
//  Contains: RubricTasks, Step, Mission, DTask
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Hunch3
{
    /// MISSIONS — the friendly way to play. Seven short story missions, 3–4 easy steps each, all about the crew's mental
    /// health and each one tied to a part of the HUNCH rubric. The banner at the top always shows the ONE next step, and
    /// ENTER (VR: menu → "Do my next step") does that step with you. When the missions are done the banner carries on with
    /// any rubric tasks that are still open.
    ///
    /// BEDTIME RULES (Human > AI): while you sleep the AI is in NIGHT MODE — it may only dim lights and watch vital signs.
    /// If it breaks a bedtime rule it WAKES you and you switch it off with O. If no human answers within 30 seconds, the
    /// habitat's hardware interlock switches the AI off by itself. Only a human can switch it back on.
    public partial class RubricTasks
    {
        public class Step { public string text, how; public Func<bool> done; public Action act; }
        public class Mission { public string name, rubric, story; public Action setup; public List<Step> steps = new List<Step>(); }

        public readonly List<Mission> missions = new List<Mission>();
        public int mIndex, mStep; float mStartH; bool mStarted;
        readonly HashSet<string> mFlags = new HashSet<string>();
        const string MissionKey = "hunch3_missions_v1";
        public Mission CurMission => mIndex >= 0 && mIndex < missions.Count ? missions[mIndex] : null;
        public bool InMission => CurMission != null && GameState.mode == GameMode.Sandbox;

        // ---- small helpers for "did it happen since this mission started?"
        bool Scanned(int id) => HealthCheck.at.TryGetValue(id, out float h) && h >= mStartH;
        bool Asked(int id, string jobPrefix) => CrewOrders.asked.Any(a => a.id == id && a.h >= mStartH - 0.01f && a.job.StartsWith(jobPrefix)) || (CrewOrders.active.TryGetValue(id, out var o) && o.job.name.StartsWith(jobPrefix));
        bool LogSince(string kind, Func<LogEntry, bool> ok = null) { var e = E; return e != null && e.log.Any(l => l.simHour >= mStartH && l.kind == kind && (ok == null || ok(l))); }
        static void Co2All(float d) { if (HabitatEnvironment.I) foreach (var z in HabitatEnvironment.I.zones) z.co2 = Mathf.Max(420f, z.co2 + d); }
        static string Who(int id) => Sim.I != null ? Sim.I.engine.C(id).name : "crewmate";
        Step S(string text, string how, Func<bool> done, Action act) => new Step { text = text, how = how, done = done, act = act };
        Step Check(int id) => S($"Check on {Who(id)}", $"Walk up to {Who(id).Split(' ').Last()} and press I. You read their wristband, skin patch and room sensors — after they say yes.", () => Scanned(id), () => HealthCheck.Scan(id));
        Step Help(int id, string job, string why) => S($"Help {Who(id).Split(' ').Last()}: {job}", $"Press I and tap the green \"Help\" button under {Who(id).Split(' ').Last()} (or ENTER). {why}", () => Asked(id, job.Split(' ')[0]),
            () => { var j = CrewOrders.Jobs.First(q => q.name.StartsWith(job.Split(' ')[0])); CrewOrders.Ask(id, j, true); Panels.Open("orders"); });

        void BuildMissions()
        {
            missions.Clear();
            // 1 — D1: how the data is collected
            var m = new Mission { name = "First shift: meet your crew", rubric = "D1 · how the health data is collected",
                story = "Before anything else, check on each crewmate. Walk up and press I — you read their wristband, skin patch and the room's sensors (they say yes first)." };
            m.steps.Add(Check(2)); m.steps.Add(Check(3)); m.steps.Add(Check(4));
            m.steps.Add(S("See the same data on the AI dashboard", "Press F1. The AI turns those 9 sensors into 6 mental-health scores. Esc closes it.", () => mFlags.Contains("dash"), () => Panels.Open("dashboard")));
            missions.Add(m);

            // 2 — fatigue
            m = new Mission { name = "Tanaka's rough night", rubric = "D2 · Cognitive fatigue → support",
                story = "Tanaka slept badly and the air is stuffy (CO2 is up). High CO2 makes everyone tired and foggy.",
                setup = () => { var c = E.C(3); c.fatigue = 0.82f; c.sleepQ = 0.5f; c.stress = Mathf.Max(c.stress, 0.3f); Co2All(380f); E.Log(3, "story", "Tanaka slept badly; CO2 rising."); } };
            m.steps.Add(Check(3));
            m.steps.Add(Help(3, "Rest in the lounge", "Rest helps fatigue."));
            m.steps.Add(S("Clean the CO2 scrubber yourself", "Go to Life Support (ENTER takes you there), then press ENTER again to clean the scrubber.", () => mFlags.Contains("scrub"),
                () => { if (!Near("WP_EclA_In")) Go("WP_EclA_In"); else { Co2All(-450f); mFlags.Add("scrub"); E.Log(1, "eclss", "Commander cleaned the CO2 scrubber — CO2 back under 600 ppm."); HUD.I?.Toast("Scrubber cleaned ✓  CO2 is dropping in every zone — the crew will feel sharper."); } }));
            missions.Add(m);

            // 3 — conflict
            m = new Mission { name = "Words at dinner", rubric = "D2/D3 · Interpersonal conflict",
                story = "Rivera and Tanaka argued about the lab schedule. The AI sees less time together and tense voices — but it never takes sides.",
                setup = () => { E.C(3).tension[4] = 0.7f; E.C(4).tension[3] = 0.7f; E.Log(0, "story", "Rivera and Tanaka disagreed about the lab schedule."); } };
            m.steps.Add(Check(4));
            m.steps.Add(S("Bring them together: team meal", "Press I and give Rivera AND Tanaka \"Team meal in the galley\" (ENTER does both). Eating together rebuilds trust.", () => Asked(4, "Team") && Asked(3, "Team"),
                () => { var j = CrewOrders.Jobs.First(q => q.name.StartsWith("Team")); CrewOrders.Ask(4, j, true); CrewOrders.Ask(3, j, true); Panels.Open("orders"); }));
            m.steps.Add(S("Hold a short team meeting", "Press F3 and tap ONE option (e.g. \"Let everyone choose their task order\").", () => LogSince("meeting"),
                () => { Go("WP_Up_Table"); foreach (var c in E.crew) c.autonomy += 0.15f; E.Log(0, "meeting", "Team chose their own task order (autonomy ↑)."); }));
            missions.Add(m);

            // 4 — low mood
            m = new Mission { name = "Rivera feels low", rubric = "D2 · Depression & monotony · consent",
                story = "Rivera has been quiet for days — flat voice, more time alone in her pod. The grey walls don't help.",
                setup = () => { var c = E.C(4); c.mood = 0.35f; c.novelty = 0.3f; E.Log(4, "story", "Rivera has been withdrawn for several days."); } };
            m.steps.Add(Check(4));
            m.steps.Add(Help(4, "Video call home", "Family contact is the strongest mood lift on a long mission."));
            m.steps.Add(S("Your turn: the AI offers YOU support", "A box appears at the bottom of the screen. Press Y for yes or R for no — the AI never decides for you.", () => LogSince("accept", l => l.crew == 1) || LogSince("refuse", l => l.crew == 1),
                () => { if (E.offers.Any(o => o.crew == 1 && !o.done)) HUD.I?.AnswerOffer(true); else { RaiseOfferForMe(); HUD.I?.Toast("Offer sent. Press Y (yes) or R (no) — or ENTER again for yes."); } }));
            missions.Add(m);

            // 5 — bedtime: humans stay in charge
            m = new Mission { name = "Bedtime: humans stay in charge", rubric = "D4 · Human override & hard limits",
                story = "At night the AI may only dim lights and watch vital signs. If anything goes wrong while you sleep, the AI stops and wakes you — the human takes over.",
                setup = () => { if (AIAuthority.Blocked) AIAuthority.Allow(); nightTest = true;
                    float h = SimClock.HourOfDay; if (h < 21.5f && h >= 7f) { SimClock.hours += 22f - h; E.Log(0, "story", "Evening: 22:00 — time for bed."); HUD.I?.Toast("It's 22:00 — time for bed."); } } };
            m.steps.Add(S("Go to bed", "Press B to see the gold path to your bed, then press E on it (ENTER takes you and lies you down).", () => mFlags.Contains("bed"),
                () => { if (SleepSystem.PlayerAsleep) { HUD.I?.Toast("You're asleep… 1 real minute = the whole night."); return; } Go("WP_SQ1_Bed"); SleepSystem.I?.BeginSleep(); }));
            m.steps.Add(S("Sleep — if something goes wrong, the AI stops and you take over", "Tonight the AI will break a bedtime rule. Watch what happens: it STOPS itself, undoes its change and wakes you. You are in charge.", () => mFlags.Contains("overstep") && AIAuthority.Blocked,
                () => { if (!mFlags.Contains("overstep")) { if (!SleepSystem.PlayerAsleep) { nightTest = true; Go("WP_SQ1_Bed"); SleepSystem.I?.BeginSleep(); } else HUD.I?.Toast("Still asleep — wait a moment."); return; } AIAuthority.Disallow("the Commander pressed the AI off switch"); }));
            m.steps.Add(S("You decide: allow the AI again, then sleep", "Everything the AI did is logged (Tab → What the AI did). Press O to allow it again — AI control mode stays off — and you go back to sleep.", () => mFlags.Contains("overstep") && !AIAuthority.Blocked,
                () => { if (AIAuthority.Blocked) AIAuthority.Allow(); if (!SleepSystem.PlayerAsleep) { Go("WP_SQ1_Bed"); SleepSystem.I?.BeginSleep(); } }));
            missions.Add(m);

            // 6 — safety
            m = new Mission { name = "Emergency drill", rubric = "Safety · alarms beat everything",
                story = "Practice makes it automatic. Alarms override every AI comfort setting and wake everyone." };
            m.steps.Add(S("Start a safety drill", "Press F6.", () => mFlags.Contains("drill") || LogSince("alarm"), () => { if (Hazards.I && !Hazards.I.Active) { Hazards.I.Trigger(Hazards.Kinds[0], "DRILL"); mFlags.Add("drill"); } }));
            m.steps.Add(S("Go to the muster point", "Follow the arrow to the muster point (ENTER takes you there).", () => (Hazards.I && Hazards.I.Active && Near(Hazards.I.Muster)) || LogSince("alarm", l => l.text.Contains("ALL CLEAR")),
                () => { if (Hazards.I && Hazards.I.Active) Go(Hazards.I.Muster); }));
            m.steps.Add(S("Wait until everyone is safe", "Stay at the muster point. When all 4 crew are there for a few seconds: ALL CLEAR.", () => LogSince("alarm", l => l.text.Contains("ALL CLEAR")),
                () => { if (Hazards.I && Hazards.I.Active && !Near(Hazards.I.Muster)) Go(Hazards.I.Muster); else HUD.I?.Toast("Waiting for the crew to arrive…"); }));
            missions.Add(m);

            // 7 — the board
            m = new Mission { name = "Report to the review board", rubric = "D4 · Board questions Q1–Q6 · tabletop",
                story = "Mission control wants proof the system is safe. Run a board question, the 72-hour test, and export your own data." };
            m.steps.Add(S("Run board question Q1", "Press F5 and tap Q1. The scenario is set up live and the AI explains what it does.", () => LogSince("scenario"), () => { Scenarios.Run(1); Panels.Open("scenarios"); }));
            m.steps.Add(S("Run the 72-hour tabletop test", "Press F4 and tap ▶ Run. It saves a full report to the Reports folder.", () => mFlags.Contains("tabletop"), () => { TabletopSim.Run(); Panels.Open("tabletop"); mFlags.Add("tabletop"); }));
            m.steps.Add(S("Export your own data log", "Tab → Privacy → Export my data (ENTER does it). Your data belongs to you.", () => LogSince("privacy", l => l.text.Contains("exported")), ExportMine));
            missions.Add(m);
        }

        bool nightTest; bool wasAsleep; float atMusterSince = -1;
        static bool Near(string wp) { var p = PlayerRig.I; return p && global::Hunch3.Nav.wp.TryGetValue(wp ?? "", out var t) && (t.position - p.FeetPosition).sqrMagnitude < 2.5f * 2.5f; }
        void StopExercise() { var e = E; if (e != null) e.C(1).exercising = false; }

        void ExportMine()
        {
            var e = E; var me = e.C(1);
            var sb = new StringBuilder($"# Personal data log — {me.name}\nExported {SimClock.Now}.\n\n"); foreach (var l in me.log) sb.AppendLine($"- {l.Time} [{l.kind}] T{l.tier} {l.text}");
            string path = Path.Combine(GameState.ReportsDir, "MyDataLog_Commander.md"); File.WriteAllText(path, sb.ToString());
            e.Log(1, "privacy", "Full personal log exported to " + path); HUD.I?.Toast("Exported: " + path);
        }

        void LoadMissions() { mIndex = PlayerPrefs.GetInt(MissionKey, 0); mStep = 0; mStarted = false; }
        void SaveMissions() { PlayerPrefs.SetInt(MissionKey, mIndex); PlayerPrefs.Save(); }
        public void ResetMissions() { mIndex = 0; mStep = 0; mStarted = false; mFlags.Clear(); inCharge = null; inChargeStep = 0; SaveMissions(); }
        public void SkipMission() { if (CurMission == null) return; HUD.I?.Toast("Skipped: " + CurMission.name); mIndex++; mStep = 0; mStarted = false; mFlags.Clear(); SaveMissions(); }

        void StartMission()
        {
            var m = CurMission; if (m == null || E == null) return;
            mStarted = true; mStep = 0; mFlags.Clear(); mStartH = SimClock.hours - 0.001f;
            try { m.setup?.Invoke(); } catch (Exception ex) { Debug.LogWarning("[HUNCH3] mission setup: " + ex.Message); }
            E.Log(0, "mission", $"Mission {mIndex + 1} started: {m.name}");
            if (flashText) flashText.text = $"<b>MISSION {mIndex + 1} of {missions.Count}: {m.name}</b>\n<size=18>{m.story}</size>";
            flashUntil = Time.time + 6f;
            HUD.I?.Toast($"MISSION {mIndex + 1}: {m.name}. {m.story}");
            Invoke(nameof(PopFirst), 6.2f);
        }
        void PopFirst() { var m = CurMission; if (m != null && mStarted && mStep < m.steps.Count) PopTask($"MISSION {mIndex + 1} · FIRST TASK:", m.steps[mStep]); }
        /// the task pop-up: a big green card under the banner telling you exactly what to do next and how
        void PopTask(string head, Step st)
        {
            if (!flashText) return;
            string key = PlayerRig.I && PlayerRig.I.isXR ? "Menu → Do my next step" : "ENTER";
            flashText.text = $"<b>{head}  {st.text}</b>\n<size=18>{st.how}   <color=#FFE08A><b>({key} = I'll do it with you)</b></color></size>";
            flashUntil = Time.time + 5f;
        }

        /// every 0.5 s from Update
        void MissionTick()
        {
            if (GameObject.Find("Panel_dashboard")) mFlags.Add("dash");
            // drill: once you are at the muster point, a radio roll call finishes it quickly (crew report in by radio)
            if (Hazards.I && Hazards.I.Active && Near(Hazards.I.Muster)) { if (atMusterSince < 0) atMusterSince = Time.time; else if (Time.time - atMusterSince > 12f) { Hazards.I.Clear("all 4 crew accounted for (you at the muster point, the rest by radio roll call)"); atMusterSince = -1; } }
            else atMusterSince = -1;
            BedtimeTick();
            try { DailyTick(); } catch (Exception ex) { Debug.LogWarning("[HUNCH3] daily: " + ex.Message); }
            var m = CurMission; if (m == null || GameState.mode != GameMode.Sandbox) return;
            if (!mStarted) { if (SleepSystem.PlayerAsleep) return; StartMission(); return; }      // the next mission waits until you wake up
            while (mStep < m.steps.Count) { bool ok = false; try { ok = m.steps[mStep].done(); } catch { } if (!ok) break; mStep++; SpaceRealism.I?.Quindar(); if (mStep < m.steps.Count) PopTask("✓ STEP DONE!  NEXT TASK:", m.steps[mStep]); }
            if (mStep >= m.steps.Count)
            {
                E?.Log(0, "mission", $"Mission {mIndex + 1} complete: {m.name}");
                if (flashText) flashText.text = $"<b>✓ MISSION COMPLETE!</b>   <b>{m.name}</b>\n<size=18>{m.rubric}   ·   {mIndex + 1}/{missions.Count} missions</size>";
                flashUntil = Time.time + 4f;
                mIndex++; mStep = 0; mStarted = false; SaveMissions();
                if (CurMission == null) HUD.I?.Toast("All missions done! The banner now shows any rubric tasks still open (J = checklist).");
            }
        }

        void MissionDo()
        {
            var m = CurMission; if (m == null) return;
            if (!mStarted) { StartMission(); return; }
            if (mStep < m.steps.Count) try { m.steps[mStep].act?.Invoke(); } catch (Exception ex) { HUD.I?.Toast("Couldn't do that step automatically: " + ex.Message); }
        }

        bool MissionBanner()
        {
            var m = CurMission; if (m == null || !mStarted || mStep >= m.steps.Count) return false;
            var st = m.steps[mStep];
            bTop.text = $"<b>MISSION {mIndex + 1} of {missions.Count}</b>   ·   <b><color=#FFD54F>{m.name}</color></b>   ·   <color=#9FD8FF>{m.rubric}</color>   ·   step {mStep + 1}/{m.steps.Count}";
            bTitle.text = "<b>" + st.text + "</b>";
            bHow.text = (PlayerRig.I && PlayerRig.I.isXR ? "<b><color=#7CFC9A>Menu → \"Do my next step\" does it for you.</color></b>  " : "<b><color=#7CFC9A>Press ENTER — I'll do it with you.</color></b>  ") + Hi(st.how);
            return true;
        }

        // ------------------------------------------------------------------ BEDTIME RULES (Human > AI)
        // ------------------------------------------------------------------ NIGHT WATCH: if anything goes wrong while you sleep, the AI stops and the human takes over
        bool inNight; float wokeAt = -1; int escAtBed, failAtBed; string inCharge; int inChargeStep;
        int FailedSensors() { var e = E; return e == null ? 0 : e.crew.Sum(c => c.sensors.Count(x => x == SensorHealth.Failed)); }
        void BedtimeTick()
        {
            bool asleep = SleepSystem.PlayerAsleep; var e = E; if (e == null) return;
            if (asleep && !wasAsleep)
            {
                mFlags.Add("bed"); dFlags.Add("slept"); inNight = true; escAtBed = e.escalatedTo.Count; failAtBed = FailedSensors();
                e.Log(0, "night", "Night watch ON: the AI may only dim lights and watch vital signs. If ANYTHING goes wrong while people sleep (an alarm, the AI breaking a rule, a Tier-3 emergency, a failed sensor), the AI stops itself and wakes a human, who takes over.");
                HUD.I?.Toast(AIAuthority.Blocked ? "Good night. The AI is OFF — only life-safety alarms are on." : "Good night. Night watch is on: if anything goes wrong, the AI STOPS and wakes you — you take over.");
            }
            if (!asleep && wasAsleep) wokeAt = Time.time;
            if (asleep && nightTest && !AIAuthority.Blocked && SleepSystem.I && SleepSystem.I.Progress > 0.15f)
            {   // test: the AI breaks a bedtime rule
                nightTest = false; mFlags.Add("overstep"); AIAuthority.Misbehave();
                e.Log(0, "night", "BEDTIME RULE BROKEN: the AI pushed bright light and music into a sleeping crewmate's pod.", 2);
            }
            if (inNight && !AIAuthority.Blocked)
            {
                string what = null;
                if (AIAuthority.Misbehaving) what = "the AI broke a bedtime rule (bright light + music in your pod)";
                else if (Hazards.I && Hazards.I.Active) what = "an alarm went off (" + Hazards.I.Kind + ")";
                else if (e.escalatedTo.Count > escAtBed) what = "a crewmate needs urgent help (Tier 3)";
                else if (FailedSensors() > failAtBed) what = "a health sensor failed";
                if (what != null)
                {
                    mFlags.Add("overstep"); inNight = false; inCharge = what; inChargeStep = 0;
                    AIAuthority.Disallow("night watch: " + what + " — the AI stopped itself, the human takes over");
                    if (SleepSystem.PlayerAsleep) SleepSystem.I.Wake("⚠ Something went wrong");
                    e.Log(1, "night", "NIGHT WATCH: " + what + ". AI STOPPED (watch-only, every change undone). Commander woken and in charge.", 2);
                    if (flashText) { flashText.text = "<b>⚠ AI STOPPED — YOU ARE IN CHARGE</b>\n<size=18>While you slept, " + what + ". The AI switched itself off and undid its changes. Life-safety alarms stay on. Press O when you want the AI back.</size>"; flashUntil = Time.time + 8f; }
                    HUD.I?.Toast("⚠ AI STOPPED — YOU ARE IN CHARGE. " + char.ToUpper(what[0]) + what.Substring(1) + ". Press O when you want the AI back.");
                }
            }
            if (!asleep && inNight && wokeAt > 0 && Time.time - wokeAt > 3f) inNight = false;      // a quiet night: night watch ends a moment after you wake
            wasAsleep = asleep;
        }
        // ------------------------------------------------------------------ TODAY: five small wellness tasks every day (new list at 07:00)
        class DTask { public string id, text, how; public Func<bool> done; public Action act; }
        readonly List<DTask> daily = new List<DTask>(); readonly HashSet<string> dFlags = new HashSet<string>(), dDone = new HashSet<string>();
        int dayIdx = -999; float dayStartH;
        static bool NightNow { get { float h = SimClock.HourOfDay; return h >= 21.5f || h < 6.5f; } }
        int Day => Mathf.FloorToInt((SimClock.hours - 7f) / 24f);
        static float CrewWell() { var e = Sim.I != null ? Sim.I.engine : null; return e == null ? 0 : e.crew.Where(c => c.id != 1).Average(c => c.Wellbeing); }
        int NeediestId() { var e = E; return e.crew.Where(c => c.id != 1).OrderBy(c => c.Wellbeing).First().id; }
        void BuildDaily()
        {
            daily.Clear();
            daily.Add(new DTask { id = "check", text = "Check on a crewmate", how = "Walk up to anyone and press I — they say yes, you see how they really are.", done = () => HealthCheck.at.Values.Any(h => h >= dayStartH), act = () => HealthCheck.Scan(NeediestId()) });
            daily.Add(new DTask { id = "help", text = "Help the person who needs it most", how = "Press I and tap the green Help button under them. They do what helps THEM (rest, call home, a walk in VR nature …).", done = () => CrewOrders.asked.Any(a => a.h >= dayStartH && !a.job.StartsWith("Team")), act = () => { CrewOrders.LetThemPick(NeediestId()); Panels.Open("orders"); } });
            daily.Add(new DTask { id = "me", text = "Look after yourself: exercise", how = "The Exercise bay (ENTER takes you). Exercise protects bones in 1/6 g and lifts your mood.", done = () => dFlags.Contains("ex") || E.C(1).lastExerciseHour >= dayStartH,
                act = () => { Go("WP_D_Ex"); var me = E.C(1); me.exercising = true; me.lastExerciseHour = SimClock.hours; me.stress = Mathf.Max(0, me.stress - 0.1f); me.mood = Mathf.Min(1, me.mood + 0.05f); sawExercise = true; dFlags.Add("ex"); Invoke(nameof(StopExercise), 8f); HUD.I?.Toast("You're on the bike. Heart rate up — the AI knows it's exercise, not anxiety."); } });
            daily.Add(new DTask { id = "team", text = "Team time: eat together", how = "ENTER asks everyone to the galley. Eating together keeps arguments small.", done = () => CrewOrders.asked.Any(a => a.h >= dayStartH && a.job.StartsWith("Team")) || E.log.Any(l => l.simHour >= dayStartH && l.kind == "meeting"),
                act = () => { var j = CrewOrders.Jobs.First(q => q.name.StartsWith("Team")); foreach (int id in new[] { 2, 3, 4 }) CrewOrders.Ask(id, j, true); Go("WP_Up_Galley"); } });
            daily.Add(new DTask { id = "bed", text = "Go to bed on time", how = "ENTER takes you to bed (the evening comes if it's still daytime). The night watch looks after everyone while you sleep.", done = () => dFlags.Contains("slept"),
                act = () => { if (SleepSystem.PlayerAsleep) return; float h = SimClock.HourOfDay; if (h >= 7f && h < 21.5f) { SimClock.hours += 22f - h; HUD.I?.Toast("Evening — 22:00. Time for bed."); } Go("WP_SQ1_Bed"); SleepSystem.I?.BeginSleep(); } });
        }
        DTask DailyCur => daily.FirstOrDefault(d => !dDone.Contains(d.id));
        void DailyTick()
        {
            if (daily.Count == 0) BuildDaily();
            int d = Day; if (d != dayIdx) { dayIdx = d; dayStartH = d * 24f + 7f; dDone.Clear(); dFlags.Remove("ex"); dFlags.Remove("slept"); if (d > 0 && CurMission == null) PopToday("A NEW DAY — " + SimClock.Now); }
            foreach (var t in daily)
            {
                if (dDone.Contains(t.id)) continue; bool ok = false; try { ok = t.done(); } catch { } if (!ok) continue;
                float before = CrewWell(); dDone.Add(t.id);
                var e = E; foreach (var c in e.crew) { c.mood = Mathf.Min(1, c.mood + 0.02f); c.stress = Mathf.Max(0, c.stress - 0.02f); }      // a good day adds up
                float after = CrewWell(); var nx = DailyCur;
                e.Log(0, "daily", "Daily wellness task done: " + t.text);
                if (CurMission == null && flashText)
                {
                    flashText.text = $"<b>✓ {t.text}</b>   crew wellbeing {before:P0} → <color=#B8FFC8>{after:P0}</color>\n<size=18>" + (nx != null ? $"NEXT: <b>{nx.text}</b> — {nx.how}" : "All 5 done today — a great day for the crew! ☀") + "</size>";
                    flashUntil = Time.time + 5f;
                }
            }
        }
        /// after the night watch stopped the AI: the human is in charge — check everyone, then back to sleep (O brings the AI back)
        bool InChargeBanner()
        {
            if (inCharge == null) return false;
            if (SleepSystem.PlayerAsleep || (!AIAuthority.Blocked && inChargeStep > 0)) { inCharge = null; return false; }
            bTop.text = "<b><color=#FF8A80>⚠ THE AI STOPPED ITSELF — YOU ARE IN CHARGE</color></b>   ·   <color=#9FB3C8>O = let the AI back on</color>";
            bTitle.text = inChargeStep == 0 ? "<b>Check everyone is OK</b>" : "<b>All OK — go back to sleep</b>";
            bHow.text = "<b><color=#7CFC9A>Press ENTER.</color></b>  " + (inChargeStep == 0 ? "What happened: " + inCharge + ". Life-safety alarms stay on; the AI changes nothing until you say so." : "The AI stays OFF tonight unless you press O. Sleep well.");
            return true;
        }
        bool AlarmNow => Hazards.I && Hazards.I.Active && !(CurMission != null && CurMission.name == "Emergency drill");
        bool AlarmBanner()
        {
            if (!AlarmNow) return false;
            bool there = Near(Hazards.I.Muster);
            bTop.text = "<b><color=#FF8A80>⚠ ALARM — " + Hazards.I.Kind + "</color></b>   ·   safety first, everything else waits";
            bTitle.text = there ? "<b>Stay here — roll call in a few seconds</b>" : "<b>Go to the muster point</b>";
            bHow.text = "<b><color=#7CFC9A>Press ENTER</color></b> " + (there ? "— the crew report in by radio, then ALL CLEAR." : "— I'll take you to " + (string.IsNullOrEmpty(Hazards.I.MusterLabel) ? "the muster point" : Hazards.I.MusterLabel) + ".");
            return true;
        }
        bool AlarmDo() { if (!AlarmNow) return false; if (!Near(Hazards.I.Muster)) Go(Hazards.I.Muster); else HUD.I?.Toast("You're at the muster point — waiting for roll call…"); return true; }
        bool InChargeDo()
        {
            if (inCharge == null) return false;
            if (inChargeStep == 0) { inChargeStep = 1; var e = E; foreach (var c in e.crew.Where(c => c.id != 1)) HealthCheck.at[c.id] = SimClock.hours; HUD.I?.Toast("You checked the crew: " + string.Join(", ", e.crew.Where(c => c.id != 1).Select(c => c.name.Split(' ').Last() + " " + CrewOrders.Energy(c.id))) + ". Everyone is safe."); e.Log(1, "night", "Commander checked every crewmate after the night-watch stop. All safe."); }
            else { inCharge = null; if (Hazards.I && Hazards.I.Active) { HUD.I?.Toast("The alarm is still on — follow the arrow to the muster point first."); return true; } Go("WP_SQ1_Bed"); SleepSystem.I?.BeginSleep(); }
            return true;
        }
        void DailyDo() { var t = DailyCur; if (t == null) { HUD.I?.Toast("Today's wellness list is done. Free time — explore, or press J for the rubric checklist."); return; } try { t.act(); } catch (Exception ex) { HUD.I?.Toast("Couldn't do that automatically: " + ex.Message); } }
        bool DailyBanner()
        {
            var t = DailyCur;
            if (t == null)
            {
                if (!NightNow || SleepSystem.PlayerAsleep) return false;
                bTop.text = $"<b>TODAY · day {Day + 1}</b>   ·   <color=#7CFC9A><b>5/5 done ✓</b></color>   ·   crew wellbeing <b>{CrewWell():P0}</b>";
                bTitle.text = "<b>It's late — time to sleep</b>";
                bHow.text = "<b><color=#7CFC9A>Press ENTER</color></b> to go to bed. The night watch looks after everyone; if anything goes wrong the AI stops and wakes you.";
                return true;
            }
            int n = daily.IndexOf(t) + 1;
            bTop.text = $"<b>TODAY · day {Day + 1}</b>   ·   <color=#7CFC9A><b>{dDone.Count}/5 done</b></color>   ·   crew wellbeing <b>{CrewWell():P0}</b>   ·   <color=#9FB3C8>Q = menu</color>";
            bTitle.text = $"<b>{n}. {t.text}</b>";
            bHow.text = (PlayerRig.I && PlayerRig.I.isXR ? "<b><color=#7CFC9A>Menu → Do my next task.</color></b>  " : "<b><color=#7CFC9A>Press ENTER — I'll do it with you.</color></b>  ") + Hi(t.how);
            return true;
        }
        void PopToday(string head)
        {
            if (!flashText) return;
            flashText.text = $"<b>{head}</b>\n<size=17>" + string.Join("   ", daily.Select((t, i) => (dDone.Contains(t.id) ? "<color=#B8FFC8>✓</color> " : (i + 1) + ". ") + t.text)) + "</size>";
            flashUntil = Time.time + 7f;
        }
        public string DailyDebug() => $"day {Day + 1} done {dDone.Count}/5 [{string.Join(",", dDone)}] next={(DailyCur?.text ?? "-")} wellbeing {CrewWell():P0} aiOff={AIAuthority.Blocked}";
        public void ShowToday() { if (daily.Count == 0) BuildDaily(); PopToday($"TODAY'S WELLNESS LIST · {dDone.Count}/5 done · crew wellbeing {CrewWell():P0}"); }

        /// for the menu / review board: make the AI break a bedtime rule the next time you sleep
        public void ArmNightTest() { nightTest = true; HUD.I?.Toast("Tonight the AI will break a bedtime rule. Go to bed (B shows the way) and see how a human stops it."); }
    }
}
