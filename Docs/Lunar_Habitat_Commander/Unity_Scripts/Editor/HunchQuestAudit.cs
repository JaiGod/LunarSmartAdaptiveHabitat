// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchQuestAudit.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Finds which of the project's optional packages the game's own scripts actually use,
//            and which OpenXR features are on for Android.
//  Contains: HunchQuestAudit
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Finds which of the project's optional packages the game's own scripts actually use, and which OpenXR features are on for Android.
public static class HunchQuestAudit
{
    static readonly string[] Keys = {
        "UnityEngine.Purchasing", "Unity.Services", "LevelPlay", "IronSource", "Unity.Advertisement", "UnityEngine.Advertisements",
        "Meta.WitAi", "Oculus.Voice", "Meta.Voice", "AndroidXR", "Unity.XR.AndroidXR", "Microsoft.GDK", "Unity.GameCore", "XGamingRuntime",
        "Authentication", "PlayerAccounts"
    };

    public static void Scan()
    {
        var sb = new StringBuilder();
        string assets = Application.dataPath;
        foreach (var f in Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories))
        {
            string txt;
            try { txt = File.ReadAllText(f); } catch { continue; }
            var hits = Keys.Where(k => txt.Contains(k)).ToArray();
            if (hits.Length > 0) sb.AppendLine("CS " + f.Substring(assets.Length) + " : " + string.Join(", ", hits));
        }
        foreach (var f in Directory.GetFiles(assets, "*.asmdef", SearchOption.AllDirectories))
        {
            string txt = File.ReadAllText(f);
            var hits = Keys.Where(k => txt.Contains(k)).ToArray();
            if (hits.Length > 0) sb.AppendLine("ASMDEF " + f.Substring(assets.Length) + " : " + string.Join(", ", hits));
        }
        foreach (var d in new[] { "Plugins/Android", "LevelPlay", "IronSource", "Resources/IronSource" })
        {
            string p = Path.Combine(assets, d);
            if (Directory.Exists(p)) sb.AppendLine("DIR Assets/" + d + " : " + string.Join(" | ", Directory.GetFileSystemEntries(p).Select(Path.GetFileName)));
        }
        try
        {
            var settingsType = Type.GetType("UnityEditor.XR.OpenXR.Features.FeatureHelpers, Unity.XR.OpenXR.Editor");
            var openxr = Type.GetType("UnityEngine.XR.OpenXR.OpenXRSettings, Unity.XR.OpenXR");
            var get = openxr != null ? openxr.GetMethod("GetSettingsForBuildTargetGroup", new[] { typeof(BuildTargetGroup) }) : null;
            var s = get != null ? get.Invoke(null, new object[] { BuildTargetGroup.Android }) : null;
            if (s != null)
            {
                var feats = (Array)openxr.GetMethod("GetFeatures", Type.EmptyTypes).Invoke(s, null);
                foreach (var ft in feats)
                {
                    var t = ft.GetType();
                    bool en = (bool)t.GetProperty("enabled").GetValue(ft);
                    var nameF = t.GetField("nameUi", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    sb.AppendLine("OPENXR-ANDROID " + (en ? "ON  " : "off ") + t.FullName + (nameF != null ? " (" + nameF.GetValue(ft) + ")" : ""));
                }
            }
            else sb.AppendLine("OPENXR settings for Android not found");
        }
        catch (Exception e) { sb.AppendLine("openxr scan failed " + e.Message); }
        try
        {
            var per = Type.GetType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget, Unity.XR.Management.Editor");
            var gs = per != null ? per.GetMethod("XRGeneralSettingsForBuildTarget").Invoke(null, new object[] { BuildTargetGroup.Android }) : null;
            var mgr = gs != null ? gs.GetType().GetProperty("Manager").GetValue(gs) : null;
            var loaders = mgr != null ? mgr.GetType().GetProperty("activeLoaders").GetValue(mgr) as System.Collections.IEnumerable : null;
            if (loaders != null) foreach (var l in loaders) sb.AppendLine("XR loader Android: " + l.GetType().FullName);
            else sb.AppendLine("XR loaders Android: none found");
        }
        catch (Exception e) { sb.AppendLine("loader scan failed " + e.Message); }
        File.WriteAllText(Path.Combine(Directory.GetParent(assets).FullName, "HunchBridge", "audit.txt"), sb.ToString());
    }
}
