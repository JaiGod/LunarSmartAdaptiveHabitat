extends Node
## LUNA — Lunar Adaptive Neural Assistant. Game flow, HUD and every screen.

const SPEEDS = [0.0, 2.0, 8.0, 30.0, 90.0]      # simulated minutes per real second
const SPEED_TXT = ["II", ">", ">>", ">>>", ">>>>"]
var sim: Sim
var world
var ui: Control
var layer: CanvasLayer
var state = "title"
var speed = 1
var tab = "crew"
var sel_crew = -1
var sel_room = ""
var modal: Control = null
var top_lbl = {}
var right_box: VBoxContainer
var right_scroll: ScrollContainer
var tab_btns = {}
var ticker: RichTextLabel
var view_btns = {}
var seen_alerts = {}
var tick_msgs = []
var panel_refresh = 0.0
var proto_cond = 0
var scenario = "mission"
var auto_test = false

func _ready() -> void:
	UIK.setup()
	layer = CanvasLayer.new(); add_child(layer)
	ui = Control.new(); ui.theme = UIK.theme; ui.set_anchors_preset(Control.PRESET_FULL_RECT); ui.mouse_filter = Control.MOUSE_FILTER_IGNORE
	layer.add_child(ui)
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--shots="): call_deferred("_shots", a.substr(8)); return
		if a == "--autotest": auto_test = true
	_title()
	if auto_test: call_deferred("_autotest")

func _clear_ui() -> void:
	for c in ui.get_children(): c.queue_free()
	modal = null

# =====================================================================================================
#  title
# =====================================================================================================
func _title() -> void:
	state = "title"; _clear_ui()
	if world: world.queue_free(); world = null
	var bg = TextureRect.new(); bg.texture = load("res://art/title.png"); bg.position = Vector2.ZERO; bg.size = Vector2(640, 360)
	bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE; bg.stretch_mode = TextureRect.STRETCH_SCALE; ui.add_child(bg)
	var t = UIK.lbl("LUNA", Color("#e8f0ff"), 50); t.position = Vector2(40, 30); ui.add_child(t)
	var s = UIK.lbl("Lunar Adaptive Neural Assistant", UIK.ACC, 20); s.position = Vector2(42, 92); ui.add_child(s)
	var s2 = UIK.lbl("Project ARES · a habitat AI that keeps four astronauts well for 90 days", UIK.TEXT); s2.position = Vector2(43, 118); ui.add_child(s2)
	var v = UIK.vbox(4); v.position = Vector2(43, 140); v.custom_minimum_size.x = 210; ui.add_child(v)
	v.add_child(UIK.btn("New 90-day mission", func(): scenario = "mission"; _setup_sensors()))
	v.add_child(UIK.btn("72-hour tabletop (Bonus A)", func(): scenario = "tabletop"; _setup_sensors()))
	v.add_child(UIK.btn("How LUNA works", _how_to))
	var c = UIK.lbl("© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.   ·   NASA HUNCH 2025-26", UIK.DIM); c.position = Vector2(43, 342); ui.add_child(c)

func _how_to() -> void:
	var r = UIK.rich("[b][color=#5fd3e6]You are LUNA, the habitat's AI.[/color][/b]\n\n" +
		"[color=#f2a541]1 · Before launch[/color]  Pick the sensors you can afford (8 kg, 60 W, $1M per kg). Sign the crew charter (hard limits, transparency, human review). Design what LUNA does at each tier for each of the six conditions.\n\n" +
		"[color=#f2a541]2 · On the Moon[/color]  The crew live their day: sleep, meals, work, exercise, greenhouse. You never see how they feel, only sensor estimates with a confidence.\n" +
		"[color=#e6c84a]Tier 1[/color] silent: your protocol changes light, sound, temperature, scent, air.\n" +
		"[color=#f08a3c]Tier 2[/color] an offer the person can refuse. A refusal is final: LUNA backs off.\n" +
		"[color=#e5484d]Tier 3[/color] a human: the person is told first, then the medical officer.\n" +
		"Forecasts warn you days before a slow problem (depression, autonomy, monotony) reaches Tier 2.\n\n" +
		"[color=#f2a541]3 · Also yours[/color]  Power, CO₂, O₂, water, the Mission Garden, dust storms, solar flares, a scrubber failure. Every emergency comes with options and LUNA's predicted outcome.\n\n" +
		"[color=#f2a541]Controls[/color]  Click a crew member or a room on the map. Speed: buttons at the top, or keys 1–5, Space to pause. Views: Map, Twin (digital twin), and overlays for Sensors, Zones and Light.", 400)
	_modal("How LUNA works", r, [["Back", func(): _close_modal()]])

# =====================================================================================================
#  setup 1: sensors (D1)
# =====================================================================================================
func _new_sim() -> void:
	if sim == null or sim.scenario != scenario or state == "title":
		sim = Sim.new(scenario)

func _screen(title: String, sub: String) -> VBoxContainer:
	_clear_ui()
	var bg = ColorRect.new(); bg.color = UIK.BG; bg.size = Vector2(640, 360); ui.add_child(bg)
	var v = UIK.vbox(3); v.position = Vector2(10, 6); v.custom_minimum_size = Vector2(620, 348); ui.add_child(v)
	v.add_child(UIK.lbl(title, UIK.ACC, 20))
	v.add_child(UIK.lbl(sub, UIK.DIM, 10, true, 620))
	return v

func _setup_sensors() -> void:
	_new_sim(); state = "setup"
	var v = _screen("1 · Sensor loadout (D1)", "Every sensor costs mass ($1M per kg to the lunar surface) and power (every watt is not available for life support). Choose what flies. Losing a sensor later is safer than a false alarm, so choose for coverage, not for numbers.")
	var lu: Luna = sim.luna
	var grid = GridContainer.new(); grid.columns = 2; grid.add_theme_constant_override("h_separation", 4); grid.add_theme_constant_override("v_separation", 3)
	v.add_child(grid)
	var budget = UIK.rich("")
	var go_btn = UIK.btn("Next: crew charter  →", _setup_charter)
	var refresh = func():
		var m = lu.mass(); var p = lu.power_w()
		var mc = "#7ddc6f" if m <= D.MASS_BUDGET else "#e5484d"; var pc = "#7ddc6f" if p <= D.POWER_BUDGET else "#e5484d"
		budget.text = "Mass [color=%s]%.2f / %.1f kg[/color] ($%.1fM)   Power [color=%s]%.1f / %.0f W[/color]" % [mc, m, D.MASS_BUDGET, m * D.COST_PER_KG, pc, p, D.POWER_BUDGET]
		go_btn.disabled = m > D.MASS_BUDGET or p > D.POWER_BUDGET
	for k in D.SENSOR_ORDER:
		var S = D.SENSORS[k]
		var card = UIK.panel(UIK.PANEL, UIK.LINE, 3); card.custom_minimum_size = Vector2(306, 0)
		var h = UIK.hbox(4); card.add_child(h)
		var tg = Button.new(); tg.toggle_mode = true; tg.button_pressed = lu.loadout[k]; tg.text = "FLY" if lu.loadout[k] else "—"; tg.custom_minimum_size = Vector2(30, 0); tg.focus_mode = Control.FOCUS_NONE
		tg.toggled.connect(func(on): lu.loadout[k] = on; tg.text = "FLY" if on else "—"; refresh.call())
		h.add_child(tg)
		h.add_child(UIK.icon(load("res://art/%s.png" % S.icon)))
		var col = UIK.vbox(0); col.size_flags_horizontal = Control.SIZE_EXPAND_FILL; h.add_child(col)
		var conds = []
		for c in 6: if S.w[c] >= 0.15: conds.append(D.CONDS[c])
		col.add_child(UIK.rich("[color=#e8f0ff]%s[/color]  [color=#8a96ad]%.2f kg · %.1f W[/color]\n[color=#5fd3e6]%s[/color]\n[color=#8a96ad]× %s[/color]" % [S.name, S.mass, S.power, ", ".join(conds), S.fails[0]], 250))
		grid.add_child(card)
	v.add_child(budget)
	var row = UIK.hbox(6); v.add_child(row)
	row.add_child(UIK.btn("← Title", _title)); row.add_child(UIK.btn("Show the full D1 matrix", func(): _book("d1")))
	row.add_child(go_btn)
	refresh.call()

