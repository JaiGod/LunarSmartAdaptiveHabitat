// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · website 3D scenes (three.js)
//  1) HERO  — a real-texture Moon (NASA/LRO-based map) lit by the Sun, with Earth, an orbiting
//             spacecraft and a pulsing beacon at our habitat site on the Shackleton crater rim.
//  2) DESCENT — a scroll-driven flight from lunar orbit down to the habitat at the south pole:
//             cratered terrain, the Sun skimming the horizon (long shadows), Earth low in the sky,
//             the central hub + 4 private sleep domes whose lights show each crew member's rest cycle.
//  Falls back to the existing CSS moon / a still image if WebGL or the CDN is unavailable.
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { DRACOLoader } from 'three/addons/loaders/DRACOLoader.js';
const gltf = new GLTFLoader(); { const d = new DRACOLoader(); d.setDecoderPath('https://www.gstatic.com/draco/versioned/decoders/1.5.6/'); gltf.setDRACOLoader(d); }
const loadModel = url => new Promise(res => gltf.load(url, g => res(g.scene), undefined, e => { console.warn('model', url, e); res(null); }));

const FM = /[?&]motion=1/.test(location.search); if (FM) document.documentElement.classList.add('fm');
const RM = !FM && matchMedia('(prefers-reduced-motion: reduce)').matches;
const TOUCH = matchMedia('(pointer:coarse)').matches;
const TEX = 'https://cdn.jsdelivr.net/gh/mrdoob/three.js@r160/examples/textures/planets/';
const loader = new THREE.TextureLoader();
const load = (name, srgb = true) => new Promise(res => loader.load(TEX + name, t => { if (srgb) t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8; res(t); }, undefined, () => res(null)));

function proceduralMoon() {            // fallback texture if the CDN is blocked
  const c = document.createElement('canvas'); c.width = 1024; c.height = 512; const g = c.getContext('2d');
  g.fillStyle = '#9b9ea6'; g.fillRect(0, 0, 1024, 512);
  let s = 7; const r = () => (s = (s * 16807) % 2147483647) / 2147483647;
  for (let i = 0; i < 18; i++) { const x = r() * 1024, y = 60 + r() * 392, R = 40 + r() * 90; const gr = g.createRadialGradient(x, y, 0, x, y, R); gr.addColorStop(0, 'rgba(60,62,72,.55)'); gr.addColorStop(1, 'rgba(60,62,72,0)'); g.fillStyle = gr; g.fillRect(x - R, y - R, 2 * R, 2 * R); }
  for (let i = 0; i < 900; i++) { const x = r() * 1024, y = r() * 512, R = 1 + Math.pow(r(), 3) * 16; g.fillStyle = 'rgba(50,52,60,.4)'; g.beginPath(); g.arc(x, y, R, 0, 7); g.fill(); g.fillStyle = 'rgba(230,232,240,.3)'; g.beginPath(); g.arc(x - R * .3, y - R * .3, R * .7, 0, 7); g.fill(); }
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}

function stars(n, radius, size) {
  const g = new THREE.BufferGeometry(), p = new Float32Array(n * 3), col = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) {
    const u = Math.random() * 2 - 1, th = Math.random() * Math.PI * 2, s = Math.sqrt(1 - u * u);
    p.set([radius * s * Math.cos(th), radius * u, radius * s * Math.sin(th)], i * 3);
    const k = .55 + Math.random() * .45, warm = Math.random() < .15, cool = Math.random() < .2;
    col.set([k * (warm ? 1 : cool ? .8 : .95), k * (warm ? .85 : .9), k * (warm ? .7 : 1)], i * 3);
  }
  g.setAttribute('position', new THREE.BufferAttribute(p, 3)); g.setAttribute('color', new THREE.BufferAttribute(col, 3));
  return new THREE.Points(g, new THREE.PointsMaterial({ size, vertexColors: true, sizeAttenuation: false, transparent: true, depthWrite: false }));
}

function atmosphere(radius, color, power = 3.5, alpha = 1) {   // fresnel glow shell
  return new THREE.Mesh(new THREE.SphereGeometry(radius, 64, 64), new THREE.ShaderMaterial({
    uniforms: { c: { value: new THREE.Color(color) }, p: { value: power }, a: { value: alpha } },
    vertexShader: 'varying vec3 n;varying vec3 v;void main(){n=normalize(normalMatrix*normal);vec4 mv=modelViewMatrix*vec4(position,1.);v=normalize(-mv.xyz);gl_Position=projectionMatrix*mv;}',
    fragmentShader: 'uniform vec3 c;uniform float p;uniform float a;varying vec3 n;varying vec3 v;void main(){float f=pow(1.-max(dot(n,v),0.),p);gl_FragColor=vec4(c,f*a);}',
    blending: THREE.AdditiveBlending, transparent: true, depthWrite: false, side: THREE.FrontSide
  }));
}

function makeRenderer(canvas) {
  const dpr = Math.min(devicePixelRatio || 1, TOUCH ? 1 : 1.25);
  const r = new THREE.WebGLRenderer({ canvas, antialias: dpr < 1.5, alpha: true, powerPreference: 'high-performance' });
  r.setPixelRatio(dpr); r.userData = { dpr, max: dpr };
  r.outputColorSpace = THREE.SRGBColorSpace; r.toneMapping = THREE.ACESFilmicToneMapping; r.toneMappingExposure = 1.05;
  return r;
}

