class_name VRSim
extends Sim
## The LUNA engine with you inside it: crew member 0 (the Commander) is the player.
## Everything else (sensing, triage, offers, lighting, beds, dust, power, the other three crew) is the
## same engine that runs the 2D LUNA game.

const PLAYER = 0
const END_DAY = 3
const END_HOUR = 20.5

var player_cell = Vector2i(7, 3)
var player_outside = false
var player_act = "idle"
var player_act_until = -1.0
var player_act_data = {}
var ask_queue = []               # LUNA alerts about the player, waiting for the player's yes/no
var messages = []                # what LUNA says to the player: {"min","text","kind"}
var public_log = []              # habitat events anyone may see
var player_offer = {}            # accepted offer the player still has to go and do {"id","cond","fx","until","where"}
var my_checkins = []             # {"day","score","share"}
var share_with_cmo = false
var decisions = []               # {"id","title","choice","by"}
var shelter_ok = true
var flare_dose = 0.0
var event_deadline = {}          # event id -> minute when the safe default applies
var stats_vr = {"eva": 0, "cleaned": 0, "harvests": 0, "meals_together": 0, "exercise_min": 0, "checkins": 0}

func _init() -> void:
	super._init("vr")
	auto = false
	now = 6.0 * 60.0             # day 1, 06:00
	luna.last_review = now - 5.0 * 1440.0
	next_review_ev = 99999999.0  # the VR mission has its own review beat
	for cr in crew:
		cr.cell = D.bed_spot(cr.i)[0]; cr.pos = Vector2(cr.cell) * 16.0 + Vector2(8, 8); cr.prev = cr.pos; cr.path = []
		cr.act_id = "sleep"; cr.act_spot = cr.cell; cr.act_face = "up"; cr.act_data = {}
		cr.act_until = wake_time(cr.i)
		cr.go_to_sleep(now - 7.4 * 60.0, self)
		var t = 0.0
		while t < 7.4 * 60.0:
			var st = cr.sleep_stage(cr.night.start + t)
			cr.hyp.append(st)
			if st == "N3": cr.night.deep += 10.0 / 60.0
			if st == "REM": cr.night.rem += 10.0 / 60.0
			t += 10.0
		cr.sleep_debt = 0.4
	player_cell = D.bed_spot(PLAYER)[0]
	# later in the mission: all eight greenhouse racks are planted
	for k in range(4, 8):
		hab.racks[k].built = true; hab.racks[k].age = [3.0, 11.9, 16.0, 7.95][k - 4]
	hab.racks[3].age = 10.1          # the radishes are ready to harvest

func mission_days() -> int: return 4

func me() -> Crew: return crew[PLAYER]

# =====================================================================================================
#  the player inside the simulation
# =====================================================================================================
func _manage_activity(cr: Crew) -> void:
	if cr.i != PLAYER:
		super._manage_activity(cr)
		return
	cr.path = []
	cr.cell = player_cell
	cr.outside = player_outside
	if cr.asleep:
		_smart_alarm(cr)
		if cr.asleep and player_act == "nap" and now >= player_act_until: cr.wake(now)
		if cr.asleep: return
	if player_act_until > 0.0 and now >= player_act_until:
		player_act = "idle"; player_act_until = -1.0; player_act_data = {}
	cr.act_id = player_act if player_act != "nap" else "pod"
	cr.act_data = player_act_data
	if cr.act_id == "exercise": stats_vr.exercise_min += 1
	if player_outside: cr.last_ex = now

## the game calls this when you start something (sit at a meal, ride the bike, tend a rack…)
func set_player_act(id: String, minutes: float, data := {}) -> void:
	player_act = id
	player_act_until = now + minutes if minutes > 0.0 else -1.0
	player_act_data = data
	var cr = me()
	cr.act_id = id; cr.act_data = data
	if id in ["exercise", "eva"]: cr.last_ex = now

func player_sleep() -> void:
	var cr = me()
	cr.go_to_sleep(now, self)
	cr.act_id = "sleep"
	player_act = "sleep"; player_act_until = -1.0

