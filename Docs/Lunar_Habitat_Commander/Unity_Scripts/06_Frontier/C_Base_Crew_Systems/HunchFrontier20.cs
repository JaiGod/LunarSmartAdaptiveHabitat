// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/C_Base_Crew_Systems/HunchFrontier20.cs
//  Section : 06_Frontier/C_Base_Crew_Systems
//  Folder  : Systems inside the central base: pick-ups + battery bank, NASA HUNCH mode, physics
//            view, Autopilot Smooth, crew of four + sleep pods, The Show, self-working crew,
//            airlocks/lifts, crew self-care.
//  Purpose : Twentieth part of Moon Frontier: - a crew that works by itself: the three crew members
//            you are not playing walk to where their day takes them
//  Contains: HunchFrontier, CrewAi, FallStar, PopTarget
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Twentieth part of Moon Frontier:
///   - a crew that works by itself: the three crew members you are not playing walk to where their day takes them
///     (on the mapped paths, like Autopilot Smooth) and answer alerts on their own. Switch into any of them with F6.
///   - a floor guard: if there is ever no floor under you, you are put back on the last safe spot at once.
///   - the five other moonbases (full game): one giant portal each at the main base, one giant portal back,
///     and a game to play at every base. No other worlds: you stand on the same Moon the whole time.
///   - two more games for those bases: Star Shower and Target Tower.
public partial class HunchFrontier
{
    // ================================================================== the crew works by itself
    class CrewAi { public bool has; public Vector3 pos; public int zone = -1, job = -1, pi; public readonly List<Vector3> path = new List<Vector3>(); public float wait, work, bob, yaw, idle; public int care; public string doing = ""; }
    readonly CrewAi[] crewAi = { new CrewAi(), new CrewAi(), new CrewAi(), new CrewAi() };
    [NonSerialized] public bool crewAiOn = true; float crewAiNext; int crewAiJobs;

    /// called for every crew member who has a body, every frame; true = this code placed them
    bool CrewAiPose(int k, Transform av)
    {
        if (!crewAiOn || k < 0 || k > 3 || !Playing || mapNow != 0) return false;
        var a = crewAi[k]; float dt = Time.deltaTime * Mathf.Max(1f, game.simSpeed);
        if (!a.has || (a.pos - av.position).sqrMagnitude > 60f * 60f) { a.pos = av.position; a.has = true; a.zone = -1; a.path.Clear(); }
        int tz = a.job >= 0 ? a.job : game.CrewZoneIndex(k); if (tz < 0 || tz >= game.zones.Count) return false;
        var z = game.zones[tz]; float[] side = { 80f, -80f, 135f, -135f };
        Vector3 spot = z.position + Quaternion.Euler(0f, z.yaw + side[k % 4], 0f) * Vector3.forward * 1.3f;
        if (a.zone != tz)
        {
            a.zone = tz; a.pi = 1; a.path.Clear(); a.work = 0f; float len; bool complete;
            if (!(HunchPath.I && HunchPath.I.Ready && HunchPath.I.Find(a.pos, spot, a.path, out len, out complete) && complete)) { a.path.Clear(); a.wait = 2.2f; }      // no walkable way between these two places on the model: a short transfer
            a.doing = "going to " + z.title;
        }
        bool moving = false;
        if (a.path.Count >= 2 && a.pi < a.path.Count)
        {
            Vector3 c = a.path[a.pi], d = c - a.pos; float step = 2.6f * dt;
            if (d.magnitude <= step) { a.pos = c; a.pi++; if (a.pi >= a.path.Count) { a.path.Clear(); a.pos = spot; } }
            else { a.pos += d.normalized * step; Vector3 f = new Vector3(d.x, 0f, d.z); if (f.sqrMagnitude > 0.0004f) a.yaw = Mathf.LerpAngle(a.yaw, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 1f - Mathf.Exp(-8f * Time.deltaTime)); }
            moving = true; a.bob += Time.deltaTime * 7f;
        }
        else if (a.wait > 0f) { a.wait -= dt; if (a.wait <= 0f) a.pos = spot; }
        else
        {
            a.pos = spot; Vector3 f = z.position - spot; f.y = 0f; if (f.sqrMagnitude > 0.01f) a.yaw = Mathf.LerpAngle(a.yaw, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 1f - Mathf.Exp(-6f * Time.deltaTime));
            if (a.job >= 0 && a.job == tz)
            {
                a.work += dt; a.doing = "working at " + z.title;
                if (a.work > 4f)
                {
                    if (a.care > 0) { CrewCared(k, a.care); a.care = 0; a.job = -1; a.work = 0f; return true; }
                    int before = sim.alerts.Count; bool mine = false; foreach (var al in sim.alerts) if (al.zoneId == z.id) mine = true;
                    if (mine && sim.phase == HabPhase.Mission) { sim.Interact(z.id); if (sim.alerts.Count < before) { crewAiJobs++; sim.Say("ARIA: " + CrewShort(k) + " took care of it at " + z.title + "."); } }
                    a.job = -1; a.work = 0f;
                }
            }
            else a.doing = "at " + z.title;
        }
        // a calm walk: a small step bounce and a gentle side-to-side sway, at a steady speed
        av.position = a.pos + Vector3.up * (moving ? Mathf.Abs(Mathf.Sin(a.bob)) * 0.035f : 0f); av.rotation = Quaternion.Euler(0f, a.yaw, moving ? Mathf.Sin(a.bob) * 3f : 0f);
        return true;
    }
    string CrewShort(int k) { string n = crewName[Mathf.Clamp(k, 0, 3)] ?? "A crewmate"; return n; }

