using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRNode = UnityEngine.XR.XRNode;
using XRUsages = UnityEngine.XR.CommonUsages;

namespace Hunch3
{
    public enum SuitState { IVA, Donning, EVA, Doffing }

    public class PlayerRig : MonoBehaviour
    {
        public static PlayerRig I;
        public XROrigin origin; public Camera cam; public CharacterController cc;
        public bool isXR; public bool autopilot; public SuitState suit = SuitState.IVA;
        public float suitO2Hours = 8f; public string zoneName = "—"; public Zone zone;
        public Vector3 FeetPosition => origin ? origin.transform.position : transform.position;
        float yaw, pitch, vy; bool seated; Vector3 standPos;
        List<Vector3> apPath = new List<Vector3>(); int apI; string apSlot = ""; float apHop;
        bool prevY, prevX, prevB, prevA, prevStick;

        void Awake() { I = this; }
        void Start()
        {
            Physics.gravity = new Vector3(0, -1.62f, 0);           // lunar gravity
            origin = FindFirstObjectByType<XROrigin>();
            cam = Camera.main ? Camera.main : (origin ? origin.Camera : null);
            isXR = UnityEngine.XR.XRSettings.isDeviceActive && !GameState.selfTest;
            if (origin)
            {
                cc = origin.GetComponent<CharacterController>();
                if (!cc) { cc = origin.gameObject.AddComponent<CharacterController>(); cc.height = 1.75f; cc.radius = 0.25f; cc.center = new Vector3(0, 0.875f, 0); }
                if (!isXR)
                {
                    foreach (var b in cam.GetComponents<Behaviour>()) if (b.GetType().Name.Contains("TrackedPoseDriver")) b.enabled = false;
                    foreach (var b in origin.GetComponentsInChildren<Behaviour>(true))
                    { string n = b.GetType().FullName ?? ""; if (n.Contains("Locomotion") || n.Contains("Teleport") || n.Contains("Turn") || n.Contains("Gravity") || n.Contains("Jump")) b.enabled = false; }
                    Util.SetEyeHeight(cam, origin.transform, 1.65f); cam.transform.localRotation = Quaternion.identity;
                }
            }
            if (isXR && origin)
                foreach (var b in origin.GetComponentsInChildren<Behaviour>(true))
                {
                    string n = b.GetType().Name;
                    if (n.Contains("SnapTurnProvider")) b.enabled = GameState.snapTurn;
                    else if (n.Contains("ContinuousTurnProvider")) b.enabled = !GameState.snapTurn;
                    else if (n.Contains("ContinuousMoveProvider")) b.enabled = GameState.smoothMove;
                }
            var sp = Util.Find(GameState.mode == GameMode.Tour ? "WP_Up_E" : "SPAWN_Player");
            if (sp) Teleport(sp.position, 90f);
            if (Sim.I != null) Sim.I.engine.C(1).zone = "Communal_Wardroom";
        }

        int qtIdx = -1;
        public void Teleport(Vector3 feet, float yawDeg = float.NaN)
        {
            if (!origin) return;
            if (cc) cc.enabled = false;
            origin.transform.position = feet + Vector3.up * 0.02f;
            if (!float.IsNaN(yawDeg)) { yaw = yawDeg; origin.transform.rotation = Quaternion.Euler(0, yaw, 0); }
            if (cc) cc.enabled = true; vy = 0;
        }

        void Update()
        {
            if (!origin) return;
            HandleShortcuts();
            if (seated && isXR && XRStandInput()) StandUp();
            if (seated && autopilot) StandUp();
            if (DoorPassage.Walking || SleepSystem.PlayerAsleep || LadderClimb.Climbing) { } else if (autopilot) Autopilot(); else if (!isXR) DesktopMove();
            // zone tracking
            if (HabitatEnvironment.I)
            {
                var z = HabitatEnvironment.I.ZoneAt(FeetPosition);
                if (z != zone)
                {
                    zone = z; zoneName = z != null ? z.name.Replace("_", " ") : (FeetPosition.y < 0.6f ? "Lunar surface" : "—");
                    if (z != null && Sim.I != null) Sim.I.engine.C(1).zone = z.name;
                    if (z != null && (GameState.mode == GameMode.Tour || HUD.I && HUD.I.showZoneCards)) HUD.I?.ZoneCard(z);
                    if (z != null && z.type == "exercise") HabitatEnvironment.I.Apply("alert:1", 1, 3);
                }
            }
            if (suit == SuitState.EVA) { suitO2Hours -= Time.deltaTime * GameState.timeScale / 3600f; if (suitO2Hours < 1f) HUD.I?.Toast("Suit O2 below 1 h — return to a suitport now."); }
        }

