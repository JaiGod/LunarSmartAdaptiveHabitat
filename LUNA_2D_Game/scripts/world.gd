extends Node2D
## Draws the habitat: floors, walls, furniture, crew, the lighting simulation, dust, overlays, digital twin.

signal picked_crew(i)
signal picked_room(rid)

const T = 16
var sim: Sim
var tex = {}
var light_layer: Node2D
var glow_layer: Node2D
var fx_layer: Node2D
var glow_tex: GradientTexture2D
var overlay = ""          # "", "sensors", "zones", "light"
var twin = false
var selected = -1
var sel_room = ""
var hover_cell = Vector2i(-1, -1)
var t = 0.0
var dust = []             # outside dust particles
var rng = RandomNumberGenerator.new()
var font: Font

func _ready() -> void:
	for f in DirAccess.get_files_at("res://art"):
		if f.ends_with(".png"): tex[f.get_basename()] = load("res://art/" + f)
	for f in DirAccess.get_files_at("res://art"):   # exported builds list .import files
		if f.ends_with(".png.import"):
			var n = f.replace(".png.import", "")
			if not tex.has(n): tex[n] = load("res://art/" + n + ".png")
	light_layer = Node2D.new(); var m1 = CanvasItemMaterial.new(); m1.blend_mode = CanvasItemMaterial.BLEND_MODE_MUL; light_layer.material = m1; add_child(light_layer)
	glow_layer = Node2D.new(); var m2 = CanvasItemMaterial.new(); m2.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD; glow_layer.material = m2; add_child(glow_layer)
	fx_layer = Node2D.new(); add_child(fx_layer)
	light_layer.draw.connect(_draw_light); glow_layer.draw.connect(_draw_glow); fx_layer.draw.connect(_draw_fx)
	var g = Gradient.new(); g.set_color(0, Color(1, 1, 1, 1)); g.set_color(1, Color(1, 1, 1, 0))
	glow_tex = GradientTexture2D.new(); glow_tex.gradient = g; glow_tex.fill = GradientTexture2D.FILL_RADIAL
	glow_tex.fill_from = Vector2(0.5, 0.5); glow_tex.fill_to = Vector2(1.0, 0.5); glow_tex.width = 64; glow_tex.height = 64
	rng.seed = 5
	for k in 260: dust.append(Vector3(rng.randf() * 464.0, rng.randf() * 304.0, rng.randf()))

func _process(delta: float) -> void:
	t += delta
	queue_redraw(); light_layer.queue_redraw(); glow_layer.queue_redraw(); fx_layer.queue_redraw()
	light_layer.visible = not twin; glow_layer.visible = not twin

# =====================================================================================================
#  base layer
# =====================================================================================================
func _draw() -> void:
	if sim == null: return
	if twin: _draw_twin(); return
	# outside: regolith, craters, solar arrays, antenna
	for y in D.MAP_H:
		for x in D.MAP_W:
			if not sim.hab.region.has(Vector2i(x, y)) or Vector2i(x, y) == Vector2i(0, 4):
				draw_texture(tex["regolith%d" % ((x * 7 + y * 13) % 3)], Vector2(x * T, y * T))
	draw_texture(tex.crater, Vector2(2, 290)); draw_texture(tex.crater, Vector2(300, -2)); draw_texture(tex.crater, Vector2(450, 150))
	# floors
	for c in sim.hab.region:
		if c == Vector2i(0, 4): continue
		var r = sim.hab.region[c]
		var fl = "floor_quarters" if r.begins_with("pod") or r == "qhall" else D.ROOMS[r].floor
		draw_texture(tex[fl], Vector2(c) * T)
	_draw_walls()
	# furniture + crew, sorted by their bottom edge
	var items = []
	_furniture(items)
	for cr in sim.crew:
		if cr.outside or cr.asleep: continue
		items.append([_dp(cr).y + 6.0, "crew", cr])
	items.sort_custom(func(a, b): return a[0] < b[0])
	for it in items:
		if it[1] == "crew": _draw_crew(it[2])
		else: draw_texture(tex[it[1]], it[2])
	# solar arrays + antenna on the surface (bottom edge), drawn over the hull rim
	var dusty = sim.hab.dust_panels
	for k in 5:
		var p = Vector2(40 + k * 56, 286)
		draw_texture(tex.solar, p)
		if dusty > 0.02: draw_rect(Rect2(p + Vector2(1, 1), Vector2(30, 14)), Color(0.55, 0.5, 0.42, 0.75 * dusty))
		if sim.hab.solar_health < 0.9 and k == 2: draw_line(p + Vector2(4, 3), p + Vector2(20, 13), Color(0.9, 0.9, 0.9), 1.0)
	draw_texture(tex.antenna, Vector2(440, -4))

