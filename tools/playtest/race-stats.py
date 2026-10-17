"""race-stats.py <race-trace.csv>: per racer, while racing: average speed, hard hits (a speed drop over 20 m/s between rows),
resets (a jump over 120 m), and seconds spent drifting (10-60 degrees between nose and velocity) or sliding (over 60)."""
import csv, math, collections, sys

rows = collections.defaultdict(list)
for r in csv.DictReader(open(sys.argv[1])):
    if r['phase'] == '3' and r.get('vz') not in (None, ''): rows[r['name']].append(r)
for name, rr in rows.items():
    speed = [math.hypot(float(r['vx']), float(r['vz'])) for r in rr]
    resets = sum(1 for a, b in zip(rr, rr[1:]) if math.hypot(float(b['x']) - float(a['x']), float(b['z']) - float(a['z'])) > 120)
    hits = sum(1 for a, b in zip(speed, speed[1:]) if a - b > 20)
    # durations from ticks (rows come every 6+ ticks, further apart at higher time scales)
    dt = [(float(b['tick']) - float(a['tick'])) / 60 for a, b in zip(rr, rr[1:])] + [0.1]
    slip = [(abs((math.degrees(math.atan2(float(r['vx']), float(r['vz'])) - float(r['yaw'])) + 180) % 360 - 180), d)
            for r, s, d in zip(rr, speed, dt) if s > 40]
    drift = sum(d for x, d in slip if 10 < x < 60); slide = sum(d for x, d in slip if x >= 60)
    print(f"{name:6} avg speed {sum(speed) / len(speed):5.1f}  hits {hits:3}  resets {resets}  drifting {drift:5.1f}s  sliding>60 {slide:5.1f}s")
