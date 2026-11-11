// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/01_Core/HabitatSim.cs
//  Section : 01_Core
//  Folder  : Game rules and the habitat simulation (crew, systems, alerts), start-up, self-test,
//            cheats and the live link to the Crew Health web page.
//  Purpose : The habitat simulation. No Unity scene objects in here, only numbers and rules taken
//            from the proposal.
//  Contains: CrewClass, HabPhase, CrewMember, HabAlert, HabitatSim
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

public enum CrewClass { Neutral, UnderlyingIssue, SuspectedIllness }
public enum HabPhase { Briefing, Commissioning, Mission, Report }

[Serializable]
public class CrewMember
{
    public string name, role, favourite;      // favourite = zone id of the intervention that works best for this person
    public float mood = 78f, stress = 22f, sleep = 82f, health = 96f;
    public float stressRate = 1f, moodRate = 1f;
    public float wellbeingSum; public float illnessSeconds;
    public float Wellbeing { get { return Mathf.Clamp(0.4f * mood + 0.3f * (100f - stress) + 0.3f * sleep, 0f, 100f); } }
    // the three classes the proposal's AI model reports
    public CrewClass Class { get { float w = Wellbeing; return w >= 60f ? CrewClass.Neutral : (w >= 42f ? CrewClass.UnderlyingIssue : CrewClass.SuspectedIllness); } }
    public int HeartRate { get { return Mathf.RoundToInt(56f + stress * 0.42f + (100f - health) * 0.15f); } }
    public CrewMember Copy() { return (CrewMember)MemberwiseClone(); }
    public void Clamp() { mood = Mathf.Clamp(mood, 0f, 100f); stress = Mathf.Clamp(stress, 0f, 100f); sleep = Mathf.Clamp(sleep, 0f, 100f); health = Mathf.Clamp(health, 0f, 100f); }
}

public class HabAlert
{
    public string type, title, detail, zoneId;
    public float secondsLeft, total;
    public bool needsPart, overdue, persistent;
    public int crew = -1, criterion, step;
    public HabAlert Copy() { return (HabAlert)MemberwiseClone(); }
}

/// The habitat simulation. No Unity scene objects in here, only numbers and rules taken from the proposal.
public class HabitatSim
{
    public const int MissionDays = 30;                 // NASA: 2-4 crew for 30+ days
    public const float SecondsPerDay = 36f;            // one game hour every second and a half: a calmer day, with more time for every job
    public static readonly string[] Criteria = {
        "Crew and mission (2-4 crew, 30+ days)",
        "Zoning and adjacency (8 NASA zones, dust control)",
        "Environmental protection (regolith, radiation, dust)",
        "Life support (ECLSS air, water, food)",
        "Power (solar + 8-day storage + nuclear backup)",
        "Redundancy and autonomy (faults, spares, robots)",
        "AI psychological monitoring (crew mental health)" };
    // top-scoring NASA adjacency sequence from the proposal
    public static readonly string[] AdjacencyOrder = { "airlock", "hygiene", "quarters", "galley", "work", "storage", "eclss", "medical" };

    public HabPhase phase = HabPhase.Briefing;
    public float missionSeconds;                      // mission clock, runs only in the Mission phase
    public List<CrewMember> crew = new List<CrewMember>();
    public List<HabAlert> alerts = new List<HabAlert>();
    public float oxygen = 100f, water = 100f, food = 100f, battery = 100f, dust = 6f, dose = 0f, arrays = 100f;
    public int parts = 1, storageStock = 3;
    public bool solarOn = true, fissionOn, eclssOk = true, waterOk = true, commsOk = true, circadianOk = true, sheltered, leak, stormActive;
    public float stormIn = -1f, stormLeft;
    public bool timeFrozen;                           // free-explore mode: days and nights still pass, but no alerts and nothing wears out
    public bool kidMode = true;                       // more time on every alert, gentler stress, nobody can be lost
    public int tends, evaDone, stormsSafe, psychHelped, repairs, highFives, nightHandled;
    public int tutorialIndex, tutorialMistakes;
    public string message = ""; public float messageAge = 99f;
    public bool newAlertFlag, missionFailed; public string failReason = "";
    public Dictionary<string, float> cooldown = new Dictionary<string, float>();
    public Func<string, string> zoneTitle = id => id;
    public Action<HabAlert> onRaised, onResolved;      // the effects and the voice listen to these
    public Action<string> onCare;                      // a routine care action was done in this zone

