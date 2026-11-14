class_name Screens
extends RefCounted
## The interfaces on the habitat's real screens. Shared screens show habitat data and anonymous crew data
## only (hard limit 1); your own data appears only on your pod desk, the kiosk and your wrist.

static func make(id: String, g) -> SUI:
	match id:
		"airlock": return AirlockUI.new(g)
		"pod_desk": return PodUI.new(g)
		"kiosk": return KioskUI.new(g)
		"hub": return HubUI.new(g)
		"eclss": return EclssUI.new(g)
		"calib": return CalibUI.new(g)
		"robot": return RobotUI.new(g)
		"forecast": return ForecastUI.new(g)
		"twin": return TwinUI.new(g)
		"console": return ConsoleUI.new(g)
		"luna_log": return LogUI.new(g)
		"eva_prep": return EvaPrepUI.new(g)
		"family": return FamilyUI.new(g)
		"plan": return PlanUI.new(g)
		"dock": return DockUI.new(g)
	return null

class SUI extends RefCounted:
	var g
	var root: Control
	var body: VBoxContainer
	var screen
	var last_sig = "~"
	func _init(game, title := "", sub := "") -> void:
		g = game
		var r = UI.screen_root(title, sub)
		root = r[0]; body = r[1]
	func sig() -> String: return ""
	func build() -> void: pass
	func tick() -> void: pass
	func refresh(force := false) -> void:
		var s = sig()
		if force or s != last_sig:
			last_sig = s
			UI.clear(body); build()
			if screen: screen.request()
		tick()
	func sim() -> VRSim: return g.sim
	func row(a: String, b: String, col := UI.INK) -> HBoxContainer:
		var h = UI.hbox(10)
		var l = UI.lbl(a, 20, UI.MUT); l.custom_minimum_size.x = 250; h.add_child(l)
		h.add_child(UI.lbl(b, 22, col))
		return h

# ------------------------------------------------------------------------------------------------------
class AirlockUI extends SUI:
	func _init(game) -> void: super._init(game, "Airlock · dust lock", "particle counter")
	func sig() -> String:
		var H = sim().hab
		return "%d|%d|%s|%s" % [int(H.dust_air), int(H.airlock_dust * 10.0), g.outside, sim().fired.get("eva_task", false)]
	func build() -> void:
		var H = sim().hab
		var bad = H.dust_air > 15.0
		body.add_child(row("Cabin air dust", "%.0f µg/m³ %s" % [H.dust_air, "(target < 15)" if bad else "· fine"], UI.BAD if bad else UI.OK))
		body.add_child(UI.bar(H.dust_air / 40.0, UI.BAD if bad else UI.OK, 560, 14, 15.0 / 40.0))
		body.add_child(row("Dust in the airlock", "%.1f load units" % H.airlock_dust, UI.WARN if H.airlock_dust > 0.6 else UI.INK))
		body.add_child(UI.bar(H.airlock_dust / 3.0, UI.WARN, 560, 14))
		body.add_child(UI.btn("Run cleaning cycle: curtain + HEPA vacuum", func(): g.airlock_cycle(), 0.55, 22))
		body.add_child(UI.lbl("Suitports are in the north wall: your suit stays outside, so most dust does too. What gets in is caught here.", 17, UI.MUT, true))
		if sim().fired.get("eva_task", false): body.add_child(UI.lbl("Task: EVA to brush the solar arrays (use a suitport).", 19, UI.WARN, true))

