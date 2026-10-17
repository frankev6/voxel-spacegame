# SPEC-92 — Racing: tracks, race mode and racer ships

| | |
|---|---|
| **Status** | Ready (2026-10-16). Amended 2026-10-17: Track 01 is the Battle Graveyard, five racers are built (the Salvage Tug was removed), the racers fly the ADR-0120 engine line, and the grapple is cut. The amendment is waiting for owner review. Design: [ADR-0118](../decisions/ADR-0118-hit-force-hazards-tracks.md) |
| **Area code** | RACE (task IDs are `RACE-001`, `RACE-002`, …) |
| **Module(s)** | `Game.Galaxy` (`Galaxy/Racing/`), `Game.Data` (definitions), `Game.Simulation` (`RaceHold` in command conversion), `Game.Net` (replication), `Game.UI` and `Game.Presentation` (race HUD), `Game.App` (playground pad, session), `Game.Editor` (track builder, Windows build) |
| **Depends on specs** | SPEC-91 (zones, walls, mines, hit force), SPEC-90 and ADR-0117 (co-op), SPEC-10 (grid spawning), SPEC-20 (commands), SPEC-21 (weapons), SPEC-61 (HUD), SPEC-62 (camera) |
| **Depended on by** | — |
| **Milestone** | M4 (owner-directed track, 2026-10-16; ADR-0118 Consequences) |

> Read [01-architecture.md](../01-architecture.md), [02-conventions.md](../02-conventions.md) and [03-glossary.md](../03-glossary.md) before this spec. This spec may not contradict them; ADR-0118 records the contract changes it makes.

## 1. Purpose
A co-op race for 2–4 players. It is the first real way to show off multiplayer with a friend over the internet. Players fly purpose-built racer ships around a looping track with:
- several paths and shortcuts;
- boost sections and field walls to crash into;
- mine fields;
- breakable barricades that open a shortcut once someone shoots them down.

Weapons stay live, so players shoot each other off the racing line with SPEC-91's hit force. Everything a track is made of (zones, walls, mines, barrier blueprints, the track spawner) is built so star systems and encounters can reuse it.

## 2. Scope
**In scope:**
- `TrackDefinition` and the track spawner;
- one track, approved first as a top-down map: **Track 01, the Battle Graveyard** (RACE-002, owner 2026-10-17), built around a fleet of capital wrecks;
- the voxel barrier and obstacle blueprints;
- the race decider: phases, countdown, gates, laps, branches, finish order, wrong way, out of bounds, respawn;
- race replication and per-pilot PvP;
- the race HUD, gate visuals and a results board;
- five racer ships in two lines, approved by the owner (2026-10-17): Dart (Vestari: Needle, Kite, Gimbal) and Ram (Khuchad Clan: Battering Ram, Hammerhead). The Salvage Tug was removed the same day. Plus one shove weapon and the two racer abilities, Slip and Shove;
- a Windows dev build;
- a Tailscale playtest guide;
- the playtest with Daniel.

**Out of scope (and who owns it instead):**
- AI racers: later SPEC-23 work (open question 5);
- tracks in the campaign or star systems (a location type): later SPEC-31/33 work, using `TrackSpawnService`;
- rewards, leaderboards, saved best times: later;
- relay or NAT traversal: Tailscale covers it for now (owner, 2026-10-16); a relay belongs in a later SPEC-90 task;
- the hazards themselves: SPEC-91.

## 3. Decisions (binding)
1. **ADR-0118 decisions 9–12** apply as written.
2. **Visuals before build.** Two things are approved by the owner as images before they are built:
   - the track layout, as a top-down map with gates, branches, boost sections, walls and mine fields marked;
   - each racer ship, as 2–4 plan-view silhouette options.

   Reason: the owner approves silhouettes before 3D, and layouts are the same kind of decision (memory: ship design direction).
