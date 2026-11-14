class_name Crew
extends RefCounted
## One astronaut. The true state below is hidden from the player: LUNA only sees it through sensors.

var i = 0
var info = {}
var name = ""
# ---- hidden state ----
var stress = 0.15
var mood = 0.35            # -1..1
var fatigue = 0.15
var agency = 0.78
var novelty = 0.85
var connection = 0.6
var sleep_debt = 0.5       # hours
var chronic = 0.15         # slow stress (what cortisol sees)
var asleep = false
var outside = false        # on EVA
var last_ex = -9999.0      # last time exercising or on EVA (activity context)
var night = {"hours": 7.5, "wakes": 2, "quality": 0.85, "start": -1.0, "wakes_now": 0}
var trust = 0.72
var science_today = 0.0
# ---- crew-controlled preferences (crew interface, Bonus B) ----
var tell_me = false        # "tell me about every change LUNA makes for me"
var pod_invite = false     # "LUNA may adjust my pod"
var opt_out = {}           # sensor id -> true
var paused_until = -1.0    # "pause LUNA for me"
var stepback_until = -1.0
var wears_sleep = true
var smart_alarm = true     # wake in light sleep inside a 30-min window
var dawn_light = true      # pod light ramps up like a sunrise before the alarm
var stage = "Wake"         # Wake, N1, N2, N3 (deep), REM
var hyp = []               # hypnogram of last night: one stage every 10 min
var alarm_flash = -1.0     # time the wake alarm went off (for the bell icon)
var woke_deep = false
# ---- activity ----
var act_id = "idle"
var act_until = 0.0
var act_spot = Vector2i.ZERO
var act_face = "down"
var act_data = {}
var cell = Vector2i.ZERO
var pos = Vector2.ZERO     # pixels (map space, centre of the 16x16 cell)
var prev = Vector2.ZERO    # position one simulated minute ago (for smooth drawing)
var path = []
var walk_t = 0.0
var face = "down"
var flip = false
var free_block_day = -1
var offer_queue = []       # accepted offers waiting to run: {"id","at","until","cond"}
var cmo_at = -1.0          # CMO check-in time (human chain of care)
var rng = RandomNumberGenerator.new()

func _init(idx: int) -> void:
	i = idx; info = D.CREW[idx]; name = info.short
	rng.seed = 1000 + idx * 77
	cell = D.bed_spot(i)[0]; pos = Vector2(cell) * 16.0 + Vector2(8, 8); prev = pos
	stress = 0.12 + 0.06 * idx * 0.5; mood = 0.35 - 0.04 * idx
	trust = 0.7 + rng.randf() * 0.08

func zone(hab: Hab) -> String:
	if outside: return "outside"
	return hab.zone_of(cell)

func is_exercising() -> bool: return act_id in ["exercise", "offer_ex"] or outside

# =====================================================================================================
#  the truth (what is really going on)
# =====================================================================================================
func debt_n() -> float: return clamp(sleep_debt / 6.0, 0.0, 1.0)

func truth(c: int, sim) -> float:
	match c:
		0: return clamp(stress * 1.15 + 0.06 * debt_n() - 0.05, 0.0, 1.0)
		1: return clamp(0.35 - mood * 0.6 + 0.25 * (0.5 - connection) + 0.1 * debt_n(), 0.0, 1.0)
		2: return sim.max_tension(i)
		3: return clamp(fatigue + 0.2 * debt_n(), 0.0, 1.0)
		4: return clamp(1.0 - agency, 0.0, 1.0)
		5: return clamp(1.0 - novelty, 0.0, 1.0)
	return 0.0

func wellbeing(sim) -> float:
	var s = 0.0
	for c in 6: s += truth(c, sim)
	return clamp(1.0 - s / 3.0, 0.0, 1.0)

