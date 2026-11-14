extends SceneTree
## headless: run the 3-day VR mission with a scripted "player" and print what happens
func _init() -> void:
	var s = VRSim.new()
	var asked = 0; var yes = 0
	var t0 = Time.get_ticks_msec()
	while not s.done:
		s.advance(1.0)
		# scripted player: sleep at 22:30, wake by alarm, eat at meals, answer LUNA's offers (yes, then no)
		var h = s.hour()
		var me = s.me()
		if not me.asleep:
			if h >= 22.5 or h < 5.0: s.player_sleep()
			elif (h >= 7.0 and h < 7.75) or (h >= 12.0 and h < 12.75) or (h >= 18.5 and h < 19.25):
				if s.player_act != "meal": s.player_cell = Vector2i(17, 2); s.set_player_act("meal", 40.0)
			elif h >= 13.5 and h < 14.0 and s.player_act != "exercise": s.player_cell = Vector2i(25, 13); s.set_player_act("exercise", 30.0)
			elif s.player_act == "idle": s.player_cell = Vector2i(17, 13); s.set_player_act("work", 60.0)
		var a = s.next_ask()
		if not a.is_empty():
			asked += 1
			var ok = asked % 2 == 1
			if ok: yes += 1
			print("D%d %s  LUNA asks you about %s (tier %d): %s -> %s" % [s.day(), s.clock(), D.CONDS[a.cond], a.tier, D.OFFERS[a.offer].say, "yes" if ok else "no"])
			s.player_reply(a, ok)
		for e in s.pending.duplicate():
			if s.now - e.get("_seen", s.now) == 0.0: pass
		if int(s.now) % 240 == 0:
			print("D%d %s  you: %s act=%s  stress %.2f fat %.2f mood %.2f | LUNA est max %.2f | CO2 %d bat %d%% dust %.2f" % [s.day(), s.clock(), "asleep" if me.asleep else "awake", me.act_id, me.stress, me.fatigue, me.mood, 1.0 - s.my_wellbeing(), s.hab.co2, s.hab.battery / 2.0, s.hab.dust_panels])
	for m in s.messages: print("LUNA> ", m.text)
	for p in s.public_log: print("LOG> ", p.text)
	for d in s.decisions: print("DECISION> ", d.title, " = ", d.choice, " (", d.by, ")")
	print("stats ", s.luna.stats)
	print("vr ", s.stats_vr, " asked ", asked, " yes ", yes, "  wb_avg ", s.record.wb_sum / max(1, s.record.wb_n))
	print("ms ", Time.get_ticks_msec() - t0)
	quit()
