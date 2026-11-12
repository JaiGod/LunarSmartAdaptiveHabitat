// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/04_Habitat/LEDs.cs
//  Section : 04_Habitat
//  Purpose : FUNCTIONAL LED SYSTEM (ISS + aircraft cabin, not cyberpunk). Every strip has a job:
//    1. Pathway LEDs  — thin strips at the base of every walkway wall. Blue = navigation; during an alarm
//                       they pulse red (danger) and the route chevrons to the muster point turn green (safe route).
//    2. Status LEDs   — one small indicator on every smart system: green healthy · yellow maintenance due ·
//                       red fault · blue sleep mode (beds) · white ready (medical scanner).
//    3. Ceiling channels — continuous recessed light channels along each corridor/room axis (reads the space,
//                       softens shadows). Colour follows the circadian schedule.
//    4. Circadian     — morning warm white → bright white work hours → soft warm evening → dim blue sleep;
//                       red pulse in an emergency.
//    + door outlines (the strip follows the real hatch shape), ladder rail strips (amber = caution), and
//      emergency red backup glow on every door frame during an alarm.
//  Low power: ~0.5 W per metre of LED strip; strips are built once as a few combined meshes.
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hunch3
{
    public class HabitatLEDs : MonoBehaviour
    {
        public static HabitatLEDs I;
        Material path, ceil, door, ladder; float meters; int strips;
        readonly Dictionary<SmartObject, Renderer> status = new Dictionary<SmartObject, Renderer>();
        readonly Dictionary<string, Material> statusMats = new Dictionary<string, Material>();
        readonly HashSet<long> used = new HashSet<long>();

        void Awake() { I = this; }
        void Start() { Invoke(nameof(BuildAll), 3f); }     // after the habitat, nav graph and smart systems exist

        void BuildAll()
        {
            bool bf = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;
            path = HUD.Glow(new Color(.25f, .55f, 1f)); ceil = HUD.Glow(new Color(1f, .97f, .92f)); door = HUD.Glow(new Color(.75f, .9f, 1f)); ladder = HUD.Glow(new Color(1f, .7f, .2f));
            var pv = new MeshB(); var cv = new MeshB(); var dv = new MeshB(); var lv = new MeshB();
            try { Pathways(pv, cv); } catch (System.Exception e) { Debug.LogWarning("[LED] path " + e.Message); }
            try { Doors(dv); } catch (System.Exception e) { Debug.LogWarning("[LED] doors " + e.Message); }
            try { Ladders(lv); } catch (System.Exception e) { Debug.LogWarning("[LED] ladder " + e.Message); }
            Physics.queriesHitBackfaces = bf;
            foreach (var m in new[] { path, ceil, door, ladder }) if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0);   // thin strips: draw both sides
            pv.Make("LED_Pathway", path, null); cv.Make("LED_CeilingChannels", ceil, null); dv.Make("LED_DoorOutlines", door, null); lv.Make("LED_LadderRails", ladder, null);
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(GameState.ReportsDir, "leds.txt"), $"LED strips {strips} · {meters:0} m (~{meters * 0.5f:0} W) · status LEDs {status.Count}\n"); } catch { }
        }

        // ---------------------------------------------------------------- 1 + 3: pathway strips and ceiling channels
        void Pathways(MeshB pv, MeshB cv)
        {
            var done = new HashSet<string>();
            foreach (var kv in Nav.adj) foreach (var nb in kv.Value)
            {
                string id = string.CompareOrdinal(kv.Key, nb) < 0 ? kv.Key + "|" + nb : nb + "|" + kv.Key; if (!done.Add(id)) continue;
                if (!Nav.wp.TryGetValue(kv.Key, out var ta) || !Nav.wp.TryGetValue(nb, out var tb) || !ta || !tb) continue;
                Vector3 a = ta.position, b = tb.position; if (Nav.IsLadder(a, b) || Mathf.Abs(a.y - b.y) > .4f) continue;
                Vector3 d = b - a; d.y = 0; float len = d.magnitude; if (len < .3f) continue; d /= len; var side = Vector3.Cross(Vector3.up, d);
                Vector3 prevC = Vector3.zero; bool hasPrev = false;
                for (float t = .25f; t < len; t += .5f)
                {
                    var p = a + d * t; if (!Physics.Raycast(p + Vector3.up * .6f, Vector3.down, out var fh, 1.4f, ~0, QueryTriggerInteraction.Ignore)) { hasPrev = false; continue; }
                    float fy = fh.point.y;
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        if (!Physics.Raycast(new Vector3(p.x, fy + .25f, p.z), side * sg, out var wh, 2.6f, ~0, QueryTriggerInteraction.Ignore) || wh.distance < .35f) continue;
                        var n = wh.normal; if (Vector3.Dot(n, side * sg) > 0) n = -n; n.y = 0; if (n.sqrMagnitude < .2f) continue; n.Normalize();
                        var q = new Vector3(wh.point.x, fy + .05f, wh.point.z) + n * .018f;
                        if (!Claim(q)) continue;
                        var along = Vector3.Cross(n, Vector3.up);
                        pv.Box(q, Quaternion.LookRotation(along, Vector3.up), new Vector3(.02f, .014f, .42f)); strips++; meters += .42f;
                    }
                    // ceiling channel over the walkway axis
                    if (Physics.Raycast(new Vector3(p.x, fy + 1.2f, p.z), Vector3.up, out var ch, 2.8f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var c = ch.point - Vector3.up * .012f;
                        if (hasPrev && Mathf.Abs(c.y - prevC.y) < .2f && (c - prevC).sqrMagnitude < .36f)
                        { var mid = (c + prevC) * .5f; var dir = c - prevC; cv.Box(mid, Quaternion.LookRotation(dir.normalized, Vector3.up), new Vector3(.07f, .012f, dir.magnitude + .02f)); meters += dir.magnitude; }
                        prevC = c; hasPrev = true;
                    }
                    else hasPrev = false;
                }
            }
        }
        bool Claim(Vector3 p) { long k = ((long)Mathf.RoundToInt(p.x / .3f) * 73856093) ^ ((long)Mathf.RoundToInt(p.y / .3f) * 19349663) ^ ((long)Mathf.RoundToInt(p.z / .3f) * 83492791); return used.Add(k); }

        // ---------------------------------------------------------------- door outlines (follow the real opening)
        void Doors(MeshB dv)
        {
            foreach (var kv in Nav.wp.Where(w => w.Key.Contains("_Door") && w.Value))
            {
                var c = kv.Value.position; Vector3 dir = Vector3.forward;
                if (Nav.adj.TryGetValue(kv.Key, out var ln) && ln.Count > 0 && Nav.wp.TryGetValue(ln[0], out var o) && o) { dir = o.position - c; dir.y = 0; if (dir.sqrMagnitude < .01f) dir = Vector3.forward; dir.Normalize(); }
                if (!Physics.Raycast(c + Vector3.up * .6f, Vector3.down, out var fh, 1.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var origin = new Vector3(c.x, fh.point.y + 1.0f, c.z); var side = Vector3.Cross(Vector3.up, dir);
                var pts = new List<Vector3>();
                for (int i = 0; i <= 18; i++)
                {
                    float ang = Mathf.Lerp(-35f, 215f, i / 18f) * Mathf.Deg2Rad; var r = side * Mathf.Cos(ang) + Vector3.up * Mathf.Sin(ang);
                    if (Physics.Raycast(origin, r, out var h, 1.6f, ~0, QueryTriggerInteraction.Ignore) && h.distance > .25f) pts.Add(h.point - r * .02f); else pts.Add(Vector3.positiveInfinity);
                }
                for (int i = 1; i < pts.Count; i++)
                {
                    var p0 = pts[i - 1]; var p1 = pts[i]; if (float.IsInfinity(p0.x) || float.IsInfinity(p1.x)) continue;
                    var dd = p1 - p0; if (dd.magnitude > .7f || dd.magnitude < .01f) continue;
                    dv.Box((p0 + p1) * .5f, Quaternion.LookRotation(dd.normalized, dir), new Vector3(.02f, .02f, dd.magnitude + .01f)); meters += dd.magnitude; strips++;
                }
            }
        }

        // ---------------------------------------------------------------- ladder rails (amber = caution, climb here)
        void Ladders(MeshB lv)
        {
            foreach (var t in Util.FindPrefix("W22_Ladder_Rail"))
            {
                var r = t.GetComponent<Renderer>(); if (!r) continue; var b = r.bounds;
                var lad = LadderClimb.I; Vector3 face = lad ? lad.Stand - new Vector3(b.center.x, 0, b.center.z) : Vector3.forward; face.y = 0; if (face.sqrMagnitude < .001f) face = Vector3.forward; face.Normalize();
                lv.Box(new Vector3(b.center.x, b.center.y, b.center.z) + face * (Mathf.Min(b.extents.x, b.extents.z) + .012f), Quaternion.LookRotation(face, Vector3.up), new Vector3(.018f, b.size.y * .96f, .01f));
                meters += b.size.y; strips++;
            }
        }

        // ---------------------------------------------------------------- 2: status LEDs on every smart system
        void EnsureStatus()
        {
            foreach (var so in SmartObject.All)
            {
                if (!so || status.ContainsKey(so)) continue;
                var rs = so.GetComponentsInChildren<Renderer>().Where(x => !(x is LineRenderer) && !x.GetComponent<TextMesh>()).ToArray(); if (rs.Length == 0) { status[so] = null; continue; }
                var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds);
                var f = so.facing; f.y = 0; if (f.sqrMagnitude < .01f) f = Vector3.forward; f.Normalize();
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(g.GetComponent<Collider>()); g.name = "StatusLED"; g.transform.SetParent(so.transform, true);
                float depth = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(f.x), 0, Mathf.Abs(f.z))));
                g.transform.position = new Vector3(b.center.x, b.max.y - .06f, b.center.z) + f * (depth + .005f); g.transform.rotation = Quaternion.LookRotation(f); g.transform.localScale = new Vector3(.05f, .018f, .006f);
                var rr = g.GetComponent<Renderer>(); rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; status[so] = rr;
            }
        }
        Material SM(string k, Color c) { if (!statusMats.TryGetValue(k, out var m) || !m) statusMats[k] = m = HUD.Glow(c); return m; }

        // ---------------------------------------------------------------- 4: circadian + emergency, every frame
        public static Color Circadian(float h, out float level)
        {
            if (h >= 6f && h < 9f) { level = .85f; return Color.Lerp(new Color(1f, .62f, .38f), new Color(1f, .9f, .8f), (h - 6f) / 3f); }    // morning: warm white, slight orange
            if (h >= 9f && h < 18f) { level = 1f; return new Color(1f, .98f, .95f); }                                                       // work hours: bright white
            if (h >= 18f && h < 22f) { level = .6f; return new Color(1f, .72f, .48f); }                                                     // evening: soft warm
            level = .3f; return new Color(.35f, .45f, .9f);                                                                                 // sleep: dim blue
        }
        void Set(Material m, Color c) { if (!m) return; m.color = c; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); }
        float nextStatus;
        void Update()
        {
            if (!path) return;
            bool alarm = Hazards.AlarmLight(out var aCol, out var aMul);
            float pulse = .45f + .55f * Mathf.Abs(Mathf.Sin(Time.time * 3.2f));
            var cc = Circadian(SimClock.HourOfDay, out float lvl);
            Set(path, alarm ? new Color(1f, .12f, .1f) * pulse : new Color(.25f, .55f, 1f) * (lvl < .5f ? .55f : .8f));
            Set(ceil, alarm ? new Color(1f, .15f, .12f) * pulse : cc * lvl);
            Set(door, alarm ? new Color(1f, .1f, .08f) * pulse : new Color(.75f, .9f, 1f) * (lvl < .5f ? .45f : .7f));
            Set(ladder, alarm ? new Color(1f, .15f, .1f) * pulse : new Color(1f, .7f, .2f) * .8f);
            if (Time.time < nextStatus) return; nextStatus = Time.time + .5f;
            EnsureStatus();
            foreach (var kv in status)
            {
                var so = kv.Key; var r = kv.Value; if (!so || !r) continue;
                string k = so.key ?? "";
                bool sleeping = (k == "smart_bed" || k == "recovery_pod" || k.StartsWith("bed")) && SleepSystem.PlayerAsleep;
                if (so.Failed) r.sharedMaterial = (Time.time % 1f) < .5f ? SM("red", new Color(1.4f, .1f, .08f)) : SM("off", new Color(.15f, .02f, .02f));
                else if (so.integrity < .6f) r.sharedMaterial = SM("yellow", new Color(1.3f, .95f, .15f));
                else if (sleeping) r.sharedMaterial = SM("blue", new Color(.25f, .45f, 1.3f));
                else if (k == "medical_scanner") r.sharedMaterial = SM("white", new Color(1.3f, 1.3f, 1.3f));
                else r.sharedMaterial = SM("green", new Color(.2f, 1.2f, .4f));
            }
        }

        /// tiny mesh builder: many thin boxes → one mesh, one draw call per LED group
        class MeshB
        {
            readonly List<Vector3> v = new List<Vector3>(); readonly List<int> t = new List<int>();
            public void Box(Vector3 c, Quaternion r, Vector3 s)
            {
                int o = v.Count; var h = s * .5f;
                for (int i = 0; i < 8; i++) v.Add(c + r * new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z));
                int[] f = { 0, 2, 3, 0, 3, 1, 4, 5, 7, 4, 7, 6, 0, 1, 5, 0, 5, 4, 2, 6, 7, 2, 7, 3, 0, 4, 6, 0, 6, 2, 1, 3, 7, 1, 7, 5 };
                foreach (var i in f) t.Add(o + i);
            }
            public void Make(string name, Material m, Transform parent)
            {
                if (v.Count == 0) return;
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var g = new GameObject(name);   // world-space vertices: no parent offset g.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
