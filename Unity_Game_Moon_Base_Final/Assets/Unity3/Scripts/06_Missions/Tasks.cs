// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/06_Missions/Tasks.cs
//  Section : 06_Missions
//  Folder  : Seven story missions and the rubric task list — the 'one next step' banner.
//  Purpose : RUBRIC TASKS — 20 concrete things to do in the habitat, one or more for every rubric
//            area (D1–D5 + both bonuses). Each task ticks itself off from what really happened in
//            the simulation (the AI's event log, the panels you opened,
//  Contains: RubricTasks, Task, CrewHealthCard, Row
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hunch3
{
    /// RUBRIC TASKS — 20 concrete things to do in the habitat, one or more for every rubric area (D1–D5 + both bonuses).
    /// Each task ticks itself off from what really happened in the simulation (the AI's event log, the panels you opened,
    /// where you walked), so there is never any doubt whether a task was completed.
    ///   • Top of the screen: the ONE task to do now, how to do it, and a 20-segment progress bar.
    ///   • J (or Q menu → Rubric tasks): the full checklist with "Guide me" (walks you there / opens the right screen)
    ///     and "Show me" (makes the situation happen now, e.g. raises a real Tier-2 offer for you to answer).
    ///   • Progress is saved between sessions and written to Reports/rubric_tasks.txt with times, as proof.
    public partial class RubricTasks : MonoBehaviour
    {
        public static RubricTasks I;
        public class Task
        {
            public string id, sec, title, how;
            public Func<bool> check; public Func<string> progress;
            public Action guide, demo; public string demoLabel = "Show me";
            public bool done; public string doneAt = "";
        }
        public static readonly (string code, string name, int pts)[] Sections = {
            ("D1", "Sensor architecture", 35), ("D2", "Habitat spatial design", 35), ("D3", "AI response protocols", 50),
            ("D4", "Ethics & hard limits", 40), ("D5", "Presentation", 25), ("Bonus A", "Tabletop simulation", 10), ("Bonus B", "Crew interface", 10) };
        public readonly List<Task> tasks = new List<Task>();
        // colour code for every rubric area (used in the banner, the checklist and the done flash)
        public static string SecHex(string code) => code switch { "D1" => "4FC3F7", "D2" => "81C784", "D3" => "FFB74D", "D4" => "FF6E6E", "D5" => "CE93D8", "Bonus A" => "FFD54F", _ => "F48FB1" };
        /// bold + yellow for every key or button you must press, cyan for screen names after an arrow
        public static string Hi(string s)
        {
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\b(F1[0-2]?|F[2-9]|Tab|Esc|Space|Y|N|O|B|J|V|E|M|T|X|U|P|Q)\b(?=[ ,\.\)/(]| =|$)", m =>
                (m.Index > 0 && char.IsLetter(s[m.Index - 1])) ? m.Value : $"<b><color=#FFD54F>{m.Value}</color></b>");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"→ ([^.(),→]+?)(?= →|[.(),]|$)", m => $"→ <b><color=#80DEEA>{m.Groups[1].Value}</color></b>");
            return s;
        }
        public int Done => tasks.Count(t => t.done);
        public Task Current => focus != null && !focus.done ? focus : tasks.FirstOrDefault(t => !t.done);
        Task focus;

        // what the simulation has shown so far
        readonly HashSet<string> zoneTypesSeen = new HashSet<string>();
        static readonly string[] AresTypes = { "communal", "private", "work", "exercise", "nature", "transition" };
        bool sawExercise, sawMap, sawDashboard, sawCrewUI, sawMatrix;
        readonly HashSet<string> events = new HashSet<string>();
        LogEntry lastSeen; float nextCheck;

        // UI
        Canvas bannerCanvas, listCanvas; Image banner; Text bTop, bTitle, bHow; readonly List<Image> segs = new List<Image>();
        Image flash; float flashUntil; Text flashText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) Make(); };
            if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) Make();
        }
        static void Make() { if (!FindFirstObjectByType<RubricTasks>()) new GameObject("RubricTasks").AddComponent<RubricTasks>(); }
        void Awake() { I = this; }

        MoodEngine E => Sim.I != null ? Sim.I.engine : null;
        static void Nav(string wp, string label) { Panels.CloseAll(); if (global::Hunch3.Nav.wp.ContainsKey(wp)) HUD.I?.SetNavTarget(wp, label); else HUD.I?.Toast("Can't find that place on the map."); }
        static void NavZone(Func<Zone, bool> pick, string label)
        {
            var env = HabitatEnvironment.I; var pr = PlayerRig.I; if (!env || !pr) return;
            var z = env.zones.Where(pick).OrderBy(q => (q.center - pr.FeetPosition).sqrMagnitude).FirstOrDefault();
            if (z == null) { HUD.I?.Toast("No " + label + " zone found."); return; }
            string wp = global::Hunch3.Nav.Nearest(z.center); if (wp != null) Nav(wp, label + " — " + z.name.Replace("_", " "));
        }
        static void OpenCrew(string tab) { Panels.crewTab = tab; Panels.Open("crew"); }

        void Start()
        {
            Build();
            Load();
            BuildMissions(); LoadMissions();
            BuildBanner();
            Scan(true);
            Invoke(nameof(Hello), 4f);
        }
        void Hello()
        {
            if (GameState.mode != GameMode.Sandbox) return;
            HUD.I?.Toast($"Rubric tasks: {Done}/{tasks.Count} done. The task at the top of the screen is your next job — press Shift+J for the full checklist.");
        }

        // ------------------------------------------------------------------ the 20 tasks
        void Add(string id, string sec, string title, string how, Func<bool> check, Action guide = null, Action demo = null, Func<string> progress = null, string demoLabel = "Show me")
            => tasks.Add(new Task { id = id, sec = sec, title = title, how = how, check = check, guide = guide, demo = demo, progress = progress, demoLabel = demoLabel });

        void Build()
        {
            // D1 — sensors (start here: the two easiest, hands-on tasks)
            Add("checkin", "D1", "Check on a crewmate's health (collect their sensor data)",
                "Walk up to any crewmate and press I (VR: menu → Crew wellness). You read their wristband, skin patch and room sensors — after they say yes.",
                () => HealthCheck.scanned.Count > 0, () => Panels.Open("orders"), () => HealthCheck.Quick(), null, "Check now");
            Add("help", "D1", "Help a crewmate's mental health with one task",
                "Press I and tap the green ♥ Help button under a crewmate. They do the task that helps them most (calm, call home, rest …).",
                () => CrewOrders.done > 0 || CrewOrders.active.Count > 0, () => Panels.Open("orders"),
                () => { int id = HealthCheck.lastId > 0 ? HealthCheck.lastId : 2; CrewOrders.LetThemPick(id); Panels.Open("orders"); }, null, "Help now");
            Add("dash", "D1", "Open the AI Dashboard and read the sensor health matrix",
                "Press F1 (VR: menu → AI Dashboard). Find the 9 sensors and whether each is OK, degraded or failed.",
                () => sawDashboard, () => Panels.Open("dashboard"));
            Add("sensor", "D1", "Break a sensor and watch graceful degradation",
                "F1 dashboard → press \"Inject: Tanaka cortisol patch FAILS\". The AI re-weights the other sensors and lowers its confidence instead of raising a false alarm.",
                () => events.Contains("sensor"), () => Panels.Open("dashboard"),
                () => { var e = E; if (e == null) return; e.C(3).sensors[(int)Sensor.Cortisol] = SensorHealth.Failed; e.Log(3, "sensor", "Cortisol patch failed — degraded mode (weights renormalised, confidence ↓)."); }, null, "Break it now");
            Add("exercise", "D1", "Exercise in the Exercise bay (no false alarm)",
                "Walk to the Exercise bay and press E on the treadmill, bike or resistance machine. Your heart-rate rise is tagged as exercise, so the AI does NOT treat it as anxiety.",
                () => sawExercise, () => NavZone(z => z.type == "exercise", "Exercise bay"));
            Add("matrix", "D1", "Read the sensor-to-condition matrix (D1)",
                "Press F11 (VR: menu → ARES deliverables) and page through D1: every sensor's signal, threshold, 2+ failure modes, mass and power.",
                () => sawMatrix, () => { DesignDocs.Jump("D1 Sensors"); DesignDocs.page++; Panels.Open("docs"); });
            // D2 — spatial design
            Add("zones", "D2", "Visit all 6 ARES zones",
                "Walk into a communal, private, work, exercise, nature and transition zone. The zone card (top right) names each one and why it is designed that way.",
                () => AresTypes.All(zoneTypesSeen.Contains),
                () => { var miss = AresTypes.FirstOrDefault(t => !zoneTypesSeen.Contains(t)); if (miss != null) NavZone(z => z.type == miss, miss + " zone"); },
                null, () => $"{AresTypes.Count(zoneTypesSeen.Contains)}/6 · still to visit: " + string.Join(", ", AresTypes.Where(t => !zoneTypesSeen.Contains(t))));
            Add("dome", "D2", "Walk the quarters hallway into your own sleep dome",
                "Follow the hallway from the Crew Support node into Quarters 1 — your private dome (no cameras inside, door sensor only).",
                () => PlayerRig.I && PlayerRig.I.zone != null && PlayerRig.I.zone.name == "Private_Quarters1", () => Nav("WP_SQ1_In", "Your sleep dome (Quarters 1)"));
            Add("configB", "D2", "Switch the base to Configuration B (conflict layout)",
                "Q menu → \"Config A ↔ B\". Acoustic screens slide out in the wardroom, a second dining point opens and the alternate route lights up.",
                () => GameState.configB, () => Panels.Open("menu"));
            // D3 — AI response protocols
            Add("tier1", "D3", "See a Tier 1 silent adjustment",
                "Speed up time (T) and watch the AI Dashboard log: when a crew member's stress builds, the AI quietly changes light, sound, temperature or scent in their zone.",
                () => events.Contains("tier1"), () => Panels.Open("dashboard"),
                () => { var c = E?.C(3); if (c != null) { c.stress = 0.95f; c.cortisol = 0.85f; } GameState.timeScale = Mathf.Max(GameState.timeScale, 600f); HUD.I?.Toast("Tanaka's stress is rising (time ×10). Watch for the AI's silent Tier 1 change — F1 log."); }, null, "Make it happen");
            Add("tier2", "D3", "Answer a Tier 2 offer (Y = yes · N = no)",
                "When the AI offers YOU support (a box at the bottom of the screen), choose yes or no. It never decides for you.",
                () => events.Contains("answer1"), null, () => RaiseOfferForMe(), null, "Send me an offer");
            Add("meeting", "D3", "Run a team meeting at the wardroom table",
                "Go to the wardroom table (upper deck) and press E, or press F3. Pick one team action (e.g. let everyone choose their task order).",
                () => events.Contains("meeting"), () => Nav("WP_Up_Table", "Wardroom table (team meeting)"));
            Add("tier3", "D3", "Handle a Tier 3 escalation in the CMO console",
                "\"Show me\" runs board question Q6 (CMO on EVA, so YOU are the backup). Then F2 → press \"Acknowledge\" for the crew member.",
                () => events.Contains("ack"), () => Panels.Open("cmo"),
                () => { Scenarios.Run(6); Panels.Open("cmo"); }, null, "Start one");
            Add("drill", "D3", "Lead a safety drill to the muster point",
                "Press F6 (or Q menu → Safety drill). Follow the green arrow to the muster point; the drill ends ALL CLEAR when everyone is there.",
                () => events.Contains("allclear"), null,
                () => { Panels.CloseAll(); if (Hazards.I && !Hazards.I.Active) Hazards.I.Trigger(Hazards.Kinds[UnityEngine.Random.Range(0, Hazards.Kinds.Length)], "DRILL"); }, null, "Start drill");
            Add("sleep", "D3", "Sleep in your bed (AI night-light + sleep sounds)",
                "Go to your sleep dome (B = find my bed), press E on the bed. The AI dims to a deep-amber night light, plays soft sleep sounds and wakes you with a sunrise ramp. 1 real minute = the night.",
                () => events.Contains("slept"), () => { if (BedFinder.I) BedFinder.I.Show(true); else Nav("WP_SQ1_Bed", "Your bed"); });
            // D4 — ethics & hard limits
            Add("decline", "D4", "Say NO to an AI offer — it must stop and log it",
                "When an offer appears, press N (VR: B). Hard limit #2: the AI stops that intervention at once and writes it in your log.",
                () => events.Contains("refuse1"), null, () => RaiseOfferForMe(), null, "Send me an offer");
            Add("optout", "D4", "Opt out of a sensor (privacy control)",
                "Shift+Tab → PRIVACY → opt out of voice, face, cortisol or skin sensor. Heart rate and CO2 stay on (the safety floor) and the screen explains why.",
                () => events.Contains("optout"), () => OpenCrew("privacy"));
            Add("override", "D4", "Override the AI",
                "Shift+Tab → OVERRIDES → \"Restore my room to default\" or \"Pause ALL AI adjustments 24 h\". The human always wins.",
                () => events.Contains("override"), () => OpenCrew("overrides"));
            Add("export", "D4", "Export everything the AI recorded about you",
                "Shift+Tab → PRIVACY → \"Export my full log\". Transparency: every crew member can see all their own data.",
                () => events.Contains("export"), () => OpenCrew("privacy"));
            Add("aioff", "D4", "Stop a misbehaving AI — disallow AI control",
                "When the AI oversteps (\"Show me\" makes it blast bright light and music into your quarters), press O (VR: menu → AI off switch). It becomes watch-only and every change is undone.",
                () => events.Contains("aioff"), null, () => AIAuthority.Misbehave(), null, "Make AI misbehave");
            Add("review", "D4", "Sign off the weekly human review of the AI",
                "F1 dashboard → \"Human system review ✓\". Without it the AI drops back to Tier 1 after 7 days.",
                () => events.Contains("review"), () => Panels.Open("dashboard"));
            // D5 — presentation
            Add("board", "D5", "Answer ALL 6 Agency Review Board questions live (Q1–Q6)",
                "Press F5 and run each question. Each one is set up live and the screen explains how the design answers it — use this in your presentation.",
                () => BoardDone().Count >= 6, () => Panels.Open("scenarios"), null, () => BoardDone().Count + "/6");
            Add("map", "D5", "Show the habitat map",
                "Press M (VR: X) for the big map with every zone. Good for walking the judges through the layout.",
                () => sawMap, () => { Panels.CloseAll(); HUD.I?.ToggleBigMap(); sawMap = true; });
            // bonuses
            Add("tabletop", "Bonus A", "Run the 72-hour tabletop simulation",
                "F4 → \"Run 72-hour simulation\". Two events at once + a sensor failure + CMO on EVA; the report is saved in Reports/.",
                () => !string.IsNullOrEmpty(TabletopSim.lastReport), () => Panels.Open("tabletop"));
            Add("crewui", "Bonus B", "Open the Crew Interface — your data and your controls",
                "Press Shift+Tab (VR: Y). See your wellness, what the AI did for you, overrides, privacy and preferences.",
                () => sawCrewUI, () => OpenCrew("wellness"));
        }

        /// A real Tier-2 offer for the Commander (fatigue protocol), wired to the engine's own Accept / Refuse.
        void RaiseOfferForMe()
        {
            var e = E; if (e == null) return;
            if (e.offers.Any(o => o.crew == 1 && !o.done)) { HUD.I?.Toast("You already have an offer waiting — answer it with Y or N."); return; }
            var c = e.C(1); c.fatigue = Mathf.Max(c.fatigue, 0.75f);
            var off = new Offer { crew = 1, cond = (int)Cond.Fatigue, tier = 2, text = "You've been working a long stretch. Take a 20-minute rest break in your quarters now?", expires = SimClock.hours + 1f };
            off.onAccept = () => e.Accept(c, (int)Cond.Fatigue); off.onDecline = () => e.Refuse(c, (int)Cond.Fatigue);
            e.Log(1, "tier2", $"T2 {CondSpec.Name[(int)Cond.Fatigue]}: offer sent — \"{off.text}\"", 2, (int)Cond.Fatigue);
            e.offers.Add(off); e.onOffer?.Invoke(off);
        }

        // ------------------------------------------------------------------ EASY MODE: one key (ENTER) does the task with you
        // Each press does the next step of the current task: it takes you there, opens the right screen, sets the situation
        // up and — where the task needs a choice from you (yes/no to an offer, acknowledging a Tier 3) — the next press makes it.
        static void Go(string wp) { Panels.CloseAll(); if (global::Hunch3.Nav.wp.TryGetValue(wp, out var t) && PlayerRig.I) PlayerRig.I.Teleport(t.position); }
        bool misbehaved;
        public void DoNext() { if (SleepSystem.PlayerAsleep) { HUD.I?.Toast("You're asleep… (E wakes you)"); return; } if (AlarmDo() || InChargeDo()) return; if (InMission) { MissionDo(); return; } if (Current == null && DailyCur != null) { DailyDo(); return; } if (Current == null && DailyCur == null && NightNow && !SleepSystem.PlayerAsleep) { Go("WP_SQ1_Bed"); SleepSystem.I?.BeginSleep(); return; } var t = Current; if (t == null) { HUD.I?.Toast("All rubric tasks are done ✓"); return; } DoTask(t); }
        public void DoTask(Task t)
        {
            var e = E; if (e == null || t == null || t.done) return; var me = e.C(1);
            var off = e.offers.FirstOrDefault(o => o.crew == 1 && !o.done);
            if (off != null && (t.id == "tier2" || t.id == "decline")) { HUD.I?.AnswerOffer(t.id == "tier2"); return; }
            try
            {
                switch (t.id)
                {
                    case "exercise": Go("WP_D_Ex"); me.exercising = true; me.lastExerciseHour = SimClock.hours; sawExercise = true; Invoke(nameof(StopExercise), 8f); HUD.I?.Toast("You're on the exercise bike. Heart rate up — tagged as EXERCISE, so the AI does not call it anxiety."); break;
                    case "zones":
                        {
                            var miss = AresTypes.FirstOrDefault(z => !zoneTypesSeen.Contains(z)); var env = HabitatEnvironment.I; var pr = PlayerRig.I; if (miss == null || !env || !pr) break;
                            var zz = env.zones.Where(z => z.type == miss).OrderBy(z => (z.center - pr.FeetPosition).sqrMagnitude).FirstOrDefault(); if (zz == null) break;
                            string wp = global::Hunch3.Nav.Nearest(zz.center); if (wp != null) Go(wp); zoneTypesSeen.Add(miss);
                            HUD.I?.Toast($"This is a {miss.ToUpper()} zone: {zz.name.Replace("_", " ")}. Press ENTER for the next one."); break;
                        }
                    case "dome": Go("WP_SQ1_In"); break;
                    case "configB": HabitatEnvironment.I?.SetConfigB(true); break;
                    case "tier2": case "decline": RaiseOfferForMe(); HUD.I?.Toast(t.id == "tier2" ? "The AI is offering you support. Press ENTER again to say YES (or Y)." : "Press ENTER again to say NO (or R) — the AI must stop and log it."); break;
                    case "meeting": Go("WP_Up_Table"); foreach (var c in e.crew) c.autonomy += 0.15f; e.Log(0, "meeting", "Team chose their own task order (autonomy ↑)."); break;
                    case "tier3":
                        if (e.escalatedTo.Any(kv => kv.Value == 1)) e.Acknowledge(e.escalatedTo.First(kv => kv.Value == 1).Key);
                        else { Scenarios.Run(6); Panels.Open("cmo"); HUD.I?.Toast("Tier 3 while the CMO is on EVA — YOU are the backup. Close this and press ENTER again to acknowledge."); }
                        break;
                    case "drill":
                        if (Hazards.I && !Hazards.I.Active) { t.demo?.Invoke(); HUD.I?.Toast("Drill started. Press ENTER again to go straight to the muster point."); }
                        else if (Hazards.I && !Near(Hazards.I.Muster)) Go(Hazards.I.Muster);
                        else HUD.I?.Toast("You're at the muster point. Waiting for the crew to arrive…");
                        break;
                    case "sleep": if (SleepSystem.PlayerAsleep) { HUD.I?.Toast("You're asleep… 1 real minute = the whole night (E wakes you early)."); break; } Go("WP_SQ1_Bed"); SleepSystem.I?.BeginSleep(); break;
                    case "optout": me.optOut[(int)Sensor.Voice] = true; e.Log(1, "privacy", "Opted out of " + SensorSpec.Name[(int)Sensor.Voice] + " (heart rate and CO2 stay on: safety floor)."); HUD.I?.Toast("Voice sensing is off for you. Heart rate and CO2 stay on — they're safety, not mood."); break;
                    case "override": me.pauseUntilHour = SimClock.hours + 24; e.Log(1, "pause", "Crew paused all AI adjustments for 24 h."); HUD.I?.Toast("All AI adjustments paused for 24 h. The human always wins."); break;
                    case "export":
                        {
                            var sb = new StringBuilder($"# Personal data log — {me.name}\nExported {SimClock.Now}.\n\n"); foreach (var l in me.log) sb.AppendLine($"- {l.Time} [{l.kind}] T{l.tier} {l.text}");
                            string dir = GameState.ReportsDir, path = Path.Combine(dir, "MyDataLog_Commander.md"); File.WriteAllText(path, sb.ToString());
                            e.Log(1, "privacy", "Full personal log exported to " + path); HUD.I?.Toast("Exported: " + path); break;
                        }
                    case "aioff":
                        if (AIAuthority.Blocked) break;
                        if (!misbehaved) { misbehaved = true; AIAuthority.Misbehave(); HUD.I?.Toast("The AI is overstepping! Press ENTER again (or O) to switch it off."); }
                        else AIAuthority.Toggle();
                        break;
                    case "review": EthicsGuard.lastReviewHour = SimClock.hours; e.Log(0, "review", "Human system review completed by Commander."); break;
                    case "board": { var dn = BoardDone(); int q = Enumerable.Range(1, 6).First(i => !dn.Contains(i)); Scenarios.Run(q); boardQs.Add(q); Panels.Open("scenarios"); HUD.I?.Toast($"Board question Q{q} answered ({dn.Count + 1}/6). Press ENTER for the next one."); break; }
                    case "tabletop": TabletopSim.Run(); Panels.Open("tabletop"); break;
                    case "map": sawMap = true; t.guide?.Invoke(); break;
                    case "checkin": HealthCheck.Quick(); break;
                    case "help": t.demo?.Invoke(); break;
                    default: if (t.demo != null) t.demo(); else t.guide?.Invoke(); break;     // dash, sensor, matrix, tier1, crewui
                }
            }
            catch (Exception ex) { HUD.I?.Toast("Couldn't do that one automatically: " + ex.Message); }
        }

        /// which board questions (1..6) have been run, from the log
        readonly HashSet<int> boardQs = new HashSet<int>();      // kept here: running a scenario resets the engine (and its log)
        public HashSet<int> BoardDone() { var h = boardQs; var e = E; if (e != null) foreach (var l in e.log) if (l.kind == "scenario" && l.text.StartsWith("Board scenario Q") && l.text.Length > 16 && char.IsDigit(l.text[16])) h.Add(l.text[16] - '0'); return h; }

        // ------------------------------------------------------------------ checking
        void Scan(bool all = false)
        {
            var e = E; if (e == null) return;
            int start = 0;
            if (!all && lastSeen != null) { int k = e.log.LastIndexOf(lastSeen); start = k >= 0 ? k + 1 : 0; }
            for (int i = start; i < e.log.Count; i++)
            {
                var l = e.log[i]; string t = l.text ?? "";
                switch (l.kind)
                {
                    case "sensor": case "tier1": case "meeting": case "ack": case "review": case "scenario": events.Add(l.kind); break;
                    case "override": case "pause": events.Add("override"); break;
                    case "accept": if (l.crew == 1) events.Add("answer1"); break;
                    case "refuse": if (l.crew == 1) { events.Add("answer1"); events.Add("refuse1"); } break;
                    case "alarm": if (t.Contains("ALL CLEAR")) events.Add("allclear"); break;
                    case "sleep": if (t.StartsWith("Slept")) events.Add("slept"); break;
                    case "authority": if (t.Contains("DISALLOWED")) events.Add("aioff"); break;
                    case "privacy": if (t.Contains("exported")) events.Add("export"); else if (t.StartsWith("Opted out") || t.Contains("opted out") || t.StartsWith("Opte")) events.Add("optout"); break;
                }
            }
            if (e.log.Count > 0) lastSeen = e.log[e.log.Count - 1];
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.jKey.wasPressedThisFrame && kb.shiftKey.isPressed) ToggleList();     // J alone = the full task board (HabitatOps)
                if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && !listCanvas && !SleepSystem.PlayerAsleep) DoNext();
                if (kb.oKey.wasPressedThisFrame) AIAuthority.Toggle();
                if (kb.mKey.wasPressedThisFrame) sawMap = true;
            }
            var xl = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
            if (xl.isValid && xl.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool x) && x) sawMap = true;   // X = map in VR

            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + 0.5f;
                var pr = PlayerRig.I;
                if (pr && pr.zone != null && !string.IsNullOrEmpty(pr.zone.type)) zoneTypesSeen.Add(pr.zone.type);
                var e = E; if (e != null && e.C(1).exercising) sawExercise = true;
                if (GameObject.Find("Panel_dashboard")) sawDashboard = true;
                if (GameObject.Find("Panel_crew")) sawCrewUI = true;
                if (GameObject.Find("Panel_docs") && DesignDocs.Current.section == "D1 Sensors" && DesignDocs.page >= 1) sawMatrix = true;
                Scan();
                try { MissionTick(); } catch (Exception ex) { HLog.W("Missions", ex.Message); }
                foreach (var t in tasks)
                {
                    if (t.done) continue;
                    bool ok = false; try { ok = t.check(); } catch { }
                    if (ok) Complete(t);
                }
                RefreshBanner();
                if (listCanvas) RefreshList();
            }
            if (flash) { float k = Mathf.Clamp01((flashUntil - Time.time) / 3.5f); flash.gameObject.SetActive(k > 0); flash.color = new Color(0.07f, 0.45f, 0.21f, Mathf.Min(1f, k * 3f)); }
        }

        void Complete(Task t)
        {
            t.done = true; t.doneAt = $"{SimClock.Now} (real {DateTime.Now:HH:mm})";
            if (focus == t) focus = null;
            var sec = Sections.First(s => s.code == t.sec);
            int secDone = tasks.Count(q => q.sec == t.sec && q.done), secAll = tasks.Count(q => q.sec == t.sec);
            if (flashText) flashText.text = $"<b>✓ TASK DONE!</b>   <b>{t.title}</b>\n<size=18><b>{t.sec} {sec.name}</b>: {secDone}/{secAll} tasks shown   ·   {Done}/{tasks.Count} overall</size>";
            flashUntil = Time.time + 3.5f;
            SpaceRealism.I?.Quindar();
            E?.Log(0, "task", $"Rubric task done ({t.sec}): {t.title}");
            Save();
            if (Done == tasks.Count) HUD.I?.Toast("🏁 ALL RUBRIC TASKS DONE — every rubric area has been demonstrated in the simulator. Report saved in Reports/rubric_tasks.txt.");
        }

        // ------------------------------------------------------------------ saving + proof file
        const string PrefKey = "hunch3_rubric_tasks_v1";
        void Load()
        {
            var s = PlayerPrefs.GetString(PrefKey, "");
            foreach (var part in s.Split('|'))
            {
                var kv = part.Split('='); if (kv.Length < 1) continue;
                var t = tasks.FirstOrDefault(q => q.id == kv[0]); if (t == null) continue;
                t.done = true; t.doneAt = kv.Length > 1 ? kv[1] : "";
            }
        }
        void Save()
        {
            PlayerPrefs.SetString(PrefKey, string.Join("|", tasks.Where(t => t.done).Select(t => t.id + "=" + t.doneAt.Replace("|", "/").Replace("=", "-"))));
            PlayerPrefs.Save();
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"RUBRIC TASKS — Lunar Smart Adaptive Habitat — {Done}/{tasks.Count} done — written {DateTime.Now}");
                foreach (var sec in Sections)
                {
                    var list = tasks.Where(t => t.sec == sec.code).ToList(); if (list.Count == 0) continue;
                    sb.AppendLine($"\n{sec.code} · {sec.name} ({sec.pts} pts) — {list.Count(t => t.done)}/{list.Count}");
                    foreach (var t in list) sb.AppendLine($"  [{(t.done ? "x" : " ")}] {t.title}{(t.done ? "   — done " + t.doneAt : "")}");
                }
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports")); Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "rubric_tasks.txt"), sb.ToString());
            }
            catch { }
        }
        public void ResetAll()
        {
            foreach (var t in tasks) { t.done = false; t.doneAt = ""; }
            zoneTypesSeen.Clear(); events.Clear(); boardQs.Clear(); sawExercise = sawMap = sawDashboard = sawCrewUI = sawMatrix = false;
            var e = E; lastSeen = e != null && e.log.Count > 0 ? e.log[e.log.Count - 1] : null;   // only count what happens from now on
            focus = null; Save(); ResetMissions(); HUD.I?.Toast("Missions and rubric checklist reset — start again from mission 1.");
        }

        // ------------------------------------------------------------------ banner (the one thing to do now)
        void BuildBanner()
        {
            var pr = PlayerRig.I; bool vr = pr && pr.isXR;
            bannerCanvas = UIKit.Canvas("RubricTaskBanner", vr, new Vector2(760, 132));
            if (vr && pr.cam) { bannerCanvas.transform.localScale = Vector3.one * 0.0009f; LazyFollow(true); }   // VR: floats in front of you (not glued to your face)
            else bannerCanvas.sortingOrder = 12;
            var root = bannerCanvas.transform; var TC = new Vector2(0.5f, 1f);
            banner = UIKit.Panel(root, "Banner", vr ? new Vector2(0.5f, 0.5f) : TC, vr ? Vector2.zero : new Vector2(0, -14), new Vector2(760, 132), new Color(0.03f, 0.06f, 0.13f, 0.9f));
            UIKit.Round(banner, 0.6f); var b = banner.transform;
            UIKit.Round(UIKit.Panel(b, "Accent", new Vector2(0, 0.5f), new Vector2(5, 0), new Vector2(8, 100), UIKit.Gold, new Vector2(0, 0.5f)), 3f);   // gold edge = "this is your task"
            bTop = UIKit.Label(b, "", 16, UIKit.Gold, new Vector2(0, 1), new Vector2(18, -7), new Vector2(728, 22), TextAnchor.UpperLeft, new Vector2(0, 1));
            bTitle = UIKit.Label(b, "", 24, Color.white, new Vector2(0, 1), new Vector2(18, -30), new Vector2(728, 32), TextAnchor.UpperLeft, new Vector2(0, 1));
            bHow = UIKit.Label(b, "", 16, new Color(0.6f, 0.9f, 1f), new Vector2(0, 1), new Vector2(18, -64), new Vector2(728, 44), TextAnchor.UpperLeft, new Vector2(0, 1));
            float segW = 728f / tasks.Count;
            for (int i = 0; i < tasks.Count; i++)
            {
                var s = UIKit.Round(UIKit.Panel(b, "Seg" + i, new Vector2(0, 0), new Vector2(18 + i * segW, 10), new Vector2(segW - 4, 10), Color.grey, new Vector2(0, 0)), 4f);
                segs.Add(s);
            }
            // green "done" flash on top
            flash = UIKit.Round(UIKit.Panel(root, "DoneFlash", vr ? new Vector2(0.5f, 0.5f) : TC, vr ? Vector2.zero : new Vector2(0, -14), new Vector2(760, 132), new Color(0.1f, 0.55f, 0.25f, 0.92f)), 0.6f);
            flashText = UIKit.Label(flash.transform, "", 26, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(730, 120), TextAnchor.MiddleCenter);
            flash.gameObject.SetActive(false);
            RefreshBanner();
        }

        /// VR comfort: the banner stays still in the world and only glides back in front of you when you turn away or walk off.
        void LazyFollow(bool snap)
        {
            var pr = PlayerRig.I; if (!pr || !pr.cam || !bannerCanvas) return;
            var c = pr.cam.transform; var f = c.forward; f.y = 0; if (f.sqrMagnitude < 1e-4f) return; f.Normalize();
            Vector3 want = c.position + f * 1.8f + Vector3.up * 0.42f;
            var t = bannerCanvas.transform;
            Vector3 toB = t.position - c.position; toB.y = 0;
            bool off = snap || Vector3.Angle(f, toB) > 32f || Mathf.Abs(toB.magnitude - 1.8f) > 0.7f || Mathf.Abs(t.position.y - want.y) > 0.5f;
            if (off) followUntil = Time.time + 0.6f;
            if (snap) { t.position = want; t.rotation = Quaternion.LookRotation(want - c.position); return; }
            if (Time.time < followUntil)
            {
                t.position = Vector3.Lerp(t.position, want, Time.deltaTime * 4f);
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(t.position - c.position), Time.deltaTime * 6f);
            }
        }
        float followUntil;

        void LateUpdate() { var pr = PlayerRig.I; if (pr && pr.isXR) LazyFollow(false); }

        void RefreshBanner()
        {
            if (!banner) return;
            bool show = GameState.mode == GameMode.Sandbox && !Panels.AnyOpen && !listCanvas && !SleepSystem.PlayerAsleep;
            banner.gameObject.SetActive(show);
            if (PlayerRig.I && PlayerRig.I.isXR) { } else
            {   // keep clear of the autopilot banner
                var rt = banner.rectTransform; var pr = PlayerRig.I;
                rt.anchoredPosition = new Vector2(0, pr && pr.autopilot ? -62 : -14);
                flash.rectTransform.anchoredPosition = rt.anchoredPosition;
            }
            var t = Current;
            bool custom = AlarmBanner() || InChargeBanner() || (InMission && MissionBanner()) || (GameState.mode == GameMode.Sandbox && Current == null && DailyBanner());
            foreach (var sg in segs) if (sg) sg.gameObject.SetActive(!custom);      // the rubric progress dots only belong to rubric tasks
            if (custom) return;
            for (int i = 0; i < segs.Count; i++)
                segs[i].color = tasks[i].done ? new Color(0.3f, 1f, 0.5f) : tasks[i] == t ? UIKit.Gold : new Color(0.3f, 0.33f, 0.4f);
            string keyJ = PlayerRig.I && PlayerRig.I.isXR ? "menu → Rubric tasks" : "J = full checklist";
            if (t == null)
            {
                bTop.text = $"RUBRIC TASKS {Done}/{tasks.Count} — ALL DONE   ·   {keyJ}";
                bTitle.text = "<b>Every rubric area has been shown in the simulator ✓</b>";
                bHow.text = "Proof with times: Reports/rubric_tasks.txt. You can reset the checklist from the J screen to practise again.";
                return;
            }
            var sec = Sections.First(s => s.code == t.sec);
            int n = tasks.IndexOf(t) + 1;
            bTop.text = $"<b>TASK {n} of {tasks.Count}</b>   ·   <b><color=#{SecHex(t.sec)}>{t.sec} {sec.name}</color></b> ({sec.pts} pts)   ·   <color=#7CFC9A><b>{Done} done</b></color>   ·   {Hi(keyJ)}";
            bTitle.text = "<b>" + t.title + "</b>" + (t.progress != null ? $"  <size=18><color=#FFE08A>{t.progress()}</color></size>" : "");
            bHow.text = (PlayerRig.I && PlayerRig.I.isXR ? "<b><color=#7CFC9A>Menu → \"Do my next task\" does it for you.</color></b>  " : "<b><color=#7CFC9A>Press ENTER — I'll do it with you.</color></b>  ") + "<b><color=#FFFFFF>HOW:</color></b> " + Hi(t.how);
        }

        // ------------------------------------------------------------------ the full checklist (J)
        public void ToggleList() { if (listCanvas) CloseList(); else OpenList(); }
        CursorLockMode prevLock;
        public void OpenList()
        {
            Panels.CloseAll();
            var pr = PlayerRig.I; bool vr = pr && pr.isXR;
            listCanvas = UIKit.Canvas("RubricTaskList", vr, new Vector2(1500, 960));
            if (vr && pr.cam) { var t = pr.cam.transform; var f = t.forward; f.y = 0; f.Normalize(); listCanvas.transform.position = t.position + f * 1.6f; listCanvas.transform.rotation = Quaternion.LookRotation(f); listCanvas.transform.localScale = Vector3.one * 0.0009f; }
            else { listCanvas.sortingOrder = 30; prevLock = Cursor.lockState; Cursor.lockState = CursorLockMode.None; }
            RefreshList(true);
        }
        public void CloseList()
        {
            if (listCanvas) Destroy(listCanvas.gameObject); listCanvas = null;
            var pr = PlayerRig.I; if (pr && !pr.isXR) Cursor.lockState = prevLock;
        }
        int listHash = -1;
        void RefreshList(bool force = false)
        {
            int h = Done * 100 + (Current != null ? tasks.IndexOf(Current) : 99) + AresTypes.Count(zoneTypesSeen.Contains) * 7000;
            if (!force && h == listHash) return; listHash = h;
            var root = listCanvas.transform; foreach (Transform ch in root) Destroy(ch.gameObject);
            var C = new Vector2(0.5f, 0.5f); var TL = new Vector2(0, 1); var TR = new Vector2(1, 1);
            var bg = UIKit.Round(UIKit.Panel(root, "Bg", C, Vector2.zero, new Vector2(1500, 960), new Color(0.03f, 0.06f, 0.13f, 0.95f)), 0.5f).transform;
            UIKit.Label(bg, $"<b>RUBRIC TASKS — {Done} of {tasks.Count} done</b>   <size=20><color=#9FB3C8>each one ticks itself off when you really do it · gold = your next task</color></size>", 32, UIKit.Accent, TL, new Vector2(24, -14), new Vector2(1200, 46), TextAnchor.UpperLeft, TL);
            var close = UIKit.Btn(bg, "Close ✕", TR, new Vector2(-20, -14), new Vector2(150, 50), CloseList, new Color(0.45f, 0.15f, 0.15f), 22); close.GetComponent<RectTransform>().pivot = TR;
            var reset = UIKit.Btn(bg, "Reset checklist", TR, new Vector2(-180, -14), new Vector2(200, 50), () => { ResetAll(); RefreshList(true); }, new Color(0.3f, 0.3f, 0.36f), 18); reset.GetComponent<RectTransform>().pivot = TR;

            string[] left = { "D1", "D2", "D3" }, right = { "D4", "D5", "Bonus A", "Bonus B" };
            Column(bg, left, 24); Column(bg, right, 764);
        }
        void Column(Transform bg, string[] secs, float x)
        {
            float y = -84; var TL = new Vector2(0, 1); var cur = Current;
            int nRows = tasks.Count(t => secs.Contains(t.sec)); float step = Mathf.Min(58f, (860f - secs.Length * 44f) / Mathf.Max(1, nRows)), rowH = step - 4f;
            foreach (var code in secs)
            {
                var sec = Sections.First(s => s.code == code); var list = tasks.Where(t => t.sec == code).ToList();
                int d = list.Count(t => t.done);
                UIKit.Label(bg, $"<b><color=#{SecHex(code)}>{code} · {sec.name}</color></b>  <size=18>({sec.pts} pts)   {d}/{list.Count} {(d == list.Count ? "<color=#4CFF8A>✓ shown</color>" : "")}</size>", 22, UIKit.Gold, TL, new Vector2(x, y), new Vector2(712, 32), TextAnchor.UpperLeft, TL);
                y -= 36;
                foreach (var t in list)
                {
                    var row = UIKit.Round(UIKit.Panel(bg, "Row_" + t.id, TL, new Vector2(x, y), new Vector2(712, rowH), t.done ? new Color(0.08f, 0.3f, 0.16f, 0.9f) : t == cur ? new Color(0.35f, 0.26f, 0.05f, 0.95f) : new Color(0.1f, 0.13f, 0.2f, 0.9f), TL), 1.4f).transform;
                    UIKit.Label(row, t.done ? "<color=#4CFF8A><b>✓</b></color>" : "<color=#FFFFFF>☐</color>", 30, Color.white, new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(34, 50), TextAnchor.MiddleLeft, new Vector2(0, 0.5f));
                    string sub = t.done ? $"<size=14><color=#9FE8B5>done {t.doneAt}</color></size>" : (t.progress != null ? $"<size=14><color=#FFE08A>{t.progress()}</color></size>" : "");
                    UIKit.Label(row, "<b>" + t.title + "</b>" + (sub.Length > 0 ? "\n" + sub : ""), 18, Color.white, new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(t.done ? 650 : 400, 52), TextAnchor.MiddleLeft, new Vector2(0, 0.5f));
                    if (!t.done)
                    {
                        var tt = t; float bx = -8;
                        var b2 = UIKit.Btn(row, "DO IT ▶", new Vector2(1, 0.5f), new Vector2(bx, 0), new Vector2(140, 42), () => { focus = tt; CloseList(); DoTask(tt); }, new Color(0.15f, 0.55f, 0.28f), 18);
                        b2.GetComponent<RectTransform>().pivot = new Vector2(1, 0.5f); bx -= 148;
                        var b1 = UIKit.Btn(row, tt.guide != null ? "Guide me" : "Do next", new Vector2(1, 0.5f), new Vector2(bx, 0), new Vector2(120, 42), () => { focus = tt; CloseList(); tt.guide?.Invoke(); HUD.I?.Toast("Next task: " + tt.title); }, new Color(0.15f, 0.45f, 0.7f), 16);
                        b1.GetComponent<RectTransform>().pivot = new Vector2(1, 0.5f);
                    }
                    y -= step;
                }
                y -= 8;
            }
        }
    }

    /// CREW HEALTH — a simple live card: one row per astronaut with a traffic-light status, heart rate, sleep, energy,
    /// where they are and what they're doing. V toggles it (VR: menu → Crew health).
    /// Vital signs are the medical safety channel, so the Commander sees them for everyone. Mood and stress are private:
    /// shown for yourself, for anyone who chose to share them, or for someone whose Tier 3 was escalated to you.
    public class CrewHealthCard : MonoBehaviour
    {
        public static CrewHealthCard I;
        Canvas canvas; Image bg; float next;
        class Row { public Text name, pill, info, bar1Label, bar2Label; public Image pillBg, bar1, bar2, bar2Back; }
        readonly Row[] rows = new Row[5];
        Text baseLine, foot;
        public static bool Visible = false;      // clean screen: Tab (or V) shows it


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) Make(); };
            if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) Make();
        }
        static void Make() { if (!FindFirstObjectByType<CrewHealthCard>()) new GameObject("CrewHealthCard").AddComponent<CrewHealthCard>(); }
        void Awake() { I = this; }
        void Start() { Invoke(nameof(BuildDelayed), 1.0f); }
        void BuildDelayed() { var pr = PlayerRig.I; if (pr && pr.isXR) Visible = false; Build(); }

        static readonly Color[] RoleCol = { Color.white, new Color(1f, 0.78f, 0.3f), new Color(0.35f, 0.85f, 1f), new Color(0.55f, 1f, 0.55f), new Color(1f, 0.55f, 0.8f) };

        void Build()
        {
            var pr = PlayerRig.I; bool vr = pr && pr.isXR;
            canvas = UIKit.Canvas("CrewHealthCard", vr, new Vector2(600, 560));
            if (!vr) canvas.sortingOrder = 11;
            var R = new Vector2(1f, 0.5f);
            bg = UIKit.Panel(canvas.transform, "Card", vr ? new Vector2(0.5f, 0.5f) : R, vr ? Vector2.zero : new Vector2(-12, -30), new Vector2(600, 560), new Color(0.03f, 0.06f, 0.13f, 0.86f), vr ? (Vector2?)null : R); UIKit.Round(bg, 0.6f);
            var t = bg.transform; var TL = new Vector2(0, 1);
            UIKit.Label(t, "<b>CREW HEALTH — LIVE</b>   <size=15><color=#9FB3C8>V hides · updates every second</color></size>", 22, UIKit.Accent, TL, new Vector2(14, -8), new Vector2(572, 30), TextAnchor.UpperLeft, TL);
            baseLine = UIKit.Label(t, "", 15, new Color(0.8f, 0.88f, 1f), TL, new Vector2(14, -38), new Vector2(572, 22), TextAnchor.UpperLeft, TL);
            for (int i = 1; i <= 4; i++)
            {
                float y = -66 - (i - 1) * 118;
                var box = UIKit.Round(UIKit.Panel(t, "Crew" + i, TL, new Vector2(10, y), new Vector2(580, 112), new Color(0.08f, 0.11f, 0.19f, 0.95f), TL), 1.2f).transform;
                var r = new Row();
                r.name = UIKit.Label(box, "", 20, RoleCol[i], TL, new Vector2(10, -6), new Vector2(380, 26), TextAnchor.UpperLeft, TL);
                r.pillBg = UIKit.Round(UIKit.Panel(box, "Pill", new Vector2(1, 1), new Vector2(-8, -6), new Vector2(170, 28), Color.green, new Vector2(1, 1)), 1.7f);
                r.pill = UIKit.Label(r.pillBg.transform, "", 16, Color.black, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(166, 26), TextAnchor.MiddleCenter);
                r.info = UIKit.Label(box, "", 15, Color.white, TL, new Vector2(10, -34), new Vector2(560, 22), TextAnchor.UpperLeft, TL);
                // bar 1: physical health (always), bar 2: wellbeing (private unless shared)
                r.bar1Label = UIKit.Label(box, "Physical", 14, new Color(0.75f, 0.82f, 0.95f), TL, new Vector2(10, -62), new Vector2(110, 20), TextAnchor.UpperLeft, TL);
                UIKit.Round(UIKit.Panel(box, "B1back", TL, new Vector2(120, -64), new Vector2(440, 14), new Color(0.2f, 0.22f, 0.28f), TL), 3.4f);
                r.bar1 = UIKit.Round(UIKit.Panel(box, "B1", TL, new Vector2(120, -64), new Vector2(440, 14), Color.green, TL), 3.4f);
                r.bar2Label = UIKit.Label(box, "Wellbeing", 14, new Color(0.75f, 0.82f, 0.95f), TL, new Vector2(10, -86), new Vector2(110, 20), TextAnchor.UpperLeft, TL);
                r.bar2Back = UIKit.Round(UIKit.Panel(box, "B2back", TL, new Vector2(120, -88), new Vector2(440, 14), new Color(0.2f, 0.22f, 0.28f), TL), 3.4f);
                r.bar2 = UIKit.Round(UIKit.Panel(box, "B2", TL, new Vector2(120, -88), new Vector2(440, 14), Color.green, TL), 3.4f);
                rows[i] = r;
            }
            foot = UIKit.Label(t, "Mood/stress stay private unless shared or escalated to you (ethics D4).", 13, new Color(0.6f, 0.68f, 0.8f), new Vector2(0, 0), new Vector2(14, 6), new Vector2(572, 20), TextAnchor.LowerLeft, new Vector2(0, 0));
            Place(); canvas.gameObject.SetActive(Visible);
        }

        void Place()
        {
            var pr = PlayerRig.I; if (!canvas || !pr || !pr.isXR || !pr.cam) return;
            var c = pr.cam.transform; var f = c.forward; f.y = 0; f.Normalize(); var right = Vector3.Cross(Vector3.up, f);
            canvas.transform.position = c.position + f * 1.4f + right * 0.6f - Vector3.up * 0.1f;
            canvas.transform.rotation = Quaternion.LookRotation(canvas.transform.position - c.position); canvas.transform.localScale = Vector3.one * 0.0012f;
        }

        public void Toggle()
        {
            Visible = !Visible; if (!canvas) return;
            canvas.gameObject.SetActive(Visible); if (Visible) { Place(); next = 0; }
            HUD.I?.Toast(Visible ? "Crew health ON (Tab to hide)." : "Crew health hidden (Tab to show).");
        }

        static bool CanSeeMood(MoodEngine e, CrewMember c) => c.id == 1 || c.shareMoodPublic || (e.escalatedTo.TryGetValue(c.id, out int who) && who == 1);   // quiet check (no 'blocked' log)

        public static int HeartRate(CrewMember c)
        {
            float hr = 62f + 30f * c.stress + 10f * c.fatigue + (c.exercising ? 58f : 0f) + (c.onEVA ? 22f : 0f) - (c.asleep ? 9f : 0f);
            hr += 2.5f * Mathf.Sin(Time.time * 0.6f + c.id * 1.7f);
            return Mathf.RoundToInt(hr);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame) Toggle();
            if (canvas && Visible && PlayerRig.I && PlayerRig.I.isXR && PlayerRig.I.cam && Vector3.Distance(canvas.transform.position, PlayerRig.I.cam.transform.position) > 3f) Place();   // walked away → card comes along
            if (!canvas || !Visible || Time.time < next) return; next = Time.time + 1f;
            var e = Sim.I != null ? Sim.I.engine : null; if (e == null) return;
            // hide on desktop when a big panel is open (it would cover it)
            if (!(PlayerRig.I && PlayerRig.I.isXR)) bg.gameObject.SetActive(!Panels.AnyOpen && !SleepSystem.PlayerAsleep && !(HUD.I && HUD.I.BigMapOpen) && !SmartInteractor.CardVisible);   // steps aside while you use something

            var env = HabitatEnvironment.I;
            float co2 = env && env.zones.Count > 0 ? env.zones.Average(z => z.co2) : e.zoneCO2Ppm;
            bool alarm = Hazards.I && Hazards.I.Active;
            baseLine.text = $"Cabin  O2 21.0 %  ·  CO2 <color={(co2 > 2500 ? "#FF6A5A" : co2 > 1500 ? "#FFB74D" : "#7CFC9A")}>{co2:0} ppm</color>  ·  70.3 kPa  ·  " +
                            (alarm ? $"<color=#FF6A5A><b>ALARM: {Hazards.I.Kind}</b></color>" : "<color=#7CFC9A>no alarms</color>") + $"  ·  {SimClock.Now}";

            for (int i = 1; i <= 4; i++)
            {
                var c = e.C(i); var r = rows[i]; if (r == null) continue;
                int hr = HeartRate(c);
                float physical = Mathf.Clamp01(0.5f * c.sleepQ + 0.5f * (1f - c.fatigue));
                bool escalated = e.escalatedTo.ContainsKey(c.id);
                // traffic light
                string pill; Color pc;
                if (escalated || c.MaxTier >= 3) { pill = "NEEDS HELP"; pc = new Color(1f, 0.32f, 0.27f); }
                else if (!c.exercising && !c.onEVA && hr > 115) { pill = "CHECK VITALS"; pc = new Color(1f, 0.32f, 0.27f); }
                else if (c.onEVA) { pill = "ON EVA"; pc = new Color(0.4f, 0.75f, 1f); }
                else if (c.asleep) { pill = "ASLEEP"; pc = new Color(0.55f, 0.6f, 1f); }
                else if (physical < 0.45f) { pill = "TIRED"; pc = new Color(1f, 0.7f, 0.2f); }
                else { pill = "OK"; pc = new Color(0.3f, 0.95f, 0.5f); }
                r.pill.text = "<b>" + pill + "</b>"; r.pillBg.color = pc;
                r.name.text = $"<b>{c.name}</b>  <size=15><color=#B8C6DC>{c.role}{(i == 1 ? " (you)" : "")}</color></size>";
                string zn = c.zone;
                if (i != 1 && env) { var ag = CrewAgent.All.FirstOrDefault(a => a.crewId == i); var zz = ag ? env.ZoneAt(ag.transform.position) : null; if (zz != null) zn = zz.name; }
                if (string.IsNullOrEmpty(zn) && c.onEVA) zn = "outside (EVA)";
                string where = c.inPod && i != 1 ? "own quarters (private)" : string.IsNullOrEmpty(zn) ? "in a hallway" : zn.Replace("Private_", "").Replace("_", " ");
                string doing = c.asleep ? "asleep in bed" : c.inPod && i != 1 ? "private time" : c.activity;
                r.info.text = $"<color=#FF8A80>♥ {hr} bpm</color>   Sleep {c.sleepQ * 100:0}%   Energy {(1 - c.fatigue) * 100:0}%   <color=#B8C6DC>{where} · {doing}</color>";
                Bar(r.bar1, physical);
                if (CanSeeMood(e, c))
                {
                    r.bar2Label.text = "Wellbeing"; r.bar2.gameObject.SetActive(true); r.bar2Back.gameObject.SetActive(true); Bar(r.bar2, c.Wellbeing);
                }
                else
                {
                    r.bar2.gameObject.SetActive(false); r.bar2Back.gameObject.SetActive(false);
                    r.bar2Label.text = "Wellbeing  <color=#8FA3B8>private — shared only with consent or at Tier 3" + (c.MaxTier >= 1 && c.MaxTier < 3 ? " · AI support running" : "") + "</color>";
                }
                r.bar2Label.rectTransform.sizeDelta = new Vector2(CanSeeMood(e, c) ? 110 : 560, 20);
            }
        }

        static void Bar(Image img, float v)
        {
            v = Mathf.Clamp01(v);
            img.rectTransform.sizeDelta = new Vector2(Mathf.Max(16f, 440f * v), 14f);
            img.color = v > 0.66f ? new Color(0.3f, 0.95f, 0.5f) : v > 0.4f ? new Color(1f, 0.75f, 0.2f) : new Color(1f, 0.32f, 0.27f);
        }
    }
}
