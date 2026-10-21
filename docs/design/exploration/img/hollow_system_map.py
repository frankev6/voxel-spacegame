"""Top-down map of the Hollow system and its exploration slice (proposal). Writes an SVG; render it with headless Chrome:
  python3 hollow_system_map.py hollow_system_slice.svg
  "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --hide-scrollbars --window-size=2000,1250 \
      --screenshot="$PWD/hollow_system_slice.png" "file://$PWD/hollow_system_slice.svg"
Coordinates are km, x east, y north, the star at the origin. Names other than VESHA, the Hollow and the Clan are placeholders."""
import math, random, sys

out = sys.argv[1]
W, H = 2000, 1250
ACC, AMB, DNG, TXT, DIM, ICE, HAZE = "#39d5ff", "#ffc247", "#ff5c7a", "#e8eef4", "#8fa3b3", "#bfe6ff", "#e2b3ff"

# --- the system (km, star at the origin)
VESHA = (-185, -30)                                  # the Clan Tether stands over VESHA's top; the Hollow orbits 4.3 km north of it
planets = [  # name, centre, radius, kind, one line
    ("VESHA", VESHA, 80, "ocean", "ocean world · top 2.5 km below the plane"),
    ("CINDER", (60, 45), 18, "barren", "scorched rock · 4 km below"),
    ("ORRO", (200, -112), 110, "gas", "gas giant · cloud deck 6 km below"),
    ("PALE", (-70, 196), 26, "ice", "ice dwarf · 3 km below"),
]
sites = {  # what slows cruise: anything you can stop at
    "hollow": (VESHA[0] + .15, VESHA[1] + 4.3), "tether": VESHA, "picket": (VESHA[0] - 9.5, VESHA[1] - 6.5),
    "skerry": (VESHA[0] + 8.5, VESHA[1] - 9.5), "derelict": (132, 64), "colony": (60, 45), "gate": (-292, 150),
}
legs = [  # the loop between planets, as waypoints
    ("VESHA → the hauler", [sites["hollow"], (-150, 30), (-104, 70), (-20, 100), (70, 92), sites["derelict"]]),
    ("the hauler → CINDER", [sites["derelict"], (98, 60), sites["colony"]]),
    ("CINDER → VESHA", [sites["colony"], (28, -38), (-60, -70), (-130, -60), sites["hollow"]]),
    ("VESHA → the gate", [sites["hollow"], (-232, 48), (-268, 106), sites["gate"]]),
]
FLIGHT, VMAX, RAMP, SLOW = 120, 8000, .3, 1000       # m/s near anything; deep-space cap; m/s per m of clearance; m of slow zone

def sample(points, step_km=.02):
    out = [points[0]]
    for (x1, y1), (x2, y2) in zip(points, points[1:]):
        n = max(1, int(math.dist((x1, y1), (x2, y2)) / step_km))
        out += [(x1 + (x2 - x1) * i / n, y1 + (y2 - y1) * i / n) for i in range(1, n + 1)]
    return out
def seconds(points):
    pts, t = sample(points), 0.0
    for a, b in zip(pts, pts[1:]):
        d = min(math.dist(a, s) for s in sites.values()) * 1000
        v = FLIGHT if d < SLOW else min(VMAX, FLIGHT + RAMP * (d - SLOW))
        t += math.dist(a, b) * 1000 / v
    return t
def length(points): return sum(math.dist(a, b) for a, b in zip(points, points[1:]))
def fmt(s): return f"{round(s)} s" if s < 90 else f"{s/60:.1f} min"

# --- canvas
ML, MT, MW, MH = 40, 130, 1400, 1080
XMIN, XMAX = -330, 330
S = MW / (XMAX - XMIN)
YMAX = MH / S / 2
def P(x, y): return ML + (x - XMIN) * S, MT + (YMAX - y) * S
def f(v): return f"{v:.1f}"
svg = []; add = svg.append
def text(x, y, s, size=15, fill=TXT, weight=400, anchor="start", spacing=0, opacity=1, halo=True):
    st = '' if halo else ' style="stroke:none"'
    add(f'<text x="{f(x)}" y="{f(y)}" font-size="{size}" fill="{fill}" font-weight="{weight}" text-anchor="{anchor}" letter-spacing="{spacing}" opacity="{opacity}"{st}>{s}</text>')
