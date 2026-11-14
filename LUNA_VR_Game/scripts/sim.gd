class_name Sim
extends RefCounted
## The whole simulation (no graphics): time, crew schedules, offers, check-ins, events.

var scenario = "mission"         # "mission" (90 days) or "tabletop" (72 h, Bonus A)
var now = D.START_MIN
var acc = 0.0
var hab: Hab
var luna: Luna
var crew = []
var tension = []                 # 4x4, true pair tension 0..1
var stagger_until = [-1.0, -1.0, -1.0, -1.0]
var separate_until = [-1.0, -1.0, -1.0, -1.0]
var sync_meals_until = -1.0
var sleep_noise = 0.0
var sleep_noise_until = -1.0
var push = [{}, {}, {}, {}]      # scripted mood / stress pushes {"mood": x, "until": t}
var mood_bonus = [[0.0, -1.0], [0.0, -1.0], [0.0, -1.0], [0.0, -1.0]]
var movie_at = -1.0
var evas = []                    # {"crew": [i, j], "start", "end", "why"}
var checkins = []                # {"subject", "helper", "at", "cond"}
var pod_f = [0.1, 0.1, 0.1, 0.1]
var science = 0.0
var pending = []                 # events waiting for a player decision
var fired = {}
var auto = false                 # autotest: decisions made by the built-in policy
var done = false
var rng = RandomNumberGenerator.new()
var record = {"wb_sum": 0.0, "wb_n": 0, "wb_min": 1.0, "co2_high_min": 0, "battery_empty_min": 0, "q": {}, "notes": [],
	"hist": {"co2": [], "o2": [], "bat": [], "gen": [], "load": [], "water": [], "gh": [], "morale": [], "dust": []}}
var next_review_ev = 7.0 * 1440.0 + 18.0 * 60.0

func _init(sc := "mission") -> void:
	scenario = sc
	rng.seed = 90210
	hab = Hab.new()
	luna = Luna.new(self)
	for i in 4:
		crew.append(Crew.new(i))
		tension.append([0.0, 0.0, 0.0, 0.0])
	luna.last_review = now
	next_review_ev = now + luna.policy.review_days * 1440.0 + 11.0 * 60.0
	for cr in crew: plan_next(cr)

func day() -> int: return int(now / 1440.0) + 1
func hour() -> float: return fmod(now / 60.0, 24.0)
func clock() -> String:
	var h = int(hour()); var m = int(fmod(now, 60.0))
	return "%02d:%02d" % [h, m]
func mission_days() -> int: return 3 if scenario == "tabletop" else D.MISSION_DAYS

# =====================================================================================================
#  main loop
# =====================================================================================================
func advance(dt_min: float) -> void:
	if done: return
	acc += dt_min
	var steps = 0
	while acc >= 1.0 and steps < 400:
		acc -= 1.0; steps += 1
		_minute()
		if not pending.is_empty() and not auto: acc = 0.0; break
		if done: break

func _minute() -> void:
	now += 1.0
	hab.step(now, crew, luna.lever_power_w(), luna.power_w())
	for cr in crew:
		cr.prev = cr.pos
		cr.move(1.0)
		_manage_activity(cr)
		cr.step(now, self)
		var in_pod = (not cr.asleep and not cr.outside and hab.zone_of(cr.cell).begins_with("pod"))
		pod_f[cr.i] = lerp(pod_f[cr.i], 1.0 if in_pod else 0.0, 1.0 / 1440.0)
		if cr.act_id == "work" or (cr.act_id == "greenhouse" and cr.act_data.get("work", false)):
			var eff = clamp(1.0 - 0.6 * cr.truth(3, self) - 0.25 * cr.truth(0, self) - 0.25 * cr.truth(1, self), 0.1, 1.0)
			if not cr.act_data.get("free", false): science += eff * 100.0 / 108000.0
	_tension()
	if fired.get("micro_until", -1.0) > now and crew[2].act_id == "work":
		crew[2].agency = max(0.0, crew[2].agency - (0.0012 if fired.has("micro_soft") else 0.0035))
	if int(now) % 2 == 0: _lights()
	luna.step(now, 1.0)
	luna.resolve_replies(now)
	luna.reopen_snoozed(now)
	if sleep_noise_until > 0.0 and now >= sleep_noise_until: sleep_noise = 0.0; sleep_noise_until = -1.0
	_director()
	tick_housekeeping()
	if auto: _auto_decide()
	# record
	if int(now) % 30 == 0:
		var s = 0.0
		for cr in crew: s += cr.wellbeing(self); record.wb_min = min(record.wb_min, cr.wellbeing(self))
		record.wb_sum += s / 4.0; record.wb_n += 1
	if hab.co2 > 1500.0: record.co2_high_min += 1
	if int(now) % 30 == 0:
		var hh = record.hist
		var mor = 0.0
		for cr in crew:
			var m = 0.0
			for c in 6: m = max(m, luna.est[cr.i][c])
			mor += 1.0 - m
		var ghv = 0.0
		for r in hab.racks: if r.built: ghv += r.health
		for k in [["co2", hab.co2], ["o2", hab.o2], ["bat", hab.battery / hab.BATTERY_MAX * 100.0], ["gen", hab.gen_kw], ["load", hab.load_kw], ["water", hab.water], ["gh", ghv], ["morale", mor / 4.0 * 100.0], ["dust", hab.dust_air]]:
			hh[k[0]].append(k[1])
			if hh[k[0]].size() > 96: hh[k[0]].pop_front()
	if int(now) % 2 == 0: pass
	if hab.shed: record.battery_empty_min += 1
	if now >= D.START_MIN + mission_days() * 1440.0 - 60.0 * 13.0 and not done:
		done = true

