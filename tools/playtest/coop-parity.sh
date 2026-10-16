#!/bin/zsh
# coop-parity.sh [slot]  ADR-0117 §7 parity check: the host's and guest <slot>'s view (default 1) of that guest's ship and the world
# around it: cell hash, working blocks, collider parts, ability slots, shield, combat status, interaction target, grid, hulk,
# salvageable and collected counts, faction table. Poses, timers and distances are left out (they lag by design). Prints every key
# that differs and exits 1, or prints "parity: no differences".
. "$(dirname "$0")/_env.sh"
export UNITY_PROJECT_PATH="$PROJECT_ROOT"
G="$PT_DIR/g.sh"
slot=${1:-1}
host=$("$G" dev --cmd "net parity $slot" | grep -o 'parity .*' | head -1)
"$G" dev --cmd "net guest $slot \"net parity\"" >/dev/null
sleep 2
guest=$("$G" dev --cmd "net reply" | grep -o 'parity .*' | tail -1)
if [[ -z "$host" || -z "$guest" ]]; then echo "parity: no reading (host '${host}', guest '${guest}'); is a session running?"; exit 2; fi
python3 - "$host" "$guest" <<'EOF'
import sys
def parse(line): return dict(token.split('=', 1) for token in line.split()[1:] if '=' in token)
host, guest = parse(sys.argv[1]), parse(sys.argv[2])
diff = [(k, host.get(k, '(none)'), guest.get(k, '(none)')) for k in sorted(set(host) | set(guest)) if host.get(k) != guest.get(k)]
for key, h, g in diff: print(f"  {key}: host {h}  guest {g}")
print("parity: no differences" if not diff else f"parity: {len(diff)} difference(s)")
sys.exit(1 if diff else 0)
EOF