def label(x, y, title, lines, color=TXT, anchor="start", size=16):
    text(x, y, title, size, color, 700, anchor, 1.2)
    for i, l in enumerate(lines): text(x, y + 19 + i * 17, l, 13, DIM, 400, anchor)

add(f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="Helvetica Neue, Helvetica, Arial, sans-serif">')
add('<style>text{paint-order:stroke;stroke:#060c12;stroke-width:4px;stroke-linejoin:round;stroke-opacity:.9}</style>')
defs = ['<defs>']
defs.append(f'<clipPath id="map"><rect x="{ML}" y="{MT}" width="{MW}" height="{MH}" rx="10"/></clipPath>')
defs.append(f'<marker id="arrow" viewBox="0 0 10 10" refX="7" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M0,1 L9,5 L0,9 z" fill="{AMB}"/></marker>')
defs.append(f'<marker id="arrowr" viewBox="0 0 10 10" refX="7" refY="5" markerWidth="8" markerHeight="8" orient="auto"><path d="M0,1 L9,5 L0,9 z" fill="{DNG}"/></marker>')
defs.append(f'<radialGradient id="glow"><stop offset="0" stop-color="{ACC}" stop-opacity=".6"/><stop offset="1" stop-color="{ACC}" stop-opacity="0"/></radialGradient>')
defs.append(f'<radialGradient id="redglow"><stop offset="0" stop-color="{DNG}" stop-opacity=".5"/><stop offset="1" stop-color="{DNG}" stop-opacity="0"/></radialGradient>')
defs.append('<radialGradient id="sun"><stop offset="0" stop-color="#fff6dc"/><stop offset=".08" stop-color="#ffe2a0"/><stop offset=".22" stop-color="#ff9a3c" stop-opacity=".55"/>'
            '<stop offset=".55" stop-color="#ff6a2a" stop-opacity=".12"/><stop offset="1" stop-color="#ff6a2a" stop-opacity="0"/></radialGradient>')
hx, hy = P(205, 128)
defs.append(f'<radialGradient id="hazemask" cx="{f(hx)}" cy="{f(hy)}" r="{f(118*S)}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#fff"/><stop offset=".6" stop-color="#fff" stop-opacity=".8"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>')
defs.append(f'<mask id="haze" maskUnits="userSpaceOnUse"><rect x="{ML}" y="{MT}" width="{MW}" height="{MH}" fill="url(#hazemask)"/></mask>')
defs.append(f'<filter id="neb" x="{ML}" y="{MT}" width="{MW}" height="{MH}" filterUnits="userSpaceOnUse"><feTurbulence type="fractalNoise" baseFrequency="0.006" numOctaves="5" seed="23"/>'
            '<feColorMatrix type="matrix" values="0 0 0 0 .62  0 0 0 0 .27  0 0 0 0 .70  3.2 0 0 0 -1.05"/></filter>')
defs.append(f'<filter id="neb2" x="{ML}" y="{MT}" width="{MW}" height="{MH}" filterUnits="userSpaceOnUse"><feTurbulence type="fractalNoise" baseFrequency="0.016 0.008" numOctaves="3" seed="5"/>'
            '<feColorMatrix type="matrix" values="0 0 0 0 .35  0 0 0 0 .75  0 0 0 0 .95  4 0 0 0 -2.3"/></filter>')

def planet_defs(i, name, centre, radius, kind, origin=None, fscale=1.0):
    """Clip, surface filter and shading for one planet drawn at a given px centre and px radius."""
    cx, cy, r = centre[0], centre[1], radius
    def q(k): return f"{k / r * 1e-4 * fscale:.5f}"
    pid = f"p{i}"
    defs.append(f'<clipPath id="{pid}c"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}"/></clipPath>')
    x0, y0, w = cx - r, cy - r, 2 * r
    if kind == "ocean":
        base = ["#21566a", "#163848"]
        layers = [(q(4000), "5", "11", ".33 .38 .27", "10 0 0 0 -5.7", .5), (q(9000) + " " + q(4500), "4", "4", ".92 .95 1", "3.2 0 0 0 -1.75", .25)]
    elif kind == "barren":
        base = ["#7a5a46", "#4a3427"]
        layers = [(q(2500), "4", "8", ".25 .17 .13", "6 0 0 0 -2.9", .55), (q(9000), "2", "3", ".95 .6 .35", "5 0 0 0 -3.4", .35)]
    elif kind == "gas":
        base = ["#c9a26f", "#7a5634"]
        layers = [(f"{.0007*fscale:.5f} " + q(45000), "3", "2", ".96 .86 .66", "5 0 0 0 -2.2", .6), (f"{.0011*fscale:.5f} " + q(90000), "2", "9", ".5 .3 .18", "5 0 0 0 -2.4", .55)]
    else:
        base = ["#cfe3ee", "#7e9fb5"]
        layers = [(q(3000), "4", "6", ".62 .76 .86", "5 0 0 0 -2.3", .5)]
    lid = []
    for j, (freq, oct_, seed, rgb, alpha, op) in enumerate(layers):
        r_, g_, b_ = rgb.split()
        fid = f"{pid}f{j}"
        defs.append(f'<filter id="{fid}" x="{f(x0)}" y="{f(y0)}" width="{f(w)}" height="{f(w)}" filterUnits="userSpaceOnUse"><feTurbulence type="fractalNoise" baseFrequency="{freq}" numOctaves="{oct_}" seed="{seed}"/>'
                    f'<feColorMatrix type="matrix" values="0 0 0 0 {r_}  0 0 0 0 {g_}  0 0 0 0 {b_}  {alpha}"/></filter>')
        lid.append((fid, op))
    # lit from the star: the shading centre leans toward the origin
    sx, sy = P(0, 0) if origin is None else origin
    dx, dy = sx - cx, sy - cy
    n = math.hypot(dx, dy) or 1
    lx, ly = cx + dx / n * r * .35, cy + dy / n * r * .35
    defs.append(f'<radialGradient id="{pid}b" cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{base[0]}"/><stop offset="1" stop-color="{base[1]}"/></radialGradient>')
    defs.append(f'<radialGradient id="{pid}s" cx="{f(lx)}" cy="{f(ly)}" r="{f(r * 1.35)}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#000" stop-opacity="0"/><stop offset=".55" stop-color="#000" stop-opacity=".15"/><stop offset="1" stop-color="#000" stop-opacity=".78"/></radialGradient>')
    return pid, lid

def draw_planet(pid, lid, cx, cy, r, kind):
    add(f'<g clip-path="url(#{pid}c)"><rect x="{f(cx-r)}" y="{f(cy-r)}" width="{f(2*r)}" height="{f(2*r)}" fill="url(#{pid}b)"/>')
    for fid, op in lid:
        add(f'<rect x="{f(cx-r)}" y="{f(cy-r)}" width="{f(2*r)}" height="{f(2*r)}" filter="url(#{fid})" opacity="{op}"/>')
    add(f'<rect x="{f(cx-r)}" y="{f(cy-r)}" width="{f(2*r)}" height="{f(2*r)}" fill="url(#{pid}s)"/></g>')
    if kind == "ocean":
        add(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}" fill="none" stroke="#7fd3ff" stroke-width="2.5" opacity=".55"/>')
        add(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r+5)}" fill="none" stroke="#7fd3ff" stroke-width="8" opacity=".1"/>')
    else:
        add(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}" fill="none" stroke="#ffffff" stroke-width="1" opacity=".18"/>')

