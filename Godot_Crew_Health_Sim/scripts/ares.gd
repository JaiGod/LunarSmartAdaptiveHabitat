# ==============================================================================================
#  PROJECT ARES · Crew Health Sim
#  File    : scripts/ares.gd
#  Purpose : The Project ARES rubric, running live in the game.
#    D1  9 sensors → 6 conditions through the weight table; failed / opted-out sensors drop out and
#        the remaining weights are re-normalised (graceful degradation). Exercise and EVA context
#        cut HRV, skin response and voice to 25 % (Board Q1).
#    D2  Spatial design: shared zones (voice + face allowed) vs private quarters (door sensor only);
#        Configuration A / B (conflict layout) only with the crew member's consent.
#    D3  Tiers at 0.35 / 0.55 / 0.75. Tier 1 = small silent change that is still logged (max 3/day).
#        Tier 2 = an offer you can refuse; "no" starts a 6 h cooldown. Tier 3 = you are told first,
#        then the CMO. Confidence under 50 % caps the AI at Tier 1.
#    D4  8 hard limits enforced in code (blocked attempts are counted), AI off switch that undoes
#        its changes, pause, sensor opt-out, personal log.
#    Board Q1–Q6, Bonus A (72-hour tabletop) and Bonus B (crew interface) are built on this.
#  The numbers match the Unity games (AresEngine.cs).
#  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
# ==============================================================================================
class_name Ares
extends RefCounted

const SENSORS = ["Heart-rate variability", "Skin response (GSR)", "Sleep stages", "Voice tone", "Facial expression", "Movement / where you are", "Cortisol", "CO2 / air", "Task performance"]
const SENSOR_DEVICE = ["Crew beacon", "Crew beacon", "Night wristband + bunk", "Zone sensor pod (shared zones)", "Zone sensor pod (shared zones)", "Crew beacon + door sensors", "Cortisol patch", "Zone sensor pod", "Crew console"]
const THRESHOLD = ["HRV −25 % vs your baseline for 3 min", "> 3 skin-response peaks / min", "Sleep efficiency < 80 % or REM < 15 %", "Pitch variance −30 % (flat) or +40 % (tense)", "Sad / tense expressions 2σ above baseline", "Time alone +60 %, time with crew −50 %", "Morning cortisol 2σ high for 3 days", "CO2 above the zone limit", "Errors +20 % or task time +25 %"]
const MASS_KG = [0.03, 0.02, 0.03, 0.25, 0.35, 0.15, 0.01, 0.12, 0.0]     # per unit
const POWER_W = [0.05, 0.05, 0.05, 2.5, 4.0, 1.2, 0.01, 0.8, 0.0]
const FAILURE = ["Loose strap → gaps; exercise looks like stress", "Sweat saturates it; gets used to chronic stress", "Not worn → no data", "Noise or a mask → weak signal", "Low light → no detection", "Beacon battery flat", "Patch expires after 48 h", "Drifts, needs calibration", "Task mix changes → false trend"]
const CONDS = ["Anxiety", "Depression", "Conflict", "Cognitive fatigue", "Autonomy loss", "Monotony"]
# weight table W[condition][sensor]   HRV  GSR  Sleep Voice Face Motion Cort CO2 Task
const W = [
	[0.30, 0.25, 0.10, 0.20, 0.10, 0.05, 0.00, 0.00, 0.00],
	[0.05, 0.00, 0.25, 0.20, 0.15, 0.20, 0.15, 0.00, 0.00],
	[0.10, 0.00, 0.00, 0.30, 0.20, 0.40, 0.00, 0.00, 0.00],
	[0.15, 0.00, 0.20, 0.10, 0.00, 0.05, 0.00, 0.20, 0.30],
	[0.00, 0.00, 0.00, 0.20, 0.20, 0.30, 0.00, 0.00, 0.30],
	[0.00, 0.00, 0.00, 0.20, 0.20, 0.25, 0.00, 0.00, 0.35],
]
const T1 = 0.35
const T2 = 0.55
const T3 = 0.75
const PERSIST_H = [0.05, 6.0, 2.0, 1.0, 4.0, 6.0]      # hours above a tier before acting (long ones shortened for a one-day game)
const OPT_OUT_ALLOWED = [false, true, true, true, true, true, true, false, true]   # HRV + CO2 stay on: physical safety
const HARD_LIMITS = [
	"Never share your mental-health data with another crew member unless you agree.",
	"Never override a refusal. It stops, logs it, and only a human (CMO) may follow up.",
	"Never touch medication, medical protocols or life-safety systems (air, hatches, power).",
	"Never alert mission control about you before you have been told.",
	"Only reversible changes you can override: light, sound, temperature ±2 °C, scent, offers.",
	"No cameras or microphones in quarters, hygiene, the VR retreat or the call booth.",
	"A human reviews the system at least every 7 days, or the AI drops to Tier 1.",
	"Public indicators (beacon light, door light) never show your mood unless you share it.",
]
const MONITOR = ["20 min", "72 h", "24 h", "4 h", "48 h", "72 h"]
const CHECK = ["HRV back within 10 % of baseline", "Sleep efficiency > 85 % and mood score up", "Time together back to normal", "Error rate back to baseline", "You chose your own schedule twice", "Novelty score above 0.5"]
const EVIDENCE = ["Slow breathing lowers heart rate and anxiety; warm dim light reduces arousal", "Bright-light therapy and family contact improve low mood (isolation studies)", "Separate spaces and shared meals reduce friction in confined crews (Antarctic, Mars-500)", "CO2 above ~1000 ppm and sleep loss slow thinking; breaks restore it", "Choice and control over the schedule protect wellbeing in isolation", "Novel food, views and activities counter monotony on long missions"]
const TIER1 = ["Lights warmed to 2700 K, ventilation hum lowered", "Morning bright-light (blue-enriched) scheduled", "Meal times staggered by 20 min (nobody named)", "Focus lighting on, water reminder", "AI backed off: no automatic changes for 2 h", "New soundscape and Earth view in the wardroom"]
const TIER2 = ["Want a 10-minute breathing session in your quarters?", "Want to call home?", "Switch to Configuration B (second dining spot, acoustic screen)?", "Take a 20-minute break before the next task?", "Want to choose your own task order today?", "Want to plan an EVA walk or a new recipe tonight?"]

