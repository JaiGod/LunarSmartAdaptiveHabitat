# ARES Voice: offline voice assistant for the lunar habitat

A Siri-style voice command app for **Project ARES (NASA HUNCH)**, written in **Swift / SwiftUI** for iPad (iPhone works too).
It is a native app, not a web page: it has no HTML, CSS or JavaScript, needs no browser and needs **no internet connection**.

Credits: **Jaineil R.** · with Angel C. and Farhan K.

## Why it works in space
| Need | How ARES Voice does it |
|---|---|
| No internet on the Moon | Speech recognition runs **on the device** (`requiresOnDeviceRecognition = true`). If a device can't do that, voice is switched off instead of falling back to the cloud, and typed commands still work. |
| Speaking back | `AVSpeechSynthesizer`, built into iOS and offline. |
| Understanding commands | A rule-based parser (`CommandParser.swift`) that runs on the device, with no cloud AI. |
| Privacy | Audio is never recorded or saved. Only the command text goes into that crew member's own log. |

## How you talk to it (like Siri)
- **Tap the orb** and speak. ARES stops listening after you pause for 1.4 seconds.
- **"Hey ARES"** hands-free mode. Say "Hey ARES, make the wardroom warmer", or say "Hey ARES", wait for "Yes?", then give the command.
- Hands-free listening **pauses inside private quarters** unless that crew member turns it on.
- Tap ? in the app for the full command list.

## Board demo (for presenting)
Tap **▶ Board demo** in the top bar. ARES plays a 19-step tour: it switches between crew members and zones, types each command for you and answers out loud. A caption explains which rubric item each step shows. It covers zone control, gentle ramps, undo, every hard limit, private replies, a Tier-2 offer and "not now", crew messages, pausing the AI, habitat status, an emergency and its cancel, and the good-night routine.
The demo doesn't use the microphone, so it still works if the room is noisy or the mic or network fails. Tap **Stop demo** at any time, and **Reset** to start fresh.

## More things to say
Good night / good morning routines · "repeat that" · "speak slower / faster" · "how much time is left" / "cancel the timer" · "read my messages" · "how do I get to the grow bay" · "what's it like outside".

## How it maps to the ARES rubric
| Rubric item | What the voice feature adds |
|---|---|
| **D1 Sensors / privacy** | The microphone is a command input only. No voice-prosody mood scoring comes from commands, and nothing is stored. Personal answers are spoken aloud only in private quarters; in shared zones they go to the screen. |
| **D2 Zones** | All six ARES zone types can be controlled by voice, with the design's default lighting (2800 K wardroom, 6500 K lab, and so on). |
| **D3 Tiers** | Tier-2 offers are answered by voice ("yes" / "not now"). "Emergency" is a crew-triggered Tier 3: the crew and the CMO are alerted first, and Mission Control after. |
| **D4 Hard limits** | Refused by voice, with the reason: medication (limit 3), pressure / O₂ / power / airlock (limit 3), another crew member's data, mood or location (limit 1), reporting someone to Mission Control first (limit 4). A declined offer isn't repeated for 6 h (limit 2). Every change can be undone ("undo", limit 5). |
| **Bonus B Crew interface** | Voice versions of the crew panel: "what did you do for me", "how am I doing", "pause AI" (72 h, no reason needed), "keep out of my quarters". |

**Gentle changes.** Lights move at most 500 K or 15 % per step, and temperature 1 °C per step. A bigger request is ramped every 20 seconds instead of happening all at once. The rule is small, gentle, reversible changes.

## Run it on an iPad (no Mac needed)
1. Install **Swift Playgrounds** (free) from the App Store on the iPad.
2. Copy the `ARESVoice.swiftpm` folder to the iPad (Files app, iCloud Drive or OneDrive), then tap it to open it in Swift Playgrounds.
3. Press ▶︎ Run, and allow Microphone and Speech Recognition when asked.

On a Mac, the same folder opens in Xcode or Swift Playgrounds.
`.github/workflows/ios-build.yml` is an optional build check on GitHub's macOS runners. It hasn't been tested yet; see the note in the file.

## Files
- `ARESVoiceApp.swift`: app entry point
- `SpeechEngine.swift`: on-device listening, the "Hey ARES" wake word and spoken replies
- `CommandParser.swift`: turns words into commands, with safety checks first
- `AresBrain.swift`: carries out commands and enforces the hard limits, gentle ramps, undo and the log
- `ContentView.swift`: the iPad / iPhone interface
- `Models.swift`: zones, the crew (4 maximum), log entries and the light-colour maths

Crew wellness numbers and habitat readings are **simulated** for the demo.