// Adaptive quality: if frames are slow, render fewer pixels (down to 0.6x); speed back up when there's headroom.
function tune(R, ms) {
  if (!R) return; const u = R.userData; u.avg = u.avg == null ? ms : u.avg * .9 + ms * .1; u.n = (u.n || 0) + 1;
  if (u.n < 20) return; u.n = 0;
  if (u.avg > 24 && u.dpr > .6) { u.dpr = Math.max(.6, u.dpr - .15); R.setPixelRatio(u.dpr); }
  else if (u.avg < 13 && u.dpr < u.max) { u.dpr = Math.min(u.max, u.dpr + .1); R.setPixelRatio(u.dpr); }
}
let HEAVY = 0; const busy = () => HEAVY > 0;
function loop(el, frame, R, heavy) {       // render only while visible
  let on = false, raf = 0, last = performance.now();
  const setOn = v => { if (heavy && v !== on) HEAVY += v ? 1 : -1; on = v; };
  const tick = n => { const ms = n - last, dt = Math.min(.05, ms / 1000); last = n; frame(dt, n / 1000); tune(R, ms); if (on && !RM) raf = requestAnimationFrame(tick); };
  new IntersectionObserver(es => { const v = es[0].isIntersecting; if (v && !on) { setOn(true); last = performance.now(); raf = requestAnimationFrame(tick); } else if (!v && on) { setOn(false); cancelAnimationFrame(raf); } }, { rootMargin: '100px' }).observe(el);
  document.addEventListener('visibilitychange', () => { if (document.hidden) { setOn(false); cancelAnimationFrame(raf); } else if (el.getBoundingClientRect().bottom > 0 && el.getBoundingClientRect().top < innerHeight) { setOn(true); last = performance.now(); raf = requestAnimationFrame(tick); } });
  return () => frame(0, performance.now() / 1000);
}

// ------------------------------------------------------------------------------------------------ HERO MOON
async function hero() {
  const box = document.getElementById('orbit'); if (!box) return;
  const canvas = document.createElement('canvas'); canvas.className = 'gl'; canvas.setAttribute('aria-hidden', 'true'); box.prepend(canvas);
  const R = makeRenderer(canvas), scene = new THREE.Scene(), cam = new THREE.PerspectiveCamera(32, 1, .1, 200);
  cam.position.set(0, .35, 7.2);
  const [moonMap, earthMap, earthSpec, clouds] = await Promise.all([load('moon_1024.jpg'), load('earth_atmos_2048.jpg'), load('earth_specular_2048.jpg', false), load('earth_clouds_1024.png')]);
  const mtex = moonMap || proceduralMoon();

  const sun = new THREE.DirectionalLight(0xfff4e6, 3.2); sun.position.set(-6, .9, .6); scene.add(sun);
  scene.add(new THREE.HemisphereLight(0x6f8fd0, 0x000000, .12));               // a whisper of earthshine

  const moonG = new THREE.Group(); scene.add(moonG);
  const moon = new THREE.Mesh(new THREE.SphereGeometry(1.55, 128, 128), new THREE.MeshStandardMaterial({ map: mtex, bumpMap: mtex, bumpScale: 2.2, roughness: 1, metalness: 0 }));
  moonG.add(moon); moonG.rotation.set(-.62, 0, .1);                                  // tilt so the south pole faces us
  moonG.add(atmosphere(1.6, 0x9cc8ff, 5, .35));

  // habitat beacon on the south-pole rim (lat -88.5°)
  const lat = -88.5 * Math.PI / 180, lon = .6, rr = 1.565;
  const bpos = new THREE.Vector3(rr * Math.cos(lat) * Math.cos(lon), rr * Math.sin(lat), rr * Math.cos(lat) * Math.sin(lon));
  const sprite = (col, s) => { const c = document.createElement('canvas'); c.width = c.height = 64; const g = c.getContext('2d'); const gr = g.createRadialGradient(32, 32, 0, 32, 32, 32); gr.addColorStop(0, col); gr.addColorStop(.25, col); gr.addColorStop(1, 'rgba(0,0,0,0)'); g.fillStyle = gr; g.fillRect(0, 0, 64, 64); const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(c), blending: THREE.AdditiveBlending, depthWrite: false, transparent: true })); sp.scale.setScalar(s); return sp; };
  const beacon = sprite('rgba(252,61,33,1)', .16); beacon.position.copy(bpos); moon.add(beacon);
  const ring = sprite('rgba(252,61,33,.6)', .3); ring.position.copy(bpos); moon.add(ring);

  // Earth
  const earth = new THREE.Mesh(new THREE.SphereGeometry(.42, 96, 96), new THREE.MeshPhongMaterial({ map: earthMap || null, color: earthMap ? 0xffffff : 0x3a74c9, specularMap: earthSpec || null, specular: 0x333333, shininess: 14 }));
  earth.position.set(2.05, 1.3, -2.4); earth.rotation.z = .41; scene.add(earth);
  if (clouds) { const cl = new THREE.Mesh(new THREE.SphereGeometry(.428, 96, 96), new THREE.MeshPhongMaterial({ map: clouds, transparent: true, depthWrite: false })); earth.add(cl); earth.userData.cl = cl; }
  earth.add(atmosphere(.47, 0x4aa8ff, 3, .9));

  // spacecraft (Gateway-like) in an inclined orbit
  const ship = new THREE.Group();
  const hull = new THREE.MeshStandardMaterial({ color: 0xdfe6f2, metalness: .4, roughness: .45 });
  const panel = new THREE.MeshStandardMaterial({ color: 0x1b3d8f, metalness: .6, roughness: .3, emissive: 0x0b1a40, emissiveIntensity: .4 });
  const body = new THREE.Mesh(new THREE.CylinderGeometry(.035, .035, .2, 16), hull); body.rotation.z = Math.PI / 2; ship.add(body);
  const pod = new THREE.Mesh(new THREE.SphereGeometry(.045, 16, 16), hull); pod.position.x = .11; ship.add(pod);
  for (const s of [-1, 1]) { const w = new THREE.Mesh(new THREE.BoxGeometry(.05, .004, .22), panel); w.position.set(-.04, 0, s * .15); ship.add(w); }
  const orbitG = new THREE.Group(); orbitG.rotation.set(.35, 0, -.32); orbitG.add(ship); scene.add(orbitG);

  scene.add(stars(1500, 80, 1.2));

  // drag to spin
  let drag = false, px = 0, vel = 0, spin = 0;
  canvas.addEventListener('pointerdown', e => { drag = true; px = e.clientX; canvas.setPointerCapture(e.pointerId); });
  canvas.addEventListener('pointermove', e => { if (!drag) return; vel = (e.clientX - px) * .006; px = e.clientX; spin += vel; });
  canvas.addEventListener('pointerup', () => drag = false);
  canvas.style.touchAction = 'pan-y';

  const size = () => { const w = box.clientWidth, h = box.clientHeight || w; R.setSize(w, h, false); cam.aspect = w / h; cam.updateProjectionMatrix(); };
  size(); new ResizeObserver(size).observe(box);
  let ang = 0;
  const once = loop(box, (dt, t) => {
    if (!drag) { vel *= .95; spin += vel + dt * .06; }
    moon.rotation.y = spin;
    if (earth.userData.cl) earth.userData.cl.rotation.y += dt * .02; earth.rotation.y += dt * .05;
    ang += dt * .35; ship.position.set(Math.cos(ang) * 2.25, 0, Math.sin(ang) * 2.25); ship.rotation.y = -ang;
    const p = .5 + .5 * Math.sin(t * 3); beacon.material.opacity = .6 + .4 * p; ring.scale.setScalar(.2 + .25 * ((t * .8) % 1)); ring.material.opacity = 1 - ((t * .8) % 1);
    R.render(scene, cam);
  }, R);
  once(); box.classList.add('gl-on');
}

