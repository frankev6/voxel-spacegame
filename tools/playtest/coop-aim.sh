#!/bin/zsh
# coop-aim.sh [rounds] [seconds]  SPEC-90 §10a: does a guest's manual fire hit on the host? Needs a running session with guest 1
# (coop.sh start). Adds 120 ms of round trip, then alternates rounds with aim anchors off and on: each round spawns a fresh AI
# fighter, guest 1 tracks it with the cursor (aim point mode, no autofire) and fires, and the host counts the fighter's lost cells.
. "$(dirname "$0")/_env.sh"
export UNITY_PROJECT_PATH="$PROJECT_ROOT"
G="$PT_DIR/g.sh"
rounds=${1:-2}; secs=${2:-6}
host() { "$G" dev --cmd "$1" 2>/dev/null; }
guest() { "$G" dev --cmd "net guest 1 \"$1\"" >/dev/null 2>&1; sleep ${2:-1}; "$G" dev --cmd "net reply" 2>/dev/null; }
cells() { host "grid id=$1" | sed -nE 's/.*solid cells=([0-9]+).*/\1/p' | head -1; }

host "net sim 60 0 0" >/dev/null; guest "net sim 60 0 0" 0.3 >/dev/null
for round in $(seq 1 $rounds); do
  for mode in off on; do
    host "net anchor $mode" >/dev/null; guest "net anchor $mode" 0.3 >/dev/null
    host "playground run=combat" >/dev/null; host "net bring 1" >/dev/null; sleep 3
    id=$(guest "aim target=nearest_hostile track=$secs" 0.5 | sed -nE 's/.*id:([0-9]+).*/\1/p' | head -1)
    [[ -z "$id" ]] && { echo "round $round $mode: no hostile"; continue; }
    before=$(cells $id)
    guest "fire group=1 seconds=$secs" 0.2 >/dev/null
    sleep $((secs + 1))
    after=$(cells $id); after=${after:-0}
    echo "round $round, anchors $mode: target $id lost $((before - after)) of $before cells"
  done
done
host "net anchor on" >/dev/null; guest "net anchor on" 0.3 >/dev/null; host "net sim 0 0 0" >/dev/null; guest "net sim 0 0 0" 0.3 >/dev/null
