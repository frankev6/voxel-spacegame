#!/bin/zsh
# g.sh <cmd> [--args]  Run a Unity CLI command against the open Editor and print just the result.
# Harness commands may omit the gh_ prefix:  g.sh snapshot | g.sh click --target "New Game" | g.sh key --keys W
. "$(dirname "$0")/_env.sh"
c=$1; shift
# Unity built-ins are either listed here or contain "_"; everything else is a harness command.
case "$c" in
  gh_*|eval|recompile|batch|menu|build|*_*) ;;
  *) c="gh_$c";;
esac
case "$c" in unfocused_input|scenario_*) c="gh_$c";; esac
unity command "$c" "$@" --timeout 60 --format json 2>&1 | python3 -c "
import json,sys
raw=sys.stdin.read()
try: d=json.loads(raw)
except Exception: print(raw[:4000]); sys.exit()
if not d.get('success'): print('ERR', json.dumps(d.get('errors'))[:2000]); sys.exit(1)
r=d['data']['result']
print(r if isinstance(r,str) else json.dumps(r, indent=1)[:8000])"
