class_name SeleneUI
extends RefCounted
## SELENE's screens: the huge personal dashboard (full screen on PC/browser, a big floating panel in VR)
## and the crew-health wall in Ops (shared room: only people who chose to appear, never mental data).

const RED = Color("#f87171")
const ORANGE = Color("#fb923c")
const YEL = Color("#fbbf24")
const GRN = Color("#34d399")
const CMDC = Color("#7dd3fc")             # command hints: what you can do hands-free
const SEV = {"RED": RED, "ORANGE": ORANGE, "YELLOW": YEL, "GREEN": GRN}

static func sev_col(s: String) -> Color: return SEV.get(s, UI.MUT)

static func tile(title: String, value: String, unit: String, col: Color, sub := "", w := 200.0) -> PanelContainer:
	var c = UI.card(0.6)
	c[0].custom_minimum_size = Vector2(w, 0)
	c[1].add_child(UI.lbl(title, 17, UI.MUT))
	var h = UI.hbox(6)
	h.add_child(UI.lbl(value, 40, col)); h.add_child(UI.lbl(unit, 18, UI.MUT))
	c[1].add_child(h)
	if sub != "": c[1].add_child(UI.lbl(sub, 14, UI.MUT, true))
	return c[0]

static func meter(name: String, v: float, col: Color, txt: String, w := 210.0, marker := -1.0, fs := 17, lw := 120.0, bh := 14.0) -> HBoxContainer:
	var h = UI.hbox(8)
	var l = UI.lbl(name, fs, UI.MUT); l.custom_minimum_size.x = lw; h.add_child(l)
	var b = UI.bar(clamp(v, 0.0, 1.0), col, w, bh, marker); b.size_flags_vertical = Control.SIZE_SHRINK_CENTER; h.add_child(b)
	h.add_child(UI.lbl(txt, fs, UI.INK))
	return h

static func card(title: String, hue := 0.6) -> Array:
	var c = UI.card(hue)
	c[1].add_child(UI.lbl(title, 21, UI.ACC))
	return c

# ======================================================================================================
#  the moon logo: crescent + orbiting spark + heartbeat, showing SELENE's state
# ======================================================================================================
class Logo extends Control:
	## the SELENE app's logo, ported 1:1: a Moon made of ~760 particles on a sphere (Fibonacci spiral), lit from the
	## upper left so it reads as a waxing crescent. Motion shows state: idle = slow drift, listening = ripples
	## travel up, thinking = bands swirl, speaking = the surface pulses with the voice.
	const PN = 760
	const LCOL = {"idle": Color8(214, 222, 232), "listening": Color8(60, 200, 240), "thinking": Color8(90, 169, 214), "speaking": Color8(236, 240, 245)}
	var s                                   # Selene
	var t = 0.0
	var pts = []
	var lcol = Color8(214, 222, 232)
	var spin = 0.0
	var energy = 0.0
	func _init(sel, px := 86.0) -> void:
		s = sel; custom_minimum_size = Vector2(px, px)
		var ga = PI * (3.0 - sqrt(5.0))
		for i in PN:
			var y = 1.0 - 2.0 * (i + 0.5) / PN; var r = sqrt(1.0 - y * y); var a = i * ga
			pts.append([cos(a) * r, y, sin(a) * r, randf() * TAU])
	func _process(dt: float) -> void:
		dt = min(dt, 0.05); t += dt
		var st = str(s.state)
		lcol = lcol.lerp(LCOL.get(st, LCOL.idle), 0.08)
		energy += ((0.0 if st == "idle" else 1.0) - energy) * 0.06
		spin += dt * (1.6 if st == "thinking" else 0.25)
		if is_visible_in_tree(): queue_redraw()
	func _draw() -> void:
		var st = str(s.state)
		var W = min(size.x, size.y); var c = size * 0.5; var R = W * 0.41
		var cs = cos(spin); var sn = sin(spin); var ct = cos(0.35); var stt = sin(0.35)
		var L = Vector3(-0.80, 0.32, 0.50)            # sunlight from the upper left, slightly toward the viewer
		var voice = 0.5 + 0.5 * sin(t * 11.0) * sin(t * 3.7) if st == "speaking" else 0.0
		for p in pts:
			var x0 = p[0]; var y0 = p[1]; var z0 = p[2]; var ph = p[3]
			var x = x0 * cs + z0 * sn; var z = -x0 * sn + z0 * cs
			var y = y0 * ct - z * stt; z = y0 * stt + z * ct
			var d = 1.0 + 0.015 * sin(t * 0.8 + ph)
			if st == "listening": d += 0.07 * energy * sin(y0 * 9.0 - t * 6.0)
			elif st == "thinking": d += 0.05 * energy * sin(atan2(z0, x0) * 4.0 + t * 5.0)
			elif st == "speaking": d += 0.12 * energy * voice * (0.6 + 0.4 * sin(ph * 3.0 + t * 9.0))
			if z < -0.15: continue
			var lit = clamp((x * L.x + y * L.y + z * L.z) * 1.4 + 0.1, 0.0, 1.0)
			var a = 0.07 + 0.93 * pow(lit, 0.8)
			var sz = (W / 80.0) * (0.6 + 0.4 * (z + 0.15) / 1.15) * (0.75 + 0.45 * lit) * (1.5 if W < 200.0 else 1.0)   # small logos: the app's canvas is downscaled, so its dots blend; ours draw 1:1
			draw_rect(Rect2(c.x + x * R * d - sz * 0.5, c.y - y * R * d - sz * 0.5, sz, sz), Color(lcol, a))

