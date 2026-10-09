#!/bin/zsh
# test.sh <editmode|playmode|perf> [filter]  Run tests and print a summary. Results: Logs/test-results/<kind>.xml
. "$(dirname "$0")/_env.sh"
kind=${1:-editmode}; filter=$2
case "$kind" in
  editmode) mode=EditMode;; playmode) mode=PlayMode;;
  perf) mode=PlayMode;;
  *) echo "usage: test.sh editmode|playmode|perf [filter]"; exit 2;;
esac
mkdir -p Logs/test-results; out="$PROJECT_ROOT/Logs/test-results/$kind.xml"
if editor_open; then
  args=(--testMode "$mode" --resultsPath "$out")
  [[ -n "$filter" ]] && args+=(--testFilter "$filter")
  [[ "$kind" == perf ]] && args+=(--testCategory Performance)
  unity command run_tests "${args[@]}" --timeout 1800 --format json > "$OUT_DIR/test-$kind.json" 2>&1
  [[ -f "$out" ]] || { echo "run_tests did not write $out:"; head -c 2000 "$OUT_DIR/test-$kind.json"; echo; echo "(close the Editor to run headless instead)"; exit 1; }
else
  args=(-batchmode -nographics -projectPath "$PROJECT_ROOT" -runTests -testPlatform "$mode" -testResults "$out" -logFile "$OUT_DIR/test-$kind.log")
  [[ -n "$filter" ]] && args+=(-testFilter "$filter")
  [[ "$kind" == perf ]] && args+=(-testCategory Performance)
  rm -f "$out"
  "$(unity_editor)" "${args[@]}" >/dev/null 2>&1
fi
python3 "$TOOLS_DIR/test_summary.py" "$out"
