import SwiftUI

// Credits: Jaineil R. · with Angel C. and Farhan K. · NASA HUNCH Project ARES

@main
struct ARESVoiceApp: App {
    @StateObject private var brain = AresBrain()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(brain)
                .environmentObject(brain.speech)
        }
    }
}
