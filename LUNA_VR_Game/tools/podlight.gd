extends SceneTree
func _init():
	var s = VRSim.new()
	for k in 200: s.advance(1.0)
	print("t ", s.clock(), " me asleep ", s.me().asleep, " act ", s.me().act_id)
	for k in ["pod0", "pod1", "quarters", "communal"]: print(k, " ", s.hab.light.get(k))
	quit()
