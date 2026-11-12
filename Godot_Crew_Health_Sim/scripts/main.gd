# ==============================================================================================
#  PROJECT ARES · NASA HUNCH 2025–26 · Crew Health Sim (Godot 4 · desktop + web, no VR)
#  File    : scripts/main.gd
#  Purpose : Builds the habitat module + lunar surface, the first-person player, every interaction,
#            the daily task list and all HUD panels:
#              Smart sleep bunk  → sleep simulation (stages, hypnogram, sleep score)
#              Night wristband   → wear it so the night is tracked
#              Crew Health Station → check-in: live ECG, blood pressure, SpO2, temperature
#              Zone sensor pod   → mood detector (voice + face, shared zones only, consent switch)
#              Cortisol patch    → stress hormone over 48 h, replace when expired
#              Crew beacon       → chest HR/HRV/skin response, always on
#              EVA suit          → spacewalk with suit heart-rate + blood-pressure detector
#              TAB               → "How is the astronaut feeling?" fusion of all devices
#  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
# ==============================================================================================
extends Node3D

const R_MOD := 9.0          # module radius (m)
const H_MOD := 4.2          # module height
const SURFACE := Vector3(80, 0, 0)
const G_MOON := 1.62

var A = Astro.new()
var AI: Ares                      # Project ARES rubric layer (sensors → conditions → tiers, limits, Board Q1–Q6)
var ares_tab = "dash"
## Why each object exists: the line in the research proposal it answers. Nothing is in the base without one.
const REQ = {
	"bunk": ["Crew Quarters", "Private sleep pod; sleep monitoring (proposal: circadian disruption countermeasure)"],
	"lights": ["Crew Quarters", "Adaptive lighting tied to circadian rhythm (proposal §2A Personalization)"],
	"wristband": ["Biomedical", "Simulated wearable data: sleep stages (proposal Deliverables: biomedical simulation)"],
	"beacon": ["Biomedical", "Wearable heart rate / HRV / skin response (proposal §5b data sources)"],
	"station": ["Medical", "Medical / first aid zone: daily vitals (proposal NASA functional zones)"],
	"patch": ["Biomedical", "Stress biomarker; individualized crew profile (proposal §5b)"],
	"pod": ["AI monitoring", "Environmental sensors + mood assessment in shared zones only (proposal §5b privacy & ethics)"],
	"console": ["Workstation", "Workstations + AI dashboard + daily journal for language-based mood check (proposal §3)"],
	"plants": ["Greenhouse", "Food + O2, stress reduction, hobby, AI-linked (greenhouse requirements §2, §4)"],
	"vr": ["Countermeasure", "VR for relaxation (proposal §5d countermeasures)"],
	"treadmill": ["Exercise", "Exercise countermeasure; bone and mood in 1/6 g (proposal §5d)"],
	"suit": ["Airlock", "EVA with suit biomedical monitoring (proposal: biomedical monitoring)"],
	"dust": ["Airlock / dust mitigation", "Lunar dust irritates lungs and eyes: clean the suit before it reaches the cabin (proposal: dust mitigation)"],
	"hygiene": ["Hygiene", "Hygiene zone, private: no cameras or mics (proposal zoning + hard limit 6)"],
	"hatch": ["Airlock", "Airlock between surface and habitat (proposal NASA functional zones)"],
	"sample": ["EVA checkpoint", "EVA walk that drives suit heart rate and blood pressure (biomedical monitoring)"],
}
var used = {}
var light_mode = 0                 # 0 auto circadian · 1 focus 6500 K · 2 wind-down 1800 K
var suit_dust = 0.0
var cabin_dust = 0.0
var ares_msg = ""
var case_running = false
var case_msg = ""
var checkin = {}                 # last health-station check-in
var checkins_today = 0
var speeds = [1.0, 5.0, 20.0]    # sim minutes per real second (awake)
var speed_i = 0
var samples = 0
var sample_nodes: Array = []
var tasks = {}
var task_day = 0

# player
var player: CharacterBody3D
var cam: Camera3D
var yaw = 0.0
var pitch = 0.0
var looking_at: Node = null

# world bits that change
var bunk_light: OmniLight3D
var led_mats: Array = []
var warn_light: OmniLight3D
var on_treadmill = false

# UI
var ui: CanvasLayer
var hud_clock: Label
var hud_tasks: RichTextLabel
var hud_vitals: Label
var hud_prompt: Label
var hud_log: Label
var hud_eva: PanelContainer
var hud_eva_text: RichTextLabel
var hud_feel: Label
var panel: PanelContainer
var panel_box: VBoxContainer
var panel_kind = ""
var ecg_t = 0.0
var sleep_fast = false

func _ready() -> void:
	AI = Ares.new(A)
	randomize()
	_env()
	_habitat()
	_surface()
	_player()
	_ui()
	_new_day_tasks()
	A.note("Day 1 — welcome aboard. Press H for help.")
	_open_panel("help")
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--shots="): _shots(a.substr(8))
		if a == "--autotest": _autotest()
		if a.begins_with("--hero="): _hero(a.substr(7))

# =============================================================================================
#  WORLD
# =============================================================================================
func _env() -> void:
	var we = WorldEnvironment.new(); var e = Environment.new()
	e.background_mode = Environment.BG_COLOR; e.background_color = Color(0.0, 0.0, 0.01)
	e.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR; e.ambient_light_color = Color(0.32, 0.36, 0.42); e.ambient_light_energy = 0.6
	e.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	e.glow_enabled = true; e.glow_intensity = 0.6; e.glow_bloom = 0.05
	we.environment = e; add_child(we)
	var sun = DirectionalLight3D.new(); sun.rotation_degrees = Vector3(-8, -60, 0); sun.light_energy = 1.6; sun.shadow_enabled = true
	sun.light_color = Color(1.0, 0.97, 0.92); add_child(sun)

