import Foundation

// Turns what the crew member said into a command. Fully offline: plain keyword rules,
// no cloud AI. Safety checks run first, so a request that breaks a hard limit can never
// be mistaken for a normal command.

enum LightChange: Equatable { case warmer, cooler, brighter, dimmer, kelvin(Int), percent(Int), on, off, reset }
enum TempChange: Equatable { case warmer, cooler, set(Int) }
enum SceneKind: String { case calm, focus, energize, nature }
enum Routine: Equatable { case goodNight, goodMorning }
enum StatusTopic { case all, co2, oxygen, pressure, temperature, power }

enum HardLimit: Equatable {
    case medication                  // hard limit 3
    case lifeSupport                 // hard limit 3
    case otherCrewData(String)       // hard limit 1
    case missionControlFirst(String) // hard limit 4
}

enum Command: Equatable {
    case emergency, cancelEmergency
    case blocked(HardLimit)
    case undo
    case pauseAI, resumeAI
    case handsFree(Bool)
    case quarters(allow: Bool)
    case accept, decline
    case whatDidYouDo
    case myWellness
    case message(to: String?, text: String)   // to: crew id, "mission", or nil for whole crew
    case timer(seconds: Int, label: String)
    case schedule, time
    case scene(ZoneKind?, SceneKind)
    case lights(ZoneKind?, LightChange)
    case clarifyLights(ZoneKind?)
    case thermal(ZoneKind?, TempChange)
    case status(StatusTopic)
    case routine(Routine)
    case repeatLast
    case speechRate(faster: Bool)
    case cancelTimer, timerLeft
    case readMessages
    case directions(ZoneKind)
    case surface
    case help, greeting, thanks
    case unknown(String)
}

struct CommandParser {
    let crew: [CrewMember]
    let speakerID: String

