"""3D look-development renders for the art directions (round 2: DRYDOCK, NOCTURNE, OPERA).

Driver (system Python):  python3 ArtSource/LookDev/lookdev.py [--direction drydock] [--stage kit,scenes] [--draft]
                         python3 ArtSource/LookDev/lookdev.py --stage check     (build rules: attachment, mounts, shapes)
                         python3 ArtSource/LookDev/lookdev.py --stage r4 --direction drydock,nocturne   (round 4 ships)
Inside Blender:          blender -b --factory-startup --python lookdev.py -- --blender --direction X --stage S --out DIR

Outputs go to docs/design/lookbook/img/r2/<direction>/ (scenes as JPEG, icons as WebP). Round 1 renders stay in
img/<direction>/ (their code is at commit 1534b2e)."""

import json
import math
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
OUT_ROOT = os.path.join(ROOT, "docs", "design", "lookbook", "img", "r3")
R4_ROOT = os.path.join(ROOT, "docs", "design", "lookbook", "img", "r4")
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
sys.path.insert(0, HERE)


def arg(argv, name, default=None):
    if name in argv:
        i = argv.index(name)
        return argv[i + 1] if i + 1 < len(argv) and not argv[i + 1].startswith("--") else True
    return default


# ======================================================================================================================
# Inside Blender
# ======================================================================================================================

def blender_main(argv):
    import bpy
    from mathutils import Vector, Matrix, Euler

    import ld_mats
    import ld_world
    import ld_build
    import ld_scenes
    from ld_styles import STYLES

    direction = arg(argv, "--direction")
    stage = arg(argv, "--stage", "kit")
    out = arg(argv, "--out")
    draft = bool(arg(argv, "--draft", False))
    only = arg(argv, "--only", None)
    S = dict(STYLES[direction])
    S["_key"] = direction
    os.makedirs(out, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    ld_scenes.setup_render(S, scene, draft)
    ld_world.setup_world(S, scene)
    mats = ld_mats.make_materials(S)
    t0 = time.time()
    if stage == "kit":
        ld_scenes.stage_kit(S, scene, mats, out, only)
    elif stage == "check":
        ld_scenes.stage_check(S, only)
    elif stage == "r4":
        import ld_r4
        ld_r4.stage_r4(S, scene, mats, out, only, draft)
    else:
        ld_scenes.stage_scenes(S, scene, mats, out, only, draft)
    print(f"LOOKDEV done {direction} {stage} in {time.time() - t0:.1f}s")


# ======================================================================================================================
# Driver
# ======================================================================================================================

def run_blender(direction, stage, draft=False, only=None):
    out = os.path.join(R4_ROOT if stage == "r4" else OUT_ROOT, direction)
    cmd = [BLENDER, "-b", "--factory-startup", "--python", os.path.abspath(__file__), "--", "--blender",
           "--direction", direction, "--stage", stage, "--out", out]
    if draft:
        cmd.append("--draft")
    if only:
        cmd += ["--only", only]
    log = os.path.join(ROOT, "Logs", "tools", f"lookdev-{direction}-{stage}.log")
    os.makedirs(os.path.dirname(log), exist_ok=True)
    t0 = time.time()
    with open(log, "w") as fh:
        rc = subprocess.call(cmd, stdout=fh, stderr=subprocess.STDOUT)
    txt = open(log).read()
    ok = "LOOKDEV done" in txt
    print(f"{direction} {stage}: {'ok' if ok else 'FAILED'} rc={rc} {time.time() - t0:.0f}s  log={log}")
    if ok and stage == "kit":
        icons_to_webp(os.path.join(out, "icons"))
    if stage in ("check", "r4"):
        print("\n".join(l[6:] for l in txt.splitlines() if l.startswith("CHECK ")))
        rows = [json.loads(l[10:]) for l in txt.splitlines() if l.startswith("CHECKJSON ")]
        merge_js(os.path.join(LOOKBOOK, "buildcheck.js"), "BUILDCHECK", {("r4" if stage == "r4" else direction): rows})
    if not ok:
        tail = [l for l in txt.splitlines() if "Error" in l or "Traceback" in l or "line " in l][-25:]
        print("\n".join(tail))
    return ok


LOOKBOOK = os.path.join(ROOT, "docs", "design", "lookbook")


def merge_js(path, var, data):
    """Data files for the look book (plain JS so the page works from file://, where fetch is blocked)."""
    cur = {}
    if os.path.exists(path):
        txt = open(path).read()
        cur = json.loads(txt[txt.index("=") + 1:].strip().rstrip(";"))
    cur.update(data)
    with open(path, "w") as fh:
        fh.write(f"/* written by ArtSource/LookDev/lookdev.py */\nwindow.{var} = {json.dumps(cur, indent=1, sort_keys=True)};\n")


def write_lineups():
    data = {}
    for d in sorted(os.listdir(OUT_ROOT)):
        folder = os.path.join(OUT_ROOT, d)
        if not os.path.isdir(folder):
            continue
        for name in sorted(os.listdir(folder)):
            if name.endswith(".json"):
                data[f"{d}/{name[:-5]}"] = json.load(open(os.path.join(folder, name)))
    merge_js(os.path.join(LOOKBOOK, "lineups.js"), "LINEUPS", data)


def icons_to_webp(folder):
    """Kit icons ship as 256 px WebP (about 15 KB each instead of 150 KB PNG)."""
    from PIL import Image
    for name in os.listdir(folder):
        if name.endswith(".png"):
            src = os.path.join(folder, name)
            Image.open(src).convert("RGBA").resize((256, 256), Image.LANCZOS).save(src[:-4] + ".webp", "WEBP", quality=88, method=4)
            os.remove(src)


def driver(argv):
    from ld_styles import ORDER
    dirs = (arg(argv, "--direction") or ",".join(ORDER)).split(",")
    stages = (arg(argv, "--stage") or "kit,scenes").split(",")
    draft = bool(arg(argv, "--draft", False))
    only = arg(argv, "--only", None)
    for d in dirs:
        for s in stages:
            run_blender(d, s, draft, only)
    if "scenes" in stages:
        write_lineups()


if __name__ == "__main__":
    if "--blender" in sys.argv:
        blender_main(sys.argv[sys.argv.index("--") + 1:])
    else:
        driver(sys.argv[1:])
