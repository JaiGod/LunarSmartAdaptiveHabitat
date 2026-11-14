class_name Selene
extends Node
## SELENE inside LUNA VR: the crew's personal health companion (port of the team's SELENE app,
## repo lunar-crew-monitor). LUNA runs the habitat; SELENE talks with *you* about your body and mind.
##
## The "big three" senses, all optional and all switched on by you:
##   - face: expressions, blinks, eye closure, yawns (browser camera, MediaPipe FaceLandmarker)
##   - posture: upright vs slumped, fidgeting (browser camera, MediaPipe PoseLandmarker; in VR: your headset)
##   - voice: spoken commands ("Selene, …"), plus loudness and pitch (browser microphone, Web Speech API)
## Only scores (0..1) and the words of commands reach the game. Nothing is recorded or stored.
## Not a medical device: it never diagnoses, and a person (the CMO) handles anything serious.

const CO2_LIMIT = 3.0          # NASA-STD-3001 Vol 2 [V2 6004]: 1-hour average ppCO2 <= 3 mmHg
const CO2_WATCH = 2.5          # early warning: crews report headaches at levels below the limit, so SELENE nudges first
const CABIN_KPA = 101.3        # sea-level air (20.9 % O2) in this habitat
const RAD_CAREER = 600.0       # NASA career limit, mSv
const EX_GOAL = 120.0          # exercise countermeasures, minutes a day (ISS practice)
const RES_GOAL = 60.0          # resistive part of it
const WATER_GOAL = 2500.0      # ml a day
const PRIOR_DOSE = [41.5, 63.0, 18.2, 37.8]   # mSv from earlier missions (career so far)
const EXERCISE = ["ex", "bike", "exercise", "offer_ex"]
const MOODS = ["happy", "calm", "tired", "anxious", "sad", "lonely"]
const CANDS = {"happy": ["celebrate", "joke", "music"], "calm": ["music", "light_relax", "none"],
	"tired": ["light_day", "nap_window", "music", "joke"], "anxious": ["breathing", "ocean_relax", "music", "talk"],
	"sad": ["family_call", "earth_view", "joke", "talk", "crew_activity"], "lonely": ["family_call", "crew_activity", "earth_view", "talk"]}
const JOKES = ["Why did the astronaut break up with the Moon? It needed space, and the Moon kept going through phases.",
	"What's an astronaut's favourite key on the keyboard? The space bar.", "How do you throw a party on the Moon? You planet.",
	"What do you call a tick on the Moon? A luna-tick.", "Why did the Sun skip college? It already had a million degrees.",
	"I tried squats in one-sixth gravity. My legs said thanks. My coffee said goodbye.",
	"Why did the water recycler get promoted? It always turned things around.",
	"What did the Moon say to the astronaut who forgot to drink water? You're going through a dry phase."]
const HELP = "Say \"Selene\" then: how am I doing · start my check-in · I feel anxious (or tired, sad, lonely, happy, calm) · breathing exercise · my brainwaves · outside conditions · radiation dose · crew status · lights warm · temperature 21 · music off · take me to the greenhouse · AI off · autopilot off · open dashboard · tell me a joke · what have you learned."

var g
var sim
# ---- sensing ----
var consent = false                         # you agreed to camera / microphone use this session
var ask_consent = []                        # [face, posture, voice] waiting for your yes on the dashboard
const CONSENT = ["Face: expressions and blinks. Posture: slumped or upright, fidgeting. Voice: spoken commands, loudness and pitch.",
	"Face and posture are analysed in this browser tab: no picture or video is recorded or sent anywhere. Only scores (0..1) reach the game.",
	"Voice: loudness and pitch stay in this tab. Turning speech into words is done by your browser's speech service (in Chrome, Google's servers); only the words of your commands reach the game.",
	"You can stop at any time (button, or say \"Selene, stop sensing\"). Not a medical device."]

func give_consent(yes: bool) -> void:
	var a = ask_consent; ask_consent = []
	if g.selene_ui: g.selene_ui.dirty = true
	if not yes or a.is_empty(): _say("Okay, sensing stays off. Everything else works without it."); return
	consent = true
	start_sensing(a[0], a[1], a[2])
var want = {"face": false, "posture": false, "voice": false}
var web = false
var js_ready = false
var js = {}                                 # last poll from the browser module
var poll_t = 0.0
var scores = {}                             # fused: {"fatigue","stress","low_mood","mood","conf","source","calibrating"}
var checkin = {}                            # last finished check-in: {"t","fatigue","stress","low_mood","status","raw"}
var checkin_until = -1.0                    # real-time seconds left in a running 30 s check-in
var ck_acc = {}
var vr_head = {"base": -1.0, "n": 0, "sum": 0.0, "h": 0.0, "pitch": 0.0, "move": 0.0, "prev": Basis(), "slump": 0.0, "down": 0.0, "fidget": 0.0, "t": 0.0}
# ---- per-crew health model ----
var vit = [{}, {}, {}, {}]
var hr_hist = [[], [], [], []]
var ex_min = [0.0, 0.0, 0.0, 0.0]
var res_min = [0.0, 0.0, 0.0, 0.0]
var water = [0.0, 0.0, 0.0, 0.0]
var rad = [0.0, 0.0, 0.0, 0.0]
var rad_today = [0.0, 0.0, 0.0, 0.0]
var day_now = -1
var share_board = [true, true, false, true]   # who chose to appear on the Ops crew-health wall (you can change yours)
# ---- assistant ----
var chat = []                               # {"who": "you"|"selene"|"alert", "text", "fb": {"mood","a"} }
var learn = {}                              # "i:mood" -> action -> [n, s]
var moods = [[], [], [], []]
var last_mood = {}
var alerts = []                             # {"sev","text","t"}
var sched = []                              # {"what","when"}
var said_flags = {}
var tts = true
var state = "idle"                          # idle | listening | thinking | speaking
var state_t = 0.0
var paused = false
var breath = {}                             # {"t": seconds, "round": n}
var confirm = ""                            # pending spoken confirmation ("luna_on")
var heard = ""
var rx = {}                                 # compiled RegEx cache

func setup(game) -> void:
	g = game; sim = g.sim
	web = OS.has_feature("web")
	_load_learn()
	for i in 4: _vitals(i, 0.0)
	chat.append({"who": "selene", "text": "Hi, I'm SELENE, your health companion. LUNA runs the habitat; I look after you. Tap a question, or switch on face, posture and voice sensing. Nothing is recorded."})