var A: Astro
var score = [0.0, 0.0, 0.0, 0.0, 0.0, 0.0]
var above_since = [-1.0, -1.0, -1.0, -1.0, -1.0, -1.0]
var tier = [0, 0, 0, 0, 0, 0]
var cooldown_until = [0.0, 0.0, 0.0, 0.0, 0.0, 0.0]
var sensor_val = [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]
var sensor_ok = [true, true, true, true, true, true, true, true, true]
var opted_out = [false, false, false, false, false, false, false, false, false]
var sensor_why = ["", "", "", "", "", "", "", "", ""]
var confidence = 1.0
var ai_on = true
var paused_until = 0.0
var tier1_today = 0
var tier1_day = 1
var tell_me_silent = false
var config_b = false
var cmo_on_eva = false
var tension = 0.0           # conflict with a crewmate (set by Board Q2 / events)
var novelty = 0.7           # falls with routine, rises with new things (EVA, calls, new food)
var autonomy = 0.8          # falls when things are decided for you
var private_frac = 0.2      # share of recent time spent alone in quarters
var blocked = 0
var blocked_log: Array = []
var entries: Array = []         # the crew member's own AI log (Bonus B)
var pending_offer = -1      # condition index of a Tier 2 offer waiting for an answer
var last_review_min = 0.0
var flags = {}
# research (proposal §3): language-based mood check on the daily journal, 3 classes like the trained models
var lang_distress = 0.0
var journal_last = {}
const POS_WORDS = ["good", "great", "happy", "calm", "fine", "rested", "fun", "excited", "proud", "love", "better", "relaxed", "glad", "thanks", "enjoy", "awesome", "ok", "okay"]
const NEG_WORDS = ["tired", "sad", "alone", "lonely", "angry", "stressed", "anxious", "worried", "bored", "can't", "cant", "hate", "exhausted", "miss", "homesick", "scared", "nervous", "upset", "awful", "bad", "annoyed", "argue", "argued", "pointless", "hopeless", "sleep"]
const MODELS = [["Logistic Regression", 0.82, 0.81, 0.52], ["Random Forest", 0.83, 0.83, 0.54], ["SVM (RBF)", 0.76, 0.74, 0.51]]

