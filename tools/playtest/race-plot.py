#!/usr/bin/env python3
"""race-plot.py  Plot a race trace (`race trace on`) over its track.

usage: race-plot.py <trace.csv> [--track ArtSource/Racing/tracks/track_01.json] [--out Logs/race-plot] [--incidents 8]

Writes, in --out:
  overview.png    the whole track (routes, fences, gates, boost strips as spawned when <trace>.boost.csv exists, mine fields, wrecks) with every racer's path; red where it
                  was outside its safe band, orange where it stalled, a ring where it re-planned its line
  incident-N.png  the worst moments, zoomed: the nose (white tick) and velocity (racer colour) every half second, and a thin line
                  to the point the AI steered for
  timeline.png    per racer against time: speed, target speed, off-line, threat and yaw rate; gate passes as ticks
Coordinates are track space (metres, x right, z up). Needs Pillow only.
"""
import argparse, csv, json, math, pathlib
from PIL import Image, ImageDraw, ImageFont

COLOURS = [(255, 210, 60), (90, 200, 255), (255, 110, 200), (140, 255, 120), (255, 150, 70), (190, 150, 255), (255, 255, 255), (120, 255, 230)]
BG, ROUTE, FENCE, GATE, BOOST, MINE, WRECK = (12, 14, 22), (34, 40, 58), (70, 160, 230), (230, 230, 230), (60, 200, 90), (220, 60, 60), (70, 70, 80)
RACING = 3


def font(size):
    try: return ImageFont.load_default(size=size)
    except TypeError: return ImageFont.load_default()


def load(trace):
    rows = {}
    with open(trace, newline='') as f:
        for r in csv.DictReader(f):
            def num(k, d=0.0):
                v = r.get(k, '')
                return float(v) if v not in ('', None) else d
            row = dict(tick=int(float(r['tick'])), phase=int(r['phase']), name=r['name'], x=num('x'), z=num('z'), yaw=num('yaw'), vx=num('vx'), vz=num('vz'),
                       yawrate=num('yawrate'), mass=num('mass'), lap=int(num('lap')), gate=int(num('lastgate', -1)), ai=r['ai'] == '1', target=num('target'),
                       off=num('off', -99), threat=num('threat'), aimx=num('aimx'), aimz=num('aimz'), cmd=int(num('cmd')), linefrom=int(num('linefrom', -2)),
                       lineto=int(num('lineto', -1)), replans=int(num('replans')))
            row['speed'] = math.hypot(row['vx'], row['vz'])
            rows.setdefault(int(r['slot']), []).append(row)
    return rows


class View:
    def __init__(self, x0, z0, x1, z1, width):
        self.x0, self.z0, self.x1, self.z1 = x0, z0, x1, z1
        self.s = width / (x1 - x0)
        self.w, self.h = int(width), int((z1 - z0) * self.s)
    def p(self, x, z): return ((x - self.x0) * self.s, (self.z1 - z) * self.s)


def draw_track(d, track, v, detail):
    for name, path in track['paths'].items():
        pts = [v.p(*q) for q in path['points']]
        if path.get('closed'): pts.append(pts[0])
        d.line(pts, fill=ROUTE, width=max(1, int(path['width'] * v.s)), joint='curve')
    for w in track['wrecks']:
        o = [v.p(*q) for q in w['outline']]
        if len(o) > 2: d.polygon(o, outline=WRECK)
    for b in track.get('boost_boxes') or track['boost']:
        c, yaw, hl, hw = b['center'], b['yaw'], b['length'] / 2, b['width'] / 2
        f, r = (math.sin(yaw), math.cos(yaw)), (math.cos(yaw), -math.sin(yaw))
        corners = [(c[0] + f[0] * a + r[0] * s, c[1] + f[1] * a + r[1] * s) for a, s in ((-hl, -hw), (-hl, hw), (hl, hw), (hl, -hw))]
        d.polygon([v.p(*q) for q in corners], outline=BOOST)
    for m in track['mines']:
        (cx, cz), r = m['c'], m['r']
        a, b = v.p(cx - r, cz + r), v.p(cx + r, cz - r)
        d.ellipse([a, b], outline=MINE)
    for w in track['walls']:
        d.line([v.p(*q) for q in w], fill=FENCE, width=2 if detail else 1)
    f = font(14 if detail else 12)
    for g in track['gates']:
        (x, z), yaw, half = g['position'], g['yaw'], g['width'] / 2
        r = (math.cos(yaw), -math.sin(yaw))
        d.line([v.p(x - r[0] * half, z - r[1] * half), v.p(x + r[0] * half, z + r[1] * half)], fill=GATE, width=2)
        px, pz = v.p(x + r[0] * (half + 15), z + r[1] * (half + 15))
        d.text((px, pz), str(g['id']), fill=GATE, font=f)


def racing(rows): return [r for r in rows if r['phase'] == RACING or r['phase'] == 4]


