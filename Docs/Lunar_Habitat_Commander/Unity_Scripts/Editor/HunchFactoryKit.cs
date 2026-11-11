// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchFactoryKit.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : The "Unity Factory" pack from the Asset Store is made for HDRP; this project uses URP.
//  Contains: HunchFactoryKit
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// The "Unity Factory" pack from the Asset Store is made for HDRP; this project uses URP.
// Convert()  switches the pack's materials to URP Lit, keeping colour, normal, metal/smoothness, occlusion and glow maps.
// Report()   writes HunchBridge\factory_kit.txt: every prefab and model in the pack with its size, so pieces can be chosen.
// Build()    reads HunchBridge\factory_pick.txt (one asset path per line) and saves Assets/Resources/HunchFactoryKit.prefab,
//            a holder whose children are clean copies of the chosen pieces (no pack scripts, lights, cameras or missing scripts).
//            The game loads that holder at run time and places the pieces in Factory Land.
public static class HunchFactoryKit
{
    const string Src = "Assets/UnityFactorySceneHDRP";
    static string Bridge { get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge"); } }

    [MenuItem("HUNCH/Factory pack: convert materials to URP")]
    public static void Convert()
    {
        var sb = new StringBuilder();
        try
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit"); int done = 0, total = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Src }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (!m) continue; total++;
                bool again = m.shader == lit;
                string was = again ? "URP Lit, second pass" : (m.shader ? m.shader.name : "none"); var names = new StringBuilder();
                Texture baseT = null, normT = null, maskT = null, emisT = null, metalT = null, occT = null; Color baseC = Color.white, emisC = Color.black; bool gotBase = false; float metal = 0f, smooth = 0.5f, smoothMax = -1f, surface = 0f, cutoff = 0f, cut = 0.5f;
                var so = new SerializedObject(m);
                var tex = so.FindProperty("m_SavedProperties.m_TexEnvs");
                for (int i = 0; tex != null && i < tex.arraySize; i++)
                {
                    var e = tex.GetArrayElementAtIndex(i); string n = e.FindPropertyRelative("first").stringValue.ToLowerInvariant(); var t = e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture; if (!t) continue; names.Append(n).Append(' ');
                    if (n == "_basemap" || n == "_bumpmap" || n == "_metallicglossmap" || n == "_occlusionmap" || n == "_emissionmap") continue;      // what an earlier pass wrote
                    if (n.Contains("detail") || n.Contains("height") || n.Contains("bent") || n.Contains("tangent") || n.Contains("coat") || n.Contains("thickness") || n.Contains("subsurface") || n.Contains("anisotropy") || n.Contains("iridescence") || n.Contains("specular") || n.Contains("transmi") || n.Contains("distortion")) continue;
                    if (n.Contains("normal")) { if (!normT || n == "_normalmap") normT = t; }
                    else if (n.Contains("mask")) { if (!maskT || n == "_maskmap") maskT = t; }
                    else if (n.Contains("metal") || n.Contains("smooth")) { if (!metalT) metalT = t; if (n.Contains("occ")) occT = t; }
                    else if (n.Contains("occlu") || n.Contains("occul") || n == "_ao") { if (!occT) occT = t; }
                    else if (n.Contains("emissi")) { if (!emisT) emisT = t; }
                    else if (n.Contains("basecolor") || n.Contains("albedo") || n.Contains("maintex") || n.Contains("basemap") || n.Contains("diffuse") || n.Contains("color")) { if (!baseT || n == "_basecolormap") baseT = t; }
                }
                var cols = so.FindProperty("m_SavedProperties.m_Colors");
                for (int i = 0; cols != null && i < cols.arraySize; i++)
                {
                    var e = cols.GetArrayElementAtIndex(i); string n = e.FindPropertyRelative("first").stringValue; Color c = e.FindPropertyRelative("second").colorValue;
                    if (n == "_BaseColor") { baseC = c; gotBase = true; } else if (n == "_Color" && !gotBase) baseC = c; else if (n == "_EmissiveColor") emisC = c;
                }
                var fl = so.FindProperty("m_SavedProperties.m_Floats");
                for (int i = 0; fl != null && i < fl.arraySize; i++)
                {
                    var e = fl.GetArrayElementAtIndex(i); string n = e.FindPropertyRelative("first").stringValue; float v = e.FindPropertyRelative("second").floatValue;
                    if (n == "_Metallic") metal = v; else if (n == "_Smoothness") smooth = v; else if (n == "_SmoothnessRemapMax") smoothMax = v; else if (n == "_SurfaceType") surface = v; else if (n == "_AlphaCutoffEnable") cutoff = v; else if (n == "_AlphaCutoff") cut = v;
                }
                m.shader = lit;
                m.SetColor("_BaseColor", baseC); if (baseT) m.SetTexture("_BaseMap", baseT);
                if (normT) { m.SetTexture("_BumpMap", normT); m.EnableKeyword("_NORMALMAP"); }
                if (maskT) { m.SetTexture("_MetallicGlossMap", maskT); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetTexture("_OcclusionMap", maskT); m.EnableKeyword("_OCCLUSIONMAP"); m.SetFloat("_Smoothness", smoothMax >= 0f ? Mathf.Clamp01(smoothMax) : 1f); m.SetFloat("_Metallic", 1f); }
                else if (metalT) { m.SetTexture("_MetallicGlossMap", metalT); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 1f); m.SetFloat("_Metallic", 1f); }
                else { m.SetFloat("_Metallic", Mathf.Clamp01(metal)); m.SetFloat("_Smoothness", Mathf.Clamp01(smooth)); }
                if (occT && !maskT) { m.SetTexture("_OcclusionMap", occT); m.EnableKeyword("_OCCLUSIONMAP"); }
                float peak = Mathf.Max(emisC.r, Mathf.Max(emisC.g, emisC.b));
                if (peak > 0.001f || emisT)
                {
                    if (peak > 3f) emisC *= 3f / peak;      // HDRP stores glow in nits; URP wants a small multiplier
                    if (peak <= 0.001f) emisC = Color.white;
                    m.SetColor("_EmissionColor", emisC); if (emisT) m.SetTexture("_EmissionMap", emisT); m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                if (surface > 0.5f)
                {
                    m.SetFloat("_Surface", 1f); m.SetOverrideTag("RenderType", "Transparent"); m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                else if (cutoff > 0.5f) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", cut); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; }
                EditorUtility.SetDirty(m); done++;
                sb.AppendLine("converted " + Path.GetFileName(path) + "  (was " + was + ")  base=" + (baseT ? baseT.name : "-") + " normal=" + (normT ? "y" : "-") + " mask=" + (maskT ? "y" : "-") + " metal=" + (metalT ? "y" : "-") + " occ=" + (occT ? "y" : "-") + " glow=" + (emisT ? "y" : "-") + (surface > 0.5f ? " transparent" : "") + "  [" + names + "]");
            }
            AssetDatabase.SaveAssets();
            sb.Insert(0, "materials " + total + ", converted " + done + "\n"); sb.AppendLine("OK");
        }
        catch (Exception e) { sb.AppendLine("FAILED " + e); }
        File.WriteAllText(Path.Combine(Bridge, "factory_convert.txt"), sb.ToString());
    }