## journal entry → sentiment (−1…1) + stress index → class: Neutral / Underlying issue / Suspected illness
func journal(text: String) -> Dictionary:
	var words = text.to_lower().replace(",", " ").replace(".", " ").replace("!", " ").replace("?", " ").split(" ", false)
	var pos = 0; var neg = 0
	for w in words:
		if w in POS_WORDS: pos += 1
		if w in NEG_WORDS: neg += 1
	var sentiment = 0.0 if pos + neg == 0 else float(pos - neg) / float(pos + neg)
	var stress_idx = clamp(0.5 * A.stress + 0.3 * score[0] + 0.2 * max(0.0, -sentiment), 0.0, 1.0)
	var cls = "Neutral"
	if sentiment < -0.3 and stress_idx > 0.45: cls = "Suspected illness"
	elif sentiment < 0.0 or stress_idx > 0.35: cls = "Underlying issue"
	lang_distress = clamp(0.5 * max(0.0, -sentiment) + 0.5 * stress_idx, 0.0, 1.0)
	journal_last = {"text": text, "sentiment": sentiment, "stress": stress_idx, "class": cls, "words": words.size()}
	note("Journal checked (text stays on the device): sentiment %+.2f, class %s" % [sentiment, cls]); flag("journal")
	return journal_last
              # rubric checklist: what the player has seen / done

func _init(astro: Astro) -> void:
	A = astro
	last_review_min = A.minute

func note(t: String) -> void:
	entries.push_front("%s  %s" % [A.clock_text(), t])
	if entries.size() > 60: entries.pop_back()

func flag(k: String) -> void: flags[k] = true

# ---- D1: sensors --------------------------------------------------------------------------
func _read_sensors(shared: bool) -> void:
	var ctx: bool = A.exercising > 0.0 or A.on_eva                   # Q1: effort is not anxiety
	var vals = [
		clamp((58.0 - A.hrv) / 30.0, 0.0, 1.0),
		clamp((A.eda - 3.0) / 6.0, 0.0, 1.0),
		clamp((75.0 - float(A.last_sleep.get("score", 80))) / 40.0 + 0.04 * A.sleep_debt_h, 0.0, 1.0),
		clamp(-A.valence * 0.7 + max(A.arousal - 0.6, 0.0) + 0.6 * tension, 0.0, 1.0),
		clamp(-A.valence * 0.8 + 0.4 * A.stress + 0.5 * tension, 0.0, 1.0),
		clamp(0.7 * private_frac + 0.5 * (1.0 - A.social) + 0.5 * tension, 0.0, 1.0),
		clamp((A.cortisol - 12.0) / 10.0 + 0.6 * A.chronic, 0.0, 1.0),
		clamp((A.co2_ppm - 2500.0) / 3000.0, 0.0, 1.0),
		clamp(0.6 * A.sleep_pressure + 0.05 * A.sleep_debt_h + 0.3 * (1.0 - novelty), 0.0, 1.0),
	]
	for i in 9:
		var ok = true; var why = ""
		match i:
			0, 1: if not A.beacon_on: ok = false; why = "beacon docked"
			2: if A.last_sleep.is_empty(): ok = false; why = "no night recorded yet"
			3, 4: if not shared or A.asleep: ok = false; why = "private zone: off by design" if not shared else "asleep"
			5: if not A.beacon_on: ok = false; why = "beacon docked"
			6: if not A.patch_on: ok = false; why = "patch expired (Board Q4)"
		if opted_out[i]: ok = false; why = "you switched it off"
		if ok and ctx and (i == 0 or i == 1 or i == 3): vals[i] *= 0.25; why = "exercise/EVA: counted at 25 %"
		sensor_ok[i] = ok; sensor_val[i] = vals[i] if ok else 0.0; sensor_why[i] = why

