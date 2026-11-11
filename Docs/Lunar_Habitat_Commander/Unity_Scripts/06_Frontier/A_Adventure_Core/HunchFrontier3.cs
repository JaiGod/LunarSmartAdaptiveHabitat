// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/A_Adventure_Core/HunchFrontier3.cs
//  Section : 06_Frontier/A_Adventure_Core
//  Folder  : Moon Frontier adventure layer: mining, aliens, tools, easy mode, survival + repair
//            mini-games, Autopilot, the 3D models from the Drive collection.
//  Purpose : Third part of Moon Frontier: - Survival: the player's health and hunger, food packs,
//            alien bumps that hurt, respawn at the airlock
//  Contains: HunchFrontier, Station, Helper, SkyRock
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// Third part of Moon Frontier:
///  - Survival: the player's health and hunger, food packs, alien bumps that hurt, respawn at the airlock
///    (Creative mode = Free explore: no hunger, no damage, unlimited blocks, free upgrades)
///  - Repair mini-games on five task stations: join the wires, lock the vent on the green bar three times,
///    press every health-monitor button, flip every light switch up, repeat the computer's colour code
///  - A simple screen that shows only what matters (U switches to the full dashboard)
///  - Third-person view with an astronaut you can see, and a dance
///  - Helper rovers you can call: food, medic, repair
///  - Sky: a relay satellite linking the base to Earth, orbital cannons that shoot down meteorites,
///    and passing events: dust storm, solar flare, meteor shower
public partial class HunchFrontier
{
    // ------------------------------------------------------------------ survival
    public float health = 100f, hunger = 100f; public int food = 3, respawns, ate;
    public bool simpleHud = true;
    bool Creative { get { return sim.timeFrozen; } }
    bool hungryWarned, hurtWarned; float hurtFree; string bannerHead = "YOU DID IT!"; bool bannerWarn, bannerAch; float creativeGift;

    void Banner(string head, string title, bool warn)
    {
        bannerHead = head; bannerTitle = title; bannerWarn = warn; bannerAch = false; bannerUntil = Time.unscaledTime + 4.5f;
        if (warn) { Play(cBoop, 0.9f); game.Chime(0.5f); game.Buzz(1f, 0.4f); } else { Play(cWin, 0.8f); game.Buzz(0.9f, 0.3f); }
    }

    public void Hurt(float amount, string why)
    {
        if (Creative || !Playing) return;
        if (why != null) { if (Time.time < hurtFree) return; hurtFree = Time.time + 3f; }      // a bump cannot be followed by another straight away
        health = Mathf.Max(0f, health - amount);
        if (why != null) Note(why + "  Health " + Mathf.RoundToInt(health));
        game.Buzz(0.9f, 0.2f);
    }

    public void Eat()
    {
        if (food <= 0) { Note("No food packs left. Do a job at the Galley or Greenhouse, or call the food rover."); return; }
        if (hunger > 92f && health > 95f) { Note("You are not hungry yet."); return; }
        food--; ate++; hunger = Mathf.Min(100f, hunger + 40f); health = Mathf.Min(100f, health + 8f); hungryWarned = false;
        Play(cPick, 0.7f); Fx2("sparkle_ellow", "star hit", cam.position + cam.forward * 1.6f, 0.5f, 0.3f, 0.8f);
        Note("Yum! Hunger " + Mathf.RoundToInt(hunger) + "   Food packs left: " + food);
    }

    void UpdateSurvival(float dt)
    {
        if (!Playing || game.Paused) return;
        if (Creative)
        {
            health = 100f; hunger = 100f;
            creativeGift -= dt; if (creativeGift <= 0f) { creativeGift = 2f; if (bag[Block] < 32) bag[Block] = 64; if (food < 3) food = 3; }
            return;
        }
        hunger = Mathf.Max(0f, hunger - dt * (100f / 900f));      // a full stomach lasts fifteen minutes
        if (hunger <= 0f) health = Mathf.Max(0f, health - dt * 1.2f);
        else if (hunger > 35f && health < 100f) health = Mathf.Min(100f, health + dt * 1.5f);
        if (hunger < 30f && food > 0 && smart) { Eat(); Note("Easy mode ate a food pack for you. Hunger " + Mathf.RoundToInt(hunger)); }
        if (hunger < 25f && !hungryWarned) { hungryWarned = true; Banner("WARNING", food > 0 ? "You are hungry - eat a food pack" : "You are hungry and out of food", true); sim.Say("ARIA: You are getting hungry, Commander. " + (game.HeadsetActive ? "Point at EAT on the dashboard." : "Press X to eat a food pack.") + " The Galley, the Greenhouse and the food rover give more."); }
        if (health < 30f && !hurtWarned) { hurtWarned = true; Banner("WARNING", "Health is low - call the medic rover", true); if (smart) CallHelper(1); }
        if (health > 60f) hurtWarned = false;
        if (health <= 0f)
        {
            respawns++; health = 100f; hunger = Mathf.Max(hunger, 60f); hurtWarned = false;
            if (ride != null) Dismount(null);
            int ai = game.ZoneIndex("airlock"); game.GoTo(ai >= 0 ? ai : 0);
            Banner("TRY AGAIN!", "You were brought back to the airlock", true);
            sim.Say("ARIA: You ran out of energy, so I carried you back to the airlock and patched you up. You kept everything in your backpack.");
        }
    }

    void OnCareFood(string zone)
    {
        if (zone != "galley" && zone != "greenhouse") return;
        if (food < 9) { food++; Note("+1 food pack from the " + (zone == "galley" ? "Galley" : "Greenhouse") + "  (" + food + ")"); }
    }

    // ------------------------------------------------------------------ task stations: repair mini-games
    class Station
    {
        public int type, index; public string name, zoneId, breakText; public Transform root, board; public Renderer lamp; public TextMeshPro status;
        public bool broken, practice; public int step, sel = -1; public float t;
        public readonly List<Renderer> els = new List<Renderer>(); public readonly List<Transform> extra = new List<Transform>(); public readonly List<LineRenderer> lines = new List<LineRenderer>();
        public int[] order; public bool[] on; public float[] flash;
        public float markerX, markerDir = 1f, greenX, speed = 0.8f; public Transform marker, green;      // vent bar
        public int[] seq; public int showAt; public float showT; public bool showing;                      // computer colour code
    }
    readonly List<Station> stations = new List<Station>(); Transform stationRoot; Material stOk, stBad, stDim, stLit, stWhite, stBoard; Material[] colDim, colLit; float breakIn = 75f; int fixedCount, fixedMask;
    static readonly string[] ColName = { "red", "blue", "yellow", "green" };

    TextMeshPro Label(Transform parent, string text, float x, float y, float size, float w, float h, Color colour)
    {
        var go = new GameObject("Text"); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(x, y, -0.008f);
        var t = go.AddComponent<TextMeshPro>(); if (game.hud) t.font = game.hud.font;
        t.text = text; t.fontSize = size; t.color = colour; t.alignment = TextAlignmentOptions.Center; t.richText = true; t.rectTransform.sizeDelta = new Vector2(w, h);
        return t;
    }
    Renderer El(Station st, PrimitiveType type, float x, float y, float sx, float sy, float sz, Material m, int element)
    {
        var g = Prim(type, st.board, new Vector3(x, y, -0.02f), new Vector3(sx, sy, sz), m); g.name = "Part " + element;
        var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(1.25f, 1.25f, 1.6f);
        var tg = g.AddComponent<HunchTarget>(); tg.kind = 20; tg.index = st.index * 100 + element;
        var r = g.GetComponent<Renderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; st.els.Add(r); return r;
    }

