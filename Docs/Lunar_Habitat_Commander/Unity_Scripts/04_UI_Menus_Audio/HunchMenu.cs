// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/04_UI_Menus_Audio/HunchMenu.cs
//  Section : 04_UI_Menus_Audio
//  Folder  : Menus, rounded UI, story intro, Star Store, music, voices (TTS/commands) and picture
//            quality.
//  Purpose : Menu page, options (including everything you can hide), and talking: - Main menu at
//            the start, pause menu on Esc (VR: left menu button)
//  Contains: HunchMenu, Page, Item, ExtraPage, VrRow
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
#pragma warning disable 0414, 0219
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// Menu page, options (including everything you can hide), and talking:
///  - Main menu at the start, pause menu on Esc (VR: left menu button)
///  - Options page: show / hide the dashboard, zone markers, crew tags, mood faces, robot buddy; switch effects, sounds, music, voices
///  - Talk (T, VR: Y button): chat with the nearest astronaut or with ARIA using quick replies, typed messages, or your voice
///  - F1: photo mode (hides everything on screen)
/// Works with mouse, keyboard, gamepad and VR controllers. In VR the pages are drawn on the dashboard card.
public class HunchMenu : MonoBehaviour
{
    enum Page { None, Main, Pause, Options, Help, Talk, Extra }
    /// style: 0 normal, 1 STAR STORE (gold and purple shimmer), 2 owned store item (green), 3 a quiet line (credits, notes)
    class Item { public string label, hint; public Action act; public int style; }

    /// Other scripts add their own pages to the pause menu with this (see HunchFrontier).
    public class ExtraPage { public string title = "", label = "", hint = ""; public ExtraPage parent; public Action<HunchMenu> build; public Action direct; }
    public readonly List<ExtraPage> extraPages = new List<ExtraPage>(); ExtraPage extra;
    /// when set, a page is listed only if this says yes (NASA HUNCH mode hides the game pages without removing them)
    public Predicate<ExtraPage> extraShow; public bool rocketHidden;
    public void AddItem(string label, string hint, Action act) { Add(label, hint, act); }
    public void AddItem(string label, string hint, Action act, int style) { Add(label, hint, act); items[items.Count - 1].style = style; }

    /// the STAR STORE page (HunchFrontier builds it): a button near the top of the main menu and the pause menu
    public ExtraPage storePage; public Func<string> storeLabel;
    public void SetStore(ExtraPage p, Func<string> label) { storePage = p; storeLabel = label; if (page == Page.Main || page == Page.Pause) Build(); }
    /// redraws the page that is open (used when a purchase or a restore finishes while the menu is up)
    public void RefreshOpen() { if (page == Page.Main || page == Page.Pause || page == Page.Extra || page == Page.Options) Build(); }
    public bool IsOpen { get { return page != Page.None; } }
    public const string Copyright = "© 2026 Jaineil R.";
    public const string Thanks = "Thanks to Angel C. and Farhan K.";
    void AddStoreRow()
    {
        if (storePage == null) return;
        string lab = storeLabel != null ? storeLabel() : "<b>STAR STORE</b>";
        Add(lab, "Costumes, Lumi outfits and the Supporter Pack. One-time purchases through the Meta Horizon Store: no ads, no coins to buy, nothing that runs out. Everything you buy stays on your Meta account, so Restart, a reset or a reinstall never loses it.", () => OpenExtra(storePage));
        items[items.Count - 1].style = 1;
    }
    public void OpenExtra(ExtraPage p) { if (p == null) return; extra = p; Open(Page.Extra); }
    public void Rebuild() { Build(); }
    // VR buttons (HunchVr): Y opens the menu or goes back, A chooses the highlighted line, B goes back, a stick moves the highlight
    public void VrMenuButton() { if (HunchIntro.Active) return; if (page == Page.None) { if (!closing) Open(sim.phase == HabPhase.Briefing ? Page.Main : Page.Pause); } else if (Time.unscaledTime - openedAt > 0.25f) Back(); }
    public void VrSelect() { if (page == Page.None || Time.unscaledTime - openedAt < 0.3f || Time.unscaledTime - vrClickAt < 0.3f) return; vrClickAt = Time.unscaledTime; Select(); game.Buzz(0.4f, 0.05f); }
    public void VrBack() { if (page != Page.None && Time.unscaledTime - openedAt > 0.25f) Back(); }
    public void VrMove(int d) { if (page != Page.None) Move(d); }
    public void OpenPause() { Open(sim.phase == HabPhase.Briefing ? Page.Main : Page.Pause); }

    public string probeText = "";                                    // used by the development test helper

    HabitatGame game; HabitatSim sim; HunchKidFeatures kid; HunchWorldFX fx; HunchAria aria; HunchVoice voice; HunchMusic music; HunchDesktopFallback desk; HunchNavAssist nav; HunchGuide guide; HunchLook look;
    Page page = Page.None; readonly List<Item> items = new List<Item>(); int sel; bool closing, photo, setUp;
    int talkWith = -1; readonly List<string> log = new List<string>(); string typed = ""; float openedAt, backspaceAt; string lastSpoken = "", cardShown = "";
    InputAction talkAction; Keyboard hookedKb; bool orbiting; Vector2 lastMouse;
    GUIStyle sTitle, sItem, sText, sSmall; Texture2D white;

    // options (saved between runs)
    bool optHud = true, optMarkers = true, optTags = true, optFaces = true, optBuddy = true, optCamFx = true, optParticles = true, optDoors = true,
         optSfx = true, optSilence = true, optMusic = true, optVoice = true, optListen = false, optAuto = false, optArrows = true, optMap = true, optCine = true;

    static readonly string[][] ZoneWords = {
        new[] { "airlock", "airlock", "air lock" }, new[] { "hygiene", "hygiene", "bathroom", "showers" }, new[] { "quarters", "crew quarters", "quarters", "bedroom", "sleep pods" },
        new[] { "galley", "galley", "kitchen" }, new[] { "work", "workstations", "workstation", "work station", "ai hub" }, new[] { "storage", "storage" },
        new[] { "eclss", "life support" }, new[] { "medical", "medical", "med bay", "doctor", "first aid" }, new[] { "sanctuary", "sanctuary", "relaxation room" },
        new[] { "exercise", "exercise", "gym" }, new[] { "greenhouse", "greenhouse", "garden", "plants" }, new[] { "power", "power plant", "power station", "reactor" },
        new[] { "fab", "fabrication", "workshop", "printer" }, new[] { "control", "mission control", "control room" }, new[] { "shelter", "storm shelter", "shelter" },
        new[] { "observation", "observation", "observatory", "earth view" } };

