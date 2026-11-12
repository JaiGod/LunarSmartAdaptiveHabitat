// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/C_Base_Crew_Systems/HunchFrontier7.cs
//  Section : 06_Frontier/C_Base_Crew_Systems
//  Folder  : Systems inside the central base: pick-ups + battery bank, NASA HUNCH mode, physics
//            view, Autopilot Smooth, crew of four + sleep pods, The Show, self-working crew,
//            airlocks/lifts, crew self-care.
//  Purpose : Seventh part of Moon Frontier: the systems inside the central base. - things to pick
//            up: battery modules, astronaut food (cans and pouches) and things to READ (tablets
//            with notes,
//  Contains: HunchFrontier, Pick, ReadItem, CamPoint, HsCrew
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// Seventh part of Moon Frontier: the systems inside the central base.
///   - things to pick up: battery modules, astronaut food (cans and pouches) and things to READ (tablets with notes,
///     and the Physics and Calculus reference sheet)
///   - the Battery Bank: collect battery modules and slot them into the big rack at the power plant
///   - the Crew Health System: the NASA HUNCH crew-health page rebuilt in the game (vitals, pulse waves, the six
///     psychological conditions, silent adjustment / options offered / escalation), fed by the four crew in the simulator
///   - security cameras: one in every zone, at the mining field and on each map, with a live picture
///   - team meetings: an alarm calls everyone to the Galley, each crew member reports, then the team votes on a plan
///   - "systems reset": if anything reaches 0% the player is brought back, everything is restored and the mission carries on
///   - the autopilot: the AI does the jobs and plays the mini-games
/// Everything is sized for the NASA limit of four crew: four health cards, four seats, four players taking turns.
public partial class HunchFrontier
{
    public bool ControlsLocked { get { return autoOn || gliding || readOpen >= 0 || (cvOn && !game.HeadsetActive); } }
    [NonSerialized] public float calcEnergy; float calcSpeed, calcBatRate, calcBatLast = -1f, calcT; Vector3 calcLast;
    HunchMenu.ExtraPage pageBase, pageHealth, pageHealthCrew, pageRead, pageMeet;
    public int probeInt; bool hudHid, hudWas;

