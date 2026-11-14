// Lunar Habitat Operations Uniform — interactive showcase (three.js)
// Intro: stars -> Moon -> suit emerges -> chest module powers on -> title.
// Scroll story: rotate, zoom to chest, system callouts, click-to-scan hologram.
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { DRACOLoader } from 'three/addons/loaders/DRACOLoader.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';

const $ = (s) => document.querySelector(s);
const stage = $('#stage');
const isMobile = matchMedia('(max-width:700px)').matches;
const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;

// ---------------------------------------------------------------- renderer
const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(devicePixelRatio, isMobile ? 1.5 : 2));
renderer.setSize(innerWidth, innerHeight);
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.05;
renderer.shadowMap.enabled = !isMobile;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
stage.appendChild(renderer.domElement);

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x02040b);
const pmrem = new THREE.PMREMGenerator(renderer);
scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
scene.environmentIntensity = 0.0;

const camera = new THREE.PerspectiveCamera(32, innerWidth / innerHeight, 0.02, 400);
camera.position.set(0, 1.1, 4.2);

const composer = new EffectComposer(renderer);
composer.addPass(new RenderPass(scene, camera));
const bloom = new UnrealBloomPass(new THREE.Vector2(innerWidth, innerHeight), 0.55, 0.5, 0.82);
composer.addPass(bloom);
composer.addPass(new OutputPass());

// ---------------------------------------------------------------- lights (ramped in by the intro)
const lights = [];
function addLight(l, full) { l.userData.full = full; l.intensity = 0; scene.add(l); lights.push(l); return l; }
const key = addLight(new THREE.DirectionalLight(0xfff3e6, 1), 2.6);
key.position.set(-2.2, 3.4, 3.2); key.castShadow = !isMobile;
key.shadow.mapSize.set(2048, 2048); key.shadow.camera.left = -1.2; key.shadow.camera.right = 1.2; key.shadow.camera.top = 2; key.shadow.camera.bottom = -0.2;
key.shadow.bias = -0.0004; key.shadow.normalBias = 0.02;
addLight(new THREE.DirectionalLight(0xdfe8ff, 1), 0.9).position.set(3, 1.6, 2.2);
addLight(new THREE.DirectionalLight(0x7fb6ff, 1), 2.2).position.set(-2.5, 2.4, -3);
addLight(new THREE.DirectionalLight(0x9fc8ff, 1), 1.8).position.set(2.6, 2.2, -2.6);
addLight(new THREE.HemisphereLight(0x8aa6d6, 0x0b0f1a, 1), 0.55);
const chestLight = new THREE.PointLight(0x5ab8ff, 0, 0.9, 2); scene.add(chestLight);

// ---------------------------------------------------------------- stars + moon
const starGeo = new THREE.BufferGeometry();
const N = isMobile ? 1800 : 3500, sp = new Float32Array(N * 3), sc = new Float32Array(N);
for (let i = 0; i < N; i++) {
  const u = Math.random() * 2 - 1, t = Math.random() * Math.PI * 2, r = 90 + Math.random() * 60, s = Math.sqrt(1 - u * u);
  sp.set([r * s * Math.cos(t), r * u, r * s * Math.sin(t)], i * 3); sc[i] = Math.random();
}
starGeo.setAttribute('position', new THREE.BufferAttribute(sp, 3));
starGeo.setAttribute('tw', new THREE.BufferAttribute(sc, 1));
const starMat = new THREE.ShaderMaterial({
  transparent: true, depthWrite: false, uniforms: { uT: { value: 0 }, uO: { value: 0 } },
  vertexShader: 'attribute float tw; varying float vT; uniform float uT; void main(){ vT = 0.55 + 0.45*sin(uT*1.3 + tw*40.0); vec4 mv = modelViewMatrix*vec4(position,1.0); gl_PointSize = (1.0 + tw*1.8) * 1.6; gl_Position = projectionMatrix*mv; }',
  fragmentShader: 'varying float vT; uniform float uO; void main(){ float d = length(gl_PointCoord-0.5); if(d>0.5) discard; gl_FragColor = vec4(vec3(0.85,0.9,1.0), (1.0-d*2.0)*vT*uO); }',
});
const stars = new THREE.Points(starGeo, starMat); scene.add(stars);

