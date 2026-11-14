class_name Habitat
extends Node3D
## Builds the habitat in 3D from the same floor plan the LUNA engine navigates (D.ROOMS, 1 cell = 1.25 m),
## then keeps it in sync with the simulation: room lighting (LUNA's CCT/lux), doors, crops, partition, robot.

const CELL = 1.25
const ROOM_H = {"airlock": 2.6, "hygiene": 2.5, "qhall": 2.5, "communal": 3.0, "greenhouse": 3.2, "corridor": 2.6,
	"medical": 2.6, "eclss": 2.9, "storage": 2.7, "work": 3.3, "exercise": 3.0, "pod": 2.4}
## wayfinding colour per room (stripes, door signs)
const ROOM_COL = {"airlock": Color("#e0a83a"), "hygiene": Color("#5ab0d6"), "qhall": Color("#9a7fd1"), "pod": Color("#9a7fd1"),
	"communal": Color("#e07b4f"), "greenhouse": Color("#5fb35f"), "corridor": Color("#8a96a8"), "medical": Color("#d9534f"),
	"eclss": Color("#3fa7a0"), "storage": Color("#a08a5a"), "work": Color("#4f8ef7"), "exercise": Color("#e0b23a")}
const ROOM_NAME = {"airlock": "Airlock · dust lock", "hygiene": "Hygiene", "qhall": "Crew quarters", "communal": "Social hub",
	"greenhouse": "Greenhouse", "corridor": "Corridor", "medical": "Medical · storm shelter", "eclss": "Life support",
	"storage": "Robot bay · stores", "work": "Wellness Operations Center", "exercise": "Exercise · Wellness Dock"}
const POD_COL = [Color("#4f8ef7"), Color("#3fbfb0"), Color("#e08a3c"), Color("#6abf5a")]

var sim: VRSim
var region = {}
var doors = []                   # {"node","panel","body","axis","closed_pos","open":0..1,"cells":[a,b]}
var lights = {}                  # light key -> {"omni": [OmniLight3D], "mats": [Material]}
var inters = []                  # interactables {"id","area","label","action","room"}
var screens = []                 # Screen3D nodes
var dyn = {}                     # named dynamic nodes models want to update
var updaters = []                # Callables(dt) called every frame
var static_root: Node3D
var mb = MB.new()
var models: Models
var separate_models = false

static func cpos(c: Vector2i, y := 0.0) -> Vector3: return Vector3((c.x + 0.5) * CELL, y, (c.y + 0.5) * CELL)
static func to_cell(p: Vector3) -> Vector2i: return Vector2i(floori(p.x / CELL), floori(p.z / CELL))

func rkind(r: String) -> String:
	if r.begins_with("pod"): return "pod"
	return r

func rh(r: String) -> float: return ROOM_H.get(rkind(r), 2.6)

## bounding rectangle (in cells) of a region
func region_rect(r: String) -> Rect2i:
	var mn = Vector2i(999, 999); var mx = Vector2i(-1, -1)
	for c in region:
		if region[c] == r:
			mn = Vector2i(min(mn.x, c.x), min(mn.y, c.y)); mx = Vector2i(max(mx.x, c.x), max(mx.y, c.y))
	return Rect2i(mn, mx - mn + Vector2i.ONE)

func world_rect(r: String) -> Rect2:
	var rr = region_rect(r)
	return Rect2(Vector2(rr.position) * CELL, Vector2(rr.size) * CELL)

func light_key_of(r: String) -> String:
	if r == "qhall": return "quarters"
	return r

# =====================================================================================================
#  build
# =====================================================================================================
func build(s: VRSim) -> void:
	sim = s
	region = s.hab.region.duplicate()
	region.erase(Vector2i(0, 4))
	static_root = Node3D.new(); static_root.name = "Static"; add_child(static_root)
	mb.chunk = 6.25
	_floors_and_ceilings()
	_walls_and_doors()
	_room_lights()
	_kit_details()
	models = Models.new(self, separate_models)
	models.build_all()
	_exterior()

func _regions() -> Array:
	var seen = {}
	for c in region: seen[region[c]] = true
	return seen.keys()

