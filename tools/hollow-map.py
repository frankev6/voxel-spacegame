#!/usr/bin/env python3
"""DEV-025: draw the Hollow's map from Logs/hollow-map/{rock.png,overlay.json} (written by Game.Editor.Content.HollowMap.Export,
tools/hollow-map.sh runs both). No Play needed.

  python3 tools/hollow-map.py [--out Logs/hollow-map/map.png] [--focus=x,z,r] [--scale 1] [--only guides,routes,...] [--hide lights,...]

Layers: grid, guides (the wall strips in their station's colour; unguided ones dim), paths (Maw to each station), routes (traffic),
junctions, lights, beacons, signs, guns, moored, patrols, stations, marks (entrance, race portal). Coordinates are the Hollow's own
(HollowLayout): x east, z north, metres; the grid is labelled every 100 m.
"""
import argparse
import json
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
LAYERS = ["grid", "paths", "routes", "guides", "junctions", "lights", "beacons", "signs", "guns", "moored", "patrols", "stations", "marks"]


def font(size):
    for f in ("Assets/Game/UI/Theme/Fonts/Inter/FNT_Inter-SemiBold.ttf", "Assets/Game/UI/Theme/Fonts/Inter/Inter-SemiBold.ttf"):
        p = os.path.join(ROOT, f)
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    try:
        return ImageFont.load_default(size)
    except TypeError:
        return ImageFont.load_default()


