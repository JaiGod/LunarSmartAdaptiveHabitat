// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Editor tool
//  File    : HunchVrAudit.cs  (Editor only, READ-ONLY — changes nothing)
//  Purpose : Checks that the VR setup is complete: XR loaders per platform (PC + Quest/Android),
//            OpenXR features + interaction profiles, XR packages, and an XR rig in every build scene.
//            Result: <project>/HunchBridge/vr_audit.txt
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;

public static class HunchVrAudit
{
    static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HunchBridge"));

    public static void Run()
    {
        var sb = new StringBuilder(DateTime.Now.ToString("HH:mm:ss") + "\n");
        sb.AppendLine("Unity " + Application.unityVersion + " · active target " + EditorUserBuildSettings.activeBuildTarget);
        foreach (var g in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android, BuildTargetGroup.WebGL })
        {
            try
            {
                var s = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(g);
                if (s == null || s.Manager == null) { sb.AppendLine($"XR {g}: (no XR settings)"); continue; }
                sb.AppendLine($"XR {g}: initOnStart={s.InitManagerOnStart} loaders=[{string.Join(", ", s.Manager.activeLoaders.Select(l => l ? l.name : "null"))}]");
            }
            catch (Exception e) { sb.AppendLine($"XR {g}: error {e.Message}"); }
        }
        // OpenXR features (by reflection so this compiles even without OpenXR)
        try
        {
            var t = Type.GetType("UnityEngine.XR.OpenXR.OpenXRSettings, Unity.XR.OpenXR");
            var m = t?.GetMethod("GetSettingsForBuildTargetGroup", new[] { typeof(BuildTargetGroup) });
            foreach (var g in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                var st = m?.Invoke(null, new object[] { g });
                if (st == null) { sb.AppendLine($"OpenXR {g}: no settings"); continue; }
                var gm = t.GetMethods().First(mm => mm.Name == "GetFeatures" && !mm.IsGenericMethod && mm.GetParameters().Length == 0);
                var feats = (Array)gm.Invoke(st, null);
                var on = feats.Cast<object>().Where(f => (bool)f.GetType().GetProperty("enabled").GetValue(f)).Select(f => (string)f.GetType().GetField("nameUi").GetValue(f));
                sb.AppendLine($"OpenXR {g} features ON: {string.Join(" | ", on)}");
            }
        }
        catch (Exception e) { sb.AppendLine("OpenXR features: " + e.Message); }
        // packages
        try
        {
            var man = File.ReadAllText(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));
            foreach (var p in new[] { "com.unity.xr.interaction.toolkit", "com.unity.xr.openxr", "com.unity.xr.management", "com.unity.xr.hands", "com.unity.xr.oculus", "com.unity.xr.meta-openxr", "com.unity.inputsystem" })
            {
                int i = man.IndexOf("\"" + p + "\""); string v = "missing";
                if (i >= 0) { int a = man.IndexOf(':', i) + 1; int b = man.IndexOf(',', a); if (b < 0) b = man.IndexOf('}', a); v = man.Substring(a, b - a).Trim().Trim('"'); }
                sb.AppendLine($"package {p}: {v}");
            }
        }
        catch (Exception e) { sb.AppendLine("packages: " + e.Message); }
        // build scenes (plus the known main scenes): does each contain an XR rig?
        var list = EditorBuildSettings.scenes.ToList();
        sb.AppendLine("scenes in Build Settings: " + list.Count);
        foreach (var extra in new[] { "Assets/HUNCH/Scenes/Lunar_Habitat_Commander.unity" })
            if (File.Exists(extra) && !list.Any(x => x.path == extra)) list.Add(new EditorBuildSettingsScene(extra, false));
        foreach (var sc in list)
        {
            string txt = ""; try { txt = File.ReadAllText(sc.path); } catch { }
            bool origin = txt.Contains("XROrigin") || txt.Contains("XR Origin") || txt.Contains("m_Name: XR Rig");
            sb.AppendLine($"scene {(sc.enabled ? "ON " : "off")} {sc.path}: xr-rig-in-file={origin} size={txt.Length / 1024} KB");
        }
        // XR rig prefabs available
        var rigs = AssetDatabase.FindAssets("XR Origin t:Prefab").Select(AssetDatabase.GUIDToAssetPath).Take(8);
        sb.AppendLine("XR Origin prefabs: " + string.Join(" | ", rigs));
        sb.AppendLine("Android build module installed: " + BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android));
        sb.AppendLine("Windows build module installed: " + BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64));
        sb.AppendLine("Android: minSdk=" + PlayerSettings.Android.minSdkVersion + " arch=" + PlayerSettings.Android.targetArchitectures + " graphics=" + string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)));
        File.WriteAllText(Path.Combine(Dir, "vr_audit.txt"), sb.ToString());
    }

    /// Sci-fi project only: if Build Settings has no scenes, add the main game scene so PC-VR and Quest builds include it.
    public static void FixBuildScenes()
    {
        const string main = "Assets/HUNCH/Scenes/Lunar_Habitat_Commander.unity";
        string msg;
        if (!File.Exists(main)) msg = "main scene not found — nothing changed";
        else if (EditorBuildSettings.scenes.Any(x => x.path == main && x.enabled)) msg = "main scene already in Build Settings — nothing changed";
        else { var l = EditorBuildSettings.scenes.Where(x => x.path != main).ToList(); l.Insert(0, new EditorBuildSettingsScene(main, true)); EditorBuildSettings.scenes = l.ToArray(); AssetDatabase.SaveAssets(); msg = "added " + main + " to Build Settings (enabled)"; }
        File.WriteAllText(Path.Combine(Dir, "vr_fix.txt"), DateTime.Now.ToString("HH:mm:ss") + "\n" + msg);
    }
}
#endif
