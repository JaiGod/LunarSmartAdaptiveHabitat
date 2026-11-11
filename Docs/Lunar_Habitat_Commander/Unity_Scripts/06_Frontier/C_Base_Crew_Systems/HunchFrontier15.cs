// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/C_Base_Crew_Systems/HunchFrontier15.cs
//  Section : 06_Frontier/C_Base_Crew_Systems
//  Folder  : Systems inside the central base: pick-ups + battery bank, NASA HUNCH mode, physics
//            view, Autopilot Smooth, crew of four + sleep pods, The Show, self-working crew,
//            airlocks/lifts, crew self-care.
//  Purpose : Fifteenth part of Moon Frontier: PHYSICS VIEW (Menu > Base systems > Physics view, or
//            F8). Off when the game starts. It draws the physics and maths of what is happening, as
//            pictures rather than formulas:
//  Contains: HunchFrontier
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// Fifteenth part of Moon Frontier: PHYSICS VIEW (Menu > Base systems > Physics view, or F8). Off when the game starts.
/// It draws the physics and maths of what is happening, as pictures rather than formulas:
///   - on you: your velocity vector, your weight, the floor's push back, and the path a jump will follow (projectile motion)
///   - around you: the Moon's gravity as a vector field (a grid of small arrows, every one the same: a uniform field)
///   - a graph board that follows you: height, speed and acceleration against time (speed is the slope of position,
///     acceleration is the slope of speed: calculus you can watch), with kinetic and potential energy
///   - the habitat's sensor network: a line from every zone to Mission Control with data packets travelling along it
///   - a sheet that says where calculus 1, 2 and 3, differential equations, linear algebra, abstract algebra and
///     computer networking show up in the base
public partial class HunchFrontier
{
    public bool physOn; Transform physRoot, physBoard; LineRenderer physV, physG, physN, physPath; readonly List<LineRenderer> physField = new List<LineRenderer>(), physNet = new List<LineRenderer>(); readonly List<Transform> physPackets = new List<Transform>();
    TextMeshPro physVL, physGL, physNL, physInfo, physNetL; Vector3 physLast, physVel; float physSpeedWas, physAcc, physTick; Texture2D physTex; Color32[] physPix; readonly float[] gH = new float[160], gV = new float[160], gA = new float[160]; int gN, physSheet = -1; bool physStarted;
    const float MoonG = 1.62f, PhysMass = 100f; const int GW = 320, GH = 180;

    const string PhysText = "MATHS AND PHYSICS YOU CAN SEE (Physics view)\n\nMECHANICS\nGreen arrow: your velocity, a vector (it has a size and a direction). Red arrow: your weight, W = m g, with g = 1.62 m/s^2 on the Moon. Blue arrow: the floor pushing back. Jump, and the blue arrow goes: only gravity acts, so you follow the dotted parabola.\n\nVECTOR FIELDS (calculus 3)\nThe grid of small arrows is the gravity field: one vector at every point in space. Here every arrow is the same, a uniform field. Electric and magnetic fields are drawn the same way in the Force Lab.\n\nCALCULUS 1 AND 2 ON THE GRAPH BOARD\nSpeed is the slope (derivative) of position. Acceleration is the slope of speed. Going the other way, distance is the area (integral) under the speed curve, and the energy the base has collected is the area under its power curve.\n\nDIFFERENTIAL EQUATIONS\nThe battery, the air and the crew's stress all change at a rate that depends on how much there is now: dx/dt = -k x gives the smooth curves you see on the health wall when a crew member calms down.\n\nLINEAR ALGEBRA\nEvery turn of your head is a rotation matrix multiplying a vector. A solar panel's power is a dot product: P = P0 (n . s), the panel's normal with the direction of the Sun.\n\nABSTRACT ALGEBRA\nThe habitat's clock counts in a cycle: hours add modulo 24, a group. The turns that leave a six-sided module looking the same form a group too (six rotations).\n\nCOMPUTER NETWORKING\nThe gold lines are the sensor network: every zone sends packets to Mission Control (a star network; if one link fails the others carry on). A signal to Earth takes about 1.3 seconds each way at the speed of light, so the base's AI has to decide by itself.";

