class_name R3UI
extends RefCounted
## Round-3 screens: AI dashboard, review board, CMO console, room controls, and the "My data" tab of your pod
## desk (the crew interface: Bonus B). Shared screens show habitat data and anonymous crew data only (hard limit 1).

const LIGHTS = [["auto", "Auto"], ["off", "Off"], ["warm", "Warm"], ["cool", "Cool"], ["amber", "Dim"], ["energise", "Bright"], ["night", "Night"]]
const SOUNDS = [["off", "Off"], ["rain", "Rain"], ["forest", "Forest"], ["ocean", "Ocean"], ["cafe", "Café"]]
const TEMPS = [19, 21, 23]
const SCENTS = [["off", "Off"], ["lavender", "Lavender"], ["mint", "Mint"], ["petrichor", "Petrichor"]]


static func tier_counts(L) -> Array:
	var n = [0, 0, 0, 0]
	for i in 4:
		for c in 6: n[L.tier[i][c]] += 1
	return n


# ------------------------------------------------------------------------------------------------------
class DashUI extends Screens.SUI:
	func _init(game) -> void: super._init(game, "AI dashboard", "sensors · tiers · hard limits · anonymous")
	func sig() -> String:
		var L = sim().luna
		return "%s|%s|%s|%d|%d|%d" % [L.off, L.restricted, L.cortisol_failed, L.stats.limits_held, L.stats.t1 + L.stats.offers, int(sim().now / 10.0)]
	func build() -> void:
		var L = sim().luna
		var state = "LUNA ON · normal"; var col = UI.OK
		if L.off: state = "LUNA OFF · it senses but takes no action until a human switches it on"; col = UI.BAD
		elif L.restricted: state = "LUNA RESTRICTED · weekly human review overdue: silent Tier 1 only (hard limit 6)"; col = UI.WARN
		body.add_child(UI.lbl(state, 21, col, true))
		var gr = GridContainer.new(); gr.columns = 4; gr.add_theme_constant_override("h_separation", 18)
		var mass = 0.0; var power = 0.0
		for s in D.SENSOR_ORDER:
			var on = L.loadout.get(s, false)
			var out = 0
			for cr in sim().crew: if cr.opt_out.get(s, false): out += 1
			var txt = "OK"; var c = UI.OK
			if not on: txt = "not fitted"; c = UI.MUT
			elif s == "cortisol" and L.cortisol_failed: txt = "FAILED"; c = UI.BAD
			elif out > 0: txt = "%d opted out" % out; c = UI.WARN
			if on: mass += D.SENSORS[s].mass; power += D.SENSORS[s].power
			gr.add_child(UI.lbl("%s · %s" % [D.SENSORS[s].short, txt], 17, c))
		body.add_child(gr)
		body.add_child(UI.lbl("Sensing layer %.2f kg of %.0f kg · %.0f W of %.0f W (about $%.1fM to land it)" % [mass, D.MASS_BUDGET, power, D.POWER_BUDGET, mass * D.COST_PER_KG], 15, UI.MUT))
		var g2 = GridContainer.new(); g2.columns = 3; g2.add_theme_constant_override("h_separation", 16)
		for c in 6:
			var a = 0.0
			for i in 4: a += L.conf[i][c]
			a /= 4.0
			var h = UI.hbox(6); var l = UI.lbl("%s conf." % D.CONDS[c], 15, UI.MUT); l.custom_minimum_size.x = 130; h.add_child(l)
			h.add_child(UI.bar(a, UI.OK if a >= 0.3 else UI.WARN, 110, 10)); g2.add_child(h)
		body.add_child(g2)
		var n = R3UI.tier_counts(L); var st = L.stats
		body.add_child(UI.lbl("Now (no names): %d at Tier 1 · %d at Tier 2 · %d at Tier 3.   Since launch: %d silent changes · %d offers (%d yes, %d no) · %d escalations (person told first: %d) · hard limits held %d" % [n[1], n[2], n[3], st.t1, st.offers, st.accepted, st.declined, st.escalations, st.informed, st.limits_held], 15, UI.INK, true))
		var r = UI.hbox(8)
		r.add_child(UI.btn("Repair the cortisol patch" if L.cortisol_failed else "Break a sensor (cortisol patch)", func(): g.r3.break_sensor(); refresh(true), 0.08, 17))
		r.add_child(UI.btn("Test a hard limit", func(): g.r3.test_hard_limit(); refresh(true), 0.02, 17))
		r.add_child(UI.btn("Switch LUNA on" if L.off else "AI OFF switch", func(): g.r3.use({"id": "ai_switch"}), 0.35 if L.off else 0.0, 17))
		body.add_child(r)


