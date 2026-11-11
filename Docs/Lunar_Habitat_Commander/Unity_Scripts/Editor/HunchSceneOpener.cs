// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchSceneOpener.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Opens the explorer scene when the project starts. The VR template's welcome tutorial
//            switches to its own SampleScene on first launch, so for the first few minutes of an
//            Editor session this switches back.
//  Contains: HunchSceneOpener
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using UnityEditor;
using UnityEditor.SceneManagement;

/// Opens the explorer scene when the project starts. The VR template's welcome tutorial switches to its own
/// SampleScene on first launch, so for the first few minutes of an Editor session this switches back.
[InitializeOnLoad]
static class HunchSceneOpener
{
    const string ScenePath = "Assets/HUNCH/Scenes/Lunar_Habitat_Commander.unity";
    const string TemplateScene = "Assets/Scenes/SampleScene.unity";
    static double nextCheck;

    static HunchSceneOpener()
    {
        if (UnityEngine.Application.isBatchMode) return;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > 240.0) { EditorApplication.update -= Tick; return; }
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!System.IO.File.Exists(ScenePath)) return;
        var s = EditorSceneManager.GetActiveScene();
        bool first = !SessionState.GetBool("hunch_scene_opened", false);
        if (s.path == ScenePath) { SessionState.SetBool("hunch_scene_opened", true); return; }
        if (s.isDirty) return;
        if (first || s.path == TemplateScene)
        {
            SessionState.SetBool("hunch_scene_opened", true);
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
