// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/03_Player_Controls/HunchVr.cs
//  Section : 03_Player_Controls
//  Folder  : How you move and find your way: VR controllers, keyboard/mouse, phone touch controls,
//            doorway help, NavMesh paths, green guide arrows + minimap.
//  Purpose : VR controls with one job per button. The laser pointer, the trigger click and the
//            teleport are done here, read straight from the controllers, so they do not depend on
//            the XR Interaction Toolkit's own pointer.
//  Contains: HunchVr
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.IO;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRC = UnityEngine.InputSystem.XR.XRController;

/// VR controls with one job per button. The laser pointer, the trigger click and the teleport are done here, read straight
/// from the controllers, so they do not depend on the XR Interaction Toolkit's own pointer.
///
///   RIGHT hand   trigger  select / use / fire          grip   teleport (hold to aim, let go to jump there)
///                A        jump (in a menu: choose)     B      next tool (in a menu: back)
///                stick    turn
///                stick    turn, click it for the security cameras
///   LEFT hand    trigger  select too                   grip   grab
///                X        go to the next job           Y      menu (also the left menu button)
///                stick    walk, click it to run
/// Everything else (maps, cameras, meeting, health, reading, music, autopilot, jetpack, talk, eat) is a big button on the dashboard.
///
/// While you play it writes vr_log.txt (next to the game's .exe, or in HunchBridge in the Editor): what the headset and
/// each button reported once a second, so a problem can be diagnosed afterwards.
public class HunchVr : MonoBehaviour
{
    public static Collider Hover;                                   // what the laser points at: a menu button, a dashboard button, a zone orb or a panel part
    public static float RightTrigger, LeftTrigger, RightGrip, LeftGrip; public static Vector2 LeftStickNow;
    public static bool A, B, X, Y;
    public static bool Hovering(Transform t) { return Hover && t && Hover.transform.IsChildOf(t); }
    static HunchVr me; static float claimAt = -9f;
    /// One click per pull. The toolkit's own pointer and this script can both notice the same press: whoever asks first gets it.
    /// A press that is only the grip (teleport / grab) never counts as "select".
    public static bool Claim(bool fromToolkit)
    {
        if (fromToolkit && (RightGrip > 0.5f || LeftGrip > 0.5f) && RightTrigger < 0.3f && LeftTrigger < 0.3f) return false;
        if (Time.unscaledTime - claimAt < 0.25f) return false;
        claimAt = Time.unscaledTime; return true;
    }
    /// where the right hand points. Uses the controller's own aim pose (the same line the headset's menus use) when it reports one.
    public static bool TryAim(out Ray ray) { ray = default(Ray); return me && me.Aim(XRC.rightHand, me.right, out ray); }
    bool Aim(XRC c, Transform fallback, out Ray ray)
    {
        ray = default(Ray);
        if (c != null && origin)
        {
            var pc = c.TryGetChildControl<Vector3Control>("pointerPosition"); var qc = c.TryGetChildControl<QuaternionControl>("pointerRotation");
            if (pc != null && qc != null)
            {
                Quaternion q = qc.ReadValue(); Vector3 lp = pc.ReadValue();
                if (q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w > 0.5f && lp.sqrMagnitude > 1e-6f)
                {
                    Transform space = origin.CameraFloorOffsetObject ? origin.CameraFloorOffsetObject.transform : origin.transform;
                    Vector3 dir = space.rotation * (q * Vector3.forward); ray = new Ray(space.TransformPoint(lp) + dir * 0.03f, dir); aimSource = "aim pose"; return true;
                }
            }
        }
        if (fallback && fallback.gameObject.activeInHierarchy) { ray = new Ray(fallback.position + fallback.forward * 0.05f, fallback.forward); aimSource = "controller object"; return true; }
        aimSource = "none"; return false;
    }
    string aimSource = "none";

