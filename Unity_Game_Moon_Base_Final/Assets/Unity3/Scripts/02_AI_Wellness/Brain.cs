// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/02_AI_Wellness/Brain.cs
//  Section : 02_AI_Wellness
//  Folder  : The AI psychological-monitoring engine: 9 sensors -> 6 condition scores -> 3 response
//            tiers, hard limits, the human OFF switch, the 70-mission threshold check and the
//            72-hour tabletop.
//  Purpose : Hard limits: what the AI is NEVER allowed to do without human authorisation.
//  Why     : Chat: 'include the mood simulator and attachment to astronaut' — wearables feed this
//            engine.
//  Contains: SimClock, Offer, EthicsGuard, MoodEngine, Proto, Sim
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Hunch3
{
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
            crew.Add(new CrewMember { id = 1, name = "Cmdr. J. Rana (you)", role = "Commander", moodColor = new Color(0.2f, 1f, 0.5f) });
            crew.Add(new CrewMember { id = 2, name = "Dr. L. Okafor", role = "Chief Medical Officer", moodColor = new Color(0.2f, 0.6f, 1f) });
            crew.Add(new CrewMember { id = 3, name = "Eng. K. Tanaka", role = "Flight Engineer", moodColor = new Color(1f, 0.75f, 0.2f) });
            crew.Add(new CrewMember { id = 4, name = "Dr. S. Rivera", role = "Mission Specialist (Geology)", moodColor = new Color(0.85f, 0.35f, 1f) });
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

    /// Scene wrapper that drives the engine in real time.
    public class Sim : MonoBehaviour
    {
        public static Sim I; public MoodEngine engine;
        public float stepMinutes = 1f; float acc;
        public static event Action<Offer> OfferRaised; public static event Action<string> Toast;
        void Awake()
        {
            I = this; engine = new MoodEngine(UnityEngine.Random.Range(1, 9999));
            engine.onOffer = o => OfferRaised?.Invoke(o);
            engine.onToast = s => Toast?.Invoke(s);
            engine.onIntervention = (lever, crew, cond) => { if (HabitatEnvironment.I) HabitatEnvironment.I.Apply(lever, crew, cond); };
            SimClock.hours = 7f; EthicsGuard.lastReviewHour = 0; EthicsGuard.blocked = 0; EthicsGuard.blockedLog.Clear();
        }
        void Update()
        {
            acc += Time.deltaTime * GameState.timeScale / 60f;  // sim minutes elapsed
            int guard = 0;
            while (acc >= stepMinutes && guard++ < 600) { acc -= stepMinutes; engine.Step(stepMinutes / 60f); }
        }
        public static void Say(string s) => Toast?.Invoke(s);
    }
}
