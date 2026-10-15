# ADR-0117 — Co-op: one code path for every effect (authority decides, every machine applies)

**Status:** Accepted for the spike (2026-10-15). Lives with ADR-0116 on `task/NET-001-coop-prototype` in the `spaceship-game-v2-coop` worktree. Owner, after guests hit collision shapes of blocks that were gone and missed salvage tints, prompts and ability icons: "i think we have to decouple the systems and make sure that host, guest, or single player, they go through the same functions so we are not running redundant or duplicate code", and "this doesn't go just for the destruction system, for every system".

## Context
ADR-0116 §4 switches whole systems off on guests (`AuthorityOnly`) and §7–10 replicate their results. Many of those systems mix two jobs:
- **deciding** what happens: damage math, integrity, which islands split, core loss, AI, ability activation, economy;
- **applying** the consequences: writing cells, marking bricks dirty, deleting empty bricks after their collider removal is queued, debris and sound, functional entities, hulk components, chunk bodies, interaction tags, ability slot state.

Turning a system off on a guest removed both jobs. The spike then rebuilt the consequences it knew about in guest-only code, and every consequence it missed became a guest bug. Single-player fixes never reach those copies.

| Single-player owner | Guest copy | Bug it caused |
|---|---|---|
| `CellWriter.RemoveCell` (cells, dirty flags, neighbours, deferred brick destroy, `CellRemoved`) | `ReplicaVoxelApi.WriteBrick`, `TouchNeighbours`, `QueueColliderRemoval` | ghost collisions with removed bricks (fixed in 18769d21 by adding one more copied step) |
| `EmitDebrisJob` | `ReplicaVoxelApi.EmitDebris` | debris for detached pods (fixed by another copied flag) |
| `CheckInstance` / `ShipDeathJob` destroy pass | `ReplicaVoxelApi.SetInstance`, `ReleaseOrphanedInstances` | a duplicate thruster hanging where a pod split off |
| `ShipDeathJob` hulk conversion | `GuestReplicator` adds `Hulk`, `ReplicaVoxelApi.EmitHulkBlast` | no core blast on guests |
| `RecordChunkJob`, `ChunkLifetimeJob` | `ReplicaVoxelApi.CreateChunk` / `DestroyChunk` | double-freed chunk collider (guest crash) |
| `ImpactEventSystem` impulses | `ReplicaDamageDrainSystem` impulse apply | (copy; same risk) |
| `GridMassUpdateSystem` from `CellRemoved` | `ReplicaVoxelApi.RequestMassRecompute` | (copy) |
| `PlayerCommandApplySystem` | `RemotePilotApplySystem` | (copy of the clamping and edge rules) |
| `PlayerTargetingSystem` (only `PlayerShip`) | `RemotePilotReceiveSystem` writes `ShipTarget` | guest autofire did nothing on the host |
| `SalvageMarkSystem`, `SalvageReachSystem` (host only) | none | no salvage tint, prompt or reach on guests |
| `BodyGridSpawner`, `EncounterDirectorService` add `Interactable` | none | no dock, hail or inspect prompts on guests |
| `AbilitySystem` fills `AbilitySlot` (host only) | none | empty ability hotbar on guests |
| `InteractionQuerySystem`, `SalvageService` use the one `PlayerShip` | none | a guest's F press does nothing on the host |

There is a second form of the same problem: host simulation assumes one player (`PlayerShip` and `PlayerCommand` singletons). Guest ships on the host get parallel code (`RemotePilot*`) or nothing. The code paths are `InteractionQuerySystem`, `SalvageReachSystem`, `SalvageService`, `PlayerCombatStatusSystem`, `FactionStandingSystem`, `PlayerTargetingSystem`, `EncounterDirectorService`, `StarSystemLoader`, `TravelService`, `OriginRebaseSystem` and `NarrativePlayerDamage`.

## Decision
1. **Every gameplay system is a decider or an applier, never both.**
   - A decider computes a decision from inputs (damage outcome, split plan, core loss, AI choice, ability activation, a sale) and writes it as data: a request, a stream record or replicated state.
   - An applier turns decisions and state into every consequence.
   - A system that does both today is split in two.