    // score keeping
    float[] handled = new float[7]; float[] total = new float[7];
    float samples, sumLife, sumBattery, sumDust, blackoutSeconds, starveSeconds, fissionDelay; int stormsMissed, stormsTotal;
    float nextEvent = 14f; int scripted; System.Random rng = new System.Random(2027);
    bool nightStarted, nightEnded;

    public HabitatSim()
    {
        crew.Add(new CrewMember { name = "Cmdr. Vega", role = "Commander", favourite = "observation", stressRate = 0.9f, moodRate = 1.0f });
        crew.Add(new CrewMember { name = "Dr. Okafor", role = "Flight surgeon", favourite = "greenhouse", stressRate = 1.0f, moodRate = 0.9f });
        crew.Add(new CrewMember { name = "Eng. Tanaka", role = "Systems engineer", favourite = "sanctuary", stressRate = 1.25f, moodRate = 1.0f });
        crew.Add(new CrewMember { name = "Sci. Rivera", role = "Geologist", favourite = "control", stressRate = 1.0f, moodRate = 1.2f });
    }

    /// A planning copy: the same numbers and rules, but silent (no voice, no effects) and with no new random events.
    /// The Autopilot Smooth AI plays ahead on copies like this to compare its choices.
    public bool noEvents;
    public HabitatSim Clone()
    {
        var s = (HabitatSim)MemberwiseClone();
        s.crew = new List<CrewMember>(crew.Count); foreach (var c in crew) s.crew.Add(c.Copy());
        s.alerts = new List<HabAlert>(alerts.Count); foreach (var a in alerts) s.alerts.Add(a.Copy());
        s.cooldown = new Dictionary<string, float>(cooldown);
        s.handled = (float[])handled.Clone(); s.total = (float[])total.Clone();
        s.onRaised = null; s.onResolved = null; s.onCare = null; s.noEvents = true;
        return s;
    }
    public bool HasAlertAt(string zone) { return AlertAt(zone) != null; }
    public float CooldownAt(string zone) { return Cd(zone); }

    public int Day { get { return Mathf.Min(MissionDays, 1 + Mathf.FloorToInt(missionSeconds / SecondsPerDay)); } }
    public float Hour { get { return (missionSeconds / SecondsPerDay * 24f + 8f) % 24f; } }   // mission starts at 08:00
    public bool IsNightHours { get { float h = Hour; return h >= 22f || h < 6f; } }
    public bool LunarNight { get { if (timeFrozen) { float d = missionSeconds / SecondsPerDay; return d >= 9f && d < 18f; } return nightStarted && !nightEnded; } }
    public bool Blackout { get { return battery <= 0.01f; } }
    public float AvgWellbeing { get { float s = 0f; foreach (var c in crew) s += c.Wellbeing; return s / crew.Count; } }

    public void Say(string m) { message = m; messageAge = 0f; }

