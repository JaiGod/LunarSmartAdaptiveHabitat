// LUNA-X Cosmos FX — procedural Milky Way, planets, Sun, black hole, shooting stars,
// lunar eclipse, and the POWER SURGE energy effect (aura, lightning, shockwave, sparkles).
// Everything is generated in code (no external images). NASA HUNCH Project ARES · © 2026 Jaineil R.
import * as THREE from 'three';

const rand = (a, b) => a + Math.random() * (b - a);

// ---------------------------------------------------------------- textures
function canvasTex(w, h, draw) { const c = document.createElement('canvas'); c.width = w; c.height = h; draw(c.getContext('2d'), w, h); const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t; }
function noiseBands(g, w, h, cols, n = 30, wob = 6) {
  for (let i = 0; i < n; i++) { const y = (i / n) * h; g.fillStyle = cols[i % cols.length]; g.beginPath(); g.moveTo(0, y);
    for (let x = 0; x <= w; x += 16) g.lineTo(x, y + Math.sin(x * 0.02 + i) * wob + Math.sin(x * 0.07 + i * 3) * wob * 0.4);
    g.lineTo(w, y + h / n + 2); g.lineTo(0, y + h / n + 2); g.fill(); }
}
const TEX = {
  earth: () => canvasTex(1024, 512, (g, w, h) => {
    g.fillStyle = '#123a7a'; g.fillRect(0, 0, w, h);
    for (let i = 0; i < 26; i++) { g.fillStyle = ['#2f6b3a', '#3b7d3f', '#8a7a4a', '#4d8a45'][i % 4]; g.beginPath(); const x = rand(0, w), y = rand(h * 0.15, h * 0.85);
      for (let a = 0; a < 6.28; a += 0.5) { const r = rand(30, 110); g.lineTo(x + Math.cos(a) * r * 1.6, y + Math.sin(a) * r * 0.8); } g.fill(); }
    g.fillStyle = '#e8f0ff'; g.fillRect(0, 0, w, h * 0.07); g.fillRect(0, h * 0.93, w, h * 0.07);
  }),
  clouds: () => canvasTex(1024, 512, (g, w, h) => { for (let i = 0; i < 420; i++) { g.fillStyle = `rgba(255,255,255,${rand(0.05, 0.22)})`; g.beginPath(); g.ellipse(rand(0, w), rand(0, h), rand(20, 90), rand(6, 22), rand(-0.3, 0.3), 0, 7); g.fill(); } }),
  jupiter: () => canvasTex(1024, 512, (g, w, h) => { noiseBands(g, w, h, ['#c9a27a', '#e6cfa8', '#a8774f', '#f0e2c6', '#b98b62', '#dcc19a'], 34, 7);
    g.fillStyle = '#b5532f'; g.beginPath(); g.ellipse(w * 0.62, h * 0.64, 60, 30, 0, 0, 7); g.fill(); g.fillStyle = '#d9875a'; g.beginPath(); g.ellipse(w * 0.62, h * 0.64, 40, 18, 0, 0, 7); g.fill(); }),
  venus: () => canvasTex(512, 256, (g, w, h) => noiseBands(g, w, h, ['#e9cf94', '#f3dfae', '#d6b577', '#f6e7c2'], 18, 10)),
  saturn: () => canvasTex(512, 256, (g, w, h) => noiseBands(g, w, h, ['#e3cf9d', '#d1b77f', '#efe0b8', '#c9ad75'], 22, 3)),
  ring: () => canvasTex(512, 8, (g, w) => { for (let x = 0; x < w; x++) { const a = 0.15 + 0.6 * Math.abs(Math.sin(x * 0.09) * Math.sin(x * 0.023)); g.fillStyle = `rgba(230,210,170,${x < 40 || (x > 300 && x < 318) ? 0 : a})`; g.fillRect(x, 0, 1, 8); } }),
};