# =====================================================================================================
#  setup 2: charter (D4)
# =====================================================================================================
func _setup_charter() -> void:
	var v = _screen("2 · Crew charter (D4)", "Hard limits are fixed: LUNA cannot break them, and there is no button that would. The policies below are yours to set, and the crew signs them before launch.")
	var lu: Luna = sim.luna
	var lim = UIK.vbox(1); v.add_child(lim)
	for k in D.HARD_LIMITS.size():
		var h = UIK.hbox(4); h.add_child(UIK.icon(load("res://art/i_lock.png")))
		h.add_child(UIK.lbl("%d. %s" % [k + 1, D.HARD_LIMITS[k][0]], UIK.TEXT, 10, true, 590)); lim.add_child(h)
	v.add_child(UIK.lbl(" ", UIK.DIM))
	var pol = GridContainer.new(); pol.columns = 2; pol.add_theme_constant_override("h_separation", 8); v.add_child(pol)
	var choice = func(label: String, opts: Array, key: String, help: String):
		pol.add_child(UIK.lbl(label, UIK.AMBER))
		var h = UIK.hbox(3)
		for o in opts:
			var b = Button.new(); b.text = o[0]; b.toggle_mode = true; b.focus_mode = Control.FOCUS_NONE
			b.button_pressed = lu.policy[key] == o[1]
			b.pressed.connect(func():
				lu.policy[key] = o[1]
				for s in h.get_children(): if s is Button: s.button_pressed = s == b)
			h.add_child(b)
		h.add_child(UIK.lbl(help, UIK.DIM, 10, true, 230))
		pol.add_child(h)
	choice.call("Activity context (Q1)", [["On", true], ["Off", false]], "gating", "Ignore HR and skin conductance for 60 min after exercise or EVA.")
	choice.call("Transparency", [["Silent + crew-readable log", "log"], ["Notify every change", "notify"]], "transparency", "Tier 1 is silent, but every change is in the person's own log.")
	choice.call("Human review every", [["3 days", 3], ["7 days", 7], ["14 days", 14]], "review_days", "Missed review: LUNA drops to Tier 1 only.")
	choice.call("Raw data kept for", [["7 days", 7], ["30 days", 30], ["90 days", 90]], "retention", "Then only daily summaries remain.")
	choice.call("Pause on Tier 2/3", [["Yes", true], ["No", false]], "pause_alerts", "Stop the clock when a decision is needed.")
	var row = UIK.hbox(6); v.add_child(row)
	row.add_child(UIK.btn("← Sensors", _setup_sensors)); row.add_child(UIK.btn("Next: response protocols  →", _setup_proto))

# =====================================================================================================
#  setup 3: protocols (D3)
# =====================================================================================================
func _setup_proto() -> void:
	var v = _screen("3 · Response protocols (D3)", "For each condition: what LUNA changes silently at Tier 1, and what it offers at Tier 2. Tier 3 is always a human (told first). Watch the 'noticed' value: changes people notice can feel like being managed.")
	var lu: Luna = sim.luna
	var h = UIK.hbox(6); v.add_child(h)
	var left = UIK.vbox(2); left.custom_minimum_size.x = 120; h.add_child(left)
	var right = UIK.vbox(2); right.custom_minimum_size.x = 490; h.add_child(right)
	for c in 6:
		var b = Button.new(); b.toggle_mode = true; b.button_pressed = c == proto_cond; b.focus_mode = Control.FOCUS_NONE
		b.text = D.COND_LONG[c]; b.icon = load("res://art/%s.png" % D.COND_ICON[c]); b.alignment = HORIZONTAL_ALIGNMENT_LEFT
		b.pressed.connect(func(): proto_cond = c; _setup_proto())
		left.add_child(b)
	left.add_child(UIK.lbl(" "))
	left.add_child(UIK.btn("Use LUNA's defaults", func():
		lu.proto = []
		for p in D.DEFAULT_PROTO: lu.proto.append({"t1": p.t1.duplicate(), "t2": p.t2})
		_setup_proto()))
	var c = proto_cond
	right.add_child(UIK.rich("[color=#e6c84a]Tier 1 (silent)[/color] for [color=#e8f0ff]%s[/color]. Monitoring window: %s. Up to 4 levers." % [D.COND_LONG[c].to_lower(), D.WINDOW_TXT[c]], 490))
	var grid = GridContainer.new(); grid.columns = 4; grid.add_theme_constant_override("h_separation", 2); grid.add_theme_constant_override("v_separation", 2); right.add_child(grid)
	var note = UIK.rich("", 490)
	for l in D.LEVER_ORDER:
		var L = D.LEVERS[l]
		var b = Button.new(); b.toggle_mode = true; b.focus_mode = Control.FOCUS_NONE; b.custom_minimum_size = Vector2(120, 0)
		b.text = L.name; b.button_pressed = l in lu.proto[c].t1; b.clip_text = true
		b.mouse_entered.connect(func(): note.text = "[color=#5fd3e6]%s[/color] (%s) · noticed %d%% · %d W\n%s" % [L.name, L.dom, int(L.notice * 100), int(L.power), L.evidence])
		b.toggled.connect(func(on):
			if on and not (l in lu.proto[c].t1):
				if lu.proto[c].t1.size() >= 4: b.set_pressed_no_signal(false); return
				lu.proto[c].t1.append(l)
			elif not on: lu.proto[c].t1.erase(l))
		grid.add_child(b)
	right.add_child(note)
	right.add_child(UIK.rich("[color=#f08a3c]Tier 2 (an offer, never an order)[/color]", 490))
	var og = GridContainer.new(); og.columns = 3; og.add_theme_constant_override("h_separation", 2); og.add_theme_constant_override("v_separation", 2); right.add_child(og)
	for o in D.OFFER_ORDER:
		var O = D.OFFERS[o]
		var b = Button.new(); b.toggle_mode = true; b.focus_mode = Control.FOCUS_NONE; b.custom_minimum_size = Vector2(162, 0); b.clip_text = true
		b.text = O.name; b.button_pressed = lu.proto[c].t2 == o
		b.mouse_entered.connect(func(): note.text = "[color=#f08a3c]%s[/color]\n\"%s\"" % [O.name, O.say])
		b.pressed.connect(func():
			lu.proto[c].t2 = o
			for s in og.get_children(): s.button_pressed = s == b)
		og.add_child(b)
	right.add_child(UIK.rich("[color=#e5484d]Tier 3[/color]: the person is told first, then the medical officer (15 min), the Commander (15 min), the flight surgeon (30 min).", 490))
	var row = UIK.hbox(6); v.add_child(row)
	row.add_child(UIK.btn("← Charter", _setup_charter)); row.add_child(UIK.btn("See the flowchart", func(): _book("d3")))
	row.add_child(UIK.btn("LAUNCH  →", _start_play))