    // ------------------------------------------------------------------ time
    public void Tick(float dt)
    {
        messageAge += dt;
        var keys = new List<string>(cooldown.Keys);
        foreach (var k in keys) cooldown[k] = Mathf.Max(0f, cooldown[k] - dt);
        if (phase != HabPhase.Mission) return;
        if (timeFrozen) { missionSeconds += dt; if (missionSeconds >= SecondsPerDay * MissionDays) missionSeconds = 0f; return; }
        missionSeconds += dt;
        float h = dt;                                   // hours of habitat time this step (1 s = 1 h)

        // ---- power: solar + storage sized for 8 days + 10 kW fission backup
        float gen = (solarOn ? 12f * arrays / 100f : 0f) + (fissionOn ? 10f : 0f), load = 9f;
        battery = Mathf.Clamp(battery + (gen - load) * (100f / (9f * 192f)) * h * (gen > load ? 4f : 1f), 0f, 100f);
        arrays = Mathf.Max(40f, arrays - 0.02f * h);
        if (LunarNight && !fissionOn) fissionDelay += dt;
        if (Blackout) blackoutSeconds += dt;

        // ---- life support
        bool air = eclssOk && !Blackout;
        oxygen = Mathf.Clamp(oxygen + (air ? 0.9f : -1.3f) * h - (leak ? 2.6f * h : 0f), 0f, 100f);
        if (kidMode && oxygen < 30f) oxygen = 30f;
        water = Mathf.Clamp(water + (waterOk && !Blackout ? -0.04f : -0.75f) * h, 0f, 100f);
        food = Mathf.Clamp(food - 0.115f * h, 0f, 100f);
        dust = Mathf.Clamp(dust + 0.035f * h, 0f, 100f);
        if (food <= 0.01f || water <= 0.01f) starveSeconds += dt;

        // ---- solar storm
        if (stormIn >= 0f) { stormIn -= dt; if (stormIn < 0f) { stormActive = true; stormLeft = 18f; stormsTotal++; if (!sheltered) { stormsMissed++; Say("ARIA: Solar storm has hit and the crew is NOT in the shelter. Radiation dose rising."); } else Say("ARIA: Storm front passing. Crew is safe behind the regolith shield."); } }
        if (stormActive)
        {
            stormLeft -= dt; dose += (sheltered ? 0.05f : 1.6f) * h;
            if (stormLeft <= 0f) { stormActive = false; sheltered = false; Say("ARIA: Solar storm has passed. Crew released from the shelter."); }
        }
        dose += 0.004f * h;

        // ---- crew: stress, sleep, mood, health
        int open = alerts.Count; float dayFactor = 1f + missionSeconds / (SecondsPerDay * MissionDays);   // isolation builds up over the mission
        float kf = kidMode ? 0.6f : 1f;
        foreach (var c in crew)
        {
            c.stress += (0.26f * dayFactor + 0.22f * open) * c.stressRate * kf * h;
            if (IsNightHours) c.sleep += (circadianOk ? 3.2f : 0.6f) * h; else c.sleep -= 1.5f * h;
            c.mood -= 0.2f * c.moodRate * dayFactor * kf * h;
            if (c.stress > 60f) c.mood -= 0.25f * h;
            if (c.sleep < 35f) { c.stress += 0.3f * h; c.mood -= 0.15f * h; }
            if (!commsOk) c.mood -= 0.12f * h;
            if (food < 15f) c.mood -= 0.5f * h;
            if (Blackout) { c.stress += 1.2f * h; c.mood -= 0.8f * h; }
            if (oxygen < 55f) { c.stress += 1.5f * h; c.health -= (oxygen < 30f ? 2.2f : 0.7f) * h; }
            if (water < 10f || food < 5f) c.health -= 0.8f * h;
            if (dust > 55f) c.health -= 0.012f * (dust - 55f) * h;
            if (stormActive && !sheltered) { c.health -= 0.9f * h; c.stress += 1.5f * h; }
            float w = c.Wellbeing;
            c.health += (w >= 60f ? 0.05f : (w < 42f ? -0.2f : -0.03f)) * h;
            c.Clamp();
            if (kidMode && c.health < 25f) c.health = 25f;
            c.wellbeingSum += w * dt;
            if (c.Class == CrewClass.SuspectedIllness) c.illnessSeconds += dt;
        }

        // ---- score samples
        samples += dt; sumLife += Mathf.Min(oxygen, water) * dt; sumBattery += battery * dt; sumDust += dust * dt;

        UpdateAlerts(dt);
        Schedule();

        // ---- end conditions
        foreach (var c in crew) if (c.health <= 0.01f && !missionFailed) { missionFailed = true; failReason = c.name + " had to be evacuated."; }
        if (oxygen <= 0.01f && !missionFailed) { missionFailed = true; failReason = "The habitat lost its breathable air."; }
        if (missionFailed || missionSeconds >= SecondsPerDay * MissionDays) { phase = HabPhase.Report; alerts.Clear(); }
    }

