// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Bridge/Editor/HunchBridge.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Development helper. Lets the assistant drive this open Editor through small text files
//            in <project>/HunchBridge: cmd.txt line 1 = an id, line 2 = a command (refresh | invoke
//            Type.Method | play | stop | menu Path/To/Item | shot file.png)
//  Contains: HunchBridge
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// Development helper. Lets the assistant drive this open Editor through small text files in <project>/HunchBridge:
///   cmd.txt      line 1 = an id, line 2 = a command (refresh | invoke Type.Method | play | stop | menu Path/To/Item | shot file.png)
///   result_<id>.txt, status.txt and compile.txt are written back.
/// It only calls static methods on classes whose name starts with "Hunch". Delete the Bridge folder to remove it.
[InitializeOnLoad]
public static class HunchBridge
{
    static readonly string Dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HunchBridge"));
    static double next;
    static readonly List<string> compileErrors = new List<string>();
    static readonly StringBuilder log = new StringBuilder();
    static bool capturing;

    static HunchBridge()
    {
        try { Directory.CreateDirectory(Dir); } catch { }
        EditorApplication.update += Tick;
        CompilationPipeline.compilationStarted += _ => compileErrors.Clear();
        CompilationPipeline.assemblyCompilationFinished += OnAssembly;
        CompilationPipeline.compilationFinished += _ => WriteCompile();
        Application.logMessageReceived += OnLog;
        Status("loaded");
        if (!File.Exists(Path.Combine(Dir, "compile.txt"))) WriteCompile();
    }

    static void OnAssembly(string assembly, CompilerMessage[] messages)
    {
        foreach (var m in messages)
            if (m.type == CompilerMessageType.Error) compileErrors.Add(m.file + "(" + m.line + "): " + m.message);
    }

    static void WriteCompile()
    {
        var sb = new StringBuilder();
        sb.AppendLine(DateTime.Now.ToString("HH:mm:ss") + " " + (compileErrors.Count == 0 ? "OK" : "ERRORS " + compileErrors.Count));
        foreach (var e in compileErrors) sb.AppendLine(e);
        Safe(() => File.WriteAllText(Path.Combine(Dir, "compile.txt"), sb.ToString()));
    }

    static void OnLog(string condition, string stack, LogType type)
    {
        if (!capturing) return;
        if (type == LogType.Log && !condition.StartsWith("[HUNCH]")) return;
        if (log.Length < 60000) log.AppendLine(type + ": " + condition + (type == LogType.Exception || type == LogType.Error ? "\n" + stack : ""));
    }

    static void Safe(Action a) { try { a(); } catch { } }

    static void Status(string note)
    {
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Safe(() => File.WriteAllText(Path.Combine(Dir, "status.txt"),
            DateTime.Now.ToString("HH:mm:ss") + " " + note + " playing=" + EditorApplication.isPlaying + " compiling=" + EditorApplication.isCompiling + " updating=" + EditorApplication.isUpdating + " scene=" + scene + " errors=" + compileErrors.Count));
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 1.5;
        Status("alive");
        string cmdPath = Path.Combine(Dir, "cmd.txt");
        if (!File.Exists(cmdPath)) return;
        string[] lines;
        try { lines = File.ReadAllLines(cmdPath); File.Delete(cmdPath); } catch { return; }
        if (lines.Length < 2) return;
        string id = lines[0].Trim(), cmd = lines[1].Trim();
        string result;
        log.Length = 0; capturing = true;
        try { result = Run(cmd); }
        catch (Exception e) { result = "EXCEPTION " + (e.InnerException ?? e); }
        capturing = false;
        Safe(() => File.WriteAllText(Path.Combine(Dir, "result_" + id + ".txt"), cmd + "\n" + result + "\n--- log ---\n" + log));
    }

    static string Run(string cmd)
    {
        if (cmd == "refresh") { AssetDatabase.Refresh(); return "refresh requested"; }
        if (cmd == "play") { EditorApplication.isPlaying = true; return "entering play mode"; }
        if (cmd == "stop") { EditorApplication.isPlaying = false; return "leaving play mode"; }
        if (cmd.StartsWith("menu ")) return "menu " + EditorApplication.ExecuteMenuItem(cmd.Substring(5).Trim());
        if (cmd.StartsWith("shot "))
        {
            string p = Path.Combine(Dir, cmd.Substring(5).Trim());
            ScreenCapture.CaptureScreenshot(p); return "screenshot requested " + p;
        }
        if (cmd.StartsWith("invoke "))
        {
            if (EditorApplication.isCompiling) return "BUSY compiling";
            string full = cmd.Substring(7).Trim(); int dot = full.LastIndexOf('.');
            if (dot < 0) return "bad name";
            string typeName = full.Substring(0, dot), method = full.Substring(dot + 1);
            if (!typeName.StartsWith("Hunch")) return "refused: only Hunch* classes";
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName);
                if (t == null) continue;
                var m = t.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (m == null) return "no such method";
                m.Invoke(null, null);
                return "invoked " + full;
            }
            return "type not found";
        }
        return "unknown command";
    }
}
