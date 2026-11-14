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

# ---- round 3: room soundscapes (rubric audio lever), safety-drill klaxon, switch, pod playlist ----
def lowpass(x, k): return np.convolve(x, np.ones(k) / k, "same")
def loopable(x, fade=0.25):
    # crossfade the tail into the head so the loop has no click
    n = int(SR * fade); y = x.copy()
    y[:n] = x[:n] * np.linspace(0, 1, n) + x[-n:] * np.linspace(1, 0, n)
    return y[:-n]
# ocean: slow swells of filtered noise
T = 8.0; tt = t(T)
o = lowpass(rng.normal(0, 1, len(tt)), 30)
sw = 0.55 + 0.45 * np.sin(2 * np.pi * tt / 4.0 - 1.2) ** 2
save("ocean", loopable(o * sw * 0.5))
# forest: soft wind + bird chirps
w = lowpass(rng.normal(0, 1, len(tt)), 60) * 0.25
for _ in range(14):
    i = rng.integers(0, len(tt) - int(SR * 0.3)); f0 = rng.uniform(2500, 4200)
    for k in range(rng.integers(2, 5)):
        n = int(SR * 0.07); j = i + k * int(SR * 0.09)
        if j + n >= len(w): break
        ph = np.cumsum(np.linspace(f0, f0 * 1.25, n)) / SR * 2 * np.pi
        w[j:j + n] += np.sin(ph) * np.hanning(n) * 0.18
save("forest", loopable(w))
# cafe: murmur (band-passed noise) + the odd cup clink
c = lowpass(rng.normal(0, 1, len(tt)), 12) - lowpass(rng.normal(0, 1, len(tt)), 70)
c = c * (0.6 + 0.4 * lowpass(np.abs(rng.normal(0, 1, len(tt))), 4000))
for _ in range(5):
    i = rng.integers(0, len(tt) - 3000); c[i:i + 3000] += tone(rng.uniform(2200, 3000), 3000 / SR, 25) * 0.25
save("cafe", loopable(c * 0.45))
# pod playlist: a gentle chord progression (pad + soft plucks)
mus = np.zeros(len(t(12.0)))
chords = [[220, 277, 330], [196, 247, 294], [174.6, 220, 262], [196, 247, 294]]
for k, ch in enumerate(chords):
    o0 = int(SR * 3.0 * k); n = int(SR * 3.2)
    seg = sum(np.sin(2 * np.pi * f * t(3.2)) * 0.12 for f in ch) * np.hanning(n)
    mus[o0:o0 + n] += seg[:len(mus) - o0]
    for j, f in enumerate(ch + [ch[1] * 2]):
        pl = tone(f * 2, 0.6, 7) * 0.12; q = o0 + int(SR * 0.7 * j)
        mus[q:q + len(pl)] += pl[:len(mus) - q]
save("music", loopable(mus))
# klaxon (safety drill): three rising whoops
kl = np.zeros(0)
for _ in range(3):
    n = int(SR * 0.7); f = np.linspace(420, 900, n); ph = np.cumsum(f) / SR * 2 * np.pi
    kl = np.concatenate([kl, np.sign(np.sin(ph)) * 0.35 * env(n, 0.01, 0.05), np.zeros(int(SR * 0.12))])
save("klaxon", lowpass(kl, 3))
# switch: a solid mechanical clack
sw2 = np.concatenate([tone(180, 0.06, 40) + lowpass(rng.normal(0, 1, int(SR * 0.06)), 3) * 0.4 * np.exp(-np.arange(int(SR * 0.06)) / 200.0)])
save("switch", sw2 * 0.7)
print("round 3 sounds ok")
