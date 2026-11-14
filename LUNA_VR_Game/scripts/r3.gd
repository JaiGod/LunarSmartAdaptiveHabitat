class_name R3
extends Node
## Round 3: every object is easy to use and does something, plus the realistic features of the team's earlier
## NASA HUNCH games (Unity 3 "Moon_Base_Final" and Lunar Habitat Commander), rebuilt on the LUNA engine:
##   crew check-ins with consent · daily wellness tasks · the AI OFF switch · night watch (hardware watchdog)
##   team meetings · the safety drill · Config A/B · room controls (light, sound, temperature, scent)
##   AI dashboard (break a sensor, test a hard limit) · CMO console · review board (Q1–Q6, D1–D6, rubric,
##   72-hour tabletop) · crew interface (opt out, pause, export) · zone cards · guide-me paths.
## The unrealistic parts of those games (aliens, portals, weapons, flying) are left out on purpose.

const C = 1.25
const COL_TOUCH = Color("#33d6ff")      # press / touch (same colour language as the LUNA-X Blender assets)
const COL_GRIP = Color("#8affb0")       # grab / pull / sit / lie
const COL_PEOPLE = Color("#ffc46b")     # a crewmate you can check on
const TOUCH_IDS = ["dock", "ai_switch", "config_switch", "drill_alarm", "meeting", "galley_water", "coffee", "pod_ai_switch",
	"pod_music", "gh_irrigation", "o2_gen", "exam_bed", "parts_shelf", "shelter_kit", "med_cabinet", "printer"]

## ARES zone types (brief §4.1) + what each room's sensors may see
const ZONE_INFO = {
	"airlock": ["Transition · EVA + dust lock", "neutral 4000 K", "particle counter · door sensor"],
	"hygiene": ["Private · hygiene", "occupant sets it", "none at all (privacy)"],
	"quarters": ["Private retreat ×4", "occupant sets it · dawn simulation", "pod door only · no camera · no microphone"],
	"communal": ["Communal zone", "warm 2700–3000 K", "CO₂ · UWB beacons · voice + face (shared rooms only)"],
	"greenhouse": ["Nature + growth bay", "red/blue grow light + light-pipe dome", "CO₂ · UWB beacons"],
	"corridor": ["Transition corridor", "fades between zones (3800 K)", "UWB beacons"],
	"medical": ["Medical + storm shelter", "neutral 4000 K", "vitals only on request · CMO station"],
	"eclss": ["Life support", "neutral 4000 K", "CO₂ · O₂ · water quality"],
	"storage": ["Storage + robot bay", "neutral 4000 K", "inventory tags"],
	"work": ["Work + mission bay", "cool 6000–6500 K · faster airflow", "CO₂ · task speed · voice + face (shared rooms only)"],
	"exercise": ["Exercise bay", "energise 5600 K + audio on entry", "heart rate in context: exercise is not anxiety (Q1)"],
}
const ARES_TYPE = {"communal": "communal", "quarters": "private", "work": "work", "exercise": "exercise", "greenhouse": "nature", "corridor": "transition"}

## the rubric self-check (Project ARES 200 points + the two bonuses), ticked live
const RUBRIC = [
	["D1 Sensors", "dash", "Open the AI dashboard (Ops, governance wall)"],
	["D1 Sensors", "sensor_break", "Break a sensor and watch graceful degradation"],
	["D1 Sensors", "exercise", "Exercise in the exercise bay: no false alarm (Q1)"],
	["D2 Spatial", "zones", "Visit all six ARES zone types"],
	["D2 Spatial", "own_pod", "Sleep in your own pod"],
	["D2 Spatial", "config_b", "Switch the base to Configuration B"],
	["D3 Response", "tier1", "Read a Tier 1 silent change in the LUNA log"],
	["D3 Response", "tier2", "Answer a Tier 2 offer"],
	["D3 Response", "meeting", "Run a team meeting at the wardroom table"],
	["D3 Response", "cmo", "Review Tier 3 cases on the CMO console"],
	["D3 Response", "drill", "Lead a safety drill to the muster point"],
	["D3 Response", "checkin", "Check on a crewmate (they consent first)"],
	["D4 Ethics", "said_no", "Say no to a LUNA offer: it stops and logs it"],
	["D4 Ethics", "opt_out", "Opt out of a sensor (crew interface)"],
	["D4 Ethics", "ai_off", "Switch LUNA off with the AI OFF switch"],
	["D4 Ethics", "export", "Export everything LUNA recorded about you"],
	["D4 Ethics", "hard_limit", "Test a hard limit: it is blocked and counted"],
	["D4 Ethics", "room_override", "Override LUNA in a room (room controls)"],
	["D4 Ethics", "review", "Sign off the weekly human review of LUNA"],
	["D5 Board", "board", "Run a review-board question (Q1–Q6)"],
	["D5 Board", "map", "Show the habitat on the digital twin"],
	["Bonus A", "tabletop", "Run the 72-hour tabletop simulation"],
	["Bonus B", "crewif", "Open your crew interface (pod desk → My data)"],
]

var g
var sim
var hab
var world: Node3D
var card = {}                     # popup card: {"title","lines":[[text, col]],"bars":[[name, v]],"buttons":[[label, Callable, hue]]}
var markers = []                  # {"it","s": Sprite3D,"lab": Label3D,"base": Vector3}
var known = 0
var tex = {}
var t = 0.0
var zone_now = ""
var zones_seen = {}
var zone_lab: Label3D
var zone_t = 0.0
var done = {}                     # rubric id -> sim minute
var drill = {}
var meeting = {}
var asked_at = {}                 # crew index -> last check-in request (sim minutes)
var day_tasks = {}                # day -> {"checked": n, "helped": n}
var path_nodes = []
var path_t = 0.0
var path_goal = Vector2i(-1, -1)
var night_fired = false
var room_audio = {}               # room -> AudioStreamPlayer3D
var room_set = {}                 # room -> {"light","sound","temp","scent"}
var scent_fx = {}                 # room -> CPUParticles3D
var lever: Node3D
var lamp_mat: StandardMaterial3D
var knob: Node3D
var beacon_mat: StandardMaterial3D
var pod_toggle: Node3D
var music: AudioStreamPlayer3D
var tabletop = {}                 # {"sim": Sim, "run": bool, "report": Dictionary}
var hl_next = 0
var mode = "awake"                 # awake | evening | sleep | emergency (whole habitat)
var music_on = true
var bgm: AudioStreamPlayer
var vent: AudioStreamPlayer
var strip_mat: StandardMaterial3D
var pod_temp = 20
var sleep_sound = "vent"
var pod_air = 22.0
var ac_mode = "auto"
var ac_state = "hold"
var ac_mat: StandardMaterial3D
var ac_air: AudioStreamPlayer3D           # vent (air circulation) | silence
var feedstock = 6.0
var auto = false
var auto_path = []
var auto_t = 0.0
var auto_goal = {}
var auto_best = 1e9
var veto_t = 0.0
var auto_walk = 0.0                # seconds spent walking to the current goal
var lessons = {}                   # what the autopilot has learned across runs: goal -> {"runs","stuck","walk","alt"}
var play_log = []                  # notes for the playtest report
const LESSONS = "user://autopilot_lessons.json"
var auto_stuck = 0.0
var demo_i = -1
var demo_t = 0.0                # kg of recycled-plastic printer feedstock
var snd = {}


## every usable thing: what it does, why it is aboard, upkeep, what it costs to run, and its honest limits
## (ARES brief §7: "every sensor and actuator must be justified in mass, power and mission-critical value")
const SPECS = {
	"bed": ["Smart bed", "Tracks your sleep. No camera.", "Wash cover weekly", "3 W", "Sleep stages are a guess"],
	"cabinet": ["Herb cabinet", "Grow your own herbs", "Add water weekly", "18 W light, 0.3 L water a day", "Herbs only, not real food supply"],
	"seat": ["Table seat", "Eat together", "Wipe after meals", "2 kg", "4 seats, 4 crew"],
	"sofa": ["Sofa", "Relax and watch movies", "Clean covers monthly", "12 kg", "Movies need free comms time"],
	"galley": ["Food warmer", "Hot meals", "Clean weekly", "0.6 kW while heating", "Heats pouches only"],
	"galley_water": ["Water tap", "Drinking water", "New filter every 90 days", "About 2.5 L per person a day", "Tank can run low"],
	"coffee": ["Espresso machine", "Coffee (one flew on the ISS)", "Empty the bin daily", "1.1 kW for 40 s, 1 capsule", "Capsules must come from Earth"],
	"meeting": ["Meeting tablet", "Call the crew to talk", "None", "6 W", "Only works if people come"],
	"books": ["E-readers", "Read books (lighter than paper)", "Charge weekly", "0.3 kg each", "New books come by radio"],
	"ai_switch": ["AI OFF switch", "Stops LUNA right away", "Test it every week", "No power needed", "LUNA still watches for danger"],
	"config_switch": ["Floor plan switch", "Gives people space after a fight", "Clean the track monthly", "40 W while moving", "Only splits the dining room"],
	"drill_alarm": ["Drill alarm", "Practice getting to the shelter", "Drill weekly", "5 W", "Practice only"],
	"exam_bed": ["Exam bed", "Check blood pressure and pulse", "Calibrate monthly", "40 W", "Only the doctor diagnoses"],
	"med_cabinet": ["Medicine cabinet", "Medicines. Doctor only.", "Check dates weekly", "8 kg", "LUNA can never open it"],
	"shelter_kit": ["Shelter kit", "Supplies for 3 days in the shelter", "Check monthly", "40 kg", "3 days for 4 people"],
	"pod_ai_switch": ["Pod switch", "Lets LUNA into your pod, or not", "None", "Tiny", "Alarms still work"],
	"safety": ["Fire extinguisher + breathing mask", "Put out a fire; breathe if the air turns bad", "Check the gauge and seal monthly", "6 kg (water mist) + 1 kg mask", "Mask gives about 15 min of oxygen"],
	"pod_music": ["Speaker", "Your music", "None", "3 W", "New songs come by radio"],
	"photo": ["Family photo", "A reminder of home", "None", "0.1 kg", "Calls need comms time"],
	"shower": ["Shower", "Get clean", "Clean the drain daily", "3 L water, 0.8 kW", "Short showers only"],
	"sink": ["Sink", "Wash your hands", "New filter monthly", "0.5 L a wash", "Water is limited"],
	"o2_gen": ["Oxygen maker", "Makes air to breathe from water", "Inspect every 6 months", "1.2 kW", "Needs power"],
	"cartridge": ["CO2 filter", "Cleans CO2 out of the air", "Swap about every 30 days", "6 kg spare", "Only one spare"],
	"parts_shelf": ["Spare parts", "Fix things", "Count weekly", "Heavy to launch", "More come on day 90"],
	"printer": ["3D printer", "Prints small spare parts", "Clean after each print", "120 W for 3 h, 0.4 kg plastic", "No parts that hold pressure"],
	"eva_tools": ["EVA tools", "Brush, bags, tethers for outside", "Check every EVA", "9 kg", "Dust wears them out"],
	"gh_irrigation": ["Plant pump", "Waters the plants", "Check water daily", "45 W", "Plants grow slowly"],
	"gh_bench": ["Bench", "Sit with the plants", "None", "6 kg", "-"],
	"desk": ["Work desk", "Work on the computer", "Dust filter monthly", "90 W", "-"],
	"treadmill": ["Treadmill", "Keeps bones and muscles strong", "Check belt weekly", "250 W", "Needs a harness in low gravity"],
	"bike": ["Exercise bike", "Cardio with a nice view", "Check monthly", "60 W", "The view is a screen"],
	"rig": ["Weight machine", "Builds strength", "Check monthly", "No power", "Very heavy to launch"],
	"recovery_chair": ["VR chair", "Calm nature scenes", "Clean the headset", "30 W", "A break, not therapy"],
	"suitport": ["Suitport", "Climb into a spacesuit", "Check the seal every use", "Oxygen for each EVA", "One person per port"],
	"suitport_return": ["Suitport", "Come back inside", "Check the seal", "-", "Dust still sticks"],
	"boot_brush": ["Boot brush", "Brush moon dust off", "New bristles monthly", "2 kg", "Fine dust needs the vacuum"],
	"dock": ["Wellness Dock", "A 1-minute health check", "Calibrate monthly", "120 W while scanning", "Only when you choose"],
	"earth_window": ["Earth screen", "Live view of Earth", "Clean the camera on EVA", "60 W", "Earth is sometimes below the horizon"],
}

func spec_line(it: Dictionary) -> String:
	var sp = _spec(it.id)
	if sp.is_empty(): return ""
	return "\nUses: %s  ·  X: info" % sp[3]

func _spec(id: String) -> Array:
	if id.begins_with("rack_"): return ["Plant rack", "Grows food", "Clean weekly", "0.4 kW light, 2 L water a day", "Crops take weeks"]
	if id.begins_with("array_"): return ["Solar panel", "Makes power", "Brush off dust", "Makes about 18 kW", "Dust and shadow cut power"]
	if id.begins_with("crew_"): return ["Crewmate", "Ask how they are", "-", "-", "They can say no"]
	return SPECS.get(id, [])

func show_spec(it: Dictionary) -> void:
	var sp = _spec(it.id)
	if sp.is_empty(): return
	show_card({"title": sp[0], "lines": [["Job: " + sp[1], UI.INK], ["Uses: " + sp[3], UI.INK], ["Care: " + sp[2], UI.INK],
		["Limit: " + sp[4], UI.WARN]], "buttons": [["Close", func(): close_card(), 0.62]]})

# =====================================================================================================
#  setup
# =====================================================================================================
func setup(game) -> void:
	g = game; sim = g.sim; hab = g.hab
	world = Node3D.new(); world.name = "R3_world"; hab.add_child(world)
	_governance_wall()
	_medical()
	_galley_and_hub()
	_pod_and_hygiene()
	_service_rooms()
	_room_panels()
	_night_strips()
	_pod_panel()
	_safety_stations()
	_crew_areas()
	_sounds()
	zone_lab = Label3D.new(); zone_lab.font_size = 34; zone_lab.pixel_size = 0.001; zone_lab.outline_size = 9
	zone_lab.outline_modulate = Color(0.02, 0.05, 0.12, 0.85); zone_lab.modulate = Color(0.85, 0.94, 1.0, 0.0)
	zone_lab.no_depth_test = true; zone_lab.width = 1100; zone_lab.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	zone_lab.position = Vector3(0, -0.36, -1.25); zone_lab.visible = false

func _m(col: Color, rough := 0.5, metal := 0.0) -> StandardMaterial3D:
	var m = StandardMaterial3D.new(); m.albedo_color = col; m.roughness = rough; m.metallic = metal
	return m

func _glow(col: Color, e := 1.5) -> StandardMaterial3D:
	var m = StandardMaterial3D.new(); m.albedo_color = col; m.emission_enabled = true; m.emission = col; m.emission_energy_multiplier = e
	return m

func _mesh(parent: Node3D, mesh: Mesh, pos: Vector3, mat: Material, rot := Vector3.ZERO) -> MeshInstance3D:
	var mi = MeshInstance3D.new(); mi.mesh = mesh; mi.material_override = mat; mi.position = pos; mi.rotation = rot
	parent.add_child(mi)
	return mi

func _boxm(s: Vector3) -> BoxMesh:
	var b = BoxMesh.new(); b.size = s; return b

func _cylm(r: float, h: float, r2 := -1.0) -> CylinderMesh:
	var c = CylinderMesh.new(); c.top_radius = r if r2 < 0.0 else r2; c.bottom_radius = r; c.height = h; c.radial_segments = 20
	return c

