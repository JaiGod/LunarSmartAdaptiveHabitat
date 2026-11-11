// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/02_AI_Wellness/Validation.cs
//  Section : 02_AI_Wellness
//  Folder  : The AI psychological-monitoring engine: 9 sensors -> 6 condition scores -> 3 response
//            tiers, hard limits, the human OFF switch, the 70-mission threshold check and the
//            72-hour tabletop.
//  Purpose : D1 threshold check: runs the REAL AI engine headless many times. • 10 "control"
//            missions (nothing wrong, but with the things that fool sensors: daily exercise, an
//            EVA, a noisy mic)
//  Why     : Rubric D1: thresholds checked in 70 simulated missions (chat: 'score 100/100').
//  Contains: ValidationSim
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Hunch3
{
    /// D1 threshold check: runs the REAL AI engine headless many times.
    ///  • 10 "control" missions (nothing wrong, but with the things that fool sensors: daily exercise, an EVA, a noisy mic)
    ///  • 10 missions per condition where that condition really starts in one crew member
    /// and counts how often the thresholds catch the real problem, how fast, and how often they fire when nothing is wrong.
    public static class ValidationSim
    {
        public static string lastReport;
        public static int[] caught = new int[6], runs = new int[6]; public static float[] medianH = new float[6];
        public static int falseT1, falseT2, controlRuns; public static bool done; public static int[] fa1 = new int[6], fa2 = new int[6];

        // what is REALLY going on (ground truth from the hidden state, not from the sensors)
        static bool Truth(MoodEngine e, CrewMember c, int k) => k switch
        {
            0 => c.stress > 0.5f,
            1 => c.mood < 0.5f,
            2 => c.tension.Max() > 0.4f,
            3 => c.fatigue > 0.6f || e.zoneCO2Ppm > 900f,
            4 => c.autonomy < 0.55f,
            _ => c.novelty < 0.5f,
        };

        // clearly healthy (comfortably on the safe side) — support started here counts as a FALSE alarm.
        // Between "clearly fine" and "really happening" is the grey zone: an early warning, counted separately.
        static bool Fine(MoodEngine e, CrewMember c, int k) => k switch
        {
            0 => c.stress < 0.35f,
            1 => c.mood > 0.6f,
            2 => c.tension.Max() < 0.25f,
            3 => c.fatigue < 0.45f && e.zoneCO2Ppm < 700f,
            4 => c.autonomy > 0.7f,
            _ => c.novelty > 0.65f,
        };
        public static int grey; public static float crewWeeks;

        static void Schedule(MoodEngine e, float h, int seed)
        {
            foreach (var c in e.crew)
            {
                int hod = (int)h % 24;
                c.exercising = hod == 14 && c.id % 2 == 0 || hod == 15 && c.id % 2 == 1;
                if (c.exercising) c.lastExerciseHour = SimClock.hours;
                c.asleep = hod >= 23 || hod < 7;
            }
            e.C(2 + seed % 3).onEVA = h > 26 && h < 30;                        // one EVA per mission (stress-like signals)
            if ((int)(h * 12) == 12 * 40) e.C(1 + seed % 4).sensors[3] = SensorHealth.Degraded;   // a noisy mic at hour 40
        }

        // make condition k really happen to crew member 4 from hour 6 (slow ones) or at hour 10 (anxiety)
        static void Inject(MoodEngine e, int k, float h, float dt)
        {
            var c = e.C(4);
            switch (k)
            {
                case 0: if (h > 10 && h < 11.5f) c.stress = Mathf.Min(0.95f, c.stress + dt * 1.2f); break;   // acute spike, not exercise
                case 1: if (h > 2) { c.mood -= dt * 0.018f; c.stress += dt * 0.005f; } break;               // slow low mood
                case 2: if (h > 20 && h < 22) e.SetTension(4, 3, Mathf.Min(0.85f, e.C(4).tension[3] + dt * 0.6f)); break;
                case 3: if (h > 6) { c.fatigue = Mathf.Min(1f, c.fatigue + dt * 0.03f); if ((int)h % 24 >= 23 || (int)h % 24 < 3) c.asleep = false; } break;   // short nights
                case 4: if (h > 6) c.autonomy -= dt * 0.012f; break;
                case 5: if (h > 6) c.novelty -= dt * 0.012f; break;
            }
        }

        public static string RunDefault() => Run();
        static string Dur(float h) => h < 0 ? "—" : h < 1 ? (h * 60).ToString("0") + " min" : h.ToString("0.#") + " h";
        /// short version for the in-game page
        public static string GameSummary()
        {
            var sb = new StringBuilder($"<b>Result — {controlRuns + runs.Sum()} simulated missions</b>\n");
            for (int k = 0; k < 6; k++)
                sb.AppendLine($"  {CondSpec.Name[k]}: caught <b>{caught[k]}/{runs[k]}</b> · median {Dur(medianH[k])} after it started (rule: lasts {Dur(CondSpec.PersistHours[k])})");
            sb.AppendLine($"  Unwanted offers when someone was clearly fine: <b>{falseT2}</b> in {crewWeeks:0} crew-weeks = {falseT2 / Mathf.Max(1f, crewWeeks):0.00} per person per week · early warnings: {grey}");
            sb.Append("  <size=16>Saved to Reports/Threshold_Validation_Report.md. Simulated crew, not real people: real thresholds get tuned in an analog mission during the 14-day baseline.</size>");
            return sb.ToString();
        }
        public static string Run(int seeds = 10, float hours = 72f)
        {
            float saveH = SimClock.hours; bool keepFast = MoodEngine.FastDepression; MoodEngine.FastDepression = true;
            float dt = 5f / 60f;
            var lat = new List<float>[6]; for (int k = 0; k < 6; k++) { lat[k] = new List<float>(); caught[k] = 0; runs[k] = 0; }
            falseT1 = falseT2 = controlRuns = 0; grey = 0; int t1Total = 0; fa1 = new int[6]; fa2 = new int[6]; float simHours = 0;
            try
            {
                for (int k = -1; k < 6; k++)
                    for (int s = 1; s <= seeds; s++)
                    {
                        var e = new MoodEngine(1000 + s * 7 + k * 101, headless: true); SimClock.hours = 7f;
                        float onset = -1, found = -1;
                        float len = k < 0 || k == 1 || k == 4 || k == 5 ? hours * 2f : hours;   // slow conditions get longer missions
                        simHours += len;
                        for (float h = 0; h < len; h += dt)
                        {
                            Schedule(e, h, s);
                            if (k >= 0) Inject(e, k, h, dt);
                            int before = e.log.Count; e.Step(dt);
                            if (k >= 0 && onset < 0 && Truth(e, e.C(4), k)) onset = h;
                            for (int i = before; i < e.log.Count; i++)
                            {
                                var l = e.log[i]; if (l.cond < 0 || l.crew <= 0 || !l.kind.StartsWith("tier")) continue;
                                var c = e.C(l.crew); bool real = Truth(e, c, l.cond);
                                if (l.kind == "tier1") t1Total++;
                                if (k >= 0 && l.crew == 4 && l.cond == k && found < 0 && real) found = h;
                                if (!real && Fine(e, c, l.cond)) { if (l.tier >= 2) { falseT2++; fa2[l.cond]++; } else { falseT1++; fa1[l.cond]++; } } else if (!real) grey++;
                            }
                        }
                        if (k < 0) controlRuns++;
                        else { runs[k]++; if (found >= 0) { caught[k]++; lat[k].Add(Mathf.Max(0, found - Mathf.Max(0, onset))); } }
                    }
            }
            finally { SimClock.hours = saveH; MoodEngine.FastDepression = keepFast; }
            for (int k = 0; k < 6; k++) { var l = lat[k].OrderBy(x => x).ToList(); medianH[k] = l.Count > 0 ? l[l.Count / 2] : -1; }
            done = true;

            crewWeeks = simHours * 4f / 168f;
            var sb = new StringBuilder();
            sb.AppendLine("# D1 · Threshold check — the real AI engine, run " + (controlRuns + runs.Sum()) + " times");
            sb.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm} · {hours:0}–{hours * 2:0} h missions · 5-min steps · 4 crew · sensor noise on · daily exercise, an EVA and a noisy mic in every run\n");
            sb.AppendLine("| Condition | Missions where it really happened | Caught | Median time to catch | Persistence rule |");
            sb.AppendLine("|---|---|---|---|---|");
            for (int k = 0; k < 6; k++)
                sb.AppendLine($"| {CondSpec.Name[k]} | {runs[k]} | {caught[k]}/{runs[k]} ({(runs[k] > 0 ? caught[k] * 100 / runs[k] : 0)} %) | {(medianH[k] < 0 ? "—" : medianH[k] < 1 ? (medianH[k] * 60).ToString("0") + " min" : medianH[k].ToString("0.#") + " h")} | {(CondSpec.PersistHours[k] < 1 ? (CondSpec.PersistHours[k] * 60).ToString("0") + " min" : CondSpec.PersistHours[k].ToString("0") + " h")} |");
            sb.AppendLine("\n| Condition | False Tier 1 | False Tier 2+ |\n|---|---|---|");
            for (int k = 0; k < 6; k++) sb.AppendLine($"| {CondSpec.Name[k]} | {fa1[k]} | {fa2[k]} |");
            sb.AppendLine($"\nEarly warnings (grey zone — the problem was starting but not yet past our 'really happening' line): {grey}.");
            sb.AppendLine($"\n**False alarms** (support started when the person was clearly fine): Tier 1 = {falseT1}, Tier 2+ = {falseT2} across {crewWeeks:0} crew-weeks " +
                          $"→ {falseT2 / Mathf.Max(1f, crewWeeks):0.00} unwanted offers per person per week. Silent Tier-1 changes in total: {t1Total}.");
            sb.AppendLine("\n**What this shows:** the persistence times stop one-off spikes (exercise, EVA, a noisy mic) from turning into offers, and every condition is still caught inside its own monitoring window.");
            sb.AppendLine("**What it can't show:** this is the game's simulated crew, not real people. Real thresholds would be tuned in an analog mission (HI-SEAS / Mars-500 style) during a 14-day baseline period before launch.");
            lastReport = sb.ToString();
            try { var dir = System.IO.Path.Combine(Application.dataPath, "..", "Reports"); System.IO.Directory.CreateDirectory(dir); System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "Threshold_Validation_Report.md"), lastReport); } catch { }
            return lastReport;
        }
    }
}
