# SPEC-90 — Co-op prototype (spike)

| | |
|---|---|
| **Status** | Spike complete: NET-002…012 working end to end in the co-op worktree |
| **Area code** | NET (task IDs are `NET-001`, `NET-002`, …) |
| **Module(s)** | `Game.Net` (folder `Assets/Game/Net/`), with session wiring in `Game.App` |
| **Depends on specs** | SPEC-10, SPEC-12, SPEC-13 (grids, bodies, damage and splits), SPEC-20, SPEC-21 (commands, weapons), SPEC-05 (VOXS), SPEC-70 (dev commands) |
| **Depended on by** | — |
| **Milestone** | M4 (spike, outside the roadmap) |

> Read [01-architecture.md](../01-architecture.md), [02-conventions.md](../02-conventions.md) and [03-glossary.md](../03-glossary.md) before this spec. Where this spec departs from them, ADR-0116 records the departure.

**Spike.** Lives only on `task/NET-001-coop-prototype` in the `/Users/frank/DEV/spaceship-game-v2-coop` worktree, branched from QA-001 at `a542ba85`. It never merges to `main` without the owner's approval. Design: [ADR-0116](../decisions/ADR-0116-coop-prototype-host-authority.md). Area code: NET.

## 1. Purpose
Prove that 2–4 friends can play together in the playground with combat, several AI and player ships, and large chunk breaks, all looking the same on every machine without obvious errors. The architecture must be good enough to keep if the owner says yes. Tests beyond correctness checks, tuning and optimisation come later (owner, 2026-10-15).

## 2. Scope
**In:**
- host and join by IP on a LAN or localhost;
- 2–4 players;
- the playground (pads, range targets, AI cruisers and frigates, xeno hulls);
- flight, weapons, shields, power, damage, splits into particles, chunks and hulks, ship death and hulks;
- join while a fight is in progress;
- a network stats overlay;
- a host-against-guest cell state hash check.

**Out (until the owner decides):**
- the campaign, travel and star system streaming;
- the Blueprint Lab and editing a ship while connected;
- narrative, dialog, quests, economy and salvage;
- saving a co-op session;
- Steam, relay and NAT traversal;
- client-side prediction beyond NET-011;
- cheating protection.

## 3. Decisions (binding for the spike)
1. Everything in ADR-0116 §Decision.
2. Menus never pause the world while a session is live. `PauseService` refuses pause tokens while `SimRole != Solo`, and UI screens open over the running world.
3. The guest's camera, HUD and input follow the ship tagged `PlayerShip` in the guest's own world. On the host, guest ships carry `RemotePilot` and never `PlayerShip`, so the host's single-player queries keep their meaning.
4. AI treats guest ships as players through allegiance (the same `Team` as the host). No AI code changes.
5. Wire format: little-endian, hand-written `DataStreamWriter` codecs, with a one-byte message kind and a protocol version in the handshake. A mismatched build is refused at connect.
6. Every message is built and applied in Burst jobs where it touches ECS data. Managed code only drives the transport driver and the session lifecycle.

## 4. Data model (new types, all in `Game.Net` unless noted)
| Type | Shape | Notes |
|---|---|---|
| `SimRole` (Core) | singleton `{ byte Value }`: 0 Solo, 1 Host, 2 Guest | Created by `Game.App`; absent counts as Solo |
| `NetSession` | singleton `{ byte LocalSlot; byte Connected; uint HostTick; float RttMs }` | Slot 0 is the host |
| `RemotePilot` (Simulation) | `IComponentData { byte Slot }` | On the host, on guest-piloted ship roots |
| `RemoteCommandBuffer` | singleton, native, per slot: latest `PlayerCommand` with edge counters | Written by the receive system, read by `RemotePilotApplySystem` |
| `Replicated` | `IComponentData { ulong NetId; uint LastSnapshotTick }` | On guests, on every replicated root and chunk body |
| `ReplicaInterpolation` | buffer of `{ uint Tick; float3 Pos; quaternion Rot; float3 Lin, Ang }`, capacity 4 | Guests only |
| `BrickShadow` | per replicated brick on the host: last-sent 4096 `Cell` values | Host only; native hash map keyed by `(NetId, brick coord)` |
| `NetStats` | singleton: bytes in and out per second per channel, message counts, snapshot age, desync count | Read by the overlay |