    // ------------------------------------------------------------------ attach to the game in every scene load
    static bool SelfTest() { return Array.IndexOf(Environment.GetCommandLineArgs(), "-hunchSelfTest") >= 0; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; }
    static void OnScene(Scene s, LoadSceneMode m) { Attach(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { Attach(); }

    static void Attach()
    {
        var g = FindFirstObjectByType<HabitatGame>(); if (!g) return;
        if (!g.GetComponent<HunchWorldFX>()) g.gameObject.AddComponent<HunchWorldFX>();
        if (!g.GetComponent<HunchAria>()) g.gameObject.AddComponent<HunchAria>();
        if (!g.GetComponent<HunchVoice>()) g.gameObject.AddComponent<HunchVoice>();
        if (!g.GetComponent<HunchGuide>()) g.gameObject.AddComponent<HunchGuide>();
        if (!g.GetComponent<HunchLook>()) g.gameObject.AddComponent<HunchLook>();
        if (!SelfTest() && !g.GetComponent<HunchMenu>()) g.gameObject.AddComponent<HunchMenu>();
    }

    void Awake()
    {
        game = GetComponent<HabitatGame>(); sim = game.sim;
        game.externalMenu = true; game.inputOverride = Take;
        Time.timeScale = 1f;
        LoadPrefs();
        Open(Page.Main);
    }

    void OnEnable()
    {
        talkAction = new InputAction("talk", InputActionType.Button);
        talkAction.AddBinding("<Keyboard>/t");      // VR: Y is the menu button now; talking is a menu choice      // Y. (A is the rig's jump button, so it must not open anything.) talkAction.AddBinding("<Gamepad>/buttonEast");
        talkAction.performed += _ => { if (page == Page.None && sim.phase != HabPhase.Briefing) OpenTalk(); };
        talkAction.Enable();
    }
    void OnDisable()
    {
        if (talkAction != null) { talkAction.Disable(); talkAction.Dispose(); talkAction = null; }
        if (hookedKb != null) { hookedKb.onTextInput -= OnText; hookedKb = null; }
    }

    void Start() { Invoke(nameof(Setup), 0.35f); }

    void Setup()
    {
        kid = GetComponent<HunchKidFeatures>(); fx = GetComponent<HunchWorldFX>(); aria = GetComponent<HunchAria>(); voice = GetComponent<HunchVoice>();
        desk = GetComponent<HunchDesktopFallback>(); nav = GetComponent<HunchNavAssist>(); music = FindFirstObjectByType<HunchMusic>();
        guide = GetComponent<HunchGuide>(); look = GetComponent<HunchLook>();
        if (voice)
        {
            voice.onCommand = OnVoice; voice.onDictation = OnDictation;
            string[][] simple = {
                new[] { "next", "next zone", "next room" }, new[] { "prev", "previous zone", "go back" }, new[] { "urgent", "take me there", "most urgent", "where am i needed" },
                new[] { "act", "do it", "activate", "go ahead", "fix it", "help them" }, new[] { "status", "status report", "aria report" }, new[] { "advice", "what should i do", "what now", "help me aria" },
                new[] { "crew", "how is the crew", "crew report" }, new[] { "fact", "tell me a fact", "moon fact" }, new[] { "joke", "tell me a joke" }, new[] { "zone", "where am i", "what is this room" },
                new[] { "five", "high five" }, new[] { "talk", "hello", "hello aria", "hi there" }, new[] { "feel", "how are you", "how are you feeling" }, new[] { "need", "what would help", "what do you need" },
                new[] { "job", "tell me about your job", "what is your job" }, new[] { "bye", "goodbye", "close menu", "resume" }, new[] { "menu", "open menu", "open the menu" },
                new[] { "start", "start mission", "start the mission" }, new[] { "explore", "free explore" }, new[] { "up", "lift up", "go upstairs" }, new[] { "down", "lift down", "go downstairs" },
                new[] { "hide", "hide everything", "photo mode" }, new[] { "show", "show everything" }, new[] { "music", "toggle music" }, new[] { "pause", "pause the game" },
                new[] { "launch", "launch the rocket", "launch rocket" }, new[] { "map", "open the map", "show the map", "close the map" } };
            foreach (var s in simple) for (int i = 1; i < s.Length; i++) voice.AddPhrase(s[i], s[0]);
            foreach (var z in ZoneWords) for (int i = 1; i < z.Length; i++) { voice.AddPhrase("go to the " + z[i], "goto:" + z[0]); voice.AddPhrase("go to " + z[i], "goto:" + z[0]); voice.AddPhrase("take me to the " + z[i], "goto:" + z[0]); voice.AddPhrase("where is the " + z[i], "nav:" + z[0]); voice.AddPhrase("guide me to the " + z[i], "nav:" + z[0]); }
        }
        setUp = true; Apply();
    }

    // ------------------------------------------------------------------ options
    static bool P(string k, bool d) { return PlayerPrefs.GetInt("hunch_" + k, d ? 1 : 0) == 1; }
    void LoadPrefs()
    {
        optHud = P("hud", true); optMarkers = P("markers", true); optTags = P("tags", true); optFaces = P("faces", true); optBuddy = P("buddy", true); optCamFx = P("camfx", true);
        optParticles = P("particles", true); optDoors = P("doors", true); optSfx = P("sfx", true); optSilence = P("silence", true); optMusic = P("music", true); optVoice = P("voice", true);
        optListen = P("listen", false); optAuto = P("auto", false); sim.kidMode = P("kid", true);
        optArrows = P("arrows", true); optMap = P("map", true); optCine = P("cine", true);
    }
    void SavePrefs()
    {
        string[] k = { "hud", "markers", "tags", "faces", "buddy", "camfx", "particles", "doors", "sfx", "silence", "music", "voice", "listen", "auto", "kid", "arrows", "map", "cine" };
        bool[] v = { optHud, optMarkers, optTags, optFaces, optBuddy, optCamFx, optParticles, optDoors, optSfx, optSilence, optMusic, optVoice, optListen, optAuto, sim.kidMode, optArrows, optMap, optCine };
        for (int i = 0; i < k.Length; i++) PlayerPrefs.SetInt("hunch_" + k[i], v[i] ? 1 : 0);
        PlayerPrefs.Save();
    }

    void Apply()
    {
        game.hideHud = !optHud || photo; game.hideMarkers = !optMarkers || photo; game.hideCrewTags = !optTags || photo;
        if (kid) { kid.hideFaces = !optFaces || photo; kid.hideBuddy = !optBuddy || photo; }
        if (fx) { fx.cameraFx = optCamFx; fx.doorsOn = optDoors; fx.sfxOn = optSfx; fx.realisticSilence = optSilence; }
        HunchFx.enabled = optParticles;
        if (music && music.On != optMusic) music.On = optMusic;
        if (aria) aria.autoCare = optAuto;
        if (guide) { guide.arrowsOn = optArrows && !photo; guide.mapOn = optMap && !photo; }
        if (look) look.cinematic = optCine;
        if (voice)
        {
            voice.speakOn = optVoice; if (!optVoice) voice.Hush();
            if (optListen && !voice.Listening) { if (!voice.StartListening()) optListen = false; }
            else if (!optListen && voice.Listening) voice.StopListening();
        }
    }

    void Toggle(ref bool b) { b = !b; Apply(); SavePrefs(); Build(); }
    static string OnOff(bool b) { return b ? "ON" : "OFF"; }
    static string Shown(bool b) { return b ? "shown" : "hidden"; }

    // ------------------------------------------------------------------ pages
    void Add(string label, string hint, Action act) { items.Add(new Item { label = label, hint = hint, act = act }); }

    void Build()
    {
        items.Clear();
        string mic = voice && voice.ListenProblem.Length > 0 ? " Problem: " + voice.ListenProblem + "." : "";
        switch (page)
        {
            case Page.Main:
                Add("Survival mission", "The real game. Commission the 8 NASA zones in order, then keep four astronauts and the habitat healthy for 30 days. You have health and hunger, aliens can bump you, and panels break and need fixing. ARIA, the habitat AI, guides you and scores you against the 7 rubric areas.", () => { Close(); game.StartMission(false); });
                Add("Creative mode", "Walk or fly around the whole Moon base and build freely. No hunger, no damage, unlimited blocks, free upgrades, no alerts, no score. Days and nights still pass, and L launches the rocket. Good for a first look or for showing the design.", () => { Close(); game.StartMission(true); });
                AddStoreRow();
                Add("Autopilot", "The AI plays instead of you, in Creative mode: it does the jobs in every zone, fixes the panels, fills the Battery Bank, reads, goes to the team meetings and plays the mini-game maps. Press SPACE (or A in the headset) to take over.", () => { Close(); game.StartMission(true); var fr = game.GetComponent<HunchFrontier>(); if (fr) fr.StartAutopilot(); });
                Add("Autopilot Smooth", "The AI plays the real Survival mission and goes for the best score. It walks and runs between the zones instead of jumping, and plays each choice ahead on a copy of the habitat before it decides. Press SPACE (or A in the headset) to take over.", () => { Close(); game.StartMission(false); var fr = game.GetComponent<HunchFrontier>(); if (fr) fr.StartAutopilotSmooth(); });
                Add("The Show", "Watch the mission as a cartoon: the Commander strolls through all 30 days with episode cards, speech bubbles, comic bursts and changing camera angles. SPACE (A in the headset) stops it and you play on from there.", () => { Close(); game.StartMission(false); var fr = game.GetComponent<HunchFrontier>(); if (fr) fr.StartShow(); });
                Add("Kid mode: " + OnOff(sim.kidMode), "ON: more time on every alert, gentler rules, nobody can get hurt. OFF: standard rules.", () => { sim.kidMode = !sim.kidMode; SavePrefs(); Build(); });
                Add("Voice chat: " + OnOff(optListen), "Talk to the game with your microphone. Commands work anywhere: \"what should I do\", \"where is the greenhouse\", \"high five\". Press T and speak freely: your words appear as text and ARIA answers. Commands run on this PC. Free speech uses Windows online speech recognition (internet, audio goes to Microsoft)." + mic, () => Toggle(ref optListen));
                Add("Options", "Sounds, voices, effects, and what is shown or hidden on screen.", () => Open(Page.Options));
                Add("How to play", "Controls for keyboard and mouse, gamepad, and VR.", () => Open(Page.Help));
                Add("Credits", "Lunar Habitat Commander  " + Copyright + "\nA student game made for the NASA HUNCH program.\n" + Thanks + ", NASA HUNCH project partners.\nNot an official NASA product.", () => { });
                items[items.Count - 1].style = 3;
                Add("Quit", "Close the game.", Quit);
                break;
            case Page.Pause:
                Add("Resume", "Back to the habitat.", Close);
                if (storePage != null && (extraShow == null || extraShow(storePage))) AddStoreRow();
                Add("Talk to ARIA", "Ask the habitat AI for advice, a status report, a crew report or a Moon fact.", () => OpenTalk(true));
                foreach (var ep in extraPages) { if (extraShow != null && !extraShow(ep)) continue; var page0 = ep; Add(ep.label, ep.hint, () => { if (page0.direct != null) page0.direct(); else OpenExtra(page0); }); }
                if (sim.phase == HabPhase.Mission && !rocketHidden) Add("Launch the supply rocket", "Starts the 5-second countdown at the spaceport (same as the L key). The rocket is " + (fx ? fx.RocketWhere : "not found") + ".", () => { Close(); if (fx && !fx.LaunchRocket()) sim.Say("ARIA: The rocket is " + fx.RocketWhere + ". It can launch again once it is back on the pad."); });
                if (sim.phase == HabPhase.Mission && sim.timeFrozen)
                {
                    if (HunchCheats.SkipOk) Add("Skip to lunar sunset", "Free explore only: jumps to the evening of day 9, just before the Sun sets for the 9-day lunar night.", () => { Close(); sim.missionSeconds = HabitatSim.SecondsPerDay * 8.8f; sim.Say("ARIA: Day 9. Watch the Sun go down. The lunar night lasts until day 19."); });
                    if (HunchCheats.SkipOk) Add("Skip to lunar sunrise", "Free explore only: jumps to day 18, just before the Sun comes back.", () => { Close(); sim.missionSeconds = HabitatSim.SecondsPerDay * 17.8f; sim.Say("ARIA: Day 18. The Sun is about to rise again."); });
                }
                Add("Options", "Sounds, voices, effects, and what is shown or hidden on screen.", () => Open(Page.Options));
                Add("How to play", "Controls for keyboard and mouse, gamepad, and VR.", () => Open(Page.Help));
                Add("Restart", "Go back to the main menu and start again. Your levels, star coins, costumes and Lumi are kept, and so is everything from the STAR STORE: purchases live on your Meta account.", Restart);
                Add("Quit", "Close the game.", Quit);
                break;
            case Page.Options:
                Add("Dashboard and panels: " + Shown(optHud), "The mission, crew and alert panels.", () => Toggle(ref optHud));
                Add("Zone rings and labels: " + Shown(optMarkers), "The glowing ring, orb and name at each of the 16 zones.", () => Toggle(ref optMarkers));
                Add("Crew info tags: " + Shown(optTags), "The floating heart rate, stress, mood and sleep readout next to each astronaut.", () => Toggle(ref optTags));
                Add("Mood faces: " + Shown(optFaces), "The smiley above each astronaut that shows how they feel.", () => Toggle(ref optFaces));
                Add("Robot buddy: " + Shown(optBuddy), "ARIA's little robot that flies ahead to show the way.", () => Toggle(ref optBuddy));
                Add("Green guide arrows: " + Shown(optArrows), "Arrows on the floor and a distance marker that lead you to where you are needed.", () => Toggle(ref optArrows));
                Add("Map, compass and GPS: " + Shown(optMap), "The floor-plan minimap with its compass and position read-out. N opens the full base map.", () => Toggle(ref optMap));
                Add("Camera effects: " + OnOff(optCamFx), "Red warning pulse, storm static, rocket shake.", () => Toggle(ref optCamFx));
                Add("Cinematic picture: " + OnOff(optCine), "Filmic tone mapping and high-quality bloom. OFF gives the flatter, brighter original look.", () => Toggle(ref optCine));
                Add("Particle effects: " + OnOff(optParticles), "Dust, sparks, healing glows, the storm shield (Hovl Studio Magic Effects pack).", () => Toggle(ref optParticles));
                Add("Automatic doors: " + OnOff(optDoors), "Sliding glass doors that open when someone comes near.", () => Toggle(ref optDoors));
                Add("Sound effects: " + OnOff(optSfx), "Habitat hum, footsteps, doors, alarms, suit breathing.", () => Toggle(ref optSfx));
                Add("Realistic space silence: " + OnOff(optSilence), "ON: outside there is no air, so the habitat goes silent and you only hear your suit. OFF: you hear everything everywhere.", () => Toggle(ref optSilence));
                Add("Music: " + OnOff(optMusic), "Background music.", () => Toggle(ref optMusic));
                Add("Spoken voices: " + OnOff(optVoice), "ARIA and the astronauts speak their lines out loud using the Windows voices.", () => Toggle(ref optVoice));
                Add("Voice chat (microphone): " + OnOff(optListen), "Spoken commands anywhere, and free speech turned into text while the talk panel (T) is open. Free speech uses Windows online speech recognition: it needs internet and the audio goes to Microsoft's speech service." + mic, () => Toggle(ref optListen));
                Add("ARIA auto-care: " + OnOff(optAuto), "ON: when there is no alert, ARIA sends a service robot to do routine care by itself. Makes the game easier.", () => Toggle(ref optAuto));
                Add("Kid mode: " + OnOff(sim.kidMode), "More time on every alert and gentler rules.", () => { sim.kidMode = !sim.kidMode; SavePrefs(); Build(); });
                Add(photo ? "Photo mode: ON (everything hidden)" : "Photo mode: OFF", "Hides everything on screen at once for clean pictures. F1 does the same while playing.", () => { photo = !photo; Apply(); Build(); });
                Add("Back", "", () => Open(sim.phase == HabPhase.Briefing ? Page.Main : Page.Pause));
                break;
            case Page.Help:
                Add("Back", "", () => Open(sim.phase == HabPhase.Briefing ? Page.Main : Page.Pause));
                break;
            case Page.Extra:
                if (extra != null && extra.build != null) extra.build(this);
                Add("Back", "", Back);
                break;
            case Page.Talk:
                if (talkWith >= 0)
                {
                    Add("How are you feeling?", "", () => Respond("feel")); Add("What would help you?", "", () => Respond("need")); Add("High five!", "", () => Respond("five"));
                    Add("Tell me about your job", "", () => Respond("job")); Add("Tell me a joke", "", () => Respond("joke")); Add("Goodbye", "", Close);
                }
                else
                {
                    Add("What should I do now?", "", () => Respond("advice")); Add("Status report", "", () => Respond("status")); Add("How is the crew?", "", () => Respond("crew"));
                    Add("What is this room?", "", () => Respond("zone")); Add("Tell me a Moon fact", "", () => Respond("fact")); Add("Tell me a joke", "", () => Respond("joke")); Add("Goodbye", "", Close);
                }
                break;
        }
        sel = Mathf.Clamp(sel, 0, Mathf.Max(0, items.Count - 1));
        ShowCard();
    }

    void Open(Page p)
    {
        bool wasOpen = page != Page.None;
        page = p; sel = 0; closing = false; openedAt = Time.unscaledTime;
        game.menuOpen = true;
        if (!wasOpen || p != Page.Talk) typed = "";
        if (!game.HeadsetActive) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        Build();
    }

    public void Close()
    {
        if (page == Page.None) return;
        if (page == Page.Talk && voice) voice.StopDictation();
        page = Page.None; closing = true; typed = "";
        if (vrRoot && vrRoot.gameObject.activeSelf) { vrRoot.gameObject.SetActive(false); VrCardDark(false); }
        game.freezeHud = false;       // the game gets its keys back at the end of this frame, so the key that closed the menu does nothing else
        if (orbiting) { orbiting = false; game.GoTo(0); Apply(); }
        game.cardOverride = ""; cardShown = "";
        if (!game.HeadsetActive && desk && desk.active) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    void Back()
    {
        if (page == Page.Extra) { if (extra != null && extra.parent != null) OpenExtra(extra.parent); else Open(sim.phase == HabPhase.Briefing ? Page.Main : Page.Pause); }
        else if (page == Page.Options || page == Page.Help) Open(sim.phase == HabPhase.Briefing ? Page.Main : Page.Pause);
        else if (page == Page.Main && sim.phase == HabPhase.Briefing) { }          // nothing behind the main menu
        else Close();
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Restart()
    {
        string name = SceneManager.GetActiveScene().name;
        if (Application.CanStreamedLevelBeLoaded(name)) { Time.timeScale = 1f; SceneManager.LoadScene(name); }
        else { Close(); sim.Say("ARIA: Restart works in the built game. In the Editor, press Play again."); }
    }

    void Move(int d) { if (items.Count == 0) return; sel = (sel + d + items.Count) % items.Count; ShowCard(); }
    void Select() { if (sel >= 0 && sel < items.Count && items[sel].act != null) items[sel].act(); }

    // the game asks this before it handles its own buttons
    bool Take(string what)
    {
        if (HunchIntro.Active) return true;
        if (page == Page.None)
        {
            if (closing) return true;
            if (what == "help" && game.HeadsetActive) { Open(sim.phase == HabPhase.Briefing ? Page.Main : Page.Pause); return true; }
            return false;
        }
        if (what != "help" && Time.unscaledTime - openedAt < 0.12f) return true;      // the press that opened this page must not also move or choose in it
        var kb = Keyboard.current; bool typing = page == Page.Talk && !game.HeadsetActive;
        switch (what)
        {
            case "interact":
                if (kb != null && (kb.enterKey.isPressed || kb.numpadEnterKey.isPressed)) return true;      // Enter is handled in Update (it may send a typed message)
                if (typing && kb != null && kb.eKey.isPressed) return true;                                  // E is a letter while typing
                if (game.HeadsetActive) { var hovered = VrHovered(); if (hovered != null) VrClick(hovered); return true; }      // VR: the trigger only presses the button the laser is on (A chooses the highlighted one)
                Select(); return true;
            case "next": Move(1); return true;
            case "prev": if (typing && kb != null && kb.qKey.isPressed) return true; Move(-1); return true;
            case "help": if (game.HeadsetActive) Back(); return true;
        }
        return true;
    }

    // ------------------------------------------------------------------ talking
    void OpenTalk() { OpenTalk(false); }
    void OpenTalk(bool ariaOnly)
    {
        if (!aria) return;
        talkWith = !ariaOnly && kid && sim.phase != HabPhase.Report ? kid.NearestCrew(7f) : -1;
        log.Clear();
        string hello = talkWith >= 0 ? aria.CrewHello(talkWith) : "Hello Commander. I am ARIA, the habitat AI. Ask me anything, or pick a question.";
        Line(Speaker(talkWith), hello); Speak(hello, talkWith);
        Open(Page.Talk);
        if (optListen && voice && !voice.StartDictation() && voice.DictationProblem.Length > 0) log.Add("<i>Speech to text is off: " + voice.DictationProblem + ". Spoken commands and typing still work.</i>");
    }

    string Speaker(int k) { return k >= 0 && k < sim.crew.Count ? sim.crew[k].name : "ARIA"; }
    void Line(string who, string text) { log.Add("<b>" + who + ":</b> " + text); while (log.Count > 7) log.RemoveAt(0); }
    void Speak(string text, int k) { if (voice) voice.Say(text, k + 1); lastSpoken = sim.message; }

    static bool Any(string t, params string[] keys) { foreach (var k in keys) if (t.Contains(k)) return true; return false; }

    /// Works out what a typed message is asking for. Simple keyword matching, so it is predictable and works offline.
    static string Intent(string raw)
    {
        var sb = new StringBuilder(" ");
        foreach (char c in raw.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(c) || c == '\'' ? c : ' ');
        string t = sb.Append(' ').ToString();
        if (Any(t, "where is", "where's", "guide me", "show me the way", "navigate", "directions", "how do i get", "how far", "find the", "lead me"))
            foreach (var z in ZoneWords) for (int i = 1; i < z.Length; i++) if (t.Contains(z[i])) return "nav:" + z[0];
        if (Any(t, "go to", "take me", "bring me", "walk to", "teleport"))
            foreach (var z in ZoneWords) for (int i = 1; i < z.Length; i++) if (t.Contains(z[i])) return "goto:" + z[0];
        if (Any(t, "launch", "rocket", "blast off", "lift off")) return "launch";
        if (Any(t, " map ")) return "map";
        if (Any(t, "high five", "highfive", " five ")) return "five";
        if (Any(t, "joke", "funny", "laugh")) return "joke";
        if (Any(t, " fact", "did you know", "teach me", "tell me something", "about the moon")) return "fact";
        if (Any(t, "how are you", "you feel", "you ok", "you alright", "you doing", "you good")) return "feel";
        if (Any(t, "scared", "afraid", "worried", "nervous", "lonely", " sad ", "anxious", "i feel", "i'm feeling", "stressed", "homesick")) return "comfort";
        if (Any(t, "what would help", "you need", "need anything", "can i help", "help you", "cheer you", "for you")) return "need";
        if (Any(t, "your job", "your role", "what do you do", "your work", "about yourself", "about you")) return "job";
        if (Any(t, "who are you", "your name", "what are you")) return "who";
        if (Any(t, "status", "report", "systems", "oxygen", "power", "water", "food", "battery", "dust")) return Any(t, "crew") ? "crew" : "status";
        if (Any(t, "crew", "everyone", "everybody", " team", "astronauts")) return "crew";
        if (Any(t, "what should", "what now", "what do i do", "what next", "advice", "advise", "help me", "what can i do", "suggest", "hint")) return "advice";
        if (Any(t, "where am i", "this room", "this zone", "this place", "what is this", "what's this", "where are we")) return "zone";
        if (Any(t, "thank")) return "thanks";
        if (Any(t, " bye", "goodbye", "see you", " later")) return "bye";
        if (Any(t, " hi ", " hello", " hey", "good morning", "good evening")) return "hello";
        return "";
    }

    void Respond(string intent)
    {
        if (!aria) return;
        if (intent.StartsWith("goto:"))
        {
            int zi = game.ZoneIndex(intent.Substring(5)); Close();
            if (zi >= 0) { game.GoTo(zi); sim.Say("ARIA: Taking you to " + game.zones[zi].title + "."); }
            return;
        }
        if (intent.StartsWith("nav:"))
        {
            int zi = game.ZoneIndex(intent.Substring(4)); string say = guide && zi >= 0 ? guide.SetWaypoint(zi) : "I cannot find that place.";
            if (page != Page.None) Close();
            sim.Say("ARIA: " + say); return;
        }
        if (intent == "launch")
        {
            if (page != Page.None) Close();
            if (!fx || !fx.LaunchRocket()) sim.Say("ARIA: The rocket is " + (fx ? fx.RocketWhere : "not available") + ". It can launch once it is back on the pad and the mission has started.");
            return;
        }
        if (intent == "map") { if (page != Page.None) Close(); if (guide && !game.HeadsetActive) guide.ProbeBigMap(); return; }
        if (intent == "bye") { Close(); return; }
        int who = talkWith; bool crew = who >= 0; string r; bool social = false;
        switch (intent)
        {
            case "hello": r = crew ? aria.CrewHello(who) : "Hello Commander. Ask me for a status report, advice, a crew report or a Moon fact."; social = crew; break;
            case "feel": r = crew ? aria.CrewFeeling(who) : "All my systems are running well, thank you. " + aria.StatusReport(); social = crew; break;
            case "need": r = crew ? aria.CrewNeeds(who) : aria.Advice(); social = crew; break;
            case "job": r = crew ? aria.CrewJob(who) : "I watch the crew's heart rate, sleep, mood and stress, predict who will need help before it gets serious, and set the lights to follow an Earth day. I also watch the air, water and power."; social = crew; break;
            case "five": if (crew && kid) { kid.HighFive(who); r = "High five!"; } else r = "I do not have hands, but here is a virtual high five!"; break;
            case "joke": r = aria.Joke(); social = crew; break;
            case "who": r = crew ? "I am " + sim.crew[who].name + ", the " + sim.crew[who].role.ToLowerInvariant() + " on this mission." : "I am ARIA, the habitat's AI. I look after the crew's wellbeing and the habitat's systems."; break;
            case "thanks": r = crew ? "Any time, Commander!" : "You are welcome, Commander."; break;
            case "comfort": r = crew ? "We look after each other up here. Take a slow breath with me. You are doing great, Commander." : aria.Comfort(); break;
            case "status": who = -1; r = aria.StatusReport(); break;
            case "advice": who = -1; r = aria.Advice(); break;
            case "crew": who = -1; r = aria.CrewReport(); break;
            case "zone": who = -1; r = aria.ZoneInfo(); break;
            case "fact": who = -1; r = aria.Fact(); break;
            default: r = crew ? "Sorry, I did not catch that. You can ask how I am feeling, what would help me, or about my job." : "I am not sure I understood. Try: what should I do, status report, how is the crew, tell me a fact, or go to the greenhouse."; break;
        }
        Line(Speaker(who), r); Speak(r, who);
        if (social && aria.SocialBoost(talkWith)) log.Add("<i>Talking helped: " + sim.crew[talkWith].name + "'s mood went up and stress went down.</i>");
        if (page != Page.Talk) sim.Say((who >= 0 ? sim.crew[who].name + ": " : "ARIA: ") + r);
        ShowCard();
    }

    // free speech turned into text by Windows while the talk panel is open
    void OnDictation(string text)
    {
        if (page != Page.Talk) return;
        typed = ""; Line("You said", text); Respond(Intent(text));
    }

    void Submit()
    {
        if (typed.Trim().Length == 0) { Select(); return; }
        string text = typed.Trim(); typed = "";
        Line("You", text); Respond(Intent(text));
    }

    void OnText(char c)
    {
        if (page != Page.Talk || game.HeadsetActive || Time.unscaledTime - openedAt < 0.2f || char.IsControl(c)) return;
        if (typed.Length == 0 && c >= '1' && c <= '9') { int i = c - '1'; if (i < items.Count) { sel = i; Select(); } return; }
        if (typed.Length < 90) typed += c;
    }

    // ------------------------------------------------------------------ voice commands
    void OnVoice(string cmd)
    {
        if (kid && voice) kid.Toast("You said: \"" + voice.LastHeard + "\"", 3f);
        if (cmd.StartsWith("goto:") || cmd.StartsWith("nav:") || cmd == "launch" || cmd == "map") { if (sim.phase == HabPhase.Briefing) return; Respond(cmd); return; }
        bool open = page != Page.None;
        switch (cmd)
        {
            case "start": if (sim.phase == HabPhase.Briefing) { Close(); game.StartMission(false); } break;
            case "explore": if (sim.phase == HabPhase.Briefing) { Close(); game.StartMission(true); } break;
            case "menu": if (!open) Open(sim.phase == HabPhase.Briefing ? Page.Main : Page.Pause); break;
            case "bye": if (open && !(page == Page.Main && sim.phase == HabPhase.Briefing)) Close(); break;
            case "act": if (open) Select(); else game.DoInteract(); break;
            case "next": if (open) Move(1); else game.GoTo((game.Current + 1) % game.zones.Count); break;
            case "prev": if (open) Move(-1); else game.GoTo((game.Current - 1 + game.zones.Count) % game.zones.Count); break;
            case "urgent": if (!open) game.GoToUrgent(); break;
            case "up": if (!open && nav) nav.Lift(1); break;
            case "down": if (!open && nav) nav.Lift(-1); break;
            case "hide": photo = true; Apply(); break;
            case "show": photo = false; Apply(); break;
            case "music": optMusic = !optMusic; Apply(); SavePrefs(); break;
            case "pause": if (!open) game.Paused = !game.Paused; break;
            case "talk": if (!open && sim.phase != HabPhase.Briefing) OpenTalk(); break;
            case "feel": case "need": case "job": case "five":
                if (sim.phase == HabPhase.Briefing) break;
                if (page != Page.Talk) { talkWith = kid ? kid.NearestCrew(8f) : -1; if (talkWith < 0 && cmd != "five") { Respond("advice"); break; } }
                if (cmd == "five" && talkWith < 0) { sim.Say("ARIA: Walk up to an astronaut first, then say high five."); break; }
                Respond(cmd); break;
            default: if (sim.phase != HabPhase.Briefing) { if (page != Page.Talk) talkWith = -1; Respond(cmd); } break;
        }
    }

    // ------------------------------------------------------------------ frame
    int listFirst, listVisible;
    /// the round X in the top right corner: one click goes back
    Rect CloseRect() { float u = Screen.height / 1080f; return new Rect(Screen.width - 84f * u, 28f * u, 56f * u, 56f * u); }
    Rect ItemRect(int i)
    {
        float u = Screen.height / 1080f, W = Screen.width, H = Screen.height;
        if (page == Page.Talk)
        {
            Rect p = TalkPanel(); float h = 46f * u;
            return new Rect(p.xMax - 440f * u, p.y + 62f * u + i * h, 420f * u, h - 6f * u);
        }
        float top = page == Page.Help ? H - 150f * u : (page == Page.Options || page == Page.Extra ? 150f : 300f) * u, avail = H - top - 70f * u, each = Mathf.Min(58f * u, avail / Mathf.Max(1, items.Count));
        // too many lines for the screen: they keep a readable height and the list scrolls instead
        float minEach = 44f * u; listVisible = items.Count;
        if (each < minEach && page != Page.Help)
        {
            each = minEach; listVisible = Mathf.Max(1, Mathf.FloorToInt(avail / each));
            if (sel < listFirst) listFirst = sel; if (sel >= listFirst + listVisible) listFirst = sel - listVisible + 1;
            listFirst = Mathf.Clamp(listFirst, 0, Mathf.Max(0, items.Count - listVisible));
            if (i < listFirst || i >= listFirst + listVisible) return new Rect(-9999f, -9999f, 1f, 1f);
            return new Rect(Mathf.Min(110f * u, W * 0.06f), top + (i - listFirst) * each, Mathf.Min(600f * u, W * 0.42f), each - 6f * u);
        }
        listFirst = 0;
        return new Rect(Mathf.Min(110f * u, W * 0.06f), top + i * each, Mathf.Min(600f * u, W * 0.42f), each - 6f * u);
    }
    Rect TalkPanel()
    {
        float u = Screen.height / 1080f, w = Mathf.Min(1280f * u, Screen.width - 20f);
        return new Rect((Screen.width - w) * 0.5f, Screen.height - 480f * u, w, 464f * u);
    }

    void Update()
    {
        if (HunchIntro.Active) return;      // the story introduction is playing: the menu waits behind it
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb != hookedKb) { if (hookedKb != null) hookedKb.onTextInput -= OnText; hookedKb = kb; if (kb != null) kb.onTextInput += OnText; }

        // ARIA says her messages out loud
        if (sim.message != lastSpoken) { lastSpoken = sim.message; if (voice && sim.message.StartsWith("ARIA") && page != Page.Talk) voice.Say(sim.message, 0); }

        if (page == Page.None)
        {
            if (kb != null && !closing)
            {
                if (kb.escapeKey.wasPressedThisFrame) Open(sim.phase == HabPhase.Briefing ? Page.Main : Page.Pause);
                else if (kb.f1Key.wasPressedThisFrame) { photo = !photo; Apply(); }
            }
            return;
        }
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame) { Back(); return; }
            if (kb.upArrowKey.wasPressedThisFrame) Move(-1);
            if (kb.downArrowKey.wasPressedThisFrame) Move(1);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { if (page == Page.Talk) Submit(); else Select(); return; }
            if (page == Page.Talk && typed.Length > 0 && kb.backspaceKey.isPressed && Time.unscaledTime >= backspaceAt)
            { typed = typed.Substring(0, typed.Length - 1); backspaceAt = Time.unscaledTime + (kb.backspaceKey.wasPressedThisFrame ? 0.35f : 0.05f); }
        }
        if (mouse != null && !game.HeadsetActive && Time.unscaledTime - openedAt > 0.1f)
        {
            Vector2 m = mouse.position.ReadValue(); m.y = Screen.height - m.y;
            bool moved = (m - lastMouse).sqrMagnitude > 1f, click = mouse.leftButton.wasPressedThisFrame; lastMouse = m;
            if (click && page != Page.Talk && CloseRect().Contains(m)) { Back(); return; }
            float wheel = mouse.scroll.ReadValue().y; if (Mathf.Abs(wheel) > 0.01f && listVisible < items.Count && page != Page.Talk) { listFirst = Mathf.Clamp(listFirst + (wheel < 0f ? 1 : -1), 0, items.Count - listVisible); sel = Mathf.Clamp(sel, listFirst, listFirst + listVisible - 1); }
            if (moved || click)                                   // a resting mouse does not fight the keyboard
                for (int i = 0; i < items.Count; i++)
                    if (ItemRect(i).Contains(m)) { sel = i; if (click) { Select(); return; } break; }
        }
        var ts = Touchscreen.current;      // a phone or tablet: a tap chooses the line under the finger
        if (ts != null && !game.HeadsetActive && Time.unscaledTime - openedAt > 0.1f && ts.primaryTouch.press.wasPressedThisFrame)
        {
            Vector2 m = ts.primaryTouch.position.ReadValue(); m.y = Screen.height - m.y;
            if (page != Page.Talk && CloseRect().Contains(m)) { Back(); return; }
            for (int i = 0; i < items.Count; i++) if (ItemRect(i).Contains(m)) { sel = i; Select(); return; }
        }
    }

    // Behind the main menu the camera slowly circles the whole Moon base (desktop only; in VR you stay where you are).
    void Backdrop()
    {
        bool want = page != Page.None && page != Page.Talk && sim.phase == HabPhase.Briefing && !game.HeadsetActive && desk && desk.active && game.origin;
        if (!want) return;
        orbiting = true;
        if (kid && !kid.hideBuddy) kid.hideBuddy = true;
        float a = Time.unscaledTime * 2.5f * Mathf.Deg2Rad;
        Vector3 pos = new Vector3(Mathf.Sin(a) * 235f, 105f, Mathf.Cos(a) * 235f), look = new Vector3(0f, 30f, 0f) - pos;
        var cc = game.origin.GetComponent<CharacterController>(); if (cc && cc.enabled) cc.enabled = false;
        game.origin.transform.position = pos - Vector3.up * desk.eyeHeight;
        desk.SetLook(Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, Mathf.Atan2(-look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg);
    }

    void LateUpdate()
    {
        Backdrop();
        if (closing) { closing = false; game.menuOpen = false; }
        if (page != Page.None && game.HeadsetActive && !HunchIntro.Active) ShowCard();
    }

    // ------------------------------------------------------------------ VR: a panel of real buttons on the dashboard card
    // Point the controller at a button and pull the trigger (or squeeze the grip). B / Y still move the highlight.
    class VrRow { public GameObject go; public Renderer bg; public TMPro.TextMeshPro label; public UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable inter; public int index; }
    const int MaxVrRows = 10;
    Transform vrRoot; TMPro.TextMeshPro vrHead, vrHint; readonly List<VrRow> vrRows = new List<VrRow>(); int vrFirst; float vrClickAt; MaterialPropertyBlock vrBlock;

    TMPro.TextMeshPro VrText(string name, float fontSize, TMPro.TextAlignmentOptions align)
    {
        var go = new GameObject(name); go.transform.SetParent(vrRoot, false);
        var t = go.AddComponent<TMPro.TextMeshPro>();
        if (game.hud) t.font = game.hud.font;
        t.fontSize = fontSize; t.alignment = align; t.richText = true; t.color = Color.white;
        try { t.fontMaterial.renderQueue = 3100; } catch { }      // after the card background, whatever the viewing angle
        return t;
    }

    void BuildVrPanel()
    {
        vrRoot = new GameObject("HUNCH VR Menu").transform; vrRoot.SetParent(game.hudRoot, false); vrRoot.localPosition = new Vector3(0f, 0f, -0.02f);
        vrBlock = new MaterialPropertyBlock();
        var mat = new Material(game.zones[0].ring.sharedMaterial); mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.08f)); mat.renderQueue = 2995;
        // the menu's own card: drawn first, then the buttons, then the words, so the text can never end up hidden behind a dark card
        var card = GameObject.CreatePrimitive(PrimitiveType.Quad); card.name = "Menu card"; Destroy(card.GetComponent<Collider>());
        card.transform.SetParent(vrRoot, false); card.transform.localPosition = new Vector3(0f, -0.005f, 0.014f); card.transform.localScale = new Vector3(1.06f, 0.69f, 1f);
        var cardMat = new Material(game.zones[0].ring.sharedMaterial); cardMat.SetColor("_BaseColor", new Color(0.06f, 0.12f, 0.26f, 0.94f)); cardMat.renderQueue = 2990;
        var cardR = card.GetComponent<Renderer>(); cardR.sharedMaterial = cardMat; cardR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; cardR.receiveShadows = false;
        vrHead = VrText("Header", 0.3f, TMPro.TextAlignmentOptions.TopLeft); vrHead.enableAutoSizing = true; vrHead.fontSizeMin = 0.1f; vrHead.fontSizeMax = 0.34f;
        vrHint = VrText("Hint", 0.17f, TMPro.TextAlignmentOptions.TopLeft); vrHint.enableAutoSizing = true; vrHint.fontSizeMin = 0.1f; vrHint.fontSizeMax = 0.18f;
        for (int i = 0; i < MaxVrRows; i++)
        {
            var row = new VrRow();
            row.go = GameObject.CreatePrimitive(PrimitiveType.Quad); row.go.name = "Button " + (i + 1);
            Destroy(row.go.GetComponent<Collider>());
            row.go.transform.SetParent(vrRoot, false); row.go.transform.localScale = new Vector3(0.97f, 0.041f, 1f);
            var box = row.go.AddComponent<BoxCollider>(); box.size = new Vector3(1f, 1f, 0.02f);
            row.bg = row.go.GetComponent<Renderer>(); row.bg.sharedMaterial = mat; row.bg.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            row.label = VrText("Label " + (i + 1), 0.25f, TMPro.TextAlignmentOptions.MidlineLeft); row.label.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            row.label.rectTransform.sizeDelta = new Vector2(0.92f, 0.041f);
            row.inter = row.go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            var captured = row;
            row.inter.hoverEntered.AddListener(_ => { if (page != Page.None && captured.index >= 0 && captured.index < items.Count) sel = captured.index; });
            row.inter.selectEntered.AddListener(_ => { if (HunchVr.Claim(true)) VrClick(captured); });
            vrRows.Add(row);
        }
    }

