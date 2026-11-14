extends Node3D
## LUNA VR: you are the Commander, one of four crew, living inside the habitat that LUNA runs.
## The LUNA engine (the same one as the 2D game) simulates everyone, including you; the 3D habitat,
## the screens and your choices are how you take part.

var sim: VRSim
var hab: Habitat
var player: Player
var avatars = []
var sun: DirectionalLight3D
var env: Environment
var hud_layer: CanvasLayer
var hud: Control
var hud_v: VBoxContainer
var popup_ctl: Control
var popup_scr: Screen3D
var wrist: Screen3D
var title_ctl: Control
var cross: Label
var prompt: Label
var fade: MeshInstance3D
var fade_mat: StandardMaterial3D
var scene_sphere: MeshInstance3D
var scene_mat: StandardMaterial3D
var ui = {}                       # screen id -> ScreenUI
var sfx = {}
var state = "title"               # title | play | sleep | report
var ff = false
var activity = {}                 # {"id","label","until","start","act","data","done": Callable}
var objectives = []
var obj_day = -1
var msg = {}                      # message being shown
var msg_t = 0.0
var seen_msgs = 0
var seen_public = 0
var asking = {}                   # LUNA offer currently on screen
var show_why = false
var voice = true
var voice_id = ""
var flags = {}
var outside = false
var eva_t = 0.0
var cleaning_t = 0.0
var refresh_t = 0.0
var twin_seen = 0.0
var report_ctl: Control
var stage_cache = {}
var parts_cache = -1
var athlete_t = 0.0
var hub_mode = "dining"
var time_mult = 1.0               # tests run the mission faster
# SELENE (the team's face + voice wellness app) as a live sensor for you
const SELENE_URL = "http://127.0.0.1:8765/status"
var selene_http: HTTPRequest
var selene_live = false
var selene_data = {}
var selene_t = 0.0
var selene_fail = 0
var selene_said_high = false
var twin_rooms = []               # [{"mat","key"}]
var twin_pucks = []
var twin_t = 0.0
var r3: R3                        # round 3: every object does something + the earlier HUNCH games' features

func _ready() -> void:
	randomize()
	# the built-in font has no ✓ ○ ★ → ₂: DejaVu Sans (free licence) fills those in, everywhere
	var dv = load("res://assets/fonts/DejaVuSans.ttf")
	if dv: ThemeDB.fallback_font.fallbacks = [dv]
	sim = VRSim.new()
	_environment()
	hab = Habitat.new(); hab.name = "Habitat"; add_child(hab)
	hab.build(sim)
	for i in range(1, 4):
		var a = Avatar.new(); a.setup(i); add_child(a); avatars.append(a)
	r3 = R3.new(); r3.name = "R3"; add_child(r3); r3.setup(self)
	player = Player.new(); player.name = "Player"; add_child(player)
	player.pointer_clicked.connect(_on_click)
	_spawn_in_pod()
	_fade_and_scene()
	_sounds()
	_build_screens()
	_build_hud()
	_build_popup()
	_build_title()
	_build_twin3d()
	selene_http = HTTPRequest.new(); selene_http.timeout = 2.0; add_child(selene_http)
	selene_http.request_completed.connect(_on_selene)
	if DisplayServer.tts_get_voices().size() > 0:
		var vs = DisplayServer.tts_get_voices_for_language("en")
		voice_id = vs[0] if vs.size() > 0 else DisplayServer.tts_get_voices()[0].id
	if player.try_openxr():
		_enter_vr_layout()
	set_fade(1.0)
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--shots="): _shots.call_deferred(arg.substr(8))
		if arg.begins_with("--autoplay="): _autoplay.call_deferred(arg.substr(11))
		if arg == "--selenetest": _selenetest.call_deferred()
		if arg == "--r3test": r3.selftest.call_deferred()
		if arg.begins_with("--r3shots="): r3.shots.call_deferred(arg.substr(10))

# =====================================================================================================
#  world: sky, Sun, Earth, ambient
# =====================================================================================================
func _environment() -> void:
	var we = WorldEnvironment.new(); env = Environment.new()
	var sky = Sky.new(); var pm = PanoramaSkyMaterial.new(); pm.panorama = Mats.tex("stars"); pm.energy_multiplier = 0.6
	sky.sky_material = pm
	env.background_mode = Environment.BG_SKY; env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.62, 0.66, 0.74); env.ambient_light_energy = 0.32
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC; env.tonemap_exposure = 1.0
	env.glow_enabled = true; env.glow_intensity = 0.5; env.glow_bloom = 0.05; env.glow_hdr_threshold = 1.2
	we.environment = env; add_child(we)
	sun = DirectionalLight3D.new(); sun.name = "Sun"
	sun.light_energy = 2.4; sun.light_color = Color(1.0, 0.97, 0.92)
	sun.shadow_enabled = true; sun.directional_shadow_max_distance = 120.0
	add_child(sun)
	sun.look_at_from_position(Vector3.ZERO, Habitat.SUN_DIR, Vector3.UP)
	sun.visible = false
	var earth = MeshInstance3D.new(); var sm = SphereMesh.new(); sm.radius = 40.0; sm.height = 80.0; earth.mesh = sm
	var em = StandardMaterial3D.new(); em.albedo_texture = Mats.tex("earth"); em.emission_enabled = true; em.emission_texture = Mats.tex("earth"); em.emission_energy_multiplier = 0.9
	earth.material_override = em
	earth.position = Vector3(18, 0, 10) + Habitat.EARTH_DIR.normalized() * 1200.0
	earth.rotation.z = 0.4
	add_child(earth)
	var sund = MeshInstance3D.new(); var sdm = SphereMesh.new(); sdm.radius = 9.0; sdm.height = 18.0; sund.mesh = sdm
	sund.material_override = Mats.glow(Color(1.0, 0.96, 0.85), 6.0, "sun_disk")
	sund.position = Vector3(18, 0, 10) - Habitat.SUN_DIR.normalized() * 1500.0 + Vector3(0, 30, 0)
	add_child(sund)

func _spawn_in_pod() -> void:
	player.place(Vector3(9.95, 0.05, 3.25), 0.0)

func _fade_and_scene() -> void:
	fade = MeshInstance3D.new(); var fm = SphereMesh.new(); fm.radius = 0.3; fm.height = 0.6; fm.radial_segments = 16; fm.rings = 8; fade.mesh = fm
	fade_mat = StandardMaterial3D.new(); fade_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	fade_mat.albedo_color = Color(0, 0, 0, 1); fade_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	fade_mat.cull_mode = BaseMaterial3D.CULL_FRONT; fade_mat.no_depth_test = true; fade_mat.render_priority = 100
	fade.material_override = fade_mat
	player.cam.add_child(fade)
	scene_sphere = MeshInstance3D.new(); var ss = SphereMesh.new(); ss.radius = 4.0; ss.height = 8.0; scene_sphere.mesh = ss
	scene_mat = StandardMaterial3D.new(); scene_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED; scene_mat.cull_mode = BaseMaterial3D.CULL_FRONT
	scene_mat.uv1_scale = Vector3(-1, 1, 1)
	scene_sphere.material_override = scene_mat; scene_sphere.visible = false
	add_child(scene_sphere)

func set_fade(a: float) -> void:
	fade_mat.albedo_color.a = a
	fade.visible = a > 0.01

func _sounds() -> void:
	for n in ["hum", "chime", "alarm", "alert", "click", "door", "vacuum", "harvest", "rain"]:
		var p = AudioStreamPlayer.new(); var s = load("res://assets/snd/%s.wav" % n)
		if n in ["hum", "rain"]:
			s.loop_mode = AudioStreamWAV.LOOP_FORWARD; s.loop_end = int(s.get_length() * s.mix_rate)
		p.stream = s; p.volume_db = -14.0 if n == "hum" else (-8.0 if n == "rain" else -4.0)
		add_child(p); sfx[n] = p

func play(n: String) -> void:
	if sfx.has(n): sfx[n].play()

func speak(text: String) -> void:
	if not voice or voice_id == "": return
	DisplayServer.tts_stop()
	DisplayServer.tts_speak(text, voice_id, 70, 1.0, 1.05)

