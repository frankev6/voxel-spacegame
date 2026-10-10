# ADR-0017 — Small craft are voxel grids; groups think, members follow

**Status:** Accepted (owner, 2026-10-10; extensibility review X7)

## Context
Carriers that launch fighter swarms, and later drones and escorts, need many small combatants at once. Before any spec builds them, three things had to be settled: how they are represented, how their AI scales, and how launches avoid colliding with the carrier.

## Decision
1. **Representation.** A small craft is a normal voxel grid, usually one brick, spawned with `GridProfile.SmallCraft` (ADR-0015).
   - It is destructible like everything else and reuses every rendering, physics, damage, power and weapon path.
   - A non-voxel `DroneStore` (struct-of-arrays rows like `ProjectileStore`, with steering, HP and a tiny weapon through `CombatHitTest`) stays a documented fallback, used only if the gate below cannot be met. Both are never built.
2. **Group AI from M3.** `FleetMember` (SPEC-23) is the group mechanism for encounter fleets and carrier wings alike, and moves from M4 to M3.
   - Members with a live leader skip target choice and follow the leader's target and formation. Only leaders (and lone ships) run the full think, so AI decision cost is O(groups), not O(craft).
   - Player orders to the player's own wing use a group order on the leader, which the carrier spec defines.
3. **Launch overlap.** `GridStowApi.Unstow` can give the craft `SplitGrace { Other = carrier, UntilTick }` (SPEC-13, enforced by SPEC-12's `SplitGraceContactSystem`). Craft and carrier ignore each other's contacts until the craft has cleared the bay. No collision-filter changes are needed.
4. **Gates (06).**
   - "Swarm: 30 ships + 100 small craft (≤ 1 brick each, in groups of 10), all fighting: main thread ≤ 10 ms median; AI ≤ 1.5 ms including craft".
   - "Launch 20 stowed craft in one tick: no frame over 8 ms".

## Consequences
- The carrier spec (future) adds only its module, systems and content (review §4).
- **Spec changes:**
  - SPEC-23: fleet tasks move to M3; members skip target choice; the swarm gate test.
  - SPEC-10: `Unstow` grace parameters.
  - SPEC-13: `SplitGrace` gains a second initialiser.
  - 06: two gate rows.
- If the swarm gate fails at M3, the owner decides between more per-grid optimisation and the `DroneStore` fallback, with measurements in the ledger.

## Alternatives considered
- **Drone store first.** Rejected for v1: it creates a second combatant model, with no voxel destruction and a separate hit, render and save path.
- **A collision-filter group index for launches.** Rejected: grid colliders are cached per brick and owned by `ColliderSwapSystem`, so changing their filters at runtime would add a writer. Contact suppression already exists.