# ------------------------------------------------------------------------------------------------------
class BoardUI extends Screens.SUI:
	var tab = "q"
	var q = 0
	var d = 0
	func _init(game) -> void: super._init(game, "Review board", "Project ARES · Q1–Q6 · D1–D6 · rubric · tabletop")
	func sig() -> String:
		var tt = g.r3.tabletop
		return "%s|%d|%d|%d|%s|%d" % [tab, q, d, g.r3.done.size(), tt.get("run", false), int(tt.get("progress", 0.0) * 20.0)]
	func build() -> void:
		var tabs = UI.hbox(8)
		for tt in [["q", "Board Q1–Q6"], ["d", "Deliverables D1–D6"], ["r", "Rubric"], ["t", "72-h tabletop"]]:
			var k = tt[0]
			tabs.add_child(UI.btn(tt[1], func(): tab = k; refresh(true), 0.6 if tab == k else 0.62, 18))
		body.add_child(tabs)
		match tab:
			"q": _q()
			"d": _d()
			"r": _r()
			"t": _t()
	func _q() -> void:
		var r = UI.hbox(6)
		for k in 6:
			var kk = k
			r.add_child(UI.btn(D.BOARD[k][0], func(): q = kk; refresh(true), 0.6 if q == k else 0.62, 18))
		body.add_child(r)
		body.add_child(UI.lbl(D.BOARD[q][1], 20, UI.INK, true))
		body.add_child(UI.lbl("LUNA's answer: " + D.BOARD[q][2], 17, UI.MUT, true))
		body.add_child(UI.btn("Run it live", func(): g.r3.board_demo(q), 0.4, 19))
	func _d() -> void:
		var r = UI.hbox(6)
		for k in 6:
			var kk = k
			r.add_child(UI.btn("D%d" % (k + 1), func(): d = kk; refresh(true), 0.6 if d == k else 0.62, 18))
		body.add_child(r)
		var L = sim().luna
		match d:
			0:
				body.add_child(UI.lbl("D1 · Sensor-to-condition matrix (weights: Anx · Dep · Con · Fat · Aut · Mon)", 17, UI.ACC))
				for s in D.SENSOR_ORDER:
					var S = D.SENSORS[s]
					var w = []
					for x in S.w: w.append("%.2f" % x)
					body.add_child(UI.lbl("%s  %s  ·  %.2f kg, %.1f W  ·  fails: %s" % [S.short, " ".join(w), S.mass, S.power, S.fails[0]], 13, UI.INK))
			1:
				body.add_child(UI.lbl("D2 · Floor plans: now %s" % ("B (conflict protocol)" if sim().hab.config_b else "A (nominal)"), 18, UI.ACC))
				body.add_child(UI.lbl("A: one wardroom and one route. B: the partition splits the wardroom into two dining points, a second route is lit, acoustic screens go up. People in conflict get space and nobody is named.", 16, UI.INK, true))
				body.add_child(UI.lbl("Zones: communal (warm) · private pods ×4 (occupant-controlled) · work (cool 6500 K) · exercise (energise) · nature (grow light) · transition corridor (gradient). Pods and hygiene: no camera, no microphone.", 15, UI.MUT, true))
				body.add_child(UI.btn("Switch to plan %s" % ("A" if sim().hab.config_b else "B"), func(): g.r3.toggle_config(); refresh(true), 0.4, 18))
			2:
				body.add_child(UI.lbl("D3 · Response flowcharts (all nine stages, three conditions)", 17, UI.ACC))
				for c in [0, 1, 2]:
					var t1 = []
					for l in L.proto[c].t1: t1.append(D.LEVERS[l].name)
					body.add_child(UI.lbl("%s: inputs + weights (D1) → persists %s → Tier 1 silent: %s → Tier 2 offer: %s → monitoring window %s → resolution: restore gradually, update baseline → Tier 3: person told first, then the medical officer" % [D.CONDS[c], ("%d min" % int(D.PERSIST_MIN[c])) if D.PERSIST_MIN[c] < 600 else ("%d h" % int(D.PERSIST_MIN[c] / 60.0)), ", ".join(t1), D.OFFERS[L.proto[c].t2].name, D.WINDOW_TXT[c]], 13, UI.INK, true))
			3:
				body.add_child(UI.lbl("D4 · Hard limits (enforced in code: try them on the AI dashboard)", 17, UI.ACC))
				for k in D.HARD_LIMITS.size():
					body.add_child(UI.lbl("%d. %s" % [k + 1, D.HARD_LIMITS[k][0]], 13, UI.INK, true))
			4:
				body.add_child(UI.lbl("D5 · Agency review board", 17, UI.ACC))
				body.add_child(UI.lbl("Run any board question live (tab 1), show the twin, the dashboard and the CMO console. Since launch LUNA made %d silent changes, %d offers (%d accepted, %d declined) and %d escalations, every one told to the person first (%d)." % [L.stats.t1, L.stats.offers, L.stats.accepted, L.stats.declined, L.stats.escalations, L.stats.informed], 16, UI.INK, true))
			5:
				var me = sim().me()
				var outs = me.opt_out.keys()
				body.add_child(UI.lbl("D6 · Your mission journal", 17, UI.ACC))
				body.add_child(UI.lbl("Day %d. You %s LUNA, opted out of %s, and LUNA may %s adjust your pod. Rubric tasks done: %d of %d." % [sim().day(), "switched off" if g.r3.done.has("ai_off") else "never switched off", ("nothing" if outs.is_empty() else ", ".join(outs)), "" if me.pod_invite else "not", g.r3.done.size(), R3.RUBRIC.size()], 16, UI.INK, true))
	func _r() -> void:
		var gr = GridContainer.new(); gr.columns = 2; gr.add_theme_constant_override("h_separation", 14)
		for r in R3.RUBRIC:
			var ok = g.r3.done.has(r[1])
			gr.add_child(UI.lbl(("✓ " if ok else "○ ") + r[2], 13, UI.OK if ok else UI.INK))
		body.add_child(gr)
		body.add_child(UI.lbl("%d of %d done · press J any time" % [g.r3.done.size(), R3.RUBRIC.size()], 15, UI.MUT))
	func _t() -> void:
		var tt = g.r3.tabletop
		body.add_child(UI.lbl("Bonus A: a 72-hour mission window with two scripted events at the same time: Sam's low-mood slide and Leo and Ana's conflict. On day 2 the medical officer goes on EVA.", 16, UI.INK, true))
		if tt.get("run", false):
			body.add_child(UI.lbl("Running… %d %%" % int(tt.get("progress", 0.0) * 100.0), 20, UI.ACC))
			body.add_child(UI.bar(tt.get("progress", 0.0), UI.ACC, 600, 14))
		elif tt.get("report", {}).is_empty():
			body.add_child(UI.btn("Run the 72-hour tabletop", func(): g.r3.start_tabletop(); refresh(true), 0.4, 20))
		else:
			var rp = tt.report
			body.add_child(UI.lbl("Report: Tier 1 ×%d · offers ×%d (%d yes, %d no) · escalations ×%d (told first %d) · false alarms %d · missed %d · hard limits held %d" % [rp.t1, rp.offers, rp.accepted, rp.declined, rp.escalations, rp.informed, rp.false_alarms, rp.missed, rp.limits], 16, UI.INK, true))
			for k in rp.first:
				body.add_child(UI.lbl("%s: LUNA first acted after %.1f h" % [D.CREW[k].short, (rp.first[k] - D.START_MIN) / 60.0], 15, UI.MUT))
			body.add_child(UI.lbl("Redesign: " + " ".join(rp.redesign), 15, UI.WARN, true))
			body.add_child(UI.btn("Run it again", func(): g.r3.tabletop = {}; g.r3.start_tabletop(); refresh(true), 0.62, 17))


