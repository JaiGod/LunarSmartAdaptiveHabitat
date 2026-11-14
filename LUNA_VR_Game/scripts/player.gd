class_name Player
extends CharacterBody3D
## You: one body that works with a mouse and keyboard, in WebXR (Quest browser) or with OpenXR (PC VR).
## Lunar gravity (1.62 m/s²), smooth locomotion + snap turn in VR, a laser pointer for screens and objects.

signal pointer_clicked(hit: Dictionary)

const GRAVITY = 1.62
const WALK = 1.7
const RUN = 3.0
const EYE = 1.62

var origin: XROrigin3D
var cam: XRCamera3D
var left: XRController3D
var right: XRController3D
var laser: MeshInstance3D
var dot: MeshInstance3D
var vr = false
var xr_iface: XRInterface
var pitch = 0.0
var frozen = false              # sitting, lying, cut-scenes
var mouse_look = true
var hit = {}                    # what the pointer is on: {"kind": "inter"/"screen", "node", "pos"}
var _click_cool = 0.0
var snap_ready = true
var in_suit = false

func _ready() -> void:
	collision_layer = 1; collision_mask = 1
	var cs = CollisionShape3D.new(); var cap = CapsuleShape3D.new(); cap.radius = 0.24; cap.height = 1.7
	cs.shape = cap; cs.position.y = 0.85; add_child(cs)
	origin = XROrigin3D.new(); add_child(origin)
	cam = XRCamera3D.new(); cam.position.y = EYE; cam.near = 0.05; cam.far = 4000.0; cam.fov = 72.0
	origin.add_child(cam); cam.current = true
	left = XRController3D.new(); left.tracker = &"left_hand"; origin.add_child(left)
	right = XRController3D.new(); right.tracker = &"right_hand"; origin.add_child(right)
	for c in [left, right]: c.add_child(_hand_mesh())
	laser = MeshInstance3D.new(); var lm = BoxMesh.new(); lm.size = Vector3(0.004, 0.004, 1.0); laser.mesh = lm
	laser.material_override = Mats.glow(Color(0.4, 0.8, 1.0), 2.0, "laser"); laser.visible = false
	add_child(laser)
	dot = MeshInstance3D.new(); var dm = SphereMesh.new(); dm.radius = 0.012; dm.height = 0.024; dot.mesh = dm
	dot.material_override = Mats.glow(Color(0.5, 0.9, 1.0), 3.0, "laser_dot"); dot.visible = false
	add_child(dot)
	right.button_pressed.connect(_on_right_button)
	left.button_pressed.connect(_on_left_button)

func _hand_mesh() -> Node3D:
	var n = Node3D.new(); var b = MB.new()
	b.box(Vector3(0, -0.01, 0.03), Vector3(0.05, 0.035, 0.11), Mats.grey_plastic())
	b.torus(Vector3(0, 0.02, -0.03), 0.03, 0.042, Mats.dark_metal(), Basis(Vector3.RIGHT, 0.6))
	b.commit(n, "hand")
	return n

# =====================================================================================================
#  VR on / off
# =====================================================================================================
func try_openxr() -> bool:
	var xi = XRServer.find_interface("OpenXR")
	if xi and xi.is_initialized():
		xr_iface = xi
		get_viewport().use_xr = true
		DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
		_vr_on()
		return true
	return false

func webxr_available() -> XRInterface:
	return XRServer.find_interface("WebXR")

func start_webxr(on_started: Callable, on_failed: Callable) -> void:
	var w = XRServer.find_interface("WebXR")
	if not w: on_failed.call("This browser has no WebXR."); return
	xr_iface = w
	w.session_mode = "immersive-vr"
	w.requested_reference_space_types = "bounded-floor, local-floor, local"
	w.required_features = "local-floor"
	w.optional_features = "bounded-floor"
	if not w.session_started.is_connected(_on_webxr_started):
		w.session_started.connect(_on_webxr_started.bind(on_started))
		w.session_failed.connect(func(msg): on_failed.call(msg))
		w.session_ended.connect(_on_webxr_ended)
		w.select.connect(_on_webxr_select)
	if not w.initialize(): on_failed.call("Couldn't start WebXR.")

func _on_webxr_started(cb: Callable) -> void:
	get_viewport().use_xr = true
	_vr_on()
	cb.call()

func _on_webxr_ended() -> void:
	get_viewport().use_xr = false
	vr = false
	cam.position = Vector3(0, EYE, 0); cam.rotation = Vector3.ZERO
	laser.visible = false; dot.visible = false

func _vr_on() -> void:
	vr = true
	cam.position = Vector3.ZERO
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE

func _on_webxr_select(source_id: int) -> void:
	# WebXR's "select" (trigger) as a fallback click for browsers that don't report button names
	var w = xr_iface as WebXRInterface
	if w and w.get_input_source_tracker(source_id) and w.get_input_source_tracker(source_id).hand == XRPositionalTracker.TRACKER_HAND_RIGHT:
		_click()

func _on_right_button(n: String) -> void:
	if n in ["trigger_click", "trigger"]: _click()
	elif n in ["ax_button", "by_button"]: get_parent().call("vr_button", "right_" + n)

func _on_left_button(n: String) -> void:
	get_parent().call("vr_button", "left_" + n)

