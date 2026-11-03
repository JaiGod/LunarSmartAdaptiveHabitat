using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Hunch3
{
    /// AI ROBOTIC HEALTH STATION (one per private quarters, SQ1..SQ4).
    /// A wall-folded 3-joint arm with a blood-pressure cuff / pulse-oximeter tool, an instrument dock (thermometer, AED)
    /// and a status ring. It is a *physical-health* sensor that complements the psychological sensor array:
    ///   • Voluntary morning check (~2 min): BP, SpO2, temperature, resting HR, HRV → personal baseline for the AI.
    ///   • Consent first: it only unfolds when YOU ask (E / trigger on the dock) — private-zone rule: AI acts on invitation.
    ///   • Results go to your own log. Shared with the CMO only if you tap Share, or automatically if you are already
    ///     at Tier 3 (CMO already in the loop) — never to other crew.
    ///   • At Tier 3 the status ring glows amber and the screen offers a check; it never deploys uninvited.
    ///   • Night: a 60 GHz radar puck in the sleep capsule tracks breathing/heart rate contact-free (no camera, no mic).
    public class HealthBot : MonoBehaviour
    {
        public static readonly Dictionary<int, HealthBot> All = new Dictionary<int, HealthBot>();
        public int owner;
        Transform shoulder, upper, elbow, fore, wrist, ring, dock;
        Quaternion qs, qu, qe, qf, qw; Vector3 axis, roomCenter; float sgn = 1, deploy, target; bool busy;
        Renderer ringR; MaterialPropertyBlock mpb;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) Make(); };
            if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) Make();
        }
        static void Make()
        {
            All.Clear();
            for (int n = 1; n <= 4; n++)
            {
                var b = Util.Find($"SQ{n}_HealthBot_Base"); if (!b) continue;
                var hb = b.gameObject.AddComponent<HealthBot>(); hb.owner = n;
            }
        }

        void Start()
        {
            All[owner] = this; mpb = new MaterialPropertyBlock();
            string p = $"SQ{owner}_HealthBot_";
            shoulder = Util.Find(p + "Shoulder"); upper = Util.Find(p + "UpperArm"); elbow = Util.Find(p + "Elbow"); fore = Util.Find(p + "Forearm");
            wrist = Util.Find(p + "Wrist_ToolChanger"); ring = Util.Find(p + "StatusRing"); dock = Util.Find(p + "DockTray");
            var z = Util.Find($"ZONE_Private_Quarters{owner}"); roomCenter = z ? z.position : transform.position;
            if (shoulder) qs = shoulder.localRotation; if (elbow) qe = elbow.localRotation; if (wrist) qw = wrist.localRotation;
            Vector3 n = roomCenter - transform.position; n.y = 0; n.Normalize();
            axis = Vector3.Cross(Vector3.up, n);
            if (ring) ringR = ring.GetComponent<Renderer>();
            // pick the swing direction that brings the tool toward the room
            if (shoulder && wrist)
            {
                Pose(1f); float d1 = Dist(); sgn = -1; Pose(1f); float d2 = Dist(); sgn = d1 < d2 ? 1 : -1; Pose(0f);
            }
        }
        float Dist() { var a = wrist.position - roomCenter; a.y = 0; return a.magnitude; }

        void Pose(float t)
        {
            if (!shoulder) return;
            shoulder.localRotation = qs; if (elbow) elbow.localRotation = qe; if (wrist) wrist.localRotation = qw;
            float k = Mathf.SmoothStep(0, 1, t);
            shoulder.RotateAround(shoulder.position, axis, sgn * 75f * k);
            if (elbow) elbow.RotateAround(elbow.position, axis, sgn * 95f * k);
            if (wrist) wrist.RotateAround(wrist.position, axis, -sgn * 20f * k);
        }

        void Update()
        {
            deploy = Mathf.MoveTowards(deploy, target, Time.deltaTime / 1.6f);
            Pose(deploy);
            if (Sim.I == null || !ringR) return;
            var c = Sim.I.engine.C(owner);
            Color col = busy ? new Color(0.2f, 0.9f, 1f) * (1.5f + Mathf.Sin(Time.time * 6f)) : c.MaxTier >= 3 ? new Color(1f, 0.6f, 0.15f) * (1.2f + 0.6f * Mathf.Sin(Time.time * 2f)) : c.asleep ? new Color(0.05f, 0.08f, 0.1f) : new Color(0.2f, 0.7f, 1f) * 0.8f;
            mpb.Clear(); mpb.SetColor("_EmissionColor", col); mpb.SetColor("_BaseColor", col); ringR.SetPropertyBlock(mpb);
            // AI crewmates use their station each morning (07:00–07:15)
            if (owner != 1 && !busy)
            {
                var ag = CrewAgent.All.FirstOrDefault(a => a.crewId == owner);
                bool there = ag && ag.task.Contains("health check") && (ag.transform.position - transform.position).magnitude < 4.5f;
                target = there ? 1f : 0f;
            }
        }

        public void Request(PlayerRig pr)
        {
            if (busy) return;
            if (owner != 1) { var o = Sim.I.engine.C(owner); HUD.I?.Toast($"This is {o.name}'s health station — private. Yours is in Quarters 1."); return; }
            StartCoroutine(Check(pr));
        }

        IEnumerator Check(PlayerRig pr)
        {
            busy = true; var e = Sim.I.engine; var c = e.C(1);
            HUD.I?.Procedure("AI HEALTH STATION · consent\nIt will measure blood pressure, SpO2, temperature and HRV (≈2 min, real time ≈10 s).\nPress Y / A to start · R to decline");
            float t0 = Time.time; bool? yes = null;
            while (Time.time - t0 < 12f && yes == null)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.yKey.wasPressedThisFrame) yes = true;
                if (kb != null && kb.rKey.wasPressedThisFrame) yes = false;
                if (XRYes()) yes = true;
                yield return null;
            }
            if (yes != true)
            {
                HUD.I?.Procedure(""); HUD.I?.Toast("Health check declined — nothing measured, nothing logged except 'declined'. The arm stays folded.");
                e.Log(1, "health", "Commander declined the robotic health check (respected; no data captured)."); busy = false; yield break;
            }
            string[] steps = { "Arm unfolding — rest your left arm on the cuff", "Cuff inflating… blood pressure", "Pulse-oximeter clip · SpO2 + resting heart rate", "Infrared thermometer · skin temperature", "Arm folding back" };
            target = 1f;
            for (int i = 0; i < steps.Length; i++)
            {
                if (i == steps.Length - 1) target = 0f;
                HUD.I?.Procedure($"AI HEALTH STATION · step {i + 1}/{steps.Length}\n{steps[i]}");
                yield return new WaitForSeconds(i == 0 ? 2.2f : 1.8f);
            }
            int sys = Mathf.RoundToInt(112 + 22 * c.stress + Random.Range(-4, 5)), dia = Mathf.RoundToInt(72 + 12 * c.stress + Random.Range(-3, 4));
            int spo2 = Random.Range(96, 100); float temp = 36.6f + 0.3f * c.stress + Random.Range(-0.1f, 0.15f);
            int hr = Mathf.RoundToInt(58 + 25 * c.stress + 8 * c.fatigue); int hrv = Mathf.RoundToInt(78 - 40 * c.stress - 15 * c.fatigue);
            bool flag = sys > 135 || spo2 < 95 || temp > 37.6f;
            string res = $"BP {sys}/{dia} mmHg · SpO2 {spo2}% · {temp:0.0} °C · HR {hr} bpm · HRV {hrv} ms" + (flag ? "  ⚠ outside your baseline" : "  ✓ within your baseline");
            e.Log(1, "health", "Robotic health check (consented): " + res + (c.shareHealthWithCmo || c.MaxTier >= 3 ? " — shared with the CMO." : " — stored in YOUR log only."));
            c.lastSocialHour = c.lastSocialHour; c.stress = Mathf.Max(0, c.stress - 0.01f);   // reassurance effect is tiny but real
            HUD.I?.Procedure("");
            HUD.I?.Toast("🩺 " + res + (c.shareHealthWithCmo || c.MaxTier >= 3 ? "\nShared with: you + the CMO (your setting / Tier 3 active)." : "\nShared with: only you. (Crew Interface → Privacy → share with CMO if you want.)") + " Physical-health countermeasure: 2 h exercise today keeps bone & muscle loss down in 1/6 g.");
            busy = false;
        }

        bool XRYes()
        {
            var d = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            return d.isValid && d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool a) && a;
        }
    }
}
