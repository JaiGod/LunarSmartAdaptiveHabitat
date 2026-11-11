// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/03_Player_Controls/HunchDesktopFallback.cs
//  Section : 03_Player_Controls
//  Folder  : How you move and find your way: VR controllers, keyboard/mouse, phone touch controls,
//            doorway help, NavMesh paths, green guide arrows + minimap.
//  Purpose : When no VR headset is running, lets the player explore with keyboard and mouse
//            instead.
//  Contains: HunchDesktopFallback
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

/// When no VR headset is running, lets the player explore with keyboard and mouse instead.
public class HunchDesktopFallback : MonoBehaviour
{
    public XROrigin origin;
    public float walkSpeed = 5f, runSpeed = 11f, flySpeed = 30f, mouseSensitivity = 0.12f, jumpSpeed = 3.0f, eyeHeight = 1.4f;
    public Vector3 MoveIntent { get; private set; }
    public bool active;
    // touch controls (HunchTouch): a stick, a look drag and a jump button stand in for the keyboard and mouse
    [System.NonSerialized] public bool padOn, padJump; [System.NonSerialized] public Vector2 padMove, padLook;

    HabitatGame tour;
    CharacterController cc;
    float yaw, pitch, vy;
    GUIStyle style, shadow;

    IEnumerator Start()
    {
        tour = GetComponent<HabitatGame>();
        if (!origin) origin = FindFirstObjectByType<XROrigin>();
        yield return new WaitForSeconds(0.25f);
        if (FlatTest())      // Editor tests on the flat screen: let go of the headset so a test run never takes over what the player sees in VR
        {
            try { var s = UnityEngine.XR.Management.XRGeneralSettings.Instance; if (s != null && s.Manager != null && s.Manager.activeLoader != null) { s.Manager.StopSubsystems(); s.Manager.DeinitializeLoader(); } } catch { }
            yield return null;
        }
        bool hmd = HeadsetRunning();
        // A headset that is connected can take a few seconds to start showing. If the VR system loaded, wait for it
        // instead of dropping to keyboard-and-mouse mode with the headset half working.
        if (!hmd && VrLoaded() && !FlatTest())
        {
            float until = Time.realtimeSinceStartup + 6f;
            while (!hmd && Time.realtimeSinceStartup < until) { yield return null; hmd = HeadsetRunning(); }
        }
        if (tour) tour.HeadsetActive = hmd;
        if (!hmd) EnableDesktop();
    }

