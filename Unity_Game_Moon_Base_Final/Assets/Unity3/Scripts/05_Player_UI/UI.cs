// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/05_Player_UI/UI.cs
//  Section : 05_Player_UI
//  Folder  : The player rig (VR + desktop), the HUD and every pop-up panel.
//  Purpose : kept for callers that still ask for a per-label colour: now always the console base
//            (no rainbow)
//  Contains: UIKit, HUD, RainbowEdge
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Hunch3
{
    public static class UIKit
    {
        static Font font;
        // kid-friendly rounded font from the operating system (Comic Sans MS on Windows); falls back to the built-in font (e.g. on Quest)
        public static Font F
        {
            get
            {
                if (font) return font;
                try
                {
                    var have = Font.GetOSInstalledFontNames();
                    foreach (var n in new[] { "Comic Sans MS", "Segoe Print", "Chalkboard SE", "Trebuchet MS" })
                        if (have != null && System.Array.IndexOf(have, n) >= 0) { font = Font.CreateDynamicFontFromOSFont(new[] { n, "Arial" }, 24); break; }
                }
                catch { }
                if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }
        public static readonly Color Bg = new Color(0.04f, 0.07f, 0.14f, 0.88f), Accent = new Color(0.25f, 0.85f, 1f), Gold = new Color(1f, 0.78f, 0.3f), Good = new Color(0.3f, 1f, 0.55f), Warn = new Color(1f, 0.6f, 0.15f), Bad = new Color(1f, 0.3f, 0.25f);

        public static Canvas Canvas(string name, bool world, Vector2 size, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform)); if (parent) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            if (world) { c.renderMode = RenderMode.WorldSpace; go.GetComponent<RectTransform>().sizeDelta = size; go.transform.localScale = Vector3.one * 0.001f; c.worldCamera = Camera.main; }
            else { c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 10; var cs = go.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f; }
            go.AddComponent<GraphicRaycaster>();
            if (world) go.AddComponent<TrackedDeviceGraphicRaycaster>();
            return c;
        }
        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = anchor; r.pivot = pivot ?? anchor; r.anchoredPosition = pos; r.sizeDelta = size; return r;
        }
        public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color? col = null, Vector2? pivot = null)
        {
            var r = Rect(parent, name, anchor, pos, size, pivot); var i = r.gameObject.AddComponent<Image>(); i.color = col ?? Bg; return i;
        }
        public static Text Label(Transform parent, string text, int size, Color col, Vector2 anchor, Vector2 pos, Vector2 box, TextAnchor align = TextAnchor.UpperLeft, Vector2? pivot = null)
        {
            var r = Rect(parent, "Text", anchor, pos, box, pivot); var t = r.gameObject.AddComponent<Text>();
            t.font = F; t.fontSize = size; t.color = col; t.alignment = align; t.text = text; t.supportRichText = true; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false; return t;
        }
        // Mission-console look: flat dark slate, a thin steel edge, white text. No gradients, no animation.
        // A caller's explicit colour (red STOP, green confirm, selected tab) is kept but toned down so it reads as a status, not a toy.
        public static readonly Color BtnBase = new Color(0.13f, 0.17f, 0.23f, 0.96f), BtnEdge = new Color(0.45f, 0.55f, 0.65f, 0.9f);
        public static Button Btn(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, Color? col = null, int fs = 26)
        {
            Color fill = col.HasValue ? Color.Lerp(BtnBase, col.Value, 0.55f) : BtnBase; fill.a = 0.96f;
            var img = Panel(parent, "Btn_" + label, anchor, pos, size, fill);
            Round(img, 2.2f);                                                                               // small 4-px-ish corner radius
            var ol = img.gameObject.AddComponent<Outline>(); ol.effectDistance = new Vector2(1, -1); ol.effectColor = BtnEdge;
            var b = img.gameObject.AddComponent<Button>(); var cb = b.colors;
            cb.normalColor = Color.white; cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f); cb.pressedColor = new Color(0.75f, 0.75f, 0.75f); cb.selectedColor = Color.white; cb.fadeDuration = 0.08f; b.colors = cb;
            b.onClick.AddListener(() => { try { onClick?.Invoke(); } catch (Exception e) { Debug.LogException(e); } });
            var t = Label(img.transform, label, fs, new Color(0.93f, 0.96f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(14, 6), TextAnchor.MiddleCenter);
            return b;
        }
        /// kept for callers that still ask for a per-label colour: now always the console base (no rainbow)
        public static Color Fun(string label) => BtnBase;

        // rounded-rectangle sprite (9-sliced, so any button size keeps the same round corners)
        static Sprite round;
        public static Sprite RoundSprite
        {
            get
            {
                if (round) return round;
                const int S = 64; const float R = 24f; var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, R, S - R), cy = Mathf.Clamp(y + 0.5f, R, S - R);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)(255 * Mathf.Clamp01(R - d + 0.5f));
                    px[y * S + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px); tex.Apply();
                round = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(R, R, R, R));
                return round;
            }
        }
        /// round the corners of any panel/button (bigger mul = smaller corners)
        public static Image Round(Image img, float mul = 1f)
        {
            if (!img) return img;
            img.sprite = RoundSprite; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = mul; return img;
        }
        public static string Bar(float v, int n = 10) { int k = Mathf.Clamp(Mathf.RoundToInt(v * n), 0, n); return new string('|', k) + new string('.', n - k); }
        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        public static Color TierCol(int t) => t >= 3 ? Bad : t == 2 ? Warn : t == 1 ? Gold : Good;
    }

    public class HUD : MonoBehaviour
    {
        public static HUD I;
        Canvas canvas; Text stats, toast, prompt, procedure, zoneCard, help, autoBanner, navText; Image offerBox, fade, bigMapBg; Text offerText; RawImage mini, bigMap;
        Camera mapCam; RenderTexture rt; Transform arrow; readonly List<GameObject> crumbs = new List<GameObject>();
        Text compass; Transform youMarker;
        Offer currentOffer; float toastUntil; public bool showZoneCards = false; string navGoal = "", navLabel = "";
        bool world;

        void Awake() { I = this; }
        void Start()
        {
            var pr = PlayerRig.I; world = pr && pr.isXR;
            canvas = world ? UIKit.Canvas("HUD_World", true, new Vector2(1600, 900), pr.cam.transform) : UIKit.Canvas("HUD", false, Vector2.zero);
            if (world) { canvas.transform.localPosition = new Vector3(0, 0, 1.1f); canvas.transform.localScale = Vector3.one * 0.00085f; }
            var root = canvas.transform;
            Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1), BC = new Vector2(0.5f, 0), TC = new Vector2(0.5f, 1), C = new Vector2(0.5f, 0.5f);
            var sp = UIKit.Panel(root, "StatsBg", TL, new Vector2(16, -16), new Vector2(430, 330), null, TL);
            stats = UIKit.Label(sp.transform, "", 19, Color.white, TL, new Vector2(12, -8), new Vector2(410, 320), TextAnchor.UpperLeft, TL);
            // minimap under the stats
            rt = new RenderTexture(512, 512, 16) { name = "MinimapRT" };
            var mm = UIKit.Panel(root, "MiniBg", TL, new Vector2(16, -354), new Vector2(300, 300), new Color(0, 0, 0, 0.8f), TL);
            mini = UIKit.Rect(mm.transform, "Mini", C, Vector2.zero, new Vector2(290, 290)).gameObject.AddComponent<RawImage>(); mini.texture = rt;
            UIKit.Label(mm.transform, "<b>MAP</b>  (M = big map)", 15, UIKit.Accent, TL, new Vector2(6, -4), new Vector2(280, 22), TextAnchor.UpperLeft, TL);
            navText = UIKit.Label(root, "", 22, UIKit.Accent, TL, new Vector2(16, -660), new Vector2(520, 60), TextAnchor.UpperLeft, TL);
            autoBanner = UIKit.Label(root, "", 30, UIKit.Gold, TC, new Vector2(0, -12), new Vector2(900, 44), TextAnchor.UpperCenter, TC);
            compass = UIKit.Label(root, "", 64, UIKit.Gold, TC, new Vector2(0, -150), new Vector2(90, 90), TextAnchor.MiddleCenter);
            procedure = UIKit.Label(root, "", 26, Color.white, TC, new Vector2(0, -60), new Vector2(1100, 90), TextAnchor.UpperCenter, TC);
            toast = UIKit.Label(root, "", 24, Color.white, BC, new Vector2(0, 200), new Vector2(1200, 110), TextAnchor.LowerCenter, BC);
            prompt = UIKit.Label(root, "", 26, UIKit.Gold, C, new Vector2(0, -70), new Vector2(800, 40), TextAnchor.MiddleCenter);
            UIKit.Label(root, "+", 30, new Color(1, 1, 1, 0.6f), C, Vector2.zero, new Vector2(40, 40), TextAnchor.MiddleCenter);
            var zc = UIKit.Panel(root, "ZoneCard", TR, new Vector2(-16, -16), new Vector2(460, 230), null, TR);
            zoneCard = UIKit.Label(zc.transform, "", 19, Color.white, TL, new Vector2(12, -8), new Vector2(440, 220), TextAnchor.UpperLeft, TL);
            zc.gameObject.SetActive(showZoneCards);      // simple screen: room info only on Z
            offerBox = UIKit.Panel(root, "Offer", BC, new Vector2(0, 82), new Vector2(980, 110), new Color(0.12f, 0.1f, 0.04f, 0.93f), BC);
            offerText = UIKit.Label(offerBox.transform, "", 22, Color.white, TL, new Vector2(12, -8), new Vector2(700, 96), TextAnchor.UpperLeft, TL);
            UIKit.Btn(offerBox.transform, "Yes (Y / A)", new Vector2(1, 0.5f), new Vector2(-190, 0), new Vector2(150, 70), () => AnswerOffer(true), new Color(0.15f, 0.5f, 0.25f), 20);
            UIKit.Btn(offerBox.transform, "No (R)", new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(130, 70), () => AnswerOffer(false), new Color(0.45f, 0.15f, 0.15f), 20);
            offerBox.gameObject.SetActive(false);
            var hp = UIKit.Panel(root, "Help", C, Vector2.zero, new Vector2(1180, 760));
            help = UIKit.Label(hp.transform, HelpText, 21, Color.white, TL, new Vector2(18, -14), new Vector2(1150, 740), TextAnchor.UpperLeft, TL);
            hp.gameObject.SetActive(false);
            bigMapBg = UIKit.Panel(root, "BigMap", C, Vector2.zero, new Vector2(860, 860), new Color(0, 0, 0, 0.92f));
            bigMap = UIKit.Rect(bigMapBg.transform, "Map", C, new Vector2(0, -10), new Vector2(820, 820)).gameObject.AddComponent<RawImage>(); bigMap.texture = rt;
            UIKit.Label(bigMapBg.transform, "<b>HABITAT MAP</b> — you are the gold arrow. N = choose a destination.", 20, UIKit.Accent, TC, new Vector2(0, -2), new Vector2(820, 26), TextAnchor.UpperCenter, TC);
            bigMapBg.gameObject.SetActive(false);
            // always-visible shortcut bar (desktop) / controller cheat-sheet (VR) — K hides it
            keysBg = UIKit.Panel(root, "KeysBar", BC, new Vector2(0, 4), new Vector2(1880, world ? 96 : 86), new Color(0.05f, 0.05f, 0.12f, 0.82f), BC);
            keys = UIKit.Label(keysBg.transform, world ? KeysVR : KeysSimple, world ? 17 : 22, Color.white, C, Vector2.zero, new Vector2(1860, 84), TextAnchor.MiddleCenter);
            if (!world) { var kr = keysBg.rectTransform; kr.sizeDelta = new Vector2(1180, 48); keys.rectTransform.sizeDelta = new Vector2(1160, 46); }
            fade = UIKit.Panel(root, "Fade", C, Vector2.zero, new Vector2(4000, 4000), new Color(0, 0, 0, 0)); fade.raycastTarget = false;
            BuildMapCam(); BuildArrow(); BuildHologram();
            Sim.OfferRaised += o => { currentOffer = o; };
            Sim.Toast += s => Toast(s);
            if (GameState.mode == GameMode.Sandbox) Toast("Welcome, Commander. Just press ENTER — the task at the top of the screen gets done with you.");
        }

        Image keysBg; Text keys;
        static string K(string key, string what, string hex) => $"<color=#{hex}><b>[{key}]</b></color> {what}";
        public static readonly string KeysDesktop =
            K("W A S D", "move", "4FC3F7") + "   " + K("Mouse", "look (L lock)", "4FC3F7") + "   " + K("E", "use / grab ladder / sleep", "81C784") + "   " + K("Space / X", "stand up · jump", "81C784") + "   " +
            K("Q", "menu", "FFB74D") + "   " + K("Tab", "crew UI", "FFB74D") + "   " + K("H", "help", "FFB74D") + "   " + K("M", "map", "FFB74D") + "   " + K("N", "go to room", "FFB74D") + "   " + K("P", "AI autopilot", "BA68C8") + "   " +
            K("G", "wave", "F06292") + "   " + K("T", "time speed", "F06292") + "\n" +
            K("F1", "AI dashboard", "4DD0E1") + "   " + K("F2", "CMO", "4DD0E1") + "   " + K("F3", "meeting", "4DD0E1") + "   " + K("F4", "tabletop", "4DD0E1") + "   " + K("F5", "board Qs", "4DD0E1") + "   " + K("I", "CHECK ON CREW", "F48FB1") + "   " +
            K("F6", "safety drill", "E57373") + "   " + K("F7", "quick travel", "AED581") + "   " + K("F8", "ladder test", "AED581") + "   " + K("F10", "outside cameras", "AED581") + "   " + K("U", "unstuck", "FFD54F") + "   " + K("K", "hide this bar", "B0BEC5") + "\n" +
            K("J", "rubric tasks", "FFD54F") + "   " + K("V", "crew health", "4DD0E1") + "   " + K("B", "find my bed", "FFB74D") + "   " + K("F", "3rd-person view", "BA68C8") + "   " + K("E / click", "pick up · throw boxes & tools", "81C784") + "   " + K("O", "AI OFF SWITCH", "FF5252");
        public static readonly string KeysVR =
            K("Left stick", "move", "4FC3F7") + "   " + K("Right stick", "turn", "4FC3F7") + "   " + K("Grip", "teleport", "81C784") + "   " + K("Trigger", "use · ladder · sleep · wake", "81C784") + "   " +
            K("L-stick click", "menu", "FFB74D") + "   " + K("Y", "crew UI", "FFB74D") + "   " + K("X", "map", "FFB74D") + "   " + K("B", "autopilot", "BA68C8") + "   " + K("A", "accept offer", "F06292") + "   " + K("Hand up + shake", "wave", "F06292") + "\n" +
            K("Menu", "rubric tasks · crew health · find my bed · AI OFF SWITCH", "FFD54F");
        /// SIMPLE screen by default: only the 5 keys you need. K switches to every shortcut and back.
        public static readonly string KeysSimple =
            K("ENTER", "do my next task", "7CFC9A") + "     " + K("I", "check on crew", "F48FB1") + "     " + K("O", "AI off", "FF5252") + "     " + K("Q", "menu", "FFB74D") + "     " + K("H", "help", "FFB74D") + "     " + K("K", "all keys", "B0BEC5");
        bool keysFull;
        public void ToggleKeys()
        {
            if (!keysBg) return; var pr = PlayerRig.I; if (pr && pr.isXR) { keysBg.gameObject.SetActive(!keysBg.gameObject.activeSelf); return; }
            keysFull = !keysFull; keys.text = keysFull ? KeysDesktop : KeysSimple; keys.fontSize = keysFull ? 17 : 22;
            keysBg.rectTransform.sizeDelta = keysFull ? new Vector2(1880, 86) : new Vector2(1180, 48); keys.rectTransform.sizeDelta = keysFull ? new Vector2(1860, 84) : new Vector2(1160, 46);
        }
        public const string HelpText =
