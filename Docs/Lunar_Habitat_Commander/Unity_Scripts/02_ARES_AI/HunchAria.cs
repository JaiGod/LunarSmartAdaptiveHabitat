// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/02_ARES_AI/HunchAria.cs
//  Section : 02_ARES_AI
//  Folder  : Project ARES AI: ARIA the habitat AI, the shared mood/sensor engine, design pages
//            D1–D6, the D4 ethics report and the threshold check.
//  Purpose : ARIA, the habitat AI. Three jobs, matching the proposal's "AI psychological monitoring
//            with adaptive interventions": 1. FORECAST - watches every astronaut's wellbeing trend
//            and predicts who will slip into "Underlying issue" and when
//  Why     : Proposal: AI psychological monitoring with adaptive interventions.
//  Contains: HunchAria
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using UnityEngine;

/// ARIA, the habitat AI. Three jobs, matching the proposal's "AI psychological monitoring with adaptive interventions":
///  1. FORECAST  - watches every astronaut's wellbeing trend and predicts who will slip into "Underlying issue" and when
///  2. ADVISE    - ranks what the commander should do next (alerts first, then crew, then systems) and says why
///  3. TALK      - answers questions in plain language, and gives each astronaut something to say that reflects how they really are
/// It is a rule-and-trend system (no cloud service, no language model), so it works offline and always gives the same answer for the same situation.
public class HunchAria : MonoBehaviour
{
    public bool autoCare;                         // optional helper: ARIA sends a service robot to do routine care by itself (easier game)
    public string hintZone = "";                  // a zone an astronaut asked for while talking
    public int talks;
    public int LastZone { get; private set; } = -1;   // where the current advice sends you