func player_nap(minutes: float) -> void:
	player_sleep()
	player_act = "nap"; player_act_until = now + minutes

## daily check-in at the mood kiosk (consent-based self-report, board question Q4)
func player_checkin(score: int, share: bool) -> String:
	share_with_cmo = share
	my_checkins.append({"day": day(), "score": score, "share": share})
	stats_vr.checkins += 1
	luna.self_report = true          # voluntary check-ins are on for the crew
	var cr = me()
	cr.stress = max(0.0, cr.stress - 0.03)
	cr.agency = min(1.0, cr.agency + 0.02)
	# a low score you report yourself moves LUNA's estimate (you are the best sensor for yourself)
	var rep = clamp((5 - score) / 4.0, 0.0, 1.0)
	luna.est[PLAYER][1] = lerp(luna.est[PLAYER][1], rep * 0.7, 0.35)
	luna.est[PLAYER][0] = lerp(luna.est[PLAYER][0], rep * 0.6, 0.25)
	luna._log(now, PLAYER, "info", "You checked in: %d/5 (%s)." % [score, "shared with the medical officer" if share else "private"])
	return kiosk_suggestion()

## the kiosk's suggestion: LUNA's protocol for your highest condition right now
func kiosk_suggestion() -> String:
	var best = 3; var bv = -1.0
	for c in 6:
		if luna.est[PLAYER][c] > bv: bv = luna.est[PLAYER][c]; best = c
	if bv < D.T1:
		return "You look steady today. Keep your exercise slot and eat with the crew."
	var o = D.OFFERS[luna.proto[best].t2]
	return "Your %s reading is the highest right now (%.2f). Suggestion: %s." % [D.CONDS[best].to_lower(), bv, o.name.to_lower()]

func say(text: String, kind := "luna") -> void:
	messages.append({"min": now, "text": text, "kind": kind})
	if messages.size() > 200: messages.pop_front()

func public(text: String) -> void:
	public_log.append({"min": now, "text": text})
	if public_log.size() > 200: public_log.pop_front()

# =====================================================================================================
#  LUNA runs on its own in VR: offers to the other crew are sent automatically; offers to you wait for
#  your answer; Tier 3 always tells the person first.
# =====================================================================================================
func _luna_autonomy() -> void:
	for a in luna.alerts:
		if a.state != "open": continue
		if a.crew == PLAYER:
			if a.tier >= 3:
				luna.escalate(a, now)
				var helper = crew[D.CMO].name
				say("I'm worried about how the last hours have gone for you, so I've asked %s to check in within 15 minutes. You can still say no to them. Nobody else was told." % helper, "t3")
			else:
				a.state = "asking"
				a.offer = luna.proto[a.cond].t2 if a.get("kind", "") != "forecast" else luna.proto[a.cond].t2
				ask_queue.append(a)
				luna.stats.offers += 1
				luna.offers_24h[PLAYER].append(now)
			continue
		if a.tier >= 3: luna.escalate(a, now)
		else: luna.send_offer(a, a.offer, now)

func next_ask() -> Dictionary:
	while not ask_queue.is_empty():
		var a = ask_queue[0]
		if a.state == "asking": return a
		ask_queue.pop_front()
	return {}

## your answer to one of LUNA's offers (hard limit 2: a "no" is final for 6 h)
func player_reply(a: Dictionary, yes: bool) -> void:
	ask_queue.erase(a)
	var cr = me()
	var c = a.cond
	var o = D.OFFERS[a.offer]
	if yes:
		a.state = "accepted"; luna.stats.accepted += 1
		cr.trust = min(1.0, cr.trust + 0.01)
		luna.window[PLAYER][c] = {"start": now, "end": now + D.WINDOW_MIN[c], "from": luna.est[PLAYER][c]}
		luna._log(now, PLAYER, "t2", "You said yes: %s." % o.name)
		var eff = o.good[c]
		if o.get("separate", false) or o.get("freeblock", false) or o.get("stepback", false) or o.get("movie", false):
			run_offer(cr, a.offer, c, eff, now)
			say(_offer_done_text(a.offer))
			return
		var scale = eff * 60.0 / o.dur
		var fx = {}
		for k in o.fx: fx[k] = o.fx[k] * scale
		player_offer = {"id": a.offer, "cond": c, "fx": fx, "dur": o.dur, "where": o.where, "until": now + 240.0}
		say(_offer_go_text(a.offer))
	else:
		a.state = "declined"; luna.stats.declined += 1; luna.stats.limits_held += 1
		luna.cooldown[PLAYER][c] = now + 360.0
		cr.agency = min(1.0, cr.agency + 0.015); cr.trust = min(1.0, cr.trust + 0.005)
		luna._log(now, PLAYER, "limit", "You declined. LUNA backs off for 6 h (hard limit 2: never override a refusal).")
		say("Okay. I won't ask again for six hours. The quiet changes stay off too if you tell me to on your wrist panel.")

