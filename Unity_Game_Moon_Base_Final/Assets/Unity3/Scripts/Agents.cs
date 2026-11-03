using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hunch3
{
    /// Waypoint graph exported from Blender (WP_* empties with "links").
    public static class Nav
    {
        public static Dictionary<string, Transform> wp = new Dictionary<string, Transform>();
        public static Dictionary<string, List<string>> adj = new Dictionary<string, List<string>>();
        public static void Build()
        {
            wp.Clear(); adj.Clear();
            foreach (var t in Util.FindPrefix("WP_")) { if (!wp.ContainsKey(t.name)) wp[t.name] = t; }
            foreach (var kv in wp)
            {
                var tag = kv.Value.GetComponent<HabTag>();
                var l = new List<string>();
                if (tag) foreach (var s in tag.Get("links").Split(',')) if (wp.ContainsKey(s.Trim())) l.Add(s.Trim());
                adj[kv.Key] = l;
            }
            // make links symmetric
            foreach (var kv in adj.ToList()) foreach (var n in kv.Value) if (!adj[n].Contains(kv.Key)) adj[n].Add(kv.Key);
        }
        public static string Nearest(Vector3 p)
        {
            string best = null; float bd = 1e9f;
            foreach (var kv in wp) { float d = (kv.Value.position - p).sqrMagnitude + Mathf.Abs(kv.Value.position.y - p.y) * 20f; if (d < bd) { bd = d; best = kv.Key; } }
            return best;
        }
        public static List<Vector3> Path(Vector3 from, string goal)
        {
            var res = new List<Vector3>();
            if (!wp.ContainsKey(goal)) return res;
            string s = Nearest(from); if (s == null) return res;
            var prev = new Dictionary<string, string>(); var dist = new Dictionary<string, float> { [s] = 0 };
            var open = new List<string> { s };
            while (open.Count > 0)
            {
                string u = open.OrderBy(x => dist[x] + (wp[x].position - wp[goal].position).magnitude).First(); open.Remove(u);
                if (u == goal) break;
                foreach (var v in adj[u])
                {
                    float nd = dist[u] + (wp[u].position - wp[v].position).magnitude;
                    if (!dist.ContainsKey(v) || nd < dist[v]) { dist[v] = nd; prev[v] = u; if (!open.Contains(v)) open.Add(v); }
                }
            }
            if (!dist.ContainsKey(goal)) return res;
            string c = goal; var nodes = new List<string>();
            while (c != null) { nodes.Add(c); c = prev.ContainsKey(c) ? prev[c] : null; }
            nodes.Reverse();
            foreach (var n in nodes) res.Add(wp[n].position);
            return res;
        }
        public static bool IsLadder(Vector3 a, Vector3 b) => Mathf.Abs(a.y - b.y) > 1.5f;
        public static readonly Dictionary<string, string> Places = new Dictionary<string, string> {
            {"Wardroom / meeting table","WP_Up_Table"},{"Galley","WP_Up_Galley"},{"Lounge","WP_Up_Lounge"},{"My quarters (sleep)","WP_SQ1_Bed"},{"My health station","WP_SQ1_Health"},{"Rest pod (core)","WP_Pod1_Bed"},{"Quarters vestibule","WP_K_C"},
            {"Lavatory + hygiene","WP_Hyg_In"},{"Storage","WP_Sto_In"},{"ECLSS air","WP_EclA_In"},{"ECLSS water/waste","WP_EclW_In"},
            {"Lab (work bay)","WP_B_Lab"},{"Command & Comms (AI dashboard)","WP_N_C"},{"Greenhouse","WP_C_Mid"},{"Maintenance & robotics","WP_E_C"},
            {"Exercise bay","WP_D_Ex"},{"VR retreat","WP_D_VR"},{"Observation","WP_D_Obs"},{"Medical + storm shelter","WP_H_C"},
            {"Recreation commons","WP_R_C"},{"Crew support / lavatory 2","WP_G_C"},{"Suitports (go outside)","WP_A_Suit"},{"Airlock","WP_A_Lock"}
        };
    }

    /// AI crewmate: follows a daily schedule, reacts to its psychological state and AI offers, moves along waypoints.
    public class CrewAgent : MonoBehaviour
    {
        public static List<CrewAgent> All = new List<CrewAgent>();
        public int crewId; public string task = "", goal = ""; public bool Exercising;
        public bool autopilotDriver;           // used by the player's autopilot
        List<Vector3> path = new List<Vector3>(); int pi; float wait;
        public float speed = 1.1f; float bob;
        CrewMember Me => Sim.I.engine.C(crewId);
        string lastSlot = "";

        void OnEnable() { if (!autopilotDriver) All.Add(this); }
        void OnDisable() { All.Remove(this); }
        Quaternion baseRot = Quaternion.identity; Vector3 front0 = Vector3.forward;
        void Start()
        {
            // The FBX root carries Blender's axis conversion (-90° about X). Keep it and only add a YAW about world-up,
            // otherwise LookRotation() throws that conversion away and the crew member walks around lying flat.
            baseRot = transform.rotation;
            var beacon = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Contains("WellnessCommBeacon_Housing"));
            Vector3 f = beacon ? beacon.position - transform.position : transform.rotation * Vector3.forward; f.y = 0;
            if (f.sqrMagnitude < 1e-4f) { f = transform.up; f.y = 0; }
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            front0 = f.normalized;
        }
        Quaternion Facing(Vector3 dir) { dir.y = 0; if (dir.sqrMagnitude < 1e-6f) return transform.rotation; return Quaternion.AngleAxis(Vector3.SignedAngle(front0, dir.normalized, Vector3.up), Vector3.up) * baseRot; }

        // walk to the foot/head of the ladder, then climb rung by rung (0.8 m/s, hands-on-rails pose), then step off
        void ClimbToward(Vector3 target, CrewMember c)
        {
            var L = LadderClimb.I; if (!L) { transform.position = target; pi++; return; }
            Vector3 p = transform.position, stand = L.StandAt(p.y);
            Vector3 flat = stand - p; flat.y = 0;
            float spd = speed * Time.deltaTime * Mathf.Clamp(GameState.timeScale / 60f, 1f, 6f);
            if (flat.magnitude > 0.05f) { transform.position += flat.normalized * Mathf.Min(spd, flat.magnitude); FaceLadder(L); return; }
            float y = Mathf.MoveTowards(p.y, target.y, 0.8f * Time.deltaTime * Mathf.Clamp(GameState.timeScale / 60f, 1f, 6f));
            transform.position = new Vector3(stand.x, y, stand.z); FaceLadder(L);
            climbing = true; c.activity = task + " (climbing ladder)";
            if (Mathf.Abs(y - target.y) < 0.01f) climbing = false;     // now on the same deck as the target → normal walking resumes
        }
        void FaceLadder(LadderClimb L)
        {
            var lad = new Vector3(L.Stand.x - L.Bottom.x, 0, L.Stand.z - L.Bottom.z);
            if (lad.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.Slerp(transform.rotation, Facing(lad), Time.deltaTime * 6f);
        }
        public bool climbing;

        static string Q(int id, string spot, string fallback) => Nav.wp.ContainsKey($"WP_SQ{id}_{spot}") ? $"WP_SQ{id}_{spot}" : fallback;
        public static (string goal, string task) Schedule(int id, CrewMember c, float hod, int day)
        {
            // safety first: an active alarm overrides every schedule (sleepers are woken by the pod lights)
            if (Hazards.I && Hazards.I.Active)
                return (Hazards.I.Muster, (Hazards.I.Kind == "MEDICAL" && id == Hazards.I.Patient ? "patient — being helped to medical" : id == 2 && Hazards.I.Kind == "MEDICAL" ? "CMO leading medical response" : "ALARM: muster") + " (" + Hazards.I.Kind + ")");
            // a Tier-3 escalation wakes only the CMO, who goes to the patient
            if (id == 2 && Sim.I != null && Sim.I.engine.escalatedTo.Any(kv => kv.Value == 2) && !c.onEVA)
            {
                int subj = Sim.I.engine.escalatedTo.First(kv => kv.Value == 2).Key;
                if (subj != 2) return (Nav.wp.ContainsKey($"WP_SQ{subj}_In") ? $"WP_SQ{subj}_In" : $"WP_Pod{subj}_Door", "CMO responding to Tier-3 escalation");
            }
            // condition-driven behaviour first
            if (c.tier[(int)Cond.Depression] >= 1 && (hod > 19 || hod < 7.5f)) return (Q(id, "Rest", $"WP_Pod{id}_Bed"), "withdrawn (in quarters)");
            if (hod >= 23 || hod < 7) return (Q(id, "Bed", $"WP_Pod{id}_Bed"), "sleeping");
            if (hod < 7.25f) return (Q(id, "Health", id % 2 == 0 ? "WP_Hyg_In" : "WP_G_C"), "morning health check (AI health robot)");
            if (hod < 7.5f) return (id % 2 == 0 ? "WP_Hyg_In" : "WP_G_C", "hygiene / lavatory");
            if (hod < 8.5f) return ("WP_Up_Table", "breakfast");
            if (hod < 9.25f) return ("WP_Up_Table", "daily planning conference (team meeting)");
            if (hod >= 12 && hod < 13) return (GameState.configB && id == 4 ? "WP_Up_Lounge" : "WP_Up_Table", "lunch");
            if (hod >= 18 && hod < 19) return (GameState.configB && id == 4 ? "WP_Up_Lounge" : "WP_Up_Table", "dinner");
            if (hod >= 19) return (new[] { "WP_R_C", "WP_Up_Lounge", "WP_D_VR", "WP_D_Obs" }[(id + day) % 4], "free time");
            bool exerciseSlot = (hod >= 13 && hod < 15 && (id + day) % 2 == 0) || (hod >= 15 && hod < 16.5f && (id + day) % 2 == 1);
            if (exerciseSlot) return ("WP_D_Ex", "exercise (2 h/day countermeasure)");
            if (c.tier[(int)Cond.Depression] >= 2) return ("WP_C_Mid", "greenhouse tending (behavioural activation)");
            switch (id)
            {
                case 2: return (hod < 12 ? "WP_H_C" : "WP_B_Lab", hod < 12 ? "crew health checks (CMO)" : "biomedical research");
                case 3: return (hod < 12 ? "WP_E_C" : (day % 2 == 0 && hod > 16.5f ? "WP_A_Suit" : "WP_EclA_In"), hod < 12 ? "maintenance & spare-part printing" : (day % 2 == 0 && hod > 16.5f ? "EVA: solar tower inspection" : "ECLSS service"));
                case 4: return (hod < 12 ? "WP_B_Lab" : "WP_C_Mid", hod < 12 ? "regolith sample analysis" : "greenhouse harvest");
                default: return (hod < 12 ? "WP_N_C" : "WP_B_Lab", hod < 12 ? "command & comms" : "science");
            }
        }

        void Update()
        {
            if (Sim.I == null || autopilotDriver) return;
            var c = Me; float hod = SimClock.HourOfDay;
            var (g, t) = Schedule(crewId, c, hod, SimClock.Day);
            string slot = g + t;
            if (slot != lastSlot)
            {
                lastSlot = slot; task = t; goal = g;
                path = Nav.Path(transform.position, g); pi = 0;
                if (t.StartsWith("EVA")) Sim.I.engine.Log(crewId, "eva", $"{c.name} heading out: suitport don → seal → leak check → step off.");
            }
            c.activity = task;
            bool arrived = pi >= path.Count;
            if (!arrived)
            {
                Vector3 target = path[pi];
                if (climbing || Nav.IsLadder(transform.position, target)) { ClimbToward(target, c); return; }     // really climb the ladder
                Vector3 d = target - transform.position; d.y = 0;
                float step = speed * Time.deltaTime * Mathf.Clamp(GameState.timeScale / 60f, 1f, 6f) * (c.tier[(int)Cond.Fatigue] > 0 ? 0.7f : 1f);
                if (d.magnitude <= step + 0.05f) { transform.position = new Vector3(target.x, target.y, target.z); pi++; }
                else
                {
                    transform.position += d.normalized * step;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Facing(d), Time.deltaTime * 6f);
                    bob += Time.deltaTime * 4f;   // lunar 'lope'
                    var p = transform.position; p.y = target.y + Mathf.Abs(Mathf.Sin(bob)) * 0.08f; transform.position = p;
                }
            }
            // states
            c.asleep = task == "sleeping"; c.inPod = (goal.Contains("_Bed") || goal.Contains("WP_SQ")) && arrived;
            Exercising = task.StartsWith("exercise") && arrived; c.exercising = Exercising; if (Exercising) c.lastExerciseHour = SimClock.hours;
            c.onEVA = task.StartsWith("EVA") && arrived;
            // privacy: in their pod with door closed → hidden from others
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = !(c.inPod) && !c.onEVA;
            // social contact & activities feed back into the latent state
            if (arrived && (task.Contains("meeting") || task.Contains("lunch") || task.Contains("dinner") || task.Contains("free time"))) { c.lastSocialHour = SimClock.hours; c.mood += 0.0004f * GameState.timeScale * Time.deltaTime; }
            if (arrived && task.Contains("greenhouse")) { c.mood += 0.0006f * GameState.timeScale * Time.deltaTime; }
        }
    }
}
