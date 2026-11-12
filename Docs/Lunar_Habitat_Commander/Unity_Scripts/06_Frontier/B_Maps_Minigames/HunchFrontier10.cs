// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/B_Maps_Minigames/HunchFrontier10.cs
//  Section : 06_Frontier/B_Maps_Minigames
//  Folder  : Extra maps and mini-games: solar farm, mining pit, Factory Land, Star Patrol, crew
//            radio + Star Clicker, Science Academy, My Astronaut, model kits, Fun Zone, endless
//            ground, Star Store pages.
//  Purpose : Tenth part of Moon Frontier: CREW RADIO talk into the microphone: the game shows
//            TRANSMITTING, and a crew member or ARIA answers when you stop.
//  Contains: HunchFrontier
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// Tenth part of Moon Frontier:
///   CREW RADIO            talk into the microphone: the game shows TRANSMITTING, and a crew member or ARIA answers when you stop.
///                          Only the loudness is measured. Nothing is recorded, saved or sent anywhere. F7 (or the Base menu) turns it off.
///   STAR CLICKER (map 10) click the star to make stardust, buy helpers that make stardust by themselves, grow the star through
///                          the real stages of a star's life, catch lucky comets, and go SUPERNOVA to start again stronger.
/// Also the shared plumbing for parts 10, 11 and 12 (click targets with index 800 and above).
public partial class HunchFrontier
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ================================================================== shared plumbing
    string ExtraText(int i)
    {
        if (i < 900) return ClickerText(i);
        if (i < 1000) return LevelText(i);
        if (i < 1100) return QuizText(i);
        if (i < 1200) return ClinicText(i);
        if (i < 1300) return HallText(i);
        if (i < 1400) return PetText(i);
        return RealText(i);
    }
    void ExtraUse(int i)
    {
        if (i < 900) ClickerUse(i);
        else if (i < 1000) LevelUse(i);
        else if (i < 1100) QuizUse(i);
        else if (i < 1200) ClinicUse(i);
        else if (i < 1300) HallUse(i);
        else if (i < 1400) PetUse(i);
        else RealUse(i);
    }
    void BuildBatch10()
    {
        try { BuildRadio(); } catch (Exception e) { HLog.E("Minigames", "radio: " + e); }
        try { BuildLevels(); } catch (Exception e) { HLog.E("Minigames", "levels: " + e); }
        try { BuildHallReads(); } catch (Exception e) { HLog.E("Minigames", "hall: " + e); }
        try { BuildSeason(); } catch (Exception e) { HLog.E("Minigames", "season: " + e); }
        try { BuildPhysics(); } catch (Exception e) { HLog.E("Minigames", "physics view: " + e); }
        try { BuildPortalPlaza(); } catch (Exception e) { HLog.E("Minigames", "portal plaza: " + e); }
        try { BuildHunch(); } catch (Exception e) { HLog.E("Minigames", "hunch mode: " + e); }
        try { BuildCrewFour(); BuildSleep(); BuildHunchExtras(); } catch (Exception e) { HLog.E("Minigames", "crew of four, sleep, extras: " + e); }
        try { BuildEndlessGround(); } catch (Exception e) { HLog.E("Minigames", "endless ground: " + e); }
        try { if (fieldOk) BuildFunZone(); } catch (Exception e) { HLog.E("Minigames", "fun zone: " + e); }
        try { BuildMeetingTable(); BuildTrackBoard(); BuildSwitchPage(); } catch (Exception e) { HLog.E("Minigames", "meeting table, tracking board: " + e); }
        try { BuildAres(); } catch (Exception e) { HLog.E("Minigames", "Project ARES: " + e); }
    }
    void UpdateBatch10(float dt)
    {
        UpdateRadio(dt); UpdateClicker(dt); UpdateLevels(dt); UpdatePet(dt); UpdateSeason(dt); UpdatePortals(dt); UpdateHunch(dt); UpdatePhysics(dt); UpdateSleep(dt); UpdateShow(); UpdateFunZone(dt);
        try { UpdateAres(dt); } catch (Exception e) { if (!aresErr) { aresErr = true; HLog.E("Minigames", "Project ARES update: " + e); } }
    }
    IEnumerator AutoMapExtra2(int k)
    {
        if (k == 10) yield return AutoClicker();
        else if (k == 11) yield return AutoAcademy();
        else if (k == 12) yield return AutoClinic();
        else if (k == 13) yield return AutoBoss();
    }
    /// a small button with its own label that turns to face you
    TextMeshPro TagLabel(Transform parent, Vector3 lp, float size, string text, Color c)
    {
        var l = Label(parent, text, 0f, 0f, size, 14f, 3f, c); l.transform.localPosition = lp; mapLabels.Add(l); return l;
    }
    public string ProbeMapView() { if (mapNow == 0) return "not on a map"; TeleportTo(mapC[mapNow] + new Vector3(probeX, 0f, probeZ), probeYaw); if (desk) desk.SetLook(probeYaw, probeInt); return "view " + probeX + "," + probeZ + " yaw " + probeYaw + " pitch " + probeInt; }

    // ================================================================== crew radio
    public bool radioOn = true; public float radioFake;      // radioFake: a pretend microphone level for tests
    AudioClip micClip, cRadioOn, cRadioOff; string radioProblem = ""; readonly float[] micBuf = new float[512];
    float radioLevel, radioFloor = 0.01f, radioTalk, radioQuiet, radioMute, radioRetry, radioShown; bool radioTx; public int radioSent; int radioTurn; string radioLast = "";

    void BuildRadio()
    {
        radioOn = PlayerPrefs.GetInt("hunch_radio", 1) == 1;
        cRadioOn = Sweep("radio on", 1250f, 1300f, 0.07f, 0.03f); cRadioOff = Sweep("radio roger", 2475f, 2475f, 0.2f, 0f);
        A("Radio operator", "Talk to the crew on the radio five times (use your microphone).", () => radioSent >= 5);
    }
    public void RadioToggle()
    {
        radioOn = !radioOn; PlayerPrefs.SetInt("hunch_radio", radioOn ? 1 : 0); if (!radioOn) RadioStop(); else radioRetry = 0f;
        Note(radioOn ? "Crew radio ON. Talk and the crew answer. Nothing is recorded." : "Crew radio OFF. The microphone is not used.");
    }
    void RadioStart()
    {
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            radioProblem = "the crew radio needs the PC or headset version"; return;
#else
            if (Microphone.devices == null || Microphone.devices.Length == 0) { radioProblem = "no microphone found"; return; }
            micClip = Microphone.Start(null, true, 1, 16000); radioProblem = micClip ? "" : "the microphone did not start";
#endif
        }
        catch (Exception e) { radioProblem = e.Message; micClip = null; }
    }
    void RadioStop()
    {
#if !(UNITY_WEBGL && !UNITY_EDITOR)
        try { if (micClip) Microphone.End(null); } catch { }
#endif
        micClip = null; radioTx = false; radioTalk = 0f;
    }
    float MicLevel()
    {
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            int pos = 0; if (true) { micClip = null; return 0f; }
#else
            int pos = Microphone.GetPosition(null); if (!Microphone.IsRecording(null)) { micClip = null; return 0f; }
#endif
            int n = micBuf.Length, start = pos - n; if (start < 0) start += micClip.samples;
            micClip.GetData(micBuf, start); float sum = 0f; for (int i = 0; i < n; i++) sum += micBuf[i] * micBuf[i];
            return Mathf.Sqrt(sum / n);
        }
        catch { micClip = null; return 0f; }
    }
    string RadioState { get { return !radioOn ? "off" : (radioProblem.Length > 0 && !micClip ? radioProblem : (micClip || radioFake > 0f ? "microphone ready" : "starting")); } }

    void UpdateRadio(float dt)
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f7Key.wasPressedThisFrame && !game.HeadsetActive && !game.menuOpen) RadioToggle();
        if (!radioOn) { if (micClip) RadioStop(); return; }
        if (!micClip && radioFake <= 0f) { radioRetry -= Time.unscaledDeltaTime; if (radioRetry <= 0f) { radioRetry = 6f; RadioStart(); } }
        float level = radioFake > 0f ? radioFake : (micClip ? MicLevel() : 0f);
        radioLevel = Mathf.Lerp(radioLevel, level, 1f - Mathf.Exp(-dt * 18f));
        if (radioMute > 0f) { radioMute -= dt; radioTx = false; radioTalk = 0f; return; }      // the crew is answering: their voice must not count as yours
        if (!radioTx) radioFloor = Mathf.Clamp(Mathf.Lerp(radioFloor, level, dt * (level < radioFloor ? 4f : 0.25f)), 0.002f, 0.08f);      // the room's own noise
        bool loud = radioLevel > Mathf.Max(0.02f, radioFloor * 3.2f);
        if (loud)
        {
            radioQuiet = 0f; radioTalk += dt;
            if (!radioTx && radioTalk > 0.25f && Playing) { radioTx = true; Play(cRadioOn, 0.5f); }
            if (radioTx && radioTalk > 20f) { radioTx = false; radioTalk = 0f; radioFloor = Mathf.Min(0.08f, level); }      // a steady noise, not a voice
        }
        else
        {
            radioQuiet += dt; if (!radioTx) radioTalk = Mathf.Max(0f, radioTalk - dt * 2f);
            if (radioTx && radioQuiet > 1.0f) { radioTx = false; Play(cRadioOff, 0.45f); if (radioTalk > 0.7f) RadioReply(radioTalk); radioTalk = 0f; }
        }
    }

    void RadioReply(float seconds)
    {
        radioTurn++; radioSent++; int n = sim.crew.Count, who = radioTurn % 3 == 0 || n < 2 ? -1 : (player + 1 + radioTurn % (n - 1)) % n;
        string you = sim.crew[Mathf.Clamp(player, 0, n - 1)].name, line;
        int ok, warn, bad; HsCounts(out ok, out warn, out bad);
        if (who < 0)
        {
            if (mtState == 1) line = "Copy, " + you + ". A team meeting is waiting for you in the Galley.";
            else if (bad > 0) line = "Copy. One crew member needs support right now. Please check the Crew Health wall.";
            else if (sim.oxygen < 40f) line = "Copy. Oxygen is at " + Mathf.RoundToInt(sim.oxygen) + " percent. Life Support needs you.";
            else if (sim.battery < 35f) line = "Copy. Battery is at " + Mathf.RoundToInt(sim.battery) + " percent. The Power Plant needs you.";
            else if (mapNow != 0) line = "Copy, " + you + ". I am tracking you on the " + MapNames[mapNow] + " map. The base is steady.";
            else line = "Loud and clear, " + you + ". Oxygen " + Mathf.RoundToInt(sim.oxygen) + ", water " + Mathf.RoundToInt(sim.water) + ", battery " + Mathf.RoundToInt(sim.battery) + " percent.";
        }
        else
        {
            var c = sim.crew[who]; c.mood = Mathf.Min(100f, c.mood + 1.5f); c.stress = Mathf.Max(0f, c.stress - 1f);      // hearing a friendly voice helps
            string[] good = { "Loud and clear, " + you + "! All good on my side.", "Copy that. Thanks for checking in, it really helps.", "Roger, " + you + ". I am on it.", "I hear you. Nice to hear a friendly voice out here." };
            string[] tired = { "I hear you. It has been a long shift, thanks for checking in.", "Copy. I could use a break soon, but I am okay.", "Roger. Talking helps. Thank you, " + you + "." };
            line = c.stress > 55f || c.mood < 45f ? tired[radioTurn % tired.Length] : good[radioTurn % good.Length];
            if (seconds > 6f) line = "That was a long message, and I got all of it. " + line;
        }
        string name = who < 0 ? "ARIA" : sim.crew[who].name; radioLast = name + ": " + line; radioShown = Time.time + 6f;
        sim.Say(name + " (radio): " + line);      // ARIA's lines are spoken by the menu's voice; crew lines here
        if (who >= 0) { try { var v = GetComponent<HunchVoice>(); if (v && v.speakOn) v.Say(line, who + 1); } catch { } }
        radioMute = 2.5f + line.Length * 0.065f;
    }

    /// one HUD line while you talk and just after the answer
    string RadioHud()
    {
        if (!radioOn || !Playing) return "";
        if (radioTx) { int k = Mathf.Clamp(Mathf.RoundToInt(radioLevel * 60f), 1, 10); return "<color=#FF9A8A><b>RADIO: TRANSMITTING</b>  " + new string('|', k) + new string('.', 10 - k) + "</color>"; }
        if (Time.time < radioShown) return "<color=#FFD070><b>RADIO</b>  " + radioLast + "</color>";
        return "";
    }
    public string ProbeRadio() { return "radio on=" + radioOn + " state=" + RadioState + " tx=" + radioTx + " level=" + radioLevel.ToString("F3") + " floor=" + radioFloor.ToString("F3") + " sent=" + radioSent + " last=" + radioLast + " mics=" +
#if UNITY_WEBGL && !UNITY_EDITOR
        0; }
