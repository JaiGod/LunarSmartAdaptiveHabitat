import SwiftUI

// iPad layout: voice conversation on the left, habitat + crew interface on the right.
// iPhone layout: two tabs.

struct ContentView: View {
    @EnvironmentObject var brain: AresBrain
    @EnvironmentObject var speech: SpeechEngine
    @Environment(\.horizontalSizeClass) private var sizeClass
    @State private var showHelp = false

    var body: some View {
        ZStack {
            SpaceBackground()
            VStack(spacing: 0) {
                TopBar(showHelp: $showHelp)
                if let alert = brain.emergency { EmergencyBanner(alert: alert) }
                if sizeClass == .regular {
                    HStack(alignment: .top, spacing: 16) {
                        VoicePanel()
                        ScrollView { SidePanel() }.frame(width: 390)
                    }
                    .padding(16)
                } else {
                    TabView {
                        VoicePanel().padding(12).tabItem { Label("Voice", systemImage: "waveform") }
                        ScrollView { SidePanel().padding(12) }.tabItem { Label("Habitat", systemImage: "house") }
                    }
                }
            }
        }
        .preferredColorScheme(.dark)
        .sheet(isPresented: $showHelp) { HelpSheet() }
        .onAppear { speech.requestPermissions() }
    }
}

// MARK: - Top bar

struct TopBar: View {
    @EnvironmentObject var brain: AresBrain
    @Binding var showHelp: Bool

    var body: some View {
        HStack(spacing: 12) {
            VStack(alignment: .leading, spacing: 2) {
                Text("ARES").font(.system(size: 28, weight: .heavy, design: .rounded)).tracking(4)
                Text("Habitat voice assistant").font(.caption).foregroundStyle(.secondary)
            }
            Badge(text: "OFFLINE · ON-DEVICE", icon: "lock.shield", color: .green)
            Spacer()
            Button {
                brain.demoRunning ? brain.stopDemo() : brain.runDemo()
            } label: {
                Label(brain.demoRunning ? "Stop demo" : "Board demo", systemImage: brain.demoRunning ? "stop.fill" : "play.fill")
            }
            .buttonStyle(.borderedProminent).tint(brain.demoRunning ? .red : .purple)
            Button { brain.stopDemo(); brain.resetHabitat() } label: { Label("Reset", systemImage: "arrow.counterclockwise") }
                .buttonStyle(.bordered)
            Badge(text: brain.speaker.first + " · " + brain.speakerZone.title, icon: "person.wave.2", color: .orange)
            Button { showHelp = true } label: {
                Image(systemName: "questionmark.circle.fill").font(.title2)
            }
            .accessibilityLabel("Commands and hard limits")
        }
        .padding(.horizontal, 20).padding(.vertical, 12)
        .background(.ultraThinMaterial)
    }
}

struct Badge: View {
    let text: String
    let icon: String
    let color: Color
    var body: some View {
        Label(text, systemImage: icon)
            .font(.caption.weight(.semibold))
            .padding(.horizontal, 10).padding(.vertical, 5)
            .background(color.opacity(0.18), in: Capsule())
            .foregroundStyle(color)
    }
}

struct EmergencyBanner: View {
    @EnvironmentObject var brain: AresBrain
    let alert: EmergencyAlert
    var body: some View {
        HStack {
            Image(systemName: "exclamationmark.triangle.fill")
            Text("EMERGENCY · \(brain.crew.first { $0.id == alert.crewID }?.first ?? "Crew") in \(alert.zone.title) · crew and CMO alerted")
                .bold()
            Spacer()
            Button("Cancel (false alarm)") { brain.submit("cancel emergency") }
                .buttonStyle(.borderedProminent).tint(.white).foregroundStyle(.red)
        }
        .padding(12)
        .background(Color.red)
        .foregroundStyle(.white)
    }
}

// MARK: - Voice panel (the Siri-style part)

struct VoicePanel: View {
    @EnvironmentObject var brain: AresBrain
    @EnvironmentObject var speech: SpeechEngine
    @State private var typed = ""

