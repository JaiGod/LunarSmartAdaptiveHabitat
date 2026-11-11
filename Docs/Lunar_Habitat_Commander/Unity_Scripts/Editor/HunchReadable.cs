// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchReadable.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Development helper: the walkable map (HunchPath) is built while the game runs, from
//            the solid shapes of the base. Unity can only read those shapes in a built game when
//            their meshes are marked Read/Write. This marks every mesh
//  Contains: HunchReadable
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Development helper: the walkable map (HunchPath) is built while the game runs, from the solid shapes of the base.
/// Unity can only read those shapes in a built game when their meshes are marked Read/Write. This marks every mesh
/// used by a solid shape in the open scene, so the "does not allow read access" errors go and the map also works in the .exe.
public static class HunchReadable
{
    public static void Fix()
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HunchBridge")); var sb = new StringBuilder();
        var models = new HashSet<string>(); int meshAssets = 0, already = 0, total = 0, skipped = 0;
        foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var m = mc.sharedMesh; if (!m) continue; total++;
            if (m.isReadable) { already++; continue; }
            string path = AssetDatabase.GetAssetPath(m); if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets")) { skipped++; continue; }
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp) { models.Add(path); continue; }
            var so = new SerializedObject(m); var p = so.FindProperty("m_IsReadable");
            if (p != null) { p.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(m); meshAssets++; } else skipped++;
        }
        foreach (var path in models) { var imp = (ModelImporter)AssetImporter.GetAtPath(path); if (!imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); } sb.AppendLine("model " + path); }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        int left = 0; foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (mc.sharedMesh && !mc.sharedMesh.isReadable) left++;
        sb.Insert(0, "solid meshes " + total + " | already readable " + already + " | models changed " + models.Count + " | mesh assets changed " + meshAssets + " | skipped " + skipped + " | still not readable " + left + "\n");
        File.WriteAllText(Path.Combine(dir, "readable.txt"), sb.ToString());
    }
}
