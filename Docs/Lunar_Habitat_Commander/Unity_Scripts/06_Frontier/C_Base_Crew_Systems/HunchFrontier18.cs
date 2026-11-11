// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/C_Base_Crew_Systems/HunchFrontier18.cs
//  Section : 06_Frontier/C_Base_Crew_Systems
//  Folder  : Systems inside the central base: pick-ups + battery bank, NASA HUNCH mode, physics
//            view, Autopilot Smooth, crew of four + sleep pods, The Show, self-working crew,
//            airlocks/lifts, crew self-care.
//  Purpose : Eighteenth part of Moon Frontier: THE SHOW. The mission as a cartoon you watch: the
//            Commander strolls through the real 30-day mission (it is Autopilot Smooth
//  Contains: HunchFrontier
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using UnityEngine;

/// Eighteenth part of Moon Frontier: THE SHOW.
/// The mission as a cartoon you watch: the Commander strolls through the real 30-day mission (it is Autopilot Smooth
/// underneath, at a walking pace), with episode title cards, speech bubbles, comic bursts when a job is done, changing
/// camera angles, a "LIVE from the Moon" strip and a spoken line for every scene. Nobody gets hurt and nothing is scripted
/// in advance: the story is whatever happens in the mission.
/// Start: main menu > The Show, or Menu > The Show. SPACE (A in the headset) stops it and hands the controls back.
public partial class HunchFrontier
{
    [NonSerialized] public bool showOn;
    int showEp = -1, showCam, showJobs; float showCardUntil, showBurstAt = -9f, showBubbleUntil, showCamAt, showEndUntil, showStartAt; bool showHudWas, showEnded;
    string showCardHead = "", showCardTitle = "", showCardSub = "", showBurst = "", showBubble = "", showEndLine = ""; Color showBurstCol = Color.yellow, showCardCol = new Color(0.1f, 0.2f, 0.5f);
    GUIStyle shBig, shMid, shSmall, shBubble, shBurst; HunchVoice showVoice;
    static readonly string[] ShowEpTitle = { "Moving In", "Life on the Moon", "Things Go Wrong (and Get Fixed)", "The Long Night", "Sunrise", "Going for the Record" };
    static readonly string[] ShowEpSub = { "Eight rooms to switch on, in the right order.", "Four people, one small home, thirty days.", "Dust, leaks and grumpy moods. The crew handles it.", "Nine days with no Sun. The reactor keeps the lights on.", "The Sun is back. So are the smiles.", "The last days. Can the score go any higher?" };
    static readonly Color[] ShowEpCol = { new Color(0.12f, 0.28f, 0.62f), new Color(0.1f, 0.5f, 0.42f), new Color(0.62f, 0.3f, 0.1f), new Color(0.2f, 0.14f, 0.42f), new Color(0.72f, 0.5f, 0.08f), new Color(0.55f, 0.12f, 0.3f) };
    static readonly string[] ShowWow = { "NICE!", "DONE!", "TEAMWORK!", "ZOOM!", "FIXED!", "YES!", "SMOOTH!", "WOW!" };
    static readonly Color[] ShowWowCol = { new Color(1f, 0.85f, 0.1f), new Color(0.3f, 0.9f, 1f), new Color(1f, 0.5f, 0.75f), new Color(0.55f, 1f, 0.4f) };

    public void StartShow()
    {
        if (!ready) return;
        if (sim.phase == HabPhase.Mission && Creative) { Note("The Show plays the real mission. Choose The Show on the main menu (Menu > Restart)."); sim.Say("ARIA: The Show plays the real thirty-day mission. Choose it on the main menu after Restart."); return; }
        StartAutopilotSmooth(); if (!autoSmooth) return;
        showOn = true; showEp = -1; showJobs = 0; showEnded = false; showEndUntil = 0f; showStartAt = Time.unscaledTime; showCamAt = Time.time + 6f; showCam = 0;
        showHudWas = game.hideHud; if (!game.HeadsetActive) game.hideHud = true;
        showVoice = GetComponent<HunchVoice>();
        ShowCard("THE SHOW", "Lunar Habitat Commander", "starring you as the Commander, with Dr. Okafor, Eng. Tanaka, Sci. Rivera and ARIA", new Color(0.55f, 0.1f, 0.14f), 4.5f);
        ShowSpeak("Welcome to The Show. Thirty days on the Moon, starting now.");
    }
    /// called when the autopilot stops for any reason
    void ShowStop() { if (!showOn) return; showOn = false; game.hideHud = showHudWas; }

