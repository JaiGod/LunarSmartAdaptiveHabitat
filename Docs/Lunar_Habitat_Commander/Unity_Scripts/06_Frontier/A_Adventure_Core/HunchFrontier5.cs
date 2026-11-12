// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/A_Adventure_Core/HunchFrontier5.cs
//  Section : 06_Frontier/A_Adventure_Core
//  Folder  : Moon Frontier adventure layer: mining, aliens, tools, easy mode, survival + repair
//            mini-games, Autopilot, the 3D models from the Drive collection.
//  Purpose : Fifth part of Moon Frontier: ten 3D models from Jaineil's NASA HUNCH model collection
//            (Google Drive, "BEST MODELS"). They were converted from .glb to .obj and live in
//            Assets/HUNCH/Resources/DriveModels (mesh + one colour texture each).
//  Contains: HunchFrontier, Flyer
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Fifth part of Moon Frontier: ten 3D models from Jaineil's NASA HUNCH model collection (Google Drive, "BEST MODELS").
/// They were converted from .glb to .obj and live in Assets/HUNCH/Resources/DriveModels (mesh + one colour texture each).
///   in the sky:    Red Ship, Futuristic Plane and Blue Supersonic Jet circle the base; UFO Stars hovers high up;
///                  UFO Neon is the aliens' UFO; Orbital Satellite is the relay satellite
///   on the ground: the rocket on its pad, the Moon outpost, the telescope; the Mars rover is the body of the two rovers
public partial class HunchFrontier
{
    class Flyer { public Transform t; public Vector3 centre; public float radius, height, speed, ang, bank, spin; }
    readonly List<Flyer> flyers = new List<Flyer>(); readonly List<Transform> driveProps = new List<Transform>(); readonly List<Vector3> usedSpots = new List<Vector3>();
    readonly List<TextMeshPro> driveLabels = new List<TextMeshPro>(); int driveCount; string driveLog = "";