func _click() -> void:
	if _click_cool > 0.0: return
	_click_cool = 0.22
	if hit.get("kind", "") == "screen":
		hit.node.pointer_button(hit.pos, true)
		var s = hit.node; var p = hit.pos
		get_tree().create_timer(0.05).timeout.connect(func(): s.pointer_button(p, false))
	pointer_clicked.emit(hit)

# =====================================================================================================
#  movement
# =====================================================================================================
func _unhandled_input(e: InputEvent) -> void:
	if vr: return
	if e is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED and mouse_look:
		if e.relative.length() > 180.0: return     # browsers send a huge jump when pointer lock (re)starts: ignore it
		rotate_y(-e.relative.x * 0.0025)
		pitch = clamp(pitch - e.relative.y * 0.0025, -1.45, 1.45)
		cam.rotation.x = pitch
	if e is InputEventMouseButton and e.pressed:
		if e.button_index == MOUSE_BUTTON_LEFT:
			if Input.mouse_mode != Input.MOUSE_MODE_CAPTURED and not get_parent().call("ui_open"):
				Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
			else:
				_click()
		elif e.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN] and hit.get("kind", "") == "screen":
			hit.node.pointer_scroll(hit.pos, e.button_index == MOUSE_BUTTON_WHEEL_UP)

func _physics_process(dt: float) -> void:
	_click_cool -= dt
	if frozen:
		velocity = Vector3.ZERO
		_update_pointer()
		return
	var input = Vector2.ZERO
	var speed = WALK
	if vr:
		input = right.get_vector2("primary") * 0.0
		var lv = left.get_vector2("primary")
		if lv.length() < 0.1: lv = left.get_vector2("thumbstick")
		input = Vector2(lv.x, -lv.y)
		var rv = right.get_vector2("primary")
		if rv.length() < 0.1: rv = right.get_vector2("thumbstick")
		if abs(rv.x) > 0.7 and snap_ready:
			_snap_turn(-sign(rv.x) * deg_to_rad(30.0)); snap_ready = false
		elif abs(rv.x) < 0.3: snap_ready = true
		_follow_head(dt)
	else:
		if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP): input.y -= 1
		if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN): input.y += 1
		if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT): input.x -= 1
		if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT): input.x += 1
		if Input.is_key_pressed(KEY_SHIFT): speed = RUN
	var basis_y = Basis(Vector3.UP, (cam.global_transform.basis.get_euler().y if vr else rotation.y))
	var dir = basis_y * Vector3(input.x, 0, input.y)
	if dir.length() > 1.0: dir = dir.normalized()
	velocity.x = dir.x * speed; velocity.z = dir.z * speed
	if not is_on_floor(): velocity.y -= GRAVITY * dt
	elif not vr and Input.is_key_pressed(KEY_SPACE): velocity.y = 1.5
	move_and_slide()
	_update_pointer()

## keep the collision body under the headset when you physically walk around your room
func _follow_head(dt: float) -> void:
	var off = cam.global_position - global_position; off.y = 0
	if off.length() < 0.02: return
	var before = global_position
	var saved_v = velocity
	velocity = off / max(dt, 0.001); velocity.y = 0
	move_and_slide()
	var moved = global_position - before; moved.y = 0
	origin.global_position -= moved
	velocity = saved_v

func _snap_turn(a: float) -> void:
	var cp = cam.global_position
	origin.rotate_y(a)
	var np = cam.global_position
	origin.global_position += Vector3(cp.x - np.x, 0, cp.z - np.z)

func eye_yaw() -> float:
	return cam.global_transform.basis.get_euler().y

## put your feet at p, looking along yaw
func place(p: Vector3, yaw: float) -> void:
	global_position = p
	velocity = Vector3.ZERO
	if vr:
		var cy = cam.global_transform.basis.get_euler().y
		origin.rotate_y(yaw - cy)
		var off = cam.global_position - global_position; off.y = 0
		origin.global_position -= off
	else:
		rotation.y = yaw; pitch = 0.0; cam.rotation.x = 0.0

# =====================================================================================================
#  pointer: right-hand laser in VR, screen centre on desktop
# =====================================================================================================
func _update_pointer() -> void:
	var from: Vector3; var dir: Vector3
	if vr:
		from = right.global_position; dir = -right.global_transform.basis.z
	else:
		from = cam.global_position; dir = -cam.global_transform.basis.z
	var space = get_world_3d().direct_space_state
	var q = PhysicsRayQueryParameters3D.create(from, from + dir * 3.2, 1 | 2 | 4, [get_rid()])
	q.collide_with_areas = true; q.collide_with_bodies = true
	var r = space.intersect_ray(q)
	var prev = hit
	hit = {}
	if r and r.collider is Area3D:
		var a: Area3D = r.collider
		if a.has_meta("screen"): hit = {"kind": "screen", "node": a.get_meta("screen"), "pos": r.position}
		elif a.has_meta("inter"): hit = {"kind": "inter", "node": a.get_meta("inter"), "pos": r.position}
	if hit.get("kind", "") == "screen":
		hit.node.pointer_move(hit.pos)
	if vr:
		var end = r.position if r else from + dir * 3.2
		var L = from.distance_to(end)
		laser.visible = true
		laser.global_transform = Transform3D(Basis.looking_at(dir, Vector3.UP if abs(dir.y) < 0.99 else Vector3.FORWARD), from + dir * L * 0.5)
		laser.scale = Vector3(1, 1, L)
		dot.visible = not hit.is_empty(); dot.global_position = end
	else:
		laser.visible = false; dot.visible = false
