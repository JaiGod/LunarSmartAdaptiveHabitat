// LUNA-X Exhibit engine — turns an existing 3D model into an interactive NASA-style exhibit.
// Intro (stars → Moon → model → power-on), scroll story with camera fly-throughs, part callouts,
// hover tooltips, click-to-expand info drawer, scan + hologram, free 360° explore with zoom,
// cinematic fly-around, step navigator, specs, habitat-systems diagram, comparison table.
// NASA HUNCH Project ARES · © 2026 Jaineil R. · with Angel C. and Farhan K.
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { DRACOLoader } from 'three/addons/loaders/DRACOLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';

const C = window.EXHIBIT;
const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
const esc = (s) => String(s ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const isMobile = matchMedia('(max-width:700px)').matches;
const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
const ACC = new THREE.Color(C.accent || '#5AB8FF');
document.documentElement.style.setProperty('--acc', C.accent || '#5AB8FF');

// ------------------------------------------------------------------ shared exhibition data
const SYSTEMS = [
  ['health', '❤', 'Crew health'], ['sleep', '☾', 'Sleep'], ['life', '◎', 'Life support'], ['water', '💧', 'Water'],
  ['agri', '🌱', 'Agriculture'], ['power', '⚡', 'Power'], ['comms', '📡', 'Comms'], ['ops', '⌂', 'Habitat ops'],
  ['eva', '👣', 'EVA & mobility'], ['sus', '♻', 'Sustainability'], ['science', '🔬', 'Science'],
];
const FAMILY0 = [
  { id: 'helmet', name: 'Cognitive Helmet', href: '../helmet/', blurb: 'AI, vision, comms, crew safety' },
  { id: 'glove', name: 'Wellness Glove', href: '../glove/', blurb: 'Vitals, plant scans, gestures' },
  { id: 'boot', name: 'Mobility Boot', href: '../boot/', blurb: 'Gait, balance, dust defense' },
  { id: 'suit', name: 'Smart Suit', href: '../suit/', blurb: 'Full EVA suit, XCore hub' },
  { id: 'uniform', name: 'Habitat Uniform', href: '../../habitat-uniform/', blurb: 'Everyday indoor flight suit' },
];
const FAMILY = C.family || FAMILY0;
// comparison rows (concept design targets, not flight hardware)
const CMP = C.cmp || {
  rows: ['Role', 'Worn', 'Main sensors', 'AI on board', 'Light language', 'Power (design target)', 'Mass (design target)', 'Links to habitat'],
  helmet: ['See, hear and think for the crew', 'EVA + habitat', 'Eye tracking, LiDAR, dust, air, radiation, surface IR', 'Fatigue + stress model, fall detection', '9-colour ring: meeting, O2, sleep, alert', '~12 W avg', '~4.8 kg', 'Comms relay, LUNA AI, wellness log'],
  glove: ['Hands-on work and vitals', 'EVA (in pressure glove) + habitat', 'Grip force, tremor, skin temp, IR material scan', 'Gesture + fatigue detection', '6-state fingertip protocol', '~3 W avg', '~0.9 kg pair', 'Greenhouse data, wrist display, alerts'],
  boot: ['Every step on regolith', 'EVA', 'Pressure-map sole, gait IMU, ankle torque', 'Gait + balance prediction', 'Path lighting + seal LED', '~6 W avg', '~2.6 kg each', 'Navigation, dust log, physio data'],
  suit: ['Full protection outside', 'EVA', 'ECG, O2, coolant, IMUs, cameras', 'XCore wellness hub + LUNA server', 'XCore ring + blade LEDs', '~180 W (PLSS)', '~95 kg (1/6 g ≈ 16 kg felt)', 'Hab dock, O2 refill, data offload'],
  uniform: ['Daily life inside the base', 'Habitat', 'Heart strap, temp, wrist + belt tablets', 'LUNA-X chest module', 'Chest ring colour cycle', '~2 W avg', '~1.4 kg', 'Habitat net, access badge, comms'],
};

// ------------------------------------------------------------------ build the page
function buildDOM() {
  const b = document.body;
  const steps = C.steps.map((s, i) => `<div class="step${s.side === 'r' ? ' r' : ''}" id="s${i + 1}" data-mode="${esc(s.mode)}">
    <div class="panel"><div class="kick">${String(i + 1).padStart(2, '0')} · ${esc(s.kick)}</div><h2>${esc(s.title)}</h2><p>${esc(s.text)}</p>
    ${s.mode === 'scan' ? `<div class="btns"><button class="btn pri" data-scan type="button">◉ ${esc(C.hub?.button || 'Run scan')}</button></div>` : ''}
    ${s.chips ? `<div class="chips">${s.chips.map((c) => `<span class="chip">${esc(c)}</span>`).join('')}</div>` : ''}</div></div>`).join('');
  const q = C.q5 || {};
  const Q = [['What it is', q.what], ['Why astronauts need it', q.why], ['How it works', q.how], ['The problem on the Moon', q.problem], ['How it connects', q.connect]];
  b.insertAdjacentHTML('afterbegin', `
<div id="load"><div style="text-align:center"><div class="kick">${esc(C.loadKick || 'LUNA-X · Exhibit')}</div><div class="bar"><i></i></div><div class="lp" id="lpct">Loading ${esc(C.short)} 0%</div></div></div>
<div id="stage"></div>
<nav class="top"><a class="brand" href="../">← LUNA-X EXHIBITION</a>
  <a href="#overview">Overview</a><a href="#parts">Systems</a><a href="#specs">Specs</a><a href="#habitat">Habitat links</a><a href="#compare">Compare</a><a href="#media">Media</a><a href="../../../">Project ARES</a></nav>
<header id="hero"><div class="kick">${esc(C.kicker)}</div><h1>${C.titleHTML || esc(C.title)}</h1><p class="tag">${esc(C.tagline)}</p>
  <div id="enter"><button class="btn pri" id="bSound" type="button">▶ Power on with sound</button><a class="btn" href="#s1">Explore</a><button class="btn" id="bFly2" type="button">⟳ Fly-around</button></div>
  <div id="scrollhint">SCROLL ▾</div></header>
<div class="story">${steps}</div>
<div class="content"><main>
  <section id="overview" class="reveal"><div class="kick">Exhibit ${esc(C.no || '')} · ${esc(C.category || '')}</div><h2 class="sec">${esc(C.title)}</h2>
    <p class="desc">${esc(C.desc)}</p><div class="q5">${Q.map(([t, v], i) => `<div class="q"><em>${i + 1}</em><b>${t}</b><p>${esc(v)}</p></div>`).join('')}</div></section>
  <section id="parts" class="reveal"><div class="kick">Systems on the model</div><h2 class="sec">Every Part Has a Job</h2>
    <p class="sub">Each card is a real part of the 3D model. Hover a part in 3D for its name, or click a card or the model to open the full briefing and fly the camera to it.</p>
    <div class="q5">${C.parts.map((p) => `<button class="q" data-part="${p.k}" type="button" style="text-align:left;cursor:pointer;color:inherit;font:inherit"><b>${esc(p.label)}</b><p>${esc(p.sub)}</p><p class="note">Open briefing →</p></button>`).join('')}</div></section>
  <section id="specs" class="reveal"><div class="kick">Technical metrics</div><h2 class="sec">By the Numbers</h2>
    <p class="sub">${esc(C.specNote || 'Concept design targets for a NASA HUNCH student design. They show how the system is meant to perform, not tested flight hardware.')}</p>
    <div class="specs">${C.specs.map((s) => `<div class="spec" data-v="${s.v}" data-p="${s.p ?? 70}"><b><span class="cnt">${s.pre || ''}${s.v}</span><small>${esc(s.u || '')}</small></b><span>${esc(s.l)}</span></div>`).join('')}</div>
    <table class="spec-t">${C.table.map(([k, v]) => `<tr><th>${esc(k)}</th><td>${esc(v)}</td></tr>`).join('')}</table></section>
  <section id="habitat" class="reveal"><div class="kick">Habitat ecosystem</div><h2 class="sec">How It Connects to the Base</h2>
    <p class="sub">The ${esc(C.short)} never works alone. Bright nodes are habitat systems it talks to. Click any node to see what flows between them.</p>
    <div class="sysw"><svg viewBox="0 0 560 560" id="sysd" aria-label="Habitat systems diagram"></svg><div id="sysinfo"></div></div></section>
  <section id="compare" class="reveal"><div class="kick">Comparison view</div><h2 class="sec">${esc(C.cmpTitle || 'The LUNA-X Family Side by Side')}</h2>
    <p class="sub">How this exhibit compares with the rest of the wearable system. Values are concept design targets.</p>
    <div class="cmp-toggle">${(C.cmpTabs || [['all', 'All'], ['eva', 'EVA gear only'], ['hab', 'Habitat wear']]).map(([k, l], i) => `<button class="btn sq${i ? '' : ' on'}" data-cmp="${k}" type="button">${esc(l)}</button>`).join('')}</div>
    <div class="cmp-wrap"><table class="cmp" id="cmpt"></table></div></section>
  <section id="media" class="reveal"><div class="kick">Renders & animation</div><h2 class="sec">Up Close</h2><p class="sub">Rendered in Blender. Click any picture to see it full size.</p>
    <div class="gal">${(C.gallery || []).map(([s, c]) => `<figure><img loading="lazy" src="${s}" alt="${esc(c)}"><figcaption>${esc(c)}</figcaption></figure>`).join('')}</div>
    <div class="vids">${(C.videos || []).map(([s, c, p]) => `<figure class="vid"><video src="${s}" ${p ? `poster="${p}"` : ''} controls playsinline loop muted preload="none"></video><figcaption>${esc(c)}</figcaption></figure>`).join('')}</div></section>
  <section id="downloads" class="reveal"><div class="kick">Take it with you</div><h2 class="sec">Downloads</h2>
    <div class="btns"><a class="btn sq" href="${C.model}" download>⬇ 3D model on this page (.glb)</a>${(C.downloads || []).map(([h, l]) => `<a class="btn sq" href="${h}" download>⬇ ${esc(l)}</a>`).join('')}</div>
    <div class="kick" style="margin-top:20px">More exhibits</div><div class="family" style="margin-top:10px">${FAMILY.map((f) => `<a class="${f.id === C.id ? 'me' : ''}" href="${f.id === C.id ? '#' : f.href}"><b>${f.name}</b><span>${f.blurb}</span></a>`).join('')}<a href="../"><b>Exhibition hall</b><span>Every model in the colony</span></a></div></section>
  <section id="team" class="reveal"><div class="kick">The people behind it</div><h2 class="sec">Made by Students</h2>
    <p class="sub">Designed, modeled and animated by <b>Jaineil R.</b> for NASA HUNCH Project ARES, with teammates <b>Angel C.</b> and <b>Farhan K.</b>, who built the Lunar Smart Adaptive Habitat it connects to.</p></section>
</main></div>
<div id="callouts"><svg></svg>${C.parts.filter((p) => !p.nocall).map((p) => `<div class="co" data-part="${p.k}">${esc(p.label)}<small>${esc(p.sub)}</small></div>`).join('')}</div>
<div id="holo">${(C.holo || []).map((h, i) => `<div class="hs" style="transition-delay:${i * 0.18}s">${esc(h.l)}: <b data-h="${i}"${h.ok ? ' style="color:#7CF0B4"' : ''}>${esc(h.t ?? h.v)}</b>${h.u ? ' ' + esc(h.u) : ''}</div>`).join('')}</div>
<div id="hint">${esc(C.hub?.hint || 'CLICK TO SCAN')}</div>
<div id="tip"></div>
<aside id="drawer" aria-live="polite"></aside>
<div id="tools"><button id="tFly" class="wide" type="button" title="Cinematic fly-around">⟳ Fly-around</button><button id="tX" type="button" title="Free 360° view with zoom">✥ Explore 360°</button>
  <button id="tIn" class="round" type="button" title="Zoom in">＋</button><button id="tOut" class="round" type="button" title="Zoom out">－</button><button id="tR" class="wide" type="button" title="Reset view">⟲ Reset</button></div>
<div id="xhelp">DRAG TO ORBIT · SCROLL / PINCH TO ZOOM · RIGHT-DRAG TO PAN · ESC TO EXIT</div>
<div id="lb"><img alt=""></div>
<footer>${esc(C.title)} · LUNA-X · NASA HUNCH Project ARES · © 2026 Jaineil R. · with Angel C. and Farhan K.</footer>`);
}
buildDOM();

// ------------------------------------------------------------------ renderer / scene
const stage = $('#stage');
const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(devicePixelRatio, isMobile ? 1.5 : 2));
renderer.setSize(innerWidth, innerHeight);
renderer.toneMapping = THREE.ACESFilmicToneMapping; renderer.toneMappingExposure = C.exposure || 1.05;
renderer.shadowMap.enabled = !isMobile; renderer.shadowMap.type = THREE.PCFSoftShadowMap;
stage.appendChild(renderer.domElement);
const scene = new THREE.Scene(); scene.background = new THREE.Color(0x02040b);
const pmrem = new THREE.PMREMGenerator(renderer);
scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture; scene.environmentIntensity = 0;
const camera = new THREE.PerspectiveCamera(32, innerWidth / innerHeight, 0.02, 400); camera.position.set(0, 1.1, 4.2);
const composer = new EffectComposer(renderer);
composer.addPass(new RenderPass(scene, camera));
// guard: one NaN/inf pixel (bad normals on tiny parts) would be smeared over the whole frame by bloom
composer.addPass(new ShaderPass({ uniforms: { tDiffuse: { value: null } },
  vertexShader: 'varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.0); }',
  fragmentShader: 'uniform sampler2D tDiffuse; varying vec2 vUv; void main(){ vec4 c = texture2D(tDiffuse, vUv); if (!(c.r == c.r) || !(c.g == c.g) || !(c.b == c.b) || !(c.a == c.a)) c = vec4(0.0,0.0,0.0,1.0); gl_FragColor = clamp(c, 0.0, 60.0); }' }));