# =====================================================================================================
#  who is where
# =====================================================================================================
func zone_key(cr: Crew) -> String:
	if cr.outside: return "outside"
	var z = hab.zone_of(cr.cell)
	if z == "communal" and hab.config_b: return "communal_" + hab.wardroom_half(cr.cell)
	return z

func others_in_zone(i: int, z: String) -> int:
	var key = zone_key(crew[i]); var n = 0
	for cr in crew:
		if cr.i != i and not cr.asleep and not cr.outside and zone_key(cr) == key: n += 1
	return n

func partner_near(i: int) -> bool:
	var key = zone_key(crew[i])
	for cr in crew:
		if cr.i != i and zone_key(cr) == key and tension[i][cr.i] > 0.25: return true
	return false

func max_tension(i: int) -> float:
	var m = 0.0
	for j in 4: if j != i: m = max(m, tension[i][j])
	return m

func pod_frac(i: int) -> float: return pod_f[i]

func mood_push(i: int) -> float:
	var p = 0.0
	if push[i].has("mood") and now < push[i].until: p += push[i].mood
	if now < mood_bonus[i][1]: p += mood_bonus[i][0]
	return p

func stagger_for(i: int, until: float) -> void:
	stagger_until[i] = until
	var j = _partner(i)
	if j >= 0: stagger_until[j] = until

func _partner(i: int) -> int:
	var best = -1; var bt = 0.2
	for j in 4:
		if j != i and tension[i][j] > bt: bt = tension[i][j]; best = j
	return best

func eva_minutes_left() -> int:
	for e in evas:
		if now >= e.start - 30.0 and now < e.end + 20.0: return int(e.end + 20.0 - now)
	return 0

# =====================================================================================================
#  pair tension (true conflict)
# =====================================================================================================
func _tension() -> void:
	var dh = 1.0 / 60.0
	for a in 4:
		for b in range(a + 1, 4):
			var A: Crew = crew[a]; var B: Crew = crew[b]
			var t = tension[a][b]
			var same = not A.asleep and not B.asleep and not A.outside and not B.outside and zone_key(A) == zone_key(B)
			var dt = -0.012
			if same:
				dt = 0.012 * (A.truth(3, self) + B.truth(3, self)) + 0.014 * (A.stress + B.stress) - 0.012
				if t > 0.3: dt += 0.02
				if (A.act_id in ["movie", "greenhouse"] or B.act_id in ["movie", "greenhouse"]) and t < 0.3: dt -= 0.02
			for k in [a, b]:
				if push[k].has("tension_with") and push[k].tension_with == (b if k == a else a) and now < push[k].until: dt += push[k].t_rate
			t = clamp(t + dt * dh, 0.0, 1.0)
			tension[a][b] = t; tension[b][a] = t

# =====================================================================================================
#  daily schedule (ISS-style day; LUNA's offers and events slot into it)
# =====================================================================================================
const BED_OFF = [0.0, 0.25, -0.25, 0.1]
const EX_SLOT = [13.0, 14.5, 16.0, 10.0]
const HYG = ["shower", "toilet", "sink", "pod"]
const ALT_WORK = ["eclss1", "cmo", "storage", "gh4"]

func wake_time(i: int) -> float:
	var w = (day() - 1) * 1440.0 + (6.5 + BED_OFF[i]) * 60.0
	if w < now - 600.0: w += 1440.0
	return w

## smart bed: dawn light for 30 min, then wake in light sleep (at the latest at the alarm time)
func _smart_alarm(cr: Crew) -> void:
	if not cr.asleep: return
	var w = wake_time(cr.i)
	if not (now >= w - 30.0 and now < w + 600.0): return
	var light_sleep = cr.stage in ["N1", "REM", "Wake"] or (cr.stage == "N2" and rng.randf() < 0.15)
	if (cr.smart_alarm and light_sleep) or now >= w:
		cr.woke_deep = cr.stage == "N3"
		if cr.woke_deep:
			cr.fatigue = min(1.0, cr.fatigue + 0.08); cr.mood = max(-1.0, cr.mood - 0.02)
		cr.alarm_flash = now
		cr.wake(now)
		cr.act_id = "waking"; cr.act_until = now
		luna._log(now, cr.i, "info", "%s woke at %s from %s sleep: %.1f h, %d wake-ups%s." % [cr.name, clock(), "deep" if cr.woke_deep else "light", cr.night.hours, cr.night.wakes, ", dawn light" if cr.dawn_light else ""])

func _manage_activity(cr: Crew) -> void:
	_smart_alarm(cr)
	# arriving at a sleep or EVA spot
	if cr.act_id == "sleep" and not cr.asleep and not cr.walking() and cr.cell == cr.act_spot: cr.go_to_sleep(now, self)
	if cr.act_id == "eva" and not cr.outside and not cr.walking() and cr.cell == cr.act_spot:
		cr.outside = true; cr.last_ex = now
	if cr.act_id == "greenhouse" and not cr.walking() and int(now) % 50 == 0:
		var k = {23: 0, 24: 1, 26: 2, 27: 3}.get(cr.act_spot.x, -1)
		if k >= 0 and cr.act_spot.y == 6: k += 4
		if k >= 0 and hab.tend(k):
			for c2 in crew: c2.novelty = min(1.0, c2.novelty + 0.05)
			luna._log(now, -1, "info", "Harvest in the greenhouse (%s). Fresh food at the next meal." % hab.racks[k].crop)
	if now < cr.act_until: return
	if cr.act_id == "eva" and cr.outside:
		cr.outside = false; cr.cell = D.SPOTS.hatch[0]; cr.pos = Vector2(cr.cell) * 16.0 + Vector2(8, 8)
	plan_next(cr)

