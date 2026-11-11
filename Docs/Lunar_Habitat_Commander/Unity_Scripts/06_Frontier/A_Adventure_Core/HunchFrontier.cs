// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/A_Adventure_Core/HunchFrontier.cs
//  Section : 06_Frontier/A_Adventure_Core
//  Folder  : Moon Frontier adventure layer: mining, aliens, tools, easy mode, survival + repair
//            mini-games, Autopilot, the 3D models from the Drive collection.
//  Purpose : Marks things the tools and the hand can use. kind: 1 alien, 2 mineral node, 3 placed
//            block, 10 rover, 11 light button, 12 fan button, 13 moon rock.
//  Contains: HunchTarget, HunchFrontier, Wire, Vent, Console, Node, Pickup, Rover
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
#pragma warning disable 0414, 0219
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Marks things the tools and the hand can use. kind: 1 alien, 2 mineral node, 3 placed block, 10 rover, 11 light button, 12 fan button, 13 moon rock.
public class HunchTarget : MonoBehaviour { public int kind, index; }

/// "Moon Frontier": the adventure layer on top of the habitat simulator.
///  - a mining field outside with water ice, ilmenite ore and helium-3, a mining drill, and a backpack to carry and use what you find
///  - friendly-looking aliens beamed down by a UFO; zap them and they are beamed home (nobody is hurt) and leave a crystal
///  - tools you choose: Zapper, Freeze ray, Plasma launcher, Mining drill, Block builder (dig regolith, place and break blocks)
///  - upgrades bought with what you mined
///  - power grid: light switch, a glowing cable network from the power plant to every zone, ventilation fans
///  - sky: Sun disc and glow that follow the real Sun direction, shooting stars; rovers driving round the field
///  - movement: run and jetpack fly in VR (the desktop already had Shift and F)
/// Everything is made in code from simple shapes, so it needs no extra assets.
public partial class HunchFrontier : MonoBehaviour
{
    public const int Hands = 0, Zapper = 1, Freeze = 2, Plasma = 3, Drill = 4, Builder = 5;
    public static readonly string[] ToolNames = { "Hands", "Zapper", "Freeze ray", "Plasma launcher", "Mining drill", "Block builder", "Plasma repeater", "Laser cannon", "Star minigun", "Comet sword", "Ninja stars" };
    public const int Ice = 0, Ore = 1, He3 = 2, Crystal = 3, Block = 4, Cell = 5;
    public static readonly string[] ItemNames = { "Water ice", "Ilmenite ore", "Helium-3", "Moon crystal", "Regolith block", "Power cell" };

    public int tool;
    public bool runOn, lightsOn = true, wiresOn = true, ventOn = true, aliensOn = true;
    public int sentHome, minedCount, placedCount;
    public int[] bag = { 0, 0, 0, 0, 12, 1 };
    public int zapLv = 1, drillLv = 1, packLv = 1, jetLv = 1, rigCount; public bool plasmaUnlocked, solarUp;
    [NonSerialized] public float probeHold;

    HabitatGame game; HabitatSim sim; HunchDesktopFallback desk; HunchNavAssist nav; HunchWorldFX fx; HunchKidFeatures kid; HunchMenu menu; XROrigin origin;
    Transform cam, root, rightCtrl; bool ready, fieldOk; System.Random rng = new System.Random(77);
    Vector3 fieldC; float baseY, lowY, skyY; const int GroundMask = ~(1 << 2);
    string note = ""; float noteUntil; bool ventWarned, outsideTip;

