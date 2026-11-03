using UnityEditor;
using UnityEngine;

namespace Hunch3.EditorTools
{
    /// Copies Blender custom properties (interact, collider, zone, links …) onto a HabTag component at import time.
    public class HabImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("Unity3/Models")) return;
            var mi = (ModelImporter)assetImporter;
            mi.importLights = false; mi.importCameras = false; mi.importAnimation = false; mi.addCollider = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.isReadable = true; mi.meshCompression = ModelImporterMeshCompression.Off;
        }
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("Unity3/Textures")) return;
            var ti = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("_Normal.png")) ti.textureType = TextureImporterType.NormalMap;
            if (assetPath.EndsWith("_Roughness.png")) ti.sRGBTexture = false;
            ti.maxTextureSize = 1024; ti.wrapMode = TextureWrapMode.Repeat;
        }
        void OnPostprocessGameObjectWithUserProperties(GameObject go, string[] names, object[] values)
        {
            if (!assetPath.Contains("Unity3/Models")) return;
            var t = go.GetComponent<HabTag>(); if (!t) t = go.AddComponent<HabTag>();
            for (int i = 0; i < names.Length; i++) t.Set(names[i], values[i] == null ? "" : values[i].ToString());
        }
    }
}
