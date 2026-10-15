# SPEC-90 — Co-op prototype (spike)

| | |
|---|---|
| **Status** | Spike complete: NET-002…011 working end to end in the co-op worktree |
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

## 10. Open questions
| # | Question | Default | Decider | Answer |
|---|---|---|---|---|
| 1 | Guest's own ship: host-authoritative first, guest-owned movement, or prediction from day one? | Host-authoritative first, prediction in NET-011 | Owner | |
| 2 | Second instance: Editor host plus a standalone dev build, or Multiplayer Play Mode virtual players? | Editor plus a dev build (also works Mac to Windows) | Owner | |
| 3 | Pause in co-op: never pause, or pause only when every player has a menu open? | Never pause | Owner | |
| 4 | A guest's ship blueprint: the host's playground flagship, or the guest's own blueprint sent at join? | The host's playground flagship for the spike | Owner | |

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

Known gaps:
1. Capture, apply and snapshots are managed main-thread code. The host completes all jobs once per frame while guests are connected. Burst jobs and per-system timing are the optimisation pass.
2. Prediction does not replay: forces only the host applies (impact impulses, ability stat boosts) show as corrections, and very large hits (a 500k test charge) snap the guest's ship. Normal combat showed no snaps.
3. A guest's own ship is host-authoritative, so its HUD target lock is local only.
4. Remote players' projectiles are cosmetic copies fired from the interpolated (100 ms old) pose, so they can look slightly offset from what the host simulates.

## 11. References
- [ADR-0116](../decisions/ADR-0116-coop-prototype-host-authority.md).
- Teardown multiplayer (Voxagon blog, 2026-03-13): https://blog.voxagon.se/2026/03/13/teardown-multiplayer.html
- Unity Transport 6.6 (built in): `com.unity.transport`.