func _habitat() -> void:
	var hab = Node3D.new(); hab.name = "Habitat"; add_child(hab)
	var wall_m = Build.mat(Color(0.86, 0.87, 0.85), 0.0, 0.85)
	var floor_m = Build.mat(Color(0.33, 0.35, 0.38), 0.2, 0.8)
	# floor + ceiling
	var fl = Build.cyl(hab, R_MOD, 0.1, Vector3(0, -0.05, 0), floor_m, 48)
	Build.collider(fl, Vector3(R_MOD * 2, 0.1, R_MOD * 2))
	Build.cyl(hab, R_MOD, 0.1, Vector3(0, H_MOD, 0), Build.mat(Color(0.78, 0.8, 0.8), 0.0, 0.9), 48)
	# floor grating rings
	for r in [3.0, 6.0]:
		var ring = MeshInstance3D.new(); var tm = TorusMesh.new(); tm.inner_radius = r - 0.03; tm.outer_radius = r + 0.03; ring.mesh = tm
		ring.material_override = Build.mat(Color(0.5, 0.52, 0.55), 0.6, 0.4); ring.scale = Vector3(1, 0.2, 1); hab.add_child(ring); ring.position.y = 0.01
	# curved wall: panels with colliders, a gap for the airlock at +x
	var n = 36
	for i in n:
		var a = TAU * (i + 0.5) / n
		if absf(wrapf(a, -PI, PI)) < 0.18: continue   # airlock opening
		var p = Vector3(cos(a), 0, sin(a)) * R_MOD
		var w = R_MOD * TAU / n * 1.04
		var panel_mi = Build.box(hab, Vector3(0.12, H_MOD, w), p + Vector3(0, H_MOD / 2, 0), wall_m, true, -a)
		# wall ribs
		Build.box(hab, Vector3(0.18, H_MOD, 0.08), Vector3(cos(TAU * i / n), 0, sin(TAU * i / n)) * (R_MOD - 0.05) + Vector3(0, H_MOD / 2, 0), Build.mat(Color(0.62, 0.64, 0.67), 0.5, 0.4), false, -TAU * i / n)
	# ---- LED lighting: ceiling channel ring, floor pathway strips (circadian colour) ----
	for r in [R_MOD - 0.6, 2.2]:
		var led = MeshInstance3D.new(); var tm = TorusMesh.new(); tm.inner_radius = r - 0.04; tm.outer_radius = r + 0.04; tm.ring_segments = 8; tm.rings = 64
		led.mesh = tm; var lm = Build.mat(Color(1, 0.95, 0.85), 0.0, 0.5, 2.2).duplicate(); led.material_override = lm
		led.scale = Vector3(1, 0.25, 1); hab.add_child(led); led.position.y = H_MOD - 0.06; led_mats.append(lm)
	var path = MeshInstance3D.new(); var ptm = TorusMesh.new(); ptm.inner_radius = 4.45; ptm.outer_radius = 4.5; ptm.rings = 64; path.mesh = ptm
	var pm = Build.mat(Color(0.3, 0.7, 1.0), 0.0, 0.5, 1.4).duplicate(); path.material_override = pm; path.scale = Vector3(1, 0.1, 1); hab.add_child(path); path.position.y = 0.02; led_mats.append(pm)
	for p in [Vector3(0, 3.6, 0), Vector3(-5.5, 3.6, 0), Vector3(0, 3.6, 5.5), Vector3(0, 3.6, -5.5), Vector3(5.5, 3.6, 0)]:
		var o = OmniLight3D.new(); o.position = p; o.omni_range = 9.0; o.light_energy = 1.1; o.light_color = Color(1, 0.96, 0.9); hab.add_child(o)
	warn_light = OmniLight3D.new(); warn_light.position = Vector3(0, 3.5, 0); warn_light.omni_range = 14; warn_light.light_color = Color(1, 0.15, 0.1); warn_light.light_energy = 0.0; hab.add_child(warn_light)

	# ---- WIRES: ceiling cable tray all round + drops to every device ----
	Build.cable_ring(hab, R_MOD - 0.45, H_MOD - 0.45, 0.25, TAU - 0.25)
	# tray hangers + service loops: cables dip out of the tray to wall junction boxes every 20°
	var loop_cols = [Color(0.08, 0.08, 0.09), Color(0.75, 0.12, 0.1), Color(0.15, 0.35, 0.85), Color(0.95, 0.8, 0.15)]
	for i in 18:
		var a = 0.35 + (TAU - 0.7) * i / 17.0
		var t0 = Vector3(cos(a), 0, sin(a)) * (R_MOD - 0.45) + Vector3(0, H_MOD - 0.48, 0)
		var w0 = Vector3(cos(a + 0.05), 0, sin(a + 0.05)) * (R_MOD - 0.12) + Vector3(0, H_MOD - 1.1, 0)
		Build.box(hab, Vector3(0.08, 0.35, 0.05), Vector3(cos(a), 0, sin(a)) * (R_MOD - 0.45) + Vector3(0, H_MOD - 0.25, 0), Build.mat(Color(0.5, 0.52, 0.55), 0.6, 0.4), false, -a)
		Build.sag_cable(hab, t0, w0, 0.18 + 0.05 * (i % 3), 0.015, loop_cols[i % 4])
		Build.box(hab, Vector3(0.1, 0.22, 0.22), w0 + Vector3(cos(a), 0, sin(a)) * 0.05, Build.mat(Color(0.3, 0.32, 0.36), 0.5, 0.4), false, -a)
	Build.cable_ring(hab, 3.4, H_MOD - 0.2, 0.0, TAU)

	# ---- Crew quarters (private) at −x : smart sleep bunk, wristband dock, crew beacon dock ----
	Build.label3d(hab, "CREW QUARTERS · private", Vector3(-7.2, 3.2, 0), 0.9, Color(1, 0.8, 0.5))
	var bunk = Build.model(hab, "res://models/sleep_bunk.glb", 2.3, Vector3(-7.3, 0, 0), PI / 2)
	_interact(bunk, "bunk", "Sleep in the smart bunk", Vector3(2.3, 1.2, 2.3))
	bunk_light = OmniLight3D.new(); bunk_light.position = Vector3(-7.0, 1.6, 0); bunk_light.omni_range = 3.5; bunk_light.light_color = Color(1, 0.6, 0.3); bunk_light.light_energy = 0.3; hab.add_child(bunk_light)
	# privacy curtain
	for z in range(-12, -1, 2):   # curtain half drawn
		Build.box(hab, Vector3(0.03, 2.4, 0.2), Vector3(-5.6, 1.25, z * 0.1), Build.mat(Color(0.3, 0.42, 0.6, 0.85)), false, 0.25 * (z % 4 - 1))
	Build.box(hab, Vector3(0.6, 0.05, 1.0), Vector3(-6.2, 1.05, 1.8), Build.mat(Color(0.5, 0.52, 0.56), 0.4, 0.5), true)  # shelf
	var band = Build.model(hab, "res://models/sleep_wristband.glb", 0.28, Vector3(-6.2, 1.08, 1.6))
	_interact(band, "wristband", "Put on / take off the night wristband", Vector3(0.5, 0.4, 0.5))
	var beacon = Build.model(hab, "res://models/crew_beacon.glb", 0.3, Vector3(-6.2, 1.08, 2.05))
	_interact(beacon, "beacon", "Crew beacon (chest) — wear / dock", Vector3(0.5, 0.4, 0.5))
	Build.label3d(hab, "wristband · beacon", Vector3(-6.2, 1.55, 1.8), 0.5)
	# wires: bunk sensor mat + dock → ceiling tray
	Build.routed_cable(hab, [Vector3(-8.3, 0.35, -0.6), Vector3(-8.45, 0.35, -0.6), Vector3(-8.45, H_MOD - 0.3, -0.6)], 0.018, Color(0.15, 0.35, 0.85))
	Build.routed_cable(hab, [Vector3(-6.2, 1.03, 2.3), Vector3(-6.2, 1.03, 2.6), Vector3(-8.4, 1.03, 2.6), Vector3(-8.45, H_MOD - 0.3, 2.6)], 0.012, Color(0.9, 0.9, 0.92))

	# ---- Medical bay (+z) : Crew Health Station ----
	Build.label3d(hab, "MEDICAL · Crew Health Station", Vector3(0, 3.2, 7.2), 0.9)
	var hs = Build.model(hab, "res://models/health_station.glb", 2.0, Vector3(0, 0, 7.4), PI)
	_interact(hs, "station", "Daily check-in at the Crew Health Station", Vector3(2.2, 2.0, 1.4))
	Build.sag_cable(hab, Vector3(-0.6, H_MOD - 0.3, 8.2), Vector3(-0.6, 1.9, 7.9), 0.25, 0.022, Color(0.08, 0.08, 0.09))
	Build.sag_cable(hab, Vector3(-0.4, H_MOD - 0.3, 8.25), Vector3(-0.4, 1.9, 7.95), 0.3, 0.016, Color(0.15, 0.35, 0.85))
	Build.sag_cable(hab, Vector3(0.5, H_MOD - 0.3, 8.2), Vector3(0.5, 1.9, 7.9), 0.2, 0.016, Color(0.95, 0.8, 0.15))
	# ECG lead + BP cuff hose hanging off the station
	Build.sag_cable(hab, Vector3(0.7, 1.2, 7.0), Vector3(1.0, 0.9, 6.6), 0.35, 0.01, Color(0.85, 0.85, 0.85))
	Build.sag_cable(hab, Vector3(-0.7, 1.2, 7.0), Vector3(-1.0, 0.8, 6.7), 0.4, 0.013, Color(0.3, 0.3, 0.32))

	# ---- Wardroom (centre, shared) : table, cortisol patches, zone sensor pod, crew console ----
	Build.label3d(hab, "WARDROOM · shared zone", Vector3(0, 3.2, 0), 0.9, Color(0.6, 1.0, 0.75))
	var top = Build.cyl(hab, 1.1, 0.06, Vector3(0, 0.95, 0), Build.mat(Color(0.92, 0.93, 0.95), 0.1, 0.4), 32)
	Build.collider(top, Vector3(2.0, 0.1, 2.0))
	Build.cyl(hab, 0.12, 0.95, Vector3(0, 0.47, 0), Build.mat(Color(0.5, 0.52, 0.55), 0.7, 0.3))
	Build.collider(hab.get_child(hab.get_child_count() - 1) as Node3D, Vector3(0.3, 0.95, 0.3))
	var patch = Build.model(hab, "res://models/cortisol_patch.glb", 0.22, Vector3(0.45, 0.98, 0.1))
	_interact(patch, "patch", "Fit a fresh cortisol patch", Vector3(0.45, 0.4, 0.45))
	var pod = Build.model(hab, "res://models/zone_sensor_pod.glb", 0.55, Vector3(0, H_MOD - 0.7, 0))
	_interact(pod, "pod", "Zone sensor pod — mood detector", Vector3(0.8, 0.8, 0.8))
	Build.routed_cable(hab, [Vector3(0, H_MOD - 0.2, 0.0), Vector3(0, H_MOD - 0.2, 3.3)], 0.02, Color(0.2, 0.7, 0.35))
	# crew console on the wall (−z side of wardroom)
	var con = Build.box(hab, Vector3(1.6, 1.0, 0.12), Vector3(3.5, 1.5, -7.6), Build.mat(Color(0.05, 0.08, 0.12), 0.3, 0.3), false, -0.4)
	Build.box(hab, Vector3(1.45, 0.85, 0.02), Vector3(3.47, 1.5, -7.53), Build.mat(Color(0.15, 0.6, 0.95), 0.0, 0.3, 1.2), false, -0.4)
	_interact(con, "console", "Crew console — meal, call home, work, CO2 drill", Vector3(1.7, 1.1, 0.5))
	Build.label3d(hab, "CREW CONSOLE", Vector3(3.5, 2.2, -7.5), 0.6)
	Build.routed_cable(hab, [Vector3(3.9, 1.0, -7.75), Vector3(3.9, 0.06, -7.75), Vector3(4.6, 0.06, -7.3), Vector3(4.9, 0.06, -7.1), Vector3(5.0, H_MOD - 0.3, -7.25)], 0.02, Color(0.08, 0.08, 0.09))

	# ---- Greenhouse corner (shared): plant care is a countermeasure (research: biophilic support) ----
	Build.label3d(hab, "GREENHOUSE · plant care", Vector3(5.6, 2.9, 5.0), 0.8, Color(0.6, 1.0, 0.6))
	var rack = Build.box(hab, Vector3(1.8, 1.6, 0.6), Vector3(5.6, 0.8, 5.6), Build.mat(Color(0.75, 0.77, 0.8), 0.5, 0.4), false, -0.8)
	for sy in [0.45, 0.95, 1.45]:
		Build.box(rack, Vector3(1.7, 0.04, 0.55), Vector3(0, sy - 0.8, 0), Build.mat(Color(0.35, 0.25, 0.18)))
		for sx in range(-3, 4):
			Build.sphere(rack, 0.1 + 0.03 * ((sx + 3) % 3), Vector3(sx * 0.24, sy - 0.66, 0), Build.mat(Color(0.25, 0.7 + 0.05 * (sx % 2), 0.3)))
		Build.box(rack, Vector3(1.7, 0.02, 0.4), Vector3(0, sy - 0.42, 0), Build.mat(Color(1.0, 0.45, 0.85), 0.0, 0.5, 1.6))   # grow LEDs
	_interact(rack, "plants", "Tend the plants (countermeasure)", Vector3(1.9, 1.7, 0.8))
	Build.routed_cable(hab, [Vector3(6.2, 1.6, 6.2), Vector3(6.4, H_MOD - 0.45, 6.4)], 0.015, Color(0.95, 0.8, 0.15))
	# ---- VR retreat (shared, no cameras/mics: hard limit 6) ----
	Build.label3d(hab, "VR RETREAT · no cameras", Vector3(-3.4, 2.6, 6.4), 0.7, Color(0.75, 0.7, 1.0))
	var vrd = Build.box(hab, Vector3(0.8, 1.0, 0.5), Vector3(-3.4, 0.5, 6.6), Build.mat(Color(0.2, 0.22, 0.3), 0.4, 0.4), true, 0.45)
	Build.box(vrd, Vector3(0.36, 0.16, 0.2), Vector3(0, 0.6, 0), Build.mat(Color(0.95, 0.95, 0.98), 0.2, 0.3))
	Build.box(vrd, Vector3(0.3, 0.06, 0.02), Vector3(0, 0.6, -0.11), Build.mat(Color(0.3, 0.6, 1.0), 0.0, 0.3, 1.5))
	_interact(vrd, "vr", "VR nature break (countermeasure)", Vector3(0.9, 1.3, 0.7))

	# ---- Hygiene (private, no sensors) ----
	Build.label3d(hab, "HYGIENE · private", Vector3(-5.6, 2.7, -5.4), 0.7, Color(1, 0.8, 0.5))
	var hy = Build.box(hab, Vector3(1.3, 2.2, 1.3), Vector3(-5.6, 1.1, -5.4), Build.mat(Color(0.88, 0.9, 0.92), 0.1, 0.6), true, 0.6)
	_interact(hy, "hygiene", "Hygiene stall (private: no sensors)", Vector3(1.4, 2.3, 1.4))
	# ---- Dust station in the airlock ----
	var du = Build.box(hab, Vector3(0.6, 1.6, 0.4), Vector3(8.2, 0.8, 1.2), Build.mat(Color(0.95, 0.6, 0.15), 0.3, 0.5), true)
	Build.label3d(hab, "DUST STATION", Vector3(8.2, 1.9, 1.2), 0.6, Color(1, 0.75, 0.4))
	_interact(du, "dust", "Dust station: brush + vacuum the suit", Vector3(0.8, 1.7, 0.6))
	# ---- Light control in quarters ----
	var lp = Build.box(hab, Vector3(0.05, 0.3, 0.4), Vector3(-8.75, 1.4, -1.8), Build.mat(Color(0.1, 0.12, 0.16), 0.3, 0.3))
	Build.box(lp, Vector3(0.02, 0.22, 0.32), Vector3(0.03, 0, 0), Build.mat(Color(1.0, 0.75, 0.45), 0.0, 0.4, 1.4))
	_interact(lp, "lights", "Light control: auto / focus / wind-down", Vector3(0.4, 0.4, 0.5))

	# ---- Exercise corner (−z) : treadmill with harness ----
	Build.label3d(hab, "EXERCISE · treadmill", Vector3(-3.5, 3.0, -7.0), 0.8)
	var tm_base = Build.box(hab, Vector3(0.9, 0.18, 2.0), Vector3(-3.5, 0.09, -6.8), Build.mat(Color(0.12, 0.12, 0.14), 0.3, 0.6), true, 0.4)
	Build.box(hab, Vector3(0.75, 0.02, 1.8), Vector3(-3.5, 0.19, -6.8), Build.mat(Color(0.03, 0.03, 0.03)), false, 0.4)
	for sx in [-0.42, 0.42]:
		var post = Build.box(hab, Vector3(0.05, 1.3, 0.05), Vector3(-3.5, 0.85, -6.8) + Vector3(sx, 0, -0.85).rotated(Vector3.UP, 0.4), Build.mat(Color(0.6, 0.62, 0.65), 0.7, 0.3))
		Build.sag_cable(hab, Vector3(-3.5, H_MOD - 0.1, -6.8) + Vector3(sx * 0.5, 0, 0).rotated(Vector3.UP, 0.4), Vector3(-3.5, 1.3, -6.8) + Vector3(sx * 0.6, 0, 0).rotated(Vector3.UP, 0.4), 0.02, 0.012, Color(0.95, 0.5, 0.1))
	_interact(tm_base, "treadmill", "Exercise 45 min (treadmill + bungee harness)", Vector3(1.0, 1.0, 2.0))

	# ---- Airlock (+x) : EVA suit ----
	Build.label3d(hab, "AIRLOCK · EVA suit", Vector3(7.6, 3.2, 0), 0.9, Color(1, 0.7, 0.4))
	Build.box(hab, Vector3(0.2, H_MOD, 0.4), Vector3(R_MOD - 0.1, H_MOD / 2, -1.6), Build.mat(Color(0.95, 0.6, 0.15), 0.3, 0.5), true)
	Build.box(hab, Vector3(0.2, H_MOD, 0.4), Vector3(R_MOD - 0.1, H_MOD / 2, 1.6), Build.mat(Color(0.95, 0.6, 0.15), 0.3, 0.5), true)
	Build.box(hab, Vector3(0.12, H_MOD, 3.0), Vector3(R_MOD + 0.4, H_MOD / 2, 0), Build.mat(Color(0.4, 0.42, 0.46), 0.6, 0.4), true)
	var suit = Build.model(hab, "res://models/astronaut.glb", 1.9, Vector3(7.9, 0, 0), -PI / 2)
	_interact(suit, "suit", "Suit up for EVA (suit HR + BP detector)", Vector3(1.0, 2.0, 1.0))
	Build.sag_cable(hab, Vector3(8.6, H_MOD - 0.3, 0.6), Vector3(8.0, 1.3, 0.25), 0.4, 0.02, Color(0.9, 0.9, 0.92))   # umbilical / charging
	Build.sag_cable(hab, Vector3(8.6, H_MOD - 0.3, -0.5), Vector3(8.0, 1.1, -0.2), 0.5, 0.016, Color(0.15, 0.35, 0.85))

	# conduit drops from the ceiling tray to the floor every 30° (power to wall outlets)
	for i in 12:
		var a = TAU * i / 12.0 + 0.13
		if absf(wrapf(a, -PI, PI)) < 0.4: continue
		var p = Vector3(cos(a), 0, sin(a)) * (R_MOD - 0.2)
		Build.routed_cable(hab, [p + Vector3(0, H_MOD - 0.3, 0), p + Vector3(0, 0.45, 0)], 0.025, Color(0.55, 0.58, 0.62))
		Build.box(hab, Vector3(0.12, 0.16, 0.2), p + Vector3(0, 0.38, 0), Build.mat(Color(0.25, 0.27, 0.3), 0.5, 0.4), false, -a)

