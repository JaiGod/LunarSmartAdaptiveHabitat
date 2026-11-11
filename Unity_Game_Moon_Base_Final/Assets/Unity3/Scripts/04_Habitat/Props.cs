// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/04_Habitat/Props.cs
//  Section : 04_Habitat
//  Folder  : The base itself: zones and their light/sound/temperature/air, dome status lights,
//            doors and hatches, ladders, loose props in 1/6 g, Moon environment, layout export.
//  Purpose : LOOSE PROPS WITH REAL LUNAR PHYSICS Cargo transfer bags (NASA CTB sizes), food
//            containers, sample bags, tools (NASA public-domain models: hammer, wrench,
//  Why     : Chat: 1/6 gravity — loose props fall and bounce like on the Moon.
//  Contains: LunarProps, PhysProp, PlayerPusher, ThirdPersonView
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Hunch3
{
    /// LOOSE PROPS WITH REAL LUNAR PHYSICS
    /// Cargo transfer bags (NASA CTB sizes), food containers, sample bags, tools (NASA public-domain models: hammer, wrench,
    /// pistol-grip tool, glove, crew lock bag, IR camera) and big cargo crates outside by the landing pad.
    ///  • every prop has its real mass and falls at 1.62 m/s² (1/6 g) — bump or throw one and it drifts up in a slow, high
    ///    arc and settles softly: that "floaty" look is what real lunar gravity does (nothing truly floats on the Moon)
    ///  • walk into a prop to push it · E picks it up / puts it down · left click throws it · VR: just grab it with your hand
    ///  • moonquakes make loose items hop
    public class LunarProps : MonoBehaviour
    {
        public static LunarProps I;
        public static readonly List<PhysProp> All = new List<PhysProp>();
        PhysProp held; float heldDist = 1.1f; Material fabric, fabricDark, white, blueLid, alu, grey, label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) Make(); };
            if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) Make();
        }
        static void Make() { if (!FindFirstObjectByType<LunarProps>()) new GameObject("LunarProps").AddComponent<LunarProps>(); }
        void Awake() { I = this; }
        void Start() { Invoke(nameof(Spawn), 2.5f); }

        static Material Mat(Color c, float smooth = 0.15f, float metal = 0f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m.SetColor("_BaseColor", c); m.color = c; m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal); return m;
        }

        // ------------------------------------------------------------------ building props
        PhysProp Box(string name, Vector3 size, Material mat, float massKg, bool straps = false, Material lid = null)
        {
            var g = new GameObject(name);
            var body = new GameObject("Body").transform; body.SetParent(g.transform, false);
            var core = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(core.GetComponent<Collider>()); core.transform.SetParent(body, false); core.transform.localScale = size; core.GetComponent<Renderer>().sharedMaterial = mat;
            if (straps)
            {   // CTB look: two dark webbing straps + a handle + a blue label patch
                foreach (float x in new[] { -0.28f, 0.28f })
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(s.GetComponent<Collider>()); s.transform.SetParent(body, false);
                    s.transform.localPosition = new Vector3(x * size.x, 0, 0); s.transform.localScale = new Vector3(0.035f, size.y + 0.006f, size.z + 0.006f); s.GetComponent<Renderer>().sharedMaterial = fabricDark;
                }
                var h = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(h.GetComponent<Collider>()); h.transform.SetParent(body, false);
                h.transform.localPosition = new Vector3(0, size.y * 0.5f + 0.012f, 0); h.transform.localScale = new Vector3(size.x * 0.35f, 0.02f, 0.04f); h.GetComponent<Renderer>().sharedMaterial = fabricDark;
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(p.GetComponent<Collider>()); p.transform.SetParent(body, false);
                p.transform.localPosition = new Vector3(0, 0, size.z * 0.5f + 0.004f); p.transform.localScale = new Vector3(size.x * 0.3f, size.y * 0.35f, 0.006f); p.GetComponent<Renderer>().sharedMaterial = label;
            }
            if (lid)
            {
                var l = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(l.GetComponent<Collider>()); l.transform.SetParent(body, false);
                l.transform.localPosition = new Vector3(0, size.y * 0.5f, 0); l.transform.localScale = new Vector3(size.x + 0.01f, 0.03f, size.z + 0.01f); l.GetComponent<Renderer>().sharedMaterial = lid;
            }
            var bc = g.AddComponent<BoxCollider>(); bc.size = size;
            return Finish(g, massKg, name);
        }

        PhysProp Model(string res, string name, float massKg)
        {
            var pf = Resources.Load<GameObject>("NASA/" + res); if (!pf) return null;
            var g = new GameObject(name);
            var m = Instantiate(pf, g.transform); m.transform.localPosition = Vector3.zero;
            foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
            var rs = m.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) { Destroy(g); return null; }
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var bc = g.AddComponent<BoxCollider>(); bc.center = b.center - g.transform.position; bc.size = Vector3.Max(b.size, new Vector3(0.03f, 0.03f, 0.03f));
            return Finish(g, massKg, name);
        }

        PhysProp Finish(GameObject g, float massKg, string name)
        {
            g.layer = 0;
            var rb = g.AddComponent<Rigidbody>(); rb.mass = massKg; rb.linearDamping = 0.05f; rb.angularDamping = 0.15f;   // thin cabin air; 0 drag outside (vacuum)
            rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = massKg < 2f ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
            var col = g.GetComponent<Collider>(); col.sharedMaterial = new PhysicsMaterial("prop") { dynamicFriction = 0.55f, staticFriction = 0.65f, bounciness = 0.12f, bounceCombine = PhysicsMaterialCombine.Average };
            var p = g.AddComponent<PhysProp>(); p.label = name; p.rb = rb;
            if (PlayerRig.I && PlayerRig.I.isXR) { var gi = g.AddComponent<XRGrabInteractable>(); gi.throwOnDetach = true; gi.useDynamicAttach = true; }
            All.Add(p); return p;
        }

        // ------------------------------------------------------------------ where they go
        void Spawn()
        {
            fabric = Mat(new Color(0.9f, 0.88f, 0.8f), 0.08f); fabricDark = Mat(new Color(0.2f, 0.22f, 0.27f), 0.1f); white = Mat(new Color(0.94f, 0.95f, 0.96f), 0.35f);
            blueLid = Mat(new Color(0.15f, 0.4f, 0.85f), 0.4f); alu = Mat(new Color(0.75f, 0.77f, 0.8f), 0.55f, 0.85f); grey = Mat(new Color(0.55f, 0.57f, 0.6f), 0.2f); label = Mat(new Color(0.1f, 0.3f, 0.75f), 0.2f);
            var env = HabitatEnvironment.I; if (!env) return;
            int n = 0;
            Zone Z(string part) => env.zones.FirstOrDefault(z => z.name.Contains(part));
            // storage + maintenance: cargo transfer bags (NASA CTB 50 × 42 × 25 cm, half CTB 25 × 42 × 25 cm)
            n += Place(Z("Storage") ?? Z("Maintenance"), new System.Func<PhysProp>[] {
                () => Box("Cargo Transfer Bag (CTB)", new Vector3(0.50f, 0.25f, 0.42f), fabric, 9f, true), () => Box("Cargo Transfer Bag (CTB)", new Vector3(0.50f, 0.25f, 0.42f), fabric, 7f, true),
                () => Box("Half CTB", new Vector3(0.25f, 0.25f, 0.42f), fabric, 4f, true), () => Box("Cargo Transfer Bag (CTB)", new Vector3(0.50f, 0.25f, 0.42f), fabric, 8f, true),
                () => Box("Half CTB", new Vector3(0.25f, 0.25f, 0.42f), fabric, 3.5f, true), () => Model("NASA_Crew_Lock_Bag", "Crew lock bag (NASA)", 2.5f) }, true);
            n += Place(Z("Maintenance"), new System.Func<PhysProp>[] {
                () => Model("NASA_Hammer", "Hammer (NASA)", 0.6f), () => Model("NASA_Wrench", "Wrench (NASA)", 0.4f), () => Model("NASA_Pistol_Grip_Tool", "Pistol-grip power tool (NASA)", 2.2f),
                () => Box("Spare-parts box", new Vector3(0.35f, 0.2f, 0.25f), alu, 5f), () => Box("Half CTB", new Vector3(0.25f, 0.25f, 0.42f), fabric, 4f, true) }, false);
            n += Place(Z("Work_Lab"), new System.Func<PhysProp>[] {
                () => Model("NASA_Infrared_Camera", "Infrared camera (NASA)", 1.3f), () => Box("Regolith sample bag", new Vector3(0.22f, 0.1f, 0.18f), grey, 1.4f),
                () => Box("Regolith sample bag", new Vector3(0.22f, 0.1f, 0.18f), grey, 1.1f), () => Box("Sample container", new Vector3(0.2f, 0.16f, 0.2f), alu, 1.8f) }, false);
            n += Place(Z("Airlock"), new System.Func<PhysProp>[] {
                () => Model("NASA_Astronaut_Glove", "Spare EVA glove (NASA)", 0.9f), () => Model("NASA_Helmet", "Spare helmet (NASA)", 4.5f),
                () => Model("NASA_Crew_Lock_Bag", "Crew lock bag (NASA)", 2.5f), () => Box("Sample tote", new Vector3(0.3f, 0.2f, 0.25f), grey, 2f) }, false);
            n += Place(Z("Nature_Greenhouse"), new System.Func<PhysProp>[] {
                () => Box("Harvest tote", new Vector3(0.4f, 0.15f, 0.3f), Mat(new Color(0.2f, 0.6f, 0.35f), 0.3f), 1.2f), () => Box("Seed packet box", new Vector3(0.2f, 0.12f, 0.15f), white, 0.4f, false, Mat(new Color(0.35f, 0.75f, 0.3f), 0.4f)) }, false);
            n += Place(Z("Wardroom") ?? Z("Commons"), new System.Func<PhysProp>[] {
                () => Box("Food container", new Vector3(0.3f, 0.2f, 0.22f), white, 2.5f, false, blueLid), () => Box("Food container", new Vector3(0.3f, 0.2f, 0.22f), white, 2.2f, false, blueLid) }, false);
            // outside: cargo crates unloaded next to the landing pad (vacuum → no air drag)
            var pad = Util.Find("LOG_G30") ?? Util.FindPrefix("LOG_G30").FirstOrDefault() ?? Util.Find("CargoLander");
            if (pad)
            {
                for (int i = 0; i < 6; i++)
                {
                    var c = Box("Cargo crate (aluminium)", new Vector3(1.0f, 0.7f, 0.8f), alu, 60f + 15f * i, false, Mat(new Color(0.9f, 0.6f, 0.12f), 0.3f));
                    Vector3 p = pad.position + new Vector3(7f + (i % 3) * 1.4f, 0, -2f + (i / 3) * 1.2f);
                    if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 80f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point;
                    c.transform.position = p + Vector3.up * 0.4f; c.rb.linearDamping = 0f; c.rb.angularDamping = 0.02f; n++;
                }
            }
            Debug.Log($"[HUNCH3] LunarProps: {n} movable props placed (1/6 g physics).");
        }

        /// put props on the floor of a zone, away from every walking route (so they never block a door)
        int Place(Zone z, System.Func<PhysProp>[] makers, bool stack)
        {
            if (z == null) return 0;
            var rnd = new System.Random(z.name.GetHashCode()); int placed = 0;
            for (int i = 0; i < makers.Length; i++)
            {
                for (int tries = 0; tries < 40; tries++)
                {
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2f, d = (0.35f + 0.6f * (float)rnd.NextDouble()) * Mathf.Max(1.2f, z.radius * 0.75f);
                    Vector3 p = z.center + new Vector3(Mathf.Cos(a) * d, 1.2f, Mathf.Sin(a) * d);
                    if (!Physics.SphereCast(p, 0.15f, Vector3.down, out var hit, 2.4f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.85f) continue;
                    if (hit.collider.name.StartsWith("Walkway_") || NearRoute(hit.point, 0.85f)) continue;
                    if (Physics.CheckBox(hit.point + Vector3.up * 0.4f, new Vector3(0.3f, 0.3f, 0.3f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;   // furniture / wall in the way
                    var pr = makers[i](); if (pr == null) break;
                    pr.transform.position = hit.point + Vector3.up * 0.2f;
                    pr.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                    placed++;
                    if (stack && i + 1 < makers.Length && rnd.NextDouble() < 0.5)
                    {   // stack the next bag on top (it settles on its own)
                        var top = makers[++i](); if (top != null) { top.transform.position = hit.point + Vector3.up * 0.55f; top.transform.rotation = pr.transform.rotation; placed++; }
                    }
                    break;
                }
            }
            return placed;
        }
        static bool NearRoute(Vector3 p, float r)
        {
            foreach (var kv in Nav.adj)
                foreach (var nb in kv.Value)
                {
                    Vector3 a = Nav.wp[kv.Key].position, b = Nav.wp[nb].position; if (Mathf.Abs(a.y - p.y) > 1.5f && Mathf.Abs(b.y - p.y) > 1.5f) continue;
                    Vector2 P = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z), ab = B - A;
                    float t = ab.sqrMagnitude < 1e-4f ? 0 : Mathf.Clamp01(Vector2.Dot(P - A, ab) / ab.sqrMagnitude);
                    if ((P - (A + ab * t)).magnitude < r) return true;
                }
            return false;
        }

        // ------------------------------------------------------------------ pick up / carry / throw (desktop)
        void Update()
        {
            var pr = PlayerRig.I; if (!pr || pr.isXR || Panels.AnyOpen) return;
            var kb = Keyboard.current; var ms = Mouse.current;
            Vector3 eye = ThirdPersonView.Active ? ThirdPersonView.Eye : pr.cam.transform.position;
            if (held)
            {
                Vector3 target = eye + pr.cam.transform.forward * heldDist;
                held.rb.linearVelocity = (target - held.transform.position) * 9f;
                held.rb.angularVelocity *= 0.8f;
                HUD.I?.SetPrompt($"Carrying: {held.label}  ({held.rb.mass:0.#} kg mass · weighs like {held.rb.mass / 6f:0.#} kg on Earth)  ·  E put down  ·  click throw");
                if (kb != null && kb.eKey.wasPressedThisFrame) Drop(Vector3.zero);
                else if (ms != null && ms.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked) Drop(pr.cam.transform.forward * Mathf.Lerp(6f, 2.5f, Mathf.InverseLerp(1f, 10f, held.rb.mass)) + Vector3.up * 1.2f);
                return;
            }
            if (Physics.Raycast(eye, pr.cam.transform.forward, out var hit, 2.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                var p = hit.collider.GetComponentInParent<PhysProp>();
                if (p)
                {
                    bool liftable = p.rb.mass <= 30f;
                    HUD.I?.SetPrompt(liftable ? $"E: pick up {p.label}  ({p.rb.mass:0.#} kg — feels like {p.rb.mass / 6f:0.#} kg in 1/6 g)" : $"{p.label}: {p.rb.mass:0} kg — too big to carry, push it by walking into it");
                    if (liftable && kb != null && kb.eKey.wasPressedThisFrame) { held = p; held.rb.useGravity = false; heldDist = Mathf.Clamp(hit.distance, 0.8f, 1.4f); }
                }
            }
        }
        void Drop(Vector3 throwVel)
        {
            if (!held) return;
            held.rb.useGravity = true; held.rb.linearVelocity = throwVel;
            if (throwVel.sqrMagnitude > 0.1f) HUD.I?.Toast($"Thrown! In 1/6 g the {held.label} flies about 6× farther and falls slowly — that's real lunar gravity.");
            held = null;
        }

        /// moonquake / big jolt: loose items hop
        public static void Jolt(float strength)
        {
            foreach (var p in All) if (p && p.rb && !p.rb.isKinematic)
                p.rb.AddForce(new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1f), Random.Range(-0.3f, 0.3f)) * strength, ForceMode.VelocityChange);
        }
    }

    /// a loose object with real mass under 1/6 g
    public class PhysProp : MonoBehaviour
    {
        public string label; public Rigidbody rb;
        void OnDestroy() { LunarProps.All.Remove(this); }
    }

    /// walking into a loose prop pushes it (a person of ~80 kg + suit nudging cargo in 1/6 g)
    public class PlayerPusher : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init() { SceneManager.sceneLoaded += (s, m) => Attach(); Attach(); }
        static void Attach()
        {
            var cc = Object.FindFirstObjectByType<CharacterController>(); if (cc && !cc.GetComponent<PlayerPusher>()) cc.gameObject.AddComponent<PlayerPusher>();
        }
        void OnControllerColliderHit(ControllerColliderHit h)
        {
            var rb = h.rigidbody; if (!rb || rb.isKinematic || !rb.GetComponent<PhysProp>()) return;
            if (h.moveDirection.y < -0.3f) return;                                    // standing on it, not pushing
            Vector3 dir = new Vector3(h.moveDirection.x, 0, h.moveDirection.z); if (dir.sqrMagnitude < 1e-4f) return;
            float speed = h.controller.velocity.magnitude;
            float dv = Mathf.Clamp(speed, 0.6f, 3f) * 80f / (80f + rb.mass);               // momentum shared with a ~80 kg astronaut
            Vector3 want = dir.normalized * dv + Vector3.up * dv * 0.18f;                  // a little lift: things tumble/hop in low g
            if (Vector3.Dot(rb.linearVelocity, dir.normalized) < dv) rb.AddForceAtPosition(want - Vector3.Project(rb.linearVelocity, dir.normalized), h.point, ForceMode.VelocityChange);
        }
    }

    /// THIRD-PERSON VIEW (desktop) — F toggles. Your astronaut walks with you; the camera sits behind your shoulder and never
    /// goes through walls. VR always stays first-person (third-person view in a headset causes motion sickness).
    [DefaultExecutionOrder(-300)]
    public class ThirdPersonView : MonoBehaviour
    {
        public static ThirdPersonView I; public static bool Active; public static Vector3 Eye;
        Transform avatar; Quaternion baseRot; Vector3 front0 = Vector3.forward; bool haveAvatar; Vector3 savedLocal; bool offset; Vector3 lastFeet; float bob; float dist = 2.3f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) Make(); };
            if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) Make();
        }
        static void Make() { if (!FindFirstObjectByType<ThirdPersonView>()) new GameObject("ThirdPersonView").AddComponent<ThirdPersonView>(); }
        void Awake() { I = this; Active = false; }

        void SetupAvatar()
        {
            if (haveAvatar) return; haveAvatar = true;
            avatar = Util.Find("CREW1_IVA_Root"); if (!avatar) return;
            baseRot = avatar.rotation;
            var beacon = avatar.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Contains("WellnessCommBeacon_Housing"));
            Vector3 f = beacon ? beacon.position - avatar.position : avatar.rotation * Vector3.forward; f.y = 0;
            if (f.sqrMagnitude < 1e-4f) { f = avatar.up; f.y = 0; }
            front0 = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            foreach (var c in avatar.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var r in avatar.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        public void Toggle()
        {
            var pr = PlayerRig.I; if (!pr) return;
            if (pr.isXR) { HUD.I?.Toast("Third-person view is desktop only — in a headset it causes motion sickness."); return; }
            SetupAvatar(); if (!avatar) { HUD.I?.Toast("No astronaut model found for third-person view."); return; }
            Active = !Active;
            avatar.gameObject.SetActive(Active);
            if (!Active && offset) { pr.cam.transform.localPosition = savedLocal; offset = false; }
            HUD.I?.Toast(Active ? "Third-person view ON — you can see your astronaut. (F = back to first person)" : "First-person view.");
        }

        void Update()
        {
            var pr = PlayerRig.I; if (!pr || !pr.cam) return;
            if (offset) { pr.cam.transform.localPosition = savedLocal; offset = false; }        // give the player rig its real head position back first
            var kb = Keyboard.current; if (kb != null && kb.fKey.wasPressedThisFrame && !Panels.AnyOpen) Toggle();
            if (Active && (pr.isXR || SleepSystem.PlayerAsleep)) { }                              // stays as is; LateUpdate skips
        }

        void LateUpdate()
        {
            var pr = PlayerRig.I; if (!pr || !pr.cam) return;
            Eye = pr.cam.transform.position;
            if (!Active || !avatar || pr.isXR) return;
            bool hide = SleepSystem.PlayerAsleep || pr.suit == SuitState.EVA;                    // in bed or in a suit: no IVA body / no offset
            avatar.gameObject.SetActive(!hide);
            if (hide) return;
            // body follows the player: feet on the floor, facing where you look
            Vector3 feet = pr.FeetPosition; float moved = new Vector2(feet.x - lastFeet.x, feet.z - lastFeet.z).magnitude; lastFeet = feet;
            bob = moved > 0.002f ? bob + Time.deltaTime * 7f : Mathf.MoveTowards(bob, Mathf.Round(bob / Mathf.PI) * Mathf.PI, Time.deltaTime * 4f);
            avatar.position = feet + Vector3.up * (0.025f * Mathf.Abs(Mathf.Sin(bob)));
            Vector3 look = pr.origin ? pr.origin.transform.forward : pr.cam.transform.forward; look.y = 0;
            if (look.sqrMagnitude > 1e-4f) avatar.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(front0, look.normalized, Vector3.up), Vector3.up) * baseRot;
            // camera behind the shoulder, pulled in when a wall is close
            var cam = pr.cam.transform; savedLocal = cam.localPosition; offset = true;
            Vector3 back = -cam.forward * dist + Vector3.up * 0.35f + cam.right * 0.35f;
            float d = back.magnitude;
            if (Physics.SphereCast(Eye, 0.18f, back / d, out var hit, d, ~0, QueryTriggerInteraction.Ignore)) d = Mathf.Max(0.25f, hit.distance - 0.05f);
            cam.position = Eye + back / back.magnitude * d;
        }
    }
}