    // ------------------------------------------------------------------ alerts
    HabAlert Add(string type, string title, string detail, string zone, float seconds, int criterion, bool needsPart = false, bool persistent = false, int crewIndex = -1)
    {
        if (kidMode) seconds *= 1.7f;
        var a = new HabAlert { type = type, title = title, detail = detail, zoneId = zone, secondsLeft = seconds, total = seconds, criterion = criterion, needsPart = needsPart, persistent = persistent, crew = crewIndex };
        alerts.Add(a); newAlertFlag = true; Say("ARIA: " + title + ". " + detail);
        if (onRaised != null) onRaised(a);
        return a;
    }
    bool Has(string type) { foreach (var a in alerts) if (a.type == type) return true; return false; }

    void UpdateAlerts(float dt)
    {
        for (int i = alerts.Count - 1; i >= 0; i--)
        {
            var a = alerts[i];
            a.secondsLeft -= dt;
            if (a.secondsLeft > 0f || a.overdue) continue;
            a.overdue = true; total[a.criterion] += 1f;        // missed the response window
            switch (a.type)
            {
                case "psych": if (a.crew >= 0) { crew[a.crew].stress += 16f; crew[a.crew].mood -= 10f; } Say("ARIA: " + crew[a.crew].name + " did not get support in time. Stress has increased."); break;
                case "medical": if (a.crew >= 0) crew[a.crew].health -= 26f; Say("ARIA: Medical response was too slow. " + crew[a.crew].name + " is worse."); break;
                case "eva": dust += 28f; Say("ARIA: EVA crew came in without decontamination. Lunar dust is spreading inside."); break;
                case "storm": break;       // the storm itself applies the penalty
                default: Say("ARIA: " + a.title + " is still not fixed. Systems are degrading."); break;
            }
            if (!a.persistent) alerts.RemoveAt(i);
        }
    }

    void Resolve(HabAlert a)
    {
        if (!a.overdue) { handled[a.criterion] += 1f; total[a.criterion] += 1f; }
        else handled[a.criterion] += 0.4f;                  // late, but fixed
        if (onResolved != null) onResolved(a);
        alerts.Remove(a);
    }

    void Schedule()
    {
        // lunar night: day 10 to day 19 (longer than the 8 days of storage, so the backup is needed)
        if (!nightStarted && missionSeconds >= SecondsPerDay * 9f)
        {
            nightStarted = true; solarOn = false;
            Add("night", "Lunar night has begun", "Solar arrays are dark for 9 days and storage lasts 8. Start the fission backup at the Power Plant.", "power", 60f, 4, false, true);
        }
        if (nightStarted && !nightEnded && missionSeconds >= SecondsPerDay * 18f) { nightEnded = true; solarOn = true; Say("ARIA: Sunrise. Solar arrays are generating again."); }

        if (noEvents) return;
        if (missionSeconds < nextEvent || alerts.Count >= 2) return;
        string[] first = { "psych", "eva", "scrubber", "storm", "sleep", "medical", "water", "arrays", "leak", "comms" };
        string type;
        if (scripted < first.Length) type = first[scripted++];
        else { string[] pool = { "psych", "psych", "psych", "eva", "scrubber", "storm", "sleep", "medical", "water", "arrays", "leak", "comms" }; type = pool[rng.Next(pool.Length)]; }
        if (Has(type) || (type == "storm" && (stormIn >= 0f || stormActive))) { nextEvent = missionSeconds + 4f; return; }
        Spawn(type);
        nextEvent = missionSeconds + 19f + (float)rng.NextDouble() * 9f;
    }