func _surface() -> void:
	var s = Node3D.new(); s.name = "Surface"; add_child(s); s.position = SURFACE
	var reg = Build.mat(Color(0.47, 0.46, 0.44), 0.0, 1.0)
	var g = Build.box(s, Vector3(120, 0.2, 120), Vector3(0, -0.1, 0), reg, true)
	for side in [Vector3(60, 0, 0), Vector3(-60, 0, 0), Vector3(0, 0, 60), Vector3(0, 0, -60)]:      # invisible edge so you can't walk off the map
		var wall = Node3D.new(); s.add_child(wall); wall.position = side + Vector3(0, 3, 0)
		Build.collider(wall, Vector3(1, 6, 120) if side.x != 0 else Vector3(120, 6, 1))
	var rng = RandomNumberGenerator.new(); rng.seed = 7
	for i in 70:
		var p = Vector3(rng.randf_range(-50, 50), 0, rng.randf_range(-50, 50))
		if p.length() < 5: continue
		var r = Build.sphere(s, rng.randf_range(0.2, 1.4), p, Build.mat(Color(0.38, 0.37, 0.36), 0.0, 1.0))
		r.scale = Vector3(1, rng.randf_range(0.35, 0.7), rng.randf_range(0.7, 1.3))
	# craters (dark rings)
	for i in 14:
		var c = MeshInstance3D.new(); var tm = TorusMesh.new(); var rr = rng.randf_range(2, 7); tm.inner_radius = rr; tm.outer_radius = rr + 0.8; c.mesh = tm
		c.material_override = Build.mat(Color(0.4, 0.39, 0.37), 0.0, 1.0); c.scale = Vector3(1, 0.15, 1); s.add_child(c)
		c.position = Vector3(rng.randf_range(-45, 45), 0.0, rng.randf_range(-45, 45))
	# the habitat from outside (dome + shield berm) and the hatch back in
	Build.cyl(s, 5.0, 3.0, Vector3(-14, 1.5, 0), Build.mat(Color(0.75, 0.75, 0.72), 0.1, 0.8))
	Build.sphere(s, 5.0, Vector3(-14, 3.0, 0), Build.mat(Color(0.62, 0.6, 0.57), 0.0, 1.0)).scale = Vector3(1, 0.45, 1)
	var hatch = Build.box(s, Vector3(0.4, 2.4, 1.6), Vector3(-8.8, 1.2, 0), Build.mat(Color(0.95, 0.6, 0.15), 0.3, 0.5), true)
	_interact(hatch, "hatch", "Airlock hatch — end EVA and go inside", Vector3(1.2, 2.4, 2.0))
	Build.label3d(s, "AIRLOCK", Vector3(-8.6, 2.8, 0), 0.9, Color(1, 0.7, 0.4))
	# solar mast with power cable to the habitat (wires outside too)
	Build.cyl(s, 0.08, 5.0, Vector3(-6, 2.5, -9), Build.mat(Color(0.7, 0.7, 0.72), 0.7, 0.3))
	Build.box(s, Vector3(3.6, 0.05, 1.8), Vector3(-6, 5.0, -9), Build.mat(Color(0.1, 0.15, 0.4), 0.6, 0.2)).rotation.x = 1.2
	Build.routed_cable(s, [Vector3(-6, 0.05, -9), Vector3(-9, 0.05, -6), Vector3(-10.5, 0.05, -2.5), Vector3(-10.5, 0.6, -1.5)], 0.04, Color(0.08, 0.08, 0.09))
	# geology samples to collect
	for p in [Vector3(12, 0.25, 8), Vector3(18, 0.25, -10), Vector3(-2, 0.25, 16)]:
		var smp = Build.sphere(s, 0.28, p, Build.mat(Color(0.95, 0.75, 0.3), 0.3, 0.4, 0.9))
		_interact(smp, "sample", "EVA checkpoint (suit logs HR + BP)", Vector3(0.8, 0.8, 0.8)); sample_nodes.append(smp)
		var beam = Build.cyl(s, 0.03, 4.0, p + Vector3(0, 2.0, 0), Build.mat(Color(1.0, 0.8, 0.3, 0.5), 0.0, 0.5, 1.5))
		smp.set_meta("beam", beam)
	# Earth low on the horizon
	Build.sphere(s, 6.0, Vector3(150, 22, 60), Build.mat(Color(0.25, 0.5, 1.0), 0.0, 0.6, 0.8))

func _interact(node: Node3D, id: String, prompt: String, size: Vector3) -> void:
	var sb = Build.collider(node, size, Vector3(0, size.y / 2, 0))
	sb.set_meta("id", id); sb.set_meta("prompt", prompt); sb.set_meta("node", node)
	sb.set_meta("req", REQ[id][1] if REQ.has(id) else "")

# =============================================================================================
#  PLAYER
# =============================================================================================
func _player() -> void:
	player = CharacterBody3D.new(); add_child(player)
	var cs = CollisionShape3D.new(); var cap = CapsuleShape3D.new(); cap.radius = 0.3; cap.height = 1.7; cs.shape = cap; cs.position.y = 0.85; player.add_child(cs)
	cam = Camera3D.new(); cam.position.y = 1.62; cam.fov = 75; player.add_child(cam)
	player.position = Vector3(-1.5, 0.1, 3.5); yaw = deg_to_rad(200)

func _unhandled_input(e: InputEvent) -> void:
	if e is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		yaw -= e.relative.x * 0.0028; pitch = clamp(pitch - e.relative.y * 0.0028, -1.35, 1.35)
	if e is InputEventMouseButton and e.pressed and panel_kind == "" and Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	if e is InputEventKey and e.pressed and not e.echo:
		match e.keycode:
			KEY_E:
				if on_treadmill: _leave_treadmill(); A.exercising = 0.0; A.note("stopped the workout early")
				elif panel_kind == "": _use()
			KEY_W, KEY_A, KEY_S, KEY_D:
				if on_treadmill: _leave_treadmill(); A.exercising = 0.0; A.note("stopped the workout early")
			KEY_TAB: _toggle_panel("feel")
			KEY_H: _toggle_panel("help")
			KEY_J: _toggle_panel("tasks")
			KEY_P: ares_tab = "cases"; _toggle_panel("ares")
			KEY_R: ares_tab = "rubric"; _toggle_panel("ares")
			KEY_O: AI.set_ai(not AI.ai_on); _toast("ARES AI " + ("ON" if AI.ai_on else "OFF — everything it changed is undone. Press O to turn it back on."))
			KEY_Y: if AI.pending_offer >= 0: _toast(AI.answer_offer(true)); _after_offer()
			KEY_N: if AI.pending_offer >= 0: _toast(AI.answer_offer(false)); _after_offer()
			KEY_ESCAPE:
				if panel_kind != "": _close_panel()
				else: Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
			KEY_BRACKETRIGHT, KEY_EQUAL: speed_i = min(speed_i + 1, speeds.size() - 1)
			KEY_BRACKETLEFT, KEY_MINUS: speed_i = max(speed_i - 1, 0)
			KEY_F: if A.asleep: sleep_fast = not sleep_fast
			KEY_SPACE:
				if on_treadmill: _leave_treadmill(); A.exercising = 0.0; A.note("stopped the workout early")
				elif A.asleep: _wake()
				elif player.is_on_floor(): player.velocity.y = 2.2

func _physics_process(dt: float) -> void:
	player.rotation.y = yaw; cam.rotation.x = pitch
	var g = G_MOON
	if not player.is_on_floor(): player.velocity.y -= g * dt
	var dir = Vector3.ZERO
	if panel_kind == "" and not A.asleep and not on_treadmill:
		var f = -player.global_transform.basis.z; var r = player.global_transform.basis.x
		f.y = 0; r.y = 0
		if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP): dir += f
		if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN): dir -= f
		if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT): dir += r
		if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT): dir -= r
	var run = Input.is_key_pressed(KEY_SHIFT)
	var sp = (5.5 if run else 3.0) * (0.85 if A.on_eva else 1.0)
	dir = dir.normalized() * sp
	player.velocity.x = lerp(player.velocity.x, dir.x, 0.2); player.velocity.z = lerp(player.velocity.z, dir.z, 0.2)
	player.move_and_slide()
	# never fall out of the world: back to a safe spot
	if player.global_position.y < -6.0:
		player.velocity = Vector3.ZERO
		player.global_position = (SURFACE + Vector3(-7.5, 0.5, 0)) if A.on_eva else Vector3(-1.5, 0.5, 3.5)
		_toast("Whoa — back on solid ground.")
	if A.on_eva:
		var moving = Vector2(player.velocity.x, player.velocity.z).length()
		A.activity = lerp(A.activity, 0.15 + 0.12 * moving + (0.25 if run and moving > 1 else 0.0), 0.05)
	# what are we looking at?
	looking_at = null
	if panel_kind == "" and not A.asleep:
		var q = PhysicsRayQueryParameters3D.create(cam.global_position, cam.global_position - cam.global_transform.basis.z * 3.2)
		q.exclude = [player.get_rid()]
		var hit = get_world_3d().direct_space_state.intersect_ray(q)
		if hit and hit.collider.has_meta("id"): looking_at = hit.collider

func _process(dt: float) -> void:
	var rate: float = speeds[speed_i]
	if A.asleep: rate = 40.0 if sleep_fast else 8.0
	if panel_kind == "station" and ecg_t < 6.0: rate = 0.3
	if on_treadmill: rate = 15.0     # the 45-minute workout plays out in about 3 seconds
	var dm = rate * dt
	# step the body in ≤1-minute chunks so the model stays stable at high speed
	var was_asleep = A.asleep
	while dm > 0.0:
		var s: float = min(dm, 1.0); A.step(s); AI.step(s, _in_shared_zone()); dm -= s
	if was_asleep and not A.asleep: _after_wake()     # the body clock woke you: stand up + sleep report
	if A.day() != task_day: _new_day_tasks()
	if case_running and AI.case_i == 4 and AI.tier[Ares.CASES[4][2]] >= 3:
		speed_i = 0; case_running = false; AI.pending_offer = -1; case_msg = AI.case_result(); _done("case")
		_toast("Tier 3: you were told first, then the Commander (the CMO is outside). " + case_msg)
	elif case_running and AI.case_i != 4 and AI.pending_offer >= 0:
		speed_i = 0
	if not A.asleep and on_treadmill and A.exercising <= 0.0:
		_leave_treadmill(); _done("exercise"); _toast("Workout done — stress down, mood up.")
	_world_tick(dt)
	_hud_tick()
	if panel_kind == "feel" or panel_kind == "pod" or (panel_kind == "ares" and ares_tab == "dash"): _refresh_live_panel()
	if panel_kind == "station": ecg_t += dt; _refresh_live_panel()

