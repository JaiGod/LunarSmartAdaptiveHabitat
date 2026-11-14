class_name UIK
## Small pixel UI kit: theme, panels, labels, buttons, bars. Everything is built in code.

const BG = Color("#0d1220")
const PANEL = Color("#141b2d")
const PANEL2 = Color("#1b2438")
const LINE = Color("#2f3a55")
const TEXT = Color("#d3dae6")
const DIM = Color("#8a96ad")
const ACC = Color("#5fd3e6")
const AMBER = Color("#f2a541")
const GOOD = Color("#7ddc6f")
const BAD = Color("#e5484d")

static var font: FontFile
static var theme: Theme

static func setup() -> Theme:
	font = load("res://fonts/luna_pixel.ttf")
	font.antialiasing = TextServer.FONT_ANTIALIASING_NONE
	font.hinting = TextServer.HINTING_NONE
	font.subpixel_positioning = TextServer.SUBPIXEL_POSITIONING_DISABLED
	font.force_autohinter = false
	theme = Theme.new()
	theme.default_font = font
	theme.default_font_size = 10
	theme.set_color("font_color", "Label", TEXT)
	theme.set_constant("line_spacing", "Label", 1)
	var sb = func(bg: Color, border: Color, bw := 1, pad := 3) -> StyleBoxFlat:
		var s = StyleBoxFlat.new(); s.bg_color = bg; s.border_color = border
		s.set_border_width_all(bw); s.set_content_margin_all(pad); s.anti_aliasing = false
		return s
	theme.set_stylebox("normal", "Button", sb.call(Color("#232d45"), Color("#3c4a6b")))
	theme.set_stylebox("hover", "Button", sb.call(Color("#2d3a59"), ACC))
	theme.set_stylebox("pressed", "Button", sb.call(Color("#1e5a66"), ACC))
	theme.set_stylebox("disabled", "Button", sb.call(Color("#171d2c"), Color("#262f45")))
	theme.set_stylebox("focus", "Button", StyleBoxEmpty.new())
	theme.set_color("font_color", "Button", TEXT)
	theme.set_color("font_hover_color", "Button", Color.WHITE)
	theme.set_color("font_disabled_color", "Button", Color("#5a6580"))
	theme.set_stylebox("panel", "PanelContainer", sb.call(PANEL, LINE, 1, 4))
	theme.set_stylebox("panel", "Panel", sb.call(PANEL, LINE, 1, 4))
	theme.set_color("default_color", "RichTextLabel", TEXT)
	theme.set_constant("line_separation", "RichTextLabel", 1)
	var sc = StyleBoxFlat.new(); sc.bg_color = Color("#3c4a6b"); sc.set_content_margin_all(0)
	theme.set_stylebox("grabber", "VScrollBar", sc)
	theme.set_stylebox("grabber_highlight", "VScrollBar", sc)
	theme.set_stylebox("grabber_pressed", "VScrollBar", sc)
	var st = StyleBoxFlat.new(); st.bg_color = Color("#10162a"); st.set_content_margin_all(0)
	theme.set_stylebox("scroll", "VScrollBar", st)
	theme.set_constant("separation", "VBoxContainer", 2)
	theme.set_constant("separation", "HBoxContainer", 3)
	var cb = sb.call(Color("#232d45"), Color("#3c4a6b"))
	theme.set_stylebox("normal", "OptionButton", cb)
	theme.set_stylebox("hover", "OptionButton", sb.call(Color("#2d3a59"), ACC))
	return theme

static func style(bg: Color, border: Color, bw := 1, pad := 4) -> StyleBoxFlat:
	var s = StyleBoxFlat.new(); s.bg_color = bg; s.border_color = border
	s.set_border_width_all(bw); s.set_content_margin_all(pad); s.anti_aliasing = false
	return s

static func panel(bg := PANEL, border := LINE, pad := 4) -> PanelContainer:
	var p = PanelContainer.new(); p.add_theme_stylebox_override("panel", style(bg, border, 1, pad)); return p

static func lbl(text: String, col := TEXT, size := 10, wrap := false, width := 0) -> Label:
	var l = Label.new(); l.text = text; l.add_theme_color_override("font_color", col)
	if size != 10: l.add_theme_font_size_override("font_size", size)
	if wrap:
		l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		if width > 0: l.custom_minimum_size.x = width
	return l

static func rich(bb: String, width := 0, fit := true) -> RichTextLabel:
	var r = RichTextLabel.new(); r.bbcode_enabled = true; r.fit_content = fit; r.scroll_active = not fit
	r.text = bb
	if width > 0: r.custom_minimum_size.x = width
	r.add_theme_font_override("normal_font", font); r.add_theme_font_override("bold_font", font)
	r.add_theme_font_size_override("normal_font_size", 10); r.add_theme_font_size_override("bold_font_size", 10)
	return r

