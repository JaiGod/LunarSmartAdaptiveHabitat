using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Hunch3
{
    /// Opens panels in front of the player (world-space in VR, screen overlay on desktop).
    public static class Panels
    {
        static readonly Dictionary<string, GameObject> open = new Dictionary<string, GameObject>();
        public static bool AnyOpen => open.Values.Any(g => g);
        public static string crewTab = "wellness";
        public static void Toggle(string id) { if (open.TryGetValue(id, out var g) && g) Close(id); else Open(id); }
        public static void Close(string id) { if (open.TryGetValue(id, out var g) && g) UnityEngine.Object.Destroy(g); open.Remove(id); if (!AnyOpen && PlayerRig.I && !PlayerRig.I.isXR) { } }
        public static void CloseAll() { foreach (var k in open.Keys.ToList()) Close(k); }
        public static void Open(string id)
        {
            CloseAll();
            var pr = PlayerRig.I; bool world = pr && pr.isXR;
            var c = UIKit.Canvas("Panel_" + id, world, new Vector2(1500, 950));
            if (world) { var t = pr.cam.transform; var f = t.forward; f.y = 0; f.Normalize(); c.transform.position = t.position + f * 1.3f; c.transform.rotation = Quaternion.LookRotation(f); c.transform.localScale = Vector3.one * 0.0011f; }
            else { c.sortingOrder = 20; Cursor.lockState = CursorLockMode.None; }
            var bg = UIKit.Panel(c.transform, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 950)); UIKit.Round(bg, 0.5f);
            var host = c.gameObject.AddComponent<PanelHost>(); host.id = id; host.root = bg.transform;
            open[id] = c.gameObject; host.Build();
        }
    }

    public class PanelHost : MonoBehaviour
    {
        public string id; public Transform root; Text body; float next; readonly List<GameObject> dyn = new List<GameObject>();
        MoodEngine E => Sim.I.engine;
        Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1);

        public void Build()
        {
            string title = id switch { "crew" => "CREW INTERFACE — Cmdr. J. Rana (only you see this)", "dashboard" => "AI PSYCHOLOGICAL MONITORING DASHBOARD — Command", "cmo" => "CHIEF MEDICAL OFFICER CONSOLE — Tier 3 queue", "meeting" => "DAILY PLANNING CONFERENCE — Team meeting", "tabletop" => "TABLETOP SIMULATION — 72-hour mission window", "scenarios" => "AGENCY REVIEW BOARD — live scenarios Q1–Q6", "nav" => "NAVIGATE — choose a destination", "menu" => "MAIN MENU (VR wrist menu · Q on desktop)", "eclss" => "ECLSS — life support status", "drive" => "PRESSURISED / OPEN ROVER — EVA destinations", "docs" => "PROJECT ARES DELIVERABLES D1–D4 (live from the AI engine) · F11", _ => id };
            UIKit.Label(root, $"<b>{title}</b>", 32, UIKit.Accent, TL, new Vector2(24, -14), new Vector2(1300, 46), TextAnchor.UpperLeft, TL);
            UIKit.Btn(root, "Close ✕", TR, new Vector2(-20, -14), new Vector2(150, 50), () => Panels.Close(id), new Color(0.45f, 0.15f, 0.15f), 22).GetComponent<RectTransform>().pivot = TR;
            body = UIKit.Label(root, "", 21, Color.white, TL, new Vector2(24, -70), new Vector2(940, 860), TextAnchor.UpperLeft, TL);
            Buttons(); Refresh();
        }
        void Btn(string label, int col, int row, Action a, Color? c = null)
        {
            var b = UIKit.Btn(root, label, TR, new Vector2(-20 - col * 500, -80 - row * rowStep), new Vector2(480, rowStep - 8), () => { a(); Refresh(); Rebuild(); }, c, 20);
            b.GetComponent<RectTransform>().pivot = TR; dyn.Add(b.gameObject);
        }
        void Rebuild() { foreach (var g in dyn) Destroy(g); dyn.Clear(); Buttons(); }
        void Update() { if (Time.time > next) { next = Time.time + 0.5f; Refresh(); } }

        float rowStep = 62f;
        void Buttons()
        {
            var me = E.C(1); int r = 0;
            rowStep = id == "crew" && (Panels.crewTab == "privacy" || Panels.crewTab == "overrides") ? 56f : 62f;
            switch (id)
            {
                case "crew":
                    foreach (var tab in new[] { "wellness", "ai actions", "overrides", "privacy", "preferences" })
                    { string t = tab; Btn((Panels.crewTab == t ? "▶ " : "") + t.ToUpper(), 0, r++, () => Panels.crewTab = t, Panels.crewTab == t ? new Color(0.15f, 0.5f, 0.7f) : null); }
                    r++;
                    if (Panels.crewTab == "wellness")
                    {
                        string[] ml = { "good", "OK", "low" }, sl = { "low", "medium", "high" };
                        int mi = me.selfMood < 0 ? -1 : Mathf.RoundToInt(me.selfMood * 2), si = me.selfStress < 0 ? -1 : Mathf.RoundToInt(me.selfStress * 2);
                        Btn("Self-check 1/2 · my mood today: " + (mi < 0 ? "tap" : ml[mi]), 0, r++, () => { int n = (mi + 1) % 3; E.SelfCheck(me, n / 2f, me.selfStress < 0 ? 0f : me.selfStress); }, new Color(0.2f, 0.4f, 0.55f));
                        Btn("Talk to the AI companion (private)", 0, r++, () => HUD.I?.Toast("AI: " + E.Companion(me)), new Color(0.35f, 0.3f, 0.6f));
                        Btn("Self-check 2/2 · my stress today: " + (si < 0 ? "tap" : sl[si]), 0, r++, () => { int n = (si + 1) % 3; E.SelfCheck(me, me.selfMood < 0 ? 0f : me.selfMood, n / 2f); }, new Color(0.2f, 0.4f, 0.55f));
                    }
                    if (Panels.crewTab == "overrides")
                    {
                        Btn(AIAuthority.Blocked ? "✓ AI DISALLOWED — tap to allow again" : "⛔ DISALLOW AI CONTROL (off switch)", 0, r++, () => AIAuthority.Toggle(), AIAuthority.Blocked ? new Color(0.15f, 0.45f, 0.25f) : new Color(0.75f, 0.1f, 0.1f));
                        for (int k = 0; k < 6; k++) if (me.tier[k] > 0) { int kk = k; Btn($"STOP: {CondSpec.Name[k]} support", 0, r++, () => E.Refuse(me, kk), new Color(0.5f, 0.2f, 0.15f)); }
                        Btn("That reading is wrong (teach the AI)", 0, r++, () => HUD.I?.Toast(E.Feedback(me)), new Color(0.35f, 0.3f, 0.6f));
                        Btn("Pause ALL AI adjustments 24 h", 0, r++, () => { me.pauseUntilHour = SimClock.hours + 24; E.Log(1, "pause", "Crew paused all AI adjustments for 24 h."); }, new Color(0.5f, 0.2f, 0.15f));
                        Btn((me.notifyEverySilentChange ? "☑" : "☐") + " Tell me about EVERY silent change", 0, r++, () => me.notifyEverySilentChange = !me.notifyEverySilentChange);
                        Btn("Restore my room to default", 0, r++, () => { for (int k = 0; k < 6; k++) if (me.tier[k] > 0) E.Refuse(me, k); HabitatEnvironment.I?.Apply("restore:", 1, 0); E.Log(1, "override", "Crew restored their environment to default; AI holds back."); });
                    }
                    else if (Panels.crewTab == "privacy")
                    {
                        Btn((me.aiPodAccess ? "☑" : "☐") + " Invite AI into my quarters", 0, r++, () => me.aiPodAccess = !me.aiPodAccess);
                        Btn((me.shareMoodPublic ? "☑" : "☐") + " Show my mood on my beacon", 0, r++, () => me.shareMoodPublic = !me.shareMoodPublic);
                        foreach (int s in new[] { 3, 4, 6, 1 }) { int ss = s; Btn((me.optOut[s] ? "☑ opted out: " : "☐ opt out: ") + SensorSpec.Name[s], 0, r++, () => { me.optOut[ss] = !me.optOut[ss]; E.Log(1, "privacy", (me.optOut[ss] ? "Opted out of " : "Re-enabled ") + SensorSpec.Name[ss]); }); }
                        Btn("Pause psych monitoring 72 h (max)", 0, r++, () => { me.pauseUntilHour = SimClock.hours + 72; E.Log(1, "privacy", "Psych monitoring paused 72 h; CMO told only that a pause is active."); });
                        Btn("Export my full data log", 0, r++, () => ExportLog(me));
                        Btn("Delete my AI history", 0, r++, () => HUD.I?.Toast($"Deleted {E.DeleteMyHistory(me)} entries. Only Tier-3 safety records remain (you + CMO only)."), new Color(0.5f, 0.2f, 0.15f));
                        Btn((me.shareHealthWithCmo ? "☑" : "☐") + " Share my health-station results with the CMO", 0, r++, () => { me.shareHealthWithCmo = !me.shareHealthWithCmo; E.Log(1, "privacy", me.shareHealthWithCmo ? "Health-station results now shared with the CMO." : "Health-station results private again."); });
                    }
                    else if (Panels.crewTab == "preferences")
                    {
                        Btn("Pod light: warm 2700 K", 0, r++, () => { if (EthicsGuard.CanActInPod(me, "pod light")) E.Log(1, "pref", "Pod light set to warm 2700 K."); else E.Log(1, "pref", "Preference saved; applied when you invite the AI into your pod."); });
                        Btn("My quarters: warmer +0.5 °C", 0, r++, () => MyRoom(me, +0.5f));
                        Btn("My quarters: cooler −0.5 °C", 0, r++, () => MyRoom(me, -0.5f));
                        Btn("Scent: lavender / citrus / none", 0, r++, () => E.Log(1, "pref", "Scent preference cycled."));
                        Btn("Music: pre-mission playlist", 0, r++, () => E.Log(1, "pref", "Favourite playlist queued for free time."));
                    }
                    break;
                case "dashboard":
                    Btn("Human system review ✓ (resets 7-day timer)", 0, r++, () => { EthicsGuard.lastReviewHour = SimClock.hours; E.Log(0, "review", "Human system review completed by Commander."); }, new Color(0.15f, 0.45f, 0.25f));
                    Btn("Toggle Config A ↔ B", 0, r++, () => HabitatEnvironment.I?.SetConfigB(!GameState.configB));
                    Btn("Inject: Tanaka cortisol patch FAILS", 0, r++, () => { E.C(3).sensors[6] = SensorHealth.Failed; E.Log(3, "sensor", "Cortisol patch failed — degraded mode (weights renormalised, confidence ↓)."); });
                    Btn("Inject: Rivera voice mic DEGRADED", 0, r++, () => { E.C(4).sensors[3] = SensorHealth.Degraded; E.Log(4, "sensor", "Voice array degraded (noise) — weight halved."); });
                    Btn("Repair all sensors", 0, r++, () => { foreach (var c in E.crew) for (int s = 0; s < 9; s++) c.sensors[s] = SensorHealth.OK; });
                    Btn("Try: AI raises O2 set-point (blocked)", 0, r++, () => EthicsGuard.LifeSafety("AI request to change O2 partial-pressure set-point"));
                    Btn("Try: show Rivera's data to you (blocked)", 0, r++, () => EthicsGuard.CanViewDetail(E, 1, 4));
                    break;
                case "cmo":
                    foreach (var kv in E.escalatedTo.ToList()) { int subj = kv.Key; if (kv.Value == 1) Btn("Acknowledge: " + E.C(subj).name, 0, r++, () => E.Acknowledge(subj), new Color(0.6f, 0.2f, 0.15f)); }
                    break;
                case "meeting":
                    Btn("Let everyone choose their task order", 0, r++, () => { foreach (var c in E.crew) { c.autonomy += 0.15f; } E.Log(0, "meeting", "Team chose their own task order (autonomy ↑)."); });
                    Btn("Plan game night in the Commons", 0, r++, () => { foreach (var c in E.crew) { c.novelty += 0.2f; c.mood += 0.05f; } E.Log(0, "meeting", "Game night planned (novelty ↑, cohesion ↑)."); });
                    Btn("Offer private mediated check-ins (opt-in)", 0, r++, () => { for (int a = 1; a <= 4; a++) for (int b = 1; b <= 4; b++) E.C(a).tension[b] *= 0.75f; E.Log(0, "meeting", "Opt-in mediated check-ins offered; no one's data was disclosed."); });
                    Btn("Rebalance workload (lighter afternoon)", 0, r++, () => { foreach (var c in E.crew) c.fatigue -= 0.1f; E.Log(0, "meeting", "Workload rebalanced."); });
                    Btn("Book Earth family-call slots", 0, r++, () => { foreach (var c in E.crew) c.mood += 0.05f; E.Log(0, "meeting", "Family-call slots booked in the private booth."); });
                    break;
                case "docs":
                    Btn("◀ Previous page", 0, r++, () => DesignDocs.page--, new Color(0.2f, 0.3f, 0.45f));
                    Btn("Next page ▶", 0, r++, () => DesignDocs.page++, new Color(0.15f, 0.45f, 0.6f));
                    r++;
                    foreach (var sec in DesignDocs.Sections) { string ss = sec; bool here = DesignDocs.Current.section == ss; Btn((here ? "▶ " : "") + ss, 0, r++, () => DesignDocs.Jump(ss), here ? new Color(0.15f, 0.5f, 0.7f) : null); }
                    r++;
                    Btn("Export all to Reports folder", 0, r++, () => HUD.I?.Toast("Saved: " + DesignDocs.ExportAll()), new Color(0.15f, 0.45f, 0.25f));
                    break;
                case "tabletop":
                    Btn("▶ Run 72-hour simulation", 0, r++, () => { TabletopSim.Run(); }, new Color(0.15f, 0.45f, 0.25f));
                    break;
                case "scenarios":
                    for (int q = 1; q <= 6; q++) { int qq = q; Btn("Q" + q + ": " + Scenarios.Short[q - 1], 0, r++, () => Scenarios.Run(qq)); }
                    break;
                case "menu":
                    {
                        var items = new (string, Action)[] {
                            (AIAuthority.Blocked ? "✓ AI is DISALLOWED — tap to allow again" : "⛔ AI OFF SWITCH (disallow AI control) · O", () => { Panels.CloseAll(); AIAuthority.Toggle(); }),
                            ("Rubric task checklist · J", () => { Panels.CloseAll(); RubricTasks.I?.OpenList(); }),
                            ("Crew health card (live) · V", () => { Panels.CloseAll(); CrewHealthCard.I?.Toggle(); }),
                            ("Find my bed · B", () => { Panels.CloseAll(); BedFinder.I?.Toggle(); }),
                            ("Third-person view · F", () => { Panels.CloseAll(); ThirdPersonView.I?.Toggle(); }),
                            ("Crew Interface (my data & overrides)", () => Panels.Open("crew")), ("Navigate to a room", () => Panels.Open("nav")),
                            ("ARES deliverables D1–D4 · F11", () => Panels.Open("docs")), ("AI Dashboard (sensors, matrix, limits)", () => Panels.Open("dashboard")), ("CMO console (Tier 3)", () => Panels.Open("cmo")),
                            ("Team meeting", () => Panels.Open("meeting")), ("Tabletop simulation (72 h)", () => Panels.Open("tabletop")),
                            ("Board scenarios Q1–Q6", () => Panels.Open("scenarios")), ("Big map on / off", () => { Panels.CloseAll(); HUD.I?.ToggleBigMap(); }),
                            ("Autopilot on / off", () => { Panels.CloseAll(); if (PlayerRig.I) PlayerRig.I.autopilot = !PlayerRig.I.autopilot; }),
                            ("Config A ↔ B (conflict protocol)", () => HabitatEnvironment.I?.SetConfigB(!GameState.configB)),
                            ("Safety drill (alarm + muster)", () => { Panels.CloseAll(); if (Hazards.I) { if (Hazards.I.Active) Hazards.I.Clear("drill ended"); else Hazards.I.Trigger(Hazards.Kinds[UnityEngine.Random.Range(0, Hazards.Kinds.Length)], "DRILL"); } }),
                            ("Time speed ×1 / ×10 / ×60", () => GameState.timeScale = GameState.timeScale >= 600 ? 60 : GameState.timeScale >= 60 && GameState.timeScale < 600 ? 600 : 60),
                            ("Unstuck (move to nearest walkway)", () => { Panels.CloseAll(); DoorPassage.I?.Unstuck(); }),
                            ("Cargo lander: launch / land", () => { Panels.CloseAll(); SpaceRealism.I?.CallLander(); }),
                            (DomeLights.AIControl ? "AI control: ON (tap to turn off)" : "AI control: OFF (optional, tap to let the AI run your day)", () => { Panels.CloseAll(); DomeLights.I?.SetAIControl(!DomeLights.AIControl); }),
                            ("Outside cameras (next view)", () => { Panels.CloseAll(); SpaceRealism.I?.NextFeed(); }),
                            ("Quick travel (ladder · quarters)", () => { Panels.CloseAll(); PlayerRig.I?.QuickTravel(); }),
                            ("Show / hide shortcut bar", () => HUD.I?.ToggleKeys()),
                            ("Help / controls", () => { Panels.CloseAll(); HUD.I?.ToggleHelp(); }),
                            ("Exit to main menu", () => UnityEngine.SceneManagement.SceneManager.LoadScene(0)) };
                        for (int i = 0; i < items.Length; i++) { var it = items[i]; Btn(it.Item1, i / 12, i % 12, it.Item2); }
                    }
                    break;
                case "nav":
                    foreach (var kv in Nav.Places) { var v = kv; Btn(kv.Key, r / 12, r % 12, () => { HUD.I?.SetNavTarget(v.Value, v.Key); Panels.Close("nav"); }); r++; }
                    break;
                case "eclss":
                    Btn("Crew: run CO2 scrubber boost (human OK)", 0, r++, () => { foreach (var z in HabitatEnvironment.I.zones) z.co2 -= 60; E.Log(1, "eclss", "Commander boosted CO2 scrubbing."); });
                    Btn("AI: change O2 set-point (blocked)", 0, r++, () => EthicsGuard.LifeSafety("AI request to change O2 set-point"));
                    Btn("Transfer Hygiene-2 urine tank to UPA", 0, r++, () => E.Log(1, "eclss", "Pretreated urine transferred to UPA for water recovery."));
                    break;
                case "drive":
                    foreach (var d in new[] { ("Solar tower array (55 m)", new Vector3(0, 0, 0), "P013_SolarTower_0_Gimbal"), ("Comm tower (20 m)", Vector3.zero, "M07_CommTower_BeaconLight"), ("Landing pad (185 m)", Vector3.zero, "LOG_G30_LandingPad_Sintered"), ("Fission reactor (1 km)", Vector3.zero, "P25_FSP_Mast") })
                    { var dd = d; Btn(dd.Item1, 0, r++, () => Drive(dd.Item3, dd.Item1)); }
                    break;
            }
        }

        void Drive(string target, string label)
        {
            var pr = PlayerRig.I; if (!pr) return;
            if (pr.suit != SuitState.EVA) { HUD.I?.Toast("You need to be on EVA (suited up) to take the rover."); return; }
            var t = Util.Find(target); if (!t) { HUD.I?.Toast("Destination not found."); return; }
            Vector3 p = t.position; p.y = 0; var hit = Physics.Raycast(p + Vector3.up * 200, Vector3.down, out var h, 400) ? h.point : p;
            pr.Teleport(hit + (pr.FeetPosition - hit).normalized * 6f + Vector3.up * 0.3f);
            HUD.I?.Toast($"Rover traverse complete → {label}. (Suit O2 keeps counting!)"); Panels.Close(id);
        }
        /// private retreat: temperature is the OCCUPANT's control, independent of the rest of the habitat (18–26 °C)
        void MyRoom(CrewMember me, float d)
        {
            var env = HabitatEnvironment.I; if (!env) return;
            foreach (var z in env.zones.Where(z => z.name == "Private_Quarters1" || z.name == "Private_Pod1"))
            { z.tempC = Mathf.Clamp(z.tempC + d, 18f, 26f); z.dTemp = z.tempC; }
            var q = env.Get("Private_Quarters1");
            E.Log(1, "pref", $"You set your quarters to {(q != null ? q.tempC : 21.5f):0.0} °C (your room only; the AI never changes it unless you invite it).");
        }
        void ExportLog(CrewMember me)
        {
            var sb = new StringBuilder($"# Personal data log — {me.name}\nExported {SimClock.Now}. Only you can request this file.\n\n");
            foreach (var l in me.log) sb.AppendLine($"- {l.Time} [{l.kind}] T{l.tier} {l.text}");
            string p = System.IO.Path.Combine(GameState.ReportsDir, "MyDataLog_Commander.md"); System.IO.File.WriteAllText(p, sb.ToString());
            E.Log(1, "privacy", "Full personal log exported to " + p); HUD.I?.Toast("Exported: " + p);
        }

        void Refresh()
        {
            if (!body || Sim.I == null) return;
            var me = E.C(1); var sb = new StringBuilder();
            switch (id)
            {
                case "crew":
                    if (Panels.crewTab == "wellness")
                    {
                        sb.AppendLine($"<b>Wellbeing {me.Wellbeing:P0}</b>  ·  detection confidence {me.confidence:P0}  ·  activity: {me.activity}");
                        sb.AppendLine("<b>Condition signals (only you and — on escalation — the CMO can see this)</b>");
                        for (int k = 0; k < 6; k++) sb.AppendLine($"<color=#{UIKit.Hex(UIKit.TierCol(me.tier[k]))}>{CondSpec.Name[k],-24} {UIKit.Bar(me.smooth[k], 20)} {me.smooth[k]:0.00}  Tier {me.tier[k]}</color>");
                        sb.AppendLine("\n<b>Your sensors</b>");
                        for (int s = 0; s < 9; s++) sb.AppendLine($"{SensorSpec.Name[s],-28} {(me.optOut[s] ? "<color=#FF9933>opted out</color>" : me.sensors[s].ToString())}  ({SensorSpec.Where[s]})");
                        sb.AppendLine($"\nPod AI access: {(me.aiPodAccess ? "INVITED" : "not invited (default)")} · Beacon shows mood: {(me.shareMoodPublic ? "yes" : "no — comm status only")}");
                    }
                    else if (Panels.crewTab == "ai actions")
                    {
                        sb.AppendLine("<b>Everything the AI has done on your behalf (newest first)</b> — Tier 1 changes are silent but never secret.");
                        foreach (var l in me.log.AsEnumerable().Reverse().Take(18)) sb.AppendLine($"<color=#{UIKit.Hex(UIKit.TierCol(l.tier))}>{l.Time}  [{l.kind}]</color> {l.text}");
                        if (me.log.Count == 0) sb.AppendLine("(nothing yet)");
                    }
                    else if (Panels.crewTab == "overrides")
                    {
                        sb.AppendLine("<b>Complete override.</b> Any intervention stops immediately when you say so — the AI logs it and backs off for 6 h (hard limit #2).");
                        sb.AppendLine("Tier 3 still reaches a <i>human</i> (the CMO) if a severe signal persists — the AI never acts on you against your refusal.\n");
                        for (int k = 0; k < 6; k++) if (me.tier[k] > 0) sb.AppendLine($"Active: {CondSpec.Name[k]} support (Tier {me.tier[k]})");
                        if (me.MaxTier == 0) sb.AppendLine("No active interventions right now.");
                        if (me.pauseUntilHour > SimClock.hours) sb.AppendLine($"\n<color=#FF9933>All AI adjustments paused until {SimClock.Format(me.pauseUntilHour)}</color>");
                    }
                    else if (Panels.crewTab == "privacy")
                    {
                        sb.AppendLine("<b>Privacy controls & opt-out pathway</b>");
                        sb.AppendLine("• You may opt out of voice, face, cortisol and GSR sensing at any time.");
                        sb.AppendLine("• Heart-rate (vital-sign floor) and CO2 (shared air) stay on: they protect crew safety, are not psychological data, and are only ever used for life-threatening alarms.");
                        sb.AppendLine("• A full psych-monitoring pause is limited to 72 h; a permanent opt-out needs a conversation with the CMO because others' safety may depend on it (board Q5).");
                        sb.AppendLine("• Raw data never leaves your beacon — only tier flags. Other crew never see your data. Mission Control is only told after YOU are told.");
                        sb.AppendLine("• Export your full log any time (button).");
                    }
                    else sb.AppendLine("<b>Preferences</b> — applied only in your pod if you invite the AI, otherwise in shared zones you use.\nPre-mission profile: warm light, lavender, piano playlist, morning bright-light therapy: yes.");
                    break;
                case "dashboard":
                    sb.AppendLine($"<b>{SimClock.Now}</b> · active supports {E.interventionsActive} · escalations {E.escalations} · blocked actions {EthicsGuard.blocked} · human review {(EthicsGuard.ReviewOverdue ? "<color=#FF4433>OVERDUE → AI limited to Tier 1</color>" : $"due {SimClock.Format(EthicsGuard.lastReviewHour + 168)}")}");
                    sb.AppendLine("<b>Crew (aggregate status only — no individual data on this screen)</b>");
                    foreach (var c in E.crew) sb.AppendLine("  " + (c.id == 1 ? E.CrewLine(c, 1) : $"{c.name}: {(c.MaxTier >= 3 ? "With CMO" : c.MaxTier == 2 ? "Support offered" : "OK")} · {c.activity}"));
                    sb.AppendLine("<b>Sensor health matrix</b>   HRV GSR SLP VOC FAC MOT COR CO2 TSK");
                    foreach (var c in E.crew) sb.AppendLine($"  {c.name.Split(' ')[1],-10} " + string.Join("  ", Enumerable.Range(0, 9).Select(s => c.optOut[s] ? "<color=#FF9933>OO</color>" : c.sensors[s] == SensorHealth.OK ? "<color=#4CFF8C>OK</color>" : c.sensors[s] == SensorHealth.Degraded ? "<color=#FFAA33>DG</color>" : "<color=#FF4433>XX</color>")) + $"   conf {c.confidence:P0}");
                    sb.AppendLine("<b>Sensor → condition weights</b>         HRV GSR SLP VOC FAC MOT COR CO2 TSK");
                    for (int k = 0; k < 6; k++) sb.AppendLine($"  {CondSpec.Name[k],-24} " + string.Join(" ", Enumerable.Range(0, 9).Select(s => (SensorSpec.W[k, s] * 100).ToString("00"))));
                    sb.AppendLine("<b>Hard limits (enforced in code)</b>");
                    for (int i = 0; i < EthicsGuard.HardLimits.Length; i++) sb.AppendLine($"  {i + 1}. {EthicsGuard.HardLimits[i]}");
                    if (EthicsGuard.blockedLog.Count > 0) sb.AppendLine("<color=#FF9933>" + EthicsGuard.blockedLog.Last() + "</color>");
                    sb.AppendLine($"Environment: {HabitatEnvironment.I?.lastAction}");
                    break;
                case "cmo":
                    sb.AppendLine("Tier 3 = immediate human chain of care. Max response: Tier 1 < 1 min (automatic) · Tier 2 offer < 5 min · Tier 3 acknowledgement < 15 min, else backup: Commander → Mission Control.");
                    sb.AppendLine("The crew member is ALWAYS told before anyone is alerted.\n");
                    if (E.escalatedTo.Count == 0) sb.AppendLine("No active escalations.");
                    foreach (var kv in E.escalatedTo)
                    {
                        var s = E.C(kv.Key); string who = kv.Value == 0 ? "Mission Control" : E.C(kv.Value).name;
                        sb.AppendLine($"<b>{s.name}</b> → responder: {who}");
                        if (kv.Value == 1) { for (int k = 0; k < 6; k++) if (s.tier[k] > 0) sb.AppendLine($"   {CondSpec.Name[k]}: Tier {s.tier[k]}  ({s.smooth[k]:0.00})"); }
                        else sb.AppendLine("   Details visible to the assigned responder only (hard limit #1).");
                    }
                    break;
                case "meeting":
                    int present = CrewAgent.All.Count(a => (a.transform.position - Nav.wp.GetValueOrDefault("WP_Up_Table")?.position ?? Vector3.zero).sqrMagnitude < 12f) + 1;
                    sb.AppendLine($"<b>{SimClock.Now}</b> — crew at the table: {present}/4");
                    sb.AppendLine("Agenda: 1) EVA plan  2) ECLSS & greenhouse status  3) Schedule choices  4) Open floor");
                    float avgF = E.crew.Average(c => c.fatigue), avgN = E.crew.Average(c => c.novelty), maxT = E.crew.Max(c => c.tension.Max());
                    sb.AppendLine("\n<b>AI suggestions (team-level only — nobody is singled out)</b>");
                    if (avgF > 0.45f) sb.AppendLine("• Team fatigue is elevated → consider a lighter afternoon.");
                    if (avgN < 0.5f) sb.AppendLine("• Days are getting samey → something new this week (game night, rim EVA).");
                    if (maxT > 0.35f) sb.AppendLine("• Team dynamics check: offer opt-in, private check-ins.");
                    if (avgF <= 0.45f && avgN >= 0.5f && maxT <= 0.35f) sb.AppendLine("• Team looks good. Keep the routine and protect free time.");
                    sb.AppendLine("\nRecent decisions:"); foreach (var l in E.log.Where(l => l.kind == "meeting").Reverse().Take(5)) sb.AppendLine("  " + l.Time + " " + l.text);
                    break;
                case "docs":
                    {
                        var pg = DesignDocs.Current;
                        sb.AppendLine($"<b><color=#{UIKit.Hex(UIKit.Gold)}>{pg.title}</color></b>   <size=16>(page {DesignDocs.page + 1}/{DesignDocs.Pages.Count})</size>");
                        sb.Append(pg.body());
                    }
                    break;
                case "tabletop": sb.Append(TabletopSim.lastReport ?? "Runs the real AI engine headless over 72 sim-hours with two simultaneous events (depression + conflict), a cortisol-sensor failure and the CMO on EVA during a Tier 3. Produces a timeline, successes/failures and redesign notes, saved to Reports/."); break;
                case "scenarios": sb.Append(Scenarios.lastExplain ?? "Pick a board question. The scenario is set up LIVE in the habitat and the AI's response is explained here and in the dashboard/CMO console."); break;
                case "menu": sb.Append("Point the ray,\npull the trigger.\n\nVR buttons\nY  crew UI\nX  map\nB  autopilot\nA  accept offer\nL-stick click\n   this menu\nGrip  teleport\nL-stick  move\nR-stick  turn\nHand up + wave\n   say hello"); break;
                case "nav": sb.Append("Pick a room.\nA gold arrow, an on-screen compass and a glowing floor trail guide you there\n(ladder between decks)."); break;
                case "eclss": var env = HabitatEnvironment.I; sb.AppendLine($"O2 {env.o2Percent:0.0}% · pressure {env.pressureKPa:0.0} kPa · water {env.waterL:0} L (98% recovery) · power {env.powerKW:0} kW"); foreach (var z in env.zones.Where(z => z.type != "transition")) sb.AppendLine($"  {z.name,-24} CO2 {z.co2:0} ppm  {z.tempC:0.0} °C  RH {z.humidity:0}%  air {z.airflow:0.00} m/s  scent {z.scent}"); sb.AppendLine("\nAI may adjust comfort levers only. Life-safety set-points are human-only (hard limit #3)."); break;
                case "drive": sb.Append("Rover traverses (must be suited). Real NASA spacing: fission power ~1 km from crew."); break;
            }
            body.text = sb.ToString();
        }
    }
}