func _world_tick(dt: float) -> void:
	var h = A.hour()
	# circadian LEDs: cool white by day, warm in the evening, deep amber at night; red pulse on CO2 alarm
	var c = Color(0.85, 0.92, 1.0)
	if h >= 19.0 or h < 6.0: c = Color(1.0, 0.55, 0.25) if (h >= 21.0 or h < 6.0) else Color(1.0, 0.8, 0.55)
	elif h < 7.0: c = Color(1.0, 0.75, 0.5)
	if AI.ai_on and AI.tier[0] >= 1: c = Color(1.0, 0.72, 0.45)        # Tier 1 (anxiety): warm 2700 K
	elif AI.ai_on and AI.tier[5] >= 1: c = Color(0.55, 0.95, 0.85)     # Tier 1 (monotony): new colour scene
	elif AI.ai_on and AI.tier[3] >= 1 and h >= 7.0 and h < 19.0: c = Color(0.8, 0.9, 1.0) * 1.15   # Tier 1 (fatigue): focus light
	if light_mode == 1: c = Color(0.85, 0.92, 1.0) * 1.2
	elif light_mode == 2: c = Color(1.0, 0.5, 0.22)
	if suit_dust > 0.0 and not A.on_eva: cabin_dust = min(1.0, cabin_dust + dt * 0.004)
	var alarm = A.co2_ppm > 4000.0
	for m in led_mats: m.emission = m.emission.lerp(c, 0.05); m.albedo_color = m.emission
	warn_light.light_energy = (0.8 + 0.8 * sin(Time.get_ticks_msec() * 0.008)) if alarm else 0.0
	bunk_light.light_energy = 0.15 if A.asleep else (0.8 if (h >= 20.0 or h < 6.0) else 0.3)
	bunk_light.light_color = Color(1.0, 0.45, 0.2) if A.asleep else Color(1.0, 0.7, 0.45)
	if A.on_eva and A.eva_suit_o2 <= 0.0: _end_eva(true)

func _in_shared_zone() -> bool:
	if A.on_eva: return false
	return player.global_position.x > -5.2     # quarters (behind the curtain) are private

# =============================================================================================
#  INTERACTIONS
# =============================================================================================
func _use() -> void:
	if looking_at == null: return
	var id: String = looking_at.get_meta("id")
	used[id] = true
	match id:
		"bunk":
			if A.hour() > 10.0 and A.hour() < 19.0 and A.sleep_pressure < 0.55:
				_toast("Too early and not tired enough — a nap would wreck tonight's sleep. Try after 20:00."); return
			_sleep()
		"wristband":
			A.wristband_on = not A.wristband_on
			if A.wristband_on: A.note("night wristband on"); _done("band")
			else: A.note("wristband off — sleep won't be tracked")
			(looking_at.get_meta("node") as Node3D).visible = not A.wristband_on
		"beacon":
			A.beacon_on = not A.beacon_on; A.note("crew beacon " + ("on (chest)" if A.beacon_on else "docked — no heart data"))
			(looking_at.get_meta("node") as Node3D).visible = not A.beacon_on
		"patch":
			if A.patch_on and A.patch_hours_left() > 6.0: _toast("Patch still good for %d h." % int(A.patch_hours_left()))
			else: A.new_patch(); _done("patch")
		"pod": _open_panel("pod")
		"station": ecg_t = 0.0; _open_panel("station")
		"console": _open_panel("console")
		"treadmill":
			if A.exercising > 0.0: return
			A.exercise(); on_treadmill = true
			_toast("Running on the treadmill… (press E, Space or WASD to stop early)")
		"suit": _start_eva()
		"hygiene": A.stress = max(0.0, A.stress - 0.03); A.minute += 10.0; A.note("Hygiene: private, no sensors in here (hard limit 6)")
		"dust":
			suit_dust = 0.0; cabin_dust *= 0.3; A.note("Suit brushed and vacuumed: dust kept out of the cabin"); AI.flag("dust")
		"lights":
			light_mode = (light_mode + 1) % 3
			A.note("Lights: " + ["auto (follows your body clock)", "focus 6500 K", "wind-down 1800 K (helps sleep)"][light_mode]); AI.flag("lights")
		"plants":
			A.stress = max(0.0, A.stress - 0.08); A.valence = min(1.0, A.valence + 0.08); AI.novelty = min(1.0, AI.novelty + 0.25); A.minute += 20.0
			A.note("20 min of plant care: stress down, mood up"); AI.note("Countermeasure used: plant care"); AI.flag("counter")
		"vr":
			A.stress = max(0.0, A.stress - 0.12); AI.novelty = min(1.0, AI.novelty + 0.2); A.minute += 15.0
			A.note("15-min VR nature break (forest + ocean)"); AI.note("Countermeasure used: VR nature break"); AI.flag("counter")
		"hatch": _end_eva(false)
		"sample":
			var n: Node3D = looking_at.get_meta("node")
			if n.visible:
				n.visible = false; (n.get_meta("beam") as Node3D).visible = false; (looking_at as CollisionObject3D).collision_layer = 0
				samples += 1; A.note("EVA checkpoint %d/3: HR %d, BP %d/%d logged" % [samples, A.hr, A.bp_sys, A.bp_dia]); A.activity = min(1.0, A.activity + 0.2)
				if samples >= 3: _done("eva"); _toast("All 3 checkpoints done. Head back to the airlock.")

func _after_offer() -> void:
	if not case_running: return
	case_running = false
	for i in 120: A.step(1.0); AI.step(1.0, _in_shared_zone())     # two hours later
	case_msg = AI.case_result(); _done("case")
	_toast("Two hours later: " + case_msg + "  (P → Health cases for more)")

func _leave_treadmill() -> void:
	on_treadmill = false
	player.global_position = Vector3(-2.0, 0.2, -5.0)     # step off beside the treadmill, clear of its frame

func _sleep() -> void:
	A.go_to_bed(); sleep_fast = false
	player.global_position = Vector3(-7.3, 0.9, 0); pitch = 0.6
	if not A.wristband_on: _toast("Asleep without the wristband — only the bunk's mattress sensor is tracking.")

func _wake() -> void:
	A.wake_up(); _after_wake()

func _after_wake() -> void:
	sleep_fast = false; player.velocity = Vector3.ZERO
	player.global_position = Vector3(-5.0, 0.2, 0); pitch = 0
	if A.last_sleep.hours >= 7.0: _done("sleep")
	_open_panel("sleep")

func _start_eva() -> void:
	if A.asleep: return
	if A.hour() < 6.0 or A.hour() > 20.0: _toast("EVAs are planned in daylight shifts (06:00–20:00)."); return
	A.on_eva = true; A.eva_suit_o2 = 100.0; samples = 0
	for n in sample_nodes:
		n.visible = true; (n.get_meta("beam") as Node3D).visible = true
		for c in n.get_children():
			if c is CollisionObject3D: c.collision_layer = 1
	player.global_position = SURFACE + Vector3(-7.5, 0.3, 0); yaw = deg_to_rad(-90)
	A.note("EVA start — suit HR + BP detector online"); _toast("You're outside. Walk to the 3 glowing checkpoints. The suit tracks your heart rate and blood pressure; keep HR under 150.")

func _end_eva(forced: bool) -> void:
	if not A.on_eva: return
	A.on_eva = false; A.activity = 0.2; suit_dust = 1.0
	_toast("Suit is covered in lunar dust. Use the dust station before it spreads into the cabin.")
	player.global_position = Vector3(7.2, 0.3, 0); yaw = deg_to_rad(90)
	A.note("EVA ended" + (" — O2 reserve reached, auto-return" if forced else ""))

func _done(k: String) -> void:
	if tasks.has(k) and not tasks[k][1]:
		tasks[k][1] = true; A.note("task done: " + tasks[k][0])

func _new_day_tasks() -> void:
	task_day = A.day()
	tasks = {
		"checkin": ["Morning check-in at the Crew Health Station", false],
		"feel": ["Check how you feel (TAB fusion screen)", false],
		"mood": ["Talk near the zone sensor pod (mood detector)", false],
		"exercise": ["Exercise 45 min on the treadmill", false],
		"eva": ["EVA walk: 3 checkpoints, HR under 150 (suit biomed)", false],
		"patch": ["Keep a cortisol patch on", A.patch_on and A.patch_hours_left() > 6],
		"band": ["Wear the night wristband before bed", A.wristband_on],
		"sleep": ["Sleep 7+ hours in the smart bunk", false],
		"journal": ["Write in the daily journal (crew console)", false],
		"case": ["Play a health case (P → Health cases)", false],
		"ares": ["Open the ARES dashboard (P)", false],
	}

# =============================================================================================
#  UI
# =============================================================================================
func _ui() -> void:
	ui = CanvasLayer.new(); add_child(ui)
	var th = Theme.new(); th.default_font_size = 17
	var root = Control.new(); root.set_anchors_preset(Control.PRESET_FULL_RECT); root.mouse_filter = Control.MOUSE_FILTER_IGNORE; root.theme = th; ui.add_child(root)
	hud_clock = _lbl(root, Vector2(16, 12), 24); hud_clock.add_theme_color_override("font_color", Color(0.6, 0.9, 1))
	hud_feel = _lbl(root, Vector2(16, 46), 18)
	hud_vitals = _lbl(root, Vector2(16, 74), 15); hud_vitals.add_theme_color_override("font_color", Color(0.8, 0.88, 0.95))
	hud_tasks = RichTextLabel.new(); hud_tasks.bbcode_enabled = true; hud_tasks.fit_content = true; hud_tasks.scroll_active = false
	hud_tasks.set_anchors_preset(Control.PRESET_TOP_RIGHT); hud_tasks.position = Vector2(-400, 12); hud_tasks.size = Vector2(388, 300); hud_tasks.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud_tasks.add_theme_font_size_override("normal_font_size", 15); root.add_child(hud_tasks)
	hud_prompt = Label.new(); hud_prompt.set_anchors_preset(Control.PRESET_CENTER); hud_prompt.position = Vector2(-300, 40); hud_prompt.size = Vector2(600, 30)
	hud_prompt.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER; hud_prompt.add_theme_font_size_override("font_size", 19); hud_prompt.add_theme_color_override("font_color", Color(1, 0.9, 0.5))
	hud_prompt.add_theme_constant_override("outline_size", 6); hud_prompt.add_theme_color_override("font_outline_color", Color.BLACK); root.add_child(hud_prompt)
	var cross = Label.new(); cross.text = "+"; cross.set_anchors_preset(Control.PRESET_CENTER); cross.position = Vector2(-6, -14); root.add_child(cross)
	hud_log = Label.new(); hud_log.set_anchors_preset(Control.PRESET_BOTTOM_LEFT); hud_log.position = Vector2(16, -190); hud_log.size = Vector2(520, 180)
	hud_log.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM; hud_log.add_theme_font_size_override("font_size", 13); hud_log.add_theme_color_override("font_color", Color(0.75, 0.8, 0.88)); root.add_child(hud_log)
	var keys = Label.new(); keys.set_anchors_preset(Control.PRESET_BOTTOM_RIGHT); keys.position = Vector2(-640, -30); keys.size = Vector2(630, 24)
	keys.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT; keys.add_theme_font_size_override("font_size", 13); keys.add_theme_color_override("font_color", Color(0.6, 0.7, 0.8))
	keys.text = "WASD move · E use · TAB how you feel · P ARES · R rubric · O AI off · J tasks · H help · [ ] speed"; root.add_child(keys)
	# EVA suit HUD
	hud_eva = PanelContainer.new(); hud_eva.set_anchors_preset(Control.PRESET_CENTER_LEFT); hud_eva.position = Vector2(16, -60); hud_eva.size = Vector2(330, 200)
	hud_eva_text = RichTextLabel.new(); hud_eva_text.bbcode_enabled = true; hud_eva_text.fit_content = true; hud_eva_text.custom_minimum_size = Vector2(310, 0); hud_eva.add_child(hud_eva_text)
	hud_eva.add_theme_stylebox_override("panel", _style(Color(0.02, 0.05, 0.1, 0.78), Color(1, 0.6, 0.2))); root.add_child(hud_eva)
	# modal panel
	panel = PanelContainer.new(); panel.set_anchors_preset(Control.PRESET_CENTER); panel.position = Vector2(-430, -320); panel.size = Vector2(860, 640)
	panel.add_theme_stylebox_override("panel", _style(Color(0.02, 0.04, 0.09, 0.97), Color(0.3, 0.7, 1.0)))
	var sc = ScrollContainer.new(); sc.custom_minimum_size = Vector2(840, 620); sc.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED; panel.add_child(sc)
	panel_box = VBoxContainer.new(); panel_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL; panel_box.add_theme_constant_override("separation", 10); sc.add_child(panel_box)
	panel.visible = false; root.add_child(panel)

