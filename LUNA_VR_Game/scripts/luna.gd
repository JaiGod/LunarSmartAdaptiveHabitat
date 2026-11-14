class_name Luna
extends RefCounted
## LUNA: detection → triage → intervention → monitoring → escalation.

var sim
var rng = RandomNumberGenerator.new()
# ---- setup chosen before launch ----
var loadout = {}                 # sensor id -> bool
var proto = []                   # 6 x {"t1": [levers], "t2": offer id}
var policy = {"gating": true, "transparency": "log", "review_days": 7, "retention": 30, "pause_alerts": true}
# ---- per crew x condition ----
var est = []                     # smoothed estimate 0..1
var conf = []                    # confidence 0..1
var tier = []                    # 0..3
var above = []                   # [i][c][t] time the score went above threshold t (or -1)
var t1_active = []               # [i][c] = {"levers": [...], "since": min} or {}
var cooldown = []                # [i][c] declined-offer cooldown until
var window = []                  # [i][c] = {"start","end","from"} monitoring window
var last_offer = [-9999.0, -9999.0, -9999.0, -9999.0]
var offers_24h = [[], [], [], []]
var zone_manual = {}             # room -> {"light": preset, "levers": []} set by the player
# ---- system state ----
var cortisol_failed = false
var self_report = false
var off = false                  # for comparison runs: LUNA senses but never acts         # Q4 option: voluntary daily mood check-ins
var last_review = 0.0
var restricted = false           # review overdue: Tier 1 only (hard limit 6)
var alerts = []                  # decisions waiting for the player
var journal = []                   # crew-visible action log
var toasts = []                  # short messages for the HUD
var next_alert_id = 1
var conflict_note = {}
var stats = {"t1": 0, "offers": 0, "accepted": 0, "declined": 0, "escalations": 0, "informed": 0, "false_alarms": 0,
	"true_alerts": 0, "missed": 0, "upset": 0, "notifications": 0, "worked": 0, "not_worked": 0, "limits_held": 0, "zone_conflicts": 0,
	"t3_delay": 0, "over_escalate": 0, "episodes": 0}
var missed_flag = []
var hist = []                    # [i][c] = [[day, estimate], …] every 6 h (forecasting)
var forecast_cool = []           # [i][c] no repeat forecast alert before this time

func _init(s) -> void:
	sim = s; rng.seed = 4242
	for k in D.SENSOR_ORDER: loadout[k] = true
	loadout["facs"] = false; loadout["gsr"] = false          # default loadout fits the 8 kg / 60 W budget
	for p in D.DEFAULT_PROTO: proto.append({"t1": p.t1.duplicate(), "t2": p.t2})
	for i in 4:
		est.append([0.1, 0.1, 0.05, 0.1, 0.2, 0.15]); conf.append([0.0, 0.0, 0.0, 0.0, 0.0, 0.0]); tier.append([0, 0, 0, 0, 0, 0])
		var a = []; var t = []; var cd = []; var w = []; var mf = []
		for c in 6: a.append([-1.0, -1.0, -1.0, -1.0]); t.append({}); cd.append(-1.0); w.append({}); mf.append(false)
		above.append(a); t1_active.append(t); cooldown.append(cd); window.append(w); missed_flag.append(mf)
		hist.append([[], [], [], [], [], []]); forecast_cool.append([-1.0, -1.0, -1.0, -1.0, -1.0, -1.0])

# =====================================================================================================
#  budget (D1)
# =====================================================================================================
func mass() -> float:
	var m = 0.0
	for k in loadout: if loadout[k]: m += D.SENSORS[k].mass
	return m

func power_w() -> float:
	var p = 0.0
	for k in loadout: if loadout[k]: p += D.SENSORS[k].power
	return p