    /// true while HunchBridge/flat.txt says "on" (Editor only)
    static bool FlatTest()
    {
        if (!Application.isEditor) return false;
        try { string p = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "HunchBridge", "flat.txt"); return System.IO.File.Exists(p) && System.IO.File.ReadAllText(p).Trim().StartsWith("on"); }
        catch { return false; }
    }

    static bool VrLoaded()
    {
        try { var s = UnityEngine.XR.Management.XRGeneralSettings.Instance; return s != null && s.Manager != null && s.Manager.activeLoader != null; }
        catch { return false; }
    }

    public static bool HeadsetRunning()
    {
        var list = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(list);
        foreach (var d in list) if (d != null && d.running) return true;
        return false;
    }

    void EnableDesktop()
    {
        active = true;
        if (!origin) return;
        var cam = origin.Camera;
        foreach (var mb in cam.GetComponents<MonoBehaviour>())
            if (mb && mb.GetType().Name == "TrackedPoseDriver") mb.enabled = false;
        foreach (Transform t in origin.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n == "Locomotion" || n == "Left Controller" || n == "Right Controller" || n.StartsWith("Affordance Callouts") || n == "TunnelingVignette")
                t.gameObject.SetActive(false);
        }
        cc = origin.GetComponent<CharacterController>();
        if (!cc) cc = origin.gameObject.AddComponent<CharacterController>();
        cc.height = 1.5f; cc.radius = 0.18f; cc.center = new Vector3(0f, 0.77f, 0f); cc.stepOffset = 0.36f; cc.slopeLimit = 60f; cc.skinWidth = 0.02f; cc.minMoveDistance = 0f;   // small enough for every doorway
        cc.enabled = true;
        cam.fieldOfView = 70f;
    }

    public void SetLook(float newYaw, float newPitch) { yaw = newYaw; pitch = newPitch; }
    /// throw the player upward (bounce pads); gravity brings them back
    public void Launch(float upSpeed) { vy = upSpeed; if (cc && cc.enabled && cc.gameObject.activeInHierarchy) cc.Move(Vector3.up * 0.06f); }

    void Update()
    {
        if (!active || !origin) return;
        if (tour && tour.menuOpen) { MoveIntent = Vector3.zero; return; }       // the menu has the mouse and keyboard
        if (tour && tour.autoBusy)                                              // the autopilot is walking and looking: only gravity is left to do here
        {
            MoveIntent = Vector3.zero;
            if (!tour.flyMode && cc && cc.enabled && cc.gameObject.activeInHierarchy) { if (cc.isGrounded) vy = -0.5f; else vy += Physics.gravity.y * Time.deltaTime; cc.Move(Vector3.up * vy * Time.deltaTime); }
            return;
        }
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb == null && !padOn) return;
        bool kbd = kb != null;
        if (padOn) { yaw += padLook.x * mouseSensitivity; pitch = Mathf.Clamp(pitch - padLook.y * mouseSensitivity, -85f, 85f); padLook = Vector2.zero; }

        if (mouse != null && kbd && !padOn)
        {
            if (mouse.leftButton.wasPressedThisFrame && !(tour && tour.uiHasMouse))
            {
                if (Cursor.lockState == CursorLockMode.Locked) { if (tour && !(tour.toolInUse != null && tour.toolInUse())) tour.DoInteract(); }      // click = act, same as E (unless a tool is in the hand: then the click uses the tool)
                else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            }
            if (kb.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Cursor.lockState == CursorLockMode.Locked || mouse.rightButton.isPressed)
            {
                var d = mouse.delta.ReadValue();
                yaw += d.x * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - d.y * mouseSensitivity, -85f, 85f);
            }
        }

        bool fly = tour && tour.flyMode;
        bool shift = kbd && kb.leftShiftKey.isPressed;
        float speed = fly ? flySpeed : (shift || (padOn && padMove.magnitude > 0.95f) ? runSpeed : walkSpeed);
        if (fly && shift) speed *= 3f;
        Vector3 input = Vector3.zero;
        if (kbd && (kb.wKey.isPressed || kb.upArrowKey.isPressed)) input.z += 1f;
        if (kbd && (kb.sKey.isPressed || kb.downArrowKey.isPressed)) input.z -= 1f;
        if (kbd && kb.dKey.isPressed) input.x += 1f;
        if (kbd && kb.aKey.isPressed) input.x -= 1f;
        if (padOn) { input.x += padMove.x; input.z += padMove.y; }
        input = Vector3.ClampMagnitude(input, 1f);

        MoveIntent = Quaternion.Euler(0f, yaw, 0f) * input;
        var look = Quaternion.Euler(fly ? pitch : 0f, yaw, 0f);
        Vector3 move = look * input * speed;

        if (fly)
        {
            if (kbd && kb.spaceKey.isPressed) move += Vector3.up * speed;
            if (kbd && (kb.cKey.isPressed || kb.leftCtrlKey.isPressed)) move += Vector3.down * speed;
            if (cc) cc.enabled = false;
            origin.transform.position += move * Time.deltaTime;
            vy = 0f;
        }
        else
        {
            if (HunchNavAssist.hold || !cc || !cc.gameObject.activeInHierarchy) return;      // a door or a lift is carrying you
            if (!cc.enabled) cc.enabled = true;
            if (cc.isGrounded && vy <= 0f) { vy = -0.5f; if ((kbd && kb.spaceKey.wasPressedThisFrame) || padJump) vy = jumpSpeed; padJump = false; }
            else { vy += Physics.gravity.y * Time.deltaTime; padJump = false; }
            move.y = vy;
            cc.Move(move * Time.deltaTime);
            if (origin.transform.position.y < -400f) { if (tour) tour.GoTo(tour.Current); }      // last resort; HunchGuide normally respawns you long before this
        }
    }

    void LateUpdate()
    {
        if (!active || !origin) return;
        var cam = origin.Camera.transform;
        cam.SetPositionAndRotation(origin.transform.position + Vector3.up * eyeHeight, Quaternion.Euler(pitch, yaw, 0f));
    }
}
