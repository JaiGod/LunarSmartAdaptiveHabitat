"""Instrument a COPY of the LUNA VR project so one run records every model primitive for Blender.
Never run on the real project folder."""
import re, sys, pathlib
root = pathlib.Path(sys.argv[1])
assert "luna_rec" in str(root), "refusing to patch anything but the scratch copy"

# ---- mb.gd: record every primitive with its call stack and material ---------------------------------
mb = (root / "scripts/mb.gd").read_text()
mb = mb.replace("var _cur = Vector3.ZERO", '''var _cur = Vector3.ZERO
static var rec = null            # Array while recording for Blender
static var model = ""
static var mat_reg = {}
static var mat_obj = {}
static var rec_mi = []
var _pend = []

static func xf_list(xf: Transform3D) -> Array:
	return [xf.basis.x.x, xf.basis.x.y, xf.basis.x.z, xf.basis.y.x, xf.basis.y.y, xf.basis.y.z,
		xf.basis.z.x, xf.basis.z.y, xf.basis.z.z, xf.origin.x, xf.origin.y, xf.origin.z]

static func col_list(c: Color) -> Array: return [c.r, c.g, c.b, c.a]

static func mat_key(m: Material) -> String:
	if m == null: return ""
	var k = m.resource_name if m.resource_name != "" else "mat_%d" % m.get_instance_id()
	if not mat_reg.has(k):
		var d = {"name": k}
		if m is StandardMaterial3D:
			var s: StandardMaterial3D = m
			d["albedo"] = col_list(s.albedo_color); d["rough"] = s.roughness; d["metal"] = s.metallic
			d["emit"] = s.emission_enabled; d["emit_col"] = col_list(s.emission); d["emit_e"] = s.emission_energy_multiplier
			d["transp"] = s.transparency; d["unshaded"] = s.shading_mode == BaseMaterial3D.SHADING_MODE_UNSHADED
			d["tex"] = s.albedo_texture.resource_path if s.albedo_texture and s.albedo_texture.resource_path != "" else ""
			d["tri"] = s.uv1_triplanar; d["uv1_scale"] = [s.uv1_scale.x, s.uv1_scale.y, s.uv1_scale.z]
			d["cull"] = s.cull_mode
		else:
			d["albedo"] = [0.7, 0.7, 0.7, 1.0]; d["rough"] = 0.6; d["metal"] = 0.0; d["shader"] = true
		mat_reg[k] = d
		mat_obj[k] = m
	return k

static func mesh_info(mesh: Mesh) -> Dictionary:
	var d = {}
	if mesh is BoxMesh: d = {"k": "box", "s": [mesh.size.x, mesh.size.y, mesh.size.z]}
	elif mesh is CylinderMesh: d = {"k": "cyl", "r": mesh.bottom_radius, "rt": mesh.top_radius, "h": mesh.height, "n": mesh.radial_segments, "caps": [mesh.cap_top, mesh.cap_bottom]}
	elif mesh is SphereMesh: d = {"k": "sph", "r": mesh.radius, "h": mesh.height, "n": mesh.radial_segments, "rings": mesh.rings, "hemi": mesh.is_hemisphere}
	elif mesh is CapsuleMesh: d = {"k": "cap", "r": mesh.radius, "h": mesh.height}
	elif mesh is QuadMesh: d = {"k": "quad", "s": [mesh.size.x, mesh.size.y], "orient": mesh.orientation}
	elif mesh is PlaneMesh: d = {"k": "plane", "s": [mesh.size.x, mesh.size.y], "orient": mesh.orientation}
	elif mesh is TorusMesh: d = {"k": "torus", "ri": mesh.inner_radius, "ro": mesh.outer_radius, "rings": mesh.rings, "segs": mesh.ring_segments}
	elif mesh is PrismMesh: d = {"k": "prism", "s": [mesh.size.x, mesh.size.y, mesh.size.z], "ltr": mesh.left_to_right}
	else:
		var tris = []
		for si in mesh.get_surface_count():
			var arr = mesh.surface_get_arrays(si)
			var v: PackedVector3Array = arr[Mesh.ARRAY_VERTEX]
			var idx = arr[Mesh.ARRAY_INDEX]
			var flat = []
			if idx != null and idx.size() > 0:
				for i in idx: flat.append_array([snappedf(v[i].x, 0.0001), snappedf(v[i].y, 0.0001), snappedf(v[i].z, 0.0001)])
			else:
				for p in v: flat.append_array([snappedf(p.x, 0.0001), snappedf(p.y, 0.0001), snappedf(p.z, 0.0001)])
			tris.append(flat)
		d = {"k": "tris", "v": tris}
	return d

func _rec(d: Dictionary, xf: Transform3D, m: Material) -> void:
	var st = []
	for f in get_stack():
		var src = String(f.source).get_file()
		if src == "mb.gd": continue
		st.append([src, f.function, f.line])
	d["mat"] = mat_key(m); d["model"] = model; d["xf"] = xf_list(xf); d["st"] = st
	rec.append(d); rec_mi.append(null)
	var ck = Vector2i(floori(_cur.x / chunk), floori(_cur.z / chunk)) if chunk > 0.0 else Vector2i.ZERO
	_pend.append([rec.size() - 1, [ck, m]])''')
