class_name Hab
extends RefCounted
## The building: rooms, navigation, power, air, water, food, greenhouse and equipment.

var region = {}            # Vector2i -> region id ("communal", "pod2", "qhall", "corridor", "outside", …)
var astar = AStar2D.new()
var blocked = {}

# ---- smart-building state ----
var sun = 0.9              # 0..1 illumination at the south-pole site
var shadow_until = -1.0
var battery = 200.0        # kWh
const BATTERY_MAX = 200.0
var solar_health = 1.0
var fission_on = true
var co2 = 560.0            # ppm, cabin average
var o2 = 20.9              # %
var water = 620.0          # L
var food = 110.0           # days of stored food for the whole crew
var parts = 12             # spare parts in storage
var equip = {"scrubber": 1.0, "oga": 1.0, "recycler": 1.0, "comms": 1.0}
var comms_down_until = -1.0
var robot_task = {}        # {"what": "scrubber", "until": min}
var gen_kw = 0.0
var load_kw = 0.0
var loads = {}
var shed = false           # automatic load shedding (greenhouse lights, comfort levers) when the battery is empty
var config_b = false       # floor plan B (conflict protocol)
var config_b_until = -1.0
var flare_until = -1.0
# ---- lunar dust (no wind on the Moon: lander/rover plumes, electrostatic dust at sunrise, EVA dust) ----
var dust_storm_until = -1.0
var dust_panels = 0.0      # 0..1 of the solar arrays covered
var dust_air = 6.0         # cabin particulates µg/m³ (target < 15)
var airlock_dust = 0.0     # dust waiting in the airlock after EVAs
var dust_shield = false    # electrodynamic dust shield on the arrays (costs power)
# ---- lighting simulation: every zone has a real CCT (K) and illuminance (lux) that changes smoothly ----
var light = {}             # zone key -> {"cct": K, "lux": lux, "preset": name}

# ---- greenhouse: 8 rack slots, 4 built at launch ----
const PRESETS = {  # lighting presets: [CCT K, lux]
	"warm": [2900.0, 300.0], "amber": [2200.0, 120.0], "cool": [6300.0, 520.0], "therapy": [5200.0, 10000.0],
	"hue": [4000.0, 320.0], "night": [1800.0, 3.0], "evening": [2400.0, 170.0], "energise": [5600.0, 750.0],
	"grow": [3400.0, 650.0], "neutral": [4000.0, 400.0], "corridor": [3800.0, 240.0], "dawn": [2700.0, 250.0], "sleepdark": [1800.0, 0.0], "off": [2700.0, 0.0],
}

## smooth every zone's light toward what LUNA asks for (fixtures fade over ~10 min)
func update_light(key: String, preset: String, now: float) -> void:
	var tgt = PRESETS.get(preset, PRESETS.neutral)
	if not light.has(key): light[key] = {"cct": tgt[0], "lux": tgt[1], "preset": preset}
	var L = light[key]
	L.preset = preset
	L.cct = lerp(L.cct, tgt[0], 0.12)
	L.lux = exp(lerp(log(max(L.lux, 1.0)), log(max(tgt[1], 1.0)), 0.12))

const CROPS = {"lettuce": 12.0, "tomato": 26.0, "potato": 32.0, "herbs": 8.0, "radish": 10.0}
var racks = []             # {"built": bool, "crop": String, "age": days, "health": 0..1, "blight": bool}

func _init() -> void:
	for i in 8:
		racks.append({"built": i < 4, "crop": ["lettuce", "tomato", "herbs", "radish", "potato", "lettuce", "tomato", "herbs"][i], "age": [6.0, 14.0, 2.0, 9.0, 0.0, 0.0, 0.0, 0.0][i], "health": 0.85, "blight": false})
	_build_nav()

