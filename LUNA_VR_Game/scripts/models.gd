class_name Models
extends RefCounted
## Every model in LUNA VR, each with its reason to exist. Built at real scale (metres) from simple, named
## parts so it exports cleanly to .glb for Blender. Nothing here is decoration: each block of geometry is
## something a lunar crew would use, service or be protected by.

const C = Habitat.CELL
var h: Habitat
var mb: MB
var root: Node3D
var per_model = false     # true: every model keeps its own meshes (for the .glb export); false: merged for speed
var shared: MB

func _init(hab: Habitat, separate := false) -> void:
	h = hab
	per_model = separate
	shared = MB.new()
	shared.chunk = 6.25
	mb = shared

static func W(cx: float, cy: float, y := 0.0) -> Vector3: return Vector3(cx * C, y, cy * C)

func _begin(name: String) -> Node3D:
	root = Node3D.new(); root.name = name
	h.add_child(root)
	mb = MB.new() if per_model else shared
	return root

func _end() -> void:
	if per_model: mb.commit(root, root.name)

func label(text: String, pos: Vector3, yaw: float, size := 26, col := Color(0.12, 0.14, 0.18), parent: Node3D = null) -> Label3D:
	var l = Label3D.new(); l.text = text; l.font_size = size; l.pixel_size = 0.0035; l.modulate = col
	l.position = pos; l.rotation.y = yaw; l.outline_size = 0
	(parent if parent else root).add_child(l)
	return l

func build_all() -> void:
	airlock(); pods(); kiosk(); hub(); greenhouse(); medical(); eclss(); robot_bay(); ops(); exercise(); hygiene()
	if not per_model:
		var all = Node3D.new(); all.name = "Furniture"; h.add_child(all)
		shared.commit(all, "furniture")

# =====================================================================================================
# DUST DECONTAMINATION AIRLOCK
#  NASA need: lunar dust irritated Apollo crews' eyes and lungs and wore seals; suitports + dust removal.
#  Gameplay: EVA start/return; dust that comes back in collects here and leaks into the cabin air.
#  Interaction: climb into a suit through a suitport; read the particle counter; run the cleaning cycle.
#  Why needed: skip the cleaning and cabin particulates rise, which the LUNA engine turns into fatigue.
# =====================================================================================================
func airlock() -> void:
	_begin("Dust_Decontamination_Airlock")
	var x0 = 1 * C; var x1 = 4 * C; var z0 = 1 * C
	# two rear-entry suitports in the north wall: the dusty suit stays outside, you climb in from here
	for k in 2:
		var cx = x0 + (0.5 + 2 * k) * C
		var b = Basis(Vector3.RIGHT, PI / 2.0)
		mb.torus(Vector3(cx, 1.25, z0 + 0.07), 0.36, 0.46, Mats.frame(), b)
		mb.box(Vector3(cx, 1.25, z0 + 0.09), Vector3(0.5, 0.62, 0.05), Mats.white_plastic())        # PLSS hatch (suit backpack door)
		mb.box(Vector3(cx, 1.05, z0 + 0.13), Vector3(0.22, 0.04, 0.05), Mats.caution())             # handle
		mb.box(Vector3(cx, 0.13, z0 + 0.45), Vector3(0.7, 0.26, 0.5), Mats.dark_metal(), 0.0, true)  # step
		mb.box(Vector3(cx, 0.265, z0 + 0.69), Vector3(0.7, 0.012, 0.04), Mats.caution())
		mb.tube_x(Vector3(cx, 1.95, z0 + 0.12), 0.02, 0.9, Mats.handrail())
		label("SUITPORT %d" % (k + 1), Vector3(cx, 1.72, z0 + 0.1), 0.0, 22)
		h.add_inter("suitport", Vector3(cx, 1.2, z0 + 0.4), Vector3(0.8, 1.4, 0.6), "", Callable(), "airlock")
	# HEPA vacuum: floor grilles feed a filter on the east wall (dust falls through the grating)
	for zz in [3.0, 5.6]:
		mb.box(Vector3(x1 - 0.08, 0.12, zz), Vector3(0.1, 0.18, 0.8), Mats.black())
	mb.tube_z(Vector3(x1 - 0.12, 0.6, 4.3), 0.07, 2.6, Mats.metal())
	mb.cyl(Vector3(x1 - 0.12, 0.9, 5.6), 0.07, 1.0, Mats.metal())
	mb.box(Vector3(x1 - 0.25, 1.75, 4.3), Vector3(0.4, 0.8, 0.9), Mats.grey_plastic())
	label("HEPA dust filter · 0.3 µm\nvacuum plenum under the grating", Vector3(x1 - 0.46, 1.75, 4.3), -PI / 2.0, 20)
	# boot brush station: rollers knock dust off boots before the curtain
	var bb = Vector3(x0 + 1.9, 0, 6.3)
	mb.box(bb + Vector3(0, 0.08, 0), Vector3(0.9, 0.16, 0.6), Mats.dark_metal(), 0.0, true)
	for dx in [-0.18, 0.18]: mb.tube_z(bb + Vector3(dx, 0.2, 0), 0.07, 0.5, Mats.rubber())
	mb.box(bb + Vector3(0, 0.5, -0.32), Vector3(0.9, 0.04, 0.04), Mats.handrail())
	for dx in [-0.42, 0.42]: mb.box(bb + Vector3(dx, 0.25, -0.32), Vector3(0.04, 0.5, 0.04), Mats.handrail())
	h.add_inter("boot_brush", bb + Vector3(0, 0.5, 0), Vector3(1.0, 1.0, 0.8), "", Callable(), "airlock")
	# EVA tool locker on the west wall
	mb.box(Vector3(x0 + 0.24, 1.0, 8.6), Vector3(0.45, 2.0, 1.0), Mats.grey_plastic(), 0.0, true)
	mb.box(Vector3(x0 + 0.47, 1.0, 8.6), Vector3(0.01, 1.9, 0.01), Mats.dark_metal())
	label("EVA tools: brush pole · sample bags · tethers", Vector3(x0 + 0.48, 1.7, 8.6), PI / 2.0, 20)
	# electrostatic dust curtain: charged electrode panels either side of the walkway catch fine dust
	var zc = 7.45
	for side in [[x0 + 0.62, 1.25], [x1 - 0.62, 1.25]]:
		mb.box(Vector3(side[0], 1.15, zc), Vector3(side[1], 2.3, 0.08), Mats.frame(), 0.0, true)
		for k in 9:
			mb.box(Vector3(side[0], 0.25 + k * 0.24, zc + 0.045), Vector3(side[1] - 0.12, 0.012, 0.01), Mats.glow(Color(0.3, 1.0, 0.5), 0.0, "curtain_glow"))   # electrodes: lit green only while charged
	mb.box(Vector3((x0 + x1) * 0.5, 2.38, zc), Vector3(x1 - x0, 0.2, 0.3), Mats.grey_plastic())   # air knife header
	label("Electrostatic dust curtain\nwalk through slowly", Vector3((x0 + x1) * 0.5, 2.38, zc + 0.16), 0.0, 20)
	h.dyn["curtain_mat"] = Mats.cache["curtain_glow"]
	# control + particle counter screen (clean side, east wall)
	h.add_screen("airlock", Vector3(x1 - 0.07, 1.45, 8.9), -PI / 2.0, Vector2(0.62, 0.42), Vector2i(620, 420), null)
	eva_prep(x0, z0)
	# floating dust (shown when dust is in the airlock)
	var p = CPUParticles3D.new(); p.name = "dust"
	p.amount = 160; p.lifetime = 6.0; p.emitting = true
	p.emission_shape = CPUParticles3D.EMISSION_SHAPE_BOX; p.emission_box_extents = Vector3(1.6, 1.1, 2.6)
	p.position = Vector3((x0 + x1) * 0.5, 1.1, 4.0)
	p.gravity = Vector3(0, -0.05, 0); p.initial_velocity_min = 0.0; p.initial_velocity_max = 0.05
	p.scale_amount_min = 0.01; p.scale_amount_max = 0.02
	var dm = SphereMesh.new(); dm.radius = 0.5; dm.height = 1.0; dm.radial_segments = 4; dm.rings = 2
	var dmat = StandardMaterial3D.new(); dmat.albedo_color = Color(0.6, 0.58, 0.55, 0.7); dmat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	dmat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED; dm.material = dmat
	p.mesh = dm
	root.add_child(p)
	h.dyn["dust_particles"] = p
	_end()

