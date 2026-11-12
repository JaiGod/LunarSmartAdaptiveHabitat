// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/D_ARES_Layer/HunchFrontier27.cs
//  Section : 06_Frontier/D_ARES_Layer
//  Folder  : Project ARES rubric layer: floors fix + ARES pages, easy-mode missions and check-ins,
//            ARES zone lights and sensor clusters in the world.
//  Purpose : PROJECT ARES IN THE WORLD (D2 spatial design) — the six zone types, their lighting and
//            their sensors are visible IN the base, not only in the documents. Nothing is moved:
//            the central dorm and every module stay exactly where they are.
//  Why     : Chat: 'KEEP THE CENTER DORM for the unrealistic game' — nothing is moved, zones are
//            only lit and labelled.
//  Contains: HunchFrontier
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
#pragma warning disable 0414, 0219
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using AR = HunchAres;

/// PROJECT ARES IN THE WORLD (D2 spatial design) — the six zone types, their lighting and their sensors are visible IN
/// the base, not only in the documents. Nothing is moved: the central dorm and every module stay exactly where they are.
///  - every module gets its ARES zone light (the colour temperature the brief asks for: warm communal, cool work …)
///  - a ceiling sensor cluster shows what is installed there: CO2 pair + UWB beacon everywhere, mic array and camera ONLY in
///    shared zones, a door sensor only (no camera, no mic) in private quarters
///  - TRANSITION CORRIDORS: where two modules are joined by an open path, a strip of lights blends from one zone's colour to
///    the other's — the "you have arrived somewhere different" cue
///  - a floating sign over each module names its zone type, light, temperature and sensors (toggle in Project ARES)
public partial class HunchFrontier
{
    Transform aresWorld; bool aresWorldOn = true; int aresTransitions;
    readonly List<Transform> aresSigns = new List<Transform>();
    readonly List<Light> aresZoneLights = new List<Light>(); readonly List<Color> aresZoneCct = new List<Color>(); Material pathLed;

    static float AresCct(string type) => type == "communal" ? 2900f : type == "private" ? 2400f : type == "work" ? 6200f : type == "exercise" ? 5800f : type == "nature" ? 4800f : 4000f;
    static string AresTemp(string type) => type == "communal" ? "22 °C" : type == "private" ? "you choose 18–24 °C" : type == "work" ? "21 °C" : type == "exercise" ? "19 °C" : type == "nature" ? "23 °C · 55 % RH" : "average of both sides";
    static string AresNice(string type) => type == "communal" ? "COMMUNAL" : type == "private" ? "PRIVATE RETREAT" : type == "work" ? "WORK & MISSION" : type == "exercise" ? "EXERCISE BAY" : type == "nature" ? "NATURE & GROWTH" : "TRANSITION";