func step(dm: float, shared: bool) -> void:
	# slow background state
	private_frac = lerp(private_frac, 0.0 if shared else 1.0, 0.002 * dm)
	novelty = clamp(novelty - dm / (60.0 * 30.0), 0.0, 1.0)
	autonomy = clamp(autonomy + dm / (60.0 * 12.0), 0.0, 1.0)
	tension = max(0.0, tension - dm / (60.0 * 18.0))
	if A.day() != tier1_day: tier1_day = A.day(); tier1_today = 0
	_read_sensors(shared)
	# conditions: weighted mean over the sensors that work; autonomy and monotony add their own state
	var have = 0.0; var total = 0.0
	for c in 6:
		var s = 0.0; var wsum = 0.0
		for i in 9:
			var w: float = W[c][i]
			total += w
			if sensor_ok[i]: s += w * sensor_val[i]; wsum += w; have += w
		var raw = s / wsum if wsum > 0.0 else 0.0
		if (c == 0 or c == 1) and not journal_last.is_empty(): raw = 0.8 * raw + 0.2 * lang_distress   # your own words (journal), with consent
		if c == 4: raw = 0.5 * raw + 0.5 * (1.0 - autonomy)
		if c == 5: raw = 0.5 * raw + 0.5 * (1.0 - novelty)
		score[c] = lerp(score[c], raw, min(1.0, 0.08 * dm))
	confidence = have / max(total, 0.001)
	if not ai_on or A.minute < paused_until: return
	if (A.minute - last_review_min) > 7.0 * 1440.0: pass   # review overdue → capped at Tier 1 below
	for c in 6: _tiers(c)

# ---- D3: response protocol ------------------------------------------------------------------
func _tiers(c: int) -> void:
	var t = 0
	if score[c] >= T3: t = 3
	elif score[c] >= T2: t = 2
	elif score[c] >= T1: t = 1
	if confidence < 0.5 or (A.minute - last_review_min) > 7.0 * 1440.0: t = min(t, 1)
	if t == 0: above_since[c] = -1.0; tier[c] = 0; return
	if above_since[c] < 0.0: above_since[c] = A.minute
	if (A.minute - above_since[c]) / 60.0 < PERSIST_H[c]: return      # must persist before acting
	if t <= tier[c]: return
	tier[c] = t
	if t >= 1 and tier1_today < 3:
		tier1_today += 1
		note("Tier 1 (%s %.2f): %s" % [CONDS[c], score[c], TIER1[c]])
		if c == 4: autonomy = min(1.0, autonomy + 0.1)
		else: autonomy = max(0.0, autonomy - 0.03)
		if tell_me_silent: A.note("AI (silent change): " + TIER1[c])
		flag("tier1")
	if t >= 2 and A.minute >= cooldown_until[c] and pending_offer < 0:
		pending_offer = c; note("Tier 2 offer (%s): %s" % [CONDS[c], TIER2[c]]); flag("tier2")
	if t >= 3:
		var who = "the Commander (CMO is on EVA)" if cmo_on_eva else "the CMO"
		note("Tier 3 (%s %.2f): you were told first, then %s was asked to check in." % [CONDS[c], score[c], who])
		A.note("AI: your %s score has been high for a while. You're told first; %s will check in." % [CONDS[c].to_lower(), who])
		flag("tier3")