function moonTexture() {
  const c = document.createElement('canvas'); c.width = 1024; c.height = 512; const g = c.getContext('2d');
  g.fillStyle = '#8d8f95'; g.fillRect(0, 0, 1024, 512);
  for (let i = 0; i < 9; i++) { g.fillStyle = `rgba(60,62,70,${0.25 + Math.random() * 0.2})`; g.beginPath(); g.ellipse(Math.random() * 1024, 100 + Math.random() * 300, 60 + Math.random() * 120, 40 + Math.random() * 80, Math.random() * 3, 0, 7); g.fill(); }
  for (let i = 0; i < 700; i++) {
    const x = Math.random() * 1024, y = Math.random() * 512, r = Math.pow(Math.random(), 3) * 26 + 1.5;
    g.fillStyle = 'rgba(40,42,48,0.35)'; g.beginPath(); g.arc(x, y, r, 0, 7); g.fill();
    g.strokeStyle = 'rgba(210,212,220,0.28)'; g.lineWidth = Math.max(1, r * 0.18); g.beginPath(); g.arc(x - r * 0.12, y - r * 0.12, r, 3.4, 5.6); g.stroke();
  }
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}
const moonMat = new THREE.MeshStandardMaterial({ map: moonTexture(), roughness: 1, metalness: 0, transparent: true, opacity: 0 });
const moon = new THREE.Mesh(new THREE.SphereGeometry(9, 64, 48), moonMat);
moon.position.set(-22, 13, -60); scene.add(moon);
const sun = new THREE.DirectionalLight(0xffffff, 0); sun.position.set(40, 10, -20); sun.target = moon; scene.add(sun);

// floor that fades into darkness
const floor = new THREE.Mesh(new THREE.CircleGeometry(6, 64), new THREE.ShaderMaterial({
  transparent: true, depthWrite: false, uniforms: { uO: { value: 0 } },
  vertexShader: 'varying vec2 vU; void main(){ vU = position.xy; gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.0); }',
  fragmentShader: 'varying vec2 vU; uniform float uO; void main(){ float d = length(vU); float a = smoothstep(2.6,0.2,d)*0.55*uO; gl_FragColor = vec4(0.08,0.13,0.24,a); }',
}));
floor.rotation.x = -Math.PI / 2; floor.position.y = -0.03; scene.add(floor);
const shadowCatcher = new THREE.Mesh(new THREE.PlaneGeometry(4, 4), new THREE.ShadowMaterial({ opacity: 0.45 }));
shadowCatcher.rotation.x = -Math.PI / 2; shadowCatcher.position.y = -0.029; shadowCatcher.receiveShadow = true; scene.add(shadowCatcher);

// ---------------------------------------------------------------- procedural twill normal map
function twillNormal() {
  const S = 256, c = document.createElement('canvas'); c.width = c.height = S; const g = c.getContext('2d'); const im = g.createImageData(S, S);
  const h = (x, y) => 0.5 + 0.5 * Math.sin(((x + y) / S) * Math.PI * 2 * 24) * (0.75 + 0.25 * Math.sin(((x - y) / S) * Math.PI * 2 * 48));
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    const dx = (h(x + 1, y) - h(x - 1, y)) * 1.6, dy = (h(x, y + 1) - h(x, y - 1)) * 1.6, i = (y * S + x) * 4;
    let nx = -dx, ny = -dy, nz = 1; const l = Math.hypot(nx, ny, nz); nx /= l; ny /= l; nz /= l;
    im.data[i] = (nx * 0.5 + 0.5) * 255; im.data[i + 1] = (ny * 0.5 + 0.5) * 255; im.data[i + 2] = (nz * 0.5 + 0.5) * 255; im.data[i + 3] = 255;
  }
  g.putImageData(im, 0, 0);
  const t = new THREE.CanvasTexture(c); t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(220, 220); t.anisotropy = 8; return t;
}
const TWILL = twillNormal();

