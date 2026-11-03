using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hunch3
{
    /// Automated beta test: run the build with -selftest. Visits every feature, captures screenshots, records errors, quits.
    public class SelfTest : MonoBehaviour
    {
        static readonly List<string> errors = new List<string>(); static readonly List<string> checks = new List<string>(); static bool started;
        string dir;
        void Awake()
        {
            if (started) { Destroy(this); return; }
            started = true; DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += (msg, st, type) => { if (type == LogType.Exception || type == LogType.Error) errors.Add(type + ": " + msg + " | " + (st ?? "").Split('\n').FirstOrDefault()); };
            dir = System.IO.Path.Combine(GameState.ReportsDir, "selftest"); System.IO.Directory.CreateDirectory(dir);
            GameState.timeScale = 60f;
        }
        void Start() { StartCoroutine(Run()); }
        void Check(string id, bool ok, string note = "") { checks.Add($"{(ok ? "PASS" : "FAIL")} | {id} | {note}"); }
        IEnumerator Shot(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png")); yield return new WaitForSeconds(0.4f); }

        IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);
            Check("U0.1 menu scene loaded", SceneManager.GetActiveScene().buildIndex == 0, SceneManager.GetActiveScene().name);
            Check("U0.2 menu buttons", GameObject.Find("Menu_0") != null && GameObject.Find("Menu_7") != null);
            yield return Shot("01_menu_vr_box");
            MenuBoot.Go(GameMode.Sandbox);
            yield return new WaitForSeconds(4f);
            Check("habitat scene loaded", SceneManager.GetActiveScene().buildIndex == 1);
            var pr = PlayerRig.I; var e = Sim.I?.engine;
            Check("systems online", pr && e != null && HabitatEnvironment.I && HUD.I, $"zones={HabitatEnvironment.I?.zones.Count} waypoints={Nav.wp.Count} agents={CrewAgent.All.Count}");
            Check("U2.1 six ARES zone types", HabitatEnvironment.I && new[] { "communal", "private", "work", "exercise", "nature", "transition" }.All(t => HabitatEnvironment.I.zones.Any(z => z.type == t)));
            Check("U8.4 1/6 gravity", Mathf.Abs(Physics.gravity.y + 1.62f) < 0.01f);
            Check("U8.4 AI crewmates", CrewAgent.All.Count == 3);
            yield return Shot("02_habitat_spawn_wardroom");
            // walk/teleport tests
            foreach (var wp in new[] { "WP_Pod1_Door", "WP_Hyg_In", "WP_B_Lab", "WP_C_Mid", "WP_D_Ex", "WP_N_C", "WP_R_C", "WP_H_C", "WP_A_Suit" })
            {
                if (!Nav.wp.ContainsKey(wp)) { Check("waypoint " + wp, false); continue; }
                pr.Teleport(Nav.wp[wp].position, 90f); yield return new WaitForSeconds(0.6f);
                var z = HabitatEnvironment.I.ZoneAt(pr.FeetPosition);
                Check("zone at " + wp, z != null, z?.name);
                if (wp == "WP_Pod1_Door" || wp == "WP_C_Mid" || wp == "WP_D_Ex" || wp == "WP_N_C") yield return Shot("03_" + wp);
            }
            // pathfinding
            var path = Nav.Path(Nav.wp["WP_Up_Table"].position, "WP_H_C"); Check("A* wardroom→medical (via ladder)", path.Count > 5, path.Count + " nodes");
            var loop = Nav.Path(Nav.wp["WP_N_C"].position, "WP_G_C"); Check("A* loop hallway route", loop.Count > 3, loop.Count + " nodes");
            // navigation arrows
            pr.Teleport(Nav.wp["WP_Up_E"].position, 0); yield return null;
            HUD.I.SetNavTarget("WP_C_Mid", "Greenhouse"); yield return new WaitForSeconds(0.5f);
            Check("U-nav arrows + trail", GameObject.Find("NavArrow") && GameObject.Find("NavArrow").activeSelf && GameObject.Find("NavChevron") != null);
            HUD.I.ToggleBigMap(); yield return new WaitForSeconds(0.3f); yield return Shot("04_big_map_nav"); HUD.I.ToggleBigMap();
            Check("map hologram on command table", GameObject.Find("HoloMap_Model") != null);
            // panels
            foreach (var p in new[] { "crew", "dashboard", "cmo", "meeting", "scenarios", "tabletop", "eclss", "nav" })
            { Panels.Open(p); yield return new WaitForSeconds(0.7f); Check("panel " + p, GameObject.Find("Panel_" + p) != null); if (p == "crew" || p == "dashboard" || p == "meeting") yield return Shot("05_panel_" + p); }
            foreach (var tab in new[] { "ai actions", "overrides", "privacy" }) { Panels.crewTab = tab; Panels.Open("crew"); yield return new WaitForSeconds(0.5f); yield return Shot("06_crew_" + tab.Replace(" ", "_")); }
            Panels.CloseAll();
            // AI protocols: force a moderate anxiety on the player → expect offer
            var me = e.C(1); me.stress = 0.95f; me.smooth[0] = 0.6f; me.aboveSince[0] = SimClock.hours - 1; e.Step(0.02f); yield return new WaitForSeconds(0.5f);
            Check("U3.3 Tier 2 offer to player", e.offers.Any(o => o.crew == 1), "offers=" + e.offers.Count);
            yield return Shot("07_ai_offer");
            HUD.I.AnswerOffer(false); Check("U4.3 refusal stops & logs", me.log.Any(l => l.kind == "refuse"));
            // all six conditions detectable
            for (int k = 0; k < 6; k++) { var c = e.C(2 + k % 3); c.smooth[k] = 0.8f; c.aboveSince[k] = SimClock.hours - 100; }
            e.Step(0.05f);
            for (int k = 0; k < 6; k++) Check("U3.1 condition " + CondSpec.Name[k], e.crew.Any(c => c.tier[k] > 0) || e.log.Any(l => l.cond == k && l.kind.StartsWith("tier")));
            Check("U3.3 Tier 3 escalation + crew informed first", e.log.Any(l => l.kind == "inform") && e.log.Any(l => l.kind == "tier3"));
            // ethics guard
            int b0 = EthicsGuard.blocked; EthicsGuard.CanViewDetail(e, 1, 4); EthicsGuard.LifeSafety("selftest O2 set-point"); var pc = e.C(3); pc.aiPodAccess = false; EthicsGuard.CanActInPod(pc, "selftest pod light");
            Check("U4.1 hard limits enforced", EthicsGuard.blocked >= b0 + 3);
            // scenarios
            for (int q = 1; q <= 6; q++) { try { Scenarios.Run(q); Check("U5.2 board scenario Q" + q, !string.IsNullOrEmpty(Scenarios.lastExplain)); } catch (Exception ex) { Check("U5.2 board scenario Q" + q, false, ex.Message); } yield return null; }
            Panels.Open("scenarios"); yield return new WaitForSeconds(0.5f); yield return Shot("08_scenario_Q6"); Panels.CloseAll();
            // tabletop
            string rep = null; try { rep = TabletopSim.Run(); } catch (Exception ex) { errors.Add("Tabletop: " + ex); }
            Check("U6 tabletop report written", rep != null && System.IO.File.Exists(rep), rep);
            Panels.Open("tabletop"); yield return new WaitForSeconds(0.5f); yield return Shot("09_tabletop"); Panels.CloseAll();
            // config B
            HabitatEnvironment.I.SetConfigB(true); yield return new WaitForSeconds(0.3f); Check("U2.3 config B deployables", GameObject.Find("CB02_Wardroom_AcousticScreen_North") != null && GameObject.Find("CB02_Wardroom_AcousticScreen_North").activeInHierarchy);
            pr.Teleport(Nav.wp["WP_Up_W"].position, 90); yield return new WaitForSeconds(0.4f); yield return Shot("10_configB_wardroom"); HabitatEnvironment.I.SetConfigB(false);
            // pod mood lights
            pr.Teleport(Nav.wp.ContainsKey("WP_SQ2_Bed") ? Nav.wp["WP_SQ2_Bed"].position : Nav.wp["WP_Pod2_Bed"].position, 0); e.C(2).mood = 0.2f; e.C(2).tier[1] = 1; yield return new WaitForSeconds(1.0f); yield return Shot("11_pod_moodlight");
            Check("U8.1 pod mood lights", GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.name.Contains("MoodLight")) >= 16);
            // EVA procedure
            pr.Teleport(Nav.wp["WP_A_Suit"].position, 90); yield return null;
            pr.BeginDon(1); yield return new WaitForSeconds(2.5f);
            Check("U8.3 EVA via suitport", pr.suit == SuitState.EVA, pr.FeetPosition.ToString());
            yield return Shot("12_eva_outside");
            pr.BeginDoff(); yield return new WaitForSeconds(2f); Check("U8.3 return via suitport", pr.suit == SuitState.IVA);
            // autopilot
            pr.Teleport(Nav.wp["WP_Up_E"].position, 0); SimClock.hours = Mathf.Floor(SimClock.hours / 24) * 24 + 13.5f; pr.autopilot = true; Vector3 a0 = pr.FeetPosition;
            yield return new WaitForSeconds(5f); Check("autopilot moves the player", (pr.FeetPosition - a0).magnitude > 1f, $"moved {(pr.FeetPosition - a0).magnitude:0.0} m");
            yield return Shot("13_autopilot"); pr.autopilot = false;
            // interactables
            var hab = GameObject.FindObjectsByType<HabInteractable>(FindObjectsSortMode.None);
            Check("interactables wired", hab.Length > 380, hab.Length + " objects");
            int fails = 0; foreach (var h in hab.Where(x => x.kind == "sit" || x.kind == "tend_plant" || x.kind == "lavatory_use" || x.kind == "cook_eat").Take(30)) { try { h.Activate(); } catch { fails++; } }
            Check("interactions run without exceptions", fails == 0);
            // AI crew moved
            SimClock.hours += 3; yield return new WaitForSeconds(3f);
            Check("crew follow schedules", CrewAgent.All.All(a => !string.IsNullOrEmpty(a.task)), string.Join(", ", CrewAgent.All.Select(a => a.crewId + ":" + a.task)));
            yield return Shot("14_final");
            Check("U9.1 no runtime exceptions", errors.Count == 0, errors.Count + " errors");
            var sb = new StringBuilder(); sb.AppendLine("# Self-test " + DateTime.Now); foreach (var c in checks) sb.AppendLine(c); sb.AppendLine("\n## Errors"); foreach (var x in errors.Distinct().Take(80)) sb.AppendLine(x);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "selftest_results.txt"), sb.ToString());
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }
    }
}