# =====================================================================================================
# PERSONAL CREW POD ×4 (+ Sleep Quality Analysis Station, Personal Hydroponics Cabinet)
#  NASA need: private crew quarters and protected sleep (NASA-STD-3001); circadian disruption is a known risk.
#  Gameplay: sleep (smart alarm in light sleep, dawn light), read last night's sleep, tend your own plants,
#            set your pod light and colour, call home.
#  Interaction: bed, desk screen, cabinet.
#  Why needed: sleep loss and lack of privacy feed fatigue and conflict; the bed is LUNA's best sensor.
# =====================================================================================================
func pods() -> void:
	for i in 4:
		_begin("Crew_Pod_%d" % (i + 1))
		var x0 = (7 + 2 * i) * C; var x1 = x0 + 2 * C; var z0 = 1 * C
		var accent = StandardMaterial3D.new(); accent.albedo_color = Habitat.POD_COL[i]; accent.roughness = 0.6; accent.resource_name = "pod_accent_%d" % i
		if i == 0: h.dyn["pod0_accent"] = accent
		# smart bed along the west wall
		var bx = x0 + 0.56; var bz = z0 + 1.2
		mb.box(Vector3(bx, 0.2, bz), Vector3(0.98, 0.4, 2.12), Mats.white_plastic(), 0.0, true)
		mb.box(Vector3(bx, 0.47, bz), Vector3(0.92, 0.14, 2.04), Mats.mattress())
		mb.box(Vector3(bx, 0.555, bz + 0.3), Vector3(0.94, 0.05, 1.3), Mats.blanket(Habitat.POD_COL[i].lerp(Color.WHITE, 0.25)))
		mb.box(Vector3(bx, 0.58, z0 + 0.4), Vector3(0.6, 0.1, 0.32), Mats.mattress())               # pillow
		mb.box(Vector3(x0 + 0.06, 1.2, bz), Vector3(0.04, 1.2, 2.12), accent)                       # headboard wall panel
		mb.box(Vector3(bx + 0.47, 0.41, bz), Vector3(0.02, 0.03, 1.8), Mats.glow(Color(0.2, 0.9, 0.5), 1.5))   # bed sensor strip
		label("Smart bed: sleep stages from the mattress sensor\nalarm waits for light sleep", Vector3(x0 + 0.09, 1.05, bz), PI / 2.0, 18)
		# desk + Sleep Quality Analysis Station
		var dx = x1 - 0.62; var dz = z0 + 0.4
		mb.box(Vector3(dx, 0.74, dz), Vector3(1.05, 0.04, 0.6), Mats.white_plastic(), 0.0, true)
		mb.box(Vector3(dx - 0.48, 0.37, dz), Vector3(0.04, 0.74, 0.55), Mats.grey_plastic())
		mb.box(Vector3(dx + 0.48, 0.37, dz), Vector3(0.04, 0.74, 0.55), Mats.grey_plastic())
		_chair(Vector3(dx, 0, z0 + 1.35), PI, accent)
		if i == 0:
			h.add_screen("pod_desk", Vector3(dx, 1.32, z0 + 0.075), 0.0, Vector2(0.86, 0.52), Vector2i(860, 520), null)
		else:
			mb.box(Vector3(dx, 1.32, z0 + 0.08), Vector3(0.86, 0.52, 0.03), Mats.black())
		# photo from home (connection to Earth)
		mb.box(Vector3(x0 + 0.08, 1.75, z0 + 2.6), Vector3(0.02, 0.26, 0.34), Mats.frame())
		mb.box(Vector3(x0 + 0.095, 1.75, z0 + 2.6), Vector3(0.01, 0.22, 0.3), Mats.glow(Color(0.55, 0.7, 0.9), 0.3))
		# Personal Hydroponics Cabinet on the east wall
		var cz = z0 + 3.1
		mb.box(Vector3(x1 - 0.22, 1.45, cz), Vector3(0.38, 0.75, 0.95), Mats.white_plastic(), 0.0, true)
		mb.box(Vector3(x1 - 0.02 - 0.4, 1.45, cz), Vector3(0.015, 0.7, 0.9), Mats.glass())
		mb.box(Vector3(x1 - 0.22, 1.78, cz), Vector3(0.3, 0.02, 0.85), Mats.glow(Color(1.0, 0.55, 0.85), 2.0, "cabinet_grow"))   # cabinet grow light (photoperiod, off at night)
		mb.box(Vector3(x1 - 0.22, 1.2, cz), Vector3(0.3, 0.12, 0.85), Mats.dark_metal())
		for k in 4:
			var pz = cz - 0.33 + k * 0.22
			mb.sphere(Vector3(x1 - 0.24, 1.36, pz), 0.07, Mats.plant(Color(0.3, 0.62, 0.25).lerp(Color(0.15, 0.4, 0.15), k * 0.2)), 8, 4, Vector3(1, 0.7, 1))
		label("Personal hydroponics: herbs + strawberries", Vector3(x1 - 0.43, 1.92, cz), -PI / 2.0, 18)
		if i == 0: h.add_inter("cabinet", Vector3(x1 - 0.5, 1.45, cz), Vector3(0.5, 0.8, 1.0), "", Callable(), "pod0")
		# soft stowage (cargo-transfer-bag style) for personal kit
		for k in 3:
			mb.box(Vector3(x1 - 0.22, 0.25 + k * 0.42, z0 + 4.45), Vector3(0.4, 0.38, 0.9), Mats.fabric())
		mb.box(Vector3(x1 - 0.06, 1.3, z0 + 4.45), Vector3(0.02, 2.4, 1.0), accent)
		if i == 0:
			h.add_inter("bed", Vector3(bx, 0.6, bz), Vector3(1.0, 0.6, 2.1), "", Callable(), "pod0")
			label("POD 1 · YOU", Vector3(x0 + 1.25, 2.2, z0 + 0.03), 0.0, 30, Color(0.2, 0.24, 0.3))
		_end()

func _chair(p: Vector3, yaw: float, seat_mat: Material) -> void:
	var b = Basis(Vector3.UP, yaw)
	mb.box(p + Vector3(0, 0.46, 0), Vector3(0.46, 0.06, 0.44), seat_mat, yaw)
	mb.box(p + b * Vector3(0, 0.75, -0.2), Vector3(0.44, 0.5, 0.05), seat_mat, yaw)
	mb.cyl(p + Vector3(0, 0.22, 0), 0.03, 0.44, Mats.metal(), 8)
	mb.cyl(p + Vector3(0, 0.02, 0), 0.24, 0.04, Mats.dark_metal(), 10)

# =====================================================================================================
# MOOD CHECK-IN KIOSK (Burnout Intervention Kiosk)
#  NASA need: self-report keeps the AI honest when sensors fail (board question Q4: degraded mode).
#  Gameplay: daily 1–5 check-in; choose private or shared with the CMO; LUNA suggests an activity.
#  Interaction: touch screen inside an acoustic privacy booth.
#  Why needed: consent-based data; it improves LUNA's estimate of you and gives you a voice.
# =====================================================================================================
func kiosk() -> void:
	_begin("Mood_Checkin_Kiosk")
	var x0 = 7 * C; var z0 = 6 * C
	# acoustic privacy booth: fabric side panels, back panel and roof keep the check-in private
	for zz in [z0 + 0.25, z0 + 2.25]:
		mb.box(Vector3(x0 + 0.5, 1.1, zz), Vector3(0.9, 2.2, 0.08), Mats.fabric(Color(0.75, 0.72, 0.85), "fabric_kiosk"), 0.0, true)
	mb.box(Vector3(x0 + 0.08, 1.1, z0 + 1.25), Vector3(0.06, 2.2, 2.0), Mats.fabric(Color(0.75, 0.72, 0.85), "fabric_kiosk"))
	mb.box(Vector3(x0 + 0.5, 2.2, z0 + 1.25), Vector3(0.9, 0.06, 2.06), Mats.white_plastic())
	# writing shelf under the touch screen
	mb.box(Vector3(x0 + 0.3, 0.95, z0 + 1.25), Vector3(0.3, 0.04, 0.7), Mats.white_plastic())
	h.add_screen("kiosk", Vector3(x0 + 0.13, 1.42, z0 + 1.25), PI / 2.0, Vector2(0.62, 0.42), Vector2i(620, 420), null)
	label("Daily check-in · private by default", Vector3(x0 + 0.13, 1.82, z0 + 1.25), PI / 2.0, 22)
	_end()

