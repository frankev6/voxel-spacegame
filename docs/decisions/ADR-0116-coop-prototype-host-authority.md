# ADR-0116 — Co-op prototype: host authority, replica guests, voxel delta stream

**Status:** Accepted (2026-10-16): merged to `main` with the owner's approval, "fix the issues you found, im happy with the multiplayer so far, my biggest question originally with this was if its feasable at all with so many voxels and you proved it was. lets merge these changes back into main so we can continue". Before that, proposed (2026-10-15) as a spike: lives on `task/NET-001-coop-prototype` in the `spaceship-game-v2-coop` worktree and never merges to `main` without the owner's approval. Owner request, verbatim: "make a prototype in a separate worktree i don't want to touch main for this testing at all ... the goal is to have a working coop with combat and multiple ships, large chunk breaks all working on both sides without really obvious error, we can add a million tests and optimize later but the focus is building out implementation with proper architecture".

## Context
- [00-vision](../00-vision.md) and architecture §2 put multiplayer out of scope for v1. This ADR does not change that on `main`. It records how the spike is built so it can be judged, and kept or thrown away, as a whole.
- Determinism holds on one machine only (architecture §2). Damage randomness seeds from `Entity.Index` (`DamageMath`, `RecordChunkJob`, `DissolveJob`), and `ColliderSwapSystem` installs colliders when a job reports `IsCompleted`, so two machines diverge within ticks. Lockstep is ruled out.
- Teardown (Voxagon, March 2026) shipped co-op voxel destruction as a hybrid: deterministic destruction commands over a reliable stream, with transforms state-synced over an unreliable one at about 1 Mbit per client.
- The built-in packages in Unity 6000.6.5f1 include `com.unity.transport` 6.6.0, `com.unity.netcode` 6.6.0 and `com.unity.multiplayer.playmode` 3.0.0.

## Decision
1. **Topology: one player hosts, 2–4 players total.** The host runs the full single-player simulation unchanged. Guests run the same build in **replica** mode: they render, predict nothing yet, and send only their ship commands. There are no dedicated servers.
2. **Library: Unity Transport directly; not Netcode for Entities.** NfE needs separate client and server worlds, doubling voxel memory on the host, and its ghosts do not fit 16 KB bricks. The spike keeps the single default `World` (architecture §4.3) and writes a thin replication layer. Transport's simulator pipeline provides latency, jitter and loss for testing.
3. **Role.** The `SimRole` singleton (`Solo`, `Host`, `Guest`) is created by `Game.App`. `Solo` and `Host` behave exactly like today's game. Single-player pays nothing: with no session, no network system runs.
4. **Role policy, in one place.** `Game.App` owns `RoleSystemPolicy`, a table classifying every system in the fixed step and in `WorldGroup` as `Both`, `AuthorityOnly` or `GuestOnly`. On a guest it disables the `AuthorityOnly` systems. These include AI, weapon fire and hits, damage resolution, integrity, split, ship death, impact events, the encounter director, streaming spawns and origin rebasing. A test fails when a system is unclassified, so new systems cannot slip through. No module outside `Game.Net` and `Game.App` changes behaviour by role.
5. **Identity.** `PersistentId` is the network identity of every grid root, hulks included. Chunk bodies use `(pool slot, spawn tick)`. Projectiles have no network identity.
6. **Channels.** Each guest connection has three pipelines:
   - **reliable ordered** with fragmentation, for spawns, despawns, cell deltas, splits and events;
   - **unreliable**, for transform snapshots at 30 Hz, prioritised by distance and recency;
   - **reliable ordered** for guest-to-host ship commands, carrying the latest and the previous command against loss.

   Large blobs (join snapshots) are split into chunks with acknowledgement flow control.
7. **Voxel replication: brick diffs against a shadow copy.** After the tick's structural sync, the host diffs every brick changed this tick against its last-sent copy and sends the changed `Cell` values. The diff catches every cell writer (damage, restores, dissolve, edits), so no writer needs a network hook. A brick with more than a quarter of its cells changed is sent whole. Guests never resolve damage.
8. **Splits.** A new hulk is a spawn message carrying the hulk's VOXS structure, sent after the grid exists on the host. The parent's detached cells arrive as ordinary air in the brick diff of the same tick. A chunk body is a spawn message with its `ChunkCell` list, pose and velocities, and the guest builds its collider through the existing chunk builder.
9. **Join.** Joining sends one snapshot: every replicated grid as either a `Blueprint` hash, when its cells are unchanged since spawn, or a VOXS structure, plus pose, velocity, kind, profile and allegiance, plus live chunk bodies and the host tick. The delta stream starts at the snapshot tick + 1 on the same reliable channel, so ordering is free.
10. **Guest presentation without authority.** Guests synthesise `CellRemoved`-equivalent records from diffs where a cell became air, so debris, VFX and audio run through the existing presentation paths. Shots, beams, explosions and shield hits arrive as cosmetic events: tracers and impacts only, never damage.
11. **Space.** Guests adopt the host's `FrameOrigin`, sent with each snapshot, and never rebase on their own. The spike runs in the playground, so the party is always within a few kilometres of the origin.