## 5. Systems, services and flow
| System | Group | Role | Does |
|---|---|---|---|
| `NetTransportService` | managed, `Game.App` frame update | Host, Guest | Owns the `NetworkDriver` and pipelines, accept and connect, handshake |
| `NetReceiveSystem` | `GameInitGroup`, after `PlayerInputSampleSystem` | Host, Guest | Drains the driver into per-kind native queues |
| `RemotePilotApplySystem` | `ShipCommandGroup`, after `PlayerCommandApplySystem` | Host | Remote commands → `ShipCommand` of `RemotePilot` ships |
| `GuestCommandSendSystem` | `GameInitGroup`, after `NetReceiveSystem` | Guest | Sends the local `PlayerCommand` once per frame |
| `ReplicaSpawnSystem` | `GameInitGroup`, before `GridSpawnSystem` | Guest | Spawn and despawn messages → `GridSpawnRequest`, chunk bodies, the NetId map |
| `ReplicaCellApplySystem` | `GameplayPostPhysicsGroup`, in place of `DamageResolveSystem` | Guest | Applies brick diffs, sets `BrickDirty`, bumps `Revision`, emits synthetic removals for debris and audio, queues mass recompute |
| `ReplicaBodySyncSystem` | `GameplayPrePhysicsGroup`, first | Guest | Interpolates snapshots 100 ms behind into kinematic bodies |
| `ReplicaFxSystem` | `GameplayPostPhysicsGroup` | Guest | Cosmetic shots, beams, explosions and shield hits → `FxEvent` and `SfxEvent` |
| `HostCaptureSystem` | `GameplayPostPhysicsGroup`, after `GameplayEndCommandBufferSystem`, before `SimClockAdvanceSystem` | Host | Brick diffs, spawn and despawn, split records, combat events → reliable queue |
| `HostSnapshotSystem` | `WorldGroup` | Host | Transforms at 30 Hz per connection, by priority |
| `NetSendSystem` | `PresentationBridgeGroup`, last | Host, Guest | Flushes queues to the driver |
| `CellHashCheckSystem` | `WorldGroup`, every 2 s | Host, Guest | Per-grid `CellStateHasher` hashes; mismatches are logged and counted |

Flows:
- **Host a session.** Run `net host` (dev command and menu). `SimRole` becomes `Host` and the driver listens on 7777. The playground runs normally.
- **Join.**
  1. Run `net join <ip>` (dev command and menu). The guest boots the World scene in replica mode: no galaxy, no playground spawns, `SimRole = Guest`.
  2. After the handshake, the host spawns the guest's ship at a free pad with `RemotePilot { Slot }` and sends the join snapshot.
  3. The guest spawns every grid and tags its own ship `PlayerShip`.
  4. The delta stream starts.
- **One hit (host).** The usual pipeline runs. Then `HostCaptureSystem` diffs the touched bricks and sends `BrickDelta`, plus `GridSpawn` for any new hulk and `ChunkSpawn` for any new chunk body.
- **One hit (guest).** `ReplicaCellApplySystem` applies the delta, debris sprays from the synthetic removals, and bricks remesh and recollide through `VoxelRebuildGroup` as usual.
- **Leave.** On disconnect, the host turns the guest's ship into an AI-less drifting ship (despawned after 30 s). A guest that loses the host returns to the main menu with a message.

## 6. Public API
- `ICoopSession` (in Core, implemented in `Game.App`): `Host(ushort port)`, `Join(string address, ushort port)`, `Leave()`, `State`, `Peers`.
- Dev commands: `net host [port]`, `net join <ip> [port]`, `net leave`, `net status`, `net sim <latencyMs> <jitterMs> <lossPct>`, `net hash`.

## 7. Edge cases and failure behaviour
- A brick diff for an unknown NetId is buffered for one second (the spawn may be in the same packet group), then dropped with a desync count.
- An unreliable snapshot older than the newest applied one is ignored.
- A large reliable payload over 256 KB is split into chunks. The join snapshot is the only expected case.
- A protocol version mismatch is refused with "Version mismatch: host X, you Y".
- The host's pause request is refused while connected (§3.2). The editor and dialog are out of scope.

## 8. Performance budget
Advisory, as ADR-0042 says: measure, record, never block. Spike targets:
- under 1 ms of host main thread for capture and send with three guests in heavy combat;
- under 500 kbps per guest median;
- no frame over 8 ms caused by applying a 2,000-cell cut on a guest.

## 9. Tasks
### NET-001 — Spike setup, ADR and plan
- **Milestone:** M4 (spike)
- **Tier:** frontier — sets the co-op architecture
- **Depends on:** —
- **Do:** Create the worktree and branch, copy the Unity Library, confirm a clean compile, and write ADR-0116 and this spec.
- **Files:** `docs/decisions/ADR-0116-coop-prototype-host-authority.md`, `docs/specs/SPEC-90-coop-prototype.md`
- **Acceptance criteria:**
  - [x] `tools/compile.sh` passes in the worktree.
  - [x] The ADR and spec exist; `tools/check-docs.py` is clean.