func _floors_and_ceilings() -> void:
	for r in _regions():
		var wr = world_rect(r)
		var c3 = Vector3(wr.get_center().x, 0, wr.get_center().y)
		var k = rkind(r)
		var fm: Material
		match k:
			"airlock": fm = Mats.grate()
			"greenhouse": fm = Mats.floor_m(Color(0.78, 0.9, 0.8), "floor_gh")
			"medical": fm = Mats.floor_m(Color(0.9, 0.92, 0.96), "floor_med")
			"pod": fm = Mats.floor_m(Color(0.92, 0.86, 0.8), "floor_pod")
			"communal": fm = Mats.floor_m(Color(0.95, 0.88, 0.8), "floor_hub")
			"exercise": fm = Mats.floor_m(Color(0.7, 0.72, 0.78), "floor_ex")
			_: fm = Mats.floor_m()
		mb.box(c3 + Vector3(0, -0.05, 0), Vector3(wr.size.x, 0.1, wr.size.y), fm, 0.0, true)
		var h = rh(r)
		if k == "greenhouse":
			# biophilic room: a light-pipe dome brings filtered sunlight down from the surface
			var hc = wr.get_center(); var hr = min(wr.size.x, wr.size.y) * 0.44
			var cm = Mats.ceiling().duplicate(); cm.resource_name = "ceiling_gh"
			mb.plane_with_hole(wr, h, hc, hr, cm)
			var dm = Mats.ceiling().duplicate(); dm.resource_name = "dome"; dm.cull_mode = BaseMaterial3D.CULL_DISABLED
			mb.sphere(Vector3(hc.x, h, hc.y), hr, dm, 24, 10, Vector3.ONE, hr * 0.62)
			mb.cyl(Vector3(hc.x, h + hr * 0.62 - 0.05, hc.y), 0.55, 0.06, Mats.light_panel("lp_gh_pipe"))
			lights.get_or_add("greenhouse", {"omni": [], "mats": []}).mats.append(Mats.cache["lp_gh_pipe"])
		else:
			mb.box(c3 + Vector3(0, h + 0.05, 0), Vector3(wr.size.x, 0.1, wr.size.y), Mats.ceiling())
		_light_panels(r, wr, h)
	mb.commit(static_root, "floors_ceilings")

## recessed LED panels: one per ~5 m², colour/brightness follow the room's lighting preset
func _light_panels(r: String, wr: Rect2, h: float) -> void:
	var keys = [light_key_of(r)]
	if r == "communal": keys = ["communal_W", "communal_E"]
	for key in keys:
		if not lights.has(key): lights[key] = {"omni": [], "mats": []}
		var m: StandardMaterial3D
		if lights[key].mats.is_empty() or key == "greenhouse":
			m = Mats.light_panel("lp_" + key) if not Mats.cache.has("lp_" + key) else Mats.cache["lp_" + key]
			if not lights[key].mats.has(m): lights[key].mats.append(m)
		else:
			m = lights[key].mats[0]
		var area = wr
		if r == "communal":
			var half = wr.size.x * 0.5
			area = Rect2(wr.position + Vector2(0 if key == "communal_W" else half, 0), Vector2(half, wr.size.y))
		if r == "greenhouse":
			for zz in [area.position.y + 0.8, area.end.y - 0.8]:
				mb.box(Vector3(area.get_center().x, h - 0.012, zz), Vector3(area.size.x - 1.0, 0.02, 0.25), m)
			continue
		var nx = max(1, int(round(area.size.x / 2.6))); var nz = max(1, int(round(area.size.y / 2.6)))
		if r == "corridor": nx = 12; nz = 1
		for ix in nx:
			for iz in nz:
				var px = area.position.x + area.size.x * (ix + 0.5) / nx
				var pz = area.position.y + area.size.y * (iz + 0.5) / nz
				var sz = Vector3(1.2, 0.02, 0.6) if area.size.x >= area.size.y else Vector3(0.6, 0.02, 1.2)
				mb.box(Vector3(px, h - 0.012, pz), sz, m)
				mb.box(Vector3(px, h - 0.004, pz), sz + Vector3(0.06, 0.0, 0.06), Mats.frame())

func _is_link(a: Vector2i, b: Vector2i) -> bool:
	for d in D.DOORS:
		if (d[0] == a and d[1] == b) or (d[0] == b and d[1] == a): return true
	for i in 4:
		var pd = D.pod_door(i)
		if (pd[0] == a and pd[1] == b) or (pd[0] == b and pd[1] == a): return true
	return false