# =====================================================================================================
# RECONFIGURABLE SOCIAL HUB (galley + wardroom)
#  NASA need: shared meals and group time build cohesion; the brief's conflict protocol (Floor plan B).
#  Gameplay: eat with the crew; movie night; close the partition when two crew are in conflict.
#  Interaction: seats at the table and sofa; hub control panel (dining / movie / split).
#  Why needed: it is where connection is rebuilt, and where conflict is managed without forcing people.
# =====================================================================================================
func hub() -> void:
	_begin("Social_Hub")
	var x0 = 15 * C; var z0 = 1 * C
	# galley: food warmer, rehydration station, chilled stowage
	mb.box(Vector3(x0 + 1.25, 0.45, z0 + 0.32), Vector3(2.4, 0.9, 0.6), Mats.white_plastic(), 0.0, true)
	mb.box(Vector3(x0 + 1.25, 0.92, z0 + 0.32), Vector3(2.44, 0.04, 0.64), Mats.grey_plastic())
	mb.box(Vector3(x0 + 0.6, 1.2, z0 + 0.2), Vector3(0.5, 0.5, 0.35), Mats.metal())
	mb.box(Vector3(x0 + 0.6, 1.2, z0 + 0.38), Vector3(0.44, 0.3, 0.01), Mats.glow(Color(1.0, 0.6, 0.3), 0.6))
	mb.box(Vector3(x0 + 1.6, 1.2, z0 + 0.18), Vector3(0.35, 0.55, 0.3), Mats.grey_plastic())
	mb.cyl(Vector3(x0 + 1.6, 1.0, z0 + 0.32), 0.015, 0.1, Mats.metal(), 8)
	label("Food warmer", Vector3(x0 + 0.6, 1.5, z0 + 0.39), 0.0, 18)
	label("Rehydration · potable water", Vector3(x0 + 1.6, 1.52, z0 + 0.34), 0.0, 18)
	h.add_inter("galley", Vector3(x0 + 1.2, 1.1, z0 + 0.6), Vector3(2.4, 0.6, 0.4), "", Callable(), "communal")
	# dining table (height-adjustable: 0.74 m dining, 0.45 m lounge)
	var tnode = Node3D.new(); tnode.name = "table"; root.add_child(tnode)
	tnode.position = W(18.0, 4.0)
	var tm = MB.new()
	tm.box(Vector3(0, 0.74, 0), Vector3(2.1, 0.05, 1.8), Mats.white_plastic(), 0.0, true)
	tm.box(Vector3(0, 0.37, 0), Vector3(0.25, 0.72, 0.25), Mats.metal())
	tm.box(Vector3(0, 0.02, 0), Vector3(0.9, 0.04, 0.9), Mats.dark_metal())
	tm.commit(tnode, "table")
	h.dyn["hub_table"] = tnode
	for s in [[17, 2, 0.0], [18, 2, 0.0], [17, 5, PI], [18, 5, PI]]:
		var pc = W(s[0] + 0.5, s[1] + 0.5)
		_chair(pc + Vector3(0, 0, 0.15 if s[2] == PI else -0.15), s[2], Mats.fabric(Color(0.95, 0.6, 0.45), "fabric_hub"))
		h.add_inter("seat", pc + Vector3(0, 0.6, 0), Vector3(0.6, 0.8, 0.6), "", Callable(), "communal")
	# modular sofa facing the wall display
	for k in 3:
		var sp = W(20.0 + k * 0.8, 6.75)
		mb.box(sp + Vector3(0, 0.22, 0), Vector3(0.78, 0.44, 0.8), Mats.fabric(Color(0.95, 0.6, 0.45), "fabric_hub"), 0.0, true)
		mb.box(sp + Vector3(0, 0.62, 0.32), Vector3(0.78, 0.5, 0.16), Mats.fabric(Color(0.95, 0.6, 0.45), "fabric_hub"))
	h.add_inter("sofa", W(21.2, 6.6, 0.6), Vector3(2.4, 0.8, 0.8), "", Callable(), "communal")
	# wall display (Earth view, movie night)
	mb.box(W(21.25, 1.0, 1.7) + Vector3(0, 0, 0.04), Vector3(2.5, 1.45, 0.06), Mats.black())
	var scr = MeshInstance3D.new(); var qm = QuadMesh.new(); qm.size = Vector2(2.4, 1.35); scr.mesh = qm
	var sm = StandardMaterial3D.new(); sm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED; sm.albedo_texture = Mats.tex("scene_earth")
	sm.uv1_scale = Vector3(0.5, 1.0, 1.0); sm.uv1_offset = Vector3(0.25, 0, 0)
	scr.material_override = sm; scr.position = W(21.25, 1.0, 1.7) + Vector3(0, 0, 0.075); root.add_child(scr)
	h.dyn["hub_screen_mat"] = sm
	# retractable partition (Floor plan B): stored in a ceiling pocket, lowers to split the room in two
	var px = 19 * C
	mb.box(Vector3(px, 2.95, 5.0), Vector3(0.22, 0.1, 7.6), Mats.frame())
	var part = Node3D.new(); part.name = "partition"; root.add_child(part)
	var pm = MB.new()
	pm.box(Vector3(px, 1.35, 5.0), Vector3(0.08, 2.7, 7.5), Mats.fabric(Color(0.85, 0.8, 0.72), "fabric_partition"), 0.0, true)
	pm.commit(part, "partition")
	part.position.y = 2.6
	h.dyn["partition"] = part
	label("Floor plan B partition", Vector3(px + 0.06, 2.75, W(0, 2.0).z), PI / 2.0, 18)
	earth_corner()
	# hub control panel
	h.add_screen("hub", Vector3(23 * C - 0.07, 1.4, W(0, 3.0).z), -PI / 2.0, Vector2(0.58, 0.4), Vector2i(580, 400), null)
	_end()