func _draw_walls() -> void:
	var dark = Color("#151a28"); var rim = Color("#7b879e"); var mid = Color("#454f68")
	var links = {}
	for d in D.DOORS: links[[d[0], d[1]]] = true; links[[d[1], d[0]]] = true
	for i in 4:
		var pd = D.pod_door(i); links[[pd[0], pd[1]]] = true; links[[pd[1], pd[0]]] = true
	for c in sim.hab.region:
		if c == Vector2i(0, 4): continue
		for dir in [Vector2i(1, 0), Vector2i(0, 1), Vector2i(-1, 0), Vector2i(0, -1)]:
			var n = c + dir
			var inside = sim.hab.region.has(n) and n != Vector2i(0, 4)
			var same = inside and sim.hab.region[n] == sim.hab.region[c]
			if same: continue
			var door = links.has([c, n])
			var p0 = Vector2(c) * T
			if not inside:   # outer hull, thick
				var r: Rect2
				match dir:
					Vector2i(1, 0): r = Rect2(p0 + Vector2(T - 2, -1), Vector2(4, T + 2))
					Vector2i(-1, 0): r = Rect2(p0 + Vector2(-2, -1), Vector2(4, T + 2))
					Vector2i(0, 1): r = Rect2(p0 + Vector2(-1, T - 2), Vector2(T + 2, 4))
					_: r = Rect2(p0 + Vector2(-1, -2), Vector2(T + 2, 4))
				if door:
					draw_rect(r, Color("#e0752f")); continue
				draw_rect(r, mid); draw_rect(r.grow(-1), rim if dir.y < 0 else dark)
				continue
			if dir.x < 0 or dir.y < 0: continue      # draw each internal edge once
			if door:
				var open = false
				for cr in sim.crew:
					if not cr.outside and (cr.cell == c or cr.cell == n or (cr.walking() and cr.pos.distance_to(Vector2(c) * T + Vector2(8, 8) + Vector2(dir) * 8.0) < 14.0)): open = true
				var col = Color("#e6c84a") if not open else Color(0, 0, 0, 0)
				if dir.x != 0:
					draw_rect(Rect2(p0 + Vector2(T - 1, 0), Vector2(2, 2)), rim); draw_rect(Rect2(p0 + Vector2(T - 1, T - 2), Vector2(2, 2)), rim)
					if not open: draw_rect(Rect2(p0 + Vector2(T - 1, 2), Vector2(2, T - 4)), col.darkened(0.3))
				else:
					draw_rect(Rect2(p0 + Vector2(0, T - 1), Vector2(2, 2)), rim); draw_rect(Rect2(p0 + Vector2(T - 2, T - 1), Vector2(2, 2)), rim)
					if not open: draw_rect(Rect2(p0 + Vector2(2, T - 1), Vector2(T - 4, 2)), col.darkened(0.3))
				continue
			if dir.x != 0: draw_rect(Rect2(p0 + Vector2(T - 1, -1), Vector2(2, T + 2)), dark); draw_rect(Rect2(p0 + Vector2(T, 0), Vector2(1, T)), mid)
			else: draw_rect(Rect2(p0 + Vector2(-1, T - 1), Vector2(T + 2, 2)), dark); draw_rect(Rect2(p0 + Vector2(0, T), Vector2(T, 1)), mid)
	# config B partition
	if sim.hab.config_b:
		draw_texture(tex.partition, Vector2(19 * T - 4, 1 * T - 2)); draw_texture(tex.partition, Vector2(19 * T - 4, 1 * T + 46))
		draw_texture_rect_region(tex.partition, Rect2(19 * T - 4, 1 * T + 94, 8, 18), Rect2(0, 0, 8, 18))

