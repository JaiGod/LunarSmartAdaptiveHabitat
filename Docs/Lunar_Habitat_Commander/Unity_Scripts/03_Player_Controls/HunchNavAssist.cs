// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/03_Player_Controls/HunchNavAssist.cs
//  Section : 03_Player_Controls
//  Folder  : How you move and find your way: VR controllers, keyboard/mouse, phone touch controls,
//            doorway help, NavMesh paths, green guide arrows + minimap.
//  Purpose : Makes sure the player can always get into every room, in VR and on desktop: - a small
//            body (1.5 m tall, 0.18 m radius) that fits doorways and tight corridors
//  Contains: HunchNavAssist
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

/// Makes sure the player can always get into every room, in VR and on desktop:
///  - a small body (1.5 m tall, 0.18 m radius) that fits doorways and tight corridors
///  - "auto-door": keep pushing against a door, wall or blocked stair for a moment and you are let through to the next free floor
///  - "lift": go straight to the floor above or below
public class HunchNavAssist : MonoBehaviour
{
    public XROrigin origin;
    public float radius = 0.18f, bodyHeight = 1.5f, pushSeconds = 0.3f, reach = 9f;
    [NonSerialized] public float debugSpeed = 3f;      // metres a second while a direction is held for the player (the autopilots)
    [NonSerialized] public Vector3 debugIntent;        // used by the automatic test to "hold a direction"
    public int passes, lifts;
    /// the cheat: push against a wall and you are let through it. Off unless you switch it on (Menu > Base systems > Walk through walls).
    public static bool wallPass; public static void SetWallPass(bool on) { wallPass = on; PlayerPrefs.SetInt("hunch_wallpass", on ? 1 : 0); }
    [NonSerialized] public bool noAutoPass;            // the autopilot is following a real path: do not push it through a wall it only brushes

    HabitatGame game; HunchDesktopFallback desk; CharacterController cc;
    InputAction stick, up, down, upVr;
    float stuck, progress; Vector3 last; AudioSource src; AudioClip whoosh;

    void Awake()
    {
        game = GetComponent<HabitatGame>(); desk = GetComponent<HunchDesktopFallback>();
        wallPass = PlayerPrefs.GetInt("hunch_wallpass", 0) == 1;
        if (!origin) origin = FindFirstObjectByType<XROrigin>();
        src = gameObject.AddComponent<AudioSource>(); src.spatialBlend = 0f; src.playOnAwake = false; src.volume = 0.35f;
        int n = 9000; var d = new float[n]; var rng = new System.Random(5); float lp = 0f;
        for (int i = 0; i < n; i++) { float env = Mathf.Sin(Mathf.PI * i / n); lp = Mathf.Lerp(lp, (float)rng.NextDouble() * 2f - 1f, 0.12f); d[i] = lp * env * 0.9f; }
        whoosh = AudioClip.Create("door", n, 1, 44100, false); whoosh.SetData(d, 0);
    }

    void OnEnable()
    {
        stick = new InputAction("move", InputActionType.Value, "<XRController>{LeftHand}/thumbstick"); stick.Enable();
        up = new InputAction("liftUp", InputActionType.Button); up.AddBinding("<Keyboard>/pageUp"); up.AddBinding("<Gamepad>/dpad/up"); up.Enable();
        down = new InputAction("liftDown", InputActionType.Button); down.AddBinding("<Keyboard>/pageDown"); down.AddBinding("<Gamepad>/dpad/down"); down.Enable();
        upVr = new InputAction("liftVr", InputActionType.Button); upVr.AddBinding("<Keyboard>/f10"); upVr.Enable();      // in VR the lift is a menu choice: a stick click is too easy to press by accident
        up.performed += _ => Lift(1);
        down.performed += _ => Lift(-1);
        upVr.performed += _ => { if (!Lift(1)) { while (Lift(-1)) { } } };       // F10: up a floor; from the top it returns to the ground (in the headset use X or the Travel menu instead)
    }
    void OnDisable() { foreach (var a in new[] { stick, up, down, upVr }) if (a != null) { a.Disable(); a.Dispose(); } }

    void Start() { Invoke(nameof(Tune), 0.7f); if (origin && origin.Camera) last = Feet(); }

    void Tune()
    {
        if (!origin) return;
        cc = origin.GetComponent<CharacterController>();
        if (!cc) return;
        cc.radius = radius; cc.skinWidth = 0.02f; cc.slopeLimit = 60f; cc.minMoveDistance = 0f;
        cc.stepOffset = 0.36f;      // never more than twice the radius, so Unity accepts it whatever the body height is
        if (origin.Camera) origin.Camera.nearClipPlane = 0.03f;      // stops walls being cut open when you stand right against them
    }

    public Vector3 Feet() { var c = origin.Camera.transform.position; return new Vector3(c.x, origin.transform.position.y, c.z); }

    void MoveFeet(Vector3 g)
    {
        Vector3 delta = g + Vector3.up * 0.03f - Feet();
        if (!cc) cc = origin.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        origin.transform.position += delta;
        if (cc) cc.enabled = true;
        Physics.SyncTransforms(); last = Feet(); stuck = 0f; progress = 0f;
        if (src && Time.timeScale <= 2f) src.PlayOneShot(whoosh);
    }

