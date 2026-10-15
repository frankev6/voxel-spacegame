#!/bin/zsh
# coop.sh [start|guest|stop|status]  Co-op spike loop (SPEC-90 NET-010). The open Editor on THIS worktree hosts; the dev player
# (tools/build-dev.sh, or invoke Game.Editor.Build.DevBuild.BuildMacDev) joins as a guest window.
#   start   enter the playground in the Editor, host, launch one guest, wait for its join snapshot
#   guest   launch another guest (slot 2, 3)
#   stop    quit the guests and leave Play mode
#   status  host and guest `net status`
# COOP_HEADLESS=1 launches guests with -batchmode -nographics: no window, so they keep a full tick rate while covered or while the
# screen is locked (a locked screen stalls a windowed player on presentation). Everything but rendering runs.
. "$(dirname "$0")/_env.sh"
export UNITY_PROJECT_PATH="$PROJECT_ROOT"
G="$PT_DIR/g.sh"
APP="$PROJECT_ROOT/Builds/dev/SpaceshipGame.app"
action=${1:-start}

launch_guest() {
  local n=$1 log="$OUT_DIR/guest$1.log"
  rm -f "$log"
  # macOS App Nap freezes covered windows long enough to time out (10 s); this key affects only the game's own bundle.
  defaults write com.koalastudios.spaceshipgame.v2 NSAppSleepDisabled -bool YES
  if [[ -n "$COOP_HEADLESS" ]]; then
    open -n "$APP" --args -batchmode -nographics -coop-join 127.0.0.1 -coop-name "Guest$n" -coop-bg-input -logFile "$log"
  else
    open -n "$APP" --args -coop-join 127.0.0.1 -coop-name "Guest$n" -coop-bg-input -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile "$log"
  fi
  for i in {1..60}; do
    grep -q "join snapshot complete" "$log" 2>/dev/null && { echo "guest $n joined"; return 0; }
    grep -qE "Lost the host|Could not|Version mismatch" "$log" 2>/dev/null && { grep "\[Net\]" "$log" | tail -3; return 1; }
    sleep 1
  done
  echo "guest $n did not finish joining in 60 s"; grep "\[Net\]" "$log" | tail -5; return 1
}

case "$action" in
  start)
    [[ -d "$APP" ]] || { echo "no dev player: run tools/build-dev.sh first"; exit 1; }
    "$PT_DIR/playground.sh" | head -1 || exit 1
    "$G" dev --cmd "net host" || exit 1
    launch_guest 1
    ;;
  guest) launch_guest ${2:-2} ;;
  stop)
    pkill -f "Builds/dev/SpaceshipGame.app" && echo "guests closed"
    unity command editor_stop --timeout 30 --format json >/dev/null 2>&1 && echo "play stopped"
    ;;
  status)
    "$G" dev --cmd "net status"
    "$G" dev --cmd 'net guest 1 "net status"' >/dev/null; sleep 1; "$G" dev --cmd "net reply" | tail -12
    ;;
  *) echo "usage: coop.sh [start|guest [n]|stop|status]"; exit 1 ;;
esac
