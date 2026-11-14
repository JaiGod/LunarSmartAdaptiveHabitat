/* Project ARES device layer: makes every page usable on phones, tablets, PCs, VR headsets and TVs.
   - Touch screens and VR laser pointers (pointer: coarse): tap targets of at least 44 px, thicker sliders, bigger checkboxes.
   - TV remotes and keyboards: a bright gold focus ring so you can always see what is selected.
   - Readability: no text smaller than 11 px.
   - Big screens (4K TVs reporting 2400+ CSS px): the page scales up so it can be read from the sofa.
   - Game pages on phones/tablets: an honest note that the game needs a keyboard + mouse or a VR headset. */
(function () {
  var d = document, root = d.documentElement;
  var css = [
    ':focus-visible{outline:3px solid #ffc94d !important;outline-offset:3px !important;border-radius:4px}',
    '@media (pointer:coarse){',
    '  nav a,header a,.top a,.brand,a.btn,a.pill,.btn,button,select,[role=button],.seg button,.quick button,.tools button,.views button{min-height:44px}',
    '  nav a,header a,.top a,.brand,a.pill{display:inline-flex;align-items:center}',
    '  .tools button{padding:0 6px !important}',
    '  p a,li a,td a,.meta a,.wrap>a,main>a{display:inline-block;padding:12px 6px;margin:-12px -6px}',
    '  input[type=range]{min-height:36px;touch-action:pan-y}',
    '  input[type=checkbox],input[type=radio]{width:22px !important;height:22px !important}',
    '  select,input[type=text],input:not([type]){font-size:16px}',            /* stops iOS zooming into fields */
    '}',
    '@media (hover:none){.only-hover{display:none}}',
    '.ares-game-note{position:fixed;left:12px;right:12px;bottom:12px;z-index:99999;background:#141a21;color:#e2e8ee;border:1px solid #ffc94d;border-radius:8px;padding:12px 14px;font:15px/1.45 system-ui,sans-serif;box-shadow:0 8px 30px rgba(0,0,0,.5)}',
    '.ares-game-note b{color:#ffd76e}.ares-game-note button{float:right;margin-left:10px;background:#ffc94d;color:#1a1200;border:0;border-radius:4px;font-weight:700;padding:6px 12px;cursor:pointer}'
  ].join('\n');
  var st = d.createElement('style'); st.id = 'ares-device'; st.textContent = css; (d.head || root).appendChild(st);

  function textFloor() {
    var all = d.body ? d.body.getElementsByTagName('*') : [];
    for (var i = 0; i < all.length; i++) {
      var e = all[i];
      if (e.closest && e.closest('svg,canvas,pre,code,.no-floor')) continue;
      var fs = parseFloat(getComputedStyle(e).fontSize);
      if (fs >= 11 || !fs) continue;
      for (var n = e.firstChild; n; n = n.nextSibling) {
        if (n.nodeType === 3 && n.textContent.trim().length > 1) { e.style.fontSize = '11px'; break; }
      }
    }
  }
  function hasGL() { return !!d.querySelector('canvas,model-viewer,iframe,video,#unity-container'); }
  function tvScale() {
    // 4K TVs and huge monitors: enlarge plain text pages only. Any page with a canvas, 3D viewer, video or
    // embedded game is left alone, because CSS zoom breaks WebGL sizing and pointer picking.
    if (root.dataset.tv === 'off' || hasGL()) { root.style.zoom = ''; return; }
    var z = innerWidth >= 2400 ? Math.min(2.2, innerWidth / 1600) : 1;
    root.style.zoom = z === 1 ? '' : String(z.toFixed(2));
  }
  function gameNote() {
    var p = location.pathname;
    if (!/\/(play|play2|luna|vr)\/(index\.html)?$/.test(p)) return;
    if (!matchMedia('(pointer:coarse)').matches || innerWidth > 1300) return;
    if (/OculusBrowser|Quest|Pico/i.test(navigator.userAgent)) return;   // VR headsets: the game runs there
    try { if (sessionStorage.getItem('ares-game-note')) return; } catch (e) {}
    var n = d.createElement('div'); n.className = 'ares-game-note'; n.setAttribute('role', 'note');
    n.innerHTML = '<button type="button">OK</button><b>Heads up:</b> this game is built for a keyboard and mouse, or a VR headset. On a phone or tablet it may load slowly and some controls will not work. Everything else on the site works here.';
    n.querySelector('button').onclick = function () { n.remove(); try { sessionStorage.setItem('ares-game-note', '1'); } catch (e) {} };
    d.body.appendChild(n);
  }
  function run() { textFloor(); tvScale(); gameNote(); setTimeout(textFloor, 2500); }
  addEventListener('load', tvScale);
  if (d.readyState === 'loading') d.addEventListener('DOMContentLoaded', run); else run();
  var t; addEventListener('resize', function () { clearTimeout(t); t = setTimeout(tvScale, 200); });
})();
