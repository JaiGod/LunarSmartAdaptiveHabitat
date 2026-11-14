extends SceneTree
## headless run: godot --headless -s tools/simtest.gd -- [mission|tabletop] [off]
func _init():
	var args = OS.get_cmdline_user_args()
	var sc = args[0] if args.size() > 0 else "mission"
	var s = Sim.new(sc); s.auto = true
	if args.has("off"): s.luna.off = true
	var t0 = Time.get_ticks_msec(); var last_day = 0
	while not s.done:
		s.advance(60.0)
		if s.day() != last_day and s.hour() >= 20.0:
			last_day = s.day()
			if last_day % (1 if sc == "tabletop" else 3) == 0 or last_day in [4, 5, 24, 25, 27, 28, 33, 38, 39, 45, 70, 71]:
				var line = "D%02d " % last_day
				for cr in s.crew:
					var tr = []
					for c in 6: tr.append(int(round(cr.truth(c, s) * 9)))
					var tt = []
					for c in 6: tt.append(s.luna.tier[cr.i][c])
					line += "%s %s %s | " % [cr.name.left(3), "".join(tr.map(func(v): return str(v))), "".join(tt.map(func(v): return str(v)))]
				line += "co2 %d bat %d sci %.0f" % [s.hab.co2, s.hab.battery, s.science]
				print(line)
	print("ms ", Time.get_ticks_msec() - t0)
	print("stats ", s.luna.stats)
	print("wellbeing avg %.2f min %.2f  co2>1500 %d min  shed %d min" % [s.record.wb_sum / max(1, s.record.wb_n), s.record.wb_min, s.record.co2_high_min, s.record.battery_empty_min])
	var trust = []
	for cr in s.crew: trust.append(snappedf(cr.trust, 0.01))
	print("trust ", trust, " science ", snappedf(s.science, 0.1))
	quit()