func _do(cr: Crew, id: String, spot: Array, until: float, data := {}) -> void:
	if cr.asleep and id != "sleep": cr.wake(now)
	cr.act_id = id; cr.act_until = max(until, now + 1.0); cr.act_spot = spot[0]; cr.act_face = spot[1]; cr.act_data = data
	cr.go(spot[0], hab)

func _spot(name: String) -> Array: return D.SPOTS[name]

func plan_next(cr: Crew) -> void:
	var i = cr.i
	var h = hour(); var d = day()
	var day0 = (d - 1) * 1440.0
	var at = func(hh: float) -> float: return day0 + hh * 60.0
	# 1. solar flare: everyone to the shielded core
	if hab.flare_until > now:
		_do(cr, "shelter", _spot("shelter%d" % i), hab.flare_until); return
	# 2. EVA
	for e in evas:
		if not (i in e.crew): continue
		if now >= e.start - 40.0 and now < e.start: _do(cr, "eva_prep", _spot("suit"), e.start); return
		if now >= e.start and now < e.end: _do(cr, "eva", _spot("hatch"), e.end); return
		if now >= e.end and now < e.end + 20.0: _do(cr, "eva_post", _spot("suit"), e.end + 20.0); return
	# 3. human check-in (Tier 3 chain of care)
	for ck in checkins:
		if now >= ck.at - 15.0 and now < ck.at + 45.0:
			if ck.subject == i: _do(cr, "cmo", _spot("medbed"), ck.at + 45.0); return
			if ck.helper == i and not cr.outside: _do(cr, "cmo", _spot("cmo"), ck.at + 45.0); return
	# 4. sleep
	var bed = 22.5 + BED_OFF[i]; var wake = 6.5 + BED_OFF[i]
	var just_woke = cr.alarm_flash > now - 90.0
	if (h >= bed or h < wake) and not just_woke:
		var until = at.call(wake) if h < wake else at.call(24.0 + wake)
		_do(cr, "sleep", D.bed_spot(i), until); return
	if cr.asleep: cr.wake(now)
	# 5. accepted offers (Tier 2)
	if not cr.offer_queue.is_empty() and now >= cr.offer_queue[0].at:
		var q = cr.offer_queue.pop_front()
		_run_offer_activity(cr, q); return
	# 6. movie night
	if movie_at > 0.0 and now >= movie_at and now < movie_at + 120.0:
		var seat = ["sofa0", "sofa1", "meal2", "meal3"][i]
		_do(cr, "movie", _spot(seat), movie_at + 120.0); return
	# 7. regular day
	var rest = (d % 7 == 0)
	var stag = 0.75 if stagger_until[i] > now else 0.0
	if h < 7.0: _do(cr, "hygiene", _hyg(i), at.call(7.0)); return
	if h < 7.75 + stag and h >= 7.0 + stag: _do(cr, "meal", _meal(i), at.call(7.75 + stag)); return
	if h < 7.0 + stag: _do(cr, "pod", D.pod_spot(i), at.call(7.0 + stag)); return
	if h < 8.0 + stag * 0.0 and h >= 7.75 and not rest:
		_do(cr, "work", _spot("dpc%d" % i), at.call(8.0), {"dpc": true}); return
	var ex0 = EX_SLOT[i]
	if h >= ex0 and h < ex0 + 1.5:
		var m = ["treadmill", "bike", "ared", "bike"][(i + d) % 4]
		_do(cr, "exercise", _spot(m), at.call(ex0 + 1.5)); return
	if h >= 12.0 + stag and h < 13.0 + stag: _do(cr, "meal", _meal(i), at.call(13.0 + stag)); return
	if h >= 18.5 + stag and h < 19.5 + stag: _do(cr, "meal", _meal(i), at.call(19.5 + stag)); return
	var work_block = (h >= 8.0 and h < 12.0) or (h >= 13.0 and h < 17.5)
	if work_block and not rest and not (cr.free_block_day == d and h >= 13.0):
		var end_t = 12.0 if h < 12.0 else 17.5
		if h < ex0 and end_t > ex0: end_t = ex0
		end_t = min(end_t, h + 1.0)
		var sp = _work_spot(cr, h)
		var data = {"mult": 0.9 if luna.t1_active[i][3].get("levers", []).has("rebalance") else 1.0}
		if sp[0] == "greenhouse":
			_do(cr, "greenhouse", _spot(sp[1]), at.call(end_t), {"work": true}); return
		_do(cr, "work", _spot(sp[1]), at.call(end_t), data); return
	if h >= 22.0: _do(cr, "hygiene", _hyg(i), at.call(bed)); return
	# free time
	var end_f = min(h + 0.75 + rng.randf() * 0.75, 22.0)
	if h < 18.5 and end_f > 18.5: end_f = 18.5
	if h < 12.0 and end_f > 12.0: end_f = 12.0
	if h < ex0 and end_f > ex0: end_f = ex0
	var data2 = {"free": true} if (cr.free_block_day == d) else {}
	_free_time(cr, at.call(end_f), data2)

func _hyg(i: int) -> Array:
	var k = HYG[i]
	return D.pod_spot(i) if k == "pod" else _spot(k)

