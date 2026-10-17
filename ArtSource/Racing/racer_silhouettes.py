#!/usr/bin/env python3
"""RACE-003: plan-view silhouette options for the two racer ships (SPEC-92), for owner approval.

Built like the approved batch-2 ship sheets: every ship is one memorable black silhouette, built around one
module that tells its story, then the same ship in layered parts (hull, plates, frames, seams, cabins,
engines and glows, guns, radiators, hazard paint, race number) in its faction livery.

Run:  python3 ArtSource/Racing/racer_silhouettes.py
Out:  docs/design/racing/img/racer_silhouettes.png
"""
import math
import os
from PIL import Image, ImageDraw

from racing_common import (ROOT, SS, BG, PANEL, PANEL_LINE, TEXT, DIM, BOOST, F_TITLE, F_BODY, F_BOLD, F_MONO, font, wrap,
                           starfield, rot)

OUT = os.path.join(ROOT, "docs", "design", "racing", "img", "racer_silhouettes.png")
BONE, INK = (226, 221, 207), (14, 14, 12)

# Vestari: pale ceramic, cyan glass and glow (faction colour cyan); Khuchad Clan: graphite and clan orange
VESTARI = dict(base=(214, 218, 210), plate=(120, 196, 200), frame=(46, 52, 56), cab=(28, 46, 52), glass=(127, 246, 255),
               glow=(191, 246, 255), gun=(58, 64, 70), rad=(150, 158, 156), seam=(150, 154, 148), haz=(0, 200, 210),
               num=(14, 14, 12), numbg=(240, 244, 236))
KHUCHAD = dict(base=(74, 79, 87), plate=(224, 90, 30), frame=(37, 40, 45), cab=(230, 224, 210), glass=(255, 198, 128),
               glow=(255, 154, 61), gun=(28, 30, 34), rad=(58, 62, 68), seam=(40, 43, 48), haz=(232, 174, 40),
               num=(240, 236, 226), numbg=(224, 90, 30))


# ---------------------------------------------------------------- part helpers (metres, nose = +x, port = +y)
def M(pts):
    """Mirror a half outline (listed nose to tail on the port side) into a closed outline."""
    return pts + [(x, -y) for x, y in reversed(pts) if abs(y) > 1e-6]