# ------------------------------------------------------------------------------------------------------
class CmoUI extends Screens.SUI:
	func _init(game) -> void: super._init(game, "CMO console", "Tier 3 only · the person is always told first")
	func sig() -> String:
		var n = 0
		for a in sim().luna.alerts: if a.tier >= 3: n += 1
		return "%d|%s|%d" % [n, sim().crew[D.CMO].outside, sim().day()]
	func build() -> void:
		var cmo = sim().crew[D.CMO]
		body.add_child(UI.lbl("Medical officer: %s · %s" % [cmo.name, "on EVA: the Commander is backup" if cmo.outside else "inside, on call"], 19, UI.WARN if cmo.outside else UI.OK, true))
		var cases = []
		for a in sim().luna.alerts: if a.tier >= 3: cases.append(a)
		if cases.is_empty():
			body.add_child(UI.lbl("No Tier 3 cases. LUNA escalates only when Tier 1 and 2 have not helped, or a reading is severe.", 17, UI.MUT, true))
		for a in cases.slice(max(0, cases.size() - 3)):
			var when = "Day %d %02d:%02d" % [int(a.at / 1440.0) + 1, int(fmod(a.at / 60.0, 24.0)), int(fmod(a.at, 60.0))]
			body.add_child(UI.lbl("%s · case %d · %s · person told first ✓ · %s" % [when, a.id, D.CONDS[a.cond].to_lower(), a.state], 16, UI.INK, true))
		body.add_child(UI.lbl("Chain: person → medical officer (3 min) → Commander if the CMO is on EVA → flight surgeon on Earth (2.6 s round trip). Mission control hears only after the person knows (hard limit 4).", 15, UI.MUT, true))
		body.add_child(UI.btn("Run the Q6 drill: CMO on EVA", func(): g.r3.board_demo(5), 0.08, 18))