    void BuildAresWorld()
    {
        if (aresWorld) Destroy(aresWorld.gameObject);
        aresWorld = new GameObject("ARES zones & sensors").transform; aresWorld.SetParent(aresRoot, false); aresSigns.Clear(); aresZoneLights.Clear(); aresZoneCct.Clear(); aresTransitions = 0; pathLed = Glow(new Color(0.25f, 0.55f, 1f));
        Material mCo2 = Glow(new Color(0.3f, 1f, 0.5f)), mMic = Glow(new Color(0.3f, 0.85f, 1f)), mCam = Glow(new Color(1f, 0.3f, 0.3f)), mDoor = Glow(new Color(1f, 0.85f, 0.3f)), mUwb = Glow(new Color(0.95f, 0.95f, 1f)), mBase = Glow(new Color(0.15f, 0.17f, 0.22f));
        foreach (var z in game.zones)
        {
            string type = AresTypeOf(z.id); var g = new GameObject("ARES " + z.id).transform; g.SetParent(aresWorld, false); g.position = z.position;
            // zone light (soft, no shadows — the base's own lights still do the work)
            var l = new GameObject("zone light").AddComponent<Light>(); l.transform.SetParent(g, false); l.transform.localPosition = Vector3.up * 2.3f;
            l.type = LightType.Point; l.range = 6f; l.intensity = 0.55f; l.shadows = LightShadows.None; l.color = AR.AresHost.Cct(AresCct(type)); aresZoneLights.Add(l); aresZoneCct.Add(l.color);
            // ceiling sensor cluster
            var s = new GameObject("sensors").transform; s.SetParent(g, false); s.localPosition = Vector3.up * 2.55f;
            Prim(PrimitiveType.Cylinder, s, Vector3.zero, new Vector3(0.34f, 0.015f, 0.34f), mBase);
            Prim(PrimitiveType.Cube, s, new Vector3(-0.1f, -0.03f, 0f), new Vector3(0.05f, 0.04f, 0.05f), mCo2);
            Prim(PrimitiveType.Cube, s, new Vector3(-0.1f, -0.03f, 0.08f), new Vector3(0.05f, 0.04f, 0.05f), mCo2);      // CO2 sensors come in redundant pairs
            Prim(PrimitiveType.Sphere, s, new Vector3(0.1f, -0.03f, 0f), Vector3.one * 0.05f, mUwb);
            bool shared = type != "private";
            if (shared) { Prim(PrimitiveType.Cylinder, s, new Vector3(0f, -0.03f, -0.1f), new Vector3(0.08f, 0.02f, 0.08f), mMic); Prim(PrimitiveType.Sphere, s, new Vector3(0f, -0.05f, 0.1f), Vector3.one * 0.06f, mCam); }
            else Prim(PrimitiveType.Cube, s, new Vector3(0f, -0.03f, 0.1f), new Vector3(0.08f, 0.03f, 0.03f), mDoor);
            // sign
            var sign = new GameObject("sign").transform; sign.SetParent(g, false); sign.localPosition = Vector3.up * 3.0f;
            string sensors = shared ? "CO2 ×2 · UWB · mic array · camera (shared zone)" : "CO2 ×2 · UWB · door sensor only — NO camera, NO mic";
            var t = Label(sign, "<b>" + AresNice(type) + "</b>", 0f, 0f, 0.9f, 4.2f, 0.5f, Color.Lerp(AR.AresHost.Cct(AresCct(type)), Color.white, 0.25f));
            t.outlineWidth = 0.2f; t.outlineColor = new Color32(0, 0, 0, 200);
            aresSigns.Add(sign);
        }
        transitionsBuilt = false;      // corridors are laid along the walking paths once the walk mesh is ready (UpdateAresWorld)
        aresWorld.gameObject.SetActive(aresWorldOn);
        HLog.I("ARES", "ARES in the world: " + game.zones.Count + " zones dressed, " + aresTransitions + " transition corridors");
    }