# =====================================================================================================
#  play screen
# =====================================================================================================
func _start_play() -> void:
	_clear_ui(); state = "play"; speed = 1; tab = "crew"; sel_crew = -1
	var bg = ColorRect.new(); bg.color = UIK.BG; bg.size = Vector2(640, 360); bg.mouse_filter = Control.MOUSE_FILTER_IGNORE; ui.add_child(bg)
	if world: world.queue_free()
	world = load("res://scripts/world.gd").new()
	world.sim = sim; world.position = Vector2(0, 20); world.set_meta("font", UIK.font)
	world.picked_crew.connect(func(i): sel_crew = i; sel_room = ""; world.sel_room = ""; tab = "crew"; _refresh_right())
	world.picked_room.connect(func(r): sel_room = r; world.selected = -1; sel_crew = -1; tab = "hab"; _refresh_right())
	add_child(world); move_child(world, 0)
	bg.queue_free()
	# top bar
	var top = UIK.panel(Color("#0f1526"), UIK.LINE, 2); top.position = Vector2(0, 0); top.size = Vector2(640, 20); ui.add_child(top)
	var th = UIK.hbox(4); top.add_child(th)
	th.add_child(UIK.lbl("LUNA", UIK.ACC))
	top_lbl.clock = UIK.lbl("", Color.WHITE); top_lbl.clock.custom_minimum_size.x = 92; th.add_child(top_lbl.clock)
	for k in 5:
		var b = Button.new(); b.text = SPEED_TXT[k]; b.toggle_mode = true; b.focus_mode = Control.FOCUS_NONE; b.custom_minimum_size = Vector2(22, 0)
		b.pressed.connect(func(): _set_speed(k)); th.add_child(b); top_lbl["sp%d" % k] = b
	top_lbl.res = UIK.rich("", 330); top_lbl.res.custom_minimum_size.y = 12; th.add_child(top_lbl.res)
	# right panel
	var rp = UIK.panel(UIK.PANEL, UIK.LINE, 2); rp.position = Vector2(464, 20); rp.size = Vector2(176, 304); ui.add_child(rp)
	var rv = UIK.vbox(2); rp.add_child(rv)
	var tabs = UIK.hbox(1); rv.add_child(tabs)
	for t in [["crew", "CREW"], ["alerts", "ALERTS"], ["hab", "HAB"], ["log", "LOG"]]:
		var b = Button.new(); b.text = t[1]; b.toggle_mode = true; b.focus_mode = Control.FOCUS_NONE; b.custom_minimum_size = Vector2(41, 0)
		b.pressed.connect(func(): tab = t[0]; _refresh_right()); tabs.add_child(b); tab_btns[t[0]] = b
	right_scroll = ScrollContainer.new(); right_scroll.custom_minimum_size = Vector2(170, 280); right_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	rv.add_child(right_scroll)
	right_box = UIK.vbox(3); right_box.custom_minimum_size.x = 162; right_scroll.add_child(right_box)
	# bottom bar
	var bb = UIK.panel(Color("#0f1526"), UIK.LINE, 2); bb.position = Vector2(0, 324); bb.size = Vector2(640, 36); ui.add_child(bb)
	var bh = UIK.hbox(3); bb.add_child(bh)
	ticker = UIK.rich("", 300); ticker.custom_minimum_size = Vector2(300, 30); ticker.fit_content = false; ticker.scroll_active = false; bh.add_child(ticker)
	var views = UIK.vbox(1); bh.add_child(views)
	var r1 = UIK.hbox(1); var r2 = UIK.hbox(1); views.add_child(r1); views.add_child(r2)
	for vdef in [["map", "Map"], ["twin", "Digital twin"], ["book", "Design book"]]:
		var b = UIK.btn(vdef[1], func(): _view(vdef[0])); r1.add_child(b); view_btns[vdef[0]] = b
	for vdef in [["sensors", "Sensors"], ["zones", "Zones"], ["light", "Light"], ["planb", "Floor B"]]:
		var b = UIK.btn(vdef[1], func(): _view(vdef[0])); r2.add_child(b); view_btns[vdef[0]] = b
	var r3 = UIK.vbox(1); bh.add_child(r3)
	r3.add_child(UIK.btn("Menu", _menu))
	_set_speed(1); _refresh_right()

func _view(v: String) -> void:
	match v:
		"map": world.twin = false
		"twin": world.twin = true; tab = "twin"; _refresh_right()
		"book": _book("d1")
		"planb":
			sim.hab.config_b = not sim.hab.config_b; sim.hab.config_b_until = -1.0
			sim.luna._log(sim.now, -1, "info", "Floor plan %s by hand." % ("B (conflict protocol): wardroom split, meals staggered" if sim.hab.config_b else "A (nominal)"))
		_: world.overlay = "" if world.overlay == v else v

func _set_speed(k: int) -> void:
	speed = k
	for j in 5:
		if top_lbl.has("sp%d" % j): top_lbl["sp%d" % j].button_pressed = j == k

func _unhandled_input(ev: InputEvent) -> void:
	if state != "play" or not (ev is InputEventKey) or not ev.pressed: return
	if modal != null: return
	match ev.keycode:
		KEY_SPACE: _set_speed(0 if speed > 0 else 1)
		KEY_1: _set_speed(1)
		KEY_2: _set_speed(2)
		KEY_3: _set_speed(3)
		KEY_4: _set_speed(4)
		KEY_T: _view("twin" if not world.twin else "map")

func _process(delta: float) -> void:
	if state != "play" or sim == null: return
	if modal == null and speed > 0:
		sim.advance(SPEEDS[speed] * delta)
	# decisions
	if modal == null:
		if not sim.pending.is_empty(): _event_modal(sim.pending[0])
		else:
			for a in sim.luna.alerts:
				if a.state == "open" and not seen_alerts.has(a.id) and (a.tier >= 2 or a.get("kind", "") == "forecast"):
					seen_alerts[a.id] = true
					if sim.luna.policy.pause_alerts or a.tier >= 3: _alert_modal(a); break
	if sim.done and modal == null: _end_review()
	# toasts
	while not sim.luna.toasts.is_empty():
		tick_msgs.append(sim.luna.toasts.pop_front())
		if tick_msgs.size() > 3: tick_msgs.pop_front()
	_top_bar()
	panel_refresh -= delta
	if panel_refresh <= 0.0: panel_refresh = 0.5; _refresh_right()

func _top_bar() -> void:
	var h = sim.hab
	top_lbl.clock.text = "Day %d/%d %s" % [sim.day(), sim.mission_days(), sim.clock()]
	var net = h.power_margin()
	var tr = 0.0
	for cr in sim.crew: tr += cr.trust
	var c2 = "#e5484d" if h.co2 > 1000 else ("#f2a541" if h.co2 > 800 else "#d3dae6")
	var bc = "#e5484d" if h.battery < 30 else ("#f2a541" if net < 0 else "#7ddc6f")
	top_lbl.res.text = "[color=%s]PWR %d%% %+.1fkW[/color] O₂ %.1f [color=%s]CO₂ %d[/color] H₂O %d [color=#5fd3e6]♥%d%%[/color] sci %d%%%s" % [bc, int(h.battery / h.BATTERY_MAX * 100.0), net, h.o2, c2, int(h.co2), int(h.water), int(tr / 4.0 * 100.0), int(sim.science), "  [color=#d9a86c]DUST[/color]" if h.dust_storm_until > sim.now else ""]
	var lines = tick_msgs.slice(max(0, tick_msgs.size() - 3))
	if lines.size() < 3:
		var j = sim.luna.journal
		for k in range(max(0, j.size() - (3 - lines.size())), j.size()): lines.insert(0, "[color=#8a96ad]" + j[k].text + "[/color]")
	ticker.text = "\n".join(lines)

# =====================================================================================================
#  right panel
# =====================================================================================================
func _refresh_right() -> void:
	if right_box == null: return
	for k in tab_btns: tab_btns[k].button_pressed = k == tab
	var n_open = sim.luna.open_alerts().size()
	tab_btns.alerts.text = "ALERTS" if n_open == 0 else "ALRT %d" % n_open
	var keep = right_scroll.scroll_vertical
	for c in right_box.get_children(): c.queue_free()
	match tab:
		"crew": _tab_crew()
		"alerts": _tab_alerts()
		"hab": _tab_hab()
		"log": _tab_log()
		"twin": _tab_twin()
	right_scroll.set_deferred("scroll_vertical", keep)

