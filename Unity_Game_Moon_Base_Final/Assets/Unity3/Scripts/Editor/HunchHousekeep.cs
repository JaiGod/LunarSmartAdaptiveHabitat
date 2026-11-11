// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Editor tool
//  File    : HunchHousekeep.cs  (Editor only)
//  Purpose : Tidies the Downloads\Nasa folder. List() writes what each top-level folder holds;
//            MoveOut() moves the folders named in HunchBridge/housekeep.txt (one name per line)
//            from Downloads\Nasa into C:\\Users\\<user>\\Unrelated_Projects (local, not synced to OneDrive). Nothing is deleted.
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class HunchHousekeep
{
    static string Bridge => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HunchBridge"));
    static string Nasa => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    static string Docs => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public static void List()
    {
        var sb = new StringBuilder();
        sb.AppendLine("NASA=" + Nasa); sb.AppendLine("DOCS=" + Docs);
        foreach (var d in Directory.GetDirectories(Nasa))
        {
            long bytes = 0; int n = 0;
            try { foreach (var f in Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories)) { n++; try { bytes += new FileInfo(f).Length; } catch { } } } catch (Exception e) { sb.AppendLine("  err " + e.Message); }
            sb.AppendLine($"## {Path.GetFileName(d)}  files={n}  MB={bytes / 1048576}");
            try { foreach (var e in Directory.GetFileSystemEntries(d).Take(25)) sb.AppendLine("   " + Path.GetFileName(e)); } catch { }
        }
        foreach (var f in Directory.GetFiles(Nasa)) sb.AppendLine("file " + Path.GetFileName(f));
        File.WriteAllText(Path.Combine(Bridge, "housekeep_list.txt"), sb.ToString());
    }

    static void CopyDir(string s, string t)
    {
        Directory.CreateDirectory(t);
        foreach (var f in Directory.GetFiles(s)) File.Copy(f, Path.Combine(t, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(s)) CopyDir(d, Path.Combine(t, Path.GetFileName(d)));
    }

    public static void MoveOut()
    {
        var sb = new StringBuilder(DateTime.Now.ToString("HH:mm:ss") + "\n");
        string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Unrelated_Projects"); Directory.CreateDirectory(target);
        foreach (var raw in File.ReadAllLines(Path.Combine(Bridge, "housekeep.txt")))
        {
            var name = raw.Trim(); if (name.Length == 0) continue;
            if (name.Contains("..") || name.Contains("/") || name.Contains("\\") || name.Equals("idle-mining-empire", StringComparison.OrdinalIgnoreCase)) { sb.AppendLine("refused " + name); continue; }
            string src = Path.Combine(Nasa, name), dst = Path.Combine(target, name);
            if (!Directory.Exists(src)) { sb.AppendLine("not found " + name); continue; }
            if (Directory.Exists(dst)) dst += "_" + DateTime.Now.ToString("HHmmss");
            try { Directory.Move(src, dst); sb.AppendLine("moved " + name + " -> " + dst); }
            catch (IOException)
            {   // different drive (e.g. OneDrive Documents): copy, then remove the original
                try { CopyDir(src, dst); Directory.Delete(src, true); sb.AppendLine("copied+moved " + name + " -> " + dst); }
                catch (Exception e2) { sb.AppendLine("FAILED " + name + ": " + e2.Message); }
            }
            catch (Exception e) { sb.AppendLine("FAILED " + name + ": " + e.Message); }
        }
        File.WriteAllText(Path.Combine(Bridge, "housekeep_out.txt"), sb.ToString());
    }

    /// Moves everything from Documents\NASA_Unrelated_Assets (OneDrive) to the local C:\Users\<user>\Unrelated_Projects.
    public static void RelocateFromDocs()
    {
        var sb = new StringBuilder(DateTime.Now.ToString("HH:mm:ss") + "\n");
        string from = Path.Combine(Docs, "NASA_Unrelated_Assets");
        string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Unrelated_Projects"); Directory.CreateDirectory(target);
        if (Directory.Exists(from))
        {
            foreach (var d in Directory.GetDirectories(from))
            {
                string dst = Path.Combine(target, Path.GetFileName(d));
                if (Directory.Exists(dst)) dst += "_" + DateTime.Now.ToString("HHmmss");
                try { Directory.Move(d, dst); sb.AppendLine("moved " + d + " -> " + dst); }
                catch (Exception e) { sb.AppendLine("FAILED " + d + ": " + e.Message); }
            }
            try { if (Directory.GetFileSystemEntries(from).Length == 0) { Directory.Delete(from); sb.AppendLine("removed empty " + from); } } catch (Exception e) { sb.AppendLine("left " + from + ": " + e.Message); }
        }
        else sb.AppendLine("nothing at " + from);
        File.WriteAllText(Path.Combine(Bridge, "relocate_out.txt"), sb.ToString());
    }
}
#endif
