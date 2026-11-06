#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;

/// VR check + USB copy helpers, driven through the HunchBridge (invoke HunchUsb.Method).
/// Settings come from HunchBridge/usb_args.txt (key=value lines). Results go to HunchBridge/usb_out.txt.
public static class HunchUsb
{
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static string Dir => Path.Combine(Root, "HunchBridge");
    static Dictionary<string, string> Args()
    {
        var d = new Dictionary<string, string>();
        try { foreach (var l in File.ReadAllLines(Path.Combine(Dir, "usb_args.txt"))) { int i = l.IndexOf('='); if (i > 0) d[l.Substring(0, i).Trim()] = l.Substring(i + 1).Trim(); } } catch { }
        return d;
    }
    static void Out(string s) { try { File.WriteAllText(Path.Combine(Dir, "usb_out.txt"), DateTime.Now.ToString("HH:mm:ss") + "\n" + s); } catch { } }

    static readonly string[] Skip = { "Library", "Temp", "Logs", "obj", "UserSettings", ".vs", ".utmp", "HunchBridge" };

    static long Size(string dir, bool skipJunk, ref int files)
    {
        long t = 0;
        try
        {
            foreach (var f in Directory.GetFiles(dir)) { try { t += new FileInfo(f).Length; files++; } catch { } }
            foreach (var d in Directory.GetDirectories(dir))
            {
                string n = Path.GetFileName(d);
                if (skipJunk && Skip.Contains(n)) continue;
                t += Size(d, skipJunk, ref files);
            }
        }
        catch { }
        return t;
    }

    static string XrFor(BuildTargetGroup g)
    {
        var s = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(g);
        if (s == null || s.Manager == null) return g + ": no XR settings";
        var loaders = s.Manager.activeLoaders.Select(l => l ? l.name : "null");
        return g + ": init on start=" + s.InitManagerOnStart + " loaders=[" + string.Join(", ", loaders) + "]";
    }

