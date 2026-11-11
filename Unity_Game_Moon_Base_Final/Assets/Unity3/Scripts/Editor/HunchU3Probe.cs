// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/Editor/HunchU3Probe.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): scene/material builder, Blender import, the
//            HunchBridge automation, probes, USB/VR/web build helpers.
//  Purpose : Development helper used with HunchBridge (Unity 3): while playing in the Editor, runs
//            <project>/HunchBridge/probe.txt one line at a time and writes probe_out.txt. Commands:
//            wait s | sandbox | time x | hours h | wp WP_Name yaw | cam name |
//  Contains: HunchU3Probe
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Hunch3;

/// Development helper used with HunchBridge (Unity 3): while playing in the Editor, runs <project>/HunchBridge/probe.txt
/// one line at a time and writes probe_out.txt. Commands: wait s | sandbox | time x | hours h | wp WP_Name yaw | cam name |
/// shot name | panel id | close | ask crewId jobIndex | pick crewId | jobs | call Type.Method | dump | stop
public static class HunchU3Probe
{
    static readonly string Dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HunchBridge"));
    static Queue<string> q; static double until; static StringBuilder o; static int errors; static bool hooked;
    public static void Scene() { if (!EditorApplication.isPlaying) UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Unity3/Scenes/00_Menu_VRBox.unity"); }
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { File.WriteAllText(Path.Combine(Dir, "probe_out.txt"), "not playing"); return; }
        q = new Queue<string>(File.ReadAllLines(Path.Combine(Dir, "probe.txt"))); o = new StringBuilder(); errors = 0; until = 0;
        if (!hooked) { hooked = true; Application.logMessageReceived += (c, st, t) => { if (o == null) return; if (t == LogType.Error || t == LogType.Exception) { errors++; if (errors <= 12) o.AppendLine("!! " + t + ": " + c + "\n" + st); } }; EditorApplication.update += Tick; }
        Application.runInBackground = true;
    }
    static float N(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    static void Flush(bool done) { try { File.WriteAllText(Path.Combine(Dir, "probe_out.txt"), o + (done ? "\nDONE errors=" + errors : "\nRUNNING")); } catch { } }
    static void Tick()
    {
        if (q == null) return;
        if (!EditorApplication.isPlaying) { o.AppendLine("play mode ended"); Flush(true); q = null; return; }
        if (EditorApplication.timeSinceStartup < until) return;
        if (q.Count == 0) { Flush(true); q = null; return; }
        string line = q.Dequeue().Trim(); if (line.Length == 0 || line.StartsWith("#")) return; var a = line.Split(' ');
        try
        {
            switch (a[0])
            {
                case "wait": until = EditorApplication.timeSinceStartup + N(a[1]); break;
                case "sandbox": MenuBoot.Go(GameMode.Sandbox); break;
                case "time": GameState.timeScale = N(a[1]); break;
                case "hours": { float h = N(a[1]); int steps = Mathf.RoundToInt(h * 60); for (int i = 0; i < steps; i++) Sim.I.engine.Step(1f / 60f); o.AppendLine("advanced " + h + " h → " + SimClock.Now); break; }
                case "wp": PlayerRig.I.Teleport(Nav.wp[a[1]].position, a.Length > 2 ? N(a[2]) : float.NaN); break;
                case "cam":
                    {
                        var c = PlayerRig.I.cam; const int w = 1920, h = 1080; var rt = new RenderTexture(w, h, 24); var prev = c.targetTexture;
                        c.targetTexture = rt; c.Render(); var pa = RenderTexture.active; RenderTexture.active = rt;
                        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                        RenderTexture.active = pa; c.targetTexture = prev; File.WriteAllBytes(Path.Combine(Dir, a[1] + ".jpg"), tex.EncodeToJPG(90));
                        UnityEngine.Object.DestroyImmediate(tex); rt.Release(); o.AppendLine("cam " + a[1] + " at " + c.transform.position.ToString("F1")); break;
                    }
                case "wake": if (SleepSystem.PlayerAsleep) SleepSystem.I?.Wake("probe"); break;
                case "maxgame": { var gt = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"); var gw = EditorWindow.GetWindow(gt); gw.maximized = a.Length < 2 || a[1] != "0"; until = EditorApplication.timeSinceStartup + 1.5; break; }
                case "shot": ScreenCapture.CaptureScreenshot(Path.Combine(Dir, a[1] + ".png")); until = EditorApplication.timeSinceStartup + 0.6; break;
                case "panel": Panels.Open(a[1]); break;
                case "close": Panels.CloseAll(); break;
                case "ask": { int id = int.Parse(a[1]); var j = CrewOrders.JobsFor(id).ToList()[int.Parse(a[2])]; o.AppendLine("ask " + id + " " + j.name + " -> " + CrewOrders.Ask(id, j)); break; }
                case "pick": o.AppendLine("pick " + a[1] + " -> " + CrewOrders.LetThemPick(int.Parse(a[1]))); break;
                case "jobs": foreach (int id in new[] { 2, 3, 4 }) { var ag = CrewAgent.All.FirstOrDefault(x => x.crewId == id); o.AppendLine($"  crew {id}: {CrewOrders.Status(id)} | task='{ag?.task}' pos={ag?.transform.position.ToString("F1")}"); } o.AppendLine("  done=" + CrewOrders.done + " " + string.Join(" / ", CrewOrders.history)); break;
                case "dump": o.AppendLine($"{SimClock.Now} agents={CrewAgent.All.Count} wp={Nav.wp.Count} panels={Panels.AnyOpen} timeScale={GameState.timeScale}"); break;
                case "call":
                    {
                        int dot = a[1].LastIndexOf('.'); Type t = null; foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) { t = asm.GetType(a[1].Substring(0, dot)); if (t != null) break; }
                        var m = t.GetMethod(a[1].Substring(dot + 1), BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                        object target = m.IsStatic ? null : UnityEngine.Object.FindFirstObjectByType(t); o.AppendLine(line + " -> " + m.Invoke(target, null)); break;
                    }
                case "scan": HealthCheck.Scan(int.Parse(a[1])); o.AppendLine("scan " + a[1] + ":\n" + HealthCheck.last[int.Parse(a[1])]); break;
                case "quick": HealthCheck.Quick(); o.AppendLine("quick -> last " + HealthCheck.lastId); break;
                case "donext": { var t = RubricTasks.I.Current; o.AppendLine("ENTER on [" + (RubricTasks.I.InMission ? "mission " + (RubricTasks.I.mIndex + 1) + "." + (RubricTasks.I.mStep + 1) : t?.id) + "]"); RubricTasks.I.DoNext(); break; }
                case "tasks": o.AppendLine("tasks " + string.Join(" ", RubricTasks.I.tasks.Select(t => (t.done ? "+" : "-") + t.id)) + "  current=" + RubricTasks.I.Current?.id); break;
                case "reset": RubricTasks.I.ResetAll(); break;
                case "mission": { var r = RubricTasks.I; var m = r.CurMission; o.AppendLine($"mission {r.mIndex + 1}/{r.missions.Count} {(m?.name)} step {r.mStep + 1}/{m?.steps.Count} :: {(m != null && r.mStep < m.steps.Count ? m.steps[r.mStep].text : "-")}  asleep={SleepSystem.PlayerAsleep} aiOff={AIAuthority.Blocked} {SimClock.Now}"); break; }
                case "mreset": RubricTasks.I.ResetMissions(); break;
                case "key": o.AppendLine("press O"); AIAuthority.Toggle(); break;
                case "day": { var r = RubricTasks.I; o.AppendLine("daily: " + r.DailyDebug()); break; }
                case "menu": Panels.Open("menu"); break;
                case "mset": RubricTasks.I.mIndex = int.Parse(a[1]); break;
                case "haz": o.AppendLine("hazard active=" + (Hazards.I && Hazards.I.Active) + " kind=" + (Hazards.I ? Hazards.I.Kind : "") + " rivera onEVA=" + Sim.I.engine.C(4).onEVA + " hod=" + SimClock.HourOfDay + " asked=" + string.Join(";", CrewOrders.asked.Select(x => x.id + ":" + x.job + "@" + x.h.ToString("F2")))); break;
                case "mskip": RubricTasks.I.mIndex = RubricTasks.I.missions.Count; break;
                case "nighttest": RubricTasks.I.ArmNightTest(); break;
                case "hazard": Hazards.I.Trigger(Hazards.Kinds[1]); break;
                case "dotask": { var r = RubricTasks.I; var t = r.tasks.FirstOrDefault(x => x.id == a[1]); if (t != null) { t.done = false; r.DoTask(t); o.AppendLine("dotask " + a[1]); } else o.AppendLine("no task " + a[1]); break; }
                case "doc": { string q = string.Join(" ", a.Skip(1)); int i = DesignDocs.Pages.FindIndex(p => p.title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0); if (i >= 0) { DesignDocs.page = i; Panels.CloseAll(); Panels.Open("docs"); } o.AppendLine("doc " + q + " -> " + i); break; }
                case "scen": Scenarios.Run(int.Parse(a[1])); Panels.CloseAll(); Panels.Open("scenarios"); break;
                case "bigmap": HUD.I?.ToggleBigMap(); break;
                case "help": HUD.I?.ToggleHelp(); break;
                case "today": RubricTasks.I.ShowToday(); break;
                case "only": foreach (var t in RubricTasks.I.tasks) t.done = t.id != a[1]; break;
                case "state": o.AppendLine("cur=" + RubricTasks.I.Current?.id + " inMission=" + RubricTasks.I.InMission + " alarm=" + (Hazards.I && Hazards.I.Active) + " asleep=" + SleepSystem.PlayerAsleep + " board=" + RubricTasks.I.BoardDone().Count + " " + SimClock.Now); break;
                case "crewtab": Panels.CloseAll(); Panels.crewTab = a[1]; Panels.Open("crew"); break;
                case "stop": EditorApplication.isPlaying = false; break;
                default: o.AppendLine("unknown: " + line); break;
            }
        }
        catch (Exception e) { o.AppendLine("FAILED " + line + ": " + (e.InnerException ?? e).Message); }
        Flush(false);
    }
}