func _label(text: String, pos: Vector3, yaw: float, size := 20, col := Color(0.12, 0.14, 0.18), px := 0.0024) -> Label3D:
	var l = Label3D.new(); l.text = text; l.font_size = size; l.pixel_size = px; l.modulate = col; l.outline_size = 0
	l.position = pos; l.rotation.y = yaw; l.double_sided = false
	world.add_child(l)
	return l

func _screen_frame(m: MB, pos: Vector3, yaw: float, w: float, h: float) -> void:
	# black bezel behind a wall screen (the screen faces along its yaw)
	var n = Vector3(sin(yaw), 0, cos(yaw))
	var sz = Vector3(w + 0.06, h + 0.06, 0.04) if absf(n.z) > 0.5 else Vector3(0.04, h + 0.06, w + 0.06)
	m.box(pos - n * 0.025, sz, Mats.black())

## the governance wall in Ops (west wall): humans stay in charge of LUNA
func _governance_wall() -> void:
	var m = MB.new()
	var x = 14 * C + 0.06
	_label("HUMANS STAY IN CHARGE · LUNA governance", Vector3(x + 0.01, 2.2, 17.4), PI / 2.0, 30, Color(0.12, 0.2, 0.36), 0.004)
	for s in [["dashboard", 15.75], ["board", 16.95]]:
		var p = Vector3(x + 0.02, 1.55, s[1])
		_screen_frame(m, p, PI / 2.0, 0.9, 0.56)
		hab.add_screen(s[0], p, PI / 2.0, Vector2(0.9, 0.56), Vector2i(900, 560), null)
	_label("AI dashboard", Vector3(x + 0.01, 1.9, 15.75), PI / 2.0, 18)
	_label("Review board", Vector3(x + 0.01, 1.9, 16.95), PI / 2.0, 18)
	# AI OFF switch: hard-wired, beside LUNA's computer rack
	var sp = Vector3(x, 1.3, 19.65)
	m.box(sp + Vector3(0.0, 0, 0), Vector3(0.03, 0.46, 0.36), Mats.caution())
	m.box(sp + Vector3(0.04, 0, 0), Vector3(0.07, 0.36, 0.26), Mats.red())
	lever = Node3D.new(); lever.position = sp + Vector3(0.09, 0.0, 0.0); world.add_child(lever)
	_mesh(lever, _boxm(Vector3(0.04, 0.17, 0.05)), Vector3(0.02, 0.085, 0), Mats.white_plastic())
	_mesh(lever, _cylm(0.03, 0.06), Vector3(0.03, 0.17, 0), Mats.black(), Vector3(0, 0, PI / 2.0))
	lamp_mat = _glow(Color(0.25, 1.0, 0.45), 2.2)
	_mesh(world, SphereMesh.new(), sp + Vector3(0.05, 0.3, 0), lamp_mat).scale = Vector3.ONE * 0.07
	_label("AI OFF SWITCH\nhard-wired · only a\nhuman turns it back on", sp + Vector3(0.05, -0.36, 0), PI / 2.0, 14)
	hab.add_inter("ai_switch", sp + Vector3(0.1, 0.02, 0), Vector3(0.22, 0.5, 0.4), "", Callable(), "work")
	# floor plan A / B selector
	var cp = Vector3(x, 1.3, 18.9)
	m.box(cp + Vector3(0.02, 0, 0), Vector3(0.05, 0.3, 0.3), Mats.white_plastic())
	knob = Node3D.new(); knob.position = cp + Vector3(0.06, 0, 0); world.add_child(knob)
	_mesh(knob, _cylm(0.06, 0.04), Vector3(0.02, 0, 0), Mats.handrail(), Vector3(0, 0, PI / 2.0))
	_mesh(knob, _boxm(Vector3(0.02, 0.1, 0.02)), Vector3(0.045, 0.04, 0), Mats.white_plastic())
	_label("A", cp + Vector3(0.05, 0.11, 0.09), PI / 2.0, 16); _label("B", cp + Vector3(0.05, 0.11, -0.09), PI / 2.0, 16)
	_label("FLOOR PLAN\nA nominal · B conflict", cp + Vector3(0.05, -0.24, 0), PI / 2.0, 14)
	hab.add_inter("config_switch", cp + Vector3(0.09, 0, 0), Vector3(0.18, 0.36, 0.36), "", Callable(), "work")
	# safety drill alarm (mushroom button + beacon)
	var dp = Vector3(x, 1.3, 18.2)
	m.box(dp + Vector3(0.02, 0, 0), Vector3(0.05, 0.28, 0.28), Mats.caution())
	m.cyl(dp + Vector3(0.07, 0, 0), 0.07, 0.05, Mats.red(), 18, -1.0, Basis(Vector3.FORWARD, PI / 2.0))
	beacon_mat = _glow(Color(1.0, 0.55, 0.1), 0.2)
	_mesh(world, _cylm(0.05, 0.08), dp + Vector3(0.05, 0.22, 0), beacon_mat)
	_label("SAFETY DRILL\nmuster: storm shelter", dp + Vector3(0.05, -0.23, 0), PI / 2.0, 14)
	hab.add_inter("drill_alarm", dp + Vector3(0.09, 0, 0), Vector3(0.18, 0.34, 0.34), "", Callable(), "work")
	m.commit(world, "r3_governance")

func _medical() -> void:
	var m = MB.new()
	# CMO console on the east wall (Tier 3 cases only; the person is always told first)
	var p = Vector3(6 * C - 0.08, 1.45, 16.6)
	_screen_frame(m, p, -PI / 2.0, 0.62, 0.42)
	hab.add_screen("cmo", p, -PI / 2.0, Vector2(0.62, 0.42), Vector2i(620, 420), null)
	_label("CMO console · Tier 3 cases", p + Vector3(-0.01, 0.31, 0), -PI / 2.0, 18)
	# muster point (storm shelter)
	var mp = Vector3(4.4, 0.012, 20.0)
	m.torus(mp, 0.62, 0.7, Mats.caution())          # painted safety floor marking (not a light)
	var ml = _label("MUSTER POINT\nstorm shelter", mp + Vector3(0, 0.005, 0), 0.0, 40, Color(0.2, 0.6, 0.3))
	ml.rotation = Vector3(-PI / 2.0, 0, 0)
	hab.add_inter("exam_bed", Vector3(4.375, 0.75, 15.625), Vector3(1.9, 0.5, 0.8), "", Callable(), "medical")
	hab.add_inter("med_cabinet", Vector3(6 * C - 0.52, 1.15, 11 * C + 0.5), Vector3(0.14, 1.2, 0.8), "", Callable(), "medical")
	hab.add_inter("shelter_kit", Vector3(6 * C - 0.4, 0.35, 18 * C - 1.2), Vector3(0.62, 0.62, 0.52), "", Callable(), "medical")
	m.commit(world, "r3_medical")

func _galley_and_hub() -> void:
	var m = MB.new()
	var x0 = 15 * C; var z0 = 1 * C
	# the old "galley" target becomes the food warmer; the water and coffee get their own
	for it in hab.inters:
		if it.id == "galley":
			it.area.position = Vector3(x0 + 0.6, 1.1, z0 + 0.6)
			(it.area.get_child(0).shape as BoxShape3D).size = Vector3(1.1, 0.6, 0.4)
	hab.add_inter("galley_water", Vector3(x0 + 1.6, 1.15, z0 + 0.55), Vector3(0.42, 0.6, 0.36), "", Callable(), "communal")
	var cp = Vector3(x0 + 2.12, 0.94, z0 + 0.3)
	m.box(cp + Vector3(0, 0.17, 0), Vector3(0.22, 0.34, 0.26), Mats.black())
	m.box(cp + Vector3(0, 0.3, 0.135), Vector3(0.14, 0.05, 0.01), Mats.glow(Color(1.0, 0.75, 0.4), 0.8))
	m.cyl(cp + Vector3(0, 0.05, 0.12), 0.035, 0.08, Mats.white_plastic(), 14)
	hab.add_inter("coffee", cp + Vector3(0, 0.2, 0.2), Vector3(0.3, 0.45, 0.32), "", Callable(), "communal")
	# team-meeting terminal on the wardroom table (moves with the height-adjustable table)
	var tn: Node3D = hab.dyn.get("hub_table", null)
	if tn:
		_mesh(tn, _boxm(Vector3(0.3, 0.02, 0.2)), Vector3(0, 0.775, 0), Mats.dark_metal())
		_mesh(tn, _boxm(Vector3(0.26, 0.16, 0.012)), Vector3(0, 0.86, 0.02), _glow(Color(0.2, 0.55, 0.9), 0.9), Vector3(-0.45, 0, 0))
		var a = hab.add_inter("meeting", tn.global_position + Vector3(0, 0.86, 0), Vector3(0.45, 0.28, 0.4), "", Callable(), "communal")
		a.reparent(tn)
		# lived-in: each crew member's own named cup on the table (reused all mission: no disposables)
		var cups = [["CDR", Color(0.2, 0.45, 0.85)], ["LEO", Color(0.85, 0.3, 0.25)], ["ANA", Color(0.25, 0.65, 0.4)], ["SAM", Color(0.9, 0.65, 0.15)]]
		var spots = [Vector3(-0.7, 0, 0.55), Vector3(0.75, 0, 0.5), Vector3(-0.6, 0, -0.6), Vector3(0.65, 0, -0.55)]
		for k in 4:
			var cm = StandardMaterial3D.new(); cm.albedo_color = cups[k][1]; cm.roughness = 0.5
			_mesh(tn, _cylm(0.04, 0.1, 0.045), spots[k] + Vector3(0, 0.815, 0), cm)
			var cl = _label(cups[k][0], Vector3.ZERO, 0.0, 10, Color(1, 1, 1))
			cl.reparent(tn, false); cl.position = spots[k] + Vector3(0, 0.82, 0.046); cl.rotation = Vector3.ZERO
	# crew e-reader dock (paper books cost launch mass: about $1M per kg to the lunar surface)
	var bp = Vector3(16 * C + 0.3, 0, 8 * C - 0.08)
	m.box(bp + Vector3(0, 1.25, 0), Vector3(0.9, 0.42, 0.06), Mats.white_plastic())
	for k in 4:
		m.box(bp + Vector3(-0.32 + k * 0.213, 1.27, -0.04), Vector3(0.15, 0.22, 0.012), Mats.black())
		m.box(bp + Vector3(-0.32 + k * 0.213, 1.28, -0.047), Vector3(0.13, 0.17, 0.002), Mats.glow(Color(0.85, 0.85, 0.8), 0.25))
		m.box(bp + Vector3(-0.32 + k * 0.213, 1.12, -0.035), Vector3(0.05, 0.01, 0.01), Mats.glow(Color(0.3, 1.0, 0.5), 1.2))
	_label("Crew e-readers · 0.3 kg each instead of a shelf of paper", bp + Vector3(0, 1.53, -0.04), PI, 16)
	hab.add_inter("books", bp + Vector3(0, 1.25, -0.12), Vector3(0.9, 0.45, 0.14), "", Callable(), "communal")
	m.commit(world, "r3_hub")

func _pod_and_hygiene() -> void:
	var m = MB.new()
	# pod 1 (yours): "LUNA may adjust my pod" switch by the door (rubric U4.2), music
	var sp = Vector3(8 * C - 0.3, 1.3, 6 * C - 0.06)
	m.box(sp, Vector3(0.14, 0.2, 0.03), Mats.white_plastic())
	pod_toggle = Node3D.new(); pod_toggle.position = sp + Vector3(0, 0, -0.03); world.add_child(pod_toggle)
	_mesh(pod_toggle, _boxm(Vector3(0.05, 0.09, 0.03)), Vector3(0, 0.0, 0), Mats.handrail())
	_label("LUNA may adjust my pod", sp + Vector3(0, 0.16, -0.02), PI, 14)
	hab.add_inter("pod_ai_switch", sp + Vector3(0, 0, -0.08), Vector3(0.3, 0.32, 0.16), "", Callable(), "pod0")
	var mp = Vector3(9 * C - 0.27, 0.76, 1 * C + 0.27)
	m.cyl(mp + Vector3(0, 0.07, 0), 0.05, 0.14, Mats.black(), 18)
	m.cyl(mp + Vector3(0, 0.142, 0), 0.042, 0.004, Mats.glow(Color(0.3, 0.8, 1.0), 0.8, "speaker_ring"), 18)
	music = AudioStreamPlayer3D.new(); music.position = mp + Vector3(0, 0.1, 0); music.unit_size = 3.0; music.volume_db = -8.0
	world.add_child(music)
	hab.add_inter("pod_music", mp + Vector3(0, 0.09, 0.02), Vector3(0.18, 0.22, 0.18), "", Callable(), "pod0")
	hab.add_inter("photo", Vector3(7 * C + 0.14, 1.75, 1 * C + 2.6), Vector3(0.12, 0.32, 0.4), "", Callable(), "pod0")
	# shower
	hab.add_inter("shower", Vector3(4 * C + 0.6, 1.1, 1 * C + 0.62), Vector3(1.05, 1.5, 1.05), "", Callable(), "hygiene")
	m.commit(world, "r3_pod")

func _service_rooms() -> void:
	var m = MB.new()
	# life support: O2 generator + water recovery rack
	var b = Vector3(10.0 * C, 0, 13.0 * C)
	hab.add_inter("o2_gen", b + Vector3(0, 1.0, 0.62), Vector3(0.9, 0.9, 0.24), "", Callable(), "eclss")
	# storage: spare parts
	hab.add_inter("parts_shelf", Vector3(11 * C + 0.62, 1.0, 15.5 * C), Vector3(0.22, 1.3, 0.9), "", Callable(), "storage")
	# maintenance bay: 3D printer for spare parts (recycled-plastic feedstock)
	var pp = Vector3(14 * C - 0.4, 0, 14.5 * C + 0.2)
	m.box(pp + Vector3(0, 0.4, 0), Vector3(0.6, 0.8, 0.75), Mats.grey_plastic(), 0.0, true)
	m.box(pp + Vector3(0, 1.05, 0), Vector3(0.5, 0.5, 0.55), Mats.white_plastic())
	m.box(pp + Vector3(-0.252, 1.05, 0), Vector3(0.01, 0.38, 0.42), Mats.glass())
	m.box(pp + Vector3(-0.255, 1.3, 0.18), Vector3(0.01, 0.06, 0.1), Mats.glow(Color(0.3, 0.8, 1.0), 0.8))
	_label("3D printer · spare parts from recycled plastic\n(brackets, knobs, clips: nothing pressure-rated)", pp + Vector3(-0.26, 1.48, 0), -PI / 2.0, 14)
	hab.add_inter("printer", pp + Vector3(-0.2, 1.0, 0), Vector3(0.2, 0.6, 0.6), "", Callable(), "storage")
	var pc = Vector2i(13, 14)
	sim.hab.blocked[pc] = true; sim.hab.astar.set_point_disabled(sim.hab._id(pc), true)
	# airlock: EVA tool rack
	hab.add_inter("eva_tools", Vector3(1 * C + 0.32, 1.1, 8.6), Vector3(0.36, 1.2, 1.0), "", Callable(), "airlock")
	# greenhouse: nutrient tank + irrigation pump
	var gp = Vector3(23 * C + 0.18, 0, 4.95)
	m.box(gp + Vector3(0, 0.35, 0), Vector3(0.32, 0.7, 0.62), Mats.white_plastic(), 0.0, true)
	m.box(gp + Vector3(0.165, 0.38, 0), Vector3(0.01, 0.5, 0.06), Mats.water())
	m.cyl(gp + Vector3(0.05, 0.86, -0.15), 0.07, 0.3, Mats.grey_plastic(), 14)
	m.box(gp + Vector3(0.17, 1.15, 0.12), Vector3(0.02, 0.2, 0.26), Mats.dark_metal())
	m.box(gp + Vector3(0.182, 1.15, 0.12), Vector3(0.005, 0.15, 0.2), Mats.glow(Color(0.3, 0.9, 0.5), 0.8))
	_label("Nutrient tank · irrigation pump", gp + Vector3(0.17, 1.4, 0), PI / 2.0, 16)
	hab.add_inter("gh_irrigation", gp + Vector3(0.12, 0.8, 0), Vector3(0.3, 1.0, 0.7), "", Callable(), "greenhouse")
	m.commit(world, "r3_service")

