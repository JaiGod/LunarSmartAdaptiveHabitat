// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/B_Maps_Minigames/HunchFrontier13.cs
//  Section : 06_Frontier/B_Maps_Minigames
//  Folder  : Extra maps and mini-games: solar farm, mining pit, Factory Land, Star Patrol, crew
//            radio + Star Clicker, Science Academy, My Astronaut, model kits, Fun Zone, endless
//            ground, Star Store pages.
//  Purpose : Thirteenth part of Moon Frontier: real 3-D models in place of simple shapes. FACTORY
//            KIT pieces of the "Unity Factory" Asset Store pack (machines, conveyors, robot arms,
//            control cabinets, carts),
//  Contains: HunchFrontier, PortalFx
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

/// Thirteenth part of Moon Frontier: real 3-D models in place of simple shapes.
///   FACTORY KIT   pieces of the "Unity Factory" Asset Store pack (machines, conveyors, robot arms, control cabinets, carts),
///                  copied into Resources/HunchFactoryKit by the Editor tool HunchFactoryKit and placed in Factory Land here.
///   MODEL KIT     models made in Blender for this game (Resources/HunchModels): astronaut, Lumi, hospital bed, pumpkin, gift box.
/// If a kit is missing the game simply keeps its simple shapes.
public partial class HunchFrontier
{
    GameObject kitFactory, kitModels; bool kitFactoryTried, kitModelsTried; public int kitPlaced;

