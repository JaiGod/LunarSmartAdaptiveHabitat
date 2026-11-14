// SELENE "big three" sensing for the browser build of LUNA VR: face, posture and voice.
// Runs entirely in this browser tab. No image, video or audio is stored or sent anywhere:
// only a few numbers (scores 0..1) and the words of voice commands are handed to the game.
// Face + posture: MediaPipe Tasks Vision (FaceLandmarker with blendshapes, PoseLandmarker lite).
// Voice: Web Speech API for commands (Chrome / Edge), Web Audio for loudness and pitch.
(function () {
  if (window.seleneSense) return;
  const VER = "0.10.14";
  const CDN = "https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@" + VER;
  const FACE_MODEL = "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task";
  const POSE_MODEL = "https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/1/pose_landmarker_lite.task";
  const BASELINE_S = 20;          // the first 20 s of camera time become your personal baseline

  const st = {
    cam: "off", mic: "off", sr: "off", err: "", model: "",
    face: null, posture: null, voice: null, scores: null,
    said: [], heard: "", frames: 0, base: null, baseN: 0, t0: 0
  };
  let video = null, stream = null, fl = null, pl = null, running = false, lastT = -1;
  let actx = null, analyser = null, micStream = null, buf = null, rec = null, wantSR = false;
  let pip = null, pipCtx = null, pipLabel = null;
  const ema = {};
  const E = (k, v, a) => { if (v == null || isNaN(v)) return ema[k]; ema[k] = ema[k] == null ? v : ema[k] + a * (v - ema[k]); return ema[k]; };
  const clamp = (x) => Math.max(0, Math.min(1, x));
  let blinkClosed = false, blinkTimes = [], yawnOpen = false, yawnTimes = [], prevPose = null;
  let pitchHist = [], speechFrames = 0, voiceFrames = 0;

  // ---------------------------------------------------------------- picture-in-picture preview (you always see that the camera is on)
  function makePip() {
    if (pip) return;
    const box = document.createElement("div");
    box.style.cssText = "position:fixed;left:12px;bottom:12px;z-index:50;background:#0b1430;border:2px solid #38bdf8;border-radius:10px;padding:4px;font:12px system-ui,sans-serif;color:#e8f0ff;pointer-events:none";
    pip = document.createElement("canvas"); pip.width = 192; pip.height = 144; pip.style.cssText = "display:block;border-radius:6px";
    pipLabel = document.createElement("div"); pipLabel.textContent = "● SELENE camera on · nothing is recorded";
    pipLabel.style.cssText = "padding:3px 2px 0;color:#f87171";
    box.appendChild(pip); box.appendChild(pipLabel); document.body.appendChild(box);
    pipCtx = pip.getContext("2d");
    pip._box = box;
  }
  function removePip() { if (pip && pip._box) pip._box.remove(); pip = null; pipCtx = null; }

  // ---------------------------------------------------------------- camera: face + posture
  function withTimeout(pr, ms, what) {
    return Promise.race([pr, new Promise((_, rej) => setTimeout(() => rej(new Error(what + " load timeout")), ms))]);
  }
  async function loadModels(wantFace, wantPose) {
    st.model = "loading";
    const V = await withTimeout(import(CDN + "/vision_bundle.mjs"), 25000, "model");
    const fs = await withTimeout(V.FilesetResolver.forVisionTasks(CDN + "/wasm"), 25000, "model");
    if (wantFace && !fl) fl = await withTimeout(V.FaceLandmarker.createFromOptions(fs, { baseOptions: { modelAssetPath: FACE_MODEL, delegate: "GPU" }, outputFaceBlendshapes: true, runningMode: "VIDEO", numFaces: 1 }), 40000, "model");
    if (wantPose && !pl) pl = await withTimeout(V.PoseLandmarker.createFromOptions(fs, { baseOptions: { modelAssetPath: POSE_MODEL, delegate: "GPU" }, runningMode: "VIDEO", numPoses: 1 }), 40000, "model");
    st.model = "ready";
  }

  async function startCamera(wantFace, wantPose) {
    if (running || st.cam === "starting") return;
    try {
      st.cam = "starting"; st.err = "";
      stream = await navigator.mediaDevices.getUserMedia({ video: { width: 640, height: 480, facingMode: "user" }, audio: false });
      video = document.createElement("video"); video.muted = true; video.playsInline = true; video.srcObject = stream;
      await video.play();
      await loadModels(wantFace, wantPose);
      if (!stream) return;                       // stopped while the models were loading
      makePip();
      running = true; st.cam = "on"; st.t0 = performance.now(); st.base = null; st.baseN = 0;
      loop();
    } catch (e) {
      const m = String((e && e.name) || "") + " " + String((e && e.message) || e);
      setErr(/NotAllowed|Permission/i.test(m) ? "camera permission was denied" : /NotFound|DevicesNotFound|Overconstrained/i.test(m) ? "no camera was found" : /NotReadable|TrackStart/i.test(m) ? "another app is using the camera" : /fetch|import|network|load|module/i.test(m) ? "the face and posture models could not download (check the internet)" : "the camera could not start");
      if (st.model === "loading") st.model = "error";
      stopCamera(); st.cam = "error";
    }
  }

  function stopCamera() {
    running = false;
    if (stream) stream.getTracks().forEach((t) => t.stop());
    stream = null; video = null; removePip();
    if (st.cam !== "error") st.cam = "off";
  }

  function loop() {
    if (!running) return;
    const now = performance.now();
    if (video && video.readyState >= 2 && video.currentTime !== lastT) {
      lastT = video.currentTime;
      let fr = null, pr = null;
      try { if (fl) fr = fl.detectForVideo(video, now); } catch (e) { }
      try { if (pl) pr = pl.detectForVideo(video, now + 1); } catch (e) { }
      analyse(fr, pr, now);
      draw(fr, pr);
    }
    setTimeout(loop, 110);           // about 9 analyses a second is plenty and keeps the game smooth
  }

  function analyse(fr, pr, now) {
    st.frames++;
    const el = (now - st.t0) / 1000;
    // ---- face
    const p = fr && fr.faceLandmarks && fr.faceLandmarks[0];
    if (p) {
      const bs = {}; ((fr.faceBlendshapes && fr.faceBlendshapes[0] && fr.faceBlendshapes[0].categories) || []).forEach((c) => (bs[c.categoryName] = c.score));
      const blink = ((bs.eyeBlinkLeft || 0) + (bs.eyeBlinkRight || 0)) / 2;
      if (blink > 0.5 && !blinkClosed) { blinkTimes.push(now); blinkClosed = true; }
      if (blink < 0.3) blinkClosed = false;
      blinkTimes = blinkTimes.filter((t) => now - t < 60000);
      const jaw = bs.jawOpen || 0;
      if (jaw > 0.6 && !yawnOpen) { yawnOpen = true; yawnTimes.push(now); }
      if (jaw < 0.3) yawnOpen = false;
      yawnTimes = yawnTimes.filter((t) => now - t < 300000);
      const span = Math.min(60, Math.max(5, el));
      const face = {
        smile: E("smile", ((bs.mouthSmileLeft || 0) + (bs.mouthSmileRight || 0)) / 2, 0.08),
        frown: E("frown", ((bs.mouthFrownLeft || 0) + (bs.mouthFrownRight || 0)) / 2, 0.08),
        brow: E("brow", ((bs.browDownLeft || 0) + (bs.browDownRight || 0)) / 2 + (bs.browInnerUp || 0) * 0.5, 0.08),
        eyes_closed: E("closed", blink > 0.6 ? 1 : 0, 0.04),          // PERCLOS-style: share of time eyes are mostly shut
        blink_rate: blinkTimes.length * 60 / span,
        yawns: yawnTimes.length,
        head_down: E("down", Math.max(0, p[1].y - (p[33].y + p[263].y) / 2 - 0.05), 0.08)
      };
      st.face = face;
    } else if (st.face) { st.face.present = false; }
    if (st.face && p) st.face.present = true;
    // ---- posture (upper body: nose 0, ears 7/8, shoulders 11/12)
    const q = pr && pr.landmarks && pr.landmarks[0];
    if (q && q[11] && q[12] && (q[11].visibility || 1) > 0.4 && (q[12].visibility || 1) > 0.4) {
      const sw = Math.hypot(q[11].x - q[12].x, q[11].y - q[12].y) || 0.2;
      const shY = (q[11].y + q[12].y) / 2;
      const earY = q[7] && q[8] ? (q[7].y + q[8].y) / 2 : q[0].y;
      const upright = (shY - earY) / sw;                 // big = head held high above the shoulders; small = slumped
      const tilt = Math.abs(q[11].y - q[12].y) / sw;
      let move = 0;
      if (prevPose) { for (const k of [0, 11, 12, 15, 16]) if (q[k] && prevPose[k]) move += Math.hypot(q[k].x - prevPose[k].x, q[k].y - prevPose[k].y); }
      prevPose = q.map((v) => ({ x: v.x, y: v.y }));
      st.posture = {
        upright: E("upright", upright, 0.08),
        tilt: E("tilt", tilt, 0.08),
        fidget: E("fidget", Math.min(1, move * 8), 0.1),
        present: true
      };
    } else if (st.posture) { st.posture.present = false; }
    // ---- personal baseline (first 20 s), then scores relative to it
    const f = st.face, po = st.posture;
    if (el < BASELINE_S) {
      if (f && f.present) {
        const b = st.base || (st.base = { smile: 0, frown: 0, brow: 0, upright: 0, blink: 0, n: 0, np: 0 });
        b.smile += f.smile; b.frown += f.frown; b.brow += f.brow; b.n++;
        if (po && po.present) { b.upright += po.upright; b.np++; }
      }
    }
    score(el);
  }

  function score(el) {
    const f = st.face, po = st.posture, v = st.voice, b = st.base;
    const calibrating = el < BASELINE_S;
    const bs = (k, d) => (b && b.n > 5 ? b[k] / b.n : d);
    const bu = b && b.np > 5 ? b.upright / b.np : 1.1;
    let fat = 0, str = 0, low = 0, w = 0;
    if (f && f.present !== false) {
      fat += 0.9 * clamp(f.eyes_closed * 3) + 0.4 * clamp((f.blink_rate - 18) / 20) + 0.3 * clamp(f.yawns / 3) + 0.5 * clamp(f.head_down * 6);
      str += 1.0 * clamp((f.brow - bs("brow", 0.15)) * 3 + 0.2);
      low += 0.7 * clamp((f.frown - bs("frown", 0.05)) * 4 + 0.15) + 0.5 * clamp((bs("smile", 0.25) - f.smile) * 2 + 0.2);
      w += 1;
    }
    if (po && po.present !== false) {
      const slump = clamp((bu - po.upright) / Math.max(0.15, bu * 0.35));
      fat += 0.5 * slump; low += 0.4 * slump; str += 0.6 * clamp(po.fidget * 2.5) + 0.2 * clamp(po.tilt * 3);
      w += 0.5;
    }
    if (v && v.active) {
      str += 0.6 * clamp((v.pitch_var - 0.12) * 4) + 0.3 * clamp((v.speech - 0.6) * 2.5);
      low += 0.4 * clamp((0.04 - v.energy) * 20);
      w += 0.4;
    }
    if (w <= 0) { st.scores = null; return; }
    const n = (x, k) => clamp(x / k);
    const s = { fatigue: n(fat, 1.6), stress: n(str, 1.8), low_mood: n(low, 1.6), calibrating: calibrating, conf: clamp(w / 1.9) };
    s.mood = s.fatigue > 0.55 ? "tired" : s.stress > 0.55 ? "anxious" : s.low_mood > 0.55 ? "sad" : f && f.smile > 0.35 ? "happy" : "calm";
    st.scores = s;
  }

  function draw(fr, pr) {
    if (!pipCtx || !video) return;
    const W = pip.width, H = pip.height;
    pipCtx.save(); pipCtx.translate(W, 0); pipCtx.scale(-1, 1);       // mirror like a selfie camera
    pipCtx.drawImage(video, 0, 0, W, H);
    pipCtx.fillStyle = "#38bdf8";
    const p = fr && fr.faceLandmarks && fr.faceLandmarks[0];
    if (p) for (const i of [33, 263, 61, 291, 1, 152, 105, 334, 10]) { pipCtx.beginPath(); pipCtx.arc(p[i].x * W, p[i].y * H, 2, 0, 7); pipCtx.fill(); }
    const q = pr && pr.landmarks && pr.landmarks[0];
    if (q) {
      pipCtx.strokeStyle = "#34d399"; pipCtx.lineWidth = 2; pipCtx.beginPath();
      for (const [a, c] of [[11, 12], [11, 13], [12, 14], [13, 15], [14, 16], [7, 11], [8, 12]]) if (q[a] && q[c]) { pipCtx.moveTo(q[a].x * W, q[a].y * H); pipCtx.lineTo(q[c].x * W, q[c].y * H); }
      pipCtx.stroke();
    }
    pipCtx.restore();
    if (pipLabel) pipLabel.textContent = (st.scores && st.scores.calibrating ? "● SELENE camera on · learning your baseline" : "● SELENE camera on · nothing is recorded");
  }

  // ---------------------------------------------------------------- microphone: loudness + pitch, and voice commands
  async function startMic() {
    if (micStream || st.mic === "starting") return;
    try {
      st.mic = "starting";
      micStream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true }, video: false });
      actx = new (window.AudioContext || window.webkitAudioContext)();
      const src = actx.createMediaStreamSource(micStream);
      analyser = actx.createAnalyser(); analyser.fftSize = 2048; buf = new Float32Array(analyser.fftSize);
      src.connect(analyser);
      st.mic = "on"; micT0 = performance.now(); micLoop();
    } catch (e) { st.mic = "error"; setErr("microphone permission was denied or no microphone was found"); micStream = null; }
  }
  function stopMic() {
    if (micStream) micStream.getTracks().forEach((t) => t.stop());
    micStream = null; analyser = null;
    if (actx) { try { actx.close(); } catch (e) { } actx = null; }
    if (st.mic !== "error") st.mic = "off";
    st.voice = null;
  }
  function micLoop() {
    if (!analyser) return;
    analyser.getFloatTimeDomainData(buf);
    let rms = 0; for (let i = 0; i < buf.length; i++) rms += buf[i] * buf[i]; rms = Math.sqrt(rms / buf.length);
    voiceFrames++;
    const speaking = rms > 0.02;
    if (speaking) {
      speechFrames++;
      const f0 = pitch(buf, actx.sampleRate);
      if (f0 > 70 && f0 < 400) { pitchHist.push(f0); if (pitchHist.length > 120) pitchHist.shift(); }
    }
    const mean = pitchHist.length ? pitchHist.reduce((a, c) => a + c, 0) / pitchHist.length : 0;
    const sd = pitchHist.length > 5 ? Math.sqrt(pitchHist.reduce((a, c) => a + (c - mean) * (c - mean), 0) / pitchHist.length) : 0;
    st.voice = {
      active: pitchHist.length > 10,
      level: E("lvl", rms, 0.3),
      energy: E("energy", speaking ? rms : null, 0.05) || 0,
      pitch: Math.round(mean),
      pitch_var: mean ? sd / mean : 0,
      speech: voiceFrames > 0 ? E("speech", speaking ? 1 : 0, 0.02) : 0
    };
    if (!running) score((performance.now() - micT0) / 1000);      // voice only: a lower-confidence read from the voice alone
    setTimeout(micLoop, 100);
  }
  function pitch(b, sr) {                              // simple autocorrelation pitch estimate
    const n = 1024; let best = -1, bestC = 0;
    for (let lag = Math.floor(sr / 400); lag < Math.floor(sr / 70); lag++) {
      let c = 0; for (let i = 0; i < n; i++) c += b[i] * b[i + lag];
      if (c > bestC) { bestC = c; best = lag; }
    }
    return best > 0 ? sr / best : 0;
  }

  let srFails = 0, micT0 = 0;
  function setErr(m) { if (!st.err) st.err = m; else if (st.err.indexOf(m) < 0) st.err += "; " + m; }
  function startVoice() {
    const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SR) { st.sr = "unsupported"; return; }
    wantSR = true; srFails = 0;
    spawnRec();
  }
  function spawnRec() {
    const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!wantSR || rec || !SR) return;
    const r = new SR(); rec = r; r.lang = "en-US"; r.continuous = true; r.interimResults = true;
    r.onresult = (ev) => {
      srFails = 0;
      for (let i = ev.resultIndex; i < ev.results.length; i++) {
        const x = ev.results[i]; const t = x[0].transcript.trim();
        if (x.isFinal) { if (t) st.said.push(t); st.heard = ""; } else st.heard = t;
      }
    };
    r.onerror = (e) => {
      if (e.error === "not-allowed" || e.error === "service-not-allowed") { wantSR = false; st.sr = "error"; setErr("speech recognition was blocked by the browser"); }
      else if (e.error === "network" || e.error === "audio-capture") {
        srFails++;
        if (srFails >= 4) { wantSR = false; st.sr = "error"; setErr(e.error === "network" ? "the browser's speech service can't be reached (offline?)" : "no microphone for speech"); }
      }
    };
    r.onend = () => {
      if (rec === r) rec = null;
      if (wantSR) setTimeout(spawnRec, Math.min(5000, 300 * Math.pow(2, srFails)));   // keep listening; back off if the service keeps failing
      else if (st.sr !== "error") st.sr = "off";
    };
    try { r.start(); st.sr = "on"; } catch (e) { rec = null; st.sr = "error"; }
  }
  function stopVoice() { wantSR = false; const r = rec; rec = null; if (r) { try { r.abort(); } catch (e) { } } if (st.sr !== "unsupported") st.sr = "off"; st.heard = ""; }

  // ---------------------------------------------------------------- the API the game calls (through JavaScriptBridge)
  window.seleneSense = {
    start: function (face, posture, voice) {
      if (face || posture) startCamera(!!face, !!posture);
      if (voice) { startMic(); startVoice(); }
      return true;
    },
    stop: function () { stopCamera(); stopMic(); stopVoice(); st.cam = "off"; st.mic = "off"; st.err = ""; st.scores = null; st.face = null; st.posture = null; return true; },
    stopCamera: function () { stopCamera(); st.face = null; st.posture = null; return true; },
    stopVoice: function () { stopMic(); stopVoice(); return true; },
    _t: { analyse: analyse, st: st },            // test hook (used by the headless check)
    hasSpeech: function () { return !!(window.SpeechRecognition || window.webkitSpeechRecognition); },
    poll: function () {
      const out = { cam: st.cam, mic: st.mic, sr: st.sr, err: st.err, model: st.model, face: st.face, posture: st.posture, voice: st.voice, scores: st.scores, said: st.said.splice(0), heard: st.heard };
      return JSON.stringify(out);
    }
  };
})();