# =====================================================================================================
# GREENHOUSE THERAPY RACKS (biophilic room with a light-pipe dome)
#  NASA need: food, O2 production and CO2 uptake for life support; plant care helped ISS crews (Veggie).
#  Gameplay: tend and harvest 8 racks (4 planted at launch); the engine grows them and counts the food.
#  Interaction: tend / harvest at each rack; LUNA can offer a gardening session to anyone.
#  Why needed: fresh food, cleaner air, and the brief's behavioural treatment for low mood.
# =====================================================================================================
func greenhouse() -> void:
	_begin("Greenhouse_Therapy_Racks")
	var cells = [Vector2i(23, 2), Vector2i(24, 2), Vector2i(26, 2), Vector2i(27, 2), Vector2i(23, 5), Vector2i(24, 5), Vector2i(26, 5), Vector2i(27, 5)]
	h.dyn["racks"] = []
	for k in 8:
		var c = cells[k]
		var p = Vector3((c.x + 0.5) * C, 0, (c.y + 0.5) * C - 0.12)
		# rack frame: four posts + top
		for ox in [-0.54, 0.54]:
			for oz in [-0.26, 0.26]:
				mb.box(p + Vector3(ox, 1.0, oz), Vector3(0.04, 2.0, 0.04), Mats.metal())
		mb.box(p + Vector3(0, 2.02, 0), Vector3(1.12, 0.04, 0.54), Mats.metal())
		mb.box(p + Vector3(0, 0.18, 0), Vector3(1.1, 0.36, 0.5), Mats.grey_plastic(), 0.0, true)            # nutrient tank
		mb.box(p + Vector3(0, 0.2, 0.255), Vector3(0.9, 0.2, 0.01), Mats.water())               # nutrient tank water-level window
		# three growing shelves: NFT channels + a grow-light bar each
		for s in 3:
			var y = 0.62 + s * 0.48
			mb.box(p + Vector3(0, y, 0), Vector3(1.1, 0.03, 0.52), Mats.white_plastic())
			for oz in [-0.12, 0.12]:
				mb.tube_x(p + Vector3(0, y + 0.05, oz), 0.035, 1.06, Mats.white_plastic())              # NFT channels
			mb.box(p + Vector3(0, y + 0.43, 0), Vector3(1.0, 0.025, 0.08), Mats.glow(Color(1.0, 0.45, 0.8), 1.6, "grow_led"))
		var pn = Node3D.new(); pn.name = "plants_%d" % k; root.add_child(pn)
		pn.position = p
		var lab = label("", p + Vector3(0, 2.15, 0.27), 0.0 if c.y == 5 else 0.0, 22)
		h.dyn["racks"].append({"node": pn, "label": lab, "stage": -2, "k": k})
		h.add_inter("rack_%d" % k, p + Vector3(0, 1.1, 0.35), Vector3(1.1, 1.8, 0.4), "", Callable(), "greenhouse")
	# observation bench at the end of the walking path
	var bp = W(23.7, 7.62)
	mb.box(bp + Vector3(0, 0.42, 0), Vector3(1.8, 0.06, 0.45), Mats.wood(), 0.0, true)
	for dx in [-0.75, 0.75]: mb.box(bp + Vector3(dx, 0.2, 0), Vector3(0.08, 0.4, 0.4), Mats.metal())
	h.add_inter("gh_bench", bp + Vector3(0, 0.6, 0), Vector3(1.8, 0.6, 0.5), "", Callable(), "greenhouse")
	# crop-care rover docked at the north wall: leaf camera + nutrient sprayer, works at night
	var rp = W(25.5, 1.35)
	mb.box(rp + Vector3(0, 0.12, 0), Vector3(0.45, 0.14, 0.35), Mats.white_plastic())
	for ox in [-0.2, 0.2]:
		for oz in [-0.15, 0.15]: mb.cyl(rp + Vector3(ox, 0.07, oz), 0.06, 0.04, Mats.rubber(), 10, -1.0, Basis(Vector3.FORWARD, PI / 2.0))
	mb.cyl(rp + Vector3(0, 0.6, 0), 0.02, 0.9, Mats.metal(), 8)
	mb.box(rp + Vector3(0, 1.08, 0.04), Vector3(0.12, 0.08, 0.1), Mats.black())
	mb.box(rp + Vector3(0, 1.08, 0.095), Vector3(0.04, 0.04, 0.01), Mats.glow(Color(0.3, 0.8, 1.0), 2.0))
	label("Crop-care rover\nleaf camera + sprayer · runs at night", rp + Vector3(0, 1.35, 0.1), 0.0, 18)
	_end()

## (re)build one rack's plants for its growth stage
func rack_plants(r: Dictionary, rack: Dictionary) -> void:
	for ch in r.node.get_children(): ch.queue_free()
	var stg = h.sim.hab.rack_stage(rack)
	if stg < 0: return
	var pm = MB.new()
	var crop = rack.crop
	var leaf = Color(0.28, 0.6, 0.24)
	if stg == 5: leaf = Color(0.5, 0.45, 0.25)
	var fruit = {"tomato": Color(0.85, 0.15, 0.1), "radish": Color(0.85, 0.2, 0.45), "potato": Color(0.75, 0.65, 0.4)}.get(crop, Color(0, 0, 0, 0))
	var size = [0.04, 0.07, 0.1, 0.13, 0.15, 0.1][stg]
	for s in 3:
		var y = 0.62 + s * 0.48 + 0.1
		for oz in [-0.12, 0.12]:
			for k in 5:
				var x = -0.44 + k * 0.22 + (0.05 if oz > 0 else 0.0)
				var sc = Vector3(1, 0.8 if crop == "lettuce" else 1.3, 1)
				pm.sphere(Vector3(x, y + size * 0.6, oz), size, Mats.plant(leaf), 8, 4, sc)
				if stg >= 3 and fruit.a > 0.0 and (k + s) % 2 == 0:
					pm.sphere(Vector3(x + 0.05, y + size * 0.4, oz + 0.05), 0.03, Mats.plant(fruit), 6, 3)
	pm.commit(r.node, "plants")

# =====================================================================================================
# MEDICAL · WATER-WALL STORM SHELTER · VR RECOVERY CHAIR
#  NASA need: medical care far from Earth; water as radiation shielding for solar particle events.
#  Gameplay: the flare event (everyone shelters here); LUNA's Tier 2 offers can send you to the chair,
#            which prescribes a calm virtual environment (forest, ocean, Earth view) by stress level.
#  Interaction: sit in the chair, accept or decline LUNA's scene; take shelter in time.
#  Why needed: emergency response + Tier 2 support you can refuse.
# =====================================================================================================
func medical() -> void:
	_begin("Medical_Storm_Shelter")
	var x0 = 1 * C; var x1 = 6 * C; var z0 = 11 * C; var z1 = 18 * C
	# water wall: 25 cm tanks lining the west and south walls (with level sight-glasses)
	for k in 5:
		var zz = z0 + 0.9 + k * 1.3
		mb.box(Vector3(x0 + 0.14, 1.1, zz), Vector3(0.22, 2.15, 1.25), Mats.white_plastic(), 0.0, true)
		mb.box(Vector3(x0 + 0.26, 1.0, zz), Vector3(0.01, 1.7, 0.08), Mats.water())
	for k in 4:
		var xx = x0 + 0.9 + k * 1.3
		mb.box(Vector3(xx, 1.1, z1 - 0.14), Vector3(1.25, 2.15, 0.22), Mats.white_plastic(), 0.0, true)
		mb.box(Vector3(xx, 1.0, z1 - 0.26), Vector3(0.08, 1.7, 0.01), Mats.water())
	label("Water wall · 25 cm of water around the shelter\nshields the crew in a solar particle event", Vector3(x0 + 2.8, 2.35, z1 - 0.27), PI, 20)
	# exam bed + vitals monitor
	var bp = W(3.5, 12.5)
	mb.box(bp + Vector3(0, 0.45, 0), Vector3(2.0, 0.12, 0.75), Mats.mattress(), 0.0, true)
	mb.box(bp + Vector3(0, 0.25, 0), Vector3(1.6, 0.4, 0.5), Mats.white_plastic())
	mb.box(bp + Vector3(1.15, 1.35, -0.32), Vector3(0.45, 0.32, 0.04), Mats.black())
	mb.box(bp + Vector3(1.15, 1.35, -0.295), Vector3(0.4, 0.27, 0.01), Mats.glow(Color(0.1, 0.5, 0.3), 0.9))
	mb.cyl(bp + Vector3(1.15, 0.6, -0.32), 0.025, 1.2, Mats.metal(), 8)
	# medical cabinet
	mb.box(Vector3(x1 - 0.25, 1.1, z0 + 0.5), Vector3(0.45, 1.6, 0.9), Mats.white_plastic(), 0.0, true)
	mb.box(Vector3(x1 - 0.475, 1.5, z0 + 0.5), Vector3(0.01, 0.26, 0.08), Mats.red())
	mb.box(Vector3(x1 - 0.475, 1.5, z0 + 0.5), Vector3(0.01, 0.08, 0.26), Mats.red())
	# shelter seats (fold-down, along the water wall) + 72 h kit
	for k in 4:
		var sx = W(2.5 + k, 0).x
		mb.box(Vector3(sx, 0.45, z1 - 0.5), Vector3(0.55, 0.06, 0.45), Mats.fabric(Color(0.85, 0.45, 0.42), "fabric_med"), 0.0, true)
	mb.box(Vector3(x1 - 0.4, 0.3, z1 - 1.2), Vector3(0.6, 0.6, 0.5), Mats.caution(), 0.0, true)
	label("Shelter kit · 72 h", Vector3(x1 - 0.4, 0.66, z1 - 0.94), 0.0, 18)
	# VR recovery chair: reclined seat under a hood; LUNA picks the scene, you can say no
	var cp = W(1.75, 14.5)
	var b = Basis(Vector3.UP, -PI / 2.0)
	mb.box(cp + Vector3(0, 0.45, 0), Vector3(0.7, 0.12, 0.6), Mats.fabric(Color(0.55, 0.6, 0.7), "fabric_chair"), 0.0, true)
	mb.box(cp + Vector3(-0.32, 0.85, 0), Vector3(0.12, 0.8, 0.6), Mats.fabric(Color(0.55, 0.6, 0.7), "fabric_chair"))
	mb.box(cp + Vector3(0.45, 0.32, 0), Vector3(0.45, 0.1, 0.5), Mats.fabric(Color(0.55, 0.6, 0.7), "fabric_chair"))
	mb.cyl(cp + Vector3(0, 0.2, 0), 0.05, 0.4, Mats.metal(), 8)
	mb.sphere(cp + Vector3(-0.1, 1.45, 0), 0.55, Mats.white_plastic(), 16, 8, Vector3(0.7, 0.55, 1.0))
	mb.box(cp + Vector3(0.18, 1.62, 0.32), Vector3(0.12, 0.04, 0.04), Mats.glow(Color(0.3, 0.8, 1.0), 1.5))
	label("VR recovery chair\nLUNA suggests a scene · you choose", cp + Vector3(0.02, 2.05, 0), PI / 2.0, 20)
	h.add_inter("recovery_chair", cp + Vector3(0.1, 0.8, 0), Vector3(0.9, 1.2, 0.8), "", Callable(), "medical")
	_end()