// ---------------------------------------------------------------- shaders
const skyVS = 'varying vec3 vD; void main(){ vD = normalize(position); gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.0); }';
const NOISE = `float h(vec3 p){ return fract(sin(dot(p,vec3(127.1,311.7,74.7)))*43758.5453); }
float n3(vec3 p){ vec3 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
  return mix(mix(mix(h(i),h(i+vec3(1,0,0)),f.x),mix(h(i+vec3(0,1,0)),h(i+vec3(1,1,0)),f.x),f.y),mix(mix(h(i+vec3(0,0,1)),h(i+vec3(1,0,1)),f.x),mix(h(i+vec3(0,1,1)),h(i+vec3(1,1,1)),f.x),f.y),f.z); }
float fbm(vec3 p){ float a=0.5, s=0.0; for(int i=0;i<5;i++){ s+=a*n3(p); p*=2.03; a*=0.5; } return s; }`;
const milkyFS = `varying vec3 vD; uniform float uO; uniform float uT; ${NOISE}
void main(){ vec3 d = normalize(vD); vec3 axis = normalize(vec3(0.35,0.8,-0.48)); float band = 1.0 - abs(dot(d, axis));
  float b = pow(band, 7.0); float n = fbm(d*4.0 + vec3(0.0,0.0,uT*0.004)); float dust = fbm(d*9.0+3.1);
  vec3 col = mix(vec3(0.18,0.12,0.35), vec3(0.95,0.78,0.58), smoothstep(0.35,0.9,n)) * b * (0.55+0.9*n);
  col *= 1.0 - 0.75*smoothstep(0.45,0.75,dust)*b; col += vec3(0.25,0.35,0.9)*pow(band,3.0)*0.06*n;
  float neb = smoothstep(0.62,0.9,fbm(d*2.2+7.0)); col += vec3(0.6,0.15,0.55)*neb*0.12 + vec3(0.1,0.3,0.7)*smoothstep(0.6,0.9,fbm(d*2.0-4.0))*0.10;
  gl_FragColor = vec4(col*uO, 1.0); }`;
const glowFS = `varying vec2 vU; uniform vec3 uC; uniform float uO; uniform float uP; void main(){ float d=length(vU-0.5)*2.0; float a=pow(max(0.0,1.0-d),uP); gl_FragColor=vec4(uC*a*uO, a*uO); }`;
const uvVS = 'varying vec2 vU; void main(){ vU = uv; gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.0); }';
const diskFS = `varying vec2 vU; uniform float uT; uniform float uO; ${NOISE}
void main(){ vec2 p=(vU-0.5)*2.0; float r=length(p); float a=atan(p.y,p.x);
  float ring=smoothstep(0.32,0.42,r)*(1.0-smoothstep(0.62,1.0,r)); float sw=fbm(vec3(cos(a-uT*0.6+r*6.0)*2.0,sin(a-uT*0.6+r*6.0)*2.0,r*4.0));
  vec3 c=mix(vec3(1.0,0.45,0.12),vec3(1.0,0.92,0.7),sw)*ring*(0.6+1.4*sw); float hole=1.0-smoothstep(0.24,0.33,r);
  float lens=exp(-pow((r-0.34)*22.0,2.0))*1.6; c+=vec3(1.0,0.85,0.6)*lens; c*=1.0-hole; float al=max(ring, max(lens*0.8,hole));
  gl_FragColor=vec4(c*uO, al*uO); }`;
const auraFS = `varying vec2 vU; uniform float uT; uniform float uO; uniform vec3 uC; ${NOISE}
void main(){ vec2 p=vU; float x=abs(p.x-0.5)*2.0; float flame=fbm(vec3(p.x*5.0, p.y*3.0 - uT*2.6, uT*0.4));
  float shape = (1.0 - smoothstep(0.55-0.35*p.y, 1.0-0.3*p.y, x + (flame-0.5)*0.6)) * smoothstep(0.0,0.12,p.y) * (1.0 - smoothstep(0.75,1.0,p.y+flame*0.25));
  float core = (1.0 - smoothstep(0.0, 0.55, x)) * 0.5; vec3 c = mix(uC, vec3(1.0,0.98,0.85), core + flame*0.4);
  float a = shape*(0.35+0.65*flame)*uO; gl_FragColor = vec4(c*a*1.6, a); }`;

