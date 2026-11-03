using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Hunch3
{
    /// Guarantees every doorway / hatch / hallway in the habitat is walkable.
    /// 1. Slims the player's CharacterController to a realistic shoulder width and raises its step height
    ///    (hatch sills and module floor seams were catching it).
    /// 2. Sweeps the player's capsule along every waypoint link (the same links the AI crew walk).
    ///    Any collider in the way (door leaf, hatch ring, frame, bulkhead lip, hull mesh edge) is recorded.
    /// 3. Small door hardware is made non-solid outright; big structure (hull) is only made passable while the
    ///    player is inside that doorway's corridor, so walls everywhere else stay solid.
    /// 4. U = "unstuck": snaps you to the nearest walkable waypoint.
    /// Writes Assets/Unity3/door_report.txt with what it found and fixed.
    public class DoorPassage : MonoBehaviour
    {
        public static DoorPassage I;
        class Corridor { public Vector3 a, b; public Collider col; }
        readonly List<Corridor> corridors = new List<Corridor>();
        readonly HashSet<Collider> ignored = new HashSet<Collider>();
        readonly List<string> disabled = new List<string>();
        CharacterController cc; bool ccWasEnabled;
        public class CCHitProbe : MonoBehaviour
        {
            public string last = ""; public float t; public Collider col; public Vector3 point; public float above;
            void OnControllerColliderHit(ControllerColliderHit h)
            {
                if (h.normal.y > 0.7f) return;          // ignore ground contacts
                col = h.collider; point = h.point; above = h.point.y - h.controller.bounds.min.y;
                last = $"{h.collider.name} [{h.collider.GetType().Name}] n=({h.normal.x:0.00},{h.normal.y:0.00},{h.normal.z:0.00}) at +{h.point.y - h.controller.transform.position.y:0.00}m"; t = Time.time;
            }
        }
        CCHitProbe probe;
        const float Radius = 0.2f, Height = 1.7f, Step = 0.45f, CorridorHalfWidth = 1.3f;
        readonly List<(Vector3 a, Vector3 b)> links = new List<(Vector3, Vector3)>();
        static readonly string[] Hardware = { "door", "hatch", "frame", "threshold", "sill", "bar", "ring", "seal", "handle", "rail", "lip", "jamb", "trim", "gasket", "latch", "curtain", "bulkhead", "flange", "collar", "portal", "arch" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) Make(); };
            if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) Make();
        }
        static void Make() { if (!FindFirstObjectByType<DoorPassage>()) new GameObject("DoorPassage").AddComponent<DoorPassage>(); }

        void Awake() { I = this; }
        void Start() { Invoke(nameof(Setup), 0.6f); }

        bool IsPlayerOrCrew(Collider c) => c == (Collider)cc || c.GetComponentInParent<CrewAgent>() || c.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
        static bool IsFloor(Collider c)
        {
            string n = c.name;
            if (n.Contains("Hull") || n.Contains("Wall") || n.Contains("Shell") || n.Contains("Bulkhead")) return false;   // e.g. CORE_C01_LowerDeck_RigidHull is a wall, not a floor
            return n.StartsWith("Walkway_") || n.Contains("Floor") || n.StartsWith("T01_Terrain") || n.Contains("Porch") || n.Contains("Platform") || n.Contains("Deck") || n.Contains("Ground") || n.Contains("Regolith");
        }

        void Setup()
        {
            if (Nav.wp.Count == 0) Nav.Build();
            cc = PlayerRig.I ? PlayerRig.I.cc : FindFirstObjectByType<CharacterController>();
            if (cc)
            {
                cc.slopeLimit = 55f; cc.minMoveDistance = 0f; EnforceHeight(cc);
                probe = cc.GetComponent<CCHitProbe>() ?? cc.gameObject.AddComponent<CCHitProbe>();
            }
            var sb = new StringBuilder();
            sb.AppendLine($"DOOR / PASSAGE REPORT  ({System.DateTime.Now})");
            sb.AppendLine($"Player capsule (world): radius {Radius} m, height {Height} m, step {Step} m · rig scale {(cc ? cc.transform.lossyScale.ToString("F2") : "-")}");
            var done = new HashSet<string>(); int links = 0, blockedLinks = 0;
            var hardwareOff = new HashSet<Collider>();
            foreach (var kv in Nav.adj)
                foreach (var nb in kv.Value)
                {
                    string key = string.CompareOrdinal(kv.Key, nb) < 0 ? kv.Key + "|" + nb : nb + "|" + kv.Key; if (!done.Add(key)) continue;
                    Vector3 a = Nav.wp[kv.Key].position, b = Nav.wp[nb].position; if (Nav.IsLadder(a, b)) continue;
                    links++; this.links.Add((a, b));
                    var blockers = Blockers(a, b);
                    if (blockers.Count == 0) continue;
                    blockedLinks++;
                    var notes = new List<string>();
                    foreach (var c in blockers)
                    {
                        if (IsHardware(c))
                        {
                            if (hardwareOff.Add(c)) { c.enabled = false; disabled.Add(c.name); }
                            // a guard rail lying across a walking route (e.g. the ladder-well rail in front of the ladder) is
                            // also hidden — walking through a visible bar looks broken; the gap is where you step onto the ladder
                            bool rail = c.name.Contains("GuardRail") && !c.name.Contains("HOLO_");
                            if (rail) foreach (var r in c.GetComponents<Renderer>()) r.enabled = false;
                            notes.Add(c.name + (rail ? " → removed from the walking route" : " → non-solid"));
                        }
                        else { corridors.Add(new Corridor { a = a, b = b, col = c }); notes.Add(c.name + " → passable inside this doorway"); }
                    }
                    sb.AppendLine($"FIXED {key}: " + string.Join("; ", notes));
                }
            // verify: re-sweep with the fixes applied (corridor colliders count as open)
            int still = 0; done.Clear();
            foreach (var kv in Nav.adj)
                foreach (var nb in kv.Value)
                {
                    string key = string.CompareOrdinal(kv.Key, nb) < 0 ? kv.Key + "|" + nb : nb + "|" + kv.Key; if (!done.Add(key)) continue;
                    Vector3 a = Nav.wp[kv.Key].position, b = Nav.wp[nb].position; if (Nav.IsLadder(a, b)) continue;
                    var rem = Blockers(a, b).Where(c => !corridors.Any(k => k.col == c)).ToList();
                    if (rem.Count > 0) { still++; sb.AppendLine($"STILL BLOCKED {key}: {string.Join(", ", rem.Select(c => c.name))}"); }
                }
            int planks = BuildWalkways();
            sb.AppendLine($"walkway planks (seam bridges) built: {planks}");
            sb.AppendLine($"\nlinks tested {links} · blocked before fix {blockedLinks} · still blocked {still} · hardware colliders made non-solid {disabled.Count} · doorway corridors {corridors.Count}");
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "Unity3", "door_report.txt"), sb.ToString()); } catch { }
            Debug.Log($"[HUNCH3] DoorPassage: {links} links, {blockedLinks} were blocked, {still} still blocked, {disabled.Count} hardware colliders off, {corridors.Count} corridors.");
        }

        // Invisible smooth walkway along every nav link: samples the real floor every 0.4 m, bridges seams / lips / gaps
        // between hallway and module floors, so the character never snags at a junction (VR + desktop).
        int BuildWalkways()
        {
            var root = new GameObject("Walkways (invisible)").transform; int count = 0;
            var done = new HashSet<string>();
            foreach (var kv in Nav.adj)
                foreach (var nb in kv.Value)
                {
                    string key = string.CompareOrdinal(kv.Key, nb) < 0 ? kv.Key + "|" + nb : nb + "|" + kv.Key; if (!done.Add(key)) continue;
                    Vector3 a = Nav.wp[kv.Key].position, b = Nav.wp[nb].position; if (Nav.IsLadder(a, b)) continue;
                    float len = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z)); if (len < 0.3f) continue;
                    int n = Mathf.Max(2, Mathf.CeilToInt(len / 0.4f) + 1);
                    var pts = new Vector3[n]; var h = new float[n]; var ok = new bool[n];
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 p = Vector3.Lerp(a, b, i / (float)(n - 1)); float wy = p.y;
                        float best = float.NegativeInfinity;
                        foreach (var hit in Physics.RaycastAll(p + Vector3.up * 1.0f, Vector3.down, 2.2f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (IsPlayerOrCrew(hit.collider) || hit.collider.name.StartsWith("Walkway_")) continue;
                            if (hit.normal.y < 0.6f) continue;
                            float y = hit.point.y; if (y > wy + 0.25f || y < wy - 0.6f) continue;
                            if (y > best) best = y;
                        }
                        pts[i] = p; ok[i] = best > float.NegativeInfinity; h[i] = ok[i] ? best : wy;
                    }
                    // fill gaps from neighbours, then max-filter to bridge seams
                    for (int i = 0; i < n; i++) if (!ok[i]) { float l = h[Mathf.Max(0, i - 1)], r = h[Mathf.Min(n - 1, i + 1)]; h[i] = Mathf.Max(l, r, Mathf.Lerp(a.y, b.y, i / (float)(n - 1))); }
                    var hm = new float[n];
                    for (int i = 0; i < n; i++) hm[i] = Mathf.Max(h[Mathf.Max(0, i - 1)], h[i], h[Mathf.Min(n - 1, i + 1)]);
                    // emit planks, merging runs of equal height
                    int s0 = 0;
                    for (int i = 1; i < n; i++)
                    {
                        bool last = i == n - 1;
                        if (!last && Mathf.Abs(hm[i] - hm[s0]) < 0.012f && Mathf.Abs(hm[i + 1] - hm[s0]) < 0.012f) continue;
                        Vector3 p1 = new Vector3(pts[s0].x, hm[s0] + 0.012f, pts[s0].z), p2 = new Vector3(pts[i].x, hm[i] + 0.012f, pts[i].z);
                        Vector3 d = p2 - p1; float L = d.magnitude; if (L < 0.02f) { s0 = i; continue; }
                        var g = new GameObject("Walkway_" + key + "_" + count); g.transform.SetParent(root, false);
                        g.transform.position = (p1 + p2) * 0.5f - Vector3.up * 0.02f; g.transform.rotation = Quaternion.LookRotation(d / L, Vector3.up);
                        var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(1.0f, 0.04f, L + 0.12f);
                        count++; s0 = i;
                    }
                }
            return count;
        }

        static bool IsHardware(Collider c)
        {
            string n = c.name.ToLower();
            float size = c.bounds.size.magnitude;
            if (n.Contains("hull") || n.Contains("shell") || n.Contains("regolith") || n.Contains("shield")) return false;
            return size < 4.5f && Hardware.Any(h => n.Contains(h)) || size < 1.6f;
        }

        List<Collider> Blockers(Vector3 a, Vector3 b)
        {
            var res = new List<Collider>();
            Vector3 d = b - a; float len = d.magnitude; if (len < 0.05f) return res; d /= len;
            // start the capsule above the step height so floor seams / sills lower than a step never count
            Vector3 p1 = a + Vector3.up * (Step + Radius + 0.03f), p2 = a + Vector3.up * (2.35f - Radius);   // sweep taller than any player so low door tops are always found
            foreach (var h in Physics.CapsuleCastAll(p1, p2, Radius, d, len, ~0, QueryTriggerInteraction.Ignore))
            {
                var c = h.collider; if (!c || !c.enabled || IsPlayerOrCrew(c) || IsFloor(c)) continue;
                if (h.distance > 0 && h.normal.y > 0.75f) continue;     // walkable surface, not a wall
                if (!res.Contains(c)) res.Add(c);
            }
            foreach (var c in Physics.OverlapCapsule(p1, p2, Radius * 0.9f, ~0, QueryTriggerInteraction.Ignore))
                if (c && c.enabled && !IsPlayerOrCrew(c) && !IsFloor(c) && !res.Contains(c)) res.Add(c);
            // ray grid (both directions, back faces included): catches mesh faces the capsule sweep misses
            bool bf = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;
            Vector3 side = Vector3.Cross(Vector3.up, d).normalized;
            for (float hy = Step + 0.05f; hy <= 2.3f; hy += 0.15f)
                for (int o = -1; o <= 1; o++)
                {
                    Vector3 s0 = a + Vector3.up * hy + side * (o * Radius), s1 = b + Vector3.up * hy + side * (o * Radius);
                    foreach (var h in Physics.RaycastAll(s0, d, len, ~0, QueryTriggerInteraction.Ignore))
                        if (h.collider && h.collider.enabled && !IsPlayerOrCrew(h.collider) && !IsFloor(h.collider) && !res.Contains(h.collider)) res.Add(h.collider);
                    foreach (var h in Physics.RaycastAll(s1, -d, len, ~0, QueryTriggerInteraction.Ignore))
                        if (h.collider && h.collider.enabled && !IsPlayerOrCrew(h.collider) && !IsFloor(h.collider) && !res.Contains(h.collider)) res.Add(h.collider);
                }
            Physics.queriesHitBackfaces = bf;
            return res;
        }

        static float SegDistXZ(Vector3 p, Vector3 a, Vector3 b, out float yOnSeg)
        {
            Vector2 P = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z);
            Vector2 ab = B - A; float t = ab.sqrMagnitude < 1e-4f ? 0 : Mathf.Clamp01(Vector2.Dot(P - A, ab) / ab.sqrMagnitude);
            yOnSeg = Mathf.Lerp(a.y, b.y, t);
            return (P - (A + ab * t)).magnitude;
        }

        void Update()
        {
            if (!cc) return;
            if (cc.enabled != ccWasEnabled) { ccWasEnabled = cc.enabled; ignored.Clear(); }   // Unity resets IgnoreCollision when a collider is re-enabled (teleport)
            if (!cc.enabled) return;
            Vector3 f = cc.transform.position;
            // which big colliders should be open right now?
            var want = new HashSet<Collider>();
            foreach (var k in corridors)
            {
                if (!k.col) continue;
                float dist = SegDistXZ(f, k.a, k.b, out float y);
                if (dist < CorridorHalfWidth && Mathf.Abs(f.y - y) < 1.4f) want.Add(k.col);
            }
            foreach (var c in want) if (!ignored.Contains(c)) { Physics.IgnoreCollision(cc, c, true); ignored.Add(c); }
            // only make a wall solid again once you are clear of it (re-enabling it while you overlap it shoves you sideways = the "glitch")
            foreach (var c in ignored.ToList()) if (!want.Contains(c)) { if (c && Overlaps(c)) continue; if (c) Physics.IgnoreCollision(cc, c, false); ignored.Remove(c); }
            Learn(f);

            Assist(f);
            Contain(f);
            if (Keyboard.current != null && Keyboard.current[Key.U].wasPressedThisFrame) Unstuck();
            if (Keyboard.current != null && Keyboard.current[Key.F9].wasPressedThisFrame && !walking) StartCoroutine(WalkTest());
        }

        // ---- learn doorways from real contact: if you bump into structure ABOVE step height that sits right on a walking
        //      route (a hatch lintel, hull lip, tunnel end ring), that piece becomes passable along that route only.
        //      Walls beside the route (> 0.6 m from its centre line) are never learned, so rooms stay closed.
        void Learn(Vector3 f)
        {
            if (!probe || !probe.col || Time.time - probe.t > 0.1f || LadderClimb.Climbing) return;
            var c = probe.col; probe.col = null;
            if (!c.enabled || IsPlayerOrCrew(c) || IsFloor(c) || c.isTrigger || probe.above < Step) return;
            if (corridors.Any(k => k.col == c && SegDistXZ(f, k.a, k.b, out _) < CorridorHalfWidth)) return;
            (Vector3 a, Vector3 b) best = default; float bd = 0.6f; bool found = false;
            foreach (var l in links)
            {
                float dp = SegDistXZ(probe.point, l.a, l.b, out float y);          // the obstacle is ON the route…
                if (dp > bd || Mathf.Abs(f.y - y) > 1.4f) continue;
                if (SegDistXZ(f, l.a, l.b, out _) > CorridorHalfWidth) continue;   // …and you are walking along it
                bd = dp; best = l; found = true;
            }
            if (!found) return;
            corridors.Add(new Corridor { a = best.a, b = best.b, col = c });
            Physics.IgnoreCollision(cc, c, true); ignored.Add(c);
            Debug.Log($"[HUNCH3] doorway learned: {c.name} made passable along {V(best.a)}→{V(best.b)} (hit {probe.above:0.00} m up)");
        }

        bool Overlaps(Collider c)
        {
            if (!c || !c.enabled) return false;
            if (c is MeshCollider mc && !mc.convex)
            {   // non-convex mesh: check the capsule against the mesh with an overlap query
                var b = cc.bounds; float r = Radius * 0.95f;
                Vector3 p1 = new Vector3(b.center.x, b.min.y + r + 0.02f, b.center.z), p2 = new Vector3(b.center.x, b.max.y - r, b.center.z);
                foreach (var o in Physics.OverlapCapsule(p1, p2, r, ~0, QueryTriggerInteraction.Ignore)) if (o == c) return true;
                return false;
            }
            return Physics.ComputePenetration(cc, cc.transform.position, cc.transform.rotation, c, c.transform.position, c.transform.rotation, out _, out _);
        }

        // ---- doorway assist: if you're pushing to move but haven't moved (caught on a door jamb / hull edge / floor seam),
        //      glide you onto the centre line of the nearest doorway/hallway corridor, where the passage is guaranteed open.
        Vector3 assistFrom; float assistClock, assistCool;
        void Assist(Vector3 f)
        {
            bool intent = false;
            var kb = Keyboard.current;
            if (kb != null && (kb.wKey.isPressed || kb.sKey.isPressed || kb.aKey.isPressed || kb.dKey.isPressed || kb.upArrowKey.isPressed || kb.downArrowKey.isPressed)) intent = true;
            var lh = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
            if (lh.isValid && lh.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 st) && st.magnitude > 0.4f) intent = true;
            if (!intent || LadderClimb.Climbing || SleepSystem.PlayerAsleep) { assistClock = 0; assistFrom = f; return; }
            assistClock += Time.deltaTime;
            if (assistClock < 0.6f) return;
            float moved = new Vector2(f.x - assistFrom.x, f.z - assistFrom.z).magnitude;
            assistClock = 0; assistFrom = f;
            if (moved > 0.12f || Time.time < assistCool) return;
            // stuck: nearest corridor within 1.8 m on this deck
            Corridor best = null; float bd = 1.8f, by = 0;
            foreach (var k in corridors) { float d = SegDistXZ(f, k.a, k.b, out float y); if (d < bd && Mathf.Abs(f.y - y) < 1.4f) { bd = d; best = k; by = y; } }
            if (best == null) return;
            Vector3 A = new Vector3(best.a.x, 0, best.a.z), B = new Vector3(best.b.x, 0, best.b.z), P = new Vector3(f.x, 0, f.z), ab = B - A;
            float t = ab.sqrMagnitude < 1e-4f ? 0 : Mathf.Clamp01(Vector3.Dot(P - A, ab) / ab.sqrMagnitude);
            Vector3 onLine = A + ab * t;
            // step onto the line and a little along it in the direction you were facing
            Vector3 face = PlayerRig.I ? PlayerRig.I.cam.transform.forward : Vector3.forward; face.y = 0;
            Vector3 along = ab.normalized * Mathf.Sign(Vector3.Dot(ab, face) == 0 ? 1 : Vector3.Dot(ab, face)) * 0.35f;
            Vector3 dest = new Vector3(onLine.x + along.x, by + 0.02f, onLine.z + along.z);
            cc.enabled = false; cc.transform.position = dest; cc.enabled = true; ignored.Clear(); ccWasEnabled = true;
            assistCool = Time.time + 0.6f;
        }

        // ---- pressure-hull containment: without a suit you can never end up outside the habitat (jumping through a
        //      hallway roof, squeezing past a hull edge, etc.). Inside = a habitat floor below you AND a roof above you.
        Vector3 lastInside; bool haveInside; float outsideT, warnAt;
        void Contain(Vector3 f)
        {
            var pr = PlayerRig.I; if (!pr || pr.suit != SuitState.IVA || walking || LadderClimb.Climbing || SleepSystem.PlayerAsleep) { outsideT = 0; return; }
            Vector3 o = f + Vector3.up * 1.0f;
            // a fat probe (not a thin ray): standing at the edge of the ladder well or a floor seam must still count as "inside"
            bool floor = Physics.SphereCast(o, 0.3f, Vector3.down, out var hd, 4.5f, ~0, QueryTriggerInteraction.Ignore) && !hd.collider.name.StartsWith("T0") && !hd.collider.name.Contains("Terrain");
            if (!floor && LadderClimb.Climbing) floor = true;
            bool roof = Physics.Raycast(o, Vector3.up, out var hu, 14f, ~0, QueryTriggerInteraction.Ignore);
            if (floor && roof) { outsideT = 0; if (hd.distance < 1.3f) { lastInside = f; haveInside = true; } return; }
            outsideT += Time.deltaTime;
            if (outsideT > 0.12f && haveInside)
            {
                outsideT = 0;
                Debug.Log($"[HUNCH3] containment: pulled back from {f} (floor={(floor ? hd.collider.name : "none")}, roof={roof}) to {lastInside}");
                cc.enabled = false; cc.transform.position = lastInside + Vector3.up * 0.02f; cc.enabled = true; ignored.Clear(); ccWasEnabled = true;
                if (Time.time > warnAt) { warnAt = Time.time + 5f; HUD.I?.Toast("⛔ That's vacuum out there! You can only go outside in a spacesuit — use the suitport in the Airlock module."); }
            }
        }

        // ---- F9: real walk test — drives the player's own CharacterController along every link with physics ----
        bool walking; public static bool Walking => I && I.walking;
        System.Collections.IEnumerator WalkTest()
        {
            walking = true; var pr = PlayerRig.I; if (pr) pr.autopilot = false;
            var sb = new StringBuilder(); sb.AppendLine($"WALK TEST ({System.DateTime.Now}) — capsule local r {cc.radius:0.00} h {cc.height:0.00} · rig scale {cc.transform.lossyScale} → world h {cc.height * cc.transform.lossyScale.y:0.00} m");
            var done = new HashSet<string>(); int ok = 0, fail = 0, n = 0;
            var pairs = new List<(string, string)>();
            foreach (var kv in Nav.adj) foreach (var nb in kv.Value)
                { string key = string.CompareOrdinal(kv.Key, nb) < 0 ? kv.Key + "|" + nb : nb + "|" + kv.Key; if (done.Add(key) && !Nav.IsLadder(Nav.wp[kv.Key].position, Nav.wp[nb].position)) pairs.Add((kv.Key, nb)); }
            foreach (var (a, b) in pairs)
            {
                n++; HUD.I?.Toast($"Walk test {n}/{pairs.Count}: {a.Replace("WP_", "")} → {b.Replace("WP_", "")}");
                bool fwd = false, back = false;
                yield return Walk(a, b, r => fwd = r);
                if (fwd) yield return Walk(b, a, r => back = r); 
                if (fwd && back) ok++; else { fail++; sb.AppendLine($"FAIL {a} → {b} ({(fwd ? "return trip" : "forward")}) stopped near {V(cc.transform.position)} · blocker: {lastBlocker}"); }
            }
            sb.AppendLine($"\nlinks walked {pairs.Count} · both directions OK {ok} · failed {fail}");
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "Unity3", "walk_report.txt"), sb.ToString()); } catch { }
            Debug.Log($"[HUNCH3] WalkTest: {ok}/{pairs.Count} links walkable both ways, {fail} failed.");
            HUD.I?.Toast($"Walk test done: {ok}/{pairs.Count} hallway links walkable both ways ({fail} failed).");
            walking = false;
        }
        static string V(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
        System.Collections.IEnumerator Walk(string from, string to, System.Action<bool> result)
        {
            var pr = PlayerRig.I; Vector3 a = Nav.wp[from].position, b = Nav.wp[to].position;
            pr.Teleport(a); yield return null;
            float len = Vector3.Distance(a, b), t = 0, limit = len / 4f + 3f, vy = 0;
            Vector3 lastP = cc.transform.position; float stuck = 0;
            while (t < limit)
            {
                Vector3 p = cc.transform.position, d = b - p; d.y = 0;
                if (d.magnitude < 0.35f) { result(true); yield break; }
                vy = cc.isGrounded ? -0.5f : vy + Physics.gravity.y * Time.deltaTime;
                EnforceHeight(cc);
                cc.Move((d.normalized * 5f + Vector3.up * vy) * Time.deltaTime);
                if ((cc.transform.position - lastP).sqrMagnitude < 1e-6f) { stuck += Time.deltaTime; if (stuck > 1.2f) break; } else stuck = 0;
                lastP = cc.transform.position; t += Time.deltaTime; yield return null;
            }
            // who stopped us? cast the capsule forward from where we got stuck
            Vector3 fp = cc.transform.position, dir = b - fp; dir.y = 0; dir.Normalize();
            Vector3 c1 = fp + Vector3.up * (cc.radius + 0.05f), c2 = fp + Vector3.up * (cc.height - cc.radius);
            var hits = Physics.CapsuleCastAll(c1, c2, cc.radius * 0.95f, dir, 0.6f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider && h.collider != (Collider)cc && !h.collider.GetComponentInParent<CrewAgent>()).OrderBy(h => h.distance)
                .Select(h => $"{h.collider.name} [{h.collider.GetType().Name}{(ignored.Contains(h.collider) ? ",ignored" : "")}] n={h.normal.y:0.00} @{h.point.y - fp.y:0.00}m").Take(3).ToList();
            var ov = Physics.OverlapCapsule(c1, c2, cc.radius + 0.06f, ~0, QueryTriggerInteraction.Ignore).Where(c => c != (Collider)cc && !c.GetComponentInParent<CrewAgent>()).Select(c => c.name + (ignored.Contains(c) ? "(ignored)" : "")).Take(4).ToList();
            string touched = $" · cc local h {cc.height:0.00} · rig scale {lastScale.y:0.00} · world h {cc.height * lastScale.y:0.00}" + (probe && Time.time - probe.t < 0.5f ? " · CC touching: " + probe.last : " · CC touching: nothing");
            lastBlocker = touched + " · cast: " + (hits.Count > 0 ? string.Join(" | ", hits) : "no hit ahead") + (ov.Count > 0 ? " · overlapping: " + string.Join(", ", ov) : "");
            result(false);
        }
        string lastBlocker = "";

        // The XR rig's body/height driver keeps re-growing the capsule (> 2 m) every frame, which catches on the 2.1 m door
        // lintels. Clamp it after everything else ran: desktop = fixed 1.7 m; VR = follows the headset but never above 1.85 m.
        public static float maxSeenHeight; public static Vector3 lastScale = Vector3.one;
        /// Call right before any cc.Move: something in the XR rig re-grows the capsule during Update.
        public static void EnforceHeight(CharacterController c)
        {
            if (!c) return;
            // the capsule scales with the rig's transform (the XR rig prefab is scaled ~1.2×) → work in WORLD metres
            var ls = c.transform.lossyScale; float sy = Mathf.Max(0.01f, Mathf.Abs(ls.y)), sxz = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z)));
            float worldH = c.height * Mathf.Max(sy, sxz);
            maxSeenHeight = Mathf.Max(maxSeenHeight, worldH); lastScale = ls;
            float want = PlayerRig.I && PlayerRig.I.isXR ? Mathf.Clamp(worldH, 1.0f, 1.85f) : Height;   // world metres
            float k = Mathf.Max(sy, sxz);
            float hLocal = want / k, cyLocal = (want * 0.5f) / k + c.skinWidth, rLocal = Radius / sxz;
            if (Mathf.Abs(c.height - hLocal) > 0.001f) c.height = hLocal;
            if (Mathf.Abs(c.center.y - cyLocal) > 0.001f || Mathf.Abs(c.center.x) > 0.001f || Mathf.Abs(c.center.z) > 0.001f) c.center = new Vector3(0, cyLocal, 0);
            if (Mathf.Abs(c.radius - rLocal) > 0.001f) c.radius = rLocal;
            float stepLocal = Step / k; if (Mathf.Abs(c.stepOffset - stepLocal) > 0.001f) c.stepOffset = Mathf.Min(stepLocal, hLocal * 0.9f);
        }
        void LateUpdate() { if (cc) EnforceHeight(cc); }

        public void Unstuck()
        {
            if (!PlayerRig.I) return;
            string n = Nav.Nearest(PlayerRig.I.FeetPosition); if (n == null) return;
            PlayerRig.I.Teleport(Nav.wp[n].position);
            HUD.I?.Toast("Unstuck: moved you to the nearest walkway (" + n.Replace("WP_", "") + ").");
        }
    }
}
