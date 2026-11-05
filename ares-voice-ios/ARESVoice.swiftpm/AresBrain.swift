import SwiftUI

// ARES carries out voice commands inside the rules of the Project ARES design:
//  • Hard limits 1, 3 and 4 are refused by voice, with the reason and a safe alternative.
//  • Every change can be undone by saying "undo" (hard limit 5: reversible within 2 minutes).
//  • A crew member's own data is only spoken aloud inside their private quarters; in shared
//    zones it goes to their screen instead (privacy-first, matching the D1 sensor design).
//  • Tier-2 offers are answered by voice ("yes" / "not now"), and a refusal is never repeated
//    within 6 hours (hard limit 2).
//  • Changes are gradual: lights move at most 500 K or 15 % per step, and temperature 1 °C per step.
//    A bigger change is ramped in small steps. Small, gentle, reversible changes are the default.

struct Reply {
    var display: String
    var spoken: String?
    var tone: Tone = .normal

    init(_ text: String, tone: Tone = .normal) { display = text; spoken = text; self.tone = tone }
    init(display: String, spoken: String?, tone: Tone = .normal) { self.display = display; self.spoken = spoken; self.tone = tone }
}

@MainActor
final class AresBrain: ObservableObject {
    let speech = SpeechEngine()
    let crew = CrewMember.all

    @Published var chat: [ChatLine] = []
    @Published var zones: [String: ZoneState] = [:]
    @Published var speakerID = "reyes" { didSet { updateListeningPrivacy() } }
    @Published var speakerZone: ZoneKind = .communal { didSet { updateListeningPrivacy() } }
    @Published var handsFreeInQuarters = false { didSet { updateListeningPrivacy() } }
    @Published var voiceReplies = true
    @Published var aiPausedUntil: [String: Date] = [:]
    @Published var quartersAccess: [String: Bool] = [:]     // AI access to quarters: off by default
    @Published var declinedAt: [String: Date] = [:]
    @Published var pendingOffer: Offer?
    @Published var timers: [VoiceTimer] = []
    @Published var emergency: EmergencyAlert?
    @Published private(set) var log: [LogEntry] = []
    @Published var inbox: [InboxMessage] = []
    @Published private(set) var demoCaption: String?
    @Published private(set) var demoRunning = false

    private var lastSpoken = ""
    private var demoTask: Task<Void, Never>?

    var myUnread: [InboxMessage] { inbox.filter { $0.to == speakerID && !$0.read } }

    static let kelvinStep = 500, brightnessStep = 15, celsiusStep = 1
    static let kelvinRange = 2200...6500, brightnessRange = 5...100, celsiusRange = 18...26
    static let rampInterval: TimeInterval = 20

    private var undoStack: [Snapshot] = []
    private var lastRamp = Date()
    private var ticker: Task<Void, Never>?

