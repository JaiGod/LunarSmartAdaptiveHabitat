// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Editor tool
//  File    : HunchOrganize.cs  (Editor only)
//  Purpose : Moves scripts into the organised folder structure with AssetDatabase.MoveAsset, so every
//            script keeps its GUID and every scene/prefab reference keeps working.
//            Plan file: <project>/HunchBridge/organize.txt  (one "Assets/old/path.cs|Assets/new/path.cs" per line)
//            Result   : <project>/HunchBridge/organize_out.txt
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class HunchOrganize
{
    static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HunchBridge"));

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    public static void Apply()
    {
        var sb = new StringBuilder(); int ok = 0, skip = 0, fail = 0;
        var lines = File.ReadAllLines(Path.Combine(Dir, "organize.txt"));
        // Pass 1: create every destination folder BEFORE batching, so the asset database knows them.
        foreach (var raw in lines)
        {
            var l = raw.Trim(); if (l.Length == 0 || !l.Contains("|")) continue;
            EnsureFolder(Path.GetDirectoryName(l.Split('|')[1].Trim()).Replace('\\', '/'));
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        // Pass 2: move the scripts (GUIDs are kept, so scenes/prefabs keep their references).
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var raw in lines)
            {
                var l = raw.Trim(); if (l.Length == 0 || !l.Contains("|")) continue;
                var p = l.Split('|'); string src = p[0].Trim(), dst = p[1].Trim();
                if (!File.Exists(src)) { if (File.Exists(dst)) skip++; else { fail++; sb.AppendLine("missing " + src); } continue; }
                string err = AssetDatabase.MoveAsset(src, dst);
                if (string.IsNullOrEmpty(err)) ok++; else { fail++; sb.AppendLine(src + " -> " + err); }
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        // remove folders that are now empty (e.g. the old Scripts/Ares)
        foreach (var f in new[] { "Assets/HUNCH/Scripts/Ares" })
            if (AssetDatabase.IsValidFolder(f) && Directory.GetFiles(f, "*.cs", SearchOption.AllDirectories).Length == 0) AssetDatabase.DeleteAsset(f);
        AssetDatabase.Refresh();
        File.WriteAllText(Path.Combine(Dir, "organize_out.txt"), DateTime.Now.ToString("HH:mm:ss") + $"\nmoved {ok}, already there {skip}, failed {fail}\n" + sb);
    }

    /// Removes empty duplicate folders such as "01_Core 1" left behind by an earlier interrupted run.
    public static void Cleanup()
    {
        var sb = new StringBuilder(); int n = 0;
        foreach (var root in new[] { "Assets/Unity3/Scripts", "Assets/HUNCH/Scripts" })
        {
            if (!AssetDatabase.IsValidFolder(root)) continue;
            foreach (var d in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
            {
                var path = d.Replace('\\', '/');
                if (!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), @" \d+$")) continue;
                if (Directory.GetFiles(path, "*", SearchOption.AllDirectories).Any(f => !f.EndsWith(".meta"))) { sb.AppendLine("kept (not empty) " + path); continue; }
                if (AssetDatabase.DeleteAsset(path)) { n++; sb.AppendLine("removed " + path); } else sb.AppendLine("could not remove " + path);
            }
        }
        AssetDatabase.Refresh();
        File.WriteAllText(Path.Combine(Dir, "cleanup_out.txt"), DateTime.Now.ToString("HH:mm:ss") + $"\nremoved {n}\n" + sb);
    }
}
#endif