# =====================================================================================================
#  tick
# =====================================================================================================
func tick(dt: float, dm: float) -> void:
	state_t -= dt
	if state_t <= 0.0 and state in ["thinking", "speaking"]: state = "listening" if want.voice and _js_on("sr") else "idle"
	if sim.day() != day_now:
		day_now = sim.day()
		for i in 4: ex_min[i] = 0.0; res_min[i] = 0.0; water[i] = 0.0; rad_today[i] = 0.0
	if dm > 0.0:
		for i in 4: _account(i, dm)
	for i in 4: _vitals(i, dt)
	_sense(dt)
	_flags()
	if not breath.is_empty():
		breath.t += dt
		if breath.t > 4 * 19.0 + 1.0: breath = {}; _say("Nice work. Four rounds done. How do you feel now?")

## minutes of exercise, water and radiation, from what each person is actually doing in the habitat
func _account(i: int, dm: float) -> void:
	var cr = sim.crew[i]
	var act = str(cr.act_id)
	if i == 0 and not g.activity.is_empty(): act = str(g.activity.get("id", act))
	if act in EXERCISE: ex_min[i] += dm
	if act in ["ex", "exercise", "offer_ex"]: res_min[i] += dm * 0.5      # ISS days split about half resistive (ARED), half aerobic
	if not cr.asleep: water[i] += dm * 1.25 + (dm * 15.0 if act in ["meal", "table", "snack", "coffee"] else 0.0)
	var H = sim.hab
	var dose = 0.6 / 1440.0                                    # inside under regolith: about 0.6 mSv/day
	if cr.outside: dose = 1.37 / 1440.0                        # lunar surface GCR (Chang'E-4 LND: ~1.37 mSv/day)
	if H.flare_until > sim.now:
		dose += 0.25 / 60.0 if cr.outside else (0.002 if cr.zone(H) == "medical" else 0.02)
	rad[i] += dose * dm; rad_today[i] += dose * dm

## a believable physiological picture for each person, driven by their real state in the simulation
func _vitals(i: int, dt: float) -> void:
	var cr = sim.crew[i]
	var act = str(cr.act_id)
	if i == 0 and not g.activity.is_empty(): act = str(g.activity.get("id", act))
	var ex = 1.0 if act in EXERCISE else 0.0
	var walking = 1.0 if cr.path.size() > 0 else 0.0
	var debt = cr.debt_n()
	var hr_t = 54.0 if cr.asleep else 62.0 + 30.0 * cr.stress + 6.0 * cr.fatigue + 9.0 * walking + 72.0 * ex
	var v = vit[i]
	var a = clamp(dt * 0.6, 0.0, 1.0) if not v.is_empty() else 1.0
	var hr = lerp(float(v.get("hr", hr_t)), hr_t, a) + randf_range(-0.6, 0.6)
	var hrv = clamp(74.0 - 40.0 * cr.stress - 12.0 * cr.fatigue - 8.0 * debt + (12.0 if cr.asleep else 0.0) - 25.0 * ex, 12.0, 110.0)
	var o2 = sim.hab.o2
	var spo2 = clamp(98.0 - max(0.0, 19.5 - o2) * 2.5 - ex * 1.0, 85.0, 100.0)
	var eeg = _eeg(cr, act)
	var e = sim.luna.est[i]
	vit[i] = {"hr": hr, "hrv": lerp(float(v.get("hrv", hrv)), hrv, a), "spo2": spo2, "temp": 36.6 + 0.6 * ex + 0.1 * cr.stress,
		"sys": 112.0 + 14.0 * cr.stress + 18.0 * ex, "dia": 72.0 + 8.0 * cr.stress + 4.0 * ex, "pi": clamp(2.2 + 2.0 * ex - 1.2 * cr.stress, 0.3, 6.0),
		"sleep": float(cr.night.get("hours", 7.0)), "eeg": eeg, "eeg_state": _eeg_state(eeg, cr.asleep),
		"balance": int(clamp(93.0 - 25.0 * cr.fatigue - 10.0 * debt, 40.0, 99.0)),
		"fatigue": e[3], "stress": e[0], "low_mood": e[1], "act": act, "asleep": cr.asleep}
	var h: Array = hr_hist[i]
	if h.is_empty() or Time.get_ticks_msec() - int(v.get("_t", 0)) > 400:
		h.append(hr); vit[i]["_t"] = Time.get_ticks_msec()
		if h.size() > 90: h.pop_front()
	else: vit[i]["_t"] = v.get("_t", 0)

func _eeg(cr, act: String) -> Dictionary:
	var d = 0.15; var th = 0.15; var al = 0.3; var be = 0.3; var ga = 0.1
	if cr.asleep:
		match str(cr.stage):
			"N3": d = 0.55; th = 0.2; al = 0.1; be = 0.1; ga = 0.05
			"REM": d = 0.15; th = 0.35; al = 0.15; be = 0.25; ga = 0.1
			_: d = 0.3; th = 0.35; al = 0.15; be = 0.15; ga = 0.05
	else:
		th += 0.25 * cr.fatigue; d += 0.1 * cr.fatigue
		be += 0.25 * cr.stress; ga += 0.08 * cr.stress; al -= 0.15 * cr.stress
		if act in ["sofa", "earth", "read", "bench", "greenhouse", "lounge", "breath"]: al += 0.12
	var s = d + th + al + be + ga
	return {"delta": d / s, "theta": th / s, "alpha": al / s, "beta": be / s, "gamma": ga / s}

func _eeg_state(e: Dictionary, asleep: bool) -> String:
	if asleep: return "asleep (deep sleep = delta)" if e.delta > 0.4 else "asleep"
	if e.theta + e.delta > 0.42: return "drowsy"
	if e.alpha >= e.beta and e.alpha > 0.3: return "relaxed and calm"
	if e.gamma > 0.14: return "high alert"
	return "focused"

# =====================================================================================================
#  the health picture: physical flags, habitat, outside
# =====================================================================================================
func ppco2() -> float: return sim.hab.co2 * 1e-6 * CABIN_KPA * 7.50062
func phys(i: int) -> Dictionary:
	var v = vit[i]; var fl = []
	if v.hrv < 35.0 and not v.act in EXERCISE: fl.append(["ORANGE", "Heart-rate variability low (%d ms): poor recovery or high stress." % int(v.hrv)])
	if v.sleep < 6.0: fl.append(["YELLOW", "Short sleep (%.1f h). Aim for 8 hours." % v.sleep])
	if ex_min[i] < 60.0 and sim.hour() > 15.0: fl.append(["YELLOW", "Exercise is well below today's countermeasure target (%d of %d min)." % [int(ex_min[i]), int(EX_GOAL)]])
	if v.spo2 < 95.0: fl.append(["RED", "Blood oxygen %d %%." % int(v.spo2)])
	var c = ppco2()
	if c > CO2_LIMIT: fl.append(["RED", "Cabin CO₂ %.1f mmHg is over NASA's 3 mmHg limit. Check the CO₂ scrubber." % c])
	elif c > CO2_WATCH: fl.append(["YELLOW", "Cabin CO₂ %.1f mmHg: headache risk rises above 2.5 mmHg." % c])
	if water[i] < 800.0 and sim.hour() > 14.0: fl.append(["YELLOW", "Drink some water: %d of %d ml today." % [int(water[i]), int(WATER_GOAL)]])
	var st = "GREEN"
	for f in fl:
		if f[0] == "RED": st = "RED"
		elif f[0] == "ORANGE" and st != "RED": st = "ORANGE"
		elif st == "GREEN": st = "YELLOW"
	return {"flags": fl, "status": st}