    public static void Info()
    {
        var a = Args(); var sb = new StringBuilder();
        sb.AppendLine("project " + Root + " · Unity " + Application.unityVersion + " · active target " + EditorUserBuildSettings.activeBuildTarget);
        sb.AppendLine(XrFor(BuildTargetGroup.Standalone));
        sb.AppendLine(XrFor(BuildTargetGroup.Android));
        sb.AppendLine("scenes in build: " + string.Join(" | ", EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)));
        sb.AppendLine("android module: " + BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android) + " · windows module: " + BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64));
        foreach (var di in DriveInfo.GetDrives())
            try { if (di.IsReady) sb.AppendLine($"drive {di.Name} {di.DriveType} {di.DriveFormat} label='{di.VolumeLabel}' free {di.AvailableFreeSpace / 1e9:0.0} GB of {di.TotalSize / 1e9:0.0} GB"); } catch { }
        foreach (var k in new[] { "size1", "size2", "size3", "size4" })
            if (a.TryGetValue(k, out var p) && Directory.Exists(p)) { int n = 0; long s = Size(p, true, ref n); sb.AppendLine($"size(no Library) {p}: {s / 1e9:0.00} GB in {n} files"); }
        Out(sb.ToString());
    }

    /// in play mode: is XR really running, and is a headset connected?
    public static void VRLive()
    {
        var sb = new StringBuilder();
        sb.AppendLine("playing=" + EditorApplication.isPlaying);
        var m = XRGeneralSettings.Instance ? XRGeneralSettings.Instance.Manager : null;
        sb.AppendLine("active loader: " + (m && m.activeLoader ? m.activeLoader.name : "none") + " · initialization complete " + (m && m.isInitializationComplete));
        var disp = new List<XRDisplaySubsystem>(); SubsystemManager.GetSubsystems(disp);
        foreach (var d in disp) sb.AppendLine("display subsystem " + d.subsystemDescriptor.id + " running=" + d.running);
        var devs = new List<InputDevice>(); InputDevices.GetDevices(devs);
        foreach (var d in devs) sb.AppendLine("device: " + d.name + " (" + d.characteristics + ")");
        try { sb.AppendLine("OpenXR runtime: " + UnityEngine.XR.OpenXR.OpenXRRuntime.name + " " + UnityEngine.XR.OpenXR.OpenXRRuntime.version); } catch (Exception e) { sb.AppendLine("OpenXR runtime: ? " + e.Message); }
        sb.AppendLine("XRSettings.isDeviceActive=" + XRSettings.isDeviceActive + " loadedDevice=" + XRSettings.loadedDeviceName);
        var cam = Camera.main; sb.AppendLine("main camera: " + (cam ? cam.name + " stereo=" + cam.stereoEnabled : "none"));
        Out(sb.ToString());
    }

    /// start XR by hand (only used if the project does not start XR on launch)
    public static void StartXR()
    {
        var m = XRGeneralSettings.Instance ? XRGeneralSettings.Instance.Manager : null;
        if (m == null) { Out("no XR manager"); return; }
        if (!m.isInitializationComplete) m.InitializeLoaderSync();
        if (m.activeLoader) m.StartSubsystems();
        VRLive();
    }

    static int copied, skipped; static long bytes; static readonly StringBuilder errs = new StringBuilder();
    static void Copy(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src))
        {
            string t = Path.Combine(dst, Path.GetFileName(f));
            try
            {
                var fi = new FileInfo(f);
                if (File.Exists(t) && new FileInfo(t).Length == fi.Length && File.GetLastWriteTimeUtc(t) >= fi.LastWriteTimeUtc) { skipped++; continue; }
                File.Copy(f, t, true); copied++; bytes += fi.Length;
            }
            catch (Exception e) { if (errs.Length < 4000) errs.AppendLine(f + ": " + e.Message); }
        }
        foreach (var d in Directory.GetDirectories(src))
        {
            string n = Path.GetFileName(d);
            if (Skip.Contains(n)) continue;
            Copy(d, Path.Combine(dst, n));
        }
    }

    /// copy src -> dst (skipping Library/Temp/Logs/...); with wipe=1 the old dst is deleted first
    public static void CopyTree()
    {
        var a = Args(); copied = skipped = 0; bytes = 0; errs.Clear();
        string src = a["src"], dst = a["dst"];
        var t0 = DateTime.Now;
        try
        {
            if (a.TryGetValue("wipe", out var w) && w == "1" && Directory.Exists(dst))
            {
                if (dst.Length < 6 || !dst.StartsWith("D:\\", StringComparison.OrdinalIgnoreCase)) throw new Exception("refusing to wipe " + dst);
                Directory.Delete(dst, true);
            }
            Copy(src, dst);
            Out($"copied {copied} files ({bytes / 1e9:0.00} GB), unchanged {skipped}, in {(DateTime.Now - t0).TotalMinutes:0.0} min\n{src} -> {dst}\n" + errs);
        }
        catch (Exception e) { Out("FAILED: " + e + "\n" + errs); }
    }

    /// Windows VR build (PC VR through Quest Link / Air Link)
    public static void BuildWin()
    {
        var a = Args(); string outDir = a["out"]; string exe = a.TryGetValue("exe", out var x) ? x : "Game.exe";
        Directory.CreateDirectory(outDir);
        var scenes = a.TryGetValue("scenes", out var sc) && sc.Length > 0 ? sc.Split('|') : EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = Path.Combine(outDir, exe), target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone, options = BuildOptions.None });
        Out("build " + r.summary.result + " · " + r.summary.totalErrors + " errors · " + (r.summary.totalSize / 1e6).ToString("0") + " MB · " + r.summary.totalTime + "\n" + string.Join("\n", r.steps.SelectMany(s => s.messages).Where(mm => mm.type == LogType.Error).Take(20).Select(mm => mm.content)));
    }
}
#endif