func _style(bg: Color, border: Color) -> StyleBoxFlat:
	var s = StyleBoxFlat.new(); s.bg_color = bg; s.border_color = border; s.set_border_width_all(2); s.set_corner_radius_all(10)
	s.content_margin_left = 16; s.content_margin_right = 16; s.content_margin_top = 12; s.content_margin_bottom = 12
	return s

func _lbl(p: Control, pos: Vector2, fs: int) -> Label:
	var l = Label.new(); l.position = pos; l.add_theme_font_size_override("font_size", fs)
	l.add_theme_constant_override("outline_size", 5); l.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.8)); p.add_child(l)
	return l

var _toast_t = 0.0
func _toast(t: String) -> void: A.note(t); hud_prompt.text = t; _toast_t = 4.0

func _hud_tick() -> void:
	var spd = "×%d" % int(speeds[speed_i]) if not A.asleep else ("sleeping ×%d  (F faster · SPACE wake)" % (40 if sleep_fast else 8))
	hud_clock.text = "%s   %s   %s" % [A.clock_text(), spd, "EVA" if A.on_eva else ("shared zone" if _in_shared_zone() else "private zone")]
	var fz = Fusion.assess(A, checkin, _in_shared_zone())
	hud_feel.text = "Feeling: %s   (%d%% confidence · TAB for why)" % [fz.label, int(fz.confidence * 100)]
	hud_feel.add_theme_color_override("font_color", fz.color)
	if A.beacon_on: hud_vitals.text = "HR %d bpm · HRV %d ms · BP %d/%d · SpO2 %d%% · CO2 %d ppm · sleep debt %.1f h" % [A.hr, A.hrv, A.bp_sys, A.bp_dia, A.spo2, A.co2_ppm, A.sleep_debt_h]
	else: hud_vitals.text = "crew beacon docked — no live heart data · CO2 %d ppm" % A.co2_ppm
	var t = "[right][b][color=#7fd3ff]TODAY · Day %d[/color][/b]\n" % A.day()
	var done = 0
	for k in tasks:
		var row = tasks[k]
		if row[1]: done += 1
		t += ("[color=#5be38a][b]DONE[/b]  %s[/color]\n" if row[1] else "[color=#e8eef6]%s[/color]\n") % row[0]
	t += "[color=#9fb0c4]%d / %d done[/color][/right]" % [done, tasks.size()]
	hud_tasks.text = t
	if _toast_t > 0.0: _toast_t -= get_process_delta_time()
	elif looking_at != null: hud_prompt.text = "E  " + str(looking_at.get_meta("prompt")) + ("\n[Why: " + str(looking_at.get_meta("req")) + "]" if str(looking_at.get_meta("req")) != "" else "")
	elif A.asleep: hud_prompt.text = "Asleep · %s · F speed up · SPACE wake up" % A.stage
	elif AI.pending_offer >= 0: hud_prompt.text = "AI offer: %s   Y yes · N no" % Ares.TIER2[AI.pending_offer]
	else: hud_prompt.text = ""
	hud_log.text = "\n".join(A.events.slice(0, 6))
	hud_eva.visible = A.on_eva
	if A.on_eva:
		var hrc = "#5be38a" if A.hr < 130 else ("#ffb74d" if A.hr < 150 else "#ff5a4a")
		hud_eva_text.text = "[b][color=#ffa94d]EVA SUIT · biomed[/color][/b]\n[color=%s]HR %d bpm[/color]   BP [b]%d/%d[/b] mmHg\nSpO2 %d%%   resp %d /min\nO2 tank [b]%d%%[/b]   helmet CO2 %.1f mmHg\nsuit %.1f °C   metabolic %d W\ncheckpoints %d / 3%s" % [hrc, A.hr, A.bp_sys, A.bp_dia, A.spo2, A.rr, A.eva_suit_o2, A.eva_suit_co2, A.eva_suit_temp, int(100 + 450 * A.activity), samples, "\n[color=#ff5a4a]HR HIGH — slow down[/color]" if A.hr > 150 else ""]

# ---- panels ----------------------------------------------------------------------------------
func _toggle_panel(k: String) -> void:
	if panel_kind == k: _close_panel()
	else: _open_panel(k)

func _open_panel(k: String) -> void:
	panel_kind = k; panel.visible = true; Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	if k == "feel": _done("feel")
	if k == "pod" and _in_shared_zone(): _done("mood"); AI.flag("pod")
	if k == "ares": _done("ares")
	_build_panel()

func _close_panel() -> void:
	panel_kind = ""; panel.visible = false; Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

func _clear() -> void:
	for c in panel_box.get_children(): c.queue_free()

func _rt(text: String, fs := 16) -> RichTextLabel:
	var r = RichTextLabel.new(); r.bbcode_enabled = true; r.fit_content = true; r.scroll_active = false; r.text = text
	r.add_theme_font_size_override("normal_font_size", fs); r.add_theme_font_size_override("bold_font_size", fs); r.custom_minimum_size = Vector2(800, 0); panel_box.add_child(r)
	return r

func _btn(text: String, cb: Callable) -> Button:
	var b = Button.new(); b.text = text; b.pressed.connect(cb); b.custom_minimum_size = Vector2(0, 38); return b

func _close_row() -> void:
	var hb = HBoxContainer.new(); panel_box.add_child(hb)
	hb.add_child(_btn("Close (Esc)", _close_panel))

var _live: RichTextLabel
var _plot: Plot

func _build_panel() -> void:
	_clear(); _live = null; _plot = null
	match panel_kind:
		"help":
			_rt("[b][font_size=26][color=#7fd3ff]CREW HEALTH SIM[/color][/font_size][/b]\n[color=#9fb0c4]Project ARES · NASA HUNCH[/color]\n\nOne day on a lunar base. Six health devices and the ARES AI estimate how you feel. Everything in the Project ARES rubric can be tried here.\n\n[b]Bunk[/b] (quarters): sleep. Wear the [b]night wristband[/b] first.\n[b]Crew Health Station[/b] (medical): ECG, blood pressure, SpO2, temperature.\n[b]Zone sensor pod[/b] (wardroom): voice and face mood estimate, shared zones only.\n[b]Cortisol patch[/b] (table): lasts 48 h.\n[b]Crew beacon[/b] (shelf): heart rate, HRV, skin response.\n[b]EVA suit[/b] (airlock): suit heart rate and blood pressure outside.\n[b]Treadmill, crew console[/b]: exercise, meals, calls, work, CO2 drill.\n\n[b]TAB[/b] how you feel · [b]P[/b] ARES panel: health cases, sensors, conditions, tiers, limits, Board Q1–Q6, tabletop, your data · [b]R[/b] rubric checklist · [b]O[/b] AI off · [b]Y / N[/b] answer an AI offer\n\nWASD move · mouse look · E use · Shift run · Space jump · [ ] time speed · J tasks\n\n[color=#9fb0c4]© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.[/color]")
			_close_row()
		"ares": _build_ares()
		"tasks":
			var t = "[b][font_size=22]Mission day %d tasks[/font_size][/b]\n\n" % A.day()
			for k in tasks: t += ("[color=#5be38a]DONE[/color]  " if tasks[k][1] else "TO DO  ") + tasks[k][0] + "\n"
			_rt(t, 18); _close_row()
		"feel":
			_rt("[b][font_size=24][color=#7fd3ff]HOW IS THE ASTRONAUT FEELING?[/color][/font_size][/b]\n[color=#9fb0c4]Sensor fusion across all six health devices (+ the EVA suit outside)[/color]")
			_live = _rt("")
			_plot = Plot.new(); _plot.kind = "circumplex"; _plot.custom_minimum_size = Vector2(800, 220); panel_box.add_child(_plot)
			_close_row(); _refresh_live_panel()
		"pod":
			_rt("[b][font_size=24][color=#7fd3ff]ZONE SENSOR POD · MOOD DETECTOR[/color][/font_size][/b]\n[color=#9fb0c4]Voice tone + facial expression → valence (happy to low) and arousal (calm to tense). Runs only in shared zones, processed on the pod; raw audio and video never leave it.[/color]")
			_live = _rt("")
			_plot = Plot.new(); _plot.kind = "circumplex"; _plot.custom_minimum_size = Vector2(800, 240); panel_box.add_child(_plot)
			var hb = HBoxContainer.new(); panel_box.add_child(hb)
			hb.add_child(_btn("Share my mood with the flight surgeon: %s" % ("ON" if A.share_mood else "OFF"), func(): A.share_mood = not A.share_mood; A.note("mood sharing " + ("on" if A.share_mood else "off")); _build_panel()))
			hb.add_child(_btn("Close (Esc)", _close_panel))
			_refresh_live_panel()
		"station":
			_rt("[b][font_size=24][color=#7fd3ff]CREW HEALTH STATION · DAILY CHECK-IN[/color][/font_size][/b]\n[color=#9fb0c4]Hold still… 2-lead ECG, oscillometric blood-pressure cuff, finger pulse-oximeter, IR thermometer[/color]")
			_plot = Plot.new(); _plot.kind = "ecg"; _plot.custom_minimum_size = Vector2(800, 170); panel_box.add_child(_plot)
			_live = _rt("")
			_close_row(); _refresh_live_panel()
		"sleep":
			var s: Dictionary = A.last_sleep
			_rt("[b][font_size=24][color=#7fd3ff]GOOD MORNING · SLEEP REPORT[/color][/font_size][/b]\n%s\n\n[font_size=40][b]%d[/b][/font_size] / 100 sleep score\n\nTime asleep [b]%.1f h[/b] · efficiency [b]%d%%[/b] · deep (N3) [b]%.1f h[/b] · REM [b]%.1f h[/b] · wake-ups [b]%d[/b]\n%s" % [
				"[color=#5be38a]Tracked by the night wristband + bunk mattress sensor[/color]" if s.tracked else "[color=#ffb74d]Wristband was off — only the bunk's mattress sensor tracked you (less detail)[/color]",
				s.score, s.hours, int(s.eff * 100), s.deep, s.rem, s.wakes,
				"Tip: under 7 h raises tomorrow's stress and lowers mood. High cabin CO2 and stress cause wake-ups." if s.hours < 7 else "Nice — 7+ hours. Deep sleep came early in the night, REM later, just like on Earth."])
			_plot = Plot.new(); _plot.kind = "hypno"; _plot.data = A.hypnogram.duplicate(); _plot.custom_minimum_size = Vector2(800, 200); panel_box.add_child(_plot)
			_close_row()
		"console":
			_rt("[b][font_size=24][color=#7fd3ff]CREW CONSOLE[/color][/font_size][/b]\n[color=#9fb0c4]Things that change how you feel — watch the TAB screen before and after.[/color]")
			var g = GridContainer.new(); g.columns = 2; panel_box.add_child(g)
			for row in [["Crew meal with the crew (hunger to 0, mood +)", func(): A.eat(); AI.flag("counter")], ["Video call home (+25 min, mood ++, stress −)", func(): A.call_home()], ["Science work block (+2 h, stress +)", func(): A.work_block()], ["CO2 scrubber drill (CO2 spike)", func(): A.co2_spike()]]:
				var b = _btn(row[0], row[1]); b.custom_minimum_size = Vector2(390, 44); g.add_child(b)
			_rt("[b]Daily journal[/b] [color=#9fb0c4](a few words on how today went; the language check runs here and the text never leaves the console)[/color]", 15)
			var jr = HBoxContainer.new(); panel_box.add_child(jr)
			var le = LineEdit.new(); le.placeholder_text = "e.g. tired today, argued at lunch, miss home"; le.custom_minimum_size = Vector2(620, 40); jr.add_child(le)
			jr.add_child(_btn("Check", func():
				var j = AI.journal(le.text); _done("journal")
				ares_msg = "Sentiment %+.2f · stress index %.2f · class: [b]%s[/b]" % [j.sentiment, j.stress, j["class"]]
				_build_panel()))
			if ares_msg != "" and AI.flags.has("journal"): _rt(ares_msg, 15)
			_live = _rt(""); _close_row(); _refresh_live_panel()

