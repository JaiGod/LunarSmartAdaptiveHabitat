#!/usr/bin/env python3
"""SELENE -> LUNA VR bridge.

Lets LUNA VR use SELENE (the team's face + voice wellness app, repo lunar-crew-monitor) as a live sensor.
SELENE's own code is not changed: this script starts SELENE as usual and also serves its latest scores at
    http://127.0.0.1:8765/status
which LUNA VR (the PC build, or the browser version on the same computer) reads every 2 seconds.

How to use
  1. Copy this file into the SELENE folder (next to selene_app.py).
  2. Run:  python selene_bridge.py          (instead of python selene_app.py)
  3. Do a camera + voice check-in in SELENE. Within 2 s LUNA VR shows "SELENE live" and LUNA reacts.

Test without a camera or SELENE:
  python selene_bridge.py --demo              (stress drifts up and down over ~80 s)
  python selene_bridge.py --demo --stress 0.85 --fatigue 0.4 --low-mood 0.2

Privacy: only three scores (0..1) and, if present, heart rate / SpO2 leave SELENE. No images or audio.
The server only listens on this computer (127.0.0.1).
"""
import argparse, json, math, sys, threading, time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PORT = 8765
latest = {"connected": False, "source": "SELENE"}


def norm(score):
    """SELENE indicator scores are personal-baseline z-scores (its dashboard shows (score+1)/4); map to 0..1."""
    if score is None:
        return None
    return round(max(0.0, min(1.0, (float(score) + 1.0) / 4.0)), 3)


class Handler(BaseHTTPRequestHandler):
    def _headers(self, code=200, length=0):
        self.send_response(code)
        self.send_header("Content-Length", str(length))
        self.send_header("Content-Type", "application/json")
        self.send_header("Cache-Control", "no-store")
        # the browser version of LUNA VR is served from GitHub Pages: allow it to read this local endpoint
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "*")
        self.send_header("Access-Control-Allow-Private-Network", "true")
        self.end_headers()

    def do_OPTIONS(self):
        self._headers(204)

    def do_GET(self):
        if self.path.split("?")[0] not in ("/status", "/"):
            body = b'{"error":"not found"}'; self._headers(404, len(body)); self.wfile.write(body); return
        body = json.dumps(latest).encode()
        self._headers(200, len(body))
        self.wfile.write(body)

    def log_message(self, *a):
        pass


def serve():
    srv = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    print(f"[bridge] LUNA VR link on http://127.0.0.1:{PORT}/status")


def run_demo(args):
    serve()
    t0 = time.time()
    print("[bridge] demo mode: Ctrl+C to stop")
    while True:
        t = time.time() - t0
        wave = 0.5 + 0.45 * math.sin(t / 80.0 * 2 * math.pi - math.pi / 2)
        latest.update({
            "connected": True, "source": "SELENE demo", "t": time.time(), "astronaut": "demo",
            "stress": args.stress if args.stress is not None else round(wave, 3),
            "fatigue": args.fatigue if args.fatigue is not None else round(0.25 + 0.2 * wave, 3),
            "low_mood": args.low_mood if args.low_mood is not None else round(0.15 + 0.15 * wave, 3),
            "status": "DEMO", "heart_rate": int(64 + 30 * wave), "spo2": 98,
        })
        time.sleep(1.0)


def run_selene():
    try:
        import crew_monitor.gui as gui
    except ImportError:
        sys.exit("Put selene_bridge.py in the SELENE folder (next to selene_app.py), or use --demo.")
    original = gui.SeleneApp._refresh

    def refresh_and_share(self):
        original(self)
        try:
            ctx = self.ctx
            if not ctx.aid:
                latest.update({"connected": False}); return
            ms = ctx.mental_summary()
            ps = ctx.physical.summary(ctx.aid)
            ind = ms.get("indicators", {})
            latest.update({
                "connected": bool(ind), "source": "SELENE", "t": time.time(), "astronaut": ctx.aid,
                "status": ms.get("status"), "last_checkin": ms.get("last_checkin"),
                "stress": norm(ind.get("stress", {}).get("score")),
                "fatigue": norm(ind.get("fatigue", {}).get("score")),
                "low_mood": norm(ind.get("low_mood", {}).get("score")),
                "heart_rate": ps.get("heart_rate"), "spo2": ps.get("spo2"),
            })
        except Exception as e:
            latest["error"] = str(e)

    gui.SeleneApp._refresh = refresh_and_share
    serve()
    import selene_app
    selene_app.main()


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--demo", action="store_true")
    ap.add_argument("--stress", type=float)
    ap.add_argument("--fatigue", type=float)
    ap.add_argument("--low-mood", dest="low_mood", type=float)
    a = ap.parse_args()
    run_demo(a) if a.demo else run_selene()
