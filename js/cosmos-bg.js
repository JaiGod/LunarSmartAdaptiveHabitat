// Project ARES · cosmic background: shooting stars, golden sparkles, click-for-meteor-shower.
// Lightweight 2D canvas behind the page. Respects prefers-reduced-motion. © 2026 Jaineil R.
(function () {
  if (window.__aresCosmos) return; window.__aresCosmos = 1;
  const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
  const c = document.createElement('canvas'); c.setAttribute('aria-hidden', 'true');
  c.style.cssText = 'position:fixed;inset:0;width:100%;height:100%;z-index:-1;pointer-events:none';
  document.body.prepend(c);
  const g = c.getContext('2d'); let W, H, dpr;
  const size = () => { dpr = Math.min(devicePixelRatio || 1, 2); W = c.width = innerWidth * dpr; H = c.height = innerHeight * dpr; };
  size(); addEventListener('resize', size);
  const R = (a, b) => a + Math.random() * (b - a);
  const sparks = Array.from({ length: innerWidth < 700 ? 40 : 90 }, () => ({ x: Math.random(), y: Math.random(), s: R(0.4, 1.6), p: R(0, 6.28), v: R(0.4, 1.4), gold: Math.random() < 0.35 }));
  const meteors = [];
  const meteor = (gold, x, y) => meteors.push({ x: x ?? R(0.1, 1.1) * W, y: y ?? R(-0.1, 0.45) * H, vx: -R(9, 16) * dpr, vy: R(4, 8) * dpr, life: 1, len: R(80, 180) * dpr, gold });
  let next = 1.5, last = performance.now();
  function frame(now) {
    const dt = Math.min(0.05, (now - last) / 1000); last = now; g.clearRect(0, 0, W, H);
    for (const s of sparks) { const a = 0.35 + 0.65 * Math.abs(Math.sin(now / 1000 * s.v + s.p)); const x = s.x * W, y = s.y * H, r = s.s * dpr;
      g.fillStyle = s.gold ? `rgba(255,214,110,${a * 0.8})` : `rgba(220,232,255,${a * 0.7})`; g.beginPath(); g.arc(x, y, r, 0, 7); g.fill();
      if (s.gold && a > 0.9) { g.strokeStyle = `rgba(255,224,140,${(a - 0.9) * 6})`; g.lineWidth = dpr * 0.8; g.beginPath(); g.moveTo(x - r * 5, y); g.lineTo(x + r * 5, y); g.moveTo(x, y - r * 5); g.lineTo(x, y + r * 5); g.stroke(); } }
    next -= dt; if (next < 0) { meteor(Math.random() < 0.4); next = R(2.5, 6); }
    for (let i = meteors.length - 1; i >= 0; i--) { const m = meteors[i]; m.x += m.vx * dt * 60; m.y += m.vy * dt * 60; m.life -= dt * 0.7;
      const n = Math.hypot(m.vx, m.vy), tx = m.x - (m.vx / n) * m.len, ty = m.y - (m.vy / n) * m.len;
      const gr = g.createLinearGradient(m.x, m.y, tx, ty); const col = m.gold ? '255,214,110' : '230,240,255';
      gr.addColorStop(0, `rgba(${col},${m.life})`); gr.addColorStop(1, `rgba(${col},0)`); g.strokeStyle = gr; g.lineWidth = 2 * dpr; g.lineCap = 'round';
      g.beginPath(); g.moveTo(m.x, m.y); g.lineTo(tx, ty); g.stroke(); g.fillStyle = `rgba(255,255,255,${m.life})`; g.beginPath(); g.arc(m.x, m.y, 1.8 * dpr, 0, 7); g.fill();
      if (m.life <= 0 || m.x < -200 || m.y > H + 200) meteors.splice(i, 1); }
    requestAnimationFrame(frame);
  }
  if (!reduced) requestAnimationFrame(frame); else { frame(performance.now()); }
  // click empty space → golden meteor shower from the click point
  addEventListener('click', (e) => { if (reduced || e.target.closest('a,button,input,textarea,select,video,summary,label,canvas')) return;
    for (let i = 0; i < 7; i++) setTimeout(() => meteor(true, e.clientX * dpr + R(0, 260) * dpr, e.clientY * dpr - R(40, 220) * dpr), i * 90); });
})();