- **Tests:** none (docs only).

### NET-002 — Game.Net module, transport and sessions
- **Milestone:** M4 (spike)
- **Tier:** frontier — new assembly and wire protocol
- **Depends on:** `NET-001`
- **Do:**
  - Add `com.unity.transport` to the manifest.
  - Add the `Game.Net` asmdef and the `AssemblyRulesTests` row.
  - Write `NetTransportService` with the three pipelines plus fragmentation, the handshake with the protocol version, and connect, disconnect and timeout.
  - Add `SimRole` and `NetSession`, `ICoopSession`, the `net host`, `join`, `leave`, `status` and `sim` dev commands, and a standalone dev-build script for a second instance (`tools/build-dev.sh`).
- **Files:** `Assets/Game/Net/**`, `Assets/Game/App/Session/CoopSession*.cs`, `Assets/Game/App/DevRuntime/Commands/NetDevCommand.cs`, `Packages/manifest.json`, `tools/build-dev.sh`
- **Acceptance criteria:**
  - [ ] An Editor host and a dev-build guest on the same Mac connect, exchange pings and show RTT in `net status`.
  - [ ] A version mismatch is refused.
  - [ ] Solo play is unchanged: no network system runs without a session.
- **Tests:** `NetCodecTests` (EditMode): round-trip of every message kind.

### NET-003 — Role policy and the replica boot path
- **Milestone:** M4 (spike)
- **Tier:** frontier — classifies every simulation system by authority
- **Depends on:** `NET-002`
- **Do:**
  - Write `RoleSystemPolicy`, which classifies every system in the fixed step and in `WorldGroup` and disables `AuthorityOnly` systems on guests.
  - Add a guest boot into the World scene in replica mode: no galaxy, no playground spawns, no origin rebase.
  - Make `PauseService` refuse pause tokens during a session.
- **Files:** `Assets/Game/App/Session/RoleSystemPolicy.cs`, `Assets/Game/App/Flow/*` (replica entry), `Assets/Game/App/Services/PauseService.cs`
- **Acceptance criteria:**
  - [ ] A guest enters an empty World scene that runs without errors.
  - [ ] Opening a menu on either side does not pause the host.
- **Tests:** `RoleSystemPolicyTests.EverySystem_IsClassified` (EditMode).

### NET-004 — Grid replication and join snapshot
- **Milestone:** M4 (spike)
- **Tier:** frontier
- **Depends on:** `NET-003`
- **Do:**
  - Send `GridSpawn` (blueprint hash or VOXS, pose, velocity, kind, profile, allegiance) and `GridDespawn` from the host. Spawn on the guest through `GridSpawnRequest`, with a NetId-to-Entity map.
  - Send a join snapshot with chunked flow control.
- **Files:** `Assets/Game/Net/Replication/Grids/**`
- **Acceptance criteria:**
  - [ ] A guest joining a populated playground sees every ship, station, target and hulk in place.
- **Tests:** `GridSpawnMessageTests` (EditMode).

### NET-005 — Transform snapshots and interpolation
- **Milestone:** M4 (spike)
- **Tier:** frontier
- **Depends on:** `NET-004`
- **Do:**
  - Send 30 Hz unreliable snapshots per connection, prioritised by distance and recency, carrying the frame origin.
  - Make remote bodies kinematic on guests, with a 100 ms interpolation buffer and extrapolation of up to 200 ms on gaps.
- **Files:** `Assets/Game/Net/Replication/Bodies/**`
- **Acceptance criteria:**
  - [ ] AI ships fly smoothly on the guest at 100 ms latency, 10 ms jitter and 2% loss.
- **Tests:** `InterpolationBufferTests` (EditMode).

### NET-006 — Remote pilots
- **Milestone:** M4 (spike)
- **Tier:** frontier
- **Depends on:** `NET-005`
- **Do:**
  - Send guest commands to the host and add `RemotePilot` and `RemotePilotApplySystem`.
  - Spawn the guest's ship on join.
  - Make the guest's camera, HUD and aim follow its own `PlayerShip`.
- **Files:** `Assets/Game/Simulation/Commands/RemotePilot*.cs`, `Assets/Game/Net/Pilots/**`, `Assets/Game/App/Session/CoopShipSpawner.cs`
- **Acceptance criteria:**
  - [ ] Two players fly their own ships in the playground at the same time.
  - [ ] AI engages both.
- **Tests:** `RemotePilotApplyTests` (EditMode): clamping and edge consumption match the local player's.

