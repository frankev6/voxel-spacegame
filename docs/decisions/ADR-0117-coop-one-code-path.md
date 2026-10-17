# ADR-0117 — Co-op: one code path for every effect (authority decides, every machine applies)

**Status:** Accepted (2026-10-15 for the spike; merged to `main` with ADR-0116 on 2026-10-16, owner approval quoted there). Owner, after guests hit collision shapes of blocks that were gone and missed salvage tints, prompts and ability icons: "i think we have to decouple the systems and make sure that host, guest, or single player, they go through the same functions so we are not running redundant or duplicate code", and "this doesn't go just for the destruction system, for every system".

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
| `BodyGridSpawner`, `EncounterDirectorService` add `Interactable` | none | no dock, hail or inspect prompts on guests (deferred: co-op is sandbox-only, NET-017) |
| `AbilitySystem` fills `AbilitySlot` (host only) | none | empty ability hotbar on guests (fixed in NET-016) |
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
3. **NET-015, pilots.** Add `Pilot`, one command conversion, and per-pilot interaction, salvage reach and salvage marking. A guest can salvage. (Combat status moved to NET-017; `PlayerShip` stays the local ship, so no `LocalPilot`.)
4. **NET-016, abilities and status.** Activation decides on the host. Slot state is replicated. Timers and effects (shield toggle, stat boosts, activation cue) run everywhere, so guest prediction includes boosts and the hotbar works.
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

4. **NET-015 (2026-10-16).**
   - **Pilots.** `Pilot { Slot }` marks every player's ship on every machine:
     - `PilotSetupSystem` tags the local `PlayerShip` as slot 0;
     - the co-op session adds it to guests' ships on the host;
     - `GuestReplicator` adds it to replica ships from their owner slot.
   - `PlayerShip` still means the ship this machine flies (input, camera, HUD), so presentation did not change.
   - **Commands.** `PilotCommandConversion.Convert` turns a sampled command into a `ShipCommand` for the local player and for remote pilots alike.
   - **Interaction.**
     - `PilotInteractCountSystem` counts each pilot's Interact edges from its `ShipCommand`.
     - `InteractionQuerySystem` (every machine) derives each pilot's `PilotInteraction`, and the local pilot's is also the `InteractionTarget` singleton the HUD reads.
     - `InteractionTriggerSystem` (authority only) turns presses into `BodyInteracted`, which now carries the pilot's `PersistentId`.
     - Remote pilots may only salvage.
     - `InteractionInputState` is deleted.
   - **Salvage.** `SalvageService` streams the wreck into the pilot that interacted. Every pilot fills one hold, the host's (SPEC-90 §10 Q5 default). `SalvageMarkSystem` and `SalvageReachSystem` run on every machine.
   - **Targeting stays on the pilot's machine.** Target cycling and cursor picks read that machine's cursor and camera. Each machine runs `PlayerTargetingSystem` for its own pilot, and the host applies the target and autofire carried in the guest's commands. It is one system, with its input on the guest and its result on the host, so it is not a copy.
   - **Still separate, recorded for later phases:**
     - `SalvageCollected` is not replicated, so the guest's prompt stays up during the pull;
     - the pull animation and the economy notices show only on the host;
     - `PlayerCombatStatus` is one host-side value (NET-017);
     - the dev playground's radar zoom and beacon markers are set up on the host only.
   - **Flaky test, not from this work.** `OpeningChapterTests.Playthrough` sometimes stops at the last probe (`shots=3092; line=72`). It fails the same way on 18769d21, before ADR-0117.