    /// who goes to which alert: the nearest free crew member who is not you, once an alert has waited a little
    void UpdateCrewAi()
    {
        if (!crewAiOn || Time.time < crewAiNext || sim.phase != HabPhase.Mission || game.crewAvatars == null) return; crewAiNext = Time.time + 1f;
        foreach (var al in sim.alerts)
        {
            if (al.total <= 0f || al.secondsLeft > al.total * 0.72f) continue;      // you get the first go at every alert
            int zi = game.ZoneIndex(al.zoneId); if (zi < 0) continue;
            bool taken = false; for (int k = 0; k < 4; k++) if (crewAi[k].job == zi) taken = true; if (taken) continue;
            if (!autoOn && game.NearZone == zi) continue;                          // you are already standing there
            int best = -1; float bd = float.MaxValue;
            for (int k = 0; k < 4 && k < game.crewAvatars.Length; k++)
            {
                if (k == playAs || !game.crewAvatars[k] || crewAi[k].job >= 0 || CrewAsleep(k)) continue;
                float d = (crewAi[k].pos - game.zones[zi].position).sqrMagnitude; if (d < bd) { bd = d; best = k; }
            }
            if (best >= 0) { crewAi[best].job = zi; crewAi[best].work = 0f; }
        }
        UpdateCrewCare();
        for (int k = 0; k < 4; k++) { int j = crewAi[k].job; if (j < 0 || crewAi[k].care > 0) continue; bool still = false; foreach (var al in sim.alerts) if (game.ZoneIndex(al.zoneId) == j) still = true; if (!still) crewAi[k].job = -1; }      // someone else got there first
    }
    public string ProbeCrewAi() { var sb = new System.Text.StringBuilder("crewAi " + crewAiOn + " jobs done " + crewAiJobs + " alerts " + sim.alerts.Count + " | "); for (int k = 0; k < 4 && k < sim.crew.Count; k++) sb.Append(sim.crew[k].name).Append(": ").Append(crewAi[k].doing).Append(" ").Append(crewAi[k].pos.ToString("F0")).Append(crewAi[k].job >= 0 ? " JOB " + game.zones[crewAi[k].job].id : "").Append(" | "); return sb.ToString(); }

    // ================================================================== the floor guard
    Vector3 safeSpot; float safeYaw; bool safeHas; float safeAt; int safeSaves;
    void UpdateFloorGuard()
    {
        if (!Playing || game.menuOpen || mapNow != 0) return;
        Vector3 f = Feet(); RaycastHit h;
        // never under the Moon: falling is fine, flying is fine, but the ground is the lowest you can ever be
        if (terrOk)
        {
            bool onPad = Mathf.Max(Mathf.Abs(f.x), Mathf.Abs(f.z)) < 284f; float lowest = onPad ? Mathf.Min(lowY, terrBase) : TerrHeight(f.x, f.z);
            if (f.y < lowest - 0.3f)
            {
                EnsureGround(f.x, f.z); Vector3 g2; float y = onPad ? (GroundAt(f.x, f.z, out g2) ? g2.y : terrBase) : lowest; bool fly = game.flyMode;
                TeleportTo(new Vector3(f.x, y + 0.12f, f.z), cam.eulerAngles.y); game.flyMode = fly; safeSaves++; return;
            }
        }
        bool floor = Physics.Raycast(f + Vector3.up * 1.2f, Vector3.down, out h, 300f, ~0, QueryTriggerInteraction.Ignore);
        if (floor && h.normal.y > 0.5f)
        {
            if (!game.flyMode && f.y - h.point.y < 0.6f && Time.time > safeAt) { safeAt = Time.time + 0.25f; safeSpot = h.point; safeYaw = cam.eulerAngles.y; safeHas = true; }
            return;
        }
        if (floor) return;
        // past the model: lay the game's own ground under the feet right now and stand on it
        if (terrOk && EnsureGround(f.x, f.z)) { float ty = TerrHeight(f.x, f.z); if (f.y < ty + 0.02f) { bool fly = game.flyMode; TeleportTo(new Vector3(f.x, ty + 0.1f, f.z), cam.eulerAngles.y); game.flyMode = fly; } return; }
        if (!safeHas || game.flyMode && f.y > safeSpot.y - 2f) return;
        // nothing at all underneath: straight back to the last place that had a floor, the same frame, so there is no long fall
        TeleportTo(safeSpot + Vector3.up * 0.08f, safeYaw); safeSaves++;
        if (safeSaves <= 3) Note("That spot had no floor. You are back on safe ground.");
    }

