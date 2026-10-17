#!/bin/zsh
# build-windows.sh  Build the Windows 64-bit development player into Builds/windows/ and zip it for a remote co-op guest (RACE-010).
#   Needs Unity Hub → Installs → this Unity version → Add modules → "Windows Build Support (Mono)", and the project closed in every
#   Editor (batch mode). Output: Builds/SpaceshipGame-windows-dev.zip. Log: Logs/tools/build-windows.log
. "$(dirname "$0")/_env.sh"
if [[ "$GAME_UNITY_TOOL_LOCK_ROOT" != "${PROJECT_ROOT:A}" ]]; then
  exec python3 "$TOOLS_DIR/unity_tool_lock.py" "$PROJECT_ROOT" -- "$TOOLS_DIR/build-windows.sh" "$@"
fi
if editor_open; then echo "Close the Editor on this project first (batch builds cannot share it)"; exit 1; fi
log="$OUT_DIR/build-windows.log"
echo "Building the Windows development player (log: $log)"
"$(unity_editor)" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" -logFile "$log" -buildTarget Win64 -executeMethod Game.Editor.Build.DevBuild.BuildWindowsDevAndQuit >/dev/null 2>&1
code=$?
grep -E "error CS[0-9]+" "$log" | sed -E 's/^.*((Assets|Packages)\/)/\1/' | sort -u | head -40
grep -E "DevBuild:" "$log" | tail -1
[[ $code -ne 0 ]] && exit $code
( cd "$PROJECT_ROOT/Builds" && rm -f SpaceshipGame-windows-dev.zip && zip -qr SpaceshipGame-windows-dev.zip windows -x "*_BackUpThisFolder_ButDontShipItWithYourGame/*" ) && echo "Zipped: $PROJECT_ROOT/Builds/SpaceshipGame-windows-dev.zip"