// ---------------------------------------------------------------- the module
export function createCosmos(scene, opts = {}) {
  const S = opts.scale || 1, cosmos = new THREE.Group(); scene.add(cosmos);
  const mobile = matchMedia('(max-width:700px)').matches;
  // milky way skydome
  const skyMat = new THREE.ShaderMaterial({ vertexShader: skyVS, fragmentShader: milkyFS, uniforms: { uO: { value: 0 }, uT: { value: 0 } }, side: THREE.BackSide, depthWrite: false });
  const sky = new THREE.Mesh(new THREE.SphereGeometry(300, 48, 32), skyMat); sky.renderOrder = -10; cosmos.add(sky);
  // glow sprite helper
  const glow = (color, size, power = 2) => { const m = new THREE.ShaderMaterial({ vertexShader: uvVS, fragmentShader: glowFS, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
    uniforms: { uC: { value: new THREE.Color(color) }, uO: { value: 0 }, uP: { value: power } } }); const s = new THREE.Mesh(new THREE.PlaneGeometry(size, size), m); return s; };
  const planets = [];
  const planet = (tex, r, pos, spin, extra) => { const m = new THREE.MeshStandardMaterial({ map: tex, roughness: 0.9, transparent: true, opacity: 0 }); const p = new THREE.Mesh(new THREE.SphereGeometry(r, 48, 32), m);
    p.position.copy(pos); p.rotation.z = 0.35; cosmos.add(p); planets.push({ p, m, spin }); if (extra) extra(p); return p; };
  // Sun
  const sunPos = new THREE.Vector3(150, 42, -190);
  const sunCore = glow('#fff4d6', 26, 1.2), sunHalo = glow('#ffb347', 110, 3.2); sunCore.position.copy(sunPos); sunHalo.position.copy(sunPos); cosmos.add(sunHalo, sunCore);
  const sunLight = new THREE.DirectionalLight(0xfff0dd, 0); sunLight.position.copy(sunPos); scene.add(sunLight);
  // Earth (with clouds + atmosphere)
  const earth = planet(TEX.earth(), 11, new THREE.Vector3(-70, 30, -150), 0.02, (p) => {
    const c = new THREE.Mesh(new THREE.SphereGeometry(11.25, 48, 32), new THREE.MeshStandardMaterial({ map: TEX.clouds(), transparent: true, opacity: 0, depthWrite: false })); p.add(c); p.userData.clouds = c;
    const atm = glow('#5ab8ff', 34, 2.4); atm.position.set(0, 0, -0.5); p.add(atm); p.userData.atm = atm; });
  const jupiter = planet(TEX.jupiter(), 16, new THREE.Vector3(120, -18, -230), 0.05);
  const venus = planet(TEX.venus(), 4, new THREE.Vector3(55, 62, -170), 0.004);
  const saturn = planet(TEX.saturn(), 9, new THREE.Vector3(-160, -6, -200), 0.03, (p) => {
    const rt = TEX.ring(); const ring = new THREE.Mesh(new THREE.RingGeometry(12, 22, 96, 1), new THREE.MeshBasicMaterial({ map: rt, transparent: true, opacity: 0, side: THREE.DoubleSide, depthWrite: false }));
    const pos = ring.geometry.attributes.position, uv = ring.geometry.attributes.uv; for (let i = 0; i < pos.count; i++) { const r = Math.hypot(pos.getX(i), pos.getY(i)); uv.setXY(i, (r - 12) / 10, 0.5); }
    ring.rotation.x = Math.PI / 2.3; p.add(ring); p.userData.ring = ring; });
  // black hole
  const bhMat = new THREE.ShaderMaterial({ vertexShader: uvVS, fragmentShader: diskFS, transparent: true, depthWrite: false, uniforms: { uT: { value: 0 }, uO: { value: 0 } } });
  const bh = new THREE.Mesh(new THREE.PlaneGeometry(60, 60), bhMat); bh.position.set(-150, 85, -230); bh.rotation.z = 0.5; cosmos.add(bh);
  // shooting stars
  const meteors = [];
  const mMat = new THREE.ShaderMaterial({ vertexShader: uvVS, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, uniforms: { uO: { value: 1 }, uC: { value: new THREE.Color('#fff1c4') } },
    fragmentShader: 'varying vec2 vU; uniform float uO; uniform vec3 uC; void main(){ float a=pow(vU.x,3.0)*(1.0-abs(vU.y-0.5)*2.0); gl_FragColor=vec4(uC*a*uO*2.0,a*uO); }' });
  function meteor(gold) { const m = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), mMat.clone()); m.material.uniforms.uC.value.set(gold ? '#ffd76a' : '#e6f1ff');
    const r = 130, th = rand(-0.9, 0.9), ph = rand(0.15, 0.7); const start = new THREE.Vector3(Math.sin(th) * r, Math.sin(ph) * r * 0.9 + 20, -Math.cos(th) * r);
    const dir = new THREE.Vector3(rand(-1, 1), rand(-0.9, -0.35), rand(-0.3, 0.3)).normalize(); m.userData = { start, dir, t: 0, life: rand(0.9, 1.6), len: rand(14, 30), speed: rand(70, 120) };
    cosmos.add(m); meteors.push(m); }
  let nextMeteor = 2;
  // ---------------- POWER SURGE (aura, lightning, sparkles, shockwave)
  const surge = new THREE.Group(); scene.add(surge);
  const auraMat = new THREE.ShaderMaterial({ vertexShader: uvVS, fragmentShader: auraFS, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide,
    uniforms: { uT: { value: 0 }, uO: { value: 0 }, uC: { value: new THREE.Color('#ffc93c') } } });
  const auras = []; for (let i = 0; i < 3; i++) { const a = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), auraMat); a.rotation.y = (i / 3) * Math.PI; surge.add(a); auras.push(a); }
  const N = mobile ? 260 : 600, sp = new Float32Array(N * 3), sv = new Float32Array(N * 3), sl = new Float32Array(N);
  const sparkGeo = new THREE.BufferGeometry(); sparkGeo.setAttribute('position', new THREE.BufferAttribute(sp, 3)); sparkGeo.setAttribute('life', new THREE.BufferAttribute(sl, 1));
  const sparkMat = new THREE.ShaderMaterial({ transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, uniforms: { uS: { value: 30 } },
    vertexShader: 'attribute float life; varying float vL; uniform float uS; void main(){ vL=life; vec4 mv=modelViewMatrix*vec4(position,1.0); gl_PointSize=uS*life/(-mv.z); gl_Position=projectionMatrix*mv; }',
    fragmentShader: 'varying float vL; void main(){ float d=length(gl_PointCoord-0.5); if(d>0.5) discard; float a=(1.0-d*2.0)*vL; gl_FragColor=vec4(vec3(1.0,0.86,0.45)*a*2.0+vec3(1.0)*pow(1.0-d*2.0,6.0)*vL, a); }' });
  const sparks = new THREE.Points(sparkGeo, sparkMat); sparks.frustumCulled = false; surge.add(sparks);
  const boltMat = new THREE.LineBasicMaterial({ color: 0xbfe6ff, transparent: true, opacity: 0, blending: THREE.AdditiveBlending });
  const bolts = []; for (let i = 0; i < 6; i++) { const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.BufferAttribute(new Float32Array(16 * 3), 3)); const l = new THREE.Line(g, boltMat.clone()); l.frustumCulled = false; surge.add(l); bolts.push(l); }
  const waveMat = new THREE.ShaderMaterial({ vertexShader: uvVS, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide, uniforms: { uO: { value: 0 }, uC: { value: new THREE.Color('#ffd76a') } },
    fragmentShader: 'varying vec2 vU; uniform float uO; uniform vec3 uC; void main(){ float r=length(vU-0.5)*2.0; float a=exp(-pow((r-0.92)*14.0,2.0))+0.25*exp(-pow((r-0.75)*6.0,2.0)); gl_FragColor=vec4(mix(uC,vec3(1.0),0.4)*a*uO*2.0, a*uO); }' });
  const wave = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), waveMat); wave.rotation.x = -Math.PI / 2; surge.add(wave);
  const wave2 = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), waveMat.clone()); surge.add(wave2);
  let surgeT = -1, surgeBox = { c: new THREE.Vector3(), h: 1, r: 0.5 };
  function powerSurge(center, height, radius, color) { surgeT = 0; surgeBox = { c: center.clone(), h: height, r: radius }; if (color) auraMat.uniforms.uC.value.set(color);
    for (let i = 0; i < N; i++) sl[i] = 0; }
  const tmp = new THREE.Vector3();
  // ---------------- update
  let moonRef = null, eclipseT = 22, eclipseOn = false;
  function update(dt, T, camera, intro) {
    const o = Math.min(1, intro); skyMat.uniforms.uO.value = o * 0.9; skyMat.uniforms.uT.value = T;
    sky.position.copy(camera.position);
    sunCore.material.uniforms.uO.value = o; sunHalo.material.uniforms.uO.value = o * (0.55 + 0.08 * Math.sin(T * 0.8)); sunLight.intensity = 0.35 * o;
    [sunCore, sunHalo, bh].forEach((s) => s.lookAt(camera.position)); bh.rotateZ(0.5);
    bhMat.uniforms.uT.value = T; bhMat.uniforms.uO.value = o;
    planets.forEach(({ p, m, spin }) => { m.opacity = o; p.rotation.y += dt * spin; if (p.userData.clouds) { p.userData.clouds.material.opacity = 0.85 * o; p.userData.clouds.rotation.y += dt * 0.008; }
      if (p.userData.atm) { p.userData.atm.lookAt(camera.position); p.userData.atm.material.uniforms.uO.value = 0.55 * o; } if (p.userData.ring) p.userData.ring.material.opacity = 0.9 * o; });
    // shooting stars
    nextMeteor -= dt; if (nextMeteor < 0 && o > 0.5) { meteor(Math.random() < 0.35); if (Math.random() < 0.25) meteor(true); nextMeteor = rand(1.5, 4.5); }
    for (let i = meteors.length - 1; i >= 0; i--) { const m = meteors[i], u = m.userData; u.t += dt; const k = u.t / u.life;
      const head = tmp.copy(u.start).addScaledVector(u.dir, u.t * u.speed); m.position.copy(head).addScaledVector(u.dir, -u.len / 2); m.lookAt(camera.position);
      m.scale.set(u.len, 0.5, 1); m.material.uniforms.uO.value = Math.sin(Math.min(1, k) * Math.PI) * o;
      // align the streak with its direction on screen
      const a = head.clone().project(camera), b = head.clone().addScaledVector(u.dir, -u.len).project(camera); m.rotateZ(Math.atan2(a.y - b.y, (a.x - b.x) * camera.aspect));
      if (k >= 1) { cosmos.remove(m); m.material.dispose(); meteors.splice(i, 1); } }
    // lunar eclipse cycle: every ~45 s the Moon dims to blood-red and back
    if (moonRef) { eclipseT -= dt; let e = 0; if (eclipseT < 0) { e = Math.sin(Math.min(1, -eclipseT / 14) * Math.PI); if (-eclipseT > 14) eclipseT = 45; }
      moonRef.material.color.setRGB(1 - 0.05 * e, 1 - 0.72 * e, 1 - 0.8 * e); moonRef.material.emissive = moonRef.material.emissive || new THREE.Color(); moonRef.material.emissive.setRGB(0.35 * e, 0.06 * e, 0.02 * e); eclipseOn = e > 0.05; }
    // power surge
    if (surgeT >= 0) {
      surgeT += dt; const k = surgeT; const env = Math.min(1, k / 0.35) * (k < 5 ? 1 : Math.max(0, 1 - (k - 5) / 1.2));
      const { c, h, r } = surgeBox; auraMat.uniforms.uT.value = T; auraMat.uniforms.uO.value = env * 0.95;
      auras.forEach((a) => { a.position.set(c.x, h * 0.62, c.z); a.scale.set(r * 2.9 * (1 + 0.04 * Math.sin(T * 30)), h * 1.55, 1); a.lookAt(camera.position.x, a.position.y, camera.position.z); });
      auras[1].rotateY(1.05); auras[2].rotateY(-1.05);
      // sparkles rise in the aura
      for (let i = 0; i < N; i++) { if (sl[i] <= 0 && env > 0.1 && Math.random() < 0.08) { const an = rand(0, 6.28), rr = rand(0.2, 1.1) * r; sp[i * 3] = c.x + Math.cos(an) * rr; sp[i * 3 + 1] = rand(0, h * 0.4); sp[i * 3 + 2] = c.z + Math.sin(an) * rr;
          sv[i * 3] = Math.cos(an) * rand(0.02, 0.2); sv[i * 3 + 1] = rand(0.6, 1.8) * h; sv[i * 3 + 2] = Math.sin(an) * rand(0.02, 0.2); sl[i] = 1; }
        if (sl[i] > 0) { sp[i * 3] += sv[i * 3] * dt; sp[i * 3 + 1] += sv[i * 3 + 1] * dt; sp[i * 3 + 2] += sv[i * 3 + 2] * dt; sl[i] -= dt * 0.75; } }
      sparkGeo.attributes.position.needsUpdate = true; sparkGeo.attributes.life.needsUpdate = true; sparkMat.uniforms.uS.value = 26 * (innerHeight / 900);
      // lightning arcs flicker around the model
      bolts.forEach((l, j) => { const on = env > 0.2 && Math.random() < 0.35; l.material.opacity = on ? rand(0.5, 1) * env : l.material.opacity * 0.6;
        if (on) { const P = l.geometry.attributes.position; const a0 = rand(0, 6.28), y0 = rand(0.1, 0.9) * h; let x = c.x + Math.cos(a0) * r * 1.2, y = y0, z = c.z + Math.sin(a0) * r * 1.2;
          for (let q = 0; q < 16; q++) { P.setXYZ(q, x, y, z); x += rand(-0.09, 0.09) * h; y += rand(-0.02, 0.1) * h; z += rand(-0.09, 0.09) * h; } P.needsUpdate = true; } });
      // shockwave energy wave (twice)
      const w1 = (k % 2.2) / 2.2, sc1 = r * (1 + w1 * 9); wave.position.set(c.x, 0.02, c.z); wave.scale.set(sc1, sc1, 1); wave.material.uniforms.uO.value = (1 - w1) * env;
      const w2 = Math.min(1, k / 1.1); wave2.position.set(c.x, h * 0.5, c.z); wave2.lookAt(camera.position); const sc2 = r * (0.5 + w2 * 7); wave2.scale.set(sc2, sc2, 1); wave2.material.uniforms.uO.value = (1 - w2) * 0.9;
      if (k > 6.4) { surgeT = -1; auraMat.uniforms.uO.value = 0; wave.material.uniforms.uO.value = 0; wave2.material.uniforms.uO.value = 0; bolts.forEach((l) => (l.material.opacity = 0)); for (let i = 0; i < N; i++) sl[i] = 0; sparkGeo.attributes.life.needsUpdate = true; }
    }
    return { eclipse: eclipseOn, surging: surgeT >= 0 };
  }
  return { update, powerSurge, setMoon: (m) => { moonRef = m; }, group: cosmos, meteorShower: (n = 8) => { for (let i = 0; i < n; i++) setTimeout(() => meteor(true), i * 120); } };
}

// ---------------------------------------------------------------- brain-wave monitor (canvas, for the hologram)
export function brainWaves(canvas) {
  const g = canvas.getContext('2d'); const W = canvas.width, H = canvas.height; let t = 0;
  const bands = [['ALPHA', 10, '#7CF0B4'], ['BETA', 20, '#5AB8FF'], ['THETA', 6, '#C4B5FD']];
  return (dt, amp = 1) => { t += dt; g.clearRect(0, 0, W, H); g.font = '600 9px JetBrains Mono, monospace';
    bands.forEach(([n, f, c], i) => { const y0 = (i + 0.5) * (H / 3); g.strokeStyle = c; g.lineWidth = 1.4; g.globalAlpha = 0.95; g.beginPath();
      for (let x = 34; x < W; x++) { const s = (x + t * 90) * 0.02; const v = Math.sin(s * f * 0.35) * 0.55 + Math.sin(s * f * 0.71 + i) * 0.25 + (Math.random() - 0.5) * 0.18; g.lineTo(x, y0 + v * (H / 8) * amp); }
      g.stroke(); g.globalAlpha = 1; g.fillStyle = c; g.fillText(n, 0, y0 + 3); }); };
}
