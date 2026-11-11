// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/A_Adventure_Core/HunchFrontier4.cs
//  Section : 06_Frontier/A_Adventure_Core
//  Folder  : Moon Frontier adventure layer: mining, aliens, tools, easy mode, survival + repair
//            mini-games, Autopilot, the 3D models from the Drive collection.
//  Purpose : Fourth part of Moon Frontier: Autopilot. The third way to play (after Survival and
//            Creative): the game plays itself in Creative mode so you can watch.
//  Contains: HunchFrontier
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// Fourth part of Moon Frontier: Autopilot.
/// The third way to play (after Survival and Creative): the game plays itself in Creative mode so you can watch.
/// It fixes the repair panels, checks on the zones, high-fives the crew, dances, visits the mining field,
/// zaps and befriends aliens, mines, builds, drives a rover, calls a helper and shows the sky.
/// Any key, a mouse click or a controller trigger hands the controls back to the player.
public partial class HunchFrontier
{
    [NonSerialized] public bool autoOn; bool autoWasA; Coroutine autoCo; InputAction autoTake; float autoSince; string autoLine = ""; int autoLap;

    public void StartAutopilot()
    {
        if (autoOn || !Playing) return;
        autoOn = true; game.autoBusy = true; autoSince = Time.unscaledTime; autoLine = "Starting..."; autoWasA = true;
        if (autoTake == null)
        {
            autoTake = new InputAction("takeOver", InputActionType.Button);
            foreach (var b in new[] { "<Gamepad>/buttonSouth", "<Gamepad>/start" }) autoTake.AddBinding(b);
        }
        autoTake.Enable();
        autoCo = StartCoroutine(AutoTasks());
    }

    string autoStopWhy = "";
    public void StopAutopilot(string why)
    {
        if (!autoOn) return;
        autoStopWhy = (why ?? "ended") + " at " + Time.time.ToString("F0");
        autoOn = false; game.autoBusy = false; if (autoCo != null) StopCoroutine(autoCo); autoCo = null; if (autoTake != null) autoTake.Disable();
        probeHold = 0f; if (nav) { nav.debugIntent = Vector3.zero; nav.debugSpeed = 3f; nav.noAutoPass = false; } autoSmooth = false; ShowStop();
        if (ride != null) Dismount(null);
        danceUntil = Mathf.Min(danceUntil, Time.time); danceAuto = false; if (thirdPerson) SetThird(false);
        tool = Hands; autoLine = ""; AutoStopExtra();
        if (why != null && Playing) Banner("YOUR TURN!", why, false);
    }

