// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/05_World_FX/HunchWorldFX.cs
//  Section : 05_World_FX
//  Folder  : Living-world effects (Sun, earthshine, storms, doors, rockets) and the friendly extras
//            (robot buddy, stars, badges).
//  Purpose : Living-world effects. Sound, Sun, doors and camera effects are generated in code;
//            particle effects come from the imported Hovl Studio "Magic Effects FREE" pack when it
//            is in the project (see HunchFxLibrary), with simple built-in ones as a fallback:
//  Contains: HunchWorldFX, Door
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Living-world effects. Sound, Sun, doors and camera effects are generated in code; particle effects come from the imported
/// Hovl Studio "Magic Effects FREE" pack when it is in the project (see HunchFxLibrary), with simple built-in ones as a fallback:
/// the Sun circling low on the horizon and setting for the lunar night, earthshine, camera effects for warnings and
/// solar storms, automatic sliding doors, a supply rocket that launches and lands, satellites, dust kicked up outside,
/// and sound that behaves like the Moon: inside you hear the habitat, outside there is no air so you only hear your suit.
[DefaultExecutionOrder(1000)]
public class HunchWorldFX : MonoBehaviour
{
    public bool sfxOn = true, realisticSilence = true, cameraFx = true, doorsOn = true;
    public int DoorCount { get { return doors.Count; } }
    public bool Outside { get { return outside; } }

    HabitatGame game; HabitatSim sim; XROrigin origin; HunchNavAssist nav;
    Light sun, earthshine; float sunBaseIntensity, sunAz0, sunElev = 13f; Material sky; float skyBase = 1.25f; Color ambientBase;
    Vignette vig; ChromaticAberration chroma; FilmGrain grain; Volume fxVolume;
    AudioSource hum, breath, alarm, rumble, shots; AudioLowPassFilter lowpass;
    AudioClip cStep, cHiss, cClick, cQuindar, cDoor;
    bool outside; float outsideCheck, lastTransition = -99f, stepDist, geiger; Vector3 lastFeet; string lastMsg = "";
    ParticleSystem dust, exhaust; Light exhaustLight;
    Transform rocket; Vector3 rocketHome; int rocketState; float rocketT; int launchesDone; Collider rocketCol; Vector3 shake;
    readonly List<Transform> sats = new List<Transform>();
    readonly List<Light> beacons = new List<Light>();

