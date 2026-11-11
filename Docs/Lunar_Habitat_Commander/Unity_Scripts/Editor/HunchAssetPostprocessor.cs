// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchAssetPostprocessor.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Import settings for everything under Assets/HUNCH.
//  Contains: HunchAssetPostprocessor
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.IO;
using UnityEditor;
using UnityEngine;

/// Import settings for everything under Assets/HUNCH.
public class HunchAssetPostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/HUNCH/")) return;
        var ti = (TextureImporter)assetImporter;
        string n = Path.GetFileNameWithoutExtension(assetPath);
        ti.mipmapEnabled = true;
        ti.anisoLevel = 4;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.maxTextureSize = 2048;
        ti.npotScale = TextureImporterNPOTScale.None;
        if (n.EndsWith("_MS")) { ti.sRGBTexture = false; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = false; }
        else if (n.EndsWith("_Normal")) { ti.textureType = TextureImporterType.NormalMap; ti.wrapMode = TextureWrapMode.Repeat; }
        else if (n.EndsWith("_Color"))
        {
            bool close = n.Contains("Interior") || n.Contains("HUB_F") || n.Contains("Mezzanine") || n.Contains("OCEAN") || n.Contains("KEEP") || n.Contains("Helix") || n.Contains("Ground");
            ti.maxTextureSize = close ? 4096 : 2048;
        }
        if (n.StartsWith("Starfield")) { ti.maxTextureSize = 4096; ti.mipmapEnabled = false; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; }
        if (n.StartsWith("EARTH")) { ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; }
    }

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/HUNCH/")) return;
        var ai = (AudioImporter)assetImporter;
        var st = ai.defaultSampleSettings; st.loadType = AudioClipLoadType.Streaming; ai.defaultSampleSettings = st; // long music track
        ai.loadInBackground = true;
    }

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/HUNCH/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.importBlendShapes = false;
        mi.importVisibility = false;
        mi.bakeAxisConversion = true;
        mi.useFileScale = true;
        mi.globalScale = 1f;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        mi.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        mi.isReadable = assetPath.EndsWith("HUNCH_Lunar_Base.fbx");      // the base must be readable in a built game: the walkable map (HunchPath) is made from its solid shapes while the game runs
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.importNormals = ModelImporterNormals.Import;
        mi.importTangents = ModelImporterTangents.CalculateMikk;
        mi.generateSecondaryUV = false;
        mi.indexFormat = ModelImporterIndexFormat.Auto;
    }
}
