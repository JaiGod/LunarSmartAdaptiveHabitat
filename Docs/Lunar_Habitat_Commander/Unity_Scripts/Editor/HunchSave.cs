// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchSave.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Saves the open scenes and every changed asset, and can close the Editor afterwards.
//  Contains: HunchSave
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Saves the open scenes and every changed asset, and can close the Editor afterwards.
public static class HunchSave
{
    static string Bridge { get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge"); } }
    public static void All()
    {
        bool scenes = EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Bridge, "save.txt"), System.DateTime.Now.ToString("HH:mm:ss") + " saved scenes=" + scenes + " dirty=" + EditorSceneManager.GetActiveScene().isDirty);
    }
    public static void Quit()
    {
        EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }
}