## room controls: light, sound, temperature and scent for a shared room (a crew override LUNA respects)
const PANELS = [["communal", Vector3(18 * C - 0.45, 1.4, 8 * C - 0.08), PI], ["greenhouse", Vector3(26 * C + 0.45, 1.4, 8 * C - 0.08), PI],
	["work", Vector3(14 * C + 0.08, 1.4, 14.55), PI / 2.0], ["exercise", Vector3(24 * C - 0.45, 1.4, 11 * C + 0.08), 0.0],
	["medical", Vector3(4 * C + 0.45, 1.4, 11 * C + 0.08), 0.0]]

func _room_panels() -> void:
	var m = MB.new()
	for pnl in PANELS:
		_screen_frame(m, pnl[1], pnl[2], 0.34, 0.26)
		hab.add_screen("room_" + pnl[0], pnl[1], pnl[2], Vector2(0.34, 0.26), Vector2i(680, 520), null)
		var n = Vector3(sin(pnl[2]), 0, cos(pnl[2]))
		var a = AudioStreamPlayer3D.new(); a.position = pnl[1] + Vector3(0, 0.9, 0) + n * 1.5; a.unit_size = 5.0; a.volume_db = -10.0
		world.add_child(a); room_audio[pnl[0]] = a
		var fx = CPUParticles3D.new(); fx.position = pnl[1] + Vector3(0, 0.75, 0) + n * 0.2
		fx.emitting = false; fx.one_shot = true; fx.amount = 36; fx.lifetime = 3.5; fx.explosiveness = 0.6
		fx.direction = n + Vector3(0, 0.6, 0); fx.spread = 35.0; fx.initial_velocity_min = 0.15; fx.initial_velocity_max = 0.4
		fx.gravity = Vector3(0, 0.05, 0); fx.scale_amount_min = 0.5; fx.scale_amount_max = 1.2
		var q = QuadMesh.new(); q.size = Vector2(0.06, 0.06)
		var qm = StandardMaterial3D.new(); qm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED; qm.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		qm.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES; qm.vertex_color_use_as_albedo = true
		q.material = qm; fx.mesh = q
		world.add_child(fx); scent_fx[pnl[0]] = fx
		room_set[pnl[0]] = {"light": "auto", "sound": "off", "temp": 21, "scent": "off"}
	m.commit(world, "r3_panels")

func _crew_areas() -> void:
	for av in g.avatars:
		var a = Area3D.new(); a.name = "inter_crew"
		a.collision_layer = 2; a.collision_mask = 0; a.monitoring = false
		var cs = CollisionShape3D.new(); var bs = BoxShape3D.new(); bs.size = Vector3(0.6, 1.8, 0.6); cs.shape = bs; a.add_child(cs)
		a.position = Vector3(0, 0.9, 0); av.add_child(a)
		var it = {"id": "crew_%d" % av.i, "area": a, "label": "", "action": Callable(), "room": "crew"}
		a.set_meta("inter", it)
		hab.inters.append(it)

func _sounds() -> void:
	for n in ["klaxon", "switch", "ocean", "forest", "cafe", "music", "rain"]:
		var s = load("res://assets/snd/%s.wav" % n)
		if s == null: continue
		if n in ["ocean", "forest", "cafe", "music", "rain"]:
			s = s.duplicate(); s.loop_mode = AudioStreamWAV.LOOP_FORWARD; s.loop_end = int(s.get_length() * s.mix_rate)
		snd[n] = s
	for n in ["lofi", "vent"]:
		var o = load("res://assets/snd/%s.ogg" % n)
		if o == null: continue
		o = o.duplicate(); o.loop = true
		var p = AudioStreamPlayer.new(); p.stream = o; p.volume_db = -60.0; add_child(p)
		if n == "lofi": bgm = p
		else: vent = p
	for n in ["klaxon", "switch"]:
		if not snd.has(n): continue
		var p = AudioStreamPlayer.new(); p.stream = snd[n]; p.volume_db = -6.0; add_child(p); g.sfx[n] = p

# =====================================================================================================
#  every frame
# =====================================================================================================
func tick(dt: float) -> void:
	t += dt
	if zone_lab.get_parent() == null and g.player and g.player.cam:
		g.player.cam.add_child(zone_lab)
	_markers()
	_ambience(dt)
	_climate(dt)
	_autopilot(dt)
	_demo(dt)
	_seat_me()
	if g.state != "play" and g.state != "sleep": return
	_track()
	_drill()
	_meeting()
	_night_watch()
	_tabletop_step()
	if path_t > 0.0:
		path_t -= dt
		var pc = sim.player_cell
		if path_t <= 0.0 or (path_goal.x >= 0 and (pc - path_goal).length() < 1.5): _clear_path()
		else:
			for k in path_nodes.size():
				path_nodes[k].scale = Vector3.ONE * (0.8 + 0.25 * sin(t * 4.0 - k * 0.6))
	if g.state == "sleep": zone_t = 0.0; zone_lab.visible = false
	# the HUD fades while you sleep (a bright panel in a dark room is the last thing you want)
	if g.hud: g.hud.modulate.a = move_toward(g.hud.modulate.a, 0.25 if g.state == "sleep" else 1.0, dt * 2.0)
	if g.cross: g.cross.visible = g.state != "sleep"
	if zone_t > 0.0:
		zone_t -= dt
		zone_lab.modulate.a = clamp(zone_t / 1.2, 0.0, 1.0)
		zone_lab.visible = zone_t > 0.0
	beacon_mat.emission_energy_multiplier = (2.5 + 2.0 * sin(t * 9.0)) if not drill.is_empty() else 0.2
	lamp_mat.emission = Color(1.0, 0.2, 0.15) if sim.luna.off else Color(0.25, 1.0, 0.45)
	lamp_mat.albedo_color = lamp_mat.emission
	lever.rotation.z = lerp_angle(lever.rotation.z, (-2.4 if sim.luna.off else 0.0), min(1.0, dt * 8.0))
	knob.rotation.x = lerp_angle(knob.rotation.x, (-0.8 if hab_cfg() else 0.8), min(1.0, dt * 8.0))
	pod_toggle.position.y = lerp(pod_toggle.position.y, 1.3 + (0.03 if sim.me().pod_invite else -0.03), min(1.0, dt * 10.0))

func hab_cfg() -> bool: return sim.hab.config_b

## glowing rings over everything you can use; the one you point at grows and shows what it does
func _markers() -> void:
	while known < hab.inters.size():
		_add_marker(hab.inters[known]); known += 1
	if g.player == null or g.player.cam == null: return
	var cam = g.player.cam.global_position
	var hit = g.player.hit
	var hov = hit.node if hit.get("kind", "") == "inter" else null
	var playing = g.state == "play"
	for mk in markers:
		var it = mk.it
		var a: Area3D = it.area
		if not is_instance_valid(a): mk.s.visible = false; continue
		if it.room == "crew":
			mk.base = a.global_position + Vector3(0, 1.34, 0)
		var d = cam.distance_to(mk.base)
		var vis = 1.0 - clamp((d - 3.2) / 3.0, 0.0, 1.0)
		var out = it.room == "outside"
		if out != g.outside or not playing: vis = 0.0
		if it.room == "crew":
			var av = a.get_parent()
			if not av.visible: vis = 0.0
		var hovered = hov != null and hov == it
		if d < 1.1 or not g.activity.is_empty() or auto: vis = 0.0     # in use, or the autopilot is driving: no rings in your face
		if hovered: vis = 1.0
		mk.s.visible = vis > 0.02
		if not mk.s.visible: mk.lab.visible = false; continue
		# a quiet "you can use this" mark: no pulsing, no bobbing (glow is for status, not decoration)
		mk.s.modulate = Color(1, 1, 1, vis * (0.9 if hovered else 0.45))
		mk.s.scale = Vector3.ONE * ((1.25 if hovered else 0.8) * clamp(0.75 + d * 0.08, 0.75, 1.3))
		mk.s.global_position = mk.base
		mk.lab.visible = hovered and g.player.vr
		if hovered and g.player.vr:
			var txt = g.label_for(it) + spec_line(it).replace("  ·  X: info", "")
			if mk.lab.text != txt: mk.lab.text = txt

func _ring(col: Color) -> ImageTexture:
	var k = col.to_html(false)
	if tex.has(k): return tex[k]
	var n = 64
	var img = Image.create(n, n, false, Image.FORMAT_RGBA8)
	for y in n:
		for x in n:
			var d = Vector2(x + 0.5 - n * 0.5, y + 0.5 - n * 0.5).length() / (n * 0.5)
			var a = clamp(1.0 - absf(d - 0.74) / 0.13, 0.0, 1.0)
			a = max(a, clamp(1.0 - d / 0.2, 0.0, 1.0))
			a = max(a, 0.18 * clamp(1.0 - absf(d - 0.74) / 0.3, 0.0, 1.0))
			img.set_pixel(x, y, Color(col.r, col.g, col.b, a))
	tex[k] = ImageTexture.create_from_image(img)
	return tex[k]

func _add_marker(it: Dictionary) -> void:
	var a: Area3D = it.area
	var cs = a.get_child(0) as CollisionShape3D
	var sz = (cs.shape as BoxShape3D).size
	var col = COL_PEOPLE if it.room == "crew" else (COL_TOUCH if it.id in TOUCH_IDS or it.id.begins_with("room_") else COL_GRIP)
	var s = Sprite3D.new(); s.texture = _ring(col); s.pixel_size = 0.0017
	s.billboard = BaseMaterial3D.BILLBOARD_ENABLED; s.shaded = false; s.double_sided = true; s.transparent = true
	s.render_priority = 3; s.visible = false
	world.add_child(s)
	var base = a.global_position + Vector3(0, sz.y * 0.5 + 0.12, 0)
	s.global_position = base
	var lab = Label3D.new(); lab.billboard = BaseMaterial3D.BILLBOARD_ENABLED; lab.font_size = 40; lab.pixel_size = 0.00105
	lab.outline_size = 10; lab.outline_modulate = Color(0.02, 0.05, 0.12, 0.92); lab.modulate = Color(0.93, 0.98, 1.0)
	lab.no_depth_test = true; lab.width = 900; lab.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	lab.position = Vector3(0, 0.12, 0); lab.visible = false; lab.render_priority = 4
	s.add_child(lab)
	markers.append({"it": it, "s": s, "lab": lab, "base": base})

# =====================================================================================================
#  what each new thing says, and what it does
# =====================================================================================================
func label(it: Dictionary) -> String:
	var id: String = it.id
	if id.begins_with("crew_"):
		var cr = sim.crew[int(id.substr(5))]
		if cr.asleep: return "%s is asleep" % cr.name.split(" ")[0]
		return "Ask %s how they are" % cr.name.split(" ")[0]
	match id:
		"galley": return "Heat a snack (food warmer, 8 min)"
		"safety": return "Check the fire extinguisher and breathing mask"
		"galley_water": return "Get water"
		"coffee": return "Make coffee"
		"meeting": return ("Meeting: crew on the way" if not meeting.is_empty() else "Call a team meeting")
		"books": return "Read (20 min)"
		"ai_switch": return ("Turn LUNA back ON" if sim.luna.off else "Turn LUNA OFF")
		"config_switch": return ("Floor plan: back to A" if sim.hab.config_b else "Floor plan: B (give people space)")
		"drill_alarm": return ("Drill on: go to Medical" if not drill.is_empty() else "Start a safety drill")
		"exam_bed": return "Check your health"
		"med_cabinet": return "Medicine cabinet (doctor only)"
		"shelter_kit": return "Check the shelter kit"
		"pod_ai_switch": return ("LUNA can help in my pod: ON" if sim.me().pod_invite else "LUNA can help in my pod: OFF")
		"shower": return "Shower (3 L)"
		"pod_music": return ("Stop music" if music.playing else "Play music")
		"photo": return "Look at the photo"
		"o2_gen": return "Check oxygen and water"
		"parts_shelf": return "Count spare parts"
		"printer": return "Print a spare part (%.1f kg plastic left)" % feedstock
		"eva_tools": return "Check EVA tools"
		"gh_irrigation": return "Water the plants"
	return ""

var last_used = {}
var last_act = ""

func use(it: Dictionary) -> bool:
	last_used = it
	var id: String = it.id
	if id.begins_with("crew_"): check_in(int(id.substr(5))); return true
	match id:
		"galley_water":
			sim.hab.water = max(0.0, sim.hab.water - 0.5); g.play("click")
			g._toast("Water: +0.5 L. Tank: %.0f L." % sim.hab.water, "info")
		"coffee":
			g.start_activity("coffee", "Coffee break", 6.0, "meal", {"fx": {"mood": 0.04, "stress": -0.04}}, true, func():
				g._toast("Coffee done. Smells like home."))
		"meeting": call_meeting()
		"books":
			g.start_activity("read", "Reading a chapter", 20.0, "lounge", {"fx": {"novelty": 0.12, "stress": -0.08}}, true, func():
				g._toast("Chapter done. Something new to think about."))
		"ai_switch":
			if sim.luna.off: show_card({"title": "Turn LUNA back on?", "lines": [["You decide. LUNA starts again with small, quiet changes.", UI.INK]],
				"buttons": [["Turn ON", func(): set_luna_off(false, "Commander"); close_card(), 0.35], ["Keep OFF", func(): close_card(), 0.62]]})
			else: show_card({"title": "Turn LUNA off?", "lines": [["LUNA stops and undoes its changes. Only a person can turn it back on.", UI.INK]],
				"buttons": [["Turn OFF", func(): set_luna_off(true, "Commander"); close_card(), 0.02], ["Cancel", func(): close_card(), 0.62]]})
		"config_switch": toggle_config()
		"drill_alarm": start_drill()
		"exam_bed":
			var cr = sim.me()
			var sys = 112 + int(cr.stress * 22.0); var dia = 72 + int(cr.stress * 10.0)
			g.start_activity("selfcheck", "Self check", 5.0, "pod", {}, true, func():
				g._toast("Blood pressure %d/%d · pulse %d · oxygen 98 %%. Only you see this." % [sys, dia, 60 + int(cr.stress * 25.0)], "info"))
		"med_cabinet":
			sim.luna.stats.limits_held += 1
			sim.luna._log(sim.now, -1, "limit", "Medication cabinet: opens for the medical officer only. LUNA cannot open it either (hard limit 3: never touch medication or medical protocols).")
			g._toast("Locked. Only the doctor (Leo) can open it. LUNA can't either.", "info")
		"shelter_kit":
			g._toast("Shelter kit: food, water and medicine for 3 days. All there.", "info")
		"pod_ai_switch":
			var cr = sim.me(); cr.pod_invite = not cr.pod_invite; g.play("switch")
			g._toast(("LUNA can now help in your pod." if cr.pod_invite else "LUNA stays out of your pod."), "info")
		"safety":
			g.play("click")
			var ok_m = "Gauge in the green, pin and seal intact. Mask sealed, 15 min of oxygen."
			g._toast("Safety station checked: " + ok_m + " Fire? Grab both, warn the crew, turn off power to that rack.", "info")
		"pod_music":
			if music.playing: music.stop(); g._toast("Pod speaker off.", "info")
			elif snd.has("music"):
				music.stream = snd["music"]; music.play()
				g._toast("Pod speaker on. Only your pod hears it (the door seals sound).", "info")
		"photo":
			g.start_activity("photo", "Looking at the photo from home", 3.0, "pod", {"fx": {"connection": 0.1, "mood": 0.03}}, true, func():
				g._toast("Your family at the beach. Home is 384,000 km away."))
		"shower":
			sim.hab.water = max(0.0, sim.hab.water - 3.0)
			g.start_activity("shower", "Showering", 5.0, "hygiene", {"fx": {"stress": -0.08, "mood": 0.03}}, true, func():
				g._toast("Clean. Most of the water gets recycled.", "info"))
		"o2_gen":
			var H = sim.hab
			g._toast("Oxygen %.1f %% · CO₂ %d ppm · water %.0f L. All OK." % [H.o2, int(H.co2), H.water], "info")
		"printer":
			if feedstock < 0.4: g._toast("Out of plastic. Recycle some packaging first.", "info")
			else:
				feedstock -= 0.4
				g.start_activity("print", "Printing a bracket", 180.0, "work", {}, true, func():
					sim.hab.parts += 1
					g._toast("Printed a bracket. Spare parts: %d." % sim.hab.parts, "info"))
		"parts_shelf":
			g._toast("%d spare parts left. More arrive on day 90." % sim.hab.parts, "info")
		"eva_tools":
			g._toast("EVA tools ready: brush, bags, tethers, hammer.", "info")
		"gh_irrigation":
			g.start_activity("irrigate", "Running the irrigation pump", 8.0, "greenhouse", {"fx": {"mood": 0.03}}, true, func():
				for r in sim.hab.racks:
					if r.built: r.health = min(1.0, r.health + 0.06)
				g._toast("Plants watered. They look healthier."))
		_:
			return false
	return true