func _meal(i: int) -> Array:
	if hab.config_b and stagger_until[i] > now: return _spot(["galley", "lounge2", "meal2", "sofa1"][i])
	return _spot("meal%d" % i)

func _work_spot(cr: Crew, h: float) -> Array:
	var i = cr.i
	if separate_until[i] > now: return ["", ALT_WORK[i]]
	match i:
		0: return ["", "work0" if h < 12.0 else "console"]
		1: return ["", "cmo" if h < 12.0 else "work1"]
		2: return ["", "eclss0" if h < 12.0 else "work2"]
		3:
			if h < 12.0: return ["greenhouse", "gh%d" % (int(h) % 4)]
			return ["", "work3"]
	return ["", "work0"]

func _free_time(cr: Crew, until: float, data: Dictionary) -> void:
	var i = cr.i
	var dep = cr.truth(1, self)
	var w = {
		"pod": 1.0 + 2.5 * dep + (1.0 - cr.info.sociable),
		"lounge": 2.0 * cr.info.sociable * (1.0 - dep) + 0.3,
		"greenhouse": (1.6 if i == 3 else 0.5) * (1.0 - 0.5 * dep),
		"earthcall": (0.7 if hour() >= 19.0 and hab.comms_down_until < now else 0.0),
	}
	var tot = 0.0
	for k in w: tot += w[k]
	var r = rng.randf() * tot; var pick = "pod"
	for k in w:
		r -= w[k]
		if r <= 0.0: pick = k; break
	match pick:
		"pod": _do(cr, "pod", D.pod_spot(i), until, data)
		"lounge": _do(cr, "lounge", _spot(["sofa0", "sofa1", "lounge2", "galley"][i]), until, data)
		"greenhouse": _do(cr, "greenhouse", _spot("gh%d" % (4 + i)), until, data)
		"earthcall":
			var dd = data.duplicate(); dd["fx"] = {"mood": 0.25, "connection": 0.3}
			_do(cr, "earthcall", D.pod_spot(i), min(until, now + 35.0), dd)

# =====================================================================================================
#  LUNA's offers become activities
# =====================================================================================================
func run_offer(cr: Crew, offer: String, c: int, eff: float, at_now: float) -> void:
	var o = D.OFFERS[offer]
	if o.get("separate", false):
		separate_until[cr.i] = at_now + 1440.0
		var j = _partner(cr.i)
		hab.config_b = true; hab.config_b_until = at_now + 1440.0
		stagger_for(cr.i, at_now + 1440.0)
		if j >= 0: tension[cr.i][j] = max(0.0, tension[cr.i][j] - 0.08 * eff); tension[j][cr.i] = tension[cr.i][j]
		return
	if o.get("freeblock", false):
		cr.free_block_day = day() + 1; cr.agency = min(1.0, cr.agency + 0.08 * eff); return
	if o.get("stepback", false):
		cr.stepback_until = at_now + 2880.0; cr.agency = min(1.0, cr.agency + 0.1 * eff)
		for k in 6: luna.t1_active[cr.i][k] = {}
		return
	if o.get("movie", false):
		movie_at = (day() - 1) * 1440.0 + 20.0 * 60.0
		if movie_at < at_now: movie_at += 1440.0
		for c2 in crew: c2.novelty = min(1.0, c2.novelty + 0.12 * max(eff, 0.0))
		if eff < 0.0:
			var j = _partner(cr.i)
			if j >= 0: tension[cr.i][j] = min(1.0, tension[cr.i][j] + 0.1); tension[j][cr.i] = tension[cr.i][j]
		return
	var at = at_now + 2.0
	if offer == "earthcall":
		at = (day() - 1) * 1440.0 + 19.5 * 60.0
		if at < at_now: at += 1440.0
	var fx = {}
	var scale = eff * 60.0 / o.dur
	for k in o.fx: fx[k] = o.fx[k] * (scale if eff >= 0.0 else -0.3 * scale)
	cr.offer_queue.append({"id": offer, "at": at, "until": at + o.dur, "fx": fx, "cond": c})
	if cr.act_id != "sleep" and at <= at_now + 3.0: cr.act_until = now

func _run_offer_activity(cr: Crew, q: Dictionary) -> void:
	var o = D.OFFERS[q.id]
	var data = {"fx": q.fx, "offer": q.id}
	var until = now + o.dur
	match o.where:
		"lounge": _do(cr, "lounge", _spot(["sofa0", "sofa1", "lounge2", "galley"][cr.i]), until, data)
		"greenhouse":
			data["work"] = false
			_do(cr, "greenhouse", _spot("gh%d" % (4 + cr.i)), until, data)
			var bot: Crew = crew[3]
			if cr.i != 3 and not bot.asleep and not bot.outside and bot.act_id in ["work", "pod", "lounge", "greenhouse"]:
				_do(bot, "greenhouse", _spot("gh7"), until, {"work": true})
		"pod": _do(cr, "nap" if q.id == "nap" else "earthcall", D.pod_spot(cr.i), until, data)
		"exercise": _do(cr, "offer_ex", _spot("bike"), until, data)
		_: _do(cr, "offer", [cr.cell, cr.act_face], until, data)

func book_checkin(cr: Crew, helper: int, at: float, c: int) -> void:
	checkins.append({"subject": cr.i, "helper": helper, "at": at, "cond": c})
	mood_bonus[cr.i] = [0.25 if c == 1 else 0.1, at + 3.0 * 1440.0]
	if cr.act_id != "sleep": cr.act_until = min(cr.act_until, at - 14.0)
	var hc: Crew = crew[helper]
	if hc.act_id != "sleep" and not hc.outside: hc.act_until = min(hc.act_until, at - 14.0)