    List<Bounds> clutter; string placeLog = "";
    static readonly float[] StAng = { -62f, -100f, -140f, 180f, 140f, 100f, -25f, 0f }, StDist = { 1.75f, 1.45f, 1.2f };
    int Clutter(Vector3 centre, Vector3 pos, Quaternion rot)
    {
        if (clutter == null)
        {
            clutter = new List<Bounds>();
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.transform.IsChildOf(root) || r.GetComponent<TMP_Text>()) continue;
                var b = r.bounds; if (b.size.x > 7f || b.size.z > 7f || b.size.y > 6f || b.size.y < 0.12f) continue;      // not hulls, floors or decals
                clutter.Add(b);
            }
        }
        int n = 0;
        foreach (var c in Physics.OverlapBox(pos + Vector3.up * 1.15f, new Vector3(0.72f, 0.85f, 0.22f), rot, ~0, QueryTriggerInteraction.Ignore)) if (!(c is CharacterController) && !c.transform.IsChildOf(origin.transform)) n += 2;
        var box = new Bounds(pos + Vector3.up * 1.2f, new Vector3(1.0f, 1.5f, 1.0f));
        foreach (var cb in clutter) if (cb.Intersects(box)) n++;
        for (int k = 2; k <= 3; k++) { var s = new Bounds(Vector3.Lerp(centre, pos, k / 4f) + Vector3.up * 1.3f, Vector3.one * 0.4f); foreach (var cb in clutter) if (cb.Intersects(s)) n++; }
        RaycastHit h; Vector3 right = rot * Vector3.right;
        foreach (float hy in new[] { 0.5f, 1.3f, 1.9f })
            foreach (float sx in new[] { -0.6f, 0f, 0.6f })
            {
                Vector3 src = centre + Vector3.up * hy, to = pos + right * sx + Vector3.up * hy - src;
                if (Cast(new Ray(src, to.normalized), out h, to.magnitude + 0.3f)) n += 2;
            }
        return n;
    }

    Station NewStation(int type, string name, string zoneId, string fallbackZone, string breakText)
    {
        // stand the panel where it is not inside furniture and can be seen from the zone ring; try the fall-back zones if the first is too cramped
        int zi = -1, best = int.MaxValue; Vector3 dir = Vector3.forward; float away = 1.75f, bestA = -62f;
        foreach (var id in (zoneId + "," + fallbackZone).Split(','))
        {
            int k = game.ZoneIndex(id); if (k < 0) continue; var zz = game.zones[k];
            foreach (float d in StDist)
            {
                foreach (float a in StAng)
                {
                    Vector3 dd = Quaternion.Euler(0f, zz.yaw + a, 0f) * Vector3.forward; int n = Clutter(zz.position, zz.position + dd * d, Quaternion.LookRotation(dd, Vector3.up));
                    if (n < best) { best = n; dir = dd; away = d; bestA = a; zi = k; }
                    if (best == 0) break;
                }
                if (best == 0) break;
            }
            if (best == 0) break;
        }
        if (zi < 0) return null;
        var z = game.zones[zi];
        placeLog += " " + name + "@" + z.id + ":" + bestA + "/" + away + "/" + best;
        var st = new Station { type = type, name = name, zoneId = z.id, breakText = breakText, index = stations.Count };
        st.root = new GameObject("Task station: " + name).transform; st.root.SetParent(stationRoot, false);
        st.root.position = z.position + dir * away; st.root.rotation = Quaternion.LookRotation(dir, Vector3.up);
        var post = Lit(new Color(0.3f, 0.32f, 0.36f)); var gold = Lit(new Color(0.95f, 0.74f, 0.22f));
        Prim(PrimitiveType.Cube, st.root, new Vector3(-0.55f, 0.45f, 0.03f), new Vector3(0.06f, 0.9f, 0.06f), post); Prim(PrimitiveType.Cube, st.root, new Vector3(0.55f, 0.45f, 0.03f), new Vector3(0.06f, 0.9f, 0.06f), post);
        st.board = new GameObject("Board").transform; st.board.SetParent(st.root, false); st.board.localPosition = new Vector3(0f, 1.32f, 0f); st.board.localRotation = Quaternion.Euler(8f, 0f, 0f);
        Prim(PrimitiveType.Cube, st.board, new Vector3(0f, 0f, 0.03f), new Vector3(1.36f, 0.9f, 0.05f), stBoard);
        Prim(PrimitiveType.Cube, st.board, new Vector3(0f, 0.46f, 0.02f), new Vector3(1.4f, 0.025f, 0.08f), gold); Prim(PrimitiveType.Cube, st.board, new Vector3(0f, -0.46f, 0.02f), new Vector3(1.4f, 0.025f, 0.08f), gold);
        Label(st.board, "<b>" + name.ToUpper() + "</b>", -0.05f, 0.385f, 0.85f, 1.15f, 0.11f, new Color(1f, 0.84f, 0.3f));
        st.status = Label(st.board, "", 0f, -0.39f, 0.5f, 1.3f, 0.1f, Color.white);
        st.lamp = Prim(PrimitiveType.Sphere, st.board, new Vector3(0.6f, 0.385f, -0.01f), Vector3.one * 0.07f, stOk).GetComponent<Renderer>();
        stations.Add(st); return st;
    }

    void BuildStations()
    {
        stationRoot = new GameObject("Task stations").transform; stationRoot.SetParent(root, false);
        stOk = Glow(new Color(0.15f, 1.5f, 0.35f)); stBad = Glow(new Color(1.7f, 0.12f, 0.1f)); stDim = Lit(new Color(0.22f, 0.23f, 0.26f)); stLit = Glow(new Color(0.15f, 1.45f, 0.4f)); stWhite = Glow(new Color(1.5f, 1.5f, 1.55f)); stBoard = Lit(new Color(0.07f, 0.09f, 0.14f));
        Color[] c = { new Color(1f, 0.1f, 0.08f), new Color(0.1f, 0.3f, 1f), new Color(1f, 0.8f, 0.04f), new Color(0.08f, 0.8f, 0.2f) };
        colDim = new Material[4]; colLit = new Material[4];
        for (int i = 0; i < 4; i++) { colDim[i] = Glow(c[i] * 0.3f); colLit[i] = Glow(c[i] * 1.2f); }

        // 1. wiring
        var w = NewStation(0, "Wiring", "power", "storage", "A power cable has come loose");
        if (w != null)
        {
            w.on = new bool[4]; w.order = new[] { 0, 1, 2, 3 };
            for (int i = 0; i < 4; i++) El(w, PrimitiveType.Sphere, -0.5f, 0.22f - 0.15f * i, 0.1f, 0.1f, 0.1f, colLit[i], i);
            for (int i = 0; i < 4; i++) El(w, PrimitiveType.Cube, 0.5f, 0.22f - 0.15f * i, 0.11f, 0.11f, 0.06f, colLit[i], 10 + i);
            for (int i = 0; i < 4; i++)
            {
                var lr = Line("Wire", colLit[i], 0.03f, w.board); lr.useWorldSpace = false; lr.positionCount = 3; lr.enabled = false; w.lines.Add(lr);
            }
        }
        // 2. ventilation: stop the marker on the green part, three times
        var v = NewStation(1, "Ventilation", "eclss", "hygiene", "A vent cover is loose");
        if (v != null)
        {
            Prim(PrimitiveType.Cube, v.board, new Vector3(0f, 0.1f, -0.005f), new Vector3(1.04f, 0.1f, 0.02f), stDim);
            v.green = Prim(PrimitiveType.Cube, v.board, new Vector3(0f, 0.1f, -0.012f), new Vector3(0.17f, 0.1f, 0.02f), stLit).transform;
            v.marker = Prim(PrimitiveType.Cube, v.board, new Vector3(0f, 0.1f, -0.022f), new Vector3(0.026f, 0.17f, 0.02f), stWhite).transform;
            El(v, PrimitiveType.Cube, 0f, -0.2f, 0.42f, 0.17f, 0.05f, Glow(new Color(1.5f, 0.85f, 0.1f)), 0);
            Label(v.board, "<b>LOCK</b>", 0f, -0.2f, 0.7f, 0.4f, 0.15f, Color.black).transform.localPosition = new Vector3(0f, -0.2f, -0.05f);
            for (int i = 0; i < 3; i++) v.extra.Add(Prim(PrimitiveType.Sphere, v.board, new Vector3(-0.1f + 0.1f * i, -0.04f, -0.01f), Vector3.one * 0.06f, stDim).transform);
        }
        // 3. health monitor: press every button
        var h = NewStation(2, "Health monitor", "medical", "quarters", "The crew health monitor has reset");
        if (h != null)
        {
            h.on = new bool[9];
            for (int i = 0; i < 9; i++) El(h, PrimitiveType.Cube, -0.22f + 0.22f * (i % 3), 0.2f - 0.19f * (i / 3), 0.15f, 0.13f, 0.05f, colDim[0], i);
        }
        // 4. lights: flip every switch up
        var l = NewStation(3, "Light switches", "work", "galley,quarters,storage,hygiene,airlock", "The lighting breakers have tripped");
        if (l != null)
        {
            l.on = new bool[5];
            for (int i = 0; i < 5; i++)
            {
                float x = -0.44f + 0.22f * i;
                Prim(PrimitiveType.Cube, l.board, new Vector3(x, 0f, -0.005f), new Vector3(0.12f, 0.3f, 0.02f), stDim);
                El(l, PrimitiveType.Cube, x, 0.06f, 0.06f, 0.14f, 0.07f, stWhite, i);
                l.extra.Add(Prim(PrimitiveType.Sphere, l.board, new Vector3(x, 0.22f, -0.01f), Vector3.one * 0.05f, stOk).transform);
            }
        }
        // 5. computer: watch the colours, then press them in the same order
        var cst = NewStation(4, "Computer", "control", "work", "The mission computer has shut down");
        if (cst != null)
        {
            cst.flash = new float[4]; cst.seq = new[] { 0, 1, 2, 3 };
            for (int i = 0; i < 4; i++) El(cst, PrimitiveType.Cube, -0.14f + 0.28f * (i % 2), 0.16f - 0.26f * (i / 2), 0.24f, 0.22f, 0.05f, colDim[i], i);
        }
        foreach (var st in stations) { ResetStation(st); st.broken = false; JoinAll(st); ShowStation(st); }
        A("Electrician", "Fix the Wiring station.", () => (fixedMask & 1) != 0);
        A("Air mechanic", "Fix the Ventilation station.", () => (fixedMask & 2) != 0);
        A("Health checker", "Fix the Health monitor station.", () => (fixedMask & 4) != 0);
        A("Bright spark", "Fix the Light switches station.", () => (fixedMask & 8) != 0);
        A("Computer whiz", "Fix the Computer station.", () => (fixedMask & 16) != 0);
        A("Snack time", "Eat a food pack.", () => ate >= 1);
        A("Moon dancer", "Do the astronaut dance.", () => danced);
        A("Rover helper", "Call a helper rover.", () => helpersCalled >= 1);
    }

    void ResetStation(Station st)
    {
        st.step = 0; st.sel = -1; st.t = 0f;
        switch (st.type)
        {
            case 0:
                for (int i = 3; i > 0; i--) { int j = rng.Next(i + 1); int tmp = st.order[i]; st.order[i] = st.order[j]; st.order[j] = tmp; }
                if (st.order[0] == 0 && st.order[1] == 1 && st.order[2] == 2) { st.order[0] = 1; st.order[1] = 0; }
                for (int i = 0; i < 4; i++) { st.on[i] = false; st.lines[i].enabled = false; st.els[4 + i].sharedMaterial = colLit[st.order[i]]; st.els[i].transform.localScale = Vector3.one * 0.1f; }
                break;
            case 1: st.speed = 0.8f; st.greenX = R(-0.38f, 0.38f); st.markerX = -0.5f; foreach (var b in st.extra) b.GetComponent<Renderer>().sharedMaterial = stDim; break;
            case 2: for (int i = 0; i < 9; i++) st.on[i] = false; break;
            case 3: { int off = 0; for (int i = 0; i < 5; i++) { st.on[i] = rng.Next(3) == 0; if (!st.on[i]) off++; } if (off < 2) { st.on[1] = false; st.on[3] = false; } break; }
            case 4: for (int i = 0; i < 4; i++) st.seq[i] = rng.Next(4); st.showing = true; st.showAt = 0; st.showT = 1.0f; break;
        }
    }

    void ShowStation(Station st)
    {
        if (st.lamp) st.lamp.sharedMaterial = st.broken ? stBad : stOk;
        string s = "";
        switch (st.type)
        {
            case 0:
                if (st.sel >= 0) st.els[st.sel].transform.localScale = Vector3.one * 0.14f;
                s = !st.broken ? "All cables joined" : (st.sel >= 0 ? "Now pick the <b>" + ColName[st.sel] + "</b> socket on the right" : "Pick a wire on the left, then its matching socket");
                break;
            case 1:
                if (st.green) st.green.localPosition = new Vector3(st.greenX, 0.1f, -0.012f);
                for (int i = 0; i < st.extra.Count; i++) st.extra[i].GetComponent<Renderer>().sharedMaterial = !st.broken || i < st.step ? stLit : stDim;
                s = !st.broken ? "Vent cover locked" : "LOCK when the marker is on green.  " + st.step + " of 3";
                break;
            case 2:
                { int n = 0; for (int i = 0; i < 9; i++) { bool on = !st.broken || st.on[i]; if (on) n++; st.els[i].sharedMaterial = on ? stLit : colDim[0]; } s = !st.broken ? "All sensors online" : "Press every red button.  " + n + " of 9"; }
                break;
            case 3:
                { int n = 0; for (int i = 0; i < 5; i++) { bool on = !st.broken || st.on[i]; if (on) n++; st.els[i].transform.localPosition = new Vector3(-0.44f + 0.22f * i, on ? 0.06f : -0.06f, -0.02f); st.extra[i].GetComponent<Renderer>().sharedMaterial = on ? stOk : stBad; } s = !st.broken ? "All lights on" : "Flip every switch up.  " + n + " of 5"; }
                break;
            case 4:
                s = !st.broken ? "Computer online" : (st.showing ? "Watch the colours..." : "Your turn: press them in the same order.  " + st.step + " of " + st.seq.Length);
                break;
        }
        if (st.status && st.status.text != s) st.status.text = s;
    }

    string StationPrompt(int code)
    {
        int si = code / 100, el = code % 100; if (si < 0 || si >= stations.Count) return "";
        var st = stations[si];
        if (!st.broken) return "practise the " + st.name + " repair";
        switch (st.type)
        {
            case 0: return el < 10 ? "pick up the " + ColName[el] + " wire" : (st.sel >= 0 ? "plug it into the " + ColName[st.order[el - 10]] + " socket" : ColName[st.order[el - 10]] + " socket (pick a wire first)");
            case 1: return "LOCK the vent cover";
            case 2: return "press the button";
            case 3: return "flip the switch";
            default: return "press " + ColName[el % 4];
        }
    }

    void StationPress(int code)
    {
        int si = code / 100, el = code % 100; if (si < 0 || si >= stations.Count) return;
        var st = stations[si]; Play(cThud, 0.6f); game.Buzz(0.4f, 0.05f);
        if (!st.broken) { st.broken = true; st.practice = true; ResetStation(st); ShowStation(st); Note("Practice: " + st.name + ". Nothing is really broken."); return; }
        switch (st.type)
        {
            case 0:
                if (el < 10) { if (st.on[el]) break; if (st.sel >= 0) st.els[st.sel].transform.localScale = Vector3.one * 0.1f; st.sel = el; }
                else if (st.sel < 0) Note("Pick a wire on the left first.");
                else
                {
                    int j = el - 10;
                    if (st.order[j] == st.sel)
                    {
                        st.on[st.sel] = true; var lr = st.lines[st.sel]; Vector3 a = st.els[st.sel].transform.localPosition, b = st.els[4 + j].transform.localPosition;
                        lr.SetPosition(0, a); lr.SetPosition(1, (a + b) * 0.5f + new Vector3(0f, -0.07f, -0.03f)); lr.SetPosition(2, b); lr.enabled = true;
                        st.els[st.sel].transform.localScale = Vector3.one * 0.1f; st.sel = -1; Play(cPick, 0.6f); HunchFx.Spawn("electro hit", st.els[4 + j].transform.position, 0.2f, 0.5f);
                        if (st.on[0] && st.on[1] && st.on[2] && st.on[3]) { FixStation(st); return; }
                    }
                    else { Play(cBoop, 0.6f); Note("That socket is " + ColName[st.order[j]] + ". Find the " + ColName[st.sel] + " one."); st.els[st.sel].transform.localScale = Vector3.one * 0.1f; st.sel = -1; }
                }
                break;
            case 1:
                if (Mathf.Abs(st.markerX - st.greenX) <= 0.1f)
                {
                    st.step++; st.speed += 0.22f; st.greenX = R(-0.38f, 0.38f); Play(cPick, 0.7f);
                    if (st.step >= 3) { FixStation(st); return; }
                }
                else { Play(cBoop, 0.6f); Note("Missed. Wait until the white marker is on the green part."); }
                break;
            case 2:
                st.on[el % 9] = true;
                { bool all = true; foreach (bool b in st.on) if (!b) all = false; if (all) { FixStation(st); return; } }
                break;
            case 3:
                st.on[el % 5] = !st.on[el % 5];
                { bool all = true; foreach (bool b in st.on) if (!b) all = false; if (all) { FixStation(st); return; } }
                break;
            case 4:
                if (st.showing) { Note("Watch the colours first."); break; }
                st.flash[el % 4] = 0.3f;
                if (el % 4 == st.seq[st.step]) { st.step++; Play(cPick, 0.5f + 0.1f * st.step); if (st.step >= st.seq.Length) { FixStation(st); return; } }
                else { Play(cBoop, 0.6f); Note("Not quite. Watch the colours again."); st.step = 0; st.showing = true; st.showAt = 0; st.showT = 0.9f; }
                break;
        }
        ShowStation(st);
    }

    void BreakStation(Station st)
    {
        st.broken = true; st.practice = false; ResetStation(st); ShowStation(st);
        if (st.type == 3) game.lightSwitch = 0.04f;
        if (st.type == 1) for (int i = 0; i < vents.Count; i++) SetZoneFan(i, false, false);
        if (st.type == 4) { var gd = GetComponent<HunchGuide>(); if (gd) gd.mapOn = false; }
        int zi = game.ZoneIndex(st.zoneId);
        Banner("WARNING", st.breakText + " - go to " + (zi >= 0 ? game.zones[zi].title : st.zoneId), true);
        sim.Say("ARIA: " + st.breakText + ". Go to " + (zi >= 0 ? game.zones[zi].title : st.zoneId) + " and fix the " + st.name + " panel, or call the repair rover.");
        try { var gd = GetComponent<HunchGuide>(); if (gd && zi >= 0 && st.type != 4) gd.SetWaypoint(zi); } catch { }
    }

    void JoinAll(Station st)
    {
        if (st.type != 0) return;
        for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) if (st.order[j] == i)
        {
            var lr = st.lines[i]; Vector3 a = st.els[i].transform.localPosition, b = st.els[4 + j].transform.localPosition;
            lr.SetPosition(0, a); lr.SetPosition(1, (a + b) * 0.5f + new Vector3(0f, -0.07f, -0.03f)); lr.SetPosition(2, b); lr.enabled = true; st.on[i] = true;
        }
    }

    void FixStation(Station st)
    {
        bool real = !st.practice; st.broken = false; st.practice = false; JoinAll(st); ShowStation(st);
        if (st.type == 3) game.lightSwitch = lightsOn ? 1f : 0.04f;
        if (st.type == 1) { ventOn = true; for (int i = 0; i < vents.Count; i++) SetZoneFan(i, true, false); }
        if (st.type == 4) { var gd = GetComponent<HunchGuide>(); if (gd) gd.mapOn = true; }
        fixedCount++; fixedMask |= 1 << st.type;
        Banner("YOU DID IT!", (real ? "Fixed: " : "Practice done: ") + st.name, false);
        Fx2("confetti_blast_multicolor", "sparks explode yellow", st.board.position - st.root.forward * 0.6f + Vector3.up * 0.3f, 0.3f, 0.6f, 1.2f);
        if (real) sim.Say("ARIA: " + st.name + " fixed. Thank you, Commander.");
    }

    Station BrokenStation() { foreach (var st in stations) if (st.broken && !st.practice) return st; return null; }

    void UpdateStations(float dt)
    {
        float blink = Mathf.Sin(Time.time * 7f); bool busy = false;
        foreach (var st in stations)
        {
            if (st.broken && (st.board.position - cam.position).sqrMagnitude < 3.4f * 3.4f) busy = true;
            if (st.broken && st.lamp) st.lamp.transform.localScale = Vector3.one * (0.07f + 0.025f * blink);
            if (st.type == 1 && st.marker)
            {
                if (st.broken) { st.markerX += st.markerDir * st.speed * dt; if (st.markerX > 0.5f) { st.markerX = 0.5f; st.markerDir = -1f; } else if (st.markerX < -0.5f) { st.markerX = -0.5f; st.markerDir = 1f; } }
                st.marker.localPosition = new Vector3(st.markerX, 0.1f, -0.022f);
            }
            if (st.type == 4)
            {
                if (st.broken && st.showing)
                {
                    st.showT -= dt;
                    if (st.showT <= 0f)
                    {
                        if (st.showAt >= st.seq.Length) { st.showing = false; st.step = 0; ShowStation(st); }
                        else { st.flash[st.seq[st.showAt]] = 0.45f; st.showAt++; st.showT = 0.7f; Play(cPick, 0.25f); }
                    }
                }
                for (int i = 0; i < 4; i++) { st.flash[i] -= dt; var want = st.flash[i] > 0f || !st.broken ? colLit[i] : colDim[i]; if (st.els[i].sharedMaterial != want) st.els[i].sharedMaterial = want; }
            }
            // what a real fault costs while it is left alone
            if (st.broken && !st.practice && RealMission && !game.Paused)
            {
                if (st.type == 0) sim.battery = Mathf.Max(0f, sim.battery - 0.25f * dt);
                if (st.type == 2) foreach (var c in sim.crew) c.stress = Mathf.Min(100f, c.stress + 0.12f * dt);
            }
        }
        if (kid) kid.busyHide = busy && Playing;
        // survival: now and then something needs fixing (one thing at a time)
        if (RealMission && !game.Paused && stations.Count > 0)
        {
            breakIn -= dt;
            if (breakIn <= 0f)
            {
                breakIn = R(80f, 130f);
                if (BrokenStation() == null) { var st = stations[rng.Next(stations.Count)]; if (!st.broken) BreakStation(st); }
            }
        }
    }

    // ------------------------------------------------------------------ helper rovers
    class Helper { public Transform t; public TextMeshPro lab; public int kind, crew = -1; public float wait; public bool done; public Vector3 away; }
    readonly List<Helper> helpers = new List<Helper>(); readonly float[] helperReady = new float[3]; int helpersCalled;
    static readonly string[] HelperName = { "Food rover", "Medic rover", "Repair rover" };

    public void CallHelper(int kind) { CallHelper(kind, -1); }
    /// crew = -1: the rover comes to you. 0..3: it comes to that crew member (they call it themselves to look after their health)
    public void CallHelper(int kind, int crew)
    {
        if (!Playing) return;
        if (crew < 0 && Time.time < helperReady[kind]) { Note(HelperName[kind] + " is recharging. Ready in " + Mathf.CeilToInt(helperReady[kind] - Time.time) + " s."); return; }
        Transform cb = crew >= 0 && game.crewAvatars != null && crew < game.crewAvatars.Length ? game.crewAvatars[crew] : null; if (crew >= 0 && !cb) return;
        if (crew < 0) { helperReady[kind] = Time.time + 45f; helpersCalled++; }
        Vector3 f = cb ? cb.position : Feet(), fw = cb ? Vector3.ProjectOnPlane(cb.forward, Vector3.up).normalized : Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized; if (fw.sqrMagnitude < 0.01f) fw = Vector3.forward; Vector3 side = Vector3.Cross(Vector3.up, fw);
        var hp = new Helper { kind = kind, crew = crew, away = (fw + side * 0.6f).normalized };
        hp.t = new GameObject(HelperName[kind]).transform; hp.t.SetParent(root, false); hp.t.position = f + fw * 13f + side * (kind - 1) * 2.5f;
        Color[] body = { new Color(0.98f, 0.6f, 0.15f), new Color(0.95f, 0.96f, 0.98f), new Color(0.98f, 0.85f, 0.15f) };
        var dark = Lit(new Color(0.12f, 0.12f, 0.14f));
        Prim(PrimitiveType.Cube, hp.t, new Vector3(0f, 0.42f, 0f), new Vector3(0.7f, 0.34f, 1.0f), Lit(body[kind]));
        Prim(PrimitiveType.Cube, hp.t, new Vector3(0f, 0.72f, -0.1f), new Vector3(0.5f, 0.26f, 0.5f), Lit(body[kind] * 0.9f));
        for (int k = 0; k < 4; k++) Prim(PrimitiveType.Cylinder, hp.t, new Vector3(k % 2 == 0 ? -0.4f : 0.4f, 0.2f, k < 2 ? 0.32f : -0.32f), new Vector3(0.4f, 0.07f, 0.4f), dark).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Prim(PrimitiveType.Sphere, hp.t, new Vector3(0f, 0.95f, -0.1f), Vector3.one * 0.14f, kind == 1 ? stBad : (kind == 0 ? stLit : stWhite));
        if (kind == 1) { Prim(PrimitiveType.Cube, hp.t, new Vector3(0f, 0.72f, 0.16f), new Vector3(0.3f, 0.09f, 0.02f), stBad); Prim(PrimitiveType.Cube, hp.t, new Vector3(0f, 0.72f, 0.16f), new Vector3(0.09f, 0.3f, 0.02f), stBad); }
        var lab = Label(hp.t, "<b>" + HelperName[kind].ToUpper() + "</b>", 0f, 1.25f, 1.6f, 2.4f, 0.3f, new Color(1f, 0.9f, 0.5f)); hp.lab = lab;
        helpers.Add(hp);
        if (crew < 0) { Note(HelperName[kind] + " is on its way!"); Play(cPop, 0.5f); }
    }

    void UpdateHelpers(float dt)
    {
        for (int i = helpers.Count - 1; i >= 0; i--)
        {
            var hp = helpers[i]; Vector3 p = hp.t.position;
            Transform cb = hp.crew >= 0 && game.crewAvatars != null && hp.crew < game.crewAvatars.Length ? game.crewAvatars[hp.crew] : null;
            if (hp.crew >= 0 && !cb && !hp.done) { hp.done = true; hp.wait = 0f; }      // that crew member is you now, or gone: the rover drives off
            Vector3 f = cb ? cb.position : Feet();
            if (!hp.done)
            {
                Vector3 to = f - p; float dy = to.y; to.y = 0f; float d = to.magnitude;
                p += to.normalized * Mathf.Min(6f * dt, Mathf.Max(0f, d - 1.6f)); p.y = Mathf.Lerp(p.y, f.y, dt * 5f);
                if (d > 0.1f) hp.t.rotation = Quaternion.Slerp(hp.t.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), dt * 6f);
                if (d < 2.3f && Mathf.Abs(dy) < 2.5f)
                {
                    hp.done = true; hp.wait = 2.5f; Play(cWin, 0.5f); HunchFx.Spawn("healing", p, 0.5f, 2f);
                    if (hp.crew >= 0) { CrewHelped(hp.crew, hp.kind); }
                    else if (hp.kind == 0) { food = Mathf.Min(9, food + 2); Note("The food rover gave you 2 food packs.  (" + food + ")"); }
                    else if (hp.kind == 1) { health = 100f; hurtWarned = false; Note("The medic rover patched you up. Health 100"); }
                    else { var st = BrokenStation(); if (st != null) { st.practice = false; FixStation(st); Note("The repair rover fixed the " + st.name + " panel for you."); } else Note("The repair rover checked everything. Nothing is broken."); }
                }
            }
            else
            {
                hp.wait -= dt;
                if (hp.wait < 0f) { p += hp.away * 5f * dt; hp.t.rotation = Quaternion.Slerp(hp.t.rotation, Quaternion.LookRotation(hp.away, Vector3.up), dt * 4f); }
                if (hp.wait < -6f) { Destroy(hp.t.gameObject); helpers.RemoveAt(i); continue; }
            }
            hp.t.position = p;
            if (hp.lab) hp.lab.transform.rotation = Quaternion.LookRotation(hp.lab.transform.position - cam.position, Vector3.up);
        }
    }

    // ------------------------------------------------------------------ third-person view, the astronaut and the dance
    Camera tpCam; Transform avatar, avBody, armL, armR, legL, legR; bool thirdPerson, danced, danceAuto; float danceUntil = -9f, danceStart, walkPhase, avYaw; Vector3 avLast;

    Transform Limb(Transform parent, float x, float y, float len, float thick, Material m)
    {
        var pivot = new GameObject("Limb").transform; pivot.SetParent(parent, false); pivot.localPosition = new Vector3(x, y, 0f);
        Prim(PrimitiveType.Capsule, pivot, new Vector3(0f, -len * 0.5f, 0f), new Vector3(thick, len * 0.5f, thick), m);
        return pivot;
    }

    void BuildAvatar()
    {
        avatar = new GameObject("Astronaut (you)").transform; avatar.SetParent(root, false);
        avBody = new GameObject("Body").transform; avBody.SetParent(avatar, false);
        var white = Lit(new Color(0.95f, 0.95f, 0.97f)); var silver = Lit(new Color(0.78f, 0.8f, 0.84f)); var gold = Lit(new Color(0.98f, 0.76f, 0.22f)); var dark = Lit(new Color(0.15f, 0.16f, 0.2f));
        Prim(PrimitiveType.Capsule, avBody, new Vector3(0f, 1.02f, 0f), new Vector3(0.46f, 0.36f, 0.34f), white);
        var blue = Lit(new Color(0.16f, 0.34f, 0.82f)); var red = Lit(new Color(0.92f, 0.2f, 0.14f));
        Prim(PrimitiveType.Cube, avBody, new Vector3(0f, 1.08f, -0.22f), new Vector3(0.36f, 0.48f, 0.18f), blue);
        Prim(PrimitiveType.Cube, avBody, new Vector3(0f, 1.34f, -0.22f), new Vector3(0.38f, 0.06f, 0.2f), gold);
        Prim(PrimitiveType.Cube, avBody, new Vector3(0f, 1.08f, -0.315f), new Vector3(0.2f, 0.12f, 0.02f), red);
        Prim(PrimitiveType.Cylinder, avBody, new Vector3(0.13f, 1.5f, -0.24f), new Vector3(0.02f, 0.15f, 0.02f), silver); Prim(PrimitiveType.Sphere, avBody, new Vector3(0.13f, 1.67f, -0.24f), Vector3.one * 0.06f, red);
        Prim(PrimitiveType.Sphere, avBody, new Vector3(0f, 1.56f, 0f), Vector3.one * 0.44f, white);
        Prim(PrimitiveType.Sphere, avBody, new Vector3(0f, 1.56f, 0.12f), new Vector3(0.34f, 0.28f, 0.24f), gold);
        Prim(PrimitiveType.Cube, avBody, new Vector3(0f, 1.1f, 0.17f), new Vector3(0.2f, 0.14f, 0.04f), dark);
        Prim(PrimitiveType.Cube, avBody, new Vector3(0f, 0.8f, 0f), new Vector3(0.44f, 0.07f, 0.34f), gold);
        armL = Limb(avBody, -0.3f, 1.3f, 0.56f, 0.14f, white); armR = Limb(avBody, 0.3f, 1.3f, 0.56f, 0.14f, white);
        legL = Limb(avBody, -0.12f, 0.74f, 0.7f, 0.17f, white); legR = Limb(avBody, 0.12f, 0.74f, 0.7f, 0.17f, white);
        foreach (var lg in new[] { legL, legR }) Prim(PrimitiveType.Cube, lg, new Vector3(0f, -0.7f, 0.05f), new Vector3(0.19f, 0.1f, 0.3f), silver);
        foreach (var am in new[] { armL, armR }) { Prim(PrimitiveType.Sphere, am, new Vector3(0f, -0.58f, 0f), Vector3.one * 0.15f, silver); Prim(PrimitiveType.Cylinder, am, new Vector3(0f, -0.2f, 0f), new Vector3(0.155f, 0.025f, 0.155f), red); Prim(PrimitiveType.Cylinder, am, new Vector3(0f, -0.27f, 0f), new Vector3(0.155f, 0.02f, 0.155f), blue); }
        foreach (var lg in new[] { legL, legR }) Prim(PrimitiveType.Cylinder, lg, new Vector3(0f, -0.42f, 0f), new Vector3(0.185f, 0.025f, 0.185f), gold);
        avatar.gameObject.SetActive(false);

        var go = new GameObject("Third-person camera"); go.transform.SetParent(root, false);
        tpCam = go.AddComponent<Camera>(); tpCam.CopyFrom(origin.Camera); tpCam.depth = origin.Camera.depth + 1f; tpCam.targetTexture = null;
        try { tpCam.GetUniversalAdditionalCameraData().renderPostProcessing = true; } catch { }
        tpCam.enabled = false;
    }

    public void SetThird(bool on)
    {
        if (game.HeadsetActive) { Note("In the headset you ARE the astronaut. Third-person view is for keyboard and mouse."); return; }
        thirdPerson = on; if (tpCam) tpCam.enabled = on; origin.Camera.enabled = !on;
        if (!danceAuto) Note(on ? "Third-person view. V switches back." : "First-person view.");
    }
    public void Dance()
    {
        if (!Playing) return;
        emoteMove = -1; danceStart = Time.time; danceUntil = Time.time + 8f; danced = true;
        if (!game.HeadsetActive && !thirdPerson) { danceAuto = true; SetThird(true); }
        Note("Dance party!"); Play(cWin, 0.6f);
        Fx2("confetti_blast_multicolor", "sparks explode yellow", Feet() + Vector3.up * 2.2f, 0.4f, 0.8f, 1.5f);
    }

    void UpdateAvatar(float dt)
    {
        if (!avatar) return;
        bool dancing = Time.time < danceUntil, vr = game.HeadsetActive;
        if (!dancing && danceAuto) { danceAuto = false; SetThird(false); }
        if (thirdPerson && (!Playing || vr)) { thirdPerson = false; if (tpCam) tpCam.enabled = false; origin.Camera.enabled = true; }
        bool show = Playing && (thirdPerson || dancing || (vr && mirrorMe));
        if (avatar.gameObject.activeSelf != show) avatar.gameObject.SetActive(show);
        if (!show) return;
        Vector3 f = Feet(), fw = Vector3.ProjectOnPlane(cam.forward, Vector3.up); if (fw.sqrMagnitude < 0.01f) fw = Vector3.forward; fw.Normalize();
        float yaw = Mathf.Atan2(fw.x, fw.z) * Mathf.Rad2Deg; bool mirror = vr;      // in the headset the astronaut dances in front of you, like a mirror
        Vector3 mv = f - avLast; mv.y = 0f; avLast = f; float speed = mv.magnitude / Mathf.Max(0.0001f, dt);
        avatar.position = mirror ? f + fw * 2.6f : (ride != null ? f + Vector3.up * 0.7f : f);
        avYaw = Mathf.LerpAngle(avYaw, mirror ? yaw + 180f : yaw, dt * 8f);
        float roll = 0f, bounce = 0f, spin = 0f, aL = 0f, aR = 0f, sL = 0f, sR = 0f, lL = 0f, lR = 0f, spread = 0f;
        if (dancing)
        {
            float t = Time.time - danceStart, b = Mathf.Sin(t * Mathf.PI * 3f); int move = emoteMove >= 0 ? emoteMove : (int)(t / 2f) % 4;
            if (move == 0) { sL = -150f - 22f * b; sR = 150f - 22f * b; roll = 8f * b; bounce = Mathf.Abs(b) * 0.07f; }
            else if (move == 1) { spin = 360f * ((t % 2f) / 2f); sL = -90f; sR = 90f; bounce = Mathf.Abs(b) * 0.05f; }
            else if (move == 2) { sL = -20f + 55f * b; sR = 20f + 55f * b; roll = -9f * b; aL = 20f * b; aR = -20f * b; }
            else if (move >= 4) EmotePose(move, t, b, ref sL, ref sR, ref aL, ref aR, ref lL, ref lR, ref roll, ref bounce, ref spin, ref spread);
            else { bounce = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * 0.4f; sL = -165f; sR = 165f; spread = 16f; }
        }
        else
        {
            walkPhase += speed * dt * 3.2f; float sw = Mathf.Clamp01(speed / 3f) * Mathf.Sin(walkPhase) * 38f;
            aL = sw; aR = -sw; lL = -sw; lR = sw; bounce = Mathf.Clamp01(speed / 3f) * Mathf.Abs(Mathf.Sin(walkPhase)) * 0.04f; sL = -6f; sR = 6f;
        }
        avatar.rotation = Quaternion.Euler(0f, avYaw, 0f);
        avBody.localPosition = new Vector3(0f, bounce, 0f); avBody.localRotation = Quaternion.Euler(0f, spin, roll);
        armL.localRotation = Quaternion.Euler(aL, 0f, sL); armR.localRotation = Quaternion.Euler(aR, 0f, sR);
        legL.localRotation = Quaternion.Euler(lL, 0f, -spread); legR.localRotation = Quaternion.Euler(lR, 0f, spread);
        if (vr && mirrorMe && !dancing) MirrorArms(yaw);
    }

    void LateThird()
    {
        if (!thirdPerson || !tpCam) return;
        Vector3 head = cam.position + Vector3.up * 0.25f, back = -cam.forward; if (ShowCamera(head)) return; back.y = Mathf.Clamp(back.y, -0.2f, 0.6f); back.Normalize();
        Vector3 want = back * 3.6f + Vector3.up * 0.7f; float dist = want.magnitude; RaycastHit h;
        if (Cast(new Ray(head, want / dist), out h, dist + 0.3f)) dist = Mathf.Max(0.8f, h.distance - 0.3f);
        Vector3 p = head + want.normalized * dist;
        tpCam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(head + cam.forward * 3f - p, Vector3.up));
    }

    // ------------------------------------------------------------------ sky: Earth link, orbital cannons, passing events
    Transform comSat, earthT; Vector3 antenna, earthPos; LineRenderer linkUp, linkDown; readonly List<Transform> linkPulse = new List<Transform>(); bool linkOk;
    readonly Transform[] cannons = new Transform[2]; readonly Vector3[] cannonDir = new Vector3[2]; readonly LineRenderer[] cannonBeam = new LineRenderer[2]; readonly float[] cannonBeamT = new float[2];
    class SkyRock { public Transform t; public Vector3 pos, vel; public float life, hitAt; public bool live; public int state; }
    readonly List<SkyRock> skyRocks = new List<SkyRock>(); float nextRock = 8f, skyEventIn = 70f, skyEventLeft; int skyEvent; GameObject stormFx; Color fillBase; float fillInt, impactIn;
    static readonly string[] SkyEventName = { "", "DUST STORM", "SOLAR FLARE", "METEOR SHOWER" };

    void BuildSkyNet()
    {
        var eo = GameObject.Find("BAKED_EARTH"); if (eo) { earthT = eo.transform; var er = eo.GetComponentInChildren<Renderer>(); earthPos = er ? er.bounds.center : eo.transform.position; } else earthPos = fieldC + SkyDir(200f, 30f) * 5000f;
        var tel = GameObject.Find("BAKED_TELECOM"); float top = 0f; antenna = fieldC + Vector3.up * 40f;
        if (tel) { var tr = tel.GetComponentInChildren<Renderer>(); if (tr) { antenna = new Vector3(tr.bounds.center.x, tr.bounds.max.y, tr.bounds.center.z); top = tr.bounds.max.y; } }
        var silver = Glow(new Color(0.95f, 0.97f, 1.05f)); var gold = Glow(new Color(1.15f, 0.82f, 0.22f)); var blue = Glow(new Color(0.15f, 0.38f, 1.1f));      // unlit so they show against the dark sky
        comSat = new GameObject("Relay satellite").transform; comSat.SetParent(root, false); comSat.localScale = Vector3.one * 3f;
        Prim(PrimitiveType.Cube, comSat, Vector3.zero, new Vector3(8f, 8f, 12f), gold);
        Prim(PrimitiveType.Cube, comSat, new Vector3(-16f, 0f, 0f), new Vector3(22f, 0.6f, 9f), blue); Prim(PrimitiveType.Cube, comSat, new Vector3(16f, 0f, 0f), new Vector3(22f, 0.6f, 9f), blue);
        Prim(PrimitiveType.Sphere, comSat, new Vector3(0f, -6f, 0f), new Vector3(9f, 3f, 9f), silver);
        var linkMat = Glow(new Color(0.3f, 1.4f, 2.2f));
        linkUp = Line("Link: base to satellite", linkMat, 0.9f, root); linkDown = Line("Link: satellite to Earth", linkMat, 2.2f, root);
        var pm = Glow(new Color(1.3f, 1.3f, 0.8f));
        for (int i = 0; i < 6; i++) { var s = Prim(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * (i < 3 ? 3.5f : 9f), pm).transform; s.name = "Signal"; linkPulse.Add(s); }
        foreach (var r in comSat.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linkOk = true;

        // two orbital defence platforms that shoot down incoming meteorites
        var beamMat = Glow(new Color(3f, 0.7f, 0.3f)); var rockMat = Glow(new Color(2.8f, 1.5f, 0.5f));
        float[] az = { 215f, 330f }, el = { 30f, 38f };
        for (int i = 0; i < 2; i++)
        {
            var c = new GameObject("Orbital cannon " + (i + 1)).transform; c.SetParent(root, false); c.localScale = Vector3.one * 3f; cannons[i] = c; cannonDir[i] = SkyDir(az[i], el[i]);
            Prim(PrimitiveType.Cylinder, c, Vector3.zero, new Vector3(26f, 22f, 26f), silver).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, c, new Vector3(0f, 0f, 38f), new Vector3(9f, 22f, 9f), gold).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cube, c, new Vector3(-42f, 0f, -6f), new Vector3(50f, 1.5f, 22f), blue); Prim(PrimitiveType.Cube, c, new Vector3(42f, 0f, -6f), new Vector3(50f, 1.5f, 22f), blue);
            Prim(PrimitiveType.Sphere, c, new Vector3(0f, 0f, 62f), Vector3.one * 10f, beamMat);
            foreach (var r in c.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cannonBeam[i] = Line("Cannon beam", beamMat, 9f, root); cannonBeam[i].enabled = false;
        }
        for (int i = 0; i < 5; i++) { var rk = new SkyRock(); rk.t = Prim(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 26f, rockMat).transform; rk.t.name = "Meteorite"; rk.t.gameObject.SetActive(false); skyRocks.Add(rk); }
        if (fill) { fillBase = fill.color; fillInt = fill.intensity; }
    }

    void StartSkyEvent(int kind)
    {
        skyEvent = kind; skyEventLeft = kind == 3 ? 16f : 22f; impactIn = 3f;
        string[] what = { "", "Dust is blowing over the solar panels. It will pass.", "Radiation is rising outside. Go indoors!", "The orbital cannons are shooting them down. Look up!" };
        Banner(SkyEventName[kind], what[kind], true);
        sim.Say("ARIA: " + (kind == 1 ? "Dust storm. Charged Moon dust is drifting over the base and the solar arrays will get dirty." : (kind == 2 ? "Solar flare! Radiation is rising outside. Stay indoors until it passes." : "Meteor shower. The orbital cannons are protecting the base, and some minerals may land in the mining field.")));
    }

    void UpdateSkyNet(float dt)
    {
        if (!comSat) return;
        // relay satellite and the signal to Earth
        float t = Time.time; Vector3 toEarth = (earthPos - antenna).normalized, side = Vector3.Cross(Vector3.up, toEarth).normalized;
        comSat.position = antenna + (toEarth * 0.55f + Vector3.up * 0.8f).normalized * 900f + side * Mathf.Sin(t * 0.07f) * 220f;
        comSat.rotation = Quaternion.LookRotation(toEarth, Vector3.up) * Quaternion.Euler(0f, 0f, t * 4f);
        bool up = kidSky && linkOk;
        if (linkUp.enabled != up) { linkUp.enabled = up; linkDown.enabled = up; foreach (var s in linkPulse) s.gameObject.SetActive(up); comSat.gameObject.SetActive(kidSky); foreach (var c in cannons) if (c) c.gameObject.SetActive(kidSky); }
        if (up)
        {
            linkUp.SetPosition(0, antenna); linkUp.SetPosition(1, comSat.position); linkDown.SetPosition(0, comSat.position); linkDown.SetPosition(1, earthPos);
            for (int i = 0; i < linkPulse.Count; i++)
            {
                float k = (t * (i < 3 ? 0.35f : 0.22f) + i * 0.33f) % 1f;
                linkPulse[i].position = i < 3 ? Vector3.Lerp(antenna, comSat.position, k) : Vector3.Lerp(comSat.position, earthPos, k);
            }
        }
        if (!kidSky) return;

        // orbital cannons and meteorites, far away in the sky
        for (int i = 0; i < 2; i++)
        {
            Vector3 cp = cam.position + cannonDir[i] * 2300f; cannons[i].position = cp;
            cannonBeamT[i] -= dt; if (cannonBeam[i].enabled && cannonBeamT[i] <= 0f) cannonBeam[i].enabled = false;
        }
        nextRock -= dt * (skyEvent == 3 ? 6f : 1f);
        if (nextRock <= 0f)
        {
            nextRock = R(9f, 17f);
            foreach (var rk in skyRocks)
            {
                if (rk.live) continue;
                rk.live = true; rk.state = 0; rk.life = 0f; rk.hitAt = R(1.3f, 2.4f);
                rk.pos = SkyDir(R(0f, 360f), R(50f, 78f)) * 2700f; Vector3 hd = SkyDir(R(0f, 360f), 0f);
                rk.vel = (hd * 0.7f + Vector3.down * 0.45f).normalized * R(380f, 560f); rk.t.localScale = Vector3.one * 26f; rk.t.gameObject.SetActive(true); break;
            }
        }
        foreach (var rk in skyRocks)
        {
            if (!rk.live) continue;
            rk.life += dt;
            if (rk.state == 0)
            {
                rk.pos += rk.vel * dt;
                if (rk.life >= rk.hitAt)
                {
                    int ci = (rk.pos - cannonDir[0] * 2300f).sqrMagnitude < (rk.pos - cannonDir[1] * 2300f).sqrMagnitude ? 0 : 1;
                    cannons[ci].rotation = Quaternion.LookRotation((rk.pos - cannonDir[ci] * 2300f).normalized, Vector3.up);
                    cannonBeam[ci].SetPosition(0, cannons[ci].TransformPoint(new Vector3(0f, 0f, 62f))); cannonBeam[ci].SetPosition(1, cam.position + rk.pos); cannonBeam[ci].enabled = true; cannonBeamT[ci] = 0.22f;
                    rk.state = 1; rk.life = 0f;
                }
            }
            else { rk.t.localScale = Vector3.one * (26f + 240f * rk.life); if (rk.life > 0.45f) { rk.live = false; rk.t.gameObject.SetActive(false); } }
            rk.t.position = cam.position + rk.pos;
        }

        // passing events
        bool outside = !fx || fx.Outside;
        if (skyEvent == 0)
        {
            if (Playing && !game.Paused) { skyEventIn -= dt; if (skyEventIn <= 0f) { skyEventIn = R(95f, 150f); StartSkyEvent(1 + rng.Next(3)); } }
        }
        else
        {
            skyEventLeft -= dt; bool real = RealMission && !game.Paused;
            if (skyEvent == 1)
            {
                if (outside && !stormFx) stormFx = HunchFx.Spawn("dust hemisphere loop", Feet(), 4f, 0f);
                if (stormFx) { if (outside) stormFx.transform.position = Feet(); else { HunchFx.Stop(stormFx); stormFx = null; } }
                if (fill) { fill.color = Color.Lerp(fill.color, new Color(0.8f, 0.6f, 0.4f), dt * 2f); fill.intensity = Mathf.Lerp(fill.intensity, fillInt * 0.6f, dt * 2f); }
                if (real) { sim.arrays = Mathf.Max(20f, sim.arrays - 0.5f * dt); sim.dust = Mathf.Min(100f, sim.dust + 0.3f * dt); }
            }
            else if (skyEvent == 2)
            {
                if (sunHalo) sunHalo.localScale = Vector3.one * 1500f * (1.9f + 0.4f * Mathf.Sin(t * 9f));
                if (fill) fill.color = Color.Lerp(fill.color, new Color(1f, 0.72f, 0.5f), dt * 2f);
                if (real && outside && !sim.sheltered) { sim.dose += 0.03f * dt; Hurt(1.6f * dt, null); }
            }
            else
            {
                impactIn -= dt;
                if (impactIn <= 0f && fieldOk)
                {
                    impactIn = R(3.5f, 6f); Vector3 p;
                    if (RandomGround(10f, 40f, out p)) { HunchFx.Spawn("stones hit", p, 1.6f, 0.9f); HunchFx.Spawn("dust puff", p, 0.5f, 1.5f); DropPickup(rng.Next(2) == 0 ? Ore : Ice, 2, p); if ((p - cam.position).sqrMagnitude < 70f * 70f) Play(cBoom, 0.5f); }
                }
            }
            if (skyEventLeft <= 0f)
            {
                if (stormFx) { HunchFx.Stop(stormFx); stormFx = null; }
                if (sunHalo) sunHalo.localScale = Vector3.one * 1500f;
                Note(SkyEventName[skyEvent].Substring(0, 1) + SkyEventName[skyEvent].Substring(1).ToLower() + " has passed.");
                skyEvent = 0;
            }
        }
        if (skyEvent != 1 && skyEvent != 2 && fill) { fill.color = Color.Lerp(fill.color, fillBase, dt * 1.5f); fill.intensity = Mathf.Lerp(fill.intensity, fillInt, dt * 1.5f); }
    }

    // ------------------------------------------------------------------ the simple screen
    GUIStyle sS, sSB, sSO;

    static void Fill(Rect r, Color c) { HunchUi.Round(r, c); }
    GUIStyle sQuest;
    void SBar(float x, float y, float w, float u, string name, float v, Color c)
    {
        GUI.Label(new Rect(x, y - 3f * u, 92f * u, 24f * u), name, sS);
        Fill(new Rect(x + 92f * u, y + 5f * u, w - 150f * u, 12f * u), new Color(1f, 1f, 1f, 0.14f)); Fill(new Rect(x + 92f * u, y + 5f * u, (w - 150f * u) * Mathf.Clamp01(v / 100f), 12f * u), c);
        GUI.Label(new Rect(x + w - 52f * u, y - 3f * u, 56f * u, 24f * u), Mathf.RoundToInt(v).ToString(), sS);
    }
    static Color Good(float v) { return v > 60f ? new Color(0.45f, 0.95f, 0.55f) : (v > 30f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.4f, 0.3f)); }

    string AlertLine(bool rich)
    {
        var st = BrokenStation();
        if (st != null) { int zi = game.ZoneIndex(st.zoneId); return "! Fix the " + st.name + " panel at " + (zi >= 0 ? game.zones[zi].title : st.zoneId); }
        if (sim.alerts.Count > 0) { var a = sim.alerts[0]; return "! " + a.title + " - go to " + sim.zoneTitle(a.zoneId); }
        if (skyEvent != 0) return "! " + SkyEventName[skyEvent];
        return "";
    }

    void DrawSimpleHud(float u, float W, float H)
    {
        if (sS == null || Mathf.Abs(sS.fontSize - HunchLook.FsF(17f * u)) > 1f)
        {
            sS = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(17f * u), richText = true, wordWrap = false, clipping = TextClipping.Clip };
            sSB = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(24f * u), richText = true, fontStyle = FontStyle.Bold };
            sSO = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(20f * u), richText = true, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            foreach (var s in new[] { sS, sSB, sSO }) s.normal.textColor = Color.white;
        }
        // top left: you and the habitat, in a few lines
        float pad = 12f * u, w = 430f * u, x = pad + 10f * u, y = pad + 6f * u; bool surv = !Creative;
        Fill(new Rect(pad, pad, w, (surv ? 196f : 144f) * u), new Color(0.02f, 0.04f, 0.09f, 0.78f));
        GUI.Label(new Rect(x, y, w, 32f * u), "DAY " + sim.Day + "   " + Mathf.FloorToInt(sim.Hour).ToString("00") + ":00   <size=" + Mathf.RoundToInt(15f * u) + "><color=" + (surv ? "#FFB070>SURVIVAL" : "#7CFC9A>CREATIVE") + "</color></size>", sSB); y += 36f * u;
        float bw = w - 20f * u;
        if (surv) { SBar(x, y, bw, u, "Health", health, Good(health)); y += 24f * u; SBar(x, y, bw, u, "Hunger", hunger, Good(hunger)); y += 28f * u; }
        SBar(x, y, bw, u, "Oxygen", sim.oxygen, Good(sim.oxygen)); y += 24f * u; SBar(x, y, bw, u, "Power", sim.battery, Good(sim.battery)); y += 26f * u;
        var sTiny = new GUIStyle(sS) { fontSize = HunchLook.Fs(14f * u) };
        GUI.Label(new Rect(x, y, bw, 22f * u), "<color=#B8C6DC>Crew health " + Mathf.RoundToInt(sim.AvgWellbeing) + "%    Solar " + Mathf.RoundToInt(sim.arrays) + "%" + (hunchOn ? "" : "    Dyson swarm " + dysonCount + " panels") + "</color>", sTiny); y += 20f * u;
        GUI.Label(new Rect(x, y, bw, 22f * u), "<color=#B8C6DC>Radiation " + sim.dose.ToString("F1") + " mSv    Earth link " + (linkOk ? "ON" : "off") + (surv ? "    <color=#FFE08A>Food " + food + "</color>" : "") + "</color>", sTiny);

        // top centre: the one thing to do now
        // Project ARES mission card (the task bar): under the panel on the left, always the ONE next step
        string mission = null; try { mission = AresTaskCard(); } catch { }
        if (!string.IsNullOrEmpty(mission))
        {
            if (sQuest == null || sQuest.fontSize != HunchLook.Fs(16f * u)) { sQuest = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(16f * u), richText = true, wordWrap = true, alignment = TextAnchor.UpperLeft }; sQuest.normal.textColor = Color.white; }
            float qy = pad + (surv ? 196f : 144f) * u + 8f * u, qh = sQuest.CalcHeight(new GUIContent(mission), w - 30f * u) + 14f * u;
            var qr = new Rect(pad, qy, w, qh); Fill(qr, new Color(0.03f, 0.05f, 0.12f, 0.88f));
            Fill(new Rect(pad, qy, 6f * u, qh), new Color(1f, 0.8f, 0.3f, 1f));      // gold edge = your task
            GUI.Label(new Rect(pad + 16f * u, qy + 7f * u, w - 30f * u, qh - 10f * u), mission, sQuest);
        }
        string alert = AlertLine(true), obj = string.IsNullOrEmpty(mission) ? game.Objective() : game.ObjectiveBase(); float ow = Mathf.Min(820f * u, W - 2f * (w + 2f * pad));
        if (ow > 260f * u)
        {
            float two = 0f; bool task = false;
            var r = new Rect((W - ow) * 0.5f, pad, ow, (alert.Length > 0 ? 84f : 54f) * u + two * u); Fill(r, new Color(0.02f, 0.04f, 0.09f, 0.82f));
            if (task) Fill(new Rect(r.x, r.y, 6f * u, r.height), new Color(1f, 0.82f, 0.3f, 0.95f));      // gold edge = your task
            GUI.Label(new Rect(r.x + 10f * u, r.y + 2f * u, r.width - 20f * u, (50f + two) * u), "<color=#FFE08A>" + obj + "</color>", sSO);
            if (alert.Length > 0) GUI.Label(new Rect(r.x + 10f * u, r.y + (50f + two) * u, r.width - 20f * u, 30f * u), "<color=#FF7A6A><b>" + alert + "</b></color>", sSO);
        }
        // bottom: what ARIA just said, and what E does here
        float by = H - 52f * u - 38f * u;
        if (finder.Length > 0) by -= 38f * u;
        if (game.NearZone >= 0)
        {
            string pr = sim.Prompt(game.zones[game.NearZone].id);
            if (pr.Length > 0) { var r = new Rect(W * 0.5f - 420f * u, by - 40f * u, 840f * u, 36f * u); Fill(r, new Color(0.02f, 0.04f, 0.09f, 0.7f)); GUI.Label(r, "<color=#FFE08A><b>" + game.zones[game.NearZone].title + "</b>   [E] " + pr + "</color>", sSO); by -= 44f * u; }
        }
        if (sim.messageAge < 8f && sim.message.Length > 0)
        {
            var r = new Rect(W * 0.5f - 520f * u, by - 62f * u, 1040f * u, 58f * u); Fill(r, new Color(0.02f, 0.04f, 0.09f, 0.6f));
            GUI.Label(new Rect(r.x + 10f * u, r.y, r.width - 20f * u, r.height), "<color=#9AD8FF>" + sim.message + "</color>", new GUIStyle(sSO) { fontSize = HunchLook.Fs(17f * u) });
        }
    }

    string SimpleCardText()
    {
        var sb = new StringBuilder(); bool surv = !Creative;
        sb.Append("<b>DAY ").Append(sim.Day).Append("   ").Append(Mathf.FloorToInt(sim.Hour).ToString("00")).Append(":00</b>   <size=70%>").Append(surv ? "<color=#FFB070>SURVIVAL</color>" : "<color=#7CFC9A>CREATIVE</color>").Append("</size>\n");
        sb.Append("<color=#FFE08A>").Append(game.Objective()).Append("</color>\n");
        string alert = AlertLine(true); if (alert.Length > 0) sb.Append("<color=#FF7A6A><b>").Append(alert).Append("</b></color>\n");
        if (surv) sb.Append("Health ").Append(Mathf.RoundToInt(health)).Append("   Hunger ").Append(Mathf.RoundToInt(hunger)).Append("   Food ").Append(food).Append("\n");
        sb.Append("Oxygen ").Append(Mathf.RoundToInt(sim.oxygen)).Append("   Power ").Append(Mathf.RoundToInt(sim.battery)).Append("   Crew ").Append(Mathf.RoundToInt(sim.AvgWellbeing)).Append("%   Dyson ").Append(dysonCount).Append("\n");
        if (game.navLine.Length > 0) sb.Append("<color=#7CFC9A>").Append(game.navLine).Append("</color>\n");
        if (game.toolLine.Length > 0) sb.Append("<size=85%><color=#E8C8FF>").Append(game.toolLine).Append("</color></size>\n");
        if (sim.messageAge < 9f) sb.Append("<size=85%><color=#9AD8FF>").Append(sim.message).Append("</color></size>");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ set-up, frame and menu
    void BuildBatch3()
    {
        BuildStations(); BuildAvatar(); BuildSkyNet(); BuildDyson(); BuildDriveModels(); BuildBatch7(); BuildBatch8();
        sim.onCare += OnCareFood;
        game.simpleCard = SimpleCardText;
        if (!menu) return;
        var help = new HunchMenu.ExtraPage { title = "HELPERS AND YOU", label = "Helpers, food, view and dance", hint = "Call a rover to bring food, heal you or do a repair. Eat, switch between first and third person, or dance." };
        help.build = m =>
        {
            bool vr = game.HeadsetActive;
            m.AddItem("Eat a food pack   (you have " + food + ")", "Fills your hunger by 40 and heals a little." + (vr ? "" : " The X key does the same."), () => { Eat(); m.Rebuild(); });
            m.AddItem("Call the food rover", "Brings 2 food packs. Ready again after 45 seconds.", () => { m.Close(); CallHelper(0); });
            m.AddItem("Call the medic rover", "Heals you to full health.", () => { m.Close(); CallHelper(1); });
            m.AddItem("Call the repair rover", "Fixes whichever panel is broken, if you would rather not do the mini-game.", () => { m.Close(); CallHelper(2); });
            if (!vr) m.AddItem("View: " + (thirdPerson ? "third person" : "first person"), "Third person shows your astronaut from behind. The V key does the same.", () => { SetThird(!thirdPerson); m.Rebuild(); });
            m.AddItem("Dance!", vr ? "Your astronaut double appears in front of you and dances." : "Your astronaut dances for 8 seconds. The B key does the same.", () => { m.Close(); Dance(); });
            m.AddItem("Autopilot: " + (autoOn ? "ON" : "off") + "   (the AI plays)", "The AI plays instead of you: it does the jobs, goes to the meetings, fills the Battery Bank and plays the mini-games. Press SPACE (or A in the headset) and it hands the controls back.", () => { m.Close(); if (autoOn) StopAutopilot("You have the controls"); else StartAutopilot(); });
            m.AddItem("Screen: " + (simpleHud ? "simple" : "full dashboard"), "Simple shows only your health, the habitat's air and power, and the one thing to do next. Full shows every panel." + (vr ? "" : " The U key does the same."), () => { simpleHud = !simpleHud; m.Rebuild(); });
        };
        menu.extraPages.Add(help);
    }

    void UpdateBatch3(float dt)
    {
        game.simpleHud = simpleHud;
        UpdateSurvival(dt); UpdateStations(dt); UpdateHelpers(dt); UpdateAvatar(dt); UpdateSkyNet(dt); UpdateDyson(dt); UpdateDriveModels(dt); UpdateBatch7(dt); UpdateBatch8(dt);
        var kb = Keyboard.current;
        if (kb != null && Playing && !game.menuOpen && !game.HeadsetActive && !autoOn)
        {
            if (kb.xKey.wasPressedThisFrame) Eat();
            if (kb.vKey.wasPressedThisFrame) SetThird(!thirdPerson);
            if (kb.bKey.wasPressedThisFrame) Dance();
            if (kb.uKey.wasPressedThisFrame) { simpleHud = !simpleHud; Note(simpleHud ? "Simple screen. U shows the full dashboard." : "Full dashboard. U goes back to the simple screen."); }
        }
    }

    public string ProbeTasks()
    {
        var sb = new StringBuilder(); sb.Append("stations=").Append(stations.Count).Append(" fixed=").Append(fixedCount).Append(" mask=").Append(fixedMask).Append(" health=").Append(Mathf.RoundToInt(health)).Append(" hunger=").Append(Mathf.RoundToInt(hunger)).Append(" food=").Append(food)
            .Append(" creative=").Append(Creative).Append(" third=").Append(thirdPerson).Append(" helpers=").Append(helpers.Count).Append(" sky=").Append(skyEvent).Append(" link=").Append(linkOk).Append(" simple=").Append(simpleHud).Append(" earth=").Append(earthPos.ToString("F0")).Append(" antenna=").Append(antenna.ToString("F0")).Append(" place=").Append(placeLog).Append(" clutter=").Append(clutter != null ? clutter.Count : -1);
        foreach (var st in stations) sb.Append(" | ").Append(st.name).Append("@").Append(st.zoneId).Append(st.broken ? (st.practice ? " practice" : " BROKEN") : " ok").Append(" step ").Append(st.step);
        return sb.ToString();
    }
    int probeStation;
    /// test helper: go to station n (0-4), break it, and face it
    public string ProbeStation()
    {
        if (stations.Count == 0) return "no stations";
        var st = stations[probeStation % stations.Count]; probeStation++;
        game.GoTo(game.ZoneIndex(st.zoneId)); if (!st.broken) BreakStation(st);
        cam.position = origin.transform.position + Vector3.up * 1.4f; Face(st.board.position);
        return st.name + " dist " + Vector3.Distance(cam.position, st.board.position).ToString("F1");
    }
    /// test helper: solve the station the probe last visited, one correct press at a time through the real press code
    public string ProbeSolveStep()
    {
        var st = stations[(probeStation + stations.Count - 1) % stations.Count]; if (!st.broken) return st.name + " already ok";
        int el = 0;
        switch (st.type)
        {
            case 0: if (st.sel < 0) { for (int i = 0; i < 4; i++) if (!st.on[i]) { el = i; break; } } else { for (int j = 0; j < 4; j++) if (st.order[j] == st.sel) el = 10 + j; } break;
            case 1: st.markerX = st.greenX; el = 0; break;
            case 2: for (int i = 0; i < 9; i++) if (!st.on[i]) { el = i; break; } break;
            case 3: for (int i = 0; i < 5; i++) if (!st.on[i]) { el = i; break; } break;
            case 4: if (st.showing) return "showing"; el = st.seq[st.step]; break;
        }
        Face(st.els[st.type == 0 && el >= 10 ? 4 + (el - 10) : Mathf.Min(el, st.els.Count - 1)].transform.position);
        StationPress(st.index * 100 + el);
        return st.name + " pressed " + el + (st.broken ? " step " + st.step : " FIXED");
    }
    public void ProbeHurt() { Hurt(45f, "Test bump."); hunger = 20f; }
    public void ProbeSky1() { StartSkyEvent(1); }
    public void ProbeSky2() { StartSkyEvent(2); }
    public void ProbeSky3() { StartSkyEvent(3); }
    public void ProbeThird() { SetThird(!thirdPerson); }
    public void ProbeDance() { Dance(); }
    public void ProbeHelperFood() { helperReady[0] = 0f; CallHelper(0); }
    public void ProbeHelperMedic() { helperReady[1] = 0f; CallHelper(1); }
    public void ProbeHelperRepair() { helperReady[2] = 0f; CallHelper(2); }
    public void ProbeFaceEarth() { Face(earthPos); }
    public void ProbeFaceSat() { if (comSat) Face(comSat.position); }
    public void ProbeFaceCannon() { Face(cam.position + cannonDir[0] * 2300f); }
    public void ProbeEat() { Eat(); }
    public void ProbeSimple() { simpleHud = !simpleHud; }
}
