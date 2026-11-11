// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchFxSetup.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Makes imported effect packs usable in this URP project: 1. converts their old built-
//            in-pipeline particle materials to URP (otherwise they render pink)
//  Contains: HunchFxSetup
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Makes imported effect packs usable in this URP project:
///  1. converts their old built-in-pipeline particle materials to URP (otherwise they render pink)
///  2. collects the effect prefabs the game uses into Assets/HUNCH/Resources/HunchFxLibrary.asset
/// Runs by itself the first time a pack is found, or from the menu HUNCH > Use imported effect packs.
public static class HunchFxSetup
{
    const string LibPath = "Assets/HUNCH/Resources/HunchFxLibrary.asset";
    static readonly string[] PackFolders = { "Assets/Hovl Studio" };
    static readonly string[] Wanted = {
        "dust puff", "dust ground", "dust loop", "dust hemisphere loop", "smoke loop", "smoke puff", "smoke aoe explosion", "ground aoe explosion",
        "healing", "healing circle", "love aura", "love hit", "star aura", "star hit", "holy hit", "buff", "plexus", "magic shield blue",
        "sparks flashing yellow", "sparks flashing red", "sparks explode white", "sparks explode green", "sparks explode yellow", "sparks explode blue",
        "sparks green", "sparks yellow", "electro hit", "stones hit", "teleport",
        "explosion", "magic fire pro blue", "magic fire pro orange", "magic fire pro green", "magic fire pro red", "magic fire pro yellow" };      // the last row is Hovl Studio "Procedural fire"