# ------------------------------------------------------------------------------------------------------
class PodUI extends SUI:
	var tab = "sleep"
	func _init(game) -> void: super._init(game, "Pod 1 · You", "private: only you see this desk")
	func sig() -> String:
		var cr = sim().me()
		return "%s|%s|%s|%s|%s|%d|%s|%s" % [tab, cr.smart_alarm, cr.dawn_light, cr.pod_invite, cr.tell_me, sim().day(), sim().player_offer.get("id", ""), sim().pod_manual]
	func build() -> void:
		var tabs = UI.hbox(8)
		for t in [["sleep", "Last night"], ["pod", "My pod"], ["home", "Call home"]]:
			var tt = t[0]
			tabs.add_child(UI.btn(t[1], func(): tab = tt; refresh(true), 0.6 if tab == tt else 0.62, 20))
		body.add_child(tabs)
		var cr = sim().me()
		match tab:
			"sleep":
				var n = g.flags.get("last_night", cr.night)
				var hyp = g.flags.get("last_hyp", cr.hyp)
				body.add_child(UI.lbl("%.1f h asleep · %d wake-ups · deep %.1f h · REM %.1f h" % [n.get("hours", 0.0), n.get("wakes", 0), n.get("deep", 0.0), n.get("rem", 0.0)], 24, UI.INK))
				var hc = HypC.new(); hc.stages = hyp; hc.custom_minimum_size = Vector2(800, 120); body.add_child(hc)
				body.add_child(UI.lbl("Woke from %s." % ("deep sleep (groggy)" if cr.woke_deep else "light sleep: the smart alarm waited for it"), 18, UI.MUT))
				var r = UI.hbox(8)
				r.add_child(UI.btn("Smart alarm: " + ("on" if cr.smart_alarm else "off"), func(): cr.smart_alarm = not cr.smart_alarm; refresh(true), 0.35, 19))
				r.add_child(UI.btn("Dawn light: " + ("on" if cr.dawn_light else "off"), func(): cr.dawn_light = not cr.dawn_light; refresh(true), 0.1, 19))
				body.add_child(r)
			"pod":
				var r1 = UI.hbox(8)
				body.add_child(UI.lbl("Pod colour", 20, UI.MUT))
				for c in [Color("#4f8ef7"), Color("#3fbfb0"), Color("#e08a3c"), Color("#c065d6"), Color("#6abf5a"), Color("#d9534f")]:
					var cc = c
					var b = UI.btn("   ", func(): (g.hab.dyn["pod0_accent"] as StandardMaterial3D).albedo_color = cc, cc.h, 20)
					r1.add_child(b)
				body.add_child(r1)
				var r2 = UI.hbox(6)
				for p in [["", "Auto"], ["warm", "Warm"], ["amber", "Amber"], ["cool", "Cool"], ["energise", "Bright"]]:
					var pp = p[0]
					r2.add_child(UI.btn(p[1] + (" ✓" if sim().pod_manual == pp else ""), func(): sim().pod_manual = pp; refresh(true), 0.12, 18))
				body.add_child(UI.lbl("Pod light (yours to set; LUNA changes it only if you invite it)", 18, UI.MUT, true))
				body.add_child(r2)
				var r3 = UI.hbox(8)
				r3.add_child(UI.btn("LUNA may adjust my pod: " + ("yes" if cr.pod_invite else "no"), func(): cr.pod_invite = not cr.pod_invite; refresh(true), 0.75, 18))
				r3.add_child(UI.btn("Tell me every change: " + ("yes" if cr.tell_me else "no"), func(): cr.tell_me = not cr.tell_me; refresh(true), 0.45, 18))
				body.add_child(r3)
			"home":
				var off = sim().player_offer
				if not off.is_empty() and off.id == "earthcall":
					body.add_child(UI.lbl("LUNA booked the comms window for you.", 22, UI.OK))
					body.add_child(UI.btn("Call home now (35 min)", func():
						g.start_activity("call", "Calling home", 35.0, "earthcall", {"fx": off.fx}, true, func():
							sim().complete_player_offer("earthcall"); g._toast("It was good to hear their voices.")), 0.4, 22))
				else:
					body.add_child(UI.lbl("Live calls need a clear comms window (about 19:30). A recorded message works any time.", 20, UI.INK, true))
				body.add_child(UI.btn("Record a video message home (10 min)", func():
					g.start_activity("msg", "Recording a message home", 10.0, "pod", {"fx": {"connection": 0.25, "mood": 0.08}}, true, func(): g._toast("Message queued for the next uplink.")), 0.58, 20))

class HypC extends Control:
	var stages = []
	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, size), Color("#101a3a"))
		var lv = {"Wake": 0, "REM": 1, "N1": 2, "N2": 3, "N3": 4}
		var cols = {"Wake": Color("#f87171"), "REM": Color("#c084fc"), "N1": Color("#7dd3fc"), "N2": Color("#38bdf8"), "N3": Color("#1d4ed8")}
		if stages.is_empty(): return
		var w = size.x / stages.size()
		for k in stages.size():
			var s = stages[k]; var y = lv.get(s, 2) * size.y / 5.0
			draw_rect(Rect2(k * w, y, w + 1, size.y / 5.0), cols.get(s, Color.WHITE))
		for k in ["Wake", "REM", "N1", "N2", "N3"]:
			draw_string(ThemeDB.fallback_font, Vector2(4, lv[k] * size.y / 5.0 + 16), k, HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color(1, 1, 1, 0.7))