func mental(i: int) -> Dictionary:
	if i == 0 and not scores.is_empty() and not scores.get("calibrating", false):
		return {"fatigue": scores.fatigue, "stress": scores.stress, "low_mood": scores.low_mood, "source": scores.source}
	if i == 0 and g.selene_live and g.selene_data.get("stress") != null and g.selene_data.get("source", "") != "SELENE in-game":
		var d = g.selene_data
		return {"fatigue": float(d.get("fatigue", 0.0)), "stress": float(d.get("stress", 0.0)), "low_mood": float(d.get("low_mood", 0.0)), "source": "SELENE app on this PC"}
	var v = vit[i]
	return {"fatigue": v.fatigue, "stress": v.stress, "low_mood": v.low_mood, "source": "habitat sensors, LUNA's estimate"}

static func mstatus(m: Dictionary) -> String:
	var x = max(m.fatigue, max(m.stress, m.low_mood))
	return "ORANGE" if x >= 0.7 else ("YELLOW" if x >= 0.45 else "GREEN")

func exterior() -> Dictionary:
	var H = sim.hab
	var flare = H.flare_until > sim.now
	var dust = 7 if H.dust_storm_until > sim.now else (2 if H.dust_air < 15.0 else 4)
	var sun = H.sun
	var d = {"elev": lerp(-1.5, 1.6, sun), "temp": -170.0 + 130.0 * sun, "rad": 1500.0 if flare else 57.0,
		"solar": 8 if flare else 2, "dust": dust, "day": sun > 0.15}
	d["eva_ok"] = not flare and dust < 6
	var notes = ["low sun on the crater rim" if d.day else "in shadow: extreme cold"]
	if flare: notes.append("solar particle event: everyone stays in the storm shelter")
	if dust >= 6: notes.append("lander plume dust in the air")
	d["notes"] = notes
	return d

func stability(i: int) -> int:
	var xs: Array = moods[i].slice(-10)
	if xs.size() < 2: return -1
	var val = {"happy": 9.0, "calm": 8.0, "tired": 5.0, "anxious": 3.0, "sad": 2.0, "lonely": 3.0}
	var mu = 0.0
	for m in xs: mu += val[m]
	mu /= xs.size()
	var sd = 0.0
	for m in xs: sd += pow(val[m] - mu, 2)
	sd = sqrt(sd / xs.size())
	return int(round(max(0.0, 100.0 - sd * 22.0)))

func _flags() -> void:
	var p = phys(0)
	for f in p.flags:
		var key = f[1].substr(0, 22)
		if said_flags.get(key, -1) != sim.day():
			said_flags[key] = sim.day()
			if f[0] in ["RED", "ORANGE"]: alert(f[0], f[1], false)

func alert(sev: String, text: String, chat_it := true) -> void:
	alerts.push_front({"sev": sev, "text": text, "t": sim.clock()})
	if alerts.size() > 12: alerts.pop_back()
	if chat_it: _log("alert", ("Warning: " if sev == "RED" else "Caution: ") + text)

# =====================================================================================================
#  sensing: browser camera + microphone (web build), SELENE app bridge (PC), headset (VR)
# =====================================================================================================
func start_sensing(face: bool, posture: bool, voice: bool) -> void:
	if not consent:
		# asked inside the SELENE dashboard itself (never hidden behind it), before the browser's own permission prompt
		ask_consent = [face, posture, voice]
		open_dash(false)
		if g.selene_ui: g.selene_ui.dirty = true
		return
	want.face = face or want.face; want.posture = posture or want.posture; want.voice = voice or want.voice
	if g.player.vr and (face or posture):
		_say("In the headset I read your posture from the headset itself: how you hold your head, slumping and fidgeting. A headset camera can't see your face, so face sensing works on the PC or browser version.")
	if web and _js_init():
		JavaScriptBridge.eval("window.seleneSense.start(%s,%s,%s)" % [str(face and not g.player.vr).to_lower(), str(posture and not g.player.vr).to_lower(), str(voice).to_lower()], true)
		if voice and not JavaScriptBridge.eval("window.seleneSense.hasSpeech()", true):
			_say("This browser can't turn speech into words (Chrome or Edge can). I'll still read your voice's loudness and pitch, and you can tap the commands.")
		_say("Starting. Your browser will ask for permission. I use the first 20 seconds to learn your normal face and posture.")
	elif not web:
		_say("On the PC version the camera and microphone run in the SELENE app: start selene_bridge.py next to SELENE and your check-ins flow in here live. Headset posture works on its own.")
	g.play("click")

func stop_sensing() -> void:
	want = {"face": false, "posture": false, "voice": false}
	if web and js_ready: JavaScriptBridge.eval("window.seleneSense.stop()", true)
	scores = {}
	if g.selene_data.get("source", "") == "SELENE in-game": g.selene_live = false; g.selene_data = {}
	_say("Sensing is off. Nothing was stored.")

func _js_init() -> bool:
	if js_ready: return true
	if not web: return false
	var src = FileAccess.get_file_as_string("res://web/selene_sense.js")
	if src == "": return false
	JavaScriptBridge.eval(src, true)
	js_ready = bool(JavaScriptBridge.eval("!!window.seleneSense", true))
	return js_ready

func _js_on(k: String) -> bool: return str(js.get(k, "off")) == "on"