// ------------------------------------------------------------------------------------------------ DESCENT
function hash(x, y) { const s = Math.sin(x * 127.1 + y * 311.7) * 43758.5453; return s - Math.floor(s); }
function vnoise(x, y) { const ix = Math.floor(x), iy = Math.floor(y), fx = x - ix, fy = y - iy, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
  const a = hash(ix, iy), b = hash(ix + 1, iy), c = hash(ix, iy + 1), d = hash(ix + 1, iy + 1); return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v; }
function fbm(x, y) { let s = 0, a = .5, f = 1; for (let i = 0; i < 5; i++) { s += a * vnoise(x * f, y * f); f *= 2.03; a *= .5; } return s; }

async function descent() {
  const sec = document.getElementById('descent'); if (!sec) return;
  const canvas = sec.querySelector('canvas'); const R = makeRenderer(canvas);
  R.shadowMap.enabled = !TOUCH; R.shadowMap.type = THREE.PCFShadowMap; R.shadowMap.autoUpdate = false; R.userData.shadowDirty = true;
  const scene = new THREE.Scene(); scene.background = new THREE.Color(0x000000);
  const cam = new THREE.PerspectiveCamera(50, 1, .5, 2000);
  const [moonMap, earthMap] = await Promise.all([load('moon_1024.jpg'), load('earth_atmos_2048.jpg')]);
  const detail = (moonMap || proceduralMoon()).clone(); detail.wrapS = detail.wrapT = THREE.RepeatWrapping; detail.repeat.set(10, 10); detail.needsUpdate = true;

  // terrain: Shackleton-like rim + scattered craters + fbm roughness, flattened pad at the habitat
  const craters = [{ x: 0, z: 95, r: 85, d: 26 }];
  let s = 3; const rnd = () => (s = (s * 16807) % 2147483647) / 2147483647;
  for (let i = 0; i < 70; i++) craters.push({ x: (rnd() - .5) * 520, z: (rnd() - .5) * 520, r: 3 + Math.pow(rnd(), 2.2) * 34, d: 0 });
  craters.forEach(c => { if (!c.d) c.d = c.r * (.18 + rnd() * .12); });
  const H = (x, z) => {
    let h = (fbm(x * .015, z * .015) - .5) * 10 + (fbm(x * .08 + 9, z * .08) - .5) * 1.6;
    for (const c of craters) { const dd = Math.hypot(x - c.x, z - c.z) / c.r; if (dd < 1) h -= c.d * (1 - dd * dd); h += c.d * .32 * Math.exp(-(((dd - 1) / .16) ** 2)); }
    const pad = Math.hypot(x, z); const k = Math.min(1, Math.max(0, (pad - 58) / 50)); const ks = k * k * (3 - 2 * k);
    return h * ks + (1 - ks) * H0;
  };
  let H0 = 0; H0 = (() => { let a = 0; for (const c of craters) { const dd = Math.hypot(c.x, c.z) / c.r; if (dd < 1) a -= c.d * (1 - dd * dd); a += c.d * .32 * Math.exp(-(((dd - 1) / .16) ** 2)); } return a + (fbm(0, 0) - .5) * 10; })();
  const N = TOUCH ? 170 : 300, SZ = 600;
  const tg = new THREE.PlaneGeometry(SZ, SZ, N, N); tg.rotateX(-Math.PI / 2);
  const pos = tg.attributes.position; const col = new Float32Array(pos.count * 3);
  for (let i = 0; i < pos.count; i++) { const x = pos.getX(i), z = pos.getZ(i), h = H(x, z); pos.setY(i, h); const g = .62 + (fbm(x * .03 + 4, z * .03) - .5) * .25; col.set([g, g, g * 1.02], i * 3); }
  tg.setAttribute('color', new THREE.BufferAttribute(col, 3)); tg.computeVertexNormals();
  const terrain = new THREE.Mesh(tg, new THREE.MeshStandardMaterial({ map: detail, vertexColors: true, roughness: 1, metalness: 0 }));
  terrain.receiveShadow = true; terrain.castShadow = !TOUCH; scene.add(terrain);

  // Sun: at the south pole it skims the horizon — long shadows
  const sun = new THREE.DirectionalLight(0xfff1dc, 4.2); sun.position.set(-220, 26, 60); scene.add(sun); scene.add(sun.target);
  sun.castShadow = !TOUCH; sun.shadow.mapSize.set(1024, 1024); Object.assign(sun.shadow.camera, { left: -95, right: 95, top: 95, bottom: -95, near: 1, far: 600 }); sun.shadow.bias = -.0018; sun.shadow.normalBias = .9;
  scene.add(new THREE.HemisphereLight(0x5a78b8, 0x111111, .18));

  // Earth low on the horizon (true at the south pole)
  const earth = new THREE.Mesh(new THREE.SphereGeometry(26, 64, 64), new THREE.MeshPhongMaterial({ map: earthMap || null, color: earthMap ? 0xffffff : 0x3a74c9, shininess: 10 }));
  earth.position.set(380, 70, -900); earth.rotation.z = .4; scene.add(earth); earth.add(atmosphere(29, 0x4aa8ff, 3, .9));
  const earthLight = new THREE.DirectionalLight(0xbfd6ff, .25); earthLight.position.copy(earth.position); scene.add(earthLight);
  const sky = stars(2600, 1500, 1.1); scene.add(sky);

  // ---- habitat: central hub + 4 sleep domes on hallways + airlock, solar towers, comm mast, landing pad
  const hab = new THREE.Group(); hab.position.y = H0; scene.add(hab);
  const white = new THREE.MeshStandardMaterial({ color: 0xe9edf3, roughness: .55, metalness: .15 });
  const shell = new THREE.MeshStandardMaterial({ color: 0xcfc6b8, roughness: .95, metalness: 0 });   // regolith-shielded dome
  const gold = new THREE.MeshStandardMaterial({ color: 0xd4a548, roughness: .35, metalness: .9 });
  const dark = new THREE.MeshStandardMaterial({ color: 0x1b2433, roughness: .5, metalness: .5 });
  const cs = o => { o.traverse(m => { if (m.isMesh) { m.castShadow = !TOUCH; m.receiveShadow = true; } }); return o; };
  const dome = (r, h, mat, glow) => {
    const g = new THREE.Group();
    const base = new THREE.Mesh(new THREE.CylinderGeometry(r, r * 1.04, h, 48), white); base.position.y = h / 2; g.add(base);
    const top = new THREE.Mesh(new THREE.SphereGeometry(r, 48, 24, 0, Math.PI * 2, 0, Math.PI / 2), mat); top.position.y = h; g.add(top);
    const band = new THREE.Mesh(new THREE.TorusGeometry(r * 1.01, .09, 8, 64), gold); band.rotation.x = Math.PI / 2; band.position.y = h; g.add(band);
    const win = new THREE.Mesh(new THREE.TorusGeometry(r * 1.005, .16, 8, 64), new THREE.MeshStandardMaterial({ color: 0x000000, emissive: glow, emissiveIntensity: 2 })); win.rotation.x = Math.PI / 2; win.position.y = h * .62; g.add(win);
    const beacon = new THREE.Mesh(new THREE.SphereGeometry(.28, 16, 16), new THREE.MeshStandardMaterial({ color: 0x000000, emissive: glow, emissiveIntensity: 3 })); beacon.position.y = h + r + .15; g.add(beacon);
    const pl = new THREE.PointLight(glow, 8, r * 4, 2); pl.position.y = h * .5; g.add(pl);
    g.userData = { win, beacon, pl }; return cs(g);
  };
  const habPrim = new THREE.Group(); hab.add(habPrim);           // stand-in shapes, replaced by the real model once it loads
  const hub = dome(7, 3.2, shell, 0xffe2b0); habPrim.add(hub);
  const sleepers = []; const moods = [0x4f8cff, 0x4f8cff, 0xffb547, 0x4f8cff];   // asleep (blue breathing) · resting (amber)
  [45, 135, 225, 315].forEach((deg, i) => {
    const a = deg * Math.PI / 180, d = 17, x = Math.cos(a) * d, z = Math.sin(a) * d;
    const sd = dome(3.4, 2.2, shell, moods[i]); sd.position.set(x, 0, z); habPrim.add(sd); sleepers.push(sd);
    const len = d - 7 - 3.4 + .6, mid = 7 + len / 2 - .3;
    const tube = cs(new THREE.Mesh(new THREE.CylinderGeometry(1.15, 1.15, len, 24), white)); tube.rotation.z = Math.PI / 2; tube.rotation.y = -a; tube.position.set(Math.cos(a) * mid, 1.3, Math.sin(a) * mid); habPrim.add(tube);
    for (const t of [.2, .8]) { const rib = new THREE.Mesh(new THREE.TorusGeometry(1.2, .07, 6, 24), gold); rib.rotation.y = Math.PI / 2 - a; const m = 7 + len * t - .3; rib.position.set(Math.cos(a) * m, 1.3, Math.sin(a) * m); habPrim.add(rib); }
  });
  const air = cs(new THREE.Mesh(new THREE.BoxGeometry(4, 2.6, 3), white)); air.position.set(-8.5, 1.3, 0); habPrim.add(air);
  const hatch = new THREE.Mesh(new THREE.CircleGeometry(.8, 24), dark); hatch.rotation.y = -Math.PI / 2; hatch.position.set(-10.52, 1.3, 0); habPrim.add(hatch);
  const towers = [];
  for (const [x, z] of [[-64, -46], [-74, 8], [-58, 52]]) {
    const t = new THREE.Group(); t.position.set(x, H(x, z) - H0, z);
    const mast = new THREE.Mesh(new THREE.CylinderGeometry(.25, .35, 18, 12), white); mast.position.y = 9; t.add(mast);
    const arr = new THREE.Group(); arr.position.y = 11;
    const pnl = new THREE.Mesh(new THREE.BoxGeometry(.12, 12, 5.5), new THREE.MeshStandardMaterial({ color: 0x14306f, metalness: .7, roughness: .25, emissive: 0x050c22 })); arr.add(pnl);
    const frame = new THREE.Mesh(new THREE.BoxGeometry(.18, 12.4, .2), white); frame.position.z = 2.8; arr.add(frame);
    t.add(arr); t.userData.arr = arr; hab.add(cs(t)); towers.push(t);
  }
  const mastG = new THREE.Group(); mastG.position.set(52, H(52, -56) - H0, -56);
  const cm = new THREE.Mesh(new THREE.CylinderGeometry(.18, .3, 20, 10), white); cm.position.y = 10; mastG.add(cm);
  const dish = new THREE.Mesh(new THREE.SphereGeometry(2, 24, 12, 0, Math.PI * 2, 0, Math.PI / 3.2), new THREE.MeshStandardMaterial({ color: 0xf2f4f8, roughness: .4, side: THREE.DoubleSide })); dish.position.y = 19.5; dish.rotation.set(Math.PI / 2.3, 0, -.6); mastG.add(dish);
  const blink = new THREE.Mesh(new THREE.SphereGeometry(.25, 10, 10), new THREE.MeshBasicMaterial({ color: 0xff3020 })); blink.position.y = 20.4; mastG.add(blink);
  hab.add(cs(mastG));
  const padX = 120, padZ = 70; const pad = new THREE.Mesh(new THREE.RingGeometry(6, 9, 64), new THREE.MeshStandardMaterial({ color: 0x8a8d94, roughness: 1 })); pad.rotation.x = -Math.PI / 2; pad.position.set(padX, H(padX, padZ) - H0 + .15, padZ); hab.add(pad);
  const padLights = []; for (let i = 0; i < 12; i++) { const a = i / 12 * Math.PI * 2; const l = new THREE.Mesh(new THREE.SphereGeometry(.25, 8, 8), new THREE.MeshBasicMaterial({ color: 0x38bdf8 })); l.position.set(padX + Math.cos(a) * 9.4, pad.position.y + .2, padZ + Math.sin(a) * 9.4); hab.add(l); padLights.push(l); }
  // a lander parked on the pad
  const lander = new THREE.Group(); const lb = new THREE.Mesh(new THREE.CylinderGeometry(1.8, 2.4, 3.2, 8), gold); lb.position.y = 2.6; lander.add(lb);
  const lt = new THREE.Mesh(new THREE.CylinderGeometry(1.3, 1.6, 2.4, 8), white); lt.position.y = 5.2; lander.add(lt);
  for (let i = 0; i < 4; i++) { const a = i * Math.PI / 2 + .4; const leg = new THREE.Mesh(new THREE.CylinderGeometry(.08, .08, 3.4, 6), dark); leg.position.set(Math.cos(a) * 2.6, 1.1, Math.sin(a) * 2.6); leg.rotation.set(Math.sin(a) * .5, 0, -Math.cos(a) * .5); lander.add(leg); }
  lander.position.set(padX, pad.position.y, padZ); hab.add(cs(lander));

  // dust motes catching the low sun
  const dN = TOUCH ? 300 : 700, dg = new THREE.BufferGeometry(), dp = new Float32Array(dN * 3);
  for (let i = 0; i < dN; i++) dp.set([(Math.random() - .5) * 200, H0 + Math.random() * 16, (Math.random() - .5) * 200], i * 3);
  dg.setAttribute('position', new THREE.BufferAttribute(dp, 3));
  const dust = new THREE.Points(dg, new THREE.PointsMaterial({ color: 0xfff1dc, size: .12, transparent: true, opacity: .45, depthWrite: false })); scene.add(dust);

  // ---- the REAL habitat: web copy of the realistic game's Blender model (Moon Base Final)
  const glowers = [];
  loadModel('models/habitat.glb').then(m => {
    if (!m) return;
    const box = new THREE.Box3().setFromObject(m), c = box.getCenter(new THREE.Vector3());
    m.position.set(-c.x, H0 - box.min.y, -c.z);
    m.traverse(o => { if (o.isMesh) { o.castShadow = !TOUCH; o.receiveShadow = true; if (o.material && o.material.map) o.material.map.anisotropy = 8; } });
    hab.add(m); habPrim.visible = false; sleepers.length = 0; R.userData.shadowDirty = true;
    m.updateMatrixWorld(true);
    const glowAt = (name, col) => {
      let p, top, bot; const T = m.getObjectByName('GLOWTOP_' + name), B = m.getObjectByName('GLOWBASE_' + name);
      if (T && B) { p = T.getWorldPosition(new THREE.Vector3()); top = p.y; bot = B.getWorldPosition(new THREE.Vector3()).y; }
      else { const o = m.getObjectByName(name); if (!o) return; const bb = new THREE.Box3().setFromObject(o); p = bb.getCenter(new THREE.Vector3()); top = bb.max.y; bot = bb.min.y; }
      const b = { max: { y: top }, min: { y: bot } };
      const beacon = new THREE.Mesh(new THREE.SphereGeometry(.35, 16, 16), new THREE.MeshStandardMaterial({ color: 0x000000, emissive: col, emissiveIntensity: 3 })); beacon.position.set(p.x, b.max.y + .5, p.z);
      const pl = new THREE.PointLight(col, 10, 16, 2); pl.position.set(p.x, b.min.y + 1.2, p.z);
      scene.add(beacon, pl); glowers.push({ beacon, pl }); };
    ['SQ1', 'SQ2', 'SQ3', 'SQ4'].forEach((s, i) => glowAt(s + '_10_Node_RigidHull', moods[i]));
    glowAt('CORE_C01_LowerDeck_RigidHull_Aluminum', 0xffe2b0);
    if (RM) once();
  });

  // ---- scroll-driven camera path
  const P = [[40, 420, 380], [140, 170, 230], [150, 95, 150], [95, H0 + 38, 85], [72, H0 + 20, 92], [-52, H0 + 11, 100]].map(a => new THREE.Vector3(...a));
  const L = [[0, H0 - 20, 60], [0, H0, 20], [0, H0, 0], [0, H0 + 2, 0], [0, H0 + 3, 0], [0, H0 + 5, -6]].map(a => new THREE.Vector3(...a));
  const path = new THREE.CatmullRomCurve3(P, false, 'centripetal'), look = new THREE.CatmullRomCurve3(L, false, 'centripetal');
  const caps = [...sec.querySelectorAll('.cap')], bar = sec.querySelector('.dprog i');
  let prog = 0, tprog = 0;
  const measure = () => { const r = sec.getBoundingClientRect(), total = sec.offsetHeight - innerHeight; tprog = Math.min(1, Math.max(0, -r.top / Math.max(1, total))); };
  addEventListener('scroll', measure, { passive: true }); measure();
  if (FM) window.__descent = p => { removeEventListener('scroll', measure); tprog = prog = p; };

  const stage = sec.querySelector('.stage');
  const size = () => { const w = stage.clientWidth, h = stage.clientHeight; R.setSize(w, h, false); cam.aspect = w / h; cam.fov = w < h ? 64 : 50; cam.updateProjectionMatrix(); };
  size(); addEventListener('resize', size);
  const tmp = new THREE.Vector3();
  const once = loop(stage, (dt, t) => {
    prog += (tprog - prog) * (RM ? 1 : Math.min(1, dt * 4));
    const e = prog < .5 ? 4 * prog ** 3 : 1 - (-2 * prog + 2) ** 3 / 2;
    cam.position.copy(path.getPointAt(e)); cam.lookAt(look.getPointAt(e, tmp));
    caps.forEach((c, i) => c.classList.toggle('on', prog >= i / caps.length - .02 && prog < (i + 1) / caps.length + (i === caps.length - 1 ? 1 : 0)));
    if (bar) bar.style.width = (prog * 100).toFixed(1) + '%';
    // dome lights: asleep = slow blue "breathing" (6/min), resting = soft amber pulse, hub = steady warm white
    sleepers.forEach((d, i) => { const k = i === 2 ? .55 + .45 * Math.sin(t * 2.2) : .35 + .65 * (.5 + .5 * Math.sin(t * Math.PI * 2 / 10 + i)); d.userData.win.material.emissiveIntensity = 2.6 * k; d.userData.beacon.material.emissiveIntensity = 3.5 * k; d.userData.pl.intensity = 9 * k; });
    glowers.forEach((g, i) => { const k = i === 4 ? 1 : i === 2 ? .55 + .45 * Math.sin(t * 2.2) : .35 + .65 * (.5 + .5 * Math.sin(t * Math.PI * 2 / 10 + i)); g.beacon.material.emissiveIntensity = 3.5 * k; g.pl.intensity = 10 * k; });
    towers.forEach(tw => tw.userData.arr.rotation.y = Math.sin(t * .05) * .3);
    blink.visible = (t % 1.6) < .2; padLights.forEach((l, i) => l.visible = ((t * 6 + i) % 12) > 1.2);
    const a = dust.geometry.attributes.position; for (let i = 0; i < a.count; i++) { let y = a.getY(i) + dt * .25; if (y > H0 + 14) y = H0; a.setY(i, y); } a.needsUpdate = true;
    earth.rotation.y += dt * .02; sky.rotation.y += dt * .002;
    if (R.userData.shadowDirty) { R.shadowMap.needsUpdate = true; R.userData.shadowDirty = false; }
    R.render(scene, cam);
  }, R, true);
  addEventListener('scroll', () => { if (RM) once(); }, { passive: true });
  once(); sec.classList.add('gl-on');
}