# =====================================================================================================
#  title + VR
# =====================================================================================================
func _build_title() -> void:
	var r = UI.screen_root("LUNA VR", "Lunar Smart Adaptive Habitat · NASA HUNCH")
	title_ctl = r[0]; var b = r[1]
	b.add_child(UI.lbl("You are the Commander, one of four crew at the lunar south pole. LUNA, the habitat's AI, looks after all of you, including you. It changes the light and sound quietly, makes offers you can refuse, and calls a human when something is serious.", 22, UI.INK, true))
	b.add_child(UI.lbl("Three days. Eat with the crew, sleep well, keep the air clean, make the calls at the Ops console, and see how it feels to be looked after by an AI.", 20, UI.MUT, true))
	var row = UI.hbox(12)
	row.add_child(UI.btn("Start (mouse + keyboard)", _start_desktop, 0.58, 24))
	var vr_b = UI.btn("Enter VR", _start_vr, 0.75, 24, player.webxr_available() != null)
	row.add_child(vr_b)
	b.add_child(row)
	var help = "Desktop: WASD move · mouse look · click / E to use · F fast-forward · Tab free the mouse · Y / N answer LUNA\nVR: left stick move · right stick turn · trigger to use and click screens · Y button fast-forward"
	b.add_child(UI.lbl(help, 18, UI.MUT, true))
	b.add_child(UI.lbl("© 2026 Jaineil R. — with thanks to Angel C. and Farhan K.", 16, UI.MUT))
	if player.webxr_available() == null:
		b.add_child(UI.lbl("VR: open this page in the Meta Quest browser and press Enter VR.", 16, UI.WARN, true))
	title_ctl.custom_minimum_size = Vector2(860, 0)
	var cc = CenterContainer.new(); cc.set_anchors_preset(Control.PRESET_FULL_RECT); cc.add_child(title_ctl)
	cc.name = "title_center"
	hud_layer.add_child(cc)

func _start_desktop() -> void:
	title_ctl.get_parent().visible = false
	if title_ctl.get_parent() is CenterContainer: title_ctl.get_parent().queue_free()
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	_begin_mission()

func _start_vr() -> void:
	player.start_webxr(func():
		_enter_vr_layout()
		if state == "title": _begin_mission()
	, func(m): _toast("VR couldn't start: %s" % m))

func _enter_vr_layout() -> void:
	# the HUD moves onto your left wrist; LUNA's offers float in front of you
	if is_instance_valid(title_ctl) and title_ctl.get_parent() is CenterContainer:
		title_ctl.get_parent().queue_free()
	hud.get_parent().remove_child(hud)
	wrist = Screen3D.new(); wrist.setup("wrist", Vector2(0.22, 0.17), Vector2i(660, 510), null, true)
	player.left.add_child(wrist)
	wrist.position = Vector3(0.0, 0.06, 0.05); wrist.rotation_degrees = Vector3(-55, 0, 0)
	hud.set_anchors_preset(Control.PRESET_TOP_LEFT)
	wrist.set_ui(hud)
	popup_ctl.get_parent().remove_child(popup_ctl)
	popup_scr = Screen3D.new(); popup_scr.setup("popup", Vector2(0.9, 0.56), Vector2i(900, 560), null, true)
	add_child(popup_scr)
	popup_ctl.set_anchors_preset(Control.PRESET_TOP_LEFT)
	popup_scr.set_ui(popup_ctl)
	popup_scr.visible = false
	cross.visible = false; prompt.visible = false
	if state == "title": _begin_mission()

func vr_button(n: String) -> void:
	if n in ["left_by_button", "left_menu_button"]: ff = not ff
	elif n == "right_by_button" and not asking.is_empty(): _answer(false)
	elif n == "right_ax_button" and not asking.is_empty(): _answer(true)

func ui_open() -> bool: return state != "play" or not asking.is_empty()

func _begin_mission() -> void:
	hud.visible = true
	state = "sleep"
	sfx.hum.play()
	flags["intro"] = true

# =====================================================================================================
#  main loop
# =====================================================================================================
func _process(dt: float) -> void:
	if state == "title" or state == "report":
		hab.sync(dt, [player.global_position])
		return
	var scale = 1.0
	if state == "sleep": scale = 120.0
	elif not activity.is_empty(): scale = 8.0
	elif ff: scale = 8.0
	if state == "play" and (not sim.pending.is_empty() or sim.hab.flare_until > sim.now) and scale > 2.0 and activity.is_empty(): scale = 2.0
	if state == "play" and not asking.is_empty(): scale = min(scale, 1.0)
	# where you are, for the engine
	if outside:
		sim.player_outside = true; sim.player_cell = Vector2i(0, 4)
	else:
		sim.player_outside = false
		var c = Habitat.to_cell(player.global_position)
		if sim.hab.region.has(c): sim.player_cell = c
	var was_asleep = sim.me().asleep
	if state == "sleep":
		# minute by minute, so you wake exactly when the smart alarm fires (not hours later in a big batch)
		sim.acc += dt * scale * time_mult
		var steps = 0
		while sim.acc >= 1.0 and steps < 600 and not sim.done:
			sim.acc -= 1.0; steps += 1
			sim._minute()
			if was_asleep and not sim.me().asleep: break
		if was_asleep and not sim.me().asleep: sim.acc = 0.0; _wake_up()
	else:
		sim.advance(dt * scale * time_mult)
	if state == "sleep" and not was_asleep and not sim.me().asleep and flags.get("intro", false):
		sim.player_sleep()       # very first night: you start asleep
	_update_activity(dt)
	_selene_tick(dt)
	_sync_world(dt)
	_messages(dt)
	refresh_t -= dt
	if refresh_t <= 0.0:
		refresh_t = 0.4
		for k in ui: ui[k].refresh()
		_objectives()
		_refresh_hud()
	_prompt()
	r3.tick(dt)
	var hs = player.hit
	if hs.get("kind", "") == "screen":
		if hs.node.id == "twin": flags["twin"] = true
		if hs.node.id == "pod_desk": flags["sleep_read"] = sim.day()
	if sim.done and state == "play": _show_report()