    void ShowSpeak(string line) { if (showVoice && showVoice.speakOn) showVoice.Say(line, 0); }
    void ShowCard(string head, string title, string sub, Color col, float seconds)
    {
        showCardHead = head; showCardTitle = title; showCardSub = sub; showCardCol = col; showCardUntil = Time.unscaledTime + seconds; Play(cWin, 0.6f);
        if (game.HeadsetActive) Banner(head, title, false);
    }
    /// the Commander says something: a speech bubble on a screen, a note in the headset
    void ShowSay(string line)
    {
        if (!showOn) return; showBubble = line; showBubbleUntil = Time.unscaledTime + 4.2f; sim.Say("Commander: " + line);
        if (game.HeadsetActive) Note("Commander: " + line);
    }
    void ShowPop()
    {
        if (!showOn) return; showJobs++; showBurst = ShowWow[(showJobs * 5 + sim.Day) % ShowWow.Length]; showBurstCol = ShowWowCol[showJobs % ShowWowCol.Length]; showBurstAt = Time.unscaledTime; Play(cPick, 0.8f);
        showCam = (showCam + 1 + showJobs % 2) % 5; showCamAt = Time.time + 7f;       // a new camera angle after every scene
    }
    /// what the Commander says on the way to a job
    void ShowLine(string zone, HabAlert alert)
    {
        if (!showOn) return; string who = alert != null && alert.crew >= 0 && alert.crew < sim.crew.Count ? sim.crew[alert.crew].name : "";
        string line;
        if (alert != null) switch (alert.type)
        {
            case "psych": line = alert.crew == 0 ? "I need a break myself. Even commanders do." : "Hey " + who + ", let's take a break together."; break;
            case "medical": line = alert.crew == 0 ? "Doctor's orders: I am getting checked." : "Hold on " + who + ", the doctor is coming!"; break;
            case "eva": line = alert.step == 0 ? "Boots off! Moon dust stays in the airlock." : "Scrub time. No dust in my habitat."; break;
            case "storm": line = "Solar storm! Everybody into the shelter, quick march!"; break;
            case "night": line = "No Sun for nine days. Reactor, do your thing."; break;
            case "leak": line = "I hear a hiss. Sealing that room right now."; break;
            case "sleep": line = "The lights think it is noon. Let's fix bedtime."; break;
            case "arrays": line = "Dusty solar panels. Send in the cleaning robot!"; break;
            case "comms": line = "Earth, can you hear me? Turning the dish."; break;
            case "scrubber": line = "The air machine is coughing. Spare part coming up."; break;
            case "water": line = "Water recycler is down. I've got the part."; break;
            default: line = "On my way!"; break;
        }
        else switch (zone)
        {
            case "airlock": line = "Dust check at the airlock."; break;
            case "hygiene": line = "Clean crew, happy crew."; break;
            case "quarters": line = "Soft lights for the sleep pods."; break;
            case "galley": line = "Dinner together. Who is cooking?"; break;
            case "work": line = "ARIA, how is everyone feeling today?"; break;
            case "storage": line = "One spare part for my pocket."; break;
            case "eclss": line = "Fresh air and clean water, coming up."; break;
            case "medical": line = "Quick health check. Say aah."; break;
            case "sanctuary": line = "Headset on. Deep breath. Forest time."; break;
            case "exercise": line = "One, two, one, two! Moon muscles!"; break;
            case "greenhouse": line = "Hello, little tomatoes."; break;
            case "power": line = "Starting the reactor before the night comes."; break;
            case "fab": line = "Printing a spare part. Beep, boop."; break;
            case "control": line = "Time for a call home."; break;
            case "shelter": line = "Shelter drill! This is only a drill."; break;
            case "observation": line = "Come and look. That blue dot is home."; break;
            default: line = "Off we go."; break;
        }
        ShowSay(line);
    }