# planets on the system map
plist = []
for i, (name, c, R, kind, line) in enumerate(planets):
    cx, cy = P(*c)
    plist.append((planet_defs(i, name, (cx, cy), R * S, kind), cx, cy, R * S, kind, name, line, c, R))
# the inset: VESHA's coast, tether-local km
IX, IY, IW, IH = 1470, 130, 490, 360
IXMIN, IXMAX = -17, 21
IS = IW / (IXMAX - IXMIN)
IYMAX = IH / IS / 2 + 1.5
def Q(x, y): return IX + (x - IXMIN) * IS, IY + (IYMAX - y) * IS
defs.append(f'<clipPath id="inset"><rect x="{IX}" y="{IY}" width="{IW}" height="{IH}" rx="10"/></clipPath>')
qx, qy = Q(0, 0)
ins = planet_defs(9, "VESHA-near", (qx, qy), 80 * IS, "ocean", origin=(qx + 4000, qy - 1500), fscale=13)
defs.append('</defs>')
svg += defs
add(f'<rect width="{W}" height="{H}" fill="#070b11"/>')

# --- system map
add('<g clip-path="url(#map)">')
add(f'<rect x="{ML}" y="{MT}" width="{MW}" height="{MH}" fill="#04070c"/>')
random.seed(3)
for _ in range(700):
    x, y = random.uniform(ML, ML + MW), random.uniform(MT, MT + MH)
    add(f'<circle cx="{f(x)}" cy="{f(y)}" r="{random.choice([.5,.7,.9,1.2])}" fill="#cfe3ff" opacity="{random.uniform(.2,.8):.2f}"/>')
