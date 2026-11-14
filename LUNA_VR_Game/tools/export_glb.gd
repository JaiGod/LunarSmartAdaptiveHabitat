extends SceneTree
## Exports every LUNA VR model as its own .glb file (opens in Blender: File > Import > glTF 2.0).
## Each model keeps its parts as separate meshes and its materials (base colour, roughness, metallic,
## emission). Run with a renderer (not --headless) so the mesh data exists:
##   godot --path . -s tools/export_glb.gd -- --out=<dir>

func _init() -> void:
	var out = "res://export_glb"
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--out="): out = a.substr(6)
	DirAccess.make_dir_recursive_absolute(out)
	var s = VRSim.new()
	var h = Habitat.new()
	h.separate_models = true
	root.add_child(h)
	h.build(s)
	await process_frame
	var targets = []
	for ch in h.get_children():
		if ch is Node3D and ch.name in ["Dust_Decontamination_Airlock", "Crew_Pod_1", "Mood_Checkin_Kiosk", "Social_Hub", "Greenhouse_Therapy_Racks",
				"Medical_Storm_Shelter", "ECLSS_and_Lighting_Calibration", "Robot_Bay_and_Stores", "Ops_LUNA", "Exercise_Bay", "Hygiene"]:
			targets.append(ch)
	var outside = h.get_node_or_null("Outside")
	if outside:
		for ch in outside.get_children():
			if ch.name in ["athlete_robot", "pressurised_rover"]: targets.append(ch)
	# the modular kit: walls, floors, ceilings, doors, cable trays, sensors (one file)
	var kit = Node3D.new(); kit.name = "Habitat_Shell_Modular_Kit"
	for n in h.static_root.get_children():
		if n is MeshInstance3D: kit.add_child(n.duplicate())
	targets.append(kit)
	var count = 0
	for t in targets:
		var clean = _meshes_only(t)
		# centre each model on its own footprint so it imports at the origin in Blender
		var aabb = AABB(); var first = true
		for m in clean.get_children():
			var b = (m as MeshInstance3D).get_aabb()
			b = m.transform * b
			aabb = b if first else aabb.merge(b); first = false
		var off = Vector3(-aabb.get_center().x, -aabb.position.y, -aabb.get_center().z)
		for m in clean.get_children(): m.position += off
		var doc = GLTFDocument.new(); var st = GLTFState.new()
		var err = doc.append_from_scene(clean, st)
		if err == OK:
			err = doc.write_to_filesystem(st, "%s/%s.glb" % [out, t.name])
		print("%s.glb  parts=%d  size=%.2f x %.2f x %.2f m  %s" % [t.name, clean.get_child_count(), aabb.size.x, aabb.size.y, aabb.size.z, "ok" if err == OK else "error %d" % err])
		if err == OK: count += 1
		clean.free()
	print("EXPORTED ", count)
	quit()

## copy only the MeshInstance3D parts (baked to world space relative to the model root)
func _meshes_only(src: Node3D) -> Node3D:
	var n = Node3D.new(); n.name = src.name
	for m in src.find_children("*", "MeshInstance3D", true, false):
		if m.get_parent() is SubViewport: continue
		var mesh = m.mesh
		if mesh == null: continue
		var mat = m.material_override
		if mat and mat is StandardMaterial3D and (mat as StandardMaterial3D).albedo_texture is ViewportTexture: continue
		var c = MeshInstance3D.new(); c.mesh = mesh; c.name = String(m.name).replace(src.name + "_", "")
		if mat and not (mat is ShaderMaterial): c.material_override = mat
		var xf = (m as Node3D).global_transform if src.is_inside_tree() else (m as Node3D).transform
		c.transform = src.global_transform.affine_inverse() * xf if src.is_inside_tree() else xf
		n.add_child(c)
	return n