func _walls_and_doors() -> void:
	var edges = []   # {"o": "v"/"h", "k": line coord (cells), "s": start, "a": region or "", "b": region or "", "door": bool}
	for c in region:
		var ra = region[c]
		# west + north outer walls
		var w = c + Vector2i(-1, 0)
		if not region.has(w): edges.append({"o": "v", "k": c.x, "s": c.y, "a": "", "b": ra, "door": false, "hatch": c == Vector2i(1, 4)})
		var n = c + Vector2i(0, -1)
		if not region.has(n): edges.append({"o": "h", "k": c.y, "s": c.x, "a": "", "b": ra, "door": false})
		# east + south (interior or outer)
		var e = c + Vector2i(1, 0)
		if not region.has(e): edges.append({"o": "v", "k": c.x + 1, "s": c.y, "a": ra, "b": "", "door": false})
		elif region[e] != ra: edges.append({"o": "v", "k": c.x + 1, "s": c.y, "a": ra, "b": region[e], "door": _is_link(c, e)})
		var so = c + Vector2i(0, 1)
		if not region.has(so): edges.append({"o": "h", "k": c.y + 1, "s": c.x, "a": ra, "b": "", "door": false})
		elif region[so] != ra: edges.append({"o": "h", "k": c.y + 1, "s": c.x, "a": ra, "b": region[so], "door": _is_link(c, so)})
	# merge straight runs of plain wall with the same rooms on both sides
	edges.sort_custom(func(p, q):
		if p.o != q.o: return p.o < q.o
		if p.k != q.k: return p.k < q.k
		if p.a != q.a: return p.a < q.a
		if p.b != q.b: return p.b < q.b
		return p.s < q.s)
	var runs = []
	for e in edges:
		if e.door or e.get("hatch", false): runs.append({"o": e.o, "k": e.k, "s": e.s, "len": 1, "a": e.a, "b": e.b, "door": e.door, "hatch": e.get("hatch", false)}); continue
		var last = runs.back() if not runs.is_empty() else null
		if last and not last.door and not last.hatch and last.o == e.o and last.k == e.k and last.a == e.a and last.b == e.b and last.s + last.len == e.s:
			last.len += 1
		else:
			runs.append({"o": e.o, "k": e.k, "s": e.s, "len": 1, "a": e.a, "b": e.b, "door": false, "hatch": false})
	for r in runs: _wall_run(r)
	mb.commit(static_root, "walls")

const WT = 0.12   # wall thickness

func _wall_run(r: Dictionary) -> void:
	var h = max(rh(r.a) if r.a != "" else 0.0, rh(r.b) if r.b != "" else 0.0)
	var L = r.len * CELL
	var along: Vector3; var normal: Vector3; var start: Vector3
	if r.o == "v":
		along = Vector3(0, 0, 1); normal = Vector3(1, 0, 0); start = Vector3(r.k * CELL, 0, r.s * CELL)
	else:
		along = Vector3(1, 0, 0); normal = Vector3(0, 0, 1); start = Vector3(r.s * CELL, 0, r.k * CELL)
	var yaw = 0.0 if r.o == "h" else PI / 2.0
	if r.door:
		_door(r, start, along, normal, h, yaw)
		return
	if r.hatch:
		_hatch_wall(start, along, normal, h, yaw)
		return
	var mid = start + along * L * 0.5
	mb.box(mid + Vector3(0, h * 0.5, 0), Vector3(L, h, WT), Mats.wall(), yaw, true)
	# per-side details: colour stripe at handrail height + dark baseboard
	for side in [[-1.0, r.a], [1.0, r.b]]:
		if side[1] == "": continue
		var off = normal * side[0] * (WT * 0.5 + 0.006)
		var col = ROOM_COL.get(rkind(side[1]), Color.GRAY)
		mb.box(mid + off + Vector3(0, 1.16, 0), Vector3(L, 0.05, 0.01), Mats.stripe(col), yaw)
		mb.box(mid + off + Vector3(0, 0.05, 0), Vector3(L, 0.1, 0.012), Mats.dark_metal(), yaw)

