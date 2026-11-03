using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hunch3
{
    /// Properties exported from Blender (interact, collider, zone, sensor_type ...) attached on import.
    public class HabTag : MonoBehaviour
    {
        public List<string> keys = new List<string>();
        public List<string> values = new List<string>();
        public string Get(string k, string def = "")
        {
            int i = keys.IndexOf(k);
            return i >= 0 ? values[i] : def;
        }
        public void Set(string k, string v)
        {
            int i = keys.IndexOf(k);
            if (i >= 0) values[i] = v; else { keys.Add(k); values.Add(v); }
        }
        public string Interact => Get("interact");
        public string ColliderKind => Get("collider", "box");
    }

    public enum GameMode { Sandbox, Tour, Scenario, Tabletop, InterfaceDemo }

    /// Settings that survive scene changes.
    public static class GameState
    {
        public static GameMode mode = GameMode.Sandbox;
        public static int scenario = 0;          // 1..6 = board questions Q1..Q6
        public static bool smoothMove = true;
        public static bool snapTurn = true;
        public static float timeScale = 60f;     // sim seconds per real second (1 real s = 1 sim min)
        public static bool selfTest;
        public static bool configB;
        public static bool questPerformance;
        public static readonly string[] Credits = { "Jaineil R. — design, modelling, software", "Angel C. — research & requirements", "Farhan K. — physical model" };
        public static string ReportsDir
        {
            get
            {
                string d = System.IO.Path.Combine(Application.isEditor ? System.IO.Path.GetFullPath(Application.dataPath + "/..") : System.IO.Path.GetFullPath(Application.dataPath + "/.."), "Reports");
                try { System.IO.Directory.CreateDirectory(d); } catch { d = Application.persistentDataPath; }
                return d;
            }
        }
    }

    public enum Cond { Anxiety = 0, Depression = 1, Conflict = 2, Fatigue = 3, Autonomy = 4, Monotony = 5 }
    public enum Sensor { HRV = 0, GSR = 1, Sleep = 2, Voice = 3, FACS = 4, Motion = 5, Cortisol = 6, CO2 = 7, Task = 8 }
    public enum SensorHealth { OK, Degraded, Failed }

    public static class SensorSpec
    {
        public static readonly string[] Name = { "Heart-rate variability", "Galvanic skin response", "Sleep architecture", "Voice prosody", "Facial action coding", "Movement / proximity", "Cortisol patch", "CO2 / air quality", "Task performance (software)" };
        public static readonly string[] Where = { "Beacon + wristband", "Beacon (chest)", "Wristband (night)", "Shared-zone mic arrays, on-device", "Shared-zone cameras only", "UWB beacons + door sensors", "Skin patch, 24–48 h", "Every zone, redundant pairs", "Lab/command terminals" };
        public static readonly float[] MassKg = { 0.03f, 0.02f, 0.03f, 0.25f, 0.35f, 0.15f, 0.01f, 0.12f, 0f };
        public static readonly float[] PowerW = { 0.05f, 0.05f, 0.05f, 2.5f, 4.0f, 1.2f, 0.01f, 0.8f, 0f };
        public static readonly string[] Threshold = {
            "RMSSD drop >25% vs personal baseline for >3 min",
            "Skin-conductance peaks >3/min above baseline",
            "Sleep efficiency <80% or REM <15% for 2 nights",
            "Pitch variance −30% (flat) or +40% (tense); speech rate shift >20%",
            "AU4/AU15 (sad/tense) frequency 2σ above baseline",
            "Dyad co-location −50% over 24 h; time-in-pod +60%",
            "Morning cortisol >2σ for 3 days",
            ">600 ppm in occupied zone",
            "Error rate +20% or completion time +25%" };
        public static readonly string[] Failure = {
            "Strap loose → dropout; exercise → false stress",
            "Sweat/exercise saturation; habituates to chronic stress",
            "Not worn at night → no data; pod light leaks",
            "Ambient noise / mask → low SNR; crew turns mic privacy off",
            "Low light → no detection; occlusion",
            "UWB drift in metal modules; beacon battery flat",
            "Patch expires (24–48 h); resupply gap",
            "Sensor drift (needs calibration); single-point failure",
            "Task mix changes → false trend; software bug" };
        // weight matrix W[cond, sensor]
        public static readonly float[,] W = {
            //HRV  GSR  Sleep Voice FACS Motion Cort CO2  Task
            {0.30f,0.25f,0.10f,0.20f,0.10f,0.05f,0.00f,0.00f,0.00f}, // anxiety
            {0.05f,0.00f,0.25f,0.20f,0.15f,0.20f,0.15f,0.00f,0.00f}, // depression
            {0.10f,0.00f,0.00f,0.30f,0.20f,0.40f,0.00f,0.00f,0.00f}, // conflict
            {0.15f,0.00f,0.20f,0.10f,0.00f,0.05f,0.00f,0.20f,0.30f}, // fatigue
            {0.00f,0.00f,0.00f,0.20f,0.20f,0.30f,0.00f,0.00f,0.30f}, // autonomy
            {0.00f,0.00f,0.00f,0.20f,0.20f,0.25f,0.00f,0.00f,0.35f}  // monotony
        };
        public static readonly bool[] OptOutAllowed = { false, true, true, true, true, true, true, false, true }; // HRV (vital safety floor) & CO2 (environmental) always on
    }

    public static class CondSpec
    {
        public static readonly string[] Name = { "Anxiety", "Depression", "Interpersonal conflict", "Cognitive fatigue", "Autonomy deprivation", "Sensory monotony" };
        public static readonly float[] TauHours = { 0.1f, 24f, 6f, 2f, 12f, 24f };          // smoothing
        public static readonly float[] PersistHours = { 0.05f, 48f, 6f, 2f, 24f, 48f };     // duration before classification
        public static readonly float[] MonitorHours = { 0.333f, 72f, 24f, 4f, 48f, 72f };   // monitoring window (anxiety 20 min, depression 72 h)
        public static readonly Color[] Col = { new Color(0.95f,0.35f,0.35f), new Color(0.45f,0.35f,0.9f), new Color(0.9f,0.55f,0.1f), new Color(0.1f,0.65f,0.6f), new Color(0.2f,0.45f,0.85f), new Color(0.6f,0.3f,0.95f) };
        public const float T1 = 0.35f, T2 = 0.55f, T3 = 0.75f;
    }

    [Serializable]
    public class LogEntry
    {
        public float simHour; public int crew; public string kind; public string text; public int tier; public int cond = -1;
        public string Time => SimClock.Format(simHour);
    }

    public class CrewMember
    {
        public int id; public string name; public string role; public Color moodColor;
        // latent psychological/physiological state (0..1)
        public float stress = 0.2f, mood = 0.75f, fatigue = 0.2f, autonomy = 0.8f, novelty = 0.7f, sleepQ = 0.85f, cortisol = 0.3f;
        public float[] tension = new float[5];
        // derived
        public float[] score = new float[6], smooth = new float[6], aboveSince = new float[6];
        public int[] tier = new int[6];
        public float confidence = 1f;
        public string activity = "idle", zone = "";
        public bool exercising, onEVA, asleep, inPod;
        public float lastExerciseHour = -99, lastSocialHour, lastNovelHour;
        // consent / privacy (crew-controlled)
        public bool aiPodAccess; public bool shareHealthWithCmo;              // AI acts inside pod only on invitation
        public bool shareMoodPublic;          // beacon ring shows mood to others only if true
        public bool notifyEverySilentChange;  // Q3 recovery: crew can disable silent tier
        public bool[] optOut = new bool[9];
        public float pauseUntilHour = -1;     // psych-monitoring pause (max 72 h)
        public SensorHealth[] sensors = new SensorHealth[9];
        public float[] baseline = { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f };   // personal resting score per condition, updated after every resolved episode
        public float Thr(int k, float t) => t + Mathf.Clamp(baseline[k] - 0.2f, 0f, 0.05f);   // a person whose normal is higher gets a slightly higher bar (max +0.05)
        public List<float> silentAt = new List<float>();   // times of silent Tier-1 changes (agency budget: max 3 per 24 h)
        public float selfMood = -1, selfStress = -1, selfAt = -99;   // voluntary 2-question self-check (0 = good … 1 = bad); counts for 24 h
        public float[] fbWeight = { 1, 1, 1, 1, 1, 1, 1, 1, 1 };   // crew feedback "that reading is wrong" lowers a sensor's weight for THIS person only (min 0.4)
        public List<LogEntry> log = new List<LogEntry>();
        public int MaxTier { get { int m = 0; for (int i = 0; i < 6; i++) m = Mathf.Max(m, tier[i]); return m; } }
        public float Wellbeing => Mathf.Clamp01(0.4f * mood + 0.25f * (1 - stress) + 0.2f * sleepQ + 0.15f * (1 - fatigue));
        public bool SensorUsable(int s) => sensors[s] != SensorHealth.Failed && !(optOut[s] && SensorSpec.OptOutAllowed[s]);
    }

    public static class Util
    {
        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root) { var r = FindDeep(c, name); if (r) return r; }
            return null;
        }
        public static IEnumerable<Transform> All(Transform root)
        {
            yield return root;
            foreach (Transform c in root) foreach (var x in All(c)) yield return x;
        }
        public static Transform[] SceneRoots() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.transform).ToArray();
        public static Transform Find(string name)
        {
            foreach (var r in SceneRoots()) { var t = FindDeep(r, name); if (t) return t; }
            return null;
        }
        public static List<Transform> FindPrefix(string prefix)
        {
            var l = new List<Transform>();
            foreach (var r in SceneRoots()) foreach (var t in All(r)) if (t.name.StartsWith(prefix)) l.Add(t);
            return l;
        }
        /// Puts the eye at `h` metres above the rig's feet regardless of the XR rig's camera floor offset.
        public static void SetEyeHeight(Camera cam, Transform origin, float h)
        {
            if (!cam) return;
            float feet = origin ? origin.position.y : 0f;
            var t = cam.transform; var p = t.position; t.position = new Vector3(p.x, feet + h, p.z);
            var lp = t.localPosition; t.localPosition = new Vector3(0, lp.y, 0);
        }
        public static Color Cct(float kelvin)
        {
            float t = kelvin / 100f, r, g, b;
            if (t <= 66) { r = 255; g = 99.47f * Mathf.Log(t) - 161.12f; b = t <= 19 ? 0 : 138.52f * Mathf.Log(t - 10) - 305.04f; }
            else { r = 329.7f * Mathf.Pow(t - 60, -0.1332f); g = 288.12f * Mathf.Pow(t - 60, -0.0755f); b = 255; }
            return new Color(Mathf.Clamp(r, 0, 255) / 255f, Mathf.Clamp(g, 0, 255) / 255f, Mathf.Clamp(b, 0, 255) / 255f);
        }
    }
}