const bloom = new UnrealBloomPass(new THREE.Vector2(innerWidth, innerHeight), C.bloom ?? 0.42, 0.4, C.bloomThreshold ?? 0.92); composer.addPass(bloom);
composer.addPass(new OutputPass());
const controls = new OrbitControls(camera, renderer.domElement);
controls.enabled = false; controls.enableDamping = true; controls.dampingFactor = 0.08; controls.minDistance = 0.4; controls.maxDistance = 6;

const lights = [];
const addLight = (l, full) => { l.userData.full = full; l.intensity = 0; scene.add(l); lights.push(l); return l; };
const key = addLight(new THREE.DirectionalLight(0xfff3e6, 1), 2.6); key.position.set(-2.2, 3.4, 3.2); key.castShadow = !isMobile;
key.shadow.mapSize.set(2048, 2048); Object.assign(key.shadow.camera, { left: -1.4, right: 1.4, top: 2.2, bottom: -0.3 }); key.shadow.bias = -0.0004; key.shadow.normalBias = 0.02;
addLight(new THREE.DirectionalLight(0xdfe8ff, 1), 0.9).position.set(3, 1.6, 2.2);
addLight(new THREE.DirectionalLight(0x7fb6ff, 1), 2.2).position.set(-2.5, 2.4, -3);
addLight(new THREE.DirectionalLight(0x9fc8ff, 1), 1.8).position.set(2.6, 2.2, -2.6);
addLight(new THREE.HemisphereLight(0x8aa6d6, 0x0b0f1a, 1), 0.6);
const coreLight = new THREE.PointLight(ACC, 0, 0.9, 2); scene.add(coreLight);