sx, sy = P(0, 0)
add(f'<rect x="{ML}" y="{MT}" width="{MW}" height="{MH}" filter="url(#neb)" mask="url(#haze)" opacity=".85"/>')
add(f'<rect x="{ML}" y="{MT}" width="{MW}" height="{MH}" filter="url(#neb2)" mask="url(#haze)" opacity=".45"/>')
# orbits and the system edge
for (_, cx, cy, r, kind, name, line, c, R) in plist:
    add(f'<circle cx="{f(sx)}" cy="{f(sy)}" r="{f(math.hypot(*c)*S)}" fill="none" stroke="#ffffff" stroke-opacity=".07" stroke-width="1" stroke-dasharray="3 6"/>')
add(f'<circle cx="{f(sx)}" cy="{f(sy)}" r="{f(318*S)}" fill="none" stroke="{ACC}" stroke-opacity=".25" stroke-width="1.2" stroke-dasharray="14 10"/>')
# the belt
random.seed(9)
for _ in range(900):
    a = random.uniform(0, math.tau); rr = random.gauss(128, 5)
    x, y = P(rr * math.cos(a), rr * math.sin(a))
    add(f'<circle cx="{f(x)}" cy="{f(y)}" r="{random.choice([.6,.8,1.1,1.5])}" fill="#b9a690" opacity="{random.uniform(.25,.7):.2f}"/>')
# the star, far below the plane, and its heat
add(f'<circle cx="{f(sx)}" cy="{f(sy)}" r="{f(70*S)}" fill="url(#sun)"/>')
add(f'<circle cx="{f(sx)}" cy="{f(sy)}" r="{f(30*S)}" fill="#ff7a2a" fill-opacity=".06" stroke="#ff9a3c" stroke-width="1.3" stroke-dasharray="6 5" opacity=".9"/>')
for (pd, cx, cy, r, kind, name, line, c, R) in plist:
    if kind == "gas":  # a faint ring below the plane
        add(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r*1.55)}" fill="none" stroke="#d8c4a0" stroke-width="{f(r*.28)}" opacity=".08"/>')
        add(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r*1.45)}" fill="none" stroke="#e8d8b8" stroke-width="2" opacity=".18"/>')
    draw_planet(pd[0], pd[1], cx, cy, r, kind)
add('</g>')
add(f'<rect x="{ML}" y="{MT}" width="{MW}" height="{MH}" rx="10" fill="none" stroke="#24313d" stroke-width="1.5"/>')

# planet names
offs = {"VESHA": (0, 80 * S + 26, "middle"), "CINDER": (-18 * S - 6, -18 * S - 8, "end"), "ORRO": (0, 110 * S + 30, "middle"), "PALE": (-26 * S - 12, -6, "end")}
for (pd, cx, cy, r, kind, name, line, c, R) in plist:
    dx, dy, anchor = offs[name]
    text(cx + dx, cy + dy, name, 19, TXT, 700, anchor, 3)
    text(cx + dx, cy + dy + 19, line, 13, DIM, 400, anchor)
    text(cx + dx, cy + dy + 35, f"radius {R} km", 12, DIM, 400, anchor, 0, .75)
text(sx - 24, sy + 30 * S + 20, "THE STAR · far below", 13, "#ffc58a", 700, "middle", 1.5)
text(sx - 24, sy + 30 * S + 36, "heat zone: hulls burn", 12, "#e6a77a", 400, "middle")
x, y = P(-45, -122); text(x, y, "THE SCATTER", 14, "#d8c7b0", 700, "middle", 2, .9)
text(x, y + 17, "asteroid belt in the plane · Raider haunts", 12, "#bfae98", 400, "middle", 0, .9)
x, y = P(250, 230); text(x, y, "THE HAZE", 17, HAZE, 700, "middle", 3)
text(x, y + 18, "nebula · sensors and ping halved", 12.5, "#cfa6e8", 400, "middle")
x, y = P(-226, -228); text(x, y, "SYSTEM EDGE · ~320 km out · gates sit here", 12.5, ACC, 700, "middle", 1.2, .8)