### NET-007 — Cell replication
- **Milestone:** M4 (spike)
- **Tier:** frontier — the destruction stream
- **Depends on:** `NET-006`
- **Do:**
  - Diff touched bricks on the host against `BrickShadow`, and send `BrickDelta` (changed cells, or the whole brick).
  - Write `ReplicaCellApplySystem` with dirty marks, revision, mass recompute and synthetic removals feeding debris, VFX and audio.
  - Write `CellHashCheckSystem`.
- **Files:** `Assets/Game/Net/Replication/Cells/**`, plus a public debris-emission entry point in `Game.Voxel` if needed (recorded in ADR-0116)
- **Acceptance criteria:**
  - [ ] After a two-minute fight, host and guest cell hashes match for every grid.
  - [ ] Debris sprays on both sides.
- **Tests:** `BrickDiffTests` (EditMode): diff and apply round-trip, whole-brick fallback.

### NET-008 — Splits, chunks and hulks
- **Milestone:** M4 (spike)
- **Tier:** frontier
- **Depends on:** `NET-007`
- **Do:**
  - Send new hulks as `GridSpawn` after they exist on the host, and send `ChunkSpawn` and `ChunkDespawn` with cells, pose and velocities.
  - Have the guest build chunk colliders through the existing builder.
- **Files:** `Assets/Game/Net/Replication/Splits/**`
- **Acceptance criteria:**
  - [ ] A cruiser cut in half shows the same two pieces, tumbling the same way, on host and guest.
  - [ ] Hulks stay consistent through further damage.
- **Tests:** none beyond the NET-007 hash check (runs in the NET-010 scenario).

### NET-009 — Combat presentation on guests
- **Milestone:** M4 (spike)
- **Tier:** standard
- **Depends on:** `NET-006`
- **Do:**
  - Send shot, beam, explosion and shield-hit events as cosmetic events.
  - Replicate `ShieldState` HP and status, and ship death and `Hulk` state, to guest HUDs and overlays.
- **Files:** `Assets/Game/Net/Replication/Combat/**`
- **Acceptance criteria:**
  - [ ] The guest sees tracers, impacts, shield flashes and explosions in step with the host.
  - [ ] The guest's HUD shows its own hull and shield.
- **Tests:** none (live check).

### NET-010 — Co-op playtest scenario and stats overlay
- **Milestone:** M4 (spike)
- **Tier:** standard
- **Depends on:** `NET-008`, `NET-009`
- **Do:**
  - Add a `net` overlay: kbps per channel, RTT, snapshot age, desyncs.
  - Write a scripted scenario: host plus guest, two AI cruisers, one big cut, a hash check.
  - Record bandwidth at 0, 50, 100 and 150 ms of latency in the ledger.
- **Files:** `Assets/Game/Presentation/Net/NetOverlay*.cs`, `tools/playtest/coop.sh`
- **Acceptance criteria:**
  - [x] The scenario runs end to end with zero desyncs and zero exceptions on both sides (`tools/playtest/coop-soak.sh`, §10a table). The overlay lives in Game.App (`CoopSession`), not Presentation.
- **Tests:** `tools/playtest/coop.sh`, `tools/playtest/coop-soak.sh`, `tools/playtest/coop-chunks.sh`.

### NET-011 — Own-ship prediction (stretch)
- **Milestone:** M4 (spike)
- **Tier:** frontier
- **Depends on:** `NET-010`
- **Do:**
  - Have the guest simulate its own ship locally from its commands.
  - Keep a 32-tick history, compare it with host snapshots, and correct smoothly. Snap past 2 m or 20°.
  - As built: one command per fixed tick, played back one per host tick from an adaptive buffer, and acknowledged in snapshots; a 256-tick history; 30% of the position error and 50% of the velocity error applied per acknowledged snapshot; snaps past 6 m or 0.5 rad, since normal corrections never come near them.
- **Files:** `Assets/Game/Net/Replication/OwnShipPredictor.cs`, `Assets/Game/Net/Replication/PilotCommandQueue.cs`
- **Acceptance criteria:**
  - [x] At 100 ms latency, the guest's ship responds on the same frame as its input, with no visible rubber-banding in normal flight (0.00 m error, 0 snaps at 167 ms RTT with jitter and loss).
- **Tests:** `PredictionTests` (EditMode).

