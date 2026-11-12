// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/C_Base_Crew_Systems/HunchFrontier22.cs
//  Section : 06_Frontier/C_Base_Crew_Systems
//  Folder  : Systems inside the central base: pick-ups + battery bank, NASA HUNCH mode, physics
//            view, Autopilot Smooth, crew of four + sleep pods, The Show, self-working crew,
//            airlocks/lifts, crew self-care.
//  Purpose : Twenty-second part of Moon Frontier: real ways through the base. The Blender model has
//            rooms and buildings with no doorway between them. Until now the autopilot crossed
//            those with a
//  Contains: HunchFrontier, Airlock
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// Twenty-second part of Moon Frontier: real ways through the base.
///   The Blender model has rooms and buildings with no doorway between them. Until now the autopilot crossed those with a
///   "transfer" (a jump) or with the walk-through-walls cheat. That is gone: once the walkable map is ready the game finds
///   every place where the way stops at a wall or a floor, and puts an AIRLOCK DOOR or a LIFT there. They are part of the
///   map, so the autopilot, the crew and the green arrows all use them, and so can you: walk into the blue door.
public partial class HunchFrontier
{
    class Airlock { public Vector3 a, b; public bool lift, small; public Transform ta, tb; }
    readonly List<Airlock> airlocks = new List<Airlock>();
    [NonSerialized] public bool airBuilt, gliding; bool airBusy; string airLog = ""; int airGlides, airDoors, airLifts, airFailed; float airCool, airStand, canvasNext; Vector3 airLastFeet;