func _sense(dt: float) -> void:
	# --- browser camera / microphone
	if web and js_ready and (want.face or want.posture or want.voice):
		poll_t -= dt
		if poll_t <= 0.0:
			poll_t = 0.25
			var s = JavaScriptBridge.eval("window.seleneSense.poll()", true)
			var d = JSON.parse_string(str(s)) if s != null else null
			if d is Dictionary:
				var was_err = str(js.get("err", ""))
				js = d
				if str(d.get("err", "")) != "" and str(d.err) != was_err: _say("I couldn't start that: %s." % d.err)
				heard = str(d.get("heard", ""))
				for t in d.get("said", []): _heard(str(t))
				var sc = d.get("scores")
				if sc is Dictionary:
					scores = {"fatigue": float(sc.fatigue), "stress": float(sc.stress), "low_mood": float(sc.low_mood), "mood": str(sc.mood), "conf": float(sc.conf),
						"calibrating": bool(sc.calibrating), "source": "SELENE · your camera + microphone, live"}
	# --- headset posture (VR): head height and angle are real measurements of how you sit and stand
	if g.player.vr and want.posture:
		var cam: Node3D = g.player.cam
		var hh = cam.global_position.y - g.player.global_position.y
		var pitch = -cam.global_transform.basis.z.y
		var vh = vr_head
		vh.t += dt
		var mv = (cam.global_transform.basis.z - vh.prev.z).length() / max(dt, 0.001)
		vh.prev = cam.global_transform.basis
		vh.move = lerp(float(vh.move), mv, 0.05)
		if vh.t < 20.0:
			vh.sum += hh; vh.n += 1
		elif vh.base < 0.0: vh.base = vh.sum / max(1, vh.n)
		if vh.base > 0.0:
			vh.slump = lerp(float(vh.slump), clamp((vh.base - hh) / 0.14, 0.0, 1.0), 0.03)
			vh.down = lerp(float(vh.down), clamp((-pitch - 0.35) / 0.4, 0.0, 1.0), 0.02)
			vh.fidget = clamp(vh.move * 0.35, 0.0, 1.0)
			var f = clamp(0.6 * vh.slump + 0.5 * vh.down, 0.0, 1.0)
			var s2 = clamp(0.8 * vh.fidget, 0.0, 1.0)
			var lo = clamp(0.5 * vh.slump + 0.2 * vh.down, 0.0, 1.0)
			if scores.is_empty() or scores.get("source", "").begins_with("SELENE · headset"):
				scores = {"fatigue": f, "stress": s2, "low_mood": lo, "mood": "tired" if f > 0.55 else ("anxious" if s2 > 0.55 else "calm"), "conf": 0.4,
					"calibrating": false, "source": "SELENE · headset posture, live"}
	# --- a running 30-second check-in collects an average
	if checkin_until > 0.0:
		checkin_until -= dt
		if not scores.is_empty() and not scores.calibrating:
			for k in ["fatigue", "stress", "low_mood"]: ck_acc[k] = ck_acc.get(k, 0.0) + scores[k]
			ck_acc["n"] = ck_acc.get("n", 0) + 1
		if checkin_until <= 0.0: _finish_checkin()
	# --- live scores feed LUNA, exactly like the SELENE app does over the bridge
	if not scores.is_empty() and not scores.calibrating and not paused:
		g.selene_live = true
		g.selene_data = {"connected": true, "source": "SELENE in-game", "stress": scores.stress, "fatigue": scores.fatigue, "low_mood": scores.low_mood}
		var cr = sim.me()
		cr.stress = lerp(cr.stress, scores.stress, dt * 0.02)
		cr.fatigue = lerp(cr.fatigue, scores.fatigue, dt * 0.015)
		cr.mood = lerp(cr.mood, 0.45 - 1.1 * scores.low_mood, dt * 0.01)

func start_checkin() -> void:
	if paused: _say("Monitoring is paused. Say \"resume monitoring\" first."); return
	var live = not scores.is_empty() or (web and (want.face or want.posture)) or (g.player.vr and want.posture)
	if not live:
		if web or g.player.vr:
			start_sensing(true, true, false)
			if not consent: return
		else:
			_finish_estimate(); return
	ck_acc = {}; checkin_until = 30.0
	_say("Starting your 30-second check-in. Relax and look at the screen; I'm reading your face and posture.")

func _finish_checkin() -> void:
	checkin_until = -1.0
	if ck_acc.get("n", 0) < 5: _finish_estimate(); return
	var n = float(ck_acc.n)
	var f = int(100.0 * ck_acc.fatigue / n); var s = int(100.0 * ck_acc.stress / n); var l = int(100.0 * ck_acc.low_mood / n)
	var st = "ORANGE" if max(f, max(s, l)) >= 70 else ("YELLOW" if max(f, max(s, l)) >= 45 else "GREEN")
	checkin = {"t": sim.clock(), "fatigue": f, "stress": s, "low_mood": l, "status": st, "raw": "measured live: %s" % scores.get("source", "")}
	_say("Check-in done: fatigue %d, stress %d, low mood %d, overall %s. These use fixed weights against your own baseline, so treat them as a hint, not a diagnosis.%s" % [f, s, l, st.to_lower(), " You're looking well today." if st == "GREEN" else " A short break and some water might help. Want a breathing exercise?"])

func _finish_estimate() -> void:
	var m = mental(0)
	var f = int(100.0 * m.fatigue); var s = int(100.0 * m.stress); var l = int(100.0 * m.low_mood)
	var st = "ORANGE" if max(f, max(s, l)) >= 70 else ("YELLOW" if max(f, max(s, l)) >= 45 else "GREEN")
	checkin = {"t": sim.clock(), "fatigue": f, "stress": s, "low_mood": l, "status": st, "raw": "no camera: estimated from your wristband, sleep and habitat sensors"}
	_say("No camera, so I estimated from your wristband, sleep and the habitat sensors: overall %s." % st.to_lower())

# =====================================================================================================
#  the assistant: offline brain (port of SELENE's OfflineBrain) with habitat actions
# =====================================================================================================
func _re(p: String) -> RegEx:
	if not rx.has(p):
		var r = RegEx.new(); r.compile(p); rx[p] = r
	return rx[p]
func _has(t: String, p: String) -> bool: return _re(p).search(t) != null
func _m(t: String, p: String) -> RegExMatch: return _re(p).search(t)

## words from the microphone: "Selene …" is a command; with the dashboard open everything is
func _heard(text: String) -> void:
	var t = text.strip_edges()
	if t == "": return
	var low = t.to_lower()
	var m = _m(low, "(?:hey |ok |okay )?(?:selene|selena|celine|serene)[,.!]?\\s*(.*)")
	if m != null:
		var rest = m.get_string(1).strip_edges()
		if rest == "": open_dash(true); _say("I'm listening."); return
		ask(rest); return
	if g.selene_open or confirm != "": ask(t)

func ask(text: String) -> void:
	if text.strip_edges() == "": return
	_log("you", text)
	state = "thinking"; state_t = 0.4
	last_mood.erase("pending")
	var r = reply(text)
	if r != "": _say(r, last_mood.get("pending", false))
	last_mood.erase("pending")

