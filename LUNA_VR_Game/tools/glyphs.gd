extends SceneTree
func _init():
	var f = ThemeDB.fallback_font
	var miss = []
	for ch in ["✓", "○", "★", "→", "▶", "₂", "·", "—", "°", "µ", "³", "×", "≥", "≤", "•", "↗", "✗", "●", "◦", "–", "é", "←", "↑", "⚠", "✔", "☐", "☑"]:
		if not f.has_char(ch.unicode_at(0)): miss.append(ch)
	print("MISSING ", " ".join(miss))
	quit()