func _furniture(items: Array) -> void:
	var add = func(name: String, px: Vector2): items.append([px.y + tex[name].get_height(), name, px])
	# airlock + hygiene
	add.call("suit", Vector2(1 * T, 1 * T - 6)); add.call("suit", Vector2(3 * T, 1 * T - 6))
	add.call("hatch", Vector2(0 * T + 9, 4 * T))
	add.call("shower", Vector2(4 * T, 1 * T)); add.call("toilet", Vector2(6 * T, 1 * T))
	# pods
	for i in 4:
		var cr: Crew = sim.crew[i]
		add.call(("bed%d_sleep" if cr.asleep else "bed%d") % i, Vector2((7 + 2 * i) * T + 1, 1 * T + 2))
		var at_desk = cr.cell == D.pod_spot(i)[0] and not cr.walking() and not cr.asleep
		add.call("desk" if at_desk else "desk_off", Vector2((8 + 2 * i) * T, 1 * T))
		add.call("plant_pot", Vector2((8 + 2 * i) * T + 2, 4 * T + 2))
	# wardroom
	add.call("galley", Vector2(15 * T, 1 * T))
	add.call("table", Vector2(17 * T, 3 * T - 2))
	for s in ["meal0", "meal1", "meal2", "meal3"]:
		var c: Vector2i = D.SPOTS[s][0]
		add.call("stool", Vector2(c) * T + Vector2(4, 6))
	add.call("screen", Vector2(19 * T, 1 * T - 4))
	add.call("window", Vector2(21 * T, 1 * T - 4))
	add.call("sofa", Vector2(20 * T, 6 * T + 2))
	# greenhouse
	for k in 8:
		var r = sim.hab.racks[k]
		var cx = [23, 24, 26, 27][k % 4]; var cy = 2 if k < 4 else 5
		var st = sim.hab.rack_stage(r)
		if st >= 0: add.call("rack%d" % st, Vector2(cx * T, cy * T))
	# medical, eclss, storage
	add.call("medbed", Vector2(2 * T + 1, 12 * T)); add.call("monitor", Vector2(3 * T + 2, 12 * T)); add.call("medcab", Vector2(4 * T, 12 * T))
	var blink = int(t * 2.0) % 2
	var broken = sim.hab.equip.scrubber < 0.5
	for x in [6, 7, 9]:
		add.call("eclss_broken" if broken and x == 6 else "eclss%d" % ((blink + x) % 2), Vector2(x * T, 12 * T))
	add.call("tank", Vector2(10 * T, 12 * T))
	add.call("shelf", Vector2(11 * T, 12 * T)); add.call("shelf", Vector2(13 * T, 12 * T))
	add.call("crate", Vector2(11 * T, 15 * T)); add.call("crate", Vector2(11 * T, 16 * T)); add.call("crate", Vector2(13 * T, 16 * T))
	if sim.hab.robot_task.is_empty(): add.call("robot", Vector2(12 * T + 2, 16 * T + 2))
	# work bay
	for x in [15, 17, 19, 21]:
		var on = false
		for cr in sim.crew: if cr.cell == Vector2i(x, 13) and cr.act_id == "work" and not cr.walking(): on = true
		add.call("desk" if on else "desk_off", Vector2(x * T, 12 * T))
	add.call("console", Vector2(17 * T, 17 * T))
	# exercise
	add.call("treadmill", Vector2(23 * T, 13 * T - 2)); add.call("bike", Vector2(25 * T, 13 * T - 4)); add.call("ared", Vector2(27 * T, 13 * T))
	add.call("screen", Vector2(25 * T, 11 * T - 4))
	add.call("lamp", Vector2(22 * T + 2, 16 * T))

## drawn position: between last minute and this minute, so walking is smooth at any speed
func _dp(cr: Crew) -> Vector2:
	if cr.prev.distance_to(cr.pos) > 40.0: return cr.pos
	return cr.prev.lerp(cr.pos, clamp(sim.acc, 0.0, 1.0))

func _draw_crew(cr: Crew) -> void:
	var sheet: Texture2D = tex["crew%d" % cr.i]
	var row = {"down": 0, "up": 1, "side": 2}[cr.face]
	var fr = 0
	if cr.walking(): fr = 1 + int(cr.walk_t * 6.0) % 2
	elif cr.act_id in ["exercise", "offer_ex"]: fr = 1 + int(t * 6.0) % 2
	var p = (_dp(cr) - Vector2(6, 14)).round()
	if cr.act_id in ["exercise", "offer_ex"] and not cr.walking(): p.y += -1 if int(t * 6.0) % 2 == 0 else 0
	var src = Rect2(fr * 12, row * 17, 12, 17)
	if selected == cr.i:
		draw_rect(Rect2(p + Vector2(-1, 15), Vector2(14, 3)), Color(0.95, 0.85, 0.3, 0.9))
	if cr.face == "side" and cr.flip:
		draw_set_transform(p + Vector2(12, 0), 0.0, Vector2(-1, 1))
		draw_texture_rect_region(sheet, Rect2(Vector2.ZERO, Vector2(12, 17)), src)
		draw_set_transform(Vector2.ZERO)
	else:
		draw_texture_rect_region(sheet, Rect2(p, Vector2(12, 17)), src)

