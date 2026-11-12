# ==============================================================================================
#  PROJECT ARES · Crew Health Sim
#  File    : scripts/build.gd
#  Purpose : Small helpers that build the 3D world from code: materials, boxes, cylinders, colliders,
#            GLB loading + auto-fit, and WIRES — cable trays, bundled cables and sagging cables
#            (catenary tubes) that run from the ceiling tray down to every health device.
#  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
# ==============================================================================================
class_name Build
extends RefCounted

static var _mats = {}

static func mat(c: Color, metal := 0.0, rough := 0.7, emit := 0.0) -> StandardMaterial3D:
	var key = "%s|%.2f|%.2f|%.2f" % [c.to_html(), metal, rough, emit]
	if _mats.has(key): return _mats[key]
	var m = StandardMaterial3D.new()
	m.albedo_color = c; m.metallic = metal; m.roughness = rough
	if emit > 0.0: m.emission_enabled = true; m.emission = c; m.emission_energy_multiplier = emit
	if c.a < 1.0: m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_mats[key] = m
	return m

static func box(parent: Node, size: Vector3, pos: Vector3, m: Material, solid := false, rot_y := 0.0) -> MeshInstance3D:
	var mi = MeshInstance3D.new(); var bm = BoxMesh.new(); bm.size = size; mi.mesh = bm; mi.material_override = m
	parent.add_child(mi); mi.position = pos; mi.rotation.y = rot_y
	if solid: collider(mi, size)
	return mi

static func cyl(parent: Node, r: float, h: float, pos: Vector3, m: Material, segs := 24) -> MeshInstance3D:
	var mi = MeshInstance3D.new(); var cm = CylinderMesh.new(); cm.top_radius = r; cm.bottom_radius = r; cm.height = h; cm.radial_segments = segs
	mi.mesh = cm; mi.material_override = m; parent.add_child(mi); mi.position = pos
	return mi

static func sphere(parent: Node, r: float, pos: Vector3, m: Material) -> MeshInstance3D:
	var mi = MeshInstance3D.new(); var sm = SphereMesh.new(); sm.radius = r; sm.height = r * 2.0; sm.radial_segments = 16; sm.rings = 8
	mi.mesh = sm; mi.material_override = m; parent.add_child(mi); mi.position = pos
	return mi

static func collider(on: Node3D, size: Vector3, offset := Vector3.ZERO) -> StaticBody3D:
	var sb = StaticBody3D.new(); var cs = CollisionShape3D.new(); var sh = BoxShape3D.new(); sh.size = size; cs.shape = sh
	sb.add_child(cs); on.add_child(sb); sb.position = offset
	return sb

static func label3d(parent: Node, text: String, pos: Vector3, size := 0.12, col := Color(0.75, 0.9, 1.0)) -> Label3D:
	var l = Label3D.new(); l.text = text; l.pixel_size = size * 0.0045; l.font_size = 48; l.modulate = col
	l.billboard = BaseMaterial3D.BILLBOARD_ENABLED; l.outline_size = 8; parent.add_child(l); l.position = pos
	return l

## load a GLB and scale it so its biggest side = target metres, standing on the floor, centred at pos
static func model(parent: Node, path: String, target: float, pos: Vector3, rot_y := 0.0) -> Node3D:
	var holder = Node3D.new(); parent.add_child(holder); holder.position = pos; holder.rotation.y = rot_y
	var ps = load(path)
	if ps == null: return holder
	var inst: Node3D = ps.instantiate(); holder.add_child(inst)
	var bb = aabb_of(inst)
	var big: float = max(bb.size.x, max(bb.size.y, bb.size.z))
	if big > 0.0001:
		var s = target / big; inst.scale = Vector3.ONE * s
		inst.position = Vector3(-bb.get_center().x * s, -bb.position.y * s, -bb.get_center().z * s)
	holder.set_meta("size", bb.size * (target / max(big, 0.0001)))
	return holder

static func aabb_of(n: Node) -> AABB:
	var out = AABB(); var first = true
	for mi in n.find_children("*", "MeshInstance3D", true, false):
		var t = Transform3D.IDENTITY; var p: Node = mi
		while p != null and p != n:
			if p is Node3D: t = (p as Node3D).transform * t
			p = p.get_parent()
		var a: AABB = t * (mi as MeshInstance3D).get_aabb()
		if first: out = a; first = false
		else: out = out.merge(a)
	return out

