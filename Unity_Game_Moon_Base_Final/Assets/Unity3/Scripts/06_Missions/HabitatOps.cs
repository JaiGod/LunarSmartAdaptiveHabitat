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
        class OpsTask { public string key, title, how, room; public Key? press; public int pts; }
        static readonly OpsTask[] All =
        {
            new OpsTask { key = "mood_tracker", title = "Log how you feel", room = "the quarters hallway", how = "look at the Mood Tracker and press E", press = null, pts = 25 },
            new OpsTask { key = "heart_rate_monitor", title = "Check your heart rate", room = "the Medical bay", how = "look at the Heart-Rate Monitor and press E", press = Key.E, pts = 25 },
            new OpsTask { key = "bp_monitor", title = "Take your blood pressure", room = "the Medical bay", how = "look at the Blood-Pressure Tracker on the wall and press E", press = Key.E, pts = 25 },
            new OpsTask { key = "hologram_companion", title = "Check in with the AI companion", room = "the upstairs lounge", how = "look at the hologram table and press E", press = Key.E, pts = 30 },
            new OpsTask { key = "greenhouse", title = "Harvest the greenhouse", room = "the Greenhouse", how = "look at a grow rack and press G", press = Key.G, pts = 30 },
            new OpsTask { key = "oxygen_tanks", title = "Check the oxygen reserve", room = "Storage", how = "look at the Oxygen Tank Rack and press E", press = Key.E, pts = 25 },
            new OpsTask { key = "exercise", title = "Do your daily workout", room = "the Exercise bay", how = "look at the treadmill, bike or resistive device and press E", press = Key.E, pts = 30 },
            new OpsTask { key = "maintenance_drone", title = "Send the drone on an inspection", room = "Maintenance", how = "look at the drone and press E", press = Key.E, pts = 30 },
            new OpsTask { key = "family_call", title = "Call home", room = "the Recreation commons", how = "look at the Family Call Booth and press E", press = Key.E, pts = 30 },
            new OpsTask { key = "smart_bed", title = "Check your sleep data", room = "your rest pod", how = "look at your bed and press F", press = Key.F, pts = 20 },
        };
        List<OpsTask> tasks = new List<OpsTask>(); int idx; public int points, streak;
        SmartObject alertObj; float nextFault = 150f, alertSince; static readonly string[] Breakable = { "eclss_core", "oxygen_tanks", "greenhouse", "maintenance_drone", "heart_rate_monitor", "ai_health_hub", "exercise" };

        Image taskBg, alertBg, welcomeBg, cheerBg; Text taskText, alertText, welcomeText, cheerText; float cheerT = -1, welcomeUntil;
        AudioSource sfx; AudioClip ding, alarm; ParticleSystem confetti;

        void Awake() { I = this; }
        public bool WelcomeOpen => welcomeBg && welcomeBg.gameObject.activeSelf;
        public void DebugAlert() { var o = SmartObject.All.FirstOrDefault(x => x && Breakable.Contains(x.key)); if (o) { o.integrity = .22f; alertObj = null; } }
        public void DebugCheer() { if (welcomeBg) welcomeBg.gameObject.SetActive(false); points += 25; Cheer("TASK COMPLETE!  Log how you feel", 25); }
        void Start() { Invoke(nameof(Build), 3.5f); }

        void Build()
        {
            var hud = HUD.I ? HUD.I.HudCanvas : null; if (!hud || (PlayerRig.I && PlayerRig.I.isXR)) return;
            var root = hud.transform; Vector2 TL = new Vector2(0, 1), TC = new Vector2(0.5f, 1), C = new Vector2(0.5f, 0.5f);
            // tasks: left column under the minimap and the nav line
            taskBg = UIKit.Panel(root, "OpsTasks", TL, new Vector2(16, -728), new Vector2(430, 200), new Color(0.03f, 0.06f, 0.13f, 0.9f), TL);
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
            welcomeText = UIKit.Label(welcomeBg.transform, Welcome, 21, Color.white, TL, new Vector2(24, -18), new Vector2(892, 530), TextAnchor.UpperLeft, TL);
            welcomeUntil = Time.time + 25f;
            foreach (var t in All) if (SmartObject.All.Any(o => o && o.key == t.key)) tasks.Add(t);
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

        void Update()
        {
            if (!taskBg) return;
            var kb = Keyboard.current;
            if (welcomeBg && welcomeBg.gameObject.activeSelf && (Time.time > welcomeUntil || (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))))
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
                $"1. Follow the arrow   2. Look at it   3. Press <b><color=#FFD54F>E</color></b> to repair.   The alarm stops once it is fixed.";
            // tasks panel
            var cur = idx < tasks.Count ? tasks[idx] : null;
            taskText.text = "<b><color=#7CFC9A>DAILY OPS</color></b>   <size=15><color=#9FB3C8>" + Mathf.Min(idx + 1, tasks.Count) + " of " + tasks.Count + "   ·   " + points + " pts" + (streak > 1 ? "   ·   streak " + streak : "") + "</color></size>\n" +
                (cur == null ? "<b>All daily jobs done!</b>\nGreat shift, Commander. New jobs tomorrow." :
                $"<size=21><b>{cur.title}</b></size>\n<color=#C9D6E8>1. Go to {cur.room} (follow the arrow)\n2. {char.ToUpper(cur.how[0]) + cur.how.Substring(1)}</color>\n<size=15><color=#9FB3C8>+{cur.pts} pts when done</color></size>");
            // celebration fade
            if (cheerT >= 0)
            {
                cheerT += Time.deltaTime; float s = cheerT < .25f ? Mathf.Lerp(.6f, 1.08f, cheerT / .25f) : cheerT < .4f ? Mathf.Lerp(1.08f, 1f, (cheerT - .25f) / .15f) : 1f;
                cheerBg.rectTransform.localScale = Vector3.one * s; if (cheerT > 3.6f) { cheerBg.gameObject.SetActive(false); cheerT = -1; }
            }
        }

        void RaiseAlert(SmartObject o)
        {
            alertObj = o; alertSince = Time.time; alertBg.gameObject.SetActive(true); sfx.PlayOneShot(alarm, .5f);
            var wp = Nav.Nearest(o.transform.position); if (wp != null) HUD.I?.SetNavTarget(wp, "REPAIR: " + o.title);
            HUD.I?.Toast($"ALERT: {o.title} is failing. Follow the arrow and repair it.");
        }

        /// called by the smart systems whenever you use one (key = what you pressed)
        public void OnUse(SmartObject so, Key key)
        {
            if (!so || idx >= tasks.Count) return; var t = tasks[idx];
            if (so.key != t.key || (t.press.HasValue && t.press.Value != key)) return;
            idx++; streak++; points += t.pts; Cheer($"TASK COMPLETE!  {t.title}", t.pts); PointNav();
        }

        void PointNav()
        {
            if (alertObj || !HUD.I) return; var cur = idx < tasks.Count ? tasks[idx] : null; if (cur == null) { if (HUD.I.NavLabel != null && HUD.I.NavLabel.StartsWith("TASK")) HUD.I.SetNavTarget("", ""); return; }
            var target = SmartObject.All.Where(o => o && o.key == cur.key).OrderBy(o => PlayerRig.I ? (o.transform.position - PlayerRig.I.FeetPosition).sqrMagnitude : 0).FirstOrDefault();
            var wp = target ? Nav.Nearest(target.transform.position) : null;
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