func _log(who: String, text: String, fb := {}) -> void:
	chat.append({"who": who, "text": text, "fb": fb})
	if chat.size() > 40: chat.pop_front()
	if g.selene_ui: g.selene_ui.dirty = true

func _say(text: String, with_fb := false) -> void:
	_log("selene", text, {"mood": last_mood.get("mood", ""), "a": last_mood.get("a", "")} if with_fb else {})
	state = "speaking"; state_t = clamp(text.length() * 0.06, 1.5, 9.0)
	if tts and g.voice_id != "":
		DisplayServer.tts_stop()
		DisplayServer.tts_speak(text.replace("°C", " degrees").replace("µSv", " micro sieverts").replace("mSv", " milli sieverts").replace("CO₂", "C O 2"), g.voice_id, 70, 1.0, 1.04)

func reply(text: String) -> String:
	var t = text.to_lower().strip_edges()
	var m: RegExMatch
	if confirm == "luna_on":
		confirm = ""
		if _has(t, "^(yes|confirm|do it|switch (it )?on)"):
			g.r3.set_luna_off(false, "the Commander (by voice, confirmed)"); return "LUNA is back on. You switched it on, so it's logged as a human decision."
		return "Okay, LUNA stays off."
	if _has(t, "kill myself|suicid|want to die|end it all|hurt myself|self[- ]?harm|no reason to live"):
		alert("RED", "Self-harm words. In the habitat this pages the flight psychologist.")
		return "I'm really glad you told me. In the habitat, this goes straight to the flight psychologist and the CMO. This is a game and can't reach anyone, so please talk to a real person now: in the US, call or text 988 (Suicide and Crisis Lifeline), or 911 if you're in danger. Outside the US, findahelpline.com lists a free line. Would you like to breathe slowly with me while you reach out?"
	if _has(t, "medical emergency|can'?t breathe|chest pain|unconscious|bleeding|\\bfire\\b|depressuri"):
		alert("RED", "Emergency reported: \"%s\"." % text.substr(0, 80))
		return "Emergency alert raised: in the habitat this pages the Commander and the flight surgeon at once. If this is a real emergency on Earth, call your local emergency number (911 in the US) now."
	# ---- habitat + game commands (a person's word always outranks the AI)
	if _has(t, "\\b(ai|luna) (off|stop)|switch (luna|the ai) off|turn (luna|the ai) off"):
		g.r3.set_luna_off(true, "the Commander (by voice)"); return "LUNA is off. It keeps only safety sensing until a person switches it back on."
	if _has(t, "(switch|turn) (luna|the ai) (back )?on|\\b(ai|luna) on\\b"):
		if not sim.luna.off: return "LUNA is already on."
		confirm = "luna_on"; return "Switching LUNA back on is a human decision. Say \"confirm\" to switch it on, or anything else to keep it off."
	if _has(t, "autopilot (off|stop)|stop (the )?autopilot"): g.r3.set_auto(false, "you said so"); return "Autopilot off. You're in control."
	if _has(t, "autopilot on|start (the )?autopilot"): g.r3.set_auto(true); return "Autopilot on. Move or speak to take over."
	if _has(t, "\\bdemo\\b"): close_dash(); g.r3.start_demo(); return "Starting the guided demo."
	if _has(t, "(open|show) (the )?(dashboard|selene)"): open_dash(true); return "Here's your dashboard."
	if _has(t, "(close|hide) (the )?(dashboard|selene)|^close$"): close_dash(); return "Closed. Say \"Selene\" if you need me."
	if _has(t, "stop sensing|camera off|stop (the )?camera|stop listening|microphone off"): stop_sensing(); return ""
	if _has(t, "(start|turn on|switch on) (face|posture|voice|camera|sensing|microphone)"):
		start_sensing(_has(t, "face|camera|sensing"), _has(t, "posture|camera|sensing"), _has(t, "voice|microphone|sensing")); return ""
	m = _m(t, "(?:take|guide|bring) me to (?:the )?(.+)|where(?:'s| is) (?:the |my )?(.+)")
	if m != null:
		var place = (m.get_string(1) + m.get_string(2)).strip_edges()
		var dest = _place(place)
		if dest.is_empty(): return "I don't know where \"%s\" is. Try: greenhouse, gym, medical, Ops, galley, airlock, life support, storage or your pod." % place
		close_dash(); g.r3._guide_to(dest.cell); return "Follow the gold path to %s." % dest.name
	if _has(t, "what time|what day|which day"): return "It's %s on day %d of 3." % [sim.clock(), sim.day()]
	# ---- logging
	m = _m(t, "(\\d+(?:\\.\\d+)?)\\s*(?:min|minutes?)\\b")
	if m != null and _has(t, "exercis|work ?out|train|run|bike|cycl|lift|resist|weights|treadmill|squat|cardio"):
		var n = float(m.get_string(1)); var resist = _has(t, "resist|weight|strength|squat|lift|ared")
		ex_min[0] += n
		if resist: res_min[0] += n
		var left = max(0.0, EX_GOAL - ex_min[0])
		return "Logged %d minutes of %s exercise. Today: %d minutes%s" % [int(n), "resistive" if resist else "aerobic", int(ex_min[0]), (", %d to go to your 2-hour countermeasure goal." % int(left)) if left > 0 else ". You've hit today's countermeasure goal!"]
	m = _m(t, "(\\d+(?:\\.\\d+)?)\\s*(ml|millilit|l\\b|lit|litre|liter|cups?|glass(?:es)?)")
	if m != null and _has(t, "drank|drink|water|hydrat|glass|cup"):
		var n2 = float(m.get_string(1)); var u = m.get_string(2)
		var ml = n2 * 1000.0 if (u.begins_with("l")) else (n2 * 250.0 if (u.begins_with("cup") or u.begins_with("glass")) else n2)
		water[0] += ml
		return "Logged %d ml. You're at %d of %d ml today." % [int(ml), int(water[0]), int(WATER_GOAL)]
	if _has(t, "joke|make me laugh|funny|cheer me up"): return JOKES[randi() % JOKES.size()]
	if _has(t, "brain ?waves?|eeg|alpha|theta"):
		var e = vit[0].eeg
		return "Your brainwaves look %s: alpha %d%%, beta %d%%, theta %d%%, delta %d%%, gamma %d%% (sleep headband EEG)." % [vit[0].eeg_state, int(e.alpha * 100), int(e.beta * 100), int(e.theta * 100), int(e.delta * 100), int(e.gamma * 100)]
	if _has(t, "blood pressure|\\bbp\\b|blood flow|circulation|perfusion"):
		return "Blood pressure %d/%d mmHg, perfusion index %.1f. In low gravity body fluids shift toward the head, so we watch this closely." % [int(vit[0].sys), int(vit[0].dia), vit[0].pi]
	if _has(t, "stab(le|ility)|balance|steady|dizzy"):
		var sbl = stability(0)
		return "Balance score %d of 100%s" % [vit[0].balance, (", emotional stability %d of 100 over your last mood logs." % sbl) if sbl >= 0 else "."]
	if _has(t, "outside|exterior|surface|solar|dust|weather|\\beva\\b"):
		var x = exterior()
		return "Outside: %d °C, radiation %d µSv/h, solar activity %d of 9, dust %d of 10. %s. %s" % [int(x.temp), int(x.rad), x.solar, x.dust, _cap(", ".join(x.notes)), "EVA conditions look okay." if x.eva_ok else "I'd hold off on EVA."]
	if _has(t, "what have you learned|learned|preferences"): return learned_text(0)
	if _has(t, "check[- ]?in|scan me|assess me|wellbeing check"): start_checkin(); return ""
	if _has(t, "breath|calm me|panic|relax me"): start_breathing(); return "Let's breathe together: in through your nose for 4, hold for 7, out slowly for 8. Four rounds. Follow the circle."
	if _has(t, "that (helped|worked)|helpful|it helped"): rate_last(true); return "Thanks, I'll remember that works for you."
	if _has(t, "(didn'?t|did not|not) help|not helpful"): rate_last(false); return "Got it, I'll try something different next time."
	if _has(t, "lights? (off|out)"): _set_light("off"); return "Lights off."
	if _has(t, "\\b(light|lights|lighting|lamp)\\b"):
		for k in [["warm", "warm"], ["evening", "warm"], ["relax", "warm"], ["cool", "cool"], ["day", "cool"], ["bright", "energise"], ["energi", "energise"], ["dim", "amber"], ["night", "night"], ["auto", "auto"]]:
			if t.find(k[0]) >= 0: return _set_light(k[1])
	m = _m(t, "(?:temperature|temp|degrees)\\D*(\\d{2}(?:\\.\\d)?)|(\\d{2}(?:\\.\\d)?)\\s*degrees")
	if m != null:
		var c = clamp(float(m.get_string(1) + m.get_string(2)), 18.0, 24.0)
		g.r3.pod_temp = int(round(c)); return "Your pod's temperature is set to %d °C. The AC will take a few minutes." % g.r3.pod_temp
	if _has(t, "radiation|dose"):
		var tot = PRIOR_DOSE[0] + rad[0]
		return "Career dose %.1f mSv, %.1f %% of NASA's 600 mSv limit. This mission so far %.2f mSv, today %.3f mSv." % [tot, tot / RAD_CAREER * 100.0, rad[0], rad_today[0]]
	if _has(t, "\\b(crew|everyone|team)\\b"):
		var parts = []
		for i in range(1, 4):
			var nm = sim.crew[i].name.split(" ")[0]
			parts.append(("%s: %s" % [nm, phys(i).status.to_lower()]) if share_board[i] else ("%s keeps their health data private" % nm))
		return "Crew physical status: " + "; ".join(parts) + ". Mental wellbeing is private: ask them, or check in with them."
	if _has(t, "habitat|cabin|oxygen level|\\bco2\\b|\\bair\\b"):
		return "Cabin O₂ %.1f %%, CO₂ %.2f mmHg (limit 3), pressure %.1f kPa, your pod %.1f °C." % [sim.hab.o2, ppco2(), CABIN_KPA, g.r3.pod_air]
	if _has(t, "(call|talk to|video).*(family|home|wife|husband|kids|mom|dad|friend)|family call"):
		sched.append({"what": "Private family video call", "when": "next comm window"}); return "I've booked a private family video call for the next comm window."
	if _has(t, "pause (the )?monitor|stop monitor|privacy"): paused = true; stop_sensing(); return "Monitoring paused. Nothing is analysed until you say \"resume monitoring\"."
	if _has(t, "resume|unpause|start monitor"): paused = false; return "Monitoring resumed."
	if _has(t, "music (off|stop)|stop (the )?music|silence|\\bquiet\\b"): g.r3.music_on = false; return "Music off."
	if _has(t, "\\b(play|music|song)\\b"): g.r3.music_on = true; return "Music on. Lo-fi while you're awake; it stops by itself at bedtime."
	if _has(t, "can'?t sleep|insomnia"): _set_light("night"); g.r3.sleep_sound = "vent"; return "I've dimmed to night light. Try slow breathing; I'll keep things quiet."
	if _has(t, "heart|pulse|hrv|oxygen|spo2|vital|temperature") or _has(t, "how am i|status|summary|doing|health|report"): return summary()
	if _has(t, "\\b(feel|feeling|i'?m|i am|mood)\\b"):
		var mood = ""
		var words = {"happy": "happy|great|good|awesome|excited|amazing|fantastic|joy", "calm": "calm|relaxed|peaceful|content|chill|fine",
			"tired": "tired|exhausted|sleepy|drained|fatigued|worn out", "anxious": "anxious|stressed|worried|nervous|tense|overwhelmed|panick",
			"sad": "sad|down|depressed|unhappy|\\blow\\b|blue|upset", "lonely": "lonely|homesick|alone|isolated|miss (my|home)"}
		for k in ["lonely", "anxious", "tired", "sad", "happy", "calm"]:
			if _has(t, words[k]): mood = k; break
		if _has(t, "\\bnot (so |very |that )?(happy|good|great|fine|calm)"): mood = "sad"
		if mood != "": return mood_reply(mood)
		return "Thanks for telling me. Would a quick check-in help?"
	if _has(t, "help|what can you|commands"): return HELP
	if _has(t, "^(hi|hello|hey|good (morning|evening|afternoon))"): return "Hello, Commander. How are you feeling today?"
	return "I didn't catch a command there. Say \"help\" to hear what I can do."

