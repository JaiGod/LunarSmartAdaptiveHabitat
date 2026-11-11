// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Editor tool
//  File    : HunchBlenderRun.cs  (Editor only)
//  Purpose : Runs Blender in the background (no window) with a Python script, e.g. to make a light
//            web copy of the habitat model. Args in HunchBridge/blender_args.txt: blend=, script=, exe=(optional).
//            The .blend is opened read-only by the script (never saved). Output: HunchBridge/blender_out.txt
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

public static class HunchBlenderRun
{
    static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HunchBridge"));
    public static void Run()
    {
        var a = new Dictionary<string, string>();
        foreach (var l in File.ReadAllLines(Path.Combine(Dir, "blender_args.txt"))) { int i = l.IndexOf('='); if (i > 0) a[l.Substring(0, i).Trim()] = l.Substring(i + 1).Trim(); }
        string exe = a.TryGetValue("exe", out var e) ? e : @"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe";
        string outFile = Path.Combine(Dir, "blender_out.txt");
        File.WriteAllText(outFile, DateTime.Now.ToString("HH:mm:ss") + " started\n");
        new Thread(() =>
        {
            try
            {
                var psi = new ProcessStartInfo(exe, $"-b \"{a["blend"]}\" --python \"{a["script"]}\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                {
                    string so = p.StandardOutput.ReadToEnd(), se = p.StandardError.ReadToEnd(); p.WaitForExit();
                    if (so.Length > 20000) so = so.Substring(so.Length - 20000);
                    File.AppendAllText(outFile, DateTime.Now.ToString("HH:mm:ss") + " exit " + p.ExitCode + "\n--- stdout ---\n" + so + "\n--- stderr ---\n" + (se.Length > 6000 ? se.Substring(se.Length - 6000) : se));
                }
            }
            catch (Exception ex) { File.AppendAllText(outFile, "ERROR " + ex.Message); }
        }) { IsBackground = true }.Start();
    }
}
#endif