    bool transitionsBuilt;
    /// TRANSITION CORRIDORS along the real walking routes between neighbouring modules: floor light strip + soft lights whose
    /// colour temperature blends from one zone to the next
    void BuildTransitions()
    {
        transitionsBuilt = true; var hp = HunchPath.I; if (hp == null || !hp.Ready) { transitionsBuilt = false; return; }
        var zs = game.zones.ToList(); var done = new HashSet<string>(); var corners = new List<Vector3>();
        var trRoot = new GameObject("transition corridors").transform; trRoot.SetParent(aresWorld, false);
        for (int i = 0; i < zs.Count; i++)
        {
            string ta = AresTypeOf(zs[i].id);
            var near = zs.Where(z => z != zs[i] && AresTypeOf(z.id) != ta).OrderBy(z => (z.position - zs[i].position).sqrMagnitude).Take(2);
            foreach (var zb in near)
            {
                string key = string.CompareOrdinal(zs[i].id, zb.id) < 0 ? zs[i].id + "|" + zb.id : zb.id + "|" + zs[i].id; if (!done.Add(key)) continue;
                corners.Clear(); float len; bool complete;
                if (!hp.Find(zs[i].position, zb.position, corners, out len, out complete) || !complete || len > 40f || corners.Count < 2) continue;
                string tb = AresTypeOf(zb.id); var tr = new GameObject("transition " + key).transform; tr.SetParent(trRoot, false); aresTransitions++;
                // walk the path, a floor dot every 1.2 m, a soft light every ~4 m, colour blending A → B
                float walked = 0f, nextDot = 1.2f, nextLight = 2.5f;
                for (int c = 1; c < corners.Count; c++)
                {
                    Vector3 p0 = corners[c - 1], p1 = corners[c]; float seg = Vector3.Distance(p0, p1);
                    while (nextDot <= walked + seg && nextDot < len - 1f)
                    {
                        float f = nextDot / len; Vector3 p = Vector3.Lerp(p0, p1, (nextDot - walked) / Mathf.Max(seg, 0.001f));
                        Color col = Color.Lerp(AR.AresHost.Cct(AresCct(ta)), AR.AresHost.Cct(AresCct(tb)), f);
                        var dot = Prim(PrimitiveType.Cube, tr, Vector3.zero, new Vector3(0.05f, 0.012f, 0.9f), pathLed); dot.transform.position = p + Vector3.up * 0.03f;
                        var dir = p1 - p0; dir.y = 0f; if (dir.sqrMagnitude > 0.001f) dot.transform.rotation = Quaternion.LookRotation(dir);
                        if (nextDot >= nextLight) { nextLight += 4f; var tl = new GameObject("blend light").AddComponent<Light>(); tl.transform.SetParent(tr, false); tl.transform.position = p + Vector3.up * 2.1f; tl.type = LightType.Point; tl.range = 3.2f; tl.intensity = 0.35f; tl.shadows = LightShadows.None; tl.color = col; }
                        nextDot += 1.2f;
                    }
                    walked += seg;
                }
            }
        }
        HLog.I("ARES", "ARES transition corridors: " + aresTransitions);
    }

    void UpdateAresWorld()
    {
        if (!transitionsBuilt && aresWorld && Time.frameCount % 30 == 0) try { BuildTransitions(); } catch (Exception e) { transitionsBuilt = true; HLog.W("ARES", "transitions: " + e.Message); }
        if (!aresWorld || !aresWorld.gameObject.activeSelf) return;
        var cam = Camera.main; if (!cam) return;
        foreach (var s in aresSigns) if (s) { var d = s.position - cam.transform.position; d.y = 0f; if (d.sqrMagnitude > 0.01f) s.rotation = Quaternion.LookRotation(d); s.gameObject.SetActive(d.sqrMagnitude < 9f * 9f); }   // a sign only when you are near it
        // circadian zone lighting (same schedule as the realistic sim) + emergency red pulse; pathway LEDs blue → red in an alarm
        bool alarm = sim.stormActive || sim.leak || sim.alerts.Any(a => a.type == "emergency" || a.type == "fire" || a.type == "breach");
        float h = sim.Hour, pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 3.2f)); Color cc; float lvl;
        if (h >= 6f && h < 9f) { cc = Color.Lerp(new Color(1f, 0.62f, 0.38f), new Color(1f, 0.9f, 0.8f), (h - 6f) / 3f); lvl = 0.85f; }
        else if (h >= 9f && h < 18f) { cc = new Color(1f, 0.98f, 0.95f); lvl = 1f; }
        else if (h >= 18f && h < 22f) { cc = new Color(1f, 0.72f, 0.48f); lvl = 0.6f; }
        else { cc = new Color(0.35f, 0.45f, 0.9f); lvl = 0.3f; }
        for (int i = 0; i < aresZoneLights.Count; i++) { var zl = aresZoneLights[i]; if (!zl) continue; var want = alarm ? new Color(1f, 0.12f, 0.1f) : Color.Lerp(aresZoneCct[i], cc, 0.5f); zl.color = Color.Lerp(zl.color, want, Time.deltaTime * 2f); zl.intensity = alarm ? 0.9f * pulse : Mathf.Lerp(zl.intensity, 0.55f * lvl, Time.deltaTime); }
        if (pathLed) { var pc = alarm ? new Color(1f, 0.12f, 0.1f) * pulse : new Color(0.25f, 0.55f, 1f) * (lvl < 0.5f ? 0.55f : 0.85f); pathLed.color = pc; if (pathLed.HasProperty("_BaseColor")) pathLed.SetColor("_BaseColor", pc); }
    }

    void AddAresWorldMenu()
    {
        var prev = pageAres.build;
        pageAres.build = mm =>
        {
            prev(mm);
            mm.AddItem("Zones & sensors in the base: " + (aresWorldOn ? "<color=#7CFC9A>ON</color>" : "OFF"), "Zone lights, ceiling sensor clusters, transition-corridor light strips and a sign over every module (" + aresTransitions + " transition corridors).", () => { aresWorldOn = !aresWorldOn; if (aresWorld) aresWorld.gameObject.SetActive(aresWorldOn); aresShown.Add("zonesworld"); mm.Rebuild(); });
        };
    }

    public string ProbeZones() => string.Join("\n", game.zones.Select(z => z.id + " | " + z.title + " | " + AresTypeOf(z.id) + " | " + z.position.ToString("F1"))) + "\ntransitions=" + aresTransitions;
    public string ProbeGoZone(string id) { int k = game.zones.FindIndex(z => z.id == id); if (k >= 0) game.GoTo(k); return "go " + id + " " + k; }
}

