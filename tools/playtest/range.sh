#!/bin/zsh
# range.sh [seed=7] Reset the real game and spawn the modular destruction test station.
. "$(dirname "$0")/_env.sh"
"$PT_DIR/newgame.sh" "${1:-7}" || exit $?
"$PT_DIR/g.sh" dev --cmd range
