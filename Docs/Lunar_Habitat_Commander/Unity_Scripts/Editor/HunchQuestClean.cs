// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchQuestClean.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : One-time clean-up so the Meta Quest build only contains what the game uses: takes out
//            the ad and Google Play billing add-ons (never used by the game), turns off the Samsung
//            Android XR features
//  Contains: HunchQuestClean
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// One-time clean-up so the Meta Quest build only contains what the game uses:
/// takes out the ad and Google Play billing add-ons (never used by the game), turns off the Samsung Android XR features
/// for Android, and keeps a copy of everything it changes in <project>/QuestCleanupBackup.
public static class HunchQuestClean
{
    static string Root { get { return Directory.GetParent(Application.dataPath).FullName; } }
    static StringBuilder log;
    static void Note(string s) { log.AppendLine(DateTime.Now.ToString("HH:mm:ss") + " " + s); }

    public static void Clean()
    {
        log = new StringBuilder();
        string bak = Path.Combine(Root, "QuestCleanupBackup");
        try
        {
            Directory.CreateDirectory(bak);
            EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);   // apply code changes after Play stops, not in the middle of a game
            Note("script changes now wait until Play mode ends");

            MoveOut("Assets/Scripts/UnityPurchasing", bak);
            MoveOut("Assets/LevelPlay", bak);

            string mt = Path.Combine(Root, "Assets/Plugins/Android/mainTemplate.gradle");
            if (File.Exists(mt))
            {
                Copy(mt, bak);
                var lines = File.ReadAllLines(mt).ToList();
                int before = lines.Count;
                lines.RemoveAll(l => l.Contains("com.unity3d.ads") || l.Contains("play-services-ads-identifier"));
                File.WriteAllLines(mt, lines);
                Note("mainTemplate.gradle: removed " + (before - lines.Count) + " ad library lines");
            }

            string am = Path.Combine(Root, "Assets/Plugins/Android/AndroidManifest.xml");
            if (File.Exists(am))
            {
                Copy(am, bak);
                string t = File.ReadAllText(am);
                string t2 = System.Text.RegularExpressions.Regex.Replace(t, "(com\\.oculus\\.supportedDevices\"\\s+android:value=\")[^\"]*\"", "$1" + HunchQuestGradleFix.Devices + "\"");
                if (t2 != t) { File.WriteAllText(am, t2); Note("AndroidManifest.xml: device list set to " + HunchQuestGradleFix.Devices); }
            }

            DisableAndroidXrFeatures();

            string pm = Path.Combine(Root, "Packages/manifest.json");
            Copy(pm, bak);
            var pl = File.ReadAllLines(pm).ToList();
            int pb = pl.Count;
            pl.RemoveAll(l => l.Contains("\"com.unity.services.levelplay\"") || l.Contains("\"com.unity.purchasing\""));
            File.WriteAllLines(pm, pl);
            Note("Packages/manifest.json: removed " + (pb - pl.Count) + " packages (LevelPlay ads, In-App Purchasing)");
        }
        catch (Exception e) { Note("FAILED " + e); }
        File.WriteAllText(Path.Combine(Root, "HunchBridge", "clean.txt"), log.ToString());
        AssetDatabase.Refresh();
        UnityEditor.PackageManager.Client.Resolve();
    }

    static void Copy(string file, string bak)
    {
        string dst = Path.Combine(bak, Path.GetFileName(file));
        if (!File.Exists(dst)) File.Copy(file, dst);
    }

    static void MoveOut(string rel, string bak)
    {
        string src = Path.Combine(Root, rel);
        if (!Directory.Exists(src)) { Note(rel + " not found (already moved?)"); return; }
        string dst = Path.Combine(bak, rel.Replace('/', '_'));
        if (Directory.Exists(dst)) dst += "_" + DateTime.Now.ToString("HHmmss");
        Directory.Move(src, dst);
        if (File.Exists(src + ".meta")) File.Move(src + ".meta", dst + ".meta");
        Note("moved " + rel + " to QuestCleanupBackup");
    }

    static void DisableAndroidXrFeatures()
    {
        var openxr = Type.GetType("UnityEngine.XR.OpenXR.OpenXRSettings, Unity.XR.OpenXR");
        if (openxr == null) { Note("OpenXR settings type not found"); return; }
        var get = openxr.GetMethod("GetSettingsForBuildTargetGroup", new[] { typeof(BuildTargetGroup) });
        var s = get != null ? get.Invoke(null, new object[] { BuildTargetGroup.Android }) : null;
        if (s == null) { Note("no OpenXR settings for Android"); return; }
        var gf = openxr.GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(m => m.Name == "GetFeatures" && !m.IsGenericMethod && m.GetParameters().Length == 0);
        var feats = gf != null ? (Array)gf.Invoke(s, null) : null;
        if (feats == null) { Note("could not list OpenXR features"); return; }
        foreach (var ft in feats)
        {
            var t = ft.GetType();
            var en = t.GetProperty("enabled");
            bool on = (bool)en.GetValue(ft);
            bool androidXr = t.FullName.Contains("AndroidXR") || (t.Namespace ?? "").Contains("AndroidXR");
            if (androidXr && on) { en.SetValue(ft, false); EditorUtility.SetDirty((UnityEngine.Object)ft); }
            Note("openxr android: " + (androidXr && on ? "TURNED OFF " : on ? "on  " : "off ") + t.FullName);
        }
        EditorUtility.SetDirty((UnityEngine.Object)s);
        AssetDatabase.SaveAssets();
    }
}
