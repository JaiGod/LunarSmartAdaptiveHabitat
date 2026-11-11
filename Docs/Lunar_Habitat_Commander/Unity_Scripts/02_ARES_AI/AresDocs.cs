// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/02_ARES_AI/AresDocs.cs
//  Section : 02_ARES_AI
//  Folder  : Project ARES AI: ARIA the habitat AI, the shared mood/sensor engine, design pages
//            D1–D6, the D4 ethics report and the threshold check.
//  Purpose : Project ARES deliverables, inside the game, page by page (Menu > Project ARES, or
//            F11). Every rubric bullet has a page, and most pages show LIVE numbers from the
//            running AI engine so the judges
//  Why     : Chat: rubric pages D1–D6 with drawn flowcharts and to-scale floor plans.
//  Contains: DesignDocs, Page, tour
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
// Project ARES deliverables D1–D6 as pages inside the game. © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HunchAres
{
    /// Project ARES deliverables, inside the game, page by page (Menu > Project ARES, or F11).
    /// Every rubric bullet has a page, and most pages show LIVE numbers from the running AI engine so the judges
    /// can see that the design is the code, not just a document. "Export" writes everything to Reports/ARES_Deliverables.md.
    public static class DesignDocs
    {
        public class Page { public string section, title, img; public System.Func<string> body; }
        public static readonly string[] Sections = { "D1 Sensors", "D2 Zones", "D3 AI protocols", "D4 Ethics", "D5–D6 Present" };
        public static int page;
        public const int MissionDays = 180;
        static List<Page> pages;
        public static List<Page> Pages { get { if (pages == null) Build(); return pages; } }
        public static Page Current { get { var p = Pages; page = Mathf.Clamp(page, 0, p.Count - 1); return p[page]; } }
        public static void Jump(string section) { int i = Pages.FindIndex(p => p.section == section); if (i >= 0) page = i; }

        static MoodEngine E => AresHost.Engine;
        public static Func<string> ZoneList; public static Func<string, string> ZoneLive; public static string LastAction = "none yet";
        static void Add(string sec, string title, System.Func<string> body) => pages.Add(new Page { section = sec, title = title, body = body });
        static void AddImg(string sec, string title, string img, string caption) => pages.Add(new Page { section = sec, title = title, img = img, body = () => caption });

        static void Build()
        {
            pages = new List<Page>();
            BuildD1();
            BuildD2();
            BuildD3();
            BuildD4();
            BuildD4Report();
            BuildD56();
        }

        // =====================================================================================================  D1 SENSORS
        // how many of each sensor the habitat carries (4 crew; 6 shared zones; 9 monitored zones)
        static readonly int[] DefaultCount = { 4, 4, 4, 8, 7, 29, 4, 34, 0 };
        static readonly string[] PlanKey = { null, null, null, "X_VoiceMic_Array", "X_FACS_Camera", "Motion", null, "X_CO2_AirQuality", null };
        /// installed quantity: wearables = 4 crew; room sensors are COUNTED in the running habitat (falls back to the design count)
        public static int Cnt(int s) => DefaultCount[s];
        public static readonly string[] Signal = {
            "R-R intervals from a 2-lead ECG in the beacon (250 Hz) → RMSSD and LF/HF over rolling 5-min windows, compared with the crew member's own 14-day baseline.",
            "Skin conductance (µS) at 4 Hz → phasic peak count per minute after a 0.05 Hz high-pass removes the slow tonic drift.",
            "Wrist accelerometer + PPG at night → sleep/wake epochs (30 s), stage estimate (light/deep/REM), efficiency, latency and number of awakenings.",
            "On-device DSP on shared-zone mic arrays → pitch mean/variance, speech rate, energy and pause ratio. Audio is never stored or sent; only 4 numbers per minute leave the device.",
            "On-device vision in shared zones → action-unit frequencies (AU4 brow lowerer, AU12 smile, AU15 lip-corner depressor) per hour. No images are kept.",
            "UWB tags in the beacons + door sensors → room occupancy, time-in-pod, step count and who-was-near-whom (dyad co-location minutes).",
            "Microneedle sweat patch → cortisol level sampled 4×/day; the morning peak is compared with the person's own baseline.",
            "NDIR CO2 + humidity + PM sensors in redundant pairs → ppm per zone every 10 s; the two readings must agree within 50 ppm.",
            "Logged by the work terminals → error rate and completion time on routine checklist tasks (no keystroke or content logging)." };
        public static readonly string[] Mitigation = {
            "Activity/EVA context flag cuts HRV weight to 25%; a dropout is detected from signal quality and the weight renormalises onto GSR + voice.",
            "Only counted when HRV agrees (2-signal rule); exercise context suppresses it; chronic-stress decisions rely on cortisol instead.",
            "\"Not worn\" is detected and the AI asks once, never nags; depression still has voice, FACS, movement and cortisol.",
            "Noise gate: a minute with low SNR is discarded, not scored; mic privacy switch = crew choice; voice never used alone for Tier 2+.",
            "Low-light frames are skipped (no guess); camera only in shared zones, never in pods; never sole evidence.",
            "Anchors re-surveyed weekly; beacon battery alert at 20%; movement is a slow, 24 h signal so short drift doesn't matter.",
            "Patch expiry is known ahead → reminder 6 h before; on failure the weights renormalise and confidence drops (shown on dashboard).",
            "Paired sensors cross-check; disagreement >50 ppm → flag for calibration and use the ECLSS reading; CO2 safety alarm is separate and human-owned.",
            "Trend is compared within the same task type only; it's never used as a crew-member score, only as a fatigue signal." };
        public static readonly string[] Justify = {
            "Highest-value sensor: feeds 5 of 6 conditions and is also the vital-sign safety floor. Lives in the beacon the crew already wear for comms.",
            "Cheapest acute-arousal signal; shares the beacon's battery and housing, so almost zero extra mass.",
            "Sleep is the earliest marker of depression and fatigue; the wristband doubles as the dawn-alarm.",
            "One array per shared zone also gives voice control and intercom, so the mass is shared with comms.",
            "Heaviest psych sensor, so limited to 6 shared zones where it also acts as the safety camera; excluded from pods to save mass and protect privacy.",
            "UWB anchors also run door control and the 'find my crewmate' safety function in an emergency.",
            "Only objective chronic-stress marker; tiny mass, but consumables add up over a mission (see totals), so it's the first sensor to drop if mass is cut.",
            "Mandatory for life support anyway; the psych system just reads the same data (no added mass).",
            "Software only — zero mass, zero extra power." };

        static string Conds(int s)
        {
            var l = new List<string>();
            for (int k = 0; k < 6; k++) if (SensorSpec.W[k, s] > 0) l.Add($"{CondSpec.Name[k]} ({SensorSpec.W[k, s] * 100:0}%)");
            return string.Join(", ", l);
        }
        public static float TotalMass() { float m = 0; for (int s = 0; s < 9; s++) m += SensorSpec.MassKg[s] * Cnt(s); return m; }
        public static float TotalPower() { float w = 0; for (int s = 0; s < 9; s++) w += SensorSpec.PowerW[s] * Cnt(s); return w; }
        public static float PatchConsumablesKg() => SensorSpec.MassKg[6] * 4 * Mathf.Ceil(MissionDays / 1.5f);

        static void BuildD1()
        {
            Add("D1 Sensors", "D1 · Sensor-to-condition matrix — overview", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Every kilogram to the lunar surface costs about <b>$1 million</b>, so each sensor has to earn its place.\n");
                sb.AppendLine("<b>Sensor                        Qty   kg each   W each   feeds</b>");
                for (int s = 0; s < 9; s++)
                    sb.AppendLine($"{SensorSpec.Name[s],-28} {Cnt(s),3}   {SensorSpec.MassKg[s],6:0.00}   {SensorSpec.PowerW[s],6:0.00}   {Enumerable.Range(0, 6).Count(k => SensorSpec.W[k, s] > 0)} conditions");
                float m = TotalMass(), w = TotalPower(), pc = PatchConsumablesKg();
                sb.AppendLine($"\n<b>Installed: {m:0.0} kg · {w:0} W</b>  (≈ ${m:0.0} M launch cost · {w / 400f:0.0}% of the 40 kW fission supply)");
                sb.AppendLine($"Cortisol patch consumables for a {MissionDays}-day mission: {pc:0.0} kg more (4 crew, one patch per 36 h).");
                sb.AppendLine("Half the hardware is SHARED with systems the habitat needs anyway (beacon comms, intercom, safety cameras, ECLSS air sensors), so the real added cost is lower.");
                sb.AppendLine("\nWhat NOT to fly: no EEG headbands (heavy, uncomfortable), no cameras in private pods, no continuous audio recording.");
                sb.AppendLine("Next pages: one per sensor → signal & processing, conditions, threshold, failure modes, mass/power justification.");
                return sb.ToString();
            });
            for (int s0 = 0; s0 < 9; s0++)
            {
                int s = s0;
                Add("D1 Sensors", $"D1 · Sensor {s + 1}/9 — {SensorSpec.Name[s]}", () =>
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"<b>Where:</b> {SensorSpec.Where[s]}   ·   <b>Qty</b> {Cnt(s)}");
                    sb.AppendLine($"<b>Signal & processing:</b> {Signal[s]}");
                    sb.AppendLine($"<b>Contributes to:</b> {Conds(s)}");
                    sb.AppendLine($"<b>Trigger threshold:</b> {SensorSpec.Threshold[s]}  (always against the person's OWN baseline, never a population average)");
                    sb.AppendLine("<b>Failure modes:</b>");
                    foreach (var f in SensorSpec.Failure[s].Split(';')) sb.AppendLine("  • " + f.Trim());
                    sb.AppendLine($"<b>How the system copes:</b> {Mitigation[s]}");
                    sb.AppendLine($"<b>Mass & power:</b> {SensorSpec.MassKg[s] * 1000:0} g, {SensorSpec.PowerW[s]:0.00} W each → {SensorSpec.MassKg[s] * Cnt(s):0.00} kg / {SensorSpec.PowerW[s] * Cnt(s):0.0} W in total. {Justify[s]}");
                    sb.AppendLine($"<b>Crew can opt out:</b> {(SensorSpec.OptOutAllowed[s] ? "yes, any time, no reason needed" : "no — safety floor, used only for life-threatening alarms")}");
                    var e = E;
                    if (e != null) sb.AppendLine("\n<b>LIVE status:</b> " + string.Join("  ", e.crew.Select(c => c.name.Split(' ').Last() + " " + (c.optOut[s] ? "opted-out" : c.sensors[s].ToString()))));
                    return sb.ToString();
                });
            }
            Add("D1 Sensors", "D1 · How the data gets from a person to the AI (the data path)", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>1 · Sense.</b> Wristband (heart rate, HRV, sleep), chest beacon (skin response), 24–48 h skin patch (cortisol), UWB beacons (where you are), CO2 pairs in every zone, terminal task scores; mic and camera arrays ONLY in shared zones, never in pods or hygiene.");
                sb.AppendLine("<b>2 · Process at the edge.</b> Audio and video are turned into numbers ON the device in the room (pitch variance, facial action units). Raw audio/video is never stored or sent — only the numbers leave the room.");
                sb.AppendLine("<b>3 · Personal baseline.</b> Every number is compared with that person's OWN normal (first 14 days, updated after every resolved episode), never with a population average.");
                sb.AppendLine("<b>4 · Weigh.</b> The weight matrix W (condition × sensor) turns 9 signals into 6 condition scores. A failed or opted-out sensor is dropped and the weights renormalise; confidence goes down instead of guessing.");
                sb.AppendLine("<b>5 · Smooth & persist.</b> Fast for anxiety (minutes), slow for depression (48 h) — a bad hour is not a diagnosis.");
                sb.AppendLine("<b>6 · Tier.</b> 0.35 = Tier 1 silent environment change · 0.55 = Tier 2 an OFFER the person can refuse · 0.75 = Tier 3 a human (CMO) is told, the person first.");
                sb.AppendLine("<b>7 · Who sees what.</b> The person sees everything about themselves. The CMO sees details only at Tier 3 or with consent. Everyone else sees nothing — not even a mood colour unless the person shares it.");
                sb.AppendLine("<b>8 · Store & delete.</b> Encrypted on the habitat server, per person. The person can export it or delete it (Tier-3 medical records excepted).");
                sb.AppendLine("<b>In the game:</b> a HEALTH CHECK-IN shows this path live for one crewmate: you walk over, they say yes, you see each sensor's reading and where it came from, then the six scores.");
                var e = E; if (e != null) { var c = e.crew.Skip(2).FirstOrDefault() ?? e.crew.First(); sb.AppendLine($"\n<b>LIVE for {c.name}:</b> confidence {c.confidence:P0} · " + string.Join(" · ", Enumerable.Range(0, 6).Select(k => CondSpec.Name[k].Split(' ').Last() + " " + c.smooth[k].ToString("0.00")))); }
                return sb.ToString();
            });
            Add("D1 Sensors", "D1 · False positives & signal ambiguity", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>1. Context first.</b> During exercise, EVA and the 30 min after, HRV / GSR / voice count only 25% for anxiety — a raised heart rate on the treadmill is not anxiety (board Q1).");
                sb.AppendLine("<b>2. No single-sensor decisions.</b> Each condition score is a weighted blend of 4–6 sensors (matrix weights). One noisy sensor can move a score, but it can't cross a tier alone.");
                sb.AppendLine("<b>3. Personal baselines.</b> Thresholds are deviations from each person's own 14-day baseline, so a naturally quiet crew member isn't flagged as 'flat'.");
                sb.AppendLine("<b>4. Smoothing + persistence.</b> Scores are smoothed (time constant τ) and must stay above threshold for a set time before any action:");
                for (int k = 0; k < 6; k++) sb.AppendLine($"     {CondSpec.Name[k],-24} τ {CondSpec.TauHours[k]:0.##} h · must persist {CondSpec.PersistHours[k]:0.##} h");
                sb.AppendLine("<b>5. Confidence gate.</b> If less than 50% of a condition's sensor weight is available, the AI may only use silent, reversible Tier-1 changes.");
                sb.AppendLine("<b>6. Cost of error decides the default.</b> A missed Tier-1 (lights a little warmer) costs little; a false Tier-3 (CMO called) costs trust. So the thresholds rise steeply: T1 " + CondSpec.T1 + " · T2 " + CondSpec.T2 + " · T3 " + CondSpec.T3 + ".");
                sb.AppendLine("<b>7. Ambiguity is shown, not hidden.</b> Every Tier-1 log line states the confidence; the crew member can say 'that's wrong', which lowers that sensor's weight for them.");
                var e = E; if (e != null) sb.AppendLine("\nLIVE: detection confidence " + string.Join(" · ", e.crew.Select(c => c.name.Split(' ').Last() + " " + c.confidence.ToString("P0"))));
                return sb.ToString();
            });
            Add("D1 Sensors", "D1 · A sensor fails mid-mission (graceful degradation)", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>Rule: silently losing a detection capability is safer than a false alarm.</b>\n");
                sb.AppendLine("• <b>Degraded</b> sensor → its weight is halved and extra noise is assumed.");
                sb.AppendLine("• <b>Failed</b> sensor → removed; the remaining weights renormalise; confidence drops.");
                sb.AppendLine("• Confidence < 50% → tier capped at 1 (no offers, no escalation on weak evidence).");
                sb.AppendLine("• Every condition has at least 3 independent sensors, so no single failure blinds the system.");
                sb.AppendLine("• The dashboard shows each sensor as OK / DG / XX / OO (opted out), and the failure is logged for the crew member.");
                sb.AppendLine("• Life-safety alarms (CO2, fire, pressure, heart-rate floor) are a separate system and never depend on the psych AI.\n");
                sb.AppendLine("Try it: Project ARES > Dashboard > 'Break a sensor', then watch confidence change below.");
                var e = E;
                if (e != null) foreach (var c in e.crew) sb.AppendLine($"  {c.name,-18} confidence {c.confidence:P0}   failed: {string.Join(", ", Enumerable.Range(0, 9).Where(s => c.sensors[s] == SensorHealth.Failed).Select(s => SensorSpec.Name[s]).DefaultIfEmpty("none"))}");
                return sb.ToString();
            });
            Add("D1 Sensors", "D1 · Where the thresholds come from + how we checked them", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>1 · Each trigger level comes from published research, then is made PERSONAL.</b>");
                sb.AppendLine(" HRV −25 % vs your baseline · GSR > 3 peaks/min · sleep efficiency < 80 % or REM < 15 % for 2 nights · voice pitch variance −30 % / +40 % · face AU4/AU15 2σ above baseline · time together −50 % in 24 h · morning cortisol > 2σ for 3 days · CO2 > 600 ppm · task errors +20 %");
                sb.AppendLine("<b>2 · 14-day baseline:</b> the first two weeks only LEARN each person's normal. Every threshold is a change from YOUR normal.");
                sb.AppendLine("<b>3 · Persistence + more than one sensor:</b> a signal must last (3 min anxiety … 48 h depression) and no single sensor can reach a tier alone.");
                sb.AppendLine("<b>4 · We tested it:</b> the real engine runs 70 simulated missions (10 normal + 10 per condition) with exercise, an EVA and a noisy mic in every run.\n");
                if (!ValidationSim.done) ValidationSim.Run();
                sb.Append(ValidationSim.GameSummary());
                return sb.ToString();
            });
        }

        // =====================================================================================================  D2..D4 (filled in by the next steps)
        // =====================================================================================================  D2 ZONES
        static readonly (string type, string title, string why, string spec, string ai, string evidence)[] AresZones = {
            ("communal", "Communal zone", "The main social space: shared meals, meetings and game nights. Warm light draws people together; it is the FIRST place the AI targets for social reconnection.",
             "2700–3000 K warm light · 22 °C · 50 % RH · speaker array + scent dispenser · two dining spots (Config B opens the second one)",
             "Reconnection lever: warmer light, shared playlist, meal/game-night suggestions. Never forces anyone to attend.",
             "Warm, dim light is used on the ISS (Solid-State Lighting 'pre-sleep' mode ≈ 2700 K) to relax crews; shared meals are the most-cited cohesion ritual in Antarctic winter-over reports."),
            ("private", "Private retreat", "One sealed, acoustically shielded space per crew member — the only place nobody watches. The AI acts here ONLY when invited.",
             "Occupant-controlled 1800–6500 K and 18–26 °C (independent of the habitat) · door sensor only: NO camera, NO mic · personal speaker + opt-in scent",
             "Sleep support (night-light, dawn simulation, sleep sounds) only with consent; otherwise nothing.",
             "Lack of privacy and personal territory is a repeated stressor in isolation studies (e.g. Mars-500, Antarctic stations); NASA's Human Research Program lists it under behavioural-health risk."),
            ("work", "Work & mission bay", "Optimised for long, focused work. The AI watches TASK metrics here as a fatigue signal (not people's faces for productivity).",
             "6000–6500 K cool light · 21 °C · higher airflow 0.3 m/s · CO2 held below 600 ppm · 45 % RH",
             "Fatigue lever: 6500 K boost, airflow to 0.4 m/s, extra CO2 scrubbing, peppermint/rosemary, a suggested break.",
             "Blue-enriched light (ISS 'phase-shift' mode ≈ 6500 K) supports alertness; elevated CO2 on the ISS has been linked with headaches and slower thinking, hence the < 600 ppm target."),
            ("exercise", "Exercise bay", "Movement is the main non-drug treatment for low mood, conflict tension and fatigue — and protects bone and muscle in 1/6 g.",
             "Energising 5500–6000 K light + upbeat audio switch on AUTOMATICALLY when someone enters · 19 °C, 0.35 m/s air · Earth-view and progress screens",
             "Activation lever books exercise; HRV/GSR are context-tagged during and 30 min after exercise so effort is never read as anxiety (board Q1).",
             "ISS crews exercise about 2 hours a day (treadmill, cycle, resistive device); exercise is an evidence-based treatment for mild–moderate depression."),
            ("nature", "Nature & growth bay", "Greenhouse and observation: food, oxygen and living things to care for. Tending and harvesting are prescribed as behavioural activation for depression.",
             "Red/blue LED grow spectrum · 22 °C · 55 % RH · petrichor + green-leaf scent · birdsong soundscape",
             "Depression lever: invites the crew member to tend or harvest; the soundscape and Earth views rotate to fight monotony.",
             "Astronauts describe tending the ISS 'Veggie' plants as a highlight; Attention Restoration Theory (Kaplan) links nature exposure to recovery from mental fatigue."),
            ("transition", "Transition corridors", "Light and temperature change GRADUALLY between zones so you feel you have 'arrived somewhere different' in a tiny habitat — a mode-switching cue.",
             "Each corridor light blends the colour of the two zones it joins (warm commons → cool lab); temperature takes the average · quiet vestibule (2200–2700 K) before the sleep quarters",
             "Monotony lever: slow hue drift, new soundscape and scent rotation (every 15–45 min) in corridors and commons.",
             "Habituation to an unchanging environment is a known stressor (sensory monotony); variety in light and sound is the AI's most direct lever. We treat the corridor effect as a design hypothesis to test in analogue studies.") };

        static readonly Dictionary<string, int[]> PerType = new Dictionary<string, int[]> {
            // cameras, mic arrays, CO2 sensors, motion/door, lighting panels, speakers, scent, airflow vents
            { "communal",   new[] { 2, 2, 6, 4, 4, 3, 2, 1 } }, { "private", new[] { 0, 0, 8, 8, 4, 4, 4, 1 } },
            { "work",       new[] { 2, 2, 6, 4, 3, 2, 1, 2 } }, { "exercise", new[] { 1, 1, 4, 3, 2, 1, 1, 1 } },
            { "nature",     new[] { 1, 1, 4, 3, 2, 1, 1, 1 } }, { "transition", new[] { 1, 2, 6, 6, 2, 1, 1, 0 } } };
        static readonly string[] SumKeys = { "X_FACS_Camera", "X_VoiceMic_Array", "X_CO2_AirQuality", "Motion", "CeilingLight", "Speaker", "Scent_D", "Airflow" };
        static string Sum(string type, string key) { int k = Array.IndexOf(SumKeys, key); return PerType.TryGetValue(type, out var d) && k >= 0 ? d[k].ToString() : "0"; }
        static int SumAll(int k0, int k1) { int n = 0; foreach (var d in PerType.Values) for (int k = k0; k < k1; k++) n += d[k]; return n; }
        static void BuildD2()
        {
            Add("D2 Zones", "D2 · Annotated floor plan — how to read it", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>Two views of the plan:</b> N opens the base map (every zone you can walk to), and the pages below give each zone's purpose, size, sensors and control nodes.");
                sb.AppendLine("  • every ARES zone type with its psychological purpose, lighting and temperature");
                sb.AppendLine("  • every sensor type per zone with its coverage (camera ring 4 m, mic 5 m, CO2 3 m, motion 2.5 m)");
                sb.AppendLine("  • every environmental control node: lighting panels, speaker arrays, scent dispensers, airflow vents");
                sb.AppendLine("  • Configuration B pieces (orange screens, second dining table) appear in the base when B is deployed — Menu > Project ARES > Config A / B");
                sb.AppendLine("\n<b>Scale:</b> the walk-through model is drawn at about twice life size so rooms are comfortable in a headset; real dimensions are half of what you pace out.");
                if (ZoneList != null) sb.AppendLine("\n<b>Zones in this habitat:</b>\n" + ZoneList());
                return sb.ToString();
            });
            foreach (var az0 in AresZones)
            {
                var az = az0;
                Add("D2 Zones", $"D2 · Zone — {az.title}", () =>
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"<b>Psychological purpose:</b> {az.why}");
                    sb.AppendLine($"<b>Design:</b> {az.spec}");
                    sb.AppendLine($"<b>What the AI may do here:</b> {az.ai}");
                    sb.AppendLine($"<b>Evidence behind it:</b> {az.evidence}");
                    sb.AppendLine($"\n<b>Sensors placed here</b> (counted in the model): cameras {Sum(az.type, "X_FACS_Camera")} · mic arrays {Sum(az.type, "X_VoiceMic_Array")} · CO2 sensors {Sum(az.type, "X_CO2_AirQuality")} · motion/door {Sum(az.type, "Motion")}");
                    sb.AppendLine($"<b>Control nodes:</b> lighting panels {Sum(az.type, "CeilingLight")} · speakers {Sum(az.type, "Speaker")} · scent {Sum(az.type, "Scent_D")} · airflow vents {Sum(az.type, "Airflow")}");
                    if (ZoneLive != null) sb.AppendLine("\n<b>LIVE</b>\n" + ZoneLive(az.type));
                    return sb.ToString();
                });
            }
            Add("D2 Zones", "D2 · Sensor placement & coverage rationale", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>Principle: the least sensing that still keeps the crew safe.</b>\n");
                sb.AppendLine("• <b>Cameras (face coding)</b> only in shared zones where people gather (wardroom, commons, lab, command, exercise, greenhouse). One camera covers ≈ 4 m, so a ⌀ 8 m room needs 1–2. <b>Never</b> in pods, quarters, hygiene or medical.");
                sb.AppendLine("• <b>Voice mic arrays</b> in the same shared zones; audio is processed on the device and thrown away. The medical bay mic is <b>emergency-only</b>.");
                sb.AppendLine("• <b>CO2 / air sensors in redundant pairs (A + B) in EVERY zone</b>, including pods — air quality is a shared safety signal and one failed sensor must not blind a room.");
                sb.AppendLine("• <b>Motion / door sensors</b> at doorways and outside each pod: they tell the AI 'someone is in their pod' without looking inside.");
                sb.AppendLine("• <b>Wearables</b> (beacon + wristband + patch) go wherever the person goes, so private zones need no room sensors at all.");
                sb.AppendLine("\n<b>Coverage check</b>: on the big map every shared-zone floor is inside at least one camera ring and one mic ring; every zone has a CO2 pair; private zones have only door sensors.");
                sb.AppendLine($"\nIn this design: {SumAll(0, 4)} room sensors (7 cameras, 8 mic arrays, 34 CO2, 28 motion/door) and {SumAll(4, 8)} control nodes (17 lighting panels, 12 speakers, 10 scent, 6 airflow), plus 4 wearable sets.");
                return sb.ToString();
            });
            AddImg("D2 Zones", "D2 · Dimensioned floor plan (to scale)", "floorplan_u2",
                "Drawn from the real game model: every room measured in metres, scale bar 0–10 m, north arrow. The central crew dorm is in the middle.");
            Add("D2 Zones", "D2 · Room sizes, volume per crew, and scaling to 6 crew", () => SizesPage);
            Add("D2 Zones", "D2 · Configuration A (nominal) vs B (conflict protocol)", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Now showing: <b>{(AresHost.ConfigB ? "Configuration B" : "Configuration A")}</b>  (Menu > Project ARES > Config A / B)\n");
                sb.AppendLine("<b>                     Config A — nominal                 Config B — conflict protocol</b>");
                sb.AppendLine("Meals           one shared table in the wardroom        2nd dining spot: fold-out table + stool in the Lounge");
                sb.AppendLine("Wardroom        open plan                               acoustic screens N + S on a ceiling track (quiet halves)");
                sb.AppendLine("Lab             shared workbench                        privacy screen between workstations");
                sb.AppendLine("Routes          one main route through the core        lit alternate route on the lower deck (no forced meetings)");
                sb.AppendLine("Schedule        everyone eats at the wardroom table     one of the pair eats in the Lounge (watch Dr. Rivera move)");
                sb.AppendLine("Commons         games / movies                          warm 'café' second social space so nobody is isolated");
                sb.AppendLine("Mediation       —                                       opt-in private check-ins, offered in the team meeting\n");
                sb.AppendLine("<b>Why it works:</b> it creates natural separation and de-escalation paths WITHOUT isolating anyone or telling the crew who is in conflict. Everything is reversible in minutes, with no tools.");
                sb.AppendLine("<b>When:</b> the AI never deploys Config B on its own. It is deployed only after a human says yes: the crew member accepts the Tier-2 conflict offer, or the Commander switches it (C / Dashboard).");
                sb.AppendLine("<b>Evidence:</b> long-duration analogues (Mars-500, HI-SEAS, Antarctic winter-overs) report friction peaking in the second half of missions (the 'third-quarter' effect); room to withdraw and separate routines reduce it.");
                return sb.ToString();
            });
        }
        // =====================================================================================================  D3 AI PROTOCOLS
        // per condition: tier interventions (levers, settings, zones), monitoring metrics, evidence
        static readonly string[] T1Detail = {
            "Zone where the person is (shared zones; their pod only if invited): light → 2400 K amber at 70 %, alpha 10 Hz binaural + ocean bed, temperature 23 °C, lavender (linalool), airflow 0.15 m/s. VR retreat pre-set to 2600 K, bergamot, rainforest.",
            "Wardroom bright-light therapy 07:30–08:00 (therapy panel specified at up to 10 000 lux; shown in the game as wardroom lights at 160 %, 5000 K), greenhouse to green-enriched spectrum + petrichor + birdsong, favourite music queued for free time, negative-ion generator on in the commons (specified hardware, not simulated).",
            "No change for the pair to notice: meal and route TIMING staggered so they aren't forced together; Commons set to warm 2700 K 'café' (coffee scent, café ambience) as a calm alternative.",
            "Work zones: 6500 K at 140 %, airflow 0.4 m/s, extra CO2 scrubbing (< 600 ppm), peppermint / rosemary, gamma 40 Hz focus bed, temperature 21 °C.",
            "Quiet mode: AI notifications cut to essentials, free-choice slots added to the schedule, the AI stops suggesting and starts asking.",
            "Dynamic hue drift in commons + corridors, new soundscape rotation, new Earth-view set, scent rotation every 15–45 min (citrus → pine → bread → sea breeze …)." };
        static readonly string[] T2Detail = {
            "Gentle offer: the VR retreat / lounge is ready, warm and calm. Pod light becomes a 6-breaths-a-minute guided-breathing light (if invited). Everything T1 stays on.",
            "BEHAVIOURAL ACTIVATION: an invitation to DO something — tend or harvest in the greenhouse, an exercise slot, a family video call booked in the best comm window; radiant wall warmth in the commons (specified hardware, not simulated).",
            "Offer of Configuration B (screens, second dining spot, privacy screen, alternate route) and opt-in private check-ins; meeting suggestions are team-level, never naming anyone.",
            "Offer of a 20-minute rest break in quarters + task rebalancing (collaborative instead of solo tasks) in the team meeting.",
            "Offer to choose your own task order or a self-directed project (printing, robotics); the Commander gets a team-level 'more choice' suggestion.",
            "Offer of something new: crater-rim sightseeing EVA, game night in the commons, a new recipe in the galley — the crew member picks." };
        static readonly string[] MonitorMetric = {
            "Anxiety score falls ≥ 15 % or below Tier 1 within 20 min; HRV (RMSSD) back within 10 % of baseline; GSR peaks < 3 / min.",
            "Over 48–72 h: sleep efficiency ≥ 80 %, daily active + social minutes rising, greenhouse / commons visits, voice energy back toward baseline.",
            "Over 24 h: the pair's co-location back to normal and calm voice tone at shared meals; no avoidance of shared zones.",
            "Over 4 h: task error rate and completion time back within 10 % of baseline; zone CO2 < 600 ppm; next night's sleep efficiency.",
            "Over 48 h: more self-chosen activities in free time, fewer declined/late tasks, voice engagement in meetings.",
            "Over 72 h: more variety of zones visited and activities chosen, steadier task performance, fewer 'bored' self-reports." };
        public static string SizesPage =
            "<b>Room sizes (measured from the game model)</b>\n" +
            "  Pill habitat (the 8 NASA zones): 29.0 × 15.9 m on level 1, 29.4 × 19.5 m on level 2 (+6 m)\n" +
            "    Level 1: airlock → hygiene → workstations, storage, ECLSS, medical · Level 2: crew quarters, galley, exercise, sanctuary / VR\n" +
            "  Centre dome (crew dorm, storm shelter, control tower): ⌀ 60 m, tower floors ⌀ 46.6 m\n" +
            "  Greenhouse ⌀ 32.8 m · fabrication bay ⌀ 32.8 m · medical, science and power modules ⌀ 28.8 m · tunnels 18–20 m long\n" +
            "  Overall site: 160 × 142 m\n\n" +
            "<b>Totals</b> ≈ 7 760 m² of floor, ≈ 25 600 m³ pressurised volume\n" +
            "  The pill habitat alone gives ≈ 600 m³ per crew member (4) — above the NASA HUNCH guide of 20–120 m³ per crew.\n" +
            "  The rest of the base is sci-fi big on purpose (portals, tower, mining); the realistic game is the flight-sized version.\n\n" +
            "<b>Scaling to the brief's 6 crew</b> (we fly 4 because the NASA HUNCH page asks for 2–4):\n" +
            "  • 2 more sleep pods in the centre dorm (it has room for 12) · galley table seats 6\n" +
            "  • +2 wearable sensor sets (0.07 kg, 0.15 W each); room sensors stay the same\n" +
            "  • Pill habitat volume at 6 crew: ≈ 400 m³ each · the AI engine loops over any crew size";
        static readonly string[] FlowKey = { "anxiety", "depression", "conflict", "fatigue", "autonomy", "monotony" };
        static readonly string[] Evidence = {
            "Slow breathing near 6 breaths/min raises HRV and calms arousal; warm dim light, nature sound and lavender scent have modest calming evidence; a short acute window (20 min) matches how anxiety spikes behave.",
            "Behavioural activation and exercise are evidence-based treatments for depression; bright-light therapy (≈10 000 lux, morning) helps mood and circadian rhythm; depression is slow, so the window is 48–72 h and the response must be ACTIVE, not just calming.",
            "Forced proximity raises friction in confined crews (Antarctic and Mars-500 reports); giving space and choice de-escalates without singling anyone out.",
            "Elevated CO2 is linked to poorer decision-making and headaches (ISS reports); blue-enriched light improves alertness; breaks restore attention.",
            "Self-Determination Theory: autonomy is a basic psychological need. The 1973 Skylab 4 crew pushed back against an over-packed ground schedule — over-control backfires.",
            "The senses habituate to unchanging input; novelty in light, sound, scent and activity restores engagement. Scent is rotated because smell habituates within minutes." };

        static string Inputs(int k)
        {
            var l = new List<string>();
            for (int s = 0; s < 9; s++) if (SensorSpec.W[k, s] > 0) l.Add($"{SensorSpec.Name[s]} {SensorSpec.W[k, s] * 100:0}% — {SensorSpec.Threshold[s]}");
            return "  • " + string.Join("\n  • ", l);
        }

        static void BuildD3()
        {
            Add("D3 AI protocols", "D3 · Three-tier severity triage (all conditions)", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Every condition gets a score 0–1 (weighted sensors, smoothed). Thresholds: <b>Tier 1 ≥ {CondSpec.T1}</b> · <b>Tier 2 ≥ {CondSpec.T2}</b> · <b>Tier 3 ≥ {CondSpec.T3}</b> (plus up to +0.05 for a person whose normal is higher).");
                sb.AppendLine("<b>Tier 1 — mild:</b> silent, subtle, reversible environment changes. Not announced, but ALWAYS written in the person's own log (silent ≠ secret).");
                sb.AppendLine("<b>Tier 2 — moderate:</b> stronger changes + a gentle OFFER in plain words. Yes / No. 'No' stops it at once and the AI waits 6 h. No answer in 1 h = 'not now'.");
                sb.AppendLine("<b>Tier 3 — severe:</b> the person is told FIRST, then the CMO (backup: Commander → Mission Control); Tier-2 support continues while humans take over.");
                sb.AppendLine("\n<b>Rules that can only LOWER the tier:</b>");
                sb.AppendLine("  • signal hasn't lasted the persistence time → Tier 0 (no action)");
                sb.AppendLine("  • confidence < 50 % (sensors failed / opted out) → max Tier 1");
                sb.AppendLine("  • weekly human review overdue (> 7 days) → max Tier 1");
                sb.AppendLine("  • crew member paused monitoring or said no → AI holds back (Tier 3 safety still possible)");
                sb.AppendLine("<b>Rule that can RAISE it:</b> a support that isn't working by the end of its monitoring window steps up one tier.");
                var e = E; if (e != null) { sb.AppendLine("\n<b>LIVE tiers</b> (you see your own detail; others only as status):"); foreach (var c in e.crew) sb.AppendLine("  " + e.CrewLine(c, 1)); }
                return sb.ToString();
            });
            for (int k0 = 0; k0 < 6; k0++)
            {
                int k = k0; string nm = CondSpec.Name[k];
                Add("D3 AI protocols", $"D3 · Flowchart {k + 1}/6 — {nm} (stages 1–4)", () =>
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("<b>① Detection inputs</b> (sensor · weight · trigger)\n" + Inputs(k));
                    sb.AppendLine("<b>② Signal weighting</b> — weighted average of the available sensors above. One sensor alone can't reach a tier (its weight is at most " + Enumerable.Range(0, 9).Max(s => SensorSpec.W[k, s]) * 100 + "%); two or three agreeing signals = high confidence. Failed / opted-out sensors are removed and confidence drops." + (k == 0 ? " HRV, GSR and voice count only 25 % during and 30 min after exercise or EVA." : "") + (k <= 1 ? " A voluntary 2-question self-check adds a signal for 24 h." : ""));
                    sb.AppendLine($"<b>③ Duration assessment</b> — smoothing time constant {CondSpec.TauHours[k]:0.##} h; must stay above threshold for <b>{(CondSpec.PersistHours[k] < 1 ? (CondSpec.PersistHours[k] * 60).ToString("0") + " min" : CondSpec.PersistHours[k].ToString("0.#") + " h")}</b> before any action." + (k == 1 ? " Fast lane: if poor sleep AND low activity both confirm it, silent Tier 1 may start earlier and Tier 2 needs 36 h instead of 48 h." : ""));
                    sb.AppendLine($"<b>④ Severity triage</b> — score ≥ {CondSpec.T1} → Tier 1, ≥ {CondSpec.T2} → Tier 2, ≥ {CondSpec.T3} → Tier 3, then the lowering rules (confidence < 50 %, review overdue, refusal, pause).");
                    var e = E; if (e != null) sb.AppendLine($"\nLIVE {nm}: " + string.Join(" · ", e.crew.Select(c => c.name.Split(' ').Last() + (c.id == 1 ? $" score {c.smooth[k]:0.00} T{c.tier[k]}" : c.tier[k] > 0 ? " support active" : " OK"))));
                    return sb.ToString();
                });
                Add("D3 AI protocols", $"D3 · Flowchart {k + 1}/6 — {nm} (stages 5–9)", () =>
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"<b>⑤ Interventions</b>\n  <b>Tier 1:</b> {T1Detail[k]}\n  <b>Tier 2:</b> {T2Detail[k]}\n     Offer wording: \"{MoodEngine.T2Offer[k]}\"\n  <b>Tier 3:</b> Tier 2 continues + human chain of care (⑨).");
                    sb.AppendLine("<b>⑥ Notification & consent</b> — T1 silent but logged (or announced, if you chose 'tell me every change'). T2 is an offer: Yes / No / ignore. Override any time: STOP button, pause 24 h, AI off switch, restore my room, 'that reading is wrong'.");
                    sb.AppendLine($"<b>⑦ Monitoring window: {(CondSpec.MonitorHours[k] < 1 ? (CondSpec.MonitorHours[k] * 60).ToString("0") + " min" : CondSpec.MonitorHours[k].ToString("0") + " h")}</b> — {MonitorMetric[k]} Improving → keep going; not improving → step up one tier (never past a refusal).");
                    sb.AppendLine("<b>⑧ Resolution</b> — score back below Tier 1 → 10-min scent air-purge, then light/sound/temperature fade back to the zone design over 30 min; episode logged; personal baseline updated (10 % toward the new normal).");
                    sb.AppendLine($"<b>⑨ Escalation</b> — CMO alert when the score passes {CondSpec.T3} for the persistence time, or Tier 2 fails its window. Must be acknowledged within <b>{MoodEngine.AckLimitH(k) * 60:0} min</b>, else Commander, then Mission Control. MC is told directly only if no crew responder exists or Tier 3 lasts > 24 h — always after the person was told.");
                    sb.AppendLine($"<color=#9FB3C8><b>Evidence:</b> {Evidence[k]}</color>");
                    return sb.ToString();
                });
                AddImg("D3 AI protocols", $"D3 · Flowchart {k + 1}/6 — {nm} (drawn diagram)", "flow_" + FlowKey[k],
                    "All nine stages as a diagram. Diamonds are decisions; numbers are the values the AI uses in this game.");
            }
            Add("D3 AI protocols", "D3 · Environmental levers — which, how strong, in what order", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>Lighting</b> 1800–6500 K, 0–10 000 lux, zone by zone · blue light suppressed after 20:00 (lights warm to ≈2200 K and dim to 55 %) · hue drift for monotony · night-light + dawn simulation in pods.");
                sb.AppendLine("<b>Audio</b> delta 2 Hz (sleep) · alpha 10 Hz (calm) · gamma 40 Hz (focus) · rain, ocean, rainforest, café, birdsong · morning chime 07:00 / evening chime 21:00 as daily anchors · personal playlists.");
                sb.AppendLine("<b>Thermal</b> 18–26 °C · 18–19 °C for sleep · 23 °C for anxiety · 1–2 °C gradients between zones (corridors take the average) · each person sets their own quarters.");
                sb.AppendLine("<b>Scent</b> lavender / bergamot (calm) · peppermint / rosemary / citrus (alert) · petrichor / coffee / bread (comfort) · rotation every 15–45 min · 10-min air purge between scents.");
                sb.AppendLine("<b>Air</b> CO2 < 600 ppm in work bays · humidity 45–55 % · airflow 0.1 m/s (sleep) to 0.4 m/s (alertness) · negative ions in the commons (depression support — specified hardware, not simulated).");
                sb.AppendLine("<b>Social & routine</b> family-call slots in the best comm window · shared meals when cohesion drops · task rebalancing toward team work · daily planning conference · a non-judgemental 'talk to me' AI companion in the Crew Interface.\n");
                sb.AppendLine("<b>Order of use (every condition):</b> 1) the least noticeable lever in the zone the person is already in → 2) a change in a zone they can CHOOSE to go to → 3) an offer → 4) people (CMO).");
                sb.AppendLine("<b>Two conditions at once:</b> levers are given different ZONES so they never fight (e.g. depression → greenhouse + wardroom, conflict → commons + timing). A safety alarm overrides every lever instantly.");
                sb.AppendLine($"\nLIVE last action: {LastAction}");
                return sb.ToString();
            });
        }
        // =====================================================================================================  D4 ETHICS
        static readonly string[] LimitWhy = {
            "Psychological data is the most personal data there is; a crew of 4 for 18 months can't 'un-know' something. Trust in the system depends on it.",
            "Autonomy is non-negotiable (ARES constraint). An AI that keeps pushing after 'no' causes the autonomy deprivation it is meant to treat.",
            "The AI is a support layer, not a doctor or a flight controller. Medicine and life support need human judgement and accountability.",
            "Being reported about behind your back destroys trust; the person must always hear it first, from the system that made the decision.",
            "Anything the AI does must be undone by any crew member in seconds — so a misclassification can never cause lasting harm.",
            "Microphones and cameras in sleeping, washing and family-call spaces would make the habitat a place with no privacy at all.",
            "An unsupervised AI drifts. A weekly human review keeps the people accountable for the system; if missed, the AI loses power automatically.",
            "A mood light on your chest would broadcast your mental state to the whole crew. Sharing must be the wearer's choice." };

        static void BuildD4()
        {
            Add("D4 Ethics", "D4 · Tension 1 — Surveillance vs. privacy", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>Where monitoring stops:</b> no camera or mic in private pods, quarters, hygiene, lavatories, the VR retreat or the family-call booth (only a door sensor). The medical-bay mic is emergency-only. Any crew member can pause ALL psych monitoring for 72 h, no reason needed.");
                sb.AppendLine("<b>Who sees what:</b> the person sees everything about themselves. Other crew see nothing (not even on the Command dashboard, which is aggregate-only). The CMO sees detail only for a Tier-3 case assigned to her. Mission Control gets condition, tier and times — never raw data — and only after the person was told.");
                sb.AppendLine($"<b>Storage, retention, deletion:</b> raw audio and video are processed on the device and never stored. Derived scores live only in memory. Log entries are deleted automatically after {MoodEngine.RetentionDays:0} days, except Tier-3 safety records (medical record: person + CMO only). The person can export everything, or delete their own history at any time (Crew Interface → Privacy).");
                sb.AppendLine("<b>Consent when it can't be fully optional:</b> every psych sensor can be opted out. Two signals stay on for everyone — the heart-rate safety floor and shared-air CO2 — because they protect life and the OTHER crew in a sealed habitat. They are vital signs, not mood data, and they only trigger life-safety alarms. A permanent opt-out is possible after a conversation with the CMO (a human decides, never the AI).");
                var e = E; if (e != null) sb.AppendLine($"\nLIVE: you have {e.C(1).log.Count} entries in your personal log · psych sensors you opted out of: {Enumerable.Range(0, 9).Count(i => e.C(1).optOut[i])}");
                return sb.ToString();
            });
            Add("D4 Ethics", "D4 · Tension 2 — Autonomy vs. intervention", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>If someone refuses — even while getting worse — the AI stops.</b> It logs the refusal and holds back: no new offer for that condition for at least 6 h. Only a HUMAN may follow up: if the condition reaches Tier 3 the CMO is asked to check in, and the person is told first. The AI never decides alone to override a 'no'.");
                sb.AppendLine("<b>Helping with autonomy deprivation without imposing:</b> the autonomy protocol REMOVES things — fewer notifications, more free-choice slots — and its Tier-2 is a question ('what would you choose?'), not a suggestion.");
                sb.AppendLine("<b>The agency test, in code:</b> if someone already shows autonomy deprivation, the AI makes NO silent changes for them — it asks instead. And nobody gets more than 3 silent changes in 24 hours.");
                sb.AppendLine("<b>Supporting vs. nudging:</b> supporting = making an option easier and leaving the choice visible (the VR retreat is ready if you want it). Nudging = steering someone without them knowing. Our rule: every Tier-1 change is in the person's own log, is reversible, and is something they would agree to if asked — and they agreed to the list before the mission.");
                var e = E; if (e != null) sb.AppendLine($"\nLIVE: refusals respected so far: {e.log.Count(l => l.kind == "refuse")} · silent changes withheld by the agency test: {e.log.Count(l => l.kind == "agency")}");
                return sb.ToString();
            });
            Add("D4 Ethics", "D4 · Tension 3 — Transparency vs. distraction", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>Silent vs. inform:</b> Tier 1 is silent because a small light or sound change is less stressful than a message about it. Tier 2 and 3 always inform. Anyone can switch to 'tell me about EVERY silent change'.");
                sb.AppendLine("<b>Transparency without intrusion:</b> no pop-ups for Tier 1. Instead a calm, always-available log (Crew Interface → AI actions) says what changed, where, why and how confident the AI was. Offers come at most once per condition, expire after 1 h and are never repeated after a 'no'.");
                sb.AppendLine("<b>Can crew see a log of all AI actions?</b> Yes — every action about them, with reasons, exportable to a file. Silent ≠ secret.");
                sb.AppendLine("<b>If someone feels deceived (board Q3):</b> the AI apologises, shows the full log and reasons, offers 'tell me everything' or 'no Tier 1 for me', and a human follows up only if wanted.");
                return sb.ToString();
            });
            Add("D4 Ethics", "D4 · Tension 4 — Individual vs. crew", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>Protecting others without revealing A:</b> the AI acts on the ENVIRONMENT and the SCHEDULE, not on information. In a conflict it staggers timing and opens a second dining spot without telling anyone who is involved. Team meetings get team-level suggestions only. Beacon lights show comm status, not mood, unless the wearer opts in.");
                sb.AppendLine("<b>When a personal crisis becomes a crew safety event:</b> only when there is an immediate risk to life or to the mission's critical operations (Tier 3 with a safety flag, a medical emergency, or someone unable to perform a safety-critical role such as EVA). Then the CMO decides what the Commander needs to know — the minimum, e.g. 'Tanaka is off EVA rotation this week', not why.");
                sb.AppendLine("<b>Who decides?</b> Never the AI. The AI detects and informs the person; the CMO decides on care and on what is shared; the Commander decides on operations; Mission Control is a backup when nobody on the Moon can respond.");
                return sb.ToString();
            });
            Add("D4 Ethics", "D4 · Hard limits (enforced in code) and why", () =>
            {
                var sb = new StringBuilder();
                for (int i = 0; i < EthicsGuard.HardLimits.Length; i++) sb.AppendLine($"<b>{i + 1}. {EthicsGuard.HardLimits[i]}</b>\n    <color=#9FB3C8>Why: {LimitWhy[i]}</color>");
                sb.AppendLine($"\nLIVE: actions blocked by hard limits so far: <b>{EthicsGuard.blocked}</b>" + (EthicsGuard.blockedLog.Count > 0 ? " · last: " + EthicsGuard.blockedLog.Last() : "") + "  (try 'Test a hard limit' on Project ARES > Dashboard)");
                return sb.ToString();
            });
            Add("D4 Ethics", "D4 · Human override — what triggers it, who can do it", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>Any crew member, any time, in 1 tap (Menu > Project ARES > AI OFF switch, or F12):</b> the AI OFF switch — the AI may not change anything; lights and systems return to normal.");
                sb.AppendLine("<b>Each person, for themselves:</b> STOP a support · pause 24 h · pause psych monitoring 72 h · restore my room · 'that reading is wrong' · opt out of a sensor · delete my history.");
                sb.AppendLine("<b>Automatic human override triggers:</b>");
                sb.AppendLine("  • Tier 3 → the CMO takes over the case (AI only keeps the environment calm)");
                sb.AppendLine("  • no acknowledgement in 15 min (acute) / 1 h (slow) → next human in the chain");
                sb.AppendLine("  • weekly human review missed (> 7 days) → AI limited to Tier 1");
                sb.AppendLine("  • confidence < 50 % (failed / opted-out / contradicting sensors) → AI limited to Tier 1; no escalation on weak evidence");
                sb.AppendLine("  • any safety alarm (fire, leak, CO2, solar storm, medical) → every AI comfort lever stops at once");
                sb.AppendLine("  • the AI tries a blocked action → it is refused, counted and shown on the dashboard for the weekly review");
                sb.AppendLine("<b>Maximum response times:</b> Tier 1 < 1 min (automatic) · Tier 2 offer < 5 min · Tier 3 acknowledged < 15 min (acute) or < 1 h (slow-building).");
                return sb.ToString();
            });
            Add("D4 Ethics", "D4 · Bedtime rules — when the AI is the only one awake", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("At night nobody is watching the AI, so it gets FEWER powers, not more.\n");
                sb.AppendLine("<b>While anyone sleeps the AI may only:</b> dim lights to a 1-lux amber night-light, run the dawn-simulation sunrise, and watch vital signs.");
                sb.AppendLine("<b>It may NOT:</b> act inside a pod (unless that person invited it), play sound, change air, O2 or temperature, start Tier-2 offers, or wake anyone — except for a life-safety alarm or a Tier-3 emergency (and then it wakes only the responder, not the whole crew).");
                sb.AppendLine("<b>If it breaks a rule:</b> it must WAKE a human and say what it did. The human switches it off (F12). Every change it made is rolled back.");
                sb.AppendLine("<b>If no human answers in 30 seconds:</b> a hardware interlock — not software the AI controls — switches it off by itself. Only a human can switch it back on.");
                sb.AppendLine("<b>Why:</b> AI can be wrong or misbehave, and a sleeping crew cannot say no. Human > AI is a hard limit, not a setting.");
                sb.AppendLine("\n<b>Try it:</b> mission 5 \"Bedtime: humans stay in charge\" (G does each step), or the menu item \"Test: AI breaks a bedtime rule tonight\".");
                return sb.ToString();
            });
            Add("D4 Ethics", "D4 · How ethics SHAPED the technical design", () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("These are engineering decisions we made BECAUSE of the ethics, not rules added at the end:");
                sb.AppendLine("  • <b>On-device processing</b> for voice and face → only 4 numbers a minute leave the device; there is nothing to leak.");
                sb.AppendLine("  • <b>Room sensors only in shared zones</b> → privacy ALSO saved ≈ 5 kg and ≈ 50 W (no camera + mic in 8 private rooms).");
                sb.AppendLine("  • <b>Weighted multi-sensor scores + confidence gate</b> → designed so no single sensor can label a person.");
                sb.AppendLine("  • <b>Environment levers instead of messages</b> → help without exposing anyone (Tension 4).");
                sb.AppendLine("  • <b>Every lever reversible in seconds</b> and fading back over 30 min → mistakes can't do lasting harm.");
                sb.AppendLine("  • <b>Agency budget</b> (max 3 silent changes a day; none for someone over-controlled) → the AI can't become the problem.");
                sb.AppendLine("  • <b>Hard limits as code</b> (EthicsGuard) → blocked actions are counted and visible, so the limits are testable, not promises.");
                sb.AppendLine("  • <b>Weekly human review timer</b> built into the AI's power level.");
                sb.AppendLine("  • <b>30-day retention + export + delete</b> → the crew member owns their data.");
                return sb.ToString();
            });
        }


        static void BuildD4Report()
        {
            // the reading sheet is small, so the report is split into 6 short pages at paragraph breaks
            var paras = EthicsReport.Text.Split(new[] { "\n\n" }, System.StringSplitOptions.None).ToList();
            int total = paras.Sum(p => p.Split(' ').Length), per = total / 6 + 1;
            var pagesTxt = new List<string>(); var cur = new List<string>(); int w = 0;
            foreach (var p in paras)
            {
                bool head = p.Length < 60 && !p.EndsWith(".");
                if (w >= per && cur.Count > 0 && (head || w > per * 1.3f)) { pagesTxt.Add(string.Join("\n\n", cur)); cur.Clear(); w = 0; }
                cur.Add(head ? "<b>" + p + "</b>" : p); w += p.Split(' ').Length;
            }
            if (cur.Count > 0) pagesTxt.Add(string.Join("\n\n", cur));
            for (int i = 0; i < pagesTxt.Count; i++)
            {
                int ii = i;
                Add("D4 Ethics", $"D4 · Written ethics report {i + 1}/{pagesTxt.Count}", () => (ii == 0 ? "<b>" + EthicsReport.Title + "</b>  <size=80%>(" + EthicsReport.Words + " words · brief: 800–1,200 · Export saves Reports/D4_Ethics_Report.md)</size>\n\n" : "") + pagesTxt[ii]);
            }
        }

        static void BuildD56()
        {
            Add("D5–D6 Present", "D5 · 15-minute review-board run-of-show (live in the simulator)", () =>
                "<b>0:00</b> Title + the problem: 6 conditions, 4 crew, 18 months (slides)\n" +
                "<b>1:30</b> D1 sensors — F11 (Project ARES) > D1 pages; Dashboard > Break a sensor (graceful degradation)\n" +
                "<b>4:00</b> D2 zones — N base map + D2 pages; walk a corridor; Config A / B switches the screens and second table\n" +
                "<b>7:00</b> D3 protocols — D3 flowcharts; F10 Crew interface: answer a Tier-2 offer, STOP a support\n" +
                "<b>10:00</b> Escalation — Board questions > Q6 (CMO on EVA → backup chain); CMO console\n" +
                "<b>11:30</b> D4 ethics — hard limits page, F12 (AI off switch), Dashboard > Test a hard limit\n" +
                "<b>13:30</b> Bonus A: 72-hour tabletop · Bonus B: crew interface tour\n" +
                "<b>14:30</b> Close + credits. Then 10-min Q&A: Board questions runs Q1–Q6 live.\n\n" +
                "Every team member presents one part. Export everything: Project ARES > Export all to the Reports folder.");
            Add("D5–D6 Present", "D6 · Reflection journal prompts (3 entries · 300–400 words each)", () =>
                "Journals are graded individually and must be in YOUR own words — these are prompts, not answers.\n\n" +
                "<b>Entry 1 (week 2 — sensors):</b> Which sensor did you argue to cut, and why? What surprised you about mass and power? What failure mode worries you most?\n\n" +
                "<b>Entry 2 (week 5 — protocols):</b> How did your view of 'silent' Tier-1 changes change? Which condition was hardest to design for and why? What did your team disagree about and how was it settled?\n\n" +
                "<b>Entry 3 (week 8 — ethics & presentation):</b> Which ethical tension changed your technical design most? What would you redesign after the tabletop simulation? Honest self-assessment: your contribution, what went well, what you'd do differently.\n\n" +
                "Tip: compare your entries — graders look for thinking that EVOLVES, not repeats.");
        }

        // ===================================================================================================== export
        public static string ExportAll()
        {
            var sb = new StringBuilder("# Project ARES — Lunar Smart Adaptive Habitat · deliverables D1–D4\n");
            sb.AppendLine("© 2026 Jaineil R. — with thanks to Angel C. and Farhan K. Generated from the running game " + (AresHost.Engine != null ? SimClock.Now : "") + "\n");
            string sec = "";
            foreach (var p in Pages)
            {
                if (p.section != sec) { sec = p.section; sb.AppendLine("\n## " + sec + "\n"); }
                sb.AppendLine("### " + p.title + "\n");
                string body = System.Text.RegularExpressions.Regex.Replace(p.body(), "<.*?>", "");
                sb.AppendLine(body); sb.AppendLine();
            }
            EthicsReport.Save(AresHost.ReportsDir);
            string path = System.IO.Path.Combine(AresHost.ReportsDir, "ARES_Deliverables.md");
            System.IO.File.WriteAllText(path, sb.ToString());
            return path;
        }
    }
}