func _door(r: Dictionary, start: Vector3, along: Vector3, normal: Vector3, h: float, yaw: float) -> void:
	var L = CELL; var dw = 1.0; var dh = 2.1
	var mid = start + along * L * 0.5
	var jamb = (L - dw) * 0.5
	for sgn in [-1.0, 1.0]:
		mb.box(mid + along * sgn * (dw * 0.5 + jamb * 0.5) + Vector3(0, h * 0.5, 0), Vector3(jamb, h, WT), Mats.wall(), yaw, true)
		mb.box(mid + along * sgn * (dw * 0.5 + 0.02) + Vector3(0, dh * 0.5, 0), Vector3(0.05, dh, WT + 0.04), Mats.frame(), yaw)
	mb.box(mid + Vector3(0, dh + (h - dh) * 0.5, 0), Vector3(dw, h - dh, WT), Mats.wall(), yaw)
	mb.box(mid + Vector3(0, dh + 0.03, 0), Vector3(dw + 0.1, 0.06, WT + 0.04), Mats.frame(), yaw)
	# the sliding panel (its own node so it can move)
	var node = Node3D.new(); node.name = "door_%s_%s" % [r.a, r.b]
	add_child(node)
	var col = Color(0.62, 0.66, 0.71)
	var pod = r.a if r.a.begins_with("pod") else (r.b if r.b.begins_with("pod") else "")
	if pod != "": col = POD_COL[int(pod.substr(3))]
	var pm = StandardMaterial3D.new(); pm.albedo_color = col; pm.roughness = 0.5; pm.resource_name = "door"
	var panel = MeshInstance3D.new(); var bm = BoxMesh.new(); bm.size = Vector3(dw, dh, 0.05); bm.material = pm
	panel.mesh = bm; node.add_child(panel)
	var win = MeshInstance3D.new(); var wm = BoxMesh.new(); wm.size = Vector3(0.12, 0.5, 0.06); wm.material = Mats.glass()
	win.mesh = wm; win.position = Vector3(0, 0.35, 0); panel.add_child(win)
	var body = StaticBody3D.new(); var cs = CollisionShape3D.new(); var bs = BoxShape3D.new(); bs.size = Vector3(dw, dh, 0.08)
	cs.shape = bs; body.add_child(cs); panel.add_child(body)
	node.position = mid + Vector3(0, dh * 0.5, 0)
	node.rotation.y = yaw
	var cells = []
	if r.o == "v": cells = [Vector2i(r.k - 1, r.s), Vector2i(r.k, r.s)]
	else: cells = [Vector2i(r.s, r.k - 1), Vector2i(r.s, r.k)]
	doors.append({"node": node, "panel": panel, "body": cs, "open": 0.0, "cells": cells, "pos": mid})
	# room signs over the door, one on each side
	for side in [[-1.0, r.b], [1.0, r.a]]:
		if side[1] == "": continue
		var lab = Label3D.new()
		var nm = ROOM_NAME.get(rkind(side[1]), side[1])
		if side[1].begins_with("pod"):
			var pi = int(side[1].substr(3))
			nm = "Pod %d · %s" % [pi + 1, D.CREW[pi].short if pi > 0 else "You"]
		lab.text = nm; lab.font_size = 36; lab.pixel_size = 0.004; lab.outline_size = 0
		lab.modulate = Color(0.12, 0.14, 0.18); lab.double_sided = false
		lab.position = mid + normal * (-side[0]) * (WT * 0.5 + 0.02) + Vector3(0, dh + 0.18, 0)
		# Label3D reads from +Z: turn it to face into the room it names
		if r.o == "v": lab.rotation.y = PI / 2.0 if side[0] < 0 else -PI / 2.0
		else: lab.rotation.y = 0.0 if side[0] < 0 else PI
		static_root.add_child(lab)
		var strip = ROOM_COL.get(rkind(side[1]), Color.GRAY)
		mb.box(lab.position + Vector3(0, -0.1, 0) + normal * (-side[0]) * -0.008, Vector3(0.9, 0.025, 0.006) if true else Vector3.ZERO, Mats.stripe(strip), yaw)

## the airlock's outer wall: hatch to the lunar surface (opened only through the EVA procedure)
func _hatch_wall(start: Vector3, along: Vector3, normal: Vector3, h: float, yaw: float) -> void:
	var mid = start + along * CELL * 0.5
	mb.box(mid + Vector3(0, h * 0.5, 0), Vector3(CELL, h, WT * 2.0), Mats.wall(), yaw, true)
	var hb = Basis(Vector3.UP, yaw) * Basis(Vector3.RIGHT, PI / 2.0)
	mb.cyl(mid + Vector3(0.09, 1.1, 0), 0.52, 0.06, Mats.frame(), 24, -1.0, Basis(Vector3.UP, PI / 2.0) * Basis(Vector3.RIGHT, PI / 2.0))
	mb.cyl(mid + Vector3(0.12, 1.1, 0), 0.44, 0.05, Mats.grey_plastic(), 24, -1.0, Basis(Vector3.UP, PI / 2.0) * Basis(Vector3.RIGHT, PI / 2.0))
	mb.cyl(mid + Vector3(0.15, 1.3, 0), 0.08, 0.03, Mats.glass(), 16, -1.0, Basis(Vector3.UP, PI / 2.0) * Basis(Vector3.RIGHT, PI / 2.0))
	mb.box(mid + Vector3(0.17, 1.0, 0), Vector3(0.05, 0.05, 0.4), Mats.caution())          # lever
	mb.cyl(mid + Vector3(0.12, 1.75, 0.45), 0.07, 0.03, Mats.white_plastic(), 16, -1.0, Basis(Vector3.UP, PI / 2.0) * Basis(Vector3.RIGHT, PI / 2.0))  # pressure gauge
	var lab = Label3D.new(); lab.text = "OUTER HATCH\nLunar vacuum beyond\nEVA procedure only"; lab.font_size = 28; lab.pixel_size = 0.004
	lab.modulate = Color(0.75, 0.12, 0.08); lab.position = mid + Vector3(0.14, 1.85, -0.15); lab.rotation.y = PI / 2.0
	static_root.add_child(lab)
	dyn["hatch_pos"] = mid + Vector3(0.4, 0, 0)