# =====================================================================================================
# ECLSS RACKS + CIRCADIAN LIGHTING CALIBRATION WALL
#  NASA need: CO2 removal, O2 generation and water recovery (as on the ISS); lighting that protects sleep
#             (the ISS moved to adjustable solid-state lights for this reason).
#  Gameplay: scrubber fault event (swap the sorbent cartridge yourself); live CO2/O2/water; preview and check
#            every room's light colour and brightness against the day schedule.
#  Interaction: rack screen + cartridge slot; calibration wall screen.
#  Why needed: high CO2 slows thinking; lighting is LUNA's main silent lever and needs a human check.
# =====================================================================================================
func eclss() -> void:
	_begin("ECLSS_and_Lighting_Calibration")
	var z0 = 11 * C
	# CO2 removal rack
	var a = W(7.0, 13.0)
	_rack(a, "CO₂ removal · 4-bed molecular sieve")
	mb.box(a + Vector3(0.35, 0.55, 0.52), Vector3(0.42, 0.3, 0.06), Mats.caution())        # sorbent cartridge
	mb.box(a + Vector3(0.35, 0.55, 0.56), Vector3(0.18, 0.04, 0.03), Mats.dark_metal())
	label("Sorbent cartridge", a + Vector3(0.35, 0.78, 0.53), 0.0, 16)
	h.add_inter("cartridge", a + Vector3(0.35, 0.55, 0.7), Vector3(0.5, 0.4, 0.3), "", Callable(), "eclss")
	h.add_screen("eclss", a + Vector3(-0.45, 1.45, 0.53), 0.0, Vector2(0.62, 0.4), Vector2i(620, 400), null)
	var led = StandardMaterial3D.new(); led.emission_enabled = true; led.albedo_color = Color(0.2, 1, 0.4); led.emission = Color(0.2, 1, 0.4); led.emission_energy_multiplier = 2.0; led.resource_name = "scrubber_led"
	mb.box(a + Vector3(0.75, 1.9, 0.52), Vector3(0.06, 0.06, 0.02), led)
	h.dyn["scrubber_led"] = led
	# O2 generation + water recovery rack
	var b = W(10.0, 13.0)
	_rack(b, "O₂ generation (electrolysis) · water recovery 98%")
	for k in 2: mb.cyl(b + Vector3(-0.35 + k * 0.7, 2.25, 0.1), 0.18, 0.5, Mats.metal(), 14)
	# calibration wall: one test tile per zone, lit with that zone's live colour temperature
	var wz = 18 * C - 0.08
	mb.box(Vector3(W(9.0, 0).x, 1.55, wz), Vector3(3.6, 1.7, 0.06), Mats.dark_metal(), 0.0, true)
	var zones = ["pod0", "pod1", "pod2", "pod3", "quarters", "communal", "greenhouse", "work", "exercise", "medical", "corridor", "eclss", "airlock"]
	h.dyn["calib_tiles"] = []
	for k in zones.size():
		var col = k % 7; var row = k / 7
		var tp = Vector3(W(9.0, 0).x + 1.5 - col * 0.5, 2.05 - row * 0.62, wz - 0.04)
		var tmat = StandardMaterial3D.new(); tmat.resource_name = "tile_" + zones[k]; tmat.emission_enabled = true; tmat.albedo_color = Color.WHITE
		mb.box(tp, Vector3(0.36, 0.36, 0.02), tmat)
		var lab = label(zones[k], tp + Vector3(0, -0.25, -0.012), PI, 16, Color(0.85, 0.88, 0.92))
		h.dyn["calib_tiles"].append({"zone": zones[k], "mat": tmat, "label": lab})
	mb.box(Vector3(W(9.0, 0).x, 2.5, wz - 0.06), Vector3(3.4, 0.04, 0.04), Mats.metal())   # spectrometer rail
	mb.box(Vector3(W(8.2, 0).x, 2.5, wz - 0.12), Vector3(0.14, 0.1, 0.1), Mats.black())
	label("Circadian lighting calibration wall · live zone colour + lux", Vector3(W(9.0, 0).x, 2.62, wz - 0.04), PI, 20, Color(0.85, 0.88, 0.92))
	h.add_screen("calib", Vector3(13.3, 1.35, wz - 0.045), PI, Vector2(0.62, 0.42), Vector2i(620, 420), null)
	_end()

func _rack(p: Vector3, text: String) -> void:
	mb.box(p + Vector3(0, 1.0, 0), Vector3(2.1, 2.0, 1.0), Mats.white_plastic(), 0.0, true)
	for k in 4:
		mb.box(p + Vector3(-0.78 + k * 0.52, 1.05, 0.505), Vector3(0.46, 1.7, 0.01), Mats.grey_plastic())
	mb.box(p + Vector3(0, 2.1, 0), Vector3(1.8, 0.2, 0.8), Mats.metal())
	mb.tube_z(p + Vector3(-0.6, 2.25, -0.2), 0.08, 1.0, Mats.metal())
	label(text, p + Vector3(0, 1.98, 0.52), 0.0, 20)

