#!/bin/zsh
# scenarios.sh [tag=regression]  Run active tagged files in stable name order.
. "$(dirname "$0")/_env.sh"
python3 - "$PROJECT_ROOT" "$@" <<'PYTHON'
import json, pathlib, shlex, subprocess, sys, time
root = pathlib.Path(sys.argv[1]); tag = sys.argv[2].removeprefix('tag=') if len(sys.argv) > 2 else 'regression'
rows = []
for file in sorted((root/'TestData/Scenarios').glob('*.scenario')):
    status, tags = 'active', []
    for line in file.read_text(encoding='utf-8-sig').splitlines():
        try: words = shlex.split(line, comments=True)
        except ValueError: continue
        if not words: continue
        if words[0] == 'status' and len(words) > 1: status = words[1]
        elif words[0] == 'tags': tags = words[1:]
        elif words[0] not in ('scenario', 'desc', 'timeout'): break
    if status != 'active' or tag not in tags: continue
    started = time.monotonic()
    result = subprocess.run([str(root/'tools/playtest/scenario.sh'), str(file)], text=True, capture_output=True)
    output = result.stdout.strip().splitlines()
    json_path = output[-1] if output and output[-1].endswith('.json') else ''
    row = dict(name=file.stem, result='pass' if result.returncode == 0 else 'fail', seconds=time.monotonic()-started, exitCode=result.returncode, jsonPath=json_path)
    rows.append(row); print(f"{row['name']:<30} {row['result']:<5} {row['seconds']:.3f}s")
    if result.returncode: print(result.stdout, result.stderr)
summary = root/'Logs/playtest/scenarios/summary.json'; summary.parent.mkdir(parents=True, exist_ok=True)
summary.write_text(json.dumps(dict(tag=tag, runs=rows), indent=2)+'\n', encoding='utf-8')
print(summary)
sys.exit(0 if all(row['exitCode'] == 0 for row in rows) else 1)
PYTHON