# ------------------------------------------------------------------------------------------------------
class KioskUI extends SUI:
	var share = false
	var result = ""
	func _init(game) -> void: super._init(game, "Daily check-in", "private by default")
	func sig() -> String:
		var d = sim().day()
		return "%d|%s|%s|%s" % [d, sim().my_checkins.any(func(c): return c.day == d), share, result]
	func build() -> void:
		var d = sim().day()
		var done = sim().my_checkins.any(func(c): return c.day == d)
		if not done:
			body.add_child(UI.lbl("How are you today?", 26, UI.INK))
			var r = UI.hbox(8)
			for k in [[1, "1 rough"], [2, "2 low"], [3, "3 okay"], [4, "4 good"], [5, "5 great"]]:
				var sc = k[0]
				r.add_child(UI.btn(k[1], func(): result = sim().player_checkin(sc, share); g.play("chime"); refresh(true), 0.02 + 0.08 * (sc - 1), 20))
			body.add_child(r)
			body.add_child(UI.btn("Share with the medical officer: " + ("yes" if share else "no (private)"), func(): share = not share; refresh(true), 0.75, 18))
			body.add_child(UI.lbl("Your answer helps LUNA read you correctly when sensors are unsure. You can skip any day.", 17, UI.MUT, true))
		else:
			var c = sim().my_checkins.filter(func(x): return x.day == d)[0]
			body.add_child(UI.lbl("Checked in: %d/5 · %s" % [c.score, "shared with the CMO" if c.share else "private"], 22, UI.OK))
			body.add_child(UI.lbl(result if result != "" else sim().kiosk_suggestion(), 19, UI.INK, true))
			var r2 = UI.hbox(6)
			for t in [["I'm tired", "nap", 3], ["I'm stressed", "breathing", 0], ["I miss home", "earthcall", 1], ["Let's talk", "chat", 0]]:
				var tt = t
				r2.add_child(UI.btn(tt[0], func(): sim().player_request(tt[1], tt[2]); g.play("click"), 0.6, 17))
			body.add_child(r2)
			body.add_child(UI.lbl("LUNA's read on you right now (only you see this)", 16, UI.MUT))
			var grid = GridContainer.new(); grid.columns = 2; body.add_child(grid)
			for k in 6:
				grid.add_child(UI.lbl("%s %.2f (conf %d%%)" % [D.CONDS[k], sim().luna.est[0][k], int(sim().luna.conf[0][k] * 100)], 15, UI.INK))
				grid.add_child(UI.bar(sim().luna.est[0][k], D.COND_COL[k], 120, 10, D.T2))

# ------------------------------------------------------------------------------------------------------
class HubUI extends SUI:
	func _init(game) -> void: super._init(game, "Social hub", "dining · movie · split")
	func sig() -> String:
		var p = sim().pending_at("hub")
		return "%s|%s|%d|%s" % [g.hub_mode, sim().hab.config_b, p.size(), p[0].id if p.size() else ""]
	func build() -> void:
		var r = UI.hbox(8)
		r.add_child(UI.btn("Dining" + (" ✓" if g.hub_mode == "dining" else ""), func(): g.hub_mode = "dining"; refresh(true), 0.08, 20))
		r.add_child(UI.btn("Movie" + (" ✓" if g.hub_mode == "movie" else ""), func(): g.hub_mode = "movie"; refresh(true), 0.7, 20))
		r.add_child(UI.btn("Partition " + ("up" if sim().hab.config_b else "down"), func():
			var H = sim().hab; H.config_b = not H.config_b; H.config_b_until = sim().now + 1440.0 if H.config_b else -1.0; refresh(true), 0.12, 20))
		body.add_child(r)
		var ev = sim().pending_at("hub")
		if ev.is_empty():
			body.add_child(UI.lbl("The partition splits the room into two quieter halves (Floor plan B) when two people need space.", 17, UI.MUT, true))
			return
		var e = ev[0]
		body.add_child(UI.lbl(e.title, 22, UI.WARN))
		body.add_child(UI.lbl(e.text, 16, UI.INK, true))
		for k in e.choices.size():
			var kk = k
			body.add_child(UI.btn(("★ " if k == e.rec else "") + e.choices[k][0], func(): sim().choose(e, kk); g.play("click"), 0.58, 16))

# ------------------------------------------------------------------------------------------------------
class EclssUI extends SUI:
	func _init(game) -> void: super._init(game, "Life support", "CO₂ · O₂ · water · power")
	func sig() -> String:
		var H = sim().hab
		return "%d|%d|%d|%d|%s" % [int(H.co2 / 20.0), int(H.equip.scrubber * 20), int(H.battery), H.parts, sim().fired.get("swap_task", false)]
	func build() -> void:
		var H = sim().hab
		var hi = H.co2 > 1000.0
		body.add_child(row("Cabin CO₂", "%d ppm" % int(H.co2), UI.BAD if hi else UI.OK))
		body.add_child(UI.bar(H.co2 / 2000.0, UI.BAD if hi else UI.OK, 560, 12, 0.5))
		body.add_child(row("O₂", "%.1f %%" % H.o2))
		body.add_child(row("Scrubber · O₂ gen · water", "%d%% · %d%% · %d%%" % [int(H.equip.scrubber * 100), int(H.equip.oga * 100), int(H.equip.recycler * 100)], UI.BAD if H.equip.scrubber < 0.6 else UI.INK))
		body.add_child(row("Power", "%.1f kW in · %.1f kW used · battery %d%%" % [H.gen_kw, H.load_kw, int(H.battery / 2.0)]))
		if sim().fired.get("swap_task", false):
			body.add_child(UI.lbl("Swap the yellow sorbent cartridge below this screen (1 spare part).", 19, UI.WARN, true))

