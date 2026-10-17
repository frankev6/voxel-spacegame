#!/bin/zsh
# race-ai.sh <laps> [ship] [timescale=4] — enter the playground, open a race with AI, autopilot on, run it and summarise.
# Guarded so it can never hang: aborts on a new exception in the editor log, a stalled sim clock, no gate passed by anyone for
# 90 sim-seconds, or the wall-clock deadline; whatever happens it ends with trace off, timescale 1 and Play mode stopped.
cd "$(dirname "$0")/../.."; export UNITY_PROJECT_PATH=${UNITY_PROJECT_PATH:-$PWD}; G=tools/playtest/g.sh
laps=${1:-1}; ship=${2:-ship_racer_kite}; scale=${3:-4}
LOG=Logs/editor-worktree.log; log0=$(( $(wc -l < $LOG) + 1 ))
deadline=$(( $(date +%s) + 60 + laps * 400 / scale + 120 ))
cleanup() {
  $G dev --cmd "race trace off" >/dev/null 2>&1; $G dev --cmd "timescale 1" >/dev/null 2>&1
  $G editor_stop >/dev/null 2>&1; echo "= cleaned up (trace off, timescale 1, play stopped)"
}
abort() { echo "ABORT: $1"; tail -n +$log0 $LOG | grep -m1 -A6 "Exception" | cut -c1-200; cleanup; exit 2; }
tools/playtest/playground.sh 1 $ship >/dev/null 2>&1 || abort "playground failed"
$G dev --cmd "timescale $scale" | tail -1
$G dev --cmd "race open laps=$laps" | tail -1
mines0=$($G entities --component MineFuse 2>&1 | head -1)
$G dev --cmd "race autopilot on" >/dev/null; $G dev --cmd "race trace on" | tail -1; $G dev --cmd "race start" >/dev/null
lastTick=0; lastTickWall=$(date +%s); lastGates=""; gatesTick=0; startWall=$(date +%s); n=0
while true; do
  sleep 5; n=$((n+1)); now=$(date +%s)
  info=$($G dev --cmd "race info" 2>&1)
  echo "$info" | grep -q "^Finished" && break
  [ $now -gt $deadline ] && abort "wall-clock deadline ($(( now - startWall )) s)"
  exc=$(tail -n +$log0 $LOG | grep -c "Exception")
  [ $exc -gt 0 ] && abort "$exc exception lines in the editor log"
  tick=$(echo "$info" | grep -o "tick [0-9]*" | head -1 | cut -d' ' -f2)
  if [ -n "$tick" ] && [ "$tick" != "$lastTick" ]; then lastTick=$tick; lastTickWall=$now; fi
  [ $(( now - lastTickWall )) -gt 30 ] && abort "sim clock stalled at tick $lastTick for 30 s"
  gates=$(echo "$info" | grep "^slot" | sed 's/ best.*//' | tr '\n' '|')
  if [ "$gates" != "$lastGates" ]; then lastGates=$gates; gatesTick=${tick:-0}; fi
  [ -n "$tick" ] && [ $(( tick - gatesTick )) -gt 5400 ] && abort "no racer passed a gate for 90 sim-seconds: $gates"
  # progress every 30 s, with the real speed-up achieved
  if [ $((n % 6)) -eq 0 ]; then
    el=$(( now - startWall )); echo "progress ${el}s wall, tick $tick ($(( ${tick:-0} / 60 ))s sim, x$(echo "scale=1; ${tick:-0} / 60 / $el" | bc)): $(echo "$info" | grep '^slot' | sed 's/ last gate.*flags/ flags/' | tr '\n' ';' | cut -c1-220)"
  fi
done
echo "=== result"; echo "$info" | grep "^slot"; echo "$info" | head -1 | cut -c1-120
echo "mines before: $mines0 / after: $($G entities --component MineFuse 2>&1 | head -1)"
exc=$(tail -n +$log0 $LOG | grep -c "Exception"); echo "exceptions: $exc"
sleep 3; cleanup
python3 tools/playtest/race-plot.py Logs/race-trace.csv --out Logs/race-plot 2>&1 | tail -12
python3 tools/playtest/race-stats.py Logs/race-trace.csv
echo "= done"
