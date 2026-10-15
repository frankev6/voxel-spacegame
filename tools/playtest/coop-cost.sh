#!/bin/zsh
# coop-cost.sh [seconds]  SPEC-90 §10a gap 1: per-frame cost of the co-op layer under load. Needs a running session with two guests
# (coop.sh start; coop.sh guest 2). Loads the range station, starts combat, keeps both guests firing, cuts the range bridges
# twice so big grids diff and chunks spawn, and prints host and guest `net status` cost lines plus frame time.
. "$(dirname "$0")/_env.sh"
export UNITY_PROJECT_PATH="$PROJECT_ROOT"
G="$PT_DIR/g.sh"
secs=${1:-15}
host() { "$G" dev --cmd "$1" 2>/dev/null; }
guest() { "$G" dev --cmd "net guest $1 \"$2\"" >/dev/null 2>&1; sleep ${3:-1}; "$G" dev --cmd "net reply" 2>/dev/null; }

host "playground run=range" >/dev/null
host "playground run=combat" >/dev/null
for slot in 1 2; do host "net bring $slot" >/dev/null; done
sleep 3
host "net costreset" >/dev/null; for slot in 1 2; do guest $slot "net costreset" 0.2 >/dev/null; done
for slot in 1 2; do guest $slot "aim target=nearest_hostile track=$secs" 0.2 >/dev/null; guest $slot "fire group=1 seconds=$secs" 0.2 >/dev/null; done
for x in 24 36 48; do "$G" gh_dmg_hit --x $x --y -0.5 --z 40.5 --amount 500000 --radius 2 >/dev/null; done
sleep $((secs / 2))
for x in 30 42; do "$G" gh_dmg_hit --x $x --y -0.5 --z 52.5 --amount 500000 --radius 2 >/dev/null; done
sleep $((secs / 2))
host "net hash" >/dev/null; sleep 2
echo "host:  $(host "net status" | grep "cost ms")"
echo "       $(host "perf" | grep -iE "frame|fps" | head -1)"
echo "guest: $(guest 1 "net status" | grep "cost ms")"