# ------------------------------------------------------------------------------------------------------
class CalibUI extends SUI:
	var sel = "pod0"
	const Z = ["pod0", "pod1", "pod2", "pod3", "quarters", "communal", "greenhouse", "work", "exercise", "medical", "corridor", "eclss", "airlock"]
	func _init(game) -> void: super._init(game, "Lighting calibration", "LUNA's lighting, checked by a human")
	func sig() -> String:
		var L = sim().hab.light.get(sel, sim().hab.light.get("communal", {}))
		return "%s|%s|%d" % [sel, L.get("preset", ""), int(L.get("lux", 0.0) / 25.0)]
	func build() -> void:
		var grid = GridContainer.new(); grid.columns = 7; body.add_child(grid)
		for z in Z:
			var zz = z
			grid.add_child(UI.btn(z, func(): sel = zz; refresh(true), 0.6, 14))
		var L = sim().hab.light.get(sel, sim().hab.light.get("communal", null))
		if L == null: return
		body.add_child(UI.lbl("%s now: %s · %d K · %d lux" % [sel, L.preset, int(L.cct), int(L.lux)], 20, UI.INK))
		var lv = sim().luna.active_levers_in(sel, sim().now)
		var lights = []
		for l in lv: if D.LEVERS[l].has("light"): lights.append(D.LEVERS[l].name)
		body.add_child(UI.lbl("LUNA is adjusting this zone: " + ", ".join(lights) if not lights.is_empty() else "Following the normal day plan.", 17, UI.WARN if not lights.is_empty() else UI.MUT, true))
		var strip = DayStrip.new(); strip.room = sel; strip.custom_minimum_size = Vector2(580, 40); body.add_child(strip)

class DayStrip extends Control:
	var room = ""
	func _draw() -> void:
		var base = {"communal": "warm", "work": "cool", "exercise": "energise", "greenhouse": "grow", "corridor": "corridor"}.get(room, "neutral")
		for h in 24:
			var p = base
			if h >= 22 or h < 6: p = "night" if not room.begins_with("pod") else "sleepdark"
			elif h >= 20: p = "evening"
			elif room.begins_with("pod") and h == 6: p = "dawn"
			var pr = Hab.PRESETS.get(p, [4000.0, 300.0])
			var c = Mats.cct(pr[0]) * (0.25 + 0.75 * clamp(log(max(pr[1], 1.0)) / log(800.0), 0.0, 1.0))
			draw_rect(Rect2(size.x * h / 24.0, 0, size.x / 24.0 + 1, size.y - 14), c)
			if h % 6 == 0: draw_string(ThemeDB.fallback_font, Vector2(size.x * h / 24.0, size.y), "%02d" % h, HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(1, 1, 1, 0.7))

# ------------------------------------------------------------------------------------------------------
class RobotUI extends SUI:
	func _init(game) -> void: super._init(game, "Robot bay & stores", "")
	func sig() -> String:
		var H = sim().hab
		return "%d|%s|%d|%d" % [H.parts, H.robot_task.get("what", ""), int(H.dust_panels * 10), int(H.equip.scrubber * 10)]
	func build() -> void:
		var H = sim().hab
		body.add_child(row("Spare parts", "%d left (resupply day 90)" % H.parts, UI.WARN if H.parts < 4 else UI.INK))
		body.add_child(UI.bar(H.parts / 12.0, UI.ACC, 520, 12))
		if H.robot_task.is_empty(): body.add_child(row("Robots", "both docked and charging", UI.OK))
		else:
			var h = int(H.robot_task.until / 60.0) % 24; var m = int(H.robot_task.until) % 60
			body.add_child(row("Robot job", "%s until %02d:%02d" % [H.robot_task.what, h, m], UI.WARN))
		var free = H.robot_task.is_empty() and H.parts > 0
		body.add_child(UI.btn("Six-legged robot: clean the arrays (4 h, 1 part)", func(): H.start_robot("dust", sim().now, 4.0); refresh(true), 0.12, 18, free and H.dust_panels > 0.08))
		body.add_child(UI.btn("Maintenance robot: repair the scrubber (6 h, 1 part)", func(): H.start_robot("scrubber", sim().now, 6.0); refresh(true), 0.55, 18, free and H.equip.scrubber < 0.6))