    public void Spawn(string type)
    {
        switch (type)
        {
            case "psych":
                {
                    int k = 0; for (int i = 1; i < crew.Count; i++) if (crew[i].Wellbeing < crew[k].Wellbeing) k = i;
                    var c = crew[k]; c.stress += 14f; c.mood -= 6f; c.Clamp();
                    Add("psych", "AI flags " + c.name + ": stress rising", "Model class: " + ClassName(c.Class) + ". Recommended intervention: " + zoneTitle(c.favourite) + ".", c.favourite, 60f, 6, false, false, k);
                    break;
                }
            case "eva": Add("eva", "EVA team returning", "Run the dust mitigation cycle at the Airlock, then decontaminate in Hygiene.", "airlock", 55f, 1); break;
            case "scrubber": eclssOk = false; Add("scrubber", "CO2 scrubber fault on loop A", "Fit a spare part at ECLSS Life Support. Oxygen is falling.", "eclss", 55f, 5, true, true); break;
            case "water": waterOk = false; Add("water", "Water recycler fault", "Fit a spare part at Hygiene. Water reserve is draining.", "hygiene", 60f, 5, true, true); break;
            case "leak": leak = true; Add("leak", "Pressure leak detected", "Isolate the compartment from the Workstations / AI Hub.", "work", 40f, 5, false, true); break;
            case "storm": Add("storm", "Solar storm warning", "Move the crew into the regolith Storm Shelter before the front arrives.", "shelter", 42f, 2); break;
            case "sleep": circadianOk = false; Add("sleep", "Sleep disruption", "Lighting is out of sync with the crew's body clocks. Reset circadian lighting in Crew Quarters.", "quarters", 60f, 6, false, true); break;
            case "medical":
                {
                    int k = rng.Next(crew.Count); var c = crew[k]; c.health -= 12f; c.Clamp();
                    Add("medical", "Medical alert: " + c.name, "Biometrics out of range (heart rate " + (c.HeartRate + 28) + "). Treat in Medical.", "medical", 50f, 0, false, false, k);
                    break;
                }
            case "arrays": arrays = Mathf.Min(arrays, 55f); Add("arrays", "Solar arrays coated in dust", "Send the cleaning robot from the Power Plant.", "power", 70f, 4, false, true); break;
            case "comms": commsOk = false; Add("comms", "Earth link lost", "Realign the dish from Mission Control. The crew cannot call home.", "control", 70f, 6, false, true); break;
        }
    }

    public static string ClassName(CrewClass c) { return c == CrewClass.Neutral ? "Neutral" : (c == CrewClass.UnderlyingIssue ? "Underlying issue" : "Suspected illness"); }

    // ------------------------------------------------------------------ player actions
    HabAlert AlertAt(string zone) { foreach (var a in alerts) if (a.zoneId == zone) return a; return null; }
    float Cd(string zone) { float v; return cooldown.TryGetValue(zone, out v) ? v : 0f; }

    /// What pressing Interact would do in this zone right now.
    public string Prompt(string zone)
    {
        if (phase == HabPhase.Briefing) return "Start the mission";
        if (phase == HabPhase.Report) return "";
        if (phase == HabPhase.Commissioning) return zone == AdjacencyOrder[tutorialIndex] ? "Commission this zone" : "Not next in the NASA sequence";
        var a = AlertAt(zone);
        if (a != null)
        {
            if (a.needsPart && parts <= 0) return "Needs a spare part (Storage or Fabrication Bay)";
            switch (a.type)
            {
                case "psych": return "Give " + crew[a.crew].name + " this intervention";
                case "eva": return a.step == 0 ? "Run dust mitigation cycle" : "Decontaminate EVA crew";
                case "storm": return "Shelter the crew";
                case "night": return "Start 10 kW fission backup";
                case "medical": return "Treat " + crew[a.crew].name;
                case "leak": return "Isolate the leaking compartment";
                case "sleep": return "Reset circadian lighting";
                case "arrays": return "Dispatch array-cleaning robot";
                case "comms": return "Realign Earth link";
                default: return "Fit spare part and repair";
            }
        }
        float cd = Cd(zone);
        if (cd > 0f) return "Ready again in " + Mathf.CeilToInt(cd) + " s";
        switch (zone)
        {
            case "airlock": return "Run dust mitigation cycle";
            case "hygiene": return "Crew hygiene and water check";
            case "quarters": return "Tune circadian lighting for sleep";
            case "galley": return "Hold a shared crew meal";
            case "work": return "Review AI wellbeing report";
            case "storage": return storageStock > 0 ? "Take a spare part (" + storageStock + " left)" : "Storage is out of spares";
            case "eclss": return "Service air and water loops";
            case "medical": return "Routine health check";
            case "sanctuary": return "Run a VR relaxation session";
            case "exercise": return "Lead exercise countermeasure";
            case "greenhouse": return "Tend the plants";
            case "power": return fissionOn ? "Shut down fission backup" : "Start 10 kW fission backup";
            case "fab": return "3D-print a spare part";
            case "control": return commsOk ? "Open a family call to Earth" : "Earth link is down";
            case "shelter": return "Run a shelter drill";
            case "observation": return "Earth-view break for the crew";
        }
        return "";
    }