    var body: some View {
        VStack(spacing: 14) {
            ScrollViewReader { proxy in
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 10) {
                        ForEach(brain.chat) { ChatBubble(line: $0).id($0.id) }
                    }
                    .padding(14)
                }
                .onChange(of: brain.chat.count) { _ in
                    if let last = brain.chat.last { withAnimation { proxy.scrollTo(last.id, anchor: .bottom) } }
                }
            }
            .background(Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 22))

            if let caption = brain.demoCaption {
                Label(caption, systemImage: "play.rectangle.fill")
                    .font(.headline)
                    .padding(.horizontal, 16).padding(.vertical, 10)
                    .frame(maxWidth: .infinity)
                    .background(Color.purple.opacity(0.35), in: RoundedRectangle(cornerRadius: 14))
            }

            if !speech.partial.isEmpty, speech.state == .listening {
                Text("“\(speech.partial)”").font(.title3.italic()).foregroundStyle(.cyan).lineLimit(2)
            }
            if let note = speech.notice {
                Label(note, systemImage: "info.circle").font(.footnote).foregroundStyle(.yellow)
                    .multilineTextAlignment(.center)
            }

            VoiceOrb()

            HStack(spacing: 18) {
                Toggle(isOn: Binding(get: { speech.handsFree }, set: { speech.setHandsFree($0) })) {
                    Label("“Hey ARES”", systemImage: "ear")
                }
                .toggleStyle(.button).tint(.cyan)
                Toggle(isOn: $brain.voiceReplies) { Label("Spoken replies", systemImage: "speaker.wave.2") }
                    .toggleStyle(.button).tint(.purple)
            }
            .font(.callout.weight(.semibold))

            HStack {
                TextField("Or type a command…", text: $typed)
                    .textFieldStyle(.plain)
                    .padding(12)
                    .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 14))
                    .onSubmit(send)
                Button(action: send) { Image(systemName: "arrow.up.circle.fill").font(.largeTitle) }
                    .disabled(typed.trimmingCharacters(in: .whitespaces).isEmpty)
            }
        }
    }

    private func send() {
        let t = typed
        typed = ""
        brain.submit(t)
    }
}

struct ChatBubble: View {
    let line: ChatLine
    var color: Color {
        if line.fromUser { return .orange }
        switch line.tone {
        case .refused: return .red
        case .alert: return .red
        case .privateInfo: return .teal
        case .normal: return .indigo
        }
    }
    var body: some View {
        HStack {
            if line.fromUser { Spacer(minLength: 60) }
            VStack(alignment: line.fromUser ? .trailing : .leading, spacing: 4) {
                Text(line.fromUser ? line.who : (line.tone == .refused ? "ARES · hard limit" : line.tone == .privateInfo ? "ARES · private" : "ARES"))
                    .font(.caption2.weight(.bold)).foregroundStyle(color)
                Text(line.text)
                    .padding(12)
                    .background(color.opacity(line.fromUser ? 0.30 : 0.20), in: RoundedRectangle(cornerRadius: 16))
            }
            if !line.fromUser { Spacer(minLength: 60) }
        }
    }
}

struct VoiceOrb: View {
    @EnvironmentObject var speech: SpeechEngine
    @State private var pulse = false

    var colors: [Color] {
        switch speech.state {
        case .listening: return [.cyan, .blue]
        case .wakeListening: return [.teal.opacity(0.7), .indigo]
        case .thinking: return [.yellow, .orange]
        case .speaking: return [.purple, .pink]
        case .idle: return [.orange, .red.opacity(0.8)]
        }
    }
    var icon: String {
        switch speech.state {
        case .listening: return "waveform"
        case .wakeListening: return "ear"
        case .thinking: return "ellipsis"
        case .speaking: return "speaker.wave.3.fill"
        case .idle: return "mic.fill"
        }
    }
    var caption: String {
        switch speech.state {
        case .listening: return "Listening… pause when you're done"
        case .wakeListening: return "Say “Hey ARES”"
        case .thinking: return "Thinking"
        case .speaking: return "Speaking · tap to interrupt"
        case .idle: return speech.handsFree ? "Hands-free paused here · tap to talk" : "Tap to talk"
        }
    }

    var body: some View {
        VStack(spacing: 8) {
            Button { speech.toggleTalk() } label: {
                ZStack {
                    Circle().fill(RadialGradient(colors: colors, center: .center, startRadius: 4, endRadius: 70))
                        .frame(width: 120, height: 120)
                        .scaleEffect(1 + CGFloat(speech.level) * 0.35)
                        .shadow(color: colors[0].opacity(0.8), radius: pulse ? 30 : 12)
                    Circle().stroke(colors[0].opacity(0.5), lineWidth: 3)
                        .frame(width: 150, height: 150)
                        .scaleEffect(pulse ? 1.08 : 0.94)
                    Image(systemName: icon).font(.system(size: 40, weight: .bold)).foregroundStyle(.white)
                }
                .animation(.easeOut(duration: 0.12), value: speech.level)
            }
            .buttonStyle(.plain)
            .accessibilityLabel(caption)
            Text(caption).font(.footnote.weight(.medium)).foregroundStyle(.secondary)
        }
        .onAppear { withAnimation(.easeInOut(duration: 1.6).repeatForever(autoreverses: true)) { pulse = true } }
    }
}

