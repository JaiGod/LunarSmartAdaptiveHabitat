extends SceneTree
func _init() -> void:
	var s = VRSim.new()
	var h = Habitat.new()
	root.add_child(h)
	var t0 = Time.get_ticks_msec()
	h.build(s)
	var mi = 0; var verts = 0
	for n in h.find_children("*", "MeshInstance3D", true, false):
		mi += 1
		if n.mesh is ArrayMesh: for k in n.mesh.get_surface_count(): verts += n.mesh.surface_get_array_len(k)
	print("built in ", Time.get_ticks_msec() - t0, " ms; mesh instances ", mi, " verts ", verts, " doors ", h.doors.size(), " inters ", h.inters.size(), " screens ", h.screens.size(), " lights ", h.lights.keys())
	quit()
