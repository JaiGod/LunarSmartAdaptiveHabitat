// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/04_Habitat/LadderClimb.cs
//  Section : 04_Habitat
//  Folder  : The base itself: zones and their light/sound/temperature/air, dome status lights,
//            doors and hatches, ladders, loose props in 1/6 g, Moon environment, layout export.
//  Purpose : Real ladder climbing between the two decks (no more teleport). Desktop: walk up to the
//            ladder, press E → W / S (or ↑ ↓) climb, reaching the top/bottom steps you off.
//  Contains: LadderClimb
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDevices = UnityEngine.XR.InputDevices;

namespace Hunch3
{
    /// Real ladder climbing between the two decks (no more teleport).
    /// Desktop: walk up to the ladder, press E → W / S (or ↑ ↓) climb, reaching the top/bottom steps you off.
    /// VR: point at the ladder + trigger → either thumbstick up/down climbs (or hold grip on both hands and pull).
    /// The AI crew and the autopilot climb the same way.  1/6 g → climbing is easy: 0.8 m/s.
    public class LadderClimb : MonoBehaviour
    {
        public static LadderClimb I;
        public static bool Climbing => I && I.climbing;
        bool climbing, auto; int autoDir; float dismountT = -1; Vector3 dismountFrom, dismountTo;
        public Vector3 Bottom, Top, Stand;          // Stand = the xz where a climber's feet go on the ladder
        public float Speed = 0.8f;
        float rungClock; Vector3 prevLeft, prevRight; bool hadGrip, wasNear, trigPrev, testRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) Make(); };
            if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) Make();
        }
        static void Make() { if (!FindFirstObjectByType<LadderClimb>()) new GameObject("LadderClimb").AddComponent<LadderClimb>(); }
        void Awake() { I = this; Locate(); }

        public void Locate()
        {
            var b = Util.Find("SPAWN_Ladder_Bottom"); var t = Util.Find("SPAWN_Ladder_Top");
            Bottom = b ? b.position : Vector3.zero; Top = t ? t.position : Bottom + Vector3.up * 2.6f;
            // ladder = midpoint of the two rails; stand 0.3 m in front of the rungs, on the bottom-landing side
            var rails = Util.FindPrefix("W22_Ladder_Rail").Select(r => r.GetComponent<Renderer>()).Where(r => r).ToList();
            Vector3 lad = rails.Count > 0 ? rails.Aggregate(Vector3.zero, (a, r) => a + r.bounds.center) / rails.Count : Bottom;
            Vector3 off = new Vector3(Bottom.x - lad.x, 0, Bottom.z - lad.z);
            Stand = off.sqrMagnitude > 0.01f ? new Vector3(lad.x, 0, lad.z) + off.normalized * 0.3f : new Vector3(Bottom.x, 0, Bottom.z);
        }
        public Vector3 StandAt(float y) => new Vector3(Stand.x, y, Stand.z);

        /// Player starts climbing (from an E press / VR select on the ladder, or the autopilot with dir ±1).
        public void Begin(PlayerRig pr, int autoDirection = 0)
        {
            if (!pr || climbing) return;
            if (pr.suit == SuitState.EVA) { HUD.I?.Toast("No ladder in an EVA suit — doff first."); return; }
            climbing = true; auto = autoDirection != 0; autoDir = autoDirection; dismountT = -1;
            if (pr.cc) pr.cc.enabled = false;
            float y = Mathf.Clamp(pr.FeetPosition.y, Bottom.y, Top.y);
            bool atTop = y > (Bottom.y + Top.y) * 0.5f;
            if (auto && autoDir < 0 && !atTop) { climbing = false; if (pr.cc) pr.cc.enabled = true; return; }   // already down
            if (auto && autoDir > 0 && atTop) { climbing = false; if (pr.cc) pr.cc.enabled = true; return; }      // already up
            y = atTop ? Top.y - 0.02f : Bottom.y + 0.02f;          // hop onto the ladder just inside the end you are at
            pr.origin.transform.position = StandAt(y);
            // face the ladder
            var lad = new Vector3(Stand.x, 0, Stand.z) - new Vector3(Bottom.x, 0, Bottom.z);
            if (lad.sqrMagnitude > 0.001f && !pr.isXR) pr.origin.transform.rotation = Quaternion.LookRotation(lad.normalized);
            if (!auto) HUD.I?.Toast(pr.isXR ? "On the ladder — thumbstick up/down (or grip both hands and pull) to climb." : (atTop ? "On the ladder — hold S (or ↓) to climb DOWN, W to go back up." : "On the ladder — hold W (or ↑) to climb UP, S to go back down."));
        }

        void Update()
        {
            var pr = PlayerRig.I; if (!pr) return;
            if (!climbing)
            {
                // F8 = self-test: ride the ladder from the top deck all the way down (logs the result)
                if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame) { pr.Teleport(Top); testRun = true; Begin(pr, -1); return; }
                // you can always grab the ladder just by standing at it (top hole or bottom landing) and pressing E / trigger
                Vector3 f = pr.FeetPosition; float hd = new Vector2(f.x - Stand.x, f.z - Stand.z).magnitude;
                bool nearEnd = Mathf.Abs(f.y - Top.y) < 0.6f || Mathf.Abs(f.y - Bottom.y) < 0.6f;
                bool near = hd < 1.6f && nearEnd && pr.suit != SuitState.EVA && !SleepSystem.PlayerAsleep;
                if (near && !wasNear) HUD.I?.Toast(Mathf.Abs(f.y - Top.y) < 0.6f ? "Ladder down to the lower deck — press E (VR: trigger) to climb down." : "Ladder up to the wardroom deck — press E (VR: trigger) to climb up.");
                wasNear = near;
                if (near)
                {
                    var kb = Keyboard.current; bool press = kb != null && kb.eKey.wasPressedThisFrame;
                    if (pr.isXR) { var rh = InputDevices.GetDeviceAtXRNode(XRNode.RightHand); if (rh.isValid && rh.TryGetFeatureValue(CommonUsages.triggerButton, out bool tb) && tb && !trigPrev) press = true; trigPrev = rh.isValid && rh.TryGetFeatureValue(CommonUsages.triggerButton, out bool tb2) && tb2; }
                    if (press) Begin(pr);
                }
                return;
            }
            var o = pr.origin.transform;
            if (dismountT >= 0)
            {
                dismountT += Time.deltaTime / 0.5f;
                o.position = Vector3.Lerp(dismountFrom, dismountTo, Mathf.SmoothStep(0, 1, dismountT));
                if (dismountT >= 1) { climbing = false; dismountT = -1; pr.Teleport(dismountTo); }
                return;
            }
            float v = 0;
            if (auto) v = autoDir;
            else
            {
                var kb = Keyboard.current;
                if (kb != null) { if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1; if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1; }
                v += XRClimb(pr);
            }
            v = Mathf.Clamp(v, -1.5f, 1.5f);
            float y = o.position.y + v * Speed * Time.deltaTime;
            if (v != 0) { rungClock += Mathf.Abs(v) * Time.deltaTime; if (!pr.isXR) pr.cam.transform.localRotation = Quaternion.Euler(-10f + 3f * Mathf.Sin(rungClock * 7f), 0, 1.5f * Mathf.Sin(rungClock * 3.5f)); }
            // step off only when you keep climbing PAST an end (starting at the top no longer bounces you straight off)
            if (v > 0 && y >= Top.y) { Dismount(pr, StandAt(Top.y), Top); return; }
            if (v < 0 && y <= Bottom.y) { Dismount(pr, StandAt(Bottom.y), Bottom); return; }
            y = Mathf.Clamp(y, Bottom.y, Top.y);
            o.position = StandAt(y);
            if (!auto) HUD.I?.SetPrompt($"Ladder  ▲ {Mathf.InverseLerp(Bottom.y, Top.y, y) * 100:0}%  (W / S · stick)");
        }

        // VR: thumbstick Y on either hand, or "pull" — both grips held and the hands move down → you go up
        float XRClimb(PlayerRig pr)
        {
            if (!pr.isXR) return 0;
            float v = 0;
            var l = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand); var r = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (l.isValid && l.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 la) && Mathf.Abs(la.y) > 0.3f) v += la.y;
            if (r.isValid && r.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 ra) && Mathf.Abs(ra.y) > 0.3f) v += ra.y;
            bool gl = l.isValid && l.TryGetFeatureValue(CommonUsages.gripButton, out bool g1) && g1, gr = r.isValid && r.TryGetFeatureValue(CommonUsages.gripButton, out bool g2) && g2;
            l.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 lp); r.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 rp);
            if ((gl || gr) && hadGrip)
            {
                float dy = 0; int n = 0;
                if (gl) { dy += lp.y - prevLeft.y; n++; } if (gr) { dy += rp.y - prevRight.y; n++; }
                if (n > 0 && Time.deltaTime > 0) v += -(dy / n) / Time.deltaTime / Speed;     // pulling hands down = moving up
            }
            hadGrip = gl || gr; prevLeft = lp; prevRight = rp;
            return v;
        }

        void Dismount(PlayerRig pr, Vector3 from, Vector3 to)
        {
            dismountFrom = from; dismountTo = to; dismountT = 0;
            Debug.Log($"[HUNCH3] ladder: stepped off at the {(to == Top ? "TOP" : "BOTTOM")} (y {to.y:0.00}){(testRun ? " — F8 self-test PASSED" : "")}"); testRun = false;
            HUD.I?.SetPrompt("");
            if (!auto) HUD.I?.Toast(to == Top ? "Upper deck — wardroom, galley, sleep pods." : "Lower deck — hallways to every module.");
        }
    }
}
