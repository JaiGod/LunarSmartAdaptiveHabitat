// PROJECT ARES · editor helper: tidies the wellness asset import (Resources/Wellness is the only copy the game loads).
// Run through the bridge: invoke HunchWellnessSetup.Run → HunchBridge/wellness_setup.txt
// © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class HunchWellnessSetup
{
    public static void Run()
    {
        var log = new System.Text.StringBuilder();
        if (AssetDatabase.IsValidFolder("Assets/Unity3/Models/Wellness")) { AssetDatabase.DeleteAsset("Assets/Unity3/Models/Wellness"); log.AppendLine("removed old Assets/Unity3/Models/Wellness"); }
        AssetDatabase.Refresh();
        foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Unity3/Resources/Wellness" }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g); var mi = AssetImporter.GetAtPath(p) as ModelImporter; if (!mi) continue;
            bool ch = false;
            if (mi.importCameras || mi.importLights) { mi.importCameras = false; mi.importLights = false; ch = true; }
            if (mi.importAnimation) { mi.importAnimation = false; ch = true; }
            if (ch) mi.SaveAndReimport();
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            var b = new Bounds(); bool f = false; foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!f) { b = r.bounds; f = true; } else b.Encapsulate(r.bounds); }
            log.AppendLine($"{Path.GetFileNameWithoutExtension(p)}: {go.GetComponentsInChildren<Renderer>().Length} renderers, size {b.size:F2}, front marker {(go.GetComponentsInChildren<Transform>().Any(t => t.name == "FRONT_MARKER") ? "yes" : "NO")}");
        }
        File.WriteAllText(Path.Combine("HunchBridge", "wellness_setup.txt"), log.ToString());
    }
}
