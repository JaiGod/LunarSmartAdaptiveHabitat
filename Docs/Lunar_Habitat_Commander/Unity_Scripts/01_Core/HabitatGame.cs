// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/01_Core/HabitatGame.cs
//  Section : 01_Core
//  Folder  : Game rules and the habitat simulation (crew, systems, alerts), start-up, self-test,
//            cheats and the live link to the Crew Health web page.
//  Purpose : Lunar Habitat Commander: keep four crew and the habitat healthy for 30 days. Rules
//            come from the HUNCH proposal.
//  Contains: HabZone, HabitatGame
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[Serializable]
public class HabZone
{
    public string id, title, rubric, description;
    public Vector3 position;
    public float yaw;
    public Transform marker;
    public Renderer ring, orb;
    public TextMeshPro label;
    public XRSimpleInteractable interactable;
}

/// Lunar Habitat Commander: keep four crew and the habitat healthy for 30 days. Rules come from the HUNCH proposal.
public class HabitatGame : MonoBehaviour
{
    public List<HabZone> zones = new List<HabZone>();
    public XROrigin origin;
    public TextMeshPro hud;
    public Transform hudRoot;
    public Light[] interiorLights;
    public Transform[] crewAvatars;
    public TextMeshPro[] crewTags;
    public Renderer[] crewStatus;
    public Transform breathRing;
    public TextMeshPro breathText;
    public float standingEyeHeight = 1.4f;
    public bool flyMode;

    [NonSerialized] public HabitatSim sim = new HabitatSim();
    [NonSerialized] public string extraLine = "", extraReport = "", advisorLine = "", cardOverride = "", navLine = "";
    [NonSerialized] public Func<bool> uiHover;                  // true while a controller points at one of the big dashboard buttons
    [NonSerialized] public Func<bool> toolInUse;                // true while a tool or weapon is in the hand: the fire button then uses the tool
    [NonSerialized] public float[] lightMul;                    // one switch per lamp (the zone consoles)
    [NonSerialized] public float simSpeed = 1f;                 // how fast the habitat clock runs (8 while you sleep in your pod)
    [NonSerialized] public Func<int, Transform, bool> crewPose;  // lets HunchFrontier lay a sleeping crew member in their pod
    [NonSerialized] public float lightSwitch = 1f;              // the habitat light switch (HunchFrontier)
    [NonSerialized] public string toolLine = "";
    [NonSerialized] public bool simpleHud;                      // show only the most important things (HunchFrontier draws that screen)
    [NonSerialized] public Func<string> simpleCard;             // VR: the short dashboard card
    public void ToggleWrist() { SetWrist(!wristMode); }
    [NonSerialized] public bool freezeHud;                      // VR: keep the card still so its buttons can be pointed at
    bool hudPlaced, hudYawSet, hudTurning; float hudYaw;
    [NonSerialized] public bool uiHasMouse;                     // an overlay (the big map) is using the mouse pointer
    [NonSerialized] public bool menuOpen, externalMenu, hideHud, hideMarkers, hideCrewTags;
    [NonSerialized] public Func<string, bool> inputOverride;      // the menu takes over next / previous / act while it is open
    [NonSerialized] public bool autoBusy;                        // the autopilot is playing: the player's keys, clicks, sticks and triggers are ignored until they take over
    [NonSerialized] public Func<bool> teleportOk;      // NASA HUNCH mode with Teleport off: Tab, Q, G and the VR X button do not jump you to a zone
    bool TeleOk() { if (teleportOk == null || teleportOk()) return true; sim.Say("ARIA: Teleport is switched off. Walk there, the green arrows show the way. You can switch it on in the menu."); return false; }
    bool Taken(string what) { if (autoBusy && !menuOpen) return true; return inputOverride != null && inputOverride(what); }      // filled in by the kid-friendly features
    public int Current { get; private set; }
    public bool HeadsetActive { get; set; }
    public int NearZone { get; private set; } = -1;
    public bool Paused { get; set; }
    public bool ShowHelp { get { return showHelp; } set { showHelp = value; } }

    InputAction interact, next, prev, urgent, fly, help, pause, wrist;
    float[] lightBase; Color[] lightColor;
    float lastInteract = -9f, hudTimer, breathLeft;
    bool showHelp, wristMode, started;
    AudioSource beeper; AudioClip beep, chime;
    readonly List<Component> haptics = new List<Component>();
    MethodInfo hapticMethod;
    Transform leftController;
    string lastCalmMessage = "";
    GUIStyle sTitle, sText, sSmall, sBig; Texture2D white;

    static readonly Color cIdle = new Color(0.2f, 0.8f, 1f), cAlert = new Color(1f, 0.25f, 0.2f), cNext = new Color(0.3f, 1f, 0.45f), cWait = new Color(0.35f, 0.4f, 0.5f);

    // ------------------------------------------------------------------ setup
    void Awake()
    {
        Physics.gravity = new Vector3(0f, -1.62f, 0f);          // lunar gravity
        if (!origin) origin = FindFirstObjectByType<XROrigin>();
        sim.zoneTitle = id => { var z = zones.Find(x => x.id == id); return z != null ? z.title : id; };
        beeper = gameObject.AddComponent<AudioSource>(); beeper.spatialBlend = 0f; beeper.playOnAwake = false; beeper.volume = 0.5f; beeper.bypassListenerEffects = true;   // ARIA speaks over the suit radio
        beep = Tone(new[] { 880f, 660f, 880f }, 0.14f); chime = Tone(new[] { 523f, 659f, 784f }, 0.12f);
    }

    static AudioClip Tone(float[] freqs, float each)
    {
        int rate = 44100, per = Mathf.RoundToInt(rate * each); var data = new float[per * freqs.Length];
        for (int k = 0; k < freqs.Length; k++) for (int i = 0; i < per; i++)
        { float env = Mathf.Sin(Mathf.PI * i / per); data[k * per + i] = Mathf.Sin(2f * Mathf.PI * freqs[k] * i / rate) * env * 0.5f; }
        var c = AudioClip.Create("tone", data.Length, 1, rate, false); c.SetData(data, 0); return c;
    }