    Renderer vrCardBg;
    void VrCardDark(bool on)      // while a menu is open the dashboard's own see-through card makes way for the menu's card
    {
        if (!vrCardBg && game.hudRoot) { var t = game.hudRoot.Find("Background"); if (t) vrCardBg = t.GetComponent<Renderer>(); }
        if (vrCardBg) { vrCardBg.SetPropertyBlock(null); vrCardBg.enabled = !on; }
    }
    public string ProbeVrCard()
    {
        var t = game.hudRoot ? game.hudRoot.Find("Background") : null; var r = t ? t.GetComponent<Renderer>() : null; var m = r ? r.sharedMaterial : null;
        string s = "background=" + (m ? m.shader.name + " queue " + m.renderQueue + " colour " + (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "?") + " size " + t.localScale.ToString("F2") + " at " + t.localPosition.ToString("F3") : "none");
        var ring = game.zones[0].ring.sharedMaterial; s += " | ring=" + ring.shader.name + " queue " + ring.renderQueue;
        if (game.hud) s += " | hud text=" + game.hud.fontSharedMaterial.shader.name + " queue " + game.hud.fontSharedMaterial.renderQueue;
        if (vrHead) s += " | menu text queue " + vrHead.fontMaterial.renderQueue + " rows " + vrRows.Count;
        return s;
    }