# =====================================================================================================
#  lighting simulation (multiply layer): every zone's real CCT + lux
# =====================================================================================================
static func cct_color(k: float) -> Color:
	var stops = [[1800.0, Color(1.0, 0.52, 0.22)], [2400.0, Color(1.0, 0.7, 0.42)], [2900.0, Color(1.0, 0.8, 0.58)], [4000.0, Color(1.0, 0.94, 0.86)], [5200.0, Color(0.96, 0.98, 1.0)], [6500.0, Color(0.82, 0.9, 1.0)]]
	if k <= stops[0][0]: return stops[0][1]
	for s in range(1, stops.size()):
		if k <= stops[s][0]:
			var f = (k - stops[s - 1][0]) / (stops[s][0] - stops[s - 1][0])
			return stops[s - 1][1].lerp(stops[s][1], f)
	return stops[-1][1]

static func lux_bright(lux: float) -> float:
	return clamp(0.2 + 0.8 * log(lux + 1.0) / log(700.0), 0.16, 1.0)

func zone_key_at(c: Vector2i) -> String:
	var r = sim.hab.region.get(c, "")
	if r == "qhall": return "quarters"
	if r == "communal" and sim.hab.config_b: return "communal_" + sim.hab.wardroom_half(c)
	return r

func light_col(key: String) -> Color:
	var L = sim.hab.light.get(key, {"cct": 4000.0, "lux": 400.0, "preset": "neutral"})
	var col = cct_color(L.cct) * lux_bright(L.lux)
	if L.preset == "hue":
		var h = fmod(t * 0.05 + sim.now / 240.0, 1.0)
		col = col.lerp(Color.from_hsv(h, 0.35, lux_bright(L.lux)), 0.6)
	if L.preset == "grow": col = Color(1.0, 0.72, 0.95) * lux_bright(L.lux)
	col.a = 1.0
	return col

func _draw_light() -> void:
	if sim == null or twin: return
	# outside: low sun at the pole; darker in a shadow period, browner in a dust storm
	var sunb = 0.55 + 0.45 * sim.hab.sun
	var out = Color(sunb, sunb, sunb * 1.03)
	if sim.hab.dust_storm_until > sim.now: out = out * Color(0.95, 0.85, 0.7)
	light_layer.draw_rect(Rect2(0, 0, D.MAP_W * T, D.MAP_H * T), out)
	for c in sim.hab.region:
		if c == Vector2i(0, 4): continue
		var key = zone_key_at(c)
		var col: Color
		if key == "corridor":
			# transition corridor: blend the rooms above and below (mode-switching cue)
			var up = zone_key_at(Vector2i(c.x, 7)); var dn = zone_key_at(Vector2i(c.x, 11))
			var f = (c.y - 7.5) / 3.0
			col = light_col(up).lerp(light_col(dn), clamp(f, 0.0, 1.0)).lerp(light_col("corridor"), 0.35)
		else:
			col = light_col(key)
		light_layer.draw_rect(Rect2(Vector2(c) * T, Vector2(T, T)), col)