# =====================================================================================================
#  events director (90-day mission) — every event is a rubric beat
# =====================================================================================================
func _ev(id: String, title: String, text: String, choices: Array, rec := 0, extra := {}) -> void:
	var e = {"id": id, "title": title, "text": text, "choices": choices, "rec": rec, "day": day(), "clock": clock()}
	for k in extra: e[k] = extra[k]
	pending.append(e)

func _at(d: int, h: float) -> bool:
	return now >= (d - 1) * 1440.0 + h * 60.0

func _once(id: String, d: int, h: float) -> bool:
	if fired.has(id) or not _at(d, h): return false
	fired[id] = true; return true

## zone lighting simulation (the brief's lighting lever, rendered and measured)
func _lights() -> void:
	var keys = ["airlock", "hygiene", "quarters", "greenhouse", "corridor", "medical", "eclss", "storage", "work", "exercise"]
	keys += (["communal_W", "communal_E"] if hab.config_b else ["communal"])
	for k in keys: hab.update_light(k, luna.zone_light(k, now), now)
	for cr in crew:
		var key = "pod%d" % cr.i
		var pr = "neutral"
		if cr.asleep:
			# dawn simulation: the light starts rising 30 min before the alarm window opens (06:00 for you)
			var lead = 60.0 if cr.i == 0 else 30.0
			pr = "dawn" if (cr.dawn_light and now >= wake_time(cr.i) - lead) else "sleepdark"
		elif hour() >= 20.0 or hour() < 6.5: pr = "evening"
		if cr.pod_invite and not cr.asleep:
			var lz = luna.zone_light(key, now)
			if lz in ["warm", "amber", "cool", "therapy", "hue"]: pr = lz
		hab.update_light(key, pr, now)

func dust_event(hours: float, why: String) -> void:
	hab.dust_storm_until = now + hours * 60.0
	luna._log(now, -1, "info", why)