// material table: Blender material name -> look (linear RGB)
const LOOK = {
  M_Jumpsuit: { c: [0.012, 0.036, 0.135], r: 0.86, sheen: 0.45, tw: 0.35 },
  M_Ripstop: { c: [0.008, 0.024, 0.09], r: 0.88, sheen: 0.3, tw: 0.6 },
  M_Undershirt: { c: [0.01, 0.012, 0.022], r: 0.85, sheen: 0.3, tw: 0.2 },
  M_ZipTape: { c: [0.008, 0.014, 0.035], r: 0.8, tw: 0.3 },
  M_Thread: { c: [0.03, 0.07, 0.2], r: 0.6, sheen: 0.4 },
  M_StrapWebbing: { c: [0.012, 0.012, 0.014], r: 0.72, tw: 0.8 },
  M_PouchNylon: { c: [0.01, 0.011, 0.013], r: 0.75, tw: 0.6 },
  M_Glove: { c: [0.006, 0.008, 0.013], r: 0.75, tw: 0.3 },
  M_GloveLeather: { c: [0.01, 0.009, 0.009], r: 0.45 }, M_GloveTip: { c: [0.025, 0.027, 0.03], r: 0.4 }, M_GloveTab: { c: [0.04, 0.042, 0.046], r: 0.4 },
  M_Boot_Upper: { c: [0.014, 0.015, 0.017], r: 0.5, cc: 0.1 }, M_Boot_Toe: { c: [0.01, 0.01, 0.011], r: 0.42 },
  M_Boot_Sole: { c: [0.012, 0.012, 0.013], r: 0.85 }, M_Boot_Midsole: { c: [0.32, 0.33, 0.34], r: 0.7 },
  M_ZipMetal: { c: [0.42, 0.43, 0.45], r: 0.32, m: 1 }, M_Gunmetal: { c: [0.1, 0.105, 0.11], r: 0.38, m: 1 }, M_Snap: { c: [0.05, 0.055, 0.06], r: 0.35, m: 1 },
  M_Cable: { c: [0.012, 0.012, 0.013], r: 0.45 }, M_PodShell: { c: [0.035, 0.037, 0.04], r: 0.42 }, M_TabletHousing: { c: [0.03, 0.032, 0.036], r: 0.38 },
  M_Rubber: { c: [0.02, 0.02, 0.021], r: 0.7 }, M_PodGlass: { c: [0.004, 0.005, 0.006], r: 0.06, cc: 1 },
};
const EMIT = {}; // name -> [materials] to animate

// ---------------------------------------------------------------- load model
const draco = new DRACOLoader(); draco.setDecoderPath('https://www.gstatic.com/draco/versioned/decoders/1.5.7/');
const loader = new GLTFLoader(); loader.setDRACOLoader(draco);
const suit = new THREE.Group(); scene.add(suit);
let hub = null, hubMeshes = [], scanPivot = null, pulse = null, edgeLEDs = [], coilPivot = null, ready = false;
const hubPos = new THREE.Vector3(0, 1.2, 0.17), hubNormal = new THREE.Vector3(0, 0, 1);

