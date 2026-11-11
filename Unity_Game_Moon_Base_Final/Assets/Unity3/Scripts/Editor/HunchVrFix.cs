// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Editor tool
//  File    : HunchVrFix.cs  (Editor only)
//  Purpose : Restores the OpenXR feature set (controller profiles, hand tracking, Quest support) in the
//            ACTIVE OpenXR settings from the older, fully configured Assets/XR/Settings/OpenXRPackageSettings.asset.
//            DryRun() only reports. Apply() turns ON the features that were on before (it never turns anything off).
//            Result: <project>/HunchBridge/vr_fix.txt
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.OpenXR;

public static class HunchVrFix
{
    static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HunchBridge"));
    const string OldAsset = "Assets/XR/Settings/OpenXRPackageSettings.asset";

    static HashSet<string> OldEnabled()
    {
        var set = new HashSet<string>();
        var txt = File.ReadAllText(OldAsset);
        foreach (var doc in txt.Split(new[] { "--- !u!" }, StringSplitOptions.None))
        {
            var n = Regex.Match(doc, @"m_Name: (.+)"); var e = Regex.Match(doc, @"m_enabled: (\d)");
            if (n.Success && e.Success && e.Groups[1].Value == "1") set.Add(n.Groups[1].Value.Trim());
        }
        return set;
    }

    static string Go(bool apply)
    {
        var sb = new StringBuilder(DateTime.Now.ToString("HH:mm:ss") + (apply ? " APPLY\n" : " DRY RUN\n"));
        if (!File.Exists(OldAsset)) return sb.Append("old settings asset not found — nothing to restore").ToString();
        var old = OldEnabled();
        sb.AppendLine("enabled in old settings: " + string.Join(" | ", old.OrderBy(x => x)));
        foreach (var g in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
        {
            var st = OpenXRSettings.GetSettingsForBuildTargetGroup(g);
            if (st == null) { sb.AppendLine(g + ": no active OpenXR settings"); continue; }
            string suffix = g == BuildTargetGroup.Standalone ? " Standalone" : " Android";
            foreach (var f in st.GetFeatures())
            {
                if (f == null) continue;
                string key = f.GetType().Name + suffix;
                if (old.Contains(key) && !f.enabled)
                {
                    sb.AppendLine((apply ? "ENABLED " : "would enable ") + key + "  (" + f.name + ")");
                    if (apply) { f.enabled = true; EditorUtility.SetDirty(f); }
                }
            }
            sb.AppendLine(g + " now ON: " + string.Join(" | ", st.GetFeatures().Where(f => f && f.enabled).Select(f => f.GetType().Name)));
            if (apply) EditorUtility.SetDirty(st);
        }
        if (apply) AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    /// Unity's own OpenXR project validation (the same list as Project Settings > XR Plug-in Management > Project Validation).
    public static void Validate()
    {
        var sb = new StringBuilder(DateTime.Now.ToString("HH:mm:ss") + " VALIDATION\n");
        foreach (var g in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
        {
            var issues = new List<UnityEngine.XR.OpenXR.Features.OpenXRFeature.ValidationRule>();
            UnityEditor.XR.OpenXR.OpenXRProjectValidation.GetCurrentValidationIssues(issues, g);
            sb.AppendLine($"{g}: {issues.Count} issue(s)");
            foreach (var i in issues) sb.AppendLine($"   [{(i.error ? "ERROR" : "warn")}] {i.message}");
        }
        File.WriteAllText(Path.Combine(Dir, "vr_validate.txt"), sb.ToString());
    }

    public static void DryRun() => File.WriteAllText(Path.Combine(Dir, "vr_fix.txt"), Go(false));
    public static void Apply() => File.WriteAllText(Path.Combine(Dir, "vr_fix.txt"), Go(true));
}
#endif