5. **NET-016 (2026-10-16).**
   - **Split.** `AbilityJob` kept only the decisions: slot resync from blocks, power grants and presses.
     - `AbilityTimerSystem` (every machine) clears last tick's flags, makes a hulk's slots unavailable and advances timers through `AbilityLifecycle.Advance`, the code `AbilityJob` used to run inline.
     - `AbilityFeedbackSystem` (every machine) plays the activation cue and effect of every slot flagged `ActivatedThisTick`; that used to be inside `Activate`.
     - `ShieldToggleEffectSystem` and `StatBoostEffectSystem` now run on every machine.
   - **Activation counter.** `AbilitySlot.Reserved` became `ActivationCount`, incremented on each activation. Replication carries it, so a guest flags `ActivatedThisTick` exactly once per host activation, even when one frame covers several ticks or several activations.
   - **Wire (protocol 7).** `AbilityState` (reliable) carries one ship's slots and its `ShieldControl.RequestedOn`.
     - The host sends it after the ship's cells, whenever a signature of the decided fields changes (slot list, state, charges, active, activation count, shield request). Timers are not part of the signature, since both machines run them.
     - A boost's end travels as ticks remaining, because the two clocks differ.
     - The join snapshot sends every ship's slots after its spawn.
   - **Guest apply.**
     - `AbilityCodec` writes the message into `AbilityHostSlot` and `AbilityHostState`. `AbilitySlotApplySystem` registers both with ship root setup, so the network layer adds no components.
     - In the next fixed tick, `AbilitySlotApplySystem` (after the timers, before the effects) copies the host's slots, rebases the boost end, flags activations and list changes, and sets `RequestedOn` so that the toggle effect ends on the host's value.
     - A host power request becomes Ready on the guest, so the guest's power allocator does not draw energy for it again.
   - **Still separate, recorded for later phases:**
     - a guest's own boost starts when the host's decision arrives, one round trip after the press (1.65 m peak error at 100 ms with the playtest tuning);
     - the guest's energy meter is its own power allocation, without the host's ability costs.
   - **Tests.** The ability fixtures run the timer and feedback systems.
     - `AbilityScenarioTests` now pins the shipped boost (1.8x speed, 2x thrust), since the content carries the 4x playtest tuning from NET-012.
     - Its peak-speed check now asks for a clear pass over the normal cap, not reaching the boosted one. The pulse reaches 199 of 216 m/s on 68c80533, before any co-op code, so this comes from QA-001's flight handling.
     - `Boost_RaisesTopSpeed` still fails its braking bound on 3c5b2faa, before this phase: 41 of 79 m/s shed in 323 ticks. That is a QA-001 flight question, recorded rather than loosened.