2. **Deciders are `AuthorityOnly`; appliers run on every machine.** On solo and host, deciders feed appliers directly. On a guest, `Game.Net` feeds the same appliers with the same records, rebuilt from the wire. Appliers do not know which machine they run on.
3. **Derived state is derived on every machine.** Anything that is a pure function of replicated state runs everywhere, for example "a hulk is salvageable" or "magnet blocks give this reach". It is never replicated and never copied.
4. **`Game.Net` only transports and translates.** It may decode wire data into the records appliers consume, set poses and velocities of replicas, predict the own ship and run its own bookkeeping. It may not write cells, add or remove gameplay components, create or destroy gameplay entities, or emit FX or SFX directly. `ReplicaVoxelApi` shrinks to reads (hash, brick read) and record producers, then goes away.
5. **No single-player assumptions in simulation.** Every player's ship carries `Pilot { Slot }` on every machine. One machine-local tag, `LocalPilot`, marks the ship this machine controls. Simulation and galaxy code iterates pilots; only presentation and input read `LocalPilot`. `RemotePilot*` merges into the one pilot path. A pilot's input comes from local input or from the network, and nothing downstream can tell which.
6. **Replication carries decisions and state, not consequences.** The voxel stream stays a diff of exact cell values (ADR-0116 §7, robust and self-correcting), but each brick write carries its removal reason so appliers can treat it as the host did. Structural transitions travel as the same records the host's deciders produce: core loss, hulk split with its instance remap, chunk creation with cells, pose and velocity. Ability slots, interaction targets per pilot, faction relations and combat status travel as compact state.
7. **Enforcement.**
   - `RoleSystemPolicy` marks each authority-only entry with the decision it owns.
   - A test fails when `Game.Net` calls a structural voxel or gameplay writer.
   - `tools/playtest/coop-parity.sh` diffs the host's and a guest's view of the same ship: HUD snapshot, interaction target, collider parts, hulk and interactable counts, ability slots. Co-op work is not done until it shows no diff.

## Phases (SPEC-90 NET-013 onwards)
1. **NET-013, shared cell apply.**
   - Extract `CellApply` from `CellWriter` as the one cell writer with all consequences.
   - Guests feed it exact writes inside the fixed step, so debris, sound, mass, dirty bricks, colliders and brick deletion run through the host's own systems.
   - Move debris emission and the final destroy pass out of `SplitSystem` and `ShipDeathSystem` into appliers that run everywhere.
   - Delete the guest copies.
2. **NET-014, structural transitions.**
   - Split core loss, hulk split, chunk creation and lifetime, and impulses into decider and applier.
   - Guests receive the records and run the appliers.
   - Spawn-time root setup is one shared function.
   - Salvage marking runs everywhere.
3. **NET-015, pilots.** Add `Pilot` and `LocalPilot`, one command-apply path, and per-pilot targeting, interaction, salvage reach and combat status. A guest can salvage.
4. **NET-016, abilities and status.** Activation decides on the host. Slot state is replicated. Effects (shield toggle, stat boosts) are appliers everywhere, so guest prediction includes boosts and the hotbar works.
5. **NET-017, galaxy state for guests.** Faction relations, economy notices, encounter and body interactables, cargo for whoever salvages (the owner picks the cargo model, SPEC-90 §10 Q5).
6. **NET-018, enforcement.** Add the role-policy annotations, the `Game.Net` writer test and `coop-parity.sh`.