# ------------------------------------------------------------------------------------------------------
class RoomUI extends Screens.SUI:
	var room = ""
	func _init(game, r: String) -> void:
		room = r
		super._init(game, "%s · room controls" % Habitat.ROOM_NAME.get(r, r), "your override · LUNA respects it")
	func sig() -> String:
		return str(g.r3.room_set.get(room, {})) + str(sim().luna.zone_manual.has(room))
	func build() -> void:
		var rs = g.r3.room_set[room]
		_row("Light", R3UI.LIGHTS, rs.light, "light")
		_row("Sound", R3UI.SOUNDS, rs.sound, "sound")
		var r = UI.hbox(6)
		var l = UI.lbl("Temp", 17, UI.MUT); l.custom_minimum_size.x = 64; r.add_child(l)
		for tc in R3UI.TEMPS:
			var v = tc
			r.add_child(UI.btn("%d °C%s" % [tc, " ✓" if rs.temp == tc else ""], func(): g.r3.room_change(room, "temp", v); refresh(true), 0.0 if tc == 23 else (0.58 if tc == 19 else 0.62), 16))
		body.add_child(r)
		_row("Scent", R3UI.SCENTS, rs.scent, "scent")
		var mine = sim().luna.zone_manual.has(room)
		body.add_child(UI.lbl("You set this room. LUNA leaves it alone until you hand it back." if mine else "LUNA runs this room (light follows the zone and time of day).", 15, UI.OK if mine else UI.MUT, true))
		if mine: body.add_child(UI.btn("Hand it back to LUNA", func(): g.r3.room_reset(room); refresh(true), 0.62, 16))
	func _row(title: String, opts: Array, cur: String, what: String) -> void:
		var r = UI.hbox(5)
		var l = UI.lbl(title, 17, UI.MUT); l.custom_minimum_size.x = 64; r.add_child(l)
		for o in opts:
			var v = o[0]
			r.add_child(UI.btn(o[1] + (" ✓" if cur == v else ""), func(): g.r3.room_change(room, what, v); refresh(true), 0.6 if cur == v else 0.62, 15))
		body.add_child(r)


