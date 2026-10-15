#!/bin/zsh
# coop-chunks.sh  SPEC-90 NET-008 check: with a session running (coop.sh start), cut both starboard range bridges into short
# segments so many chunk bodies spawn, wait past their lifetime so they despawn, then report chunk counts and a hash check.
. "$(dirname "$0")/_env.sh"
export UNITY_PROJECT_PATH="$PROJECT_ROOT"
G="$PT_DIR/g.sh"
"$G" dev --cmd "playground run=range" | tail -1
for slot in 1 2 3; do "$G" dev --cmd "net bring $slot" >/dev/null 2>&1; done
sleep 2
for x in 24 30 36 42 48; do
  for z in 40.5 52.5; do "$G" gh_dmg_hit --x $x --y -0.5 --z $z --amount 500000 --radius 2 >/dev/null; done
done
sleep 2
"$G" dev --cmd "net status" | grep "host:"
wait_secs=${1:-45}
echo "waiting $wait_secs s for chunk lifetimes"
sleep $wait_secs
"$G" dev --cmd "net status" | grep "host:"
"$G" dev --cmd "net hash" >/dev/null
sleep 3
grep -h "hash check" "$OUT_DIR"/guest*.log | tail -3
for log in "$OUT_DIR"/guest*.log; do echo "$log: $(grep -c 'Native Crash' "$log") crashes, $(grep -ciE 'exception' "$log") exceptions"; done
