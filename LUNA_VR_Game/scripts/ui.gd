class_name UI
extends RefCounted
## Small UI toolkit for the in-world screens (big type: readable in a headset at arm's length).

const BG = Color("#0b1430")
const PANEL = Color("#13204a")
const INK = Color("#e8f0ff")
const MUT = Color("#9fb2d8")
const ACC = Color("#38bdf8")
const OK = Color("#34d399")
const WARN = Color("#fbbf24")
const BAD = Color("#f87171")

static var theme: Theme

static func get_theme() -> Theme:
	if theme: return theme
	theme = Theme.new()
	theme.default_font_size = 22
	var sb = StyleBoxFlat.new(); sb.bg_color = Color("#1d3a78"); sb.set_corner_radius_all(6); sb.content_margin_left = 14; sb.content_margin_right = 14; sb.content_margin_top = 8; sb.content_margin_bottom = 8
	sb.border_color = Color("#4f8ef7"); sb.set_border_width_all(2)
	var sh = sb.duplicate(); sh.bg_color = Color("#2b56a8"); sh.border_color = Color("#9fd3f5")
	var sp = sb.duplicate(); sp.bg_color = Color("#3b6fd0")
	var sd = sb.duplicate(); sd.bg_color = Color("#1a2140"); sd.border_color = Color("#2b3560")
	theme.set_stylebox("normal", "Button", sb); theme.set_stylebox("hover", "Button", sh)
	theme.set_stylebox("pressed", "Button", sp); theme.set_stylebox("disabled", "Button", sd); theme.set_stylebox("focus", "Button", StyleBoxEmpty.new())
	theme.set_color("font_color", "Button", INK); theme.set_color("font_hover_color", "Button", Color.WHITE)
	theme.set_color("font_disabled_color", "Button", Color("#6b7799"))
	theme.set_color("font_color", "Label", INK)
	return theme

## root of a screen: dark background, padded vertical box
static func screen_root(title: String, subtitle := "") -> Array:
	var root = PanelContainer.new()
	root.theme = get_theme()
	var sb = StyleBoxFlat.new(); sb.bg_color = BG; sb.content_margin_left = 18; sb.content_margin_right = 18; sb.content_margin_top = 12; sb.content_margin_bottom = 12
	root.add_theme_stylebox_override("panel", sb)
	var v = VBoxContainer.new(); v.add_theme_constant_override("separation", 8)
	root.add_child(v)
	var head = HBoxContainer.new(); v.add_child(head)
	var t = lbl(title, 28, ACC); head.add_child(t)
	if subtitle != "":
		var s = lbl(subtitle, 18, MUT); s.size_flags_horizontal = Control.SIZE_EXPAND_FILL; s.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		head.add_child(s)
	var body = VBoxContainer.new(); body.add_theme_constant_override("separation", 8); body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	v.add_child(body)
	return [root, body]

static func lbl(text: String, size := 22, col := INK, wrap := false) -> Label:
	var l = Label.new(); l.text = text
	l.add_theme_font_size_override("font_size", size); l.add_theme_color_override("font_color", col)
	if wrap: l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART; l.custom_minimum_size.x = 100
	return l

static func btn(text: String, cb: Callable, hue := -1.0, size := 22, enabled := true) -> Button:
	var b = Button.new(); b.text = text
	b.action_mode = BaseButton.ACTION_MODE_BUTTON_PRESS
	b.add_theme_font_size_override("font_size", size)
	b.disabled = not enabled
	if hue >= 0.0:
		var sb = StyleBoxFlat.new(); sb.bg_color = Color.from_hsv(hue, 0.55, 0.42); sb.set_corner_radius_all(6)
		sb.border_color = Color.from_hsv(hue, 0.45, 0.85); sb.set_border_width_all(2)
		sb.content_margin_left = 14; sb.content_margin_right = 14; sb.content_margin_top = 8; sb.content_margin_bottom = 8
		var sh = sb.duplicate(); sh.bg_color = Color.from_hsv(hue, 0.6, 0.58)
		b.add_theme_stylebox_override("normal", sb); b.add_theme_stylebox_override("hover", sh); b.add_theme_stylebox_override("pressed", sh)
	b.pressed.connect(cb)
	return b

static func hbox(sep := 8) -> HBoxContainer:
	var h = HBoxContainer.new(); h.add_theme_constant_override("separation", sep); return h

static func vbox(sep := 6) -> VBoxContainer:
	var v = VBoxContainer.new(); v.add_theme_constant_override("separation", sep); return v

static func card(hue := 0.6) -> Array:
	var p = PanelContainer.new()
	var sb = StyleBoxFlat.new(); sb.bg_color = PANEL; sb.set_corner_radius_all(8); sb.content_margin_left = 12; sb.content_margin_right = 12; sb.content_margin_top = 8; sb.content_margin_bottom = 8
	sb.border_color = Color.from_hsv(hue, 0.5, 0.6); sb.border_width_left = 4
	p.add_theme_stylebox_override("panel", sb)
	var v = vbox(4); p.add_child(v)
	return [p, v]

## horizontal bar 0..1 with optional marker line
static func bar(v: float, col: Color, w := 260.0, h := 16.0, marker := -1.0) -> Control:
	var c = BarC.new(); c.val = clamp(v, 0.0, 1.0); c.col = col; c.marker = marker
	c.custom_minimum_size = Vector2(w, h)
	return c

static func spark(vals: Array, col: Color, w := 300.0, h := 70.0, lo := 0.0, hi := 1.0, proj := []) -> Control:
	var c = SparkC.new(); c.vals = vals; c.col = col; c.lo = lo; c.hi = hi; c.proj = proj
	c.custom_minimum_size = Vector2(w, h)
	return c

static func clear(n: Node) -> void:
	for ch in n.get_children():
		n.remove_child(ch); ch.queue_free()

class BarC extends Control:
	var val = 0.0
	var col = Color.WHITE
	var marker = -1.0
	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, size), Color("#22305c"))
		draw_rect(Rect2(Vector2.ZERO, Vector2(size.x * val, size.y)), col)
		if marker >= 0.0: draw_line(Vector2(size.x * marker, -2), Vector2(size.x * marker, size.y + 2), Color.WHITE, 2.0)

class SparkC extends Control:
	var vals = []
	var proj = []
	var col = Color.WHITE
	var lo = 0.0
	var hi = 1.0
	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, size), Color("#101a3a"))
		var all = vals.size() + proj.size()
		if all < 2: return
		var pts = PackedVector2Array(); var pp = PackedVector2Array()
		for k in vals.size():
			pts.append(Vector2(size.x * k / float(all - 1), size.y * (1.0 - clamp((vals[k] - lo) / max(hi - lo, 0.0001), 0.0, 1.0))))
		if vals.size() >= 2: draw_polyline(pts, col, 2.5, true)
		if not proj.is_empty() and not vals.is_empty():
			pp.append(pts[pts.size() - 1])
			for k in proj.size():
				var kk = vals.size() + k
				pp.append(Vector2(size.x * kk / float(all - 1), size.y * (1.0 - clamp((proj[k] - lo) / max(hi - lo, 0.0001), 0.0, 1.0))))
			for k in range(0, pp.size() - 1, 2):
				draw_line(pp[k], pp[min(k + 1, pp.size() - 1)], col.lerp(Color.WHITE, 0.4), 2.0)
			if pp.size() > 2: draw_polyline(pp, Color(col.r, col.g, col.b, 0.45), 1.5, true)
