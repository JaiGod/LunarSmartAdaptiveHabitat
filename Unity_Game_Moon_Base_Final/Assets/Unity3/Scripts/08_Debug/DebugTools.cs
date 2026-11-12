// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/08_Debug/DebugTools.cs
//  Section : 08_Debug
//  Purpose : DEBUG TOOLS — one place for logging, the in-game debug panel and the Scene-view gizmos.
//    HLog       tagged logs ("[Power] …", "[Interaction] …") + the last 10 lines kept for the panel
//    DebugPanel F9: FPS, position, room, aimed object, O2/CO2/power, memory, crew/NPC/object counts, last logs.
//               Developer tests live HERE (not on player keys): 1 ladder ride · 2 door walk · 3 repair alert ·
//               4 reward test · 5 jump to next smart object
//    HabitatGizmos  Scene view only: cyan = air zones · blue = walking routes + crew paths ·
//                   green = interaction reach of every smart object · yellow = alerts/faults
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hunch3
{
    /// Tagged logging. Use HLog.I("Power", "Solar panel online") instead of Debug.Log("working").
    public static class HLog
    {
        public static readonly Queue<string> Recent = new Queue<string>();
        static void Keep(string s) { Recent.Enqueue($"{Time.time:0.0}s {s}"); while (Recent.Count > 10) Recent.Dequeue(); }
        public static void I(string tag, string msg) { var s = $"[{tag}] {msg}"; Debug.Log(s); Keep(s); }
        public static void W(string tag, string msg) { var s = $"[{tag}] {msg}"; Debug.LogWarning(s); Keep("<color=#FFB74D>" + s + "</color>"); }
        public static void E(string tag, string msg) { var s = $"[{tag}] {msg}"; Debug.LogError(s); Keep("<color=#FF6A5A>" + s + "</color>"); }
    }

    public class DebugPanel : MonoBehaviour
    {
        public static bool Open;
        Image bg; Text txt; float fps, nextText;

        void Start()
        {
            var hud = HUD.I ? HUD.I.HudCanvas : null; if (!hud) { Invoke(nameof(Start), 1f); return; }
            bg = UIKit.Panel(hud.transform, "DebugPanel", new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(520, 620), new Color(0, 0, 0, 0.82f), new Vector2(1, 0.5f));
            txt = UIKit.Label(bg.transform, "", 16, Color.white, new Vector2(0, 1), new Vector2(12, -10), new Vector2(496, 600), TextAnchor.UpperLeft, new Vector2(0, 1));
            bg.gameObject.SetActive(false);
        }

        void Update()
        {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            var kb = Keyboard.current; if (kb == null || !bg) return;
            if (kb.f9Key.wasPressedThisFrame) { Open = !Open; bg.gameObject.SetActive(Open); HLog.I("Debug", "panel " + (Open ? "open" : "closed")); }
            if (!Open) return;
            if (kb.digit1Key.wasPressedThisFrame) LadderClimb.I?.SelfTest();
            if (kb.digit2Key.wasPressedThisFrame) DoorPassage.I?.RunWalkTest();
            if (kb.digit3Key.wasPressedThisFrame) { HabitatOps.I?.DebugAlert(); HLog.I("Debug", "repair alert forced"); }
            if (kb.digit4Key.wasPressedThisFrame) HabitatOps.I?.DebugCheer();
            if (kb.digit5Key.wasPressedThisFrame) SmartDebug.VisitNext();
            if (kb.digit6Key.wasPressedThisFrame && HabitatCleanup.I) { HabitatCleanup.Enabled = !HabitatCleanup.Enabled; HabitatCleanup.I.Apply(HabitatCleanup.Enabled); }
            if (Time.unscaledTime < nextText) return; nextText = Time.unscaledTime + 0.25f;
            var pr = PlayerRig.I; var env = HabitatEnvironment.I; var z = pr ? pr.zone : null;
            var aim = Aim.Smart ? Aim.Smart.title : Aim.Hab ? Aim.Hab.name : "—";
            long mem = System.GC.GetTotalMemory(false) / (1024 * 1024);
            var sb = new System.Text.StringBuilder();
            sb.Append("<b><color=#40D9FF>DEBUG</color></b>  F9 closes\n");
            sb.Append($"FPS <b>{fps:0}</b>   frame {Time.unscaledDeltaTime * 1000f:0.0} ms   managed mem {mem} MB\n");
            if (pr) sb.Append($"Player {pr.FeetPosition.x:0.0}, {pr.FeetPosition.y:0.0}, {pr.FeetPosition.z:0.0}   {(pr.isXR ? "VR" : "desktop")}\n");
            sb.Append($"Room <b>{(pr ? pr.zoneName : "—")}</b>   nearest waypoint {(pr ? Nav.Nearest(pr.FeetPosition) : "—")}\n");
            sb.Append($"Aimed at <b>{aim}</b>\n");
            if (env) sb.Append($"O2 {env.o2Percent:0.0}%   CO2 {(z != null ? z.co2 : 0):0} ppm   power {env.powerKW:0} kW (solar {env.solarKW:0})   water {env.waterL:0} L\n");
            sb.Append($"Crew agents {CrewAgent.All.Count}   smart objects {SmartObject.All.Count(o => o)}   faults {SmartObject.All.Count(o => o && o.Failed)}\n");
            sb.Append($"Sim {SimClock.Now}  ×{GameState.timeScale / 60f:0.#}   alarm {(Hazards.I && Hazards.I.Active ? Hazards.I.Kind : "none")}\n");
            sb.Append("\n<b>Tests</b>  1 ladder ride · 2 door walk · 3 repair alert · 4 reward · 5 next smart object · 6 show/hide cut objects\n");
            sb.Append("\n<b>Last logs</b>\n<size=13>");
            foreach (var l in HLog.Recent.Reverse()) sb.Append(l).Append('\n');
            sb.Append("</size>");
            txt.text = sb.ToString();
        }
    }

    /// Scene-view debug drawing (Gizmos only show in the Editor's Scene view or with Gizmos on in the Game view)
    public class HabitatGizmos : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            var env = HabitatEnvironment.I;
            if (env) { Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f); foreach (var z in env.zones) Gizmos.DrawWireSphere(z.center, z.radius); }   // cyan: air / light zones
            Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.8f);                                                                                    // blue: routes
            foreach (var kv in Nav.adj) if (Nav.wp.TryGetValue(kv.Key, out var a) && a) foreach (var n in kv.Value) if (Nav.wp.TryGetValue(n, out var b) && b) Gizmos.DrawLine(a.position + Vector3.up * .1f, b.position + Vector3.up * .1f);
            foreach (var ag in CrewAgent.All) if (ag && Nav.wp.TryGetValue(ag.goal ?? "", out var g) && g) Gizmos.DrawLine(ag.transform.position + Vector3.up, g.position + Vector3.up);
            foreach (var o in SmartObject.All)
            {
                if (!o) continue;
                Gizmos.color = o.Failed ? new Color(1f, 0.85f, 0.2f, 0.9f) : new Color(0.3f, 1f, 0.45f, 0.35f);                                  // green reach / yellow fault
                Gizmos.DrawWireSphere(o.transform.position, Aim.SmartReach);
            }
        }
    }
}