def overview(track, data, out):
    xs = [q[0] for p in track['paths'].values() for q in p['points']]; zs = [q[1] for p in track['paths'].values() for q in p['points']]
    v = View(min(xs) - 250, min(zs) - 250, max(xs) + 250, max(zs) + 250, 2400)
    img = Image.new('RGB', (v.w, v.h), BG); d = ImageDraw.Draw(img)
    draw_track(d, track, v, False)
    legend = []
    for i, (slot, rows) in enumerate(sorted(data.items())):
        rows = racing(rows); col = COLOURS[i % len(COLOURS)]
        if len(rows) < 2: continue
        for a, b in zip(rows, rows[1:]):
            if math.hypot(b['x'] - a['x'], b['z'] - a['z']) > 120: continue   # a teleport (respawn)
            bad = a['ai'] and a['off'] > 0
            stall = a['speed'] < 10 and a['phase'] == RACING
            d.line([v.p(a['x'], a['z']), v.p(b['x'], b['z'])], fill=(255, 60, 60) if bad else (255, 160, 40) if stall else col, width=4 if bad or stall else 2)
        for a, b in zip(rows, rows[1:]):
            if b['replans'] != a['replans']:
                x, y = v.p(b['x'], b['z']); d.ellipse([x - 7, y - 7, x + 7, y + 7], outline=col, width=2)
        last = rows[-1]
        legend.append((col, f"{rows[0]['name']} (slot {slot})  lap {last['lap']}  gate {last['gate']}  mass {rows[0]['mass']:.0f}->{last['mass']:.0f}"))
    f = font(22)
    for i, (col, text) in enumerate(legend): d.text((20, 20 + i * 28), text, fill=col, font=f)
    d.text((20, 20 + len(legend) * 28 + 6), "red: outside its safe band   orange: stalled   ring: new line   green box: boost strip   red circle: mine field",
           fill=(200, 200, 200), font=font(16))
    img.save(out / 'overview.png')


def incidents(data, limit):
    found = []
    for slot, rows in data.items():
        rows = racing(rows); run = []
        for r in rows + [None]:
            bad = r is not None and r['phase'] == RACING and (r['ai'] and r['off'] > 0 or r['speed'] < 10 or abs(r['yawrate']) > 1.6)
            if bad: run.append(r)
            elif run:
                found.append((len(run), slot, run[0]['tick'], run[-1]['tick'])); run = []
    found.sort(reverse=True)
    return found[:limit]


def incident(track, data, slot, t0, t1, index, out):
    rows = [r for r in data[slot] if t0 - 300 <= r['tick'] <= t1 + 180]
    xs = [r['x'] for r in rows]; zs = [r['z'] for r in rows]
    cx, cz = (min(xs) + max(xs)) / 2, (min(zs) + max(zs)) / 2
    half = max(220, (max(xs) - min(xs)) / 2 + 80, (max(zs) - min(zs)) / 2 + 80)
    v = View(cx - half, cz - half, cx + half, cz + half, 1100)
    img = Image.new('RGB', (v.w, v.h + 150), BG); d = ImageDraw.Draw(img)
    draw_track(d, track, v, True)
    # the other racers at the same moments, faint
    for other, orows in data.items():
        if other == slot: continue
        pts = [v.p(r['x'], r['z']) for r in orows if t0 - 300 <= r['tick'] <= t1 + 180]
        if len(pts) > 1: d.line(pts, fill=(90, 90, 110), width=1)
    col = COLOURS[sorted(data).index(slot) % len(COLOURS)]
    d.line([v.p(r['x'], r['z']) for r in rows], fill=col, width=2)
    f = font(13)
    for k, r in enumerate(rows):
        x, y = v.p(r['x'], r['z'])
        inside = t0 <= r['tick'] <= t1
        if k % 3 == 0:
            if r['ai'] and r['aimx'] != 0: d.line([(x, y), v.p(r['aimx'], r['aimz'])], fill=(80, 80, 140), width=1)
            nose = (math.sin(r['yaw']) * 35, math.cos(r['yaw']) * 35)
            d.line([(x, y), v.p(r['x'] + nose[0], r['z'] + nose[1])], fill=(255, 255, 255), width=2)
            d.line([(x, y), v.p(r['x'] + r['vx'] * .4, r['z'] + r['vz'] * .4)], fill=col, width=2)
            d.ellipse([x - 3, y - 3, x + 3, y + 3], fill=(255, 60, 60) if inside else col)
        if k % 9 == 0:
            flags = ('B' if r['cmd'] & 2 else '') + ('+' if r['cmd'] & 1 else '') + ('F' if r['cmd'] & 4 else '')
            d.text((x + 6, y + 4), f"{(r['tick'] - rows[0]['tick']) / 60:.1f}s {r['speed']:.0f}/{r['target']:.0f} off{r['off']:.0f} {flags}", fill=(220, 220, 220), font=f)
    name = rows[0]['name']
    lines = [f"incident {index}: {name} (slot {slot}), ticks {t0}..{t1} ({(t1 - t0) / 60:.1f} s), from gate {rows[0]['gate']} to {rows[-1]['gate']}",
             f"mass {rows[0]['mass']:.0f} -> {rows[-1]['mass']:.0f} kg; max yaw rate {max(abs(r['yawrate']) for r in rows):.2f} rad/s; min speed {min(r['speed'] for r in rows):.0f} m/s",
             "white tick: nose   colour arrow: velocity (0.4 s)   blue line: AI aim point   red dots: the incident   labels: t speed/target off, B brake + boost F firing",
             "line plans in view: " + ", ".join(sorted({f"{r['linefrom']}->{r['lineto']}" for r in rows if r['ai']}))]
    for i, text in enumerate(lines): d.text((14, v.h + 12 + i * 30), text, fill=(230, 230, 230), font=font(18))
    img.save(out / f'incident-{index}.png')