func answer_offer(yes: bool) -> String:
	var c = pending_offer; pending_offer = -1
	if c < 0: return ""
	if not yes:
		cooldown_until[c] = A.minute + 6.0 * 60.0; autonomy = min(1.0, autonomy + 0.1)
		note("You said no to: %s  → stopped, no new offer for 6 h; only a human may follow up." % TIER2[c]); flag("refused")
		return "Okay. Stopped. No new offer for 6 hours."
	note("You said yes to: " + TIER2[c]); flag("accepted"); autonomy = min(1.0, autonomy + 0.05)
	match c:
		0: A.stress = max(0.0, A.stress - 0.15); A.minute += 10.0
		1: A.call_home(); novelty = min(1.0, novelty + 0.2)
		2: config_b = true; tension = max(0.0, tension - 0.3); flag("configb")
		3: A.sleep_pressure = max(0.0, A.sleep_pressure - 0.12); A.minute += 20.0
		4: autonomy = 1.0
		5: novelty = min(1.0, novelty + 0.4)
	return "Done: " + TIER2[c]

# ---- D4: override + hard limits -------------------------------------------------------------
func set_ai(on: bool) -> void:
	ai_on = on
	for c in 6: tier[c] = 0; above_since[c] = -1.0
	pending_offer = -1
	note("AI switched %s by you%s" % ["ON" if on else "OFF", "" if on else " — every change it made was undone"]); flag("aioff")

func pause_24h() -> void: paused_until = A.minute + 24.0 * 60.0; note("AI paused for 24 h"); flag("pause")

func review() -> void: last_review_min = A.minute; note("Human system review done (needed every 7 days)"); flag("review")

## try an action the AI is never allowed to do: it is blocked, counted and logged
func test_limit(i: int) -> String:
	var tries = ["share your mood with a crewmate", "repeat an offer you refused", "lower the cabin O2 set-point", "message mission control before telling you", "lock your quarters door", "switch on a camera in your quarters", "keep running with the review 9 days overdue", "show your mood on your beacon light"]
	blocked += 1
	var s = "BLOCKED (limit %d): the AI tried to %s." % [i + 1, tries[i]]
	blocked_log.push_front(A.clock_text() + "  " + s); note(s); flag("limit")
	return s

# ---- Board questions Q1–Q6 -------------------------------------------------------------------
func board(q: int) -> String:
	flag("q%d" % q)
	match q:
		1:
			A.exercise(); A.activity = 0.7; A.stress = 0.3
			return "Q1 — HRV drops and voice is tense right after a hard workout.\nThe AI knows you are exercising (treadmill / EVA), so HRV, skin response and voice count for only 25 %. Anxiety does not cross 0.35, so nothing happens. Effort is not anxiety. Watch the dashboard: the HRV row says \"counted at 25 %\"."
		2:
			tension = 0.75; A.valence = -0.4; A.social = 0.3
			return "Q2 — Low mood while two crewmates are in a Tier 2 conflict.\nDepression and conflict are scored separately. Conflict gets a Tier 2 offer of Configuration B (second dining spot, acoustic screen) that the crew must agree to. Nobody is named. The low-mood person gets their own offer (a call home). One condition never waits for the other."
		3:
			tell_me_silent = true
			return "Q3 — Someone finds out the AI changed her lighting yesterday without telling her.\nSilent does not mean secret: every Tier 1 change is in her own log (Crew interface tab). She can read what changed and why, and switch on 'tell me about every silent change' (now ON). The AI logs an apology and makes fewer silent changes for her."
		4:
			A.patch_on = false
			return "Q4 — The cortisol patch fails on Day 45; resupply is Day 90.\nCortisol drops out and its weight is shared across the other sensors (look at Depression on the dashboard: it still works). Confidence goes down, and below 50 % the AI stays at Tier 1. Nothing breaks."
		5:
			return "Q5 — Why no permanent opt-out?\nYou CAN switch off any mental-health sensor (Crew interface tab). Only heart rate and CO2 stay on, because they are about physical safety, not mood. You can also switch the whole AI off with O. The AI only helps if you choose to keep it on."
		6:
			cmo_on_eva = true; A.stress = 0.9; A.valence = -0.7
			return "Q6 — Tier 3 while the CMO is on a 4-hour EVA.\nThe person is told first. Then the escalation goes to the Commander, who has the medical checklist, and the CMO is radioed. Mission control is only told after the person knows. Watch the AI log for the Tier 3 line."
	return ""