    void OnEnable()
    {
        interact = Make("interact", "<Keyboard>/e", "<Keyboard>/enter", "<Gamepad>/buttonSouth");      // VR triggers, A, B, X and Y are read in HunchVr
        next = Make("next", "<Keyboard>/tab", "<Gamepad>/rightShoulder");
        prev = Make("prev", "<Keyboard>/q", "<Gamepad>/leftShoulder");
        urgent = Make("urgent", "<Keyboard>/g", "<Gamepad>/buttonWest");
        fly = Make("fly", "<Keyboard>/f");
        help = Make("help", "<Keyboard>/h", "<XRController>{LeftHand}/menu", "<Gamepad>/start");
        pause = Make("pause", "<Keyboard>/p");
        wrist = Make("wrist", "<Keyboard>/f9");      // VR: in the menu under Explorer (the left stick click is the run button)
        interact.performed += c =>
        {
            if (Taken("interact")) return;
            if (HeadsetActive)
            {
                // VR: pulling the trigger while pointing at a zone's orb acts on that zone, from wherever you stand
                for (int i = 0; i < zones.Count; i++) if (zones[i].interactable && zones[i].interactable.isHovered) { Current = i; NearZone = i; DoInteract(true); return; }
                if (uiHover != null && uiHover()) return;      // the big dashboard buttons take the trigger
                // with a tool in the hand the right trigger fires it; the left trigger still acts in a zone ring
                if (toolInUse != null && toolInUse() && c.control != null)
                    foreach (var usage in c.control.device.usages) if (usage == UnityEngine.InputSystem.CommonUsages.RightHand) return;
            }
            DoInteract();
        };
        next.performed += c => { if (Taken("next")) return; if (c.control != null && c.control.device is UnityEngine.InputSystem.XR.XRController) return;      // VR: B changes the tool outside menus
            if (!TeleOk()) return; GoTo((Current + 1) % zones.Count); };
        prev.performed += c => { if (Taken("prev")) return; if (c.control != null && c.control.device is UnityEngine.InputSystem.XR.XRController) return;      // VR: Y is the talk button outside menus
            if (!TeleOk()) return; GoTo((Current - 1 + zones.Count) % zones.Count); };
        urgent.performed += _ => { if (!Taken("urgent")) GoToUrgent(); };
        fly.performed += _ => { if (menuOpen || autoBusy) return; if (!flyMode && !HunchCheats.FlyOk) { sim.Say("ARIA: Flying is switched off. You can switch it on in the menu: Cheats and anti-cheat."); return; } flyMode = !flyMode; };
        help.performed += _ => { if (!Taken("help")) showHelp = !showHelp; };
        pause.performed += _ => { if (!menuOpen) Paused = !Paused; };
        wrist.performed += _ => { if (!menuOpen) SetWrist(!wristMode); };
    }
    void OnDisable() { foreach (var a in new[] { interact, next, prev, urgent, fly, help, pause, wrist }) if (a != null) { a.Disable(); a.Dispose(); } }
    static InputAction Make(string name, params string[] bindings)
    {
        var a = new InputAction(name, InputActionType.Button);
        foreach (var b in bindings) a.AddBinding(b);
        a.Enable(); return a;
    }

