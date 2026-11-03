using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Hunch3.EditorTools
{
    [Serializable] public class MatItem { public string name; public float[] baseColor; public float metal, rough; public float[] emit; public float emitStrength, alpha; public bool baked; public float tileM; }
    [Serializable] public class MatList { public MatItem[] items; }

    public static class Builder
    {
        const string Root = "Assets/Unity3";
        const string HabFbx = Root + "/Models/LunarHabitat_Unity3_Habitat.fbx", TerFbx = Root + "/Models/LunarHabitat_Unity3_Terrain.fbx";
        const string MenuScene = Root + "/Scenes/00_Menu_VRBox.unity", HabScene = Root + "/Scenes/01_LunarHabitat.unity";
        static readonly string[] RigCandidates = { "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab", "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Hands Variant.prefab", "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab" };
        static readonly List<string> report = new List<string>();
        static void R(string s) { report.Add(s); Debug.Log("[HUNCH3] " + s); }

        [MenuItem("HUNCH 3/1. Build scenes")] public static void BuildScenesMenu() { Run(() => { Materials(); Scenes(); }); }
        [MenuItem("HUNCH 3/2. Build Windows .exe")] public static void BuildExeMenu() { Run(BuildWindows); }
        [MenuItem("HUNCH 3/3. Build Quest .apk")] public static void BuildApkMenu() { Run(BuildAndroid); }
        public static void BuildAll() { Run(() => { Materials(); Scenes(); BuildWindows(); }, true); }
        public static void BuildScenesOnly() { Run(() => { Materials(); Scenes(); }, true); }

        static void Run(Action a, bool batch = false)
        {
            report.Clear(); int code = 0;
            try { a(); R("DONE"); }
            catch (Exception e) { R("FAILED: " + e); code = 1; }
            Directory.CreateDirectory("Reports"); File.WriteAllLines("Reports/build_log.txt", report);
            if (batch) EditorApplication.Exit(code);
        }

        // ---------- materials ----------
        static void Materials()
        {
            AssetDatabase.ImportAsset(HabFbx, ImportAssetOptions.ForceUpdate); AssetDatabase.ImportAsset(TerFbx, ImportAssetOptions.ForceUpdate);
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Resources/materials.json");
            var list = JsonUtility.FromJson<MatList>(json.text);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Directory.CreateDirectory(Root + "/Materials");
            var made = new Dictionary<string, Material>();
            foreach (var it in list.items)
            {
                string p = $"{Root}/Materials/{it.name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (!m) { m = new Material(lit); AssetDatabase.CreateAsset(m, p); }
                m.shader = lit;
                var bc = new Color(it.baseColor[0], it.baseColor[1], it.baseColor[2], it.alpha);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/{it.name}_BaseColor.png");
                var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/{it.name}_Normal.png");
                float tile = it.tileM > 0 ? 1f / it.tileM : 0.5f;
                if (tex) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", new Vector2(tile, tile)); m.SetColor("_BaseColor", new Color(1, 1, 1, it.alpha)); }
                else m.SetColor("_BaseColor", bc);
                if (nrm) { m.SetTexture("_BumpMap", nrm); m.SetTextureScale("_BumpMap", new Vector2(tile, tile)); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.6f); }
                m.SetFloat("_Metallic", it.metal); m.SetFloat("_Smoothness", Mathf.Clamp01(1f - it.rough));
                if (it.emit != null && it.emit.Length == 3 && it.emitStrength > 0)
                {
                    var ec = new Color(it.emit[0], it.emit[1], it.emit[2]) * Mathf.Min(it.emitStrength, 4f);
                    m.SetColor("_EmissionColor", ec); m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    m.SetColor("_BaseColor", new Color(it.emit[0], it.emit[1], it.emit[2]));
                }
                if (it.alpha < 0.99f)
                {
                    m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
                }
                EditorUtility.SetDirty(m); made[it.name] = m;
            }
            foreach (var fbx in new[] { HabFbx, TerFbx })
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
                foreach (var kv in made) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
                mi.SaveAndReimport();
            }
            var unl = Shader.Find("Universal Render Pipeline/Unlit");
            foreach (var (nm, tr) in new[] { ("Hunch3_Unlit", false), ("Hunch3_UnlitTransparent", true) })
            {
                string up = $"{Root}/Resources/{nm}.mat"; var um = AssetDatabase.LoadAssetAtPath<Material>(up);
                if (!um) { um = new Material(unl); AssetDatabase.CreateAsset(um, up); }
                if (tr) { um.SetFloat("_Surface", 1); um.SetFloat("_Blend", 0); um.SetFloat("_SrcBlend", 5); um.SetFloat("_DstBlend", 10); um.SetFloat("_ZWrite", 0); um.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); um.renderQueue = 3000; }
                EditorUtility.SetDirty(um);
            }
            AssetDatabase.SaveAssets(); R($"materials: {made.Count}");
        }

        // ---------- scenes ----------
        static GameObject Rig()
        {
            foreach (var p in RigCandidates) { var pf = AssetDatabase.LoadAssetAtPath<GameObject>(p); if (pf) { var g = (GameObject)PrefabUtility.InstantiatePrefab(pf); R("rig: " + p); return g; } }
            throw new Exception("No XR rig prefab found");
        }
        static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>()) return;
            var es = new GameObject("EventSystem", typeof(EventSystem)); es.AddComponent<XRUIInputModule>();
        }
        static void Scenes()
        {
            ConfigureRendering();
            Directory.CreateDirectory(Root + "/Scenes");
            // ---- menu ----
            var s0 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rig0 = Rig(); rig0.transform.position = Vector3.zero; EnsureEventSystem();
            new GameObject("MenuBoot").AddComponent<MenuBoot>();
            RenderSettings.skybox = null; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.25f, 0.2f, 0.4f);
            EditorSceneManager.SaveScene(s0, MenuScene); R("menu scene saved");

            // ---- habitat ----
            var s1 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var hab = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HabFbx)); hab.name = "LunarHabitat_Unity3_Habitat";
            var ter = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TerFbx)); ter.name = "LunarHabitat_Unity3_Terrain";
            int cols = 0, inter = 0, lights = 0, tele = 0;
            var lightCols = new Dictionary<string, Color>();
            var json = JsonUtility.FromJson<MatList>(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Resources/materials.json").text);
            foreach (var it in json.items) if (it.emit != null && it.emit.Length == 3) lightCols[it.name] = new Color(it.emit[0], it.emit[1], it.emit[2]);
            foreach (var t in hab.GetComponentsInChildren<Transform>(true).Concat(ter.GetComponentsInChildren<Transform>(true)).ToList())
            {
                var tag = t.GetComponent<HabTag>(); var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
                string ck = tag ? tag.ColliderKind : (t.name.StartsWith("T0") ? "mesh" : "none");
                if (tag && tag.Get("unity_export").ToLower() == "false") { t.gameObject.SetActive(false); continue; }
                if (mf && mf.sharedMesh)
                {
                    if (ck == "mesh") { var mc = t.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; cols++; }
                    else if (ck == "box") { t.gameObject.AddComponent<BoxCollider>(); cols++; }
                }
                string it2 = tag ? tag.Interact : "";
                if (mf && mf.sharedMesh && !t.GetComponent<Collider>() && !string.IsNullOrEmpty(it2) && it2 != "mood_light" && it2 != "pod_controller" && it2 != "crew_agent" && it2 != "eva_suit_dock")
                { var bc = t.gameObject.AddComponent<BoxCollider>(); bc.isTrigger = true; }
                if (!string.IsNullOrEmpty(it2) && it2 != "mood_light" && it2 != "pod_controller" && it2 != "crew_agent" && it2 != "eva_suit_dock" && t.GetComponent<Collider>())
                {
                    if (it2 == "pickup") { var rb = t.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.mass = 8; t.gameObject.AddComponent<XRGrabInteractable>(); }
                    var hi = t.gameObject.AddComponent<HabInteractable>(); hi.kind = it2; inter++;
                }
                bool floor = (t.name.Contains("Floor") || t.name.StartsWith("T01_Terrain") || t.name.Contains("Porch") || t.name.Contains("Platform")) && t.GetComponent<Collider>();
                if (floor) { t.gameObject.AddComponent<TeleportationArea>(); tele++; }
                if (mr && !t.GetComponent<HabInteractable>() && !t.name.Contains("Mood") && !t.name.StartsWith("CB") && !t.name.StartsWith("CREW") && !t.name.StartsWith("IVA") && !t.name.StartsWith("EVA") && !t.name.Contains("SolarTower"))   // solar arrays track the Sun → not static
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                // real lights behind light fixtures
                if (mr && mr.sharedMaterial)
                {
                    string mn = mr.sharedMaterial.name;
                    bool mood = mn.StartsWith("Emit_Mood") && (t.name.Contains("Ceiling_Ring_N") || t.name.Contains("UnderBed"));
                    bool fixture = mn.StartsWith("Emit_LED") || mn.StartsWith("Emit_Grow") || mood;
                    if (fixture && mr.bounds.size.magnitude > 0.25f)
                    {
                        var lg = new GameObject("L_" + t.name); lg.transform.position = mr.bounds.center - Vector3.up * 0.12f; lg.transform.SetParent(t, true);
                        var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.shadows = LightShadows.None;
                        l.color = lightCols.TryGetValue(mn, out var lc) ? lc : Color.white;
                        l.range = mood ? 1.6f : mn.StartsWith("Emit_Grow") ? 3.2f : 7.5f; l.intensity = mood ? 0.25f : mn.StartsWith("Emit_Grow") ? 1.0f : 3.2f;
                        lights++;
                    }
                }
            }
            R($"colliders {cols} · interactables {inter} · lights {lights} · teleport floors {tele}");
            // Real NASA 3D models (NASA 3D Resources, public domain) replace the hand-built placeholders outside
            NasaModels(hab);
            // Hologram map for the command table: a separate, NON-static copy built here in the editor.
            // (Instantiating the static-batched habitat at runtime drew full-size teal ghosts of every module over the whole base.)
            {
                var holo = (GameObject)UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HabFbx)); holo.name = "HoloMap_Model";
                foreach (var mb in holo.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(mb);
                foreach (var lt in holo.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(lt);
                foreach (var co in holo.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(co);
                foreach (var t2 in holo.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(t2.gameObject, (StaticEditorFlags)0);
                    if (t2 != holo.transform) t2.name = "HOLO_" + t2.name;
                    var mr2 = t2.GetComponent<MeshRenderer>(); if (mr2) mr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                holo.SetActive(false);
                R("hologram map copy built (non-static)");
            }
            // sun (south pole: very low), Earth on the horizon, black sky
            var sun = new GameObject("Sun_SouthPole").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 2.2f; sun.color = new Color(1f, 0.97f, 0.92f); sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(8f, 215f, 0);
            var earth = GameObject.CreatePrimitive(PrimitiveType.Sphere); earth.name = "Earth_Horizon"; UnityEngine.Object.DestroyImmediate(earth.GetComponent<Collider>()); earth.transform.position = new Vector3(-1600, 110, -1100); earth.transform.localScale = Vector3.one * 70;
            var em = new Material(Shader.Find("Universal Render Pipeline/Unlit")); em.SetColor("_BaseColor", new Color(0.35f, 0.55f, 0.95f)); AssetDatabase.CreateAsset(em, Root + "/Materials/Earth_Unlit.mat"); earth.GetComponent<Renderer>().sharedMaterial = em;
            RenderSettings.skybox = null; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.2f, 0.2f, 0.2f);
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom; RenderSettings.reflectionIntensity = 0.35f;   // no blue default-sky reflections on metal
            var rig = Rig(); var spawn = hab.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "SPAWN_Player");
            if (spawn) rig.transform.position = spawn.position;
            foreach (var cam in rig.GetComponentsInChildren<Camera>(true)) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.farClipPlane = 3000; }
            EnsureEventSystem();
            new GameObject("HabitatBoot").AddComponent<HabitatBoot>();
            EditorSceneManager.SaveScene(s1, HabScene); R("habitat scene saved");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuScene, true), new EditorBuildSettingsScene(HabScene, true) };
            PlayerSettings.productName = "Lunar Smart Adaptive Habitat"; PlayerSettings.companyName = "Jaineil R.";
            AssetDatabase.SaveAssets();
        }

        static Bounds WorldBounds(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }
        static Bounds GroupBounds(GameObject hab, Func<string, bool> pick, bool hide)
        {
            Bounds b = default; bool any = false;
            foreach (var t in hab.GetComponentsInChildren<Transform>(true))
            {
                if (!pick(t.name)) continue;
                var r = t.GetComponent<Renderer>(); if (r) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            }
            if (hide) foreach (var t in hab.GetComponentsInChildren<Transform>(true)) if (pick(t.name)) t.gameObject.SetActive(false);
            return any ? b : new Bounds(Vector3.zero, Vector3.zero);
        }
        static float GroundY(Vector3 p, float fallback)
        {
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(p + Vector3.up * 60f, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity; foreach (var h in hits) if (h.collider.name.StartsWith("T01_Terrain") || h.collider.name.Contains("LandingPad")) best = Mathf.Max(best, h.point.y);
            return float.IsNegativeInfinity(best) ? fallback : best;
        }
        static GameObject PlaceNasa(string file, string name, Bounds target, bool alignLong, bool collider)
        {
            var a = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/NASA/{file}.fbx");
            if (!a) { R("NASA model missing: " + file); return null; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(a); g.name = name;
            g.transform.position = new Vector3(target.center.x, GroundY(target.center, target.min.y), target.center.z);
            if (alignLong)
            {
                var b = WorldBounds(g); bool tx = target.size.x >= target.size.z, gx = b.size.x >= b.size.z;
                if (tx != gx) g.transform.Rotate(0, 90, 0, Space.World);
            }
            {   // whatever the model's pivot is: centre it on the target and sit its lowest point exactly on the ground
                var b = WorldBounds(g); float gy = GroundY(target.center, target.min.y);
                g.transform.position += new Vector3(target.center.x - b.center.x, gy - b.min.y, target.center.z - b.center.z);
            }
            foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (collider) { var b = WorldBounds(g); var bc = g.AddComponent<BoxCollider>(); bc.center = g.transform.InverseTransformPoint(b.center); var ls = g.transform.lossyScale; bc.size = new Vector3(b.size.x / Mathf.Abs(ls.x), b.size.y / Mathf.Abs(ls.y), b.size.z / Mathf.Abs(ls.z)); }
            R($"NASA model placed: {name} at {g.transform.position}");
            return g;
        }
        static void NasaModels(GameObject hab)
        {
            // pressurised rover → NASA Space Exploration Vehicle
            var rov = GroupBounds(hab, n => n.StartsWith("V0") && n.Contains("PressRover"), true);
            if (rov.size != Vector3.zero)
            {   // park it a few metres clear of the airlock (the SEV is larger than the old placeholder)
                var away = new Vector3(rov.center.x, 0, rov.center.z).normalized; rov.center += away * 4.5f;
                PlaceNasa("NASA_Space_Exploration_Vehicle", "NASA_SEV_PressurizedRover", rov, true, true);
            }
            // ISRU excavator → NASA RASSOR (it drives around its dig site at runtime)
            var exc = GroupBounds(hab, n => n.StartsWith("LOG_G20") || n.StartsWith("LOG_G21") || n.StartsWith("LOG_G22"), true);
            if (exc.size != Vector3.zero) PlaceNasa("NASA_RASSOR", "NASA_RASSOR", exc, false, false);
            // cargo lander on the sintered landing pad → NASA Apollo LM (descent + ascent stage) as the supply lander
            var pad = GroupBounds(hab, n => n.StartsWith("LOG_G30_LandingPad"), false);
            if (pad.size != Vector3.zero) { var b = pad; b.center = new Vector3(pad.center.x, pad.max.y, pad.center.z); PlaceNasa("NASA_Apollo_Lunar_Module", "CargoLander", b, false, false); }
        }

        /// Contact shadows + ambient occlusion so objects read as touching the floor/walls they sit on.
        static void ConfigureRendering()
        {
            try
            {
                var assets = new List<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>();
                if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset d) assets.Add(d);
                for (int q = 0; q < QualitySettings.names.Length; q++) if (QualitySettings.GetRenderPipelineAssetAt(q) is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset a && !assets.Contains(a)) assets.Add(a);
                var ssaoType = typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                foreach (var urp in assets)
                {
                    var so = new SerializedObject(urp);
                    void SetB(string n, bool v) { var p = so.FindProperty(n); if (p != null) p.boolValue = v; }
                    void SetI(string n, int v) { var p = so.FindProperty(n); if (p != null) p.intValue = v; }
                    SetB("m_AdditionalLightShadowsSupported", true); SetI("m_AdditionalLightsShadowmapResolution", 2048); SetB("m_SoftShadowsSupported", true);
                    SetI("m_AdditionalLightsPerObjectLimit", 8); SetB("m_MainLightShadowsSupported", true);
                    so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(urp);
                    // SSAO renderer feature on every renderer this asset uses
                    var rl = so.FindProperty("m_RendererDataList");
                    for (int i = 0; rl != null && i < rl.arraySize; i++)
                    {
                        var rd = rl.GetArrayElementAtIndex(i).objectReferenceValue as UnityEngine.Rendering.Universal.ScriptableRendererData;
                        if (!rd || ssaoType == null || rd.rendererFeatures.Any(f => f && f.GetType() == ssaoType)) continue;
                        var feat = ScriptableObject.CreateInstance(ssaoType) as UnityEngine.Rendering.Universal.ScriptableRendererFeature; feat.name = "ScreenSpaceAmbientOcclusion";
                        AssetDatabase.AddObjectToAsset(feat, rd);
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out string _g, out long lid);
                        var rso = new SerializedObject(rd);
                        var fl = rso.FindProperty("m_RendererFeatures"); var fm = rso.FindProperty("m_RendererFeatureMap");
                        fl.arraySize++; fl.GetArrayElementAtIndex(fl.arraySize - 1).objectReferenceValue = feat;
                        if (fm != null) { fm.arraySize++; fm.GetArrayElementAtIndex(fm.arraySize - 1).longValue = lid; }
                        rso.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(rd);
                        R("SSAO added to " + rd.name);
                    }
                }
                AssetDatabase.SaveAssets();
                R($"rendering: additional-light soft shadows ON on {assets.Count} URP asset(s)");
            }
            catch (Exception e) { R("rendering config skipped: " + e.Message); }
        }

        static void BuildWindows()
        {
            string u3 = "C:/Users/jaine/Downloads/Nasa/official projects for nasa/unity 3"; string outDir = Directory.Exists(u3) ? Path.Combine(u3, "Builds/Windows") : Path.GetFullPath("../Builds/Windows"); Directory.CreateDirectory(outDir);
            // Mono for the desktop/PC-VR build: quick to build, no C++ toolchain needed, matches the existing build folder
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            var opts = new BuildPlayerOptions { scenes = new[] { MenuScene, HabScene }, locationPathName = Path.Combine(outDir, "LunarHabitat.exe"), target = BuildTarget.StandaloneWindows64, options = BuildOptions.None };
            var rep = BuildPipeline.BuildPlayer(opts);
            R($"windows build: {rep.summary.result} · {rep.summary.totalErrors} errors · {rep.summary.totalSize / 1e6:0} MB → {opts.locationPathName}");
            foreach (var st in rep.steps) foreach (var m in st.messages) if (m.type == LogType.Error || m.type == LogType.Exception) R($"  build error [{st.name}]: {m.content}");
            if (rep.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Windows build failed");
        }
        static void BuildAndroid()
        {
            string u3q = "C:/Users/jaine/Downloads/Nasa/official projects for nasa/unity 3"; string outDir = Directory.Exists(u3q) ? Path.Combine(u3q, "Builds/Quest") : Path.GetFullPath("../Builds/Quest"); Directory.CreateDirectory(outDir);
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            var opts = new BuildPlayerOptions { scenes = new[] { MenuScene, HabScene }, locationPathName = Path.Combine(outDir, "LunarHabitat.apk"), target = BuildTarget.Android, options = BuildOptions.None };
            var rep = BuildPipeline.BuildPlayer(opts);
            R($"quest build: {rep.summary.result} · {rep.summary.totalErrors} errors → {opts.locationPathName}");
        }
    }
}