func _cap(s: String) -> String: return s.substr(0, 1).to_upper() + s.substr(1)

func summary() -> String:
	var v = vit[0]; var p = phys(0); var m = mental(0)
	var s = "Heart rate %d bpm, oxygen %d %%, HRV %d ms. Exercise %d of 120 minutes, water %d ml, sleep %.1f hours. Mental wellbeing %s (%s)." % [int(v.hr), int(v.spo2), int(v.hrv), int(ex_min[0]), int(water[0]), v.sleep, mstatus(m).to_lower(), m.source]
	if not p.flags.is_empty(): s += " Heads up: " + p.flags[0][1]
	return s

func _set_light(v: String) -> String:
	var z = sim.hab.zone_of(sim.player_cell)
	if z == "pod0":
		sim.pod_manual = v; return "Pod light set to %s." % v
	var room = z
	if g.r3.room_set.has(room):
		g.r3.room_change(room, "light", v); return "%s light set to %s. Shared rooms go back to LUNA's schedule when you leave." % [room.capitalize(), v]
	return "Lights here follow the habitat schedule. You can set your own pod's light any time."

const PLACES = {"greenhouse": [Vector2i(21, 3), "the greenhouse"], "garden": [Vector2i(21, 3), "the greenhouse"], "gym": [Vector2i(17, 8), "the exercise bay"],
	"exercise": [Vector2i(17, 8), "the exercise bay"], "medical": [Vector2i(2, 8), "Medical"], "shelter": [Vector2i(2, 8), "the storm shelter"],
	"ops": [Vector2i(10, 7), "Ops"], "console": [Vector2i(10, 7), "the Ops console"], "galley": [Vector2i(15, 2), "the galley"], "kitchen": [Vector2i(15, 2), "the galley"],
	"table": [Vector2i(14, 2), "the wardroom table"], "airlock": [Vector2i(2, 3), "the airlock"], "suitport": [Vector2i(2, 2), "the suitports"],
	"life support": [Vector2i(5, 8), "life support"], "eclss": [Vector2i(5, 8), "life support"], "storage": [Vector2i(8, 8), "storage"],
	"pod": [Vector2i(6, 3), "your pod"], "bed": [Vector2i(6, 3), "your bed"], "kiosk": [Vector2i(7, 4), "the check-in kiosk"],
	"dock": [Vector2i(17, 8), "the Wellness Dock"], "toilet": [Vector2i(5, 2), "hygiene"], "bathroom": [Vector2i(5, 2), "hygiene"], "shower": [Vector2i(5, 2), "hygiene"]}