# ------------------------------------------------------------------------------------------------------
class ForecastUI extends SUI:
	func _init(game) -> void: super._init(game, "Trend & forecast wall", "habitat data + anonymous crew data only (hard limit 1)")
	func sig() -> String: return "%d" % int(sim().now / 30.0)
	func _proj(v: Array) -> Array:
		if v.size() < 4: return []
		var n = min(8, v.size()); var a = v[v.size() - n]; var b = v[v.size() - 1]
		var slope = (b - a) / (n - 1)
		var out = []
		for k in range(1, 9): out.append(b + slope * k)
		return out
	func build() -> void:
		var H = sim().hab; var hh = sim().record.hist
		var row2 = UI.hbox(14); body.add_child(row2)
		var items = [
			["CO₂ (ppm) · next 4 h", hh.co2, 400.0, 2000.0, "%d ppm" % int(H.co2), UI.BAD if H.co2 > 1000 else UI.OK],
			["Battery (%)", hh.bat, 0.0, 100.0, "%d %%" % int(H.battery / 2.0), UI.ACC],
			["Cabin dust (µg/m³)", hh.dust, 0.0, 40.0, "%.0f · arrays %d%% dusty" % [H.dust_air, int(H.dust_panels * 100.0)], UI.WARN],
			["Crew wellbeing (anonymous)", hh.morale, 0.0, 100.0, "%d %% average" % int(sim().crew_wellbeing_avg() * 100.0), UI.OK],
		]
		for it in items:
			var c = UI.card(it[5].h); row2.add_child(c[0])
			c[1].add_child(UI.lbl(it[0], 17, UI.MUT)); c[1].add_child(UI.lbl(it[4], 24, it[5]))
			c[1].add_child(UI.spark(it[1], it[5], 330, 120, it[2], it[3], _proj(it[1])))
		var c2 = UI.card(0.75); row2.add_child(c2[0])
		var st = sim().luna.stats
		c2[1].add_child(UI.lbl("Time together now", 17, UI.MUT))
		c2[1].add_child(UI.lbl("%d %% of awake crew are with someone" % int(sim().time_together_today() * 100.0), 20, UI.INK))
		c2[1].add_child(UI.lbl("LUNA so far: %d quiet changes · %d offers (%d yes, %d no) · %d human escalations" % [st.t1, st.offers, st.accepted, st.declined, st.escalations], 17, UI.INK, true))

# ------------------------------------------------------------------------------------------------------
class TwinUI extends SUI:
	var mode = "light"
	var sel = ""
	var map: TwinMap
	func _init(game) -> void: super._init(game, "Digital twin", "live habitat · tap a room")
	func sig() -> String: return mode + "|" + sel + "|" + str(int(sim().now / 15.0))
	func build() -> void:
		var hb = UI.hbox(14); body.add_child(hb)
		map = TwinMap.new(); map.ui = self; map.custom_minimum_size = Vector2(640, 420); hb.add_child(map)
		var side = UI.vbox(8); side.size_flags_horizontal = Control.SIZE_EXPAND_FILL; hb.add_child(side)
		var r = UI.hbox(6); side.add_child(r)
		for m in [["light", "Light"], ["systems", "Systems"], ["people", "People"]]:
			var mm = m[0]
			r.add_child(UI.btn(m[1] + (" ✓" if mode == mm else ""), func(): mode = mm; refresh(true), 0.55, 18))
		var info = UI.lbl(_info(), 19, UI.INK, true); side.add_child(info)
		var H = sim().hab
		side.add_child(UI.lbl("CO₂ %d ppm · O₂ %.1f %% · battery %d %%\nsolar %.1f kW · arrays %d %% dusty · cabin dust %.0f µg/m³" % [int(H.co2), H.o2, int(H.battery / 2.0), H.gen_kw, int(H.dust_panels * 100.0), H.dust_air], 17, UI.MUT, true))
	func tick() -> void:
		if map: map.queue_redraw()
		if screen: screen.request(1)
	func _info() -> String:
		if sel == "": return "Crew positions come from the location beacons everyone agreed to wear for safety. Mood data is never shown here."
		var H = sim().hab
		var key = sel if sel != "quarters" else "quarters"
		var L = H.light.get(key, H.light.get("communal", {}))
		var n = 0
		for cr in sim().crew: if not cr.outside and H.zone_of(cr.cell) == sel: n += 1
		return "%s · %d people · light %s (%d K, %d lx) · CO₂ %d ppm" % [Habitat.ROOM_NAME.get(sel, sel), n, L.get("preset", "?"), int(L.get("cct", 0)), int(L.get("lux", 0)), int(H.co2)]

