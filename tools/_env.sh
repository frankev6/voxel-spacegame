# Shared setup for tools/*.sh. Source it:  . "$(dirname "$0")/_env.sh"
# Finds the project root from this file's location, puts the Unity CLI on PATH, cds to the root.
TOOLS_DIR="$(cd "$(dirname "${(%):-%x}" 2>/dev/null || dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$TOOLS_DIR" && while [[ ! -d ProjectSettings && "$PWD" != "/" ]]; do cd ..; done; pwd)"
export PATH="$HOME/.unity/bin:$PATH"
export UNITY_NO_BANNER=1
OUT_DIR="$PROJECT_ROOT/Logs/tools"            # gitignored; NOT Temp/ (Unity deletes Temp/ when it exits)
mkdir -p "$OUT_DIR"
cd "$PROJECT_ROOT"

# editor_open: 0 if an Editor with a reachable Pipeline server has THIS project open
editor_open() {
  unity status --format json 2>/dev/null | python3 -c "
import json,sys,os
try: d=json.load(sys.stdin)
except Exception: sys.exit(1)
data = d.get('data', d) if isinstance(d,dict) else d
rows = data if isinstance(data,list) else data.get('instances', [])
root=os.path.realpath('$PROJECT_ROOT')
ok=any(os.path.realpath(str(r.get('project') or r.get('projectPath') or ''))==root and str(r.get('state','')).lower() in ('ready','playing','compiling','reloading') for r in rows if isinstance(r,dict))
sys.exit(0 if ok else 1)"
}

# unity_editor: path of the Editor binary for this project's version (Hub install location; override with UNITY_EDITOR)
unity_editor() {
  if [[ -n "$UNITY_EDITOR" ]]; then echo "$UNITY_EDITOR"; return; fi
  local v=$(sed -n 's/^m_EditorVersion: //p' "$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt")
  for base in "/Applications/Unity/Hub/Editor/$v/Unity.app/Contents/MacOS/Unity" "$HOME/Applications/Unity/Hub/Editor/$v/Unity.app/Contents/MacOS/Unity" "/c/Program Files/Unity/Hub/Editor/$v/Editor/Unity.exe"; do
    [[ -x "$base" ]] && { echo "$base"; return; }
  done
  unity editors path "$v" 2>/dev/null | head -1
}
# Headless runs call the Editor binary directly: the CLI's `unity run`/`unity test` wait on its sign-in helper,
# which can hang in non-interactive shells. Live-Editor commands (`unity command ...`) are unaffected.