# ======================================================================================================
#  live heart-rate trace
# ======================================================================================================
class Trace extends Control:
	var s; var i = 0
	func _init(sel, idx: int) -> void: s = sel; i = idx; custom_minimum_size = Vector2(300, 70)
	func _process(_dt: float) -> void: queue_redraw()
	func _draw() -> void:
		var h: Array = s.hr_hist[i]
		draw_rect(Rect2(Vector2.ZERO, size), Color(0.04, 0.07, 0.16))
		if h.size() < 2: return
		var lo = 40.0; var hi = 160.0
		var pts = PackedVector2Array()
		for k in h.size():
			pts.append(Vector2(size.x * k / 89.0, size.y - (float(h[k]) - lo) / (hi - lo) * size.y))
		draw_polyline(pts, Color("#f87171"), 2.0)

# ======================================================================================================
#  4-7-8 breathing circle
# ======================================================================================================
class Breath extends Control:
	var s
	func _init(sel) -> void: s = sel; custom_minimum_size = Vector2(260, 260); mouse_filter = MOUSE_FILTER_IGNORE
	func _process(_dt: float) -> void: visible = not s.breath.is_empty(); queue_redraw()
	func _draw() -> void:
		if s.breath.is_empty(): return
		var ph = s.breath_phase()
		var c = size * 0.5
		draw_circle(c, 120.0, Color(0.05, 0.1, 0.25, 0.92))
		draw_circle(c, 40.0 + 70.0 * float(ph[1]), Color(0.22, 0.74, 0.97, 0.75))
		var f = ThemeDB.fallback_font
		draw_string(f, c + Vector2(-110, 6), str(ph[0]), HORIZONTAL_ALIGNMENT_CENTER, 220, 24, Color.WHITE)