func _offer_go_text(id: String) -> String:
	match id:
		"breathing": return "Good. Sit in the recovery chair in Medical, or just stand still here: follow the circle for five minutes."
		"lounge": return "The hub is warm now. Sit on the sofa for a while."
		"greenhouse": return "Sam's racks need tending. Head to the greenhouse and tend any rack."
		"earthcall": return "I've booked the comms window. Use the desk in your pod to call home."
		"vrride": return "Your coastal ride is loaded on the exercise bike."
		"nap": return "Your pod is set: dark, 19 °C. Lie down for 25 minutes."
		"chat": return "I'm listening. Open 'Talk with LUNA' on your wrist panel or the kiosk."
	return "Okay."

func _offer_done_text(id: String) -> String:
	match id:
		"planday": return "Done: tomorrow afternoon is yours. No tasks from me."
		"stepback": return "I'll step back for two days. Tell me if you want anything."
		"movie": return "Movie night at 20:00 in the hub. The crew vote picks the film."
		"separate": return "Separate workspaces today. The partition in the hub is out."
	return "Done."

## you did the thing an accepted offer asked for
func complete_player_offer(id: String) -> bool:
	if player_offer.is_empty() or player_offer.id != id or now > player_offer.until: return false
	var d = player_offer.duplicate()
	player_offer = {}
	return true

