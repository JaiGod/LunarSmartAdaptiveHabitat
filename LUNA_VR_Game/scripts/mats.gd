class_name Mats
extends RefCounted
## Material library. Plain, NASA-interior surfaces: white panels, grey grip floor, Nomex fabric, anodised
## aluminium, colour-coded room stripes. Emissive only where something really emits light (screens, LEDs).

static var cache = {}

static func tex(name: String) -> Texture2D:
	var k = "tex_" + name
	if not cache.has(k): cache[k] = load("res://assets/tex/%s.png" % name)
	return cache[k]

static func _m(key: String, col: Color, rough := 0.7, metal := 0.0) -> StandardMaterial3D:
	if cache.has(key): return cache[key]
	var m = StandardMaterial3D.new()
	m.resource_name = key
	m.albedo_color = col; m.roughness = rough; m.metallic = metal
	cache[key] = m
	return m

static func _tri(m: StandardMaterial3D, t: String, scale: float) -> StandardMaterial3D:
	m.albedo_texture = tex(t)
	m.uv1_triplanar = true; m.uv1_world_triplanar = true
	m.uv1_scale = Vector3.ONE * scale
	m.uv1_triplanar_sharpness = 4.0
	return m

static func wall() -> StandardMaterial3D:
	if not cache.has("wall"): _tri(_m("wall", Color(1, 1, 1), 0.55), "wall", 0.8)
	return cache["wall"]
static func floor_m(tint := Color(1, 1, 1), key := "floor") -> StandardMaterial3D:
	if not cache.has(key): _tri(_m(key, tint, 0.8), "floor", 0.8)
	return cache[key]
static func grate() -> StandardMaterial3D:
	if not cache.has("grate"): _tri(_m("grate", Color(1, 1, 1), 0.5, 0.2), "grate", 1.6)
	return cache["grate"]
static func ceiling() -> StandardMaterial3D:
	if not cache.has("ceiling"): _tri(_m("ceiling", Color(1, 1, 1), 0.9), "ceiling", 0.8)
	return cache["ceiling"]
static func fabric(tint := Color(1, 1, 1), key := "fabric") -> StandardMaterial3D:
	if not cache.has(key): _tri(_m(key, tint, 0.95), "fabric", 2.0)
	return cache[key]
static func regolith() -> StandardMaterial3D:
	if not cache.has("regolith"):
		var m = _tri(_m("regolith", Color(1, 1, 1), 0.98), "regolith", 0.08)
		m.uv1_triplanar_sharpness = 2.0
	return cache["regolith"]
static func bags() -> StandardMaterial3D:
	if not cache.has("bags"): _tri(_m("bags", Color(1, 1, 1), 0.98), "bags", 0.35)
	return cache["bags"]
static func solar() -> StandardMaterial3D:
	if not cache.has("solar"): _tri(_m("solar", Color(1, 1, 1), 0.25, 0.15), "solar", 0.9)
	return cache["solar"]

static func metal() -> StandardMaterial3D: return _m("metal", Color(0.74, 0.76, 0.79), 0.35, 0.25)
static func dark_metal() -> StandardMaterial3D: return _m("dark_metal", Color(0.3, 0.32, 0.35), 0.5, 0.2)
static func frame() -> StandardMaterial3D: return _m("frame", Color(0.42, 0.45, 0.49), 0.5, 0.2)
static func white_plastic() -> StandardMaterial3D: return _m("white_plastic", Color(0.9, 0.91, 0.92), 0.45)
static func grey_plastic() -> StandardMaterial3D: return _m("grey_plastic", Color(0.55, 0.58, 0.62), 0.55)
static func black() -> StandardMaterial3D: return _m("black", Color(0.06, 0.07, 0.08), 0.5)
static func rubber() -> StandardMaterial3D: return _m("rubber", Color(0.12, 0.12, 0.13), 0.9)
static func handrail() -> StandardMaterial3D: return _m("handrail", Color(0.16, 0.36, 0.72), 0.4, 0.2)   # NASA-blue handrails
static func caution() -> StandardMaterial3D: return _m("caution", Color(0.95, 0.72, 0.1), 0.6)
static func red() -> StandardMaterial3D: return _m("red", Color(0.78, 0.16, 0.12), 0.5)
static func wood() -> StandardMaterial3D: return _m("wood", Color(0.62, 0.45, 0.3), 0.75)
static func cable(col: Color) -> StandardMaterial3D: return _m("cable_" + col.to_html(false), col, 0.6)
static func mattress() -> StandardMaterial3D: return _m("mattress", Color(0.82, 0.84, 0.88), 0.95)
static func blanket(col: Color) -> StandardMaterial3D: return _m("blanket_" + col.to_html(false), col, 0.95)
static func skin(col: Color) -> StandardMaterial3D: return _m("skin_" + col.to_html(false), col, 0.7)
static func suit(col: Color) -> StandardMaterial3D: return _m("suit_" + col.to_html(false), col, 0.85)
static func plant(col := Color(0.25, 0.55, 0.22)) -> StandardMaterial3D:
	var m = _m("plant_" + col.to_html(false), col, 0.8)
	return m
static func water() -> StandardMaterial3D:
	if not cache.has("water"):
		var m = _m("water", Color(0.25, 0.5, 0.8, 0.55), 0.1, 0.0)
		m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	return cache["water"]
static func glass() -> StandardMaterial3D:
	if not cache.has("glass"):
		var m = _m("glass", Color(0.75, 0.85, 0.92, 0.22), 0.05, 0.0)
		m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		m.cull_mode = BaseMaterial3D.CULL_DISABLED
	return cache["glass"]

## colour-coded room stripe (wayfinding, as on the ISS)
static func stripe(col: Color) -> StandardMaterial3D: return _m("stripe_" + col.to_html(false), col, 0.5)

## something that emits light: LEDs, light panels, screen backs
static func glow(col: Color, energy := 1.0, key := "") -> StandardMaterial3D:
	var k = key if key != "" else "glow_%s_%.1f" % [col.to_html(false), energy]
	if cache.has(k): return cache[k]
	var m = StandardMaterial3D.new()
	m.resource_name = k
	m.albedo_color = col
	m.emission_enabled = true; m.emission = col; m.emission_energy_multiplier = energy
	cache[k] = m
	return m

## a unique (not cached) emissive material whose colour the game changes at runtime (room light panels)
static func light_panel(key: String) -> StandardMaterial3D:
	var m = StandardMaterial3D.new()
	m.resource_name = key
	m.albedo_color = Color(1, 1, 1)
	m.emission_enabled = true; m.emission = Color(1, 0.95, 0.85); m.emission_energy_multiplier = 1.4
	cache[key] = m
	return m

## colour temperature (K) to RGB (Tanner Helland approximation)
static func cct(k: float) -> Color:
	var t = clamp(k, 1000.0, 40000.0) / 100.0
	var r: float; var g: float; var b: float
	if t <= 66.0:
		r = 255.0
		g = 99.4708025861 * log(t) - 161.1195681661
		b = 0.0 if t <= 19.0 else 138.5177312231 * log(t - 10.0) - 305.0447927307
	else:
		r = 329.698727446 * pow(t - 60.0, -0.1332047592)
		g = 288.1221695283 * pow(t - 60.0, -0.0755148492)
		b = 255.0
	return Color(clamp(r, 0, 255) / 255.0, clamp(g, 0, 255) / 255.0, clamp(b, 0, 255) / 255.0)