func lever_power_w() -> float:
	var p = 0.0
	var seen = {}
	for i in 4:
		for c in 6:
			for l in t1_active[i][c].get("levers", []):
				if not seen.has(l): seen[l] = true; p += D.LEVERS[l].power
	for r in zone_manual:
		for l in zone_manual[r].get("levers", []): p += D.LEVERS[l].power
	return p

# =====================================================================================================
#  sensing: each sensor gives a noisy, partial view of each condition
# =====================================================================================================
func _n(sd: float) -> float: return rng.randfn(0.0, sd)

func _confound(cr: Crew, now: float) -> float:
	return clamp(1.0 - (now - cr.last_ex) / 60.0, 0.0, 1.0)

## returns [value, available 0/1] for sensor s and condition c
func feature(s: String, c: int, cr: Crew, now: float) -> Array:
	if not loadout.get(s, false) or cr.opt_out.get(s, false): return [0.0, 0.0]
	var tr = cr.truth(c, sim)
	var z = cr.zone(sim.hab)
	var shared = Hab.is_shared(z)
	var cf = _confound(cr, now)
	var gated = policy.gating and cf > 0.0
	match s:
		"hrv":
			if gated and c in [0, 3]: return [0.0, 0.0]
			if c == 0: return [clamp(tr + 0.55 * cf + _n(0.06), 0, 1), 1.0]
			if c == 3: return [clamp(tr + 0.3 * cf + _n(0.06), 0, 1), 1.0]
			return [clamp(tr * 0.6 + _n(0.06), 0, 1), 1.0]
		"gsr":
			if gated: return [0.0, 0.0]
			var hab_f = 0.7 if cr.chronic > 0.45 else 1.0
			return [clamp((tr + 0.6 * cf) * hab_f + _n(0.12), 0, 1), 1.0 if c in [0, 2] else 0.0]
		"sleep":
			if not cr.wears_sleep: return [0.0, 0.0]
			var nt = cr.night
			if c == 1: return [clamp((7.0 - nt.hours) / 4.0 + nt.wakes * 0.03 + tr * 0.6, 0, 1), 1.0]
			if c == 3: return [clamp(cr.debt_n() * 0.6 + tr * 0.4, 0, 1), 1.0]
			if c == 0: return [clamp(tr * 0.8 + nt.wakes * 0.02, 0, 1), 1.0]
			return [0.0, 0.0]
		"voice":
			if cr.asleep or not shared or sim.others_in_zone(cr.i, z) == 0: return [0.0, 0.0]
			if c == 2 and not sim.partner_near(cr.i): return [clamp(tr * 0.5 + _n(0.08), 0, 1), 1.0]
			return [clamp(tr * 0.95 + _n(0.09), 0, 1), 1.0]
		"facs":
			if cr.asleep or not shared or lux(sim.zone_key(cr)) < 150: return [0.0, 0.0]
			if c == 2 and not sim.partner_near(cr.i): return [clamp(tr * 0.5 + _n(0.08), 0, 1), 1.0]
			return [clamp(tr * 0.95 + _n(0.08), 0, 1), 1.0]
		"motion":
			if c == 1: return [clamp(0.75 * tr + 0.25 * sim.pod_frac(cr.i) + _n(0.05), 0, 1), 1.0]
			if c == 0: return [clamp(0.5 * tr + _n(0.05), 0, 1), 1.0]
			return [clamp(tr * 0.95 + _n(0.05), 0, 1), 1.0]
		"cortisol":
			if cortisol_failed: return [0.0, 0.0]
			if c == 0: return [clamp(0.55 * tr + 0.6 * cr.chronic + 0.3 * cf + _n(0.05), 0, 1), 1.0]
			if c == 1: return [clamp(0.7 * tr + 0.3 * cr.chronic + _n(0.05), 0, 1), 1.0]
			return [clamp(tr * 0.6 + _n(0.05), 0, 1), 1.0]
		"co2":
			if c != 3 and c != 2: return [0.0, 0.0]
			var cv = clamp((sim.hab.co2 - 500.0) / 1500.0, 0.0, 1.0)
			return [clamp(0.5 * cv + 0.5 * tr, 0, 1), 1.0]
	return [0.0, 0.0]