loader.load('model/lhc_uniform.glb', (g) => {
  const root = g.scene;
  root.traverse((o) => {
    if (o.isMesh) {
      o.castShadow = true; o.receiveShadow = true;
      const mats = Array.isArray(o.material) ? o.material : [o.material];
      mats.forEach((m, i) => {
        const L = LOOK[m.name];
        if (L && !m.map) {
          const p = new THREE.MeshPhysicalMaterial({ name: m.name, color: new THREE.Color().setRGB(...L.c), roughness: L.r, metalness: L.m || 0,
            sheen: L.sheen || 0, sheenRoughness: 0.5, sheenColor: new THREE.Color(0x9fb6e6), clearcoat: L.cc || 0, clearcoatRoughness: 0.2 });
          if (L.tw) { p.normalMap = TWILL; p.normalScale.set(L.tw, L.tw); }
          if (Array.isArray(o.material)) o.material[i] = p; else o.material = p;
        }
        const mm = Array.isArray(o.material) ? o.material[i] : o.material;
        if (/LED|CoreLight|HUD|ScanTick|Pulse|EdgeLED|Screen|Lens|O2Label/.test(mm.name)) {
          mm.toneMapped = true; (EMIT[mm.name] = EMIT[mm.name] || []).push(mm);
          if (!mm.emissive || mm.emissive.getHex() === 0) mm.emissive = new THREE.Color(0x5ab8ff);
          if (/HUD|Pulse|EdgeLED/.test(mm.name)) mm.emissive.set(0x5ab8ff);
          mm.userData.base = mm.emissiveIntensity || 1;
        }
      });
    }
    if (/^XCore_/.test(o.name) && o.isMesh) hubMeshes.push(o);
    if (o.name === 'XCore_Root') hub = o;
    if (o.name === 'ScanRing_Pivot') scanPivot = o;
    if (o.name === 'XCore_Pulse') pulse = o;
    const m = o.name.match(/^XCore_EdgeLED_(\d+)/); if (m) edgeLEDs[+m[1]] = o;
  });
  // keep the scene graph mannequin-free (body is never exported, but be safe)
  ['Body', 'Eye_L', 'Eye_R', 'FitForm_Smooth'].forEach((n) => { const o = root.getObjectByName(n); if (o) o.visible = false; });
  suit.add(root);
  root.updateMatrixWorld(true);
  if (hub) {
    hub.getWorldPosition(hubPos);
    const q = new THREE.Quaternion(); hub.getWorldQuaternion(q);
    hubNormal.set(0, 0, 1).applyQuaternion(q); // refined below from geometry
    const box = new THREE.Box3(); hubMeshes.forEach((h) => box.expandByObject(h)); box.getCenter(hubPos);
    hubNormal.set(0, 0, 1);
  }
  // sway pivot for the earpiece coil
  const coil = root.getObjectByName('Wire_Ear_Coil');
  if (coil) {
    const b = new THREE.Box3().setFromObject(coil), top = new THREE.Vector3((b.min.x + b.max.x) / 2, b.max.y, (b.min.z + b.max.z) / 2);
    coilPivot = new THREE.Group(); coilPivot.position.copy(top); coil.parent.add(coilPivot); coilPivot.attach(coil);
  }
  ready = true; done();
}, (e) => {
  const p = e.total ? Math.round((e.loaded / e.total) * 100) : 0;
  $('#load .bar i').style.width = p + '%'; $('#lpct').textContent = 'Loading uniform ' + p + '%';
}, (err) => { console.error(err); $('#lpct').textContent = 'Could not load the 3D model. Scroll down for the renders.'; done(); });

let t0 = 0;
function done() { $('#load').classList.add('done'); t0 = performance.now(); }

