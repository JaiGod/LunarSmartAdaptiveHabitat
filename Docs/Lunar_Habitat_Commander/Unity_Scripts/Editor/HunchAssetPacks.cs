// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchAssetPacks.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Finds Asset Store packages that were downloaded on this PC but not yet imported into
//            the project, and imports the chosen ones.
//  Contains: HunchAssetPacks
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Finds Asset Store packages that were downloaded on this PC but not yet imported into the project, and imports the chosen ones.
// Scan() writes HunchBridge\asset_cache.txt. Import() imports every package path listed in HunchBridge\asset_import.txt.
public static class HunchAssetPacks
{
    static string Bridge { get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge"); } }
    static string Cache { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Unity", "Asset Store-5.x"); } }

    [MenuItem("HUNCH/List downloaded Asset Store packages")]
    public static void Scan()
    {
        var sb = new StringBuilder();
        try
        {
            sb.AppendLine("cache " + Cache + (Directory.Exists(Cache) ? "" : "  (not found)"));
            if (Directory.Exists(Cache))
                foreach (var f in Directory.GetFiles(Cache, "*.unitypackage", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTime))
                    sb.AppendLine(File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm") + " | " + (new FileInfo(f).Length / 1024) + " KB | " + f);
            sb.AppendLine("--- top folders in Assets");
            foreach (var d in Directory.GetDirectories(Application.dataPath)) sb.AppendLine(Directory.GetLastWriteTime(d).ToString("yyyy-MM-dd HH:mm") + " | " + Path.GetFileName(d));
            sb.AppendLine("OK");
        }
        catch (Exception e) { sb.AppendLine("FAILED " + e); }
        File.WriteAllText(Path.Combine(Bridge, "asset_cache.txt"), sb.ToString());
    }

    // takes an imported pack back out (used if its code does not compile in this project). Folder names are read from HunchBridge\asset_remove.txt.
    public static void Remove()
    {
        var sb = new StringBuilder();
        try
        {
            foreach (var line in File.ReadAllLines(Path.Combine(Bridge, "asset_remove.txt")))
            {
                string n = line.Trim(); if (n.Length == 0 || n.Contains("..") || n.Contains("/") || n.Contains("\\") || n == "HUNCH") continue;
                string full = Path.Combine(Application.dataPath, n);
                if (!Directory.Exists(full)) { sb.AppendLine("not found " + n); continue; }
                Directory.Delete(full, true); if (File.Exists(full + ".meta")) File.Delete(full + ".meta");
                sb.AppendLine("removed " + n);
            }
            sb.AppendLine("OK");
        }
        catch (Exception e) { sb.AppendLine("FAILED " + e); }
        File.WriteAllText(Path.Combine(Bridge, "asset_remove_result.txt"), sb.ToString());
        AssetDatabase.Refresh();
    }

    // Looks inside each package listed in HunchBridge\asset_import.txt without importing it: how many files, how many scripts,
    // which folders it creates, and whether it would overwrite project settings. Writes HunchBridge\asset_peek.txt.
    public static void Peek()
    {
        var sb = new StringBuilder();
        try
        {
            foreach (var line in File.ReadAllLines(Path.Combine(Bridge, "asset_import.txt")))
            {
                string p = line.Trim(); if (p.Length == 0 || !File.Exists(p)) continue;
                var paths = new List<string>();
                using (var fs = File.OpenRead(p))
                using (var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress))
                {
                    var header = new byte[512]; var skip = new byte[1 << 16];
                    while (ReadFull(gz, header, 512) == 512)
                    {
                        string name = Encoding.ASCII.GetString(header, 0, 100).TrimEnd('\0');
                        if (name.Length == 0) break;
                        string sizeStr = Encoding.ASCII.GetString(header, 124, 12).Trim('\0', ' ');
                        long size = sizeStr.Length > 0 ? Convert.ToInt64(sizeStr, 8) : 0, padded = (size + 511) / 512 * 512;
                        if (name.EndsWith("/pathname") && size > 0 && size < 4096)
                        {
                            var buf = new byte[padded]; ReadFull(gz, buf, (int)padded);
                            paths.Add(Encoding.UTF8.GetString(buf, 0, (int)size).Split('\n')[0].Trim());
                        }
                        else { long left = padded; while (left > 0) { int got = gz.Read(skip, 0, (int)Math.Min(left, skip.Length)); if (got <= 0) break; left -= got; } }
                    }
                }
                int scripts = paths.Count(x => x.EndsWith(".cs")), settings = paths.Count(x => x.StartsWith("ProjectSettings")), pkgs = paths.Count(x => x.StartsWith("Packages")), shaders = paths.Count(x => x.EndsWith(".shader") || x.EndsWith(".shadergraph")), prefabs = paths.Count(x => x.EndsWith(".prefab"));
                var tops = paths.Select(x => { var parts = x.Split('/'); return parts.Length > 2 ? parts[0] + "/" + parts[1] + "/" + parts[2] : x; }).Distinct().Take(14);
                sb.AppendLine(Path.GetFileName(p) + ": files " + paths.Count + ", prefabs " + prefabs + ", scripts " + scripts + ", shaders " + shaders + ", ProjectSettings " + settings + ", Packages " + pkgs);
                foreach (var t in tops) sb.AppendLine("    " + t);
                foreach (var c in paths.Where(x => x.EndsWith(".cs")).Take(12)) sb.AppendLine("    script " + c);
            }
            sb.AppendLine("OK");
        }
        catch (Exception e) { sb.AppendLine("FAILED " + e); }
        File.WriteAllText(Path.Combine(Bridge, "asset_peek.txt"), sb.ToString());
    }
    static int ReadFull(Stream s, byte[] buf, int n) { int total = 0; while (total < n) { int got = s.Read(buf, total, n - total); if (got <= 0) break; total += got; } return total; }

    public static void Import()
    {
        var sb = new StringBuilder();
        try
        {
            string list = Path.Combine(Bridge, "asset_import.txt");
            foreach (var line in File.ReadAllLines(list))
            {
                string p = line.Trim(); if (p.Length == 0) continue;
                // only packages from Unity's own Asset Store download folder
                if (!Path.GetFullPath(p).StartsWith(Path.GetFullPath(Cache), StringComparison.OrdinalIgnoreCase) || !File.Exists(p)) { sb.AppendLine("skipped " + p); continue; }
                AssetDatabase.ImportPackage(p, false); sb.AppendLine("imported " + p);
            }
            sb.AppendLine("OK");
        }
        catch (Exception e) { sb.AppendLine("FAILED " + e); }
        File.WriteAllText(Path.Combine(Bridge, "asset_import_result.txt"), sb.ToString());
        AssetDatabase.Refresh();
    }
}