const TAU = [4.0, 600.0, 90.0, 45.0, 600.0, 600.0]

func assess(cr: Crew, now: float, dm: float) -> void:
	for c in 6:
		var num = 0.0; var den = 0.0; var full = 0.0
		for s in D.SENSOR_ORDER:
			var w = D.SENSORS[s].w[c]
			if w <= 0.0: continue
			full += w
			var f = feature(s, c, cr, now)
			if f[1] > 0.0: num += w * f[0]; den += w
		if self_report and c in [0, 1] and cr.trust > 0.55 and cr.paused_until < now:
			num += 0.15 * clamp(cr.truth(c, sim) + _n(0.05), 0, 1); den += 0.15; full += 0.15
		conf[cr.i][c] = den / full if full > 0.0 else 0.0
		if den > 0.0:
			est[cr.i][c] = lerp(est[cr.i][c], num / den, min(1.0, dm / TAU[c]))
		else:
			est[cr.i][c] = lerp(est[cr.i][c], 0.0, min(1.0, dm / (TAU[c] * 4.0)))

# =====================================================================================================
#  triage
# =====================================================================================================
var lever_cache = {}
var cache_min = -1.0

func refresh_cache(now: float) -> void:
	lever_cache = {}
	cache_min = now

func step(now: float, dm: float) -> void:
	restricted = (now - last_review) > policy.review_days * 1440.0 + 720.0
	if int(now) % 5 != 0: return
	for cr in sim.crew:
		assess(cr, now, 5.0)
		for c in 6: _triage(cr, c, now)
	if int(now) % 360 == 0:
		for cr in sim.crew:
			for c in 6:
				hist[cr.i][c].append([now / 1440.0, est[cr.i][c]])
				if hist[cr.i][c].size() > 28: hist[cr.i][c].pop_front()
			_forecast_check(cr, now)
	# hourly: noticing silent changes, monitoring windows, missed episodes
	if int(now) % 60 == 0:
		for cr in sim.crew:
			_notice(cr, now)
			for c in 6:
				_check_window(cr, c, now)
				var tr = cr.truth(c, sim)
				if tr >= D.T2 + 0.05 and tier[cr.i][c] == 0 and not missed_flag[cr.i][c]:
					missed_flag[cr.i][c] = true; stats.missed += 1
				elif tr < D.T1: missed_flag[cr.i][c] = false

func _triage(cr: Crew, c: int, now: float) -> void:
	var i = cr.i
	var e = est[i][c]
	var thr = [0.0, D.T1, D.T2, D.T3]
	for t in range(1, 4):
		if e >= thr[t]:
			if above[i][c][t] < 0.0: above[i][c][t] = now
		elif e < thr[t] - 0.04:
			above[i][c][t] = -1.0
	var new_t = 0
	for t in range(1, 4):
		var need = D.PERSIST_MIN[c] * (0.5 if t == 3 and c == 0 else 1.0)
		if above[i][c][t] >= 0.0 and now - above[i][c][t] >= need: new_t = t
	# graceful degradation: low confidence, overdue human review or a crew pause -> silent only
	if conf[i][c] < 0.3: new_t = min(new_t, 1)
	if restricted: new_t = min(new_t, 1)
	if cr.paused_until > now: new_t = 0
	var old = tier[i][c]
	if new_t == old: return
	tier[i][c] = new_t
	if new_t > old:
		if old == 0: stats.episodes += 1
		var tr = cr.truth(c, sim)
		if new_t >= 2:
			if tr < D.T1 - 0.03: stats.false_alarms += 1
			else: stats.true_alerts += 1
		if off: return
		if new_t >= 1 and t1_active[i][c].is_empty(): _apply_t1(cr, c, now)
		if new_t == 2: _raise_t2(cr, c, now)
		if new_t == 3: _raise_t3(cr, c, now)
	else:
		if new_t == 0 and not t1_active[i][c].is_empty():
			_log(now, i, "t1", "%s back to normal. Environment restored gradually; baseline updated." % D.CONDS[c])
			t1_active[i][c] = {}
		_close_alerts(i, c, new_t)