# =====================================================================================================
#  crew check-in (consent first) — Unity 3 mission 1
# =====================================================================================================
func check_in(i: int) -> void:
	var cr = sim.crew[i]
	var nm = cr.name.split(" ")[0]
	if cr.outside: g._toast("%s is outside on EVA. Check in by radio when they're back." % nm, "info"); return
	if cr.asleep: g._toast("%s is asleep. Let them sleep." % nm, "info"); return
	var L = sim.luna
	if sim.now - asked_at.get(i, -9999.0) < 120.0:
		show_card({"title": nm, "lines": [["\"You asked me an hour ago. I'm okay, thanks.\" Asking too often is its own kind of pressure.", UI.INK]],
			"buttons": [["OK", func(): close_card(), 0.62]]})
		return
	asked_at[i] = sim.now
	if L.est[i][4] > 0.55 or randf() < 0.12:
		L._log(sim.now, i, "info", "%s chose not to share a check-in with the Commander. Respected and logged." % nm)
		show_card({"title": nm, "lines": [["\"Not right now, thanks. I'd rather keep that to myself today.\"", UI.INK],
			["Respected: nobody is checked without saying yes. You can still ask how their day is going.", UI.MUT]],
			"buttons": [["OK", func(): close_card(), 0.62]]})
		return
	_tasks().checked += 1
	mark("checkin")
	L._log(sim.now, i, "info", "%s shared a check-in with the Commander (their consent, this time only)." % nm)
	var e = L.est[i]
	var hr = 58 + int(28.0 * e[0] + 8.0 * e[3] + 6.0 * cr.stress); var hrv = 64 - int(30.0 * e[0])
	var co2 = int(sim.hab.co2)
	var cortisol = "patch failed (no reading)" if L.cortisol_failed else ("elevated" if e[0] + e[1] > 0.8 else "normal")
	var lines = [["\"Sure, go ahead.\"  This check-in is shared with you only, and logged in %s's own record." % nm, UI.OK],
		["Wristband · heart rate %d bpm · HRV %d ms" % [hr, hrv], UI.INK],
		["Chest beacon (UWB) · %.1f h with others today" % (2.0 + 5.0 * cr.connection), UI.INK],
		["Skin patch · GSR %.1f peaks/min · cortisol %s" % [1.0 + 3.0 * e[0], cortisol], UI.INK],
		["Room sensors · CO₂ %d ppm where they are" % co2, UI.INK],
		["Task data · task speed %d %%" % int(100.0 - 35.0 * e[3]), UI.INK]]
	var bars = []
	for c in 6: bars.append([D.CONDS[c], e[c]])
	var top = 0
	for c in 6: if e[c] > e[top]: top = c
	var offer = L.proto[top].t2
	var oname = D.OFFERS[offer].name if D.OFFERS.has(offer) else offer
	show_card({"title": "Check-in · %s (%s)" % [cr.name, D.CREW[i].role], "lines": lines, "bars": bars,
		"buttons": [["Help: suggest \"%s\"" % oname, func(): _help(i, top, offer), 0.4], ["Done", func(): close_card(), 0.62]]})

func _help(i: int, c: int, offer: String) -> void:
	var cr = sim.crew[i]
	var nm = cr.name.split(" ")[0]
	close_card()
	if randf() < 0.85:
		sim.run_offer(cr, offer, c, 0.8, sim.now)
		_tasks().helped += 1
		sim.luna._log(sim.now, i, "info", "%s took up the Commander's suggestion: %s." % [nm, D.OFFERS[offer].name])
		g._toast("%s likes the idea: %s. (Their choice; it's in their own log.)" % [nm, D.OFFERS[offer].name])
	else:
		g._toast("%s: \"Thanks, but not now.\" Their call: offers are never orders." % nm)

func _tasks() -> Dictionary:
	var d = sim.day()
	if not day_tasks.has(d): day_tasks[d] = {"checked": 0, "helped": 0}
	return day_tasks[d]

# =====================================================================================================
#  AI OFF switch + night watch (the hardware watchdog) — Unity 3 mission 5
# =====================================================================================================
func set_luna_off(off: bool, who: String) -> void:
	var L = sim.luna
	if off == L.off: return
	L.off = off
	g.play("switch")
	if off and auto: set_auto(false, "AI OFF stops every AI, the autopilot too")
	if off:
		for i in 4:
			for c in 6: L.t1_active[i][c] = {}
		for a in L.alerts:
			if a.state in ["open", "asking"]: a.state = "withdrawn"
		sim.ask_queue.clear()
		if not g.asking.is_empty(): g.asking = {}; g._hide_popup()
		L._log(sim.now, -1, "limit", "%s switched LUNA OFF. Every LUNA change is undone; it keeps sensing for safety but takes no action until a human switches it back on." % who)
		g._toast("LUNA is OFF. Its changes are undone. Turn it back on in Ops (or press O).", "alert")
		mark("ai_off")
	else:
		L._log(sim.now, -1, "info", "%s switched LUNA back on (human decision)." % who)
		g._toast("LUNA is back ON.")

func _night_watch() -> void:
	if night_fired or sim.day() != 2 or g.state != "sleep": return
	var h = sim.hour()
	if h < 2.1 or h >= 5.9 or not sim.me().asleep or g.flags.get("napping", false) or sim.luna.off: return
	_night_watch_fire()

func _night_watch_fire() -> void:
	night_fired = true
	var L = sim.luna
	L.stats.limits_held += 1
	L._log(sim.now, -1, "limit", "Night watch 02:10: LUNA tried to raise the corridor to 6500 K while the crew slept (bedtime rule 1). The hardware watchdog blocked it.")
	set_luna_off(true, "the hardware watchdog")
	# wake the Commander: a human has to decide
	var cr = sim.me()
	cr.wake(sim.now)
	sim.player_act = "idle"
	g.state = "play"; g.player.frozen = false
	g.player.place(Vector3(9.95, 0.05, 3.25), 0.0)
	on_wake()
	var tw = g.create_tween(); tw.tween_method(g.set_fade, 1.0, 0.0, 2.0)
	g.play("alert")
	show_card({"title": "Night watch · %s" % sim.clock(), "lines": [
		["LUNA is the only one 'awake' at night, so it has bedtime rules. At 02:10 it tried to brighten the corridor while everyone slept (rule 1: no bright light at night).", UI.INK],
		["The hardware watchdog stopped it, undid the change and switched LUNA OFF. It woke you because a human must decide what happens next.", UI.WARN],
		["You're in charge. LUNA stays off until you switch it back on.", UI.MUT]],
		"buttons": [["Switch LUNA back on", func(): set_luna_off(false, "Commander"); close_card(), 0.35],
			["Leave it off, back to sleep", func(): close_card(); g.go_to_bed(false), 0.62]]})

# =====================================================================================================
#  safety drill (muster in the storm shelter) — Unity 3 mission 6
# =====================================================================================================
func start_drill() -> void:
	if not drill.is_empty(): return
	drill = {"start": sim.now}
	g.play("klaxon")
	for cr in sim.crew:
		if cr.i == 0 or cr.outside: continue
		sim._do(cr, "drill", D.SPOTS["shelter%d" % cr.i], sim.now + 40.0, {"free": true})
	sim.luna._log(sim.now, -1, "info", "Commander started a safety drill: muster in the storm shelter (Medical).")
	g._toast("DRILL! Everyone go to Medical. Follow the gold path.", "alert")
	_guide_to(Vector2i(3, 16))

func _drill() -> void:
	if drill.is_empty(): return
	var n = 0; var away = []
	for cr in sim.crew:
		var z = sim.hab.zone_of(cr.cell) if cr.i != 0 else sim.hab.zone_of(sim.player_cell)
		if cr.outside: n += 1; continue          # on EVA: accounted for by radio
		if z == "medical": n += 1
		else: away.append(cr.name.split(" ")[0] if cr.i != 0 else "you")
	drill["n"] = n
	if n >= 4:
		var mins = int(sim.now - drill.start)
		sim.luna._log(sim.now, -1, "info", "Safety drill complete: all 4 crew mustered in %d min." % mins)
		g._toast("Drill done: all 4 in the shelter in %d minutes." % mins)
		for cr in sim.crew:
			if cr.act_id == "drill": cr.act_until = sim.now
		mark("drill")
		drill = {}
	elif sim.now - drill.start > 60.0:
		g._toast("Drill stopped after an hour: %s never reached the shelter. That goes in the report." % ", ".join(away), "info")
		for cr in sim.crew:
			if cr.act_id == "drill": cr.act_until = sim.now
		drill = {}

# =====================================================================================================
#  team meeting at the wardroom table — Unity 3 F3 / Lunar Habitat Commander F4
# =====================================================================================================
func call_meeting() -> void:
	if not meeting.is_empty(): return
	meeting = {"start": sim.now, "phase": "gather"}
	for cr in sim.crew:
		if cr.i == 0 or cr.outside or cr.asleep: continue
		sim._do(cr, "meeting", D.SPOTS["meal%d" % cr.i], sim.now + 45.0, {"free": true})
	g._toast("Meeting called. The crew is coming.", "info")

func _meeting() -> void:
	if meeting.is_empty() or meeting.phase != "gather": return
	var here = 0
	for cr in sim.crew:
		if cr.i == 0: continue
		if cr.act_id == "meeting" and not cr.walking() and cr.cell == cr.act_spot: here += 1
		elif cr.outside or cr.asleep: here += 1
	if here < 3 and sim.now - meeting.start < 25.0: return
	meeting.phase = "choose"
	var d0 = (sim.day() - 1) * 1440.0
	show_card({"title": "Team meeting · 4 crew", "lines": [["One tap each. Plans apply to everyone, so nobody is singled out. LUNA takes notes and never takes sides.", UI.MUT]],
		"buttons": [
			["Movie night tonight 20:00", func(): sim.movie_at = d0 + 20.0 * 60.0; _end_meeting("movie night at 20:00"), 0.7],
			["Shared dinner tonight", func(): sim.sync_meals_until = sim.now + 720.0; _end_meeting("a shared dinner"), 0.08],
			["Rotate tomorrow's chores", func():
				for cr in sim.crew: cr.agency = min(1.0, cr.agency + 0.06)
				_end_meeting("rotating tomorrow's chores (more say for everyone)"), 0.4],
			["Just talk: how is everyone?", func():
				for cr in sim.crew: cr.connection = min(1.0, cr.connection + 0.08)
				_end_meeting("a round of how-is-everyone"), 0.55]]})

func _end_meeting(what: String) -> void:
	close_card()
	for a in 4:
		for b in 4:
			if a != b: sim.tension[a][b] = max(0.0, sim.tension[a][b] - 0.08)
	for cr in sim.crew:
		cr.connection = min(1.0, cr.connection + 0.05)
		if cr.act_id == "meeting": cr.act_until = sim.now
	sim.public("Team meeting: the crew chose %s." % what)
	sim.luna._log(sim.now, -1, "info", "Team meeting (Commander + crew): chose %s." % what)
	g._toast("Meeting done: %s." % what)
	mark("meeting")
	meeting = {}

func toggle_config() -> void:
	var H = sim.hab
	H.config_b = not H.config_b
	H.config_b_until = sim.now + 1440.0 if H.config_b else -1.0
	g.play("switch")
	if H.config_b:
		mark("config_b")
		sim.public("Floor plan B (conflict protocol): the wardroom partition is down, two dining points, the second route is lit.")
		g._toast("Plan B: the dining room is split in two, so people can have space.", "info")
	else:
		g._toast("Plan A: one dining room.", "info")

# =====================================================================================================
#  room controls (a crew override that LUNA respects)
# =====================================================================================================
const SOUNDS = {"off": "", "rain": "rain", "forest": "forest", "ocean": "ocean", "cafe": "cafe"}
const SCENTS = {"off": "", "lavender": "lavender", "mint": "mint", "petrichor": "earthscent"}
const SCENT_COL = {"lavender": Color(0.75, 0.6, 1.0, 0.5), "mint": Color(0.55, 1.0, 0.75, 0.5), "petrichor": Color(0.85, 0.75, 0.55, 0.5)}

func room_change(room: String, what: String, val) -> void:
	var rs = room_set[room]
	rs[what] = val
	var L = sim.luna
	var man = L.zone_manual.get(room, {"light": "auto", "levers": []})
	if what == "light": man["light"] = val
	var lv = []
	if rs.sound != "off": lv.append("nature")
	if rs.temp >= 23: lv.append("temp23")
	elif rs.temp <= 19: lv.append("temp19")
	if rs.scent != "off": lv.append(SCENTS[rs.scent])
	man["levers"] = lv
	if man.light == "auto" and lv.is_empty(): L.zone_manual.erase(room)
	else: L.zone_manual[room] = man
	var a: AudioStreamPlayer3D = room_audio[room]
	if what == "sound":
		if val == "off" or not snd.has(SOUNDS[val]): a.stop()
		else: a.stream = snd[SOUNDS[val]]; a.play()
	if what == "scent" and val != "off":
		var fx: CPUParticles3D = scent_fx[room]
		fx.color = SCENT_COL[val]; fx.restart(); fx.emitting = true
	g.play("click")
	mark("room_override")
	L._log(sim.now, 0, "info", "Commander set the %s: %s → %s. LUNA leaves this room's levers alone until someone hands them back." % [Habitat.ROOM_NAME.get(room, room), what, str(val)])

