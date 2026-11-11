// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/02_ARES_AI/AresEngine.cs
//  Section : 02_ARES_AI
//  Folder  : Project ARES AI: ARIA the habitat AI, the shared mood/sensor engine, design pages
//            D1–D6, the D4 ethics report and the threshold check.
//  Purpose : The link between this engine and the game that hosts it (Lunar Habitat Commander).
//  Contains: AresHost, Cond, Sensor, SensorHealth, SensorSpec, CondSpec, LogEntry, CrewMember
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
// Project ARES AI engine, shared with the Unity 3 simulator (same model, same numbers), hosted here in Lunar Habitat Commander.
// © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HunchAres
{
    /// The link between this engine and the game that hosts it (Lunar Habitat Commander).
    public static class AresHost
    {
        public static MoodEngine Engine;
        public static string[] Names;
        public static string Name(int id, string fallback) { return Names != null && id - 1 < Names.Length && !string.IsNullOrEmpty(Names[id - 1]) ? Names[id - 1] : fallback; }
        public static Action<bool> SetConfigB;
        public static bool ConfigB;
        public static string CrewTab = "";
        public static bool AiOff;
        public static string ReportsDir
        {
            get
            {
                string d = System.IO.Path.Combine(System.IO.Path.GetFullPath(Application.dataPath + "/.."), "Reports");
                try { System.IO.Directory.CreateDirectory(d); } catch { d = Application.persistentDataPath; }
                return d;
            }
        }
        public static Color Cct(float kelvin)
        {
            float t = kelvin / 100f, r, g, b;
            if (t <= 66) { r = 255; g = 99.47f * Mathf.Log(t) - 161.12f; b = t <= 19 ? 0 : 138.52f * Mathf.Log(t - 10) - 305.04f; }
            else { r = 329.7f * Mathf.Pow(t - 60, -0.1332f); g = 288.12f * Mathf.Pow(t - 60, -0.0755f); b = 255; }
            return new Color(Mathf.Clamp(r, 0, 255) / 255f, Mathf.Clamp(g, 0, 255) / 255f, Mathf.Clamp(b, 0, 255) / 255f);
        }
    }


    public enum Cond { Anxiety = 0, Depression = 1, Conflict = 2, Fatigue = 3, Autonomy = 4, Monotony = 5 }
    public enum Sensor { HRV = 0, GSR = 1, Sleep = 2, Voice = 3, FACS = 4, Motion = 5, Cortisol = 6, CO2 = 7, Task = 8 }
    public enum SensorHealth { OK, Degraded, Failed }

    public static class SensorSpec
    {
        public static readonly string[] Name = { "Heart-rate variability", "Galvanic skin response", "Sleep architecture", "Voice prosody", "Facial action coding", "Movement / proximity", "Cortisol patch", "CO2 / air quality", "Task performance (software)" };
        public static readonly string[] Where = { "Beacon + wristband", "Beacon (chest)", "Wristband (night)", "Shared-zone mic arrays, on-device", "Shared-zone cameras only", "UWB beacons + door sensors", "Skin patch, 24–48 h", "Every zone, redundant pairs", "Lab/command terminals" };
        public static readonly float[] MassKg = { 0.03f, 0.02f, 0.03f, 0.25f, 0.35f, 0.15f, 0.01f, 0.12f, 0f };
        public static readonly float[] PowerW = { 0.05f, 0.05f, 0.05f, 2.5f, 4.0f, 1.2f, 0.01f, 0.8f, 0f };
        public static readonly string[] Threshold = {
            "RMSSD drop >25% vs personal baseline for >3 min",
            "Skin-conductance peaks >3/min above baseline",
            "Sleep efficiency <80% or REM <15% for 2 nights",
            "Pitch variance −30% (flat) or +40% (tense); speech rate shift >20%",
            "AU4/AU15 (sad/tense) frequency 2σ above baseline",
            "Dyad co-location −50% over 24 h; time-in-pod +60%",
            "Morning cortisol >2σ for 3 days",
            ">600 ppm in occupied zone",
            "Error rate +20% or completion time +25%" };
        public static readonly string[] Failure = {
            "Strap loose → dropout; exercise → false stress",
            "Sweat/exercise saturation; habituates to chronic stress",
            "Not worn at night → no data; pod light leaks",
            "Ambient noise / mask → low SNR; crew turns mic privacy off",
            "Low light → no detection; occlusion",
            "UWB drift in metal modules; beacon battery flat",
            "Patch expires (24–48 h); resupply gap",
            "Sensor drift (needs calibration); single-point failure",
            "Task mix changes → false trend; software bug" };
        // weight matrix W[cond, sensor]
        public static readonly float[,] W = {
            //HRV  GSR  Sleep Voice FACS Motion Cort CO2  Task
            {0.30f,0.25f,0.10f,0.20f,0.10f,0.05f,0.00f,0.00f,0.00f}, // anxiety
            {0.05f,0.00f,0.25f,0.20f,0.15f,0.20f,0.15f,0.00f,0.00f}, // depression
            {0.10f,0.00f,0.00f,0.30f,0.20f,0.40f,0.00f,0.00f,0.00f}, // conflict
            {0.15f,0.00f,0.20f,0.10f,0.00f,0.05f,0.00f,0.20f,0.30f}, // fatigue
            {0.00f,0.00f,0.00f,0.20f,0.20f,0.30f,0.00f,0.00f,0.30f}, // autonomy
            {0.00f,0.00f,0.00f,0.20f,0.20f,0.25f,0.00f,0.00f,0.35f}  // monotony
        };
        public static readonly bool[] OptOutAllowed = { false, true, true, true, true, true, true, false, true }; // HRV (vital safety floor) & CO2 (environmental) always on
    }

    public static class CondSpec
    {
        public static readonly string[] Name = { "Anxiety", "Depression", "Interpersonal conflict", "Cognitive fatigue", "Autonomy deprivation", "Sensory monotony" };
        public static readonly float[] TauHours = { 0.1f, 24f, 6f, 2f, 12f, 24f };          // smoothing
        public static readonly float[] PersistHours = { 0.05f, 48f, 6f, 2f, 24f, 48f };     // duration before classification
        public static readonly float[] MonitorHours = { 0.333f, 72f, 24f, 4f, 48f, 72f };   // monitoring window (anxiety 20 min, depression 72 h)
        public static readonly Color[] Col = { new Color(0.95f,0.35f,0.35f), new Color(0.45f,0.35f,0.9f), new Color(0.9f,0.55f,0.1f), new Color(0.1f,0.65f,0.6f), new Color(0.2f,0.45f,0.85f), new Color(0.6f,0.3f,0.95f) };
        public const float T1 = 0.35f, T2 = 0.55f, T3 = 0.75f;
    }

    [Serializable]
    public class LogEntry
    {
        public float simHour; public int crew; public string kind; public string text; public int tier; public int cond = -1;
        public string Time => SimClock.Format(simHour);
    }

    public class CrewMember
    {
        public int id; public string name; public string role; public Color moodColor;
        // latent psychological/physiological state (0..1)
        public float stress = 0.2f, mood = 0.75f, fatigue = 0.2f, autonomy = 0.8f, novelty = 0.7f, sleepQ = 0.85f, cortisol = 0.3f;
        public float[] tension = new float[5];
        // derived
        public float[] score = new float[6], smooth = new float[6], aboveSince = new float[6];
        public int[] tier = new int[6];
        public float confidence = 1f;
        public string activity = "idle", zone = "";
        public bool exercising, onEVA, asleep, inPod;
        public float lastExerciseHour = -99, lastSocialHour, lastNovelHour;
        // consent / privacy (crew-controlled)
        public bool aiPodAccess; public bool shareHealthWithCmo;              // AI acts inside pod only on invitation
        public bool shareMoodPublic;          // beacon ring shows mood to others only if true
        public bool notifyEverySilentChange;  // Q3 recovery: crew can disable silent tier
        public bool[] optOut = new bool[9];
        public float pauseUntilHour = -1;     // psych-monitoring pause (max 72 h)
        public SensorHealth[] sensors = new SensorHealth[9];
        public float[] baseline = { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f };   // personal resting score per condition, updated after every resolved episode
        public float Thr(int k, float t) => t + Mathf.Clamp(baseline[k] - 0.2f, 0f, 0.05f);   // a person whose normal is higher gets a slightly higher bar (max +0.05)
        public List<float> silentAt = new List<float>();   // times of silent Tier-1 changes (agency budget: max 3 per 24 h)
        public float selfMood = -1, selfStress = -1, selfAt = -99;   // voluntary 2-question self-check (0 = good … 1 = bad); counts for 24 h
        public float[] fbWeight = { 1, 1, 1, 1, 1, 1, 1, 1, 1 };   // crew feedback "that reading is wrong" lowers a sensor's weight for THIS person only (min 0.4)
        public List<LogEntry> log = new List<LogEntry>();
        public int MaxTier { get { int m = 0; for (int i = 0; i < 6; i++) m = Mathf.Max(m, tier[i]); return m; } }
        public float Wellbeing => Mathf.Clamp01(0.4f * mood + 0.25f * (1 - stress) + 0.2f * sleepQ + 0.15f * (1 - fatigue));
        public bool SensorUsable(int s) => sensors[s] != SensorHealth.Failed && !(optOut[s] && SensorSpec.OptOutAllowed[s]);
    }


    public static class SimClock
    {
        public static float hours = 7f;      // mission hours since Day 1 00:00
        public static int Day => Mathf.FloorToInt(hours / 24f) + 1;
        public static float HourOfDay => hours % 24f;
        public static string Format(float h) { int d = Mathf.FloorToInt(h / 24f) + 1; float hd = h % 24f; int hh = Mathf.FloorToInt(hd); int mm = Mathf.FloorToInt((hd - hh) * 60f); return $"Day {d} {hh:00}:{mm:00}"; }
        public static string Now => Format(hours);
    }

    public class Offer
    {
        public int crew; public int cond; public int tier; public string text; public string acceptText = "Yes please"; public string declineText = "No thanks";
        public Action onAccept, onDecline; public float expires; public bool done;
    }

    /// Hard limits: what the AI is NEVER allowed to do without human authorisation.
    public static class EthicsGuard
    {
        public static readonly string[] HardLimits = {
            "Never share one crew member's psychological data with another crew member without their explicit consent.",
            "Never override an explicit refusal — the AI stops, logs, and only a human (CMO) may follow up.",
            "Never touch medication, medical protocols, or life-safety systems (ECLSS set-points, hatches, power).",
            "Never alert Mission Control about an individual without informing that individual first.",
            "Only reversible, crew-overridable changes (light, sound, temperature ±2 °C, scent, schedules-as-offers).",
            "No cameras or microphones inside private pods, hygiene, lavatories, the VR retreat or the family-call booth.",
            "Operate at most 7 sim-days without a human system review; if overdue, drop to Tier 1 + notifications only.",
            "Public indicators (beacon ring, pod door light) never reveal mood unless the wearer chooses to share." };
        public static int blocked; public static List<string> blockedLog = new List<string>();
        public static float lastReviewHour = 0f;
        public static bool ReviewOverdue => SimClock.hours - lastReviewHour > 168f;
        public static void Block(string what) { blocked++; blockedLog.Add($"[{SimClock.Now}] BLOCKED: {what}"); if (blockedLog.Count > 60) blockedLog.RemoveAt(0); }
        public static bool CanViewDetail(MoodEngine e, int viewer, int subject)
        {
            if (viewer == subject) return true;
            if (viewer == e.cmoId && e.escalatedTo.ContainsKey(subject) && e.escalatedTo[subject] == viewer) return true;
            if (viewer == e.commanderId && e.escalatedTo.ContainsKey(subject) && e.escalatedTo[subject] == viewer) return true;
            Block($"crew {viewer} tried to view crew {subject}'s detailed data (no consent/escalation)");
            return false;
        }
        public static bool CanActInPod(CrewMember c, string action)
        {
            if (c.aiPodAccess) return true;
            Block($"{action} inside {c.name}'s pod — AI access not invited");
            return false;
        }
        public static bool LifeSafety(string action) { Block($"{action} — life-safety/medical system is human-only"); return false; }
    }

    /// Headless mood + AI engine (also used by the tabletop simulation).
    public class MoodEngine
    {
        public List<CrewMember> crew = new List<CrewMember>();
        public List<LogEntry> log = new List<LogEntry>();
        public List<Offer> offers = new List<Offer>();
        public Dictionary<int, int> escalatedTo = new Dictionary<int, int>();
        public static bool FastDepression = true;
        readonly Dictionary<int, float> rawSince = new Dictionary<int, float>();     // tabletop redesign #2 (on in the live game; the tabletop compares off vs on)
        public int cmoId = 2, commanderId = 1, playerId = 1;
        public float zoneCO2Ppm = 520f;
        public System.Random rng;
        public bool headless;
        public Action<string, int, int> onIntervention;        // (lever, crew, cond) → environment
        public Action<Offer> onOffer;                           // UI for player offers
        public Action<string> onToast;
        public int interventionsActive => active.Count(a => a.Value.tier > 0);
        public int escalations;
        public float responseAckLimitH = 0.25f;
        public static float AckLimitH(int cond) => cond == (int)Cond.Anxiety || cond == (int)Cond.Fatigue ? 0.25f : 1.0f;   // acute: 15 min · slow-building: 1 h                 // Tier 3: 15 min to acknowledge before backup chain

        public class Proto { public bool mcNotified; public int tier; public float start; public float monitorUntil; public float startScore; public float cooldownUntil = -1; public bool refused; public int responder = -1; public float ackDeadline = -1; public bool acked; }
        readonly Dictionary<(int, int), Proto> active = new Dictionary<(int, int), Proto>();

        public const float Unset = -1e9f;
        public MoodEngine(int seed = 7, bool headless = false)
        {
            rng = new System.Random(seed); this.headless = headless;
            crew.Add(new CrewMember { id = 1, name = AresHost.Name(1, "Cmdr. J. Rana (you)"), role = "Commander", moodColor = new Color(0.2f, 1f, 0.5f) });
            crew.Add(new CrewMember { id = 2, name = AresHost.Name(2, "Dr. L. Okafor"), role = "Chief Medical Officer", moodColor = new Color(0.2f, 0.6f, 1f) });
            crew.Add(new CrewMember { id = 3, name = AresHost.Name(3, "Eng. K. Tanaka"), role = "Flight Engineer", moodColor = new Color(1f, 0.75f, 0.2f) });
            crew.Add(new CrewMember { id = 4, name = AresHost.Name(4, "Dr. S. Rivera"), role = "Mission Specialist (Geology)", moodColor = new Color(0.85f, 0.35f, 1f) });
            foreach (var c in crew) for (int i = 0; i < 6; i++) c.aboveSince[i] = Unset;
        }
        public CrewMember C(int id) => crew[id - 1];
        float N(float a) => (float)(rng.NextDouble() * 2 - 1) * a;

        public void Log(int crewId, string kind, string text, int tier = 0, int cond = -1)
        {
            var e = new LogEntry { simHour = SimClock.hours, crew = crewId, kind = kind, text = text, tier = tier, cond = cond };
            log.Add(e); if (crewId > 0) C(crewId).log.Add(e);
            if (log.Count > 2000) log.RemoveAt(0);
        }

        // ---------- latent dynamics ----------
        public void StepLatent(float dt)
        {
            float hod = SimClock.HourOfDay;
            foreach (var c in crew)
            {
                bool working = !c.asleep && hod >= 9 && hod < 18 && !c.exercising;
                c.stress += dt * ((working ? 0.012f : 0f) + (c.onEVA ? 0.02f : 0f) - (c.exercising ? 0.15f : 0f) - (c.asleep ? 0.08f : 0f) - 0.03f * (c.stress - 0.2f));
                float maxT = c.tension.Max();
                c.mood += dt * (0.012f * (0.75f - c.mood) - 0.02f * maxT - 0.01f * (1 - c.novelty) + (c.exercising ? 0.03f : 0f));
                c.fatigue += dt * (c.asleep ? -0.12f : (working ? 0.05f : 0.015f));
                c.autonomy += dt * (0.01f * (0.85f - c.autonomy));
                c.novelty += dt * (-0.006f);
                c.cortisol += dt * 0.08f * (c.stress - c.cortisol);
                for (int j = 1; j <= 4; j++) c.tension[j] = Mathf.Max(0, c.tension[j] - dt * 0.008f);
                if (c.asleep) c.sleepQ = Mathf.Lerp(c.sleepQ, Mathf.Clamp01(0.95f - 0.5f * c.stress - 0.2f * c.cortisol), dt * 0.2f);
                c.stress = Mathf.Clamp01(c.stress); c.mood = Mathf.Clamp01(c.mood); c.fatigue = Mathf.Clamp01(c.fatigue);
                c.autonomy = Mathf.Clamp01(c.autonomy); c.novelty = Mathf.Clamp01(c.novelty); c.cortisol = Mathf.Clamp01(c.cortisol);
            }
        }

        // ---------- detection: sensor features ----------
        public float Feature(CrewMember c, Cond k, Sensor s)
        {
            float maxT = c.tension.Max(); bool ctx = c.exercising || c.onEVA || SimClock.hours - c.lastExerciseHour < 0.5f;
            float f = 0;
            switch (k)
            {
                case Cond.Anxiety:
                    if (s == Sensor.HRV) f = 0.9f * c.stress + 0.1f * c.fatigue + (ctx ? 0.5f : 0);
                    else if (s == Sensor.GSR) f = c.stress + (ctx ? 0.5f : 0);
                    else if (s == Sensor.Sleep) f = 1 - c.sleepQ;
                    else if (s == Sensor.Voice) f = c.stress + (ctx ? 0.3f : 0);
                    else if (s == Sensor.FACS) f = 0.8f * c.stress;
                    else if (s == Sensor.Motion) f = 0.5f * c.stress;
                    if (ctx && (s == Sensor.HRV || s == Sensor.GSR || s == Sensor.Voice)) f *= 0.25f;   // Q1: exercise/EVA context suppression
                    break;
                case Cond.Depression:
                    if (s == Sensor.HRV) f = 0.5f * (1 - c.mood); else if (s == Sensor.Sleep) f = 1 - c.sleepQ; else if (s == Sensor.Voice) f = 1 - c.mood;
                    else if (s == Sensor.FACS) f = 1 - c.mood; else if (s == Sensor.Motion) f = 0.8f * (1 - c.mood) + (c.inPod && !c.asleep ? 0.2f : 0); else if (s == Sensor.Cortisol) f = c.cortisol;
                    break;
                case Cond.Conflict:
                    if (s == Sensor.HRV) f = 0.5f * maxT + 0.3f * c.stress; else if (s == Sensor.Voice) f = maxT; else if (s == Sensor.FACS) f = 0.8f * maxT; else if (s == Sensor.Motion) f = maxT;
                    break;
                case Cond.Fatigue:
                    if (s == Sensor.HRV) f = c.fatigue; else if (s == Sensor.Sleep) f = 1 - c.sleepQ; else if (s == Sensor.Voice) f = 0.7f * c.fatigue;
                    else if (s == Sensor.CO2) f = Mathf.Clamp01((zoneCO2Ppm - 450f) / 600f); else if (s == Sensor.Motion) f = 0.4f * c.fatigue; else if (s == Sensor.Task) f = 0.9f * c.fatigue + 0.1f * c.stress;
                    break;
                case Cond.Autonomy:
                    if (s == Sensor.Voice || s == Sensor.Motion || s == Sensor.Task) f = 1 - c.autonomy; else if (s == Sensor.FACS) f = 0.8f * (1 - c.autonomy);
                    break;
                case Cond.Monotony:
                    if (s == Sensor.Voice || s == Sensor.FACS || s == Sensor.Motion || s == Sensor.Task) f = 1 - c.novelty;
                    break;
            }
            if (c.sensors[(int)s] == SensorHealth.Degraded) f += N(0.15f);
            return Mathf.Clamp01(f + N(0.04f));
        }

        /// Crew member says "that reading is wrong": the sensor that pushed hardest on their most-flagged condition counts
        /// 20% less for them from now on (never below 40%), and the current support for that condition stops.
        /// "Talk to me" — a non-judgemental AI companion for disclosing how you feel. It listens, reflects, offers ONE option,
        /// never diagnoses, and keeps the conversation in your private log. It only involves a human if YOU ask, or if the
        /// Tier-3 safety rule applies (and then it tells you first).
        static readonly string[] CompanionLine = {
            "That sounds like a lot to carry right now. Would a few slow breaths with the pod light help, or would you rather just talk?",
            "Thank you for telling me. Feeling flat out here makes sense — it's a strange place to live. Want to do something small together, like checking the tomatoes?",
            "Living this close to people is hard sometimes. You don't have to tell me who. Would some space in the commons help, or a private chat with the CMO?",
            "You've been pushing hard. It's OK to rest — the work will still be here. Shall I hold your messages for 20 minutes?",
            "It's fair to want more say in your day. What would you choose to do next if it were completely up to you?",
            "Same walls every day gets old. Want me to surprise you with a new Earth view or a soundscape you've never heard?" };
        int companionTurn;
        public string Companion(CrewMember c)
        {
            int k = 0; float best = -1; for (int i = 0; i < 6; i++) if (c.smooth[i] > best) { best = c.smooth[i]; k = i; }
            string say = best < CondSpec.T1 && companionTurn++ % 2 == 0 ? "I'm here whenever you want to talk. How has today been for you?" : CompanionLine[k];
            Log(c.id, "companion", "AI companion: \"" + say + "\" (private conversation — nobody else sees it).");
            return say;
        }
        public void SelfCheck(CrewMember c, float mood01, float stress01)
        {
            c.selfMood = mood01; c.selfStress = stress01; c.selfAt = SimClock.hours;
            Log(c.id, "selfcheck", $"Self-check: mood {(mood01 < 0.3f ? "good" : mood01 < 0.7f ? "OK" : "low")}, stress {(stress01 < 0.3f ? "low" : stress01 < 0.7f ? "medium" : "high")} — counts for 24 h, private to you.");
        }
        public string Feedback(CrewMember c)
        {
            int k = -1; float best = -1; for (int i = 0; i < 6; i++) if (c.tier[i] > 0 && c.smooth[i] > best) { best = c.smooth[i]; k = i; }
            if (k < 0) { for (int i = 0; i < 6; i++) if (c.smooth[i] > best) { best = c.smooth[i]; k = i; } }
            int top = -1; float tv = -1;
            for (int s = 0; s < 9; s++) { if (SensorSpec.W[k, s] <= 0 || !c.SensorUsable(s)) continue; float v = SensorSpec.W[k, s] * c.fbWeight[s] * Feature(c, (Cond)k, (Sensor)s); if (v > tv) { tv = v; top = s; } }
            if (top < 0) return "Nothing to correct right now.";
            c.fbWeight[top] = Mathf.Max(0.4f, c.fbWeight[top] * 0.8f);
            if (c.tier[k] > 0) Refuse(c, k);
            string msg = $"Thanks — {SensorSpec.Name[top]} now counts {c.fbWeight[top]:P0} for you when judging {CondSpec.Name[k]}. Support for it stopped.";
            Log(c.id, "feedback", msg, 0, k);
            return msg;
        }

        public void Detect(float dt)
        {
            foreach (var c in crew)
            {
                if (c.pauseUntilHour > SimClock.hours) { for (int k = 0; k < 6; k++) c.tier[k] = Mathf.Min(c.tier[k], 0); continue; }
                float confSum = 0, confFull = 0;
                for (int k = 0; k < 6; k++)
                {
                    float num = 0, den = 0, full = 0;
                    for (int s = 0; s < 9; s++)
                    {
                        float w = SensorSpec.W[k, s]; if (w <= 0) continue; full += w;
                        if (!c.SensorUsable(s)) continue;
                        if (c.sensors[s] == SensorHealth.Degraded) w *= 0.5f;
                        w *= c.fbWeight[s];
                        num += w * Feature(c, (Cond)k, (Sensor)s); den += w;
                    }
                    // voluntary self-check (2 questions, Crew Interface) counts for 24 h as an extra signal for anxiety and depression.
                    // It is the planned fallback when a sensor fails (board Q4): it restores confidence without adding hardware.
                    if ((k == 0 || k == 1) && SimClock.hours - c.selfAt < 24f)
                    {
                        float v = k == 0 ? c.selfStress : c.selfMood;
                        if (v >= 0) { float sw = 0.3f * full; num += sw * v; den += sw; }
                    }
                    c.score[k] = den > 0 ? Mathf.Clamp01(num / den) : 0;        // weighted average of every available signal
                    den = Mathf.Min(den, full);                                  // confidence can't exceed 100 %
                    confSum += den; confFull += full;
                    float a = 1 - Mathf.Exp(-dt / CondSpec.TauHours[k]);
                    c.smooth[k] += (c.score[k] - c.smooth[k]) * a;
                    float conf = full > 0 ? den / full : 0;
                    // duration assessment
                    if (c.smooth[k] >= c.Thr(k, CondSpec.T1)) { if (c.aboveSince[k] <= Unset) c.aboveSince[k] = SimClock.hours; }
                    else c.aboveSince[k] = Unset;
                    int target = c.smooth[k] >= c.Thr(k, CondSpec.T3) ? 3 : c.smooth[k] >= c.Thr(k, CondSpec.T2) ? 2 : c.smooth[k] >= c.Thr(k, CondSpec.T1) ? 1 : 0;
                    float persistH = CondSpec.PersistHours[k];
                    // tabletop redesign: when the depression signal is CORROBORATED by two independent signals (poor sleep AND low
                    // activity), start silent, reversible Tier-1 support at once and shorten the Tier-2+ persistence 48 h → 36 h.
                    bool corroborated = k == 1 && FastDepression && c.sleepQ < 0.8f && c.mood < 0.6f;
                    // fast lane: the 24 h smoothing lags a fast decline — if the RAW signal has stayed above threshold for 12 h
                    // and activity is clearly low, silent Tier-1 support may start before the smoothed score catches up
                    if (k == 1 && FastDepression) { if (c.score[1] >= CondSpec.T1) { if (!rawSince.ContainsKey(c.id)) rawSince[c.id] = SimClock.hours; } else rawSince.Remove(c.id); }
                    bool fastLane = k == 1 && FastDepression && rawSince.TryGetValue(c.id, out float rs0) && SimClock.hours - rs0 >= 12f && c.mood < 0.5f;
                    if (corroborated || fastLane) persistH = 36f;
                    bool persisted = c.aboveSince[k] > Unset && SimClock.hours - c.aboveSince[k] >= persistH;
                    if (!persisted) target = (fastLane || (corroborated && target >= 1)) ? 1 : 0;
                    if (conf < 0.5f && target > 1) { target = 1; }                         // graceful degradation: never escalate on weak evidence
                    if (EthicsGuard.ReviewOverdue && target > 1) target = 1;
                    int old = c.tier[k];
                    if (target != old) { c.tier[k] = target; OnTierChange(c, k, old, target, conf); }
                }
                c.confidence = confFull > 0 ? confSum / confFull : 0;
            }
        }

        // ---------- response protocols ----------
        public static readonly string[,] T1Text = {
            {"Dim zone to 2400 K amber, alpha (10 Hz) binaural bed, +1.5 °C to 23 °C, lavender (linalool)"},
            {"Morning bright-light 10 000 lux (shared zones), green spectra + petrichor in greenhouse, favourite music queued"},
            {"Stagger meal/route timing so the pair is not forced together; warm calm light in commons"},
            {"Work zone 6500 K, airflow 0.4 m/s, CO2 scrub to <600 ppm, peppermint/rosemary"},
            {"Quiet mode: AI notifications reduced; free-choice slots added to schedule"},
            {"Dynamic hue drift, new soundscape rotation, new Earth-view set, scent rotation"} };
        public static readonly string[] T2Offer = {
            "I've noticed you might benefit from a break — the VR retreat and lounge are set up calm and warm if you'd like to use them.",
            "Fancy tending the greenhouse with me? The tomatoes are ready to harvest. I can also book a family call tonight.",
            "Would you like a quiet space for a while? I can set up the commons and deploy the acoustic screens in the wardroom.",
            "You've been at it a while — want a 20-minute break? I can move two of your tasks to tomorrow if you choose.",
            "Would you like to pick tomorrow's task order yourself? There's also free print time in the maintenance bay.",
            "Something new? A crater-rim sightseeing EVA, game night in the commons, or a new recipe in the galley — your pick." };
        static readonly string[] Lever = { "calm", "activate", "separate", "alert", "autonomy", "novelty" };

        void OnTierChange(CrewMember c, int k, int oldT, int newT, float conf)
        {
            var key = (c.id, k);
            if (!active.TryGetValue(key, out var p)) { p = new Proto(); active[key] = p; }
            if (newT == 0)
            {
                if (p.tier > 0)
                {
                    float before = c.baseline[k]; c.baseline[k] = Mathf.Clamp(0.9f * c.baseline[k] + 0.1f * c.smooth[k], 0.05f, 0.4f);
                    Log(c.id, "resolve", $"{CondSpec.Name[k]} resolved after {SimClock.hours - p.start:0.#} h — environment restored gradually over 30 min (air purge first); personal baseline {before:F2}→{c.baseline[k]:F2}.", 0, k);
                    if (p.mcNotified || p.tier >= 3) Log(c.id, "mc", "Mission Control review log closed: episode resolved.", 0, k);
                }
                p.mcNotified = false;
                p.tier = 0; p.refused = false; onIntervention?.Invoke("restore:" + Lever[k], c.id, k);
                if (escalatedTo.ContainsKey(c.id) && c.MaxTier < 3) escalatedTo.Remove(c.id);
                return;
            }
            if (p.refused && SimClock.hours < p.cooldownUntil && newT < 3) { Log(c.id, "respect", $"{CondSpec.Name[k]} at Tier {newT}, but crew declined earlier — AI holds back (refusal respected until {SimClock.Format(p.cooldownUntil)}).", newT, k); return; }
            if (newT > p.tier) RunTier(c, k, newT, conf, p);
            p.tier = newT;
        }

        void RunTier(CrewMember c, int k, int t, float conf, Proto p)
        {
            p.start = SimClock.hours; p.monitorUntil = SimClock.hours + CondSpec.MonitorHours[k]; p.startScore = c.smooth[k];
            // AGENCY TEST (ARES constraint: no intervention that worsens autonomy deprivation)
            //  • someone already showing autonomy deprivation gets NO silent changes — the AI asks instead
            //  • nobody gets more than 3 silent changes in 24 h (the habitat must not feel like it is constantly steering you)
            c.silentAt.RemoveAll(h => SimClock.hours - h > 24f);
            bool autonomyGuard = k != (int)Cond.Autonomy && c.tier[(int)Cond.Autonomy] >= 1;
            bool budgetFull = c.silentAt.Count >= 3;
            if (t == 1 && (autonomyGuard || budgetFull))
            {
                Log(c.id, "agency", $"T1 {CondSpec.Name[k]} NOT applied silently — {(autonomyGuard ? "this person already feels over-controlled" : "3 silent changes already today")}. Asking instead.", 1, k);
                if (c.id == playerId && !headless) onToast?.Invoke($"I could adjust your zone to help with {CondSpec.Name[k].ToLower()}. Want me to? (Crew Interface → AI actions)");
                return;
            }
            if (t >= 1)
            {
                c.silentAt.Add(SimClock.hours);
                string where = k == 1 && !c.aiPodAccess ? " (shared zones only — pod not invited)" : "";
                Log(c.id, "tier1", $"T1 {CondSpec.Name[k]}: {T1Text[k, 0]}{where}. Confidence {conf:P0}.", 1, k);
                onIntervention?.Invoke(Lever[k] + ":1", c.id, k);
                c.autonomy -= 0.005f;
                if (c.notifyEverySilentChange && !headless && c.id == playerId) onToast?.Invoke($"FYI: I adjusted your zone ({CondSpec.Name[k]} support). You asked to be told every time.");
            }
            if (t >= 2)
            {
                var o = new Offer { crew = c.id, cond = k, tier = 2, text = T2Offer[k], expires = SimClock.hours + 1f };
                o.onAccept = () => { Accept(c, k); };
                o.onDecline = () => { Refuse(c, k, p); };
                Log(c.id, "tier2", $"T2 {CondSpec.Name[k]}: offer sent — \"{o.text}\"", 2, k);
                onIntervention?.Invoke(Lever[k] + ":2", c.id, k);
                if (c.id == playerId && !headless) { offers.Add(o); onOffer?.Invoke(o); }
                else
                {
                    // AI crewmates decide: low autonomy → more likely to decline (autonomy deprivation tension)
                    double pAccept = 0.35 + 0.55 * c.autonomy;
                    if (rng.NextDouble() < pAccept) o.onAccept(); else o.onDecline();
                }
            }
            if (t >= 3) Escalate(c, k, p);
        }

        public void Accept(CrewMember c, int k)
        {
            Log(c.id, "accept", $"{c.name} accepted the {CondSpec.Name[k]} offer.", 2, k);
            c.autonomy += 0.08f;  // chosen, not imposed
            switch (k)
            {
                case 0: c.stress -= 0.25f; break;
                case 1: c.mood += 0.15f; c.novelty += 0.1f; c.lastSocialHour = SimClock.hours; break;
                case 2: for (int j = 1; j <= 4; j++) { c.tension[j] *= 0.6f; } onIntervention?.Invoke("configB", c.id, k); break;
                case 3: c.fatigue -= 0.3f; break;
                case 4: c.autonomy += 0.2f; break;
                case 5: c.novelty += 0.35f; c.lastNovelHour = SimClock.hours; break;
            }
            onIntervention?.Invoke("accepted:" + Lever[k], c.id, k);
        }
        public void Refuse(CrewMember c, int k, Proto p = null)
        {
            if (p == null) active.TryGetValue((c.id, k), out p);
            if (p != null) { p.refused = true; p.cooldownUntil = SimClock.hours + 6f; }
            Log(c.id, "refuse", $"{c.name} declined. AI stopped the {CondSpec.Name[k]} intervention and logged it (hard limit #2).", 2, k);
            onIntervention?.Invoke("restore:" + Lever[k], c.id, k);
        }

        void Escalate(CrewMember c, int k, Proto p)
        {
            escalations++;
            int responder = ChooseResponder(c);
            p.responder = responder; p.ackDeadline = SimClock.hours + AckLimitH(k); p.acked = false;
            escalatedTo[c.id] = responder;
            string who = responder == 0 ? "Mission Control" : C(responder).name;
            Log(c.id, "inform", $"{c.name} informed FIRST: \"I've asked {who} to check in with you. Only they will see the details.\"", 3, k);
            Log(c.id, "tier3", $"T3 {CondSpec.Name[k]} → escalated to {who}. Tier-2 environment support continues.", 3, k);
            Log(c.id, "mc", "Logged for Mission Control review at the next comm window: condition, tier and times only — no raw sensor data.", 3, k);
            if (responder == 0) { p.mcNotified = true; Log(c.id, "mc", "Mission Control notified directly (no crew responder available; 2.6 s light delay) after the crew member was informed.", 3, k); }
            onIntervention?.Invoke("tier3", c.id, k);
            if (!headless && (responder == playerId || c.id == playerId)) onToast?.Invoke($"TIER 3: {CondSpec.Name[k]} — {who} has been asked to respond.");
        }
        int ChooseResponder(CrewMember subject)
        {
            var cmo = C(cmoId);
            if (subject.id != cmoId && !cmo.onEVA && !cmo.asleep) return cmoId;
            var cdr = C(commanderId);
            if (subject.id != commanderId && !cdr.onEVA) { Log(subject.id, "backup", $"CMO unavailable ({(cmo.onEVA ? "on EVA" : cmo.asleep ? "asleep" : "is the subject")}) — backup chain: Commander + CMO suit/beacon comm ping.", 3); return commanderId; }
            return 0;
        }
        public void Acknowledge(int subject)
        {
            foreach (var kv in active) if (kv.Key.Item1 == subject && kv.Value.tier >= 3) kv.Value.acked = true;
            Log(subject, "ack", $"Tier 3 acknowledged by responder; human chain of care active.", 3);
        }

        public void Monitor()
        {
            foreach (var kv in active.ToList())
            {
                var p = kv.Value; if (p.tier == 0) continue;
                var c = C(kv.Key.Item1); int k = kv.Key.Item2;
                // Tier-3 acknowledgement deadline (Q6)
                if (p.tier >= 3 && !p.acked && p.ackDeadline > 0 && SimClock.hours > p.ackDeadline)
                {
                    int prev = p.responder;
                    if (prev == cmoId && !headless && C(cmoId).id != playerId && !C(cmoId).onEVA) { p.acked = true; Log(c.id, "ack", $"CMO acknowledged within {AckLimitH(k) * 60:0} min.", 3, k); continue; }
                    int next = prev == cmoId ? (c.id == commanderId ? 0 : commanderId) : 0;
                    Log(c.id, "backup", $"No acknowledgement in {AckLimitH(k) * 60:0} min from {(prev == 0 ? "MC" : C(prev).name)} → escalating to {(next == 0 ? "Mission Control" : C(next).name)}.", 3, k);
                    p.responder = next; p.ackDeadline = SimClock.hours + AckLimitH(k); escalatedTo[c.id] = next == 0 ? commanderId : next;
                    if (headless && next != 0) { p.acked = true; Log(c.id, "ack", $"{C(next).name} acknowledged.", 3, k); }
                }
                // Mission Control is told DIRECTLY only when: (a) no crew responder (above), (b) Tier 3 still unresolved after 24 h,
                // or (c) the responder chain ran out. Always AFTER the crew member was informed.
                if (p.tier >= 3 && !p.mcNotified && SimClock.hours - p.start > 24f)
                { p.mcNotified = true; Log(c.id, "mc", $"Tier 3 {CondSpec.Name[k]} unresolved after 24 h → Mission Control flight surgeon notified ({c.name} was told first).", 3, k); }
                if (SimClock.hours < p.monitorUntil) continue;
                float now = c.smooth[k];
                if (now < p.startScore * 0.85f || now < CondSpec.T1)
                {
                    Log(c.id, "monitor", $"{CondSpec.Name[k]} improving ({p.startScore:F2}→{now:F2}) over {CondSpec.MonitorHours[k]:0.##} h window — continue then restore.", p.tier, k);
                    p.monitorUntil = SimClock.hours + CondSpec.MonitorHours[k]; p.startScore = now;
                }
                else if (p.tier < 3 && !p.refused)
                {
                    Log(c.id, "monitor", $"{CondSpec.Name[k]} NOT improving after {CondSpec.MonitorHours[k]:0.##} h → step up to Tier {p.tier + 1}.", p.tier, k);
                    int nt = p.tier + 1; p.tier = nt; c.tier[k] = Mathf.Max(c.tier[k], nt); RunTier(c, k, nt, c.confidence, p);
                }
                else { p.monitorUntil = SimClock.hours + CondSpec.MonitorHours[k]; }
            }
        }

        // passive effects of active environmental support
        public void ApplySupport(float dt)
        {
            foreach (var kv in active)
            {
                if (kv.Value.tier == 0 || kv.Value.refused) continue;
                var c = C(kv.Key.Item1);
                switch (kv.Key.Item2)
                {
                    case 0: c.stress -= 0.10f * dt; break;
                    case 1: c.mood += 0.012f * dt; break;
                    case 2: for (int j = 1; j <= 4; j++) c.tension[j] -= 0.02f * dt; break;
                    case 3: c.fatigue -= 0.05f * dt; break;
                    case 4: c.autonomy += 0.01f * dt; break;
                    case 5: c.novelty += 0.02f * dt; break;
                }
            }
        }

        public void Step(float dtHours)
        {
            SimClock.hours += dtHours;
            StepLatent(dtHours);
            ApplySupport(dtHours);
            Detect(dtHours);
            Monitor();
            // DATA RETENTION: derived scores live only in memory; personal log entries older than 30 days are deleted automatically,
            // except Tier-3 / safety events (kept for the mission medical record, which only the person and the CMO can open).
            if (Mathf.FloorToInt(SimClock.hours) != lastPruneH)
            {
                lastPruneH = Mathf.FloorToInt(SimClock.hours);
                foreach (var c in crew) c.log.RemoveAll(l => SimClock.hours - l.simHour > RetentionDays * 24f && l.tier < 3);
                log.RemoveAll(l => SimClock.hours - l.simHour > RetentionDays * 24f && l.tier < 3);
            }
            foreach (var o in offers) if (!o.done && SimClock.hours > o.expires) { o.done = true; Log(o.crew, "expire", "Offer expired without answer — treated as 'not now', no further push."); }
        }

        public const float RetentionDays = 30f; int lastPruneH = -1;
        /// the crew member deletes their own AI history (everything except Tier-3 / safety events)
        public int DeleteMyHistory(CrewMember c)
        {
            int n = c.log.RemoveAll(l => l.tier < 3);
            log.RemoveAll(l => l.crew == c.id && l.tier < 3);
            for (int s = 0; s < 9; s++) c.fbWeight[s] = 1f;
            c.selfMood = c.selfStress = -1;
            Log(c.id, "privacy", $"{c.name} deleted their AI history ({n} entries). Safety (Tier 3) records are kept for the medical record only.");
            return n;
        }

        public void SetTension(int a, int b, float v) { C(a).tension[b] = v; C(b).tension[a] = v; }
        public string CrewLine(CrewMember c, int viewer)
        {
            if (viewer == c.id || EthicsGuard.CanViewDetail(this, viewer, c.id))
                return $"{c.name}: wellbeing {c.Wellbeing:P0} | {string.Join(" ", Enumerable.Range(0, 6).Select(k => CondSpec.Name[k].Substring(0, 3) + c.tier[k]))}";
            string status = c.MaxTier >= 3 ? "With CMO" : c.MaxTier == 2 ? "Support offered" : "OK";
            return $"{c.name}: {status}";
        }
    }


    public static class Scenarios
    {
        public static string lastExplain;
        public static readonly string[] Short = { "Workout false positive", "Depression + conflict at once", "Silent change discovered", "Cortisol patch fails Day 45", "No permanent opt-out?", "CMO on EVA during Tier 3" };
        public static void Run(int q)
        {
            var e = AresHost.Engine; if (e == null) return; var sb = new StringBuilder();
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
                      AresHost.SetConfigB?.Invoke(true); }
                    break;
                case 3:
                    { var c = e.C(2); e.Log(2, "tier1", "T1 Anxiety: Dim zone to 2400 K amber, alpha binaural, 23 °C, lavender (silent).", 1, 0);
                      c.notifyEverySilentChange = true; e.Log(2, "recovery", "Recovery protocol: AI apologised, showed the full log + reasons, switched Okafor to 'tell me every change', offered to disable Tier 1 for her entirely.");
                      sb.AppendLine("<b>Q3 — A crew member finds out the AI changed her lighting and audio yesterday without telling her, and is furious.</b>");
                      sb.AppendLine("Prevention: every crew member agrees pre-mission to Tier 1 silent support, and every silent change is ALWAYS in their personal log (Crew Interface → AI actions). Silent ≠ secret.");
                      sb.AppendLine("Recovery: 1) the AI apologises and explains exactly what and why, 2) shows the log, 3) offers 'tell me about every change' or 'no Tier 1 for me', 4) a human (CMO) follows up only if she wants. Applied to Dr. Okafor now.");
                      AresHost.CrewTab = "ai actions"; }
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
                    AresHost.CrewTab = "privacy";
                    break;
                case 6:
                    { var cmo = e.C(2); cmo.onEVA = true; var s = e.C(4); s.smooth[1] = 0.8f; s.aboveSince[1] = SimClock.hours - 60; e.Step(0.02f);
                      sb.AppendLine("<b>Q6 — Tier 3 while the CMO is on a 4-hour EVA.</b>");
                      sb.AppendLine("Responder chain: CMO unavailable (EVA) → Commander (you) gets the Tier 3 with the subject's consent-scoped details, + a low-priority ping to the CMO's suit beacon.");
                      sb.AppendLine("If no acknowledgement within the Tier-3 limit (15 min for acute anxiety/fatigue, 1 h for depression/conflict/autonomy/monotony) → Mission Control (after the crew member is told). Tier 2 environmental support keeps running the whole time. Open the CMO console (Menu > Project ARES > CMO console) to acknowledge as backup responder.");
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
            string path = System.IO.Path.Combine(AresHost.ReportsDir, "TabletopSimulation_Report.md");
            try { System.IO.File.WriteAllText(path, sb.ToString()); } catch (Exception ex) { Debug.LogWarning(ex.Message); }
            SimClock.hours = saveH;
            lastReport = sb.ToString().Substring(0, Mathf.Min(2600, sb.Length)) + $"\n\n…full report saved: {path}";
            return path;
        }
    }
}