    // ================================================================== switching player has its own menu line
    HunchMenu.ExtraPage pagePlayAs;
    void BuildSwitchPage()
    {
        if (!menu) return;
        pagePlayAs = new HunchMenu.ExtraPage { label = PlayAsLabel(), hint = "Step into the next crew member: Commander, Dr. Okafor, Eng. Tanaka, Sci. Rivera. You appear where they are standing. The one you leave carries on by themselves: they walk to their own jobs and answer alerts, like the other two. The key for this is F6.", direct = () => { menu.Close(); SwitchCrew(); pagePlayAs.label = PlayAsLabel(); } };
        int at = pageCheat != null ? menu.extraPages.IndexOf(pageCheat) : -1; if (at < 0) menu.extraPages.Add(pagePlayAs); else menu.extraPages.Insert(at, pagePlayAs);
        var old = menu.extraShow;
        // on a phone or tablet only the big choices are listed: travel, switch player, cheats, NASA HUNCH mode, and the base systems (crew health is in there)
        menu.extraShow = p =>
        {
            if (p == pagePlayAs) return true;
            bool ok = old == null || old(p);
            if (ok && HunchTouch.I && HunchTouch.I.on) return p == pageHunch || p == pageCheat || p == pageBase || p.label.StartsWith("Travel");
            return ok;
        };
    }
    string PlayAsLabel() { return "Switch player (F6): now " + PlayAsName; }

    // ================================================================== the five other moonbases
    static readonly string[] BaseName = { "Slicer Base", "Zero-G Base", "Dance Base", "Star Shower Base", "Target Base" };
    static readonly string[] BaseGame = { "Space Slicer", "Zero-G Dome", "Emote Stage", "Star Shower", "Target Tower" };
    readonly float[] baseReach = new float[5]; readonly Vector3[] baseCentre = new Vector3[5]; readonly bool[] baseOk = new bool[5]; readonly Vector3[] gateHome = new Vector3[5], gateAway = new Vector3[5]; readonly float[] gateHomeYaw = new float[5], gateAwayYaw = new float[5]; readonly bool[] gateOk = new bool[5];
    readonly List<Transform> gateSpin = new List<Transform>(); float gateCool;