    bool RowHot(VrRow r) { return r.go.activeInHierarchy && ((r.inter && r.inter.isHovered) || HunchVr.Hovering(r.go.transform)); }
    VrRow VrHovered() { foreach (var r in vrRows) if (RowHot(r)) return r; return null; }

    void VrClick(VrRow r)
    {
        if (page == Page.None || Time.unscaledTime - vrClickAt < 0.3f || Time.unscaledTime - openedAt < 0.3f) return;      // trigger and grip can both report the same press
        vrClickAt = Time.unscaledTime;
        int win = VrWindow();
        if (r.index == -2) vrFirst = Mathf.Max(0, vrFirst - win);
        else if (r.index == -3) vrFirst = Mathf.Min(Mathf.Max(0, items.Count - win), vrFirst + win);
        else if (r.index >= 0 && r.index < items.Count) { sel = r.index; Select(); }
        game.Buzz(0.4f, 0.05f);
    }

    float VrHeadHeight() { return page == Page.Talk ? 0.25f : (page == Page.Help ? 0.5f : (page == Page.Main ? 0.13f : 0.06f)); }
    int VrMaxRows() { return Mathf.Clamp(Mathf.FloorToInt((0.6f - VrHeadHeight() - (page == Page.Talk || page == Page.Help ? 0.01f : 0.09f)) / 0.046f), 1, MaxVrRows); }
    int VrWindow() { int max = VrMaxRows(); return items.Count <= max ? items.Count : max - 2; }      // two rows become "more above / more below" when the list is long