static func btn(text: String, cb: Callable, disabled := false, tip := "") -> Button:
	var b = Button.new(); b.text = text; b.disabled = disabled; b.focus_mode = Control.FOCUS_NONE
	if tip != "": b.tooltip_text = tip
	b.pressed.connect(cb)
	return b

static func icon(tex: Texture2D, scale := 1) -> TextureRect:
	var t = TextureRect.new(); t.texture = tex; t.stretch_mode = TextureRect.STRETCH_KEEP
	t.custom_minimum_size = tex.get_size() * scale
	if scale != 1: t.expand_mode = TextureRect.EXPAND_IGNORE_SIZE; t.stretch_mode = TextureRect.STRETCH_SCALE
	return t

static func hbox(sep := 3) -> HBoxContainer:
	var h = HBoxContainer.new(); h.add_theme_constant_override("separation", sep); return h

static func vbox(sep := 2) -> VBoxContainer:
	var v = VBoxContainer.new(); v.add_theme_constant_override("separation", sep); return v

## a bar with tier thresholds marked
class Bar extends Control:
	var value = 0.0
	var col = Color.WHITE
	var marks = true
	var ghost = -1.0
	func _init(w := 40, h := 5) -> void: custom_minimum_size = Vector2(w, h)
	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, size), Color("#0b1020"))
		draw_rect(Rect2(Vector2.ZERO, Vector2(size.x * clamp(value, 0.0, 1.0), size.y)), col)
		if ghost >= 0.0: draw_rect(Rect2(Vector2(size.x * clamp(ghost, 0.0, 1.0) - 1, 0), Vector2(1, size.y)), Color(1, 1, 1, 0.7))
		if marks:
			for th in [D.T1, D.T2, D.T3]: draw_rect(Rect2(Vector2(size.x * th, 0), Vector2(1, size.y)), Color(0, 0, 0, 0.7))
		draw_rect(Rect2(Vector2.ZERO, size), Color("#2f3a55"), false, 1.0)

static func bar(v: float, c: Color, w := 40, h := 5, marks := true) -> Bar:
	var b = Bar.new(w, h); b.value = v; b.col = c; b.marks = marks; return b

## sparkline for the digital twin
class Spark extends Control:
	var data = []
	var col = Color.WHITE
	var lo = 0.0
	var hi = 1.0
	var line = -1.0
	func _init(w := 160, h := 22) -> void: custom_minimum_size = Vector2(w, h)
	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, size), Color("#08101e"))
		if line >= 0.0:
			var y = size.y - (line - lo) / (hi - lo) * size.y
			draw_line(Vector2(0, y), Vector2(size.x, y), Color(1, 0.3, 0.3, 0.5), 1.0)
		if data.size() > 1:
			var pts = PackedVector2Array()
			for k in data.size():
				var x = k * (size.x - 1) / float(max(1, data.size() - 1))
				var y = size.y - 1 - clamp((data[k] - lo) / (hi - lo), 0.0, 1.0) * (size.y - 2)
				pts.append(Vector2(x, y))
			draw_polyline(pts, col, 1.0)
		draw_rect(Rect2(Vector2.ZERO, size), Color("#2f3a55"), false, 1.0)

## pixel hypnogram of last night's sleep
class Hypno extends Control:
	var hyp = []
	func _init(w := 160, h := 26) -> void: custom_minimum_size = Vector2(w, h)
	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, size), Color("#08101e"))
		var lv = {"Wake": 0, "REM": 1, "N1": 2, "N2": 3, "N3": 4}
		var cols = {"Wake": Color("#e5484d"), "REM": Color("#a970ff"), "N1": Color("#7fb0ff"), "N2": Color("#4f8ef7"), "N3": Color("#2c4f9e")}
		if hyp.is_empty(): return
		var w = (size.x - 2) / float(hyp.size())
		for k in hyp.size():
			var s = hyp[k]; var y = 2 + lv.get(s, 2) * (size.y - 6) / 4.0
			draw_rect(Rect2(1 + k * w, y, max(1.0, w), size.y - 2 - y), cols.get(s, Color.WHITE))
		draw_rect(Rect2(Vector2.ZERO, size), Color("#2f3a55"), false, 1.0)

## 24-hour strip of colours (circadian lighting plan)
class Strip extends Control:
	var cols = []
	var now_frac = -1.0
	func _init(w := 156, h := 10) -> void: custom_minimum_size = Vector2(w, h)
	func _draw() -> void:
		if cols.is_empty(): return
		var w = size.x / cols.size()
		for k in cols.size(): draw_rect(Rect2(k * w, 0, ceil(w), size.y), cols[k])
		if now_frac >= 0.0: draw_rect(Rect2(now_frac * size.x - 1, -1, 2, size.y + 2), Color.WHITE)
		draw_rect(Rect2(Vector2.ZERO, size), Color("#2f3a55"), false, 1.0)
