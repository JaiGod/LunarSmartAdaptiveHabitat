// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/03_Player_Controls/HunchGuide.cs
//  Section : 03_Player_Controls
//  Folder  : How you move and find your way: VR controllers, keyboard/mouse, phone touch controls,
//            doorway help, NavMesh paths, green guide arrows + minimap.
//  Purpose : Finding your way: - GREEN ARROWS on the floor flow from you toward where you are
//            needed (the most urgent alert, or a place you asked for)
//  Contains: HunchGuide
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// Finding your way:
///  - GREEN ARROWS on the floor flow from you toward where you are needed (the most urgent alert, or a place you asked for)
///  - a marker on screen with the distance, or an arrow at the screen edge when the place is behind you
///  - a live floor-plan MINIMAP with a compass and GPS-style position read-out (desktop), and a full base map on N
///  - in VR the direction, distance and floor are written on the dashboard card
///  - RESPAWN: if you fall for a long time or drop out of the world you are put back where you last stood
/// Distances are straight-line. Any wall in the way can be passed by pushing against it (see HunchNavAssist).
[DefaultExecutionOrder(900)]
public class HunchGuide : MonoBehaviour
{
    public bool arrowsOn = true, mapOn = true;
    public int Target { get; private set; } = -1;
    public float Distance { get; private set; }
    public int Respawns { get; private set; }
    public bool BigMap { get { return bigMap; } }