try { hero(); } catch (e) { console.warn('hero 3D', e); }
try { descent(); } catch (e) { console.warn('descent 3D', e); }

// ------------------------------------------------------------------------------------------------ NEBULA (page background)
// An original, slowly drifting emission nebula (red hydrogen glow, blue reflection light, dark dust lanes) drawn by a shader.
function nebula() {
  const cv = document.getElementById('neb'); if (!cv) return;
  const R = new THREE.WebGLRenderer({ canvas: cv, antialias: false, alpha: true, powerPreference: 'low-power' });
  const scale = TOUCH ? .22 : .3; R.setPixelRatio(scale);
  const scene = new THREE.Scene(), cam = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);
  const u = { t: { value: 0 }, res: { value: new THREE.Vector2(1, 1) }, scroll: { value: 0 } };
  scene.add(new THREE.Mesh(new THREE.PlaneGeometry(2, 2), new THREE.ShaderMaterial({ uniforms: u, depthWrite: false, transparent: true,
    vertexShader: 'varying vec2 v;void main(){v=uv;gl_Position=vec4(position,1.);}',
    fragmentShader: `precision highp float;varying vec2 v;uniform float t;uniform vec2 res;uniform float scroll;
      float h(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
      float n(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(h(i),h(i+vec2(1,0)),f.x),mix(h(i+vec2(0,1)),h(i+vec2(1,1)),f.x),f.y);}
      float fbm(vec2 p){float s=0.,a=.5;for(int i=0;i<6;i++){s+=a*n(p);p=p*2.03+vec2(1.7,9.2);a*=.5;}return s;}
      void main(){vec2 p=(v-.5)*vec2(res.x/res.y,1.)*2.6;p.y+=scroll*.6;float tt=t*.012;
        vec2 q=vec2(fbm(p+tt),fbm(p+vec2(5.2,1.3)-tt));vec2 r=vec2(fbm(p+3.*q+vec2(1.7,9.2)+tt*.7),fbm(p+3.*q+vec2(8.3,2.8)-tt*.5));
        float f=fbm(p+2.5*r);
        float red=smoothstep(.45,.95,f)*(.6+.4*q.x), blue=smoothstep(.55,1.,fbm(p*1.4+r*1.6-tt))*(.5+.5*r.y);
        float dust=smoothstep(.55,.8,fbm(p*2.2+q*3.+vec2(3.,7.)));
        vec3 c=vec3(.95,.16,.24)*red*1.1+vec3(.35,.55,1.)*blue*.95+vec3(.45,.2,.75)*red*blue*.8;
        c*=1.-dust*.85; float vig=smoothstep(1.6,.2,length((v-.5)*vec2(1.6,1.)));
        gl_FragColor=vec4(c*vig,clamp(max(red,blue)*vig*.9,0.,1.));}`
  })));
  const size = () => { R.setSize(innerWidth, innerHeight, false); u.res.value.set(innerWidth, innerHeight); }; size(); addEventListener('resize', size);
  addEventListener('scroll', () => u.scroll.value = scrollY / Math.max(1, document.body.scrollHeight), { passive: true });
  let last = 0; const tick = n => { if (!document.hidden && n - last > 90 && !busy()) { last = n; u.t.value = n / 1000; R.render(scene, cam); } if (!RM) requestAnimationFrame(tick); };
  requestAnimationFrame(tick); u.t.value = 20; R.render(scene, cam); cv.classList.add('on');
}

