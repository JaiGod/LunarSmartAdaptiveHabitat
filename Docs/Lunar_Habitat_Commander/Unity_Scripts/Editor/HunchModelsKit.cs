// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchModelsKit.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Models made in Blender for this game live in Assets/HUNCH/Models as .fbx files (the
//            .blend is in unity2\Blender).
//  Contains: HunchModelsKit
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Models made in Blender for this game live in Assets/HUNCH/Models as .fbx files (the .blend is in unity2\Blender).
// Build() gives each one clean URP materials (saved in Assets/HUNCH/Models/Materials) and saves
// Assets/Resources/HunchModels.prefab, a holder the game loads at run time (HunchFrontier13.Kit).
public static class HunchModelsKit
{
    const string Dir = "Assets/HUNCH/Models";
    static readonly string[] Names = { "Astronaut", "Lumi", "HospitalBed", "Pumpkin", "GiftBox", "GiftLid", "LegendPortal", "HubCap", "HubShell", "PlasmaRepeater", "LaserCannon", "StarMinigun", "CometSword", "NinjaStar", "AsteroidBlaster", "Alien", "Ufo", "FakeTree" };
    static string Bridge { get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge"); } }

    [MenuItem("HUNCH/Models: build the kit")]
    public static void Build()
    {
        var sb = new StringBuilder();
        try
        {
            AssetDatabase.Refresh();
            if (!AssetDatabase.IsValidFolder(Dir + "/Materials")) AssetDatabase.CreateFolder(Dir, "Materials");
            var lit = Shader.Find("Universal Render Pipeline/Lit"); var made = new Dictionary<string, Material>(); int baked = 0; var root = new GameObject("HunchModels");
            foreach (string n in Names)
            {
                string path = Dir + "/" + n + ".fbx"; var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!imp) { sb.AppendLine("missing " + path); continue; }
                imp.importCameras = false; imp.importLights = false; imp.importBlendShapes = false;
                if (n == "LegendPortal") { imp.animationType = ModelImporterAnimationType.Legacy; imp.importAnimation = true; imp.generateAnimations = ModelImporterGenerateAnimations.GenerateAnimations; imp.animationWrapMode = WrapMode.Loop; }      // its rings and crystals are animated in Blender
                else { imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None; }
                imp.SaveAndReimport();
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(path); var g = (GameObject)PrefabUtility.InstantiatePrefab(src); PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                g.name = n; g.transform.SetParent(root.transform, false); g.transform.localPosition = Vector3.zero;
                foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                {
                    var ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++)
                    {
                        if (!ms[i]) continue; string mn = ms[i].name; Material m;
                        if (!made.TryGetValue(mn, out m))
                        {
                            Color c = ms[i].HasProperty("_BaseColor") ? ms[i].GetColor("_BaseColor") : (ms[i].HasProperty("_Color") ? ms[i].GetColor("_Color") : Color.white);
                            float metal = ms[i].HasProperty("_Metallic") ? ms[i].GetFloat("_Metallic") : 0f, smooth = ms[i].HasProperty("_Smoothness") ? ms[i].GetFloat("_Smoothness") : (ms[i].HasProperty("_Glossiness") ? ms[i].GetFloat("_Glossiness") : 0.5f);
                            m = new Material(lit); m.name = mn; c.a = 1f; m.SetColor("_BaseColor", c);
                            string low = mn.ToLowerInvariant();
                            if (low.Contains("glow")) { m.SetColor("_EmissionColor", c * 2.6f); m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; metal = 0f; smooth = 0.5f; }
                            else if (low.Contains("visor")) { metal = 1f; smooth = 0.92f; }
                            else if (low.Contains("metal") || low.Contains("steel")) { metal = 0.85f; smooth = 0.7f; }
                            else if (low.Contains("gold") || low.Contains("ribbon")) { metal = 0.8f; smooth = 0.75f; }
                            else if (low.Contains("fur") || low.Contains("belly") || low.Contains("sheet") || low.Contains("blanket") || low.Contains("suit")) { metal = 0f; smooth = 0.25f; }
                            else if (low.Contains("eye") || low.Contains("pupil")) { metal = 0f; smooth = 0.85f; }
                            m.SetFloat("_Metallic", Mathf.Clamp01(metal)); m.SetFloat("_Smoothness", Mathf.Clamp01(smooth));
                            // baked texture maps made in Blender (colour, normal, metal + smoothness), when this material has them
                            string tx = Dir + "/Textures/" + mn;
                            var tc = AssetDatabase.LoadAssetAtPath<Texture2D>(tx + "_Color.png"); var tn = AssetDatabase.LoadAssetAtPath<Texture2D>(tx + "_Normal.png"); var tm = AssetDatabase.LoadAssetAtPath<Texture2D>(tx + "_MS.png");
                            if (tc) { m.SetTexture("_BaseMap", tc); m.SetColor("_BaseColor", Color.white); if (low.Contains("glow")) { m.SetTexture("_EmissionMap", tc); m.SetColor("_EmissionColor", Color.white * 1.9f); } baked++; }
                            if (tn) { m.SetTexture("_BumpMap", tn); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
                            if (tm) { m.SetTexture("_MetallicGlossMap", tm); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 1f); m.SetFloat("_Metallic", 1f); }
                            string mp = Dir + "/Materials/" + mn + ".mat"; AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(m, mp); made[mn] = m;
                        }
                        ms[i] = m;
                    }
                    r.sharedMaterials = ms;
                }
                var b = new Bounds(Vector3.zero, Vector3.zero); bool first = true; foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
                sb.AppendLine("model " + n + " | size " + b.size.x.ToString("F2") + " x " + b.size.y.ToString("F2") + " x " + b.size.z.ToString("F2") + " | centre " + b.center.ToString("F2") + " | rotation " + g.transform.localEulerAngles.ToString("F0") + " | scale " + g.transform.localScale.ToString("F2") + " | children " + g.transform.childCount + " | animation " + (g.GetComponentInChildren<Animation>() ? g.GetComponentInChildren<Animation>().GetClipCount() + " clip(s)" : "none"));
            }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/HunchModels.prefab"); UnityEngine.Object.DestroyImmediate(root); AssetDatabase.SaveAssets();
            sb.AppendLine("materials " + made.Count + ", with baked textures " + baked); sb.AppendLine("OK");
        }
        catch (Exception e) { sb.AppendLine("FAILED " + e); }
        File.WriteAllText(Path.Combine(Bridge, "models_kit.txt"), sb.ToString());
    }
}
