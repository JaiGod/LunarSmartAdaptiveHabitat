# ==============================================================================================
#  PROJECT ARES · Crew Health Sim
#  File    : scripts/fusion.gd
#  Purpose : "How is the astronaut feeling?" — sensor fusion across all six health devices (+ the
#            EVA suit). Each device only reports what it can really measure; the fusion weighs the
#            evidence, says which devices agree, and turns it into one plain-language read-out.
#  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
# ==============================================================================================
class_name Fusion
extends RefCounted

const DEVICES := ["Crew beacon (chest)", "Night wristband", "Cortisol patch", "Zone sensor pod", "Crew Health Station", "Smart sleep bunk", "EVA suit"]

## One line per device: [name, available, reading text, stress vote (-1 calm … +1 stressed), fatigue vote, mood vote (−1 low … +1 good), weight]
static func device_rows(a: Astro, checkin: Dictionary, in_shared_zone: bool) -> Array:
	var rows = []
	# 1 crew beacon: ECG → HR + HRV, skin conductance, location
	if a.beacon_on:
		var hrv_z: float = clamp((45.0 - a.hrv) / 25.0, -1.0, 1.0)
		var eda_z: float = clamp((a.eda - 4.0) / 5.0, -1.0, 1.0)
		rows.append(["Crew beacon (chest)", true, "HR %d bpm · HRV %d ms · skin %.1f µS" % [a.hr, a.hrv, a.eda], clamp(0.6 * hrv_z + 0.4 * eda_z - (0.4 if a.activity > 0.4 else 0.0), -1.0, 1.0), clamp(-hrv_z * 0.2, -1.0, 1.0), 0.0, 1.0])
	else: rows.append(["Crew beacon (chest)", false, "not worn", 0.0, 0.0, 0.0, 0.0])
	# 2 wristband: last night's sleep stages
	var ls: Dictionary = a.last_sleep
	if not ls.is_empty() and ls.get("tracked", false):
		var f: float = clamp((70.0 - ls.score) / 30.0, -1.0, 1.0)
		rows.append(["Night wristband", true, "last night %.1f h · score %d · deep %.1f h · REM %.1f h · %d wake-ups" % [ls.hours, ls.score, ls.deep, ls.rem, ls.wakes], f * 0.3, f, -f * 0.3, 0.9])
	elif a.asleep and a.wristband_on: rows.append(["Night wristband", true, "tracking now · stage %s · HR %d" % [a.stage, a.hr], 0.0, 0.0, 0.0, 0.3])
	else: rows.append(["Night wristband", false, "not worn last night" if not a.wristband_on else "no night recorded yet", 0.0, 0.0, 0.0, 0.0])
	# 3 cortisol patch: compare with the expected value for this time of day
	if a.patch_on:
		var h = a.hour()
		var expected = 4.0 + 10.0 * max(0.0, cos((h - 8.0) / 24.0 * TAU))
		var dz: float = clamp((a.cortisol - expected - 2.0) / 6.0, -1.0, 1.0)
		rows.append(["Cortisol patch", true, "%.1f µg/dL (expected ~%.0f now) · %d h left" % [a.cortisol, expected, int(a.patch_hours_left())], dz, 0.0, -dz * 0.3, 0.8])
	else: rows.append(["Cortisol patch", false, "expired — fit a new patch", 0.0, 0.0, 0.0, 0.0])
	# 4 zone sensor pod: voice + face (only in shared zones, on-device) and CO2 everywhere
	if in_shared_zone and not a.asleep and not a.on_eva:
		var v_est: float = clamp(a.valence + a.rng.randf_range(-0.12, 0.12), -1.0, 1.0)
		var tone = "bright" if v_est > 0.3 else ("flat" if v_est > -0.2 else "low, slow")
		rows.append(["Zone sensor pod", true, "voice %s · face %s · CO2 %d ppm" % [tone, "relaxed" if a.arousal < 0.5 else "tense", a.co2_ppm], clamp((a.arousal - 0.5) * 1.2, -1.0, 1.0), 0.0, v_est, 0.7 if a.share_mood else 0.35])
	else: rows.append(["Zone sensor pod", true if not a.on_eva else false, "CO2 %d ppm · voice/face off (private zone)" % a.co2_ppm if not a.on_eva else "outside — no pod", clamp((a.co2_ppm - 3500.0) / 3000.0, 0.0, 0.5), clamp((a.co2_ppm - 3500.0) / 3000.0, 0.0, 0.5), 0.0, 0.2 if not a.on_eva else 0.0])
	# 5 health station: last check-in
	if not checkin.is_empty():
		var age_h: float = (a.minute - float(checkin.t)) / 60.0
		var bpz: float = clamp((float(checkin.sys) - 125.0) / 20.0, -1.0, 1.0)
		rows.append(["Crew Health Station", true, "BP %d/%d · SpO2 %d%% · %.1f °C · %.0f h ago" % [checkin.sys, checkin.dia, checkin.spo2, checkin.temp, age_h], bpz * 0.5, 0.0, 0.0, 0.6 if age_h < 12.0 else 0.25])
	else: rows.append(["Crew Health Station", false, "no check-in today", 0.0, 0.0, 0.0, 0.0])
	# 6 smart bunk: under-mattress sleep sensing (backup if the wristband was off) + sleep pressure
	var sp_v: float = clamp((a.sleep_pressure - 0.55) / 0.4, -1.0, 1.0)
	rows.append(["Smart sleep bunk", true, ("asleep · %s" % a.stage) if a.asleep else ("awake %.1f h · sleep debt %.1f h" % [a.awake_min / 60.0, a.sleep_debt_h]), 0.0, clamp(sp_v + 0.12 * a.sleep_debt_h, -1.0, 1.0), 0.0, 0.7])
	# 7 EVA suit: heart rate + blood pressure detector in the suit, metabolic load
	if a.on_eva:
		rows.append(["EVA suit", true, "HR %d · BP %d/%d · O2 %d%% · helmet CO2 %.1f mmHg" % [a.hr, a.bp_sys, a.bp_dia, a.eva_suit_o2, a.eva_suit_co2], clamp((a.hr - 120.0) / 40.0, -1.0, 1.0), 0.0, 0.0, 1.0])
	return rows