func _refresh_live_panel() -> void:
	if _live == null: return
	match panel_kind:
		"ares":
			if ares_tab == "dash": _live.text = _ares_dash()
		"feel":
			var fz = Fusion.assess(A, checkin, _in_shared_zone())
			var t = "[font_size=30][b][color=#%s]%s[/color][/b][/font_size]   confidence [b]%d%%[/b] · %d of %d devices agree\n%s\n[color=#9fb0c4]Suggestion:[/color] %s\n\n" % [fz.color.to_html(false), fz.label, int(fz.confidence * 100), fz.agree, fz.voters, fz.sentence, fz.advice]
			t += "[b]stress[/b] %s   [b]fatigue[/b] %s   [b]mood[/b] %s\n\n" % [_bar(fz.stress), _bar(fz.fatigue), _bar(fz.mood)]
			t += "[table=3]"
			for r in fz.rows:
				t += "[cell][b]%s[/b]  [/cell][cell][color=%s]%s[/color]  [/cell][cell]%s[/cell]" % [r[0], "#5be38a" if r[1] else "#ff8a7a", "ON" if r[1] else "--", r[2]]
			t += "[/table]"
			_live.text = t
			if _plot: _plot.v = A.valence; _plot.a = A.arousal; _plot.queue_redraw()
		"pod":
			if not _in_shared_zone():
				_live.text = "[color=#ffb74d]You're in a private zone — the pod's camera and microphone are off here. Walk into the wardroom.[/color]"
			elif A.asleep: _live.text = "asleep — detector idle"
			else:
				var v: float = A.valence; var a: float = A.arousal
				var word = "happy & energised" if v > 0.3 and a > 0.5 else ("content & relaxed" if v > 0.3 else ("tense / anxious" if a > 0.55 else ("low / sad" if v < -0.25 else "neutral")))
				_live.text = "Voice: pitch variation %s, speech rate %s · Face: %s\nEstimated mood: [b]%s[/b]  (valence %+.2f · arousal %.2f)\nCabin CO2 here: %d ppm · Shared with flight surgeon: [b]%s[/b]" % ["high" if v > 0.2 else "low", "fast" if a > 0.55 else "normal", "smiling" if v > 0.3 else ("brow tense" if a > 0.55 else "neutral"), word, v, a, A.co2_ppm, "yes (consented)" if A.share_mood else "no — stays with you"]
			if _plot: _plot.v = A.valence; _plot.a = A.arousal; _plot.queue_redraw()
		"station":
			if _plot: _plot.hr = A.hr; _plot.t = ecg_t; _plot.queue_redraw()
			if ecg_t < 6.0:
				_live.text = "Measuring… %d%%   (cuff inflating, ECG recording)" % int(ecg_t / 6.0 * 100)
			else:
				if checkin.is_empty() or float(checkin.get("t", -1)) < A.minute - 30.0:
					checkin = {"t": A.minute, "sys": int(A.bp_sys), "dia": int(A.bp_dia), "spo2": int(A.spo2), "temp": A.core_t, "hr": int(A.hr), "hrv": int(A.hrv)}
					if A.hour() < 12.0 or true: _done("checkin")
					A.note("check-in saved: BP %d/%d" % [checkin.sys, checkin.dia])
				var flag = []
				if checkin.sys >= 135: flag.append("BP high — rest 10 min and re-check")
				if checkin.spo2 < 94: flag.append("SpO2 low — check cabin O2")
				if checkin.temp > 37.6: flag.append("temperature raised")
				_live.text = "[font_size=22]HR [b]%d[/b] bpm   BP [b]%d/%d[/b] mmHg   SpO2 [b]%d%%[/b]   [b]%.1f °C[/b]   HRV %d ms[/font_size]\n%s\nSaved to your private health record. The flight surgeon sees medical numbers; mood stays private unless you share it." % [checkin.hr, checkin.sys, checkin.dia, checkin.spo2, checkin.temp, checkin.hrv, "[color=#5be38a]All within flight limits.[/color]" if flag.is_empty() else "[color=#ffb74d]" + " · ".join(flag) + "[/color]"]
		"console":
			_live.text = "hunger %d%% · connection with home %d%% · stress %d%% · mood %+.2f · CO2 %d ppm" % [A.hunger * 100, A.social * 100, A.stress * 100, A.valence, A.co2_ppm]

func _bar(x: float) -> String:
	var n = int(round((x + 1.0) * 5.0)); n = clamp(n, 0, 10)
	var col = "#5be38a" if x < -0.1 else ("#ffd166" if x < 0.35 else "#ff6a5a")
	return "[color=%s]%s[/color][color=#334]%s[/color]" % [col, "|".repeat(n * 2), "|".repeat((10 - n) * 2)]

# =============================================================================================
#  ARES panel: dashboard · your data · Board Q1–Q6 · hard limits · tabletop · rubric
# =============================================================================================
func _build_ares() -> void:
	var tabs = HFlowContainer.new(); panel_box.add_child(tabs)
	for tb in [["cases", "Health cases"], ["dash", "Dashboard"], ["trace", "Why it's here"], ["protocols", "Protocols"], ["habitat", "Habitat"], ["model", "Model"], ["report", "Report"], ["crew", "Your data"], ["board", "Board Q1–Q6"], ["limits", "Hard limits"], ["tabletop", "Tabletop"], ["rubric", "Rubric"]]:
		var b = _btn(tb[1], func(): ares_tab = tb[0]; ares_msg = ""; _build_panel()); b.toggle_mode = true; b.button_pressed = ares_tab == tb[0]; tabs.add_child(b)
	AI.flag("crew" if ares_tab == "crew" else ares_tab)
	match ares_tab:
		"dash":
			_live = _rt(_ares_dash(), 15)
		"cases":
			_rt("[b][font_size=22]Health cases[/font_size][/b]\nEach case sets up a real crew-health problem. Time runs fast until the AI notices. Then [b]you[/b] decide: Y to accept its offer, N to refuse.\n", 15)
			for i in Ares.CASES.size():
				var ii = i
				var hb = HBoxContainer.new(); panel_box.add_child(hb)
				hb.add_child(_btn("Play", func(): ares_msg = AI.start_case(ii); case_running = true; speed_i = 2; _close_panel(); _toast(ares_msg)))
				var l = Label.new(); l.text = "%s: %s" % [Ares.CASES[i][0], Ares.CASES[i][1]]; l.autowrap_mode = TextServer.AUTOWRAP_WORD; l.custom_minimum_size = Vector2(700, 0); hb.add_child(l)
			if case_msg != "": _rt("[color=#5be38a]Last result: " + case_msg + "[/color]", 15)
		"trace":
			var t = "[b][font_size=22]Why each object is here[/font_size][/b]\nEvery object in the base answers a line in the research proposal. Nothing is decoration. Ticks show what you have used.\n\n[table=3]"
			for k in REQ:
				t += "[cell]%s  [/cell][cell][b]%s[/b]  [/cell][cell]%s[/cell]" % ["[color=#5be38a]used[/color]" if used.has(k) else "[color=#6f8398]-[/color]", REQ[k][0], REQ[k][1]]
			_rt(t + "[/table]", 14)
		"model":
			var t = "[b][font_size=22]Mood models (research proposal §3)[/font_size][/b]\nTrained on public mood datasets (DAIC, eRisk) to sort people into 3 classes: Neutral, Underlying issue, Suspected illness.\n\n[table=4][cell][b]Model[/b]  [/cell][cell][b]Accuracy[/b]  [/cell][cell][b]F1[/b]  [/cell][cell][b]Accuracy after +0.3 sentiment shift[/b][/cell]"
			for m in Ares.MODELS: t += "[cell]%s  [/cell][cell]%.2f  [/cell][cell]%.2f  [/cell][cell]%.2f[/cell]" % [m[0], m[1], m[2], m[3]]
			t += "[/table]\n\nWhat it means: Random Forest was best (0.83), but when people's words get more positive by 0.3 the accuracy falls to ~0.54. So the game never acts on language alone: the journal is only 20 % of the anxiety and depression scores, and the sensors have to agree.\n\n"
			if AI.journal_last.is_empty(): t += "Write in the daily journal at the crew console to see the live check."
			else: t += "Your last journal: sentiment %+.2f · stress index %.2f · class [b]%s[/b]" % [AI.journal_last.sentiment, AI.journal_last.stress, AI.journal_last["class"]]
			_rt(t, 15)
		"report":
			var t = "[b][font_size=22]Health report[/font_size][/b]\n\n[b]Individual (only you see this)[/b]\n"
			var fz = Fusion.assess(A, checkin, _in_shared_zone())
			t += "Now: %s (%d%% confidence)\n" % [fz.label, int(fz.confidence * 100)]
			if not A.last_sleep.is_empty(): t += "Last night: %.1f h, score %d\n" % [A.last_sleep.hours, A.last_sleep.score]
			if not checkin.is_empty(): t += "Check-in: BP %d/%d, SpO2 %d%%\n" % [checkin.sys, checkin.dia, checkin.spo2]
			for c in 6: t += "%s %.2f (tier %d) · " % [Ares.CONDS[c], AI.score[c], AI.tier[c]]
			t += "\n\n[b]Aggregate (for mission planners: no names, no single person)[/b]\nCrew of 4 · average sleep 7.1 h · CO2 exposure normal · offers this week: %d accepted, %d refused · Tier 3 events: %d · hard-limit blocks: %d" % [1 if AI.flags.has("accepted") else 0, 1 if AI.flags.has("refused") else 0, 1 if AI.flags.has("tier3") else 0, AI.blocked]
			_rt(t, 15)
		"protocols":
			var t = "[b][font_size=22]D3 · Response protocol for each condition[/font_size][/b]\nTiers at 0.35 / 0.55 / 0.75. A score must stay above a tier for a while before the AI acts. Under 50 % confidence the AI stays at Tier 1.\n"
			for c in 6:
				t += "\n[b][color=#7fd3ff]%s[/color][/b]  (score now %.2f, tier %d)\n" % [Ares.CONDS[c], AI.score[c], AI.tier[c]]
				var ss = []
				for i in 9:
					if Ares.W[c][i] > 0.0: ss.append("%s %d%%" % [Ares.SENSORS[i], int(Ares.W[c][i] * 100)])
				t += "1 Detect: %s\n2 Persist: %s h above a tier\n3 Tier 1 (silent, logged, max 3/day): %s\n4 Tier 2 (offer, 'no' = 6 h cooldown): %s\n5 Tier 3: you are told first, then the CMO (Commander if the CMO is on EVA)\n6 Monitor: %s, check: %s\nEvidence: %s\n" % [", ".join(ss), str(Ares.PERSIST_H[c]), Ares.TIER1[c], Ares.TIER2[c], Ares.MONITOR[c], Ares.CHECK[c], Ares.EVIDENCE[c]]
			_rt(t, 14)
		"habitat":
			_rt("[b][font_size=22]D2 · Habitat layout[/font_size][/b]\nModule: 18 m across, 4.2 m high ≈ 1,070 m³ = about 267 m³ per person for a crew of 4 (NASA minimum 20 m³, comfortable 120 m³). Green = shared (voice + face sensing allowed). Orange = private (door sensor only, wearables still work). Configuration %s." % ("B: second dining spot + acoustic screen" if AI.config_b else "A: one shared wardroom"), 15)
			var pl = Plot.new(); pl.kind = "plan"; pl.v = 1.0 if AI.config_b else 0.0; pl.custom_minimum_size = Vector2(800, 380); panel_box.add_child(pl)
		"crew":
			var t = "[b][font_size=22]Your data (crew interface)[/font_size][/b]\nOnly you see this. The AI log lists every change it made, including silent ones.\n\n"
			t += "AI: [b]%s[/b]%s   ·   tell me about silent changes: [b]%s[/b]   ·   Configuration %s\n\n" % ["ON" if AI.ai_on else "OFF", "  (paused)" if A.minute < AI.paused_until else "", "on" if AI.tell_me_silent else "off", "B" if AI.config_b else "A"]
			t += "[b]Your AI log[/b]\n" + ("\n".join(AI.entries.slice(0, 14)) if AI.entries.size() else "(nothing yet)")
			_rt(t, 15)
			var g = GridContainer.new(); g.columns = 3; panel_box.add_child(g)
			g.add_child(_btn("AI off / on (O)", func(): AI.set_ai(not AI.ai_on); _build_panel()))
			g.add_child(_btn("Pause support 24 h", func(): AI.pause_24h(); _build_panel()))
			g.add_child(_btn("Tell me about silent changes", func(): AI.tell_me_silent = not AI.tell_me_silent; _build_panel()))
			g.add_child(_btn("Configuration A / B (consent)", func(): AI.config_b = not AI.config_b; AI.note("You switched to Configuration " + ("B" if AI.config_b else "A")); AI.flag("configb"); _build_panel()))
			g.add_child(_btn("Human system review", func(): AI.review(); _build_panel()))
			g.add_child(_btn("Delete my log", func(): AI.entries.clear(); AI.note("You deleted your log"); _build_panel()))
			_rt("[b]Sensors you can switch off[/b] (heart rate and CO2 stay on for physical safety)", 15)
			var g2 = GridContainer.new(); g2.columns = 3; panel_box.add_child(g2)
			for i in 9:
				var ii = i
				var b2 = _btn("%s: %s" % [Ares.SENSORS[i], "OFF" if AI.opted_out[i] else "on"], func(): AI.opted_out[ii] = not AI.opted_out[ii]; AI.note(("Switched off " if AI.opted_out[ii] else "Switched on ") + Ares.SENSORS[ii]); _build_panel())
				b2.disabled = not Ares.OPT_OUT_ALLOWED[i]; g2.add_child(b2)
		"board":
			_rt("[b][font_size=22]Agency Review Board questions[/font_size][/b]\nEach button sets up the situation in the game and shows what the design does.", 15)
			var g = GridContainer.new(); g.columns = 3; panel_box.add_child(g)
			for q in range(1, 7):
				var qq = q
				g.add_child(_btn("Q%d%s" % [q, "  (done)" if AI.flags.has("q%d" % q) else ""], func(): ares_msg = AI.board(qq); _build_panel()))
			if ares_msg != "": _rt(ares_msg, 16)
		"limits":
			var t = "[b][font_size=22]8 hard limits (enforced in code)[/font_size][/b]\nPress Test: the AI tries to break the limit and is blocked. Blocked so far: [b]%d[/b]\n" % AI.blocked
			_rt(t, 15)
			for i in 8:
				var ii = i
				var hb = HBoxContainer.new(); panel_box.add_child(hb)
				hb.add_child(_btn("Test", func(): ares_msg = AI.test_limit(ii); _build_panel()))
				var l = Label.new(); l.text = "%d. %s" % [i + 1, Ares.HARD_LIMITS[i]]; l.autowrap_mode = TextServer.AUTOWRAP_WORD; l.custom_minimum_size = Vector2(700, 0); hb.add_child(l)
			if ares_msg != "": _rt("[color=#ff8a7a]" + ares_msg + "[/color]", 15)
		"tabletop":
			_rt("[b][font_size=22]Bonus A · 72-hour tabletop[/font_size][/b]\nRuns 72 hours fast with anxiety and conflict at once, a failed cortisol patch and the CMO on EVA. Your game state is put back afterwards.", 15)
			panel_box.add_child(_btn("Run the 72-hour tabletop", func(): ares_msg = AI.tabletop(); _build_panel()))
			if ares_msg != "": _rt(ares_msg, 14)
		"rubric":
			var t = "[b][font_size=22]Project ARES rubric in this game[/font_size][/b]\nTicks appear when you try each part.\n\n"
			var got = 0; var all = 0
			for row in AI.rubric():
				if row[1] == "": t += "\n[b][color=#7fd3ff]%s[/color][/b]\n" % row[0]; continue
				all += 1; var ok = AI.flags.has(row[1])
				if ok: got += 1
				t += ("[color=#5be38a]DONE[/color]  " if ok else "[color=#6f8398]to do[/color]  ") + row[0] + "\n"
			_rt("%s\n[b]%d / %d tried[/b]" % [t, got, all], 15)
	_close_row()

