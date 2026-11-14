#!/usr/bin/env python3
"""Synthesised sounds for LUNA VR (16-bit mono WAV)."""
import numpy as np, wave
SR = 22050
def save(name, x):
    x = np.clip(x, -1, 1); d = (x * 32767 * 0.9).astype(np.int16)
    with wave.open("assets/snd/%s.wav" % name, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(d.tobytes())
def t(sec): return np.arange(int(SR * sec)) / SR
def env(n, a=0.01, r=0.3):
    e = np.ones(n); ai = int(SR * a); ri = int(SR * r)
    if ai: e[:ai] = np.linspace(0, 1, ai)
    if ri: e[-ri:] *= np.linspace(1, 0, ri)
    return e
def tone(f, sec, decay=3.0):
    tt = t(sec); return np.sin(2 * np.pi * f * tt) * np.exp(-decay * tt)
rng = np.random.default_rng(3)
# habitat hum: fans + 60 Hz-ish, loopable (whole number of cycles)
tt = t(4.0)
hum = 0.25 * np.sin(2 * np.pi * 55 * tt) + 0.12 * np.sin(2 * np.pi * 110 * tt) + 0.05 * np.sin(2 * np.pi * 165 * tt)
noise = rng.normal(0, 1, len(tt)); k = np.ones(40) / 40; air = np.convolve(noise, k, "same") * 0.35
save("hum", (hum + air) * 0.5)
# LUNA chime: soft two-note
c = np.concatenate([tone(660, 0.35, 5), tone(880, 0.6, 4)]); save("chime", c * env(len(c), 0.005, 0.2) * 0.6)
# smart alarm: gentle rising bells
a = np.zeros(int(SR * 2.4))
for k2, f in enumerate([523, 659, 784, 1047]):
    s = tone(f, 1.2, 3.5) * 0.5; o = int(SR * 0.4 * k2); a[o:o + len(s)] += s
save("alarm", a * 0.7)
# alert: two short beeps
b = np.concatenate([np.sin(2 * np.pi * 988 * t(0.12)), np.zeros(int(SR * 0.08)), np.sin(2 * np.pi * 988 * t(0.12))])
save("alert", b * env(len(b), 0.005, 0.03) * 0.5)
cl = tone(1800, 0.05, 60); save("click", cl * 0.5)
# door: filtered noise swoosh
n = rng.normal(0, 1, int(SR * 0.6)); n = np.convolve(n, np.ones(25) / 25, "same")
save("door", n * np.sin(np.linspace(0, np.pi, len(n))) * 0.6)
# vacuum / cleaning cycle
v = rng.normal(0, 1, int(SR * 3.0)); v = v - np.convolve(v, np.ones(8) / 8, "same")
save("vacuum", v * env(len(v), 0.3, 0.6) * 0.35)
# harvest pluck
p = tone(330, 0.4, 9) + 0.5 * tone(495, 0.4, 12); save("harvest", p * 0.6)
# rain soundscape (recovery chair)
r = rng.normal(0, 1, int(SR * 6.0)); r = np.convolve(r, np.ones(6) / 6, "same")
drops = np.zeros_like(r)
for _ in range(500):
    i = rng.integers(0, len(r) - 400); drops[i:i + 400] += tone(rng.uniform(1500, 3500), 400 / SR, 60) * rng.uniform(0.05, 0.2)
save("rain", (r * 0.15 + drops) * 0.8)
print("ok")