        bool Key(Key k) => Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame;
        bool Btn(XRNode node, UnityEngine.XR.InputFeatureUsage<bool> u) { var d = XRInputDevices.GetDeviceAtXRNode(node); return d.isValid && d.TryGetFeatureValue(u, out bool v) && v; }

        void HandleShortcuts()
        {
            bool y = Btn(XRNode.LeftHand, XRUsages.secondaryButton), x = Btn(XRNode.LeftHand, XRUsages.primaryButton);
            bool b = Btn(XRNode.RightHand, XRUsages.secondaryButton), a = Btn(XRNode.RightHand, XRUsages.primaryButton);
            bool st = Btn(XRNode.LeftHand, XRUsages.primary2DAxisClick);
            if (Key(UnityEngine.InputSystem.Key.Tab) || (y && !prevY)) Panels.Toggle("crew");
            if (Key(UnityEngine.InputSystem.Key.M) || (x && !prevX)) HUD.I?.ToggleBigMap();
            if (Key(UnityEngine.InputSystem.Key.P) || (b && !prevB)) { autopilot = !autopilot; apSlot = ""; HUD.I?.Toast(autopilot ? "AUTOPILOT ON — the AI is playing as you (P to take back control)" : "Autopilot off — you're in control"); }
            if (Key(UnityEngine.InputSystem.Key.Y) || (a && !prevA)) HUD.I?.AnswerOffer(true);
            if (Key(UnityEngine.InputSystem.Key.R)) HUD.I?.AnswerOffer(false);
            if (Key(UnityEngine.InputSystem.Key.H)) HUD.I?.ToggleHelp();
            if (Key(UnityEngine.InputSystem.Key.K)) HUD.I?.ToggleKeys();
            if (Key(UnityEngine.InputSystem.Key.Q) || (st && !prevStick)) Panels.Toggle("menu");
            if (Key(UnityEngine.InputSystem.Key.N)) Panels.Toggle("nav");
            if (Key(UnityEngine.InputSystem.Key.F1)) Panels.Toggle("dashboard");
            if (Key(UnityEngine.InputSystem.Key.F2)) Panels.Toggle("cmo");
            if (Key(UnityEngine.InputSystem.Key.F3)) Panels.Toggle("meeting");
            if (Key(UnityEngine.InputSystem.Key.F4)) Panels.Toggle("tabletop");
            if (Key(UnityEngine.InputSystem.Key.F5)) Panels.Toggle("scenarios");
            if (Key(UnityEngine.InputSystem.Key.C)) HabitatEnvironment.I?.SetConfigB(!GameState.configB);
            if (Key(UnityEngine.InputSystem.Key.T)) { GameState.timeScale = GameState.timeScale >= 600 ? 1 : GameState.timeScale >= 60 ? 600 : 60; HUD.I?.Toast($"Time speed: 1 s = {GameState.timeScale / 60f:0.#} sim min"); }
            if (Key(UnityEngine.InputSystem.Key.Z)) HUD.I?.ToggleZoneCards();
            if (Key(UnityEngine.InputSystem.Key.F7)) QuickTravel();
            if (Key(UnityEngine.InputSystem.Key.F12)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GameState.ReportsDir, $"shot_{DateTime.Now:HHmmss}.png"));
            if (Key(UnityEngine.InputSystem.Key.Escape)) { if (Panels.AnyOpen) Panels.CloseAll(); else UnityEngine.SceneManagement.SceneManager.LoadScene(0); }
            prevY = y; prevX = x; prevB = b; prevA = a; prevStick = st;
        }

        /// quick-travel (desktop F7 / VR menu): cycles ladder top → quarters vestibule → quarters 1-4
        public void QuickTravel()
        {
            {
                string[] q = { "SPAWN_Ladder_Top", "WP_K_C", "WP_SQ1_In", "WP_SQ2_In", "WP_SQ3_In", "WP_SQ4_In" };
                for (int k = 0; k < q.Length; k++)
                {
                    qtIdx = (qtIdx + 1) % q.Length;
                    Transform t = Nav.wp.ContainsKey(q[qtIdx]) ? Nav.wp[q[qtIdx]] : Util.Find(q[qtIdx]);
                    if (t) { Teleport(t.position); HUD.I?.Toast("Quick-travel: " + q[qtIdx]); break; }
                }
            }
        }

