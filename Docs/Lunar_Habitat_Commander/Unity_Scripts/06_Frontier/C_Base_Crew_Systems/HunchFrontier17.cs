// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/C_Base_Crew_Systems/HunchFrontier17.cs
//  Section : 06_Frontier/C_Base_Crew_Systems
//  Folder  : Systems inside the central base: pick-ups + battery bank, NASA HUNCH mode, physics
//            view, Autopilot Smooth, crew of four + sleep pods, The Show, self-working crew,
//            airlocks/lifts, crew self-care.
//  Purpose : Seventeenth part of Moon Frontier: - a crew of four that counts you: you are the
//            Commander, with three crewmates (never five people)
//  Why     : Chat: '4 crew members max INCLUDING himself' + sleep pods.
//  Contains: HunchFrontier
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// Seventeenth part of Moon Frontier:
///   - a crew of four that counts you: you are the Commander, with three crewmates (never five people)
///   - sleep: four sleep pods in Crew Quarters; the crew lie down in them at night or when you switch Sleep mode on,
///     and you can sleep in your own pod (the clock runs faster until morning)
///   - NASA HUNCH mode extras: a Teleport setting, the helper rovers, and a realistic-scale view of the base
public partial class HunchFrontier
{
    // ================================================================== four people, counting you
    void BuildCrewFour()
    {
        for (int k = 0; k < 4 && k < sim.crew.Count; k++) { crewName[k] = k == 0 ? "Commander" : sim.crew[k].name; crewRole[k] = k == 0 ? "Commander" : sim.crew[k].role; if (game.crewAvatars != null && k < game.crewAvatars.Length) crewBody[k] = game.crewAvatars[k]; }
        if (sim.crew.Count > 0) { sim.crew[0].name = "Commander (you)"; sim.crew[0].role = "Commander: this is you"; }
        if (game.crewAvatars != null && game.crewAvatars.Length > 0 && game.crewAvatars[0]) { game.crewAvatars[0].gameObject.SetActive(false); game.crewAvatars[0] = null; }      // you are the fourth crew member, so there is no fifth body walking about
    }

    // ================================================================== play as any of the four
    // F6 (or Menu > Base systems > Play as) steps you into the next crew member. The one you leave gets their body back and
    // carries on with their own day by themselves, as the other two already do.
    readonly string[] crewName = new string[4], crewRole = new string[4]; readonly Transform[] crewBody = new Transform[4];
    [NonSerialized] public int playAs;
    public string PlayAsName { get { return crewName[Mathf.Clamp(playAs, 0, 3)] ?? "Commander"; } }
    public void SwitchCrew()
    {
        int n = Mathf.Min(4, sim.crew.Count); if (n < 2 || game.crewAvatars == null) return;
        if (!Playing || game.menuOpen) return;
        if (autoOn) { Note("Stop the autopilot first (Space), then switch."); return; }
        if (sleepMe) { Note("Wake up first, then switch."); return; }
        int old = playAs, k = (playAs + 1) % n;
        for (int tries = 0; tries < n && (k >= game.crewAvatars.Length || !crewBody[k]); tries++) k = (k + 1) % n;      // only into someone who has a body
        if (k == old || !crewBody[k]) { Note("Nobody else to switch to right now."); return; }
        Vector3 to = crewBody[k].position; float yaw = crewBody[k].eulerAngles.y;
        // the one you leave stands where you stood, then walks back into their own routine
        if (crewBody[old]) { crewBody[old].position = Feet(); crewBody[old].rotation = Quaternion.Euler(0f, cam ? cam.eulerAngles.y : 0f, 0f); crewBody[old].gameObject.SetActive(true); if (old < game.crewAvatars.Length) game.crewAvatars[old] = crewBody[old]; }
        if (old >= 0 && old < 4) { var ca = crewAi[old]; ca.pos = Feet(); ca.has = true; ca.zone = -1; ca.path.Clear(); ca.job = -1; ca.wait = 0f; }      // the one you leave starts from exactly where you were standing
        crewBody[k].gameObject.SetActive(false); game.crewAvatars[k] = null;
        sim.crew[old].name = crewName[old]; sim.crew[old].role = crewRole[old];
        sim.crew[k].name = crewName[k] + " (you)"; sim.crew[k].role = crewRole[k] + ": this is you";
        playAs = k; game.flyMode = false; TeleportTo(to + Vector3.up * 0.05f, yaw);
        Note("You are now " + crewName[k] + ". " + crewName[old] + " carries on by autopilot. F6 switches again.");
        sim.Say("ARIA: Hello " + crewName[k] + ". " + crewName[old] + " is back on their own schedule.");
        Play(cPop, 0.5f); game.Buzz(0.5f, 0.1f);
    }
    public string ProbeSwitch() { SwitchCrew(); var sb = new System.Text.StringBuilder("playAs " + playAs + " " + PlayAsName + " | "); for (int k = 0; k < 4 && k < sim.crew.Count; k++) sb.Append(sim.crew[k].name).Append(game.crewAvatars != null && k < game.crewAvatars.Length && game.crewAvatars[k] ? " body " : " (no body) "); return sb + "feet " + Feet().ToString("F0"); }

