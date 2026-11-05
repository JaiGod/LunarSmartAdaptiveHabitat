import Foundation
import AVFoundation
import Speech

// Voice in and voice out, built to work with NO internet (the Moon has no Wi-Fi to Apple's servers).
//  • Recognition: Apple Speech with requiresOnDeviceRecognition = true, so audio never leaves the iPad.
//    If the device can't do on-device English, voice is switched off rather than falling back to the cloud.
//  • Speaking: AVSpeechSynthesizer, which is built into iOS and runs offline.
//  • Two ways to talk, like Siri: tap the orb (push-to-talk), or "Hey ARES" hands-free.
//  • Audio is never recorded or stored. Only the text of a command is kept, in the crew member's own log.

enum VoiceState: Equatable { case idle, listening, wakeListening, thinking, speaking }

@MainActor
final class SpeechEngine: NSObject, ObservableObject, AVSpeechSynthesizerDelegate {
    @Published private(set) var state: VoiceState = .idle
    @Published private(set) var partial = ""
    @Published private(set) var level: Float = 0
    @Published private(set) var handsFree = false
    @Published private(set) var permissionsOK = false
    @Published private(set) var onDeviceOK = true
    @Published var notice: String?

    /// Called with the words of a finished command.
    var onCommand: ((String) -> Void)?

    /// Set by ARES when hands-free listening must pause (for example, inside private quarters).
    var handsFreeSuspended = false {
        didSet {
            guard handsFreeSuspended != oldValue else { return }
            if handsFreeSuspended, state == .wakeListening { stopAudio(); state = .idle }
            if !handsFreeSuspended, handsFree, state == .idle { startWakeSession() }
        }
    }

    private enum Mode { case push, wake }
    private enum VoiceError: LocalizedError {
        case noRecognizer, noMicrophone
        var errorDescription: String? {
            switch self {
            case .noRecognizer: return "Speech recognition isn't available for English on this device."
            case .noMicrophone: return "No microphone input was found."
            }
        }
    }

    private let recognizer = SFSpeechRecognizer(locale: Locale(identifier: "en-US"))
    private let audioEngine = AVAudioEngine()
    private let synth = AVSpeechSynthesizer()
    private var request: SFSpeechAudioBufferRecognitionRequest?
    private var task: SFSpeechRecognitionTask?
    private var sessionID = 0
    private var mode: Mode = .push
    private var silenceTask: Task<Void, Never>?
    private var noSpeechTask: Task<Void, Never>?
    private var restartTask: Task<Void, Never>?
    private var listenAfterSpeaking = false

    /// Habitat words the recogniser should expect.
    private let vocabulary = ["ARES", "Hey ARES", "Okafor", "Tanaka", "Novak", "Reyes", "wardroom", "grow bay",
                              "airlock", "kelvin", "CO2", "Mission Control", "quarters", "CMO"]

    override init() {
        super.init()
        synth.delegate = self
    }

    var canListen: Bool { permissionsOK && onDeviceOK && (recognizer?.isAvailable ?? false) }

    // MARK: permissions

    func requestPermissions() {
        SFSpeechRecognizer.requestAuthorization { status in
            Task { @MainActor in
                guard status == .authorized else {
                    self.permissionsOK = false
                    self.notice = "Speech recognition is off. Allow it in Settings, or type commands below."
                    return
                }
                AVAudioSession.sharedInstance().requestRecordPermission { granted in
                    Task { @MainActor in
                        self.permissionsOK = granted
                        self.onDeviceOK = self.recognizer?.supportsOnDeviceRecognition ?? false
                        if !granted {
                            self.notice = "Microphone access is off. Allow it in Settings, or type commands below."
                        } else if !self.onDeviceOK {
                            self.notice = "This device can't recognise English speech offline. ARES never sends audio off the habitat, so voice is off here. Type commands instead."
                        }
                    }
                }
            }
        }
    }

    // MARK: controls

    /// The orb button: start talking, stop talking, or interrupt ARES while it speaks.
    func toggleTalk() {
        switch state {
        case .listening:
            if mode == .push { finishPush() } else { stopAudio(); state = .idle; resumeHandsFree() }
        case .speaking:
            state = .idle
            synth.stopSpeaking(at: .immediate)
            startPush()
        default:
            startPush()
        }
    }