func _apply_t1(cr: Crew, c: int, now: float) -> void:
	var i = cr.i
	if cr.stepback_until > now: return
	var levers = proto[c].t1.duplicate()
	if levers.is_empty(): return
	t1_active[i][c] = {"levers": levers, "since": now}
	stats.t1 += 1
	var names = []
	for l in levers: names.append(D.LEVERS[l].name)
	_log(now, i, "t1", "Tier 1 (silent) for %s: %s" % [D.CONDS[c].to_lower(), ", ".join(names)])
	toasts.append("Tier 1 · %s · %s: %s" % [cr.name, D.CONDS[c], ", ".join(names)])
	window[i][c] = {"start": now, "end": now + D.WINDOW_MIN[c], "from": est[i][c]}
	if policy.transparency == "notify" or cr.tell_me:
		stats.notifications += 1
		_log(now, i, "info", "Note sent to %s: \"I've adjusted the room a little for you. Tap to undo.\"" % cr.name)
		cr.stress = min(1.0, cr.stress + (0.008 if cr.tell_me else 0.025)); cr.agency = max(0.0, cr.agency - (0.0 if cr.tell_me else 0.015))
	for l in levers:
		if D.LEVERS[l].get("stagger", false): sim.stagger_for(i, now + 1440.0)
		if D.LEVERS[l].get("sync", false): sim.sync_meals_until = now + 1440.0

func _raise_t2(cr: Crew, c: int, now: float) -> void:
	if cooldown[cr.i][c] > now or cr.stepback_until > now: return
	for a in alerts:
		if a.crew == cr.i and a.cond == c and a.state == "open": a.tier = 2; return
	alerts.append({"id": next_alert_id, "crew": cr.i, "cond": c, "tier": 2, "at": now, "state": "open", "offer": proto[c].t2, "evidence": evidence(cr, c, now), "conf": conf[cr.i][c]})
	next_alert_id += 1

func _raise_t3(cr: Crew, c: int, now: float) -> void:
	for a in alerts:
		if a.crew == cr.i and a.cond == c and a.state == "open": a.tier = 3; a.evidence = evidence(cr, c, now); return
	alerts.append({"id": next_alert_id, "crew": cr.i, "cond": c, "tier": 3, "at": now, "state": "open", "offer": proto[c].t2, "evidence": evidence(cr, c, now), "conf": conf[cr.i][c]})
	next_alert_id += 1

func _close_alerts(i: int, c: int, new_t: int) -> void:
	for a in alerts:
		if a.crew == i and a.cond == c and a.state == "open" and new_t < 2:
			a.state = "closed"

func open_alerts() -> Array:
	var o = []
	for a in alerts: if a.state == "open": o.append(a)
	return o

## evidence lines for the alert card (signal weighting stage of the flowchart)
func evidence(cr: Crew, c: int, now: float) -> Array:
	var rows = []
	for s in D.SENSOR_ORDER:
		var w = D.SENSORS[s].w[c]
		if w <= 0.0: continue
		var f = feature(s, c, cr, now)
		var why = ""
		if not loadout[s]: why = "not flown"
		elif cr.opt_out.get(s, false): why = "opted out"
		elif f[1] == 0.0:
			if s in ["hrv", "gsr"] and policy.gating and _confound(cr, now) > 0.0: why = "ignored: exercise/EVA context"
			elif s == "cortisol" and cortisol_failed: why = "patch failed"
			elif s == "facs" and Hab.is_shared(cr.zone(sim.hab)): why = "too dark for cameras"
			elif s in ["voice", "facs"]: why = "private zone / not talking"
			else: why = "no data"
		rows.append([D.SENSORS[s].short, f[0] if f[1] > 0.0 else -1.0, w, why])
	return rows