# ======================================================================================================
#  the HUGE personal dashboard (1920 x 1080 design size)
# ======================================================================================================
class Dash extends Control:
	var g; var s
	var vr = false
	var dirty = true
	var cards: Control
	var chat_box: VBoxContainer
	var input: LineEdit
	var heard: Label
	var state_l: Label
	var t = 0.0
	var who = 0                                    # whose data: 0 = you; crewmates only if they chose to share
	var tabs = []
	var voice_btn: Button

	func _init(game, in_vr := false) -> void:
		g = game; s = game.selene; vr = in_vr
		custom_minimum_size = Vector2(1920, 1080); size = Vector2(1920, 1080)
		theme = UI.get_theme()
		var bg = ColorRect.new(); bg.color = Color(0.03, 0.05, 0.13, 1.0); bg.size = Vector2(1920, 1080); bg.mouse_filter = MOUSE_FILTER_STOP; add_child(bg)
		var root = HBoxContainer.new(); root.position = Vector2(20, 14); root.size = Vector2(1880, 1052); root.add_theme_constant_override("separation", 16); add_child(root)
		var left = VBoxContainer.new(); left.custom_minimum_size = Vector2(1370, 0); left.add_theme_constant_override("separation", 10); root.add_child(left)
		# header
		var head = UI.hbox(14); left.add_child(head)
		head.add_child(Logo.new(s, 96.0))
		var tv = UI.vbox(2); head.add_child(tv)
		tv.add_child(UI.lbl("SELENE · your health companion", 34, UI.ACC))
		state_l = UI.lbl("", 18, UI.MUT); tv.add_child(state_l)
		var sp = Control.new(); sp.size_flags_horizontal = SIZE_EXPAND_FILL; head.add_child(sp)
		for k in 4:
			var nm = "You" if k == 0 else g.sim.crew[k].name.split(" ")[0]
			var kk = k
			var tb = UI.btn(nm, func(): who = kk; dirty = true, 0.55, 20); tabs.append(tb); head.add_child(tb)
		head.add_child(UI.btn("Close ✕", func(): g.toggle_selene(), 0.0, 20))
		cards = VBoxContainer.new(); cards.add_theme_constant_override("separation", 10); left.add_child(cards)
		# chat column
		var right = VBoxContainer.new(); right.custom_minimum_size = Vector2(494, 0); right.add_theme_constant_override("separation", 8); root.add_child(right)
		var cc = UI.card(0.62); cc[0].size_flags_vertical = SIZE_EXPAND_FILL; right.add_child(cc[0])
		cc[1].add_child(UI.lbl("Talk to SELENE", 24, UI.ACC))
		heard = UI.lbl("", 16, UI.WARN, true); cc[1].add_child(heard)
		var sc = ScrollContainer.new(); sc.custom_minimum_size = Vector2(460, 470); sc.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED; cc[1].add_child(sc)
		chat_box = VBoxContainer.new(); chat_box.custom_minimum_size = Vector2(440, 0); chat_box.add_theme_constant_override("separation", 6); sc.add_child(chat_box)
		var qp = GridContainer.new(); qp.columns = 2; qp.add_theme_constant_override("h_separation", 6); qp.add_theme_constant_override("v_separation", 6); cc[1].add_child(qp)
		for q in ["How am I doing?", "Start my check-in", "I feel anxious", "I feel tired", "I feel lonely", "I feel happy", "Breathing exercise", "My brainwaves", "Outside conditions", "Crew status", "Radiation dose", "What have you learned?", "Tell me a joke", "Help"]:
			var qq = q
			qp.add_child(UI.btn(q, func(): s.ask(qq), 0.6, 17))
		if not vr:
			var ih = UI.hbox(6); cc[1].add_child(ih)
			input = LineEdit.new(); input.placeholder_text = "Type to SELENE…"; input.custom_minimum_size = Vector2(340, 40); input.add_theme_font_size_override("font_size", 18)
			input.text_submitted.connect(func(tx): s.ask(tx); input.text = "")
			ih.add_child(input)
			ih.add_child(UI.btn("Send", func(): s.ask(input.text); input.text = "", 0.55, 18))
		voice_btn = UI.btn("", func():
			if s.want.voice: s.stop_voice()
			else: s.start_sensing(false, false, true), 0.4, 20)
		voice_btn.clip_text = true; voice_btn.custom_minimum_size = Vector2(440, 46)
		right.add_child(voice_btn)
		var vrow = UI.hbox(6); right.add_child(vrow)
		vrow.add_child(UI.btn("Spoken replies: " + ("on" if s.tts else "off"), func(): s.tts = not s.tts; dirty = true, 0.75, 17))
		vrow.add_child(UI.btn("Delete my data", func(): s.learn = {}; s._save_learn(); s.chat = []; s.alerts = []; s._say("Deleted: what I learned about you, our chat and the alerts."), 0.0, 17))
		var br = Breath.new(s); br.position = Vector2(640, 380); add_child(br)
		_rebuild()

	func _process(dt: float) -> void:
		t += dt
		if not visible: return
		state_l.text = {"idle": "ready · say \"Selene\" or tap a question", "listening": "listening…", "thinking": "thinking…", "speaking": "speaking"}.get(s.state, "") + \
			("   ·   ● camera on" if s._js_on("cam") else "") + ("   ·   ● microphone on" if s._js_on("mic") else "") + ("   ·   monitoring paused" if s.paused else "")
		heard.text = ("\"%s…\"" % s.heard) if s.heard != "" else ""
		if t > 1.0 or dirty:
			t = 0.0
			if dirty: _chat()
			dirty = false
			_rebuild()          # buttons fire on press (UI.btn), so a refresh can never swallow a click

	func _chat() -> void:
		UI.clear(chat_box)
		for m in s.chat.slice(-9):
			var col = UI.INK if m.who == "selene" else (UI.ACC if m.who == "you" else RED)
			var pre = "SELENE: " if m.who == "selene" else ("You: " if m.who == "you" else "")
			chat_box.add_child(UI.lbl(pre + m.text, 17, col, true))
			if m.get("fb", {}).get("a", "") != "":
				var fb = m.fb; var h = UI.hbox(6)
				h.add_child(UI.btn("Helpful", func(): s.rate(0, fb.mood, fb.a, true); fb["a"] = ""; s._log("selene", "Thanks, I'll remember that works for you."), 0.4, 15))
				h.add_child(UI.btn("Not helpful", func(): s.rate(0, fb.mood, fb.a, false); fb["a"] = ""; s._log("selene", "Got it, I'll try something different next time."), 0.0, 15))
				chat_box.add_child(h)

	func _rebuild() -> void:
		if voice_btn: voice_btn.text = ("Mic on: say \"Selene, …\" (tap to stop)" if s._js_on("sr") else (("Turn on voice commands" if s.web else "Voice commands: browser version") if not s.want.voice else "Mic: " + _st("sr").replace(" (see chat)", "")))
		for k in tabs.size(): tabs[k].modulate = Color(1, 1, 1) if k == who else Color(0.55, 0.6, 0.7)
		UI.clear(cards)
		var i = who
		if i != 0 and not s.share_board[i]:
			cards.add_child(UI.lbl("%s keeps their health data private. SELENE only shows a crewmate's data when they choose to share it. Ask how they are, or check in with them in person (I)." % g.sim.crew[i].name, 24, UI.MUT, true))
			var p0 = s.phys(i)
			cards.add_child(UI.lbl("Physical status they agreed to show: %s" % p0.status, 26, SeleneUI.sev_col(p0.status)))
			return
		var v = s.vit[i]; var p = s.phys(i)
		# ---- row 1: vitals
		var r1 = UI.hbox(10); cards.add_child(r1)
		var hrc = UI.card(0.6); hrc[0].custom_minimum_size = Vector2(390, 0); r1.add_child(hrc[0])
		var hh = UI.hbox(10); hrc[1].add_child(hh)
		var hv = UI.vbox(0); hh.add_child(hv)
		hv.add_child(UI.lbl("Heart rate", 17, UI.MUT))
		var hb = UI.hbox(6); hb.add_child(UI.lbl("%d" % int(v.hr), 46, RED)); hb.add_child(UI.lbl("bpm", 18, UI.MUT)); hv.add_child(hb)
		hh.add_child(Trace.new(s, i))
		var ex = v.act in Selene.EXERCISE
		r1.add_child(SeleneUI.tile("Blood oxygen", "%d" % int(v.spo2), "%", GRN if v.spo2 >= 95 else RED, "SpO₂ · wristband", 165))
		r1.add_child(SeleneUI.tile("HRV", "%d" % int(v.hrv), "ms", GRN if v.hrv >= 35 or ex else ORANGE, "recovery · exercise lowers it" if ex else "higher = better recovered", 175))
		r1.add_child(SeleneUI.tile("Body temp", "%.1f" % v.temp, "°C", UI.INK, "skin patch", 165))
		r1.add_child(SeleneUI.tile("Blood pressure", "%d/%d" % [int(v.sys), int(v.dia)], "mmHg", UI.INK, "fluid shift watch", 215))
		var stt = SeleneUI.tile("Status", p.status, "", SeleneUI.sev_col(p.status), "physical", 150); stt.size_flags_horizontal = SIZE_EXPAND_FILL; r1.add_child(stt)
		# ---- row 2: countermeasures · brainwaves · mind
		var r2 = UI.hbox(10); cards.add_child(r2)
		var cm = SeleneUI.card("Countermeasures today"); cm[0].custom_minimum_size = Vector2(430, 0); r2.add_child(cm[0])
		cm[1].add_child(SeleneUI.meter("Exercise", s.ex_min[i] / s.EX_GOAL, GRN, "%d / 120 min" % int(s.ex_min[i])))
		cm[1].add_child(SeleneUI.meter("Resistive", s.res_min[i] / s.RES_GOAL, GRN, "%d / 60 min" % int(s.res_min[i])))
		cm[1].add_child(SeleneUI.meter("Water", s.water[i] / s.WATER_GOAL, UI.ACC, "%d / 2500 ml" % int(s.water[i])))
		cm[1].add_child(SeleneUI.meter("Sleep", v.sleep / 8.0, Color("#a970ff"), ("%.1f h so far" if v.asleep else "%.1f / 8 h") % v.sleep))
		var dose = s.PRIOR_DOSE[i] + s.rad[i]
		cm[1].add_child(SeleneUI.meter("Radiation", dose / s.RAD_CAREER, YEL, "%.1f / 600 mSv" % dose))
		cm[1].add_child(UI.lbl("Today %.3f mSv · balance %d/100" % [s.rad_today[i], v.balance], 15, UI.MUT))
		var bw = SeleneUI.card("Brainwaves · sleep headband EEG"); bw[0].custom_minimum_size = Vector2(400, 0); r2.add_child(bw[0])
		var e = v.eeg
		for k in [["Delta", "delta", Color("#6366f1")], ["Theta", "theta", Color("#a970ff")], ["Alpha", "alpha", GRN], ["Beta", "beta", YEL], ["Gamma", "gamma", RED]]:
			bw[1].add_child(SeleneUI.meter(k[0], e[k[1]] / 0.6, k[2], "%d %%" % int(e[k[1]] * 100.0), 190))
		bw[1].add_child(UI.lbl("Reads as: " + v.eeg_state, 19, UI.INK))
		var mi = SeleneUI.card("Mind · fatigue, stress, mood"); mi[0].custom_minimum_size = Vector2(450, 0); r2.add_child(mi[0])
		if i == 0:
			var m = s.mental(0)
			mi[1].add_child(SeleneUI.meter("Fatigue", m.fatigue, YEL, "%d" % int(m.fatigue * 100), 200, 0.45))
			mi[1].add_child(SeleneUI.meter("Stress", m.stress, ORANGE, "%d" % int(m.stress * 100), 200, 0.45))
			mi[1].add_child(SeleneUI.meter("Low mood", m.low_mood, Color("#4f8ef7"), "%d" % int(m.low_mood * 100), 200, 0.45))
			var ms = Selene.mstatus(m)
			mi[1].add_child(UI.lbl("Overall %s · from %s" % [ms, m.source], 16, SeleneUI.sev_col(ms), true))
			if not s.checkin.is_empty(): mi[1].add_child(UI.lbl("Last check-in %s: fatigue %d · stress %d · low mood %d" % [s.checkin.t, s.checkin.fatigue, s.checkin.stress, s.checkin.low_mood], 15, UI.MUT, true))
			var sb = s.stability(0)
			if sb >= 0: mi[1].add_child(UI.lbl("Emotional stability %d/100 (last mood logs)" % sb, 15, UI.MUT))
			if s.checkin_until > 0.0: mi[1].add_child(UI.lbl("Check-in running · %d s" % int(s.checkin_until), 20, UI.ACC))
		else:
			mi[1].add_child(UI.lbl("Mental wellbeing stays private, even when someone shares their physical data.", 18, UI.MUT, true))
		# ---- row 3: the big three senses (yours only)
		if i == 0: cards.add_child(_senses())
		# ---- row 4: habitat · outside · alerts + schedule
		var r4 = UI.hbox(10); cards.add_child(r4)
		var hc = SeleneUI.card("Habitat"); hc[0].custom_minimum_size = Vector2(330, 0); r4.add_child(hc[0])
		var co2 = s.ppco2()
		hc[1].add_child(SeleneUI.meter("CO₂", co2 / 4.0, RED if co2 > s.CO2_LIMIT else (YEL if co2 > s.CO2_WATCH else GRN), "%.2f mmHg" % co2, 110, s.CO2_LIMIT / 4.0))
		hc[1].add_child(UI.lbl("O₂ %.1f %% · %.1f kPa · pod %.1f °C" % [g.sim.hab.o2, s.CABIN_KPA, g.r3.pod_air], 16, UI.INK))
		hc[1].add_child(UI.lbl("Light here: %s" % str(g.r3.mode), 16, UI.MUT))
		var x = s.exterior()
		var oc = SeleneUI.card("Outside · south pole"); oc[0].custom_minimum_size = Vector2(390, 0); r4.add_child(oc[0])
		oc[1].add_child(UI.lbl("%d °C · radiation %d µSv/h" % [int(x.temp), int(x.rad)], 18, UI.INK))
		oc[1].add_child(UI.lbl("Solar activity %d/9 · dust %d/10 · sun %.1f°" % [x.solar, x.dust, x.elev], 16, UI.INK))
		oc[1].add_child(UI.lbl("EVA: GO" if x.eva_ok else "EVA: HOLD", 22, GRN if x.eva_ok else RED))
		oc[1].add_child(UI.lbl(s._cap(", ".join(x.notes)), 15, UI.MUT, true))
		var ac = SeleneUI.card("Alerts + schedule"); ac[0].custom_minimum_size = Vector2(540, 0); r4.add_child(ac[0])
		var any = false
		for a in s.alerts.slice(0, 3):
			ac[1].add_child(UI.lbl("%s %s · %s" % [a.t, a.sev, a.text], 15, SeleneUI.sev_col(a.sev), true)); any = true
		for f in p.flags.slice(0, 2):
			ac[1].add_child(UI.lbl("now · " + f[1], 15, SeleneUI.sev_col(f[0]), true)); any = true
		for sc in s.sched.slice(-3):
			ac[1].add_child(UI.lbl("Scheduled: %s · %s" % [sc.what, sc.when], 15, UI.ACC, true)); any = true
		if not any: ac[1].add_child(UI.lbl("No alerts. Nothing scheduled yet.", 16, UI.MUT))

	func _senses() -> PanelContainer:
		if not s.ask_consent.is_empty():
			var k = SeleneUI.card("SELENE would like to use your camera and microphone", 0.12)
			for line in Selene.CONSENT: k[1].add_child(UI.lbl(line, 18, UI.INK, true))
			var kb = UI.hbox(10); k[1].add_child(kb)
			kb.add_child(UI.btn("Yes, switch them on", func(): s.give_consent(true), 0.4, 20))
			kb.add_child(UI.btn("Not now", func(): s.give_consent(false), 0.62, 20))
			return k[0]
		var c = SeleneUI.card("The big three · face, posture, voice (yours only, nothing recorded)", 0.55)
		var r = UI.hbox(14); c[1].add_child(r)
		var js = s.js
		var f = js.get("face", null); var po = js.get("posture", null); var vo = js.get("voice", null)
		# face
		var fc = UI.vbox(4); fc.custom_minimum_size.x = 400; r.add_child(fc)
		fc.add_child(UI.lbl("Face · " + _st("cam") + ("" if not g.player.vr else " (not in a headset)"), 18, UI.INK, true))
		if f is Dictionary:
			fc.add_child(SeleneUI.meter("Smile", float(f.smile), GRN, "%d" % int(f.smile * 100), 150))
			fc.add_child(SeleneUI.meter("Brow tension", float(f.brow), ORANGE, "%d" % int(f.brow * 100), 150))
			fc.add_child(SeleneUI.meter("Eyes closed", float(f.eyes_closed) * 3.0, YEL, "%d %%" % int(f.eyes_closed * 100), 150))
			fc.add_child(UI.lbl("Blinks %d/min (typical 15-20) · yawns %d" % [int(f.blink_rate), int(f.yawns)], 15, UI.MUT))
		else: fc.add_child(UI.lbl("Expressions, blinks, eye closure, yawns", 15, UI.MUT, true))
		fc.add_child(UI.lbl("Commands: nod = yes · shake your head = no", 15, CMDC))
		# posture
		var pc = UI.vbox(4); pc.custom_minimum_size.x = 400; r.add_child(pc)
		if g.player.vr:
			var vh = s.vr_head
			pc.add_child(UI.lbl("Posture · headset " + ("on" if s.want.posture else "off"), 18, UI.INK))
			if s.want.posture:
				pc.add_child(SeleneUI.meter("Slumped", vh.slump, YEL, "%d" % int(vh.slump * 100), 150))
				pc.add_child(SeleneUI.meter("Head down", vh.down, YEL, "%d" % int(vh.down * 100), 150))
				pc.add_child(SeleneUI.meter("Fidgeting", vh.fidget, ORANGE, "%d" % int(vh.fidget * 100), 150))
				if vh.base < 0.0: pc.add_child(UI.lbl("Stand or sit as you normally do: learning your height (20 s)", 15, UI.ACC, true))
			pc.add_child(UI.lbl("Commands (no camera needed): nod / shake your head · a controller above your head = call SELENE · both = STOP", 15, CMDC, true))
		else:
			pc.add_child(UI.lbl("Posture · " + _st("cam"), 18, UI.INK, true))
			if po is Dictionary and po.get("present", false):
				pc.add_child(SeleneUI.meter("Upright", clamp(float(po.upright) / 1.6, 0.0, 1.0), GRN, "%.2f" % float(po.upright), 150))
				pc.add_child(SeleneUI.meter("Fidgeting", float(po.fidget) * 2.5, ORANGE, "%d" % int(po.fidget * 250), 150))
				pc.add_child(SeleneUI.meter("Shoulder tilt", float(po.tilt) * 3.0, YEL, "%d" % int(po.tilt * 300), 150))
			else: pc.add_child(UI.lbl("Slumped or upright, fidgeting (shoulders + head in view)", 15, UI.MUT, true))
			pc.add_child(UI.lbl("Commands: raise a hand = call SELENE · both hands up = STOP", 15, CMDC, true))
		# voice
		var vc = UI.vbox(4); vc.custom_minimum_size.x = 400; r.add_child(vc)
		vc.add_child(UI.lbl("Voice · mic " + _st("mic") + " · commands " + _st("sr"), 18, UI.INK, true))
		if vo is Dictionary:
			vc.add_child(SeleneUI.meter("Loudness", float(vo.level) * 12.0, UI.ACC, "", 150))
			vc.add_child(UI.lbl("Pitch %d Hz · variation %d %% · talking %d %%" % [int(vo.pitch), int(vo.pitch_var * 100), int(vo.speech * 100)], 15, UI.MUT))
		vc.add_child(UI.lbl("Commands: say \"Selene, …\" (wait until SELENE finishes talking)", 15, CMDC, true))
		if Time.get_ticks_msec() - s.gest_t < 60000 and s.last_gesture != "":
			var names = {"nod": "nod", "shake": "head shake", "hand": "hand raised", "hands_up": "both hands up"}
			c[1].add_child(UI.lbl("Last hands-free command: %s · %s" % [names.get(s.last_gesture, s.last_gesture), s.gest_note], 17, CMDC))
		var b = UI.hbox(8); c[1].add_child(b)
		var on = s.want.face or s.want.posture or s.want.voice
		if not on:
			b.add_child(UI.btn("Switch on face + posture + voice", func(): s.start_sensing(true, true, true), 0.4, 18))
			b.add_child(UI.btn("Voice only", func(): s.start_sensing(false, false, true), 0.55, 18))
			if g.player.vr: b.add_child(UI.btn("Headset posture only", func(): s.start_sensing(false, true, false), 0.55, 18))
		else:
			b.add_child(UI.btn("Stop all sensing", func(): s.stop_sensing(), 0.0, 18))
		b.add_child(UI.btn("30 s check-in", func(): s.start_checkin(), 0.6, 18))
		b.add_child(UI.btn("Hands-free commands: " + ("on" if s.gest_on else "off"), func(): s.set_gestures(not s.gest_on), 0.55 if s.gest_on else 0.62, 18))
		var sc = s.scores
		if not sc.is_empty():
			b.add_child(UI.lbl("Live read: %s%s" % [sc.mood, " (learning your baseline…)" if sc.calibrating else ""], 18, UI.ACC))
		return c[0]

	func _st(k: String) -> String:
		var v = str(s.js.get(k, "off"))
		if not s.web and k in ["cam", "mic", "sr"]: return "via the SELENE app" if g.selene_live else "browser only"
		return {"on": "on", "off": "off", "starting": "starting…", "error": "couldn't start (see chat)", "unsupported": "not in this browser"}.get(v, v)