# =====================================================================================================
# MAINTENANCE ROBOT BAY + STORES
#  NASA need: spares, tools and stowage far from resupply (cargo transfer bags as on the ISS).
#  Gameplay: the spare-part count is real (repairs use parts); dispatch the robot to repairs.
#  Interaction: robot bay screen.
#  Why needed: repairs must cost something, and you can see what is left.
# =====================================================================================================
func robot_bay() -> void:
	_begin("Robot_Bay_and_Stores")
	# cargo-transfer-bag shelving (soft stowage, as on the ISS)
	for c in [Vector2i(11, 12), Vector2i(13, 12)]:
		var p = Vector3((c.x + 0.5) * C, 0, (c.y + 0.5) * C - 0.1)
		for s in 4:
			mb.box(p + Vector3(0, 0.05 + s * 0.55, 0), Vector3(1.15, 0.03, 0.55), Mats.metal(), 0.0, s == 0)
			if s < 3:
				for k in 2: mb.box(p + Vector3(-0.28 + k * 0.56, 0.27 + s * 0.55, 0), Vector3(0.52, 0.4, 0.5), Mats.fabric())
		for ox in [-0.56, 0.56]: mb.box(p + Vector3(ox, 0.9, 0), Vector3(0.04, 1.8, 0.55), Mats.metal())
		label("Cargo transfer bags", p + Vector3(0, 1.85, 0.3), 0.0, 18)
	# spare parts shelf: the boxes on it are the real count
	var sp = Vector3(11 * C + 0.27, 0, 15.5 * C)
	mb.box(sp + Vector3(0, 0.9, 0), Vector3(0.5, 1.8, 1.0), Mats.metal(), 0.0, true)
	var parts = Node3D.new(); parts.name = "parts"; parts.position = sp; root.add_child(parts)
	h.dyn["parts_node"] = parts
	label("Spare parts", sp + Vector3(0.26, 1.95, 0), PI / 2.0, 20)
	# maintenance robot on its charging dock
	var dp = W(12.5, 16.5)
	mb.box(dp + Vector3(0, 0.03, 0), Vector3(1.0, 0.06, 1.0), Mats.dark_metal())
	mb.box(dp + Vector3(0, 0.07, -0.42), Vector3(0.3, 0.04, 0.06), Mats.caution())
	var rob = Node3D.new(); rob.name = "maintenance_robot"; rob.position = dp; root.add_child(rob)
	var rm = MB.new()
	rm.box(Vector3(0, 0.18, 0), Vector3(0.6, 0.22, 0.5), Mats.white_plastic())
	for ox in [-0.25, 0.25]:
		for oz in [-0.2, 0.2]: rm.cyl(Vector3(ox, 0.09, oz), 0.09, 0.06, Mats.rubber(), 10, -1.0, Basis(Vector3.FORWARD, PI / 2.0))
	rm.cyl(Vector3(0, 0.65, 0), 0.07, 0.75, Mats.grey_plastic(), 10)
	rm.box(Vector3(0, 1.08, 0.05), Vector3(0.24, 0.14, 0.18), Mats.white_plastic())
	rm.box(Vector3(0, 1.08, 0.145), Vector3(0.16, 0.05, 0.01), Mats.glow(Color(0.3, 0.8, 1.0), 2.0))
	rm.box(Vector3(0.2, 0.85, 0.15), Vector3(0.06, 0.06, 0.4), Mats.metal())
	rm.box(Vector3(0.2, 0.72, 0.38), Vector3(0.06, 0.3, 0.06), Mats.metal())
	rm.box(Vector3(0.2, 0.55, 0.4), Vector3(0.1, 0.06, 0.1), Mats.dark_metal())
	rm.commit(rob, "robot")
	h.dyn["robot"] = rob
	label("Maintenance robot · repairs, inspections, night work", dp + Vector3(0, 1.45, 0), 0.0, 18)
	h.add_screen("robot", Vector3(14 * C - 0.07, 1.45, W(0, 13.6).z), -PI / 2.0, Vector2(0.58, 0.4), Vector2i(580, 400), null)
	_end()

func parts_boxes(n: int) -> void:
	var node: Node3D = h.dyn["parts_node"]
	for ch in node.get_children(): ch.queue_free()
	var pm = MB.new()
	for k in n:
		var row = k / 4; var col = k % 4
		pm.box(Vector3(0.02, 0.12 + row * 0.42, -0.34 + col * 0.23), Vector3(0.4, 0.18, 0.2), Mats.grey_plastic() if k % 3 else Mats.caution())
	pm.commit(node, "parts")

# =====================================================================================================
# OPS: DIGITAL TWIN TABLE · TREND & FORECAST WALL · EMERGENCY COORDINATION CONSOLE · LUNA COMPUTE RACK
#  NASA need: habitat health monitoring and prediction; humans decide, the AI advises.
#  Gameplay: find faults on the twin, read forecasts (habitat + anonymous crew-wide wellbeing),
#            make every event decision with LUNA's predicted outcome per option.
#  Interaction: touch the twin; choose at the console.
#  Why needed: prediction before failure, without breaking hard limit 1 (nobody sees a crewmate's data).
# =====================================================================================================
func ops() -> void:
	_begin("Ops_LUNA")
	var z0 = 11 * C
	# analyst desks with twin monitors: the crew reviews LUNA's advice here
	for cx in [15, 17, 19, 21]:
		var p = Vector3((cx + 0.5) * C, 0, 12.5 * C)
		mb.box(p + Vector3(0, 0.74, 0), Vector3(1.15, 0.04, 0.62), Mats.white_plastic(), 0.0, true)
		for ox in [-0.54, 0.54]: mb.box(p + Vector3(ox, 0.37, 0), Vector3(0.04, 0.74, 0.58), Mats.grey_plastic())
		for ox in [-0.25, 0.25]:
			mb.box(p + Vector3(ox, 1.05, -0.12), Vector3(0.46, 0.3, 0.03), Mats.black())
			mb.box(p + Vector3(ox, 1.05, -0.104), Vector3(0.43, 0.27, 0.005), Mats.glow(Color(0.12, 0.25, 0.45), 0.8))
		_chair(p + Vector3(0, 0, 0.75), PI, Mats.fabric(Color(0.45, 0.6, 0.9), "fabric_ops"))
		h.add_inter("desk", p + Vector3(0, 0.9, 0.5), Vector3(1.0, 0.6, 0.6), "", Callable(), "work")
	# trend & forecast wall (display only) on the east wall, clear of the doors
	mb.box(Vector3(22 * C - 0.05, 2.0, 14.5 * C), Vector3(0.06, 1.5, 7.2), Mats.black())
	h.add_screen("forecast", Vector3(22 * C - 0.085, 2.0, 14.5 * C), -PI / 2.0, Vector2(7.0, 1.35), Vector2i(1920, 370), null, 0.0, false)
	# digital twin table: horizontal touch screen showing the live habitat
	var tp = W(19.0, 14.5)
	mb.box(tp + Vector3(0, 0.45, 0), Vector3(2.0, 0.9, 0.8), Mats.white_plastic(), 0.0, true)
	mb.box(tp + Vector3(0, 0.92, 0), Vector3(2.4, 0.04, 1.15), Mats.dark_metal())
	h.add_screen("twin", tp + Vector3(0, 1.12, -0.1), 0.0, Vector2(2.3, 1.05), Vector2i(1150, 525), null, -1.1)
	label("Digital twin · live habitat", tp + Vector3(0, 0.96, 0.6), 0.0, 20)
	# emergency coordination console (curved: three angled sections)
	var cp = W(18.0, 17.55)
	for k in 3:
		var yaw = (k - 1) * 0.42
		var off = Vector3((k - 1) * 0.95, 0, -abs(k - 1) * 0.25)
		mb.box(cp + off + Vector3(0, 0.45, 0), Vector3(0.98, 0.9, 0.55), Mats.white_plastic(), yaw, true)
		mb.box(cp + off + Vector3(0, 0.92, 0), Vector3(1.0, 0.04, 0.6), Mats.dark_metal(), yaw)
	h.add_screen("console", cp + Vector3(0, 1.36, 0.2), PI, Vector2(0.95, 0.6), Vector2i(950, 600), null)
	h.add_screen("luna_log", cp + Vector3(-0.97, 1.3, -0.08), PI - 0.42, Vector2(0.8, 0.5), Vector2i(800, 500), null)
	label("Emergency coordination · LUNA advises, you decide", cp + Vector3(0, 1.8, 0.2), PI, 22)
	# LUNA compute rack: the AI is software on two redundant flight computers, not a glowing orb
	var lp = W(14.0, 17.0) + Vector3(0.45, 0, 0)
	mb.box(lp + Vector3(0, 1.0, 0), Vector3(0.8, 2.0, 1.2), Mats.dark_metal(), 0.0, true)
	for k in 8:
		mb.box(lp + Vector3(0.405, 0.35 + k * 0.2, 0), Vector3(0.01, 0.12, 1.05), Mats.black())
		for j in 4: mb.box(lp + Vector3(0.412, 0.35 + k * 0.2, -0.4 + j * 0.06), Vector3(0.005, 0.02, 0.02), Mats.glow(Color(0.2, 0.9, 0.4) if (j + k) % 5 else Color(0.3, 0.6, 1.0), 2.5))
	label("LUNA · 2 redundant flight computers · 140 W\nsoftware only: it cannot touch medical or life-safety systems", lp + Vector3(0.42, 2.15, 0), PI / 2.0, 18, Color(0.15, 0.17, 0.2))
	wellness_center()
	_end()

