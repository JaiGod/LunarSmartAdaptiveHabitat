// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/03_Player_Controls/HunchPath.cs
//  Section : 03_Player_Controls
//  Folder  : How you move and find your way: VR controllers, keyboard/mouse, phone touch controls,
//            doorway help, NavMesh paths, green guide arrows + minimap.
//  Purpose : Real paths through the base: round the walls, through the openings, up the stairs. A
//            few seconds after the game starts this builds a walkable map (a NavMesh) of the main
//            base from the same solid
//  Contains: HunchPath
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// Real paths through the base: round the walls, through the openings, up the stairs.
/// A few seconds after the game starts this builds a walkable map (a NavMesh) of the main base from the same solid
/// shapes the player bumps into. It is built in the background, so the game does not stop.
/// Used by Autopilot Smooth and The Show (so the Commander does not walk through hard surfaces) and by the green guide
/// arrows (so they follow the way you can really walk, and the distance is the walking distance).
/// If the map cannot be built, everything falls back to the straight-line behaviour and says so.
public class HunchPath : MonoBehaviour
{
    public static HunchPath I;
    public bool Ready, Failed; public string Info = "not started"; public float BuildSeconds; public int Sources, Asked, Found, Complete, Unreadable;

    public int AgentType;
    /// the airlock doors and lifts the game added where the model has no way through: each is two ends you can pass between
    public static readonly List<Vector3[]> Links = new List<Vector3[]>();
    /// a place every room can be walked to and from (the doors are built that way): when a way stops short, it goes via here
    public static Vector3 Hub; public static bool HubSet; bool viaHub; public NavMeshQueryFilter Filter { get { return filter; } }
    NavMeshData data; NavMeshDataInstance inst; NavMeshQueryFilter filter; NavMeshPath path; AsyncOperation op; float t0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var g = FindFirstObjectByType<HabitatGame>();
        if (g && !g.GetComponent<HunchPath>() && !Application.isBatchMode) g.gameObject.AddComponent<HunchPath>();
    }
    void Awake() { I = this; }
    void OnDestroy() { if (inst.valid) inst.Remove(); if (I == this) I = null; }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(4.5f);                 // after the game has put its own furniture in place
        Begin();
        while (op != null && !op.isDone) yield return null;
        if (op == null) yield break;
        BuildSeconds = Time.realtimeSinceStartup - t0;
        var tri = NavMesh.CalculateTriangulation();
        if (tri.vertices == null || tri.vertices.Length < 30) { Failed = true; Info = "no walkable map could be made from the model (its meshes may not allow reading in a built game)"; HLog.W("Player", "path: " + Info); yield break; }
        Ready = true; Info = "walkable map ready: " + (tri.indices.Length / 3) + " pieces from " + Sources + " shapes in " + BuildSeconds.ToString("F1") + " s" + (Unreadable > 0 ? " (" + Unreadable + " shapes are not readable outside the Editor)" : "");
        HLog.I("Player", "path: " + Info);
    }

    void Begin()
    {
        try
        {
            t0 = Time.realtimeSinceStartup; path = new NavMeshPath();
            var s = NavMesh.CreateSettings(); s.agentRadius = 0.24f; s.agentHeight = 1.5f; s.agentClimb = 0.36f; s.agentSlope = 55f;
            s.overrideVoxelSize = true; s.voxelSize = 0.12f; s.overrideTileSize = true; s.tileSize = 128; s.minRegionArea = 1.2f;
            filter = new NavMeshQueryFilter { agentTypeID = s.agentTypeID, areaMask = NavMesh.AllAreas }; AgentType = s.agentTypeID;
            var bounds = new Bounds(new Vector3(0f, 14f, 0f), new Vector3(300f, 46f, 300f));       // the main base, every floor
            var src = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), src);
            var game = GetComponent<HabitatGame>(); Transform me = game && game.origin ? game.origin.transform : null;
            for (int i = src.Count - 1; i >= 0; i--)
            {
                var c = src[i].component; bool drop = !c;
                if (!drop)
                {
                    var col = c as Collider; Transform t = c.transform;
                    if (c is CharacterController || (col && col.isTrigger) || c.GetComponentInParent<Rigidbody>() || (me && t.IsChildOf(me))) drop = true;      // nothing that moves, and not you
                    else { string top = TopName(t); if (top == "HUNCH Crew" || top == "HUNCH Zones" || top == "ARIA Robot Buddy" || top == "HUNCH Recreation Objects") drop = true; }
                    if (!drop) for (Transform p = t; p != null; p = p.parent) { string n = p.name; if (n == "Aliens" || n == "UFO" || n.StartsWith("Rover") || n.StartsWith("Meteorite") || n.StartsWith("Astronaut")) { drop = true; break; } }
                }
                if (drop) src.RemoveAt(i);
            }
            Sources = src.Count; foreach (var q in src) { var mc = q.component as MeshCollider; if (mc && mc.sharedMesh && !mc.sharedMesh.isReadable) Unreadable++; }
            data = new NavMeshData(s.agentTypeID); inst = NavMesh.AddNavMeshData(data);
            op = NavMeshBuilder.UpdateNavMeshDataAsync(data, s, src, bounds); Info = "building the walkable map from " + Sources + " shapes";
        }
        catch (System.Exception e) { Failed = true; op = null; Info = "could not start: " + e.Message; HLog.W("Player", "path: " + Info); }
    }
    static string TopName(Transform t) { while (t.parent) t = t.parent; return t.name; }

    /// The way to walk from one place to another. complete = it reaches the place; otherwise it ends at the nearest point you can walk to.
    public bool Find(Vector3 from, Vector3 to, List<Vector3> corners, out float length, out bool complete)
    {
        bool ok = FindDirect(from, to, corners, out length, out complete);
        if (ok && complete || !HubSet || viaHub) return ok;
        // stopped short: go via the hub instead (every room is joined to it both ways)
        viaHub = true;
        var a = new List<Vector3>(); var b = new List<Vector3>(); float la, lb; bool ca, cb;
        bool okA = FindDirect(from, Hub, a, out la, out ca), okB = okA && ca && FindDirect(Hub, to, b, out lb, out cb) && cb;
        viaHub = false;
        if (!okB) return ok;
        corners.Clear(); corners.AddRange(a); for (int i = 1; i < b.Count; i++) corners.Add(b[i]);
        FindDirect(Hub, to, b, out lb, out cb); length = la + lb; complete = true; return true;
    }
    bool FindDirect(Vector3 from, Vector3 to, List<Vector3> corners, out float length, out bool complete)
    {
        corners.Clear(); length = 0f; complete = false; if (!Ready) return false; Asked++;
        NavMeshHit a, b;
        if (!NavMesh.SamplePosition(from + Vector3.up * 0.2f, out a, 2.2f, filter) || !NavMesh.SamplePosition(to + Vector3.up * 0.2f, out b, 2.6f, filter)) return false;
        // long ways across the whole base can run out of search room in one go: carry on from where the last piece stopped, up to six pieces
        Vector3 cur = a.position; var used = new List<int>(); int usedIndex = -1;
        for (int piece = 0; piece < 10; piece++)
        {
            if (!NavMesh.CalculatePath(cur, b.position, filter, path) || path.status == NavMeshPathStatus.PathInvalid) { if (piece == 0) return false; break; }
            var c = path.corners; if (c == null || c.Length < 1) { if (piece == 0) return false; break; }
            int firstOfPiece = corners.Count;
            for (int i = 0; i < c.Length; i++) { if (corners.Count > 0 && (corners[corners.Count - 1] - c[i]).sqrMagnitude < 0.0004f) continue; if (corners.Count > 0) length += (c[i] - corners[corners.Count - 1]).magnitude; corners.Add(c[i]); }
            Vector3 end = c[c.Length - 1];
            if (path.status == NavMeshPathStatus.PathComplete && (end - b.position).sqrMagnitude < 0.5f) { complete = true; break; }
            // stopped short: if a door or a lift starts right here, go through it and carry on from its other end
            Vector3[] use = null; bool fromA = true; float bd = 2.2f * 2.2f;
            for (int li = 0; li < Links.Count; li++)
            {
                if (used.Contains(li)) continue; var L = Links[li];
                for (int e = 0; e < 2; e++) { Vector3 d = end - L[e]; float dy = Mathf.Abs(d.y); d.y = 0f; if (dy < 1.6f && d.sqrMagnitude < bd) { bd = d.sqrMagnitude; use = L; fromA = e == 0; usedIndex = li; } }
            }
            if (use == null)
            {
                // no door right where it stopped: is there one you CAN walk to whose far side is nearer the place?
                // a door you can walk to that leads out of this area and whose far side is nearer the place
                float bestFar = (end - b.position).magnitude - 0.5f; var tp = new NavMeshPath();
                for (int li = 0; li < Links.Count; li++)
                {
                    if (used.Contains(li)) continue; var L = Links[li];
                    for (int e = 0; e < 2; e++)
                    {
                        float far = (L[1 - e] - b.position).magnitude; if (far >= bestFar) continue;
                        if (!NavMesh.CalculatePath(cur, L[e], filter, tp) || tp.status != NavMeshPathStatus.PathComplete) continue;
                        if (NavMesh.CalculatePath(cur, L[1 - e], filter, tp) && tp.status == NavMeshPathStatus.PathComplete) continue;      // its far side is in this same area: no use
                        bestFar = far; use = L; fromA = e == 0; usedIndex = li;
                    }
                }
                if (use != null)
                {
                    // walk back from where the piece stopped to that door: replace this piece with the way to the door
                    NavMesh.CalculatePath(cur, fromA ? use[0] : use[1], filter, tp); var tc = tp.corners;
                    while (corners.Count > 0 && piece >= 0 && corners.Count > firstOfPiece) corners.RemoveAt(corners.Count - 1);
                    foreach (var q in tc) { if (corners.Count > 0 && (corners[corners.Count - 1] - q).sqrMagnitude < 0.0004f) continue; corners.Add(q); }
                    end = fromA ? use[0] : use[1];
                }
            }
            if (use != null)
            {
                used.Add(usedIndex); Vector3 p0 = fromA ? use[0] : use[1], p1 = fromA ? use[1] : use[0];
                length += (p0 - end).magnitude + (p1 - p0).magnitude; corners.Add(p0); corners.Add(p1); cur = p1; continue;
            }
            if ((end - cur).sqrMagnitude < 0.25f) break;      // no progress: this really is as far as you can walk
            cur = end;
        }
        if (corners.Count < 2) { if (corners.Count == 1) corners.Add(corners[0]); else return false; }
        Found++; if (complete) Complete++;
        return true;
    }

    public string ProbeInfo() { return "ready=" + Ready + " failed=" + Failed + " | " + Info + " | asked " + Asked + " found " + Found + " complete " + Complete; }
    /// test hook: is there a way between every pair of neighbouring zones?
    public string ProbeZones()
    {
        var game = GetComponent<HabitatGame>(); if (!Ready || !game) return ProbeInfo();
        var sb = new System.Text.StringBuilder(); var cs = new List<Vector3>(); int ok = 0, part = 0, none = 0;
        for (int i = 0; i < game.zones.Count; i++)
        {
            int j = (i + 1) % game.zones.Count; float len; bool comp;
            if (!Find(game.zones[i].position, game.zones[j].position, cs, out len, out comp)) { none++; sb.Append(game.zones[i].id).Append(">").Append(game.zones[j].id).Append(":none "); }
            else if (comp) { ok++; sb.Append(game.zones[i].id).Append(">").Append(game.zones[j].id).Append(":").Append(Mathf.RoundToInt(len)).Append("m "); }
            else { part++; sb.Append(game.zones[i].id).Append(">").Append(game.zones[j].id).Append(":part ").Append(Mathf.RoundToInt(len)).Append("m "); }
        }
        return "complete " + ok + " partial " + part + " none " + none + " | " + sb + "| " + Info;
    }
}
