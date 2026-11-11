// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchSceneList.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Lists the big parts of the open scene (name, path, size, centre) to
//            HunchBridge\scene_big.txt, so the outer dome and the
//  Contains: HunchSceneList
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Lists the big parts of the open scene (name, path, size, centre) to HunchBridge\scene_big.txt, so the outer dome and the
// tall central rod can be named for NASA HUNCH mode.
public static class HunchSceneList
{
    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderByDescending(x => x.bounds.size.magnitude).Take(90))
        {
            var b = r.bounds; string path = r.name; var t = r.transform.parent; int d = 0; while (t && d++ < 4) { path = t.name + "/" + path; t = t.parent; }
            var mf = r.GetComponent<MeshFilter>(); int tris = mf && mf.sharedMesh ? mf.sharedMesh.triangles.Length / 3 : 0;
            sb.AppendLine(path + " | size " + b.size.x.ToString("F1") + " x " + b.size.y.ToString("F1") + " x " + b.size.z.ToString("F1") + " | centre " + b.center.x.ToString("F1") + ", " + b.center.y.ToString("F1") + ", " + b.center.z.ToString("F1") + " | tris " + tris + " | mat " + (r.sharedMaterial ? r.sharedMaterial.name : "-"));
        }
        // every part of the main base (not the far sites), smallest last, for the realistic-scale view of NASA HUNCH mode
        var all = new StringBuilder(); var model = GameObject.Find("HUNCH_Lunar_Base");
        if (model) foreach (Transform ch in model.transform)
        {
            var rs = ch.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            if (new Vector2(b.center.x, b.center.z).magnitude > 160f || ch.name.StartsWith("E2_") || ch.name.StartsWith("E3_") || ch.name.StartsWith("E4_") || ch.name.StartsWith("E5_") || ch.name.StartsWith("E6_")) continue;
            all.AppendLine(ch.name + " | size " + b.size.x.ToString("F0") + " x " + b.size.y.ToString("F0") + " x " + b.size.z.ToString("F0") + " | centre " + b.center.x.ToString("F0") + ", " + b.center.y.ToString("F0") + ", " + b.center.z.ToString("F0") + " | col " + ch.GetComponentsInChildren<Collider>(true).Length + (ch.gameObject.activeSelf ? "" : " (off)"));
        }
        foreach (var rootName in new[] { "HUNCH Crew", "HUNCH Zones" }) { var g = GameObject.Find(rootName); if (g) foreach (Transform ch in g.transform) all.AppendLine(rootName + "/" + ch.name + " at " + ch.position.ToString("F1") + " children " + ch.childCount); }
        File.WriteAllText(Path.Combine(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge"), "scene_all.txt"), all.ToString());
        sb.AppendLine("--- roots"); foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) sb.AppendLine(g.name + " children " + g.transform.childCount + (g.activeSelf ? "" : " (off)"));
        sb.AppendLine("OK");
        File.WriteAllText(Path.Combine(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge"), "scene_big.txt"), sb.ToString());
    }
}