    class Door { public Transform root, a, b; public Vector3 centre, axis; public float width, open; public bool wasOpen; }
    readonly List<Door> doors = new List<Door>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var g = FindFirstObjectByType<HabitatGame>();
        if (g && !g.GetComponent<HunchWorldFX>()) g.gameObject.AddComponent<HunchWorldFX>();
    }

    // ------------------------------------------------------------------ materials cloned from ones already in the scene
    Material Clear(Color c) { var m = new Material(game.zones[0].ring.sharedMaterial); m.SetColor("_BaseColor", c); return m; }
    Material Solid(Color c) { var m = new Material(game.crewStatus[0].sharedMaterial); m.SetColor("_BaseColor", c); return m; }

    IEnumerator Start()
    {
        game = GetComponent<HabitatGame>(); sim = game.sim; origin = game.origin; nav = GetComponent<HunchNavAssist>();
        yield return new WaitForSeconds(0.8f);
        if (!origin) origin = game.origin;
        SetupSun(); SetupCameraFx(); SetupAudio(); SetupParticles(); SetupRocket(); SetupSatellites(); SetupPackFx();
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("Warning Beacon " + (i + 1)); var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.15f, 0.1f); l.range = 12f; l.intensity = 0f; l.shadows = LightShadows.None; beacons.Add(l);
        }
        if (origin && origin.Camera) lastFeet = origin.Camera.transform.position;
        ready = true;
        if (doorsOn && !Application.isBatchMode) StartCoroutine(FindDoors());
    }
    bool ready;

    // ------------------------------------------------------------------ Sun, earthshine, sky
    void SetupSun()
    {
        sun = RenderSettings.sun;
        if (!sun) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
        if (sun)
        {
            sunBaseIntensity = sun.intensity;
            Vector3 toSun = -sun.transform.forward; sunAz0 = Mathf.Atan2(toSun.x, toSun.z) * Mathf.Rad2Deg; sunElev = Mathf.Asin(Mathf.Clamp(toSun.y, -1f, 1f)) * Mathf.Rad2Deg;
        }
        var earth = GameObject.Find("BAKED_EARTH");
        var eg = new GameObject("Earthshine"); earthshine = eg.AddComponent<Light>();
        earthshine.type = LightType.Directional; earthshine.color = new Color(0.6f, 0.75f, 1f); earthshine.intensity = 0.1f; earthshine.shadows = LightShadows.None;
        Vector3 from = earth && earth.GetComponent<Renderer>() ? earth.GetComponent<Renderer>().bounds.center : new Vector3(-4000f, 1000f, -1900f);
        eg.transform.rotation = Quaternion.LookRotation((Vector3.zero - from).normalized);
        if (RenderSettings.skybox) { sky = new Material(RenderSettings.skybox); RenderSettings.skybox = sky; if (sky.HasProperty("_Exposure")) skyBase = sky.GetFloat("_Exposure"); }
        ambientBase = RenderSettings.ambientLight;
    }

    void UpdateSun()
    {
        if (!sun) return;
        float d = sim.missionSeconds / HabitatSim.SecondsPerDay;          // mission day, 0..30
        // At the lunar south pole the Sun never climbs high: it circles the horizon, then sets for the long night.
        float target = sim.phase == HabPhase.Mission || sim.phase == HabPhase.Report
            ? (sim.LunarNight ? -9f : 3.5f + 11f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(d < 9f ? d / 9f : (d - 18f) / 12f)))
            : sunElev;
        sunElev = Mathf.Lerp(sunElev, target, 1f - Mathf.Exp(-0.5f * Time.deltaTime));
        float az = (sunAz0 + 18f * d) * Mathf.Deg2Rad, el = sunElev * Mathf.Deg2Rad;
        Vector3 toSun = new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
        sun.transform.rotation = Quaternion.LookRotation(-toSun);
        float up = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2f, 2.5f, sunElev));
        sun.intensity = sunBaseIntensity * up;                            // no air on the Moon: sunlight stays white, it just gets cut off by the horizon
        earthshine.intensity = Mathf.Lerp(0.2f, 0.08f, up);
        RenderSettings.ambientLight = ambientBase * Mathf.Lerp(0.55f, 1f, up);
        if (sky && sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", skyBase * Mathf.Lerp(1.6f, 0.8f, up));   // eyes and cameras see more stars in the dark
    }

    // ------------------------------------------------------------------ camera effects for warnings and storms
    void SetupCameraFx()
    {
        var go = new GameObject("HUNCH Live FX Volume"); fxVolume = go.AddComponent<Volume>(); fxVolume.isGlobal = true; fxVolume.priority = 20f;
        var p = ScriptableObject.CreateInstance<VolumeProfile>(); fxVolume.profile = p;
        vig = p.Add<Vignette>(true); vig.intensity.Override(0.16f); vig.color.Override(Color.black); vig.smoothness.Override(0.45f);
        chroma = p.Add<ChromaticAberration>(true); chroma.intensity.Override(0f);
        grain = p.Add<FilmGrain>(true); grain.intensity.Override(0f); grain.response.Override(0.6f);
    }

    void UpdateCameraFx()
    {
        if (!fxVolume) return;
        fxVolume.weight = cameraFx ? 1f : 0f;
        float urgency = 0f;
        foreach (var a in sim.alerts) { float k = 1f - Mathf.Clamp01(a.secondsLeft / 14f); if (a.overdue) k = 1f; urgency = Mathf.Max(urgency, k); }
        float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
        bool vr = game.HeadsetActive;
        Color c = Color.Lerp(Color.black, new Color(0.7f, 0f, 0f), urgency);
        float inten = Mathf.Lerp(0.16f, vr ? 0.28f : 0.44f, urgency * pulse);
        if (sim.Blackout) { c = Color.black; inten = 0.5f; }
        vig.color.value = Color.Lerp(vig.color.value, c, Time.unscaledDeltaTime * 4f);
        vig.intensity.value = Mathf.Lerp(vig.intensity.value, inten, Time.unscaledDeltaTime * 6f);
        float storm = sim.stormActive && !vr ? (sim.sheltered ? 0.25f : 1f) : 0f;
        chroma.intensity.value = Mathf.Lerp(chroma.intensity.value, 0.6f * storm, Time.unscaledDeltaTime * 3f);
        grain.intensity.value = Mathf.Lerp(grain.intensity.value, 0.7f * storm, Time.unscaledDeltaTime * 3f);

        int b = 0;
        foreach (var a in sim.alerts)
        {
            if (b >= beacons.Count) break;
            int zi = game.ZoneIndex(a.zoneId); if (zi < 0) continue;
            beacons[b].transform.position = game.zones[zi].position + Vector3.up * 3.2f;
            beacons[b].intensity = (cameraFx ? 22f : 0f) * (0.15f + 0.85f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f)));      // a flashing warning lamp, not a floodlight
            b++;
        }
        for (; b < beacons.Count; b++) beacons[b].intensity = 0f;
    }

    // ------------------------------------------------------------------ sound made from maths: no audio files needed
    static AudioClip Clip(string name, float seconds, System.Func<float, float, float> f, bool loop = false)
    {
        int rate = 44100, n = Mathf.RoundToInt(rate * seconds); var d = new float[n];
        for (int i = 0; i < n; i++) d[i] = f(i / (float)rate, i / (float)n);
        if (loop)
        {
            int fade = rate / 5;                                           // blend the end into the start so the loop has no click
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; d[i] = d[i] * k + d[n - fade + i] * (1f - k); }
            n -= fade; System.Array.Resize(ref d, n);
        }
        var c = AudioClip.Create(name, n, 1, rate, false); c.SetData(d, 0); return c;
    }

    AudioSource Source(string name, AudioClip clip, bool loop, float volume)
    {
        var go = new GameObject("SFX " + name); go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>(); s.clip = clip; s.loop = loop; s.volume = volume; s.spatialBlend = 0f; s.playOnAwake = false;
        if (loop && clip) s.Play();
        return s;
    }

    void SetupAudio()
    {
        var rng = new System.Random(11); float lp = 0f, lp2 = 0f;
        var cHum = Clip("habitat hum", 5f, (t, u) => { lp = Mathf.Lerp(lp, (float)rng.NextDouble() * 2f - 1f, 0.02f); return 0.06f * Mathf.Sin(2f * Mathf.PI * 60f * t) + 0.035f * Mathf.Sin(2f * Mathf.PI * 120f * t) + 0.02f * Mathf.Sin(2f * Mathf.PI * 240f * t) + lp * 0.9f; }, true);
        var cBreath = Clip("suit breathing", 4.6f, (t, u) =>
        {
            float env = u < 0.4f ? Mathf.Sin(Mathf.PI * u / 0.4f) : (u > 0.5f && u < 0.92f ? 0.7f * Mathf.Sin(Mathf.PI * (u - 0.5f) / 0.42f) : 0f);
            lp2 = Mathf.Lerp(lp2, (float)rng.NextDouble() * 2f - 1f, u < 0.45f ? 0.22f : 0.1f);
            return lp2 * env * 0.8f + 0.012f * Mathf.Sin(2f * Mathf.PI * 180f * t);                   // air through the regulator, plus the fan
        }, true);
        var cAlarm = Clip("warning tone", 1.2f, (t, u) => (u < 0.5f ? Mathf.Sin(2f * Mathf.PI * 740f * t) : Mathf.Sin(2f * Mathf.PI * 587f * t)) * 0.35f * Mathf.Min(1f, Mathf.Sin(Mathf.PI * ((u * 2f) % 1f)) * 3f), true);
        float lr = 0f;
        var cRumble = Clip("rocket rumble", 3f, (t, u) => { lr = Mathf.Lerp(lr, (float)rng.NextDouble() * 2f - 1f, 0.015f); return lr * 4f; }, true);
        cStep = Clip("footstep", 0.14f, (t, u) => (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(95f, 45f, u) * t) * 0.8f + ((float)rng.NextDouble() - 0.5f) * 0.3f) * Mathf.Pow(1f - u, 3f));
        float lh = 0f;
        cHiss = Clip("airlock hiss", 1.6f, (t, u) => { float w = (float)rng.NextDouble() * 2f - 1f; lh = Mathf.Lerp(lh, w, 0.6f); return (w - lh) * 0.5f * Mathf.Pow(1f - u, 1.5f) * Mathf.Min(1f, u * 30f); });
        cClick = Clip("radiation click", 0.012f, (t, u) => ((float)rng.NextDouble() * 2f - 1f) * Mathf.Pow(1f - u, 2f));
        cQuindar = Clip("radio tone", 0.25f, (t, u) => Mathf.Sin(2f * Mathf.PI * 2525f * t) * 0.3f * Mathf.Min(1f, Mathf.Min(u, 1f - u) * 20f));
        float ld = 0f;
        cDoor = Clip("door", 0.5f, (t, u) => { ld = Mathf.Lerp(ld, (float)rng.NextDouble() * 2f - 1f, 0.15f); return ld * Mathf.Sin(Mathf.PI * u) * 0.9f + 0.1f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 320f, u) * t) * Mathf.Sin(Mathf.PI * u); });
        hum = Source("hum", cHum, true, 0f); breath = Source("breath", cBreath, true, 0f); alarm = Source("alarm", cAlarm, true, 0f); rumble = Source("rumble", cRumble, true, 0f);
        shots = Source("one shots", null, false, 1f);
        breath.bypassListenerEffects = true;                                 // your own breathing is inside the suit, so it is never muffled
        var listener = FindFirstObjectByType<AudioListener>();
        if (listener) { lowpass = listener.gameObject.GetComponent<AudioLowPassFilter>(); if (!lowpass) lowpass = listener.gameObject.AddComponent<AudioLowPassFilter>(); lowpass.cutoffFrequency = 22000f; }
    }

    void Play(AudioClip c, float v) { if (sfxOn && shots && c && Time.timeScale <= 2f) shots.PlayOneShot(c, v); }

    int outsideVotes;
    /// Inside = a roof over you OR walls all around you. One thin ray straight up used to miss through roof gaps,
    /// skylights and the open ring over the meeting room, so you were told you were "outside" while indoors.
    static bool Enclosed(Vector3 p)
    {
        int up = 0;
        if (Physics.SphereCast(p, 0.35f, Vector3.up, out _, 200f, ~0, QueryTriggerInteraction.Ignore)) up += 2;
        for (int i = 0; i < 6; i++)
        {
            Vector3 d = Quaternion.Euler(0f, i * 60f, 0f) * (Quaternion.Euler(-65f, 0f, 0f) * Vector3.forward);
            if (Physics.Raycast(p, d, 200f, ~0, QueryTriggerInteraction.Ignore)) up++;
        }
        if (up >= 4) return true;
        int walls = 0;
        for (int i = 0; i < 8; i++)
            if (Physics.Raycast(p, Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward, 30f, ~0, QueryTriggerInteraction.Ignore)) walls++;
        return walls >= 7 && up >= 1;
    }

    void UpdateAudio()
    {
        if (!origin || !origin.Camera) return;
        var cam = origin.Camera.transform;
        // inside or outside? anything overhead (roof or the pressurised dome) counts as inside
        outsideCheck -= Time.unscaledDeltaTime;
        if (outsideCheck <= 0f)
        {
            outsideCheck = 0.35f;
            bool now = sim.phase != HabPhase.Briefing && !Enclosed(cam.position);
            if (now != outside) outsideVotes++; else outsideVotes = 0;
            if (outsideVotes >= 2)      // must disagree twice in a row (0.7 s) — no flicker under skylights or ring gaps
            {
                outsideVotes = 0;
                outside = now;
                if (Time.unscaledTime - lastTransition > 6f && sim.phase != HabPhase.Briefing)
                {
                    Play(cHiss, 0.5f); lastTransition = Time.unscaledTime;
                    sim.Say(outside ? "ARIA: You are outside. There is no air on the Moon, so there is no sound. You only hear your own suit." : "ARIA: Back in pressurised space. You can hear the habitat again.");
                }
            }
        }
        float dt = Time.unscaledDeltaTime * 2.5f; float on = sfxOn ? 1f : 0f;
        bool vac = outside && realisticSilence;
        hum.volume = Mathf.Lerp(hum.volume, on * (vac ? 0f : (sim.Blackout ? 0.02f : 0.16f)), dt);
        breath.volume = Mathf.Lerp(breath.volume, on * (outside ? 0.5f : 0f), dt);
        float urgent = 0f; foreach (var a in sim.alerts) if (a.secondsLeft < 10f || a.overdue) urgent = 1f;
        alarm.volume = Mathf.Lerp(alarm.volume, on * urgent * (vac ? 0.04f : 0.12f), dt);
        if (lowpass) lowpass.cutoffFrequency = Mathf.Lerp(lowpass.cutoffFrequency, vac ? 500f : 22000f, dt);

        // footsteps and Moon dust
        Vector3 feet = nav ? nav.Feet() : new Vector3(cam.position.x, origin.transform.position.y, cam.position.z);
        Vector3 d = feet - lastFeet; lastFeet = feet; float moved = new Vector2(d.x, d.z).magnitude;
        if (moved < 3f && !game.flyMode) stepDist += moved; else stepDist = 0f;
        if (stepDist > 2.1f)
        {
            stepDist = 0f; Play(cStep, outside ? 0.25f : 0.4f);
            if (outside)
            {
                if (HunchFx.Has("dust puff")) HunchFx.Spawn("dust puff", feet + Vector3.up * 0.05f, 0.07f, 1.5f);      // Moon dust, from the imported pack
                else if (dust) { dust.transform.position = feet + Vector3.up * 0.05f; dust.Emit(10); }
            }
        }
        // ARIA's radio beep, like Mission Control
        if (sim.message != lastMsg) { lastMsg = sim.message; if (sim.messageAge < 0.5f) Play(cQuindar, 0.22f); }
        // radiation counter during a solar storm
        if (sim.stormActive) { geiger -= Time.unscaledDeltaTime; if (geiger <= 0f) { geiger = Random.Range(0.02f, sim.sheltered ? 0.5f : 0.09f); Play(cClick, 0.6f); } }
    }

    // ------------------------------------------------------------------ particles
    void SetupParticles()
    {
        dust = MakeParticles("Moon Dust", Clear(new Color(0.62f, 0.6f, 0.57f, 0.5f)), 1.8f, 1.0f, 2.4f, 0.08f, 0.22f, 55f, 0.12f, 1f);
        dust.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
    }

    ParticleSystem MakeParticles(string name, Material mat, float life, float speedMin, float speedMax, float sizeMin, float sizeMax, float angle, float radius, float gravity)
    {
        var go = new GameObject(name); var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = false; main.playOnAwake = false; main.startLifetime = life; main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax); main.gravityModifier = gravity; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 600;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = angle; sh.radius = radius;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    // ------------------------------------------------------------------ supply rocket: launches on days 5, 15 and 25, lands two days later
    void SetupRocket()
    {
        var go = GameObject.Find("BAKED_ROCKET"); if (!go) return;
        rocket = go.transform; rocketHome = rocket.position; rocketCol = go.GetComponent<Collider>();
        var r = go.GetComponent<Renderer>(); Vector3 nozzle = r ? new Vector3(r.bounds.center.x, r.bounds.min.y + 1f, r.bounds.center.z) : rocketHome;
        padPos = nozzle + Vector3.down * 0.9f;
        var puff = HunchFx.MaterialOf("smoke puff");                       // soft round puffs from the imported pack instead of plain squares
        exhaust = MakeParticles("Rocket Exhaust", puff ? puff : Clear(new Color(1f, 0.75f, 0.35f, 0.85f)), 1.4f, 30f, 60f, 4f, 9f, 9f, 2.5f, 0f);
        if (puff) { var em = exhaust.main; em.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.45f, 0.9f), new Color(1f, 0.55f, 0.2f, 0.7f)); em.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f); }
        exhaust.transform.SetParent(rocket, true); exhaust.transform.position = nozzle; exhaust.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var lg = new GameObject("Rocket Flame Light"); lg.transform.SetParent(rocket, true); lg.transform.position = nozzle + Vector3.down * 3f;
        exhaustLight = lg.AddComponent<Light>(); exhaustLight.type = LightType.Point; exhaustLight.color = new Color(1f, 0.7f, 0.4f); exhaustLight.range = 500f; exhaustLight.intensity = 0f; exhaustLight.shadows = LightShadows.None;
    }

    bool launchNow, manual; float awaySince; public bool rocketOff;      // NASA HUNCH mode hides the spaceport: no launches then
    /// Launch the supply rocket now (L key, pause menu, or "launch the rocket" by voice). Returns false if it is not on the pad.
    public bool LaunchRocket()
    {
        if (rocketOff) return false;
        if (!rocket || rocketState != 0 || sim.phase != HabPhase.Mission) return false;
        launchNow = true; manual = true; return true;
    }
    public string RocketWhere { get { return !rocket ? "" : (rocketState == 0 ? "on the pad" : (rocketState == 3 ? "in flight" : (rocketState == 4 ? "landing" : "launching"))); } }

    void UpdateRocket()
    {
        if (!rocket) return;
        float day = sim.missionSeconds / HabitatSim.SecondsPerDay; shake = Vector3.zero; padBlast = false;
        bool mission = sim.phase == HabPhase.Mission;
        if (launchNow) { launchNow = false; if (rocketState == 0) { rocketState = 1; rocketT = 0f; sim.Say("ARIA: Supply rocket launching from the spaceport in 5 seconds. Watch the sky!"); } }
        float burn = 0f;
        switch (rocketState)
        {
            case 0:                                                        // on the pad
                if (mission && !rocketOff && launchesDone < 3 && day >= 4f + 10f * launchesDone) { rocketState = 1; rocketT = 0f; sim.Say("ARIA: Supply rocket launching from the spaceport in 5 seconds. Watch the sky!"); }
                break;
            case 1:                                                        // countdown
                rocketT += Time.deltaTime; burn = Mathf.Clamp01(rocketT / 5f) * 0.4f; padBlast = rocketT > 2.5f;
                if (rocketT >= 5f) { rocketState = 2; rocketT = 0f; if (rocketCol) rocketCol.enabled = false; HunchFx.Spawn("smoke aoe explosion", padPos, 9f, 1.8f); }
                break;
            case 2:                                                        // climbing
                rocketT += Time.deltaTime; burn = 1f; padBlast = rocketT < 4f;
                rocket.position = rocketHome + Vector3.up * (0.5f * 9f * rocketT * rocketT);
                if (rocketT > 20f) { rocketState = 3; rocketT = day; awaySince = Time.time; rocket.gameObject.SetActive(false); if (!manual) launchesDone++; manual = false; }
                break;
            case 3:                                                        // away for two days
                if (day >= rocketT + 2f || day < rocketT || Time.time > awaySince + 60f || !mission) { rocketState = 4; rocketT = 0f; rocket.gameObject.SetActive(true); sim.Say("ARIA: Supply rocket on final approach. Landing at the spaceport."); }
                break;
            case 4:                                                        // landing
                rocketT += Time.deltaTime; burn = 0.8f;
                float left = Mathf.Max(0f, 16f - rocketT);
                rocket.position = rocketHome + Vector3.up * (0.5f * 7f * left * left);
                if (left <= 0f) { rocketState = 0; rocket.position = rocketHome; if (rocketCol) rocketCol.enabled = true; HunchFx.Spawn("smoke aoe explosion", padPos, 7f, 1.8f); }
                padBlast = left < 5f;
                break;
        }
        if (exhaust)
        {
            if (burn > 0.05f && rocket.gameObject.activeSelf) exhaust.Emit(Mathf.CeilToInt(220f * burn * Time.deltaTime));
            exhaustLight.intensity = Mathf.Lerp(exhaustLight.intensity, burn * 60000f * (0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 20f, 0f)), Time.deltaTime * 8f);
        }
        // regolith blasted off the pad by the engine (imported pack)
        if (padBlast && !padDust) padDust = HunchFx.Spawn("dust hemisphere loop", padPos, 7f, 0f);
        else if (!padBlast && padDust) { HunchFx.Stop(padDust); padDust = null; }
        // you feel a launch through the ground, even where there is no air to carry sound
        float dist = origin && origin.Camera ? Vector3.Distance(origin.Camera.transform.position, rocket.position) : 9999f;
        float feel = burn * Mathf.Clamp01(1f - dist / 900f);
        if (rumble) rumble.volume = Mathf.Lerp(rumble.volume, sfxOn ? feel * 0.6f : 0f, Time.deltaTime * 4f);
        if (cameraFx && !game.HeadsetActive && feel > 0.02f) shake = new Vector3(Mathf.PerlinNoise(Time.time * 30f, 1f) - 0.5f, Mathf.PerlinNoise(Time.time * 30f, 7f) - 0.5f, 0f) * 0.08f * feel;
    }

    // ------------------------------------------------------------------ effects from the imported Hovl Studio pack
    readonly Dictionary<HabAlert, GameObject> alertFx = new Dictionary<HabAlert, GameObject>();
    readonly List<HabAlert> gone = new List<HabAlert>();
    GameObject shieldFx, padDust; Vector3 padPos; bool padBlast;

    void SetupPackFx() { sim.onRaised += OnRaised; sim.onResolved += OnResolved; sim.onCare += OnCare; }
    void OnDestroy() { if (sim != null) { sim.onRaised -= OnRaised; sim.onResolved -= OnResolved; sim.onCare -= OnCare; } }

    Vector3 Spot(string zoneId)                                            // the glowing orb is where the equipment is
    {
        int zi = game.ZoneIndex(zoneId); if (zi < 0) return Vector3.zero;
        var z = game.zones[zi]; return z.orb ? z.orb.transform.position : z.position + Vector3.up * 1.2f;
    }
    Vector3 Floor(string zoneId) { int zi = game.ZoneIndex(zoneId); return zi < 0 ? Vector3.zero : game.zones[zi].position + Vector3.up * 0.05f; }
    Vector3 Crew(int k) { return game.crewAvatars != null && k >= 0 && k < game.crewAvatars.Length && game.crewAvatars[k] ? game.crewAvatars[k].position : (origin && origin.Camera ? origin.Camera.transform.position + Vector3.down * 1.2f : Vector3.zero); }      // crew member 0 is you

    void OnRaised(HabAlert a)                                              // something that keeps playing until the problem is fixed
    {
        string fx = null; float s = 1f; Vector3 p = Spot(a.zoneId);
        switch (a.type)
        {
            case "scrubber": case "water": case "comms": case "arrays": fx = "sparks flashing yellow"; s = 1.2f; break;      // broken equipment sparks
            case "leak": fx = "smoke loop"; s = 0.35f; HunchFx.Spawn("stones hit", p, 0.7f, 0.9f); break;                    // micrometeoroid hit, air venting
            case "eva": fx = "dust loop"; s = 0.3f; break;                                                                  // dusty suits in the airlock
        }
        if (fx == null) return;
        var g = HunchFx.Spawn(fx, p, s, 0f); if (g) alertFx[a] = g;
    }

    void OnResolved(HabAlert a)
    {
        Vector3 p = Spot(a.zoneId), c = Crew(a.crew);
        switch (a.type)
        {
            case "psych": HunchFx.Spawn("healing", c, 0.6f, 5f); HunchFx.Spawn("love hit", c + Vector3.up * 1.7f, 0.5f, 0.9f); break;
            case "medical": HunchFx.Spawn("healing circle", c + Vector3.up * 0.05f, 0.7f, 6f); HunchFx.Spawn("healing", c, 0.6f, 5f); break;
            case "night": HunchFx.Spawn("electro hit", p, 0.9f, 0.9f); break;
            case "eva": HunchFx.Spawn("dust puff", p, 0.25f, 1.8f); break;
            case "sleep": HunchFx.Spawn("star aura", Floor(a.zoneId), 0.8f, 4f); break;
            case "storm": break;                                           // the shield below shows it
            default: HunchFx.Spawn("sparks explode white", p, 1.2f, 0.9f); HunchFx.Spawn("star hit", p, 0.45f, 0.9f); break;
        }
    }

    void OnCare(string zone)
    {
        Vector3 p = Spot(zone);
        switch (zone)
        {
            case "greenhouse": HunchFx.Spawn("sparks explode green", p, 1.2f, 0.9f); break;
            case "medical": HunchFx.Spawn("healing circle", Floor(zone), 0.7f, 4f); break;
            case "exercise": HunchFx.Spawn("buff", Floor(zone), 0.7f, 3f); break;
            case "galley": case "sanctuary": case "observation": case "control": HunchFx.Spawn("love hit", p, 0.45f, 0.9f); break;
            case "power": case "fab": HunchFx.Spawn("electro hit", p, 0.6f, 0.9f); break;
            default: HunchFx.Spawn("sparks explode blue", p, 1f, 0.9f); break;
        }
    }

    void UpdatePackFx()
    {
        if (alertFx.Count > 0)
        {
            gone.Clear();
            foreach (var kv in alertFx) if (!sim.alerts.Contains(kv.Key)) gone.Add(kv.Key); else if (kv.Value) kv.Value.transform.position = Spot(kv.Key.zoneId);
            foreach (var a in gone) { HunchFx.Stop(alertFx[a]); alertFx.Remove(a); }
        }
        // the regolith storm shelter doing its job: a shield while the crew rides out a solar storm inside
        bool shield = sim.phase == HabPhase.Mission && sim.sheltered;
        if (shield && !shieldFx) shieldFx = HunchFx.Spawn("magic shield blue", Floor("shelter"), 3.2f, 0f);
        else if (!shield && shieldFx) { HunchFx.Stop(shieldFx); shieldFx = null; }
    }

    void SetupSatellites()
    {
        var m = Solid(new Color(1.6f, 1.6f, 1.5f));
        for (int i = 0; i < 3; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.name = "Satellite " + (i + 1); Destroy(s.GetComponent<Collider>());
            s.transform.localScale = Vector3.one * 9f; var r = s.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
            sats.Add(s.transform);
        }
    }

    void UpdateSatellites()
    {
        for (int i = 0; i < sats.Count; i++)
        {
            float a = Time.time * (0.11f + 0.03f * i) + i * 2.1f;
            Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3600f;
            sats[i].position = Quaternion.Euler(55f + 20f * i, 40f * i, 0f) * p;      // tilted orbits, so they rise, cross the sky and set
        }
    }

    // ------------------------------------------------------------------ automatic doors, found by measuring the building itself
    static float Reach(Vector3 from, Vector3 dir, float max)
    {
        RaycastHit h; return Physics.Raycast(from, dir, out h, max, ~0, QueryTriggerInteraction.Ignore) ? h.distance : max;
    }
    static float Width(Vector3 p, Vector3 axis)
    {
        float w = 0f;      // the widest of three heights: a real doorway is narrow from floor to head height, a gap between furniture is not
        foreach (float hh in new[] { 0.5f, 1.5f, 2.7f }) w = Mathf.Max(w, Reach(p + Vector3.up * hh, axis, 8f) + Reach(p + Vector3.up * hh, -axis, 8f));
        return w;
    }

    readonly List<Vector3> rejected = new List<Vector3>(); readonly System.Text.StringBuilder doorLog = new System.Text.StringBuilder();
    /// null when the opening at c is a proper doorway; otherwise the reason it is not
    static string DoorFault(Vector3 c, Vector3 axis, Vector3 pass, float w, float top)
    {
        if (top > 6.2f) return "no lintel (open to " + top.ToString("F1") + " m)";
        foreach (float hh in new[] { 0.5f, 1.5f })
        {
            float f1 = Reach(c + Vector3.up * hh, pass, 4f), f2 = Reach(c + Vector3.up * hh, -pass, 4f);
            if (f1 < 1.6f || f2 < 1.6f) return "blocked " + Mathf.Min(f1, f2).ToString("F1") + " m past the opening";
        }
        int walls = 0;
        for (int side = -1; side <= 1; side += 2)
        {
            // the wall the door is in must carry on beyond the door frame, on at least one side
            Vector3 q = c + axis * (side * (w * 0.5f + 0.7f)) + Vector3.up * 1.5f;
            if (Reach(q - pass * 1.5f, pass, 3f) <= 2.6f) walls++;
        }
        if (walls == 0) return "no wall beside the opening";
        return null;
    }

    IEnumerator FindDoors()
    {
        yield return new WaitForSeconds(1.5f);
        var seen = new HashSet<long>(); var found = new List<Door>(); int budget = 0; const float step = 0.5f;
        var glass = Clear(new Color(0.45f, 0.8f, 1f, 0.42f)); var trim = Solid(new Color(1f, 0.78f, 0.3f));
        foreach (var z in game.zones)
        {
            for (int i = -28; i <= 28; i++)
                for (int j = -28; j <= 28; j++)
                {
                    Vector3 p = new Vector3(Mathf.Round((z.position.x + i * step) / step) * step, z.position.y, Mathf.Round((z.position.z + j * step) / step) * step);
                    long key = ((long)Mathf.RoundToInt(p.x / step) << 40) ^ ((long)Mathf.RoundToInt(p.z / step) << 16) ^ (long)Mathf.RoundToInt(p.y);
                    if (!seen.Add(key)) continue;
                    if (++budget % 250 == 0) yield return null;
                    RaycastHit fl;
                    if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out fl, 2.3f, ~0, QueryTriggerInteraction.Ignore) || fl.normal.y < 0.7f) continue;
                    Vector3 f = fl.point;
                    for (int ax = 0; ax < 2; ax++)
                    {
                        Vector3 axis = ax == 0 ? Vector3.right : Vector3.forward, pass = ax == 0 ? Vector3.forward : Vector3.right;
                        float w = Width(f, axis);
                        if (w < 1.6f || w > 4.4f) continue;
                        if (Reach(f + Vector3.up * 1.5f, pass, 4f) < 3.2f || Reach(f + Vector3.up * 1.5f, -pass, 4f) < 3.2f) continue;      // must be able to walk through
                        if (Width(f + pass * 2.4f, axis) < w + 2f || Width(f - pass * 2.4f, axis) < w + 2f) continue;                    // a thin wall with a room on each side, not a corridor
                        float a = Reach(f + Vector3.up * 1.0f, axis, 8f), b = Reach(f + Vector3.up * 1.0f, -axis, 8f);
                        Vector3 c = f + axis * (a - b) * 0.5f;
                        float top = Reach(c + Vector3.up * 0.5f, Vector3.up, 12f) + 0.5f;
                        if (top < 3f) continue;
                        bool dup = false;
                        foreach (var e in found) if ((e.centre - c).sqrMagnitude < 25f) { dup = true; break; }
                        if (dup) continue;
                        // a real doorway, not a gap between two pillars or two pieces of furniture
                        string why = DoorFault(c, axis, pass, w, top);
                        if (why != null)
                        {
                            bool logged = false; foreach (var rc in rejected) if ((rc - c).sqrMagnitude < 25f) { logged = true; break; }
                            if (!logged) { rejected.Add(c); doorLog.Append(" [no door at ").Append(c.ToString("F1")).Append(": ").Append(why).Append("]"); }
                            continue;
                        }
                        found.Add(new Door { centre = c, axis = axis, width = w, open = 0f });
                        var d = found[found.Count - 1]; float hgt = Mathf.Min(top - 0.1f, 5.6f);
                        var root = new GameObject("Auto Door " + found.Count); root.transform.position = c;
                        d.root = root.transform; d.a = Panel(root.transform, glass, trim, axis, w, hgt, -1f); d.b = Panel(root.transform, glass, trim, axis, w, hgt, 1f);
                        if (found.Count >= 24) break;
                    }
                    if (found.Count >= 24) break;
                }
            if (found.Count >= 24) break;
        }
        doors.AddRange(found);
        Debug.Log("[HUNCH] automatic doors placed: " + doors.Count);
    }

    Transform Panel(Transform root, Material glass, Material trim, Vector3 axis, float w, float h, float side)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(p.GetComponent<Collider>()); p.name = side < 0f ? "Left Panel" : "Right Panel";
        p.transform.SetParent(root, false); p.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, axis), Vector3.up);
        p.transform.localScale = new Vector3(w * 0.5f, h, 0.1f); p.transform.position = root.position + axis * side * w * 0.25f + Vector3.up * h * 0.5f;
        var r = p.GetComponent<Renderer>(); r.sharedMaterial = glass; r.shadowCastingMode = ShadowCastingMode.Off;
        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(bar.GetComponent<Collider>()); bar.name = "Trim";
        bar.transform.SetParent(p.transform, false); bar.transform.localScale = new Vector3(0.06f, 1f, 1.3f); bar.transform.localPosition = new Vector3(-side * 0.47f, 0f, 0f);
        var br = bar.GetComponent<Renderer>(); br.sharedMaterial = trim; br.shadowCastingMode = ShadowCastingMode.Off;
        return p.transform;
    }

    void UpdateDoors()
    {
        if (doors.Count == 0 || !origin || !origin.Camera) return;
        Vector3 player = origin.Camera.transform.position;
        foreach (var d in doors)
        {
            if (d.root.gameObject.activeSelf != doorsOn) d.root.gameObject.SetActive(doorsOn);
            if (!doorsOn) continue;
            bool near = Flat(player - d.centre) < 5.5f && Mathf.Abs(player.y - 1.4f - d.centre.y) < 3.5f;
            if (!near && game.crewAvatars != null) foreach (var c in game.crewAvatars) if (c && Flat(c.position - d.centre) < 3.5f && Mathf.Abs(c.position.y - d.centre.y) < 3f) { near = true; break; }
            if (near && !d.wasOpen && Flat(player - d.centre) < 25f) Play(cDoor, 0.35f);
            d.wasOpen = near;
            d.open = Mathf.MoveTowards(d.open, near ? 1f : 0f, Time.deltaTime * 2.2f);
            float e = Mathf.SmoothStep(0f, 1f, d.open); float hgt = d.a.localScale.y;
            d.a.position = d.centre - d.axis * d.width * (0.25f + 0.48f * e) + Vector3.up * hgt * 0.5f;
            d.b.position = d.centre + d.axis * d.width * (0.25f + 0.48f * e) + Vector3.up * hgt * 0.5f;
        }
    }
    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // for the development test helper
    public string DoorInfo()
    {
        var sb = new System.Text.StringBuilder("doors " + doors.Count + ":");
        foreach (var d in doors) sb.Append(" [").Append(d.centre.x.ToString("F1")).Append(",").Append(d.centre.y.ToString("F1")).Append(",").Append(d.centre.z.ToString("F1")).Append(" w").Append(d.width.ToString("F1")).Append(d.axis.x > 0.5f ? " X" : " Z").Append(" h").Append(d.a ? d.a.localScale.y.ToString("F1") : "?").Append("]");
        sb.Append("  REJECTED ").Append(rejected.Count).Append(":").Append(doorLog);
        return sb.ToString();
    }
    public int probeDoor;
    public string ProbeDoorView()
    {
        if (doors.Count == 0) return "no doors";
        var d = doors[probeDoor % doors.Count]; probeDoor++;
        Vector3 pass = Vector3.Cross(Vector3.up, d.axis), p = d.centre - pass * 7.5f + Vector3.up * 0.1f;
        var cc = origin.GetComponent<CharacterController>(); if (cc) cc.enabled = false; origin.transform.position = p; if (cc) cc.enabled = true; Physics.SyncTransforms();
        var dk = GetComponent<HunchDesktopFallback>(); if (dk) dk.SetLook(Mathf.Atan2(pass.x, pass.z) * Mathf.Rad2Deg, 0f);
        return "door " + (probeDoor - 1) + " at " + d.centre.ToString("F1") + " w " + d.width.ToString("F1");
    }
    /// test helper: stand in front of the next door, as far back as the room allows, and report what was measured there
    public string ProbeDoorClose()
    {
        if (doors.Count == 0) return "no doors";
        var d = doors[probeDoor % doors.Count]; probeDoor++;
        Vector3 pass = Vector3.Cross(Vector3.up, d.axis), mid = d.centre + Vector3.up * 1.5f;
        float back = Reach(mid, -pass, 8f), front = Reach(mid, pass, 8f); if (front > back) { pass = -pass; float t = back; back = front; front = t; }
        float dist = Mathf.Clamp(back - 0.6f, 2.2f, 6.5f); Vector3 p = d.centre - pass * dist + Vector3.up * 0.1f;
        var cc = origin.GetComponent<CharacterController>(); if (cc) cc.enabled = false; origin.transform.position = p; if (cc) cc.enabled = true; Physics.SyncTransforms();
        var dk = GetComponent<HunchDesktopFallback>(); if (dk) dk.SetLook(Mathf.Atan2(pass.x, pass.z) * Mathf.Rad2Deg, -6f);
        float top = Reach(d.centre + Vector3.up * 0.5f, Vector3.up, 12f) + 0.5f;
        return "door " + (probeDoor - 1) + " at " + d.centre.ToString("F1") + " w " + d.width.ToString("F1") + " panelH " + d.a.localScale.y.ToString("F1") + " lintel " + top.ToString("F1") + " room " + back.ToString("F1") + "/" + front.ToString("F1") + " viewDist " + dist.ToString("F1");
    }
    public string RocketInfo() { return rocket ? "rocket home " + rocketHome.ToString("F0") + " pad " + padPos.ToString("F0") + " state " + rocketState + " sunElev " + sunElev.ToString("F1") + " sunI " + (sun ? sun.intensity.ToString("F2") : "none") : "no rocket"; }

    // ------------------------------------------------------------------ frame
    void Update()
    {
        if (!ready) return;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.lKey.wasPressedThisFrame && !game.menuOpen && !LaunchRocket() && sim.phase == HabPhase.Mission) sim.Say(rocketOff ? "ARIA: NASA HUNCH mode shows only the main base, so the spaceport and its rocket are switched off." : "ARIA: The rocket is " + RocketWhere + ". It can launch again once it is back on the pad.");
        UpdateSun(); UpdateCameraFx(); UpdateAudio(); UpdateRocket(); UpdateSatellites(); UpdateDoors(); UpdatePackFx();
    }

    void LateUpdate()
    {
        if (ready && shake != Vector3.zero && origin && origin.Camera) origin.Camera.transform.position += origin.Camera.transform.rotation * shake;
    }
}
