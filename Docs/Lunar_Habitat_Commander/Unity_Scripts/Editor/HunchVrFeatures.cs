// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchVrFeatures.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Brings in the VR interaction sample sets that ship inside the XR packages already
//            installed in this project
//  Contains: HunchVrFeatures
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

// Brings in the VR interaction sample sets that ship inside the XR packages already installed in this project
// (they are copied from the package folder, nothing comes from the internet), and reports which VR features exist.
// Menu: HUNCH / Import all VR interaction samples.   Report: HunchBridge\vr_features.txt
public static class HunchVrFeatures
{
    static readonly string[] Packages = { "com.unity.xr.interaction.toolkit", "com.unity.xr.hands" };
    // these need other products (Apple Vision Pro, phone AR) or old input code and would not compile or be used here
    static readonly string[] Skip = { "visionos", "ar starter", "legacy", "polyspatial" };

    static string Bridge { get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge"); } }

    [MenuItem("HUNCH/Import all VR interaction samples")]
    public static void Import()
    {
        var log = new StringBuilder(); var added = new List<string>();
        try
        {
            foreach (var pkg in Packages)
            {
                var samples = Sample.FindByPackage(pkg, string.Empty).ToList();
                log.AppendLine(pkg + ": " + samples.Count + " sample sets");
                foreach (var s in samples)
                {
                    string name = s.displayName ?? "";
                    bool skip = Skip.Any(k => name.ToLowerInvariant().Contains(k));
                    if (s.isImported) { log.AppendLine("  have     " + name); continue; }
                    if (skip) { log.AppendLine("  skipped  " + name + "  (needs other hardware or packages)"); continue; }
                    bool ok = s.Import(Sample.ImportOptions.HideImportWindow | Sample.ImportOptions.OverridePreviousImports);
                    log.AppendLine((ok ? "  ADDED    " : "  FAILED   ") + name + "  -> " + s.importPath);
                    if (ok) added.Add(s.importPath);
                }
            }
            File.WriteAllLines(Path.Combine(Bridge, "vr_features_added.txt"), added.ToArray());
            log.AppendLine("OK added " + added.Count);
        }
        catch (Exception e) { log.AppendLine("FAILED " + e); }
        File.WriteAllText(Path.Combine(Bridge, "vr_features.txt"), log.ToString());
        AssetDatabase.Refresh();
    }

    // takes the last import back out (used if a sample set does not compile in this project)
    public static void Undo()
    {
        var log = new StringBuilder();
        try
        {
            string list = Path.Combine(Bridge, "vr_features_added.txt");
            if (File.Exists(list))
                foreach (var p in File.ReadAllLines(list))
                {
                    if (string.IsNullOrEmpty(p) || !Directory.Exists(p)) continue;
                    string full = Path.GetFullPath(p), assets = Path.GetFullPath(Application.dataPath);
                    if (!full.StartsWith(assets, StringComparison.OrdinalIgnoreCase) || full.Length <= assets.Length + 8) continue;      // only ever inside Assets\Samples
                    Directory.Delete(full, true); if (File.Exists(full + ".meta")) File.Delete(full + ".meta");
                    log.AppendLine("removed " + p);
                }
            log.AppendLine("OK");
        }
        catch (Exception e) { log.AppendLine("FAILED " + e); }
        File.WriteAllText(Path.Combine(Bridge, "vr_features_undo.txt"), log.ToString());
        AssetDatabase.Refresh();
    }
}