    void ShowCard()
    {
        if (page == Page.None || !game.HeadsetActive || !game.hudRoot)
        {
            if (page == Page.None) game.cardOverride = "";
            if (vrRoot && vrRoot.gameObject.activeSelf) { vrRoot.gameObject.SetActive(false); VrCardDark(false); }
            game.freezeHud = false;
            return;
        }
        if (!vrRoot) BuildVrPanel();
        if (!vrRoot.gameObject.activeSelf) { vrRoot.gameObject.SetActive(true); VrCardDark(true); }
        game.freezeHud = true;
        if (game.cardOverride != " ") { game.cardOverride = " "; if (game.hud) game.hud.text = " "; }      // the normal dashboard text makes way for the buttons

        // ---- header
        var sb = new StringBuilder();
        if (page == Page.Talk)
        {
            sb.Append("<b>Talking with ").Append(Speaker(talkWith)).Append("</b>\n<size=80%>");
            for (int i = Mathf.Max(0, log.Count - 3); i < log.Count; i++) sb.Append(log[i]).Append("\n");
            sb.Append("</size>");
        }
        else if (page == Page.Help) sb.Append("<b>HOW TO PLAY</b>\n<size=75%>").Append(HelpText(true)).Append("</size>");
        else sb.Append(page == Page.Options ? "<b>OPTIONS</b>" : (page == Page.Extra && extra != null ? "<b>" + extra.title + "</b>" : page == Page.Pause ? "<b>PAUSED</b>" : "<b>LUNAR HABITAT COMMANDER</b>\n<size=60%>NASA HUNCH - Lunar Smart Adaptive Habitat with AI Psychological Monitoring</size>\n<size=48%><color=#FFE27A>" + Copyright + "</color>   <color=#B8C4DA>" + Thanks + "</color></size>"));
        float top = 0.3f, headH = VrHeadHeight();
        string head = sb.ToString(); if (vrHead.text != head) vrHead.text = head;
        vrHead.rectTransform.sizeDelta = new Vector2(0.96f, headH); vrHead.rectTransform.localPosition = new Vector3(0f, top - headH * 0.5f, 0f);

        // ---- buttons
        { var hot = VrHovered(); if (hot != null && hot.index >= 0 && hot.index < items.Count) sel = hot.index; }
        int max = VrMaxRows(), win = VrWindow(); bool scroll = items.Count > max;
        if (!scroll) vrFirst = 0;
        else
        {
            var hovered = VrHovered();
            if (hovered == null) { if (sel < vrFirst) vrFirst = sel; if (sel >= vrFirst + win) vrFirst = sel - win + 1; }      // B / Y moved the highlight off the page
            vrFirst = Mathf.Clamp(vrFirst, 0, Mathf.Max(0, items.Count - win));
        }
        float y0 = top - headH - 0.035f;
        for (int i = 0; i < vrRows.Count; i++)
        {
            var r = vrRows[i]; int idx = -1; string text = "";
            if (scroll)
            {
                if (i == 0) { idx = -2; text = vrFirst > 0 ? "<color=#9AD8FF>...  more above  ...</color>" : ""; }
                else if (i == win + 1) { idx = -3; text = vrFirst + win < items.Count ? "<color=#9AD8FF>...  more below  ...</color>" : ""; }
                else if (i <= win) idx = vrFirst + i - 1;
            }
            else if (i < items.Count) idx = i;
            bool used = idx >= 0 ? idx < items.Count : text.Length > 0;
            if (r.go.activeSelf != used) { r.go.SetActive(used); r.label.gameObject.SetActive(used); }
            if (!used) continue;
            r.index = idx; bool on = idx == sel;
            if (idx >= 0) text = (on ? "<color=#FFFFFF><b>" : "<color=#E6ECF8>") + items[idx].label + (on ? "</b></color>" : "</color>");
            if (r.label.text != text) r.label.text = text;
            float y = y0 - i * 0.046f;
            r.go.transform.localPosition = new Vector3(0f, y, 0.006f); r.label.rectTransform.localPosition = new Vector3(0.01f, y, 0f);
            int st = idx >= 0 ? items[idx].style : 0;
            if (st == 1)
            {
                // the STAR STORE rows shimmer from gold to violet so they stand out on the card
                float w = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f + i * 0.7f);
                Color c = Color.Lerp(new Color(1f, 0.74f, 0.12f), new Color(0.72f, 0.28f, 1f), w);
                c.a = on ? 0.95f : (RowHot(r) ? 0.8f : 0.55f); if (on) c = Color.Lerp(c, Color.white, 0.18f);
                vrBlock.SetColor("_BaseColor", c);
            }
            else if (st == 2) vrBlock.SetColor("_BaseColor", on ? new Color(0.3f, 1f, 0.55f, 0.75f) : new Color(0.25f, 0.85f, 0.5f, RowHot(r) ? 0.5f : 0.3f));
            else vrBlock.SetColor("_BaseColor", on ? new Color(1f, 0.72f, 0.2f, 0.6f) : (RowHot(r) ? new Color(0.6f, 0.85f, 1f, 0.3f) : new Color(1f, 1f, 1f, st == 3 ? 0.06f : 0.13f)));
            r.bg.SetPropertyBlock(vrBlock);
        }

