// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchPortalKit.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : The "Stone portal" pack by Ordoss (Unity Asset Store). Build() lists its prefabs in
//            HunchBridge\portal_kit.txt and
//  Contains: HunchPortalKit
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// The "Stone portal" pack by Ordoss (Unity Asset Store). Build() lists its prefabs in HunchBridge\portal_kit.txt and
// saves Assets/Resources/HunchPortalKit.prefab, a holder with a copy of each one, which the game uses for its map gates.
public static class HunchPortalKit
{
    const string Src = "Assets/OrdossFX";
    static string Bridge { get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge"); } }

    [MenuItem("HUNCH/Portal pack: build the kit")]
    public static void Build()
    {
        var sb = new StringBuilder();
        try
        {
            AssetDatabase.Refresh(); var root = new GameObject("HunchPortalKit"); int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Src }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var src = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!src) continue;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(src); PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                g.name = Path.GetFileNameWithoutExtension(path); g.transform.SetParent(root.transform, false); g.transform.localPosition = Vector3.zero; n++;
                foreach (var t in g.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                foreach (var c in g.GetComponentsInChildren<Component>(true)) if (c is Camera || c is AudioListener || c is Rigidbody || c is Collider) UnityEngine.Object.DestroyImmediate(c);
                var b = new Bounds(Vector3.zero, Vector3.zero); bool first = true; int tris = 0, rn = 0;
                foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue; rn++; var mf = r.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) tris += mf.sharedMesh.triangles.Length / 3; if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
                sb.AppendLine("portal " + g.name + " | size " + b.size.x.ToString("F2") + " x " + b.size.y.ToString("F2") + " x " + b.size.z.ToString("F2") + " | centre " + b.center.ToString("F2") + " | mesh renderers " + rn + " | tris " + tris + " | particle systems " + g.GetComponentsInChildren<ParticleSystem>(true).Length + " | lights " + g.GetComponentsInChildren<Light>(true).Length + " | scripts " + g.GetComponentsInChildren<MonoBehaviour>(true).Length + " | animators " + g.GetComponentsInChildren<Animator>(true).Length + " | scale " + g.transform.localScale.ToString("F2") + " rot " + g.transform.localEulerAngles.ToString("F0"));
                foreach (Transform ch in g.transform) sb.AppendLine("     child " + ch.name + (ch.GetComponent<ParticleSystem>() ? " [particles]" : "") + (ch.GetComponent<MeshRenderer>() ? " [mesh]" : "") + (ch.GetComponent<Light>() ? " [light]" : "") + " pos " + ch.localPosition.ToString("F2"));
                foreach (var r in g.GetComponentsInChildren<Renderer>(true)) foreach (var m in r.sharedMaterials) if (m) sb.AppendLine("     material " + m.name + " -> " + (m.shader ? m.shader.name : "none") + (m.shader && !m.shader.isSupported ? " (NOT SUPPORTED)" : ""));
            }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/HunchPortalKit.prefab");
            UnityEngine.Object.DestroyImmediate(root); sb.AppendLine("prefabs " + n); sb.AppendLine("OK");
        }
        catch (Exception e) { sb.AppendLine("FAILED " + e); }
        File.WriteAllText(Path.Combine(Bridge, "portal_kit.txt"), sb.ToString());
    }
}