public partial class HunchFrontier
{
    /// evidence capture (HunchBridge): set HunchFrontier.probeArg "<what>", then call HunchFrontier.ProbeEvidence
    public string probeArg = "";
    public string ProbeEvidence()
    {
        string[] a = (probeArg ?? "").Split(' '); string rest = a.Length > 1 ? string.Join(" ", a.Skip(1)) : "";
        try { CloseRead(); } catch { }
        try { menu.Close(); } catch { }
        switch (a[0])
        {
            case "check": CheckOn(int.Parse(rest)); break;
            case "ares": menu.OpenExtra(pageAres); break;
            case "dash": menu.OpenExtra(pageAresDash); aresShown.Add("dash"); break;
            case "sensors": menu.OpenExtra(pageAresSensors); break;
            case "crew": menu.OpenExtra(pageAresCrew); break;
            case "optout": menu.OpenExtra(pageAresOptOut); break;
            case "cmo": menu.OpenExtra(pageAresCmo); break;
            case "board": menu.OpenExtra(pageAresBoard); break;
            case "score": menu.OpenExtra(pageAresScore); break;
            case "missions": menu.OpenExtra(pageAresMissions); break;
            case "checkpage": menu.OpenExtra(pageAresCheck); break;
            case "doc": { int i = AR.DesignDocs.Pages.FindIndex(p => p.title.IndexOf(rest, StringComparison.OrdinalIgnoreCase) >= 0); AresDoc(i); return "doc " + rest + " -> " + i; }
            case "configB": SetConfigB(true); break;
            case "configA": SetConfigB(false); break;
            case "aioff": AresAiOff(true); break;
            case "aion": AresAiOff(false); break;
            case "offer": OfferMe(); menu.OpenExtra(pageAresCrew); break;
            case "q": { int q = int.Parse(rest); AR.Scenarios.Run(q); aresShown.Add("q" + q); AresSheet("Board question Q" + q, AR.Scenarios.lastExplain); break; }
            case "tabletop": AR.TabletopSim.Run(); aresShown.Add("tabletop"); AresSheet("72-hour tabletop simulation", AR.TabletopSim.lastReport); break;
            case "export": AresSheet("Your data, exported", "Saved: " + AresExportLog()); break;
            case "sensorfail": { var c = ares.C(3); c.sensors[0] = AR.SensorHealth.Failed; c.sensors[3] = AR.SensorHealth.Degraded; ares.Log(3, "sensor", "Tanaka: heart-rate sensor FAILED, voice DEGRADED — weights renormalised, confidence lowered."); aresShown.Add("sensorfail"); menu.OpenExtra(pageAresSensors); break; }
            case "tier": return ProbeAresTier();
            case "night": nightTest = true; if (sleepZone >= 0) game.GoTo(sleepZone); break;
            case "sleep": SleepMe(); break;
            case "zone": { int k = game.zones.FindIndex(z => z.id == rest); if (k >= 0) game.GoTo(k); break; }
        }
        return "show " + probeArg;
    }
}