    void UpdateAuto()
    {
        if (!autoOn) return;
        if (!Playing) { if (autoSmooth) SmoothFinish(); StopAutopilot(null); return; }
        if (Time.unscaledTime - autoSince < 1.5f) return;
        // only one deliberate button hands the controls back: Space (or Esc for the menu) on a keyboard, A in the headset.
        // Clicks, mouse moves, other keys, triggers and grips are ignored, so resting a hand on the controller cannot stop it.
        var kb = Keyboard.current; bool vrA = game.HeadsetActive && HunchVr.A;
        bool take = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) || (vrA && !autoWasA) || (!game.HeadsetActive && autoTake != null && autoTake.WasPressedThisFrame());
        autoWasA = vrA;
        if (take) StopAutopilot("You have the controls");
    }

    void AutoSay(string text) { autoLine = text; sim.Say("ARIA autopilot: " + text); }

    void DrawAuto(float u, float W, float H)
    {
        float w = Mathf.Min(780f * u, W - 40f * u); var r = new Rect((W - w) * 0.5f, 104f * u, w, 62f * u);
        Fill(r, new Color(0.02f, 0.1f, 0.05f, 0.82f)); Fill(new Rect(r.x, r.y, r.width, 3f * u), new Color(0.45f, 1f, 0.55f, 0.9f));
        GUI.Label(new Rect(r.x, r.y + 3f * u, r.width, 28f * u), "<color=#7CFC9A><b>" + (autoSmooth ? "AUTOPILOT SMOOTH" : "AUTOPILOT") + "</b></color>   <color=#C8D4E8>" + (autoSmooth ? SmoothHudLine() : "the AI is playing") + "  -  press SPACE to take over</color>", sBar);
        GUI.Label(new Rect(r.x, r.y + 30f * u, r.width, 28f * u), "<color=#FFE08A>" + autoLine + "</color>", sBar);
    }

    // ---- small moves the autopilot is made of
    IEnumerator Turn(Vector3 target, float seconds)
    {
        Vector3 d = target - cam.position; if (d.sqrMagnitude < 0.01f) yield break;
        if (game.HeadsetActive)
        {
            // in the headset the view is never dragged round slowly: one clean turn so the thing is in front of you
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            if (flat.sqrMagnitude > 0.04f && Vector3.Angle(Vector3.ProjectOnPlane(cam.forward, Vector3.up), flat) > 50f) origin.MatchOriginUpCameraForward(Vector3.up, flat.normalized);
            yield break;
        }
        if (!desk) yield break;
        float y1 = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, p1 = -Mathf.Asin(Mathf.Clamp(d.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        float y0 = cam.eulerAngles.y, p0 = cam.eulerAngles.x; if (p0 > 180f) p0 -= 360f;
        for (float t = 0f; t < seconds; t += Time.deltaTime) { float k = Mathf.SmoothStep(0f, 1f, t / seconds); desk.SetLook(Mathf.LerpAngle(y0, y1, k), Mathf.Lerp(p0, p1, k)); yield return null; }
        desk.SetLook(y1, p1);
    }
    IEnumerator Pan(float degrees, float seconds)
    {
        if (!desk || game.HeadsetActive) { yield return new WaitForSeconds(seconds); yield break; }
        float y0 = cam.eulerAngles.y;
        for (float t = 0f; t < seconds; t += Time.deltaTime) { desk.SetLook(y0 + degrees * Mathf.SmoothStep(0f, 1f, t / seconds), 6f); yield return null; }
    }
    Alien NearestAlien()
    {
        Alien best = null; float bd = 60f * 60f;
        foreach (var a in aliens) if (a.alive && !a.friend) { float d = (a.t.position - cam.position).sqrMagnitude; if (d < bd) { bd = d; best = a; } }
        return best;
    }
    Vector3 FlatForward() { Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up); return f.sqrMagnitude < 0.01f ? Vector3.forward : f.normalized; }

    IEnumerator AutoStation(Station st)
    {
        int zi = game.ZoneIndex(st.zoneId); if (zi < 0) yield break;
        string[] how = { "Joining each wire to the socket of the same colour.", "Locking the vent when the marker is on the green - three times.", "Pressing every button on the health monitor.", "Flipping every light switch up.", "Watching the colours, then pressing them in the same order." };
        AutoSay(st.name + " panel. " + how[Mathf.Clamp(st.type, 0, how.Length - 1)]);
        if (autoSmooth) { yield return SmoothToZone(zi, true, null); if (!autoOn) yield break; } else { game.GoTo(zi); yield return new WaitForSeconds(0.6f); }
        yield return Turn(st.board.position, 0.6f);
        if (!st.broken) { st.broken = true; st.practice = true; ResetStation(st); ShowStation(st); }
        yield return new WaitForSeconds(1.0f);
        float guard = Time.time + 45f;
        while (st.broken && Time.time < guard)
        {
            int el = -1;
            switch (st.type)
            {
                case 0: if (st.sel < 0) { for (int i = 0; i < 4; i++) if (!st.on[i]) { el = i; break; } } else { for (int j = 0; j < 4; j++) if (st.order[j] == st.sel) el = 10 + j; } break;
                case 1: if (Mathf.Abs(st.markerX - st.greenX) <= 0.05f) el = 0; break;
                case 2: for (int i = 0; i < 9; i++) if (!st.on[i]) { el = i; break; } break;
                case 3: for (int i = 0; i < 5; i++) if (!st.on[i]) { el = i; break; } break;
                case 4: if (!st.showing) el = st.seq[st.step]; break;
            }
            if (el < 0) { yield return null; continue; }
            if (st.type != 1)
            {
                var part = st.els[st.type == 0 && el >= 10 ? 4 + (el - 10) : Mathf.Min(el, st.els.Count - 1)];
                yield return Turn(part.transform.position, 0.3f);
                yield return new WaitForSeconds(0.15f);
            }
            StationPress(st.index * 100 + el);
            yield return new WaitForSeconds(st.type == 1 ? 0.7f : 0.25f);
        }
        yield return new WaitForSeconds(2.4f);
    }

    IEnumerator AutoRun()
    {
        bool vr = game.HeadsetActive;
        yield return new WaitForSeconds(1.2f);
        AutoSay("Autopilot is on. Sit back and watch. Press " + (vr ? "A" : "Space") + " when you want the controls.");
        yield return Pan(70f, 3.5f);
        while (true)
        {
            // 1. the repair panels
            for (int i = 0; i < stations.Count; i++) yield return AutoStation(stations[(i + autoLap) % stations.Count]);

            // 2. jobs around the base
            for (int k = 0; k < 4; k++)
            {
                int zi = (autoLap * 4 + k) * 3 % game.zones.Count; var z = game.zones[zi];
                AutoSay("Checking on " + z.title + ".");
                game.GoTo(zi); yield return new WaitForSeconds(0.7f);
                yield return Pan(k % 2 == 0 ? 85f : -85f, 2.8f);
                game.DoInteract(); yield return new WaitForSeconds(1.6f);
            }

            // 3. the crew, and a dance
            if (kid) { AutoSay("High five for the crew!"); kid.HighFive(autoLap % Mathf.Max(1, sim.crew.Count)); yield return new WaitForSeconds(2.2f); }
            AutoSay("Dance break!"); Dance(); yield return new WaitForSeconds(8.6f);

            // 4. the mining field
            if (fieldOk)
            {
                AutoSay("Off to the mining field to meet the aliens.");
                GoField(); yield return new WaitForSeconds(1.0f);
                if (!vr && nav) { SetThird(true); nav.debugIntent = FlatForward(); yield return new WaitForSeconds(2.6f); nav.debugIntent = Vector3.zero; yield return new WaitForSeconds(0.6f); SetThird(false); }

                for (int n = 0; n < 2; n++)
                {
                    var a = NearestAlien(); if (a == null) break;
                    AutoSay("Zapping an alien home. It is not hurt - its UFO beams it up.");
                    if (vr) { HitAlien(a, 999f, false); yield return new WaitForSeconds(1.8f); continue; }
                    tool = Zapper; yield return Turn(a.t.position + Vector3.up * 0.5f, 0.5f);
                    float end = Time.time + 2.4f; probeHold = end;
                    while (Time.time < end && a.alive) { Face(a.t.position + Vector3.up * 0.5f); yield return null; }
                    probeHold = 0f; yield return new WaitForSeconds(1.0f);
                }
                tool = Hands;
                var pal = NearestAlien();
                if (pal != null && friends < 3)
                {
                    AutoSay("Saying hello to make a new alien friend.");
                    yield return Turn(pal.t.position + Vector3.up * 0.5f, 0.6f);
                    Befriend(pal); yield return new WaitForSeconds(2.6f);
                }
                if (!vr)
                {
                    AutoSay("Mining a glowing rock with the drill.");
                    ProbeNearNode(); yield return new WaitForSeconds(0.7f);
                    tool = Drill; probeHold = Time.time + 3.4f; yield return new WaitForSeconds(3.7f); probeHold = 0f;
                    if (nav) { nav.debugIntent = FlatForward(); yield return new WaitForSeconds(0.9f); nav.debugIntent = Vector3.zero; }

                    AutoSay("Building with blocks.");
                    tool = Builder; if (desk) desk.SetLook(cam.eulerAngles.y + 100f, 24f); yield return new WaitForSeconds(0.6f);
                    PlaceBlock(AimRay()); yield return new WaitForSeconds(0.6f);
                    for (int n = 0; n < 2 && blocks.Count > 0; n++) { yield return Turn(blocks[blocks.Count - 1].position + Vector3.up * 0.45f, 0.4f); PlaceBlock(AimRay()); yield return new WaitForSeconds(0.6f); }
                    tool = Hands;

                    if (rovers.Count > 0 && nav)
                    {
                        AutoSay("Taking a rover for a drive.");
                        Mount(rovers[autoLap % rovers.Count]); yield return new WaitForSeconds(0.5f); SetThird(true);
                        Vector3 d0 = fieldC - Feet(); d0.y = 0f; if (d0.sqrMagnitude < 1f) d0 = FlatForward(); d0.Normalize();
                        for (float t = 0f; t < 7f; t += Time.deltaTime)
                        {
                            Vector3 dir = Quaternion.Euler(0f, Mathf.Sin(t * 0.9f) * 55f, 0f) * d0; nav.debugIntent = dir;
                            if (desk) desk.SetLook(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 10f);
                            yield return null;
                        }
                        nav.debugIntent = Vector3.zero; yield return new WaitForSeconds(0.4f);
                        SetThird(false); Dismount(null); yield return new WaitForSeconds(1.0f);
                    }
                }
                AutoSay("Calling the food rover for a snack delivery.");
                helperReady[0] = 0f; CallHelper(0); yield return new WaitForSeconds(6.5f);

                // 5. the sky
                if (comSat)
                {
                    AutoSay("Look up! Orbital cannons shoot down meteorites before they reach the base.");
                    if (skyEvent == 0) StartSkyEvent(3);
                    yield return Turn(cam.position + cannonDir[0] * 2300f, 1.2f); yield return new WaitForSeconds(5.5f);
                    AutoSay("The relay satellite carries our messages to Earth and back.");
                    yield return Turn(comSat.position, 1.2f); yield return new WaitForSeconds(3f);
                    yield return Turn(earthPos, 1.4f); yield return new WaitForSeconds(3f);
                }
            }

            if (dyson.Count > 0 && sun && -sun.transform.forward.y > 0f)
            {
                AutoSay("The Dyson swarm: " + dysonCount + " collector panels circle the Sun and beam power to the Moon.");
                Transform top = null; foreach (var d in dyson) if (d.gameObject.activeSelf && (top == null || d.position.y > top.position.y)) top = d;
                yield return Turn(top ? top.position : cam.position - sun.transform.forward * 3200f, 1.4f); yield return new WaitForSeconds(3.5f);
                if (rectenna) { AutoSay("The gold dish by the mining field receives the power beam."); yield return Turn(rectenna.position + Vector3.up * 3.5f, 1.2f); yield return new WaitForSeconds(2.5f); }
            }

            if (fieldOk && driveProps.Count > 0)
            {
                AutoSay("The rocket, the outpost, the telescope and the ships overhead are 3D models from Jaineil's NASA HUNCH collection.");
                foreach (var prop in driveProps) { yield return Turn(prop.position + Vector3.up * (prop.name.StartsWith("Rocket") ? 8f : 2.5f), 1.2f); yield return new WaitForSeconds(2.2f); }
                if (flyers.Count > 0) { float until = Time.time + 4f; while (Time.time < until) { Face(flyers[0].t.position); yield return null; } }
            }

            // 6. back inside: the light switch
            int ci = game.ZoneIndex("control"); if (ci < 0) ci = 0;
            AutoSay("Back inside to test the habitat lights.");
            game.GoTo(ci); yield return new WaitForSeconds(1.0f);
            SetLights(false); yield return Pan(60f, 2.6f); SetLights(true); yield return new WaitForSeconds(1.6f);

            autoLap++;
            AutoSay("That was the whole tour. I will go round again - press " + (vr ? "A" : "Space") + " whenever you want to play.");
            yield return new WaitForSeconds(4f);
        }
    }

    // ------------------------------------------------------------------ Dyson swarm
    // The affordable kind of Dyson sphere: not a shell round the Sun, just a few collector panels in orbit round it.
    // They beam power to a receiver dish on the Moon, so the habitat gets some power even in the lunar night.
    [NonSerialized] public int dysonCount = 3; public const int DysonMax = 8;
    readonly System.Collections.Generic.List<Transform> dyson = new System.Collections.Generic.List<Transform>(), powerPulse = new System.Collections.Generic.List<Transform>();
    Transform rectenna, rectDish; TMPro.TextMeshPro rectLabel; LineRenderer powerBeam;

    void BuildDyson()
    {
        var dr = new GameObject("Dyson swarm").transform; dr.SetParent(root, false);
        var gold = Glow(new Color(1.5f, 1.05f, 0.3f)); var blue = Glow(new Color(0.2f, 0.5f, 1.4f)); var silver = Lit(new Color(0.8f, 0.82f, 0.86f)); var goldLit = Lit(new Color(0.95f, 0.74f, 0.22f));
        for (int i = 0; i < DysonMax; i++)
        {
            var p = new GameObject("Collector panel " + (i + 1)).transform; p.SetParent(dr, false); p.localScale = Vector3.one * 1.5f;
            Prim(PrimitiveType.Cube, p, Vector3.zero, new Vector3(90f, 90f, 3f), blue);
            Prim(PrimitiveType.Cube, p, new Vector3(0f, 47f, 0f), new Vector3(100f, 6f, 6f), gold); Prim(PrimitiveType.Cube, p, new Vector3(0f, -47f, 0f), new Vector3(100f, 6f, 6f), gold);
            Prim(PrimitiveType.Cube, p, new Vector3(47f, 0f, 0f), new Vector3(6f, 100f, 6f), gold); Prim(PrimitiveType.Cube, p, new Vector3(-47f, 0f, 0f), new Vector3(6f, 100f, 6f), gold);
            Prim(PrimitiveType.Cube, p, new Vector3(0f, 0f, -1f), new Vector3(90f, 3f, 4f), gold); Prim(PrimitiveType.Cube, p, new Vector3(0f, 0f, -1f), new Vector3(3f, 90f, 4f), gold);
            foreach (var r in p.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            p.gameObject.SetActive(false); dyson.Add(p);
        }
        A("Star power", "Launch another Dyson swarm panel (Upgrades).", () => dysonCount > 3);

        // the receiver dish, on open ground beside the mining field
        Vector3 at = Vector3.zero, g; bool ok = false;
        if (fieldOk)
            foreach (float dist in new[] { 30f, 38f, 22f })
            {
                for (int k = 0; k < 8 && !ok; k++)
                {
                    float a = (k * 45f + 20f) * Mathf.Deg2Rad, x = fieldC.x + Mathf.Sin(a) * dist, z = fieldC.z + Mathf.Cos(a) * dist;
                    if (OpenAt(x, z, 9f, out g) && OpenAt(x, z, 4.5f, out g) && !Physics.CheckSphere(g + Vector3.up * 5f, 4.5f, ~0, QueryTriggerInteraction.Ignore)) { at = g; ok = true; }
                }
                if (ok) break;
            }
        if (!ok) return;
        rectenna = new GameObject("Dyson power receiver").transform; rectenna.SetParent(root, false); rectenna.position = at;
        Prim(PrimitiveType.Cylinder, rectenna, new Vector3(0f, 0.25f, 0f), new Vector3(4.4f, 0.25f, 4.4f), silver);
        Prim(PrimitiveType.Cylinder, rectenna, new Vector3(0f, 1.9f, 0f), new Vector3(0.5f, 1.6f, 0.5f), silver);
        for (int i = 0; i < 4; i++) Prim(PrimitiveType.Cube, rectenna, Quaternion.Euler(0f, 45f + 90f * i, 0f) * new Vector3(0f, 0.7f, 2.6f), new Vector3(1.6f, 0.08f, 1.6f), blue).transform.localRotation = Quaternion.Euler(-28f, 45f + 90f * i, 0f);
        rectDish = new GameObject("Dish").transform; rectDish.SetParent(rectenna, false); rectDish.localPosition = new Vector3(0f, 3.7f, 0f);
        Prim(PrimitiveType.Sphere, rectDish, Vector3.zero, new Vector3(4.6f, 0.9f, 4.6f), goldLit);
        Prim(PrimitiveType.Cylinder, rectDish, new Vector3(0f, 0.22f, 0f), new Vector3(4.8f, 0.04f, 4.8f), blue);
        Prim(PrimitiveType.Cylinder, rectDish, new Vector3(0f, 1.2f, 0f), new Vector3(0.16f, 1.0f, 0.16f), silver);
        Prim(PrimitiveType.Sphere, rectDish, new Vector3(0f, 2.2f, 0f), Vector3.one * 0.6f, gold);
        rectLabel = Label(rectenna, "<b>DYSON POWER RECEIVER</b>", 0f, 6.9f, 5f, 16f, 1.2f, new Color(1f, 0.84f, 0.3f));
        powerBeam = Line("Dyson power beam", Glow(new Color(1.3f, 0.95f, 0.3f)), 0.8f, root); powerBeam.startWidth = 3.5f; powerBeam.endWidth = 0.3f;
        for (int i = 0; i < 4; i++) { var s = Prim(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 1.5f, gold).transform; s.name = "Power pulse"; powerPulse.Add(s); }
        foreach (var r in rectenna.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void UpdateDyson(float dt)
    {
        // each panel is worth about 1 kW at the habitat (the base load is 9 kW)
        if (RealMission && !game.Paused) sim.battery = Mathf.Min(100f, sim.battery + dysonCount * (100f / (9f * 192f)) * dt);
    }

    void LateDyson()
    {
        if (dyson.Count == 0 || !sun) return;
        Vector3 toSun = -sun.transform.forward; bool up = toSun.y > -0.03f && sun.enabled;
        Vector3 c = cam.position + toSun * 3200f, u = Vector3.Cross(toSun, Vector3.up); if (u.sqrMagnitude < 0.001f) u = Vector3.right; u.Normalize(); Vector3 v = Vector3.Cross(toSun, u);
        float t = Time.time * 0.1f;
        for (int i = 0; i < dyson.Count; i++)
        {
            bool show = up && i < dysonCount; var p = dyson[i]; if (p.gameObject.activeSelf != show) p.gameObject.SetActive(show); if (!show) continue;
            float a = t + i * Mathf.PI * 2f / Mathf.Max(1, dysonCount);
            p.position = c + u * (Mathf.Cos(a) * 620f) + v * (Mathf.Sin(a) * 620f);      // a wide ring: the panels on the far side of it are below the horizon
            p.rotation = Quaternion.LookRotation(toSun, v) * Quaternion.Euler(0f, 25f * Mathf.Cos(a), a * Mathf.Rad2Deg * 0.5f);
        }
        if (!rectenna) return;
        // the power beam: straight from the swarm while the Sun is up, relayed by the satellite when it is not
        Transform hi = null; if (up) for (int i = 0; i < dysonCount && i < dyson.Count; i++) if (hi == null || dyson[i].position.y > hi.position.y) hi = dyson[i];
        Vector3 src = hi != null && hi.position.y > rectDish.position.y + 40f ? hi.position : (comSat ? comSat.position : rectDish.position + Vector3.up * 3000f);
        Vector3 aim = (src - rectDish.position).normalized; if (aim.y < 0.8f) { aim.y = 0.8f; aim.Normalize(); }      // the dish never tips over on its side
        rectDish.rotation = Quaternion.Slerp(rectDish.rotation, Quaternion.FromToRotation(Vector3.up, aim), Time.deltaTime * 2f);
        Vector3 focus = rectDish.position + rectDish.up * 2.2f, along = (src - focus).normalized;
        powerBeam.SetPosition(0, src); powerBeam.SetPosition(1, focus);
        for (int i = 0; i < powerPulse.Count; i++) { float k = (Time.time * 0.45f + i / (float)powerPulse.Count) % 1f; powerPulse[i].position = focus + along * (170f * (1f - k)); }
        if (rectLabel) rectLabel.transform.rotation = Quaternion.LookRotation(rectLabel.transform.position - cam.position, Vector3.up);
    }
    public string ProbeDyson() { Vector3 toSun = sun ? -sun.transform.forward : Vector3.up; Transform hi = null; foreach (var d in dyson) if (d.gameObject.activeSelf && (hi == null || d.position.y > hi.position.y)) hi = d; Face(hi ? hi.position : cam.position + toSun * 3200f); return "dyson=" + dysonCount + " sunElev=" + (Mathf.Asin(toSun.y) * Mathf.Rad2Deg).ToString("F1") + " receiver=" + (rectenna ? rectenna.position.ToString("F0") : "none"); }
    public string ProbeReceiver() { if (!rectenna) return "no receiver"; Vector3 p = rectenna.position + new Vector3(15f, 0f, 15f), g; if (GroundAt(p.x, p.z, out g)) p = g; TeleportTo(p, 225f); cam.position = origin.transform.position + Vector3.up * 1.4f; Face(rectenna.position + Vector3.up * 3.5f); var rb = new Bounds(rectenna.position, Vector3.zero); foreach (var r in rectenna.GetComponentsInChildren<Renderer>()) rb.Encapsulate(r.bounds); return "receiver at " + rectenna.position.ToString("F0") + " size " + rb.size.ToString("F1") + " cam " + cam.position.ToString("F0"); }

    public string ProbeAuto() { return "auto=" + autoOn + " lap=" + autoLap + " line=" + autoLine; }
}