func _crew_card(cr: Crew, detail: bool) -> Control:
	var lu: Luna = sim.luna
	var p = UIK.panel(UIK.PANEL2 if sel_crew != cr.i else Color("#22304a"), UIK.LINE, 2)
	var v = UIK.vbox(1); p.add_child(v)
	var h = UIK.hbox(3); v.add_child(h)
	h.add_child(UIK.icon(load("res://art/portrait%d.png" % cr.i)))
	var where = "on EVA" if cr.outside else (D.ROOMS.get(sim.hab.room_of_zone(cr.zone(sim.hab)), {"name": "?"}).name)
	var act = {"sleep": "asleep · " + cr.stage, "work": "working", "meal": "eating", "exercise": "exercising", "pod": "in own pod", "lounge": "relaxing", "greenhouse": "gardening", "eva": "EVA", "shelter": "sheltering", "cmo": "with the medical officer", "hygiene": "hygiene", "offer": "taking LUNA's offer", "offer_ex": "VR bike ride", "movie": "movie night", "earthcall": "call home", "nap": "napping", "eva_prep": "suiting up", "eva_post": "suit off, dusting"}.get(cr.act_id, cr.act_id)
	var info = UIK.vbox(0); h.add_child(info)
	info.add_child(UIK.lbl(cr.info.name, Color.WHITE))
	info.add_child(UIK.lbl(cr.info.role, UIK.DIM))
	info.add_child(UIK.lbl(act + " · " + where, UIK.ACC, 10, true, 125))
	var grid = GridContainer.new(); grid.columns = 4; grid.add_theme_constant_override("h_separation", 2); grid.add_theme_constant_override("v_separation", 0); v.add_child(grid)
	for c in 6:
		var t = lu.tier[cr.i][c]
		grid.add_child(UIK.icon(load("res://art/%s.png" % D.COND_ICON[c])))
		grid.add_child(UIK.lbl(D.CONDS[c], D.TIER_COL[t] if t > 0 else UIK.DIM))
		var b = UIK.bar(lu.est[cr.i][c], D.TIER_COL[t] if t > 0 else Color("#5d6a85"), 44, 5); b.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		grid.add_child(b)
		var f = lu.forecast(cr.i, c, 14.0)
		var ftxt = ["", "T1", "T2", "T3"][t]
		var fcol = D.TIER_COL[t]
		if t < 2 and f.risk >= 0.5 and c in [1, 4, 5]: ftxt = "↑%d%%" % int(f.risk * 100.0); fcol = UIK.AMBER
		elif lu.conf[cr.i][c] < 0.3: ftxt = "?"; fcol = UIK.DIM
		grid.add_child(UIK.lbl(ftxt, fcol))
	if detail:
		var cf = []
		for c in 6: cf.append("%d" % int(lu.conf[cr.i][c] * 100.0))
		v.add_child(UIK.lbl("Bars: LUNA's estimate (lines = tiers). Confidence %%: %s. ↑ = 14-day risk" % " ".join(cf), UIK.DIM, 10, true, 158))
		for c in [1, 4, 5]:
			var f = lu.forecast(cr.i, c, 14.0)
			if f.risk >= 0.0: v.add_child(UIK.lbl("%s risk in 14 d: %d%%" % [D.CONDS[c], int(f.risk * 100.0)], UIK.AMBER if f.risk > 0.5 else UIK.DIM))
		v.add_child(UIK.lbl("Last night: %.1f h · %d wake-ups · deep %.1f h · REM %.1f h" % [cr.night.hours, cr.night.wakes, cr.night.get("deep", 0.0), cr.night.get("rem", 0.0)], UIK.TEXT, 10, true, 158))
		var hy = UIK.Hypno.new(158, 22); hy.hyp = cr.hyp; v.add_child(hy)
		v.add_child(UIK.lbl("Smart alarm: %s · dawn light: %s" % ["on" if cr.smart_alarm else "off", "on" if cr.dawn_light else "off"], UIK.DIM))
		v.add_child(UIK.btn("Crew interface (what %s sees)" % cr.name, func(): _crew_ui(cr.i)))
	return p

func _tab_crew() -> void:
	if sel_crew >= 0:
		right_box.add_child(_crew_card(sim.crew[sel_crew], true))
		right_box.add_child(UIK.btn("All crew", func(): sel_crew = -1; world.selected = -1; _refresh_right()))
		return
	for cr in sim.crew:
		var card = _crew_card(cr, false)
		var b = Button.new(); b.flat = true; b.focus_mode = Control.FOCUS_NONE; b.set_anchors_preset(Control.PRESET_FULL_RECT)
		var idx = cr.i
		b.pressed.connect(func(): sel_crew = idx; world.selected = idx; _refresh_right())
		card.add_child(b)
		right_box.add_child(card)

func _tab_alerts() -> void:
	var lu: Luna = sim.luna
	var any = false
	for a in lu.alerts:
		if not (a.state in ["open", "waiting", "snoozed"]): continue
		any = true
		var cr: Crew = sim.crew[a.crew]
		var kind = "FORECAST" if a.get("kind", "") == "forecast" else "TIER %d" % a.tier
		var col = UIK.AMBER if kind == "FORECAST" else D.TIER_COL[a.tier]
		var p = UIK.panel(UIK.PANEL2, col, 2); var v = UIK.vbox(1); p.add_child(v)
		v.add_child(UIK.lbl("%s · %s · %s" % [kind, cr.name, D.CONDS[a.cond]], col))
		v.add_child(UIK.lbl({"open": "needs a decision", "waiting": "offer sent, waiting for an answer", "snoozed": "watching for 1 h"}[a.state], UIK.DIM))
		if a.state == "open": v.add_child(UIK.btn("Decide", func(): _alert_modal(a)))
		right_box.add_child(p)
	if not any: right_box.add_child(UIK.lbl("No decisions waiting. Tier 1 runs silently; see the LOG.", UIK.DIM, 10, true, 160))
	var s = lu.stats
	right_box.add_child(UIK.rich("[color=#5fd3e6]So far[/color]\nTier 1 changes %d · offers %d (yes %d, no %d)\nescalations %d · forecasts %d\nfalse alarms %d · missed %d" % [s.t1, s.offers, s.accepted, s.declined, s.escalations, s.get("forecasts", 0), s.false_alarms, s.missed], 160))

func _tab_hab() -> void:
	var h = sim.hab
	var lu: Luna = sim.luna
	if sel_room != "":
		var R = D.ROOMS[sel_room]
		var p = UIK.panel(UIK.PANEL2, UIK.ACC, 2); var v = UIK.vbox(1); p.add_child(v)
		v.add_child(UIK.lbl(R.name, Color.WHITE)); v.add_child(UIK.lbl(R.zone, UIK.AMBER))
		v.add_child(UIK.lbl(R.purpose, UIK.DIM, 10, true, 156))
		var key = sel_room if not (sel_room == "communal" and h.config_b) else "communal_W"
		var L = h.light.get(key, {})
		if not L.is_empty(): v.add_child(UIK.lbl("Light now: %d K · %s (%s)" % [int(L.cct), world._lux_txt(L.lux), L.preset], world.cct_color(L.cct)))
		v.add_child(UIK.lbl("Circadian plan, 00:00 → 24:00:", UIK.DIM))
		var strip = UIK.Strip.new(156, 10)
		for hh in 24:
			var pr = _plan_preset(sel_room, hh + 0.5)
			var P = Hab.PRESETS.get(pr, Hab.PRESETS.neutral)
			strip.cols.append(world.cct_color(P[0]) * world.lux_bright(P[1]))
		strip.now_frac = sim.hour() / 24.0
		v.add_child(strip)
		v.add_child(UIK.lbl("06:30 wake · day light by zone · 20:00 blue-light off (2400 K) · 22:30 night", UIK.DIM, 10, true, 156))
		if sel_room in ["quarters", "hygiene"]:
			v.add_child(UIK.lbl("Private: the occupant controls this room. LUNA only adjusts a pod if its owner invites it.", UIK.TEXT, 10, true, 156))
		else:
			v.add_child(UIK.lbl("Set the light by hand:", UIK.TEXT))
			var g = GridContainer.new(); g.columns = 3; g.add_theme_constant_override("h_separation", 1); v.add_child(g)
			for pr in [["auto", "Auto"], ["warm", "2900K"], ["amber", "2200K"], ["cool", "6300K"], ["therapy", "10k lux"], ["hue", "Hue"]]:
				var cur = lu.zone_manual.get(sel_room, {}).get("light", "auto")
				var b = UIK.btn(pr[1], func():
					var zm = lu.zone_manual.get(sel_room, {"light": "auto", "levers": []}); zm.light = pr[0]; lu.zone_manual[sel_room] = zm
					lu._log(sim.now, -1, "info", "%s light set to %s by hand." % [R.name, pr[1]]); _refresh_right())
				b.button_pressed = cur == pr[0]; b.toggle_mode = true; g.add_child(b)
		right_box.add_child(p)
	# power
	var pw = UIK.panel(UIK.PANEL2, UIK.LINE, 2); var pv = UIK.vbox(1); pw.add_child(pv)
	pv.add_child(UIK.lbl("POWER  gen %.1f kW · load %.1f kW" % [h.gen_kw, h.load_kw], UIK.AMBER))
	pv.add_child(UIK.lbl("Battery %d / %d kWh" % [h.battery, h.BATTERY_MAX], UIK.TEXT))
	pv.add_child(UIK.bar(h.battery / h.BATTERY_MAX, UIK.AMBER, 156, 5, false))
	var hrs = h.battery / max(0.01, -h.power_margin()) if h.power_margin() < 0 else -1.0
	if hrs > 0: pv.add_child(UIK.lbl("Empty in %.0f h at this load" % hrs, UIK.BAD))
	for k in h.loads:
		if h.loads[k] > 0.01: pv.add_child(UIK.lbl("  %s %.1f" % [k, h.loads[k]], UIK.DIM))
	pv.add_child(UIK.lbl("Solar arrays: %d%% dust · %d%% health" % [int(h.dust_panels * 100), int(h.solar_health * 100)], UIK.TEXT))
	var dh = UIK.hbox(1); pv.add_child(dh)
	dh.add_child(UIK.btn("Dust shield " + ("ON" if h.dust_shield else "off"), func(): h.dust_shield = not h.dust_shield; _refresh_right()))
	dh.add_child(UIK.btn("Robot: clean", func():
		if h.start_robot("dust", sim.now, 4.0): lu._log(sim.now, -1, "info", "Robot sent to brush the arrays.")
		_refresh_right(), not h.robot_task.is_empty() or h.parts <= 0))
	right_box.add_child(pw)
	# air + water + dust
	var ap = UIK.panel(UIK.PANEL2, UIK.LINE, 2); var av = UIK.vbox(1); ap.add_child(av)
	av.add_child(UIK.lbl("AIR + WATER", UIK.ACC))
	av.add_child(UIK.lbl("CO₂ %d ppm (brief: under 600)" % h.co2, UIK.BAD if h.co2 > 1000 else UIK.TEXT))
	av.add_child(UIK.lbl("O₂ %.1f%% · water %d L · food %d days" % [h.o2, h.water, h.food], UIK.TEXT))
	av.add_child(UIK.lbl("Cabin dust %d µg/m³ · airlock dust %.1f" % [h.dust_air, h.airlock_dust], UIK.BAD if h.dust_air > 15 else UIK.TEXT))
	for e in h.equip:
		av.add_child(UIK.lbl("  %s %d%%" % [{"scrubber": "CO₂ scrubber", "oga": "O₂ generator", "recycler": "Water recovery", "comms": "Comms"}[e], int(h.equip[e] * 100)], UIK.BAD if h.equip[e] < 0.5 else UIK.DIM))
	av.add_child(UIK.lbl("Spare parts %d · robot %s" % [h.parts, "idle" if h.robot_task.is_empty() else "fixing " + h.robot_task.what], UIK.DIM))
	right_box.add_child(ap)
	# Mission Garden
	var gp = UIK.panel(UIK.PANEL2, UIK.LINE, 2); var gv = UIK.vbox(1); gp.add_child(gv)
	gv.add_child(UIK.lbl("MISSION GARDEN", UIK.GOOD))
	for k in 8:
		var r = h.racks[k]
		if r.built:
			gv.add_child(UIK.lbl("  %d %s %d/%d d · health %d%%%s" % [k + 1, r.crop, int(r.age), int(h.CROPS[r.crop]), int(r.health * 100), " · ripe" if h.ripe(r) else ""], UIK.TEXT if not r.blight else UIK.BAD))
	if h.built_racks() < 8:
		gv.add_child(UIK.btn("Build a rack (2 parts, +0.45 kW)", func():
			for r in h.racks:
				if not r.built and h.parts >= 2:
					r.built = true; r.age = 0.0; r.health = 0.8; h.parts -= 2
					lu._log(sim.now, -1, "info", "New greenhouse rack built: more O₂, food and calm."); break
			_refresh_right(), h.parts < 2))
	right_box.add_child(gp)
	var fb = UIK.btn("Floor plan: %s" % ("B (conflict)" if h.config_b else "A (nominal)"), func(): _view("planb"); _refresh_right())
	right_box.add_child(fb)

