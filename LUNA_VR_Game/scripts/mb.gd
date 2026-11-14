class_name MB
extends RefCounted
## Mesh builder. Every primitive added here is merged with the others that share its material, so a whole
## room becomes a handful of meshes (few draw calls: this is what keeps a standalone VR headset at 72 fps).
## Named sub-builders become separate MeshInstance3D nodes, which is what the .glb export keeps as parts.

var groups = {}          # [chunk, Material] -> SurfaceTool
var mats = {}            # same key -> Material
var cols = []            # [Transform3D, Vector3 size]
## > 0: split merged geometry into square tiles of this size (metres), so each mesh only meets the few
## room lights near it (the Compatibility renderer lights each mesh with at most 8 lights)
var chunk = 0.0
var _cur = Vector3.ZERO

func _st(m: Material) -> SurfaceTool:
	var ck = Vector2i(floori(_cur.x / chunk), floori(_cur.z / chunk)) if chunk > 0.0 else Vector2i.ZERO
	var key = [ck, m]
	if not groups.has(key):
		var st = SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		groups[key] = st; mats[key] = m
	return groups[key]

func add(mesh: Mesh, xf: Transform3D, m: Material) -> void:
	_cur = xf.origin
	_st(m).append_from(mesh, 0, xf)

## axis-aligned (optionally yaw-rotated) box; c = centre
func box(c: Vector3, s: Vector3, m: Material, yaw := 0.0, collide := false) -> void:
	var bm = BoxMesh.new(); bm.size = s
	var xf = Transform3D(Basis(Vector3.UP, yaw), c)
	add(bm, xf, m)
	if collide: cols.append([xf, s])

func box_xf(xf: Transform3D, s: Vector3, m: Material, collide := false) -> void:
	var bm = BoxMesh.new(); bm.size = s
	add(bm, xf, m)
	if collide: cols.append([xf, s])

## vertical cylinder (c = centre)
func cyl(c: Vector3, r: float, h: float, m: Material, sides := 16, r_top := -1.0, basis := Basis()) -> void:
	var cm = CylinderMesh.new(); cm.bottom_radius = r; cm.top_radius = r if r_top < 0.0 else r_top; cm.height = h
	cm.radial_segments = sides; cm.rings = 1
	add(cm, Transform3D(basis, c), m)

## cylinder lying along X (yaw rotates it in the floor plane)
func tube_x(c: Vector3, r: float, length: float, m: Material, yaw := 0.0, sides := 10) -> void:
	cyl(c, r, length, m, sides, -1.0, Basis(Vector3.UP, yaw) * Basis(Vector3.FORWARD, PI / 2.0))

func tube_z(c: Vector3, r: float, length: float, m: Material, sides := 10) -> void:
	cyl(c, r, length, m, sides, -1.0, Basis(Vector3.RIGHT, PI / 2.0))

func sphere(c: Vector3, r: float, m: Material, segs := 16, rings := 8, scale := Vector3.ONE, hemi_h := 0.0) -> void:
	var sm = SphereMesh.new(); sm.radius = r; sm.height = r * 2.0; sm.radial_segments = segs; sm.rings = rings
	if hemi_h > 0.0: sm.is_hemisphere = true; sm.height = hemi_h
	add(sm, Transform3D(Basis.from_scale(scale), c), m)

func capsule(c: Vector3, r: float, h: float, m: Material, basis := Basis()) -> void:
	var cm = CapsuleMesh.new(); cm.radius = r; cm.height = h; cm.radial_segments = 12; cm.rings = 4
	add(cm, Transform3D(basis, c), m)

## flat quad facing +Z by default (for screens, labels on walls)
func quad(c: Vector3, size: Vector2, m: Material, yaw := 0.0, pitch := 0.0) -> void:
	var qm = QuadMesh.new(); qm.size = size
	add(qm, Transform3D(Basis(Vector3.UP, yaw) * Basis(Vector3.RIGHT, pitch), c), m)

func torus(c: Vector3, r_in: float, r_out: float, m: Material, basis := Basis()) -> void:
	var tm = TorusMesh.new(); tm.inner_radius = r_in; tm.outer_radius = r_out; tm.rings = 24; tm.ring_segments = 8
	add(tm, Transform3D(basis, c), m)

## a flat horizontal panel with a circular hole (ceiling around the greenhouse light-pipe dome)
func plane_with_hole(rect: Rect2, y: float, hole_c: Vector2, hole_r: float, m: Material, facing_down := true) -> void:
	_cur = Vector3(hole_c.x, y, hole_c.y)
	var st = _st(m)
	var angs = []
	for k in 64: angs.append(TAU * k / 64.0)
	for corner in [rect.position, Vector2(rect.end.x, rect.position.y), rect.end, Vector2(rect.position.x, rect.end.y)]:
		angs.append(fposmod(atan2(corner.y - hole_c.y, corner.x - hole_c.x), TAU))
	angs.sort()
	var n = angs.size()
	for k in n:
		var a0 = angs[k]; var a1 = angs[(k + 1) % n]
		if a1 < a0: a1 += TAU
		var i0 = hole_c + Vector2(cos(a0), sin(a0)) * hole_r
		var i1 = hole_c + Vector2(cos(a1), sin(a1)) * hole_r
		var o0 = _ray_rect(hole_c, a0, rect); var o1 = _ray_rect(hole_c, a1, rect)
		var tri = [[i0, o0, o1], [i0, o1, i1]]
		for t in tri:
			var pts = t if not facing_down else [t[0], t[2], t[1]]
			for p in pts:
				st.set_normal(Vector3.DOWN if facing_down else Vector3.UP)
				st.set_uv(Vector2(p.x, p.y) * 0.5)
				st.add_vertex(Vector3(p.x, y, p.y))

func _ray_rect(c: Vector2, a: float, r: Rect2) -> Vector2:
	var d = Vector2(cos(a), sin(a)); var best = 1e9
	for t in [(r.position.x - c.x) / d.x if absf(d.x) > 1e-6 else 1e9, (r.end.x - c.x) / d.x if absf(d.x) > 1e-6 else 1e9,
			(r.position.y - c.y) / d.y if absf(d.y) > 1e-6 else 1e9, (r.end.y - c.y) / d.y if absf(d.y) > 1e-6 else 1e9]:
		if t > 0.0:
			var p = c + d * t
			if p.x >= r.position.x - 0.001 and p.x <= r.end.x + 0.001 and p.y >= r.position.y - 0.001 and p.y <= r.end.y + 0.001:
				best = min(best, t)
	return c + d * best

## turn everything into MeshInstance3D children of `parent` (one per material) + one StaticBody3D for the boxes
func commit(parent: Node3D, label: String, shadows := false) -> Array:
	var out = []
	for key in groups:
		var st: SurfaceTool = groups[key]
		var m: Material = mats[key]
		var mesh = st.commit()
		if mesh.get_surface_count() == 0: continue
		mesh.surface_set_material(0, m)
		var mi = MeshInstance3D.new()
		mi.mesh = mesh
		mi.name = label + "_" + str(m.resource_name if m.resource_name != "" else "mat")
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if shadows else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		parent.add_child(mi)
		out.append(mi)
	if not cols.is_empty():
		var sb = StaticBody3D.new(); sb.name = label + "_collision"
		parent.add_child(sb)
		for c in cols:
			var cs = CollisionShape3D.new(); var bs = BoxShape3D.new(); bs.size = c[1]
			cs.shape = bs; cs.transform = c[0]
			sb.add_child(cs)
	groups = {}; mats = {}; cols = []
	return out