def rgb(hexstr, a=255):
    return (int(hexstr[0:2], 16), int(hexstr[2:4], 16), int(hexstr[4:6], 16), a)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dir", default=os.path.join(ROOT, "Logs", "hollow-map"))
    ap.add_argument("--out")
    ap.add_argument("--focus", help="x,z,r: crop to a square of radius r metres round (x, z)")
    ap.add_argument("--scale", type=float, help="pixels per pixel of the 2 m raster (default 1.5, or to ~1400 px with --focus)")
    ap.add_argument("--only", help="comma list of layers to draw")
    ap.add_argument("--hide", help="comma list of layers to leave out")
    a = ap.parse_args()
    with open(os.path.join(a.dir, "overlay.json")) as f:
        M = json.load(f)
    rock = Image.open(os.path.join(a.dir, "rock.png")).convert("RGB")
    layers = set(a.only.split(",")) if a.only else set(LAYERS)
    if a.hide:
        layers -= set(a.hide.split(","))
    unknown = layers - set(LAYERS)
    if unknown:
        sys.exit("unknown layers: " + ", ".join(sorted(unknown)) + "; known: " + ", ".join(LAYERS))
    cell, x0, z0, H = M["cell"], M["x0"], M["z0"], M["h"]
    # crop and scale
    left, top, right, bottom = 0, 0, M["w"], H
    if a.focus:
        fx, fz, fr = (float(v) for v in a.focus.split(","))
        left, right = int((fx - fr - x0) / cell), int((fx + fr - x0) / cell)
        top, bottom = int(H - (fz + fr - z0) / cell), int(H - (fz - fr - z0) / cell)
    s = a.scale or (1.5 if not a.focus else max(1.0, 1400 / max(1, right - left)))
    img = rock.crop((left, top, right, bottom)).resize((int((right - left) * s), int((bottom - top) * s)), Image.NEAREST).convert("RGBA")
    over = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    small, mid, big = font(max(10, int(9 + s))), font(max(12, int(12 + s * 1.5))), font(max(14, int(15 + s * 2)))
    lw = max(1, int(round(s)))

    def P(x, z):
        return (((x - x0) / cell - left) * s, ((H - (z - z0) / cell) - top) * s)

    def label(q, text, f, fill, anchor="lm"):
        d.text(q, text, font=f, fill=fill, anchor=anchor, stroke_width=max(2, lw), stroke_fill=(0, 0, 0, 220))

    if "grid" in layers:
        g0x, g1x = math.floor((x0 + left * cell) / 100) * 100, (x0 + right * cell)
        g1z, g0z = z0 + (H - top) * cell, math.floor((z0 + (H - bottom) * cell) / 100) * 100
        x = g0x
        while x <= g1x:
            q = P(x, 0)[0]
            d.line([(q, 0), (q, img.height)], fill=(255, 255, 255, 60 if x % 500 else 110), width=1)
            label((q + 3, 12), str(int(x)), small, (220, 220, 220, 200))
            x += 100
        z = g0z
        while z <= g1z:
            q = P(0, z)[1]
            d.line([(0, q), (img.width, q)], fill=(255, 255, 255, 60 if z % 500 else 110), width=1)
            label((3, q - 8), str(int(z)), small, (220, 220, 220, 200))
            z += 100
    if "paths" in layers:
        for p in M["paths"]:
            d.line([P(*q) for q in p["points"]], fill=rgb(p["colour"], 70), width=max(2, 3 * lw))
    if "routes" in layers:
        for r in M["traffic"]:
            pts = [P(*q) for q in r["points"]] + ([P(*r["points"][0])] if r["loop"] else [])
            for i in range(0, len(pts) - 1, 2):                    # dashed, so they never read as guides
                d.line([pts[i], pts[i + 1]], fill=(255, 255, 255, 120), width=lw)
    if "guides" in layers:
        for n in M["neons"]:
            (ax, az), (bx, bz) = n["ab"]
            if n["guides"]:
                d.line([P(ax, az), P(bx, bz)], fill=rgb(n["colour"]), width=max(2, 2 * lw))
            else:
                d.line([P(ax, az), P(bx, bz)], fill=rgb(n["colour"], 70), width=lw)
    if "junctions" in layers:
        for j in M["junctions"]:
            q = P(j["x"], j["z"]); r = 3 * lw
            d.rectangle([q[0] - r, q[1] - r, q[0] + r, q[1] + r], outline=(255, 255, 255, 230), width=lw)
    if "lights" in layers:
        for l in M["lights"]:
            q = P(l["x"], l["z"]); r = max(1.5, 1.2 * s)
            d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], fill=rgb(l["colour"], 200))
    if "beacons" in layers:
        for b in M["beacons"]:
            q = P(b["x"], b["z"]); r = 4 * lw
            d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], outline=rgb(b["colour"]), width=lw)
    if "moored" in layers:
        for m in M["moored"]:
            ln = {"ship_nocturne_wraith": 20.0, "ship_nocturne_vesper": 45.0}.get(m["ship"], 50.0)
            u = (math.sin(m["yaw"]), math.cos(m["yaw"])); v = (u[1], -u[0])
            d.polygon([P(m["x"] + u[0] * i * ln / 2 + v[0] * j * ln / 6, m["z"] + u[1] * i * ln / 2 + v[1] * j * ln / 6)
                       for i, j in ((1, 1), (1, -1), (-1, -1), (-1, 1))], outline=(255, 226, 120, 230))
    if "patrols" in layers:
        for g in M["patrols"]:
            q = P(g["x"], g["z"]); r = g["r"] / cell * s
            d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], outline=(255, 122, 47, 170), width=lw)
    if "guns" in layers:
        for g in M["guns"]:
            q = P(g["x"], g["z"]); r = (5 if g["big"] else 3.5) * lw
            d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], fill=(255, 60, 40, 255), outline=(0, 0, 0, 255))
            d.line([q, (q[0] + math.sin(g["yaw"]) * 12 * lw, q[1] - math.cos(g["yaw"]) * 12 * lw)], fill=(255, 60, 40, 255), width=lw + 1)
    if "signs" in layers:
        for t in M["signs"]:
            label(P(t["x"], t["z"]), t["text"], small, rgb(t["colour"], 230), "mm")
    if "stations" in layers:
        for st in M["stations"]:
            q = P(st["x"], st["z"]); r = 9 / cell * s * 2
            d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], outline=rgb(st["colour"]), width=max(2, lw))
            label((q[0] + r + 4, q[1]), st["title"], mid, rgb(st["colour"]))
    if "marks" in layers:
        for m in M["marks"]:
            q = P(m["x"], m["z"]); u = (math.sin(m["yaw"]), -math.cos(m["yaw"]))
            if "width" in m:                                       # a gate: a bar across its width, facing its yaw
                hw = m["width"] / 2 / cell * s; v = (u[1], -u[0])
                d.line([(q[0] - v[0] * hw, q[1] - v[1] * hw), (q[0] + v[0] * hw, q[1] + v[1] * hw)], fill=(255, 176, 0, 255), width=max(3, 2 * lw))
            r = 7 * lw
            d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], outline=(255, 255, 255, 255), width=max(2, lw))
            d.line([q, (q[0] + u[0] * 20 * lw, q[1] + u[1] * 20 * lw)], fill=(255, 255, 255, 255), width=max(2, lw))
            label((q[0] + r + 4, q[1] + 14 * lw), m["text"], mid, (255, 255, 255, 255))
    img = Image.alpha_composite(img, over)
    # legend and scale bar
    d = ImageDraw.Draw(img)
    bar = 100 / cell * s
    d.rectangle([12, img.height - 26, 12 + bar, img.height - 20], fill=(255, 255, 255, 255))
    d.text((16 + bar, img.height - 30), "100 m", font=small, fill=(255, 255, 255, 255))
    lines = ["THE HOLLOW at the flight plane, north up, Hollow metres" + (f", round {a.focus}" if a.focus else ""),
             "rock: walls light, inside brown; tunnels blue, caverns brown (darker = deeper floor); space black",
             "guides in their station's colour (dim: guide nowhere); traffic dashed; paths faint; junctions squares;",
             "guns red (tick: facing); berths yellow; patrols orange; gates amber bar"]
    lh = small.size + 4 if hasattr(small, "size") else 14
    box_w = max(d.textlength(t, font=small) for t in lines) + 16
    d.rectangle([0, img.height - 34 - lh * len(lines), box_w, img.height - 30], fill=(0, 0, 0, 190))
    for i, t in enumerate(lines):
        d.text((8, img.height - 30 - lh * (len(lines) - i)), t, font=small, fill=(230, 230, 230, 255))
    out = a.out or os.path.join(a.dir, "map.png" if not a.focus else "map_" + a.focus.replace(",", "_").replace("-", "m") + ".png")
    img.convert("RGB").save(out, optimize=True)
    print(out, f"{img.width} x {img.height}")


if __name__ == "__main__":
    main()