    // ------------------------------------------------------------------ attach
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var g = FindFirstObjectByType<HabitatGame>();
        if (g && !g.GetComponent<HunchFrontier>() && !Application.isBatchMode) g.gameObject.AddComponent<HunchFrontier>();
    }

    InputAction fireVr, jumpVr, toolVr, runVr; float lastA = -9f; bool wasHeld;

    void OnEnable()
    {
        fireVr = new InputAction("fire", InputActionType.Value, "<XRController>{RightHand}/trigger"); fireVr.Enable();
        jumpVr = new InputAction("jet", InputActionType.Button, "<XRController>{RightHand}/primaryButton"); jumpVr.Enable();
        toolVr = new InputAction("tool", InputActionType.Button, "<XRController>{RightHand}/secondaryButton"); toolVr.Enable();
        runVr = new InputAction("run", InputActionType.Button, "<XRController>{LeftHand}/thumbstickClicked"); runVr.Enable();
        jumpVr.performed += _ =>
        {
            if (!ready || !game.HeadsetActive || game.menuOpen) return;
            lastA = Time.unscaledTime;      // A only jumps. The jetpack is a dashboard button, so it can never switch on by accident.
        };
        runVr.performed += _ => { if (ready && game.HeadsetActive && !game.menuOpen) { runOn = !runOn; Note(runOn ? "Run: ON" : "Run: OFF"); game.Buzz(0.4f, 0.06f); } };
    }
    public float VrMoveMul { get { return (runOn ? 2f : 1f) * (ride != null ? 2.2f : 1f) * (1f + 0.3f * (jetLv - 1)) * (mapNow == 8 ? 4f + starBoost * 0.3f : 1f); } }      // walking speed in the headset (HunchVr)
    public void VrNextTool() { if (ready && game.HeadsetActive && !game.menuOpen && Playing && !autoOn) NextTool(); }      // the B button (HunchVr)
    void OnDisable() { foreach (var a in new[] { fireVr, jumpVr, toolVr, runVr }) if (a != null) { a.Disable(); a.Dispose(); } }

    bool Playing { get { return sim.phase == HabPhase.Mission || sim.phase == HabPhase.Commissioning; } }
    bool RealMission { get { return sim.phase == HabPhase.Mission && !sim.timeFrozen; } }
    float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    Vector3 Feet() { return nav && origin && origin.Camera ? nav.Feet() : origin.transform.position; }

    IEnumerator Start()
    {
        game = GetComponent<HabitatGame>(); if (!game) yield break;
        sim = game.sim;
        yield return new WaitForSeconds(1.0f);
        desk = GetComponent<HunchDesktopFallback>(); nav = GetComponent<HunchNavAssist>(); fx = GetComponent<HunchWorldFX>(); kid = GetComponent<HunchKidFeatures>(); menu = GetComponent<HunchMenu>();
        origin = game.origin;
        if (!origin || !origin.Camera || game.zones.Count == 0) yield break;
        cam = origin.Camera.transform;
        foreach (var t in origin.GetComponentsInChildren<Transform>(true)) if (t.name == "Right Controller") rightCtrl = t;
        root = new GameObject("HUNCH Frontier").transform;
        SetupMaterials(); SetupAudio();
        HubClearEarly();
        try { FixFloorsAndTrees(); } catch (Exception e) { Debug.LogError("[HUNCH] floors and trees: " + e); }
        FindField();
        BuildSky(); BuildGrid(); BuildToolModel(); BuildShots();
        BuildConsoles(); BuildGunModels();
        if (fieldOk) { BuildBeacon(); BuildNodes(); BuildAliens(); BuildUfo(); BuildRovers(); BuildCrates(); BuildRocks(); }
        RegisterMenu();
        BuildToolDesigns(); BuildQuickBar(); BuildAch(); BuildKidSky(); RegisterMenu2(); BuildBatch3();
        game.toolInUse = () => ready && Playing && (tool != Hands || focus != null || heldRock != null || smartTool != 0 || mapNow != 0 || readOpen >= 0);
        game.uiHover = () => ready && QuickHovered() != null;
        ready = true;
        Debug.Log("[HUNCH] frontier ready, field " + (fieldOk ? fieldC.ToString("F0") : "not found"));
    }

    // ------------------------------------------------------------------ materials, shapes, sound
    Material litSrc, solidSrc, clearSrc; readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    void SetupMaterials()
    {
        clearSrc = game.zones[0].ring ? game.zones[0].ring.sharedMaterial : null;
        solidSrc = game.crewStatus != null && game.crewStatus.Length > 0 && game.crewStatus[0] ? game.crewStatus[0].sharedMaterial : clearSrc;
        if (kid && kid.buddy) { var b = kid.buddy.Find("Body"); if (b && b.GetComponent<Renderer>()) litSrc = b.GetComponent<Renderer>().sharedMaterial; }
        if (!litSrc) litSrc = solidSrc;
    }
    Material Make(Material src, string kind, Color c)
    {
        string key = kind + c.ToString("F2"); Material m;
        if (mats.TryGetValue(key, out m)) return m;
        m = new Material(src); m.SetColor("_BaseColor", c);
        if (kind == "lit") Surface(m, c);
        mats[key] = m; return m;
    }
    // ---- surface detail for every shaded material: a fine grain in the colour, a matching bump map, and metal / paint / rock response chosen from the colour
    static Texture2D grainTex, grainNormal;
    static void BuildGrain()
    {
        if (grainTex) return; const int N = 256; var h = new float[N * N]; var r = new System.Random(5);
        for (int oct = 0; oct < 5; oct++)
        {
            int cells = 4 << oct; var g = new float[cells * cells]; for (int i = 0; i < g.Length; i++) g[i] = (float)r.NextDouble(); float amp = 0.5f / (1 << oct);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float fx = x * (float)cells / N, fy = y * (float)cells / N; int x0 = (int)fx, y0 = (int)fy, x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells; float tx = fx - x0, ty = fy - y0; tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
                h[y * N + x] += Mathf.Lerp(Mathf.Lerp(g[y0 * cells + x0], g[y0 * cells + x1], tx), Mathf.Lerp(g[y1 * cells + x0], g[y1 * cells + x1], tx), ty) * amp;
            }
        }
        var col = new Color32[N * N]; var nor = new Color32[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float v = h[y * N + x]; byte b = (byte)Mathf.Clamp(Mathf.RoundToInt((0.90f + 0.11f * v) * 255f), 0, 255); col[y * N + x] = new Color32(b, b, b, 255);
            float dx = h[y * N + (x + 1) % N] - h[y * N + (x + N - 1) % N], dy = h[((y + 1) % N) * N + x] - h[((y + N - 1) % N) * N + x]; var n = new Vector3(-dx * 3.5f, -dy * 3.5f, 1f).normalized;
            nor[y * N + x] = new Color32((byte)(n.x * 127.5f + 127.5f), (byte)(n.y * 127.5f + 127.5f), (byte)(n.z * 127.5f + 127.5f), 255);
        }
        grainTex = new Texture2D(N, N, TextureFormat.RGBA32, true, false); grainTex.SetPixels32(col); grainTex.wrapMode = TextureWrapMode.Repeat; grainTex.anisoLevel = 8; grainTex.Apply(true);
        grainNormal = new Texture2D(N, N, TextureFormat.RGBA32, true, true); grainNormal.SetPixels32(nor); grainNormal.wrapMode = TextureWrapMode.Repeat; grainNormal.anisoLevel = 8; grainNormal.Apply(true);
    }
    static void Surface(Material m, Color c)
    {
        BuildGrain();
        if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", grainTex); m.SetTextureScale("_BaseMap", new Vector2(2f, 2f)); }
        if (m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", grainNormal); m.SetFloat("_BumpScale", 0.45f); m.EnableKeyword("_NORMALMAP"); }
        float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b)), sat = mx > 0.001f ? (mx - mn) / mx : 0f, metal, smooth;
        if (c.r > 0.8f && c.g > 0.55f && c.g < 0.88f && c.b < 0.45f) { metal = 0.6f; smooth = 0.72f; }      // gold
        else if (sat < 0.18f && mx > 0.5f && mx < 0.9f) { metal = 0.45f; smooth = 0.58f; }                   // steel and silver
        else if (sat < 0.18f && mx >= 0.9f) { metal = 0f; smooth = 0.42f; }                                 // white suit cloth and painted panels
        else if (sat < 0.2f) { metal = 0.08f; smooth = 0.22f; }                                             // rock, regolith, rubber
        else { metal = 0f; smooth = 0.5f; }                                                                 // paint and plastic
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
    }
    Material Lit(Color c) { return Make(litSrc, "lit", c); }           // shaded by the Sun and the lamps
    Material Glow(Color c) { return Make(solidSrc, "glow", c); }       // unlit; bright colours bloom
    Material Clear(Color c) { return Make(clearSrc, "clear", c); }     // see-through

    GameObject Prim(PrimitiveType type, Transform parent, Vector3 lp, Vector3 ls, Material m, bool keepCollider = false)
    {
        var g = GameObject.CreatePrimitive(type);
        if (!keepCollider) { var c = g.GetComponent<Collider>(); if (c) { c.enabled = false; Destroy(c); } }
        g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = ls;
        var r = g.GetComponent<Renderer>(); r.sharedMaterial = m;
        return g;
    }
    LineRenderer Line(string name, Material m, float width, Transform parent)
    {
        var g = new GameObject(name); g.transform.SetParent(parent ? parent : root, false);
        var lr = g.AddComponent<LineRenderer>(); lr.sharedMaterial = m; lr.widthMultiplier = width; lr.positionCount = 2; lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false; lr.numCapVertices = 2; lr.numCornerVertices = 2;
        return lr;
    }
    static void SetLayer(GameObject g, int layer) { foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer; }

    AudioSource src, drillSrc; AudioClip cZap, cFreeze, cBoom, cPick, cThud, cPop, cBoop;

    static AudioClip Sweep(string name, float f0, float f1, float seconds, float noise)
    {
        int rate = 44100, n = Mathf.RoundToInt(rate * seconds); var d = new float[n]; var r = new System.Random(3); double ph = 0; float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n, f = Mathf.Lerp(f0, f1, t), env = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.6f));
            ph += 2.0 * Math.PI * f / rate; lp = Mathf.Lerp(lp, (float)r.NextDouble() * 2f - 1f, 0.2f);
            d[i] = (Mathf.Sin((float)ph) * (1f - noise) + lp * noise) * env * 0.5f;
        }
        var c = AudioClip.Create(name, n, 1, rate, false); c.SetData(d, 0); return c;
    }

    void SetupAudio()
    {
        src = gameObject.AddComponent<AudioSource>(); src.spatialBlend = 0f; src.playOnAwake = false; src.volume = 0.5f; src.bypassListenerEffects = true;      // heard through the suit
        cZap = Sweep("zap", 1500f, 420f, 0.13f, 0.1f); cFreeze = Sweep("freeze", 500f, 1700f, 0.22f, 0.15f); cBoom = Sweep("boom", 160f, 40f, 0.5f, 0.75f);
        cPick = Sweep("pick", 900f, 1500f, 0.12f, 0f); cThud = Sweep("thud", 170f, 70f, 0.12f, 0.3f); cPop = Sweep("pop", 300f, 1100f, 0.18f, 0.05f); cBoop = Sweep("boop", 260f, 180f, 0.2f, 0f);
        drillSrc = gameObject.AddComponent<AudioSource>(); drillSrc.spatialBlend = 0f; drillSrc.playOnAwake = false; drillSrc.loop = true; drillSrc.volume = 0f; drillSrc.bypassListenerEffects = true;
        int n = 22050; var d = new float[n]; var r = new System.Random(9); float lp = 0f;
        for (int i = 0; i < n; i++) { float saw = ((i * 96f / 44100f) % 1f) * 2f - 1f; lp = Mathf.Lerp(lp, (float)r.NextDouble() * 2f - 1f, 0.3f); d[i] = saw * 0.3f + lp * 0.25f; }
        var clip = AudioClip.Create("drill", n, 1, 44100, false); clip.SetData(d, 0); drillSrc.clip = clip;
    }
    void Play(AudioClip c, float v) { if (src && c && (!fx || fx.sfxOn) && Time.timeScale <= 2f) src.PlayOneShot(c, v); }
    void Note(string text) { note = text; noteUntil = Time.unscaledTime + 3.5f; }
    // plays an effect from a newly imported pack when it is there, otherwise the older stand-in
    GameObject Fx2(string name, string fallback, Vector3 p, float scale, float fallbackScale, float seconds)
    {
        if (HunchFx.Has(name)) return HunchFx.Spawn(name, p, scale * packScale, seconds);
        return fallback != null ? HunchFx.Spawn(fallback, p, fallbackScale, seconds) : null;
    }
    public float packScale = 0.6f;

    // ------------------------------------------------------------------ finding open ground outside
    bool GroundAt(float x, float z, out Vector3 p)
    {
        p = Vector3.zero; RaycastHit h;
        if (!Physics.Raycast(new Vector3(x, skyY, z), Vector3.down, out h, 4000f, GroundMask, QueryTriggerInteraction.Ignore)) return false;
        if (h.normal.y < 0.8f || h.point.y > baseY + 2.5f || h.point.y < Mathf.Max(baseY - 15f, lowY - 18f)) return false;
        if (h.collider.GetComponentInParent<HunchTarget>()) return false;
        p = h.point; return true;
    }
    bool OpenAt(float x, float z, float r, out Vector3 p)
    {
        if (!GroundAt(x, z, out p)) return false;
        Vector3 q;
        for (int k = 0; k < 8; k++)
        {
            float a = k * 45f * Mathf.Deg2Rad;
            if (!GroundAt(x + Mathf.Sin(a) * r, z + Mathf.Cos(a) * r, out q) || Mathf.Abs(q.y - p.y) > 3f) return false;
        }
        return true;
    }
    bool RandomGround(float minR, float maxR, out Vector3 p)
    {
        for (int i = 0; i < 30; i++)
        {
            float a = R(0f, Mathf.PI * 2f), r = R(minR, maxR);
            if (GroundAt(fieldC.x + Mathf.Sin(a) * r, fieldC.z + Mathf.Cos(a) * r, out p)) return true;
        }
        p = fieldC; return false;
    }

    void FindField()
    {
        int ai = game.ZoneIndex("airlock"); Vector3 a = game.zones[ai >= 0 ? ai : 0].position;
        baseY = a.y; lowY = a.y; float high = a.y;
        foreach (var z in game.zones) { lowY = Mathf.Min(lowY, z.position.y); high = Mathf.Max(high, z.position.y); }
        skyY = high + 500f;
        Vector3 p, q;
        for (float r = 16f; r <= 260f && !fieldOk; r += 6f)
            for (int k = 0; k < 24 && !fieldOk; k++)
            {
                float ang = k * 15f * Mathf.Deg2Rad; Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                if (!OpenAt(a.x + dir.x * r, a.z + dir.z * r, 10f, out p)) continue;
                fieldC = p; fieldOk = true;
                if (OpenAt(a.x + dir.x * (r + 24f), a.z + dir.z * (r + 24f), 10f, out q)) fieldC = q;      // a little further from the walls when there is room
            }
    }

    public void TeleportTo(Vector3 p, float yaw)
    {
        if (ride != null) Dismount(null);      // any jump to another place takes you off the rover first, so you never arrive still sitting on it
        var cc = origin.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
        if (game.HeadsetActive)
        {
            origin.MatchOriginUpCameraForward(Vector3.up, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
            origin.MoveCameraToWorldLocation(p + Vector3.up * (Mathf.Max(0.2f, origin.CameraInOriginSpaceHeight) + 0.05f));
        }
        else
        {
            origin.transform.SetPositionAndRotation(p + Vector3.up * 0.05f, Quaternion.identity);
            if (desk) desk.SetLook(yaw, 0f);
        }
        if (cc) cc.enabled = true;
        Physics.SyncTransforms();
    }
    public void GoField()
    {
        if (!HunchCheats.TeleOk) { Note("Teleport is off. Menu > Cheats and anti-cheat."); return; }
        if (!fieldOk) { sim.Say("ARIA: I could not find open ground for the mining field."); return; }
        Vector3 p; if (!GroundAt(fieldC.x - 4f, fieldC.z - 4f, out p)) p = fieldC;
        TeleportTo(p, 45f);
        sim.Say("ARIA: This is the mining field. Choose the Mining drill and aim at the glowing rocks. Watch out for the aliens - zap them and they get beamed home.");
    }

    // ------------------------------------------------------------------ sky: Sun disc and glow, shooting stars
    Light sun; Transform sunCore, sunHalo; Material sunCoreMat, sunHaloMat; LineRenderer[] meteors = new LineRenderer[2]; Vector3[] metPos = new Vector3[2], metVel = new Vector3[2]; float[] metLife = new float[2]; float nextMeteor = 6f; Material metMat;

    static Texture2D Radial(float power)
    {
        int n = 128; var t = new Texture2D(n, n, TextureFormat.RGBA32, false); t.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f, r = Mathf.Sqrt(dx * dx + dy * dy);
            byte a = (byte)(255f * Mathf.Pow(Mathf.Clamp01(1f - r), power));
            px[y * n + x] = new Color32(255, 255, 255, a);
        }
        t.SetPixels32(px); t.Apply(); return t;
    }

    void BuildSky()
    {
        sun = RenderSettings.sun;
        if (!sun) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
        if (sun && clearSrc)
        {
            sunHaloMat = new Material(clearSrc); sunHaloMat.SetTexture("_BaseMap", Radial(2.6f)); sunHaloMat.renderQueue = 2900;
            sunCoreMat = new Material(clearSrc); sunCoreMat.SetTexture("_BaseMap", Radial(0.35f)); sunCoreMat.renderQueue = 2901;
            sunHalo = Prim(PrimitiveType.Quad, root, Vector3.zero, Vector3.one * 1500f, sunHaloMat).transform; sunHalo.name = "Sun glow";
            sunCore = Prim(PrimitiveType.Quad, root, Vector3.zero, Vector3.one * 150f, sunCoreMat).transform; sunCore.name = "Sun disc";
            foreach (var t in new[] { sunHalo, sunCore }) { var r = t.GetComponent<Renderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        }
        metMat = Glow(new Color(2.2f, 2.3f, 2.6f));
        for (int i = 0; i < meteors.Length; i++) { meteors[i] = Line("Shooting star", metMat, 5f, root); meteors[i].enabled = false; }
    }

    void LateUpdate()
    {
        if (!ready || !cam) return;
        LateSky(); LateThird(); LateDyson();
        if (sun && sunHalo)
        {
            Vector3 toSun = -sun.transform.forward; float a = Mathf.Clamp01((toSun.y + 0.04f) / 0.08f);
            bool show = a > 0.01f && sun.enabled;
            if (sunHalo.gameObject.activeSelf != show) { sunHalo.gameObject.SetActive(show); sunCore.gameObject.SetActive(show); }
            if (show)
            {
                Vector3 p = cam.position + toSun * 3300f;
                sunHalo.position = p; sunCore.position = p - toSun * 5f;
                sunHalo.rotation = sunCore.rotation = Quaternion.LookRotation(toSun);
                float flick = 1f + 0.03f * Mathf.Sin(Time.time * 1.7f);
                sunHaloMat.SetColor("_BaseColor", new Color(1.2f, 1.1f, 0.9f, 0.42f * a * flick));
                sunCoreMat.SetColor("_BaseColor", new Color(4f, 3.8f, 3.3f, a));
            }
        }
        // shooting stars
        nextMeteor -= Time.deltaTime;
        if (nextMeteor <= 0f)
        {
            nextMeteor = R(5f, 13f);
            for (int i = 0; i < meteors.Length; i++)
            {
                if (metLife[i] > 0f) continue;
                float az = R(0f, Mathf.PI * 2f), el = R(0.45f, 1.2f);
                Vector3 dir = new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
                Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
                metPos[i] = dir * 2600f; metVel[i] = (side * R(0.6f, 1f) + Vector3.down * R(0.25f, 0.6f)).normalized * R(1500f, 2400f); metLife[i] = R(0.6f, 1.0f);
                meteors[i].enabled = true; break;
            }
        }
        for (int i = 0; i < meteors.Length; i++)
        {
            if (metLife[i] <= 0f) continue;
            metLife[i] -= Time.deltaTime; metPos[i] += metVel[i] * Time.deltaTime;
            if (metLife[i] <= 0f) { meteors[i].enabled = false; continue; }
            meteors[i].widthMultiplier = 5f * Mathf.Clamp01(metLife[i] * 3f);
            meteors[i].SetPosition(0, cam.position + metPos[i]); meteors[i].SetPosition(1, cam.position + metPos[i] - metVel[i] * 0.1f);
        }
    }

    // ------------------------------------------------------------------ power grid: lights, wiring, ventilation
    class Wire { public LineRenderer lr; public Vector3[] pts; public float[] len; public float total, t; public Transform pulse; }
    class Vent { public Transform blades; public Renderer lamp; public ParticleSystem air; public bool on = true; public float spin = 540f; }
    readonly List<Wire> wires = new List<Wire>(); readonly List<Vent> vents = new List<Vent>(); Transform wireRoot; Material wireOn, wireOff, lampOn, lampOff; float fanSpeed = 540f; bool gridLive = true;

    void BuildGrid()
    {
        wireRoot = new GameObject("Power cables").transform; wireRoot.SetParent(root, false);
        wireOn = Glow(new Color(0.2f, 0.75f, 1.05f)); wireOff = Glow(new Color(0.35f, 0.06f, 0.05f));
        var pulseMat = Glow(new Color(2.4f, 2.2f, 1.2f));
        int si = game.ZoneIndex("power"); if (si < 0) si = game.ZoneIndex("eclss"); if (si < 0) si = 0;
        Vector3 s = game.zones[si].position; const float H = 3.6f;
        for (int i = 0; i < game.zones.Count; i++)
        {
            if (i == si) continue;
            Vector3 d = game.zones[i].position;
            var w = new Wire();
            w.pts = new[] { s + Vector3.up * 0.4f, s + Vector3.up * H, new Vector3(d.x, s.y + H, s.z), new Vector3(d.x, s.y + H, d.z), new Vector3(d.x, d.y + H, d.z), d + Vector3.up * 0.4f + new Vector3(0.9f, 0f, 0.9f) };
            w.pts[4] += new Vector3(0.9f, 0f, 0.9f);
            w.len = new float[w.pts.Length]; for (int k = 1; k < w.pts.Length; k++) { w.total += Vector3.Distance(w.pts[k - 1], w.pts[k]); w.len[k] = w.total; }
            w.lr = Line("Cable to " + game.zones[i].id, wireOn, 0.022f, wireRoot); w.lr.positionCount = w.pts.Length; w.lr.SetPositions(w.pts);
            w.pulse = Prim(PrimitiveType.Sphere, wireRoot, s, Vector3.one * 0.09f, pulseMat).transform; w.pulse.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            w.t = R(0f, 1f);
            wires.Add(w);
        }

        // one ventilation unit near the ceiling of every zone
        var housing = Lit(new Color(0.22f, 0.24f, 0.28f)); var blade = Lit(new Color(0.75f, 0.78f, 0.82f));
        lampOn = Glow(new Color(0.3f, 2.2f, 0.6f)); lampOff = Glow(new Color(2.2f, 0.25f, 0.2f));
        var airMat = HunchFx.MaterialOf("smoke puff");
        var ventRoot = new GameObject("Ventilation").transform; ventRoot.SetParent(root, false);
        foreach (var z in game.zones)
        {
            Vector3 b = z.position + new Vector3(-1.3f, 0f, 1.3f); float y = z.position.y + 3.1f; RaycastHit h;
            if (Physics.Raycast(b + Vector3.up * 1.3f, Vector3.up, out h, 8f, GroundMask, QueryTriggerInteraction.Ignore)) y = h.point.y - 0.14f;
            var u = new GameObject("Vent " + z.id).transform; u.SetParent(ventRoot, false); u.position = new Vector3(b.x, y, b.z);
            Prim(PrimitiveType.Cylinder, u, Vector3.zero, new Vector3(0.62f, 0.06f, 0.62f), housing);
            var v = new Vent();
            v.blades = new GameObject("Blades").transform; v.blades.SetParent(u, false); v.blades.localPosition = new Vector3(0f, -0.08f, 0f);
            for (int k = 0; k < 4; k++) { var bl = Prim(PrimitiveType.Cube, v.blades, Vector3.zero, new Vector3(0.52f, 0.012f, 0.1f), blade).transform; bl.localRotation = Quaternion.Euler(14f, k * 45f, 0f); }
            v.lamp = Prim(PrimitiveType.Sphere, u, new Vector3(0.36f, -0.03f, 0f), Vector3.one * 0.06f, lampOn).GetComponent<Renderer>();
            if (airMat)
            {
                var pg = new GameObject("Air"); pg.transform.SetParent(u, false); pg.transform.localPosition = new Vector3(0f, -0.15f, 0f); pg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var ps = pg.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.startLifetime = 1.7f; main.startSpeed = 0.75f; main.startSize = 0.1f; main.startColor = new Color(0.8f, 0.95f, 1f, 0.16f); main.maxParticles = 30; main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = 7f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 10f; sh.radius = 0.16f;
                var pr = pg.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = airMat; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ps.Play(); v.air = ps;
            }
            vents.Add(v);
        }
    }

    public void SetLights(bool on) { lightsOn = on; game.lightSwitch = on ? 1f : 0.04f; for (int i = 0; i < consoles.Count; i++) SetZoneLight(i, true, false); Play(cThud, 0.5f); sim.Say(on ? "ARIA: Habitat lights on." : "ARIA: Habitat lights off. The batteries charge a little faster in the dark."); }
    public void SetVent(bool on)
    {
        ventOn = on; ventWarned = false;
        for (int i = 0; i < vents.Count; i++) SetZoneFan(i, on, false);
        sim.Say(on ? "ARIA: Ventilation fans running. Air is being cleaned and mixed." : "ARIA: Ventilation fans stopped. Stale air builds up, so oxygen will slowly fall until you switch them back on.");
    }

    // A small control console beside every zone: one button for that zone's lamps, one for its ventilation fan.
    class Console { public Renderer lightBtn, fanBtn; public bool lightOn = true; }
    readonly List<Console> consoles = new List<Console>(); int[] lightZone; Material btnLightOn, btnFanOn, btnOff;

    void BuildConsoles()
    {
        btnLightOn = Glow(new Color(2.4f, 2.0f, 0.5f)); btnFanOn = Glow(new Color(0.4f, 1.8f, 2.4f)); btnOff = Lit(new Color(0.2f, 0.2f, 0.22f));
        var post = Lit(new Color(0.3f, 0.32f, 0.36f)); var face = Lit(new Color(0.1f, 0.11f, 0.13f));
        var cRoot = new GameObject("Zone consoles").transform; cRoot.SetParent(root, false);
        for (int i = 0; i < game.zones.Count; i++)
        {
            var z = game.zones[i]; Vector3 dir = Quaternion.Euler(0f, z.yaw + 55f, 0f) * Vector3.forward;
            var c = new GameObject("Console " + z.id).transform; c.SetParent(cRoot, false); c.position = z.position + dir * 1.35f; c.rotation = Quaternion.LookRotation(dir, Vector3.up);
            Prim(PrimitiveType.Cylinder, c, new Vector3(0f, 0.47f, 0f), new Vector3(0.09f, 0.47f, 0.09f), post);
            var panel = new GameObject("Panel").transform; panel.SetParent(c, false); panel.localPosition = new Vector3(0f, 1.0f, 0f); panel.localRotation = Quaternion.Euler(50f, 0f, 0f);
            Prim(PrimitiveType.Cube, panel, Vector3.zero, new Vector3(0.52f, 0.3f, 0.05f), face);
            var con = new Console();
            for (int b = 0; b < 2; b++)
            {
                var g = Prim(PrimitiveType.Cylinder, panel, new Vector3(b == 0 ? -0.13f : 0.13f, 0f, -0.04f), new Vector3(0.15f, 0.02f, 0.15f), b == 0 ? btnLightOn : btnFanOn);
                g.name = b == 0 ? "Light button" : "Fan button"; g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(1.5f, 4f, 1.5f);
                var tg = g.AddComponent<HunchTarget>(); tg.kind = b == 0 ? 11 : 12; tg.index = i;
                if (b == 0) con.lightBtn = g.GetComponent<Renderer>(); else con.fanBtn = g.GetComponent<Renderer>();
            }
            consoles.Add(con);
        }
        // which zone each lamp belongs to (the nearest one)
        if (game.interiorLights != null)
        {
            lightZone = new int[game.interiorLights.Length]; game.lightMul = new float[game.interiorLights.Length];
            for (int i = 0; i < lightZone.Length; i++)
            {
                game.lightMul[i] = 1f; lightZone[i] = -1; if (!game.interiorLights[i]) continue;
                Vector3 lp = game.interiorLights[i].transform.position; float best = 1e9f;
                for (int k = 0; k < game.zones.Count; k++) { Vector3 d = game.zones[k].position - lp; float dd = d.x * d.x + d.z * d.z + d.y * d.y * 4f; if (dd < best) { best = dd; lightZone[i] = k; } }
            }
        }
    }

    public void SetZoneLight(int zone, bool on, bool say = true)
    {
        if (zone < 0 || zone >= consoles.Count) return;
        var c = consoles[zone]; c.lightOn = on; if (c.lightBtn) c.lightBtn.sharedMaterial = on ? btnLightOn : btnOff;
        if (lightZone != null) for (int i = 0; i < lightZone.Length; i++) if (lightZone[i] == zone) game.lightMul[i] = on ? 1f : 0.03f;
        if (say) { switchUses++; Play(cThud, 0.6f); game.Buzz(0.4f, 0.06f); Note(game.zones[zone].title + ": lights " + (on ? "ON" : "OFF")); }
    }
    public void SetZoneFan(int zone, bool on, bool say = true)
    {
        if (zone < 0 || zone >= vents.Count) return;
        var v = vents[zone]; v.on = on; if (v.lamp) v.lamp.sharedMaterial = on ? lampOn : lampOff; if (v.air) { var em = v.air.emission; em.enabled = on; }
        if (zone < consoles.Count && consoles[zone].fanBtn) consoles[zone].fanBtn.sharedMaterial = on ? btnFanOn : btnOff;
        if (say) { switchUses++; Play(cThud, 0.6f); game.Buzz(0.4f, 0.06f); ventWarned = false; Note(game.zones[zone].title + ": fan " + (on ? "ON" : "OFF")); }
    }

    void UpdateGrid(float dt)
    {
        bool live = !sim.Blackout;
        if (live != gridLive) { gridLive = live; foreach (var w in wires) w.lr.sharedMaterial = live ? wireOn : wireOff; }
        if (wireRoot.gameObject.activeSelf != wiresOn) wireRoot.gameObject.SetActive(wiresOn);
        if (wiresOn)
            foreach (var w in wires)
            {
                if (w.pulse.gameObject.activeSelf != live) w.pulse.gameObject.SetActive(live);
                if (!live) continue;
                w.t += dt * 9f / Mathf.Max(1f, w.total); if (w.t > 1f) w.t -= 1f;
                float dist = w.t * w.total; int k = 1; while (k < w.pts.Length - 1 && w.len[k] < dist) k++;
                float seg = w.len[k] - w.len[k - 1];
                w.pulse.position = Vector3.Lerp(w.pts[k - 1], w.pts[k], seg > 0.001f ? (dist - w.len[k - 1]) / seg : 0f);
            }
        int fansOff = 0;
        foreach (var v in vents)
        {
            if (!v.on) fansOff++;
            v.spin = Mathf.Lerp(v.spin, v.on && live ? 540f : 0f, dt * 1.5f);
            if (v.blades) v.blades.Rotate(0f, v.spin * dt, 0f, Space.Self);
        }
        if (RealMission && !game.Paused)
        {
            if (!lightsOn) sim.battery = Mathf.Min(100f, sim.battery + 0.12f * dt);
            if (fansOff > 0) { sim.oxygen = Mathf.Max(0f, sim.oxygen - 0.25f * dt * fansOff / Mathf.Max(1, vents.Count)); if (!ventWarned && sim.oxygen < 80f) { ventWarned = true; sim.Say("ARIA: Oxygen is falling because the ventilation is off. Switch it on in the menu under Power, lights and air."); } }
        }
    }

    // ------------------------------------------------------------------ the field: beacon, mineral nodes, pickups, rovers
    class Node { public Transform t; public Collider col; public int item, yield, stages = 1, stagesMax = 1; public float hp, regrow, maxHp = 3f; public bool up; }
    class Pickup { public Transform t; public int item, count; public float age; }
    class Rover { public Transform t; public Transform[] wheels; public Transform dish; public Collider col; public float ang, radius, speed, y, check, dust; public Vector3 heading = Vector3.forward, last; }
    readonly List<Node> nodes = new List<Node>(); readonly List<Pickup> pickups = new List<Pickup>(); readonly List<Rover> rovers = new List<Rover>();
    Transform beaconLamp; float fullNote;

    void BuildBeacon()
    {
        var b = new GameObject("Mining field beacon").transform; b.SetParent(root, false); b.position = fieldC;
        Prim(PrimitiveType.Cylinder, b, new Vector3(0f, 3f, 0f), new Vector3(0.12f, 3f, 0.12f), Lit(new Color(0.7f, 0.72f, 0.76f)));
        Prim(PrimitiveType.Cylinder, b, new Vector3(0f, 0.08f, 0f), new Vector3(1.1f, 0.08f, 1.1f), Lit(new Color(0.25f, 0.27f, 0.3f)));
        beaconLamp = Prim(PrimitiveType.Sphere, b, new Vector3(0f, 6.2f, 0f), Vector3.one * 0.45f, Glow(new Color(0.4f, 2.6f, 1.2f))).transform;
        // a tall beam of light so the field can be found from anywhere outside
        var pillar = Prim(PrimitiveType.Cylinder, b, new Vector3(0f, 126f, 0f), new Vector3(0.9f, 120f, 0.9f), Clear(new Color(0.35f, 1.3f, 0.75f, 0.13f)));
        pillar.name = "Light pillar"; pillar.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void BuildNodes()
    {
        int[] kinds = { Ice, Ore, Ice, Ore, He3, Ore, Ice, Ore, Ice, He3, Ore, Ice, Ore, Ice, He3, Ore };
        var iceLit = Lit(new Color(0.62f, 0.86f, 0.98f)); var iceGlow = Glow(new Color(0.5f, 1.5f, 2.2f));
        var oreLit = Lit(new Color(0.16f, 0.15f, 0.17f)); var oreGlow = Glow(new Color(2.2f, 1.0f, 0.25f));
        var heLit = Lit(new Color(0.42f, 0.4f, 0.38f)); var heGlow = Glow(new Color(1.6f, 2.4f, 0.4f));
        var nodeRoot = new GameObject("Mineral nodes").transform; nodeRoot.SetParent(root, false);
        for (int i = 0; i < kinds.Length; i++)
        {
            Vector3 p; if (!RandomGround(7f, 38f, out p)) continue;
            var n = new Node { item = kinds[i], hp = 3f, up = true };
            n.t = new GameObject(ItemNames[kinds[i]] + " node").transform; n.t.SetParent(nodeRoot, false); n.t.position = p; n.t.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            if (kinds[i] == Ice)
                for (int k = 0; k < 5; k++)
                {
                    float h = R(0.7f, 1.5f); var c = Prim(PrimitiveType.Cube, n.t, new Vector3(R(-0.4f, 0.4f), h * 0.4f, R(-0.4f, 0.4f)), new Vector3(0.24f, h, 0.24f), k == 0 ? iceGlow : iceLit).transform;
                    c.localRotation = Quaternion.Euler(R(-22f, 22f), R(0f, 90f), R(-22f, 22f));
                }
            else if (kinds[i] == Ore)
            {
                for (int k = 0; k < 4; k++) Prim(PrimitiveType.Sphere, n.t, new Vector3(R(-0.45f, 0.45f), R(0.15f, 0.4f), R(-0.45f, 0.45f)), new Vector3(R(0.6f, 1f), R(0.5f, 0.8f), R(0.6f, 1f)), oreLit);
                for (int k = 0; k < 5; k++) Prim(PrimitiveType.Cube, n.t, new Vector3(R(-0.55f, 0.55f), R(0.3f, 0.75f), R(-0.55f, 0.55f)), Vector3.one * R(0.07f, 0.13f), oreGlow).transform.localRotation = Quaternion.Euler(R(0f, 90f), R(0f, 90f), 0f);
            }
            else
            {
                for (int k = 0; k < 6; k++) { float a = k * 60f * Mathf.Deg2Rad; Prim(PrimitiveType.Sphere, n.t, new Vector3(Mathf.Sin(a) * 0.6f, 0.12f, Mathf.Cos(a) * 0.6f), new Vector3(0.5f, 0.36f, 0.5f), heLit); }
                Prim(PrimitiveType.Sphere, n.t, new Vector3(0f, 0.22f, 0f), new Vector3(0.7f, 0.6f, 0.7f), heGlow);
            }
            var sc = n.t.gameObject.AddComponent<SphereCollider>(); sc.center = new Vector3(0f, 0.5f, 0f); sc.radius = 0.85f; n.col = sc;
            var tg = n.t.gameObject.AddComponent<HunchTarget>(); tg.kind = 2; tg.index = nodes.Count;
            nodes.Add(n);
        }
    }

    void UpdateNodes(float dt)
    {
        foreach (var n in nodes)
        {
            if (!n.up) { n.regrow -= dt; if (n.regrow <= 0f) { n.up = true; n.hp = n.maxHp; n.stages = n.stagesMax; n.col.enabled = true; } }
            float want = n.up ? (n.stagesMax > 1 ? 0.55f + 0.45f * n.stages / n.stagesMax : 1f) : 0.02f, s = n.t.localScale.x;
            if (Mathf.Abs(s - want) > 0.005f) n.t.localScale = Vector3.one * Mathf.Lerp(s, want, dt * 3f);
        }
        if (beaconLamp) beaconLamp.localScale = Vector3.one * (0.4f + 0.12f * Mathf.Sin(Time.time * 3f));
    }

    public int Capacity(int item) { return (item == Block ? 64 : 20) * packLv; }
    public int AddItem(int item, int n)
    {
        int room = Capacity(item) - bag[item], add = Mathf.Clamp(n, 0, Mathf.Max(0, room));
        bag[item] += add;
        if (add < n && Time.unscaledTime > fullNote) { fullNote = Time.unscaledTime + 4f; Note("Backpack is full of " + ItemNames[item] + ". Use some (I) or buy the bigger backpack."); }
        return add;
    }

    void DropPickup(int item, int count, Vector3 p)
    {
        var pk = new Pickup { item = item, count = count };
        pk.t = new GameObject("Pickup " + ItemNames[item]).transform; pk.t.SetParent(root, false); pk.t.position = p + Vector3.up * 0.6f;
        if (item == Crystal) Prim(PrimitiveType.Cube, pk.t, Vector3.zero, Vector3.one * 0.3f, Glow(new Color(2.2f, 0.7f, 2.4f))).transform.localRotation = Quaternion.Euler(45f, 0f, 35.26f);
        else
        {
            Prim(PrimitiveType.Cube, pk.t, Vector3.zero, new Vector3(0.55f, 0.4f, 0.55f), Lit(new Color(0.85f, 0.6f, 0.15f)));
            Prim(PrimitiveType.Cube, pk.t, new Vector3(0f, 0.21f, 0f), new Vector3(0.58f, 0.05f, 0.2f), Glow(item == Cell ? new Color(0.4f, 2.2f, 0.7f) : new Color(1.6f, 1.6f, 1.8f)));
        }
        pickups.Add(pk);
    }

    void BuildCrates()
    {
        int[] items = { Cell, Block, Crystal, Cell, Block }; int[] counts = { 1, 8, 2, 1, 8 };
        for (int i = 0; i < items.Length; i++) { Vector3 p; if (RandomGround(4f, 20f, out p)) DropPickup(items[i], counts[i], p - Vector3.up * 0.25f); }
    }

    void UpdatePickups(float dt)
    {
        Vector3 f = Feet();
        for (int i = pickups.Count - 1; i >= 0; i--)
        {
            var pk = pickups[i]; pk.age += dt;
            pk.t.Rotate(0f, 80f * dt, 0f, Space.World);
            Vector3 d = pk.t.position - f; float dy = d.y; d.y = 0f;
            bool near = d.sqrMagnitude <= 2.9f && dy >= -1.5f && dy <= 3f;
            if (!near && pk.age > 0.5f) foreach (var a in aliens) if (a.alive && a.friend && (a.t.position - pk.t.position).sqrMagnitude < 3.5f) { near = true; break; }      // an alien friend fetched it
            if (pk.age < 0.5f || !near) continue;
            int got = AddItem(pk.item, pk.count);
            if (got <= 0) continue;
            Note("Picked up " + got + " x " + ItemNames[pk.item]); Play(cPick, 0.6f); game.Buzz(0.4f, 0.08f);
            Fx2("sparkle_ellow", "star hit", pk.t.position, 0.7f, 0.25f, 0.8f);
            Destroy(pk.t.gameObject); pickups.RemoveAt(i);
        }
    }

    void BuildRovers()
    {
        var body = Lit(new Color(0.86f, 0.87f, 0.9f)); var dark = Lit(new Color(0.12f, 0.12f, 0.14f)); var gold = Lit(new Color(0.85f, 0.65f, 0.2f)); var lamp = Glow(new Color(2.4f, 2.2f, 1.6f));
        for (int i = 0; i < 2; i++)
        {
            var r = new Rover { radius = 27f + i * 9f, speed = (i == 0 ? 2.6f : -2.0f), ang = i * 2.1f, y = fieldC.y };
            r.t = new GameObject("Rover " + (i + 1)).transform; r.t.SetParent(root, false);
            Prim(PrimitiveType.Cube, r.t, new Vector3(0f, 0.62f, 0f), new Vector3(1.1f, 0.38f, 1.8f), body);
            Prim(PrimitiveType.Cube, r.t, new Vector3(0f, 0.86f, -0.25f), new Vector3(1.3f, 0.05f, 1.0f), gold);
            Prim(PrimitiveType.Cylinder, r.t, new Vector3(0.3f, 1.15f, 0.55f), new Vector3(0.04f, 0.3f, 0.04f), dark);
            Prim(PrimitiveType.Cube, r.t, new Vector3(0.3f, 1.48f, 0.6f), new Vector3(0.22f, 0.14f, 0.16f), dark);
            Prim(PrimitiveType.Sphere, r.t, new Vector3(0.3f, 1.48f, 0.7f), Vector3.one * 0.09f, lamp);
            r.dish = Prim(PrimitiveType.Sphere, r.t, new Vector3(-0.3f, 1.05f, -0.55f), new Vector3(0.45f, 0.1f, 0.45f), body).transform; r.dish.localRotation = Quaternion.Euler(50f, 0f, 0f);
            var wl = new List<Transform>();
            for (int k = 0; k < 6; k++)
            {
                var w = Prim(PrimitiveType.Cylinder, r.t, new Vector3(k % 2 == 0 ? -0.66f : 0.66f, 0.28f, (k / 2 - 1) * 0.68f), new Vector3(0.56f, 0.09f, 0.56f), dark).transform;
                w.localRotation = Quaternion.Euler(0f, 0f, 90f); wl.Add(w);
            }
            r.wheels = wl.ToArray();
            var bc = r.t.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.7f, 0f); bc.size = new Vector3(1.4f, 1.1f, 2f); r.col = bc;
            var rt = r.t.gameObject.AddComponent<HunchTarget>(); rt.kind = 10; rt.index = i;
            SetLayer(r.t.gameObject, 2);
            rovers.Add(r);
        }
    }

    void UpdateRovers(float dt)
    {
        foreach (var r in rovers)
        {
            if (r == ride)
            {
                // the rover drives wherever you walk
                Vector3 f = Feet(), g; float gy = GroundAt(f.x, f.z, out g) ? g.y : f.y;
                Vector3 mv = f - r.last; mv.y = 0f; r.last = f;
                if (mv.sqrMagnitude > 0.0004f) r.heading = Vector3.Slerp(r.heading, mv.normalized, dt * 6f);
                r.t.SetPositionAndRotation(new Vector3(f.x, gy, f.z), Quaternion.LookRotation(r.heading, Vector3.up));
                float sp = mv.magnitude / Mathf.Max(0.0001f, dt);
                foreach (var w in r.wheels) w.Rotate(0f, sp / 0.28f * Mathf.Rad2Deg * dt, 0f, Space.Self);
                r.dust -= dt; if (r.dust <= 0f && sp > 1f) { r.dust = 0.5f; HunchFx.Spawn("dust puff", r.t.position - r.heading * 1.0f + Vector3.up * 0.05f, 0.12f, 1.2f); }
                continue;
            }
            r.ang += r.speed / r.radius * dt;
            float x = fieldC.x + Mathf.Sin(r.ang) * r.radius, z = fieldC.z + Mathf.Cos(r.ang) * r.radius;
            r.check -= dt; if (r.check <= 0f) { r.check = 0.2f; Vector3 g; if (GroundAt(x, z, out g)) r.y = g.y; }
            Vector3 pos = new Vector3(x, Mathf.Lerp(r.t.position.y, r.y, dt * 4f), z);
            Vector3 fwd = new Vector3(Mathf.Cos(r.ang), 0f, -Mathf.Sin(r.ang)) * Mathf.Sign(r.speed);
            r.t.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up));
            foreach (var w in r.wheels) w.Rotate(0f, Mathf.Abs(r.speed) / 0.28f * Mathf.Rad2Deg * dt, 0f, Space.Self);
            if (r.dish) r.dish.Rotate(0f, 40f * dt, 0f, Space.World);
            r.dust -= dt; if (r.dust <= 0f) { r.dust = 1.4f; if ((pos - cam.position).sqrMagnitude < 90f * 90f) HunchFx.Spawn("dust puff", pos - fwd * 0.9f + Vector3.up * 0.05f, 0.12f, 1.2f); }
        }
    }

    Rover ride; float eyeBase;

    void Mount(Rover r)
    {
        if (ride != null) Dismount(null);
        ride = r; r.col.enabled = false; r.last = Feet(); r.heading = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 g; Vector3 p = r.t.position; if (GroundAt(p.x, p.z, out g)) p = g;
        TeleportTo(p, Mathf.Atan2(r.heading.x, r.heading.z) * Mathf.Rad2Deg); r.last = Feet();
        if (desk && !game.HeadsetActive) { eyeBase = desk.eyeHeight; desk.eyeHeight = eyeBase + 0.75f; }
        Note("Riding the rover! Move to drive it. " + (game.HeadsetActive ? "Look down and pull the right trigger to get off." : "Look down and click to get off."));
        Play(cPop, 0.6f); game.Buzz(0.6f, 0.12f);
    }
    void Dismount(string why)
    {
        if (ride == null) return;
        var r = ride; ride = null; r.col.enabled = true;
        if (desk && eyeBase > 0f) { desk.eyeHeight = eyeBase; eyeBase = 0f; }
        Vector3 d = r.t.position - fieldC; d.y = 0f;
        r.radius = Mathf.Clamp(d.magnitude, 12f, 46f); r.ang = Mathf.Atan2(d.x, d.z);
        // step off to the side so you are not standing inside it
        Vector3 side = r.t.position + r.t.right * 1.9f, g; if (GroundAt(side.x, side.z, out g)) side = g; else side.y = Feet().y;
        TeleportTo(side, cam.eulerAngles.y);
        if (why != null) Note(why); else Note("You got off the rover.");
    }

    // moon rocks: pick one up and throw it (VR: grab with the grip). A thrown rock sends an alien home too.
    class Rock { public Transform t; public Rigidbody rb; public Collider col; }
    readonly List<Rock> rocks = new List<Rock>(); Rock heldRock;

    void BuildRocks()
    {
        var m1 = Lit(new Color(0.42f, 0.41f, 0.4f)); var m2 = Lit(new Color(0.3f, 0.29f, 0.3f));
        var rr = new GameObject("Moon rocks").transform; rr.SetParent(root, false);
        for (int i = 0; i < 14; i++)
        {
            Vector3 p; if (!RandomGround(3f, 30f, out p)) continue;
            var g = Prim(PrimitiveType.Sphere, rr, Vector3.zero, new Vector3(R(0.3f, 0.45f), R(0.24f, 0.34f), R(0.3f, 0.42f)), i % 2 == 0 ? m1 : m2, true);
            g.name = "Moon rock"; g.transform.position = p + Vector3.up * 0.3f; g.transform.rotation = Quaternion.Euler(R(0f, 360f), R(0f, 360f), 0f);
            var rk = new Rock { t = g.transform, col = g.GetComponent<Collider>() };
            rk.rb = g.AddComponent<Rigidbody>(); rk.rb.mass = 3f; rk.rb.angularDamping = 0.6f; rk.rb.linearDamping = 0.05f;
            var tg = g.AddComponent<HunchTarget>(); tg.kind = 13; tg.index = rocks.Count;
            try { g.AddComponent<XRGrabInteractable>(); } catch { }
            rocks.Add(rk);
        }
    }

    void PickRock(Rock r) { heldRock = r; r.rb.isKinematic = true; r.col.enabled = false; Note("Holding a moon rock. " + (game.HeadsetActive ? "Right trigger" : "Click") + " to throw it."); Play(cPick, 0.4f); }
    void ThrowRock()
    {
        var r = heldRock; if (r == null) return; heldRock = null;
        Ray ray = AimRay(); r.t.position = cam.position + ray.direction * 0.9f;
        r.col.enabled = true; r.rb.isKinematic = false; r.rb.linearVelocity = ray.direction * 11f + Vector3.up * 1.6f; r.rb.angularVelocity = new Vector3(R(-4f, 4f), R(-4f, 4f), R(-4f, 4f));
        Play(cThud, 0.5f); game.Buzz(0.5f, 0.08f);
    }
    void UpdateRocks(float dt)
    {
        if (heldRock != null) { heldRock.t.position = cam.position + cam.forward * 0.9f + cam.right * 0.32f - cam.up * 0.28f; heldRock.t.Rotate(40f * dt, 60f * dt, 0f); }
        foreach (var r in rocks)
        {
            if (r == heldRock || r.rb.isKinematic) continue;
            if (r.t.position.y < lowY - 40f) { Vector3 p; RandomGround(3f, 25f, out p); r.rb.linearVelocity = Vector3.zero; r.t.position = p + Vector3.up * 0.5f; continue; }
            if (r.rb.linearVelocity.sqrMagnitude < 9f) continue;
            foreach (var a in aliens)
                if (a.alive && !a.friend && (a.t.position + Vector3.up * 0.5f - r.t.position).sqrMagnitude < 0.9f) { HitAlien(a, 2f, false); rockHits++; r.rb.linearVelocity *= 0.3f; break; }
        }
    }

    // guard turrets (an upgrade): they zap aliens that come near
    class Turret { public Transform t, head; public float cd; public bool modelHead; }
    public float turretYaw;      // turns the imported turret model if it was modelled facing another way
    readonly List<Turret> turrets = new List<Turret>();

    void BuildTurret()
    {
        Vector3 p; if (!fieldOk || !RandomGround(5f, 16f, out p)) return;
        var tu = new Turret();
        tu.t = new GameObject("Guard turret").transform; tu.t.SetParent(root, false); tu.t.position = p;
        var steel = Lit(new Color(0.5f, 0.52f, 0.56f)); var dark = Lit(new Color(0.15f, 0.16f, 0.18f));
        tu.head = new GameObject("Head").transform; tu.head.SetParent(tu.t, false); tu.head.localPosition = new Vector3(0f, 1.2f, 0f);
        GameObject model = null;
        foreach (var n in HunchFx.NamesStarting("gun:")) if (n.Contains("motion") || n.Contains("sentry") || n.Contains("turret")) { var pf = HunchFx.Prefab(n); if (pf) { model = Instantiate(pf); break; } }
        if (model)
        {
            // the imported "Motion Sensor Gun" model: stood on the ground, 1.7 m tall, the whole thing turns to aim
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var c0 in model.GetComponentsInChildren<Collider>(true)) Destroy(c0);
            var rs = model.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float k = 1.7f / Mathf.Max(0.001f, b.size.y);
                tu.head.localPosition = Vector3.zero;
                model.transform.SetParent(tu.head, true); model.transform.localScale *= k;
                model.transform.localPosition = new Vector3(-b.center.x * k, -b.min.y * k, -b.center.z * k);
                tu.modelHead = true;
            }
            else { Destroy(model); model = null; }
        }
        if (!model)
        {
            Prim(PrimitiveType.Cylinder, tu.t, new Vector3(0f, 0.5f, 0f), new Vector3(0.35f, 0.5f, 0.35f), steel);
            Prim(PrimitiveType.Sphere, tu.head, Vector3.zero, Vector3.one * 0.55f, dark);
            Prim(PrimitiveType.Cylinder, tu.head, new Vector3(0f, 0f, 0.45f), new Vector3(0.1f, 0.35f, 0.1f), steel).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, tu.head, new Vector3(0f, 0f, 0.82f), Vector3.one * 0.13f, Glow(new Color(0.6f, 2.6f, 0.5f)));
        }
        var c = tu.t.gameObject.AddComponent<CapsuleCollider>(); c.center = new Vector3(0f, 0.75f, 0f); c.height = 1.5f; c.radius = 0.35f;
        SetLayer(tu.t.gameObject, 2);
        turrets.Add(tu);
    }
    void UpdateTurrets(float dt)
    {
        foreach (var tu in turrets)
        {
            Alien best = null; float bd = 28f * 28f;
            foreach (var a in aliens) if (a.alive && !a.friend) { float d = (a.t.position - tu.t.position).sqrMagnitude; if (d < bd) { bd = d; best = a; } }
            tu.cd -= dt;
            if (best == null) { tu.head.Rotate(0f, 25f * dt, 0f, Space.World); continue; }
            Vector3 aim = best.t.position + Vector3.up * 0.5f - (tu.head.position + (tu.modelHead ? Vector3.up * 1.2f : Vector3.zero));
            if (tu.modelHead) aim.y = 0f;
            if (aim.sqrMagnitude > 0.01f) tu.head.rotation = Quaternion.Slerp(tu.head.rotation, Quaternion.LookRotation(aim.normalized, Vector3.up) * Quaternion.Euler(0f, tu.modelHead ? turretYaw : 0f, 0f), dt * 5f);
            if (tu.cd > 0f || !Playing) continue;
            Vector3 muzzle = tu.modelHead ? tu.t.position + Vector3.up * 1.3f + aim.normalized * 0.6f : tu.head.position + tu.head.forward * 0.85f;
            tu.cd = 1.5f; ShowBeam(muzzle, best.t.position + Vector3.up * 0.5f, beamGreen);
            if ((tu.t.position - cam.position).sqrMagnitude < 40f * 40f) Play(cZap, 0.2f);
            HitAlien(best, 1f, false);
        }
    }

    // automatic mining rigs (an upgrade)
    class Rig { public Transform t, bit; public float timer; }
    readonly List<Rig> rigs = new List<Rig>();

    void BuildRig()
    {
        Vector3 p; if (!fieldOk || !RandomGround(5f, 14f, out p)) return;
        var g = new Rig { timer = 8f };
        g.t = new GameObject("Auto-miner rig").transform; g.t.SetParent(root, false); g.t.position = p;
        var frame = Lit(new Color(0.9f, 0.7f, 0.15f)); var steel = Lit(new Color(0.5f, 0.52f, 0.56f));
        for (int k = 0; k < 3; k++)
        {
            float a = k * 120f * Mathf.Deg2Rad; var leg = Prim(PrimitiveType.Cylinder, g.t, new Vector3(Mathf.Sin(a) * 0.7f, 1.2f, Mathf.Cos(a) * 0.7f), new Vector3(0.09f, 1.35f, 0.09f), frame).transform;
            leg.localRotation = Quaternion.Euler(Mathf.Cos(a) * -28f, 0f, Mathf.Sin(a) * 28f);
        }
        Prim(PrimitiveType.Cube, g.t, new Vector3(0f, 2.35f, 0f), new Vector3(0.6f, 0.4f, 0.6f), frame);
        Prim(PrimitiveType.Sphere, g.t, new Vector3(0f, 2.7f, 0f), Vector3.one * 0.18f, Glow(new Color(2.4f, 1.4f, 0.2f)));
        g.bit = Prim(PrimitiveType.Cylinder, g.t, new Vector3(0f, 1.1f, 0f), new Vector3(0.2f, 1.1f, 0.2f), steel).transform;
        Prim(PrimitiveType.Cube, g.bit, new Vector3(0f, -0.6f, 0f), new Vector3(1.9f, 0.12f, 0.5f), steel);
        var c = g.t.gameObject.AddComponent<CapsuleCollider>(); c.center = new Vector3(0f, 1.3f, 0f); c.height = 2.6f; c.radius = 0.7f;
        SetLayer(g.t.gameObject, 2);
        rigs.Add(g);
    }

    void UpdateRigs(float dt)
    {
        foreach (var g in rigs)
        {
            g.bit.Rotate(0f, 420f * dt, 0f, Space.Self);
            g.bit.localPosition = new Vector3(0f, 1.0f + 0.22f * Mathf.Sin(Time.time * 2.4f), 0f);
            if (!Playing) continue;
            g.timer -= dt;
            if (g.timer > 0f) continue;
            g.timer = 9f; int item = rng.Next(10) < 2 ? He3 : (rng.Next(2) == 0 ? Ice : Ore);
            if (AddItem(item, 1) > 0) { minedCount++; if ((g.t.position - cam.position).sqrMagnitude < 60f * 60f) HunchFx.Spawn("stones hit", g.t.position + Vector3.up * 0.2f, 0.35f, 0.8f); }
        }
    }

    // ------------------------------------------------------------------ aliens and their UFO
    class Alien { public Transform t; public Renderer body; public SphereCollider col; public Color colour; public float hp, frozen, phase, gy, speed, wait, flash, check, respawn, flee, touch; public Vector3 goal; public bool alive, friend; public MaterialPropertyBlock block; }
    readonly List<Alien> aliens = new List<Alien>(); Transform ufo, ufoLights, beam; float beamT; Vector3 beamTo; float ufoAng;

    void BuildAliens()
    {
        Color[] cols = { new Color(0.35f, 0.85f, 0.3f), new Color(0.65f, 0.4f, 0.9f), new Color(0.2f, 0.8f, 0.75f), new Color(0.95f, 0.55f, 0.75f), new Color(0.5f, 0.9f, 0.25f), new Color(0.4f, 0.55f, 0.95f) };
        var white = Lit(new Color(0.97f, 0.97f, 0.97f)); var black = Lit(new Color(0.03f, 0.03f, 0.04f)); var tip = Glow(new Color(2.4f, 2.0f, 0.4f));
        var alienRoot = new GameObject("Aliens").transform; alienRoot.SetParent(root, false);
        for (int i = 0; i < cols.Length; i++)
        {
            var a = new Alien { colour = cols[i], block = new MaterialPropertyBlock(), respawn = 3f + i * 2.5f, speed = 2.0f + 0.25f * i };
            a.t = new GameObject("Alien " + (i + 1)).transform; a.t.SetParent(alienRoot, false);
            var skin = new Material(litSrc); skin.SetColor("_BaseColor", cols[i]); if (skin.HasProperty("_BaseMap")) skin.SetTexture("_BaseMap", null);
            // the alien modelled in Blender (rounded body, big eyes, ears, tail); the coded ball-and-sticks alien is only the fallback
            GameObject am = null; try { am = Kit(false, "Alien", a.t, Vector3.zero, 180f, 1.25f, false); } catch { am = null; }
            Renderer amBody = null; if (am) foreach (var r in am.GetComponentsInChildren<Renderer>()) if (r.name == "Body") amBody = r;
            if (amBody) { amBody.sharedMaterial = skin; a.body = amBody; }
            else { if (am) Destroy(am); a.body = Prim(PrimitiveType.Sphere, a.t, new Vector3(0f, 0.45f, 0f), new Vector3(0.85f, 0.75f, 0.85f), skin).GetComponent<Renderer>(); }
            if (!amBody) for (int e = -1; e <= 1; e += 2)
            {
                Prim(PrimitiveType.Sphere, a.t, new Vector3(0.17f * e, 0.6f, 0.31f), Vector3.one * 0.24f, white);
                Prim(PrimitiveType.Sphere, a.t, new Vector3(0.17f * e, 0.61f, 0.41f), Vector3.one * 0.11f, black);
                Prim(PrimitiveType.Cylinder, a.t, new Vector3(0.14f * e, 0.95f, 0f), new Vector3(0.03f, 0.16f, 0.03f), skin).transform.localRotation = Quaternion.Euler(0f, 0f, -14f * e);
                Prim(PrimitiveType.Sphere, a.t, new Vector3(0.18f * e, 1.12f, 0f), Vector3.one * 0.1f, tip);
                Prim(PrimitiveType.Sphere, a.t, new Vector3(0.22f * e, 0.07f, 0.05f), new Vector3(0.26f, 0.14f, 0.34f), skin);
            }
            if (!amBody) Prim(PrimitiveType.Sphere, a.t, new Vector3(0f, 0.38f, 0.39f), new Vector3(0.22f, 0.06f, 0.06f), black);      // a little smile
            a.col = a.t.gameObject.AddComponent<SphereCollider>(); a.col.center = new Vector3(0f, 0.5f, 0f); a.col.radius = 0.55f;
            var tg = a.t.gameObject.AddComponent<HunchTarget>(); tg.kind = 1; tg.index = i;
            SetLayer(a.t.gameObject, 2);
            a.t.gameObject.SetActive(false);
            aliens.Add(a);
        }
    }

    void BuildUfo()
    {
        ufo = new GameObject("UFO").transform; ufo.SetParent(root, false); ufo.position = fieldC + Vector3.up * 30f;
        GameObject um = null; try { um = Kit(false, "Ufo", ufo, new Vector3(0f, -0.9f, 0f), 0f, 9f, false); } catch { um = null; }      // the saucer modelled in Blender
        if (um) { foreach (var r in um.GetComponentsInChildren<Renderer>()) { if (r.name == "Dome") r.sharedMaterial = Clear(new Color(0.4f, 1f, 0.9f, 0.45f)); else if (r.name == "Engine") r.sharedMaterial = Glow(new Color(0.6f, 2.4f, 1.6f)); } }
        else
        {
            Prim(PrimitiveType.Sphere, ufo, Vector3.zero, new Vector3(9f, 1.5f, 9f), Lit(new Color(0.55f, 0.58f, 0.64f)));
            Prim(PrimitiveType.Sphere, ufo, new Vector3(0f, 0.9f, 0f), new Vector3(4f, 2.4f, 4f), Clear(new Color(0.4f, 1f, 0.9f, 0.45f)));
            Prim(PrimitiveType.Sphere, ufo, new Vector3(0f, -0.6f, 0f), new Vector3(3f, 0.6f, 3f), Glow(new Color(0.6f, 2.4f, 1.6f)));
        }
        ufoLights = new GameObject("Lights").transform; ufoLights.SetParent(ufo, false);
        var a = Glow(new Color(2.6f, 0.6f, 0.4f)); var b = Glow(new Color(0.5f, 1.2f, 2.6f));
        for (int k = 0; k < 10; k++) { float an = k * 36f * Mathf.Deg2Rad; Prim(PrimitiveType.Sphere, ufoLights, new Vector3(Mathf.Sin(an) * 4.1f, -0.05f, Mathf.Cos(an) * 4.1f), Vector3.one * 0.5f, k % 2 == 0 ? a : b); }
        beam = Prim(PrimitiveType.Cylinder, root, Vector3.zero, Vector3.one, Clear(new Color(0.4f, 1.0f, 0.9f, 0.1f))).transform; beam.name = "UFO beam";
        beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.gameObject.SetActive(false);
        foreach (var r in ufo.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (HunchFx.Has("magic fire pro blue"))      // the UFO's engine: blue fire from the Procedural fire pack, pointing down
        {
            var fire = HunchFx.Spawn("magic fire pro blue", ufo.position + Vector3.down * 0.9f, 2.4f, 0f, ufo);
            if (fire) fire.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
        }
    }

    void UpdateUfo(float dt)
    {
        if (!ufo) return;
        if (ufo.gameObject.activeSelf != aliensOn) ufo.gameObject.SetActive(aliensOn);
        if (!aliensOn) { if (beam.gameObject.activeSelf) beam.gameObject.SetActive(false); return; }
        ufoAng += dt * 0.14f;
        ufo.position = fieldC + new Vector3(Mathf.Sin(ufoAng) * 18f, 22f + Mathf.Sin(Time.time * 0.8f) * 1.5f, Mathf.Cos(ufoAng) * 18f);
        ufo.rotation = Quaternion.Euler(Mathf.Sin(Time.time * 0.6f) * 4f, 0f, Mathf.Cos(Time.time * 0.5f) * 4f);
        ufoLights.Rotate(0f, 70f * dt, 0f, Space.Self);
        beamT -= dt; bool on = beamT > 0f;
        if (beam.gameObject.activeSelf != on) beam.gameObject.SetActive(on);
        if (on)
        {
            Vector3 from = ufo.position + Vector3.down * 0.6f, d = beamTo - from; float len = d.magnitude;
            beam.position = from + d * 0.5f; beam.up = d / Mathf.Max(0.01f, len); float w = 1.1f * Mathf.Clamp01(beamT * 2.5f);
            beam.localScale = new Vector3(w, len * 0.5f, w);
        }
    }

    void SpawnAlien(Alien a)
    {
        Vector3 p; if (!RandomGround(16f, 44f, out p)) return;
        a.t.position = p; a.gy = p.y; a.hp = 2f + (aliens.IndexOf(a) % 2); a.frozen = 0f; a.flash = 0f; a.flee = 0f; a.wait = 0f; a.goal = p; a.alive = true;
        a.t.localScale = Vector3.one * 0.05f; a.t.gameObject.SetActive(true);
        beamT = 1.3f; beamTo = p;
        if ((p - cam.position).sqrMagnitude < 120f * 120f) { HunchFx.Spawn("teleport", p, 0.5f, 1.2f); TeleFx(p, 1f); }
    }

    // the imported teleporter effect, when a pack with one has been added to the project
    void TeleFx(Vector3 p, float scale) { if (HunchFx.Has("pack teleporter")) HunchFx.Spawn("pack teleporter", p, scale * teleScale, 2.2f); }
    public float teleScale = 1f; public int friends;

    void Befriend(Alien a)
    {
        if (a.friend) return;
        if (friends >= 3) { Note("You already have 3 alien friends. The alien waves back."); HunchFx.Spawn("love hit", a.t.position + Vector3.up * 1.2f, 0.4f, 0.9f); return; }
        a.friend = true; friends++; a.hp = 99f; a.frozen = 0f; a.speed = 3.6f;
        HunchFx.Spawn("love hit", a.t.position + Vector3.up * 1.2f, 0.6f, 0.9f); HunchFx.Spawn("love aura", a.t.position, 0.5f, 3f);
        Fx2("confetti_blast_multicolor", null, a.t.position + Vector3.up * 1.4f, 0.35f, 1f, 1.2f);
        Play(cPop, 0.8f); game.Buzz(0.8f, 0.2f);
        Note("You made an alien friend! It follows you outside and fetches crystals and crates.");
        if (friends == 1) sim.Say("ARIA: First contact, Commander! The alien is friendly now. It will follow you around the field and fetch things for you.");
        if (kid) kid.Toast("New friend!", 3f);
    }

    void SendHome(Alien a)
    {
        Vector3 p = a.t.position;
        HunchFx.Spawn("teleport", p, 0.6f, 1.2f); HunchFx.Spawn("star hit", p + Vector3.up * 0.6f, 0.5f, 0.9f);
        beamT = 0.9f; beamTo = p;
        DropPickup(Crystal, 1, p);
        a.alive = false; a.t.gameObject.SetActive(false); a.respawn = R(7f, 14f);
        TeleFx(p, 1f); Fx2("flash_blue_purple", null, p + Vector3.up * 0.5f, 1f, 1f, 0.8f);
        sentHome++; Play(cPop, 0.7f); game.Buzz(0.7f, 0.15f);
        Note("Alien beamed home!  Sent home: " + sentHome);
        if (sentHome == 1) sim.Say("ARIA: Nice shot. The alien was beamed back to its ship and left a moon crystal. Walk over it to pick it up - crystals buy upgrades.");
        else if (sentHome % 10 == 0) sim.Say("ARIA: " + sentHome + " aliens sent home. The solar cables are safe, Commander.");
        if (kid && sentHome == 5) kid.Toast("Badge: Alien Wrangler!", 4f);
    }

    void HitAlien(Alien a, float dmg, bool freeze)
    {
        if (!a.alive || a.friend) return;
        a.hp -= dmg; a.flash = 0.16f; if (freeze) a.frozen = 4.5f;
        HunchFx.Spawn(freeze ? "sparks explode blue" : "electro hit", a.t.position + Vector3.up * 0.5f, 0.35f, 0.6f);
        if (a.hp <= 0f) SendHome(a);
    }

    void UpdateAliens(float dt)
    {
        Vector3 f = Feet(); bool outside = !fx || fx.Outside; bool spawnOk = aliensOn && Playing;
        for (int i = 0; i < aliens.Count; i++)
        {
            var a = aliens[i];
            if (!a.alive) { if (!spawnOk) continue; a.respawn -= dt; if (a.respawn <= 0f) { a.respawn = 4f; SpawnAlien(a); } continue; }
            if (!aliensOn) { if (a.friend) { a.friend = false; friends--; } a.alive = false; a.t.gameObject.SetActive(false); a.respawn = 2f; continue; }
            Vector3 pos = a.t.position; pos.y = a.gy;
            Vector3 toP = f - pos; toP.y = 0f; float dP = toP.magnitude;
            a.flee -= dt; a.touch -= dt; a.wait -= dt; a.flash -= dt;
            bool frozen = a.frozen > 0f; if (frozen) a.frozen -= dt;
            if (!frozen)
            {
                bool chase = outside && dP < 42f && a.flee <= 0f && Mathf.Abs(f.y - a.gy) < 6f;
                if (a.friend)
                {
                    // a friend trots along beside you, and waits in the field while you are indoors
                    a.wait = 1f;
                    if (outside) { Vector3 sideDir = Quaternion.Euler(0f, 70f + i * 55f, 0f) * Vector3.forward; a.goal = f + sideDir * 2.4f; }
                }
                else if (chase) a.goal = f;
                else if (a.wait <= 0f || (a.goal - pos).sqrMagnitude < 3f) { Vector3 g; RandomGround(8f, 44f, out g); a.goal = g; a.wait = R(5f, 11f); }
                Vector3 d = a.goal - pos; d.y = 0f;
                if (d.magnitude > 1.3f)
                {
                    Vector3 dir = d.normalized; pos += dir * a.speed * dt;
                    a.t.rotation = Quaternion.Slerp(a.t.rotation, Quaternion.LookRotation(dir, Vector3.up), dt * 6f);
                    a.phase += dt * 7.5f;
                }
                a.check -= dt;
                if (a.check <= 0f)
                {
                    a.check = 0.25f; Vector3 g;
                    if (GroundAt(pos.x, pos.z, out g)) a.gy = Mathf.Lerp(a.gy, g.y, 0.6f);
                    else if (!a.friend) { a.goal = fieldC; a.flee = 3f; a.wait = 4f; }      // walked off the field: turn back
                }
                if (dP < 1.5f && a.touch <= 0f && outside && !a.friend)
                {
                    a.touch = 4f; a.flee = 3.5f; a.wait = 3.5f; a.goal = pos - (dP > 0.01f ? toP / dP : Vector3.forward) * 12f;
                    Play(cBoop, 0.7f); game.Buzz(0.8f, 0.2f);
                    if (RealMission && !sim.kidMode) sim.battery = Mathf.Max(0f, sim.battery - 2f);
                    if (Creative) Note("Boop! An alien bumped you. Zap it to send it home.");
                    else Hurt(sim.kidMode ? 4f : 8f, "Ouch! An alien bumped you. Zap it to send it home.");
                }
            }
            float hop = Mathf.Abs(Mathf.Sin(a.phase)) * 0.4f, grow = Mathf.Min(1f, a.t.localScale.x + dt * 2.5f);
            a.t.position = new Vector3(pos.x, a.gy + hop, pos.z);
            a.t.localScale = new Vector3(grow, grow * (1f - 0.12f * Mathf.Cos(a.phase * 2f)), grow);
            Color c = a.flash > 0f ? Color.white * 1.6f : (frozen ? Color.Lerp(a.colour, new Color(0.6f, 0.9f, 1.3f), 0.75f) : (a.friend ? Color.Lerp(a.colour, new Color(1.3f, 0.7f, 0.9f), 0.45f + 0.15f * Mathf.Sin(Time.time * 4f)) : a.colour));
            a.block.SetColor("_BaseColor", c); a.body.SetPropertyBlock(a.block);
        }
    }

    // ------------------------------------------------------------------ tools: model in the hand, beams, plasma, drill, blocks
    Transform toolModel, tipT; Renderer tipR; Material[] tipMats; int shownTool = -1;
    class BeamFx { public LineRenderer lr; public float t; }
    readonly List<BeamFx> beams = new List<BeamFx>(); LineRenderer drillBeam; Transform ball; Vector3 ballVel; float ballLife; Material beamGreen, beamCyan;
    float cool, digT, blockT, sparkT; bool drilling;
    readonly List<Transform> blocks = new List<Transform>(); Transform blockRoot; Material[] blockMats;

    void BuildToolModel()
    {
        toolModel = new GameObject("Tool in hand").transform;
        var dark = Lit(new Color(0.16f, 0.17f, 0.2f)); var grey = Lit(new Color(0.6f, 0.62f, 0.66f));
        Prim(PrimitiveType.Cube, toolModel, new Vector3(0f, -0.07f, -0.04f), new Vector3(0.045f, 0.12f, 0.05f), dark);
        Prim(PrimitiveType.Cube, toolModel, new Vector3(0f, 0f, 0.03f), new Vector3(0.06f, 0.07f, 0.26f), grey);
        Prim(PrimitiveType.Cylinder, toolModel, new Vector3(0f, 0.005f, 0.22f), new Vector3(0.034f, 0.08f, 0.034f), dark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        tipT = Prim(PrimitiveType.Sphere, toolModel, new Vector3(0f, 0.005f, 0.31f), Vector3.one * 0.05f, grey).transform; tipR = tipT.GetComponent<Renderer>();
        tipMats = new[] { grey, Glow(new Color(0.6f, 2.6f, 0.5f)), Glow(new Color(0.5f, 1.8f, 2.8f)), Glow(new Color(2.6f, 0.6f, 2.4f)), Glow(new Color(2.8f, 1.4f, 0.3f)), Glow(new Color(2.2f, 2.2f, 2.2f)) };
        foreach (var r in toolModel.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        toolModel.gameObject.SetActive(false);
        blockRoot = new GameObject("Placed blocks").transform; blockRoot.SetParent(root, false);
        blockMats = new[] { Lit(new Color(0.82f, 0.8f, 0.76f)), Lit(new Color(0.72f, 0.7f, 0.68f)), Lit(new Color(0.9f, 0.87f, 0.82f)) };
    }

    // If a weapon pack has been imported (HUNCH > Use imported effect packs lists its models), its guns replace the simple block gun.
    readonly List<GameObject> gunModels = new List<GameObject>(); readonly List<Renderer> blockGun = new List<Renderer>(); int shownGun = -2;

    void BuildGunModels()
    {
        foreach (var r in toolModel.GetComponentsInChildren<Renderer>(true)) if (r != tipR) blockGun.Add(r);
        foreach (var n in HunchFx.NamesStarting("gun:"))
        {
            if (gunModels.Count >= 4) break;
            if (n.Contains("motion") || n.Contains("sentry") || n.Contains("turret")) continue;      // that one is a turret: BuildTurret uses it
            var prefab = HunchFx.Prefab(n); if (!prefab) continue;
            long tris = 0; foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh) for (int sm = 0; sm < mf.sharedMesh.subMeshCount; sm++) tris += mf.sharedMesh.GetIndexCount(sm) / 3;
            if (game.HeadsetActive && tris > 500000) { Debug.Log("[HUNCH] gun model " + n + " has " + tris + " triangles: too heavy for VR, keeping the simple gun in the headset"); continue; }
            GameObject g = null;
            try
            {
                g = Instantiate(prefab); g.name = n; g.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var c in g.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true)) if (mb) mb.enabled = false;
                foreach (var au in g.GetComponentsInChildren<AudioSource>(true)) au.enabled = false;
                foreach (var ps in g.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
                foreach (var rb in g.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
                var rs = g.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) { Destroy(g); continue; }
                Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                Vector3 sz = b.size; float longest = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
                if (longest < 0.0001f) { Destroy(g); continue; }
                // turn the longest side to point forward and make it 42 cm long
                Quaternion rot = Quaternion.identity;
                if (sz.x >= sz.y && sz.x >= sz.z) rot = Quaternion.Euler(0f, -90f, 0f); else if (sz.y >= sz.x && sz.y >= sz.z) rot = Quaternion.Euler(90f, 0f, 0f);
                var holder = new GameObject("Gun " + gunModels.Count); holder.transform.SetPositionAndRotation(b.center, Quaternion.identity);
                g.transform.SetParent(holder.transform, true);
                holder.transform.rotation = rot; holder.transform.localScale = Vector3.one * (0.42f / longest);
                holder.transform.SetParent(toolModel, false); holder.transform.localPosition = new Vector3(0f, -0.02f, 0.1f); holder.transform.localRotation = rot;
                foreach (var r in rs) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                holder.SetActive(false); gunModels.Add(holder);
            }
            catch (Exception e) { Debug.LogWarning("[HUNCH] could not use gun model " + n + ": " + e.Message); if (g) Destroy(g); }
        }
        if (gunModels.Count > 0) Debug.Log("[HUNCH] using " + gunModels.Count + " imported gun models");
    }

    void BuildShots()
    {
        beamGreen = Glow(new Color(0.7f, 3f, 0.6f)); beamCyan = Glow(new Color(0.6f, 2f, 3f));
        for (int i = 0; i < 4; i++) { var b = new BeamFx { lr = Line("Beam", beamGreen, 0.05f, root) }; b.lr.enabled = false; beams.Add(b); }
        drillBeam = Line("Drill beam", Glow(new Color(3f, 1.5f, 0.3f)), 0.035f, root); drillBeam.enabled = false;
        ball = Prim(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.45f, Glow(new Color(2.8f, 0.7f, 2.8f))).transform; ball.name = "Plasma ball"; ball.gameObject.SetActive(false);
    }

    // A ray for the tools and the hand. It ignores the player's own body and hands, which the camera sits inside.
    static readonly RaycastHit[] hitBuf = new RaycastHit[16];
    bool Cast(Ray ray, out RaycastHit best, float dist)
    {
        int n = Physics.RaycastNonAlloc(ray, hitBuf, dist, ~0, QueryTriggerInteraction.Ignore); best = default(RaycastHit); float bd = float.MaxValue; bool any = false;
        for (int i = 0; i < n; i++)
        {
            var c = hitBuf[i].collider;
            if (!c || c is CharacterController || c.transform.IsChildOf(origin.transform)) continue;
            if (hitBuf[i].distance < bd) { bd = hitBuf[i].distance; best = hitBuf[i]; any = true; }
        }
        return any;
    }

    Ray AimRay()
    {
        Ray aim; if (game.HeadsetActive && HunchVr.TryAim(out aim)) return aim;
        if (game.HeadsetActive && rightCtrl) return new Ray(rightCtrl.position + rightCtrl.forward * 0.05f, rightCtrl.forward);
        return new Ray(cam.position, cam.forward);
    }
    Vector3 Muzzle() { return toolModel.gameObject.activeInHierarchy ? tipT.position : cam.position + cam.forward * 0.5f - cam.up * 0.2f; }

    public void NextTool()
    {
        if (hunchOn) { tool = Hands; Note("NASA HUNCH mode: tools are put away."); return; }
        do { tool = (tool + 1) % ToolNames.Length; } while (tool == Plasma && !plasmaUnlocked);
        Note("Tool: " + ToolNames[tool] + ToolTip()); Play(cPick, 0.35f); game.Buzz(0.3f, 0.05f);
    }
    string ToolTip()
    {
        bool vr = game.HeadsetActive; string fire = vr ? "right trigger" : "left click";
        switch (tool)
        {
            case Zapper: return "  -  " + fire + " to zap aliens";
            case Freeze: return "  -  " + fire + " freezes an alien for a few seconds";
            case Plasma: return "  -  " + fire + " launches a ball that bursts";
            case Drill: return "  -  hold " + fire + " on glowing rocks, blocks, or the ground outside";
            case Builder: return "  -  " + fire + " places a block" + (vr ? " (the drill removes one)" : ", right click removes one");
            case PlasmaRep: case Laser: case Minigun: case Sword: case NinjaTool: return ExtraToolTip(fire);
            default: return "  -  " + (vr ? "trigger" : "click or E") + " acts in a zone ring";
        }
    }

    void ShowBeam(Vector3 from, Vector3 to, Material m)
    {
        foreach (var b in beams) if (b.t <= 0f) { b.t = 0.08f; b.lr.sharedMaterial = m; b.lr.SetPosition(0, from); b.lr.SetPosition(1, to); b.lr.enabled = true; return; }
    }

    void FireBeam(Ray ray, bool freeze)
    {
        float range = mapNow == 8 ? 700f : 70f;      // in space the beams reach much further
        RaycastHit h; Vector3 end = ray.origin + ray.direction * range;
        if (Cast(ray, out h, range))
        {
            end = h.point; var tg = h.collider.GetComponentInParent<HunchTarget>();
            if (tg && tg.kind == 1 && tg.index < aliens.Count) HitAlien(aliens[tg.index], freeze ? 1f : zapLv, freeze);
            else if (tg && tg.kind == 32) ZrHit(tg.index);
            else if (tg && tg.kind == 36) { if (tg.index >= 200) BossHit(tg.index - 200); else StarHit(tg.index); }
            else if (tg && tg.kind == 37) SliceHit(tg.index);
            else if (mapNow == 3) zrCombo = 0;
        }
        if (mapNow == 3 && mgOn) zrShots++;
        recoil = freeze ? 0.6f : 1f;
        ShowBeam(Muzzle(), end, freeze ? beamCyan : beamGreen); Play(freeze ? cFreeze : cZap, 0.45f); game.Buzz(0.3f, 0.04f);
    }

    void FirePlasma(Ray ray)
    {
        recoil = 1.6f;
        ball.position = Muzzle(); ballVel = ray.direction * 36f; ballLife = 2.6f; ball.gameObject.SetActive(true);
        Play(cFreeze, 0.6f); game.Buzz(0.6f, 0.1f);
    }

    void Burst(Vector3 p)
    {
        ball.gameObject.SetActive(false); ballLife = 0f;
        HunchFx.Spawn("sparks explode blue", p, 2.2f, 0.9f); HunchFx.Spawn("electro hit", p, 1.4f, 0.9f); Fx2("explosion", null, p, 1f, 1f, 1.5f);
        Play(cBoom, 0.8f); game.Buzz(1f, 0.25f);
        foreach (var a in aliens) if (a.alive && (a.t.position - p).sqrMagnitude < 36f) HitAlien(a, 3f, false);
    }

    void UpdateShots(float dt)
    {
        foreach (var b in beams) if (b.t > 0f) { b.t -= dt; if (b.t <= 0f) b.lr.enabled = false; }
        if (ballLife > 0f)
        {
            ballLife -= dt; RaycastHit h; float step = ballVel.magnitude * dt;
            if (Physics.SphereCast(ball.position, 0.2f, ballVel.normalized, out h, step, ~0, QueryTriggerInteraction.Ignore)) Burst(h.point);
            else if (ballLife <= 0f) Burst(ball.position);
            else ball.position += ballVel * dt;
        }
        if (drillBeam.enabled && !drilling) drillBeam.enabled = false;
        drillSrc.volume = Mathf.Lerp(drillSrc.volume, drilling && (!fx || fx.sfxOn) ? 0.35f : 0f, dt * 10f);
        if (drilling && !drillSrc.isPlaying) drillSrc.Play(); else if (!drilling && drillSrc.isPlaying && drillSrc.volume < 0.01f) drillSrc.Stop();
        drilling = false;
    }

    void Harvest(Node n)
    {
        int count = n.yield > 0 ? n.yield : (n.item == He3 ? 1 : 2), got = AddItem(n.item, count);
        if (n.stages > 1)      // a deep vein: this stage is worked out, the next one is harder to reach
        {
            n.stages--; n.hp = n.maxHp; minedCount++;
            HunchFx.Spawn("stones hit", n.t.position + Vector3.up * 0.6f, 0.8f, 0.9f); Play(cPick, 0.7f); game.Buzz(0.6f, 0.12f);
            Note("+" + got + " " + ItemNames[n.item] + "   Deep vein: " + n.stages + " stage" + (n.stages > 1 ? "s" : "") + " left");
            return;
        }
        n.up = false; n.regrow = 40f; n.col.enabled = false; minedCount++;
        HunchFx.Spawn("stones hit", n.t.position + Vector3.up * 0.4f, 0.6f, 0.9f); Play(cPick, 0.7f); game.Buzz(0.6f, 0.12f);
        Fx2(n.item == Ice ? "water_blast_blue" : (n.item == He3 ? "flash_magic_ellow_blue" : "flash_ellow"), null, n.t.position + Vector3.up * 0.6f, 1f, 1f, 0.9f);
        if (got > 0) Note("+" + got + " " + ItemNames[n.item] + "   (I opens the backpack)");
        if (minedCount == 1) sim.Say("ARIA: First minerals mined. Open the backpack to use them: ice becomes water, ilmenite ore gives oxygen, helium-3 gives power.");
    }

    void DrillAt(Ray ray, float dt)
    {
        RaycastHit h;
        if (!Cast(ray, out h, 7.5f)) { digT = blockT = 0f; return; }
        drilling = true; drillBeam.enabled = true; drillBeam.SetPosition(0, Muzzle()); drillBeam.SetPosition(1, h.point);
        sparkT -= dt; bool spark = sparkT <= 0f; if (spark) { sparkT = 0.3f; game.Buzz(0.25f, 0.05f); }
        var tg = h.collider.GetComponentInParent<HunchTarget>();
        if (tg && tg.kind == 2 && tg.index < nodes.Count)
        {
            var n = nodes[tg.index]; n.hp -= dt * 1.3f * drillLv;
            if (spark) HunchFx.Spawn("sparks yellow", h.point, 0.25f, 0.4f);
            if (n.hp <= 0f && n.up) Harvest(n);
        }
        else if (tg && tg.kind == 31) PitDrill(tg.index, dt, h.point, spark);
        else if (tg && tg.kind == 34) FacDrill(tg.index, dt, h.point, spark);
        else if (tg && tg.kind == 3)
        {
            blockT += dt * drillLv;
            if (blockT > 0.35f) { blockT = 0f; RemoveBlock(tg.transform); }
        }
        else if (!tg && h.normal.y > 0.5f && (!fx || fx.Outside))
        {
            digT += dt * drillLv;
            if (spark) HunchFx.Spawn("dust puff", h.point, 0.1f, 0.8f);
            if (digT > 1.0f) { digT = 0f; if (AddItem(Block, 1) > 0) { Note("+1 Regolith block  (" + bag[Block] + ")"); Play(cThud, 0.5f); } }
        }
    }

    void RemoveBlock(Transform b)
    {
        if (!blocks.Remove(b)) return;
        HunchFx.Spawn("dust puff", b.position, 0.15f, 0.8f); Play(cThud, 0.6f);
        Destroy(b.gameObject); AddItem(Block, 1);
    }

    void BreakBlock(Ray ray)
    {
        RaycastHit h;
        if (!Cast(ray, out h, 9f)) return;
        var tg = h.collider.GetComponentInParent<HunchTarget>();
        if (tg && tg.kind == 3) RemoveBlock(tg.transform);
    }

    static Vector3 Axis(Vector3 n)
    {
        float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
        if (ay >= ax && ay >= az) return new Vector3(0f, Mathf.Sign(n.y), 0f);
        return ax >= az ? new Vector3(Mathf.Sign(n.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(n.z));
    }

    void PlaceBlock(Ray ray)
    {
        if (bag[Block] <= 0 && !Creative) { Note("No blocks left. Dig the ground outside with the Mining drill to get more."); return; }
        if (blocks.Count >= 400) { Note("That is the most blocks the base can hold (400)."); return; }
        RaycastHit h;
        if (!Cast(ray, out h, 9f)) { Note("Too far. Aim at the ground, a wall or another block."); return; }
        var tg = h.collider.GetComponentInParent<HunchTarget>(); Vector3 p;
        if (tg && tg.kind == 3) p = tg.transform.position + Axis(h.normal);
        else
        {
            Vector3 q = h.point + h.normal * 0.5f;
            p = new Vector3(Mathf.Floor(q.x) + 0.5f, h.normal.y > 0.6f ? h.point.y + 0.5f : q.y, Mathf.Floor(q.z) + 0.5f);
        }
        Vector3 f = Feet();
        if (Mathf.Abs(p.x - f.x) < 0.66f && Mathf.Abs(p.z - f.z) < 0.66f && p.y > f.y - 0.5f && p.y < f.y + 2.2f) { Note("Too close. Step back a little."); return; }
        foreach (var o in blocks) { Vector3 d = o.position - p; if (Mathf.Abs(d.x) < 0.9f && Mathf.Abs(d.y) < 0.9f && Mathf.Abs(d.z) < 0.9f) { Note("There is already a block there."); return; } }
        var g = Prim(PrimitiveType.Cube, blockRoot, Vector3.zero, Vector3.one, blockMats[rng.Next(blockMats.Length)], true);
        g.name = "Block"; g.transform.position = p; var t = g.AddComponent<HunchTarget>(); t.kind = 3; t.index = blocks.Count;
        blocks.Add(g.transform); if (!Creative) bag[Block]--; placedCount++;
        Play(cThud, 0.7f); game.Buzz(0.4f, 0.06f);
        if (placedCount == 1) sim.Say("ARIA: First block placed. Stack them to build walls, steps or a shelter against the aliens.");
    }

    // what the hand is pointing at: buttons, rovers, rocks, aliens to befriend
    HunchTarget focus; string focusText = "";

    void UpdateFocus()
    {
        focus = null; focusText = "";
        if (!Playing || game.menuOpen) return;
        if (ride != null && cam.forward.y < -0.6f) { focus = ride.t.GetComponent<HunchTarget>(); focusText = "get off the rover"; return; }
        if (heldRock != null) return;
        RaycastHit h; float reach = mapNow == 6 || mapNow == 8 || mapNow == 13 ? 75f : (mapNow == 10 ? 60f : (mapNow == 11 || mapNow == 12 ? 16f : (game.HeadsetActive ? 9f : 5.5f)));
        if (!Cast(AimRay(), out h, reach)) return;
        var tg = h.collider.GetComponentInParent<HunchTarget>(); if (!tg) return;
        switch (tg.kind)
        {
            case 1: if (tool == Hands && friends < 3 && tg.index < aliens.Count && aliens[tg.index].alive && !aliens[tg.index].friend) { focus = tg; focusText = "say hello and make friends"; } break;
            case 10: if (tg.index < rovers.Count && rovers[tg.index] != ride) { focus = tg; focusText = "ride the rover"; } break;
            case 11: if (tg.index < consoles.Count) { focus = tg; focusText = game.zones[tg.index].title + " lights: " + (consoles[tg.index].lightOn ? "ON (switch off)" : "OFF (switch on)"); } break;
            case 12: if (tg.index < vents.Count) { focus = tg; focusText = game.zones[tg.index].title + " fan: " + (vents[tg.index].on ? "ON (switch off)" : "OFF (switch on)"); } break;
            case 13: if (tg.index < rocks.Count) { focus = tg; focusText = "pick up the moon rock"; } break;
            case 20: focus = tg; focusText = StationPrompt(tg.index); break;
            case 30: focus = tg; focusText = "tilt solar panel " + (tg.index / 2 + 1) + (tg.index % 2 == 0 ? " west (-10 degrees)" : " east (+10 degrees)"); break;
            case 31: focusText = PitInfo(tg.index); break;      // no click action: the drill does the work
            case 33: focus = tg; focusText = TdPadText(tg.index); break;
            case 34: focusText = FacDepText(tg.index); break;      // the drill does the work
            case 35: focusText = FacText(tg.index); if (focusText.Length > 0) focus = tg; break;
            case 36: focusText = tg.index >= 200 ? "tickle spot: zap it!" : StarText(tg.index); break;
            case 40: focus = tg; focusText = PickText(tg.index); break;
            case 41: focus = tg; focusText = batHeld > 0 ? "put " + batHeld + " battery module" + (batHeld > 1 ? "s" : "") + " into the Battery Bank" : "Battery Bank: " + batBank + " of " + BatSlots; break;
            case 43: focus = tg; focusText = mtState == 1 ? "start the team meeting" : "ring the bell and call a team meeting"; break;
            case 44: focus = tg; focusText = "open the Crew Health System"; break;
            case 46: focus = tg; focusText = tg.index < 0 ? "choose a map" : (tg.index == 0 ? "go back to the central base" : (tg.index >= 100 ? "play " + MapNames[tg.index - 100] + " again" : "go to " + MapNames[tg.index])); break;
        }
    }

    void Activate(HunchTarget tg)
    {
        switch (tg.kind)
        {
            case 1: if (tg.index < aliens.Count) Befriend(aliens[tg.index]); break;
            case 10: if (ride != null && tg == ride.t.GetComponent<HunchTarget>()) Dismount(null); else if (tg.index < rovers.Count) Mount(rovers[tg.index]); break;
            case 11: SetZoneLight(tg.index, !consoles[tg.index].lightOn); break;
            case 12: SetZoneFan(tg.index, !vents[tg.index].on); break;
            case 13: PickRock(rocks[tg.index]); break;
            case 20: StationPress(tg.index); break;
            case 30: SolPress(tg.index); break;
            case 33: TdPad(tg.index); break;
            case 35: FacUse(tg.index); break;
            case 40: TakePick(tg.index); break;
            case 41: BankInsert(); break;
            case 43: MeetingGo(); break;
            case 44: if (menu && pageHealth != null) menu.OpenExtra(pageHealth); break;
            case 46: PortalGo(tg.index, tg.transform.parent); break;
        }
    }

    bool HoveringOrb() { foreach (var z in game.zones) if (z.interactable && (z.interactable.isHovered || HunchVr.Hovering(z.interactable.transform))) return true; return false; }

    void UpdateInput(float dt)
    {
        var kb = Keyboard.current; var mouse = Mouse.current; bool vr = game.HeadsetActive;
        if (readOpen >= 0 || (cvOn && !vr)) { wasHeld = true; return; }      // reading a sheet or watching the cameras: the hands are busy
        if (!vr && kb != null && !autoOn)
        {
            if (kb.rKey.wasPressedThisFrame) NextTool();
            if (kb.iKey.wasPressedThisFrame && menu && pageBag != null) { menu.OpenExtra(pageBag); return; }
            if (kb.oKey.wasPressedThisFrame) SetLights(!lightsOn);
            if (kb.jKey.wasPressedThisFrame) { GoField(); return; }
        }
        bool held = false, pressed = false, alt = false;
        if (autoOn) { }                                                         // the autopilot pulls the trigger itself (probeHold)
        else if (vr) { held = ((fireVr != null && fireVr.ReadValue<float>() > 0.55f) || HunchVr.RightTrigger > 0.55f) && !HoveringOrb(); }
        else if (mouse != null && Cursor.lockState == CursorLockMode.Locked && !game.uiHasMouse) { held = mouse.leftButton.isPressed; alt = mouse.rightButton.wasPressedThisFrame; }
        if (Time.time < probeHold) held = true;
        if (blastOn) { held = false; pressed = false; }                              // in the gunner's seat the trigger fires the big lasers only
        pressed = held && !wasHeld; wasHeld = held;
        if (vr && QuickInput(pressed)) return;      // a big button on the dashboard took the trigger pull
        cool -= dt;
        if (pressed && heldRock != null) { ThrowRock(); cool = 0.3f; return; }
        if (pressed && focus != null) { Activate(focus); cool = 0.3f; return; }
        if (tool == Hands) { SmartUse(held, dt); return; }
        Ray ray = AimRay();
        switch (tool)
        {
            case Zapper: if (held && cool <= 0f) { cool = 0.22f; FireBeam(ray, false); } break;
            case Freeze: if (held && cool <= 0f) { cool = 0.5f; FireBeam(ray, true); } break;
            case Plasma: if (pressed && cool <= 0f && ballLife <= 0f) { cool = 0.9f; FirePlasma(ray); } break;
            case Drill: if (held) DrillAt(ray, dt); else { digT = 0f; blockT = 0f; } break;
            case Builder: if (pressed && cool <= 0f) { cool = 0.15f; PlaceBlock(ray); } if (alt) BreakBlock(ray); break;
            default: ExtraToolFire(ray, held, pressed); break;
        }
    }

    // ------------------------------------------------------------------ movement helpers (VR run and jetpack)
    object mover; PropertyInfo pSpeed, pFly; float baseSpeed = 2.5f, curSpeed = -1f; bool moverSearched, flyApplied; float deskRun, deskFly, deskWalk;

    void FindMover()
    {
        moverSearched = true;
        foreach (var mb in origin.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!mb) continue;
            var t = mb.GetType(); var ps = t.GetProperty("moveSpeed"); var pf = t.GetProperty("enableFly");
            if (ps == null || pf == null || ps.PropertyType != typeof(float) || pf.PropertyType != typeof(bool)) continue;
            mover = mb; pSpeed = ps; pFly = pf; baseSpeed = (float)ps.GetValue(mb, null); break;
        }
        if (desk) { deskRun = desk.runSpeed; deskFly = desk.flySpeed; deskWalk = desk.walkSpeed; }
    }

    public void ToggleFly()
    {
        if (!game.flyMode && !HunchCheats.FlyOk) { Note("Flying is switched off. Menu > Cheats and anti-cheat."); return; }
        game.flyMode = !game.flyMode;
        Note(game.flyMode ? (game.HeadsetActive ? "Jetpack ON: push the stick and look where you want to go. Hold A to rise." : "Fly mode ON (F)") : "Jetpack OFF");
        Play(cPop, 0.4f); game.Buzz(0.5f, 0.1f);
    }

    void UpdateMove(float dt)
    {
        if (!moverSearched) FindMover();
        float jet = 1f + 0.3f * (jetLv - 1);
        float rideMul = ride != null ? 2.2f : 1f;
        if (desk && deskRun > 0f) { desk.runSpeed = deskRun * jet * (ride != null ? 1.6f : 1f); desk.flySpeed = deskFly * jet; desk.walkSpeed = deskWalk * rideMul; }
        if (!game.HeadsetActive) return;
        if (mover != null)
        {
            float want = baseSpeed * (runOn ? 2.2f : 1f) * (game.flyMode ? 1.8f : 1f) * jet * rideMul;
            try
            {
                if (Mathf.Abs(curSpeed - want) > 0.01f) { pSpeed.SetValue(mover, want, null); curSpeed = want; }
                if (flyApplied != game.flyMode) { pFly.SetValue(mover, game.flyMode, null); flyApplied = game.flyMode; }
            }
            catch { mover = null; }
        }
        if (game.flyMode && !game.menuOpen && ((jumpVr != null && jumpVr.IsPressed()) || HunchVr.A))
        {
            Vector3 up = Vector3.up * 5f * jet * dt; var cc = origin.GetComponent<CharacterController>();
            if (cc && cc.enabled && cc.gameObject.activeInHierarchy) cc.Move(up); else origin.transform.position += up;
        }
    }

    // ------------------------------------------------------------------ using what is in the backpack, and upgrades
    public void UseItem(int item)
    {
        if (bag[item] <= 0) { Note("You have no " + ItemNames[item] + "."); return; }
        switch (item)
        {
            case Ice: bag[item]--; sim.water = Mathf.Min(100f, sim.water + 12f); sim.Say("ARIA: Ice melted and filtered. Water +12%."); break;
            case Ore: bag[item]--; sim.oxygen = Mathf.Min(100f, sim.oxygen + 10f); sim.Say("ARIA: Ilmenite heated in the reactor. The oxygen locked in the rock is now in our tanks: oxygen +10%."); break;
            case He3: bag[item]--; sim.battery = Mathf.Min(100f, sim.battery + 15f); sim.Say("ARIA: Helium-3 loaded. Power +15%."); break;
            case Cell: bag[item]--; sim.battery = Mathf.Min(100f, sim.battery + 25f); sim.Say("ARIA: Power cell plugged in. Power +25%."); break;
            case Crystal: Note("Crystals buy upgrades. Open Upgrades."); return;
            case Block: tool = Builder; Note("Block builder selected. Aim and " + (game.HeadsetActive ? "pull the right trigger" : "click") + " to place."); return;
        }
        itemsUsed++;
        Play(cPick, 0.6f); Fx2(item == Ice ? "water_blast_blue" : "flash_magic_ellow_blue", "sparks explode green", cam.position + cam.forward * 1.8f, 0.6f, 0.5f, 0.8f);
    }

    static readonly string[] UpNames = { "Zapper Mk2", "Plasma launcher", "Drill Mk2", "Bigger backpack", "Jetpack boost", "Auto-miner rig", "Solar array boost", "Guard turret", "Dyson swarm panel" };
    static readonly string[] UpHints = {
        "The zapper hits twice as hard, so most aliens go home in one or two zaps.", "A new tool: fires a glowing ball that bursts and sends home every alien within 6 metres.",
        "The drill mines rocks, blocks and regolith twice as fast.", "Carry twice as much of everything.", "Run and fly 30% faster.",
        "A robot rig in the mining field that digs by itself and sends ice, ore and sometimes helium-3 straight to your backpack. You can buy up to three.",
        "Cleans and re-angles the solar arrays: arrays back to 100% and power +20%.",
        "A robot turret in the mining field that zaps any alien that comes within 28 metres (it never zaps your alien friends). You can buy two.",
        "Launches one more collector panel into orbit round the Sun. Each panel beams about 1 kW to the receiver dish by the mining field, day and night. You start with 3 and can have 8." };
    static readonly int[][] UpCost = {      // ice, ore, he3, crystal
        new[] { 0, 0, 0, 3 }, new[] { 0, 2, 0, 5 }, new[] { 0, 4, 0, 0 }, new[] { 2, 3, 0, 0 }, new[] { 0, 0, 2, 2 }, new[] { 0, 6, 0, 2 }, new[] { 0, 4, 0, 3 }, new[] { 0, 3, 0, 2 }, new[] { 0, 3, 2, 2 } };

    bool Owned(int k) { switch (k) { case 0: return zapLv > 1; case 1: return plasmaUnlocked; case 2: return drillLv > 1; case 3: return packLv > 1; case 4: return jetLv > 1; case 5: return rigCount >= 3; case 7: return turrets.Count >= 2; case 8: return dysonCount >= DysonMax; default: return false; } }
    static string CostText(int k) { var sb = new StringBuilder(); for (int i = 0; i < 4; i++) if (UpCost[k][i] > 0) sb.Append(sb.Length > 0 ? ", " : "").Append(UpCost[k][i]).Append(" ").Append(ItemNames[i].ToLower()); return sb.ToString(); }
    bool CanPay(int k) { if (Creative) return true; for (int i = 0; i < 4; i++) if (bag[i] < UpCost[k][i]) return false; return true; }

    public void Buy(int k)
    {
        if (Owned(k)) { Note("You already have " + UpNames[k] + "."); return; }
        if (!CanPay(k)) { Note(UpNames[k] + " needs " + CostText(k) + "."); Play(cBoop, 0.5f); return; }
        if (!Creative) for (int i = 0; i < 4; i++) bag[i] -= UpCost[k][i];
        switch (k)
        {
            case 0: zapLv = 2; break;
            case 1: plasmaUnlocked = true; tool = Plasma; break;
            case 2: drillLv = 2; break;
            case 3: packLv = 2; break;
            case 4: jetLv = 2; break;
            case 5: rigCount++; BuildRig(); break;
            case 6: sim.arrays = 100f; sim.battery = Mathf.Min(100f, sim.battery + 20f); solarUp = true; break;
            case 7: BuildTurret(); break;
            case 8: dysonCount++; break;
        }
        Play(cPop, 0.8f); game.Buzz(0.8f, 0.2f); Fx2("flash_star_ellow_purple", "sparks explode yellow", cam.position + cam.forward * 1.8f, 0.6f, 0.5f, 0.8f);
        sim.Say("ARIA: Upgrade installed: " + UpNames[k] + ".");
    }

    // ------------------------------------------------------------------ menu pages
    HunchMenu.ExtraPage pageRoot, pageBag, pageUp, pageGrid;

    void RegisterMenu()
    {
        if (!menu) return;
        pageRoot = new HunchMenu.ExtraPage { title = "EXPLORER", label = "Explorer: tools, backpack, upgrades, power", hint = "Choose a tool or weapon, run and fly, go to the mining field, open your backpack, buy upgrades, and switch the lights, wiring and ventilation." };
        pageBag = new HunchMenu.ExtraPage { title = "BACKPACK", parent = pageRoot };
        pageUp = new HunchMenu.ExtraPage { title = "UPGRADES", parent = pageRoot };
        pageGrid = new HunchMenu.ExtraPage { title = "POWER, LIGHTS AND AIR", parent = pageRoot };
        pageRoot.build = m =>
        {
            bool vr = game.HeadsetActive;
            m.AddItem("Tool: " + ToolNames[tool] + "   (change)", "Hands, Zapper, Freeze ray, Plasma launcher (an upgrade), Mining drill, Block builder, Plasma repeater, Laser cannon, Star minigun, Comet sword, Ninja stars. " + (vr ? "B also changes the tool. Right trigger uses it." : "R also changes the tool. Left click uses it."), () => { NextTool(); m.Rebuild(); });
            if (vr) m.AddItem("Run: " + (runOn ? "ON" : "OFF"), "Walk more than twice as fast. Left stick click does the same.", () => { runOn = !runOn; m.Rebuild(); });
            m.AddItem("Jetpack fly: " + (game.flyMode ? "ON" : "OFF"), vr ? "Push the left stick and look where you want to go; hold A to rise. The JETPACK button under the dashboard card does the same." : "Fly anywhere. F does the same. Space rises, C sinks.", () => { ToggleFly(); m.Rebuild(); });
            if (ride != null) m.AddItem("Get off the rover", "Steps off the rover you are riding.", () => { m.Close(); Dismount(null); });
            if (fieldOk) m.AddItem("Go to the mining field", "Outside the base: glowing rocks to mine, aliens to send home, rovers, and the UFO.", () => { m.Close(); GoField(); });
            m.AddItem("Go back inside (airlock)", "Returns you to the airlock.", () => { m.Close(); int ai = game.ZoneIndex("airlock"); game.GoTo(ai >= 0 ? ai : 0); });
            if (vr) m.AddItem("Go to the next zone", "Jumps to the next of the 16 zones.", () => { m.Close(); game.GoTo((game.Current + 1) % game.zones.Count); });
            m.AddItem("Backpack", "What you are carrying, and what each thing does when you use it." + (vr ? "" : " The I key opens it."), () => m.OpenExtra(pageBag));
            m.AddItem("Upgrades", "Spend ice, ore, helium-3 and crystals on better tools.", () => m.OpenExtra(pageUp));
            m.AddItem("Power, lights and air", "Light switch, the cable network, and the ventilation fans.", () => m.OpenExtra(pageGrid));
            m.AddItem("Aliens: " + (aliensOn ? "ON" : "OFF"), "OFF sends every alien and the UFO away for a calm visit.", () => { aliensOn = !aliensOn; m.Rebuild(); });
            if (vr) m.AddItem("Dashboard on wrist / floating", "Moves the dashboard card onto your left wrist, or back to floating in front of you.", () => { m.Close(); game.ToggleWrist(); });
        };
        pageBag.build = m =>
        {
            string[] what = { "Melt into water: water +12%", "Extract oxygen from the rock: oxygen +10%", "Fuel for the reactor: power +15%", "Spend on upgrades", "Place with the Block builder", "Plug in: power +25%" };
            string[] why = {
                "Water ice hides in craters near the Moon's south pole that never see the Sun. Melted and filtered it becomes drinking water.",
                "Ilmenite is a real Moon mineral made of iron, titanium and oxygen. Heating it releases the oxygen. NASA calls using local materials like this ISRU.",
                "Helium-3 has been collecting in Moon dust for billions of years from the solar wind. In the game it tops up the power.",
                "Aliens leave these behind when they are beamed home.", "Blocks of pressed Moon dust (regolith). Dig the ground outside with the Mining drill to get more.", "A charged battery pack from a supply crate." };
            for (int i = 0; i < ItemNames.Length; i++)
            {
                int k = i;
                m.AddItem(ItemNames[i] + "  x " + bag[i] + " / " + Capacity(i) + "     " + what[i], why[i], () => { UseItem(k); if (k == Block) m.Close(); else m.Rebuild(); });
            }
        };
        pageUp.build = m =>
        {
            for (int i = 0; i < UpNames.Length; i++)
            {
                int k = i; string state = Owned(k) ? "owned" : (k == 5 && rigCount > 0 ? rigCount + " built, " + CostText(k) : (k == 7 && turrets.Count > 0 ? turrets.Count + " built, " + CostText(k) : (k == 8 ? dysonCount + " of " + DysonMax + " in orbit, " + CostText(k) : CostText(k))));
                m.AddItem(UpNames[i] + "   -   " + state + (!Owned(k) && CanPay(k) ? "   (buy)" : ""), UpHints[i] + "  You have: " + bag[Ice] + " ice, " + bag[Ore] + " ore, " + bag[He3] + " helium-3, " + bag[Crystal] + " crystals.", () => { Buy(k); m.Rebuild(); });
            }
        };
        pageGrid.build = m =>
        {
            m.AddItem("Habitat lights: " + (lightsOn ? "ON" : "OFF"), "Switches every lamp in the habitat. With the lights off the batteries charge a little faster." + (game.HeadsetActive ? "" : " The O key does the same."), () => { SetLights(!lightsOn); m.Rebuild(); });
            m.AddItem("Power cables: " + (wiresOn ? "shown" : "hidden"), "The cable network that carries electricity from the power plant to every zone. The moving lights are the current. If the batteries run flat the cables turn red.", () => { wiresOn = !wiresOn; m.Rebuild(); });
            m.AddItem("Ventilation fans: " + (ventOn ? "ON" : "OFF"), "A fan unit near the ceiling of every zone keeps the air moving and clean. With the fans off, oxygen slowly falls during a mission.", () => { SetVent(!ventOn); m.Rebuild(); });
        };
        if (fieldOk) menu.extraPages.Add(new HunchMenu.ExtraPage { label = "Go to the aliens (mining field)", hint = "Takes you straight outside to the mining field: aliens to zap or befriend, rovers to ride, rocks to mine and throw. " + (game.HeadsetActive ? "" : "The J key does the same."), direct = () => { menu.Close(); GoField(); } });
        menu.extraPages.Add(pageRoot);
    }

    // ------------------------------------------------------------------ frame
    void Update()
    {
        if (!ready || !origin || !origin.Camera) return;
        float dt = Time.deltaTime; cam = origin.Camera.transform;
        UpdateGrid(dt);
        if (fieldOk) { UpdateAliens(dt); UpdateUfo(dt); UpdateRovers(dt); UpdateNodes(dt); UpdateRigs(dt); UpdateRocks(dt); UpdateTurrets(dt); }
        UpdatePickups(dt); UpdateFocus(); UpdateEasy(dt); UpdateFinder(); UpdateAch(dt); UpdateQuickBar(); UpdateBatch3(dt); UpdateAuto();
        if (ride != null && !Playing) Dismount(null);
        if (Playing && fieldOk && !announced && !hunchOn) { announceT += dt; if (announceT > 9f) { announced = true; sim.Say("ARIA: Aliens are hopping around the mining field outside the base. " + (game.HeadsetActive ? "Open the menu and choose Go to the aliens." : "Press J to go there.") + " You can zap them home or make friends."); } }
        if (Playing && !game.menuOpen) UpdateInput(dt); else wasHeld = false;
        UpdateShots(dt); UpdateToolModel(); UpdateMove(dt);
        if (!outsideTip && fx && fx.Outside && Playing && fieldOk && (Feet() - fieldC).sqrMagnitude < 60f * 60f) { outsideTip = true; Note("Mining field ahead: look for the green beacon. " + (game.HeadsetActive ? "B" : "R") + " chooses a tool."); }
        // VR: one line on the dashboard card
        var sb = new StringBuilder();
        sb.Append("Tool: ").Append(ToolLabel()).Append(game.flyMode ? "   Jetpack ON" : "").Append(runOn ? "   Run ON" : "").Append("   Ice ").Append(bag[Ice]).Append("  Ore ").Append(bag[Ore]).Append("  He-3 ").Append(bag[He3]).Append("  Crystals ").Append(bag[Crystal]).Append("  Blocks ").Append(bag[Block]);
        if (finder.Length > 0) sb.Append("\n").Append(finder);
        if (focusText.Length > 0) sb.Append("\n<color=#FFE08A>Right trigger: ").Append(focusText).Append("</color>");
        else if (heldRock != null) sb.Append("\n<color=#FFE08A>Right trigger: throw the rock</color>");
        else if (smartTool != 0) sb.Append("\n<color=#FFE08A>").Append(SmartPrompt(true)).Append("</color>");
        if (Time.unscaledTime < noteUntil) sb.Append("\n").Append(note);
        game.toolLine = sb.ToString();
        { string ex = HudExtra(); if (ex.Length > 0) game.toolLine = ex + "\n" + game.toolLine; }
        if (autoOn) game.toolLine = "<color=#7CFC9A><b>AUTOPILOT</b>  " + autoLine + "\n<size=80%>Press A to take over</size></color>\n" + game.toolLine;
    }

    // where the aliens are, in words
    string finder = ""; bool announced; float announceT;
    void UpdateFinder()
    {
        finder = "";
        if (!fieldOk || !aliensOn || !Playing || mapNow != 0) return;
        Vector3 f = Feet(); Alien best = null; float bd = 1e9f;
        foreach (var a in aliens) if (a.alive && !a.friend) { float d = (a.t.position - f).sqrMagnitude; if (d < bd) { bd = d; best = a; } }
        float toField = Vector3.Distance(f, fieldC);
        if (toField > 90f || best == null) { finder = "Aliens: mining field, " + Mathf.RoundToInt(toField) + " m away" + (game.HeadsetActive ? "  (menu: Go to the aliens)" : "  -  press J to go there"); return; }
        Vector3 to = best.t.position - f; to.y = 0f; Vector3 fw = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        float ang = Vector3.SignedAngle(fw, to, Vector3.up); string where = Mathf.Abs(ang) < 25f ? "ahead" : (Mathf.Abs(ang) > 150f ? "behind you" : (ang > 0f ? "to your right" : "to your left"));
        finder = "Nearest alien: " + Mathf.RoundToInt(Mathf.Sqrt(bd)) + " m " + where + (friends > 0 ? "   Friends: " + friends : "");
    }

    // ------------------------------------------------------------------ desktop: tool bar, crosshair, notes
    GUIStyle sBar, sNote; Texture2D white;

    void OnGUI()
    {
        if (ready && !game.HeadsetActive && !game.menuOpen) ShowGui();      // The Show draws its own picture, also on the closing card after the mission
        if (!ready || game.HeadsetActive || game.menuOpen || game.uiHasMouse || !Playing) return;
        if (game.hideHud && !hudHid) return;      // hidden by the player; when a sheet or the cameras hid it, they still draw their own picture
        float u = Screen.height / 1080f, W = Screen.width, H = Screen.height;
        if (sBar == null || Mathf.Abs(sBar.fontSize - HunchLook.FsF(17f * u)) > 1f)
        {
            white = Texture2D.whiteTexture;
            sBar = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(17f * u), richText = true, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            sNote = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(21f * u), richText = true, alignment = TextAnchor.MiddleCenter };
            sBar.normal.textColor = Color.white; sNote.normal.textColor = Color.white;
        }
        if (DrawBatch7(u, W, H)) return;      // the security camera picture fills the screen
        bool simple = game.simpleHud; if (simple) DrawSimpleHud(u, W, H);
        float bw = Mathf.Min(900f * u, W - 28f * u), bh = 34f * u; var r = new Rect((W - bw) * 0.5f, simple ? H - 48f * u : H - 190f * u - 66f * u, bw, bh);
        GUI.color = new Color(0.02f, 0.04f, 0.09f, 0.78f); GUI.DrawTexture(r, white); GUI.color = Color.white;
        if (hunchOn) GUI.Label(r, "<color=#7CFC9A><b>NASA HUNCH mode</b></color>   <color=#C8D4E8>[Esc] menu: checklist and documents   [F2] crew health   [F7] radio   [O] lights</color>", sBar); else
        GUI.Label(r, "<color=#FFE08A><b>[R] " + ToolLabel() + "</b></color>   <color=#9AD8FF>Ice " + bag[Ice] + "   Ore " + bag[Ore] + "   He-3 " + bag[He3] + "   Crystals " + bag[Crystal] + "   Blocks " + bag[Block] + "</color>   <color=#C8D4E8>" + (simple ? "[I] backpack   [X] eat   [J] aliens   [Esc] menu" : "[I] backpack   [O] lights   [J] aliens") + (game.flyMode ? "   FLY" : "") + "</color>", sBar);
        if (Time.unscaledTime < noteUntil)
        {
            var nr = new Rect(W * 0.5f - 480f * u, H * 0.5f + 70f * u, 960f * u, 40f * u);
            GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(nr, white); GUI.color = Color.white;
            GUI.Label(nr, "<color=#FFE08A>" + note + "</color>", sNote);
        }
        if (finder.Length > 0)
        {
            var fr = new Rect(r.x, r.y - bh - 4f * u, bw, bh);
            GUI.color = new Color(0.02f, 0.07f, 0.05f, 0.7f); GUI.DrawTexture(fr, white); GUI.color = Color.white;
            GUI.Label(fr, "<color=#7CFC9A>" + finder + "</color>", sBar);
        }
        string prompt = focusText.Length > 0 ? "Click: " + focusText : (heldRock != null ? "Click: throw the rock" : SmartPrompt(false));
        if (prompt.Length > 0)
        {
            var pr = new Rect(W * 0.5f - 300f * u, H * 0.5f + 24f * u, 600f * u, 36f * u);
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(pr, white); GUI.color = Color.white;
            GUI.Label(pr, "<color=#FFE08A><b>" + prompt + "</b></color>", sNote);
        }
        DrawBanner(u, W, H);
        if (autoOn) DrawAuto(u, W, H);
        if (tool != Hands || focus != null || smart)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.85f); float c = 2f * Mathf.Max(1f, u), l = 9f * u;
            GUI.DrawTexture(new Rect(W * 0.5f - l, H * 0.5f - c * 0.5f, l * 2f, c), white); GUI.DrawTexture(new Rect(W * 0.5f - c * 0.5f, H * 0.5f - l, c, l * 2f), white); GUI.color = Color.white;
        }
    }

    // ------------------------------------------------------------------ test helper hooks (used by the editor probe)
    public string ProbeInfo()
    {
        int alive = 0; foreach (var a in aliens) if (a.alive) alive++;
        int up = 0; foreach (var n in nodes) if (n.up) up++;
        return "field=" + (fieldOk ? fieldC.ToString("F0") : "none") + " nodes=" + up + "/" + nodes.Count + " aliens=" + alive + "/" + aliens.Count + " sentHome=" + sentHome + " mined=" + minedCount + " blocks=" + blocks.Count + " pickups=" + pickups.Count
            + " wires=" + wires.Count + " vents=" + vents.Count + " rovers=" + rovers.Count + " rigs=" + rigs.Count + " tool=" + ToolNames[tool] + " bag=" + string.Join(",", bag) + " mover=" + (mover != null) + " lights=" + lightsOn + " fly=" + game.flyMode + " note=" + note;
    }
    public void ProbeField() { GoField(); }
    public string ProbeFocus() { return "focus=" + (focus ? focus.kind.ToString() : "none") + " text=" + focusText + " finder=" + finder + " friends=" + friends + " ride=" + (ride != null) + " rocks=" + rocks.Count + " held=" + (heldRock != null) + " turrets=" + turrets.Count + " guns=" + gunModels.Count + " tele=" + HunchFx.Has("pack teleporter") + " consoles=" + consoles.Count; }
    public void ProbeHands() { tool = Hands; }
    public string ProbeRay() { RaycastHit h; Ray r = AimRay(); if (!Cast(r, out h, 12f)) return "ray hit nothing from " + r.origin.ToString("F1") + " dir " + r.direction.ToString("F2"); var tg = h.collider.GetComponentInParent<HunchTarget>(); return "ray hit " + h.collider.name + " at " + h.distance.ToString("F1") + " m kind " + (tg ? tg.kind.ToString() : "-") + " rock0 at " + (rocks.Count > 0 ? Vector3.Distance(r.origin, rocks[0].t.position).ToString("F1") : "?"); }
    public string ProbeFxShow()
    {
        // lines every new pack effect up in front of the camera so they can be checked in one picture
        string[] names = { "pack teleporter", "explosion", "magic fire pro orange", "magic fire pro blue", "confetti_blast_multicolor", "flash_blue_purple", "sparkle_ellow", "water_blast_blue", "area_circles_blue", "flash_star_ellow_purple" };
        Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized, r = Vector3.Cross(Vector3.up, f); var sb = new StringBuilder(); Vector3 basePos = Feet() + f * 7f;
        for (int i = 0; i < names.Length; i++) { bool has = HunchFx.Has(names[i]); sb.Append(names[i]).Append(has ? "=yes " : "=NO "); if (has) HunchFx.Spawn(names[i], basePos + r * ((i - 4.5f) * 2.2f) + Vector3.up * 0.6f, 1f, 3f); }
        return sb.ToString();
    }
    public string ProbeTurret() { BuildTurret(); if (turrets.Count == 0) return "no turret"; var tu = turrets[turrets.Count - 1]; Vector3 p = tu.t.position + new Vector3(3.2f, 0f, 0f), g; if (GroundAt(p.x, p.z, out g)) p = g; TeleportTo(p, -90f); cam.position = origin.transform.position + Vector3.up * 1.4f; Face(tu.t.position + Vector3.up * 0.9f); return "turret model=" + tu.modelHead; }
    public string ProbeNearRover() { if (rovers.Count == 0) return "no rover"; var r = rovers[0]; Vector3 p = r.t.position - r.t.right * 2.6f, g; if (GroundAt(p.x, p.z, out g)) p = g; TeleportTo(p, 0f); cam.position = origin.transform.position + Vector3.up * 1.4f; Face(r.t.position + Vector3.up * 0.7f); return "rover"; }
    public void ProbeFaceRover() { if (rovers.Count > 0) Face(rovers[0].t.position + Vector3.up * 0.7f); }
    public string ProbeNearRock() { if (rocks.Count == 0) return "no rock"; var r = rocks[0]; Vector3 p = r.t.position + new Vector3(1.8f, 0f, 0f), g; if (GroundAt(p.x, p.z, out g)) p = g; TeleportTo(p, 0f); cam.position = origin.transform.position + Vector3.up * 1.4f; Face(r.t.position); return "rock"; }
    public string ProbeNearConsole() { var z = game.zones[game.Current]; Transform b = consoles[game.Current].lightBtn.transform; Face(b.position); return "console " + z.id + " dist " + Vector3.Distance(cam.position, b.position).ToString("F1"); }
    public void ProbeFaceFan() { Face(consoles[game.Current].fanBtn.transform.position); }
    public void ProbeLookDown() { if (desk) desk.SetLook(cam.eulerAngles.y, 60f); }
    public void ProbeWalk() { if (nav) nav.debugIntent = cam.forward; }
    public void ProbeStopWalk() { if (nav) nav.debugIntent = Vector3.zero; }
    public void ProbeNextTool() { NextTool(); }
    public void ProbeHold() { probeHold = Time.time + 3.2f; }
    public void ProbeGive() { for (int i = 0; i < 4; i++) bag[i] = Capacity(i); }
    public void ProbeBuyAll() { for (int k = 0; k < UpNames.Length; k++) Buy(k); }
    public void ProbeLights() { SetLights(!lightsOn); }
    public void ProbeVent() { SetVent(!ventOn); }
    public void ProbeUseAll() { UseItem(Ice); UseItem(Ore); UseItem(He3); UseItem(Cell); }
    void Face(Vector3 target)
    {
        if (!desk) return;
        Vector3 d = target - cam.position; if (d.sqrMagnitude < 0.01f) return;
        desk.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Asin(Mathf.Clamp(d.normalized.y, -1f, 1f)) * Mathf.Rad2Deg);
    }
    public string ProbeAimAlien()
    {
        Alien best = null; float bd = 1e9f;
        foreach (var a in aliens) if (a.alive) { float d = (a.t.position - cam.position).sqrMagnitude; if (d < bd) { bd = d; best = a; } }
        if (best == null) return "no alien";
        Face(best.t.position + Vector3.up * 0.5f); return "alien at " + Mathf.Sqrt(bd).ToString("F0") + " m";
    }
    public string ProbeNearNode()
    {
        Node best = null; float bd = 1e9f;
        foreach (var n in nodes) if (n.up) { float d = (n.t.position - fieldC).sqrMagnitude; if (d < bd) { bd = d; best = n; } }
        if (best == null) return "no node";
        Vector3 p = best.t.position + new Vector3(2.4f, 0f, 0f); Vector3 g; if (GroundAt(p.x, p.z, out g)) p = g;
        TeleportTo(p, -90f); Physics.SyncTransforms(); cam.position = origin.transform.position + Vector3.up * 1.4f; Face(best.t.position + Vector3.up * 0.5f);
        return ItemNames[best.item] + " node";
    }
    public void ProbeAimDown() { if (desk) desk.SetLook(cam.eulerAngles.y, 24f); }
    public void ProbeAimBlock() { if (blocks.Count > 0) Face(blocks[blocks.Count - 1].position + Vector3.up * 0.45f); }
    public string ProbeFaceSun() { if (!sun) return "no sun"; Vector3 d = -sun.transform.forward; if (desk) desk.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Asin(d.y) * Mathf.Rad2Deg - 6f); return "sun elevation " + (Mathf.Asin(d.y) * Mathf.Rad2Deg).ToString("F1") + " shown=" + (sunHalo && sunHalo.gameObject.activeSelf); }
    public void ProbeFaceUfo() { if (ufo) Face(ufo.position); }
    public void ProbeFaceField() { Face(fieldC + Vector3.up * 1.5f); }
    public void ProbeBack() { Vector3 d = (cam.position - fieldC); d.y = 0f; Vector3 p = fieldC + d.normalized * 16f, g; if (GroundAt(p.x, p.z, out g)) p = g; TeleportTo(p, 0f); }
    public void ProbeAimLevel() { if (desk) desk.SetLook(cam.eulerAngles.y, 8f); }
}
