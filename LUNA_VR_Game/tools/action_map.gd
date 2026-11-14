extends SceneTree
func _init():
	var am = OpenXRActionMap.new()
	am.create_default_action_sets()
	print("save ", ResourceSaver.save(am, "res://openxr_action_map.tres"))
	quit()