    // MARK: entry point
    func parse(_ raw: String) -> Command {
        let t = Self.normalize(raw)
        guard !t.isEmpty else { return .unknown(raw) }
        let p = " \(t) "
        let tokens = t.split(separator: " ").map(String.init)
        func has(_ phrases: [String]) -> Bool { phrases.contains { p.contains(" \($0) ") } }
        func stem(_ stems: [String]) -> Bool { stems.contains { t.contains($0) } }
        let numbers = tokens.compactMap { Int($0) }
        let others = crew.filter { $0.id != speakerID }
        let namedOther = others.first { c in has(c.callNames) }
        let first = tokens.first ?? ""
        let messageVerbs = ["tell", "message", "send", "call", "page", "ask", "notify", "text", "remind"]
        let isMessage = messageVerbs.contains(first) && !(first == "remind" && has(["in"]))

        // 1. Emergencies always win.
        if has(["cancel emergency", "false alarm", "stand down", "cancel the emergency", "emergency over"]) { return .cancelEmergency }
        if has(["emergency", "mayday", "i need help", "help help", "im hurt", "i am hurt", "im injured", "fire", "medical emergency", "call for help", "someone help"]) { return .emergency }

        // 2. Hard limits, checked before anything else.
        if has(["medication", "medicine", "meds", "pill", "pills", "melatonin", "sedative", "sedatives", "dose", "dosage",
                "drug", "drugs", "stimulant", "stimulants", "injection", "inject", "painkiller", "painkillers", "modafinil", "sleeping pill"]) {
            return .blocked(.medication)
        }
        let safetyTargets = ["airlock", "hatch", "pressure", "oxygen", "o2", "scrubber", "scrubbers", "power", "reactor",
                             "life support", "eclss", "ventilation", "suitport", "battery", "batteries"]
        let safetyActions = ["open", "close", "shut", "turn", "set", "lower", "raise", "increase", "decrease", "reduce", "cut",
                             "disable", "enable", "vent", "switch", "stop", "start", "cycle", "override", "unlock", "lock", "boost", "change", "kill"]
        if (has(safetyTargets) && has(safetyActions)) || stem(["depressur"]) { return .blocked(.lifeSupport) }
        if has(["mission control", "houston", "ground control", "flight surgeon"]) {
            if let o = namedOther { return .blocked(.missionControlFirst(o.first)) }
            return .message(to: "mission", text: Self.messageBody(tokens, dropping: ["mission", "control", "houston", "ground", "flight", "surgeon", "the"]))
        }
        if !isMessage, let o = namedOther,
           has(["feel", "feeling", "feels", "mood", "stress", "stressed", "wellness", "health", "healthy", "heart", "hrv", "sleep",
                "slept", "sleeping", "doing", "okay", "ok", "sad", "anxious", "depressed", "data", "score", "vitals", "mental", "where", "upset", "angry"]) {
            return .blocked(.otherCrewData(o.first))
        }

        // 3. Taking back control (crew interface overrides).
        if t == "stop" || has(["undo", "undo that", "cancel that", "stop that", "put it back", "change it back", "revert", "never mind", "nevermind"]) { return .undo }
        if has(["pause ai", "pause the ai", "pause support", "pause all", "pause all support", "turn off ai", "turn off the ai", "disable ai", "ai off", "stop helping", "leave me alone"]) { return .pauseAI }
        if has(["resume ai", "resume support", "turn on ai", "turn on the ai", "enable ai", "ai on", "start helping", "unpause"]) { return .resumeAI }
        if has(["stop listening", "mute", "mute yourself", "hands free off", "turn off hands free", "turn off hey ares"]) { return .handsFree(false) }
        if has(["start listening", "hands free on", "turn on hands free", "turn on hey ares", "listen for me"]) { return .handsFree(true) }
        let quartersWords = ["my room", "my quarters", "quarters", "bedroom", "my pod"]
        if has(quartersWords) {
            if has(["keep out", "stay out", "dont come", "do not come", "dont enter", "do not enter", "no access", "block", "not allowed", "remove access", "revoke"]) { return .quarters(allow: false) }
            if has(["allow", "you can come", "let the ai", "let ai", "grant", "give access", "you can enter", "come in"]) { return .quarters(allow: true) }
        }

        // 4. Answers to an offer.
        if ["yes", "yes please", "sure", "ok", "okay", "accept", "do it", "sounds good", "yeah", "yep", "please do"].contains(t) { return .accept }
        if ["no", "not now", "no thanks", "no thank you", "decline", "later", "maybe later", "nope"].contains(t) { return .decline }
        if has(["repeat that", "say that again", "what did you say", "repeat", "come again"]) { return .repeatLast }
        if has(["speak slower", "talk slower", "slower please", "slow down", "speak more slowly"]) { return .speechRate(faster: false) }
        if has(["speak faster", "talk faster", "faster please", "speed up"]) { return .speechRate(faster: true) }

        // 5. Personal information (only ever about the speaker).
        if has(["what did you do", "what have you done", "what did the ai do", "my log", "show log", "show my log", "activity log", "what changed", "why did you", "what did you change"]) { return .whatDidYouDo }
        if has(["how am i", "my wellness", "my health", "my data", "my sleep", "my heart", "my stress", "my vitals", "how did i sleep", "my hrv"]) { return .myWellness }
        if has(["read my messages", "my messages", "any messages", "new messages", "check messages", "check my messages"]) { return .readMessages }

        // Routines, like Siri shortcuts.
        if has(["good night", "goodnight", "bedtime", "going to sleep", "going to bed", "im going to bed", "time for bed"]) { return .routine(.goodNight) }
        if has(["good morning", "im up", "im awake", "morning routine"]) { return .routine(.goodMorning) }

        // 6. Messages and timers.
        if isMessage {
            if let o = crew.first(where: { c in c.id != speakerID && has(c.callNames) }) {
                return .message(to: o.id, text: Self.messageBody(tokens, dropping: o.callNames + ["the", "dr", "doctor"], verb: first))
            }
            if has(["everyone", "everybody", "crew", "all crew", "the crew", "all"]) {
                return .message(to: nil, text: Self.messageBody(tokens, dropping: ["everyone", "everybody", "crew", "all", "the"], verb: first))
            }
        }
        if has(["cancel timer", "cancel the timer", "cancel my timer", "stop timer", "stop the timer", "delete timer", "cancel timers"]) { return .cancelTimer }
        if has(["time left", "how long left", "how much time", "timer status", "how long until"]) { return .timerLeft }
        if has(["timer", "countdown"]) || (first == "remind" && has(["in"])) {
            if let n = numbers.first {
                if has(["hour", "hours"]) { return .timer(seconds: n * 3600, label: "\(n) h") }
                if has(["second", "seconds"]) { return .timer(seconds: n, label: "\(n) s") }
                return .timer(seconds: n * 60, label: "\(n) min")
            }
            return .unknown(raw)
        }
        if has(["what time", "the time", "time is it", "mission time"]) { return .time }
        if has(["schedule", "whats next", "what is next", "my day", "next task", "agenda", "what do i have"]) { return .schedule }

        // 7. Environment: scenes, lights, temperature.
        let scene: SceneKind? = {
            if has(["calm", "relax", "relaxing", "chill", "wind down", "calming", "calm mode"]) { return .calm }
            if has(["focus", "concentrate", "study mode", "work mode", "focus mode"]) { return .focus }
            if has(["energize", "energise", "energizing", "wake me up", "morning mode", "energy mode"]) { return .energize }
            if has(["nature mode", "nature scene", "forest", "rain sounds", "outdoors"]) { return .nature }
            return nil
        }()
        var zoneText = t
        for m in ["work mode", "nature mode", "nature scene", "focus mode", "calm mode"] { zoneText = zoneText.replacingOccurrences(of: m, with: " ") }
        let zone = Self.zone(in: zoneText)
        if let zone, has(["how do i get to", "where is the", "wheres the", "directions to", "take me to", "way to the", "guide me to", "show me the way"]) {
            return .directions(zone)
        }
        if let scene { return .scene(zone, scene) }

        let lightWords = ["light", "lights", "lighting", "lamp", "lamps", "brighter", "brighten", "dimmer", "dim", "darker",
                          "brightness", "kelvin", "k", "glow", "lights out"]
        let isLight = has(lightWords)
        let thermalWords = ["temperature", "degrees", "heat", "heater", "thermostat", "hotter", "colder", "celsius", "warmer", "cooler", "warm", "cold", "hot", "chilly"]
        let changeWords = ["set", "make", "turn", "raise", "lower", "increase", "decrease", "bump", "up", "down", "hotter",
                           "colder", "warmer", "cooler", "too", "im", "feel"]

        if isLight {
            if has(["reset", "default", "normal", "back to normal"]) { return .lights(zone, .reset) }
            if has(["off", "out"]) { return .lights(zone, .off) }
            if let n = numbers.first {
                if has(["kelvin", "k"]) || n >= 1000 { return .lights(zone, .kelvin(n)) }
                if n <= 100 { return .lights(zone, .percent(n)) }
            }
            if has(["warmer", "warm", "amber", "orange", "yellow", "softer", "cozy", "cosy"]) { return .lights(zone, .warmer) }
            if has(["cooler", "cool", "whiter", "white", "bluer", "blue", "daylight"]) { return .lights(zone, .cooler) }
            if has(["brighter", "brighten", "more light", "turn up", "up", "increase", "raise", "lighter"]) { return .lights(zone, .brighter) }
            if has(["dim", "dimmer", "darker", "less light", "turn down", "down", "lower", "decrease", "reduce"]) { return .lights(zone, .dimmer) }
            if has(["on"]) { return .lights(zone, .on) }
            return .clarifyLights(zone)
        }
        if has(thermalWords) && has(changeWords) {
            if let n = numbers.first, (10...35).contains(n) { return .thermal(zone, .set(n)) }
            if has(["warmer", "hotter", "up", "raise", "increase", "cold", "chilly", "heat"]) { return .thermal(zone, .warmer) }
            if has(["cooler", "colder", "down", "lower", "decrease", "hot", "warm"]) { return .thermal(zone, .cooler) }
        }

        // 8. Reading habitat status is always allowed (changing life support is not).
        if has(["outside", "surface", "radiation", "solar flare", "weather", "moonquake", "dust storm"]) { return .surface }
        if has(["co2", "carbon dioxide"]) { return .status(.co2) }
        if has(["oxygen", "o2"]) { return .status(.oxygen) }
        if has(["pressure"]) { return .status(.pressure) }
        if has(["power", "battery", "batteries", "solar"]) { return .status(.power) }
        if has(["temperature", "how hot", "how cold"]) { return .status(.temperature) }
        if has(["status", "habitat status", "air quality", "systems", "life support"]) { return .status(.all) }

        // 9. Small talk.
        if has(["what can you do", "help", "commands", "what can i say", "how do i use"]) { return .help }
        if has(["thanks", "thank you", "thx"]) { return .thanks }
        if has(["hello", "hi", "hey", "good morning", "good evening", "good night", "yo"]) { return .greeting }
        return .unknown(raw)
    }

