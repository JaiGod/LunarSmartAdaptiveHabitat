#pragma warning disable 0414, 0219
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using AR = HunchAres;

/// Project ARES layer (the Student Brief rubric, 200 points) inside Lunar Habitat Commander, plus two clean-ups of the base model:
///  - FLOORS: several floors of the Blender model sit at exactly the same height (the hub's second floor and its shell, the crew
///    core deck, the node and tunnel floors, the tower floors and roof, the paved pad and the Moon-dust ground). Two surfaces at
///    the same height flicker ("z-fighting"). Each pair is now separated by a few centimetres, so one clean floor shows.
///  - TREES: trees belong only in the greenhouse. The tree in the medical bay and the one on the crew core's upper deck are cut
///    out of the model when the game starts (the greenhouse is never touched); the hub's forest is switched off (HubClearEarly).
///  - ARES: the same AI engine as the Unity 3 simulator (six conditions, nine sensors, three tiers, consent, hard limits),
///    with every deliverable D1–D6, the review board questions Q1–Q6, the 72-hour tabletop (Bonus A) and the crew interface
///    (Bonus B) on one menu page: Menu > Project ARES, or F11. F10 opens the crew interface, F12 is the AI OFF switch.
public partial class HunchFrontier
{
    // ================================================================== floors that overlap
    string floorLog = "", treeLog = ""; bool aresErr, aresMissionErr;
    static readonly (string part, float dy)[] FloorNudge = {
        ("HUB_Shell", -0.03f), ("NODES_Shell", -0.03f), ("CORE_Shell", -0.03f), ("HUB_Tower_Shell", -0.03f), ("HUB_Tower_Core", -0.03f),
        ("NODES_Interior", 0.02f), ("SITE", 0.015f), ("KINGDOM_Ground", 0.03f), ("TERRAIN", -0.04f) };

    void FixFloorsAndTrees()
    {
        var model = GameObject.Find("HUNCH_Lunar_Base"); if (!model) { floorLog = "base model not found"; return; }
        int moved = 0;
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
        {
            if (t == model.transform) continue; int us = t.name.IndexOf('_'); if (us < 0) continue;
            string part = t.name.Substring(us + 1);
            foreach (var n in FloorNudge) if (part == n.part) { t.position += Vector3.up * n.dy; moved++; }
        }
        floorLog = moved + " model parts separated from the floor they overlapped";
        int trees = 0, tris = 0; string perMesh = "";
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
        {
            string n = mf.name; if (!n.EndsWith("_Interior") || n.Contains("AGRI") || (n.Contains("HUB_Interior") && !mf.gameObject.activeInHierarchy)) continue;      // the greenhouse keeps its trees; a switched-off hub forest is left alone
            try { int k, r; RemoveTrees(mf, out k, out r); trees += k; tris += r; if (k > 0) { var bb = mf.GetComponent<Renderer>().bounds; perMesh += n + ":" + k + "/" + r + " @" + bb.center.ToString("F0") + "; "; } } catch (Exception e) { treeLog += n + ": " + e.Message + "; "; }
        }
        treeLog = trees + " trees outside the greenhouse removed (" + tris + " triangles) " + perMesh + (treeLog.Length > 0 ? "; " + treeLog : "");
        Physics.SyncTransforms();
        Debug.Log("[HUNCH] floors: " + floorLog + " | trees: " + treeLog);
    }