@"<b><color=#FFD54F>THE EASY WAY TO PLAY</color></b>
  <b>ENTER</b> (VR: menu → Do my next step) — does the task at the top of the screen WITH you, one step per press.
  <b>I</b> next to a crewmate — health check-in: they say yes, you read their sensors and the 6 mental-health levels.  <b>I</b> anywhere — crew wellness screen.
  <b>O</b> — the AI OFF switch. Humans always outrank the AI (it also has bedtime rules and a 30-second hardware interlock).
  <b>J</b> — rubric checklist.   7 missions about the crew's mental health run in order; the banner always shows the ONE next step.
<b><color=#40D9FF>CONTROLS</color></b>
<b>Desktop</b>  WASD / arrows move · Shift run · Space jump (1/6 g!) · Right-mouse or L (lock) look · E / click interact
<b>VR (Quest)</b>  Left stick move · Right stick turn · Grip teleport · Trigger interact / press buttons · Y crew UI · X map · B autopilot · A accept
<b>Shortcuts</b>  (VR button in brackets)
  Tab  (Y)   Crew Interface — your data, AI actions, overrides, privacy
  M    (X)   Big map            N   Navigate to a room (arrows + trail)
  P    (B)   Autopilot — the AI plays as you      Y (A) accept · R decline AI offer
  F1  AI dashboard   F2  CMO console   F3  Team meeting   F4  Tabletop simulation   F5  Board scenarios Q1–Q6   F11  ARES deliverables D1–D4
  C   Config A ↔ B (conflict protocol)   T  time speed   Z  zone info cards   Q  main menu (VR: left-stick click)   U  unstuck   F12 screenshot   Esc  close / menu