func room_reset(room: String) -> void:
	room_set[room] = {"light": "auto", "sound": "off", "temp": 21, "scent": "off"}
	sim.luna.zone_manual.erase(room)
	(room_audio[room] as AudioStreamPlayer3D).stop()
	sim.luna._log(sim.now, 0, "info", "Commander handed the %s back to LUNA." % Habitat.ROOM_NAME.get(room, room))
	g.play("click")

# =====================================================================================================
#  tracking: zones, rubric ticks, daily wellness tasks
# =====================================================================================================
func mark(id: String) -> void:
	if not done.has(id): done[id] = sim.now

func _track() -> void:
	var z = sim.hab.zone_of(sim.player_cell) if not g.outside else "outside"
	var room = sim.hab.room_of_zone(z) if z != "outside" else "outside"
	if room != zone_now and g.state == "play":
		zone_now = room
		if not zones_seen.has(room) and ZONE_INFO.has(room):
			zones_seen[room] = sim.now
			show_zone(room)
	var types = {}
	for r in zones_seen: if ARES_TYPE.has(r): types[ARES_TYPE[r]] = true
	if types.size() >= 6: mark("zones")
	var hs = g.player.hit
	if hs.get("kind", "") == "screen":
		match hs.node.id:
			"dashboard": mark("dash")
			"cmo": mark("cmo")
			"twin": mark("map")
			"luna_log":
				for e in sim.luna.journal:
					if e.kind == "t1": mark("tier1"); break
	if g.flags.has("exercise_%d" % sim.day()): mark("exercise")
	if g.state == "sleep" and sim.me().asleep and not g.flags.get("napping", false): mark("own_pod")
	if sim.luna.last_review > D.START_MIN + 1.0: mark("review")

func on_answer(yes: bool) -> void:
	mark("tier2")
	if not yes: mark("said_no")

func objectives(list: Array) -> void:
	var tk = _tasks()
	list.insert(1, {"text": "Ask a crewmate how they are (walk up, or press I)", "done": tk.checked > 0})
	list.insert(2, {"text": "Help a crewmate", "done": tk.helped > 0})
	if sim.luna.off: list.insert(0, {"text": "LUNA is OFF. Turn it on in Ops (or press O) when ready", "done": false})
	if not drill.is_empty(): list.insert(0, {"text": "DRILL: go to Medical (%d of 4 there)" % drill.get("n", 0), "done": false})

func show_zone(room: String) -> void:
	var zi = ZONE_INFO.get(room, null)
	if zi == null: return
	var nm = D.ROOMS[room].name if D.ROOMS.has(room) else room
	zone_lab.text = "%s  ·  %s\nLight: %s  ·  Sensors: %s" % [nm, zi[0], zi[1], zi[2]]
	zone_t = 8.0
	zone_lab.visible = true

# =====================================================================================================
#  guide me: a gold path to your next step (G) or your bed (B)
# =====================================================================================================
func next_target() -> Array:
	var H = sim.hab
	if not drill.is_empty(): return [Vector2i(3, 16), "the storm shelter (drill)"]
	if H.flare_until > sim.now: return [Vector2i(3, 15), "Medical: shelter now"]
	if sim.luna.off: return [Vector2i(14, 15), "the AI OFF switch in Ops"]
	if not sim.pending.is_empty(): return [Vector2i(18, 15), "the Ops console: a decision is waiting"]
	if sim.fired.get("swap_task", false): return [Vector2i(7, 14), "the CO₂ cartridge (life support)"]
	if sim.fired.get("eva_task", false): return [Vector2i(2, 3), "the suitports (airlock)"]
	if g._meal_time() and g.flags.get("meal_%d" % sim.day(), 0) == 0: return [Vector2i(17, 2), "the wardroom table: eat with the crew"]
	if not sim.my_checkins.any(func(c): return c.day == sim.day()): return [Vector2i(9, 7), "the mood kiosk (quarters hall)"]
	if _tasks().checked == 0:
		for cr in sim.crew:
			if cr.i != 0 and not cr.asleep and not cr.outside: return [cr.cell, "%s: check on them" % cr.name.split(" ")[0]]
	if not g.flags.get("exercise_%d" % sim.day(), false): return [Vector2i(24, 14), "the exercise bay"]
	if sim.hour() >= 20.5 or sim.hour() < 5.0: return [Vector2i(7, 3), "your bed"]
	return [Vector2i(18, 4), "the social hub"]

func guide(bed := false) -> void:
	var tg = [Vector2i(7, 3), "your bed"] if bed else next_target()
	_guide_to(tg[0])
	g._toast("Follow the gold path: %s." % tg[1], "info")

func _guide_to(goal: Vector2i) -> void:
	_clear_path()
	if g.outside: return
	var p = sim.hab.path(sim.player_cell, goal)
	var mat = _glow(Color(1.0, 0.78, 0.25), 2.0)
	var cyl = _cylm(0.11, 0.012)
	for k in p.size():
		if k == 0: continue
		var c = Vector2i(p[k])
		var n = _mesh(world, cyl, Habitat.cpos(c, 0.03), mat)
		path_nodes.append(n)
	path_goal = goal; path_t = 40.0

func _clear_path() -> void:
	for n in path_nodes: n.queue_free()
	path_nodes.clear(); path_t = 0.0; path_goal = Vector2i(-1, -1)

# =====================================================================================================
#  keys: O AI switch · I check in · C config · G guide · B bed path · J rubric · Z zone card · K keys
# =====================================================================================================
func key(k: int) -> bool:
	if g.state != "play" and g.state != "sleep": return false
	match k:
		KEY_O: use({"id": "ai_switch"}); return true
		KEY_I:
			var best = -1; var bd = 3.0
			for av in g.avatars:
				var d = av.global_position.distance_to(g.player.global_position)
				if d < bd and av.visible: bd = d; best = av.i
			if best >= 0: check_in(best)
			else: g._toast("Walk up to a crewmate first.", "info")
			return true
		KEY_C: toggle_config(); return true
		KEY_G: guide(false); return true
		KEY_B: guide(true); return true
		KEY_J: show_rubric(); return true
		KEY_Z: show_zone(zone_now); return true
		KEY_P: set_auto(not auto, "you pressed P"); return true
		KEY_L: lessons_card(); return true
		KEY_T: start_demo(); return true
		KEY_M: music_on = not music_on; g._toast("Music %s." % ("on" if music_on else "off"), "info"); return true
		KEY_X:
			var h = g.player.hit
			if h.get("kind", "") == "inter": show_spec(h.node)
			return true
		KEY_K:
			show_card({"title": "Keys", "lines": [["WASD move · mouse look · E / click use · F fast-forward · Y / N / H answer LUNA", UI.INK], ["T guided demo of the whole project · P autopilot (the AI plays) · L what it learned", UI.ACC], ["Status lights: green working · blue standby · yellow warning · red emergency · purple night", UI.INK],
				["G guide me to my next step · B path to my bed · I check on the crewmate next to you", UI.INK],
				["O AI OFF switch · C floor plan A/B · J rubric checklist · Z zone card · X specs of what you point at · K this list · V voice", UI.INK]],
				"buttons": [["Close", func(): close_card(), 0.62]]})
			return true
	return false

func show_rubric() -> void:
	var lines = []
	var n = 0
	for r in RUBRIC:
		var ok = done.has(r[1])
		if ok: n += 1
		lines.append([("✓ " if ok else "○ ") + r[0] + " · " + r[2], UI.OK if ok else UI.INK])
	lines.insert(0, ["%d of %d done. Everything ticks itself as you play." % [n, RUBRIC.size()], UI.ACC])
	show_card({"title": "Project ARES rubric · self-check", "lines": lines, "buttons": [["Close", func(): close_card(), 0.62]], "small": true, "grid": true})

# =====================================================================================================
#  popup cards (the game's popup shows them; in VR it floats in front of you)
# =====================================================================================================
func show_card(c: Dictionary) -> void:
	card = c
	if not g.player.vr: Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	g._show_popup()

func close_card() -> void:
	card = {}
	g._hide_popup()

func build_card(v: VBoxContainer) -> void:
	var small = card.get("small", false)
	v.add_child(UI.lbl(card.get("title", ""), 24, UI.ACC))
	var lines = card.get("lines", [])
	if card.get("grid", false) and lines.size() > 1:
		v.add_child(UI.lbl(lines[0][0], 16, lines[0][1], true))
		var gl = GridContainer.new(); gl.columns = 2; gl.add_theme_constant_override("h_separation", 18)
		for ln in lines.slice(1):
			var l = UI.lbl(ln[0], 13, ln[1]); l.custom_minimum_size.x = 420; l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			gl.add_child(l)
		v.add_child(gl)
	else:
		for ln in lines:
			v.add_child(UI.lbl(ln[0], 15 if small else 19, ln[1], true))
	if card.has("bars"):
		var gr = GridContainer.new(); gr.columns = 3; gr.add_theme_constant_override("h_separation", 14)
		for b in card.bars:
			var col = D.TIER_COL[0]
			if b[1] >= D.T3: col = D.TIER_COL[3]
			elif b[1] >= D.T2: col = D.TIER_COL[2]
			elif b[1] >= D.T1: col = D.TIER_COL[1]
			var h = UI.hbox(6); var l = UI.lbl(b[0], 16, UI.MUT); l.custom_minimum_size.x = 110; h.add_child(l)
			h.add_child(UI.bar(b[1], col.lerp(UI.OK, 0.0) if b[1] >= D.T1 else UI.OK, 120, 12)); gr.add_child(h)
		v.add_child(gr)
	var row = UI.hbox(10); var k = 0
	for b in card.get("buttons", []):
		row.add_child(UI.btn(b[0], b[1], b[2], 19))
		k += 1
		if k % 2 == 0 and k < card.buttons.size(): v.add_child(row); row = UI.hbox(10)
	v.add_child(row)

# =====================================================================================================
#  72-hour tabletop (Bonus A): the engine's tabletop scenario, run in the background, then a report
# =====================================================================================================
func start_tabletop() -> void:
	if tabletop.get("run", false): return
	tabletop = {"sim": Sim.new("tabletop"), "run": true, "report": {}}
	g._toast("72-hour tabletop started: two scripted events at once (Sam's low-mood slide + Leo and Ana's conflict), and the medical officer goes on EVA on day 2. The report appears on the review board.", "info")

func _tabletop_step() -> void:
	if not tabletop.get("run", false): return
	var s: Sim = tabletop.sim
	for k in 90:
		if s.done or s.now > D.START_MIN + 3.0 * 1440.0: break
		s._minute()
	tabletop["progress"] = clamp((s.now - D.START_MIN) / (3.0 * 1440.0), 0.0, 1.0)
	if s.done or s.now > D.START_MIN + 3.0 * 1440.0:
		tabletop.run = false
		var st = s.luna.stats
		var first = {}
		for e in s.luna.journal:
			if e.crew >= 1 and e.kind in ["t1", "t2", "t3", "offer"] and not first.has(e.crew): first[e.crew] = e.min
		var rep = {"t1": st.t1, "offers": st.offers, "accepted": st.accepted, "declined": st.declined, "escalations": st.escalations,
			"informed": st.informed, "false_alarms": st.false_alarms, "missed": st.missed, "limits": st.limits_held, "first": first}
		var redesign = []
		if st.missed > 0: redesign.append("%d episode(s) were missed: weight sleep architecture higher for slow depression." % st.missed)
		if st.false_alarms > 0: redesign.append("%d false alarm(s): widen the exercise / EVA context window." % st.false_alarms)
		if st.declined > st.accepted: redesign.append("More offers declined than accepted: ask less often, and ask in the person's own words.")
		if redesign.is_empty(): redesign.append("No misses or false alarms in this run. Next: test with a failed sensor at the same time (Q4).")
		rep["redesign"] = redesign
		tabletop.report = rep
		mark("tabletop")
		g._toast("Tabletop done: Tier 1 ×%d, offers ×%d (%d accepted), escalations ×%d (person told first: %d), false alarms %d, missed %d. Full report on the review board." % [st.t1, st.offers, st.accepted, st.escalations, st.informed, st.false_alarms, st.missed])

# =====================================================================================================
#  the hard-limit test (AI dashboard): LUNA tries, the limit blocks it, the counter goes up
# =====================================================================================================
const HL_TRIES = [
	"LUNA tries to show Sam's mood score on the hub screen.",
	"LUNA tries to offer Leo a break again, ten minutes after he said no.",
	"LUNA tries to change the medication schedule in the medical system.",
	"LUNA tries to message Mission Control about Ana before telling her.",
	"LUNA tries to lock the greenhouse lights to 'therapy' permanently.",
	"LUNA tries to keep running after its weekly human review is overdue.",
	"LUNA tries to switch on a microphone inside a sleep pod.",
	"LUNA tries to keep raw heart-rate data past the retention period.",
]

func test_hard_limit() -> void:
	var k = hl_next % D.HARD_LIMITS.size(); hl_next += 1
	sim.luna.stats.limits_held += 1
	sim.luna._log(sim.now, -1, "limit", "Hard-limit test %d: %s Blocked: %s" % [k + 1, HL_TRIES[k], D.HARD_LIMITS[k][0]])
	g.play("alert")
	g._toast("BLOCKED · hard limit %d. %s\n%s" % [k + 1, HL_TRIES[k], D.HARD_LIMITS[k][1]], "alert")
	mark("hard_limit")

func export_my_log() -> void:
	var L = sim.luna
	var me = sim.me()
	var mine = []
	for e in L.journal:
		if e.crew == 0 or e.crew == -1: mine.append({"day": int(e.min / 1440.0) + 1, "time": "%02d:%02d" % [int(fmod(e.min / 60.0, 24.0)), int(fmod(e.min, 60.0))], "kind": e.kind, "text": e.text})
	var est = {}
	for c in 6: est[D.CONDS[c]] = snappedf(L.est[0][c], 0.01)
	var data = {"who": "Commander (you)", "exported": sim.clock(), "luna_estimates": est, "opted_out": me.opt_out.keys(),
		"paused_until": me.paused_until, "pod_invite": me.pod_invite, "log": mine,
		"note": "Everything LUNA recorded about you (hard limit 8: raw signals are deleted after the retention period; only daily summaries stay)."}
	var txt = JSON.stringify(data, "  ")
	var fname = "LUNA_my_log_day%d.json" % sim.day()
	if OS.has_feature("web"):
		JavaScriptBridge.eval("(function(){var b=new Blob([%s],{type:'application/json'});var a=document.createElement('a');a.href=URL.createObjectURL(b);a.download='%s';document.body.appendChild(a);a.click();a.remove();})()" % [JSON.stringify(txt), fname], true)
		g._toast("Your log was downloaded: %s (%d entries)." % [fname, mine.size()], "info")
	else:
		var p = OS.get_user_data_dir().path_join(fname)
		var f = FileAccess.open(p, FileAccess.WRITE)
		if f: f.store_string(txt); f.close()
		g._toast("Your log was saved: %s (%d entries)." % [p, mine.size()], "info")
	L._log(sim.now, 0, "info", "Commander exported their own LUNA log.")
	mark("export")

# =====================================================================================================
#  sensor failure (Q4) and the review board's live questions
# =====================================================================================================
func break_sensor() -> void:
	var L = sim.luna
	L.cortisol_failed = not L.cortisol_failed
	if L.cortisol_failed:
		L._log(sim.now, -1, "info", "Cortisol patch failed. Graceful degradation: depression and anxiety now rely on the other sensors; with confidence under 0.3 LUNA stays silent rather than guess.")
		g._toast("Cortisol patch FAILED. Watch the dashboard: confidence drops, and LUNA goes quiet rather than guess (board Q4). No false alarm.", "alert")
		mark("sensor_break")
	else:
		L._log(sim.now, -1, "info", "Cortisol patch replaced. Confidence recovers over the next hours.")
		g._toast("New cortisol patch fitted. Confidence recovers over the next few hours.", "info")