def R(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def C(cx, cy, r, n=28):
    return [(cx + math.cos(a) * r, cy + math.sin(a) * r) for a in [i * math.tau / n for i in range(n)]]


def flipy(pts):
    return [(x, -y) for x, y in pts]


def bell(x, y, r, length=1.6, direction=-1):
    """Engine bell opening toward -x (direction -1) as a trapezoid."""
    return [(x, y - r * 0.6), (x, y + r * 0.6), (x + direction * length, y + r), (x + direction * length, y - r)]


class Ship:
    def __init__(self, pal):
        self.pal, self.parts = pal, []

    def add(self, kind, shape, **kw):
        self.parts.append((kind, shape, kw)); return self

    def bounds(self):
        xs = [p[0] for k, s, kw in self.parts if k not in ("line", "seam", "num") for p in s]
        ys = [p[1] for k, s, kw in self.parts if k not in ("line", "seam", "num") for p in s]
        return min(xs), min(ys), max(xs), max(ys)


def draw(d, ship, cx, cy, scale, mono):
    x0, y0, x1, y1 = ship.bounds()
    ox, oy = cx - (x0 + x1) / 2 * scale, cy + (y0 + y1) / 2 * scale

    def P(p):
        return (ox + p[0] * scale, oy - p[1] * scale)
    pal = ship.pal
    for kind, shape, kw in ship.parts:
        if mono:
            if kind in ("seam", "line", "num", "glow", "glass", "haz"):
                continue
            d.polygon([P(p) for p in shape], fill=INK)
            continue
        if kind in ("seam", "line"):
            d.line([P(p) for p in shape], fill=pal[kw.get("col", "seam")] + (255,), width=max(1, int(kw.get("w", 0.12) * scale)))
        elif kind == "num":
            (x, y), txt = shape[0], kw["text"]
            r = kw.get("r", 1.2) * scale
            X, Y = P((x, y))
            d.ellipse((X - r, Y - r, X + r, Y + r), fill=pal["numbg"] + (255,), outline=pal["frame"] + (255,), width=max(1, int(scale * 0.12)))
            d.text((X, Y), txt, font=font(F_BOLD, r / SS * 1.05), fill=pal["num"] + (255,), anchor="mm")
        elif kind == "glow":
            pts = [P(p) for p in shape]
            for grow, alpha in ((1.9, 40), (1.4, 80), (1.0, 255)):
                cxp = sum(p[0] for p in pts) / len(pts); cyp = sum(p[1] for p in pts) / len(pts)
                d.polygon([(cxp + (px - cxp) * grow, cyp + (py - cyp) * grow) for px, py in pts], fill=pal["glow"] + (alpha,))
        else:
            d.polygon([P(p) for p in shape], fill=pal[kind] + (255,), outline=pal["frame"] + (255,) if kind in ("base", "plate", "rad", "cab") else None)


def chevrons(ship, x0, x1, half_w, n, kind="haz"):
    step = (x1 - x0) / n
    for i in range(n):
        a = x0 + i * step
        ship.add(kind, [(a, half_w), (a + step * 0.45, half_w), (a + step * 0.95, 0), (a + step * 0.45, -half_w), (a, -half_w), (a + step * 0.5, 0)])


# ---------------------------------------------------------------- the Dart line (Vestari)
def dart_needle():
    s = Ship(VESTARI)
    s.add("base", M([(17.0, 0.12), (10.0, 0.45), (6.0, 1.3), (1.0, 2.4), (-5.0, 2.2), (-9.0, 1.5), (-12.0, 0.9)]))
    for side in (1, -1):
        py = [(0.0, 2.0), (-1.4, 2.0), (-8.6, 6.0), (-7.2, 6.4)]
        s.add("frame", py if side > 0 else flipy(py))
        pod = [(-2.5, 6.9), (-3.4, 7.8), (-11.5, 7.9), (-13.0, 7.4), (-13.0, 5.6), (-11.5, 5.1), (-3.4, 5.2)]
        s.add("base", pod if side > 0 else flipy(pod))
        s.add("plate", R(-5.5, 7.95 * side, -10.5, 7.45 * side) if side > 0 else R(-5.5, -7.45, -10.5, -7.95))
        s.add("rad", R(-6.0, 5.6 * side, -10.0, 5.2 * side) if side > 0 else R(-6.0, -5.2, -10.0, -5.6))
        for k in range(5):
            x = -6.4 - k * 0.8
            s.add("line", [(x, 5.15 * side), (x, 5.65 * side)], col="frame", w=0.12)
        s.add("frame", bell(-13.0, 6.5 * side, 0.9, 1.4))
        s.add("glow", bell(-13.3, 6.5 * side, 0.6, 0.8))
        can = [(6.4, 1.2), (3.0, 3.6), (2.4, 3.6), (3.6, 1.6)]
        s.add("plate", can if side > 0 else flipy(can))
    s.add("cab", M([(7.0, 0.0), (6.0, 0.55), (3.0, 0.8), (1.2, 0.6)]))
    s.add("glass", M([(6.8, 0.0), (5.6, 0.38), (3.6, 0.5)]))
    s.add("gun", R(10.0, 0.18, 18.4, -0.18)); s.add("glow", C(18.6, 0, 0.32, 10))
    s.add("plate", M([(-1.0, 2.1), (-4.0, 2.0), (-4.0, 1.4), (-1.0, 1.5)]))
    s.add("frame", bell(-12.0, 0.0, 0.7, 1.2)); s.add("glow", bell(-12.2, 0.0, 0.45, 0.7))
    for x in (4.5, 0.0, -3.5, -7.0):
        w = 2.3 if x > -5 else 1.6
        s.add("seam", [(x, w), (x, -w)])
    s.add("seam", [(9.8, 0.0), (-11.5, 0.0)], col="haz", w=0.16)
    s.add("num", [(-6.0, 0.0)], text="07", r=0.95)
    return s


def dart_kite():
    s = Ship(VESTARI)
    s.add("base", M([(13.5, 0.0), (9.0, 1.6), (2.0, 5.2), (-6.0, 11.6), (-8.4, 11.9), (-7.4, 7.0), (-4.4, 4.0), (-7.8, 1.6), (-8.4, 0.0)]))
    s.add("plate", M([(12.6, 0.0), (8.6, 1.3), (6.0, 2.2), (6.0, 0.0)]))
    for side in (1, -1):
        le = [(2.0, 5.2), (-6.0, 11.6), (-6.4, 10.9), (1.4, 4.7)]
        s.add("plate", le if side > 0 else flipy(le))
        for k in range(7):  # the heat sail: radiator fins along the trailing edge
            y = 4.6 + k * 0.95
            fin = [(-4.6 - k * 0.42, y), (-9.6 - k * 0.2, y + 0.15), (-9.6 - k * 0.2, y + 0.55), (-4.6 - k * 0.42, y + 0.5)]
            s.add("rad", fin if side > 0 else flipy(fin))
        gun = R(-5.8, 9.25, -0.6, 9.65)
        s.add("gun", gun if side > 0 else flipy(gun)); s.add("glow", C(-0.4, 9.45 * side, 0.24, 8))
        s.add("seam", [(1.0, 4.6 * side), (-5.5, 10.6 * side)])
    s.add("cab", M([(8.6, 0.0), (7.4, 0.9), (2.6, 1.3), (0.4, 1.0)]))
    s.add("glass", M([(8.3, 0.0), (7.1, 0.6), (4.4, 0.8)]))
    for y in (-1.25, 0.0, 1.25):
        s.add("frame", bell(-8.0, y, 0.55, 1.1)); s.add("glow", bell(-8.2, y, 0.36, 0.6))
    for x in (5.0, 1.5, -2.0, -5.0):
        s.add("seam", [(x, 1.8 if x > 0 else 3.0), (x, -(1.8 if x > 0 else 3.0))])
    s.add("seam", [(13.0, 0), (-7.6, 0)], col="haz", w=0.16)
    s.add("num", [(-3.0, 0.0)], text="22", r=1.0)
    return s


def dart_gimbal():
    s = Ship(VESTARI)
    ring_o, ring_i = C(0, 0, 7.4, 48), C(0, 0, 5.9, 48)
    for k in range(48):  # the thrust ring as 48 segments
        a0, a1 = ring_o[k], ring_o[(k + 1) % 48]; b0, b1 = ring_i[k], ring_i[(k + 1) % 48]
        s.add("base", [a0, a1, b1, b0])
    for k in range(8):
        a = k * math.tau / 8 + math.tau / 16
        c = (math.cos(a) * 7.6, math.sin(a) * 7.6)
        tang = math.degrees(a) + 90
        pod = [rot((c[0] + dx, c[1] + dy), tang, c) for dx, dy in ((-1.1, -0.55), (1.1, -0.55), (1.1, 0.55), (-1.1, 0.55))]
        s.add("plate", pod)
        b = [rot(p, tang, c) for p in bell(c[0] - 1.1, c[1], 0.5, 0.9)]
        s.add("frame", b); s.add("glow", [rot(p, tang, c) for p in bell(c[0] - 1.25, c[1], 0.32, 0.5)])
    for a in (45, 135, 225, 315):
        sp = [rot(p, a) for p in R(1.8, 0.3, 6.0, -0.3)]
        s.add("frame", sp)
    s.add("base", M([(4.4, 0.0), (3.4, 1.6), (0.0, 2.2), (-3.2, 1.7), (-4.0, 0.0)]))
    s.add("cab", M([(3.6, 0.0), (2.6, 1.0), (0.4, 1.3), (-0.6, 1.0)]))
    s.add("glass", M([(3.4, 0.0), (2.4, 0.7), (1.0, 0.85)]))
    s.add("gun", R(4.0, 0.2, 10.2, -0.2)); s.add("glow", C(10.4, 0, 0.3, 10))
    s.add("plate", M([(-1.6, 1.8), (-3.4, 1.4), (-3.4, 0.6), (-1.6, 0.9)]))
    for k in range(16):
        a = k * math.tau / 16
        s.add("seam", [(math.cos(a) * 5.95, math.sin(a) * 5.95), (math.cos(a) * 7.35, math.sin(a) * 7.35)])
    s.add("num", [(-2.3, 0.0)], text="31", r=0.85)
    return s


# ---------------------------------------------------------------- the Ram line (Khuchad Clan)
def ram_battering():
    s = Ship(KHUCHAD)
    s.add("base", M([(9.0, 4.6), (6.0, 5.4), (-6.0, 5.4), (-11.0, 4.6), (-14.0, 3.6), (-14.0, 0.0)]))
    s.add("plate", M([(22.0, 0.0), (20.0, 1.6), (15.0, 6.2), (9.4, 7.8), (8.2, 7.6), (8.2, 0.0)]))
    chevrons(s, 12.5, 19.0, 3.2, 4)
    for y in (2.2, 0.0, -2.2):  # boarding spikes
        s.add("frame", [(21.4 - abs(y) * 0.8, y + 0.45), (23.6 - abs(y) * 0.8, y), (21.4 - abs(y) * 0.8, y - 0.45)])
    s.add("gun", R(-3.0, 0.45, 23.0, -0.45)); s.add("frame", R(19.6, 0.75, 22.2, -0.75)); s.add("glow", C(23.3, 0, 0.45, 10))
    for y in (3.0, -3.0):
        s.add("frame", bell(-14.0, y, 1.5, 2.0)); s.add("glow", bell(-14.6, y, 1.0, 1.2))
    s.add("frame", bell(-14.0, 0.0, 0.8, 1.3)); s.add("glow", bell(-14.4, 0.0, 0.5, 0.8))
    # a salvaged engine pod bolted on the port side only
    s.add("base", [(-4.0, 5.4), (-4.0, 7.6), (-12.0, 7.6), (-12.0, 5.4)]); s.add("plate", R(-5.0, 7.6, -11.0, 7.1))
    s.add("frame", bell(-12.0, 6.5, 1.0, 1.4)); s.add("glow", bell(-12.4, 6.5, 0.65, 0.8))
    for x in (-5.5, -7.5, -9.5):
        s.add("line", [(x, 5.4), (x, 7.6)], col="frame", w=0.14)
    # starboard bolt-on armour, the cab offset to starboard
    for x0 in (6.0, 1.0, -4.0):
        s.add("plate", [(x0, -5.4), (x0, -6.4), (x0 - 4.4, -6.4), (x0 - 4.4, -5.4)])
        for rx in (x0 - 0.6, x0 - 3.8):
            s.add("frame", C(rx, -5.9, 0.18, 6))
    s.add("cab", R(5.0, -1.4, 1.0, -4.2)); s.add("glass", R(4.9, -1.8, 4.3, -3.8))
    s.add("rad", R(-1.0, 4.4, -6.0, 3.0)); s.add("rad", R(-7.0, 4.0, -11.0, 2.8))
    for x in (8.0, 3.0, -2.0, -7.0, -11.0):
        s.add("seam", [(x, 5.3), (x, -5.3)])
    s.add("num", [(-8.0, -2.4)], text="13", r=1.4)
    return s


def ram_tug():
    s = Ship(KHUCHAD)
    s.add("base", [(10.0, 4.2), (10.0, -4.6), (-12.0, -4.6), (-13.0, -3.0), (-13.0, 3.0), (-12.0, 4.2)])
    s.add("frame", R(10.0, 6.4, 12.4, -6.6))
    for y in (5.6, 3.2, 0.8, -1.6, -4.0, -6.0):
        s.add("cab", R(12.4, y + 0.5, 13.4, y - 0.5))  # rubber bumper blocks
    chevrons(s, 10.2, 12.2, 5.6, 1)
    # the grapple crane: offset boom on the port side, knuckle and claw past the bow
    s.add("plate", [(-6.0, 3.6), (-6.0, 6.2), (-3.0, 6.6), (-1.0, 5.6), (-1.0, 3.6)])
    s.add("plate", [(-2.0, 5.4), (15.0, 6.6), (15.4, 7.6), (-2.0, 6.6)])
    for k in range(9):
        x = -1.0 + k * 1.8
        s.add("line", [(x, 5.5 + k * 0.07), (x + 0.9, 6.55 + k * 0.07)], col="frame", w=0.14)
    s.add("frame", C(15.6, 7.1, 0.9, 14))
    s.add("plate", [(16.0, 7.6), (19.6, 9.2), (20.4, 8.6), (17.4, 7.0)])
    s.add("plate", [(16.0, 6.6), (19.6, 5.0), (20.4, 5.6), (17.4, 7.2)])
    s.add("haz", R(18.4, 9.0, 19.4, 8.5)); s.add("haz", R(18.4, 5.6, 19.4, 5.2))
    # the starboard impact driver
    s.add("base", R(-4.0, -4.6, 9.0, -6.2)); s.add("gun", R(-2.0, -5.0, 15.0, -5.8)); s.add("glow", C(15.3, -5.4, 0.42, 10))
    # cargo cage aft
    s.add("frame", R(-12.0, 3.4, -6.0, -3.8))
    for x in (-11.0, -9.5, -8.0, -6.8):
        s.add("line", [(x, 3.4), (x, -3.8)], col="rad", w=0.2)
    for y in (2.0, 0.0, -2.0):
        s.add("line", [(-12.0, y), (-6.0, y)], col="rad", w=0.2)
    s.add("cab", R(8.6, 2.0, 4.0, -2.4)); s.add("glass", R(8.5, 1.6, 7.9, -2.0))
    for y in (2.6, 0.9, -0.9, -2.6):
        s.add("frame", bell(-13.0, y, 0.7, 1.1)); s.add("glow", bell(-13.3, y, 0.45, 0.6))
    s.add("base", R(-2.0, -6.2, -7.0, -8.0)); s.add("frame", bell(-7.0, -7.1, 0.7, 1.1)); s.add("glow", bell(-7.3, -7.1, 0.45, 0.6))
    for x in (3.0, -1.0, -5.0):
        s.add("seam", [(x, 4.1), (x, -4.5)])
    s.add("num", [(1.0, 0.6)], text="04", r=1.25)
    return s


def ram_hammer():
    s = Ship(KHUCHAD)
    s.add("base", M([(9.0, 2.0), (2.0, 2.8), (-10.0, 2.8), (-13.0, 2.0), (-13.0, 0.0)]))
    s.add("frame", R(8.6, 1.6, 11.4, -1.6))
    s.add("plate", [(11.2, 11.0), (12.4, 12.2), (14.4, 11.6), (15.0, 9.4), (15.0, -9.4), (14.4, -11.6), (12.4, -12.2), (11.2, -11.0)])
    chevrons(s, 11.6, 14.6, 8.0, 2)
    for y in (6.0, 3.0, -3.0, -6.0):
        s.add("cab", R(15.0, y + 0.6, 15.8, y - 0.6))
    for y in (10.2, -10.2):  # an impact driver at each tip: shove off-centre to spin a rival
        s.add("gun", R(9.0, y + 0.35, 18.4, y - 0.35)); s.add("glow", C(18.7, y, 0.4, 10))
    s.add("frame", R(-12.0, 7.0, -17.0, -7.0))
    for y in (5.2, 1.8, -1.8, -5.2):
        s.add("base", R(-12.4, y + 1.3, -16.6, y - 1.3))
        s.add("frame", bell(-17.0, y, 1.0, 1.4)); s.add("glow", bell(-17.4, y, 0.65, 0.8))
    for y in (4.0, -4.0):
        s.add("frame", [(-9.0, y * 0.6), (-12.0, y * 1.6), (-12.0, y * 1.6 - 0.6 * (1 if y > 0 else -1)), (-9.0, y * 0.6 - 0.6 * (1 if y > 0 else -1))])
    s.add("cab", R(3.6, 2.6, -0.6, 0.2)); s.add("glass", R(3.5, 2.3, 2.9, 0.5))
    s.add("rad", R(-2.0, -0.6, -8.0, -2.4))
    for x in (6.0, 1.0, -4.0, -9.0):
        s.add("seam", [(x, 2.7), (x, -2.7)])
    s.add("num", [(-5.0, 1.2)], text="66", r=1.1)
    return s


DART = dict(
    name="DART", faction="Vestari", tag="Light racer · high tech · slick ceramic hull",
    doctrine="Vestari pilots race to prove purity of line: never touched, never hit. Their racers are stripped "
             "couriers. They turn on their cores, carry one light gun, and are so light that every hit throws them "
             "across the track.",
    counter="Countered by: the Ram's impact driver, repulsor mines, engine wash. Beats the Ram on any clean line.",
    ability="SLIP: a sideways dash (about 25 m/s) on a 6 s cooldown, to dodge a shot or a mine.",
    options=[
        ("D1  NEEDLE", dart_needle, "Spear courier · 32 m",
         "A mag-sled courier: everything hangs off one long accelerator spine, which doubles as a needle gun at the bow. "
         "Two engine pods sit on swept pylons, wide of the centre of mass, so it yaws hard and recovers fast.",
         "Weak to: side hits. The pylons are thin, and losing a pod sends it spinning.",
         dict(speed=5, grip=4, mass=1, shove=1, tough=1)),
        ("D2  KITE", dart_kite, "Heat-sail wing · 22 m long, 24 m span",
         "A flying wing whose trailing edge is a heat sail of radiator fins. It sheds boost heat faster than anything, "
         "so it can boost longer. Twin wingtip guns.",
         "Weak to: fin damage. Every fin it loses shortens its boost, and the span is a wide target.",
         dict(speed=5, grip=3, mass=2, shove=2, tough=1)),
        ("D3  GIMBAL", dart_gimbal, "Ring racer · 21 m",
         "A cockpit pod inside a ring of eight thrusters. It pushes equally in every direction, so it corners and dodges "
         "best. The silhouette is a circle, unlike anything else on the grid.",
         "Weak to: top speed (only some of the ring pushes forward) and ring hits, which knock out two thrusters at once.",
         dict(speed=3, grip=5, mass=2, shove=1, tough=2)),
    ])

RAM = dict(
    name="RAM", faction="Khuchad Clan", tag="Heavy racer · low tech · bolted-on refits",
    doctrine="The Clan races the way it fights: in a pack, behind one big ship. Its racers are refitted work and "
             "boarding hulls with armoured prows and an impact driver, a slow kinetic gun that shoves more than it "
             "hurts. They barely move when hit and knock rivals and rocks off the racing line.",
    counter="Countered by: a clean Dart, EMP mines (it drifts a long way on carry), tight hairpins. Beats the Dart in any contact.",
    ability="SHOVE: a 60 m radial repulsor pulse that pushes ships, mines and rocks away. No damage; 10 s cooldown.",
    options=[
        ("R1  BATTERING RAM", ram_battering, "Boarding ram · 38 m",
         "A boarding ram: a spiked wedge prow wider than the hull, with the impact driver's barrel run straight through it. "
         "Mismatched engines, a salvaged pod bolted on the port side, and armour plates bolted on the starboard side.",
         "Weak to: turning. The bolted pod pulls it to port, and it needs the whole track width in hairpins.",
         dict(speed=2, grip=2, mass=5, shove=4, tough=5)),
        ("R2  SALVAGE TUG", ram_tug, "Refitted tug · 34 m",
         "A salvage tug that kept its grapple crane, so it can tether to pylons, wrecks and rivals. It has a rubber "
         "bumper bow, an impact driver on the starboard side and a cargo cage aft.",
         "Weak to: the crane. It is heavy on one side, and shooting it off ends the tether.",
         dict(speed=3, grip=4, mass=4, shove=3, tough=4)),
        ("R3  HAMMERHEAD", ram_hammer, "Dock-bumper hammer · 36 m long, 24 m wide",
         "A cut-down shipyard dock bumper welded across a narrow hull, with an impact driver at each tip: fire one and "
         "the shove spins a rival off its line. Four engines sit in a box aft.",
         "Weak to: drag in the turns. The plate is as wide as a gate and the biggest target on the track.",
         dict(speed=2, grip=1, mass=5, shove=5, tough=5)),
    ])


# ---------------------------------------------------------------- sheet
def bars(d, x, y, ratings):
    for i, (k, lab) in enumerate([("speed", "Speed"), ("grip", "Cornering"), ("mass", "Mass"), ("shove", "Shove power"),
                                  ("tough", "Toughness")]):
        yy = y + i * 25 * SS
        d.text((x, yy), lab, font=font(F_BODY, 14), fill=DIM)
        for j in range(5):
            x0 = x + 112 * SS + j * 21 * SS
            d.rectangle((x0, yy + 3 * SS, x0 + 17 * SS, yy + 14 * SS), fill=BOOST if j < ratings[k] else (40, 42, 38))


def main():
    W, H = 3400, 2560
    img = Image.new("RGB", (W * SS, H * SS), BG)
    starfield(img, (0, 0, W * SS, H * SS), 5, density=1.4)
    d = ImageDraw.Draw(img, "RGBA")
    d.text((60 * SS, 46 * SS), "RACER SILHOUETTES  ·  TWO SHIPS, THREE OPTIONS EACH", font=font(F_TITLE, 36), fill=TEXT)
    d.text((60 * SS, 102 * SS), "Plan view, nose to the right, every ship at the same scale. The black shape must read on "
           "its own; the colour plan shows how it is built. Pick one per ship, or mix features.", font=font(F_BODY, 21), fill=DIM)
    SCALE = 17 * SS  # pixels per metre
    row_h, top = 1170, 160
    for r, line in enumerate((DART, RAM)):
        y0 = (top + r * (row_h + 30)) * SS
        x0 = 60 * SS
        d.rectangle((x0, y0, x0 + 560 * SS, y0 + row_h * SS), fill=PANEL, outline=PANEL_LINE, width=2 * SS)
        d.text((x0 + 30 * SS, y0 + 30 * SS), line["name"], font=font(F_TITLE, 46), fill=BOOST)
        d.text((x0 + 30 * SS, y0 + 98 * SS), line["faction"].upper(), font=font(F_BOLD, 18), fill=TEXT)
        d.text((x0 + 30 * SS, y0 + 126 * SS), line["tag"], font=font(F_BODY, 16), fill=DIM)
        yy = y0 + 172 * SS
        for head, txt in (("DOCTRINE", line["doctrine"]), ("COUNTER", line["counter"]), ("RACER ABILITY", line["ability"])):
            d.text((x0 + 30 * SS, yy), head, font=font(F_BOLD, 14), fill=DIM); yy += 24 * SS
            for chunk in wrap(txt, 54):
                d.text((x0 + 30 * SS, yy), chunk, font=font(F_BODY, 17), fill=TEXT); yy += 25 * SS
            yy += 18 * SS
        pal = VESTARI if line is DART else KHUCHAD
        d.text((x0 + 30 * SS, yy), "LIVERY", font=font(F_BOLD, 14), fill=DIM); yy += 26 * SS
        for k in ("base", "plate", "frame", "glass", "glow", "haz"):
            d.rectangle((x0 + 30 * SS, yy, x0 + 70 * SS, yy + 22 * SS), fill=pal[k])
            d.text((x0 + 82 * SS, yy + 11 * SS), {"base": "Hull", "plate": "Plates", "frame": "Frames", "glass": "Glass",
                                                     "glow": "Engine glow", "haz": "Stripe"}[k], font=font(F_BODY, 15), fill=TEXT, anchor="lm")
            yy += 30 * SS
        for i, (title, fn, size, notes, weak, ratings) in enumerate(line["options"]):
            ship = fn()
            cx0, cw = (650 + i * 910) * SS, 880 * SS
            d.rectangle((cx0, y0, cx0 + cw, y0 + row_h * SS), fill=PANEL, outline=PANEL_LINE, width=2 * SS)
            d.text((cx0 + 24 * SS, y0 + 24 * SS), title, font=font(F_TITLE, 21), fill=TEXT)
            d.text((cx0 + cw - 24 * SS, y0 + 30 * SS), size, font=font(F_BODY, 15), fill=DIM, anchor="ra")
            bx0, by0, bx1, by1 = cx0 + 20 * SS, y0 + 70 * SS, cx0 + cw - 20 * SS, y0 + 520 * SS
            d.rectangle((bx0, by0, bx1, by1), fill=BONE)
            for gx in range(int(bx0), int(bx1), int(SCALE * 5)):
                d.line([(gx, by0), (gx, by1)], fill=(210, 204, 190), width=SS)
            for gy in range(int(by0), int(by1), int(SCALE * 5)):
                d.line([(bx0, gy), (bx1, gy)], fill=(210, 204, 190), width=SS)
            draw(d, ship, (bx0 + bx1) / 2, (by0 + by1) / 2, SCALE, mono=True)
            d.text((bx0 + 12 * SS, by1 - 12 * SS), "5 m grid", font=font(F_MONO, 13), fill=(130, 126, 114), anchor="ls")
            fx0, fy0, fx1, fy1 = cx0 + 20 * SS, y0 + 535 * SS, cx0 + cw - 20 * SS, y0 + 895 * SS
            d.rectangle((fx0, fy0, fx1, fy1), fill=(8, 9, 8), outline=PANEL_LINE, width=SS)
            draw(d, ship, (fx0 + fx1) / 2, (fy0 + fy1) / 2, SCALE * 0.8, mono=False)
            ny = y0 + 912 * SS
            for chunk in wrap(notes, 66):
                d.text((cx0 + 24 * SS, ny), chunk, font=font(F_BODY, 16), fill=TEXT); ny += 23 * SS
            ny += 6 * SS
            for chunk in wrap(weak, 66):
                d.text((cx0 + 24 * SS, ny), chunk, font=font(F_BODY, 16), fill=BOOST); ny += 23 * SS
            bars(d, cx0 + cw - 270 * SS, y0 + 912 * SS, ratings)
    d.text((60 * SS, (H - 40) * SS), "RACE-003 · silhouettes and livery for approval; the voxel build (RACE-008) follows "
           "DESIGN-GUIDE blocks. Ratings are design intent, measured in RACE-008.", font=font(F_BODY, 14), fill=DIM)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.resize((W, H), Image.LANCZOS).save(OUT, optimize=True)
    print(OUT)


if __name__ == "__main__":
    main()
