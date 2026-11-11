// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchQuestBuild.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Builds the Meta Quest version as an .apk (the Meta Horizon Store does not take .aab
//            files), with the settings already in the project, then reads the finished .apk back
//            with the Android SDK's aapt2 so the store's checks can be confirmed before uploading.
//  Contains: HunchQuestBuild, HunchQuestGradleFix
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// Builds the Meta Quest version as an .apk (the Meta Horizon Store does not take .aab files), with the settings already in the project,
/// then reads the finished .apk back with the Android SDK's aapt2 so the store's checks can be confirmed before uploading.
public static class HunchQuestBuild
{
    static string Root { get { return Directory.GetParent(Application.dataPath).FullName; } }
    internal static string Bridge { get { return Path.Combine(Root, "HunchBridge"); } }
    internal static void Log(string s) { File.AppendAllText(Path.Combine(Bridge, "quest_build.txt"), DateTime.Now.ToString("HH:mm:ss") + " " + s + Environment.NewLine); }
    static string ApkPath { get { return Path.Combine(Root, "Builds", "LunarHabitatCommander.apk"); } }

    public static void Build()
    {
        File.WriteAllText(Path.Combine(Bridge, "quest_build.txt"), "");
        try
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) { Log("active platform is " + EditorUserBuildSettings.activeBuildTarget + ", switching to Android"); EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android); }
            EditorUserBuildSettings.buildAppBundle = false;      // .apk, not .aab
            PlayerSettings.Android.bundleVersionCode = Math.Max(PlayerSettings.Android.bundleVersionCode + 1, 3);   // every store upload needs a higher number
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            string apk = ApkPath;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Log("scenes: " + string.Join(", ", scenes) + " | package " + PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android) + " | version " + PlayerSettings.bundleVersion + " (" + PlayerSettings.Android.bundleVersionCode + ") | custom keystore " + PlayerSettings.Android.useCustomKeystore);
            var rep = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = apk, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.None });
            Log("result " + rep.summary.result + ", " + (rep.summary.totalSize / 1000000UL) + " MB, errors " + rep.summary.totalErrors + ", warnings " + rep.summary.totalWarnings + ", time " + rep.summary.totalTime);
            foreach (var st in rep.steps) foreach (var m in st.messages) if (m.type == LogType.Error || m.type == LogType.Exception) Log("ERROR " + m.content.Replace("\n", " | "));
            if (File.Exists(apk)) Log("apk " + new FileInfo(apk).Length / 1000000 + " MB at " + apk);
            Check();
        }
        catch (Exception e) { Log("FAILED " + e.Message); }
    }

    /// Reads the .apk with aapt2 and writes the store-relevant lines to HunchBridge/quest_check.txt.
    public static void Check()
    {
        var sb = new StringBuilder();
        try
        {
            string aapt = FindAapt2();
            sb.AppendLine("aapt2: " + (aapt ?? "not found"));
            if (aapt != null && File.Exists(ApkPath))
            {
                string badging = Run(aapt, "dump badging \"" + ApkPath + "\"");
                foreach (var l in badging.Split('\n')) if (l.StartsWith("package:") || l.Contains("sdkVersion") || l.StartsWith("application-label") || l.StartsWith("native-code")) sb.AppendLine(l.TrimEnd());
                string tree = Run(aapt, "dump xmltree --file AndroidManifest.xml \"" + ApkPath + "\"");
                File.WriteAllText(Path.Combine(Bridge, "quest_manifest.txt"), tree);
                var lines = tree.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i].Contains("supportedDevices") || lines[i].Contains("targetSdkVersion") || lines[i].Contains("minSdkVersion"))
                    {
                        sb.AppendLine(lines[i].Trim());
                        if (lines[i].Contains("supportedDevices") && i + 1 < lines.Length) sb.AppendLine(lines[i + 1].Trim());
                    }
                string perms = Run(aapt, "dump permissions \"" + ApkPath + "\"");
                foreach (var l in perms.Split('\n')) if (l.Contains("permission")) sb.AppendLine(l.Trim());
                foreach (var bad in new[] { "com.unity3d.ads", "ironsource", "AD_ID", "ADSERVICES", "vending.BILLING", "xr.api.SPATIAL", "xr.input.hand_tracking" })
                    sb.AppendLine("CHECK " + bad + (tree.Contains(bad) || perms.Contains(bad) ? " STILL THERE" : " gone"));
                bool stanley = tree.Contains("stanley");
                var m = Regex.Match(badging, "targetSdkVersion:'(\\d+)'");
                int target = m.Success ? int.Parse(m.Groups[1].Value) : -1;
                sb.AppendLine("CHECK devices " + (stanley ? "BAD (stanley still there)" : "ok") + " | target sdk " + target + (target >= 0 && target <= HunchQuestGradleFix.MaxTarget ? " ok" : " BAD"));
            }
        }
        catch (Exception e) { sb.AppendLine("check FAILED " + e.Message); }
        File.WriteAllText(Path.Combine(Bridge, "quest_check.txt"), DateTime.Now.ToString("HH:mm:ss") + Environment.NewLine + sb);
    }

    static string FindAapt2()
    {
        var sdks = new System.Collections.Generic.List<string>();
        string props = Path.Combine(Root, "Library", "Bee", "Android", "Prj", "IL2CPP", "Gradle", "local.properties");
        if (File.Exists(props))
            foreach (var l in File.ReadAllLines(props))
                if (l.StartsWith("sdk.dir=")) sdks.Add(l.Substring(8).Replace("\\:", ":").Replace("\\\\", "\\"));
        string env = Environment.GetEnvironmentVariable("ANDROID_HOME"); if (!string.IsNullOrEmpty(env)) sdks.Add(env);
        foreach (var sdk in sdks)
        {
            string bt = Path.Combine(sdk, "build-tools");
            if (!Directory.Exists(bt)) continue;
            foreach (var d in Directory.GetDirectories(bt).OrderByDescending(x => x))
            {
                string exe = Path.Combine(d, Application.platform == RuntimePlatform.WindowsEditor ? "aapt2.exe" : "aapt2");
                if (File.Exists(exe)) return exe;
            }
        }
        return null;
    }

    static string Run(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var p = Process.Start(psi))
        {
            string o = p.StandardOutput.ReadToEnd();
            string e = p.StandardError.ReadToEnd();
            p.WaitForExit(60000);
            return o + (string.IsNullOrEmpty(e) ? "" : "\nSTDERR " + e);
        }
    }
}