### NET-012 — Co-op hardening: cost, big hits, target lock, aim compensation
- **Milestone:** M4 (spike)
- **Tier:** frontier
- **Depends on:** `NET-011`
- **Do:**
  - Time the co-op layer per frame (`net status` costs, `tools/playtest/coop-cost.sh`) and move the hot loops to Burst: cell hashing, the host's brick diff against its shadow, guests' whole-brick writes, the orphaned-instance check and chunk colliders. Complete only the jobs capture reads instead of every job.
  - Predict knockback on the guest's own ship from its cosmetic hits' impulse requests, and glide instead of snapping on large corrections.
  - Send the guest's target lock and weapon mode to the host, which never ran targeting for guest ships.
  - Lag-compensate player aim: send it as a point on the grid under it, rebuilt on every machine from the pose that machine shows.
- **Files:** `Assets/Game/Voxel/Replica/ReplicaJobs.cs`, `Assets/Game/Net/Replication/AimAnchor.cs`, `Assets/Game/Voxel/Replica/ReplicaDamageDrainSystem.cs`, `tools/playtest/coop-cost.sh`, `tools/playtest/coop-aim.sh`
- **Acceptance criteria:**
  - [x] A big cut costs a guest under 8 ms outside joins (was 24 ms), with 0 hash mismatches.
  - [x] Large hits on a guest's ship do not snap it (53 snaps before, 0 after, in the same cut scenario).
  - [x] A guest's autofire and target-lead weapons work on the host.
- **Tests:** `PredictionTests` (EditMode), `coop-soak.sh`, `coop-cost.sh`, `coop-aim.sh`.

### NET-013 — One code path: shared cell apply
- **Milestone:** M4 (spike)
- **Tier:** frontier
- **Depends on:** `NET-012`
- **Do:** ([ADR-0117](../decisions/ADR-0117-coop-one-code-path.md) phase 1)
  - Extract `CellApply` from `CellWriter`: the one writer of a cell together with all its consequences:
    - brick dirty flags and neighbour marks;
    - the brick's deferred destroy;
    - the `CellRemoved` record;
    - instance cell counts.
    The resolver, split and dissolve paths call it, and single-player behaviour is unchanged.
  - Guests queue exact cell and instance writes (with each brick's removal reason) and apply them in the fixed step through `CellApply`, at the resolver's place. Debris, sound, mass, collider and mesh rebuilds and brick deletion then run in the host's own systems.
  - Move debris emission out of `SplitSystem` and the final destroy pass out of `ShipDeathSystem` into appliers that run on every machine.
  - Delete the guest copies:
    - `ReplicaVoxelApi.WriteBrick`, `TouchNeighbours`, `QueueColliderRemoval`, `SetInstance`, `EmitDebris` and `RequestMassRecompute`;
    - the dense write job.
- **Files:** `Assets/Game/Voxel/Damage/Jobs/CellApply.cs`, `Assets/Game/Voxel/Damage/Systems/ExactCellApplySystem.cs`, `DebrisEmitSystem.cs`, `StructuralDestroySystem.cs`, `Assets/Game/Net/Replication/GuestReplicator.cs`
- **Acceptance criteria:**
  - [x] Damage, integrity, split and ship-death tests pass. EditMode Voxel.Damage: 84. PlayMode: `ShipDeathRegressionTests`, `DamageDeterminismTests`, `DamagePersistenceScenarioTests`, `CombatScenarioTests`. Hand-built test pipelines list the new appliers.
  - [x] Guests get debris, break sounds, collider removal and mass changes from the shared systems.
    - `coop-colliders.sh 4098 -24 21 24 117 24 101 -40 53`: host 30 bricks / 30 collider parts / 15,443 cells, guest identical.
    - `coop-cost.sh`: 0 hash mismatches, 0 desyncs, 0 missing writes, 620 debris particles from `DebrisEmitSystem` on a guest.
    - `coop-soak.sh 6`: 0 bad hashes and 0 snaps at 0 to 150 ms.
  - [x] `Game.Net` no longer writes cells or instance health.
- **Tests:** existing Voxel damage suites, `coop-colliders.sh`, `coop-soak.sh`.

### NET-014 — One code path: structural transitions and spawn setup
- **Milestone:** M4 (spike)
- **Tier:** frontier
- **Depends on:** `NET-013`
- **Do:** (ADR-0117 phase 2)
  - Split core loss, hulk splits (with the instance remap that moves functional blocks), chunk creation and lifetime, and impulses into a decider and an applier.
  - Guests receive the deciders' records and run the same appliers. This deletes `EmitHulkBlast`, `CreateChunk`, `DestroyChunk`, `ReleaseOrphanedInstances` and the impulse copy in `ReplicaDamageDrainSystem`.
  - One shared root setup for every spawn path, by kind and profile.
  - `SalvageMarkSystem` runs everywhere (a derivation).
- **Files:** `Assets/Game/Voxel/Damage/Systems/ShipDeathSystem.cs`, `SplitSystem.cs`, `ImpactEventSystem.cs`, `Assets/Game/Voxel/Grid/GridRootSetup.cs`
- **Acceptance criteria:**
  - [x] A core loss runs through `HulkTransitionSystem` on host and guests alike (hulk components, kind, breach blast and sound). In a live check (`gh_dmg_kill_core`), the guest's hulk matched the host's: 966 cells, 0 hash mismatches.
  - [x] Chunk bodies are set up by `ChunkBodySetup` on host and guests. `coop-chunks.sh`: 8 chunks spawned and expired, 0 mismatches, no crashes.
  - [x] Impulses come from one applier everywhere.
  - [x] Salvage tint on guests: done in NET-015, once the guest's F press went to the host. `SalvageMarkSystem` runs on every machine.
  - [ ] Split-off hulks moving their functional blocks, and one root-setup function. Recorded in ADR-0117 note 2.
- **Tests:** existing Voxel suites, `coop-soak.sh`, `coop-parity.sh`.

### NET-015 — One code path: pilots
- **Milestone:** M4 (spike)
- **Tier:** frontier
- **Depends on:** `NET-014`
- **Do:** (ADR-0117 phase 3)
  - Every player's ship has `Pilot { Slot }` on every machine. `PlayerShip` keeps meaning the ship this machine flies, so presentation is unchanged (no separate `LocalPilot`).
  - One command conversion for every pilot (`PilotCommandConversion`).
  - Interaction queries, salvage reach and salvage marking work per pilot on every machine. Acting on an Interact press is the host's decision.
- **Files:** `Assets/Game/Simulation/Commands/Pilot.cs`, `PilotCommandConversion.cs`, `Assets/Game/Galaxy/Interaction/InteractionQuerySystem.cs`, `InteractionTriggerSystem.cs`
- **Acceptance criteria:**
  - [x] A guest sees the Salvage prompt and can salvage. Live with a headless guest: the guest's HUD showed "F Salvage" on the playground wreck, its F press salvaged the wreck on the host into the shared hold (thrusters, turrets, 20 scrap), and the wreck despawned on both. 0 hash mismatches, no errors.
  - [x] One command conversion: the local player's commands and remote pilots' commands go through `PilotCommandConversion.Convert`.
  - [x] Target lock and autofire run through `PlayerTargetingSystem` for every pilot, on that pilot's own machine (it reads that machine's cursor). The host takes the result from the pilot's commands. Recorded in ADR-0117 note 4.
  - [ ] Combat status per pilot: moved to NET-017. `PlayerCombatStatus` is still one host-side value.
  - [ ] Remote pilots can only salvage. Docking, hailing, jumping and inspecting change scenes or open dialogue on one machine, so they wait for a co-op design.