# --- the loop
def smooth(points):
    pts = [P(*p) for p in points]
    d = f"M{f(pts[0][0])},{f(pts[0][1])}"
    for i in range(1, len(pts)):
        p0 = pts[i - 2] if i >= 2 else pts[i - 1]
        p1, p2 = pts[i - 1], pts[i]
        p3 = pts[i + 1] if i + 1 < len(pts) else p2
        c1 = (p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6)
        c2 = (p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6)
        d += f" C{f(c1[0])},{f(c1[1])} {f(c2[0])},{f(c2[1])} {f(p2[0])},{f(p2[1])}"
    return d
def trim(points, a_km, b_km):
    (x1, y1), (x2, y2) = points[0], points[1]
    n = math.dist(points[0], points[1]); s = [(x1 + (x2 - x1) * a_km / n, y1 + (y2 - y1) * a_km / n)]
    (x3, y3), (x4, y4) = points[-2], points[-1]
    n = math.dist(points[-2], points[-1]); e = [(x4 - (x4 - x3) * b_km / n, y4 - (y4 - y3) * b_km / n)]
    return s + points[1:-1] + e
times = []
for name, pts in legs:
    d = smooth(trim(pts, 9, 11))
    add(f'<path d="{d}" fill="none" stroke="#000" stroke-opacity=".5" stroke-width="7"/>')
    add(f'<path d="{d}" fill="none" stroke="{AMB}" stroke-width="3" stroke-dasharray="12 7" marker-end="url(#arrow)"/>')
    times.append((name, length(pts), seconds(pts)))
tags = {"VESHA → the hauler": (30, 113), "the hauler → CINDER": (98, 78), "CINDER → VESHA": (-62, -82), "VESHA → the gate": (-238, 76)}
for name, km, s in times:
    x, y = P(*tags[name]); s_ = f"{km:.0f} km · {fmt(s)}"
    wpx = len(s_) * 7.6 + 16
    add(f'<rect x="{f(x - wpx/2)}" y="{f(y - 14)}" width="{f(wpx)}" height="22" rx="11" fill="#071018" fill-opacity=".88" stroke="{AMB}" stroke-opacity=".55"/>')
    text(x, y + 2, s_, 13, AMB, 600, "middle")

# --- sites on the system map
x, y = P(*VESHA)
add(f'<circle cx="{f(x)}" cy="{f(y)}" r="16" fill="url(#glow)"/>')
add(f'<circle cx="{f(x)}" cy="{f(y)}" r="5" fill="#0b1b27" stroke="{ACC}" stroke-width="2"/>')
add(f'<rect x="{f(x-36)}" y="{f(y-30)}" width="72" height="56" rx="4" fill="none" stroke="{ACC}" stroke-opacity=".7" stroke-dasharray="4 3"/>')
text(x, y - 38, "THE HOLLOW · THE CLAN TETHER", 13.5, ACC, 700, "middle", 1)
text(x, y - 56, "see the coast, top right", 12, DIM, 400, "middle")
x, y = P(*sites["derelict"])
add(f'<circle cx="{f(x)}" cy="{f(y)}" r="26" fill="url(#redglow)"/>')
add(f'<g transform="rotate(-28 {f(x)} {f(y)})"><rect x="{f(x-13)}" y="{f(y-3)}" width="12" height="6" rx="1.5" fill="#7b5b45" stroke="#d9a27a"/><rect x="{f(x+2)}" y="{f(y-3)}" width="10" height="6" rx="1.5" fill="#6d5140" stroke="#d9a27a" transform="rotate(16 {f(x+2)} {f(y)})"/></g>')
for (ox, oy, rot) in ((15, -11, 200), (18, 9, 160), (-6, 16, 250)):
    cx, cy = x + ox, y + oy
    add(f'<path d="M{f(cx+5)},{f(cy)} L{f(cx-4)},{f(cy-4)} L{f(cx-1.5)},{f(cy)} L{f(cx-4)},{f(cy+4)} z" fill="{DNG}" transform="rotate({rot} {f(cx)} {f(cy)})"/>')
