#!/bin/zsh
# waitscene.sh <SceneName> [timeout_s]  Wait until the active scene's name contains SceneName.
. "$(dirname "$0")/_env.sh"
for i in $(seq 1 ${2:-60}); do
  act=$("$PT_DIR/g.sh" eval --code 'return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;' 2>/dev/null | python3 -c "import sys,json
t=sys.stdin.read()
try: print(json.loads(t)['result'])
except Exception: print(t.strip())")
  [[ "$act" == *"$1"* ]] && { echo "scene $1 active after ${i}s"; exit 0; }
  sleep 1
done
echo "timeout waiting for $1 (active: $act)"; exit 1