        void DesktopMove()
        {
            var kb = Keyboard.current; var ms = Mouse.current; if (kb == null) return;
            if (ms != null && (ms.rightButton.isPressed || Cursor.lockState == CursorLockMode.Locked))
            {
                var dlt = ms.delta.ReadValue() * 0.12f; yaw += dlt.x; pitch = Mathf.Clamp(pitch - dlt.y, -80, 80);
            }
            if (kb.lKey.wasPressedThisFrame) Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            origin.transform.rotation = Quaternion.Euler(0, yaw, 0); cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Vector3 mv = Vector3.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) mv += Vector3.forward; if (kb.sKey.isPressed || kb.downArrowKey.isPressed) mv += Vector3.back;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mv += Vector3.left; if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mv += Vector3.right;
            if (seated && (mv != Vector3.zero || kb.spaceKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)) { StandUp(); return; }
            float spd = kb.leftShiftKey.isPressed ? 3.2f : 1.6f;
            Vector3 world = origin.transform.rotation * mv.normalized * spd;
            if (cc && cc.enabled)
            {
                if (cc.isGrounded) { vy = -0.5f; if (kb.spaceKey.wasPressedThisFrame) vy = suit == SuitState.EVA ? 2.2f : 1.1f; }   // 1/6 g: big hops outside, only a small hop under a 2.3 m ceiling
                vy += Physics.gravity.y * Time.deltaTime;
                { DoorPassage.EnforceHeight(cc); } cc.Move((world + Vector3.up * vy) * Time.deltaTime);
            }
            else origin.transform.position += world * Time.deltaTime;
        }

        public bool Seated => seated;
        public void Sit(Vector3 seat)
        {
            standPos = FeetPosition; Teleport(seat - Vector3.up * 0.0f); seated = true;
            if (cc) cc.enabled = false;                                  // don't let the seat's collider shove you around while seated
            if (!isXR) Util.SetEyeHeight(cam, origin.transform, 1.2f);
            HUD.I?.Toast(isXR ? "Seated. Push either thumbstick to stand up." : "Seated. Press SPACE, X or any move key (W A S D) to stand up.");
        }
        /// back on your feet where you were standing before you sat down (never stuck inside the chair)
        public void StandUp()
        {
            if (!seated) return;
            seated = false; Teleport(standPos);
            if (!isXR) Util.SetEyeHeight(cam, origin.transform, 1.65f);
            HUD.I?.Toast("Standing.");
        }
        bool XRStandInput()
        {
            foreach (var node in new[] { UnityEngine.XR.XRNode.LeftHand, UnityEngine.XR.XRNode.RightHand })
            {
                var d = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node); if (!d.isValid) continue;
                if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 a) && a.magnitude > 0.6f) return true;
            }
            return false;
        }

        // ---- autopilot: the AI plays as the Commander using the same schedule as the crew ----
        void Autopilot()
        {
            if (Sim.I == null) return;
            var me = Sim.I.engine.C(1); float hod = SimClock.HourOfDay;
            var (g, t) = CrewAgent.Schedule(1, me, hod, SimClock.Day);
            if (GameState.mode == GameMode.Tour) { g = TourGoal(); t = "guided tour"; }
            else if (HUD.I && !string.IsNullOrEmpty(HUD.I.NavGoal)) { g = HUD.I.NavGoal; t = HUD.I.NavLabel; }   // auto-walk to the chosen destination
            if (g + t != apSlot) { apSlot = g + t; apPath = Nav.Path(FeetPosition, g); apI = 0; me.activity = t + " (autopilot)"; if (HUD.I && HUD.I.NavGoal != g) HUD.I.SetNavTarget(g, t); }
            if (apI >= apPath.Count) { if (GameState.mode == GameMode.Tour) NextTourStop(); return; }
            Vector3 target = apPath[apI]; Vector3 d = target - FeetPosition; float flat = new Vector2(d.x, d.z).magnitude;
            if (Nav.IsLadder(FeetPosition, target) && !isXR && LadderClimb.I) { LadderClimb.I.Begin(this, target.y > FeetPosition.y ? 1 : -1); return; }   // really climbs
            if (Nav.IsLadder(FeetPosition, target) || isXR)
            {
                apHop += Time.deltaTime;
                if (apHop > (isXR ? 1.2f : 0f) || Nav.IsLadder(FeetPosition, target)) { apHop = 0; Teleport(target, Quaternion.LookRotation(new Vector3(d.x, 0, d.z).sqrMagnitude > 1e-3f ? new Vector3(d.x, 0, d.z) : origin.transform.forward).eulerAngles.y); apI++; }
                return;
            }
            if (flat < 0.15f) { apI++; return; }
            Vector3 dir = new Vector3(d.x, 0, d.z).normalized;
            yaw = Mathf.MoveTowardsAngle(yaw, Quaternion.LookRotation(dir).eulerAngles.y, 180f * Time.deltaTime);
            origin.transform.rotation = Quaternion.Euler(0, yaw, 0); pitch = Mathf.Lerp(pitch, 5f, Time.deltaTime); cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Vector3 step = dir * Mathf.Min(1.8f * Time.deltaTime, flat);
            if (cc && cc.enabled) { DoorPassage.EnforceHeight(cc); cc.Move(step + Vector3.down * 0.5f * Time.deltaTime); } else origin.transform.position += step;
            if ((FeetPosition - target).sqrMagnitude > 9f && apI > 0 && (FeetPosition - apPath[apI - 1]).sqrMagnitude > 9f) Teleport(target);   // unstuck (lost)
            // blocked by furniture (e.g. a chair standing on the waypoint): if we stop making progress, step past it
            if ((FeetPosition - apLastPos).sqrMagnitude > 0.0025f) { apLastPos = FeetPosition; apStuckT = 0; }
            else if ((apStuckT += Time.deltaTime) > 1.0f) { apStuckT = 0; Teleport(target + (target - FeetPosition).normalized * 0.0f); apI++; }
        }
        static readonly string[] Tour = { "WP_Up_Table", "WP_Pod1_Door", "WP_Up_Lounge", "WP_K_C", "WP_SQ1_In", "WP_SQ1_Health", "WP_Hyg_In", "WP_B_Lab", "WP_N_C", "WP_R_C", "WP_E_C", "WP_C_Mid", "WP_G_C", "WP_H_C", "WP_D_Obs", "WP_D_VR", "WP_D_Ex", "WP_A_Suit" };
        int tourI;
        string TourGoal() => Tour[tourI % Tour.Length];
        void NextTourStop() { tourHold += Time.deltaTime; if (tourHold > 9f) { tourHold = 0; tourI++; apSlot = ""; } }
        float tourHold; Vector3 apLastPos; float apStuckT;

        // ---- EVA: suitport procedure ----
        public void BeginDon(int suitNo)
        {
            if (suit != SuitState.IVA) return;
            StartCoroutine(DonRoutine(suitNo));
        }
        System.Collections.IEnumerator DonRoutine(int n)
        {
            suit = SuitState.Donning;
            string[] steps = {
                "1/6  Suit #"+n+" status: GREEN (PLSS charged, O2 8 h, CO2 scrubber OK)",
                "2/6  Inner suitport hatch opening… back into the suit through the PLSS hatch",
                "3/6  Arms & legs in — helmet locked, Wellness & Comm Beacon paired to suit comms",
                "4/6  Suitport hatch closing & sealing… suit pressurising to 29.6 kPa",
                "5/6  Leak check: 2-minute hold… ΔP < 0.2 kPa — PASS",
                "6/6  Releasing suitport latches — step down onto the platform. Dust stays outside!" };
            foreach (var s in steps) { HUD.I?.Procedure(s); yield return new WaitForSeconds(GameState.selfTest ? 0.2f : 1.6f); }
            var root = Util.Find($"CREW{n}_EVA_Suit_Root");
            Vector3 p = root ? root.position : FeetPosition;
            if (root) foreach (var r in root.GetComponentsInChildren<Renderer>()) r.enabled = false;   // you are wearing it
            var outSpawn = Util.Find("SPAWN_EVA_Outside");
            Vector3 away = root ? (root.position - new Vector3(-9f, p.y, 0)).normalized : Vector3.forward;
            Teleport((outSpawn ? outSpawn.position : p) , 90f);
            suit = SuitState.EVA; suitO2Hours = 8f; donnedSuit = n;
            if (Sim.I != null) { var me = Sim.I.engine.C(1); me.onEVA = true; Sim.I.engine.Log(1, "eva", "Commander egressed via suitport " + n + " (dust-free entry design)."); }
            HUD.I?.Procedure("EVA ACTIVE — 1/6 g. Suit O2 8 h. Return to suitport " + n + " platform and press E (or trigger) to dock.");
        }
        public int donnedSuit = 1;
        public void BeginDoff()
        {
            if (suit != SuitState.EVA) return; StartCoroutine(DoffRoutine());
        }
        System.Collections.IEnumerator DoffRoutine()
        {
            suit = SuitState.Doffing;
            string[] steps = { "1/4  Back onto the suitport — latches engaged", "2/4  Equalising pressure between suit and habitat…", "3/4  Hatch open — climbing out forwards into the suit room", "4/4  Suit stays docked OUTSIDE. Lunar dust: 0 g brought inside ✓" };
            foreach (var s in steps) { HUD.I?.Procedure(s); yield return new WaitForSeconds(GameState.selfTest ? 0.2f : 1.4f); }
            var root = Util.Find($"CREW{donnedSuit}_EVA_Suit_Root"); if (root) foreach (var r in root.GetComponentsInChildren<Renderer>()) r.enabled = true;
            var w = Util.Find("WP_A_Suit"); if (w) Teleport(w.position, 90f);
            suit = SuitState.IVA; if (Sim.I != null) { var me = Sim.I.engine.C(1); me.onEVA = false; me.novelty += 0.3f; me.lastNovelHour = SimClock.hours; }
            HUD.I?.Procedure("");
        }
    }
}