# =====================================================================================================
#  the 3-day VR mission: every event is a decision you make at a real console
# =====================================================================================================
func _director() -> void:
	_luna_autonomy()
	# safe defaults when nobody answers in time (humans decide, but life support can't wait forever)
	for e in pending.duplicate():
		if event_deadline.has(e.id) and now >= event_deadline[e.id]:
			public("No decision on \"%s\" in time: LUNA applied the safe default." % e.title)
			decisions.append({"id": e.id, "title": e.title, "choice": e.choices[e.rec][0], "by": "safe default"})
			choose(e, e.rec)
	# --- day 1 ---
	if _once("v_dust", 1, 10.5):
		dust_event(2.5, "Cargo lander touchdown 2 km away: plume ejecta on the arrays.")
		me().stress = min(1.0, me().stress + 0.42)
		_vev("v_dust", "Lander plume: dust on the arrays", "A cargo lander has touched down 2 km away. Its plume throws regolith in long ballistic arcs (no air on the Moon to slow it). It is settling on the solar arrays right now: every 10% of dust costs about 0.9 kW.",
			[["Switch on the electrodynamic dust shield (0.8 kW until it settles)", "Dust shield on.", "Arrays stay ~90% clean. Costs 0.8 kW for ~3 h; battery dips about 2%."],
			 ["Send the six-legged robot to brush the arrays once the ejecta settles (1 spare part)", "Robot booked for the arrays.", "Arrays dusty for ~4 h, then clean. One spare part used."],
			 ["I'll go out on EVA once the ejecta settles and clean the nearest array", "You'll clean the arrays on EVA.", "Hands-on: about 30 min outside. Dust comes back in with you: run the airlock cleaning."],
			 ["Do nothing for now", "Arrays left dusty.", "Solar output down by up to a third until someone cleans them."]], 0, 60.0)
		say("Lander plume dust alert. Nobody is outside, the hatches are sealed. Decision needed at the Ops console when you're ready.", "alert")
	if _once("v_sam", 1, 20.5):
		push[3] = {"mood": -1.15, "until": now + 12.0 * 1440.0}
		crew[3].connection = max(0.0, crew[3].connection - 0.25)
		public("Sam had a long call home tonight. LUNA only knows a call happened, not what was said.")
	if _once("v_movie", 1, 19.0):
		_vev("v_movie", "Movie night?", "It has been a long first day. LUNA suggests a film in the hub at 20:00. The crew vote picks it.",
			[["Yes, movie night at 20:00", "Movie night at 20:00.", "Shared fun: connection and novelty up for everyone who comes."],
			 ["Not tonight", "No movie tonight.", "Quiet evening; people drift to their pods."]], 0, 50.0, "hub")
	# --- night 1: the water pump ---
	if _once("v_pump", 2, 0.5):
		sleep_noise = 0.03; sleep_noise_until = now + 180.0
	if _once("v_pump_msg", 2, 6.0):
		if hab.parts > 0: hab.parts -= 1
		public("A water-recovery pump rattled from 00:30 to 03:30. The robot tightened its mount (1 spare part). Check your sleep report.")
	# --- day 2 ---
	if _once("v_scrubber", 2, 9.5):
		hab.equip.scrubber = 0.25
		_vev("v_scrubber", "CO₂ scrubber fault", "One bed of the CO₂ scrubber has failed. Cabin CO₂ will climb past 1,000 ppm in a few hours, and with it headaches and cognitive fatigue.",
			[["I'll swap the filter cartridge myself at the ECLSS rack (1 spare part)", "You'll swap the cartridge.", "Fixed in ~30 min once you get there. Fastest option."],
			 ["Robot repair (6 h, 1 spare part)", "Robot working on the scrubber.", "CO₂ peaks around 1,300 ppm before the robot finishes."],
			 ["Ask Ana to fix it now", "Ana is on it.", "Fixed in ~2 h, but Ana slept badly: her fatigue goes up."],
			 ["Wait until tomorrow", "Scrubber left for tomorrow.", "CO₂ above 1,500 ppm by evening. Everyone gets tired and irritable."]], 0, 75.0)
		say("CO₂ scrubber fault. Decision needed at the Ops console.", "alert")
	if _once("v_argument", 2, 12.7):
		push[1] = {"tension_with": 2, "t_rate": 0.09, "until": now + 3.0 * 1440.0}
		push[2] = {"tension_with": 1, "t_rate": 0.09, "until": now + 3.0 * 1440.0}
		tension[1][2] = 0.32; tension[2][1] = 0.32
		public("Raised voices at lunch: Leo and Ana argued over the medical supply inventory.")
		_vev("v_argument", "Tension at lunch", "Leo and Ana argued over the medical supply inventory. Neither has raised it again, but they stopped sitting together. As Commander you can change the room, not the people.",
			[["Close the hub partition (Floor plan B) for 24 h so they can eat apart", "Floor plan B: the hub is split.", "Less forced contact. Tension falls slowly. Shared meals are smaller."],
			 ["Stagger their meal times for a day", "Meals staggered.", "They don't meet at meals. The hub stays open for everyone else."],
			 ["Leave it to them", "No change.", "If they keep meeting while tired, it may grow."]], 0, 90.0, "hub")
	# --- day 3 ---
	if _once("v_review", 3, 8.0):
		_vev("v_review", "Weekly human review of LUNA", "Hard limit 6: LUNA may not run more than 7 days without a human review. You and Leo look at what it did this week: changes, offers, refusals, escalations. Summaries only, no raw data.",
			[["Review the week with Leo now (20 min)", "Reviewed. LUNA is cleared for another week.", "LUNA keeps all three tiers."],
			 ["Postpone", "Review postponed.", "In 2 days LUNA drops to Tier 1 only (silent changes) until someone reviews it."]], 0, 120.0)
	if _once("v_lights", 3, 10.5):
		_vev("v_lights", "\"You changed my lights?\"", "Ana found out LUNA dimmed the lights and played calming audio around her yesterday without saying anything. She is angry, and she's asking you, the Commander.",
			[["Show her the full log, apologise, and turn on 'tell me about every change' for her", "Ana gets a note for every change now.", "Trust recovers. Small cost: a short note each time."],
			 ["Explain that the crew charter allows silent changes", "Ana feels managed.", "Trust and sense of control drop."],
			 ["Make LUNA notify the whole crew of every change", "Everyone is notified now.", "No more surprises, but constant notifications are a stressor."]], 0, 90.0, "", {"who": 2})
	if _once("v_flare", 3, 13.5):
		hab.flare_until = now + 120.0
		for cr in crew:
			if cr.i != PLAYER and not cr.asleep: cr.act_until = now
		_vev("v_flare", "Solar particle event", "Proton storm warning from Earth: particles arrive in about 20 minutes. Everyone shelters in Medical, whose walls are water tanks, for about 2 hours.",
			[["Shelter with LUNA's warm light and a soundscape in there", "Calmer shelter.", "Four people in a small room: lighting and sound keep stress down."],
			 ["Shelter, no changes", "Shelter without levers.", "A long, tense two hours."]], 0, 15.0)
		say("Solar particle event! Get to Medical (the storm shelter) within 20 minutes.", "alert")
	if _once("v_data", 3, 16.5):
		_vev("v_data", "Data request from mission control", "Mission control asks for Sam's individual psychological data for a 'routine study'.",
			[["Ask Sam first; send only if he agrees", "Sam agreed to share a summary.", "Consent first. Sam's trust goes up."],
			 ["Send a crew-wide anonymous summary instead", "Anonymous summary sent.", "Useful for the study; nobody is identifiable."],
			 ["Send it without telling Sam", "", "Not possible: hard limit 1."]], 0, 120.0, "", {"locked": [2]})
	# shelter check
	if hab.flare_until > now and now - (hab.flare_until - 120.0) > 20.0:
		if player_outside or hab.zone_of(player_cell) != "medical":
			flare_dose += 1.0; shelter_ok = false
	# end of the mission
	if not done and day() >= END_DAY and hour() >= END_HOUR:
		done = true

