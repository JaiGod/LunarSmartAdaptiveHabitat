# Health-monitoring hardware (Blender)

Six devices that carry the nine D1 sensors, modelled in Blender by `health_models.py` (run it with
`blender -b --python health_models.py`; it builds each model from scratch and exports a `.glb`).
The exported models are in [`models/health/`](../../models/health/) and can be spun in the browser on the
[3D model library](https://jaigod.github.io/LunarSmartAdaptiveHabitat/models.html#health).

| Model | D1 sensors | What it does for the crew |
|---|---|---|
| Crew beacon (chest) | 1 HRV, 2 skin response, 6 location | Every heartbeat from a 2-lead ECG → heart rate + HRV; stress peaks; which room you're in |
| Night wristband | 3 sleep | Sleep stages (light/deep/REM), overnight heart rate |
| Cortisol patch | 7 cortisol | Chronic stress hormone, 4× a day for 24–48 h |
| Zone sensor pod | 4 voice, 5 face, 6 UWB, 8 CO2 | Paired CO2 sensors in every zone; voice + face only in shared zones, processed on the device |
| Crew Health Station | (medical) | Daily check-in: live ECG, blood pressure, SpO2, temperature |
| Smart sleep bunk | 3 sleep | Under-mattress sleep sensing, circadian mood light, privacy curtain |

© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