mb = mb.replace('''	_cur = xf.origin
	_st(m).append_from(mesh, 0, xf)''', '''	_cur = xf.origin
	if rec != null: _rec(mesh_info(mesh), xf, m)
	_st(m).append_from(mesh, 0, xf)''')
mb = mb.replace('''	_cur = Vector3(hole_c.x, y, hole_c.y)
	var st = _st(m)''', '''	_cur = Vector3(hole_c.x, y, hole_c.y)
	if rec != null: _rec({"k": "phole", "rect": [rect.position.x, rect.position.y, rect.size.x, rect.size.y], "y": y, "hc": [hole_c.x, hole_c.y], "hr": hole_r, "down": facing_down}, Transform3D(), m)
	var st = _st(m)''')
mb = mb.replace('''		mi.mesh = mesh
		mi.name = label''', '''		mi.mesh = mesh
		mi.set_meta("mb", true)
		_mi_for[key] = mi
		mi.name = label''')
mb = mb.replace('''func commit(parent: Node3D, label: String, shadows := false) -> Array:
	var out = []''', '''func commit(parent: Node3D, label: String, shadows := false) -> Array:
	var out = []
	var _mi_for = {}''')
mb = mb.replace('''	groups = {}; mats = {}; cols = []
	return out''', '''	if rec != null:
		for pe in _pend: rec_mi[pe[0]] = _mi_for.get(pe[1])
	_pend = []
	groups = {}; mats = {}; cols = []
	return out''')
assert "_mi_for[key] = mi" in mb and "rec_mi[pe[0]]" in mb
assert mb.count("_rec(") >= 3 and 'set_meta("mb"' in mb
(root / "scripts/mb.gd").write_text(mb)

# ---- models.gd: tag the current model ----------------------------------------------------------------
md = (root / "scripts/models.gd").read_text()
md = md.replace('''	root = Node3D.new(); root.name = name''', '''	root = Node3D.new(); root.name = name; MB.model = name''')
assert "MB.model = name" in md
(root / "scripts/models.gd").write_text(md)

# ---- habitat.gd: tag shell / exterior ---------------------------------------------------------------
hb = (root / "scripts/habitat.gd").read_text()
hb = hb.replace('''	mb.chunk = 6.25
	_floors_and_ceilings()''', '''	mb.chunk = 6.25; MB.model = "Shell"
	_floors_and_ceilings()''')
hb = hb.replace('''	models.build_all()
	_exterior()''', '''	models.build_all(); MB.model = "Exterior"
	_exterior()''')
assert hb.count("MB.model =") == 2
(root / "scripts/habitat.gd").write_text(hb)

# ---- game.gd: --blender=dir dump mode -----------------------------------------------------------------
gm = (root / "scripts/game.gd").read_text()
gm = gm.replace('''		if arg == "--selenetest": _selenetest.call_deferred()''', '''		if arg == "--selenetest": _selenetest.call_deferred()
		if arg.begins_with("--blender="): _blender_dump.call_deferred(arg.substr(10))''')
gm += (pathlib.Path(__file__).parent / "dump_func.gd.txt").read_text()
(root / "scripts/game.gd").write_text(gm)

# recording must be on before the habitat is built
gm = (root / "scripts/game.gd").read_text()
old = """	hab = Habitat.new(); hab.name = "Habitat"; add_child(hab)
	hab.build(sim)"""
assert old in gm
gm = gm.replace(old, """	hab = Habitat.new(); hab.name = "Habitat"; add_child(hab); MB.rec = [] if " ".join(OS.get_cmdline_user_args()).contains("--blender=") else null
	hab.build(sim)""")
(root / "scripts/game.gd").write_text(gm)
print("patched ok")