    /// where the five other bases stand (read from the model: E2 ... E6)
    void FindMoonbases()
    {
        var model = GameObject.Find("HUNCH_Lunar_Base"); if (!model) return; baseRoofs.Clear();
        for (int k = 0; k < 5; k++)
        {
            var t = model.transform.Find("E" + (k + 2) + "_HUB_Shell"); var r = t ? t.GetComponent<Renderer>() : null;
            if (r) { baseCentre[k] = new Vector3(r.bounds.center.x, r.bounds.min.y, r.bounds.center.z); baseOk[k] = true; }
            // how far that base reaches (domes, rings and all), so the portal and the game stand clear outside it
            string pre = "E" + (k + 2) + "_"; float reach = 60f;
            if (baseOk[k]) foreach (Transform ch in model.transform)
            {
                if (!ch.name.StartsWith(pre)) continue; var cr = ch.GetComponent<Renderer>(); if (!cr || cr.bounds.size.x > 520f || cr.bounds.size.z > 520f) continue;
                Vector3 c = cr.bounds.center - baseCentre[k]; float e = Mathf.Max(Mathf.Abs(c.x) + cr.bounds.extents.x, Mathf.Abs(c.z) + cr.bounds.extents.z); if (e > reach) reach = e;
            }
            baseReach[k] = reach;
            // the roofed buildings of that base (domes, hub, modules): nothing of ours may stand inside one
            if (baseOk[k]) foreach (Transform ch in model.transform)
            {
                if (!ch.name.StartsWith(pre)) continue; var cr = ch.GetComponent<Renderer>(); if (!cr) continue; Vector3 sz = cr.bounds.size;
                if (sz.y < 4f || sz.x > 150f || sz.z > 150f) continue;
                baseRoofs.Add(new Vector4(cr.bounds.center.x, cr.bounds.center.z, Mathf.Max(cr.bounds.extents.x, cr.bounds.extents.z), k));
            }
        }
    }
    readonly List<Vector4> baseRoofs = new List<Vector4>();
    bool UnderRoof(Vector3 g, float margin) { foreach (var b in baseRoofs) { float dx = g.x - b.x, dz = g.z - b.y, r = b.z + margin; if (dx * dx + dz * dz < r * r) return true; } return false; }
    /// open ground beside one of the other bases; falls back to the ground near the main base
    bool BaseSpot(int k, float clear, out Vector3 at)
    {
        at = Vector3.zero; Vector3 g;
        if (k >= 0 && k < 5 && baseOk[k])
            foreach (float dist in new[] { 48f, 60f, 72f, 86f, 100f, 116f, 132f, 150f, 170f, 190f, 212f, 236f })
                for (int i = 0; i < 18; i++)
                {
                    float a = (i * 20f + k * 7f) * Mathf.Deg2Rad, x = baseCentre[k].x + Mathf.Sin(a) * dist, z = baseCentre[k].z + Mathf.Cos(a) * dist;
                    if (!OpenAt(x, z, clear, out g) || !OpenAt(x, z, clear * 0.5f, out g)) continue;
                    if (UnderRoof(g, clear + 2f)) continue;
                    if (Physics.CheckSphere(g + Vector3.up * (clear + 1.2f), clear, ~0, QueryTriggerInteraction.Ignore)) continue;
                    bool near = false; foreach (var u in usedSpots) if ((u - g).sqrMagnitude < (clear + 9f) * (clear + 9f)) { near = true; break; }
                    if (near) continue;
                    usedSpots.Add(g);
                    // a level, raised play floor: nothing built here can sink into the Moon ground or have it poke through
                    float wide = (clear + 3.5f) * 2f; var pf = new GameObject("Play floor").transform; pf.SetParent(funRoot ? funRoot : root, false); pf.position = g;
                    Prim(PrimitiveType.Cylinder, pf, new Vector3(0f, -0.08f, 0f), new Vector3(wide, 0.24f, wide), Lit(new Color(0.5f, 0.53f, 0.6f)));
                    Prim(PrimitiveType.Cylinder, pf, new Vector3(0f, -0.1f, 0f), new Vector3(wide + 0.7f, 0.24f, wide + 0.7f), Lit(new Color(0.83f, 0.62f, 0.2f)));
                    Solid(pf, new Vector3(0f, -0.08f, 0f), new Vector3(wide * 0.72f, 0.48f, wide * 0.72f));
                    at = g + Vector3.up * 0.16f; return true;
                }
        return FunSpot(clear, out at);
    }

    Transform GatePortal(Vector3 at, float yaw, string title, string sub, int index, Color glow)
    {
        var p = new GameObject("Giant portal: " + title).transform; p.SetParent(funRoot, false); p.position = at; p.rotation = Quaternion.Euler(0f, yaw, 0f);
        var stone = Lit(new Color(0.3f, 0.31f, 0.36f)); var gold = Lit(new Color(0.83f, 0.62f, 0.2f)); var gm = Glow(glow * 2.4f);
        Prim(PrimitiveType.Cube, p, new Vector3(0f, 0.3f, 0f), new Vector3(16f, 0.6f, 5f), stone); Solid(p, new Vector3(0f, 0.3f, 0f), new Vector3(16f, 0.6f, 5f));
        // the ring: 28 gold blocks in a circle 14 m across, glowing stones between them
        var ring = new GameObject("Ring").transform; ring.SetParent(p, false); ring.localPosition = new Vector3(0f, 8f, 0f);
        for (int i = 0; i < 28; i++)
        {
            float a = i * Mathf.PI * 2f / 28f; var b = Prim(PrimitiveType.Cube, ring, new Vector3(Mathf.Sin(a) * 7f, Mathf.Cos(a) * 7f, 0f), new Vector3(1.7f, 1.2f, 1.3f), i % 2 == 0 ? gold : stone);
            b.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
            if (i % 2 == 1) Prim(PrimitiveType.Sphere, ring, new Vector3(Mathf.Sin(a) * 7f, Mathf.Cos(a) * 7f, 0.7f), Vector3.one * 0.55f, gm);
        }
        var swirl = new GameObject("Swirl").transform; swirl.SetParent(p, false); swirl.localPosition = new Vector3(0f, 8f, 0f);
        var disc = Prim(PrimitiveType.Cylinder, swirl, Vector3.zero, new Vector3(12.6f, 0.03f, 12.6f), Clear(new Color(glow.r, glow.g, glow.b, 0.42f))); disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        for (int i = 0; i < 9; i++) { float a = i * 40f * Mathf.Deg2Rad, rr = 1.2f + i * 0.55f; Prim(PrimitiveType.Sphere, swirl, new Vector3(Mathf.Sin(a) * rr, Mathf.Cos(a) * rr, 0f), Vector3.one * (0.9f - i * 0.06f), gm); }
        gateSpin.Add(swirl);
        foreach (float sx in new[] { -8.6f, 8.6f }) { Prim(PrimitiveType.Cylinder, p, new Vector3(sx, 5f, 0f), new Vector3(1.4f, 5f, 1.4f), stone); Prim(PrimitiveType.Sphere, p, new Vector3(sx, 10.8f, 0f), Vector3.one * 2f, gm); }
        FacButton(p, new Vector3(0f, 5f, 0f), new Vector3(11f, 9f, 0.6f), Clear(new Color(1f, 1f, 1f, 0.02f)), index);
        var l = Label(p, "<b>" + title + "</b>\n<size=50%>" + sub + "</size>", 0f, 0f, 9f, 60f, 9f, new Color(1f, 0.84f, 0.3f)); l.transform.localPosition = new Vector3(0f, 18.5f, 0f); mapLabels.Add(l);
        return p;
    }

