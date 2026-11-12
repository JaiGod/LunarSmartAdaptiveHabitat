// Astronaut Voice Log: microphone, speech recognition, saving and screen updates.

(() => {
  'use strict';

  const { MOODS, words, analyze, makeFeatures, detectPitch, rms } = MoodCore;
  const $ = (id) => document.getElementById(id);
  const BASELINE_KEY = 'voiceBaseline';

  // MARK: Storage. Logs and their audio stay in this browser.

  const store = {
    db: null,
    open() {
      return new Promise((resolve, reject) => {
        const request = indexedDB.open('astronaut-voice-log', 1);
        request.onupgradeneeded = () => request.result.createObjectStore('logs', { keyPath: 'id' });
        request.onsuccess = () => { this.db = request.result; resolve(); };
        request.onerror = () => reject(request.error);
      });
    },
    run(mode, action) {
      return new Promise((resolve, reject) => {
        if (!this.db) { resolve(undefined); return; }
        const request = action(this.db.transaction('logs', mode).objectStore('logs'));
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
      });
    },
    all() { return this.run('readonly', (s) => s.getAll()); },
    put(log) { return this.run('readwrite', (s) => s.put(log)); },
    remove(id) { return this.run('readwrite', (s) => s.delete(id)); },
  };

  function loadBaseline() {
    try {
      return JSON.parse(localStorage.getItem(BASELINE_KEY));
    } catch (error) {
      return null;
    }
  }

  function saveBaseline(value) {
    try {
      localStorage.setItem(BASELINE_KEY, JSON.stringify(value));
    } catch (error) {
      // The baseline still works until the page is closed.
    }
  }

  // MARK: Audio pipeline

  const pipeline = {
    onSpeech: null,
    onFatal: null,
    running: false,
    capturing: false,
    frames: [],
    recognition: null,
    stream: null,
    context: null,
    recorder: null,
    chunks: [],

    async start() {
      const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
      if (!Recognition) throw new Error('This browser has no speech recognition. Open the site in Chrome.');

      this.stream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false },
      });
      this.context = new (window.AudioContext || window.webkitAudioContext)();
      await this.context.resume();

      const source = this.context.createMediaStreamSource(this.stream);
      const processor = this.context.createScriptProcessor(4096, 1, 1);
      const silent = this.context.createGain();
      silent.gain.value = 0;
      processor.onaudioprocess = (event) => this.process(event.inputBuffer);
      source.connect(processor);
      processor.connect(silent);
      silent.connect(this.context.destination);

      this.running = true;
      this.beginRecognition();
    },

    stop() {
      this.running = false;
      this.capturing = false;
      this.frames = [];
      if (this.recorder && this.recorder.state !== 'inactive') this.recorder.stop();
      this.recorder = null;
      this.dropRecognition(null);
      if (this.stream) this.stream.getTracks().forEach((track) => track.stop());
      this.stream = null;
      if (this.context) this.context.close();
      this.context = null;
    },

    beginRecognition() {
      if (!this.running || this.recognition) return;
      const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
      const current = new Recognition();
      current.lang = 'en-US';
      current.continuous = true;
      current.interimResults = true;

      current.onresult = (event) => {
        if (this.recognition !== current) return;
        let text = '';
        for (let i = 0; i < event.results.length; i++) text += event.results[i][0].transcript;
        this.onSpeech(text.trim(), false);
      };
      current.onerror = (event) => {
        if (this.recognition === current && ['not-allowed', 'service-not-allowed'].includes(event.error)) {
          this.onFatal('Speech recognition is blocked. Allow the microphone in the address bar, then try again.');
        }
      };
      // Silence and time limits end up here, so wait briefly and listen again.
      current.onend = () => {
        if (this.recognition !== current) return;
        this.recognition = null;
        if (!this.running) return;
        this.onSpeech('', true);
        setTimeout(() => this.beginRecognition(), 250);
      };

      this.recognition = current;
      current.start();
    },

    dropRecognition(afterEnd) {
      const old = this.recognition;
      this.recognition = null;
      if (!old) {
        if (afterEnd) afterEnd();
        return;
      }
      old.onresult = null;
      old.onerror = null;
      old.onend = afterEnd;
      old.abort();
    },

    restartRecognition() {
      this.dropRecognition(() => this.beginRecognition());
    },

    beginCapture(save) {
      this.frames = [];
      this.chunks = [];
      this.recorder = null;
      if (save) {
        const recorder = new MediaRecorder(this.stream);
        recorder.ondataavailable = (event) => {
          if (event.data.size > 0) this.chunks.push(event.data);
        };
        recorder.start();
        this.recorder = recorder;
      }
      this.capturing = true;
    },

    // Stops saving and returns what was captured. Listening continues.
    endCapture() {
      this.capturing = false;
      const frames = this.frames;
      this.frames = [];
      const recorder = this.recorder;
      const chunks = this.chunks;
      this.recorder = null;

      const audio = new Promise((resolve) => {
        if (!recorder || recorder.state === 'inactive') {
          resolve(null);
          return;
        }
        recorder.onstop = () => resolve(new Blob(chunks, { type: recorder.mimeType || 'audio/webm' }));
        recorder.stop();
      });
      return { frames, audio };
    },

    process(buffer) {
      if (!this.capturing) return;
      const samples = buffer.getChannelData(0);
      this.frames.push({
        db: 20 * Math.log10(Math.max(rms(samples), 0.000001)),
        pitch: detectPitch(samples, buffer.sampleRate),
        seconds: samples.length / buffer.sampleRate,
      });
    },
  };

  // MARK: App logic

  const state = {
    mode: 'idle',
    status: 'Tap Start Listening',
    liveText: '',
    logs: [],
    baseline: null,
    committedText: '',
    partialText: '',
    starting: false,
    calibrationTimer: null,
    player: null,
    playerUrl: null,
    resumeAfterPlayback: false,
  };

  const fullText = () => `${state.committedText} ${state.partialText}`.trim();

  function clearSpeech() {
    state.committedText = '';
    state.partialText = '';
    state.liveText = '';
  }

  function set(changes) {
    Object.assign(state, changes);
    render();
  }

  async function startListening() {
    if (state.mode !== 'idle' || state.starting) return;
    state.starting = true;
    try {
      await pipeline.start();
      clearSpeech();
      set({ mode: 'listening', status: 'Listening. Say "start log".' });
    } catch (error) {
      pipeline.stop();
      const blocked = error && (error.name === 'NotAllowedError' || error.name === 'SecurityError');
      const missing = error && error.name === 'NotFoundError';
      let message = `Error: ${error && error.message}`;
      if (blocked) message = 'Microphone is off. Allow it in the address bar, then try again.';
      if (missing) message = 'No microphone was found. Connect one, then try again.';
      set({ status: message });
    } finally {
      state.starting = false;
    }
  }

  function stopListening() {
    if (state.mode !== 'listening') return;
    pipeline.stop();
    clearSpeech();
    set({ mode: 'idle', status: 'Tap Start Listening' });
  }

  async function mainButtonTapped() {
    switch (state.mode) {
      case 'idle':
        await startListening();
        if (state.mode === 'listening') startLog();
        break;
      case 'listening':
        startLog();
        break;
      case 'recording':
        stopLog(fullText());
        break;
      case 'calibrating':
        finishBaseline();
        break;
      case 'playing':
        finishPlayback();
        break;
      default:
        break;
    }
  }

  function handleSpeech(text, ended) {
    if (!['listening', 'recording', 'calibrating'].includes(state.mode)) return;

    if (text) state.partialText = text;
    const spoken = ` ${words(text).join(' ')} `;

    if (state.mode === 'listening') {
      state.liveText = state.partialText;
      if ([' start log ', ' begin log ', ' start blog '].some((c) => spoken.includes(c))) {
        startLog();
        return;
      }
      if (spoken.includes(' set baseline ')) {
        startBaseline();
        return;
      }
      if (spoken.includes(' play last log ') && state.logs.length) {
        play(state.logs[0]);
        return;
      }
      if (ended) clearSpeech();
    } else if (state.mode === 'recording') {
      state.liveText = fullText();
      for (const command of ['stop log', 'end log', 'stop blog']) {
        if (spoken.includes(` ${command} `)) {
          stopLog(textBefore(command));
          return;
        }
      }
      if (ended) {
        state.committedText = fullText();
        state.partialText = '';
      }
    } else {
      state.liveText = fullText();
      if (ended) {
        state.committedText = fullText();
        state.partialText = '';
      }
    }
    render();
  }

  function textBefore(command) {
    const text = fullText();
    const index = text.toLowerCase().lastIndexOf(command);
    return index < 0 ? text : text.slice(0, index).trim();
  }

  function startLog() {
    if (state.mode !== 'listening') return;
    try {
      pipeline.beginCapture(true);
      clearSpeech();
      pipeline.restartRecognition();
      set({ mode: 'recording', status: 'Recording. Say "stop log" when finished.' });
    } catch (error) {
      set({ status: `Error: ${error.message}` });
    }
  }

  async function stopLog(spokenText) {
    if (state.mode !== 'recording') return;

    const { frames, audio } = pipeline.endCapture();
    const features = makeFeatures(frames, words(spokenText).length);
    const reading = analyze(spokenText, features, state.baseline);

    clearSpeech();
    pipeline.restartRecognition();
    set({ mode: 'listening', status: 'Saving log...' });

    const date = new Date();
    const log = {
      id: `${date.getTime()}-${Math.random().toString(36).slice(2, 8)}`,
      date: date.toISOString(),
      transcript: spokenText,
      reading,
      audio: await audio,
    };
    state.logs.unshift(log);
    renderLogs();
    try {
      await store.put(log);
      if (state.mode === 'listening') set({ status: 'Saved. Listening for "start log".' });
    } catch (error) {
      set({ status: 'This log could not be saved, so it will disappear when the page closes.' });
    }
    render();
  }

  async function deleteLog(id) {
    state.logs = state.logs.filter((log) => log.id !== id);
    renderLogs();
    render();
    try {
      await store.remove(id);
    } catch (error) {
      set({ status: 'That log could not be deleted from this browser.' });
    }
  }

  function startBaseline() {
    if (state.mode !== 'listening') return;
    pipeline.beginCapture(false);
    clearSpeech();
    pipeline.restartRecognition();
    set({ mode: 'calibrating', status: 'Read the sentence calmly for 20 seconds.' });
    state.calibrationTimer = setTimeout(finishBaseline, 20000);
  }

  function finishBaseline() {
    if (state.mode !== 'calibrating') return;
    clearTimeout(state.calibrationTimer);

    const { frames } = pipeline.endCapture();
    const features = makeFeatures(frames, words(fullText()).length);

    clearSpeech();
    pipeline.restartRecognition();

    if (features.speechSeconds < 5 || features.pitchHz <= 0) {
      set({ mode: 'listening', status: 'Not enough clear speech. Please try the baseline again.' });
      return;
    }
    saveBaseline(features);
    set({ mode: 'listening', baseline: features, status: 'Baseline saved. Listening.' });
  }

  function releasePlayer() {
    if (state.player) {
      state.player.onended = null;
      state.player.onerror = null;
      state.player.pause();
    }
    if (state.playerUrl) URL.revokeObjectURL(state.playerUrl);
    state.player = null;
    state.playerUrl = null;
  }

  function play(log) {
    if (!['idle', 'listening', 'playing'].includes(state.mode)) {
      set({ status: 'Finish the current recording first.' });
      return;
    }
    if (!log.audio) {
      set({ status: 'This log has no saved audio.' });
      return;
    }

    // Listening pauses so the app does not hear its own playback.
    if (state.mode === 'listening') {
      state.resumeAfterPlayback = true;
      pipeline.stop();
      clearSpeech();
    } else if (state.mode === 'idle') {
      state.resumeAfterPlayback = false;
    }

    releasePlayer();
    state.playerUrl = URL.createObjectURL(log.audio);
    state.player = new Audio(state.playerUrl);
    state.player.onended = finishPlayback;
    state.player.onerror = () => finishPlayback('Could not play this log.');
    state.player.play().catch(() => finishPlayback('Could not play this log.'));
    set({ mode: 'playing', status: 'Playing log' });
  }

  async function finishPlayback(message) {
    if (state.mode !== 'playing') return;
    releasePlayer();
    set({ mode: 'idle', status: typeof message === 'string' ? message : 'Tap Start Listening' });
    if (state.resumeAfterPlayback) {
      state.resumeAfterPlayback = false;
      await startListening();
    }
  }

  function gmtText(iso, forFile) {
    const text = new Date(iso).toISOString().slice(0, 19).replace('T', forFile ? '_' : ' ');
    return forFile ? text.replace(/:/g, '-') : `${text} GMT`;
  }

  function downloadLog(log) {
    const extension = log.audio.type.includes('mp4') ? 'm4a' : 'webm';
    const link = document.createElement('a');
    link.href = URL.createObjectURL(log.audio);
    link.download = `Log_${gmtText(log.date, true)}_GMT.${extension}`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(link.href), 10000);
  }

  // MARK: Screen

  const BUTTON_TITLES = {
    idle: 'START LOG',
    listening: 'START LOG',
    recording: 'STOP LOG',
    calibrating: 'FINISH BASELINE',
    playing: 'STOP PLAYBACK',
  };

  function element(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined) node.textContent = text;
    return node;
  }

  function render() {
    const { mode } = state;
    document.body.dataset.mode = mode;
    $('status').textContent = state.status;
    $('main-button').textContent = BUTTON_TITLES[mode];
    $('start-listening').hidden = mode !== 'idle';
    $('stop-listening').hidden = mode !== 'listening';
    $('baseline-button').textContent = state.baseline ? 'Redo Calm Baseline' : 'Set Calm Baseline';
    $('baseline-button').disabled = mode !== 'listening';
    $('baseline-script').hidden = mode !== 'calibrating';
    $('command-hint').hidden = mode === 'calibrating';

    const live = $('live-speech');
    live.textContent = state.liveText || 'Words you say appear here';
    live.classList.toggle('placeholder', !state.liveText);
    renderMoodCard();
  }

  function renderMoodCard() {
    const card = $('mood-card');
    const latest = state.logs[0];
    card.hidden = !latest;
    if (!latest) return;

    const { reading } = latest;
    const mood = MOODS[reading.mood];
    card.style.setProperty('--mood', mood.color);
    $('mood-name').textContent = `${mood.emoji} ${mood.label}`;
    $('mood-confidence').textContent = `Confidence: ${reading.confidence}`;
    $('mood-support').hidden = !(reading.mood === 'lowMood' || reading.mood === 'sad');

    const bars = $('mood-bars');
    bars.replaceChildren(...reading.scores.slice(0, 3).map((score) => {
      const row = element('div', 'bar-row');
      const info = MOODS[score.mood];
      const name = element('span', 'bar-emoji', info.emoji);
      name.title = info.label;
      const track = element('div', 'bar-track');
      const fill = element('div', 'bar-fill');
      fill.style.width = `${Math.round(score.share * 100)}%`;
      fill.style.background = info.color;
      track.append(fill);
      row.append(name, track, element('span', 'bar-value', `${Math.round(score.share * 100)}%`));
      return row;
    }));
  }

  function renderLogs() {
    const list = $('log-list');
    $('log-count').textContent = String(state.logs.length).padStart(3, '0');
    $('log-empty').hidden = state.logs.length > 0;

    list.replaceChildren(...state.logs.map((log) => {
      const mood = MOODS[log.reading.mood];
      const details = element('details', 'log');
      const summary = element('summary');
      summary.append(element('span', 'log-emoji', mood.emoji));
      const heading = element('span', 'log-heading');
      heading.append(element('strong', '', mood.label), element('time', '', gmtText(log.date)));
      summary.append(heading);

      const body = element('div', 'log-body');
      body.append(element('p', '', log.transcript || '(No words heard)'));
      body.append(element('p', 'log-confidence', `Confidence: ${log.reading.confidence}`));
      const reasons = element('ul');
      log.reading.reasons.forEach((reason) => reasons.append(element('li', '', reason)));
      body.append(reasons);

      const actions = element('div', 'log-actions');
      const playButton = element('button', 'text-button', 'Play');
      playButton.type = 'button';
      playButton.addEventListener('click', () => play(log));
      const saveButton = element('button', 'text-button', 'Download audio');
      saveButton.type = 'button';
      saveButton.addEventListener('click', () => downloadLog(log));
      const deleteButton = element('button', 'text-button danger', 'Delete');
      deleteButton.type = 'button';
      deleteButton.addEventListener('click', () => deleteLog(log.id));
      if (log.audio) actions.append(playButton, saveButton);
      actions.append(deleteButton);
      body.append(actions);

      details.append(summary, body);
      return details;
    }));
  }

  async function boot() {
    pipeline.onSpeech = handleSpeech;
    pipeline.onFatal = (message) => {
      pipeline.stop();
      clearSpeech();
      set({ mode: 'idle', status: message });
    };

    $('main-button').addEventListener('click', mainButtonTapped);
    $('start-listening').addEventListener('click', startListening);
    $('stop-listening').addEventListener('click', stopListening);
    $('baseline-button').addEventListener('click', startBaseline);

    state.baseline = loadBaseline();
    render();
    renderLogs();

    if (!window.isSecureContext) {
      set({ status: 'The microphone needs a secure address. Open this site through localhost or https.' });
    }

    try {
      await store.open();
      const saved = await store.all();
      state.logs = (saved || []).sort((a, b) => b.date.localeCompare(a.date));
      renderLogs();
      render();
    } catch (error) {
      set({ status: 'This browser cannot save logs, so they will disappear when the page closes.' });
    }
  }

  boot();
})();