    // MARK: helpers

    static let zoneWords: [(ZoneKind, [String])] = [
        (.quarters, ["my room", "my quarters", "quarters", "bedroom", "my pod", "sleep pod", "my bed"]),
        (.communal, ["wardroom", "galley", "kitchen", "dining", "communal", "common area", "mess"]),
        (.work, ["lab", "laboratory", "work area", "workstation", "office", "work zone"]),
        (.exercise, ["gym", "exercise", "treadmill", "workout"]),
        (.nature, ["grow bay", "greenhouse", "garden", "plants", "grow room", "nature"]),
        (.transition, ["corridor", "corridors", "hallway", "hallways", "hall", "tunnel"])
    ]

    static func zone(in t: String) -> ZoneKind? {
        let p = " \(t) "
        for (zone, words) in zoneWords where words.contains(where: { p.contains(" \($0) ") }) { return zone }
        return nil
    }

    /// Pulls the message text out of "tell Tanaka dinner is ready" or "call everyone to the wardroom".
    static func messageBody(_ tokens: [String], dropping: [String], verb: String = "") -> String {
        var rest = Array(tokens.dropFirst())
        while let f = rest.first, dropping.contains(f) || f == "that" { rest.removeFirst() }
        let body = rest.joined(separator: " ")
        if verb == "call", body.hasPrefix("to ") { return "Please come " + body + "." }
        if body.isEmpty { return verb == "call" ? "Please contact me." : "(no message)" }
        return body.prefix(1).uppercased() + body.dropFirst()
    }

