extends SceneTree
func _init():
	var s = Sim.new("mission"); s.auto = true
	while s.day() < 6: s.advance(30.0)
	while s.hour() < 13.0: s.advance(10.0)
	for cr in s.crew:
		var c = 1
		var parts = []
		for sn in D.SENSOR_ORDER:
			var f = s.luna.feature(sn, c, cr, s.now)
			if f[1] > 0: parts.append("%s=%.2f" % [sn, f[0]])
		print(cr.name, " dep est %.2f truth %.2f conf %.2f mood %.2f conn %.2f night %s pod %.2f | %s" % [s.luna.est[cr.i][c], cr.truth(c, s), s.luna.conf[cr.i][c], cr.mood, cr.connection, str(cr.night), s.pod_frac(cr.i), " ".join(parts)])
	quit()