    CrewMember Worst(Func<CrewMember, float> f) { var b = crew[0]; foreach (var c in crew) if (f(c) < f(b)) b = c; return b; }
    void All(Action<CrewMember> f) { foreach (var c in crew) { f(c); c.Clamp(); } }

    public void Interact(string zone)
    {
        if (phase == HabPhase.Briefing) { phase = HabPhase.Commissioning; Say("ARIA: Commissioning walk. Visit the 8 NASA zones in adjacency order, starting at the Airlock."); return; }
        if (phase == HabPhase.Report || string.IsNullOrEmpty(zone)) return;
        if (phase == HabPhase.Commissioning)
        {
            if (zone == AdjacencyOrder[tutorialIndex])
            {
                tutorialIndex++;
                if (tutorialIndex >= AdjacencyOrder.Length) { phase = HabPhase.Mission; tutorialIndex = AdjacencyOrder.Length; Say("ARIA: All 8 zones commissioned in NASA adjacency order. Day 1 begins. Keep the crew and the habitat healthy for 30 days."); }
                else Say("ARIA: " + zoneTitle(zone) + " commissioned. Next: " + zoneTitle(AdjacencyOrder[tutorialIndex]) + ".");
            }
            else { tutorialMistakes++; Say("ARIA: Out of sequence. The next zone is " + zoneTitle(AdjacencyOrder[tutorialIndex]) + "."); }
            return;
        }

        var a = AlertAt(zone);
        if (a != null)
        {
            if (a.needsPart && parts <= 0) { Say("ARIA: No spare part on hand. Take one from Storage or 3D-print one in the Fabrication Bay."); return; }
            if (a.needsPart) parts--;
            switch (a.type)
            {
                case "psych": { var c = crew[a.crew]; c.stress -= 30f; c.mood += 16f; c.Clamp(); Say("ARIA: Intervention delivered. " + c.name + " is calmer: stress " + Mathf.RoundToInt(c.stress) + ", mood " + Mathf.RoundToInt(c.mood) + "."); psychHelped++; Resolve(a); break; }
                case "eva":
                    if (a.step == 0) { a.step = 1; a.zoneId = "hygiene"; a.secondsLeft += 22f; a.detail = "Now decontaminate the EVA crew in Hygiene."; dust = Mathf.Max(0f, dust - 4f); Say("ARIA: Dust cycle complete. Send the EVA crew straight to Hygiene."); }
                    else { dust = Mathf.Max(0f, dust - 8f); Say("ARIA: EVA crew is clean. Airlock to Hygiene kept the dust out of the living areas."); evaDone++; Resolve(a); }
                    break;
                case "storm": stormsSafe++; sheltered = true; stormIn = Mathf.Max(0.5f, a.secondsLeft); Say("ARIA: Crew is in the shelter behind the regolith shield."); Resolve(a); break;
                case "night": nightHandled++; fissionOn = true; Say("ARIA: Fission backup online at 10 kW. The habitat will ride through the lunar night."); Resolve(a); break;
                case "medical": { var c = crew[a.crew]; c.health += 30f; c.stress -= 6f; c.Clamp(); Say("ARIA: " + c.name + " treated and stable."); Resolve(a); break; }
                case "leak": repairs++; leak = false; Say("ARIA: Compartment isolated. Pressure is holding."); Resolve(a); break;
                case "sleep": circadianOk = true; All(c => c.sleep += 8f); Say("ARIA: Lighting now follows an Earth day and night cycle again."); Resolve(a); break;
                case "arrays": arrays = 100f; Say("ARIA: Cleaning robot dispatched. Solar arrays back to full output."); Resolve(a); break;
                case "comms": commsOk = true; Say("ARIA: Earth link restored."); Resolve(a); break;
                case "scrubber": repairs++; eclssOk = true; Say("ARIA: Scrubber repaired. Loop A is back, loop B stood in as the redundant system."); Resolve(a); break;
                case "water": repairs++; waterOk = true; Say("ARIA: Water recycler repaired."); Resolve(a); break;
            }
            return;
        }

        if (Cd(zone) > 0f) { Say("ARIA: That is not ready yet (" + Mathf.CeilToInt(Cd(zone)) + " s)."); return; }
        float cd = 35f;
        switch (zone)
        {
            case "airlock": dust = Mathf.Max(0f, dust - 10f); Say("ARIA: Dust mitigation cycle complete."); cd = 30f; break;
            case "hygiene": dust = Mathf.Max(0f, dust - 7f); All(c => c.mood += 1.5f); Say("ARIA: Hygiene round done, water loop checked."); cd = 30f; break;
            case "quarters": All(c => { c.sleep += 7f; c.stress -= 2f; }); Say("ARIA: Each sleep pod's lighting tuned to its occupant's body clock."); cd = 40f; break;
            case "galley": if (food < 3f) { Say("ARIA: There is not enough food for a shared meal."); return; } food -= 2f; All(c => { c.mood += 8f; c.stress -= 5f; }); Say("ARIA: Shared meal. Social time lifts the whole crew."); cd = 45f; break;
            case "work": { var w = Worst(c => c.Wellbeing); All(c => c.stress -= 2f); Say("ARIA report: lowest wellbeing is " + w.name + " at " + Mathf.RoundToInt(w.Wellbeing) + " (" + ClassName(w.Class) + "). Suggested: " + zoneTitle(w.favourite) + "."); cd = 25f; break; }
            case "storage": if (storageStock <= 0) { Say("ARIA: Storage has no spares left. Use the Fabrication Bay."); return; } storageStock--; parts++; Say("ARIA: Spare part collected. You now carry " + parts + "."); cd = 8f; break;
            case "eclss": oxygen = Mathf.Min(100f, oxygen + 9f); water = Mathf.Min(100f, water + 5f); Say("ARIA: Air and water loops serviced."); cd = 40f; break;
            case "medical": { var c = Worst(x => x.health); c.health += 9f; c.Clamp(); Say("ARIA: Health check done. " + c.name + " health " + Mathf.RoundToInt(c.health) + "."); cd = 40f; break; }
            case "sanctuary": { var c = Worst(x => -x.stress); c.stress -= 22f; c.mood += 5f; c.Clamp(); Say("ARIA: VR nature session for " + c.name + ". Stress now " + Mathf.RoundToInt(c.stress) + "."); cd = 35f; break; }
            case "exercise": All(c => { c.health += 3f; c.stress -= 6f; c.sleep += 2f; c.mood += 2f; }); Say("ARIA: Exercise session logged for all four crew."); cd = 40f; break;
            case "greenhouse": tends++; All(c => { c.mood += 6f; c.stress -= 3f; }); oxygen = Mathf.Min(100f, oxygen + 5f); food = Mathf.Min(100f, food + 6f); Say("ARIA: Plants tended. Fresh food and oxygen added."); cd = 40f; break;
            case "power": fissionOn = !fissionOn; Say(fissionOn ? "ARIA: Fission backup online at 10 kW." : "ARIA: Fission backup shut down. Running on solar and storage."); cd = 6f; break;
            case "fab": parts++; Say("ARIA: Spare part printed. You now carry " + parts + "."); cd = 22f; break;
            case "control": if (!commsOk) { Say("ARIA: The Earth link is down. Realign it first."); return; } { var c = Worst(x => x.mood); c.mood += 20f; c.stress -= 8f; c.Clamp(); Say("ARIA: " + c.name + " spoke with family on Earth. Mood now " + Mathf.RoundToInt(c.mood) + "."); cd = 55f; } break;
            case "shelter": Say("ARIA: Shelter drill complete. Everyone knows the route."); All(c => c.stress -= 1f); cd = 60f; break;
            case "observation": All(c => { c.mood += 5f; c.stress -= 4f; }); Say("ARIA: The crew watched Earth for a while."); cd = 45f; break;
            default: return;
        }
        cooldown[zone] = cd;
        if (onCare != null) onCare(zone);
    }