    HabitatGame game; HunchFrontier frontier; HunchMenu menu; XROrigin origin; Transform right, left, cam;
    LineRenderer laser, arc; Transform dot, ring; Material lineMat, hotMat, ringMat, badMat;
    bool wasR, wasL, wasGrip, wasA, wasB, wasX, wasY, wasClick, wasMenu, teleOk, built; int stickDir; Vector3 telePoint;
    readonly RaycastHit[] hits = new RaycastHit[32]; readonly Vector3[] arcPts = new Vector3[48];
    float vy; int switchedOff, managersOff, turnDir; bool jumpNow, addedBody; readonly System.Collections.Generic.List<GameObject> keepOff = new System.Collections.Generic.List<GameObject>();
    float logAt, flushAt, startAt; readonly StringBuilder log = new StringBuilder(); string logPath, lastEvent = ""; int logLines;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; }
    static void OnScene(Scene s, LoadSceneMode m) { Attach(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { Attach(); }
    static void Attach() { var g = FindFirstObjectByType<HabitatGame>(); if (g && !g.GetComponent<HunchVr>() && !Application.isBatchMode) g.gameObject.AddComponent<HunchVr>(); }

    void Start()
    {
        me = this; game = GetComponent<HabitatGame>(); origin = game.origin ? game.origin : FindFirstObjectByType<XROrigin>(); startAt = Time.unscaledTime;
        try
        {
            string dir = Directory.GetParent(Application.dataPath).FullName, bridge = Path.Combine(dir, "HunchBridge");
            logPath = Path.Combine(Application.isEditor && Directory.Exists(bridge) ? bridge : dir, "vr_log.txt");
        }
        catch { logPath = null; }
        Hover = null;
    }

    static float Axis(XRC c, string name) { if (c == null) return 0f; var a = c.TryGetChildControl<AxisControl>(name); return a != null ? a.ReadValue() : 0f; }
    static bool Btn(XRC c, string name) { if (c == null) return false; var b = c.TryGetChildControl<ButtonControl>(name); return b != null && b.isPressed; }
    static Vector2 Stick(XRC c) { if (c == null) return Vector2.zero; var s = c.TryGetChildControl<Vector2Control>("thumbstick"); if (s == null) s = c.TryGetChildControl<Vector2Control>("primary2DAxis"); return s != null ? s.ReadValue() : Vector2.zero; }

    void Build()
    {
        built = true;
        if (!origin) return;
        foreach (var t in origin.GetComponentsInChildren<Transform>(true)) { if (t.name == "Right Controller") right = t; else if (t.name == "Left Controller") left = t; }
        cam = origin.Camera ? origin.Camera.transform : null;
        if (!origin.GetComponent<CharacterController>())      // the body that walks and falls; the VR rig normally has one already
        {
            var body = origin.gameObject.AddComponent<CharacterController>();
            body.height = 1.5f; body.radius = 0.18f; body.center = new Vector3(0f, 0.77f, 0f); body.stepOffset = 0.36f; body.slopeLimit = 60f; body.skinWidth = 0.02f; body.minMoveDistance = 0f; addedBody = true;
        }
        // walking, turning, jumping and teleporting are all done here, so the toolkit's own versions are switched off: nothing moves you twice
        foreach (var t in origin.GetComponentsInChildren<Transform>(true)) if (t.name == "Locomotion" || t.name.Contains("Teleport Interactor")) { t.gameObject.SetActive(false); switchedOff++; keepOff.Add(t.gameObject); }
        // the toolkit's own stick handler would bring its teleport line back whenever a stick is pushed forward
        foreach (var mb in origin.GetComponentsInChildren<MonoBehaviour>(true)) if (mb && mb.GetType().Name == "ControllerInputActionManager") { mb.enabled = false; managersOff++; }
        var src = game.zones.Count > 0 && game.zones[0].ring ? game.zones[0].ring.sharedMaterial : null;
        if (!src) return;
        lineMat = new Material(src); lineMat.SetColor("_BaseColor", new Color(0.55f, 0.9f, 1f, 0.55f)); lineMat.renderQueue = 3200;
        hotMat = new Material(src); hotMat.SetColor("_BaseColor", new Color(1f, 0.8f, 0.25f, 0.95f)); hotMat.renderQueue = 3200;
        ringMat = new Material(src); ringMat.SetColor("_BaseColor", new Color(0.3f, 1f, 0.5f, 0.9f)); ringMat.renderQueue = 3200;
        badMat = new Material(src); badMat.SetColor("_BaseColor", new Color(1f, 0.3f, 0.25f, 0.8f)); badMat.renderQueue = 3200;
        laser = Line("HUNCH VR laser", 2, 0.006f); arc = Line("HUNCH VR teleport arc", arcPts.Length, 0.02f);
        dot = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform; Destroy(dot.GetComponent<Collider>()); dot.name = "HUNCH VR laser dot"; dot.localScale = Vector3.one * 0.025f;
        dot.GetComponent<Renderer>().sharedMaterial = hotMat; dot.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform; Destroy(ring.GetComponent<Collider>()); ring.name = "HUNCH VR teleport spot"; ring.localScale = new Vector3(0.7f, 0.01f, 0.7f);
        ring.GetComponent<Renderer>().sharedMaterial = ringMat; ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dot.gameObject.SetActive(false); ring.gameObject.SetActive(false); arc.enabled = false; laser.enabled = false;
    }
    LineRenderer Line(string name, int points, float width)
    {
        var go = new GameObject(name); var lr = go.AddComponent<LineRenderer>(); lr.sharedMaterial = lineMat; lr.positionCount = points; lr.startWidth = lr.endWidth = width; lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false; lr.numCapVertices = 2; return lr;
    }

    bool Mine(Collider c) { return !c || c is CharacterController || (origin && c.transform.IsChildOf(origin.transform)); }

    /// nearest thing along the ray: a button-like target (returned in "target") or whatever solid thing is in front of it
    bool Point(Ray ray, float dist, out RaycastHit best, out Collider target)
    {
        int n = Physics.RaycastNonAlloc(ray, hits, dist, ~0, QueryTriggerInteraction.Collide); best = default(RaycastHit); target = null; float bd = float.MaxValue; bool any = false;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider; if (Mine(c)) continue;
            bool button = c.GetComponentInParent<XRSimpleInteractable>() || c.GetComponentInParent<HunchTarget>();
            if (c.isTrigger && !button) continue;
            if (hits[i].distance < bd) { bd = hits[i].distance; best = hits[i]; target = button ? c : null; any = true; }
        }
        return any;
    }
    bool Solid(Vector3 from, Vector3 to, out RaycastHit best)
    {
        Vector3 d = to - from; float len = d.magnitude; best = default(RaycastHit); if (len < 1e-4f) return false;
        int n = Physics.RaycastNonAlloc(new Ray(from, d / len), hits, len, ~0, QueryTriggerInteraction.Ignore); float bd = float.MaxValue; bool any = false;
        for (int i = 0; i < n; i++) { if (Mine(hits[i].collider)) continue; if (hits[i].distance < bd) { bd = hits[i].distance; best = hits[i]; any = true; } }
        return any;
    }

    void Update()
    {
        var rc = XRC.rightHand; var lc = XRC.leftHand;
        RightTrigger = Axis(rc, "trigger"); LeftTrigger = Axis(lc, "trigger"); RightGrip = Axis(rc, "grip"); LeftGrip = Axis(lc, "grip");
        A = Btn(rc, "primaryButton"); B = Btn(rc, "secondaryButton"); X = Btn(lc, "primaryButton"); Y = Btn(lc, "secondaryButton");
        Vector2 rs = Stick(rc), ls = Stick(lc); LeftStickNow = ls;
        WriteLog(rc, lc, rs, ls);

        if (!game || !game.HeadsetActive) { Hover = null; return; }
        if (!built) Build();
        for (int i = 0; i < keepOff.Count; i++) if (keepOff[i] && keepOff[i].activeSelf) keepOff[i].SetActive(false);
        if (!frontier) frontier = GetComponent<HunchFrontier>();
        if (!menu) menu = GetComponent<HunchMenu>();
        bool intro = HunchIntro.Active, inMenu = game.menuOpen;

        // ---- laser pointer from the right hand
        Hover = probeHover; bool showLaser = false;
        Ray ray;
        if (laser && !intro && Aim(rc, right, out ray))
        {
            RaycastHit h; Collider target;
            Vector3 end = ray.origin + ray.direction * 4f;
            if (Point(ray, 14f, out h, out target)) { end = h.point; if (!probeHover) Hover = target; }
            laser.SetPosition(0, ray.origin); laser.SetPosition(1, end); showLaser = true;
            var want = Hover ? hotMat : lineMat; if (laser.sharedMaterial != want) laser.sharedMaterial = want;
            if (dot) { if (dot.gameObject.activeSelf != (Hover != null)) dot.gameObject.SetActive(Hover != null); if (Hover) dot.position = end; }
        }
        if (laser && laser.enabled != showLaser) laser.enabled = showLaser;
        if (!showLaser && dot && dot.gameObject.activeSelf) dot.gameObject.SetActive(false);

        // ---- triggers: select / use
        bool r = RightTrigger > 0.55f, l = LeftTrigger > 0.55f;
        if (!intro) { if (r && !wasR && Claim(false)) { lastEvent = "right trigger on " + (Hover ? Hover.name : "nothing"); game.VrSubmit(true); } if (l && !wasL && Claim(false)) { lastEvent = "left trigger on " + (Hover ? Hover.name : "nothing"); game.VrSubmit(false); } }
        wasR = r; wasL = l;

        // ---- buttons
        bool a = A, b = B, x = X, y = Y;
        if (!intro)
        {
            if (y && !wasY && menu) { lastEvent = "Y menu"; menu.VrMenuButton(); }
            if (inMenu && menu)
            {
                if (a && !wasA) { lastEvent = "A choose"; menu.VrSelect(); }
                if (b && !wasB) { lastEvent = "B back"; menu.VrBack(); }
                float sy = Mathf.Abs(rs.y) > Mathf.Abs(ls.y) ? rs.y : ls.y; int dir = sy > 0.6f ? -1 : (sy < -0.6f ? 1 : 0);
                if (dir != 0 && dir != stickDir) menu.VrMove(dir);
                stickDir = dir;
            }
            else
            {
                stickDir = 0;
                if (b && !wasB && frontier) { lastEvent = "B next tool"; frontier.VrNextTool(); }
                if (x && !wasX) { lastEvent = "X next job"; game.VrNextJob(); }
                if (a && !wasA) jumpNow = true;
            }
        }
        wasA = a; wasB = b; wasX = x; wasY = y;
        // the right stick click opens the security cameras (the left menu button already opens the menu, like Y)
        bool click = Btn(rc, "thumbstickClicked");
        if (!intro && click && !wasClick && frontier) { lastEvent = "right stick click: cameras"; frontier.VrStickClick(); }
        wasClick = click;

        // ---- right grip: teleport. Hold to aim the arc, let go to jump there.
        Ray aim = default(Ray);
        bool grip = RightGrip > 0.6f && !intro && !inMenu && arc && !(frontier && frontier.ControlsLocked) && game.sim.phase != HabPhase.Briefing && game.sim.phase != HabPhase.Report && Aim(rc, right, out aim);
        if (grip)
        {
            Vector3 p = aim.origin, v = aim.direction * 8.5f; const float step = 0.045f; int count = 1; arcPts[0] = p; teleOk = false; RaycastHit h;
            for (int i = 1; i < arcPts.Length; i++)
            {
                Vector3 q = p + v * step; v += Vector3.down * 9.8f * step;
                if (Solid(p, q, out h)) { arcPts[i] = h.point; count = i + 1; teleOk = h.normal.y > 0.6f && !h.collider.GetComponentInParent<XRGrabInteractable>() && !h.collider.GetComponentInParent<HunchTarget>(); telePoint = h.point; break; }
                arcPts[i] = q; p = q; count = i + 1;
            }
            arc.positionCount = count; for (int i = 0; i < count; i++) arc.SetPosition(i, arcPts[i]);
            var want = teleOk ? ringMat : badMat; if (arc.sharedMaterial != want) arc.sharedMaterial = want;
            if (!arc.enabled) arc.enabled = true;
            if (ring.gameObject.activeSelf != teleOk) ring.gameObject.SetActive(teleOk);
            if (teleOk) ring.position = telePoint + Vector3.up * 0.02f;
        }
        else
        {
            if (wasGrip && teleOk && frontier && cam)
            {
                lastEvent = "teleport to " + telePoint.ToString("F1");
                game.flyMode = false;
                frontier.TeleportTo(telePoint, cam.eulerAngles.y); game.Buzz(0.5f, 0.08f);
            }
            teleOk = false;
            if (arc && arc.enabled) arc.enabled = false;
            if (ring && ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
        }
        wasGrip = grip;
        Move(ls, rs, intro || inMenu);
    }

    /// Left stick walks (in the direction you face), right stick turns in steps, A jumps. With the jetpack on, the stick flies where you look.
    /// throw the player upward (bounce pads); gravity brings them back
    public void Launch(float upSpeed) { vy = upSpeed; launched = true; }
    bool launched;
    void Move(Vector2 ls, Vector2 rs, bool busy)
    {
        if (!origin || !cam) return;
        var cc = origin.GetComponent<CharacterController>(); float dt = Mathf.Min(Time.deltaTime, 0.05f);
        bool playing = game.sim.phase == HabPhase.Mission || game.sim.phase == HabPhase.Commissioning;
        if (busy || !playing || (frontier && frontier.ControlsLocked)) { jumpNow = false; turnDir = 0; return; }      // menus and the autopilot have the controls
        int td = rs.x > 0.7f ? 1 : (rs.x < -0.7f ? -1 : 0);
        if (td != 0 && td != turnDir) origin.RotateAroundCameraUsingOriginUp(30f * td);
        turnDir = td;
        if (ls.sqrMagnitude < 0.04f) ls = Vector2.zero;
        float speed = 2.6f * (frontier ? frontier.VrMoveMul : 1f);
        if (game.flyMode)
        {
            Vector3 fly = cam.rotation * new Vector3(ls.x, 0f, ls.y) * (speed * 2.2f) * dt; vy = 0f; jumpNow = false;
            if (HunchNavAssist.hold) { } else if (cc && cc.enabled && cc.gameObject.activeInHierarchy) cc.Move(fly); else origin.transform.position += fly;
            return;
        }
        if (!cc || !cc.enabled || !cc.gameObject.activeInHierarchy || HunchNavAssist.hold) { jumpNow = false; return; }
        if (launched) launched = false;
        else if (cc.isGrounded && vy <= 0f) { vy = -0.5f; if (jumpNow) { vy = 2.7f; lastEvent = "A jump"; } } else vy += Physics.gravity.y * dt;
        jumpNow = false;
        Vector3 walk = Quaternion.Euler(0f, cam.eulerAngles.y, 0f) * new Vector3(ls.x, 0f, ls.y) * speed;
        cc.Move((walk + Vector3.up * vy) * dt);
    }

    void WriteLog(XRC rc, XRC lc, Vector2 rs, Vector2 ls)
    {
        if (logPath == null || logLines > 3600) return;      // one hour
        float now = Time.unscaledTime;
        if (now >= logAt)
        {
            logAt = now + 1f; logLines++;
            if (logLines == 1) log.Append("Lunar Habitat Commander VR log  ").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("  ").Append(Application.isEditor ? "Editor" : "built game").Append("  Unity ").Append(Application.unityVersion).Append("\n");
            var c = origin && origin.Camera ? origin.Camera.transform : null;
            log.Append((now - startAt).ToString("F0")).Append("s headset=").Append(game && game.HeadsetActive).Append(" running=").Append(HunchDesktopFallback.HeadsetRunning())
               .Append(" phase=").Append(game ? game.sim.phase.ToString() : "?").Append(" menu=").Append(game && game.menuOpen).Append(" intro=").Append(HunchIntro.Active).Append(" fly=").Append(game && game.flyMode)
               .Append(" head=").Append(c ? c.position.ToString("F1") : "?").Append(" yaw=").Append(c ? c.eulerAngles.y.ToString("F0") : "?")
               .Append(" | R ").Append(rc != null ? rc.layout : "none").Append(" trig=").Append(RightTrigger.ToString("F2")).Append(" grip=").Append(RightGrip.ToString("F2")).Append(A ? " A" : "").Append(B ? " B" : "").Append(" stick=").Append(rs.ToString("F1"))
               .Append(" | L ").Append(lc != null ? lc.layout : "none").Append(" trig=").Append(LeftTrigger.ToString("F2")).Append(" grip=").Append(LeftGrip.ToString("F2")).Append(X ? " X" : "").Append(Y ? " Y" : "").Append(" stick=").Append(ls.ToString("F1"))
               .Append(" | ctrl=").Append(right ? "found" : "missing").Append(" aim=").Append(aimSource).Append(" off=").Append(switchedOff).Append("+").Append(managersOff).Append(addedBody ? " body=added" : "").Append(" feet=").Append(origin ? origin.transform.position.ToString("F1") : "?").Append(" point=").Append(Hover ? Hover.name : "-").Append(" last=").Append(lastEvent).Append("\n");
        }
        if (now >= flushAt) { flushAt = now + 3f; try { File.WriteAllText(logPath, log.ToString()); } catch { logPath = null; } }
    }

    // ------------------------------------------------------------------ test hooks (Editor, no headset needed)
    public static string probeName = ""; static Collider probeHover;
    /// shows the headset's dashboard, menus and button logic on the flat screen
    public string ProbeFake() { game.HeadsetActive = true; return "headset view on the flat screen"; }
    public string ProbeUnfake() { game.HeadsetActive = false; probeHover = null; return "back to keyboard and mouse"; }
    /// pretend the laser is on the object called probeName (for example "Button 3")
    public string ProbePoint()
    {
        probeHover = null;
        foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None)) if (c.name == probeName) { probeHover = c; break; }
        Hover = probeHover; return probeHover ? "pointing at " + probeHover.name : "nothing called " + probeName;
    }
    public string ProbeTrigger() { Hover = probeHover; if (!Claim(false)) return "too soon"; game.VrSubmit(true); return "trigger on " + (Hover ? Hover.name : "nothing") + State(); }
    public string ProbeY() { menu.VrMenuButton(); return "Y" + State(); }
    public string ProbeA() { if (game.menuOpen) menu.VrSelect(); else jumpNow = true; return "A" + State(); }
    public string ProbeB() { if (game.menuOpen) menu.VrBack(); else frontier.VrNextTool(); return "B" + State(); }
    public string ProbeX() { game.VrNextJob(); return "X" + State(); }
    public string ProbeStickDown() { menu.VrMove(1); return "stick down" + State(); }
    public string ProbeTeleport()
    {
        Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized, from = cam.position + f * 3f; RaycastHit h;
        if (!Solid(from, from + Vector3.down * 6f, out h)) return "no floor 3 m ahead";
        Vector3 before = origin.transform.position; frontier.TeleportTo(h.point, cam.eulerAngles.y);
        return "teleported " + (origin.transform.position - before).magnitude.ToString("F1") + " m to " + h.point.ToString("F1") + State();
    }
    /// every piece of 3D text near the player: what it is, how big it is drawn
    public string ProbeTexts()
    {
        var sb = new StringBuilder();
        foreach (var t in FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None))
        {
            if (!t.gameObject.activeInHierarchy || (t.transform.position - cam.position).magnitude > 8f) continue;
            var b = t.GetComponent<Renderer>().bounds.size; string s = t.text ?? ""; s = s.Replace("\n", "/"); if (s.Length > 26) s = s.Substring(0, 26);
            sb.Append("\n   ").Append(t.name).Append(" < ").Append(t.transform.parent ? t.transform.parent.name : "-").Append("  font ").Append(t.fontSize.ToString("F2")).Append(t.enableAutoSizing ? " auto " + t.fontSizeMin.ToString("F2") + "-" + t.fontSizeMax.ToString("F2") : "")
              .Append(" scale ").Append(t.transform.lossyScale.x.ToString("F2")).Append(" box ").Append(t.rectTransform.sizeDelta.ToString("F2")).Append(" drawn ").Append(b.x.ToString("F1")).Append("x").Append(b.y.ToString("F1")).Append("x").Append(b.z.ToString("F1")).Append("  '").Append(s).Append("'");
        }
        return sb.ToString();
    }
    string State() { return "  [menu=" + game.menuOpen + " phase=" + game.sim.phase + " near=" + game.NearZone + " fly=" + game.flyMode + " feet=" + origin.transform.position.ToString("F1") + " off=" + switchedOff + " laser=" + (laser ? "built" : "none") + "]"; }

    void OnDestroy()
    {
        try { if (logPath != null && log.Length > 0) File.WriteAllText(logPath, log.ToString()); } catch { }
        foreach (var o in new Object[] { laser ? laser.gameObject : null, arc ? arc.gameObject : null, dot ? dot.gameObject : null, ring ? ring.gameObject : null }) if (o) Destroy(o);
        Hover = null; if (me == this) me = null;
    }
}
