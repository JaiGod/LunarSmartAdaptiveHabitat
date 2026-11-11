// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchStoreSetup.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Finds and installs Meta's Platform SDK (store purchases) from the Unity package
//            registry, writing what happened to HunchBridge/store_setup.txt.
//  Contains: HunchStoreSetup
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

/// Finds and installs Meta's Platform SDK (store purchases) from the Unity package registry, writing what happened
/// to HunchBridge/store_setup.txt.
public static class HunchStoreSetup
{
    static SearchRequest search; static AddRequest add;
    static string Log { get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge", "store_setup.txt"); } }
    static void Note(string s) { File.AppendAllText(Log, DateTime.Now.ToString("HH:mm:ss") + " " + s + Environment.NewLine); }

    public static void Search()
    {
        File.WriteAllText(Log, "");
        search = Client.Search("com.meta.xr.sdk.platform");
        EditorApplication.update += PollSearch;
        Note("searching the Unity registry for com.meta.xr.sdk.platform");
    }

    static void PollSearch()
    {
        if (search == null || !search.IsCompleted) return;
        EditorApplication.update -= PollSearch;
        if (search.Status == StatusCode.Success && search.Result != null)
            foreach (var p in search.Result)
                Note("found " + p.name + " latest " + p.versions.latestCompatible + " | recent: " + string.Join(", ", p.versions.compatible.Reverse().Take(6)));
        else Note("search failed: " + (search.Error != null ? search.Error.message : "no result"));
        search = null;
    }

    /// Installs the Platform SDK at the same version as the Meta Voice SDK already in the project.
    public static void Install()
    {
        string ver = "85.0.1";
        string verFile = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge", "store_version.txt");
        if (File.Exists(verFile)) ver = File.ReadAllText(verFile).Trim();
        add = Client.Add("com.meta.xr.sdk.platform@" + ver);
        EditorApplication.update += PollAdd;
        Note("installing com.meta.xr.sdk.platform@" + ver);
    }

    static void PollAdd()
    {
        if (add == null || !add.IsCompleted) return;
        EditorApplication.update -= PollAdd;
        Note(add.Status == StatusCode.Success ? "installed " + add.Result.name + " " + add.Result.version + " | depends on: " + string.Join(", ", add.Result.dependencies.Select(d => d.name + "@" + d.version))
                                              : "install failed: " + (add.Error != null ? add.Error.message : "?"));
        add = null;
    }
}
