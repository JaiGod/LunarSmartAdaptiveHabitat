// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/C_Base_Crew_Systems/HunchFrontier23.cs
//  Section : 06_Frontier/C_Base_Crew_Systems
//  Folder  : Systems inside the central base: pick-ups + battery bank, NASA HUNCH mode, physics
//            view, Autopilot Smooth, crew of four + sleep pods, The Show, self-working crew,
//            airlocks/lifts, crew self-care.
//  Purpose : Twenty-third part of Moon Frontier: - the crew look after their own health: a calm
//            break in the Sanctuary when stressed, a check-up in the Medical Bay,
//  Contains: HunchFrontier
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

/// Twenty-third part of Moon Frontier:
///   - the crew look after their own health: a calm break in the Sanctuary when stressed, a check-up in the Medical Bay,
///     and they call the medic or food rover to themselves when they need it.
///   - a smoother game on every device: if the frame rate drops, shadows, detail and the far ground step down a little
///     (and in the headset the picture size), and step back up when there is room again.
///   - a sky that sparkles: every star has a soft glow, some flash with a little four-point sparkle, and once in a while a
///     far-away star flares up as a supernova: a small, slow brightening and fading, nothing big.
public partial class HunchFrontier
{
    // ================================================================== the crew look after themselves
    float careNext; readonly float[] crewRoverReady = new float[4]; int crewCareDone;
    void UpdateCrewCare()
    {
        if (Time.time < careNext || game.crewAvatars == null) return; careNext = Time.time + 2f;
        int med = game.ZoneIndex("medical"), calm = game.ZoneIndex("sanctuary"); if (calm < 0) calm = game.ZoneIndex("exercise");
        for (int k = 0; k < 4 && k < sim.crew.Count; k++)
        {
            if (k == playAs || k >= game.crewAvatars.Length || !game.crewAvatars[k] || CrewAsleep(k)) continue;
            var c = sim.crew[k]; var a = crewAi[k]; if (a.job >= 0) continue;
            if (c.health < 70f && Time.time >= crewRoverReady[k]) { crewRoverReady[k] = Time.time + 90f; CallHelper(1, k); sim.Say("ARIA: " + CrewShort(k) + " called the medic rover to look after their health."); continue; }
            if (sim.food < 35f && Time.time >= crewRoverReady[k]) { crewRoverReady[k] = Time.time + 90f; CallHelper(0, k); sim.Say("ARIA: Food is getting low, so " + CrewShort(k) + " called the food rover."); continue; }
            if ((c.stress > 60f || c.mood < 50f) && calm >= 0) { a.job = calm; a.care = 2; a.work = 0f; }
            else if (c.health < 82f && med >= 0) { a.job = med; a.care = 1; a.work = 0f; }
        }
    }
    void CrewCared(int k, int care)
    {
        var c = sim.crew[k];
        if (care == 1) c.health = Mathf.Min(100f, c.health + 12f); else { c.stress = Mathf.Max(0f, c.stress - 15f); c.mood = Mathf.Min(100f, c.mood + 10f); }
        c.Clamp(); crewCareDone++;
        sim.Say("ARIA: " + CrewShort(k) + (care == 1 ? " had a check-up in the Medical Bay." : " took a calm break and feels better."));
    }
    void CrewHelped(int k, int kind)
    {
        if (k < 0 || k >= sim.crew.Count) return; var c = sim.crew[k];
        if (kind == 0) { sim.food = Mathf.Min(100f, sim.food + 10f); c.mood = Mathf.Min(100f, c.mood + 6f); }
        else if (kind == 1) { c.health = Mathf.Min(100f, c.health + 25f); c.stress = Mathf.Max(0f, c.stress - 8f); }
        c.Clamp(); crewCareDone++; Note(CrewShort(k) + ": thanks, rover!");
    }
    public string ProbeCare() { var sb = new System.Text.StringBuilder("care done " + crewCareDone + " | "); for (int k = 0; k < 4 && k < sim.crew.Count; k++) sb.Append(sim.crew[k].name).Append(" health ").Append(sim.crew[k].health.ToString("F0")).Append(" stress ").Append(sim.crew[k].stress.ToString("F0")).Append(" mood ").Append(sim.crew[k].mood.ToString("F0")).Append(" care ").Append(crewAi[k].care).Append(" ").Append(crewAi[k].doing).Append(" | "); return sb.ToString(); }
    public string ProbeCareTest() { for (int k = 0; k < 4 && k < sim.crew.Count; k++) if (k != playAs) { sim.crew[k].health = 60f; sim.crew[k].stress = 70f; crewRoverReady[k] = 0f; } careNext = 0f; return ProbeCare(); }