class TwinMap extends Control:
	var ui
	func _sc() -> float: return min(size.x / (29.0 * 1.0), size.y / 19.0)
	func _gui_input(e: InputEvent) -> void:
		if e is InputEventMouseButton and e.pressed:
			var s = _sc(); var c = Vector2i(int(e.position.x / s), int(e.position.y / s))
			var H = ui.sim().hab
			if H.region.has(c):
				var z = H.zone_of(c); ui.sel = "quarters" if z == "qhall" else z; ui.refresh(true)
	func _draw() -> void:
		var s = _sc(); var H = ui.sim().hab
		draw_rect(Rect2(Vector2.ZERO, size), Color("#08102a"))
		for c in H.region:
			var z = H.zone_of(c)
			var col = Color("#1d2b55")
			match ui.mode:
				"light":
					var key = z if not z.begins_with("pod") else z
					var L = H.light.get(key, H.light.get("communal", null))
					if z == "qhall": L = H.light.get("quarters", null)
					if L: col = Mats.cct(L.cct) * clamp(0.25 + 0.6 * log(max(L.lux, 1.0)) / log(800.0), 0.15, 1.0)
				"systems":
					col = Color("#1f6f4a")
					if z == "eclss" and H.equip.scrubber < 0.6: col = Color("#b33a2e")
					if z == "airlock" and H.airlock_dust > 0.6: col = Color("#b38a2e")
					if H.co2 > 1000.0 and z != "eclss": col = Color("#8a6a2e")
				"people":
					col = Color("#1d2b55")
			if ui.sel != "" and (z == ui.sel or (ui.sel == "quarters" and z == "qhall")): col = col.lerp(Color.WHITE, 0.25)
			draw_rect(Rect2(Vector2(c) * s + Vector2(0.5, 0.5), Vector2(s - 1, s - 1)), col)
		var names = ["Y", "L", "A", "S"]
		for cr in ui.sim().crew:
			if cr.outside: continue
			var p = (cr.pos / 16.0) * s if cr.i != 0 else (Vector2(ui.g.player.global_position.x, ui.g.player.global_position.z) / Habitat.CELL) * s
			draw_circle(p, s * 0.45, Avatar.SUIT[cr.i] if cr.i else Color("#ffffff"))
			draw_string(ThemeDB.fallback_font, p + Vector2(-5, 6), names[cr.i], HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color.BLACK)
		if ui.g.outside: draw_string(ThemeDB.fallback_font, Vector2(4, size.y - 6), "You: outside on EVA", HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color.WHITE)

# ------------------------------------------------------------------------------------------------------
class ConsoleUI extends SUI:
	func _init(game) -> void: super._init(game, "Emergency coordination", "LUNA advises · you decide")
	func sig() -> String:
		var p = sim().pending_at("ops")
		return "%d|%s|%d" % [p.size(), p[0].id if p.size() else "", int(sim().now / 10.0) if p.size() else 0]
	func build() -> void:
		var ev = sim().pending_at("ops")
		if ev.is_empty():
			body.add_child(UI.lbl("No decisions waiting.", 24, UI.OK))
			for d in sim().decisions.slice(max(0, sim().decisions.size() - 5)):
				body.add_child(UI.lbl("· %s → %s (%s)" % [d.title, d.choice, d.by], 16, UI.MUT, true))
			body.add_child(UI.lbl("Last human review of LUNA: %d days ago (limit 7)." % int((sim().now - sim().luna.last_review) / 1440.0), 16, UI.MUT))
			return
		var e = ev[0]
		var left = int(sim().event_deadline.get(e.id, sim().now) - sim().now)
		body.add_child(UI.lbl(e.title + ("   · safe default in %d min" % left if left > 0 else ""), 24, UI.WARN))
		body.add_child(UI.lbl(e.text, 16, UI.INK, true))
		var locked = e.get("locked", [])
		for k in e.choices.size():
			var kk = k
			var lockd = k in locked
			var b = UI.btn(("★ LUNA recommends · " if k == e.rec else "") + e.choices[k][0] + (" (blocked: hard limit)" if lockd else ""), func(): sim().choose(e, kk); g.play("click"); refresh(true), 0.58 if k != e.rec else 0.42, 17, not lockd)
			body.add_child(b)
			if e.choices[k].size() > 2: body.add_child(UI.lbl("   predicted: " + e.choices[k][2], 14, UI.MUT, true))

# ------------------------------------------------------------------------------------------------------
class LogUI extends SUI:
	func _init(game) -> void: super._init(game, "LUNA activity", "anonymous")
	func sig() -> String: return "%d|%d" % [sim().public_log.size(), sim().luna.stats.t1 + sim().luna.stats.offers]
	func build() -> void:
		var st = sim().luna.stats
		body.add_child(UI.lbl("%d quiet changes · %d offers · %d accepted · %d declined · %d escalations" % [st.t1, st.offers, st.accepted, st.declined, st.escalations], 18, UI.INK, true))
		for p in sim().public_log.slice(max(0, sim().public_log.size() - 5)):
			var h = int(p.min / 60.0) % 24; var m = int(p.min) % 60
			body.add_child(UI.lbl("%02d:%02d  %s" % [h, m, p.text], 15, UI.MUT, true))
		body.add_child(UI.lbl("No names here. Each person sees their own entries; the medical officer sees someone's data only after a Tier 3 that person was told about.", 13, UI.MUT, true))