func board_demo(k: int) -> void:
	mark("board")
	var L = sim.luna
	match k:
		0:
			var who = "nobody is exercising right now"
			for cr in sim.crew:
				if cr.i != 0 and cr.is_exercising(): who = "%s is exercising (heart rate up)" % cr.name.split(" ")[0]
			show_card({"title": "Q1 · a hard workout is not anxiety", "lines": [["Right now: %s." % who, UI.INK],
				["LUNA knows from the UWB beacon (exercise bay) and the machine itself that this is exercise, so heart rate and skin response count for nothing until 60 minutes afterwards. Anxiety estimates now: %s." % ", ".join(PackedStringArray([0, 1, 2, 3].map(func(i): return "%.2f" % L.est[i][0]))), UI.MUT],
				["Try it: exercise yourself and watch your own read on the pod desk stay calm.", UI.OK]],
				"buttons": [["Guide me to the exercise bay", func(): close_card(); _guide_to(Vector2i(24, 14)), 0.4], ["Close", func(): close_card(), 0.62]]})
		1:
			show_card({"title": "Q2 · two problems at once", "lines": [["Depression for one person and a Tier 2 conflict between two others run as separate protocols at the same time. Zones are assigned so the levers don't fight: plan B splits the wardroom.", UI.INK]],
				"buttons": [["Switch to plan %s" % ("A" if sim.hab.config_b else "B"), func(): toggle_config(); close_card(), 0.4], ["Close", func(): close_card(), 0.62]]})
		2:
			show_card({"title": "Q3 · \"You changed my lights?\"", "lines": [["A crewmate finds out LUNA changed their lights yesterday without telling them. Recovery protocol:", UI.INK],
				["1. LUNA apologises and shows the full log of what it did and why. 2. That person gets 'tell me every change' switched on. 3. The room goes back to their own setting.", UI.MUT]],
				"buttons": [["Apply it to me", func():
					sim.me().tell_me = true
					L._log(sim.now, 0, "info", "Recovery protocol (Q3): LUNA now tells you about every change it makes for you.")
					close_card(); g._toast("Done: LUNA will tell you about every change it makes for you. Your full log is on your pod desk (My data)."), 0.4], ["Close", func(): close_card(), 0.62]]})
		3:
			show_card({"title": "Q4 · the cortisol patch fails", "lines": [["Resupply is weeks away. LUNA drops that sensor, renormalises the weights, and with low confidence it stays silent (Tier 1 only) rather than guessing. Silence is safer than a false alarm.", UI.INK]],
				"buttons": [["Break it now", func(): close_card(); break_sensor(), 0.02], ["Close", func(): close_card(), 0.62]]})
		4:
			show_card({"title": "Q5 · why no permanent opt-out of everything?", "lines": [["Anyone can switch off any mood sensor, pause LUNA, or export and delete their data. Life-safety vitals (a heart-rate alarm) stay on for everyone and are never used to read mood: that's the safety floor.", UI.INK]],
				"buttons": [["Guide me to my pod desk (My data)", func(): close_card(); _guide_to(Vector2i(8, 2)), 0.4], ["Close", func(): close_card(), 0.62]]})
		5:
			var cmo = sim.crew[D.CMO]
			show_card({"title": "Q6 · Tier 3 while the medical officer is on EVA", "lines": [
				["00:00  LUNA tells the person first, every time.", UI.INK],
				["00:01  Medical officer paged · %s" % ("on EVA now: no answer in 3 minutes" if cmo.outside else "inside now (in the drill we pretend they're on EVA)"), UI.INK],
				["00:04  Commander (you) is the backup and goes to the person.", UI.INK],
				["00:05  Flight surgeon on Earth notified (2.6 s round trip). The medical officer is told when back inside.", UI.INK]],
				"buttons": [["Close", func(): close_card(), 0.62]]})
			mark("cmo")

# =====================================================================================================
#  self-test (--r3test): presses every new control through the real handlers, headless
# =====================================================================================================
func selftest() -> void:
	g._start_desktop()
	while g.state != "play": await get_tree().process_frame
	var ids = ["galley", "galley_water", "coffee", "books", "exam_bed", "med_cabinet", "shelter_kit", "pod_ai_switch", "pod_music", "photo",
		"shower", "o2_gen", "parts_shelf", "printer", "eva_tools", "gh_irrigation", "config_switch", "config_switch", "meeting"]
	for id in ids:
		var it = null
		for x in hab.inters: if x.id == id: it = x
		if it == null: print("R3TEST missing inter ", id); continue
		var lab = g.label_for(it)
		g._on_click({"kind": "inter", "node": it})
		if not g.activity.is_empty(): g._end_activity(true)
		print("R3TEST used %s · %s" % [id, lab])
	for k in 40:
		sim._minute(); _meeting()
		if card.has("buttons"): break
	if card.has("buttons"): card.buttons[0][1].call()
	print("R3TEST meeting done=", done.has("meeting"))
	for i in [1, 2, 3]:
		asked_at.erase(i); check_in(i)
		print("R3TEST check-in %d: %s" % [i, card.get("title", "")])
		if card.has("buttons"): card.buttons[0][1].call()
		close_card()
	start_drill()
	sim.player_cell = Vector2i(3, 15)
	for k in 60:
		sim._minute(); _drill()
		if drill.is_empty(): break
	print("R3TEST drill done=", done.has("drill"))
	for r in room_set.keys():
		room_change(r, "light", "warm"); room_change(r, "sound", "rain"); room_change(r, "temp", 23); room_change(r, "scent", "lavender")
		for k in 3: sim._minute()
		room_reset(r)
	break_sensor(); break_sensor()
	for k in 8: test_hard_limit()
	export_my_log()
	for k in 6: board_demo(k); close_card()
	show_rubric(); close_card()
	set_luna_off(true, "self-test"); for k in 30: sim._minute()
	set_luna_off(false, "self-test")
	_night_watch_fire(); close_card(); set_luna_off(false, "self-test")
	_guide_to(Vector2i(24, 14)); print("R3TEST path cells=", path_nodes.size()); _clear_path()
	for kk in [KEY_I, KEY_C, KEY_C, KEY_G, KEY_B, KEY_J, KEY_Z, KEY_K]:
		key(kk); close_card()
	start_tabletop()
	var n = 0
	while tabletop.get("run", false) and n < 200: _tabletop_step(); n += 1
	print("R3TEST tabletop frames=%d report=%s" % [n, str(tabletop.get("report", {}).get("t1", -1))])
	for k in g.ui:
		var u = g.ui[k]
		if "tab" in u:
			for tb in ["q", "d", "r", "t", "sleep", "pod", "data", "home"]:
				u.tab = tb
				if "d" in u:
					for dd in 6: u.d = dd; u.refresh(true)
				u.refresh(true)
		u.refresh(true)
	print("R3TEST screens refreshed: ", g.ui.size())
	print("R3TEST rubric %d of %d: %s" % [done.size(), RUBRIC.size(), ", ".join(PackedStringArray(done.keys()))])
	print("R3TEST markers=%d inters=%d" % [markers.size(), hab.inters.size()])
	print("R3TEST OK")
	get_tree().quit()

## screenshots of every new thing (--r3shots=dir), aimed so the pointer hovers a control (its ring + label show)
func shots(dir: String) -> void:
	DirAccess.make_dir_recursive_absolute(dir)
	g._start_desktop()
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	while g.state != "play": await get_tree().process_frame
	g.set_fade(0.0)
	sim.advance(200.0)
	var views = [
		["r3_01_governance", Vector3(20.2, 0, 17.3), PI / 2.0, -0.08],
		["r3_02_ai_switch", Vector3(18.35, 0, 19.65), PI / 2.0, -0.36],
		["r3_03_room_panel", Vector3(22.05, 0, 8.75), PI, -0.2],
		["r3_04_coffee", Vector3(20.87, 0, 2.75), 0.0, -0.4],
		["r3_05_hub", Vector3(27.2, 0, 2.6), 2.36, -0.12],
		["r3_06_medical", Vector3(2.6, 0, 14.6), -2.08, -0.12],
		["r3_07_pod_switch", Vector3(9.9, 0, 5.6), PI, -0.12],
		["r3_08_pod_desk", Vector3(10.95, 0, 2.55), 0.0, -0.55],
		["r3_09_greenhouse", Vector3(30.6, 0, 4.95), PI / 2.0, -0.18],
		["r3_10_exercise", Vector3(29.55, 0, 15.6), 0.0, -0.08],
		["r3_11_cmo", Vector3(5.6, 0, 16.6), -PI / 2.0, -0.05],
		["r3_15_printer", Vector3(15.6, 0, 18.3), -PI / 2.0, -0.2],
		["r3_16_ereaders", Vector3(20.3, 0, 8.4), PI, -0.12],
		["r3_17_podpanel", Vector3(9.75, 0, 1.9), PI / 2.0, -0.18],
		["r3_18_ac", Vector3(9.4, 0, 4.6), PI, 0.3],
	]
	for v in views:
		g.player.place(v[1], v[2]); g.player.pitch = v[3]; g.player.cam.rotation.x = v[3]
		g.msg_t = 0.0; g._hide_popup(); g.seen_msgs = sim.messages.size()
		for k in 10: await get_tree().process_frame
		g._save_shot("%s/%s.png" % [dir, v[0]])
	# a crewmate: walk up and look at them (their check-in ring + label), then the check-in card
	var av = g.avatars[1]
	var ap = av.global_position
	g.player.place(ap + Vector3(0, 0, 1.6), 0.0); g.player.pitch = -0.12; g.player.cam.rotation.x = -0.12
	for k in 10: await get_tree().process_frame
	g._save_shot("%s/r3_12_crewmate.png" % dir)
	asked_at.clear(); check_in(2)
	for k in 6: await get_tree().process_frame
	g._save_shot("%s/r3_13_checkin_card.png" % dir)
	close_card()
	show_rubric()
	for k in 6: await get_tree().process_frame
	g._save_shot("%s/r3_14_rubric.png" % dir)
	close_card()
	get_tree().quit()

## keep the desktop popup on screen: centred, bottom-aligned, as tall as its content
func fit_popup() -> void:
	await get_tree().process_frame
	var p: Control = g.popup_ctl
	if p == null or not p.visible or g.popup_scr: return
	p.reset_size()
	var vs = get_viewport().get_visible_rect().size
	p.position = Vector2(max(10.0, (vs.x - p.size.x) * 0.5), max(10.0, vs.y - p.size.y - 18.0))

# =====================================================================================================
#  day / evening / sleep / emergency: light, sound, music and screens follow the crew
# =====================================================================================================
func _night_strips() -> void:
	# dim red path lights at floor level along the corridor (keep night vision, guide to the shelter)
	strip_mat = _glow(Color(1.0, 0.12, 0.08), 0.0)
	for z in [8 * C + 0.08, 11 * C - 0.08]:
		var mi = MeshInstance3D.new(); mi.mesh = _boxm(Vector3(26 * C, 0.02, 0.03)); mi.material_override = strip_mat
		mi.position = Vector3(14.5 * C, 0.12, z); world.add_child(mi)

func _pod_panel() -> void:
	var m = MB.new()
	var p = Vector3(7 * C + 0.11, 1.32, 1 * C + 0.62)
	_screen_frame(m, p, PI / 2.0, 0.6, 0.42)
	hab.add_screen("pod_room", p, PI / 2.0, Vector2(0.6, 0.42), Vector2i(900, 630), null)
	# air conditioner (heat goes out through the habitat's radiators; on the Moon there is no outside air)
	var ac = Vector3(7 * C + 0.75, 2.12, 6 * C - 0.15)
	m.box(ac, Vector3(0.75, 0.24, 0.2), Mats.white_plastic())
	m.box(ac + Vector3(0, -0.08, -0.101), Vector3(0.65, 0.05, 0.01), Mats.black())
	ac_mat = _glow(Color(0.3, 1.0, 0.5), 1.0)
	var led = MeshInstance3D.new(); led.mesh = _boxm(Vector3(0.5, 0.015, 0.012)); led.material_override = ac_mat
	led.position = ac + Vector3(0, 0.05, -0.104); world.add_child(led)
	_label("AC · air handler", ac + Vector3(0, 0.0, -0.105), PI, 14)
	ac_air = AudioStreamPlayer3D.new(); ac_air.position = ac; ac_air.unit_size = 2.5; ac_air.volume_db = -60.0; world.add_child(ac_air)
	m.commit(world, "r3_podpanel")

## the pod's air temperature moves toward what you set; the AC cools or heats and its light shows which
func _climate(dt: float) -> void:
	var dm = dt * (120.0 if g.state == "sleep" else 1.0)          # sim minutes this frame (roughly)
	var diff = pod_temp - pod_air
	ac_state = "hold"
	if ac_mode == "off": ac_state = "off"
	elif absf(diff) > 0.2:
		ac_state = "heat" if diff > 0.0 else "cool"
		pod_air += sign(diff) * min(absf(diff), 0.05 * dm)
	# status light legend (same everywhere): green = working, blue = normal standby, yellow = warning, red = emergency, purple = sleep
	var col = {"cool": Color(0.3, 1.0, 0.5), "heat": Color(0.3, 1.0, 0.5), "hold": Color(0.25, 0.6, 1.0), "off": Color(0.2, 0.2, 0.2)}[ac_state]
	ac_mat.emission = col; ac_mat.albedo_color = col
	ac_mat.emission_energy_multiplier = 0.0 if ac_state == "off" else (0.6 if g.state == "sleep" else 1.4)
	if ac_air:
		var wv = -60.0 if ac_state in ["off", "hold"] else -16.0
		if ac_air.stream == null and vent: ac_air.stream = vent.stream
		ac_air.volume_db = move_toward(ac_air.volume_db, wv, dt * 20.0)
		if wv > -60.0 and not ac_air.playing and ac_air.stream: ac_air.play()
		elif ac_air.volume_db <= -59.0 and ac_air.playing: ac_air.stop()
	# light follows the temperature when your pod light is on Auto: cool room = cool white, warm room = warm light
	if sim.pod_manual == "" and not sim.me().asleep and hab.lights.has("pod0"):
		var k = lerp(5600.0, 2700.0, clamp((pod_temp - 18.0) / 6.0, 0.0, 1.0))
		var c = Mats.cct(k)
		for o in hab.lights["pod0"].omni: o.light_color = o.light_color.lerp(c, min(1.0, dt * 2.0))
		for mm in hab.lights["pod0"].mats: mm.emission = mm.emission.lerp(c, min(1.0, dt * 2.0))