    void BuildMoonGates()
    {
        if (!funRoot) return;
        Color[] hue = { new Color(1f, 0.6f, 0.25f), new Color(0.4f, 0.8f, 1f), new Color(1f, 0.45f, 0.9f), new Color(1f, 0.9f, 0.35f), new Color(0.5f, 1f, 0.55f) };
        for (int k = 0; k < 5; k++)
        {
            if (!baseOk[k]) { funLog += "moonbase " + (k + 2) + " not found in the model; "; continue; }
            Vector3 home, away; if (!FunSpot(10f, out home)) { funLog += "no room for portal " + (k + 1) + " at the main base; "; continue; }
            if (!BaseSpot(k, 10f, out away) || (away - baseCentre[k]).sqrMagnitude > 300f * 300f) { funLog += "no open ground for the portal at " + BaseName[k] + "; "; continue; }
            Vector3 toField = fieldC - home; toField.y = 0f; float hy = toField.sqrMagnitude > 1f ? Mathf.Atan2(toField.x, toField.z) * Mathf.Rad2Deg : 0f;
            Vector3 toBase = baseCentre[k] - away; toBase.y = 0f; float ay = toBase.sqrMagnitude > 1f ? Mathf.Atan2(toBase.x, toBase.z) * Mathf.Rad2Deg : 0f;
            GatePortal(home, hy, "TO " + BaseName[k].ToUpper(), "play " + BaseGame[k] + " there   -   walk in, or click", 1700 + k, hue[k]);
            GatePortal(away, ay, "BACK TO THE MAIN BASE", BaseName[k] + "   -   walk in, or click", 1710 + k, hue[k]);
            gateHome[k] = home; gateAway[k] = away; gateHomeYaw[k] = hy; gateAwayYaw[k] = ay; gateOk[k] = true;
        }
    }
    public void GoBase(int k, bool back)
    {
        if (k < 0 || k > 4 || !gateOk[k] || hunchOn) return;
        game.flyMode = false; gateCool = Time.time + 2.5f;
        Vector3 at = back ? gateHome[k] : gateAway[k]; float yaw = back ? gateHomeYaw[k] : gateAwayYaw[k]; Vector3 fw = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        TeleportTo(at + fw * 9f + Vector3.up * 0.3f, yaw);
        Note(back ? "Back at the main base." : "Welcome to " + BaseName[k] + ". The game here: " + BaseGame[k] + "."); Play(cWin, 0.5f); game.Buzz(0.6f, 0.15f);
        try { Fx2("pack teleporter", "teleport", Feet() + Vector3.up * 0.2f, 0.3f, 0.3f, 1.1f); } catch { }
    }
    void UpdateMoonGates(float dt)
    {
        foreach (var s in gateSpin) if (s) s.Rotate(0f, 0f, 40f * dt, Space.Self);
        if (hunchOn || !Playing || mapNow != 0 || Time.time < gateCool) return;
        Vector3 f = Feet();
        for (int k = 0; k < 5; k++)
        {
            if (!gateOk[k]) continue;
            if (Flat(f - gateHome[k]).sqrMagnitude < 2.2f * 2.2f && Mathf.Abs(f.y - gateHome[k].y) < 4f) { GoBase(k, false); return; }
            if (Flat(f - gateAway[k]).sqrMagnitude < 2.2f * 2.2f && Mathf.Abs(f.y - gateAway[k].y) < 4f) { GoBase(k, true); return; }
        }
    }