func _draw_glow() -> void:
	if sim == null or twin: return
	var hab = sim.hab
	var g = func(p: Vector2, r: float, col: Color): glow_layer.draw_texture_rect(glow_tex, Rect2(p - Vector2(r, r), Vector2(r * 2, r * 2)), false, col)
	# grow lights
	if not hab.shed:
		for k in 8:
			if hab.racks[k].built:
				var cx = [23, 24, 26, 27][k % 4]; var cy = 2 if k < 4 else 5
				g.call(Vector2(cx * T + 8, cy * T + 3), 14.0, Color(0.5, 0.12, 0.45, 0.6))
	# pods: dawn simulation and reading light
	for cr in sim.crew:
		var L = hab.light.get("pod%d" % cr.i, {})
		if L.is_empty(): continue
		var p = Vector2((7 + 2 * cr.i) * T + 16, 3 * T)
		if L.preset == "dawn" or (cr.alarm_flash > sim.now - 20.0):
			var k2 = clamp(L.lux / 250.0, 0.0, 1.0)
			g.call(p, 24.0, Color(0.9, 0.5, 0.15, 0.55 * k2))
	# therapy light, screens, exercise energise
	for key in hab.light:
		var L = hab.light[key]
		if L.preset == "therapy":
			var rid = key.split("_")[0]
			if D.ROOMS.has(rid):
				var r = D.ROOMS[rid].rect
				g.call(Vector2(r[0] + r[2] / 2.0, r[1] + r[3] / 2.0) * T, 50.0, Color(0.6, 0.65, 0.75, 0.55))
	g.call(Vector2(20 * T, 1 * T + 2), 12.0, Color(0.2, 0.35, 0.6, 0.5))
	g.call(Vector2(26 * T, 11 * T + 2), 12.0, Color(0.2, 0.35, 0.6, 0.5))
	if hab.equip.scrubber < 0.5 and int(t * 3.0) % 2 == 0: g.call(Vector2(6 * T + 8, 13 * T), 16.0, Color(0.8, 0.1, 0.1, 0.7))
	# alarms
	if hab.flare_until > sim.now and int(t * 2.0) % 2 == 0: g.call(Vector2(3 * T + 8, 14 * T), 40.0, Color(0.7, 0.1, 0.1, 0.4))

# =====================================================================================================
#  effects + overlays
# =====================================================================================================
func _draw_fx() -> void:
	if sim == null: return
	var L = fx_layer
	var hab = sim.hab
	# dust storm: grains drifting in ballistic arcs over the surface strip and the arrays
	if hab.dust_storm_until > sim.now and not twin:
		for d in dust:
			var x = fmod(d.x + t * (12.0 + d.z * 18.0), 464.0)
			var y = d.y + sin(t * 1.5 + d.z * 6.0) * 3.0
			var c = Vector2i(int(x / T), int(y / T))
			if hab.region.has(c) and c != Vector2i(0, 4): continue
			L.draw_rect(Rect2(x, y, 1 + int(d.z * 1.6), 1), Color(0.85, 0.78, 0.62, 0.55 + 0.4 * d.z))
		L.draw_rect(Rect2(0, 0, 464, 304), Color(0.6, 0.5, 0.35, 0.08))
		var lab = "DUST STORM · arrays %d%% covered" % int(hab.dust_panels * 100.0)
		L.draw_rect(Rect2(150, 276, get_meta("font").get_string_size(lab, HORIZONTAL_ALIGNMENT_LEFT, -1, 10).x + 6, 11), Color(0.25, 0.18, 0.08, 0.85))
		L.draw_string(get_meta("font"), Vector2(153, 285), lab, HORIZONTAL_ALIGNMENT_LEFT, -1, 10, Color("#f2c27a"))
	# dust in the airlock after EVAs
	if hab.airlock_dust > 0.05 and not twin:
		for k in int(min(hab.airlock_dust, 3.0) * 8):
			L.draw_rect(Rect2(1 * T + 2 + (k * 13) % 44, 1 * T + 4 + (k * 29) % 100 + sin(t + k) * 2.0, 1, 1), Color(0.75, 0.7, 0.6, 0.8))
	if not twin:
		for cr in sim.crew:
			var bed = Vector2((7 + 2 * cr.i) * T + 8, 1 * T)
			if cr.asleep:
				var z = int(t * 1.5) % 3
				L.draw_string(get_meta("font"), bed + Vector2(6, -1 - z * 2), "z" if z < 2 else "Z", HORIZONTAL_ALIGNMENT_LEFT, -1, 10, Color(0.75, 0.85, 1.0, 0.85))
			if cr.alarm_flash > sim.now - 25.0 and int(t * 4.0) % 2 == 0:
				L.draw_texture(tex.i_bell, bed + Vector2(-4, -4))
			if cr.outside or cr.asleep: continue
			var top = (_dp(cr) - Vector2(0, 18)).round()
			var tr = sim.luna.max_tier(cr.i)
			if tr > 0: L.draw_rect(Rect2(top + Vector2(-2, -2), Vector2(4, 4)), D.TIER_COL[tr])
			if cr.act_id == "offer" or cr.act_data.has("offer"): L.draw_texture(tex.i_trust, top + Vector2(3, -6))
		# EVA marker outside the hatch
		var outs = []
		for cr in sim.crew: if cr.outside: outs.append(cr.name)
		if not outs.is_empty():
			L.draw_rect(Rect2(0, 4 * T - 9, 2 + outs.size() * 18, 8), Color(0, 0, 0, 0.6))
			L.draw_string(get_meta("font"), Vector2(2, 4 * T - 2), "EVA " + "+".join(outs), HORIZONTAL_ALIGNMENT_LEFT, -1, 10, Color("#f2a541"))
		if not hab.robot_task.is_empty():
			var where = {"scrubber": Vector2(6 * T, 14 * T), "dust": Vector2(4 * T, 18 * T), "solar": Vector2(4 * T, 18 * T)}.get(hab.robot_task.what, Vector2(12 * T, 14 * T))
			L.draw_texture(tex.robot, where + Vector2(sin(t * 3.0) * 2.0, 0))
	# selection outline for a room
	if sel_room != "" and D.ROOMS.has(sel_room):
		var r = D.ROOMS[sel_room].rect
		L.draw_rect(Rect2(r[0] * T, r[1] * T, r[2] * T, r[3] * T), Color(0.95, 0.85, 0.3, 0.9), false, 1.0)
	match overlay:
		"sensors": _ov_sensors(L)
		"zones": _ov_zones(L)
		"light": _ov_light(L)