# ------------------------------------------------------------------------------------------------------
class EvaPrepUI extends SUI:
	func _init(game) -> void: super._init(game, "EVA prep", "suit · readiness")
	func sig() -> String: return "%s|%d|%s" % [g.flags.get("eva_ready", -1.0) > sim().now - 240.0, sim().day(), g.outside]
	func build() -> void:
		var ready = g.flags.get("eva_ready", -1.0) > sim().now - 240.0
		body.add_child(row("Suit O₂ / cooling water", "100 % · 100 %  (umbilicals connected)", UI.OK))
		var L = sim().luna; var cr = sim().me()
		var fat = L.est[0][3]; var anx = L.est[0][0]
		var n = g.flags.get("last_night", cr.night)
		body.add_child(row("Sleep last night", "%.1f h · %d wake-ups" % [n.get("hours", 0.0), n.get("wakes", 0)], UI.INK))
		body.add_child(row("LUNA: fatigue · stress", "%.2f · %.2f" % [fat, anx], UI.WARN if max(fat, anx) > D.T2 else UI.INK))
		var go = fat < D.T2 and anx < D.T2 and n.get("hours", 7.0) >= 5.5
		body.add_child(UI.lbl("LUNA advises: GO" if go else "LUNA advises: rest first, or keep the EVA short. You decide.", 22, UI.OK if go else UI.WARN, true))
		if not ready:
			body.add_child(UI.btn("Run pre-EVA check (5 min)", func():
				g.flags["eva_ready"] = sim().now; g.play("chime"); refresh(true), 0.4, 22))
		else:
			body.add_child(UI.lbl("Check done. Climb in through a suitport.", 20, UI.OK))

# ------------------------------------------------------------------------------------------------------
class FamilyUI extends SUI:
	var idx = 0
	const MSGS = [
		["Mum", "We watched the Moon from the porch tonight and waved. The garden tomatoes finally came in. Eat your vegetables up there too!"],
		["Sister", "Got the robotics team's results back: we made state! Your rover video helped. Send a picture of the greenhouse?"],
		["Dad", "Fixed the old telescope. Saturday we'll try to spot the crater rim where you live. Proud of you."],
	]
	func _init(game) -> void: super._init(game, "Family messages", "private · delayed uplink")
	func sig() -> String: return "%d|%d|%s" % [idx, sim().day(), sim().hab.comms_down_until > sim().now]
	func build() -> void:
		if sim().hab.comms_down_until > sim().now:
			body.add_child(UI.lbl("Relay satellite down: messages queue until it's back.", 20, UI.WARN, true)); return
		var m = MSGS[(idx + sim().day() - 1) % MSGS.size()]
		body.add_child(UI.lbl("From: " + m[0], 20, UI.ACC))
		body.add_child(UI.lbl(m[1], 21, UI.INK, true))
		var r = UI.hbox(8)
		r.add_child(UI.btn("Record a reply (10 min)", func():
			g.start_activity("reply", "Recording a reply home", 10.0, "pod", {"fx": {"connection": 0.3, "mood": 0.08}}, true, func(): g._toast("Reply queued for the next uplink.")), 0.58, 20))
		r.add_child(UI.btn("Next message", func(): idx += 1; refresh(true), 0.6, 20))
		body.add_child(r)

# ------------------------------------------------------------------------------------------------------
class PlanUI extends SUI:
	func _init(game) -> void: super._init(game, "Intervention planning", "crew-wide · no names")
	func sig() -> String:
		return "%d|%d|%d|%d|%d" % [int(sim().movie_at), sim().crew[1].free_block_day, int(sim().sync_meals_until), int(sim().now / 60.0), g.flags.get("gh_session", -1)]
	func build() -> void:
		var L = sim().luna
		var rising = 0; var high = 0
		for cr in sim().crew:
			if cr.i == 0: continue
			var top = 0.0
			for c in 6: top = max(top, L.est[cr.i][c])
			if top >= D.T2: high += 1
			for c in [1, 3, 5]:
				var f = L.forecast(cr.i, c, 7.0)
				if f.risk >= 0.5: rising += 1; break
		body.add_child(UI.lbl("Crew: %d of 3 at an elevated level now · %d with a rising 7-day burnout trend" % [high, rising], 19, UI.WARN if high + rising > 0 else UI.OK, true))
		body.add_child(UI.lbl("Plans apply to everyone, so nobody is singled out:", 17, UI.MUT))
		var day0 = (sim().day() - 1) * 1440.0
		body.add_child(UI.btn("Movie night tonight 20:00 (connection, novelty)", func():
			sim().movie_at = day0 + 20.0 * 60.0; sim().public("Commander scheduled movie night at 20:00."); refresh(true), 0.7, 17, sim().movie_at < day0))
		body.add_child(UI.btn("Shared dinner tonight (connection)", func():
			sim().sync_meals_until = sim().now + 720.0; sim().public("Commander asked for a shared dinner tonight."); refresh(true), 0.08, 17, sim().sync_meals_until < sim().now))
		body.add_child(UI.btn("Free afternoon tomorrow for all (autonomy)", func():
			for cr in sim().crew: cr.free_block_day = sim().day() + 1
			sim().public("Tomorrow afternoon is free for the whole crew."); refresh(true), 0.4, 17, sim().crew[1].free_block_day <= sim().day()))
		body.add_child(UI.btn("Crew greenhouse session (mood, food)", func():
			g.flags["gh_session"] = sim().day()
			for cr in sim().crew:
				if cr.i != 0: cr.offer_queue.append({"id": "greenhouse", "at": sim().now + 10.0, "until": sim().now + 60.0, "fx": {"mood": 0.15, "connection": 0.2}, "cond": 1})
			sim().public("Commander called a crew greenhouse session."); refresh(true), 0.33, 17, g.flags.get("gh_session", -1) != sim().day()))
		body.add_child(UI.lbl(g.selene_status(), 16, UI.ACC if g.selene_live else UI.MUT, true))

