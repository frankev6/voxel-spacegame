# 00 — Vision

**Status: BINDING for scope. Owner: Frank.** Working title: *Spaceship Game V2* (placeholder name).

## The one-sentence game

You build your ship block by block, each block doing something different, and travel from star system to star system across a galaxy, uncovering a mystery — in a world where massive ships and stations are made of individually destructible voxels that break apart realistically when they ram, burn and explode.

## Pillars (in priority order)

1. **Destruction you can read.** Every hit removes real cells. Ships lose the parts that were hit and fly differently afterwards. Big impacts tear off chunks that tumble away with momentum. Nothing pops out of existence; wrecks stay as hulks.
2. **Your ship is your progression.** Blocks are the reward. Thrusters, weapons, reactors, shields and structure each change what the ship can do. Building is fast, readable and undoable, and every stat change is visible before you commit.
3. **Massive scale.** Stations and capital ships of hundreds of thousands to a million cells are normal, not a tech demo. Performance targets in [06-performance-budgets.md](06-performance-budgets.md) are hard gates.
4. **A galaxy with a secret.** Star systems are generated from a seed, but the mystery is authored: clues found in places point to other places. Knowledge, not just gear, opens the way forward (lanes and gates unlock through facts).
5. **Coherent look.** One art direction for ships, space, effects and UI, approved by the owner before production art starts ([SPEC-80](specs/SPEC-80-art-direction.md)).

## The core loop

1. Fly in a star system (planar flight, third-person camera).
2. Explore points of interest, trade, talk, pick up rumors and clues.
3. Fight: weapons carve cells; ramming and explosions break structures.
4. Salvage blocks and resources from wrecks and loot.
5. Dock and edit your ship with what you have.
6. Jump along lanes to the next star system the clues point to.

## Scope for the first playable milestone (M3, "vertical slice")

- Two to four star systems connected by lanes, one hub station, one derelict, one hostile encounter type.
- Player ship built from at least 12 block types (hull, armour slope, core, reactor, capacitor, 2 thrusters, 3 weapons, shield emitter, a cargo/utility block).
- Full destruction pipeline: cell damage, splitting into debris, chunk bodies and hulks, ramming.
- Blueprint Lab editor with undo/redo and commit against inventory.
- Save and load anywhere outside combat.
- The opening of the mystery: wake-up, first clue, first gated lane.
- All art may still be placeholder at M3 unless SPEC-80 is approved earlier.

## Explicit non-goals for v1

- Multiplayer or networking of any kind.
- Planet surfaces, landing, ship interiors, walking on foot.
- Modding support.
- Localization (re-added later through Yarn line IDs).
- Console platforms and touch input.
- Free 6-DOF flight. Ships fly on a plane (ADR-0003); the plane lock is isolated so this can be revisited.

## Where the old prototype fits

The old repo (`spaceship-game`) is a **reference only**: for feel, content and writing. No code is copied from it. Its content was exported to neutral data in `Import/Legacy/` and is converted by [SPEC-72](specs/SPEC-72-legacy-migration.md). Its audit lives in `docs/reference/audit-2026-10/` as background on *why* this architecture exists. Where the audit and a spec disagree, the spec wins.