- **Tests:** command and interaction EditMode suites, `coop-parity.sh`.

### NET-016 — One code path: abilities and status
- **Milestone:** M4 (spike)
- **Tier:** frontier
- **Depends on:** `NET-015`
- **Do:** (ADR-0117 phase 4)
  - Ability activation stays the host's decision (`AbilitySystem`: slot resync, power grants, presses).
  - Timers (cooldowns, charges, boost expiry) are one shared step on every machine (`AbilityTimerSystem`, `AbilityLifecycle`).
  - Slot state is replicated per ship whenever a decision changes it, with an activation counter, so each activation reaches a guest exactly once.
  - Shield-toggle, stat-boost and activation feedback run as appliers everywhere, so the guest's hotbar fills and its prediction includes boosts.
- **Files:** `Assets/Game/Simulation/Abilities/AbilitySystem.cs`, `AbilityTimerSystem.cs`, `AbilitySlotApplySystem.cs`, `AbilityFeedbackSystem.cs`, `Assets/Game/Net/Replication/AbilityCodec.cs`
- **Acceptance criteria:**
  - [x] The guest's hotbar matches the host's view of its ship. Live with a headless guest: the same ability entries as the host (boost with 2 charges, shield); key 2 turned the guest's shield off and on through the host.
  - [x] A boost no longer arrives as a correction. The guest's own ship reached the boosted speed (245 m/s against a 62 m/s cap) with a peak prediction error of 0.20 m and no snaps; at 100 ms simulated latency, 1.65 m, smoothed, no snaps. Both use the 4x playtest tuning. Without a boost the error is 0.00 m.
- **Tests:** ability suites, `coop-parity.sh`, `coop-soak.sh`.

### NET-017 — One code path: galaxy state for guests
- **Milestone:** M4 (spike)
- **Tier:** frontier
- **Depends on:** `NET-016`
- **Do:** (ADR-0117 phase 5)
  - Guests receive faction relations, economy notices, combat status and the interactables of stations and encounters.
  - Combat status per pilot (moved from NET-015), and `SalvageCollected` replicated so a guest's prompt clears when the pull starts.
  - Salvage goes to the cargo model chosen in §10 Q5.
