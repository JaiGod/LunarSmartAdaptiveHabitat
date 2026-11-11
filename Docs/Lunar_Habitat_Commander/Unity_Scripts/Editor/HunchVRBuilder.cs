// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchVRBuilder.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Builds the HUNCH Lunar Dome VR Explorer scene from the Blender export. Menu: HUNCH >
//            ...
//  Contains: HunchVRBuilder, JStation, JStations
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Builds the HUNCH Lunar Dome VR Explorer scene from the Blender export. Menu: HUNCH > ...
public static class HunchVRBuilder
{
    const string Src = @"C:\Users\jaine\Downloads\Nasa\official projects for nasa\unity1\HUNCH_Base_Unity";
    const string PreviewDir = @"C:\Users\jaine\Downloads\Nasa\official projects for nasa\unity2\previews";
    const string FbxSource = @"C:\Users\jaine\Downloads\Nasa\official projects for nasa\unity2\Model_Export\HUNCH_Lunar_Base_x2.fbx";
    const float S = 2.0f;                // how much the Blender model was enlarged; light positions below are in the original coordinates
    const string Root = "Assets/HUNCH";
    const string FbxPath = Root + "/Model/HUNCH_Lunar_Base.fbx";
    const string ScenePath = Root + "/Scenes/Lunar_Habitat_Commander.unity";
    const string RigPrefab = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Hands Variant.prefab";

    [Serializable] class JStation { public string id; public string rubric; public string title; public string description; public float[] pos; public float[] look; public bool platform; }
    [Serializable] class JStations { public JStation[] stations; }

    static void Log(string s) { Debug.Log("[HUNCH] " + s); }
    static Vector3 B2U(float x, float y, float z) { return new Vector3(x, z, y); } // Blender (Z up) -> Unity (Y up)
    static Vector3 B2U(float[] p) { return B2U(p[0], p[1], p[2]); }