func _place(p: String) -> Dictionary:
	for k in PLACES:
		if p.find(k) >= 0: return {"cell": PLACES[k][0], "name": PLACES[k][1]}
	return {}

# ---- moods: SELENE learns what helps *you* (Thompson sampling, like SELENE's learning.py)
func _beta(a: float, b: float) -> float:
	var x = 0.0; var y = 0.0
	for k in max(1, int(round(a))): x -= log(max(1e-9, randf()))
	for k in max(1, int(round(b))): y -= log(max(1e-9, randf()))
	return x / (x + y)

func choose(i: int, mood: String) -> String:
	var st = learn.get("%d:%s" % [i, mood], {})
	var best = ""; var bv = -1.0
	for a in CANDS[mood]:
		var ns = st.get(a, [0, 0])
		var v = _beta(1.0 + ns[1], 1.0 + ns[0] - ns[1])
		if v > bv: bv = v; best = a
	return best

func rate(i: int, mood: String, a: String, ok: bool) -> void:
	if mood == "" or a == "": return
	var key = "%d:%s" % [i, mood]
	var st = learn.get(key, {})
	var ns = st.get(a, [0, 0])
	st[a] = [ns[0] + 1, ns[1] + (1 if ok else 0)]
	learn[key] = st
	_save_learn()

func rate_last(ok: bool) -> void:
	rate(0, last_mood.get("mood", ""), last_mood.get("a", ""), ok)

func learned_text(i: int) -> String:
	var parts = []
	for mood in MOODS:
		var st = learn.get("%d:%s" % [i, mood], {})
		if st.is_empty(): continue
		var best = ""; var bv = -1.0
		for a in st:
			var v = (st[a][1] + 1.0) / (st[a][0] + 2.0)
			if v > bv: bv = v; best = a
		parts.append("when %s, %s" % [mood, best.replace("_", " ")])
	if parts.is_empty(): return "I'm still learning. Tap Helpful or Not helpful after my suggestions and I'll adapt to you."
	return "Here's what works for you: " + "; ".join(parts) + "."

func mood_reply(mood: String) -> String:
	moods[0].append(mood)
	var a = choose(0, mood)
	last_mood = {"mood": mood, "a": a, "pending": true}
	var open = {"happy": "Love hearing that!", "calm": "Nice, calm is a great place to be.", "tired": "Thanks for telling me.", "anxious": "I hear you. Let's bring it down a notch.",
		"sad": "I'm sorry it's a heavy day.", "lonely": "That's a really normal feeling this far from home."}[mood]
	var txt = ""
	match a:
		"celebrate": txt = "Want me to note that win in your mission journal?"
		"joke": txt = "Here's one: " + JOKES[randi() % JOKES.size()]
		"music": g.r3.music_on = true; txt = "I've put some lo-fi on in your pod."
		"light_relax": _set_light("warm"); txt = "I've switched to warm light."
		"light_day": _set_light("energise"); txt = "I've switched to bright, blue-rich light: it helps you feel alert."
		"nap_window": sched.append({"what": "25-minute nap in your pod", "when": "next free slot"}); txt = "I've reserved a 25-minute nap in your next free slot."
		"breathing": start_breathing(); txt = "Let's do a short 4-7-8 breathing exercise together. Follow the circle."
		"ocean_relax": _set_light("warm"); txt = "Warm light on. A few slow breaths by the Earth window might help too."
		"family_call": sched.append({"what": "Private family video call", "when": "next comm window"}); txt = "I've booked a private family video call in the next comm window."
		"crew_activity": sched.append({"what": "Crew movie night (suggested)", "when": "this evening"}); txt = "I've suggested a crew movie night this evening."
		"earth_view": txt = "The Earth window in the social hub is live right now. It helps a lot of people."
		"talk": txt = "Do you want to tell me what's on your mind?"
		_: txt = "I'm here if you need anything."
	return "%s %s Was that helpful?" % [open, txt]

func start_breathing() -> void:
	breath = {"t": 0.0}
	open_dash(false)

## 4-7-8 breathing: returns [phase text, circle scale 0..1]
func breath_phase() -> Array:
	if breath.is_empty(): return ["", 0.0]
	var t = fmod(breath.t, 19.0)
	if t < 4.0: return ["Breathe in · %d" % int(4.0 - t + 1.0), t / 4.0]
	if t < 11.0: return ["Hold · %d" % int(11.0 - t + 1.0), 1.0]
	return ["Breathe out · %d" % int(19.0 - t + 1.0), 1.0 - (t - 11.0) / 8.0]

# ---- dashboard open/close (the game owns the overlay and the VR panel)
func open_dash(listen: bool) -> void:
	if not g.selene_open: g.toggle_selene()
	if listen and want.voice: state = "listening"
