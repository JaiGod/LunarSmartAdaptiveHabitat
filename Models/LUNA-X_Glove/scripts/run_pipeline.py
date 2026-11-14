"""Rebuilds the LUNA-X glove file from scratch: model -> animation pack -> layer view.
Only ever writes LUNA-X_Wellness_Glove.blend in this Glove folder."""
import subprocess, sys, os
here = os.path.dirname(os.path.abspath(__file__))
gd = os.path.dirname(here)
bl = sys.argv[1]
target = os.path.join(gd, "LUNA-X_Wellness_Glove.blend")
base = [bl, "--background", "--factory-startup", "-noaudio", "--python-exit-code", "1"]
steps = [
    base + ["--python", os.path.join(here, "01_build.py"), "--", gd],
    base + [target, "--python", os.path.join(here, "02_animate.py"), "--", gd, "render"],
    base + [target, "--python", os.path.join(here, "03_layers.py"), "--", gd, "render"],
]
for s in steps:
    r = subprocess.run(s)
    if r.returncode != 0:
        sys.exit(r.returncode)
print("PIPELINE DONE", flush=True)
