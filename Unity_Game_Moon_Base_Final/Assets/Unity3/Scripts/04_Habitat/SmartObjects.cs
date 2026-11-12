// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/04_Habitat/SmartObjects.cs
//  Section : 04_Habitat
//  Purpose : Every wellness / medical / life-support asset is a SYSTEM, not a prop. Look at one and it
//            highlights in its category colour with a holographic label; context keys run real actions
//            that change crew health and habitat telemetry; TAB opens diagnostics + AI recommendations;
//            holding the right mouse button runs an engineering scan (integrity, last service, AI failure
//            prediction) and E while scanning performs maintenance. Equipment wears out over mission time.
//  Colours : green = life support · blue = psychological health · yellow = research/ops ·
//            purple = medical · red = emergency
//  Contains: SysCat, SmartAction, SmartObject, SmartInteractor, WellnessSpawner, SmartCatalog
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Hunch3
{
    public enum SysCat { LifeSupport, Psych, Research, Medical, Emergency }

    public class SmartAction
    {
        public Key key; public string label; public Func<SmartObject, string> run;
        public SmartAction(Key k, string l, Func<SmartObject, string> r) { key = k; label = l; run = r; }
    }

    /// One habitat system the crew can operate. Built by WellnessSpawner from a Resources/Wellness FBX.
    public class SmartObject : MonoBehaviour
    {
        public static readonly List<SmartObject> All = new List<SmartObject>();
        public static SmartObject Hover;
        public string key, title, why, state = "Nominal", mode = "";
        public SysCat cat; public float integrity = .9f, lastServiceHours, powerW = 40f; public int uses;
        public readonly List<SmartAction> actions = new List<SmartAction>();
        public Func<SmartObject, string[]> telemetry = _ => new string[0];
        public Func<SmartObject, string[]> advice = _ => new string[0];
        public Action<SmartObject> tick;
        public GameObject model; public Bounds localBounds;
        GameObject highlight; TextMesh label; float lastHours = -1, nextLabel;

        public bool Failed => integrity < .35f;
        public float Eff => Failed ? .5f : 1f;                    // a worn-out system only gives half its benefit
        public static Color CatColor(SysCat c) => c == SysCat.LifeSupport ? new Color(.25f, 1f, .45f) : c == SysCat.Psych ? new Color(.35f, .62f, 1f) : c == SysCat.Research ? new Color(1f, .85f, .2f) : c == SysCat.Medical ? new Color(.78f, .45f, 1f) : new Color(1f, .3f, .3f);
        public static string CatName(SysCat c) => c == SysCat.LifeSupport ? "LIFE SUPPORT" : c == SysCat.Psych ? "PSYCHOLOGICAL HEALTH" : c == SysCat.Research ? "RESEARCH / OPERATIONS" : c == SysCat.Medical ? "MEDICAL" : "EMERGENCY";
        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public void Init()
        {
            All.Add(this);
            var bc = gameObject.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.center = localBounds.center; bc.size = localBounds.size + Vector3.one * .06f;
            AddSolidColliders(model);
            try { var xr = gameObject.AddComponent<XRSimpleInteractable>(); xr.selectEntered.AddListener(_ => { if (actions.Count > 0) SmartInteractor.Report(this, actions[0].run(this)); }); } catch (Exception e) { Debug.LogWarning("[HUNCH3] XR on " + key + ": " + e.Message); }
            BuildHighlight();
            lastServiceHours = SimClock.hours - UnityEngine.Random.Range(24f, 24f * 20f);
        }
        public static void AddSolidColliders(GameObject g)
        {
            if (!g) return;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh && mf.sharedMesh.bounds.size.magnitude * mf.transform.lossyScale.magnitude > .5f && !mf.GetComponent<Collider>())
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
        void OnDestroy() { All.Remove(this); }

        void BuildHighlight()
        {
            highlight = new GameObject("Highlight"); highlight.transform.SetParent(transform, false);
            var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(sh); m.color = CatColor(cat);
            Vector3 mn = localBounds.min - Vector3.one * .03f, mx = localBounds.max + Vector3.one * .03f;
            Vector3[] c = { new Vector3(mn.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mx.z), new Vector3(mn.x, mn.y, mx.z) };
            Vector3 up = new Vector3(0, mx.y - mn.y, 0);
            void Line(params Vector3[] pts)
            {
                var g = new GameObject("edge"); g.transform.SetParent(highlight.transform, false); var lr = g.AddComponent<LineRenderer>();
                lr.useWorldSpace = false; lr.material = m; lr.startColor = lr.endColor = CatColor(cat); lr.widthMultiplier = .012f; lr.positionCount = pts.Length; lr.SetPositions(pts); lr.shadowCastingMode = ShadowCastingMode.Off;
            }
            Line(c[0], c[1], c[2], c[3], c[0]); Line(c[0] + up, c[1] + up, c[2] + up, c[3] + up, c[0] + up);
            for (int i = 0; i < 4; i++) Line(c[i], c[i] + up);
            var lg = new GameObject("HoloLabel"); lg.transform.SetParent(highlight.transform, false); lg.transform.localPosition = new Vector3(localBounds.center.x, mx.y + .12f, localBounds.center.z);
            label = lg.AddComponent<TextMesh>(); label.characterSize = .011f; label.fontSize = 60; label.anchor = TextAnchor.LowerCenter; label.alignment = TextAlignment.Left; label.richText = true;
            try { var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); if (f) { label.font = f; lg.GetComponent<MeshRenderer>().sharedMaterial = f.material; } } catch { }
            highlight.SetActive(false);
        }

        public void SetHighlight(bool on) { if (highlight && highlight.activeSelf != on) { highlight.SetActive(on); nextLabel = 0; } }

        public string Status => Failed ? "<color=#ff5555>MAINTENANCE REQUIRED</color>" : state;

        void Update()
        {
            // wear: ~1.2 % per mission day (a system nobody services fails in ~6 weeks)
            if (lastHours < 0) lastHours = SimClock.hours;
            float dh = SimClock.hours - lastHours; lastHours = SimClock.hours;
            if (dh > 0 && dh < 48) integrity = Mathf.Max(.05f, integrity - dh * .0005f);
            tick?.Invoke(this);
            if (highlight && highlight.activeSelf)
            {
                var cam = PlayerRig.I ? PlayerRig.I.cam : Camera.main;
                if (cam) { label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position); }
                if (Time.time > nextLabel) { nextLabel = Time.time + .3f; label.text = LabelText(); }
            }
        }

        string LabelText()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"<color=#{Hex(CatColor(cat))}>[ {CatName(cat)} ]</color>\n<b>{title.ToUpper()}</b>\nStatus: {Status}\n");
            foreach (var l in telemetry(this).Take(3)) sb.Append(l).Append('\n');
            sb.Append($"Integrity {integrity * 100:0}%\n");
            sb.Append("<color=#9fdcff>");
            foreach (var a in actions) sb.Append($"[{a.key}] {a.label}   ");
            sb.Append("\n[TAB] Diagnostics   hold [RMB] Engineering scan</color>");
            return sb.ToString();
        }

        public string[] Diagnostics()
        {
            var l = new List<string> { $"<b>{title.ToUpper()}</b>  ·  {CatName(cat)}", why, "", "<b>LIVE TELEMETRY</b>" };
            l.AddRange(telemetry(this)); l.Add("");
            var adv = advice(this); if (adv.Length > 0) { l.Add("<b>AI RECOMMENDATIONS</b>"); l.AddRange(adv.Select(a => "+ " + a)); }
            l.Add(""); l.Add($"Used {uses}x this mission · mode: {(string.IsNullOrEmpty(mode) ? "standard" : mode)}");
            return l.ToArray();
        }
        public string[] Engineering()
        {
            float days = (SimClock.hours - lastServiceHours) / 24f, fail = Mathf.Pow(1f - integrity, 1.6f) * 70f;
            return new[] { $"<b>ENGINEERING INSPECTION · {title.ToUpper()}</b>", $"Integrity: {integrity * 100:0}%", $"Last maintenance: {days:0} days ago", $"AI failure prediction (30 d): {fail:0}%", $"Power draw: {powerW:0} W", Failed ? "<color=#ff6666>Output halved until serviced.</color>" : "Within limits.", "", "<b>[E] perform maintenance</b> while scanning (≈10 min of crew time)" };
        }
        public string Service()
        {
            integrity = 1f; lastServiceHours = SimClock.hours; var me = SmartCatalog.Me; if (me != null) { me.fatigue += .02f; me.autonomy += .02f; me.Clamp01(); }
            return $"{title}: serviced — integrity 100%. Maintenance logged to the habitat dashboard.";
        }
    }

    /// Desktop look-at selection, context keys, TAB diagnostics and hold-RMB engineering scan.
    public class SmartInteractor : MonoBehaviour
    {
        bool diag; string last = ""; float lastUntil; SmartObject lastObj;
        GUIStyle box, txt;
        public static void Report(SmartObject so, string msg)
        {
            if (string.IsNullOrEmpty(msg)) return; so.uses++;
            HUD.I?.Toast(msg); var si = FindFirstObjectByType<SmartInteractor>(); if (si) { si.last = msg; si.lastUntil = Time.time + 6f; si.lastObj = so; }
        }
        bool Scanning => Mouse.current != null && Mouse.current.rightButton.isPressed;

        void Update()
        {
            var pr = PlayerRig.I; SmartObject hit = null;
            if (pr && pr.cam && !pr.isXR)
            {
                var origin = ThirdPersonView.Active ? ThirdPersonView.Eye : pr.cam.transform.position;
                var hits = Physics.RaycastAll(new Ray(origin, pr.cam.transform.forward), 3.6f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance);
                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(pr.transform)) continue;
                    hit = h.collider.GetComponentInParent<SmartObject>(); break;
                }
            }
            if (SmartObject.Hover && SmartObject.Hover != hit) SmartObject.Hover.SetHighlight(false);
            SmartObject.Hover = hit; if (!hit) { diag = false; return; }
            hit.SetHighlight(true);
            var kb = Keyboard.current; if (kb == null || Panels.AnyOpen) return;
            if (kb.tabKey.wasPressedThisFrame) diag = !diag;
            if (Scanning) { if (kb.eKey.wasPressedThisFrame) Report(hit, hit.Service()); return; }
            foreach (var a in hit.actions)
                if (kb[a.key].wasPressedThisFrame) { Report(hit, a.run(hit)); break; }
        }

        void OnGUI()
        {
            var so = SmartObject.Hover; if (!so) return;
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, fontSize = 15, wordWrap = true, padding = new RectOffset(14, 14, 12, 12) };
                var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, new Color(.02f, .05f, .12f, .88f)); tex.Apply(); box.normal.background = tex; box.normal.textColor = Color.white;
            }
            string[] lines = Scanning ? so.Engineering() : diag ? so.Diagnostics() : null;
            if (lines == null) return;
            float w = Mathf.Min(460, Screen.width * .4f), h = 30 + lines.Length * 21;
            var r = new Rect(Screen.width - w - 20, Screen.height * .18f, w, h);
            var c = SmartObject.CatColor(so.cat); GUI.color = c; GUI.Box(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), GUIContent.none); GUI.color = Color.white;
            GUI.Box(r, string.Join("\n", lines), box);
        }
    }

    /// Builds every wellness / medical / life-support system from Resources/Wellness and finds a clear,
    /// reachable spot for it in the right zone (never on a walkway, never through a wall).
    public class WellnessSpawner : MonoBehaviour
    {
        public struct Spec { public string key, anchor; public bool wall; public float plinth, scale; public Spec(string k, string a, bool w = false, float p = 0, float s = 1) { key = k; anchor = a; wall = w; plinth = p; scale = s; } }
        public static readonly Spec[] Realistic =
        {
            new Spec("recovery_pod_healthy", "WP_H_C"), new Spec("medical_scanner", "WP_H_C"), new Spec("heart_rate_monitor", "WP_H_C"), new Spec("bp_monitor", "WP_H_C", true),
            new Spec("smart_bed", "WP_H_C"), new Spec("ai_health_hub", "WP_N_C"), new Spec("habitat_console", "WP_N_C"), new Spec("sensor_wall", "WP_N_C", true),
            new Spec("eclss_core", "WP_EclA_In"), new Spec("oxygen_tanks", "WP_EclA_In"), new Spec("greenhouse", "WP_C_Mid"), new Spec("emotional_garden", "WP_C_Mid"),
            new Spec("sanctuary_pod", "WP_D_VR"), new Spec("earth_window", "WP_D_Obs", true), new Spec("hologram_companion", "WP_Up_Lounge"), new Spec("stress_lounge", "WP_R_C"),
            new Spec("emergency_mh_station", "WP_R_C", true), new Spec("mood_tracker", "WP_K_C", true), new Spec("maintenance_drone", "WP_E_C"),
            new Spec("ai_wristband", "WP_SQ1_Health", false, .95f, 5f), new Spec("circadian_light", "WP_SQ1_Bed", false, .55f, 1f),
        };
        public Spec[] specs = Realistic; public int placed;
        public static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

        void Start() { Invoke(nameof(SpawnAll), 2.5f); }
        void SpawnAll()
        {
            var log = new List<string>();
            foreach (var s in specs)
            {
                try { log.Add(Spawn(s)); } catch (Exception e) { log.Add($"{s.key}: ERROR {e.Message}"); }
            }
            Debug.Log("[HUNCH3] smart systems placed " + placed + "/" + specs.Length + "\n" + string.Join("\n", log));
            try { System.IO.Directory.CreateDirectory("Reports"); System.IO.File.WriteAllLines("Reports/smart_objects.txt", log); } catch { }
        }

        public static GameObject LoadModel(string key)
        {
            var pf = Resources.Load<GameObject>("Wellness/" + key); if (!pf) return null;
            var g = Instantiate(pf); g.name = key; FixMaterials(g); return g;
        }
        string Spawn(Spec s)
        {
            var model = LoadModel(s.key); if (!model) return $"{s.key}: model missing in Resources/Wellness";
            if (!Nav.wp.TryGetValue(s.anchor, out var anchor) || !anchor) anchor = Util.Find(s.anchor);
            if (!anchor) { Destroy(model); return $"{s.key}: anchor {s.anchor} not found"; }
            var root = new GameObject("SMART_" + s.key);
            // front of the asset (Blender -Y) from the FRONT_MARKER empty, measured before anything rotates
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); model.transform.localScale *= s.scale;
            var fm = Util.FindDeep(model.transform, "FRONT_MARKER"); Vector3 front = fm ? fm.position - model.transform.position : Vector3.back; front.y = 0; front = front.sqrMagnitude > 1e-4f ? front.normalized : Vector3.back;
            var mb = WorldBounds(model);
            if (s.plinth > 0)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(p.GetComponent<Collider>()); p.name = "Display_Plinth"; p.transform.SetParent(root.transform, false);
                p.transform.localScale = new Vector3(.45f, s.plinth / 2f, .45f); p.transform.localPosition = new Vector3(0, s.plinth / 2f, 0); p.GetComponent<Renderer>().sharedMaterial = Lit("Plinth", new Color(.88f, .9f, .93f));
                model.transform.position = new Vector3(-mb.center.x, s.plinth - mb.min.y + .01f, -mb.center.z);
            }
            else model.transform.position = new Vector3(-mb.center.x, -mb.min.y + .01f, -mb.center.z);
            model.transform.SetParent(root.transform, true);
            var lb = WorldBounds(root);   // root still at origin, identity → world == local
            if (!Place(root.transform, lb, front, anchor, s.wall, out string how)) { Destroy(root); return $"{s.key}: no clear spot near {s.anchor} (skipped)"; }
            var so = root.AddComponent<SmartObject>(); so.key = s.key; so.model = model; so.localBounds = lb;
            SmartCatalog.Configure(so); so.Init(); placed++;
            return $"{s.key}: placed {how} near {s.anchor} at {root.transform.position:F2} size {lb.size:F2}";
        }

        static Bounds WorldBounds(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one * .3f);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        static bool Blocked(Vector3 center, Vector3 half, Quaternion rot, Transform self)
        {
            foreach (var c in Physics.OverlapBox(center, half, rot, ~0, QueryTriggerInteraction.Ignore))
            {
                if (self && c.transform.IsChildOf(self)) continue;
                if (PlayerRig.I && c.transform.IsChildOf(PlayerRig.I.transform)) continue;
                if (c.GetComponentInParent<CrewAgent>()) continue;
                return true;
            }
            return false;
        }

        static bool Place(Transform root, Bounds lb, Vector3 front, Transform anchor, bool wall, out string how)
        {
            how = "";
            Vector3 a = anchor.position; float floorY = a.y;
            if (Physics.Raycast(a + Vector3.up * .6f, Vector3.down, out var fh, 4f, ~0, QueryTriggerInteraction.Ignore)) floorY = fh.point.y;
            Vector3 af = new Vector3(a.x, floorY, a.z);
            Vector3 half = lb.extents; float radius = Mathf.Max(half.x, half.z);
            int seed = Mathf.Abs(root.name.GetHashCode()) % 360;
            for (int pass = 0; pass < 2; pass++)
            {
                float clear = pass == 0 ? .7f : .3f;
                if (wall)
                {
                    for (int k = 0; k < 48; k++)
                    {
                        float ang = seed + k * 7.5f; var dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                        foreach (float hgt in new[] { 1.3f, 1.0f })
                        {
                            if (!Physics.Raycast(af + Vector3.up * hgt, dir, out var wh, 5f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(wh.normal.y) > .3f) continue;
                            if (wh.collider.GetComponentInParent<SmartObject>() || wh.collider.GetComponentInParent<CrewAgent>()) continue;
                            var n = new Vector3(wh.normal.x, 0, wh.normal.z).normalized;
                            var rot = Quaternion.FromToRotation(front, n);
                            var foot = wh.point + n * (half.z + .04f); foot.y = floorY;
                            if (Try(root, lb, rot, foot, floorY, clear, out how)) { how = "on wall" + how; return true; }
                        }
                    }
                }
                else
                {
                    for (float r = Mathf.Max(1f, radius + .5f); r < 6f; r += .35f)
                        for (int k = 0; k < 18; k++)
                        {
                            var dir = Quaternion.Euler(0, seed + k * 20f, 0) * Vector3.forward; var p = af + dir * r;
                            if (Physics.Linecast(af + Vector3.up * 1.1f, p + Vector3.up * 1.1f, ~0, QueryTriggerInteraction.Ignore)) continue;   // a wall in between
                            var face = af - p; face.y = 0; var rot = Quaternion.FromToRotation(front, face.normalized);
                            if (Try(root, lb, rot, p, floorY, clear, out how)) { how = "on floor" + how; return true; }
                        }
                }
            }
            return false;
        }

        static bool Try(Transform root, Bounds lb, Quaternion rot, Vector3 foot, float floorY, float clear, out string how)
        {
            how = "";
            if (!Physics.Raycast(foot + Vector3.up * 1.2f, Vector3.down, out var gh, 2.4f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(gh.point.y - floorY) > .25f) return false;
            Vector3 pos = new Vector3(foot.x, gh.point.y, foot.z) - rot * new Vector3(lb.center.x, 0, lb.center.z);
            Vector3 c = pos + rot * lb.center + Vector3.up * .04f, h = lb.extents + new Vector3(.04f, -.06f, .04f);
            if (Blocked(c, h, rot, root)) return false;
            float rad = Mathf.Max(lb.extents.x, lb.extents.z);
            foreach (var w in Nav.wp.Values)
            {
                if (!w || Mathf.Abs(w.position.y - gh.point.y) > 1.6f) continue;
                var d = w.position - c; d.y = 0; if (d.magnitude < rad + clear) return false;      // keep walkways & doorways clear
            }
            root.SetPositionAndRotation(pos, rot); how = clear < .5f ? " (tight)" : ""; return true;
        }

        // ---------------- materials: FBX colours → URP Lit; EMIT_* glow, GLASS_* transparent ----------------
        public static Material Lit(string name, Color col)
        {
            if (matCache.TryGetValue(name, out var m) && m) return m;
            var sh = Shader.Find("Universal Render Pipeline/Lit"); m = sh ? new Material(sh) : new Material(Shader.Find("Standard")); m.name = "SO_" + name;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col); m.color = col;
            string n = name.ToLower(); bool metal = n.Contains("alumin") || n.Contains("gold");
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal ? .9f : 0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", metal ? .72f : n.Contains("screen") || n.Contains("glass") ? .9f : .4f);
            if (name.Contains("EMIT_"))
            {
                m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(col.r, col.g, col.b) * 2.4f);
            }
            if (name.Contains("GLASS_"))
            {
                col.a = n.Contains("water") ? .45f : n.Contains("electro") ? .45f : .22f;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
            }
            matCache[name] = m; return m;
        }
        public static void FixMaterials(GameObject g)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (!ms[i]) continue; string n = ms[i].name.Replace(" (Instance)", "");
                    Color c = ms[i].HasProperty("_BaseColor") ? ms[i].GetColor("_BaseColor") : ms[i].HasProperty("_Color") ? ms[i].color : Color.white;
                    ms[i] = Lit(n, c);
                }
                r.sharedMaterials = ms;
            }
        }
    }

    /// What each system is for, what it shows, and what each key really does to the crew & habitat.
    public static class SmartCatalog
    {
        public static CrewMember Me => Sim.I != null && Sim.I.engine != null ? Sim.I.engine.C(1) : null;
        static HabitatEnvironment Env => HabitatEnvironment.I;
        static float Co2 => Env && Env.zones.Count > 0 ? Env.zones.Average(z => z.co2) : 520f;
        static string P(float v) => $"{Mathf.Clamp01(v) * 100:0}%";
        static void Fx(float stress = 0, float mood = 0, float fatigue = 0, float sleep = 0, float novelty = 0, float autonomy = 0, float eff = 1)
        {
            var c = Me; if (c == null) return;
            c.stress += stress * eff; c.mood += mood * eff; c.fatigue += fatigue * eff; c.novelty += novelty * eff; c.autonomy += autonomy * eff;
            c.sleepQ = Mathf.Clamp01(c.sleepQ + sleep * eff); c.Clamp01();
        }
        static void Co2All(float d) { if (Env) foreach (var z in Env.zones) z.co2 = Mathf.Max(420f, z.co2 + d); }
        public static float filterDays = 14f, waterRecovery = 92f, o2Kg = 27.2f, harvestKg; static int earthView, lightMode, podMode;
        static readonly string[] PodModes = { "Circadian Reset", "Stress Recovery", "VR Relaxation", "Isolation Countermeasure", "Medical Recovery" };

        static void A(SmartObject so, Key k, string label, Func<SmartObject, string> run) => so.actions.Add(new SmartAction(k, label, run));

        public static void Configure(SmartObject so)
        {
            var me = Me;
            switch (so.key)
            {
                case "recovery_pod_healthy":
                    so.title = "Adaptive Recovery Pod"; so.cat = SysCat.Psych; so.powerW = 120;
                    so.why = "Sleep + recovery capsule. Sensor mat, EEG headrest and canopy sensors feed the AI; it adapts light, temperature, sound and the canopy tint, and shows Earth / a counsellor when stress is detected.";
                    so.telemetry = s => new[] { $"Sleep quality {P(Me?.sleepQ ?? .85f)}", $"Stress {P(Me?.stress ?? .2f)} · Fatigue {P(Me?.fatigue ?? .2f)}", $"Mode: {PodModes[podMode]}" };
                    so.advice = s => Advice();
                    A(so, Key.E, "Enter pod", s => { if (SleepSystem.I) { SleepSystem.I.BeginSleep(s.transform); return "Recovery pod: canopy closing, lights dimming to 1800 K. Sleep well."; } Fx(fatigue: -.25f, sleep: .12f, eff: s.Eff); return "Recovery pod: 20-minute recovery nap logged (fatigue down, sleep quality up)."; });
                    A(so, Key.F, "Sleep analytics", s => $"Sleep analytics: quality {P(Me?.sleepQ ?? .85f)} · deep 22% · REM 21% · HR 54 · 3 wake-ups. Trend: {(Me != null && Me.sleepQ < .7f ? "worse than your baseline — AI suggests a circadian reset" : "at your baseline")}.");
                    A(so, Key.R, "Run recovery session", s => { podMode = (podMode + 1) % PodModes.Length; s.mode = PodModes[podMode]; switch (podMode) { case 0: Fx(sleep: .08f, fatigue: -.05f, eff: s.Eff); break; case 1: Fx(stress: -.15f, eff: s.Eff); break; case 2: Fx(stress: -.08f, novelty: .1f, eff: s.Eff); break; case 3: Fx(mood: .08f, novelty: .06f, eff: s.Eff); break; default: Fx(fatigue: -.1f, eff: s.Eff); break; } return $"Recovery session: {PodModes[podMode]} running. Expected recovery +18 mental wellness."; });
                    so.tick = PodTick; break;
                case "medical_scanner":
                    so.title = "Smart Medical Scanner Bay"; so.cat = SysCat.Medical; so.powerW = 450;
                    so.why = "CMO station: non-contact mmWave + thermal + PPG scan arch, vitals monitor, AED, ultrasound, O2 and IV. Every scan is consent-gated and stays with the crew member unless they share it.";
                    so.telemetry = s => new[] { "HR 66 · SpO2 98% · BP 118/76", "Temp 37.0 °C · Resp 14", $"Last scan: {(s.uses > 0 ? "this session" : "none today")}" };
                    so.advice = s => new[] { "Weekly full scan keeps the personal baseline accurate", "Share with CMO only with consent (Crew panel)" };
                    A(so, Key.E, "Begin scan", s => { Fx(stress: -.02f); if (Me != null) Me.shareHealthWithCmo = Me.shareHealthWithCmo; return "Scan complete (your consent): HR 66, HRV 52 ms, SpO2 98%, BP 118/76, 37.0 °C, no anomalies. Saved to YOUR health record."; });
                    A(so, Key.F, "Crew health history", s => "Health history (yours): 14 scans · resting HR 64→62 · BP stable · sleep 7.3 h avg · no CMO alerts.");
                    break;
                case "heart_rate_monitor":
                    so.title = "Heart-Rate & Vitals Monitor"; so.cat = SysCat.Medical; so.powerW = 35;
                    so.why = "Chest-strap ECG + finger pulse-ox. Heart rate and HRV are core stress/anxiety signals — exercise is context-tagged so it is never mistaken for anxiety.";
                    so.telemetry = s => new[] { $"HR {Mathf.RoundToInt(60 + (Me?.stress ?? .2f) * 40)} bpm · HRV {Mathf.RoundToInt(70 - (Me?.stress ?? .2f) * 40)} ms", "SpO2 98% · Resp 14", Me != null && Me.exercising ? "Context: EXERCISE (not anxiety)" : "Context: rest" };
                    A(so, Key.E, "Measure now", s => $"Heart: {Mathf.RoundToInt(60 + (Me?.stress ?? .2f) * 40)} bpm, HRV {Mathf.RoundToInt(70 - (Me?.stress ?? .2f) * 40)} ms. {(Me != null && Me.stress > .5f ? "HRV is low — try the 6-breaths/min exercise at the sanctuary pod." : "Within your normal range.")}");
                    A(so, Key.F, "24 h trend", s => "24 h: resting HR 58–64, HRV dips after the 14:00 task block (fatigue pattern), recovers after exercise.");
                    break;
                case "bp_monitor":
                    so.title = "Blood-Pressure Tracker"; so.cat = SysCat.Medical; so.powerW = 15;
                    so.why = "Automatic cuff. Fluid shifts in 1/6 g and chronic stress both show up in blood pressure; readings auto-log to the CMO only with consent.";
                    so.telemetry = s => new[] { $"Last: {118 + Mathf.RoundToInt((Me?.stress ?? .2f) * 14)}/{76 + Mathf.RoundToInt((Me?.stress ?? .2f) * 8)} mmHg", "Pulse 68", "Trend: stable" };
                    A(so, Key.E, "Take reading", s => $"Blood pressure {118 + Mathf.RoundToInt((Me?.stress ?? .2f) * 14)}/{76 + Mathf.RoundToInt((Me?.stress ?? .2f) * 8)} mmHg, pulse 68. Logged to your record.");
                    break;
                case "smart_bed":
                    so.title = "AI Sleep-Detection Bed"; so.cat = SysCat.Medical; so.powerW = 25;
                    so.why = "Observation bed in the medical bay: under-mattress ballistocardiography tracks heart rate, breathing and movement with nothing worn; the headboard software shows the hypnogram.";
                    so.telemetry = s => new[] { $"Sleep score {Mathf.RoundToInt((Me?.sleepQ ?? .85f) * 100)}", "HR 54 · Resp 13 · 19.5 °C", "Stages: deep 22% · REM 21%" };
                    so.advice = s => Advice();
                    A(so, Key.E, "Rest (observed)", s => { Fx(fatigue: -.15f, sleep: .06f, eff: s.Eff); return "Resting in the observation bed: breathing and HR tracked contact-free. Fatigue down."; });
                    A(so, Key.F, "Sleep software", s => "Sleep software: 7 h 38 m · sleep score 86 · 3 awakenings · temperature held at 19.5 °C.");
                    break;
                case "ai_health_hub":
                    so.title = "AI Health Detection System"; so.cat = SysCat.Psych; so.powerW = 300;
                    so.why = "On-board edge computer: fuses 9 sensor types into scores for the 6 conditions. No cloud, encrypted, hard limits enforced in code, human review every 7 days.";
                    so.telemetry = s => new[] { $"Your wellbeing {P(Me?.Wellbeing ?? .8f)} · tier {Me?.MaxTier ?? 0}", "Sensor fusion 9/9 · confidence 0.94", "Hard-limit violations blocked: logged" };
                    A(so, Key.E, "Open AI dashboard", s => { Panels.Open("dashboard"); return ""; });
                    A(so, Key.F, "Explain my scores", s => $"Your scores: stress {P(Me?.stress ?? .2f)}, fatigue {P(Me?.fatigue ?? .2f)}, mood {P(Me?.mood ?? .75f)}. Only you see these unless you choose to share.");
                    break;
                case "habitat_console":
                    so.title = "Smart Habitat Control Console"; so.cat = SysCat.Research; so.powerW = 180;
                    so.why = "Command & comms: habitat health, power, radiation, crew wellness trends (aggregate only — never one person's data without consent).";
                    so.telemetry = s => new[] { $"O2 {(Env ? Env.o2Percent : 21f):0.0}% · CO2 {Co2:0} ppm", $"Power {(Env ? Env.powerKW : 52):0} kW · solar {(Env ? Env.solarKW : 12):0} kW", "Radiation 0.4 mSv/day" };
                    A(so, Key.E, "Habitat dashboard", s => { Panels.Open("dashboard"); return ""; });
                    A(so, Key.F, "Crew wellness trend", s => "Crew wellness (aggregate, 7 days): sleep 7.4 h · stress down 12% · no open alerts.");
                    break;
                case "sensor_wall":
                    so.title = "Biometrics Sensor Array"; so.cat = SysCat.LifeSupport; so.powerW = 60;
                    so.why = "Room sensors: camera (shared zones only), mic array, paired CO2, temperature, UWB location anchor, radiation dosimeter. If one fails the AI drops that signal and tells the crew.";
                    so.telemetry = s => new[] { $"CO2 {Co2:0} ppm · 21.5 °C", "18 / 18 sensors online", s.Failed ? "DRIFT DETECTED — recalibrate" : "Calibration OK" };
                    A(so, Key.E, "Sensor status", s => "Sensors: 18/18 online. Cameras exist only in shared zones — never in pods, bathrooms, VR retreat or call booth.");
                    A(so, Key.R, "Recalibrate", s => { s.integrity = Mathf.Max(s.integrity, .95f); return "Sensor array recalibrated against the reference CO2 cell."; });
                    break;
                case "eclss_core":
                    so.title = "ECLSS Core"; so.cat = SysCat.LifeSupport; so.powerW = 2200;
                    so.why = "Environmental Control & Life Support: CO2 scrubbers, oxygen generation, water recovery. High CO2 causes fatigue and headaches, so it feeds the fatigue signal.";
                    so.telemetry = s => new[] { $"O2 {(Env ? Env.o2Percent : 21f):0.0}% · CO2 {Co2:0} ppm", $"Water recovery {waterRecovery:0}%", $"AI: filter change in {filterDays:0} days" };
                    so.advice = s => Co2 > 800 ? new[] { "CO2 is high — replace the scrubber filter (R)" } : new string[0];
                    A(so, Key.E, "Inspect atmosphere", s => $"Atmosphere: O2 {(Env ? Env.o2Percent : 21f):0.0}%, CO2 {Co2:0} ppm, 70.3 kPa. CO2 removal {(s.Failed ? "DEGRADED" : "active")}.");
                    A(so, Key.R, "Replace scrubber filter", s => { filterDays = 90; Co2All(-350f * s.Eff); s.integrity = Mathf.Max(s.integrity, .9f); Fx(fatigue: -.05f); return "Scrubber filter replaced: CO2 dropping in every zone — the crew will feel sharper."; });
                    A(so, Key.F, "Water recovery", s => $"Water recovery {waterRecovery:0}% · urine → UPA → drinking water · tank {(Env ? Env.waterL : 2850):0} L.");
                    so.tick = s => { filterDays = Mathf.Max(0, filterDays - Time.deltaTime / 600f); if (s.Failed && Env) foreach (var z in Env.zones) z.co2 += Time.deltaTime * .5f; };
                    break;
                case "oxygen_tanks":
                    so.title = "Oxygen Tank Rack"; so.cat = SysCat.LifeSupport; so.powerW = 0;
                    so.why = "High-pressure O2 reserve: backup if oxygen generation fails, and medical oxygen for the CMO. Oxidizer — no flames, regulators checked weekly.";
                    so.telemetry = s => new[] { $"Reserve {o2Kg:0.0} kg (4 x 6.8 kg)", "Manifold 2200 psi", o2Kg < 12 ? "LOW — order resupply" : "Days of backup: 9" };
                    A(so, Key.E, "Check pressures", s => "O2 tanks: 2200 / 2150 / 2050 / 980 psi. Tank 4 below 50% — swap scheduled.");
                    A(so, Key.R, "Swap empty tank", s => { o2Kg = 27.2f; s.integrity = 1f; return "Tank 4 swapped and leak-checked. Reserve back to 27.2 kg."; });
                    break;
                case "greenhouse":
                    so.title = "Modular Lunar Greenhouse"; so.cat = SysCat.LifeSupport; so.powerW = 900;
                    so.why = "Hydroponic towers make food and oxygen; tending plants is behavioural activation — the top evidence-based countermeasure for low mood.";
                    so.telemetry = s => new[] { "Growth 94% · pH 6.0 · EC 1.8", "O2 production +2.1 kg/day", $"Harvested {harvestKg:0.0} kg" };
                    A(so, Key.E, "Enter care mode", s => { Fx(mood: .06f, stress: -.05f, novelty: .03f, eff: s.Eff); return "Care mode: you pruned and checked the towers. Mood up (behavioural activation)."; });
                    A(so, Key.F, "Inspect plant health", s => "Plant health: 96 healthy, 3 yellowing (nutrient EC low in tower 2), 0 mould.");
                    A(so, Key.G, "Harvest", s => { harvestKg += .4f; Fx(mood: .05f, eff: s.Eff); return "Harvested 0.4 kg of lettuce & herbs — fresh food is a morale boost."; });
                    break;
                case "emotional_garden":
                    so.title = "Emotional Support Garden"; so.cat = SysCat.Psych; so.powerW = 120;
                    so.why = "Flowers, soil smell and a bench: biophilic design that lowers stress and breaks monotony. The AI suggests it for low mood.";
                    so.telemetry = s => new[] { "16 blooms · soil 22 °C", "Water 1.2 L today", "Mood effect: +12 (crew avg)" };
                    A(so, Key.E, "Plant & tend", s => { Fx(mood: .06f, stress: -.04f, novelty: .05f, eff: s.Eff); return "You planted new seeds with the AI planting arm. Something to watch grow."; });
                    A(so, Key.G, "Sit on the bench", s => { Fx(stress: -.06f, eff: s.Eff); return "Five quiet minutes among the flowers. Stress down."; });
                    break;
                case "sanctuary_pod":
                    so.title = "Personal Sanctuary Pod"; so.cat = SysCat.Psych; so.powerW = 90;
                    so.why = "Noise-cancelling acoustic shell, recliner, VR headset dock and holo-plants. The breathing light guides 6 breaths per minute. No cameras or mics inside.";
                    so.telemetry = s => new[] { "Noise -38 dB", $"Calm {P(1 - (Me?.stress ?? .2f))}", "Occupied: no" };
                    A(so, Key.E, "Breathing session", s => { Fx(stress: -.12f, eff: s.Eff); return "Guided breathing (6 breaths/min, 5 min). Heart-rate variability up, stress down."; });
                    A(so, Key.R, "VR nature session", s => { Fx(stress: -.06f, novelty: .12f, mood: .04f, eff: s.Eff); return "VR: forest walk on Earth. Novelty up — fights sensory monotony."; });
                    break;
                case "earth_window":
                    so.title = "Virtual Earth Window"; so.cat = SysCat.Psych; so.powerW = 140;
                    so.why = "A live Earth view and nature scenes on a high-brightness panel — counters isolation and the grey monotony of the lunar surface.";
                    so.telemetry = s => new[] { $"View: {new[] { "Earth live", "Forest", "Ocean", "Home city" }[earthView]}", "1.3 s signal delay", "" };
                    A(so, Key.E, "Change view", s => { earthView = (earthView + 1) % 4; Fx(novelty: .05f, mood: .03f, eff: s.Eff); return $"Window view: {new[] { "Earth live", "Forest", "Ocean", "Home city" }[earthView]}."; });
                    break;
                case "hologram_companion":
                    so.title = "AI Companion Hologram"; so.cat = SysCat.Psych; so.powerW = 80;
                    so.why = "A virtual counsellor for check-ins and CBT-style coaching. It suggests, never orders; you can switch it off anytime; it never reports you without telling you first.";
                    so.telemetry = s => new[] { "Companion: available", $"Check-ins this week: {s.uses}", "Privacy: on-board only" };
                    A(so, Key.E, "Talk to companion", s => { Fx(stress: -.06f, mood: .04f, autonomy: .02f, eff: s.Eff); var c = Me; return c != null && c.stress > .5f ? "Companion: \"You've had a heavy few days. Want to try 5 minutes in the sanctuary pod, or call home?\"" : c != null && c.mood < .5f ? "Companion: \"How about tending the greenhouse with Okafor this afternoon?\"" : "Companion: \"You're doing well. Anything on your mind today?\""; });
                    A(so, Key.F, "Mood check-in", s => { if (Me != null) { Me.selfMood = Me.mood < .5f ? .6f : .2f; Me.selfStress = Me.stress; Me.selfAt = SimClock.hours; } return "Mood check-in saved (2 questions). Your answer counts more than any sensor for 24 h."; });
                    break;
                case "stress_lounge":
                    so.title = "Stress Recovery Lounge"; so.cat = SysCat.Psych; so.powerW = 110;
                    so.why = "Soft seating, warm 2700 K light, nature mode on the wall screen — the shared place to decompress together after hard shifts.";
                    so.telemetry = s => new[] { "Light 2700 K warm", "Nature mode: forest", $"Crew here: {CrewAgent.All.Count(a => a && Vector3.Distance(a.transform.position, s.transform.position) < 4f)}" };
                    A(so, Key.E, "Sit & unwind", s => { Fx(stress: -.08f, mood: .05f, eff: s.Eff); if (Me != null) Me.lastSocialHour = SimClock.hours; return "You sit back in the lounge. Stress down, and time near crewmates counts as social contact."; });
                    A(so, Key.F, "Nature mode", s => { Fx(novelty: .06f, eff: s.Eff); return "Lounge screen: rain forest soundscape and view."; });
                    break;
                case "emergency_mh_station":
                    so.title = "Emergency Mental Health Station"; so.cat = SysCat.Emergency; so.powerW = 20;
                    so.why = "24/7 private line to the flight surgeon / psychologist, breathing guide and comfort kit. Always works — even if the AI is switched off.";
                    so.telemetry = s => new[] { "Line to Earth: ready (1.3 s delay)", "Privacy curtain: available", "AI cannot listen in" };
                    A(so, Key.E, "TALK NOW", s => { Fx(stress: -.1f, mood: .04f); return "Private line open to the flight psychologist. Nothing is recorded by the habitat AI."; });
                    A(so, Key.R, "Breathing exercise", s => { Fx(stress: -.08f); return "Box breathing: in 4 s · hold 4 s · out 4 s · hold 4 s. Repeat 6 times."; });
                    A(so, Key.F, "Comfort kit", s => { Fx(stress: -.03f); return "Comfort kit: weighted blanket, noise-cancelling headset, family photos, stress ball."; });
                    break;
                case "mood_tracker":
                    so.title = "Mood Health Tracker"; so.cat = SysCat.Psych; so.powerW = 10;
                    so.why = "Voluntary 2-tap mood check-in. Self-report is weighted above sensors for 24 h, and the trend helps the AI learn your personal baseline.";
                    so.telemetry = s => new[] { $"Your mood {P(Me?.mood ?? .75f)}", "14-day trend: improving", "Visible to: only you" };
                    A(so, Key.E, "Check in: good", s => { if (Me != null) { Me.selfMood = .2f; Me.selfStress = .2f; Me.selfAt = SimClock.hours; } Fx(autonomy: .02f); return "Logged: feeling good. Thanks — your answer outranks the sensors for 24 h."; });
                    A(so, Key.R, "Check in: low", s => { if (Me != null) { Me.selfMood = .7f; Me.selfStress = .6f; Me.selfAt = SimClock.hours; } return "Logged: feeling low. The AI will OFFER (never force) support: greenhouse, call home or the sanctuary pod."; });
                    break;
                case "maintenance_drone":
                    so.title = "Autonomous Maintenance Drone"; so.cat = SysCat.Research; so.powerW = 250;
                    so.why = "Inspects seals, filters and wiring, brushes off lunar dust and predicts failures before they happen — so the crew spends less time on chores.";
                    so.telemetry = s => { var worst = SmartObject.All.Where(o => o != s).OrderBy(o => o.integrity).FirstOrDefault(); return new[] { "Battery 86%", worst ? $"Worst system: {worst.title} {worst.integrity * 100:0}%" : "All systems nominal", "Dust brushes: 72% life" }; };
                    A(so, Key.E, "Dispatch inspection", s => { var worst = SmartObject.All.Where(o => o != s).OrderBy(o => o.integrity).FirstOrDefault(); if (!worst) return "Drone: nothing to inspect."; worst.integrity = Mathf.Min(1f, worst.integrity + .3f * s.Eff); worst.lastServiceHours = SimClock.hours; return $"Drone inspected and cleaned the {worst.title}: integrity now {worst.integrity * 100:0}%."; });
                    A(so, Key.R, "Recharge", s => { s.integrity = Mathf.Max(s.integrity, .9f); return "Drone docked and charging."; });
                    break;
                case "ai_wristband":
                    so.title = "AI Monitoring Wristband (display, 5x scale)"; so.cat = SysCat.Psych; so.powerW = 0;
                    so.why = "Worn at night: PPG heart rate, skin conductance (stress), skin temperature and motion (sleep). The mood ring shows your state only to you unless you choose to share it.";
                    so.telemetry = s => new[] { $"HR {Mathf.RoundToInt(58 + (Me?.stress ?? .2f) * 30)} · stress {(Me != null && Me.stress > .5f ? "HIGH" : "LOW")}", $"Sleep {P(Me?.sleepQ ?? .85f)}", $"Mood ring: {(Me != null && Me.shareMoodPublic ? "shared" : "private")}" };
                    A(so, Key.E, "Sync wristband", s => "Wristband synced: last night HR 52–61, 2 stress spikes (both during the 14:00 task), sleep 7 h 20 m.");
                    A(so, Key.F, "Share mood ring", s => { if (Me != null) Me.shareMoodPublic = !Me.shareMoodPublic; return Me != null && Me.shareMoodPublic ? "Your mood ring is now visible to crewmates (you can switch it off any time)." : "Your mood ring is private again."; });
                    break;
                case "circadian_light":
                    so.title = "Adaptive Circadian Light"; so.cat = SysCat.Psych; so.powerW = 12;
                    so.why = "Bedside sunrise lamp: blue-rich light to wake, warm melatonin-safe light at night. Keeps the body clock on a 24 h day under a 29.5-day lunar day.";
                    so.telemetry = s => new[] { $"Mode: {new[] { "Sunrise 06:30", "Focus 6500 K", "Wind-down 1800 K" }[lightMode]}", "Melatonin-safe after 21:00", "" };
                    A(so, Key.E, "Change light mode", s => { lightMode = (lightMode + 1) % 3; if (lightMode == 2) Fx(sleep: .05f); if (lightMode == 1) Fx(fatigue: -.04f); return $"Circadian light: {new[] { "sunrise alarm set for 06:30", "focus light 6500 K", "wind-down 1800 K (melatonin-safe)" }[lightMode]}."; });
                    break;
                default:
                    so.title = so.key.Replace("_", " "); so.cat = SysCat.Research; so.why = "Habitat system.";
                    A(so, Key.E, "Use", s => s.title + " used."); break;
            }
        }

        static string[] Advice()
        {
            var c = Me; if (c == null) return new string[0];
            var l = new List<string>();
            if (c.sleepQ < .75f) { l.Add("Circadian light therapy 20 min"); l.Add("Earth nature simulation before bed"); }
            if (c.stress > .45f) { l.Add("Stress Recovery mode (R) or the sanctuary pod"); l.Add("Talk to the AI companion or call home"); }
            if (c.mood < .55f) l.Add("Greenhouse activity session (behavioural activation)");
            if (c.fatigue > .5f) l.Add("20-minute recovery nap");
            if (l.Count == 0) l.Add("You're at your baseline — keep the routine");
            l.Add("Expected recovery: +18 mental wellness");
            return l.ToArray();
        }

        /// The pod model itself changes with the person's state: green = healthy, yellow = fatigue (circadian fix), blue = stress (calm protocol).
        static float nextPod;
        static void PodTick(SmartObject so)
        {
            if (Time.time < nextPod || Me == null) return; nextPod = Time.time + 3f;
            string want = Me.stress > .55f ? "stress" : (Me.fatigue > .55f || Me.sleepQ < .6f) ? "fatigue" : "healthy";
            so.state = want == "stress" ? "<color=#5b8cff>STRESS — calm protocol</color>" : want == "fatigue" ? "<color=#ffd23f>FATIGUE — circadian reset</color>" : "<color=#3fff7a>HEALTHY</color>";
            if (so.model && so.model.name == "recovery_pod_" + want) return;
            var m = WellnessSpawner.LoadModel("recovery_pod_" + want); if (!m) return;
            m.transform.SetParent(so.transform, false);
            if (so.model) { m.transform.localPosition = so.model.transform.localPosition; m.transform.localRotation = so.model.transform.localRotation; m.transform.localScale = so.model.transform.localScale; UnityEngine.Object.Destroy(so.model); }
            so.model = m; SmartObject.AddSolidColliders(m);
        }
    }
}
