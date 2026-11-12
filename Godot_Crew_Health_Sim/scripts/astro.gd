# ==============================================================================================
#  PROJECT ARES · NASA HUNCH 2025–26 · Crew Health Sim (Godot 4, desktop + web)
#  File    : scripts/astro.gd
#  Purpose : The astronaut's body and mind, in simulated minutes. Everything the six health devices
#            read comes from here: sleep pressure + circadian clock, heart rate, HRV, blood pressure,
#            SpO2, core/skin temperature, cortisol, cabin CO2 exposure, stress and mood (valence/arousal).
#  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
# ==============================================================================================
class_name Astro
extends RefCounted

# ---- clock ---------------------------------------------------------------------------------
var minute = 7.0 * 60.0          # minutes since mission day 1, 00:00 (start 07:00)
func day() -> int: return int(minute / 1440.0) + 1
func hour() -> float: return fmod(minute / 60.0, 24.0)
func clock_text() -> String:
	var h = int(hour()); var m = int(fmod(minute, 60.0))
	return "Day %d  %02d:%02d" % [day(), h, m]

# ---- state ---------------------------------------------------------------------------------
var sleep_pressure = 0.15        # 0 rested … 1 exhausted (process S)
var sleep_debt_h = 0.5           # hours owed
var awake_min = 0.0
var woke_at = 7.0 * 60.0
var stress = 0.25                # 0 … 1  (acute + chronic)
var chronic = 0.15               # slow part of stress (drives cortisol baseline)
var valence = 0.2                # mood -1 sad … +1 happy
var arousal = 0.4                # 0 calm … 1 activated
var activity = 0.05              # 0 rest … 1 max effort
var fitness = 0.6
var hunger = 0.2
var social = 0.6                 # connection with home/crew, decays in isolation
var co2_ppm = 2400.0             # cabin CO2 where the astronaut is
var on_eva = false
var asleep = false
var stage = "Awake"              # Awake · N1 · N2 · N3 (deep) · REM
var sleep_min = 0.0
var exercising = 0.0             # minutes left
var eva_suit_o2 = 100.0
var eva_suit_co2 = 2.0           # mmHg in helmet
var eva_suit_temp = 21.0

# devices worn / fitted
var wristband_on = false
var beacon_on = true
var patch_on = true
var patch_applied = 7.0 * 60.0   # minute the cortisol patch was put on (lasts 48 h)
var share_mood = false           # crew member consents to share mood with the flight surgeon

# night record
var hypnogram: Array = []         # one stage index per 5 sim-min: 0 W,1 N1,2 N2,3 N3,4 REM
var awakenings = 0
var last_sleep = {}              # report of the last night
var events: Array = []               # short event log

# readings (updated every tick)
var hr = 64.0
var hrv = 52.0
var bp_sys = 116.0
var bp_dia = 76.0
var spo2 = 98.0
var core_t = 36.8
var skin_t = 33.5
var cortisol_expected = 12.0
var cortisol = 12.0             # µg/dL saliva-equivalent
var eda = 2.0                   # skin conductance µS
var rr = 14.0                   # breaths/min

var rng = RandomNumberGenerator.new()
func _init(): rng.seed = 2026

func note(t: String) -> void:
	events.push_front("%s  %s" % [clock_text().substr(6), t])
	if events.size() > 8: events.pop_back()

# circadian alertness, peaks ~17:00, trough ~04:00
func circadian() -> float: return cos((hour() - 17.0) / 24.0 * TAU)

func alertness() -> float: return clamp(0.5 + 0.35 * circadian() - 0.6 * sleep_pressure - 0.04 * sleep_debt_h, 0.0, 1.0)

func patch_hours_left() -> float: return 48.0 - (minute - patch_applied) / 60.0

