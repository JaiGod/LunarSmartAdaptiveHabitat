// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/06_Missions/HabitatOps.cs
//  Purpose : Makes the smart systems PLAYABLE with clear instructions:
//            1) a "How to play" card when you arrive (keys + what the outlines mean)
//            2) DAILY OPS — one task at a time with exactly where to go (the nav arrow points there) and which key to press,
//               points and a celebration when you finish it
//            3) SYSTEM ALERTS — when equipment wears out or breaks, a red banner says what broke, where it is and how to
//               fix it (look at it, press E to repair). Fixing it is the only way the alarm goes away.
//  Layout  : nothing overlaps the existing HUD — left column under the minimap (tasks), below the mission banner (alerts),
//            screen centre only for the short celebration and the arrival card.
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hunch3
{
    public class HabitatOps : MonoBehaviour
    {
        public static HabitatOps I;
        class OpsTask { public string key, hab, title, how, room; public Key? press; public int pts; }
        static OpsTask T(string key, Key? k, string title, string room, string how, int pts) => new OpsTask { key = key, press = k, title = title, room = room, how = how, pts = pts };
        static OpsTask H(string kind, string title, string room, string how, int pts) => new OpsTask { hab = kind, title = title, room = room, how = how, pts = pts };
        /// A full shift: wellness, medical, life support, science and crew life. Tasks whose equipment isn't in the base are skipped.
        static readonly OpsTask[] All =
        {
            T("mood_tracker", null, "Log how you feel", "the quarters hallway", "look at the Mood Tracker and press E (feeling good) or R (feeling low)", 25),
            T("smart_bed", Key.F, "Check last night's sleep", "your rest pod (upstairs)", "look at your bed and press F", 20),
            T("circadian_light", Key.E, "Set your wake-up light", "your quarters", "look at the circadian lamp and press E", 20),
            T("ai_wristband", Key.E, "Sync your wristband", "your quarters", "look at the wristband display and press E", 20),
            H("cook_eat", "Eat breakfast with the crew", "the Galley (upstairs)", "look at the food warmer and press E", 25),
            H("health_check", "Morning check with the health robot", "your quarters", "look at the health robot dock and press E", 30),
            T("heart_rate_monitor", Key.E, "Check your heart rate", "the Medical bay", "look at the Heart-Rate Monitor and press E", 25),
            T("bp_monitor", Key.E, "Take your blood pressure", "the Medical bay", "look at the Blood-Pressure Tracker on the wall and press E", 25),
            T("medical_scanner", Key.F, "Review your health history", "the Medical bay", "look at the exam couch and press F", 20),
            T("ai_health_hub", Key.F, "Ask the AI to explain your scores", "Command", "look at the AI Health Detection System and press F", 25),
            T("habitat_console", Key.F, "Review the crew wellness trend", "Command", "look at the Habitat Control Console and press F", 20),
            T("eclss_core", Key.F, "Check water recovery", "ECLSS (core, lower deck)", "look at an ECLSS rack and press F", 20),
            T("eclss_core", Key.R, "Replace the CO2 scrubber filter", "ECLSS (core, lower deck)", "look at an ECLSS rack and press R", 40),
            T("oxygen_tanks", Key.E, "Check the oxygen reserve", "Storage", "look at the Oxygen Tank Rack and press E", 25),
            T("greenhouse", Key.F, "Inspect plant health", "the Greenhouse", "look at a grow rack and press F", 20),
            T("greenhouse", Key.G, "Harvest fresh food", "the Greenhouse", "look at a grow rack and press G", 30),
            H("science_task", "Analyse a regolith sample", "the Lab", "look at the glovebox or microscope and press E", 30),
            H("print_spare_part", "Print a spare part", "Maintenance", "look at the 3D printer and press E", 30),
            T("maintenance_drone", Key.E, "Send the drone on an inspection", "Maintenance", "look at the drone and press E", 30),
            T("exercise", Key.E, "Do your daily workout", "the Exercise bay", "look at the treadmill, bike or resistive device and press E", 30),
            T("earth_window", Key.E, "Look at Earth for a minute", "Observation / Exercise bay", "look at the Earth view display and press E", 20),
            T("sanctuary_pod", Key.R, "Take a VR nature break", "the VR retreat", "look at the VR headset dock and press R", 30),
            T("emergency_mh_station", Key.R, "Practise the breathing exercise", "the Recreation commons", "look at the crisis support station and press R", 25),
            T("hologram_companion", Key.E, "Talk to the AI companion", "the upstairs lounge", "look at the hologram table and press E", 30),
            T("hologram_companion", Key.F, "Do your mood check-in", "the upstairs lounge", "look at the hologram table and press F", 20),
            H("team_meeting", "Hold the team meeting", "the Wardroom (upstairs)", "look at the meeting table and press E", 40),
            H("play_game", "Play a board game with the crew", "the Recreation commons", "look at the game table and press E", 25),
            T("family_call", Key.E, "Call home", "the Recreation commons", "look at the Family Call Booth and press E", 30),
            H("sleep", "Go to bed (end of shift)", "your rest pod (upstairs)", "look at your bed and press E", 40),
        };
        List<OpsTask> tasks = new List<OpsTask>(); List<HabInteractable> habs = new List<HabInteractable>(); int idx; public int points, streak;
        SmartObject alertObj; float nextFault = 150f, alertSince; static readonly string[] Breakable = { "eclss_core", "oxygen_tanks", "greenhouse", "maintenance_drone", "heart_rate_monitor", "ai_health_hub", "exercise" };

        bool vr; Image taskBg, alertBg, welcomeBg, cheerBg; Text taskText, alertText, welcomeText, cheerText; float cheerT = -1, welcomeUntil;
        AudioSource sfx; AudioClip ding, alarm; ParticleSystem confetti;

        void Awake() { I = this; }
        public bool WelcomeOpen => welcomeBg && welcomeBg.gameObject.activeSelf;
        public void DebugAlert() { var o = SmartObject.All.FirstOrDefault(x => x && Breakable.Contains(x.key)); if (o) { o.integrity = .22f; alertObj = null; } }
        public void DebugCheer() { if (welcomeBg) welcomeBg.gameObject.SetActive(false); points += 25; Cheer("TASK COMPLETE!  Log how you feel", 25); }
        void Start() { Invoke(nameof(Build), 3.5f); }

        void Build()
        {
            var hud = HUD.I ? HUD.I.HudCanvas : null; if (!hud) return; vr = PlayerRig.I && PlayerRig.I.isXR;
            var root = hud.transform; Vector2 TL = new Vector2(0, 1), TC = new Vector2(0.5f, 1), C = new Vector2(0.5f, 0.5f);
            // tasks: left column under the minimap and the nav line
            taskBg = UIKit.Panel(root, "OpsTasks", TL, new Vector2(16, vr ? -560 : -728), new Vector2(430, 200), new Color(0.03f, 0.06f, 0.13f, 0.9f), TL);
            taskText = UIKit.Label(taskBg.transform, "", 18, Color.white, TL, new Vector2(12, -8), new Vector2(408, 186), TextAnchor.UpperLeft, TL);
            // alerts: under the mission banner and the compass
            alertBg = UIKit.Panel(root, "OpsAlert", TC, new Vector2(0, -250), new Vector2(1000, 100), new Color(0.35f, 0.04f, 0.05f, 0.94f), TC);
            alertText = UIKit.Label(alertBg.transform, "", 19, Color.white, TL, new Vector2(14, -8), new Vector2(972, 90), TextAnchor.UpperLeft, TL);
            alertBg.gameObject.SetActive(false);
            // celebration
            cheerBg = UIKit.Panel(root, "OpsCheer", C, new Vector2(0, 170), new Vector2(760, 120), new Color(0.04f, 0.18f, 0.08f, 0.92f));
            cheerText = UIKit.Label(cheerBg.transform, "", 30, Color.white, C, Vector2.zero, new Vector2(740, 110), TextAnchor.MiddleCenter);
            cheerBg.gameObject.SetActive(false);
            // arrival card
            welcomeBg = UIKit.Panel(root, "HowToPlay", C, new Vector2(0, 40), new Vector2(940, 560), new Color(0.02f, 0.05f, 0.11f, 0.96f));
            welcomeText = UIKit.Label(welcomeBg.transform, vr ? WelcomeVR : Welcome, 21, Color.white, TL, new Vector2(24, -18), new Vector2(892, 530), TextAnchor.UpperLeft, TL);
            welcomeUntil = Time.time + 25f;
            habs = FindObjectsByType<HabInteractable>(FindObjectsSortMode.None).ToList();
            foreach (var t in All) if (t.key != null ? SmartObject.All.Any(o => o && o.key == t.key) : habs.Any(h => h && h.kind == t.hab)) tasks.Add(t);
            sfx = gameObject.AddComponent<AudioSource>(); sfx.spatialBlend = 0; sfx.volume = .5f;
            ding = Tone("ding", new[] { 784f, 988f, 1319f }, .12f); alarm = Tone("alarm", new[] { 880f, 660f, 880f, 660f }, .18f);
            BuildConfetti(); PointNav();
        }

        const string Welcome =
@"<size=30><b><color=#40D9FF>HOW TO PLAY</color></b></size>
<b>Move</b>  W A S D · <b>Look</b>  hold right mouse, or press L to lock the mouse · <b>Run</b> Shift · <b>Jump</b> Space

<b><color=#FFD54F>Use things</color></b>  Look at an object until it gets a <b>coloured outline</b>.
The card at the bottom of the screen says what it is and <b>which key to press</b> (usually <b>E</b>).
  <color=#40FF73>green</color> life support · <color=#5A9EFF>blue</color> mental health · <color=#C773FF>purple</color> medical · <color=#FFD23F>yellow</color> operations · <color=#FF4D4D>red</color> emergency

<b><color=#FFD54F>Daily ops</color></b> (left side)  one job at a time. The <b>arrow</b> on the floor takes you there. Finish it for points.
<b><color=#FF6B6B>Alerts</color></b> (red bar)  something broke. Follow the arrow, look at it and press <b>E</b> to repair it.

<b>Other keys</b>  ENTER do the mission step · I check on crew · M map · N navigate · H all help · O AI off switch

<color=#9FB3C8>Press SPACE or ENTER to start.</color>";
        const string WelcomeVR =
@"<size=30><b><color=#40D9FF>HOW TO PLAY (VR)</color></b></size>
<b>Move</b> left stick · <b>Turn</b> right stick · <b>Teleport</b> grip

<b><color=#FFD54F>Use things</color></b>  Point the right-hand ray at an object until it gets a <b>coloured outline</b>.
The card below says what it is. <b>Trigger</b> = do it.  <b>Click the right stick</b> = choose another option.

<b><color=#FFD54F>Daily ops</color></b> (left panel)  one job at a time; follow the arrow on the floor. Finish it for points.
<b><color=#FF6B6B>Alerts</color></b> (red bar)  something broke: follow the arrow, point at it and pull the <b>trigger</b> to repair.

<color=#9FB3C8>Press A to start.</color>";

        void Update()
        {
            if (!taskBg) return;
            var kb = Keyboard.current;
            bool aBtn = false; if (vr) { var rh = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand); rh.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out aBtn); }
            if (welcomeBg && welcomeBg.gameObject.activeSelf && (Time.time > welcomeUntil || aBtn || (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))))
                welcomeBg.gameObject.SetActive(false);
            // faults: a worn-out system, or a random breakdown every few minutes
            if (!alertObj)
            {
                var worn = SmartObject.All.FirstOrDefault(o => o && o.Failed);
                if (worn) RaiseAlert(worn);
                else if (Time.time > nextFault)
                {
                    nextFault = Time.time + UnityEngine.Random.Range(200f, 320f);
                    var pool = SmartObject.All.Where(o => o && Breakable.Contains(o.key)).ToList();
                    if (pool.Count > 0) { var o = pool[UnityEngine.Random.Range(0, pool.Count)]; o.integrity = .22f; RaiseAlert(o); }
                }
            }
            else if (!alertObj.Failed) { var o = alertObj; alertObj = null; alertBg.gameObject.SetActive(false); points += 50; Cheer($"SYSTEM RESTORED!  {o.title} is working again", 50); PointNav(); }
            else if (Time.time - alertSince > 20f && Time.time % 20f < Time.deltaTime) sfx.PlayOneShot(alarm, .35f);     // gentle reminder every 20 s
            if (alertObj) alertText.text = $"<b><color=#FFB3B3>ALERT</color>  {alertObj.title} needs repair</b>  ({Mathf.RoundToInt(alertObj.integrity * 100)}% condition)\n" +
                (vr ? "1. Follow the arrow   2. Point at it   3. Pull the <b><color=#FFD54F>trigger</color></b> to repair." : "1. Follow the arrow   2. Look at it   3. Press <b><color=#FFD54F>E</color></b> to repair.") + "   The alarm stops once it is fixed.";
            // tasks panel
            var cur = idx < tasks.Count ? tasks[idx] : null;
            taskText.text = "<b><color=#7CFC9A>DAILY OPS</color></b>   <size=15><color=#9FB3C8>" + Mathf.Min(idx + 1, tasks.Count) + " of " + tasks.Count + "   ·   " + points + " pts" + (streak > 1 ? "   ·   streak " + streak : "") + "</color></size>\n" +
                (cur == null ? "<b>All daily jobs done!</b>\nGreat shift, Commander. New jobs tomorrow." :
                $"<size=21><b>{cur.title}</b></size>\n<color=#C9D6E8>1. Go to {cur.room} (follow the arrow)\n2. {Cap(vr ? VrHow(cur.how) : cur.how)}</color>\n<size=15><color=#9FB3C8>+{cur.pts} pts when done</color></size>");
            // celebration fade
            if (cheerT >= 0)
            {
                cheerT += Time.deltaTime; float s = cheerT < .25f ? Mathf.Lerp(.6f, 1.08f, cheerT / .25f) : cheerT < .4f ? Mathf.Lerp(1.08f, 1f, (cheerT - .25f) / .15f) : 1f;
                cheerBg.rectTransform.localScale = Vector3.one * s; if (cheerT > 3.6f) { cheerBg.gameObject.SetActive(false); cheerT = -1; }
            }
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
        static string VrHow(string how) => System.Text.RegularExpressions.Regex.Replace(how.Replace("look at", "point at"), @"press [EFGR]( \([^)]*\))?( or [EFGR]( \([^)]*\))?)?", "pull the trigger (click the right stick to pick the option)");
        void RaiseAlert(SmartObject o)
        {
            alertObj = o; alertSince = Time.time; alertBg.gameObject.SetActive(true); sfx.PlayOneShot(alarm, .5f);
            var wp = Nav.Nearest(o.transform.position); if (wp != null) HUD.I?.SetNavTarget(wp, "REPAIR: " + o.title);
            HUD.I?.Toast($"ALERT: {o.title} is failing. Follow the arrow and repair it.");
        }

        /// called by the smart systems whenever you use one (key = what you pressed)
        public static bool vrSel;   // VR: the trigger runs the action you picked with stick-click, so any action counts
        public void OnHab(HabInteractable h)
        {
            if (!h || idx >= tasks.Count) return; var t = tasks[idx];
            if (t.hab == null || h.kind != t.hab) return;
            if (t.hab == "sleep" && !(h.name.StartsWith("Q1") || h.name.StartsWith("SQ1"))) return;   // your own bed
            idx++; streak++; points += t.pts; Cheer($"TASK COMPLETE!  {t.title}", t.pts); PointNav();
        }
        public void OnUse(SmartObject so, Key key)
        {
            if (!so || idx >= tasks.Count) return; var t = tasks[idx];
            if (t.key == null || so.key != t.key || (t.press.HasValue && t.press.Value != key && !(vrSel && key != Key.None))) return;
            idx++; streak++; points += t.pts; Cheer($"TASK COMPLETE!  {t.title}", t.pts); PointNav();
        }

        void PointNav()
        {
            if (alertObj || !HUD.I) return; var cur = idx < tasks.Count ? tasks[idx] : null; if (cur == null) { if (HUD.I.NavLabel != null && HUD.I.NavLabel.StartsWith("TASK")) HUD.I.SetNavTarget("", ""); return; }
            Vector3 me = PlayerRig.I ? PlayerRig.I.FeetPosition : Vector3.zero; Transform target = null;
            if (cur.key != null) target = SmartObject.All.Where(o => o && o.key == cur.key).OrderBy(o => (o.transform.position - me).sqrMagnitude).Select(o => o.transform).FirstOrDefault();
            else target = habs.Where(h => h && h.kind == cur.hab && (cur.hab != "sleep" || h.name.StartsWith("Q1"))).OrderBy(h => (h.transform.position - me).sqrMagnitude).Select(h => h.transform).FirstOrDefault();
            var wp = target ? Nav.Nearest(target.GetComponent<Renderer>() ? target.GetComponent<Renderer>().bounds.center : target.position) : null;
            if (wp != null && (string.IsNullOrEmpty(HUD.I.NavGoal) || (HUD.I.NavLabel ?? "").StartsWith("TASK") || (HUD.I.NavLabel ?? "").StartsWith("REPAIR"))) HUD.I.SetNavTarget(wp, "TASK: " + cur.title);
        }

        void Cheer(string msg, int pts)
        {
            if (!cheerBg) return;
            cheerText.text = $"<b>{msg}</b>\n<size=26><color=#7CFFB2>+{pts} points</color>   <color=#9FB3C8>total {points}</color></size>";
            cheerBg.gameObject.SetActive(true); cheerT = 0; sfx.PlayOneShot(ding, .7f);
            var cam = PlayerRig.I ? PlayerRig.I.cam : Camera.main;
            if (confetti && cam) { confetti.transform.position = cam.transform.position + cam.transform.forward * 1.6f + Vector3.up * .3f; confetti.Emit(140); }
        }

        void BuildConfetti()
        {
            var g = new GameObject("OpsConfetti"); confetti = g.AddComponent<ParticleSystem>(); confetti.Stop();
            var m = confetti.main; m.loop = false; m.playOnAwake = false; m.startLifetime = 1.8f; m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            m.startSize = new ParticleSystem.MinMaxCurve(.03f, .07f); m.gravityModifier = .35f; m.maxParticles = 400; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .84f, .3f), new Color(.3f, .9f, 1f)) { mode = ParticleSystemGradientMode.TwoColors };
            var em = confetti.emission; em.rateOverTime = 0; var sh = confetti.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .2f;
            var r = g.GetComponent<ParticleSystemRenderer>(); var sh2 = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"); if (sh2) r.material = new Material(sh2);
        }

        static AudioClip Tone(string name, float[] notes, float each)
        {
            int sr = 44100, n = (int)(sr * each); var data = new float[n * notes.Length];
            for (int k = 0; k < notes.Length; k++) for (int i = 0; i < n; i++) { float t = i / (float)sr; data[k * n + i] = Mathf.Sin(2 * Mathf.PI * notes[k] * t) * Mathf.Exp(-t * 9f) * .6f; }
            var c = AudioClip.Create(name, data.Length, 1, sr, false); c.SetData(data, 0); return c;
        }
    }
}