    public static void BuildAll() { Run(() => { ImportAssets(); BuildMaterials(); BuildScene(); RenderPreviews(); }); }
    public static void SceneOnly() { Run(() => { BuildMaterials(); BuildScene(); RenderPreviews(); }); }
    public static void SceneAndPlayer() { Run(() => { BuildMaterials(); BuildScene(); RenderPreviews(); BuildPlayerInternal(); }); }
    [MenuItem("HUNCH/Build Windows app (.exe)")] public static void BuildPlayer() { Run(BuildPlayerInternal); }
    static void BuildPlayerInternal()
    {
        string outDir = @"C:\Users\jaine\Downloads\Nasa\official projects for nasa\unity2\Lunar_Habitat_Commander_Build";
        Directory.CreateDirectory(outDir);
        // Build what is saved on disk. A stray unsaved edit in the open scene would otherwise stop the build with a "save changes?" pop-up.
        var open = EditorSceneManager.GetActiveScene();
        if (open.isDirty && !EditorApplication.isPlaying) { Log("open scene had unsaved edits: discarded, building the saved scene"); EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); }
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x); // IL2CPP module is not installed on this PC
        var opts = new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = Path.Combine(outDir, "Lunar_Habitat_Commander.exe"), target = BuildTarget.StandaloneWindows64, options = BuildOptions.None };
        var rep = BuildPipeline.BuildPlayer(opts);
        Log("player build " + rep.summary.result + ", " + (rep.summary.totalSize / 1000000UL) + " MB, errors " + rep.summary.totalErrors);
        if (rep.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("player build failed");
    }
    public static void PreviewsOnly() { Run(() => { EditorSceneManager.OpenScene(ScenePath); RenderPreviews(); }); }

    [MenuItem("HUNCH/Rebuild everything (re-import model + textures)")] static void MenuAll() { BuildAll(); }
    [MenuItem("HUNCH/Rebuild scene only")] static void MenuScene() { SceneOnly(); }

    static void Run(Action a)
    {
        try { a(); Log("DONE OK"); }
        catch (Exception e)
        {
            Debug.LogError("[HUNCH] FAILED " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    // ------------------------------------------------------------------ import
    static void ImportAssets()
    {
        foreach (var d in new[] { "Model", "Textures", "Sky", "Materials", "Scenes", "Settings" }) Directory.CreateDirectory(Root + "/" + d);
        CopyIfNewer(FbxSource, FbxPath);     // the building enlarged to 2x in Blender so rooms, doors and corridors are roomier
        CopyIfNewer(Path.Combine(Src, "Sky", "Starfield_Equirect_4k.png"), Root + "/Sky/Starfield_Equirect_4k.png");
        string tex = Path.Combine(Src, "Textures");
        CopyIfNewer(Path.Combine(tex, "Regolith_Detail_Normal.png"), Root + "/Textures/Regolith_Detail_Normal.png");
        int n = 0, emit = 0;
        foreach (var c in Directory.GetFiles(tex, "*_Color.png"))
        {
            string name = Path.GetFileName(c).Replace("_Color.png", "");
            if (name == "EARTH") { CopyIfNewer(Path.Combine(tex, "EARTH_Emit.png"), Root + "/Textures/EARTH_Emit.png"); continue; }
            CopyIfNewer(c, Root + "/Textures/" + name + "_Color.png");
            string e = Path.Combine(tex, name + "_Emit.png");
            if (File.Exists(e) && new FileInfo(e).Length > 60000) { CopyIfNewer(e, Root + "/Textures/" + name + "_Emit.png"); emit++; }
            string ms = Root + "/Textures/" + name + "_MS.png";
            string m = Path.Combine(tex, name + "_Metallic.png"), r = Path.Combine(tex, name + "_Roughness.png");
            if (File.Exists(m) && File.Exists(r) && (!File.Exists(ms) || File.GetLastWriteTimeUtc(ms) < File.GetLastWriteTimeUtc(r))) PackMS(m, r, ms);
            n++;
        }
        Log("copied " + n + " texture sets, " + emit + " with emission");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Log("asset import finished");
    }

    static void CopyIfNewer(string src, string dst)
    {
        if (!File.Exists(src)) { Log("missing " + src); return; }
        if (File.Exists(dst) && File.GetLastWriteTimeUtc(dst) >= File.GetLastWriteTimeUtc(src) && new FileInfo(dst).Length == new FileInfo(src).Length) return;
        File.Copy(src, dst, true);
    }

    // metallic -> RGB, smoothness (1 - roughness) -> A, the layout the URP Lit shader expects
    static void PackMS(string metalPath, string roughPath, string dst)
    {
        var tm = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        var tr = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        tm.LoadImage(File.ReadAllBytes(metalPath));
        tr.LoadImage(File.ReadAllBytes(roughPath));
        var pm = tm.GetPixels32(); var pr = tr.GetPixels32();
        if (pm.Length == pr.Length)
            for (int i = 0; i < pm.Length; i++) { byte mv = pm[i].r; pm[i] = new Color32(mv, mv, mv, (byte)(255 - pr[i].r)); }
        tm.SetPixels32(pm);
        File.WriteAllBytes(dst, tm.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tm); UnityEngine.Object.DestroyImmediate(tr);
    }

    // ------------------------------------------------------------------ materials
    static Texture2D Tex(string file) { return AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + file); }

    static Material GetOrCreate(string name, string shader)
    {
        string p = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        var sh = Shader.Find(shader);
        if (m == null) { m = new Material(sh) { name = name }; AssetDatabase.CreateAsset(m, p); }
        else m.shader = sh;
        return m;
    }

    static void MakeTransparent(Material m, Color c, float smooth)
    {
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_AlphaClip", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f); m.SetFloat("_CastShadows", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHATEST_ON");
        m.SetShaderPassEnabled("DepthOnly", false); m.SetShaderPassEnabled("ShadowCaster", false);
        m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
    }

    static Material MaterialFor(string src)
    {
        if (src == "Glass") { var g = GetOrCreate("HUNCH_Glass", "Universal Render Pipeline/Lit"); MakeTransparent(g, new Color(0.55f, 0.75f, 0.9f, 0.08f), 0.93f); g.SetFloat("_EnvironmentReflections", 0f); g.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); EditorUtility.SetDirty(g); return g; }
        if (src == "Water") { var w = GetOrCreate("HUNCH_Water", "Universal Render Pipeline/Lit"); MakeTransparent(w, new Color(0.08f, 0.42f, 0.65f, 0.42f), 0.92f); return w; }
        string key = src.StartsWith("BAKED_") ? src.Substring(6) : src;
        if (key == "EARTH")
        {
            var e = GetOrCreate("HUNCH_Earth", "Universal Render Pipeline/Unlit");
            e.SetTexture("_BaseMap", Tex("EARTH_Emit.png")); e.SetColor("_BaseColor", Color.white * 1.6f); e.SetFloat("_Cull", 2f);
            return e;
        }
        var m = GetOrCreate("HUNCH_" + key, "Universal Render Pipeline/Lit");
        var col = Tex(key + "_Color.png"); var ms = Tex(key + "_MS.png"); var em = Tex(key + "_Emit.png");
        m.SetColor("_BaseColor", Color.white);
        if (col) m.SetTexture("_BaseMap", col); else Log("no colour map for " + src);
        bool ground = key == "TERRAIN" || key == "KINGDOM_Ground" || key == "ROCKS";
        if (ground) { m.SetTexture("_MetallicGlossMap", null); m.DisableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.18f); } // regolith is dusty rock, not metal
        else if (ms)
        {
            m.SetTexture("_MetallicGlossMap", ms); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.SetFloat("_SmoothnessTextureChannel", 0f);
        }
        else { m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.3f); }
        if (em)
        {
            m.SetTexture("_EmissionMap", em); m.SetColor("_EmissionColor", Color.white * 1.15f);
            m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
        m.SetFloat("_Cull", 0f); m.doubleSidedGI = true; // Blender shows both sides of thin walls
        if (key == "TERRAIN" || key == "KINGDOM_Ground" || key == "ROCKS")
        {
            var dn = Tex("Regolith_Detail_Normal.png");
            if (dn)
            {
                m.SetTexture("_DetailNormalMap", dn); m.SetFloat("_DetailNormalMapScale", 0.45f);
                m.SetTextureScale("_DetailAlbedoMap", key == "TERRAIN" ? new Vector2(90f, 90f) : new Vector2(40f, 40f));
                m.SetFloat("_DetailAlbedoMapScale", 1f); m.EnableKeyword("_DETAIL_MULX2");
            }
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void BuildMaterials()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
        if (importer == null) throw new Exception("model not imported: " + FbxPath);
        var names = new HashSet<string>(AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Material>().Select(m => m.name));
        foreach (var kv in importer.GetExternalObjectMap()) if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);
        Log("source materials: " + names.Count + " -> " + string.Join(", ", names.OrderBy(x => x)));
        foreach (var n in names)
        {
            var m = MaterialFor(n);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), m);
        }
        AssetDatabase.SaveAssets();
        importer.SaveAndReimport();
        Log("materials remapped");
    }

    // ------------------------------------------------------------------ scene
    static Type FindType(string name)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] ts; try { ts = a.GetTypes(); } catch { continue; }
            foreach (var t in ts) if (t.Name == name && typeof(MonoBehaviour).IsAssignableFrom(t) && !t.IsAbstract) return t;
        }
        return null;
    }

    static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material m, string name)
    {
        var g = GameObject.CreatePrimitive(type); g.name = name;
        UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = localPos; g.transform.localScale = localScale;
        var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
        return g;
    }

    static Light PointLight(Transform parent, string name, Vector3 pos, Color c, float intensity, float range)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = pos * S;
        var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.intensity = intensity * S * S; l.range = range * S; l.shadows = LightShadows.None;
        return l;
    }

    static JStations LoadStations() { return JsonUtility.FromJson<JStations>(File.ReadAllText(Root + "/stations.json")); }

    static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory(Root + "/Scenes"); Directory.CreateDirectory(Root + "/Settings");

        // ---- model
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        model.name = "HUNCH_Lunar_Base";
        int tris = 0; var colliders = new List<Collider>();
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.sharedMesh) continue;
            for (int sm = 0; sm < mf.sharedMesh.subMeshCount; sm++) tris += (int)(mf.sharedMesh.GetIndexCount(sm) / 3);
            var r = mf.GetComponent<MeshRenderer>();
            string n = mf.name;
            if (n.Contains("EARTH")) { if (r) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; } continue; }
            if (n.Contains("DETAILS")) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh;
            colliders.Add(mc);
        }
        foreach (var n in new[] { "BAKED_KINGDOM_Spire", "BAKED_SPACEPORT", "BAKED_TELECOM", "BAKED_CORE_Shell", "BAKED_EARTH", "BAKED_TERRAIN" })
        {
            var t = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n);
            var r = t ? t.GetComponent<Renderer>() : null;
            Log("bounds " + n + " : " + (r ? r.bounds.center.ToString("F1") + " size " + r.bounds.size.ToString("F1") : "NOT FOUND"));
        }
        Log("triangles " + tris); Log("model objects " + model.GetComponentsInChildren<MeshFilter>(true).Length + ", colliders " + colliders.Count);

        var data = LoadStations();

        // ---- observation platforms for stations that float in the air
        var glass = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/HUNCH_Glass.mat");
        var plat = GetOrCreate("HUNCH_Platform", "Universal Render Pipeline/Lit");
        MakeTransparent(plat, new Color(0.3f, 0.8f, 1f, 0.07f), 0.5f); plat.SetFloat("_EnvironmentReflections", 0f); plat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        plat.SetColor("_EmissionColor", new Color(0.1f, 0.5f, 0.8f) * 0.05f); plat.EnableKeyword("_EMISSION"); plat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        var extras = new GameObject("HUNCH Extras");
        foreach (var s in data.stations.Where(s => s.platform))
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p.name = "Observation Platform"; p.transform.SetParent(extras.transform, false);
            UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.position = B2U(s.pos) + Vector3.down * 0.06f; p.transform.localScale = new Vector3(7f, 0.05f, 7f);
            p.GetComponent<Renderer>().sharedMaterial = plat; p.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var bc = p.AddComponent<BoxCollider>(); bc.size = new Vector3(1f, 2f, 1f);
            colliders.Add(bc);
        }

        // ---- teleport anywhere that is roughly flat
        var taType = FindType("TeleportationArea");
        if (taType != null)
        {
            var area = model.AddComponent(taType);
            var so = new SerializedObject(area);
            var layers = so.FindProperty("m_InteractionLayers.m_Bits"); if (layers != null) layers.longValue = 2147483648L; else Log("no interaction layer property");
            var cols = so.FindProperty("m_Colliders");
            if (cols != null) { cols.arraySize = colliders.Count; for (int i = 0; i < colliders.Count; i++) cols.GetArrayElementAtIndex(i).objectReferenceValue = colliders[i]; }
            var f = so.FindProperty("m_FilterSelectionByHitNormal"); if (f != null) f.boolValue = true; else Log("no hit-normal filter property");
            var tol = so.FindProperty("m_UpNormalToleranceDegrees"); if (tol != null) tol.floatValue = 35f;
            so.ApplyModifiedPropertiesWithoutUndo();
            Log("teleport area added (" + taType.FullName + ")");
        }
        else Log("WARNING TeleportationArea type not found");

        // ---- lights
        var lights = new GameObject("HUNCH Lights");
        var sunGo = new GameObject("Sun"); sunGo.transform.SetParent(lights.transform, false);
        var sun = sunGo.AddComponent<Light>(); sun.type = LightType.Directional; sun.color = new Color(1f, 0.97f, 0.92f); sun.intensity = 2.5f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.92f;
        Vector3 toSun = B2U(-0.84f, 0.49f, 0.22f).normalized;
        sunGo.transform.rotation = Quaternion.LookRotation(-toSun);
        RenderSettings.sun = sun;
        Color warm = new Color(1f, 0.86f, 0.66f), cool = new Color(0.75f, 0.9f, 1f), white = new Color(1f, 0.96f, 0.9f);
        float[] floors = { 0.35f, 4.2f, 8.4f, 12.6f, 16.8f };
        for (int i = 0; i < floors.Length; i++)
        {
            PointLight(lights.transform, "Hub Floor " + (i + 1) + " A", B2U(6f, 2f, floors[i] + 3f), i >= 2 ? cool : warm, 9f, 16f);
            PointLight(lights.transform, "Hub Floor " + (i + 1) + " B", B2U(-6f, -2f, floors[i] + 3f), i >= 2 ? cool : warm, 9f, 16f);
        }
        float[] ang = { 0f, 60f, 120f, 240f, 300f }; string[] mod = { "Logistics", "Science", "Medical", "Greenhouse", "Utility" };
        for (int i = 0; i < ang.Length; i++)
        {
            float a = ang[i] * Mathf.Deg2Rad;
            PointLight(lights.transform, mod[i] + " Light", B2U(32f * Mathf.Cos(a), 32f * Mathf.Sin(a), 3.6f), mod[i] == "Greenhouse" ? new Color(1f, 0.75f, 0.95f) : white, 10f, 15f);
        }
        PointLight(lights.transform, "Crew Deck 1 Light A", B2U(-35f, 0f, 2.6f), warm, 5f, 9f);
        PointLight(lights.transform, "Crew Deck 1 Light B", B2U(-29f, 0f, 2.6f), warm, 5f, 9f);
        PointLight(lights.transform, "Crew Deck 2 Light A", B2U(-35f, 0f, 5.6f), warm, 5f, 9f);
        PointLight(lights.transform, "Crew Deck 2 Light B", B2U(-29f, 0f, 5.6f), warm, 5f, 9f);
        for (int i = 0; i < 6; i++)
        {
            float a = (30f + i * 60f) * Mathf.Deg2Rad;
            PointLight(lights.transform, "Plaza Light " + (i + 1), B2U(50f * Mathf.Cos(a), 50f * Mathf.Sin(a), 16f), warm, 90f, 60f);
        }
        PointLight(lights.transform, "Tower Crown Light", B2U(0f, 0f, 33f), white, 160f, 70f);
        PointLight(lights.transform, "Saucer Light", B2U(0f, 0f, 92f), cool, 120f, 45f);
        float oa = 265f * Mathf.Deg2Rad;
        PointLight(lights.transform, "Ocean Biosphere Light", B2U(105f * Mathf.Cos(oa), 105f * Mathf.Sin(oa), 11f), new Color(0.6f, 0.9f, 1f), 70f, 40f);
        float[] slots = { 40f, 85f, 130f, 175f, 220f, 310f, 355f };
        for (int i = 0; i < slots.Length; i++)
        {
            float a = slots[i] * Mathf.Deg2Rad;
            PointLight(lights.transform, "Keep Light " + (i + 1), B2U(105f * Mathf.Cos(a), 105f * Mathf.Sin(a), 4f), warm, 16f, 18f);
        }
        PointLight(lights.transform, "Spaceport Flood", B2U(175f, 50f, 22f), white, 220f, 80f);
        PointLight(lights.transform, "Telecom Flood", B2U(-170f, 55f, 18f), white, 150f, 60f);

        // ---- sky, ambient, reflections
        var sky = GetOrCreate("HUNCH_StarSky", "Skybox/Cubemap");
        sky.SetTexture("_Tex", BuildStarCube()); sky.SetFloat("_Exposure", 1.25f); sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.064f, 0.075f);
        RenderSettings.fog = false;
        var cube = BuildReflectionCube(toSun);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = cube;
        RenderSettings.reflectionIntensity = 0.6f;

        // ---- post processing
        string profPath = Root + "/Settings/HUNCH_PostFX.asset";
        AssetDatabase.DeleteAsset(profPath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profPath);
        var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(1.1f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.72f);
        var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.Neutral);
        var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(0f); ca.contrast.Override(12f); ca.saturation.Override(6f);
        var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.16f);
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        var volGo = new GameObject("Post FX Volume"); var vol = volGo.AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = profile;
        TuneUrpAssets();

        // ---- player rig
        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab);
        if (rigPrefab == null) throw new Exception("rig prefab missing: " + RigPrefab);
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
        rig.name = "Player (XR Origin)";
        var origin = rig.GetComponentInChildren<XROrigin>(true);
        if (origin == null) throw new Exception("XROrigin not found in rig prefab");

        // ---- dashboard card (VR)
        var hudRoot = new GameObject("HUNCH Dashboard Card");
        var bgMat = GetOrCreate("HUNCH_CardBackground", "Universal Render Pipeline/Unlit");
        MakeTransparent(bgMat, new Color(0.02f, 0.03f, 0.07f, 0.84f), 0f); bgMat.renderQueue = 2990;
        var bg = Prim(PrimitiveType.Quad, hudRoot.transform, new Vector3(0f, 0f, 0.012f), new Vector3(1.04f, 0.64f, 1f), bgMat, "Background");
        var textGo = new GameObject("Text"); textGo.transform.SetParent(hudRoot.transform, false);
        var tmp = textGo.AddComponent<TextMeshPro>();
        tmp.rectTransform.sizeDelta = new Vector2(0.96f, 0.58f);
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.08f; tmp.fontSizeMax = 0.42f; tmp.fontSize = 0.3f;
        tmp.alignment = TextAlignmentOptions.TopLeft; tmp.color = Color.white; tmp.richText = true; tmp.text = "Lunar Habitat Commander";
        hudRoot.SetActive(false);

        // ---- game object
        var gameGo = new GameObject("HUNCH Habitat Game");
        var game = gameGo.AddComponent<HabitatGame>();
        game.origin = origin; game.hud = tmp; game.hudRoot = hudRoot.transform;
        var inside = new List<Light>();
        foreach (var l in lights.GetComponentsInChildren<Light>())
        {
            string n = l.name;
            if (n.StartsWith("Hub Floor") || n.StartsWith("Crew Deck") || n.StartsWith("Logistics") || n.StartsWith("Science") || n.StartsWith("Medical") || n.StartsWith("Greenhouse") || n.StartsWith("Utility")) inside.Add(l);
        }
        game.interiorLights = inside.ToArray();

        // ---- zones: ring on the floor, action orb to point at, label, and a VR teleport pad from the template
        var zonesRoot = new GameObject("HUNCH Zones");
        var ringMat = GetOrCreate("HUNCH_ZoneRing", "Universal Render Pipeline/Unlit"); MakeTransparent(ringMat, new Color(0.2f, 0.8f, 1f, 0.4f), 0f);
        var orbMat = GetOrCreate("HUNCH_ZoneOrb", "Universal Render Pipeline/Unlit"); MakeTransparent(orbMat, new Color(0.3f, 0.9f, 1f, 0.85f), 0f);
        var anchorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VRTemplateAssets/Prefabs/Teleport/Teleport Anchor.prefab");
        if (anchorPrefab == null) Log("WARNING template teleport anchor prefab not found");
        int zi = 0;
        foreach (var s in data.stations)
        {
            Vector3 p = B2U(s.pos), l = B2U(s.look); Vector3 d = l - p; d.y = 0f; d = d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
            float yaw = Quaternion.LookRotation(d).eulerAngles.y;
            var root = new GameObject("Zone " + (zi + 1) + " " + s.title); root.transform.SetParent(zonesRoot.transform, false); root.transform.position = p;
            var ring = Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(1.5f, 0.012f, 1.5f), ringMat, "Ring");
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere); orb.name = "Action Orb"; orb.transform.SetParent(root.transform, false);
            orb.transform.position = p + d * 1.0f + Vector3.up * 1.4f; orb.transform.localScale = Vector3.one * 0.26f;
            orb.GetComponent<Renderer>().sharedMaterial = orbMat; orb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var inter = orb.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            var lab = new GameObject("Label"); lab.transform.SetParent(root.transform, false); lab.transform.position = p + d * 1.0f + Vector3.up * 1.62f;
            var lt = lab.AddComponent<TextMeshPro>(); lt.rectTransform.sizeDelta = new Vector2(4.5f, 1.6f); lt.fontSize = 1.5f; lt.alignment = TextAlignmentOptions.Bottom; lt.richText = true; lt.color = Color.white;
            lt.text = (zi + 1) + "  " + s.title;
            if (anchorPrefab != null)
            {
                var a = (GameObject)PrefabUtility.InstantiatePrefab(anchorPrefab);
                a.name = "Teleport Pad"; a.transform.SetParent(root.transform, true); a.transform.position = p + Vector3.up * 0.02f; a.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            game.zones.Add(new HabZone { id = s.id, title = s.title, rubric = s.rubric, description = s.description, position = p, yaw = yaw, marker = root.transform, ring = ring.GetComponent<Renderer>(), orb = orb.GetComponent<Renderer>(), label = lt, interactable = inter });
            zi++;
        }

        // ---- crew avatars with live biometric tags
        var crewRoot = new GameObject("HUNCH Crew");
        var suit = GetOrCreate("HUNCH_Suit", "Universal Render Pipeline/Lit"); suit.SetColor("_BaseColor", new Color(0.92f, 0.93f, 0.95f)); suit.SetFloat("_Metallic", 0.1f); suit.SetFloat("_Smoothness", 0.45f);
        var visor = GetOrCreate("HUNCH_Visor", "Universal Render Pipeline/Lit"); visor.SetColor("_BaseColor", new Color(1f, 0.76f, 0.28f)); visor.SetFloat("_Metallic", 1f); visor.SetFloat("_Smoothness", 0.92f);
        var dark = GetOrCreate("HUNCH_SuitDark", "Universal Render Pipeline/Lit"); dark.SetColor("_BaseColor", new Color(0.18f, 0.2f, 0.24f)); dark.SetFloat("_Metallic", 0.3f); dark.SetFloat("_Smoothness", 0.4f);
        Color[] stripes = { new Color(1f, 0.3f, 0.25f), new Color(0.3f, 0.75f, 1f), new Color(1f, 0.8f, 0.2f), new Color(0.45f, 1f, 0.5f) };
        var avs = new List<Transform>(); var tags = new List<TextMeshPro>(); var stats = new List<Renderer>();
        for (int k = 0; k < 4; k++)
        {
            var a = new GameObject("Crew " + (k + 1)); a.transform.SetParent(crewRoot.transform, false); a.transform.position = game.zones[Mathf.Min(k, game.zones.Count - 1)].position + new Vector3(0.6f, 0f, 0.6f);
            var sm = GetOrCreate("HUNCH_CrewStripe_" + (k + 1), "Universal Render Pipeline/Unlit"); sm.SetColor("_BaseColor", stripes[k]);
            Prim(PrimitiveType.Capsule, a.transform, new Vector3(0f, 1.08f, 0f), new Vector3(0.46f, 0.36f, 0.34f), suit, "Torso");
            Prim(PrimitiveType.Capsule, a.transform, new Vector3(-0.12f, 0.42f, 0f), new Vector3(0.18f, 0.42f, 0.18f), suit, "Leg L");
            Prim(PrimitiveType.Capsule, a.transform, new Vector3(0.12f, 0.42f, 0f), new Vector3(0.18f, 0.42f, 0.18f), suit, "Leg R");
            Prim(PrimitiveType.Capsule, a.transform, new Vector3(-0.31f, 1.05f, 0f), new Vector3(0.14f, 0.32f, 0.14f), suit, "Arm L");
            Prim(PrimitiveType.Capsule, a.transform, new Vector3(0.31f, 1.05f, 0f), new Vector3(0.14f, 0.32f, 0.14f), suit, "Arm R");
            Prim(PrimitiveType.Sphere, a.transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.36f, 0.36f, 0.36f), suit, "Helmet");
            Prim(PrimitiveType.Sphere, a.transform, new Vector3(0f, 1.61f, 0.085f), new Vector3(0.28f, 0.22f, 0.24f), visor, "Visor");
            Prim(PrimitiveType.Cube, a.transform, new Vector3(0f, 1.12f, -0.2f), new Vector3(0.36f, 0.48f, 0.16f), dark, "Life Support Pack");
            Prim(PrimitiveType.Cube, a.transform, new Vector3(0f, 1.22f, 0.165f), new Vector3(0.3f, 0.06f, 0.02f), sm, "Name Stripe");
            Prim(PrimitiveType.Cube, a.transform, new Vector3(0f, 0.05f, 0.03f), new Vector3(0.42f, 0.1f, 0.3f), dark, "Boots");
            var stm = GetOrCreate("HUNCH_CrewStatus", "Universal Render Pipeline/Unlit"); stm.SetColor("_BaseColor", new Color(0.3f, 1f, 0.45f));
            var st = Prim(PrimitiveType.Sphere, a.transform, new Vector3(0f, 1.92f, 0f), Vector3.one * 0.1f, stm, "Heartbeat Light");
            var tg = new GameObject("Biometric Tag"); tg.transform.SetParent(a.transform, false); tg.transform.localPosition = new Vector3(0f, 2.02f, 0f);
            var tt = tg.AddComponent<TextMeshPro>(); tt.rectTransform.sizeDelta = new Vector2(2.4f, 1.2f); tt.fontSize = 0.8f; tt.alignment = TextAlignmentOptions.Bottom; tt.richText = true; tt.text = "Crew " + (k + 1);
            avs.Add(a.transform); tags.Add(tt); stats.Add(st.GetComponent<Renderer>());
        }
        game.crewAvatars = avs.ToArray(); game.crewTags = tags.ToArray(); game.crewStatus = stats.ToArray();

        // ---- guided breathing sphere shown after calming interventions
        var breathMat = GetOrCreate("HUNCH_Breath", "Universal Render Pipeline/Unlit"); MakeTransparent(breathMat, new Color(0.35f, 0.95f, 0.85f, 0.28f), 0f);
        var breath = new GameObject("Breathing Guide");
        Prim(PrimitiveType.Sphere, breath.transform, Vector3.zero, Vector3.one, breathMat, "Sphere");
        var bt = new GameObject("Text"); bt.transform.SetParent(breath.transform, false); bt.transform.localPosition = new Vector3(0f, -0.72f, 0f);
        var btt = bt.AddComponent<TextMeshPro>(); btt.rectTransform.sizeDelta = new Vector2(3f, 0.6f); btt.fontSize = 2.2f; btt.alignment = TextAlignmentOptions.Center; btt.text = "Breathe in";
        game.breathRing = breath.transform; game.breathText = btt;

        // ---- grab-and-throw recreation objects from the VR template (they fall slowly in lunar gravity)
        string[] toys = { "Cube Interactable", "Sphere Interactable", "Torus Interactable Variant", "Cylinder Interactable Variant" };
        string[] toyZones = { "sanctuary", "observation", "galley" };
        var toysRoot = new GameObject("HUNCH Recreation Objects"); int placed = 0;
        foreach (var tz in toyZones)
        {
            var z = game.zones.Find(x => x.id == tz); if (z == null) continue;
            for (int t = 0; t < 2; t++)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VRTemplateAssets/Prefabs/Interactables/" + toys[(placed) % toys.Length] + ".prefab");
                if (pf == null) { placed++; continue; }
                var g = (GameObject)PrefabUtility.InstantiatePrefab(pf); g.transform.SetParent(toysRoot.transform, true);
                g.transform.position = z.position + Quaternion.Euler(0f, z.yaw + (t == 0 ? -50f : 50f), 0f) * Vector3.forward * 0.55f + Vector3.up * 1.0f;
                placed++;
            }
        }
        Log("zones " + game.zones.Count + ", interior lights " + inside.Count + ", recreation objects " + placed);

        // ---- kid-friendly features: mood faces, high-five hands, robot buddy, garden, stars
        var kid = gameGo.AddComponent<HunchKidFeatures>();
        var starMat = GetOrCreate("HUNCH_Star", "Universal Render Pipeline/Unlit"); starMat.SetColor("_BaseColor", new Color(1f, 0.82f, 0.12f) * 1.7f);
        kid.starMaterial = starMat;
        kid.confettiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VRTemplateAssets/Prefabs/Blaster/Confetti.prefab");
        if (kid.confettiPrefab == null) Log("WARNING confetti prefab not found");
        var faceMat = GetOrCreate("HUNCH_Face", "Universal Render Pipeline/Unlit"); faceMat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.25f));
        var black = GetOrCreate("HUNCH_Black", "Universal Render Pipeline/Unlit"); black.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.04f));
        var handMat = GetOrCreate("HUNCH_HighFive", "Universal Render Pipeline/Unlit"); MakeTransparent(handMat, new Color(0.4f, 1f, 0.5f, 0.75f), 0f);
        var fc = new List<Transform>(); var fr = new List<Renderer>(); var ml = new List<Transform>(); var mm = new List<Transform>(); var mr = new List<Transform>(); var ht = new List<Transform>();
        foreach (var a in avs)
        {
            var face = new GameObject("Mood Face"); face.transform.SetParent(a, false); face.transform.localPosition = new Vector3(0f, 3.0f, 0f);
            var disc = Prim(PrimitiveType.Sphere, face.transform, Vector3.zero, new Vector3(0.36f, 0.36f, 0.1f), faceMat, "Face");
            Prim(PrimitiveType.Sphere, face.transform, new Vector3(-0.075f, 0.05f, -0.05f), Vector3.one * 0.05f, black, "Eye L");
            Prim(PrimitiveType.Sphere, face.transform, new Vector3(0.075f, 0.05f, -0.05f), Vector3.one * 0.05f, black, "Eye R");
            ml.Add(Prim(PrimitiveType.Cube, face.transform, new Vector3(-0.07f, -0.075f, -0.05f), new Vector3(0.075f, 0.024f, 0.02f), black, "Mouth L").transform);
            mm.Add(Prim(PrimitiveType.Cube, face.transform, new Vector3(0f, -0.075f, -0.05f), new Vector3(0.075f, 0.024f, 0.02f), black, "Mouth M").transform);
            mr.Add(Prim(PrimitiveType.Cube, face.transform, new Vector3(0.07f, -0.075f, -0.05f), new Vector3(0.075f, 0.024f, 0.02f), black, "Mouth R").transform);
            fc.Add(face.transform); fr.Add(disc.GetComponent<Renderer>());
            ht.Add(Prim(PrimitiveType.Sphere, a, new Vector3(0.42f, 1.45f, 0.32f), Vector3.one * 0.15f, handMat, "High-Five Hand").transform);
        }
        kid.faces = fc.ToArray(); kid.faceRenderers = fr.ToArray(); kid.mouthL = ml.ToArray(); kid.mouthM = mm.ToArray(); kid.mouthR = mr.ToArray(); kid.handTargets = ht.ToArray();

        var eyeMat = GetOrCreate("HUNCH_BuddyEye", "Universal Render Pipeline/Unlit"); eyeMat.SetColor("_BaseColor", new Color(0.3f, 0.95f, 1f) * 1.8f);
        var bud = new GameObject("ARIA Robot Buddy"); bud.transform.position = game.zones[0].position + new Vector3(1f, 1.3f, 1f);
        Prim(PrimitiveType.Sphere, bud.transform, Vector3.zero, Vector3.one * 0.36f, suit, "Body");
        Prim(PrimitiveType.Sphere, bud.transform, new Vector3(0f, 0.02f, 0.1f), new Vector3(0.28f, 0.2f, 0.18f), dark, "Screen");
        Prim(PrimitiveType.Sphere, bud.transform, new Vector3(-0.06f, 0.035f, 0.185f), new Vector3(0.055f, 0.07f, 0.02f), eyeMat, "Eye L");
        Prim(PrimitiveType.Sphere, bud.transform, new Vector3(0.06f, 0.035f, 0.185f), new Vector3(0.055f, 0.07f, 0.02f), eyeMat, "Eye R");
        Prim(PrimitiveType.Cylinder, bud.transform, new Vector3(0f, 0.24f, 0f), new Vector3(0.016f, 0.07f, 0.016f), visor, "Antenna");
        Prim(PrimitiveType.Sphere, bud.transform, new Vector3(0f, 0.33f, 0f), Vector3.one * 0.055f, starMat, "Antenna Light");
        Prim(PrimitiveType.Capsule, bud.transform, new Vector3(-0.21f, -0.03f, 0f), new Vector3(0.07f, 0.09f, 0.07f), visor, "Arm L").transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
        Prim(PrimitiveType.Capsule, bud.transform, new Vector3(0.21f, -0.03f, 0f), new Vector3(0.07f, 0.09f, 0.07f), visor, "Arm R").transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
        var sp = new GameObject("Speech"); sp.transform.SetParent(bud.transform, false); sp.transform.localPosition = new Vector3(0f, 0.42f, 0f); sp.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var spt = sp.AddComponent<TextMeshPro>(); spt.rectTransform.sizeDelta = new Vector2(2.4f, 1.0f); spt.fontSize = 0.95f; spt.alignment = TextAlignmentOptions.Bottom; spt.richText = true; spt.color = new Color(1f, 0.95f, 0.7f); spt.text = "Hi! I'm ARIA.";
        kid.buddy = bud.transform; kid.buddySpeech = spt;

        var gz = game.zones.Find(x => x.id == "greenhouse");
        if (gz != null)
        {
            var leaf = GetOrCreate("HUNCH_Leaf", "Universal Render Pipeline/Lit"); leaf.SetColor("_BaseColor", new Color(0.2f, 0.7f, 0.25f)); leaf.SetFloat("_Smoothness", 0.3f); leaf.SetFloat("_Metallic", 0f);
            var red = GetOrCreate("HUNCH_Tomato", "Universal Render Pipeline/Lit"); red.SetColor("_BaseColor", new Color(0.9f, 0.15f, 0.1f)); red.SetFloat("_Smoothness", 0.7f); red.SetFloat("_Metallic", 0f);
            var garden = new GameObject("HUNCH Growing Garden"); var spr = new List<Transform>();
            for (int i = 0; i < 8; i++)
            {
                var g = new GameObject("Sprout " + (i + 1)); g.transform.SetParent(garden.transform, false);
                g.transform.position = gz.position + Quaternion.Euler(0f, gz.yaw + 45f * i + 20f, 0f) * Vector3.forward * (2.4f + 0.5f * (i % 2));
                Prim(PrimitiveType.Cylinder, g.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.3f, 0.05f), leaf, "Stem");
                Prim(PrimitiveType.Sphere, g.transform, new Vector3(-0.13f, 0.52f, 0f), new Vector3(0.28f, 0.09f, 0.18f), leaf, "Leaf L");
                Prim(PrimitiveType.Sphere, g.transform, new Vector3(0.13f, 0.58f, 0f), new Vector3(0.28f, 0.09f, 0.18f), leaf, "Leaf R");
                Prim(PrimitiveType.Sphere, g.transform, new Vector3(0f, 0.72f, 0.05f), Vector3.one * 0.15f, red, "Fruit").SetActive(false);
                g.transform.localScale = Vector3.one * 0.22f; spr.Add(g.transform);
            }
            kid.sprouts = spr.ToArray();
        }

        var desk = gameGo.AddComponent<HunchDesktopFallback>(); desk.origin = origin;
        var navAssist = gameGo.AddComponent<HunchNavAssist>(); navAssist.origin = origin;
        gameGo.AddComponent<HunchSelfTest>();
        rig.transform.position = game.zones[0].position;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.productName = "Lunar Habitat Commander";
        PlayerSettings.companyName = "HUNCH Student Project";
        AssetDatabase.SaveAssets();
        Log("scene saved with " + game.zones.Count + " zones: " + ScenePath);
    }

    // Stars drawn straight onto cube faces, so there is no stretching at the poles
    static Cubemap BuildStarCube()
    {
        int n = 1024; var cube = new Cubemap(n, TextureFormat.RGBA32, false);
        var faces = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
        var rng = new System.Random(2027);
        foreach (var f in faces)
        {
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0f, 0f, 0f, 1f);
            for (int k = 0; k < 1500; k++)
            {
                int x = rng.Next(2, n - 2), y = rng.Next(2, n - 2);
                double m = Math.Pow(rng.NextDouble(), 7.0);            // most stars faint, a few bright
                float b = 0.16f + 0.84f * (float)m;
                float t = (float)rng.NextDouble();                     // colour temperature
                Color c = Color.Lerp(new Color(1f, 0.82f, 0.68f), new Color(0.72f, 0.84f, 1f), t) * b;
                Add(px, n, x, y, c);
                if (m > 0.35) { Color h = c * 0.45f; Add(px, n, x + 1, y, h); Add(px, n, x - 1, y, h); Add(px, n, x, y + 1, h); Add(px, n, x, y - 1, h); }
                if (m > 0.75) { Color h = c * 0.2f; Add(px, n, x + 2, y, h); Add(px, n, x - 2, y, h); Add(px, n, x, y + 2, h); Add(px, n, x, y - 2, h); Add(px, n, x + 1, y + 1, h); Add(px, n, x - 1, y - 1, h); Add(px, n, x + 1, y - 1, h); Add(px, n, x - 1, y + 1, h); }
            }
            cube.SetPixels(px, f);
        }
        cube.Apply(false);
        string p = Root + "/Sky/HUNCH_Stars.cubemap";
        AssetDatabase.DeleteAsset(p); AssetDatabase.CreateAsset(cube, p);
        return cube;
    }
    static void Add(Color[] px, int n, int x, int y, Color c)
    {
        int i = y * n + x; Color o = px[i]; o += c; o.a = 1f;
        px[i] = new Color(Mathf.Min(1f, o.r), Mathf.Min(1f, o.g), Mathf.Min(1f, o.b), 1f);
    }

    static Cubemap BuildReflectionCube(Vector3 toSun)
    {
        int n = 64; var cube = new Cubemap(n, TextureFormat.RGBAHalf, true);
        Color zenith = new Color(0.07f, 0.08f, 0.12f), horizon = new Color(0.62f, 0.59f, 0.54f), ground = new Color(0.32f, 0.31f, 0.29f);
        var faces = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
        foreach (var f in faces)
        {
            var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f; Vector3 d;
                switch (f)
                {
                    case CubemapFace.PositiveX: d = new Vector3(1f, -v, -u); break;
                    case CubemapFace.NegativeX: d = new Vector3(-1f, -v, u); break;
                    case CubemapFace.PositiveY: d = new Vector3(u, 1f, v); break;
                    case CubemapFace.NegativeY: d = new Vector3(u, -1f, -v); break;
                    case CubemapFace.PositiveZ: d = new Vector3(u, -v, 1f); break;
                    default: d = new Vector3(-u, -v, -1f); break;
                }
                d.Normalize();
                Color c = d.y >= 0f ? Color.Lerp(horizon, zenith, Mathf.Pow(d.y, 0.45f)) : Color.Lerp(horizon, ground, Mathf.Pow(-d.y, 0.6f));
                float s = Mathf.Max(0f, Vector3.Dot(d, toSun)); c += new Color(1f, 0.95f, 0.85f) * Mathf.Pow(s, 24f) * 5f;
                c.a = 1f; px[y * n + x] = c;
            }
            cube.SetPixels(px, f);
        }
        cube.Apply(true);
        string p = Root + "/Settings/HUNCH_Reflection.cubemap";
        AssetDatabase.DeleteAsset(p); AssetDatabase.CreateAsset(cube, p);
        return cube;
    }

    static void TuneUrpAssets()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            var so = new SerializedObject(asset);
            Set(so, "m_SupportsHDR", 1); Set(so, "m_MSAA", 4); Set(so, "m_ShadowCascadeCount", 4); Set(so, "m_MainLightShadowmapResolution", 4096);
            Set(so, "m_MainLightShadowsSupported", 1); Set(so, "m_SoftShadowsSupported", 1); Set(so, "m_AdditionalLightsRenderingMode", 1); Set(so, "m_AdditionalLightsPerObjectLimit", 8);
            var sd = so.FindProperty("m_ShadowDistance"); if (sd != null) sd.floatValue = 260f * S;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
            Log("tuned URP asset " + path);
        }
    }
    static void Set(SerializedObject so, string prop, int v)
    {
        var p = so.FindProperty(prop); if (p == null) return;
        if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = v != 0; else p.intValue = v;
    }

    // ------------------------------------------------------------------ previews
    static void RenderPreviews()
    {
        Directory.CreateDirectory(PreviewDir);
        ShaderUtil.allowAsyncCompilation = false;
        var data = LoadStations();
        var go = new GameObject("HUNCH Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>(); cam.fieldOfView = 78f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 6000f; cam.allowHDR = true; cam.allowMSAA = true;
        var cd = go.AddComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing = true; cd.renderShadows = true;
        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        for (int i = 0; i < data.stations.Length; i++)
        {
            var s = data.stations[i];
            Vector3 p = B2U(s.pos) + Vector3.up * 1.65f, l = B2U(s.look);
            cam.transform.position = p; cam.transform.rotation = Quaternion.LookRotation((l - p).normalized, Vector3.up);
            cam.targetTexture = rt;
            cam.Render(); cam.Render();
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply(); RenderTexture.active = null;
            string safe = new string(s.title.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
            File.WriteAllBytes(Path.Combine(PreviewDir, (i + 1).ToString("00") + "_" + safe + ".jpg"), tex.EncodeToJPG(88));
        }
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(go); rt.Release();
        Log("previews written: " + data.stations.Length);
    }
}