    void BuildPhysics()
    {
        physSheet = reads.Count; reads.Add(new ReadItem { title = "Maths and physics you can see", zone = "Physics view", got = true, body = () => PhysText });
        physRoot = new GameObject("Physics view").transform; physRoot.SetParent(root, false);
        var green = Glow(new Color(0.4f, 2.2f, 0.6f)); var red = Glow(new Color(2.4f, 0.45f, 0.35f)); var blue = Glow(new Color(0.45f, 1.0f, 2.6f)); var cyan = Glow(new Color(0.3f, 0.9f, 1.3f)); var gold = Glow(new Color(2.2f, 1.6f, 0.3f));
        physV = Line("velocity", green, 0.05f, physRoot); physG = Line("weight", red, 0.05f, physRoot); physN = Line("normal force", blue, 0.05f, physRoot); physPath = Line("jump path", gold, 0.03f, physRoot); physPath.positionCount = 24;
        foreach (var l in new[] { physV, physG, physN }) l.positionCount = 5;
        physVL = PhysLabel("", new Color(0.5f, 1f, 0.6f)); physGL = PhysLabel("", new Color(1f, 0.55f, 0.45f)); physNL = PhysLabel("", new Color(0.55f, 0.8f, 1f));
        for (int i = 0; i < 25; i++) { var l = Line("field", cyan, 0.02f, physRoot); l.positionCount = 5; physField.Add(l); }
        // the sensor network: every zone to Mission Control
        int hub = ZoneIndex("control", "telecom"); if (hub < 0) hub = ZoneIndex("work"); if (hub < 0) hub = 0;
        for (int i = 0; i < game.zones.Count; i++)
        {
            if (i == hub) continue; var l = Line("network link", gold, 0.02f, physRoot); l.positionCount = 3; Vector3 a = game.zones[i].position + Vector3.up * 2.5f, b = game.zones[hub].position + Vector3.up * 2.5f;
            l.SetPosition(0, a); l.SetPosition(1, (a + b) * 0.5f + Vector3.up * 1.2f); l.SetPosition(2, b); physNet.Add(l);
            physPackets.Add(Prim(PrimitiveType.Sphere, physRoot, a, Vector3.one * 0.14f, green).transform);
        }
        physNetL = PhysLabel("<b>SENSOR NETWORK</b>\n<size=70%>" + (game.zones.Count - 1) + " links to Mission Control. Earth link: 1.3 s each way.</size>", new Color(1f, 0.84f, 0.3f)); physNetL.transform.position = game.zones[hub].position + Vector3.up * 3.6f; physNetL.fontSize = 2.2f;
        // the graph board
        physTex = new Texture2D(GW, GH, TextureFormat.RGBA32, false); physTex.wrapMode = TextureWrapMode.Clamp; physPix = new Color32[GW * GH];
        var bm = new Material(solidSrc); if (bm.HasProperty("_BaseMap")) bm.SetTexture("_BaseMap", physTex); bm.mainTexture = physTex; bm.SetColor("_BaseColor", Color.white);
        physBoard = new GameObject("Graph board").transform; physBoard.SetParent(physRoot, false);
        Prim(PrimitiveType.Quad, physBoard, Vector3.zero, new Vector3(0.8f, 0.45f, 1f), bm); Prim(PrimitiveType.Cube, physBoard, new Vector3(0f, 0f, 0.012f), new Vector3(0.84f, 0.49f, 0.01f), Lit(new Color(0.95f, 0.74f, 0.22f)));
        physInfo = Label(physBoard, "", 0f, 0f, 0.42f, 0.84f, 0.2f, Color.white); physInfo.transform.localPosition = new Vector3(0f, 0.33f, 0f); physInfo.alignment = TextAlignmentOptions.Bottom;
        var key = Label(physBoard, "<color=#6CB8FF>height</color>   <color=#7CFC9A>speed = slope of position</color>   <color=#FF8A7A>acceleration = slope of speed</color>", 0f, 0f, 0.3f, 0.84f, 0.08f, Color.white); key.transform.localPosition = new Vector3(0f, -0.27f, 0f);
        physRoot.gameObject.SetActive(false);
        A("Physicist", "Switch on the Physics view and jump, to watch your own projectile path.", () => physOn && physStarted && physAirTime > 0.6f);
    }
    float physAirTime;
    TextMeshPro PhysLabel(string text, Color c) { var l = Label(physRoot, text, 0f, 0f, 1.1f, 6f, 1f, c); mapLabels.Add(l); return l; }
    public void PhysToggle() { physOn = !physOn; Note(physOn ? "Physics view ON: vectors, the gravity field, a graph board and the sensor network." : "Physics view off."); if (physOn) Play(cPick, 0.6f); }