# =====================================================================================================
#  one simulated minute
# =====================================================================================================
func step(now: float, sim) -> void:
	var dh = 1.0 / 60.0
	var hab: Hab = sim.hab
	var z = zone(hab)
	var others = sim.others_in_zone(i, z)
	var co2f = clamp((hab.co2 - 800.0) / 1500.0, 0.0, 1.0)
	var dn = debt_n()
	# activity effects (per hour)
	var fx = {}
	match act_id:
		"sleep":
			var q = night.quality
			fx = {"fatigue": -0.13 * q, "stress": -0.05}
			sleep_debt = max(0.0, sleep_debt - 0.72 * q * dh)
		"work":
			fx = {"fatigue": 0.075 * (1.0 + co2f) * (1.0 + 0.6 * dn) * act_data.get("mult", 1.0), "stress": 0.012 + (0.02 if fatigue > 0.6 else 0.0), "agency": -0.004}
			if act_data.get("free", false): fx = {"agency": 0.03, "stress": -0.03, "fatigue": -0.03}
		"exercise", "offer_ex":
			fx = {"stress": -0.09, "mood": 0.012, "fatigue": -0.03}
		"meal":
			fx = {"connection": 0.09 if others > 0 else -0.01, "mood": 0.008, "fatigue": -0.02}
		"lounge", "movie":
			fx = {"connection": 0.07 if others > 0 else 0.0, "stress": -0.04, "fatigue": -0.05, "novelty": 0.03 if act_id == "movie" else 0.0}
		"pod", "earthcall", "nap":
			fx = {"stress": -0.06, "fatigue": -0.05, "connection": -0.015, "agency": 0.012}
		"greenhouse":
			fx = {"mood": 0.012, "stress": -0.04, "novelty": 0.012, "connection": 0.04 if others > 0 else 0.0}
		"eva":
			fx = {"stress": 0.05, "novelty": 0.12, "fatigue": 0.04}
		"shelter":
			fx = {"stress": 0.07, "fatigue": 0.01}
		"cmo":
			fx = {"stress": -0.25, "mood": 0.12, "connection": 0.08}
	if act_data.has("fx"):
		for k in act_data.fx: fx[k] = fx.get(k, 0.0) + act_data.fx[k]
	# environment (LUNA's levers in this zone + air)
	var env = sim.luna.zone_effects(z, i, now)
	for k in env: fx[k] = fx.get(k, 0.0) + env[k] * 0.4     # environmental levers are subtle by design
	fx["stress"] = fx.get("stress", 0.0) + 0.012 * co2f + (0.02 if hab.shed else 0.0)
	fx["fatigue"] = fx.get("fatigue", 0.0) + 0.025 * co2f
	# apply
	stress = clamp(stress + (fx.get("stress", 0.0) + (_stress_base() - stress) * 0.12) * dh, 0.0, 1.0)
	fatigue = clamp(fatigue + fx.get("fatigue", 0.0) * dh, 0.0, 1.0)
	connection = clamp(connection + (fx.get("connection", 0.0) + (0.5 - connection) * 0.05) * dh, 0.0, 1.0)
	agency = clamp(agency + (fx.get("agency", 0.0) + (0.74 - agency) * 0.01) * dh, 0.0, 1.0)
	novelty = clamp(novelty + (fx.get("novelty", 0.0) + (0.22 - novelty) * 0.0055) * dh, 0.0, 1.0)
	var target = 0.28 + 0.4 * (connection - 0.5) + 0.25 * (agency - 0.6) + 0.2 * (novelty - 0.5) - 0.3 * dn - 0.35 * max(stress - 0.45, 0.0) + sim.mood_push(i)
	mood = clamp(mood + ((target - mood) / 30.0 + fx.get("mood", 0.0)) * dh, -1.0, 1.0)
	chronic = lerp(chronic, stress, 0.0008)
	if not asleep: sleep_debt = min(12.0, sleep_debt + 0.33 * dh)
	if is_exercising(): last_ex = now
	if asleep:
		stage = sleep_stage(now)
		if stage == "N3": night.deep += 1.0 / 60.0
		if stage == "REM": night.rem += 1.0 / 60.0
		if int(now - night.start) % 10 == 0: hyp.append(stage)
	if asleep and rng.randf() < (0.0035 + 0.01 * stress + 0.006 * co2f + sim.sleep_noise) * (1.0 if night.quality > 0.0 else 1.0):
		night.wakes_now += 1
		night.quality = clamp(night.quality - 0.04, 0.2, 1.0)
		hyp.append("Wake")

func _stress_base() -> float: return 0.1 + 0.12 * (1.0 - info.resil)

# =====================================================================================================
#  sleep
# =====================================================================================================
func go_to_sleep(now: float, sim) -> void:
	asleep = true
	var hab: Hab = sim.hab
	var q = 0.95 - 0.35 * stress - 0.1 * clamp((hab.co2 - 900.0) / 1500.0, 0.0, 1.0)
	q -= sim.luna.night_light_penalty()
	night = {"hours": 0.0, "wakes": 0, "quality": clamp(q, 0.25, 1.0), "start": now, "wakes_now": 0, "deep": 0.0, "rem": 0.0}
	hyp = []; stage = "N1"; woke_deep = false

## sleep architecture: ~90-min cycles, deep sleep early in the night, REM late
func sleep_stage(now: float) -> String:
	var t = now - night.start
	if t < 12.0: return "N1"
	var cyc = int(t / 90.0)
	var ph = fmod(t, 90.0)
	var deep_len = max(0.0, 32.0 - 9.0 * cyc) * (1.0 - 0.6 * stress)
	var rem_len = 10.0 + 6.0 * cyc
	if ph < 8.0: return "N1"
	if ph < 8.0 + deep_len * 0.3: return "N2"
	if ph < 8.0 + deep_len * 1.3: return "N3"
	if ph > 90.0 - rem_len: return "REM"
	return "N2"

func wake(now: float) -> void:
	if not asleep: return
	asleep = false
	stage = "Wake"
	night.hours = max(0.0, (now - night.start) / 60.0 - 0.15 * night.wakes_now)
	night.wakes = night.wakes_now

# =====================================================================================================
#  movement (fractional minutes, so it's smooth at any speed)
# =====================================================================================================
const SPEED = 2.4   # cells per simulated minute

func move(dt_min: float) -> void:
	if path.is_empty():
		var target = Vector2(cell) * 16.0 + Vector2(8, 8)
		pos = pos.move_toward(target, dt_min * SPEED * 16.0)
		if pos.distance_to(target) < 0.5: face = act_face
		return
	var budget = dt_min * SPEED * 16.0
	while budget > 0.0 and not path.is_empty():
		var nxt: Vector2i = path[0]
		var tp = Vector2(nxt) * 16.0 + Vector2(8, 8)
		var d = pos.distance_to(tp)
		var dir = tp - pos
		if abs(dir.x) > abs(dir.y): face = "side"; flip = dir.x < 0
		elif dir.y < 0: face = "up"
		elif dir.y > 0: face = "down"
		if d <= budget:
			pos = tp; budget -= d; cell = nxt; path.pop_front()
		else:
			pos += dir.normalized() * budget; budget = 0.0
	walk_t += dt_min

func walking() -> bool: return not path.is_empty()

func go(to: Vector2i, hab: Hab) -> void:
	if to == cell: path = []; return
	var p = hab.path(cell, to)
	if p.size() > 1: p.pop_front()
	path = p