## New single writers (architecture §8, spike only)
| State | Writer on a guest | Notes |
|---|---|---|
| Cell contents, `Cell.Hp`, `VoxelGrid.Revision`, `BrickDirty` | `ReplicaCellApplySystem` | `DamageResolveSystem`, `IntegritySystem` and `SplitSystem` are disabled on guests |
| `LocalTransform`, `PhysicsVelocity` of replicated bodies | `ReplicaBodySyncSystem` | Remote bodies are kinematic on guests |
| Grid and chunk spawn and despawn | `ReplicaSpawnSystem`, through `GridSpawnRequest` and the existing pipeline | Streaming and encounter spawns are disabled on guests |

| State | Writer on the host | Notes |
|---|---|---|
| `ShipCommand` of a guest-piloted ship | `RemotePilotApplySystem` | Same clamping and edge rules as `PlayerCommandApplySystem` |

## Assemblies
A new `Game.Net` module (`Assets/Game/Net/`) may reference Core, Data, Voxel and Simulation, plus Entities, Collections, Burst, Mathematics, Unity Physics and Unity Transport. `Game.App` references `Game.Net`, and `Game.Presentation` does not. `AssemblyRulesTests` gains that row. Unity Transport is allowed only in `Game.Net`, `Game.App` and tests.