label(x + 26, y - 26, "THE LOST HAULER", ["Clan derelict on the Haze's edge", "Raider scavengers on it"], "#ffd1b0")
x, y = P(*sites["colony"])
add(f'<rect x="{f(x-5)}" y="{f(y-5)}" width="10" height="10" fill="#2b3640" stroke="#ffd7a8" stroke-width="1.6" transform="rotate(45 {f(x)} {f(y)})"/>')
label(x + 46, y + 26, "THE DEAD COLONY", ["a lift down to CINDER's surface", "the away team goes down here"], "#ffd7a8")
x, y = P(*sites["gate"])
add(f'<circle cx="{f(x)}" cy="{f(y)}" r="16" fill="none" stroke="#9fb0bf" stroke-width="4"/>')
add(f'<circle cx="{f(x)}" cy="{f(y)}" r="16" fill="none" stroke="{DNG}" stroke-width="1.4" stroke-dasharray="4 3"/>')
add(f'<rect x="{f(x-5.5)}" y="{f(y-1.5)}" width="11" height="9" rx="1.5" fill="{DNG}"/><path d="M{f(x-3.5)},{f(y-1.5)} v-3 a3.5,3.5 0 0 1 7,0 v3" fill="none" stroke="{DNG}" stroke-width="1.8"/>')
label(x + 24, y - 4, "LANE GATE · LOCKED", ["at the system edge", "opens with the clue from CINDER"])
x, y = P(258, 165)
add(f'<circle cx="{f(x)}" cy="{f(y)}" r="40" fill="url(#redglow)"/>')
add(f'<circle cx="{f(x)}" cy="{f(y)}" r="16" fill="none" stroke="{DNG}" stroke-width="2"/>')
text(x, y + 8, "?", 22, DNG, 800, "middle")
x2, y2 = P(296, 120)
add(f'<path d="M{f(x+12)},{f(y+12)} Q{f(x+50)},{f(y+40)} {f(x2)},{f(y2)}" fill="none" stroke="{DNG}" stroke-width="1.5" stroke-dasharray="6 5" marker-end="url(#arrowr)"/>')
label(x, y - 32, "UNKNOWN · LARGE · MOVING", ["the hook for the next slice"], DNG, "middle", 14)
for (c, name, line, below) in (((200, -112), "CLAN SKIMMER RIG", "later: a cloud-deck descent", 0), ((-70, 196), "LISTENING POST", "later: who is it listening to?", 26 * S + 22)):
    x, y = P(*c)
    add(f'<circle cx="{f(x)}" cy="{f(y)}" r="5" fill="#1a232c" stroke="{DIM}" stroke-width="1.6"/>')
    ly = y + below if below else y - 12
    text(x, ly, name, 12.5, "#c6d2dc", 700, "middle", 1)
    text(x, ly + (16 if below else 32), line, 12, DIM, 400, "middle")
minors = [((-120, 52), "drifting cargo pod"), ((22, 98), "a Raider pair"), ((-50, -64), "dead lifeboat"), ((-262, 92), "Clan nav beacon"), ((112, 0), "debris stream"), ((-12, -150), "a distress call")]
for (mx, my), name in minors:
    x, y = P(mx, my); col = DNG if "Raider" in name else ICE
    add(f'<rect x="{f(x-4)}" y="{f(y-4)}" width="8" height="8" transform="rotate(45 {f(x)} {f(y)})" fill="#071018" stroke="{col}" stroke-width="1.5"/>')
    text(x + 10, y + 4, name, 12, col, 500)
# step badges
badges = [(1, VESHA, (-22, 22)), (2, VESHA, (22, 22)), (3, (-104, 70), (0, 0)), (4, sites["derelict"], (6, 24)), (5, sites["colony"], (-24, -4)),
          (6, VESHA, (40, -10)), (7, sites["gate"], (-26, 20))]
for n, at, (ox, oy) in badges:
    x, y = P(*at); x += ox; y += oy
    add(f'<circle cx="{f(x)}" cy="{f(y)}" r="12" fill="{AMB}" stroke="#071018" stroke-width="2"/>')
    text(x, y + 5, str(n), 13.5, "#071018", 800, "middle", halo=False)
# scale bar
x0, y0 = ML + 26, MT + MH - 30
add(f'<rect x="{x0-12}" y="{y0-26}" width="{f(100*S+70)}" height="48" rx="8" fill="#071018" fill-opacity=".85"/>')
add(f'<line x1="{x0}" y1="{y0}" x2="{f(x0+100*S)}" y2="{y0}" stroke="{TXT}" stroke-width="2"/>')
for k in (0, 50, 100):
    xx = x0 + k * S
    add(f'<line x1="{f(xx)}" y1="{y0-6}" x2="{f(xx)}" y2="{y0+6}" stroke="{TXT}" stroke-width="2"/>')
    text(xx, y0 + 18, f"{k} km", 11.5, DIM, 500, "middle")
