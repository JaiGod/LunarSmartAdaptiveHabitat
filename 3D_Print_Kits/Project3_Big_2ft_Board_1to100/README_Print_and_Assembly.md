# 2 ft x 2 ft Physical Model — Print & Assembly Guide (scale 1:100)
Board: 610 x 610 mm (2 ft) wood/foam board. Habitat footprint at 1:100 ≈ 420 x 580 mm, centred.
All STL files are in millimetres and keep their real positions: import into the slicer with
"keep original positions / import as assembly" and every part lands in the right spot.

## Print list (PLA, 0.4 mm nozzle, 0.2 mm layers, walls already thickened to ≥1.6 mm)
01 Core lower deck (rigid)                 02 Core upper inflatable  ← REMOVABLE ROOF
03 Upper-deck insert: 4 pods + wardroom     04 Lower-deck insert: hygiene/ECLSS/storage rooms
05 Airlock/EVA module   06 Lab   07 Greenhouse   08 Wellness
09–13 Nodes: Command, Maintenance, Medical+Storm Shelter, Recreation Commons, Crew Support
      each with its own *_ROOF_removable dome (lift off to see inside)
14 Loop hallways        15 Regolith shields (optional, lift-off segments)
16 Rover, LTV, comm tower, radiators
00 Board reference — do NOT print (alignment only)

## Electronics (separate box under/beside the board — not part of the building, per HUNCH slides)
| Function (rubric link)                    | Part                                  | Qty |
|-------------------------------------------|---------------------------------------|-----|
| Main controller + Wi-Fi to dashboard       | ESP32 DevKit (or Arduino Mega 2560 + ESP-01) | 1 |
| Pod mood lights (one per bedroom)          | WS2812B LED strip, 10 LEDs per pod     | 4   |
| Wearable beacon mock (HR/HRV)              | MAX30102 pulse/SpO2 sensor             | 1   |
| Wearable stress (GSR)                      | Grove GSR sensor                       | 1   |
| Beacon mood ring                           | WS2812 12-LED ring                     | 1   |
| CO2 / air quality (cognitive fatigue)      | Sensirion SCD40 (I2C)                  | 1   |
| Temp / humidity                            | BME280                                 | 1   |
| Motion / door occupancy                    | PIR HC-SR501 (or RCWL-0516)            | 2   |
| Voice level (prosody proxy, no recording)  | MAX9814 or INMP441 mic                 | 1   |
| Pod door lock                              | SG90 micro servo                       | 1   |
| Soundscapes / alerts                       | DFPlayer Mini + 3 W speaker            | 1   |
| Crew interface screen                      | 0.96" SSD1306 OLED                     | 2   |
| Tier-3 CMO alert light                     | Red LED + 220 Ω                        | 1   |
| Power                                      | 5 V 4 A supply, breadboard, jumpers    | 1   |
Route LED strips through 3 mm holes drilled in pod ceilings of part 03; sensors sit in the
electronics box with leads to the model. Live readings feed the same dashboard as the Unity sim.