    func setHandsFree(_ on: Bool) {
        handsFree = on
        if on {
            if state == .idle { startWakeSession() }
        } else if state == .wakeListening || (state == .listening && mode == .wake) {
            stopAudio(); state = .idle
        }
    }

    func speak(_ text: String) {
        stopAudio()
        let session = AVAudioSession.sharedInstance()
        try? session.setCategory(.playAndRecord, mode: .default, options: [.defaultToSpeaker, .duckOthers, .allowBluetooth])
        try? session.setActive(true)
        let u = AVSpeechUtterance(string: text)
        u.voice = AVSpeechSynthesisVoice(language: "en-US")
        u.rate = AVSpeechUtteranceDefaultSpeechRate
        state = .speaking
        synth.speak(u)
    }

    /// When a reply is shown but not spoken.
    func finishedWithoutSpeaking() {
        if state == .thinking { state = .idle }
        resumeHandsFree()
    }

    // MARK: sessions

    private func startPush() {
        guard canListen else {
            if notice == nil { notice = "Voice isn't available right now. Type a command below." }
            return
        }
        mode = .push
        do {
            try startSession()
            state = .listening
            armNoSpeech()
        } catch { fail(error) }
    }

    private func startWakeSession() {
        guard handsFree, !handsFreeSuspended, canListen, state == .idle else { return }
        mode = .wake
        do {
            try startSession()
            state = .wakeListening
            scheduleRestart()
        } catch { fail(error) }
    }

    private func resumeHandsFree() {
        if handsFree, state == .idle { startWakeSession() }
    }

    private func startSession() throws {
        stopAudio()
        guard let recognizer else { throw VoiceError.noRecognizer }

        let session = AVAudioSession.sharedInstance()
        try session.setCategory(.playAndRecord, mode: .default, options: [.defaultToSpeaker, .duckOthers, .allowBluetooth])
        try session.setActive(true, options: .notifyOthersOnDeactivation)

        let req = SFSpeechAudioBufferRecognitionRequest()
        req.shouldReportPartialResults = true
        req.requiresOnDeviceRecognition = true      // never send audio to a server
        req.taskHint = .search                      // short commands, not dictation
        req.contextualStrings = vocabulary
        request = req

        sessionID += 1
        let id = sessionID
        partial = ""

        let input = audioEngine.inputNode
        let format = input.outputFormat(forBus: 0)
        guard format.sampleRate > 0, format.channelCount > 0 else { throw VoiceError.noMicrophone }
        input.installTap(onBus: 0, bufferSize: 1024, format: format,
                         block: SpeechEngine.makeTap(request: req) { [weak self] value in
                             Task { @MainActor in self?.level = value }
                         })
        audioEngine.prepare()
        try audioEngine.start()

        task = recognizer.recognitionTask(with: req) { [weak self] result, error in
            let text = result?.bestTranscription.formattedString
            let failed = error != nil
            Task { @MainActor in self?.received(text: text, failed: failed, id: id) }
        }
    }

    /// Runs on the audio thread: feeds the recogniser and measures loudness for the orb.
    nonisolated private static func makeTap(request: SFSpeechAudioBufferRecognitionRequest,
                                            level: @escaping (Float) -> Void) -> AVAudioNodeTapBlock {
        var count = 0
        return { buffer, _ in
            request.append(buffer)
            count += 1
            guard count % 3 == 0, let data = buffer.floatChannelData?[0] else { return }
            let n = Int(buffer.frameLength)
            guard n > 0 else { return }
            var sum: Float = 0
            for i in 0..<n { sum += data[i] * data[i] }
            level(min(1, (sum / Float(n)).squareRoot() * 12))
        }
    }

    private func stopAudio() {
        silenceTask?.cancel(); noSpeechTask?.cancel(); restartTask?.cancel()
        if audioEngine.isRunning { audioEngine.stop() }
        audioEngine.inputNode.removeTap(onBus: 0)
        request?.endAudio()
        task?.cancel()
        task = nil
        request = nil
        sessionID += 1          // ignore late callbacks from the old session
        level = 0
    }

