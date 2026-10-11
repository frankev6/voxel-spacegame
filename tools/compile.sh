#!/bin/zsh
# compile.sh  Compile the project and print C# errors. Exit 1 on any error.
#   Editor open  -> recompile through the Unity CLI (seconds).
#   Editor closed -> headless batch import + compile (slower; first run imports everything).
. "$(dirname "$0")/_env.sh"
if [[ "$GAME_UNITY_TOOL_LOCK_ROOT" != "${PROJECT_ROOT:A}" ]]; then
  exec python3 "$TOOLS_DIR/unity_tool_lock.py" "$PROJECT_ROOT" -- "$TOOLS_DIR/compile.sh" "$@"
fi
if editor_open; then
  setopt pipefail
  "$TOOLS_DIR/playtest/rc.sh" 2>&1 | tee "$OUT_DIR/compile.log"
  exit $?
fi
log="$OUT_DIR/compile.log"
echo "No open Editor for this project: running a headless batch compile (log: $log)"
"$(unity_editor)" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" -logFile "$log" -executeMethod Game.Editor.ProjectSetup.VerifyCompileAndQuit >/dev/null 2>&1
code=$?
errs=$(grep -E "error CS[0-9]+" "$log" | sed -E 's/^.*((Assets|Packages)\/)/\1/' | sort -u)
if [[ -n "$errs" ]]; then echo "$errs" | head -60; echo "COMPILE FAILED"; exit 1; fi
if grep -q "VerifyCompileAndQuit: OK" "$log"; then echo "COMPILE OK"; exit 0; fi
echo "Unity exited with code $code and no OK marker; tail of log:"; tail -30 "$log"; exit 1
