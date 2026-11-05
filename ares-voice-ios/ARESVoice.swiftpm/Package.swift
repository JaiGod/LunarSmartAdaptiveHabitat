// swift-tools-version: 5.8

// ARES Voice: offline voice assistant for the Project ARES lunar habitat.
// Opens directly in Swift Playgrounds (iPad or Mac) and in Xcode.

import PackageDescription
import AppleProductTypes

let package = Package(
    name: "ARES Voice",
    platforms: [
        .iOS("16.0")
    ],
    products: [
        .iOSApplication(
            name: "ARES Voice",
            targets: ["AppModule"],
            bundleIdentifier: "com.jaineilr.aresvoice",
            teamIdentifier: "",
            displayVersion: "1.0",
            bundleVersion: "1",
            appIcon: .placeholder(icon: .rocket),
            accentColor: .presetColor(.orange),
            supportedDeviceFamilies: [
                .pad,
                .phone
            ],
            supportedInterfaceOrientations: [
                .portrait,
                .landscapeRight,
                .landscapeLeft,
                .portraitUpsideDown(.when(deviceFamilies: [.pad]))
            ],
            capabilities: [
                .microphone(purposeString: "ARES listens only while you talk to it. Audio stays on this device and is never saved."),
                .speechRecognition(purposeString: "ARES turns your voice commands into text on this device, with no internet connection.")
            ]
        )
    ],
    targets: [
        .executableTarget(
            name: "AppModule",
            path: "."
        )
    ]
)