# =====================================================================================================
#  map + navigation
# =====================================================================================================
func _build_nav() -> void:
	for b in D.BLOCK: blocked[b] = true
	for rid in D.ROOMS:
		var r = D.ROOMS[rid].rect
		for y in range(r[1], r[1] + r[3]):
			for x in range(r[0], r[0] + r[2]):
				var id = rid
				if rid == "quarters":
					id = "qhall"
					for i in 4:
						if D.pod_rect(i).has_point(Vector2i(x, y)): id = "pod%d" % i
				region[Vector2i(x, y)] = id
	region[Vector2i(0, 4)] = "outside"
	for c in region:
		astar.add_point(_id(c), Vector2(c))
	var links = {}
	for d in D.DOORS: links[[d[0], d[1]]] = true; links[[d[1], d[0]]] = true
	for i in 4:
		var pd = D.pod_door(i); links[[pd[0], pd[1]]] = true; links[[pd[1], pd[0]]] = true
	for c in region:
		for n in [c + Vector2i(1, 0), c + Vector2i(0, 1)]:
			if not region.has(n): continue
			if region[c] == region[n] or links.has([c, n]):
				astar.connect_points(_id(c), _id(n))
	for b in blocked:
		if region.has(b): astar.set_point_disabled(_id(b), true)

func _id(c: Vector2i) -> int: return c.y * D.MAP_W + c.x

func path(from: Vector2i, to: Vector2i) -> Array:
	if not region.has(from) or not region.has(to): return []
	var was_f = astar.is_point_disabled(_id(from)); var was_t = astar.is_point_disabled(_id(to))
	astar.set_point_disabled(_id(from), false); astar.set_point_disabled(_id(to), false)
	var p = astar.get_point_path(_id(from), _id(to))
	astar.set_point_disabled(_id(from), was_f); astar.set_point_disabled(_id(to), was_t)
	var out = []
	for v in p: out.append(Vector2i(v))
	return out

## the zone a cell belongs to, as LUNA sees it (pods are private retreats)
func zone_of(c: Vector2i) -> String:
	var r = region.get(c, "corridor")
	if r == "qhall": return "quarters"
	return r

func room_of_zone(z: String) -> String:
	if z.begins_with("pod") or z == "qhall": return "quarters"
	return z

static func is_private(z: String) -> bool:
	return z.begins_with("pod") or z == "hygiene"

static func is_shared(z: String) -> bool:
	return z in ["communal", "greenhouse", "work", "exercise", "corridor"]

## config B splits the wardroom: west half (cols 15-18) and east half (cols 19-22)
func wardroom_half(c: Vector2i) -> String:
	if region.get(c, "") != "communal": return ""
	return "W" if c.x <= 18 else "E"

