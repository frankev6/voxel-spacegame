#!/usr/bin/env python3
"""Print a compact summary of an NUnit XML results file. Exit 1 if anything failed or nothing ran."""
import sys, xml.etree.ElementTree as ET
path = sys.argv[1]
try:
    root = ET.parse(path).getroot()
except Exception as e:
    print(f"no results at {path}: {e}"); sys.exit(1)
cases = root.iter("test-case")
passed = failed = skipped = 0
fails = []
for c in cases:
    r = c.get("result", "")
    if r == "Passed": passed += 1
    elif r == "Failed":
        failed += 1
        msg = (c.findtext("failure/message") or "").strip().splitlines()
        fails.append(f"  FAIL {c.get('fullname')}: {msg[0] if msg else ''}")
    else: skipped += 1
print(f"tests: {passed} passed, {failed} failed, {skipped} skipped  ({path})")
for f in fails[:40]: print(f)
sys.exit(1 if failed or passed == 0 else 0)