    /// one model from Resources/DriveModels; unit-sized in the file, so "size" is its longest side in metres
    Transform DriveModel(string name, Transform parent, float size, bool selfLit)
    {
        var src = Resources.Load<GameObject>("DriveModels/" + name); var tex = Resources.Load<Texture2D>("DriveModels/" + name + "_tex");
        var mf = src ? src.GetComponentInChildren<MeshFilter>() : null;
        if (!mf || !mf.sharedMesh) { driveLog += " missing:" + name; return null; }
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localScale = Vector3.one * size;
        go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh; var r = go.AddComponent<MeshRenderer>();
        var m = new Material(selfLit ? solidSrc : litSrc);
        if (tex) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", selfLit ? new Color(1.15f, 1.15f, 1.15f, 1f) : Color.white);
        if (!selfLit) { if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f); if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f); }
        r.sharedMaterial = m; r.shadowCastingMode = selfLit ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        driveCount++; driveLog += " " + name + (tex ? "" : "(no texture)");
        return go.transform;
    }

    bool FreeSpot(float[] dists, float startDeg, float clear, out Vector3 at)
    {
        at = Vector3.zero; Vector3 g;
        if (!fieldOk) return false;
        foreach (float dist in dists)
            for (int k = 0; k < 12; k++)
            {
                float a = (startDeg + k * 30f) * Mathf.Deg2Rad, x = fieldC.x + Mathf.Sin(a) * dist, z = fieldC.z + Mathf.Cos(a) * dist;
                if (!OpenAt(x, z, clear, out g) || !OpenAt(x, z, clear * 0.5f, out g)) continue;
                if (Physics.CheckSphere(g + Vector3.up * (clear + 1f), clear, ~0, QueryTriggerInteraction.Ignore)) continue;
                // it must be on the mining-field side of the tunnels, in plain view from the field
                RaycastHit hit; Vector3 eye = fieldC + Vector3.up * 2.5f, to = g + Vector3.up * 2.5f - eye;
                if (Cast(new Ray(eye, to.normalized), out hit, to.magnitude)) continue;
                bool near = false; foreach (var u in usedSpots) if ((u - g).sqrMagnitude < 16f * 16f) { near = true; break; }
                if (near) continue;
                at = g; usedSpots.Add(g); return true;
            }
        return false;
    }

    void Plate(Transform parent, string title, float y)
    {
        var lab = Label(parent, "<b>" + title + "</b>\n<size=60%>3D model by Jaineil R.</size>", 0f, y, 4f, 16f, 2.2f, new Color(1f, 0.84f, 0.3f)); driveLabels.Add(lab);
    }

    void AddFlyer(string name, float size, Vector3 centre, float radius, float height, float speed, float startDeg, float bank, float spin, Color engine)
    {
        var holder = new GameObject("Flying: " + name).transform; holder.SetParent(root, false);
        var m = DriveModel(name, holder, size, true); if (!m) { Destroy(holder.gameObject); return; }
        m.localPosition = new Vector3(0f, -0.1f * size, 0f);
        if (engine.a > 0f) Prim(PrimitiveType.Sphere, m, new Vector3(0f, 0.1f, -0.5f), Vector3.one * 0.07f, Glow(engine));
        flyers.Add(new Flyer { t = holder, centre = centre, radius = radius, height = height, speed = speed, ang = startDeg * Mathf.Deg2Rad, bank = bank, spin = spin });
    }

    void BuildDriveModels()
    {
        if (rectenna) usedSpots.Add(rectenna.position);
        Vector3 hub = fieldOk ? fieldC : antenna; hub.y = fieldOk ? fieldC.y : 0f; Vector3 g;

        // ---- in the sky
        AddFlyer("RedShip", 16f, hub, 95f, 42f, 20f, 0f, 22f, 0f, new Color(2.6f, 1.2f, 0.3f, 1f));
        AddFlyer("FuturisticPlane", 14f, hub, 150f, 64f, -26f, 120f, 18f, 0f, new Color(0.5f, 1.4f, 2.6f, 1f));
        AddFlyer("BlueSupersonicJet", 14f, hub, 210f, 90f, 36f, 240f, 26f, 0f, new Color(0.6f, 1.6f, 2.6f, 1f));
        AddFlyer("UfoStars", 20f, hub, 60f, 110f, 3.5f, 60f, 0f, 14f, new Color(0f, 0f, 0f, 0f));

        // the aliens' UFO and the relay satellite get the real models
        if (ufo)
        {
            var m = DriveModel("UfoNeon", ufo, 11f, true);
            if (m) { m.localPosition = new Vector3(0f, -2.2f, 0f); foreach (var r in ufo.GetComponentsInChildren<MeshRenderer>()) if (r.transform != m) r.enabled = false; }
        }
        if (comSat)
        {
            var m = DriveModel("OrbitalSatellite", comSat, 22f, true);
            if (m) { m.localPosition = new Vector3(0f, -3f, 0f); foreach (var r in comSat.GetComponentsInChildren<MeshRenderer>()) if (r.transform != m) r.enabled = false; }
        }
        // the two rovers become Mars rovers (they still drive, and you can still ride them)
        foreach (var rv in rovers)
        {
            var m = DriveModel("MarsRover", rv.t, 3.4f, true);
            if (m) { foreach (var r in rv.t.GetComponentsInChildren<MeshRenderer>()) if (r.transform != m) r.enabled = false; }
        }

        // ---- on the ground, on open ground round the mining field
        if (FreeSpot(new[] { 34f, 42f, 26f }, 200f, 8f, out g))
        {
            var p = new GameObject("Rocket on its pad").transform; p.SetParent(root, false); p.position = g; driveProps.Add(p);
            Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 0.2f, 0f), new Vector3(9f, 0.2f, 9f), Lit(new Color(0.3f, 0.31f, 0.34f)));
            Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 0.42f, 0f), new Vector3(9.4f, 0.03f, 9.4f), Glow(new Color(1.6f, 1.1f, 0.25f)));
            var m = DriveModel("MissileLaunch", p, 16f, true); if (m) m.localPosition = new Vector3(0f, 0.4f, 0f);
            Plate(p, "ROCKET", 18.5f);
        }
        if (FreeSpot(new[] { 38f, 30f, 46f }, 80f, 9f, out g))
        {
            var p = new GameObject("Moon outpost").transform; p.SetParent(root, false); p.position = g; driveProps.Add(p);
            Vector3 toField = fieldC - g; toField.y = 0f; p.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, toField.normalized), Vector3.up);
            var m = DriveModel("MoonBase", p, 15f, true); if (m) m.localPosition = Vector3.zero;
            Plate(p, "MOON OUTPOST", 6.2f);
        }
        if (FreeSpot(new[] { 24f, 30f, 36f }, 320f, 4f, out g))
        {
            var p = new GameObject("Telescope").transform; p.SetParent(root, false); p.position = g; driveProps.Add(p);
            Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 0.1f, 0f), new Vector3(3.2f, 0.1f, 3.2f), Lit(new Color(0.3f, 0.31f, 0.34f)));
            var m = DriveModel("Telescope", p, 3.2f, true); if (m) m.localPosition = new Vector3(0f, 0.2f, 0f);
            Plate(p, "TELESCOPE", 4.3f);
        }
        A("Ship spotter", "Go outside and look up at the ships circling the base.", () => flyers.Count > 0 && Playing && fx && fx.Outside && cam.forward.y > 0.25f);
        HLog.I("Adventure", "Drive models placed: " + driveCount + driveLog);
    }

    void UpdateDriveModels(float dt)
    {
        foreach (var f in flyers)
        {
            f.ang += f.speed / f.radius * dt;
            Vector3 p = f.centre + new Vector3(Mathf.Sin(f.ang), 0f, Mathf.Cos(f.ang)) * f.radius + Vector3.up * (f.height + Mathf.Sin(Time.time * 0.4f + f.radius) * 2f);
            Vector3 fwd = new Vector3(Mathf.Cos(f.ang), 0f, -Mathf.Sin(f.ang)) * Mathf.Sign(f.speed);
            f.t.position = p;
            f.t.rotation = f.spin != 0f ? Quaternion.Euler(0f, Time.time * f.spin, 0f) : Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, 0f, -f.bank * Mathf.Sign(f.speed));
        }
        foreach (var l in driveLabels) if (l) l.transform.rotation = Quaternion.LookRotation(l.transform.position - cam.position, Vector3.up);
    }

    public string ProbeDrive() { var sb = new System.Text.StringBuilder("drive models=" + driveCount + driveLog + " flyers=" + flyers.Count + " props="); foreach (var p in driveProps) sb.Append(p.name).Append("@").Append(p.position.ToString("F0")).Append(" "); return sb.ToString(); }
    int probeProp;
    public string ProbeProp()
    {
        if (driveProps.Count == 0) return "no props";
        var p = driveProps[probeProp % driveProps.Count]; probeProp++; float back = p.name.StartsWith("Tel") ? 7f : 26f;
        Vector3 d = (fieldC - p.position); d.y = 0f; d.Normalize(); Vector3 at = p.position + d * back, g; if (GroundAt(at.x, at.z, out g)) at = g;
        TeleportTo(at, 0f); cam.position = origin.transform.position + Vector3.up * 1.4f; Face(p.position + Vector3.up * (p.name.StartsWith("Rocket") ? 8f : 2.5f));
        return p.name + " at " + p.position.ToString("F0");
    }
    int probeFly;
    public string ProbeFlyer() { if (flyers.Count == 0) return "no flyers"; var f = flyers[probeFly % flyers.Count]; probeFly++; Face(f.t.position); return f.t.name + " dist " + Vector3.Distance(cam.position, f.t.position).ToString("F0"); }
}
