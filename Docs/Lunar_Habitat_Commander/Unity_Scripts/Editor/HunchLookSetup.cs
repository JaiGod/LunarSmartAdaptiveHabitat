// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchLookSetup.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Picture quality set-up that has to be done in the Editor (it changes the renderer
//            asset, not the scene).
//  Contains: HunchLookSetup
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Picture quality set-up that has to be done in the Editor (it changes the renderer asset, not the scene).
// AddSsao()     adds URP's Screen Space Ambient Occlusion to every renderer in the project that does not have it yet:
//               soft contact shadows where objects meet, which is most of what makes simple shapes look solid.
//               The game switches it off in the headset (HunchLook) to keep VR smooth.
// RemoveSsao()  takes it out again.
public static class HunchLookSetup
{
    static string Bridge { get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge"); } }
    const string SsaoType = "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion";

    [MenuItem("HUNCH/Look: add ambient occlusion")]
    public static void AddSsao()
    {
        var sb = new StringBuilder();
        try
        {
            var type = typeof(ScriptableRendererFeature).Assembly.GetType(SsaoType);
            if (type == null) { sb.AppendLine("SSAO type not found in this URP version"); }
            else foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); if (!path.StartsWith("Assets/")) continue;
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path); if (!data) continue;
                if (data.rendererFeatures.Any(f => f && f.GetType() == type)) { sb.AppendLine("already has it: " + path); continue; }
                var feat = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type); feat.name = "ScreenSpaceAmbientOcclusion";
                var fso = new SerializedObject(feat); var sh = fso.FindProperty("m_Shader");
                if (sh != null && !sh.objectReferenceValue) { sh.objectReferenceValue = Shader.Find("Hidden/Universal Render Pipeline/ScreenSpaceAmbientOcclusion"); }
                var inten = fso.FindProperty("m_Settings.Intensity"); if (inten != null) inten.floatValue = 2.2f;
                var rad = fso.FindProperty("m_Settings.Radius"); if (rad != null) rad.floatValue = 0.25f;
                var fall = fso.FindProperty("m_Settings.Falloff"); if (fall != null) fall.floatValue = 80f;
                fso.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.AddObjectToAsset(feat, data);
                string g; long id; AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out g, out id);
                var so = new SerializedObject(data); var list = so.FindProperty("m_RendererFeatures"); var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feat;
                map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(data);
                sb.AppendLine("added to " + path + "  shader " + (sh != null && sh.objectReferenceValue ? "ok" : "MISSING") + "  features now " + data.rendererFeatures.Count);
            }
            AssetDatabase.SaveAssets(); sb.AppendLine("OK");
        }
        catch (Exception e) { sb.AppendLine("FAILED " + e); }
        File.WriteAllText(Path.Combine(Bridge, "look_setup.txt"), sb.ToString());
    }

    [MenuItem("HUNCH/Look: remove ambient occlusion")]
    public static void RemoveSsao()
    {
        var sb = new StringBuilder();
        try
        {
            var type = typeof(ScriptableRendererFeature).Assembly.GetType(SsaoType);
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); if (!path.StartsWith("Assets/")) continue;
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path); if (!data) continue;
                var so = new SerializedObject(data); var list = so.FindProperty("m_RendererFeatures"); var map = so.FindProperty("m_RendererFeatureMap");
                for (int i = list.arraySize - 1; i >= 0; i--)
                {
                    var f = list.GetArrayElementAtIndex(i).objectReferenceValue; if (!f || f.GetType() != type || f.name != "ScreenSpaceAmbientOcclusion") continue;
                    list.GetArrayElementAtIndex(i).objectReferenceValue = null; list.DeleteArrayElementAtIndex(i); if (i < map.arraySize) map.DeleteArrayElementAtIndex(i);
                    so.ApplyModifiedPropertiesWithoutUndo(); UnityEngine.Object.DestroyImmediate(f, true); sb.AppendLine("removed from " + path);
                }
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets(); sb.AppendLine("OK");
        }
        catch (Exception e) { sb.AppendLine("FAILED " + e); }
        File.WriteAllText(Path.Combine(Bridge, "look_setup.txt"), sb.ToString());
    }
}