# ---- one simulated minute ------------------------------------------------------------------
func step(dm: float) -> void:
	minute += dm
	var h = hour()
	if asleep: _sleep_step(dm)
	else:
		awake_min += dm
		sleep_pressure = clamp(sleep_pressure + dm / (16.0 * 60.0), 0.0, 1.2)
		if awake_min > 16.0 * 60.0: sleep_debt_h += dm / 60.0 * 0.5
	hunger = clamp(hunger + dm / (6.0 * 60.0), 0.0, 1.0)
	social = clamp(social - dm / (60.0 * 60.0), 0.0, 1.0)
	if patch_on and patch_hours_left() <= 0.0:
		patch_on = false; note("cortisol patch expired — fit a new one")
	if exercising > 0.0:
		exercising -= dm
		activity = lerp(activity, 0.65, 0.2)
		if exercising <= 0.0:
			fitness = min(1.0, fitness + 0.02); stress = max(0.0, stress - 0.12); valence = min(1.0, valence + 0.15)
			note("finished exercise — stress down, mood up")
	elif not on_eva and not asleep:
		activity = lerp(activity, 0.08, 0.1)
	if asleep: activity = lerp(activity, 0.0, 0.3)

	# cabin CO2: drifts to 2400 ppm (CDRA set-point), events push it up
	co2_ppm = lerp(co2_ppm, 2400.0, 0.01 * dm)
	var co2_load: float = clamp((co2_ppm - 2500.0) / 3000.0, 0.0, 1.0)

	# stress: workload, CO2, sleep debt, hunger, isolation push it up; it relaxes on its own
	var push = 0.25 * activity * (1.0 if on_eva else 0.3) + 0.25 * co2_load + 0.03 * sleep_debt_h + 0.15 * max(hunger - 0.6, 0.0) + 0.15 * (1.0 - social) * 0.4
	stress = clamp(lerp(stress, push + chronic * 0.6, 0.01 * dm), 0.0, 1.0)
	chronic = clamp(lerp(chronic, max(stress, 0.6 * max(-valence, 0.0)), 0.0004 * dm), 0.0, 1.0)   # low mood keeps the slow stress system up

	# mood follows stress, sleep, social connection; arousal follows activity + circadian
	var v_target = 0.55 - 0.9 * stress - 0.25 * max(sleep_pressure - 0.5, 0.0) - 0.04 * sleep_debt_h + 0.35 * (social - 0.5) - 0.2 * max(hunger - 0.7, 0.0)
	valence = clamp(lerp(valence, v_target, 0.006 * dm), -1.0, 1.0)
	var a_target = 0.25 + 0.5 * activity + 0.35 * stress + 0.2 * circadian() - (0.4 if asleep else 0.0)
	arousal = clamp(lerp(arousal, a_target, 0.05 * dm), 0.0, 1.0)

	# physiology
	var stage_hr = {"Awake": 0.0, "N1": -6.0, "N2": -9.0, "N3": -13.0, "REM": -2.0}
	var hr_t = 62.0 - 8.0 * (fitness - 0.5) + 95.0 * activity + 16.0 * stress + 4.0 * circadian() + 6.0 * co2_load + stage_hr.get(stage, 0.0)
	hr = lerp(hr, hr_t, min(1.0, 0.25 * dm)) + rng.randf_range(-0.8, 0.8)
	var hrv_t = 58.0 + 20.0 * (fitness - 0.5) - 30.0 * stress - 2.0 * sleep_debt_h - 25.0 * activity + (14.0 if stage == "N3" else 0.0)
	hrv = clamp(lerp(hrv, hrv_t, 0.1 * dm) + rng.randf_range(-1.0, 1.0), 8.0, 120.0)
	bp_sys = lerp(bp_sys, 114.0 + 45.0 * activity + 14.0 * stress + 4.0 * co2_load - (10.0 if asleep else 0.0), 0.2 * dm)
	bp_dia = lerp(bp_dia, 74.0 + 9.0 * activity + 9.0 * stress - (7.0 if asleep else 0.0), 0.2 * dm)
	var o2_low: float = 0.0 if not on_eva else clamp((25.0 - eva_suit_o2) / 25.0, 0.0, 1.0)
	spo2 = clamp(lerp(spo2, 98.2 - 2.0 * activity - 9.0 * o2_low - 1.2 * co2_load, 0.3 * dm) + rng.randf_range(-0.15, 0.15), 70.0, 100.0)
	core_t = lerp(core_t, 36.75 + 0.3 * circadian() + 0.9 * activity - (0.2 if asleep else 0.0), 0.05 * dm)
	skin_t = lerp(skin_t, 33.4 - 0.6 * circadian() + (0.6 if asleep else 0.0) + 1.0 * activity, 0.05 * dm)
	rr = lerp(rr, 13.0 + 18.0 * activity + 3.0 * stress + 3.0 * co2_load - (2.0 if asleep else 0.0), 0.3 * dm)
	eda = clamp(lerp(eda, 1.5 + 7.0 * stress + 3.0 * arousal, 0.1 * dm) + rng.randf_range(-0.05, 0.05), 0.3, 20.0)
	# cortisol: awakening response (+60 % in the first 45 min), falls through the day, chronic stress raises it
	var since_wake = (minute - woke_at) / 60.0
	var car: float = 7.0 * exp(-pow((since_wake - 0.5) / 0.45, 2.0)) if not asleep else 0.0
	var diurnal = 4.0 + 10.0 * max(0.0, cos((h - 8.0) / 24.0 * TAU))
	cortisol = lerp(cortisol, diurnal + car + 9.0 * chronic + 3.0 * stress, 0.05 * dm)
	cortisol_expected = diurnal + car + 2.1          # what a calm, rested person would show at this time of day

	if on_eva:
		eva_suit_o2 = max(0.0, eva_suit_o2 - dm * (0.12 + 0.35 * activity))
		eva_suit_co2 = lerp(eva_suit_co2, 2.0 + 6.0 * activity, 0.1 * dm)
		eva_suit_temp = lerp(eva_suit_temp, 21.0 + 6.0 * activity, 0.05 * dm)