func _ov_sensors(L: Node2D) -> void:
	var f = get_meta("font")
	for rid in D.ROOMS:
		var r = D.ROOMS[rid].rect
		var rect = Rect2(r[0] * T, r[1] * T, r[2] * T, r[3] * T)
		if rid in ["quarters", "hygiene"]:
			for k in range(0, int(rect.size.x + rect.size.y), 6):
				var a = rect.position + Vector2(k, 0); var b = rect.position + Vector2(0, k)
				L.draw_line(a, b, Color(0.65, 0.4, 1.0, 0.25), 1.0)
			var lab = "private: no cameras or mics, door sensor only" if rid == "quarters" else "no sensors"
			L.draw_rect(Rect2(rect.position + Vector2(1, rect.size.y - 12), Vector2(f.get_string_size(lab, HORIZONTAL_ALIGNMENT_LEFT, -1, 10).x + 4, 11)), Color(0, 0, 0, 0.7))
			L.draw_string(f, rect.position + Vector2(3, rect.size.y - 3), lab, HORIZONTAL_ALIGNMENT_LEFT, -1, 10, Color("#c9a6ff"))
			continue
		var icons = []
		if sim.luna.loadout.co2: icons.append("s_co2")
		if Hab.is_shared(rid):
			if sim.luna.loadout.voice: icons.append("s_voice")
			if sim.luna.loadout.facs: icons.append("s_facs")
			if sim.luna.loadout.motion: icons.append("s_motion")
			if sim.luna.loadout.voice or sim.luna.loadout.facs:
				L.draw_circle(rect.get_center(), min(rect.size.x, rect.size.y) * 0.45, Color(0.25, 0.75, 0.8, 0.12))
		for k in icons.size():
			L.draw_rect(Rect2(rect.position + Vector2(2 + k * 10, 2), Vector2(10, 10)), Color(0, 0, 0, 0.6))
			L.draw_texture(tex[icons[k]], rect.position + Vector2(3 + k * 10, 3))
	if sim.luna.loadout.motion:
		for i in 4:
			var pd = D.pod_door(i)[0]
			L.draw_texture(tex.s_motion, Vector2(pd) * T + Vector2(4, 10))

func _ov_zones(L: Node2D) -> void:
	var f = get_meta("font")
	for rid in D.ROOMS:
		var r = D.ROOMS[rid].rect
		var p = Vector2(r[0] * T + 2, r[1] * T + 2)
		var txt = D.ROOMS[rid].zone
		var w = f.get_string_size(txt, HORIZONTAL_ALIGNMENT_LEFT, -1, 10).x
		L.draw_rect(Rect2(p, Vector2(w + 4, 11)), Color(0, 0, 0, 0.7))
		L.draw_string(f, p + Vector2(2, 9), txt, HORIZONTAL_ALIGNMENT_LEFT, -1, 10, Color("#f2a541") if D.ROOMS[rid].shared else Color("#c9a6ff"))