    // ================================================================== smooth on every device
    int perfLevel = -1, perfGood; float perfT, perfFrames, perfFps, baseShadow = -1f, baseLod = -1f;
    void UpdatePerf()
    {
        if (perfLevel < 0) { baseShadow = QualitySettings.shadowDistance; baseLod = QualitySettings.lodBias; perfLevel = Application.isMobilePlatform ? 1 : 0; PerfApply(); }
        perfT += Time.unscaledDeltaTime; perfFrames++;
        if (perfT < 4f) return;
        perfFps = perfFrames / perfT; perfT = 0f; perfFrames = 0f;
        if (Time.timeScale > 1.01f || game.menuOpen || !Playing) return;
        int floor = Application.isMobilePlatform ? 1 : 0; float target = game.HeadsetActive ? 70f : (Application.isMobilePlatform ? 30f : 45f);
        if (perfFps < target * 0.92f && perfLevel < 3) { perfLevel++; perfGood = 0; PerfApply(); }
        else if (perfFps > target * 1.35f && perfLevel > floor) { if (++perfGood >= 4) { perfGood = 0; perfLevel--; PerfApply(); } }
        else perfGood = 0;
    }
    void PerfApply()
    {
        if (perfLevel < 0 || baseShadow < 0f) return;
        float[] sh = { 1f, 0.7f, 0.45f, 0.3f }, lod = { 1f, 0.85f, 0.7f, 0.55f }, vp = { 1f, 0.9f, 0.82f, 0.75f };
        QualitySettings.shadowDistance = baseShadow * sh[perfLevel]; QualitySettings.lodBias = baseLod * lod[perfLevel];
        if (game.HeadsetActive) UnityEngine.XR.XRSettings.renderViewportScale = vp[perfLevel];
        TileRing = perfLevel >= 2 ? 2 : 3;
    }
    void PerfRestore()
    {
        if (baseShadow >= 0f) { QualitySettings.shadowDistance = baseShadow; QualitySettings.lodBias = baseLod; }
        UnityEngine.XR.XRSettings.renderViewportScale = 1f;
    }
    public string ProbePerf() { return "fps " + perfFps.ToString("F0") + " level " + perfLevel + " shadows " + QualitySettings.shadowDistance.ToString("F0") + " lod " + QualitySettings.lodBias.ToString("F2") + " ring " + TileRing; }

    // ================================================================== a sky that sparkles, and a far-away supernova
    bool sky23; readonly List<Transform> halos = new List<Transform>(), flares = new List<Transform>(); readonly List<Material> haloMats = new List<Material>(); readonly List<Color> haloCol = new List<Color>(); readonly List<int> flareStar = new List<int>();
    Transform novaStar, novaHalo; Material novaMat, novaHaloMat; Vector3 novaDir; float novaNext = 360f, novaStart = -1f; int novaSeen;