    void Start()
    {
        var cam = origin ? origin.Camera : Camera.main;
        if (cam)
        {
            cam.farClipPlane = 6000f; cam.nearClipPlane = 0.05f;
            var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data) data.renderPostProcessing = true;
        }
        if (interiorLights != null)
        {
            lightBase = new float[interiorLights.Length]; lightColor = new Color[interiorLights.Length];
            for (int i = 0; i < interiorLights.Length; i++) if (interiorLights[i]) { lightBase[i] = interiorLights[i].intensity; lightColor[i] = interiorLights[i].color; }
        }
        for (int i = 0; i < zones.Count; i++)
        {
            int k = i; var z = zones[i];
            if (z.interactable) z.interactable.selectEntered.AddListener(_ => { if (!HunchVr.Claim(true)) return; Current = k; NearZone = k; DoInteract(true); });   // VR: point at the orb and pull the trigger
        }
        if (origin)
        {
            foreach (var mb in origin.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!mb || mb.GetType().Name != "HapticImpulsePlayer") continue;
                var m = mb.GetType().GetMethod("SendHapticImpulse", new[] { typeof(float), typeof(float) });
                if (m != null) { haptics.Add(mb); hapticMethod = m; }
            }
            foreach (var t in origin.GetComponentsInChildren<Transform>(true)) if (t.name == "Left Controller") leftController = t;
            if (hudRoot) { var bgT = hudRoot.Find("Background"); var bgR = bgT ? bgT.GetComponent<Renderer>() : null; if (bgR) bgR.material.renderQueue = 2980; }      // the card is always drawn before the words on it, from any angle
        }
        if (breathRing) breathRing.gameObject.SetActive(false);
        Invoke(nameof(Begin), 0.4f);
    }

    void Begin() { started = true; GoTo(0); }

    /// VR: a trigger was pulled (HunchVr). Select what the laser points at; otherwise act in the zone ring you stand in.
    public void VrSubmit(bool rightHand)
    {
        if (Taken("interact")) return;
        for (int i = 0; i < zones.Count; i++) if (zones[i].interactable && (zones[i].interactable.isHovered || HunchVr.Hovering(zones[i].interactable.transform))) { Current = i; NearZone = i; DoInteract(true); return; }
        if (uiHover != null && uiHover()) return;                               // a big dashboard button takes it
        if (rightHand && toolInUse != null && toolInUse()) return;              // a tool in the hand, or a panel part or rover under the laser
        DoInteract();
    }
    /// VR: the X button. Jump to the zone that needs you; with nothing pending, to the next zone.
    public void VrNextJob()
    {
        if (autoBusy || menuOpen || !started || sim.phase == HabPhase.Briefing || sim.phase == HabPhase.Report) return;
        if (!TeleOk()) return;
        int k = UrgentZone();
        if (k >= 0) { GoTo(k); sim.Say("ARIA: " + zones[k].title + ". This is where you are needed."); }
        else { GoTo((Current + 1) % zones.Count); sim.Say("ARIA: Nothing urgent. This is " + zones[Current].title + "."); }
        Buzz(0.5f, 0.08f);
    }

    public void Chime(float volume) { if (beeper) beeper.PlayOneShot(chime, volume); }

    public void Buzz(float amplitude, float seconds)
    {
        if (hapticMethod == null) return;
        foreach (var h in haptics) { try { hapticMethod.Invoke(h, new object[] { amplitude, seconds }); } catch { } }
    }

    // ------------------------------------------------------------------ travel and actions
    [NonSerialized] public Action beforeJump;      // filled in by the game: get off a rover (or out of anything you sit in) before a jump
    public void GoTo(int i)
    {
        if (zones.Count == 0 || !origin) return;
        if (beforeJump != null) { try { beforeJump(); } catch { } }
        Current = Mathf.Clamp(i, 0, zones.Count - 1);
        var z = zones[Current];
        var cc = origin.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        var fwd = Quaternion.Euler(0f, z.yaw, 0f) * Vector3.forward;
        if (HeadsetActive)
        {
            origin.MatchOriginUpCameraForward(Vector3.up, fwd);
            origin.MoveCameraToWorldLocation(z.position + Vector3.up * (Mathf.Max(0.2f, origin.CameraInOriginSpaceHeight) + 0.05f));
        }
        else
        {
            origin.transform.SetPositionAndRotation(z.position + Vector3.up * 0.05f, Quaternion.identity);
            var desk = GetComponent<HunchDesktopFallback>();
            if (desk) desk.SetLook(z.yaw, 0f);
        }
        if (cc) cc.enabled = true;
        Physics.SyncTransforms();
        NearZone = Current;
    }

    /// Called by the menu page. explore = walk around freely with the clock stopped and no alerts.
    public void StartMission(bool explore)
    {
        if (sim.phase != HabPhase.Briefing) return;
        if (explore) { sim.phase = HabPhase.Mission; sim.timeFrozen = true; sim.tutorialIndex = 0; sim.Say("ARIA: Free explore. No alerts and nothing wears out, but days and nights still pass. Press L to launch the rocket and N for the map."); }
        else sim.Interact("");
    }

    public int ZoneIndex(string id) { for (int i = 0; i < zones.Count; i++) if (zones[i].id == id) return i; return -1; }

    /// The zone the player should head for right now, or -1 when nothing is pending.
    public int UrgentZone()
    {
        if (sim.phase == HabPhase.Commissioning) return ZoneIndex(HabitatSim.AdjacencyOrder[sim.tutorialIndex]);
        if (sim.phase != HabPhase.Mission) return -1;
        HabAlert best = null;
        foreach (var a in sim.alerts) if (best == null || a.secondsLeft < best.secondsLeft) best = a;
        if (best == null) return -1;
        string target = best.zoneId;
        if (best.needsPart && sim.parts <= 0) target = sim.storageStock > 0 ? "storage" : "fab";
        return ZoneIndex(target);
    }

    /// G on the keyboard: go where you are needed. With no alert it takes you to the next zone of the commissioning walk, or simply the next zone, so the key always does something.
    public void GoToUrgent()
    {
        if (!started || menuOpen || zones.Count == 0 || sim.phase == HabPhase.Briefing || sim.phase == HabPhase.Report) return;
        if (UrgentZone() < 0 && sim.phase == HabPhase.Mission && UrgentExtra != null) { bool done = false; try { done = UrgentExtra(); } catch (System.Exception e) { HLog.W("Core", "mission step: " + e.Message); } if (done) return; }
        if (!TeleOk()) return;
        int k = UrgentZone();
        if (k < 0 && sim.phase == HabPhase.Commissioning) k = ZoneIndex(HabitatSim.AdjacencyOrder[Mathf.Min(sim.tutorialIndex, HabitatSim.AdjacencyOrder.Length - 1)]);
        if (k >= 0) { GoTo(k); sim.Say("ARIA: " + zones[k].title + ". This is where you are needed."); }
        else { GoTo((Current + 1) % zones.Count); sim.Say("ARIA: Nothing urgent. This is " + zones[Current].title + "."); }
    }

    public void DoInteract(bool fromOrb = false)
    {
        if (!started || Time.unscaledTime - lastInteract < 0.3f / Mathf.Max(1f, Time.timeScale)) return;
        lastInteract = Time.unscaledTime;
        if (sim.phase == HabPhase.Report || menuOpen) return;
        if (sim.phase == HabPhase.Briefing && externalMenu) return;      // the menu page starts the mission
        if (sim.phase != HabPhase.Briefing && NearZone < 0) { sim.Say("ARIA: Stand in a zone ring to act. Tab jumps to the next zone, G / X to the most urgent alert."); return; }
        string id = NearZone >= 0 ? zones[NearZone].id : "";
        int before = sim.alerts.Count; var phaseBefore = sim.phase; int tut = sim.tutorialIndex;
        sim.Interact(id);
        bool good = sim.alerts.Count < before || sim.tutorialIndex > tut || sim.phase != phaseBefore;
        if (beeper) beeper.PlayOneShot(chime, good ? 0.6f : 0.25f);
        Buzz(good ? 0.7f : 0.3f, good ? 0.25f : 0.08f);
    }

    void SetWrist(bool on)
    {
        wristMode = on && leftController != null && HeadsetActive;
        if (!hudRoot) return;
        if (wristMode)
        {
            hudRoot.SetParent(leftController, false);
            hudRoot.localPosition = new Vector3(0f, 0.11f, 0.05f); hudRoot.localRotation = Quaternion.Euler(40f, 0f, 0f); hudRoot.localScale = Vector3.one * 0.3f;
        }
        else { hudRoot.SetParent(null, true); hudRoot.localScale = Vector3.one * 0.8f; }
    }

    // ------------------------------------------------------------------ frame update
    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
            for (int i = 0; i < keys.Length; i++) if (!menuOpen && kb[keys[i]].wasPressedThisFrame && i < zones.Count && TeleOk()) GoTo(i);
        }
        if (!started) return;
        if (!Paused && !menuOpen) sim.Tick(Time.deltaTime * simSpeed);

        if (sim.newAlertFlag) { sim.newAlertFlag = false; if (beeper) beeper.PlayOneShot(beep, 0.8f); Buzz(1f, 0.4f); }
        if (sim.messageAge < 0.05f && sim.message != lastCalmMessage && (sim.message.Contains("Intervention delivered") || sim.message.Contains("VR nature session") || sim.message.Contains("watched Earth")))
        { lastCalmMessage = sim.message; breathLeft = 10f; }

        FindNearZone();
        UpdateMarkers();
        UpdateLights();
        UpdateCrew();
        UpdateBreathing();

        hudTimer -= Time.unscaledDeltaTime;
        if (hudTimer <= 0f) { hudTimer = 0.25f; if (hud) hud.text = CardText(); }
    }

    void FindNearZone()
    {
        if (!origin || !origin.Camera) return;
        Vector3 p = origin.Camera.transform.position; int best = -1; float bd = 3.2f;
        for (int i = 0; i < zones.Count; i++)
        {
            Vector3 d = zones[i].position - p; float dy = (p.y - standingEyeHeight) - zones[i].position.y; d.y = 0f;
            if (Mathf.Abs(dy) < 1.6f && d.magnitude < bd) { bd = d.magnitude; best = i; }
        }
        NearZone = best;
    }

    HabAlert AlertFor(string zone) { foreach (var a in sim.alerts) if (a.zoneId == zone) return a; return null; }

    void UpdateMarkers()
    {
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
        var cam = origin && origin.Camera ? origin.Camera.transform : null;
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (z.marker && z.marker.gameObject.activeSelf == hideMarkers) z.marker.gameObject.SetActive(!hideMarkers);
            if (hideMarkers) continue;
            var a = AlertFor(z.id);
            bool tutNext = sim.phase == HabPhase.Commissioning && z.id == HabitatSim.AdjacencyOrder[sim.tutorialIndex];
            float cd; sim.cooldown.TryGetValue(z.id, out cd);
            Color c = a != null ? cAlert * (0.6f + 0.6f * pulse) : (tutNext ? cNext * (0.6f + 0.6f * pulse) : (cd > 0f ? cWait : cIdle));
            if (i == NearZone) c *= 1.35f;
            if (z.ring) { var rc = c; rc.a = a != null || tutNext ? 0.7f : 0.38f; z.ring.material.SetColor("_BaseColor", rc); }
            if (z.orb)
            {
                var oc = c * 1.4f; oc.a = 0.85f; z.orb.material.SetColor("_BaseColor", oc);
                float s = 0.26f * (a != null || tutNext ? 1f + 0.25f * pulse : 1f); z.orb.transform.localScale = Vector3.one * s;
            }
            if (z.label)
            {
                var sb = new StringBuilder();
                sb.Append("<b>").Append(i + 1).Append("  ").Append(z.title).Append("</b>\n<size=55%>").Append(z.rubric).Append("</size>");
                if (a != null) sb.Append("\n<size=70%><color=#FF6A5A>! ").Append(a.title).Append("  ").Append(Mathf.Max(0, Mathf.CeilToInt(a.secondsLeft))).Append(" s</color></size>");
                else if (tutNext) sb.Append("\n<size=70%><color=#6CFF8A>Commission this zone next</color></size>");
                if (i == NearZone) { string p = sim.Prompt(z.id); if (p.Length > 0) sb.Append("\n<size=65%><color=#FFE08A>").Append(HeadsetActive ? "Trigger: " : "[E] ").Append(p).Append("</color></size>"); }
                z.label.text = sb.ToString();
                if (cam)
                {
                    Vector3 away = z.label.transform.position - cam.position; float flat = new Vector2(away.x, away.z).magnitude;
                    bool far = flat > 4.5f && !freezeHud;      // close up the name would fill your view; the dashboard already says where you are
                    if (z.label.gameObject.activeSelf != far) z.label.gameObject.SetActive(far);
                    away.y = 0f; if (away.sqrMagnitude > 0.01f) z.label.transform.rotation = Quaternion.LookRotation(away, Vector3.up);      // stays upright
                }
            }
        }
    }

    // adaptive lighting: follows an Earth day and night cycle, as the proposal asks
    void UpdateLights()
    {
        if (interiorLights == null || lightBase == null) return;
        float h = sim.Hour, f; Color tint;
        if (h >= 7f && h < 18f) { f = 1f; tint = Color.white; }
        else if (h >= 18f && h < 22f) { f = Mathf.Lerp(1f, 0.55f, (h - 18f) / 4f); tint = new Color(1f, 0.82f, 0.6f); }
        else if (h >= 22f || h < 5f) { f = 0.3f; tint = new Color(1f, 0.6f, 0.35f); }
        else { f = Mathf.Lerp(0.3f, 1f, (h - 5f) / 2f); tint = new Color(1f, 0.85f, 0.7f); }
        if (sim.phase != HabPhase.Mission) { f = 1f; tint = Color.white; }
        if (!sim.circadianOk) { f = 0.9f; tint = new Color(0.6f, 0.75f, 1f); }
        if (sim.Blackout) { f = 0.07f; tint = new Color(1f, 0.2f, 0.15f); }
        if (sim.stormActive) tint = Color.Lerp(tint, new Color(1f, 0.35f, 0.25f), 0.5f + 0.5f * Mathf.Sin(Time.time * 8f));
        for (int i = 0; i < interiorLights.Length; i++)
        {
            var l = interiorLights[i]; if (!l) continue;
            l.intensity = Mathf.Lerp(l.intensity, lightBase[i] * f * lightSwitch * (lightMul != null && i < lightMul.Length ? lightMul[i] : 1f), Time.deltaTime * 2f);
            l.color = Color.Lerp(l.color, lightColor[i] * tint, Time.deltaTime * 2f);
        }
    }

    /// where this crew member's day takes them right now (a zone index)
    public int CrewZoneIndex(int k) { return ZoneIndex(CrewZone(k)); }
    string CrewZone(int k)
    {
        foreach (var a in sim.alerts) if (a.crew == k && (a.type == "psych" || a.type == "medical")) return a.zoneId;
        if (sim.sheltered || (sim.stormActive && sim.sheltered)) return "shelter";
        float h = sim.Hour;
        if (sim.phase != HabPhase.Mission) return k == 0 ? "airlock" : (k == 1 ? "hygiene" : (k == 2 ? "work" : "storage"));
        if (h >= 22f || h < 6f) return "quarters";
        if ((h >= 12f && h < 13f) || (h >= 18f && h < 19f)) return "galley";
        if (h >= 16f && h < 17f) return "exercise";
        switch (k) { case 0: return "work"; case 1: return "medical"; case 2: return "eclss"; default: return h < 12f ? "greenhouse" : "storage"; }
    }

    void UpdateCrew()
    {
        if (crewAvatars == null) return;
        var cam = origin && origin.Camera ? origin.Camera.transform : null;
        for (int k = 0; k < crewAvatars.Length && k < sim.crew.Count; k++)
        {
            var av = crewAvatars[k]; if (!av) continue; var c = sim.crew[k];
            int zi = ZoneIndex(CrewZone(k));
            if (crewPose != null && crewPose(k, av)) { }
            else if (zi >= 0)
            {
                var z = zones[zi];
                float[] side = { 80f, -80f, 135f, -135f };
                Vector3 target = z.position + Quaternion.Euler(0f, z.yaw + side[k % 4], 0f) * Vector3.forward * 1.3f;
                if ((av.position - target).sqrMagnitude > 0.01f) { av.position = target; av.rotation = Quaternion.LookRotation((z.position - target).normalized, Vector3.up); }
            }
            Color col = c.Class == CrewClass.Neutral ? new Color(0.3f, 1f, 0.45f) : (c.Class == CrewClass.UnderlyingIssue ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.25f, 0.2f));
            if (crewStatus != null && k < crewStatus.Length && crewStatus[k])
            {
                float beat = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * Mathf.PI * c.HeartRate / 60f));       // pulses at the heart rate
                crewStatus[k].material.SetColor("_BaseColor", col * (1.2f * beat));
                crewStatus[k].transform.localScale = Vector3.one * (0.09f + 0.035f * beat);
            }
            if (crewTags != null && k < crewTags.Length && crewTags[k] && crewTags[k].gameObject.activeSelf == hideCrewTags) crewTags[k].gameObject.SetActive(!hideCrewTags);
            if (crewTags != null && k < crewTags.Length && crewTags[k] && !hideCrewTags)
            {
                string hex = ColorUtility.ToHtmlStringRGB(col);
                crewTags[k].text = "<b>" + c.name + "</b>\n<size=70%>" + c.role + "</size>\n<size=80%><color=#" + hex + ">" + HabitatSim.ClassName(c.Class) + "</color>\nHR " + c.HeartRate + "   Stress " + Mathf.RoundToInt(c.stress) + "\nMood " + Mathf.RoundToInt(c.mood) + "   Sleep " + Mathf.RoundToInt(c.sleep) + "</size>";
                if (cam) crewTags[k].transform.rotation = Quaternion.LookRotation(crewTags[k].transform.position - cam.position, Vector3.up);
            }
        }
    }

    // guided breathing shown after a calming intervention
    void UpdateBreathing()
    {
        if (!breathRing) return;
        if (breathLeft <= 0f) { if (breathRing.gameObject.activeSelf) breathRing.gameObject.SetActive(false); return; }
        breathLeft -= Time.unscaledDeltaTime;
        if (!breathRing.gameObject.activeSelf) breathRing.gameObject.SetActive(true);
        var cam = origin.Camera.transform; var flat = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized; if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
        breathRing.position = Vector3.Lerp(breathRing.position, cam.position + flat * 1.3f + Vector3.down * 0.1f, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
        breathRing.rotation = Quaternion.LookRotation(breathRing.position - cam.position, Vector3.up);
        float ph = (Time.unscaledTime % 5f) / 5f; bool inhale = ph < 0.5f; float s = inhale ? ph * 2f : 2f - ph * 2f;
        breathRing.localScale = Vector3.one * Mathf.Lerp(0.22f, 0.5f, Mathf.SmoothStep(0f, 1f, s));
        if (breathText) breathText.text = inhale ? "Breathe in" : "Breathe out";
    }

    void LateUpdate()
    {
        if (!hudRoot || !origin || !origin.Camera) return;
        bool show = HeadsetActive && !HunchIntro.Active;
        if (hudRoot.gameObject.activeSelf != show) hudRoot.gameObject.SetActive(show);
        if (!show || wristMode) return;
        var cam = origin.Camera.transform;
        var flat = Vector3.ProjectOnPlane(cam.forward, Vector3.up); if (flat.sqrMagnitude < 0.01f) flat = cam.up; flat.Normalize();
        bool centre = sim.phase == HabPhase.Briefing || sim.phase == HabPhase.Report || showHelp || menuOpen;
        var right = Vector3.Cross(Vector3.up, flat);
        var target = cam.position + flat * (centre ? 1.6f : 1.5f) + (centre ? Vector3.down * 0.05f : Vector3.down * 0.55f - right * 0.55f);
        hudRoot.localScale = Vector3.one * (centre ? 1.25f : 0.8f);
        if (freezeHud)
        {
            // a menu is open: the card stays where it is until you turn well away from it or move off
            Vector3 to = hudRoot.position - cam.position; float ang = Vector3.Angle(flat, Vector3.ProjectOnPlane(to, Vector3.up));
            if (hudPlaced && to.magnitude < 3.2f && to.magnitude > 0.6f && ang < 60f) { hudRoot.rotation = Quaternion.LookRotation(to, Vector3.up); return; }
            hudRoot.position = target; hudPlaced = true; hudYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg; hudYawSet = true;
        }
        else
        {
            hudPlaced = false;
            // The card moves with your head straight away (so it can never lag into your body when you walk)
            // and only swings round after you have turned well away from it, so it is calm to read.
            float viewYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            bool far = (hudRoot.position - target).sqrMagnitude > 16f;
            if (!hudYawSet || far) { hudYaw = viewYaw; hudYawSet = true; hudTurning = false; }
            float diff = Mathf.Abs(Mathf.DeltaAngle(hudYaw, viewYaw));
            if (diff > (centre ? 14f : 30f)) hudTurning = true; else if (diff < 2f) hudTurning = false;
            if (hudTurning) hudYaw = Mathf.LerpAngle(hudYaw, viewYaw, 1f - Mathf.Exp(-5f * dt));
            var f = Quaternion.Euler(0f, hudYaw, 0f) * Vector3.forward; var r = Vector3.Cross(Vector3.up, f);
            target = cam.position + f * (centre ? 1.6f : 1.5f) + (centre ? Vector3.down * 0.05f : Vector3.down * 0.55f - r * 0.55f);
            hudRoot.position = far ? target : Vector3.Lerp(hudRoot.position, target, 1f - Mathf.Exp(-14f * dt));
            Vector3 gap = hudRoot.position - cam.position; gap.y = 0f;
            if (gap.sqrMagnitude < 1f) hudRoot.position = target;      // never closer than a metre
        }
        hudRoot.rotation = Quaternion.LookRotation(hudRoot.position - cam.position, Vector3.up);
    }

    // ------------------------------------------------------------------ text
    static string Col(float v, bool lowIsBad = true)
    {
        bool bad = lowIsBad ? v < 35f : v > 65f, warn = lowIsBad ? v < 60f : v > 40f;
        return bad ? "#FF6A5A" : (warn ? "#FFD25A" : "#7CFC9A");
    }
    string Status()
    {
        if (sim.Blackout) return "BLACKOUT";
        if (sim.stormActive) return sim.sheltered ? "SOLAR STORM - sheltered" : "SOLAR STORM - EXPOSED";
        if (sim.LunarNight) return sim.fissionOn ? "Lunar night - fission on" : "LUNAR NIGHT - on storage";
        return sim.fissionOn ? "Sunlit - fission on" : "Sunlit";
    }
    /// Project ARES missions (HunchFrontier26): the mission step shown on the objective bar, and G doing that step when no alarm waits
    public static System.Func<string> ObjectiveExtra;
    public static System.Func<bool> UrgentExtra;
    public string Objective()
    {
        if (sim.phase == HabPhase.Mission && ObjectiveExtra != null) { string ex = null; try { ex = ObjectiveExtra(); } catch { } if (!string.IsNullOrEmpty(ex)) return ex; }
        return ObjectiveBase();
    }
    public string ObjectiveBase()
    {
        switch (sim.phase)
        {
            case HabPhase.Briefing: return "Press Interact to begin";
            case HabPhase.Commissioning: return "Commissioning walk " + sim.tutorialIndex + "/8: go to " + sim.zoneTitle(HabitatSim.AdjacencyOrder[sim.tutorialIndex]);
            case HabPhase.Report: return "Mission report";
        }
        HabAlert best = null; foreach (var a in sim.alerts) if (best == null || a.secondsLeft < best.secondsLeft) best = a;
        if (best == null) return "All clear. Look after the crew: meals, exercise, plants, calls home.";
        return best.title + " -> " + sim.zoneTitle(best.zoneId) + " (" + Mathf.Max(0, Mathf.CeilToInt(best.secondsLeft)) + " s)";
    }

    const string Brief = "<b>LUNAR HABITAT COMMANDER</b>\n<size=75%>NASA HUNCH - Lunar Smart Adaptive Habitat with AI Psychological Monitoring</size>\n\nYou run a south-pole habitat with a crew of four for 30 days. ARIA, the habitat AI, watches each astronaut's mood, stress, sleep and heart rate and tells you when someone needs help.\n\n1. Commission the 8 NASA zones in adjacency order: Airlock, Hygiene, Crew Quarters, Galley, Workstations, Storage, ECLSS, Medical.\n2. Answer ARIA's alerts in the right zone before the timer runs out.\n3. Between alerts, look after the crew and the systems.\n\nAt the end you are scored against the 7 rubric areas.";
    const string VrKeys = "<b>VR: every button has one job</b>\nRIGHT hand   Trigger: select / use   Grip: teleport (hold to aim, let go)   A: jump   B: next tool   Stick: turn (click it: cameras)\nLEFT hand   Stick: walk (click it to run)   Trigger: select too   Grip: grab   X: go to the next job   Y: menu\nPoint the laser at a glowing orb, a button or a repair panel and pull the trigger. Two rows of big buttons under this card do everything else: maps, cameras, meeting, health, reading, music, autopilot, jetpack, talk, eat.";
    const string DeskKeys = "<b>Keys</b>  E act   Tab / Q next / previous zone   1-0 zones   G most urgent alert   WASD + mouse move   click = act\nPush against any door or wall for a moment to pass through   PageUp / PageDown lift to the next floor   F fly   P pause   M music   H help\nT talk to the nearest astronaut or to ARIA (type or use your voice)   N map   L launch the rocket   Esc menu and options   F1 photo mode\nWalk into the gold stars to collect them   K kid mode\nR change tool (zapper, freeze ray, drill, block builder)   click = use the tool   I backpack   O lights   Esc > Explorer: mining field, upgrades, power\nY maps and mini-games   Z security cameras   F2 crew health   F3 things to read   F4 team meeting   F5 music style   F6 autopilot (the AI plays)";

    public string KidLine() { return "Kid mode is " + (sim.kidMode ? "ON: more time on every alert, gentler rules, nobody gets hurt." : "OFF: standard rules.") + "  Press K to switch."; }

    public string ReportText()
    {
        var s = sim.Scores(); float o = sim.Overall(); var sb = new StringBuilder();
        sb.Append("<b>MISSION REPORT</b>   ").Append(sim.missionFailed ? "<color=#FF6A5A>Mission ended early: " + sim.failReason + "</color>" : "30 days completed").Append("\n");
        sb.Append("<b>Rubric score ").Append(Mathf.RoundToInt(o)).Append(" / 100   ").Append(HabitatSim.Grade(o)).Append("</b>\n\n");
        for (int i = 0; i < s.Length; i++) sb.Append("<color=").Append(Col(s[i])).Append(">").Append(Mathf.RoundToInt(s[i]).ToString().PadLeft(3)).Append("</color>  ").Append(HabitatSim.Criteria[i]).Append("\n");
        sb.Append("\n<b>Crew health reports</b> (alerts handled ").Append(sim.Handled).Append(" of ").Append(sim.Raised).Append(", radiation ").Append(sim.dose.ToString("F1")).Append(" mSv)\n");
        foreach (var c in sim.crew) sb.Append(c.name).Append(": ").Append(HabitatSim.ClassName(c.Class)).Append(", wellbeing ").Append(Mathf.RoundToInt(c.Wellbeing)).Append(", health ").Append(Mathf.RoundToInt(c.health)).Append("\n");
        if (extraReport.Length > 0) sb.Append("\n").Append(extraReport);
        return sb.ToString();
    }

    public string CardText()
    {
        if (!string.IsNullOrEmpty(cardOverride)) return cardOverride;
        if (sim.phase == HabPhase.Briefing) return Brief + "\n\n" + KidLine() + "\n\n<size=70%>" + VrKeys + "</size>\n\n<color=#FFE08A>Pull the trigger to begin</color>";
        if (sim.phase == HabPhase.Report) return ReportText();
        if (simpleHud && simpleCard != null && !showHelp) return simpleCard();
        var sb = new StringBuilder();
        if (showHelp) sb.Append("<size=70%>").Append(VrKeys).Append("</size>\n\n");
        sb.Append("<b>DAY ").Append(sim.Day).Append(" / 30   ").Append(Mathf.FloorToInt(sim.Hour).ToString("00")).Append(":00</b>   ").Append(sim.phase == HabPhase.Mission ? Status() : "Commissioning").Append("\n");
        sb.Append("<color=#FFE08A>").Append(Objective()).Append("</color>\n");
        sb.Append("O2 <color=").Append(Col(sim.oxygen)).Append(">").Append(Mathf.RoundToInt(sim.oxygen)).Append("</color>  Water <color=").Append(Col(sim.water)).Append(">").Append(Mathf.RoundToInt(sim.water)).Append("</color>  Food <color=").Append(Col(sim.food)).Append(">").Append(Mathf.RoundToInt(sim.food)).Append("</color>  Power <color=").Append(Col(sim.battery)).Append(">").Append(Mathf.RoundToInt(sim.battery)).Append("</color>  Dust <color=").Append(Col(sim.dust, false)).Append(">").Append(Mathf.RoundToInt(sim.dust)).Append("</color>  Parts ").Append(sim.parts).Append("\n");
        foreach (var c in sim.crew) sb.Append("<color=").Append(Col(c.Wellbeing)).Append(">").Append(c.name).Append("  ").Append(HabitatSim.ClassName(c.Class)).Append("</color>  HR ").Append(c.HeartRate).Append("  stress ").Append(Mathf.RoundToInt(c.stress)).Append("  mood ").Append(Mathf.RoundToInt(c.mood)).Append("\n");
        if (extraLine.Length > 0) sb.Append("<color=#FFE08A>").Append(extraLine).Append("</color>\n");
        if (navLine.Length > 0) sb.Append("<color=#7CFC9A>").Append(navLine).Append("</color>\n");
        if (toolLine.Length > 0) sb.Append("<size=80%><color=#E8C8FF>").Append(toolLine).Append("</color></size>\n");
        if (advisorLine.Length > 0) sb.Append("<size=85%><color=#B8F5C8>").Append(advisorLine).Append("</color></size>\n");
        if (sim.messageAge < 9f) sb.Append("<size=85%><color=#9AD8FF>").Append(sim.message).Append("</color></size>");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ desktop screen
    static string Strip(string s) { return System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", ""); }
    void Bar(Rect r, float frac, Color c)
    {
        HunchUi.Bar(r, frac, c, new Color(1f, 1f, 1f, 0.14f));      // rounded, friendlier than a sharp rectangle
    }
    static Color C(float v, bool lowIsBad = true)
    {
        bool bad = lowIsBad ? v < 35f : v > 65f, warn = lowIsBad ? v < 60f : v > 40f;
        return bad ? new Color(1f, 0.42f, 0.35f) : (warn ? new Color(1f, 0.82f, 0.35f) : new Color(0.49f, 0.99f, 0.6f));
    }
    void Panel(Rect r) { HunchUi.Round(r, new Color(0.04f, 0.08f, 0.17f, 0.82f)); }

    void OnGUI()
    {
        if (HeadsetActive || !started || hideHud || menuOpen || uiHasMouse) return;
        if (sim.phase == HabPhase.Briefing && externalMenu) return;
        float u = Screen.height / 1080f;
        if (sTitle == null || Mathf.Abs(sTitle.fontSize - HunchLook.FsF(26f * u)) > 1f)
        {
            white = Texture2D.whiteTexture;
            sTitle = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(26f * u), fontStyle = FontStyle.Bold, richText = true, wordWrap = true };
            sText = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(19f * u), richText = true, wordWrap = true };
            sSmall = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(15f * u), richText = true, wordWrap = true };
            sBig = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(22f * u), richText = true, wordWrap = true };
            foreach (var st in new[] { sTitle, sText, sSmall, sBig }) st.normal.textColor = Color.white;
        }
        float pad = 14f * u, W = Screen.width, H = Screen.height;

        if (sim.phase == HabPhase.Briefing || sim.phase == HabPhase.Report)
        {
            var r = new Rect(W * 0.5f - 480f * u, H * 0.5f - 330f * u, 960f * u, 660f * u); Panel(r);
            string t = sim.phase == HabPhase.Briefing ? Brief + "\n\n" + KidLine() + "\n\n" + DeskKeys + "\n\n<color=#FFE08A>Press E to begin</color>" : ReportText() + "\n<color=#FFE08A>Alt+F4 to close. Run again to beat your score.</color>";
            GUI.Label(new Rect(r.x + 28f * u, r.y + 22f * u, r.width - 56f * u, r.height - 44f * u), t.Replace("<size=75%>", "<size=" + sSmall.fontSize + ">"), sBig);
            return;
        }
        if (simpleHud) return;      // the simple screen is drawn by HunchFrontier

        // ---- left: mission, systems, crew
        var L = new Rect(pad, pad, 470f * u, 640f * u); Panel(L);
        float x = L.x + pad, y = L.y + pad * 0.7f, w = L.width - 2f * pad, lh = 26f * u;
        GUI.Label(new Rect(x, y, w, 34f * u), "DAY " + sim.Day + " / 30    " + Mathf.FloorToInt(sim.Hour).ToString("00") + ":00", sTitle); y += 34f * u;
        GUI.Label(new Rect(x, y, w, lh), sim.phase == HabPhase.Mission ? Status() : "Commissioning walk", sText); y += lh;
        GUI.Label(new Rect(x, y, w, lh * 2.3f), "<color=#FFE08A>" + Objective() + "</color>", sText); y += lh * 2.4f;
        string[] names = { "Oxygen", "Water", "Food", "Power", "Dust", "Arrays" }; float[] vals = { sim.oxygen, sim.water, sim.food, sim.battery, sim.dust, sim.arrays };
        for (int i = 0; i < names.Length; i++)
        {
            GUI.Label(new Rect(x, y, 90f * u, lh), names[i], sText);
            Bar(new Rect(x + 96f * u, y + 7f * u, w - 160f * u, 13f * u), vals[i] / 100f, C(vals[i], i != 4));
            GUI.Label(new Rect(x + w - 56f * u, y, 60f * u, lh), Mathf.RoundToInt(vals[i]) + "%", sText); y += lh;
        }
        GUI.Label(new Rect(x, y, w, lh), "Radiation " + sim.dose.ToString("F1") + " mSv     Spare parts " + sim.parts, sText); y += lh * 1.3f;
        GUI.Label(new Rect(x, y, w, lh), "<b>ARIA crew monitor</b>", sText); y += lh;
        foreach (var c in sim.crew)
        {
            var cc = C(c.Wellbeing); string hex = ColorUtility.ToHtmlStringRGB(cc);
            GUI.Label(new Rect(x, y, w, lh), "<color=#" + hex + "><b>" + c.name + "</b>  " + HabitatSim.ClassName(c.Class) + "</color>", sText); y += lh * 0.95f;
            Bar(new Rect(x, y + 5f * u, w, 8f * u), c.Wellbeing / 100f, cc); y += 15f * u;
            GUI.Label(new Rect(x, y, w, lh), "HR " + c.HeartRate + "   stress " + Mathf.RoundToInt(c.stress) + "   mood " + Mathf.RoundToInt(c.mood) + "   sleep " + Mathf.RoundToInt(c.sleep) + "   health " + Mathf.RoundToInt(c.health), sSmall); y += lh * 1.05f;
        }

        // ---- right: alerts and live rubric score
        var R = new Rect(W - 470f * u - pad, pad, 470f * u, 150f * u + 78f * u * Mathf.Max(1, sim.alerts.Count)); Panel(R);
        x = R.x + pad; y = R.y + pad * 0.7f; w = R.width - 2f * pad;
        GUI.Label(new Rect(x, y, w, 34f * u), "ALERTS", sTitle);
        GUI.Label(new Rect(x + 170f * u, y + 6f * u, w - 170f * u, lh), "Rubric score so far: " + Mathf.RoundToInt(sim.Overall()), sText); y += 38f * u;
        if (sim.alerts.Count == 0) { GUI.Label(new Rect(x, y, w, lh), sim.phase == HabPhase.Commissioning ? "None during commissioning" : "None. All systems nominal.", sText); y += lh; }
        foreach (var a in sim.alerts)
        {
            int zi = ZoneIndex(a.zoneId);
            GUI.Label(new Rect(x, y, w, lh), "<color=#FF7A6A><b>! " + a.title + "</b></color>", sText); y += lh;
            GUI.Label(new Rect(x, y, w, lh), "Go to " + sim.zoneTitle(a.zoneId) + (zi >= 0 && zi < 10 ? "  [" + ((zi + 1) % 10) + "]" : "") + (a.needsPart ? "   needs a spare part" : ""), sSmall); y += lh * 0.85f;
            Bar(new Rect(x, y + 5f * u, w, 8f * u), Mathf.Clamp01(a.secondsLeft / a.total), a.overdue ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.75f, 0.3f)); y += 24f * u;
        }
        GUI.Label(new Rect(x, R.yMax - 62f * u, w, 60f * u), "G jumps to the most urgent alert.  Tab next zone.  H help.", sSmall);

        // ---- bottom: where you are, what E does, what ARIA said
        float bx = Mathf.Max(W * 0.5f - 520f * u, 428f * u), bw = Mathf.Min(1040f * u, W - bx - 14f * u);      // stays clear of the stars panel on narrow screens
        var B = new Rect(bx, H - 190f * u, bw, 176f * u); Panel(B);
        string where = NearZone >= 0 ? "<b>" + (NearZone + 1) + "  " + zones[NearZone].title + "</b>   <size=" + sSmall.fontSize + ">" + zones[NearZone].rubric + " - " + zones[NearZone].description + "</size>" : "<b>Between zones</b>   <size=" + sSmall.fontSize + ">walk into a glowing ring, or press Tab</size>";
        GUI.Label(new Rect(B.x + pad, B.y + 8f * u, B.width - 2f * pad, 56f * u), where, sText);
        if (NearZone >= 0) GUI.Label(new Rect(B.x + pad, B.y + 66f * u, B.width - 2f * pad, 34f * u), "<color=#FFE08A>[E]  " + sim.Prompt(zones[NearZone].id) + "</color>", sBig);
        if (sim.messageAge < 10f) GUI.Label(new Rect(B.x + pad, B.y + 104f * u, B.width - 2f * pad, 66f * u), "<color=#9AD8FF>" + sim.message + "</color>", sText);
        if (Paused) GUI.Label(new Rect(W * 0.5f - 80f * u, H * 0.4f, 300f * u, 60f * u), "PAUSED (P)", sTitle);
        if (showHelp) { var hr = new Rect(W * 0.5f - 520f * u, H * 0.5f - 60f * u, 1040f * u, 110f * u); Panel(hr); GUI.Label(new Rect(hr.x + pad, hr.y + pad, hr.width - 2f * pad, hr.height), DeskKeys, sText); }
    }
}