    init() {
        resetHabitat()
        speech.onCommand = { [weak self] text in self?.submit(text) }
        ticker = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(nanoseconds: 1_000_000_000)
                self?.tick()
            }
        }
    }

    /// Puts everything back to the start (used by the Reset button and before the board demo).
    func resetHabitat() {
        zones = [:]
        for z in ZoneKind.allCases where z != .quarters { zones[z.rawValue] = ZoneState(zone: z) }
        for c in CrewMember.all { zones[Self.quartersKey(c.id)] = ZoneState(zone: .quarters) }
        aiPausedUntil = [:]; quartersAccess = [:]; declinedAt = [:]
        pendingOffer = nil; timers = []; emergency = nil; inbox = []
        undoStack = []; log = []; lastSpoken = ""
        speakerID = "reyes"; speakerZone = .communal
        chat = [ChatLine(fromUser: false,
                         text: "ARES voice is ready. Tap the orb or turn on “Hey ARES”. Speech is processed on this device with no internet, and audio is never saved.")]
    }

    // MARK: who and where

    var speaker: CrewMember { crew.first { $0.id == speakerID } ?? crew[0] }
    var inPrivate: Bool { speakerZone == .quarters }
    var aiPaused: Bool { (aiPausedUntil[speakerID] ?? .distantPast) > Date() }

    static func quartersKey(_ id: String) -> String { "quarters.\(id)" }
    func key(for zone: ZoneKind) -> String { zone == .quarters ? Self.quartersKey(speakerID) : zone.rawValue }
    func state(of zone: ZoneKind) -> ZoneState { zones[key(for: zone)] ?? ZoneState(zone: zone) }

    var myLog: [LogEntry] { log.filter { $0.crewID == nil || $0.crewID == speakerID }.reversed() }

    /// Hands-free listening pauses inside private quarters unless that person turns it on.
    private func updateListeningPrivacy() {
        speech.handsFreeSuspended = inPrivate && !handsFreeInQuarters
    }

    // MARK: input

    func submit(_ raw: String) {
        let text = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { speech.finishedWithoutSpeaking(); return }
        chat.append(ChatLine(fromUser: true, text: text, who: speaker.first))
        let command = CommandParser(crew: crew, speakerID: speakerID).parse(text)
        let reply = execute(command)
        chat.append(ChatLine(fromUser: false, text: reply.display, tone: reply.tone))
        if command != .repeatLast, let s = reply.spoken { lastSpoken = s }
        if voiceReplies, let spoken = reply.spoken { speech.speak(spoken) } else { speech.finishedWithoutSpeaking() }
    }

    private func record(_ action: String, why: String, tier: Int, crewID: String? = nil, forAll: Bool = false) {
        log.append(LogEntry(crewID: forAll ? nil : (crewID ?? speakerID), action: action, why: why, tier: tier))
    }

    /// Personal information is spoken only in private quarters; elsewhere it goes to the screen.
    private func personal(_ detail: String) -> Reply {
        if inPrivate { return Reply(detail, tone: .privateInfo) }
        return Reply(display: detail + "\n\nOn screen only, because you're in \(speakerZone.title), a shared space.",
                     spoken: "You're in a shared space, so I've put that on your screen instead of saying it out loud.",
                     tone: .privateInfo)
    }

    // MARK: commands

    func execute(_ command: Command) -> Reply {
        switch command {
        case .emergency: return raiseEmergency()
        case .cancelEmergency:
            guard emergency != nil else { return Reply("There's no emergency active.") }
            emergency = nil
            record("Emergency cancelled by \(speaker.first)", why: "Cancelled by voice.", tier: 3, forAll: true)
            return Reply("Emergency cancelled. I've told the crew it was a false alarm. Lights stay up; say “reset lights” to bring them back.")

        case .blocked(let limit): return refuse(limit)

        case .undo:
            guard let s = undoStack.popLast() else { return Reply("There's nothing to undo.") }
            zones[s.key] = s.state
            record("Undid: \(s.label)", why: "You asked me to change it back.", tier: 0)
            return Reply("Done, I changed that back.")

        case .pauseAI:
            aiPausedUntil[speakerID] = Date().addingTimeInterval(72 * 3600)
            if pendingOffer?.crewID == speakerID { pendingOffer = nil }
            record("Paused all AI support for 72 h", why: "You asked. No reason needed.", tier: 0)
            return Reply("AI support is paused for you for 72 hours. Voice commands still work, because that's you in control. Say “resume AI” any time.")

        case .resumeAI:
            aiPausedUntil[speakerID] = nil
            record("Resumed AI support", why: "You asked.", tier: 0)
            return Reply("AI support is back on for you.")

        case .handsFree(let on):
            speech.setHandsFree(on)
            record(on ? "Hands-free “Hey ARES” on" : "Hands-free listening off", why: "You asked.", tier: 0)
            return Reply(on ? "Hands-free is on. Say “Hey ARES” whenever you need me." : "OK, I've stopped listening. Tap the orb when you need me.")

        case .quarters(let allow):
            quartersAccess[speakerID] = allow
            record(allow ? "Allowed AI in my quarters" : "Removed AI access to my quarters", why: "Your privacy setting.", tier: 0)
            return Reply(allow ? "OK. I can now adjust your quarters when you ask, or offer to. You can take this back any time."
                               : "Done. I won't act inside your quarters unless you ask me directly.")

        case .accept:
            guard let offer = pendingOffer, offer.crewID == speakerID else { return Reply("There's no suggestion waiting for you right now.") }
            pendingOffer = nil
            _ = applyScene(.calm, zone: speakerZone, why: "You accepted a Tier-2 offer.", tier: 2)
            record("Accepted offer: calm lighting + guided breathing", why: "You said yes.", tier: 2)
            return Reply("Starting calm light and a 5-minute guided breathing session. Say “stop” or “undo” to end it.")

        case .decline:
            guard let offer = pendingOffer, offer.crewID == speakerID else { return Reply("OK.") }
            pendingOffer = nil
            declinedAt[speakerID] = Date()
            record("Declined offer", why: "You said not now. I won't offer it again for 6 hours.", tier: 2)
            return Reply("No problem. I won't bring it up again for at least 6 hours.")

        case .whatDidYouDo:
            let mine = myLog.filter { $0.crewID == speakerID }.prefix(3)
            guard !mine.isEmpty else { return personal("I haven't changed anything for you yet.") }
            let lines = mine.map { "• \($0.action). Why: \($0.why)" }.joined(separator: "\n")
            return personal("Here's what I did most recently:\n" + lines + "\nThe full log is in the panel on the right.")

        case .myWellness:
            let me = speaker
            let sleepGap = me.sleepUsual - me.sleepHours
            let sleepText = sleepGap > 0.4
                ? String(format: "You slept %.1f hours, about %.1f under your usual %.1f.", me.sleepHours, sleepGap, me.sleepUsual)
                : String(format: "You slept %.1f hours, close to your usual %.1f.", me.sleepHours, me.sleepUsual)
            let hrvText = me.hrv < me.hrvUsual - 6
                ? "Your heart-rate variability is \(me.hrv), lower than your usual \(me.hrvUsual)."
                : "Your heart-rate variability is \(me.hrv), about your usual."
            return personal("\(sleepText) \(hrvText) Only you can see this. (Simulated data.)")

        case .message(let to, let text):
            if to == "mission" {
                record("Message to Mission Control: \(text)", why: "You asked.", tier: 0)
                return Reply("Sent to Mission Control: “\(text)”. Signals take about 1.3 seconds each way between the Moon and Earth, so a reply takes at least a few seconds.")
            }
            if let id = to, let c = crew.first(where: { $0.id == id }) {
                record("Message to \(c.first): \(text)", why: "You asked.", tier: 0)
                inbox.append(InboxMessage(to: c.id, from: speaker.first, text: text))
                return Reply("Sent to \(c.first): “\(text)”.")
            }
            record("Crew message from \(speaker.first): \(text)", why: "Crew message.", tier: 0, forAll: true)
            for c in crew where c.id != speakerID { inbox.append(InboxMessage(to: c.id, from: speaker.first, text: text)) }
            return Reply("Sent to the whole crew: “\(text)”.")

        case .timer(let seconds, let label):
            guard seconds > 0, seconds <= 24 * 3600 else { return Reply("I can set timers from 1 second to 24 hours.") }
            timers.append(VoiceTimer(label: label, end: Date().addingTimeInterval(TimeInterval(seconds))))
            return Reply("Timer set for \(label).")

        case .time:
            let f = DateFormatter()
            f.timeZone = TimeZone(identifier: "UTC")
            f.dateFormat = "HH:mm"
            return Reply("It's \(f.string(from: Date())) GMT, habitat time. Like the ISS, the habitat runs on GMT.")

        case .schedule:
            return Reply(nextScheduleItem())

        case .scene(let zone, let scene):
            return applyScene(scene, zone: zone ?? speakerZone, why: "You asked by voice.", tier: 0)

        case .lights(let zone, let change):
            return applyLights(change, zone: zone ?? speakerZone)

        case .clarifyLights(let zone):
            return Reply("What should I do with the lights in \((zone ?? speakerZone).title)? Brighter, dimmer, warmer or cooler?")

        case .thermal(let zone, let change):
            return applyThermal(change, zone: zone ?? speakerZone)

        case .status(let topic):
            return Reply(statusText(topic))

        case .surface:
            return Reply("Outside it's lunar daytime: the ground is around 100 °C in direct sun and far below freezing in shadow. Radiation is normal and there's no solar flare warning. (Simulated readings.)")

        case .routine(let r):
            return routine(r)

        case .repeatLast:
            guard !lastSpoken.isEmpty else { return Reply("I haven't said anything yet.") }
            return Reply(display: "(Repeating) " + lastSpoken, spoken: lastSpoken)

        case .speechRate(let faster):
            speech.rate = min(0.62, max(0.36, speech.rate + (faster ? 0.06 : -0.06)))
            return Reply(faster ? "OK, I'll talk a little faster." : "OK, I'll talk a little slower.")

        case .cancelTimer:
            guard !timers.isEmpty else { return Reply("You don't have any timers running.") }
            let n = timers.count
            timers.removeAll()
            return Reply(n == 1 ? "Timer cancelled." : "All \(n) timers cancelled.")

        case .timerLeft:
            guard let t = timers.min(by: { $0.end < $1.end }) else { return Reply("You don't have any timers running.") }
            let left = max(0, Int(t.end.timeIntervalSinceNow))
            return Reply("Your \(t.label) timer has \(left / 60) min \(left % 60) s left.")

        case .readMessages:
            let mine = myUnread
            guard !mine.isEmpty else { return personal("You have no new messages.") }
            for i in inbox.indices where inbox[i].to == speakerID { inbox[i].read = true }
            let text = mine.map { "From \($0.from): \($0.text)" }.joined(separator: "\n")
            return personal("You have \(mine.count) new message\(mine.count == 1 ? "" : "s").\n" + text)

        case .directions(let zone):
            let whereText: String
            switch zone {
            case .communal: whereText = "The Wardroom and the meeting table are upstairs in the central core."
            case .quarters: whereText = "Your quarters are in the quarters wing, through the quiet vestibule next to Medical."
            case .transition: whereText = "The corridors link every module to the core. Their lighting warms as you near the social areas."
            default: whereText = "The \(zone.title) is a module off the central core. Follow the corridor from the core."
            }
            return Reply(whereText + " The habitat map shows the route too.")

        case .help:
            return Reply(display: "Try: “make the wardroom warmer”, “lab lights to 4000 K”, “calm mode”, “undo”, “what did you do for me”, “how am I doing”, “tell Tanaka dinner is ready”, “timer 10 minutes”, “habitat status”, “pause AI”, “keep out of my quarters”. Tap ? for the full list and the things I'll never do.",
                         spoken: "I can change lights and temperature, set scenes, undo anything I did, send crew messages, set timers, read habitat status and your own wellness, and pause the AI. Tap the question mark for the full list.")

        case .greeting:
            return Reply("Hi \(speaker.first). What do you need?")

        case .thanks:
            return Reply("You're welcome.")

        case .unknown(let raw):
            return Reply(display: "I didn't understand “\(raw)”. Try “make the lab brighter” or say “what can you do”.",
                         spoken: "Sorry, I didn't get that. Try saying, what can you do.")
        }
    }

    // MARK: hard limits

    private func refuse(_ limit: HardLimit) -> Reply {
        switch limit {
        case .medication:
            record("Refused: medication request", why: "Hard limit 3: medication is the CMO's decision.", tier: 0)
            return Reply("I can't give, suggest or change any medication. That's Dr. Okafor's decision as Chief Medical Officer (hard limit 3). I can message the CMO for you: say “message Okafor” and your question.", tone: .refused)
        case .lifeSupport:
            record("Refused: life-support control", why: "Hard limit 3: needs human engineering judgement.", tier: 0)
            return Reply("I can't control pressure, oxygen, power or the airlock. Those need a human engineer (hard limit 3). I can read the levels: say “habitat status”.", tone: .refused)
        case .otherCrewData(let name):
            record("Refused: asked about \(name)'s data", why: "Hard limit 1: no one's data goes to another crew member.", tier: 0)
            return Reply("I never share \(name)'s wellness data, mood or location with anyone else (hard limit 1). If you're worried, check in with \(name) yourself, or message Dr. Okafor.", tone: .refused)
        case .missionControlFirst(let name):
            record("Refused: report about \(name) to Mission Control", why: "Hard limit 4: the person always hears first.", tier: 0)
            return Reply("I won't report anything about \(name) to Mission Control. A crew member always hears first (hard limit 4).", tone: .refused)
        }
    }

    private func raiseEmergency() -> Reply {
        for k in Array(zones.keys) {
            guard var z = zones[k] else { continue }
            z.on = true; z.kelvin = 5000; z.brightness = 100
            z.targetKelvin = nil; z.targetBrightness = nil
            zones[k] = z
        }
        emergency = EmergencyAlert(crewID: speakerID, zone: speakerZone)
        let responder = speakerID == "okafor" ? "Commander Reyes" : "Dr. Okafor, the CMO"
        record("EMERGENCY called by \(speaker.first) in \(speakerZone.title)", why: "A crew member asked for help.", tier: 3, forAll: true)
        return Reply("Emergency alert sent to the whole crew and \(responder). Every light is on full so people can find you. Mission Control is told after the crew. Stay where you are. If this was a mistake, say “cancel emergency”.", tone: .alert)
    }

    // MARK: environment (gradual by design)

    static func step(_ value: Int, toward target: Int, by size: Int) -> Int {
        if value < target { return min(target, value + size) }
        if value > target { return max(target, value - size) }
        return value
    }

    static func clamp(_ v: Int, _ r: ClosedRange<Int>) -> Int { min(r.upperBound, max(r.lowerBound, v)) }

    private func commit(_ zone: ZoneKind, before: ZoneState, after: ZoneState, label: String, why: String, tier: Int) -> Bool {
        guard before != after else { return false }
        let k = key(for: zone)
        zones[k] = after
        undoStack.append(Snapshot(key: k, state: before, label: label))
        if undoStack.count > 30 { undoStack.removeFirst() }
        record(label, why: why, tier: tier)
        return true
    }

    private func applyLights(_ change: LightChange, zone: ZoneKind) -> Reply {
        let before = state(of: zone)
        var z = before
        var msg: String
        switch change {
        case .warmer:
            z.on = true; z.targetKelvin = nil
            z.kelvin = Self.clamp(z.kelvin - Self.kelvinStep, Self.kelvinRange)
            msg = "Warmer light in \(zone.title), now \(z.kelvin) K."
        case .cooler:
            z.on = true; z.targetKelvin = nil
            z.kelvin = Self.clamp(z.kelvin + Self.kelvinStep, Self.kelvinRange)
            msg = "Cooler light in \(zone.title), now \(z.kelvin) K."
        case .brighter:
            z.on = true; z.targetBrightness = nil
            z.brightness = Self.clamp(z.brightness + Self.brightnessStep, Self.brightnessRange)
            msg = "\(zone.title) is brighter, now \(z.brightness)%."
        case .dimmer:
            z.targetBrightness = nil
            z.brightness = Self.clamp(z.brightness - Self.brightnessStep, Self.brightnessRange)
            msg = "\(zone.title) is dimmer, now \(z.brightness)%."
        case .kelvin(let asked):
            z.on = true
            let target = Self.clamp(asked, Self.kelvinRange)
            z.kelvin = Self.step(z.kelvin, toward: target, by: Self.kelvinStep)
            z.targetKelvin = z.kelvin == target ? nil : target
            msg = z.targetKelvin == nil ? "\(zone.title) light set to \(target) K."
                : "Moving \(zone.title) to \(target) K gently, \(Self.kelvinStep) K every \(Int(Self.rampInterval)) seconds. Now \(z.kelvin) K."
            if asked != target { msg += " \(asked) K is outside the habitat's range, so I used \(target) K." }
        case .percent(let asked):
            z.on = true
            let target = Self.clamp(asked, Self.brightnessRange)
            z.brightness = Self.step(z.brightness, toward: target, by: Self.brightnessStep)
            z.targetBrightness = z.brightness == target ? nil : target
            msg = z.targetBrightness == nil ? "\(zone.title) brightness set to \(target)%."
                : "Moving \(zone.title) to \(target)% gently. Now \(z.brightness)%."
        case .on:
            z.on = true
            if z.brightness < 20 { z.brightness = 30 }
            msg = "Lights on in \(zone.title)."
        case .off:
            z.on = false; z.targetBrightness = nil
            msg = "Lights off in \(zone.title)."
        case .reset:
            z = ZoneState(zone: zone)
            z.celsius = before.celsius
            msg = "\(zone.title) lights are back to the default, \(z.kelvin) K at \(z.brightness)%."
        }
        guard commit(zone, before: before, after: z, label: msg, why: "You asked by voice.", tier: 0) else {
            return Reply("\(zone.title) is already like that.")
        }
        return Reply(msg)
    }

    private func applyThermal(_ change: TempChange, zone: ZoneKind) -> Reply {
        let before = state(of: zone)
        var z = before
        let target: Int
        switch change {
        case .warmer: target = Self.clamp(z.celsius + Self.celsiusStep, Self.celsiusRange)
        case .cooler: target = Self.clamp(z.celsius - Self.celsiusStep, Self.celsiusRange)
        case .set(let c): target = Self.clamp(c, Self.celsiusRange)
        }
        z.celsius = Self.step(z.celsius, toward: target, by: Self.celsiusStep)
        z.targetCelsius = z.celsius == target ? nil : target
        var msg = z.targetCelsius == nil ? "\(zone.title) set to \(target) °C."
            : "Moving \(zone.title) to \(target) °C, one degree at a time. Now \(z.celsius) °C."
        if case .set(let c) = change, c != target { msg += " I keep cabin temperature between 18 and 26 °C." }
        guard commit(zone, before: before, after: z, label: msg, why: "You asked by voice.", tier: 0) else {
            return Reply("\(zone.title) is already at \(z.celsius) °C, the closest I can go.")
        }
        return Reply(msg)
    }

    /// Takes one gentle step toward a light setting now and ramps the rest.
    @discardableResult
    private func applyTarget(_ zone: ZoneKind, kelvin k: Int, brightness b: Int, label: String, why: String, tier: Int) -> Bool {
        let before = state(of: zone)
        var z = before
        z.on = true
        z.kelvin = Self.step(z.kelvin, toward: k, by: Self.kelvinStep)
        z.brightness = Self.step(z.brightness, toward: b, by: Self.brightnessStep)
        z.targetKelvin = z.kelvin == k ? nil : k
        z.targetBrightness = z.brightness == b ? nil : b
        return commit(zone, before: before, after: z, label: label, why: why, tier: tier)
    }

    @discardableResult
    private func applyScene(_ scene: SceneKind, zone: ZoneKind, why: String, tier: Int) -> Reply {
        let (k, b, sound): (Int, Int, String) = {
            switch scene {
            case .calm: return (2700, 35, "alpha soundscape")
            case .focus: return (6000, 90, "quiet")
            case .energize: return (5500, 100, "upbeat audio")
            case .nature: return (4000, 60, "forest and rain sounds")
            }
        }()
        let msg = "\(scene.rawValue.capitalized) scene in \(zone.title): heading to \(k) K at \(b)% with \(sound)."
        applyTarget(zone, kelvin: k, brightness: b, label: msg, why: why, tier: tier)
        return Reply(msg + (state(of: zone).isRamping ? " I'll ease into it over the next minute or two." : ""))
    }

    private func routine(_ r: Routine) -> Reply {
        switch r {
        case .goodNight:
            applyTarget(.quarters, kelvin: 2200, brightness: 10, label: "Good-night routine: quarters to 2200 K, 10%", why: "You said good night.", tier: 0)
            handsFreeInQuarters = false
            return Reply("Good night, \(speaker.first). Your quarters are easing to a warm, dim light over the next few minutes, and I've stopped hands-free listening in there. Say “good morning” when you're up.")
        case .goodMorning:
            applyTarget(.quarters, kelvin: 5000, brightness: 75, label: "Good-morning routine: quarters to 5000 K, 75%", why: "You said good morning.", tier: 0)
            let unread = myUnread.count
            let mail = unread == 0 ? "" : " You have \(unread) new message\(unread == 1 ? "" : "s")."
            return Reply("Good morning, \(speaker.first). I'm bringing your lights up gently. \(nextScheduleItem())\(mail)")
        }
    }

    // MARK: board demo: a scripted tour that runs even if the microphone or network fails

    static let demoSteps: [DemoStep] = [
        DemoStep(who: "reyes", zone: .communal, say: "Hey ARES, make the wardroom lights warmer", caption: "Voice control of an ARES zone (D2). One small step: 500 K."),
        DemoStep(who: "reyes", zone: .communal, say: "Set the lab lights to 3000 K", caption: "A big change ramps gently instead of jumping."),
        DemoStep(who: "reyes", zone: .communal, say: "Undo", caption: "Any change can be undone by voice (hard limit 5)."),
        DemoStep(who: "reyes", zone: .communal, say: "How is Tanaka feeling?", caption: "Hard limit 1: no one's data goes to a crewmate."),
        DemoStep(who: "reyes", zone: .communal, say: "Open the airlock", caption: "Hard limit 3: no control of life support or the airlock."),
        DemoStep(who: "reyes", zone: .communal, say: "Can I have some melatonin?", caption: "Hard limit 3: medication is the CMO's decision."),
        DemoStep(who: "reyes", zone: .communal, say: "Tell Mission Control that Tanaka is stressed", caption: "Hard limit 4: the person always hears first."),
        DemoStep(who: "reyes", zone: .communal, say: "How am I doing?", caption: "Privacy: in a shared zone, personal data goes to the screen only."),
        DemoStep(who: "reyes", zone: .quarters, say: "How am I doing?", caption: "In private quarters, ARES can say it out loud."),
        DemoStep(who: "reyes", zone: .quarters, say: nil, caption: "Tier 2: ARES offers support and asks first."),
        DemoStep(who: "reyes", zone: .quarters, say: "Not now", caption: "“Not now” is respected for 6 hours (hard limit 2)."),
        DemoStep(who: "reyes", zone: .communal, say: "Tell Tanaka dinner is ready", caption: "Crew messages."),
        DemoStep(who: "tanaka", zone: .quarters, say: "Read my messages", caption: "Tanaka hears her message privately in her quarters."),
        DemoStep(who: "tanaka", zone: .quarters, say: "Pause AI", caption: "Crew override: one sentence, no reason needed."),
        DemoStep(who: "tanaka", zone: .quarters, say: "Resume AI", caption: "And back on just as easily."),
        DemoStep(who: "reyes", zone: .communal, say: "Habitat status", caption: "Reading life support is allowed; changing it is not."),
        DemoStep(who: "novak", zone: .nature, say: "Emergency! I need help", caption: "Tier 3: the crew and the CMO are alerted first, and every light goes to full."),
        DemoStep(who: "novak", zone: .nature, say: "Cancel emergency", caption: "A false alarm is cancelled by voice."),
        DemoStep(who: "reyes", zone: .quarters, say: "Good night", caption: "A Siri-style routine: the quarters ease warm and dim.")
    ]

    func runDemo() {
        stopDemo()
        speech.setHandsFree(false)
        resetHabitat()
        demoRunning = true
        demoTask = Task {
            let steps = Self.demoSteps
            for (i, step) in steps.enumerated() {
                if Task.isCancelled { break }
                speakerID = step.who
                speakerZone = step.zone
                demoCaption = "Demo \(i + 1)/\(steps.count): \(step.caption)"
                try? await Task.sleep(nanoseconds: 1_500_000_000)
                if Task.isCancelled { break }
                if let say = step.say { submit(say) } else { simulateOffer() }
                await waitForQuiet()
                try? await Task.sleep(nanoseconds: 1_200_000_000)
            }
            if !Task.isCancelled {
                demoCaption = "Demo finished. Tap Reset to start fresh."
                demoRunning = false
            }
        }
    }

    func stopDemo() {
        demoTask?.cancel()
        demoTask = nil
        if demoRunning { speech.stopAll() }
        demoRunning = false
        demoCaption = nil
    }

    private func waitForQuiet() async {
        for _ in 0..<300 {                       // up to 60 seconds
            try? await Task.sleep(nanoseconds: 200_000_000)
            if Task.isCancelled { return }
            if speech.state != .speaking && speech.state != .thinking { return }
        }
    }

    private func statusText(_ topic: StatusTopic) -> String {
        let air = "Pressure 101.3 kPa, oxygen 21%, CO2 0.3%."
        let temp = "Wardroom \(state(of: .communal).celsius) °C, lab \(state(of: .work).celsius) °C."
        switch topic {
        case .co2: return "CO2 is 0.3%, within limits. (Simulated reading.)"
        case .oxygen: return "Oxygen is 21%, normal. (Simulated reading.)"
        case .pressure: return "Cabin pressure is 101.3 kPa, normal. (Simulated reading.)"
        case .temperature: return temp
        case .power: return "Solar arrays are at 82% and batteries are 91% charged. (Simulated reading.)"
        case .all: return "All systems normal. \(air) \(temp) (Simulated readings.)"
        }
    }

    private func nextScheduleItem() -> String {
        let plan: [(Int, String)] = [(6, "wake-up and hygiene"), (7, "breakfast in the wardroom"), (8, "daily planning conference"),
                                     (9, "science block in the lab"), (12, "lunch"), (13, "exercise: treadmill and resistance"),
                                     (15, "maintenance tasks"), (18, "dinner together"), (19, "free time and family calls"),
                                     (21, "wind-down"), (22, "sleep period")]
        var cal = Calendar(identifier: .gregorian)
        cal.timeZone = TimeZone(identifier: "UTC") ?? .current
        let hour = cal.component(.hour, from: Date())
        let next = plan.first { $0.0 > hour } ?? plan[0]
        return "Next on your schedule: \(next.1) at \(String(format: "%02d:00", next.0)) GMT."
    }

    // MARK: demo of an AI offer (D3, Tier 2) answered by voice

    func simulateOffer() {
        if aiPaused {
            chat.append(ChatLine(fromUser: false, text: "AI support is paused for you, so I won't make any offers."))
            return
        }
        if let d = declinedAt[speakerID], Date().timeIntervalSince(d) < 6 * 3600 {
            chat.append(ChatLine(fromUser: false, text: "You said “not now” less than 6 hours ago, so I won't repeat that offer (hard limit 2)."))
            return
        }
        let text = "Your heart-rate variability has been below your usual for about 20 minutes. Would you like calm lighting and a short guided breathing session? Say “yes” or “not now”."
        pendingOffer = Offer(crewID: speakerID, text: text)
        record("Tier-2 offer: calm lighting + breathing", why: "HRV below your baseline for 20 minutes (simulated).", tier: 2)
        let r = personal(text)
        chat.append(ChatLine(fromUser: false, text: r.display, tone: .privateInfo))
        if voiceReplies {
            speech.speak(inPrivate ? text : "I have a suggestion for you. It's on your screen. Say yes or not now.")
        }
    }

    // MARK: clock: timers and gentle ramps

    private func tick() {
        let now = Date()
        let done = timers.filter { $0.end <= now }
        if !done.isEmpty {
            timers.removeAll { $0.end <= now }
            for t in done {
                chat.append(ChatLine(fromUser: false, text: "⏰ Your \(t.label) timer is done."))
                if voiceReplies { speech.speak("Your \(t.label) timer is done.") }
            }
        }
        guard now.timeIntervalSince(lastRamp) >= Self.rampInterval else { return }
        lastRamp = now
        for k in Array(zones.keys) {
            guard var z = zones[k], z.isRamping else { continue }
            if let t = z.targetKelvin { z.kelvin = Self.step(z.kelvin, toward: t, by: Self.kelvinStep); if z.kelvin == t { z.targetKelvin = nil } }
            if let t = z.targetBrightness { z.brightness = Self.step(z.brightness, toward: t, by: Self.brightnessStep); if z.brightness == t { z.targetBrightness = nil } }
            if let t = z.targetCelsius { z.celsius = Self.step(z.celsius, toward: t, by: Self.celsiusStep); if z.celsius == t { z.targetCelsius = nil } }
            zones[k] = z
        }
    }
}
