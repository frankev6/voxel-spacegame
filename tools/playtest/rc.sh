#!/bin/zsh
# rc.sh [--no-trigger]  Recompile through the CLI, wait, print compile errors and Editor state. Exit 1 on errors.
. "$(dirname "$0")/_env.sh"
start=$(wc -l < Logs/Editor.log 2>/dev/null | tr -d ' '); start=${start:-0}
[[ "$1" != "--no-trigger" ]] && unity command recompile --format json >/dev/null 2>&1
for i in $(seq 1 180); do
  sleep 1
  st=$(unity command recompile_status --format json 2>/dev/null | python3 -c "import json,sys
try:
  r=json.load(sys.stdin)['data']['result']; print(r.get('status') if isinstance(r,dict) else r)
except Exception: print('unreachable')")
  case "$st" in completed|up_to_date|idle|failed) break;; esac
done
echo "recompile: $st after ${i}s"
errs=$(awk -v s=$start 'NR>s' Logs/Editor.log 2>/dev/null | grep -E "error CS[0-9]+" | sed -E 's/^.*((Assets|Packages)\/)/\1/' | sort -u)
[[ -n "$errs" ]] && echo "$errs" | head -60
for i in $(seq 1 90); do
  s2=$(unity command editor_status --format json 2>/dev/null | python3 -c "import json,sys
try:
  r=json.load(sys.stdin)['data']['result']; print(r['status'], r['compiling'], r['domainReloadInProgress'], r['playMode'])
except Exception: print('unreachable')")
  [[ "$s2" == "ready False False"* ]] && break
  sleep 1
done
echo "editor: $s2"
[[ -z "$errs" && "$st" != "failed" ]] && echo "COMPILE OK" || { echo "COMPILE FAILED"; exit 1; }