## Implementation notes (2026-10-15, NET-002 onwards)
Choices made while building, which refine the decisions above:
1. **Guests apply replicated voxel state at frame start.** *(Cells and instance health superseded by [ADR-0117](ADR-0117-coop-one-code-path.md): guests now queue them as exact writes, applied in the fixed step by the host's own `CellWriter`.)* `ReplicaApplySystem` (GameInitGroup, before `GridSpawnSystem`) applies the reliable stream on the main thread with direct structural changes, as a spawn would: grid spawns through `GridSpawnRequest`, despawns, cell deltas, instance health, chunk bodies and hash checks. The cell writes go through the new public `Game.Voxel.Replica.ReplicaVoxelApi`, so `Game.Voxel` stays the only code that writes cells. Bricks emptied by a delta are destroyed and unlinked from the root's `LinkedEntityGroup`, as the host's resolver does.
2. **Exact grid wire format instead of VOXS.** Grids travel as their exact palette, instance order and brick cells (deflated). VOXS re-indexes, and later raw cell values must mean the same on both ends. Both ends run the same build, so block indices agree, and the handshake's protocol version guards the layout.
3. **Shared delta stream.** The host keeps one shadow, not one per guest. A joining guest gets the current state of every grid at the end of tick T, then the same deltas from T + 1 as everyone else. A brick's first change is sent whole; later changes are diffs against the shadow (whole again when more than a quarter changed).
4. **Weapons run cosmetically on guests.** Snapshots carry each ship's `ShipCommand`. Guests keep `WeaponAimSystem`, `WeaponFireSystem`, `ProjectileStepSystem`, `BeamStepSystem`, `ExplosionSystem` and `ShieldSystem`, so tracers, impacts and shield flashes come from the existing presentation paths. `ReplicaDamageDrainSystem` (Game.Voxel, guests only) consumes and discards the damage, impulse and restore channels in place of `DamageResolveSystem` and `ImpactEventSystem`. Shield HP is overwritten from snapshots every tick.
5. **Guest bodies are kinematic.** Replicas spawn with `GridMotion.Kinematic`. `ReplicaBodySyncSystem` sets their pose 100 ms behind the host and gives them the replicated linear velocity, so graphical smoothing stays smooth. `GridMassUpdateSystem` is authority-only.
6. **`Game.Net` also references `Game.Galaxy`** for `FrameOrigin`. Guests write the host's origin into it, since `OriginRebaseSystem` is authority-only.
7. **The role singleton is a boot entity**, created by `GameComposition` with `Solo`. `RoleSystemPolicy` (Game.App) switches systems by name, restores each system's own solo state on leave, and keeps every Game.Net system disabled outside a session.
8. **Managed systems for the spike.** Capture, apply and snapshots are main-thread managed code that completes all jobs on host ticks with guests connected. Burst jobs are the optimisation pass, not the spike (owner: "optimize later"). Solo play runs none of it.
9. **Pausing.** `PauseService.KeepRunning` keeps counting pause tokens, for menus and input contexts, while time keeps running during a live session.
10. **Capture placement.** `HostCaptureSystem` runs once per frame, last in WorldGroup, because the GameplayEnd command buffer must stay the tick's last structural point (SystemGroupOrderTests). `HostRemovalTapSystem` records each tick's removal reasons right after `DamageResolveSystem`, since a frame can run several ticks. Cells that left as detached bodies are flagged so guests skip debris for them.
11. **Chunk replicas.** Guests destroy replica chunk entities and let `ChunkColliderCleanupSystem` retire their colliders, as on the host. Retiring them as well double-freed the blob and crashed a guest on 2026-10-15.
12. **Dev tooling.** `net guest <slot> "<command>"` runs any dev command on a guest and returns its reply (`net reply`), and players accept `-coop-host` and `-coop-join <ip>` on the command line. One terminal can drive every instance.
13. **Own-ship prediction (NET-011).** The guest's own ship spawns as a local dynamic body. `PropulsionSystem`, `PlanarConstraintSystem` and `GridMassUpdateSystem` now run on guests too; they skip bodies with no inverse mass, so kinematic replicas are untouched. Every guest fixed tick labels its command with a sequence. Packets carry the last eight commands against loss. The host plays them back one per host tick from an adaptive jitter buffer (`PilotCommandQueue`), so each command drives exactly one tick on both machines, and snapshots acknowledge the sequence applied. `ReplicaBodySyncSystem` records the predicted state per tick, compares it with the host's state for the acknowledged command, and applies part of the difference to the body and to newer records. There is no replay: a ship's motion is smooth enough that the measured error holds. Matching per frame instead of per tick made the error grow with frame-rate differences, up to 10 m.
14. **Plumes on replicas.** Only propulsion writes `ThrusterState`, and it skips kinematic replicas, so snapshots carry each ship's lit thrusters by block instance. Guests apply them to the matching functional entities.
15. **Pilot names.** `PilotName` (Game.Core) sits on players' ships. It is written by the session on the host and from the spawn header on guests, and the overlay and HUD target panel show it instead of the blueprint name. Presentation still does not reference Game.Net.
16. **Leaving.** `NetTransport.Shutdown` sends a goodbye three times, unreliable and past the network simulator, before disconnecting, so the far end notices at once instead of after the 10 s timeout.
17. **Burst replica work (NET-012).** `ReplicaJobs` (Game.Voxel) runs cell hashing, the host's diff against its shadow (shadows are now native arrays), guests' dense brick writes, the orphaned-instance scan and chunk colliders as Burst jobs that complete before returning, so callers keep their shape. Each is run once at join, since the first call of a Burst job in a player costs a few milliseconds. Host capture completes only the component dependencies it reads; completing every job also waited on mesh and collider builds.
18. **Guest knockback.** `ReplicaDamageDrainSystem` applies requested impulses instead of discarding them. Only the guest's own ship has mass there, so its cosmetic hits knock it back at once. Large corrections take the host's velocity at once and glide to its position, snapping only beyond 25 m or 1.2 rad.
19. **Guest weapons state.** Each command carries the guest's target lock (network id) and weapon mode. The host's targeting system runs only for `PlayerShip`, so before this a guest's autofire and target-lead weapons did nothing on the host.
20. **Aim anchors.** A player's aim travels as a grid's PersistentId plus a point in its frame (`AimAnchor`). The host rebuilds a guest's aim from the grid's current pose, and guests rebuild every player's aim from the pose they display, which compensates for interpolation and command latency without rewinding the world.

## Consequences
- Single-player keeps its fixed step, ordering, determinism tests and performance. Every change is gated by `SimRole`.
- Guests see the world about 100 ms behind (the interpolation buffer) plus half the round trip. Their own ship responds on the same frame (NET-011).
- Bandwidth target per guest: about 250–500 kbps in heavy combat, plus bursts on big cuts. The spike measures it; it is not a gate.
- Throwaway risk is accepted: if the owner rejects co-op, the worktree and branch are deleted and `main` never saw them.

## Alternatives rejected
- **Netcode for Entities:** two worlds on the host, ghosts unsuited to bricks, and every system re-tagged.
- **Deterministic lockstep with rollback:** needs cross-machine bit-identical simulation, and rollback cannot snapshot or re-simulate million-cell grids within a frame.
- **Damage-event replication (guests resolve damage):** cheaper bandwidth, but needs damage resolution in fixed-point for x64/ARM identity. This is a possible later optimisation (Teardown's model), not the starting point.
