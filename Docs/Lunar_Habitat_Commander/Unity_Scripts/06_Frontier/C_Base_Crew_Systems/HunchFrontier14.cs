// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/C_Base_Crew_Systems/HunchFrontier14.cs
//  Section : 06_Frontier/C_Base_Crew_Systems
//  Folder  : Systems inside the central base: pick-ups + battery bank, NASA HUNCH mode, physics
//            view, Autopilot Smooth, crew of four + sleep pods, The Show, self-working crew,
//            airlocks/lifts, crew self-care.
//  Purpose : Fourteenth part of Moon Frontier: NASA HUNCH MODE. Menu > "NASA HUNCH mode". ENABLED =
//            only what the NASA HUNCH project sheet asks for; disabled = everything (the fun
//            version).
//  Why     : NASA HUNCH mode: only what the HUNCH project sheet asks for.
//  Contains: HunchFrontier, HItem
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

/// Fourteenth part of Moon Frontier: NASA HUNCH MODE.
///   Menu > "NASA HUNCH mode". ENABLED = only what the NASA HUNCH project sheet asks for; disabled = everything (the fun version).
///   When enabled:
///    - only the main base is shown: the outer glass dome, the tall central spire and every site outside the dome are hidden
///    - a regolith cover lies over the living ring for radiation protection, greenhouses stand outside in the sunlight,
///      and the aquaponics fish tank is inside the base by the greenhouse zone
///    - no aliens, no tools that shoot, no portals, maps, mini-games, shop, costumes, pet, level-ups or season decorations
///    - a checklist with one box for every item the project sheet says MUST be there; a box ticks itself when you show that item
///    - documents to open in the game (also in VR): habitat facts, sensors, AI response protocol and hard limits,
///      the live smart-systems dashboard, the crew health report (can be saved as a file), and a privacy and consent page
public partial class HunchFrontier
{
    public bool hunchOn; HunchMenu.ExtraPage pageHunch, pageCheck, pageConsent; readonly List<GameObject> hunchHidden = new List<GameObject>();
    Transform hunchRoot; int hunchSheetFirst = -1, hunchFlags, hunchZones; readonly bool[] hunchConsent = { true, true, true, true }; bool hunchAliensWere = true, hunchCareWas, hunchWorldDone; float hunchGround, hunchTick, hunchRad; public float probeY; Vector3 hunchCover; string hunchPlaced = ""; string hunchReportPath = "";
    readonly List<Transform> hunchFish = new List<Transform>(); Vector3 hunchTankAt;
    class HItem { public string group, text, how; public Func<bool> test; public bool done; }
    readonly List<HItem> hunchItems = new List<HItem>();
    const int HFDash = 1, HFCrew = 2, HFReport = 4, HFFacts = 8, HFSensors = 16, HFProtocol = 32, HFEclss = 64, HFConsent = 128, HFMeeting = 256, HFAuto = 512, HFEvening = 1024, HFOutside = 2048, HFHeadset = 4096;

    int ZoneIndex(params string[] keys)
    {
        for (int i = 0; i < game.zones.Count; i++) { string id = (game.zones[i].id ?? "").ToLowerInvariant(), ti = (game.zones[i].title ?? "").ToLowerInvariant(); foreach (var k in keys) if (id == k || ti.Contains(k)) return i; }
        return -1;
    }
    bool ZoneSeen(params string[] keys) { int i = ZoneIndex(keys); return i >= 0 && (hunchZones & (1 << i)) != 0; }
    void HAdd(string group, string text, string how, Func<bool> test) { hunchItems.Add(new HItem { group = group, text = text, how = how, test = test }); }

