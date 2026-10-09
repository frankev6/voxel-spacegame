# Shared setup for playtest scripts. Source it:  . "$(dirname "$0")/_env.sh"
PT_DIR="$(cd "$(dirname "${(%):-%x}" 2>/dev/null || dirname "$0")" && pwd)"
. "$PT_DIR/../_env.sh"
PT_OUT="$PROJECT_ROOT/Logs/playtest"          # gitignored; NOT Temp/ (Unity deletes Temp/ when it exits)
mkdir -p "$PT_OUT"