def timeline(data, out):
    slots = sorted(data); H = 230; W = 2200
    img = Image.new('RGB', (W, H * len(slots) + 40), BG); d = ImageDraw.Draw(img)
    ticks = [r['tick'] for rows in data.values() for r in racing(rows)]
    if not ticks: return
    t0, t1 = min(ticks), max(ticks)
    def tx(t): return 60 + (t - t0) / max(1, t1 - t0) * (W - 80)
    f = font(14)
    for i, slot in enumerate(slots):
        rows = racing(data[slot]); top = 20 + i * H
        d.rectangle([60, top, W - 20, top + H - 30], outline=(60, 60, 80))
        d.text((64, top + 2), f"{rows[0]['name'] if rows else slot}: speed (white) target (yellow) off-line (red, 0 at the band edge) threat (orange) yaw rate (cyan)", fill=(220, 220, 220), font=f)
        def series(key, scale, colour, base=None):
            base = top + H - 30 if base is None else base
            pts = [(tx(r['tick']), base - r[key] * scale) for r in rows]
            if len(pts) > 1: d.line(pts, fill=colour, width=1)
        series('speed', .7, (240, 240, 240)); series('target', .7, (240, 220, 60))
        mid = top + (H - 30) / 2
        d.line([(60, mid), (W - 20, mid)], fill=(50, 50, 60))
        series('off', .25, (255, 70, 70), mid); series('threat', 6, (255, 150, 40)); series('yawrate', 30, (80, 220, 255), mid)
        for a, b in zip(rows, rows[1:]):
            if b['gate'] != a['gate']:
                x = tx(b['tick']); d.line([(x, top + H - 40), (x, top + H - 30)], fill=(200, 200, 200)); d.text((x - 4, top + H - 56), str(b['gate']), fill=(200, 200, 200), font=f)
    for s in range(0, int((t1 - t0) / 60) + 1, 10):
        x = tx(t0 + s * 60); d.text((x - 8, H * len(slots) + 20), f"{s}s", fill=(160, 160, 160), font=f)
    img.save(out / 'timeline.png')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('trace'); ap.add_argument('--track', default='ArtSource/Racing/tracks/track_01.json'); ap.add_argument('--out', default='Logs/race-plot')
    ap.add_argument('--incidents', type=int, default=8)
    a = ap.parse_args()
    track = json.load(open(a.track)); data = load(a.trace)
    boxes = pathlib.Path(a.trace).with_suffix('.boost.csv')   # the strips as spawned (race trace writes them)
    if boxes.exists():
        with open(boxes, newline='') as f:
            track['boost_boxes'] = [dict(center=[float(r['x']), float(r['z'])], yaw=float(r['yaw']), length=2 * float(r['hz']), width=2 * float(r['hx'])) for r in csv.DictReader(f)]
    out = pathlib.Path(a.out); out.mkdir(parents=True, exist_ok=True)
    for old in out.glob('incident-*.png'): old.unlink()
    overview(track, data, out); timeline(data, out)
    found = incidents(data, a.incidents)
    for i, (n, slot, t0, t1) in enumerate(found, 1): incident(track, data, slot, t0, t1, i, out)
    for slot, rows in sorted(data.items()):
        rr = racing(rows)
        if rr: print(f"{rr[0]['name']:8} slot {slot:3}: lap {rr[-1]['lap']} gate {rr[-1]['gate']}  outside band {sum(1 for r in rr if r['ai'] and r['off'] > 0) / 10:.1f}s  "
                     f"stalled {sum(1 for r in rr if r['speed'] < 10 and r['phase'] == RACING) / 10:.1f}s  mass {rr[0]['mass']:.0f}->{rr[-1]['mass']:.0f}  replans {rr[-1]['replans'] - rr[0]['replans']}")
    for slot, rows in sorted(data.items()):
        gates = []
        for r in racing(rows):
            if r['phase'] == RACING and (not gates or gates[-1] != r['gate']) and r['gate'] >= 0: gates.append(r['gate'])
        if rows: print(f"{rows[0]['name']:8} gates: {' '.join(map(str, gates))}")
    for slot, rows in sorted(data.items()):
        rr = racing(rows)
        if rr: print(f"{rr[0]['name']:8} firing {sum(1 for r in rr if r['cmd'] & 4) / 10:.1f}s")
    print(f"{len(found)} incidents; plots in {out}")


if __name__ == '__main__':
    main()