## Implementation notes
1. **NET-013 (2026-10-15).**
   - `CellWriter` gained `ApplyExactCell` and `ApplyExactInstance`:
     - a cell becoming air goes through `RemoveCell`;
     - a placement marks the brick and its neighbours like `PlaceCell`;
     - an instance that stops working queues its functional entity on the destroy list.
   - `ExactCellApplySystem` (every role, right after `DamageResolveSystem`) runs them in a Burst job and publishes into the same lists the resolver's flatten fills.
   - Those lists are now cleared at the tick reset instead of in the flatten, so either source can append.
   - Debris and break sounds moved from `SplitSystem` into `DebrisEmitSystem`. The final destruction pass moved from `ShipDeathSystem` into `StructuralDestroySystem`, with dissolved grids handed over in `DamageStreamsData.DissolveDespawns`. Both run on every role.
   - A guest decodes `GridDelta` into exact writes, cells first, then instance health. Removals in a brick the host flagged quiet carry `DetachedBody`.
   - Exact writes reach `DamageStreams.QueueExact` at frame start. Hash checks and despawns wait while writes are pending.
   - Debris from an exact write has no hit behind it, so it kicks outward from the centre of the removed cells, using the first damage type's kick speeds.
   - Deleted:
     - `ReplicaVoxelApi.WriteBrick`, `SetInstance`, `ReleaseOrphanedInstances`, `EmitDebris`, `RequestMassRecompute`, `QueueColliderRemoval` and `TouchNeighbours`;
     - `ReplicaJobs.WriteDense` and `ContainsRef`.
   - Mass now updates on guests from `CellRemoved` like the host's, not from full recomputes.
2. **NET-014 (2026-10-15).**
   - **Impulses.** `ImpulseApplySystem` (every role) applies damage impulses and requested impulses, which was the first half of `ImpactEventSystem`. Ram damage stays authority-only. `ReplicaDamageDrainSystem` now only drains the damage and restore channels.
   - **Core loss.**
     - `ShipDeathJob` only decides: it queues a `HulkTransition`, with `HulkInfo.CoreLocal` (new) holding the core's position in the grid frame.
     - `HulkTransitionSystem` (every role) applies it: hulk components through `HulkSetup.Add`, the kind, and the breach explosion request and sound. `RecordHulkJob` uses `HulkSetup.Add` too.
     - Guests queue the host's transitions, from `GridState` (with blast) and from spawn headers (no blast), through `DamageStreams.QueueHulk`.
     - On a guest the breach explosion's damage is drained like any cosmetic shot's, so only its effects show. `ReplicaVoxelApi.EmitHulkBlast` is deleted.
   - **Chunk bodies.** `ChunkBodySetup` holds the archetype and the component writes, for `RecordChunkJob` and the guest's replicas alike. Only the mass (kinematic on guests) and graphical smoothing differ. The collider comes from the host's chunk box builder.
   - **Protocol 6.** Spawn headers and `GridState` carry `HulkInfo`.
   - **Still separate, recorded for later phases:**
     - split-off hulks are built by `RecordHulkJob` on the authority but spawned from their structure on guests, so guests recreate the functional blocks instead of moving them;
     - ram contact sparks and sounds come only from the authority's ram damage job;
     - the debris shown when a full chunk pool evicts a live chunk is the authority's only;
     - spawn-time root setup is not yet one function.
3. **Headless guests.** A player started with `-nographics` no longer waits for a voxel renderer before spawning grids (`VoxelConfigFactory`). `COOP_HEADLESS=1 coop.sh start` runs guests that way, so checks keep a full tick rate while windows are covered or the screen is locked. A locked screen stalls a windowed player on presentation.

## Consequences
- Single-player behaviour must stay identical. Each split is a refactor of existing systems, checked by the existing damage, integrity, split, ability and interaction tests before co-op is touched.
- System order changes: new applier systems sit where the code they came from ran, so `SystemGroupOrderTests` and the role-policy table both change.
- Guests apply cells in the fixed step, not at frame start. A hash check waits until the writes before it have been applied.
- The spike's earlier guest code mostly gets deleted. The diff is large, but each phase is shippable on its own.

## Alternatives rejected
- **Keep the copies and test each consequence (option 3 of the 2026-10-15 review):** cheapest, but copies drift from single player and gaps keep being found one at a time.
- **Guests run the deciders on replicated hits:** removes every copy, but needs bit-identical results across machines (fixed-point damage, deterministic job completion). ADR-0116 rejected it for the same reason.
