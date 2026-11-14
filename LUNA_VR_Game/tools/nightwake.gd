extends SceneTree
func _init():
	var s = VRSim.new()
	while s.now < 22.0 * 60.0: s.advance(1.0)
	s.player_sleep()
	var was = true
	while s.now < 1440.0 + 7.0 * 60.0:
		s.advance(1.0)
		var a = s.me().asleep
		if a != was:
			print("flip at ", s.clock(), " asleep=", a, " act=", s.me().act_id, " player_act=", s.player_act, " stage=", s.me().stage)
			was = a
			if not a: s.player_sleep(); was = true
	quit()