func _ares_dash() -> String:
	var t = "[b][font_size=20]ARES dashboard[/font_size][/b]   AI [b]%s[/b] · confidence [b]%d%%[/b]%s\n\n" % ["ON" if AI.ai_on else "OFF", int(AI.confidence * 100), "  (under 50%: Tier 1 only)" if AI.confidence < 0.5 else ""]
	t += "[b]D1 · 9 sensors[/b]\n[table=4]"
	for i in 9:
		t += "[cell]%s  [/cell][cell][color=%s]%s[/color]  [/cell][cell]%s  [/cell][cell][color=#9fb0c4]%s[/color][/cell]" % [Ares.SENSORS[i], "#5be38a" if AI.sensor_ok[i] else "#ff8a7a", ("%.2f" % AI.sensor_val[i]) if AI.sensor_ok[i] else "off", Ares.SENSOR_DEVICE[i], AI.sensor_why[i] if AI.sensor_why[i] != "" else Ares.THRESHOLD[i]]
	var kg = 0.0; var w = 0.0
	for i in 9: kg += Ares.MASS_KG[i]; w += Ares.POWER_W[i]
	t += "[/table]\n[color=#9fb0c4]One set per crew member + per zone: about 13 kg and 110 W for the whole base (this set: %.2f kg, %.1f W). Failure modes: %s.[/color]" % [kg, w, "; ".join(Ares.FAILURE.slice(0, 3))]
	t += "\n\n[b]D2/D3 · 6 conditions[/b]   tiers at 0.35 / 0.55 / 0.75\n[table=3]"
	for c in 6:
		var s: float = AI.score[c]
		var col = "#5be38a" if s < Ares.T1 else ("#ffd166" if s < Ares.T2 else ("#ffa94d" if s < Ares.T3 else "#ff5a4a"))
		t += "[cell]%s  [/cell][cell][color=%s]%s %.2f[/color]  [/cell][cell]Tier %d[/cell]" % [Ares.CONDS[c], col, "|".repeat(int(s * 30)), s, AI.tier[c]]
	t += "[/table]"
	if AI.pending_offer >= 0: t += "\n\n[color=#ffd166]Offer waiting: %s  (Y yes / N no)[/color]" % Ares.TIER2[AI.pending_offer]
	return t

# =============================================================================================
#  small 2D plots: ECG trace, hypnogram, mood circumplex
# =============================================================================================
class Plot extends Control:
	var kind = ""
	var data: Array = []
	var v = 0.0
	var a = 0.5
	var hr = 70.0
	var t = 0.0
	func _draw() -> void:
		var W = size.x; var H = size.y
		draw_rect(Rect2(Vector2.ZERO, size), Color(0.01, 0.03, 0.06))
		match kind:
			"ecg":
				for gx in range(0, int(W), 20): draw_line(Vector2(gx, 0), Vector2(gx, H), Color(0.1, 0.25, 0.15), 1)
				for gy in range(0, int(H), 20): draw_line(Vector2(0, gy), Vector2(W, gy), Color(0.1, 0.25, 0.15), 1)
				var pts = PackedVector2Array(); var beat = 60.0 / max(hr, 30.0)
				var span = 3.0
				for i in int(W):
					var tt: float = t - span + span * i / W
					var ph = fposmod(tt, beat) / beat
					var y = 0.0
					y += 0.12 * exp(-pow((ph - 0.15) / 0.03, 2))      # P
					y -= 0.12 * exp(-pow((ph - 0.27) / 0.008, 2))     # Q
					y += 1.0 * exp(-pow((ph - 0.3) / 0.012, 2))       # R
					y -= 0.25 * exp(-pow((ph - 0.33) / 0.01, 2))      # S
					y += 0.3 * exp(-pow((ph - 0.55) / 0.05, 2))       # T
					pts.append(Vector2(i, H * 0.65 - y * H * 0.5))
				draw_polyline(pts, Color(0.3, 1.0, 0.5), 2.0, true)
			"hypno":
				var names = ["Awake", "REM", "Light N1", "N2", "Deep N3"]
				var row = {0: 0, 4: 1, 1: 2, 2: 3, 3: 4}
				var cols = [Color(1, 0.5, 0.4), Color(0.6, 0.5, 1), Color(0.5, 0.8, 1), Color(0.3, 0.55, 0.95), Color(0.15, 0.3, 0.8)]
				for k in 5:
					draw_string(ThemeDB.fallback_font, Vector2(4, 22 + k * (H - 30) / 5.0), names[k], HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.7, 0.8, 0.9))
				var x0 = 80.0; var n: int = max(1, data.size())
				for i in data.size():
					var r: int = row.get(int(data[i]), 3)
					var y = 10 + r * (H - 30) / 5.0
					draw_rect(Rect2(x0 + (W - x0 - 10) * i / n, y, (W - x0 - 10) / n + 1, (H - 30) / 5.0 - 4), cols[r])
				draw_string(ThemeDB.fallback_font, Vector2(x0, H - 4), "lights out  -  %.1f h  -  wake" % (data.size() * 5.0 / 60.0), HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.7, 0.8, 0.9))
			"plan":
				var c0 = Vector2(W * 0.5, H * 0.5); var Rr: float = min(W, H) * 0.46; var f2 = ThemeDB.fallback_font
				draw_circle(c0, Rr, Color(0.12, 0.16, 0.22))
				draw_arc(c0, Rr, 0, TAU, 64, Color(0.6, 0.7, 0.8), 2.0)
				var sc = Rr / 9.0
				var zones = [["Quarters", Vector2(-7.0, 0), Color(1, 0.6, 0.25), 2.2], ["Medical", Vector2(0, 7.0), Color(0.35, 0.85, 0.55), 1.8], ["Wardroom", Vector2(0, 0), Color(0.35, 0.85, 0.55), 2.2], ["Exercise", Vector2(-3.5, -6.8), Color(0.35, 0.85, 0.55), 1.6], ["Console", Vector2(3.5, -7.2), Color(0.35, 0.85, 0.55), 1.2], ["Airlock", Vector2(8.0, 0), Color(0.7, 0.75, 0.85), 1.4]]
				if v > 0.5: zones.append(["2nd dining (Config B)", Vector2(4.5, 3.5), Color(0.45, 0.8, 1.0), 1.4])
				for z in zones:
					var pz = c0 + Vector2(z[1].x, z[1].y) * sc
					draw_circle(pz, z[3] * sc, Color(z[2], 0.35)); draw_arc(pz, z[3] * sc, 0, TAU, 32, z[2], 2.0)
					draw_string(f2, pz + Vector2(-50, 4), z[0], HORIZONTAL_ALIGNMENT_CENTER, 100, 12, Color.WHITE)
				draw_line(c0 + Vector2(-5.6, -1.2) * sc, c0 + Vector2(-5.6, 1.2) * sc, Color(0.4, 0.55, 0.9), 4.0)
				draw_string(f2, c0 + Vector2(-5.4, 1.9) * sc, "curtain + door sensor", HORIZONTAL_ALIGNMENT_LEFT, -1, 11, Color(0.7, 0.8, 1.0))
				draw_string(f2, Vector2(10, H - 10), "scale: radius 9 m · sensor pod in every shared zone · beacon + wristband go with you", HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(0.7, 0.8, 0.9))
			"circumplex":
				var c = Vector2(W * 0.5, H * 0.5); var R: float = min(W, H) * 0.42
				draw_arc(c, R, 0, TAU, 64, Color(0.3, 0.5, 0.7), 2.0)
				draw_line(c - Vector2(R, 0), c + Vector2(R, 0), Color(0.25, 0.35, 0.5)); draw_line(c - Vector2(0, R), c + Vector2(0, R), Color(0.25, 0.35, 0.5))
				var f = ThemeDB.fallback_font
				draw_string(f, c + Vector2(-30, -R - 6), "tense", HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(1, 0.6, 0.5))
				draw_string(f, c + Vector2(-26, R + 16), "calm", HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.5, 0.8, 1))
				draw_string(f, c + Vector2(R + 6, 4), "happy", HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.5, 1, 0.6))
				draw_string(f, c + Vector2(-R - 40, 4), "low", HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.7, 0.6, 1))
				draw_string(f, c + Vector2(R * 0.55, -R * 0.75), "excited", HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(0.8, 0.9, 1))
				draw_string(f, c + Vector2(-R * 0.95, -R * 0.75), "stressed", HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(0.8, 0.9, 1))
				draw_string(f, c + Vector2(R * 0.55, R * 0.85), "relaxed", HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(0.8, 0.9, 1))
				draw_string(f, c + Vector2(-R * 0.95, R * 0.85), "tired / bored", HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(0.8, 0.9, 1))
				var p = c + Vector2(v * R, -(a - 0.5) * 2.0 * R)
				draw_circle(p, 9, Color(1, 0.85, 0.3)); draw_arc(p, 14, 0, TAU, 24, Color(1, 0.85, 0.3, 0.5), 2)

# =============================================================================================
#  automated screenshots for the README (godot … -- --shots=<dir>)
# =============================================================================================
func _shot_case() -> void:
	_close_panel(); player.global_position = Vector3(1.5, 0.1, 2.0); yaw = deg_to_rad(200); pitch = 0.0
	AI.start_case(1); case_running = true
	for i in 300:
		A.step(1.0); AI.step(1.0, true)
		if AI.pending_offer >= 0: break