    // ------------------------------------------------------------------ where things can stand
    readonly List<Vector3> spotsTaken = new List<Vector3>();
    bool WallSpot(string zoneIds, out Vector3 pos, out Vector3 dir, out int zone)
    {
        if (spotsTaken.Count == 0) foreach (var s in stations) spotsTaken.Add(s.root.position);
        int best = int.MaxValue; pos = Vector3.zero; dir = Vector3.forward; zone = -1;
        foreach (var id in zoneIds.Split(','))
        {
            int k = game.ZoneIndex(id); if (k < 0) continue; var zz = game.zones[k];
            foreach (float d in StDist)
                foreach (float a in StAng)
                {
                    Vector3 dd = Quaternion.Euler(0f, zz.yaw + a, 0f) * Vector3.forward, p = zz.position + dd * d;
                    int n = Clutter(zz.position, p, Quaternion.LookRotation(dd, Vector3.up));
                    foreach (var t in spotsTaken) if ((t - p).sqrMagnitude < 1.7f * 1.7f) n += 40;
                    if (n < best) { best = n; pos = p; dir = dd; zone = k; }
                }
            if (best <= 2) break;
        }
        if (zone < 0) return false;
        spotsTaken.Add(pos); return true;
    }
    static readonly float[] FloatAng = { 35f, -35f, 0f, 180f, 160f, -160f, 110f, -110f, 60f, -60f };
    Vector3 FloatSpot(HabZone z, int seed)
    {
        Vector3 p; RaycastHit h; Vector3 src = z.position + Vector3.up * 1.2f;
        for (int i = 0; i < FloatAng.Length; i++)
            foreach (float d in new[] { 1.2f, 0.9f, 1.55f })
            {
                Vector3 dd = Quaternion.Euler(0f, z.yaw + FloatAng[(i + seed) % FloatAng.Length], 0f) * Vector3.forward; p = z.position + dd * d + Vector3.up * 1.0f;
                if (Physics.CheckSphere(p, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Cast(new Ray(src, (p - src).normalized), out h, (p - src).magnitude)) continue;
                bool near = false; foreach (var t in spotsTaken) if ((t - p).sqrMagnitude < 0.75f * 0.75f) { near = true; break; }
                if (near) continue;
                spotsTaken.Add(p); return p;
            }
        p = z.position + Vector3.up * 1.0f + Quaternion.Euler(0f, z.yaw + 35f + seed * 47f, 0f) * Vector3.forward * 0.9f; spotsTaken.Add(p); return p;
    }

    // ------------------------------------------------------------------ things to pick up
    class Pick { public Transform t; public int type, index, zone = -1; public bool taken; public float respawn; public Vector3 home; }
    readonly List<Pick> picks = new List<Pick>(); public int foodFound;
    static readonly string[] FoodName = { "Canned peaches", "Beef stew pouch", "Freeze-dried ice cream", "Tortilla pack", "Mac and cheese (just add water)", "Canned tuna", "Dried fruit and nuts", "Cocoa drink pouch" };
    static readonly string[] FoodFact =
    {
        "Canned fruit keeps for years with no fridge.",
        "Heat-treated pouches are warmed in a small food warmer.",
        "A real astronaut treat. It is crunchy, not cold.",
        "Astronauts use tortillas instead of bread: no crumbs floating around.",
        "Hot water from the galley turns the dry noodles back into dinner.",
        "Protein keeps muscles strong in low gravity.",
        "A quick snack that needs no cooking.",
        "Drinks come as powder in a pouch. Add water and use the straw."
    };

    Pick AddPick(int type, int index, Vector3 p, int zone)
    {
        var k = new Pick { type = type, index = index, home = p, zone = zone };
        k.t = new GameObject(type == 0 ? "Battery module" : (type == 1 ? "Something to read" : "Astronaut food")).transform; k.t.SetParent(root, false); k.t.position = p;
        var gold = Lit(new Color(0.95f, 0.74f, 0.22f)); var silver = Lit(new Color(0.8f, 0.82f, 0.86f));
        if (type == 0)
        {
            Prim(PrimitiveType.Cylinder, k.t, Vector3.zero, new Vector3(0.2f, 0.17f, 0.2f), Glow(new Color(0.4f, 1.9f, 0.6f)));
            Prim(PrimitiveType.Cylinder, k.t, new Vector3(0f, 0.18f, 0f), new Vector3(0.21f, 0.02f, 0.21f), gold); Prim(PrimitiveType.Cylinder, k.t, new Vector3(0f, -0.18f, 0f), new Vector3(0.21f, 0.02f, 0.21f), gold);
            Prim(PrimitiveType.Cylinder, k.t, new Vector3(0f, 0.22f, 0f), new Vector3(0.07f, 0.03f, 0.07f), silver);
        }
        else if (type == 1)
        {
            Prim(PrimitiveType.Cube, k.t, new Vector3(0f, 0f, 0.012f), new Vector3(0.34f, 0.46f, 0.02f), gold);
            Prim(PrimitiveType.Cube, k.t, Vector3.zero, new Vector3(0.3f, 0.42f, 0.02f), Glow(new Color(1.0f, 0.97f, 0.85f)));
            var ink = Lit(new Color(0.08f, 0.1f, 0.25f));
            for (int i = 0; i < 4; i++) Prim(PrimitiveType.Cube, k.t, new Vector3(i == 3 ? -0.04f : 0f, 0.12f - i * 0.08f, -0.012f), new Vector3(i == 3 ? 0.14f : 0.22f, 0.022f, 0.004f), ink);
        }
        else
        {
            Color[] label = { new Color(0.95f, 0.6f, 0.2f), new Color(0.6f, 0.3f, 0.2f), new Color(0.95f, 0.75f, 0.85f), new Color(0.9f, 0.8f, 0.5f), new Color(0.95f, 0.8f, 0.2f), new Color(0.3f, 0.5f, 0.9f), new Color(0.6f, 0.4f, 0.25f), new Color(0.45f, 0.28f, 0.18f) };
            if (index % 2 == 0)
            {
                Prim(PrimitiveType.Cylinder, k.t, Vector3.zero, new Vector3(0.17f, 0.11f, 0.17f), Lit(label[index % label.Length]));
                Prim(PrimitiveType.Cylinder, k.t, new Vector3(0f, 0.115f, 0f), new Vector3(0.175f, 0.008f, 0.175f), silver); Prim(PrimitiveType.Cylinder, k.t, new Vector3(0f, -0.115f, 0f), new Vector3(0.175f, 0.008f, 0.175f), silver);
            }
            else
            {
                Prim(PrimitiveType.Cube, k.t, Vector3.zero, new Vector3(0.24f, 0.3f, 0.05f), silver);
                Prim(PrimitiveType.Cube, k.t, new Vector3(0f, -0.02f, -0.027f), new Vector3(0.18f, 0.14f, 0.004f), Lit(label[index % label.Length]));
            }
        }
        Prim(PrimitiveType.Sphere, k.t, new Vector3(0f, -0.42f, 0f), new Vector3(0.36f, 0.02f, 0.36f), Glow(type == 0 ? new Color(0.3f, 1.4f, 0.5f) : (type == 1 ? new Color(1.5f, 1.2f, 0.4f) : new Color(1.5f, 0.7f, 0.3f))));
        var sc = k.t.gameObject.AddComponent<SphereCollider>(); sc.radius = 0.34f;
        var tg = k.t.gameObject.AddComponent<HunchTarget>(); tg.kind = 40; tg.index = picks.Count;
        picks.Add(k); return k;
    }

    void BuildPicks()
    {
        // battery modules: one in each zone (not the power plant, where the bank is) and a few out at the mining field
        int cells = 0; int power = game.ZoneIndex("power");
        for (int i = 0; i < game.zones.Count && cells < 9; i++) { if (i == power || i == batZone) continue; AddPick(0, cells++, FloatSpot(game.zones[i], 0), i); }
        if (fieldOk) for (int i = 0; i < 4; i++) { Vector3 p; if (RandomGround(8f, 30f, out p)) AddPick(0, cells++, p + Vector3.up * 0.9f, -1); }
        // things to read
        for (int i = 0; i < reads.Count; i++)
        {
            int zi = game.ZoneIndex(reads[i].zone);
            if (reads[i].zone == "field") { Vector3 p; if (fieldOk && GroundAt(fieldC.x + 3f, fieldC.z - 6f, out p)) AddPick(1, i, p + Vector3.up * 1.0f, -1); else if (game.zones.Count > 0) AddPick(1, i, FloatSpot(game.zones[0], 5), 0); continue; }
            if (zi < 0) zi = i % game.zones.Count;
            AddPick(1, i, FloatSpot(game.zones[zi], 1 + i), zi);
        }
        // astronaut food, enough for a crew of four
        // food is stored, never loose: eight sealed cans and pouches on the pantry racks in the Galley and in Storage
        int[] foodZone; Vector3[] slot = PantrySlots(out foodZone);
        for (int i = 0; i < slot.Length; i++) AddPick(2, i, slot[i], foodZone[i]);
        A("Space chef", "Pick up four different astronaut foods.", () => foodFound >= 4);
        A("Bookworm", "Pick up and read three things.", () => ReadCount() >= 3);
    }

    void TakePick(int i)
    {
        if (i < 0 || i >= picks.Count) return; var p = picks[i]; if (p.taken) return;
        if (p.type == 0)
        {
            if (batBank + batHeld >= BatSlots) { Note("The Battery Bank does not need any more modules."); return; }
            if (batHeld >= BatCarry) { Note("You can carry " + BatCarry + " battery modules. Put them in the Battery Bank at the Power Plant first."); return; }
            batHeld++; Note("Battery module picked up (" + batHeld + " of " + BatCarry + "). Take it to the Battery Bank at the Power Plant.");
        }
        else if (p.type == 1) { reads[p.index].got = true; OpenRead(p.index); }
        else
        {
            if (food >= 12) { Note("Your food bag is full (12)."); return; }
            food++; foodFound++; hunger = Mathf.Min(100f, hunger + 5f); p.respawn = 150f;
            Note("+1 " + FoodName[p.index % FoodName.Length] + ".  " + FoodFact[p.index % FoodFact.Length]);
        }
        p.taken = true; p.t.gameObject.SetActive(false); Play(cPick, 0.7f); game.Buzz(0.5f, 0.08f);
        HunchFx.Spawn("star hit", p.home, 0.3f, 0.6f);
    }
    string PickText(int i)
    {
        if (i < 0 || i >= picks.Count) return ""; var p = picks[i];
        return p.type == 0 ? "pick up the battery module" : (p.type == 1 ? "pick up and read: " + reads[p.index].title : "pick up: " + FoodName[p.index % FoodName.Length]);
    }

    void UpdatePicks(float dt)
    {
        Vector3 f = Feet(); float spin = Time.time * 50f, bob = Mathf.Sin(Time.time * 2f) * 0.06f; bool can = Playing && !ControlsLocked && !game.menuOpen;
        for (int i = 0; i < picks.Count; i++)
        {
            var p = picks[i];
            if (p.taken) { if (p.type == 2) { p.respawn -= dt; if (p.respawn <= 0f) { p.taken = false; p.t.gameObject.SetActive(true); } } continue; }
            Vector3 d = p.home - f; if (d.sqrMagnitude > 45f * 45f) continue;
            if (p.type == 2) { p.t.position = p.home; p.t.rotation = Quaternion.Euler(0f, p.index * 37f, 0f); }      // food stands still on its shelf
            else { p.t.position = p.home + Vector3.up * bob; p.t.rotation = Quaternion.Euler(p.type == 1 ? 12f : 0f, spin + i * 40f, 0f); }
            if (can && d.y > -0.4f && d.y < 2.2f && new Vector2(d.x, d.z).sqrMagnitude < 1.05f * 1.05f && Time.time > pickQuiet) { pickQuiet = Time.time + 1.2f; TakePick(i); }
        }
    }
    float pickQuiet;

    // ------------------------------------------------------------------ the Battery Bank
    public int batHeld, batBank; public const int BatSlots = 8, BatCarry = 3;
    Transform batRack; readonly Renderer[] batSlot = new Renderer[BatSlots]; TextMeshPro batLabel; Material batOn, batOff; float batRescue; int batZone = -1;

    void BuildBank()
    {
        Vector3 pos, dir;
        if (!WallSpot("power,eclss,control,work", out pos, out dir, out batZone)) return;
        batOn = Glow(new Color(0.4f, 1.9f, 0.6f)); batOff = Lit(new Color(0.08f, 0.09f, 0.11f));
        var dark = Lit(new Color(0.14f, 0.15f, 0.19f)); var gold = Lit(new Color(0.95f, 0.74f, 0.22f));
        batRack = new GameObject("Battery Bank").transform; batRack.SetParent(root, false); batRack.position = pos; batRack.rotation = Quaternion.LookRotation(dir, Vector3.up);
        var frame = Prim(PrimitiveType.Cube, batRack, new Vector3(0f, 1.0f, 0.14f), new Vector3(1.6f, 2.0f, 0.3f), dark, true);
        var tg = frame.AddComponent<HunchTarget>(); tg.kind = 41;
        Prim(PrimitiveType.Cube, batRack, new Vector3(0f, 2.02f, 0.12f), new Vector3(1.66f, 0.04f, 0.36f), gold); Prim(PrimitiveType.Cube, batRack, new Vector3(0f, 0.03f, 0.12f), new Vector3(1.66f, 0.06f, 0.36f), gold);
        for (int i = 0; i < BatSlots; i++)
            batSlot[i] = Prim(PrimitiveType.Cube, batRack, new Vector3((i % 4 - 1.5f) * 0.36f, 1.42f - (i / 4) * 0.66f, -0.03f), new Vector3(0.28f, 0.52f, 0.06f), batOff).GetComponent<Renderer>();
        batLabel = Label(batRack, "", 0f, 1.87f, 0.7f, 1.5f, 0.2f, new Color(1f, 0.84f, 0.3f)); batLabel.transform.localPosition = new Vector3(0f, 1.87f, -0.02f);
        A("Power banker", "Fill all eight slots of the Battery Bank.", () => batBank >= BatSlots);
    }

    void BankInsert()
    {
        if (!batRack) return;
        if (batHeld <= 0) { Note(batBank >= BatSlots ? "The Battery Bank is full and online." : "Battery Bank: " + batBank + " of " + BatSlots + ". Find the glowing green battery modules around the base and the mining field."); return; }
        int n = Mathf.Min(batHeld, BatSlots - batBank); batHeld -= n; batBank += n;
        sim.battery = Mathf.Min(100f, sim.battery + 6f * n);
        Play(cPick, 0.9f); game.Buzz(0.8f, 0.2f); HunchFx.Spawn("star hit", batRack.position + Vector3.up * 1.2f, 0.6f, 0.9f);
        if (batBank >= BatSlots) { Banner("BATTERY BANK ONLINE", "All 8 modules are in. Stored power protects the base in a blackout.", false); sim.Say("ARIA: Battery Bank complete. It now trickle-charges the habitat and switches in if the power ever fails."); }
        else Note("Battery Bank: " + batBank + " of " + BatSlots + "  (+" + 6 * n + "% power now)");
    }

    void UpdateBank(float dt)
    {
        if (!batRack) return;
        for (int i = 0; i < BatSlots; i++) { var want = i < batBank ? batOn : batOff; if (batSlot[i].sharedMaterial != want) batSlot[i].sharedMaterial = want; }
        string s = "<b>BATTERY BANK</b>  " + batBank + " / " + BatSlots + (batHeld > 0 ? "\n<size=70%>click to put in " + batHeld + "</size>" : (batBank >= BatSlots ? "\n<size=70%>online</size>" : "\n<size=70%>bring battery modules</size>"));
        if (batLabel.text != s) batLabel.text = s;
        if (!Playing) return;
        if (batHeld > 0 && !ControlsLocked && (Feet() - batRack.position).sqrMagnitude < 2.0f * 2.0f) BankInsert();
        if (RealMission)
        {
            sim.battery = Mathf.Min(100f, sim.battery + batBank * 0.012f * dt);
            if (batBank >= BatSlots && sim.battery < 3f && Time.time > batRescue) { batRescue = Time.time + 300f; sim.battery = 45f; Banner("BATTERY BANK", "Stored power switched in: 45%", false); }
        }
    }

    // ------------------------------------------------------------------ things to read, and the Physics and Calculus sheet
    class ReadItem { public string title, zone, img; public Func<string> body; public bool got; }
    readonly List<ReadItem> reads = new List<ReadItem>(); int readOpen = -1; Transform readSheet; TextMeshPro readHead, readText, readFoot; float readLive, readOpenedAt; bool readWasTrig;
    int ReadCount() { int n = 0; foreach (var r in reads) if (r.got) n++; return n; }

    void BuildReads()
    {
        reads.Add(new ReadItem { title = "Physics and Calculus Reference Sheet", zone = "airlock", body = CalcSheet });
        reads.Add(new ReadItem { title = "Mission Brief", zone = "control", body = () =>
            "<b>NASA HUNCH: Lunar Smart Adaptive Habitat</b>\nwith AI psychological monitoring\n\n" +
            "Crew: 2 to 4 people (this base is built for 4)\nStay: 30 days or more\n\n" +
            "Your jobs, Commander:\n 1. Commission the 8 zones in NASA adjacency order.\n 2. Keep air, water, food and power above zero.\n 3. Answer every alert before its timer ends.\n 4. Look after the crew: mood, stress and sleep.\n 5. Attend the team meetings and vote on the plan.\n\n" +
            "ARIA, the habitat AI, watches the crew's vitals and mood and changes light, sound and schedule to help them before a small problem grows." });
        reads.Add(new ReadItem { title = "Crew Health Thresholds", zone = "medical", body = () =>
            "<b>When the health system raises a flag</b>\n\n" +
            "Vital              Caution           Danger\n" +
            "Heart rate    over 110 / under 48    over 130 / under 38 bpm\n" +
            "Blood oxygen   under 94 %      under 90 %\n" +
            "O2 flow        under 0.65      under 0.40 L/min\n" +
            "CO2            over 4          over 6 mmHg\n" +
            "Breathing    over 22 / under 9    over 28 / under 6 per min\n" +
            "Body temp      over 37.8 C     over 38.5 C\n\n" +
            "<b>What the AI does</b>\n Mild: a silent adjustment (light, sound, scent).\n Moderate: options are offered to the crew member.\n Severe: escalated to the flight surgeon and a team meeting is called.\n\n" +
            "Six conditions are watched: anxiety, depression, conflict, fatigue, loss of autonomy and sensory monotony." });
        reads.Add(new ReadItem { title = "Miner's Guide to the Moon", zone = "field", body = () =>
            "<b>What is in the ground</b>\n Water ice: drinking water, and oxygen when split.\n Ilmenite ore: iron, titanium and oxygen.\n Helium-3: fuel for fusion power.\n Moon crystals: left behind when an alien is beamed home.\n\n" +
            "<b>Deep veins</b> are worked in three stages. Each stage gives ore and the vein gets smaller.\n\n" +
            "<b>Ore gets richer with depth</b>\n grade G(d) = 1 - e^(-0.55 d)\n rate of change dG/dd = 0.55 e^(-0.55 d)\nSo digging deeper always helps, but each extra metre helps a little less.\n\n" +
            "The Moon mining pit map (through the Map Gate) lets you test this: the bottom layer holds the most." });
        reads.Add(new ReadItem { title = "Solar Panel Angles", zone = "power", body = () =>
            "<b>Getting the most from a solar panel</b>\n\nPower depends on the angle between the panel and the Sun:\n   P = P0 cos(theta)\n\nTo find the best angle, set the rate of change to zero:\n   dP/d(theta) = -P0 sin(theta) = 0   so   theta = 0\nA panel facing the Sun squarely gives full power.\n At 30 degrees off: 87 %.   At 60 degrees off: 50 %.\n\n" +
            "Energy is power added up over time:\n   E = integral of P(t) dt\nThe Solar farm map scores you on exactly this integral." });
        reads.Add(new ReadItem { title = "Battery Bank Manual", zone = "eclss", body = () =>
            "<b>Battery Bank</b>\n\nThe rack at the Power Plant has 8 slots: two battery modules for each of the 4 crew.\n\n 1. Find the glowing green modules around the base and the mining field.\n 2. You can carry 3 at a time.\n 3. Walk up to the rack, or click it, to slot them in.\n\nEach module adds 6 % power straight away and a slow trickle charge.\nWith all 8 in, the bank switches in by itself during a blackout.\n\n" +
            "Battery maths:  dB/dt = (P_in - P_load) / C\nIf more power comes in than goes out, the charge B rises." });
        reads.Add(new ReadItem { title = "Engineer Tanaka's Log", zone = "quarters", body = () =>
            "<b>Personal log, Eng. Tanaka</b>\n\nDay 6. Fixed the vent fan again. Dust gets everywhere.\n\nDay 9. Lunar night started. Fourteen Earth days of dark. The fission unit hums and the cables glow blue when I walk past.\n\nDay 11. ARIA noticed I was tired before I did. It dimmed the lights and moved my hard jobs to tomorrow. Clever machine.\n\nDay 12. Team meeting in the Galley. We voted for a rest hour. Rivera made cocoa. Best day so far.\n\nNote to self: jump height here is v0 squared over 2 g. I checked. Six times higher than at home." });
    }

    string CalcSheet()
    {
        float g = Mathf.Abs(Physics.gravity.y), v0 = desk ? desk.jumpSpeed : 2.7f;
        var sb = new StringBuilder();
        sb.Append("<b>MOTION ON THE MOON</b>   g = ").Append(g.ToString("F2")).Append(" m/s^2  (Earth 9.81)\n");
        sb.Append(" velocity v = dx/dt      acceleration a = dv/dt\n");
        sb.Append(" jump height h = v0^2 / (2 g) = ").Append((v0 * v0 / (2f * g)).ToString("F2")).Append(" m for v0 = ").Append(v0.ToString("F1")).Append(" m/s\n");
        sb.Append(" time in the air t = 2 v0 / g = ").Append((2f * v0 / g).ToString("F1")).Append(" s\n");
        sb.Append(" teleport arc: x = v cos(a) t,  y = v sin(a) t - (1/2) g t^2\n");
        sb.Append("<b>POWER AND ENERGY</b>\n");
        sb.Append(" energy is the integral of power: E = integral of P(t) dt\n");
        sb.Append(" battery: dB/dt = (P_in - P_load) / C\n");
        sb.Append(" solar panel: P = P0 cos(theta); best where dP/d(theta) = 0\n");
        sb.Append(" Dyson swarm: P = n P1, with n = ").Append(dysonCount).Append(" collector panels\n");
        sb.Append("<b>AIR</b>\n oxygen used: dO/dt = -k N  so  O(t) = O0 - k N t   (N = 4 crew)\n");
        sb.Append("<b>MINING</b>\n ore grade with depth: G(d) = 1 - e^(-0.55 d)\n");
        sb.Append("<b>CREW HEALTH</b>\n pulse wave y = sin(2 pi f t) + 0.35 sin(4 pi f t + 0.9), f = heart rate / 60\n alertness A(t) = cos(2 pi (t - 15) / 24), t in hours\n");
        sb.Append("<b>LIVE RIGHT NOW</b>\n");
        sb.Append(" your speed |dx/dt| = ").Append(calcSpeed.ToString("F1")).Append(" m/s\n");
        sb.Append(" battery ").Append(sim.battery.ToString("F0")).Append(" %,  dB/dt = ").Append((calcBatRate >= 0f ? "+" : "")).Append(calcBatRate.ToString("F2")).Append(" % per minute\n");
        sb.Append(" energy collected so far (integral of P dt) = ").Append(calcEnergy.ToString("F1")).Append(" units");
        return sb.ToString();
    }

    void BuildSheet()
    {
        readSheet = new GameObject("Reading sheet").transform;
        Prim(PrimitiveType.Cube, readSheet, new Vector3(0f, 0f, 0.012f), new Vector3(0.66f, 0.88f, 0.012f), Lit(new Color(0.75f, 0.58f, 0.18f)));
        Prim(PrimitiveType.Quad, readSheet, Vector3.zero, new Vector3(0.62f, 0.84f, 1f), Glow(new Color(0.92f, 0.89f, 0.78f)));
        readHead = Label(readSheet, "", 0f, 0.375f, 0.27f, 0.58f, 0.06f, new Color(0.5f, 0.26f, 0.02f));
        readText = Label(readSheet, "", 0f, -0.02f, 0.2f, 0.57f, 0.7f, new Color(0.05f, 0.07f, 0.2f)); readText.alignment = TextAlignmentOptions.TopLeft; readText.enableAutoSizing = true; readText.fontSizeMin = 0.07f; readText.fontSizeMax = 0.2f;
        readFoot = Label(readSheet, "", 0f, -0.395f, 0.15f, 0.58f, 0.04f, new Color(0.35f, 0.3f, 0.25f));
        readHead.enableAutoSizing = true; readHead.fontSizeMin = 0.1f; readHead.fontSizeMax = 0.27f;
        foreach (var r in readSheet.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
    }

    public void OpenRead(int i)
    {
        if (i < 0 || i >= reads.Count) return;
        if (!readSheet) BuildSheet();
        if (thirdPerson) SetThird(false);
        readOpen = i; readOpenedAt = Time.unscaledTime; readLive = 0f; readWasTrig = true;
        readHead.text = "<b>" + reads[i].title.ToUpper() + "</b>"; readText.text = reads[i].body(); ReadImg(reads[i].img);
        readFoot.text = game.HeadsetActive ? "Pull the trigger or press A to put it away" : "Press E, Space or click to put it away";
        if (game.HeadsetActive)
        {
            Vector3 f = FlatForward(); readSheet.SetParent(null, false);
            readSheet.SetPositionAndRotation(cam.position + f * 0.85f - Vector3.up * 0.08f, Quaternion.LookRotation(f, Vector3.up));
        }
        else { readSheet.SetParent(cam, false); readSheet.localPosition = new Vector3(0f, 0f, 0.74f); readSheet.localRotation = Quaternion.identity; }
        readSheet.localScale = Vector3.one; readSheet.gameObject.SetActive(true);
        if (reads[i].title.StartsWith("Physics")) sim.Say("ARIA: That is the Physics and Calculus sheet. The numbers at the bottom are live: they are measured from this game as you play.");
    }
    public void CloseRead() { if (readOpen < 0) return; readOpen = -1; if (readSheet) { readSheet.SetParent(null, false); readSheet.gameObject.SetActive(false); } wasHeld = true; }

    void UpdateRead(float dt)
    {
        // live numbers for the sheet: a derivative (speed, battery rate) and an integral (energy)
        calcT += dt;
        if (calcT >= 0.5f)
        {
            Vector3 f = Feet(); calcSpeed = Mathf.Lerp(calcSpeed, (f - calcLast).magnitude / calcT, 0.6f); calcLast = f;
            if (calcBatLast >= 0f) calcBatRate = Mathf.Lerp(calcBatRate, (sim.battery - calcBatLast) / calcT * 60f, 0.3f);
            calcBatLast = sim.battery; calcT = 0f;
        }
        if (Playing) calcEnergy += dysonCount * 0.05f * dt;
        if (readOpen < 0) return;
        if (!Playing || (game.menuOpen && Time.unscaledTime - readOpenedAt > 0.4f)) { CloseRead(); return; }      // a sheet opened FROM the menu survives the frame in which the menu is still closing
        readLive -= dt;
        if (readLive <= 0f && readOpen == 0) { readLive = 0.5f; string s = reads[0].body(); if (readText.text != s) readText.text = s; }
        if (Time.unscaledTime - readOpenedAt < 0.5f || autoOn) return;
        var kb = Keyboard.current; var mouse = Mouse.current; bool close = false;
        if (game.HeadsetActive)
        {
            bool trig = HunchVr.RightTrigger > 0.55f || HunchVr.LeftTrigger > 0.55f || HunchVr.A || HunchVr.B;
            close = trig && !readWasTrig; readWasTrig = trig;
            if ((cam.position - readSheet.position).sqrMagnitude > 2.5f * 2.5f) close = true;
        }
        else close = (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        if (close) CloseRead();
    }

    // ------------------------------------------------------------------ security cameras
    class CamPoint { public string name; public Vector3 pos; public Quaternion rot; public int map; }
    readonly List<CamPoint> cvPoints = new List<CamPoint>(); Camera cvCam; RenderTexture cvRt; public bool cvOn; int cvIndex; Transform cvScreen; TextMeshPro cvLabel; float cvOpenedAt; bool cvWasStick;

    void BuildCams()
    {
        var body = Lit(new Color(0.12f, 0.13f, 0.16f)); var lens = Glow(new Color(0.3f, 0.8f, 2.0f)); var led = Glow(new Color(2.2f, 0.3f, 0.3f));
        var holder = new GameObject("Security cameras").transform; holder.SetParent(root, false);
        foreach (var z in game.zones)
        {
            Vector3 c = z.position + Vector3.up * 2.3f, bestDir = Quaternion.Euler(0f, z.yaw + 180f, 0f) * Vector3.forward; float bestD = 0f; RaycastHit h;
            for (int k = 0; k < 8; k++)
            {
                Vector3 d = Quaternion.Euler(0f, z.yaw + k * 45f, 0f) * Vector3.forward; float dist = Cast(new Ray(c, d), out h, 4.5f) ? h.distance : 4.5f;
                // glass, doorways and furniture without solid colliders also stop the camera: it must stay inside the room it watches
                RaycastHit th; if (Physics.Raycast(c + d * 0.6f, d, out th, dist, ~0, QueryTriggerInteraction.Collide) && th.collider.isTrigger && !th.collider.GetComponentInParent<HunchTarget>()) dist = Mathf.Min(dist, th.distance + 0.6f);
                if (clutter != null) foreach (var cb in clutter) { float bd; if (!cb.Contains(c) && cb.IntersectRay(new Ray(c, d), out bd) && bd > 0f && bd < dist) dist = bd; }
                if (dist > bestD) { bestD = dist; bestDir = d; }
            }
            Vector3 p = c + bestDir * Mathf.Clamp(bestD * 0.75f, 0.4f, 3.2f);
            if (Cast(new Ray(p - Vector3.up * 0.3f, Vector3.up), out h, 0.6f)) p.y = h.point.y - 0.35f;
            AddCam(holder, z.title, p, z.position + Vector3.up * 1.0f, 0, body, lens, led);
        }
        if (fieldOk) AddCam(holder, "Mining field", fieldC + new Vector3(-13f, 9f, -13f), fieldC + Vector3.up, 0, body, lens, led);
        for (int k = 1; k < MapCount; k++) if (mapRoot[k]) AddCam(mapRoot[k], MapNames[k], mapC[k] + new Vector3(0f, 15f, -26f), mapC[k] + new Vector3(0f, 1f, 12f), k, body, lens, led);
        var go = new GameObject("Security camera view"); go.transform.SetParent(root, false);
        cvRt = new RenderTexture(960, 540, 24); cvRt.name = "CCTV";
        cvCam = go.AddComponent<Camera>(); cvCam.targetTexture = cvRt; cvCam.fieldOfView = 78f; cvCam.nearClipPlane = 0.08f; cvCam.farClipPlane = 4000f; cvCam.allowHDR = false; cvCam.depth = -5; cvCam.enabled = false;
        if (game.hudRoot)
        {
            var m = new Material(solidSrc); if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", cvRt); m.mainTexture = cvRt; m.SetColor("_BaseColor", Color.white);
            cvScreen = Prim(PrimitiveType.Quad, game.hudRoot, new Vector3(0f, 0.74f, 0f), new Vector3(0.96f, 0.54f, 1f), m).transform; cvScreen.name = "Camera screen";
            cvLabel = Label(cvScreen, "", 0f, 0f, 0.3f, 1f, 0.1f, Color.white); cvLabel.transform.localPosition = new Vector3(0f, -0.44f, -0.01f); cvLabel.transform.localScale = new Vector3(1f, 0.96f / 0.54f, 1f);
            cvScreen.gameObject.SetActive(false);
        }
    }
    void AddCam(Transform parent, string name, Vector3 pos, Vector3 lookAt, int map, Material body, Material lens, Material led)
    {
        var cp = new CamPoint { name = name, pos = pos, rot = Quaternion.LookRotation((lookAt - pos).normalized, Vector3.up), map = map }; cvPoints.Add(cp);
        var t = new GameObject("Camera: " + name).transform; t.SetParent(parent, true); t.SetPositionAndRotation(pos, cp.rot);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.22f), new Vector3(0.14f, 0.12f, 0.24f), body); Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0f, -0.09f), new Vector3(0.08f, 0.02f, 0.08f), lens).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Prim(PrimitiveType.Sphere, t, new Vector3(0.05f, 0.075f, -0.14f), Vector3.one * 0.025f, led);
    }
    bool CvValid(int i) { return i >= 0 && i < cvPoints.Count && (cvPoints[i].map == 0 || cvPoints[i].map == mapNow); }

    public void ToggleCams() { SetCams(!cvOn); }
    public void SetCams(bool on)
    {
        if (!cvCam || cvPoints.Count == 0) return;
        if (on && (!Playing || autoOn && false)) return;
        cvOn = on; cvCam.enabled = on; cvOpenedAt = Time.unscaledTime;
        if (cvScreen) cvScreen.gameObject.SetActive(on && game.HeadsetActive);
        if (on)
        {
            if (thirdPerson) SetThird(false);
            if (!CvValid(cvIndex)) cvIndex = 0;
            if (mapNow != 0) for (int i = 0; i < cvPoints.Count; i++) if (cvPoints[i].map == mapNow) cvIndex = i;
            CvApply(); Play(cPick, 0.5f);
        }
    }
    public void NextCam(int step)
    {
        if (!cvOn) { SetCams(true); return; }
        for (int n = 0; n < cvPoints.Count; n++) { cvIndex = (cvIndex + step + cvPoints.Count) % cvPoints.Count; if (CvValid(cvIndex)) break; }
        CvApply(); Play(cPick, 0.4f);
    }
    void CvApply()
    {
        var p = cvPoints[cvIndex]; cvCam.transform.SetPositionAndRotation(p.pos + p.rot * Vector3.forward * 0.05f, p.rot);
        if (cvLabel) cvLabel.text = "<b>CAM " + (cvIndex + 1) + " of " + cvPoints.Count + "</b>   " + p.name + "   <color=#FF6A5A>REC</color>";
    }
    void UpdateCams()
    {
        var kb = Keyboard.current; bool vr = game.HeadsetActive;
        if (kb != null && kb.zKey.wasPressedThisFrame && Playing && !game.menuOpen && !vr && !autoOn && readOpen < 0) ToggleCams();
        if (!cvOn) return;
        if (!Playing || game.menuOpen) { SetCams(false); return; }
        if (vr || Time.unscaledTime - cvOpenedAt < 0.25f) return;
        var mouse = Mouse.current;
        if (kb != null)
        {
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame || (mouse != null && mouse.leftButton.wasPressedThisFrame)) NextCam(1);
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame)) NextCam(-1);
            else if (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) SetCams(false);
        }
    }

    // ------------------------------------------------------------------ the Crew Health System (from the NASA HUNCH crew-health page)
    class HsCrew { public float hr = 66f, spo2 = 98f, flow = 0.85f, co2 = 2.6f, resp = 14f, temp = 36.7f, risk, inten, alarmAt; public int cond = -1, state, level, pick; public string cls = "Neutral"; public LineRenderer pulse, breath; public TextMeshPro card; public Renderer lamp; }
    HsCrew[] hs; Transform hsWall; TextMeshPro hsFoot; float hsTick; readonly List<string> hsLog = new List<string>(); int hsSel, hsZone = -1; Material hsGreen, hsAmber, hsRed, hsWaveG, hsWaveR;
    static readonly string[] HsCond = { "Anxiety", "Depression", "Interpersonal conflict", "Cognitive fatigue", "Autonomy deprivation", "Sensory monotony" };
    static readonly string[] HsAuto =
    {
        "lights to warm 2400 K amber, calm soundscape, 23 C, lavender", "bright 5000 K morning light, nature scenes on the virtual window", "more sound separation, meal times staggered",
        "more airflow, slower task pacing, cooler light", "schedule choices unlocked for the next block", "new soundscape, citrus scent, changed window scenes"
    };
    static readonly string[][] HsOffer =
    {
        new[] { "Guided breathing session", "Quiet time in a private pod", "Call home / Earth view" },
        new[] { "Greenhouse plant-care session", "Scheduled call with family", "Shared meal with a crewmate" },
        new[] { "Stagger the schedule for 24 h", "Facilitated debrief with the Commander", "Solo exercise block" },
        new[] { "20-minute rest break now", "Hand off the high-stakes task", "Earlier sleep period tonight" },
        new[] { "Choose your next 2 h of tasks", "Free personal block", "Reorder tomorrow's schedule" },
        new[] { "VR nature walk", "Greenhouse hobby time", "Rearrange the galley layout" }
    };
    static readonly string[] HsState = { "", "silent adjustment running", "options offered - choose one", "ESCALATED to the flight surgeon", "chosen option in progress", "handled at the team meeting" };

    void HsNote(string who, string msg) { linkSeq++; linkLog.Insert(0, "{\"n\":" + linkSeq + ",\"who\":\"" + Js(who) + "\",\"msg\":\"" + Js(msg) + "\"}"); if (linkLog.Count > 20) linkLog.RemoveAt(linkLog.Count - 1); hsLog.Insert(0, "Day " + sim.Day + " " + Mathf.FloorToInt(sim.Hour).ToString("00") + ":00  " + who + ": " + msg); if (hsLog.Count > 30) hsLog.RemoveAt(hsLog.Count - 1); }

    void BuildHealth()
    {
        hs = new HsCrew[sim.crew.Count]; for (int i = 0; i < hs.Length; i++) hs[i] = new HsCrew { pick = i % HsCond.Length, hr = sim.crew[i].HeartRate };
        hsGreen = Glow(new Color(0.4f, 1.9f, 0.6f)); hsAmber = Glow(new Color(2.0f, 1.4f, 0.3f)); hsRed = Glow(new Color(2.2f, 0.35f, 0.35f)); hsWaveG = Glow(new Color(0.3f, 2.0f, 1.2f)); hsWaveR = Glow(new Color(2.4f, 0.4f, 0.5f));
        Vector3 pos, dir;
        if (!WallSpot("medical,control,work,galley", out pos, out dir, out hsZone)) return;
        hsWall = new GameObject("Crew Health System").transform; hsWall.SetParent(root, false); hsWall.position = pos; hsWall.rotation = Quaternion.LookRotation(dir, Vector3.up);
        var dark = Lit(new Color(0.03f, 0.05f, 0.12f)); var edge = Glow(new Color(0.2f, 1.2f, 1.6f)); var cardMat = Lit(new Color(0.07f, 0.1f, 0.24f)); var post = Lit(new Color(0.3f, 0.32f, 0.36f));
        Prim(PrimitiveType.Cube, hsWall, new Vector3(-1.0f, 0.3f, 0.1f), new Vector3(0.08f, 0.6f, 0.08f), post); Prim(PrimitiveType.Cube, hsWall, new Vector3(1.0f, 0.3f, 0.1f), new Vector3(0.08f, 0.6f, 0.08f), post);
        var frame = Prim(PrimitiveType.Cube, hsWall, new Vector3(0f, 1.45f, 0.08f), new Vector3(2.56f, 1.76f, 0.08f), dark, true);
        var tg = frame.AddComponent<HunchTarget>(); tg.kind = 44;
        Prim(PrimitiveType.Cube, hsWall, new Vector3(0f, 2.34f, 0.07f), new Vector3(2.6f, 0.03f, 0.1f), edge); Prim(PrimitiveType.Cube, hsWall, new Vector3(0f, 0.56f, 0.07f), new Vector3(2.6f, 0.03f, 0.1f), edge);
        Label(hsWall, "<b>NASA HUNCH CREW HEALTH SYSTEM</b>   <size=70%>Lunar Smart Adaptive Habitat</size>", 0f, 2.23f, 0.8f, 2.45f, 0.12f, new Color(0.45f, 0.95f, 1f));
        for (int i = 0; i < hs.Length && i < 4; i++)
        {
            float cx = i % 2 == 0 ? -0.63f : 0.63f, cy = i < 2 ? 1.8f : 1.04f; var h = hs[i];
            Prim(PrimitiveType.Cube, hsWall, new Vector3(cx, cy, 0.035f), new Vector3(1.2f, 0.7f, 0.02f), cardMat);
            h.card = Label(hsWall, "", cx, cy + 0.1f, 0.4f, 1.12f, 0.46f, Color.white); h.card.alignment = TextAlignmentOptions.TopLeft;
            h.lamp = Prim(PrimitiveType.Sphere, hsWall, new Vector3(cx + 0.52f, cy + 0.28f, 0.01f), Vector3.one * 0.06f, hsGreen).GetComponent<Renderer>();
            h.pulse = Line("Pulse wave", hsWaveG, 0.012f, hsWall); h.pulse.useWorldSpace = false; h.pulse.positionCount = 48; h.pulse.transform.localPosition = new Vector3(cx, cy - 0.17f, 0.0f);
            h.breath = Line("Breathing wave", Glow(new Color(0.4f, 0.9f, 2.2f)), 0.01f, hsWall); h.breath.useWorldSpace = false; h.breath.positionCount = 48; h.breath.transform.localPosition = new Vector3(cx, cy - 0.28f, 0.0f);
        }
        hsFoot = Label(hsWall, "", 0f, 0.63f, 0.5f, 2.45f, 0.1f, Color.white);
    }

    static int Lv(float v, float cHi, float dHi, float cLo, float dLo) { return v > dHi || v < dLo ? 2 : (v > cHi || v < cLo ? 1 : 0); }

    void HsStep()
    {
        for (int i = 0; i < hs.Length; i++)
        {
            var h = hs[i]; var c = sim.crew[i]; if (hunchOn && i < 4 && !hunchConsent[i]) { h.cond = -1; h.inten = 0f; h.state = 0; }      // monitoring switched off by this crew member
            float sev = h.cond >= 0 ? h.inten : 0f;
            // natural causes, from the simulator's own crew model
            if (RealMission)
            {
                float anx = Mathf.Clamp01((c.stress - 40f) / 50f), dep = Mathf.Clamp01((55f - c.mood) / 45f), fat = Mathf.Clamp01((50f - c.sleep) / 45f);
                int nc = anx >= dep && anx >= fat ? 0 : (dep >= fat ? 1 : 3); float ni = Mathf.Max(anx, Mathf.Max(dep, fat));
                if (ni >= 0.35f && ni > h.inten + 0.05f && h.state != 4 && h.state != 5) { h.cond = nc; h.inten = ni; }
            }
            // vitals
            float hrT = c.HeartRate + (h.cond == 0 ? 34f * sev : 0f) + (h.cond == 3 ? 8f * sev : 0f);
            h.hr = Mathf.Lerp(h.hr, hrT, 0.3f) + R(-1.2f, 1.2f);
            float o2 = Mathf.Clamp01(sim.oxygen / 100f);
            h.spo2 = Mathf.Clamp(Mathf.Lerp(h.spo2, 98.4f - (1f - o2) * 16f - (ventOn ? 0f : 1.2f), 0.3f) + R(-0.15f, 0.15f), 70f, 100f);
            h.flow = Mathf.Lerp(h.flow, 0.86f * o2 * (sim.eclssOk ? 1f : 0.7f), 0.3f);
            h.co2 = Mathf.Lerp(h.co2, 2.6f + (ventOn ? 0f : 1.5f) + (1f - o2) * 5f + (sim.eclssOk ? 0f : 1.3f), 0.3f) + R(-0.04f, 0.04f);
            h.resp = Mathf.Lerp(h.resp, 14f + c.stress * 0.07f + (h.cond == 0 ? 7f * sev : 0f) + (h.co2 > 4f ? 3f : 0f), 0.3f) + R(-0.2f, 0.2f);
            h.temp = Mathf.Lerp(h.temp, 36.7f + (100f - c.health) * 0.022f, 0.3f) + R(-0.02f, 0.02f);
            int lv = Mathf.Max(Lv(h.hr, 110f, 130f, 48f, 38f), Mathf.Max(Lv(h.spo2, 999f, 999f, 94f, 90f), Mathf.Max(Lv(h.flow, 999f, 999f, 0.65f, 0.40f), Mathf.Max(Lv(h.co2, 4f, 6f, -9f, -9f), Mathf.Max(Lv(h.resp, 22f, 28f, 9f, 6f), Lv(h.temp, 37.8f, 38.5f, -9f, -9f))))));
            // the overall class: sentiment and stress carry most of the weight, as in the project's model
            float sent = Mathf.Clamp(c.mood / 50f - 1f, -1f, 1f);
            h.risk = Mathf.Clamp01(0.6f * (1f - sent) / 2f + 0.4f * c.stress / 100f + 0.25f * sev);
            h.cls = h.risk >= 0.58f || sev >= 0.75f ? "Suspected Illness" : (h.risk >= 0.42f || sev >= 0.55f ? "Underlying Issue" : "Neutral");
            if (sev >= 0.55f) lv = Mathf.Max(lv, sev >= 0.75f ? 2 : 1);
            if (lv == 2 && h.level < 2 && Time.time > h.alarmAt) { h.alarmAt = Time.time + 40f; HsNote(c.name, "critical reading - check the health wall"); if (Playing && mapNow == 0) Note("Crew Health: " + c.name + " has a critical reading."); }
            h.level = lv;
            // the three-step response
            if (h.cond < 0) continue;
            h.inten *= h.state == 1 || h.state == 4 || h.state == 5 ? 0.94f : 0.9985f;
            if (h.inten < 0.06f)
            {
                if (h.state != 0) HsNote(c.name, HsCond[h.cond] + ": resolved, environment restored gradually");
                h.cond = -1; h.state = 0; h.inten = 0f; continue;
            }
            int tier = h.inten >= 0.75f ? 3 : (h.inten >= 0.55f ? 2 : (h.inten >= 0.35f ? 1 : 0));
            if (tier == 3 && h.state != 3 && h.state != 5 && h.state != 4)
            {
                h.state = 3; HsNote(c.name, HsCond[h.cond] + ": escalated to the flight surgeon (severe signal pattern)");
                CallMeeting(c.name + " needs the team's support: " + HsCond[h.cond].ToLower() + ".", i);
            }
            else if (tier == 2 && (h.state == 0 || h.state == 1))
            {
                h.state = 2; HsNote(c.name, HsCond[h.cond] + ": moderate. Options offered to the crew member");
                if (Playing) { Banner("CREW HEALTH", c.name + ": options offered" + (game.HeadsetActive ? " - HEALTH button" : " - press F2"), true); sim.Say("ARIA: " + c.name + " shows signs of " + HsCond[h.cond].ToLower() + ". I have three options ready in the Crew Health System."); }
            }
            else if (tier == 1 && h.state == 0)
            {
                h.state = 1; c.stress -= 3f; c.mood += 2f; c.Clamp(); HsNote(c.name, HsCond[h.cond] + ": early signs. Silent adjustment: " + HsAuto[h.cond]);
            }
        }
    }

    void HsInject(int i, float level)
    {
        if (i < 0 || i >= hs.Length) return; var h = hs[i];
        h.cond = h.pick; h.inten = level; h.state = 0; HsNote(sim.crew[i].name, "simulated " + HsCond[h.cond].ToLower() + " injected (" + (level < 0.5f ? "mild" : (level < 0.7f ? "moderate" : "severe")) + ")");
        HsStep();
    }
    void HsAccept(int i, int option)
    {
        if (i < 0 || i >= hs.Length) return; var h = hs[i]; if (h.cond < 0) return; var c = sim.crew[i];
        h.state = 4; c.mood += 8f; c.stress -= 10f; c.Clamp(); sim.psychHelped++;
        HsNote(c.name, HsCond[h.cond] + ": chose \"" + HsOffer[h.cond][option] + "\"");
        Note(c.name + " starts: " + HsOffer[h.cond][option]); Play(cPick, 0.7f);
    }
    string HsLine(int i)
    {
        var h = hs[i];
        return "HR " + Mathf.RoundToInt(h.hr) + "   SpO2 " + Mathf.RoundToInt(h.spo2) + "%   CO2 " + h.co2.ToString("F1") + "   Resp " + Mathf.RoundToInt(h.resp) + "   " + h.temp.ToString("F1") + " C";
    }
    void HsCounts(out int ok, out int warn, out int bad) { ok = warn = bad = 0; if (hs == null) return; foreach (var h in hs) { if (h.level == 2) bad++; else if (h.level == 1) warn++; else ok++; } }

    void UpdateHealth(float dt)
    {
        if (hs == null) return;
        if (Playing && !game.Paused) { hsTick += dt; if (hsTick >= 1f) { hsTick = 0f; HsStep(); } }
        if (kbPressed(Key.F2) && Playing && !game.menuOpen && !ControlsLocked && menu) menu.OpenExtra(pageHealth);
        if (!hsWall || (cam.position - hsWall.position).sqrMagnitude > 16f * 16f) return;
        int ok, warn, bad; HsCounts(out ok, out warn, out bad);
        for (int i = 0; i < hs.Length && i < 4; i++)
        {
            var h = hs[i]; var c = sim.crew[i]; if (!h.card) continue;
            string col = h.level == 2 ? "FF7A6A" : (h.level == 1 ? "FFD070" : "7CFC9A");
            string s = "<b>" + c.name + "</b>  <size=75%>" + c.role + "</size>\n<size=85%>HR " + Mathf.RoundToInt(h.hr) + " bpm   SpO2 " + Mathf.RoundToInt(h.spo2) + "%   Resp " + Mathf.RoundToInt(h.resp) + "\nCO2 " + h.co2.ToString("F1") + " mmHg   O2 flow " + h.flow.ToString("F2") + "   " + h.temp.ToString("F1") + " C\n<color=#" + col + "><b>" + h.cls + "</b></color>" + (h.cond >= 0 ? "  " + HsCond[h.cond] + " - " + HsState[h.state] : "") + "</size>";
            if (h.card.text != s) h.card.text = s;
            var lm = h.level == 2 ? hsRed : (h.level == 1 ? hsAmber : hsGreen); if (h.lamp.sharedMaterial != lm) h.lamp.sharedMaterial = lm;
            var wm = h.hr > c.HeartRate + 22f || h.level == 2 ? hsWaveR : hsWaveG; if (h.pulse.sharedMaterial != wm) h.pulse.sharedMaterial = wm;
            float f = h.hr / 60f, fr = h.resp / 60f, t0 = Time.time;
            for (int k = 0; k < 48; k++)
            {
                float x = (k / 47f - 0.5f) * 1.08f, tt = t0 + k * 0.045f;
                h.pulse.SetPosition(k, new Vector3(x, (Mathf.Sin(2f * Mathf.PI * f * tt) + 0.35f * Mathf.Sin(4f * Mathf.PI * f * tt + 0.9f)) * 0.04f, -0.012f));
                h.breath.SetPosition(k, new Vector3(x, Mathf.Cos(2f * Mathf.PI * fr * (t0 + k * 0.2f)) * 0.03f, -0.012f));
            }
        }
        string foot = "<color=#7CFC9A>" + ok + " nominal</color>   <color=#FFD070>" + warn + " caution</color>   <color=#FF7A6A>" + bad + " critical</color>     green line: pulse   blue line: breathing     aim and click to open";
        if (hsFoot.text != foot) hsFoot.text = foot;
    }
    static bool kbPressed(Key k) { var kb = Keyboard.current; return kb != null && kb[k].wasPressedThisFrame; }

    // ------------------------------------------------------------------ team meetings
    public int mtState, mtCount; float mtNext = 150f, mtLeft, mtClosed; string mtWhy = ""; int mtCrew = -1, mtZone = -1; Transform mtBell, mtHolo; Vector3 mtSpot; readonly int[] mtVote = new int[4];
    static readonly string[] MtOption = { "Rest and recreation hour", "Maintenance blitz", "Stock up on supplies" };
    static readonly string[] MtShort = { "Rest and fun", "Maintenance", "Supplies" };
    static readonly string[] MtEffect = { "Everyone's mood goes up and stress goes down.", "Dust is cleaned, the solar arrays and the batteries get a boost.", "More food and water for the base, and two food packs for you." };

    void BuildMeeting()
    {
        Vector3 pos, dir;
        if (!WallSpot("galley,quarters,sanctuary,control", out pos, out dir, out mtZone)) { mtZone = Mathf.Max(0, game.ZoneIndex("galley")); pos = game.zones[mtZone].position; dir = Vector3.forward; }
        mtSpot = pos;
        mtBell = new GameObject("Meeting bell").transform; mtBell.SetParent(root, false); mtBell.position = pos; mtBell.rotation = Quaternion.LookRotation(dir, Vector3.up);
        Prim(PrimitiveType.Cylinder, mtBell, new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.45f, 0.5f), Lit(new Color(0.3f, 0.32f, 0.36f)));
        Prim(PrimitiveType.Cylinder, mtBell, new Vector3(0f, 0.92f, 0f), new Vector3(0.62f, 0.03f, 0.62f), Lit(new Color(0.95f, 0.74f, 0.22f)));
        var dome = Prim(PrimitiveType.Sphere, mtBell, new Vector3(0f, 0.98f, 0f), new Vector3(0.42f, 0.3f, 0.42f), Glow(new Color(1.9f, 1.3f, 0.3f)), true);
        dome.GetComponent<SphereCollider>().radius = 1.0f; var tg = dome.AddComponent<HunchTarget>(); tg.kind = 43;
        mapLabels.Add(Label(mtBell, "<b>TEAM MEETING BELL</b>\n<size=60%>press to call the crew</size>", 0f, 1.75f, 1.3f, 3f, 0.6f, new Color(1f, 0.84f, 0.3f)));
        // four see-through crew figures that appear while a meeting is on
        mtHolo = new GameObject("Meeting crew").transform; mtHolo.SetParent(root, false); mtHolo.position = pos;
        Vector3 inward = -dir; Color[] cc = { new Color(0.3f, 0.8f, 1f, 0.55f), new Color(0.4f, 1f, 0.6f, 0.55f), new Color(1f, 0.8f, 0.3f, 0.55f), new Color(0.9f, 0.5f, 1f, 0.55f) };
        for (int i = 0; i < 4 && i < sim.crew.Count; i++)
        {
            Vector3 p = inward * 1.4f + Quaternion.Euler(0f, -60f + i * 40f, 0f) * inward * 1.3f;
            var fig = new GameObject("Crew " + (i + 1)).transform; fig.SetParent(mtHolo, false); fig.localPosition = p;
            Prim(PrimitiveType.Capsule, fig, new Vector3(0f, 0.75f, 0f), new Vector3(0.45f, 0.55f, 0.35f), Clear(cc[i])); Prim(PrimitiveType.Sphere, fig, new Vector3(0f, 1.5f, 0f), Vector3.one * 0.34f, Clear(cc[i]));
            mapLabels.Add(Label(fig, "<b>" + sim.crew[i].name + "</b>", 0f, 1.95f, 0.9f, 2f, 0.3f, Color.white));
        }
        mtHolo.gameObject.SetActive(false);
        if (menu)
        {
            pageMeet = new HunchMenu.ExtraPage { title = "TEAM MEETING" };
            pageMeet.build = m =>
            {
                m.AddItem("<b>Team meeting: 4 votes</b>", mtWhy + "\n\n" + "Everyone reports, then the team votes on what to do next. Four votes in all: the three crew and you. Nothing is decided until you vote.", () => { });
                int me = Mathf.Clamp(playAs, 0, 3);
                for (int i = 0; i < sim.crew.Count && i < 4; i++) { int ii = i; m.AddItem(i == me ? sim.crew[i].name + ": your vote is below" : sim.crew[i].name + ": votes " + MtShort[mtVote[ii]], (i == me ? "This is you. " : "") + "\"" + MtReport(i) + "\"" + (i == me ? "\n\nVote with one of the three lines below." : "\n\nVotes for: " + MtOption[mtVote[ii]]), () => { }); }
                for (int o = 0; o < 3; o++)
                {
                    int oo = o, n = 0; for (int i = 0; i < 4 && i < sim.crew.Count; i++) if (i != me && mtVote[i] == o) n++;
                    m.AddItem("VOTE: " + MtShort[o] + "   (" + n + " crew)", MtEffect[o] + "\n\nNothing is decided until you vote. If the votes are tied, yours decides.", () => { m.Close(); MeetingVote(oo); });
                }
            };
        }
        A("Team player", "Take part in a team meeting and vote.", () => mtCount > 0);
    }

    string MtReport(int i)
    {
        var c = sim.crew[i]; var h = hs != null && i < hs.Length ? hs[i] : null;
        if (h != null && h.cond >= 0 && h.inten > 0.3f) return "I could use some help with " + HsCond[h.cond].ToLower() + ".";
        if (c.stress > 55f) return "I am stressed. Stress " + Mathf.RoundToInt(c.stress) + ".";
        if (c.sleep < 45f) return "I did not sleep well.";
        if (c.mood < 55f) return "I feel a bit low today.";
        if (sim.battery < 50f) return "I am worried about power: " + Mathf.RoundToInt(sim.battery) + "%.";
        if (sim.food < 55f || sim.water < 55f) return "Supplies are getting low.";
        return "All good here. Ready for work.";
    }
    int MtWant(int i)
    {
        var c = sim.crew[i];
        if (c.stress > 50f || c.mood < 55f || c.sleep < 45f) return 0;
        if (sim.battery < 60f || sim.dust > 30f || sim.arrays < 80f) return 1;
        if (sim.food < 60f || sim.water < 60f) return 2;
        return (i + sim.Day) % 3;
    }
    public int MeetingBest() { int[] n = new int[3]; for (int i = 0; i < 4 && i < sim.crew.Count; i++) n[mtVote[i]]++; return n[0] >= n[1] && n[0] >= n[2] ? 0 : (n[1] >= n[2] ? 1 : 2); }

    public void CallMeeting(string why, int crew)
    {
        if (mtState != 0 || !Playing || sim.phase != HabPhase.Mission) return;
        mtState = 1; mtLeft = 75f; mtWhy = why; mtCrew = crew;
        if (meetTable)
        {
            Banner("TEAM MEETING", "Everyone to the round table!  You are brought there in a moment.", true);
            sim.Say("ARIA: Team meeting. " + why + " You are brought to the round table in the centre dome. The clock stops while you talk and vote.");
        }
        else
        {
            Banner("TEAM MEETING", "Everyone to the Galley!" + (game.HeadsetActive ? "  MEET button" : "  Press F4"), true);
            sim.Say("ARIA: Team meeting. " + why + " Come to the Galley. " + (game.HeadsetActive ? "Point at MEET on the dashboard to go straight there." : "Press F4 to go straight there."));
        }
    }
    /// the meeting key / button / bell: call one, or go to the one that has been called
    public void MeetingGo()
    {
        if (!Playing) return;
        if (mtState == 0) { CallMeeting("You called the team together.", -1); if (mtState == 0) { Note("Team meetings start once the mission is running."); return; } }
        if (mtState == 2) { if (menu && pageMeet != null && !game.menuOpen) menu.OpenExtra(pageMeet); return; }
        if (!MeetBeam())
        {
            if (mapNow != 0) GoMap(0);
            if ((Feet() - mtSpot).sqrMagnitude > 4f * 4f) game.GoTo(mtZone);
        }
        StartMeeting();
    }
    void StartMeeting()
    {
        mtState = 2; mtClosed = 0f;
        for (int i = 0; i < 4 && i < sim.crew.Count; i++) mtVote[i] = MtWant(i);
        mtHolo.gameObject.SetActive(!meetCentre); Play(cWin, 0.5f);      // at the round table the crew sit in their chairs; the see-through figures are only for the Galley bell
        if (menu && pageMeet != null && !autoOn) menu.OpenExtra(pageMeet);
    }
    public void MeetingVote(int option)
    {
        if (mtState != 2) return;
        // four votes in all: the three crew you are not playing, and you. Your own crew member's guess is not counted when you vote yourself.
        int me = Mathf.Clamp(playAs, 0, 3); int[] n = new int[3];
        for (int i = 0; i < 4 && i < sim.crew.Count; i++) { if (option >= 0 && i == me) continue; n[mtVote[i]]++; }
        if (option >= 0) n[option]++;
        int top = Mathf.Max(n[0], Mathf.Max(n[1], n[2]));
        int win = option >= 0 && n[option] == top ? option : (n[0] == top ? 0 : (n[1] == top ? 1 : 2));
        switch (win)
        {
            case 0: foreach (var c in sim.crew) { c.mood += 8f; c.stress -= 10f; c.Clamp(); } break;
            case 1: sim.dust = Mathf.Max(0f, sim.dust - 15f); sim.arrays = Mathf.Min(100f, sim.arrays + 10f); sim.battery = Mathf.Min(100f, sim.battery + 10f); break;
            case 2: sim.food = Mathf.Min(100f, sim.food + 12f); sim.water = Mathf.Min(100f, sim.water + 12f); food = Mathf.Min(12, food + 2); break;
        }
        if (mtCrew >= 0 && hs != null && mtCrew < hs.Length && hs[mtCrew].cond >= 0) { hs[mtCrew].state = 5; var c = sim.crew[mtCrew]; c.mood += 6f; c.stress -= 8f; c.Clamp(); HsNote(c.name, "supported at the team meeting"); }
        mtState = 0; mtCount++; mtNext = R(300f, 420f); mtHolo.gameObject.SetActive(false); meetPressCool = Time.time + 4f;      // the click that voted must not start the next meeting
        Banner("MEETING OVER", "The team chose: " + MtOption[win], false);
        sim.Say("ARIA: Meeting over. The team voted " + n[0] + " / " + n[1] + " / " + n[2] + " and chose " + MtOption[win].ToLower() + ". " + MtEffect[win]);
    }

    /// a meeting that ends with nothing decided: you left, or nobody came
    public void MeetingCancel(string why)
    {
        if (mtState == 0) return;
        mtState = 0; mtNext = 240f; mtHolo.gameObject.SetActive(false); meetCentre = false; meetPressCool = Time.time + 2f;
        Banner("MEETING ENDED", why, false); sim.Say("ARIA: " + why);
    }
    void UpdateMeeting(float dt)
    {
        if (!mtBell) return;
        if (kbPressed(Key.F4) && Playing && !game.menuOpen && !ControlsLocked) MeetingGo();
        if (!Playing) return;
        if (mtState == 0)
        {
            if (sim.phase == HabPhase.Mission && mapNow == 0 && !game.menuOpen && !game.Paused) { mtNext -= dt; if (mtNext <= 0f) { mtNext = 300f; CallMeeting("It is time for the team check-in.", -1); } }
        }
        else if (mtState == 1)
        {
            mtLeft -= dt;
            if (!autoOn && !game.menuOpen && meetTable && mtLeft < 71f) { MeetBeam(); StartMeeting(); }      // four seconds after the call you are brought to the table: the one jump that is always allowed
            else if (!autoOn && mapNow == 0 && (Feet() - mtSpot).sqrMagnitude < 3.2f * 3.2f && !game.menuOpen) StartMeeting();
            else if (mtLeft <= 0f)
            {
                if (autoOn) { for (int i = 0; i < 4 && i < sim.crew.Count; i++) mtVote[i] = MtWant(i); mtState = 2; MeetingVote(-1); }      // the autopilot is one of the four: the crew decide
                else MeetingCancel("Nobody came to the table, so the meeting is put off. Nothing was decided.");
            }
        }
        else if (mtState == 2 && !autoOn)
        {
            // closing the page without voting leaves the decision to the crew
            // closing the page decides nothing: the meeting waits for your vote
            if (game.menuOpen) mtClosed = 0f; else { float was = mtClosed; mtClosed += dt; if (was <= 1.2f && mtClosed > 1.2f) Note("The meeting is waiting for your vote. " + (game.HeadsetActive ? "Press the red button" : "Press the red button or F4") + " to vote, or walk away to end it."); }
        }
    }

    // ------------------------------------------------------------------ anything at 0%: bring the player back, restore everything, carry on
    public int sysResets; float batZero;
    void UpdateReset(float dt)
    {
        if (sim.timeFrozen) return;
        bool failed = sim.missionFailed && sim.phase == HabPhase.Report;
        if (sim.phase != HabPhase.Mission && !failed) return;
        string why = "";
        if (failed) why = sim.failReason;
        else if (sim.oxygen < 1f) why = "The air ran out.";
        else if (sim.water < 1f) why = "The water ran out.";
        else if (sim.food < 1f) why = "The food ran out.";
        else foreach (var c in sim.crew) if (c.health < 1f) { why = c.name + " needed urgent care."; break; }
        if (why.Length == 0)
        {
            if (sim.battery < 1f) { batZero += dt; if (batZero > 20f) why = "The batteries were flat for too long."; } else batZero = 0f;
        }
        if (why.Length == 0) return;
        batZero = 0f; sysResets++;
        sim.missionFailed = false; sim.failReason = ""; sim.phase = HabPhase.Mission; sim.alerts.Clear();
        sim.oxygen = 100f; sim.water = 100f; sim.food = 100f; sim.battery = 100f; sim.dust = Mathf.Min(sim.dust, 10f); sim.arrays = Mathf.Max(sim.arrays, 90f);
        sim.eclssOk = true; sim.waterOk = true; sim.commsOk = true; sim.circadianOk = true; sim.leak = false;
        foreach (var c in sim.crew) { c.health = Mathf.Max(c.health, 85f); c.mood = Mathf.Max(c.mood, 70f); c.stress = Mathf.Min(c.stress, 30f); c.sleep = Mathf.Max(c.sleep, 70f); }
        if (hs != null) foreach (var h in hs) { h.cond = -1; h.state = 0; h.inten = 0f; }
        health = 100f; hunger = 100f; food = Mathf.Max(food, 3);
        if (mapNow != 0) GoMap(0);
        if (ride != null) Dismount(null);
        int ai = game.ZoneIndex("airlock"); game.GoTo(ai >= 0 ? ai : 0);
        Banner("SYSTEMS RESET", "Everything is restored. The mission continues!", true);
        sim.Say("ARIA: " + why + " I have reset the habitat systems and brought you back to the airlock. Day " + sim.Day + " carries on.");
    }

    // ------------------------------------------------------------------ menu page, VR buttons, keys
    void BuildBatch7()
    {
        BuildReads();
        try { BuildMaps(); } catch (Exception e) { HLog.E("Crew", "maps: " + e); }
        try { BuildRichNodes(); } catch (Exception e) { HLog.E("Crew", "rich nodes: " + e); }
        try { BuildBank(); BuildHealth(); BuildMeeting(); BuildPicks(); BuildCams(); } catch (Exception e) { HLog.E("Crew", "base systems: " + e); }
        if (!musicRef) musicRef = FindFirstObjectByType<HunchMusic>();
        calcLast = Feet();
        if (menu)
        {
            pageBase = new HunchMenu.ExtraPage { title = "BASE SYSTEMS", label = "Base systems: health, cameras, meeting, batteries, music", hint = "The Crew Health System, the security cameras, team meetings, the Battery Bank, things you have picked up to read, the music, the player and the autopilot." };
            pageHealth = new HunchMenu.ExtraPage { title = "CREW HEALTH SYSTEM", parent = pageBase };
            pageHealthCrew = new HunchMenu.ExtraPage { title = "CREW MEMBER", parent = pageHealth };
            pageRead = new HunchMenu.ExtraPage { title = "THINGS TO READ", parent = pageBase };
            pageBase.build = m =>
            {
                int ok, warn, bad; HsCounts(out ok, out warn, out bad); bool vr = game.HeadsetActive;
                m.AddItem("Crew Health System" + (bad > 0 ? "   <color=#FF7A6A>" + bad + " critical</color>" : (warn > 0 ? "   <color=#FFD070>" + warn + " caution</color>" : "")), ok + " nominal, " + warn + " caution, " + bad + " critical. Vitals, pulse waves and the AI's three-step response for the four crew. Built from the NASA HUNCH crew-health page." + (vr ? "" : " F2 opens it."), () => m.OpenExtra(pageHealth));
                m.AddItem("Security cameras: " + (cvOn ? "ON" : "off"), "A live picture from every zone, the mining field and the map you are on." + (vr ? " Click the right stick, or use CAMS and CAM > on the dashboard." : " Z opens it; arrow keys or clicks change camera."), () => { m.Close(); ToggleCams(); });
                m.AddItem(mtState == 1 ? "Team meeting CALLED: go now" : (mtState == 2 ? "Team meeting: back to the vote" : "Team meeting: call one now"), "The crew gather in the Galley, each one reports, then everyone votes on a plan. Meetings are also called every few minutes and when a crew member needs support." + (vr ? "" : " F4 does the same."), () => { m.Close(); MeetingGo(); });
                m.AddItem("Battery Bank: " + batBank + " of " + BatSlots, "You are carrying " + batHeld + ". " + "Collect the glowing green battery modules and slot them into the rack at the Power Plant. Choosing this takes you to the rack.", () => { m.Close(); if (mapNow != 0) GoMap(0); if (batZone >= 0) game.GoTo(batZone); });
                m.AddItem("Things to read: " + ReadCount() + " of " + reads.Count + " found", "Tablets you have picked up, including the Physics and Calculus reference sheet." + (vr ? "" : " F3 opens this list."), () => m.OpenExtra(pageRead));
                m.AddItem("Music: " + (musicRef ? HunchMusic.Styles[musicRef.style] : "none") + "   (next)", "Space ambient, Happy EDM, Lo-fi chill, Chiptune, Mellow bounce, Synthwave, Funky beat, or your own files from the Music folder next to the game." + (vr ? "" : " F5 does the same."), () => { NextMusic(); m.Rebuild(); });
                m.AddItem("Player " + (player + 1) + " of 4   (switch)", "Now playing: " + PlayerName + ". " + "Up to four players take turns on this computer. Mini-game best scores are kept for each player.", () => { player = (player + 1) % 4; m.Rebuild(); });
                m.AddItem("Autopilot: " + (autoOn ? "ON" : "off"), "The AI plays instead of you. The AI does the jobs, attends the meetings, collects batteries and plays the mini-games. " + (vr ? "A" : "Space") + " hands the controls back.", () => { m.Close(); if (autoOn) StopAutopilot("You have the controls"); else StartAutopilot(); });
                m.AddItem("Autopilot Smooth: " + (autoOn && autoSmooth ? "<color=#7CFC9A>ON</color>" : "off") + "   (the AI walks, runs and plays to win)", "The AI plays the mission for the best score. It walks and runs from zone to zone instead of jumping there, and before every choice it plays the next minute ahead on a copy of the habitat to see which job helps the score most. Press SPACE (or A in the headset) to take over." + (game.HeadsetActive ? "" : " F9 does the same."), () => { m.Close(); if (autoOn && autoSmooth) StopAutopilot("You have the controls"); else StartAutopilotSmooth(); });
                ExtraMenu(m);
            };
            pageHealth.build = m =>
            {
                hunchFlags |= HFDash;
                for (int i = 0; i < hs.Length; i++)
                {
                    int ii = i; var h = hs[i];
                    m.AddItem((h.level == 2 ? "<color=#FF7A6A>" : (h.level == 1 ? "<color=#FFD070>" : "<color=#7CFC9A>")) + sim.crew[i].name + "</color>   HR " + Mathf.RoundToInt(h.hr) + "   " + h.cls, HsLine(i) + ".  " + sim.crew[i].role + ". Risk " + h.risk.ToString("F2") + ". " + (h.cond >= 0 ? HsCond[h.cond] + ": " + HsState[h.state] + "." : "No condition detected.") + " Choose for details, options and the scenario simulator.", () => { hsSel = ii; m.OpenExtra(pageHealthCrew); });
                }
                m.AddItem("Alert feed: " + (hsLog.Count > 0 ? hsLog[0] : "no alerts yet"), hsLog.Count > 1 ? hsLog[1] + (hsLog.Count > 2 ? "\n" + hsLog[2] : "") : "The newest health event is shown here.", () => m.Rebuild());
                m.AddItem("Go to the health wall", "Takes you to the big Crew Health System screen with the live pulse and breathing waves.", () => { m.Close(); if (mapNow != 0) GoMap(0); if (hsZone >= 0) game.GoTo(hsZone); });
                if (!game.HeadsetActive) m.AddItem("Open the live web dashboard (browser)", "Opens the NASA HUNCH Crew Health System page in your web browser, linked to this game: it shows these four crew, their vitals, the habitat systems and the clock, live. Buttons pressed on the page (accept, inject a scenario) come back into the game. It works on this computer while the game is running.", () => { OpenHealthPage(); });
            };
            pageHealthCrew.build = m =>
            {
                hunchFlags |= HFCrew;
                int i = Mathf.Clamp(hsSel, 0, hs.Length - 1); var h = hs[i]; var c = sim.crew[i];
                m.AddItem("<b>" + c.name + "</b>  " + c.role + "   " + h.cls + "   risk " + h.risk.ToString("F2"), HsLine(i) + "   O2 flow " + h.flow.ToString("F2") + " L/min.  Mood " + Mathf.RoundToInt(c.mood) + ", stress " + Mathf.RoundToInt(c.stress) + ", sleep " + Mathf.RoundToInt(c.sleep) + ".", () => m.Rebuild());
                if (h.cond >= 0) m.AddItem(HsCond[h.cond] + ": " + HsState[h.state], "Silent adjustment used for this condition: " + HsAuto[h.cond] + ".", () => m.Rebuild());
                if (h.cond >= 0 && h.state == 2) for (int o = 0; o < 3; o++) { int oo = o; m.AddItem("Offer: " + HsOffer[h.cond][o], "The crew member starts this now. Mood goes up and stress goes down, and the condition fades.", () => { HsAccept(i, oo); m.Rebuild(); }); }
                m.AddItem("Scenario to simulate: " + HsCond[h.pick] + "   (change)", "The biomedical scenario simulator: pick a condition, then inject it at one of three strengths to watch the AI detect and respond.", () => { h.pick = (h.pick + 1) % HsCond.Length; m.Rebuild(); });
                m.AddItem("Inject mild", "Early signs: the AI makes a silent adjustment to light, sound and scent.", () => { HsInject(i, 0.42f); m.Rebuild(); });
                m.AddItem("Inject moderate", "The AI offers three options to the crew member. You choose one here.", () => { HsInject(i, 0.65f); m.Rebuild(); });
                m.AddItem("Inject severe", "The AI escalates to the flight surgeon and calls a team meeting.", () => { HsInject(i, 0.9f); m.Rebuild(); });
                m.AddItem("Clear this crew member", "Removes the simulated condition.", () => { h.cond = -1; h.state = 0; h.inten = 0f; m.Rebuild(); });
            };
            pageRead.build = m =>
            {
                for (int i = 0; i < reads.Count; i++)
                {
                    int ii = i;
                    if (reads[i].got) m.AddItem(reads[i].title, "Read it again.", () => { m.Close(); OpenRead(ii); });
                    else m.AddItem("<color=#8894B0>? not found yet (look near: " + reads[i].zone + ")</color>", "Glowing tablets float in the zones. Walk into one or aim and click to pick it up.", () => { });
                }
            };
            menu.extraPages.Add(pageBase);
        }
        // a second row of big buttons on the VR dashboard, so nothing needs a button the controller does not have
        if (quickRoot)
        {
            int first = quick.Count;
            AddQuick(() => "MAPS", () => { if (menu && pageMaps != null) menu.OpenExtra(pageMaps); });
            AddQuick(() => "CAMS\n<size=70%>" + (cvOn ? "ON" : "off") + "</size>", ToggleCams);
            AddQuick(() => "CAM >\n<size=70%>next</size>", () => NextCam(1));
            AddQuick(() => mtState == 1 ? "<color=#FF9A8A>MEET\n<size=70%>go now</size></color>" : "MEET", MeetingGo);
            AddQuick(() => "HEALTH", () => { if (menu && pageHealth != null) menu.OpenExtra(pageHealth); });
            AddQuick(() => "READ\n<size=70%>" + ReadCount() + "/" + reads.Count + "</size>", () => { if (menu && pageRead != null) menu.OpenExtra(pageRead); });
            AddQuick(() => "MUSIC\n<size=70%>next</size>", NextMusic);
            AddQuick(() => "AUTO\n<size=70%>AI plays</size>", StartAutopilot);
            int n = quick.Count - first; float pitch = 1.04f / Mathf.Max(1, n);
            for (int i = 0; i < n; i++)
            {
                float x = -0.52f + pitch * (i + 0.5f);
                quick[first + i].go.transform.localPosition = new Vector3(x, -0.115f, 0.004f); quick[first + i].label.rectTransform.localPosition = new Vector3(x, -0.115f, 0f);
            }
        }
    }

    public void NextMusic()
    {
        if (!musicRef) musicRef = FindFirstObjectByType<HunchMusic>();
        if (!musicRef) { Note("Music is not available."); return; }
        musicBefore = -1; musicRef.NextStyle(); Note("Music: " + HunchMusic.Styles[musicRef.style] + (musicRef.Message.Length > 0 ? "  -  " + musicRef.Message : ""));
    }
    void OpenHealthPage()
    {
        var link = GetComponent<HunchLink>();
        if (link && link.Running) { Application.OpenURL(HunchLink.Url); Note("Opened the live Crew Health dashboard in your browser. It shows this game's crew and habitat."); return; }
        string path = Path.Combine(Application.streamingAssetsPath, "hunch-crew-health.html");
        if (!File.Exists(path)) { Note("The web page file is missing: " + path); return; }
        Application.OpenURL("file:///" + path.Replace("\\", "/")); Note("Opened the Crew Health System page in your browser.");
    }
    /// VR: the right stick click
    public void VrStickClick() { if (ready && Playing && !game.menuOpen) ToggleCams(); }

    void UpdateBatch7(float dt)
    {
        game.autoBusy = ControlsLocked;
        bool quiet = !game.HeadsetActive && (readOpen >= 0 || cvOn) && Playing && !game.menuOpen;
        if (quiet && !hudHid) { hudHid = true; hudWas = game.hideHud; game.hideHud = true; }
        else if (!quiet && hudHid) { hudHid = false; game.hideHud = hudWas; }
        UpdateMaps(dt); UpdatePicks(dt); UpdateBank(dt); UpdateRead(dt); UpdateCams(); UpdateHealth(dt); UpdateMeeting(dt); UpdateReset(dt);
        if (Playing && !game.menuOpen && !ControlsLocked && !game.HeadsetActive)
        {
            if (kbPressed(Key.F3) && menu) menu.OpenExtra(pageRead);
            if (kbPressed(Key.F5)) NextMusic();
            if (kbPressed(Key.F6)) StartAutopilot();
            if (kbPressed(Key.F9)) StartAutopilotSmooth();
        }
        if (cvOn && game.HeadsetActive && cvScreen && !cvScreen.gameObject.activeSelf) cvScreen.gameObject.SetActive(true);
    }

    /// extra lines for the HUD (desktop: a box at the top; VR: the dashboard card)
    string HudExtra()
    {
        var sb = new StringBuilder(); string map = MapHud();
        if (map.Length > 0) sb.Append("<color=#9AD8FF>").Append(map).Append("</color>");
        if (mtState == 1) { if (sb.Length > 0) sb.Append("\n"); sb.Append("<color=#FF9A8A><b>TEAM MEETING</b> called - go to the Galley").Append(game.HeadsetActive ? " (MEET button)" : " (F4)").Append("   ").Append(Mathf.CeilToInt(mtLeft)).Append(" s</color>"); }
        if (batHeld > 0) { if (sb.Length > 0) sb.Append("\n"); sb.Append("<color=#7CFC9A>Carrying ").Append(batHeld).Append(" battery module").Append(batHeld > 1 ? "s" : "").Append(" - take to the Battery Bank at the Power Plant (").Append(batBank).Append(" of ").Append(BatSlots).Append(")</color>"); }
        string rh = RadioHud(); if (rh.Length > 0) { if (sb.Length > 0) sb.Append("\n"); sb.Append(rh); }
        string lh = LevelHud(); if (lh.Length > 0) { if (sb.Length > 0) sb.Append("\n"); sb.Append(lh); }
        string hh = HunchHud(); if (hh.Length > 0) { if (sb.Length > 0) sb.Append("\n"); sb.Append(hh); }
        return sb.ToString();
    }

    /// desktop drawing. Returns true when the security camera picture fills the screen.
    bool DrawBatch7(float u, float W, float H)
    {
        if (cvOn && cvRt)
        {
            GUI.color = Color.white; GUI.DrawTexture(new Rect(0f, 0f, W, H), cvRt, ScaleMode.ScaleAndCrop, false);
            Fill(new Rect(0f, 0f, W, 54f * u), new Color(0f, 0f, 0f, 0.6f)); Fill(new Rect(0f, H - 46f * u, W, 46f * u), new Color(0f, 0f, 0f, 0.6f));
            var p = cvPoints[cvIndex];
            GUI.Label(new Rect(0f, 8f * u, W, 40f * u), "<b>CAM " + (cvIndex + 1) + " of " + cvPoints.Count + "</b>    " + p.name + "    Day " + sim.Day + "  " + Mathf.FloorToInt(sim.Hour).ToString("00") + ":" + Mathf.FloorToInt(sim.Hour % 1f * 60f).ToString("00") + (Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f ? "    <color=#FF5A4A>REC</color>" : ""), sNote);
            GUI.Label(new Rect(0f, H - 42f * u, W, 36f * u), "<color=#C8D4E8>Arrow keys or click: next camera     Z, E or Space: close</color>", sBar);
            return true;
        }
        if (readOpen >= 0 || hudHid) return true;      // reading: nothing is drawn over the sheet
        string s = HudExtra(); if (s.Length == 0) return false;
        int lines = 1; foreach (char ch in s) if (ch == '\n') lines++;
        float w = Mathf.Min(1100f * u, W - 30f * u), h = 30f * u * lines + 6f * u; var r = new Rect((W - w) * 0.5f, (autoOn ? 172f : 104f) * u, w, h);
        Fill(r, new Color(0.02f, 0.05f, 0.12f, 0.8f)); GUI.Label(r, s, sBar);
        return false;
    }

    // ------------------------------------------------------------------ the autopilot: the AI plays
    IEnumerator AutoTasks()
    {
        bool vr = game.HeadsetActive; int round = 0, zoneTurn = 0, mapTurn = 0, jobRun = 0;
        yield return new WaitForSeconds(1.0f);
        AutoSay("Autopilot is on: the AI plays. Press " + (vr ? "A" : "Space") + " when you want the controls back.");
        yield return new WaitForSeconds(2.2f);
        while (true)
        {
            if (mapNow != 0) { GoMap(0); yield return new WaitForSeconds(1.0f); }
            if (readOpen >= 0) CloseRead();
            // look after yourself
            if (!Creative && hunger < 55f && food > 0) { AutoSay("Eating a food pack."); Eat(); yield return new WaitForSeconds(1.4f); }
            // a team meeting has been called
            if (mtState == 1)
            {
                AutoSay("Team meeting called. Going to the Galley."); MeetingGo(); yield return new WaitForSeconds(1.0f);
                if (mtBell) yield return Turn(mtBell.position + Vector3.up * 1.2f, 0.6f);
                yield return new WaitForSeconds(2.0f);
                if (mtState == 2) { int v = MeetingBest(); AutoSay("The crew have reported. Voting for: " + MtOption[v] + "."); yield return new WaitForSeconds(1.6f); MeetingVote(v); yield return new WaitForSeconds(2.5f); }
                continue;
            }
            // a broken panel
            Station broken = null; foreach (var st in stations) if (st.broken) { broken = st; break; }
            if (broken != null) { yield return AutoStation(broken); continue; }
            // the job that is needed now (an alert, or the next zone to commission)
            int uz = game.UrgentZone();
            if (uz >= 0 && jobRun < 6)
            {
                jobRun++; var z = game.zones[uz]; string p = sim.Prompt(z.id);
                AutoSay("Job at " + z.title + (p.Length > 0 ? ": " + p : "") + ".");
                game.GoTo(uz); yield return new WaitForSeconds(0.8f); game.DoInteract(); yield return new WaitForSeconds(1.5f);
                continue;
            }
            jobRun = 0;
            // crew health: choose one of the options the AI offered
            bool did = false;
            if (hs != null) for (int i = 0; i < hs.Length; i++) if (hs[i].state == 2 && hs[i].cond >= 0)
            {
                AutoSay("Crew health: " + sim.crew[i].name + " has signs of " + HsCond[hs[i].cond].ToLower() + ". Starting: " + HsOffer[hs[i].cond][0] + ".");
                if (hsZone >= 0) { game.GoTo(hsZone); yield return new WaitForSeconds(0.6f); if (hsWall) yield return Turn(hsWall.position + Vector3.up * 1.5f, 0.6f); }
                HsAccept(i, 0); yield return new WaitForSeconds(2.2f); did = true; break;
            }
            if (did) continue;
            // battery modules into the Battery Bank
            if (batRack && batBank < BatSlots)
            {
                Pick cell = null; if (batHeld < BatCarry && batBank + batHeld < BatSlots) foreach (var p in picks) if (p.type == 0 && !p.taken) { cell = p; break; }
                if (cell != null)
                {
                    AutoSay("Collecting a battery module for the Battery Bank.");
                    yield return AutoToPick(cell); TakePick(picks.IndexOf(cell)); yield return new WaitForSeconds(0.9f); continue;
                }
                if (batHeld > 0)
                {
                    AutoSay("Slotting " + batHeld + " battery module" + (batHeld > 1 ? "s" : "") + " into the Battery Bank.");
                    game.GoTo(batZone); yield return new WaitForSeconds(0.7f); yield return Turn(batRack.position + Vector3.up * 1.2f, 0.6f); yield return new WaitForSeconds(0.4f);
                    BankInsert(); yield return new WaitForSeconds(1.8f); continue;
                }
            }
            // something to read
            Pick book = null; foreach (var p in picks) if (p.type == 1 && !p.taken) { book = p; break; }
            if (book != null)
            {
                AutoSay("Picking up \"" + reads[book.index].title + "\" to read.");
                yield return AutoToPick(book); TakePick(picks.IndexOf(book)); yield return new WaitForSeconds(4.5f); CloseRead(); yield return new WaitForSeconds(0.5f); continue;
            }
            // astronaut food
            if (food < 6)
            {
                Pick snack = null; foreach (var p in picks) if (p.type == 2 && !p.taken) { snack = p; break; }
                if (snack != null) { AutoSay("Picking up " + FoodName[snack.index % FoodName.Length].ToLower() + "."); yield return AutoToPick(snack); TakePick(picks.IndexOf(snack)); yield return new WaitForSeconds(1.2f); continue; }
            }
            // routine care at the next zone
            int zi = zoneTurn % game.zones.Count; zoneTurn++; var zz = game.zones[zi];
            AutoSay("Routine check at " + zz.title + ".");
            game.GoTo(zi); yield return new WaitForSeconds(0.7f); yield return Pan(zoneTurn % 2 == 0 ? 80f : -80f, 2.2f); game.DoInteract(); yield return new WaitForSeconds(1.4f);
            round++;
            if (round % 3 == 0 && Creative && stations.Count > 0) yield return AutoStation(stations[(round / 3) % stations.Count]);
            if (round % 4 == 0 && !hunchOn) { mapTurn = mapTurn % (MapCount - 1) + 1; yield return AutoMap(mapTurn); }
            if (round % 5 == 0 && fieldOk && !vr && sim.phase == HabPhase.Mission)
            {
                AutoSay("Mining a glowing rock with the drill."); GoField(); yield return new WaitForSeconds(0.8f);
                ProbeNearNode(); yield return new WaitForSeconds(0.7f);
                tool = Drill; probeHold = Time.time + 3.4f; yield return new WaitForSeconds(3.7f); probeHold = 0f; tool = Hands;
            }
        }
    }

    IEnumerator AutoToPick(Pick p)
    {
        if (p.zone >= 0) game.GoTo(p.zone);
        else { Vector3 at = p.home + new Vector3(1.6f, -0.9f, 0f), g; if (GroundAt(at.x, at.z, out g)) at = g; TeleportTo(at, 0f); }
        yield return new WaitForSeconds(0.6f);
        yield return Turn(p.home, 0.6f);
        yield return new WaitForSeconds(0.4f);
    }

    IEnumerator AutoMap(int k)
    {
        if (!mapRoot[k] || sim.phase != HabPhase.Mission) yield break;
        AutoSay("Mini-game time: " + MapNames[k] + ". The AI plays it.");
        yield return new WaitForSeconds(1.2f);
        GoMap(k); yield return new WaitForSeconds(0.8f);
        if (mapNow != k) yield break;
        if (k >= 7) { yield return AutoMapExtra(k); yield return new WaitForSeconds(3f); if (mapNow == k) GoMap(0); yield return new WaitForSeconds(1f); yield break; }
        if (k == 4)
        {
            TeleportTo(mapC[4] + new Vector3(0f, 0f, 10f), 0f); yield return new WaitForSeconds(9.5f);
            if (thirdPerson) SetThird(false);
            TeleportTo(mapC[4] + new Vector3(14f, 0f, 2f), 270f); yield return new WaitForSeconds(7.5f);
        }
        else
        {
            if (k == 1) { TeleportTo(mapC[1] + new Vector3(0f, 0f, -3.5f), 0f); if (desk) desk.SetLook(0f, -8f); }
            if (k == 2) { TeleportTo(mapC[2] + new Vector3(0f, 0f, 1.5f), 0f); }
            if (k == 5 && desk) desk.SetLook(0f, -2f);
            if (k == 6 && desk) desk.SetLook(-12f, 6f);
            while (mgCount > 0f && mapNow == k) yield return null;
            float guard = Time.time + (k == 6 ? 260f : 110f), act = 0f, aim = 0f; int pitAim = -1;
            while (mgOn && Time.time < guard && mapNow == k)
            {
                float dt = Time.deltaTime; act -= dt;
                if (k == 1 && act <= 0f)
                {
                    act = 0.3f; int worst = -1; float wd = 6f;
                    for (int i = 0; i < solPanels.Count; i++) { float d = Mathf.Abs(solPanels[i].ang - solPhi); if (d > wd) { wd = d; worst = i; } }
                    if (worst >= 0) SolPress(worst * 2 + (solPanels[worst].ang < solPhi ? 1 : 0));
                }
                else if (k == 2)
                {
                    if (pitAim < 0 || pit[pitAim].gone)
                    {
                        pitAim = -1; int bestV = -1; float bestHp = 99f;
                        for (int i = 0; i < pit.Count; i++)
                        {
                            var b = pit[i]; if (b.gone) continue;
                            int above = i + PitW * PitW; if (b.layer < PitLayers - 1 && above < pit.Count && !pit[above].gone) continue;      // still buried
                            int v = PitValue[b.kind];
                            if (v > bestV || (v == bestV && b.hp < bestHp)) { bestV = v; bestHp = b.hp; pitAim = i; }
                        }
                    }
                    if (pitAim >= 0) { Vector3 bp = pit[pitAim].t.position; Face(bp); drilling = true; drillBeam.enabled = true; drillBeam.SetPosition(0, Muzzle()); drillBeam.SetPosition(1, bp); PitDrill(pitAim, dt * 1.5f, bp, false); }
                }
                else if (k == 3)
                {
                    ZrTarget tg = null; int ti = -1; for (int i = 0; i < zrTargets.Count; i++) if (zrTargets[i].up) { tg = zrTargets[i]; ti = i; break; }
                    if (tg != null)
                    {
                        Vector3 tp = tg.t.position + Vector3.up * 0.6f; Face(tp); aim += dt;
                        if (aim > 0.45f) { aim = 0f; ShowBeam(Muzzle(), tp, beamGreen); Play(cZap, 0.4f); zrShots++; ZrHit(ti); }
                    }
                    else aim = 0f;
                }
                yield return null;
            }
        }
        yield return new WaitForSeconds(4f);
        if (mapNow == k) GoMap(0);
        yield return new WaitForSeconds(1.0f);
    }

    void AutoStopExtra()
    {
        if (readOpen >= 0) CloseRead();
        if (mapNow == 2) tool = Drill; else if (mapNow == 3 || mapNow == 8) tool = Zapper;
    }

    // ------------------------------------------------------------------ test hooks
    public string ProbeSys()
    {
        int cells = 0, booksN = 0, foods = 0; foreach (var p in picks) { if (p.type == 0) cells++; else if (p.type == 1) booksN++; else foods++; }
        var sb = new StringBuilder("maps:");
        for (int k = 1; k < MapCount; k++) sb.Append(" ").Append(mapRoot[k] ? (mapC[k] - mapC[0]).x.ToString("F0") : "none");
        sb.Append(" gate=").Append(mapGateOk).Append(" | picks cells=").Append(cells).Append(" reads=").Append(booksN).Append(" food=").Append(foods);
        sb.Append(" | bank=").Append(batRack ? game.zones[batZone].id : "none").Append(" ").Append(batBank).Append("/").Append(BatSlots).Append(" held=").Append(batHeld);
        sb.Append(" | health=").Append(hsWall ? game.zones[hsZone].id : "none").Append(" | bell=").Append(mtBell ? game.zones[mtZone].id : "none").Append(" meet=").Append(mtState).Append(" next=").Append(mtNext.ToString("F0"));
        sb.Append(" | cams=").Append(cvPoints.Count).Append(" on=").Append(cvOn).Append(" | nodes=").Append(nodes.Count).Append(" | map=").Append(mapNow).Append(" mg=").Append(mgOn).Append(" score=").Append(mgScore.ToString("F0")).Append(" t=").Append(mgT.ToString("F0"));
        sb.Append(" | music=").Append(musicRef ? HunchMusic.Styles[musicRef.style] : "none").Append(" | resets=").Append(sysResets).Append(" quick=").Append(quick.Count).Append(" player=").Append(player);
        return sb.ToString();
    }
    public string ProbeGoMap() { GoMap(probeInt); return "map " + mapNow + " " + MapNames[mapNow]; }
    public string ProbeMapStart() { mgCount = 0.05f; return "starting"; }
    public string ProbeMapLook() { if (desk) desk.SetLook(0f, mapNow == 2 ? -4f : (mapNow == 1 ? -6f : 2f)); return "look"; }
    public string ProbeAt() { if (mapNow == 0) return "not on a map"; var p = mapC[mapNow] + new Vector3(probeX, 0f, probeZ); TeleportTo(p, probeYaw); return "at " + probeX + "," + probeZ; }
    public float probeX, probeZ, probeYaw;
    public string ProbePick() { int n = 0; for (int i = 0; i < picks.Count; i++) if (picks[i].type == probeInt && !picks[i].taken) { var p = picks[i]; if (p.zone >= 0) game.GoTo(p.zone); else TeleportTo(p.home - Vector3.up * 0.9f + new Vector3(1.6f, 0f, 0f), 270f); Face(p.home); n = i; return "facing pick " + n + " type " + p.type + " zone " + p.zone + " at " + p.home.ToString("F1"); } return "none left"; }
    public string ProbeTake() { for (int i = 0; i < picks.Count; i++) if (picks[i].type == probeInt && !picks[i].taken) { TakePick(i); return "took " + i + " held=" + batHeld + " food=" + food + " read=" + readOpen; } return "none"; }
    public string ProbeBank() { if (!batRack) return "no bank"; game.GoTo(batZone); Face(batRack.position + Vector3.up * 1.2f); return "bank " + batBank + " held " + batHeld; }
    public string ProbeBankFill() { batHeld = BatCarry; BankInsert(); batHeld = BatCarry; BankInsert(); batHeld = 2; BankInsert(); return "bank " + batBank; }
    public string ProbeHealthWall() { if (!hsWall) return "no wall"; game.GoTo(hsZone); Face(hsWall.position + Vector3.up * 1.45f); return "health wall in " + game.zones[hsZone].id; }
    public string ProbeInject() { HsInject(probeInt % hs.Length, probeX > 0f ? probeX : 0.65f); var h = hs[probeInt % hs.Length]; return "state " + h.state + " inten " + h.inten.ToString("F2") + " cls " + h.cls + " meet " + mtState; }
    public string ProbeHealthPage() { if (menu) menu.OpenExtra(pageHealth); return "health page"; }
    public string ProbeBasePage() { if (menu) menu.OpenExtra(pageBase); return "base page"; }
    public string ProbeMapsPage() { if (menu) menu.OpenExtra(pageMaps); return "maps page"; }
    public string ProbeMeet() { sim.phase = HabPhase.Mission; MeetingGo(); return "meeting state " + mtState + " why " + mtWhy; }
    public string ProbeVote() { int b = MeetingBest(); if (menu) menu.Close(); MeetingVote(b); return "voted " + b + " state " + mtState + " count " + mtCount; }
    public string ProbeBell() { if (!mtBell) return "no bell"; game.GoTo(mtZone); Face(mtBell.position + Vector3.up * 1.0f); return "bell in " + game.zones[mtZone].id; }
    public string ProbeCam() { NextCam(1); return "cam " + cvIndex + " " + cvPoints[cvIndex].name + " on=" + cvOn; }
    public string ProbeCamWhere()
    {
        var p = cvPoints[Mathf.Clamp(probeInt, 0, cvPoints.Count - 1)].pos; var sb = new StringBuilder("cam at " + p.ToString("F1") + " inside:");
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if (r.enabled && r.bounds.Contains(p) && r.bounds.size.magnitude < 40f && r.sharedMaterial) sb.Append("\n   ").Append(r.name).Append(" < ").Append(r.transform.parent ? r.transform.parent.name : "-").Append(" size ").Append(r.bounds.size.ToString("F1")).Append(" queue ").Append(r.sharedMaterial.renderQueue).Append(" ").Append(r.sharedMaterial.shader.name);
        return sb.ToString();
    }
    // ------------------------------------------------------------------ live link to the Crew Health web page (served by HunchLink)
    int linkSeq; readonly List<string> linkLog = new List<string>();
    static string Js(string t) { return (t ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " "); }
    static string Nf(float v, string f) { return v.ToString(f, System.Globalization.CultureInfo.InvariantCulture); }

    /// everything the web page shows, as JSON
    public string LinkJson()
    {
        if (!ready || sim == null) return "";
        var sb = new StringBuilder(1600); float o = Mathf.Clamp01(sim.oxygen / 100f); bool night = sim.LunarNight;
        sb.Append("{\"game\":\"Lunar Habitat Commander\",\"day\":").Append(sim.Day).Append(",\"hour\":").Append(Nf(sim.Hour, "F3"))
          .Append(",\"phase\":\"").Append(sim.phase).Append("\",\"mode\":\"").Append(sim.phase == HabPhase.Briefing ? "Menu" : (sim.timeFrozen ? "Creative" : "Survival"))
          .Append("\",\"map\":\"").Append(MapNames[mapNow]).Append("\",\"autopilot\":").Append(autoOn ? "true" : "false").Append(",\"meeting\":").Append(mtState)
          .Append(",\"player\":{\"health\":").Append(Mathf.RoundToInt(health)).Append(",\"hunger\":").Append(Mathf.RoundToInt(hunger)).Append(",\"food\":").Append(food).Append(",\"batteryBank\":").Append(batBank).Append("}");
        sb.Append(",\"hab\":{\"o2\":").Append(Nf(16.5f + 4.4f * o, "F2")).Append(",\"co2\":").Append(Mathf.RoundToInt(600f + (1f - o) * 4000f + (ventOn ? 0f : 900f) + (sim.eclssOk ? 0f : 1500f)))
          .Append(",\"press\":").Append(Nf(sim.leak ? 96.5f : 101.3f, "F1")).Append(",\"water\":").Append(Nf(sim.water, "F1")).Append(",\"batt\":").Append(Nf(sim.battery, "F1"))
          .Append(",\"solar\":").Append(Nf(sim.solarOn && !night ? 8f * sim.arrays / 100f : 0f, "F2")).Append(",\"load\":").Append(Nf(lightsOn ? 6f : 4.8f, "F1"))
          .Append(",\"nuke\":").Append(sim.fissionOn ? "true" : "false").Append(",\"night\":").Append(night ? "true" : "false").Append(",\"leak\":").Append(sim.leak ? "true" : "false")
          .Append(",\"scrub\":").Append(sim.eclssOk ? "false" : "true").Append(",\"green\":").Append(Mathf.RoundToInt(sim.food)).Append("}");
        sb.Append(",\"crew\":[");
        for (int i = 0; i < sim.crew.Count && i < 4; i++)
        {
            var c = sim.crew[i]; var h = hs != null && i < hs.Length ? hs[i] : null; if (i > 0) sb.Append(",");
            sb.Append("{\"name\":\"").Append(Js(c.name)).Append("\",\"role\":\"").Append(Js(c.role)).Append("\",\"mood\":").Append(Nf(c.mood, "F1")).Append(",\"stress\":").Append(Nf(c.stress, "F1"))
              .Append(",\"sleep\":").Append(Nf(c.sleep, "F1")).Append(",\"health\":").Append(Nf(c.health, "F1")).Append(",\"hr0\":").Append(c.HeartRate);
            if (h != null)
                sb.Append(",\"hr\":").Append(Nf(h.hr, "F1")).Append(",\"spo2\":").Append(Nf(h.spo2, "F1")).Append(",\"flow\":").Append(Nf(h.flow, "F2")).Append(",\"co2\":").Append(Nf(h.co2, "F2"))
                  .Append(",\"resp\":").Append(Nf(h.resp, "F1")).Append(",\"temp\":").Append(Nf(h.temp, "F2")).Append(",\"risk\":").Append(Nf(h.risk, "F3")).Append(",\"cls\":\"").Append(h.cls)
                  .Append("\",\"cond\":").Append(h.cond).Append(",\"inten\":").Append(Nf(h.inten, "F3")).Append(",\"state\":").Append(h.cond >= 0 ? h.state : 0).Append(",\"level\":").Append(h.level);
            else sb.Append(",\"hr\":").Append(c.HeartRate).Append(",\"spo2\":98,\"flow\":0.86,\"co2\":2.6,\"resp\":14,\"temp\":36.7,\"risk\":0.2,\"cls\":\"Neutral\",\"cond\":-1,\"inten\":0,\"state\":0,\"level\":0");
            sb.Append("}");
        }
        sb.Append("],\"alerts\":[");
        for (int i = 0; i < sim.alerts.Count; i++) { var a = sim.alerts[i]; if (i > 0) sb.Append(","); sb.Append("{\"title\":\"").Append(Js(a.title)).Append("\",\"zone\":\"").Append(Js(a.zoneId)).Append("\",\"seconds\":").Append(Mathf.CeilToInt(a.secondsLeft)).Append("}"); }
        sb.Append("],\"log\":[");
        for (int i = 0; i < linkLog.Count; i++) { if (i > 0) sb.Append(","); sb.Append(linkLog[i]); }
        sb.Append("]}");
        return sb.ToString();
    }

    /// something was pressed on the web page: "?do=accept&crew=1", "?do=inject&crew=2&cond=0&level=0.72", "?do=clear&crew=0", "?do=ack&crew=3"
    public void LinkCommand(string query)
    {
        if (!ready || hs == null || string.IsNullOrEmpty(query)) return;
        string what = ""; int crew = -1, cond = 0; float level = 0.65f;
        foreach (var part in query.TrimStart('?').Split('&'))
        {
            int eq = part.IndexOf('='); if (eq <= 0) continue; string k = part.Substring(0, eq), v = part.Substring(eq + 1);
            if (k == "do") what = v;
            else if (k == "crew") int.TryParse(v, out crew);
            else if (k == "cond") int.TryParse(v, out cond);
            else if (k == "level") float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out level);
        }
        if (crew < 0 || crew >= hs.Length) return;
        var h = hs[crew]; string name = sim.crew[crew].name;
        switch (what)
        {
            case "inject": h.pick = Mathf.Clamp(cond, 0, HsCond.Length - 1); HsInject(crew, Mathf.Clamp(level, 0.36f, 0.95f)); Note("Web dashboard: " + HsCond[h.pick].ToLower() + " scenario injected for " + name + "."); break;
            case "clear": if (h.cond >= 0) HsNote(name, HsCond[h.cond] + ": cleared from the web dashboard"); h.cond = -1; h.state = 0; h.inten = 0f; break;
            case "accept": if (h.cond >= 0 && h.state == 2) { HsAccept(crew, 0); Note("Web dashboard: " + name + " accepted support."); } break;
            case "ack": if (h.cond >= 0 && h.state == 3) { h.state = 5; HsNote(name, HsCond[h.cond] + ": flight surgeon acknowledged on the web dashboard. Care plan started"); } break;
        }
    }
    public string ProbeLink() { var l = GetComponent<HunchLink>(); return l ? l.ProbeSelf() : "no link component"; }
    public string ProbeLinkCmd() { var l = GetComponent<HunchLink>(); return l ? l.ProbeCmd() : "no link component"; }
    public string ProbeLinkState() { var sb = new StringBuilder(); for (int i = 0; i < hs.Length; i++) sb.Append(sim.crew[i].name).Append(": cond ").Append(hs[i].cond).Append(" state ").Append(hs[i].state).Append(" inten ").Append(hs[i].inten.ToString("F2")).Append(" | "); return sb.ToString(); }

    public string ProbeCamOff() { SetCams(false); return "cams off"; }
    public string ProbeReadOpen() { reads[probeInt % reads.Count].got = true; OpenRead(probeInt % reads.Count); return "reading " + reads[readOpen].title; }
    public string ProbeReadClose() { CloseRead(); return "closed"; }
    public string ProbeMusic() { NextMusic(); return musicRef ? HunchMusic.Styles[musicRef.style] + " | " + musicRef.Message : "no music"; }
    public string ProbeZero() { sim.timeFrozen = false; sim.phase = HabPhase.Mission; sim.oxygen = 0.4f; return "oxygen set to 0.4"; }
    public string ProbeTdBuild() { for (int i = 0; i < 3; i++) TdPad(i * 2); return "energy " + tdEnergy; }
    public string ProbeRichNode() { for (int i = nodes.Count - 1; i >= 0; i--) if (nodes[i].stagesMax > 1) { var n = nodes[i]; Vector3 p = n.t.position + new Vector3(4f, 0f, 0f), g; if (GroundAt(p.x, p.z, out g)) p = g; TeleportTo(p, 270f); Face(n.t.position + Vector3.up * 0.8f); return n.t.name + " stages " + n.stages; } return "no rich node"; }
    public string ProbeGate() { if (!mapGateOk) return "no gate"; Vector3 d = (fieldC - mapGateAt); d.y = 0f; d.Normalize(); Vector3 p = mapGateAt + d * 6f, g; if (GroundAt(p.x, p.z, out g)) p = g; TeleportTo(p, 0f); Face(mapGateAt + Vector3.up * 1.6f); return "gate at " + mapGateAt.ToString("F0"); }
}
