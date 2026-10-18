#!/bin/zsh
# hollow-map.sh [hollow-map.py args]  DEV-025: export the Hollow's flight-plane slice and overlay (Game.Editor.Content.HollowMap.Export),
# then draw Logs/hollow-map/map.png. With an open Editor the export runs in it (about a second, Play not needed; it refuses while in
# Play so it never touches a running session); without one, headless. Examples:
#   tools/hollow-map.sh                                  the whole Hollow
#   tools/hollow-map.sh --focus=-160,110,160             a 320 m square round the race portal
#   tools/hollow-map.sh --only guides,stations,paths     just the wayfinding
source "${0:A:h}/_env.sh"
cd "$PROJECT_ROOT"
if editor_open; then
  unity command editor_status --project-path "$PWD" 2>/dev/null | grep -q '"playMode":"stopped"' || { echo "hollow-map: the Editor is in Play; stop it first"; exit 1; }
  unity command eval 'Game.Editor.Content.HollowMap.Export(); return 0;' --project-path "$PWD" >/dev/null || { echo "hollow-map: export failed"; exit 1; }
else
  "$(unity_editor)" -batchmode -nographics -quit -projectPath "$PWD" -executeMethod Game.Editor.Content.HollowMap.Export -logFile Logs/tools/hollow-map.log \
    || { echo "hollow-map: headless export failed; see Logs/tools/hollow-map.log"; exit 1; }
fi
python3 tools/hollow-map.py "$@"