func _ov_light(L: Node2D) -> void:
	var f = get_meta("font")
	var shown = {}
	for c in sim.hab.region:
		var key = zone_key_at(c)
		if key == "" or shown.has(key) or key.begins_with("pod"): continue
		shown[key] = c
	for i in 4: shown["pod%d" % i] = Vector2i(7 + 2 * i, 4)
	for key in shown:
		var Lz = sim.hab.light.get(key, {})
		if Lz.is_empty(): continue
		var c: Vector2i = shown[key]
		var p = Vector2(c) * T + Vector2(1, 1)
		if D.ROOMS.has(key): p = Vector2(D.ROOMS[key].rect[0], D.ROOMS[key].rect[1]) * T + Vector2(2, 14)
		var txt = "%dK\n%s" % [int(round(Lz.cct / 100.0) * 100), _lux_txt(Lz.lux)]
		if key.begins_with("pod"): txt = "%.1fk\n%s" % [Lz.cct / 1000.0, _lux_txt(Lz.lux).replace(" lx", "")]
		if key == "quarters": continue
		var lines = txt.split("\n")
		var w = 0.0
		for ln in lines: w = max(w, f.get_string_size(ln, HORIZONTAL_ALIGNMENT_LEFT, -1, 10).x)
		L.draw_rect(Rect2(p, Vector2(w + 4, 11 * lines.size())), Color(0, 0, 0, 0.72))
		for k in lines.size():
			L.draw_string(f, p + Vector2(2, 9 + 11 * k), lines[k], HORIZONTAL_ALIGNMENT_LEFT, -1, 10, cct_color(Lz.cct))

static func _lux_txt(lux: float) -> String:
	if lux >= 1000.0: return "%dk lx" % int(lux / 1000.0)
	return "%d lx" % int(lux)

