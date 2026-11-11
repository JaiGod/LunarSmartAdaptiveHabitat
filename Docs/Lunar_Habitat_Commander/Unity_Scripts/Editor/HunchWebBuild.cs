// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchWebBuild.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Web version for Unity Play (play.unity.com). Install(): adds Unity's WebGL Publisher
//            package (it adds the Publish menu that uploads to Unity Play).
//  Contains: HunchWebBuild
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

/// Web version for Unity Play (play.unity.com).
///   Install(): adds Unity's WebGL Publisher package (it adds the Publish menu that uploads to Unity Play).
///   Build():   builds the game for WebGL into <project>/Builds/WebGL, gzip-compressed with a fallback, and reports the size.
/// Log: HunchBridge/web_build.txt
public static class HunchWebBuild
{
    static string Root { get { return Directory.GetParent(Application.dataPath).FullName; } }
    static string LogPath { get { return Path.Combine(Root, "HunchBridge", "web_build.txt"); } }
    static void Note(string s) { File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss") + " " + s + Environment.NewLine); }
    static AddRequest add;

    public static void Install()
    {
        File.WriteAllText(LogPath, "");
        add = Client.Add("com.unity.connect.share");
        EditorApplication.update += Poll;
        Note("installing com.unity.connect.share (WebGL Publisher)");
    }
    static void Poll()
    {
        if (add == null || !add.IsCompleted) return;
        EditorApplication.update -= Poll;
        Note(add.Status == StatusCode.Success ? "installed " + add.Result.name + " " + add.Result.version : "install failed: " + (add.Error != null ? add.Error.message : "?"));
        add = null;
    }

    public static void Build()
    {
        Note("web build starting");
        try
        {
            string scene = "Assets/HUNCH/Scenes/Lunar_Habitat_Commander.unity";
            string outDir = Path.Combine(Root, "Builds", "WebGL");
            Directory.CreateDirectory(outDir);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.maximumMemorySize = 4096;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithoutStacktrace;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            var rep = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scene }, locationPathName = outDir, target = BuildTarget.WebGL, options = BuildOptions.None });
            Note("result " + rep.summary.result + ", errors " + rep.summary.totalErrors + ", warnings " + rep.summary.totalWarnings + ", time " + rep.summary.totalTime);
            foreach (var st in rep.steps) foreach (var m in st.messages) if (m.type == LogType.Error || m.type == LogType.Exception) Note("ERROR " + m.content.Split('\n')[0]);
            long total = 0;
            if (Directory.Exists(outDir)) foreach (var f in Directory.GetFiles(outDir, "*", SearchOption.AllDirectories)) { long n = new FileInfo(f).Length; total += n; if (n > 20000000) Note("  big file " + Path.GetFileName(f) + " " + (n / 1000000) + " MB"); }
            Note("build folder " + (total / 1000000) + " MB (Unity Play limit: 500 MB zipped)");
        }
        catch (Exception e) { Note("FAILED " + e.Message); }
    }

    /// makes the WEB version lighter without touching the Quest or PC versions: only the "WebGL" platform overrides change.
    /// Textures: at most 1024 px and crunch-compressed. Sounds: lower quality. Then Build() again.
    public static void Slim()
    {
        int tex = 0, snd = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                var ti = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter; if (ti == null) continue;
                var s = ti.GetPlatformTextureSettings("WebGL");
                int max = Mathf.Min(ti.maxTextureSize, ti.textureType == TextureImporterType.NormalMap ? 512 : 1024);
                if (s.overridden && s.maxTextureSize <= max && s.crunchedCompression) continue;
                s.overridden = true; s.maxTextureSize = max; s.format = TextureImporterFormat.Automatic; s.textureCompression = TextureImporterCompression.Compressed;
                s.crunchedCompression = true; s.compressionQuality = 50;
                ti.SetPlatformTextureSettings(s); ti.SaveAndReimport(); tex++;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
            {
                var ai = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as AudioImporter; if (ai == null) continue;
                var s = ai.GetOverrideSampleSettings("WebGL"); s.quality = 0.35f; s.loadType = AudioClipLoadType.CompressedInMemory;
                ai.SetOverrideSampleSettings("WebGL", s); ai.SaveAndReimport(); snd++;
            }
        }
        catch (Exception e) { Note("slim FAILED " + e.Message); }
        finally { AssetDatabase.StopAssetEditing(); }
        Note("slim: web-only settings for " + tex + " textures and " + snd + " sounds (Quest and PC unchanged)");
    }

    /// one zip of the web build, for uploading on play.unity.com by hand (Builds/Lunar_Habitat_Commander_WebGL.zip)
    public static void Zip()
    {
        try
        {
            string src = Path.Combine(Root, "Builds", "WebGL"), zip = Path.Combine(Root, "Builds", "Lunar_Habitat_Commander_WebGL.zip");
            if (File.Exists(zip)) File.Delete(zip);
            using (var fs = new FileStream(zip, FileMode.Create))
            using (var arc = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
                foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                {
                    var e = arc.CreateEntry(f.Substring(src.Length + 1).Replace('\\', '/'), System.IO.Compression.CompressionLevel.Fastest);
                    using (var o = e.Open()) using (var i = File.OpenRead(f)) i.CopyTo(o);
                }
            Note("zip " + zip + " " + (new FileInfo(zip).Length / 1000000) + " MB");
        }
        catch (Exception e) { Note("zip FAILED " + e.Message); }
    }
}
