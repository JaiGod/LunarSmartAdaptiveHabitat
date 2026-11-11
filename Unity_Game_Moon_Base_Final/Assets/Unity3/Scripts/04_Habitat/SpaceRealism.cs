// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/04_Habitat/SpaceRealism.cs
//  Section : 04_Habitat
//  Folder  : The base itself: zones and their light/sound/temperature/air, dome status lights,
//            doors and hatches, ladders, loose props in 1/6 g, Moon environment, layout export.
//  Purpose : SPACE REALISM — the Moon's environment, ported and corrected from the unity2
//            "HunchWorldFX/HabitatSim" systems (no aliens, no portals, no weapons). Everything is
//            procedural: no audio files or particle packs needed.
//  Why     : Chat: 'VERY VERY REALISTIC … Moon is 1/6 gravity'.
//  Contains: SpaceRealism, Tracker, orbiters, flare
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Hunch3
{
    /// SPACE REALISM — the Moon's environment, ported and corrected from the unity2 "HunchWorldFX/HabitatSim" systems
    /// (no aliens, no portals, no weapons). Everything is procedural: no audio files or particle packs needed.
    ///  • South-pole Sun: circles the horizon once per 29.5-day lunar day at 0–2° elevation, long shadows, lunar-night
    ///    periods when it drops behind the ridges; white light (no air), earthshine fill from Earth.
    ///  • Solar power follows the Sun (vertical arrays rotate to track it), fission covers the night.
    ///  • Vacuum is silent: outside (EVA) the habitat hum is cut, only your suit's breathing and fans are heard.
    ///  • Moon dust: footstep puffs outside, suit dust builds up, dust-lock air knives clean it on the way in.
    ///  • Space weather: solar particle events with a 30-min NOAA/ACE warning → storm shelter; personal dosimeter (mSv),
    ///    Geiger clicks, camera noise when unshielded.
    ///  • Micrometeoroids: impact flashes + dust puffs on the regolith; rare hull strike → LEAK alarm + venting jet.
    ///  • Moonquakes: rare shallow moonquakes (shaking + rumble) and thermal "pings" when the Sun rises or sets.
    ///  • Cargo lander (NASA Apollo LM model) lands and launches at the landing pad with a regolith blast plume.
    ///  • RASSOR excavator robot (NASA model) works the ISRU site; satellites cross the black, non-twinkling starfield;
    ///    Earth link shows the 1.28 s one-way light delay.
    public class SpaceRealism : MonoBehaviour
    {
        public static SpaceRealism I;
        public float SunElevation, SunAzimuth, SolarKW = 12f, DoseToday, DoseMission, DoseRate, SuitDust;
        public bool LunarNight, Sheltered, Outside;
        public string Status = "";

        const float LunarDayHours = 708.7f;              // synodic month
        Light sun, earthshine; float sunAz0; Color ambientBase; bool ambientSet;
        Volume fxVol; Vignette vig; ChromaticAberration chroma; FilmGrain grain;
        AudioSource hum, breath, shots, rumble; AudioLowPassFilter lowpass;
        AudioClip cStep, cHiss, cClick, cThud, cPing, cQuindar;
        ParticleSystem dust, vent, plume; Transform ventAt;
        Vector3 lastFeet; float stepDist, geiger, nextImpact, nextQuake, quakeUntil, quakeAmp, nextSpeRoll, speWarnAt = -1, ventUntil;
        int lastDay = -1; SuitState lastSuit = SuitState.IVA; bool wasNight; float lastHours;
        Vector3 shake; Transform camT;
        // lander
        Transform lander; Vector3 landerHome; int landerState; float landerT; Light landerFlame; float nextLanderHours = 30f;
        // RASSOR
        Transform rassor; Vector3 rassorHome; float rassorT;
        // sats + stars
        readonly List<Transform> sats = new List<Transform>(); ParticleSystem stars;
        // solar trackers
        class Tracker { public Transform t; public Vector3 pivot; public Quaternion rot0; public Vector3 pos0; }
        readonly List<Tracker> trackers = new List<Tracker>(); float trackerAz0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) Make(); };
            if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) Make();
        }
        static void Make() { if (!FindFirstObjectByType<SpaceRealism>()) new GameObject("SpaceRealism").AddComponent<SpaceRealism>(); }
        void Awake() { I = this; }

        void Start()
        {
            var s = Util.Find("Sun_SouthPole"); sun = s ? s.GetComponent<Light>() : FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun) { var to = -sun.transform.forward; sunAz0 = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg; }
            var eg = new GameObject("Earthshine"); earthshine = eg.AddComponent<Light>(); earthshine.type = LightType.Directional;
            earthshine.color = new Color(0.62f, 0.74f, 1f); earthshine.intensity = 0.08f; earthshine.shadows = LightShadows.None;
            var earth = Util.Find("Earth_Horizon"); Vector3 from = earth ? earth.position : new Vector3(-1600, 110, -1100);
            eg.transform.rotation = Quaternion.LookRotation((Vector3.zero - from).normalized);
            SetupFx(); SetupAudio(); SetupParticles(); SetupStars(); SetupSats(); SetupTrackers();
            lander = Util.Find("CargoLander"); if (lander) landerHome = lander.position;
            rassor = Util.Find("NASA_RASSOR"); if (rassor) rassorHome = rassor.position;
            nextImpact = Time.time + Random.Range(20f, 50f); nextQuake = SimClock.hours + Random.Range(30f, 90f); nextSpeRoll = SimClock.hours + Random.Range(20f, 60f);
            lastHours = SimClock.hours;
        }

        // ------------------------------------------------------------------ the Sun at the lunar south pole
        void UpdateSun()
        {
            if (!sun) return;
            float h = SimClock.hours, phase = (h / LunarDayHours) % 1f;
            SunAzimuth = sunAz0 + 360f * phase;
            // Sun skims the horizon (max ~1.6°); a 6-day stretch each lunar day it is below the local ridge line → lunar night
            float el = 1.0f + 0.6f * Mathf.Sin(2 * Mathf.PI * phase);
            float nightK = Mathf.InverseLerp(0.02f, 0f, Mathf.Abs(Mathf.Repeat(phase - 0.62f + 0.5f, 1f) - 0.5f) - 0.085f);   // smooth dip around phase 0.62
            SunElevation = Mathf.Lerp(el, -1.5f, Mathf.Clamp01(nightK));
            LunarNight = SunElevation < 0f;
            float az = SunAzimuth * Mathf.Deg2Rad, e = Mathf.Max(SunElevation, -2f) * Mathf.Deg2Rad;
            // visual elevation is exaggerated ×4 so interiors/terrain still read well, but stays a low, raking light
            float ev = Mathf.Max(SunElevation * 4f, -6f) * Mathf.Deg2Rad;
            var toSun = new Vector3(Mathf.Cos(ev) * Mathf.Sin(az), Mathf.Sin(ev), Mathf.Cos(ev) * Mathf.Cos(az));
            sun.transform.rotation = Quaternion.LookRotation(-toSun);
            float up = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.5f, 0.5f, SunElevation));
            sun.intensity = 2.2f * up;                                   // no atmosphere: the light never reddens, it is just cut off
            earthshine.intensity = Mathf.Lerp(0.18f, 0.06f, up);
            // solar arrays are vertical (low Sun) and track azimuth: output ∝ visibility; night = fission + batteries only
            SolarKW = 14f * up;
            if (wasNight != LunarNight && Time.time > 5f)
            {
                HUD.I?.Toast(LunarNight ? "🌑 Sunset — the Sun has dropped behind the crater rim. Lunar night: solar power off, fission reactor + batteries carry the base. Expect thermal 'pings' as the structure cools."
                                       : "🌅 Sunrise — sunlight back on the arrays. Long shadows: the Sun never rises more than ~1.6° at the south pole.");
                Play(cPing, 0.5f); Invoke(nameof(PingLater), 1.3f); Invoke(nameof(PingLater), 3.1f);
            }
            wasNight = LunarNight;
            // solar trackers follow the Sun's azimuth
            foreach (var tr in trackers)
            {
                if (!tr.t) continue;
                var q = Quaternion.AngleAxis(SunAzimuth - trackerAz0, Vector3.up);
                tr.t.SetPositionAndRotation(tr.pivot + q * (tr.pos0 - tr.pivot), q * tr.rot0);
            }
        }
        void PingLater() { Play(cPing, Random.Range(0.25f, 0.45f)); }

        // ------------------------------------------------------------------ camera effects (alarm vignette, radiation noise, quake shake)
        void SetupFx()
        {
            var go = new GameObject("SpaceRealism_FX_Volume"); fxVol = go.AddComponent<Volume>(); fxVol.isGlobal = true; fxVol.priority = 1100f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>(); fxVol.sharedProfile = p;
            vig = p.Add<Vignette>(true); vig.intensity.Override(0.12f); vig.color.Override(Color.black); vig.smoothness.Override(0.45f);
            chroma = p.Add<ChromaticAberration>(true); chroma.intensity.Override(0f);
            grain = p.Add<FilmGrain>(true); grain.intensity.Override(0f); grain.response.Override(0.6f);
        }
        void UpdateFx(PlayerRig pr)
        {
            bool alarm = Hazards.I && Hazards.I.Active;
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
            Color c = alarm ? new Color(0.7f, 0f, 0f) : Color.black;
            float inten = alarm ? (pr.isXR ? 0.26f : 0.4f) * pulse : 0.12f;
            vig.color.value = Color.Lerp(vig.color.value, c, Time.unscaledDeltaTime * 4f);
            vig.intensity.value = Mathf.Lerp(vig.intensity.value, inten, Time.unscaledDeltaTime * 6f);
            float rad = pr.isXR ? 0f : Mathf.Clamp01(DoseRate / 15f);            // VR comfort: no grain/aberration in the headset
            chroma.intensity.value = Mathf.Lerp(chroma.intensity.value, 0.6f * rad, Time.unscaledDeltaTime * 3f);
            grain.intensity.value = Mathf.Lerp(grain.intensity.value, 0.8f * rad, Time.unscaledDeltaTime * 3f);
        }

        // ------------------------------------------------------------------ procedural sound (from unity2, retuned)
        static AudioClip Clip(string name, float seconds, System.Func<float, float, float> f, bool loop = false)
        {
            int rate = 44100, n = Mathf.RoundToInt(rate * seconds); var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = f(i / (float)rate, i / (float)n);
            if (loop) { int fade = rate / 5; for (int i = 0; i < fade; i++) { float k = i / (float)fade; d[i] = d[i] * k + d[n - fade + i] * (1f - k); } n -= fade; System.Array.Resize(ref d, n); }
            var c = AudioClip.Create(name, n, 1, rate, false); c.SetData(d, 0); return c;
        }
        AudioSource Source(string name, AudioClip clip, bool loop, float vol)
        {
            var go = new GameObject("SFX " + name); go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>(); s.clip = clip; s.loop = loop; s.volume = vol; s.spatialBlend = 0f; s.playOnAwake = false;
            if (loop && clip) s.Play(); return s;
        }
        void SetupAudio()
        {
            var rng = new System.Random(11); float lp = 0f, lp2 = 0f, lr = 0f, lh = 0f;
            var cHum = Clip("habitat fans", 5f, (t, u) => { lp = Mathf.Lerp(lp, (float)rng.NextDouble() * 2f - 1f, 0.02f); return 0.05f * Mathf.Sin(2f * Mathf.PI * 60f * t) + 0.03f * Mathf.Sin(2f * Mathf.PI * 120f * t) + lp * 0.8f; }, true);
            var cBreath = Clip("suit breathing", 4.6f, (t, u) =>
            {
                float env = u < 0.4f ? Mathf.Sin(Mathf.PI * u / 0.4f) : (u > 0.5f && u < 0.92f ? 0.7f * Mathf.Sin(Mathf.PI * (u - 0.5f) / 0.42f) : 0f);
                lp2 = Mathf.Lerp(lp2, (float)rng.NextDouble() * 2f - 1f, u < 0.45f ? 0.22f : 0.1f);
                return lp2 * env * 0.8f + 0.012f * Mathf.Sin(2f * Mathf.PI * 180f * t);
            }, true);
            var cRumble = Clip("ground rumble", 3f, (t, u) => { lr = Mathf.Lerp(lr, (float)rng.NextDouble() * 2f - 1f, 0.015f); return lr * 4f; }, true);
            cStep = Clip("footstep", 0.14f, (t, u) => (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(95f, 45f, u) * t) * 0.8f + ((float)rng.NextDouble() - 0.5f) * 0.3f) * Mathf.Pow(1f - u, 3f));
            cHiss = Clip("airlock hiss", 1.6f, (t, u) => { float w = (float)rng.NextDouble() * 2f - 1f; lh = Mathf.Lerp(lh, w, 0.6f); return (w - lh) * 0.5f * Mathf.Pow(1f - u, 1.5f) * Mathf.Min(1f, u * 30f); });
            cClick = Clip("geiger click", 0.012f, (t, u) => ((float)rng.NextDouble() * 2f - 1f) * Mathf.Pow(1f - u, 2f));
            cThud = Clip("impact thud", 0.6f, (t, u) => (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(70f, 30f, u) * t) + ((float)rng.NextDouble() - 0.5f) * 0.4f) * Mathf.Pow(1f - u, 2.5f));
            cPing = Clip("thermal ping", 0.9f, (t, u) => (Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 2210f * t) * 0.25f) * Mathf.Pow(1f - u, 4f) * 0.4f);
            cQuindar = Clip("quindar", 0.25f, (t, u) => Mathf.Sin(2f * Mathf.PI * 2525f * t) * 0.3f * Mathf.Min(1f, Mathf.Min(u, 1f - u) * 20f));
            hum = Source("fans", cHum, true, 0f); breath = Source("breath", cBreath, true, 0f); rumble = Source("rumble", cRumble, true, 0f);
            shots = Source("one shots", null, false, 1f);
            breath.bypassListenerEffects = true;     // your own breathing is inside the helmet — never muffled
            var listener = FindFirstObjectByType<AudioListener>();
            if (listener) { lowpass = listener.GetComponent<AudioLowPassFilter>(); if (!lowpass) lowpass = listener.gameObject.AddComponent<AudioLowPassFilter>(); lowpass.cutoffFrequency = 22000f; }
        }
        void Play(AudioClip c, float v) { if (shots && c && GameState.timeScale <= 600f) shots.PlayOneShot(c, v); }
        public void Quindar() => Play(cQuindar, 0.22f);

        // ------------------------------------------------------------------ particles built in code
        static Material ParticleMat(Color c, bool additive)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit"); var m = new Material(sh);
            m.SetColor("_BaseColor", c); m.SetFloat("_Surface", 1); m.SetFloat("_Blend", additive ? 2 : 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha); m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000; return m;
        }
        static ParticleSystem MakePS(string name, Material mat, float life, float sMin, float sMax, float zMin, float zMax, float angle, float radius, float gravity, int max = 800)
        {
            var go = new GameObject(name); var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.startLifetime = life; main.startSpeed = new ParticleSystem.MinMaxCurve(sMin, sMax);
            main.startSize = new ParticleSystem.MinMaxCurve(zMin, zMax); main.gravityModifier = gravity; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = max;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = angle; sh.radius = radius;
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }); col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }
        void SetupParticles()
        {
            // regolith is dark grey; with no air, dust flies in clean ballistic arcs at 1/6 g and does not billow
            dust = MakePS("MoonDust", ParticleMat(new Color(0.55f, 0.54f, 0.52f, 0.55f), false), 1.8f, 1.0f, 2.6f, 0.06f, 0.2f, 55f, 0.12f, 1f);
            dust.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            vent = MakePS("VentingJet", ParticleMat(new Color(0.95f, 0.97f, 1f, 0.6f), false), 0.9f, 6f, 11f, 0.08f, 0.35f, 8f, 0.03f, 0f, 1500);
            plume = MakePS("LanderPlume", ParticleMat(new Color(0.6f, 0.58f, 0.55f, 0.6f), false), 2.5f, 8f, 18f, 0.3f, 1.2f, 80f, 2f, 1f, 3000);
            plume.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        }
        void Puff(Vector3 at, int n) { if (!dust) return; dust.transform.position = at; dust.Emit(n); }

        // ------------------------------------------------------------------ stars (no twinkling — there is no air) + satellites
        void SetupStars()
        {
            stars = MakePS("Starfield", ParticleMat(Color.white, true), 1e6f, 0, 0, 1, 1, 0, 0, 0, 2500);
            var m = stars.main; m.loop = false; m.startLifetime = 1e6f; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 2500;
            var col = stars.colorOverLifetime; col.enabled = false;
            var rng = new System.Random(7); var ps = new ParticleSystem.Particle[2200];
            for (int i = 0; i < ps.Length; i++)
            {
                Vector3 d; do d = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1); while (d.sqrMagnitude > 1 || d.sqrMagnitude < 0.01f);
                d.Normalize(); if (d.y < -0.05f) d.y = -d.y;                       // below the horizon is hidden by terrain anyway
                float mag = Mathf.Pow((float)rng.NextDouble(), 3f);
                ps[i].position = d * 2600f; ps[i].startSize = Mathf.Lerp(2.5f, 9f, mag); ps[i].remainingLifetime = 1e6f; ps[i].startLifetime = 1e6f;
                float tint = (float)rng.NextDouble(); ps[i].startColor = Color.Lerp(new Color(0.75f, 0.82f, 1f), new Color(1f, 0.9f, 0.78f), tint) * Mathf.Lerp(0.35f, 1f, mag);
            }
            stars.Play(); stars.SetParticles(ps, ps.Length); stars.Pause();   // frozen: stars do not twinkle without an atmosphere
        }
        void SetupSats()
        {
            // LRO-class orbiters and a relay satellite: sunlit points that rise and set (orbits ~2 h)
            for (int i = 0; i < 3; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(s.GetComponent<Collider>()); s.name = i == 0 ? "LRO (orbiter)" : i == 1 ? "LunaNet relay sat" : "Gateway (NRHO, high)";
                s.transform.localScale = Vector3.one * (i == 2 ? 6f : 4f); s.GetComponent<Renderer>().sharedMaterial = ParticleMat(new Color(1f, 0.97f, 0.9f, 1f), true); sats.Add(s.transform);
            }
        }
        void UpdateSats()
        {
            float t = Time.time;
            for (int i = 0; i < sats.Count; i++)
            {
                float per = i == 2 ? 3000f : 420f + 160f * i, a = (t / per + i * 0.37f) * Mathf.PI * 2f, incl = (i == 2 ? 70f : 88f - 25f * i) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(incl), Mathf.Sin(a) * Mathf.Cos(incl)) * 2400f;
                if (i == 2) p = new Vector3(Mathf.Cos(a) * 900f, 1900f + 300f * Mathf.Sin(a), Mathf.Sin(a) * 900f);
                sats[i].position = p; sats[i].gameObject.SetActive(p.y > 30f);
            }
        }
        void SetupTrackers()
        {
            // arrays: rotate the gimbal + wings of each solar tower about its mast (the Builder leaves them non-static)
            var groups = Util.FindPrefix("P0").Where(x => x.name.Contains("SolarTower_") && (x.name.Contains("Wing") || x.name.Contains("Gimbal"))).GroupBy(x => x.name.Substring(x.name.IndexOf("SolarTower_"), 13));
            foreach (var g in groups)
            {
                var mast = Util.FindPrefix("P0").FirstOrDefault(x => x.name.Contains(g.Key) && x.name.Contains("Mast"));
                Vector3 pivot = mast ? mast.position : g.First().position; pivot.y = 0;
                foreach (var t in g) trackers.Add(new Tracker { t = t, pivot = new Vector3(pivot.x, t.position.y, pivot.z), rot0 = t.rotation, pos0 = t.position });
            }
            trackerAz0 = sunAz0;
        }

        // ------------------------------------------------------------------ cargo lander (NASA Apollo LM model) + RASSOR
        public void CallLander()
        {
            if (!lander || landerState != 0) { HUD.I?.Toast(lander ? "Cargo lander already in flight." : "No cargo lander model in this build."); return; }
            landerState = 1; landerT = 0; HUD.I?.Toast("🚀 Cargo lander: ascent from the landing pad, 300 m from the base. Stay inside the blast berm radius — ejecta travels kilometres in vacuum.");
            Quindar();
        }
        void UpdateLander(Vector3 feet)
        {
            if (!lander) return;
            if (!landerFlame) { var lg = new GameObject("LanderFlame"); landerFlame = lg.AddComponent<Light>(); landerFlame.type = LightType.Point; landerFlame.range = 120f; landerFlame.color = new Color(1f, 0.85f, 0.6f); landerFlame.intensity = 0; }
            if (SimClock.hours > nextLanderHours && landerState == 0 && !(Hazards.I && Hazards.I.Active)) { nextLanderHours = SimClock.hours + 72f; CallLander(); }
            float dt = Time.deltaTime; float thrust = 0;
            switch (landerState)
            {
                case 1: landerT += dt; thrust = 1; lander.position = landerHome + Vector3.up * (0.5f * 4f * landerT * landerT); if (landerT > 22f) { landerState = 2; landerT = 0; HUD.I?.Toast("Cargo lander in orbit — returning with supplies in a few minutes (sim: 2 days)."); } break;
                case 2: landerT += dt; thrust = 0; lander.gameObject.SetActive(false); if (landerT > 90f) { lander.gameObject.SetActive(true); landerState = 3; landerT = 22f; HUD.I?.Toast("Cargo lander on final descent to the pad — 2.1 t of food, water, spare scrubber cartridges and CTBs."); Quindar(); } break;
                case 3: landerT -= dt; thrust = 1; lander.position = landerHome + Vector3.up * Mathf.Max(0, 0.5f * 4f * landerT * landerT); if (landerT <= 0f) { landerState = 0; lander.position = landerHome; HUD.I?.Toast("Touchdown. Cargo ready for unloading by rover."); } break;
            }
            landerFlame.transform.position = lander.position + Vector3.up * 0.5f; landerFlame.intensity = thrust * (8f + 3f * Mathf.PerlinNoise(Time.time * 9f, 0));
            float alt = lander.position.y - landerHome.y;
            if (thrust > 0 && alt < 40f && plume) { plume.transform.position = landerHome + Vector3.up * 0.3f; plume.Emit(Mathf.RoundToInt(60 * dt * 60 * (1f - alt / 40f))); }
            float dist = Vector3.Distance(feet, lander.position);
            rumble.volume = Mathf.Lerp(rumble.volume, thrust > 0 ? Mathf.Clamp01(1f - dist / 900f) * 0.6f : 0f, dt * 3f);
            if (thrust > 0 && dist < 900f) shake += Random.insideUnitSphere * 0.02f * (1f - dist / 900f);   // felt through the ground, not heard through air
        }
        void UpdateRassor()
        {
            if (!rassor) return;
            rassorT += Time.deltaTime * Mathf.Clamp(GameState.timeScale / 60f, 1f, 10f) * 0.05f;
            var p = rassorHome + new Vector3(Mathf.Sin(rassorT) * 6f, 0, Mathf.Sin(rassorT * 0.5f) * 3f);
            var d = p - rassor.position; if (d.sqrMagnitude > 1e-5f) rassor.rotation = Quaternion.Slerp(rassor.rotation, Quaternion.LookRotation(new Vector3(d.x, 0, d.z)), Time.deltaTime * 2f);
            rassor.position = p;
            if (Random.value < Time.deltaTime * 2f) Puff(rassor.position + rassor.forward * 0.8f + Vector3.up * 0.1f, 4);   // bucket drums throw regolith
        }

        // ------------------------------------------------------------------ exterior camera feeds (Command console / desktop F10)
        Camera feedCam; int feedIdx = -1; RenderTexture feedRT; GameObject feedScreen;
        public void NextFeed()
        {
            var targets = new List<(string label, Transform t, Vector3 off)>();
            var sev = Util.Find("NASA_SEV_PressurizedRover"); if (sev) targets.Add(("EXT CAM 1 · Pressurised rover (NASA SEV)", sev, new Vector3(9, 4, 9)));
            if (rassor) targets.Add(("EXT CAM 2 · ISRU dig site — RASSOR excavator", rassor, new Vector3(6, 3, 6)));
            if (lander) targets.Add(("EXT CAM 3 · Landing pad — cargo lander", lander, new Vector3(28, 12, 28)));
            targets.Add(("EXT CAM 4 · Base overview (comm tower)", null, new Vector3(-55, 38, -55)));
            feedIdx++;
            if (feedIdx >= targets.Count) { feedIdx = -1; if (feedCam) feedCam.gameObject.SetActive(false); if (feedScreen) feedScreen.SetActive(false); HUD.I?.Toast("Exterior cameras off."); return; }
            if (!feedCam) { var g = new GameObject("ExteriorFeedCam"); feedCam = g.AddComponent<Camera>(); feedCam.depth = 50; feedCam.farClipPlane = 4000; feedCam.clearFlags = CameraClearFlags.SolidColor; feedCam.backgroundColor = Color.black; feedCam.GetUniversalAdditionalCameraData().renderPostProcessing = true; }
            feedCam.gameObject.SetActive(true);
            var pr = PlayerRig.I;
            if (pr && pr.isXR)
            {   // VR: never take over the headset view — show the feed on a floating monitor in front of you
                if (!feedRT) feedRT = new RenderTexture(1280, 720, 16) { name = "ExteriorFeedRT" };
                feedCam.targetTexture = feedRT;
                if (!feedScreen)
                {
                    feedScreen = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(feedScreen.GetComponent<Collider>()); feedScreen.name = "ExteriorFeedScreen";
                    var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.SetTexture("_BaseMap", feedRT); feedScreen.GetComponent<Renderer>().sharedMaterial = m;
                    feedScreen.transform.localScale = new Vector3(1.28f, 0.72f, 1f);
                }
                feedScreen.SetActive(true);
                var cam = pr.cam.transform; var fwd = cam.forward; fwd.y = 0; fwd.Normalize();
                feedScreen.transform.position = cam.position + fwd * 1.4f; feedScreen.transform.rotation = Quaternion.LookRotation(fwd);
            }
            else feedCam.targetTexture = null;
            var tg = targets[feedIdx]; Vector3 focus = tg.t ? tg.t.position + Vector3.up * 1.5f : Vector3.zero;
            feedCam.transform.position = focus + tg.off; feedCam.transform.LookAt(focus);
            HUD.I?.Toast(tg.label + "   (F10: next camera)");
        }

        // ------------------------------------------------------------------ main loop
        void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f10Key.wasPressedThisFrame && PlayerRig.I) NextFeed();
            var pr = PlayerRig.I; var env = HabitatEnvironment.I; if (!pr || Sim.I == null) return;
            camT = pr.cam ? pr.cam.transform : null;
            float dh = Mathf.Max(0, SimClock.hours - lastHours); lastHours = SimClock.hours;
            UpdateSun(); UpdateSats(); UpdateFx(pr); UpdateRassor();
            Vector3 feet = pr.FeetPosition;
            Outside = pr.suit == SuitState.EVA;
            var zone = env ? env.ZoneAt(feet) : null;
            Sheltered = zone != null && (zone.name.Contains("Medical") || zone.name.Contains("Storm"));

            // --- audio: vacuum silence outside
            if (lowpass) lowpass.cutoffFrequency = Mathf.Lerp(lowpass.cutoffFrequency, Outside ? 450f : 22000f, Time.deltaTime * 2.5f);
            hum.volume = Mathf.Lerp(hum.volume, Outside ? 0f : 0.07f, Time.deltaTime * 2f);
            breath.volume = Mathf.Lerp(breath.volume, Outside ? 0.45f : 0f, Time.deltaTime * 2f);
            if (pr.suit != lastSuit)
            {
                if (pr.suit == SuitState.EVA) { Play(cHiss, 0.5f); HUD.I?.Toast("Outside: no air → no sound. You only hear your own suit (fans, breathing) and what travels through your boots. Dust sticks to everything."); }
                if (lastSuit == SuitState.EVA)
                {
                    Play(cHiss, 0.5f);
                    if (SuitDust > 0.15f) { HUD.I?.Toast($"Dust lock: air knives + brushes strip {SuitDust * 100:0}% suit dust before you enter. Lunar dust is sharp and abrasive — it wears seals and irritates lungs."); Puff(feet + Vector3.up * 1f, 60); }
                    SuitDust = 0;
                }
                lastSuit = pr.suit;
            }
            // --- footsteps + dust
            Vector3 d = feet - lastFeet; lastFeet = feet; float moved = new Vector2(d.x, d.z).magnitude;
            if (moved < 2f) stepDist += moved; else stepDist = 0;
            if (stepDist > (Outside ? 1.6f : 0.9f))
            {
                stepDist = 0; Play(cStep, Outside ? 0.22f : 0.18f);
                if (Outside) { Puff(feet + Vector3.up * 0.05f, 10); SuitDust = Mathf.Min(1f, SuitDust + 0.004f); }
            }
            // --- radiation dosimetry (mSv). Surface ≈ 1.4 mSv/day (Chang'e-4 LND); habitat walls + regolith ≈ ×0.3; storm shelter water wall ≈ ×0.05
            bool spe = Hazards.I && Hazards.I.Active && Hazards.I.Kind == "SPE";
            float baseRate = 1.4f / 24f;
            float shield = Outside ? 1f : Sheltered ? 0.05f : 0.3f;
            DoseRate = (baseRate + (spe ? 40f : 0f)) * shield;              // mSv per sim hour (large SPE unshielded ~40 mSv/h peak)
            DoseToday += DoseRate * dh; DoseMission += DoseRate * dh;
            if (SimClock.Day != lastDay) { lastDay = SimClock.Day; DoseToday = 0; }
            if (DoseRate > 0.2f) { geiger -= Time.deltaTime; if (geiger <= 0f) { geiger = Random.Range(0.02f, Mathf.Lerp(0.6f, 0.05f, Mathf.Clamp01(DoseRate / 15f))); Play(cClick, 0.5f); } }
            // --- space weather: NOAA/ACE warning ~30 sim-min before particles arrive
            if (SimClock.hours > nextSpeRoll && speWarnAt < 0 && !(Hazards.I && Hazards.I.Active))
            {
                nextSpeRoll = SimClock.hours + Random.Range(60f, 140f);
                if (Random.value < 0.6f) { speWarnAt = SimClock.hours + 0.5f; Quindar(); HUD.I?.Toast("☢ SPACE WEATHER ALERT (NOAA SWPC / ACE at L1): X-class flare observed, protons arriving in ~30 min. Finish tasks and head for the storm shelter (Medical node)."); }
            }
            if (speWarnAt > 0 && SimClock.hours >= speWarnAt) { speWarnAt = -1; Hazards.I?.Trigger("SPE", "proton flux > 100 pfu (S3 storm)"); }
            // --- micrometeoroid impact flashes on the regolith (seen from the surface: a flash + spray of ejecta)
            if (Time.time > nextImpact)
            {
                nextImpact = Time.time + Random.Range(35f, 110f);
                Vector3 at = feet + Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward * Random.Range(40f, 220f);
                if (Physics.Raycast(at + Vector3.up * 200f, Vector3.down, out var hit, 400f)) at = hit.point;
                StartCoroutine(Flash(at));
                // very rare: a strike on the habitat itself → leak
                if (Random.value < 0.06f && !(Hazards.I && Hazards.I.Active)) HullStrike();
            }
            if (ventAt && Time.time < ventUntil && Hazards.I && Hazards.I.Active && Hazards.I.Kind == "LEAK") { vent.transform.SetPositionAndRotation(ventAt.position, ventAt.rotation); vent.Emit(Mathf.CeilToInt(200 * Time.deltaTime)); }
            // --- moonquakes: shallow (rare, felt for minutes) — sensors on the hull report structural loads
            if (SimClock.hours > nextQuake && !(Hazards.I && Hazards.I.Active))
            {
                nextQuake = SimClock.hours + Random.Range(80f, 200f); quakeUntil = Time.time + Random.Range(12f, 25f); quakeAmp = Random.Range(0.015f, 0.04f);
                HUD.I?.Toast($"〰 Shallow moonquake detected (M{Random.Range(2.6f, 4.1f):0.0}). The Moon rings like a bell — shaking lasts minutes because the dry crust barely damps it. Structural strain gauges: nominal.");
            }
            if (Time.time < quakeUntil) { shake += Random.insideUnitSphere * quakeAmp * Mathf.Clamp01((quakeUntil - Time.time) / 5f); rumble.volume = Mathf.Max(rumble.volume, 0.35f); if (Random.value < 0.02f) LunarProps.Jolt(quakeAmp * 9f); }   // loose cargo and tools hop in slow motion
            UpdateLander(feet);
            // --- solar power into the ECLSS/power model
            if (env) { env.solarKW = SolarKW; env.powerKW = 40f + SolarKW; }
            // --- status line for the HUD
            Status = $"Sun {SunElevation:0.0}° el · {(LunarNight ? "LUNAR NIGHT" : "day")} · solar {SolarKW:0} kW\nDose {DoseRate:0.00} mSv/h · today {DoseToday:0.00} · mission {DoseMission:0.0} mSv{(Outside ? $" · suit dust {SuitDust * 100:0}%" : "")}\nEarth link: 1.28 s one-way";
        }
        System.Collections.IEnumerator Flash(Vector3 at)
        {
            var lg = new GameObject("ImpactFlash"); var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.95f, 0.85f); l.range = 25f; l.intensity = 30f; lg.transform.position = at + Vector3.up * 0.5f;
            Puff(at, 40);
            var pr = PlayerRig.I; if (pr && Vector3.Distance(pr.FeetPosition, at) < 120f) Play(cThud, 0.25f);     // only through the ground
            for (float t = 0; t < 0.25f; t += Time.deltaTime) { l.intensity = Mathf.Lerp(30f, 0f, t / 0.25f); yield return null; }
            Destroy(lg);
        }
        void HullStrike()
        {
            var hulls = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name.Contains("Hallway_Hull") && !r.name.StartsWith("HOLO_")).ToList();
            if (hulls.Count == 0) return;
            var h = hulls[Random.Range(0, hulls.Count)]; var b = h.bounds;
            var go = new GameObject("BreachPoint"); go.transform.position = b.center + Vector3.up * (b.extents.y * 0.7f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.up); ventAt = go.transform; ventUntil = Time.time + 600f;
            Play(cThud, 0.9f); Play(cHiss, 0.8f);
            Hazards.I?.Trigger("LEAK", $"micrometeoroid strike on {h.name.Split('_')[0]} hallway — hatches closing, patch kit needed");
        }

        void LateUpdate()
        {
            // quake / launch shaking: a small rotational jitter applied after the player script set the camera this frame
            // (desktop only — never shake the headset view in VR, for comfort)
            if (!camT) return;
            var pr = PlayerRig.I;
            if (shake.sqrMagnitude > 1e-8f && pr && !pr.isXR) camT.localRotation = camT.localRotation * Quaternion.Euler(shake.x * 40f, shake.y * 40f, shake.z * 40f);
            shake = Vector3.Lerp(shake, Vector3.zero, Time.deltaTime * 10f);
        }
    }
}
