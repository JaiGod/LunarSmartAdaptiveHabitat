using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hunch3
{
    /// DOME STATUS LIGHTS + AI CONTROL MODE
    ///
    /// Every dome has a status beacon on its apex (visible outside and on the exterior cameras) that shows the rest cycle:
    ///   quarters (SQ1–4): asleep → slow blue "breathing" (6/min) · resting/withdrawn in quarters → soft amber pulse ·
    ///                     awake inside → steady warm white · empty → off · that person at Tier 3 → amber double-flash
    ///   shared domes:     day → steady white · quiet hours (22:00–07:00) → dim slow blue · dawn → warm ramp
    ///   any hazard:       all domes flash red; the muster dome alternates red/white so people can find it
    ///   Command dome:     green blink = AI in control · amber = AI has handed control to a human
    ///
    /// AI CONTROL MODE (rubric: Tier 1 silent, Tier 2 consent, Tier 3 human decision, hard limits):
    /// the AI runs the base and plays your character (autopilot) until something needs a human — an alarm, a Tier-3
    /// escalation, an offer that needs YOUR consent, or a hard-limit block. Then it stops, tells you why, points you to
    /// the problem and hands you control. When the problem is resolved it offers to take over again.
    public class DomeLights : MonoBehaviour
    {
        public static DomeLights I;
        public static bool AIControl = false;                // OPTIONAL: off at start; turn on from the Q / wrist menu
        public bool AIHasControl { get; private set; }
        public string HandoverReason = "";

        class Beacon { public string code; public Transform t; public Renderer r; public Light l; public int crew; public bool common; }
        readonly List<Beacon> beacons = new List<Beacon>();
        MaterialPropertyBlock mpb;
        int lastEsc, lastBlocked; bool lastHazard; float resumeAt = -1, startAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) Make(); };
            if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) Make();
        }
        static void Make() { if (!FindFirstObjectByType<DomeLights>()) new GameObject("DomeLights").AddComponent<DomeLights>(); }
        void Awake() { I = this; mpb = new MaterialPropertyBlock(); }

        void Start()
        {
            startAt = Time.time;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")); mat.SetColor("_BaseColor", Color.white);
            // dome roofs: "<code>12_Node_DomeRoof"; the core habitat uses its upper inflatable shell
            var roofs = Util.FindPrefix("").Where(t => t.name.EndsWith("12_Node_DomeRoof") && !t.name.StartsWith("HOLO_")).ToList();
            var core = Util.Find("CORE_C10_UpperDeck_Inflatable_BetaCloth_Outer"); if (core) roofs.Add(core);
            foreach (var roof in roofs)
            {
                var rr = roof.GetComponent<Renderer>(); if (!rr) continue;
                var b = rr.bounds; Vector3 apex = new Vector3(b.center.x, b.max.y + 0.12f, b.center.z);
                string code = roof == core ? "CORE" : roof.name.Substring(0, roof.name.IndexOf("12_Node"));
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(g.GetComponent<Collider>()); g.name = "DomeBeacon_" + code;
                g.transform.position = apex; g.transform.localScale = new Vector3(0.45f, 0.22f, 0.45f);
                var r = g.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var l = new GameObject("DomeBeaconLight").AddComponent<Light>(); l.transform.SetParent(g.transform, false); l.transform.localPosition = Vector3.up * 1.2f;
                l.type = LightType.Point; l.range = 9f; l.intensity = 0f; l.shadows = LightShadows.None;
                int crew = code.StartsWith("SQ") && code.Length >= 3 && char.IsDigit(code[2]) ? code[2] - '0' : 0;
                beacons.Add(new Beacon { code = code, t = g.transform, r = r, l = l, crew = crew, common = crew == 0 });
            }
            var e = Sim.I != null ? Sim.I.engine : null;
            lastEsc = e != null ? e.escalations : 0; lastBlocked = EthicsGuard.blocked;
            if (AIControl && GameState.mode == GameMode.Sandbox) Invoke(nameof(TakeControlFirstTime), 3f);
            else if (GameState.mode == GameMode.Sandbox) Invoke(nameof(Hint), 6f);
        }

        void TakeControlFirstTime()
        {
            if (!AIControl || !PlayerRig.I) return;
            GiveToAI("AI control mode: the AI runs the base and walks your day for you. It will stop and hand control to you the moment something needs a human decision. (Q menu → AI control on/off · P takes manual control at any time)");
        }

        void Hint() { if (!AIControl) HUD.I?.Toast("You are in control. Optional: Q menu → \"AI control\" lets the AI run your day and call you when something needs a human."); }

        public void SetAIControl(bool on)
        {
            AIControl = on;
            if (on) GiveToAI("AI control ON — the AI runs the routine; you'll be called when something needs a human.");
            else { AIHasControl = false; if (PlayerRig.I) PlayerRig.I.autopilot = false; HUD.I?.Toast("AI control OFF — you are in command."); }
        }

        void GiveToAI(string msg)
        {
            var pr = PlayerRig.I; if (!pr) return;
            if (AIAuthority.Blocked) { AIControl = false; HUD.I?.Toast("AI control is DISALLOWED by the Commander (O = allow again)."); return; }
            AIHasControl = true; HandoverReason = ""; pr.autopilot = true; resumeAt = -1;
            HUD.I?.Toast("🤖 " + msg);
            Sim.I?.engine.Log(0, "ai-control", "AI took routine control (autopilot + Tier-1 environment levers). Human-only decisions remain with the crew.");
        }

        void HandToHuman(string reason, string navWp = null, string navLabel = null)
        {
            var pr = PlayerRig.I; if (!pr) return;
            AIHasControl = false; HandoverReason = reason; pr.autopilot = false;
            HUD.I?.Toast("⚠ AI → YOU: " + reason + "  (The AI stopped autopilot. It never makes this call for you.)");
            if (!string.IsNullOrEmpty(navWp)) HUD.I?.SetNavTarget(navWp, navLabel ?? navWp);
            Sim.I?.engine.Log(0, "handover", "AI handed control to the Commander: " + reason, 2);
            SpaceRealism.I?.Quindar();
        }

        void Update()
        {
            var e = Sim.I != null ? Sim.I.engine : null; var pr = PlayerRig.I; if (e == null || !pr) return;
            // --- player pressed P / B (manual takeover) while AI had control
            if (AIHasControl && !pr.autopilot && Time.time - startAt > 4f) { AIHasControl = false; HandoverReason = "manual takeover"; HUD.I?.Toast("You took manual control. (Q menu → AI control to give it back.)"); }
            // --- what needs a human?
            bool hazard = Hazards.I && Hazards.I.Active;
            if (AIControl)
            {
                if (hazard && !lastHazard) HandToHuman($"{Hazards.I.Title}. Lead the crew to {Hazards.I.MusterLabel}.", Hazards.I.Muster, Hazards.I.MusterLabel);
                else if (e.escalations > lastEsc) HandToHuman("Tier-3 escalation — a crew member needs a human decision (CMO / Commander). Open the CMO console (F2 / menu).");
                else if (e.offers.Any(o => o.crew == 1 && !o.done) && AIHasControl) HandToHuman("The AI has an offer for YOU — it needs your consent (Y / N). It will not decide for you.");
                else if (EthicsGuard.blocked > lastBlocked && AIHasControl) HandToHuman("The AI hit a hard limit (blocked action). Review it on the AI dashboard (F1). If the AI is overstepping, press O to disallow AI control.");
                else if (EthicsGuard.ReviewOverdue && AIHasControl) HandToHuman("Weekly human review of the AI is overdue — the AI drops to Tier 1 until a human signs it off.");
                // resume when the problem is gone
                bool problem = hazard || e.offers.Any(o => o.crew == 1 && !o.done) || e.escalatedTo.Count > 0;
                if (!AIHasControl && HandoverReason != "manual takeover" && !string.IsNullOrEmpty(HandoverReason) && !problem)
                {
                    if (resumeAt < 0) { resumeAt = Time.time + 20f; HUD.I?.Toast("Problem resolved. The AI will resume routine control in 20 s (press P to stay in manual)."); }
                    else if (Time.time > resumeAt) GiveToAI("AI resumed routine control.");
                }
                else if (problem) resumeAt = -1;
            }
            lastHazard = hazard; lastEsc = e.escalations; lastBlocked = EthicsGuard.blocked;
            UpdateBeacons(e, hazard);
        }

        void UpdateBeacons(MoodEngine e, bool hazard)
        {
            float t = Time.time, hod = SimClock.HourOfDay;
            bool quiet = hod >= 22f || hod < 7f, dawn = hod >= 6.25f && hod < 7f;
            string musterZone = "";
            if (hazard && Nav.wp.ContainsKey(Hazards.I.Muster) && HabitatEnvironment.I) { var z = HabitatEnvironment.I.ZoneAt(Nav.wp[Hazards.I.Muster].position); if (z != null) musterZone = z.name; }
            foreach (var b in beacons)
            {
                Color col; float k;
                if (hazard)
                {
                    bool muster = !string.IsNullOrEmpty(musterZone) && HabitatEnvironment.I.ZoneAt(b.t.position - Vector3.up * 3f)?.name == musterZone;
                    bool on = Mathf.Repeat(t * 2f, 1f) < 0.5f;
                    col = muster ? (on ? Color.white : new Color(1f, 0.08f, 0.05f)) : new Color(1f, 0.08f, 0.05f); k = muster ? 1.6f : (on ? 1.4f : 0.1f);
                }
                else if (b.crew > 0)
                {
                    var c = e.C(b.crew);
                    bool inside = c.inPod || (b.crew == 1 && SleepSystem.PlayerAsleep);
                    bool asleep = c.asleep || (b.crew == 1 && SleepSystem.PlayerAsleep);
                    bool resting = inside && !asleep && (c.activity.Contains("rest") || c.activity.Contains("withdrawn") || c.activity.Contains("free") || c.activity.Contains("nap"));
                    if (c.MaxTier >= 3) { float p = Mathf.Repeat(t, 1.2f); col = new Color(1f, 0.6f, 0.1f); k = (p < 0.12f || (p > 0.25f && p < 0.37f)) ? 1.5f : 0.05f; }
                    else if (asleep) { col = new Color(0.25f, 0.45f, 1f); k = 0.12f + 0.38f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 0.1f)); }       // 6 breaths / min
                    else if (resting) { col = new Color(1f, 0.62f, 0.25f); k = 0.25f + 0.3f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 0.2f)); }
                    else if (inside) { col = new Color(1f, 0.85f, 0.65f); k = 0.6f; }
                    else { col = Color.black; k = 0f; }
                }
                else
                {
                    if (b.code == "N")
                    {   // Command dome shows who is in control
                        bool ai = AIControl && AIHasControl;
                        col = ai ? new Color(0.2f, 1f, 0.35f) : new Color(1f, 0.7f, 0.15f);
                        k = ai ? (Mathf.Repeat(t, 3f) < 0.25f ? 1.4f : 0.15f) : 0.9f;
                    }
                    else if (dawn) { col = Color.Lerp(new Color(1f, 0.45f, 0.2f), new Color(1f, 0.9f, 0.75f), Mathf.InverseLerp(6.25f, 7f, hod)); k = 0.7f; }
                    else if (quiet) { col = new Color(0.3f, 0.45f, 1f); k = 0.1f + 0.2f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 0.05f)); }
                    else { col = new Color(1f, 0.97f, 0.9f); k = 0.55f; }
                }
                mpb.Clear(); mpb.SetColor("_BaseColor", col * Mathf.Max(0.05f, k)); b.r.SetPropertyBlock(mpb);
                b.r.enabled = k > 0.01f || col != Color.black;
                b.l.color = col; b.l.intensity = 3f * k;
            }
        }
    }
}
