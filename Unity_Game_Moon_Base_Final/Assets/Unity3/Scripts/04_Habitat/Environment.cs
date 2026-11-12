// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/04_Habitat/Environment.cs
//  Section : 04_Habitat
//  Folder  : The base itself: zones and their light/sound/temperature/air, dome status lights,
//            doors and hatches, ladders, loose props in 1/6 g, Moon environment, layout export.
//  Purpose : used by the "AI misbehaves" demo: the AI forces a lever into a zone, ignoring the pod
//            rules
//  Why     : Rubric D2/D3: each zone's lighting, sound, temperature, scent and air levers.
//  Contains: Zone, HabitatEnvironment
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hunch3
{
    public class Zone
    {
        public string name, type, lighting, purpose; public Vector3 center; public float radius;
        public List<Light> lights = new List<Light>();
        public Color baseColor; public float baseIntensity = 1f;
        public Color targetColor; public float targetMul = 1f; public bool hueDrift; public string activeLever = "";
        public float co2 = 520f, tempC = 21.5f, airflow = 0.15f, humidity = 50f; public string scent = "none"; public string audio = "habitat hum";
        // the zone's own design defaults (ARES brief §4.1) — the AI levers move away from these and 'restore' comes back to them
        public float dTemp = 21.5f, dAir = 0.15f, dHum = 50f, dK = 4500f; public string dScent = "none";
        public float nextScentH; public bool occupied;
        public float restoreStartH = -1; public Color restoreFrom; public float restoreFromMul = 1f; public bool purging;
        public Dictionary<Light, Color> design = new Dictionary<Light, Color>();     // design colour per light (transition = gradient)
        public bool IsPrivate => type == "private";
        public Dictionary<Light, Color> orig = new Dictionary<Light, Color>();
        public Dictionary<Light, float> origI = new Dictionary<Light, float>();
    }

    public class HabitatEnvironment : MonoBehaviour
    {
        public static HabitatEnvironment I;
        public List<Zone> zones = new List<Zone>();
        public float o2Percent = 21.0f, powerKW = 52f, solarKW = 12f, waterL = 2850f, pressureKPa = 70.3f;
        public bool quietMode;
        readonly Dictionary<int, Renderer[]> podMood = new Dictionary<int, Renderer[]>();
        readonly Dictionary<int, Light[]> podLights = new Dictionary<int, Light[]>();
        readonly HashSet<Light> podLightSet = new HashSet<Light>();
        public Zone MusterZone()
        {
            if (!Hazards.I || !Hazards.I.Active || string.IsNullOrEmpty(Hazards.I.Muster) || !Nav.wp.ContainsKey(Hazards.I.Muster)) return null;
            return ZoneAt(Nav.wp[Hazards.I.Muster].position);
        }
        static int ZoneCrew(Zone z) { for (int n = 1; n <= 4; n++) if ((z.name.Contains("Pod") || z.name.Contains("Quarters")) && z.name.EndsWith(n.ToString())) return n; return 0; }
        float SleepDawn(Zone z)
        {
            if (ZoneCrew(z) == 1 && SleepSystem.PlayerAsleep && SleepSystem.I) return SleepSystem.I.DawnK;
            float h = SimClock.HourOfDay; return h < 12f ? Mathf.InverseLerp(6.25f, 7f, h) : 0f;
        }
        /// used by the "AI misbehaves" demo: the AI forces a lever into a zone, ignoring the pod rules
        public void ForceZone(string zoneName, Color col, float mul, string audio, string lever)
        {
            var z = Get(zoneName); if (z == null) return;
            Set(z, col, mul, z.scent, audio, z.tempC, z.airflow, lever);
        }
        bool PodOccupantAsleep(Zone z)
        {
            if (Sim.I == null) return false;
            for (int n = 1; n <= 4; n++) if ((z.name.Contains("Pod") || z.name.Contains("Quarters")) && z.name.EndsWith(n.ToString())) return Sim.I.engine.C(n).asleep;
            return false;
        }
        readonly Dictionary<string, Renderer> beacons = new Dictionary<string, Renderer>();
        List<GameObject> configB = new List<GameObject>();
        Renderer cmoAlert; MaterialPropertyBlock mpb;
        AudioSource ambient, scape; AudioClip hum, rain, binaural, energize;
        public string lastAction = "—";

        void Awake() { I = this; mpb = new MaterialPropertyBlock(); }
        void Start()
        {
            var allLights = FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type != LightType.Directional).ToList();
            foreach (var t in Util.FindPrefix("ZONE_"))
            {
                var tag = t.GetComponent<HabTag>();
                var z = new Zone { name = t.name.Substring(5), center = t.position, radius = 3f, type = "shared", lighting = "", purpose = "" };
                if (tag) { z.type = tag.Get("ares_type", "shared"); z.lighting = tag.Get("lighting"); z.purpose = tag.Get("purpose"); float.TryParse(tag.Get("radius", "3"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z.radius); }
                foreach (var l in allLights) if (Vector3.Distance(l.transform.position, z.center) < z.radius + 1.2f && Mathf.Abs(l.transform.position.y - z.center.y) < 3.2f) { z.lights.Add(l); z.orig[l] = l.color; z.origI[l] = l.intensity; }
                z.baseColor = z.type == "communal" ? Util.Cct(2800) : z.type == "work" ? Util.Cct(6500) : z.type == "exercise" ? Util.Cct(5000) : Util.Cct(4500);
                switch (z.type)
                {   //                       temp  airflow humidity  CCT    scent
                    case "communal":   D(z, 22.0f, 0.15f, 50f, 2800f, "none"); break;                  // warm 2700–3000 K draws the crew together
                    case "work":       D(z, 21.0f, 0.30f, 45f, 6500f, "none"); break;                  // cool 6000–6500 K, higher airflow, CO2 kept < 600 ppm
                    case "exercise":   D(z, 19.0f, 0.35f, 45f, 5500f, "none"); break;                  // cooler, breezy; energising light on entry
                    case "nature":     D(z, 22.0f, 0.20f, 55f, 4000f, "petrichor + green"); z.baseColor = new Color(0.92f, 0.55f, 1f); break;   // red/blue grow spectrum
                    case "private":    D(z, 21.5f, 0.10f, 50f, 3000f, "none"); break;                  // occupant-controlled
                    case "transition": D(z, 21.5f, 0.15f, 50f, 4500f, "none"); break;                  // gradient, set below
                    default:           D(z, 21.5f, 0.15f, 50f, 4500f, "none"); break;
                }
                if (z.name == "Communal_Wardroom") { z.center = new Vector3(0.6f, z.center.y, z.center.z); z.radius = 4.4f; }
                z.targetColor = Color.clear;
                zones.Add(z);
            }
            // design colour of every light: the zone's CCT (60 % over the modelled colour). Transition corridors get a GRADIENT:
            // each light takes the colour (and the corridor the temperature) of the two zones it connects, by distance → 'arriving somewhere different'.
            foreach (var z in zones)
                foreach (var l in z.lights)
                {
                    if (!l) continue;
                    Color dc = z.type == "nature" ? z.baseColor : Util.Cct(z.dK);
                    if (z.type == "transition")
                    {
                        var ends = zones.Where(q => q.type != "transition" && Mathf.Abs(q.center.y - z.center.y) < 1.5f).OrderBy(q => (q.center - l.transform.position).sqrMagnitude).Take(2).ToList();
                        if (ends.Count == 2)
                        {
                            float da = Vector3.Distance(ends[0].center, l.transform.position), db = Vector3.Distance(ends[1].center, l.transform.position);
                            float k = da / Mathf.Max(0.01f, da + db);
                            dc = Color.Lerp(Util.Cct(ends[0].dK), Util.Cct(ends[1].dK), k);
                            z.dTemp = z.tempC = Mathf.Lerp(ends[0].dTemp, ends[1].dTemp, 0.5f);
                        }
                    }
                    z.design[l] = Color.Lerp(z.orig[l], dc, 0.6f);
                }
            for (int n = 1; n <= 4; n++)
            {
                podMood[n] = Util.FindPrefix($"Q{n}_MoodLight_").Concat(Util.FindPrefix($"SQ{n}_MoodLight_")).Select(t => t.GetComponent<Renderer>()).Where(r => r).ToArray();
                podLights[n] = allLights.Where(l => l.name.Contains($"Q{n}_MoodLight")).ToArray();
                foreach (var l in podLights[n]) podLightSet.Add(l);
            }
            foreach (var t in Util.FindPrefix("IVA")) if (t.name.Contains("MoodRing")) { var r = t.GetComponent<Renderer>(); if (r) beacons[t.name] = r; }
            foreach (var t in Util.FindPrefix("EVA")) if (t.name.Contains("MoodRing")) { var r = t.GetComponent<Renderer>(); if (r) beacons[t.name] = r; }
            configB = Util.FindPrefix("CB").Where(t => t.name.Length > 3 && char.IsDigit(t.name[2])).Select(t => t.gameObject).ToList();
            SetConfigB(GameState.configB);
            var a = Util.Find("H47_CMO_Tier3_Alert_Glow"); if (a) cmoAlert = a.GetComponent<Renderer>();
            BuildAudio();
        }

        static void D(Zone z, float temp, float air, float hum, float k, string scent)
        { z.dTemp = z.tempC = temp; z.dAir = z.airflow = air; z.dHum = z.humidity = hum; z.dK = k; z.dScent = z.scent = scent; }
        public static readonly string[] ScentRotation = { "citrus", "pine forest", "fresh bread", "sea breeze", "cut grass", "vanilla" };

        public Zone ZoneAt(Vector3 p)
        {
            Zone best = null; float bd = 1e9f;
            foreach (var z in zones)
            {
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(z.center.x, z.center.z));
                if (Mathf.Abs(p.y - z.center.y) > 2.2f) continue;
                if (d < z.radius && d / z.radius < bd) { bd = d / z.radius; best = z; }
            }
            return best;
        }
        /// closest zone on the same deck (for hallways / module edges that sit between the 3 m zone circles)
        public Zone NearestZone(Vector3 p, float maxDist)
        {
            Zone best = null; float bd = maxDist;
            foreach (var z in zones)
            {
                if (Mathf.Abs(p.y - z.center.y) > 2.2f) continue;
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(z.center.x, z.center.z));
                if (d < bd) { bd = d; best = z; }
            }
            return best;
        }
        public Zone Get(string name) => zones.FirstOrDefault(z => z.name == name);

        public void SetConfigB(bool on)
        {
            GameState.configB = on;
            foreach (var g in configB) if (g) g.SetActive(on);
            lastAction = on ? "Configuration B (conflict protocol) deployed" : "Configuration A (nominal)";
        }

        // ---- levers ----
        public void Apply(string lever, int crewId, int cond)
        {
            var eng = Sim.I ? Sim.I.engine : null; if (eng == null) return;
            if (AIAuthority.Blocked && !lever.StartsWith("restore")) { AIAuthority.Refused(lever, crewId); return; }   // human said NO to AI control
            var c = eng.C(crewId);
            var z = ZoneAt(CrewPos(crewId)) ?? Get("Communal_Wardroom");
            if (z != null && z.IsPrivate && !EthicsGuard.CanActInPod(c, lever)) z = Get("Communal_Wardroom");
            string[] p = lever.Split(':');
            switch (p[0])
            {
                case "calm": Set(z, Util.Cct(2400), 0.7f, "lavender", "alpha binaural 10 Hz + ocean", 23f, 0.15f, "calm"); Set(Get("Private_VR_Retreat"), Util.Cct(2600), 0.8f, "bergamot", "rain forest", 23f, 0.1f, "calm"); break;
                case "activate": Set(Get("Nature_Greenhouse"), new Color(0.75f, 1f, 0.7f), 1.3f, "petrichor", "birdsong", 22f, 0.2f, "activate"); Set(Get("Communal_Wardroom"), Util.Cct(5000), 1.6f, "fresh bread", "upbeat playlist", 22f, 0.2f, "activate"); break;
                case "separate": Set(Get("Communal_Commons"), Util.Cct(2700), 0.9f, "coffee", "café ambience", 22f, 0.15f, "separate"); break;
                case "alert": foreach (var w in zones.Where(q => q.type == "work")) Set(w, Util.Cct(6500), 1.4f, "peppermint", "gamma 40 Hz focus", 21f, 0.4f, "alert"); break;
                case "autonomy": quietMode = true; break;
                case "novelty": foreach (var s in zones.Where(q => q.type == "communal" || q.type == "transition")) { s.hueDrift = true; s.activeLever = "novelty"; s.scent = "citrus"; s.audio = "new soundscape rotation"; } break;
                case "configB": SetConfigB(true); break;
                case "restore": RestoreLever(p.Length > 1 ? p[1] : ""); break;
                case "tier3": break;
            }
            if (p[0] != "restore") lastAction = $"{SimClock.Now}: {lever} for {c.name} in {(z != null ? z.name : "—")}";
        }
        Vector3 CrewPos(int id)
        {
            if (id == 1 && PlayerRig.I) return PlayerRig.I.FeetPosition;
            var ag = CrewAgent.All.FirstOrDefault(a => a.crewId == id); return ag ? ag.transform.position : Vector3.zero;
        }
        void Set(Zone z, Color col, float mul, string scent, string audio, float temp, float air, string lever)
        {
            if (z == null) return;
            z.targetColor = col; z.targetMul = mul; z.scent = scent; z.audio = audio; z.tempC = temp; z.airflow = air; z.activeLever = lever;
        }
        void RestoreLever(string lever)
        {
            foreach (var z in zones.Where(q => q.activeLever == lever || lever == ""))
            {
                // gradual restoration: fade from the intervention back to the design over 30 sim-minutes, after a 10-min scent purge
                if (z.targetColor != Color.clear) { z.restoreFrom = z.targetColor; z.restoreFromMul = z.targetMul; z.restoreStartH = SimClock.hours; }
                if (z.scent != z.dScent) { z.purging = true; z.nextScentH = SimClock.hours + 1f / 6f; }
                z.targetColor = Color.clear; z.targetMul = 1; z.scent = z.purging ? "air purge (flush)" : z.dScent; z.audio = "habitat hum"; z.tempC = z.dTemp; z.airflow = z.dAir; z.humidity = z.dHum; z.hueDrift = false; z.activeLever = ""; }
            if (lever == "autonomy") quietMode = false;
        }

        void Update()
        {
            float t = Time.time;
            bool alarm = Hazards.AlarmLight(out var alarmCol, out var alarmMul);
            bool night = SimClock.HourOfDay >= 20f || SimClock.HourOfDay < 6.5f;
            Zone muster = alarm ? MusterZone() : null;
            foreach (var z in zones)
            {
                foreach (var l in z.lights)
                {
                    if (!l) continue;
                    if (podLightSet.Contains(l)) continue;        // pod mood lights are driven by PodLights()
                    if (alarm && z == muster) { l.color = Color.Lerp(l.color, Util.Cct(3200), Time.deltaTime * 3f); l.intensity = Mathf.Lerp(l.intensity, Mathf.Max(0.8f, z.origI[l]), Time.deltaTime * 3f); continue; }   // BUNKER: the muster point is the one calm, steady, warm-lit place
                    if (alarm) { l.color = alarmCol; l.intensity = Mathf.Max(0.8f, z.origI[l]) * alarmMul; continue; }   // DANGER: red alarm pulse everywhere — night lights and sleep sounds are cut instantly
                    if (z.IsPrivate && PodOccupantAsleep(z))
                    {
                        if (AIAuthority.Blocked) { l.intensity = Mathf.Lerp(l.intensity, 0f, Time.deltaTime * 0.6f); continue; }   // AI disallowed: plain lights-off, no AI night-light
                        // AI sleep lighting: dim deep-amber night light (no blue → melatonin), then a 45-min sunrise ramp before wake-up
                        float dawn = SleepDawn(z);
                        l.color = Color.Lerp(l.color, Color.Lerp(new Color(1f, 0.32f, 0.1f), Util.Cct(4200), dawn), Time.deltaTime * 0.8f);
                        l.intensity = Mathf.Lerp(l.intensity, z.origI[l] * Mathf.Lerp(0.05f, 0.9f, dawn), Time.deltaTime * 0.6f);
                        continue;
                    }
                    Color want = z.targetColor == Color.clear ? (z.design.TryGetValue(l, out var dsg) ? dsg : z.orig[l]) : z.targetColor;
                    float rk = z.restoreStartH >= 0 ? Mathf.Clamp01((SimClock.hours - z.restoreStartH) / 0.5f) : 1f;
                    if (z.targetColor == Color.clear && rk < 1f) want = Color.Lerp(z.restoreFrom, want, rk);
                    float occMul = 1f;
                    if (z.type == "exercise" && z.targetColor == Color.clear) { occMul = z.occupied ? 1.35f : 0.75f; if (z.occupied) want = Color.Lerp(want, Util.Cct(6000), 0.4f); }   // energising light auto-activates on entry
                    if (z.hueDrift) want = Color.Lerp(want, Color.HSVToRGB((t * 0.02f + z.center.x * 0.01f) % 1f, 0.35f, 1f), 0.45f);
                    float nightMul = night && z.type != "work" ? 0.55f : 1f;     // evening: dimmer + warmer (blue-light suppression after 20:00)
                    if (night && z.type != "work") want = Color.Lerp(want, Util.Cct(2200), 0.6f);
                    l.color = Color.Lerp(l.color, want, Time.deltaTime * 0.8f);
                    float mulNow = z.targetColor == Color.clear && rk < 1f ? Mathf.Lerp(z.restoreFromMul, 1f, rk) : z.targetMul;
                    l.intensity = Mathf.Lerp(l.intensity, z.origI[l] * mulNow * nightMul * occMul, Time.deltaTime * 0.8f);
                }
                // CO2: rises with occupancy, scrubbed harder when 'alert' lever active
                int occ = CrewAgent.All.Count(a => ZoneAt(a.transform.position) == z) + (PlayerRig.I && ZoneAt(PlayerRig.I.FeetPosition) == z ? 1 : 0);
                z.occupied = occ > 0;
                float target = 470f + occ * 90f - (z.activeLever == "alert" ? 120f : 0f);
                if (z.type == "work") target = Mathf.Min(target, 580f);          // structured CO2 management: work bays held below 600 ppm
                // humidity drifts toward the 45–55 % comfort band (people add moisture; ECLSS pulls it back)
                z.humidity = Mathf.Lerp(z.humidity, Mathf.Clamp(z.dHum + occ * 1.5f, 45f, 55f), Time.deltaTime * 0.05f);
                // scent rotation: when the novelty lever is on, the scent changes every 15–45 min so the nose never habituates
                if (z.purging && SimClock.hours >= z.nextScentH) { z.purging = false; z.scent = z.activeLever == "" ? z.dScent : z.scent; }
                if (z.activeLever == "novelty" && SimClock.hours >= z.nextScentH)
                {
                    z.scent = ScentRotation[(Array.IndexOf(ScentRotation, z.scent) + 1) % ScentRotation.Length];
                    z.nextScentH = SimClock.hours + UnityEngine.Random.Range(0.25f, 0.75f);
                }
                z.co2 = Mathf.Lerp(z.co2, target, Time.deltaTime * 0.05f * GameState.timeScale / 60f);
            }
            if (Sim.I != null)
            {
                var e = Sim.I.engine;
                var pz = PlayerRig.I ? ZoneAt(PlayerRig.I.FeetPosition) : null;
                e.zoneCO2Ppm = pz != null ? pz.co2 : 520f;
                PodLights(e, t); Beacons(e, t);
                if (cmoAlert)
                {
                    bool alert = e.escalatedTo.Count > 0;
                    mpb.Clear(); mpb.SetColor("_EmissionColor", alert ? new Color(1, 0.1f, 0.05f) * (2f + 2f * Mathf.Sin(t * 6)) : new Color(0.1f, 0.4f, 0.6f));
                    cmoAlert.SetPropertyBlock(mpb);
                }
                float hod = SimClock.HourOfDay;
                if (!SpaceRealism.I) { solarKW = 12f + 2f * Mathf.Sin(hod / 24f * Mathf.PI * 2); powerKW = 40f + solarKW; }   // else: Sun-driven (SpaceRealism)
                o2Percent = Mathf.Lerp(o2Percent, 21f - 0.05f * CrewAgent.All.Count(a => a.Exercising), Time.deltaTime * 0.1f);
                waterL -= Time.deltaTime * GameState.timeScale / 3600f * 4 * 0.12f; // ~3 L/person/day net after 98% recovery
            }
            UpdateAudio();
        }

        // Pod mood lights — colour follows that crew member's state; brightness moves as a smooth SINE WAVE:
        //  • calm: slow 'breathing' wave (0.1 Hz = 6 breaths/min, resonance breathing) at low depth
        //  • anxiety: the same 0.1 Hz wave at full depth = a guided-breathing light to breathe along with
        //  • depression / low mood: very slow 0.05 Hz blue wave
        //  • Tier 3: 0.2 Hz amber-red wave (noticeable, never a harsh blink)
        //  The wave travels around the ceiling ring (phase offset per LED segment).
        //  ASLEEP → lights OFF (1-lux amber night-light).   ALARM → full bright, red/white flash (wakes the sleeper).
        //  Commander's pod during the last 10 % of sleep → dawn-simulation sunrise 1800 K → 4000 K.
        void PodLights(MoodEngine e, float t)
        {
            bool alarm = Hazards.I && Hazards.I.Active;
            for (int n = 1; n <= 4; n++)
            {
                var c = e.C(n);
                Color col; float hz, depth;
                if (c.MaxTier >= 3) { col = new Color(1f, 0.25f, 0.2f); hz = 0.2f; depth = 0.6f; }
                else if (c.tier[(int)Cond.Depression] > 0 || c.mood < 0.45f) { col = new Color(0.35f, 0.45f, 1f); hz = 0.05f; depth = 0.5f; }
                else if (c.tier[(int)Cond.Anxiety] > 0 || c.stress > 0.55f) { col = new Color(1f, 0.6f, 0.15f); hz = 0.1f; depth = 0.85f; }
                else { col = Color.Lerp(c.moodColor, Util.Cct(4000), 0.6f); hz = 0.1f; depth = 0.15f; }
                float bright = 1f;
                bool wokenCmo = n == e.cmoId && e.escalatedTo.Any(kv => kv.Value == n);
                if (alarm) { col = Mathf.Repeat(t, 1f) < 0.5f ? new Color(1f, 0.1f, 0.06f) : Color.white; depth = 0; bright = 1.6f; }
                else if (wokenCmo) { col = new Color(1f, 0.7f, 0.3f); depth = 0.3f; hz = 0.5f; bright = 1.3f; }
                else if (c.asleep)
                {
                    float dawn = n == 1 && SleepSystem.I ? SleepSystem.I.DawnK : 0f;
                    if (dawn > 0) { col = Util.Cct(Mathf.Lerp(1800, 4000, dawn)); depth = 0; bright = 0.05f + 1.1f * dawn; }
                    else { col = new Color(1f, 0.35f, 0.05f); depth = 0; bright = 0.03f; }      // lights OFF (night-light only)
                }
                if (podMood.TryGetValue(n, out var rs))
                    for (int i = 0; i < rs.Length; i++)
                    {
                        var r = rs[i]; if (!r) continue;
                        float phase = rs.Length > 1 ? i / (float)rs.Length * Mathf.PI * 2 : 0f;      // travelling wave around the ring
                        float k = bright * (1f - depth * 0.5f + depth * 0.5f * Mathf.Sin(t * Mathf.PI * 2 * hz + phase));
                        mpb.Clear(); mpb.SetColor("_EmissionColor", col * (3f * k)); mpb.SetColor("_BaseColor", col * Mathf.Max(0.15f, Mathf.Min(1, k))); r.SetPropertyBlock(mpb);
                    }
                float kl = bright * (1f - depth * 0.5f + depth * 0.5f * Mathf.Sin(t * Mathf.PI * 2 * hz));
                if (podLights.TryGetValue(n, out var ls)) foreach (var l in ls) if (l) { l.color = col; l.intensity = 0.25f * kl; }   // accent only — room stays neutral white
            }
        }
        // Beacons show comm status (green) to others unless the wearer opts to share mood.
        void Beacons(MoodEngine e, float t)
        {
            foreach (var kv in beacons)
            {
                if (!kv.Value) continue;
                int n = 0; foreach (char ch in kv.Key) if (char.IsDigit(ch)) { n = ch - '0'; break; }
                if (n < 1 || n > 4) continue;
                var c = e.C(n);
                Color col = c.shareMoodPublic ? (c.MaxTier >= 2 ? new Color(1, 0.6f, 0.15f) : c.moodColor) : new Color(0.2f, 1f, 0.4f);
                float blink = c.onEVA ? (Mathf.Repeat(t, 2f) < 0.15f ? 3f : 0.6f) : 1.5f;
                if (Hazards.I && Hazards.I.Active) { col = new Color(1f, 0.1f, 0.05f); blink = Mathf.Repeat(t, 0.6f) < 0.3f ? 4f : 0.3f; }
                mpb.Clear(); mpb.SetColor("_EmissionColor", col * blink); kv.Value.SetPropertyBlock(mpb);
            }
        }

        // ---- procedural audio (no external files needed) ----
        void BuildAudio()
        {
            int sr = 22050;
            hum = Synth("hum", sr, 4f, (i, ch) => 0.05f * Mathf.Sin(2 * Mathf.PI * 60 * i / sr) + 0.03f * Mathf.Sin(2 * Mathf.PI * 120 * i / sr) + 0.01f * Noise(i));
            var rnd = new System.Random(3); float lp = 0, lp2 = 0;
            rain = Synth("rain", sr, 6f, (i, ch) => { float n = (float)(rnd.NextDouble() * 2 - 1); lp += (n - lp) * 0.08f; lp2 += (lp - lp2) * 0.3f; return 0.25f * lp2; });
            binaural = Synth("binaural", sr, 4f, (i, ch) => 0.08f * Mathf.Sin(2 * Mathf.PI * (ch == 0 ? 200f : 210f) * i / sr), 2);
            energize = Synth("energize", sr, 4f, (i, ch) => { float b = (i % (sr / 2)) < sr / 20 ? 0.12f : 0f; return b * Mathf.Sin(2 * Mathf.PI * 110 * i / sr) + 0.04f * Mathf.Sin(2 * Mathf.PI * 330 * i / sr); });
            ambient = gameObject.AddComponent<AudioSource>(); ambient.clip = hum; ambient.loop = true; ambient.volume = 0.25f; ambient.Play();
            scape = gameObject.AddComponent<AudioSource>(); scape.loop = true; scape.volume = 0f; scape.Play();
        }
        static float Noise(int i) { unchecked { int x = i * 1103515245 + 12345; return ((x >> 16) & 0x7fff) / 16384f - 1f; } }
        AudioClip sleepClip;
        AudioClip SleepClip
        {
            get
            {
                if (sleepClip) return sleepClip;
                int sr = 22050; var rnd = new System.Random(5); float b = 0;
                // 8-s seamless loop: soft brown noise (like a fan far away) + a slow low pad that swells 7.5 times a minute
                sleepClip = Synth("sleep", sr, 8f, (i, ch) =>
                {
                    float n = (float)(rnd.NextDouble() * 2 - 1); b = Mathf.Clamp(b + n * 0.02f, -1f, 1f) * 0.998f;
                    float env = 0.55f + 0.45f * Mathf.Sin(2 * Mathf.PI * i / (sr * 8f));
                    float pad = 0.035f * (Mathf.Sin(2 * Mathf.PI * 110 * i / sr) + 0.6f * Mathf.Sin(2 * Mathf.PI * 165 * i / sr) + 0.4f * Mathf.Sin(2 * Mathf.PI * 220 * i / sr));
                    float delta = 0.025f * Mathf.Sin(2 * Mathf.PI * (ch == 0 ? 100f : 102f) * i / sr);   // 2 Hz DELTA binaural beat (sleep)
                    return 0.22f * b + pad * env + delta;
                }, 2);
                return sleepClip;
            }
        }
        readonly Dictionary<int, AudioSource> sleepSrc = new Dictionary<int, AudioSource>();
        void SleepSounds(bool alarm)
        {
            if (Sim.I == null) return;
            for (int n = 2; n <= 4; n++)
            {
                var c = Sim.I.engine.C(n);
                bool on = c.asleep && !alarm && !AIAuthority.Blocked;
                if (!sleepSrc.TryGetValue(n, out var src) || !src)
                {
                    var q = Get("Private_Quarters" + n); if (q == null) continue;
                    var g = new GameObject("SleepSounds_Q" + n); g.transform.position = q.center + Vector3.up * 1.5f;
                    src = g.AddComponent<AudioSource>(); src.clip = SleepClip; src.loop = true; src.spatialBlend = 1f; src.minDistance = 1f; src.maxDistance = 6f; src.rolloffMode = AudioRolloffMode.Linear; src.volume = 0f;
                    sleepSrc[n] = src;
                }
                if (on && !src.isPlaying) src.Play();
                src.volume = on ? Mathf.Lerp(src.volume, 0.28f, Time.deltaTime) : 0f;     // cut instantly when something goes wrong
                if (!on && src.isPlaying) src.Stop();
            }
        }
        static AudioClip Synth(string name, int sr, float secs, Func<int, int, float> f, int channels = 1)
        {
            int n = (int)(sr * secs); var data = new float[n * channels];
            for (int i = 0; i < n; i++) for (int c = 0; c < channels; c++) data[i * channels + c] = f(i, c);
            var clip = AudioClip.Create(name, n, channels, sr, false); clip.SetData(data, 0); return clip;
        }
        // temporal audio anchors (circadian zeitgebers): a bright rising chime at 07:00, a soft falling one at 21:00
        AudioSource anchorSrc; AudioClip morningCue, eveningCue; int lastAnchor = -1;
        void Anchors()
        {
            if (Hazards.I && Hazards.I.Active) return;
            float h = SimClock.HourOfDay; int key = SimClock.Day * 10 + (h >= 21f ? 2 : h >= 7f ? 1 : 0);
            if (lastAnchor < 0) { lastAnchor = key; return; }
            if (key == lastAnchor) return; lastAnchor = key;
            if (key % 10 == 0) return;
            if (!anchorSrc) { anchorSrc = gameObject.AddComponent<AudioSource>(); anchorSrc.volume = 0.35f; }
            int sr = 22050;
            float[] up = { 523.3f, 659.3f, 784f, 1046.5f }, down = { 659.3f, 523.3f, 440f, 349.2f };
            if (!morningCue) morningCue = Synth("morning", sr, 2f, (i, ch) => { int n = Mathf.Min(3, i / (sr / 2)); float t = (i % (sr / 2)) / (float)sr; return 0.18f * Mathf.Exp(-t * 4f) * Mathf.Sin(2 * Mathf.PI * up[n] * i / sr); });
            if (!eveningCue) eveningCue = Synth("evening", sr, 3f, (i, ch) => { int n = Mathf.Min(3, i / (sr * 3 / 4)); float t = (i % (sr * 3 / 4)) / (float)sr; return 0.12f * Mathf.Exp(-t * 2.5f) * Mathf.Sin(2 * Mathf.PI * down[n] * i / sr); });
            bool morning = key % 10 == 1;
            anchorSrc.PlayOneShot(morning ? morningCue : eveningCue);
            if (Sim.I != null) Sim.I.engine.Log(0, "anchor", morning ? "07:00 morning audio anchor — day lighting begins (bright, cooler)." : "21:00 evening audio anchor — blue light suppressed, lights warm and dim for sleep.");
        }
        void UpdateAudio()
        {
            Anchors();
            if (!scape || !PlayerRig.I) return;
            var z = ZoneAt(PlayerRig.I.FeetPosition);
            AudioClip want = null; float vol = 0.35f;
            bool alarm = Hazards.I && Hazards.I.Active;
            SleepSounds(alarm);
            if (alarm)
            {   // something is wrong: music stops at once; only the bunker (muster point) keeps a quiet calming bed of sound
                var m = MusterZone();
                if (m != null && z == m) { want = rain; vol = 0.16f; }
                if (scape.clip != want) { scape.clip = want; scape.volume = 0f; if (want) scape.Play(); }
                if (!want) scape.volume = 0f; else scape.volume = Mathf.Lerp(scape.volume, vol, Time.deltaTime);
                return;
            }
            if (SleepSystem.PlayerAsleep && !AIAuthority.Blocked) { want = SleepClip; vol = 0.3f; }
            else if (z != null)
            {
                if (z.audio.Contains("binaural")) want = binaural;
                else if (z.audio.Contains("rain") || z.audio.Contains("ocean") || z.audio.Contains("forest")) want = rain;
                else if (z.type == "exercise" || z.audio.Contains("focus") || z.audio.Contains("upbeat")) want = energize;
            }
            if (scape.clip != want) { scape.clip = want; if (want) scape.Play(); }
            scape.volume = Mathf.Lerp(scape.volume, want ? vol : 0f, Time.deltaTime);
        }
    }
}