# ======================================================================================================
#  Ops crew-health wall (shared room: only crew who chose to appear; never mental data; hard limit 1)
# ======================================================================================================
class Wall extends Screens.SUI:
	func _init(game) -> void:
		super._init(game, "SELENE · crew health", "only crew who chose to appear · physical data only, never mental · press U for your own")
		var head = body.get_parent().get_child(0)
		head.get_child(0).add_theme_font_size_override("font_size", 46)
		if head.get_child_count() > 1: head.get_child(1).add_theme_font_size_override("font_size", 24)
		var lg = Logo.new(g.selene, 70.0); head.add_child(lg); head.move_child(lg, 0)
	func sig() -> String: return "%d|%s" % [int(Time.get_ticks_msec() / 2000), str(g.selene.share_board)]
	func build() -> void:
		var s = g.selene
		var r = UI.hbox(16); r.size_flags_vertical = Control.SIZE_EXPAND_FILL; body.add_child(r)
		for i in 4:
			var cr = sim().crew[i]
			var c = UI.card(0.6); c[0].custom_minimum_size = Vector2(455, 0); c[0].size_flags_horizontal = Control.SIZE_EXPAND_FILL; r.add_child(c[0])
			var nm = "You" if i == 0 else cr.name.split(" ")[0]
			if not s.share_board[i]:
				c[1].add_child(UI.lbl(nm, 62, UI.INK)); c[1].add_child(UI.lbl("Chose not to appear on the wall.", 40, UI.MUT, true)); continue
			var p = s.phys(i); var v = s.vit[i]
			var h = UI.hbox(18); c[1].add_child(h)
			h.add_child(UI.lbl(nm, 62, UI.INK)); h.add_child(UI.lbl(p.status, 62, SeleneUI.sev_col(p.status)))
			c[1].add_child(UI.lbl("%s · %d bpm" % ["asleep" if cr.asleep else ("on EVA" if cr.outside else "awake"), int(v.hr)], 42, UI.INK))
			for m in [["Exercise", s.ex_min[i] / s.EX_GOAL, GRN, "%d / 120 min" % int(s.ex_min[i])], ["Water", s.water[i] / s.WATER_GOAL, UI.ACC, "%.1f / 2.5 L" % (s.water[i] / 1000.0)],
					["Sleep", v.sleep / 8.0, Color("#a970ff"), ("%.1f h so far" if v.asleep else "%.1f / 8 h") % v.sleep]]:
				c[1].add_child(UI.lbl("%s  %s" % [m[0], m[3]], 32, UI.MUT))
				c[1].add_child(UI.bar(m[1], m[2], 410, 20))
		var r2 = UI.hbox(50); body.add_child(r2)
		var x = s.exterior(); var co2 = s.ppco2()
		r2.add_child(UI.lbl("Cabin CO₂ %.2f mmHg (limit 3) · O₂ %.1f %%" % [co2, sim().hab.o2], 36, RED if co2 > s.CO2_LIMIT else UI.INK))
		r2.add_child(UI.lbl("Outside %d °C · EVA %s" % [int(x.temp), "GO" if x.eva_ok else "HOLD"], 36, GRN if x.eva_ok else RED))
