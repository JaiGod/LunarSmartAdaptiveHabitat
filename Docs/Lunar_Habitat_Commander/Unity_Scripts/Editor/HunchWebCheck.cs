// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchWebCheck.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Checks whether this Unity install can make a WebGL build (needed for Unity Play) and
//            writes the answer to HunchBridge/webgl_check.txt.
//  Contains: HunchWebCheck
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.IO;
using UnityEditor;
using UnityEngine;

/// Checks whether this Unity install can make a WebGL build (needed for Unity Play) and writes the answer to HunchBridge/webgl_check.txt.
public static class HunchWebCheck
{
    public static void Run()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        bool ok = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        string manifest = File.ReadAllText(Path.Combine(root, "Packages", "manifest.json"));
        File.WriteAllText(Path.Combine(root, "HunchBridge", "webgl_check.txt"),
            "unity " + Application.unityVersion + "\nwebgl module installed: " + ok + "\nwebgl publisher package: " + manifest.Contains("com.unity.connect.share") +
            "\neditor at: " + EditorApplication.applicationPath + "\n");
    }
}