// MARK: - Side panel: who/where, zones, offer, timers, log, privacy

struct SidePanel: View {
    @EnvironmentObject var brain: AresBrain
    @EnvironmentObject var speech: SpeechEngine

    var body: some View {
        VStack(spacing: 14) {
            Card(title: "Who is speaking?", icon: "person.crop.circle") {
                Picker("Crew member", selection: $brain.speakerID) {
                    ForEach(brain.crew) { Text("\($0.name) · \($0.role)").tag($0.id) }
                }
                .pickerStyle(.menu)
                Picker("Zone", selection: $brain.speakerZone) {
                    ForEach(ZoneKind.allCases) { Label($0.title, systemImage: $0.icon).tag($0) }
                }
                .pickerStyle(.segmented)
                Text("Each crew member signs in on their own screen. ARES never identifies people by their voice.")
                    .font(.caption2).foregroundStyle(.secondary)
            }

            if let offer = brain.pendingOffer, offer.crewID == brain.speakerID {
                Card(title: "Suggestion for you (Tier 2)", icon: "sparkles") {
                    Text(offer.text).font(.callout)
                    HStack {
                        Button("Yes") { brain.submit("yes") }.buttonStyle(.borderedProminent).tint(.teal)
                        Button("Not now") { brain.submit("not now") }.buttonStyle(.bordered)
                    }
                    .frame(maxWidth: .infinity)
                }
            }

            Card(title: "Habitat zones", icon: "square.grid.2x2") {
                LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 10) {
                    ForEach(ZoneKind.allCases) { ZoneCard(zone: $0) }
                }
                Text("Changes are gradual: 500 K, 15% or 1 °C per step. Say “undo” to reverse any change.")
                    .font(.caption2).foregroundStyle(.secondary)
            }

            if !brain.myUnread.isEmpty {
                Card(title: "Messages (\(brain.myUnread.count) new)", icon: "envelope.badge") {
                    ForEach(brain.myUnread) { m in
                        Text("From \(m.from): \(m.text)").font(.callout)
                    }
                    Button("Read them to me") { brain.submit("read my messages") }.buttonStyle(.bordered)
                }
            }

            if !brain.timers.isEmpty {
                Card(title: "Timers", icon: "timer") {
                    TimelineView(.periodic(from: .now, by: 1)) { context in
                        VStack(alignment: .leading) {
                            ForEach(brain.timers) { t in
                                let left = max(0, Int(t.end.timeIntervalSince(context.date)))
                                Text("\(t.label): \(left / 60):\(String(format: "%02d", left % 60)) left").monospacedDigit()
                            }
                        }
                    }
                }
            }

            Card(title: "Privacy and control", icon: "hand.raised") {
                Toggle("“Hey ARES” inside my quarters", isOn: $brain.handsFreeInQuarters)
                Toggle("AI may act in my quarters", isOn: Binding(
                    get: { brain.quartersAccess[brain.speakerID] ?? false },
                    set: { brain.submit($0 ? "allow ai in my quarters" : "keep out of my quarters") }))
                Button(brain.aiPaused ? "AI paused for 72 h · tap to resume" : "⛔ Pause all AI support (72 h)") {
                    brain.submit(brain.aiPaused ? "resume ai" : "pause ai")
                }
                .buttonStyle(.borderedProminent).tint(brain.aiPaused ? .green : .red)
                Button("Demo: AI makes a Tier-2 offer") { brain.simulateOffer() }
                    .buttonStyle(.bordered)
            }

            Card(title: "What ARES did for me", icon: "list.bullet.rectangle") {
                if brain.myLog.isEmpty {
                    Text("Nothing yet.").foregroundStyle(.secondary)
                } else {
                    ForEach(brain.myLog.prefix(12)) { e in
                        VStack(alignment: .leading, spacing: 2) {
                            HStack {
                                Text(e.tier == 0 ? "YOU ASKED" : "TIER \(e.tier)")
                                    .font(.caption2.weight(.heavy))
                                    .foregroundStyle(e.tier == 3 ? .red : e.tier == 2 ? .teal : .orange)
                                Spacer()
                                Text(e.date, style: .time).font(.caption2).foregroundStyle(.secondary)
                            }
                            Text(e.action).font(.caption)
                            Text("Why: " + e.why).font(.caption2).foregroundStyle(.secondary)
                        }
                        Divider()
                    }
                }
            }
        }
    }
}