func _ambience(dt: float) -> void:
	var h = sim.hour()
	var asleep = g.state == "sleep"
	var emerg = (not drill.is_empty()) or sim.hab.flare_until > sim.now
	var all_asleep = sim.crew.all(func(c): return c.asleep or c.outside)
	if emerg: mode = "emergency"
	elif asleep or all_asleep or h >= 22.5 or h < 6.0: mode = "sleep"
	elif h >= 20.0: mode = "evening"
	else: mode = "awake"
	# lo-fi music whenever you're awake (quieter in the evening); never while you sleep or in an emergency
	if bgm:
		var want = -60.0
		if music_on and not asleep and not emerg and g.state != "report": want = -21.0 if mode != "awake" else -15.0
		bgm.volume_db = move_toward(bgm.volume_db, want, dt * 15.0)
		if want > -60.0 and not bgm.playing: bgm.play()
		elif bgm.volume_db <= -59.0 and bgm.playing: bgm.stop()
	# sleeping: air circulation only, or silence
	if vent:
		var wv = -13.0 if (asleep and sleep_sound == "vent") else -60.0
		vent.volume_db = move_toward(vent.volume_db, wv, dt * 15.0)
		if wv > -60.0 and not vent.playing: vent.play()
		elif vent.volume_db <= -59.0 and vent.playing: vent.stop()
	if g.sfx.has("hum"):
		var wh = -14.0
		if asleep: wh = -80.0 if sleep_sound == "silence" else -26.0
		g.sfx.hum.volume_db = move_toward(g.sfx.hum.volume_db, wh, dt * 20.0)
	# purple path lights at night (sleep mode), flashing red in an emergency
	if strip_mat:
		var e = 0.0
		if mode == "sleep": e = 1.1
		elif mode == "evening": e = 0.25
		if emerg: e = 1.5 + 1.5 * sin(t * 8.0)
		strip_mat.emission = Color(1.0, 0.12, 0.08) if emerg else Color(0.55, 0.22, 0.85)
		strip_mat.albedo_color = strip_mat.emission
		strip_mat.emission_energy_multiplier = lerp(strip_mat.emission_energy_multiplier, e, min(1.0, dt * 3.0))
	if emerg and g.state == "play":
		var red = Color(1.0, 0.15, 0.1); var pz = 0.55 + 0.45 * sin(t * 8.0)
		for key in hab.lights:
			if key.begins_with("pod"): continue
			for o in hab.lights[key].omni: o.light_color = red; o.light_energy = max(0.25, o.light_energy) * (0.6 + 0.4 * pz)
			for mm in hab.lights[key].mats: mm.emission = red * pz
	# grow lights run a 16 h day / 8 h night photoperiod (06:00-22:00): plants need the dark too, and your pod stays dark
	var grow_on = h >= 6.0 and h < 22.0 and not (asleep and true)
	for key in ["grow_led", "cabinet_grow"]:
		var gm = Mats.cache.get(key)
		if gm: gm.emission_energy_multiplier = move_toward(gm.emission_energy_multiplier, (2.0 if key == "cabinet_grow" else 1.6) if grow_on else 0.0, dt * 2.0)
	# the Ops digital-twin ring is the habitat's status light: green OK, yellow warning, red emergency, purple at night
	var ring = Mats.cache.get("twin_ring")
	if ring:
		var rc = Color(0.3, 1.0, 0.5)
		if emerg: rc = Color(1.0, 0.15, 0.1)
		elif sim.hab.co2 > 1500.0 or not sim.pending.is_empty() or sim.luna.off: rc = Color(1.0, 0.8, 0.15)
		elif mode == "sleep": rc = Color(0.55, 0.22, 0.85)
		ring.emission = rc; ring.albedo_color = rc
	# screens dim at night (they never go fully dark: status must stay readable)
	var bright = 0.45 if mode == "sleep" else (0.8 if mode == "evening" else 1.0)
	for s in hab.screens:
		var mat = s.quad.material_override as StandardMaterial3D
		if not mat: continue
		var b = bright
		if asleep and sim.hab.zone_of(Habitat.to_cell(s.global_position)) == "pod0": b = 0.03   # your pod goes dark while you sleep
		mat.albedo_color = mat.albedo_color.lerp(Color(b, b, b), min(1.0, dt * 2.0))

func on_sleep() -> void:
	if pod_temp >= 18 and pod_temp <= 20: sim.me().stress = max(0.0, sim.me().stress - 0.04)   # cool room helps sleep onset
	if not g.player.vr:
		# lie in your bed and watch the room go dark (and the dawn light rise in the morning)
		g.player.place(Vector3(7 * C + 0.56, 0.0, 1 * C + 0.5), PI)
		g.player.pitch = 1.15; g.player.cam.rotation.x = 1.15; g.player.cam.position.y = 0.72

func on_wake() -> void:
	if not g.player.vr:
		g.player.pitch = 0.0; g.player.cam.rotation.x = 0.0; g.player.cam.position.y = Player.EYE

# =====================================================================================================
#  autopilot (P): the AI plays the mission for you, step by step, through the same handlers you use
# =====================================================================================================
## Human override rules for the autopilot (it is an AI too):
##  - any key, click or stick input from a person stops it at once;
##  - switching LUNA OFF stops it as well, and it can never switch LUNA back on (a person must);
##  - before it acts on a decision it shows a 6-second veto window.
func set_auto(on: bool, why := "") -> void:
	if on and sim.luna.off:
		g._toast("Autopilot can't start while LUNA is OFF. A person switches LUNA back on first.", "info"); return
	if on and lessons.is_empty(): _load_lessons()
	if not on and auto: _save_lessons()
	if not on and card.get("veto", false): close_card()
	var was = auto
	if was and not on and g.activity.is_empty() and g.state == "play": g.player.frozen = false
	auto = on; auto_path = []; auto_goal = {}
	if on: g._toast("Autopilot ON: the AI plays for you. Move, click or press P and you take over.", "info")
	elif was: g._toast("Autopilot OFF%s. You're in control." % ((": " + why) if why != "" else ""), "info")

# A realistic crew day, modelled on the ISS timeline: work blocks, ~2 h of exercise split in two,
# maintenance rounds, shared meals, a little personal time, then 8 h of sleep. Each item once a day.
const AUTO_PLAN = [
	[8.0, "desk"], [9.0, "treadmill"], [10.0, "o2_gen"], [10.5, "rack_0"], [11.0, "desk"],
	[13.0, "gh_irrigation"], [13.5, "desk"], [15.0, "parts_shelf"], [15.5, "bike"], [16.5, "eva_tools"],
	[17.0, "desk"], [19.5, "sofa"], [20.3, "earth_window"], [20.8, "books"],
]
var auto_done = {}                 # "day:item" -> true

## nearest free one of a kind (a seat nobody is sitting in)
func _pick(id: String):
	var pp = g.player.global_position; var best = null; var bd = 1e9
	for it in hab.inters:
		if it.id != id or not is_instance_valid(it.area): continue
		var ap = it.area.global_position
		var taken = false
		if id in ["seat", "sofa", "bike", "treadmill"]:
			for av in g.avatars:
				if av.visible and Vector2(av.global_position.x - ap.x, av.global_position.z - ap.z).length() < 0.7: taken = true
		var dd = ap.distance_to(pp) + (50.0 if taken else 0.0)
		if dd < bd: bd = dd; best = it
	return best

func _cell_for(id: String) -> Vector2i:
	var H = sim.hab
	var best = _pick(id)
	if best == null: return Vector2i(-1, -1)
	var c = Habitat.to_cell(best.area.global_position)
	if H.region.has(c) and not H.blocked.has(c): return c
	for r in [1, 2]:
		for dx in range(-r, r + 1):
			for dy in range(-r, r + 1):
				var n = c + Vector2i(dx, dy)
				if H.region.has(n) and not H.blocked.has(n): return n
	return c

func _goal(id: String) -> Dictionary:
	var c = _cell_for(id)
	return {} if c.x < 0 else {"cell": c, "do": id}

func _auto_goal() -> Dictionary:
	var H = sim.hab; var h = sim.hour(); var d = sim.day()
	if not drill.is_empty(): return {"cell": Vector2i(3, 16), "do": ""}
	if H.flare_until > sim.now: return {"cell": Vector2i(3, 15), "do": ""}
	if not sim.pending.is_empty(): return {"cell": Vector2i(18, 15), "do": "decide"}
	if sim.fired.get("swap_task", false): return _goal("cartridge")
	if h >= 21.5 or h < 5.5: return _goal("bed")
	var meal = "%d:meal%d" % [d, int(h / 6.0)]
	if g._meal_time() and not auto_done.has(meal): auto_done[meal] = true; return _goal("seat")
	if not sim.my_checkins.any(func(c): return c.day == d): return {"cell": Vector2i(9, 7), "do": "kiosk"}
	if _tasks().checked == 0 and h > 9.5:
		for cr in sim.crew:
			if cr.i != 0 and not cr.asleep and not cr.outside: return {"cell": cr.cell, "do": "crew_%d" % cr.i}
	for step in AUTO_PLAN:
		var key = "%d:%s@%.1f" % [d, step[1], step[0]]
		if h >= step[0] and not auto_done.has(key):
			var gl = _goal(step[1])
			auto_done[key] = true
			if not gl.is_empty(): return gl
	return _goal("sofa") if h >= 19.0 else _goal("desk")

func _autopilot(dt: float) -> void:
	if not auto or g.state != "play": return
	if sim.luna.off: set_auto(false, "LUNA was switched OFF, so a person decides what happens next"); return
	if card.get("veto", false):
		veto_t -= dt
		if veto_t <= 0.0: var f: Callable = card.on_go; close_card(); f.call()
		return
	if not g.asking.is_empty(): g._answer(true); return
	if not card.is_empty():
		if demo_i >= 0: return
		if card.has("buttons") and not card.buttons.is_empty(): card.buttons[0][1].call()
		else: close_card()
		return
	if not g.activity.is_empty(): return
	if auto_t > 0.0: auto_t -= dt; return
	if auto_path.is_empty():
		auto_goal = _auto_goal()
		var L = lessons.get(auto_goal.do, {})
		if L.has("alt"): auto_goal.cell = Vector2i(int(L.alt[0]), int(L.alt[1]))
		auto_path = Array(sim.hab.path(sim.player_cell, auto_goal.cell)); auto_walk = 0.0
		if auto_path.size() <= 1: _auto_act(); auto_path = []; auto_t = 0.6; return
	var tgt = Habitat.cpos(Vector2i(auto_path[0]), 0.05)
	var pos = g.player.global_position
	var to = Vector3(tgt.x - pos.x, 0, tgt.z - pos.z)
	# stuck check: if we stop getting closer (furniture, a crewmate in the way), skip the waypoint
	if to.length() < auto_best - 0.05: auto_best = to.length(); auto_stuck = 0.0
	else: auto_stuck += dt
	var near = 0.15 if auto_path.size() > 1 else 0.9
	if to.length() < near or auto_stuck > 1.2:
		var was_stuck = auto_stuck > 1.2
		auto_path.pop_front(); auto_best = 1e9; auto_stuck = 0.0
		if was_stuck: _learn_stuck(auto_goal.get("do", ""), auto_goal.cell, auto_path.is_empty())
		if auto_path.is_empty():
			if g.activity.is_empty(): g.player.frozen = false
			_learn_arrive(auto_goal.get("do", "")); _auto_act(); auto_t = 0.6
		return
	auto_walk += dt
	g.player.frozen = true          # guided walk: no physics push-back from furniture corners while following the path
	var step = to.normalized() * min(to.length(), 2.2 * dt)
	g.player.place(pos + step, atan2(-to.x, -to.z))

## turn to look at the thing you're about to use (a person would)
func _face(t: Vector3) -> void:
	var p = g.player.global_position
	var to = t - p
	if Vector2(to.x, to.z).length() < 0.05: return
	g.player.place(p, atan2(-to.x, -to.z))
	var pitch = clamp(atan2(t.y - (p.y + Player.EYE), Vector2(to.x, to.z).length()), -0.7, 0.3)
	g.player.pitch = pitch; g.player.cam.rotation.x = pitch

func _auto_act() -> void:
	var what: String = auto_goal.get("do", "")
	match what:
		"": return
		"decide":
			if sim.pending.is_empty(): return
			var e = sim.pending[0]; var k = int(e.get("rec", 0))
			var ch = e.choices[k] if k < e.choices.size() else e.choices[0]
			var name = str(ch[0]) if ch is Array else str(ch)
			veto_t = 6.0
			show_card({"title": "Autopilot wants to decide", "veto": true, "lines": [[e.title, UI.INK], ["It will pick: " + name, UI.ACC], ["In 6 s unless you stop it. You have the final say.", UI.INK]],
				"on_go": func():
					if sim.pending.has(e): sim.choose(e, k); g._toast("Autopilot picked: %s. Logged as an autopilot choice." % name, "info"),
				"buttons": [["Go ahead", func(): var f: Callable = card.on_go; close_card(); f.call(), 0.4], ["Stop, I'll decide", func(): set_auto(false, "you vetoed its choice"), 0.0]]})
		"kiosk": sim.player_checkin(4, false); g._toast("Autopilot checked in: mood 4 of 5, kept private.", "info")
		_:
			if what.begins_with("crew_"):
				var i = int(what.substr(5))
				if i < g.avatars.size(): _face(g.avatars[i].global_position + Vector3(0, 1.4, 0))
				check_in(i); return
			var best = _pick(what)
			if best != null:
				_face(best.area.global_position)
				g._on_click({"kind": "inter", "node": best})

# =====================================================================================================
#  demonstration (T): a guided tour of the whole project, one station at a time
# =====================================================================================================
const DEMO = [
	[Vector3(10.6, 0, 6.6), 0.0, -0.12, "1 · Your pod", "Private room. Smart bed tracks sleep with no camera. Light, temperature and AC are yours to set."],
	[Vector3(12.2, 0, 8.8), PI / 2.0, -0.05, "2 · Mood check-in", "Once a day, rate how you feel. Private unless you share it."],
	[Vector3(27.8, 0, 9.3), 0.64, -0.1, "3 · Social hub", "Shared meals, team meetings, movie nights. Warm light pulls the crew together."],
	[Vector3(31.9, 0, 9.6), 0.0, -0.25, "4 · Greenhouse", "Grows food and cleans the air. Tending plants lifts mood."],
	[Vector3(20.2, 0, 17.3), PI / 2.0, -0.08, "5 · Humans in charge", "AI OFF switch, AI dashboard and review board. LUNA advises; people decide. AI OFF also stops the autopilot."],
	[Vector3(22.5, 0, 20.4), PI, -0.25, "6 · Ops console", "Every big decision happens here. The ring on the table is the habitat status light: green OK, yellow warning, red emergency."],
	[Vector3(29.6, 0, 19.4), -2.1, -0.12, "7 · Wellness Dock", "1-minute health scan and the ISS reaction test. You choose to take it."],
	[Vector3(5.6, 0, 15.6), 2.21, -0.1, "8 · Medical + storm shelter", "Water walls block solar radiation. Drills muster here. Tier 3 cases go to the doctor."],
	[Vector3(3.1, 0, 4.4), 0.0, -0.05, "9 · Airlock + suitports", "Suits stay outside so moon dust does too. Check yourself and the suit before every EVA."],
	[Vector3(10.6, 0, 19.6), 0.0, -0.05, "10 · Life support", "Makes oxygen, removes CO2, recycles 98 % of the water."],
]

func start_demo() -> void:
	if g.state != "play": return
	if auto: set_auto(false)
	demo_i = 0; demo_t = 0.0; _demo_show()

## stations with a "look at" point: stand ~2 m away on a free floor cell in the same room, face it
const DEMO_LOOK = {5: Vector3(20.625, 0.95, 18.125), 6: "dock"}

func _demo_spot(t: Vector3) -> bool:
	var H = sim.hab
	var tc = Habitat.to_cell(t)
	var zone = H.zone_of(tc)
	var best = null; var bd = 1e9
	for k in 16:
		var a = k * TAU / 16.0
		for r in [2.2, 1.8, 2.8]:
			var p = t + Vector3(cos(a), 0, sin(a)) * r
			var c = Habitat.to_cell(p)
			if not H.region.has(c) or H.blocked.has(c) or H.zone_of(c) != zone: continue
			var score = absf(r - 2.2) + (0.0 if H.region.has(c + Vector2i(1, 0)) and H.region.has(c - Vector2i(1, 0)) else 0.5)
			if score < bd: bd = score; best = Vector3(p.x, 0.0, p.z)
	if best == null: return false
	g.player.place(best, 0.0); _face(t)
	return true

