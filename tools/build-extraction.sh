#!/bin/zsh
# build-extraction.sh  EXT-007/008: export Tollhouse Nine (ArtSource/Places/lantern_export.py), then build its blueprints and the Rock
# den's (Game/Content/Build Lantern and Den). With an open Editor the build runs in it; without one, headless.
source "${0:A:h}/_env.sh"
cd "$PROJECT_ROOT"
python3 ArtSource/Places/lantern_export.py || exit 1
if editor_open; then
  unity command eval 'Game.Editor.Content.LanternBuilder.Build(); return 0;' --project-path "$PWD" >/dev/null || { echo "build-extraction: build failed"; exit 1; }
else
  "$(unity_editor)" -batchmode -nographics -quit -projectPath "$PWD" -executeMethod Game.Editor.Content.LanternBuilder.Build -logFile Logs/tools/build-extraction.log \
    || { echo "build-extraction: headless build failed; see Logs/tools/build-extraction.log"; exit 1; }
fi
grep -E "Built Tollhouse|did not fit|Exception" Logs/tools/build-extraction.log 2>/dev/null | head -20