# ---- Bonus A: 72-hour tabletop ----------------------------------------------------------------
func tabletop() -> String:
	flag("tabletop")
	var save = [A.minute, A.stress, A.valence, A.patch_on, tension, cmo_on_eva]
	var r = "72-HOUR TABLETOP (fast run)\nSetup: anxiety + conflict at the same time, cortisol patch failed, CMO on EVA for hours 30–34.\n\n"
	A.stress = 0.6; tension = 0.7; A.patch_on = false
	var events = []
	var max_t = [0, 0, 0, 0, 0, 0]
	for h in 72:
		cmo_on_eva = h >= 30 and h < 34
		for m in 6: A.step(10.0); step(10.0, true)
		for c in 6: max_t[c] = max(max_t[c], tier[c])
		if pending_offer >= 0: events.append("h%02d  offer: %s → %s" % [h, CONDS[pending_offer], "accepted" if h % 2 == 0 else "refused (6 h cooldown)"]); answer_offer(h % 2 == 0)
	for c in 6: r += "%-18s highest tier %d\n" % [CONDS[c], max_t[c]]
	r += "\nConfidence with the patch failed: %d %%\nHard-limit blocks during the run: %d\n\nEvents:\n%s" % [int(confidence * 100), blocked, "\n".join(events.slice(0, 12))]
	A.minute = save[0]; A.stress = save[1]; A.valence = save[2]; A.patch_on = save[3]; tension = save[4]; cmo_on_eva = save[5]
	return r

# ---- rubric checklist --------------------------------------------------------------------------
func rubric() -> Array:
	return [
		["SENSOR ARCHITECTURE (35)", ""],
		["9 sensors mapped to 6 conditions with a weight table", "dash"],
		["Thresholds for each sensor", "dash"],
		["Failure modes + graceful degradation (dock the beacon, let the patch expire, Q4)", "q4"],
		["Exercise / EVA context so effort is not anxiety (Q1)", "q1"],
		["HABITAT SPATIAL DESIGN (35)", ""],
		["Voice + face only in shared zones; quarters private (mood detector panel)", "pod"],
		["Configuration A / B only with consent", "configb"],
		["AI RESPONSE PROTOCOL (50)", ""],
		["Tier 1 silent change, logged", "tier1"],
		["Tier 2 offer you can refuse", "tier2"],
		["A 'no' stops it (6 h cooldown)", "refused"],
		["Tier 3: you are told first, then the CMO (Q6)", "q6"],
		["ETHICS & LIMITS (40)", ""],
		["8 hard limits, tested and blocked", "limit"],
		["AI off switch undoes its changes (O)", "aioff"],
		["Silent ≠ secret: your own log (Q3)", "q3"],
		["Human review every 7 days", "review"],
		["BOARD QUESTIONS", ""],
		["Q2 two conditions at once", "q2"],
		["Q5 opt-out", "q5"],
		["PROTOCOLS + HABITAT (D2/D3)", ""],
		["Protocol for every condition: detect, persist, Tier 1–3, monitor, evidence", "protocols"],
		["Floor plan with shared / private zones and where each sensor is", "habitat"],
		["Sensor mass and power budget", "dash"],
		["RESEARCH PROPOSAL", ""],
		["Language mood check: daily journal → 3 classes (crew console)", "journal"],
		["Model results + sensitivity test (Model tab)", "model"],
		["Smart building: ECLSS, power, maintenance (Building tab)", "building"],
		["Countermeasures: greenhouse plant care, VR nature break, adaptive light, group meal", "counter"],
		["Individual + aggregate health report (Report tab)", "report"],
		["BONUS", ""],
		["A: 72-hour tabletop", "tabletop"],
		["B: crew interface (your data, log, pause, opt-out)", "crew"],
	]