    bool Fits(Vector3 g)
    {
        return !Physics.CheckCapsule(g + Vector3.up * (radius + 0.1f), g + Vector3.up * (bodyHeight - radius), radius * 0.9f, ~0, QueryTriggerInteraction.Ignore);
    }

    // In VR the body capsule has to follow the headset: as tall as the player's eye height and standing under their head.
    // Without this the capsule has no height, Unity treats it as switched off, and stick movement and gravity stop working.
    /// set while a door or a lift is carrying the player: nothing else may move or switch on the body
    public static bool hold;
    void FitBodyToHeadset()
    {
        if (hold) return;
        if (!cc) cc = origin.GetComponent<CharacterController>();
        if (!cc) return;
        Vector3 head = origin.CameraInOriginSpacePos;
        float h = Mathf.Clamp(head.y + 0.1f, 0.6f, 2.2f);
        if (Mathf.Abs(cc.height - h) > 0.01f) cc.height = h;
        if (Mathf.Abs(cc.radius - radius) > 0.001f) cc.radius = radius;
        Vector3 c = new Vector3(head.x, h * 0.5f + cc.skinWidth, head.z);
        if ((cc.center - c).sqrMagnitude > 0.0001f) cc.center = c;
        if (!cc.enabled) cc.enabled = true;
    }

    void Update()
    {
        if (!origin || !origin.Camera) return;
        if (game && game.HeadsetActive) FitBodyToHeadset();
        Vector3 intent = debugIntent;
        if (intent == Vector3.zero)
        {
            if (desk && desk.active) intent = desk.MoveIntent;
            else
            {
                Vector2 s = stick.ReadValue<Vector2>();
                bool free = !game || (!game.menuOpen && !HunchIntro.Active && (game.sim.phase == HabPhase.Mission || game.sim.phase == HabPhase.Commissioning));      // the stick is walking, not steering a menu
                if (free && s.sqrMagnitude > 0.25f) intent = Quaternion.Euler(0f, origin.Camera.transform.eulerAngles.y, 0f) * new Vector3(s.x, 0f, s.y);
            }
        }
        else
        {
            if (!cc) cc = origin.GetComponent<CharacterController>();
            if (cc && cc.enabled && cc.gameObject.activeInHierarchy && !hold) cc.Move((debugIntent.normalized * debugSpeed + Vector3.down * 2f) * Time.deltaTime);
        }
        Vector3 f = Feet(); Vector3 d = f - last; d.y = 0f; last = f;
        if (intent.sqrMagnitude > 0.2f && Time.timeScale > 0f && !(game && game.flyMode))
        {
            // progress made in the direction the player is pushing; sliding sideways along a wall does not count
            Vector3 want = new Vector3(intent.x, 0f, intent.z).normalized;
            progress += Vector3.Dot(d, want); stuck += Time.deltaTime;
            if (stuck >= pushSeconds)
            {
                if (progress < 0.45f * pushSeconds && !noAutoPass) TryPass(want);
                stuck = 0f; progress = 0f;
            }
        }
        else { stuck = 0f; progress = 0f; }
    }

    /// Let the player through whatever is blocking them, to the nearest free piece of floor ahead.
    /// Tries straight ahead first, then a little to each side, then accepts any floor at all, so the player is never trapped.
    public bool TryPass(Vector3 dir)
    {
        if (!HunchCheats.Walls) return false;      // walking through walls is a cheat, and it is off
        Vector3 f = Feet(); RaycastHit hit;
        float[] angles = { 0f, 20f, -20f, 40f, -40f };
        for (int strict = 1; strict >= 0; strict--)
            foreach (float ang in angles)
            {
                Vector3 dd = Quaternion.Euler(0f, ang, 0f) * dir;
                for (float d = 0.4f; d <= reach; d += 0.2f)
                {
                    Vector3 p = f + dd * d;
                    // floor may be a step up (0.5 m) or a drop down (2 m)
                    if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out hit, 2.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < 0.6f) continue;
                    if (strict == 1 && !Fits(hit.point)) continue;
                    if (strict == 0 && d < 0.8f) continue;
                    MoveFeet(hit.point); passes++;
                    return true;
                }
            }
        return false;
    }

    /// Go to the next floor above (dir = 1) or below (dir = -1).
    public bool Lift(int dir)
    {
        if (!origin || !origin.Camera) return false;
        Vector3 f = Feet(); RaycastHit hit;
        for (float h = 3.0f; h <= 24f; h += 0.2f)
        {
            // straight above/below first, then a little to each side, so a cluttered spot does not make the lift skip a floor
            for (int ring = 0; ring < 4; ring++)
                for (int k = 0; k < (ring == 0 ? 1 : 8); k++)
                {
                    Vector3 off = ring == 0 ? Vector3.zero : Quaternion.Euler(0f, 45f * k, 0f) * Vector3.forward * (0.7f * ring);
                    Vector3 q = f + off + Vector3.up * (dir > 0 ? h : 0.25f - h);
                    if (!Physics.Raycast(q, Vector3.down, out hit, 0.25f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < 0.6f) continue;
                    if (dir > 0 ? hit.point.y < f.y + 3.0f : hit.point.y > f.y - 3.0f) continue;       // a real storey, not a desk or shelf
                    if (!Fits(hit.point)) continue;
                    MoveFeet(hit.point); lifts++;
                    return true;
                }
        }
        return false;
    }
}