# ---- sleep ---------------------------------------------------------------------------------
func go_to_bed() -> void:
	asleep = true; stage = "N1"; sleep_min = 0.0; hypnogram.clear(); awakenings = 0
	note("lights out — the bunk dims to amber, wristband tracking")

func _sleep_step(dm: float) -> void:
	sleep_min += dm
	sleep_pressure = max(0.0, sleep_pressure - dm / (8.0 * 60.0) * 1.15)
	sleep_debt_h = max(0.0, sleep_debt_h - dm / 60.0 * 0.35)
	# 90-minute cycles: deep sleep early in the night, REM later
	var cyc = fmod(sleep_min, 90.0) / 90.0
	var night = sleep_min / 480.0
	var s = "N2"
	if sleep_min < 12.0: s = "N1"
	elif cyc < 0.15: s = "N1" if cyc < 0.06 else "N2"
	elif cyc < 0.55: s = "N3" if cyc > 0.25 and cyc < 0.25 + 0.35 * (1.0 - night) else "N2"
	elif cyc > 1.0 - (0.12 + 0.3 * night): s = "REM"
	# disturbances: high CO2, stress, the wrong circadian time
	var p_wake = 0.002 * dm * (1.0 + 4.0 * max(0.0, (co2_ppm - 3000.0) / 2000.0) + 3.0 * stress + 3.0 * max(-valence, 0.0) + (2.0 if circadian() > 0.3 else 0.0))   # low mood = broken sleep
	if rng.randf() < p_wake:
		s = "Awake"; awakenings += 1
	stage = s
	if int(sleep_min / 5.0) >= hypnogram.size():
		hypnogram.append(["Awake", "N1", "N2", "N3", "REM"].find(s))
	if (sleep_pressure <= 0.02 and circadian() > -0.6 and sleep_min > 300.0) or (hour() >= 6.5 and hour() < 7.0 and sleep_min > 360.0): wake_up()
	elif valence < -0.4 and hour() >= 4.5 and hour() < 6.5 and sleep_min > 300.0 and rng.randf() < 0.02 * dm: wake_up()   # early-morning waking, a classic sign of low mood

func wake_up() -> void:
	if not asleep: return
	asleep = false; stage = "Awake"; awake_min = 0.0; woke_at = minute
	var n = max(1, hypnogram.size())
	var cnt = [0, 0, 0, 0, 0]
	for v in hypnogram: cnt[v] += 1
	var total_h = sleep_min / 60.0
	var eff = 1.0 - float(cnt[0]) / n
	var deep = float(cnt[3]) * 5.0 / 60.0
	var rem = float(cnt[4]) * 5.0 / 60.0
	var score = int(clamp(100.0 * (0.45 * min(total_h / 8.0, 1.0) + 0.25 * eff + 0.15 * min(deep / 1.5, 1.0) + 0.15 * min(rem / 1.8, 1.0)) - 2.0 * awakenings, 0.0, 100.0))
	last_sleep = {"hours": total_h, "eff": eff, "deep": deep, "rem": rem, "wakes": awakenings, "score": score, "tracked": wristband_on}
	valence = clamp(valence + 0.004 * (score - 60), -1.0, 1.0)
	stress = clamp(stress - 0.002 * score, 0.0, 1.0)
	note("woke up · sleep score %d · %.1f h" % [score, total_h])

# ---- actions -------------------------------------------------------------------------------
func eat() -> void: hunger = 0.0; valence = min(1.0, valence + 0.08); note("crew meal — hunger gone")
func call_home() -> void: social = 1.0; valence = min(1.0, valence + 0.25); stress = max(0.0, stress - 0.1); minute += 25.0; note("video call with family — mood up")
func work_block() -> void: minute += 120.0; stress = min(1.0, stress + 0.12); sleep_pressure += 0.12; note("2-hour science block done")
func exercise() -> void: exercising = 45.0; note("45 min on the treadmill (harness + bungees)")
func co2_spike() -> void: co2_ppm = 5200.0; note("CO2 scrubber hiccup — cabin CO2 5200 ppm")
func new_patch() -> void: patch_on = true; patch_applied = minute; note("fresh cortisol patch fitted (48 h)")
