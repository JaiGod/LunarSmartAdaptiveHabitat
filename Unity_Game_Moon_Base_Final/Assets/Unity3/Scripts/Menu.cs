using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace Hunch3
{
    /// Main menu: the player stands inside a vibrant glowing box with floating panels.
    public class MenuBoot : MonoBehaviour
    {
        readonly List<Renderer> grid = new List<Renderer>(); Transform moon, ring, holo; Text sub; Camera cam; bool isXR;
        GameObject settingsPanel, creditsPanel;
        void Start()
        {
            GameState.selfTest |= Environment.GetCommandLineArgs().Contains("-selftest");
            if (GameState.selfTest) StopXR();
            isXR = UnityEngine.XR.XRSettings.isDeviceActive && !GameState.selfTest;
            var origin = FindFirstObjectByType<XROrigin>(); cam = Camera.main;
            if (!isXR && cam) { foreach (var b in cam.GetComponents<Behaviour>()) if (b.GetType().Name.Contains("TrackedPoseDriver")) b.enabled = false; Util.SetEyeHeight(cam, origin ? origin.transform : null, 1.6f); cam.transform.localRotation = Quaternion.identity; }
            if (origin) origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // solid (invisible) floor so the rig's built-in gravity can't drop the player out of the box
            var floor = new GameObject("MenuFloorCollider").AddComponent<BoxCollider>(); floor.size = new Vector3(8, 0.2f, 8); floor.center = new Vector3(0, -0.1f, 0);
            if (origin && !isXR)
                foreach (var b in origin.GetComponentsInChildren<Behaviour>(true))
                { string n = b.GetType().FullName ?? ""; if (n.Contains("Locomotion") || n.Contains("Teleport") || n.Contains("Turn") || n.Contains("Gravity") || n.Contains("Jump")) b.enabled = false; }
            if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.01f, 0.0f, 0.04f); }
            RenderSettings.ambientLight = new Color(0.25f, 0.2f, 0.4f);
            if (!isXR && cam) Util.SetEyeHeight(cam, origin ? origin.transform : null, 1.6f);
            BuildBox(); BuildUI();
            if (!isXR) DesktopUIClicker.Ensure();
            if (GameState.selfTest && !FindFirstObjectByType<SelfTest>()) new GameObject("SelfTest").AddComponent<SelfTest>();
        }
        public static void StopXR()
        {
            try { var m = UnityEngine.XR.Management.XRGeneralSettings.Instance?.Manager; if (m != null && m.activeLoader != null) { m.StopSubsystems(); m.DeinitializeLoader(); } } catch (Exception e) { Debug.LogWarning(e.Message); }
        }
        Material Neon(Color c, float e) { var m = HUD.Glow(c); return m; }
        void BuildBox()
        {
            // a 8 x 5 x 8 m glowing box room: floor, walls, ceiling made of neon grid tiles
            float W = 8, H = 5;
            var tex = new Texture2D(256, 256); var px = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++) { bool line = x % 64 < 3 || y % 64 < 3; bool dot = (x % 64 - 32) * (x % 64 - 32) + (y % 64 - 32) * (y % 64 - 32) < 6; px[y * 256 + x] = line ? Color.white : dot ? new Color(0.6f, 0.6f, 0.8f) : new Color(0.03f, 0.02f, 0.08f); }
            tex.SetPixels(px); tex.Apply(); tex.wrapMode = TextureWrapMode.Repeat;
            void Face(Vector3 pos, Vector3 euler, Vector2 size, Color c)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>()); q.name = "NeonBoxFace";
                q.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler)); q.transform.localScale = new Vector3(size.x, size.y, 1);
                var m = HUD.Glow(c); m.mainTexture = tex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); m.mainTextureScale = size / 2f; if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", size / 2f);
                q.GetComponent<Renderer>().sharedMaterial = m; grid.Add(q.GetComponent<Renderer>());
            }
            Face(new Vector3(0, 0, 0), new Vector3(90, 0, 0), new Vector2(W, W), new Color(0.2f, 0.9f, 1f));
            Face(new Vector3(0, H, 0), new Vector3(-90, 0, 0), new Vector2(W, W), new Color(1f, 0.3f, 0.9f));
            Face(new Vector3(0, H / 2, W / 2), new Vector3(0, 0, 0), new Vector2(W, H), new Color(0.5f, 0.3f, 1f));
            Face(new Vector3(0, H / 2, -W / 2), new Vector3(0, 180, 0), new Vector2(W, H), new Color(0.5f, 0.3f, 1f));
            Face(new Vector3(W / 2, H / 2, 0), new Vector3(0, 90, 0), new Vector2(W, H), new Color(0.2f, 1f, 0.7f));
            Face(new Vector3(-W / 2, H / 2, 0), new Vector3(0, -90, 0), new Vector2(W, H), new Color(1f, 0.6f, 0.2f));
            // glowing edge beams
            var beam = HUD.Glow(new Color(1, 1, 1));
            foreach (var x in new[] { -W / 2, W / 2 }) foreach (var z in new[] { -W / 2, W / 2 }) { var b = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(b.GetComponent<Collider>()); b.transform.position = new Vector3(x, H / 2, z); b.transform.localScale = new Vector3(0.06f, H, 0.06f); b.GetComponent<Renderer>().sharedMaterial = beam; }
            // floating moon + orbit ring + spinning habitat-style hologram
            var mo = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(mo.GetComponent<Collider>()); moon = mo.transform; moon.position = new Vector3(-2.2f, 2.6f, 2.6f); moon.localScale = Vector3.one * 1.1f;
            mo.GetComponent<Renderer>().sharedMaterial = HUD.Glow(new Color(0.75f, 0.75f, 0.8f));
            var rg = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(rg.GetComponent<Collider>()); ring = rg.transform; ring.position = moon.position; ring.localScale = new Vector3(1.8f, 0.005f, 1.8f); rg.GetComponent<Renderer>().sharedMaterial = HUD.Glow(new Color(0.3f, 0.9f, 1f), 0.5f);
            var hg = new GameObject("HabitatHologram"); holo = hg.transform; holo.position = new Vector3(2.3f, 1.2f, 2.4f);
            var hm = HUD.Glow(new Color(0.3f, 1f, 0.8f), 0.6f);
            void P(PrimitiveType t, Vector3 p, Vector3 s) { var g = GameObject.CreatePrimitive(t); Destroy(g.GetComponent<Collider>()); g.transform.SetParent(holo, false); g.transform.localPosition = p; g.transform.localScale = s; g.GetComponent<Renderer>().sharedMaterial = hm; }
            P(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.5f, 0.15f, 0.5f)); P(PrimitiveType.Sphere, new Vector3(0, 0.18f, 0), new Vector3(0.52f, 0.4f, 0.52f));
            foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back }) P(PrimitiveType.Capsule, d * 0.5f, new Vector3(0.18f, 0.25f, 0.18f));
            // particles (stars)
            var ps = new GameObject("Stars").AddComponent<ParticleSystem>(); var main = ps.main; main.startLifetime = 8; main.startSpeed = 0.05f; main.startSize = 0.03f; main.maxParticles = 600; main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.9f, 1f), new Color(1f, 0.6f, 1f));
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(W - 0.5f, H - 0.5f, W - 0.5f); ps.transform.position = new Vector3(0, H / 2, 0);
            var em = ps.emission; em.rateOverTime = 70; var pr = ps.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = HUD.Glow(Color.white);
            var l = new GameObject("MenuLight").AddComponent<Light>(); l.type = LightType.Point; l.range = 12; l.intensity = 2; l.color = new Color(0.7f, 0.6f, 1f); l.transform.position = new Vector3(0, 4, 0);
        }
        void BuildUI()
        {
            var c = UIKit.Canvas("MenuCanvas", true, new Vector2(1800, 1150)); c.transform.position = new Vector3(0, 1.6f, 2.6f); c.transform.localScale = Vector3.one * 0.0013f;
            var bg = UIKit.Panel(c.transform, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1800, 1150), new Color(0.03f, 0.02f, 0.1f, 0.82f));
            UIKit.Label(bg.transform, "<b>LUNAR SMART ADAPTIVE HABITAT</b>", 74, new Color(0.4f, 0.95f, 1f), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(1700, 100), TextAnchor.UpperCenter);
            sub = UIKit.Label(bg.transform, "NASA HUNCH 2025-26 · AI Psychological Monitoring for Positive Crew Mental Health · 4 crew · 1/6 g", 28, new Color(1f, 0.6f, 0.95f), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(1700, 44), TextAnchor.UpperCenter);
            string[] labels = { "▶  START MISSION (sandbox)", "◎  GUIDED ZONE TOUR (autopilot)", "⚖  AGENCY BOARD SCENARIOS Q1–Q6", "⏱  TABLETOP SIMULATION (72 h)", "☰  CREW INTERFACE DEMO", "⚙  SETTINGS", "★  CREDITS", "✕  QUIT" };
            Action[] acts = { () => Go(GameMode.Sandbox), () => Go(GameMode.Tour), () => Go(GameMode.Scenario), () => Go(GameMode.Tabletop), () => Go(GameMode.InterfaceDemo), () => Toggle(settingsPanel), () => Toggle(creditsPanel), Quit };
            Color[] cols = { new Color(0.1f, 0.6f, 0.4f), new Color(0.1f, 0.4f, 0.75f), new Color(0.55f, 0.25f, 0.75f), new Color(0.75f, 0.45f, 0.1f), new Color(0.15f, 0.5f, 0.6f), new Color(0.3f, 0.3f, 0.45f), new Color(0.5f, 0.4f, 0.1f), new Color(0.5f, 0.15f, 0.2f) };
            for (int i = 0; i < labels.Length; i++)
            { var b = UIKit.Btn(bg.transform, labels[i], new Vector2(0.5f, 1), new Vector2(i < 4 ? -440 : 440, -260 - (i % 4) * 120), new Vector2(820, 96), acts[i], cols[i], 34); b.name = "Menu_" + i; }
            UIKit.Label(bg.transform, "VR: point & pull the trigger · Desktop: click · In-game: H for help", 24, new Color(0.8f, 0.8f, 1f), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1700, 40), TextAnchor.LowerCenter);
            settingsPanel = UIKit.Panel(bg.transform, "Settings", new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1100, 520), new Color(0.05f, 0.05f, 0.15f, 0.97f)).gameObject;
            var st = UIKit.Label(settingsPanel.transform, "", 30, Color.white, new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(1000, 160), TextAnchor.UpperCenter);
            void RefreshS() { st.text = $"<b>SETTINGS</b>\nMovement: {(GameState.smoothMove ? "smooth" : "teleport")} · Turn: {(GameState.snapTurn ? "snap" : "smooth")} · Time: 1 s = {GameState.timeScale / 60f:0.#} sim min\nQuest performance mode: {(GameState.questPerformance ? "ON (fewer lights)" : "off")} · Crew: 4 (you + 3 AI)"; }
            RefreshS();
            UIKit.Btn(settingsPanel.transform, "Movement", new Vector2(0.5f, 0.5f), new Vector2(-270, -40), new Vector2(500, 80), () => { GameState.smoothMove = !GameState.smoothMove; RefreshS(); });
            UIKit.Btn(settingsPanel.transform, "Turn style", new Vector2(0.5f, 0.5f), new Vector2(270, -40), new Vector2(500, 80), () => { GameState.snapTurn = !GameState.snapTurn; RefreshS(); });
            UIKit.Btn(settingsPanel.transform, "Time speed", new Vector2(0.5f, 0.5f), new Vector2(-270, -140), new Vector2(500, 80), () => { GameState.timeScale = GameState.timeScale >= 600 ? 60 : 600; RefreshS(); });
            UIKit.Btn(settingsPanel.transform, "Quest performance", new Vector2(0.5f, 0.5f), new Vector2(270, -140), new Vector2(500, 80), () => { GameState.questPerformance = !GameState.questPerformance; RefreshS(); });
            UIKit.Btn(settingsPanel.transform, "Done", new Vector2(0.5f, 0.5f), new Vector2(0, -230), new Vector2(300, 70), () => settingsPanel.SetActive(false), new Color(0.2f, 0.5f, 0.3f));
            settingsPanel.SetActive(false);
            creditsPanel = UIKit.Panel(bg.transform, "Credits", new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1100, 520), new Color(0.05f, 0.05f, 0.15f, 0.97f)).gameObject;
            UIKit.Label(creditsPanel.transform, "<b>CREDITS</b>\n\n© Jaineil R. — design, 3D habitat, software\nwith thanks to Angel C. and Farhan K.\n\nNASA HUNCH · Lunar Smart Adaptive Habitat Design\nwith AI Psychological Monitoring", 32, Color.white, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(1000, 400), TextAnchor.UpperCenter);
            UIKit.Btn(creditsPanel.transform, "Close", new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(300, 70), () => creditsPanel.SetActive(false), new Color(0.2f, 0.5f, 0.3f));
            creditsPanel.SetActive(false);
        }
        void Toggle(GameObject g) { if (g) g.SetActive(!g.activeSelf); }
        public static void Go(GameMode m) { GameState.mode = m; SceneManager.LoadScene(1); }
        void Quit() { Application.Quit(); }
        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < grid.Count; i++)
            {
                if (!grid[i]) continue;
                var col = Color.HSVToRGB((t * 0.05f + i * 0.13f) % 1f, 0.75f, 1f) * (0.8f + 0.2f * Mathf.Sin(t * 2 + i));
                var m = grid[i].sharedMaterial; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col); m.color = col;
            }
            if (moon) moon.Rotate(0, 8 * Time.deltaTime, 0); if (ring) ring.Rotate(0, -20 * Time.deltaTime, 0);
            if (holo) { holo.Rotate(0, 25 * Time.deltaTime, 0); holo.position = new Vector3(2.3f, 1.2f + 0.08f * Mathf.Sin(t * 1.5f), 2.4f); }
            if (sub) sub.color = Color.Lerp(new Color(1f, 0.6f, 0.95f), new Color(0.5f, 0.9f, 1f), 0.5f + 0.5f * Mathf.Sin(t));
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.enterKey.wasPressedThisFrame) Go(GameMode.Sandbox);
        }
    }

    /// Habitat scene bootstrap: wires every system together.
    public class HabitatBoot : MonoBehaviour
    {
        void LateRenderReport() { try { RenderFix.Apply(); } catch (System.Exception e) { Debug.LogWarning(e); } }
        void Awake()
        {
            GameState.selfTest |= Environment.GetCommandLineArgs().Contains("-selftest");
            if (GameState.selfTest) MenuBoot.StopXR();
            Physics.gravity = new Vector3(0, -1.62f, 0);
            // neutral lunar interior: no fog haze, no stray post-process volumes tinting the picture
            Debug.Log($"[HUNCH3] render: fog={RenderSettings.fog} fogCol={RenderSettings.fogColor} mode={RenderSettings.fogMode} dens={RenderSettings.fogDensity} amb={RenderSettings.ambientLight} refl={RenderSettings.defaultReflectionMode}");
            RenderSettings.fog = false;
            // No baked lighting data → Unity falls back to the DEFAULT blue-sky ambient probe + reflection cube,
            // which is what tinted every interior wall teal. Replace both with neutral grey at runtime.
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2(); sh.Clear(); sh.AddAmbientLight(new Color(0.36f, 0.35f, 0.34f));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.36f, 0.35f, 0.34f); RenderSettings.ambientProbe = sh;
            var cube = new Cubemap(8, TextureFormat.RGBA32, false); var px = new Color[64]; for (int i = 0; i < 64; i++) px[i] = new Color(0.10f, 0.10f, 0.10f, 1f);
            foreach (CubemapFace f in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ }) cube.SetPixels(px, f);
            cube.Apply();
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = cube; RenderSettings.reflectionIntensity = 0.6f;
            DynamicGI.UpdateEnvironment();
            foreach (var v in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { Debug.Log($"[HUNCH3] disabling volume {v.name} (global={v.isGlobal})"); v.enabled = false; }
            Nav.Build();
            gameObject.AddComponent<Sim>();
            gameObject.AddComponent<HabitatEnvironment>();
            gameObject.AddComponent<PlayerRig>();
            gameObject.AddComponent<DesktopInteractor>();
            gameObject.AddComponent<HUD>();
            if (!(UnityEngine.XR.XRSettings.isDeviceActive && !GameState.selfTest)) DesktopUIClicker.Ensure();
        }
        void Start()
        {
            try { RenderFix.Apply(); } catch (System.Exception e) { Debug.LogWarning("[HUNCH3] RenderFix: " + e); }
            Invoke(nameof(LateRenderReport), 6f);
            for (int n = 1; n <= 4; n++)
            {
                var r = Util.Find($"CREW{n}_IVA_Root"); if (!r) continue;
                if (n == 1) { r.gameObject.SetActive(false); continue; }        // you ARE crew 1
                var a = r.gameObject.AddComponent<CrewAgent>(); a.crewId = n;
                var start = Nav.Path(r.position, n == 2 ? "WP_Up_Table" : n == 3 ? "WP_E_C" : "WP_C_Mid");
                if (start.Count > 0) r.position = start[0];
            }
            if (GameState.questPerformance) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Point && l.GetInstanceID() % 2 == 0) l.enabled = false;
            switch (GameState.mode)
            {
                case GameMode.Tour: PlayerRig.I.autopilot = true; HUD.I?.Toast("Guided tour: the autopilot walks you through every ARES zone. Press P to explore on your own."); break;
                case GameMode.Scenario: Panels.Open("scenarios"); break;
                case GameMode.Tabletop: Panels.Open("tabletop"); break;
                case GameMode.InterfaceDemo: Sim.I.engine.C(1).smooth[3] = 0.6f; Sim.I.engine.C(1).aboveSince[3] = SimClock.hours - 3; Panels.Open("crew"); break;
            }
            if (GameState.selfTest && !FindFirstObjectByType<SelfTest>()) new GameObject("SelfTest").AddComponent<SelfTest>();
        }
    }
}