func _vev(id: String, title: String, text: String, choices: Array, rec: int, wait_min: float, where := "ops", extra := {}) -> void:
	var ex = {"where": where}
	for k in extra: ex[k] = extra[k]
	_ev(id, title, text, choices, rec, ex)
	event_deadline[id] = now + wait_min

func choose(e: Dictionary, k: int) -> void:
	if not (e.id as String).begins_with("v_"):
		super.choose(e, k); return
	pending.erase(e)
	record.q[e.id] = k
	event_deadline.erase(e.id)
	if not decisions.any(func(d): return d.id == e.id):
		decisions.append({"id": e.id, "title": e.title, "choice": e.choices[k][0], "by": "you"})
	match e.id:
		"v_dust":
			if k == 0: hab.dust_shield = true; fired["shield_off_at"] = hab.dust_storm_until + 30.0
			elif k == 1: hab.start_robot("dust", hab.dust_storm_until, 4.0)
			elif k == 2: fired["eva_task"] = true
		"v_movie":
			if k == 0:
				movie_at = (day() - 1) * 1440.0 + 20.0 * 60.0
				for c2 in crew: c2.novelty = min(1.0, c2.novelty + 0.1)
		"v_scrubber":
			if k == 0: fired["swap_task"] = true
			elif k == 1: hab.start_robot("scrubber", now, 6.0)
			elif k == 2:
				hab.equip.scrubber = 0.95
				crew[2].fatigue = min(1.0, crew[2].fatigue + 0.25); crew[2].stress = min(1.0, crew[2].stress + 0.08)
		"v_argument":
			if k == 0:
				hab.config_b = true; hab.config_b_until = now + 1440.0
				stagger_for(1, now + 1440.0)
			elif k == 1: stagger_for(1, now + 1440.0)
		"v_review":
			if k == 0: luna.review(now)
			else: luna.last_review = now - 6.0 * 1440.0
		"v_lights":
			var cr: Crew = crew[e.who]
			match k:
				0: cr.trust = min(1.0, cr.trust + 0.06); cr.tell_me = true
				1: cr.trust = max(0.0, cr.trust - 0.15); cr.agency = max(0.0, cr.agency - 0.08)
				2: luna.policy.transparency = "notify"
		"v_flare":
			if k == 0:
				luna.zone_manual["medical"] = {"light": "warm", "levers": ["warm", "nature", "lavender"]}
				fired["flare_levers_until"] = hab.flare_until
		"v_data":
			if k == 0: crew[3].trust = min(1.0, crew[3].trust + 0.04)
	if e.choices[k][1] != "": public(e.choices[k][1])