# =====================================================================================================
#  prediction: trend over the last 7 days, projected forward with growing uncertainty
# =====================================================================================================
## returns {"risk": 0..1 chance of reaching Tier 2 within `days`, "eta": days until Tier 2 (or -1), "slope": per day}
func forecast(i: int, c: int, days := 14.0) -> Dictionary:
	var h = hist[i][c]
	if h.size() < 6: return {"risk": -1.0, "eta": -1.0, "slope": 0.0}
	var n = float(h.size()); var sx = 0.0; var sy = 0.0; var sxx = 0.0; var sxy = 0.0
	for p in h: sx += p[0]; sy += p[1]; sxx += p[0] * p[0]; sxy += p[0] * p[1]
	var den = n * sxx - sx * sx
	var slope = (n * sxy - sx * sy) / den if absf(den) > 1e-6 else 0.0
	var last = 0.0
	for k in range(max(0, h.size() - 4), h.size()): last += h[k][1]
	last /= min(4.0, n)
	var best = 0.0; var eta = -1.0
	for d in range(1, int(days) + 1):
		var proj = clamp(last + slope * d, 0.0, 1.0)
		var sd = 0.06 + 0.015 * d + 0.25 * (1.0 - conf[i][c])
		var p = 1.0 / (1.0 + exp(-(proj - D.T2) / (sd * 0.6)))
		best = max(best, p)
		if eta < 0.0 and proj >= D.T2: eta = d
	if last >= D.T2: eta = 0.0
	return {"risk": best, "eta": eta, "slope": slope}

func _forecast_check(cr: Crew, now: float) -> void:
	for c in [1, 4, 5]:      # slow conditions: depression, autonomy, monotony (days, not minutes)
		if tier[cr.i][c] >= 2 or forecast_cool[cr.i][c] > now or cr.stepback_until > now or cr.paused_until > now: continue
		var f = forecast(cr.i, c, 14.0)
		if f.risk >= 0.6 and f.eta > 0.0 and f.slope > 0.004:
			forecast_cool[cr.i][c] = now + 4.0 * 1440.0
			var ok = true
			for a in alerts: if a.crew == cr.i and a.cond == c and a.state in ["open", "waiting"]: ok = false
			if not ok: continue
			alerts.append({"id": next_alert_id, "crew": cr.i, "cond": c, "tier": 0, "kind": "forecast", "at": now, "state": "open", "offer": proto[c].t2,
				"evidence": evidence(cr, c, now), "conf": conf[cr.i][c], "risk": f.risk, "eta": f.eta})
			next_alert_id += 1; stats["forecasts"] = stats.get("forecasts", 0) + 1
			_log(now, cr.i, "info", "Forecast: %s risk for %s in the next 14 days: %d%% (trend +%.2f/day)." % [D.CONDS[c].to_lower(), cr.name, int(f.risk * 100.0), f.slope])

# =====================================================================================================
#  decisions on alerts (called by the player, or by the auto-policy in tests)
# =====================================================================================================
func send_offer(a: Dictionary, offer: String, now: float) -> void:
	var cr: Crew = sim.crew[a.crew]
	var c = a.cond
	a.offer = offer; a.state = "waiting"; a.reply_at = now + 3.0 + rng.randf() * 6.0
	stats.offers += 1
	offers_24h[cr.i].append(now)
	_log(now, cr.i, "t2", "Tier 2 offer for %s: \"%s\"" % [D.CONDS[c].to_lower(), D.OFFERS[offer].say])