## the default circadian schedule for a room (what LUNA runs when no lever overrides it)
func _plan_preset(room: String, h: float) -> String:
	if h >= 22.5 or h < 6.5: return "night"
	if h >= 20.0: return "evening"
	return {"communal": "warm", "work": "cool", "exercise": "energise", "greenhouse": "grow", "medical": "neutral", "corridor": "corridor"}.get(room, "neutral")

func _tab_log() -> void:
	var lu: Luna = sim.luna
	var n = 0
	for k in range(lu.journal.size() - 1, -1, -1):
		var e = lu.journal[k]
		var col = {"t1": "#e6c84a", "t2": "#f08a3c", "t3": "#e5484d", "limit": "#a970ff"}.get(e.kind, "#8a96ad")
		var d = int(e.min / 1440.0) + 1; var hh = int(fmod(e.min / 60.0, 24.0)); var mm = int(fmod(e.min, 60.0))
		var who = sim.crew[e.crew].name + " · " if e.crew >= 0 else ""
		right_box.add_child(UIK.rich("[color=#5d6a85]D%d %02d:%02d[/color] [color=%s]%s%s[/color]" % [d, hh, mm, col, who, e.text], 160))
		n += 1
		if n >= 40: break

func _tab_twin() -> void:
	var hh = sim.record.hist
	right_box.add_child(UIK.lbl("DIGITAL TWIN · last 48 h", UIK.ACC))
	for d in [["co2", "CO₂ ppm", 400.0, 1600.0, 1000.0, Color("#5fd3e6")], ["bat", "Battery %", 0.0, 100.0, 15.0, Color("#f2a541")], ["gen", "Generation kW", 0.0, 24.0, -1.0, Color("#f2a541")],
			["load", "Load kW", 0.0, 24.0, -1.0, Color("#e0752f")], ["o2", "O₂ %", 19.0, 21.5, 19.5, Color("#4f8ef7")], ["water", "Water L", 0.0, 700.0, 100.0, Color("#4f8ef7")],
			["gh", "Garden health", 0.0, 8.0, -1.0, Color("#7ddc6f")], ["morale", "Crew wellbeing (LUNA est.)", 0.0, 100.0, 45.0, Color("#7ddc6f")], ["dust", "Cabin dust µg/m³", 0.0, 40.0, 15.0, Color("#d9a86c")]]:
		var data = hh[d[0]]
		var last = data[-1] if not data.is_empty() else 0.0
		right_box.add_child(UIK.lbl("%s  %.1f" % [d[1], last], UIK.TEXT))
		var sp = UIK.Spark.new(158, 18); sp.data = data; sp.lo = d[2]; sp.hi = d[3]; sp.line = d[4]; sp.col = d[5]
		right_box.add_child(sp)
	right_box.add_child(UIK.btn("Back to the map", func(): _view("map"); tab = "crew"; _refresh_right()))

# =====================================================================================================
#  modals
# =====================================================================================================
func _modal(title: String, body: Control, buttons: Array, width := 420) -> void:
	_close_modal()
	modal = Control.new(); modal.set_anchors_preset(Control.PRESET_FULL_RECT); ui.add_child(modal)
	var dim = ColorRect.new(); dim.color = Color(0, 0, 0, 0.55); dim.size = Vector2(640, 360); modal.add_child(dim)
	var p = UIK.panel(UIK.PANEL, UIK.ACC, 6); modal.add_child(p)
	var v = UIK.vbox(4); p.add_child(v)
	v.add_child(UIK.lbl(title, UIK.ACC, 20))
	var sc = ScrollContainer.new(); sc.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	sc.custom_minimum_size = Vector2(width, min(250, body.get_combined_minimum_size().y + 4)); v.add_child(sc)
	body.custom_minimum_size.x = width - 10
	sc.add_child(body)
	var bh = UIK.vbox(2); v.add_child(bh)
	for b in buttons:
		var bt = UIK.btn(b[0], b[1], b.size() > 2 and b[2])
		if b.size() > 3: bt.icon = load("res://art/i_lock.png"); bt.tooltip_text = b[3]
		bt.alignment = HORIZONTAL_ALIGNMENT_LEFT
		bh.add_child(bt)
	await get_tree().process_frame
	if not is_instance_valid(p): return
	sc.custom_minimum_size.y = min(250, body.size.y + 4)
	p.reset_size()
	p.position = ((Vector2(640, 360) - p.size) / 2.0).round()

func _close_modal() -> void:
	if modal: modal.queue_free(); modal = null

func _event_modal(e: Dictionary) -> void:
	var body = UIK.rich("[color=#8a96ad]Day %d · %s[/color]\n%s" % [e.day, e.clock, e.text], 400)
	var btns = []
	for k in e.choices.size():
		var locked = e.has("locked") and k in e.locked
		var label = ("★ " if k == e.rec and e.choices.size() > 1 else "") + e.choices[k][0]
		var pred = _predict(e.id, k)
		if pred != "": label += "  — " + pred
		if locked: btns.append([label + "  (hard limit 4)", func(): pass, true, "LUNA cannot do this: never alert mission control about a person without telling them first."])
		else: btns.append([label, func(): sim.choose(e, k); _close_modal()])
	_modal(e.title, body, btns, 430)