func _room_lights() -> void:
	for r in _regions():
		var wr = world_rect(r)
		var h = rh(r)
		var keys = [light_key_of(r)]
		if r == "communal": keys = ["communal_W", "communal_E"]
		for key in keys:
			var pts = []
			if r == "corridor":
				for fx in [0.17, 0.5, 0.83]: pts.append(Vector3(wr.position.x + wr.size.x * fx, h - 0.35, wr.get_center().y))
			elif r == "communal":
				var cx = wr.position.x + wr.size.x * (0.25 if key == "communal_W" else 0.75)
				pts.append(Vector3(cx, h - 0.4, wr.get_center().y))
			else:
				pts.append(Vector3(wr.get_center().x, h - 0.35, wr.get_center().y))
			for p in pts:
				var o = OmniLight3D.new()
				o.position = p
				o.omni_range = max(wr.size.x, wr.size.y) * (0.85 if r != "corridor" else 0.25) + 1.5
				if r == "communal": o.omni_range = 7.5
				o.omni_attenuation = 0.8
				o.light_energy = 1.0
				o.shadow_enabled = false
				add_child(o)
				lights.get_or_add(key, {"omni": [], "mats": []}).omni.append(o)

## cable trays, handrails, sensor modules: only where a real habitat has them
func _kit_details() -> void:
	var cw = world_rect("corridor")
	# corridor: handrails both sides (crew move through here carrying things), cable trays above
	for zz in [cw.position.y + 0.07, cw.end.y - 0.07]:
		var sgn = 1.0 if zz < cw.get_center().y else -1.0
		var z2 = zz + sgn * 0.05
		mb.box(Vector3(cw.get_center().x, 1.0, z2), Vector3(cw.size.x - 0.4, 0.04, 0.04), Mats.handrail())
		for x in range(int(cw.position.x + 1.0), int(cw.end.x), 3):
			mb.box(Vector3(x, 1.0, zz + sgn * 0.025), Vector3(0.03, 0.03, 0.06), Mats.handrail())
		_cable_tray(Vector3(cw.get_center().x, 2.36, zz + sgn * 0.12), cw.size.x - 0.3, 0.0)
	# technical rooms get trays too (they feed the racks)
	for r in ["eclss", "storage", "airlock"]:
		var wr = world_rect(r)
		_cable_tray(Vector3(wr.get_center().x, rh(r) - 0.3, wr.position.y + 0.2), wr.size.x - 0.3, 0.0)
	var wo = world_rect("work")
	_cable_tray(Vector3(wo.get_center().x, rh("work") - 0.35, wo.end.y - 0.2), wo.size.x - 0.3, 0.0)
	# sensor modules: CO2/air in every room; location beacon in shared rooms; NO microphones or cameras in pods/hygiene
	for r in _regions():
		var wr = world_rect(r)
		var h = rh(r)
		var p = Vector3(wr.end.x - 0.07, h - 0.45, wr.position.y + 0.45)
		mb.box(p, Vector3(0.05, 0.12, 0.18), Mats.white_plastic())
		mb.box(p + Vector3(-0.03, 0.03, 0), Vector3(0.01, 0.015, 0.015), Mats.glow(Color(0.2, 0.9, 0.4), 2.0))
		if Hab.is_shared(r):
			mb.box(p + Vector3(0, -0.2, 0), Vector3(0.05, 0.08, 0.08), Mats.grey_plastic())
	mb.commit(static_root, "kit")
	# privacy labels where the brief forbids voice/camera sensors
	for r in _regions():
		if not Hab.is_private(r): continue
		var wr = world_rect(r)
		var lab = Label3D.new()
		lab.text = "No cameras · no microphones in here\nLUNA watches the door, not the room"
		lab.font_size = 22; lab.pixel_size = 0.0035; lab.modulate = Color(0.25, 0.28, 0.34)
		lab.position = Vector3(wr.end.x - 0.075, 1.55, wr.end.y - 0.6); lab.rotation.y = -PI / 2.0
		if r == "hygiene": lab.position = Vector3(wr.position.x + 0.075, 1.55, wr.end.y - 0.6); lab.rotation.y = PI / 2.0
		static_root.add_child(lab)

func _cable_tray(c: Vector3, length: float, yaw: float) -> void:
	mb.box(c, Vector3(length, 0.02, 0.22), Mats.dark_metal(), yaw)
	mb.box(c + Vector3(0, 0.04, 0.1), Vector3(length, 0.08, 0.01), Mats.dark_metal(), yaw)
	mb.box(c + Vector3(0, 0.04, -0.1), Vector3(length, 0.08, 0.01), Mats.dark_metal(), yaw)
	var k = 0
	for col in [Color("#2b6fd6"), Color("#d9d9d9"), Color("#e0a83a"), Color("#3a3a3a")]:
		mb.tube_x(c + Vector3(0, 0.035, -0.07 + k * 0.045), 0.016, length - 0.05, Mats.cable(col), yaw, 6)
		k += 1

# =====================================================================================================
#  interactables + screens (the game decides what they do)
# =====================================================================================================
func add_inter(id: String, pos: Vector3, size: Vector3, label: Variant, action: Callable, room := "") -> Area3D:
	var a = Area3D.new(); a.name = "inter_" + id
	a.collision_layer = 2; a.collision_mask = 0; a.monitoring = false
	var cs = CollisionShape3D.new(); var bs = BoxShape3D.new(); bs.size = size; cs.shape = bs; a.add_child(cs)
	a.position = pos
	add_child(a)
	var it = {"id": id, "area": a, "label": label, "action": action, "room": room}
	a.set_meta("inter", it)
	inters.append(it)
	return a