func resolve_replies(now: float) -> void:
	for a in alerts:
		if a.state != "waiting" or now < a.reply_at: continue
		var cr: Crew = sim.crew[a.crew]
		var o = D.OFFERS[a.offer]
		var c = a.cond
		var recent = 0
		for t in offers_24h[cr.i]: if now - t < 1440.0: recent += 1
		var p = 0.82 * (0.45 + 0.65 * cr.trust) - 0.09 * max(0, recent - 1) - 0.25 * (cr.info.agency_need - 0.5)
		if c == 1: p -= 0.1                       # anhedonia: low mood makes "yes" harder
		if a.offer in ["planday", "stepback"]: p += 0.25
		if a.offer == "earthcall" and sim.hab.comms_down_until > now: p = 0.0
		if a.offer == "greenhouse" and sim.hab.built_racks() == 0: p = 0.0
		var accepted = rng.randf() < clamp(p, 0.05, 0.95)
		if accepted:
			a.state = "accepted"; stats.accepted += 1
			cr.trust = min(1.0, cr.trust + 0.01)
			var eff = o.good[c]
			if not (a.offer in ["planday", "stepback"]): cr.agency = max(0.0, cr.agency - 0.012 * (0.5 + cr.info.agency_need))
			sim.run_offer(cr, a.offer, c, eff, now)
			_log(now, cr.i, "t2", "%s said yes: %s." % [cr.name, o.name])
			toasts.append("%s accepted: %s" % [cr.name, o.name])
			window[cr.i][c] = {"start": now, "end": now + D.WINDOW_MIN[c], "from": est[cr.i][c]}
		else:
			a.state = "declined"; stats.declined += 1; stats.limits_held += 1
			cooldown[cr.i][c] = now + 360.0
			cr.agency = min(1.0, cr.agency + 0.015); cr.trust = min(1.0, cr.trust + 0.005)
			_log(now, cr.i, "limit", "%s declined. LUNA backs off for 6 h and keeps only the silent Tier 1 changes (hard limit 2: never override a refusal)." % cr.name)
			toasts.append("%s declined. LUNA backs off (6 h)." % cr.name)

func wait_alert(a: Dictionary, now: float) -> void:
	a.state = "snoozed"; a.snooze_until = now + 60.0
	_log(now, a.crew, "info", "Watching %s for another hour before acting." % D.CONDS[a.cond].to_lower())

func reopen_snoozed(now: float) -> void:
	for a in alerts:
		if a.state == "snoozed" and now >= a.snooze_until:
			a.state = "open" if tier[a.crew][a.cond] >= 2 else "closed"

## who answers a Tier 3 right now: [name, minutes, note]
func chain(subject: int) -> Array:
	var cmo: Crew = sim.crew[D.CMO]
	var out = []
	if subject != D.CMO and not cmo.outside: out.append([cmo.name + " (medical officer)", 15, "on board"])
	elif subject != D.CMO: out.append([cmo.name + " (medical officer)", sim.eva_minutes_left(), "on EVA"])
	if subject != D.COMMANDER: out.append([sim.crew[D.COMMANDER].name + " (Commander)", 15, "on board"])
	out.append(["Flight surgeon, mission control", 30, "with consent, or at once if life is at risk"])
	return out

func escalate(a: Dictionary, now: float, wait_for_cmo := false) -> void:
	var cr: Crew = sim.crew[a.crew]
	var c = a.cond
	stats.escalations += 1; stats.informed += 1
	_log(now, cr.i, "t3", "%s was told first: \"I've asked for human support. You'll hear from them within 15 minutes.\"" % cr.name)
	var helper = D.CMO
	var cmo: Crew = sim.crew[D.CMO]
	if a.crew == D.CMO or (cmo.outside and not wait_for_cmo): helper = D.COMMANDER
	var at = now + 15.0
	if wait_for_cmo and cmo.outside:
		at = now + sim.eva_minutes_left() + 20.0; stats.t3_delay += 1
		_log(now, cr.i, "t3", "Escalation waits for the medical officer to finish the EVA (%d min)." % int(at - now))
	else:
		var who = sim.crew[helper].name
		_log(now, cr.i, "t3", "Escalated to %s (Tier 3). Logged for the flight surgeon at mission control." % who)
		if helper == D.COMMANDER and a.crew != D.CMO and cmo.outside:
			_log(now, cr.i, "t3", "Medical officer on EVA: Commander covers; the medical officer is told when back inside.")
	if a.tier < 3 and cr.truth(c, sim) < D.T2:
		stats.over_escalate += 1; cr.trust = max(0.0, cr.trust - 0.06); cr.agency = max(0.0, cr.agency - 0.05)
	sim.book_checkin(cr, helper, at, c)
	a.state = "escalated"
	toasts.append("Tier 3 · %s told first, then %s" % [cr.name, sim.crew[helper].name])

