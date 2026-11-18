/* Luna AI Training Hub — Project ARES
   Rule-based assistant with a feedback loop. No model, no network calls;
   everything lives in localStorage on the reader's own machine. */
(() => {
'use strict';
const $ = s => document.querySelector(s);
const RM = matchMedia('(prefers-reduced-motion: reduce)').matches;
const T0 = Date.now();
const esc = s => String(s).replace(/[&<>"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));

/* ---------- skills ---------- */
const SK = [
  ['comm',  'Communication'],
  ['crew',  'Crew support'],
  ['emerg', 'Emergency response'],
  ['hab',   'Habitat control'],
  ['res',   'Resource management'],
  ['pred',  'Predictive maintenance']
];

/* ---------- Luna's knowledge file ----------
   Short answers on purpose. Crew reading this at 3am want one or two
   plain sentences, not a paragraph. */
const KB0 = [
 {k:['oxygen','o2'],            s:'emerg', a:'Cabin oxygen sits near 21 per cent and carbon dioxide is scrubbed all the time. If oxygen drops past the caution line I tell the crew, seal the leaking module and open the reserve tanks.'},
 {k:['water','recycl','drink'], s:'res',   a:'Water is filtered and distilled back into drinking water. I watch the tank level and say something early if recovery drops below plan, not when the tank is nearly empty.'},
 {k:['dust','storm','regolith'],s:'hab',   a:'Moon dust is sharp and sticks to everything. Brush down in the airlock, check the filters after every walk, and keep suits out of the living space.'},
 {k:['radiation','flare','shield'], s:'emerg', a:'In a solar particle event I move the crew to the best shielded module and log the dose each person takes. Water tanks and stored supplies are the shielding.'},
 {k:['power','battery','energy','grid'], s:'res', a:'Solar arrays charge the batteries through the lunar day. At night I drop the things nobody needs first and never touch life support.'},
 {k:['sleep','bunk','wake','rest'], s:'crew', a:'Bunks go dim and warm at night, then slowly brighten to cool white to wake you. Each crew member gets their own timing.'},
 {k:['anxiety','anxious','stress','panic','worried'], s:'crew', a:'I compare you to your own baseline, never to the others. Tier 1 is quiet: light, sound, scent. Tier 2 is an offer you can turn down. Tier 3 tells you first, then the Chief Medical Officer.'},
 {k:['lonely','depress','sad','mood'], s:'crew', a:'For low mood I offer people first: a call home, a game, somewhere quiet. I only suggest. The crew decide.'},
 {k:['greenhouse','plant','food','crop'], s:'res', a:'The greenhouse feeds the crew and lifts the mood. I watch soil moisture, light hours and pump health and flag trouble before the plants show it.'},
 {k:['earth','signal','uplink'], s:'comm', a:'If the link to Earth drops I switch to the backup antenna, hold outgoing messages in a queue and tell the crew when it should be back.'},
 {k:['solar panel','array','inspect'], s:'pred', a:'I compare each array against the output it should be making. A sudden drop is almost always dust or a loose connector, so I book a clean or an inspection.'},
 {k:['tier','severity','triage'], s:'crew', a:'Three tiers: mild, moderate, severe, at scores of 0.35, 0.55 and 0.75. If I am not confident I lower the thresholds, so I act earlier rather than later.'},
 {k:['meteor','impact','micromet'], s:'emerg', a:'For a meteoroid risk I watch hull pressure, move people away from the exposed wall and get the patch kits out.'},
 {k:['luna','who are you','what are you'], s:'comm', a:'I am Luna, the habitat assistant. On this page I am a rule-based demonstration, not a real language model, and I learn from what you rate.'},
 {k:['medical','injury','sick','doctor','cmo'], s:'emerg', a:'For a medical event I alert the Chief Medical Officer, put the first aid steps on the nearest screen and open a link to Earth. A human makes the call, always.'},
 {k:['airlock','eva','spacewalk'], s:'hab', a:'Before a walk I check suit pressure, battery and the dust-off cycle. After it, filters and suit surfaces get checked before anyone opens an inner hatch.'}
];

/* ---------- drills ---------- */
const ICON = {
 dust:'<path d="M3 8h11a3 3 0 1 0-3-3"/><path d="M3 13h15a3 3 0 1 1-3 3"/><path d="M3 18h8"/>',
 power:'<path d="M13 3 5 14h6l-1 7 8-11h-6z"/>',
 oxy:'<path d="M5 5h9v9H5z"/><path d="M17 8c2 2 2 4 0 6"/><path d="M20 6c3.2 3 3.2 7 0 10"/>',
 comms:'<path d="M12 14v7"/><path d="M8 21h8"/><circle cx="12" cy="11" r="2.4"/><path d="M6.5 5.5a8 8 0 0 0 0 11M17.5 5.5a8 8 0 0 1 0 11"/>',
 med:'<path d="M3 12h4l2-5 3 10 2.5-6 1.5 3h5"/>',
 meteor:'<circle cx="15" cy="9" r="4.5"/><path d="M8 16 3 21M9 11 4 13M13 17l-2 5"/>'
};
const DRILLS = [
 {id:'dust', ic:'dust',  t:'Dust storm warning', s:'hab',
  steps:['Particle count on the exterior sensors is climbing.','Predicted duration 36 hours. Solar output likely down about 40 per cent.','Checking airlock seals and filter load.'],
  recs:['Hold all walks outside until it passes','Switch the base to battery priority','Run an extra filter cycle in the airlock','Tell the crew and show them the timeline']},
 {id:'power', ic:'power', t:'Power failure', s:'res',
  steps:['Main bus voltage has dropped below the limit.','Fault traced to array string 3.','Working out battery runtime at reduced load.'],
  recs:['Shed lighting and anything non-critical','Keep life support and comms powered','Move the load to the backup battery bank','Put the repair on the maintenance list']},
 {id:'oxy', ic:'oxy', t:'Oxygen leak', s:'emerg',
  steps:['Pressure in Module B falling 0.3 kilopascals a minute.','Mapping pressure to find the leak.','About 41 minutes before it reaches the caution line.'],
  recs:['Seal the Module B hatches','Move the crew into Module A','Open the reserve oxygen supply','Send the patch kit and tell the Commander']},
 {id:'comms', ic:'comms', t:'Communication loss', s:'comm',
  steps:['The link to Earth has been down for 90 seconds.','Testing the backup antenna.','14 outgoing messages held in the queue.'],
  recs:['Switch to the backup antenna','Tell the crew when it should be back','Keep logging everything locally','Retry the link every 30 seconds']},
 {id:'med', ic:'med', t:'Medical emergency', s:'crew',
  steps:['A crew member\'s vitals are outside their own baseline.','Person told first, then the Chief Medical Officer.','Pulling up the first aid checklist.'],
  recs:['Alert the Chief Medical Officer and the Commander','Put the first aid steps on the nearest screen','Open a medical link to Earth','Dim the lights and quieten the module']},
 {id:'meteor', ic:'meteor', t:'Meteoroid risk', s:'pred',
  steps:['Tracking debris on a possible intercept path.','Hull sensors armed on the north wall.','Estimated window about 12 minutes.'],
  recs:['Move the crew away from the north wall','Close the inner hatches','Get the patch and sealant kits ready','Log the pressure readings for review']}
];

const LOOP = [
 ['Crew feedback',      'You rate an answer or score a drill'],
 ['Evaluation',         'Luna checks what the rating was about'],
 ['Error detection',    'A wrong answer is found and marked down'],
 ['Model refinement',   'Weights change; a correction is stored'],
 ['Knowledge update',   'The knowledge file is rewritten'],
 ['Better next time',   'The next matching question uses it']
];

const CLUSTERS = [
 ['Safety',       'emerg', '#FC3D21'], ['Engineering', 'pred',  '#FBBF24'],
 ['Crew support', 'crew',  '#34D399'], ['Habitat',     'hab',   '#38BDF8'],
 ['Resources',    'res',   '#8be9ff'], ['Comms',       'comm',  '#8B5CF6']
];

const SUBS = [
 ['Habitat monitoring', 'hab',  92,  '%',          'Holding each module within half a degree'],
 ['Resource tracking',  'res',  78,  '%',          'Forecasting what runs out first'],
 ['Water recycling',    'res',  94,  '% recovered','Booking the next filter swap'],
 ['Radiation watch',    'emerg',12,  'µSv/h',      'Choosing which module is the shelter'],
 ['Energy grid',        'res',  6.4, 'kW',         'Moving heavy loads into the lunar day'],
 ['Sleep quarters',     'crew', 86,  '% rest',     'Tuning each bunk to its own person'],
 ['Health monitoring',  'crew', 97,  '% baseline', 'Watching baselines, never averages']
];

const ROAD = [
 ['Feedback learning',   'Luna learns from ratings and corrections. Working on this page now.',           () => S.counts.feedback > 1280],
 ['Reading photographs', 'Luna reads habitat and equipment photos. Rule-based here, not a trained model.', () => S.counts.images > 342],
 ['Predicting faults',   'Luna spots a failure coming from the trend instead of the alarm.',               () => S.skills.pred >= 70],
 ['Decision support',    'Luna proposes a decision with her reasons. A human always approves it.',         () => S.skills.emerg >= 80],
 ['Base-wide network',   'Habitats share what they learn while each crew member\'s own data stays private.', () => false]
];

/* ---------- state ---------- */
const fresh = () => ({
  skills: {comm:62, crew:58, emerg:55, hab:60, res:57, pred:50},
  counts: {feedback:1280, images:342, interactions:5416, sims:218, cycles:96},
  sess:   {fb:0, imp:0, tr:0, img:0, saf:0},
  kb: KB0.map((e,i) => ({id:i, k:e.k, a:e.a, s:e.s, w:1, taught:false})),
  mem: [], hist: [56, 56.4, 56.9, 57.1, 57.6, 58, 58.2]
});
let S;
try { S = JSON.parse(localStorage.getItem('lunaHub.v2') || 'null'); } catch(e) { S = null; }
if (!S || !S.skills || !S.kb) S = fresh();
const save = () => { try { localStorage.setItem('lunaHub.v2', JSON.stringify(S)); } catch(e) {} };
const START = Object.assign({}, S.skills);          // where Luna stood when you arrived
const avg   = () => SK.reduce((a,[k]) => a + S.skills[k], 0) / SK.length;
const clamp = v => Math.max(0, Math.min(99, v));

/* ---------- console log ---------- */
const con = $('#con'), logEl = $('#c-log');
const clock = () => {
  const s = Math.floor((Date.now() - T0) / 1000);
  return [s/3600, s/60%60, s%60].map(v => String(Math.floor(v)).padStart(2,'0')).join(':');
};
function log(tag, msg, delta) {
  const d = document.createElement('div');
  d.innerHTML = `<span class="t">${clock()}</span><span class="tag ${tag}">${tag}</span><span>${esc(msg)}</span>` +
    (delta ? `<span class="d${delta < 0 ? ' neg' : ''}">${delta > 0 ? '+' : ''}${delta.toFixed(1)}</span>` : '');
  logEl.appendChild(d);
  logEl.scrollTop = logEl.scrollHeight;
  $('#c-last').innerHTML = `<b>${tag}</b> ${esc(msg)}`;
}
$('#c-tog').onclick = () => {
  const open = con.classList.toggle('open');
  $('#c-tog').setAttribute('aria-expanded', open);
  $('#c-tog').textContent = open ? 'HIDE' : 'LOG';
  if (open) logEl.scrollTop = logEl.scrollHeight;
};

function bump(skill, amount, why) {
  const before = S.skills[skill];
  S.skills[skill] = clamp(before + amount);
  const moved = S.skills[skill] - before;
  S.hist.push(+avg().toFixed(2));
  if (S.hist.length > 40) S.hist.shift();
  S.counts.cycles++;
  if (why) log(why.tag, why.msg, moved);
  save();
  render();
}

/* ---------- counters ---------- */
const shownAt = {};
function animate(el, to) {
  const key = el.id, from = shownAt[key] ?? to;
  shownAt[key] = to;
  if (RM || from === to) { el.textContent = to.toLocaleString(); return; }
  const t0 = performance.now();
  (function frame(t) {
    const p = Math.min(1, (t - t0) / 900);
    el.textContent = Math.round(from + (to - from) * (1 - Math.pow(1 - p, 3))).toLocaleString();
    if (p < 1) requestAnimationFrame(frame);
  })(t0);
}

/* ---------- radar ---------- */
function radar() {
  const cx = 150, cy = 126, R = 86;
  const pt = (i, v) => {
    const a = -Math.PI/2 + i * Math.PI/3;
    return [cx + Math.cos(a) * R * v/100, cy + Math.sin(a) * R * v/100];
  };
  const poly = vals => vals.map((v,i) => pt(i,v).join(',')).join(' ');
  let s = '';
  for (let r = 1; r <= 4; r++) {
    s += `<polygon points="${poly(SK.map(() => r*25))}" fill="none" stroke="rgba(125,160,220,.13)" stroke-width="1"/>`;
  }
  SK.forEach((_, i) => {
    const [x,y] = pt(i, 100);
    s += `<line x1="${cx}" y1="${cy}" x2="${x}" y2="${y}" stroke="rgba(125,160,220,.13)"/>`;
  });
  s += `<polygon points="${poly(SK.map(([k]) => START[k]))}" fill="none" stroke="#5d719a" stroke-width="1.4" stroke-dasharray="4 3"/>`;
  s += `<polygon points="${poly(SK.map(([k]) => S.skills[k]))}" fill="rgba(56,189,248,.16)" stroke="#38BDF8" stroke-width="1.8"/>`;
  SK.forEach(([k], i) => {
    const [x,y] = pt(i, 100);
    const dx = x < cx - 4 ? -7 : x > cx + 4 ? 7 : 0;
    s += `<circle cx="${pt(i, S.skills[k])[0]}" cy="${pt(i, S.skills[k])[1]}" r="2.6" fill="#38BDF8"/>`;
    s += `<text x="${x + dx}" y="${y + (y < cy ? -9 : 15)}" text-anchor="${dx < 0 ? 'end' : dx > 0 ? 'start' : 'middle'}"
           font-family="JetBrains Mono, monospace" font-size="9" fill="#8da2c8">${k.toUpperCase()}</text>`;
  });
  $('#radar').innerHTML = s;
}

/* ---------- gauge + metrics ---------- */
function gauge() {
  const v = avg(), L = Math.PI * 78;
  $('#gauge').innerHTML =
    `<path d="M22 110 A78 78 0 0 1 178 110" fill="none" stroke="rgba(125,160,220,.14)" stroke-width="11" stroke-linecap="round"/>
     <path d="M22 110 A78 78 0 0 1 178 110" fill="none" stroke="${v > 75 ? '#34D399' : v > 55 ? '#38BDF8' : '#FBBF24'}"
       stroke-width="11" stroke-linecap="round" stroke-dasharray="${L}" stroke-dashoffset="${L * (1 - v/100)}"
       style="transition:stroke-dashoffset .8s cubic-bezier(.2,.8,.2,1)"/>
     <text x="100" y="100" text-anchor="middle" font-family="JetBrains Mono, monospace" font-size="34"
       font-weight="600" fill="#E6F1FF">${v.toFixed(1)}</text>
     <text x="100" y="121" text-anchor="middle" font-family="Inter, sans-serif" font-size="11" fill="#5d719a">overall training score</text>`;
  const M = [
    ['Response accuracy',  (S.skills.comm + S.skills.crew) / 2],
    ['Hazard detection',   (S.skills.emerg + S.skills.pred) / 2],
    ['Learning efficiency',Math.min(99, avg() + S.counts.cycles / 60)],
    ['Crew satisfaction',  Math.min(99, S.skills.crew + 4)],
    ['Prediction success', S.skills.pred]
  ];
  $('#metrics').innerHTML = M.map(([n,v]) => {
    v = clamp(v);
    return `<tr><td class="lab">${n}</td><td style="width:45%"><div class="meter ${v>78?'g':v>58?'':'a'}"><i style="width:${v}%"></i></div></td><td class="n">${v.toFixed(1)}%</td></tr>`;
  }).join('');
}

/* ---------- trend ---------- */
function canvas2d(c, h) {
  const d = devicePixelRatio || 1, w = c.clientWidth;
  c.width = w * d; c.height = h * d;
  const x = c.getContext('2d'); x.setTransform(d, 0, 0, d, 0, 0);
  return {x, w, h};
}
function trend() {
  const {x, w, h} = canvas2d($('#trend'), 104);
  x.clearRect(0, 0, w, h);
  const D = S.hist, mn = Math.min(...D) - .6, mx = Math.max(...D) + .6;
  const pt = (v,i) => [2 + i * (w-4) / Math.max(1, D.length-1), h - 16 - (v-mn)/(mx-mn) * (h-30)];
  x.strokeStyle = 'rgba(125,160,220,.1)';
  x.beginPath(); x.moveTo(0, h-16); x.lineTo(w, h-16); x.stroke();
  const g = x.createLinearGradient(0, 0, 0, h);
  g.addColorStop(0, 'rgba(56,189,248,.3)'); g.addColorStop(1, 'rgba(56,189,248,0)');
  x.beginPath(); D.forEach((v,i) => { const [a,b] = pt(v,i); i ? x.lineTo(a,b) : x.moveTo(a,b); });
  x.lineTo(w-2, h-16); x.lineTo(2, h-16); x.fillStyle = g; x.fill();
  x.beginPath(); D.forEach((v,i) => { const [a,b] = pt(v,i); i ? x.lineTo(a,b) : x.moveTo(a,b); });
  x.strokeStyle = '#38BDF8'; x.lineWidth = 1.8; x.stroke();
  const [a,b] = pt(D[D.length-1], D.length-1);
  x.fillStyle = '#34D399'; x.beginPath(); x.arc(a, b, 3, 0, 7); x.fill();
  x.fillStyle = '#5d719a'; x.font = '10px JetBrains Mono, monospace';
  x.fillText('training score over time', 2, 10);
}

/* ---------- render ---------- */
function render() {
  Object.entries(S.counts).forEach(([k,v]) => { const el = $('#c-' + k); if (el) animate(el, v); });
  $('#scorepill').textContent = avg().toFixed(1);
  $('#skilltb').innerHTML = SK.map(([k,n]) => {
    const v = S.skills[k], d = v - START[k];
    return `<tr><td class="lab">${n}</td>
      <td><div class="meter ${v>75?'g':v>55?'':'a'}"><i style="width:${v}%"></i></div></td>
      <td class="n">${v.toFixed(1)}</td>
      <td class="n ${d>0?'up':d<0?'down':''}">${d ? (d>0?'+':'') + d.toFixed(1) : '—'}</td></tr>`;
  }).join('');
  ['fb','imp','tr','img','saf'].forEach((k,i) => $('#d' + (i+1)).textContent = S.sess[k]);
  radar(); gauge(); trend(); knowledge(); memory(); subs(); road(); legend();
}

/* ---------- the loop animation ---------- */
$('#loopEl').innerHTML = LOOP.map(([t,d],i) =>
  `<div class="lstep"><div class="n">${i+1}</div><h4 style="margin-bottom:4px;font-size:13px">${t}</h4><p>${d}</p></div>`).join('');
let looping = false;
function runLoop() {
  if (looping) return;
  looping = true;
  const steps = [...document.querySelectorAll('.lstep')];
  let i = 0;
  (function step() {
    steps.forEach(s => s.classList.remove('on'));
    if (i < steps.length) { steps[i++].classList.add('on'); setTimeout(step, RM ? 0 : 380); }
    else looping = false;
  })();
}

/* ---------- chat ---------- */
const STOP = new Set('the a an is are was what how do does can to of in on for and or i we you me my it this that with about tell please where when which who there here why'.split(' '));
const words = s => s.toLowerCase().replace(/[^a-z0-9 ]/g,' ').split(/\s+/).filter(w => w.length > 2 && !STOP.has(w));
function match(q) {
  const lq = ' ' + q.toLowerCase().replace(/[^a-z0-9 ]/g,' ') + ' ';
  let best = null, bs = 0;
  S.kb.forEach(e => {
    let sc = 0;
    e.k.forEach(k => {
      const kk = k.trim();
      const hit = kk.length > 3 ? lq.includes(kk) : new RegExp('\\b' + kk + '\\b').test(lq);
      if (hit) sc += kk.length > 3 ? 2 : 1;
    });
    sc *= e.w;
    if (sc > bs) { bs = sc; best = e; }
  });
  return bs > 0 ? best : null;
}
const chat = $('#chat');
function bubble(cls, html) {
  const d = document.createElement('div');
  d.className = 'msg ' + cls;
  d.innerHTML = html;
  chat.appendChild(d);
  chat.scrollTop = chat.scrollHeight;
  return d;
}
function ask(q) {
  bubble('u', esc(q));
  S.counts.interactions++;
  const entry = match(q);
  const rec = {q, a: entry ? entry.a : null, r: 0, t: Date.now()};
  S.mem.push(rec);
  if (S.mem.length > 200) S.mem.shift();
  if (entry) {
    const m = bubble('l', esc(entry.a) +
      `<div class="fb"><small>Did that help?</small>
       <button class="btn sm" data-r="1">Yes</button>
       <button class="btn sm" data-r="-1">No, fix it</button></div>`);
    m.querySelectorAll('button').forEach(b => b.onclick = () => rate(m, rec, entry, +b.dataset.r));
  } else {
    const m = bubble('l', 'I do not have an answer for that. What should I say next time?<div class="fb"></div>');
    teach(m, rec, null);
  }
  save(); render();
}
function teach(m, rec, entry) {
  const f = m.querySelector('.fb');
  f.innerHTML = `<input type="text" placeholder="The answer she should give" aria-label="Correct answer" style="flex:1 1 180px">
                 <button class="btn key sm">Save it</button>`;
  const input = f.querySelector('input');
  f.querySelector('button').onclick = () => {
    const a = input.value.trim();
    if (!a) { input.focus(); return; }
    const k = words(rec.q).slice(0, 5);
    if (!k.length) k.push(rec.q.toLowerCase().slice(0, 12));
    S.kb.push({id: Date.now(), k, a, s: entry ? entry.s : 'comm', w: 1.5, taught: true});
    if (entry) entry.w = Math.max(.3, entry.w - .3);
    rec.a = a; rec.r = -1; rec.fixed = true;
    S.counts.feedback++; S.sess.fb++; S.sess.imp++; S.sess.tr++;
    runLoop();
    f.innerHTML = '<small>Saved. Ask again and she will use your answer.</small>';
    bump(entry ? entry.s : 'comm', 1.4, {tag: 'teach', msg: `new answer stored for "${k.slice(0,2).join(' ')}"`});
  };
  input.focus();
}
function rate(m, rec, entry, r) {
  rec.r = r;
  S.counts.feedback++; S.sess.fb++; S.sess.tr++;
  runLoop();
  if (r > 0) {
    entry.w = Math.min(3, entry.w + .2);
    S.sess.imp++;
    m.querySelector('.fb').innerHTML = '<small>Noted. She will lean on that answer more.</small>';
    bump(entry.s, 1.2, {tag: 'rate', msg: `"${entry.k[0]}" answer confirmed`});
  } else {
    S.sess.saf++;
    m.querySelector('.fb').innerHTML = '';
    teach(m, rec, entry);
    bump(entry.s, .3, {tag: 'rate', msg: `"${entry.k[0]}" answer marked wrong`});
  }
}
$('#send').onclick = () => { const v = $('#q').value.trim(); if (v) { $('#q').value = ''; ask(v); } };
$('#q').addEventListener('keydown', e => { if (e.key === 'Enter') $('#send').click(); });
function knowledge() {
  $('#kn').textContent = S.kb.length;
  $('#kt').textContent = S.kb.filter(e => e.taught).length;
  $('#kw').textContent = (S.kb.reduce((a,e) => a + e.w, 0) / S.kb.length).toFixed(2);
  $('#kbtop').innerHTML = [...S.kb].sort((a,b) => b.w - a.w).slice(0, 5)
    .map(e => `<tr><td class="lab">${esc(e.k[0])}${e.taught ? ' <span class="pill ok">yours</span>' : ''}</td><td class="n">${e.w.toFixed(1)}</td></tr>`).join('');
}

/* ---------- photographs ---------- */
const drop = $('#drop'), file = $('#file');
['dragenter','dragover'].forEach(ev => drop.addEventListener(ev, e => { e.preventDefault(); drop.classList.add('h'); }));
['dragleave','drop'].forEach(ev => drop.addEventListener(ev, e => { e.preventDefault(); drop.classList.remove('h'); }));
drop.addEventListener('drop', e => { if (e.dataTransfer.files[0]) look(e.dataTransfer.files[0]); });
file.onchange = () => { if (file.files[0]) look(file.files[0]); };
const PARTS = {
  habitat:   ['Pressurised module','Airlock','Dome section','Connecting tunnel'],
  equipment: ['Life-support unit','Pump and valve assembly','Cable run','Storage rack'],
  rover:     ['Rover chassis','Wheel assembly','Antenna mast','Regolith surface'],
  solar:     ['Solar array','Mounting frame','Junction box','Cable harness']
};
function look(f) {
  const url = URL.createObjectURL(f);
  $('#prev').src = url; $('#prev').style.display = 'block';
  const im = new Image();
  im.onload = () => {
    const c = $('#tmp'), x = c.getContext('2d');
    x.drawImage(im, 0, 0, 48, 48);
    const d = x.getImageData(0, 0, 48, 48).data;
    let br = 0, sat = 0, edge = 0;
    for (let i = 0; i < d.length; i += 4) {
      const r = d[i], g = d[i+1], b = d[i+2];
      br += (r+g+b)/3;
      sat += Math.max(r,g,b) - Math.min(r,g,b);
      if (i >= 4) edge += Math.abs(d[i] - d[i-4]);
    }
    const n = d.length / 4;
    br /= n; sat /= n; edge /= n;
    const cat = $('#cat').value, pool = PARTS[cat];
    const seen = [...new Set([pool[Math.floor(br) % pool.length], pool[Math.floor(edge*7) % pool.length]])];
    const wear = Math.min(100, Math.round(edge*3.2 + (br < 60 ? 20 : 0) + (sat < 25 ? 12 : 0)));
    const cond = 100 - wear;
    const verdict = cond > 78 ? ['Good','ok'] : cond > 55 ? ['Fair, keep an eye on it','warn'] : ['Needs inspection','bad'];
    const advice = cat === 'solar' ? (cond > 78 ? 'Check the output figure at the next cycle.' : 'Clean the panel and check the string connectors.')
      : cat === 'rover' ? (cond > 78 ? 'Routine wheel and antenna check.' : 'Look at wheel wear and dust in the joints.')
      : cat === 'equipment' ? (cond > 78 ? 'Keep it on the normal maintenance schedule.' : 'Check the seals, connectors and wear marks.')
      : (cond > 78 ? 'Carry on with the usual hull and seal checks.' : 'Inspect the seals and surface for dust or damage.');
    const conf = Math.round(Math.min(96, 52 + avg()/3 + Math.min(18, edge*2) + Math.min(8, S.skills.pred/12)));
    S.counts.images++; S.sess.img++; S.sess.saf++; S.sess.tr++;
    $('#vout').innerHTML = `<h3>What Luna sees <span class="pill ${verdict[1]}">${verdict[0]}</span></h3>
      <div class="tags" style="margin-bottom:14px">${seen.map(o => `<span class="pill">${o}</span>`).join('')}</div>
      <table class="readout"><tbody>
        <tr><td class="lab">Condition</td><td><div class="meter ${cond>78?'g':cond>55?'a':'r'}"><i style="width:${cond}%"></i></div></td><td class="n">${cond}/100</td></tr>
        <tr><td class="lab">Confidence</td><td><div class="meter"><i style="width:${conf}%"></i></div></td><td class="n">${conf}%</td></tr>
      </tbody></table>
      <p style="font-size:13.5px;color:var(--mut);margin:14px 0 0">${advice}</p>
      <div class="fb" style="display:flex;gap:7px;align-items:center;margin-top:14px">
        <small style="color:var(--dim);font-size:12px">Was that right?</small>
        <button class="btn sm" data-r="1">Yes</button><button class="btn sm" data-r="-1">No</button></div>
      <p class="note" style="margin-top:14px">Measured: brightness ${br.toFixed(0)}, colour spread ${sat.toFixed(0)}, edge detail ${edge.toFixed(1)}.</p>`;
    $('#vout').querySelectorAll('[data-r]').forEach(b => b.onclick = () => {
      S.counts.feedback++; S.sess.fb++;
      runLoop();
      const good = +b.dataset.r > 0;
      if (good) S.sess.imp++;
      $('#vout .fb').innerHTML = '<small style="color:var(--dim);font-size:12px">Thanks — that is logged.</small>';
      bump('pred', good ? 1.1 : .3, {tag: 'vision', msg: good ? 'photo reading confirmed' : 'photo reading marked wrong'});
    });
    log('vision', `${cat} photo classified, confidence ${conf}%`);
    save(); render();
  };
  im.src = url;
}

/* ---------- memory ---------- */
function memory() {
  const q = $('#ms').value.toLowerCase();
  const items = S.mem.filter(m => !q || (m.q + ' ' + (m.a || '')).toLowerCase().includes(q)).slice().reverse();
  $('#memlist').innerHTML = items.length
    ? items.slice(0, 40).map(m => `<div class="it"><q>${esc(m.q)}</q>
        <span>${esc((m.a || 'no answer yet').slice(0, 130))}</span>
        <div style="margin-top:6px"><span class="pill ${m.r>0?'ok':m.r<0?'warn':''}">${m.r>0?'confirmed':m.r<0?(m.fixed?'you corrected it':'marked wrong'):'not rated'}</span></div></div>`).join('')
    : '<p class="note">Nothing yet. Ask her something in the section above.</p>';
  memnet();
}
$('#ms').oninput = memory;
function memnet() {
  const {x, w, h} = canvas2d($('#memnet'), 250);
  x.clearRect(0, 0, w, h);
  const recent = S.mem.slice(-14);
  if (!recent.length) {
    x.fillStyle = '#5d719a'; x.font = '12px Inter, sans-serif';
    x.fillText('Questions appear here once you have asked a few.', 10, 24);
    return;
  }
  const P = recent.map((m,i) => {
    const a = i / recent.length * 6.283;
    return {x: w/2 + Math.cos(a) * (w/2 - 30), y: h/2 + Math.sin(a) * (h/2 - 24), w: new Set(words(m.q)), r: m.r};
  });
  for (let i = 0; i < P.length; i++) for (let j = i+1; j < P.length; j++) {
    let shared = 0;
    P[i].w.forEach(v => { if (P[j].w.has(v)) shared++; });
    if (shared) {
      x.strokeStyle = `rgba(56,189,248,${.2 + .18*shared})`;
      x.lineWidth = 1;
      x.beginPath(); x.moveTo(P[i].x, P[i].y); x.lineTo(P[j].x, P[j].y); x.stroke();
    }
  }
  P.forEach(p => {
    x.fillStyle = p.r > 0 ? '#34D399' : p.r < 0 ? '#FBBF24' : '#38BDF8';
    x.beginPath(); x.arc(p.x, p.y, 5, 0, 7); x.fill();
  });
}
$('#clearmem').onclick = () => {
  if (!confirm('Erase everything Luna has learned on this device?')) return;
  localStorage.removeItem('lunaHub.v2');
  S = fresh();
  Object.assign(START, S.skills);
  chat.innerHTML = '';
  greet();
  log('boot', 'training data erased, Luna reset to her starting knowledge file');
  save(); render();
};

/* ---------- drills ---------- */
$('#drillgrid').innerHTML = DRILLS.map(d =>
  `<button data-id="${d.id}"><svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#38BDF8" stroke-width="1.5" stroke-linecap="round">${ICON[d.ic]}</svg>${d.t}</button>`).join('');
let running = false, current = null;
$('#drillgrid').addEventListener('click', e => {
  const b = e.target.closest('button[data-id]');
  if (b && !running) {
    document.querySelectorAll('#drillgrid button').forEach(x => x.classList.toggle('on', x === b));
    drill(DRILLS.find(d => d.id === b.dataset.id));
  }
});
function drill(d) {
  running = true; current = d;
  $('#drillstate').textContent = 'running'; $('#drillstate').className = 'pill warn';
  $('#stars').hidden = true;
  const out = $('#drillout');
  out.textContent = '';
  const lines = [`<span class="hl">▸ ${d.t.toUpperCase()}</span>`, '', 'WHAT I SEE',
    ...d.steps.map(s => '  ' + s), '', 'WHAT I SUGGEST',
    ...d.recs.map((r,i) => `  ${i+1}. ${r}`), '',
    'The crew decide. Score how useful that was and I will learn from it.'];
  let i = 0;
  (function next() {
    if (i < lines.length) { out.innerHTML += lines[i++] + '\n'; setTimeout(next, RM ? 0 : 230); }
    else { running = false; stars(); }
  })();
  log('drill', `${d.t} drill started`);
}
function stars() {
  const el = $('#stars');
  el.hidden = false;
  el.innerHTML = '<small style="color:var(--dim);font-size:12px;margin-right:8px">Rate the plan</small>' +
    [1,2,3,4,5].map(n => `<button data-n="${n}" aria-label="${n} out of 5">
      <svg width="20" height="20" viewBox="0 0 24 24" stroke-width="1.5"><path d="m12 3 2.7 5.8 6.3.8-4.6 4.4 1.2 6.2-5.6-3.1-5.6 3.1 1.2-6.2L3 9.6l6.3-.8z"/></svg></button>`).join('');
  const btns = [...el.querySelectorAll('button')];
  btns.forEach(b => {
    b.onmouseenter = () => btns.forEach(x => x.classList.toggle('on', +x.dataset.n <= +b.dataset.n));
    b.onclick = () => {
      const n = +b.dataset.n;
      S.counts.sims++; S.counts.feedback++;
      S.sess.fb++; S.sess.tr++; S.sess.saf++;
      if (n >= 4) S.sess.imp++;
      runLoop();
      el.hidden = true;
      $('#drillstate').textContent = 'scored'; $('#drillstate').className = 'pill ok';
      const skillName = SK.find(([k]) => k === current.s)[1];
      $('#drillout').innerHTML += `\n<span class="hl">Scored ${n} of 5 — ${skillName} ${n >= 3 ? 'improved' : 'adjusted down'}.</span>\n`;
      bump(current.s, (n - 2.5), {tag: 'drill', msg: `${current.t} scored ${n}/5`});
    };
  });
  el.onmouseleave = () => btns.forEach(x => x.classList.remove('on'));
}

/* ---------- knowledge map ---------- */
const bc = $('#brainc');
let nodes = [], edges = [], ry = .6, rx = .28, dragging = false, lx = 0, ly = 0, BW, BH, ctx;
CLUSTERS.forEach(([name, skill, col], ci) => {
  const a = ci / 6 * 6.283, cx = Math.cos(a) * 165, cz = Math.sin(a) * 165, cy = Math.sin(ci * 1.7) * 48;
  for (let i = 0; i < 9; i++)
    nodes.push({x: cx + (Math.random()-.5)*105, y: cy + (Math.random()-.5)*105, z: cz + (Math.random()-.5)*105, c: ci, col, s: skill});
});
for (let i = 0; i < nodes.length; i++) for (let j = i+1; j < nodes.length; j++) {
  const a = nodes[i], b = nodes[j];
  const dist = Math.hypot(a.x-b.x, a.y-b.y, a.z-b.z);
  if ((a.c === b.c && dist < 108) || (a.c !== b.c && dist < 92)) edges.push([i, j, Math.random()]);
}
function sizeBrain() { const r = canvas2d(bc, 440); ctx = r.x; BW = r.w; BH = r.h; }
sizeBrain();
addEventListener('resize', () => { sizeBrain(); trend(); memnet(); });
bc.addEventListener('pointerdown', e => { dragging = true; lx = e.clientX; ly = e.clientY; bc.setPointerCapture(e.pointerId); bc.style.cursor = 'grabbing'; });
bc.addEventListener('pointerup',   () => { dragging = false; bc.style.cursor = 'grab'; });
bc.addEventListener('pointermove', e => {
  if (!dragging) return;
  ry += (e.clientX - lx) * .008;
  rx = Math.max(-1.1, Math.min(1.1, rx + (e.clientY - ly) * .008));
  lx = e.clientX; ly = e.clientY;
});
function project(n) {
  let x = n.x*Math.cos(ry) - n.z*Math.sin(ry), z = n.x*Math.sin(ry) + n.z*Math.cos(ry);
  const y = n.y*Math.cos(rx) - z*Math.sin(rx);
  z = n.y*Math.sin(rx) + z*Math.cos(rx);
  const f = 540 / (540 + z + 250);
  return {x: BW/2 + x*f*1.75, y: BH/2 + y*f*1.55, f, z};
}
function brain(t) {
  if (!dragging && !RM) ry += .0028;
  ctx.clearRect(0, 0, BW, BH);
  const P = nodes.map(project);
  edges.forEach(([i,j,r]) => {
    const lvl = S.skills[nodes[i].s] / 100;
    if (r > lvl * 1.05) return;
    ctx.strokeStyle = `rgba(56,189,248,${.06 + .2*lvl*((P[i].f+P[j].f)/2)})`;
    ctx.lineWidth = .8;
    ctx.beginPath(); ctx.moveTo(P[i].x, P[i].y); ctx.lineTo(P[j].x, P[j].y); ctx.stroke();
  });
  P.map((_,i) => i).sort((a,b) => P[b].z - P[a].z).forEach(i => {
    const n = nodes[i], p = P[i], lvl = S.skills[n.s] / 100;
    ctx.globalAlpha = Math.min(1, (.22 + lvl*.9) * (.78 + .22*Math.sin(t/520 + i)));
    ctx.fillStyle = n.col; ctx.shadowColor = n.col; ctx.shadowBlur = 5 + lvl*15;
    ctx.beginPath(); ctx.arc(p.x, p.y, (2 + lvl*3)*p.f*1.6, 0, 7); ctx.fill();
  });
  ctx.globalAlpha = 1; ctx.shadowBlur = 0;
  ctx.font = '11px JetBrains Mono, monospace';
  ctx.textAlign = 'center'; ctx.fillStyle = 'rgba(230,241,255,.8)';
  CLUSTERS.forEach(([name], ci) => {
    const g = nodes.filter(n => n.c === ci);
    const p = project({x: g.reduce((a,n)=>a+n.x,0)/9, y: g.reduce((a,n)=>a+n.y,0)/9 - 72, z: g.reduce((a,n)=>a+n.z,0)/9});
    ctx.fillText(name, p.x, p.y);
  });
  requestAnimationFrame(brain);
}
requestAnimationFrame(brain);
function legend() {
  $('#legend').innerHTML = CLUSTERS.map(([n,s,c]) =>
    `<span class="pill" style="border-color:${c}55;color:${c}">${n} ${S.skills[s].toFixed(0)}%</span>`).join('');
}

/* ---------- habitat subsystems ---------- */
function subs() {
  $('#subs').innerHTML = SUBS.map(([n, s, v, u, note]) => {
    const jitter = v * (1 + (Math.random()-.5) * .03);
    const g = S.skills[s];
    return `<tr><td class="lab">${n}</td>
      <td class="n">${jitter.toFixed(v < 20 ? 1 : 0)} <span style="color:var(--dim);font-size:12px">${u}</span></td>
      <td><div class="meter ${g>75?'g':g>55?'':'a'}"><i style="width:${g}%"></i></div></td>
      <td class="lab" style="font-size:13px">${note}</td></tr>`;
  }).join('');
}
setInterval(() => { if (!document.hidden) subs(); }, 4000);

/* ---------- roadmap ---------- */
let phase = 0;
function road() {
  $('#road').innerHTML = ROAD.map(([t,d,done],i) =>
    `<button data-i="${i}" class="${done() ? 'done' : ''} ${i === phase ? 'on' : ''}">
      <div class="n">${done() ? '✓' : i+1}</div><h4>${t}</h4><p>${t === ROAD[i][0] ? '' : ''}</p></button>`).join('');
  document.querySelectorAll('#road button').forEach(b => b.onclick = () => { phase = +b.dataset.i; road(); });
  const [t,d,done] = ROAD[phase];
  $('#pdesc').innerHTML = `<h3>${t} <span class="pill ${done() ? 'ok' : ''}">${done() ? 'this page does it' : 'planned'}</span></h3>
    <p style="color:var(--mut);margin-top:8px">${d}</p>`;
}

/* ---------- go ---------- */
function greet() {
  bubble('l', 'I am Luna. Ask me about oxygen, dust, power, sleep or how I handle anxiety — then tell me whether the answer was any good.');
}
greet();
log('boot', `console ready — ${S.kb.length} answers, training score ${avg().toFixed(1)}`);
render();
})();