// ---------------------------------------------------------------- scan effects
const sweep = new THREE.Mesh(new THREE.TorusGeometry(0.42, 0.006, 8, 96), new THREE.MeshBasicMaterial({ color: 0x5ab8ff, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false }));
sweep.rotation.x = Math.PI / 2; scene.add(sweep);
const sweepDisc = new THREE.Mesh(new THREE.CircleGeometry(0.42, 64), new THREE.MeshBasicMaterial({ color: 0x38bdf8, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide }));
sweepDisc.rotation.x = -Math.PI / 2; scene.add(sweepDisc);
const ringMat = new THREE.MeshBasicMaterial({ color: 0x7cc8ff, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
const scanRing = new THREE.Mesh(new THREE.RingGeometry(0.08, 0.088, 96), ringMat); scene.add(scanRing);
const beamMat = new THREE.ShaderMaterial({
  transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide, uniforms: { uO: { value: 0 }, uT: { value: 0 } },
  vertexShader: 'varying vec2 vU; void main(){ vU = uv; gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.0); }',
  fragmentShader: 'varying vec2 vU; uniform float uO; uniform float uT; void main(){ float l = 0.5+0.5*sin(vU.y*60.0 - uT*6.0); float a = (1.0-vU.y)*0.35*uO*(0.6+0.4*l); gl_FragColor = vec4(0.35,0.72,1.0,a); }',
});
const beam = new THREE.Mesh(new THREE.CylinderGeometry(0.075, 0.32, 0.55, 48, 1, true), beamMat); scene.add(beam);
let scanT = -1, holoOn = false;
function runScan() {
  scanT = 0; holoOn = true; $('#holo').classList.add('on'); sfx.scan();
}
$('#bScan').addEventListener('click', runScan);

// ---------------------------------------------------------------- sound (shares the site's mute switch)
const sfx = (() => {
  let ctx = null, hum = null;
  const on = () => { try { return localStorage.getItem('ares-sound') === 'on'; } catch (e) { return false; } };
  const ac = () => { if (!ctx) { const C = window.AudioContext || window.webkitAudioContext; if (!C) return null; ctx = new C(); } if (ctx.state === 'suspended') ctx.resume(); return ctx; };
  function tone(f, d, type = 'sine', v = 0.04, w = 0, f2) {
    const c = ac(); if (!c || !on()) return; const t = c.currentTime + w, o = c.createOscillator(), g = c.createGain();
    o.type = type; o.frequency.setValueAtTime(f, t); if (f2) o.frequency.exponentialRampToValueAtTime(f2, t + d);
    g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(v, t + 0.02); g.gain.exponentialRampToValueAtTime(0.0001, t + d);
    o.connect(g).connect(c.destination); o.start(t); o.stop(t + d + 0.05);
  }
  return {
    chirp() { tone(2100, 0.06, 'sine', 0.02); tone(2800, 0.05, 'sine', 0.014, 0.05); },
    click() { tone(240, 0.03, 'square', 0.02); tone(1500, 0.05, 'sine', 0.02, 0.02); },
    startup() { tone(110, 1.4, 'sine', 0.05, 0, 220); tone(440, 0.25, 'sine', 0.025, 0.9); tone(660, 0.3, 'sine', 0.025, 1.1); tone(880, 0.5, 'sine', 0.02, 1.3); },
    scan() { tone(300, 1.2, 'sawtooth', 0.012, 0, 1600); tone(1760, 0.12, 'sine', 0.02, 1.2); tone(2350, 0.18, 'sine', 0.018, 1.35); },
    hum(start) {
      const c = ac(); if (!c) return;
      if (start && on() && !hum) {
        const g = c.createGain(); g.gain.value = 0; g.connect(c.destination);
        const a = c.createOscillator(), b = c.createOscillator(); a.frequency.value = 55; b.frequency.value = 110.6; b.type = 'triangle';
        a.connect(g); b.connect(g); a.start(); b.start(); g.gain.linearRampToValueAtTime(0.012, c.currentTime + 2.5); hum = { g, a, b };
      } else if (!start && hum) { const h = hum; hum = null; h.g.gain.linearRampToValueAtTime(0, c.currentTime + 0.6); setTimeout(() => { h.a.stop(); h.b.stop(); }, 700); }
    },
    on,
  };
})();
let powered = false;
$('#bSound').addEventListener('click', () => {
  const b = document.getElementById('ares-snd');
  if (!sfx.on() && b) b.click(); // turns on the site's music + effects (remembers the choice)
  setTimeout(() => { sfx.startup(); sfx.hum(true); }, 120);
  $('#bSound').textContent = '● Systems online';
});
document.addEventListener('visibilitychange', () => { if (document.hidden) sfx.hum(false); });

// ---------------------------------------------------------------- scroll story
const steps = [...document.querySelectorAll('.step')];
let mode = 'hero', storyP = 0;
const io = new IntersectionObserver((es) => es.forEach((e) => { if (e.isIntersecting) e.target.classList.add('vis'); }), { threshold: 0.35 });
steps.forEach((s) => io.observe(s));
function updateScroll() {
  const vh = innerHeight; let m = 'hero', best = -1;
  steps.forEach((s) => { const r = s.getBoundingClientRect(); const c = Math.max(0, Math.min(r.bottom, vh) - Math.max(r.top, 0)) / vh; if (c > best && c > 0.35) { best = c; m = s.dataset.step; } });
  const content = document.querySelector('.content').getBoundingClientRect();
  if (content.top < vh * 0.55) m = 'content';
  if (window.scrollY < vh * 0.5) m = 'hero';
  if (m !== mode) { mode = m; sfx.chirp(); }
  const s1 = $('#s1').getBoundingClientRect();
  storyP = Math.max(0, Math.min(1, (vh - s1.top) / (s1.height + vh)));
  $('#callouts').classList.toggle('on', mode === 'callouts');
  if (mode !== 'scan' && mode !== 'callouts') { holoOn = false; $('#holo').classList.remove('on'); }
}
addEventListener('scroll', updateScroll, { passive: true });

// ---------------------------------------------------------------- pointer: parallax + hub hover/click
const mouse = new THREE.Vector2(), ndc = new THREE.Vector2(), ray = new THREE.Raycaster();
let hoverHub = false, glowBoost = 0;
addEventListener('pointermove', (e) => {
  mouse.set(e.clientX / innerWidth - 0.5, e.clientY / innerHeight - 0.5);
  ndc.set((e.clientX / innerWidth) * 2 - 1, -(e.clientY / innerHeight) * 2 + 1);
});
renderer.domElement.style.pointerEvents = 'auto';
addEventListener('click', (e) => {
  if (!ready || mode === 'content') return;
  if (e.target.closest('a,button,.panel,nav')) return;
  ray.setFromCamera(new THREE.Vector2((e.clientX / innerWidth) * 2 - 1, -(e.clientY / innerHeight) * 2 + 1), camera);
  if (ray.intersectObjects(hubMeshes, false).length) { sfx.click(); runScan(); }
});

// ---------------------------------------------------------------- camera states
const camState = { target: new THREE.Vector3(0, 0.86, 0), dist: 4.2, h: 0.25, az: 0 };
const want = { target: new THREE.Vector3(), dist: 4.2, h: 0.25 };
let autoAz = 0, suitRot = 0;
function hubWorld(out) { return out.copy(hubPos).applyMatrix4(suit.matrixWorld); }

// ---------------------------------------------------------------- callout + hologram layout
const svg = $('#callouts svg'), cos = [...document.querySelectorAll('.co')], hs = [...document.querySelectorAll('.hs')];
const v3 = new THREE.Vector3();
function toScreen(p) { v3.copy(p).project(camera); return [(v3.x * 0.5 + 0.5) * innerWidth, (-v3.y * 0.5 + 0.5) * innerHeight]; }
function layoutOverlays() {
  const [hx, hy] = toScreen(hubWorld(new THREE.Vector3()));
  const W = innerWidth, dx = Math.min(330, W * 0.3), dy = Math.min(170, innerHeight * 0.2);
  const spots = [[-dx, -dy], [dx, -dy], [-dx, dy], [dx, dy]];
  let lines = '';
  cos.forEach((el, i) => {
    const [ox, oy] = spots[i]; const x = Math.max(10, Math.min(W - el.offsetWidth - 10, hx + ox - (ox < 0 ? el.offsetWidth : 0))), y = hy + oy - el.offsetHeight / 2;
    el.style.left = x + 'px'; el.style.top = y + 'px';
    const ex = ox < 0 ? x + el.offsetWidth : x, ey = y + el.offsetHeight / 2;
    lines += `<line x1="${hx}" y1="${hy}" x2="${ex}" y2="${ey}"/><circle cx="${ex}" cy="${ey}" r="3"/>`;
  });
  svg.innerHTML = lines + `<circle cx="${hx}" cy="${hy}" r="4"/>`;
  const R = Math.min(250, W * 0.26);
  hs.forEach((el, i) => {
    const a = -Math.PI * 0.85 + i * (Math.PI * 0.55) + (i > 1 ? Math.PI * 0.6 : 0);
    el.style.left = Math.max(8, Math.min(W - el.offsetWidth - 8, hx + Math.cos(a) * R - el.offsetWidth / 2)) + 'px';
    el.style.top = (hy + Math.sin(a) * R * 0.62 - el.offsetHeight / 2) + 'px';
  });
  const hint = $('#hint'); hint.style.left = hx + 'px'; hint.style.top = hy + 'px';
  hint.style.opacity = (mode === 'scan' && !holoOn) || (hoverHub && mode !== 'content') ? 1 : 0;
}
setInterval(() => { if (!holoOn) return; $('#hr').textContent = 70 + Math.round(Math.random() * 4); $('#o2').textContent = 97 + Math.round(Math.random() * 2); $('#ct').textContent = (36.7 + Math.random() * 0.2).toFixed(1); }, 1400);

// ---------------------------------------------------------------- animation loop
const clock = new THREE.Clock();
const ss = (a, b, x) => { const t = Math.min(1, Math.max(0, (x - a) / (b - a))); return t * t * (3 - 2 * t); };
const tmp = new THREE.Vector3();

// LUNA-X color system: blue power-on -> green nominal -> cyan comms -> purple AI -> amber check -> green
const CYC = [[0x5ab8ff,0],[0x26ff5a,2.5],[0x1ae6ff,5],[0x9e4dff,7.5],[0xff9e14,10],[0x26ff5a,11.2]];
const cA = new THREE.Color(), cB = new THREE.Color(), cNow = new THREE.Color();
function cycleColor(t) {
  const u = ((t % 12) + 12) % 12; let i = 0;
  while (i < CYC.length - 1 && u >= CYC[i + 1][1]) i++;
  const [ca, ta] = CYC[i], [cb, tb] = CYC[Math.min(i + 1, CYC.length - 1)];
  const k = tb > ta ? Math.min(1, Math.max(0, (u - ta - (tb - ta) * 0.8) / ((tb - ta) * 0.2))) : 0;
  return cNow.copy(cA.set(ca)).lerp(cB.set(cb), k);
}
function setEmitColor(name, c) { (EMIT[name] || []).forEach((m) => m.emissive.copy(c)); }
function setEmit(name, v) { (EMIT[name] || []).forEach((m) => { m.emissiveIntensity = v; }); }
function tick() {
  const dt = Math.min(0.05, clock.getDelta()), T = clock.elapsedTime;
  const it = ready ? (performance.now() - t0) / 1000 : 0;      // intro time
  const skip = reduced ? 99 : it;
  // intro: stars -> moon -> suit -> power on -> title
  starMat.uniforms.uO.value = ss(0, 1.8, skip); starMat.uniforms.uT.value = T;
  moonMat.opacity = ss(1.0, 4.0, skip); moon.rotation.y = T * 0.01; sun.intensity = 2.2 * ss(1, 4, skip);
  const emerge = ss(2.4, 5.2, skip);
  lights.forEach((l) => { l.intensity = l.userData.full * emerge; });
  scene.environmentIntensity = 0.35 * emerge; floor.material.uniforms.uO.value = emerge;
  shadowCatcher.material.opacity = 0.45 * emerge;
  const power = ss(5.0, 6.6, skip);
  if (power > 0.02 && !powered) { powered = true; document.getElementById('hero').classList.add('on'); if (sfx.on()) sfx.startup(); }

  // chest module
  const hb = (T * 1.2) % 1;                         // ~72 bpm
  const beat = Math.exp(-Math.pow((hb - 0.08) / 0.035, 2)) + 0.6 * Math.exp(-Math.pow((hb - 0.24) / 0.04, 2));
  glowBoost += ((hoverHub ? 1 : 0) - glowBoost) * Math.min(1, dt * 6);
  const g = power * (1 + glowBoost * 0.8);
  const cc = power > 0.5 ? cycleColor(skip - 6.6) : cA.set(0x5ab8ff);
  ['LX_HUD','LX_ScanTick','M_PulseBlue','LX_CoreLight'].forEach((n) => setEmitColor(n, cc));
  edgeLEDs.forEach((o) => o && o.material.emissive.copy(cc)); chestLight.color.copy(cc);
  setEmit('LX_HUD', 6 * g * (0.85 + 0.15 * Math.sin(T * 3)));
  setEmit('LX_CoreLight', g * (2 + 7 * beat));
  setEmit('LX_ScanTick', 8 * g);
  setEmit('M_LED_Green', g * (0.5 + 12 * beat));
  setEmit('M_LED_Cyan', g * (2 + 5 * (0.5 + 0.5 * Math.sin(T * 2.6))));
  setEmit('M_LED_Amber', g * (1 + 5 * (Math.sin(T * 6.3) > 0.6 ? 1 : 0)));
  ['M_TabletScreen', 'M_WatchScreen'].forEach((n) => setEmit(n, 2.2 * power));
  if (scanPivot) scanPivot.rotateY(dt * 0.6 * (0.3 + power));
  const pp = ((T - 6) % 4) / 4;                    // pulse every 4 s
  if (pulse) { const s = 1 + pp * 1.4; pulse.scale.setScalar(s); setEmit('M_PulseBlue', power > 0.5 ? 14 * (1 - pp) * (1 - pp) : 0); }
  edgeLEDs.forEach((o, k) => { if (!o) return; const ph = ((T * 1.4 - k / 12) % 1 + 1) % 1; o.material.emissiveIntensity = power * (2 + 20 * Math.exp(-ph * 9)); });
  hubWorld(tmp); chestLight.position.copy(tmp).add(new THREE.Vector3(0, 0, 0.12)); chestLight.intensity = power * (0.12 + 0.12 * beat + glowBoost * 0.25);

  // idle life: breathing, posture, wire sway
  const br = Math.sin(T * 2 * Math.PI / 4.6);
  suit.scale.set(1 + 0.0022 * br, 1 + 0.001 * br, 1 + 0.004 * br);
  suit.position.y = 0.0015 * Math.sin(T * 0.7);
  if (coilPivot) { coilPivot.rotation.z = 0.06 * Math.sin(T * 1.3); coilPivot.rotation.x = 0.04 * Math.sin(T * 0.9 + 1); }

  // camera choreography
  autoAz += dt * 0.12;
  if (mode === 'hero') { want.target.set(0, 0.86, 0); want.dist = 4.3; want.h = 0.28; suitRot += (Math.sin(autoAz * 0.6) * 0.5 - suitRot) * dt; }
  else if (mode === 'rotate') { want.target.set(0, 0.86, 0); want.dist = 4.0; want.h = 0.22; suitRot += ((storyP * 4.5 * Math.PI) % (2 * Math.PI) * 0 + storyP * 2 * Math.PI * 1.0 - suitRot) * Math.min(1, dt * 3); }
  else if (mode === 'zoom' || mode === 'callouts' || mode === 'scan') {
    suitRot += ((Math.round(suitRot / (2 * Math.PI)) * 2 * Math.PI) - suitRot) * Math.min(1, dt * 2.5);
    hubWorld(want.target); want.dist = mode === 'zoom' ? 0.75 : (mode === 'callouts' ? 1.35 : 1.0); want.h = 0.03;
  } else { want.target.set(0, 0.86, 0); want.dist = 4.6; want.h = 0.3; }
  suit.rotation.y = suitRot;
  const k = Math.min(1, dt * 2.2);
  camState.target.lerp(want.target, k); camState.dist += (want.dist - camState.dist) * k; camState.h += (want.h - camState.h) * k;
  const near = mode === 'zoom' || mode === 'callouts' || mode === 'scan';
  const az = (near ? 0.12 * Math.sin(autoAz * 0.8) : 0.35 * Math.sin(autoAz)) + mouse.x * (near ? 0.18 : 0.35);
  const el = camState.h + mouse.y * -0.12;
  camera.position.set(camState.target.x + Math.sin(az) * camState.dist, camState.target.y + el * camState.dist, camState.target.z + Math.cos(az) * camState.dist);
  camera.lookAt(camState.target);

  // hover glow on the chest module
  if (ready && mode !== 'content') {
    ray.setFromCamera(ndc, camera);
    const h = ray.intersectObjects(hubMeshes, false).length > 0;
    if (h && !hoverHub) sfx.chirp();
    hoverHub = h; document.body.style.cursor = h ? 'pointer' : '';
  }

  // scan sweep + hologram
  if (scanT >= 0) {
    scanT += dt; const u = scanT / 2.4;
    const y = 1.55 - 1.6 * Math.min(1, u);
    sweep.position.set(0, y, 0); sweepDisc.position.set(0, y, 0);
    sweep.material.opacity = u < 1 ? 0.9 : Math.max(0, 0.9 - (u - 1) * 3); sweepDisc.material.opacity = sweep.material.opacity * 0.16;
    const hw = hubWorld(new THREE.Vector3());
    scanRing.position.copy(hw).add(new THREE.Vector3(0, 0, 0.03)); scanRing.lookAt(camera.position);
    const rr = ((scanT * 0.8) % 1); scanRing.scale.setScalar(1 + rr * 6); ringMat.opacity = (1 - rr) * 0.8;
    if (scanT > 9) { scanT = -1; sweep.material.opacity = 0; sweepDisc.material.opacity = 0; ringMat.opacity = 0; }
  }
  const bo = holoOn ? 1 : 0; beamMat.uniforms.uO.value += (bo - beamMat.uniforms.uO.value) * Math.min(1, dt * 3); beamMat.uniforms.uT.value = T;
  hubWorld(beam.position); beam.position.z += 0.3; beam.rotation.set(Math.PI / 2, 0, 0);

  layoutOverlays();
  composer.render();
  requestAnimationFrame(tick);
}
addEventListener('resize', () => {
  camera.aspect = innerWidth / innerHeight; camera.updateProjectionMatrix();
  renderer.setSize(innerWidth, innerHeight); composer.setSize(innerWidth, innerHeight);
});
updateScroll();
requestAnimationFrame(tick);