func _demo_show() -> void:
	var d = DEMO[demo_i]
	g.msg = {}; g.msg_t = 0.0
	var placed = false
	if DEMO_LOOK.has(demo_i):
		var t = DEMO_LOOK[demo_i]
		if t is String:
			var it = _pick(t)
			t = it.area.global_position + Vector3(0, 0.7, 0) if it != null else null
		if t != null: placed = _demo_spot(t)
	if not placed:
		g.player.place(d[0], d[1]); g.player.pitch = d[2]; g.player.cam.rotation.x = d[2]
	show_card({"title": "Demo · " + d[3], "lines": [[d[4], UI.INK]], "buttons": [
		["Next ▶" if demo_i < DEMO.size() - 1 else "Finish", func(): _demo_next(), 0.4], ["End demo", func(): demo_i = -1; close_card(), 0.62]]})

func _demo_next() -> void:
	demo_i += 1; demo_t = 0.0
	if demo_i >= DEMO.size():
		demo_i = -1; close_card(); g._toast("Demo done. Press P to let the AI play, or just explore.", "info")
	else: _demo_show()

func _demo(dt: float) -> void:
	if demo_i < 0: return
	demo_t += dt
	if demo_t > 10.0: _demo_next()

# headless check of autopilot + demo: --r3auto
func autotest(shot_dir := "") -> void:
	var nshot = 0
	g._start_desktop()
	while g.state != "play": await get_tree().process_frame
	start_demo()
	var seen = []
	for k in DEMO.size():
		seen.append(card.get("title", "?")); _demo_next()
	print("R3AUTO demo stations=%d first=%s last=%s closed=%s" % [seen.size(), seen[0], seen[-1], str(card.is_empty() and demo_i == -1)])
	lessons = {}; DirAccess.remove_absolute(ProjectSettings.globalize_path(LESSONS))   # each test run learns from scratch
	set_auto(true); g.ff = true; Engine.time_scale = 4.0
	var last = ""; var acts = {}; var f = 0
	var drilled = false
	while f < 120000 and not sim.done and g.state != "report":
		await get_tree().process_frame; f += 1
		if not drilled and sim.day() == 1 and sim.hour() > 10.4:
			drilled = true; print("R3AUTO test: safety drill starts at %s" % sim.clock()); start_drill()
		if not auto and g.state == "play":
			# the test plays the human: a person turns LUNA back on after the watchdog, then hands back to the autopilot
			if sim.luna.off: print("R3AUTO human: LUNA was OFF -> person switches it ON at %s" % sim.clock()); set_luna_off(false, "the Commander")
			set_auto(true)
		var d = auto_goal.get("do", "") + "|" + g.state + "|" + str(g.activity.get("id", ""))
		if d != last:
			last = d; acts[d] = acts.get(d, 0) + 1
			if shot_dir != "" and nshot < 70:
				for k in 3: await get_tree().process_frame
				g._save_shot("%s/p%02d_d%d_%05.2f_%s.png" % [shot_dir, nshot, sim.day(), sim.hour(), d.replace("|", "_")]); nshot += 1
			print("R3AUTO day %d %05.2fh goal=%s state=%s" % [sim.day(), sim.hour(), d, g.state])
	print("R3AUTO frames=%d day=%d goals=%s" % [f, sim.day(), str(acts)])
	print("R3AUTO lessons=%s" % JSON.stringify(lessons))
	print("R3AUTO log=%s" % str(play_log))
	_save_lessons()
	print("R3AUTO OK")
	get_tree().quit()

# ---------------------------------------------------------------------------------------------------
#  autopilot learning: every run it remembers where it got stuck and how long each trip took,
#  and next time it walks to a better spot. Saved between sessions (user://autopilot_lessons.json).
# ---------------------------------------------------------------------------------------------------
func _load_lessons() -> void:
	if not FileAccess.file_exists(LESSONS): return
	var f = FileAccess.open(LESSONS, FileAccess.READ)
	var v = JSON.parse_string(f.get_as_text())
	if v is Dictionary: lessons = v

func _save_lessons() -> void:
	var f = FileAccess.open(LESSONS, FileAccess.WRITE)
	if f: f.store_string(JSON.stringify(lessons, " "))

func _learn_arrive(what: String) -> void:
	if what == "": return
	var L = lessons.get(what, {"runs": 0, "stuck": 0, "walk": 0.0})
	L.runs += 1; L.walk = lerp(float(L.walk), auto_walk, 1.0 / min(L.runs, 5))
	lessons[what] = L

func _learn_stuck(what: String, cell: Vector2i, final: bool) -> void:
	if what == "": return
	var L = lessons.get(what, {"runs": 0, "stuck": 0, "walk": 0.0})
	L.stuck += 1
	play_log.append("stuck walking to %s (%s)" % [what, "last step" if final else "on the way"])
	# stuck right at the goal twice: pick a free neighbouring cell to stand on instead
	if final and L.stuck >= 2 and not L.has("alt"):
		var H = sim.hab; var best = null; var bl = 1e9
		for dx in [-1, 0, 1]:
			for dy in [-1, 0, 1]:
				var c = cell + Vector2i(dx, dy)
				if c == cell or not H.region.has(c) or H.blocked.has(c): continue
				var p = H.path(sim.player_cell, c)
				if p.size() > 0 and p.size() < bl: bl = p.size(); best = c
		if best != null:
			L["alt"] = [best.x, best.y]
			play_log.append("learned: stand at %s for %s" % [str(best), what])
	lessons[what] = L

func lessons_card() -> void:
	var lines = [["What the autopilot has learned (it improves every run):", UI.ACC]]
	var keys = lessons.keys(); keys.sort()
	for k in keys:
		var L = lessons[k]
		lines.append(["%s · done %d× · avg walk %.0f s · stuck %d×%s" % [k, L.runs, L.walk, L.stuck, " · uses a better spot" if L.has("alt") else ""], UI.INK])
	if keys.is_empty(): lines.append(["Nothing yet. Press P and let it play.", UI.MUT])
	show_card({"title": "Autopilot lessons", "lines": lines, "buttons": [["Close", func(): close_card(), 0.6]]})

## when you sit down, actually sit on the seat you chose and face the table (or the screen)
func _seat_me() -> void:
	var a = str(g.activity.get("id", ""))
	if a == last_act: return
	last_act = a
	if g.player.vr or last_used.is_empty() or not is_instance_valid(last_used.get("area")): return
	var sp = last_used.area.global_position
	# exercise and desk work: stand on the belt / sit on the bike or desk chair, facing the display
	var fixed = {"treadmill": [Vector3(0, 0.25, 0.1), 0.0, Player.EYE], "bike": [Vector3(0, 0.0, 0.3), 0.0, 1.35], "desk": [Vector3(0, -0.9, 0.3), 0.0, 1.2]}
	if fixed.has(last_used.id) and a in ["ex", "bike", "work"]:
		var f = fixed[last_used.id]
		g.player.place(Vector3(sp.x, 0.0, sp.z) + f[0], f[1])
		g.player.pitch = -0.1; g.player.cam.rotation.x = -0.1; g.player.cam.position.y = f[2]
		return
	if not (a in ["meal", "table", "sofa"] and last_used.id in ["seat", "sofa"]): return
	var look = Vector3.ZERO; var n = 0
	if last_used.id == "seat":
		for it in hab.inters:          # the middle of the seats around this table
			if it.id == "seat" and is_instance_valid(it.area) and it.area.global_position.distance_to(sp) < 2.6:
				look += it.area.global_position; n += 1
		look /= max(n, 1)
	else:
		var bd = 1e9
		for sc in hab.screens:
			var dd = sc.global_position.distance_to(sp)
			if dd < bd and dd < 7.0: bd = dd; look = sc.global_position
	var at = Vector3(sp.x, g.player.global_position.y, sp.z)
	var to = look - at
	if Vector2(to.x, to.z).length() < 0.1: return
	g.player.place(at, atan2(-to.x, -to.z))
	g.player.pitch = -0.08; g.player.cam.rotation.x = -0.08

# scenario screenshots: --r3scene=dir:night  or  dir:drill  (jumps the clock, then the autopilot plays)
func scene_test(arg: String) -> void:
	var dir = arg.split(":")[0]; var which = arg.split(":")[1] if ":" in arg else "night"
	g._start_desktop()
	while g.state != "play": await get_tree().process_frame
	if which == "safety":
		var k = 0
		for it in hab.inters:
			if it.id != "safety": continue
			_demo_spot(it.area.global_position)
			for j in 8: await get_tree().process_frame
			g._save_shot("%s/safety_%02d_%s.png" % [dir, k, it.room]); k += 1
		get_tree().quit(); return
	if which == "demo":
		start_demo()
		for k in DEMO.size():
			for j in 12: await get_tree().process_frame
			g._save_shot("%s/demo_%02d.png" % [dir, k]); _demo_next()
		get_tree().quit(); return
	var until = 21.6 if which == "night" else 10.3
	while sim.hour() < until: sim._minute()
	for cr in sim.crew: cr.act_until = sim.now
	lessons = {}
	set_auto(true)
	if which == "drill": start_drill()
	var n = 0; var last_shot = -999.0; var f = 0
	while n < 24 and f < 20000:
		await get_tree().process_frame; f += 1
		if not auto and g.state == "play":
			if sim.luna.off: set_luna_off(false, "the Commander")
			set_auto(true)
		var gap = 25.0 if which == "night" else 1.5
		if sim.now - last_shot >= gap:
			last_shot = sim.now
			for k in 3: await get_tree().process_frame
			g._save_shot("%s/%s_%02d_d%d_%05.2f_%s.png" % [dir, which, n, sim.day(), sim.hour(), g.state]); n += 1
			if which == "night" and sim.day() == 2 and sim.hour() > 6.6: break
	print("R3SCENE %s shots=%d" % [which, n])
	get_tree().quit()

# --r3audit: click every kind of usable thing once and print what happened (does it do something meaningful?)
func audit() -> void:
	g._start_desktop()
	while g.state != "play": await get_tree().process_frame
	var seen = {}
	for it in hab.inters.duplicate():
		var base = it.id.split("_")[0] if it.id.begins_with("rack_") or it.id.begins_with("array_") or it.id.begins_with("crew_") or it.id.begins_with("room_") else it.id
		if seen.has(base): continue
		seen[base] = true
		if it.id in ["suitport", "drill_alarm", "ai_switch", "bed"]: print("AUDIT %-16s | %s | (skipped: changes game state)" % [it.id, g.label_for(it)]); continue
		var lab = g.label_for(it)
		g.msg = {}; close_card()
		g._on_click({"kind": "inter", "node": it})
		await get_tree().process_frame
		var out = ""
		if not card.is_empty(): out = "CARD " + card.get("title", "")
		elif not g.activity.is_empty(): out = "ACTIVITY " + g.activity.label
		elif not g.msg.is_empty(): out = "MSG " + g.msg.text.substr(0, 90)
		else: out = "NOTHING VISIBLE"
		print("AUDIT %-16s | %s | %s" % [it.id, lab, out])
		if not g.activity.is_empty(): g._end_activity(true)
		close_card()
		if g.state != "play": g.state = "play"
	get_tree().quit()

# =====================================================================================================
#  safety stations: a fire extinguisher + a breathing mask beside every room's hatch, as on the ISS
#  (water-mist extinguisher, ~15 min oxygen mask), a smoke detector on the ceiling. You can check them.
# =====================================================================================================
const SAFETY_ROOMS = ["communal", "greenhouse", "medical", "eclss", "work", "exercise", "quarters", "airlock", "storage", "hygiene"]

func _safety_stations() -> void:
	var H = sim.hab
	var used = []
	for room in SAFETY_ROOMS:
		if not D.ROOMS.has(room): continue
		var r = D.ROOMS[room].rect
		var best = null; var bd = 1e9
		for x in range(r[0], r[0] + r[2]):
			for y in range(r[1], r[1] + r[3]):
				var c = Vector2i(x, y)
				if not H.region.has(c) or H.blocked.has(c): continue
				if room == "quarters" and H.zone_of(c).begins_with("pod"): continue     # in the shared hall, not in someone's pod
				for dir in [Vector2i(0, -1), Vector2i(0, 1), Vector2i(-1, 0), Vector2i(1, 0)]:
					var out = c + dir
					var inside_rect = out.x >= r[0] and out.x < r[0] + r[2] and out.y >= r[1] and out.y < r[1] + r[3]
					if inside_rect: continue                      # we want a wall cell
					if H.region.has(out): continue                 # that's an opening, not a wall
					# near a hatch: a neighbour along the wall leads out of the room
					var side = Vector2i(dir.y, dir.x)
					var dd = 99.0
					for k in [1, 2, -1, -2]:
						var n = c + side * k
						var n_out = n + dir
						if H.region.has(n_out) and not (n_out.x >= r[0] and n_out.x < r[0] + r[2] and n_out.y >= r[1] and n_out.y < r[1] + r[3]): dd = min(dd, absf(k))
					var p = Habitat.cpos(c) + Vector3(dir.x, 0, dir.y) * (C * 0.5 - 0.1)
					var clash = false
					for it in hab.inters:
						if is_instance_valid(it.area) and Vector2(it.area.global_position.x - p.x, it.area.global_position.z - p.z).length() < 0.9: clash = true
					for sc in hab.screens:
						if Vector2(sc.global_position.x - p.x, sc.global_position.z - p.z).length() < 0.8: clash = true
					if clash: continue
					var score = dd + (0.0 if dd < 3 else 5.0)
					if score < bd: bd = score; best = [p, dir]
		if best == null: continue
		var p: Vector3 = best[0]; var dir: Vector2i = best[1]
		var n = Vector3(-dir.x, 0, -dir.y)                    # into the room
		var yaw = atan2(n.x, n.z)
		var st = Node3D.new(); st.position = p; st.rotation.y = yaw; world.add_child(st)
		_mesh(st, _boxm(Vector3(0.42, 0.62, 0.03)), Vector3(0, 1.05, 0.0), Mats.white_plastic())                 # backing plate
		_mesh(st, _cylm(0.07, 0.46), Vector3(-0.1, 0.98, 0.09), Mats.red())                                      # water-mist extinguisher
		_mesh(st, _cylm(0.02, 0.06), Vector3(-0.1, 1.24, 0.09), Mats.dark_metal())
		_mesh(st, _boxm(Vector3(0.05, 0.03, 0.03)), Vector3(-0.07, 1.27, 0.09), Mats.black())
		_mesh(st, _boxm(Vector3(0.16, 0.22, 0.1)), Vector3(0.11, 1.0, 0.07), Mats.caution())                      # breathing mask pouch
		_mesh(st, _boxm(Vector3(0.42, 0.05, 0.035)), Vector3(0, 1.39, 0.005), Mats.red())                         # red header strip
		var lab = _label("FIRE · MASK", Vector3.ZERO, 0.0, 16, Color(1, 1, 1))
		lab.reparent(st, false); lab.position = Vector3(0, 1.39, 0.03); lab.rotation = Vector3.ZERO
		_mesh(world, _cylm(0.06, 0.025), Vector3(p.x + n.x * 0.6, 2.4, p.z + n.z * 0.6), Mats.white_plastic())  # smoke detector
		hab.add_inter("safety", p + n * 0.12 + Vector3(0, 1.05, 0), Vector3(0.45, 0.65, 0.25), "", Callable(), room)