    HabitatGame game; HabitatSim sim;
    const int N = 24; float[][] hist; int head, filled; float sampleAt, autoAt = 20f, hintUntil; float[] talkReady;
    int factIndex, jokeIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var g = FindFirstObjectByType<HabitatGame>();
        if (g && !g.GetComponent<HunchAria>()) g.gameObject.AddComponent<HunchAria>();
    }

    void Awake()
    {
        game = GetComponent<HabitatGame>(); sim = game.sim;
        hist = new float[sim.crew.Count][]; for (int i = 0; i < hist.Length; i++) hist[i] = new float[N];
        talkReady = new float[sim.crew.Count];
    }

    void Update()
    {
        if (sim.phase != HabPhase.Mission) { game.advisorLine = ""; LastZone = -1; return; }
        // one wellbeing sample per game hour
        if (!sim.timeFrozen && sim.missionSeconds >= sampleAt)
        {
            sampleAt = sim.missionSeconds + 1f;
            for (int k = 0; k < hist.Length; k++) hist[k][head] = sim.crew[k].Wellbeing;
            head = (head + 1) % N; filled = Mathf.Min(N, filled + 1);
        }
        int zone; string why; Recommend(out zone, out why); LastZone = zone;
        string f = Forecast();
        game.advisorLine = "ARIA advises: " + why + (f.Length > 0 ? "\nARIA forecast: " + f : "");

        if (autoCare && !sim.timeFrozen && !game.menuOpen && sim.alerts.Count == 0 && sim.missionSeconds >= autoAt && zone >= 0)
        {
            autoAt = sim.missionSeconds + 14f;
            string id = game.zones[zone].id; float cd; sim.cooldown.TryGetValue(id, out cd);
            if (cd <= 0f && id != "power" && id != "storage") { sim.Interact(id); sim.Say("ARIA auto-care: service robot sent to " + game.zones[zone].title + ". " + sim.message.Replace("ARIA: ", "")); }
        }
    }

    // ------------------------------------------------------------------ 1. forecast
    /// Wellbeing change per game hour over the last day, from a least-squares line through the samples.
    public float Trend(int k)
    {
        if (filled < 8) return 0f;
        float sx = 0f, sy = 0f, sxy = 0f, sxx = 0f; int n = filled;
        for (int i = 0; i < n; i++) { float y = hist[k][(head - n + i + N * 2) % N]; sx += i; sy += y; sxy += i * y; sxx += i * i; }
        float d = n * sxx - sx * sx; return Mathf.Abs(d) < 0.001f ? 0f : (n * sxy - sx * sy) / d;
    }

    /// Hours until this astronaut is predicted to drop into the next worse class, or -1 if they are steady or improving.
    public float HoursToDrop(int k)
    {
        float w = sim.crew[k].Wellbeing, t = Trend(k);
        if (t > -0.08f) return -1f;
        float limit = w >= 60f ? 60f : (w >= 42f ? 42f : 0f);
        return limit <= 0f ? -1f : (w - limit) / -t;
    }

    public string Forecast()
    {
        int who = -1; float best = 72f;
        for (int k = 0; k < sim.crew.Count; k++) { float h = HoursToDrop(k); if (h >= 0f && h < best) { best = h; who = k; } }
        if (who < 0) return filled < 8 ? "learning the crew's baselines." : "crew wellbeing is steady for the next 3 days.";
        var c = sim.crew[who];
        return c.name + " is trending down and could reach \"" + (c.Wellbeing >= 60f ? "Underlying issue" : "Suspected illness") + "\" in about " + Mathf.Max(1, Mathf.RoundToInt(best)) + " h. Best for them: " + sim.zoneTitle(c.favourite) + ".";
    }

    // ------------------------------------------------------------------ 2. advice
    bool Ready(string zone) { float cd; sim.cooldown.TryGetValue(zone, out cd); return cd <= 0f; }

    public void Recommend(out int zone, out string why)
    {
        zone = -1; why = "";
        if (sim.phase == HabPhase.Commissioning) { zone = game.UrgentZone(); why = "commission " + (zone >= 0 ? game.zones[zone].title : "the next zone") + " next. NASA's order keeps dusty areas away from clean ones."; return; }
        if (sim.phase != HabPhase.Mission) return;
        if (sim.alerts.Count > 0)
        {
            HabAlert a = null; foreach (var x in sim.alerts) if (a == null || x.secondsLeft < a.secondsLeft) a = x;
            zone = game.UrgentZone();
            why = a.needsPart && sim.parts <= 0 ? "get a spare part from " + (zone >= 0 ? game.zones[zone].title : "Storage") + " first, then fix \"" + a.title + "\"."
                : "go to " + sim.zoneTitle(a.zoneId) + " now: " + a.title + " (" + Mathf.Max(0, Mathf.CeilToInt(a.secondsLeft)) + " s left).";
            return;
        }
        if (hintZone.Length > 0 && Time.time < hintUntil && Ready(hintZone)) { zone = game.ZoneIndex(hintZone); why = "an astronaut asked for " + sim.zoneTitle(hintZone) + ". Listening to the crew is the best medicine."; return; }

        // score every routine action by how much it is needed right now, pick the most useful one that is ready
        string best = ""; float bs = 0f; string reason = "";
        System.Action<string, float, string> consider = (id, score, r) => { if (score > bs && Ready(id) && game.ZoneIndex(id) >= 0) { bs = score; best = id; reason = r; } };
        float stress = 0f, mood = 100f, sleep = 100f, health = 100f; string sWho = "", mWho = "", hWho = "";
        foreach (var c in sim.crew)
        {
            if (c.stress > stress) { stress = c.stress; sWho = c.name; }
            if (c.mood < mood) { mood = c.mood; mWho = c.name; }
            if (c.health < health) { health = c.health; hWho = c.name; }
            sleep = Mathf.Min(sleep, c.sleep);
        }
        consider("eclss", Mathf.Max(92f - sim.oxygen, 80f - sim.water) * 2.2f, "service the air and water loops at ECLSS. Oxygen " + Mathf.RoundToInt(sim.oxygen) + "%, water " + Mathf.RoundToInt(sim.water) + "%.");
        consider("airlock", (sim.dust - 14f) * 2.2f, "run a dust cycle at the Airlock. Dust is at " + Mathf.RoundToInt(sim.dust) + "% and it harms lungs and equipment.");
        consider("greenhouse", (62f - sim.food) * 1.6f + 6f, sim.food < 62f ? "tend the Greenhouse. Food is at " + Mathf.RoundToInt(sim.food) + "%." : "tend the Greenhouse. Plants add food, oxygen and a better mood.");
        if (sim.parts <= 0) consider(sim.storageStock > 0 ? "storage" : "fab", 30f, "pick up a spare part so you are ready for the next breakdown.");
        if (sim.LunarNight && !sim.fissionOn) consider("power", 80f, "start the fission backup. The Sun is down for 9 days and storage only lasts 8.");
        else if (sim.battery < 35f && !sim.fissionOn) consider("power", 60f, "start the fission backup. Battery is at " + Mathf.RoundToInt(sim.battery) + "%.");
        consider("sanctuary", (stress - 30f) * 1.5f, "run a VR relaxation session in the Sanctuary. " + sWho + "'s stress is " + Mathf.RoundToInt(stress) + ".");
        consider("control", sim.commsOk ? (68f - mood) * 1.5f : 0f, "open a family call from Mission Control. " + mWho + "'s mood is " + Mathf.RoundToInt(mood) + ".");
        consider("galley", (74f - mood) * 1.0f + 4f, "hold a shared crew meal in the Galley. Eating together lifts everyone.");
        consider("quarters", (70f - sleep) * 1.4f, "tune the sleep lighting in Crew Quarters. Someone's sleep score is " + Mathf.RoundToInt(sleep) + ".");
        consider("medical", (88f - health) * 1.5f, "do a health check in Medical. " + hWho + "'s health is " + Mathf.RoundToInt(health) + ".");
        consider("exercise", (stress - 22f) * 0.8f + 5f, "lead an exercise session. It protects muscles and bones in low gravity and lowers stress.");
        consider("observation", 4f, "take the crew to the Observation deck to look at Earth.");
        if (best.Length == 0) { why = "everything is looked after. Explore, collect stars, or talk to the crew."; return; }
        zone = game.ZoneIndex(best); why = reason;
    }

    // ------------------------------------------------------------------ 3. talking
    public string Advice() { int z; string w; Recommend(out z, out w); return sim.phase == HabPhase.Briefing ? "Open the menu and start the mission. I will guide you from there." : "My advice: " + w; }

    public string StatusReport()
    {
        if (sim.phase == HabPhase.Briefing) return "The habitat is ready and the crew of four is waiting for you, Commander.";
        string power = sim.Blackout ? "We have lost power." : (sim.LunarNight ? (sim.fissionOn ? "It is lunar night and the fission backup is carrying us." : "It is lunar night and we are running on stored power only.") : "The solar arrays are in sunlight.");
        return "Day " + sim.Day + " of 30. Oxygen " + Mathf.RoundToInt(sim.oxygen) + " percent, water " + Mathf.RoundToInt(sim.water) + ", food " + Mathf.RoundToInt(sim.food) + ", battery " + Mathf.RoundToInt(sim.battery) + ", dust " + Mathf.RoundToInt(sim.dust) + ". " + power
            + (sim.alerts.Count == 0 ? " No open alerts." : " " + sim.alerts.Count + (sim.alerts.Count == 1 ? " open alert." : " open alerts.")) + " Score so far " + Mathf.RoundToInt(sim.Overall()) + " out of 100.";
    }

    public string CrewReport()
    {
        var sb = new System.Text.StringBuilder(); int worst = 0;
        for (int k = 0; k < sim.crew.Count; k++) if (sim.crew[k].Wellbeing < sim.crew[worst].Wellbeing) worst = k;
        foreach (var c in sim.crew) sb.Append(c.name).Append(": ").Append(HabitatSim.ClassName(c.Class)).Append(", wellbeing ").Append(Mathf.RoundToInt(c.Wellbeing)).Append(". ");
        var w = sim.crew[worst];
        sb.Append(w.Wellbeing >= 60f ? "Everyone is in the healthy range." : w.name + " needs the most support. Try " + sim.zoneTitle(w.favourite) + ".");
        string f = Forecast(); if (sim.phase == HabPhase.Mission) sb.Append(" Forecast: ").Append(f);
        return sb.ToString();
    }

    public string ZoneInfo()
    {
        if (game.NearZone < 0) return "You are between zones. Walk into a glowing ring, or ask me to take you somewhere.";
        var z = game.zones[game.NearZone]; string p = sim.Prompt(z.id);
        return "This is " + z.title + ". " + z.description + (p.Length > 0 ? " Right now you can: " + p + "." : "");
    }

    static readonly string[] Facts = {
        "The Moon has no air, so sound cannot travel outside. You would only hear your own suit and your own breathing.",
        "A day and night on the Moon last about 29 Earth days. That is why this habitat needs a nuclear backup for the long night.",
        "Moon gravity is one sixth of Earth's. You could jump about six times higher here.",
        "This habitat is covered in lunar soil called regolith. A thick layer blocks radiation from the Sun and from deep space.",
        "The lunar south pole has water ice in craters that have not seen sunlight for billions of years.",
        "Astronauts lose muscle and bone in low gravity, so they exercise about two hours every day.",
        "Moon dust is sharp like broken glass. That is why the airlock and the hygiene room are right next to each other.",
        "A radio message takes about 1.3 seconds to travel from the Moon to Earth.",
        "Plants help astronauts twice: they make oxygen and food, and caring for them lowers stress.",
        "On long missions, feeling lonely is as real a risk as a broken machine. That is why I watch mood and sleep as well as oxygen." };
    static readonly string[] Jokes = {
        "Why did the Sun go to school? To get a little brighter.",
        "How do you organise a space party? You planet.",
        "What is an astronaut's favourite key on the keyboard? The space bar.",
        "Why did the cow go to the Moon? To see the Milky Way.",
        "How does the Moon cut its hair? Eclipse it." };
    public string Fact() { return Facts[factIndex++ % Facts.Length]; }
    public string Joke() { return Jokes[jokeIndex++ % Jokes.Length]; }

    public string Comfort() { return "Thank you for telling me. Feeling worried is normal, even for astronauts. Try this with me: breathe in slowly for four seconds, hold for four, and out for four. You are doing a good job, Commander, and you are not alone up here."; }

    // ---- what an astronaut says. Everything is built from their real numbers, so talking to the crew is a way to read the simulation.
    static readonly string[] Jobs = {
        "I am the mission commander. I keep the four of us working as a team, and I decide when it is safe to go outside. When it gets hard I go up to the Observation deck and look at Earth.",
        "I am the flight surgeon. I track everyone's heart rate, sleep and mood with ARIA. Medical is at the far end of the habitat, away from the dusty airlock, so it stays clean. The greenhouse is where I relax.",
        "I am the systems engineer. I keep the air, water and power running. Every important machine has a backup, and what we cannot carry we 3D-print. After a long repair I use the VR Sanctuary to unwind.",
        "I am the geologist. I study the regolith and the ice at the south pole. The same regolith is piled over our roof as a radiation shield. I miss my family, so calls home from Mission Control mean a lot to me." };

    public string CrewHello(int k) { var c = sim.crew[k]; return "Hi Commander! " + (c.Class == CrewClass.Neutral ? "Good to see you." : "I am glad you came by."); }

    public string CrewFeeling(int k)
    {
        var c = sim.crew[k]; var sb = new System.Text.StringBuilder();
        sb.Append(c.Class == CrewClass.Neutral ? "I am doing well, thanks for asking." : (c.Class == CrewClass.UnderlyingIssue ? "I am a bit worn down, to tell you the truth." : "I am really struggling today."));
        if (c.stress > 55f) sb.Append(" I feel very tense, and my heart rate is up at ").Append(c.HeartRate).Append(".");
        else if (c.stress > 35f) sb.Append(" I am a little stressed.");
        if (c.sleep < 45f) sb.Append(" I have not been sleeping well.");
        if (c.mood < 50f) sb.Append(" I miss home.");
        if (c.health < 65f) sb.Append(" My body does not feel right either.");
        if (sim.stormActive) sb.Append(" This solar storm has me on edge.");
        else if (sim.LunarNight && c.mood < 70f) sb.Append(" The long lunar night is getting to me a little.");
        if (c.Class == CrewClass.Neutral && c.stress <= 35f) sb.Append(" Sleep is good, mood is good. Thank you for checking on me.");
        return sb.ToString();
    }

    public string CrewNeeds(int k)
    {
        var c = sim.crew[k]; string fav = sim.zoneTitle(c.favourite);
        hintZone = c.favourite; hintUntil = Time.time + 90f;
        if (c.health < 60f) { hintZone = "medical"; return "I think I should be looked at in Medical. After that, " + fav + " always helps me."; }
        if (c.sleep < 45f) { hintZone = "quarters"; return "I need proper sleep. Could you tune the lighting in Crew Quarters? And " + fav + " helps me most of all."; }
        if (c.stress > 45f || c.mood < 55f) return "What helps me most is " + fav + ". Could you set that up for me?";
        return "I am fine right now. My favourite thing is " + fav + ", if you want to do something nice for the crew.";
    }

    public string CrewJob(int k) { return Jobs[k % Jobs.Length]; }

    /// Talking is itself one of the proposal's interventions (social contact). A small, real boost, at most once every 25 game hours per person.
    public bool SocialBoost(int k)
    {
        talks++;
        if (sim.phase != HabPhase.Mission || sim.missionSeconds < talkReady[k]) return false;
        talkReady[k] = sim.missionSeconds + 25f;
        var c = sim.crew[k]; c.mood += 3f; c.stress -= 2f; c.Clamp(); return true;
    }
}