func _sync_world(dt: float) -> void:
	var actors = [player.global_position]
	for a in avatars:
		var cr: Crew = sim.crew[a.i]
		var p2 = cr.prev.lerp(cr.pos, clamp(sim.acc, 0.0, 1.0))
		var wp = Vector3(p2.x / 16.0 * Habitat.CELL, 0, p2.y / 16.0 * Habitat.CELL)
		a.pose(cr, wp, dt)
		actors.append(wp)
	hab.sync(dt, actors)
	var H = sim.hab
	# outside: the Sun only lights the world when you are out there (no light leaks into the cabin)
	sun.visible = outside
	env.ambient_light_energy = 0.12 if outside else 0.32
	# solar arrays gather real dust from the engine's dust model
	if hab.h_solar_mat: hab.h_solar_mat.albedo_color = Color(1, 1, 1).lerp(Color(0.62, 0.58, 0.52), H.dust_panels)
	# airlock dust + the electrostatic curtain during a cleaning cycle
	var dp: CPUParticles3D = hab.dyn["dust_particles"]
	dp.emitting = H.airlock_dust > 0.15 or cleaning_t > 0.0
	dp.gravity = Vector3(0, -1.5 if cleaning_t > 0.0 else -0.05, 0)
	var cm: StandardMaterial3D = hab.dyn["curtain_mat"]
	cm.emission_energy_multiplier = lerp(cm.emission_energy_multiplier, 3.0 if cleaning_t > 0.0 else 0.35, min(1.0, dt * 3.0))
	cleaning_t = max(0.0, cleaning_t - dt)
	# crops grow (rebuild a rack only when its stage changes)
	for r in hab.dyn["racks"]:
		var rack = H.racks[r.k]
		var stg = H.rack_stage(rack)
		if stg != r.stage:
			r.stage = stg; hab.models.rack_plants(r, rack)
		if stg < 0: r.label.text = "Expansion slot · not planted"
		else:
			var left_d = max(0.0, H.CROPS[rack.crop] - rack.age)
			r.label.text = "%s · %d%%%s" % [rack.crop.capitalize(), int(rack.health * 100.0), " · ripe!" if H.ripe(rack) else " · %.0f d" % left_d]
	if H.parts != parts_cache:
		parts_cache = H.parts; hab.models.parts_boxes(H.parts)
	# hub: partition (Floor plan B) and table height (movie mode)
	var part: Node3D = hab.dyn["partition"]
	part.position.y = move_toward(part.position.y, 0.0 if H.config_b else 2.6, dt * 0.6)
	var movie = sim.movie_at > 0.0 and sim.now >= sim.movie_at - 20.0 and sim.now < sim.movie_at + 120.0
	var tbl: Node3D = hab.dyn["hub_table"]
	tbl.position.y = move_toward(tbl.position.y, -0.29 if (movie or hub_mode == "movie") else 0.0, dt * 0.3)
	(hab.dyn["hub_screen_mat"] as StandardMaterial3D).albedo_texture = Mats.tex("scene_ocean" if (movie or hub_mode == "movie") else "scene_earth")
	var riding = activity.get("id", "") == "bike" and activity.get("data", {}).has("offer")
	(hab.dyn["ex_screen_mat"] as StandardMaterial3D).albedo_texture = Mats.tex("scene_ocean" if riding else "scene_earth")
	if riding: (hab.dyn["ex_screen_mat"] as StandardMaterial3D).uv1_offset.x += dt * 0.02
	# maintenance robot out on a job
	(hab.dyn["robot"] as Node3D).visible = H.robot_task.is_empty() or H.robot_task.what == "dust"
	var led: StandardMaterial3D = hab.dyn["scrubber_led"]
	var lc = Color(0.2, 1, 0.4) if H.equip.scrubber > 0.6 else Color(1, 0.25, 0.15)
	led.albedo_color = lc; led.emission = lc
	# six-legged robot walks the arrays when it is cleaning them
	var ath: Node3D = hab.dyn["athlete"]
	if not H.robot_task.is_empty() and H.robot_task.what == "dust" and sim.now >= H.robot_task.until - 240.0:
		athlete_t += dt * 0.05
		var k = int(athlete_t) % 3; var nk = (k + 1) % 3
		var a0: Vector3 = hab.dyn["array_%d" % k] + Vector3(0, 0, 3.5); var a1: Vector3 = hab.dyn["array_%d" % nk] + Vector3(0, 0, 3.5)
		ath.position = a0.lerp(a1, fmod(athlete_t, 1.0))
	# calibration wall tiles: each lit with its zone's live colour temperature
	for t in hab.dyn["calib_tiles"]:
		var L = H.light.get(t.zone, H.light.get("communal", null)) if t.zone != "communal" else H.light.get("communal", H.light.get("communal_W", null))
		if L == null: continue
		t.mat.emission = Mats.cct(L.cct); t.mat.albedo_color = Mats.cct(L.cct)
		t.mat.emission_energy_multiplier = hab.lux_energy(L.lux) * 1.4
		t.label.text = "%s\n%d K · %d lx" % [t.zone, int(L.cct), int(L.lux)]
	if scene_sphere.visible: scene_sphere.global_position = player.cam.global_position
	_update_twin3d(dt)

# =====================================================================================================
#  sleeping and waking (the smart bed is the engine's: it waits for light sleep)
# =====================================================================================================
func go_to_bed(nap := false) -> void:
	if OS.get_cmdline_user_args().size() > 0: print("BED at ", sim.clock(), " nap=", nap)
	_end_activity(false)
	flags["napping"] = nap
	if nap: sim.player_nap(25.0)
	else: sim.player_sleep()
	state = "sleep"
	player.frozen = true
	var t = create_tween(); t.tween_method(set_fade, 0.0, 1.0 if player.vr else 0.62, 2.5)
	r3.on_sleep()
	speak("Good night.")

func _wake_up() -> void:
	if OS.get_cmdline_user_args().size() > 0: print("WAKE at ", sim.clock(), " napping=", flags.get("napping", false), " act=", sim.player_act)
	state = "play"
	flags.erase("intro")
	player.frozen = false
	player.place(Vector3(9.95, 0.05, 3.25), 0.0)
	r3.on_wake()
	var cr = sim.me()
	play("alarm")
	var t = create_tween(); t.tween_method(set_fade, 1.0, 0.0, 3.0)
	var n = cr.night
	var how = "light sleep, so the alarm felt easy" if not cr.woke_deep else "deep sleep (the alarm window ran out)"
	if not flags.get("napping", false):
		flags["last_night"] = n.duplicate(); flags["last_hyp"] = cr.hyp.duplicate()
	if not flags.get("napping", false):
		sim.say("Good morning. You slept %.1f hours with %d wake-ups, and I woke you from %s. The pod light rose like a sunrise first. Details on your desk screen." % [n.hours, n.wakes, how])
	else:
		sim.say("Nap over. You should feel a little sharper now.")
	sim.player_act = "idle"

# =====================================================================================================
#  activities: sit, eat, tend, exercise… the engine applies the effects; time runs faster meanwhile
# =====================================================================================================
func start_activity(id: String, label: String, minutes: float, act: String, data := {}, freeze := true, done: Callable = Callable()) -> void:
	_end_activity(false)
	activity = {"id": id, "label": label, "until": sim.now + minutes, "start": sim.now, "act": act, "data": data, "done": done, "pos": player.global_position}
	sim.set_player_act(act, minutes + 1.0, data)
	player.frozen = freeze
	play("click")

func _update_activity(dt: float) -> void:
	if activity.is_empty(): return
	var moved = false
	if not player.vr:
		for k in [KEY_W, KEY_A, KEY_S, KEY_D]: if Input.is_key_pressed(k): moved = true
	else:
		if player.left.get_vector2("primary").length() > 0.6 or player.left.get_vector2("thumbstick").length() > 0.6: moved = true
	if moved and sim.now - activity.start > 1.0: _end_activity(false); return
	if sim.now >= activity.until: _end_activity(true)

func _end_activity(finished: bool) -> void:
	if activity.is_empty(): return
	var a = activity
	activity = {}
	player.frozen = false
	if not player.vr: player.cam.position.y = Player.EYE
	scene_sphere.visible = false
	sfx.rain.stop()
	sim.set_player_act("idle", 0.0)
	if finished and a.done.is_valid(): a.done.call()
	elif not finished: _toast("Stopped: %s" % a.label)

# =====================================================================================================
#  interactions
# =====================================================================================================
func _meal_time() -> bool:
	var h = sim.hour()
	return (h >= 7.0 and h < 8.25) or (h >= 12.0 and h < 13.25) or (h >= 18.5 and h < 19.75)

func label_for(it: Dictionary) -> String:
	var l3 = r3.label(it)
	if l3 != "": return l3
	var id: String = it.id
	var H = sim.hab
	if id.begins_with("rack_"):
		var r = H.racks[int(id.substr(5))]
		if not r.built: return "Expansion rack (not planted)"
		return ("Harvest the %s" if H.ripe(r) else "Tend the %s (%d%%)") % ([r.crop] if H.ripe(r) else [r.crop, int(r.health * 100)])
	if id.begins_with("array_"):
		return "Brush dust off this array (%d%% dusty)" % int(H.dust_panels * 100.0) if outside else "Solar array"
	match id:
		"bed":
			if sim.hour() >= 20.5 or sim.hour() < 5.0: return "Go to sleep (smart alarm: light sleep, 06:00–06:30)"
			if not sim.player_offer.is_empty() and sim.player_offer.id == "nap": return "Take LUNA's 25-minute nap"
			return "Lie down and rest (20 min)"
		"cabinet": return "Tend your hydroponics cabinet"
		"seat": return "Eat with the crew" if _meal_time() else "Sit at the table (talk, play cards)"
		"sofa": return "Movie night on the sofa" if sim.movie_at > 0.0 and absf(sim.now - sim.movie_at - 30.0) < 90.0 else "Relax on the sofa"
		"galley": return "Snack and a drink from the galley"
		"gh_bench": return "Sit among the plants"
		"desk": return "Work at the Ops desk (45 min)"
		"treadmill": return "Run on the treadmill (30 min)"
		"bike": return "Scenic ride on the bike (LUNA's offer)" if (not sim.player_offer.is_empty() and sim.player_offer.id == "vrride") else "Ride the cycle ergometer (30 min)"
		"rig": return "Resistance training (30 min)"
		"recovery_chair": return "Sit in the VR recovery chair"
		"sink": return "Wash up"
		"suitport": return "Climb into your suit through the suitport (EVA)" if g_eva_ready() else "Suitport (run the EVA prep check first)"
		"earth_window": return "Sit by the Earth-view display (live south-pole camera, 20 min)"
		"dock": return "Wellness Dock: posture, heart rate, Reaction Self Test"
		"suitport_return": return "Back in through the suitport"
		"boot_brush": return "Brush the dust off your boots"
		"cartridge": return "Swap the sorbent cartridge (1 spare part)" if sim.fired.get("swap_task", false) else "Sorbent cartridge (OK)"
	return id