func close_dash() -> void:
	if g.selene_open: g.toggle_selene()

func _load_learn() -> void:
	if FileAccess.file_exists("user://selene_learn.json"):
		var v = JSON.parse_string(FileAccess.get_file_as_string("user://selene_learn.json"))
		if v is Dictionary: learn = v
func _save_learn() -> void:
	var f = FileAccess.open("user://selene_learn.json", FileAccess.WRITE)
	if f: f.store_string(JSON.stringify(learn))

# =====================================================================================================
#  self-test (--seltest): every command path, the dashboard for each person, the wall, VR posture
# =====================================================================================================
func selftest() -> void:
	g._start_desktop()
	while g.state != "play": await get_tree().process_frame
	var bad = 0
	var tests = ["how am I doing?", "start my check-in", "I feel anxious", "I'm feeling tired", "i feel so lonely", "I feel happy", "I'm calm",
		"I'm not so good", "that helped", "breathing exercise", "my brainwaves", "blood pressure", "balance", "outside conditions", "radiation dose", "crew status",
		"habitat air", "lights warm", "lights off", "temperature 21", "set it to 30 degrees", "music off", "play some music", "take me to the greenhouse",
		"where is medical", "take me to narnia", "what time is it", "I exercised 30 minutes on the treadmill", "drank 2 cups of water", "I drank 500 ml",
		"tell me a joke", "what have you learned?", "call my family", "pause monitoring", "resume monitoring", "autopilot on", "autopilot off",
		"AI off", "switch LUNA back on", "confirm", "can't sleep", "help", "hello", "blorf zzz", "I have chest pain", "I want to hurt myself"]
	for t in tests:
		var r = reply(t)
		if not t in ["start my check-in", "breathing exercise"] and r == "": bad += 1; print("SELTEST EMPTY reply for: ", t)
		print("SELTEST > %s\n        %s" % [t, r.substr(0, 150)])
	if sim.luna.off: bad += 1; print("SELTEST FAIL: LUNA still off after confirm")
	if g.r3.auto: g.r3.set_auto(false)
	g.r3.close_card()
	_heard("Selene, what time is it"); _heard("random chatter nobody asked for")
	print("SELTEST wake word chat=%d last=%s" % [chat.size(), chat[-1].text.substr(0, 60)])
	g.toggle_selene()
	for k in 30: await get_tree().process_frame
	for who in 4:
		g.selene_ui.who = who; g.selene_ui.dirty = true
		for k in 4: await get_tree().process_frame
		print("SELTEST dash who=%d cards=%d" % [who, g.selene_ui.cards.get_child_count()])
		if g.selene_ui.cards.get_child_count() == 0: bad += 1
	g.selene_ui.who = 0
	start_breathing()
	for k in 20: await get_tree().process_frame
	print("SELTEST breath ", breath_phase())
	breath = {}
	g.toggle_selene()
	# the Ops wall
	var w = g.ui.get("selene_wall")
	if w == null: bad += 1; print("SELTEST FAIL: no Ops wall")
	else: w.refresh(true); print("SELTEST wall children=%d" % w.body.get_child_count())
	# headset posture: a person who sits tall, then slumps with head down
	var vh = vr_head
	vh.base = 1.62; want.posture = true
	g.player.vr = true
	var cam: Node3D = g.player.cam
	var y0 = cam.position.y
	cam.position.y = 1.62 - 0.2; cam.rotation.x = -0.8
	for k in 400: _sense(0.05)
	print("SELTEST vr slumped scores=%s" % [scores])
	if scores.get("fatigue", 0.0) < 0.4: bad += 1; print("SELTEST FAIL: slump not seen")
	cam.position.y = y0; cam.rotation.x = 0.0
	g.player.vr = false; want.posture = false; scores = {}; g.selene_live = false; g.selene_data = {}
	# the VR version of the dashboard: a big floating panel you point at
	g.player.vr = true
	if g.selene_ui: g.selene_ui.queue_free()
	g.selene_ui = null
	if g.selene_layer: g.selene_layer.queue_free(); g.selene_layer = null
	g.selene_open = false
	g.toggle_selene()
	for k in 10: await get_tree().process_frame
	var vr_ok = g.selene_scr != null and g.selene_scr.visible and g.selene_scr.ui == g.selene_ui and g.selene_ui.input == null
	print("SELTEST vr panel ok=%s at %s" % [vr_ok, g.selene_scr.global_position if g.selene_scr else "-"])
	if not vr_ok: bad += 1
	g.toggle_selene()
	if g.selene_scr.visible: bad += 1; print("SELTEST FAIL: VR panel did not close")
	g.player.vr = false
	# a full day of accounting
	for k in 600: tick(0.1, 1.0)
	print("SELTEST day: ex %s water %s rad %s" % [ex_min, water, rad])
	print("SELTEST phys you=%s" % phys(0))
	print("SELTEST %s (%d problems)" % ["OK" if bad == 0 else "FAIL", bad])
	get_tree().quit()

## screenshots for checking the layout (--selshot=dir)
func shots(dir: String) -> void:
	DirAccess.make_dir_recursive_absolute(dir)
	g._start_desktop()
	while g.state != "play": await get_tree().process_frame
	for k in 60: await get_tree().process_frame
	reply("I feel anxious"); ask("how am I doing?"); ask("my brainwaves")
	g.toggle_selene()
	for k in 40: await get_tree().process_frame
	get_viewport().get_texture().get_image().save_png(dir + "/selene_dash.png")
	start_breathing()
	for k in 90: await get_tree().process_frame
	get_viewport().get_texture().get_image().save_png(dir + "/selene_breath.png")
	breath = {}
	g.selene_ui.who = 2; g.selene_ui.dirty = true
	for k in 20: await get_tree().process_frame
	get_viewport().get_texture().get_image().save_png(dir + "/selene_private.png")
	g.selene_ui.who = 1; g.selene_ui.dirty = true
	for k in 20: await get_tree().process_frame
	get_viewport().get_texture().get_image().save_png(dir + "/selene_crew.png")
	g.toggle_selene()
	g.hud.visible = false
	for spot in [["selene_wall", Vector3(15.15, 0, 8.3), PI, 0.12], ["forecast", Vector3(14.3, 0, 10.6), -PI / 2.0, 0.42]]:
		g.player.global_position = spot[1]; g.player.rotation.y = spot[2]; g.player.pitch = spot[3]; g.player.cam.rotation.x = spot[3]
		for k in 50: await get_tree().process_frame
		get_viewport().get_texture().get_image().save_png(dir + "/%s.png" % spot[0])
	get_tree().quit()