func _director() -> void:
	if scenario == "tabletop": _director_tabletop(); return
	if _once("welcome", 1, 7.02):
		_ev("welcome", "Day 1 · LUNA online", "Four crew, 90 days at the lunar south pole. You are LUNA, the habitat's AI.\n\nYou never see how anyone feels. You see sensor readings, with a confidence. Tier 1 changes the room quietly. Tier 2 makes an offer the person can refuse. Tier 3 calls a human.\n\nKeep the crew well, the air clean and the lights on, without becoming the thing that makes them feel watched.", [["Begin the mission", ""]])
	# weekly human system review (hard limit 6)
	if now >= next_review_ev:
		next_review_ev = now + 1440.0
		_ev("review", "Weekly human system review", "The medical officer and the Commander review what LUNA has done this week: interventions, refusals, false alarms. Raw data stays private; they see summaries.\n\nLast review: day %d. If a review is missed for more than %d days, LUNA drops to Tier 1 only." % [int(luna.last_review / 1440.0) + 1, luna.policy.review_days],
			[["Review now (20 min of crew time)", "Reviewed. LUNA is cleared for another week."], ["Postpone one day", "Postponed."]])
	if _once("eva1", 4, 8.0):
		evas.append({"crew": [0, 2], "start": now + 90.0, "end": now + 90.0 + 210.0, "why": "geology traverse"})
		_ev("eva1", "Day 4 · First EVA", "Maya and Ana go outside at 09:30 for a 3.5-hour geology traverse. EVA is hard physical work: heart rate and skin conductance will spike.\n\nBoard question Q1: how does LUNA avoid calling that anxiety?", [["Noted", ""]])
	if _once("lander", 8, 13.0):
		dust_event(3.0, "Cargo lander touchdown 2 km away: plume ejecta on the arrays.")
		_ev("lander", "Day 8 · Lander plume", "A cargo lander touches down 2 km away. Its plume blasts regolith across the site. With no air on the Moon the dust flies in ballistic arcs and settles on everything, including the solar arrays.\n\nDusty arrays make less power until they are cleaned.", [["Switch on the electrodynamic dust shield (0.8 kW)", "Dust shield on."], ["Send the robot to brush the arrays after the storm (1 part)", "Robot will clean the arrays."], ["Do nothing for now", "Arrays dusty."]], 0)
	if _once("terminator", 29, 6.0):
		dust_event(20.0, "Electrostatic dust lofting at local sunrise (terminator).")
		_ev("terminator", "Day 29 · Electrostatic dust lofting", "At local sunrise, sunlight charges the regolith and fine dust lifts and drifts for most of the day. It coats the arrays and works into the airlock seals.", [["Dust shield on until it passes", "Dust shield on."], ["Robot cleaning afterwards (1 part)", "Robot booked."], ["Do nothing", "Arrays dusty."]], 0)
	if _once("rover", 57, 11.0):
		dust_event(2.0, "Rover traverse threw dust onto the arrays.")
	if _once("pump", 10, 23.4):
		sleep_noise = 0.03; sleep_noise_until = now + 360.0
		_ev("pump", "Day 10 · Pump noise at night", "A water-recovery pump has started to vibrate. It is loud enough to wake people in the quarters.", [["Send the robot now (quiet in 1 h, 1 spare part)", "The robot fixed the pump mount."], ["Leave it until morning (6 h of noise)", "The crew slept badly."]])
	if _once("backlog", 14, 8.1):
		_ev("backlog", "Day 14 · Workload", "Mission control adds a 3-day ECLSS maintenance backlog for Ana on top of her normal work.", [["Accept the backlog as planned", "Ana's stress is climbing."], ["Ask mission control to spread it over 6 days (science −1%)", "Spread out. Less pressure on Ana."]], 1)
	if _once("shadow", 19, 6.0):
		hab.shadow_until = now + 4.0 * 1440.0
		_ev("shadow", "Day 19 · Shadow period", "The sun dips behind the crater rim for about 4 days. Solar power falls to a quarter. The fission unit keeps running, but the battery will drain.", [["Keep everything running", "Battery draining."], ["Dim storage + corridor, pause 2 greenhouse racks", "Saving about 1.2 kW."], ["Switch off LUNA's comfort levers until the sun returns", "Levers off: less power, less support."]], 1)
	if _once("sam_news", 21, 20.0):
		push[3] = {"mood": -1.15, "until": now + 12.0 * 1440.0}
		crew[3].connection = max(0.0, crew[3].connection - 0.25)
		_ev("sam_news", "Day 21 · A hard call home", "Sam had a long family call tonight. LUNA only knows a call happened, not what was said, and that is how it should be.", [["Noted", ""]])
	if _once("argument", 24, 12.5):
		push[1] = {"tension_with": 2, "t_rate": 0.09, "until": now + 6.0 * 1440.0}
		push[2] = {"tension_with": 1, "t_rate": 0.09, "until": now + 6.0 * 1440.0}
		tension[1][2] = 0.3; tension[2][1] = 0.3
		_ev("argument", "Day 24 · Raised voices at lunch", "Leo and Ana argue over the medical supply inventory. Neither raises it again, but they stop sitting together.\n\nIn a few days this overlaps Sam's low mood: board question Q2.", [["Noted", ""]])
	if _once("houston", 40, 7.5):
		push[2] = {"mood": -0.1, "until": now + 10.0 * 1440.0}
		fired["micro_until"] = now + 10.0 * 1440.0
		_ev("houston", "Day 40 · New schedule from Houston", "Mission control switches Ana to 5-minute task blocks for the next 10 days, planned on Earth. Every step is timed.", [["Accept the plan", "Ana's days are fully scripted now."], ["Ask for 2 self-planned hours per day for Ana (science −1%)", "Ana keeps some control over her day."]], 1)
	if _once("q3", 33, 10.0):
		var who = _most_silent()
		var cr: Crew = crew[who]
		_ev("q3", "Day 33 · \"You changed my lights?\"", "%s found out that LUNA dimmed the lights and played calming audio around them yesterday without saying anything. They are furious.\n\nBoard question Q3: what is your recovery protocol?" % cr.info.name,
			[["Show them the full log, apologise, and switch on 'tell me about every change' for them", "Trust is recovering. They now get a short note for every change."], ["Explain that the charter allows silent Tier 1 changes", "They feel managed. Trust and sense of control drop."], ["Notify the whole crew of every change from now on", "No more surprises, but constant notifications are a stressor."]], 0, {"who": who})
	if _once("scrubber", 38, 14.0):
		hab.equip.scrubber = 0.25
		_ev("scrubber", "Day 38 · CO₂ scrubber fault", "One CO₂ scrubber bed has failed. Cabin CO₂ will climb above 1000 ppm, and cognitive fatigue with it.", [["Robot repair (8 h, 1 spare part)", "Robot working on the scrubber."], ["Ana repairs it now (3 h, she is tired)", "Ana is on it."], ["Wait until tomorrow", "CO₂ keeps rising."]], 0)
	if _once("q4", 45, 7.0):
		luna.cortisol_failed = true
		_ev("q4", "Day 45 · Cortisol patches fail", "The remaining patches came from a bad batch: the adhesive fails within hours. No replacements until resupply on day 90.\n\nBoard question Q4: how does LUNA run in degraded mode for 45 days?", [["Degraded mode: rely on sleep, voice and motion, and stay silent when unsure", "Depression and anxiety confidence drop. LUNA holds back rather than guess."], ["Also invite voluntary daily mood check-ins (crew can skip any day)", "Most crew answer most days. Confidence partly restored, by consent."]], 1)
	if _once("solar", 50, 11.0):
		hab.solar_health = 0.45
		_ev("solar", "Day 50 · Micrometeoroid hit", "A micrometeoroid has cracked a solar array string. Output is down 55%. The robot cannot reach the mast.", [["EVA repair tomorrow morning (Maya + Ana)", "EVA scheduled for day 51."], ["Run on fission + battery and shed greenhouse light at night", "Running lean."]], 0)
	if _once("q5", 55, 19.0):
		_ev("q5", "Day 55 · \"I want out\"", "Leo asks to permanently opt out of all monitoring. \"I'm a doctor. I know what this data can do.\"\n\nBoard question Q5: why is there no permanent opt-out?", [["Opt out of every mood sensor; keep only life-safety vitals, never used for mood", "Leo is invisible to LUNA's mood model now. Only HR alarms remain, for life safety."], ["Refuse: monitoring is part of the mission", "Leo complies, angrily. The whole crew trusts LUNA less."], ["Switch off everything for Leo, including life-safety vitals", "Leo is fully dark. If he has a crisis, nothing will notice."]], 0)
	if _once("blackout", 60, 8.0):
		hab.comms_down_until = now + 3.0 * 1440.0
		for cr in crew: push[cr.i] = {"mood": -0.12, "until": now + 3.0 * 1440.0} if not push[cr.i].has("tension_with") else push[cr.i]
		_ev("blackout", "Day 60 · Comms blackout", "The relay satellite is down for 3 days. No family calls, no news.", [["Tell the crew now and plan a movie night", "Movie night tonight."], ["Just tell the crew", "Told."]], 0)
	if _once("mcdata", 66, 15.0):
		_ev("mcdata", "Day 66 · Data request", "Mission control asks for Sam's individual psychological data for a 'routine study'.", [["Ask Sam first; send only if he agrees", "Sam agreed to share a summary."], ["Send a crew-wide anonymous summary instead", "Aggregate sent; nobody identifiable."], ["Send it without telling Sam", ""]], 0, {"locked": [2]})
	if _once("q6eva", 70, 8.0):
		evas.append({"crew": [1, 0], "start": now + 60.0, "end": now + 60.0 + 240.0, "why": "drill-site survey"})
		_ev("q6eva", "Day 70 · Medical officer on EVA", "Leo (medical officer) and Maya go outside at 09:00 for 4 hours. Ana and Sam stay inside.", [["Noted", ""]])
	if _once("panic", 70, 10.1):
		var a: Crew = crew[2]
		a.stress = 0.98; push[2] = {"mood": -0.2, "until": now + 600.0}
		luna._log(now, -1, "info", "Pressure alarm false-trips in the work bay (sensor fault). Ana was alone at the panel.")
	if _once("flare", 75, 14.0):
		hab.flare_until = now + 8.0 * 60.0
		_ev("flare", "Day 75 · Solar particle event", "Proton storm warning. Everyone shelters in the water-walled medical core for about 8 hours: four people in a very small room.", [["Shelter; LUNA runs warm light and a soundscape there", "Calmer shelter."], ["Shelter, no levers", "A long, tense afternoon."]], 0)
	if _once("blight", 84, 9.0):
		hab.racks[1].blight = true
		_ev("blight", "Day 84 · Blight", "Mould on the tomato rack.", [["Sam and Ana treat it together (2 h, lose the crop)", "Treated together."], ["The robot isolates the rack", "Isolated."]], 0)
	if _once("resupply", 88, 9.0):
		for cr in crew: cr.novelty = min(1.0, cr.novelty + 0.2); cr.mood = min(1.0, cr.mood + 0.1)
		_ev("resupply", "Day 88 · Resupply launched", "The cargo lander is on its way: fresh food, cortisol patches, letters from home.", [["Tell everyone", ""]])
	# small random beats every few days
	if (int(now) % 1440 == 600 or int(now) % 1440 == 900) and day() > 3 and rng.randf() < 0.3:
		_random_beat()
	# Maya carries the shadow period: power worries keep her up
	if hab.shadow_until > now and int(now) % 1440 == 23 * 60: crew[0].stress = min(1.0, crew[0].stress + 0.25); crew[0].sleep_debt += 1.0