func _on_click(hit: Dictionary) -> void:
	if state != "play": return
	if hit.get("kind", "") != "inter": return
	var it = hit.node
	var id: String = it.id
	if r3.use(it): return
	var H = sim.hab
	if id.begins_with("rack_"):
		var k = int(id.substr(5))
		if not H.racks[k].built: _toast("This rack isn't planted yet."); return
		start_activity(id, "Tending the greenhouse", 12.0, "greenhouse", {}, true, func():
			var res = sim.tend_rack(k)
			if res.begins_with("Harvested"): play("harvest")
			_toast(res)
			if sim.complete_player_offer("greenhouse"): _offer_effect("greenhouse")
			flags["tended_rack"] = sim.day())
		return
	if id.begins_with("array_"):
		if not outside: return
		start_activity(id, "Brushing the array", 6.0, "eva", {}, true, func():
			sim.clean_array(0.36); _toast("Brushed. Arrays %d%% dusty." % int(H.dust_panels * 100.0)))
		return
	match id:
		"bed":
			if sim.hour() >= 20.5 or sim.hour() < 5.0: go_to_bed(false)
			elif not sim.player_offer.is_empty() and sim.player_offer.id == "nap":
				sim.complete_player_offer("nap"); go_to_bed(true)
			else: start_activity("rest", "Resting in your pod", 20.0, "pod")
		"cabinet":
			start_activity("cabinet", "Tending your plants", 10.0, "greenhouse", {"fx": {"mood": 0.06}}, true, func():
				_toast("Basil and strawberries tended. Your own small garden."); flags["cabinet"] = sim.day())
		"seat":
			if _meal_time():
				start_activity("meal", "Eating with the crew", 30.0, "meal", {}, true, func():
					flags["meal_%d" % sim.day()] = flags.get("meal_%d" % sim.day(), 0) + 1; sim.stats_vr.meals_together += 1)
			else: start_activity("table", "Sitting at the table", 20.0, "lounge")
			if not player.vr: player.cam.position.y = 1.15
		"sofa":
			var d = {}
			start_activity("sofa", "Relaxing on the sofa", 30.0, "movie" if sim.movie_at > 0.0 and absf(sim.now - sim.movie_at - 30.0) < 90.0 else "lounge", d, true, func():
				if sim.complete_player_offer("lounge"): _offer_effect("lounge"))
			if not player.vr: player.cam.position.y = 1.1
		"galley":
			start_activity("snack", "Snack break", 8.0, "meal", {"fx": {"mood": 0.03}})
		"gh_bench":
			start_activity("bench", "Sitting among the plants", 20.0, "greenhouse", {"fx": {"stress": -0.15}})
		"desk":
			start_activity("work", "Working at the Ops desk", 45.0, "work")
		"treadmill", "rig":
			start_activity("ex", "Exercising", 30.0, "exercise", {}, true, func(): flags["exercise_%d" % sim.day()] = true)
		"bike":
			var data = {}
			var vr_offer = not sim.player_offer.is_empty() and sim.player_offer.id == "vrride"
			if vr_offer: data = {"fx": sim.player_offer.fx, "offer": "vrride"}
			start_activity("bike", "Scenic ride" if vr_offer else "Cycling", 30.0, "exercise", data, true, func():
				flags["exercise_%d" % sim.day()] = true
				if sim.complete_player_offer("vrride"): _offer_effect("vrride"))
		"recovery_chair": _recovery_chair()
		"sink": start_activity("wash", "Washing up", 8.0, "hygiene", {"fx": {"stress": -0.05}})
		"suitport":
			if g_eva_ready(): _eva_out()
			else: _toast("Run the pre-EVA check first: the EVA prep panel between the suitports (O₂, cooling water, readiness).")
		"earth_window":
			start_activity("earth", "By the Earth window", 20.0, "lounge", {"fx": {"connection": 0.2, "novelty": 0.15, "stress": -0.1}}, true, func():
				flags["earth_%d" % sim.day()] = true; _toast("Earth turned a little while you watched. You feel less far away."))
			if not player.vr: player.cam.position.y = 1.15
		"dock": _toast("Step on the dock and use the screen in front of you: scan, then the Reaction Self Test.")
		"suitport_return": _eva_in()
		"boot_brush":
			H.airlock_dust = max(0.0, H.airlock_dust - 0.3); _toast("Boots brushed: less dust carried past the curtain.")
		"cartridge":
			if sim.fired.get("swap_task", false):
				start_activity("swap", "Swapping the cartridge", 15.0, "work", {}, true, func():
					if sim.swap_cartridge(): _toast("Cartridge swapped. The scrubber is back to 97%. CO₂ will fall over the next hour.")
					else: _toast("No spare parts left."))
			else: _toast("The scrubber is running normally.")

func _recovery_chair() -> void:
	# LUNA suggests a scene from your stress level; you can pick another or just sit
	var stress = sim.luna.est[0][0]
	var sc = "forest" if stress > 0.45 else ("ocean" if sim.luna.est[0][5] > 0.4 else "earth")
	var offer_id = ""
	if not sim.player_offer.is_empty() and sim.player_offer.id in ["breathing", "vrride", "chat"]: offer_id = sim.player_offer.id
	var data = {"fx": {"stress": -0.35, "novelty": 0.15}}
	if offer_id != "": data = {"fx": sim.player_offer.fx, "offer": offer_id}
	start_activity("chair", "Recovery chair: %s scene" % sc, 15.0, "offer" if offer_id != "" else "lounge", data, true, func():
		if offer_id != "" and sim.complete_player_offer(offer_id): _offer_effect(offer_id)
		_toast("Session done. LUNA logged it as a break you chose."))
	scene_mat.albedo_texture = Mats.tex("scene_" + sc)
	scene_sphere.visible = true
	sfx.rain.play()
	sim.say("I picked the %s scene because your stress reading is %.2f. Breathe in as the light rises, out as it falls." % [sc, stress])

func _offer_effect(id: String) -> void:
	_toast("Done: %s. LUNA will watch whether it helped (monitoring window)." % D.OFFERS[id].name)

# =====================================================================================================
#  EVA: suitport out, clean the arrays, suitport in (dust comes with you: clean it in the airlock)
# =====================================================================================================
func _eva_out() -> void:
	if sim.hab.flare_until > sim.now: _toast("No EVA during a solar particle event."); return
	if sim.hour() >= 19.0 or sim.hour() < 7.0: _toast("EVAs are planned for daytime shifts (07:00–19:00)."); return
	var t = create_tween(); t.tween_method(set_fade, 0.0, 1.0, 0.8)
	t.tween_callback(func():
		outside = true; sim.stats_vr.eva += 1; eva_t = sim.now
		sim.set_player_act("eva", 0.0)
		player.place(Vector3(3.1, 0.2, -1.6), 0.0)
		if not hab.inters.any(func(i): return i.id == "suitport_return"):
			hab.add_inter("suitport_return", Vector3(3.1, 1.2, 0.2), Vector3(3.6, 1.8, 0.6), "", Callable(), "outside")
		sim.public("You started an EVA through the suitport.")
		sim.say("Suit check good: oxygen 8 hours, cooling on. The arrays are to the north-west. Brush them with the pole from the base of each tower."))
	t.tween_method(set_fade, 1.0, 0.0, 1.2)

func _eva_in() -> void:
	var t = create_tween(); t.tween_method(set_fade, 0.0, 1.0, 0.8)
	t.tween_callback(func():
		outside = false
		sim.set_player_act("idle", 0.0)
		player.place(Vector3(3.1, 0.05, 2.4), PI)
		sim.hab.airlock_dust += 1.0
		sim.say("Welcome back. Some dust came in around the suitport seal. Run the cleaning cycle on the airlock panel before you go further."))
	t.tween_method(set_fade, 1.0, 0.0, 1.2)