// stars + moon + floor
const N = isMobile ? 1800 : 3500, spos = new Float32Array(N * 3), stw = new Float32Array(N);
for (let i = 0; i < N; i++) { const u = Math.random() * 2 - 1, t = Math.random() * 6.283, r = 90 + Math.random() * 60, s = Math.sqrt(1 - u * u); spos.set([r * s * Math.cos(t), r * u, r * s * Math.sin(t)], i * 3); stw[i] = Math.random(); }
const sg = new THREE.BufferGeometry(); sg.setAttribute('position', new THREE.BufferAttribute(spos, 3)); sg.setAttribute('tw', new THREE.BufferAttribute(stw, 1));
const starMat = new THREE.ShaderMaterial({ transparent: true, depthWrite: false, uniforms: { uT: { value: 0 }, uO: { value: 0 } },
  vertexShader: 'attribute float tw; varying float vT; uniform float uT; void main(){ vT = 0.55 + 0.45*sin(uT*1.3 + tw*40.0); gl_PointSize = (1.0 + tw*1.8) * 1.6; gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.0); }',
  fragmentShader: 'varying float vT; uniform float uO; void main(){ float d = length(gl_PointCoord-0.5); if(d>0.5) discard; gl_FragColor = vec4(vec3(0.85,0.9,1.0), (1.0-d*2.0)*vT*uO); }' });
scene.add(new THREE.Points(sg, starMat));
function moonTexture() {
  const c = document.createElement('canvas'); c.width = 1024; c.height = 512; const g = c.getContext('2d');
  g.fillStyle = '#8d8f95'; g.fillRect(0, 0, 1024, 512);
  for (let i = 0; i < 9; i++) { g.fillStyle = `rgba(60,62,70,${0.25 + Math.random() * 0.2})`; g.beginPath(); g.ellipse(Math.random() * 1024, 100 + Math.random() * 300, 60 + Math.random() * 120, 40 + Math.random() * 80, Math.random() * 3, 0, 7); g.fill(); }
  for (let i = 0; i < 700; i++) { const x = Math.random() * 1024, y = Math.random() * 512, r = Math.pow(Math.random(), 3) * 26 + 1.5;
    g.fillStyle = 'rgba(40,42,48,0.35)'; g.beginPath(); g.arc(x, y, r, 0, 7); g.fill(); g.strokeStyle = 'rgba(210,212,220,0.28)'; g.lineWidth = Math.max(1, r * 0.18); g.beginPath(); g.arc(x - r * 0.12, y - r * 0.12, r, 3.4, 5.6); g.stroke(); }
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}
const moonMat = new THREE.MeshStandardMaterial({ map: moonTexture(), roughness: 1, transparent: true, opacity: 0 });
const moon = new THREE.Mesh(new THREE.SphereGeometry(9, 64, 48), moonMat); moon.position.set(-22, 13, -60); scene.add(moon);
const sun = new THREE.DirectionalLight(0xffffff, 0); sun.position.set(40, 10, -20); sun.target = moon; scene.add(sun);
const floor = new THREE.Mesh(new THREE.CircleGeometry(6, 64), new THREE.ShaderMaterial({ transparent: true, depthWrite: false, uniforms: { uO: { value: 0 } },
  vertexShader: 'varying vec2 vU; void main(){ vU = position.xy; gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.0); }',
  fragmentShader: 'varying vec2 vU; uniform float uO; void main(){ float d = length(vU); float a = smoothstep(2.6,0.2,d)*0.55*uO; gl_FragColor = vec4(0.08,0.13,0.24,a); }' }));
floor.rotation.x = -Math.PI / 2; floor.position.y = -0.002; scene.add(floor);
const shadowCatcher = new THREE.Mesh(new THREE.PlaneGeometry(5, 5), new THREE.ShadowMaterial({ opacity: 0.45 }));
shadowCatcher.rotation.x = -Math.PI / 2; shadowCatcher.position.y = -0.001; shadowCatcher.receiveShadow = true; scene.add(shadowCatcher);
// display plinth ring (museum feel)
const plinth = new THREE.Mesh(new THREE.RingGeometry(0.55, 0.565, 128), new THREE.MeshBasicMaterial({ color: ACC, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false }));
plinth.rotation.x = -Math.PI / 2; plinth.position.y = 0.001; scene.add(plinth);

// ------------------------------------------------------------------ load + normalise model
const draco = new DRACOLoader(); draco.setDecoderPath('https://www.gstatic.com/draco/versioned/decoders/1.5.7/');
const loader = new GLTFLoader(); loader.setDRACOLoader(draco);
const model = new THREE.Group(); scene.add(model);
const FIT = C.fit || 1.4;
let H = 1, R = 1, ready = false, modelCorners = [];
const center = new THREE.Vector3(0, 0.7, 0);
const meshes = [], partOf = new Map(), parts = {}, glow = [];
const hub = { pos: new THREE.Vector3(), meshes: [], has: false };
function visBox(root) { // bounding box of visible meshes only
  const b = new THREE.Box3(), t = new THREE.Box3();
  root.traverse((o) => { let v = true, n = o; while (n) { if (!n.visible) { v = false; break; } n = n.parent; } if (v && o.isMesh) { o.geometry.computeBoundingBox(); t.copy(o.geometry.boundingBox).applyMatrix4(o.matrixWorld); b.union(t); } });
  return b;
}
const nameChain = (o) => { let s = '', n = o; while (n && n !== model) { s += ' ' + n.name; n = n.parent; } return s; };
const rx = (s) => (s ? new RegExp(s) : null);
const reCycle = rx(C.glow?.cycle), reBeat = rx(C.glow?.beat), reHide = rx(C.hide);