// ------------------------------------------------------------------------------------------------ SPACEWALK (EVA)
// An astronaut (NASA's free 3D astronaut model) drifting on a tether above Earth's glowing limb, with the Sun's flare.
async function eva() {
  const sec = document.getElementById('eva'); if (!sec) return;
  const stage = sec.querySelector('.stage'), canvas = stage.querySelector('canvas.gl3'), fl = stage.querySelector('canvas.flare');
  const R = makeRenderer(canvas); R.toneMappingExposure = 1.15;
  const scene = new THREE.Scene(), cam = new THREE.PerspectiveCamera(42, 1, .1, 3000);
  cam.position.set(0, 0, 0);
  const [earthMap, clouds, spec, astro] = await Promise.all([load('earth_atmos_2048.jpg'), load('earth_clouds_1024.png'), load('earth_specular_2048.jpg', false), loadModel('models/astronaut.glb')]);
  const sunDir = new THREE.Vector3(-.2, .27, -.94).normalize();
  const sun = new THREE.DirectionalLight(0xffffff, 3.4); sun.position.copy(sunDir).multiplyScalar(50); scene.add(sun);
  scene.add(new THREE.HemisphereLight(0x8fb6ff, 0x1d3a6e, .9));                           // blue light bouncing off Earth
  const fill = new THREE.DirectionalLight(0xfff3e4, 1.6); fill.position.set(3, 2, 6); scene.add(fill);   // soft key light on the suit
  // Earth: a huge sphere below us so only the curved limb shows
  const earth = new THREE.Group(); earth.position.set(0, -132, -70); scene.add(earth);
  const eMesh = new THREE.Mesh(new THREE.SphereGeometry(120, 160, 160), new THREE.MeshPhongMaterial({ map: earthMap || null, color: earthMap ? 0xffffff : 0x2c64b8, specularMap: spec || null, specular: 0x334455, shininess: 16 }));
  earth.add(eMesh); eMesh.rotation.set(.3, 2.2, .15);
  let cl = null; if (clouds) { cl = new THREE.Mesh(new THREE.SphereGeometry(120.5, 160, 160), new THREE.MeshPhongMaterial({ map: clouds, transparent: true, depthWrite: false, opacity: .95 })); cl.rotation.copy(eMesh.rotation); earth.add(cl); }
  earth.add(atmosphere(123.5, 0x58a8ff, 2.4, 1.1)); earth.add(atmosphere(121, 0x9fd0ff, 6, .6));
  scene.add(stars(1800, 1500, 1.3));
  // truss (station arm) coming in from the lower right
  const metal = new THREE.MeshStandardMaterial({ color: 0xb8bcc4, metalness: .85, roughness: .38 }), darkm = new THREE.MeshStandardMaterial({ color: 0x3b3f48, metalness: .7, roughness: .5 });
  const truss = new THREE.Group(); truss.position.set(4.6, -2.9, -7.5); truss.rotation.set(.2, -.9, .32); scene.add(truss);
  const beam = new THREE.Mesh(new THREE.CylinderGeometry(.32, .32, 9, 20), metal); beam.rotation.z = Math.PI / 2; truss.add(beam);
  for (let i = -4; i <= 4; i++) { const r = new THREE.Mesh(new THREE.TorusGeometry(.36, .05, 8, 20), darkm); r.rotation.y = Math.PI / 2; r.position.x = i; truss.add(r); }
  const joint = new THREE.Mesh(new THREE.BoxGeometry(1.2, 1, 1), metal); joint.position.x = -4.6; truss.add(joint);
  // astronaut
  const astroG = new THREE.Group(); scene.add(astroG); astroG.position.set(1.6, .2, -7.2);
  if (astro) {
    const b = new THREE.Box3().setFromObject(astro), s = b.getSize(new THREE.Vector3()), c = b.getCenter(new THREE.Vector3());
    const k = 1.45 / Math.max(s.x, s.y, s.z); astro.scale.setScalar(k); astro.position.copy(c).multiplyScalar(-k);
    astro.traverse(o => { if (o.isMesh && o.material) { o.material.envMapIntensity = 1; } });
    astroG.add(astro);
  } else {
    const suit = new THREE.MeshStandardMaterial({ color: 0xf2f2f0, roughness: .7 });
    const body = new THREE.Mesh(new THREE.CapsuleGeometry(.45, .9, 8, 16), suit); astroG.add(body);
    const helm = new THREE.Mesh(new THREE.SphereGeometry(.34, 24, 24), new THREE.MeshStandardMaterial({ color: 0x111111, metalness: .9, roughness: .1 })); helm.position.y = .95; astroG.add(helm);
  }
  // the suit's comm + biometric unit (the attachment from our design) — a small blinking light on the chest
  const unit = new THREE.Mesh(new THREE.SphereGeometry(.05, 12, 12), new THREE.MeshBasicMaterial({ color: 0x38bdf8 })); unit.position.set(.12, .25, .42); astroG.add(unit);
  // tether from the suit to the truss
  const tetherMat = new THREE.MeshStandardMaterial({ color: 0xe9e3d0, roughness: .8 });
  let tether = null; const tA = new THREE.Vector3(), tB = new THREE.Vector3();
  const updateTether = t => {
    astroG.localToWorld(tA.set(.25, -.15, .1)); truss.localToWorld(tB.set(-4, 0, 0));
    const mid = tA.clone().lerp(tB, .5); mid.y -= .9 + .25 * Math.sin(t * .7); mid.x += .4 * Math.sin(t * .5);
    const curve = new THREE.CatmullRomCurve3([tA.clone(), tA.clone().lerp(mid, .5).add(new THREE.Vector3(0, -.3, 0)), mid, tB.clone()]);
    if (tether) { tether.geometry.dispose(); scene.remove(tether); }
    tether = new THREE.Mesh(new THREE.TubeGeometry(curve, 40, .022, 6, false), tetherMat); scene.add(tether);
  };
  // lens flare drawn on a 2D canvas on top, following the Sun's projected position
  const g2 = fl.getContext('2d'); const sunWorld = sunDir.clone().multiplyScalar(900);
  const ghosts = [[.25, 26, 'rgba(120,255,170,.18)'], [.45, 14, 'rgba(255,170,90,.22)'], [.62, 44, 'rgba(255,120,120,.10)'], [.8, 9, 'rgba(160,190,255,.35)'], [1.1, 70, 'rgba(90,140,255,.08)'], [1.35, 20, 'rgba(255,220,140,.16)']];
  const drawFlare = t => {
    const w = fl.width, h = fl.height; g2.clearRect(0, 0, w, h);
    const p = sunWorld.clone().project(cam); if (p.z > 1) return;
    const sx = (p.x * .5 + .5) * w, sy = (-p.y * .5 + .5) * h, cx = w / 2, cy = h / 2, sc = w / 1400;
    g2.globalCompositeOperation = 'lighter';
    let gr = g2.createRadialGradient(sx, sy, 0, sx, sy, 260 * sc); gr.addColorStop(0, 'rgba(255,255,255,1)'); gr.addColorStop(.06, 'rgba(255,250,235,.95)'); gr.addColorStop(.25, 'rgba(255,220,170,.25)'); gr.addColorStop(1, 'rgba(255,200,150,0)');
    g2.fillStyle = gr; g2.beginPath(); g2.arc(sx, sy, 260 * sc, 0, 7); g2.fill();
    g2.save(); g2.translate(sx, sy); g2.rotate(t * .02);                                      // starburst rays
    for (let i = 0; i < 18; i++) { const L = (i % 2 ? 180 : 320) * sc * (.85 + .15 * Math.sin(t * 1.3 + i)); g2.rotate(Math.PI * 2 / 18); const lg = g2.createLinearGradient(0, 0, L, 0); lg.addColorStop(0, 'rgba(255,255,255,.55)'); lg.addColorStop(1, 'rgba(255,255,255,0)'); g2.fillStyle = lg; g2.beginPath(); g2.moveTo(0, -1.6 * sc); g2.lineTo(L, 0); g2.lineTo(0, 1.6 * sc); g2.fill(); }
    g2.restore();
    for (const [k, r, c] of ghosts) { const x = sx + (cx - sx) * k * 2, y = sy + (cy - sy) * k * 2; const gg = g2.createRadialGradient(x, y, 0, x, y, r * sc * 1.4); gg.addColorStop(0, c); gg.addColorStop(.7, c); gg.addColorStop(1, 'rgba(0,0,0,0)'); g2.fillStyle = gg; g2.beginPath(); g2.arc(x, y, r * sc * 1.4, 0, 7); g2.fill(); }
    g2.globalCompositeOperation = 'source-over';
  };
  let mx = 0, my = 0;
  stage.addEventListener('pointermove', e => { const r = stage.getBoundingClientRect(); mx = (e.clientX - r.left) / r.width - .5; my = (e.clientY - r.top) / r.height - .5; }, { passive: true });
  const size = () => { const w = stage.clientWidth, h = stage.clientHeight; R.setSize(w, h, false); cam.aspect = w / h; cam.fov = w < h ? 60 : 42; cam.updateProjectionMatrix(); const d = Math.min(devicePixelRatio || 1, 1.5); fl.width = w * d; fl.height = h * d; };
  size(); addEventListener('resize', size);
  let cx = 0, cy = 0;
  const once = loop(stage, (dt, t) => {
    cx += (mx - cx) * Math.min(1, dt * 2); cy += (my - cy) * Math.min(1, dt * 2);
    cam.position.set(cx * .8, -cy * .5, 0); cam.lookAt(cx * .3, -.6 - cy * .2, -10);
    astroG.position.set(1.6 + Math.sin(t * .23) * .35, .2 + Math.sin(t * .41) * .22, -7.2 + Math.sin(t * .17) * .3);
    astroG.rotation.set(.15 + Math.sin(t * .19) * .12, -.6 + t * .045, -.22 + Math.sin(t * .27) * .1);
    unit.visible = (t % 1.4) < .9;
    eMesh.rotation.y += dt * .004; if (cl) cl.rotation.y += dt * .006;
    truss.rotation.z = .32 + Math.sin(t * .1) * .015;
    updateTether(t); R.render(scene, cam); drawFlare(t);
  }, R, true);
  once(); sec.classList.add('gl-on');
}

try { nebula(); } catch (e) { console.warn('nebula', e); }
try { eva(); } catch (e) { console.warn('eva', e); }