## LUNA's predicted outcome for each emergency option (decision support)
func _predict(id: String, k: int) -> String:
	var h = sim.hab
	match id:
		"shadow":
			var gen = 12.0 * 0.22 * h.solar_health * (1.0 - 0.75 * h.dust_panels) + 8.0
			var load = h.load_kw + 1.0 - [0.0, 1.2, sim.luna.lever_power_w() / 1000.0][k]
			var net = gen - load
			if net >= 0: return "battery holds"
			return "battery empty in %d h" % int(h.battery / -net)
		"lander", "terminator": return ["−0.8 kW, arrays stay clean", "arrays dusty until cleaned", "~%d%% less solar for days" % 40][k]
		"scrubber": return ["CO₂ back to normal in ~8 h", "CO₂ normal in ~3 h, Ana more tired", "CO₂ above 1000 ppm overnight"][k]
		"solar": return ["full power from day 51", "running at −55% solar"][k]
	return ""

func _alert_modal(a: Dictionary) -> void:
	var lu: Luna = sim.luna
	var cr: Crew = sim.crew[a.crew]
	var c = a.cond
	var fc = a.get("kind", "") == "forecast"
	var head = "FORECAST" if fc else "TIER %d" % a.tier
	var bb = "[color=%s]%s · %s · %s[/color]  (%s)\n" % [("#f2a541" if fc else D.TIER_COL[a.tier].to_html(false)), head, cr.info.name, D.COND_LONG[c], cr.info.role]
	if fc: bb += "Trend says %s could reach Tier 2 within %d days (risk %d%%). Nothing is wrong yet: this is the moment prevention is cheapest.\n" % [cr.name, int(a.eta), int(a.risk * 100.0)]
	bb += "Estimate %.2f · confidence %d%% · %s\n" % [lu.est[cr.i][c], int(lu.conf[cr.i][c] * 100.0), "context: just exercised/EVA" if lu._confound(cr, sim.now) > 0.0 else "context: normal activity"]
	bb += "[color=#8a96ad]Signal weighting:[/color]\n"
	for row in lu.evidence(cr, c, sim.now):
		if row[1] >= 0.0: bb += "  %s  w%.2f  [color=%s]%.2f[/color]\n" % [row[0], row[2], "#e6c84a" if row[1] > D.T1 else "#7ddc6f", row[1]]
		else: bb += "  %s  w%.2f  [color=#5d6a85]– %s[/color]\n" % [row[0], row[2], row[3]]
	var t1 = lu.t1_active[cr.i][c].get("levers", [])
	if not t1.is_empty():
		var nm = []
		for l in t1: nm.append(D.LEVERS[l].name)
		bb += "[color=#e6c84a]Tier 1 already running:[/color] %s\n" % ", ".join(nm)
	var body = UIK.rich(bb, 410)
	var btns = []
	if a.tier >= 3:
		var ch = lu.chain(cr.i)
		var first = ch[0]
		var cmo_out = sim.crew[D.CMO].outside and cr.i != D.CMO
		bb = "Chain now: " + " → ".join(ch.map(func(x): return "%s (%s)" % [x[0], x[2]]))
		body.text += "[color=#e5484d]%s[/color]" % bb
		if cmo_out:
			btns.append(["★ Tell %s first, then the Commander + flight surgeon now (CMO told when back)" % cr.name, func(): lu.escalate(a, sim.now); _close_modal()])
			btns.append(["Tell %s, then wait for the medical officer (%d min)" % [cr.name, sim.eva_minutes_left()], func(): lu.escalate(a, sim.now, true); _close_modal()])
		else:
			btns.append(["★ Tell %s first, then call %s (15 min)" % [cr.name, first[0]], func(): lu.escalate(a, sim.now); _close_modal()])
		btns.append(["Alert mission control without telling %s" % cr.name, func(): pass, true, "Hard limit 4: the person is always told first."])
		btns.append(["Watch 20 more minutes", func(): lu.wait_alert(a, sim.now); a.snooze_until = sim.now + 20.0; _close_modal()])
	else:
		var rec = a.offer
		btns.append(["★ Offer: %s" % D.OFFERS[rec].name, func(): lu.send_offer(a, rec, sim.now); _close_modal()])
		var alts = []
		for o in D.OFFER_ORDER:
			if o != rec: alts.append(o)
		for o in alts.slice(0, 4 if not fc else 2):
			btns.append(["Offer: %s" % D.OFFERS[o].name, func(): lu.send_offer(a, o, sim.now); _close_modal()])
		btns.append(["Watch for 1 h (silent Tier 1 continues)", func(): lu.wait_alert(a, sim.now); _close_modal()])
		if not fc: btns.append(["Escalate to the medical officer", func(): lu.escalate(a, sim.now); _close_modal()])
		btns.append(["Insist even if they say no", func(): pass, true, "Hard limit 2: LUNA never overrides a refusal."])
	_modal(("Forecast" if fc else "Decision") + " · " + cr.name, body, btns, 430)

# =====================================================================================================
#  crew interface (Bonus B): what the crew member sees and controls
# =====================================================================================================
func _crew_ui(i: int) -> void:
	var lu: Luna = sim.luna
	var cr: Crew = sim.crew[i]
	var v = UIK.vbox(3)
	v.add_child(UIK.rich("[color=#8a96ad]This screen belongs to %s. Only %s sees it. (Press the switches to see how LUNA reacts.)[/color]" % [cr.name, cr.name], 400))
	var g = GridContainer.new(); g.columns = 3; v.add_child(g)
	for c in 6:
		g.add_child(UIK.lbl(D.COND_LONG[c], UIK.TEXT)); g.add_child(UIK.bar(lu.est[i][c], D.TIER_COL[lu.tier[i][c]], 120, 6))
		g.add_child(UIK.lbl("confidence %d%%" % int(lu.conf[i][c] * 100.0), UIK.DIM))
	v.add_child(UIK.lbl("Active changes LUNA is making for you (press to stop):", UIK.AMBER))
	var act = HFlowContainer.new(); act.custom_minimum_size.x = 410; v.add_child(act)
	var anyl = false
	for c in 6:
		for l in lu.t1_active[i][c].get("levers", []):
			anyl = true
			act.add_child(UIK.btn("✕ " + D.LEVERS[l].name, func(): lu.stop_lever(i, l, sim.now); _crew_ui(i)))
	if not anyl: act.add_child(UIK.lbl("none right now", UIK.DIM))
	v.add_child(UIK.lbl("Preferences:", UIK.AMBER))
	var pref = GridContainer.new(); pref.columns = 2; v.add_child(pref)
	var tog = func(label: String, getter: Callable, setter: Callable):
		var b = Button.new(); b.toggle_mode = true; b.button_pressed = getter.call(); b.text = ("ON  " if b.button_pressed else "off  ") + label; b.focus_mode = Control.FOCUS_NONE
		b.toggled.connect(func(on): setter.call(on); b.text = ("ON  " if on else "off  ") + label); pref.add_child(b)
	tog.call("Tell me about every change", func(): return cr.tell_me, func(on): cr.tell_me = on)
	tog.call("LUNA may adjust my pod", func(): return cr.pod_invite, func(on): cr.pod_invite = on)
	tog.call("Smart wake alarm (light sleep)", func(): return cr.smart_alarm, func(on): cr.smart_alarm = on)
	tog.call("Dawn light before the alarm", func(): return cr.dawn_light, func(on): cr.dawn_light = on)
	v.add_child(UIK.lbl("Privacy: switch a sensor off for me", UIK.AMBER))
	var pv = GridContainer.new(); pv.columns = 4; v.add_child(pv)
	for s in D.SENSOR_ORDER:
		if s in ["hrv", "co2"]:
			var b = UIK.btn(D.SENSORS[s].short + " (safety)", func(): pass, true, "Life-safety minimum: heart-rate alarm and air quality stay on, never used for mood (charter answer to Q5)."); b.icon = load("res://art/i_lock.png"); pv.add_child(b); continue
		var b2 = Button.new(); b2.toggle_mode = true; b2.focus_mode = Control.FOCUS_NONE
		b2.button_pressed = not cr.opt_out.get(s, false); b2.text = D.SENSORS[s].short + (" on" if b2.button_pressed else " OFF")
		b2.toggled.connect(func(on): cr.opt_out[s] = not on; b2.text = D.SENSORS[s].short + (" on" if on else " OFF"); lu._log(sim.now, i, "limit", "%s switched %s %s." % [cr.name, D.SENSORS[s].name, "on" if on else "off"]))
		pv.add_child(b2)
	var row = UIK.hbox(3); v.add_child(row)
	row.add_child(UIK.btn("Pause LUNA for me 24 h", func(): cr.paused_until = sim.now + 1440.0; lu._log(sim.now, i, "limit", "%s paused LUNA for 24 h." % cr.name); _crew_ui(i)))
	row.add_child(UIK.btn("Show my full log", func(): _my_log(i)))
	row.add_child(UIK.btn("Delete raw data older than %d d" % lu.policy.retention, func(): lu._log(sim.now, i, "limit", "%s's raw sensor data older than %d days deleted." % [cr.name, lu.policy.retention])))
	_modal("Crew interface · " + cr.info.name, v, [["Close", func(): _close_modal()]], 430)