loader.load(C.model, (g) => {
  const root = g.scene;
  if (C.rot) root.rotation.set(...C.rot.map((d) => (d * Math.PI) / 180));
  root.updateMatrixWorld(true);
  root.traverse((o) => { if ((reHide && reHide.test(o.name)) || /(^|_)(Lunar_Ground|Ground|Studio_Floor|Floor|Regolith_Ground|Backdrop)$/i.test(o.name)) o.visible = false; });
  const box = visBox(root), size = box.getSize(new THREE.Vector3()), c = box.getCenter(new THREE.Vector3());
  const s = FIT / Math.max(size.x, size.y, size.z);
  const wrap = new THREE.Group(); wrap.add(root); wrap.scale.setScalar(s); wrap.position.set(-c.x * s, -box.min.y * s, -c.z * s);
  model.add(wrap); model.updateMatrixWorld(true);
  modelCorners = []; for (const x of [box.min.x, box.max.x]) for (const y of [box.min.y, box.max.y]) for (const z of [box.min.z, box.max.z]) modelCorners.push(model.worldToLocal(wrap.localToWorld(new THREE.Vector3(x, y, z))));
  H = size.y * s; R = 0.5 * Math.hypot(size.x, size.y, size.z) * s; center.set(0, H / 2, 0);
  plinth.scale.setScalar(Math.max(0.5, Math.hypot(size.x, size.z) * s * 0.75) / 0.55);
  Object.assign(key.shadow.camera, { left: -R * 1.4, right: R * 1.4, top: H + 0.3, bottom: -0.3 }); key.shadow.camera.updateProjectionMatrix();
  controls.minDistance = R * 0.35; controls.maxDistance = R * 7;

  const partRe = C.parts.map((p) => [p, new RegExp(p.match)]);
  const hubRe = rx(C.hub?.match);
  root.traverse((o) => {
    if (!o.isMesh || !o.visible) return;
    o.castShadow = true; o.receiveShadow = true; meshes.push(o);
    const nAttr = o.geometry.attributes.normal;
    if (!nAttr) o.geometry.computeVertexNormals();
    else { let bad = false; for (let i = 0; i < nAttr.count; i++) { const x = nAttr.getX(i), y = nAttr.getY(i), z = nAttr.getZ(i), l = x * x + y * y + z * z; if (!(l > 1e-8) || !isFinite(l)) { bad = true; break; } } if (bad) { o.geometry.computeVertexNormals(); const N2 = o.geometry.attributes.normal; for (let i = 0; i < N2.count; i++) { const l = N2.getX(i) ** 2 + N2.getY(i) ** 2 + N2.getZ(i) ** 2; if (!(l > 1e-8) || !isFinite(l)) N2.setXYZ(i, 0, 1, 0); } N2.needsUpdate = true; } }
    const chain = nameChain(o);
    const mats = Array.isArray(o.material) ? o.material : [o.material];
    mats.forEach((m) => {
      const em = m.emissive && (m.emissive.r + m.emissive.g + m.emissive.b) > 0.02;
      if (em && !m.userData.lx) {
        m.userData.lx = { c: m.emissive.clone(), i: m.emissiveIntensity || 1, cyc: reCycle ? reCycle.test(m.name) || reCycle.test(chain) : false, beat: reBeat ? reBeat.test(m.name) || reBeat.test(chain) : false, ph: Math.random() * 6.28 };
        glow.push(m);
      }
    });
    for (const [p, re] of partRe) if (re.test(chain)) {
      if (!partOf.has(o)) partOf.set(o, p.k);
      (parts[p.k] = parts[p.k] || { p, meshes: [] }).meshes.push(o);
    }
    if (hubRe && hubRe.test(chain)) hub.meshes.push(o);
  });
  // part anchors (in model-local space) + outward direction
  for (const k in parts) {
    const b = new THREE.Box3(); parts[k].meshes.forEach((m) => b.expandByObject(m));
    const w = b.getCenter(new THREE.Vector3()); parts[k].local = model.worldToLocal(w.clone());
    const n = new THREE.Vector3(parts[k].local.x, 0, parts[k].local.z);
    parts[k].n = n.lengthSq() > 1e-6 ? n.normalize() : new THREE.Vector3(0, 0, 1);
    if (parts[k].p.face) parts[k].n.set(...parts[k].p.face).normalize();
    parts[k].size = b.getSize(new THREE.Vector3()).length();
    // highlight-able copies of non-glowing materials
    parts[k].meshes.forEach((m) => {
      if (Array.isArray(m.material)) return;
      if (!m.material.userData.lx && m.material.emissive) { m.material = m.material.clone(); m.material.userData.hl = true; m.material.emissive = new THREE.Color(0); }
    });
  }
  if (hub.meshes.length) { const b = new THREE.Box3(); hub.meshes.forEach((m) => b.expandByObject(m)); hub.local = model.worldToLocal(b.getCenter(new THREE.Vector3())); hub.has = true;
    const n = new THREE.Vector3(hub.local.x, 0, hub.local.z); hub.n = C.hub.face ? new THREE.Vector3(...C.hub.face).normalize() : (n.lengthSq() > 1e-6 ? n.normalize() : new THREE.Vector3(0, 0, 1)); }
  else { hub.local = center.clone(); hub.n = new THREE.Vector3(0, 0, 1); }
  // tell the page which callouts have real anchors
  $$('.co').forEach((el) => { if (!parts[el.dataset.part]) el.remove(); });
  camState.target.copy(center); camState.dist = R * 3;
  ready = true; done();
}, (e) => { const p = e.total ? Math.round((e.loaded / e.total) * 100) : 0; $('#load .bar i').style.width = p + '%'; $('#lpct').textContent = `Loading ${C.short} ${p}%`; },
(err) => { console.error(err); $('#lpct').textContent = 'Could not load the 3D model. Scroll down for the renders.'; setTimeout(done, 1200); });
window.LX = { get state() { return { ready, mode, curStep, R, H, parts: Object.keys(parts), hub: hub.has, glow: glow.length, meshes: meshes.length, cam: camera.position.toArray().map((x) => +x.toFixed(2)), dist: +camState.dist.toFixed(2) }; } };
let t0 = 0; function done() { $('#load').classList.add('done'); t0 = performance.now(); }

