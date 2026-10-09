#!/bin/zsh
# to.sh <seconds> <cmd...>  Run a command with a hard timeout (macOS has no `timeout`).
secs=$1; shift
perl -e 'alarm shift; exec @ARGV' "$secs" "$@"