    // ================================================================== cheats and anti-cheat
    HunchMenu.ExtraPage pageCheat;
    string CheatLabel() { HunchCheats.Hunch = hunchOn; HunchCheats.Creative = Creative; return "Cheats and anti-cheat: " + (HunchCheats.Anti ? "<color=#7CFC9A>fair play</color>" : HunchCheats.OnCount + " on"); }
    void CheatMenu(HunchMenu m)
    {
        HunchCheats.Hunch = hunchOn; HunchCheats.Creative = Creative; bool a = HunchCheats.Anti;
        string where = "<color=#9AD8FF>These switches are for " + HunchCheats.ModeName + ". Creative starts with cheats allowed, Survival starts with every cheat off, and NASA HUNCH mode asks you.</color>\n\n";
        m.AddItem("Anti-cheat (fair play): " + (a ? "<color=#7CFC9A>ON</color>" : "off"), "ON switches every cheat below off at once and locks them, so the mission is played by the rules: solid walls, no flying, no jumping to zones, no skipping time. OFF lets you choose each one yourself.", () => { HunchCheats.Anti = !HunchCheats.Anti; if (HunchCheats.Anti) game.flyMode = false; CheatDone(m); });
        CheatRow(m, a, "Walk through walls", HunchCheats.Get(HunchCheats.WallsI), where + "Push against a wall for a moment and you are let through it. Off: walls, doors and furniture are solid for you and for the autopilots; where the model has no doorway the autopilots make a short transfer.", () => HunchCheats.Flip(HunchCheats.WallsI));
        CheatRow(m, a, "Fly (F key or the jetpack)", HunchCheats.Get(HunchCheats.FlyI), where + "Float anywhere, through the air and over the base. Off: you stay on your feet.", () => { HunchCheats.Flip(HunchCheats.FlyI); if (!HunchCheats.FlyOk) game.flyMode = false; });
        CheatRow(m, a, "Skip time (free explore)", HunchCheats.Get(HunchCheats.SkipI), where + "The two main-menu lines that jump to the lunar sunset and the lunar sunrise in free explore. Off: time only passes by itself.", () => HunchCheats.Flip(HunchCheats.SkipI));
        CheatRow(m, a, "Teleport to zones (Tab, Q, G, number keys, Travel)", HunchCheats.Get(HunchCheats.TeleI), where + "Go straight to a zone without walking. Tab and Q for the next and previous zone, G for where you are needed, the number keys, the Travel page, and X in the headset. Off: you walk, and the green arrows show the way. The buildings of the base are not all joined by doorways, so if a wall stops you, switch this on.", () => HunchCheats.Flip(HunchCheats.TeleI));
    }
    // NASA HUNCH mode asks about cheats the moment it is switched on
    HunchMenu.ExtraPage pageHunchAsk;
    void HunchAskCheats()
    {
        if (!menu) return;
        if (pageHunchAsk == null) pageHunchAsk = new HunchMenu.ExtraPage { title = "NASA HUNCH MODE:  CHEATS?", build = m =>
        {
            m.AddItem("No cheats  (fair play)", "Solid walls, no flying, no skipping time, no teleporting to zones. This is the honest way to show the habitat to the judges.", () => { HunchCheats.Hunch = true; HunchCheats.SetAll(2, false); game.flyMode = false; if (pageCheat != null) pageCheat.label = CheatLabel(); m.Close(); Note("NASA HUNCH mode: cheats off."); });
            m.AddItem("Yes, allow cheats", "Walking through walls, flying, skipping time and teleporting to zones are all switched on for NASA HUNCH mode.", () => { HunchCheats.Hunch = true; HunchCheats.SetAll(2, true); if (pageCheat != null) pageCheat.label = CheatLabel(); m.Close(); Note("NASA HUNCH mode: cheats on."); });
            m.AddItem("Let me choose each one", "Opens the cheats page, where each cheat has its own switch.", () => { HunchCheats.Hunch = true; if (pageCheat != null) m.OpenExtra(pageCheat); });
        } };
        HunchCheats.Hunch = true; menu.OpenExtra(pageHunchAsk);
    }
    void CheatRow(HunchMenu m, bool locked, string name, bool on, string hint, Action flip)
    {
        if (locked) m.AddItem(name + ": <color=#9AA6BA>locked</color>", hint + "\n\n<color=#FFE08A>Anti-cheat is on. Switch it off (first line) to change this.</color>", () => Note("Anti-cheat is on. Switch it off first."));
        else m.AddItem(name + ": " + (on ? "<color=#FFB060>ON</color>" : "off"), hint, () => { flip(); HunchCheats.Save(); CheatDone(m); });
    }
    void CheatDone(HunchMenu m) { if (pageCheat != null) pageCheat.label = CheatLabel(); m.Rebuild(); }