## Fused read-out: {label, sentence, color, stress, fatigue, mood, confidence, agree}
static func assess(a: Astro, checkin: Dictionary, in_shared_zone: bool) -> Dictionary:
	var rows = device_rows(a, checkin, in_shared_zone)
	var ws = 0.0; var s = 0.0; var wf = 0.0; var f = 0.0; var wm = 0.0; var m = 0.0
	var used = 0
	for r in rows:
		if not r[1] or r[6] <= 0.0: continue
		used += 1
		if r[3] != 0.0: s += r[3] * r[6]; ws += r[6]
		if r[4] != 0.0: f += r[4] * r[6]; wf += r[6]
		if r[5] != 0.0: m += r[5] * r[6]; wm += r[6]
	s = s / ws if ws > 0.0 else 0.0
	f = f / wf if wf > 0.0 else 0.0
	m = m / wm if wm > 0.0 else 0.0
	# agreement: how many devices lean the same way as the fused stress score
	var agree = 0; var voters = 0
	for r in rows:
		if r[1] and absf(r[3]) > 0.1:
			voters += 1
			if signf(r[3]) == signf(s): agree += 1
	var conf: float = clamp(0.25 + 0.1 * used + 0.25 * (float(agree) / max(1, voters)), 0.0, 0.97)
	var label = "Calm and steady"; var col = Color(0.35, 0.85, 0.55)
	if a.asleep: label = "Asleep"; col = Color(0.45, 0.6, 1.0)
	elif a.on_eva and s > 0.5: label = "Working hard on EVA"; col = Color(1.0, 0.6, 0.25)
	elif s > 0.45 and f > 0.35: label = "Overloaded"; col = Color(1.0, 0.35, 0.3)
	elif s > 0.4: label = "Stressed"; col = Color(1.0, 0.55, 0.3)
	elif f > 0.45: label = "Exhausted" if f > 0.75 else "Tired"; col = Color(0.95, 0.8, 0.3)
	elif m < -0.25: label = "Low mood"; col = Color(0.6, 0.55, 1.0)
	elif m > 0.3 and s < 0.0: label = "Rested and upbeat"; col = Color(0.3, 0.95, 0.75)
	var why = []
	if a.beacon_on: why.append("heart-rate variability %s" % ("low" if a.hrv < 40 else "healthy"))
	if a.patch_on: why.append("cortisol %s for the time of day" % ("high" if rows[2][3] > 0.2 else "normal"))
	if not a.last_sleep.is_empty(): why.append("last night's sleep score %d" % a.last_sleep.score)
	if a.co2_ppm > 3500: why.append("cabin CO2 is high (%d ppm)" % a.co2_ppm)
	if a.on_eva: why.append("suit heart rate %d bpm" % a.hr)
	var sentence = "%s — %s." % [label, ", ".join(why)] if why.size() else label
	var advice = "Keep going — all readings are in range."
	if label == "Overloaded" or label == "Stressed": advice = "Suggest a break: 10 min breathing, a call home or the exercise bike. Check the CO2 scrubber if CO2 is high."
	elif label == "Tired" or label == "Exhausted": advice = "Move the next hard task, plan an early night (dim amber light from 20:00)."
	elif label == "Low mood": advice = "Offer a call with family or crew time; flag to the flight surgeon only with consent."
	elif label == "Working hard on EVA": advice = "Slow the pace — keep heart rate under 150 bpm and watch suit O2."
	return {"label": label, "sentence": sentence, "advice": advice, "color": col, "stress": s, "fatigue": f, "mood": m, "confidence": conf, "agree": agree, "voters": voters, "rows": rows}