    /// Finds trees in a baked mesh by their shape (a cluster of round crowns on a trunk) and cuts them out. Works in world space,
    /// so it does not matter how the model was scaled or turned on import.
    void RemoveTrees(MeshFilter mf, out int treeCount, out int removedTris)
    {
        treeCount = 0; removedTris = 0;
        var src = mf.sharedMesh; if (!src || !src.isReadable) return;
        var tr = mf.transform; var lv = src.vertices; int nv = lv.Length;
        var wv = new Vector3[nv]; for (int i = 0; i < nv; i++) wv[i] = tr.TransformPoint(lv[i]);
        // weld by position (the importer splits vertices along UV seams)
        var weld = new int[nv]; var map = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < nv; i++) { var key = new Vector3Int(Mathf.RoundToInt(wv[i].x * 1000f), Mathf.RoundToInt(wv[i].y * 1000f), Mathf.RoundToInt(wv[i].z * 1000f)); int id; if (!map.TryGetValue(key, out id)) { id = map.Count; map[key] = id; } weld[i] = id; }
        var par = new int[map.Count]; for (int i = 0; i < par.Length; i++) par[i] = i;
        Func<int, int> F = null; F = x => { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; };
        var subs = new int[src.subMeshCount][];
        for (int s = 0; s < subs.Length; s++)
        {
            subs[s] = src.GetTriangles(s);
            for (int i = 0; i < subs[s].Length; i += 3) { int a = F(weld[subs[s][i]]), b = F(weld[subs[s][i + 1]]), c = F(weld[subs[s][i + 2]]); par[b] = a; par[F(c)] = a; }
        }
        // per component: bounds and welded-vertex count
        var mn = new Dictionary<int, Vector3>(); var mx = new Dictionary<int, Vector3>(); var cnt = new Dictionary<int, HashSet<int>>();
        for (int i = 0; i < nv; i++)
        {
            int c = F(weld[i]); Vector3 p = wv[i];
            if (!mn.ContainsKey(c)) { mn[c] = p; mx[c] = p; cnt[c] = new HashSet<int>(); }
            mn[c] = Vector3.Min(mn[c], p); mx[c] = Vector3.Max(mx[c], p); cnt[c].Add(weld[i]);
        }
        float floor = mf.GetComponent<Renderer>() ? mf.GetComponent<Renderer>().bounds.min.y : wv.Min(p => p.y);
        // round crowns: about as wide as deep, well above the floor, a low-poly sphere
        var crowns = new List<int>();
        foreach (var c in mn.Keys)
        {
            Vector3 e = mx[c] - mn[c]; int k = cnt[c].Count;
            if (e.x < 1f || e.x > 9f || e.z < 1f || e.z > 9f || Mathf.Abs(e.x - e.z) > 0.45f * Mathf.Max(e.x, e.z) || e.y < 0.6f * Mathf.Min(e.x, e.z) || mn[c].y < floor + 2f || k < 30 || k > 200) continue;
            Vector3 ctr = (mn[c] + mx[c]) * 0.5f, half = Vector3.Max(e * 0.5f, Vector3.one * 0.001f); float s1 = 0f, s2 = 0f; int m = 0;
            for (int i = 0; i < nv; i++) if (F(weld[i]) == c) { Vector3 q = wv[i] - ctr; float r = new Vector3(q.x / half.x, q.y / half.y, q.z / half.z).magnitude; s1 += r; s2 += r * r; m++; }
            float sd = m > 0 ? Mathf.Sqrt(Mathf.Max(0f, s2 / m - (s1 / m) * (s1 / m))) : 1f;
            if (sd < 0.12f) crowns.Add(c);
        }
        var used = new HashSet<int>(); var remove = new HashSet<int>();
        foreach (var c in crowns)
        {
            if (used.Contains(c)) continue; Vector3 cc = (mn[c] + mx[c]) * 0.5f;
            var grp = crowns.Where(o => !used.Contains(o) && new Vector2(((mn[o] + mx[o]) * 0.5f - cc).x, ((mn[o] + mx[o]) * 0.5f - cc).z).magnitude < 8f).ToList();
            foreach (var o in grp) used.Add(o);
            if (grp.Count < 3) continue;
            Vector3 lo = grp.Select(o => mn[o]).Aggregate(Vector3.Min), hi = grp.Select(o => mx[o]).Aggregate(Vector3.Max);
            bool trunk = false;
            foreach (var o in mn.Keys)
            {
                Vector3 e = mx[o] - mn[o], m2 = (mn[o] + mx[o]) * 0.5f;
                if (e.x < 3.2f && e.z < 3.2f && e.y > 1.2f && mx[o].y > lo.y - 0.5f && mn[o].y < lo.y && m2.x > lo.x - 0.5f && m2.x < hi.x + 0.5f && m2.z > lo.z - 0.5f && m2.z < hi.z + 0.5f) { trunk = true; break; }
            }
            if (!trunk) continue;
            treeCount++;
            foreach (var o in mn.Keys)
                if (mn[o].x >= lo.x - 0.6f && mx[o].x <= hi.x + 0.6f && mn[o].z >= lo.z - 0.6f && mx[o].z <= hi.z + 0.6f && mx[o].y > floor + 1f) remove.Add(o);
        }
        if (treeCount == 0) return;
        var mesh = Instantiate(src); mesh.name = src.name + " (no trees)";
        for (int s = 0; s < subs.Length; s++)
        {
            var keep = new List<int>(subs[s].Length);
            for (int i = 0; i < subs[s].Length; i += 3) { if (remove.Contains(F(weld[subs[s][i]]))) { removedTris++; continue; } keep.Add(subs[s][i]); keep.Add(subs[s][i + 1]); keep.Add(subs[s][i + 2]); }
            mesh.SetTriangles(keep, s);
        }
        mesh.RecalculateBounds(); mf.sharedMesh = mesh;
        var mc = mf.GetComponent<MeshCollider>(); if (mc) { mc.sharedMesh = null; mc.sharedMesh = mesh; }
    }
    public string ProbeFloors() { return "floors: " + floorLog + " | ground: " + terrainFix + " | trees: " + treeLog; }

    // ================================================================== Project ARES
    AR.MoodEngine ares; HunchMenu.ExtraPage pageAres, pageAresDocs, pageAresDash, pageAresCrew, pageAresCmo, pageAresBoard, pageAresOptOut, pageAresScore, pageAresSensors;
    int aresDocFirst = -1, aresSheet = -1; float aresClockH = -1f, aresLive, aresFrozenH; string aresSheetText = "", aresSheetTitle = "";
    readonly Dictionary<string, Light> aresLights = new Dictionary<string, Light>(); Transform aresRoot, aresConfigB;
    readonly HashSet<string> aresShown = new HashSet<string>();      // what has been shown, for the rubric self-check
    static readonly string[] LeverZone = { "sanctuary|quarters|observation", "greenhouse", "galley", "work|control|lab", "quarters", "observation|sanctuary" };
    static readonly float[] LeverKelvin = { 2400f, 5000f, 2700f, 6500f, 3200f, 4000f };
    static readonly string[] LeverName = { "calm", "activate", "separate", "alert", "autonomy", "novelty" };

    /// The Blender terrain (dark, hilly) and the endless Moon ground (light dust, made in code) overlap all round the base,
    /// so dark patches poke through the light ground. Both are now dressed in the same Moon dust, so they read as ONE floor.
    string terrainFix = "";
    void OneMoonFloor()
    {
        var model = GameObject.Find("HUNCH_Lunar_Base"); if (!model || terrMat == null) { terrainFix = "no model or ground material"; return; }
        int n = 0;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            string nm = r.name; if (!nm.EndsWith("_TERRAIN")) continue;      // the paved pad was re-surfaced already by the endless-ground code
            var pm = new Material(terrMat); Vector2 tile = new Vector2(r.bounds.size.x / 22f, r.bounds.size.z / 22f);
            pm.mainTextureScale = tile; if (pm.HasProperty("_BaseMap")) pm.SetTextureScale("_BaseMap", tile); if (pm.HasProperty("_BumpMap")) pm.SetTextureScale("_BumpMap", tile);
            var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = pm; r.sharedMaterials = ms; n++;
        }
        terrainFix = n + " ground meshes dressed in the same Moon dust";
    }

    void BuildAres()
    {
        try { OneMoonFloor(); } catch (Exception e) { terrainFix = "failed: " + e.Message; }
        aresRoot = new GameObject("Project ARES").transform; aresRoot.SetParent(root, false);
        var names = new string[4]; for (int i = 0; i < 4; i++) names[i] = i < sim.crew.Count ? sim.crew[i].name : null;
        AR.AresHost.Names = names;
        ares = new AR.MoodEngine(UnityEngine.Random.Range(1, 9999)); AR.AresHost.Engine = ares;
        ares.cmoId = 2; ares.commanderId = 1; ares.playerId = 1;
        ares.onIntervention = AresLever;
        ares.onOffer = o => { aresShown.Add("offer"); sim.Say("ARIA: " + o.text + "  (Menu > Project ARES > Crew interface to answer: yes or no)"); Note("AI offer waiting: F10"); };
        ares.onToast = s => { sim.Say("ARIA: " + s); Note(s); };
        AR.SimClock.hours = AresNow(); aresClockH = AR.SimClock.hours; AR.EthicsGuard.lastReviewHour = AR.SimClock.hours; AR.EthicsGuard.blocked = 0; AR.EthicsGuard.blockedLog.Clear();
        AR.AresHost.SetConfigB = SetConfigB; AR.AresHost.AiOff = false; AR.AresHost.ConfigB = false;
        AR.DesignDocs.ZoneList = () => string.Join("\n", game.zones.Select(z => "  " + z.title + "  — " + AresTypeOf(z.id)));
        AR.DesignDocs.ZoneLive = type => { var zs = game.zones.Where(z => AresTypeOf(z.id) == type).Select(z => "  " + z.title + (aresLights.Values.Any(l => l && (l.transform.position - z.position).sqrMagnitude < 4f) ? "   AI support active here" : "   design settings")); return zs.Any() ? string.Join("\n", zs) : "  (this zone type is part of the shared spaces)"; };

        // every deliverable page as a sheet (works in the headset: the same sheet as the tablets)
        aresDocFirst = reads.Count;
        foreach (var p in AR.DesignDocs.Pages) { var pp = p; reads.Add(new ReadItem { title = pp.title, zone = "Project ARES", got = true, body = pp.body, img = pp.img }); }
        aresSheet = reads.Count;
        reads.Add(new ReadItem { title = "Project ARES", zone = "Project ARES", got = true, body = () => aresSheetText });
        BuildAresMenu();
        try { BuildAresMissions(); } catch (Exception e) { Debug.LogError("[HUNCH] ARES missions: " + e); }
        try { BuildAresWorld(); AddAresWorldMenu(); } catch (Exception e) { Debug.LogError("[HUNCH] ARES world: " + e); }
    }
    float AresNow() { return (sim.Day - 1) * 24f + sim.Hour; }
    string AresTypeOf(string id)
    {
        id = (id ?? "").ToLowerInvariant();
        if (id.Contains("galley") || id.Contains("meeting") || id.Contains("hub") || id.Contains("shelter")) return "communal";      // the storm shelter is where everyone gathers
        if (id.Contains("fab")) return "work";
        if (id.Contains("quarters") || id.Contains("sleep") || id.Contains("hygiene")) return "private";
        if (id.Contains("work") || id.Contains("control") || id.Contains("lab") || id.Contains("medical") || id.Contains("eclss") || id.Contains("storage") || id.Contains("power")) return "work";
        if (id.Contains("exercise") || id.Contains("gym")) return "exercise";
        if (id.Contains("greenhouse") || id.Contains("observation") || id.Contains("sanctuary")) return "nature";
        return "transition";
    }
    void AresSheet(string title, string text) { aresSheetTitle = title; aresSheetText = text; reads[aresSheet].title = title; menu.Close(); OpenRead(aresSheet); }
    void AresDoc(int i) { if (i < 0) return; aresShown.Add("doc:" + AR.DesignDocs.Pages[i].section); menu.Close(); OpenRead(aresDocFirst + i); }

    void UpdateAres(float dt)
    {
        if (ares == null) return;
        try { UpdateAresWorld(); } catch { }
        try { UpdateAresMissions(dt); } catch (Exception e) { if (!aresMissionErr) { aresMissionErr = true; Debug.LogError("[HUNCH] ARES missions: " + e); } }
        var kb = Keyboard.current;
        if (kb != null && !game.menuOpen && readOpen < 0)
        {
            if (kb.f11Key.wasPressedThisFrame) menu.OpenExtra(pageAres);
            else if (kb.f10Key.wasPressedThisFrame) menu.OpenExtra(pageAresCrew);
            else if (kb.f12Key.wasPressedThisFrame) AresAiOff(!AR.AresHost.AiOff);
        }
        if (readOpen == aresSheet || (readOpen >= aresDocFirst && readOpen < aresSheet)) { aresLive += dt; if (aresLive > 1f) { aresLive = 0f; if (reads[readOpen].img == null) readText.text = reads[readOpen].body(); } }
        float now = AresNow();
        if (now < aresClockH - 1f || now > aresClockH + 48f) { AR.SimClock.hours = now; aresClockH = now; return; }      // a restart or a big jump: resync, no catch-up
        if (AR.AresHost.AiOff || !Playing) { aresClockH = now; return; }
        int guard = 0;
        while (aresClockH + 1f / 60f <= now && guard++ < 240)
        {
            aresClockH += 1f / 60f;
            for (int k = 0; k < 4 && k < sim.crew.Count; k++)
            {
                var c = ares.C(k + 1); var g = sim.crew[k];
                c.stress = Mathf.Lerp(c.stress, g.stress / 100f, 0.01f); c.mood = Mathf.Lerp(c.mood, g.mood / 100f, 0.01f);
                try { c.asleep = CrewAsleep(k); } catch { }
                float hod = AR.SimClock.HourOfDay; c.exercising = !c.asleep && (int)hod == 14 + (k % 2);
                if (c.exercising) c.lastExerciseHour = AR.SimClock.hours;
            }
            AR.SimClock.hours = aresClockH - 1f / 60f; ares.Step(1f / 60f);
        }
        if (guard >= 240) aresClockH = now;
        // novelty lever: a slow hue drift
        Light nl; if (aresLights.TryGetValue("novelty", out nl) && nl) nl.color = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.03f, 1f), 0.35f, 1f);
    }

    /// what the AI does to the world: a light in the zone the lever belongs to (warm for calm, bright for activation ...).
    /// Every change is reversible, logged and switched off by the AI OFF switch.
    void AresLever(string lever, int crew, int cond)
    {
        if (AR.AresHost.AiOff) return;
        string who = crew > 0 && crew <= 4 ? ares.C(crew).name : "crew";
        if (lever == "configB") { SetConfigB(true); return; }
        if (lever == "tier3") { AR.DesignDocs.LastAction = "Tier 3 for " + who + ": the human chain of care is active"; return; }
        if (lever.StartsWith("restore:")) { string ln = lever.Substring(8); Light l; if (aresLights.TryGetValue(ln, out l) && l) Destroy(l.gameObject); aresLights.Remove(ln); AR.DesignDocs.LastAction = "restored " + ln + " zone (30-min fade)"; return; }
        if (lever.StartsWith("accepted:")) { int k2 = crew - 1; if (k2 >= 0 && k2 < sim.crew.Count) { sim.crew[k2].stress = Mathf.Max(0f, sim.crew[k2].stress - 10f); sim.crew[k2].mood = Mathf.Min(100f, sim.crew[k2].mood + 6f); sim.psychHelped++; } return; }
        int c = Mathf.Clamp(cond, 0, 5); string key = LeverName[c];
        int zi = ZoneIndex(LeverZone[c].Split('|')); if (zi < 0) return;
        Light li; if (!aresLights.TryGetValue(key, out li) || !li)
        {
            var go = new GameObject("ARES lever: " + key); go.transform.SetParent(aresRoot, false); go.transform.position = game.zones[zi].position + Vector3.up * 3.2f;
            li = go.AddComponent<Light>(); li.type = LightType.Point; li.range = 14f; li.shadows = LightShadows.None; aresLights[key] = li;
        }
        li.color = AR.AresHost.Cct(LeverKelvin[c]); li.intensity = lever.EndsWith(":2") ? 3.2f : 2.0f;
        AR.DesignDocs.LastAction = (lever.EndsWith(":2") ? "Tier 2" : "Tier 1") + " " + AR.CondSpec.Name[c] + " for " + who + ": " + game.zones[zi].title + " lit at " + LeverKelvin[c] + " K";
    }

    /// a spot beside a zone that is not inside a wall or a counter (tries a few distances, then the other side)
    Vector3 AresFreeSpot(Vector3 centre, Vector3 dir, float up, Vector3 half, Quaternion rot)
    {
        foreach (float sgn in new[] { 1f, -1f })
            foreach (float d in new[] { 3.4f, 2.6f, 4.2f, 1.9f, 5.0f })
            {
                Vector3 p = centre + dir * (d * sgn) + Vector3.up * (up > 0f ? up : half.y + 0.05f);
                if (!Physics.CheckBox(p, half, rot, ~0, QueryTriggerInteraction.Ignore)) return up > 0f ? p : p - Vector3.up * (half.y + 0.05f);
            }
        return centre + dir * 3.4f + Vector3.up * up;
    }

    void SetConfigB(bool on)
    {
        AR.AresHost.ConfigB = on; aresShown.Add("configB");
        if (aresConfigB) { Destroy(aresConfigB.gameObject); aresConfigB = null; }
        if (!on) { sim.Say("ARIA: Configuration A (nominal): one shared dining table, open galley, one main route."); return; }
        aresConfigB = new GameObject("Config B (conflict protocol)").transform; aresConfigB.SetParent(aresRoot, false);
        var orange = Lit(new Color(1f, 0.55f, 0.15f)); var wood = Lit(new Color(0.55f, 0.4f, 0.25f)); var glow = Glow(new Color(1.6f, 1.0f, 0.3f));
        int gi = ZoneIndex("galley"), li = ZoneIndex("observation", "sanctuary", "exercise");
        if (gi >= 0)
        {
            var z = game.zones[gi]; Vector3 f = Quaternion.Euler(0f, z.yaw, 0f) * Vector3.forward, r = Vector3.Cross(Vector3.up, f);
            // acoustic screens on the ceiling track, either side of the table: no colliders, so nothing gets in the way
            for (int s = -1; s <= 1; s += 2)
            {
                var sc = Prim(PrimitiveType.Cube, aresConfigB, Vector3.zero, new Vector3(3.2f, 2.4f, 0.06f), orange); sc.name = "Acoustic screen";
                sc.transform.position = AresFreeSpot(z.position, r * s, 1.6f, new Vector3(1.6f, 1.1f, 0.2f), Quaternion.LookRotation(r)); sc.transform.rotation = Quaternion.LookRotation(r);
            }
            var lab = Label(aresConfigB, "<b>CONFIG B</b>\n<size=60%>acoustic screens: quiet halves of the galley</size>", 0f, 0f, 0.5f, 4f, 1f, new Color(1f, 0.8f, 0.4f)); lab.transform.position = z.position + Vector3.up * 3.3f; mapLabels.Add(lab);
            // the lit alternate route: a glowing strip on the floor from the galley
            var strip = Prim(PrimitiveType.Cube, aresConfigB, Vector3.zero, new Vector3(0.25f, 0.02f, 8f), glow); strip.name = "Alternate route light";
            strip.transform.position = z.position - f * 5f + r * 2f + Vector3.up * 0.05f; strip.transform.rotation = Quaternion.LookRotation(f);
        }
        if (li >= 0)
        {
            var z = game.zones[li]; Vector3 f = Quaternion.Euler(0f, z.yaw, 0f) * Vector3.forward, r = Vector3.Cross(Vector3.up, f);
            Vector3 at = AresFreeSpot(z.position, r, 0f, new Vector3(0.9f, 0.5f, 0.9f), Quaternion.identity);
            var top = Prim(PrimitiveType.Cylinder, aresConfigB, Vector3.zero, new Vector3(1.4f, 0.04f, 1.4f), wood); top.transform.position = at + Vector3.up * 0.95f; top.name = "Fold-out table";
            Prim(PrimitiveType.Cylinder, aresConfigB, Vector3.zero, new Vector3(0.12f, 0.47f, 0.12f), wood).transform.position = at + Vector3.up * 0.47f;
            for (int s = -1; s <= 1; s += 2) Prim(PrimitiveType.Cylinder, aresConfigB, Vector3.zero, new Vector3(0.45f, 0.25f, 0.45f), orange).transform.position = at + f * (s * 1.1f) + Vector3.up * 0.25f;
            var lab = Label(aresConfigB, "<b>CONFIG B</b>\n<size=60%>second dining spot: nobody is forced together</size>", 0f, 0f, 0.5f, 4f, 1f, new Color(1f, 0.8f, 0.4f)); lab.transform.position = at + Vector3.up * 2.4f; mapLabels.Add(lab);
        }
        sim.Say("ARIA: Configuration B (conflict protocol) deployed: acoustic screens in the galley, a second dining spot, a lit alternate route. Nobody is told who is in conflict.");
    }

    void AresAiOff(bool off)
    {
        AR.AresHost.AiOff = off; aresShown.Add("aioff");
        if (off) { foreach (var l in aresLights.Values) if (l) Destroy(l.gameObject); aresLights.Clear(); ares.Log(1, "override", "AI OFF switch pressed: the AI may change nothing; every zone back to its design settings."); }
        else ares.Log(1, "override", "AI switched back on by the crew.");
        sim.Say(off ? "ARIA: AI OFF. I will change nothing until a crew member switches me back on. Every zone is back to normal." : "ARIA: AI back on. Monitoring resumes; I will start with silent, reversible support only.");
        Banner(off ? "AI OFF" : "AI ON", off ? "The AI may change nothing. Systems back to normal." : "Monitoring resumes", off);
        if (menu) menu.Rebuild();
    }

    // ------------------------------------------------------------------ the menu pages
    void BuildAresMenu()
    {
        if (!menu) return;
        pageAres = new HunchMenu.ExtraPage { title = "PROJECT ARES — AGENCY REVIEW BOARD", label = "Project ARES: rubric, deliverables, board questions (F11)", hint = "Everything the Project ARES student brief grades: D1 sensors, D2 zones, D3 AI protocols, D4 ethics, the six board questions run live, the 72-hour tabletop and the crew interface." };
        pageAresDocs = new HunchMenu.ExtraPage { title = "DELIVERABLES D1–D6", parent = pageAres };
        pageAresDash = new HunchMenu.ExtraPage { title = "AI DASHBOARD (LIVE)", parent = pageAres };
        pageAresSensors = new HunchMenu.ExtraPage { title = "SENSOR HEALTH", parent = pageAresDash };
        pageAresCrew = new HunchMenu.ExtraPage { title = "CREW INTERFACE — " + (sim.crew.Count > 0 ? sim.crew[0].name : "you"), parent = pageAres };
        pageAresOptOut = new HunchMenu.ExtraPage { title = "MY SENSORS (OPT OUT)", parent = pageAresCrew };
        pageAresCmo = new HunchMenu.ExtraPage { title = "CMO CONSOLE", parent = pageAres };
        pageAresBoard = new HunchMenu.ExtraPage { title = "AGENCY REVIEW BOARD QUESTIONS", parent = pageAres };
        pageAresScore = new HunchMenu.ExtraPage { title = "RUBRIC SELF-CHECK (200 POINTS)", parent = pageAres };

        pageAres.build = m =>
        {
            aresShown.Add("ares");
            m.AddItem("Deliverables D1–D6 (" + AR.DesignDocs.Pages.Count + " pages, live numbers)", "D1 sensor matrix, D2 zones and Config A / B, D3 flowcharts for all six conditions, D4 ethics and hard limits, D5 run of show, D6 journal prompts.", () => m.OpenExtra(pageAresDocs));
            m.AddItem("AI dashboard (live tiers, sensors, hard limits)", "Each crew member's status, sensor health, break a sensor, test a hard limit, the weekly human review.", () => m.OpenExtra(pageAresDash));
            m.AddItem("Crew interface (F10)   " + (ares.offers.Any(o => !o.done) ? "<color=#FFD070>offer waiting</color>" : ""), "Bonus B: your wellness data, everything the AI did for you, STOP, pause, opt out, 'that reading is wrong', delete my history.", () => m.OpenExtra(pageAresCrew));
            m.AddItem("CMO console   " + (ares.escalatedTo.Count > 0 ? "<color=#FF9A8A>" + ares.escalatedTo.Count + " Tier 3</color>" : "no Tier 3"), "Tier 3 cases assigned to a human responder, and the acknowledge button.", () => m.OpenExtra(pageAresCmo));
            m.AddItem("Board questions Q1–Q6 (run live)", "Each question from the student brief runs on the real engine and explains what happened.", () => m.OpenExtra(pageAresBoard));
            m.AddItem("Bonus A: run the 72-hour tabletop simulation", "Two simultaneous events (depression + conflict), a sensor failure and the CMO on EVA, on the real engine. Writes a report.", () => { aresShown.Add("tabletop"); string path = AR.TabletopSim.Run(); AresSheet("72-hour tabletop simulation", AR.TabletopSim.lastReport); });
            m.AddItem("Configuration: " + (AR.AresHost.ConfigB ? "<color=#FFB060>B — conflict protocol</color>" : "A — nominal"), "Switch A and B. B adds acoustic screens in the galley, a second dining spot and a lit alternate route. Only a human switches it.", () => { SetConfigB(!AR.AresHost.ConfigB); m.Rebuild(); });
            m.AddItem("AI OFF switch (F12): " + (AR.AresHost.AiOff ? "<color=#FF9A8A>AI IS OFF</color>" : "<color=#7CFC9A>AI on</color>"), "Any crew member, any time, one tap. The AI may change nothing and every zone goes back to its design settings.", () => AresAiOff(!AR.AresHost.AiOff));
            m.AddItem("Rubric self-check (harsh)", "Every line of the 200-point rubric, where it is shown, and a strict score.", () => m.OpenExtra(pageAresScore));
            m.AddItem("Export everything to the Reports folder", "D1–D6 pages, your AI log and the tabletop report as text files next to the game.", () => { string p1 = AR.DesignDocs.ExportAll(); string p2 = AresExportLog(); AresSheet("Exported", "Saved:\n" + p1 + "\n" + p2); });
        };
        pageAresDocs.build = m =>
        {
            var pages = AR.DesignDocs.Pages; string sec = "";
            for (int i = 0; i < pages.Count; i++)
            {
                int ii = i; if (pages[i].section != sec) { sec = pages[i].section; m.AddItem("<color=#FFD070>-- " + sec + " --</color>", "", () => { }); }
                m.AddItem(pages[i].title, "Opens on the reading sheet (works in the headset).", () => AresDoc(ii));
            }
        };
        pageAresDash.build = m =>
        {
            aresShown.Add("dash");
            m.AddItem("Clock " + AR.SimClock.Now + " · detection confidence " + string.Join(" ", ares.crew.Select(c => c.confidence.ToString("P0"))), "Confidence falls when sensors fail or are opted out; under 50 % the AI may only use Tier 1.", () => { });
            foreach (var c in ares.crew) { var cc = c; m.AddItem(ares.CrewLine(cc, 1), cc.id == 1 ? "Your own detail." : "Other crew are shown only as status (hard limit 1). Detail needs their consent or a Tier-3 escalation.", () => { }); }
            m.AddItem("Sensor health (and break a sensor)", "Inject a failure and watch the weights renormalise and confidence drop (graceful degradation).", () => m.OpenExtra(pageAresSensors));
            m.AddItem("Test a hard limit: view Okafor's data as the Commander", "Blocked and logged: nobody sees another crew member's psychological data without consent.", () => { bool ok = AR.EthicsGuard.CanViewDetail(ares, 1, 2); sim.Say("ARIA: " + (ok ? "Allowed: escalated case." : "Blocked by hard limit 1. Logged for the weekly review.")); m.Rebuild(); });
            m.AddItem("Test a hard limit: AI changes the oxygen set-point", "Blocked: life-safety systems are human-only (hard limit 3).", () => { AR.EthicsGuard.LifeSafety("AI tried to change the ECLSS oxygen set-point"); sim.Say("ARIA: Blocked by hard limit 3. Life support is human-only."); m.Rebuild(); });
            m.AddItem("Test a hard limit: AI acts inside Tanaka's pod", "Blocked unless Tanaka invited the AI into the pod.", () => { AR.EthicsGuard.CanActInPod(ares.C(3), "Calm lighting"); sim.Say("ARIA: " + (ares.C(3).aiPodAccess ? "Allowed: Tanaka invited the AI." : "Blocked: Tanaka's pod is private.")); m.Rebuild(); });
            m.AddItem("Blocked actions so far: " + AR.EthicsGuard.blocked + (AR.EthicsGuard.blockedLog.Count > 0 ? "   last: " + AR.EthicsGuard.blockedLog.Last() : ""), "", () => { });
            m.AddItem("Weekly human system review: " + (AR.EthicsGuard.ReviewOverdue ? "<color=#FF9A8A>OVERDUE — AI limited to Tier 1</color>" : "done " + AR.SimClock.Format(AR.EthicsGuard.lastReviewHour)) + "   (sign now)", "Hard limit 7: at most 7 days without a human review, or the AI drops to Tier 1.", () => { AR.EthicsGuard.lastReviewHour = AR.SimClock.hours; ares.Log(0, "review", "Weekly human system review signed by the Commander and the CMO."); m.Rebuild(); });
            m.AddItem("Full AI log (last 40 lines)", "", () => AresSheet("AI log", string.Join("\n", ares.log.Skip(Math.Max(0, ares.log.Count - 40)).Select(l => l.Time + "  " + (l.crew > 0 ? ares.C(l.crew).name + ": " : "") + l.text))));
        };
        pageAresSensors.build = m =>
        {
            for (int s = 0; s < 9; s++)
            {
                int ss = s;
                m.AddItem(AR.SensorSpec.Name[s] + ":  " + string.Join("  ", ares.crew.Select(c => c.name.Split(' ').Last().Replace("(you)", "") + " " + (c.optOut[ss] ? "OO" : c.sensors[ss] == AR.SensorHealth.OK ? "ok" : c.sensors[ss] == AR.SensorHealth.Degraded ? "DG" : "XX"))),
                    "Click to cycle Tanaka's " + AR.SensorSpec.Name[s] + ": OK > degraded > failed > OK. " + AR.SensorSpec.Failure[s], () => { var c = ares.C(3); c.sensors[ss] = (AR.SensorHealth)(((int)c.sensors[ss] + 1) % 3); ares.Log(3, "sensor", AR.SensorSpec.Name[ss] + " now " + c.sensors[ss] + " (degraded mode: weights renormalise)."); ares.Step(0.01f); aresShown.Add("sensorfail"); m.Rebuild(); });
            }
            m.AddItem("Repair every sensor", "", () => { foreach (var c in ares.crew) for (int s = 0; s < 9; s++) c.sensors[s] = AR.SensorHealth.OK; m.Rebuild(); });
        };
        pageAresCrew.build = m =>
        {
            aresShown.Add("crew"); var me = ares.C(1);
            foreach (var o in ares.offers.Where(o => !o.done).ToList())
            {
                var oo = o;
                m.AddItem("<color=#FFD070>OFFER:</color> " + oo.text, "", () => { });
                m.AddItem("   Yes please", "The support starts. You chose it, so it also helps your sense of control.", () => { oo.done = true; oo.onAccept?.Invoke(); aresShown.Add("accept"); m.Rebuild(); });
                m.AddItem("   No thanks", "The AI stops at once, logs it and waits at least 6 hours (hard limit 2).", () => { oo.done = true; oo.onDecline?.Invoke(); aresShown.Add("refuse"); m.Rebuild(); });
            }
            m.AddItem("My wellbeing " + me.Wellbeing.ToString("P0") + " · confidence " + me.confidence.ToString("P0") + " (open)", "Only you see this: " + string.Join(", ", Enumerable.Range(0, 6).Select(k => AR.CondSpec.Name[k] + " " + me.smooth[k].ToString("0.00") + (me.tier[k] > 0 ? " (Tier " + me.tier[k] + ")" : ""))) + ".", () => AresSheet("My wellness data", "Only you see this.\n\n" + string.Join("\n", Enumerable.Range(0, 6).Select(k => AR.CondSpec.Name[k] + ": score " + me.smooth[k].ToString("0.00") + "  ·  Tier " + me.tier[k])) + "\n\nWellbeing " + me.Wellbeing.ToString("P0") + " · stress " + me.stress.ToString("0.00") + " · mood " + me.mood.ToString("0.00") + " · sleep quality " + me.sleepQ.ToString("0.00") + "\nDetection confidence " + me.confidence.ToString("P0") + " (falls when sensors fail or you opt out)."));
            m.AddItem("What the AI did for me (my log)", "Every action, silent ones too, with the reason and the confidence. Silent never means secret.", () => { aresShown.Add("mylog"); AresSheet("My AI log", string.Join("\n", me.log.Skip(Math.Max(0, me.log.Count - 30)).Select(l => l.Time + "  " + l.text).DefaultIfEmpty("Nothing yet."))); });
            m.AddItem("STOP every support for me now", "Ends every active intervention for you; the AI holds back for 6 hours.", () => { for (int k = 0; k < 6; k++) if (me.tier[k] > 0) ares.Refuse(me, k); aresShown.Add("stop"); sim.Say("ARIA: Stopped. I will hold back for 6 hours."); m.Rebuild(); });
            m.AddItem("Pause psychological monitoring 72 h" + (me.pauseUntilHour > AR.SimClock.hours ? "   <color=#FFB060>paused until " + AR.SimClock.Format(me.pauseUntilHour) + "</color>" : ""), "No reason needed. Heart-rate safety floor and CO2 stay on (vital signs).", () => { me.pauseUntilHour = me.pauseUntilHour > AR.SimClock.hours ? -1 : AR.SimClock.hours + 72f; ares.Log(1, "privacy", me.pauseUntilHour > 0 ? "Monitoring paused for 72 h." : "Monitoring resumed."); m.Rebuild(); });
            m.AddItem("Tell me about every silent change: " + (me.notifyEverySilentChange ? "ON" : "off"), "Board Q3: for anyone who would rather know every time.", () => { me.notifyEverySilentChange = !me.notifyEverySilentChange; m.Rebuild(); });
            m.AddItem("AI may act inside my pod: " + (me.aiPodAccess ? "invited" : "not invited"), "Your pod is the one place nobody watches. The AI acts there only on invitation.", () => { me.aiPodAccess = !me.aiPodAccess; m.Rebuild(); });
            m.AddItem("Share my mood on my beacon light: " + (me.shareMoodPublic ? "yes" : "no"), "Public lights never show mood unless you choose (hard limit 8).", () => { me.shareMoodPublic = !me.shareMoodPublic; m.Rebuild(); });
            m.AddItem("My sensors: opt out of any psych sensor", "Every psychological sensor can be switched off. Heart rate and CO2 stay on as vital signs.", () => m.OpenExtra(pageAresOptOut));
            m.AddItem("That reading is wrong", "The sensor that pushed hardest counts 20 % less for you from now on, and the current support stops.", () => { aresShown.Add("feedback"); sim.Say("ARIA: " + ares.Feedback(me)); m.Rebuild(); });
            m.AddItem("Quick self-check: I feel good", "Two taps, private to you, counts for 24 h (a fallback signal when a sensor fails, board Q4).", () => { ares.SelfCheck(me, 0.1f, 0.1f); sim.Say("ARIA: Thanks. Noted privately for 24 hours."); m.Rebuild(); });
            m.AddItem("Quick self-check: I feel low and stressed", "", () => { ares.SelfCheck(me, 0.8f, 0.8f); sim.Say("ARIA: Thank you for telling me. Noted privately; it counts for 24 hours."); m.Rebuild(); });
            m.AddItem("Talk to me (AI companion)", "Non-judgemental; listens, offers one option, never diagnoses. Private.", () => { aresShown.Add("companion"); sim.Say("ARIA: " + ares.Companion(me)); });
            m.AddItem("Restore my room", "Every change near you goes back to the design settings.", () => { foreach (var l in aresLights.Values) if (l) Destroy(l.gameObject); aresLights.Clear(); ares.Log(1, "override", "Room restored by the crew member."); sim.Say("ARIA: Your room is back to normal."); });
            m.AddItem("Export my log to a file", "", () => AresSheet("Exported", "Saved: " + AresExportLog()));
            m.AddItem("Delete my AI history", "Everything except Tier-3 safety records (medical record: you and the CMO only).", () => { int n = ares.DeleteMyHistory(me); sim.Say("ARIA: Deleted " + n + " entries."); m.Rebuild(); });
        };
        pageAresOptOut.build = m =>
        {
            var me = ares.C(1);
            for (int s = 0; s < 9; s++)
            {
                int ss = s;
                m.AddItem(AR.SensorSpec.Name[s] + ": " + (!AR.SensorSpec.OptOutAllowed[s] ? "always on (vital sign / shared air)" : me.optOut[s] ? "<color=#FFB060>OPTED OUT</color>" : "on"), AR.SensorSpec.Where[s],
                    () => { if (!AR.SensorSpec.OptOutAllowed[ss]) { sim.Say("ARIA: That one stays on: it is a vital sign, not mood data (board Q5). A permanent opt-out goes through a talk with the CMO."); return; } me.optOut[ss] = !me.optOut[ss]; ares.Log(1, "privacy", AR.SensorSpec.Name[ss] + (me.optOut[ss] ? " opted out." : " back on.")); m.Rebuild(); });
            }
        };
        pageAresCmo.build = m =>
        {
            aresShown.Add("cmo");
            if (ares.escalatedTo.Count == 0) m.AddItem("No Tier-3 case right now. Run board question Q6 to see the chain.", "", () => { });
            foreach (var kv in ares.escalatedTo.ToList())
            {
                int subj = kv.Key;
                m.AddItem("Tier 3: " + ares.C(subj).name + "  → responder " + (kv.Value == 0 ? "Mission Control" : ares.C(kv.Value).name), "The person was told first. Acknowledge to take over the case (limit 15 min acute, 1 h slow-building).", () => { });
                m.AddItem("   Acknowledge as responder", "", () => { ares.Acknowledge(subj); aresShown.Add("ack"); sim.Say("ARIA: Acknowledged. Human chain of care active; environmental support continues."); m.Rebuild(); });
            }
            m.AddItem("CMO on EVA (Okafor): " + (ares.C(2).onEVA ? "yes" : "no"), "When the CMO is unreachable the Commander is the backup, then Mission Control.", () => { ares.C(2).onEVA = !ares.C(2).onEVA; m.Rebuild(); });
        };
        pageAresBoard.build = m =>
        {
            for (int q = 1; q <= 6; q++)
            {
                int qq = q;
                m.AddItem("Q" + q + ": " + AR.Scenarios.Short[q - 1] + (aresShown.Contains("q" + q) ? "   <color=#7CFC9A>[shown]</color>" : ""), "Runs on the real engine, then explains.", () => { AR.Scenarios.Run(qq); aresShown.Add("q" + qq); AresSheet("Board question Q" + qq, AR.Scenarios.lastExplain); });
            }
        };
        pageAresScore.build = m => { foreach (var line in AresScore()) { var l = line; m.AddItem(l.Item1, l.Item2, () => { }); } };

        menu.extraPages.Insert(Mathf.Min(2, menu.extraPages.Count), pageAres);
        var prev = menu.extraShow; menu.extraShow = p => p == pageAres || prev == null || prev(p);
    }


    // ------------------------------------------------------------------ test hook: press every Project ARES button
    public string ProbeAres()
    {
        if (ares == null) return "ARES not built";
        var sb = new StringBuilder(); int pressed = 0, failed = 0;
        var itemsF = typeof(HunchMenu).GetField("items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var pages = new[] { pageAres, pageAresDocs, pageAresDash, pageAresSensors, pageAresCrew, pageAresOptOut, pageAresCmo, pageAresBoard, pageAresScore };
        foreach (var pg in pages)
        {
            menu.OpenExtra(pg);
            var list = (System.Collections.IList)itemsF.GetValue(menu); int n = list.Count;
            sb.Append(pg.title).Append(": ").Append(n).Append(" rows; ");
            for (int i = 0; i < n; i++)
            {
                menu.OpenExtra(pg); list = (System.Collections.IList)itemsF.GetValue(menu); if (i >= list.Count) break;
                var it = list[i]; var lab = (string)it.GetType().GetField("label").GetValue(it); var act = (Action)it.GetType().GetField("act").GetValue(it);
                if (lab == "Back" || act == null) continue;
                try { act(); pressed++; } catch (Exception e) { failed++; sb.Append("\n  FAIL [" + pg.title + "] " + lab + ": " + e.GetType().Name + " " + e.Message); }
                if (readOpen >= 0) { string body = reads[readOpen].body(); if (string.IsNullOrEmpty(body)) sb.Append("\n  EMPTY sheet from " + lab); CloseRead(); }
            }
        }
        menu.Close();
        if (AR.AresHost.AiOff) AresAiOff(false);
        var me0 = ares.C(1); for (int q = 0; q < 9; q++) { me0.optOut[q] = false; foreach (var c0 in ares.crew) c0.sensors[q] = AR.SensorHealth.OK; } me0.pauseUntilHour = -1;
        if (A_ConfigBOk()) sb.Append("\nconfigB pieces ok"); else sb.Append("\nconfigB pieces MISSING");
        sb.Append("\npressed " + pressed + ", failed " + failed + ", docs " + AR.DesignDocs.Pages.Count + ", log " + ares.log.Count + ", blocked " + AR.EthicsGuard.blocked + ", aiOff " + AR.AresHost.AiOff + ", configB " + AR.AresHost.ConfigB);
        foreach (var r in AresScore()) sb.Append("\n  ").Append(System.Text.RegularExpressions.Regex.Replace(r.Item1, "<.*?>", ""));
        return sb.ToString();
    }
    public string ProbeAresOpen() { menu.OpenExtra(pageAres); return "open"; }
    public string ProbeAresCrewOpen() { menu.OpenExtra(pageAresCrew); return "open"; }
    public string ProbeAresDoc() { AresDoc(0); return "doc"; }
    public string ProbeAresRead() { return "readOpen=" + readOpen + (readOpen >= 0 ? " " + reads[readOpen].title : ""); }
    public string ProbeAresClose() { CloseRead(); menu.Close(); return "closed"; }
    bool A_ConfigBOk() { if (!AR.AresHost.ConfigB) SetConfigB(true); return aresConfigB && aresConfigB.childCount >= 4; }
    public string ProbeAresTier() { var c = ares.C(1); c.stress = 0.95f; for (int i = 0; i < 600; i++) ares.Step(1f / 60f); return "tiers " + string.Join(",", c.tier) + " offers " + ares.offers.Count(o => !o.done) + " lights " + aresLights.Count + " last " + AR.DesignDocs.LastAction; }

    string AresExportLog()
    {
        var me = ares.C(1); var sb = new StringBuilder("# My AI log — " + me.name + "\n\n");
        foreach (var l in me.log) sb.AppendLine("- " + l.Time + "  " + l.text);
        string path = System.IO.Path.Combine(AR.AresHost.ReportsDir, "My_AI_Log.md");
        try { System.IO.File.WriteAllText(path, sb.ToString()); } catch (Exception e) { return "could not save: " + e.Message; }
        return path;
    }

    /// The rubric, line by line, scored strictly: a line counts only when the game can SHOW it (or it has been shown).
    List<(string, string)> AresScore()
    {
        var l = new List<(string, string)>(); int got = 0;
        Func<string, int, bool, string, string> Row = (text, pts, ok, where) => { if (ok) got += pts; return (ok ? "<color=#7CFC9A>[" + pts + "/" + pts + "]</color> " : "<color=#FF9A8A>[0/" + pts + "]</color> ") + text; };
        l.Add((Row("Sensor matrix: signal, conditions, thresholds, 2+ failure modes, mass and power (D1)", 20, true, ""), "Deliverables > D1 Sensors (9 sensor pages)."));
        l.Add((Row("False positives and ambiguity handled; sensor failure degrades gracefully", 15, true, ""), "D1 pages + Dashboard > Sensor health > break a sensor. " + (aresShown.Contains("sensorfail") ? "Shown." : "Not shown yet.")));
        l.Add((Row("Six zones with a psychological purpose; sensor placement and coverage", 20, true, ""), "D2 Zones pages."));
        l.Add((Row("Configuration A vs B, both in the world", 15, true, ""), "Config A / B switch puts the screens, second table and alternate route in the base. " + (aresShown.Contains("configB") ? "Shown." : "Not shown yet.")));
        l.Add((Row("Flowcharts: all nine stages, for all six conditions; tiers, consent, monitoring, escalation", 50, true, ""), "D3 pages (12) and the live engine."));
        l.Add((Row("Ethics: four tensions with design answers; hard limits enforced in code; human override", 40, true, ""), "D4 pages, Dashboard > Test a hard limit, AI OFF switch."));
        l.Add((Row("Presentation support: run of show and every board question live", 25, true, ""), "D5 page + Board questions Q1–Q6. Your own delivery is graded by the panel."));
        l.Add((Row("Reflection journals", 15, false, ""), "Prompts are in D6, but the journals must be written by each of you in your own words. The game cannot earn these 15 points."));
        l.Add((Row("Bonus A: 72-hour tabletop with a report", 10, true, ""), "Project ARES > run the tabletop (writes Reports/TabletopSimulation_Report.md)."));
        l.Add((Row("Bonus B: crew interface with privacy, override and opt-out", 10, true, ""), "F10 crew interface."));
        l.Insert(0, ("<b>Score the game can earn: " + got + " / 220</b>  (" + Mathf.RoundToInt(got * 100f / 220f) + " %)", "Strict: the journals (15) cannot come from the game. The brief asks for SIX crew; this base is built for four (the NASA HUNCH sheet says 2–4), so be ready to defend that choice. Physical model and live delivery are graded outside the game."));
        return l;
    }
}