    void BuildHunch()
    {
        hunchOn = PlayerPrefs.GetInt("hunch_mode2", 0) == 1;      // the game starts with NASA HUNCH mode off (the full game); switch it on in the menu
        try { BuildHygiene(); } catch (Exception e) { HLog.E("Crew", "hygiene: " + e); }
        for (int i = 0; i < 4; i++) hunchConsent[i] = PlayerPrefs.GetInt("hunch_consent_" + i, 1) == 1;
        // ---- the documents (read on the same sheet as the tablets, so they work in the headset too)
        hunchSheetFirst = reads.Count;
        reads.Add(new ReadItem { title = "Habitat facts and requirements", zone = "NASA HUNCH", got = true, body = HunchFacts });
        reads.Add(new ReadItem { title = "Sensors and what they detect", zone = "NASA HUNCH", got = true, body = () => HunchSensors });
        reads.Add(new ReadItem { title = "AI response protocol, consent and hard limits", zone = "NASA HUNCH", got = true, body = () => HunchProtocol });
        reads.Add(new ReadItem { title = "Smart systems dashboard (live)", zone = "NASA HUNCH", got = true, body = HunchEclss });
        reads.Add(new ReadItem { title = "Crew health report (live)", zone = "NASA HUNCH", got = true, body = HunchReport });
        // ---- the checklist: one box for each MUST in the project sheet
        HAdd("DELIVERABLES", "AI psychological monitoring dashboard", "Open the Crew Health System.", () => (hunchFlags & HFDash) != 0);
        HAdd("DELIVERABLES", "Biomedical simulation: wearable data (heart rate, sleep, mood)", "On the dashboard, open one crew member.", () => (hunchFlags & HFCrew) != 0);
        HAdd("DELIVERABLES", "Adaptive feedback / intervention system", "Accept one of the AI's suggestions for a crew member.", () => sim.psychHelped > 0);
        HAdd("DELIVERABLES", "Personalized countermeasures, real-time reporting", "Open the Crew health report (it lists each person's own best countermeasure).", () => (hunchFlags & HFReport) != 0);
        HAdd("DELIVERABLES", "Working demo: personal space", "Walk into the Crew Quarters.", () => ZoneSeen("quarters", "sleep"));
        HAdd("DELIVERABLES", "Working demo: common space", "Walk into the Galley.", () => ZoneSeen("galley"));
        HAdd("DELIVERABLES", "Destressors with feedback", "Use the Sanctuary, or accept an AI suggestion.", () => ZoneSeen("sanctuary", "psych", "observation") || sim.psychHelped > 0);
        HAdd("KEY CONSIDERATIONS", "Dimensions: about 20 to 120 cubic metres for each crew member", "Open Habitat facts and requirements.", () => (hunchFlags & HFFacts) != 0);
        HAdd("KEY CONSIDERATIONS", "Structure: modular, expandable, radiation-shielded", "Go outside through the Airlock and look at the regolith cover.", () => (hunchFlags & HFOutside) != 0);
        HAdd("KEY CONSIDERATIONS", "AI monitoring: real-time, privacy-respecting, adaptive", "Open Privacy and consent.", () => (hunchFlags & HFConsent) != 0);
        HAdd("KEY CONSIDERATIONS", "Smart systems: life support (ECLSS), environment and power", "Open the Smart systems dashboard.", () => (hunchFlags & HFEclss) != 0);
        HAdd("KEY CONSIDERATIONS", "Environmental controls you can use", "Switch a light or a fan on a zone console.", () => switchUses > 0);
        HAdd("KEY CONSIDERATIONS", "Adaptive lighting through a 24-hour day", "Stay until the habitat's evening (19:00) or early morning.", () => (hunchFlags & HFEvening) != 0);
        HAdd("KEY CONSIDERATIONS", "Social prompts", "Hold a team meeting: press the big round button on the table in the centre dome (or F4).", () => (hunchFlags & HFMeeting) != 0);
        HAdd("KEY CONSIDERATIONS", "VR / AR", "Play with the headset on.", () => (hunchFlags & HFHeadset) != 0);
        HAdd("KEY CONSIDERATIONS", "Signals: voice, face, group interaction, laughter, tears, isolation", "Open Sensors and what they detect.", () => (hunchFlags & HFSensors) != 0);
        HAdd("KEY CONSIDERATIONS", "Voice channel", "Talk to the crew on the radio (your microphone).", () => radioSent > 0);
        HAdd("KEY CONSIDERATIONS", "Ethical AI: response levels, consent, hard limits", "Open AI response protocol, consent and hard limits.", () => (hunchFlags & HFProtocol) != 0);
        HAdd("LUNAR CONSTRAINTS", "Dust", "Walk into the Airlock (dust mitigation).", () => ZoneSeen("airlock"));
        HAdd("LUNAR CONSTRAINTS", "Limited resupply", "Walk into Storage.", () => ZoneSeen("storage"));
        HAdd("LUNAR CONSTRAINTS", "Autonomy with little help from Earth", "Let ARIA send a service robot by itself (it does this while you play).", () => (hunchFlags & HFAuto) != 0);
        HAdd("LUNAR CONSTRAINTS", "Power: solar arrays with a backup", "Walk into the power zone.", () => ZoneSeen("power", "arrays"));
        HAdd("ROOMS", "Life support room", "Walk into Life Support.", () => ZoneSeen("eclss", "life support", "scrubber", "water"));
        HAdd("ROOMS", "Laboratory / workspace", "Walk into the Workstations.", () => ZoneSeen("work", "control", "lab"));
        HAdd("ROOMS", "Hygiene area", "Walk into Hygiene.", () => ZoneSeen("hygiene"));
        HAdd("ROOMS", "Medical bay", "Walk into the Medical Bay.", () => ZoneSeen("medical"));
        HAdd("ROOMS", "Plants outside in sunlight, fish inside the base", "Walk into the Greenhouse (plants, in sunlight) and into Life Support (the fish tank is inside there).", () => ZoneSeen("greenhouse") && ZoneSeen("eclss", "life support"));
        for (int i = 0; i < hunchItems.Count; i++) hunchItems[i].done = PlayerPrefs.GetInt("hunch_item_" + i, 0) == 1;
        if (!menu) return;
        pageHunch = new HunchMenu.ExtraPage { label = HunchLabel(), hint = "ENABLED shows only what the NASA HUNCH project sheet asks for: the main base, the crew, the AI health monitoring and the smart systems, with a checklist. Disabled brings back everything else (maps, mini-games, aliens, shop).", direct = () => { HunchSet(!hunchOn); if (hunchOn) HunchAskCheats(); else menu.Rebuild(); } };
        pageCheck = new HunchMenu.ExtraPage { title = "NASA HUNCH CHECKLIST", label = "NASA HUNCH checklist and documents", hint = "One box for every item the project sheet says must be there. A box ticks itself when you show that item in the game." };
        pageCheck.build = m =>
        {
            int done = 0; foreach (var it in hunchItems) if (it.done) done++;
            m.AddItem("Checked " + done + " of " + hunchItems.Count + "   (untick all)", "Clears every tick so you can show the items again for a new audience.", () => { for (int i = 0; i < hunchItems.Count; i++) { hunchItems[i].done = false; PlayerPrefs.SetInt("hunch_item_" + i, 0); } hunchFlags = 0; hunchZones = 0; m.Rebuild(); });
            m.AddItem("Document: Habitat facts and requirements", "Crew size, volume for each person, levels, shielding, site, power, autonomy.", () => { m.Close(); HunchSheet(0, HFFacts); });
            m.AddItem("Document: Sensors and what they detect", "Which sensor finds which condition, and what happens when one fails.", () => { m.Close(); HunchSheet(1, HFSensors); });
            m.AddItem("Document: AI response protocol, consent and hard limits", "Mild, moderate and severe responses, and what the AI is never allowed to do.", () => { m.Close(); HunchSheet(2, HFProtocol); });
            m.AddItem("Smart systems dashboard (live)", "Life support, environment and power, updated while you read.", () => { m.Close(); HunchSheet(3, HFEclss); });
            m.AddItem("Crew health report (live)   and save as a file", "Each crew member's vitals, class, condition and own best countermeasure, and the team totals. Also saved in a Reports folder next to the game.", () => { m.Close(); HunchSaveReport(); HunchSheet(4, HFReport); });
            m.AddItem("Privacy and consent", "Each crew member can switch AI monitoring off. The AI then stops watching that person and writes it in the log.", () => { hunchFlags |= HFConsent; m.OpenExtra(pageConsent); });
            string g = "";
            foreach (var it in hunchItems)
            {
                if (it.group != g) { g = it.group; m.AddItem("<color=#FFD070>-- " + g + " --</color>", "", () => { }); }
                m.AddItem((it.done ? "<color=#7CFC9A>[x]</color> " : "[  ] ") + it.text, it.done ? "Shown." : "To tick this box: " + it.how, () => { });
            }
        };
        pageConsent = new HunchMenu.ExtraPage { title = "PRIVACY AND CONSENT", parent = pageCheck };
        pageConsent.build = m =>
        {
            for (int i = 0; i < 4 && i < sim.crew.Count; i++)
            {
                int ii = i;
                m.AddItem(sim.crew[i].name + ": AI monitoring " + (hunchConsent[i] ? "<color=#7CFC9A>CONSENT GIVEN</color>" : "<color=#FF9A8A>SWITCHED OFF by the crew member</color>"), "The private sleeping pods have no cameras. A crew member can refuse monitoring or any intervention; the AI must stop and log it. Only the flight surgeon can override, and only for a safety emergency.", () =>
                {
                    hunchConsent[ii] = !hunchConsent[ii]; PlayerPrefs.SetInt("hunch_consent_" + ii, hunchConsent[ii] ? 1 : 0);
                    HsNote(sim.crew[ii].name, hunchConsent[ii] ? "consent given: AI monitoring resumed" : "consent withdrawn: AI monitoring stopped and logged"); m.Rebuild();
                });
            }
        };
        menu.extraPages.Insert(0, pageHunch); menu.extraPages.Insert(1, pageCheck);      // the checklist and documents can be opened in both modes
        menu.extraShow = p => !hunchOn || p == pageHunch || p == pageCheck || p == pageBase;      // pages are hidden, never removed, so nothing is lost when the mode is switched off
        if (hunchOn) HunchApply(true);
    }
    string HunchLabel() { return hunchOn ? "NASA HUNCH mode: <color=#7CFC9A>ENABLED</color>" : "NASA HUNCH mode: disabled"; }
    void HunchSheet(int k, int flag) { hunchFlags |= flag; if (hunchSheetFirst >= 0) OpenRead(hunchSheetFirst + k); }

    bool cheatTold;
    public void HunchSet(bool on)
    {
        if (hunchOn == on) return; hunchOn = on; PlayerPrefs.SetInt("hunch_mode2", on ? 1 : 0); HunchApply(on);
        Banner(on ? "NASA HUNCH MODE" : "FULL GAME", on ? "Only the base, the crew and the rubric" : "Maps, mini-games and everything else are back", false);
        sim.Say(on ? "ARIA: NASA HUNCH mode. Only the main base is shown. Open the menu for the checklist and the documents." : "ARIA: Full game. Everything is switched back on.");
    }

    void HunchApply(bool on)
    {
        if (pageHunch != null) pageHunch.label = HunchLabel();
        var aria = GetComponent<HunchAria>();
        if (on)
        {
            if (mapNow != 0) { LeaveMapQuiet(); game.GoTo(0); }
            if (autoOn) StopAutopilot("NASA HUNCH mode");
            hunchAliensWere = aliensOn; aliensOn = false; tool = Hands; if (lvOpen) LvClose();
            if (aria) { hunchCareWas = aria.autoCare; aria.autoCare = true; }
            if (menu) menu.rocketHidden = true; if (fx) fx.rocketOff = true;
            HunchWorld(true);
        }
        else
        {
            aliensOn = hunchAliensWere; if (aria) aria.autoCare = hunchCareWas;
            if (menu) menu.rocketHidden = false; if (fx) fx.rocketOff = false;
            HunchWorld(false);
            if (hunchRocket && !hunchRocket.activeSelf) hunchRocket.SetActive(true);      // the full game gets its spaceport rocket back
        }
    }

