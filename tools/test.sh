#!/bin/zsh
# test.sh <editmode|playmode|perf> [filter]  Run tests and print a summary. Results: Logs/test-results/<kind>.xml
# perf runs the Performance category in EditMode and PlayMode, then tools/perf_summary.py over the Logs/perf records written
# during the run (SPEC-71 §5.3); it exits non-zero if a test or a perf record failed.
. "$(dirname "$0")/_env.sh"
if [[ "$GAME_UNITY_TOOL_LOCK_ROOT" != "${PROJECT_ROOT:A}" ]]; then
  exec python3 "$TOOLS_DIR/unity_tool_lock.py" "$PROJECT_ROOT" -- "$TOOLS_DIR/test.sh" "$@"
fi
kind=${1:-editmode}; filter=$2

run_mode() {   # run_mode <EditMode|PlayMode> <results name> <category or "">
  local mode=$1 name=$2 category=$3
  local out="$PROJECT_ROOT/Logs/test-results/$name.xml"
  local run_exit=0
  if editor_open; then
    python3 "$TOOLS_DIR/live-tests.py" "$PROJECT_ROOT" "$mode" "$filter" "$category" "$out"
    run_exit=$?
    if (( run_exit != 0 )); then
      echo "Live Unity test request failed; no result summary accepted. See $OUT_DIR/test-$name.json"
      return 1
    fi
  else
    rm -f "$out"
    local args=(-batchmode -nographics -projectPath "$PROJECT_ROOT" -runTests -testPlatform "$mode" -testResults "$out" -logFile "$OUT_DIR/test-$name.log")
    [[ -n "$filter" ]] && args+=(-testFilter "$filter")
    [[ -n "$category" ]] && args+=(-testCategory "$category")
    "$(unity_editor)" "${args[@]}" >/dev/null 2>&1
    run_exit=$?
  fi
  python3 "$TOOLS_DIR/test_summary.py" "$out"
  local summary_exit=$?
  if (( run_exit != 0 )); then
    echo "Unity test process/command failed (exit $run_exit); see $OUT_DIR/test-$name.log or .json"
    return 1
  fi
  return $summary_exit
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
