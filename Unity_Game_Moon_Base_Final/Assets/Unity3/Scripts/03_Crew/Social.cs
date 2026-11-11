// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/03_Crew/Social.cs
//  Section : 03_Crew
//  Folder  : The 4-person crew (you + 3 AI crewmates): schedules, jobs, waving, sleep pods with
//            mood lights, and the robotic health station.
//  Purpose : WAVING — small social moments that also feed the AI's interpersonal-conflict and
//            depression signals. • An AI crewmate who sees you within ~4 m waves and says hello
//            (once a minute at most).
//  Why     : Chat: 'team meetings should exist' + social cues feeding the AI.
//  Contains: Social, Waver
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDevices = UnityEngine.XR.InputDevices;

namespace Hunch3
{
    /// WAVING — small social moments that also feed the AI's interpersonal-conflict and depression signals.
    ///  • An AI crewmate who sees you within ~4 m waves and says hello (once a minute at most).
    ///    Low mood → a small wave and no words (social-withdrawal cue the movement/FACS sensors log).
    ///  • Two crewmates passing each other wave — unless their dyad tension is high (they avoid each other: conflict cue).
    ///  • You wave: G on desktop, or raise a controller above your head and wave it in VR. The nearest crewmate waves back.
    ///    Every wave exchanged counts as social contact (lastSocialHour, small mood lift).
    public class Social : MonoBehaviour
    {
        public static Social I;
        class Waver { public List<Transform> parts = new List<Transform>(); public List<Vector3> plp = new List<Vector3>(); public List<Quaternion> plr = new List<Quaternion>(); public CrewAgent a; public Transform arm; public Vector3 pivot; public Vector3 axis; public float sign = 1; public Vector3 lp; public Quaternion lr; public float until, lastWave = -999; public GameObject proc; }
        readonly Dictionary<CrewAgent, Waver> wavers = new Dictionary<CrewAgent, Waver>();
        readonly Dictionary<string, float> pairCooldown = new Dictionary<string, float>();
        float playerWaveUntil, lastPlayerWave = -99; Queue<float> handX = new Queue<float>();
        static readonly string[] HelloHi = { "Morning, Commander!", "Hey! Good to see you.", "Hi Commander — all good?", "Hey, how's it going?" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) Make(); };
            if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) Make();
        }
        static void Make() { if (!FindFirstObjectByType<Social>()) new GameObject("Social").AddComponent<Social>(); }
        void Awake() { I = this; }
        void Start() { Invoke(nameof(Setup), 1.0f); }

        void Setup()
        {
            var sb = new StringBuilder("CREW PARTS (for wave rig)\n");
            foreach (var a in CrewAgent.All)
            {
                var w = new Waver { a = a };
                var parts = a.GetComponentsInChildren<Renderer>(true).Where(r => r.transform != a.transform).ToList();
                sb.AppendLine($"{a.name}: " + string.Join(", ", parts.Select(p => p.name).Take(60)));
                var arms = parts.Where(r => { string n = r.name.ToLower(); return (n.Contains("arm") || n.Contains("sleeve")) && !n.Contains("glove"); }).ToList();
                var pick = arms.OrderByDescending(r => r.name.Contains("_R") || r.name.ToLower().Contains("right") ? 1 : 0).ThenByDescending(r => r.bounds.size.y).FirstOrDefault();
                if (pick)
                {
                    w.arm = pick.transform; w.lp = pick.transform.localPosition; w.lr = pick.transform.localRotation;
                    // the WHOLE arm waves as one piece: upper arm + forearm + hand/glove + anything strapped to it (patch, wristband)
                    string side = pick.name.Contains("_L") ? "_L" : "_R";
                    var armB = pick.bounds;
                    foreach (var r in parts)
                    {
                        string n = r.name;
                        bool sameLimb = (n.Contains("Arm" + side) || n.Contains("Hand" + side) || n.Contains("Glove" + side) || n.Contains("Sleeve" + side) || n.Contains("Forearm" + side));
                        if (!sameLimb)
                        {   // small accessories sitting on that arm (cortisol patch, wristband, shoulder patch)
                            var lower = parts.FirstOrDefault(q => q.name.Contains("Arm" + side + "_Lower") || q.name.Contains("Forearm" + side));
                            bool onUpper = r.bounds.size.magnitude < 0.25f && armB.SqrDistance(r.bounds.center) < 0.0009f;
                            bool onLower = lower && r.bounds.size.magnitude < 0.25f && lower.bounds.SqrDistance(r.bounds.center) < 0.0009f;
                            sameLimb = onUpper || onLower;
                        }
                        if (sameLimb) { w.parts.Add(r.transform); w.plp.Add(r.transform.localPosition); w.plr.Add(r.transform.localRotation); }
                    }
                    if (!w.parts.Contains(w.arm)) { w.parts.Add(w.arm); w.plp.Add(w.lp); w.plr.Add(w.lr); }
                    // if a part is parented under another part it already follows it — rotate only the top-most ones
                    for (int i = w.parts.Count - 1; i >= 0; i--)
                    {
                        var par = w.parts[i].parent; bool nested = false;
                        while (par && par != a.transform) { if (w.parts.Contains(par)) { nested = true; break; } par = par.parent; }
                        if (nested) { w.parts.RemoveAt(i); w.plp.RemoveAt(i); w.plr.RemoveAt(i); }
                    }
                    sb.AppendLine($"   wave limb parts → {string.Join(", ", w.parts.Select(t => t.name))}");
                }
                wavers[a] = w;
                sb.AppendLine($"   wave arm → {(pick ? pick.name : "procedural")}");
            }
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "Unity3", "crew_parts.txt"), sb.ToString()); } catch { }
        }

        bool CanWave(CrewAgent a)
        {
            if (!a || Sim.I == null) return false;
            var c = Sim.I.engine.C(a.crewId);
            return !c.asleep && !c.inPod && !c.onEVA && !a.climbing && !(Hazards.I && Hazards.I.Active);
        }

        public void Wave(CrewAgent a, float seconds = 2.2f)
        {
            if (!wavers.TryGetValue(a, out var w)) return;
            if (Time.time - w.lastWave < 3f) return;
            w.lastWave = Time.time; w.until = Time.time + seconds;
        }

        void Update()
        {
            if (Sim.I == null) return;
            var pr = PlayerRig.I; var e = Sim.I.engine;
            // ---- player waves (desktop G, or VR hand-above-head shake) ----
            bool playerWave = Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame || XRWaveGesture(pr);
            if (playerWave && Time.time - lastPlayerWave > 1.5f && pr)
            {
                lastPlayerWave = Time.time;
                var best = CrewAgent.All.Where(CanWave).OrderBy(a => (a.transform.position - pr.FeetPosition).sqrMagnitude).FirstOrDefault();
                if (best && (best.transform.position - pr.FeetPosition).magnitude < 8f && Mathf.Abs(best.transform.position.y - pr.FeetPosition.y) < 1.6f)
                {
                    var c = e.C(best.crewId); Wave(best, 2.5f);
                    c.lastSocialHour = SimClock.hours; c.mood = Mathf.Min(1, c.mood + 0.01f); e.C(1).lastSocialHour = SimClock.hours; e.C(1).mood = Mathf.Min(1, e.C(1).mood + 0.01f);
                    HUD.I?.Toast($"👋 You wave at {c.name}. {(c.mood < 0.45f ? "(a small wave back — they seem quiet today)" : c.name.Split(' ').Last() + " waves back: \"" + HelloHi[(best.crewId + SimClock.Day) % HelloHi.Length] + "\"")}");
                    e.Log(best.crewId, "social", $"Wave exchanged with Commander (social contact; movement sensor logs reciprocity).");
                }
                else HUD.I?.Toast("👋 You wave — nobody is close enough to see it.");
            }
            // ---- crew greet the player ----
            if (pr)
                foreach (var a in CrewAgent.All)
                {
                    if (!CanWave(a) || !wavers.TryGetValue(a, out var w)) continue;
                    Vector3 d = pr.FeetPosition - a.transform.position; if (Mathf.Abs(d.y) > 1.6f) continue; d.y = 0;
                    if (d.magnitude > 4f || Time.time - w.lastWave < 60f) continue;
                    Vector3 eye = a.transform.position + Vector3.up * 1.5f;
                    if (Physics.Linecast(eye, pr.cam.transform.position, out var hit, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() && !hit.collider.GetComponentInParent<CrewAgent>()) continue;   // can't see you
                    var c = e.C(a.crewId);
                    Wave(a, c.mood < 0.45f ? 1.0f : 2.2f);
                    if (c.mood >= 0.45f) HUD.I?.Toast($"👋 {c.name}: \"{HelloHi[(a.crewId + SimClock.Day + (int)Time.time / 60) % HelloHi.Length]}\"  (G / raise your hand to wave back)");
                    c.lastSocialHour = SimClock.hours;
                }
            // ---- crew greet each other (unless they're in conflict) ----
            var list = CrewAgent.All;
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                {
                    var A = list[i]; var B = list[j]; if (!CanWave(A) || !CanWave(B)) continue;
                    if ((A.transform.position - B.transform.position).sqrMagnitude > 2.5f * 2.5f) continue;
                    string key = A.crewId + "-" + B.crewId; if (pairCooldown.TryGetValue(key, out float t0) && Time.time - t0 < 90f) continue;
                    pairCooldown[key] = Time.time;
                    float tension = e.C(A.crewId).tension[B.crewId];
                    if (tension > 0.5f) { e.Log(A.crewId, "social", $"{e.C(A.crewId).name} and {e.C(B.crewId).name} passed without greeting (dyad tension {tension:0.00}) — proximity-avoidance cue for the conflict model."); continue; }
                    Wave(A); Wave(B);
                }
            // ---- animate ----
            foreach (var w in wavers.Values) Animate(w);
        }

        void Animate(Waver w)
        {
            if (!w.a) return;
            bool on = Time.time < w.until;
            float k = on ? Mathf.Clamp01((w.until - Time.time) / 0.3f) * Mathf.Clamp01((Time.time - (w.until - 2.5f)) / 0.3f) : 0f;
            if (w.arm)
            {
                for (int i = 0; i < w.parts.Count; i++) if (w.parts[i]) { w.parts[i].localPosition = w.plp[i]; w.parts[i].localRotation = w.plr[i]; }   // rest pose
                if (k <= 0) return;
                var b = w.arm.GetComponent<Renderer>().bounds;
                Vector3 shoulder = new Vector3(b.center.x, b.max.y - 0.04f, b.center.z);
                Vector3 side = b.center - new Vector3(w.a.transform.position.x, b.center.y, w.a.transform.position.z); side.y = 0;
                if (side.sqrMagnitude < 1e-4f) side = Vector3.right; side.Normalize();
                Vector3 fwd = Vector3.Cross(side, Vector3.up);                                     // body's forward/back axis
                // pick the rotation sense ONCE per frame from geometry (no per-frame flip-flopping = no shaking)
                float sgn = Vector3.Dot(Quaternion.AngleAxis(90f, fwd) * Vector3.down, side) > 0 ? 1f : -1f;
                float ang = sgn * (150f * k + 18f * k * Mathf.Sin(Time.time * Mathf.PI * 2 * 1.6f));   // raise the arm out to the side, then a gentle wave
                foreach (var t in w.parts) if (t) t.RotateAround(shoulder, fwd, ang);
            }
            else
            {
                if (!w.proc)
                {
                    w.proc = GameObject.CreatePrimitive(PrimitiveType.Capsule); Destroy(w.proc.GetComponent<Collider>()); w.proc.name = "WaveHand";
                    w.proc.transform.SetParent(w.a.transform, false); w.proc.transform.localScale = new Vector3(0.09f, 0.3f, 0.09f);
                    var src = w.a.GetComponentInChildren<Renderer>(); if (src) w.proc.GetComponent<Renderer>().sharedMaterial = src.sharedMaterial;
                }
                w.proc.SetActive(k > 0);
                if (k > 0) { w.proc.transform.localPosition = new Vector3(0.28f, 1.75f, 0); w.proc.transform.localRotation = Quaternion.Euler(0, 0, -15f + 25f * Mathf.Sin(Time.time * Mathf.PI * 2 * 2.2f)); }
            }
        }

        // VR: a controller held above the head and moved side to side ≥ 2 times in ~1 s
        bool XRWaveGesture(PlayerRig pr)
        {
            if (!pr || !pr.isXR) return false;
            foreach (var node in new[] { XRNode.RightHand, XRNode.LeftHand })
            {
                var d = InputDevices.GetDeviceAtXRNode(node);
                if (!d.isValid || !d.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p)) continue;
                var head = InputDevices.GetDeviceAtXRNode(XRNode.Head); head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 hp);
                if (p.y < hp.y + 0.05f) continue;
                handX.Enqueue(p.x); while (handX.Count > 72) handX.Dequeue();
                var arr = handX.ToArray(); int flips = 0; float dir = 0;
                for (int i = 1; i < arr.Length; i++) { float dd = arr[i] - arr[i - 1]; if (Mathf.Abs(dd) < 0.004f) continue; float s = Mathf.Sign(dd); if (dir != 0 && s != dir) flips++; dir = s; }
                if (flips >= 3) { handX.Clear(); return true; }
                return false;
            }
            handX.Clear(); return false;
        }
    }
}