# =====================================================================================================
# EXERCISE BAY: treadmill with load harness, cycle ergometer with scenic display, resistance rig
#  NASA need: daily exercise against bone and muscle loss (ISS: T2 treadmill, CEVIS bike, ARED).
#  Gameplay: your exercise slot; LUNA's "scenic ride" offer for monotony.
#  Interaction: ride, run, lift.
#  Why needed: exercise treats low mood, fatigue and conflict in the engine (and in life).
# =====================================================================================================
func exercise() -> void:
	_begin("Exercise_Bay")
	var z0 = 11 * C
	# treadmill with load harness (ISS T2 style: bungees hold you on the belt)
	var tp = W(23.5, 14.0)
	mb.box(tp + Vector3(0, 0.12, 0), Vector3(0.8, 0.24, 1.9), Mats.dark_metal(), 0.0, true)
	mb.box(tp + Vector3(0, 0.25, 0), Vector3(0.6, 0.02, 1.6), Mats.rubber())
	for ox in [-0.42, 0.42]: mb.box(tp + Vector3(ox, 1.1, -0.2), Vector3(0.05, 1.9, 0.05), Mats.metal())
	mb.box(tp + Vector3(0, 2.05, -0.2), Vector3(0.9, 0.05, 0.05), Mats.metal())
	for ox in [-0.2, 0.2]: mb.box(tp + Vector3(ox, 1.6, -0.2), Vector3(0.015, 0.9, 0.015), Mats.caution())   # harness bungees
	mb.box(tp + Vector3(0, 1.2, -0.85), Vector3(0.5, 0.3, 0.05), Mats.black())
	label("Treadmill + load harness", tp + Vector3(0, 2.2, -0.2), 0.0, 18)
	h.add_inter("treadmill", tp + Vector3(0, 0.9, 0), Vector3(0.8, 1.2, 1.6), "", Callable(), "exercise")
	# cycle ergometer (ISS CEVIS style)
	var bp = W(25.5, 13.6)
	mb.box(bp + Vector3(0, 0.35, 0.2), Vector3(0.3, 0.7, 0.5), Mats.dark_metal(), 0.0, true)
	mb.box(bp + Vector3(0, 0.62, 0.4), Vector3(0.36, 0.08, 0.36), Mats.fabric(Color(0.3, 0.3, 0.32), "fabric_bike"))
	mb.cyl(bp + Vector3(0, 0.45, -0.2), 0.22, 0.08, Mats.metal(), 16, -1.0, Basis(Vector3.FORWARD, PI / 2.0))
	mb.box(bp + Vector3(0, 0.95, -0.25), Vector3(0.45, 0.04, 0.04), Mats.black())
	label("Cycle ergometer", bp + Vector3(0, 1.25, -0.3), 0.0, 18)
	h.add_inter("bike", bp + Vector3(0, 0.8, 0.2), Vector3(0.6, 1.0, 0.9), "", Callable(), "exercise")
	# resistance rig with vacuum cylinders (ISS ARED style)
	var rp = W(27.5, 14.0)
	mb.box(rp + Vector3(0, 0.06, 0), Vector3(1.0, 0.12, 1.4), Mats.dark_metal(), 0.0, true)
	for ox in [-0.4, 0.4]: mb.cyl(rp + Vector3(ox, 0.8, -0.5), 0.09, 1.4, Mats.metal(), 12)
	mb.box(rp + Vector3(0, 1.45, -0.3), Vector3(1.3, 0.04, 0.04), Mats.metal())
	label("Resistance rig (vacuum cylinders)", rp + Vector3(0, 1.75, -0.5), 0.0, 18)
	h.add_inter("rig", rp + Vector3(0, 0.9, 0), Vector3(1.0, 1.2, 1.2), "", Callable(), "exercise")
	# Earth-view / scenic display for the bike
	mb.box(W(26.0, 0, 1.75) + Vector3(0, 0, z0 + 0.05), Vector3(3.2, 1.6, 0.06), Mats.black())
	var scr = MeshInstance3D.new(); var qm = QuadMesh.new(); qm.size = Vector2(3.1, 1.5); scr.mesh = qm
	var sm = StandardMaterial3D.new(); sm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED; sm.albedo_texture = Mats.tex("scene_earth")
	sm.uv1_scale = Vector3(0.5, 1.0, 1.0); sm.uv1_offset = Vector3(0.25, 0, 0)
	scr.material_override = sm; scr.position = W(26.0, 0, 1.75) + Vector3(0, 0, z0 + 0.085); root.add_child(scr)
	h.dyn["ex_screen_mat"] = sm
	wellness_dock()
	_end()

# =====================================================================================================
# HYGIENE
#  NASA need: hygiene + water recovery (wash water is reclaimed). Privacy: no voice or camera sensors.
#  Gameplay: morning routine (a small lift to how you start the day).  Interaction: wash up.
# =====================================================================================================
func hygiene() -> void:
	_begin("Hygiene")
	var x0 = 4 * C; var z0 = 1 * C
	# shower stall: glass screens, shower head, drain tray
	mb.box(Vector3(x0 + 0.62, 1.1, z0 + 0.62), Vector3(1.15, 2.2, 0.03), Mats.glass())
	mb.box(Vector3(x0 + 1.2, 1.1, z0 + 0.6), Vector3(0.03, 2.2, 1.15), Mats.glass())
	mb.cyl(Vector3(x0 + 0.5, 2.1, z0 + 0.4), 0.12, 0.04, Mats.metal(), 12)
	mb.box(Vector3(x0 + 0.6, 0.03, z0 + 0.6), Vector3(1.1, 0.06, 1.1), Mats.white_plastic())
	# toilet (urine is processed back into water)
	var tp = W(6.5, 1.5)
	mb.box(tp + Vector3(0, 0.25, -0.15), Vector3(0.45, 0.5, 0.55), Mats.white_plastic(), 0.0, true)
	mb.box(tp + Vector3(0, 0.75, -0.48), Vector3(0.5, 0.6, 0.12), Mats.white_plastic())
	# hand-wash sink + mirror
	var sp = W(5.5, 1.3)
	mb.box(sp + Vector3(0, 0.85, -0.1), Vector3(0.6, 0.1, 0.45), Mats.white_plastic(), 0.0, true)
	mb.box(sp + Vector3(0, 1.35, -0.32), Vector3(0.5, 0.6, 0.02), Mats.metal())
	label("Wash water is reclaimed (98%)", sp + Vector3(0, 1.75, -0.31), 0.0, 18)
	h.add_inter("sink", sp + Vector3(0, 1.0, 0.1), Vector3(0.7, 0.6, 0.5), "", Callable(), "hygiene")
	_end()


# =====================================================================================================
# EVA PREPARATION STATION (airlock, between the suitports)
#  NASA need: suits are serviced between EVAs (O2 recharge, cooling water, batteries); EVA readiness checks.
#  Gameplay: before any EVA you run the pre-EVA check: suit consumables + LUNA's read on your fatigue,
#            stress and sleep. LUNA advises go / wait; you decide.
#  Interaction: prep panel screen; umbilical hoses run to both suitports.
#  Why needed: EVA is the riskiest thing the crew does; wellness should be checked before it, not after.
# =====================================================================================================
func eva_prep(x0: float, z0: float) -> void:
	var cx = x0 + 1.5 * C
	# suit service panel: O2, cooling-water and power umbilicals with status LEDs
	mb.box(Vector3(cx, 1.15, z0 + 0.12), Vector3(0.9, 1.5, 0.18), Mats.white_plastic(), 0.0, true)
	for k in 2:
		var tx = cx - 0.25 + k * 0.5
		mb.cyl(Vector3(tx, 0.62, z0 + 0.26), 0.07, 0.3, Mats.metal(), 10)                         # O2 + water quick-disconnects
		mb.box(Vector3(tx, 0.45, z0 + 0.26), Vector3(0.05, 0.05, 0.05), Mats.glow(Color(0.2, 0.9, 0.5), 1.5) if k == 0 else Mats.glow(Color(0.3, 0.6, 1.0), 1.5))
	for side in [-1.0, 1.0]:   # umbilicals to the two suitports
		mb.tube_x(Vector3(cx + side * 0.75, 0.85, z0 + 0.2), 0.025, 0.65, Mats.cable(Color("#2b6fd6") if side < 0 else Color("#e9edf2")), 0.0, 8)
	label("EVA prep · O₂ recharge · cooling water · readiness check", Vector3(cx, 2.02, z0 + 0.22), 0.0, 18)
	h.add_screen("eva_prep", Vector3(cx, 1.38, z0 + 0.215), 0.0, Vector2(0.62, 0.42), Vector2i(620, 420), null)