// ------------------------------------------------------------------ scan effects
const sweepMat = new THREE.MeshBasicMaterial({ color: ACC, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false });
const sweep = new THREE.Mesh(new THREE.TorusGeometry(1, 0.012, 8, 128), sweepMat); sweep.rotation.x = Math.PI / 2; scene.add(sweep);
const discMat = new THREE.MeshBasicMaterial({ color: ACC, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
const disc = new THREE.Mesh(new THREE.CircleGeometry(1, 64), discMat); disc.rotation.x = -Math.PI / 2; scene.add(disc);
const ringMat = new THREE.MeshBasicMaterial({ color: ACC, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
const scanRing = new THREE.Mesh(new THREE.RingGeometry(0.9, 1, 96), ringMat); scene.add(scanRing);
let scanT = -1, holoOn = false, focusUntil = 0;
function runScan() { if (!ready) return; scanT = 0; holoOn = true; $('#holo').classList.add('on'); sfx.scan(); if (mode !== 'scan') focusUntil = clock.elapsedTime + 8.5; }
function clearHolo() { holoOn = false; focusUntil = 0; $('#holo').classList.remove('on'); }
$$('[data-scan]').forEach((b) => b.addEventListener('click', (e) => { e.stopPropagation(); runScan(); }));
setInterval(() => { if (!holoOn) return; $$('[data-h]').forEach((el) => { const h = C.holo[+el.dataset.h]; if (h.t == null) el.textContent = (h.v + (Math.random() - 0.5) * 2 * (h.j || 0)).toFixed(h.d || 0); }); }, 1300);

// ------------------------------------------------------------------ sound (shares the site's mute switch)
const sfx = (() => {
  let ctx = null;
  const on = () => { try { return localStorage.getItem('ares-sound') === 'on'; } catch (e) { return false; } };
  const ac = () => { if (!ctx) { const A = window.AudioContext || window.webkitAudioContext; if (!A) return null; ctx = new A(); } if (ctx.state === 'suspended') ctx.resume(); return ctx; };
  function tone(f, d, type = 'sine', v = 0.04, w = 0, f2) { const c = ac(); if (!c || !on()) return; const t = c.currentTime + w, o = c.createOscillator(), g = c.createGain();
    o.type = type; o.frequency.setValueAtTime(f, t); if (f2) o.frequency.exponentialRampToValueAtTime(f2, t + d);
    g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(v, t + 0.02); g.gain.exponentialRampToValueAtTime(0.0001, t + d); o.connect(g).connect(c.destination); o.start(t); o.stop(t + d + 0.05); }
  return { on,
    chirp() { tone(2100, 0.06, 'sine', 0.02); tone(2800, 0.05, 'sine', 0.014, 0.05); },
    click() { tone(240, 0.03, 'square', 0.02); tone(1500, 0.05, 'sine', 0.02, 0.02); },
    open() { tone(520, 0.12, 'sine', 0.02); tone(780, 0.16, 'sine', 0.018, 0.08); },
    startup() { tone(110, 1.4, 'sine', 0.05, 0, 220); tone(440, 0.25, 'sine', 0.025, 0.9); tone(660, 0.3, 'sine', 0.025, 1.1); tone(880, 0.5, 'sine', 0.02, 1.3); },
    scan() { tone(300, 1.2, 'sawtooth', 0.012, 0, 1600); tone(1760, 0.12, 'sine', 0.02, 1.2); tone(2350, 0.18, 'sine', 0.018, 1.35); },
    whoosh() { tone(180, 0.9, 'triangle', 0.015, 0, 420); } };
})();
$('#bSound').addEventListener('click', () => { const b = document.getElementById('ares-snd'); if (!sfx.on() && b) b.click(); setTimeout(() => sfx.startup(), 120); $('#bSound').textContent = '● Systems online'; });

// ------------------------------------------------------------------ scroll story
const stepEls = $$('.step');
let mode = 'hero', curStep = -1, stepP = 0;
const io = new IntersectionObserver((es) => es.forEach((e) => { if (e.isIntersecting) { e.target.classList.add('vis'); if (e.target.id === 'specs') countUp(); } }), { threshold: 0.2 });
stepEls.forEach((s) => io.observe(s)); $$('.reveal').forEach((s) => io.observe(s));
function updateScroll() {
  const vh = innerHeight; let m = 'hero', best = -1, idx = -1;
  stepEls.forEach((s, i) => { const r = s.getBoundingClientRect(); const c = Math.max(0, Math.min(r.bottom, vh) - Math.max(r.top, 0)) / vh; if (c > best && c > 0.35) { best = c; m = s.dataset.mode; idx = i; } });
  if ($('.content').getBoundingClientRect().top < vh * 0.55) { m = 'content'; idx = -1; }
  if (scrollY < vh * 0.5) { m = 'hero'; idx = -1; }
  if (m !== mode || idx !== curStep) { mode = m; curStep = idx; sfx.chirp(); }
  if (idx >= 0) { const r = stepEls[idx].getBoundingClientRect(); stepP = Math.max(0, Math.min(1, (vh - r.top) / (r.height + vh))); }
  $('#callouts').classList.toggle('on', mode === 'callouts');
  if (mode !== 'scan' && clock.elapsedTime > focusUntil) clearHolo();
  $$('#stepnav .sn-dot').forEach((d) => d.classList.toggle('on', d.dataset.go === curStopId()));
}
addEventListener('scroll', updateScroll, { passive: true });

// step navigator: dots, arrows, keyboard
const stops = ['hero', ...stepEls.map((s) => s.id), 'overview'];
const stopEl = (id) => (id === 'hero' ? document.body : document.getElementById(id));
const labels = { hero: 'Intro', overview: 'Details' };
C.steps.forEach((s, i) => { labels['s' + (i + 1)] = String(i + 1).padStart(2, '0') + ' · ' + s.kick; });
const stepNav = document.createElement('div'); stepNav.id = 'stepnav';
stepNav.innerHTML = '<button class="sn-arr" data-d="-1" type="button" aria-label="Previous section">▲</button>' +
  stops.map((id, i) => `<button class="sn-dot" data-go="${id}" type="button" aria-label="${esc(labels[id])}"><span>${esc(labels[id])}</span><b>${id === 'hero' ? '◦' : id === 'overview' ? '≡' : String(i).padStart(2, '0')}</b></button>`).join('') +
  '<button class="sn-arr" data-d="1" type="button" aria-label="Next section">▼</button><div class="sn-tip">↑ ↓ keys</div>';
document.body.appendChild(stepNav);
function curStopId() { const vh = innerHeight; let id = 'hero'; stops.forEach((sid) => { if (sid !== 'hero' && stopEl(sid).getBoundingClientRect().top < vh * 0.45) id = sid; }); return id; }
function goStop(id) { const el = stopEl(id); const y = id === 'hero' ? 0 : el.getBoundingClientRect().top + scrollY + (id === 'overview' ? -60 : Math.max(0, (el.offsetHeight - innerHeight) / 2)); scrollTo({ top: y, behavior: reduced ? 'auto' : 'smooth' }); }
function stepBy(d) { const i = stops.indexOf(curStopId()); goStop(stops[Math.max(0, Math.min(stops.length - 1, i + d))]); sfx.chirp(); }
stepNav.addEventListener('click', (e) => { const b = e.target.closest('button'); if (!b) return; e.stopPropagation(); if (b.dataset.go) { goStop(b.dataset.go); sfx.chirp(); } else stepBy(+b.dataset.d); });
addEventListener('keydown', (e) => {
  if (e.key === 'Escape') { if (exploring) setExplore(false); else if (drawerPart) closeDrawer(); else clearHolo(); return; }
  if (e.target.closest('input,textarea,select,summary') || e.metaKey || e.ctrlKey || e.altKey || exploring) return;
  const here = curStopId();
  if (['ArrowDown', 'ArrowRight', 'PageDown', ' '].includes(e.key)) { if (here === 'overview') return; e.preventDefault(); stepBy(1); }
  else if (['ArrowUp', 'ArrowLeft', 'PageUp'].includes(e.key)) { if (here === 'overview' && $('#overview').getBoundingClientRect().top < -40) return; e.preventDefault(); stepBy(-1); }
});

// ------------------------------------------------------------------ pointer: hover tooltips, click to brief / scan
const ndc = new THREE.Vector2(-9, -9), mouse = new THREE.Vector2(), ray = new THREE.Raycaster();
let hoverPart = null, hoverHub = false, lastMove = { x: 0, y: 0 };
addEventListener('pointermove', (e) => { mouse.set(e.clientX / innerWidth - 0.5, e.clientY / innerHeight - 0.5); ndc.set((e.clientX / innerWidth) * 2 - 1, -(e.clientY / innerHeight) * 2 + 1); lastMove = { x: e.clientX, y: e.clientY }; });
const tip = $('#tip');
function pick() {
  if (!ready || mode === 'content') return null;
  ray.setFromCamera(ndc, camera);
  const hit = ray.intersectObjects(meshes, false)[0]; if (!hit) return null;
  return { mesh: hit.object, part: partOf.get(hit.object) || null, hub: hub.meshes.includes(hit.object) };
}
let downAt = null;
renderer.domElement.addEventListener('pointerdown', (e) => { downAt = { x: e.clientX, y: e.clientY }; });
addEventListener('click', (e) => {
  if (!ready || mode === 'content') return;
  if (e.target.closest('a,button,.panel,nav,#drawer,#tools,#stepnav,.co,summary')) return;
  if (downAt && Math.hypot(e.clientX - downAt.x, e.clientY - downAt.y) > 6) return; // was a drag
  ndc.set((e.clientX / innerWidth) * 2 - 1, -(e.clientY / innerHeight) * 2 + 1);
  const p = pick();
  if (p?.hub && (mode === 'scan' || !p.part)) { sfx.click(); runScan(); return; }
  if (p?.part) { sfx.click(); openDrawer(p.part); return; }
  if (p?.hub) { sfx.click(); runScan(); return; }
  if (holoOn && mode !== 'scan') clearHolo();
});
$$('.co').forEach((el) => {
  el.addEventListener('click', (e) => { e.stopPropagation(); sfx.click(); openDrawer(el.dataset.part); });
  el.addEventListener('pointerenter', () => { hoverPart = el.dataset.part; });
  el.addEventListener('pointerleave', () => { hoverPart = null; });
});
$$('[data-part]').forEach((el) => { if (!el.classList.contains('co')) el.addEventListener('click', () => { sfx.click(); openDrawer(el.dataset.part); }); });

// ------------------------------------------------------------------ expanding info drawer
const drawer = $('#drawer'); let drawerPart = null;
const sysName = Object.fromEntries(SYSTEMS.map(([k, i, n]) => [k, i + ' ' + n]));
function openDrawer(k) {
  const P = C.parts.find((p) => p.k === k); if (!P) return;
  drawerPart = k; sfx.open();
  drawer.innerHTML = `<button class="x" type="button" aria-label="Close">✕</button><div class="kick">${esc(C.short)} · System briefing</div><h3>${esc(P.label)}</h3><p class="lead">${esc(P.what || P.sub)}</p>
    ${P.metrics ? `<div class="metrics">${P.metrics.map(([v, l]) => `<div><b>${esc(v)}</b><small>${esc(l)}</small></div>`).join('')}</div>` : ''}
    <details open><summary><i>1</i>Why astronauts need it</summary><p>${esc(P.why)}</p></details>
    <details open><summary><i>2</i>How it works</summary><p>${esc(P.how)}</p></details>
    <details><summary><i>3</i>The problem it solves on the Moon</summary><p>${esc(P.problem)}</p></details>
    <details><summary><i>4</i>How it connects to the habitat</summary><p>${esc(P.connect)}</p></details>
    ${P.links ? `<div class="kick" style="margin:14px 0 8px">Habitat systems</div><div class="chips">${P.links.map((l) => `<span class="chip" data-sys="${l}">${esc(sysName[l] || l)}</span>`).join('')}</div>` : ''}
    <div class="btns" style="margin-top:18px"><button class="btn sq" data-prev type="button">← Previous</button><button class="btn sq" data-next type="button">Next system →</button></div>`;
  drawer.classList.add('on');
  $('.x', drawer).onclick = closeDrawer;
  const i = C.parts.findIndex((p) => p.k === k);
  $('[data-next]', drawer).onclick = () => openDrawer(C.parts[(i + 1) % C.parts.length].k);
  $('[data-prev]', drawer).onclick = () => openDrawer(C.parts[(i - 1 + C.parts.length) % C.parts.length].k);
  $$('[data-sys]', drawer).forEach((c) => (c.onclick = () => { closeDrawer(); selSys(c.dataset.sys); document.getElementById('habitat').scrollIntoView({ behavior: 'smooth' }); }));
  $$('.co').forEach((c) => c.classList.toggle('hot', c.dataset.part === k));
}
function closeDrawer() { drawerPart = null; drawer.classList.remove('on'); $$('.co').forEach((c) => c.classList.remove('hot')); }

// ------------------------------------------------------------------ explore (free orbit + zoom), fly-around, zoom buttons
let exploring = false, zoomMul = 1, flyT = -1;
function setExplore(on) {
  exploring = on; document.body.classList.toggle('explore', on); $('#tX').classList.toggle('on', on); $('#tX').textContent = on ? '✕ Exit 360°' : '✥ Explore 360°';
  controls.enabled = on; if (on) { controls.target.copy(camState.target); controls.update(); flyT = -1; sfx.whoosh(); }
}
$('#tX').onclick = () => setExplore(!exploring);
const fly = () => { if (exploring) setExplore(false); flyT = 0; flyAz0 = camAz; sfx.whoosh(); };
let flyAz0 = 0, camAz = 0;
$('#tFly').onclick = fly; $('#bFly2').onclick = fly;
function zoom(f) { if (exploring) { const d = camera.position.clone().sub(controls.target).multiplyScalar(f); camera.position.copy(controls.target).add(d); } else zoomMul = Math.max(0.45, Math.min(1.8, zoomMul * f)); }
$('#tIn').onclick = () => zoom(0.82); $('#tOut').onclick = () => zoom(1.22);
$('#tR').onclick = () => { zoomMul = 1; flyT = -1; if (exploring) setExplore(false); closeDrawer(); clearHolo(); };

// ------------------------------------------------------------------ callout + hologram layout
const v3 = new THREE.Vector3();
const toScreen = (p) => { v3.copy(p).project(camera); return [(v3.x * 0.5 + 0.5) * innerWidth, (-v3.y * 0.5 + 0.5) * innerHeight, v3.z]; };
const worldOf = (local) => model.localToWorld(local.clone());
const facingOf = (n) => { const w = n.clone().applyAxisAngle(new THREE.Vector3(0, 1, 0), model.rotation.y); const to = camera.position.clone().sub(model.position); to.y = 0; to.normalize(); return w.x * to.x + w.z * to.z; };
const svg = $('#callouts svg');
function layoutOverlays(T) {
  const W = innerWidth, Hh = innerHeight;
  const showAll = mode === 'callouts', want = drawerPart || hoverPart;
  let lines = '';
  if (showAll || want) {
    // screen extent of the model
    let minX = 1e9, maxX = -1e9;
    modelCorners.forEach((c) => { const [x] = toScreen(model.localToWorld(c.clone())); minX = Math.min(minX, x); maxX = Math.max(maxX, x); });
    const [cx] = toScreen(center);
    const gap = 36, top = 84, bot = Hh - 70;
    const cols = { '-1': [], '1': [] };
    $$('.co').forEach((el) => {
      const P = parts[el.dataset.part]; if (!P) return;
      const on = showAll || el.dataset.part === want;
      const [ax, ay, az] = toScreen(worldOf(P.local)); const f = facingOf(P.n);
      const vis = on && f > -0.15 && az < 1 && ax > 0 && ax < W && ay > 0 && ay < Hh;
      el.classList.toggle('hidden', !vis); el.style.opacity = vis ? 1 : 0; el.style.pointerEvents = vis ? 'auto' : 'none';
      if (!vis) return;
      const side = ax < cx ? -1 : 1;
      cols[side].push({ el, ax, ay, w: el.offsetWidth, h: el.offsetHeight });
    });
    [-1, 1].forEach((side) => {
      const col = cols[side].sort((a, b) => a.ay - b.ay);
      // stack labels in a column beside the model, as close to their anchor height as possible
      col.forEach((it) => { it.y = Math.max(top, Math.min(bot - it.h, it.ay - it.h / 2)); });
      for (let i = 1; i < col.length; i++) { const min = col[i - 1].y + col[i - 1].h + 10; if (col[i].y < min) col[i].y = min; }
      const over = col.length ? col[col.length - 1].y + col[col.length - 1].h - bot : 0;
      if (over > 0) col.forEach((it) => { it.y = Math.max(top, it.y - over); });
      col.forEach((it) => {
        let x = side < 0 ? minX - gap - it.w : maxX + gap;
        x = Math.max(10, Math.min(W - it.w - 64, x));
        // if the column would cover the model (narrow screens), keep it at the screen edge
        it.el.style.left = x + 'px'; it.el.style.top = it.y + 'px';
        const ex = side < 0 ? x + it.w : x, ey = it.y + it.h / 2;
        const kx = side < 0 ? Math.min(ex + 18, it.ax - 8) : Math.max(ex - 18, it.ax + 8);
        lines += `<polyline points="${it.ax},${it.ay} ${kx},${ey} ${ex},${ey}"/><circle cx="${it.ax}" cy="${it.ay}" r="3.4"/><circle class="ping" cx="${it.ax}" cy="${it.ay}" r="4"/>`;
      });
    });
    $('#callouts').style.opacity = 1;
  } else { $('#callouts').style.opacity = ''; $$('.co').forEach((el) => { el.style.opacity = ''; el.style.pointerEvents = ''; }); }
  svg.innerHTML = lines;
  // hologram ring around the hub
  const [hx, hy] = toScreen(worldOf(hub.local)); const hf = facingOf(hub.n);
  const Rr = Math.min(250, W * 0.26);
  $$('#holo .hs').forEach((el, i, all) => { const a = -Math.PI * 0.92 + (i / Math.max(1, all.length - 1)) * Math.PI * 0.84 + (i % 2 ? Math.PI : 0) * 0;
    const ang = (i / all.length) * Math.PI * 2 - Math.PI * 0.75;
    el.style.left = Math.max(8, Math.min(W - el.offsetWidth - 8, hx + Math.cos(ang) * Rr - el.offsetWidth / 2)) + 'px'; el.style.top = Math.max(70, hy + Math.sin(ang) * Rr * 0.62 - el.offsetHeight / 2) + 'px'; });
  $('#holo').style.opacity = Math.max(0, Math.min(1, (hf + 0.1) * 3));
  const hint = $('#hint'); hint.style.left = hx + 'px'; hint.style.top = hy + 'px';
  hint.style.opacity = hf > 0.2 && !exploring && ((mode === 'scan' && !holoOn) || (hoverHub && mode !== 'content')) ? 1 : 0;
}

// ------------------------------------------------------------------ systems diagram + compare + specs + gallery
const sysd = $('#sysd');
function drawSys() {
  const cx = 280, cy = 280, r = 205; const on = C.systems || {};
  let h = `<defs><radialGradient id="cg"><stop offset="0" stop-color="${C.accent || '#5AB8FF'}" stop-opacity=".55"/><stop offset="1" stop-color="#0a1430" stop-opacity=".95"/></radialGradient></defs>`;
  const pos = SYSTEMS.map((s, i) => { const a = (i / SYSTEMS.length) * Math.PI * 2 - Math.PI / 2; return [cx + Math.cos(a) * r, cy + Math.sin(a) * r]; });
  SYSTEMS.forEach(([k], i) => { const [x, y] = pos[i]; const mx = (cx + x) / 2 + (y - cy) * 0.12, my = (cy + y) / 2 - (x - cx) * 0.12; h += `<path class="link${on[k] ? ' on' : ''}" data-l="${k}" d="M${cx},${cy} Q${mx},${my} ${x},${y}"/>`; });
  SYSTEMS.forEach(([k, ico, name], i) => { const [x, y] = pos[i]; h += `<g class="node${on[k] ? ' on' : ''}" data-sys="${k}" tabindex="0"><circle cx="${x}" cy="${y}" r="38"/><text class="ico" x="${x}" y="${y + 2}">${ico}</text><text x="${x}" y="${y + 56}">${name.toUpperCase()}</text></g>`; });
  h += `<g class="core"><circle cx="${cx}" cy="${cy}" r="70"/><text x="${cx}" y="${cy - 4}">${esc(C.short.split(' ')[0].toUpperCase())}</text><text x="${cx}" y="${cy + 16}" style="font-size:10px;fill:#bfe6ff">${esc((C.short.split(' ').slice(1).join(' ') || '').toUpperCase())}</text></g>`;
  sysd.innerHTML = h;
  $$('.node', sysd).forEach((n) => { n.addEventListener('click', () => selSys(n.dataset.sys)); n.addEventListener('keydown', (e) => { if (e.key === 'Enter') selSys(n.dataset.sys); }); });
}
function selSys(k) {
  const s = SYSTEMS.find((x) => x[0] === k); const txt = C.systems?.[k];
  $$('.node', sysd).forEach((n) => n.classList.toggle('sel', n.dataset.sys === k));
  const via = C.parts.filter((p) => p.links?.includes(k)).map((p) => p.label);
  $('#sysinfo').innerHTML = `<div class="kick">${txt ? 'Connected system' : 'Indirect link'}</div><h3>${s[1]} ${esc(s[2])}</h3><p>${esc(txt || `The ${C.short} has no direct link to ${s[2].toLowerCase()}. It reaches it through the LUNA habitat AI, which shares crew data across every system.`)}</p>
    ${via.length ? `<div class="flow">VIA: ${via.map(esc).join(' · ')}</div>` : ''}`;
}
drawSys(); selSys(Object.keys(C.systems || { health: 1 })[0]);
function drawCmp(filter = 'all') {
  const grp = C.cmpGroups || { eva: ['helmet', 'glove', 'boot', 'suit'], hab: ['uniform', 'glove', 'helmet'] };
  const ids = FAMILY.map((f) => f.id).filter((id) => CMP[id] && (filter === 'all' || (grp[filter] || []).includes(id) || id === C.id));
  $('#cmpt').innerHTML = `<thead><tr><th></th>${ids.map((id) => { const f = FAMILY.find((x) => x.id === id); return `<th class="${id === C.id ? 'me' : ''}">${id === C.id ? esc(f.name) + ' ●' : `<a href="${f.href}">${esc(f.name)} ↗</a>`}</th>`; }).join('')}</tr></thead>
    <tbody>${CMP.rows.map((r, i) => `<tr><th>${r}</th>${ids.map((id) => `<td class="${id === C.id ? 'me' : ''}">${esc(CMP[id][i])}</td>`).join('')}</tr>`).join('')}</tbody>`;
}
drawCmp(); $$('[data-cmp]').forEach((b) => (b.onclick = () => { $$('[data-cmp]').forEach((x) => x.classList.toggle('on', x === b)); drawCmp(b.dataset.cmp); }));
let counted = false;
function countUp() {
  if (counted) return; counted = true;
  $$('.spec').forEach((el) => { el.style.setProperty('--p', el.dataset.p + '%'); const v = parseFloat(el.dataset.v); if (isNaN(v)) return; const span = $('.cnt', el); const pre = span.textContent.replace(/[\d.,]+.*$/, ''); const dec = (String(el.dataset.v).split('.')[1] || '').length; const t1 = performance.now();
    const f = () => { const k = Math.min(1, (performance.now() - t1) / 1400), e = 1 - Math.pow(1 - k, 3); span.textContent = pre + (v * e).toFixed(dec); if (k < 1) requestAnimationFrame(f); }; f(); });
}
const lb = $('#lb'), lbi = $('img', lb);
$$('.gal').forEach((g) => g.addEventListener('click', (e) => { const i = e.target.closest('img'); if (i) { lbi.src = i.src; lb.classList.add('on'); } }));
lb.onclick = () => lb.classList.remove('on');

// ------------------------------------------------------------------ animation loop
const clock = new THREE.Clock();
const ss = (a, b, x) => { const t = Math.min(1, Math.max(0, (x - a) / (b - a))); return t * t * (3 - 2 * t); };
const CYC = C.cycle || [[0x5ab8ff, 0], [0x26ff5a, 2.5], [0x1ae6ff, 5], [0x9e4dff, 7.5], [0xff9e14, 10], [0x26ff5a, 11.2]];
const cA = new THREE.Color(), cB = new THREE.Color(), cNow = new THREE.Color();
function cycleColor(t) { const u = ((t % 12) + 12) % 12; let i = 0; while (i < CYC.length - 1 && u >= CYC[i + 1][1]) i++;
  const [ca, ta] = CYC[i], [cb, tb] = CYC[Math.min(i + 1, CYC.length - 1)]; const k = tb > ta ? Math.min(1, Math.max(0, (u - ta - (tb - ta) * 0.8) / ((tb - ta) * 0.2))) : 0; return cNow.copy(cA.set(ca)).lerp(cB.set(cb), k); }
const camState = { target: new THREE.Vector3(0, 0.7, 0), dist: 4, h: 0.25 };
const want = { target: new THREE.Vector3(), dist: 4, h: 0.25 };
let modelRot = 0, wantRot = 0, powered = false;
const wrapNear = (a, ref) => a + Math.round((ref - a) / (Math.PI * 2)) * Math.PI * 2;
const tmp = new THREE.Vector3();
function tick() {
  const dt = Math.min(0.05, clock.getDelta()), T = clock.elapsedTime;
  const it = ready ? (performance.now() - t0) / 1000 : 0, sk = reduced ? 99 : it;
  starMat.uniforms.uO.value = ss(0, 1.8, sk); starMat.uniforms.uT.value = T;
  moonMat.opacity = ss(1, 4, sk); moon.rotation.y = T * 0.01; sun.intensity = 2.2 * ss(1, 4, sk);
  const emerge = ss(2.4, 5.2, sk);
  lights.forEach((l) => { l.intensity = l.userData.full * emerge; });
  scene.environmentIntensity = 0.38 * emerge; floor.material.uniforms.uO.value = emerge; shadowCatcher.material.opacity = 0.45 * emerge;
  model.position.y = (1 - emerge) * -0.08 * H; model.visible = emerge > 0.001;
  const power = ss(5, 6.6, sk);
  if (power > 0.02 && !powered) { powered = true; $('#hero').classList.add('on'); if (sfx.on()) sfx.startup(); }
  plinth.material.opacity = 0.35 * power * (0.75 + 0.25 * Math.sin(T * 1.6));

  // lights on the model: power-on, heartbeat, colour cycle, gentle shimmer
  const hb = (T * 1.2) % 1, beat = Math.exp(-Math.pow((hb - 0.08) / 0.035, 2)) + 0.6 * Math.exp(-Math.pow((hb - 0.24) / 0.04, 2));
  const cc = power > 0.5 ? cycleColor(sk - 6.6) : cA.set(CYC[0][0]);
  for (const m of glow) { const L = m.userData.lx; let i = L.i * power;
    if (L.cyc) { m.emissive.copy(cc); i *= 0.9 + 0.25 * Math.sin(T * 3 + L.ph); } else i *= 0.88 + 0.12 * Math.sin(T * 2.2 + L.ph);
    if (L.beat) i *= 0.5 + 1.6 * beat; m.emissiveIntensity = i; }
  coreLight.color.copy(cc); coreLight.distance = R * 1.2; coreLight.intensity = power * (0.15 + 0.15 * beat) * (hub.has ? 1 : 0);
  if (ready) { worldOf(hub.local).toArray ? tmp.copy(worldOf(hub.local)) : 0; coreLight.position.copy(tmp).add(hub.n.clone().applyAxisAngle(new THREE.Vector3(0, 1, 0), model.rotation.y).multiplyScalar(R * 0.12)); }
  // highlight hovered / briefed part (subtle, accent-tinted)
  for (const k in parts) { const on = k === hoverPart || k === drawerPart; parts[k].meshes.forEach((m) => { const mm = m.material; if (!mm.userData?.hl) return; const t = on ? 0.16 + 0.06 * Math.sin(T * 4) : 0; mm.emissive.lerp(cB.copy(ACC).multiplyScalar(t), Math.min(1, dt * 8)); }); }

  // ---- camera choreography
  const focus = T < focusUntil;
  if (holoOn && !focus && mode !== 'scan' && scanT < 0) clearHolo();
  const S = curStep >= 0 ? C.steps[curStep] : null;
  let near = false;
  if (drawerPart && parts[drawerPart] && mode !== 'content') {
    const P = parts[drawerPart]; wantRot = wrapNear(-Math.atan2(P.n.x, P.n.z), modelRot); want.target.copy(worldOf(P.local)).lerp(center, 0.15); want.dist = Math.max(R * 1.25, Math.min(R * 2.2, P.size * 3.2)); want.h = 0.12; near = true;
  } else if (focus && mode !== 'scan') { wantRot = wrapNear(-Math.atan2(hub.n.x, hub.n.z), modelRot); want.target.copy(worldOf(hub.local)).lerp(center, 0.25); want.dist = R * 1.5; want.h = 0.05; near = true; }
  else if (mode === 'hero') { want.target.copy(center); want.dist = R * 3.1; want.h = 0.26; wantRot = Math.sin(T * 0.07) * 0.5 + (C.heroRot || 0); }
  else if (S && S.mode === 'turn') { want.target.copy(center); want.dist = R * (S.d || 2.6); want.h = S.h ?? 0.2; wantRot = (C.heroRot || 0) + stepP * Math.PI * 2; }
  else if (S && S.mode.startsWith('part:')) { const P = parts[S.mode.slice(5)]; if (P) { wantRot = wrapNear(-Math.atan2(P.n.x, P.n.z) + (S.yaw || 0), modelRot); want.target.copy(worldOf(P.local)).lerp(center, 0.35); want.dist = R * 1.45 * (S.d || 1.1); want.h = S.h ?? 0.08; near = true; } }
  else if (S && S.mode === 'hub') { wantRot = wrapNear(-Math.atan2(hub.n.x, hub.n.z), modelRot); want.target.copy(worldOf(hub.local)).lerp(center, 0.25); want.dist = R * 1.45 * (S.d || 0.9); want.h = S.h ?? 0.04; near = true; }
  else if (S && S.mode === 'callouts') { wantRot = wrapNear((C.calloutRot || 0) + Math.sin(T * 0.15) * 0.25, modelRot); want.target.copy(center); want.dist = R * (S.d || 2.3); want.h = S.h ?? 0.14; }
  else if (S && S.mode === 'scan') { wantRot = wrapNear(-Math.atan2(hub.n.x, hub.n.z), modelRot); want.target.copy(worldOf(hub.local)).lerp(center, 0.35); want.dist = R * 1.3 * (S.d || 1.6); want.h = 0.06; near = true; }
  else { want.target.copy(center); want.dist = R * 3.3; want.h = 0.3; }
  modelRot += (wantRot - modelRot) * Math.min(1, dt * (mode === 'hero' ? 1 : 2.6)); model.rotation.y = modelRot;
  // breathing idle
  const br = Math.sin(T * 2 * Math.PI / 4.6); model.scale.set(1 + 0.0015 * br, 1 + 0.001 * br, 1 + 0.0015 * br);

  if (exploring) { controls.update(); }
  else {
    const k = Math.min(1, dt * 2.2);
    camState.target.lerp(want.target, k); camState.dist += (want.dist * zoomMul - camState.dist) * k; camState.h += (want.h - camState.h) * k;
    let az = (near ? 0.12 * Math.sin(T * 0.1) : 0.3 * Math.sin(T * 0.12)) + mouse.x * (near ? 0.16 : 0.32);
    let el = camState.h + mouse.y * -0.12, dist = camState.dist;
    if (flyT >= 0) { flyT += dt / 9; const e = flyT < 1 ? flyT * flyT * (3 - 2 * flyT) : 1; az += e * Math.PI * 2; el += Math.sin(e * Math.PI) * 0.35; dist *= 1 - Math.sin(e * Math.PI) * 0.25; if (flyT >= 1) flyT = -1; }
    camAz = az;
    camera.position.set(camState.target.x + Math.sin(az) * dist, camState.target.y + el * dist, camState.target.z + Math.cos(az) * dist);
    camera.lookAt(camState.target);
  }
  camera.near = Math.max(0.005, R * 0.01); camera.updateProjectionMatrix();

  // hover → tooltip
  if (ready && mode !== 'content') {
    const p = (lastMove.x || lastMove.y) ? pick() : null;
    const hp = p?.part || null; const hh = !!p?.hub;
    if ((hp && hp !== hoverPart) || (hh && !hoverHub)) sfx.chirp();
    if (!$('.co:hover')) hoverPart = hp; hoverHub = hh;
    document.body.style.cursor = hp || hh ? 'pointer' : '';
    if (hp || hh) { const P = C.parts.find((x) => x.k === hp); tip.innerHTML = hp ? `<b>${esc(P.label)}</b><span>${esc(P.sub)} · click for briefing</span>` : `<b>${esc(C.hub?.label || 'Core')}</b><span>Click to run a scan</span>`;
      tip.style.left = lastMove.x + 'px'; tip.style.top = lastMove.y + 'px'; tip.classList.add('on'); } else tip.classList.remove('on');
  } else tip.classList.remove('on');

  // scan sweep
  if (scanT >= 0) {
    scanT += dt; const u = scanT / 2.4, y = H * 1.05 - H * 1.1 * Math.min(1, u);
    const rr = Math.max(R * 0.7, 0.3); sweep.scale.setScalar(rr); disc.scale.setScalar(rr);
    sweep.position.set(0, y, 0); disc.position.set(0, y, 0);
    sweepMat.opacity = u < 1 ? 0.9 : Math.max(0, 0.9 - (u - 1) * 3); discMat.opacity = sweepMat.opacity * 0.14;
    scanRing.position.copy(worldOf(hub.local)); scanRing.lookAt(camera.position);
    const q = (scanT * 0.8) % 1; scanRing.scale.setScalar(R * 0.08 * (1 + q * 3)); ringMat.opacity = (1 - q) * 0.55;
    if (scanT > 9) { scanT = -1; sweepMat.opacity = 0; discMat.opacity = 0; ringMat.opacity = 0; }
  }
  if (ready) layoutOverlays(T);
  composer.render();
  requestAnimationFrame(tick);
}
addEventListener('resize', () => { camera.aspect = innerWidth / innerHeight; camera.updateProjectionMatrix(); renderer.setSize(innerWidth, innerHeight); composer.setSize(innerWidth, innerHeight); });
updateScroll();
requestAnimationFrame(tick);
