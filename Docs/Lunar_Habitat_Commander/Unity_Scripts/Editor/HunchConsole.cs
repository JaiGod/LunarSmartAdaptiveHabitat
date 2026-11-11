// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchConsole.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Development helper: writes what the Unity Console window is showing to
//            <project>/HunchBridge/console.txt, the same messages counted, so the warnings and
//            errors can be read and fixed.
//  Contains: HunchConsole
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Development helper: writes what the Unity Console window is showing to <project>/HunchBridge/console.txt,
/// the same messages counted, so the warnings and errors can be read and fixed.
public static class HunchConsole
{
    public static void Dump()
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HunchBridge")); var sb = new StringBuilder();
        try
        {
            const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var asm = typeof(Editor).Assembly; var T = asm.GetType("UnityEditor.LogEntries"); var E = asm.GetType("UnityEditor.LogEntry");
            int n = (int)T.GetMethod("GetCount", F).Invoke(null, null); sb.AppendLine("entries " + n);
            T.GetMethod("StartGettingEntries", F).Invoke(null, null);
            var e = Activator.CreateInstance(E); var get = T.GetMethod("GetEntryInternal", F); var fm = E.GetField("message", F); var fmode = E.GetField("mode", F);
            var count = new Dictionary<string, int>(); var order = new List<string>();
            for (int i = 0; i < n && i < 2000; i++)
            {
                get.Invoke(null, new object[] { i, e }); string m = (string)fm.GetValue(e) ?? ""; int mode = fmode != null ? Convert.ToInt32(fmode.GetValue(e)) : 0;
                var lines = m.Split('\n'); string head = lines[0]; for (int k = 1; k < lines.Length && k < 7; k++) head += "\n      " + lines[k];
                string key = "[mode " + mode.ToString("X") + "] " + head;
                if (count.ContainsKey(key)) count[key]++; else { count[key] = 1; order.Add(key); }
            }
            T.GetMethod("EndGettingEntries", F).Invoke(null, null);
            foreach (var k in order) sb.AppendLine("x" + count[k] + " " + k);
        }
        catch (Exception ex) { sb.AppendLine("could not read the console: " + ex); }
        File.WriteAllText(Path.Combine(dir, "console.txt"), sb.ToString());
    }
}