# ------------------------------------------------------------------------------------------------------
class DockUI extends SUI:
	var phase = "idle"       # idle | wait | go | done
	var trials = []
	var t0 = 0
	var go_at = 0
	var hr = 0
	var spo2 = 0
	var posture = 0
	func _init(game) -> void: super._init(game, "Wellness Dock", "your scan · private")
	func sig() -> String: return "%s|%d" % [phase, trials.size()]
	func tick() -> void:
		if phase == "wait" and Time.get_ticks_msec() >= go_at:
			phase = "go"; t0 = Time.get_ticks_msec(); refresh(true)
		elif phase == "go" and Time.get_ticks_msec() - t0 > 2500:
			trials.append(2500); _next()
	func _next() -> void:
		if trials.size() >= 5: _finish(); return
		phase = "wait"; go_at = Time.get_ticks_msec() + randi_range(900, 2400); refresh(true)
	func _hit() -> void:
		if phase == "go":
			trials.append(Time.get_ticks_msec() - t0); _next()
		elif phase == "wait":
			trials.append(900); _next()     # false start counts as a lapse
	func _finish() -> void:
		phase = "done"
		var cr = sim().me()
		var live = g.selene_data
		hr = int(live.get("heart_rate", 0)) if live.get("heart_rate") else int(62 + 30 * cr.stress + 8 * cr.fatigue + randi_range(-3, 3))
		spo2 = int(live.get("spo2", 0)) if live.get("spo2") else randi_range(97, 99)
		posture = int(100 - 35 * cr.fatigue - randi_range(0, 6))
		var mean = 0.0
		for t in trials: mean += t
		mean /= trials.size()
		# slower reactions -> more fatigue (a PVT-style self test); the person chose to take it
		var f = clamp((mean - 300.0) / 500.0, 0.0, 1.0)
		sim().luna.est[0][3] = lerp(sim().luna.est[0][3], f, 0.4)
		g.flags["dock_%d" % sim().day()] = int(mean)
		sim().luna._log(sim().now, 0, "info", "Wellness Dock: reaction %d ms, HR %d, posture %d." % [int(mean), hr, posture])
		refresh(true)
	func build() -> void:
		match phase:
			"idle":
				body.add_child(UI.lbl("A 1-minute scan: posture camera, finger-clip heart rate, then the Reaction Self Test. Click the panel the moment it turns green (5 times).", 18, UI.INK, true))
				body.add_child(UI.btn("Start scan", func(): trials = []; _next(), 0.45, 24))
				body.add_child(UI.lbl(g.selene_status(), 15, UI.ACC if g.selene_live else UI.MUT, true))
			"wait", "go":
				var b = Button.new(); b.text = "WAIT…" if phase == "wait" else "NOW!"
				b.action_mode = BaseButton.ACTION_MODE_BUTTON_PRESS
				b.custom_minimum_size = Vector2(640, 300); b.add_theme_font_size_override("font_size", 64)
				var sb = StyleBoxFlat.new(); sb.bg_color = Color("#7a1d1d") if phase == "wait" else Color("#1d7a3a"); sb.set_corner_radius_all(12)
				b.add_theme_stylebox_override("normal", sb); b.add_theme_stylebox_override("hover", sb); b.add_theme_stylebox_override("pressed", sb)
				b.pressed.connect(_hit)
				body.add_child(b)
				body.add_child(UI.lbl("Trial %d of 5" % (trials.size() + 1), 18, UI.MUT))
			"done":
				var mean = 0.0
				for t in trials: mean += t
				mean /= trials.size()
				body.add_child(UI.lbl("Reaction %d ms · HR %d bpm · SpO₂ %d %% · posture %d/100" % [int(mean), hr, spo2, posture], 22, UI.INK, true))
				var verdict = "Sharp. Normal for you." if mean < 380 else ("A little slow: some fatigue showing. A short break or a nap would help." if mean < 520 else "Slow: real fatigue. Rest before any EVA or precision task.")
				body.add_child(UI.lbl(verdict, 20, UI.OK if mean < 380 else UI.WARN, true))
				body.add_child(UI.lbl("Your result updated LUNA's fatigue estimate for you. Nobody else sees it.", 15, UI.MUT, true))
				body.add_child(UI.btn("Done", func(): phase = "idle"; refresh(true), 0.6, 20))
