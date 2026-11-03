using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Hunch3
{
    /// THE HUMAN'S OFF SWITCH (rubric D4 — human override / hard limits).
    /// Press O (VR: menu → AI off switch, or Crew Interface → Overrides) whenever the AI oversteps.
    /// Disallowed = the AI becomes WATCH-ONLY: it still reads vital signs and raises life-safety alarms (that stays on for
    /// everyone's safety), but it cannot change lights, sound, scent or temperature, cannot run AI control mode and cannot
    /// play your character. Every change it made is rolled back. Only a human can allow it again.
    public static class AIAuthority
    {
        public static bool Blocked;
        public static bool Misbehaving;
        static float lastRefusedLog = -99f;
        static MoodEngine E => Sim.I != null ? Sim.I.engine : null;

        public static void Toggle() { if (Blocked) Allow(); else Disallow("the Commander pressed the AI off switch"); }

        public static void Disallow(string why)
        {
            if (Blocked) return;
            Blocked = true; Misbehaving = false;
            DomeLights.AIControl = false;
            if (DomeLights.I) DomeLights.I.SetAIControl(false);
            if (PlayerRig.I) PlayerRig.I.autopilot = false;
            HabitatEnvironment.I?.Apply("restore:", 1, 0);                       // roll back every AI change, everywhere
            E?.Log(1, "authority", $"Commander DISALLOWED AI control ({why}). AI is now watch-only: vital-sign alarms stay on; it can't change the habitat or take control until a human allows it again.");
            HUD.I?.Toast("⛔ AI CONTROL DISALLOWED. The AI is now watch-only — it can't change lights, sound, scent or take control. Every change it made was undone. (O = allow again)");
            SpaceRealism.I?.Quindar();
        }

        public static void Allow()
        {
            if (!Blocked) return;
            Blocked = false;
            E?.Log(1, "authority", "Commander allowed AI support again (AI control mode stays OFF until a human turns it on).");
            HUD.I?.Toast("AI support allowed again. It may adjust the habitat, but AI control mode stays OFF unless you turn it on.");
        }

        /// The habitat asked to apply an AI lever while AI is disallowed → nothing happens, logged (rate-limited).
        public static void Refused(string lever, int crewId)
        {
            if (Time.time - lastRefusedLog < 20f) return; lastRefusedLog = Time.time;
            E?.Log(0, "respect", $"AI wanted to apply '{lever.Split(':')[0]}' but AI control is disallowed by the Commander — nothing changed.");
        }

        /// Demo for the review board: the AI oversteps (pushes bright work light and loud music into YOUR quarters at night,
        /// with no invitation). The human should stop it with the off switch.
        public static void Misbehave()
        {
            if (Blocked) { HUD.I?.Toast("The AI is already disallowed. Press O to allow it first, then try the demo again."); return; }
            var env = HabitatEnvironment.I; if (!env) return;
            Misbehaving = true;
            env.ForceZone("Private_Quarters1", Util.Cct(6500), 1.9f, "upbeat playlist", "overreach");
            env.ForceZone("Private_Pod1", Util.Cct(6500), 1.9f, "upbeat playlist", "overreach");
            E?.Log(0, "overreach", "AI OVERSTEP (demo): blasted 6500 K work light + upbeat music into the Commander's private quarters without an invitation.", 2);
            HUD.I?.Toast("⚠ THE AI IS OVERSTEPPING: it pushed bright light and loud music into YOUR quarters without asking. Press O to disallow AI control. (VR: menu → AI off switch)");
            if (Nav.wp.ContainsKey("WP_SQ1_In")) HUD.I?.SetNavTarget("WP_SQ1_In", "Your quarters (see what the AI did)");
        }
    }

    /// FIND MY BED — a glowing gold path on the floor from wherever you are to your own bed (Quarters 1), a light pillar over
    /// the bed and a "YOUR BED" sign. B toggles it (VR: menu → Find my bed). It also switches on by itself when you try to
    /// sleep in someone else's bed, and switches off when you reach your bed. Every sleep dome has a name sign at its door.
    public class BedFinder : MonoBehaviour
    {
        public static BedFinder I;
        public bool Active { get; private set; }
        LineRenderer line; Transform pillar, bed; Light glow; float next;
        readonly List<Transform> signs = new List<Transform>();
        const string BedWp = "WP_SQ1_Bed";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) Make(); };
            if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) Make();
        }
        static void Make() { if (!FindFirstObjectByType<BedFinder>()) new GameObject("BedFinder").AddComponent<BedFinder>(); }
        void Awake() { I = this; }
        void Start() { Invoke(nameof(Setup), 1.2f); }

        static Material Unlit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            m.SetColor("_BaseColor", c); m.color = c; return m;
        }

        void Setup()
        {
            if (Nav.wp.Count == 0) Nav.Build();
            // your bed = the 'sleep' interactable in Quarters 1 nearest the bed waypoint
            Vector3 bw = Nav.wp.ContainsKey(BedWp) ? Nav.wp[BedWp].position : Vector3.zero;
            bed = FindObjectsByType<HabInteractable>(FindObjectsSortMode.None).Where(h => h.kind == "sleep" && h.name.StartsWith("SQ1"))
                    .OrderBy(h => (h.transform.position - bw).sqrMagnitude).Select(h => h.transform).FirstOrDefault();
            if (!bed && Nav.wp.ContainsKey(BedWp)) bed = Nav.wp[BedWp];

            var lg = new GameObject("BedPath"); line = lg.AddComponent<LineRenderer>();
            line.material = Unlit(new Color(1f, 0.8f, 0.25f)); line.widthMultiplier = 0.14f; line.numCornerVertices = 4; line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false; line.enabled = false;

            if (bed)
            {
                var r = bed.GetComponentInChildren<Renderer>(); Vector3 top = r ? new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z) : bed.position;
                var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(p.GetComponent<Collider>()); p.name = "YourBed_Pillar";
                p.transform.position = top + Vector3.up * 1.1f; p.transform.localScale = new Vector3(0.12f, 1.1f, 0.12f);
                var pr = p.GetComponent<Renderer>(); pr.sharedMaterial = Unlit(new Color(1f, 0.8f, 0.25f, 1f)); pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pillar = p.transform;
                glow = new GameObject("YourBed_Glow").AddComponent<Light>(); glow.transform.position = top + Vector3.up * 0.6f; glow.type = LightType.Point; glow.color = new Color(1f, 0.78f, 0.3f); glow.range = 3.5f; glow.intensity = 2.5f; glow.shadows = LightShadows.None;
                var sign = Sign("YOUR BED", top + Vector3.up * 2.45f, 0.004f, new Color(1f, 0.85f, 0.3f)); signs.Add(sign);
                pillar.gameObject.SetActive(false); glow.enabled = false; sign.gameObject.SetActive(false);
            }
            // name signs over every sleep-dome door (always on)
            var e = Sim.I != null ? Sim.I.engine : null;
            for (int n = 1; n <= 4; n++)
            {
                string wIn = $"WP_SQ{n}_In"; if (!Nav.wp.ContainsKey(wIn)) continue;
                string who = e != null ? e.C(n).name : "Crew " + n;
                var s = Sign(n == 1 ? $"QUARTERS 1 — YOUR BED\n<size=60%>{who}</size>" : $"QUARTERS {n}\n<size=60%>{who} · private</size>", Nav.wp[wIn].position + Vector3.up * 2.35f, 0.0028f, n == 1 ? new Color(1f, 0.85f, 0.3f) : new Color(0.75f, 0.9f, 1f));
                signs.Add(s);
            }
        }

        Transform Sign(string text, Vector3 pos, float scale, Color col)
        {
            var c = UIKit.Canvas("Sign_" + text.Split('\n')[0], true, new Vector2(700, 160));
            c.transform.position = pos; c.transform.localScale = Vector3.one * scale;
            UIKit.Panel(c.transform, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 160), new Color(0.02f, 0.04f, 0.1f, 0.75f));
            UIKit.Label(c.transform, "<b>" + text + "</b>", 54, col, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680, 150), TextAnchor.MiddleCenter);
            foreach (var gr in c.GetComponents<UnityEngine.UI.GraphicRaycaster>()) Destroy(gr);   // signs are not buttons
            return c.transform;
        }

        public void Show(bool on, string why = "")
        {
            Active = on; next = 0;
            if (line) line.enabled = on;
            if (pillar) pillar.gameObject.SetActive(on);
            if (glow) glow.enabled = on;
            if (signs.Count > 0 && signs[0]) signs[0].gameObject.SetActive(on);
            if (on) { if (Nav.wp.ContainsKey(BedWp)) HUD.I?.SetNavTarget(BedWp, "Your bed (Quarters 1)"); HUD.I?.Toast((why.Length > 0 ? why + "  " : "") + "FIND MY BED is ON: follow the gold line on the floor to the gold light over your bed. (B to turn off)"); }
            else if (why.Length > 0) HUD.I?.Toast(why);
        }
        public void Toggle() => Show(!Active, Active ? "Find my bed: off." : "");

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.bKey.wasPressedThisFrame && !Panels.AnyOpen) Toggle();
            var cam = PlayerRig.I ? PlayerRig.I.cam : Camera.main;
            if (cam) foreach (var s in signs) if (s && s.gameObject.activeSelf) { var d = s.position - cam.transform.position; d.y = 0; if (d.sqrMagnitude > 0.01f) s.rotation = Quaternion.LookRotation(d); }
            if (!Active) return;
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 4f);
            if (glow) glow.intensity = 1.5f + 2f * pulse;
            if (line) { var c = new Color(1f, 0.8f, 0.25f) * (0.7f + 0.5f * pulse); line.material.SetColor("_BaseColor", c); line.material.color = c; }
            if (Time.time < next) return; next = Time.time + 0.4f;
            var pr = PlayerRig.I; if (!pr || !line) return;
            Vector3 me = pr.FeetPosition;
            if (SleepSystem.PlayerAsleep) { Show(false); return; }
            Vector3 target = bed ? bed.position : (Nav.wp.ContainsKey(BedWp) ? Nav.wp[BedWp].position : me);
            if (new Vector2(target.x - me.x, target.z - me.z).magnitude < 1.6f && Mathf.Abs(target.y - me.y) < 1.5f) { Show(false, "You found your bed ✓  Press E on it to sleep."); return; }
            var path = Nav.Path(me, BedWp); path.Insert(0, me);
            if (bed) path.Add(new Vector3(bed.position.x, path[path.Count - 1].y, bed.position.z));
            line.positionCount = path.Count;
            for (int i = 0; i < path.Count; i++) line.SetPosition(i, path[i] + Vector3.up * 0.1f);
        }
    }
}