func _shot_ares() -> void:
	_close_panel(); A.stress = 0.6; A.valence = -0.3
	for i in 240: A.step(1.0); AI.step(1.0, true)
	ares_tab = "dash"; _open_panel("ares")

func _shot_report() -> void:
	for i in 470: A.step(1.0)
	_wake()

func _shots(dir: String) -> void:
	var plan = [
		["hab", func(): _close_panel(); player.global_position = Vector3(3.5, 0.1, 4.5); yaw = deg_to_rad(40); pitch = -0.05],
		["quarters", func(): player.global_position = Vector3(-3.0, 0.1, 1.0); yaw = deg_to_rad(80); pitch = -0.15],
		["station", func(): player.global_position = Vector3(0, 0.1, 4.6); yaw = deg_to_rad(180); pitch = 0.0; ecg_t = 0.0; _open_panel("station")],
		["checkin", func(): ecg_t = 7.0],
		["feel", func(): _close_panel(); _open_panel("feel")],
		["pod", func(): _close_panel(); player.global_position = Vector3(1.5, 0.1, 1.5); _open_panel("pod")],
		["wires", func(): _close_panel(); player.global_position = Vector3(5.0, 0.1, -3.0); yaw = deg_to_rad(-30); pitch = 0.45],
		["eva", func(): _start_eva(); A.activity = 0.6; A.minute += 30],
		["eva2", func(): yaw = deg_to_rad(-150); pitch = -0.05],
		["sleep", func(): _end_eva(false); A.wristband_on = true; A.minute = 22 * 60.0; _sleep(); sleep_fast = true],
		["report", _shot_report],
		["ares_dash", _shot_ares],
		["ares_board", func(): ares_tab = "board"; ares_msg = AI.board(2); _build_panel()],
		["ares_limits", func(): ares_tab = "limits"; ares_msg = AI.test_limit(5); _build_panel()],
		["ares_model", func(): AI.journal("tired today and I miss home, argued at lunch"); ares_tab = "model"; _build_panel()],
		["ares_protocols", func(): ares_tab = "protocols"; _build_panel()],
		["ares_habitat", func(): ares_tab = "habitat"; _build_panel()],
		["ares_trace", func(): ares_tab = "trace"; _build_panel()],
		["overview", func(): _close_panel(); player.global_position = Vector3(-2.0, 0.1, -2.0); yaw = deg_to_rad(-45); pitch = -0.2],
		["ares_rubric", func(): ares_tab = "rubric"; _build_panel()],
		["ares_cases", func(): ares_tab = "cases"; _build_panel()],
		["case_offer", _shot_case],
	]
	DirAccess.make_dir_recursive_absolute(dir)
	for step in plan:
		step[1].call()
		for i in 40: await get_tree().process_frame
		var img = get_viewport().get_texture().get_image()
		img.save_png(dir + "/" + step[0] + ".png")
		print("shot ", step[0])
	get_tree().quit()

# =============================================================================================
#  autoplay test (godot … -- --autotest): plays the whole game like a person and prints BUG lines
# =============================================================================================
var _bugs = 0
func _bug(t: String) -> void: _bugs += 1; print("BUG: ", t)
func _frames(n: int) -> void:
	for i in n: await get_tree().physics_frame

func _all_targets() -> Array:
	var out = []
	var st = [self]
	while st.size():
		var n = st.pop_back()
		for c in n.get_children(): st.append(c)
		if n is StaticBody3D and n.has_meta("id"): out.append(n)
	return out

## stand somewhere sensible, look at the thing, press E for real (ray + collider), return whether it was targeted
func _aim_use(sb: StaticBody3D, press := true) -> bool:
	var tgt: Vector3 = sb.global_transform * ((sb.get_child(0) as CollisionShape3D).position)
	var base = SURFACE if tgt.x > 40.0 else Vector3.ZERO
	var flat = Vector3(tgt.x - base.x, 0, tgt.z - base.z)
	var away = -flat.normalized() if flat.length() > 0.5 else Vector3(1, 0, 0)
	if base != Vector3.ZERO: away = Vector3(-1, 0, 0)
	for d in [1.5, 1.1, 2.0, 2.6]:
		for rot in [0.0, 0.6, -0.6, 1.2, -1.2]:
			var stand = tgt + away.rotated(Vector3.UP, rot) * d; stand.y = 0.1 + base.y
			player.global_position = stand; player.velocity = Vector3.ZERO
			var eye = stand + Vector3(0, 1.62, 0); var v = tgt - eye
			yaw = atan2(-v.x, -v.z); pitch = atan2(v.y, Vector2(v.x, v.z).length())
			await _frames(3)
			if looking_at == sb:
				if press: _use()
				return true
	return false

func _press_all_buttons(skip: Array = []) -> void:
	var btns = []
	var st = [panel_box]
	while st.size():
		var n = st.pop_back()
		for c in n.get_children(): st.append(c)
		if n is Button and not n.disabled and not skip.has(n.text): btns.append(n.text)
	return

func _autotest() -> void:
	print("AUTOTEST start")
	await _frames(5)
	_close_panel()
	A.minute = 7 * 60.0 + 30.0
	# 1) every object in the habitat can be targeted and used
	var ids_seen = {}
	for sb in _all_targets():
		var id = str(sb.get_meta("id"))
		if id in ["sample", "hatch", "bunk", "suit"]: continue
		var ok = await _aim_use(sb)
		ids_seen[id] = ok
		if not ok: _bug("can't aim at '%s' at %s" % [id, str(sb.global_position)])
		if panel_kind != "":
			if panel_kind == "station":
				for i in 60: await get_tree().process_frame
				ecg_t = 7.0; await get_tree().process_frame
				if checkin.is_empty(): _bug("station check-in did not save")
			if panel_kind == "console":
				for c in panel_box.get_children():
					if c is GridContainer:
						for b in c.get_children(): (b as Button).pressed.emit()
						break
			_close_panel()
		if on_treadmill:
			for i in 400:
				await get_tree().process_frame
				if not on_treadmill: break
			if on_treadmill: _bug("treadmill never finished")
			if player.global_position.distance_to(Vector3(-3.5, 0, -6.8)) < 0.6: _bug("left treadmill inside its frame")
	print("used: ", ids_seen)
	if A.beacon_on == false: A.beacon_on = true
	# 2) every panel + every ARES tab builds and its buttons work
	for k in ["help", "tasks", "feel", "pod", "station", "console"]:
		_open_panel(k); await get_tree().process_frame; _close_panel()
	for tb in ["cases", "dash", "trace", "protocols", "habitat", "model", "report", "crew", "board", "limits", "tabletop", "rubric"]:
		ares_tab = tb; _open_panel("ares"); await get_tree().process_frame
		if tb in ["board", "limits", "crew", "tabletop"]:
			var names = []
			var st = [panel_box]
			while st.size():
				var n = st.pop_back()
				for c in n.get_children(): st.append(c)
				if n is Button and not n.disabled and n.get_parent() is not HFlowContainer and not n.text.begins_with("Close"): names.append(n.text)
			for nm in names:
				var found = null
				var st2 = [panel_box]
				while st2.size():
					var n = st2.pop_back()
					for c in n.get_children(): st2.append(c)
					if n is Button and n.text == nm and not n.is_queued_for_deletion(): found = n
				if found: found.pressed.emit(); await get_tree().process_frame
		_close_panel()
	if not AI.ai_on: AI.set_ai(true)
	AI.paused_until = 0.0
	for k in 9: AI.opted_out[k] = false
	# 3) each health case plays out to a result
	for i in Ares.CASES.size():
		ares_msg = AI.start_case(i); case_running = true; speed_i = 2; case_msg = ""
		var t0 = Time.get_ticks_msec()
		var steps = 0
		while case_running and steps < 2000:
			A.step(1.0); AI.step(1.0, true); steps += 1
			if i == 4 and AI.tier[Ares.CASES[4][2]] >= 3: case_running = false; case_msg = AI.case_result()
			elif i != 4 and AI.pending_offer >= 0:
				if AI.pending_offer != Ares.CASES[i][2]: _bug("case %d: the offer was about %s, not %s" % [i, Ares.CONDS[AI.pending_offer], Ares.CONDS[Ares.CASES[i][2]]])
				AI.answer_offer(i % 2 == 0); _after_offer()
		if case_running: _bug("case %d (%s) never triggered in %d sim-min" % [i, Ares.CASES[i][0], steps]); case_running = false
		else:
			print("case ", i, " ok after ", steps, " min: ", case_msg.left(110))
			if not ("tier 0" in case_msg) and false: pass
			if "(tier 0)" in case_msg: _bug("case %d ended but its own condition never left tier 0" % i)
	speed_i = 0
	# 4) EVA twice: checkpoints must be collectable both times
	for run in 2:
		A.minute = A.day() * 1440.0 + 10 * 60.0
		_start_eva()
		if not A.on_eva: _bug("EVA didn't start"); break
		await _frames(5)
		var got = 0
		for smp in sample_nodes:
			for c in smp.get_children():
				if c is StaticBody3D and await _aim_use(c): got += 1
		if samples != 3: _bug("EVA run %d: collected %d/3 checkpoints (aimed %d)" % [run + 1, samples, got])
		var hatch = null
		for sb in _all_targets(): if sb.get_meta("id") == "hatch": hatch = sb
		if not await _aim_use(hatch): _bug("can't reach the airlock hatch")
		if A.on_eva: _bug("hatch didn't end the EVA"); _end_eva(false)
		await _frames(10)
		if player.global_position.y < -1: _bug("fell through the floor after EVA")
	# 5) fall off the surface edge -> respawn
	_start_eva(); player.global_position = SURFACE + Vector3(0, -7, 0); await _frames(3)
	if player.global_position.y < -6: _bug("no respawn after falling")
	_end_eva(false)
	# 6) sleep: bunk at night, let the body clock wake you, you must be standing again with a report
	A.minute = A.day() * 1440.0 + 22 * 60.0 + 30.0; A.wristband_on = true
	var bunk = null
	for sb in _all_targets(): if sb.get_meta("id") == "bunk": bunk = sb
	if not await _aim_use(bunk): _bug("can't aim at the bunk")
	if not A.asleep: _bug("bunk did not start sleep")
	sleep_fast = true
	var fr = 0
	while A.asleep and fr < 3000: await get_tree().process_frame; fr += 1
	if A.asleep: _bug("never woke up")
	if panel_kind != "sleep": _bug("no sleep report after waking up on your own")
	if player.global_position.x < -6.0: _bug("still lying in the bunk after waking")
	print("slept: ", A.last_sleep)
	_close_panel()
	# 7) new day resets tasks; run a few days quickly for numerical blow-ups
	var d0 = A.day()
	for i in 3 * 1440: A.step(1.0); AI.step(1.0, i % 3 == 0)
	await get_tree().process_frame
	if task_day != A.day(): _bug("tasks not reset on a new day")
	for v in [A.hr, A.hrv, A.bp_sys, A.spo2, A.stress, A.valence, A.co2_ppm]:
		if is_nan(float(v)) or is_inf(float(v)): _bug("NaN/inf in body model")
	for c in 6:
		if is_nan(AI.score[c]): _bug("NaN in ARES score")
	print("AUTOTEST done, bugs=", _bugs)
	get_tree().quit()

## website card pictures: wide views of the habitat with no panels
func _hero(dir: String) -> void:
	DirAccess.make_dir_recursive_absolute(dir)
	_close_panel(); ui.visible = false; cam.fov = 82
	var views = [
		["hero_wide", 13 * 60.0, Vector3(4.0, 0.1, -6.2), 150.0, -0.15],
		["hero_night", 22 * 60.0, Vector3(4.0, 0.1, -6.2), 150.0, -0.15],
		["hero_suit", 13 * 60.0, Vector3(3.0, 0.1, 2.2), -70.0, -0.12],
		["hero_eva", 13 * 60.0, SURFACE + Vector3(4.0, 0.1, 6.0), -120.0, 0.02],
	]
	for v in views:
		A.minute = v[1]; player.global_position = v[2]; yaw = deg_to_rad(v[3]); pitch = v[4]
		if v[0] == "hero_eva": A.on_eva = true
		for i in 160: await get_tree().process_frame
		get_viewport().get_texture().get_image().save_png(dir + "/" + v[0] + ".png")
		print("hero ", v[0])
	get_tree().quit()
