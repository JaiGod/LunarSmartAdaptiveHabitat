// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/03_Player_Controls/HunchTouch.cs
//  Section : 03_Player_Controls
//  Folder  : How you move and find your way: VR controllers, keyboard/mouse, phone touch controls,
//            doorway help, NavMesh paths, green guide arrows + minimap.
//  Purpose : Mobile controls: the game on a touch screen with very few buttons. - left thumb: a
//            stick to walk (push it all the way to run)
//  Why     : Chat (Unity Play): mobile controls switched on for this game.
//  Contains: HunchTouch, Finger
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using UnityEngine;
using UnityEngine.InputSystem;

/// Mobile controls: the game on a touch screen with very few buttons.
///   - left thumb: a stick to walk (push it all the way to run)
///   - right thumb: drag anywhere to look around
///   - three buttons only: ACT (do the job here, or use what you aim at), JUMP, and MENU
/// Everything else (travel, autopilot, The Show, sleep, options) is reached through MENU, where a tap chooses a line.
/// It switches itself on when the game runs on a phone or tablet, and can be switched on by hand on a computer
/// (Menu > Base systems > Mobile controls) where the mouse then plays the part of one finger.
public class HunchTouch : MonoBehaviour
{
    public static HunchTouch I;
    public bool on; public string Info = "";
    HabitatGame game; HunchDesktopFallback desk; HunchMenu menu; HunchFrontier frontier;
    int stickId = -1, lookId = -1; Vector2 stickFrom, stickNow, lookLast; float actFlash, jumpFlash, menuFlash, demoUntil;
    GUIStyle sBtn; Texture2D disc;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var g = FindFirstObjectByType<HabitatGame>();
        if (g && !g.GetComponent<HunchTouch>() && !Application.isBatchMode) g.gameObject.AddComponent<HunchTouch>();
    }
    void Awake()
    {
        I = this; game = GetComponent<HabitatGame>();
        on = PlayerPrefs.GetInt("hunch_mobile", Application.isMobilePlatform ? 1 : 0) == 1;
        const int n = 64; disc = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f); disc.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 12f))); }
        disc.Apply();
    }
    public void Set(bool value) { on = value; PlayerPrefs.SetInt("hunch_mobile", on ? 1 : 0); if (!on && desk) { desk.padOn = false; desk.padMove = Vector2.zero; } if (on) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }

    // ---- where things are on the screen (also used to decide what a finger touched)
    float U { get { return Mathf.Min(Screen.height / 1080f, Screen.width / 1500f); } }
    Rect ActRect { get { float u = U; return new Rect(Screen.width - 250f * u, Screen.height - 290f * u, 200f * u, 200f * u); } }
    Rect JumpRect { get { float u = U; return new Rect(Screen.width - 430f * u, Screen.height - 190f * u, 140f * u, 140f * u); } }
    Rect MenuRect { get { float u = U; return new Rect(Screen.width - 190f * u, 24f * u, 160f * u, 90f * u); } }
    Vector2 StickHome { get { float u = U; return new Vector2(220f * u, Screen.height - 230f * u); } }
    float StickReach { get { return 120f * U; } }

    struct Finger { public int id; public Vector2 pos; public bool began, ended; }
    readonly System.Collections.Generic.List<Finger> fingers = new System.Collections.Generic.List<Finger>();
    void ReadFingers()
    {
        fingers.Clear(); var ts = Touchscreen.current;
        if (ts != null) foreach (var t in ts.touches)
        {
            var ph = t.phase.ReadValue(); if (ph == UnityEngine.InputSystem.TouchPhase.None) continue;
            Vector2 p = t.position.ReadValue(); p.y = Screen.height - p.y;
            fingers.Add(new Finger { id = t.touchId.ReadValue(), pos = p, began = ph == UnityEngine.InputSystem.TouchPhase.Began, ended = ph == UnityEngine.InputSystem.TouchPhase.Ended || ph == UnityEngine.InputSystem.TouchPhase.Canceled });
        }
        var m = Mouse.current;      // on a computer the mouse is one finger
        if (fingers.Count == 0 && m != null && (m.leftButton.isPressed || m.leftButton.wasReleasedThisFrame))
        {
            Vector2 p = m.position.ReadValue(); p.y = Screen.height - p.y;
            fingers.Add(new Finger { id = 9999, pos = p, began = m.leftButton.wasPressedThisFrame, ended = m.leftButton.wasReleasedThisFrame });
        }
    }

    void Update()
    {
        if (!desk) desk = GetComponent<HunchDesktopFallback>(); if (!menu) menu = GetComponent<HunchMenu>(); if (!frontier) frontier = GetComponent<HunchFrontier>();
        if (!on || !desk || !desk.active || (game && game.HeadsetActive)) { if (desk) desk.padOn = false; return; }
        desk.padOn = true; ReadFingers();
        if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        bool busy = game.menuOpen || HunchIntro.Active;
        foreach (var f in fingers)
        {
            if (f.began)
            {
                if (HunchIntro.Active) { HunchIntro.Skip(); continue; }
                if (MenuRect.Contains(f.pos)) { menuFlash = 0.2f; if (menu) menu.VrMenuButton(); continue; }
                if (busy) continue;                                             // the menu handles its own taps
                if (ActRect.Contains(f.pos)) { actFlash = 0.2f; Act(); continue; }
                if (JumpRect.Contains(f.pos)) { jumpFlash = 0.2f; desk.padJump = true; continue; }
                if (f.pos.x < Screen.width * 0.42f && f.pos.y > Screen.height * 0.35f && stickId < 0) { stickId = f.id; stickFrom = f.pos; stickNow = f.pos; continue; }
                if (lookId < 0) { lookId = f.id; lookLast = f.pos; }
            }
            if (f.id == stickId) { stickNow = f.pos; if (f.ended) stickId = -1; }
            if (f.id == lookId) { Vector2 d = f.pos - lookLast; lookLast = f.pos; desk.padLook += new Vector2(d.x, -d.y) * (1.6f * 1080f / Mathf.Max(600f, Screen.height)); if (f.ended) lookId = -1; }
        }
        // a finger that vanished without an "ended" (it left the screen)
        if (stickId >= 0 && !fingers.Exists(x => x.id == stickId)) stickId = -1;
        if (lookId >= 0 && !fingers.Exists(x => x.id == lookId)) lookId = -1;
        Vector2 v = stickId >= 0 && !busy ? (stickNow - stickFrom) / StickReach : Vector2.zero; v.y = -v.y;
        desk.padMove = Time.time < demoUntil ? new Vector2(0f, 1f) : Vector2.ClampMagnitude(v, 1f);
        actFlash -= Time.unscaledDeltaTime; jumpFlash -= Time.unscaledDeltaTime; menuFlash -= Time.unscaledDeltaTime;
    }

    /// the one action button: the job of the zone you stand in, or whatever you are aiming at
    void Act()
    {
        if (!game) return;
        if (frontier) frontier.probeHold = Time.time + 0.12f;                    // "pulls the trigger" on a panel, pod, pick-up or portal in front of you
        if (!(game.toolInUse != null && game.toolInUse())) game.DoInteract();
    }

    void OnGUI()
    {
        if (!on || !desk || !desk.active || (game && game.HeadsetActive) || Event.current.type != EventType.Repaint) return;
        float u = U; if (sBtn == null || Mathf.Abs(sBtn.fontSize - HunchLook.FsF(34f * u)) > 1f) { sBtn = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(34f * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; sBtn.normal.textColor = Color.white; }
        Button(MenuRect, "MENU", new Color(0.1f, 0.14f, 0.28f, 0.75f), menuFlash > 0f, false);
        if (game.menuOpen || HunchIntro.Active) return;
        // the stick
        Vector2 home = stickId >= 0 ? stickFrom : StickHome; float reach = StickReach;
        GUI.color = new Color(1f, 1f, 1f, 0.16f); GUI.DrawTexture(new Rect(home.x - reach * 1.25f, home.y - reach * 1.25f, reach * 2.5f, reach * 2.5f), disc);
        Vector2 knob = home + (stickId >= 0 ? Vector2.ClampMagnitude(stickNow - stickFrom, reach) : Vector2.zero);
        GUI.color = new Color(1f, 0.85f, 0.3f, 0.6f); GUI.DrawTexture(new Rect(knob.x - reach * 0.55f, knob.y - reach * 0.55f, reach * 1.1f, reach * 1.1f), disc); GUI.color = Color.white;
        Button(ActRect, "ACT", new Color(0.1f, 0.55f, 0.25f, 0.7f), actFlash > 0f, true);
        Button(JumpRect, "JUMP", new Color(0.15f, 0.3f, 0.6f, 0.65f), jumpFlash > 0f, true);
    }
    void Button(Rect r, string text, Color c, bool lit, bool round)
    {
        if (lit) c = Color.Lerp(c, Color.white, 0.45f);
        GUI.color = c; GUI.DrawTexture(r, round ? (Texture)disc : Texture2D.whiteTexture); GUI.color = Color.white; GUI.Label(r, text, sBtn);
    }

    public string ProbeOn() { Set(true); return "mobile controls " + on; }
    public string ProbeOff() { Set(false); return "mobile controls " + on; }
    /// test hook: hold the stick forward for a moment and press ACT, as two fingers would
    public string ProbeWalk() { if (!desk) return "no desk"; StartCoroutine(Demo()); return "walking"; }
    System.Collections.IEnumerator Demo() { demoUntil = Time.time + 1.5f; Vector3 a = transform.position; var o = game.origin ? game.origin.transform.position : a; while (Time.time < demoUntil) yield return null; Info = "moved " + ((game.origin ? game.origin.transform.position : a) - o).magnitude.ToString("F1") + " m"; Act(); }
    public string ProbeInfo() { return "on=" + on + " padOn=" + (desk && desk.padOn) + " " + Info; }
}