func airlock_cycle() -> void:
	if outside: return
	cleaning_t = 4.0
	play("vacuum")
	var before = sim.airlock_clean()
	_toast("Cleaning cycle: electrostatic curtain on, HEPA vacuum running. Dust load %.1f → %.1f." % [before, sim.hab.airlock_dust])
	flags["cleaned_airlock"] = true

# =====================================================================================================
#  LUNA talking to you: messages, offers you can refuse, and why it is asking
# =====================================================================================================
func _messages(dt: float) -> void:
	# new LUNA messages
	while seen_msgs < sim.messages.size():
		var m = sim.messages[seen_msgs]; seen_msgs += 1
		_toast(m.text, m.kind)
	# LUNA asks you something
	if asking.is_empty() and state == "play" and r3.card.is_empty():
		var a = sim.next_ask()
		if not a.is_empty() and activity.is_empty():
			asking = a; show_why = false
			_show_popup()
			play("chime")
			speak(D.OFFERS[a.offer].say)
	if msg_t > 0.0:
		msg_t -= dt
		if msg_t <= 0.0 and asking.is_empty(): _hide_popup()

func _toast(text: String, kind := "luna") -> void:
	msg = {"text": text, "kind": kind}
	msg_t = 7.0 if text.length() < 120 else 10.0
	if kind in ["alert", "t3"]: play("alert")
	elif kind == "luna": play("chime")
	if kind != "info" and state == "play": speak(text)
	if asking.is_empty(): _show_popup()

func _answer(yes: bool) -> void:
	if asking.is_empty(): return
	r3.on_answer(yes)
	var a = asking
	asking = {}
	sim.player_reply(a, yes)
	if yes and D.OFFERS[a.offer].where == "here":
		# "here" offers happen where you stand: a guided breathing circle in front of you
		if a.offer == "breathing":
			start_activity("breath", "Guided breathing", 6.0, "offer", {"fx": sim.player_offer.get("fx", {}), "offer": "breathing"}, true, func():
				sim.complete_player_offer("breathing"); _offer_effect("breathing"))
		elif a.offer == "chat":
			sim.complete_player_offer("chat")
			sim.say("Thanks for talking. You've had a heavy day: the lander-plume dust alert, a lot on your plate. That's a normal reaction, not a failure. Want me to keep the hub warm tonight?")
	_hide_popup()

func _build_popup() -> void:
	popup_ctl = PanelContainer.new()
	popup_ctl.theme = UI.get_theme()
	var sb = StyleBoxFlat.new(); sb.bg_color = Color(0.04, 0.08, 0.2, 0.94); sb.set_corner_radius_all(10); sb.border_color = UI.ACC; sb.set_border_width_all(2)
	sb.content_margin_left = 20; sb.content_margin_right = 20; sb.content_margin_top = 14; sb.content_margin_bottom = 14
	popup_ctl.add_theme_stylebox_override("panel", sb)
	popup_ctl.custom_minimum_size = Vector2(760, 0)
	popup_ctl.visible = false
	hud_layer.add_child(popup_ctl)
	popup_ctl.set_anchors_preset(Control.PRESET_CENTER_BOTTOM)
	popup_ctl.position = Vector2(260, 470)

func _show_popup() -> void:
	UI.clear(popup_ctl)
	var v = UI.vbox(10); popup_ctl.add_child(v)
	if not asking.is_empty():
		var o = D.OFFERS[asking.offer]
		v.add_child(UI.lbl("LUNA · an offer, not an order" if asking.get("kind", "") != "forecast" else "LUNA · looking ahead", 20, UI.ACC))
		var txt = o.say
		if asking.get("kind", "") == "forecast":
			txt = "Your %s readings have been creeping up for a few days. If that continues you could reach a Tier 2 level within %d days. %s" % [D.CONDS[asking.cond].to_lower(), int(asking.get("eta", 3)), o.say]
		v.add_child(UI.lbl(txt, 26, UI.INK, true))
		var row = UI.hbox(12)
		row.add_child(UI.btn("Yes, please  [Y]", func(): _answer(true), 0.4, 24))
		row.add_child(UI.btn("No thanks  [N]", func(): _answer(false), 0.02, 24))
		row.add_child(UI.btn("Why are you asking?  [H]", func(): show_why = not show_why; _show_popup(), 0.6, 22))
		v.add_child(row)
		if show_why:
			v.add_child(UI.lbl("What LUNA sees (only you get this list):", 18, UI.MUT))
			for e in asking.evidence:
				var t = "%s: %s" % [e[0], ("%.2f" % e[1]) if e[1] >= 0.0 else e[3]]
				v.add_child(UI.lbl("· " + t + "   (weight %.2f)" % e[2], 18, UI.INK))
			v.add_child(UI.lbl("Confidence %d%%. Saying no starts a 6-hour cool-down; LUNA never overrides a refusal." % int(asking.conf * 100.0), 18, UI.MUT, true))
	elif not r3.card.is_empty():
		r3.build_card(v)
		if not msg.is_empty(): v.add_child(UI.lbl(msg.text, 16, UI.MUT, true))
	elif not msg.is_empty():
		var head = {"alert": "ALERT", "t3": "LUNA · human support", "info": "Habitat", "luna": "LUNA"}.get(msg.kind, "LUNA")
		v.add_child(UI.lbl(head, 20, UI.BAD if msg.kind == "alert" else UI.ACC))
		v.add_child(UI.lbl(msg.text, 24, UI.INK, true))
	popup_ctl.visible = true
	if popup_scr:
		popup_scr.visible = true
		popup_scr.request(4)
		popup_ctl.size = Vector2(900, 560); popup_ctl.custom_minimum_size = Vector2(900, 560)
		var f = -player.cam.global_transform.basis.z; f.y = 0; f = f.normalized()
		popup_scr.global_position = player.cam.global_position + f * 1.1 + Vector3(0, -0.15, 0)
		popup_scr.look_at(player.cam.global_position, Vector3.UP); popup_scr.rotate_object_local(Vector3.UP, PI)
	elif not asking.is_empty():
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	if not popup_scr: r3.fit_popup()

func _hide_popup() -> void:
	if not r3.card.is_empty() and state == "play":
		msg = {}; _show_popup(); return
	popup_ctl.visible = false
	if popup_scr: popup_scr.visible = false
	msg = {}
	if not player.vr and state == "play" and Input.mouse_mode == Input.MOUSE_MODE_VISIBLE and asking.is_empty():
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

func _unhandled_key_input(e: InputEvent) -> void:
	if not (e is InputEventKey and e.pressed and not e.echo): return
	if r3.key(e.keycode): return
	match e.keycode:
		KEY_Y: if not asking.is_empty(): _answer(true)
		KEY_N: if not asking.is_empty(): _answer(false)
		KEY_H: if not asking.is_empty(): show_why = not show_why; _show_popup()
		KEY_F: ff = not ff
		KEY_E: player._click()
		KEY_TAB, KEY_ESCAPE:
			Input.mouse_mode = Input.MOUSE_MODE_VISIBLE if Input.mouse_mode == Input.MOUSE_MODE_CAPTURED else Input.MOUSE_MODE_CAPTURED
		KEY_V: voice = not voice; _toast("LUNA voice %s." % ("on" if voice else "off"), "info")