    static void ArrowLine(LineRenderer l, Vector3 a, Vector3 b, Vector3 viewDir)
    {
        Vector3 d = b - a; float len = d.magnitude; if (len < 0.02f) { for (int i = 0; i < 5; i++) l.SetPosition(i, a); return; }
        d /= len; Vector3 side = Vector3.Cross(d, viewDir).normalized; if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(d, Vector3.up).normalized; float h = Mathf.Min(0.18f, len * 0.4f);
        l.SetPosition(0, a); l.SetPosition(1, b); l.SetPosition(2, b - d * h + side * h * 0.6f); l.SetPosition(3, b); l.SetPosition(4, b - d * h - side * h * 0.6f);
    }

    void UpdatePhysics(float dt)
    {
        var kb = Keyboard.current; if (kb != null && kb.f8Key.wasPressedThisFrame && !game.HeadsetActive && !game.menuOpen) PhysToggle();
        bool on = physOn && Playing && physRoot; if (physRoot && physRoot.gameObject.activeSelf != on) { physRoot.gameObject.SetActive(on); physStarted = false; }
        if (!on || dt <= 0f) return;
        Vector3 feet = Feet(), fwd = cam.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward; fwd.Normalize(); Vector3 right = Vector3.Cross(Vector3.up, fwd);
        if (!physStarted) { physStarted = true; physLast = feet; physVel = Vector3.zero; physSpeedWas = 0f; gN = 0; }
        Vector3 v = (feet - physLast) / dt; physLast = feet; if (v.sqrMagnitude > 60f * 60f) v = Vector3.zero;      // a teleport is not a velocity
        physVel = Vector3.Lerp(physVel, v, 1f - Mathf.Exp(-dt * 10f)); float speed = physVel.magnitude; physAcc = Mathf.Lerp(physAcc, (speed - physSpeedWas) / dt, 1f - Mathf.Exp(-dt * 6f)); physSpeedWas = speed;
        bool air = !Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, 0.55f, ~0, QueryTriggerInteraction.Ignore); physAirTime = air ? physAirTime + dt : 0f;
        // ---- vectors on you, drawn a little in front so you can see them
        Vector3 o = feet + fwd * 1.5f + Vector3.up * 0.95f, view = cam.forward;
        Vector3 vTip = o + Vector3.ClampMagnitude(physVel * 0.35f, 1.6f); ArrowLine(physV, o, vTip, view); physVL.transform.position = vTip + Vector3.up * 0.18f; physVL.text = "<b>v = " + speed.ToString("F1") + " m/s</b>";
        ArrowLine(physG, o, o + Vector3.down * 0.7f, view); physGL.transform.position = o + Vector3.down * 0.82f + right * 0.5f; physGL.text = "<b>W = m g</b>  g = 1.62 m/s^2";
        physN.enabled = !air; physNL.gameObject.SetActive(!air); if (!air) { ArrowLine(physN, o + right * 0.12f, o + right * 0.12f + Vector3.up * 0.7f, view); physNL.transform.position = o + Vector3.up * 0.85f + right * 0.45f; physNL.text = "<b>N</b> floor pushes back"; }
        physPath.enabled = air; if (air) for (int i = 0; i < 24; i++) { float t = i * 0.12f; physPath.SetPosition(i, feet + Vector3.up * 0.1f + physVel * t + Vector3.down * (0.5f * MoonG * t * t)); }
        // ---- the gravity field: the same vector at every point
        float gx = Mathf.Round(feet.x / 3f) * 3f, gz = Mathf.Round(feet.z / 3f) * 3f; int n = 0;
        for (int ix = -2; ix <= 2; ix++) for (int iz = -2; iz <= 2; iz++) { Vector3 a = new Vector3(gx + ix * 3f, feet.y + 2.3f, gz + iz * 3f); ArrowLine(physField[n++], a, a + Vector3.down * 0.55f, view); }
        // ---- packets on the sensor network
        float tt = Time.time; for (int i = 0; i < physNet.Count; i++) { float u = Mathf.PingPong(tt * 0.35f + i * 0.17f, 1f); Vector3 a = physNet[i].GetPosition(0), m = physNet[i].GetPosition(1), b = physNet[i].GetPosition(2); physPackets[i].position = Vector3.Lerp(Vector3.Lerp(a, m, u), Vector3.Lerp(m, b, u), u); }
        // ---- the graph board follows you, low and to the left
        Vector3 want = cam.position + fwd * 1.25f - right * 0.75f + Vector3.down * 0.32f; physBoard.position = Vector3.Lerp(physBoard.position, want, 1f - Mathf.Exp(-dt * 5f)); physBoard.rotation = Quaternion.LookRotation(physBoard.position - cam.position, Vector3.up);
        physTick -= dt; if (physTick > 0f) return; physTick = 0.1f;
        if (gN < gH.Length) gN++; for (int i = gH.Length - 1; i > 0; i--) { gH[i] = gH[i - 1]; gV[i] = gV[i - 1]; gA[i] = gA[i - 1]; }
        gH[0] = feet.y; gV[0] = speed; gA[0] = physAcc; PhysDraw();
        float ke = 0.5f * PhysMass * speed * speed, pe = PhysMass * MoonG * Mathf.Max(0f, feet.y - gHmin);
        physInfo.text = "<b>PHYSICS VIEW</b>   kinetic " + Mathf.RoundToInt(ke) + " J   potential " + Mathf.RoundToInt(pe) + " J   total " + Mathf.RoundToInt(ke + pe) + " J";
    }
    float gHmin;
    void PhysPlot(float[] d, float lo, float hi, Color32 c)
    {
        int px = -1, py = 0; float span = Mathf.Max(0.001f, hi - lo);
        for (int i = 0; i < gN; i++)
        {
            int x = GW - 1 - i * 2, y = Mathf.Clamp(Mathf.RoundToInt((d[i] - lo) / span * (GH - 24)) + 12, 1, GH - 2);
            if (px >= 0) { int steps = Mathf.Max(Mathf.Abs(px - x), Mathf.Abs(py - y)); for (int s = 0; s <= steps; s++) { int xx = Mathf.RoundToInt(Mathf.Lerp(px, x, s / (float)Mathf.Max(1, steps))), yy = Mathf.RoundToInt(Mathf.Lerp(py, y, s / (float)Mathf.Max(1, steps))); if (xx >= 0 && xx < GW) { physPix[yy * GW + xx] = c; physPix[(yy + 1) * GW + xx] = c; } } }
            px = x; py = y;
        }
    }
    void PhysDraw()
    {
        var bg = new Color32(8, 12, 26, 255); var grid = new Color32(30, 44, 78, 255);
        for (int y = 0; y < GH; y++) for (int x = 0; x < GW; x++) physPix[y * GW + x] = (x % 40 == 0 || y % 30 == 12) ? grid : bg;
        float hMin = 1e9f, hMax = -1e9f, vMax = 2f, aMax = 2f; for (int i = 0; i < gN; i++) { hMin = Mathf.Min(hMin, gH[i]); hMax = Mathf.Max(hMax, gH[i]); vMax = Mathf.Max(vMax, gV[i]); aMax = Mathf.Max(aMax, Mathf.Abs(gA[i])); }
        gHmin = hMin; if (hMax - hMin < 1f) hMax = hMin + 1f;
        PhysPlot(gA, -aMax, aMax, new Color32(255, 138, 122, 255)); PhysPlot(gV, 0f, vMax, new Color32(124, 252, 154, 255)); PhysPlot(gH, hMin, hMax, new Color32(108, 184, 255, 255));
        physTex.SetPixels32(physPix); physTex.Apply(false);
    }
    public string ProbePhys() { if (!physOn) PhysToggle(); return "physics view " + physOn + " field " + physField.Count + " links " + physNet.Count + " speed " + physVel.magnitude.ToString("F1") + " samples " + gN; }
    public string ProbePhysOff() { if (physOn) PhysToggle(); return "physics view " + physOn; }
    public string ProbePhysSheet() { OpenRead(physSheet); return "sheet " + physSheet; }
    public string ProbeJump() { LaunchPlayer(4.5f); return "jump"; }
}
