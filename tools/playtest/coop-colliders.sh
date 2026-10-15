#!/bin/zsh
# coop-colliders.sh <grid id> [x z ...]  Co-op check: compare one grid's cells and collider on host and guest 1. With coordinates,
# first blows a 12 m hole at each (world x z, y 4) so whole bricks empty, then waits for the collider pipeline and compares.
# Matching cell counts with different collider child counts means the guest kept collision for bricks that are gone.
. "$(dirname "$0")/_env.sh"
export UNITY_PROJECT_PATH="$PROJECT_ROOT"
G="$PT_DIR/g.sh"
id=$1; shift
while (( $# >= 2 )); do "$G" gh_dmg_hit --x $1 --y 4 --z $2 --amount 5000000 --radius 12 >/dev/null; shift 2; done
sleep 3
echo "host:  $("$G" dev --cmd "grid id=$id" | grep -E 'bricks=|collider=' | tr '\n' ' ')"
"$G" dev --cmd "net guest 1 \"grid id=$id\"" >/dev/null; sleep 1.5
echo "guest: $("$G" dev --cmd "net reply" | grep -E 'bricks=|collider=' | tail -2 | tr '\n' ' ')"
