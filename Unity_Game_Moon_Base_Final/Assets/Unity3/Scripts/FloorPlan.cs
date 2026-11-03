using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hunch3
{
    /// D2 ANNOTATED FLOOR PLAN — drawn on top of the big map (M), never in the 3D view (layer 31 = map cameras only).
    ///   • every zone: tinted area in its ARES colour + label "name · ARES type · ⌀ size · lighting"
    ///   • every sensor: icon + its coverage circle (camera, voice mic, CO2 pair, motion/door)
    ///   • every environmental control node: lighting panel, speaker array, scent dispenser, airflow vent
    ///   • Configuration B pieces glow orange when B is deployed (acoustic screens, 2nd dining table, privacy screen, alt-route light)
    /// The legend and scale bar are drawn by the HUD next to the big map.
    public class FloorPlan : MonoBehaviour
    {
        public static FloorPlan I;
        const int Layer = 31;
        readonly List<Transform> labels = new List<Transform>();
        readonly List<GameObject> cbMarks = new List<GameObject>();
        GameObject root; bool shown;
        public static readonly (string key, string label, Color col, float cover)[] Kinds = {
            ("X_FACS_Camera",       "Face-coding camera (shared zones only)", new Color(1f, 0.35f, 0.35f), 4.0f),
            ("X_VoiceMic_Array",    "Voice mic array (on-device)",          new Color(1f, 0.65f, 0.2f), 5.0f),
            ("X_CO2_AirQuality",    "CO2 / air-quality pair",               new Color(0.4f, 0.85f, 1f), 3.0f),
            ("Motion",              "Motion / door / UWB sensor",           new Color(0.75f, 0.75f, 0.75f), 2.5f),
            ("Speaker",             "Speaker array (control node)",         new Color(0.75f, 0.45f, 1f), 0f),
            ("Scent_D",             "Scent dispenser (control node)",       new Color(0.4f, 1f, 0.45f), 0f),
            ("CeilingLight",        "Tunable lighting panel (control node)", new Color(1f, 0.9f, 0.35f), 0f),
            ("Airflow",             "Airflow / thermal vent (control node)", new Color(0.92f, 0.95f, 1f), 0f) };
        public static readonly Dictionary<string, Color> ZoneCol = new Dictionary<string, Color> {
            {"communal", new Color(1f, 0.6f, 0.25f)}, {"private", new Color(0.65f, 0.45f, 1f)}, {"work", new Color(0.35f, 0.7f, 1f)},
            {"exercise", new Color(1f, 0.35f, 0.4f)}, {"nature", new Color(0.35f, 0.9f, 0.4f)}, {"transition", new Color(0.85f, 0.85f, 0.6f)},
            {"medical", new Color(1f, 1f, 1f)}, {"support", new Color(0.6f, 0.6f, 0.65f)}, {"eva", new Color(0.9f, 0.7f, 0.3f)} };
        public static readonly Dictionary<string, int> KindCount = new Dictionary<string, int>();
        /// ARES zone type → device kind → how many (filled from the real habitat)
        public static readonly Dictionary<string, Dictionary<string, int>> PerType = new Dictionary<string, Dictionary<string, int>>();
        public static bool Ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) Make(); };
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("LunarHabitat")) Make();
        }
        static void Make() { if (!FindFirstObjectByType<FloorPlan>()) new GameObject("FloorPlan_D2").AddComponent<FloorPlan>(); }
        void Awake() { I = this; }
        void Start() { Invoke(nameof(Build), 1.5f); }

        static Material Mat(Color c, float a) => HUD.Glow(c, a);

        GameObject Disc(Vector3 at, float r, Color c, float a, string nm)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = nm; Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(root.transform, true); g.transform.position = at; g.transform.localScale = new Vector3(r * 2f, 0.005f, r * 2f);
            var rr = g.GetComponent<Renderer>(); rr.sharedMaterial = Mat(c, a); rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false;
            g.layer = Layer; return g;
        }
        GameObject Ring(Vector3 at, float r, Color c, string nm)
        {
            var g = new GameObject(nm); g.layer = Layer; g.transform.SetParent(root.transform, false);
            var lr = g.AddComponent<LineRenderer>(); lr.useWorldSpace = true; lr.loop = true; int n = 40; lr.positionCount = n;
            for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2; lr.SetPosition(i, at + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)); }
            lr.widthMultiplier = 0.2f; lr.sharedMaterial = Mat(c, 0.9f); lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }
        void Label(Vector3 at, string text, float size, Color c)
        {
            var g = new GameObject("PlanLabel"); g.layer = Layer; g.transform.SetParent(root.transform, false); g.transform.position = at;
            var tm = g.AddComponent<TextMesh>(); tm.text = text; tm.font = UIKit.F; tm.fontSize = 64; tm.characterSize = size; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = c; tm.richText = false;
            var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = UIKit.F.material; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            labels.Add(g.transform);
        }

        void Build()
        {
            var env = HabitatEnvironment.I; if (!env) return;
            root = new GameObject("FloorPlan_Overlay");
            // 1) zones
            foreach (var z in env.zones)
            {
                Color c = ZoneCol.TryGetValue(z.type, out var zc) ? zc : Color.gray;
                float y = z.center.y + 2.2f;                                    // just under the map's slice plane (feet + 2.3 m) → drawn OVER furniture and walls
                Disc(new Vector3(z.center.x, y, z.center.z), z.radius, c, 0.16f, "PlanZone_" + z.name);
                Ring(new Vector3(z.center.x, y + 0.01f, z.center.z), z.radius, c, "PlanZoneEdge_" + z.name);
                if (z.type == "transition" && z.radius < 2f) continue;       // short tunnels: colour only, no label (keeps the plan readable)
                string light = string.IsNullOrEmpty(z.lighting) ? "" : "\n" + (z.lighting.Length > 26 ? z.lighting.Substring(0, 26) + "…" : z.lighting);
                Label(new Vector3(z.center.x, y + 0.08f, z.center.z), $"{z.name.Replace("_", " ")}\n{z.type.ToUpper()} · ⌀ {z.radius * 2f:0.0} m{light}", z.radius > 2.5f ? 0.16f : 0.11f, Color.white);
            }
            // 2) sensors + control nodes (real objects in the habitat)
            var all = Util.SceneRoots().SelectMany(Util.All).Where(t => t.gameObject.layer != Layer && !t.name.StartsWith("HOLO_")).ToList();
            KindCount.Clear(); PerType.Clear();
            foreach (var k in Kinds)
            {
                var found = all.Where(t => t.name.Contains(k.key) && (t.GetComponent<Renderer>() || t.childCount == 0)).ToList();
                if (k.key == "Motion") found = all.Where(t => (t.name.Contains("Motion_Proximity") || t.name.Contains("DoorMotion") || t.name.Contains("EntryMotion")) && t.GetComponent<Renderer>()).ToList();
                // one marker per physical device (a device can be several meshes)
                var spots = new List<Vector3>();
                foreach (var t in found) { var p = t.position; if (spots.All(s => (s - p).sqrMagnitude > 0.16f)) spots.Add(p); }
                KindCount[k.key] = spots.Count;
                foreach (var p in spots)
                {
                    var z = env.ZoneAt(p); float fy = (z != null ? z.center.y : p.y) + 2.22f;
                    string zt = z != null ? z.type : "other";
                    if (!PerType.TryGetValue(zt, out var dct)) PerType[zt] = dct = new Dictionary<string, int>();
                    dct[k.key] = (dct.TryGetValue(k.key, out var c0) ? c0 : 0) + 1;
                    var icon = GameObject.CreatePrimitive(k.cover > 0 ? PrimitiveType.Sphere : PrimitiveType.Cube); icon.name = "PlanIcon_" + k.key; Destroy(icon.GetComponent<Collider>());
                    icon.transform.SetParent(root.transform, true); icon.transform.position = new Vector3(p.x, fy + 0.05f, p.z); icon.transform.localScale = Vector3.one * (k.cover > 0 ? 0.7f : 0.5f);
                    var r = icon.GetComponent<Renderer>(); r.sharedMaterial = Mat(k.col, 1f); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; icon.layer = Layer;
                    if (k.cover > 0) Ring(new Vector3(p.x, fy + 0.03f, p.z), k.cover, new Color(k.col.r, k.col.g, k.col.b), "PlanCover_" + k.key);
                }
            }
            // 3) Configuration B deployables
            foreach (var t in all.Where(t => t.name.Length > 3 && t.name.StartsWith("CB") && char.IsDigit(t.name[2])))
            {
                var z = env.ZoneAt(t.position); float fy = (z != null ? z.center.y : t.position.y) + 2.24f;
                var m = Disc(new Vector3(t.position.x, fy, t.position.z), 0.6f, new Color(1f, 0.5f, 0.1f), 0.85f, "PlanCB_" + t.name); cbMarks.Add(m);
            }
            Ready = true;
            Show(false);
        }

        public void Show(bool on) { shown = on; if (root) root.SetActive(on); }
        void LateUpdate()
        {
            if (!root || !shown) return;
            // map rotates with the player: keep labels upright on the map
            float yaw = PlayerRig.I && PlayerRig.I.cam ? PlayerRig.I.cam.transform.eulerAngles.y : 0f;
            var rot = Quaternion.Euler(90f, yaw, 0f);
            foreach (var l in labels) if (l) l.rotation = rot;
            bool b = GameState.configB; float k = 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
            foreach (var g in cbMarks) if (g) { g.SetActive(b); g.transform.localScale = new Vector3(1.2f * k + 0.2f, 0.005f, 1.2f * k + 0.2f); }
        }

        public static string Legend()
        {
            var sb = new System.Text.StringBuilder("<b>D2 ANNOTATED FLOOR PLAN</b>\nM closes · C switches Config A ↔ B\n\n<b>ARES zones</b>");
            foreach (var kv in ZoneCol.Where(kv => kv.Key != "support" && kv.Key != "eva" && kv.Key != "medical")) sb.Append($"\n  <color=#{UIKit.Hex(kv.Value)}>■</color> {kv.Key}");
            sb.Append("\n\n<b>Sensors</b> (ring = coverage area)");
            foreach (var k in Kinds.Where(k => k.cover > 0)) sb.Append($"\n  <color=#{UIKit.Hex(k.col)}>●</color> {k.label.Split('(')[0].Trim()} ×{(KindCount.TryGetValue(k.key, out var n) ? n : 0)}");
            sb.Append("\n\n<b>Environmental control nodes</b>");
            foreach (var k in Kinds.Where(k => k.cover <= 0)) sb.Append($"\n  <color=#{UIKit.Hex(k.col)}>■</color> {k.label.Split('(')[0].Trim()} ×{(KindCount.TryGetValue(k.key, out var n) ? n : 0)}");
            sb.Append($"\n\n<color=#FF8019>●</color> Config B pieces: {(GameState.configB ? "<b>DEPLOYED</b>" : "hidden (Config A)")}\n\nPrivate pods & quarters: door sensor only — no camera, no mic (privacy by design).\nZone labels show ⌀ = diameter of the zone.");
            return sb.ToString();
        }
    }
}