func add_screen(id: String, pos: Vector3, yaw: float, size_m: Vector2, px: Vector2i, ui: Control, pitch := 0.0, interactive := true) -> Screen3D:
	var s = Screen3D.new()
	s.name = "screen_" + id
	s.setup(id, size_m, px, ui, interactive)
	s.position = pos
	s.rotation = Vector3(pitch, yaw, 0)
	add_child(s)
	screens.append(s)
	return s

# =====================================================================================================
#  live sync with the simulation
# =====================================================================================================
const LUX_E = 0.55

func lux_energy(lux: float) -> float:
	if lux < 0.5: return 0.0          # lights off: really dark
	return clamp(0.15 + LUX_E * log(max(lux, 1.0) / 20.0) / log(10.0), 0.03, 2.4)

func sync(dt: float, actors: Array) -> void:
	# lighting: every room follows LUNA's lighting simulation (CCT in kelvin, illuminance in lux)
	for key in lights:
		var src = key
		if key.begins_with("communal_") and not sim.hab.config_b: src = "communal"
		var L = sim.hab.light.get(src, sim.hab.light.get(key, null))
		if L == null: continue
		var col = Mats.cct(L.cct)
		var e = lux_energy(L.lux)
		for o in lights[key].omni:
			o.light_color = o.light_color.lerp(col, min(1.0, dt * 2.0))
			o.light_energy = lerp(o.light_energy, e, min(1.0, dt * 2.0))
		for m in lights[key].mats:
			m.emission = m.emission.lerp(col, min(1.0, dt * 2.0))
			m.emission_energy_multiplier = lerp(m.emission_energy_multiplier, (0.25 + e * 1.1) if e > 0.0 else 0.0, min(1.0, dt * 2.0))
	# doors open for anyone within reach
	for d in doors:
		var want = 0.0
		for a in actors:
			if Vector2(a.x - d.pos.x, a.z - d.pos.z).length() < 1.7: want = 1.0
		d.open = move_toward(d.open, want, dt * 2.2)
		d.panel.position.x = -d.open * 0.98
		d.body.disabled = d.open > 0.6
	for u in updaters: u.call(dt)

# =====================================================================================================
#  outside: regolith shielding, suitport porch, vertical solar towers, ATHLETE-style repair robot, rover, dish
# =====================================================================================================
const SUN_DIR = Vector3(-0.62, -0.05, -0.78)     # light travels this way: the Sun sits low in the north-west
const EARTH_DIR = Vector3(0.35, 0.09, -0.93)    # Earth hangs low over the northern horizon at the pole