text(x0 + 100 * S + 34, y0 + 4, "N ↑", 13, TXT, 700, "middle")

# --- inset: VESHA's coast
add(f'<g clip-path="url(#inset)"><rect x="{IX}" y="{IY}" width="{IW}" height="{IH}" fill="#04070c"/>')
draw_planet(ins[0], ins[1], qx, qy, 80 * IS, "ocean")
def site_q(k): return Q(sites[k][0] - VESHA[0], sites[k][1] - VESHA[1])
x, y = site_q("hollow")
add(f'<circle cx="{f(x)}" cy="{f(y)}" r="40" fill="url(#glow)" opacity=".6"/>')
add(f'<ellipse cx="{f(x-8)}" cy="{f(y+2)}" rx="14" ry="10" fill="#5b5148" stroke="#a89582" stroke-width="1.2" transform="rotate(-18 {f(x)} {f(y)})"/>')
add(f'<ellipse cx="{f(x+9)}" cy="{f(y-4)}" rx="11" ry="9" fill="#625649" stroke="#a89582" stroke-width="1.2" transform="rotate(-18 {f(x)} {f(y)})"/>')
label(x + 26, y - 14, "THE HOLLOW", ["Clan haven: market, docks, crew"], TXT, "start", 15)
x, y = site_q("tether")
add(f'<circle cx="{f(x)}" cy="{f(y)}" r="22" fill="url(#glow)"/><circle cx="{f(x)}" cy="{f(y)}" r="6" fill="#0b1b27" stroke="{ACC}" stroke-width="2.2"/>')
for i in range(5):
    a = math.radians(i * 72 + 15); rr = .6 * IS
    add(f'<rect x="{f(x+rr*math.cos(a)-2.5)}" y="{f(y+rr*math.sin(a)-1.2)}" width="5" height="2.4" fill="#8b98a3"/>')
label(x + 22, y + 22, "THE CLAN TETHER", ["dockmaster · 5 wrecks", "the lift down to VESHA"], TXT, "start", 15)
x, y = site_q("picket")
add(f'<rect x="{f(x-9)}" y="{f(y-9)}" width="18" height="18" fill="#2b3640" stroke="#9fb0bf" stroke-width="1.3" transform="rotate(12 {f(x)} {f(y)})"/>')
for a in (57, 147, 237, 327):
    add(f'<circle cx="{f(x+14*math.cos(math.radians(a)))}" cy="{f(y+14*math.sin(math.radians(a)))}" r="2.6" fill="{DNG}"/>')
label(x, y - 40, "PICKET 9", ["dead picket, guns still live"], TXT, "middle", 14)
x, y = site_q("skerry")
add(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(1.1*IS)}" fill="#a9c6d6" stroke="#e6f6ff" stroke-width="1.2"/>')
label(x, y - 40, "SKERRY", ["ice moonlet: fuel, cover"], TXT, "middle", 14)
# routes leaving the coast
x, y = site_q("hollow")
add(f'<path d="M{f(x+6)},{f(y-18)} Q{f(x+60)},{f(y-110)} {f(IX+IW-30)},{f(IY+26)}" fill="none" stroke="{AMB}" stroke-width="2.5" stroke-dasharray="10 6" marker-end="url(#arrow)"/>')
text(IX + IW - 24, IY + 50, "to the hauler", 12.5, AMB, 600, "end")
add(f'<path d="M{f(x-14)},{f(y-12)} Q{f(x-80)},{f(y-80)} {f(IX+22)},{f(IY+30)}" fill="none" stroke="{AMB}" stroke-width="2.5" stroke-dasharray="10 6" marker-end="url(#arrow)"/>')
text(IX + 22, IY + 54, "to the gate", 12.5, AMB, 600, "start")
# inset scale
x0, y0 = IX + 18, IY + IH - 22
add(f'<line x1="{x0}" y1="{y0}" x2="{f(x0+5*IS)}" y2="{y0}" stroke="{TXT}" stroke-width="2"/>')
text(x0 + 5 * IS + 8, y0 + 4, "5 km", 11.5, DIM, 500)
add('</g>')
add(f'<rect x="{IX}" y="{IY}" width="{IW}" height="{IH}" rx="10" fill="none" stroke="{ACC}" stroke-opacity=".5" stroke-width="1.5"/>')
text(IX + IW - 14, IY + IH - 16, f"VESHA'S COAST · zoomed {IS / S:.0f}×", 13, ACC, 700, "end", 1.2)

