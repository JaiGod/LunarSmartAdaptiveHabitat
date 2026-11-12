// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/07_Debug/HunchDebug.cs
//  Section : 07_Debug
//  Purpose : DEBUG TOOLS — tagged logging (HLog) + a debug panel on the ` key (left of 1):
//            FPS, frame time, memory, player position, nearest zone, habitat O2 / water / power,
//            alerts, crew count, mission day and the last 10 log lines.
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Tagged logging: HLog.I("Power", "Solar panel online") → "[Power] Solar panel online", kept for the debug panel.
public static class HLog
{
    public static readonly Queue<string> Recent = new Queue<string>();
    static void Keep(string s) { Recent.Enqueue(Time.time.ToString("0.0") + "s " + s); while (Recent.Count > 10) Recent.Dequeue(); }
    public static void I(string tag, string msg) { var s = "[" + tag + "] " + msg; Debug.Log(s); Keep(s); }
    public static void W(string tag, string msg) { var s = "[" + tag + "] " + msg; Debug.LogWarning(s); Keep(s); }
    public static void E(string tag, string msg) { var s = "[" + tag + "] " + msg; Debug.LogError(s); Keep(s); }
}

public class HunchDebug : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { var g = FindFirstObjectByType<HabitatGame>(); if (g && !g.GetComponent<HunchDebug>()) g.gameObject.AddComponent<HunchDebug>(); }

    HabitatGame game; bool open; float fps; string text = ""; float next; GUIStyle st;
    void Awake() { game = GetComponent<HabitatGame>(); }

    void Update()
    {
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
        var kb = Keyboard.current; if (kb != null && kb.backquoteKey.wasPressedThisFrame) { open = !open; HLog.I("Debug", "panel " + (open ? "open" : "closed")); }
        if (!open || Time.unscaledTime < next || !game) return; next = Time.unscaledTime + 0.25f;
        var sim = game.sim; var cam = Camera.main; Vector3 p = cam ? cam.transform.position : Vector3.zero;
        int nz = game.NearZone; string zone = nz >= 0 && nz < game.zones.Count ? game.zones[nz].title : "—";
        var sb = new System.Text.StringBuilder();
        sb.Append("<b>DEBUG</b>   ` closes\n");
        sb.Append("FPS " + fps.ToString("0") + "   frame " + (Time.unscaledDeltaTime * 1000f).ToString("0.0") + " ms   managed mem " + (System.GC.GetTotalMemory(false) / (1024 * 1024)) + " MB\n");
        sb.Append("Camera " + p.ToString("F1") + "   near zone: " + zone + "\n");
        sb.Append("Day " + sim.Day + "  hour " + sim.Hour.ToString("0.0") + "   phase " + sim.phase + "\n");
        sb.Append("O2 " + sim.oxygen.ToString("0") + "%   water " + sim.water.ToString("0") + "%   food " + sim.food.ToString("0") + "%   battery " + sim.battery.ToString("0") + "%\n");
        sb.Append("solar " + (sim.solarOn ? "on" : "off") + "  fission " + (sim.fissionOn ? "on" : "off") + "  ECLSS " + (sim.eclssOk ? "ok" : "FAULT") + "  storm " + (sim.stormActive ? "ACTIVE" : "no") + "  leak " + (sim.leak ? "YES" : "no") + "\n");
        sb.Append("crew " + sim.crew.Count + "   alerts " + sim.alerts.Count + "   zones " + game.zones.Count + "\n\n<b>Last logs</b>\n");
        foreach (var l in HLog.Recent) sb.Append(l).Append('\n');
        text = sb.ToString();
    }

    void OnGUI()
    {
        if (!open) return;
        if (st == null) { st = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, fontSize = 14, wordWrap = true }; st.normal.textColor = Color.white; }
        GUI.Box(new Rect(Screen.width - 500, 60, 480, 420), text, st);
    }
}
