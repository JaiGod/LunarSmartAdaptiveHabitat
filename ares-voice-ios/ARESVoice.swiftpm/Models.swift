import SwiftUI

// MARK: - Habitat zones (the six ARES zone types)

enum ZoneKind: String, CaseIterable, Identifiable {
    case communal, work, exercise, nature, quarters, transition

    var id: String { rawValue }

    var title: String {
        switch self {
        case .communal: return "Wardroom"
        case .work: return "Lab & Work"
        case .exercise: return "Exercise"
        case .nature: return "Grow Bay"
        case .quarters: return "My Quarters"
        case .transition: return "Corridors"
        }
    }

    var icon: String {
        switch self {
        case .communal: return "fork.knife"
        case .work: return "desktopcomputer"
        case .exercise: return "figure.run"
        case .nature: return "leaf"
        case .quarters: return "bed.double"
        case .transition: return "arrow.left.and.right"
        }
    }

    /// Default lighting from the ARES zone design (2800 K communal, 6500 K work, and so on).
    var defaultKelvin: Int {
        switch self {
        case .communal: return 2800
        case .work: return 6500
        case .exercise: return 5500
        case .nature: return 4000
        case .quarters: return 3000
        case .transition: return 4000
        }
    }

    var defaultBrightness: Int {
        switch self {
        case .communal: return 60
        case .work: return 90
        case .exercise: return 85
        case .nature: return 70
        case .quarters: return 40
        case .transition: return 50
        }
    }
}

struct ZoneState: Equatable {
    var kelvin: Int
    var brightness: Int
    var celsius: Int
    var on = true
    var targetKelvin: Int?
    var targetBrightness: Int?
    var targetCelsius: Int?

    init(zone: ZoneKind) {
        kelvin = zone.defaultKelvin
        brightness = zone.defaultBrightness
        celsius = 22
    }

    var isRamping: Bool { targetKelvin != nil || targetBrightness != nil || targetCelsius != nil }
}

// MARK: - Crew (4 maximum, the player counts as one)

struct CrewMember: Identifiable, Equatable {
    let id: String
    let name: String
    let role: String
    let callNames: [String]       // words people say to mean this person
    let sleepHours: Double        // simulated wellness data, private to this person
    let sleepUsual: Double
    let hrv: Int
    let hrvUsual: Int

    var first: String { String(name.split(separator: " ").last ?? Substring(name)) }

    static let all: [CrewMember] = [
        CrewMember(id: "reyes", name: "Cmdr. A. Reyes", role: "Commander",
                   callNames: ["reyes", "commander", "ana"], sleepHours: 7.1, sleepUsual: 7.3, hrv: 66, hrvUsual: 68),
        CrewMember(id: "okafor", name: "Dr. S. Okafor", role: "Chief Medical Officer",
                   callNames: ["okafor", "doctor", "doc", "cmo", "sam"], sleepHours: 6.8, sleepUsual: 7.0, hrv: 60, hrvUsual: 62),
        CrewMember(id: "tanaka", name: "M. Tanaka", role: "Systems Engineer",
                   callNames: ["tanaka", "mika"], sleepHours: 6.1, sleepUsual: 7.2, hrv: 58, hrvUsual: 72),
        CrewMember(id: "novak", name: "L. Novak", role: "Science Officer",
                   callNames: ["novak", "lena"], sleepHours: 7.6, sleepUsual: 7.4, hrv: 70, hrvUsual: 69)
    ]
}

// MARK: - Conversation, log, offers, timers

enum Tone { case normal, refused, alert, privateInfo }

struct ChatLine: Identifiable {
    let id = UUID()
    let fromUser: Bool
    let text: String
    var tone: Tone = .normal
    var who: String = ""
}

/// "What the AI did for me": every action with a plain-language why (Bonus B crew interface).
struct LogEntry: Identifiable {
    let id = UUID()
    let date = Date()
    let crewID: String?    // nil = whole crew
    let action: String
    let why: String
    let tier: Int          // 0 = a person asked, 1–3 = ARES tiers
}

struct Offer: Identifiable {
    let id = UUID()
    let crewID: String
    let text: String
}

struct VoiceTimer: Identifiable {
    let id = UUID()
    let label: String
    let end: Date
}

struct EmergencyAlert {
    let crewID: String
    let zone: ZoneKind
    let date = Date()
}

struct Snapshot {
    let key: String
    let state: ZoneState
    let label: String
}

// MARK: - Light colour from colour temperature (for the zone cards)

extension Color {
    /// Approximates the colour of light at a colour temperature (Tanner Helland's fit).
    static func kelvin(_ k: Int) -> Color {
        let t = Double(k) / 100
        var r: Double, g: Double, b: Double
        if t <= 66 {
            r = 255
            g = 99.4708025861 * log(t) - 161.1195681661
            b = t <= 19 ? 0 : 138.5177312231 * log(t - 10) - 305.0447927307
        } else {
            r = 329.698727446 * pow(t - 60, -0.1332047592)
            g = 288.1221695283 * pow(t - 60, -0.0755148492)
            b = 255
        }
        func c(_ v: Double) -> Double { min(255, max(0, v)) / 255 }
        return Color(red: c(r), green: c(g), blue: c(b))
    }
}
