/* Project ARES site audio: section music + NASA-style sound effects + mute/unmute.
   Usage: <script src="audio/ares-audio.js" data-base="audio/" data-default="hero"
           data-sections='{"helmet":"habitat","boot":"boots"}'></script>
   Tracks: hero, habitat, boots, gloves, suit. All music is CC0 (see audio/CREDITS.md).
   Sound effects are synthesized with the Web Audio API (no files). */
(function () {
  var me = document.currentScript;
  var BASE = (me && me.getAttribute('data-base')) || 'audio/';
  var DEF = (me && me.getAttribute('data-default')) || 'hero';
  var MAP = {};
  try { MAP = JSON.parse((me && me.getAttribute('data-sections')) || '{}'); } catch (e) {}
  var FILES = { hero: 'hero_ambient.mp3', habitat: 'habitat_arp.mp3', boots: 'boots_industrial.mp3', gloves: 'gloves_minimal.mp3', suit: 'suit_orchestral.mp3' };
  var VOL = 0.32, KEY = 'ares-sound';

  var on = false;
  try { on = localStorage.getItem(KEY) === 'on'; } catch (e) {}
  var started = false, current = null, players = {}, ducked = false, ctx = null;

  /* ---------- music ---------- */
  function player(k) {
    if (!players[k]) {
      var a = new Audio(BASE + FILES[k]); a.loop = true; a.preload = 'none'; a.volume = 0; players[k] = a;
    }
    return players[k];
  }
  function fade(a, to, ms, done) {
    var from = a.volume, t0 = performance.now();
    if (a._f) cancelAnimationFrame(a._f);
    (function step(t) {
      var u = Math.min(1, (t - t0) / ms); a.volume = Math.max(0, Math.min(1, from + (to - from) * u));
      if (u < 1) a._f = requestAnimationFrame(step); else { a._f = 0; if (done) done(); }
    })(t0);
  }
  function target() { return on && !ducked ? VOL : 0; }
  function playTrack(k) {
    if (!FILES[k]) k = DEF;
    if (k === current) { if (current) fade(player(current), target(), 600); return; }
    var old = current; current = k;
    if (old) { var o = player(old); fade(o, 0, 1400, function () { if (current !== old) o.pause(); }); }
    if (!on) return;
    var a = player(k); a.play().catch(function () {}); fade(a, target(), 1800);
    if (started && old) whoosh();
  }

  /* ---------- sound effects (Web Audio) ---------- */
  function ac() {
    if (!ctx) { var C = window.AudioContext || window.webkitAudioContext; if (!C) return null; ctx = new C(); }
    if (ctx.state === 'suspended') ctx.resume();
    return ctx;
  }
  function tone(f, dur, type, vol, when, f2) {
    var c = ac(); if (!c || !on) return;
    var t = c.currentTime + (when || 0), o = c.createOscillator(), g = c.createGain();
    o.type = type || 'sine'; o.frequency.setValueAtTime(f, t); if (f2) o.frequency.exponentialRampToValueAtTime(f2, t + dur);
    g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(vol || 0.05, t + 0.01); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g).connect(c.destination); o.start(t); o.stop(t + dur + 0.05);
  }
  function noise(dur, vol, freq, q, when) {
    var c = ac(); if (!c || !on) return;
    var t = c.currentTime + (when || 0), n = c.sampleRate * dur, b = c.createBuffer(1, n, c.sampleRate), d = b.getChannelData(0);
    for (var i = 0; i < n; i++) d[i] = Math.random() * 2 - 1;
    var s = c.createBufferSource(), f = c.createBiquadFilter(), g = c.createGain();
    s.buffer = b; f.type = 'bandpass'; f.frequency.value = freq || 3000; f.Q.value = q || 0.7;
    g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(vol || 0.05, t + dur * 0.25); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    s.connect(f).connect(g).connect(c.destination); s.start(t); s.stop(t + dur);
  }
  var lastHover = 0;
  function hover() { var n = performance.now(); if (n - lastHover < 90) return; lastHover = n; tone(1850, 0.05, 'sine', 0.018); }
  function click() { tone(220, 0.04, 'square', 0.025); tone(1320, 0.08, 'sine', 0.03, 0.03); }            // suit connection click + beep
  function airlock() { noise(0.9, 0.05, 2600, 0.5); tone(90, 1.1, 'sine', 0.035, 0, 60); }              // airlock hiss + low hum
  function whoosh() { airlock(); tone(660, 0.08, 'sine', 0.025, 0.55); tone(990, 0.1, 'sine', 0.025, 0.66); }
  function radio() { noise(0.18, 0.04, 1800, 3); tone(1200, 0.06, 'square', 0.015, 0.2); }               // radio squelch + chirp

  /* ---------- mute / unmute button ---------- */
  var css = document.createElement('style');
  css.textContent = '#ares-snd{position:fixed;right:18px;bottom:18px;z-index:9999;display:flex;align-items:center;gap:8px;padding:10px 14px;border-radius:999px;' +
    'background:rgba(10,20,48,.82);border:1px solid rgba(56,189,248,.45);color:#E6F1FF;font:600 13px Inter,system-ui,sans-serif;cursor:pointer;' +
    'backdrop-filter:blur(8px);box-shadow:0 6px 24px rgba(0,0,0,.45);transition:border-color .2s,transform .2s}' +
    '#ares-snd:hover{border-color:#38BDF8;transform:translateY(-2px)}#ares-snd .bars{display:flex;gap:2px;align-items:flex-end;height:14px}' +
    '#ares-snd .bars i{width:3px;background:#38BDF8;border-radius:2px;height:4px}#ares-snd.on .bars i{animation:aresb .9s ease-in-out infinite}' +
    '#ares-snd.on .bars i:nth-child(2){animation-delay:.15s}#ares-snd.on .bars i:nth-child(3){animation-delay:.3s}#ares-snd.on .bars i:nth-child(4){animation-delay:.45s}' +
    '@keyframes aresb{0%,100%{height:4px}50%{height:14px}}#ares-snd .lbl{white-space:nowrap}';
  document.head.appendChild(css);
  var btn = document.createElement('button');
  btn.id = 'ares-snd'; btn.type = 'button';
  btn.innerHTML = '<span class="bars"><i></i><i></i><i></i><i></i></span><span class="lbl"></span>';
  function paint() {
    btn.classList.toggle('on', on);
    btn.querySelector('.lbl').textContent = on ? 'Sound on · mute' : 'Sound off · unmute';
    btn.setAttribute('aria-pressed', on ? 'true' : 'false');
    btn.title = on ? 'Mute music and sound effects' : 'Play music and sound effects';
  }
  btn.addEventListener('click', function (e) {
    e.stopPropagation();
    on = !on; try { localStorage.setItem(KEY, on ? 'on' : 'off'); } catch (er) {}
    paint();
    if (on) { started = true; var k = current || DEF; current = null; playTrack(k); airlock(); }
    else Object.keys(players).forEach(function (k) { var a = players[k]; fade(a, 0, 500, function () { a.pause(); }); });
  });
  function mount() { document.body.appendChild(btn); paint(); }
  if (document.body) mount(); else document.addEventListener('DOMContentLoaded', mount);

  /* browsers block sound until the visitor interacts: if sound was left on, start on first interaction */
  function firstGesture() {
    if (started) return; started = true;
    if (on) { var k = current || DEF; current = null; playTrack(k); }
  }
  ['pointerdown', 'keydown', 'touchstart'].forEach(function (ev) { window.addEventListener(ev, firstGesture, { once: true, passive: true }); });

  /* ---------- section-aware music ---------- */
  function setupSections() {
    current = null; var pending = DEF;
    var ids = Object.keys(MAP), els = ids.map(function (id) { return document.getElementById(id); }).filter(Boolean);
    if (!els.length || !('IntersectionObserver' in window)) { current = DEF; return; }
    var vis = {};
    var io = new IntersectionObserver(function (ents) {
      ents.forEach(function (en) { vis[en.target.id] = en.isIntersecting ? en.intersectionRatio : 0; });
      var best = null, br = 0.12;
      Object.keys(vis).forEach(function (id) { if (vis[id] > br) { br = vis[id]; best = id; } });
      var k = best ? MAP[best] : DEF;
      if (!started) { current = k; return; }
      playTrack(k);
    }, { threshold: [0, 0.12, 0.3, 0.5, 0.75] });
    els.forEach(function (el) { io.observe(el); });
    current = pending;
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', setupSections); else setupSections();

  /* ---------- UI sound effects + video ducking ---------- */
  document.addEventListener('pointerover', function (e) {
    var t = e.target.closest && e.target.closest('a,button,.card,.btn,figure,summary');
    if (t && !t.contains(e.relatedTarget)) hover();
  });
  document.addEventListener('click', function (e) {
    if (e.target.closest && e.target.closest('#ares-snd')) return;
    if (e.target.closest && e.target.closest('a,button,.btn,.card,figure,summary')) click();
  });
  function duck(v) { ducked = v; if (current && on) fade(player(current), target(), v ? 500 : 1500); }
  document.addEventListener('play', function (e) { if (e.target.tagName === 'VIDEO') { radio(); if (!e.target.muted) duck(true); } }, true);
  ['pause', 'ended'].forEach(function (ev) {
    document.addEventListener(ev, function (e) {
      if (e.target.tagName !== 'VIDEO') return;
      var anyPlaying = [].some.call(document.querySelectorAll('video'), function (v) { return !v.paused && !v.ended && !v.muted; });
      if (!anyPlaying) duck(false);
    }, true);
  });
  document.addEventListener('visibilitychange', function () {
    if (!current || !on) return; var a = player(current);
    if (document.hidden) a.pause(); else if (started) { a.play().catch(function () {}); }
  });
})();
