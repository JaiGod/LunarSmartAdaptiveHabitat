// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchProbe.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Development helper used together with HunchBridge: while the game is playing in the
//            Editor, runs a small script from Commands: wait s | start | explore | goto zoneId |
//            act | spawn type | speed x | day d | look yaw pitch | tp x y z |
//  Contains: HunchProbe
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Development helper used together with HunchBridge: while the game is playing in the Editor, runs a small script from
/// <project>/HunchBridge/probe.txt (one command per line) and writes what happened to probe_out.txt.
/// Commands: wait s | start | explore | goto zoneId | act | spawn type | speed x | day d | look yaw pitch | tp x y z |
///           shot name | dump | mute | call Type.Method | set Type.field value | stop
public static class HunchProbe
{
    static readonly string Dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HunchBridge"));
    static Queue<string> queue; static double waitUntil; static StringBuilder outp; static int errors; static bool hooked;

    public static void Run()
    {
        if (!EditorApplication.isPlaying) { File.WriteAllText(Path.Combine(Dir, "probe_out.txt"), "not playing"); return; }
        queue = new Queue<string>(File.ReadAllLines(Path.Combine(Dir, "probe.txt")));
        outp = new StringBuilder(); errors = 0; waitUntil = 0;
        if (!hooked) { hooked = true; Application.logMessageReceived += OnLog; EditorApplication.update += Tick; }
        Application.runInBackground = true;
    }