# ------------------------------------------------------------------------------------------------------
## the "My data" tab of your pod desk: the crew interface (Bonus B)
static func my_data(ui) -> void:
	var g = ui.g; var sim = g.sim; var L = sim.luna; var me = sim.me()
	g.r3.mark("crewif")
	ui.body.add_child(UI.lbl("LUNA's read on you (only you see this)", 16, UI.MUT))
	var gr = GridContainer.new(); gr.columns = 3; gr.add_theme_constant_override("h_separation", 14)
	for c in 6:
		var h = UI.hbox(5); var l = UI.lbl(D.CONDS[c], 15, UI.MUT); l.custom_minimum_size.x = 96; h.add_child(l)
		h.add_child(UI.bar(L.est[0][c], D.TIER_COL[L.tier[0][c]] if L.tier[0][c] > 0 else UI.OK, 110, 10)); gr.add_child(h)
	ui.body.add_child(gr)
	var mine = []
	for e in L.journal:
		if e.crew == 0: mine.append(e)
	if not mine.is_empty():
		ui.body.add_child(UI.lbl("Last thing LUNA did for you: " + mine[-1].text, 14, UI.INK, true))
	var r = UI.hbox(5)
	ui.body.add_child(UI.lbl("Sensors you share (heart-rate alarms stay on for life safety):", 14, UI.MUT))
	for s in ["hrv", "gsr", "sleep", "voice", "facs", "cortisol"]:
		var ss = s
		var out = me.opt_out.get(s, false)
		r.add_child(UI.btn("%s %s" % [D.SENSORS[s].short, "off" if out else "on"], func():
			me.opt_out[ss] = not me.opt_out.get(ss, false)
			if not me.opt_out[ss]: me.opt_out.erase(ss)
			else: g.r3.mark("opt_out")
			L._log(sim.now, 0, "info", "Commander %s %s. LUNA %s." % ["opted out of" if me.opt_out.has(ss) else "shared again", D.SENSORS[ss].name, "stops using it at once" if me.opt_out.has(ss) else "may use it again"])
			ui.refresh(true), 0.02 if out else 0.35, 14))
	ui.body.add_child(r)
	var r2 = UI.hbox(6)
	var paused = me.paused_until > sim.now
	r2.add_child(UI.btn("Resume LUNA for me" if paused else "Pause LUNA for me (24 h)", func():
		me.paused_until = -1.0 if paused else sim.now + 1440.0
		L._log(sim.now, 0, "info", "Commander %s monitoring for themselves." % ("resumed" if paused else "paused (24 h)"))
		ui.refresh(true), 0.12, 15))
	r2.add_child(UI.btn("Export my log", func(): g.r3.export_my_log(), 0.4, 15))
	r2.add_child(UI.btn("Delete my raw history", func():
		for c in 6: L.hist[0][c] = []
		L._log(sim.now, 0, "info", "Commander deleted their raw sensor history. Only the daily summaries needed for the mission record stay.")
		g._toast("Your raw history is deleted. LUNA keeps only daily summaries.", "info"), 0.0, 15))
	ui.body.add_child(r2)


# ------------------------------------------------------------------------------------------------------
## your pod's own controls (next to your pillow): light on/off, temperature, sleep sound, music
class PodRoomUI extends Screens.SUI:
	func _init(game) -> void: super._init(game, "My pod", "light · temperature · AC · sound")
	func sig() -> String: return "%s|%d|%s|%s|%s|%s|%d" % [sim().pod_manual, g.r3.pod_temp, g.r3.sleep_sound, g.r3.music_on, g.r3.ac_mode, g.r3.ac_state, int(g.r3.pod_air * 10.0)]
	func _row(t: String, items: Array) -> void:
		body.add_child(UI.lbl(t, 22, UI.MUT))
		var r = UI.hbox(8)
		for it in items: r.add_child(UI.btn(it[0], it[1], it[2], 26))
		body.add_child(r)
	func build() -> void:
		var r3 = g.r3
		var lights = []
		for o in [["", "Auto"], ["off", "Off"], ["amber", "Dim"], ["warm", "Warm"], ["energise", "Bright"]]:
			var v = o[0]
			lights.append([o[1] + (" ✓" if sim().pod_manual == v else ""), func(): sim().pod_manual = v; g.play("click"); refresh(true), 0.6 if sim().pod_manual == v else 0.62])
		_row("Light", lights)
		var temps = []
		for tc in [18, 20, 22, 24]:
			var v = tc
			temps.append(["%d °C%s" % [tc, " ✓" if r3.pod_temp == tc else ""], func(): r3.pod_temp = v; g.play("click"); refresh(true), 0.58 if tc <= 20 else 0.05])
		_row("Temperature  (now %.1f °C)" % r3.pod_air, temps)
		var st = {"cool": "cooling", "heat": "heating", "hold": "holding", "off": "off"}[r3.ac_state]
		_row("AC: %s" % st, [["Auto" + (" ✓" if r3.ac_mode == "auto" else ""), func(): r3.ac_mode = "auto"; refresh(true), 0.55],
			["Off" + (" ✓" if r3.ac_mode == "off" else ""), func(): r3.ac_mode = "off"; refresh(true), 0.62]])
		_row("When I sleep", [["Air vents" + (" ✓" if r3.sleep_sound == "vent" else ""), func(): r3.sleep_sound = "vent"; refresh(true), 0.55],
			["Silence" + (" ✓" if r3.sleep_sound == "silence" else ""), func(): r3.sleep_sound = "silence"; refresh(true), 0.62],
			["Music " + ("on" if r3.music_on else "off"), func(): r3.music_on = not r3.music_on; refresh(true), 0.75]])