    /// A friendly high-five: a small, real boost. Social contact is one of the proposal's countermeasures.
    public void HighFive(int k)
    {
        if (k < 0 || k >= crew.Count) return;
        var c = crew[k]; c.mood += 6f; c.stress -= 5f; c.Clamp(); highFives++;
    }

    // ------------------------------------------------------------------ scoring against the rubric
    static float Ratio(float h, float t) { return (h + 1f) / (t + 1f); }
    public float[] Scores()
    {
        var s = new float[7];
        float progress = Mathf.Clamp01(missionSeconds / (SecondsPerDay * MissionDays));
        float health = 0f, wb = 0f, ill = 0f; foreach (var c in crew) { health += c.health; wb += samples > 0f ? c.wellbeingSum / samples : c.Wellbeing; ill += samples > 0f ? c.illnessSeconds / samples : 0f; }
        health /= crew.Count; wb /= crew.Count; ill /= crew.Count;
        s[0] = 100f * progress * (0.45f + 0.55f * health / 100f) * (0.7f + 0.3f * Ratio(handled[0], total[0]));
        s[1] = 55f * (tutorialIndex / 8f) * Mathf.Clamp01(1f - tutorialMistakes / 8f) + 45f * Ratio(handled[1], total[1]) * (phase == HabPhase.Briefing || phase == HabPhase.Commissioning ? 0f : 1f);
        float avgDust = samples > 0f ? sumDust / samples : dust;
        s[2] = (samples > 0f ? 100f : 0f) - dose * 1.4f - Mathf.Max(0f, avgDust - 15f) * 1.3f - stormsMissed * 14f;
        s[3] = (samples > 0f ? sumLife / samples : 0f) - 100f * (samples > 0f ? starveSeconds / samples : 0f);
        float bf = samples > 0f ? blackoutSeconds / samples : 0f;
        s[4] = samples > 0f ? 100f * (0.55f + 0.45f * (sumBattery / samples) / 100f) * (1f - Mathf.Clamp01(bf * 4f)) - Mathf.Min(25f, fissionDelay * 0.25f) - 8f * (total[4] - handled[4]) : 0f;
        s[5] = samples > 0f ? 100f * Ratio(handled[5], total[5]) : 0f;
        s[6] = samples > 0f ? 0.6f * wb + 40f * Ratio(handled[6], total[6]) - 30f * ill : 0f;
        for (int i = 0; i < 7; i++) s[i] = Mathf.Clamp(s[i], 0f, 100f) * (missionFailed ? 0.25f + 0.75f * progress : 1f);   // an evacuated habitat does not meet the 30-day requirement
        return s;
    }
    /// the plain average of the seven rubric scores
    public float RawOverall() { var s = Scores(); float t = 0f; foreach (var v in s) t += v; return t / s.Length; }
    /// the mission score that is shown and graded. It is curved to be forgiving in the middle and strict at the top:
    /// a steady mission lands in the B range, an A needs nearly everything right, a C means real mistakes, a D means about half was missed.
    public float Overall() { return Mathf.Pow(Mathf.Clamp01(RawOverall() / 100f), 0.6f) * 100f; }
    public static string Letter(float v) { return v >= 93f ? "A" : (v >= 78f ? "B" : (v >= 62f ? "C" : (v >= 45f ? "D" : "F"))); }
    public static string Grade(float v) { return v >= 93f ? "A   OUTSTANDING" : (v >= 78f ? "B   MEETS NASA REQUIREMENTS" : (v >= 62f ? "C   PARTIALLY MEETS" : (v >= 45f ? "D   NEEDS MUCH MORE WORK" : "F   DOES NOT MEET"))); }
    public int Handled { get { float t = 0f; foreach (var v in handled) t += v; return Mathf.RoundToInt(t); } }
    public int Raised { get { float t = 0f; foreach (var v in total) t += v; return Mathf.RoundToInt(t) + alerts.Count; } }
}