    static void OnLog(string c, string st, LogType t)
    {
        if (outp == null) return;
        if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) { errors++; if (errors <= 12) outp.AppendLine("!! " + t + ": " + c + "\n" + st); }
        else if (c.StartsWith("[HUNCH]")) outp.AppendLine("log: " + c);
    }

    static void Flush(bool done) { try { File.WriteAllText(Path.Combine(Dir, "probe_out.txt"), outp + (done ? "\nDONE errors=" + errors : "\nRUNNING")); } catch { } }

    static float Num(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

    static void Tick()
    {
        if (queue == null) return;
        if (!EditorApplication.isPlaying) { outp.AppendLine("play mode ended"); Flush(true); queue = null; return; }
        if (EditorApplication.timeSinceStartup < waitUntil) return;
        if (queue.Count == 0) { Flush(true); queue = null; return; }
        string line = queue.Dequeue().Trim(); if (line.Length == 0 || line.StartsWith("#")) return;
        var a = line.Split(' ');
        try
        {
            var game = UnityEngine.Object.FindFirstObjectByType<HabitatGame>();
            switch (a[0])
            {
                case "wait": waitUntil = EditorApplication.timeSinceStartup + Num(a[1]); break;
                case "start": game.StartMission(false); break;
                case "explore": game.StartMission(true); break;
                case "goto": game.GoTo(game.ZoneIndex(a[1])); break;
                case "act": game.DoInteract(); break;
                case "spawn": game.sim.Spawn(a[1]); break;
                case "speed": Time.timeScale = Num(a[1]); break;
                case "day": game.sim.missionSeconds = Num(a[1]) * HabitatSim.SecondsPerDay; break;
                case "mute": AudioListener.volume = 0f; break;
                case "unfreeze": game.sim.timeFrozen = false; break;
                case "five": game.GetComponent<HunchKidFeatures>().HighFive(int.Parse(a[1])); break;
                case "look": game.GetComponent<HunchDesktopFallback>().SetLook(Num(a[1]), Num(a[2])); break;
                case "tp": { var o = game.origin; var cc = o.GetComponent<CharacterController>(); if (cc) cc.enabled = false; o.transform.position = new Vector3(Num(a[1]), Num(a[2]), Num(a[3])); if (cc) cc.enabled = true; Physics.SyncTransforms(); break; }
                case "shot": ScreenCapture.CaptureScreenshot(Path.Combine(Dir, a[1] + ".png")); waitUntil = EditorApplication.timeSinceStartup + 0.6; break;
                case "cam":   // cam name [fov] : renders the player's camera straight to a 2560x1440 picture (no desktop overlay text)
                    {
                        var c = game.origin.Camera; const int w = 2560, h = 1440;
                        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                        var prevT = c.targetTexture; float prevFov = c.fieldOfView;
                        if (a.Length > 2) c.fieldOfView = Num(a[2]);
                        c.targetTexture = rt; c.aspect = (float)w / h; c.Render();
                        var prevA = RenderTexture.active; RenderTexture.active = rt;
                        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                        RenderTexture.active = prevA; c.targetTexture = prevT; c.ResetAspect(); c.fieldOfView = prevFov;
                        File.WriteAllBytes(Path.Combine(Dir, a[1] + ".jpg"), tex.EncodeToJPG(95));
                        UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                        outp.AppendLine("cam " + a[1] + " pos=" + c.transform.position.ToString("F1") + " fwd=" + c.transform.forward.ToString("F2"));
                        waitUntil = EditorApplication.timeSinceStartup + 0.3; break;
                    }
                case "stop": EditorApplication.isPlaying = false; break;
                case "call":
                    {
                        int dot = a[1].LastIndexOf('.'); var t = Find(a[1].Substring(0, dot)); string mn = a[1].Substring(dot + 1);
                        var m = t.GetMethod(mn, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                        object target = m.IsStatic ? null : UnityEngine.Object.FindFirstObjectByType(t);
                        var r = m.Invoke(target, null); outp.AppendLine(line + " -> " + r); break;
                    }
                case "set":
                    {
                        int dot = a[1].LastIndexOf('.'); var t = Find(a[1].Substring(0, dot)); var f = t.GetField(a[1].Substring(dot + 1), BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        object target = f.IsStatic ? null : UnityEngine.Object.FindFirstObjectByType(t);
                        object v = f.FieldType == typeof(bool) ? (object)(a[2] == "true") : (f.FieldType == typeof(int) ? (object)int.Parse(a[2]) : (f.FieldType == typeof(float) ? (object)Num(a[2]) : (object)string.Join(" ", a, 2, a.Length - 2)));
                        f.SetValue(target, v); break;
                    }
                case "dump":
                    {
                        var sim = game.sim; var fx = game.GetComponent<HunchWorldFX>();
                        outp.AppendLine("phase=" + sim.phase + " day=" + sim.Day + " hour=" + sim.Hour.ToString("F1") + " frozen=" + sim.timeFrozen + " near=" + game.NearZone + " menu=" + game.menuOpen + " headset=" + game.HeadsetActive);
                        outp.AppendLine("alerts=" + sim.alerts.Count + " msg=" + sim.message);
                        foreach (var al in sim.alerts) outp.AppendLine("  alert " + al.type + " @" + al.zoneId + " " + al.secondsLeft.ToString("F0") + "s");
                        outp.AppendLine("worldfx=" + (fx ? "yes doors=" + fx.DoorCount + " outside=" + fx.Outside : "MISSING") + " packEffects=" + HunchFx.Count);
                        int n = 0; var names = new SortedDictionary<string, int>();
                        foreach (var go in UnityEngine.Object.FindObjectsByType<HunchFxLife>(FindObjectsSortMode.None)) n++;
                        foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                        { var root = ps.transform.root.name; if (!root.StartsWith("FX ")) continue; int c; names.TryGetValue(root, out c); names[root] = c + ps.particleCount; }
                        outp.AppendLine("fxFading=" + n); foreach (var kv in names) outp.AppendLine("  " + kv.Key + " particles=" + kv.Value);
                        var cam = game.origin.Camera.transform; outp.AppendLine("cam=" + cam.position.ToString("F1") + " fwd=" + cam.forward.ToString("F2") + " screen=" + Screen.width + "x" + Screen.height + " fps=" + (1f / Time.smoothDeltaTime).ToString("F0"));
                        break;
                    }
                default: outp.AppendLine("unknown: " + line); break;
            }
        }
        catch (Exception e) { outp.AppendLine("FAILED " + line + ": " + (e.InnerException ?? e).Message); }
        Flush(false);
    }

    static Type Find(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) { var t = asm.GetType(name); if (t != null) return t; }
        throw new Exception("type not found " + name);
    }
}