        // ---- what the highlighted button does, and how to use the panel
        int shown = scroll ? max : items.Count; float hy = y0 - shown * 0.046f - 0.005f, room = hy - (-0.31f);
        bool showHint = room > 0.05f;
        if (vrHint.gameObject.activeSelf != showHint) vrHint.gameObject.SetActive(showHint);
        if (showHint)
        {
            string hint = (sel < items.Count && !string.IsNullOrEmpty(items[sel].hint) ? items[sel].hint + "\n" : "") + "<color=#9AD8FF>Point the laser and pull the trigger.  Stick: move.  A: choose.  B: back.  Y: close." + (voice && voice.Listening ? "  Mic on: just say it." : "") + "</color>";
            if (vrHint.text != hint) vrHint.text = hint;
            vrHint.rectTransform.sizeDelta = new Vector2(0.96f, room); vrHint.rectTransform.localPosition = new Vector3(0f, hy - room * 0.5f, 0f);
        }
    }

    static string HelpText(bool vr)
    {
        return vr
            ? "Every button has one job.\nRIGHT hand: trigger = select / use, grip = teleport (hold to aim, let go), A = jump, B = next tool, stick = turn.\nLEFT hand: stick = walk (click it to run), X = go to the next job, Y = this menu, trigger = select too.\nPoint the laser at a glowing orb, a dashboard button or a repair panel and pull the trigger. Jetpack is a button on the dashboard.\nKeep pushing against a door or wall to pass through. Another floor: press X, or Y and then Travel.\nTouch an astronaut's raised hand for a high five. Walk into gold stars to collect them.\nSwitch player: Y, then Switch player. The crew you are not playing work by themselves. Mirror astronaut and the live tracking board are under Base systems.\nThe game starts with NASA HUNCH mode off. Cheats and anti-cheat is its own page; in NASA HUNCH mode every cheat starts off.\nGiant portals by the mining field lead to five other moonbases, each with a game. A jump or a portal always takes you off a rover first."
            : "<b>Move</b>   WASD + mouse, Shift to run, Space to jump, F to fly (a cheat: off in NASA HUNCH mode until you switch it on)\n<b>Switch player</b>   F6 steps into the next crew member; the others work by themselves\n<b>Tools</b>   R changes the tool (ten of them), left click uses it. B dances, V is third-person view\n<b>Act</b>   E or left click while standing in a zone ring\n<b>Travel</b>   Tab / Q next and previous zone, 1-0 jump to a zone, G most urgent alert, PageUp / PageDown lift between floors\n<b>Doors</b>   They slide open when you come near. Walls are solid; walking through them is a cheat you can switch on (Menu, Cheats and anti-cheat).\n<b>Meeting</b>   F4, or the big round button on the table in the centre dome. The clock stops while you vote.\n<b>Other moonbases</b>   Full game: five giant portals by the mining field, each to a base with a game (Space Slicer, Zero-G Dome, Emote Stage, Star Shower, Target Tower).\n<b>Touch screen</b>   Left thumb stick walks (push it fully to run), drag on the right to turn and look, ACT, JUMP and MENU. The menu then shows only the big choices: NASA HUNCH mode, Travel, Switch player, Cheats, Base systems.\n<b>Talk</b>   T near an astronaut (or anywhere for ARIA). Pick a question, type a message and press Enter, or turn on Voice chat and speak.\n<b>Find your way</b>   Green arrows on the floor lead to where you are needed. The minimap (bottom right) has a compass and GPS read-out. N opens the full map: click a zone to be guided there. Or ask ARIA \"where is the greenhouse\".\n<b>Rocket</b>   L launches the supply rocket. If you fall a long way you are put back where you stood.\n<b>Menu</b>   Esc. <b>Photo mode</b> F1. <b>Help</b> H. <b>Pause</b> P. <b>Music</b> M. <b>Kid mode</b> K.\n<b>Gamepad</b>   A act, bumpers next / previous zone, X most urgent, B talk, Y high five, Start help.\n\n<b>Goal</b>   Answer ARIA's alerts in the right zone before the timer ends, and between alerts look after the crew: meals, exercise, plants, sleep, calls home. Gold stars and high fives are bonuses.\n\n<b>Voice chat phrases</b>   \"what should I do\", \"status report\", \"how is the crew\", \"go to the greenhouse\", \"take me there\", \"do it\", \"how are you\", \"what would help\", \"high five\", \"tell me a fact\", \"open menu\", \"resume\".";
    }

    // ------------------------------------------------------------------ desktop drawing (mouse and keys are read in Update)
    void Styles(float u)
    {
        if (sTitle != null && Mathf.Abs(sTitle.fontSize - HunchLook.FsF(58f * u)) < 1.5f) return;
        white = Texture2D.whiteTexture;
        sTitle = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(58f * u), fontStyle = FontStyle.Bold, richText = true, wordWrap = true };
        sItem = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(25f * u), richText = true, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, wordWrap = false };
        sText = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(21f * u), richText = true, wordWrap = true };
        sSmall = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(16f * u), richText = true, wordWrap = true };
        foreach (var s in new[] { sTitle, sItem, sText, sSmall }) s.normal.textColor = Color.white;
        if (HunchLook.Title) { sTitle.font = HunchLook.Title; sTitle.fontStyle = FontStyle.Normal; }
    }
    void Box(Rect r, Color c) { HunchUi.Round(r, c); }

    /// a STAR STORE row: a gold-to-violet card with a light sweeping across it and a few twinkling stars
    void StoreRow(Rect r, bool on, int i, float u)
    {
        float t = Time.unscaledTime;
        Color gold = new Color(1f, 0.74f, 0.14f), violet = new Color(0.62f, 0.24f, 0.98f), pink = new Color(1f, 0.38f, 0.72f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.4f + i);
        Box(new Rect(r.x - 4f * u, r.y - 4f * u, r.width + 8f * u, r.height + 8f * u), on ? new Color(1f, 0.95f, 0.7f, 0.95f) : Color.Lerp(gold, pink, pulse) * new Color(1f, 1f, 1f, 0.75f));      // glowing rim
        int strips = 14; float sw = r.width / strips;
        for (int k = 0; k < strips; k++)
        {
            float f = k / (float)(strips - 1); Color c = f < 0.55f ? Color.Lerp(gold, pink, f / 0.55f) : Color.Lerp(pink, violet, (f - 0.55f) / 0.45f);
            c *= on ? 0.86f : 0.64f; c.a = 1f;
            var sr = new Rect(r.x + k * sw, r.y, sw + 1f, r.height);
            if (k == 0 || k == strips - 1) HunchUi.Round(sr, c, r.height * 0.3f); else { GUI.color = c; GUI.DrawTexture(sr, Texture2D.whiteTexture); GUI.color = Color.white; }
        }
        float sweep = Mathf.Repeat(t * 0.45f + i * 0.13f, 1.6f) - 0.3f;      // a soft shine passes over the card now and then
        if (sweep > 0f && sweep < 1f) { GUI.color = new Color(1f, 1f, 1f, 0.22f); GUI.DrawTexture(new Rect(r.x + sweep * r.width - 30f * u, r.y + 2f * u, 60f * u, r.height - 4f * u), Texture2D.whiteTexture); GUI.color = Color.white; }
        for (int s = 0; s < 4; s++)
        {
            float tw = 0.5f + 0.5f * Mathf.Sin(t * (3f + s) + s * 1.7f + i); float d = (5f + tw * 5f) * u;
            float sx = r.x + r.width * (0.62f + 0.1f * s), sy = r.y + r.height * (s % 2 == 0 ? 0.28f : 0.7f);
            HunchUi.Round(new Rect(sx - d * 0.5f, sy - d * 0.5f, d, d), new Color(1f, 1f, 0.85f, 0.4f + tw * 0.6f), d * 0.5f);
        }
        HunchUi.Round(new Rect(r.x + 6f * u, r.y + r.height * 0.5f - 8f * u, 16f * u, 16f * u), new Color(1f, 0.96f, 0.6f, 1f), 8f * u);      // the star dot
    }

    /// +1 when a menu line ends in an "on" word, -1 for an "off" word, 0 when it is not a switch
    static int SwitchState(string label)
    {
        int cut = label.LastIndexOf(": "); if (cut < 0) return 0;
        string t = System.Text.RegularExpressions.Regex.Replace(label.Substring(cut + 2), "<.*?>", "").Trim().ToLowerInvariant();
        if (t == "on" || t == "enabled" || t == "yes") return 1;
        if (t == "off" || t == "disabled" || t == "no") return -1;
        return 0;
    }

    void OnGUI()
    {
        if (game.HeadsetActive || HunchIntro.Active || Event.current.type != EventType.Repaint) return;
        float u = Screen.height / 1080f, W = Screen.width, H = Screen.height; Styles(u);

        if (page == Page.None)
        {
            if (game.hideHud || game.uiHasMouse || game.simpleHud || sim.phase == HabPhase.Briefing || sim.phase == HabPhase.Report) return;
            // ARIA's advice and forecast, top centre
            if (game.advisorLine.Length > 0)
            {
                float w = Mathf.Min(900f * u, W - 2f * 500f * u);
                if (w > 300f * u) { var r = new Rect((W - w) * 0.5f, 12f * u, w, 96f * u); Box(r, new Color(0.02f, 0.07f, 0.06f, 0.8f)); GUI.Label(new Rect(r.x + 12f * u, r.y + 6f * u, r.width - 24f * u, r.height - 10f * u), "<color=#B8F5C8>" + game.advisorLine + "</color>", sSmall); }
            }
            string tip = "Esc menu    T talk    N map    L rocket" + (voice && voice.Listening ? "    <color=#7CFC9A>Mic on</color>" + (voice.LastHeard.Length > 0 ? ": \"" + voice.LastHeard + "\"" : "") : "");
            GUI.Label(new Rect(W * 0.5f - 300f * u, H - 216f * u, 600f * u, 24f * u), "<color=#C8D4E8>" + tip + "</color>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
            return;
        }

        if (page == Page.Talk) { DrawTalk(u); return; }

        Box(new Rect(0f, 0f, W, H), new Color(0.01f, 0.02f, 0.05f, page == Page.Main ? 0.62f : 0.74f));
        float left = Mathf.Min(110f * u, W * 0.06f);
        if (page == Page.Main)
        {
            GUI.Label(new Rect(left, 90f * u, W - 2f * left, 76f * u), "LUNAR HABITAT COMMANDER", sTitle);
            GUI.Label(new Rect(left, 168f * u, W - 2f * left, 34f * u), "<color=#9AD8FF>NASA HUNCH  -  Lunar Smart Adaptive Habitat with AI Psychological Monitoring</color>", sText);
            GUI.Label(new Rect(left, 204f * u, W - 2f * left, 60f * u), "<color=#C8D4E8>Crew of 4   30 days   16 zones   Lunar south pole</color>", sText);
        }
        else GUI.Label(new Rect(left, 50f * u, W - 2f * left, 76f * u), page == Page.Options ? "OPTIONS" : (page == Page.Help ? "HOW TO PLAY" : (page == Page.Extra && extra != null ? extra.title : "PAUSED")), sTitle);

        for (int i = 0; i < items.Count; i++)
        {
            Rect r = ItemRect(i); bool on = i == sel; if (r.x < -1000f) continue;
            { float wave = Mathf.Sin(Time.unscaledTime * 2.2f + i * 0.55f) * 2.5f * u + (on ? Mathf.Cos(Time.unscaledTime * 5f) * 4f * u : 0f); r.x += wave + (on ? 8f * u : 0f); }      // the rows sway like a slow wave; the chosen one leans out and bobs
            // friendly rows: soft rounded cards, a warm highlight on the chosen one, and a little switch for anything that is on or off
            // colour says how often a line is used: green at the top (most used), yellow in the middle, pink at the bottom
            float tt = items.Count > 1 ? i / (float)(items.Count - 1) : 0f;
            Color g0 = new Color(0.38f, 0.92f, 0.52f), y0 = new Color(1f, 0.86f, 0.36f), p0 = new Color(1f, 0.56f, 0.78f);
            Color tint = tt < 0.5f ? Color.Lerp(g0, y0, tt * 2f) : Color.Lerp(y0, p0, (tt - 0.5f) * 2f);
            int st = items[i].style;
            if (st == 1) { StoreRow(r, on, i, u); }
            else
            {
                if (st == 2) tint = new Color(0.35f, 0.95f, 0.55f); else if (st == 3) tint = new Color(0.62f, 0.68f, 0.8f);
                if (on) Box(new Rect(r.x - 3f * u, r.y - 3f * u, r.width + 6f * u, r.height + 6f * u), new Color(1f, 1f, 1f, 0.85f));      // a white rim shows which line is chosen
                Box(r, new Color(tint.r * (on ? 0.62f : 0.34f), tint.g * (on ? 0.62f : 0.34f), tint.b * (on ? 0.62f : 0.34f), st == 3 && !on ? 0.5f : (on ? 0.98f : 0.86f)));
                Box(new Rect(r.x + 7f * u, r.y + r.height * 0.5f - 6f * u, 12f * u, 12f * u), tint);
            }
            string lab = items[i].label; int sw = SwitchState(lab); float pad = sw != 0 ? 86f * u : 24f * u;
            if (sw != 0) { int cut = lab.LastIndexOf(": "); if (cut > 0) lab = lab.Substring(0, cut); }
            if (st == 1) { string plain = System.Text.RegularExpressions.Regex.Replace(lab, "</?color[^>]*>", ""); GUI.Label(new Rect(r.x + 30f * u, r.y + 2f * u, r.width - pad - 22f * u, r.height), "<color=#2A0A3AB0>" + (on ? "<b>" + plain + "</b>" : plain) + "</color>", sItem); }      // a soft shadow keeps the words readable on gold
            GUI.Label(new Rect(r.x + 28f * u, r.y, r.width - pad - 22f * u, r.height), (on ? "<color=#FFFFFF><b>" : "<color=#F2F6FF>") + lab + (on ? "</b></color>" : "</color>"), sItem);
            if (sw != 0) { float sh = Mathf.Min(r.height - 10f * u, 30f * u); HunchUi.Switch(new Rect(r.xMax - 70f * u, r.y + (r.height - sh) * 0.5f, 56f * u, sh), sw > 0); }
        }
        if (page == Page.Help)
        {
            var hr = new Rect(left, 140f * u, W - 2f * left, H - 320f * u); Box(hr, new Color(0.02f, 0.04f, 0.09f, 0.7f));
            GUI.Label(new Rect(hr.x + 20f * u, hr.y + 14f * u, hr.width - 40f * u, hr.height - 20f * u), HelpText(false), sText);
        }
        else if (sel < items.Count && !string.IsNullOrEmpty(items[sel].hint))
        {
            // right side: what the highlighted item does
            float rx = ItemRect(0).xMax + 50f * u, rw = W - rx - left;
            if (rw > 240f * u) { var hr = new Rect(rx, ItemRect(0).y, rw, 240f * u); Box(hr, new Color(0.02f, 0.04f, 0.09f, 0.7f)); GUI.Label(new Rect(hr.x + 20f * u, hr.y + 14f * u, hr.width - 40f * u, hr.height - 20f * u), items[sel].hint, sText); }
        }
        if (listVisible < items.Count && items.Count > 0)
        {
            // scroll bar beside the list
            Rect a0 = ItemRect(listFirst), a1 = ItemRect(listFirst + listVisible - 1); float bx = a0.xMax + 14f * u, by = a0.y, bh = a1.yMax - a0.y;
            HunchUi.Round(new Rect(bx, by, 10f * u, bh), new Color(1f, 1f, 1f, 0.16f), 5f * u);
            float th = Mathf.Max(30f * u, bh * listVisible / items.Count), ty = by + (bh - th) * (listFirst / (float)Mathf.Max(1, items.Count - listVisible));
            HunchUi.Round(new Rect(bx, ty, 10f * u, th), new Color(0.45f, 0.9f, 0.6f, 0.95f), 5f * u);
        }
        if (page == Page.Main)
            GUI.Label(new Rect(W * 0.5f, H - 92f * u, W * 0.5f - left, 30f * u), "<color=#FFE27A><b>" + Copyright + "</b></color>   <color=#B8C4DA>" + Thanks + "</color>", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleRight });
        { Rect x = CloseRect(); float pulse = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 4f); var xr = new Rect(x.center.x - x.width * 0.5f * pulse, x.center.y - x.height * 0.5f * pulse, x.width * pulse, x.height * pulse); HunchUi.Round(xr, new Color(1f, 0.42f, 0.5f, 0.95f), xr.height * 0.5f); GUI.Label(xr, "<b>X</b>", new GUIStyle(sItem) { alignment = TextAnchor.MiddleCenter }); }
        GUI.Label(new Rect(left, H - 54f * u, W - 2f * left, 30f * u), "<color=#9AD8FF>Mouse or Up / Down to choose     Click or Enter to select     Esc or the X: back" + (listVisible < items.Count ? "     Wheel: scroll" : "") + "</color>", sSmall);
    }

    void DrawTalk(float u)
    {
        Rect p = TalkPanel(); Box(p, new Color(0.02f, 0.04f, 0.09f, 0.96f)); Box(new Rect(p.x, p.y, p.width, 4f * u), new Color(0.6f, 0.85f, 1f, 0.9f));
        bool crew = talkWith >= 0 && talkWith < sim.crew.Count;
        string head = "<b>Talking with " + Speaker(talkWith) + "</b>";
        if (crew) { var c = sim.crew[talkWith]; head += "   <size=" + sSmall.fontSize + "><color=#C8D4E8>" + c.role + "   " + HabitatSim.ClassName(c.Class) + "   HR " + c.HeartRate + "   stress " + Mathf.RoundToInt(c.stress) + "   mood " + Mathf.RoundToInt(c.mood) + "   sleep " + Mathf.RoundToInt(c.sleep) + "</color></size>"; }
        else head += "   <size=" + sSmall.fontSize + "><color=#C8D4E8>Habitat AI   crew monitoring, forecasts and advice</color></size>";
        GUI.Label(new Rect(p.x + 20f * u, p.y + 12f * u, p.width - 40f * u, 40f * u), head, sItem);

        float lw = p.width - 500f * u; var sb = new StringBuilder();
        for (int i = Mathf.Max(0, log.Count - 6); i < log.Count; i++) sb.Append(log[i].StartsWith("<b>You") ? "<color=#FFE08A>" : (log[i].StartsWith("<i>") ? "<color=#7CFC9A>" : "<color=#E6ECF8>")).Append(log[i]).Append("</color>\n");
        var logStyle = new GUIStyle(sText) { alignment = TextAnchor.LowerLeft, fontSize = HunchLook.Fs(19f * u) };
        GUI.Label(new Rect(p.x + 20f * u, p.y + 58f * u, lw, p.height - 136f * u), sb.ToString(), logStyle);

        var tb = new Rect(p.x + 20f * u, p.yMax - 66f * u, lw, 48f * u); Box(tb, new Color(1f, 1f, 1f, 0.1f));
        bool blink = (Time.unscaledTime * 2f) % 2f < 1f;
        bool hearing = voice && voice.Dictating; string hyp = hearing ? voice.Hypothesis : "";
        GUI.Label(new Rect(tb.x + 12f * u, tb.y, tb.width - 20f * u, tb.height), typed.Length > 0 ? typed + (blink ? "|" : "") : (hyp.Length > 0 ? "<color=#7CFC9A>" + hyp + " ...</color>" : "<color=#8A96AA>Type a message and press Enter" + (hearing ? ", or just speak (microphone is listening)" : (voice && voice.Listening ? ", or say a command" : "")) + (blink ? " |" : "") + "</color>"), sItem);

        for (int i = 0; i < items.Count; i++)
        {
            Rect r = ItemRect(i); bool on = i == sel;
            Box(r, on ? new Color(1f, 0.82f, 0.35f, 0.28f) : new Color(1f, 1f, 1f, 0.08f));
            GUI.Label(new Rect(r.x + 12f * u, r.y, r.width - 16f * u, r.height), (on ? "<color=#FFE08A>" : "<color=#E6ECF8>") + (i + 1) + "   " + items[i].label + "</color>", new GUIStyle(sText) { alignment = TextAnchor.MiddleLeft });
        }
    }

    // ------------------------------------------------------------------ hooks for the development test helper
    public void ProbeExplore() { Close(); game.StartMission(true); }
    public void ProbeSurvival() { Close(); game.StartMission(false); }
    public void ProbeMain() { Open(Page.Main); }
    public void ProbePause() { Open(Page.Pause); }
    public void ProbeOptions() { Open(Page.Options); }
    public void ProbeHelp() { Open(Page.Help); }
    public void ProbeTalk() { OpenTalk(false); }
    public void ProbeTalkAria() { OpenTalk(true); }
    public void ProbeDown() { Move(1); }
    public void ProbeSelect() { Select(); }
    public void ProbeChat() { typed = probeText; Submit(); }
    public void ProbeClose() { Close(); }
    public string ProbeState() { return "font=" + (look ? look.FontName : "?") + " dict=" + (voice ? voice.Dictating + " " + voice.DictationProblem : "") + " page=" + page + " sel=" + sel + " items=" + items.Count + " talkWith=" + talkWith + " log=" + (log.Count > 0 ? log[log.Count - 1] : "") + " speak=" + (voice ? voice.SpeakAvailable.ToString() : "novoice") + " listen=" + (voice ? voice.Listening + " " + voice.ListenProblem : ""); }
}