# =====================================================================================================
#  digital twin (blueprint of the live systems)
# =====================================================================================================
func _draw_twin() -> void:
	var f = get_meta("font")
	var hab = sim.hab
	draw_rect(Rect2(0, 0, D.MAP_W * T, D.MAP_H * T), Color("#081426"))
	for x in range(0, D.MAP_W * T, 8): draw_line(Vector2(x, 0), Vector2(x, D.MAP_H * T), Color(0.1, 0.25, 0.4, 0.35) if x % 32 else Color(0.15, 0.35, 0.55, 0.5))
	for y in range(0, D.MAP_H * T, 8): draw_line(Vector2(0, y), Vector2(D.MAP_W * T, y), Color(0.1, 0.25, 0.4, 0.35) if y % 32 else Color(0.15, 0.35, 0.55, 0.5))
	var cyan = Color("#5fd3e6")
	for rid in D.ROOMS:
		var r = D.ROOMS[rid].rect
		var rect = Rect2(r[0] * T + 1, r[1] * T + 1, r[2] * T - 2, r[3] * T - 2)
		draw_rect(rect, Color(0.1, 0.3, 0.45, 0.25)); draw_rect(rect, cyan, false, 1.0)
		draw_string(f, rect.position + Vector2(3, 10), D.ROOMS[rid].name.to_upper(), HORIZONTAL_ALIGNMENT_LEFT, rect.size.x - 4, 10, cyan)
	# flows: power (amber) from arrays + fission -> battery (storage) -> loads; air (cyan) from ECLSS; O2 from greenhouse (green)
	var ph = fmod(t * 0.6, 1.0)
	var flow = func(a: Vector2, b: Vector2, col: Color, rate: float):
		draw_line(a, b, Color(col, 0.45), 1.0)
		var n = int(clamp(rate, 1.0, 6.0))
		for k in n:
			var u = fmod(ph + float(k) / n, 1.0)
			draw_rect(Rect2(a.lerp(b, u) - Vector2(1, 1), Vector2(2, 2)), col)
	var bat = Vector2(12 * T + 8, 14 * T)
	flow.call(Vector2(150, 296), bat, Color("#f2a541"), hab.gen_kw / 4.0)
	flow.call(bat, Vector2(8 * T, 14 * T), Color("#f2a541"), 3)
	flow.call(bat, Vector2(18 * T, 14 * T), Color("#f2a541"), 2)
	flow.call(bat, Vector2(25 * T, 4 * T), Color("#f2a541"), hab.built_racks())
	flow.call(Vector2(8 * T, 12 * T), Vector2(8 * T, 9 * T), cyan, 4 * hab.equip.scrubber)
	flow.call(Vector2(8 * T, 9 * T), Vector2(2 * T, 9 * T), cyan, 3); flow.call(Vector2(8 * T, 9 * T), Vector2(27 * T, 9 * T), cyan, 4)
	flow.call(Vector2(25 * T, 6 * T), Vector2(25 * T, 9 * T), Color("#7ddc6f"), hab.built_racks())
	flow.call(Vector2(5 * T, 5 * T), Vector2(8 * T, 13 * T), Color("#4f8ef7"), 2)
	# readouts
	var rd = func(p: Vector2, txt: String, col: Color):
		var w = f.get_string_size(txt, HORIZONTAL_ALIGNMENT_LEFT, -1, 10).x
		draw_rect(Rect2(p, Vector2(w + 4, 11)), Color(0.02, 0.06, 0.12, 0.9)); draw_rect(Rect2(p, Vector2(w + 4, 11)), Color(col, 0.6), false, 1.0)
		draw_string(f, p + Vector2(2, 9), txt, HORIZONTAL_ALIGNMENT_LEFT, -1, 10, col)
	rd.call(Vector2(6 * T + 2, 15 * T + 4), "CO2 %d ppm" % hab.co2, Color("#e5484d") if hab.co2 > 1000 else cyan)
	rd.call(Vector2(6 * T + 2, 16 * T + 6), "O2 %.1f%%" % hab.o2, cyan)
	rd.call(Vector2(11 * T + 1, 16 * T + 6), "BAT %d%%" % int(hab.battery / hab.BATTERY_MAX * 100.0), Color("#f2a541"))
	rd.call(Vector2(30, 274), "SOLAR %.1f kW  FISSION %.0f kW  LOAD %.1f kW" % [12.0 * hab.sun * hab.solar_health * (1.0 - 0.75 * hab.dust_panels), 8.0 if hab.fission_on else 0.0, hab.load_kw], Color("#f2a541"))
	rd.call(Vector2(23 * T + 2, 6 * T + 4), "GH %d racks" % hab.built_racks(), Color("#7ddc6f"))
	rd.call(Vector2(4 * T + 2, 6 * T + 2), "H2O %d L" % hab.water, Color("#4f8ef7"))
	rd.call(Vector2(1 * T + 2, 16 * T + 6), "DUST %d" % hab.dust_air, Color("#d9c7a0") if hab.dust_air < 15 else Color("#e5484d"))
	# crew as dots coloured by wellbeing (the true state is NOT shown: LUNA's estimate)
	for cr in sim.crew:
		if cr.outside: continue
		var sc = 0.0
		for c in 6: sc = max(sc, sim.luna.est[cr.i][c])
		var col = Color("#7ddc6f").lerp(Color("#e5484d"), clamp((sc - 0.2) / 0.6, 0.0, 1.0))
		draw_circle(cr.pos, 4.0, col); draw_arc(cr.pos, 6.0, 0, TAU, 16, Color(col, 0.5), 1.0)
		draw_string(f, cr.pos + Vector2(7, 3), cr.name, HORIZONTAL_ALIGNMENT_LEFT, -1, 10, Color.WHITE)

# =====================================================================================================
#  input
# =====================================================================================================
func _unhandled_input(ev: InputEvent) -> void:
	if sim == null: return
	if ev is InputEventMouseButton and ev.pressed and ev.button_index == MOUSE_BUTTON_LEFT:
		var p = get_local_mouse_position()
		if p.x < 0 or p.y < 0 or p.x >= D.MAP_W * T or p.y >= D.MAP_H * T: return
		var best = -1; var bd = 12.0
		for cr in sim.crew:
			if cr.outside: continue
			var cp = cr.pos if not cr.asleep else Vector2((7 + 2 * cr.i) * T + 8, 2 * T)
			var d = cp.distance_to(p + Vector2(0, 4))
			if d < bd: bd = d; best = cr.i
		if best >= 0:
			selected = best; emit_signal("picked_crew", best); get_viewport().set_input_as_handled(); return
		var c = Vector2i(int(p.x / T), int(p.y / T))
		var rid = sim.hab.room_of_zone(sim.hab.zone_of(c)) if sim.hab.region.has(c) else ""
		if rid != "":
			sel_room = rid; emit_signal("picked_room", rid); get_viewport().set_input_as_handled()
