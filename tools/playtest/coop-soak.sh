#!/bin/zsh
# coop-soak.sh [seconds per point]  SPEC-90 NET-010 automated scenario. Needs the open Editor on THIS worktree and the dev player.
# Hosts the playground combat with two guests, then for each simulated latency (0, 50, 100, 150 ms, ±10 ms jitter, 2% loss above 0)
# has both guests fly and fire while an AI ship fights them, and prints one table row per point: host send rate per guest, guest
# receive rate, round trip, own-ship prediction error and snaps, host command buffer, cell hash mismatches. Ends with a crash scan.
. "$(dirname "$0")/_env.sh"
export UNITY_PROJECT_PATH="$PROJECT_ROOT"
G="$PT_DIR/g.sh"
secs=${1:-20}

host() { "$G" dev --cmd "$1" 2>/dev/null; }
guest() { "$G" dev --cmd "net guest $1 \"$2\"" >/dev/null 2>&1; sleep ${3:-1}; "$G" dev --cmd "net reply" 2>/dev/null; }
field() { sed -nE "s/.*$1.*/\\1/p" | head -1; }

"$PT_DIR/coop.sh" start | tail -1 || exit 1
"$PT_DIR/coop.sh" guest 2 | tail -1 || exit 1
host "playground run=combat" >/dev/null
for slot in 1 2; do host "net bring $slot" >/dev/null; done
sleep 3

printf "\n%-8s %-12s %-12s %-8s %-18s %-10s %-24s %-10s\n" "latency" "host out (2)" "guest in" "rtt" "pred err (peak)" "snaps" "host cmd buffer" "hash"
for ms in 0 50 100 150; do
  loss=$([[ $ms == 0 ]] && echo 0 || echo 2); jitter=$([[ $ms == 0 ]] && echo 0 || echo 10)
  half=$((ms / 2))
  host "net sim $half $jitter $loss" >/dev/null
  for slot in 1 2; do guest $slot "net sim $half $jitter $loss" 0.3 >/dev/null; done
  base1=$(guest 1 "net status" | field 'snaps ([0-9]+)')
  starve0=$(host "net status" | field 'pilot 1 #[0-9]+ queue [0-9]+ starved ([0-9]+)')
  for slot in 1 2; do
    guest $slot "aim target=nearest_hostile track=$secs" 0.2 >/dev/null
    guest $slot "fire group=1 seconds=$secs" 0.2 >/dev/null
  done
  # Weave: alternate thrust and turn so prediction sees acceleration, braking and rotation.
  end=$((SECONDS + secs))
  while (( SECONDS < end )); do
    guest 1 "key keys=W,A hold=2" 0.2 >/dev/null; guest 2 "key keys=W,D hold=2" 0.2 >/dev/null; sleep 2
    guest 1 "key keys=S,D hold=1" 0.2 >/dev/null; guest 2 "key keys=S,A hold=1" 0.2 >/dev/null; sleep 1.5
  done
  hs=$(host "net status")
  out=$(echo "$hs" | sed -nE 's/.*out ([0-9]+) kbps reliable, ([0-9]+) kbps unreliable.*/\1+\2/p' | head -1)
  gs=$(guest 1 "net status")
  in=$(echo "$gs" | sed -nE 's/.*in  ([0-9]+) kbps reliable, ([0-9]+) kbps unreliable.*/\1+\2/p' | head -1)
  rtt=$(echo "$gs" | field 'rtt ([0-9]+) ms')
  err=$(echo "$gs" | sed -nE 's/.*error ([0-9.]+) m \(peak ([0-9.]+)\).*/\1 (\2)/p' | head -1)
  snaps=$(echo "$gs" | field 'snaps ([0-9]+)')
  starve1=$(echo "$hs" | field 'pilot 1 #[0-9]+ queue [0-9]+ starved ([0-9]+)')
  buffer="+$((starve1 - starve0)) starves, depth $(echo "$hs" | field 'pilot 1 #[0-9]+ queue [0-9]+ starved [0-9]+ skipped [0-9]+ target ([0-9]+)')"
  host "net hash" >/dev/null; sleep 2
  hash=$(grep -h "hash check" "$OUT_DIR"/guest1.log | tail -1 | sed -nE 's/.*, ([0-9]+) mismatched.*/\1 bad/p')
  printf "%-8s %-12s %-12s %-8s %-18s %-10s %-24s %-10s\n" "${ms}ms" "${out} kbps" "${in} kbps" "${rtt}ms" "$err" "+$((snaps - base1))" "$buffer" "$hash"
done
host "net sim 0 0 0" >/dev/null
echo
for log in "$OUT_DIR"/guest1.log "$OUT_DIR"/guest2.log; do
  echo "$(basename $log): $(grep -c 'Native Crash' "$log") crashes, $(grep -iE 'exception' "$log" | grep -vc 'GetOrCreateSystemsAndLogException') exceptions, $(grep -c 'mismatch:' "$log") hash warnings"
done
