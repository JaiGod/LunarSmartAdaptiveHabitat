class_name Screen3D
extends Node3D
## A real screen in the habitat: a 2D interface rendered into a SubViewport and shown on a quad.
## Works with a VR laser pointer, a desktop crosshair or a mouse (the game converts hits into clicks).

var id = ""
var size_m = Vector2.ONE
var px = Vector2i(512, 320)
var vp: SubViewport
var quad: MeshInstance3D
var area: Area3D
var ui: Control
var interactive = true
var hover_px = Vector2(-1, -1)
var _refresh_t = 0.0
var refresh_every = 0.5          # seconds between redraws for display-only screens
var _want = 3                    # frames still to redraw (screens redraw only when something changed)

func setup(sid: String, s: Vector2, p: Vector2i, control: Control, inter := true) -> void:
	id = sid; size_m = s; px = p; interactive = inter
	vp = SubViewport.new()
	vp.size = p
	vp.transparent_bg = false
	vp.disable_3d = true
	vp.gui_disable_input = false
	vp.render_target_update_mode = SubViewport.UPDATE_ONCE
	add_child(vp)
	quad = MeshInstance3D.new()
	var qm = QuadMesh.new(); qm.size = s
	quad.mesh = qm
	var m = StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.albedo_texture = vp.get_texture()
	m.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR
	quad.material_override = m
	quad.position.z = 0.002
	add_child(quad)
	# bezel
	var bz = MeshInstance3D.new(); var bm = BoxMesh.new(); bm.size = Vector3(s.x + 0.04, s.y + 0.04, 0.02); bz.mesh = bm
	bz.material_override = Mats.black(); bz.position.z = -0.009
	add_child(bz)
	area = Area3D.new()
	area.collision_layer = 4 if inter else 0; area.collision_mask = 0; area.monitoring = false
	var cs = CollisionShape3D.new(); var bs = BoxShape3D.new(); bs.size = Vector3(s.x, s.y, 0.03); cs.shape = bs
	area.add_child(cs)
	area.set_meta("screen", self)
	add_child(area)
	if control: set_ui(control)

func set_ui(control: Control) -> void:
	if ui and ui != control and ui.get_parent() == vp: vp.remove_child(ui)
	ui = control
	ui.size = Vector2(px)
	if ui.get_parent(): ui.get_parent().remove_child(ui)
	vp.add_child(ui)
	request()

## redraw for the next few frames (hover effects and layout settle in 2–3 frames)
func request(frames := 3) -> void:
	_want = max(_want, frames)

## hit point in world space -> pixel in the viewport
func world_to_px(p: Vector3) -> Vector2:
	var l = to_local(p)
	return Vector2((l.x / size_m.x + 0.5) * px.x, (0.5 - l.y / size_m.y) * px.y)

func pointer_move(p: Vector3) -> void:
	hover_px = world_to_px(p)
	var ev = InputEventMouseMotion.new()
	ev.position = hover_px; ev.global_position = hover_px
	vp.push_input(ev, true)
	request(2)

func pointer_button(p: Vector3, pressed: bool) -> void:
	hover_px = world_to_px(p)
	var ev = InputEventMouseButton.new()
	ev.button_index = MOUSE_BUTTON_LEFT; ev.pressed = pressed
	ev.position = hover_px; ev.global_position = hover_px
	vp.push_input(ev, true)
	request(4)

func pointer_scroll(p: Vector3, up: bool) -> void:
	var ev = InputEventMouseButton.new()
	ev.button_index = MOUSE_BUTTON_WHEEL_UP if up else MOUSE_BUTTON_WHEEL_DOWN; ev.pressed = true
	ev.position = world_to_px(p); ev.global_position = ev.position
	vp.push_input(ev, true)

func _process(dt: float) -> void:
	if not is_visible_in_tree(): return
	if not interactive:
		_refresh_t -= dt
		if _refresh_t <= 0.0: _refresh_t = refresh_every; _want = max(_want, 1)
	if _want > 0:
		_want -= 1
		vp.render_target_update_mode = SubViewport.UPDATE_ONCE