    GameObject KitSource(bool factory, string name)
    {
        if (factory) { if (!kitFactoryTried) { kitFactoryTried = true; kitFactory = Resources.Load<GameObject>("HunchFactoryKit"); } }
        else if (!kitModelsTried) { kitModelsTried = true; kitModels = Resources.Load<GameObject>("HunchModels"); }
        var root = factory ? kitFactory : kitModels; if (!root) return null; var t = root.transform.Find(name); return t ? t.gameObject : null;
    }
    static Bounds BoundsOf(GameObject g)
    {
        var b = new Bounds(g.transform.position, Vector3.zero); bool first = true;
        foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (r is ParticleSystemRenderer) continue; if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        return b;
    }
    /// places a copy of a kit piece standing on the ground at lp. fit > 0 scales it so its longest side is that many metres; solid adds one simple box to stop you walking through it
    GameObject Kit(bool factory, string name, Transform parent, Vector3 lp, float yaw, float fit, bool solid)
    {
        var src = KitSource(factory, name); if (!src) return null;
        var g = Instantiate(src, parent); g.name = name; g.SetActive(true); g.transform.localPosition = lp; g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * src.transform.localRotation;
        foreach (var c in g.GetComponentsInChildren<Collider>(true)) Destroy(c);
        var b = BoundsOf(g);
        if (fit > 0f) { float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)); if (longest > 0.001f) { g.transform.localScale *= fit / longest; b = BoundsOf(g); } }
        Vector3 want = parent.TransformPoint(lp); g.transform.position += new Vector3(want.x - b.center.x, want.y - b.min.y, want.z - b.center.z); b = BoundsOf(g);
        if (solid) { var block = new GameObject("Block"); block.transform.SetParent(parent, false); block.transform.position = b.center; block.transform.rotation = Quaternion.identity; var bc = block.AddComponent<BoxCollider>(); bc.size = b.size; }
        kitPlaced++; return g;
    }

    void FacKit(Transform r)
    {
        if (!KitSource(true, "Arm")) return;
        // a conveyor line down the aisle between the two rows of machines
        for (int i = 0; i < 3; i++) Kit(true, "Line_04", r, new Vector3(-2f, 0f, 19f + i * 7f), 0f, 6.4f, true);
        Kit(true, "Line_05", r, new Vector3(-2f, 0f, 12.2f), 0f, 3.4f, true);
        for (int i = 0; i < 5; i++) Kit(true, "Product", r, new Vector3(-2f, 1.05f, 17.5f + i * 4.2f), 90f, 0.7f, false);
        // robot arms working the line, control cabinets by every machine
        Kit(true, "Arm", r, new Vector3(-9.6f, 0f, 21f), 90f, 4.2f, true); Kit(true, "Arm", r, new Vector3(5.6f, 0f, 21f), -90f, 4.2f, true);
        Kit(true, "Arm", r, new Vector3(-9.6f, 0f, 31f), 90f, 4.2f, true); Kit(true, "Arm", r, new Vector3(5.6f, 0f, 31f), -90f, 4.2f, true);
        for (int i = 0; i < 3; i++) { Kit(true, i == 0 ? "Controller_1" : (i == 1 ? "Controller_2" : "Controller_3"), r, new Vector3(-9.2f, 0f, 14.6f + i * 10f), 90f, 2.4f, true); Kit(true, i == 1 ? "Controller_1" : "Controller_3", r, new Vector3(5.2f, 0f, 14.6f + i * 10f), -90f, 2.4f, true); }
        // a workshop row of real machines between the chests and the production line
        string[] row = { "Line_06", "Line_01", "Line_08", "Line_07", "Line_02", "Line_03", "Line_09" }; float[] size = { 3.2f, 2.2f, 2.6f, 5.2f, 1.9f, 1.9f, 1.8f }; float[] rx = { -22f, -18f, -14.5f, 11.5f, 16.5f, 19.5f, 22.5f };
        for (int i = 0; i < row.Length; i++) Kit(true, row[i], r, new Vector3(rx[i], 0f, i < 3 ? 12f : 1.2f), i < 3 ? 180f : 0f, size[i], true);
        // carts by the chests
        Kit(true, "Cart", r, new Vector3(-9.4f, 0f, 2.6f), 30f, 1.5f, true); Kit(true, "Wagon", r, new Vector3(9.6f, 0f, 2.8f), -20f, 2.2f, true); Kit(true, "Product", r, new Vector3(9.6f, 1.25f, 2.8f), 70f, 0.7f, false);
    }
    // ================================================================== stone portals (the "Stone portal" pack by Ordoss) between the worlds
    class PortalFx { public Transform root, ring; public Transform[] swirl = new Transform[6], orbs; public float[] swirlSize = new float[6]; public int layers = 3; public Light lamp; public AudioSource hum; public Transform spinA, spinB; public Transform[] gems; public Vector3[] gemHome; public float punch, lampBase, radius; public Vector3 centre; }
    readonly List<PortalFx> portals = new List<PortalFx>(); GameObject kitPortal; bool kitPortalTried; static Texture2D spiralTex; public bool portalBusy; float portalFlash; Transform portalVeil; Material portalVeilMat;

    static void BuildSpiral()
    {
        if (spiralTex) return; const int N = 256; var px = new Color32[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f, r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
            float arms = 0.5f + 0.5f * Mathf.Sin(3f * a + 9f * r), fine = 0.5f + 0.5f * Mathf.Sin(7f * a - 16f * r);
            float alpha = (arms * arms * 0.8f + fine * 0.2f) * Mathf.SmoothStep(1f, 0.72f, r) * (0.35f + 0.65f * Mathf.SmoothStep(0f, 0.3f, r)) + Mathf.SmoothStep(0.25f, 0f, r) * 0.9f;
            byte w = (byte)Mathf.Clamp(Mathf.RoundToInt((0.6f + 0.4f * arms) * 255f), 0, 255); px[y * N + x] = new Color32(w, w, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
        }
        spiralTex = new Texture2D(N, N, TextureFormat.RGBA32, true); spiralTex.SetPixels32(px); spiralTex.wrapMode = TextureWrapMode.Clamp; spiralTex.Apply(true);
    }

    /// dresses a gate as a stone portal with a spinning vortex and orbiting sparks. Returns true when the stone model was used.
    bool PortalStone(Transform g, Color c, int index)
    {
        if (!kitPortalTried) { kitPortalTried = true; kitPortal = Resources.Load<GameObject>("HunchPortalKit"); }
        var fx = new PortalFx { root = g }; bool stone = kitPortal && kitPortal.transform.childCount > 0;
        if (stone)
        {
            string name = index < 0 ? "PortalPurple" : (index == 0 ? "PortalGreen" : (index >= 100 ? "PortalYellow" : (index % 2 == 0 ? "PortalBlue" : "PortalRed")));
            var src = kitPortal.transform.Find(name); if (!src) src = kitPortal.transform.GetChild(0);
            const float k = 0.42f; var p = Instantiate(src.gameObject, g); p.name = "Stone portal"; p.SetActive(true); p.transform.localPosition = Vector3.zero; p.transform.localRotation = Quaternion.identity; p.transform.localScale = Vector3.one * k;
            foreach (var ps in p.GetComponentsInChildren<ParticleSystem>(true)) { var m = ps.main; m.scalingMode = ParticleSystemScalingMode.Hierarchy; }
            foreach (var l in p.GetComponentsInChildren<Light>(true)) { l.range = Mathf.Max(6f, l.range * k * 1.8f); l.shadows = LightShadows.None; fx.lamp = l; fx.lampBase = Mathf.Min(l.intensity, 6f); }
            fx.ring = p.transform.Find("Portal"); kitPlaced++;
        }
        BuildSpiral(); fx.centre = new Vector3(0f, stone ? 2.22f : 1.4f, 0f); fx.radius = stone ? 1.7f : 1.0f; float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b), 1f);
        for (int i = 0; i < 3; i++)
        {
            var m = new Material(clearSrc); var tint = i == 1 ? new Color(1f, 1f, 1f, 0.55f) : new Color(c.r / mx, c.g / mx, c.b / mx, 0.85f - i * 0.15f); m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", spiralTex); m.mainTexture = spiralTex; if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            fx.swirlSize[i] = fx.radius * 2f * (1f - i * 0.2f); var q = Prim(PrimitiveType.Quad, g, fx.centre + new Vector3(0f, 0f, 0.03f * (i - 1)), Vector3.one * fx.swirlSize[i], m); q.name = "Vortex"; fx.swirl[i] = q.transform;
        }
        fx.orbs = new Transform[10]; var spark = Glow(new Color(c.r * 1.3f + 0.3f, c.g * 1.3f + 0.3f, c.b * 1.3f + 0.3f));
        for (int j = 0; j < fx.orbs.Length; j++) fx.orbs[j] = Prim(PrimitiveType.Sphere, g, fx.centre, Vector3.one * 0.12f, spark).transform;
        portals.Add(fx); return stone;
    }

    void UpdatePortals(float dt)
    {
        if (portalVeil) { bool on = portalFlash > 0.01f; if (portalVeil.gameObject.activeSelf != on) portalVeil.gameObject.SetActive(on); if (on) portalVeilMat.SetColor("_BaseColor", new Color(0.78f, 0.66f, 1f, Mathf.Clamp01(portalFlash) * 0.92f)); }
        if (portals.Count == 0 || !cam) return; Vector3 f = Feet(); float t = Time.time;
        foreach (var p in portals)
        {
            if (!p.root || !p.root.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(f, p.root.position), near = Mathf.Clamp01(1f - (d - 2.5f) / 8f); p.punch = Mathf.MoveTowards(p.punch, 0f, dt * 1.4f); float sp = 1f + near * 2.4f + p.punch * 7f;
            if (p.ring) p.ring.Rotate(0f, 0f, dt * 8f * sp, Space.Self);
            if (p.spinA) p.spinA.RotateAround(p.spinA.position, p.root.forward, dt * 36f * sp); if (p.spinB) p.spinB.RotateAround(p.spinB.position, p.root.forward, -dt * 70f * sp);
            if (p.gems != null) for (int i = 0; i < p.gems.Length; i++) { p.gems[i].position = p.root.TransformPoint(p.gemHome[i]) + Vector3.up * (Mathf.Sin(t * 1.4f + i * 1.6f) * 0.16f); p.gems[i].Rotate(Vector3.up, dt * 50f * (i % 2 == 0 ? 1f : -1f), Space.World); }
            for (int i = 0; i < p.layers; i++) { var q = p.swirl[i]; q.Rotate(0f, 0f, dt * (i % 2 == 0 ? 60f : -95f) * sp * (1f + i * 0.5f), Space.Self); q.localScale = Vector3.one * p.swirlSize[i] * (1f + Mathf.Sin(t * (1.7f + i)) * 0.05f + near * 0.06f + p.punch * 0.35f); }
            for (int j = 0; j < p.orbs.Length; j++)
            {
                float a = t * (0.7f + near * 1.6f + p.punch * 5f) * (j % 2 == 0 ? 1f : -0.7f) + j * 0.628f, r = p.radius * (1.02f + 0.1f * Mathf.Sin(t * 2.1f + j) - p.punch * 0.5f);
                p.orbs[j].localPosition = p.centre + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, Mathf.Sin(t * 3f + j * 1.3f) * 0.3f); p.orbs[j].localScale = Vector3.one * (0.1f + 0.05f * Mathf.Sin(t * 6f + j) + near * 0.04f);
            }
            if (p.lamp) p.lamp.intensity = p.lampBase * (0.75f + 0.25f * Mathf.Sin(t * 5.3f) + near * 0.8f + p.punch * 3f);
            if (p.hum) p.hum.volume = (!fx || fx.sfxOn ? 0.16f : 0f) * (0.5f + near * 0.9f + p.punch);
            p.root.localScale = Vector3.one * (1f + p.punch * 0.1f);
        }
    }

    /// travel through a gate with the warp: the portal spins up, the view washes violet, you arrive in a burst
    public void PortalGo(int index, Transform gate)
    {
        if (index < 0) { if (menu && pageMaps != null) menu.OpenExtra(pageMaps); return; }
        if (index >= 100) { MgPrepare(index - 100); return; }
        if (portalBusy || !Playing) return; StartCoroutine(PortalRun(index, gate));
    }
    System.Collections.IEnumerator PortalRun(int index, Transform gate)
    {
        portalBusy = true; BuildExtraSounds();
        if (!portalVeil && cam)
        {
            portalVeilMat = new Material(clearSrc); if (portalVeilMat.HasProperty("_Cull")) portalVeilMat.SetFloat("_Cull", 0f); portalVeilMat.renderQueue = 3900;
            var v = Prim(PrimitiveType.Quad, cam, new Vector3(0f, 0f, 0.45f), Vector3.one * 3f, portalVeilMat); v.name = "Portal veil"; portalVeil = v.transform; v.SetActive(false);
        }
        foreach (var p in portals) if (gate && p.root == gate) p.punch = 1f;
        Vector3 at = gate ? gate.position + Vector3.up * 2.2f : cam.position + cam.forward * 2.5f;
        Play(cWarp, 0.8f); game.Buzz(0.8f, 0.5f); HunchFx.Spawn("teleport", at, 1.6f, 1.0f); HunchFx.Spawn("star hit", at, 1.4f, 0.8f);
        float t = 0f; while (t < 0.65f) { t += Time.deltaTime; portalFlash = t / 0.65f; yield return null; }
        if (index == 13 && mapNow != 13) BossEnter(); else GoMap(index);
        yield return null;
        Vector3 here = Feet() + Vector3.up * 1.2f + cam.forward * 1.5f; HunchFx.Spawn("teleport", here, 1.4f, 1.0f); HunchFx.Spawn("star hit", here, 1.2f, 0.8f); Play(cPop, 0.6f);
        foreach (var p in portals) if (p.root && p.root.gameObject.activeInHierarchy && (p.root.position - Feet()).sqrMagnitude < 30f * 30f) p.punch = 0.7f;
        while (t > 0f) { t -= Time.deltaTime * 0.9f; portalFlash = Mathf.Clamp01(t / 0.65f); yield return null; }
        portalFlash = 0f; portalBusy = false;
    }
    public string ProbePortal() { int act = 0; foreach (var p in portals) if (p.root && p.root.gameObject.activeInHierarchy) act++; return "portals " + portals.Count + " active " + act + " stone kit " + (kitPortal ? kitPortal.transform.childCount + " prefabs" : "missing") + " busy " + portalBusy; }
    public string ProbePortalGo() { Transform gate = null; float best = 1e9f; foreach (var p in portals) if (p.root && p.root.gameObject.activeInHierarchy) { float d = (p.root.position - Feet()).sqrMagnitude; if (d < best) { best = d; gate = p.root; } } PortalGo(probeInt, gate); return "going to " + probeInt + " via " + (gate ? gate.name : "no gate"); }
    // ================================================================== the portal plaza: one legendary portal for every world, at the mining field
    Transform plazaRoot; static AudioClip portalHum;
    static AudioClip PortalHum()
    {
        if (portalHum) return portalHum; int rate = 22050, n = rate * 4; var d = new float[n];
        for (int i = 0; i < n; i++) { float t = i / (float)rate; d[i] = (Mathf.Sin(6.2832f * 110f * t) * 0.5f + Mathf.Sin(6.2832f * 110.75f * t + 1f) * 0.35f + Mathf.Sin(6.2832f * 220.25f * t) * 0.15f) * (0.75f + 0.25f * Mathf.Sin(6.2832f * 0.5f * t)) * 0.4f; }
        portalHum = AudioClip.Create("portal hum", n, 1, rate, false); portalHum.SetData(d, 0); return portalHum;
    }
    string plazaSite = "";
    /// true when the whole disc is open flat ground: no tunnel, wall, building or prop anywhere in it
    bool SiteFlat(Vector3 c, float rad, float tol)
    {
        Vector3 g, q; if (!GroundAt(c.x, c.z, out g)) return false;
        for (float dx = -rad; dx <= rad; dx += 2.5f) for (float dz = -rad; dz <= rad; dz += 2.5f)
        { if (dx * dx + dz * dz > rad * rad) continue; if (!GroundAt(c.x + dx, c.z + dz, out q) || Mathf.Abs(q.y - g.y) > tol) return false; }
        return !Physics.CheckBox(g + Vector3.up * (tol + 4.2f), new Vector3(rad * 0.7f, 3.6f, rad * 0.7f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
    }
    /// the nearest place to the mining field where the whole plaza fits without touching anything
    bool PlazaSite(float rad, out Vector3 at)
    {
        at = Vector3.zero;
        for (float dist = rad + 14f; dist <= 150f; dist += 6f) for (int k = 0; k < 24; k++)
        {
            float a = (150f + k * 15f) * Mathf.Deg2Rad; Vector3 c = new Vector3(fieldC.x + Mathf.Sin(a) * dist, 0f, fieldC.z + Mathf.Cos(a) * dist);
            bool near = false; foreach (var u in usedSpots) if ((new Vector3(u.x, 0f, u.z) - c).sqrMagnitude < (rad + 9f) * (rad + 9f)) { near = true; break; }
            if (near || !SiteFlat(c, rad, 1.4f)) continue;
            Vector3 g; GroundAt(c.x, c.z, out g); at = g; usedSpots.Add(g); return true;
        }
        return false;
    }
    void BuildPortalPlaza()
    {
        if (!fieldOk) return; Vector3 at; int n = MapCount - 1; const float R = 18f;
        plazaSite = "strict";
        if (!PlazaSite(R + 5f, out at)) { plazaSite = "loose"; if (!FreeSpot(new[] { 48f, 58f, 40f, 68f, 78f }, 150f, R + 4f, out at) && !FreeSpot(new[] { 48f, 58f, 40f, 68f, 78f }, 150f, 8f, out at)) { plazaSite = "default"; at = fieldC + new Vector3(0f, 0f, 52f); } }
        at.y = GroundAt(at.x, at.z, at.y);
        plazaRoot = new GameObject("Portal plaza").transform; plazaRoot.SetParent(root, false); plazaRoot.position = at;
        Prim(PrimitiveType.Cylinder, plazaRoot, new Vector3(0f, 0.05f, 0f), new Vector3(5f, 0.05f, 5f), Glow(new Color(0.9f, 0.7f, 2.2f)));
        var sign = Label(plazaRoot, "<b>PORTAL PLAZA</b>\n<size=55%>a portal to every world</size>", 0f, 0f, 9f, 60f, 6f, new Color(1f, 0.84f, 0.3f)); sign.transform.localPosition = new Vector3(0f, 9.5f, 0f); mapLabels.Add(sign);
        for (int k = 1; k <= n; k++)
        {
            Vector3 dir = Quaternion.Euler(0f, (k - 1) * 360f / n, 0f) * Vector3.forward, p = at + dir * R; p.y = GroundAt(p.x, p.z, at.y);
            var g = new GameObject("Portal to " + MapNames[k]).transform; g.SetParent(plazaRoot, false); g.position = p; g.rotation = Quaternion.LookRotation(dir, Vector3.up);      // the front faces the middle of the plaza
            PortalLegend(g, (k - 1) / (float)n, k, MapNames[k]);
        }
    }
    /// a portal built from the Blender model LegendPortal (its rings and crystals are animated in Blender), with a five-layer vortex, a light and a hum you hear from its own place
    void PortalLegend(Transform g, float hue, int index, string title)
    {
        Color c = Color.HSVToRGB(hue, 0.9f, 1f), c2 = Color.HSVToRGB((hue + 0.45f) % 1f, 0.75f, 1f); var src = KitSource(false, "LegendPortal");
        if (!src) { bool stone = PortalStone(g, c * 2f, index); var hit0 = Prim(PrimitiveType.Cube, g, new Vector3(0f, 2.15f, 0f), new Vector3(3f, 3.5f, 0.3f), Clear(new Color(1f, 1f, 1f, 0.03f)), true); var t0 = hit0.AddComponent<HunchTarget>(); t0.kind = 46; t0.index = index; mapLabels.Add(Label(g, "<b>" + title + "</b>", 0f, stone ? 4.8f : 3.7f, 5f, 9f, 1.6f, Color.white)); return; }
        const float k = 0.8f; var fxp = new PortalFx { root = g, layers = 5 };
        var m = Instantiate(src, g); m.name = "Legend portal"; m.SetActive(true); m.transform.localPosition = Vector3.zero; m.transform.localRotation = src.transform.localRotation; m.transform.localScale = src.transform.localScale * k;
        foreach (var col in m.GetComponentsInChildren<Collider>(true)) Destroy(col);
        foreach (var r in m.GetComponentsInChildren<Renderer>())
        {
            var ms = r.materials;
            for (int i = 0; i < ms.Length; i++) { Color? tint = ms[i].name.StartsWith("PortalGlowGem") ? c : (ms[i].name.StartsWith("PortalGlowRune") ? c2 : (Color?)null); if (tint == null) continue; ms[i].SetColor("_BaseColor", tint.Value); ms[i].SetColor("_EmissionColor", tint.Value * 2.8f); ms[i].EnableKeyword("_EMISSION"); }
            r.materials = ms;
        }
        var an = m.GetComponentInChildren<Animation>();
        if (an && an.GetClipCount() > 0) { an.wrapMode = WrapMode.Loop; an.playAutomatically = true; foreach (AnimationState st in an) { st.wrapMode = WrapMode.Loop; st.speed = 0.7f + (index % 5) * 0.15f; } an.Play(); }
        else
        {      // the Blender animation did not come through the import: turn the same parts here instead
            var gl = new List<Transform>(); foreach (var tr in m.GetComponentsInChildren<Transform>()) { if (tr.name == "SpinA") fxp.spinA = tr; else if (tr.name == "SpinB") fxp.spinB = tr; else if (tr.name.StartsWith("Crystal")) gl.Add(tr); }
            fxp.gems = gl.ToArray(); fxp.gemHome = new Vector3[gl.Count]; for (int i = 0; i < gl.Count; i++) fxp.gemHome[i] = g.InverseTransformPoint(gl[i].position);
        }
        BuildSpiral(); fxp.centre = new Vector3(0f, 2.55f * k, 0f); fxp.radius = 1.42f * k;
        for (int i = 0; i < 5; i++)
        {
            var mat = new Material(clearSrc); Color lc = Color.HSVToRGB((hue + i * 0.07f) % 1f, i == 2 ? 0.25f : 0.85f, 1f); lc.a = 0.9f - i * 0.12f; mat.SetColor("_BaseColor", lc);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", spiralTex); mat.mainTexture = spiralTex; if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            fxp.swirlSize[i] = fxp.radius * 2.5f * (1f - i * 0.15f); var q = Prim(PrimitiveType.Quad, g, fxp.centre + new Vector3(0f, 0f, (i - 2) * 0.07f), Vector3.one * fxp.swirlSize[i], mat); q.name = "Vortex"; fxp.swirl[i] = q.transform;
        }
        fxp.orbs = new Transform[12]; var spark = Glow(c * 2.2f + new Color(0.3f, 0.3f, 0.3f)); var spark2 = Glow(c2 * 2.2f + new Color(0.3f, 0.3f, 0.3f));
        for (int j = 0; j < fxp.orbs.Length; j++) fxp.orbs[j] = Prim(PrimitiveType.Sphere, g, fxp.centre, Vector3.one * 0.12f, j % 2 == 0 ? spark : spark2).transform;
        var lg = new GameObject("Portal light"); lg.transform.SetParent(g, false); lg.transform.localPosition = fxp.centre + new Vector3(0f, 0f, -0.8f); var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.range = 9f; l.intensity = 2.5f; l.shadows = LightShadows.None; fxp.lamp = l; fxp.lampBase = 2.5f;
        var hum = g.gameObject.AddComponent<AudioSource>(); hum.clip = PortalHum(); hum.loop = true; hum.spatialBlend = 1f; hum.rolloffMode = AudioRolloffMode.Linear; hum.minDistance = 1.5f; hum.maxDistance = 16f; hum.dopplerLevel = 0f; hum.volume = 0.1f; hum.pitch = 0.7f + (index % 7) * 0.1f; hum.playOnAwake = true; hum.Play(); fxp.hum = hum;
        Prim(PrimitiveType.Cylinder, g, new Vector3(0f, 0.03f, 0f), new Vector3(4.6f, 0.03f, 2.6f), Glow(c * 1.4f));
        var hit = Prim(PrimitiveType.Cube, g, fxp.centre, new Vector3(2.6f, 2.8f, 0.3f), Clear(new Color(1f, 1f, 1f, 0.02f)), true); var tg = hit.AddComponent<HunchTarget>(); tg.kind = 46; tg.index = index;
        mapLabels.Add(Label(g, "<b>" + title + "</b>", 0f, 5.1f, 5f, 12f, 1.6f, Color.white));
        portals.Add(fxp); kitPlaced++;
    }
    public string ProbePlaza() { if (!plazaRoot) return "no plaza"; game.flyMode = probeY > 1f; TeleportTo(plazaRoot.position + new Vector3(probeX, probeY, probeZ), probeYaw); if (desk) desk.SetLook(probeYaw, probeInt); int anims = 0; foreach (var a in plazaRoot.GetComponentsInChildren<Animation>()) if (a.isPlaying) anims++; int coded = 0; foreach (var pf in portals) if (pf.spinA) coded++; return "plaza at " + plazaRoot.position.ToString("F0") + " site " + plazaSite + " portals " + (plazaRoot.childCount - 2) + " animations playing " + anims + " coded " + coded; }
    public string ProbeKit() { return "factory kit " + (kitFactory ? kitFactory.transform.childCount + " pieces" : "missing") + ", models kit " + (kitModels ? kitModels.transform.childCount + " pieces" : "missing") + ", placed " + kitPlaced; }
}
