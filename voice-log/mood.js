// Mood estimate and voice measurements, ported from voice.swift.
// The result is an estimate for a school prototype. It is NOT a medical or psychological diagnosis.

const MoodCore = (() => {
  'use strict';

  const MOODS = {
    happy: { label: 'Happy', emoji: '😀', color: 'var(--mood-happy)' },
    calm: { label: 'Calm', emoji: '😌', color: 'var(--mood-calm)' },
    sad: { label: 'Sad', emoji: '😢', color: 'var(--mood-sad)' },
    lowMood: { label: 'Low Mood (possible depression signs)', emoji: '😞', color: 'var(--mood-low)' },
    anxious: { label: 'Anxious', emoji: '😰', color: 'var(--mood-anxious)' },
    worried: { label: 'Worried', emoji: '😟', color: 'var(--mood-worried)' },
    angry: { label: 'Angry', emoji: '😠', color: 'var(--mood-angry)' },
    tired: { label: 'Tired', emoji: '😴', color: 'var(--mood-tired)' },
  };
  const MOOD_KEYS = Object.keys(MOODS);

  // Words and short phrases linked to each mood. Add more to improve it.
  const KEYWORDS = {
    happy: ['happy', 'great', 'excited', 'wonderful', 'amazing', 'glad', 'joy', 'love this',
      'fantastic', 'proud', 'grateful', 'good day'],
    calm: ['calm', 'relaxed', 'peaceful', 'fine', 'steady', 'okay', 'comfortable'],
    sad: ['sad', 'crying', 'cry', 'miss home', 'miss my family', 'lonely', 'homesick', 'upset',
      'heartbroken', 'alone'],
    lowMood: ['depressed', 'hopeless', 'empty', 'worthless', 'numb', 'no energy', 'pointless',
      "can't go on", 'nothing matters', "don't care anymore", 'give up'],
    anxious: ['anxious', 'panic', 'panicking', 'nervous', 'scared', 'afraid', 'terrified',
      'heart racing', "can't breathe", 'overwhelmed', 'on edge'],
    worried: ['worried', 'worry', 'worrying', 'concerned', 'unsure', 'what if', 'not sure',
      'problem', 'trouble', 'uneasy'],
    angry: ['angry', 'furious', 'mad', 'annoyed', 'frustrated', 'irritated', 'hate', 'fed up',
      'unacceptable', 'sick of'],
    tired: ['tired', 'exhausted', 'sleepy', 'fatigued', 'drained', 'worn out', "can't sleep",
      'no sleep', 'need rest'],
  };

  // Stands in for Apple's NaturalLanguage sentiment score, which browsers do not have.
  const POSITIVE = new Set(['good', 'great', 'happy', 'love', 'wonderful', 'amazing', 'excited',
    'fantastic', 'glad', 'proud', 'grateful', 'nice', 'beautiful', 'best', 'enjoy', 'enjoyed',
    'fun', 'success', 'successful', 'perfect', 'awesome', 'better', 'hope', 'calm', 'peaceful',
    'smooth', 'safe', 'thankful', 'joy', 'pleased', 'delighted']);
  const NEGATIVE = new Set(['bad', 'terrible', 'awful', 'sad', 'hate', 'angry', 'worst', 'horrible',
    'scared', 'afraid', 'lonely', 'depressed', 'hopeless', 'worried', 'anxious', 'failure',
    'failed', 'fail', 'broken', 'pain', 'hurt', 'tired', 'exhausted', 'upset', 'problem',
    'trouble', 'miserable', 'cry', 'crying', 'frustrated', 'furious', 'worthless', 'dangerous',
    'wrong', 'sick', 'annoyed', 'nervous', 'panic', 'empty']);
  const NEGATORS = new Set(['not', 'never', 'no', "isn't", "wasn't", "aren't", "don't", "didn't",
    "won't", 'without']);

  const rise = (value, low, high) => Math.min(Math.max((value - low) / (high - low), 0), 1);
  const relative = (value, base) => (value > 0 && base > 0 ? value / base - 1 : 0);
  const sum = (list, pick) => list.reduce((total, item) => total + pick(item), 0);

  function words(text) {
    return (text.toLowerCase().replace(/’/g, "'").match(/[\p{L}']+/gu) || []);
  }

  // Counts a phrase, skipping cases such as "not happy" or "never really happy".
  function matches(phrase, spoken) {
    if (!phrase.length || spoken.length < phrase.length) return 0;
    let count = 0;
    for (let start = 0; start <= spoken.length - phrase.length; start++) {
      if (!phrase.every((word, i) => spoken[start + i] === word)) continue;
      const before = spoken.slice(Math.max(0, start - 2), start);
      if (!before.some((word) => NEGATORS.has(word))) count++;
    }
    return count;
  }

  function sentimentScore(spoken) {
    let positive = 0;
    let negative = 0;
    spoken.forEach((word, i) => {
      const negated = spoken.slice(Math.max(0, i - 2), i).some((w) => NEGATORS.has(w));
      if (POSITIVE.has(word)) negated ? negative++ : positive++;
      else if (NEGATIVE.has(word)) negated ? positive++ : negative++;
    });
    return (positive - negative) / (positive + negative + 1);
  }

  function emptyFeatures() {
    return { energyDB: 0, pitchHz: 0, pitchVariation: 0, wordsPerMinute: 0, pauseRatio: 0, speechSeconds: 0 };
  }

  function makeFeatures(frames, wordCount) {
    if (!frames.length) return emptyFeatures();

    // Background noise is estimated from the quietest moments.
    const sorted = frames.map((f) => f.db).sort((a, b) => a - b);
    const noiseFloor = sorted[Math.floor(sorted.length / 10)];
    const speech = frames.filter((f) => f.db > noiseFloor + 8);
    if (!speech.length) return emptyFeatures();

    const totalSeconds = sum(frames, (f) => f.seconds);
    const speechSeconds = sum(speech, (f) => f.seconds);
    const pitches = speech.map((f) => f.pitch).filter((p) => p !== null);

    const result = emptyFeatures();
    result.energyDB = sum(speech, (f) => f.db) / speech.length;
    result.speechSeconds = speechSeconds;
    result.pauseRatio = 1 - speechSeconds / Math.max(totalSeconds, 0.001);
    result.wordsPerMinute = speechSeconds > 0 ? (wordCount / speechSeconds) * 60 : 0;

    if (pitches.length >= 3) {
      const mean = sum(pitches, (p) => p) / pitches.length;
      const squares = sum(pitches, (p) => (p - mean) * (p - mean));
      result.pitchHz = mean;
      result.pitchVariation = Math.sqrt(squares / pitches.length) / mean;
    }
    return result;
  }

  function analyze(transcript, features, baseline) {
    const points = {};
    MOOD_KEYS.forEach((key) => { points[key] = 0; });
    points.calm = 0.6;

    const reasons = [];
    const spoken = words(transcript);
    let keywordHits = 0;

    // 1. What was said
    MOOD_KEYS.forEach((key) => {
      const found = [];
      let hits = 0;
      KEYWORDS[key].forEach((phrase) => {
        const count = matches(words(phrase), spoken);
        if (count > 0) {
          hits += count;
          found.push(phrase);
        }
      });
      if (hits > 0) {
        points[key] += Math.min(hits * 1.5, 4.5);
        keywordHits += hits;
        reasons.push(`${MOODS[key].label} words: ${found.join(', ')}`);
      }
    });

    const sentiment = sentimentScore(spoken);
    const positive = rise(sentiment, 0.1, 0.6);
    const negative = rise(-sentiment, 0.1, 0.6);
    points.happy += positive * 2;
    points.calm += positive * 0.5;
    points.sad += negative * 0.8;
    points.lowMood += negative * 0.6;
    points.worried += negative * 0.6;
    points.anxious += negative * 0.5;
    points.angry += negative * 0.5;
    if (sentiment > 0.25) reasons.push('Overall wording sounds positive');
    if (sentiment < -0.25) reasons.push('Overall wording sounds negative');

    // 2. How it sounded, compared with this person's calm voice
    let usedBaseline = false;
    if (baseline && features.speechSeconds >= 2 && baseline.pitchHz > 0) {
      usedBaseline = true;
      const loud = features.energyDB - baseline.energyDB;
      const pitch = relative(features.pitchHz, baseline.pitchHz);
      const variation = relative(features.pitchVariation, baseline.pitchVariation);
      const rate = relative(features.wordsPerMinute, baseline.wordsPerMinute);
      const pause = features.pauseRatio - baseline.pauseRatio;

      const excited = rise(pitch, 0.03, 0.15) * rise(variation, 0.05, 0.35);
      points.happy += excited * (0.6 + positive) * 1.5;
      points.angry += (rise(loud, 2, 8) * 1.5 + rise(rate, 0.05, 0.25) * 0.7) * (0.5 + negative);
      points.anxious += (rise(pitch, 0.04, 0.18) * 1.2 + rise(rate, 0.05, 0.25)) * (0.6 + negative * 0.4);
      points.worried += rise(pitch, 0.02, 0.1) * 0.8 + rise(pause, 0.03, 0.15) * 0.8;
      points.sad += rise(-pitch, 0.03, 0.12) + rise(-loud, 1.5, 6);
      points.lowMood += rise(-variation, 0.1, 0.4) * 1.2 + rise(-loud, 1.5, 6) * 0.8
        + rise(pause, 0.05, 0.2) * 0.8 + rise(-rate, 0.08, 0.25) * 0.6;
      points.tired += (rise(-rate, 0.1, 0.3) + rise(-loud, 2, 7) * 0.8) * (1 - negative * 0.5);

      const change = Math.max(Math.abs(pitch) / 0.1, Math.abs(loud) / 4, Math.abs(rate) / 0.2);
      points.calm += (1 - Math.min(change, 1)) * 1.2;

      if (Math.abs(loud) >= 3) {
        reasons.push(`Voice ${Math.abs(loud).toFixed(0)} dB ${loud > 0 ? 'louder' : 'quieter'} than baseline`);
      }
      if (Math.abs(pitch) >= 0.05) {
        reasons.push(`Pitch ${Math.trunc(Math.abs(pitch) * 100)}% ${pitch > 0 ? 'higher' : 'lower'} than baseline`);
      }
      if (Math.abs(rate) >= 0.1) {
        reasons.push(`Speaking ${Math.trunc(Math.abs(rate) * 100)}% ${rate > 0 ? 'faster' : 'slower'} than baseline`);
      }
      if (variation <= -0.2) reasons.push('Voice flatter than baseline');
      if (pause >= 0.08) reasons.push('More pauses than baseline');
    } else {
      reasons.push('No voice baseline used, so this is based mostly on words');
    }

    // 3. Pick the strongest result
    const ranked = MOOD_KEYS.map((key) => ({ mood: key, value: points[key] }))
      .sort((a, b) => b.value - a.value);
    const total = Math.max(sum(ranked, (r) => r.value), 0.001);
    const margin = ranked[0].value - ranked[1].value;

    let confidence = 'Low';
    if (margin >= 1.5 && usedBaseline && keywordHits > 0) confidence = 'High';
    else if (margin >= 0.6 && (usedBaseline || keywordHits > 0)) confidence = 'Medium';

    if (spoken.length < 3) reasons.push('Very few words were heard');

    return {
      mood: ranked[0].mood,
      confidence,
      scores: ranked.map((r) => ({ mood: r.mood, share: r.value / total })),
      reasons,
      usedBaseline,
    };
  }

  function rms(samples) {
    let squares = 0;
    for (let i = 0; i < samples.length; i++) squares += samples[i] * samples[i];
    return Math.sqrt(squares / samples.length);
  }

  // Finds the voice pitch using autocorrelation, between 75 and 400 Hz.
  function detectPitch(samples, sampleRate) {
    const step = Math.max(1, Math.trunc(sampleRate / 12000));
    const reduced = [];
    for (let position = 0; position + step <= samples.length; position += step) {
      let mean = 0;
      for (let i = 0; i < step; i++) mean += samples[position + i];
      reduced.push(mean / step);
    }

    const rate = sampleRate / step;
    const minLag = Math.trunc(rate / 400);
    const maxLag = Math.trunc(rate / 75);
    if (minLag <= 1 || reduced.length <= maxLag * 2) return null;

    const correlation = new Array(maxLag + 2).fill(0);
    for (let lag = minLag - 1; lag <= maxLag + 1; lag++) {
      let both = 0;
      let first = 0;
      let second = 0;
      for (let i = 0; i < reduced.length - lag; i++) {
        both += reduced[i] * reduced[i + lag];
        first += reduced[i] * reduced[i];
        second += reduced[i + lag] * reduced[i + lag];
      }
      const divisor = Math.sqrt(first * second);
      correlation[lag] = divisor > 0 ? both / divisor : 0;
    }

    let best = 0;
    for (let lag = minLag; lag <= maxLag; lag++) {
      if (correlation[lag] > best) best = correlation[lag];
    }
    if (best <= 0.55) return null;

    // The first strong peak avoids reading double the real period.
    for (let lag = minLag; lag <= maxLag; lag++) {
      const value = correlation[lag];
      if (value >= best * 0.9 && value > correlation[lag - 1] && value >= correlation[lag + 1]) {
        return rate / lag;
      }
    }
    return null;
  }

  return { MOODS, words, analyze, makeFeatures, detectPitch, rms };
})();

if (typeof module !== 'undefined') module.exports = MoodCore;