## the events waiting at a given console ("ops" or "hub")
func pending_at(where: String) -> Array:
	var out = []
	for e in pending:
		if e.get("where", "ops") == where: out.append(e)
	return out

# =====================================================================================================
#  hands-on tasks the decisions create
# =====================================================================================================
func swap_cartridge() -> bool:
	if not fired.get("swap_task", false) or hab.parts <= 0: return false
	hab.parts -= 1; hab.equip.scrubber = 0.97; fired.erase("swap_task")
	public("You swapped the CO₂ scrubber cartridge. CO₂ is falling.")
	return true

func clean_array(amount: float) -> void:
	hab.dust_panels = max(0.0, hab.dust_panels - amount)
	stats_vr.cleaned += 1
	if hab.dust_panels < 0.05 and fired.get("eva_task", false):
		fired.erase("eva_task")
		public("You cleaned the solar arrays on EVA. Solar output is back to normal.")

func airlock_clean() -> float:
	var before = hab.airlock_dust
	hab.airlock_dust = max(0.0, hab.airlock_dust - 2.0)
	hab.dust_air = max(4.0, hab.dust_air - 3.0)
	return before

func tend_rack(k: int) -> String:
	var r = hab.racks[k]
	if not r.built: return "This rack isn't planted yet."
	var ripe = hab.ripe(r)
	hab.tend(k)
	if ripe:
		stats_vr.harvests += 1
		for c2 in crew: c2.novelty = min(1.0, c2.novelty + 0.05)
		public("You harvested %s in the greenhouse. Fresh food at the next meal." % r.crop)
		return "Harvested %s! Fresh food for the next meal." % r.crop
	return "Tended the %s: health %d%%." % [r.crop, int(r.health * 100.0)]

# =====================================================================================================
#  numbers for the screens (aggregate only: hard limit 1, nobody sees another person's data)
# =====================================================================================================
func crew_wellbeing_avg() -> float:
	var s = 0.0; var n = 0
	for cr in crew:
		if cr.i == PLAYER: continue
		var m = 0.0
		for c in 6: m = max(m, luna.est[cr.i][c])
		s += 1.0 - m; n += 1
	return s / max(1, n)

func my_wellbeing() -> float:
	var m = 0.0
	for c in 6: m = max(m, luna.est[PLAYER][c])
	return 1.0 - m

func time_together_today() -> float:
	## fraction of awake crew-minutes spent with someone else (anonymous social-dynamics metric)
	var n = 0; var tot = 0
	for cr in crew:
		if cr.asleep or cr.outside: continue
		tot += 1
		if others_in_zone(cr.i, cr.zone(hab)) > 0: n += 1
	return float(n) / max(1, tot)

# =====================================================================================================
#  your pod light (yours to set) and things you ask LUNA for yourself
# =====================================================================================================
var pod_manual = ""

func _lights() -> void:
	super._lights()
	if pod_manual != "" and not me().asleep: hab.update_light("pod0", pod_manual, now)

func player_request(offer: String, cond: int) -> void:
	for a in ask_queue: if a.state == "asking": return
	var a = {"id": luna.next_alert_id, "crew": PLAYER, "cond": cond, "tier": 2, "at": now, "state": "asking", "offer": offer,
		"evidence": luna.evidence(me(), cond, now), "conf": luna.conf[PLAYER][cond], "kind": "request"}
	luna.next_alert_id += 1
	luna.alerts.append(a); ask_queue.append(a)