3. **Planar tracks.** Everything sits on the flight plane. Paths cross at level intersections, which are designed as collision risks. There are no ramps or vertical loops. Reason: SPEC-20 decision 9.
4. **A lap is valid when the pilot passes every required gate in order.** Branch gates are optional. A branch's gates must be passed in order once it is entered, so a path cannot be cut across a wall gap that has no gate. Reason: multiple paths and shortcuts without loopholes.
5. **The race is decided only on the authority.** That covers phase, countdown, progress, finish order and respawns. Guests apply the replicated `RaceState` and never derive progress themselves. Reason: ADR-0117 decision 2. Gate crossings are derived everywhere (SPEC-91 `ZoneContacts`), but only the authority consumes them.
6. **Racers are rivals.** During a race each pilot gets a race team (`Allegiance.Team` = 100 + slot), so friendly-fire filters, where any exist, treat racers as hostile to each other. The pilot's own team is restored after the race. Reason: the owner wants to shoot each other.
7. **Damage persists through a race.** A destroyed racer respawns after 3 s at the respawn point of its last passed required gate, in a fresh ship of the same blueprint, and keeps its lap progress. Its hulk stays on the track as an obstacle until the race ends. Reason: destruction is a pillar, and wrecks make later laps different.
8. **No defeat screen during a race.** `DefeatWatcher` ignores pilot deaths while `RaceState.Phase` is `Countdown` or `Racing`. Reason: respawn replaces defeat.
9. **Default race rules:** 3 laps, 2–4 pilots, a countdown from 3, and a 60 s finish window after the first finisher.
10. **The track lives in the playground**, spawned 1,500 m south of the pads by pad `18  RACE`. Co-op runs only in the sandbox (SPEC-90). Reason: Daniel's first test.
11. **Track 01 is a battle graveyard** (owner, 2026-10-17): "instead of just having one ship, its a group of derelict ships, like an ancient battle took place with large ships and we go through and around them."
    - The layout and its routes are in `docs/design/racing/track-01-options.md` and `track_01_battle_graveyard.json`.
    - The wrecks are hollow shells at 2 m cells (ADR-0119 decision 10).
    - The grapple is cut.
12. **Five racers are built** (owner: "for ships i actually like all options you made, lets create all 6"; then, 2026-10-17: "remove the salvage tug ship, it has no place in this"). The three Darts share the Slip ability and the two Rams share Shove. Each hull keeps its own handling.
13. **Racers fly the ADR-0120 engine line, and their hulls are shaped** (owner, 2026-10-17: "you need to also make new thruster engines … also in your building of the ships you missed to use a lot of the hull block options").
    - Darts fly sprint engines (fast spool, hardest boost) and verniers. Rams fly torches (slow spool, most thrust), with sprint engines for correction.
    - `export_racers.py` shapes each hull as a height field. Domes rise from the outline and are chamfered underneath. Plates, cabins and barrels stand proud with chamfered edges.
    - Outline edges within 8.5° of a kit line (grid, 45° or 1:2) are snapped onto that line through cell corners, so the builder seats wedges and long slopes along the silhouette. Slopes run along the grid only; diagonal tops step. The Gimbal's ring is an octagon.

14. **First playable (2026-10-17).** These differ from §4–§5 as written:
    - **Placement.** The track spawns 3.3 km south of the pads, so its north straight is about 2.2 km away.
    - **Co-op.** The race reaches guests as `race view` and `race sync` over the dev command channel, which works in development builds only. A replicated `RaceState` message replaces it later.
    - **Respawn.** A respawned ship has no shield, because the playground fits shields only at session start.
    - **Guide.** `docs/RACING-WITH-FRIENDS.md` is the playtest guide.

## 4. Data model

### 4.1 Track content (`Game.Data.Definitions.Racing`, public contract)
```csharp
public sealed class TrackDefinition : ContentDefinition
{
    public string DisplayName;
    public int DefaultLaps = 3;
    public TrackGate[] Gates;            // index = gate id; gate 0 is start/finish
    public TrackPiece[] Pieces;          // voxel walls, pylons, barricades, obstacles
    public TrackZone[] Zones;            // boost strips, slow fields, field walls (SPEC-91 ZoneDefinition)
    public TrackMines[] MineFields;      // SPEC-91 MineFieldDefinition
    public Vector2[] Bounds;             // closed polyline; becomes a Repel path zone and a Bounds zone
    public TrackSlot[] StartGrid;        // ≥ 4
    public Texture2D LayoutMap;          // the approved top-down map (doc and loading screen)
}
[Serializable] public struct TrackGate
{
    public Vector2 Position; public float Yaw; public float Width;   // gate line is Width across, perpendicular to Yaw
    public bool Required;                                            // on every valid lap
    public int[] Next;                                               // ids reachable next (branches); validator: graph from 0 back to 0
    public Vector2 RespawnPosition; public float RespawnYaw;
}
[Serializable] public struct TrackPiece { public ShipBlueprintDefinition Blueprint; public Vector2 Position; public float Yaw; public TrackPieceKind Kind; }
public enum TrackPieceKind : byte { Wall = 0, Pylon = 1, Barricade = 2, Obstacle = 3, Decor = 4 }   // Obstacle = dynamic (pushable)
[Serializable] public struct TrackZone { public ZoneDefinition Zone; public Vector2[] Path; public Vector2 Position; public float Yaw; }
[Serializable] public struct TrackMines { public MineFieldDefinition Field; public Vector2 Position; public float Yaw; public uint Seed; }
[Serializable] public struct TrackSlot { public Vector2 Position; public float Yaw; }
```
Pieces of kind `Wall`, `Pylon` and `Decor` spawn as `GridKind.Station` with `GridProfile.Station`. They are static and destructible like stations. `Barricade` spawns the same way, but its blueprint is light and its gate sits behind it. `Obstacle` spawns as `GridKind.Asteroid` with a dynamic body, so shots and rams push it.