    void BuildSky23()
    {
        sky23 = true; if (!skyRoot || !clearSrc) return;
        // forty more small stars, so the sky is full
        Material[] star = { Glow(new Color(2.6f, 2.4f, 1.6f)), Glow(new Color(1.6f, 2.2f, 2.8f)), Glow(new Color(2.8f, 1.7f, 2.4f)), Glow(new Color(1.8f, 2.8f, 2.0f)) };
        for (int i = 0; i < 40; i++)
        {
            var s = Prim(PrimitiveType.Sphere, skyRoot, Vector3.zero, Vector3.one, star[i % star.Length]); s.name = "Twinkle"; s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            twinkles.Add(s.transform); twDir.Add(SkyDir(R(0f, 360f), R(6f, 85f))); twSize.Add(R(5f, 12f));
        }
        var tex = Radial(2.2f); Color[] tint = { new Color(1f, 0.92f, 0.65f, 0.32f), new Color(0.6f, 0.85f, 1f, 0.32f), new Color(1f, 0.65f, 0.9f, 0.32f), new Color(0.7f, 1f, 0.75f, 0.32f) };
        for (int i = 0; i < twinkles.Count; i++)
        {
            var m = new Material(clearSrc); m.SetTexture("_BaseMap", tex); var col = tint[i % tint.Length]; m.SetColor("_BaseColor", col); m.renderQueue = 2895;
            var q = Prim(PrimitiveType.Quad, skyRoot, Vector3.zero, Vector3.one, m); q.name = "Star glow"; var r = q.GetComponent<Renderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            halos.Add(q.transform); haloMats.Add(m); haloCol.Add(col);
            if (i % 3 == 0)
            {
                // a four-point sparkle: two thin bright strokes that flash now and then
                var f = new GameObject("Sparkle").transform; f.SetParent(skyRoot, false);
                var sm = new Material(clearSrc); sm.SetTexture("_BaseMap", tex); sm.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.85f)); sm.renderQueue = 2896;
                foreach (float rot in new[] { 0f, 90f }) { var a = Prim(PrimitiveType.Quad, f, Vector3.zero, new Vector3(1f, 0.08f, 1f), sm); a.transform.localRotation = Quaternion.Euler(0f, 0f, rot); a.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                flares.Add(f); flareStar.Add(i);
            }
        }
        novaMat = new Material(Glow(Color.white)); var ns = Prim(PrimitiveType.Sphere, skyRoot, Vector3.zero, Vector3.one, novaMat); ns.name = "Supernova"; ns.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; novaStar = ns.transform;
        novaHaloMat = new Material(clearSrc); novaHaloMat.SetTexture("_BaseMap", tex); novaHaloMat.renderQueue = 2895; var nh = Prim(PrimitiveType.Quad, skyRoot, Vector3.zero, Vector3.one, novaHaloMat); nh.name = "Supernova glow"; nh.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; novaHalo = nh.transform;
        novaStar.gameObject.SetActive(false); novaHalo.gameObject.SetActive(false);
        novaNext = Time.time + R(240f, 420f);
    }

    void LateSky23(float t)
    {
        if (!sky23) BuildSky23();
        if (!cam || halos.Count == 0) return;
        Vector3 c = cam.position;
        for (int i = 0; i < halos.Count && i < twinkles.Count; i++)
        {
            float glow = 0.75f + 0.25f * Mathf.Sin(t * (0.8f + (i % 7) * 0.23f) + i * 2.1f);
            halos[i].position = c + twDir[i] * 2990f; halos[i].rotation = Quaternion.LookRotation(twDir[i]); halos[i].localScale = Vector3.one * twSize[i] * 3.4f * glow;
            var col = haloCol[i]; col.a *= glow; haloMats[i].SetColor("_BaseColor", col);
        }
        for (int j = 0; j < flares.Count; j++)
        {
            int i = flareStar[j]; float flash = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * (0.35f + (i % 5) * 0.07f) + i * 1.3f)), 14f);
            flares[j].position = c + twDir[i] * 2985f; flares[j].rotation = Quaternion.LookRotation(twDir[i]) * Quaternion.Euler(0f, 0f, 45f + t * 15f); flares[j].localScale = Vector3.one * twSize[i] * (0.2f + 7f * flash);
        }
        // the supernova: far away and small. It brightens for 6 seconds, shines for 8, and fades over half a minute.
        if (!novaStar) return;
        if (novaStart < 0f && Playing && Time.time >= novaNext)
        {
            novaStart = Time.time; novaDir = SkyDir(R(0f, 360f), R(18f, 62f)); novaSeen++;
            novaStar.gameObject.SetActive(true); novaHalo.gameObject.SetActive(true);
            if (!hunchOn) sim.Say("ARIA: Look up! A far-away star just flared up: a supernova, very far from here and completely safe. It will fade in a minute.");
        }
        if (novaStart < 0f) return;
        float age = Time.time - novaStart, b = age < 6f ? age / 6f : (age < 14f ? 1f : Mathf.Clamp01(1f - (age - 14f) / 31f));
        if (age > 45f) { novaStart = -1f; novaNext = Time.time + R(420f, 780f); novaStar.gameObject.SetActive(false); novaHalo.gameObject.SetActive(false); return; }
        Color col2 = Color.Lerp(Color.Lerp(new Color(1f, 1f, 1f), new Color(0.6f, 0.82f, 1f), Mathf.Clamp01(age / 20f)), new Color(1f, 0.55f, 0.85f), Mathf.Clamp01((age - 20f) / 25f));
        novaStar.position = c + novaDir * 2980f; novaStar.localScale = Vector3.one * (10f + 22f * b); novaMat.SetColor("_BaseColor", col2 * (1.5f + 2.5f * b));
        novaHalo.position = c + novaDir * 2975f; novaHalo.rotation = Quaternion.LookRotation(novaDir); novaHalo.localScale = Vector3.one * (40f + 90f * b) * (1f + 0.06f * Mathf.Sin(t * 3f));
        var hc = col2; hc.a = 0.45f * b; novaHaloMat.SetColor("_BaseColor", hc);
    }
    public string ProbeSupernova() { novaNext = 0f; return "supernova soon; stars " + twinkles.Count + " glows " + halos.Count + " sparkles " + flares.Count; }
    public string ProbeNovaView() { if (novaStart < 0f) return "no supernova"; if (desk) desk.SetLook(Mathf.Atan2(novaDir.x, novaDir.z) * Mathf.Rad2Deg, -Mathf.Asin(novaDir.y) * Mathf.Rad2Deg); return "looking at the supernova, " + (Time.time - novaStart).ToString("F0") + " s old"; }

    // ================================================================== the glowing zone orbs never block your view
    readonly Dictionary<Transform, Vector3> orbBase = new Dictionary<Transform, Vector3>();
    void UpdateOrbs()
    {
        if (!cam) return;
        foreach (var z in game.zones)
        {
            if (!z.interactable) continue; var t = z.interactable.transform; Vector3 bs;
            if (!orbBase.TryGetValue(t, out bs)) { bs = t.localScale; orbBase[t] = bs; }
            float d = (t.position - cam.position).magnitude, k = Mathf.Clamp01((d - 0.9f) / 2.4f);      // closer than about 3 m it shrinks away, right up to you it is a small dot
            Vector3 want = bs * Mathf.Lerp(0.12f, 1f, k); if ((t.localScale - want).sqrMagnitude > 0.000001f) t.localScale = want;
        }
    }
    public string ProbePadShape()
    {
        var model = GameObject.Find("HUNCH_Lunar_Base"); var t = model ? model.transform.Find("BAKED_KINGDOM_Ground") : null; if (!t) return "no pad";
        var col = t.GetComponent<Collider>(); if (!col) return "pad has no collider";
        int n = 0, high = 0, mid = 0; float max = -99f, min = 99f; string ex = "";
        for (int ix = -27; ix <= 27; ix++) for (int iz = -27; iz <= 27; iz++)
        {
            RaycastHit h; if (!col.Raycast(new Ray(new Vector3(ix * 10f, 60f, iz * 10f), Vector3.down), out h, 100f)) continue; n++;
            float y = h.point.y; max = Mathf.Max(max, y); min = Mathf.Min(min, y); if (y > 0.3f) { mid++; if (y > 1.5f) { high++; if (ex.Length < 300) ex += "(" + ix * 10 + "," + iz * 10 + ":" + y.ToString("F1") + ") "; } }
        }
        return "pad hits " + n + " above 0.3 m: " + mid + " above 1.5 m: " + high + " min " + min.ToString("F2") + " max " + max.ToString("F1") + " | " + ex;
    }
}