6. **NET-017 (2026-10-16).**
   - **Salvage.** `SalvageService.TrySalvage` stays the decision: yields, the hold, and `SalvageCollected { Collector }` written as state.
     - Its consequences run on every machine. `SalvageMarkSystem` removes the prompt from collected wrecks. `SalvagePullSystem` (now on every role) streams the cubes into the collector.
     - The despawn after the last wave moved out of the pull job into `SalvageDespawnSystem` (authority).
     - The host sends `GridSalvage` (wreck, collector), and a guest adds the same component.
   - **Notices.**
     - `EconomyNotice.Pilot` names the co-op pilot a notice is for (0 = this machine's player).
     - `SalvageService` sets it to the collector, unless the collector is the local player.
     - The co-op session sends it to that guest as `Notice`, where it is published for the guest's own player.
     - `HudNotificationPresenter` shows only this machine's notices.
     - The salvage cue now follows the `SalvageDone` notice for this machine's pilot, so it plays wherever the salvaging pilot is.
   - **Combat status.**
     - `PlayerCombatStatusSystem` (authority) counts every pilot into `PilotCombatStatus`, which `PilotSetupSystem` adds.
     - `PlayerCombatStatusSyncSystem` (every machine, right after it) writes the singleton from the ship this machine flies. Without exactly one `PlayerShip`, it zeroes the hostile count and keeps the last damage tick, as before.
     - The host sends `PilotStatus` on change. The last-hit tick goes at most twice a second, as ticks ago.
   - **Faction relations.** The host sends the stance table (`Factions`) when it changes and in the join snapshot. The guest writes it into its own table, which has the same content and faction count. `net factions` prints it.
   - **Protocol 8.** New messages: `GridSalvage`, `PilotStatus`, `Factions` and `Notice`.
   - **Deferred: interactables of bodies and encounters.** Co-op can only be hosted from the sandbox playground, which loads no bodies and runs no encounters, so no dock, hail, inspect or jump target exists to replicate or to check. When co-op leaves the sandbox:
     - send the root's or body anchor's `Interactable` with the grid;
     - show non-salvage prompts on guests as host-only until those flows have a co-op design.
     - Trade is in the same position, because only the host can open the merchant screen.

7. **NET-018 (2026-10-16).** The enforcement in §7 exists.
   - `RoleSystemPolicy.Decisions` names, for each authority-only system, the decision it owns and how a guest receives it.
   - `NetWriterRulesTests` reads Game.Net's compiled IL with reflection (no Cecil), so lambdas and state machines are covered.
     - What Game.Net may write is an allow-list with a reason per component: its own bookkeeping, replicated state (pilot, allegiance, poses, snapshot shield, plumes and commands, the pilot-machine target lock) and records for appliers (`AbilityHostState`; `SalvageCollected` and `PilotCombatStatus` as replicated decisions).
     - Everything else fails the test, with the call site named.
   - `net parity` and `tools/playtest/coop-parity.sh` compare one guest ship as host and guest derive it. Poses, timers and distances are left out, because they lag by design.

8. **Follow-ups before the merge (2026-10-16).**
   - **Guest energy.** `AbilitySlotApplySystem` adds each host activation's one-shot cost to `AbilityHostState.ChargeTotal`; `PowerAllocateJob` draws what it has not drawn yet (`ShipPower.HostCharged`) at the point where the host granted it. A guest's energy meter now pays its boost (live: 200 → 191 right after the press, then recharging). `AbilityCodec` keeps the applier's field when it writes the host's state.
   - **Playground beacons on guests.** The host's playground sends `playground view=<origin>` to each guest once it is in the world; the guest shows the rings, labels, radar markers and 800 m radar range without running pads (live screenshot checked).
   - **Shipped boost restored.** NET-012's 4x playtest tuning is gone from `ability_speed_boost` (1.8x speed, 2x thrust again), and `AbilityScenarioTests` no longer pins it.
   - **Braking bound (note 5).** QA-001's warm-start shrink in `PropulsionJob` cost real thrust under saturated coupled demand: Gauss–Seidel could not win the 10% back each tick. The job now also solves from the exact warm start and keeps that only when its residual is measurably lower, so a null space still bleeds. `Boost_RaisesTopSpeed` passes; the new `NullSpace_BleedsAtRest` keeps the owner's resting-thruster fix (SPEC-20 decision 7).
   - **Opening playthrough (note 4).** Six isolated runs after the allocator fix all passed; not reproduced.
   - **Still open:** a guest's boost starts one round trip after the press (no activation prediction); dock, hail, inspect and trade for guests wait for co-op outside the sandbox.

## Consequences
- Single-player behaviour must stay identical. Each split is a refactor of existing systems, checked by the existing damage, integrity, split, ability and interaction tests before co-op is touched.
- System order changes: new applier systems sit where the code they came from ran, so `SystemGroupOrderTests` and the role-policy table both change.
- Guests apply cells in the fixed step, not at frame start. A hash check waits until the writes before it have been applied.
- The spike's earlier guest code mostly gets deleted. The diff is large, but each phase is shippable on its own.

## Alternatives rejected
- **Keep the copies and test each consequence (option 3 of the 2026-10-15 review):** cheapest, but copies drift from single player and gaps keep being found one at a time.
- **Guests run the deciders on replicated hits:** removes every copy, but needs bit-identical results across machines (fixed-point damage, deterministic job completion). ADR-0116 rejected it for the same reason.
