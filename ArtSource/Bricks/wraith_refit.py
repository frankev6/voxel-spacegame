"""WRAITH refit: the engines and manoeuvring thrusters of the NOCTURNE frigate (owner, 2026-10-18: "we have to improve the
wraith's engines and controls, its hard to maneuver especially for a small ships thats is meant to be highly maneverable").

The look-book design (ld_slopes.wraith_rebuilt) carried only stern thrusters, so strafing, reversing and most of its turning
came from the core's assist. The refit swaps the stern thrusters for the ADR-0120 sprint engines and adds four lateral pods
(bow and stern, both sides) and two reverse pods at the shoulders, each with its nozzle named so the blueprint builder seats it
facing out (NocturneShipBuilder: a sixth module entry is the nozzle direction in look-book axes, x right, y forward, z up).

export_lookbook_ships.py applies it when it writes ship_nocturne_wraith.json; run this file on its own to patch the JSON in place:
    python3 ArtSource/Bricks/wraith_refit.py
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
JSON = os.path.join(HERE, "ships", "ship_nocturne_wraith.json")
IDENTITY = [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]

# kind, box minimum and size in look-book cells, nozzle direction
ENGINES = {"thruster_l": "sprint_m", "thruster_s": "sprint_s"}
AFT = [0, -1, 0]
PODS = [
    ("rcs", [3, 22, 2], [1, 4, 4], [1, 0, 0]),     # bow, right side: pushes the bow left
    ("rcs", [-4, 22, 2], [1, 4, 4], [-1, 0, 0]),   # bow, left side
    ("rcs", [4, -6, 2], [1, 4, 4], [1, 0, 0]),     # stern, right side
    ("rcs", [-5, -6, 2], [1, 4, 4], [-1, 0, 0]),   # stern, left side
    ("rcs", [2, 27, 2], [2, 1, 4], [0, 1, 0]),     # shoulders, facing forward: reverse thrust
    ("rcs", [-4, 27, 2], [2, 1, 4], [0, 1, 0]),
]


def apply(mods):
    """The refitted module list: stern engines upgraded and aimed aft, the manoeuvring pods added (idempotent)."""
    out = []
    for m in mods:
        kind = m[0]
        if kind in ENGINES or kind in ENGINES.values():
            out.append([ENGINES.get(kind, kind), m[1], m[2], m[3], m[4], AFT])
        elif kind == "rcs":
            continue   # re-added below
        else:
            out.append(m)
    for kind, mn, size, nozzle in PODS:
        out.append([kind, mn, IDENTITY, size, size, nozzle])
    return out


if __name__ == "__main__":
    with open(JSON) as f:
        d = json.load(f)
    d["mods"] = apply(d["mods"])
    with open(JSON, "w") as f:
        json.dump(d, f, separators=(",", ":"))
    print("WRAITH refit: %d modules -> %s" % (len(d["mods"]), JSON))
