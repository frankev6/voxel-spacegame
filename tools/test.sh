#!/bin/zsh
# test.sh <editmode|playmode|perf> [filter]  Run tests and print a summary. Results: Logs/test-results/<kind>.xml
# perf runs the Performance category in EditMode and PlayMode, then tools/perf_summary.py over the Logs/perf records written
# during the run (SPEC-71 §5.3); it exits non-zero if a test or a perf record failed.
. "$(dirname "$0")/_env.sh"
kind=${1:-editmode}; filter=$2

run_mode() {   # run_mode <EditMode|PlayMode> <results name> <category or "">
  local mode=$1 name=$2 category=$3
  local out="$PROJECT_ROOT/Logs/test-results/$name.xml"
  if editor_open; then
    local args=(--testMode "$mode" --resultsPath "$out")
    [[ -n "$filter" ]] && args+=(--testFilter "$filter")
    [[ -n "$category" ]] && args+=(--testCategory "$category")
    unity command run_tests "${args[@]}" --timeout 1800 --format json > "$OUT_DIR/test-$name.json" 2>&1
    [[ -f "$out" ]] || { echo "run_tests did not write $out:"; head -c 2000 "$OUT_DIR/test-$name.json"; echo; echo "(close the Editor to run headless instead)"; return 1; }
  else
    local args=(-batchmode -nographics -projectPath "$PROJECT_ROOT" -runTests -testPlatform "$mode" -testResults "$out" -logFile "$OUT_DIR/test-$name.log")
    [[ -n "$filter" ]] && args+=(-testFilter "$filter")
    [[ -n "$category" ]] && args+=(-testCategory "$category")
    rm -f "$out"
    "$(unity_editor)" "${args[@]}" >/dev/null 2>&1
  fi
  python3 "$TOOLS_DIR/test_summary.py" "$out"
}

mkdir -p Logs/test-results
case "$kind" in
  editmode) run_mode EditMode editmode "";;
  playmode) run_mode PlayMode playmode "";;
  perf)
    start=$(date +%s)
    run_mode EditMode perf-editmode Performance; s1=$?
    run_mode PlayMode perf-playmode Performance; s2=$?
    python3 "$TOOLS_DIR/perf_summary.py" --since "$start"; s3=$?
    (( s1 == 0 && s2 == 0 && s3 == 0 ));;
  *) echo "usage: test.sh editmode|playmode|perf [filter]"; exit 2;;
esac