### 4.2 Race state (`Game.Galaxy.Racing`, public contract)
```csharp
public enum RacePhase : byte { Idle = 0, Lobby = 1, Countdown = 2, Racing = 3, Finished = 4 }
public struct RaceState : IComponentData          // singleton; writer RaceSystem (authority), replicated
{
    public RacePhase Phase; public uint PhaseTick; public byte Laps; public ushort TrackGroup; public ContentHash Track;
    public uint FirstFinishTick;
}
[InternalBufferCapacity(4)]
public struct RacerProgress : IBufferElementData // on the RaceState entity, one per pilot slot in the race
{
    public byte Slot; public byte Lap; public short LastGate; public short ExpectedGate; public ushort BranchMask;
    public uint LapStartTick, BestLapTicks, FinishTick; public byte Place; public byte Flags; // WrongWay=1, OutOfBounds=2, Respawning=4, Finished=8
    public uint OutOfBoundsSinceTick; public uint RespawnAtTick;
}
public struct RaceHold : IComponentData, IEnableableComponent { }   // on pilot roots; writer RaceSystem; read by PilotCommandConversion
public struct RaceRespawnRequest : IComponentData { public byte Slot; public float2 Position; public float Yaw; } // authority → App session
```

### 4.3 Commands and HUD
- `PilotCommandConversion.Convert` zeroes thrust, turn, fire groups and edges while the pilot's root has `RaceHold` enabled.
- The HUD view model `RaceHudModel` (UI) holds: phase, countdown seconds, lap `n/N`, place `p/P`, lap time, best lap, last split delta, next-gate world position, the wrong-way and out-of-bounds warnings, and the results rows.

## 5. Systems, services and flow

| System or service | Group and order | Role | Does |
|---|---|---|---|
| `TrackSpawnService` | managed, Galaxy | Authority (guests get grids and zones through replication) | `Spawn(TrackDefinition, origin, group)`: pieces → `GridSpawnRequest`s; zones, bounds and mines → `HazardSpawn`; gate zones from `Gates`. `Despawn(group)` |
| `RaceSystem` | `GameplayPostPhysicsGroup`, after `ZoneSystem` (SPEC-91) | AuthorityOnly | Phases, holds, progress from `ZoneContacts`, places, out of bounds, respawn requests |
| `RaceRespawnService` | managed, App session, frame update | Authority | Consumes `RaceRespawnRequest`: spawns the pilot's blueprint at the pose with `Pilot { Slot }`; on a guest, the existing pilot-ship path tags it `PlayerShip` |
| `RaceHudSyncSystem` | `PresentationBridgeGroup`, after `HudSyncSystem` | Both | `RaceState` + local pilot → `RaceHudModel`, only on change |
| `GateVisualSystem` | `PresentationBridgeGroup` | Both | Gate posts and the line; next gate highlighted for the local pilot; shortcut gates tinted |
| `RaceSession` (dev) | App, `DevPlaygroundRace` | Authority | Pad and `race` commands: spawn the track, put pilots in racer ships at the start grid, start and end |