# =====================================================================================================
#  HUD (desktop overlay; in VR it lives on your left wrist)
# =====================================================================================================
func _build_hud() -> void:
	hud_layer = CanvasLayer.new(); add_child(hud_layer)
	hud = PanelContainer.new(); hud.theme = UI.get_theme()
	var sb = StyleBoxFlat.new(); sb.bg_color = Color(0.03, 0.06, 0.16, 0.82); sb.set_corner_radius_all(8)
	sb.content_margin_left = 14; sb.content_margin_right = 14; sb.content_margin_top = 10; sb.content_margin_bottom = 10
	hud.add_theme_stylebox_override("panel", sb)
	hud_v = UI.vbox(4); hud.add_child(hud_v)
	hud.custom_minimum_size = Vector2(360, 0)
	hud.position = Vector2(14, 14)
	hud.visible = false
	hud_layer.add_child(hud)
	cross = Label.new(); cross.text = "+"; cross.add_theme_font_size_override("font_size", 26)
	cross.set_anchors_preset(Control.PRESET_CENTER); cross.position -= Vector2(7, 18)
	hud_layer.add_child(cross)
	prompt = UI.lbl("", 22, Color.WHITE); prompt.set_anchors_preset(Control.PRESET_CENTER)
	prompt.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER; prompt.position += Vector2(-300, 24); prompt.custom_minimum_size = Vector2(600, 0)
	prompt.add_theme_color_override("font_outline_color", Color.BLACK); prompt.add_theme_constant_override("outline_size", 6)
	hud_layer.add_child(prompt)

func _refresh_hud() -> void:
	if wrist: wrist.request(2)
	UI.clear(hud_v)
	var spd = "fast-forward ▶▶" if ff else ("time-lapse" if not activity.is_empty() else "")
	if state == "sleep": spd = "asleep…"
	hud_v.add_child(UI.lbl("Day %d of 3 · %s  %s" % [sim.day(), sim.clock(), spd], 24, UI.ACC))
	if sim.luna.off: hud_v.add_child(UI.lbl("LUNA OFF · a human switches it back on (Ops, or O)", 18, UI.BAD, true))
	if outside:
		hud_v.add_child(UI.lbl("EVA · suit O₂ %.1f h · arrays %d%% dusty" % [8.0 - (sim.now - eva_t) / 60.0, int(sim.hab.dust_panels * 100.0)], 18, UI.WARN))
	if not activity.is_empty():
		var f = clamp((sim.now - activity.start) / max(1.0, activity.until - activity.start), 0.0, 1.0)
		hud_v.add_child(UI.lbl(activity.label + "  (move to stop)", 18, UI.INK))
		hud_v.add_child(UI.bar(f, UI.OK, 320, 10))
	for o in objectives:
		hud_v.add_child(UI.lbl(("✓ " if o.done else "○ ") + o.text, 17, UI.MUT if o.done else UI.INK, true))
	var wb = sim.my_wellbeing()
	var row = UI.hbox(6)
	row.add_child(UI.lbl("LUNA's read on you", 16, UI.MUT)); row.add_child(UI.bar(wb, UI.OK.lerp(UI.BAD, 1.0 - wb), 120, 10))
	row.add_child(UI.lbl("only you see this", 14, UI.MUT))
	hud_v.add_child(row)
	if player.vr:
		var r2 = UI.hbox(6)
		r2.add_child(UI.btn("Fast-forward" if not ff else "Normal speed", func(): ff = not ff, 0.12, 18))
		r2.add_child(UI.btn("Voice " + ("on" if voice else "off"), func(): voice = not voice, 0.6, 18))
		hud_v.add_child(r2)
		var r3b = UI.hbox(6)
		r3b.add_child(UI.btn("Guide me", func(): r3.guide(false), 0.12, 18))
		r3b.add_child(UI.btn("Rubric", func(): r3.show_rubric(), 0.6, 18))
		r3b.add_child(UI.btn("AI OFF" if not sim.luna.off else "LUNA on", func(): r3.use({"id": "ai_switch"}), 0.0, 18))
		r3b.add_child(UI.btn("Music " + ("on" if r3.music_on else "off"), func(): r3.music_on = not r3.music_on, 0.75, 18))
		hud_v.add_child(r3b)
	else:
		hud_v.add_child(UI.lbl("G guide me · I ask a crewmate · M music · J rubric · K keys", 14, UI.MUT))

func _prompt() -> void:
	if player.vr:
		return
	var hit = player.hit
	cross.modulate = Color(0.5, 0.9, 1.0) if not hit.is_empty() else Color(1, 1, 1, 0.6)
	if hit.get("kind", "") == "inter" and state == "play":
		prompt.text = "[E / click]  " + label_for(hit.node) + r3.spec_line(hit.node)
	elif hit.get("kind", "") == "screen": prompt.text = ""
	else: prompt.text = ""

# =====================================================================================================
#  the day's objectives (a checklist, not a script: the engine keeps running whatever you do)
# =====================================================================================================
func _objectives() -> void:
	var d = sim.day()
	var list = []
	var ck = sim.my_checkins.any(func(c): return c.day == d)
	list.append({"text": "Check in at the mood kiosk (quarters hall)", "done": ck})
	list.append({"text": "Eat a meal with the crew (social hub)", "done": flags.get("meal_%d" % d, 0) > 0})
	if d == 1:
		list.append({"text": "Look at the digital twin in Ops", "done": flags.get("twin", false)})
		list.append({"text": "Tend your hydroponics cabinet (your pod)", "done": flags.get("cabinet", -1) == d})
	if d == 2:
		list.append({"text": "Read last night's sleep on your pod desk", "done": flags.get("sleep_read", -1) == d})
		list.append({"text": "Tend a greenhouse rack", "done": flags.get("tended_rack", -1) == d})
	if d == 3:
		list.append({"text": "Reaction Self Test at the Wellness Dock (exercise bay)", "done": flags.has("dock_3")})
		list.append({"text": "Sit by the Earth window (social hub)", "done": flags.get("earth_3", false)})
	list.append({"text": "Exercise 30 min (exercise bay)", "done": flags.get("exercise_%d" % d, false)})
	for e in sim.pending:
		list.append({"text": "Decide: %s (%s)" % [e.title, "social hub panel" if e.get("where", "ops") == "hub" else "Ops console"], "done": false})
	if sim.fired.get("swap_task", false): list.append({"text": "Swap the CO₂ cartridge (life support)", "done": false})
	if sim.fired.get("eva_task", false): list.append({"text": "EVA: brush the solar arrays (suitport, airlock)", "done": false})
	if sim.hab.airlock_dust > 0.6 and not outside: list.append({"text": "Run the airlock cleaning cycle", "done": false})
	if sim.hab.flare_until > sim.now: list.append({"text": "SHELTER in Medical now!", "done": sim.hab.zone_of(sim.player_cell) == "medical" and not outside})
	if not sim.player_offer.is_empty(): list.append({"text": "LUNA offer you accepted: %s" % D.OFFERS[sim.player_offer.id].name, "done": false})
	if d == 3: list.append({"text": "Final dinner, then the mission report at 20:30", "done": false})
	else: list.append({"text": "Go to bed in your pod after 21:00", "done": false})
	r3.objectives(list)
	objectives = list

# =====================================================================================================
#  the end: what happened, what LUNA did, what you decided
# =====================================================================================================
func _show_report() -> void:
	state = "report"
	_end_activity(false)
	player.frozen = true
	var r = UI.screen_root("Mission report · 3 days", "LUNA VR")
	report_ctl = r[0]; var b = r[1]
	var st = sim.luna.stats
	var me = sim.me()
	var lines = [
		"Your wellbeing (LUNA's estimate, private to you): %d%%" % int(sim.my_wellbeing() * 100.0),
		"Crew-wide wellbeing (anonymous): %d%%" % int(sim.crew_wellbeing_avg() * 100.0),
		"LUNA's quiet Tier 1 changes: %d · offers: %d (yes %d, no %d) · human escalations: %d" % [st.t1, st.offers, st.accepted, st.declined, st.escalations],
		"Your check-ins: %d · meals together: %d · exercise: %d min · harvests: %d · EVAs: %d" % [sim.stats_vr.checkins, sim.stats_vr.meals_together, sim.stats_vr.exercise_min, sim.stats_vr.harvests, sim.stats_vr.eva],
		"Air: CO₂ %d ppm · cabin dust %.0f µg/m³ · battery %d%% · spare parts left %d" % [sim.hab.co2, sim.hab.dust_air, int(sim.hab.battery / 2.0), sim.hab.parts],
		"Storm shelter: %s" % ("reached in time" if sim.shelter_ok else "late: extra radiation dose logged"),
	]
	for l in lines: b.add_child(UI.lbl(l, 20, UI.INK, true))
	b.add_child(UI.lbl("Decisions", 22, UI.ACC))
	for dd in sim.decisions: b.add_child(UI.lbl("· %s: %s (%s)" % [dd.title, dd.choice, dd.by], 17, UI.INK, true))
	b.add_child(UI.lbl("Every number above came from the same LUNA engine as the 2D game, running live while you played.", 16, UI.MUT, true))
	b.add_child(UI.btn("Play again", func(): get_tree().reload_current_scene(), 0.58, 24))
	report_ctl.custom_minimum_size = Vector2(900, 0)
	if player.vr:
		popup_scr.set_ui(report_ctl); popup_scr.visible = true
		var f = -player.cam.global_transform.basis.z; f.y = 0; f = f.normalized()
		popup_scr.global_position = player.cam.global_position + f * 1.2
		popup_scr.look_at(player.cam.global_position, Vector3.UP); popup_scr.rotate_object_local(Vector3.UP, PI)
	else:
		var cc = CenterContainer.new(); cc.set_anchors_preset(Control.PRESET_FULL_RECT); cc.add_child(report_ctl); hud_layer.add_child(cc)
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE

# =====================================================================================================
#  screens
# =====================================================================================================
func _build_screens() -> void:
	for s in hab.screens:
		var su = Screens.make(s.id, self)
		if su:
			ui[s.id] = su
			s.set_ui(su.root)
			su.screen = s
			su.refresh(true)

# =====================================================================================================
#  tools: screenshots from fixed viewpoints (--shots=dir), used to check every room
# =====================================================================================================
func _shots(dir: String) -> void:
	DirAccess.make_dir_recursive_absolute(dir)
	_start_desktop()
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	while state != "play": await get_tree().process_frame
	set_fade(0.0)
	sim.advance(150.0)
	var views = [
		["01_pod", Vector3(10.6, 0, 6.6), 0.0, -0.12], ["02_kiosk", Vector3(12.2, 0, 8.8), PI / 2.0, -0.05],
		["03_hub", Vector3(27.8, 0, 9.3), 0.64, -0.1], ["04_greenhouse", Vector3(31.9, 0, 9.6), 0.0, 0.32],
		["05_ops", Vector3(20.6, 0, 20.0), -0.35, -0.05], ["06_console", Vector3(22.5, 0, 20.4), PI, -0.25],
		["07_airlock", Vector3(3.1, 0, 9.6), 0.0, -0.05], ["08_eclss", Vector3(10.6, 0, 19.6), 0.0, -0.05],
		["09_calib", Vector3(11.2, 0, 19.3), PI, 0.0], ["10_medical", Vector3(5.6, 0, 15.6), 2.21, -0.1],
		["11_exercise", Vector3(31.2, 0, 21.6), 0.0, -0.05], ["12_storage", Vector3(15.6, 0, 21.8), 0.0, -0.05],
		["13_corridor", Vector3(2.6, 0, 11.9), -PI / 2.0, 0.0],
		["16_wellness_center", Vector3(22.6, 0, 20.3), 0.62, -0.2], ["17_wellness_dock", Vector3(29.6, 0, 19.4), -2.1, -0.12],
		["18_earth_corner", Vector3(25.2, 0, 6.6), -PI / 2.0, -0.05], ["19_eva_prep", Vector3(3.1, 0, 4.4), 0.0, -0.05],
	]
	for v in views:
		player.place(v[1], v[2]); player.pitch = v[3]; player.cam.rotation.x = v[3]
		msg_t = 0.0; _hide_popup(); seen_msgs = sim.messages.size()
		for k in 8: await get_tree().process_frame
		_save_shot("%s/%s.png" % [dir, v[0]])
	outside = true
	for v in [["14_outside_arrays", Vector3(-6, 0, 6), 0.15, 0.12], ["15_outside_hab", Vector3(-14, 0, -12), -2.45, 0.02]]:
		player.place(v[1], v[2]); player.pitch = v[3]; player.cam.rotation.x = v[3]
		for k in 10: await get_tree().process_frame
		_save_shot("%s/%s.png" % [dir, v[0]])
	get_tree().quit()

## the whole 3-day mission played by a script through the real game code (no walking: it calls the same
## handlers your clicks do), so every system, screen and event runs; screenshots at the key moments
func _autoplay(dir: String) -> void:
	DirAccess.make_dir_recursive_absolute(dir)
	_start_desktop()
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	time_mult = 30.0
	var shot_n = 0
	var last_h = -1
	var did = {}
	var asked = 0
	while state != "report":
		await get_tree().process_frame
		if int(sim.now / 120.0) != last_h:
			last_h = int(sim.now / 120.0)
			print("AP D%d %s state=%s act=%s asleep=%s pend=%d co2=%d wb=%.2f" % [sim.day(), sim.clock(), state, activity.get("id", "-"), sim.me().asleep, sim.pending.size(), sim.hab.co2, sim.my_wellbeing()])
		if state != "play": continue
		var d = sim.day(); var h = sim.hour()
		var key = func(k): return "%d_%s" % [d, k]
		if not asking.is_empty():
			asked += 1
			if not did.has("shot_offer"):
				did["shot_offer"] = true; await get_tree().process_frame
				_save_shot("%s/offer.png" % dir)
			_answer(asked % 3 != 0)
			continue
		if not activity.is_empty(): continue
		if not did.has(key.call("checkin")) and h >= 6.6:
			did[key.call("checkin")] = true; sim.player_checkin(3 if d < 3 else 4, d == 2); continue
		for e in sim.pending.duplicate():
			if sim.now - e.get("_t0", sim.now) >= 0.0 and not did.has("ev_" + e.id):
				did["ev_" + e.id] = true
				if not did.has("shot_console"):
					did["shot_console"] = true
					player.place(Vector3(22.5, 0, 20.4), PI); player.pitch = -0.3; player.cam.rotation.x = -0.3
					ui["console"].refresh(true)
					for k in 6: await get_tree().process_frame
					_save_shot("%s/console.png" % dir)
				var k2 = e.rec if e.id != "v_dust" else 2
				sim.choose(e, k2)
		if sim.fired.get("swap_task", false) and not did.has("swap"):
			did["swap"] = true; _on_click({"kind": "inter", "node": {"id": "cartridge"}}); continue
		if sim.fired.get("eva_task", false) and sim.hab.dust_storm_until < sim.now and not did.has("eva") and h > 9.0 and h < 17.0:
			did["eva"] = true; _eva_out()
			for k in 120: await get_tree().process_frame
			_save_shot("%s/eva.png" % dir)
			for k in 3:
				_on_click({"kind": "inter", "node": {"id": "array_%d" % k}})
				while not activity.is_empty(): await get_tree().process_frame
			_eva_in()
			for k in 120: await get_tree().process_frame
			airlock_cycle(); continue
		if sim.hab.flare_until > sim.now and not did.has(key.call("shelter")):
			did[key.call("shelter")] = true; player.place(Vector3(4.0, 0.05, 19.5), PI); continue
		if _meal_time() and not did.has(key.call("meal_%d" % int(h))):
			did[key.call("meal_%d" % int(h))] = true; _on_click({"kind": "inter", "node": {"id": "seat"}}); continue
		if h >= 9.0 and not did.has(key.call("tend")):
			did[key.call("tend")] = true; _on_click({"kind": "inter", "node": {"id": "rack_%d" % (d % 8)}}); continue
		if h >= 10.0 and not did.has(key.call("cabinet")):
			did[key.call("cabinet")] = true; _on_click({"kind": "inter", "node": {"id": "cabinet"}}); continue
		if h >= 14.0 and not did.has(key.call("bike")):
			did[key.call("bike")] = true; _on_click({"kind": "inter", "node": {"id": "bike"}}); continue
		if h >= 15.5 and not did.has(key.call("chair")):
			did[key.call("chair")] = true; _on_click({"kind": "inter", "node": {"id": "recovery_chair"}}); continue
		if h >= 16.0 and h < 17.0 and not did.has(key.call("work")):
			did[key.call("work")] = true; _on_click({"kind": "inter", "node": {"id": "desk"}}); continue
		if not sim.player_offer.is_empty() and not did.has("off_" + str(sim.now)):
			var oid = sim.player_offer.id
			var map = {"lounge": "sofa", "greenhouse": "rack_1", "vrride": "bike", "nap": "bed", "earthcall": "", "breathing": "recovery_chair", "chat": "recovery_chair"}
			did["off_" + str(sim.now)] = true
			if map.get(oid, "") != "" and not (oid == "nap" and (h >= 20.5 or h < 6.0)): _on_click({"kind": "inter", "node": {"id": map[oid]}}); continue
		if (h >= 22.0 or h < 5.0) and not sim.me().asleep:
			if shot_n == 0:
				shot_n = 1
				player.place(Vector3(23.75, 0, 20.2), 0.0); player.pitch = -0.6; player.cam.rotation.x = -0.6
				ui["twin"].refresh(true)
				for k in 6: await get_tree().process_frame
				_save_shot("%s/twin.png" % dir)
			_on_click({"kind": "inter", "node": {"id": "bed"}})
	for k in 10: await get_tree().process_frame
	_save_shot("%s/report.png" % dir)
	print("AUTOPLAY DONE asked=%d decisions=%d stats=%s vr=%s wb=%.2f crew=%.2f" % [asked, sim.decisions.size(), sim.luna.stats, sim.stats_vr, sim.my_wellbeing(), sim.crew_wellbeing_avg()])
	for dd in sim.decisions: print("DECISION ", dd.title, " = ", dd.choice, " by ", dd.by)
	for m in sim.messages: print("LUNA> ", m.text)
	get_tree().quit()