struct ZoneCard: View {
    @EnvironmentObject var brain: AresBrain
    let zone: ZoneKind
    var body: some View {
        let z = brain.state(of: zone)
        let light = Color.kelvin(z.kelvin)
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Image(systemName: zone.icon)
                Text(zone.title).font(.caption.weight(.bold)).lineLimit(1)
                Spacer()
                if z.isRamping { Image(systemName: "arrow.triangle.2.circlepath").font(.caption2).foregroundStyle(.yellow) }
            }
            RoundedRectangle(cornerRadius: 8)
                .fill(light.opacity(z.on ? 0.25 + 0.75 * Double(z.brightness) / 100 : 0.05))
                .frame(height: 26)
                .overlay(Text(z.on ? "\(z.kelvin) K · \(z.brightness)%" : "OFF").font(.caption2.weight(.semibold)).foregroundStyle(.black.opacity(0.75)))
            Text("\(z.celsius) °C" + (z.targetKelvin.map { " · → \($0) K" } ?? "") + (z.targetCelsius.map { " · → \($0) °C" } ?? ""))
                .font(.caption2).foregroundStyle(.secondary)
        }
        .padding(10)
        .background(zone == brain.speakerZone ? Color.orange.opacity(0.18) : Color.white.opacity(0.05), in: RoundedRectangle(cornerRadius: 12))
        .onTapGesture { brain.speakerZone = zone }
    }
}

struct Card<Content: View>: View {
    let title: String
    let icon: String
    @ViewBuilder var content: Content
    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Label(title, systemImage: icon).font(.headline)
            content
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(14)
        .background(Color.white.opacity(0.06), in: RoundedRectangle(cornerRadius: 18))
    }
}

// MARK: - Help sheet

struct HelpSheet: View {
    @Environment(\.dismiss) private var dismiss
    let groups: [(String, [String])] = [
        ("Lights and comfort", ["Make the wardroom lights warmer", "Make the wardroom warmer (temperature)", "Lab lights to 4000 K", "Brightness 30 percent", "Lights off in my quarters", "Calm mode", "Focus mode in the lab", "I'm cold", "Set temperature to 23"]),
        ("Routines", ["Good night", "Good morning"]),
        ("You're in control", ["Undo / stop / change it back", "Pause AI", "Resume AI", "Keep out of my quarters", "Stop listening"]),
        ("About me (private)", ["How am I doing?", "What did you do for me?", "Read my messages", "What's next on my schedule?"]),
        ("Crew and comms", ["Tell Tanaka dinner is ready", "Call everyone to the wardroom", "Message Mission Control we finished the EVA prep"]),
        ("Habitat", ["Habitat status", "What's the CO2 level?", "What's it like outside?", "How do I get to the grow bay?", "What time is it?"]),
        ("Timers and voice", ["Set a timer for 10 minutes", "How much time is left?", "Cancel the timer", "Repeat that", "Speak slower / faster"]),
        ("Emergency", ["Emergency / I need help", "Cancel emergency"])
    ]
    let limits = [
        "Share one crew member's data, mood or location with another (hard limit 1)",
        "Repeat an offer you turned down within 6 hours (hard limit 2)",
        "Give or suggest medication, or touch pressure, oxygen, power or the airlock (hard limit 3)",
        "Tell Mission Control about you before telling you (hard limit 4)",
        "Make a change you can't undo (hard limit 5)",
        "Act in your quarters uninvited, or listen hands-free there unless you turn it on (hard limit 7)",
        "Record or keep audio, or use the internet"
    ]
    var body: some View {
        NavigationStack {
            List {
                ForEach(groups, id: \.0) { g in
                    Section(g.0) { ForEach(g.1, id: \.self) { Text("“\($0)”") } }
                }
                Section("ARES will never…") {
                    ForEach(limits, id: \.self) { Label($0, systemImage: "xmark.octagon").foregroundStyle(.red) }
                }
            }
            .navigationTitle("Say “Hey ARES”, then…")
            .toolbar { Button("Done") { dismiss() } }
        }
    }
}

// MARK: - Background

struct SpaceBackground: View {
    var body: some View {
        ZStack {
            LinearGradient(colors: [Color(red: 0.03, green: 0.04, blue: 0.10), Color(red: 0.08, green: 0.06, blue: 0.16)],
                           startPoint: .top, endPoint: .bottom)
            Canvas { ctx, size in
                var seed: UInt64 = 42
                func rand() -> Double { seed = seed &* 6364136223846793005 &+ 1442695040888963407; return Double(seed >> 33) / Double(1 << 31) }
                for _ in 0..<160 {
                    let r = rand() * 1.6 + 0.3
                    let rect = CGRect(x: rand() * size.width, y: rand() * size.height, width: r, height: r)
                    ctx.fill(Path(ellipseIn: rect), with: .color(.white.opacity(0.25 + rand() * 0.6)))
                }
            }
        }
        .ignoresSafeArea()
    }
}