    void UpdateShow()
    {
        if (!showOn) return;
        if (!autoOn || !autoSmooth) { ShowStop(); return; }
        int ep = sim.phase == HabPhase.Commissioning ? 0 : (sim.Day < 5 ? 1 : (sim.Day < 10 ? 2 : (sim.Day < 19 ? 3 : (sim.Day < 26 ? 4 : 5))));
        if (ep != showEp && Time.unscaledTime > showCardUntil - 0.5f && Time.unscaledTime - showStartAt > 4f)
        {
            showEp = ep; ShowCard("EPISODE " + (ep + 1), ShowEpTitle[ep], ShowEpSub[ep], ShowEpCol[ep], 3.6f); ShowSpeak("Episode " + (ep + 1) + ". " + ShowEpTitle[ep] + ".");
        }
        if (Time.time > showCamAt) { showCam = (showCam + 1) % 5; showCamAt = Time.time + 7f; }
    }
    /// the mission is over: the closing card
    void ShowFinish(float score, string grade)
    {
        if (!showOn) return; showEnded = true; showEndUntil = Time.unscaledTime + 14f; showEndLine = "Final score " + score.ToString("F0") + " of 100   -   " + grade;
        ShowSpeak("The end. Final score " + score.ToString("F0") + " out of one hundred. Thanks for watching The Show.");
    }