func _exterior() -> void:
	var ex = Node3D.new(); ex.name = "Outside"; add_child(ex)
	var m = MB.new()
	# ground (just under the habitat floor) + far crater rims
	m.box(Vector3(18, -0.12, 10), Vector3(900, 0.2, 900), Mats.regolith(), 0.0, true)
	var rng = RandomNumberGenerator.new(); rng.seed = 21
	for k in 26:
		var a = rng.randf() * TAU; var d = rng.randf_range(260.0, 520.0)
		var r = rng.randf_range(60.0, 160.0)
		m.sphere(Vector3(18 + cos(a) * d, -r * 0.82, 10 + sin(a) * d), r, Mats.regolith(), 16, 8, Vector3(1.6, 1.0, 1.6))
	for k in 70:
		var p = Vector3(rng.randf_range(-70, 90), 0, rng.randf_range(-70, 80))
		if p.x > -4 and p.x < 40 and p.z > -6 and p.z < 28: continue
		m.sphere(p, rng.randf_range(0.15, 0.9), Mats.regolith(), 8, 5, Vector3(1.2, 0.6, 1.0))
	for k in 9:   # small craters: raised rim
		var p = Vector3(rng.randf_range(-60, 80), -0.05, rng.randf_range(-60, 70))
		if p.x > -6 and p.x < 42 and p.z > -10 and p.z < 30: continue
		var rr = rng.randf_range(2.0, 6.0)
		m.torus(p, rr, rr + 1.4, Mats.regolith(), Basis.from_scale(Vector3(1, 0.12, 1)))
	# roof and berms of regolith bags (radiation + micrometeoroid shielding)
	var x0 = 1 * CELL; var x1 = 28 * CELL; var z0 = 1 * CELL; var z1 = 18 * CELL
	m.box(Vector3((x0 + x1) / 2, 3.65, (z0 + z1) / 2), Vector3(x1 - x0 + 0.6, 0.6, z1 - z0 + 0.6), Mats.bags())
	var gw = world_rect("greenhouse")
	m.sphere(Vector3(gw.get_center().x, 3.9, gw.get_center().y), 3.4, Mats.bags(), 18, 9, Vector3.ONE, 1.9)
	for seg in [[x0, z0, x1, z0, 0], [x0, z1, x1, z1, 1], [x1, z0, x1, z1, 2], [x0, z0, x0, z1, 3]]:
		var pieces = []
		if seg[4] == 0: pieces = [[5.4, x1]]                     # north: gap for the suitport porch
		elif seg[4] == 3: pieces = [[z0, 4.8], [6.5, z1]]          # west: gap for the cargo hatch
		elif seg[4] == 1 or seg[4] == 2: pieces = [[x0, x1] if seg[4] == 1 else [z0, z1]]
		for pc in pieces:
			var L = pc[1] - pc[0]; var mid = (pc[0] + pc[1]) * 0.5
			var pm = PrismMesh.new(); pm.size = Vector3(3.2, 3.9, L)
			var xf: Transform3D
			match seg[4]:
				0: xf = Transform3D(Basis(Vector3.UP, PI / 2.0), Vector3(mid, 1.95, z0 - 1.68))
				1: xf = Transform3D(Basis(Vector3.UP, PI / 2.0), Vector3(mid, 1.95, z1 + 1.68))
				2: xf = Transform3D(Basis(), Vector3(x1 + 1.68, 1.95, mid))
				3: xf = Transform3D(Basis(), Vector3(x0 - 1.68, 1.95, mid))
			m.add(pm, xf, Mats.bags())
			m.cols.append([xf, Vector3(1.6, 3.9, L)])
	# suitport porch: the two EVA suits stay docked outside the north wall
	m.box(Vector3(3.1, 0.06, 0.4), Vector3(4.2, 0.12, 1.6), Mats.dark_metal(), 0.0, true)
	for k in 2:
		var sx = 1 * CELL + (0.5 + 2 * k) * CELL
		_suit(m, Vector3(sx, 0.12, z0 - 0.42))
	m.commit(ex, "outside", true)
	# vertical solar array towers (VSAT-style: tall masts catch the low polar Sun and clear the dust)
	var sm = Mats.solar()
	h_solar_mat = sm
	var face_yaw = atan2(-SUN_DIR.x, -SUN_DIR.z)
	for k in 3:
		var tp = Vector3(-14.0 + k * 9.0, 0, -22.0 - k * 3.0)
		var t = MB.new()
		t.cyl(tp + Vector3(0, 6.0, 0), 0.22, 12.0, Mats.metal(), 12)
		t.cyl(tp + Vector3(0, 0.15, 0), 1.2, 0.3, Mats.dark_metal(), 16)
		var b = Basis(Vector3.UP, face_yaw)
		t.box_xf(Transform3D(b, tp + Vector3(0, 7.0, 0) + b * Vector3(0, 0, 0.3)), Vector3(2.6, 8.2, 0.08), sm)
		t.box_xf(Transform3D(b, tp + Vector3(0, 7.0, 0) + b * Vector3(0, 0, 0.24)), Vector3(2.7, 8.3, 0.04), Mats.frame())
		t.commit(ex, "solar_tower_%d" % k, true)
		add_inter("array_%d" % k, tp + Vector3(0, 1.5, 0), Vector3(2.6, 3.0, 2.6), "", Callable(), "outside")
		dyn["array_%d" % k] = tp
	_athlete(ex, Vector3(-2.0, 0, -15.0))
	_rover(ex, Vector3(16.0, 0, -9.0))
	# comm dish pointed at Earth
	var dm = MB.new(); var dp = Vector3(36.0, 0, -4.0)
	dm.cyl(dp + Vector3(0, 1.6, 0), 0.12, 3.2, Mats.metal(), 10)
	var db = Basis.looking_at(EARTH_DIR, Vector3.UP) * Basis(Vector3.RIGHT, -PI / 2.0)
	dm.cyl(dp + Vector3(0, 3.4, 0), 1.3, 0.12, Mats.white_plastic(), 24, 0.2, db)
	dm.commit(ex, "comm_dish", true)
	var l = Label3D.new(); l.text = "S-band / Ka-band dish → Earth"; l.font_size = 40; l.pixel_size = 0.01
	l.position = dp + Vector3(0, 5.2, 0); l.billboard = BaseMaterial3D.BILLBOARD_ENABLED; ex.add_child(l)
	dyn["outside_root"] = ex

var h_solar_mat: StandardMaterial3D