    void UpdateAirlocks(float dt)
    {
        // world canvases need to know which camera looks at them, or the headset cannot point at them
        if (Time.unscaledTime >= canvasNext && cam)
        {
            canvasNext = Time.unscaledTime + 2.5f; var c = cam.GetComponent<Camera>();
            if (c) foreach (var cv in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (cv.renderMode == RenderMode.WorldSpace && !cv.worldCamera) cv.worldCamera = c;
        }
        if (!airBuilt && !airBusy && HunchPath.I && HunchPath.I.Ready) { airBusy = true; StartCoroutine(BuildAirlocks()); }
        if (!airBuilt || gliding || !Playing || mapNow != 0 || game.menuOpen || autoOn || ride != null || Time.time < airCool) { airLastFeet = Feet(); return; }
        // you: keep walking into the wall where one of these hidden doors is, and after a moment it lets you through (like an automatic door)
        Vector3 f = Feet(), vel = dt > 0.0001f ? (f - airLastFeet) / dt : Vector3.zero; airLastFeet = f; vel.y = 0f;
        Vector3 want = game.HeadsetActive ? Quaternion.Euler(0f, cam.eulerAngles.y, 0f) * new Vector3(HunchVr.LeftStickNow.x, 0f, HunchVr.LeftStickNow.y) : (desk ? desk.MoveIntent : Vector3.zero); want.y = 0f;
        Airlock push = null; bool pushA = true;
        if (want.sqrMagnitude > 0.25f && vel.magnitude < 0.6f)
            foreach (var k in airlocks)
            {
                if (k.small || k.lift) continue;
                for (int e = 0; e < 2 && push == null; e++)
                {
                    Vector3 here = e == 0 ? k.a : k.b, there = e == 0 ? k.b : k.a; if (!AirNear(f, here, 0.9f)) continue;
                    Vector3 dir = there - here; dir.y = 0f; if (dir.sqrMagnitude < 0.01f) continue;
                    if (Vector3.Dot(want.normalized, dir.normalized) > 0.6f) { push = k; pushA = e == 0; }
                }
            }
        if (push != null) { airStand += dt; if (airStand > 0.45f) { airStand = 0f; StartCoroutine(AirGlide(push, pushA)); } }
        else airStand = 0f;
    }

    bool AirNear(Vector3 p, Vector3 q, float r) { Vector3 d = p - q; float dy = Mathf.Abs(d.y); d.y = 0f; return dy < 1.5f && d.sqrMagnitude < r * r; }
    /// is the stretch from where you stand to this next corner of the path one of the doors or lifts?
    bool AirAt(Vector3 feet, Vector3 next, out Airlock lk, out bool fromA)
    {
        lk = null; fromA = true;
        foreach (var k in airlocks)
        {
            if (AirNear(feet, k.a, 0.9f) && (next - k.b).sqrMagnitude < 1.2f * 1.2f && (feet - k.b).sqrMagnitude > 0.3f * 0.3f) { lk = k; fromA = true; return true; }
            if (AirNear(feet, k.b, 0.9f) && (next - k.a).sqrMagnitude < 1.2f * 1.2f && (feet - k.a).sqrMagnitude > 0.3f * 0.3f) { lk = k; fromA = false; return true; }
        }
        return false;
    }
    /// stuck next to a door or a lift: use it, in the direction that brings you closer to where you are going
    bool AirNearby(Vector3 feet, Vector3 target, out Airlock lk, out bool fromA)
    {
        lk = null; fromA = true; float best = float.MaxValue;
        foreach (var k in airlocks)
            for (int e = 0; e < 2; e++)
            {
                Vector3 here = e == 0 ? k.a : k.b, there = e == 0 ? k.b : k.a; if (!AirNear(feet, here, 1.6f)) continue;
                float gain = (there - target).sqrMagnitude - (here - target).sqrMagnitude; if (gain < 0f && gain < best) { best = gain; lk = k; fromA = e == 0; }
            }
        return lk != null;
    }
    public string ProbeAirDebug()
    {
        var hp = HunchPath.I; if (!hp || !hp.Ready) return "no map"; int j = Mathf.Clamp(probeInt, 0, game.zones.Count - 1); Vector3 to = game.zones[j].position; var f = hp.Filter;
        NavMeshHit a = new NavMeshHit(), b = new NavMeshHit(); bool sa = NavMesh.SamplePosition(game.zones[0].position + Vector3.up * 0.2f, out a, 2.6f, f), sb = NavMesh.SamplePosition(to + Vector3.up * 0.2f, out b, 2.6f, f);
        var p = new NavMeshPath(); bool ok = sa && sb && NavMesh.CalculatePath(a.position, b.position, f, p);
        string corners = ""; if (ok) foreach (var c in p.corners) corners += c.ToString("F1") + " ";
        NavMeshHit e = new NavMeshHit(); bool edge = sb && NavMesh.FindClosestEdge(b.position, out e, f);
        return game.zones[j].id + " zone " + to.ToString("F1") + " sampled " + (sb ? b.position.ToString("F2") : "none") + " | from airlock: " + (ok ? p.status + " corners " + p.corners.Length + ": " + corners : "no path") + " | nearest edge " + (edge ? e.distance.ToString("F2") : "-");
    }
    /// a short smooth step along the mapped way when the body is caught on an edge the map does not know about
    IEnumerator GlideTo(Vector3 to, float speed)
    {
        gliding = true; HunchNavAssist.hold = true; if (nav) nav.debugIntent = Vector3.zero;
        var cc = origin.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
        Vector3 from = Feet(); to += Vector3.up * 0.04f; float dur = Mathf.Max(0.25f, (to - from).magnitude / speed), t = 0f;
        while (t < dur) { t += Time.deltaTime; AirPut(Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur)))); yield return null; }
        AirPut(to); Physics.SyncTransforms(); if (cc) cc.enabled = true; HunchNavAssist.hold = false; airLastFeet = Feet(); gliding = false;
    }
    void AirPut(Vector3 feet)
    {
        if (game.HeadsetActive) origin.MoveCameraToWorldLocation(feet + Vector3.up * Mathf.Max(0.2f, origin.CameraInOriginSpaceHeight));
        else origin.transform.position = feet;
    }
    /// through the door, or up or down the lift: a smooth ride, never a jump
    IEnumerator AirGlide(Airlock k, bool fromA)
    {
        gliding = true; HunchNavAssist.hold = true; if (nav) nav.debugIntent = Vector3.zero;
        var cc = origin.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
        Vector3 from = Feet(), to = (fromA ? k.b : k.a) + Vector3.up * 0.04f; float len = (to - from).magnitude, dur = Mathf.Max(0.4f, len / (k.lift ? 3.4f : 2.8f)), t = 0f;
        try { Play(cThud, 0.25f); } catch { }
        while (t < dur) { t += Time.deltaTime; float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur)); AirPut(Vector3.Lerp(from, to, u)); yield return null; }
        AirPut(to); Physics.SyncTransforms();
        if (cc) cc.enabled = true; HunchNavAssist.hold = false; airGlides++; airCool = Time.time + 0.9f; airLastFeet = Feet(); gliding = false;
    }

    void AirGate(Airlock k, bool endA)
    {
        Vector3 here = endA ? k.a : k.b, there = endA ? k.b : k.a, dir = there - here; dir.y = 0f; if (dir.sqrMagnitude < 0.0004f) dir = Vector3.forward; dir.Normalize();
        var t = new GameObject(k.lift ? "Lift pad" : "Airlock door").transform; t.SetParent(root, false); t.position = here; t.rotation = Quaternion.LookRotation(dir, Vector3.up);
        var gold = Lit(new Color(0.83f, 0.62f, 0.2f)); var glow = Glow(k.lift ? new Color(1.6f, 2.4f, 0.6f) : new Color(0.5f, 1.8f, 2.8f));
        Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.015f, 0f), new Vector3(0.8f, 0.012f, 0.8f), glow);
        Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.02f, 0f), new Vector3(0.62f, 0.013f, 0.62f), gold);
        if (k.lift)
        {
            foreach (float sx in new[] { -0.6f, 0.6f }) Prim(PrimitiveType.Cylinder, t, new Vector3(sx, 0.55f, 0f), new Vector3(0.07f, 0.55f, 0.07f), gold);
            var l = Label(t, "<b>LIFT</b>\n<size=60%>" + (there.y > here.y ? "going up" : "going down") + ": stand here</size>", 0f, 1.9f, 0.7f, 3f, 0.6f, new Color(0.8f, 1f, 0.5f)); mapLabels.Add(l);
        }
        else if (!Physics.CheckBox(here + Vector3.up * 1.2f + dir * 0.2f, new Vector3(0.75f, 0.9f, 0.12f), t.rotation, ~0, QueryTriggerInteraction.Ignore))
        {
            foreach (float sx in new[] { -0.62f, 0.62f }) Prim(PrimitiveType.Cube, t, new Vector3(sx, 1.1f, 0.2f), new Vector3(0.12f, 2.2f, 0.12f), gold);
            Prim(PrimitiveType.Cube, t, new Vector3(0f, 2.26f, 0.2f), new Vector3(1.36f, 0.12f, 0.12f), gold);
            Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.1f, 0.22f), new Vector3(1.12f, 2.2f, 0.03f), Clear(new Color(0.4f, 0.85f, 1f, 0.3f)));
            var l = Label(t, "<b>AIRLOCK DOOR</b>\n<size=60%>walk in</size>", 0f, 2.6f, 0.6f, 3f, 0.5f, new Color(0.6f, 0.9f, 1f)); mapLabels.Add(l);
        }
        if (endA) k.ta = t; else k.tb = t;
    }
    void AddAirlock(Vector3 a, Vector3 b, bool lift, int agent)
    {
        Vector3 ab = b - a; float flat = new Vector2(ab.x, ab.z).magnitude;
        var k = new Airlock { a = a, b = b, lift = lift, small = !lift && flat < 1.6f && Mathf.Abs(ab.y) < 0.6f };      // a small gap in the walkable map (a raised sill): no door is drawn and you simply walk over it
        NavMesh.AddLink(new NavMeshLinkData { startPosition = a, endPosition = b, width = 0.7f, costModifier = -1f, bidirectional = true, area = 0, agentTypeID = agent });
        HunchPath.Links.Add(new[] { a, b });
        airlocks.Add(k);      // nothing is drawn: the doors work like the base's own automatic doors, so they never stand in the way if (lift) airLifts++; else airDoors++;
    }

    /// Makes every zone reachable on foot from every other: wherever the way stops, a door through that wall or a lift to that floor.
    IEnumerator BuildAirlocks()
    {
        yield return null;
        HunchPath.Links.Clear();
        var hp = HunchPath.I; var filter = hp.Filter; var path = new NavMeshPath(); var tmp = new NavMeshPath();
        var pts = new List<Vector3>(); foreach (var z in game.zones) pts.Add(z.position);
        if (meetTable) pts.Add(meetTable.position + new Vector3(0f, 0f, -3.4f));
        int root = -1; NavMeshHit rh = new NavMeshHit();
        for (int i = 0; i < pts.Count && root < 0; i++) if (NavMesh.SamplePosition(pts[i] + Vector3.up * 0.2f, out rh, 2.6f, filter)) root = i;
        if (root < 0) { airLog = "no zone stands on the walkable map"; airBuilt = true; yield break; }
        Vector3 start = rh.position; HunchPath.Hub = start; HunchPath.HubSet = false;
        // both ways: from the airlock to every place, and from every place back (a room can be easy to get into and hard to leave)
        for (int jj = 0; jj < pts.Count * 2; jj++)
        {
            int j = jj % pts.Count; bool back = jj >= pts.Count;
            if (j == root) continue;
            NavMeshHit th; if (!NavMesh.SamplePosition(pts[j] + Vector3.up * 0.2f, out th, 2.6f, filter)) { if (!back) { airFailed++; airLog += "place " + j + " is not on the walkable map; "; } continue; }
            var way = new List<Vector3>(); Vector3 from = back ? th.position : start, goal = back ? start : th.position;
            for (int n = 0; n < 9 && airlocks.Count < 60; n++)
            {
                float wl; bool wc; if (!hp.Find(from, goal, way, out wl, out wc)) { airFailed++; airLog += "no way at all to place " + j + "; "; break; }
                if (wc) break;
                Vector3 E = way[way.Count - 1], T = goal, v = T - E, hv = v; hv.y = 0f; Vector3 B = E; bool lift = false, ok = false; float eT = v.magnitude;
                // a way through only counts if it really brings you closer to the place (not into a dead-end corner or onto a ledge)
                var qway = new List<Vector3>();
                Func<Vector3, bool> closer = q => { float ql; bool qc; if (!hp.Find(q, T, qway, out ql, out qc)) return false; return qc || (qway[qway.Count - 1] - T).magnitude < eT - 0.6f; };
                if (Mathf.Abs(v.y) > 1.6f && hv.magnitude < 5f)
                {
                    // the place is on the floor above or below: a lift, straight up or down where that leads somewhere, else straight to the place
                    lift = true; NavMeshHit lh;
                    if (NavMesh.SamplePosition(new Vector3(E.x, T.y + 0.2f, E.z), out lh, 4f, filter) && Mathf.Abs(lh.position.y - T.y) < 1.2f && closer(lh.position)) { B = lh.position; ok = true; }
                    else { B = T; ok = true; }
                }
                else
                {
                    Vector3 dir = hv.sqrMagnitude > 0.0001f ? hv.normalized : Vector3.forward;
                    for (float d = 0.6f; d <= 18f && !ok; d += 0.3f)
                    {
                        NavMeshHit bh; if (!NavMesh.SamplePosition(E + dir * d + Vector3.up * 0.2f, out bh, 0.45f, filter)) continue;
                        if (Mathf.Abs(bh.position.y - E.y) > 0.9f) continue;                                                             // the same floor only: never a door onto a table or a roof
                        if (NavMesh.CalculatePath(E, bh.position, filter, tmp) && tmp.status == NavMeshPathStatus.PathComplete) continue;       // still the room you are in
                        if (!closer(bh.position)) continue;                                                                               // a dead end behind the wall: look further
                        B = bh.position; ok = true;
                    }
                    if (!ok && eT < 40f) { B = T; ok = true; lift = Mathf.Abs(v.y) > 1.6f; }      // nothing better on the other side: link straight to the place
                }
                bool same = false; foreach (var k0 in airlocks) if (((k0.a - E).sqrMagnitude < 0.09f && (k0.b - B).sqrMagnitude < 0.09f) || ((k0.b - E).sqrMagnitude < 0.09f && (k0.a - B).sqrMagnitude < 0.09f)) same = true;
                if (same && (B - T).sqrMagnitude > 0.09f && eT < 40f) { B = T; lift = Mathf.Abs(v.y) > 1.6f; same = false; foreach (var k0 in airlocks) if ((k0.a - E).sqrMagnitude < 0.09f && (k0.b - B).sqrMagnitude < 0.09f) same = true; }
                if (same) { airFailed++; airLog += "the way to place " + j + " would not open; "; break; }
                if (!ok || (B - E).sqrMagnitude < 0.05f) { airFailed++; airLog += "could not open a way to place " + j + "; "; break; }
                AddAirlock(E, B, lift, hp.AgentType);
                yield return null;
            }
        }
        // check: how many places can now be walked to
        int reach = 0; var chk = new List<Vector3>(); for (int j = 0; j < pts.Count; j++) { if (j == root) { reach++; continue; } float cl; bool cc2; if (hp.Find(start, pts[j], chk, out cl, out cc2) && cc2) reach++; }
        airLog = "airlock doors " + airDoors + ", lifts " + airLifts + ", places you can walk to " + reach + " of " + pts.Count + (airFailed > 0 ? ", problems " + airFailed + ": " + airLog : "");
        HunchPath.HubSet = true;
        HLog.I("Crew", "ways through the base: " + airLog);
        airBuilt = true;
    }

    public string ProbeAir() { var sb = new System.Text.StringBuilder(); foreach (var k in airlocks) sb.Append(k.lift ? "L" : "D").Append(k.a.ToString("F0")).Append(">").Append(k.b.ToString("F0")).Append(' '); return "built " + airBuilt + " | " + airLog + " | rides " + airGlides + " | " + sb + "| zones " + (HunchPath.I ? HunchPath.I.ProbeZones() : "no map"); }
    public string ProbeAirView() { if (airlocks.Count == 0) return "no doors"; var k = airlocks[Mathf.Clamp(probeInt, 0, airlocks.Count - 1)]; Vector3 d = k.b - k.a; d.y = 0f; d = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward; game.flyMode = false; TeleportTo(k.a - d * 1.6f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg); return "looking at " + (k.lift ? "lift " : "door ") + probeInt + " of " + airlocks.Count + " at " + k.a.ToString("F1"); }
    public string ProbeAirBack()
    {
        var hp = HunchPath.I; if (!hp || !hp.Ready) return "no map"; int j = Mathf.Clamp(probeInt, 0, game.zones.Count - 1); var w = new List<Vector3>(); float l; bool c;
        var sb = new System.Text.StringBuilder(game.zones[j].id + ": ");
        foreach (int t in new[] { 0, 3, 9, 15 }) { if (t >= game.zones.Count) continue; bool ok = hp.Find(game.zones[j].position, game.zones[t].position, w, out l, out c); sb.Append("to ").Append(game.zones[t].id).Append(ok ? (c ? " complete " : " PARTIAL end " + w[w.Count - 1].ToString("F1")) : " none").Append(" | "); }
        return sb.ToString();
    }
}
