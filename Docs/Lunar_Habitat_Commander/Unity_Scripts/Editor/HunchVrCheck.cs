// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchVrCheck.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Checks whether this PC is ready to show the game in a VR headset, and writes the
//            answer to HunchBridge/vr_check.txt. Menu: HUNCH > Check VR connection. It only reads:
//            nothing on the PC is changed.
//  Contains: HunchVrCheck
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Checks whether this PC is ready to show the game in a VR headset, and writes the answer to HunchBridge/vr_check.txt.
/// Menu: HUNCH > Check VR connection. It only reads: nothing on the PC is changed.
public static class HunchVrCheck
{
    /// While the game is playing: what the VR system reports (headset worn, controllers found, picture size).
    public static void Runtime()
    {
        var sb = new StringBuilder();
        sb.AppendLine("checked " + DateTime.Now.ToString("HH:mm:ss") + " playing=" + EditorApplication.isPlaying);
        try
        {
            sb.AppendLine("runtime=" + UnityEngine.XR.OpenXR.OpenXRRuntime.name + " " + UnityEngine.XR.OpenXR.OpenXRRuntime.version);
            sb.AppendLine("device=" + UnityEngine.XR.XRSettings.loadedDeviceName + " active=" + UnityEngine.XR.XRSettings.isDeviceActive + " eye=" + UnityEngine.XR.XRSettings.eyeTextureWidth + "x" + UnityEngine.XR.XRSettings.eyeTextureHeight + " mode=" + UnityEngine.XR.XRSettings.stereoRenderingMode);
            var displays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>(); SubsystemManager.GetSubsystems(displays);
            foreach (var d in displays) sb.AppendLine("display running=" + d.running + " renderPasses=" + d.GetRenderPassCount() + " opaque=" + d.displayOpaque);
            var devs = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>(); UnityEngine.XR.InputDevices.GetDevices(devs);
            foreach (var dev in devs)
            {
                bool present = false, tracked = false, trig = false; Vector3 pos = Vector3.zero;
                bool hasPresence = dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence, out present);
                dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out tracked);
                dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out pos);
                dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out trig);
                sb.AppendLine("input " + dev.name + " | " + dev.characteristics + " | valid=" + dev.isValid + " tracked=" + tracked + " pos=" + pos.ToString("F2") + (hasPresence ? " worn=" + present : "") + " trigger=" + trig);
            }
            var cam = Camera.main;
            if (cam) sb.AppendLine("camera " + cam.name + " pos=" + cam.transform.position.ToString("F2") + " local=" + cam.transform.localPosition.ToString("F2") + " stereo=" + cam.stereoEnabled + " enabled=" + cam.enabled);
            var cc = UnityEngine.Object.FindFirstObjectByType<CharacterController>();
            if (cc) sb.AppendLine("body height=" + cc.height.ToString("F2") + " radius=" + cc.radius.ToString("F2") + " centre=" + cc.center.ToString("F2") + " enabled=" + cc.enabled + " grounded=" + cc.isGrounded);
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "Left Controller" || t.name == "Right Controller" || t.name == "Locomotion" || t.name == "Left Hand" || t.name == "Right Hand" || t.name == "HUNCH Dashboard Card")
                    sb.AppendLine("object " + t.name + " active=" + t.gameObject.activeInHierarchy + " pos=" + t.position.ToString("F2"));
        }
        catch (Exception e) { sb.AppendLine("error " + e.Message); }
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HunchBridge"));
        try { File.WriteAllText(Path.Combine(dir, "vr_runtime.txt"), sb.ToString()); } catch { }
    }

    [MenuItem("HUNCH/Check VR connection")]
    public static void Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine("checked " + DateTime.Now.ToString("HH:mm:ss"));

        // which OpenXR runtime Windows will hand to Unity (the Quest Link app must be the active one)
        string active = "";
        try
        {
            var reg = Type.GetType("Microsoft.Win32.Registry, mscorlib") ?? Type.GetType("Microsoft.Win32.Registry, Microsoft.Win32.Registry") ?? Type.GetType("Microsoft.Win32.Registry");
            if (reg != null)
            {
                var get = reg.GetMethod("GetValue", new[] { typeof(string), typeof(string), typeof(object) });
                active = get.Invoke(null, new object[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Khronos\OpenXR\1", "ActiveRuntime", "" }) as string ?? "";
            }
            else active = "(could not read the registry)";
        }
        catch (Exception e) { active = "(error " + (e.InnerException ?? e).Message + ")"; }
        sb.AppendLine("activeRuntime=" + (active.Length == 0 ? "NONE" : active));
        string a = active.ToLowerInvariant();
        sb.AppendLine("runtimeKind=" + (active.Length == 0 ? "none" : (a.Contains("oculus") || a.Contains("meta") ? "meta-quest-link" : (a.Contains("steam") ? "steamvr" : "other"))));

        // which headset programs are running right now
        foreach (var name in new[] { "OVRServer_x64", "OVRRedir", "OVRServiceLauncher", "OculusClient", "Meta Quest Link", "MetaQuestLink", "vrserver", "vrmonitor" })
        {
            int n = 0; try { n = System.Diagnostics.Process.GetProcessesByName(name).Length; } catch { n = -1; }
            sb.AppendLine("process " + name + "=" + n);
        }
        try
        {
            string pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            sb.AppendLine("linkInstalled=" + (Directory.Exists(Path.Combine(pf, "Oculus")) || Directory.Exists(Path.Combine(pf, "Meta Horizon")) || Directory.Exists(Path.Combine(pf, "Meta Quest Link"))));
            sb.AppendLine("oculusBase=" + (Environment.GetEnvironmentVariable("OculusBase") ?? ""));
        }
        catch { }
        sb.AppendLine("buildTarget=" + EditorUserBuildSettings.activeBuildTarget);
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HunchBridge"));
        try { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "vr_check.txt"), sb.ToString()); } catch { }
        Debug.Log("[HUNCH] VR check\n" + sb);
    }
}