# --- header
text(ML, 58, "THE HOLLOW SYSTEM · EXPLORATION SLICE", 30, TXT, 700, "start", 2)
text(ML, 90, "Proposal for approval · top-down, north up · four planets lie below the flight plane, each at its own depth: their disks are regions you fly over, and their coasts are the sites in the plane above them.", 15.5, DIM)
x, y = ML + MW, 58
add(f'<line x1="{f(x-330)}" y1="{y-6}" x2="{f(x-300)}" y2="{y-6}" stroke="{AMB}" stroke-width="3" stroke-dasharray="8 5"/>')
text(x - 292, y - 1, "the loop", 13.5, AMB, 600)
add(f'<rect x="{f(x-212)}" y="{y-11}" width="9" height="9" transform="rotate(45 {f(x-207.5)} {f(y-6.5)})" fill="none" stroke="{ICE}" stroke-width="1.5"/>')
text(x - 194, y - 1, "minor find", 13.5, ICE, 600)
add(f'<circle cx="{f(x-98)}" cy="{y-6}" r="5" fill="#1a232c" stroke="{DIM}" stroke-width="1.6"/>')
text(x - 88, y - 1, "later site", 13.5, DIM, 600)

# --- side panel: the loop
px0, py0 = IX, IY + IH + 18
add(f'<rect x="{px0}" y="{py0}" width="{IW}" height="{f(MT + MH - py0)}" rx="10" fill="#0b131b" stroke="#1d2934"/>')
cx, cy = px0 + 20, py0 + 34
text(cx, cy, "THE LOOP · ABOUT 20 MIN", 16, AMB, 700, "start", 1.5)
steps = [
    ("Rumour", "The Hollow's salvage broker: a Clan hauler went dark."),
    ("The tether", "Its last beacon pointed past the belt, at the Haze."),
    ("The crossing", "Cross the Scatter. Ping (V) to see, and be seen, or run quiet."),
    ("The hauler", "Raiders strip it. Fight, or cut in. Its log names CINDER."),
    ("Away team", "Down the dead colony's lift: one scene, a crew trait decides it."),
    ("Home", "Skirt the star's heat back to the Hollow. Repair, sell, hire."),
    ("The gate", "The clue from CINDER opens it. The slice ends there."),
]
cy += 30
for i, (title, line) in enumerate(steps):
    add(f'<circle cx="{f(cx+10)}" cy="{f(cy-5)}" r="10" fill="{AMB}"/>')
    text(cx + 10, cy, str(i + 1), 12, "#071018", 800, "middle", halo=False)
    text(cx + 30, cy, title, 14, TXT, 700)
    text(cx + 30, cy + 17, line, 12.5, DIM)
    cy += 43
total = sum(s for _, _, s in times) + seconds([sites["hollow"], sites["tether"]]) * 2
cy += 4
add(f'<line x1="{cx}" y1="{f(cy-14)}" x2="{f(px0+IW-20)}" y2="{f(cy-14)}" stroke="#1d2934"/>')
text(cx, cy + 4, "TRAVEL", 13, ACC, 700, "start", 1.5)
for j, l in enumerate([f"About {fmt(total)} of the loop is travel. Cruise slows to",
                       f"flight speed (120 m/s) within 1 km of anything you",
                       f"can stop at, and builds to {VMAX/1000:.0f} km/s in open space.",
                       f"Crossing VESHA takes about {160 / VMAX * 1000:.0f} s: its surface scrolls by."]):
    text(cx, cy + 22 + j * 16, l, 12.5, DIM)
cy += 22 + 4 * 16 + 14
text(cx, cy, "NEW TO BUILD", 13, ACC, 700, "start", 1.5)
for j, l in enumerate(["cruise (ADR) · “?” contacts · a ping that gives you away",
                       "away-team scenes · named crew · fuel and supplies",
                       "CINDER, ORRO, PALE, the belt, the hauler, the gate"]):
    text(cx, cy + 18 + j * 16, l, 12.5, DIM)

add('</svg>')
open(out, "w").write("\n".join(svg))
for name, km, s in times: print(f"{name}: {km:.0f} km, {fmt(s)}")
print("hollow<->tether", fmt(seconds([sites['hollow'], sites['tether']])), "| loop travel", fmt(total))