### 5.1 Race flow
1. **Lobby.** Pad `18  RACE` (or `race open [track]`) spawns the track. It moves every pilot (host and guests) to the start grid in their chosen racer ship (`race ship <id>`, default alternating the two racers). It enables `RaceHold` and sets the race teams.
2. **Countdown.** `race start`, or every pilot pressing F at the grid. The phase moves to `Countdown`, and the HUD shows 3, 2, 1 for 3 s. On GO, `RaceHold` is disabled, the phase becomes `Racing`, and every `LapStartTick` is set to the current tick.
3. **Progress.** `RaceSystem`, for each `ZoneContact` of kind `Gate` with `Crossed = +1`, from a pilot root in the race:
   - **The expected gate** (`ExpectedGate`, or any id in the current branch's `Next` set): advance `LastGate` and update `ExpectedGate`. A non-required gate that starts a branch sets its bit in `BranchMask`.
   - **Gate 0 after every required gate:** finish the lap. `Lap++`, record the lap time and best lap, and reset `BranchMask`. On the last lap, set `Finished`, `FinishTick` and `Place`.
   - **A gate behind the pilot** (`Crossed = −1`, or a gate already passed this lap): set `WrongWay` until the expected gate is crossed.
4. **Places.** These are recomputed every tick, ordered by:
   1. `Finished`, then by `FinishTick`;
   2. otherwise lap;
   3. then gates passed this lap;
   4. then the distance to the expected gate, ascending.
5. **Out of bounds.** A `Bounds` contact with `Inside = 0` starts `OutOfBoundsSinceTick`. After 5 s outside, request a respawn.
6. **Death.** When a pilot root becomes a `Hulk`, set `Respawning` and `RespawnAtTick = now + 180`, then post a `RaceRespawnRequest` at that tick. The new ship gets `RaceHold` disabled and the same race team.
7. **Manual reset.** Holding the interact key for 1.5 s posts a respawn request after 1 s. It counts as a respawn, with no lap penalty.
8. **Finish.** The phase becomes `Finished` once all pilots finish, or 60 s after the first finisher. The HUD shows the results board. `race end`, or the pad, despawns the track group, restores teams and returns pilots to the pads.

### 5.2 Co-op
- `RaceState` and its `RacerProgress` travel reliably on change as `RaceState`. Countdown and lap timers are sent as "ticks since phase start", as ability ends are (ADR-0117 note 5).
- `RaceHold` is replicated on pilot roots (the guest's own command conversion needs it for prediction). The race teams travel in the existing allegiance state.
- Respawned ships go through the existing grid spawn and pilot path. A guest whose `PlayerShip` is replaced re-targets its camera and HUD (`CameraTargetSyncSystem` already follows `PlayerShip`).

## 6. Public API
- `TrackSpawnService.Spawn(TrackDefinition track, double3 originInStarSystem, ushort group)` and `.Despawn(ushort group)`. Precondition: the definition passes the validator (§7).
- Dev commands:
  - `race open [track]`, `race ship <blueprint>`, `race start`, `race end`, `race laps <n>`;
  - `race info`: phase, places, gates;
  - `race respawn`;
  - `race map`: writes the layout map PNG for review, from the definition.

## 7. Edge cases and failure behaviour
| Situation | Required behaviour |
|---|---|
| Gate graph has no path from gate 0 back to gate 0 through all required gates | Validator error naming the gate (content import fails) |
| Pilot cuts across a branch without its first gate | Next required gate is still expected; the cut does not count |
| Two pilots cross the finish line in the same tick | Order by crossing fraction along the segment (earlier fraction first), then slot |
| Guest disconnects mid-race | Their row stays as DNF; the race continues |
| Pilot joins while racing | Spectates (no ship on the grid) until the next race |
| Respawn point blocked by a hulk | Spawn offset sideways up to 3 × 20 m; if all blocked, wait one second and retry |
| Track spawned 1,500 m away triggers an origin rebase | Zones, gates and grids shift with SPEC-91's rules; progress is unaffected |
| Mine field near the start grid | Validator warns if any mine lies within 120 m of a start slot |

## 8. Performance budget
Advisory (ADR-0042):
- the track, with about 60 static wall pieces, 6 obstacles, 40 mines, 20 zones and 4 racers, must hold the playground's frame time within 2 ms of the empty playground;
- `RaceSystem` under 0.05 ms;
- race replication under 2 kbps per guest outside respawns.

## 9. Tasks

### RACE-001 — Public contract types
- **Milestone:** M4
- **Tier:** standard — transcribes §4 with no behaviour
- **Depends on:** `HAZ-001`
- **Do:**
  - Add the §4 types.
  - Add `RaceHold` handling stubs in `PilotCommandConversion` (no-op until RACE-005).
  - Make the `TrackDefinition` validator skeleton.
- **Files:** `Assets/Game/Data/Definitions/Racing/*.cs`, `Assets/Game/Galaxy/Racing/*.cs`, `Assets/Game/Simulation/Commands/PilotCommandConversion.cs`.
- **Acceptance criteria:**
  - [ ] Compiles; existing tests pass.
- **Tests:** `RaceContractTests.Types_Exist`.

### RACE-002 — Track layout for owner approval
- **Milestone:** M4
- **Tier:** standard — design work producing images; the owner chooses
- **Depends on:** —
- **Do:** Design 2–3 layout options for the first track. Each is a top-down map at true scale, about 1.5–2.5 km across, with:
  - the start grid;
  - required gates and two or three branches (one long safe line, one short shortcut through a mine field or behind a breakable barricade);
  - at least one level crossing;
  - boost sections, field-wall sections, voxel walls and pylons;
  - mine fields and drifting obstacles.

  Give each option an estimated lap time for the fast racer and a short "what makes it fun" note. Show them to the owner as images and record the chosen one.
- **Files:** `docs/design/racing/track-01-options.md`, `docs/design/racing/img/*.png`, `ArtSource/Racing/track_layouts.py`.
- **Acceptance criteria:**
  - [ ] The owner has picked (or adjusted) one layout.
  - [ ] Its gates, branches and hazards are listed as coordinates ready for RACE-004.
- **Tests:** none (design deliverable); the layout script runs headless and writes the images.

### RACE-003 — Racer ship silhouettes for owner approval
- **Milestone:** M4
- **Tier:** standard — design work producing images; the owner chooses
- **Depends on:** —
- **Do:** Following the ship-design direction, design two racers, each with a faction, a doctrine, a niche and a counter. Give each 2–4 black plan-view silhouette options, with notes on mass, thrust layout, weapon mounts and how it uses the mass-against-knockback trade-off. Default concepts:
  - **"Dart"**: light and very fast, it turns on its core. Hits throw it around, so it wins by avoiding contact. One fixed light gun.
  - **"Ram"**: a heavy racer with a reinforced prow and a forward shove weapon (RACE-008). It is slower but barely moves when hit, and it knocks rivals and obstacles off the line.
- **Files:** `docs/design/racing/racers.md`, `docs/design/racing/img/racer_*.png`, `ArtSource/Racing/racer_silhouettes.py`.
- **Acceptance criteria:**
  - [ ] The owner has approved one silhouette for each racer (or asked for another round).
- **Tests:** none (design deliverable).

### RACE-004 — Track builder, barrier blueprints and spawner
- **Milestone:** M4
- **Tier:** frontier — spawns many grids, zones and mines with stable ids, across roles
- **Depends on:** `RACE-001`, `RACE-002`, `RACE-012`, `HAZ-007`, `HAZ-008`
- **Do:**
  - Build Track 01 from `track_01_battle_graveyard.json`: the wrecks from RACE-012, field walls, zones, mines and gates. Mechanics that land later attach in RACE-015.
  - Make the barrier and obstacle blueprints from human-hull blocks, NOCTURNE paint:
    - `track_wall_straight` and `track_wall_curve`;
    - `track_pylon`;
    - `track_barricade` (thin, breakable);
    - `track_obstacle_rock` (dynamic).
  - Write an editor `TrackBuilder` that turns the approved layout coordinates into `track_01.asset`, filling wall runs with pieces along polylines.
  - Implement `TrackSpawnService` (§5) and the validator (§7).
  - Implement `race map`.
- **Files:** `Assets/Game/Editor/Content/TrackBuilder.cs`, `Assets/Game/Galaxy/Racing/TrackSpawnService.cs`, `TrackValidator.cs`, `Assets/Game/Content/Racing/*`, `Assets/Game/Content/Ships/track_*.asset`.
- **Acceptance criteria:**
  - [ ] `track_01` validates.
  - [ ] Spawning and despawning it 10 times leaks no entities.
  - [ ] A screenshot from above matches the approved map.
  - [ ] Barricades break into chunks under fire and open their shortcut gate's line.
- **Tests:** `TrackValidatorTests.MissingLoop_Fails`, `.MineNearStart_Warns`, `TrackSpawnTests.SpawnDespawn_NoLeaks`, `.StableIds`.

### RACE-005 — Race decider
- **Milestone:** M4
- **Tier:** frontier — the authority's rules over gates, branches, places, holds and respawns
- **Depends on:** `RACE-004`, `HAZ-006`
- **Do:**
  - Implement `RaceSystem` (§5.1 steps 2–8).
  - Implement `RaceHold` in `PilotCommandConversion`.
  - Implement race teams (decision 6).
  - Make `DefeatWatcher` ignore deaths during a race.
  - Add the `race` dev commands (except `map`, which is RACE-004).
- **Files:** `Assets/Game/Galaxy/Racing/RaceSystem.cs`, `RaceRules.cs`; `Assets/Game/Simulation/Commands/PilotCommandConversion.cs`; `Assets/Game/App/Session/DefeatWatcher.cs`; `Assets/Game/App/DevRuntime/Commands/RaceDevCommand.cs`.
- **Acceptance criteria:**
  - [ ] Scripted pilots complete 3 laps.
  - [ ] A shortcut lap is valid only through its gates.
  - [ ] A wall-cut is rejected.
  - [ ] Wrong way and out of bounds raise their flags; out of bounds respawns after 5 s.
  - [ ] Places are correct at every tick of a scripted overtake.
  - [ ] A same-tick finish orders by crossing fraction.
  - [ ] Hold blocks thrust and fire during the countdown.
- **Tests:** `RaceRulesTests.ThreeLaps`, `.Shortcut_Valid`, `.WallCut_Rejected`, `.WrongWay`, `.OutOfBounds_Respawns`, `.Places_Overtake`, `.SameTickFinish`, `.Hold_BlocksInput`.

### RACE-006 — Respawn
- **Milestone:** M4
- **Tier:** frontier — replaces a pilot's ship mid-session on host and guest
- **Depends on:** `RACE-005`
- **Do:**
  - Implement `RaceRespawnService`: spawn at the gate respawn pose with blocked-point offsets, the same blueprint, `Pilot { Slot }`, the race team and `RaceHold` disabled.
  - Keep the hulk.
  - Make sure camera, HUD and input follow the new `PlayerShip` on host and guest.
- **Files:** `Assets/Game/App/Session/RaceRespawnService.cs`; `Assets/Game/Net/Replication/GuestReplicator.cs` (only if the guest's pilot path needs it); tests.
- **Acceptance criteria:**
  - [ ] A destroyed racer flies again in 3 s at its last gate, on the host and on a guest.
  - [ ] The old hulk stays.
  - [ ] The camera follows the new ship.
  - [ ] Progress is kept.
- **Tests:** `RaceRespawnTests.Death_RespawnsAtGate`, `.Blocked_Offsets`, `.KeepsProgress`; co-op check recorded.

### RACE-007 — Race HUD, gate visuals and results
- **Milestone:** M4
- **Tier:** standard — presentation over finished state, NOCTURNE tokens
- **Depends on:** `RACE-005`
- **Do:**
  - Build the race HUD panel: lap, place, lap time, best lap, split delta, the countdown in the centre, and the wrong-way and out-of-bounds banners.
  - Add a next-gate marker on screen and at the screen edge.
  - Implement `GateVisualSystem`: posts, line shimmer, the next gate highlighted, shortcut gates tinted.
  - Build the results board.
- **Files:** `Assets/Game/UI/Screens/Race/*` (UXML, USS, view), `Assets/Game/UI/ViewModels/RaceHudModel.cs`, `Assets/Game/Presentation/Racing/RaceHudSyncSystem.cs`, `GateVisualSystem.cs`.
- **Acceptance criteria:**
  - [ ] The HUD updates only on change.
  - [ ] It is legible at 1280×720 and 4K.
  - [ ] It passes `UILayoutAudit`.
  - [ ] Screenshots of countdown, racing and results are in the ledger log.
- **Tests:** `RaceHudTests.Binds_Model`, `.Countdown`, `.Results`; `AuditTests` covers the new screen.

### RACE-008 — The Dart line: Needle, Kite and Gimbal
- **Milestone:** M4
- **Tier:** standard — content builds to approved silhouettes
- **Depends on:** `RACE-003`, `HAZ-001`
- **Do:**
  - Write `RacerShipBuilder` (an editor builder like `NocturneShipBuilder`).
  - Build the three Vestari racers from the approved plan views in `racer_silhouettes.py`. Use existing blocks at 0.5 m cells; the top layer follows the silhouette and the hull is 3–5 layers deep.
  - Use the Vestari livery from the sheet as paint, and the race numbers 07, 22 and 31.
  - Handling per the sheet:
    - **Needle:** pods wide of the centre of mass, so it yaws hard;
    - **Kite:** a radiator sail, so it boosts longer;
    - **Gimbal:** a thruster ring, so it thrusts equally in every direction.
- **Files:** `Assets/Game/Editor/Content/RacerShipBuilder.cs`, `Assets/Game/Content/Ships/ship_racer_needle.asset`, `ship_racer_kite.asset`, `ship_racer_gimbal.asset`.
- **Acceptance criteria:**
  - [ ] All three validate, spawn and fly in the playground.
  - [ ] Top-down screenshots match the approved silhouettes.
  - [ ] Mass, thrust and top speed are recorded.
- **Tests:** `RacerShipTests.Darts_Validate`, `.Darts_Fly`.

### RACE-012 — Graveyard wrecks
- **Milestone:** M4
- **Tier:** standard — large content builds from the approved map, at 2 m cells
- **Depends on:** `RACE-002`, `HAZ-001`
- **Do:** Build the wreck blueprints of Track 01 as hollow shells at 2 m cells, with torn edges where the map shows breaks:
  - the dreadnought's stern and bow halves;
  - the carrier, with its flight deck open end to end (the hangar run) and slots for the bulkheads and door;
  - the battleship, with turret barbettes on its south flank and a reactor breach on its north;
  - the engine section;
  - two cruisers (one burned out);
  - two frigates rammed together;
  - the salvage rig.

  Damage cell HP is set so racers' guns can open bulkheads in a few seconds but cannot cut capital hulls in a race. Record cell counts.
- **Files:** `Assets/Game/Editor/Content/WreckBuilder.cs`, `Assets/Game/Content/Ships/wreck_*.asset`.
- **Acceptance criteria:**
  - [ ] Each wreck validates and spawns.
  - [ ] No wreck is over 600k cells.
  - [ ] Top-down screenshots match the map.
  - [ ] The flight deck is flyable end to end.
- **Tests:** `WreckContentTests.Validate_CellBudget`, `.FlightDeck_Clear`.

### RACE-013 — The Ram line: Battering Ram and Hammerhead
- **Milestone:** M4
- **Tier:** standard — content builds to approved silhouettes, plus one weapon
- **Depends on:** `RACE-008`
- **Do:**
  - Build the two Khuchad racers in `RacerShipBuilder`, with the Khuchad livery and the race numbers 13 and 66. They fly torch engines (ADR-0120).
  - Add `wpn_impact_driver`: kinetic and fixed, with low damage, high `projectileMass` and slow fire. Mounts:
    - **Battering Ram:** spinal;
    - **Hammerhead:** one at each tip of the hammer plate.
- **Files:** `Assets/Game/Content/Ships/ship_racer_battering_ram.asset`, `ship_racer_hammerhead.asset`, `Assets/Game/Content/Weapons/wpn_impact_driver.asset`, `Assets/Game/Content/Blocks/weapon_impact_driver.asset`.
- **Acceptance criteria:**
  - [x] Both validate, spawn and fly. Torch launch measured: the Battering Ram reaches 3.7 m/s at 0.5 s and 61 m/s at 4 s; the Hammerhead 7.0 and 83.
  - [ ] Top-down screenshots match the approved silhouettes.
  - [ ] The impact driver fires.
  - [ ] Its knockback is measured once HAZ-005 lands.
- **Tests:** `RacerShipTests.Rams_Validate`, `.Rams_Fly`, `.ImpactDriver_Fires`.

### RACE-014 — Racer abilities: Slip and Shove
- **Milestone:** M4
- **Tier:** standard — two abilities on the SPEC-24 framework
- **Depends on:** `RACE-013`, `HAZ-002`
- **Do:**
  - **Slip** (the Darts): a sideways impulse of about 25 m/s through `ImpulseRequest`, with a 6 s cooldown.
  - **Shove** (the Rams): a 60 m radial impulse on ships, mines and rocks, with no damage and a 10 s cooldown.
  - Both are general abilities usable by any ship.
- **Files:** `Assets/Game/Content/Abilities/ability_slip.asset`, `ability_shove.asset`; `Assets/Game/Simulation/Abilities/*`.
- **Acceptance criteria:**
  - [ ] Slip moves a Dart sideways by its Δv and the move carries.
  - [ ] Shove pushes everything in range away and damages nothing.
  - [ ] Cooldowns hold.
- **Tests:** `RacerAbilityTests.Slip_Sideways`, `.Shove_PushesNoDamage`.

### RACE-015 — Track 01 set pieces
- **Milestone:** M4
- **Tier:** standard — configures finished systems on the track
- **Depends on:** `RACE-004`, `HAZ-013`, `HAZ-014`, `HAZ-016`, `HAZ-017`, `HAZ-018`, `HAZ-019`, `HAZ-020`, `HAZ-021`
- **Do:** Wire the graveyard's mechanisms and lap events (SPEC-92 decision 11):
  - the bow half drifts, narrowing the Break to 130, 90 and 50 m;
  - the rig's arms sweep;
  - the flight-deck door and its switch;
  - the battleship's turrets wake on lap 2 with a leader override;
  - the reactor pulse with its shadows (every 8 s, then 4 s on lap 3);
  - the engine section fires in turn;
  - the minefield payloads;
  - the canisters;
  - the lane-gate portal pair.
- **Files:** `Assets/Game/Content/Racing/track_01*.asset`, `Assets/Game/Content/Hazards/mech_track01_*.asset`.
- **Acceptance criteria:**
  - [ ] A scripted three-lap run sees every set piece act at its lap.
  - [ ] Top-down screenshots per lap are in the ledger log.
- **Tests:** `Track01Tests.LapEvents_Fire`, `.Break_Narrows`.

### RACE-009 — Race replication and co-op start
- **Milestone:** M4
- **Tier:** frontier — new reliable state and per-pilot holds under prediction
- **Depends on:** `RACE-006`, `HAZ-010`
- **Do:**
  - Add the `RaceState` message (§5.2) and `RaceHold` replication, and bump the protocol.
  - Add the role-policy entries.
  - Add pad and lobby flow for guests (a guest pressing F at the grid counts as ready).
  - Add race fields to `coop-parity.sh`.
- **Files:** `Assets/Game/Net/Protocol/*`, `Assets/Game/Net/Replication/*`, `Assets/Game/App/Session/RoleSystemPolicy.cs`, `tools/playtest/coop-parity.sh`.
- **Acceptance criteria:**
  - [ ] Host and guest see the same countdown (±1 tick of display), places and laps.
  - [ ] A guest held at the grid cannot creep forward.
  - [ ] The parity diff is empty.
- **Tests:** `RaceReplicationTests.State_RoundTrip`, `.Hold_Predicted`; parity run recorded.

### RACE-010 — Windows dev build and remote playtest guide
- **Milestone:** M4
- **Tier:** standard — build plumbing and a written guide
- **Depends on:** —
- **Do:**
  - **Owner step first:** install "Windows Build Support (Mono)" for Unity 6000.6.5f1 in Unity Hub. Today only Mac and WebGL support are installed.
  - Add `DevBuild.BuildWindowsDev` (`StandaloneWindows64`, Mono, development, same scenes) and `tools/build-dev.sh windows`, which writes a zip under `Builds/dev-win/`.
  - Check that Burst compiles for Windows from macOS. If it cannot, record that, and ship with Burst disabled for that build as a temporary measure.
  - Write `tools/playtest/REMOTE.md`:
    - both players install Tailscale and join the same tailnet;
    - the host runs `net host`;
    - Daniel launches the build with `-coop-join <host tailscale IP>`, or uses the in-game join command;
    - expected ping, and what to do on "Version mismatch".
  - Add a `join.bat` next to the exe that asks for the IP.
- **Files:** `Assets/Game/Editor/Build/DevBuild.cs`, `tools/build-dev.sh`, `tools/playtest/REMOTE.md`, `tools/playtest/windows/join.bat`.
- **Acceptance criteria:**
  - [ ] The Windows build is produced on this Mac.
  - [ ] It starts and joins a Mac host over Tailscale; verified with Daniel, or with any Windows machine.
  - [ ] The guide works step by step.
- **Tests:** `DevBuildTests.WindowsTarget_Configured` (EditMode); the live join is recorded in the ledger log.

### RACE-011 — Two-player race playtest with Daniel
- **Milestone:** M4
- **Tier:** standard — scripted session plus notes
- **Depends on:** `RACE-007`, `RACE-008`, `RACE-009`, `RACE-010`, `RACE-013`, `RACE-014`, `RACE-015`
- **Do:** Run a full race with Daniel over Tailscale:
  - 3 laps, both racers, mines, barricades and shortcuts;
  - `net status` RTT and loss, and frame times on both machines;
  - parity checked once per lap.

  Write a short report of bugs and feel notes, with screenshots and a 30 s clip if possible. File the bugs as new tasks.
- **Files:** `docs/playtests/2026-10-race-01.md`.
- **Acceptance criteria:**
  - [ ] A complete race finishes, with results on both machines.
  - [ ] The report is written and its bugs are filed.
- **Tests:** none (live session); the scripted single-machine equivalent is `RaceScenarioTests.FullRace_TwoPilots` (PlayMode, host plus headless guest), which must pass first.

## 10. Open questions
| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | Is a planar track with level crossings fine? There can be no ramps or loops over one another. | Yes (decision 3) | Owner |
| 2 | When a racer is destroyed, is it a 3 s respawn at the last gate, with its hulk left as an obstacle? | Yes (decision 7) | Owner |
| 3 | Are all weapons live in races, or only the racers' own? | Racers' own loadouts only; other ships cannot join a race | Owner |
| 4 | Laps and player count? | 3 laps, 2–4 pilots | Owner |
| 5 | AI racers to fill the grid? | Not now | Owner |
| 6 | Track name and racer names? | Labels `track_01`, Dart and Ram until the owner names them | Owner |
| 7 | Where is the track in the world? | Playground, 1,500 m south of the pads (decision 10) | Owner |
| 8 | Should ramming a rival count as a legal move? | Yes; rams already deal damage both ways | Owner |

## 11. References
- ADR-0118, SPEC-91, SPEC-90, ADR-0116, ADR-0117.
- Ship-design direction (owner memory): silhouettes first, form follows function.
- `docs/HANDOFF.md` and `tools/playtest/README.md` (co-op loop).