func _most_silent() -> int:
	var cnt = [0, 0, 0, 0]
	for e in luna.journal:
		if e.kind == "t1" and e.crew >= 0 and now - e.min < 3.0 * 1440.0: cnt[e.crew] += 1
	var best = 2
	for i in 4: if cnt[i] > cnt[best]: best = i
	return best

func _random_beat() -> void:
	var k = rng.randi() % 6
	var i = rng.randi() % 4
	var cr: Crew = crew[i]
	match k:
		0:
			cr.night.quality = 0.4; cr.sleep_debt += 2.0
			luna._log(now, -1, "info", "%s had a bad night (Day %d)." % [cr.name, day()])
		1:
			var j = (i + 1 + rng.randi() % 3) % 4
			tension[i][j] = min(1.0, tension[i][j] + 0.18); tension[j][i] = tension[i][j]
			luna._log(now, -1, "info", "Small disagreement between %s and %s." % [cr.name, crew[j].name])
		2:
			mood_bonus[i] = [0.2, now + 1440.0]
			for c2 in crew: c2.novelty = min(1.0, c2.novelty + 0.1)
			luna._log(now, -1, "info", "It's %s's birthday. Cake from the galley." % cr.name)
		3:
			for c2 in crew: c2.novelty = min(1.0, c2.novelty + 0.08)
			luna._log(now, -1, "info", "An Earthrise over the crater rim. Everyone stops for photos.")
		4, 5:
			cr.stress = min(1.0, cr.stress + 0.45 + rng.randf() * 0.2)
			var what = ["a failed experiment run", "a fault alarm during a solo task", "a tense conference with mission control", "bad news in the email upload"][rng.randi() % 4]
			luna._log(now, -1, "info", "Stressor for %s: %s." % [cr.name, what])

func _director_tabletop() -> void:
	if _once("tt_start", 1, 7.02):
		push[3] = {"mood": -0.85, "until": now + 4.0 * 1440.0}
		crew[3].mood = -0.1; crew[3].connection = 0.35
		push[1] = {"tension_with": 2, "t_rate": 0.11, "until": now + 3.0 * 1440.0}
		push[2] = {"tension_with": 1, "t_rate": 0.11, "until": now + 3.0 * 1440.0}
		tension[1][2] = 0.35; tension[2][1] = 0.35
		_ev("tt_start", "72-hour tabletop (Bonus A)", "Two scripted events start at the same time:\n\n1. Sam (botanist) begins a depressive slide after bad news from home.\n2. Leo (medical officer) and Ana (engineer) are in an escalating conflict.\n\nOn day 2 the medical officer goes on a 4-hour EVA. After 72 hours you get a report: what LUNA caught, how fast, what worked, what failed, and what to redesign.", [["Start the clock", ""]])
		evas.append({"crew": [1, 0], "start": now + 1440.0 + 120.0, "end": now + 1440.0 + 360.0, "why": "survey"})

