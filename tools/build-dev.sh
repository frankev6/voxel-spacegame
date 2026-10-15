#!/bin/zsh
# build-dev.sh  Build a standalone macOS development player into Builds/dev/SpaceshipGame.app (SPEC-90 NET-002).
#   Needs the project closed in every Editor (batch mode). Log: Logs/tools/build-dev.log
#   Run a co-op guest:  open -n Builds/dev/SpaceshipGame.app --args -coop-join 127.0.0.1 -screen-fullscreen 0 -screen-width 1280 -screen-height 720
. "$(dirname "$0")/_env.sh"
if [[ "$GAME_UNITY_TOOL_LOCK_ROOT" != "${PROJECT_ROOT:A}" ]]; then
  exec python3 "$TOOLS_DIR/unity_tool_lock.py" "$PROJECT_ROOT" -- "$TOOLS_DIR/build-dev.sh" "$@"
fi
if editor_open; then echo "Close the Editor on this project first (batch builds cannot share it)"; exit 1; fi
log="$OUT_DIR/build-dev.log"
echo "Building the development player (log: $log)"
"$(unity_editor)" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" -logFile "$log" -buildTarget OSXUniversal -executeMethod Game.Editor.Build.DevBuild.BuildMacDevAndQuit >/dev/null 2>&1
code=$?
grep -E "error CS[0-9]+" "$log" | sed -E 's/^.*((Assets|Packages)\/)/\1/' | sort -u | head -40
grep "DevBuild:" "$log" | tail -1
exit $code