func _save_shot(path: String) -> void:
	var img = get_viewport().get_texture().get_image()
	if img: img.save_png(path)


func g_eva_ready() -> bool: return flags.get("eva_ready", -9999.0) > sim.now - 240.0

# =====================================================================================================
#  SELENE live link: the team's face + voice app sends its scores; you become a real sensor reading
# =====================================================================================================
func selene_status() -> String:
	if selene_live:
		return "SELENE live: stress %s · fatigue %s · low mood %s (from your last camera + voice check-in)" % [_f(selene_data.get("stress")), _f(selene_data.get("fatigue")), _f(selene_data.get("low_mood"))]
	return "SELENE not connected. On this PC run selene_bridge.py next to SELENE to use your real face and voice."

func _f(v) -> String: return "–" if v == null else "%.2f" % float(v)

func _selene_tick(dt: float) -> void:
	selene_t -= dt
	if selene_t > 0.0 or selene_http.get_http_client_status() != HTTPClient.STATUS_DISCONNECTED: return
	selene_t = 2.0 if selene_live else (4.0 if selene_fail < 3 else 20.0)
	selene_http.request(SELENE_URL)

func _on_selene(result: int, code: int, _h: PackedStringArray, body: PackedByteArray) -> void:
	if result != HTTPRequest.RESULT_SUCCESS or code != 200:
		if selene_live: _toast("SELENE disconnected. LUNA goes back to the habitat sensors alone.", "info")
		selene_live = false; selene_fail += 1
		return
	var d = JSON.parse_string(body.get_string_from_utf8())
	if typeof(d) != TYPE_DICTIONARY or not d.get("connected", false): return
	selene_fail = 0
	if not selene_live:
		selene_live = true
		_toast("SELENE connected. Your real face and voice check-ins now feed LUNA's read on you, live.")
	selene_data = d
	# you are the person SELENE measured: your state in the habitat follows it, and LUNA's normal
	# sensing -> triage -> Tier 1 light/sound -> Tier 2 offer chain reacts on its own
	var cr = sim.me()
	if d.get("stress") != null: cr.stress = lerp(cr.stress, clamp(float(d.stress), 0.0, 1.0), 0.5)
	if d.get("fatigue") != null: cr.fatigue = lerp(cr.fatigue, clamp(float(d.fatigue), 0.0, 1.0), 0.4)
	if d.get("low_mood") != null: cr.mood = lerp(cr.mood, 0.45 - 1.1 * clamp(float(d.low_mood), 0.0, 1.0), 0.3)
	var st = float(d.get("stress", 0.0) if d.get("stress") != null else 0.0)
	if st > 0.7 and not selene_said_high:
		selene_said_high = true
		sim.say("Your SELENE check-in shows stress above your normal. I'll keep this room calm, and I'm here if you want a break.")
	elif st < 0.5: selene_said_high = false

# =====================================================================================================
#  3D digital twin in the Wellness Operations Center: a 1:30 habitat, lit with each room's real light
# =====================================================================================================
const TWIN_S = 1.0 / 30.0

func _build_twin3d() -> void:
	var tw: Node3D = hab.dyn.get("twin3d")
	if tw == null: return
	var spin = Node3D.new(); spin.name = "spin"; tw.add_child(spin)
	var c = Vector3(18.1, 0, 11.9)
	for r in hab._regions():
		var wr = hab.world_rect(r)
		var mi = MeshInstance3D.new(); var bm = BoxMesh.new()
		var hgt = hab.rh(r) * TWIN_S
		bm.size = Vector3(wr.size.x * TWIN_S - 0.004, hgt, wr.size.y * TWIN_S - 0.004)
		mi.mesh = bm
		var m = StandardMaterial3D.new(); m.emission_enabled = true; m.albedo_color = Color(0.8, 0.85, 0.95, 0.85)
		m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		mi.material_override = m
		mi.position = Vector3((wr.get_center().x - c.x) * TWIN_S, hgt * 0.5, (wr.get_center().y - c.z) * TWIN_S)
		spin.add_child(mi)
		twin_rooms.append({"mat": m, "key": hab.light_key_of(r) if r != "communal" else "communal"})
	for i in 4:
		var p = MeshInstance3D.new(); var cm = CylinderMesh.new(); cm.top_radius = 0.022; cm.bottom_radius = 0.022; cm.height = 0.12; p.mesh = cm
		p.material_override = Mats.glow(Avatar.SUIT[i] if i > 0 else Color(1, 1, 1), 2.0, "puck_%d" % i)
		spin.add_child(p); twin_pucks.append(p)

func _update_twin3d(dt: float) -> void:
	if twin_rooms.is_empty(): return
	var tw: Node3D = hab.dyn["twin3d"]
	var spin = tw.get_node("spin")
	spin.rotation.y += dt * 0.12
	var c = Vector3(18.1, 0, 11.9)
	for i in 4:
		var wp: Vector3
		if i == 0:
			wp = player.global_position
			twin_pucks[i].visible = not outside
		else:
			var cr: Crew = sim.crew[i]
			var p2 = cr.prev.lerp(cr.pos, clamp(sim.acc, 0.0, 1.0))
			wp = Vector3(p2.x / 16.0 * Habitat.CELL, 0, p2.y / 16.0 * Habitat.CELL)
			twin_pucks[i].visible = not cr.outside
		twin_pucks[i].position = Vector3((wp.x - c.x) * TWIN_S, 0.1, (wp.z - c.z) * TWIN_S)
	twin_t -= dt
	if twin_t > 0.0: return
	twin_t = 0.5
	for t in twin_rooms:
		var L = sim.hab.light.get(t.key, sim.hab.light.get("communal", null))
		if L == null: continue
		var col = Mats.cct(L.cct)
		t.mat.albedo_color = Color(col.r, col.g, col.b, 0.85)
		t.mat.emission = col; t.mat.emission_energy_multiplier = 0.2 + hab.lux_energy(L.lux) * 0.5

func _selenetest() -> void:
	_start_desktop()
	while state != "play": await get_tree().process_frame
	time_mult = 3.0
	var t0 = Time.get_ticks_msec()
	var asked = []
	while Time.get_ticks_msec() - t0 < 25000:
		await get_tree().process_frame
		if not asking.is_empty():
			asked.append("%s %s" % [D.CONDS[asking.cond], asking.offer]); _answer(true)
	print("SELENETEST live=%s data=%s" % [selene_live, selene_data])
	print("SELENETEST you: stress %.2f fatigue %.2f mood %.2f | LUNA est anx %.2f dep %.2f fat %.2f | tier anx %d | t1 %s" % [sim.me().stress, sim.me().fatigue, sim.me().mood, sim.luna.est[0][0], sim.luna.est[0][1], sim.luna.est[0][3], sim.luna.tier[0][0], sim.luna.t1_active[0][0]])
	print("SELENETEST asked ", asked)
	for m in sim.messages: print("LUNA> ", m.text)
	get_tree().quit()