    static Bounds Measure(GameObject g, out int renderers, out int tris)
    {
        var b = new Bounds(g.transform.position, Vector3.zero); renderers = 0; tris = 0; bool first = true;
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue; renderers++;
            var mf = r.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) tris += mf.sharedMesh.triangles.Length / 3;
            var sk = r as SkinnedMeshRenderer; if (sk && sk.sharedMesh) tris += sk.sharedMesh.triangles.Length / 3;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    [MenuItem("HUNCH/Factory pack: list pieces")]
    public static void Report()
    {
        var sb = new StringBuilder();
        try
        {
            foreach (string kind in new[] { "t:Prefab", "t:Model" })
            {
                sb.AppendLine("--- " + kind);
                foreach (var guid in AssetDatabase.FindAssets(kind, new[] { Src }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid); var g = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!g) continue;
                    int rn, tr; var b = Measure(g, out rn, out tr);
                    sb.AppendLine(path.Substring(Src.Length + 1) + " | size " + b.size.x.ToString("F1") + " x " + b.size.y.ToString("F1") + " x " + b.size.z.ToString("F1") + " | renderers " + rn + " | tris " + tr + " | scripts " + g.GetComponentsInChildren<MonoBehaviour>(true).Length + " | anim " + (g.GetComponentInChildren<Animator>(true) ? "y" : "-"));
                }
            }
            long texBytes = 0; int texN = 0; foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Src })) { var fi = new FileInfo(AssetDatabase.GUIDToAssetPath(guid)); if (fi.Exists) { texBytes += fi.Length; texN++; } }
            sb.AppendLine("--- textures " + texN + ", " + (texBytes >> 20) + " MB on disk"); sb.AppendLine("OK");
        }
        catch (Exception e) { sb.AppendLine("FAILED " + e); }
        File.WriteAllText(Path.Combine(Bridge, "factory_kit.txt"), sb.ToString());
    }

    [MenuItem("HUNCH/Factory pack: build the kit")]
    public static void Build()
    {
        var sb = new StringBuilder();
        try
        {
            var root = new GameObject("HunchFactoryKit"); var texSeen = new HashSet<string>();
            foreach (var line in File.ReadAllLines(Path.Combine(Bridge, "factory_pick.txt")))
            {
                string rel = line.Trim(); if (rel.Length == 0) continue; string path = Src + "/" + rel; var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!src) { sb.AppendLine("missing " + rel); continue; }
                var g = (GameObject)PrefabUtility.InstantiatePrefab(src); PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                g.name = Path.GetFileNameWithoutExtension(rel); g.transform.SetParent(root.transform, false); g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity;
                foreach (var t in g.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true)) if (mb) UnityEngine.Object.DestroyImmediate(mb);
                foreach (var c in g.GetComponentsInChildren<Component>(true)) if (c is Light || c is Camera || c is AudioListener || c is ReflectionProbe || c is AudioSource || c is Rigidbody) UnityEngine.Object.DestroyImmediate(c);
                foreach (var r in g.GetComponentsInChildren<Renderer>(true)) foreach (var m in r.sharedMaterials)
                {
                    if (!m) continue;
                    foreach (string prop in m.GetTexturePropertyNames())
                    {
                        var t = m.GetTexture(prop); if (!t) continue; string tp = AssetDatabase.GetAssetPath(t); if (!texSeen.Add(tp)) continue;
                        var imp = AssetImporter.GetAtPath(tp) as TextureImporter; if (imp && imp.maxTextureSize > 1024) { imp.maxTextureSize = 1024; imp.SaveAndReimport(); }      // keeps the game's download small
                    }
                }
                int rn, tr; var b = Measure(g, out rn, out tr); sb.AppendLine("kit " + g.name + " | size " + b.size.x.ToString("F1") + " x " + b.size.y.ToString("F1") + " x " + b.size.z.ToString("F1") + " | centre " + b.center.ToString("F1") + " | renderers " + rn + " | tris " + tr);
            }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/HunchFactoryKit.prefab"); UnityEngine.Object.DestroyImmediate(root);
            sb.AppendLine("textures capped " + texSeen.Count); sb.AppendLine("OK");
        }
        catch (Exception e) { sb.AppendLine("FAILED " + e); }
        File.WriteAllText(Path.Combine(Bridge, "factory_build.txt"), sb.ToString());
        AssetDatabase.Refresh();
    }
}
