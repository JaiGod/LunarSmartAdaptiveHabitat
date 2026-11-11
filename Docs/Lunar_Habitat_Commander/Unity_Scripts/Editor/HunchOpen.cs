// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchOpen.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Menu shortcut that opens the main game scene.
//  Contains: HunchOpen
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using UnityEditor;
using UnityEditor.SceneManagement;

// Opens the game's scene (used by the test bridge after Unity has been restarted on another scene).
public static class HunchOpen
{
    public static void Scene()
    {
        const string path = "Assets/HUNCH/Scenes/Lunar_Habitat_Commander.unity";
        if (EditorApplication.isPlaying) return;
        if (EditorSceneManager.GetActiveScene().path != path) EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }
}