    /// the outer size of the building the crew rooms are in: every part of the model whose middle lies among those rooms
    bool HunchHabitatBounds(Vector3 lo, Vector3 hi, out Bounds b)
    {
        b = new Bounds(); bool any = false; var model = GameObject.Find("HUNCH_Lunar_Base"); if (!model) return false;
        var box = new Bounds((lo + hi) * 0.5f, (hi - lo) + new Vector3(14f, 30f, 14f));
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            var rb = r.bounds; if (rb.size.x > 70f || rb.size.z > 70f || !box.Contains(rb.center)) continue;
            if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
        }
        return any && b.size.x < 90f && b.size.z < 90f;
    }
    void HunchCrewBox(out Vector3 lo, out Vector3 hi)
    {
        lo = new Vector3(1e9f, 1e9f, 1e9f); hi = -lo; int n = 0;
        foreach (string k in new[] { "airlock", "hygiene", "quarters", "galley", "work", "storage", "eclss", "medical", "sanctuary", "exercise" }) { int i = ZoneIndex(k); if (i < 0) continue; lo = Vector3.Min(lo, game.zones[i].position); hi = Vector3.Max(hi, game.zones[i].position); n++; }
        if (n == 0) { lo = new Vector3(-10f, 0f, -10f); hi = new Vector3(10f, 6f, 10f); }
    }

    /// hides everything that is not the main base, and builds the regolith cover, the outside greenhouses and the fish tank
    void HunchWorld(bool on)
    {
        if (on)
        {
            hunchHidden.Clear(); var model = GameObject.Find("HUNCH_Lunar_Base");
            if (model) foreach (Transform ch in model.transform)
            {
                if (!ch.gameObject.activeSelf) continue; string n = ch.name.ToUpperInvariant(); var r = ch.GetComponent<Renderer>(); Vector3 c = r ? r.bounds.center : ch.position;
                bool sky = n.Contains("TERRAIN") || n.Contains("ROCKS") || n.Contains("DETAILS") || n.Contains("EARTH"), far = new Vector2(c.x, c.z).magnitude > 150f;
                if (n.Contains("KINGDOM_DOME") || n.Contains("KINGDOM_SPIRE") || n.Contains("KINGDOM_HELIX") || (far && !sky && !n.Contains("TELECOM"))) { ch.gameObject.SetActive(false); hunchHidden.Add(ch.gameObject); }      // the monumental parts no first crew could build, and the two domes no zone uses
            }
            foreach (Transform ch in root) { string n = ch.name; if (ch.gameObject.activeSelf && (n == "Map gate" || n == "Season at the gate" || n == "Portal plaza" || n == "Fun zone" || n.StartsWith("Flying: ") || n == "Rocket on its pad" || n.Contains("Orbital cannon") || n.Contains("Guard turret"))) { ch.gameObject.SetActive(false); hunchHidden.Add(ch.gameObject); } }
            // the central hub keeps three floors only: the tower above Mission Control is taken off and a low dome (modelled in Blender) roofs it
            if (model)
            {
                var upper = new List<GameObject>(); float capY = 24f;
                int hubStyle = PlayerPrefs.GetInt("hunch_hub_style", 0);      // 0 = the hub's own wall, shortened (default); 1 = the pressure shell with regolith bags, modelled in Blender
                foreach (string part in new string[0])      /* the central hub stays the original tall tower in both modes, as it was */ { var t = model.transform.Find(part); if (t && t.gameObject.activeSelf) { t.gameObject.SetActive(false); upper.Add(t.gameObject); } }
                var f3 = model.transform.Find("BAKED_HUB_F3"); if (f3) { var fr = f3.GetComponent<Renderer>(); if (fr) capY = fr.bounds.max.y; }
                Physics.SyncTransforms(); bool floorOk = true;
                foreach (var z in game.zones) if (!Physics.Raycast(z.position + Vector3.up * 1.0f, Vector3.down, 3.5f, ~0, QueryTriggerInteraction.Ignore)) floorOk = false;
                if (!floorOk) { foreach (var g in upper) g.SetActive(true); upper.Clear(); hunchPlaced += "hub tower kept (a zone stands on it); "; }
                hunchHidden.AddRange(upper); hubCapOn = hubStyle == 1 && upper.Count > 0; hubCapY = capY;
                // no new shell: the hub keeps its own gold-and-glass wall and its own dome. The wall is shortened to three floors and the dome is lowered onto it.
                if (upper.Count > 0 && !hubShort && hubStyle == 0)
                {
                    hubTS = model.transform.Find("BAKED_HUB_Tower_Shell"); hubTR = model.transform.Find("BAKED_HUB_TowerRoof");
                    var sr2 = hubTS ? hubTS.GetComponent<Renderer>() : null; var rr2 = hubTR ? hubTR.GetComponent<Renderer>() : null;
                    if (sr2 && rr2)
                    {
                        hubTSPos = hubTS.position; hubTSScale = hubTS.localScale; hubTRPos = hubTR.position; Bounds sb2 = sr2.bounds, rb2 = rr2.bounds;
                        float over = Mathf.Clamp(sb2.max.y - rb2.min.y, 0f, 8f), sy = Mathf.Clamp((capY + 0.1f + over - sb2.min.y) / Mathf.Max(1f, sb2.size.y), 0.2f, 1f);
                        hubTS.localScale = new Vector3(hubTSScale.x, hubTSScale.y * sy, hubTSScale.z); hubTS.position = hubTSPos + Vector3.up * ((sb2.min.y - hubTSPos.y) * (1f - sy));
                        hubTR.position = hubTRPos + Vector3.up * (capY + 0.1f - rb2.min.y); hubShort = true; hunchPlaced += "hub wall shortened x" + sy.ToString("F2") + ", dome lowered " + (rb2.min.y - capY - 0.1f).ToString("F1") + " m; ";
                    }
                }
                var rk = model.transform.Find("BAKED_ROCKET"); hunchRocket = rk ? rk.gameObject : null;
            }
            Physics.SyncTransforms(); if (!hunchRoot) BuildHunchWorld();
            if (hubCapOn && !hubCap)
            {
                // the three floors that stay get a new pressure shell, modelled in Blender: a closed wall with a window band, a regolith-bag berm against it and a regolith-covered dome roof
                float baseY = 8.6f; var bs = model ? model.transform.Find("BAKED_HUB_Shell") : null; var bsr = bs ? bs.GetComponent<Renderer>() : null; if (bsr) baseY = bsr.bounds.max.y;
                try { hubCap = Kit(false, "HubShell", hunchRoot, new Vector3(0f, baseY, 0f), 0f, 59.67f, false); } catch { hubCap = null; }
                if (hubCap)
                {
                    hubCap.name = "Hub pressure shell (Blender)";
                    var ring = new GameObject("Hub wall (solid)").transform; ring.SetParent(hubCap.transform, true); ring.position = new Vector3(0f, baseY, 0f); ring.rotation = Quaternion.identity;
                    for (int k = 0; k < 24; k++) { var seg = new GameObject("wall").transform; seg.SetParent(ring, false); float a = k * 15f; seg.localRotation = Quaternion.Euler(0f, a, 0f); seg.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 7.9f, 25.2f); var bc = seg.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(7f, 15.8f, 0.5f); }
                    var hl = Label(hunchRoot, "<b>CENTRAL HUB</b>\n<size=55%>Three floors under one pressure shell. Regolith bags against the wall and a regolith blanket on the roof shield the crew from radiation and micrometeorites.</size>", 0f, 0f, 6f, 60f, 6f, new Color(1f, 0.84f, 0.3f)); hl.transform.position = new Vector3(0f, baseY + 28f, 0f); hl.transform.SetParent(hubCap.transform, true); mapLabels.Add(hl);
                }
                else { hubCap = Kit(false, "HubCap", hunchRoot, new Vector3(0f, hubCapY - 0.3f, 0f), 0f, 54f, false); if (hubCap) hubCap.name = "Hub roof (Blender)"; }
            }
            if (hubCap) hubCap.SetActive(hubCapOn);
            hunchRoot.gameObject.SetActive(true);
        }
        else
        {
            foreach (var g in hunchHidden) if (g) g.SetActive(true); hunchHidden.Clear();
            if (hubShort) { if (hubTS) { hubTS.localScale = hubTSScale; hubTS.position = hubTSPos; } if (hubTR) hubTR.position = hubTRPos; hubShort = false; }      // the full game gets the whole tower back
            if (hunchRoot) hunchRoot.gameObject.SetActive(false);
        }
        Physics.SyncTransforms();
    }

    float GroundAt(float x, float z, float fallback)
    {
        RaycastHit h; return Physics.Raycast(new Vector3(x, 400f, z), Vector3.down, out h, 800f, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : fallback;
    }
    void BuildHunchWorld()
    {
        hunchRoot = new GameObject("NASA HUNCH world").transform; hunchRoot.SetParent(root, false); hunchRoot.position = Vector3.zero; hunchRoot.rotation = Quaternion.identity;
        int gz = ZoneIndex("greenhouse"); Vector3 gp = gz >= 0 ? game.zones[gz].position : new Vector3(30f, 0.2f, 0f); hunchGround = game.zones.Count > 0 ? game.zones[0].position.y : 0f;
        // ---- regolith radiation cover over the living ring (seen only from outside, casts no shadow into the rooms)
        var reg = new Material(Lit(new Color(0.43f, 0.42f, 0.41f))); if (reg.HasProperty("_BaseMap")) reg.SetTextureScale("_BaseMap", new Vector2(14f, 8f)); if (reg.HasProperty("_Smoothness")) reg.SetFloat("_Smoothness", 0.08f);
        Vector3 lo, hi; HunchCrewBox(out lo, out hi); Vector3 mid = (lo + hi) * 0.5f; float rx = (hi.x - lo.x) * 0.5f + 21f, rz = (hi.z - lo.z) * 0.5f + 21f, ry = (hi.y - hunchGround) + 16f, rad = 96f;
        Bounds hb; if (HunchHabitatBounds(lo, hi, out hb)) { mid = hb.center; rx = hb.extents.x * 1.38f + 2f; rz = hb.extents.z * 1.38f + 2f; ry = (hb.max.y - hunchGround) * 1.3f + 1.5f; }      // an ellipsoid this much bigger than the building covers all of it
        // the protection layer must not overlap a neighbouring building: shrink it until it clears every other dome (tunnels may pass through it, as they would for real)
        {
            var mdl = GameObject.Find("HUNCH_Lunar_Base"); float best = 1f; int hits0 = -1;
            var crew = new Bounds((lo + hi) * 0.5f, (hi - lo) + new Vector3(14f, 30f, 14f)); var others = new List<Bounds>();
            if (mdl) foreach (var r in mdl.GetComponentsInChildren<Renderer>())
            {
                var rb = r.bounds; string n = r.name.ToUpperInvariant();
                if (!r.gameObject.activeInHierarchy || rb.size.x > 70f || rb.size.z > 70f || crew.Contains(rb.center) || rb.max.y < hunchGround + 2.5f) continue;
                if (n.Contains("TUNNEL") || n.Contains("NODE") || n.Contains("CORE") || n.Contains("GROUND") || n.Contains("TERRAIN") || n.Contains("SITE")) continue;
                others.Add(rb);
            }
            for (float sc = 1f; sc >= 0.74f; sc -= 0.02f)
            {
                float ex = rx * sc * 1.03f, ez2 = rz * sc * 1.03f; int hits = 0;
                foreach (var ob in others) { float cx = Mathf.Clamp(mid.x, ob.min.x, ob.max.x) - mid.x, cz = Mathf.Clamp(mid.z, ob.min.z, ob.max.z) - mid.z; if (cx * cx / (ex * ex) + cz * cz / (ez2 * ez2) < 1f) hits++; }
                if (hits0 < 0) hits0 = hits; best = sc; if (hits == 0) break;
            }
            rx *= best; rz *= best; hunchPlaced += "cover scale " + best.ToString("F2") + " (overlapped " + hits0 + " of " + others.Count + " buildings before); ";
        }
        hunchRad = Mathf.Max(rx, rz); hunchCover = new Vector3(rx, ry, rz);
        // loose Moon rocks never sit in or under the protection layer
        foreach (var rk in rocks) if (rk != null && rk.t)
        {
            Vector3 q = rk.t.position - mid; q.y = 0f; float e = q.x * q.x / (rx * rx * 1.08f) + q.z * q.z / (rz * rz * 1.08f);
            if (e < 1.2f) { if (q.sqrMagnitude < 0.01f) q = Vector3.right; Vector3 np = mid + q * (1.15f / Mathf.Sqrt(Mathf.Max(e, 0.0001f))); np.y = GroundAt(np.x, np.z, hunchGround) + 0.3f; rk.t.position = np; if (rk.rb) { rk.rb.linearVelocity = Vector3.zero; rk.rb.angularVelocity = Vector3.zero; } }
        }
        var shell = Prim(PrimitiveType.Sphere, hunchRoot, new Vector3(mid.x, hunchGround - 0.5f, mid.z), new Vector3(rx * 2f, ry * 2f, rz * 2f), reg); shell.name = "Regolith radiation cover";
        var sr = shell.GetComponent<Renderer>(); sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // the same cover shown part-built: the far end is buried, the entrance end still stands in the open with a low berm round it, so the habitat can be seen
        coverFull = shell;
        coverCut = Prim(PrimitiveType.Sphere, hunchRoot, new Vector3(mid.x + rx * 0.5f, hunchGround - 0.5f, mid.z), new Vector3(rx * 1.2f, ry * 2f, rz * 2f), reg); coverCut.name = "Regolith radiation cover (part-built)";
        coverCut.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        coverBerm = Prim(PrimitiveType.Sphere, hunchRoot, new Vector3(mid.x, hunchGround - 0.5f, mid.z), new Vector3(rx * 2.04f, ry * 0.42f, rz * 2.04f), reg); coverBerm.name = "Regolith berm";
        coverBerm.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hunchCoverMode = PlayerPrefs.GetInt("hunch_cover2", 2); HunchCoverApply();      // the regolith mound is not shown unless you choose it
        // ---- greenhouses outside, on the side of the base nearest the greenhouse zone
        Vector3 dir = new Vector3(gp.x, 0f, gp.z); if (dir.sqrMagnitude < 1f) dir = Vector3.right; dir.Normalize(); Vector3 side = Vector3.Cross(Vector3.up, dir);
        var glass = Clear(new Color(0.7f, 0.92f, 1f, 0.22f)); var frame = Lit(new Color(0.8f, 0.82f, 0.86f)); var soil = Lit(new Color(0.27f, 0.17f, 0.1f)); var leaf = Lit(new Color(0.2f, 0.62f, 0.25f)); var fruit = Lit(new Color(0.9f, 0.25f, 0.2f));
        bool found = false;
        for (int ring = 0; ring < 10 && !found; ring++) for (int k = 0; k < 33 && !found; k++)
        {
            float off = (k == 0 ? 0f : ((k + 1) / 2) * 11f * (k % 2 == 0 ? -1f : 1f)); Vector3 d2 = Quaternion.Euler(0f, off, 0f) * dir, s2 = Vector3.Cross(Vector3.up, d2); float r2 = 88f + ring * 6f; bool free = true;
            for (int g = -1; g <= 1 && free; g++) { Vector3 c = d2 * r2 + s2 * (g * 13f); float gy = GroundAt(c.x, c.z, hunchGround); if (!SiteFlat(c, 10.5f, 1.2f) || Mathf.Abs(gy - GroundAt(c.x + 4f, c.z + 4f, hunchGround)) > 1.2f || Physics.CheckBox(new Vector3(c.x, gy + 3.2f, c.z), new Vector3(4.6f, 2.6f, 9.6f), Quaternion.LookRotation(d2, Vector3.up), ~0, QueryTriggerInteraction.Ignore)) free = false; }
            if (free) { found = true; dir = d2; side = s2; rad = r2; hunchPlaced += "greenhouses ring " + ring + " offset " + off + "; "; }
        }
        if (!found) hunchPlaced += "greenhouses: no fully free spot, default used; ";
        for (int g = -1; g <= 1; g++)
        {
            Vector3 at = dir * rad + side * (g * 13f); at.y = GroundAt(at.x, at.z, hunchGround);
            var gh = new GameObject("Greenhouse " + (g + 2)).transform; gh.SetParent(hunchRoot, false); gh.position = at; gh.rotation = Quaternion.LookRotation(dir, Vector3.up);
            var tube = Prim(PrimitiveType.Cylinder, gh, new Vector3(0f, 0f, 0f), new Vector3(8f, 9f, 8f), glass); tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cube, gh, new Vector3(0f, 0.1f, 0f), new Vector3(8.2f, 0.2f, 18f), frame);
            for (int k = 0; k < 4; k++) { var hoop = Prim(PrimitiveType.Cylinder, gh, new Vector3(0f, 0f, -8.6f + k * 5.75f), new Vector3(8.3f, 0.12f, 8.3f), frame); hoop.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); }
            for (int row = -1; row <= 1; row += 2)
            {
                Prim(PrimitiveType.Cube, gh, new Vector3(row * 1.9f, 0.4f, 0f), new Vector3(1.6f, 0.4f, 16f), soil);
                for (int p = 0; p < 9; p++) { float z = -7.2f + p * 1.8f, hgt = 0.7f + ((p * 7 + g * 3 + row) & 3) * 0.18f; Prim(PrimitiveType.Capsule, gh, new Vector3(row * 1.9f, 0.6f + hgt * 0.5f, z), new Vector3(0.55f, hgt * 0.5f, 0.55f), leaf); if (p % 2 == 0) Prim(PrimitiveType.Sphere, gh, new Vector3(row * 1.9f + 0.25f, 0.7f + hgt * 0.6f, z), Vector3.one * 0.2f, fruit); }
            }
            if (g == 0)
            {
                var link = Prim(PrimitiveType.Cylinder, gh, new Vector3(0f, 0.5f, -11.5f), new Vector3(1.0f, 2.6f, 1.0f), frame); link.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var l = Label(gh, "<b>GREENHOUSES</b>\n<size=55%>Outside the radiation cover, in sunlight. Water rich in fish waste is piped out from the tank inside the base.</size>", 0f, 0f, 9f, 60f, 8f, new Color(1f, 0.84f, 0.3f)); l.transform.localPosition = new Vector3(0f, 9f, 0f); mapLabels.Add(l);
            }
        }
        var sl = Label(hunchRoot, "<b>REGOLITH RADIATION COVER</b>\n<size=55%>Loose Moon soil piled over the crew habitat shields the crew from radiation and micrometeorites.</size>", 0f, 0f, 6f, 60f, 6f, new Color(1f, 0.84f, 0.3f)); sl.transform.position = new Vector3(mid.x, hunchGround + ry + 4f, mid.z); mapLabels.Add(sl); coverLabel = sl; HunchCoverApply();
        // ---- the aquaponics fish tank, inside the base beside the greenhouse zone
        int ez = ZoneIndex("eclss", "life support"); if (ez < 0) ez = gz; Vector3 ep = ez >= 0 ? game.zones[ez].position : gp; Vector3 td = Quaternion.Euler(0f, (ez >= 0 ? game.zones[ez].yaw : 0f) - 70f, 0f) * Vector3.forward; Vector3 tf; if (FreeNear(ep, ez >= 0 ? game.zones[ez].yaw + 110f : 0f, new Vector3(0.85f, 0.95f, 0.45f), new[] { 1.9f, 2.6f, 3.3f }, out hunchTankAt, out tf)) { td = tf; hunchPlaced += "tank free spot; "; } else hunchPlaced += "tank: no free spot, default used; ";
        var tank = new GameObject("Aquaponics fish tank").transform; tank.SetParent(hunchRoot, false); tank.position = hunchTankAt; tank.rotation = Quaternion.LookRotation(td, Vector3.up);
        Prim(PrimitiveType.Cube, tank, new Vector3(0f, 0.35f, 0f), new Vector3(1.5f, 0.7f, 0.7f), Lit(new Color(0.16f, 0.17f, 0.2f)));
        Prim(PrimitiveType.Cube, tank, new Vector3(0f, 1.05f, 0f), new Vector3(1.4f, 0.7f, 0.6f), Clear(new Color(0.3f, 0.7f, 1f, 0.35f)));
        Prim(PrimitiveType.Cube, tank, new Vector3(0f, 1.43f, 0f), new Vector3(1.5f, 0.06f, 0.7f), frame); Prim(PrimitiveType.Cylinder, tank, new Vector3(0.6f, 1.9f, 0.2f), new Vector3(0.07f, 0.5f, 0.07f), Lit(new Color(0.3f, 0.6f, 0.9f)));
        for (int i = 0; i < 5; i++) { var f = Prim(PrimitiveType.Sphere, tank, new Vector3(0f, 1.05f, 0f), new Vector3(0.16f, 0.08f, 0.06f), Lit(i % 2 == 0 ? new Color(0.95f, 0.5f, 0.1f) : new Color(0.9f, 0.85f, 0.8f))); hunchFish.Add(f.transform); }
        Solid(tank, new Vector3(0f, 0.75f, 0f), new Vector3(1.5f, 1.5f, 0.7f));
        var tl = Label(tank, "<b>AQUAPONICS</b>\n<size=60%>Fish waste feeds the plants in the greenhouses outside; the plants clean the water for the fish.</size>", 0f, 0f, 0.9f, 3.2f, 0.9f, Color.white); tl.transform.localPosition = new Vector3(0f, 2.5f, 0f); mapLabels.Add(tl);
    }

    Transform hubTS, hubTR; Vector3 hubTSPos, hubTSScale, hubTRPos; bool hubShort;
    GameObject coverFull, coverCut, coverBerm, hubCap, hunchRocket; bool hubCapOn; float hubCapY; int hunchCoverMode; TMPro.TextMeshPro coverLabel;
    static readonly string[] CoverName = { "part-built (the habitat can be seen)", "complete (the habitat is buried)", "hidden" };
    public string ProbeCover() { hunchCoverMode = probeInt; HunchCoverApply(); return "cover mode " + hunchCoverMode + " | " + hunchPlaced; }
    void HunchCoverApply()
    {
        if (coverFull) coverFull.SetActive(hunchCoverMode == 1); if (coverCut) coverCut.SetActive(hunchCoverMode == 0); if (coverBerm) coverBerm.SetActive(hunchCoverMode == 0);
        if (coverLabel) coverLabel.text = hunchCoverMode == 2 ? "" : "<b>REGOLITH RADIATION COVER</b>\n<size=55%>" + (hunchCoverMode == 0 ? "Robots are still piling Moon soil over the crew habitat. This end is left open so you can see it." : "Loose Moon soil piled over the crew habitat shields the crew from radiation and micrometeorites.") + "</size>";
    }
    void HunchCoverRow(HunchMenu m)
    {
        m.AddItem("Regolith cover: " + CoverName[Mathf.Clamp(hunchCoverMode, 0, 2)], "The radiation cover over the crew habitat. Part-built leaves the entrance end in the open so the habitat can still be seen; complete buries all of it, as the finished base would be; hidden takes it away.", () => { hunchCoverMode = (hunchCoverMode + 1) % 3; PlayerPrefs.SetInt("hunch_cover3", hunchCoverMode); HunchCoverApply(); m.Rebuild(); });
    }

    void UpdateHunch(float dt)
    {
        if (!Playing) return;
        if (HunchCheats.Creative != Creative) { HunchCheats.Creative = Creative; if (!HunchCheats.FlyOk) game.flyMode = false; if (pageCheat != null) pageCheat.label = CheatLabel(); }
        if (!cheatTold && !Creative && !hunchOn) { cheatTold = true; Note("Survival: cheats start off. Menu > Cheats if you want one."); }
        if (HunchCheats.Hunch != hunchOn) { HunchCheats.Hunch = hunchOn; if (!HunchCheats.FlyOk) game.flyMode = false; if (pageCheat != null) pageCheat.label = CheatLabel(); }      // each mode has its own cheat switches
        if (hunchOn) { if (tool != Hands) tool = Hands; if (aliensOn) aliensOn = false; if (hunchRocket && hunchRocket.activeSelf) { hunchRocket.SetActive(false); if (!hunchHidden.Contains(hunchRocket)) hunchHidden.Add(hunchRocket); } }      // no rocket hanging in the sky: the spaceport is not part of this base
        float t = Time.time; for (int i = 0; i < hunchFish.Count; i++) { var f = hunchFish[i]; if (!f) continue; float a = t * (0.5f + i * 0.13f) + i * 1.3f; f.localPosition = new Vector3(Mathf.Sin(a) * 0.55f, 1.05f + Mathf.Sin(a * 1.7f) * 0.2f, Mathf.Cos(a * 1.3f) * 0.18f); f.localRotation = Quaternion.Euler(0f, Mathf.Cos(a) > 0f ? 0f : 180f, 0f); }
        if (game.NearZone >= 0 && game.NearZone < 31) hunchZones |= 1 << game.NearZone;
        if (mtState == 2) hunchFlags |= HFMeeting; if (game.HeadsetActive) hunchFlags |= HFHeadset; if (fx && fx.Outside) hunchFlags |= HFOutside;
        if (sim.phase == HabPhase.Mission && (sim.Hour >= 19f || sim.Hour < 6f)) hunchFlags |= HFEvening;
        if (sim.message != null && sim.message.StartsWith("ARIA auto-care")) hunchFlags |= HFAuto;
        hunchTick -= dt; if (hunchTick > 0f) return; hunchTick = 0.5f;
        for (int i = 0; i < hunchItems.Count; i++)
        {
            var it = hunchItems[i]; if (it.done) continue; bool ok = false; try { ok = it.test(); } catch { }
            if (ok) { it.done = true; PlayerPrefs.SetInt("hunch_item_" + i, 1); if (hunchOn) { Play(cPick, 0.6f); Note("Checklist: " + it.text + "   [x]"); } }
        }
    }
    string HunchHud()
    {
        if (!hunchOn || !Playing) return ""; int done = 0; HItem next = null; foreach (var it in hunchItems) { if (it.done) done++; else if (next == null) next = it; }
        return "<color=#7CFC9A><b>NASA HUNCH</b>  checklist " + done + " of " + hunchItems.Count + "</color>" + (next != null ? "   next: " + next.how : "   <b>everything shown</b>");
    }

    // ------------------------------------------------------------------ the documents
    string HunchFacts()
    {
        var sb = new StringBuilder(); Vector3 lo, hi; HunchCrewBox(out lo, out hi); int levels = hi.y - lo.y > 3f ? 2 : 1; float vol = (hi.x - lo.x + 8f) * (hi.z - lo.z + 8f) * levels * 2.6f;
        int crew = Mathf.Max(1, sim.crew.Count);
        sb.Append("WHAT THE PROJECT SHEET ASKS FOR, AND WHAT THIS BASE HAS\n\n");
        sb.Append("Crew: 2 astronauts as standard, 4 at peak. This base: ").Append(crew).Append(" people in all, counting you (the Commander) and ").Append(crew - 1).Append(" crewmates, each with a private sleep pod.\n\n");
        sb.Append("Volume: about 20 to 120 cubic metres for each crew member. ");
        if (vol > 1f) sb.Append("This model's crew habitat is roughly ").Append(Mathf.RoundToInt(vol / 100f) * 100).Append(" cubic metres on ").Append(levels).Append(levels > 1 ? " levels" : " level").Append(" (estimated from where its rooms are), about ").Append(Mathf.RoundToInt(vol / crew / 10f) * 10).Append(" for each person. The walk-through model is drawn at about twice life size so the rooms are comfortable in a headset: its decks are about 5.9 m apart, and a real deck is about 2.9 m. At true size (half of every length) the living space is about ").Append(Mathf.RoundToInt(vol / 8f / 10f) * 10).Append(" cubic metres, about ").Append(Mathf.RoundToInt(vol / 8f / crew / 5f) * 5).Append(" for each person: inside the 20 to 120 range.\n\n"); else sb.Append("See the model's dimensions.\n\n");
        sb.Append("HOW IT COULD REALLY BE BUILT: HARD, NOT IMPOSSIBLE\nPhase 1 (this proposal): one two-deck pill habitat for 2 to 4 people. A rigid lower deck lands on a cargo lander; the upper deck is inflatable and is blown up on site. Robots pile regolith over it before the crew arrives. A 10 kW fission unit and solar arrays stand apart from it. Greenhouse tubes and the fish tank come with the first crew.\nPhase 2 (years later): a greenhouse dome and a workshop, joined by tunnels.\nPhase 3 (the long-term vision): the central hub and the rest of the base. NASA HUNCH mode hides the outer dome, the tower, the spire, the telecom mast and the far sites because no first crew could build them.\n\n");
        sb.Append("Structure: modular rooms joined by corridors, on more than one level, with space to add modules for a bigger crew. The crew habitat is under a regolith cover for radiation and micrometeorite protection.\n\n");
        sb.Append("Rooms: airlock (dust mitigation, suit port), storage, laboratory and workstations, hygiene, crew quarters, galley, medical bay, life support, sanctuary. Noisy work areas are kept away from quiet sleeping areas.\n\n");
        sb.Append("Food: greenhouses stand outside the cover in sunlight. An aquaponics fish tank inside the habitat, by Life Support, sends water rich in fish waste to the plants; the plants clean it for the fish.\n\n");
        sb.Append("Life support: oxygen, water recovery, carbon dioxide removal and waste, watched on the smart systems dashboard.\n\n");
        sb.Append("Power: solar arrays with a backup supply and a battery bank.\n\n");
        sb.Append("Site: lunar south pole (for example near Shackleton crater), close to water ice.\n\n");
        sb.Append("Autonomy: the AI keeps the systems running and sends service robots without waiting for Earth. The design is meant to look after itself between crews for 6 to 24 months.\n\n");
        sb.Append("Lunar constraints handled: extreme temperature, radiation, dust, limited resupply.");
        return sb.ToString();
    }
    const string HunchSensors = "WHAT THE AI WATCHES\n\nHeart rate and its variability (wearable): anxiety, stress, fatigue\nSkin response (wearable): sudden anxiety\nSleep tracker: depression, fatigue, body clock\nVoice tone (microphones in shared rooms): mood, conflict, fatigue\nFace expression (cameras in shared rooms only): emotion, low mood, conflict\nMovement and who is near whom: isolation, conflict, loss of choice\nAir and carbon dioxide: tired thinking, irritability\n\nThe project sheet lists these signals: voice, face, group interaction, laughter and jokes, tears, isolation. Laughter and group talk in the Galley count as healthy signs; long silence, eating alone and staying in a pod count toward isolation.\n\nSIX CONDITIONS THE AI LOOKS FOR\nAnxiety, depression, conflict between crew members, tired thinking, loss of personal choice, and boredom from unchanging surroundings.\n\nIF A SENSOR FAILS\nNo single sensor decides anything. The AI needs two or more signals to agree. If one fails it says so on the dashboard, relies on the others, and widens its uncertainty instead of guessing.\n\nIN THIS GAME\nThe wearable numbers are simulated. The crew radio measures only how loud you speak. No real face or voice analysis is done.";
    const string HunchProtocol = "HOW THE AI RESPONDS\n\nMILD or early: quiet changes in the background. Warmer, dimmer light; calm sound; a little warmer air; a scent. No alarm.\n\nMODERATE: the crew member is told, and offered choices. Nothing is forced.\n\nSEVERE: straight to the flight surgeon, and a crew check-in.\n\nAfter each response the AI watches for about 20 minutes. Better: it restores the room slowly and logs it. Not better: it moves up one level.\n\nWHAT THE AI CAN CHANGE\nLight colour and brightness through a 24-hour day, sound, temperature and air flow, scent, the schedule, social prompts (shared meals, calls home, team meetings), and views of Earth and nature.\n\nCONSENT\nIf a crew member refuses an intervention, the AI stops and writes it in the log. Each crew member can switch monitoring off on the Privacy and consent page.\n\nHARD LIMITS: THE AI NEVER\n- puts a camera or microphone inside a private pod\n- overrides a refusal by itself\n- shares one person's private state with the rest of the crew\n- locks a door, changes a medicine, or changes the mission plan without a human\n- hides from the crew that it is active\n\nOnly the flight surgeon may override privacy, and only for a safety emergency.";
    string HunchEclss()
    {
        var sb = new StringBuilder(); float co2 = 0f; if (hs != null) { foreach (var h in hs) co2 += h.co2; co2 /= Mathf.Max(1, hs.Length); }
        sb.Append("SMART SYSTEMS DASHBOARD      day ").Append(sim.Day).Append("   ").Append(Mathf.FloorToInt(sim.Hour).ToString("00")).Append(":00\n\n");
        sb.Append("LIFE SUPPORT (ECLSS)\n  Oxygen generation     ").Append(Mathf.RoundToInt(sim.oxygen)).Append("%   ").Append(sim.oxygen > 60f ? "normal" : (sim.oxygen > 30f ? "LOW" : "CRITICAL")).Append('\n');
        sb.Append("  Water recovery        ").Append(Mathf.RoundToInt(sim.water)).Append("%   ").Append(sim.water > 60f ? "normal" : (sim.water > 30f ? "LOW" : "CRITICAL")).Append('\n');
        sb.Append("  Carbon dioxide (cabin) ").Append(co2.ToString("F1")).Append(" mmHg   ").Append(co2 < 4f ? "normal" : "HIGH").Append('\n');
        sb.Append("  Food stores           ").Append(Mathf.RoundToInt(sim.food)).Append("%\n  Ventilation fans      ").Append(ventOn ? "running" : "OFF").Append("\n\n");
        sb.Append("ENVIRONMENT\n  Lights                ").Append(lightsOn ? "on, following the 24-hour day" : "OFF").Append("\n  Dust inside           ").Append(Mathf.RoundToInt(sim.dust)).Append("%\n  Radiation dose        ").Append(sim.dose.ToString("F1")).Append(" mSv\n  Solar storm           ").Append(sim.stormLeft > 0f ? "IN PROGRESS: shelter" : "none").Append("\n\n");
        sb.Append("POWER\n  Battery               ").Append(Mathf.RoundToInt(sim.battery)).Append("%\n  Solar arrays          ").Append(Mathf.RoundToInt(sim.arrays)).Append("%\n  Battery Bank modules  ").Append(batBank).Append(" of ").Append(BatSlots).Append("\n\n");
        sb.Append("ALERTS  ").Append(sim.alerts.Count == 0 ? "none" : sim.alerts.Count + " open").Append("\n\nSwitch lights and fans on the console in each zone. ARIA sends a service robot by itself when a system needs routine care.");
        return sb.ToString();
    }
    string HunchReport()
    {
        var sb = new StringBuilder(); int ok, warn, bad; HsCounts(out ok, out warn, out bad);
        sb.Append("CREW HEALTH REPORT      day ").Append(sim.Day).Append("   ").Append(Mathf.FloorToInt(sim.Hour).ToString("00")).Append(":00\n\nTEAM: ").Append(ok).Append(" nominal, ").Append(warn).Append(" caution, ").Append(bad).Append(" critical.   Interventions accepted so far: ").Append(sim.psychHelped).Append("\n\n");
        for (int i = 0; hs != null && i < hs.Length && i < sim.crew.Count; i++)
        {
            var h = hs[i]; var c = sim.crew[i]; int fz = ZoneIndex(c.favourite ?? "none");
            sb.Append(c.name).Append("  (").Append(c.role).Append(")\n");
            if (i < 4 && !hunchConsent[i]) { sb.Append("  Monitoring switched off by the crew member. No data shown.\n\n"); continue; }
            sb.Append("  Heart rate ").Append(Mathf.RoundToInt(h.hr)).Append("   oxygen ").Append(Mathf.RoundToInt(h.spo2)).Append("%   breathing ").Append(Mathf.RoundToInt(h.resp)).Append("   temperature ").Append(h.temp.ToString("F1")).Append(" C\n");
            sb.Append("  Sleep ").Append(Mathf.RoundToInt(c.sleep)).Append("%   mood ").Append(Mathf.RoundToInt(c.mood)).Append("%   stress ").Append(Mathf.RoundToInt(c.stress)).Append("%\n");
            sb.Append("  AI class: ").Append(h.cls).Append(h.cond >= 0 ? "   condition: " + HsCond[h.cond] + (h.inten < 0.5f ? " (mild)" : (h.inten < 0.7f ? " (moderate)" : " (severe)")) : "   no condition").Append('\n');
            sb.Append("  Own best countermeasure: ").Append(fz >= 0 ? game.zones[fz].title : "time in the Sanctuary").Append("\n\n");
        }
        sb.Append("Simulated data. Classes follow the project's three groups: Neutral, Underlying issue, Suspected illness.");
        return sb.ToString();
    }
    void HunchSaveReport()
    {
        try { string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Reports"); Directory.CreateDirectory(dir); hunchReportPath = Path.Combine(dir, "crew_health_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"); File.WriteAllText(hunchReportPath, HunchReport().Replace("\n", "\r\n")); Note("Report saved: " + hunchReportPath); }
        catch (Exception e) { Note("Could not save the report: " + e.Message); }
    }

    // ================================================================== realistic fittings for both modes: nothing overlaps, food is stored, a real bathroom
    /// a free place on the floor near a point: floor underneath, nothing already there, no wall in between. facing points back at the centre.
    bool FreeNear(Vector3 centre, float yaw, Vector3 half, float[] dists, out Vector3 pos, out Vector3 facing)
    {
        foreach (float dist in dists) for (int k = 0; k < 24; k++)
        {
            Vector3 d = Quaternion.Euler(0f, yaw + k * 15f, 0f) * Vector3.forward, c = centre + d * dist; RaycastHit fh;
            if (!Physics.Raycast(c + Vector3.up * 1.2f, Vector3.down, out fh, 2.2f, ~0, QueryTriggerInteraction.Ignore) || fh.normal.y < 0.8f) continue;
            if (Physics.CheckBox(new Vector3(c.x, fh.point.y + half.y + 0.12f, c.z), half, Quaternion.LookRotation(d, Vector3.up), ~0, QueryTriggerInteraction.Ignore)) continue;
            if (Mathf.Abs(fh.point.y - centre.y) > 1.2f || Physics.Linecast(new Vector3(centre.x, fh.point.y + 1.9f, centre.z), new Vector3(c.x, fh.point.y + 1.9f, c.z), ~0, QueryTriggerInteraction.Ignore)) continue;
            pos = new Vector3(c.x, fh.point.y, c.z); facing = -d; return true;
        }
        Vector3 f0 = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward; pos = centre + f0 * dists[0]; facing = -f0; return false;
    }
    /// makes a fitting solid, so you cannot walk through it and nothing else is placed inside it
    void Solid(Transform t, Vector3 centre, Vector3 size) { var b = t.gameObject.AddComponent<BoxCollider>(); b.center = centre; b.size = size; Physics.SyncTransforms(); }

    /// food is never loose: it sits on two pantry racks, in sealed cans and pouches. Returns where the eight packs go.
    Vector3[] PantrySlots(out int[] zoneOf)
    {
        var slots = new Vector3[8]; zoneOf = new int[8]; string[] rooms = { "galley", "storage" };
        var steel = Lit(new Color(0.72f, 0.74f, 0.79f)); var dark = Lit(new Color(0.16f, 0.17f, 0.2f));
        for (int r = 0; r < 2; r++)
        {
            int zi = ZoneIndex(rooms[r]); if (zi < 0) zi = ZoneIndex("galley"); if (zi < 0) zi = 0; var z = game.zones[zi]; Vector3 pos, face;
            bool ok = FreeNear(z.position, z.yaw + 150f + r * 60f, new Vector3(0.72f, 0.85f, 0.26f), new[] { 2.0f, 2.6f, 1.6f, 3.2f, 1.2f, 3.8f }, out pos, out face);
            if (!ok) foreach (string alt in new[] { "storage", "galley", "quarters", "work" }) { int ai = ZoneIndex(alt); if (ai < 0) continue; Vector3 p2, f2; if (FreeNear(game.zones[ai].position, game.zones[ai].yaw + 30f, new Vector3(0.72f, 0.85f, 0.26f), new[] { 2.0f, 2.6f, 1.6f, 3.2f, 1.2f, 3.8f }, out p2, out f2)) { ok = true; zi = ai; pos = p2; face = f2; hunchPlaced += "(" + rooms[r] + " rack moved to " + alt + ") "; break; } }
            var rack = new GameObject("Pantry rack").transform; rack.SetParent(root, false); rack.position = pos; rack.rotation = Quaternion.LookRotation(face, Vector3.up);
            foreach (float sx in new[] { -0.66f, 0.66f }) Prim(PrimitiveType.Cube, rack, new Vector3(sx, 0.8f, 0f), new Vector3(0.05f, 1.6f, 0.42f), steel);
            for (int sh = 0; sh < 3; sh++) { Prim(PrimitiveType.Cube, rack, new Vector3(0f, 0.25f + sh * 0.5f, 0f), new Vector3(1.32f, 0.04f, 0.42f), steel); if (sh < 2) Prim(PrimitiveType.Cube, rack, new Vector3(0f, 0.36f + sh * 0.5f, 0.2f), new Vector3(1.32f, 0.16f, 0.02f), dark); }      // a lip on each shelf so nothing drifts off
            Prim(PrimitiveType.Cube, rack, new Vector3(0f, 0.8f, -0.2f), new Vector3(1.32f, 1.6f, 0.02f), dark);
            var l = Label(rack, "<b>PANTRY</b>\n<size=60%>sealed cans and pouches only</size>", 0f, 0f, 0.9f, 2.4f, 0.6f, Color.white); l.transform.localPosition = new Vector3(0f, 1.9f, 0f); mapLabels.Add(l);
            Solid(rack, new Vector3(0f, 0.8f, 0f), new Vector3(1.4f, 1.6f, 0.46f)); hunchPlaced += "pantry " + rooms[r] + (ok ? " free spot; " : " default; ");
            for (int k = 0; k < 4; k++) { slots[r * 4 + k] = rack.TransformPoint(new Vector3(k % 2 == 0 ? -0.32f : 0.32f, (k < 2 ? 0.25f : 0.75f) + 0.18f, 0.02f)); zoneOf[r * 4 + k] = zi; }
        }
        return slots;
    }

    int hygSheet = -1;
    const string HygieneText = "BATHROOM AND HYGIENE ON THE MOON\n\nTOILET\nOn the space station there is no gravity to carry waste away, so the toilet uses moving air instead. The Moon has one sixth of Earth's gravity: things do fall, but slowly, so this toilet keeps the air flow, with a seat, foot bars and handholds to keep you in place.\n\nSolid waste is sealed in a bag inside a canister. Urine goes through a funnel and hose to the water recovery system.\n\nWATER\nNothing is poured away. Urine, sweat and the moisture in the air are cleaned and used again. The space station now recovers about 98 percent of its water. That is why the fish tank, the plants and the crew can share the same water loop.\n\nWASHING\nThere is no shower on the space station. Crews use wet towels, rinseless soap and no-rinse shampoo, and a sealed sink so drops cannot float off. On the Moon a small enclosed shower is possible because water falls, but every drop is still collected.\n\nWHY IT MATTERS FOR THE CREW\nA private, clean bathroom is one of the first things people miss in isolation. Good hygiene space lowers stress and stops arguments, so it is part of the mental-health design, not an extra.";
    /// a real space bathroom in the Hygiene zone: a suction toilet and a sealed wash station
    void BuildHygiene()
    {
        hygSheet = reads.Count; reads.Add(new ReadItem { title = "Bathroom and hygiene on the Moon", zone = "Hygiene", got = true, body = () => HygieneText });
        int zi = ZoneIndex("hygiene"); if (zi < 0) return; var z = game.zones[zi]; Vector3 pos, face;
        var white = Lit(new Color(0.92f, 0.93f, 0.95f)); var steel = Lit(new Color(0.72f, 0.74f, 0.79f)); var dark = Lit(new Color(0.14f, 0.15f, 0.18f)); var blue = Lit(new Color(0.2f, 0.45f, 0.8f)); var yellow = Lit(new Color(0.95f, 0.8f, 0.2f));
        // ---- the toilet
        bool ok = FreeNear(z.position, z.yaw + 180f, new Vector3(0.45f, 0.75f, 0.5f), new[] { 1.9f, 2.5f, 1.5f, 3.1f }, out pos, out face);
        var t = new GameObject("Space toilet").transform; t.SetParent(root, false); t.position = pos; t.rotation = Quaternion.LookRotation(face, Vector3.up);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.24f, 0f), new Vector3(0.52f, 0.48f, 0.6f), white); Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.5f, 0.04f), new Vector3(0.4f, 0.03f, 0.44f), white);
        Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.515f, 0.04f), new Vector3(0.2f, 0.02f, 0.22f), dark);                                                      // the small opening: air pulls waste in
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.75f, -0.26f), new Vector3(0.52f, 0.9f, 0.1f), white); Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.05f, -0.2f), new Vector3(0.3f, 0.12f, 0.03f), Glow(new Color(0.3f, 1.6f, 0.6f)));
        Prim(PrimitiveType.Cylinder, t, new Vector3(0.33f, 0.62f, -0.05f), new Vector3(0.035f, 0.22f, 0.035f), steel); Prim(PrimitiveType.Cylinder, t, new Vector3(0.33f, 0.86f, 0.02f), new Vector3(0.09f, 0.04f, 0.09f), yellow);   // urine hose and funnel
        foreach (float sx in new[] { -0.3f, 0.3f }) { Prim(PrimitiveType.Cylinder, t, new Vector3(sx, 0.75f, 0.2f), new Vector3(0.03f, 0.22f, 0.03f), blue); var bar = Prim(PrimitiveType.Cylinder, t, new Vector3(sx * 0.7f, 0.08f, 0.42f), new Vector3(0.03f, 0.1f, 0.03f), blue); bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); }
        var hit = FacButton(t, new Vector3(0f, 0.6f, 0.05f), new Vector3(0.7f, 1.2f, 0.75f), Clear(new Color(1f, 1f, 1f, 0.02f)), 1400);
        var tl = Label(t, "<b>TOILET</b>\n<size=60%>air flow, sealed waste, urine recycled</size>", 0f, 0f, 0.8f, 2.4f, 0.6f, Color.white); tl.transform.localPosition = new Vector3(0f, 1.55f, 0f); mapLabels.Add(tl);
        Physics.SyncTransforms(); hunchPlaced += ok ? "toilet free spot; " : "toilet default; ";
        // ---- the wash station
        ok = FreeNear(z.position, z.yaw + 60f, new Vector3(0.5f, 0.9f, 0.3f), new[] { 1.9f, 2.5f, 1.5f, 3.1f }, out pos, out face);
        var w = new GameObject("Wash station").transform; w.SetParent(root, false); w.position = pos; w.rotation = Quaternion.LookRotation(face, Vector3.up);
        Prim(PrimitiveType.Cube, w, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.45f), white); Prim(PrimitiveType.Cube, w, new Vector3(0f, 1.45f, -0.2f), new Vector3(0.9f, 1.1f, 0.05f), steel);
        Prim(PrimitiveType.Sphere, w, new Vector3(0f, 1.05f, 0.02f), new Vector3(0.6f, 0.42f, 0.42f), Clear(new Color(0.6f, 0.85f, 1f, 0.3f)));                               // the sealed sink dome with two hand ports
        foreach (float sx in new[] { -0.16f, 0.16f }) Prim(PrimitiveType.Cylinder, w, new Vector3(sx, 1.02f, 0.22f), new Vector3(0.13f, 0.01f, 0.13f), dark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Prim(PrimitiveType.Cube, w, new Vector3(0f, 1.62f, -0.17f), new Vector3(0.5f, 0.5f, 0.01f), Lit(new Color(0.75f, 0.85f, 0.92f)));                                    // mirror
        Prim(PrimitiveType.Cube, w, new Vector3(-0.33f, 1.38f, -0.14f), new Vector3(0.16f, 0.2f, 0.08f), blue); Prim(PrimitiveType.Cylinder, w, new Vector3(0.33f, 1.38f, -0.12f), new Vector3(0.07f, 0.1f, 0.07f), yellow);   // wipes, no-rinse soap
        FacButton(w, new Vector3(0f, 0.95f, 0.05f), new Vector3(0.95f, 1.9f, 0.55f), Clear(new Color(1f, 1f, 1f, 0.02f)), 1401);
        var wl = Label(w, "<b>WASH STATION</b>\n<size=60%>sealed sink, rinseless soap, every drop recovered</size>", 0f, 0f, 0.8f, 2.6f, 0.6f, Color.white); wl.transform.localPosition = new Vector3(0f, 2.25f, 0f); mapLabels.Add(wl);
        Physics.SyncTransforms(); hunchPlaced += ok ? "wash free spot; " : "wash default; ";
    }
    string RealText(int i) { return i >= 1900 && i < 1906 ? HealthText(i) : i >= 1800 && i < 1810 ? KioskText(i) : i >= 1600 && i < 1800 ? FunText(i) : i >= 1500 && i < 1504 ? SleepText(i) : i == 1400 ? "space toilet: how it works" : (i == 1401 ? "wash station: how it works" : ""); }
    void RealUse(int i) { if (i >= 1900 && i < 1906) HealthUse(i); else if (i >= 1800 && i < 1810) KioskUse(i); else if (i >= 1600 && i < 1800) FunUse(i); else if (i >= 1500 && i < 1504) SleepUse(i); else if ((i == 1400 || i == 1401) && hygSheet >= 0) OpenRead(hygSheet); }

    public string ProbeFitting() { string[] names = { "Space toilet", "Wash station", "Pantry rack", "Aquaponics fish tank" }; string nm = names[Mathf.Clamp(probeInt, 0, names.Length - 1)]; Transform t = null; foreach (var tr in root.GetComponentsInChildren<Transform>(true)) if (tr.name == nm) { t = tr; break; } if (!t) return nm + " not found"; game.flyMode = false; TeleportTo(t.position + t.forward * 1.9f, 0f); Face(t.position + Vector3.up * 0.9f); return nm + " at " + t.position.ToString("F1") + " | " + hunchPlaced; }
    int HunchPagesShown() { if (!menu) return -1; int n = 0; foreach (var p in menu.extraPages) if (menu.extraShow == null || menu.extraShow(p)) n++; return n; }
    /// test hook: every page of the pause menu, and whether it is listed now
    public string ProbeMenuPages() { if (!menu) return "no menu"; var sb = new StringBuilder(); foreach (var p in menu.extraPages) sb.Append(menu.extraShow == null || menu.extraShow(p) ? "[shown] " : "[hidden] ").Append(p.label).Append(" ; "); return menu.extraPages.Count + " pages: " + sb; }
    /// test hook: how much of the world is switched on, to compare the full game before and after NASA HUNCH mode
    public string ProbeWorldCount()
    {
        int rOn = 0, cOn = 0, kids = 0, kidsOff = 0; var model = GameObject.Find("HUNCH_Lunar_Base"); string off = "";
        if (model) { foreach (Transform ch in model.transform) { kids++; if (!ch.gameObject.activeSelf) { kidsOff++; if (off.Length < 300) off += ch.name + ","; } } foreach (var r in model.GetComponentsInChildren<Renderer>()) if (r.enabled) rOn++; foreach (var c in model.GetComponentsInChildren<Collider>()) if (c.enabled) cOn++; }
        int mine = 0, mineOff = 0; string off2 = ""; foreach (Transform ch in root) { mine++; if (!ch.gameObject.activeSelf) { mineOff++; if (off2.Length < 400) off2 += ch.name + ","; } }
        return "model children " + kids + " off " + kidsOff + " [" + off + "] renderers on " + rOn + " colliders on " + cOn + " | game objects " + mine + " off " + mineOff + " [" + off2 + "] rocket " + (fx ? fx.RocketWhere : "?") + " aliens " + aliensOn + " third " + thirdPerson;
    }
    public string ProbeHunchOn() { HunchSet(true); return ProbeHunch(); }
    public string ProbeHunchOff() { HunchSet(false); return ProbeHunch(); }
    public string ProbeHunch() { int done = 0; foreach (var it in hunchItems) if (it.done) done++; return "hunch " + hunchOn + " hidden " + hunchHidden.Count + " items " + done + "/" + hunchItems.Count + " menu pages " + HunchPagesShown() + " aliens " + aliensOn + " tank " + hunchTankAt.ToString("F0") + " ground " + hunchGround.ToString("F1") + " cover " + hunchCover.ToString("F0") + " placed: " + hunchPlaced + " zones " + game.zones.Count; }
    public string ProbeHunchDocs() { HunchSaveReport(); hunchFlags |= HFDash | HFCrew | HFFacts | HFSensors | HFProtocol | HFEclss | HFConsent | HFReport; HunchSheet(probeInt, 0); return "sheet " + probeInt + " report " + hunchReportPath; }
    public string ProbeHunchView() { float y = GroundAt(probeX, probeZ, hunchGround) + probeY; game.flyMode = probeY > 1f; TeleportTo(new Vector3(probeX, y + 0.1f, probeZ), probeYaw); if (desk) desk.SetLook(probeYaw, probeInt); return "at " + probeX + "," + y.ToString("F1") + "," + probeZ; }
    public string ProbeHunchTank() { probeInt = 3; return ProbeFitting(); }
    public string ProbeHunchZones() { var sb = new StringBuilder(); for (int i = 0; i < game.zones.Count; i++) sb.Append(game.zones[i].id).Append('=').Append(game.zones[i].title).Append(game.zones[i].position.ToString("F0")).Append(" | "); return sb.ToString(); }
    public string ProbeHunchList() { var sb = new StringBuilder(); foreach (var it in hunchItems) sb.Append(it.done ? "[x] " : "[ ] ").Append(it.text).Append(" ; "); return sb.ToString(); }
}