#else
        (Microphone.devices != null ? Microphone.devices.Length : 0); }
#endif

    // ================================================================== star clicker (map 10)
    const int ClkN = 6; const double ClkNovaAt = 250000;
    static readonly string[] ClkName = { "Star-catcher drone", "Comet net", "Solar sail", "Nebula farm", "Star nursery", "Galaxy engine" };
    static readonly double[] ClkCost0 = { 15, 100, 800, 6000, 45000, 350000 };
    static readonly double[] ClkRate = { 0.5, 4, 20, 110, 600, 3200 };
    static readonly string[] ClkStage = { "Protostar", "Red dwarf", "Yellow star", "Blue giant", "Red supergiant", "Galaxy heart" };
    static readonly double[] ClkStageAt = { 0, 200, 5000, 100000, 2000000, 50000000 };
    static readonly Color[] ClkStageCol = { new Color(1.3f, 0.55f, 0.25f), new Color(1.9f, 0.4f, 0.25f), new Color(2.3f, 1.8f, 0.45f), new Color(0.6f, 1.2f, 2.7f), new Color(2.7f, 0.7f, 0.3f), new Color(2.2f, 1.1f, 2.7f) };
    public double clkDust, clkTotal, clkLife; public int clkClicks, clkPower, clkNova; readonly int[] clkGen = new int[ClkN];
    int clkPlayer = -1, clkStageNow = -1, clkPopAt; float clkSaveT, clkPunch, clkRush, clkCometIn = 25f, clkLabelT; bool clkCometOn;
    Transform clkStar, clkHalo, clkOrbitRoot, clkComet; Material clkMat; TextMeshPro clkBig, clkPowerL, clkNovaL; readonly TextMeshPro[] clkGenL = new TextMeshPro[ClkN];
    readonly List<Transform>[] clkOrb = new List<Transform>[ClkN]; readonly TextMeshPro[] clkPop = new TextMeshPro[8]; readonly float[] clkPopT = new float[8];
    static readonly Vector3 ClkStarAt = new Vector3(0f, 8f, 18f);

    static string Big(double v)
    {
        if (v < 1000) return v < 10 && v != Math.Floor(v) ? v.ToString("F1", Inv) : Math.Floor(v).ToString("F0", Inv);
        string[] u = { "K", "M", "B", "T", "Qa", "Qi" }; int k = -1; while (v >= 1000 && k < u.Length - 1) { v /= 1000; k++; }
        return v.ToString(v < 10 ? "F2" : (v < 100 ? "F1" : "F0"), Inv) + u[k];
    }
    double ClkMult { get { return (1.0 + 0.5 * clkNova) * (clkRush > 0f ? 2.0 : 1.0); } }
    double ClkBase() { double r = 0; for (int i = 0; i < ClkN; i++) r += clkGen[i] * ClkRate[i]; return r; }
    double ClkRateNow() { return ClkBase() * ClkMult; }
    double ClkClick() { return (Math.Pow(2, clkPower) + ClkBase() * 0.03) * ClkMult; }
    double ClkCost(int i) { return Math.Ceiling(ClkCost0[i] * Math.Pow(1.15, clkGen[i])); }
    double ClkPowerCost() { return 50 * Math.Pow(6, clkPower); }
    int ClkStageOf(double total) { int s = 0; for (int i = 0; i < ClkStageAt.Length; i++) if (total >= ClkStageAt[i]) s = i; return s; }

    void ClkSave()
    {
        if (clkPlayer < 0) return; var sb = new StringBuilder();
        sb.Append(clkDust.ToString("R", Inv)).Append('|').Append(clkTotal.ToString("R", Inv)).Append('|').Append(clkLife.ToString("R", Inv)).Append('|').Append(clkClicks).Append('|').Append(clkPower).Append('|').Append(clkNova).Append('|');
        for (int i = 0; i < ClkN; i++) sb.Append(i > 0 ? "," : "").Append(clkGen[i]);
        PlayerPrefs.SetString("hunch_clk_" + clkPlayer, sb.ToString());
    }
    void ClkLoad()
    {
        if (clkPlayer >= 0) ClkSave();
        clkPlayer = player; clkDust = clkTotal = clkLife = 0; clkClicks = clkPower = clkNova = 0; for (int i = 0; i < ClkN; i++) clkGen[i] = 0;
        try
        {
            var a = PlayerPrefs.GetString("hunch_clk_" + player, "").Split('|');
            if (a.Length >= 7)
            {
                clkDust = double.Parse(a[0], Inv); clkTotal = double.Parse(a[1], Inv); clkLife = double.Parse(a[2], Inv); clkClicks = int.Parse(a[3]); clkPower = int.Parse(a[4]); clkNova = int.Parse(a[5]);
                var g = a[6].Split(','); for (int i = 0; i < ClkN && i < g.Length; i++) clkGen[i] = int.Parse(g[i]);
            }
        }
        catch { }
        clkStageNow = ClkStageOf(clkTotal); ClkOrbits(true); ClkPaint();
    }

    void BuildClicker(Transform r)
    {
        BuildExtraSounds();
        var dark = Lit(new Color(0.12f, 0.13f, 0.18f)); var gold = Lit(new Color(0.95f, 0.74f, 0.22f));
        clkMat = new Material(solidSrc); clkMat.SetColor("_BaseColor", ClkStageCol[0]);
        var s = Prim(PrimitiveType.Sphere, r, ClkStarAt, Vector3.one * 4f, clkMat, true); s.name = "The star"; var st = s.AddComponent<HunchTarget>(); st.kind = 35; st.index = 800; clkStar = s.transform;
        clkHalo = Prim(PrimitiveType.Sphere, r, ClkStarAt, Vector3.one * 5.4f, Clear(new Color(1f, 0.8f, 0.4f, 0.10f))).transform;
        clkOrbitRoot = new GameObject("Orbits").transform; clkOrbitRoot.SetParent(r, false); clkOrbitRoot.localPosition = ClkStarAt;
        for (int i = 0; i < ClkN; i++) clkOrb[i] = new List<Transform>();
        clkBig = TagLabel(r, ClkStarAt + new Vector3(0f, 6.2f, 0f), 9f, "", new Color(1f, 0.84f, 0.3f)); clkBig.rectTransform.sizeDelta = new Vector2(60f, 9f);
        Color[] gc = { new Color(0.6f, 0.65f, 0.72f), new Color(0.3f, 1.4f, 1.6f), new Color(1.6f, 1.2f, 0.3f), new Color(1.1f, 0.4f, 1.6f), new Color(1.7f, 0.5f, 0.9f), new Color(0.4f, 0.7f, 2.2f) };
        for (int i = 0; i < ClkN; i++)
        {
            var p = new GameObject(ClkName[i]).transform; p.SetParent(r, false); p.localPosition = new Vector3((i - 2.5f) * 5.2f, 0f, 2f);
            Prim(PrimitiveType.Cube, p, new Vector3(0f, 0.5f, 0f), new Vector3(3.4f, 1f, 1.6f), dark); Prim(PrimitiveType.Cube, p, new Vector3(0f, 1.03f, 0f), new Vector3(3.5f, 0.06f, 1.7f), gold);
            FacButton(p, new Vector3(0f, 1.35f, 0f), new Vector3(2.6f, 0.6f, 1.1f), Glow(gc[i] * 0.8f), 801 + i);
            ClkShape(i, p, new Vector3(0f, 2.3f, 0f), 1.6f);
            clkGenL[i] = TagLabel(p, new Vector3(0f, 3.9f, 0f), 1.9f, "", Color.white);
        }
        var up = new GameObject("Click power").transform; up.SetParent(r, false); up.localPosition = new Vector3(-18f, 0f, -4f);
        Prim(PrimitiveType.Cube, up, new Vector3(0f, 0.5f, 0f), new Vector3(3.4f, 1f, 1.6f), dark); FacButton(up, new Vector3(0f, 1.35f, 0f), new Vector3(2.6f, 0.6f, 1.1f), Glow(new Color(0.4f, 1.8f, 0.6f)), 810);
        clkPowerL = TagLabel(up, new Vector3(0f, 3.0f, 0f), 1.9f, "", Color.white);
        var nv = new GameObject("Supernova").transform; nv.SetParent(r, false); nv.localPosition = new Vector3(18f, 0f, -4f);
        Prim(PrimitiveType.Cube, nv, new Vector3(0f, 0.5f, 0f), new Vector3(3.4f, 1f, 1.6f), dark); FacButton(nv, new Vector3(0f, 1.35f, 0f), new Vector3(2.6f, 0.6f, 1.1f), Glow(new Color(2.2f, 0.5f, 1.8f)), 830);
        clkNovaL = TagLabel(nv, new Vector3(0f, 3.0f, 0f), 1.9f, "", Color.white);
        var cm = Prim(PrimitiveType.Sphere, r, new Vector3(-45f, 10f, 10f), Vector3.one * 1.3f, Glow(new Color(2.6f, 2.0f, 0.5f)), true); cm.name = "Lucky comet"; var ct = cm.AddComponent<HunchTarget>(); ct.kind = 35; ct.index = 820;
        var cs = cm.GetComponent<SphereCollider>(); if (cs) cs.radius = 1.3f; Prim(PrimitiveType.Sphere, cm.transform, new Vector3(-1.2f, 0f, 0f), new Vector3(2.4f, 0.5f, 0.5f), Clear(new Color(1f, 0.9f, 0.5f, 0.25f))); clkComet = cm.transform; cm.SetActive(false);
        for (int i = 0; i < clkPop.Length; i++) { clkPop[i] = TagLabel(r, ClkStarAt, 4.5f, "", new Color(1f, 0.95f, 0.6f)); clkPop[i].gameObject.SetActive(false); }
        A("Star clicker", "Click the star 100 times on the Star Clicker map.", () => clkClicks >= 100);
        A("Stardust tycoon", "Make one million stardust in total.", () => clkLife >= 1000000);
        A("Supernova!", "Grow the star until it can go supernova, then press SUPERNOVA.", () => clkNova >= 1);
    }

    GameObject ClkShape(int i, Transform parent, Vector3 lp, float k)
    {
        switch (i)
        {
            case 0: return Prim(PrimitiveType.Cube, parent, lp, new Vector3(0.5f, 0.25f, 0.5f) * k, Lit(new Color(0.7f, 0.74f, 0.8f)));
            case 1: return Prim(PrimitiveType.Sphere, parent, lp, new Vector3(0.7f, 0.25f, 0.7f) * k, Glow(new Color(0.3f, 1.5f, 1.7f)));
            case 2: return Prim(PrimitiveType.Cube, parent, lp, new Vector3(0.8f, 0.8f, 0.04f) * k, Lit(new Color(0.98f, 0.8f, 0.3f)));
            case 3: return Prim(PrimitiveType.Sphere, parent, lp, Vector3.one * 0.8f * k, Clear(new Color(0.8f, 0.3f, 1f, 0.45f)));
            case 4: return Prim(PrimitiveType.Sphere, parent, lp, Vector3.one * 0.5f * k, Glow(new Color(2.2f, 0.7f, 1.2f)));
            default: return Prim(PrimitiveType.Cylinder, parent, lp, new Vector3(0.6f, 0.12f, 0.6f) * k, Glow(new Color(0.5f, 0.9f, 2.6f)));
        }
    }
    void ClkOrbits(bool rebuild)
    {
        if (!clkOrbitRoot) return;
        for (int i = 0; i < ClkN; i++)
        {
            if (rebuild) { foreach (var t in clkOrb[i]) if (t) Destroy(t.gameObject); clkOrb[i].Clear(); }
            int want = Mathf.Min(clkGen[i], 8);
            while (clkOrb[i].Count < want) clkOrb[i].Add(ClkShape(i, clkOrbitRoot, Vector3.zero, 1f).transform);
        }
    }
    void ClkPaint() { if (clkMat) clkMat.SetColor("_BaseColor", ClkStageCol[Mathf.Clamp(clkStageNow, 0, ClkStageCol.Length - 1)]); }
    void ClkFloat(string text, Vector3 at)
    {
        var l = clkPop[clkPopAt]; if (!l) return; l.text = text; l.transform.position = at; l.gameObject.SetActive(true); clkPopT[clkPopAt] = 0.9f; clkPopAt = (clkPopAt + 1) % clkPop.Length;
    }

    /// stardust keeps coming on every map; the pictures only move while you are on the Star Clicker map
    void UpdateClicker(float dt)
    {
        if (!clkStar) return;
        if (clkPlayer != player) ClkLoad();
        if (!Playing) return;
        if (clkRush > 0f) clkRush -= dt;
        double gain = ClkRateNow() * dt; clkDust += gain; clkTotal += gain; clkLife += gain;
        clkSaveT -= dt; if (clkSaveT <= 0f) { clkSaveT = 10f; ClkSave(); }
        int s = ClkStageOf(clkTotal);
        if (s != clkStageNow) { bool grew = s > clkStageNow; clkStageNow = s; ClkPaint(); if (grew && mapNow == 10) { Banner("YOUR STAR GREW!", "It is now a " + ClkStage[s].ToLower(), false); Play(cWin, 0.7f); AddXp(30); } }
    }
    void UpdateClickerMap(float dt)
    {
        float t = Time.time; clkPunch = Mathf.MoveTowards(clkPunch, 0f, dt * 3f);
        float size = (4f + clkStageNow * 0.7f) * (1f + Mathf.Sin(t * 2.2f) * 0.03f + clkPunch * 0.18f);
        clkStar.localScale = Vector3.one * size; clkStar.Rotate(0f, dt * 12f, 0f); clkHalo.localScale = Vector3.one * size * (1.3f + Mathf.Sin(t * 1.3f) * 0.05f);
        for (int i = 0; i < ClkN; i++)
        {
            float rad = 5.5f + i * 1.4f, sp = 0.9f - i * 0.1f; var tilt = Quaternion.Euler((i - 2.5f) * 9f, 0f, (i % 2 == 0 ? 6f : -6f));
            for (int j = 0; j < clkOrb[i].Count; j++) { float a = t * sp + j * (Mathf.PI * 2f / Mathf.Max(1, clkOrb[i].Count)); var o = clkOrb[i][j]; o.localPosition = tilt * new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad); o.Rotate(0f, dt * 60f, 0f); }
        }
        for (int i = 0; i < clkPop.Length; i++)
        {
            if (clkPopT[i] <= 0f) continue; clkPopT[i] -= dt; var l = clkPop[i]; l.transform.position += Vector3.up * dt * 2.4f; l.alpha = Mathf.Clamp01(clkPopT[i] * 2f);
            if (clkPopT[i] <= 0f) l.gameObject.SetActive(false);
        }
        // the lucky comet
        if (!clkCometOn) { clkCometIn -= dt; if (clkCometIn <= 0f) { clkCometOn = true; clkComet.gameObject.SetActive(true); clkComet.localPosition = new Vector3(-45f, 10f, 10f); Note("A lucky comet! Click it before it flies away."); Play(cWarp, 0.5f); } }
        else
        {
            var p = clkComet.localPosition; p.x += dt * 8f; p.y = 10f + Mathf.Sin(p.x * 0.25f) * 2f; clkComet.localPosition = p;
            if (p.x > 45f) { clkCometOn = false; clkComet.gameObject.SetActive(false); clkCometIn = R(30f, 50f); }
        }
        clkLabelT -= dt; if (clkLabelT > 0f) return; clkLabelT = 0.2f;
        clkBig.text = "<size=45%><b>STAR CLICKER</b>   " + ClkStage[clkStageNow] + (clkNova > 0 ? "   supernovas " + clkNova : "") + "</size>\n<b>" + Big(clkDust) + "</b> stardust\n<size=45%>+" + Big(ClkRateNow()) + " each second" + (clkRush > 0f ? "   <color=#7CFC9A>BOOST x2  " + Mathf.CeilToInt(clkRush) + " s</color>" : "") + "</size>";
        for (int i = 0; i < ClkN; i++)
        {
            double c = ClkCost(i); bool can = clkDust >= c;
            clkGenL[i].text = "<b>" + ClkName[i] + "</b>  x" + clkGen[i] + "\n<size=80%>+" + Big(ClkRate[i] * ClkMult) + "/s each\n<color=" + (can ? "#7CFC9A" : "#FF9A8A") + ">cost " + Big(c) + "</color></size>";
        }
        double pc = ClkPowerCost();
        clkPowerL.text = "<b>STRONGER CLICK</b>  level " + clkPower + "\n<size=80%>each click +" + Big(ClkClick()) + "\n<color=" + (clkDust >= pc ? "#7CFC9A" : "#FF9A8A") + ">cost " + Big(pc) + "</color></size>";
        clkNovaL.text = "<b>SUPERNOVA</b>\n<size=80%>" + (clkTotal >= ClkNovaAt ? "<color=#7CFC9A>READY: start again with\n+50% stardust for ever</color>" : "unlocks at " + Big(ClkNovaAt) + " made\nso far " + Big(clkTotal)) + "</size>";
    }

    string ClickerText(int i)
    {
        if (i == 800) return "click the star: +" + Big(ClkClick()) + " stardust";
        if (i >= 801 && i < 801 + ClkN) return "buy a " + ClkName[i - 801].ToLower() + " for " + Big(ClkCost(i - 801)) + " stardust";
        if (i == 810) return "stronger click for " + Big(ClkPowerCost()) + " stardust";
        if (i == 820) return "catch the lucky comet!";
        if (i == 830) return clkTotal >= ClkNovaAt ? "go SUPERNOVA: start again, +50% stardust for ever" : "supernova unlocks at " + Big(ClkNovaAt) + " stardust made";
        return "";
    }
    void ClickerUse(int i)
    {
        if (i == 800)
        {
            double v = ClkClick(); clkDust += v; clkTotal += v; clkLife += v; clkClicks++; clkPunch = 1f; Play(cPick, 0.35f); game.Buzz(0.3f, 0.04f);
            ClkFloat("+" + Big(v), clkStar.position + new Vector3(R(-2.5f, 2.5f), R(1f, 3f), -3f));
            if (clkClicks % 25 == 0) AddXp(5);
            return;
        }
        if (i >= 801 && i < 801 + ClkN)
        {
            int g = i - 801; double c = ClkCost(g);
            if (clkDust < c) { Play(cBoop, 0.4f); Note("A " + ClkName[g].ToLower() + " costs " + Big(c) + " stardust. You have " + Big(clkDust) + ". Keep clicking the star!"); return; }
            clkDust -= c; clkGen[g]++; ClkOrbits(false); Play(cClank, 0.6f); clkLabelT = 0f; Note(ClkName[g] + " bought. It makes " + Big(ClkRate[g] * ClkMult) + " stardust every second by itself."); return;
        }
        if (i == 810)
        {
            double c = ClkPowerCost(); if (clkDust < c) { Play(cBoop, 0.4f); Note("A stronger click costs " + Big(c) + " stardust."); return; }
            clkDust -= c; clkPower++; Play(cGrow, 0.7f); clkLabelT = 0f; Note("Stronger click! Each click now gives " + Big(ClkClick()) + " stardust."); return;
        }
        if (i == 820)
        {
            if (!clkCometOn) return; double v = Math.Max(ClkRateNow() * 45.0, ClkClick() * 30.0); clkDust += v; clkTotal += v; clkLife += v;
            HunchFx.Spawn("star hit", clkComet.position, 1f, 1f); Play(cWin, 0.6f); ClkFloat("COMET! +" + Big(v), clkComet.position); clkCometOn = false; clkComet.gameObject.SetActive(false); clkCometIn = R(30f, 50f); AddXp(10); return;
        }
        if (i == 830)
        {
            if (clkTotal < ClkNovaAt) { Play(cBoop, 0.4f); Note("The star is not big enough yet. Make " + Big(ClkNovaAt) + " stardust in total (so far " + Big(clkTotal) + ")."); return; }
            clkNova++; clkDust = 0; clkTotal = 0; clkPower = 0; for (int g = 0; g < ClkN; g++) clkGen[g] = 0; ClkOrbits(true); clkStageNow = 0; ClkPaint(); clkLabelT = 0f;
            HunchFx.Spawn("star hit", clkStar.position, 4f, 2f); Play(cBoom, 0.8f); Play(cWin, 0.8f); game.Buzz(1f, 0.5f); AddItem(Crystal, 2); AddXp(80); ClkSave();
            Banner("SUPERNOVA!", "A new star is born. Stardust x" + (1.0 + 0.5 * clkNova).ToString("F1", Inv) + " for ever", false);
        }
    }
    string ClickerStatus() { return "Stardust " + Big(clkDust) + "   +" + Big(ClkRateNow()) + " each second   Click +" + Big(ClkClick()) + "   Star: " + ClkStage[Mathf.Max(0, clkStageNow)] + (clkNova > 0 ? "   Supernovas " + clkNova : ""); }

    IEnumerator AutoClicker()
    {
        AutoSay("Star Clicker. Clicking the star to make stardust, then buying helpers that make it by themselves.");
        TeleportTo(MapSpawn(10), 0f); yield return Turn(clkStar.position, 0.6f);
        for (int n = 0; n < 90 && mapNow == 10 && autoOn; n++)
        {
            ClickerUse(800);
            for (int g = ClkN - 1; g >= 0; g--) if (clkDust >= ClkCost(g)) { ClickerUse(801 + g); break; }
            if (clkCometOn) ClickerUse(820);
            yield return new WaitForSeconds(0.09f);
        }
        AutoSay("The star now makes " + Big(ClkRateNow()) + " stardust every second.");
    }
    public string ProbeClicker() { for (int n = 0; n < 40; n++) ClickerUse(800); ClickerUse(801); ClickerUse(801); ClickerUse(810); return ClickerStatus() + " | clicks " + clkClicks + " gens " + clkGen[0] + "," + clkGen[1]; }
    public string ProbeClickerRich() { clkDust += 400000; clkTotal += 400000; clkLife += 400000; for (int g = 0; g < ClkN; g++) for (int n = 0; n < 3; n++) ClickerUse(801 + g); clkCometIn = 0f; return ClickerStatus(); }
    public string ProbeNova() { ClickerUse(830); return ClickerStatus(); }
}