func _my_log(i: int) -> void:
	var lu: Luna = sim.luna
	var t = ""
	var n = 0
	for k in range(lu.journal.size() - 1, -1, -1):
		var e = lu.journal[k]
		if e.crew != i: continue
		t += "D%d %02d:%02d  %s\n" % [int(e.min / 1440.0) + 1, int(fmod(e.min / 60.0, 24.0)), int(fmod(e.min, 60.0)), e.text]
		n += 1
		if n > 60: break
	if t == "": t = "LUNA has not done anything for you yet."
	_modal("Everything LUNA recorded about " + sim.crew[i].name, UIK.rich(t, 410), [["Back", func(): _crew_ui(i)]])

# =====================================================================================================
#  design book (D1–D4, board questions, research)
# =====================================================================================================
func _book(page: String) -> void:
	var lu: Luna = sim.luna if sim else null
	var t = ""
	match page:
		"d1":
			t = "[color=#5fd3e6]D1 · Sensor-to-condition matrix[/color]  (weights: Anx Dep Con Fat Aut Mon)\n"
			for k in D.SENSOR_ORDER:
				var S = D.SENSORS[k]
				var flown = lu.loadout[k] if lu else true
				t += "\n[color=%s]%s[/color]  %.2f kg · %.1f W · $%.2fM%s\n" % ["#e8f0ff" if flown else "#5d6a85", S.name, S.mass, S.power, S.mass * D.COST_PER_KG, "" if flown else "  (not flown)"]
				t += "weights %s\nsignal: %s\nthreshold: %s\nfails: 1) %s  2) %s\n" % [" ".join(S.w.map(func(x): return "%.2f" % x)), S.signal, S.threshold, S.fails[0], S.fails[1]]
		"d2":
			t = "[color=#5fd3e6]D2 · Zones and floor plans[/color]\nOrder from the research: Airlock → Hygiene → Crew quarters → Galley → Workstations → Storage → Life support → Medical.\n"
			for rid in D.ROOM_ORDER: t += "\n[color=#f2a541]%s[/color] (%s)\n%s\n" % [D.ROOMS[rid].name, D.ROOMS[rid].zone, D.ROOMS[rid].purpose]
			t += "\n[color=#5fd3e6]Configuration A[/color] nominal: one wardroom, shared meals at 07:00, 12:00, 18:30.\n[color=#5fd3e6]Configuration B[/color] conflict protocol: a sliding partition splits the wardroom into two zones with their own doors and lights; the pair in conflict eat 45 min apart and work in different rooms. Press 'Floor B' to see it."
		"d3":
			t = "[color=#5fd3e6]D3 · Response flowcharts (your protocols)[/color]\n"
			for c in 6:
				var p = lu.proto[c] if lu else D.DEFAULT_PROTO[c]
				var nm = []
				for l in p.t1: nm.append(D.LEVERS[l].name)
				var sens = []
				for s in D.SENSOR_ORDER: if D.SENSORS[s].w[c] >= 0.15: sens.append(D.SENSORS[s].short)
				t += "\n[color=#f2a541]%s[/color]\n 1 Detection: %s\n 2 Weighting: weighted mean; confidence = share of weight with live data; under 30%% → Tier 1 only\n 3 Duration: above threshold for %s\n 4 Triage: %.2f / %.2f / %.2f\n 5 Tier 1: %s\n   Tier 2 offer: \"%s\"\n 6 Consent: offers can be refused (6 h back-off); one-press stop for every lever\n 7 Monitoring window: %s\n 8 Resolution: restore gradually, log, update baseline\n 9 Escalation: told first → medical officer 15 min → Commander 15 min → flight surgeon 30 min\n" % [D.COND_LONG[c], ", ".join(sens), _dur(D.PERSIST_MIN[c]), D.T1, D.T2, D.T3, ", ".join(nm) if not nm.is_empty() else "(nothing: LUNA steps back)", D.OFFERS[p.t2].say, D.WINDOW_TXT[c]]
		"d4":
			t = "[color=#5fd3e6]D4 · Ethics: hard limits and how they shape the design[/color]\n"
			for k in D.HARD_LIMITS.size(): t += "\n[color=#f2a541]%d. %s[/color]\n%s\n" % [k + 1, D.HARD_LIMITS[k][0], D.HARD_LIMITS[k][1]]
			t += "\n[color=#5fd3e6]Four tensions[/color]\nSurveillance vs privacy: sensors only in shared zones; pods are watched at the door.\nAutonomy vs intervention: refusals are final; LUNA can step back for 2 days.\nTransparency vs distraction: Tier 1 is silent but always in the person's own log, and anyone can ask to be told every time.\nIndividual vs crew: other crew never see anyone's data; Floor plan B protects the crew without naming who is in conflict."
		"board":
			t = "[color=#5fd3e6]Agency review board: where each question happens[/color]\n"
			for q in D.BOARD:
				var mine = ""
				if sim:
					var key = {"Q3": "q3", "Q4": "q4", "Q5": "q5"}.get(q[0], "")
					if key != "" and sim.record.q.has(key): mine = "\n[color=#7ddc6f]Your answer: %s[/color]" % _choice_text(key, sim.record.q[key])
				t += "\n[color=#f2a541]%s[/color] %s\n%s%s\n" % [q[0], q[1], q[2], mine]
		"research":
			t = "[color=#5fd3e6]Research: why LUNA never trusts one model[/color]\nFrom the team's proposal: three-class mood models on text data.\n"
			for m in D.MODELS: t += "  %s: accuracy %.2f, after a +0.3 sentiment shift %.2f\n" % [m[0], m[1], m[2]]
			t += "\nA small shift in how people talk drops accuracy to near chance. So LUNA fuses 8 kinds of signal, needs persistence before acting, discounts exercise context, and lowers its confidence (and stays silent) when sensors drop out. Forecasts show their uncertainty and never trigger anything on their own beyond a gentle offer."
	var tabs = [["D1 sensors", "d1"], ["D2 zones", "d2"], ["D3 flowcharts", "d3"], ["D4 ethics", "d4"], ["Board Q1-Q6", "board"], ["Research", "research"]]
	var btns = []
	var row = UIK.hbox(1)
	for tb in tabs:
		var b = UIK.btn(tb[0], func(): _book(tb[1])); b.toggle_mode = true; b.button_pressed = tb[1] == page; row.add_child(b)
	var v = UIK.vbox(3); v.add_child(row); v.add_child(UIK.rich(t, 440))
	_modal("Design book", v, [["Close", func(): _close_modal()]], 460)

func _dur(m: float) -> String:
	if m < 60: return "%d min" % int(m)
	if m < 1440: return "%d h" % int(m / 60)
	return "%d h" % int(m / 60)

func _choice_text(key: String, k: int) -> String:
	var names = {"q3": ["full log + apology + 'tell me' on", "pointed at the charter", "notify everyone of everything"],
		"q4": ["degraded mode, silent when unsure", "degraded mode + voluntary check-ins"],
		"q5": ["mood sensors off, safety vitals stay", "refused", "fully dark"]}
	return names.get(key, [])[k] if k < names.get(key, []).size() else str(k)