    // ================================================================== Star Shower (catch the falling stars)
    Transform showerRoot; TextMeshPro showerLabel; bool showerOk, showerOn; Vector3 showerAt; float showerLeft, showerNext; int showerScore, showerBest;
    class FallStar { public Transform t; public Vector3 vel; public bool live; }
    readonly List<FallStar> fallStars = new List<FallStar>();
    void BuildStarShower(Vector3 g)
    {
        showerOk = true; showerAt = g; var s = new GameObject("Star Shower").transform; s.SetParent(funRoot, false); s.position = g; showerRoot = s;
        var deck = Lit(new Color(0.22f, 0.24f, 0.3f)); var post = Lit(new Color(0.75f, 0.77f, 0.82f));
        Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.1f, 0f), new Vector3(20f, 0.1f, 20f), deck); Solid(s, new Vector3(0f, 0.1f, 0f), new Vector3(14f, 0.2f, 14f));
        Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.21f, 0f), new Vector3(20.4f, 0.02f, 20.4f), Glow(new Color(2.8f, 2.3f, 0.5f))); Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.22f, 0f), new Vector3(19.6f, 0.021f, 19.6f), deck);
        var sign = Label(s, "<b>STAR SHOWER</b>\n<size=55%>Stars fall for 40 seconds. Run under them and catch as many as you can.</size>", 0f, 0f, 5f, 44f, 5f, new Color(1f, 0.84f, 0.3f)); sign.transform.localPosition = new Vector3(0f, 12f, 0f); mapLabels.Add(sign);
        showerLabel = Label(s, "", 0f, 0f, 1.8f, 12f, 1.6f, Color.white); showerLabel.transform.localPosition = new Vector3(0f, 3.2f, 10.6f); mapLabels.Add(showerLabel);
        var ped = new GameObject("Shower start").transform; ped.SetParent(s, false); ped.localPosition = new Vector3(1.6f, 0.2f, 10.6f);
        Prim(PrimitiveType.Cylinder, ped, new Vector3(0f, 0.45f, 0f), new Vector3(0.24f, 0.45f, 0.24f), post, true);
        FacButton(ped, new Vector3(0f, 0.98f, 0f), new Vector3(0.46f, 0.14f, 0.46f), Glow(new Color(0.6f, 2.6f, 0.8f)), 1650);
        var sm = Glow(new Color(3f, 2.4f, 0.5f));
        for (int k = 0; k < 14; k++) { var t = Prim(PrimitiveType.Sphere, funRoot, Vector3.zero, new Vector3(0.7f, 0.7f, 0.24f), sm).transform; t.name = "Falling star"; t.gameObject.SetActive(false); fallStars.Add(new FallStar { t = t }); }
        showerBest = PlayerPrefs.GetInt("shower_best", 0);
    }
    void UpdateStarShower(float dt)
    {
        if (!showerOk) return;
        if (showerLabel) showerLabel.text = showerOn ? "<b>" + showerScore + "</b>   <size=70%>" + Mathf.CeilToInt(showerLeft) + " s</size>" : "<size=70%>press the green button to play\nbest " + showerBest + "</size>";
        if (!showerOn) return;
        Vector3 f = Feet(), head = cam.position;
        if (hunchOn || mapNow != 0 || !Playing || (f - showerAt).sqrMagnitude > 40f * 40f) showerLeft = 0f;
        showerLeft -= dt; showerNext -= dt;
        if (showerNext <= 0f && showerLeft > 1.5f)
        {
            showerNext = Mathf.Lerp(0.8f, 0.35f, 1f - showerLeft / 40f);
            foreach (var s in fallStars) if (!s.live) { s.live = true; s.t.gameObject.SetActive(true); s.t.position = showerAt + new Vector3(R(-8.5f, 8.5f), 20f, R(-8.5f, 8.5f)); s.vel = Vector3.down * R(4.5f, 7f); break; }
        }
        foreach (var s in fallStars)
        {
            if (!s.live) continue; s.t.position += s.vel * dt; s.t.Rotate(0f, 240f * dt, 0f);
            Vector3 d = s.t.position - head; d.y = s.t.position.y - (f.y + 1f);
            if (d.sqrMagnitude < 1.7f * 1.7f) { s.live = false; s.t.gameObject.SetActive(false); showerScore++; Play(cPop, 0.55f); game.Buzz(0.3f, 0.05f); }
            else if (s.t.position.y < showerAt.y + 0.3f) { s.live = false; s.t.gameObject.SetActive(false); }
        }
        if (showerLeft <= 0f)
        {
            showerOn = false; foreach (var s in fallStars) { s.live = false; s.t.gameObject.SetActive(false); }
            bool best = showerScore > showerBest; if (best) { showerBest = showerScore; PlayerPrefs.SetInt("shower_best", showerBest); }
            Note("Star Shower: " + showerScore + " stars" + (best ? "  -  a new best!" : "   (best " + showerBest + ")")); Play(cWin, 0.7f);
        }
    }

    // ================================================================== Target Tower (hit the targets as they light up)
    Transform towerRoot; TextMeshPro towerLabel; bool towerOk, towerOn; Vector3 towerAt; float towerLeft, towerNext; int towerScore, towerBest, towerToolWas;
    class PopTarget { public Transform t; public Renderer r; public float until; }
    readonly List<PopTarget> popTargets = new List<PopTarget>(); Material popOff, popOn;
    void BuildTargetTower(Vector3 g)
    {
        towerOk = true; towerAt = g; var s = new GameObject("Target Tower").transform; s.SetParent(funRoot, false); s.position = g; towerRoot = s;
        Vector3 toField = fieldC - g; toField.y = 0f; if (toField.sqrMagnitude < 1f) toField = Vector3.forward; s.rotation = Quaternion.LookRotation(-toField.normalized, Vector3.up);
        var deck = Lit(new Color(0.22f, 0.24f, 0.3f)); var post = Lit(new Color(0.75f, 0.77f, 0.82f)); popOff = Lit(new Color(0.25f, 0.27f, 0.32f)); popOn = Glow(new Color(0.6f, 2.8f, 0.9f));
        Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.1f, 0f), new Vector3(5f, 0.1f, 5f), deck); Solid(s, new Vector3(0f, 0.1f, 0f), new Vector3(3.5f, 0.2f, 3.5f));
        Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.21f, 0f), new Vector3(5.3f, 0.02f, 5.3f), Glow(new Color(0.6f, 2.6f, 0.9f))); Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.22f, 0f), new Vector3(4.6f, 0.021f, 4.6f), deck);
        Prim(PrimitiveType.Cube, s, new Vector3(0f, 5f, 16f), new Vector3(1.2f, 10f, 1.2f), post, true);
        for (int k = 0; k < 12; k++)
        {
            float a = (-55f + (k % 6) * 22f) * Mathf.Deg2Rad; float y = 2.2f + (k / 6) * 3.4f + (k % 2) * 0.8f;
            var t = Prim(PrimitiveType.Cylinder, s, new Vector3(Mathf.Sin(a) * 15f, y, Mathf.Cos(a) * 15f), new Vector3(1.5f, 0.08f, 1.5f), popOff, false).transform; t.localRotation = Quaternion.LookRotation(Vector3.up, -new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)));
            var col = t.gameObject.AddComponent<SphereCollider>(); col.radius = 0.6f; var tg = t.gameObject.AddComponent<HunchTarget>(); tg.kind = 37; tg.index = 100 + k;
            Prim(PrimitiveType.Cylinder, s, new Vector3(Mathf.Sin(a) * 15.2f, y * 0.5f, Mathf.Cos(a) * 15.2f), new Vector3(0.1f, y * 0.5f, 0.1f), post);
            popTargets.Add(new PopTarget { t = t, r = t.GetComponent<Renderer>() });
        }
        var sign = Label(s, "<b>TARGET TOWER</b>\n<size=55%>Targets light up green for a moment. Hit them with any tool before they go dark. 40 seconds.</size>", 0f, 0f, 5f, 44f, 5f, new Color(1f, 0.84f, 0.3f)); sign.transform.localPosition = new Vector3(0f, 12.5f, 16f); mapLabels.Add(sign);
        towerLabel = Label(s, "", 0f, 0f, 1.8f, 12f, 1.6f, Color.white); towerLabel.transform.localPosition = new Vector3(0f, 2.9f, 5f); mapLabels.Add(towerLabel);
        var ped = new GameObject("Tower start").transform; ped.SetParent(s, false); ped.localPosition = new Vector3(1.9f, 0.2f, 1.2f);
        Prim(PrimitiveType.Cylinder, ped, new Vector3(0f, 0.45f, 0f), new Vector3(0.24f, 0.45f, 0.24f), post, true);
        FacButton(ped, new Vector3(0f, 0.98f, 0f), new Vector3(0.46f, 0.14f, 0.46f), Glow(new Color(0.6f, 2.6f, 0.8f)), 1660);
        towerBest = PlayerPrefs.GetInt("tower_best", 0);
    }
    void TargetHit(int i)
    {
        if (!towerOn || i < 0 || i >= popTargets.Count) return; var p = popTargets[i]; if (Time.time > p.until) return;
        p.until = 0f; if (p.r) p.r.sharedMaterial = popOff; towerScore++; Play(cPop, 0.55f); game.Buzz(0.35f, 0.05f);
        try { Fx2("sparks explode yellow", "electro hit", p.t.position, 0.5f, 0.5f, 1f); } catch { }
    }
    void UpdateTargetTower(float dt)
    {
        if (!towerOk) return;
        if (towerLabel) towerLabel.text = towerOn ? "<b>" + towerScore + "</b>   <size=70%>" + Mathf.CeilToInt(towerLeft) + " s</size>" : "<size=70%>press the green button to play\nbest " + towerBest + "</size>";
        if (!towerOn) return;
        if (hunchOn || mapNow != 0 || !Playing || (Feet() - towerAt).sqrMagnitude > 30f * 30f) towerLeft = 0f;
        towerLeft -= dt; towerNext -= dt;
        foreach (var p in popTargets) if (p.until > 0f && Time.time > p.until) { p.until = 0f; if (p.r) p.r.sharedMaterial = popOff; }
        if (towerNext <= 0f && towerLeft > 1f)
        {
            towerNext = Mathf.Lerp(1.1f, 0.55f, 1f - towerLeft / 40f); var p = popTargets[rng.Next(popTargets.Count)];
            if (p.until <= 0f) { p.until = Time.time + Mathf.Lerp(2.4f, 1.5f, 1f - towerLeft / 40f); if (p.r) p.r.sharedMaterial = popOn; }
        }
        if (towerLeft <= 0f)
        {
            towerOn = false; foreach (var p in popTargets) { p.until = 0f; if (p.r) p.r.sharedMaterial = popOff; }
            if (tool == Zapper && towerToolWas != Zapper) tool = towerToolWas;
            bool best = towerScore > towerBest; if (best) { towerBest = towerScore; PlayerPrefs.SetInt("tower_best", towerBest); }
            Note("Target Tower: " + towerScore + " hits" + (best ? "  -  a new best!" : "   (best " + towerBest + ")")); Play(cWin, 0.7f);
        }
    }

    // ------------------------------------------------------------------ shared by the buttons of this file
    string MoonText(int i)
    {
        if (i == 1650) return showerOn ? "Star Shower is running" : "start Star Shower (40 seconds)   best " + showerBest;
        if (i == 1670) return BlastText();
        if (i == 1660) return towerOn ? "Target Tower is running" : "start Target Tower (40 seconds)   best " + towerBest;
        if (i >= 1700 && i < 1705) return "step through to " + BaseName[i - 1700] + " (" + BaseGame[i - 1700] + ")";
        if (i >= 1710 && i < 1715) return "step through, back to the main base";
        return "";
    }
    bool MoonUse(int i)
    {
        if (i == 1650) { if (!showerOn && showerOk) { showerOn = true; showerLeft = 40f; showerNext = 0.5f; showerScore = 0; Note("Star Shower! Run under the falling stars."); Play(cWin, 0.5f); } return true; }
        if (i == 1660) { if (!towerOn && towerOk) { towerOn = true; towerLeft = 40f; towerNext = 0.6f; towerScore = 0; towerToolWas = tool; if (tool == Hands || tool == Drill || tool == Builder || tool == Sword) tool = Zapper; Note("Target Tower! Hit the green targets."); Play(cWin, 0.5f); } return true; }
        if (i == 1670) { BlastToggle(); return true; }
        if (i >= 1700 && i < 1705) { GoBase(i - 1700, false); return true; }
        if (i >= 1710 && i < 1715) { GoBase(i - 1710, true); return true; }
        return false;
    }
    void UpdateMoonbases(float dt) { UpdateFloorGuard(); UpdateCrewAi(); if (!funRoot) return; UpdateMoonGates(dt); UpdateStarShower(dt); UpdateTargetTower(dt); }

    public string ProbeMoon() { var sb = new System.Text.StringBuilder(); for (int k = 0; k < 5; k++) sb.Append(BaseName[k]).Append(baseOk[k] ? " base" + baseCentre[k].ToString("F0") + " reach " + baseReach[k].ToString("F0") : " (no base)").Append(gateOk[k] ? " gates " + gateHome[k].ToString("F0") + ">" + gateAway[k].ToString("F0") : " (no gates)").Append(" | "); return sb + "shower " + showerOk + showerAt.ToString("F0") + " tower " + towerOk + towerAt.ToString("F0") + " | saves " + safeSaves + " | " + funLog; }
    public string ProbeGoBase() { GoBase(Mathf.Clamp(probeInt, 0, 4), probeY > 0.5f); return "at " + Feet().ToString("F0"); }
    public string ProbeGateView() { int k = Mathf.Clamp(probeInt, 0, 4); if (!gateOk[k]) return "no gate " + k; game.flyMode = true; Vector3 fw = Quaternion.Euler(0f, gateHomeYaw[k], 0f) * Vector3.forward; TeleportTo(gateHome[k] + fw * 30f + Vector3.up * 9f, gateHomeYaw[k] + 180f); if (desk) desk.SetLook(gateHomeYaw[k] + 180f, 4f); return "looking at portal " + k; }
    public string ProbeShower() { MoonUse(1650); return "shower " + showerOn; }
    public string ProbeTower() { MoonUse(1660); return "tower " + towerOn; }
    public string ProbeTowerHit() { int n = 0; for (int i = 0; i < popTargets.Count; i++) if (popTargets[i].until > 0f) { TargetHit(i); n++; } return "hit " + n + " score " + towerScore; }
}