    private func received(text: String?, failed: Bool, id: Int) {
        guard id == sessionID else { return }
        if let text, !text.isEmpty {
            switch mode {
            case .push:
                partial = text
                noSpeechTask?.cancel()
                armSilence { [weak self] in self?.finishPush() }
            case .wake:
                if let command = Self.afterWakeWord(text) {
                    partial = command.isEmpty ? "…" : command
                    state = .listening
                    armSilence { [weak self] in self?.finishWake(command) }
                } else if text.count > 140 {
                    restartWake()   // long conversation with no "ARES" in it: start fresh
                }
            }
        }
        if failed {
            if mode == .wake, handsFree, state != .speaking { restartWake(after: 0.5) }
            else if state == .listening {
                if partial.isEmpty { stopAudio(); state = .idle } else { finishPush() }
            }
        }
    }

    /// A command is finished when the speaker has been quiet for 1.4 seconds.
    private func armSilence(_ action: @escaping @MainActor () -> Void) {
        silenceTask?.cancel()
        silenceTask = Task {
            try? await Task.sleep(nanoseconds: 1_400_000_000)
            guard !Task.isCancelled else { return }
            action()
        }
    }

    private func armNoSpeech() {
        noSpeechTask?.cancel()
        noSpeechTask = Task { [weak self] in
            try? await Task.sleep(nanoseconds: 7_000_000_000)
            guard !Task.isCancelled, let self, self.state == .listening, self.partial.isEmpty else { return }
            self.stopAudio()
            self.state = .idle
            self.notice = "I didn't hear anything. Tap the orb and try again."
            self.resumeHandsFree()
        }
    }

    private func finishPush() {
        let text = partial.trimmingCharacters(in: .whitespacesAndNewlines)
        stopAudio()
        guard !text.isEmpty else { state = .idle; resumeHandsFree(); return }
        notice = nil
        state = .thinking
        onCommand?(text)
    }

    private func finishWake(_ command: String) {
        stopAudio()
        if command.trimmingCharacters(in: .whitespaces).isEmpty {
            listenAfterSpeaking = true      // "Hey ARES" … pause … then the command, like Siri
            speak("Yes?")
        } else {
            state = .thinking
            onCommand?(command)
        }
    }

    /// The on-device recogniser stops after about a minute, so hands-free restarts itself every 50 s.
    private func scheduleRestart() {
        restartTask?.cancel()
        restartTask = Task { [weak self] in
            try? await Task.sleep(nanoseconds: 50_000_000_000)
            guard !Task.isCancelled, let self, self.mode == .wake, self.state == .wakeListening else { return }
            self.restartWake()
        }
    }

    private func restartWake(after delay: Double = 0) {
        stopAudio()
        state = .idle
        guard handsFree else { return }
        restartTask = Task { [weak self] in
            if delay > 0 { try? await Task.sleep(nanoseconds: UInt64(delay * 1_000_000_000)) }
            guard !Task.isCancelled else { return }
            self?.startWakeSession()
        }
    }

    private func fail(_ error: Error) {
        stopAudio()
        state = .idle
        notice = "Voice problem: \(error.localizedDescription) You can still type commands."
    }

    /// Returns what was said after "ARES", or nil if the wake word wasn't heard.
    static func afterWakeWord(_ text: String) -> String? {
        let lower = text.lowercased()
        let pattern = "\\b(" + CommandParser.aresNames.joined(separator: "|") + ")\\b"
        guard let r = lower.range(of: pattern, options: .regularExpression) else { return nil }
        var rest = Substring(lower[r.upperBound...])
        while let c = rest.first, !(c.isLetter || c.isNumber) { rest = rest.dropFirst() }
        return String(rest)
    }

    // MARK: AVSpeechSynthesizerDelegate

    nonisolated func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didFinish utterance: AVSpeechUtterance) {
        Task { @MainActor in self.finishedSpeaking() }
    }

    nonisolated func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didCancel utterance: AVSpeechUtterance) {
        Task { @MainActor in self.finishedSpeaking() }
    }

    private func finishedSpeaking() {
        guard state == .speaking, !synth.isSpeaking else { return }
        state = .idle
        if listenAfterSpeaking {
            listenAfterSpeaking = false
            startPush()
            return
        }
        resumeHandsFree()
    }
}