- **Files:** `Assets/Game/Net/Replication/HostReplicator.cs`, `Assets/Game/Net/Replication/GuestReplicator.cs`, `Assets/Game/Galaxy/Salvage/SalvageService.cs`, `SalvageDespawnSystem.cs`, `Assets/Game/Simulation/Ai/PlayerCombatStatusSyncSystem.cs`, `Assets/Game/App/Session/CoopSession.Notices.cs`
- **Acceptance criteria:**
  - [ ] Guests see dock, hail and inspect prompts. Deferred: co-op hosts only from the sandbox playground, which has no loaded bodies or encounters, so there are none to show. Its pads are dev actions that run on the host. Recorded in ADR-0117 note 6.
  - [x] Guests see salvage notices. Live: after the guest's F press its prompt cleared and its own HUD showed "Salvaged 32 items."; the host's HUD did not. Trade notices come from the merchant screen, which only the host can open (same deferral).
  - [x] Radar colours follow standing changes. The stance table that targeting and radar read replicates. Live: `net factions` matched on host and guest before, after setting Vestari hostile, and after setting it back.
  - [x] Combat status per pilot. The host computes it for every pilot, and each guest receives its own.
  - [x] Salvage goes to the shared host hold (§10 Q5 default), and the pull animation runs on every machine.
- **Tests:** `coop-parity.sh`.

### NET-018 — One code path: enforcement
- **Milestone:** M4 (spike)
- **Tier:** frontier
- **Depends on:** `NET-013`
- **Do:** (ADR-0117 §7)
  - Annotate each authority-only entry in `RoleSystemPolicy` with the decision it owns.
  - Add a test that `Game.Net` calls no structural voxel or gameplay writer.
  - Add `tools/playtest/coop-parity.sh`, which diffs the host's and a guest's view of the same ship: HUD snapshot, interaction target, collider parts, hulk and interactable counts, ability slots.
- **Files:** `Assets/Game/App/Session/RoleSystemPolicy.cs`, `Assets/Game/Tests/EditMode/Net/NetWriterRulesTests.cs`, `Assets/Game/App/DevRuntime/DevParity.cs`, `tools/playtest/coop-parity.sh`
- **Acceptance criteria:**
  - [x] `RoleSystemPolicy.Decisions` names the decision of every authority-only system and how a guest receives it. `EveryDecider_NamesItsDecision` fails on a missing or stale entry.
  - [x] `NetWriterRulesTests` decodes the IL of every Game.Net method. It fails on calls to CellWriter, audio, effect or sound queues, `EventBus.Publish`, entity destruction, replica helpers outside an allow-list, and typed writes of components that are not replicated state or records. A self-check proves that each rule fires.
  - [x] `coop-parity.sh` compares the host's and a guest's `net parity` line: cell hash, working blocks, collider parts, ability slots, shield, combat status, interaction target, grid, hulk, salvageable and collected counts, and the faction table.
    - Live: no differences at rest, with a wreck in range after a boost, and after damage to the guest's ship.
    - The HUD snapshot is not compared directly, because the host draws no HUD for a guest's ship. The line holds the state the HUD is built from.
- **Tests:** `NetWriterRulesTests` (EditMode), `coop-parity.sh`.

