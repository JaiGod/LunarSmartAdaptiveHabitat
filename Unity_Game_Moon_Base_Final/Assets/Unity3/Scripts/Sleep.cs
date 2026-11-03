using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDevices = UnityEngine.XR.InputDevices;

namespace Hunch3
{
    /// SLEEP — you really sleep: one real minute = a full night (8 h sim). Pod lights go OUT (1-lux amber night-light only),
    /// the wristband records sleep architecture live, and the last 10% of the minute runs a dawn-simulation sunrise.
    /// Anything dangerous (Hazards) wakes you instantly: alarm lights, klaxon, muster instructions.
    public class SleepSystem : MonoBehaviour
    {
        public static SleepSystem I;
        public static bool PlayerAsleep => I && I.asleep;
        public const float NightRealSeconds = 60f, NapRealSeconds = 20f;
        bool asleep; float realT, realLen, simHoursTotal, startHour, prevScale; int interruptions; string wokeBy = "";
        float deepH, remH, lightH;
        Canvas overlay; Image dark; Text info;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) Make(); };
            if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) Make();
        }
        static void Make()
        {
            if (FindFirstObjectByType<SleepSystem>()) return;
            var g = new GameObject("SleepAndSafety"); g.AddComponent<SleepSystem>(); g.AddComponent<Hazards>();
        }
        void Awake() { I = this; }

        public float Progress => asleep && realLen > 0 ? realT / realLen : 0f;
        public float DawnK => asleep ? Mathf.InverseLerp(0.88f, 1f, realT / Mathf.Max(1, realLen)) : 0f;   // 0..1 sunrise ramp

        Vector3 standPos; bool haveStand;
        public void BeginSleep() => BeginSleep(null);
        /// Lie down in THIS bed (the one you used), and wake up standing exactly where you were — same deck, same room.
        public void BeginSleep(Transform bedT)
        {
            var pr = PlayerRig.I; if (!pr || asleep) return;
            if (Hazards.I && Hazards.I.Active) { HUD.I?.Toast("You can't sleep during an alarm — get to the muster point."); return; }
            if (pr.suit == SuitState.EVA) { HUD.I?.Toast("Take your suit off at the suitport before sleeping."); return; }
            var me = Sim.I.engine.C(1);
            float h = SimClock.HourOfDay; bool night = h >= 20 || h < 6;
            simHoursTotal = night ? Mathf.Clamp((7f - h + 24f) % 24f, 6f, 8.5f) : 1.5f;
            realLen = night ? NightRealSeconds : NapRealSeconds;
            prevScale = GameState.timeScale;
            GameState.timeScale = simHoursTotal * 3600f / realLen;           // e.g. 8 h in 60 s → 1 real s = 8 sim min
            startHour = SimClock.hours; realT = 0; interruptions = 0; wokeBy = ""; deepH = remH = lightH = 0;
            asleep = true; me.asleep = true; me.inPod = true; me.activity = night ? "sleeping (pod)" : "nap (pod)";
            // lie down on the mattress
            if (pr.cc) pr.cc.enabled = false;
            standPos = pr.FeetPosition; haveStand = true;
            var bed = bedT ? bedT : Util.Find("Q1_Bed_Mattress");
            if (bed && Vector3.Distance(bed.position, standPos) > 4f && !bedT) bed = null;       // never jump to a far-away bed
            var rr = bed ? bed.GetComponentInChildren<Renderer>() : null;
            Vector3 c = rr ? rr.bounds.center : (bed ? bed.position : standPos);
            float top = rr ? rr.bounds.max.y : standPos.y + 0.5f;
            pr.origin.transform.position = new Vector3(c.x, standPos.y, c.z);                     // stay on the floor you were standing on
            Util.SetEyeHeight(pr.cam, pr.origin.transform, Mathf.Max(0.25f, top - standPos.y) + 0.18f);
            if (!pr.isXR) pr.cam.transform.localRotation = Quaternion.Euler(-75f, 0, 0);   // looking up at the ceiling mood ring
            BuildOverlay();
            Sim.I.engine.Log(1, "sleep", $"Commander in pod — {(night ? "night sleep" : "nap")}, pod lights OFF, blue light suppressed, pod 18.5 °C, white-noise mask.");
            HUD.I?.Toast(night ? "Lights out. Good night, Commander. (1 real minute = the whole night · E / any button wakes you)" : "Nap: 90 min in 20 s. (E / any button wakes you)");
        }

        public void Wake(string reason = "")
        {
            if (!asleep) return;
            var pr = PlayerRig.I; var me = Sim.I.engine.C(1);
            float slept = SimClock.hours - startHour;
            asleep = false; me.asleep = false; me.activity = "awake";
            GameState.timeScale = string.IsNullOrEmpty(reason) ? prevScale : 60f;
            if (overlay) Destroy(overlay.gameObject);
            if (pr)
            {
                var wp = haveStand ? standPos : (Nav.wp.ContainsKey("WP_Pod1_Bed") ? Nav.wp["WP_Pod1_Bed"].position : pr.FeetPosition);
                pr.Teleport(wp); haveStand = false; if (!pr.isXR) { Util.SetEyeHeight(pr.cam, pr.origin.transform, 1.65f); pr.cam.transform.localRotation = Quaternion.identity; }
            }
            float eff = Mathf.Clamp01(0.97f - 0.35f * me.stress - 0.12f * interruptions);
            me.sleepQ = Mathf.Lerp(me.sleepQ, eff, Mathf.Clamp01(slept / 6f));
            if (!string.IsNullOrEmpty(reason)) { interruptions++; wokeBy = reason; }
            string rep = $"Slept {slept:0.0} h · efficiency {eff:P0} · deep {deepH:0.0} h · REM {remH:0.0} h · light {lightH:0.0} h" + (string.IsNullOrEmpty(reason) ? " · woke with the sunrise light" : $" · WOKEN BY: {reason}");
            Sim.I.engine.Log(1, "sleep", rep);
            HUD.I?.Toast(string.IsNullOrEmpty(reason) ? "Good morning. " + rep : "⚠ " + reason + " — you were woken. Lights are ON.");
        }

        void BuildOverlay()
        {
            var pr = PlayerRig.I;
            overlay = UIKit.Canvas("SleepOverlay", true, new Vector2(1600, 1000), pr.cam.transform);
            overlay.transform.localPosition = new Vector3(0, 0, 0.32f); overlay.transform.localRotation = Quaternion.identity; overlay.transform.localScale = Vector3.one * 0.0005f;
            overlay.sortingOrder = 50;
            dark = UIKit.Panel(overlay.transform, "Dark", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6000, 6000), new Color(0, 0, 0, 0.9f)); dark.raycastTarget = false;
            info = UIKit.Label(overlay.transform, "", 30, new Color(1f, 0.75f, 0.45f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1300, 700), TextAnchor.MiddleCenter);
        }

        static string Stage(float minutesAsleep, out int idx)
        {
            // 90-min cycles; deep sleep front-loaded, REM grows each cycle (normal sleep architecture)
            float m = minutesAsleep; if (m < 12) { idx = 1; return "N1 (falling asleep)"; }
            int cyc = (int)(m / 90f); float p = (m % 90f) / 90f; float remShare = Mathf.Lerp(0.12f, 0.4f, cyc / 4f), deepShare = Mathf.Lerp(0.4f, 0.05f, cyc / 4f);
            if (p < 0.5f - deepShare / 2) { idx = 2; return "N2 (light)"; }
            if (p < 0.5f + deepShare / 2) { idx = 3; return "N3 (deep, slow-wave)"; }
            if (p > 1 - remShare) { idx = 4; return "REM (dreaming)"; }
            idx = 2; return "N2 (light)";
        }

        void Update()
        {
            if (!asleep) return;
            realT += Time.deltaTime;
            float simMin = (SimClock.hours - startHour) * 60f;
            string st = Stage(simMin, out int idx);
            float dh = Time.deltaTime * GameState.timeScale / 3600f;
            if (idx == 3) deepH += dh; else if (idx == 4) remH += dh; else lightH += dh;
            var me = Sim.I.engine.C(1);
            float hr = 48 + 8 * me.stress + (idx == 4 ? 6 : idx == 3 ? -4 : 0) + 2 * Mathf.Sin(Time.time * 2f), hrv = 85 - 40 * me.stress + (idx == 3 ? 10 : 0);
            float dawn = DawnK;
            if (dark) dark.color = new Color(dawn * 0.35f, dawn * 0.2f, dawn * 0.05f, 0.9f - 0.55f * dawn);
            if (info) info.text =
                $"<size=54>{(dawn > 0 ? "☀  sunrise" : "z z z")}</size>\n\n<b>{SimClock.Now}</b>   ({simMin / 60f:0.0} h of {simHoursTotal:0.0} h)\n\n" +
                $"Sleep stage: <b>{st}</b>\nHeart rate {hr:0} bpm · HRV {hrv:0} ms · wristband + beacon (sleep architecture sensor)\n" +
                $"Pod: lights <b>{(dawn > 0 ? $"dawn simulation {Mathf.Lerp(1800, 4000, dawn):0} K" : "OFF")}</b> · 18.5 °C · airflow 0.1 m/s · pink-noise mask\n" +
                $"Habitat: {(Hazards.I && Hazards.I.Active ? "<color=#ff5544>ALARM</color>" : "all systems nominal — the AI is watching so you can rest")}\n\n" +
                "<size=24>E / A / trigger = wake up early</size>";
            bool wakeKey = (Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)) || AnyXRButton();
            if (realT > 1.0f && wakeKey) { simHoursTotal = SimClock.hours - startHour; Wake(); return; }
            if (realT >= realLen) Wake();
        }

        bool prevXR;
        bool AnyXRButton()
        {
            bool now = false;
            foreach (var node in new[] { XRNode.LeftHand, XRNode.RightHand })
            {
                var d = InputDevices.GetDeviceAtXRNode(node); if (!d.isValid) continue;
                if (d.TryGetFeatureValue(CommonUsages.primaryButton, out bool a) && a) now = true;
                if (d.TryGetFeatureValue(CommonUsages.triggerButton, out bool t) && t) now = true;
            }
            bool edge = now && !prevXR; prevXR = now; return edge;
        }
    }

    /// HAZARDS & ALARMS — "lights go off when someone is in danger".
    /// Solar particle event, CO2 scrubber fault, slow pressure leak, smoke, medical emergency.
    /// Every light in the habitat switches to a red alarm pulse, every pod light comes ON (wakes sleepers), klaxon sounds,
    /// beacons flash, all crew (AI and you) muster at the right safe place. All-clear once everyone is accounted for.
    public class Hazards : MonoBehaviour
    {
        public static Hazards I;
        public bool Active; public string Kind = "", Title = "", Muster = "", MusterLabel = "";
        public int Patient; float startReal, mustered; float nextNightRoll = -1; bool nightEventDone; float eventAtSleepK;
        AudioSource klaxon; int lastEscCount;
        public static readonly string[] Kinds = { "SPE", "CO2", "LEAK", "SMOKE", "MEDICAL" };

        void Awake() { I = this; }
        void Start()
        {
            klaxon = gameObject.AddComponent<AudioSource>(); klaxon.loop = true; klaxon.spatialBlend = 0; klaxon.volume = 0.35f;
            int sr = 22050; var clip = AudioClip.Create("klaxon", sr, 1, sr, false); var d = new float[sr];
            for (int i = 0; i < sr; i++) { float tt = (float)i / sr; d[i] = (tt % 0.5f < 0.3f ? 0.5f : 0f) * Mathf.Sign(Mathf.Sin(2 * Mathf.PI * (tt % 0.5f < 0.15f ? 880 : 660) * tt)) * 0.4f; }
            clip.SetData(d, 0); klaxon.clip = clip;
        }

        public void Trigger(string kind, string cause = "")
        {
            if (Active) return;
            Active = true; Kind = kind; startReal = Time.time; mustered = 0;
            switch (kind)
            {
                case "SPE": Title = "SOLAR PARTICLE EVENT — radiation storm"; Muster = "WP_H_C"; MusterLabel = "Storm shelter (Medical module, water-wall)"; break;
                case "CO2": Title = "CO2 SCRUBBER FAULT — CO2 rising above 2,500 ppm in the work bays"; Muster = "WP_Up_Table"; MusterLabel = "Wardroom (clean-air zone)"; break;
                case "LEAK": Title = "PRESSURE LEAK — 0.3 kPa/min drop in a hallway"; Muster = "WP_Up_Table"; MusterLabel = "Core wardroom (hatches to leaking module closed)"; break;
                case "SMOKE": Title = "SMOKE DETECTED — galley/maintenance"; Muster = "WP_N_C"; MusterLabel = "Command module (put on PBA masks)"; break;
                default:
                    Kind = "MEDICAL"; Patient = Random.Range(3, 5);
                    Title = $"MEDICAL EMERGENCY — {Sim.I.engine.C(Patient).name} collapsed"; Muster = "WP_H_C"; MusterLabel = "Medical bay (bring the patient; CMO leads)"; break;
            }
            if (!string.IsNullOrEmpty(cause)) Title += " · " + cause;
            if (HabitatEnvironment.I && kind == "LEAK") HabitatEnvironment.I.pressureKPa -= 1.2f;
            foreach (var c in Sim.I.engine.crew) { c.stress = Mathf.Min(1, c.stress + 0.08f); c.asleep = false; }
            Sim.I.engine.Log(0, "alarm", $"ALARM: {Title}. All pod lights ON, red alarm lighting, klaxon. Muster: {MusterLabel}. (Stress signals during the alarm are context-tagged — not counted as anxiety.)", 3);
            if (SleepSystem.PlayerAsleep) SleepSystem.I.Wake(Title);
            GameState.timeScale = 60f;
            klaxon.Play();
            HUD.I?.SetNavTarget(Muster, MusterLabel);
            HUD.I?.Procedure($"⚠ {Title}\nGo to: {MusterLabel}  (follow the arrow)");
        }

        public void Clear(string how)
        {
            if (!Active) return;
            Active = false; klaxon.Stop(); HUD.I?.Procedure("");
            if (Kind == "LEAK" && HabitatEnvironment.I) HabitatEnvironment.I.pressureKPa = 70.3f;
            foreach (var c in Sim.I.engine.crew) { c.lastSocialHour = SimClock.hours; c.mood = Mathf.Min(1, c.mood + 0.02f); }
            Sim.I.engine.Log(0, "alarm", $"ALL CLEAR ({Kind}): {how}. Lighting restoring gradually; event logged for mission control & the after-action team meeting.");
            HUD.I?.Toast($"✅ ALL CLEAR — {how}. Lights fade back to normal. This drill/event will be reviewed at the next team meeting.");
            Kind = "";
        }

        bool AtMuster(Vector3 p) { if (!Nav.wp.ContainsKey(Muster)) return true; var m = Nav.wp[Muster].position; return Vector2.Distance(new Vector2(p.x, p.z), new Vector2(m.x, m.z)) < 3.2f && Mathf.Abs(p.y - m.y) < 1.6f; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f6Key.wasPressedThisFrame) { if (Active) Clear("drill ended by Commander"); else Trigger(Kinds[Random.Range(0, Kinds.Length)], "DRILL"); }
            // random danger at night while the player sleeps (~35% of nights)
            if (SleepSystem.PlayerAsleep)
            {
                if (nextNightRoll < 0) { nightEventDone = Random.value > 0.35f; eventAtSleepK = Random.Range(0.25f, 0.7f); nextNightRoll = 1; }
                if (!nightEventDone && SleepSystem.I && SleepSystem.I.Progress > eventAtSleepK) { nightEventDone = true; Trigger(Kinds[Random.Range(0, Kinds.Length)]); }
            }
            else nextNightRoll = -1;
            // a Tier-3 psychological escalation at night wakes the CMO (her pod light comes on, gentle amber)
            var e = Sim.I != null ? Sim.I.engine : null;
            if (e != null && e.escalatedTo.Count > lastEscCount) { var cmo = e.C(e.cmoId); if (cmo.asleep) e.Log(e.cmoId, "wake", $"CMO woken by Tier-3 escalation — pod light ON (amber), wrist haptic. (Not a habitat alarm: other crew keep sleeping.)", 3); }
            if (e != null) lastEscCount = e.escalatedTo.Count;

            if (!Active) return;
            // muster check: everybody (you + AI crew) at the safe place
            var pr = PlayerRig.I;
            bool youThere = pr && AtMuster(pr.FeetPosition);
            int crewThere = CrewAgent.All.Count(a => AtMuster(a.transform.position));
            bool all = youThere && crewThere >= CrewAgent.All.Count;
            HUD.I?.Procedure($"⚠ {Title}\nMuster: {MusterLabel} — crew accounted for {crewThere + (youThere ? 1 : 0)}/4" + (all ? $"  ·  all clear in {Mathf.Max(0, 8 - mustered):0} s" : "  (follow the arrow)"));
            if (all) { mustered += Time.deltaTime; if (mustered > 8f) Clear("all 4 crew accounted for at the " + MusterLabel); }
            else mustered = 0;
            if (Time.time - startReal > 180f) Clear("timed out — Commander did not reach the muster point (logged)");
        }

        /// Light override used by HabitatEnvironment: returns true if alarm lighting is active and gives the colour/intensity multiplier.
        public static bool AlarmLight(out Color col, out float mul)
        {
            col = new Color(1f, 0.12f, 0.08f); mul = 0;
            if (!I || !I.Active) return false;
            mul = 0.6f + 0.9f * Mathf.Abs(Mathf.Sin(Time.time * 3.2f));
            return true;
        }
    }
}