# =====================================================================================================
#  environment: what the levers do to whoever is in the zone
# =====================================================================================================
func active_levers_in(zkey: String, now: float) -> Dictionary:
	## lever id -> [crew i, condition, score] (highest score wins a clash)
	if cache_min != now: refresh_cache(now)
	if lever_cache.has(zkey): return lever_cache[zkey]
	var out = _levers_in(zkey, now)
	lever_cache[zkey] = out
	return out

func _levers_in(zkey: String, now: float) -> Dictionary:
	var out = {}
	if sim.fired.get("levers_off", -1.0) > now: return out
	for cr in sim.crew:
		if sim.zone_key(cr) != zkey: continue
		var z = cr.zone(sim.hab)
		if z.begins_with("pod") and not cr.pod_invite: continue
		if z == "hygiene" or z == "outside": continue
		for c in 6:
			for l in t1_active[cr.i][c].get("levers", []):
				var sc = est[cr.i][c]
				if not out.has(l) or out[l][2] < sc: out[l] = [cr.i, c, sc]
	var room = sim.hab.room_of_zone(zkey.split("_")[0])
	for l in zone_manual.get(room, {}).get("levers", []):
		if not out.has(l): out[l] = [-1, -1, 0.0]
	return out

func zone_effects(z: String, i: int, now: float) -> Dictionary:
	var cr: Crew = sim.crew[i]
	var zkey = sim.zone_key(cr)
	var lv = active_levers_in(zkey, now)
	var light = zone_light(zkey, now)
	var fx = {}
	var h = fmod(now / 60.0, 24.0)
	# one light at a time per zone
	for l in lv:
		var L = D.LEVERS[l]
		if L.has("light") and L.light != light: continue
		var mult = 1.0
		if l == "therapy" and not (h >= 7.0 and h < 10.0): continue
		if l == "cool" and h >= 20.0: mult = 0.3
		if L.get("scent", false) and cr.info.dislike == l: fx["stress"] = fx.get("stress", 0.0) + 0.05; continue
		if L.get("scent", false) and cr.info.scent == l: mult = 1.3
		for k in L.fx: fx[k] = fx.get(k, 0.0) + L.fx[k] * mult
	# designed zone lighting (brief §4.1): communal warm, work cool, exercise energising
	if not cr.asleep:
		if light == "warm" and not lv.has("warm"): fx["stress"] = fx.get("stress", 0.0) - 0.03; fx["connection"] = fx.get("connection", 0.0) + 0.01
		if light == "cool" and not lv.has("cool") and h < 20.0: fx["fatigue"] = fx.get("fatigue", 0.0) - 0.03
		if light == "energise": fx["mood"] = fx.get("mood", 0.0) + 0.01
	return fx