/// The Meta Horizon Store only accepts its own device names in com.oculus.supportedDevices, and a target SDK of 34 or lower.
/// This runs last, after Unity and the XR plugins have written the Android project, and fixes both before Gradle builds the .apk.
public class HunchQuestGradleFix : IPostGenerateGradleAndroidProject
{
    public const string Devices = "quest2|questpro|quest3|quest3s";
    public const int MaxTarget = 35;
    public static readonly string[] RemovePermissions = {
        "com.google.android.gms.permission.AD_ID", "android.permission.ACCESS_ADSERVICES_TOPICS", "android.permission.ACCESS_ADSERVICES_ATTRIBUTION",
        "android.permission.ACCESS_ADSERVICES_AD_ID", "com.android.vending.BILLING" };
    public static readonly string[] RemoveFeatures = { "android.software.xr.api.SPATIAL", "android.hardware.xr.input.hand_tracking" };
    public int callbackOrder { get { return int.MaxValue; } }

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        var log = new StringBuilder();
        try
        {
            string root = Directory.GetParent(path).FullName;
            string dump = Path.Combine(HunchQuestBuild.Bridge, "gradle_dump"); Directory.CreateDirectory(dump);
            bool deviceFound = false;
            string libManifest = null;
            foreach (var f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string rel = f.Substring(root.Length).TrimStart('\\', '/');
                string relU = "/" + rel.Replace('\\', '/');
                if (relU.Contains("/build/") || relU.StartsWith("/.gradle/")) continue;
                string name = Path.GetFileName(f);
                bool gradle = name.EndsWith(".gradle") || name.EndsWith(".gradle.kts") || name == "gradle.properties";
                bool manifest = name == "AndroidManifest.xml";
                if (!gradle && !manifest) continue;
                string txt = File.ReadAllText(f), before = txt;
                if (gradle)
                    txt = Regex.Replace(txt, "(targetSdk\\w*\\s*[=:]?\\s*[\"']?)(\\d+)", mm => int.Parse(mm.Groups[2].Value) > MaxTarget ? mm.Groups[1].Value + MaxTarget : mm.Value);
                if (manifest)
                {
                    if (rel.Replace('\\', '/').StartsWith("unityLibrary/src/main")) libManifest = f;
                    txt = Regex.Replace(txt, "android:targetSdkVersion\\s*=\\s*\"(\\d+)\"", mm => int.Parse(mm.Groups[1].Value) > MaxTarget ? "android:targetSdkVersion=\"" + MaxTarget + "\"" : mm.Value);
                    txt = Regex.Replace(txt, "<meta-data\\b[^>]*>", mm =>
                    {
                        if (!mm.Value.Contains("com.oculus.supportedDevices")) return mm.Value;
                        deviceFound = true;
                        log.AppendLine("devices in " + rel + " were: " + mm.Value);
                        return Regex.Replace(mm.Value, "android:value\\s*=\\s*\"[^\"]*\"", "android:value=\"" + Devices + "\"");
                    });
                }
                if (txt != before) { File.WriteAllText(f, txt); log.AppendLine("fixed " + rel); }
                File.Copy(f, Path.Combine(dump, rel.Replace('\\', '_').Replace('/', '_')), true);
            }
            // The app's own manifest wins over every library: tell the merger to drop anything Meta would flag.
            string launcher = Path.Combine(root, "launcher", "src", "main", "AndroidManifest.xml");
            if (File.Exists(launcher))
            {
                string lt = File.ReadAllText(launcher);
                if (!lt.Contains("hunch-quest-clean"))
                {
                    var open = Regex.Match(lt, "<manifest\\b[^>]*>");
                    if (open.Success)
                    {
                        string tag = open.Value;
                        if (!tag.Contains("xmlns:tools")) tag = tag.Insert(tag.Length - 1, " xmlns:tools=\"http://schemas.android.com/tools\"");
                        var add = new StringBuilder("\n  <!-- hunch-quest-clean -->");
                        foreach (var p in RemovePermissions) add.Append("\n  <uses-permission android:name=\"" + p + "\" tools:node=\"remove\" />");
                        foreach (var f in RemoveFeatures) add.Append("\n  <uses-feature android:name=\"" + f + "\" tools:node=\"remove\" />");
                        lt = lt.Substring(0, open.Index) + tag + add + lt.Substring(open.Index + open.Length);
                        File.WriteAllText(launcher, lt);
                        log.AppendLine("launcher manifest: drop " + RemovePermissions.Length + " permissions and " + RemoveFeatures.Length + " Android XR features");
                    }
                }
            }
            if (!deviceFound && libManifest != null)
            {
                string txt = File.ReadAllText(libManifest);
                var app = Regex.Match(txt, "<application\\b[^>]*?(/?)>");
                if (app.Success && app.Groups[1].Value != "/")
                {
                    txt = txt.Insert(app.Index + app.Length, "\n    <meta-data android:name=\"com.oculus.supportedDevices\" android:value=\"" + Devices + "\" />");
                    File.WriteAllText(libManifest, txt);
                    log.AppendLine("added devices to unityLibrary manifest");
                }
                else log.AppendLine("no devices entry and no <application> to add one to");
            }
        }
        catch (Exception e) { log.AppendLine("FAILED " + e.Message); }
        HunchQuestBuild.Log("gradle fix: " + log.ToString().Replace(Environment.NewLine, " || "));
    }
}
