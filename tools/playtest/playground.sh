#!/bin/zsh
# playground.sh  Enter Play and launch the isolated development playground.
. "$(dirname "$0")/_env.sh"
python3 - "$PROJECT_ROOT" "$@" <<'PYTHON'
import pathlib, re, subprocess, sys, time
root = pathlib.Path(sys.argv[1])
seed = sys.argv[2] if len(sys.argv) > 2 else '1'
ship = sys.argv[3] if len(sys.argv) > 3 else ''
started = time.monotonic()
deadline = started + 90
def expired():
    print('failed: playground did not finish within 90 s'); sys.exit(1)
def call(command, *args):
    remaining = deadline - time.monotonic()
    if remaining <= 0: expired()
    limit = min(65, remaining)
    try:
        result = subprocess.run([str(root/'tools/playtest/to.sh'), str(limit), str(root/'tools/playtest/g.sh'), command, *args], capture_output=True, text=True, timeout=limit)
        if result.returncode: raise RuntimeError(result.stdout + result.stderr)
        return result.stdout.strip()
    except (subprocess.TimeoutExpired, RuntimeError, OSError) as error:
        if time.monotonic() >= deadline: expired()
        print('Editor unreachable (is a modal dialog open?):', str(error)[:300]); sys.exit(3)
result = call('playground')
match = re.match(r'^op (\d+):', result)
if not match:
    print(result); sys.exit(1)
operation = match.group(1)
while time.monotonic() < deadline:
    result = call('op', '--id', operation)
    if result.startswith('succeeded:'):
        print(f'{result} ({time.monotonic()-started:.3f}s)')
        print(call('snapshot', '--ui', 'false')); sys.exit(0)
    if not result.startswith('running:'):
        print(result); sys.exit(1)
    time.sleep(.5)
expired()
PYTHON
