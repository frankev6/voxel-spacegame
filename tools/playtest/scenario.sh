#!/bin/zsh
# scenario.sh <name or path> [timeout=600]  Run and poll through the shared CLI.
. "$(dirname "$0")/_env.sh"
python3 - "$PROJECT_ROOT" "$@" <<'PYTHON'
import json, pathlib, subprocess, sys, time
root = pathlib.Path(sys.argv[1])
if len(sys.argv) < 3:
    print('usage: scenario.sh <name or path> [timeout=600]'); sys.exit(2)
file = pathlib.Path(sys.argv[2])
if not file.exists() and file.suffix != '.scenario': file = root / 'TestData/Scenarios' / (str(file) + '.scenario')
try:
    timeout = float(sys.argv[3].removeprefix('timeout=')) if len(sys.argv) > 3 else 600
    if not 0 < timeout < float('inf'): raise ValueError()
except ValueError:
    print('timeout must be finite and positive'); sys.exit(2)
def call(command, *args):
    try:
        result = subprocess.run([str(root/'tools/playtest/to.sh'), '65', str(root/'tools/playtest/g.sh'), command, *args], capture_output=True, text=True, timeout=70)
        if result.returncode: raise RuntimeError(result.stdout + result.stderr)
        return json.loads(result.stdout)
    except (subprocess.TimeoutExpired, ValueError, RuntimeError, OSError) as error:
        print('Editor unreachable (is a modal dialog open?):', str(error)[:300]); sys.exit(3)
result = call('scenario', '--file', str(file.resolve()))
run = result.get('runId')
deadline = time.monotonic() + timeout
while result['outcome'] == 'running':
    if time.monotonic() >= deadline:
        result = call('scenario_stop', '--run', str(run)); break
    time.sleep(1)
    result = call('scenario_status', '--run', str(run))
print(f"{result.get('name', file.stem)} {result['outcome']} {result.get('seconds', 0):.3f}s: {result.get('message', '')}")
print(result.get('jsonPath', ''))
sys.exit(result['exitCode'])
PYTHON
