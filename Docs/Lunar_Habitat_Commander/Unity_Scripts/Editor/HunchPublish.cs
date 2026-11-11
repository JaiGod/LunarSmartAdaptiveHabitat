// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchPublish.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Packs the Windows build into one zip that can be uploaded to a game page (itch.io and
//            similar), and gathers screenshots.
//  Contains: HunchPublish
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

// Packs the Windows build into one zip that can be uploaded to a game page (itch.io and similar), and gathers screenshots.
// Menu: HUNCH / Make publish package.   Output: <unity2>\Publish\
public static class HunchPublish
{
    const string ZipName = "Lunar_Habitat_Commander_Windows.zip";
    const string TopFolder = "Lunar Habitat Commander";

    static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }
    static string Unity2 { get { return Directory.GetParent(ProjectRoot).FullName; } }

    [MenuItem("HUNCH/Make publish package (zip + screenshots)")]
    public static void Make()
    {
        var log = new StringBuilder();
        string report = Path.Combine(ProjectRoot, "HunchBridge", "publish.txt");
        try
        {
            string build = Path.Combine(Unity2, "Lunar_Habitat_Commander_Build");
            string exe = Path.Combine(build, "Lunar_Habitat_Commander.exe");
            if (!File.Exists(exe)) throw new Exception("build not found: " + exe + "  (run HUNCH / Build Windows app first)");
            string outDir = Path.Combine(Unity2, "Publish");
            Directory.CreateDirectory(outDir);
            log.AppendLine("exe written " + File.GetLastWriteTime(exe).ToString("yyyy-MM-dd HH:mm:ss"));

            // ---- the zip
            string zip = Path.Combine(outDir, ZipName), tmp = zip + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);
            int files = 0; long raw = 0;
            using (var fs = new FileStream(tmp, FileMode.Create))
            using (var arc = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var f in Directory.GetFiles(build, "*", SearchOption.AllDirectories))
                {
                    string rel = f.Substring(build.Length).TrimStart('\\', '/').Replace('\\', '/');
                    if (rel.Contains("_DoNotShip") || rel.Contains("_BackUpThisFolder") || rel.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) continue;
                    var entry = arc.CreateEntry(TopFolder + "/" + rel, System.IO.Compression.CompressionLevel.Optimal);
                    entry.LastWriteTime = File.GetLastWriteTime(f);
                    using (var src = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var dst = entry.Open()) src.CopyTo(dst, 1 << 20);
                    files++; raw += new FileInfo(f).Length;
                }
            }
            if (File.Exists(zip)) File.Delete(zip);
            File.Move(tmp, zip);
            log.AppendLine("zip " + zip);
            log.AppendLine("zip files " + files + ", " + (raw / 1048576) + " MB before, " + (new FileInfo(zip).Length / 1048576) + " MB zipped");

            // ---- screenshots for the game page
            string shots = Path.Combine(outDir, "Screenshots"); Directory.CreateDirectory(shots);
            string bridge = Path.Combine(ProjectRoot, "HunchBridge");
            string[,] pick =
            {
                { "x1_menu_font.png", "01_main_menu.png" }, { "u6_greenhouse.png", "02_greenhouse.png" }, { "y1_arrows.png", "03_green_arrows.png" },
                { "v9_rocket_liftoff.png", "04_rocket_liftoff.png" }, { "y2_bigmap.png", "05_map.png" }, { "t9_shield.png", "06_storm_shield.png" },
                { "y4_night.png", "07_lunar_night.png" }, { "u2_airlock.png", "08_airlock.png" }, { "t5_talk_aria.png", "09_talk_to_aria.png" },
                { "q3_zap.png", "10_zap_alien.png" }, { "q5_ufo.png", "11_ufo.png" }, { "q6_mining.png", "12_mining_drill.png" }, { "q8_field.png", "13_mining_field.png" }, { "q9_sun.png", "14_sun_over_spaceport.png" },
                { "p7_cables_vent.png", "15_power_cables.png" }, { "p1b_zap_alien.png", "16_backpack_bar.png" },
            };
            int copied = 0;
            for (int i = 0; i < pick.GetLength(0); i++)
            {
                string src = Path.Combine(bridge, pick[i, 0]);
                if (!File.Exists(src)) continue;
                File.Copy(src, Path.Combine(shots, pick[i, 1]), true); copied++;
            }
            log.AppendLine("screenshots " + copied);
            log.AppendLine("OK");
            Debug.Log("[HUNCH] publish package ready in " + outDir);
        }
        catch (Exception e) { log.AppendLine("FAILED " + e); Debug.LogError("[HUNCH] publish package failed " + e); }
        try { Directory.CreateDirectory(Path.GetDirectoryName(report)); File.WriteAllText(report, log.ToString()); } catch { }
    }
}
