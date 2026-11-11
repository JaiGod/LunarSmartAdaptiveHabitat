// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/01_Core/HunchSelfTest.cs
//  Section : 01_Core
//  Folder  : Game rules and the habitat simulation (crew, systems, alerts), start-up, self-test,
//            cheats and the live link to the Crew Health web page.
//  Purpose : Developer check and demo: run the app with -hunchSelfTest <folder> [-hunchIdle]
//            [-hunchSpeed 8] A bot plays the whole mission, saves screenshots and a score report,
//            then quits.
//  Contains: HunchSelfTest
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
#pragma warning disable 0414, 0219
using System;
using System.Collections;
using System.IO;
using UnityEngine;

/// Developer check and demo: run the app with  -hunchSelfTest <folder> [-hunchIdle] [-hunchSpeed 8]
/// A bot plays the whole mission, saves screenshots and a score report, then quits.
public class HunchSelfTest : MonoBehaviour
{
    HabitatGame game; string dir; int shot;

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        int k = Array.IndexOf(args, "-hunchSelfTest");
        if (k < 0 || k + 1 >= args.Length) yield break;
        dir = args[k + 1]; Directory.CreateDirectory(dir);
        bool idle = Array.IndexOf(args, "-hunchIdle") >= 0;
        float speed = 8f; int si = Array.IndexOf(args, "-hunchSpeed"); if (si >= 0 && si + 1 < args.Length) float.TryParse(args[si + 1], out speed);
        game = GetComponent<HabitatGame>(); var sim = game.sim;
        if (Application.isBatchMode) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60; }   // behave like a real 60 fps session
        yield return new WaitForSecondsRealtime(2.5f);
        yield return Shot("briefing");
        if (Array.IndexOf(args, "-hunchNavTest") >= 0) { yield return NavTest(); yield break; }
        if (Array.IndexOf(args, "-hunchShotOnly") >= 0)
        {
            game.DoInteract(); yield return new WaitForSecondsRealtime(0.8f); yield return Shot("airlock");
            foreach (var zid in new[] { "medical", "quarters", "greenhouse", "shelter" }) { game.GoTo(game.ZoneIndex(zid)); yield return new WaitForSecondsRealtime(0.8f); yield return Shot(zid); }
            Application.Quit(); yield break;
        }
        game.DoInteract();
        yield return new WaitForSecondsRealtime(0.6f);
        for (int guard = 0; guard < 40 && sim.phase == HabPhase.Commissioning; guard++)
        {
            game.GoToUrgent(); yield return new WaitForSecondsRealtime(0.5f);
            if (sim.tutorialIndex == 3) yield return Shot("commissioning");
            game.DoInteract(); yield return new WaitForSecondsRealtime(0.5f);
        }
        Time.timeScale = speed;
        bool s1 = false, s2 = false, s3 = false, s4 = false; float lastAct = 0f; int r = 0;
        string[] routine = { "galley", "exercise", "greenhouse", "quarters", "sanctuary", "control", "observation", "eclss", "airlock", "fab", "medical", "hygiene", "work" };
        while (sim.phase == HabPhase.Mission)
        {
            if (!idle && sim.missionSeconds - lastAct > 6f)          // the bot needs about four game-hours to reach a zone and act
            {
                lastAct = sim.missionSeconds;
                if (sim.alerts.Count > 0) { game.GoToUrgent(); yield return null; game.DoInteract(); }
                else
                {
                    // a sensible player: fix what is low first, otherwise look after the crew
                    float st = 0f, md = 0f, hl = 100f; foreach (var c in sim.crew) { st += c.stress; md += c.mood; hl = Mathf.Min(hl, c.health); } st /= sim.crew.Count; md /= sim.crew.Count;
                    var order = new System.Collections.Generic.List<string>();
                    if (sim.oxygen < 88f || sim.water < 85f) order.Add("eclss");
                    if (sim.dust > 18f) order.Add("airlock");
                    if (sim.parts < 2) order.Add("fab");
                    if (hl < 85f) order.Add("medical");
                    if (sim.food < 45f) order.Add("greenhouse");
                    if (st > 28f || md < 72f) order.AddRange(new[] { "sanctuary", "galley", "exercise", "observation", "greenhouse", "control", "quarters", "work" });
                    order.AddRange(routine);
                    foreach (var z in order)
                    {
                        float cd; sim.cooldown.TryGetValue(z, out cd);
                        if (cd > 0f || (z == "fab" && sim.parts >= 2) || (z == "control" && !sim.commsOk)) continue;
                        game.GoTo(game.ZoneIndex(z)); yield return null; game.DoInteract(); break;
                    }
                }
            }
            if (!s1 && sim.alerts.Count > 0 && sim.Day >= 2) { s1 = true; Time.timeScale = 0f; yield return new WaitForSecondsRealtime(0.4f); yield return Shot("alert"); Time.timeScale = speed; }
            if (!s2 && sim.Day >= 8 && sim.IsNightHours) { s2 = true; Time.timeScale = 0f; game.GoTo(game.ZoneIndex("quarters")); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("night_quarters"); Time.timeScale = speed; }
            if (!s3 && sim.Day >= 13 && !sim.IsNightHours) { s3 = true; Time.timeScale = 0f; game.GoTo(game.ZoneIndex("medical")); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("lunar_night_medical"); Time.timeScale = speed; }
            if (!s4 && sim.Day >= 20 && !sim.IsNightHours) { s4 = true; Time.timeScale = 0f; game.GoTo(game.ZoneIndex("greenhouse")); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("greenhouse"); Time.timeScale = speed; }
            yield return null;
        }
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(1.0f);
        yield return Shot("report");
        var sc = sim.Scores(); string txt = (idle ? "IDLE" : "BOT") + " overall=" + sim.Overall().ToString("F1") + " grade=" + HabitatSim.Grade(sim.Overall()) + " day=" + sim.Day + " failed=" + sim.missionFailed + " " + sim.failReason + "\n";
        for (int i = 0; i < sc.Length; i++) txt += sc[i].ToString("F0") + "  " + HabitatSim.Criteria[i] + "\n";
        txt += "handled " + sim.Handled + " of " + sim.Raised + "  dose " + sim.dose.ToString("F1") + "  O2 " + sim.oxygen.ToString("F0") + " water " + sim.water.ToString("F0") + " food " + sim.food.ToString("F0") + " battery " + sim.battery.ToString("F0") + " dust " + sim.dust.ToString("F0") + "\n";
        foreach (var c in sim.crew) txt += c.name + " wb " + c.Wellbeing.ToString("F0") + " health " + c.health.ToString("F0") + " stress " + c.stress.ToString("F0") + " mood " + c.mood.ToString("F0") + " sleep " + c.sleep.ToString("F0") + "\n";
        txt += "headset=" + game.HeadsetActive + " screen=" + Screen.width + "x" + Screen.height + "\n";
        File.WriteAllText(Path.Combine(dir, idle ? "selftest_idle.txt" : "selftest_bot.txt"), txt);
        Application.Quit();
    }

    // Walks in a straight line between places that are separated by doors, walls and floors, to prove nothing traps the player.
    IEnumerator NavTest()
    {
        var nav = GetComponent<HunchNavAssist>(); string txt = "";
        game.DoInteract(); yield return new WaitForSecondsRealtime(0.5f);
        string[][] legs = { new[] { "airlock", "medical" }, new[] { "airlock", "storage" }, new[] { "storage", "shelter" }, new[] { "quarters", "sanctuary" }, new[] { "shelter", "greenhouse" }, new[] { "greenhouse", "power" }, new[] { "power", "fab" } };
        foreach (var leg in legs)
        {
            int a = game.ZoneIndex(leg[0]), b = game.ZoneIndex(leg[1]);
            game.GoTo(a); yield return new WaitForSeconds(0.3f);
            Vector3 target = game.zones[b].position; float t0 = Time.time; int p0 = nav.passes; float dist = 999f;
            while (Time.time - t0 < 45f)
            {
                Vector3 d = target - nav.Feet(); d.y = 0f; dist = d.magnitude;
                if (dist < 1.2f) break;
                nav.debugIntent = d.normalized;
                yield return null;
            }
            nav.debugIntent = Vector3.zero;
            txt += leg[0] + " -> " + leg[1] + ": " + (dist < 1.2f ? "ARRIVED" : "STUCK at " + dist.ToString("F1") + " m") + " in " + (Time.time - t0).ToString("F1") + " s, auto-door used " + (nav.passes - p0) + " times, height " + nav.Feet().y.ToString("F2") + " (target " + target.y.ToString("F2") + ")\n";
            Debug.Log("[HUNCH] navtest " + leg[0] + " -> " + leg[1] + " dist " + dist.ToString("F1") + " passes " + (nav.passes - p0));
            yield return null;
        }
        game.GoTo(game.ZoneIndex("shelter")); yield return new WaitForSeconds(0.3f);
        txt += "lift from hub ground: " + nav.Feet().y.ToString("F2");
        for (int i = 0; i < 6; i++) { bool ok = nav.Lift(1); yield return new WaitForSeconds(0.2f); txt += (ok ? " -> " + nav.Feet().y.ToString("F2") : " (no higher floor)"); if (!ok) break; }
        txt += "\nlift down:";
        for (int i = 0; i < 8; i++) { bool ok = nav.Lift(-1); yield return new WaitForSeconds(0.2f); txt += (ok ? " -> " + nav.Feet().y.ToString("F2") : " (ground)"); if (!ok) break; }
        game.GoTo(game.ZoneIndex("airlock")); yield return new WaitForSeconds(0.3f);
        txt += "\nlift from crew deck 1: " + nav.Feet().y.ToString("F2");
        for (int i = 0; i < 3; i++) { bool ok = nav.Lift(1); yield return new WaitForSeconds(0.2f); txt += (ok ? " -> " + nav.Feet().y.ToString("F2") : " (no higher floor)"); if (!ok) break; }
        var cc = game.origin.GetComponent<CharacterController>();
        txt += "\nbody: height " + (cc ? cc.height.ToString("F2") : "?") + " radius " + (cc ? cc.radius.ToString("F2") : "?") + " step " + (cc ? cc.stepOffset.ToString("F2") : "?") + " headset=" + game.HeadsetActive + "\n";
        File.WriteAllText(Path.Combine(dir, "navtest.txt"), txt);
        Application.Quit();
    }

    IEnumerator Shot(string name)
    {
        if (Application.isBatchMode) yield break;          // headless run: no pictures
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, (++shot).ToString("00") + "_" + name + ".png"));
        yield return new WaitForSecondsRealtime(0.4f);
    }
}