func _suit(m: MB, p: Vector3) -> void:
	var w = Mats.white_plastic()
	m.box(p + Vector3(0, 1.25, 0.05), Vector3(0.62, 0.7, 0.32), w)           # backpack (docked to the suitport)
	m.capsule(p + Vector3(0, 1.2, -0.25), 0.26, 0.85, Mats.suit(Color(0.92, 0.93, 0.94)))
	m.sphere(p + Vector3(0, 1.75, -0.28), 0.19, Mats.suit(Color(0.92, 0.93, 0.94)), 12, 8)
	m.sphere(p + Vector3(0, 1.74, -0.4), 0.13, Mats.glow(Color(0.9, 0.7, 0.3), 0.15, "visor"), 12, 6, Vector3(1, 0.8, 0.5))
	for side in [-1.0, 1.0]:
		m.capsule(p + Vector3(0.14 * side, 0.45, -0.25), 0.11, 0.9, Mats.suit(Color(0.92, 0.93, 0.94)))
		m.capsule(p + Vector3(0.36 * side, 1.05, -0.25), 0.09, 0.7, Mats.suit(Color(0.92, 0.93, 0.94)))

## six-limbed lunar robot (after NASA JPL's ATHLETE): wheels on its feet, an arm with a dust brush
func _athlete(parent: Node3D, p: Vector3) -> void:
	var n = Node3D.new(); n.name = "athlete_robot"; n.position = p; parent.add_child(n)
	var m = MB.new()
	var deck_y = 2.2
	m.cyl(Vector3(0, deck_y, 0), 1.3, 0.3, Mats.white_plastic(), 6)
	m.cyl(Vector3(0, deck_y + 0.25, 0), 0.9, 0.2, Mats.grey_plastic(), 6)
	for k in 6:
		var a = TAU * k / 6.0 + PI / 6.0
		var hip = Vector3(cos(a) * 1.25, deck_y, sin(a) * 1.25)
		var knee = Vector3(cos(a) * 2.1, deck_y + 0.6, sin(a) * 2.1)
		var foot = Vector3(cos(a) * 2.4, 0.35, sin(a) * 2.4)
		for seg in [[hip, knee], [knee, foot]]:
			var mid = (seg[0] + seg[1]) * 0.5; var d = seg[1] - seg[0]
			var b = Basis.looking_at(d.normalized(), Vector3.UP if abs(d.normalized().y) < 0.99 else Vector3.FORWARD)
			m.box_xf(Transform3D(b, mid), Vector3(0.14, 0.14, d.length()), Mats.metal())
		m.cyl(foot, 0.35, 0.18, Mats.rubber(), 14, -1.0, Basis(Vector3.UP, a) * Basis(Vector3.FORWARD, PI / 2.0))
	m.cyl(Vector3(0, deck_y + 1.0, 0), 0.06, 1.6, Mats.metal(), 8)
	m.box(Vector3(0, deck_y + 1.85, 0), Vector3(0.4, 0.18, 0.18), Mats.black())
	m.box(Vector3(0.9, deck_y + 0.5, 0), Vector3(1.4, 0.1, 0.1), Mats.metal())
	m.box(Vector3(1.55, deck_y + 0.1, 0), Vector3(0.1, 0.8, 0.1), Mats.metal())
	m.box(Vector3(1.55, deck_y - 0.35, 0), Vector3(0.5, 0.12, 0.2), Mats.caution())     # electrostatic brush head
	m.commit(n, "athlete", true)
	var l = Label3D.new(); l.text = "ATHLETE-style six-limbed rover\ninspects and brushes the arrays after lander plumes"; l.font_size = 40; l.pixel_size = 0.008
	l.position = Vector3(0, deck_y + 2.6, 0); l.billboard = BaseMaterial3D.BILLBOARD_ENABLED; n.add_child(l)
	dyn["athlete"] = n

func _rover(parent: Node3D, p: Vector3) -> void:
	var n = Node3D.new(); n.name = "pressurised_rover"; n.position = p; n.rotation.y = 0.4; parent.add_child(n)
	var m = MB.new()
	m.box(Vector3(0, 1.6, 0), Vector3(2.6, 1.9, 4.2), Mats.white_plastic(), 0.0, true)
	m.box(Vector3(0, 1.9, -2.12), Vector3(2.2, 0.8, 0.05), Mats.glass())
	m.box(Vector3(0, 0.55, 0), Vector3(2.4, 0.3, 4.4), Mats.dark_metal())
	for zz in [-1.6, 0.0, 1.6]:
		for xx in [-1.35, 1.35]: m.cyl(Vector3(xx, 0.5, zz), 0.5, 0.35, Mats.rubber(), 16, -1.0, Basis(Vector3.FORWARD, PI / 2.0))
	for xx in [-0.6, 0.6]: m.torus(Vector3(xx, 1.5, 2.12), 0.3, 0.38, Mats.frame(), Basis(Vector3.RIGHT, PI / 2.0))
	m.commit(n, "rover", true)
	var l = Label3D.new(); l.text = "Pressurised rover · suitports at the back"; l.font_size = 40; l.pixel_size = 0.008
	l.position = Vector3(0, 3.2, 0); l.billboard = BaseMaterial3D.BILLBOARD_ENABLED; n.add_child(l)
