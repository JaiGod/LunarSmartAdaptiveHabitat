// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/03_Crew/CrewOrders.cs
//  Section : 03_Crew
//  Folder  : The 4-person crew (you + 3 AI crewmates): schedules, jobs, waving, sleep pods with
//            mood lights, and the robotic health station.
//  Purpose : CREW JOBS (I, or Menu > Crew jobs, or walk up to a crewmate and press E). The
//            Commander gives the three AI crewmates a job with ONE tap: Okafor, Tanaka and Rivera
//            each have a column of big
//  Why     : Chat: '2-4 astronauts, single player' — you command three AI crewmates.
//  Contains: CrewOrders, Job, Order, HealthCheck, CrewMemberExt, CrewOrdersTicker
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hunch3
{
    /// CREW JOBS (I, or Menu > Crew jobs, or walk up to a crewmate and press E).
    /// The Commander gives the three AI crewmates a job with ONE tap: Okafor, Tanaka and Rivera each have a column of big
    /// buttons with the jobs that fit their role. The crewmate walks there (by ladder if needed), works for the set time,
    /// and the job's effect lands in the habitat (less CO2, fresh food, a printed spare part ...).
    /// It is ASKED, not ordered (ARES autonomy rule): someone exhausted or already over-controlled may say "not now", and
    /// that answer is respected and logged. "Let them pick" gives the choice back, which RAISES their sense of autonomy.
    public static class CrewOrders
    {
        public class Job { public string name, wp, why, help; public float hours; public Action<CrewMember> effect; public int role; }    // role 0 = anyone, 2/3/4 = that crewmate's specialty
        public class Order { public Job job; public string status = "asked"; public float startH = -1, doneH = -1; public bool chosenByThem; }
        public static readonly Dictionary<int, Order> active = new Dictionary<int, Order>();
        public static readonly List<string> history = new List<string>();
        public static int done;
        /// each person's colour, the same as on the CREW HEALTH card (you gold, Okafor cyan, Tanaka green, Rivera pink)
        public static readonly Color[] Col = { Color.white, new Color(1f, 0.78f, 0.3f), new Color(0.35f, 0.85f, 1f), new Color(0.55f, 1f, 0.55f), new Color(1f, 0.55f, 0.8f) };
        public static string Hex(int id) => ColorUtility.ToHtmlStringRGB(Col[Mathf.Clamp(id, 0, 4)]);
        public static readonly List<(int id, string job, float h)> asked = new List<(int id, string job, float h)>();

        static MoodEngine E => Sim.I.engine;
        static void Co2(float d) { if (HabitatEnvironment.I) foreach (var z in HabitatEnvironment.I.zones) z.co2 = Mathf.Max(420f, z.co2 + d); }

        // Every job is a MENTAL-HEALTH support from the ARES design: it names the condition it helps (the six the AI watches).
        public static readonly Job[] Jobs = {
            new Job { name = "Breathing break (calm)",       help = "Anxiety ↓",     wp = "WP_D_Obs",     hours = 0.5f, role = 0, why = "Slow breathing at the window: heart-rate variability recovers, stress drops.", effect = c => { c.stress -= 0.15f; } },
            new Job { name = "Video call home",              help = "Depression ↓",  wp = "WP_N_C",       hours = 1f,   role = 0, why = "Talking to family is the strongest mood lift on a long mission.", effect = c => { c.mood += 0.12f; c.lastSocialHour = SimClock.hours; } },
            new Job { name = "Team meal in the galley",      help = "Conflict ↓",    wp = "WP_Up_Galley", hours = 1f,   role = 0, why = "Eating together rebuilds trust between crewmates.", effect = c => { for (int i = 0; i < c.tension.Length; i++) c.tension[i] *= 0.6f; c.mood += 0.04f; c.lastSocialHour = SimClock.hours; } },
            new Job { name = "Rest in the lounge",           help = "Fatigue ↓",     wp = "WP_Up_Lounge", hours = 1f,   role = 0, why = "A real break: the brain recovers and mistakes drop.", effect = c => { c.fatigue -= 0.25f; c.stress -= 0.04f; } },
            new Job { name = "Plan their own afternoon",     help = "Autonomy ↑",    wp = "WP_Up_Table",  hours = 0.5f, role = 0, why = "Choosing your own schedule gives back a sense of control.", effect = c => { c.autonomy += 0.2f; } },
            new Job { name = "VR nature walk",               help = "Monotony ↓",    wp = "WP_D_VR",      hours = 1f,   role = 0, why = "Forests, rain and ocean sounds break the grey-walls sameness.", effect = c => { c.novelty += 0.2f; c.mood += 0.04f; } },
            new Job { name = "Exercise session",             help = "Anxiety + mood",wp = "WP_D_Ex",      hours = 1f,   role = 0, why = "Protects bone and muscle in 1/6 g and lifts mood (the AI knows a high heart rate here is exercise, not anxiety).", effect = c => { c.fatigue -= 0.1f; c.stress -= 0.08f; c.mood += 0.05f; c.lastExerciseHour = SimClock.hours; } },
            new Job { name = "Wellness checks for the crew", help = "everyone",      wp = "WP_H_C",       hours = 1f,   role = 2, why = "The CMO checks on everyone in the medical bay (consent-based).", effect = c => { foreach (var m in E.crew) m.stress -= 0.04f; } },
            new Job { name = "Fix the CO2 scrubber",         help = "Fatigue ↓ all", wp = "WP_EclA_In",   hours = 1f,   role = 3, why = "High CO2 makes everyone tired and foggy — cleaning the scrubber clears the air.", effect = c => { Co2(-120f); foreach (var m in E.crew) m.fatigue -= 0.05f; } },
            new Job { name = "Tend the greenhouse",          help = "Monotony ↓",    wp = "WP_C_Mid",     hours = 1f,   role = 4, why = "Green, living things and fresh food — caring for plants lifts mood.", effect = c => { c.mood += 0.08f; c.novelty += 0.1f; } },
        };
        public static IEnumerable<Job> JobsFor(int id) => Jobs.Where(j => j.role == id || j.role == 0);

        /// one tap: ask crewmate `id` to do `job`
        public static string Ask(int id, Job job, bool theirChoice = false)
        {
            if (Sim.I == null) return "";
            var c = E.C(id); string first = c.name.Split(' ').Last();
            if (Hazards.I && Hazards.I.Active) return Say(id, $"{first}: Not during an alarm — safety first.", "busy");
            float hod = SimClock.HourOfDay; bool later = c.onEVA || hod >= 23 || hod < 7;
            if (later)
            {   // never wake or interrupt them: the task waits (sleep and EVA are protected)
                active[id] = new Order { job = job, status = "on the way", chosenByThem = theirChoice }; asked.Add((id, job.name, SimClock.hours));
                E.Log(id, "job", $"{c.name} will do: {job.name} {(c.onEVA ? "after the EVA" : "after 07:00")} (not woken — sleep is protected).");
                return Say(id, c.onEVA ? $"{first} is on EVA — they'll do it when they're back inside." : $"{first} is asleep — they'll do it after 07:00. Sleep is protected, nobody is woken.", "ok");
            }
            if (!theirChoice && job.role != 0 && (c.fatigue > 0.7f || c.tier[(int)Cond.Autonomy] >= 2))
            {
                E.Log(id, "job", $"{c.name} said \"not now\" to: {job.name} ({(c.fatigue > 0.7f ? "exhausted" : "feeling over-scheduled")}). Respected — no pressure.");
                return Say(id, $"{first}: \"Not now please — {(c.fatigue > 0.7f ? "I'm wiped out" : "I need some say in my day")}. Can it wait?\" (Respected. Try \"Let them pick\".)", "declined");
            }
            active[id] = new Order { job = job, status = "on the way", chosenByThem = theirChoice }; asked.Add((id, job.name, SimClock.hours));
            c.autonomy += theirChoice ? 0.06f : -0.01f;
            E.Log(id, "job", $"{c.name} {(theirChoice ? "chose" : "agreed to")}: {job.name}.");
            return Say(id, $"{first}: \"{(theirChoice ? "I'll do" : "On it —")} {job.name.ToLower()}.\"  ({job.help})", "ok");
        }
        /// "Let them pick": the crewmate chooses the job that suits how they feel right now
        public static string LetThemPick(int id) => Ask(id, Suggest(id), true);
        /// the one task that helps this person most right now (their highest condition)
        public static Job Suggest(int id)
        {
            var c = E.C(id); var list = JobsFor(id).ToList();
            // they pick what helps the condition that is highest for them right now
            float[] need = { c.stress, 1 - c.mood, c.tension.Length > 0 ? c.tension.Max() : 0, c.fatigue, 1 - c.autonomy, 1 - c.novelty };   // what a person could see
            int worst = 0; for (int k = 1; k < 6; k++) if (need[k] + c.smooth[k] > need[worst] + c.smooth[worst]) worst = k;
            if (need[worst] + c.smooth[worst] < 0.5f) return list.FirstOrDefault(j => j.name.StartsWith(id == 2 ? "Exercise" : id == 3 ? "VR nature" : "Video call")) ?? list[0];   // nobody needs much: their own favourite way to recharge
            string want = worst switch { 0 => "Breathing", 1 => "Video call", 2 => "Team meal", 3 => "Rest", 4 => "Plan", _ => "VR nature" };
            return list.FirstOrDefault(j => j.name.StartsWith(want)) ?? list[0];
        }
        public static string Cancel(int id)
        {
            if (!active.ContainsKey(id)) return "";
            var o = active[id]; active.Remove(id); E.Log(id, "job", $"Job cancelled: {o.job.name}.");
            return Say(id, $"{E.C(id).name.Split(' ').Last()} goes back to their normal day.", "ok");
        }
        static string Say(int id, string s, string kind) { HUD.I?.Toast(s); return s; }

        /// called by CrewAgent.Schedule: a job overrides the routine (but never an alarm, a Tier-3 response or sleep)
        public static bool Override(int id, out string goal, out string task)
        {
            goal = task = null;
            if (!active.TryGetValue(id, out var o)) return false;
            if (!Nav.wp.ContainsKey(o.job.wp)) { active.Remove(id); return false; }
            goal = o.job.wp; task = "job: " + o.job.name.ToLower();      // the text stays the same while working, so the walker keeps its path
            return true;
        }
        /// progress: arrive → work for the job's hours → effect, log, toast
        public static void Tick()
        {
            if (Sim.I == null) return;
            foreach (var kv in active.ToList())
            {
                int id = kv.Key; var o = kv.Value; var a = CrewAgent.All.FirstOrDefault(x => x.crewId == id); if (!a) continue;
                bool there = Nav.wp.ContainsKey(o.job.wp) && (a.transform.position - Nav.wp[o.job.wp].position).sqrMagnitude < 1.2f * 1.2f;
                if (o.status == "on the way" && there) { o.status = "working"; o.startH = SimClock.hours; }
                if (o.status == "working" && SimClock.hours - o.startH >= o.job.hours)
                {
                    var c = E.C(id); o.job.effect?.Invoke(c); c.Clamp01(); done++;
                    string line = $"{SimClock.Now}  {c.name}: {o.job.name} DONE";
                    history.Insert(0, line); if (history.Count > 12) history.RemoveAt(history.Count - 1);
                    E.Log(id, "job", $"Finished: {o.job.name}. {o.job.why}");
                    HUD.I?.Toast($"✓ {c.name.Split(' ').Last()} finished: {o.job.name} — {o.job.help}. {o.job.why}");
                    active.Remove(id);
                }
            }
        }
        public static string Status(int id)
        {
            var c = E.C(id);
            if (active.TryGetValue(id, out var o)) return o.status == "working" ? $"working: {o.job.name} {Mathf.Clamp01((SimClock.hours - o.startH) / o.job.hours):P0}" : $"walking to: {o.job.name}";
            return c.activity;
        }
        /// how the person feels about work right now, in words (no private psych data — just what a commander could see)
        public static string Energy(int id) { var c = E.C(id); return c.asleep ? "asleep" : c.onEVA ? "on EVA" : c.fatigue > 0.7f ? "very tired" : c.fatigue > 0.45f ? "a bit tired" : "ready"; }
    }

    /// HEALTH CHECK-IN (I next to a crewmate, or the button in Crew jobs): you physically walk up to a crewmate and read their ARES sensors —
    /// the wristband (heart rate, HRV, sleep), the chest beacon (skin response), the cortisol skin patch and the room's air and
    /// mic/camera arrays. The crewmate is ASKED first and can say no; anything they opted out of shows OFF; a broken
    /// sensor shows NO DATA. The result is the same data the AI uses, turned into the six mental-health conditions.
    public static class HealthCheck
    {
        public static readonly Dictionary<int, string> last = new Dictionary<int, string>();
        public static readonly HashSet<int> scanned = new HashSet<int>();
        public static readonly Dictionary<int, float> at = new Dictionary<int, float>();
        public static int lastId = -1;
        static MoodEngine E => Sim.I.engine;

        public static CrewAgent Nearest(float maxDist)
        {
            var p = PlayerRig.I; if (!p) return null;
            return CrewAgent.All.Where(a => a.crewId != 1).OrderBy(a => (a.transform.position - p.FeetPosition).sqrMagnitude)
                .FirstOrDefault(a => (a.transform.position - p.FeetPosition).sqrMagnitude < maxDist * maxDist);
        }
        /// I key next to someone: the crewmate next to you (or, if nobody is close, the next one you haven't checked yet — you are walked over)
        public static void Quick()
        {
            var a = Nearest(3f); if (a) { Scan(a.crewId); return; }
            int id = new[] { 2, 3, 4 }.FirstOrDefault(i => !scanned.Contains(i)); Scan(id == 0 ? 2 : id);
        }
        public static void Scan(int id)
        {
            if (Sim.I == null) return; var c = E.C(id); string first = c.name.Split(' ').Last();
            var a = CrewAgent.All.FirstOrDefault(x => x.crewId == id); var p = PlayerRig.I;
            if (!c.onEVA && a && p && (a.transform.position - p.FeetPosition).sqrMagnitude > 3f * 3f)
            {   // walk the Commander over (a check-in is done in person)
                var fwd = p.FeetPosition - a.transform.position; fwd.y = 0; if (fwd.sqrMagnitude < 0.01f) fwd = a.transform.forward; fwd.y = 0; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                fwd.Normalize(); Vector3 best = a.transform.position + fwd * 1.3f, look = -fwd;
                for (int t = 0; t < 8; t++)
                {   // first free spot around them (never inside a wall or a bunk), starting on your side
                    var d = Quaternion.Euler(0, 45f * t, 0) * fwd; var at = a.transform.position + d * 1.3f;
                    if (!Physics.CheckCapsule(at + Vector3.up * 0.45f, at + Vector3.up * 1.6f, 0.28f, ~0, QueryTriggerInteraction.Ignore)) { best = at; look = -d; break; }
                }
                p.Teleport(best, Quaternion.LookRotation(look).eulerAngles.y);      // beside them, facing them
            }
            var sb = new System.Text.StringBuilder();
            string consent = c.onEVA ? $"{first} is outside on EVA — suit telemetry by radio, {first} said yes over comms." : c.asleep ? $"{first} is asleep — only the night wristband (sleep) is read, nothing else." : $"You: \"Quick check-in, ok?\"  {first}: \"Sure.\"  (consent given — they can say no any time)";
            sb.AppendLine($"<b><color=#{CrewOrders.Hex(id)}>{c.name}</color> — {SimClock.Now}</b>   <size=19>{consent}</size>");
            float co2 = 0; string here = c.zone; var env = HabitatEnvironment.I; if (env) { var z = env.zones.OrderBy(q => a ? (q.center - a.transform.position).sqrMagnitude : 0).FirstOrDefault(); if (z != null) { co2 = z.co2; if (string.IsNullOrEmpty(here)) here = z.name.Replace("_", " "); } }
            string R(Sensor s, string val) { int i = (int)s; if (c.asleep && s != Sensor.Sleep) return null; if (c.optOut[i] && SensorSpec.OptOutAllowed[i]) return $"{Short(s)} OFF (opted out)"; if (c.sensors[i] == SensorHealth.Failed) return $"{Short(s)} NO DATA (sensor failed)"; return $"{Short(s)} {val}" + (c.sensors[i] == SensorHealth.Degraded ? " (noisy)" : ""); }
            var parts = new[] {
                R(Sensor.HRV, $"{62 + 40 * c.stress:0} bpm · HRV {58 - 34 * c.stress:0} ms"), R(Sensor.GSR, $"{1 + 4 * c.stress:0.0} peaks/min"), R(Sensor.Sleep, $"{70 + 25 * c.sleepQ:0}% efficient"),
                R(Sensor.Voice, c.mood < 0.45f ? "flat" : c.stress > 0.5f ? "tense" : "normal"), R(Sensor.FACS, c.mood < 0.45f ? "low affect" : "normal"), R(Sensor.Motion, $"{(c.inPod ? "in pod" : string.IsNullOrEmpty(here) ? "in the base" : here)}"),
                R(Sensor.Cortisol, $"{8 + 14 * c.cortisol:0.0} µg/dL"), R(Sensor.CO2, $"{co2:0} ppm"), R(Sensor.Task, $"{100 - 35 * c.fatigue:0}% accuracy") }.Where(x => x != null);
            sb.AppendLine("<size=19>" + string.Join("  ·  ", parts) + "</size>");
            sb.AppendLine("<size=16><color=#9FD8FF>HOW IT WAS COLLECTED: wristband (heart, HRV, sleep) · chest beacon (skin response) · 24–48 h skin patch (cortisol) · room mic & camera arrays, processed on-device, shared zones only · UWB beacons (where) · CO2 pairs in every zone · terminal task scores. Raw audio/video never leaves the room.</color></size>");
            if (!c.asleep)
            {
                var cond = new List<string>(); string worst = null; float wv = 0;
                for (int k = 0; k < 6; k++) { float v = c.smooth[k]; string lvl = v >= CondSpec.T3 ? "<color=#FF5544>HIGH</color>" : v >= CondSpec.T2 ? "<color=#FFAA33>watch</color>" : v >= CondSpec.T1 ? "<color=#FFE066>mild</color>" : "<color=#7CFC9A>ok</color>"; cond.Add($"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(CondSpec.Col[k], Color.white, 0.35f))}>{CondSpec.Name[k].Split(' ').Last()}</color> {lvl}"); if (v > wv) { wv = v; worst = CondSpec.Name[k]; } }
                sb.AppendLine("<size=19>" + string.Join(" · ", cond) + "</size>");
                string tip = wv < CondSpec.T1 ? $"{first} looks well. Nothing to do." : worst switch { "Anxiety" => "Suggest: Breathing break (calm)", "Depression" => "Suggest: Video call home", "Interpersonal conflict" => "Suggest: Team meal in the galley", "Cognitive fatigue" => "Suggest: Rest in the lounge", "Autonomy deprivation" => "Suggest: Plan their own afternoon", _ => "Suggest: VR nature walk" };
                sb.Append($"<b><color=#7CFC9A>{tip}</color></b>  <size=17>(only {first} and the CMO see the details — you see this because {first} said yes)</size>");
            }
            last[id] = sb.ToString(); lastId = id; scanned.Add(id); at[id] = SimClock.hours;
            E.Log(id, "checkin", $"In-person health check-in by the Commander (consented). Sensors read: {string.Join(", ", parts)}.");
            HUD.I?.Toast($"Health check-in done on {first} ({scanned.Count}/3 crew). Results are on the Crew wellness screen (I).");
            Panels.Open("orders");
        }
        static string Short(Sensor s) => s switch { Sensor.HRV => "Heart", Sensor.GSR => "Skin", Sensor.Sleep => "Sleep", Sensor.Voice => "Voice", Sensor.FACS => "Face", Sensor.Motion => "Where", Sensor.Cortisol => "Cortisol", Sensor.CO2 => "Air CO2", _ => "Task" };
    }

    static class CrewMemberExt { public static void Clamp01(this CrewMember c) { c.stress = Mathf.Clamp01(c.stress); c.mood = Mathf.Clamp01(c.mood); c.fatigue = Mathf.Clamp01(c.fatigue); c.autonomy = Mathf.Clamp01(c.autonomy); c.novelty = Mathf.Clamp01(c.novelty); } }

    /// drives job progress every frame (added by HabitatBoot), shows what each crewmate is doing above their head,
    /// and reminds you how to give a job when you walk up to someone
    public class CrewOrdersTicker : MonoBehaviour
    {
        readonly Dictionary<int, TextMesh> tags = new Dictionary<int, TextMesh>(); int nearShown = -1; float nextNear;
        void Update()
        {
            try
            {
                CrewOrders.Tick();
                foreach (var a in CrewAgent.All)
                {
                    if (!tags.TryGetValue(a.crewId, out var tm) || !tm)
                    {
                        var g = new GameObject("JobTag " + a.crewId); tm = g.AddComponent<TextMesh>(); tm.characterSize = 0.035f; tm.fontSize = 48; tm.anchor = TextAnchor.LowerCenter; tm.alignment = TextAlignment.Center; tags[a.crewId] = tm;
                        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); if (f) { tm.font = f; g.GetComponent<MeshRenderer>().sharedMaterial = f.material; }
                    }
                    var r = a.GetComponentInChildren<Renderer>(); bool vis = r && r.enabled;
                    var pc = PlayerRig.I ? PlayerRig.I.cam : null;
                    if (vis && pc)
                    {   // only for people you can actually see, 1.5–14 m away (no labels through walls or in your face)
                        Vector3 head = a.transform.position + Vector3.up * 1.7f, d = head - pc.transform.position; float dist = d.magnitude;
                        vis = dist > 1.5f && dist < (HUD.Clean ? 6f : 14f) && !Physics.RaycastAll(pc.transform.position, d / dist, dist - 0.3f, ~0, QueryTriggerInteraction.Ignore).Any(h => !h.transform.IsChildOf(a.transform) && !h.transform.IsChildOf(PlayerRig.I.transform));
                    }
                    tm.gameObject.SetActive(vis); if (!vis) continue;
                    tm.transform.position = a.transform.position + Vector3.up * 2.05f;
                    var pr = PlayerRig.I; if (pr && pr.cam) tm.transform.rotation = Quaternion.LookRotation(tm.transform.position - pr.cam.transform.position);
                    bool job = CrewOrders.active.ContainsKey(a.crewId);
                    tm.color = job ? Color.Lerp(CrewOrders.Col[Mathf.Clamp(a.crewId, 0, 4)], Color.white, 0.35f) : CrewOrders.Col[Mathf.Clamp(a.crewId, 0, 4)];
                    string what = job ? CrewOrders.Status(a.crewId) : a.task; int paren = what.IndexOf(" ("); if (paren > 0) what = what.Substring(0, paren); if (what.Length > 26) what = what.Substring(0, 25) + "…";
                    tm.text = HUD.Clean && !job ? Sim.I.engine.C(a.crewId).name.Split(' ').Last() : Sim.I.engine.C(a.crewId).name.Split(' ').Last() + "\n<size=34>" + what + "</size>";
                }
                // walk up to a crewmate → one reminder how to give them a job
                var p = PlayerRig.I; if (p && Time.time > nextNear)
                {
                    nextNear = Time.time + 1f;
                    var near = CrewAgent.All.FirstOrDefault(x => x.crewId != 1 && (x.transform.position - p.FeetPosition).sqrMagnitude < 2.5f * 2.5f);
                    if (near && near.crewId != nearShown && !Panels.AnyOpen) { nearShown = near.crewId; HUD.I?.Toast($"{Sim.I.engine.C(near.crewId).name.Split(' ').Last()} is here.  press I to check on them (read their sensors, then help)"); }
                    if (!near) nearShown = -1;
                }
            }
            catch (Exception e) { HLog.W("Crew", "jobs: " + e.Message); enabled = false; }
        }
        void OnDestroy() { foreach (var t in tags.Values) if (t) Destroy(t.gameObject); }
    }
}
