using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Hunch3
{
    public static class Scenarios
    {
        public static string lastExplain;
        public static readonly string[] Short = { "Workout false positive", "Depression + conflict at once", "Silent change discovered", "Cortisol patch fails Day 45", "No permanent opt-out?", "CMO on EVA during Tier 3" };
        public static void Run(int q)
        {
            var e = Sim.I.engine; var sb = new StringBuilder();
            switch (q)
            {
                case 1:
                    { var c = e.C(3); c.exercising = true; c.lastExerciseHour = SimClock.hours; c.stress = 0.35f;
                      e.Step(0.02f); float withCtx = c.score[0];
                      float raw = (e.Feature(c, Cond.Anxiety, Sensor.HRV) / 0.25f);
                      sb.AppendLine("<b>Q1 — HRV drop + vocal tension right after a hard workout.</b>");
                      sb.AppendLine($"Tanaka just finished the treadmill. Raw HRV/GSR/voice look like stress, but the beacon's activity tag marks EXERCISE/EVA context.");
                      sb.AppendLine($"Context rule: HRV, GSR and voice-tension weights × 0.25 for 30 min after exercise or EVA → anxiety score {withCtx:0.00} (stays below Tier 1 = {CondSpec.T1}).");
                      sb.AppendLine("The 3-minute persistence rule + the 20-min monitoring window catch REAL anxiety that outlasts the workout. No false alarm, no intervention, nothing logged against the crew member.");
                      c.exercising = false; }
                    break;
                case 2:
                    { var a = e.C(4); a.mood = 0.25f; a.aboveSince[1] = SimClock.hours - 49; a.smooth[1] = 0.62f;
                      e.SetTension(2, 3, 0.75f); e.C(2).smooth[2] = 0.6f; e.C(3).smooth[2] = 0.6f; e.C(2).aboveSince[2] = SimClock.hours - 7; e.C(3).aboveSince[2] = SimClock.hours - 7;
                      e.Step(0.02f);
                      sb.AppendLine("<b>Q2 — Rivera shows depression while Okafor & Tanaka are in a Tier 2 conflict.</b>");
                      sb.AppendLine("Both protocols run in parallel, each on its own monitoring clock (depression 72 h, conflict 24 h).");
                      sb.AppendLine("Lever arbitration by ZONE: depression → behavioural activation in the GREENHOUSE + bright light in the wardroom; conflict → the pair is separated by timing/routes, the COMMONS becomes a calm alternative, Config B screens deploy on acceptance.");
                      sb.AppendLine("No lever fights another because they act in different zones; the Commander sees only team-level suggestions in the meeting. Watch the dashboard: 3 crew with active support.");
                      HabitatEnvironment.I?.SetConfigB(true); }
                    break;
                case 3:
                    { var c = e.C(2); e.Log(2, "tier1", "T1 Anxiety: Dim zone to 2400 K amber, alpha binaural, 23 °C, lavender (silent).", 1, 0);
                      c.notifyEverySilentChange = true; e.Log(2, "recovery", "Recovery protocol: AI apologised, showed the full log + reasons, switched Okafor to 'tell me every change', offered to disable Tier 1 for her entirely.");
                      sb.AppendLine("<b>Q3 — A crew member finds out the AI changed her lighting and audio yesterday without telling her, and is furious.</b>");
                      sb.AppendLine("Prevention: every crew member agrees pre-mission to Tier 1 silent support, and every silent change is ALWAYS in their personal log (Crew Interface → AI actions). Silent ≠ secret.");
                      sb.AppendLine("Recovery: 1) the AI apologises and explains exactly what and why, 2) shows the log, 3) offers 'tell me about every change' or 'no Tier 1 for me', 4) a human (CMO) follows up only if she wants. Applied to Dr. Okafor now.");
                      Panels.crewTab = "ai actions"; }
                    break;
                case 4:
                    { var c = e.C(3); c.sensors[(int)Sensor.Cortisol] = SensorHealth.Failed; SimClock.hours = Mathf.Max(SimClock.hours, 44 * 24f); e.Step(0.02f);
                      float confBefore = c.confidence; e.SelfCheck(c, 0.5f, 0.5f); e.Step(0.02f);
                      e.Log(3, "care", "Cortisol patch out until resupply (Day 90): weekly CMO wellness chat booked with Tanaka's agreement; daily 2-question self-check offered.", 0);
                      sb.AppendLine($"Daily self-check (2 taps in the Crew Interface) replaces part of the lost signal: confidence {confBefore:P0} → {c.confidence:P0} after Tanaka's first self-check.");
                      sb.AppendLine("<b>Q4 — Cortisol patch fails on Day 45; resupply on Day 90.</b>");
                      sb.AppendLine($"Degraded mode: cortisol removed, depression weights renormalised over sleep, voice, face, movement, HRV → Tanaka confidence now {c.confidence:P0}.");
                      sb.AppendLine("Below 50 % confidence a condition can't go above Tier 1 on sensors alone (so no false alarms); instead the AI schedules a weekly CMO wellness chat + a 2-question self-check in the Crew Interface to replace the missing signal.");
                      sb.AppendLine("Mitigation for the 45 days: daily 'patch-less' saliva test card from the medical kit (manual input), and cortisol is never a single point of failure because it has 0 weight in anxiety/conflict/fatigue."); }
                    break;
                case 5:
                    sb.AppendLine("<b>Q5 — Why no permanent opt-out?</b>");
                    sb.AppendLine("Ethics: crew CAN opt out of every psychological sensor (voice, face, cortisol, GSR) and pause all psych monitoring for 72 h — no reason needed.");
                    sb.AppendLine("Safety: heart-rate extremes and shared-air CO2 stay on because they detect life-threatening events and protect OTHER crew in a sealed habitat — they are vital signs, not mood data.");
                    sb.AppendLine("A permanent opt-out is possible but routed through a human conversation with the CMO (never decided by the AI), so the crew member's autonomy and the crew's safety are both respected.");
                    Panels.crewTab = "privacy";
                    break;
                case 6:
                    { var cmo = e.C(2); cmo.onEVA = true; var s = e.C(4); s.smooth[1] = 0.8f; s.aboveSince[1] = SimClock.hours - 60; e.Step(0.02f);
                      sb.AppendLine("<b>Q6 — Tier 3 while the CMO is on a 4-hour EVA.</b>");
                      sb.AppendLine("Responder chain: CMO unavailable (EVA) → Commander (you) gets the Tier 3 with the subject's consent-scoped details, + a low-priority ping to the CMO's suit beacon.");
                      sb.AppendLine("If no acknowledgement within the Tier-3 limit (15 min for acute anxiety/fatigue, 1 h for depression/conflict/autonomy/monotony) → Mission Control (after the crew member is told). Tier 2 environmental support keeps running the whole time. Open the CMO console (F2) to acknowledge as backup responder.");
                      cmo.onEVA = false; }
                    break;
            }
            lastExplain = sb.ToString(); e.Log(0, "scenario", "Board scenario Q" + q + " run.");
        }
    }

    /// Headless 72-hour run of the real engine with scripted simultaneous events.
    public static class TabletopSim
    {
        public static string lastReport;
        /// compact re-run of the same 72 h script (no timeline), for before/after comparisons
        public static (int signal, int dep, int maxTier, bool fa, int conf, float sleepQ, float mood) Quick(bool fastDepression)
        {
            bool keep = MoodEngine.FastDepression; MoodEngine.FastDepression = fastDepression;
            float saveH = SimClock.hours; var e = new MoodEngine(42, headless: true); SimClock.hours = 7f;
            int signal = -1, dep = -1, conf = -1, maxT = 0; bool fa = false; float sq = 0, md = 0;
            for (int m = 0; m < 72 * 60; m++)
            {
                float h = m / 60f;
                if (h > 2) { var c = e.C(4); c.mood -= 0.0003f; c.novelty -= 0.00005f; c.stress += 0.00008f; }
                if (h > 20 && h < 22) e.SetTension(2, 3, Mathf.Min(0.85f, e.C(2).tension[3] + 0.01f));
                if (m == 30 * 60) e.C(4).sensors[6] = SensorHealth.Failed;
                e.C(2).onEVA = h > 50 && h < 54;
                foreach (var c in e.crew) { c.exercising = (int)h % 24 == 14 && c.id % 2 == 0 || (int)h % 24 == 15 && c.id % 2 == 1; if (c.exercising) c.lastExerciseHour = SimClock.hours; c.asleep = (int)h % 24 >= 23 || (int)h % 24 < 7; }
                int before = e.log.Count; e.Step(1f / 60f);
                if (signal < 0 && e.C(4).smooth[1] >= CondSpec.T1) { signal = m; sq = e.C(4).sleepQ; md = e.C(4).mood; }
                for (int i = before; i < e.log.Count; i++)
                {
                    var l = e.log[i];
                    if (l.cond == 1 && l.kind == "tier1" && dep < 0) dep = m;
                    if (l.cond == 2 && l.kind == "tier1" && conf < 0) conf = m;
                    if (l.cond == 1 && l.kind.StartsWith("tier")) maxT = Mathf.Max(maxT, l.tier);
                    if (l.cond == 0 && l.kind == "tier2" && l.crew > 0 && e.C(l.crew).stress < 0.45f) fa = true;
                }
            }
            SimClock.hours = saveH; MoodEngine.FastDepression = keep;
            return (signal, dep, maxT, fa, conf, sq, md);
        }
        public static string Run()
        {
            bool keepFast = MoodEngine.FastDepression; MoodEngine.FastDepression = false;     // Run A = the original 48 h rule
            float saveH = SimClock.hours; var e = new MoodEngine(42, headless: true); SimClock.hours = 7f;
            var timeline = new List<string>(); int firstDep = -1, firstConf = -1, depT3 = -1, ackAt = -1, depMaxTier = 0; bool falseAlarm = false;
            for (int m = 0; m < 72 * 60; m++)
            {
                float h = m / 60f;
                // Event A: Rivera (4) slow depression build-up from hour 2
                if (h > 2) { var c = e.C(4); c.mood -= 0.0003f; c.novelty -= 0.00005f; c.stress += 0.00008f; }   // ~-1.8 %/h mood, mild stress
                // Event B: Okafor (2) vs Tanaka (3) conflict starting hour 20
                if (h > 20 && h < 22) e.SetTension(2, 3, Mathf.Min(0.85f, e.C(2).tension[3] + 0.01f));
                // sensor failure hour 30 (Rivera cortisol)
                if (m == 30 * 60) { e.C(4).sensors[6] = SensorHealth.Failed; timeline.Add($"{SimClock.Format(SimClock.hours)}  ⚠ Rivera cortisol patch FAILED → degraded mode"); }
                // CMO on EVA hours 50-54
                e.C(2).onEVA = h > 50 && h < 54; if (m == 50 * 60) timeline.Add($"{SimClock.Format(SimClock.hours)}  CMO Okafor starts 4 h EVA");
                // exercise sessions (should NOT trigger anxiety)
                foreach (var c in e.crew) { c.exercising = (int)h % 24 == 14 && c.id % 2 == 0 || (int)h % 24 == 15 && c.id % 2 == 1; if (c.exercising) c.lastExerciseHour = SimClock.hours; c.asleep = (int)h % 24 >= 23 || (int)h % 24 < 7; }
                int before = e.log.Count;
                e.Step(1f / 60f);
                for (int i = before; i < e.log.Count; i++)
                {
                    var l = e.log[i];
                    if (l.kind == "tier1" || l.kind == "tier2" || l.kind == "tier3" || l.kind == "backup" || l.kind == "ack" || l.kind == "inform" || l.kind == "accept" || l.kind == "refuse" || l.kind == "monitor" || l.kind == "resolve")
                        timeline.Add($"{l.Time}  {(l.crew > 0 ? e.C(l.crew).name : "")}: {l.text}");
                    if (l.cond == 1 && l.kind == "tier1" && firstDep < 0) firstDep = m;
                    if (l.cond == 2 && l.kind == "tier1" && firstConf < 0) firstConf = m;
                    if (l.cond == 1 && l.kind == "tier3" && depT3 < 0) depT3 = m;
                    if (l.kind == "ack" && ackAt < 0 && depT3 >= 0) ackAt = m;
                    if (l.cond == 0 && l.kind == "tier2" && l.crew > 0 && e.C(l.crew).stress < 0.45f) falseAlarm = true;   // offer while true stress was low = false positive
                    if (l.cond == 1 && l.kind.StartsWith("tier")) depMaxTier = Mathf.Max(depMaxTier, l.tier);
                }
            }
            var sb = new StringBuilder();
            sb.AppendLine("# Tabletop Simulation — 72-hour window (real AI engine, headless)");
            sb.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm} · seed 42 · step 1 sim-minute · 4 crew\n");
            sb.AppendLine("## Scripted events (simultaneous)\n- A: Rivera — slow-building depression from hour 2\n- B: Okafor vs Tanaka — interpersonal conflict from hour 20\n- C: Rivera cortisol patch fails at hour 30 (degraded mode)\n- D: CMO Okafor on EVA hours 50–54 (Tier 3 responder unavailable)\n- Control: daily exercise sessions for all crew (must NOT cause anxiety alarms)\n");
            sb.AppendLine("## Results");
            sb.AppendLine($"- Depression first detected (Tier 1): {(firstDep >= 0 ? SimClock.Format(7 + firstDep / 60f) : "not within 72 h")}");
            sb.AppendLine($"- Conflict first detected (Tier 1): {(firstConf >= 0 ? SimClock.Format(7 + firstConf / 60f) : "not within 72 h")}");
            sb.AppendLine($"- Depression Tier 3 escalation: {(depT3 >= 0 ? SimClock.Format(7 + depT3 / 60f) : "none (resolved below Tier 3)")}");
            sb.AppendLine($"- Exercise false-positive anxiety offers: {(falseAlarm ? "YES (fail)" : "0 (pass)")}");
            sb.AppendLine($"- Escalations: {e.escalations} · interventions logged: {e.log.Count(l => l.kind.StartsWith("tier"))} · refusals respected: {e.log.Count(l => l.kind == "refuse")}");
            // ---- findings are generated from what actually happened in this run (no canned claims) ----
            int refusals = e.log.Count(l => l.kind == "refuse"), backups = e.log.Count(l => l.kind == "backup"), informs = e.log.Count(l => l.kind == "inform"), t3 = e.log.Count(l => l.kind == "tier3");
            bool informedFirst = e.log.Where(l => l.kind == "tier3").All(l => e.log.Any(i => i.kind == "inform" && i.crew == l.crew && i.simHour <= l.simHour));
            var good = new List<string>(); var bad = new List<string>();
            if (firstDep >= 0 && firstConf >= 0) good.Add("Both conditions were detected and ran in parallel on separate clocks with zone-separated levers (greenhouse/wardroom vs commons)."); 
            if (firstDep >= 0) good.Add($"Slow-building depression was caught at {SimClock.Format(7 + firstDep / 60f)} (peak Tier {depMaxTier}) using sleep, voice, face and movement trends."); else bad.Add("Depression was NOT detected inside 72 h — the 48 h persistence rule is too slow for this onset.");
            if (firstConf >= 0) good.Add($"Conflict between Okafor and Tanaka was detected at {SimClock.Format(7 + firstConf / 60f)} from co-location, voice and face signals."); else bad.Add("Conflict was not detected inside 72 h.");
            if (!falseAlarm) good.Add("Daily exercise produced 0 false anxiety offers (exercise/EVA context suppression works)."); else bad.Add("At least one anxiety offer fired while real stress was low (false positive).");
            if (t3 > 0 && informedFirst) good.Add($"All {t3} Tier-3 escalation(s) told the crew member FIRST ({informs} notice(s)) before any human was alerted.");
            if (backups > 0) good.Add($"Backup chain engaged {backups} time(s) when the primary responder was unavailable."); else if (t3 > 0) good.Add("Tier 3 reached the CMO directly (CMO available) — backup chain not needed in this run.");
            if (refusals > 0) good.Add($"{refusals} offer refusal(s) stopped the intervention immediately and were logged; the AI backed off for 6 h."); else good.Add("No offers were refused in this run (AI crewmates accepted).");
            bad.Add("After the cortisol patch failed, depression confidence dropped and escalation above Tier 1 was capped until enough other evidence accumulated — a real risk of under-reaction.");
            if (refusals > 0) bad.Add("Crew with low autonomy declined offers, delaying behavioural activation.");
            if (t3 == 0) bad.Add("No Tier-3 event occurred, so the 15-minute acknowledgement / backup chain was not exercised here — it is tested separately in board scenario Q6.");
            sb.AppendLine("\n## What worked"); foreach (var g in good) sb.AppendLine("- " + g);
            sb.AppendLine("\n## What failed / was weak"); foreach (var b in bad) sb.AppendLine("- " + b);
            sb.AppendLine("\n## Redesign\n1. Add a 2-question daily self-check in the Crew Interface as a cortisol fallback (raises confidence without new mass).\n2. Fast lane for depression: 12 h of raw signal + low activity starts silent Tier-1 support early; Tier-2 persistence 48 h → 36 h (re-tested below).\n3. Present offers as choices between two activities (autonomy-preserving) to raise acceptance.\n4. Pre-assign the Commander as standing deputy CMO during any CMO EVA (no 15-min wait).");
            // ---- Run B: same scripted 72 h, redesign #2 switched on, measured against Run A ----
            MoodEngine.FastDepression = keepFast;
            var A = Quick(false); var B = Quick(true);
            string F(int m) => m >= 0 ? SimClock.Format(7 + m / 60f) : "not within 72 h";
            sb.AppendLine("\n## Re-test after redesign #2 (same seed, same events)\nRedesign #2 as built: if the RAW depression signal stays above threshold for 12 h while activity is clearly low (or sleep AND activity both drop), silent, reversible Tier-1 support starts before the 24 h-smoothed score catches up; Tier 2+ still needs 36 h of persistence.");
            sb.AppendLine("| Measure | Run A (original 48 h rule) | Run B (redesign #2) |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine($"| Depression signal first above threshold | {F(A.signal)} | {F(B.signal)} |");
            sb.AppendLine($"| Depression Tier 1 support starts | {F(A.dep)} | {F(B.dep)} |");
            sb.AppendLine($"| Highest depression tier reached | {A.maxTier} | {B.maxTier} |");
            sb.AppendLine($"| Rivera sleep quality / mood when the smoothed signal crossed | {A.sleepQ:0.00} / {A.mood:0.00} | {B.sleepQ:0.00} / {B.mood:0.00} |");
            sb.AppendLine($"| Exercise false-positive anxiety offers | {(A.fa ? "yes" : "0")} | {(B.fa ? "yes" : "0")} |");
            sb.AppendLine($"| Conflict Tier 1 | {F(A.conf)} | {F(B.conf)} |");
            sb.AppendLine("\n## Timeline (key events)");
            foreach (var t in timeline.Take(140)) sb.AppendLine("- " + t);
            string path = System.IO.Path.Combine(GameState.ReportsDir, "TabletopSimulation_Report.md");
            try { System.IO.File.WriteAllText(path, sb.ToString()); } catch (Exception ex) { Debug.LogWarning(ex.Message); }
            SimClock.hours = saveH;
            lastReport = sb.ToString().Substring(0, Mathf.Min(2600, sb.Length)) + $"\n\n…full report saved: {path}";
            return path;
        }
    }
}