## 10. Open questions
| # | Question | Default | Decider | Answer |
|---|---|---|---|---|
| 1 | Guest's own ship: host-authoritative first, guest-owned movement, or prediction from day one? | Host-authoritative first, prediction in NET-011 | Owner | |
| 2 | Second instance: Editor host plus a standalone dev build, or Multiplayer Play Mode virtual players? | Editor plus a dev build (also works Mac to Windows) | Owner | |
| 3 | Pause in co-op: never pause, or pause only when every player has a menu open? | Never pause | Owner | |
| 4 | A guest's ship blueprint: the host's playground flagship, or the guest's own blueprint sent at join? | The host's playground flagship for the spike | Owner | |
| 5 | Cargo in co-op: one shared party hold, or a hold per player? | One shared hold (the host's inventory) for the spike; whoever salvages fills it | Owner | |

## 10a. Spike status (2026-10-15)
Verified live with the Editor as host and one or two dev-build guests on one Mac (`tools/playtest/coop.sh`, `tools/playtest/coop-soak.sh`):
- **Join and leave.** A guest joins by IP into a replica World scene, gets the join snapshot and its own ship (a copy of the host's flagship, so it has the same fitted shield), and can join while a fight is in progress. Killing a guest frees its slot, its ship drifts and is removed after 30 s, and a relaunched guest rejoins. When the host leaves, its goodbye (sent three times, past the network simulator) returns guests to the main menu within a second or two, also at 150 ms and 2% loss.
- **Flight and combat.** Guests fly their own ships with prediction (NET-011): the ship answers input on the same frame, and the host's state corrects it. AI engages host and guests alike. Guests see AI and player tracers, acid hits, shield ripples, impact wounds, debris, thruster plumes and engine sound on every ship, and players' names on their ships.
- **Destruction.** Damage, hulks (core kill), a pod cut off as a hulk, and bridge segments freed as chunk bodies appear on every machine. Cell hashes matched for every grid in every check. Guests' hull bars follow damage (replica deltas request a mass recompute).
- **Single-player.** No Game.Net system runs without a session. EditMode Net, HUD, system-order and project tests pass.

NET-010 scenario (`coop-soak.sh 12`: two guests weaving and firing at an AI ship, 12 s per point, the latency split across both ends, ±10 ms jitter and 2% loss above 0):

| Added latency | Host out, 2 guests | Guest in | RTT | Prediction error | Snaps | Host command buffer | Hash |
|---|---|---|---|---|---|---|---|
| 0 ms | 288 kbps | 56 kbps* | 16 ms | 0.00 m | 0 | depth 1 | 0 mismatched |
| 50 ms | 374 kbps | 120 kbps | 116 ms | 0.00 m | 0 | 1 starve, depth 1 | 0 mismatched |
| 100 ms | 436 kbps | 152 kbps | 167 ms | 0.00 m | 0 | 6 starves, depth 3 | 0 mismatched |
| 150 ms | 438 kbps | 204 kbps | 208 ms | 0.00 m | 0 | 4 starves, depth 2 | 0 mismatched |

\*The receive rate is a one-second sample and swings with how many ships are in the guest's packets; the host's send rate, about 150–220 kbps per guest, is the steadier figure. No crashes, exceptions or hash warnings on either guest.

Bugs found and fixed while testing: a double free of replica chunk colliders (crashed a guest after chunks expired), debris sprayed for cells that left as detached bodies (pods showed a cloud of cubes), AI weapons idle on guests until the target and weapon mode were replicated, a duplicate thruster left hanging where a pod split off, per-frame command matching that made prediction error grow with frame-rate differences (now one command per tick), and a command buffer that trimmed itself into constant starvation under jitter (now adaptive).

NET-012 hardening (2026-10-15; `coop-cost.sh`: range station loaded, AI combat, two guests firing, five 500k cuts across the range bridges, worst single frame outside joins):

| Work | Before | After |
|---|---|---|
| Guest apply, worst frame | 23.6 ms | 5–7 ms (first use of each job now paid at join) |
| Guest cell hash check | 10–14 ms | under 4 ms |
| Guest chunk spawn | 20 ms (managed collider build) | under 1 ms after warm-up |
| Guest whole-brick write | 3–4.5 ms per brick | about 0.2 ms |
| Host capture / hash, worst frame | 2.0 / 1.8 ms | 1.9 / 0.4 ms |
| Steady state per frame | host 0.2 ms, guest 0.1 ms | unchanged |
| Snaps on a guest ship in the cut scenario | 53 | 0 (2 from `net bring` teleports) |

Lag-compensated aim, measured with guest 2 circling while guest 1 tracked it by cursor at 120 ms round trip: the host moved guest 1's aim by up to 4.9 m to where guest 2 actually was, about half a fighter's width (`coop-aim.sh` also reports damage dealt with anchors off and on).

Known gaps:
1. Prediction does not replay: forces only the host applies (ram contacts, damage-driven impulses, ability stat boosts) still arrive as corrections, now as a glide over a few snapshots rather than a jump.
2. A guest whose window macOS throttles (covered by another window) simulates fewer than 60 ticks a second; the host repeats its commands, which costs small corrections (under 0.5 m measured). In `coop-soak.sh` with two windows on one screen, the covered guest ran at 23 ticks a second.
3. Joining costs a guest one 15–30 ms frame while it builds the replicated grids.
4. Remote shots' sparks are still cosmetic: with aim anchors they aim at the same spot as on the host, but hit or miss is decided by each machine's own copy, so an occasional spark may show without host damage or the reverse.

## 11. References
- [ADR-0116](../decisions/ADR-0116-coop-prototype-host-authority.md).
- Teardown multiplayer (Voxagon blog, 2026-03-13): https://blog.voxagon.se/2026/03/13/teardown-multiplayer.html
- Unity Transport 6.6 (built in): `com.unity.transport`.