# ---- WIRES --------------------------------------------------------------------------------
## a smooth tube through the given points (used for every cable)
static func tube(parent: Node, pts: PackedVector3Array, r: float, m: Material, sides := 6) -> MeshInstance3D:
	var st = SurfaceTool.new(); st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var n = pts.size()
	var rings = []
	for i in n:
		var fwd: Vector3 = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
		var up = Vector3.UP if absf(fwd.dot(Vector3.UP)) < 0.95 else Vector3.RIGHT
		var side = fwd.cross(up).normalized(); var up2 = side.cross(fwd).normalized()
		var ring = []
		for k in sides:
			var a = TAU * k / sides
			ring.append([pts[i] + (side * cos(a) + up2 * sin(a)) * r, (side * cos(a) + up2 * sin(a))])
		rings.append(ring)
	for i in n - 1:
		for k in sides:
			var k2 = (k + 1) % sides
			var q = [rings[i][k], rings[i + 1][k], rings[i + 1][k2], rings[i][k], rings[i + 1][k2], rings[i][k2]]
			for v in q: st.set_normal(v[1]); st.add_vertex(v[0])
	var mi = MeshInstance3D.new(); mi.mesh = st.commit(); mi.material_override = m; parent.add_child(mi)
	return mi

## a cable hanging between a and b with some sag (catenary-ish)
static func sag_cable(parent: Node, a: Vector3, b: Vector3, sag: float, r: float, c: Color) -> void:
	var pts = PackedVector3Array()
	for i in 17:
		var t = i / 16.0
		var p = a.lerp(b, t); p.y -= sag * 4.0 * t * (1.0 - t)
		pts.append(p)
	tube(parent, pts, r, mat(c, 0.0, 0.55))

## a cable that follows a path of corners (drops down a wall, runs along the floor edge, etc.)
static func routed_cable(parent: Node, corners: Array, r: float, c: Color) -> void:
	var pts = PackedVector3Array()
	for i in corners.size() - 1:
		var a: Vector3 = corners[i]; var b: Vector3 = corners[i + 1]
		var steps: int = max(2, int(a.distance_to(b) / 0.25))
		for s in steps: pts.append(a.lerp(b, float(s) / steps))
	pts.append(corners[-1])
	tube(parent, pts, r, mat(c, 0.0, 0.55))

## ceiling cable tray running round the module, with a bundle of colour-coded cables in it
static func cable_ring(parent: Node, radius: float, y: float, a0: float, a1: float) -> void:
	var tray = mat(Color(0.55, 0.58, 0.62), 0.6, 0.4)
	var segs: int = int((a1 - a0) / 0.12)
	for i in segs:
		var a = a0 + (a1 - a0) * (i + 0.5) / segs
		var p = Vector3(cos(a) * radius, y, sin(a) * radius)
		var len = radius * (a1 - a0) / segs * 1.02
		var floor_plate = box(parent, Vector3(0.32, 0.02, len), p + Vector3(0, -0.06, 0), tray); floor_plate.rotation.y = -a
		var l = box(parent, Vector3(0.02, 0.1, len), p + Vector3(cos(a), 0, sin(a)) * 0.16 + Vector3(0, -0.02, 0), tray); l.rotation.y = -a
		var rr = box(parent, Vector3(0.02, 0.1, len), p - Vector3(cos(a), 0, sin(a)) * 0.16 + Vector3(0, -0.02, 0), tray); rr.rotation.y = -a
	# cables in the tray: power (black/red), data (blue), fibre (yellow), sensor bus (green), ground (green-yellow)
	var cols = [Color(0.08, 0.08, 0.09), Color(0.75, 0.12, 0.1), Color(0.15, 0.35, 0.85), Color(0.95, 0.8, 0.15), Color(0.2, 0.7, 0.35), Color(0.9, 0.9, 0.92)]
	for ci in cols.size():
		var off = -0.11 + 0.044 * ci
		var pts = PackedVector3Array()
		var steps: int = int((a1 - a0) / 0.05)
		for i in steps + 1:
			var a = a0 + (a1 - a0) * i / steps
			var rr2 = radius + off + 0.008 * sin(a * 9.0 + ci)
			pts.append(Vector3(cos(a) * rr2, y - 0.03 + 0.012 * (ci % 2), sin(a) * rr2))
		tube(parent, pts, 0.016, mat(cols[ci], 0.0, 0.5))
	# cable ties every ~1 m
	for i in int((a1 - a0) * radius):
		var a = a0 + float(i) / radius
		var t = box(parent, Vector3(0.28, 0.07, 0.015), Vector3(cos(a) * radius, y - 0.03, sin(a) * radius), mat(Color(0.95, 0.95, 0.95)))
		t.rotation.y = -a