    HabitatGame game; HabitatSim sim; XROrigin origin; HunchAria aria; HunchNavAssist nav; HunchWorldFX fx; HunchDesktopFallback desk;
    const int N = 10; Transform[] chev; Renderer[] chevR; MaterialPropertyBlock mpb;
    Camera mapCam; RenderTexture mapRT; float mapTimer; bool bigMap, ready, cursorWasLocked;
    Vector3 baseCentre; float baseHalf = 200f, baseTop = 120f;
    Vector3 lastSafe; bool haveSafe; float safeTimer, airTime, minY = -40f, lastY;
    int waypoint = -1; float floorDiff;
    // the way you can really walk (HunchPath): the arrows follow it, and the distance is the walking distance
    readonly System.Collections.Generic.List<Vector3> gPath = new System.Collections.Generic.List<Vector3>(); float gPathAt, gPathLen; bool gPathOk, gPathComplete; int gPathFor = -2;
    public float Walk { get; private set; }
    Texture2D tri, white; GUIStyle sSmall, sMid, sBig;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var g = FindFirstObjectByType<HabitatGame>();
        if (g && !g.GetComponent<HunchGuide>()) g.gameObject.AddComponent<HunchGuide>();
    }

    IEnumerator Start()
    {
        game = GetComponent<HabitatGame>(); sim = game.sim;
        yield return new WaitForSeconds(0.9f);
        origin = game.origin; aria = GetComponent<HunchAria>(); nav = GetComponent<HunchNavAssist>(); fx = GetComponent<HunchWorldFX>(); desk = GetComponent<HunchDesktopFallback>();
        if (!origin || !origin.Camera || game.zones.Count == 0) yield break;

        // ---- green arrows (chevrons) built in code
        var mesh = new Mesh { name = "Guide Chevron" };
        mesh.vertices = new[] { new Vector3(-0.45f, 0f, -0.3f), new Vector3(0f, 0f, 0.3f), new Vector3(0f, 0f, 0f), new Vector3(-0.45f, 0f, -0.6f),
                                new Vector3(0.45f, 0f, -0.3f), new Vector3(0.45f, 0f, -0.6f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 1, 4, 5, 1, 5, 2,      // seen from above
                                 2, 1, 0, 3, 2, 0, 5, 4, 1, 2, 5, 1 };    // and from below
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var mat = new Material(game.zones[0].ring.sharedMaterial); mat.SetColor("_BaseColor", new Color(0.25f, 1.6f, 0.4f, 0.9f)); mat.renderQueue = 3100;
        chev = new Transform[N]; chevR = new Renderer[N]; mpb = new MaterialPropertyBlock();
        var root = new GameObject("Guide Arrows").transform;
        for (int i = 0; i < N; i++)
        {
            var go = new GameObject("Arrow " + i); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            go.transform.localScale = Vector3.one * 0.95f; go.SetActive(false);
            chev[i] = go.transform; chevR[i] = r;
        }

        // ---- how big the base is (for the full map) and how low is "fell out of the world"
        var b = new Bounds(game.zones[0].position, Vector3.one); float low = float.MaxValue;
        foreach (var z in game.zones) { b.Encapsulate(z.position); low = Mathf.Min(low, z.position.y); }
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if (r.name.StartsWith("BAKED_") && !r.name.Contains("EARTH") && r.bounds.extents.magnitude < 900f && r.bounds.center.magnitude < 1200f) b.Encapsulate(r.bounds);
        baseCentre = b.center; baseHalf = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) + 15f, 80f, 900f); baseTop = b.max.y;
        minY = low - 25f;

        // ---- map camera: looks straight down, north (+Z) at the top
        if (!Application.isBatchMode)
        {
            mapRT = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32) { name = "Guide Map" };
            var cg = new GameObject("Guide Map Camera"); mapCam = cg.AddComponent<Camera>();
            mapCam.orthographic = true; mapCam.clearFlags = CameraClearFlags.SolidColor; mapCam.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 1f);
            mapCam.targetTexture = mapRT; mapCam.allowMSAA = false; mapCam.allowHDR = false; mapCam.enabled = false; mapCam.depth = -10f;
            cg.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var cd = mapCam.GetUniversalAdditionalCameraData(); if (cd) { cd.renderPostProcessing = false; cd.renderShadows = false; }
        }
        lastY = Feet().y;
        ready = true;
    }

    Vector3 Feet() { if (nav) return nav.Feet(); var c = origin.Camera.transform.position; return new Vector3(c.x, origin.transform.position.y, c.z); }

    /// Ask the arrows to lead to this zone until you arrive (used by "where is the greenhouse" and by clicking the map).
    public string SetWaypoint(int zone)
    {
        if (zone < 0 || zone >= game.zones.Count) return "";
        waypoint = zone;
        Vector3 d = game.zones[zone].position - Feet(); float dy = d.y; d.y = 0f;
        return "Follow the green arrows to " + game.zones[zone].title + ": " + Mathf.RoundToInt(d.magnitude) + " m " + Wind(d) + FloorWords(dy) + ".";
    }

    static string Wind(Vector3 flat)
    {
        if (flat.sqrMagnitude < 0.01f) return "";
        float a = (Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg + 360f) % 360f;
        string[] w = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
        return w[Mathf.RoundToInt(a / 45f) % 8];
    }
    static string FloorWords(float dy) { return dy > 1.2f ? ", " + dy.ToString("F0") + " m up" : (dy < -1.2f ? ", " + (-dy).ToString("F0") + " m down" : ""); }
    /// distance and height to the place: the walking distance when the way is mapped, the straight-line distance otherwise, and how far up or down
    string HowFar() { return Mathf.RoundToInt(Walk) + " m" + (gPathOk && gPathComplete ? " walk" : "") + (floorDiff > 1.2f ? "  \u2191 " + floorDiff.ToString("F0") + " m up" : (floorDiff < -1.2f ? "  \u2193 " + (-floorDiff).ToString("F0") + " m down" : "")); }
    /// the point a given distance along the mapped path, and the direction of travel there
    bool AlongPath(float s, out Vector3 p, out Vector3 dir)
    {
        p = Vector3.zero; dir = Vector3.forward;
        for (int k = 1; k < gPath.Count; k++)
        {
            Vector3 seg = gPath[k] - gPath[k - 1]; float len = seg.magnitude; if (len < 0.001f) continue;
            if (s <= len) { p = gPath[k - 1] + seg * (s / len); dir = seg / len; return true; }
            s -= len;
        }
        return false;
    }

    // ------------------------------------------------------------------ frame
    void Update()
    {
        if (!ready) return;
        Vector3 feet = Feet(); var cam = origin.Camera.transform;
        bool playing = sim.phase == HabPhase.Commissioning || sim.phase == HabPhase.Mission;

        // ---- where to?
        if (waypoint >= 0 && (waypoint == game.NearZone || !playing)) waypoint = -1;
        int t = waypoint;
        if (t < 0 && playing) t = game.UrgentZone();
        if (t < 0 && sim.phase == HabPhase.Mission && !sim.timeFrozen && aria) t = aria.LastZone;
        Target = playing ? t : -1;
        Vector3 d = Vector3.zero;
        if (Target >= 0) { d = game.zones[Target].position - feet; floorDiff = d.y; d.y = 0f; Distance = d.magnitude; }
        bool mapped = HunchPath.I && HunchPath.I.Ready && Target >= 0;
        if (mapped && (Time.unscaledTime >= gPathAt || Target != gPathFor)) { gPathAt = Time.unscaledTime + 0.4f; gPathFor = Target; gPathOk = HunchPath.I.Find(feet, game.zones[Target].position, gPath, out gPathLen, out gPathComplete); }
        if (!mapped) gPathOk = false;
        Walk = gPathOk && gPathComplete ? gPathLen : Distance;
        bool arrived = Target >= 0 && Target == game.NearZone;

        // ---- green arrows on the floor
        bool show = arrowsOn && Target >= 0 && !arrived && Distance > 3f && !game.menuOpen;
        if (show)
        {
            Vector3 dir = d.normalized; const float spacing = 1.7f; float flow = (Time.unscaledTime * 2.4f) % spacing, span = N * spacing;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            for (int i = 0; i < N; i++)
            {
                float s = 2.3f + i * spacing + flow; Vector3 p; RaycastHit hit;
                if (gPathOk && gPath.Count >= 2)
                {
                    // along the mapped path: round the corners, and tilted up or down on stairs, so each arrow points the way you move next
                    Vector3 pd; if (s > gPathLen - 0.8f || !AlongPath(s, out p, out pd)) { if (chev[i].gameObject.activeSelf) chev[i].gameObject.SetActive(false); continue; }
                    chev[i].SetPositionAndRotation(p + Vector3.up * 0.14f, Quaternion.LookRotation(pd, Vector3.up));
                }
                else
                {
                    if (s > Distance - 1f) { if (chev[i].gameObject.activeSelf) chev[i].gameObject.SetActive(false); continue; }
                    p = feet + dir * s;
                    p.y = Physics.Raycast(p + Vector3.up * 1.3f, Vector3.down, out hit, 3.3f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y + 0.14f : feet.y + 0.14f;
                    chev[i].SetPositionAndRotation(p, rot);
                }
                float a = Mathf.Clamp01((s - 2.2f) / 1.5f) * Mathf.Clamp01((span + 2.3f - s) / 4f);
                mpb.SetColor("_BaseColor", new Color(0.25f, 1.6f, 0.4f, 0.9f * a)); chevR[i].SetPropertyBlock(mpb);
                if (!chev[i].gameObject.activeSelf) chev[i].gameObject.SetActive(true);
            }
        }
        else for (int i = 0; i < N; i++) if (chev[i].gameObject.activeSelf) chev[i].gameObject.SetActive(false);

        // ---- VR: the same information as a line on the dashboard card
        game.navLine = Target < 0 ? "" : (arrived ? "You are here: " + game.zones[Target].title
            : "Go to " + game.zones[Target].title + ": " + Mathf.RoundToInt(Walk) + " m " + (gPathOk && gPathComplete ? "walk, " : "") + Wind(d) + FloorWords(floorDiff) + (Mathf.Abs(floorDiff) > 2.5f ? " (press X to go there)" : ""));

        // ---- respawn after a fall
        bool grounded = false; RaycastHit g;
        if (Physics.Raycast(feet + Vector3.up * 0.6f, Vector3.down, out g, 1.3f, ~0, QueryTriggerInteraction.Ignore) && g.normal.y > 0.6f) grounded = true;
        safeTimer -= Time.unscaledDeltaTime;
        if (grounded && !game.flyMode && safeTimer <= 0f && feet.y > minY + 5f) { safeTimer = 0.4f; lastSafe = g.point; haveSafe = true; }
        bool falling = !grounded && !game.flyMode && !game.menuOpen && feet.y < lastY - 0.0005f;
        airTime = falling ? airTime + Time.deltaTime : (grounded || game.flyMode ? 0f : airTime);
        lastY = feet.y;
        if (playing && (feet.y < minY || airTime > 5f)) Respawn();

        // ---- map
        var kb = Keyboard.current;
        if (kb != null && kb.nKey.wasPressedThisFrame && !game.menuOpen && playing && !game.HeadsetActive) SetBigMap(!bigMap);
        if (bigMap && (game.menuOpen || !playing)) SetBigMap(false);
        if (bigMap && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ClickMap();
        UpdateMapCamera(feet);
    }

    void Respawn()
    {
        airTime = 0f; Respawns++;
        if (haveSafe)
        {
            var cc = origin.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
            if (game.HeadsetActive) origin.MoveCameraToWorldLocation(lastSafe + Vector3.up * (Mathf.Max(0.2f, origin.CameraInOriginSpaceHeight) + 0.1f));
            else origin.transform.position = lastSafe + Vector3.up * 0.1f;
            if (cc) cc.enabled = true; Physics.SyncTransforms();
        }
        else game.GoTo(game.Current);
        lastY = Feet().y;
        sim.Say("ARIA: You fell, so I put you back where you last stood.");
        game.Chime(0.5f); game.Buzz(0.6f, 0.2f);
    }

    // ------------------------------------------------------------------ map
    void SetBigMap(bool on)
    {
        if (on == bigMap) return;
        bigMap = on; game.uiHasMouse = on; mapTimer = 0f;
        if (on) { cursorWasLocked = Cursor.lockState == CursorLockMode.Locked; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        else if (cursorWasLocked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    float mapHalf = 30f; Vector3 mapCentre;

    void UpdateMapCamera(Vector3 feet)
    {
        if (!mapCam) return;
        bool want = (mapOn || bigMap) && !game.HeadsetActive && !game.hideHud && !game.menuOpen && (sim.phase == HabPhase.Commissioning || sim.phase == HabPhase.Mission);
        mapTimer -= Time.unscaledDeltaTime;
        if (!want || mapTimer > 0f) { if (mapCam.enabled) mapCam.enabled = false; return; }
        mapTimer = bigMap ? 0.25f : 0.1f;                                   // the map is redrawn a few times a second, not every frame
        if (bigMap)
        {
            mapHalf = baseHalf; mapCentre = baseCentre;
            mapCam.transform.position = new Vector3(baseCentre.x, baseTop + 40f, baseCentre.z); mapCam.nearClipPlane = 1f; mapCam.farClipPlane = baseTop + 400f;
        }
        else
        {
            bool outdoors = fx && fx.Outside;                                // indoors: a slice just above your head shows the floor plan. Outdoors: a view from high up.
            mapHalf = outdoors ? 120f : 30f; mapCentre = feet;
            float h = 150f;
            if (!outdoors) { RaycastHit up; float room = Physics.Raycast(feet + Vector3.up * 1.4f, Vector3.up, out up, 4f, ~0, QueryTriggerInteraction.Ignore) ? up.distance : 4f; h = Mathf.Min(3.1f, 1.25f + room); }      // stay under the ceiling
            mapCam.transform.position = new Vector3(feet.x, feet.y + h, feet.z); mapCam.nearClipPlane = 0.05f; mapCam.farClipPlane = outdoors ? 600f : 9f;
        }
        mapCam.orthographicSize = mapHalf;
        mapCam.enabled = true;                                               // renders this frame, switched off again on the next
    }

    Rect BigRect() { float u = Screen.height / 1080f, s = Mathf.Min(Screen.width - 520f * u, Screen.height - 150f * u); return new Rect((Screen.width - s) * 0.5f, 70f * u, s, s); }
    Rect MiniRect() { float u = Screen.height / 1080f; return new Rect(Screen.width - 264f * u, Screen.height - 468f * u, 250f * u, 250f * u); }
    Vector2 MapPoint(Rect r, Vector3 world) { return new Vector2(r.center.x + (world.x - mapCentre.x) / mapHalf * r.width * 0.5f, r.center.y - (world.z - mapCentre.z) / mapHalf * r.height * 0.5f); }

    void ClickMap()
    {
        Vector2 m = Mouse.current.position.ReadValue(); m.y = Screen.height - m.y; Rect r = BigRect();
        if (!r.Contains(m)) return;
        int best = -1; float bd = 34f * Screen.height / 1080f;
        for (int i = 0; i < game.zones.Count; i++) { float dd = (MapPoint(r, game.zones[i].position) - m).magnitude; if (dd < bd) { bd = dd; best = i; } }
        if (best >= 0) { sim.Say("ARIA: " + SetWaypoint(best)); SetBigMap(false); }
    }

    // ------------------------------------------------------------------ desktop drawing
    void Box(Rect r, Color c) { HunchUi.Round(r, c); }
    void Tri(Vector2 at, float size, float angle, Color c)
    {
        var m = GUI.matrix; GUIUtility.RotateAroundPivot(angle, at);
        GUI.color = c; GUI.DrawTexture(new Rect(at.x - size * 0.5f, at.y - size * 0.5f, size, size), tri); GUI.color = Color.white; GUI.matrix = m;
    }
    void Line(Vector2 a, Vector2 b, float w, Color c)
    {
        var m = GUI.matrix; Vector2 d = b - a; GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
        Box(new Rect(a.x, a.y - w * 0.5f, d.magnitude, w), c); GUI.matrix = m;
    }

    void OnGUI()
    {
        if (!ready || game.HeadsetActive || game.hideHud || game.menuOpen || Event.current.type != EventType.Repaint) return;
        if (sim.phase != HabPhase.Commissioning && sim.phase != HabPhase.Mission) return;
        float u = Screen.height / 1080f, W = Screen.width, H = Screen.height;
        if (tri == null)
        {
            white = Texture2D.whiteTexture;
            tri = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) px[y * 64 + x] = new Color32(255, 255, 255, (byte)(Mathf.Abs(y - 31.5f) < (63 - x) * 0.5f && x > 2 ? 255 : 0));     // points to the right
            tri.SetPixels32(px); tri.Apply();
        }
        if (sSmall == null || Mathf.Abs(sSmall.fontSize - HunchLook.FsF(15f * u)) > 1f)
        {
            sSmall = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(15f * u), richText = true, wordWrap = true };
            sMid = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(20f * u), richText = true, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            sBig = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(26f * u), richText = true, fontStyle = FontStyle.Bold };
            foreach (var s in new[] { sSmall, sMid, sBig }) s.normal.textColor = Color.white;
        }
        var cam = origin.Camera; Vector3 feet = Feet(); float yaw = cam.transform.eulerAngles.y;
        Color green = new Color(0.35f, 1f, 0.5f, 1f);
        bool arrived = Target >= 0 && Target == game.NearZone;

        // ---- marker on the target, or an arrow at the screen edge pointing to it
        if (arrowsOn && Target >= 0 && !arrived && !bigMap)
        {
            Vector3 sp = cam.WorldToScreenPoint(game.zones[Target].position + Vector3.up * 2.6f);
            float x = sp.x, y = H - sp.y;
            float mx = Mathf.Min(510f * u, W * 0.36f); Rect safe = new Rect(mx, 140f * u, W - 2f * mx, H - 140f * u - 240f * u);     // the part of the screen not covered by panels
            string label = HowFar();
            if (sp.z > 0f && safe.Contains(new Vector2(x, y)))
            {
                Tri(new Vector2(x, y - 14f * u), 26f * u, 90f, green);
                GUI.Label(new Rect(x - 120f * u, y - 62f * u, 240f * u, 30f * u), "<color=#7CFC9A>" + label + "</color>", sMid);
            }
            else
            {
                Vector2 c = safe.center, v = new Vector2(x - c.x, y - c.y); if (sp.z <= 0f) v = -v;
                if (v.sqrMagnitude < 1f) v = Vector2.up; v.Normalize();
                float k = Mathf.Min(safe.width * 0.5f / Mathf.Max(0.001f, Mathf.Abs(v.x)), safe.height * 0.5f / Mathf.Max(0.001f, Mathf.Abs(v.y)));
                Vector2 p = c + v * k;
                Tri(p, 44f * u, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg, green);
                Vector2 lp = p - v * 46f * u;
                GUI.Label(new Rect(lp.x - 120f * u, lp.y - 15f * u, 240f * u, 30f * u), "<color=#7CFC9A>" + label + "</color>", sMid);
            }
        }

        if (bigMap) { DrawBigMap(u, feet, yaw, green); return; }
        if (!mapOn || mapRT == null) return;

        // ---- minimap with compass, and the GPS read-out above it
        Rect M = MiniRect();
        Box(new Rect(M.x - 3f * u, M.y - 3f * u, M.width + 6f * u, M.height + 6f * u), new Color(0.6f, 0.85f, 1f, 0.55f));
        GUI.DrawTexture(M, mapRT, ScaleMode.StretchToFill, false);
        for (int i = 0; i < game.zones.Count; i++)
        {
            var z = game.zones[i]; Vector2 p = MapPoint(M, z.position);
            bool isT = i == Target, alert = false; foreach (var a in sim.alerts) if (a.zoneId == z.id) alert = true;
            bool inside = M.Contains(p);
            if (!inside && !isT && !alert) continue;
            p.x = Mathf.Clamp(p.x, M.x + 6f * u, M.xMax - 6f * u); p.y = Mathf.Clamp(p.y, M.y + 6f * u, M.yMax - 6f * u);
            Color c = isT ? green : (alert ? new Color(1f, 0.35f, 0.3f, 1f) : new Color(0.6f, 0.85f, 1f, Mathf.Abs(z.position.y - feet.y) < 3f ? 0.95f : 0.4f));
            float s = (isT || alert ? 11f : 7f) * u; Box(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), c);
            if (isT || alert) GUI.Label(new Rect(p.x + 6f * u, p.y - 11f * u, 60f * u, 22f * u), "<color=#" + ColorUtility.ToHtmlStringRGB(c) + "><b>" + (i + 1) + "</b></color>", sSmall);
        }
        if (Target >= 0 && !arrived) { Vector2 tp = MapPoint(M, game.zones[Target].position); tp.x = Mathf.Clamp(tp.x, M.x, M.xMax); tp.y = Mathf.Clamp(tp.y, M.y, M.yMax); Line(M.center, tp, 2f * u, new Color(0.35f, 1f, 0.5f, 0.7f)); }
        Tri(M.center, 22f * u, yaw - 90f, Color.white);
        var cs = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(M.center.x - 15f * u, M.y + 1f * u, 30f * u, 20f * u), "<color=#FF8A7A>N</color>", cs);
        GUI.Label(new Rect(M.center.x - 15f * u, M.yMax - 21f * u, 30f * u, 20f * u), "S", cs);
        GUI.Label(new Rect(M.x + 1f * u, M.center.y - 10f * u, 22f * u, 20f * u), "W", cs);
        GUI.Label(new Rect(M.xMax - 23f * u, M.center.y - 10f * u, 22f * u, 20f * u), "E", cs);

        if (game.simpleHud)
        {
            // simple screen: just where to go
            if (Target >= 0)
            {
                Rect G2 = new Rect(M.x - 3f * u, M.y - 36f * u, M.width + 6f * u, 30f * u); Box(G2, new Color(0.02f, 0.04f, 0.09f, 0.8f));
                GUI.Label(new Rect(G2.x + 8f * u, G2.y + 4f * u, G2.width - 12f * u, G2.height - 6f * u), "<color=#7CFC9A>" + (arrived ? "You are here: " : "To ") + game.zones[Target].title + (arrived ? "" : "  " + HowFar()) + "</color>", new GUIStyle(sSmall) { wordWrap = false, clipping = TextClipping.Clip });
            }
            return;
        }
        string[] wind = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        var sb = new StringBuilder();
        sb.Append("<b>GPS</b>  ").Append(Mathf.RoundToInt(feet.x)).Append(" m E   ").Append(Mathf.RoundToInt(feet.z)).Append(" m N   height ").Append(feet.y.ToString("F0")).Append(" m\n");
        sb.Append("Heading ").Append(Mathf.RoundToInt(yaw).ToString("000")).Append("° ").Append(wind[Mathf.RoundToInt(yaw / 45f) % 8]);
        sb.Append(fx && fx.Outside ? "   outside" : "   inside").Append("\n");
        if (Target >= 0) sb.Append("<color=#7CFC9A>").Append(arrived ? "You are here: " : "To ").Append(game.zones[Target].title).Append(arrived ? "" : "  " + HowFar()).Append("</color>");
        else sb.Append("<color=#C8D4E8>No destination. Ask ARIA \"where is the greenhouse\".</color>");
        Rect G = new Rect(M.x - 3f * u, M.y - 84f * u, M.width + 6f * u, 78f * u); Box(G, new Color(0.02f, 0.04f, 0.09f, 0.8f));
        GUI.Label(new Rect(G.x + 8f * u, G.y + 4f * u, G.width - 12f * u, G.height - 6f * u), sb.ToString(), sSmall);
    }

    void DrawBigMap(float u, Vector3 feet, float yaw, Color green)
    {
        Rect r = BigRect();
        Box(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.01f, 0.02f, 0.05f, 0.7f));
        Box(new Rect(r.x - 3f * u, r.y - 3f * u, r.width + 6f * u, r.height + 6f * u), new Color(0.6f, 0.85f, 1f, 0.6f));
        if (mapRT) GUI.DrawTexture(r, mapRT, ScaleMode.StretchToFill, false);
        GUI.Label(new Rect(r.x, 22f * u, r.width, 40f * u), "BASE MAP   <size=" + sSmall.fontSize + "><color=#9AD8FF>click a zone to get green arrows to it     N: close</color></size>", sBig);
        Vector2 me = MapPoint(r, feet);
        if (Target >= 0) Line(me, MapPoint(r, game.zones[Target].position), 3f * u, new Color(0.35f, 1f, 0.5f, 0.85f));
        var list = new StringBuilder("<b>Zones</b>\n");
        for (int i = 0; i < game.zones.Count; i++)
        {
            var z = game.zones[i]; Vector2 p = MapPoint(r, z.position);
            bool isT = i == Target, alert = false; foreach (var a in sim.alerts) if (a.zoneId == z.id) alert = true;
            Color c = isT ? green : (alert ? new Color(1f, 0.35f, 0.3f, 1f) : new Color(0.6f, 0.85f, 1f, 1f));
            float s = 12f * u; Box(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), c);
            string hex = ColorUtility.ToHtmlStringRGB(c);
            GUI.Label(new Rect(p.x + 8f * u, p.y - 12f * u, 50f * u, 24f * u), "<color=#" + hex + "><b>" + (i + 1) + "</b></color>", sSmall);
            Vector3 d = z.position - feet; d.y = 0f;
            list.Append("<color=#").Append(hex).Append(">").Append(i + 1).Append("  ").Append(z.title).Append("</color>  <color=#8A96AA>").Append(Mathf.RoundToInt(d.magnitude)).Append(" m</color>\n");
        }
        Tri(me, 26f * u, yaw - 90f, Color.white);
        var cs = new GUIStyle(sMid);
        GUI.Label(new Rect(r.center.x - 20f * u, r.y + 2f * u, 40f * u, 26f * u), "<color=#FF8A7A>N</color>", cs);
        float lx = r.xMax + 18f * u, lw = Screen.width - lx - 10f * u;
        if (lw > 150f * u) { Box(new Rect(lx - 6f * u, r.y, lw, 30f * u + 24f * u * game.zones.Count), new Color(0.02f, 0.04f, 0.09f, 0.8f)); GUI.Label(new Rect(lx, r.y + 4f * u, lw - 10f * u, r.height), list.ToString(), new GUIStyle(sSmall) { wordWrap = false }); }
        GUI.Label(new Rect(r.x, r.yMax + 6f * u, r.width, 26f * u), "<color=#C8D4E8>White arrow: you.  Green: where you are needed.  Red: alert.  Scale: the map is " + Mathf.RoundToInt(baseHalf * 2f) + " m across.</color>", sSmall);
    }

    // for the development test helper
    public string ProbeInfo() { return "target=" + Target + " walk=" + Walk.ToString("F0") + " mapped=" + gPathOk + "/" + gPathComplete + " corners=" + gPath.Count + " dist=" + Distance.ToString("F0") + " floor=" + floorDiff.ToString("F1") + " waypoint=" + waypoint + " respawns=" + Respawns + " safe=" + (haveSafe ? lastSafe.ToString("F1") : "none") + " minY=" + minY.ToString("F0") + " base=" + baseCentre.ToString("F0") + " half=" + baseHalf.ToString("F0") + " big=" + bigMap + " nav=" + game.navLine; }
    public void ProbeBigMap() { SetBigMap(!bigMap); }
    public string ProbeWaypointGreenhouse() { return SetWaypoint(game.ZoneIndex("greenhouse")); }
}
