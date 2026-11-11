// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Editor tool
//  File    : HunchSitePreview.cs  (Editor only)
//  Purpose : Tiny read-only web server for previewing the project website on THIS computer only
//            (http://127.0.0.1:8781/), serving <project>/HunchBridge/site. Stops by itself after 1 hour.
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
#if UNITY_EDITOR
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public static class HunchSitePreview
{
    const int Port = 8781;
    static TcpListener listener; static DateTime until;
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HunchBridge", "site"));

    public static void Start()
    {
        Stop(); Directory.CreateDirectory(Root); until = DateTime.Now.AddHours(1);
        listener = new TcpListener(IPAddress.Loopback, Port); listener.Start();
        var l = listener; new Thread(() => { while (DateTime.Now < until) { try { var c = l.AcceptTcpClient(); ThreadPool.QueueUserWorkItem(_ => Serve(c)); } catch { break; } } try { l.Stop(); } catch { } }) { IsBackground = true }.Start();
        File.WriteAllText(Path.Combine(Root, "..", "site_out.txt"), DateTime.Now.ToString("HH:mm:ss") + " serving " + Root + " on http://127.0.0.1:" + Port + "/");
    }
    public static void Stop() { try { listener?.Stop(); } catch { } listener = null; }

    static string Mime(string f)
    {
        switch (Path.GetExtension(f).ToLowerInvariant())
        {
            case ".html": return "text/html; charset=utf-8"; case ".js": return "text/javascript; charset=utf-8"; case ".css": return "text/css";
            case ".png": return "image/png"; case ".jpg": case ".jpeg": return "image/jpeg"; case ".svg": return "image/svg+xml"; case ".mp4": return "video/mp4";
            case ".json": return "application/json"; case ".ico": return "image/x-icon"; default: return "application/octet-stream";
        }
    }
    static void Serve(TcpClient c)
    {
        try
        {
            using (c) using (var s = c.GetStream())
            {
                var rd = new StreamReader(s, Encoding.ASCII, false, 4096, true); string first = rd.ReadLine() ?? ""; string ln; while (!string.IsNullOrEmpty(ln = rd.ReadLine())) { }
                var parts = first.Split(' '); string path = parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Split('?')[0]) : "/";
                if (path.EndsWith("/")) path += "index.html";
                string full = Path.GetFullPath(Path.Combine(Root, path.TrimStart('/')));
                if (!full.StartsWith(Root) || !File.Exists(full)) { var nb = Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"); s.Write(nb, 0, nb.Length); return; }
                var head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: " + Mime(full) + "\r\nContent-Length: " + new FileInfo(full).Length + "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
                s.Write(head, 0, head.Length); using (var f = File.OpenRead(full)) f.CopyTo(s);
            }
        }
        catch { }
    }
}
#endif