public partial class HunchFrontier
{
    Renderer readImgQuad; Material readImgMat; string readImgName;
    /// diagram pages (drawn flowcharts, dimensioned floor plan) show a picture on the reading sheet; the text becomes a caption
    void ReadImg(string name)
    {
        Texture2D tex = string.IsNullOrEmpty(name) ? null : Resources.Load<Texture2D>("ARES/" + name);
        if (tex && !readImgQuad)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>()); q.name = "Reading sheet picture";
            q.transform.SetParent(readSheet, false); readImgQuad = q.GetComponent<Renderer>();
            var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            readImgMat = sh ? new Material(sh) : new Material(solidSrc); readImgQuad.sharedMaterial = readImgMat;
            readImgQuad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; readImgQuad.receiveShadows = false;
        }
        if (readImgQuad) readImgQuad.gameObject.SetActive(tex != null);
        if (!tex) return;
        readImgName = name;
        readImgMat.mainTexture = tex; if (readImgMat.HasProperty("_BaseMap")) readImgMat.SetTexture("_BaseMap", tex);
        var white = Color.white; readImgMat.color = white; if (readImgMat.HasProperty("_BaseColor")) readImgMat.SetColor("_BaseColor", white);
        if (readImgMat.HasProperty("_EmissionColor")) readImgMat.SetColor("_EmissionColor", Color.black);
        float maxW = 0.6f, maxH = 0.64f, k = Mathf.Min(maxW / tex.width, maxH / tex.height);
        readImgQuad.transform.localPosition = new Vector3(0f, -0.06f, -0.003f); readImgQuad.transform.localRotation = Quaternion.identity;
        readImgQuad.transform.localScale = new Vector3(tex.width * k, tex.height * k, 1f);
        // caption stays readable above the picture
        readText.text = "<size=60%>" + readText.text + "</size>";
    }

    /// D2: export the real room geometry so the dimensioned floor plan is drawn to scale (Reports/layout_sci.json)
    public string ProbeLayout()
    {
        var sb = new System.Text.StringBuilder("{\"zones\":[");
        sb.Append(string.Join(",", game.zones.Select(z => "{\"id\":\"" + z.id + "\",\"title\":\"" + z.title.Replace("\"", "'") + "\",\"type\":\"" + AresTypeOf(z.id) + "\",\"x\":" + z.position.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ",\"y\":" + z.position.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ",\"z\":" + z.position.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "}")));
        sb.Append("],\"objects\":[");
        var ci = System.Globalization.CultureInfo.InvariantCulture; bool first = true; int n = 0;
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue; var b = r.bounds;
            if (b.size.x < 1.2f && b.size.z < 1.2f) continue; if (b.size.magnitude > 400f) continue;
            if (!first) sb.Append(","); first = false; n++;
            sb.Append("{\"n\":\"" + r.name.Replace("\"", "'").Replace("\\", "/") + "\",\"p\":\"" + (r.transform.parent ? r.transform.parent.name.Replace("\"", "'").Replace("\\", "/") : "") + "\",\"cx\":" + b.center.x.ToString("F2", ci) + ",\"cy\":" + b.center.y.ToString("F2", ci) + ",\"cz\":" + b.center.z.ToString("F2", ci) + ",\"sx\":" + b.size.x.ToString("F2", ci) + ",\"sy\":" + b.size.y.ToString("F2", ci) + ",\"sz\":" + b.size.z.ToString("F2", ci) + ",\"col\":" + (r.GetComponent<Collider>() ? 1 : 0) + "}");
        }
        sb.Append("]}");
        string dir = System.IO.Path.Combine(Application.dataPath, "..", "Reports"); System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "layout_sci.json"), sb.ToString());
        return "layout objects " + n;
    }
}