    [InitializeOnLoadMethod]
    static void Auto()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string sig = string.Join("|", ExtraFolders().ToArray());
            bool newPack = sig != EditorPrefs.GetString("hunch_packs_" + Application.dataPath.GetHashCode(), "");
            if ((Folders().Count > 0 && AssetDatabase.LoadAssetAtPath<HunchFxLibrary>(LibPath) == null) || newPack) Build();
        };
    }

    // every other folder at the top of Assets is treated as an imported pack: its guns and particle effects are offered to the game
    static readonly string[] Known = { "CompositionLayers", "Hovl Studio", "HUNCH", "Resources", "Samples", "Scenes", "Scripts", "Settings", "TextMesh Pro", "VRTemplateAssets", "XR", "XRI", "StreamingAssets", "Plugins", "Editor", "Editor Default Resources", "Gizmos", "AddressableAssetsData" };
    static readonly string[] GunWords = { "gun", "rifle", "pistol", "blaster", "weapon", "laser", "shotgun", "smg", "revolver", "cannon", "launcher", "carbine", "sniper" };
    static readonly string[] NotGun = { "bullet", "ammo", "magazine", "shell", "muzzle", "projectile", "clip", "scope", "casing", "crosshair", "icon", "demo", "target", "impact" };
    static List<string> ExtraFolders()
    {
        var l = new List<string>();
        foreach (var d in Directory.GetDirectories(Application.dataPath)) { string n = Path.GetFileName(d); if (System.Array.IndexOf(Known, n) < 0 && !n.StartsWith(".")) l.Add("Assets/" + n); }
        l.Sort(); return l;
    }

    static List<string> Folders() { var l = new List<string>(); foreach (var f in PackFolders) if (AssetDatabase.IsValidFolder(f)) l.Add(f); return l; }

    [MenuItem("HUNCH/Use imported effect packs")]
    public static void Build()
    {
        var folders = Folders(); var extra = ExtraFolders();
        EditorPrefs.SetString("hunch_packs_" + Application.dataPath.GetHashCode(), string.Join("|", extra.ToArray()));
        if (folders.Count == 0 && extra.Count == 0) { Debug.Log("[HUNCH] no effect pack folder found"); return; }
        // Hovl "Procedural fire": the fire-ball materials use an old shader; the pack ships a URP version (a shader graph) beside it
        int swapped = 0; var fireGraph = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Hovl Studio/Procedural fire/Shaders/FireSphere.shadergraph");
        if (fireGraph && AssetDatabase.IsValidFolder("Assets/Hovl Studio/Procedural fire"))
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Hovl Studio/Procedural fire" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!m || !m.shader || m.shader == fireGraph || !m.shader.name.Contains("FireSphere")) continue;
                m.shader = fireGraph; EditorUtility.SetDirty(m); swapped++;
            }
        string report = folders.Count > 0 ? ConvertMaterials(folders.ToArray()) : "";
        if (swapped > 0) report += " Fire materials switched to the URP shader graph: " + swapped + ".";

        var lib = AssetDatabase.LoadAssetAtPath<HunchFxLibrary>(LibPath);
        if (!lib)
        {
            Directory.CreateDirectory("Assets/HUNCH/Resources");
            lib = ScriptableObject.CreateInstance<HunchFxLibrary>();
            AssetDatabase.CreateAsset(lib, LibPath);
        }
        lib.names.Clear(); lib.prefabs.Clear();
        var want = new HashSet<string>(Wanted);
        if (folders.Count > 0) foreach (var guid in AssetDatabase.FindAssets("t:Prefab", folders.ToArray()))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid), name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (!want.Contains(name) || lib.names.Contains(name)) continue;
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p) { lib.names.Add(name); lib.prefabs.Add(p); }
        }
        // ---- other imported packs: guns for the hand, and their particle effects
        var found = new System.Text.StringBuilder(); int guns = 0, fxs = 0;
        if (extra.Count > 0)
        {
            report += " Other packs (" + string.Join(", ", extra.ToArray()) + "): " + ConvertMaterials(extra.ToArray());
            // a pack's ready-made prefabs come first; its raw model files are only used when it has no gun prefab
            var paths = new List<string>(); foreach (var guid in AssetDatabase.FindAssets("t:GameObject", extra.ToArray())) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort((a, b) => (a.EndsWith(".prefab") ? 0 : 1).CompareTo(b.EndsWith(".prefab") ? 0 : 1));
            var packsWithGun = new HashSet<string>();
            foreach (var path in paths)
            {
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant(), lower = path.ToLowerInvariant();
                string pack = path.Split('/').Length > 1 ? path.Split('/')[1] : "";
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!p) continue;
                bool particles = p.GetComponentInChildren<ParticleSystem>(true) != null;
                bool mesh = p.GetComponentInChildren<MeshFilter>(true) != null || p.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
                bool gunWord = false, notGun = false;
                foreach (var w in GunWords) if (lower.Contains(w)) gunWord = true;
                foreach (var w in NotGun) if (name.Contains(w)) notGun = true;
                if (mesh && !particles && gunWord && !notGun && guns < 8 && !lib.names.Contains("gun:" + name))
                {
                    if (!path.EndsWith(".prefab") && packsWithGun.Contains(pack)) continue;
                    long tris = 0; foreach (var mf in p.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh) for (int sm = 0; sm < mf.sharedMesh.subMeshCount; sm++) tris += mf.sharedMesh.GetIndexCount(sm) / 3;
                    packsWithGun.Add(pack); lib.names.Add("gun:" + name); lib.prefabs.Add(p); guns++; found.AppendLine("gun   " + path + "   " + tris + " triangles"); continue;
                }
                if ((particles || lower.Contains("teleport")) && fxs < 40 && path.EndsWith(".prefab") && !lib.names.Contains(name))
                {
                    lib.names.Add(name); lib.prefabs.Add(p); fxs++; found.AppendLine("fx    " + path);
                    if (name.Contains("teleport") && !lib.names.Contains("pack teleporter")) { lib.names.Add("pack teleporter"); lib.prefabs.Add(p); }
                }
            }
        }
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log("[HUNCH] effect library: " + lib.names.Count + " entries (" + guns + " guns, " + fxs + " pack effects). " + report);
        try { File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge", "packs.txt"), "folders: " + string.Join(", ", extra.ToArray()) + "\n" + found + "guns " + guns + " effects " + fxs + "\n" + report + "\n"); } catch { }
    }

    static float F(Material m, string p, float d) { return m.HasProperty(p) ? m.GetFloat(p) : d; }

    static string ConvertMaterials(string[] folders)
    {
        var unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var plit = Shader.Find("Universal Render Pipeline/Particles/Lit");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (!unlit || !plit || !lit) return "URP shaders not found, materials left alone.";
        int done = 0, already = 0; var skipped = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m || !m.shader) continue;
            string sn = m.shader.name;
            if (sn.StartsWith("Universal Render Pipeline") || sn.StartsWith("Shader Graphs")) { already++; continue; }
            bool legacy = sn.StartsWith("Legacy Shaders/Particles") || sn.StartsWith("Mobile/Particles");
            bool particle = sn.StartsWith("Particles/") || legacy;
            bool standard = sn == "Standard" || sn == "Standard (Specular setup)" || (sn.StartsWith("Legacy Shaders/") && !legacy) || sn.StartsWith("Mobile/Diffuse") || sn.StartsWith("Mobile/Bumped");      // old solid-surface shaders become URP Lit
            if (!particle && !standard) { skipped.Add(m.name + " (" + sn + ")"); continue; }

            // ---- remember the old look
            Texture tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Vector2 ts = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one, to = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : (m.HasProperty("_TintColor") ? m.GetColor("_TintColor") * 2f : Color.white);
            float mode = F(m, "_Mode", legacy ? (sn.Contains("Additive") ? 4f : 2f) : (sn.Contains("Transparent/Cutout") ? 1f : (sn.Contains("Transparent/") ? 2f : 0f)));
            float src = F(m, "_SrcBlend", legacy ? 5f : 1f), dst = F(m, "_DstBlend", legacy ? (sn.Contains("Additive") ? 1f : 10f) : 0f);
            float cull = F(m, "_Cull", particle ? 0f : 2f), colorMode = F(m, "_ColorMode", 0f), flip = F(m, "_FlipbookMode", 0f);
            float gloss = F(m, "_Glossiness", 0.5f), glossScale = F(m, "_GlossMapScale", 1f);
            bool emission = m.IsKeywordEnabled("_EMISSION") || F(m, "_EmissionEnabled", 0f) > 0.5f;
            bool normal = m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap");
            bool metalMap = m.HasProperty("_MetallicGlossMap") && m.GetTexture("_MetallicGlossMap");
            bool distortion = m.IsKeywordEnabled("EFFECT_BUMP") || F(m, "_DistortionEnabled", 0f) > 0.5f;
            bool lighting = sn == "Particles/Standard Surface";
            bool transparent = standard ? mode >= 2f : (mode >= 2f || legacy), clip = Mathf.Approximately(mode, 1f);

            // ---- switch shader and carry the look over
            m.shader = standard ? lit : (lighting ? plit : unlit);
            m.shaderKeywords = new string[0];
            m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", ts); m.SetTextureOffset("_BaseMap", to);
            if (distortion) col.a = 0f;                      // screen-distortion particles need extra URP settings; hide them instead of showing a wrong picture
            m.SetColor("_BaseColor", col);
            float blend = 0f;                                // 0 alpha, 1 premultiplied, 2 additive, 3 multiply
            if (Mathf.Approximately(dst, 1f)) blend = 2f; else if (Mathf.Approximately(src, 1f) && transparent) blend = 1f; else if (Mathf.Approximately(src, 2f)) blend = 3f;
            m.SetFloat("_Surface", transparent ? 1f : 0f); m.SetFloat("_Blend", blend); m.SetFloat("_AlphaClip", clip ? 1f : 0f);
            m.SetFloat("_SrcBlend", transparent ? src : 1f); m.SetFloat("_DstBlend", transparent ? dst : 0f);
            m.SetFloat("_SrcBlendAlpha", 1f); m.SetFloat("_DstBlendAlpha", transparent ? (blend == 2f ? 1f : 10f) : 0f);
            m.SetFloat("_ZWrite", transparent ? 0f : 1f); m.SetFloat("_Cull", cull);
            if (transparent) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (clip) m.EnableKeyword("_ALPHATEST_ON");
            if (blend == 1f) m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            if (blend == 3f) m.EnableKeyword("_ALPHAMODULATE_ON");
            if (emission) m.EnableKeyword("_EMISSION");
            if (normal && (standard || lighting)) m.EnableKeyword("_NORMALMAP");
            if (metalMap && (standard || lighting)) m.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", metalMap ? glossScale : gloss);
            if (particle)
            {
                m.SetFloat("_ColorMode", colorMode); m.SetFloat("_FlipbookBlending", flip);
                m.SetFloat("_SoftParticlesEnabled", 0f); m.SetFloat("_CameraFadingEnabled", 0f); m.SetFloat("_DistortionEnabled", 0f);
                if (flip > 0.5f) m.EnableKeyword("_FLIPBOOKBLENDING_ON");
                if (Mathf.Approximately(colorMode, 3f)) m.EnableKeyword("_COLOROVERLAY_ON");
                else if (Mathf.Approximately(colorMode, 4f)) m.EnableKeyword("_COLORCOLOR_ON");
                else if (colorMode > 0.5f) m.EnableKeyword("_COLORADDSUBDIFF_ON");
            }
            m.SetOverrideTag("RenderType", transparent ? "Transparent" : (clip ? "TransparentCutout" : "Opaque"));
            m.renderQueue = transparent ? 3000 : (clip ? 2450 : 2000);
            EditorUtility.SetDirty(m);
            done++;
        }
        AssetDatabase.SaveAssets();
        return "Materials converted to URP: " + done + ", already URP: " + already + (skipped.Count > 0 ? ", left alone: " + string.Join(", ", skipped) : "") + ".";
    }
}