    /// the show's own cameras (a screen only): behind, side, front, high and low
    int showCamShown = -1;
    static Vector3 ShowOffset(int mode, Vector3 f, Vector3 r)
    {
        switch (mode)
        {
            case 1: return r * 3.4f + f * 0.6f + Vector3.up * 0.4f;                 // walking beside
            case 2: return f * 3.6f + r * 0.8f + Vector3.up * 0.3f;                 // in front, looking back at the face
            case 3: return -f * 5.5f + Vector3.up * 3.6f;                           // high and wide
            case 4: return -f * 2.4f - r * 1.2f - Vector3.up * 0.7f;                // low hero shot
            default: return -f * 3.6f + Vector3.up * 0.7f;                          // behind
        }
    }
    bool ShowCamera(Vector3 head)
    {
        if (!showOn || !tpCam) return false;
        Vector3 f = FlatForward(), r = Vector3.Cross(Vector3.up, f), off = Vector3.zero; float dist = 1f; int mode = showCam; RaycastHit h;
        // take the wanted angle if there is room for it; otherwise the next one that has room, so the camera is never inside a wall
        for (int attempt = 0; attempt < 5; attempt++)
        {
            mode = (showCam + attempt) % 5; off = ShowOffset(mode, f, r); float want = off.magnitude, free = want;
            if (Physics.SphereCast(head, 0.3f, off / want, out h, want, ~0, QueryTriggerInteraction.Ignore)) free = h.distance - 0.05f;
            dist = Mathf.Max(0.5f, free); if (free >= Mathf.Min(want, 2.2f) * 0.85f) break;
            if (attempt == 4) { mode = 0; off = ShowOffset(0, f, r); want = off.magnitude; free = Physics.SphereCast(head, 0.3f, off / want, out h, want, ~0, QueryTriggerInteraction.Ignore) ? h.distance - 0.05f : want; dist = Mathf.Max(0.4f, free); }
        }
        Vector3 p = head + off.normalized * dist, look = head + (mode == 2 ? Vector3.zero : f * 1.2f) - p;
        Quaternion wantRot = Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : f, Vector3.up);
        bool cut = mode != showCamShown || (tpCam.transform.position - p).sqrMagnitude > 9f; showCamShown = mode;
        float k = cut ? 1f : 1f - Mathf.Exp(-10f * Time.deltaTime);
        tpCam.transform.SetPositionAndRotation(Vector3.Lerp(tpCam.transform.position, p, k), Quaternion.Slerp(tpCam.transform.rotation, wantRot, k));
        return true;
    }

    // ------------------------------------------------------------------ drawing (a screen only)
    void ShowGui()
    {
        bool ending = showEnded && Time.unscaledTime < showEndUntil;
        if (!showOn && !ending) return;
        float u = Screen.height / 1080f, W = Screen.width, H = Screen.height, now = Time.unscaledTime;
        if (shBig == null || Mathf.Abs(shBig.fontSize - HunchLook.FsF(86f * u)) > 1f)
        {
            shBig = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(86f * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true }; shBig.normal.textColor = Color.white;
            shMid = new GUIStyle(shBig) { fontSize = HunchLook.Fs(38f * u) }; shSmall = new GUIStyle(shBig) { fontSize = HunchLook.Fs(24f * u), fontStyle = FontStyle.Normal };
            shBubble = new GUIStyle(shBig) { fontSize = HunchLook.Fs(27f * u) }; shBubble.normal.textColor = new Color(0.08f, 0.08f, 0.12f);
            shBurst = new GUIStyle(shBig) { fontSize = HunchLook.Fs(64f * u) }; shBurst.normal.textColor = new Color(0.1f, 0.05f, 0.15f);
        }
        if (ending)
        {
            Fill(new Rect(0f, 0f, W, H), new Color(0.05f, 0.05f, 0.1f, 0.9f));
            GUI.Label(new Rect(0f, H * 0.2f, W, 130f * u), "THE END", shBig);
            GUI.Label(new Rect(W * 0.1f, H * 0.42f, W * 0.8f, 60f * u), "<color=#FFD866>" + showEndLine + "</color>", shMid);
            GUI.Label(new Rect(W * 0.1f, H * 0.55f, W * 0.8f, 90f * u), "Thirty days on the Moon. Nobody was hurt, and everybody is still friends.\nThanks for watching THE SHOW.", shSmall);
            return;
        }
        // cinema bars and the LIVE strip
        float bar = 64f * u; Fill(new Rect(0f, 0f, W, bar), new Color(0f, 0f, 0f, 0.92f)); Fill(new Rect(0f, H - bar, W, bar), new Color(0f, 0f, 0f, 0.92f));
        bool blink = Mathf.Repeat(now, 1.2f) < 0.8f;
        Fill(new Rect(22f * u, 16f * u, 108f * u, 32f * u), new Color(0.85f, 0.1f, 0.12f)); GUI.Label(new Rect(22f * u, 16f * u, 108f * u, 32f * u), (blink ? "<color=#FFFFFF>●</color> " : "  ") + "LIVE", shSmall);
        GUI.Label(new Rect(140f * u, 14f * u, W - 160f * u, 36f * u), "<color=#FFD866><b>THE SHOW</b></color>   from the Moon   -   " + (sim.phase == HabPhase.Commissioning ? "moving in" : "day " + sim.Day + " of " + HabitatSim.MissionDays + "   score " + sim.Overall().ToString("F0")) + "   -   press SPACE to stop", new GUIStyle(shSmall) { alignment = TextAnchor.MiddleLeft });
        // the lower third: who is on screen and what they are doing
        float lw = Mathf.Min(900f * u, W * 0.7f); var lt = new Rect(40f * u, H - bar - 96f * u, lw, 78f * u);
        Fill(new Rect(lt.x, lt.y, 10f * u, lt.height), new Color(1f, 0.82f, 0.15f)); Fill(new Rect(lt.x + 10f * u, lt.y, lt.width, lt.height), new Color(0.05f, 0.08f, 0.18f, 0.88f));
        GUI.Label(new Rect(lt.x + 26f * u, lt.y + 4f * u, lt.width - 20f * u, 34f * u), "<b>THE COMMANDER</b>   <color=#9AD0FF>that's you</color>", new GUIStyle(shSmall) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold });
        GUI.Label(new Rect(lt.x + 26f * u, lt.y + 38f * u, lt.width - 20f * u, 34f * u), "<color=#FFE08A>" + autoLine + "</color>", new GUIStyle(shSmall) { alignment = TextAnchor.MiddleLeft, fontSize = HunchLook.Fs(20f * u), wordWrap = false, clipping = TextClipping.Clip });
        // what ARIA last said, in the bottom bar
        if (sim.messageAge < 7f && sim.message != null && sim.message.StartsWith("ARIA")) GUI.Label(new Rect(20f * u, H - bar + 12f * u, W - 40f * u, 40f * u), "<color=#CFE3FF>" + sim.message + "</color>", new GUIStyle(shSmall) { fontSize = HunchLook.Fs(20f * u), wordWrap = false, clipping = TextClipping.Clip });
        // speech bubble above the Commander's head
        if (now < showBubbleUntil && showBubble.Length > 0)
        {
            Vector2 at = new Vector2(W * 0.5f, H * 0.3f);
            if (tpCam && tpCam.enabled) { Vector3 sp = tpCam.WorldToScreenPoint(cam.position + Vector3.up * 0.75f); if (sp.z > 0.2f) at = new Vector2(Mathf.Clamp(sp.x, W * 0.22f, W * 0.78f), Mathf.Clamp(H - sp.y, bar + 150f * u, H * 0.62f)); }
            float bw = Mathf.Min(560f * u, 60f * u + showBubble.Length * 15.5f * u), bh = (showBubble.Length > 34 ? 104f : 70f) * u; var br = new Rect(at.x - bw * 0.5f, at.y - bh - 46f * u, bw, bh);
            float pop = Mathf.Clamp01((now - (showBubbleUntil - 4.2f)) * 6f);
            var m0 = GUI.matrix; GUIUtility.ScaleAroundPivot(Vector2.one * (0.6f + 0.4f * pop), new Vector2(at.x, at.y - 30f * u));
            Fill(new Rect(br.x - 5f * u, br.y - 5f * u, br.width + 10f * u, br.height + 10f * u), new Color(0.08f, 0.08f, 0.12f)); Fill(br, Color.white);
            GUIUtility.RotateAroundPivot(45f, new Vector2(at.x, br.yMax)); Fill(new Rect(at.x - 16f * u, br.yMax - 16f * u, 32f * u, 32f * u), Color.white); GUI.matrix = m0;
            GUIUtility.ScaleAroundPivot(Vector2.one * (0.6f + 0.4f * pop), new Vector2(at.x, at.y - 30f * u)); GUI.Label(new Rect(br.x + 12f * u, br.y, br.width - 24f * u, br.height), showBubble, shBubble); GUI.matrix = m0;
        }
        // comic burst when a job is done
        float bt = now - showBurstAt;
        if (bt >= 0f && bt < 1.3f)
        {
            Vector2 c = new Vector2(W * 0.76f, H * 0.34f); float s = (1f + 0.5f * Mathf.Exp(-bt * 9f)) * (bt > 1.0f ? 1f - (bt - 1.0f) / 0.3f : 1f) * 150f * u; var m0 = GUI.matrix;
            for (int layer = 0; layer < 2; layer++) for (int i = 0; i < 6; i++)
            {
                GUIUtility.RotateAroundPivot(i * 15f + bt * 25f, c); float q = layer == 0 ? s * 1.1f : s;
                Fill(new Rect(c.x - q, c.y - q, q * 2f, q * 2f), layer == 0 ? new Color(0.1f, 0.05f, 0.15f) : showBurstCol); GUI.matrix = m0;
            }
            GUIUtility.RotateAroundPivot(-9f, c); GUI.Label(new Rect(c.x - s * 1.4f, c.y - s * 0.6f, s * 2.8f, s * 1.2f), showBurst, new GUIStyle(shBurst) { fontSize = HunchLook.Fs(Mathf.Max(8f, s * 0.42f)) }); GUI.matrix = m0;
        }
        // title cards
        if (now < showCardUntil)
        {
            float left = showCardUntil - now, a = Mathf.Clamp01(left * 2.5f); Color col = showCardCol; col.a = 0.96f * a;
            Fill(new Rect(0f, 0f, W, H), col);
            for (int i = 0; i < 9; i++) { var m0 = GUI.matrix; GUIUtility.RotateAroundPivot(i * 20f + now * 12f, new Vector2(W * 0.5f, H * 0.5f)); Fill(new Rect(W * 0.5f - 14f * u, -H, 28f * u, H * 3f), new Color(1f, 1f, 1f, 0.05f * a)); GUI.matrix = m0; }      // turning rays behind the title
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(0f, H * 0.2f, W, 70f * u), "<color=#FFD866>" + showCardHead + "</color>", shMid);
            GUI.Label(new Rect(W * 0.06f, H * 0.3f, W * 0.88f, 230f * u), showCardTitle, shBig);
            GUI.Label(new Rect(W * 0.12f, H * 0.6f, W * 0.76f, 100f * u), showCardSub, shSmall);
            GUI.color = Color.white;
        }
    }

    public string ProbeShow() { StartShow(); return "show=" + showOn + " smooth=" + autoSmooth + " phase=" + sim.phase; }
    public string ProbeShowInfo() { return "show=" + showOn + " ep=" + showEp + " cam=" + showCam + " jobs=" + showJobs + " bubble=" + showBubble + " | " + ProbeSmoothInfo(); }
}
