# Wellness, Medical & Life-Support Asset Pack

23 game-ready Blender models for the Lunar Smart Adaptive Habitat (PROJECT ARES · NASA HUNCH). They are built procedurally by
[`wellness_models.py`](wellness_models.py) (Blender 5.2) at real-world scale in metres, and rendered by [`wellness_render.py`](wellness_render.py).

**Design rule: every object earns its place.** A lunar habitat has no decorative props, because every kilogram is launched from Earth. Each asset is
a working system in the realistic game (`Unity_Game_Moon_Base_Final/Assets/Unity3/Scripts/04_Habitat/SmartObjects.cs`):

* **Smart highlight:** look at an object and it outlines in its category colour with a holographic label (green = life support,
  blue = psychological health, yellow = operations, purple = medical, red = emergency).
* **Context actions:** E / F / R / G do real things, such as replacing the scrubber filter (CO₂ drops), harvesting the greenhouse, running a recovery session, or opening the private crisis line.
  Each action changes crew stress, mood, fatigue and sleep, or the habitat telemetry.
* **TAB diagnostics:** live telemetry and AI recommendations.
* **Hold right mouse to scan:** an engineering inspection showing integrity, days since last maintenance and the AI failure prediction. Press E while scanning to service the system.
* **Wear:** systems lose about 1.2 % integrity per mission day. Below 35 % they give half their benefit until serviced, and the maintenance drone repairs the most worn one.
* **Adaptive Recovery Pod:** the model swaps between three AI states (healthy green, fatigue yellow, stress blue) from the player's own readings.
* **Placement:** each system is placed in its functional zone (medical bay, ECLSS, greenhouse, command, VR retreat, lounge). Free-standing systems face the room, wall units mount on walls,
  and walkways and doorways are kept clear.

In **Lunar Habitat Commander** the Recovery Pod replaces the four existing beds; it does not add more.

Material contract: `EMIT_*` = emissive light, `GLASS_*` = transparent; everything else is opaque PBR. An empty called `FRONT_MARKER` sits
1 m in front of each asset so the game knows which way it faces.

| Render | Asset | Category | Size (m) | Triangles | File |
|---|---|---|---|---|---|
| ![](renders/ai_wristband.jpg) | **AI Psychological Monitoring Wristband** | Psychological health | 0.08 × 0.04 × 0.09 | 10,408 | [glb](../../models/wellness/ai_wristband.glb) |
| ![](renders/sanctuary_pod.jpg) | **Personal Sanctuary Relaxation Pod** | Psychological health | 2.5 × 2.5 × 2.16 | 26,442 | [glb](../../models/wellness/sanctuary_pod.glb) |
| ![](renders/greenhouse.jpg) | **Modular Lunar Greenhouse** | Life support | 3.2 × 2.2 × 2.49 | 51,212 | [glb](../../models/wellness/greenhouse.glb) |
| ![](renders/medical_scanner.jpg) | **Smart Medical Scanner Bay** | Medical | 2.7 × 1.95 × 2.45 | 54,244 | [glb](../../models/wellness/medical_scanner.glb) |
| ![](renders/hologram_companion.jpg) | **AI Companion Hologram Projector** | Psychological health | 1.25 × 1.44 × 1.86 | 15,684 | [glb](../../models/wellness/hologram_companion.glb) |
| ![](renders/circadian_light.jpg) | **Adaptive Circadian Lighting System** | Psychological health | 0.6 × 0.41 × 0.54 | 11,264 | [glb](../../models/wellness/circadian_light.glb) |
| ![](renders/emotional_garden.jpg) | **Emotional Support Garden Module** | Psychological health | 3.03 × 1.58 × 1.72 | 19,748 | [glb](../../models/wellness/emotional_garden.glb) |
| ![](renders/eclss_core.jpg) | **ECLSS Core Life Support Machine** | Life support | 2.0 × 1.2 × 2.17 | 26,460 | [glb](../../models/wellness/eclss_core.glb) |
| ![](renders/maintenance_drone.jpg) | **Autonomous Maintenance Drone** | Operations | 0.85 × 0.85 × 0.57 | 13,588 | [glb](../../models/wellness/maintenance_drone.glb) |
| ![](renders/habitat_console.jpg) | **Smart Habitat Control Console** | Operations | 1.8 × 1.39 × 1.39 | 26,220 | [glb](../../models/wellness/habitat_console.glb) |
| ![](renders/emergency_mh_station.jpg) | **Emergency Mental Health Station** | Emergency | 1.38 × 1.12 × 2.12 | 17,656 | [glb](../../models/wellness/emergency_mh_station.glb) |
| ![](renders/sensor_wall.jpg) | **Biometrics Sensor Wall Array** | Life support | 2.4 × 0.18 × 1.37 | 19,072 | [glb](../../models/wellness/sensor_wall.glb) |
| ![](renders/earth_window.jpg) | **Virtual Earth Window System** | Psychological health | 1.9 × 0.31 × 1.25 | 9,428 | [glb](../../models/wellness/earth_window.glb) |
| ![](renders/recovery_pod_healthy.jpg) | **Adaptive Recovery Sleep Pod · healthy** | Psychological health | 3.17 × 1.73 × 2.17 | 21,132 | [glb](../../models/wellness/recovery_pod_healthy.glb) |
| ![](renders/recovery_pod_fatigue.jpg) | **Adaptive Recovery Sleep Pod · fatigue** | Psychological health | 3.17 × 1.71 × 2.14 | 34,548 | [glb](../../models/wellness/recovery_pod_fatigue.glb) |
| ![](renders/recovery_pod_stress.jpg) | **Adaptive Recovery Sleep Pod · stress** | Psychological health | 3.17 × 1.58 × 2.14 | 27,328 | [glb](../../models/wellness/recovery_pod_stress.glb) |
| ![](renders/stress_lounge.jpg) | **Stress Recovery Lounge** | Psychological health | 3.33 × 3.45 × 2.32 | 13,984 | [glb](../../models/wellness/stress_lounge.glb) |
| ![](renders/oxygen_tanks.jpg) | **Oxygen Tank Rack** | Life support | 1.86 × 0.56 × 1.85 | 47,348 | [glb](../../models/wellness/oxygen_tanks.glb) |
| ![](renders/ai_health_hub.jpg) | **AI Health Detection System** | Medical | 2.12 × 0.83 × 2.07 | 29,116 | [glb](../../models/wellness/ai_health_hub.glb) |
| ![](renders/mood_tracker.jpg) | **Mood Health Tracker** | Psychological health | 0.7 × 0.14 × 1.0 | 14,632 | [glb](../../models/wellness/mood_tracker.glb) |
| ![](renders/heart_rate_monitor.jpg) | **Heart-Rate & Vitals Monitor** | Medical | 0.6 × 0.63 × 1.58 | 11,136 | [glb](../../models/wellness/heart_rate_monitor.glb) |
| ![](renders/bp_monitor.jpg) | **Blood-Pressure Tracker** | Medical | 0.5 × 0.42 × 0.86 | 28,152 | [glb](../../models/wellness/bp_monitor.glb) |
| ![](renders/smart_bed.jpg) | **AI Sleep-Detection Bed** | Psychological health | 2.25 × 1.58 × 2.07 | 19,188 | [glb](../../models/wellness/smart_bed.glb) |

© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