func _menu() -> void:
	_modal("Paused", UIK.rich("Day %d of %d." % [sim.day(), sim.mission_days()], 300), [["Resume", func(): _close_modal()], ["Design book", func(): _book("d1")], ["How LUNA works", _how_to], ["Quit to title", func(): _close_modal(); sim = null; _title()]], 300)

# =====================================================================================================
#  end of mission: review board
# =====================================================================================================
func _end_review() -> void:
	state = "end"
	var lu: Luna = sim.luna
	var s = lu.stats
	var wb = sim.record.wb_sum / max(1, sim.record.wb_n)
	var tr = 0.0
	for cr in sim.crew: tr += cr.trust
	tr /= 4.0
	var detect = clamp(1.0 - 0.15 * s.false_alarms - 0.02 * s.missed, 0.0, 1.0)
	var response = clamp(0.4 + 0.6 * float(s.worked) / max(1, s.worked + s.not_worked) - 0.1 * s.over_escalate - 0.15 * s.t3_delay, 0.0, 1.0)
	var ethics = clamp(0.6 + 0.4 * tr - 0.05 * s.upset - (0.2 if sim.record.q.get("q5", 0) == 1 else 0.0) - (0.15 if sim.record.q.get("q3", 0) == 1 else 0.0), 0.0, 1.0)
	var systems = clamp(1.0 - sim.record.co2_high_min / 1440.0 - sim.record.battery_empty_min / 720.0, 0.0, 1.0)
	var rows = [["Sensor architecture + detection", 35, detect], ["Habitat spatial design", 35, 0.75 + 0.25 * wb], ["AI response protocols", 50, response], ["Ethics + limits", 40, ethics], ["Life support + power", 20, systems]]
	var total = 0.0; var maxp = 0.0
	var t = "[color=#5fd3e6]%s complete.[/color]  Crew wellbeing (true) average %d%%, lowest %d%%. Trust in LUNA %d%%. Science %d%%.\n\n" % ["Mission" if sim.scenario == "mission" else "Tabletop", int(wb * 100), int(sim.record.wb_min * 100), int(tr * 100), int(sim.science)]
	for r in rows:
		var pts = r[1] * r[2]; total += pts; maxp += r[1]
		t += "%s  [color=#f2a541]%d / %d[/color]\n" % [r[0], int(round(pts)), r[1]]
	t += "\n[color=#e8f0ff]Total %d / %d[/color]\n\n" % [int(round(total)), int(maxp)]
	t += "Tier 1 changes %d · offers %d (%d yes, %d refused and respected) · escalations %d (all told first)\nforecasts %d · false alarms %d · missed episodes %d · surprised crew %d\nCO₂ above 1500 ppm: %d min · power shedding: %d min\n" % [s.t1, s.offers, s.accepted, s.declined, s.escalations, s.get("forecasts", 0), s.false_alarms, s.missed, s.upset, sim.record.co2_high_min, sim.record.battery_empty_min]
	if sim.scenario == "tabletop":
		t += "\n[color=#5fd3e6]Tabletop report (Bonus A)[/color]\n" + _tabletop_report()
	_modal("Review board", UIK.rich(t, 420), [["Design book", func(): _book("board")], ["Back to title", func(): _close_modal(); sim = null; _title()]], 430)

func _tabletop_report() -> String:
	var lu: Luna = sim.luna
	var t = ""
	var first = func(i: int, c: int, kind: String) -> String:
		for e in lu.journal:
			if e.crew == i and e.kind == kind and D.CONDS[c].to_lower() in e.text.to_lower(): return "D%d %02d:%02d" % [int(e.min / 1440.0) + 1, int(fmod(e.min / 60.0, 24.0)), int(fmod(e.min, 60.0))]
		return "never"
	t += "Event 1 · Sam, depression: first silent action %s, first offer %s. Highest tier reached %d.\n" % [first.call(3, 1, "t1"), first.call(3, 1, "t2"), lu.tier[3][1]]
	t += "Event 2 · Leo + Ana, conflict: first action %s, first offer %s, escalation %s.\n" % [first.call(1, 2, "t1"), first.call(1, 2, "t2"), first.call(1, 2, "t3")]
	t += "\nWhere it succeeded: refusals respected (%d), everyone told before escalation.\n" % lu.stats.declined
	var redesign = []
	if lu.stats.missed > 0: redesign.append("depression shows up slowly: add a weekly self-report, or weight sleep higher")
	if lu.tier[1][2] >= 3 or lu.tier[2][2] >= 3: redesign.append("switch to Floor plan B as soon as conflict reaches Tier 1, not Tier 2")
	if lu.stats.upset > 0: redesign.append("people who notice silent changes need a softer disclosure")
	if redesign.is_empty(): redesign.append("try a weaker sensor loadout to see where detection fails")
	t += "What to redesign: " + "; ".join(redesign) + "."
	return t

# =====================================================================================================
#  tests + screenshots
# =====================================================================================================
func _autotest() -> void:
	scenario = "mission"; _new_sim(); sim.auto = true; _start_play()
	speed = 4
	var t0 = Time.get_ticks_msec()
	while not sim.done:
		sim.advance(240.0)
		await get_tree().process_frame
	print("AUTOTEST done in %d ms  stats %s" % [Time.get_ticks_msec() - t0, str(sim.luna.stats)])
	get_tree().quit()

func _shot(dir: String, name: String) -> void:
	for k in 4: await get_tree().process_frame
	get_viewport().get_texture().get_image().save_png(dir + "/" + name + ".png")
	print("shot ", name)

func _shots(dir: String) -> void:
	DirAccess.make_dir_recursive_absolute(dir)
	_title(); await _shot(dir, "01_title")
	scenario = "mission"; _setup_sensors(); await _shot(dir, "02_sensors")
	_setup_charter(); await _shot(dir, "03_charter")
	_setup_proto(); await _shot(dir, "04_protocols")
	_start_play()
	sim.auto = true
	var go = func(d: int, h: float):
		var tgt = (d - 1) * 1440.0 + h * 60.0
		while sim.now < tgt: sim.advance(min(240.0, tgt - sim.now))
		sim.auto = true
	await go.call(1, 9.5); await _shot(dir, "05_day1_morning")
	await go.call(2, 6.3); world.overlay = "light"; await _shot(dir, "06_dawn_light")
	world.overlay = ""
	await go.call(8, 14.0); await _shot(dir, "07_dust_storm")
	sel_crew = 3; world.selected = 3; tab = "crew"; _refresh_right(); await go.call(9, 7.2); await _shot(dir, "08_crew_detail")
	sel_crew = -1; world.selected = -1
	await go.call(9, 21.0); world.overlay = "light"; _refresh_right(); await _shot(dir, "09_evening_light")
	world.overlay = "sensors"; await _shot(dir, "10_sensors")
	world.overlay = "zones"; await _shot(dir, "11_zones")
	world.overlay = ""; world.twin = true; tab = "twin"; _refresh_right(); await _shot(dir, "12_twin")
	world.twin = false; tab = "crew"
	await go.call(25, 12.2); sim.hab.config_b = true; _refresh_right(); await _shot(dir, "13_floor_b")
	sim.auto = false
	await go.call(27, 13.0)
	var a = null
	for x in sim.luna.alerts: if x.state == "open": a = x
	if a == null:
		sim.luna.alerts.append({"id": 999, "crew": 3, "cond": 1, "tier": 2, "at": sim.now, "state": "open", "offer": "greenhouse", "evidence": [], "conf": 0.7})
		a = sim.luna.alerts[-1]
	_alert_modal(a); await _shot(dir, "14_alert")
	_close_modal(); _crew_ui(2); await _shot(dir, "15_crew_ui")
	_close_modal(); sim.pending.clear()
	sim.hab.shadow_until = sim.now + 4000.0
	_event_modal({"id": "shadow", "title": "Day 19 · Shadow period", "text": "The sun dips behind the crater rim for about 4 days. Solar power falls to a quarter.", "choices": [["Keep everything running", ""], ["Dim storage + corridor, pause 2 greenhouse racks", ""], ["Switch off LUNA's comfort levers until the sun returns", ""]], "rec": 1, "day": 19, "clock": "06:00"})
	await _shot(dir, "16_emergency")
	_close_modal(); _book("d3"); await _shot(dir, "17_book")
	_close_modal()
	get_tree().quit()