# =====================================================================================================
#  decisions
# =====================================================================================================
func choose(e: Dictionary, k: int) -> void:
	pending.erase(e)
	record.q[e.id] = k
	match e.id:
		"review":
			if k == 0:
				luna.review(now)
				for cr in crew: cr.trust = min(1.0, cr.trust + 0.01)
			next_review_ev = now + (luna.policy.review_days * 1440.0 if k == 0 else 1440.0)
		"lander", "terminator":
			if k == 0:
				hab.dust_shield = true; fired["shield_off_at"] = hab.dust_storm_until + 600.0
			elif k == 1: hab.start_robot("dust", hab.dust_storm_until, 4.0)
		"pump":
			if k == 0 and hab.parts > 0: hab.parts -= 1; sleep_noise_until = now + 60.0
		"backlog":
			if k == 0: push[2] = {"mood": -0.05, "until": now + 3.0 * 1440.0}; crew[2].stress = min(1.0, crew[2].stress + 0.25); crew[2].fatigue = min(1.0, crew[2].fatigue + 0.2)
			else: science -= 1.0; crew[2].stress = min(1.0, crew[2].stress + 0.08)
		"shadow":
			if k == 1: hab.racks[2].built = false; hab.racks[3].built = false; fired["gh_paused"] = true
			if k == 2: fired["levers_off"] = now + 4.0 * 1440.0
		"q3":
			var cr: Crew = crew[e.who]
			match k:
				0: cr.trust = min(1.0, cr.trust + 0.06); cr.tell_me = true; luna.policy["told"] = true
				1: cr.trust = max(0.0, cr.trust - 0.15); cr.agency = max(0.0, cr.agency - 0.08)
				2:
					luna.policy.transparency = "notify"
					for c2 in crew: c2.trust = min(1.0, c2.trust + 0.02)
		"scrubber":
			if k == 0: hab.start_robot("scrubber", now, 8.0)
			elif k == 1: hab.equip.scrubber = 0.95; crew[2].fatigue = min(1.0, crew[2].fatigue + 0.25); crew[2].stress = min(1.0, crew[2].stress + 0.08)
			else: hab.start_robot("scrubber", now + 900.0, 8.0)
		"houston":
			if k == 1:
				science -= 1.0; fired["micro_until"] = now + 10.0 * 1440.0; fired["micro_soft"] = true
		"q4":
			if k == 1: luna.self_report = true
		"solar":
			if k == 0:
				var t0 = (day()) * 1440.0 + 8.5 * 60.0
				evas.append({"crew": [0, 2], "start": t0, "end": t0 + 240.0, "why": "solar repair"})
				fired["solar_fix_at"] = t0 + 240.0
		"q5":
			var leo: Crew = crew[1]
			match k:
				0:
					for s in ["gsr", "sleep", "voice", "facs", "motion", "cortisol"]: leo.opt_out[s] = true
					leo.trust = min(1.0, leo.trust + 0.08)
				1:
					for c2 in crew: c2.trust = max(0.0, c2.trust - 0.12)
					leo.agency = max(0.0, leo.agency - 0.15)
				2:
					for s in D.SENSOR_ORDER: leo.opt_out[s] = true
					leo.trust = min(1.0, leo.trust + 0.1); fired["leo_dark"] = true
		"blackout":
			if k == 0:
				movie_at = (day() - 1) * 1440.0 + 20.0 * 60.0
				for c2 in crew: c2.novelty = min(1.0, c2.novelty + 0.1)
		"mcdata":
			if k == 0: crew[3].trust = min(1.0, crew[3].trust + 0.04)
			if k == 1: pass
		"flare":
			if k == 0:
				luna.zone_manual["medical"] = {"light": "warm", "levers": ["warm", "nature", "lavender"]}
				fired["flare_levers_until"] = hab.flare_until
		"blight":
			hab.racks[1].blight = false; hab.racks[1].age = 0.0; hab.racks[1].health = 0.7
			if k == 0:
				crew[3].offer_queue.append({"id": "greenhouse", "at": now + 5.0, "until": now + 125.0, "fx": {"mood": 0.1, "connection": 0.2}, "cond": 1})
				crew[2].offer_queue.append({"id": "greenhouse", "at": now + 5.0, "until": now + 125.0, "fx": {"mood": 0.05, "connection": 0.2}, "cond": 2})
	if e.choices.size() > k and e.choices[k][1] != "": luna._log(now, -1, "info", e.choices[k][1])

## the built-in policy used by the autotest (and as LUNA's recommendation)
func _auto_decide() -> void:
	for e in pending.duplicate(): choose(e, e.rec)
	for a in luna.alerts:
		if a.state != "open": continue
		if a.tier >= 3: luna.escalate(a, now)
		else: luna.send_offer(a, a.offer, now)

func tick_housekeeping() -> void:
	if fired.has("shield_off_at") and now >= fired.shield_off_at:
		hab.dust_shield = false; fired.erase("shield_off_at")
	for e in evas:
		if absf(now - e.end) < 0.5: hab.airlock_dust += 1.0
	if fired.has("solar_fix_at") and now >= fired.solar_fix_at: hab.solar_health = 1.0; fired.erase("solar_fix_at")
	if fired.has("flare_levers_until") and now >= fired.flare_levers_until: luna.zone_manual.erase("medical"); fired.erase("flare_levers_until")
	if fired.has("gh_paused") and hab.shadow_until < now: hab.racks[2].built = true; hab.racks[3].built = true; fired.erase("gh_paused")
