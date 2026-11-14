class_name Avatar
extends Node3D
## One of the other three crew members, driven by the LUNA engine (where they go, what they do).
## Simple, readable body: flight suit in their colour, sitting, walking, sleeping, exercising.

var i = 0
var torso: Node3D
var legs = []
var arms = []
var tag: Label3D
var walk_phase = 0.0
var last_pos = Vector3.ZERO
var yaw_target = 0.0

const SUIT = [Color("#2f5fb3"), Color("#e9edf2"), Color("#d9772f"), Color("#4f9a4a")]
const SKIN = [Color("#8d5a3c"), Color("#e0b18f"), Color("#f0c8a0"), Color("#b97f5a")]
const HAIR = [Color("#1d1410"), Color("#5a3a22"), Color("#141414"), Color("#2b1c12")]

func setup(idx: int) -> void:
	i = idx
	name = "crew_%d" % idx
	var suit = Mats.suit(SUIT[i]); var skin = Mats.skin(SKIN[i]); var hair = Mats.skin(HAIR[i])
	torso = Node3D.new(); torso.position.y = 0.9; add_child(torso)
	var tb = MB.new()
	tb.capsule(Vector3(0, 0.28, 0), 0.19, 0.66, suit, Basis.from_scale(Vector3(1.0, 1.0, 0.72)))
	tb.sphere(Vector3(0, 0.78, 0), 0.12, skin, 12, 8, Vector3(0.9, 1.08, 0.95))
	tb.sphere(Vector3(0, 0.84, -0.015), 0.123, hair, 12, 6, Vector3(0.95, 0.75, 1.0))
	tb.box(Vector3(0, 0.5, 0.135), Vector3(0.12, 0.06, 0.01), Mats.white_plastic())             # name patch
	tb.box(Vector3(0.11, 0.48, 0.135), Vector3(0.05, 0.035, 0.01), Mats.glow(Color(0.2, 0.7, 1.0), 1.0))   # UWB location tag
	tb.commit(torso, "torso")
	for side in [-1.0, 1.0]:
		var leg = Node3D.new(); leg.position = Vector3(0.09 * side, 0.9, 0); add_child(leg)
		var lb = MB.new()
		lb.capsule(Vector3(0, -0.43, 0), 0.075, 0.86, suit)
		lb.box(Vector3(0, -0.86, 0.05), Vector3(0.11, 0.08, 0.24), Mats.rubber())
		lb.commit(leg, "leg")
		legs.append(leg)
		var arm = Node3D.new(); arm.position = Vector3(0.24 * side, 0.52, 0); torso.add_child(arm)
		var ab = MB.new()
		ab.capsule(Vector3(0, -0.3, 0), 0.06, 0.62, suit)
		ab.sphere(Vector3(0, -0.62, 0), 0.05, skin, 8, 4)
		ab.commit(arm, "arm")
		arms.append(arm)
	tag = Label3D.new()
	tag.text = D.CREW[i].short + "\n" + D.CREW[i].role
	tag.font_size = 30; tag.pixel_size = 0.0028; tag.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	tag.modulate = Color(1, 1, 1, 0.85); tag.outline_size = 6; tag.outline_modulate = Color(0, 0, 0, 0.6)
	tag.position.y = 2.0; tag.no_depth_test = false
	add_child(tag)

## pose for what the engine says they are doing
func pose(cr: Crew, world_pos: Vector3, dt: float) -> void:
	visible = not cr.outside
	var moved = world_pos - position
	moved.y = 0
	var speed = moved.length() / max(dt, 0.0001)
	if moved.length() > 0.002: yaw_target = atan2(moved.x, moved.z)
	elif cr.act_face == "up": yaw_target = PI
	elif cr.act_face == "down": yaw_target = 0.0
	elif cr.act_face == "side": yaw_target = -PI / 2.0 if cr.flip else PI / 2.0
	position = world_pos
	rotation.y = lerp_angle(rotation.y, yaw_target, min(1.0, dt * 8.0))
	var walking = speed > 0.05 and cr.walking()
	var sitting = cr.act_id in ["meal", "work", "lounge", "movie", "pod", "earthcall", "cmo", "shelter", "offer"] and not cr.walking()
	var lying = cr.asleep or cr.act_id == "nap"
	var exercising = cr.act_id in ["exercise", "offer_ex"] and not cr.walking()
	rotation.x = 0.0; position.y = world_pos.y
	if lying:
		rotation.x = -PI / 2.0; position.y = 0.62; torso.rotation.x = 0.0
		for l in legs: l.rotation.x = 0.0
		for a in arms: a.rotation.x = 0.0
		tag.visible = false
		return
	tag.visible = true
	if walking:
		walk_phase += dt * 7.5
		var s = sin(walk_phase) * 0.45
		legs[0].rotation.x = s; legs[1].rotation.x = -s
		arms[0].rotation.x = -s * 0.8; arms[1].rotation.x = s * 0.8
		torso.position.y = 0.9 + abs(cos(walk_phase)) * 0.025
	elif sitting:
		for l in legs: l.rotation.x = lerp(l.rotation.x, -PI / 2.0, min(1.0, dt * 6.0))
		for a in arms: a.rotation.x = lerp(a.rotation.x, -0.5, min(1.0, dt * 6.0))
		torso.position.y = lerp(torso.position.y, 0.5, min(1.0, dt * 6.0))
		for l in legs: l.position.y = torso.position.y
	elif exercising:
		walk_phase += dt * 10.0
		var s2 = sin(walk_phase) * 0.6
		legs[0].rotation.x = s2; legs[1].rotation.x = -s2
		arms[0].rotation.x = -s2; arms[1].rotation.x = s2
		torso.position.y = 0.92 + abs(cos(walk_phase)) * 0.04
	else:
		for l in legs: l.rotation.x = lerp(l.rotation.x, 0.0, min(1.0, dt * 6.0)); l.position.y = 0.9
		for a in arms: a.rotation.x = lerp(a.rotation.x, -0.25 if cr.act_id == "greenhouse" else 0.0, min(1.0, dt * 6.0))
		torso.position.y = lerp(torso.position.y, 0.9, min(1.0, dt * 6.0))
	if not sitting:
		for l in legs: l.position.y = 0.9 if not walking else torso.position.y