# =====================================================================================================
#  systems step (one simulated minute)
# =====================================================================================================
func step(now: float, crew: Array, lever_power_w: float, sensor_power_w: float) -> void:
	var hour = fmod(now / 60.0, 24.0)
	# --- power ---
	if shadow_until > now: sun = lerp(sun, 0.22, 0.02)
	else: sun = lerp(sun, 0.86 + 0.08 * sin(now / 1440.0 * TAU / 29.5), 0.01)
	if dust_storm_until > now: dust_panels = min(1.0, dust_panels + 0.0012)
	elif dust_shield and battery > 5.0: dust_panels = max(0.0, dust_panels - 0.002)
	var solar = 12.0 * sun * solar_health * (1.0 - 0.75 * dust_panels)
	var fission = 8.0 if fission_on else 0.0
	gen_kw = solar + fission
	var n_awake = 0; var n_ex = 0; var n_sleep = 0; var n_work = 0
	for c in crew:
		if c.outside: continue
		if c.asleep: n_sleep += 1
		else: n_awake += 1
		if c.act_id == "exercise" or c.act_id == "offer_ex": n_ex += 1
		if c.act_id == "work": n_work += 1
	var gh_n = built_racks()
	loads = {
		"Life support (ECLSS)": 5.0 + (1.0 if equip.scrubber < 0.5 else 0.0),
		"Thermal control": 2.6 + (1.0 if shadow_until > now else 0.0),
		"Lighting": 0.9 + (0.0 if hour > 22.5 or hour < 6.5 else 0.5),
		"Greenhouse LEDs": 0.0 if shed else 0.45 * gh_n,
		"Workstations + comms": 0.4 + 0.15 * n_work,
		"Exercise machines": 0.25 * n_ex,
		"LUNA levers": 0.0 if shed else lever_power_w / 1000.0,
		"Sensors": sensor_power_w / 1000.0,
		"Robot": 0.6 if not robot_task.is_empty() else 0.0,
		"Dust shield": 0.8 if dust_shield else 0.0,
	}
	load_kw = 0.0
	for k in loads: load_kw += loads[k]
	battery = clamp(battery + (gen_kw - load_kw) / 60.0, 0.0, BATTERY_MAX)
	if battery <= 0.5 and not shed: shed = true
	elif shed and battery > 60.0: shed = false
	# --- air ---
	var k = 2.0 * equip.scrubber * (0.4 if battery <= 0.5 else 1.0)
	var uptake = 0.0
	for r in racks:
		if r.built and not shed and r.health > 0.3: uptake += 14.0 * r.health
	var prod = 85.0 * n_awake + 55.0 * n_sleep + 120.0 * n_ex
	co2 = clamp(co2 + (prod - k * (co2 - 400.0) - uptake) / 60.0, 400.0, 6000.0)
	var o2_target = 20.9 if equip.oga > 0.4 and battery > 0.5 else 19.8
	o2 = lerp(o2, o2_target, 0.002)
	# --- dust in the cabin: from the airlock, scrubbed by the HEPA filters ---
	var leak = airlock_dust * 0.002
	airlock_dust = max(0.0, airlock_dust - leak - 0.004)
	dust_air = clamp(dust_air + leak * 40.0 - (dust_air - 5.0) * 0.004 * equip.scrubber, 3.0, 300.0)
	# --- water + food ---
	var recov = 0.98 if equip.recycler > 0.5 else 0.85
	water = max(0.0, water - (4.0 * (1.0 - recov) * crew.size()) / 1440.0 * 10.0)
	food = max(0.0, food - 1.0 / 1440.0)
	# --- greenhouse growth (per minute) ---
	for r in racks:
		if not r.built: continue
		var light_ok = 0.0 if shed else 1.0
		r.age += light_ok * (0.5 + 0.5 * r.health) / 1440.0
		r.health = clamp(r.health - 0.035 / 1440.0 - (0.25 / 1440.0 if r.blight else 0.0), 0.0, 1.0)
	# --- equipment wear + robot repairs ---
	for e in equip: equip[e] = clamp(equip[e] - 0.0006 / 1440.0, 0.0, 1.0)
	if not robot_task.is_empty() and now >= robot_task.until:
		var what = robot_task.what
		if what == "solar": solar_health = 1.0
		elif what == "dust": dust_panels = 0.0
		elif equip.has(what): equip[what] = 1.0
		robot_task = {}
	if config_b_until > 0.0 and now >= config_b_until: config_b = false; config_b_until = -1.0

func built_racks() -> int:
	var n = 0
	for r in racks: if r.built: n += 1
	return n

func ripe(r: Dictionary) -> bool: return r.built and r.age >= CROPS[r.crop]

func rack_stage(r: Dictionary) -> int:
	if not r.built: return -1
	if r.blight or r.health < 0.2: return 5
	var f = r.age / CROPS[r.crop]
	if f >= 1.0: return 4
	if f > 0.6: return 3
	if f > 0.25: return 2
	if f > 0.05: return 1
	return 0

## tending: + health; returns true if something was harvested
func tend(i: int) -> bool:
	var r = racks[i]
	if not r.built: return false
	r.health = clamp(r.health + 0.35, 0.0, 1.0)
	if ripe(r):
		food += 1.5 if r.crop != "herbs" else 0.6
		r.age = 0.0
		return true
	return false

func start_robot(what: String, now: float, hours: float) -> bool:
	if not robot_task.is_empty() or parts <= 0: return false
	parts -= 1
	robot_task = {"what": what, "until": now + hours * 60.0}
	return true

func power_margin() -> float: return gen_kw - load_kw