    // ================================================================== sleep
    [NonSerialized] public bool sleepOn; bool sleepMe; float sleepStart, sleepMeStart, sleepHours; int sleepAlerts; Vector3 sleepAt;
    readonly Transform[] sleepBed = new Transform[4]; readonly TextMeshPro[] sleepZ = new TextMeshPro[4]; string sleepPlaced = ""; int sleepZone = -1;
    // Adaptive Recovery Pod layer on the SAME four bunks (no extra beds): a state light strip the AI drives
    // (green healthy · yellow fatigue → circadian reset · blue stress → calm protocol), sleep analytics (H) and a recovery session (G).
    readonly GameObject[,] podLight = new GameObject[4, 3]; readonly int[] podState = new int[4]; float podRecoverUntil;
    static readonly string[] PodStateName = { "<color=#5BFF8A>HEALTHY</color>", "<color=#FFD23F>FATIGUE · circadian reset</color>", "<color=#6FA0FF>STRESS · calm protocol</color>" };
    void PodLights(Transform bed, int k, Vector3 at)
    {
        Color[] c = { new Color(0.35f, 2.2f, 0.8f), new Color(2.2f, 1.7f, 0.3f), new Color(0.5f, 0.9f, 2.4f) };
        for (int s = 0; s < 3; s++) { var g = Prim(PrimitiveType.Cube, bed, at, new Vector3(0.9f, 0.035f, 0.035f), Glow(c[s])); g.name = "Recovery state light " + s; g.SetActive(s == 0); podLight[k, s] = g; }
    }
    /// debug (HunchBridge: invoke HunchFrontier.DumpSleepPods) → HunchBridge/sleep_probe.txt: where the 4 sleep pods ended up
    public static void DumpSleepPods()
    {
        var f = FindFirstObjectByType<HunchFrontier>(); var sb = new StringBuilder();
        if (!f) sb.Append("no HunchFrontier (not playing?)");
        else { sb.AppendLine("placed: " + f.sleepPlaced); for (int k = 0; k < 4; k++) sb.AppendLine(k + ": " + (f.sleepBed[k] ? f.sleepBed[k].name + " @ " + f.sleepBed[k].position.ToString("F2") + " fwd " + f.sleepBed[k].forward.ToString("F2") : "none") + "  lamp " + (f.podLamp[k] ? f.podLamp[k].color.ToString() + " I=" + f.podLamp[k].intensity.ToString("F2") : "-")); }
        System.IO.File.WriteAllText(System.IO.Path.Combine("HunchBridge", "sleep_probe.txt"), sb.ToString());
    }
    static int viewPod = -1;
    /// debug: stand 2.2 m from the next sleep pod and look at it (for a screenshot)
    public static void ViewNextPod()
    {
        var f = FindFirstObjectByType<HunchFrontier>(); if (!f) return; viewPod = (viewPod + 1) % 4; var b = f.sleepBed[viewPod]; if (!b) return;
        f.game.flyMode = false; Vector3 side = b.right; Vector3 at = b.position + side * 2.2f + Vector3.up * 0.05f;
        f.TeleportTo(at, Quaternion.LookRotation(-side).eulerAngles.y); f.Face(b.position + Vector3.up * 0.7f);
    }
    struct DormBed { public Vector3 c, axis; public float top, len, w; }
    /// scans the dorm for bed surfaces: a 0.2 m grid of downward rays; flat cells 0.3–0.85 m (or 1.3–2.0 m: upper bunk)
    /// above the floor are grouped, and groups that are bed-sized (1.6–2.6 m long, 0.6–1.4 m wide) are beds.
    List<DormBed> FindDormBeds(Vector3 centre)
    {
        var found = new List<DormBed>(); Physics.SyncTransforms();
        foreach (float R in new[] { 9f, 14f, 20f })
        {
            const float G = 0.2f; int n = Mathf.CeilToInt(R / G); var layer = new Dictionary<long, Vector3>[2] { new Dictionary<long, Vector3>(), new Dictionary<long, Vector3>() };
            for (int ix = -n; ix <= n; ix++) for (int iz = -n; iz <= n; iz++)
            {
                Vector3 o = new Vector3(centre.x + ix * G, centre.y + 3.0f, centre.z + iz * G); if ((o - new Vector3(centre.x, o.y, centre.z)).sqrMagnitude > R * R) continue;
                var hits = Physics.RaycastAll(o, Vector3.down, 4.5f, ~0, QueryTriggerInteraction.Ignore); if (hits.Length == 0) continue;
                float floor = float.MaxValue; foreach (var h in hits) if (h.normal.y > 0.9f && Mathf.Abs(h.point.y - centre.y) < 0.7f) floor = Mathf.Min(floor, h.point.y); if (floor == float.MaxValue) continue;
                foreach (var h in hits)
                {
                    if (h.normal.y < 0.85f) continue; float up = h.point.y - floor;
                    int L = up > 0.3f && up < 0.85f ? 0 : up > 1.3f && up < 2.0f ? 1 : -1; if (L < 0) continue;
                    long key = (long)(ix + 10000) * 100000 + (iz + 10000); if (!layer[L].ContainsKey(key) || layer[L][key].y < h.point.y) layer[L][key] = h.point;
                }
            }
            for (int L = 0; L < 2; L++)
            {
                var left = new HashSet<long>(layer[L].Keys);
                while (left.Count > 0)
                {
                    long s0 = left.First(); left.Remove(s0); var comp = new List<Vector3>(); var q = new Queue<long>(); q.Enqueue(s0);
                    while (q.Count > 0) { long k0 = q.Dequeue(); var p0 = layer[L][k0]; comp.Add(p0); foreach (long nb in new[] { k0 + 1, k0 - 1, k0 + 100000, k0 - 100000 }) if (left.Contains(nb) && Mathf.Abs(layer[L][nb].y - p0.y) < 0.06f) { left.Remove(nb); q.Enqueue(nb); } }
                    if (comp.Count < 25 || comp.Count > 110) continue;                       // 1–4.4 m² only
                    Vector3 m = Vector3.zero; foreach (var p in comp) m += p; m /= comp.Count;
                    float sxx = 0, szz = 0, sxz = 0; foreach (var p in comp) { float dx = p.x - m.x, dz = p.z - m.z; sxx += dx * dx; szz += dz * dz; sxz += dx * dz; }
                    float ang = 0.5f * Mathf.Atan2(2f * sxz, sxx - szz); Vector3 ax = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)), side = new Vector3(-ax.z, 0f, ax.x);
                    float a0 = 1e9f, a1 = -1e9f, b0 = 1e9f, b1 = -1e9f; foreach (var p in comp) { float u = Vector3.Dot(p - m, ax), v = Vector3.Dot(p - m, side); a0 = Mathf.Min(a0, u); a1 = Mathf.Max(a1, u); b0 = Mathf.Min(b0, v); b1 = Mathf.Max(b1, v); }
                    float len = a1 - a0 + G, wid = b1 - b0 + G;
                    if (len < 1.6f || len > 2.6f || wid < 0.6f || wid > 1.4f) continue;
                    var c = m + ax * (a0 + a1) * 0.5f + side * (b0 + b1) * 0.5f;
                    if (found.Any(f => (new Vector3(f.c.x, 0, f.c.z) - new Vector3(c.x, 0, c.z)).sqrMagnitude < 0.5f && Mathf.Abs(f.top - m.y) < 0.2f)) continue;
                    found.Add(new DormBed { c = c, axis = ax, top = m.y, len = len, w = wid });
                }
            }
            if (found.Count >= 4) break;
        }
        return found.OrderBy(b => (b.c - centre).sqrMagnitude).Take(4).ToList();
    }

    // Automatic sleep lighting (same rules as the realistic sim): the AI dims each pod to a deep-amber night light when its
    // person falls asleep (no blue light → melatonin), runs a 45-minute sunrise before 06:00, and goes back to neutral white
    // when they get up. Under-bed path LEDs stay on at 5 % so nobody trips at night. Red pulse = habitat alarm.
    readonly Light[] podLamp = new Light[4]; readonly GameObject[] podUnder = new GameObject[4];
    void SleepLeds(Transform bed, int k, float w, float len)
    {
        var lg = new GameObject("Pod sleep light " + (k + 1)); lg.transform.SetParent(bed, false); lg.transform.localPosition = new Vector3(0f, 1.3f, -len * 0.3f);
        var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 2.2f; l.intensity = 0.6f; l.shadows = LightShadows.None; l.color = new Color(1f, 0.92f, 0.8f); podLamp[k] = l;
        podUnder[k] = Prim(PrimitiveType.Cube, bed, new Vector3(0f, 0.04f, 0f), new Vector3(w * 0.9f, 0.015f, len * 0.9f), Glow(new Color(0.9f, 0.45f, 0.12f)));
    }
    void UpdateSleepLeds()
    {
        float h = sim.Hour; bool alarm = sim.stormActive || sim.leak;
        for (int k = 0; k < 4; k++)
        {
            var l = podLamp[k]; if (!l) continue; bool asleep = k == 0 ? sleepMe : CrewAsleep(k);
            Color c; float I;
            if (alarm) { c = new Color(1f, 0.1f, 0.08f); I = 0.4f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 3f)); }
            else if (asleep && h >= 5.25f && h < 6f) { float dawn = (h - 5.25f) / 0.75f; c = Color.Lerp(new Color(1f, 0.32f, 0.1f), new Color(1f, 0.85f, 0.65f), dawn); I = Mathf.Lerp(0.05f, 0.9f, dawn); }   // sunrise
            else if (asleep) { c = new Color(1f, 0.32f, 0.1f); I = 0.05f; }                                                     // deep-amber night light
            else if (h >= 20f || h < 6f) { c = new Color(1f, 0.7f, 0.45f); I = 0.35f; }                                         // evening: warm, dim
            else { c = new Color(1f, 0.95f, 0.88f); I = 0.6f; }
            l.color = Color.Lerp(l.color, c, Time.deltaTime * 2f); l.intensity = Mathf.Lerp(l.intensity, I, Time.deltaTime * 2f);
            if (podUnder[k]) podUnder[k].SetActive(asleep || h >= 20f || h < 6f || alarm);
        }
    }
    int PodStateOf(int k) { if (k >= sim.crew.Count) return 0; var c = sim.crew[k]; return c.stress > 55f ? 2 : (c.sleep < 55f ? 1 : 0); }
    static readonly Color[] SleepColour = { new Color(0.85f, 0.7f, 0.25f), new Color(0.25f, 0.55f, 0.85f), new Color(0.3f, 0.7f, 0.4f), new Color(0.75f, 0.35f, 0.35f) };

    void BuildSleep()
    {
        sleepZone = ZoneIndex("quarters"); if (sleepZone < 0) return; var z = game.zones[sleepZone];
        // The base ALREADY has four beds in the centre dorm: use those (no extra beds). They are part of the baked
        // dorm mesh, so they are found by shape: flat 2 m x 1 m surfaces at bed height.
        var dorm = FindDormBeds(z.position);
        if (dorm.Count > 0)
        {
            for (int k = 0; k < 4; k++)
            {
                var d = dorm[k % dorm.Count];
                Transform bed = new GameObject("Sleep pod " + (k + 1) + " (dorm bed)").transform; bed.SetParent(root, false);
                bed.position = new Vector3(d.c.x, d.top - 0.45f, d.c.z) + Vector3.up * (k >= dorm.Count ? 0.02f * k : 0f);
                bed.rotation = Quaternion.LookRotation(d.axis, Vector3.up);
                FacButton(bed, new Vector3(0f, 0.5f, 0f), new Vector3(d.w + 0.05f, 0.9f, d.len + 0.05f), Clear(new Color(1f, 1f, 1f, 0.02f)), 1500 + k);
                var l = Label(bed, "", 0f, 0f, 0.5f, 2.0f, 0.4f, Color.white); l.transform.localPosition = new Vector3(0f, 1.25f, -d.len * 0.45f); mapLabels.Add(l); sleepZ[k] = l;
                PodLights(bed, k, new Vector3(0f, 0.38f, -d.len * 0.5f - 0.01f));
                SleepLeds(bed, k, d.w, d.len);
                sleepBed[k] = bed;
            }
            sleepPlaced = "dorm beds: " + dorm.Count + " found (" + string.Join(" ", dorm.Select(b => b.c.ToString("F1"))) + ")";
            game.crewPose = SleepPose; return;
        }
        var frame = Lit(new Color(0.2f, 0.22f, 0.26f)); var white = Lit(new Color(0.9f, 0.91f, 0.93f)); var steel = Lit(new Color(0.72f, 0.74f, 0.79f));
        // one bedroom for the four of you: a small room with its own door and two double bunks. Nothing is placed where something already stands.
        Vector3 rp = Vector3.zero, rf = Vector3.forward; bool room = false; float W = 3.8f, D = 3.2f; Physics.SyncTransforms();
        // first a full-size room you can see into from Crew Quarters, then a smaller one, then the same two anywhere on this deck with free standing room at the door
        for (int pass = 0; pass < 4 && !room; pass++)
        foreach (float dist in new[] { 4.4f, 5.2f, 3.8f, 6.0f, 6.8f, 7.6f, 8.6f, 9.8f, 11f, 12.5f, 14f, 16f })
        {
            W = pass % 2 == 0 ? 3.8f : 3.1f; D = pass % 2 == 0 ? 3.2f : 2.9f;
            for (int a = 0; a < 24 && !room; a++)
            {
                Vector3 d = Quaternion.Euler(0f, z.yaw + 180f + a * 15f, 0f) * Vector3.forward, c = z.position + d * dist, side = Vector3.Cross(Vector3.up, d); RaycastHit fh;
                if (!Physics.Raycast(c + Vector3.up * 1.2f, Vector3.down, out fh, 2.2f, ~0, QueryTriggerInteraction.Ignore) || fh.normal.y < 0.8f || Mathf.Abs(fh.point.y - z.position.y) > 0.6f) continue;
                bool flat = true; foreach (float sx in new[] { -W * 0.5f + 0.1f, W * 0.5f - 0.1f }) foreach (float sz in new[] { -D * 0.5f + 0.1f, D * 0.5f - 0.1f }) { RaycastHit ch; Vector3 q = c + side * sx + d * sz; if (!Physics.Raycast(q + Vector3.up * 1.2f, Vector3.down, out ch, 2.2f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(ch.point.y - fh.point.y) > 0.25f) flat = false; }
                if (!flat) continue;
                if (Physics.CheckBox(new Vector3(c.x, fh.point.y + 1.3f, c.z), new Vector3(W * 0.5f + 0.1f, 1.15f, D * 0.5f + 0.1f), Quaternion.LookRotation(d, Vector3.up), ~0, QueryTriggerInteraction.Ignore)) continue;
                if (pass < 2) { if (Physics.Linecast(new Vector3(z.position.x, fh.point.y + 1.9f, z.position.z), new Vector3(c.x, fh.point.y + 1.9f, c.z) - d * (D * 0.5f + 0.3f), ~0, QueryTriggerInteraction.Ignore)) continue; }
                else { Vector3 door = new Vector3(c.x, fh.point.y, c.z) - d * (D * 0.5f + 0.7f); RaycastHit dh; if (Physics.CheckSphere(door + Vector3.up * 1.1f, 0.45f, ~0, QueryTriggerInteraction.Ignore) || !Physics.Raycast(door + Vector3.up * 1.0f, Vector3.down, out dh, 1.5f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(dh.point.y - fh.point.y) > 0.3f) continue; }
                rp = new Vector3(c.x, fh.point.y, c.z); rf = -d; room = true;
            }
            if (room) break;
        }
        if (room)
        {
            var rm = new GameObject("Crew bedroom").transform; rm.SetParent(root, false); rm.position = rp; rm.rotation = Quaternion.LookRotation(rf, Vector3.up);      // the door faces Crew Quarters
            var wall = Lit(new Color(0.86f, 0.87f, 0.9f));
            Prim(PrimitiveType.Cube, rm, new Vector3(0f, 1.15f, -D * 0.5f + 0.04f), new Vector3(W, 2.3f, 0.08f), wall, true);
            foreach (float sx in new[] { -W * 0.5f + 0.04f, W * 0.5f - 0.04f }) Prim(PrimitiveType.Cube, rm, new Vector3(sx, 1.15f, 0f), new Vector3(0.08f, 2.3f, D), wall, true);
            foreach (float sg in new[] { -1f, 1f }) Prim(PrimitiveType.Cube, rm, new Vector3(sg * (0.5f + (W - 1f) * 0.25f), 1.15f, D * 0.5f - 0.04f), new Vector3((W - 1f) * 0.5f, 2.3f, 0.08f), wall, true);      // the front wall, with the doorway in the middle
            Prim(PrimitiveType.Cube, rm, new Vector3(0f, 2.15f, D * 0.5f - 0.04f), new Vector3(1.0f, 0.3f, 0.08f), wall);
            Prim(PrimitiveType.Cube, rm, new Vector3(0f, 2.33f, 0f), new Vector3(W, 0.06f, D), wall);
            var sign = Label(rm, "<b>CREW BEDROOM</b>\n<size=60%>four sleep pods: yours and your three crewmates'</size>", 0f, 0f, 0.8f, 3.4f, 0.7f, Color.white); sign.transform.localPosition = new Vector3(0f, 2.75f, D * 0.5f + 0.06f); mapLabels.Add(sign);
            for (int k = 0; k < 4; k++)
            {
                Transform bed = new GameObject("Sleep pod " + (k + 1)).transform; bed.SetParent(rm, false); bool upper = k % 2 == 1; float bx = (k < 2 ? -1f : 1f) * (W * 0.5f - 0.6f);
                bed.localPosition = new Vector3(bx, upper ? 1.2f : 0f, -D * 0.5f + 1.18f); bed.localRotation = Quaternion.identity;
                if (upper) foreach (float px in new[] { -0.52f, 0.52f }) foreach (float pz in new[] { -1.02f, 1.02f }) Prim(PrimitiveType.Cube, bed, new Vector3(px, -0.4f, pz), new Vector3(0.06f, 1.6f, 0.06f), steel);
                Prim(PrimitiveType.Cube, bed, new Vector3(0f, 0.17f, 0f), new Vector3(1.0f, 0.34f, 2.1f), frame);
                Prim(PrimitiveType.Cube, bed, new Vector3(0f, 0.41f, 0f), new Vector3(0.9f, 0.14f, 2.0f), white);
                Prim(PrimitiveType.Cube, bed, new Vector3(0f, 0.52f, -0.78f), new Vector3(0.5f, 0.1f, 0.34f), white);
                Prim(PrimitiveType.Cube, bed, new Vector3(0f, 0.5f, 0.42f), new Vector3(0.92f, 0.06f, 1.1f), Lit(SleepColour[k]));
                Prim(PrimitiveType.Cube, bed, new Vector3(0.3f, 0.95f, -1.0f), new Vector3(0.12f, 0.06f, 0.03f), Glow(new Color(1.6f, 1.1f, 0.5f)));
                FacButton(bed, new Vector3(0f, 0.5f, 0f), new Vector3(1.05f, 0.9f, 2.15f), Clear(new Color(1f, 1f, 1f, 0.02f)), 1500 + k);
                if (!upper) Solid(bed, new Vector3(0f, 0.3f, 0f), new Vector3(1.0f, 0.6f, 2.1f));
                var l = Label(bed, "", 0f, 0f, 0.5f, 2.0f, 0.4f, Color.white); l.transform.localPosition = new Vector3(0f, 0.95f, 1.0f); mapLabels.Add(l); sleepZ[k] = l; PodLights(bed, k, new Vector3(0f, 0.36f, -1.07f));
                sleepBed[k] = bed;
            }
            Physics.SyncTransforms(); sleepPlaced = "bedroom " + W.ToString("F1") + " x " + D.ToString("F1") + " at " + rp.ToString("F1");
        }
        else
        {
            sleepPlaced = "no room for a bedroom: pods by the walls ";
            var spots = new List<Vector3>(); var faces = new List<Vector3>(); int free = 0, stacked = 0;
            for (int k = 0; k < 4; k++)
            {
                Vector3 pos = z.position, face = Vector3.forward; Transform bed = new GameObject("Sleep pod " + (k + 1)).transform; bed.SetParent(root, false);
                bool ok = false; if (k % 2 == 0) ok = FreeNear(z.position, z.yaw + 90f + k * 90f, new Vector3(0.7f, 1.2f, 1.25f), new[] { 3.3f, 4.0f, 4.8f, 5.6f, 6.6f, 8f, 9.5f, 11f }, out pos, out face);
                float up = 0f;
                if (ok) { spots.Add(pos); faces.Add(face); free++; stacked = 0; }
                else if (spots.Count > 0) { int under = spots.Count - 1; pos = spots[under]; face = faces[under]; up = 1.2f * ++stacked; }
                bed.position = pos + Vector3.up * up; bed.rotation = Quaternion.LookRotation(face, Vector3.up);
                Prim(PrimitiveType.Cube, bed, new Vector3(0f, 0.17f, 0f), new Vector3(1.0f, 0.34f, 2.1f), frame);
                Prim(PrimitiveType.Cube, bed, new Vector3(0f, 0.41f, 0f), new Vector3(0.9f, 0.14f, 2.0f), white);
                Prim(PrimitiveType.Cube, bed, new Vector3(0f, 0.5f, 0.42f), new Vector3(0.92f, 0.06f, 1.1f), Lit(SleepColour[k]));
                FacButton(bed, new Vector3(0f, 0.5f, 0f), new Vector3(1.05f, 0.9f, 2.15f), Clear(new Color(1f, 1f, 1f, 0.02f)), 1500 + k);
                if (up > 0f) foreach (float px in new[] { -0.52f, 0.52f }) foreach (float pz in new[] { -1.02f, 1.02f }) Prim(PrimitiveType.Cube, bed, new Vector3(px, -up * 0.5f + 0.2f, pz), new Vector3(0.06f, up + 0.4f, 0.06f), steel);
                if (up <= 0f) { Solid(bed, new Vector3(0f, 0.3f, 0f), new Vector3(1.0f, 0.6f, 2.1f)); Physics.SyncTransforms(); }
                var l = Label(bed, "", 0f, 0f, 0.7f, 2.2f, 0.5f, Color.white); l.transform.localPosition = new Vector3(0f, 1.25f, -0.9f); mapLabels.Add(l); sleepZ[k] = l; PodLights(bed, k, new Vector3(0f, 0.36f, -1.07f));
                sleepBed[k] = bed; sleepPlaced += k + (ok ? ":floor " : (up > 0f ? ":upper bunk " : ":default "));
            }
        }
        game.crewPose = SleepPose;
    }

    bool CrewAsleep(int k)
    {
        if (sim.phase != HabPhase.Mission || k < 0 || k >= 4 || !sleepBed[k] || sim.sheltered || sim.stormActive || mtState != 0) return false;
        foreach (var a in sim.alerts) if (a.crew == k && (a.type == "psych" || a.type == "medical")) return false;
        return sleepOn || sim.IsNightHours;
    }
    /// called by the game for every crew member each frame: puts a sleeping one in their pod
    bool SleepPose(int k, Transform av)
    {
        if (MeetPose(k, av)) return true;
        if (!CrewAsleep(k)) return CrewAiPose(k, av);
        var bed = sleepBed[k]; Vector3 p = bed.TransformPoint(new Vector3(0f, 0.5f, 0.85f)); Quaternion r = bed.rotation * Quaternion.Euler(-90f, 0f, 0f);
        if ((av.position - p).sqrMagnitude > 0.0001f) av.SetPositionAndRotation(p, r);
        return true;
    }

    public void SleepToggle()
    {
        if (sim.phase != HabPhase.Mission) { Note("Sleep mode starts once the mission is running."); return; }
        sleepOn = !sleepOn; sleepStart = sim.missionSeconds; sleepAlerts = sim.alerts.Count;
        game.lightSwitch = sleepOn ? 0.15f : (lightsOn ? 1f : 0.04f);
        Banner(sleepOn ? "SLEEP MODE" : "WAKE UP", sleepOn ? "The crew are in their sleep pods. Lights are dimmed." : "The crew are back at work", false);
        sim.Say(sleepOn ? "ARIA: Sleep mode. The crew are resting in their pods and I have dimmed the lights. I will wake everyone if anything needs them." : "ARIA: Good morning. The crew are up and the lights are back.");
    }
    /// you lie down in your own pod: the clock runs faster until morning
    public void SleepMe()
    {
        if (sim.phase != HabPhase.Mission || autoOn) { Note("You can sleep once the mission is running."); return; }
        if (sleepMe) { SleepMeEnd("You got up."); return; }
        if (sleepZone < 0 || game.NearZone != sleepZone) { Note("Your sleep pod is in Crew Quarters. Walk there first."); sim.Say("ARIA: Your sleep pod is in Crew Quarters, Commander."); return; }
        sleepMe = true; sleepMeStart = sim.missionSeconds; sleepAlerts = sim.alerts.Count; sleepAt = Feet(); game.simSpeed = 8f; game.lightSwitch = 0.1f;
        Banner("SLEEPING", "Time passes faster until morning. Move, or press any key, to get up.", false);
        sim.Say("ARIA: Sleep well, Commander. I will wake you at six, or sooner if the habitat needs you.");
    }
    void SleepMeEnd(string why)
    {
        if (!sleepMe) return; sleepMe = false; game.simSpeed = 1f; game.lightSwitch = sleepOn ? 0.15f : (lightsOn ? 1f : 0.04f);
        float hrs = sim.missionSeconds - sleepMeStart; sleepHours += hrs;
        Banner("GOOD MORNING", "You slept " + hrs.ToString("F0") + " hours. " + why, false); sim.Say("ARIA: " + why + " You slept " + hrs.ToString("F0") + " hours.");
    }

    void UpdateSleep(float dt)
    {
        { var kb6 = Keyboard.current; if (kb6 != null && kb6.f6Key.wasPressedThisFrame) SwitchCrew(); }
        if (sleepZone < 0) return;
        bool mission = sim.phase == HabPhase.Mission;
        if (!mission) { if (sleepOn) { sleepOn = false; game.lightSwitch = lightsOn ? 1f : 0.04f; } if (sleepMe) { sleepMe = false; game.simSpeed = 1f; } }
        if (sleepOn)
        {
            // a daytime rest: the crew recover sleep as they do at night, and calm down a little
            if (!sim.timeFrozen && !sim.IsNightHours) for (int k = 1; k < sim.crew.Count; k++) if (CrewAsleep(k)) { var c = sim.crew[k]; c.sleep += 4.7f * dt; c.stress -= 0.4f * dt; c.Clamp(); }
            if (sim.alerts.Count > sleepAlerts) { sleepOn = false; game.lightSwitch = lightsOn ? 1f : 0.04f; Banner("WAKE UP", "ARIA woke the crew: the habitat needs them", true); }
            else if (sim.missionSeconds - sleepStart > 10f || sim.missionSeconds < sleepStart) { sleepOn = false; game.lightSwitch = lightsOn ? 1f : 0.04f; sim.Say("ARIA: Rest period over. The crew are getting up."); }
            sleepAlerts = Mathf.Min(sleepAlerts, sim.alerts.Count);
        }
        if (sleepMe)
        {
            float slept = sim.missionSeconds - sleepMeStart; var kb = Keyboard.current;
            if (sim.crew.Count > 0 && !sim.timeFrozen && !sim.IsNightHours) { var c = sim.crew[0]; c.sleep += 4.7f * dt * game.simSpeed; c.stress -= 0.4f * dt * game.simSpeed; c.Clamp(); }
            if (sim.alerts.Count > sleepAlerts) SleepMeEnd("ARIA woke you: the habitat needs you.");
            else if ((Feet() - sleepAt).sqrMagnitude > 1.2f * 1.2f || (kb != null && kb.anyKey.wasPressedThisFrame && slept > 0.5f) || (game.HeadsetActive && HunchVr.A)) SleepMeEnd("You got up.");
            else if (slept > 1f && (slept >= 9f || slept < 0f || (sim.Hour >= 6f && sim.Hour < 7f))) SleepMeEnd("It is morning.");
            sleepAlerts = Mathf.Min(sleepAlerts, sim.alerts.Count);
        }
        UpdateSleepLeds();
        for (int k = 0; k < 4; k++)
        {
            if (!sleepZ[k]) continue; bool me = k == 0, asleep = me ? sleepMe : CrewAsleep(k);
            string who = k < sim.crew.Count ? sim.crew[k].name : "crew";
            int st = PodStateOf(k);
            if (st != podState[k]) { podState[k] = st; for (int s = 0; s < 3; s++) if (podLight[k, s]) podLight[k, s].SetActive(s == st); }
            bool looking = focus != null && focus.kind == 35 && focus.index == 1500 + k;
            sleepZ[k].text = "<b>RECOVERY POD</b>\n<size=60%>" + who + (asleep ? "   <color=#9AD0FF>z z z   asleep</color>" : (k < sim.crew.Count ? "   sleep " + Mathf.RoundToInt(sim.crew[k].sleep) + "%" : "")) + "\n" + PodStateName[st]
                + (looking ? "\n<color=#9fdcff>[H] sleep analytics   [G] recovery session</color>" : "") + "</size>";
        }
        {   // keys while looking at a pod (H and G are free in this game)
            var kb = Keyboard.current; int k = focus != null && focus.kind == 35 ? focus.index - 1500 : -1;
            if (kb != null && k >= 0 && k < 4 && k < sim.crew.Count && !game.menuOpen)
            {
                var c = sim.crew[k];
                if (kb.hKey.wasPressedThisFrame)
                    Banner("SLEEP ANALYTICS · " + c.name.ToUpper(), "Sleep " + Mathf.RoundToInt(c.sleep) + "%  ·  stress " + Mathf.RoundToInt(c.stress) + "%  ·  HR " + c.HeartRate + "  ·  " + (podState[k] == 2 ? "AI: calm protocol, Earth view, counsellor" : podState[k] == 1 ? "AI: circadian reset 21:30, light therapy" : "AI: at baseline, keep the routine"), false);
                if (kb.gKey.wasPressedThisFrame)
                {
                    if (sim.missionSeconds < podRecoverUntil) Note("A recovery session is already running.");
                    else { podRecoverUntil = sim.missionSeconds + 3f; if (podState[k] == 2) { c.stress -= 15f; c.mood += 4f; } else if (podState[k] == 1) { c.sleep += 12f; } else { c.mood += 3f; } c.Clamp();
                        sim.Say("ARIA: Recovery session for " + c.name + ": " + (podState[k] == 2 ? "calming blue light, Earth view and slow breathing." : podState[k] == 1 ? "circadian reset light and a 20-minute nap." : "a short relaxation, you're at your baseline.")); }
                }
            }
        }
    }
    string SleepText(int i) { int k = i - 1500; return k == 0 ? (sleepMe ? "your sleep pod: get up" : "your sleep pod: sleep until morning") : (k > 0 && k < sim.crew.Count ? sim.crew[k].name + "'s sleep pod: " + (sleepOn ? "wake the crew" : "send the crew to sleep") : ""); }
    void SleepUse(int i) { if (i == 1500) SleepMe(); else SleepToggle(); }

    // ================================================================== NASA HUNCH mode: teleport setting and rovers
    [NonSerialized] public bool hunchTeleport; HunchMenu.ExtraPage pageTele, pageShow;
    void BuildHunchExtras()
    {
        hunchTeleport = PlayerPrefs.GetInt("hunch_teleport2", 1) == 1;
        game.teleportOk = () => HunchCheats.TeleOk;      // a cheat the player switched on wins over every other setting
        game.beforeJump = () => { if (ride != null) Dismount(null); if (sliceOn) SliceEnd(); };
        if (!menu) return;
        pageTele = new HunchMenu.ExtraPage { label = TeleLabel(), hint = "ON adds the Travel page (go straight to any zone) and the jump to the rovers at the mining field, in NASA HUNCH mode. OFF means you walk, or use the short hop on the right grip in the headset. It is your choice: real astronauts walk.", direct = () => { hunchTeleport = !hunchTeleport; PlayerPrefs.SetInt("hunch_teleport2", hunchTeleport ? 1 : 0); pageTele.label = TeleLabel(); menu.Rebuild(); } };
        int at = Mathf.Max(0, menu.extraPages.IndexOf(pageCheck)); menu.extraPages.Insert(at, pageTele);
        // the centre dome stays open: trees belong only in the greenhouse
        BuildHubFloor();
        pageShow = new HunchMenu.ExtraPage { label = "The Show: the mission as a cartoon", hint = "The Commander strolls through the real 30-day mission while you watch: episode cards, speech bubbles, comic bursts and changing camera angles. SPACE (A in the headset) stops it. For a whole show from the first minute, choose The Show on the main menu.", direct = () => { menu.Close(); StartShow(); } };
        menu.extraPages.Add(pageShow);
        pageCheat = new HunchMenu.ExtraPage { title = "CHEATS AND ANTI-CHEAT", label = CheatLabel(), hint = "Every shortcut that bends the rules, in one place: walking through walls, flying, jumping to zones and skipping time. Anti-cheat ON switches them all off for fair play.", build = CheatMenu };
        menu.extraPages.Add(pageCheat);
        menu.extraShow = p => hunchOn ? (p == pageHunch || p == pageCheck || p == pageBase || p == pageTele || p == pageShow || p == pageCheat || (hunchTeleport && HunchCheats.TeleOk && p.label.StartsWith("Travel"))) : (p != pageTele && (HunchCheats.TeleOk || !p.label.StartsWith("Travel")));
    }
    string TeleLabel() { return "Teleport in NASA HUNCH mode: " + (hunchTeleport ? "<color=#7CFC9A>ON</color>" : "off"); }
    /// the helper rovers are real robotics, so NASA HUNCH mode keeps them (rows for the Base systems page)
    void HunchRoverRows(HunchMenu m)
    {
        m.AddItem("Call the food rover", "A rover brings 2 sealed food packs. Ready again after 45 seconds.", () => { m.Close(); CallHelper(0); });
        m.AddItem("Call the medic rover", "A rover with the medical kit comes to you.", () => { m.Close(); CallHelper(1); });
        m.AddItem("Call the repair rover", "A rover fixes whichever panel is broken.", () => { m.Close(); CallHelper(2); });
    }

    // ================================================================== the centre of the base, cleared
    /// The trees, planters, desks, screens and couches in the central hub are one baked piece of the Blender model, so they
    /// cannot be removed one at a time in Unity. Clear switches that whole piece off (the hall, stairs and floors stay);
    /// it is done before anything else is built so nothing is placed on furniture that is not there.
    [NonSerialized] public bool hubClear; GameObject hubInterior; string hubNote = "";
    void HubClearEarly()
    {
        HunchCheats.ResetAll();      // a fresh start every time: the settings never carry over from the last game
        hubClear = true; /* trees belong only in the greenhouse: the hub forest is always switched off */ var model = GameObject.Find("HUNCH_Lunar_Base");
        if (model) { var t = model.transform.Find("BAKED_HUB_Interior"); if (t) hubInterior = t.gameObject; }
        if (!hubInterior) { hubNote = "the hub's furniture piece was not found"; return; }
        if (!hubClear) return;
        hubInterior.SetActive(false); Physics.SyncTransforms();
        foreach (var z in game.zones) if (!Physics.Raycast(z.position + Vector3.up * 1.0f, Vector3.down, 3.5f, ~0, QueryTriggerInteraction.Ignore)) { hubInterior.SetActive(true); Physics.SyncTransforms(); hubClear = false; hubNote = "kept: the " + z.title + " zone stands on it"; break; }      // never take a floor away from a zone
    }
    void HubRows(HunchMenu m)
    {
        m.AddItem("Centre dome: open hall (trees only in the greenhouse)" + (hubNote.Length > 0 ? "   (" + hubNote + ")" : ""), "The hub's forest, planters, desks and couches are one piece of the model and stay switched off: every tree in the base is in the greenhouse.", () => { });
    }

    // ================================================================== test hooks
    public string ProbeSleep() { SleepToggle(); return "sleepOn=" + sleepOn + " beds " + sleepPlaced + " asleep " + (CrewAsleep(1) ? 1 : 0) + (CrewAsleep(2) ? 1 : 0) + (CrewAsleep(3) ? 1 : 0) + " hour " + sim.Hour.ToString("F1") + " crew " + sim.crew.Count + " first " + sim.crew[0].name + " avatar0 " + (game.crewAvatars != null && game.crewAvatars.Length > 0 && game.crewAvatars[0] ? "shown" : "none"); }
    public string ProbeSleepView() { if (sleepZone < 0 || !sleepBed[1]) return "no beds"; game.flyMode = false; game.GoTo(sleepZone); Vector3 mid = (sleepBed[0].position + sleepBed[2].position) * 0.5f; if (probeInt >= 10) { Transform rm = sleepBed[0].parent; TeleportTo(rm.TransformPoint(new Vector3(0f, 0f, 1.2f)), rm.eulerAngles.y + 180f); Face(rm.TransformPoint(new Vector3(0f, 0.9f, -1.2f))); return "inside the bedroom " + sleepPlaced; } Face(mid + Vector3.up * 1.1f); return "at quarters, near " + game.NearZone + " " + sleepPlaced; }
    public string ProbeSleepMe() { if (sleepZone >= 0) game.GoTo(sleepZone); SleepMe(); return "sleepMe=" + sleepMe + " speed " + game.simSpeed + " hour " + sim.Hour.ToString("F1"); }
    public string ProbeSleepInfo() { return "sleepOn=" + sleepOn + " sleepMe=" + sleepMe + " speed " + game.simSpeed + " hour " + sim.Hour.ToString("F1") + " day " + sim.Day + " light " + game.lightSwitch.ToString("F2") + " slept " + sleepHours.ToString("F0"); }
    /// test hook: stand at an exact place (probeX, probeY, probeZ), looking along probeYaw with pitch probeInt
    public string ProbeSpot() { game.flyMode = true; TeleportTo(new Vector3(probeX, probeY, probeZ), probeYaw); if (desk) desk.SetLook(probeYaw, probeInt); return "at " + probeX + "," + probeY + "," + probeZ; }
    /// test hook: switch every part of the base model whose name contains probeName off (probeInt 0) or on (probeInt 1)
    public string probeName = "";
    public string ProbeHubBounds() { var model = GameObject.Find("HUNCH_Lunar_Base"); if (!model) return "no model"; var sb = new System.Text.StringBuilder(); foreach (Transform ch in model.transform) if (ch.name.Contains(probeName.Length > 0 ? probeName : "HUB")) { var r = ch.GetComponent<Renderer>(); if (r) sb.Append(ch.name).Append(ch.gameObject.activeSelf ? " on " : " off ").Append(r.bounds.min.ToString("F1")).Append(r.bounds.max.ToString("F1")).Append(" | "); } return sb.ToString(); }
    public string ProbeModelPart() { var model = GameObject.Find("HUNCH_Lunar_Base"); if (!model || probeName.Length == 0) return "nothing"; int n = 0; foreach (Transform ch in model.transform) if (ch.name.Contains(probeName)) { ch.gameObject.SetActive(probeInt != 0); n++; } Physics.SyncTransforms(); return probeName + " -> " + (probeInt != 0 ? "on" : "off") + " x" + n; }
    public string ProbeHub() { return "hubClear=" + hubClear + " found=" + (hubInterior != null) + " active=" + (hubInterior && hubInterior.activeSelf) + " note=" + hubNote; }
    public string ProbeTele() { if (pageTele != null && pageTele.direct != null) pageTele.direct(); return "teleport " + hunchTeleport + " | " + ProbeMenuPages(); }
}
