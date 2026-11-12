// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/01_Core/HunchLink.cs
//  Section : 01_Core
//  Folder  : Game rules and the habitat simulation (crew, systems, alerts), start-up, self-test,
//            cheats and the live link to the Crew Health web page.
//  Purpose : The live link between the game and the NASA HUNCH Crew Health System web page. While
//            the game runs it answers on this computer only (http://127.0.0.1:8765/):
//  Contains: HunchLink
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

/// The live link between the game and the NASA HUNCH Crew Health System web page.
/// While the game runs it answers on this computer only (http://127.0.0.1:8765/):
///     /        the Crew Health System page itself (StreamingAssets/hunch-crew-health.html)
///     /state   the game's crew, vitals, habitat systems, clock and health log as JSON, refreshed twice a second
///     /cmd     what was pressed on the page: accept / clear / acknowledge / inject a scenario
/// Nothing leaves the computer: the address 127.0.0.1 can only be reached from this machine.
public class HunchLink : MonoBehaviour
{
    public static int Port = 8765;      // 8765, or the next free one if another copy of the game already has it
    public static string Url { get { return "http://127.0.0.1:" + Port + "/"; } }
    public bool Running { get; private set; }
    public string Problem = "";
    public int served, commands;

    HttpListener listener; volatile string json = "{}"; byte[] page; readonly Queue<string> queue = new Queue<string>();
    float tick; HabitatGame game; HunchFrontier frontier;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var g = FindFirstObjectByType<HabitatGame>();
        if (g && !g.GetComponent<HunchLink>() && !Application.isBatchMode) g.gameObject.AddComponent<HunchLink>();
    }

    void Start()
    {
        game = GetComponent<HabitatGame>();
        try { page = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, "hunch-crew-health.html")); }
        catch (Exception e) { page = null; HLog.W("Core", "link: web page not found: " + e.Message); }
        for (int p = 8765; p <= 8770 && !Running; p++)
        {
            try
            {
                Port = p; listener = new HttpListener(); listener.Prefixes.Add(Url); listener.Start();
                Running = true; listener.BeginGetContext(OnRequest, listener);
                HLog.I("Core", "live link for the Crew Health page: " + Url);
            }
            catch (Exception e) { Running = false; Problem = e.Message; try { if (listener != null) listener.Close(); } catch { } listener = null; }
        }
        if (!Running) { Port = 8765; HLog.I("Core", "live link is off (another program uses the ports 8765 to 8770): " + Problem); }
    }

    // runs on a background thread: it only hands out text prepared on the main thread
    void OnRequest(IAsyncResult ar)
    {
        var l = (HttpListener)ar.AsyncState; HttpListenerContext ctx = null;
        try { if (l == null || !l.IsListening) return; ctx = l.EndGetContext(ar); } catch { ctx = null; }
        try { if (l != null && l.IsListening) l.BeginGetContext(OnRequest, l); } catch { }
        if (ctx == null) return;
        try
        {
            string path = ctx.Request.Url.AbsolutePath; var res = ctx.Response; byte[] body;
            res.AddHeader("Access-Control-Allow-Origin", "*"); res.AddHeader("Cache-Control", "no-store");
            if (path == "/state") { body = Encoding.UTF8.GetBytes(json); res.ContentType = "application/json; charset=utf-8"; }
            else if (path == "/cmd")
            {
                lock (queue) { if (queue.Count < 32) queue.Enqueue(ctx.Request.Url.Query); }
                body = Encoding.UTF8.GetBytes("{\"ok\":true}"); res.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/" || path == "/index.html")
            {
                body = page ?? Encoding.UTF8.GetBytes("The Crew Health System page is missing from the game's StreamingAssets folder."); res.ContentType = page != null ? "text/html; charset=utf-8" : "text/plain; charset=utf-8";
            }
            else { res.StatusCode = 404; body = Encoding.UTF8.GetBytes("not found"); res.ContentType = "text/plain"; }
            res.ContentLength64 = body.Length; res.OutputStream.Write(body, 0, body.Length); res.OutputStream.Close();
            System.Threading.Interlocked.Increment(ref served);
        }
        catch { try { ctx.Response.Abort(); } catch { } }
    }

    void Update()
    {
        if (!Running) return;
        if (!frontier) frontier = GetComponent<HunchFrontier>();
        if (!frontier) return;
        while (true)
        {
            string q = null;
            lock (queue) { if (queue.Count > 0) q = queue.Dequeue(); }
            if (q == null) break;
            commands++;
            try { frontier.LinkCommand(q); } catch (Exception e) { HLog.W("Core", "link command failed: " + e.Message); }
        }
        tick -= Time.unscaledDeltaTime;
        if (tick > 0f) return;
        tick = 0.5f;
        try { string s = frontier.LinkJson(); if (!string.IsNullOrEmpty(s)) json = s; } catch (Exception e) { HLog.W("Core", "link state failed: " + e.Message); }
    }

    void Close()
    {
        Running = false;
        try { if (listener != null) { listener.Stop(); listener.Close(); } } catch { }
        listener = null;
    }
    void OnDestroy() { Close(); }
    void OnApplicationQuit() { Close(); }

    /// test hook: asks its own address like a browser would
    public string ProbeSelf()
    {
        if (!Running) return "not running: " + Problem;
        try
        {
            using (var wc = new WebClient())
            {
                string state = wc.DownloadString(Url + "state"); string html = wc.DownloadString(Url);
                try { File.WriteAllText(Path.Combine(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge"), "link_state.json"), state); } catch { }
                return "state " + state.Length + " chars, page " + html.Length + " chars, linked page=" + html.Contains("live link to the Unity game") + ", served " + served + ", commands " + commands + " | " + (state.Length > 420 ? state.Substring(0, 420) : state);
            }
        }
        catch (Exception e) { return "request failed: " + e.Message; }
    }
    public string ProbeCmd()
    {
        if (!Running) return "not running";
        try { using (var wc = new WebClient()) { return wc.DownloadString(Url + "cmd?do=inject&crew=1&cond=3&level=0.72"); } }
        catch (Exception e) { return "request failed: " + e.Message; }
    }
}
