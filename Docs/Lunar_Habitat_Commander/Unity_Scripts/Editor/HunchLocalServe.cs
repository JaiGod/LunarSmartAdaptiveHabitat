// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchLocalServe.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : Tiny read-only file server on this computer only (127.0.0.1), used to hand the store
//            images and the Quest .apk to the Meta Developer Dashboard page in the Claude browser.
//            It serves just two things: files in HunchBridge/serve
//  Contains: HunchLocalServe
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

/// Tiny read-only file server on this computer only (127.0.0.1), used to hand the store images and the Quest .apk
/// to the Meta Developer Dashboard page in the Claude browser. It serves just two things: files in HunchBridge/serve
/// and Builds/LunarHabitatCommander.apk, answers only the Meta dashboard origin, and stops by itself after two hours.
public static class HunchLocalServe
{
    const int Port = 8779;
    static TcpListener listener; static Thread thread; static DateTime until;
    static string Root { get { return Directory.GetParent(Application.dataPath).FullName; } }
    static string Log { get { return Path.Combine(Root, "HunchBridge", "serve_log.txt"); } }

    public static void Start()
    {
        Stop();
        string serveDir = Path.Combine(Root, "HunchBridge", "serve"); Directory.CreateDirectory(serveDir);
        string apk = Path.Combine(Root, "Builds", "LunarHabitatCommander.apk");
        until = DateTime.Now.AddHours(2);
        listener = new TcpListener(IPAddress.Loopback, Port);
        listener.Start();
        thread = new Thread(() => Loop(serveDir, apk)) { IsBackground = true, Name = "HunchLocalServe" };
        thread.Start();
        File.WriteAllText(Log, DateTime.Now.ToString("HH:mm:ss") + " serving on 127.0.0.1:" + Port + Environment.NewLine);
    }

    public static void Stop()
    {
        try { if (listener != null) listener.Stop(); } catch { }
        listener = null;
    }

    static void Note(string s) { try { File.AppendAllText(Log, DateTime.Now.ToString("HH:mm:ss") + " " + s + Environment.NewLine); } catch { } }

    static void Loop(string serveDir, string apk)
    {
        var l = listener;
        while (l != null && DateTime.Now < until)
        {
            TcpClient c;
            try { c = l.AcceptTcpClient(); } catch { break; }
            ThreadPool.QueueUserWorkItem(_ => Handle(c, serveDir, apk));
        }
        try { if (l != null) l.Stop(); } catch { }
        Note("stopped");
    }

    static void Handle(TcpClient c, string serveDir, string apk)
    {
        try
        {
            using (c)
            using (var s = c.GetStream())
            {
                var reader = new StreamReader(s, Encoding.ASCII, false, 4096, true);
                string first = reader.ReadLine() ?? ""; string origin = "";
                string line;
                while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                    if (line.StartsWith("Origin:", StringComparison.OrdinalIgnoreCase)) origin = line.Substring(7).Trim();
                var parts = first.Split(' ');
                string method = parts.Length > 0 ? parts[0] : "", path = parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Split('?')[0]) : "/";
                bool okOrigin = origin == "" || origin.EndsWith(".meta.com") || origin == "https://developers.meta.com";
                string cors = okOrigin && origin != "" ? "Access-Control-Allow-Origin: " + origin + "\r\nAccess-Control-Allow-Private-Network: true\r\nAccess-Control-Allow-Methods: GET, OPTIONS\r\nAccess-Control-Allow-Headers: *\r\nVary: Origin\r\n" : "";
                if (method == "OPTIONS") { Write(s, "HTTP/1.1 204 No Content\r\n" + cors + "Content-Length: 0\r\nConnection: close\r\n\r\n"); return; }
                string file = null;
                if (path == "/apk") file = apk;
                else if (path.StartsWith("/serve/")) { string name = Path.GetFileName(path); if (name.Length > 0) file = Path.Combine(serveDir, name); }
                if (!okOrigin || method != "GET" || file == null || !File.Exists(file))
                { Write(s, "HTTP/1.1 404 Not Found\r\n" + cors + "Content-Length: 0\r\nConnection: close\r\n\r\n"); Note("404 " + path + " origin=" + origin); return; }
                string type = file.EndsWith(".png") ? "image/png" : file.EndsWith(".apk") ? "application/vnd.android.package-archive" : "application/octet-stream";
                long len = new FileInfo(file).Length;
                Write(s, "HTTP/1.1 200 OK\r\n" + cors + "Content-Type: " + type + "\r\nContent-Length: " + len + "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
                using (var f = File.OpenRead(file)) f.CopyTo(s, 1 << 20);
                Note("200 " + path + " " + len + " bytes origin=" + origin);
            }
        }
        catch (Exception e) { Note("error " + e.Message); }
    }

    static void Write(Stream s, string head) { var b = Encoding.ASCII.GetBytes(head); s.Write(b, 0, b.Length); }
}