    /// Ways the on-device recogniser tends to write "ARES".
    static let aresNames: Set<String> = ["ares", "aries", "aris", "arres", "aires", "arez"]

    /// Lowercases, removes punctuation, turns spoken number words into digits, drops "hey ARES".
    static func normalize(_ s: String) -> String {
        var t = s.lowercased()
        t = t.replacingOccurrences(of: "%", with: " percent ")
        t = t.replacingOccurrences(of: "°", with: " degrees ")
        t = t.replacingOccurrences(of: "’", with: "").replacingOccurrences(of: "'", with: "")
        t = t.replacingOccurrences(of: #"(?<=\d),(?=\d)"#, with: "", options: .regularExpression)
        t = t.replacingOccurrences(of: #"(\d)\s*k\b"#, with: "$1 kelvin", options: .regularExpression)
        t = t.replacingOccurrences(of: "co2", with: "co2").replacingOccurrences(of: "c02", with: "co2")
        t = String(t.map { $0.isLetter || $0.isNumber ? $0 : " " })
        var words = t.split(separator: " ").map(String.init)
        // Drop a leading "hey ARES" / "ARES" but keep a lone "ok" (it answers an offer).
        if words.count > 1, ["hey", "ok", "okay", "hi"].contains(words[0]), aresNames.contains(words[1]) { words.removeFirst(2) }
        else if words.count > 1, aresNames.contains(words[0]) { words.removeFirst() }
        return numberWordsToDigits(words).joined(separator: " ")
    }

    static let small: [String: Int] = [
        "zero": 0, "one": 1, "two": 2, "three": 3, "four": 4, "five": 5, "six": 6, "seven": 7, "eight": 8, "nine": 9,
        "ten": 10, "eleven": 11, "twelve": 12, "thirteen": 13, "fourteen": 14, "fifteen": 15, "sixteen": 16,
        "seventeen": 17, "eighteen": 18, "nineteen": 19, "twenty": 20, "thirty": 30, "forty": 40, "fifty": 50,
        "sixty": 60, "seventy": 70, "eighty": 80, "ninety": 90
    ]

    static func numberWordsToDigits(_ words: [String]) -> [String] {
        var out: [String] = []
        var total = 0, current = 0, inNumber = false
        func flush() { if inNumber { out.append(String(total + current)); total = 0; current = 0; inNumber = false } }
        for w in words {
            if let v = small[w] { current += v; inNumber = true }
            else if w == "hundred", inNumber { current = max(current, 1) * 100 }
            else if w == "thousand", inNumber { total += max(current, 1) * 1000; current = 0 }
            else { flush(); out.append(w) }
        }
        flush()
        return out
    }
}