<b>Life</b>  Sleep: E on your bed (Pod 1) — 1 real minute = the whole night, lights OFF, sunrise wake-up · Ladder: E on it, then W / S (VR: stick or grip-and-pull)
  G (VR: raise a hand + wave)  wave hello · F6  safety drill / alarm (red lights, klaxon, muster) · F9  door walk test
<b>Going outside</b>  Go to the suit room (Airlock module) → interact with a suitport hatch → 6-step don procedure.
  Return to your suit's platform and press E / trigger to dock. The airlock is the contingency route (suit required).";

        void BuildMapCam()
        {
            var go = new GameObject("MinimapCamera"); mapCam = go.AddComponent<Camera>(); mapCam.orthographic = true; mapCam.orthographicSize = 14; mapCam.targetTexture = rt;
            mapCam.clearFlags = CameraClearFlags.SolidColor; mapCam.backgroundColor = new Color(0.05f, 0.06f, 0.08f); mapCam.cullingMask = ~(1 << 30); mapCam.nearClipPlane = 0.3f; mapCam.farClipPlane = 120;
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            // gold "you are here" marker: only the map cameras see layer 31
            var pr = PlayerRig.I; if (pr && pr.cam) pr.cam.cullingMask &= ~(1 << 31);
            mapCam.cullingMask = ~(1 << 30);
            youMarker = new GameObject("Map_YouAreHere").transform;
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(dot.GetComponent<Collider>()); dot.transform.SetParent(youMarker, false); dot.transform.localScale = new Vector3(0.9f, 0.2f, 0.9f);
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(nose.GetComponent<Collider>()); nose.transform.SetParent(youMarker, false); nose.transform.localPosition = new Vector3(0, 0, 0.75f); nose.transform.localScale = new Vector3(0.35f, 0.2f, 0.7f);
            var gm = Glow(UIKit.Gold); foreach (var r in youMarker.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = gm; r.gameObject.layer = 31; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            youMarker.gameObject.layer = 31;
        }
        void BuildArrow()
        {
            arrow = new GameObject("NavArrow").transform;
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder); shaft.transform.SetParent(arrow, false); shaft.transform.localScale = new Vector3(0.03f, 0.12f, 0.03f); shaft.transform.localRotation = Quaternion.Euler(90, 0, 0); shaft.transform.localPosition = new Vector3(0, 0, -0.06f);
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube); head.transform.SetParent(arrow, false); head.transform.localScale = new Vector3(0.09f, 0.03f, 0.09f); head.transform.localRotation = Quaternion.Euler(0, 45, 0); head.transform.localPosition = new Vector3(0, 0, 0.08f);
            foreach (var c in arrow.GetComponentsInChildren<Collider>()) Destroy(c);
            var m = Glow(UIKit.Gold); foreach (var r in arrow.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
            arrow.gameObject.SetActive(false);
        }
        public static Material Glow(Color c, float alpha = 1f)
        {
            var tmpl = Resources.Load<Material>(alpha < 1f ? "Hunch3_UnlitTransparent" : "Hunch3_Unlit");   // shipped templates: shader never stripped
            var m = tmpl ? new Material(tmpl) : new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")); m.color = new Color(c.r, c.g, c.b, alpha); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha));
            if (alpha < 1f && m.HasProperty("_Surface")) { m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", 10); m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000; }
            return m;
        }
        // Holographic 3D map of the habitat on the Command node briefing table.
        // Looks like a real table-top projector: a metal emitter puck, a faint light beam, a clean floor plan
        // (floors bright, walls faint), only the habitat itself (no rover, flags, crew or loose props), scaled to fit
        // inside the table, a slow scan line and a gentle flicker.
        Transform holo, holoYou, holoSrc, holoScan; Material holoFloor, holoWall, holoCone, holoScanMat; float holoTop, holoH, holoScale = 0.011f;
        Vector3 HoloMap(Vector3 world) => holo.TransformPoint(holoSrc.InverseTransformPoint(world));
        void BuildHologram()
        {
            var table = Util.Find("N39_Briefing_Table"); var src = Util.Find("LunarHabitat_Unity3_Habitat");
            if (!table || !src) return;
            // pre-built, non-static copy made by the editor Builder (never Instantiate the static-batched habitat at runtime)
            var ct = Util.Find("HoloMap_Model"); if (!ct) return;
            var copy = ct.gameObject; copy.SetActive(true);
            holo = copy.transform; holoSrc = src;
            // 1) lay the copy over the real habitat at full size, so every part can be judged by its real size and place
            holo.SetParent(null, true); holo.position = src.position; holo.rotation = src.rotation; holo.localScale = src.lossyScale;
            if (Nav.wp.Count == 0) Nav.Build();
            Bounds foot = new Bounds(src.position, Vector3.one); bool any = false;
            foreach (var w in Nav.wp.Values) { if (!any) { foot = new Bounds(w.position, Vector3.one); any = true; } else foot.Encapsulate(w.position); }
            foot.Expand(new Vector3(14f, 6f, 14f));     // walls and hull sit a few metres beyond the walkway waypoints
            string[] skip = { "crew", "rover", "flag", "terrain", "solar", "antenna", "tower", "lander", "rock", "regolith", "sky", "earth", "sun_", "astronaut", "evasuit", "chair", "seat", "plant", "lamp", "screen", "label", "sign", "beacon", "table" };
            holoFloor = Glow(new Color(0.35f, 0.9f, 1f), 0.30f);
            holoWall = Glow(new Color(0.25f, 0.75f, 1f), 0.07f);
            Bounds kept = foot; bool keptAny = false;
            foreach (var r in copy.GetComponentsInChildren<MeshRenderer>(true))
            {
                var bnd = r.bounds; string n = r.name.ToLowerInvariant(); float big = Mathf.Max(bnd.size.x, bnd.size.y, bnd.size.z);
                bool keep = r.gameObject.layer != 30 && big >= 1.2f && big < 30f && foot.Contains(bnd.center);
                if (keep) foreach (var k in skip) if (n.Contains(k)) { keep = false; break; }
                if (!keep) { r.enabled = false; continue; }
                bool floor = bnd.size.y < 0.45f && Mathf.Max(bnd.size.x, bnd.size.z) > 1.5f;     // flat & wide = a deck → the readable floor plan
                var m = floor ? holoFloor : holoWall; var ms = new Material[r.sharedMaterials.Length]; for (int i = 0; i < ms.Length; i++) ms[i] = m;
                r.sharedMaterials = ms; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                if (!keptAny) { kept = bnd; keptAny = true; } else kept.Encapsulate(bnd);
            }
            // 2) shrink it so the whole habitat fits inside the table top, lowest deck floating just above the emitter
            var tb = table.GetComponent<Renderer>() ? table.GetComponent<Renderer>().bounds : new Bounds(table.position, Vector3.one * 1.5f);
            float tableR = Mathf.Min(tb.extents.x, tb.extents.z);
            holoScale = Mathf.Min(0.03f, (tableR * 1.6f) / Mathf.Max(kept.size.x, kept.size.z, 1f));
            holo.localScale = src.lossyScale * holoScale; holo.position = Vector3.zero;
            float emitterH = 0.05f, lift = 0.12f;
            Vector3 top = new Vector3(tb.center.x, tb.max.y + emitterH + lift, tb.center.z);
            Vector3 anchor = new Vector3(kept.center.x, kept.min.y, kept.center.z);
            holo.position = top - HoloMap(anchor);
            holoTop = tb.max.y; holoH = Mathf.Max(0.15f, kept.size.y * holoScale) + emitterH + lift;
            // 3) the projector hardware: a brushed-metal puck with a glowing lens ring, a faint light beam and a cyan glow light
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var metal = new Material(lit); metal.color = new Color(0.22f, 0.24f, 0.27f); if (metal.HasProperty("_BaseColor")) metal.SetColor("_BaseColor", metal.color); if (metal.HasProperty("_Metallic")) metal.SetFloat("_Metallic", 0.85f); if (metal.HasProperty("_Smoothness")) metal.SetFloat("_Smoothness", 0.6f);
            Transform Part(PrimitiveType t, string nm, Vector3 pos, Vector3 scl, Material m)
            {
                var g = GameObject.CreatePrimitive(t); g.name = nm; Destroy(g.GetComponent<Collider>()); g.transform.position = pos; g.transform.localScale = scl;
                var rr = g.GetComponent<Renderer>(); rr.sharedMaterial = m; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return g.transform;
            }
            float pr = Mathf.Min(0.22f, tableR * 0.35f);
            Part(PrimitiveType.Cylinder, "HoloProjector_Base", new Vector3(tb.center.x, tb.max.y + emitterH * 0.5f, tb.center.z), new Vector3(pr * 2f, emitterH * 0.5f, pr * 2f), metal);
            Part(PrimitiveType.Cylinder, "HoloProjector_LensRing", new Vector3(tb.center.x, tb.max.y + emitterH + 0.002f, tb.center.z), new Vector3(pr * 1.5f, 0.003f, pr * 1.5f), Glow(new Color(0.4f, 0.95f, 1f)));
            Part(PrimitiveType.Cylinder, "HoloProjector_Lens", new Vector3(tb.center.x, tb.max.y + emitterH + 0.004f, tb.center.z), new Vector3(pr * 1.2f, 0.003f, pr * 1.2f), metal);
            holoCone = Glow(new Color(0.3f, 0.85f, 1f), 0.035f);
            float coneR = tableR * 0.85f;
            Part(PrimitiveType.Cylinder, "HoloProjector_Beam", new Vector3(tb.center.x, tb.max.y + emitterH + holoH * 0.5f, tb.center.z), new Vector3(coneR * 2f, holoH * 0.5f, coneR * 2f), holoCone);
            holoScanMat = Glow(new Color(0.6f, 1f, 1f), 0.12f);
            holoScan = Part(PrimitiveType.Cylinder, "HoloProjector_ScanLine", new Vector3(tb.center.x, tb.max.y + emitterH, tb.center.z), new Vector3(coneR * 2f, 0.001f, coneR * 2f), holoScanMat);
            var gl = new GameObject("HoloProjector_Glow").AddComponent<Light>(); gl.type = LightType.Point; gl.color = new Color(0.35f, 0.85f, 1f); gl.range = 1.6f; gl.intensity = 0.8f; gl.shadows = LightShadows.None;
            gl.transform.position = new Vector3(tb.center.x, tb.max.y + 0.3f, tb.center.z);
            // 4) "you are here": a small gold dot that follows you around the map
            var you = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(you.GetComponent<Collider>()); you.transform.localScale = Vector3.one * 0.025f; you.GetComponent<Renderer>().sharedMaterial = Glow(UIKit.Gold); holoYou = you.transform; holoYou.name = "HoloMap_YouAreHere";
        }
        void AnimateHologram()
        {
            if (!holo) return;
            float t = Time.time;
            float flick = 0.92f + 0.05f * Mathf.Sin(t * 13f) + 0.03f * Mathf.Sin(t * 37f);
            void A(Material m, Color c, float a) { if (!m) return; var col = new Color(c.r, c.g, c.b, a * flick); m.color = col; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col); }
            A(holoFloor, new Color(0.35f, 0.9f, 1f), 0.30f); A(holoWall, new Color(0.25f, 0.75f, 1f), 0.07f); A(holoCone, new Color(0.3f, 0.85f, 1f), 0.035f);
            if (holoScan) { var p = holoScan.position; p.y = holoTop + 0.05f + Mathf.Repeat(t * 0.12f, 1f) * holoH; holoScan.position = p; }
        }

        void Update()
        {
            var pr = PlayerRig.I; if (!pr || Sim.I == null) return;
            var e = Sim.I.engine; var me = e.C(1); var env = HabitatEnvironment.I;
            var z = pr.zone;
            int act = e.interventionsActive;
            float avgWell = e.crew.Average(c => c.Wellbeing);
            string suit = pr.suit == SuitState.EVA ? $"<color=#{UIKit.Hex(UIKit.Gold)}>EVA</color> suit O2 {pr.suitO2Hours:0.0} h" : pr.suit.ToString();
            stats.text =
                $"<b><color=#{UIKit.Hex(UIKit.Accent)}>LUNAR SMART ADAPTIVE HABITAT</color></b>\n" +
                $"<b>{SimClock.Now}</b>   ×{GameState.timeScale / 60f:0.#} speed   g = 1.62 m/s²\n" +
                $"Zone: <b>{pr.zoneName}</b>{(z != null ? "  (" + z.lighting + ")" : "")}\n" +
                $"O2 {env?.o2Percent:0.0}%   CO2 {(z != null ? z.co2 : 520):0} ppm   {(z != null ? z.tempC : 21.5f):0.0} °C\n" +
                $"Power {env?.powerKW:0} kW (fission 40 + solar {env?.solarKW:0})   Water {env?.waterL:0} L\n" +
                $"Pressure {env?.pressureKPa:0.0} kPa   Config {(GameState.configB ? "<color=#FF9933>B (conflict)</color>" : "A (nominal)")}\n" +
                $"Suit: {suit}\n" +
                $"<b>Crew wellbeing (aggregate)</b> {UIKit.Bar(avgWell)} {avgWell:P0}\n" +
                $"Your state: <color=#{UIKit.Hex(UIKit.TierCol(me.MaxTier))}>{(me.MaxTier == 0 ? "OK" : "Tier " + me.MaxTier)}</color>  wellbeing {me.Wellbeing:P0}\n" +
                $"AI: {act} active support · {e.escalations} escalations · {EthicsGuard.blocked} blocked by hard limits\n" +
                $"Activity: {me.activity}" +
                (SpaceRealism.I ? "\n" + SpaceRealism.I.Status : "");
            autoBanner.text = pr.autopilot ? (string.IsNullOrEmpty(navGoal) ? "◆ AUTOPILOT — AI is playing as you (P / B to take control) ◆" : $"◆ AUTO-WALK to {navLabel} (P / B to stop) ◆") : (GameState.mode == GameMode.Tour ? "GUIDED TOUR" : "");
            if (Time.time > toastUntil) toast.text = "";
            if (offerBox.gameObject.activeSelf != (currentOffer != null && !currentOffer.done)) offerBox.gameObject.SetActive(currentOffer != null && !currentOffer.done);
            if (currentOffer != null) offerText.text = $"<b><color=#{UIKit.Hex(UIKit.Gold)}>AI offer (Tier 2 — your choice)</color></b>\n{currentOffer.text}";
            // minimap follows the player
            var fp = pr.FeetPosition;
            mapCam.transform.position = new Vector3(fp.x, fp.y + 2.3f + 30f, fp.z);
            mapCam.orthographicSize = bigMapBg.gameObject.activeSelf ? 48 : 14;
            mapCam.nearClipPlane = 30f; mapCam.farClipPlane = 70f;   // slice: only geometry below head height on your current floor (cuts away roofs & the deck above)
            mapCam.transform.rotation = Quaternion.Euler(90, pr.cam.transform.eulerAngles.y, 0);
            if (pr.cam) pr.cam.cullingMask &= ~(1 << 31);
            if (youMarker) { youMarker.position = fp + Vector3.up * 2.0f; youMarker.rotation = Quaternion.Euler(0, pr.cam.transform.eulerAngles.y, 0); youMarker.localScale = Vector3.one * (bigMapBg.gameObject.activeSelf ? 5f : 1.6f); }
            if (holoYou && holo) holoYou.position = HoloMap(fp) + Vector3.up * 0.015f;
            AnimateHologram();
            if (planLegend && bigMapBg.gameObject.activeSelf) planLegend.text = FloorPlan.Legend() + $"\n\n<b>Scale</b>: map width ≈ {mapCam.orthographicSize * 2f * mapCam.aspect:0} m";
            UpdateNav(pr);
        }

        public string NavGoal => navGoal; public string NavLabel => navLabel;
        public void SetNavTarget(string wpName, string label)
        {
            if (wpName == navGoal && crumbs.Count > 0) return;
            navGoal = wpName; navLabel = label;
            foreach (var c in crumbs) Destroy(c); crumbs.Clear();
            if (string.IsNullOrEmpty(wpName) || !PlayerRig.I) { arrow.gameObject.SetActive(false); navText.text = ""; if (compass) compass.text = ""; return; }
            var path = Nav.Path(PlayerRig.I.FeetPosition, wpName);
            var m = Glow(UIKit.Accent, 0.85f);
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector3 a = path[i], b = path[i + 1]; if (Nav.IsLadder(a, b)) continue;
                float len = Vector3.Distance(a, b); int n = Mathf.Max(1, Mathf.FloorToInt(len / 1.2f));
                for (int k = 0; k < n; k++)
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>()); q.name = "NavChevron";
                    q.transform.position = Vector3.Lerp(a, b, (k + 0.5f) / n) + Vector3.up * 0.03f;
                    q.transform.rotation = Quaternion.LookRotation(Vector3.down, (b - a).normalized); q.transform.localScale = new Vector3(0.25f, 0.4f, 1);
                    q.GetComponent<Renderer>().sharedMaterial = m; crumbs.Add(q);
                }
            }
            arrow.gameObject.SetActive(true);
        }
        void UpdateNav(PlayerRig pr)
        {
            if (string.IsNullOrEmpty(navGoal) || !Nav.wp.ContainsKey(navGoal)) return;
            var goal = Nav.wp[navGoal].position; var path = Nav.Path(pr.FeetPosition, navGoal);
            Vector3 next = path.Count > 1 ? path[1] : goal; if (path.Count > 0 && (path[0] - pr.FeetPosition).sqrMagnitude > 1.2f) next = path[0];
            float dist = Vector3.Distance(pr.FeetPosition, goal);
            var cam = pr.cam.transform;
            arrow.position = cam.position + cam.forward * 0.9f - cam.up * 0.25f;
            var dir = next - arrow.position; dir.y = 0; if (dir.sqrMagnitude > 1e-4f) arrow.rotation = Quaternion.Slerp(arrow.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 8f);
            if (compass) { var f = cam.forward; f.y = 0; var to = next - cam.position; to.y = 0; float rel = Vector3.SignedAngle(f, to, Vector3.up); compass.text = "➜"; compass.rectTransform.localEulerAngles = new Vector3(0, 0, 90f - rel); }
            navText.text = $"➜ <b>{navLabel}</b>  {dist:0} m" + (Mathf.Abs(goal.y - pr.FeetPosition.y) > 1.5f ? "  (use the ladder)" : "");
            if (dist < 1.2f) { Toast($"Arrived: {navLabel}"); SetNavTarget("", ""); }
        }

        public void Toast(string s)
        {
            if (!toast) return;
            // the headline (up to the first full stop, colon or dash) is bold so the important part jumps out
            if (!string.IsNullOrEmpty(s) && !s.Contains("<b>"))
            {
                int cut = -1; foreach (var sep in new[] { ". ", ": ", " — ", "! " }) { int k = s.IndexOf(sep); if (k > 0 && (cut < 0 || k < cut)) cut = k + sep.Length - 1; }
                if (cut > 0 && cut < 90) s = "<b>" + s.Substring(0, cut) + "</b>" + s.Substring(cut);
            }
            toast.text = s; toastUntil = Time.time + 7f;
        }
        public void SetPrompt(string s) { if (prompt) prompt.text = s; }
        public void Procedure(string s) { if (procedure) procedure.text = string.IsNullOrEmpty(s) ? "" : "<b>" + s + "</b>"; }
        public void ZoneCard(Zone z)
        {
            if (!zoneCard) return;
            int cams = Util.FindPrefix("X_FACS_Camera").Count(t => (t.position - z.center).magnitude < z.radius + 1);
            zoneCard.text = $"<b><color=#{UIKit.Hex(UIKit.Accent)}>{z.name.Replace("_", " ")}</color></b>  ·  ARES zone: <b>{z.type}</b>\n<i>{z.purpose}</i>\nLighting: {z.lighting}\nSensors here: {(z.type == "private" ? "door/occupancy only — NO camera or mic" : cams + " FACS cam(s), mic array, CO2 pair, motion")}\nCurrent: {z.co2:0} ppm CO2 · {z.tempC:0.0} °C · scent {z.scent} · audio {z.audio}";
        }
        public void ToggleZoneCards() { showZoneCards = !showZoneCards; zoneCard.transform.parent.gameObject.SetActive(showZoneCards); }
        public void ToggleHelp() { help.transform.parent.gameObject.SetActive(!help.transform.parent.gameObject.activeSelf); }
        Text planLegend;
        public bool BigMapOpen => bigMapBg && bigMapBg.gameObject.activeSelf;
        public void ToggleBigMap()
        {
            bool on = !bigMapBg.gameObject.activeSelf; bigMapBg.gameObject.SetActive(on);
            FloorPlan.I?.Show(on);
            if (zoneCard) zoneCard.transform.parent.gameObject.SetActive(!on && showZoneCards);
            if (on && !planLegend)
            {
                var lg = UIKit.Panel(bigMapBg.transform, "PlanLegend", new Vector2(1, 0.5f), new Vector2(8, 0), new Vector2(500, 860), new Color(0.03f, 0.05f, 0.1f, 0.92f), new Vector2(0, 0.5f));
                planLegend = UIKit.Label(lg.transform, "", 18, Color.white, new Vector2(0, 1), new Vector2(12, -10), new Vector2(476, 840), TextAnchor.UpperLeft, new Vector2(0, 1));
            }
        }
        public void AnswerOffer(bool yes)
        {
            if (currentOffer == null || currentOffer.done) return;
            currentOffer.done = true; if (yes) currentOffer.onAccept?.Invoke(); else currentOffer.onDecline?.Invoke();
            Toast(yes ? "Accepted — thank you. You can stop it any time in your Crew Interface." : "Declined. The AI has stopped and logged your choice; it won't push again for 6 h.");
            if (yes && currentOffer.cond == 0) { var w = "WP_D_VR"; SetNavTarget(w, "VR retreat"); }
            if (yes && currentOffer.cond == 1) SetNavTarget("WP_C_Mid", "Greenhouse");
            currentOffer = null;
        }
        public void Fade() { StartCoroutine(FadeCo()); }
        System.Collections.IEnumerator FadeCo() { for (float t = 0; t < 1; t += Time.deltaTime * 2) { fade.color = new Color(0, 0, 0, t); yield return null; } yield return new WaitForSeconds(0.6f); for (float t = 1; t > 0; t -= Time.deltaTime * 1.5f) { fade.color = new Color(0, 0, 0, t); yield return null; } fade.color = Color.clear; }
    }

    /// Animated rainbow border for buttons (kid-friendly): the outline colour drifts slowly through the hues.
    /// (retired) the old rainbow/pulse effect — kept as an empty component so nothing that still adds it breaks
    public class RainbowEdge : MonoBehaviour { public Outline outline; public float phase; }
}