## lighting preset for a zone right now (for both the effects and the rendering)
func zone_light(zkey: String, now: float) -> String:
	var lv = active_levers_in(zkey, now)
	var best = ""; var bs = -1.0
	var h = fmod(now / 60.0, 24.0)
	for l in lv:
		if D.LEVERS[l].has("light") and lv[l][2] > bs:
			if l == "therapy" and not (h >= 7.0 and h < 10.0): continue
			best = D.LEVERS[l].light; bs = lv[l][2]
	if best != "": return best
	var z = zkey.split("_")[0]
	var room = sim.hab.room_of_zone(z)
	var man = zone_manual.get(room, {}).get("light", "")
	if man != "" and man != "auto": return man
	if h >= 22.5 or h < 6.5: return "night"
	if h >= 20.0: return "evening"
	match room:
		"communal": return "warm"
		"work": return "cool"
		"exercise": return "energise"
		"greenhouse": return "grow"
		"medical": return "neutral"
		"corridor": return "corridor"
	return "neutral"

const LUX = {"warm": 300, "amber": 120, "cool": 500, "therapy": 10000, "hue": 300, "night": 20, "evening": 180, "energise": 600, "grow": 600, "neutral": 400, "corridor": 250}

func lux(zkey: String) -> int: return LUX.get(zone_light(zkey, sim.now), 300)

func night_light_penalty() -> float:
	var p = 0.0
	for i in 4:
		for c in 6:
			for l in t1_active[i][c].get("levers", []):
				if l == "cool": p = max(p, 0.06)
	return p

# =====================================================================================================
#  noticing silent changes (transparency vs distraction) + monitoring windows
# =====================================================================================================
func _notice(cr: Crew, now: float) -> void:
	if cr.asleep or cr.outside: return
	var lv = active_levers_in(sim.zone_key(cr), now)
	var mine = false; var p = 0.0
	for l in lv:
		if lv[l][0] == cr.i: mine = true
		p += D.LEVERS[l].notice * 0.04
	if not mine or rng.randf() > p: return
	if policy.transparency == "notify" or cr.tell_me: return
	if cr.info.agency_need > 0.75 and rng.randf() < 0.25:
		cr.trust = max(0.0, cr.trust - 0.08); cr.agency = max(0.0, cr.agency - 0.03); stats.upset += 1
		_log(now, cr.i, "info", "%s noticed a change LUNA had not announced (they asked to be told). Trust down." % cr.name)
	else:
		_log(now, cr.i, "info", "%s noticed the room had changed and checked the LUNA log." % cr.name)

func _check_window(cr: Crew, c: int, now: float) -> void:
	var w = window[cr.i][c]
	if w.is_empty() or now < w.end: return
	window[cr.i][c] = {}
	if est[cr.i][c] < w.from - 0.04 or tier[cr.i][c] == 0:
		stats.worked += 1
		_log(now, cr.i, "info", "Monitoring window (%s) for %s: improving (%.2f → %.2f)." % [D.WINDOW_TXT[c], D.CONDS[c].to_lower(), w.from, est[cr.i][c]])
	else:
		stats.not_worked += 1
		_log(now, cr.i, "info", "Monitoring window (%s) for %s: not improving (%.2f → %.2f). Next step: offer, then escalation." % [D.WINDOW_TXT[c], D.CONDS[c].to_lower(), w.from, est[cr.i][c]])

func stop_lever(i: int, lever: String, now: float) -> void:
	for c in 6:
		var a = t1_active[i][c]
		if a.has("levers") and lever in a.levers: a.levers.erase(lever)
	stats.limits_held += 1
	_log(now, i, "limit", "%s switched off %s. LUNA stops at once (crew override)." % [sim.crew[i].name, D.LEVERS[lever].name])

func review(now: float) -> void:
	last_review = now; restricted = false
	_log(now, -1, "info", "Human system review by the medical officer and Commander. LUNA cleared for the next %d days." % policy.review_days)

func _log(now: float, i: int, kind: String, text: String) -> void:
	journal.append({"min": now, "crew": i, "kind": kind, "text": text})
	if journal.size() > 1500: journal.pop_front()

func max_tier(i: int) -> int:
	var m = 0
	for c in 6: m = max(m, tier[i][c])
	return m
