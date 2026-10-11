#!/usr/bin/env python3
"""Summarise perf records written by PerfRun.Write (SPEC-71 §5.3): one line per Logs/perf/*.json with test, gate, median, p95,
budget and PASS/FAIL. Exit 1 if any record failed.

  perf_summary.py [--dir Logs/perf] [--since <unix seconds>]   (--since: only records written during this run)"""
import argparse, glob, json, os, sys

def fmt(v):
    return "-" if v is None else f"{v:.4g}"

def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default=os.path.join(root, "Logs", "perf"))
    ap.add_argument("--since", type=float, default=0)
    a = ap.parse_args()
    files = sorted(f for f in glob.glob(os.path.join(a.dir, "*.json")) if os.path.getmtime(f) >= a.since)
    if not files:
        print(f"perf: no records in {a.dir}"); return 0
    rows, failed = [], 0
    for f in files:
        try:
            r = json.load(open(f))
        except Exception as e:
            rows.append((os.path.basename(f), "-", "-", "-", "-", f"FAIL (unreadable: {e})")); failed += 1; continue
        stats = r.get("stats") or []
        gate = r.get("gate")
        s = next((x for x in stats if x.get("name") == gate), stats[0] if stats else {})
        b = r.get("budget") or {}
        budget = " / ".join(x for x in (f"med {fmt(b.get('median'))}" if b.get("median") is not None else "", f"p95 {fmt(b.get('p95'))}" if b.get("p95") is not None else "") if x) or "-"
        ok = bool(r.get("pass"))
        failed += 0 if ok else 1
        rows.append((r.get("test", os.path.basename(f)), gate or s.get("name", "-"), fmt(s.get("median")), fmt(s.get("p95")), budget, "PASS" if ok else "FAIL"))
    head = ("test", "gate", "median", "p95", "budget", "result")
    widths = [max(len(str(x[i])) for x in rows + [head]) for i in range(len(head))]
    for row in [head] + rows:
        print("  ".join(str(v).ljust(widths[i]) for i, v in enumerate(row)))
    print(f"perf: {len(rows) - failed} pass, {failed} fail")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