# =====================================================================================================
# EARTH CONNECTION CORNER (social hub, east half)
#  NASA need: isolation from Earth is a core long-duration stressor; contact with home protects mood.
#  Gameplay: sit by the virtual window (live Earth view) and read / answer family messages.
#  Interaction: armchairs by the window; family message station screen.
#  Why needed: the brief's connection lever, made into a place you choose to go.
# =====================================================================================================
func earth_corner() -> void:
	var wx = 23 * C - 0.08
	# virtual window frame around a high-resolution Earth-view panel on the east wall
	mb.box(Vector3(wx, 1.7, 5.6), Vector3(0.06, 1.5, 2.3), Mats.frame())
	var scr = MeshInstance3D.new(); var qm = QuadMesh.new(); qm.size = Vector2(2.1, 1.3); scr.mesh = qm
	var sm = StandardMaterial3D.new(); sm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED; sm.albedo_texture = Mats.tex("scene_earth")
	sm.uv1_scale = Vector3(0.5, 0.9, 1.0); sm.uv1_offset = Vector3(0.25, 0.04, 0)
	scr.material_override = sm; scr.position = Vector3(wx - 0.035, 1.7, 5.6); scr.rotation.y = -PI / 2.0; root.add_child(scr)
	h.dyn["earth_window_mat"] = sm
	label("Earth window · live view from the south-pole camera", Vector3(wx - 0.04, 2.6, 5.6), -PI / 2.0, 18)
	for k in 2:
		var cp = Vector3(wx - 1.4, 0, 5.0 + k * 1.2)
		var b = Basis(Vector3.UP, -PI / 2.0)
		mb.box(cp + Vector3(0, 0.25, 0), Vector3(0.75, 0.5, 0.75), Mats.fabric(Color(0.5, 0.62, 0.8), "fabric_earth"), 0.0, true)
		mb.box(cp + Vector3(-0.32, 0.62, 0), Vector3(0.14, 0.62, 0.75), Mats.fabric(Color(0.5, 0.62, 0.8), "fabric_earth"))
	h.add_inter("earth_window", Vector3(wx - 1.4, 0.7, 5.6), Vector3(0.9, 0.8, 2.0), "", Callable(), "communal")
	# family message station
	var fp = Vector3(wx - 0.3, 0, 8.6)
	mb.box(fp + Vector3(0, 0.74, 0), Vector3(0.55, 0.04, 0.9), Mats.white_plastic(), 0.0, true)
	mb.box(fp + Vector3(0.05, 0.37, 0), Vector3(0.45, 0.74, 0.04), Mats.grey_plastic())
	mb.torus(fp + Vector3(-0.05, 0.82, 0.3), 0.05, 0.075, Mats.black(), Basis(Vector3.FORWARD, PI / 2.0))   # headset
	h.add_screen("family", fp + Vector3(0.22, 1.25, 0), -PI / 2.0, Vector2(0.62, 0.42), Vector2i(620, 420), null)

# =====================================================================================================
# WELLNESS OPERATIONS CENTER additions (the flagship room: Ops)
#  NASA need: crew health monitoring and planning; the AI shows its work and humans plan the response.
#  Gameplay: a live 3D twin of the habitat (crew pucks move, rooms light up with their real lighting),
#            the crew-wide intervention planner (movie night, shared meals, a free afternoon, greenhouse
#            session) and the SELENE live-sensor status.
#  Interaction: watch the twin; plan crew-wide interventions at the planning station.
#  Why needed: this room is where the whole project is visible: sensing -> AI -> human decisions.
# =====================================================================================================
func wellness_center() -> void:
	# 3D twin plinth: a 1:30 model of the habitat on a slow turntable
	var pp = Vector3(16.5 * C, 0, 14.5 * C)
	mb.cyl(pp + Vector3(0, 0.45, 0), 0.62, 0.9, Mats.white_plastic(), 24)
	mb.cyl(pp + Vector3(0, 0.92, 0), 0.72, 0.04, Mats.dark_metal(), 32)
	mb.torus(pp + Vector3(0, 0.95, 0), 0.7, 0.74, Mats.glow(Color(0.3, 1.0, 0.5), 0.5, "twin_ring"))   # habitat status ring: green OK, yellow warning, red emergency
	var tw = Node3D.new(); tw.name = "twin3d"; tw.position = pp + Vector3(0, 0.96, 0); root.add_child(tw)
	h.dyn["twin3d"] = tw
	label("3D digital twin · 1:30 · live", pp + Vector3(0, 0.6, 0.66), 0.0, 18)
	# intervention planning station (standing desk, crew-wide plans only)
	var sp = Vector3(26.2, 0, 22.05)
	mb.box(sp + Vector3(0, 0.5, 0), Vector3(0.9, 1.0, 0.5), Mats.white_plastic(), 0.0, true)
	mb.box(sp + Vector3(0, 1.02, 0), Vector3(0.95, 0.04, 0.55), Mats.dark_metal())
	h.add_screen("plan", sp + Vector3(0, 1.45, -0.1), PI, Vector2(0.75, 0.48), Vector2i(750, 480), null, -0.35)
	label("Intervention planning · crew-wide, no names", sp + Vector3(0, 1.85, -0.1), PI, 18)

# =====================================================================================================
# WELLNESS DOCK (exercise bay): posture camera arch, finger-clip heart rate / SpO2, Reaction Self Test
#  NASA need: ISS crews take a short reaction-time test (the Reaction Self Test, a psychomotor
#             vigilance task) to catch fatigue; posture and heart rate are routine health checks.
#  Gameplay: a 1-minute scan + a reaction test; your result updates LUNA's fatigue estimate (your consent:
#            you chose to take it) and you get a wellness report.
#  Interaction: step on the dock, use the screen (trigger / click when the light turns green).
#  Why needed: a direct, measurable fatigue signal that the person controls.
# =====================================================================================================
func wellness_dock() -> void:
	var dp = Vector3(25.5 * C, 0, 16.8 * C)
	# dock platform: stand on the ring for the 1-minute scan (ring = standby blue)
	mb.cyl(dp + Vector3(0, 0.04, 0), 0.6, 0.08, Mats.dark_metal(), 24, -1.0, Basis(), )
	mb.torus(dp + Vector3(0, 0.085, 0), 0.5, 0.56, Mats.glow(Color(0.25, 0.6, 1.0), 0.5, "dock_ring"))
	# posture camera arch
	for side in [-1.0, 1.0]: mb.box(dp + Vector3(side * 0.75, 1.1, 0.25), Vector3(0.08, 2.2, 0.12), Mats.white_plastic())
	mb.box(dp + Vector3(0, 2.25, 0.25), Vector3(1.6, 0.12, 0.14), Mats.white_plastic())
	for k in 3: mb.box(dp + Vector3(-0.5 + k * 0.5, 2.18, 0.17), Vector3(0.1, 0.06, 0.05), Mats.black())
	# console with finger clip
	mb.box(dp + Vector3(0, 0.5, -0.75), Vector3(0.7, 1.0, 0.35), Mats.white_plastic(), 0.0, true)
	mb.box(dp + Vector3(0.22, 1.03, -0.68), Vector3(0.1, 0.05, 0.12), Mats.grey_plastic())
	label("Wellness Dock · posture · heart rate · Reaction Self Test (as on the ISS)", dp + Vector3(0, 2.45, 0.25), 0.0, 18)
	h.add_screen("dock", dp + Vector3(0, 1.4, -0.8), 0.0, Vector2(0.7, 0.46), Vector2i(700, 460), null, -0.25)
	h.add_inter("dock", dp + Vector3(0, 0.5, 0), Vector3(1.0, 1.0, 1.0), "", Callable(), "exercise")
