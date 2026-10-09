#!/bin/zsh
# shot.sh <name> [--width W --height H]  Capture the Game view to Logs/playtest/<name>.png.
# Fetches the image inline: capture_game_view --save_path would write under Assets/ and create .meta files.
. "$(dirname "$0")/_env.sh"
name=$1; shift
unity command capture_game_view --width 1280 --height 720 "$@" --format json 2>&1 | python3 -c "
import json,sys,base64
d=json.load(sys.stdin)
if not d.get('success'): print('ERR', d.get('errors')); sys.exit(1)
r=d['data']['result']
b=r.get('data') or r.get('base64') or r.get('image') or r.get('png')
open('$PT_OUT/$name.png','wb').write(base64.b64decode(b)); print('$PT_OUT/$name.png')"
