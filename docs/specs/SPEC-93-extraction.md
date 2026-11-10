# SPEC-93 — Extraction: the den, the heist and the tow

| | |
|---|---|
| **Status** | In progress |
| **Area code** | EXT (task IDs are `EXT-001`, `EXT-002`, …) |
| **Module(s)** | `Game.Simulation` (`Simulation/Extraction/`), `Game.Data` (`Data/Extraction/`), `Game.Presentation` (cable, HUD), `Game.App` (`App/DevRuntime/Extraction/`: the places and the loop), `Game.Editor` (place builders) |
| **Depends on specs** | SPEC-12 (bodies, mass), SPEC-13 (`DamageStreams` impulses and damage), SPEC-20 (flight carry of `ExternalPush`), SPEC-23 (AI targeting), SPEC-33 (factions), SPEC-91 (zones, contacts), ADR-0119 (movers, mechanisms, portals), ADR-0123 (3D flight) |
| **Depended on by** | — |
| **Milestone** | M4 (owner-directed track, 2026-11-10) |

> Read [01-architecture.md](../01-architecture.md), [02-conventions.md](../02-conventions.md) and [03-glossary.md](../03-glossary.md) before this spec. This spec may not contradict them. The design it builds is in [docs/design/places/README.md](../design/places/README.md): the owner's decisions in his words, the Tollhouse Nine sheets and the Rock den sheet. [ADR-0124](../decisions/ADR-0124-extraction-loop.md) records the pivot.

## 1. Purpose

The game becomes a co-op extraction game (owner, 2026-11-10). A crew lives in a **den**, a natural asteroid they dig into and build on. From the den's **board** anyone in the crew takes a job, and the crew leaves through the den's **gate**. The first job is a **heist**: steal a **station core**, which is rare, powers the den and lets the crew start building. The core is **tethered** to its station; cutting the last tether sets off the **alarm**. The crew can stay unseen: a **transponder** is a disguise, watchers notice what does not fit, and a heavy load on a **magnet tow** knocks on walls and can be heard. A ship cannot jump with a towed load unless its **cargo blocks** can take the mass. Back home, the core goes into the den's **socket**; the den's guns and doors run on its power, and the den has to be held when someone comes for the core.

## 2. Scope

**In scope:**
- The magnet tow: a cable between a ship and a load, mass-true, which knocks, snaps and drops.
- The cargo rule at gates; stowing a load in a hold that can take it.
- The station core, its tethers, district power and blackout; the core socket and den power.
- The transponder (off, or on with a code), watchers, restricted zones, suspicion, identification and the alarm timeline.
- Tollhouse Nine ("the Lantern") and the Rock as voxel places in the playground's star system, a gate pair between them, the board, and the loop from the board to the socket.
- Den building from the ship: digging rock and placing modules; a den defence scenario.
- Playground commands and scenario probes for all of it; EditMode tests for each system.

**Out of scope (and who owns it instead):**
- Co-op replication of the new state beyond what ADR-0117 already replicates (roots, poses, cells): EXT-012, later.
- Final art, audio and UI polish: SPEC-80/81, after the owner has played it.
- Galaxy-scale travel between star systems: SPEC-32. The loop runs inside the playground's star system.
- Ship editor changes: SPEC-51. The den is built in the world, from the ship.

## 3. Decisions (binding)

1. **Places come first; jobs follow from them.** Every feature of a heist place has a reason to exist apart from the heist (owner: "make a proper lived in feeling world/scenario"). Ways in, cover and ways out are listed per place, never a single solution.
2. **The tow is a cable, not a teleport.** `TowLink` joins two physics bodies with a one-sided spring (slack under its length, a spring and damper over it). Forces go through `DamageStreams.Impulses` (`ProducerKind.Tow`, appended), so the ship's flight assist carries them (SPEC-91) and mass shows in handling. The salvage collector (QA-001) stays for wrecks.
3. **Knocks are noise.** A towed load whose contact speed with any other body exceeds `KnockSpeed` produces a `NoiseEvent` at the contact. Noise is heard by watchers within its radius; a heard knock raises suspicion of the ship towing the load.
4. **The cargo rule is checked where a ship leaves**: a gate (portal) refuses a ship whose tow load's mass exceeds its cargo capacity in tonnes. A load stowed in a hold (`Stow`) leaves the world and travels as an item; it can only be stowed when the hold can take it.
5. **A station core is a dynamic grid held by kinematic tethers.** Each `CoreTether` names a district. Cutting a tether (damage or the cut key) blacks out its district. When the last tether goes, the core is free and the station's alarm starts. Cutting is the only way to free a core; shots cannot break it (the core grid is `DamageImmune`).
6. **The transponder is a disguise.** A ship's `Transponder` is `Off` or broadcasts one `AccessCode` (Trader, Work, Garrison). Codes open the matching `AccessZone`s without suspicion; a dark ship is noticed by any watcher that sees it.
7. **Watchers decide, identification is per ship.** A watcher (searchlight, sensor, garrison ship, crowd) that sees a ship which does not fit raises that ship's `Suspicion`. At 1 the ship is `Wanted` by the watcher's faction. AI treats a `Wanted` ship as hostile whatever the faction stance; nobody else is. The alarm says the core is gone, not who has it.
8. **The alarm is a timeline of mechanism actions.** `HeistDirector` (authority) runs the place's `AlarmStep` list against the alarm's start tick: lock doors, lock the mass driver, launch cutters, scan the lanes, the cruiser. Places differ in data, not code (ADR-0119 decision 9).
9. **Authority decides; every machine applies** (ADR-0117). Tow forces, knocks and blackouts are applied everywhere; suspicion, identification, the alarm and outcomes are decided on the authority only.
10. **The den is a station grid of rock you can dig and build on from your ship.** Digging removes rock cells (and gives ore); building places modules from a small menu (guns, lamps, doors, cable posts, tanks). Guns and doors work only while the den has power from a socketed core.
11. **The loop is one star system.** The den and the heist place sit kilometres apart in the playground's system, joined by a gate pair. Leaving through a gate with a load breaks rule 4 or obeys it.

## 4. Data model

```csharp
// Game.Data.Combat (append)
public enum ProducerKind : byte { /* …, */ Tow = 13 }

// Game.Simulation.Extraction — public contract
public struct TowLink : IComponentData            // on the towing ship's root
{
    public Entity Load;                           // a root with PhysicsVelocity + PhysicsMass, or Null
    public float3 ShipAnchor, LoadAnchor;         // body-local attach points (m)
    public float Length;                          // slack length (m)
    public float Stiffness, Damping;              // spring N/m per tonne of the lighter body, damping ratio
    public float BreakForce;                      // N; over it the cable snaps (0 = unbreakable)
    public uint SinceTick;
}
public struct TowLoad : IComponentData { public Entity Tower; public float MassT; }   // on a towed root
public enum AccessCode : byte { None = 0, Trader = 1, Work = 2, Garrison = 3 }
public struct Transponder : IComponentData { public byte On; public AccessCode Code; }
public struct Suspicion : IComponentData { public float Value; public uint LastSeenTick; public byte Seen; }   // 0..1
public struct Wanted : IComponentData { public ulong FactionMask; }   // bit per faction index (≤ 64)
public enum WatcherKind : byte { Light = 0, Sensor = 1, Ship = 2, Crowd = 3 }
public struct Watcher : IComponentData
{
    public WatcherKind Kind; public byte Faction;
    public float3 Facing;                         // local direction (Ship and Crowd look all round)
    public float Range, CosHalfArc, Rate;         // suspicion per second at close range
    public ushort District;                       // a blacked-out district's lights and sensors are dark
}
public struct AccessZone : IComponentData { public float3 Center, HalfExtents; public AccessCode Needs; public ushort District; }
public struct NoiseEvent { public float3 Position; public float Radius; public Entity Source; }   // source = the towing ship
public struct StationCore : IComponentData { public ulong Station; public byte Tethers, Freed; public float MassT; }
public struct CoreTether : IComponentData { public Entity Core; public float3 Anchor; public ushort District; public float Hp; public byte Cut; }
public struct District : IComponentData { public ushort Id; public byte Powered; }   // lamps, watchers and zones read it
public enum HeistPhase : byte { Calm = 0, Alarm = 1, Lockdown = 2, Over = 3 }
public struct HeistState : IComponentData { public HeistPhase Phase; public uint AlarmTick; public byte CruiserArrived, Outcome; }
public struct AlarmStep : IBufferElementData { public float AtSeconds; public MechanismActionKind Kind; public ushort Group; public float Value; public byte Done; }
public struct CoreSocket : IComponentData { public float3 Position; public float Radius; public Entity Core; public byte Powered; }
public struct DenPowered : IComponentData { public byte On; }   // on den modules: guns fire and doors move only while On
```

Managed: `ExtractionService` (App, dev runtime): the places, the loop state (`JobState`: None, Accepted, OnSite, CoreTaken, Home, Installed), the board, codes the crew holds, the den's build menu and costs.

## 5. Systems, services and flow

| System | Group | Role | Reads → writes |
|---|---|---|---|
| `TowSystem` | `GameplayPrePhysicsGroup` | every machine | `TowLink`, poses, velocities, masses → `ImpulseRequest`s (Tow), snaps the link over `BreakForce` |
| `TowKnockSystem` | `GameplayPostPhysicsGroup` | every machine | collision events of `TowLoad` roots → `NoiseEvents` |
| `CoreTetherSystem` | `GameplayPostPhysicsGroup` | authority decides cuts; every machine holds the core | damage on tether posts, cut requests → `CoreTether.Cut`, `District.Powered`, `StationCore.Freed`, the core's motion |
| `WatchSystem` | `GameplayPostPhysicsGroup` | authority | watchers, ships, `Transponder`, `AccessZone`, `NoiseEvents`, line of sight → `Suspicion`, `Wanted` |
| `HeistDirector` | `GameplayPostPhysicsGroup` | authority | `StationCore.Freed`, `AlarmStep`s, clock → `HeistState`, `MechanismActions` (via a request list) |
| `CoreSocketSystem` | `GameplayPostPhysicsGroup` | authority | a free core inside a socket's radius → installs it (kinematic, at the socket), `DenPowered` |
| AI (`AiThinkJob`, amended) | existing | — | `Wanted` → hostile |

Flow of a job: board (accept) → gate (portal) → on site (transponder, watchers) → cut tethers (blackout, alarm) → tow out (knocks, cutters) → gate home (cargo rule) → socket (power).

## 6. Public API

- `TowApi.Attach(EntityManager, Entity ship, Entity load, float length)`, `TowApi.Release(EntityManager, Entity ship)`: authority; attach needs the load within `Length + 10` m and the ship to have a salvage magnet (or the dev override).
- `TowApi.CanJump(EntityManager, Entity ship, out float loadT, out float capacityT)`.
- `ExtractionService.Accept(jobId)`, `Board()`, `Build(module, at)`, `Dig(at, radius)`.
- Console (F1): `tow attach|release|status`, `transponder off|on [code]`, `heist status|alarm|reset`, `extraction goto=den|lantern[@…]`, `den dig|build|power`, `job accept=<id>|status`.

## 7. Edge cases and failure behaviour

| Situation | Behaviour |
|---|---|
| The load or the ship is destroyed | The link is released; a `TowLoad` without a live tower is removed |
| Cable force over `BreakForce` | The cable snaps (released) and makes a knock where it broke |
| Origin rebase | Anchors are body-local; nothing to rebase |
| A tether's district is already dark | Cutting it changes nothing else; a district stays dark |
| Two crews tow the same load | The second `Attach` fails |
| Gate with a load too heavy | Refused, with a toast naming the load and the capacity |
| The core is shot | Nothing happens to it (`DamageImmune`); its tether posts are what cutting breaks |
| A watcher in a blacked-out district | Sees nothing (except `Ship` watchers, which carry their own lights) |
| Socket while not powered | The core installs and powers the den; one socket takes one core |

## 8. Performance budget

Advisory (ADR-0042). Tow and knocks: O(links); watch: O(watchers × nearby ships) with a spatial query, 0.2 ms at 60 watchers and 8 ships. No benchmarks are part of these tasks.

## 9. Tasks

### EXT-001 — The spec, the ADR and the decisions
- **Milestone:** M4
- **Tier:** frontier — sets the direction for a new game loop
- **Depends on:** `—`
- **Do:** Write this spec and ADR-0124; record the owner's decisions verbatim; ledger rows.
- **Files:** `docs/specs/SPEC-93-extraction.md`, `docs/decisions/ADR-0124-extraction-loop.md`, `docs/design/places/README.md`, `docs/LEDGER.md`.
- **Tests:** none (docs)
- **Acceptance criteria:**
  - [ ] `tools/check-docs.py` passes.

### EXT-002 — The magnet tow
- **Milestone:** M4
- **Tier:** frontier — new physics coupling between bodies
- **Depends on:** `EXT-001`
- **Do:** §4 tow types; `TowSystem`, `TowKnockSystem`, `TowApi`; the cable drawn as a line; console `tow`.
- **Files:** `Assets/Game/Simulation/Extraction/*`, `Assets/Game/Presentation/Extraction/*`, `Assets/Game/App/DevRuntime/Commands/*`.
- **Tests:** `Assets/Game/Tests/EditMode/Simulation/Extraction/TowTests.cs`
- **Acceptance criteria:**
  - [ ] A slack cable applies nothing; a taut one pulls both bodies by their masses (EditMode).
  - [ ] Over `BreakForce` the cable snaps; a destroyed load releases the link.
  - [ ] A load hitting a wall faster than `KnockSpeed` makes a `NoiseEvent`.

### EXT-003 — The cargo rule and stowing
- **Milestone:** M4
- **Tier:** standard — a check at an existing decision point
- **Depends on:** `EXT-002`
- **Do:** Refuse portal transit for a ship whose load outweighs its cargo capacity; `Stow`/`Unstow` a load into a hold that can take it.
- **Files:** `Assets/Game/Simulation/Mechanisms/PortalDecideSystem.cs`, `Assets/Game/Simulation/Extraction/*`.
- **Tests:** `Assets/Game/Tests/EditMode/Simulation/Extraction/CargoRuleTests.cs`
- **Acceptance criteria:**
  - [ ] A ship towing a 600 t load without cargo blocks is refused at a gate; with enough capacity it passes, load and all.

### EXT-004 — The station core, tethers and districts
- **Milestone:** M4
- **Tier:** frontier — authority decisions with world-wide consequences
- **Depends on:** `EXT-002`
- **Do:** §4 core types; `CoreTetherSystem`; district power; the cut key near a tether post; console `heist cut`.
- **Files:** `Assets/Game/Simulation/Extraction/*`.
- **Tests:** `Assets/Game/Tests/EditMode/Simulation/Extraction/CoreTetherTests.cs`
- **Acceptance criteria:**
  - [ ] Cutting a tether darkens its district; cutting the last frees the core and starts the alarm.

### EXT-005 — Transponder, watchers, suspicion and identification
- **Milestone:** M4
- **Tier:** frontier — AI targeting change
- **Depends on:** `EXT-004`
- **Do:** `Transponder`, `Watcher`, `AccessZone`, `Suspicion`, `Wanted`; `WatchSystem`; AI hostility for `Wanted`; the toggle key and HUD line.
- **Files:** `Assets/Game/Simulation/Extraction/*`, `Assets/Game/Simulation/Ai/AiThinkJob.cs`, `Assets/Game/Presentation/*`.
- **Tests:** `Assets/Game/Tests/EditMode/Simulation/Extraction/WatchTests.cs`
- **Acceptance criteria:**
  - [ ] A dark ship in a light's cone becomes Wanted; a ship with the right code in its zone does not.
  - [ ] A knock heard by a watcher raises the tower's suspicion; garrison AI attacks a Wanted ship only.

### EXT-006 — The alarm director
- **Milestone:** M4
- **Tier:** frontier — the heist's state machine
- **Depends on:** `EXT-005`
- **Do:** `HeistState`, `AlarmStep`s, `HeistDirector`; outcomes (escaped, caught).
- **Files:** `Assets/Game/Simulation/Extraction/*`.
- **Tests:** `Assets/Game/Tests/EditMode/Simulation/Extraction/HeistDirectorTests.cs`
- **Acceptance criteria:**
  - [ ] Steps fire at their times after the alarm, once each.

### EXT-007 — Tollhouse Nine, the place
- **Milestone:** M4
- **Tier:** frontier — a large built place
- **Depends on:** `EXT-006`
- **Do:** A 3D layout from the sketch (Python), blueprints (Editor builder), the runtime place: districts, lamps, signs, traffic, watchers, zones, doors, the mass driver, the outfall flush, the depot, the core in its Cradle on six tethers, stand-ins for the fixer and the engineer.
- **Files:** `ArtSource/Places/lantern_export.py`, `Assets/Game/Editor/Content/LanternBuilder.cs`, `Assets/Game/App/DevRuntime/Extraction/*`.
- **Tests:** playground scenario `lantern` (tools/playtest)
- **Acceptance criteria:**
  - [ ] `extraction goto=lantern` puts the ship at the toll queue; the core can be cut free and towed out by at least four of the six jobs' routes.

### EXT-008 — The Rock, the socket and den building
- **Milestone:** M4
- **Tier:** frontier — in-world building
- **Depends on:** `EXT-004`
- **Do:** The rock and its cave as a station grid; the socket; `CoreSocketSystem`; dig and build from the ship; den modules powered by the core.
- **Files:** `Assets/Game/App/DevRuntime/Extraction/*`, `Assets/Game/Simulation/Extraction/*`.
- **Tests:** `Assets/Game/Tests/EditMode/Simulation/Extraction/CoreSocketTests.cs`, playground scenario `den`
- **Acceptance criteria:**
  - [ ] Digging removes rock and gives ore; a placed gun fires only after a core is socketed.

### EXT-009 — The board, the gates and the loop
- **Milestone:** M4
- **Tier:** frontier — ties the places together
- **Depends on:** `EXT-003`, `EXT-007`, `EXT-008`
- **Do:** The board at the den (F), jobs, the gate pair, the loop state, the outcome toasts.
- **Files:** `Assets/Game/App/DevRuntime/Extraction/*`.
- **Tests:** playground scenario `loop`
- **Acceptance criteria:**
  - [ ] A scripted run goes from the board to the socket.

### EXT-010 — Den defence
- **Milestone:** M4
- **Tier:** standard — a scenario on existing AI
- **Depends on:** `EXT-008`
- **Do:** An attack on the den after a core is installed: Alliance ships follow the core home.
- **Files:** `Assets/Game/App/DevRuntime/Extraction/*`.
- **Tests:** playground scenario `den-attack`
- **Acceptance criteria:**
  - [ ] `den attack` spawns a wave; powered den guns engage it.

### EXT-011 — Scenarios and playtests
- **Milestone:** M4
- **Tier:** standard — test scripts
- **Depends on:** `EXT-009`, `EXT-010`
- **Do:** Playground scenario scripts for each job and the loop; PlayMode smoke test.
- **Files:** `tools/playtest/*`, `Assets/Game/Tests/PlayMode/Extraction/*`.
- **Tests:** `Assets/Game/Tests/PlayMode/Extraction/ExtractionLoopTests.cs`
- **Acceptance criteria:**
  - [ ] The loop scenario passes headless.

### EXT-012 — Co-op replication of extraction state
- **Milestone:** M4
- **Tier:** frontier — net protocol
- **Depends on:** `EXT-011`
- **Do:** Replicate tow links, tether cuts, districts, suspicion shown to the guest, heist state and the den's power.
- **Files:** `Assets/Game/Net/*`.
- **Tests:** `tools/playtest/coop-parity.sh`
- **Acceptance criteria:**
  - [ ] `tools/playtest/coop-parity.sh` matches during a heist.

## 10. Open questions

| # | Question | Default in use | Decider |
|---|---|---|---|
| 1 | Which jobs must work first? | The Dimming, the Trojan, the Depot Fire and Pod Seven; the Scrap Run and the Inside Man through simple stand-ins | Owner |
| 2 | How does the den grow after the first core? | Cores give power (more cores, more powered modules); hauls give blocks and ore | Owner |
| 3 | Can a den-sized rock be dug in play? | Yes, at 2 m cells; measured in EXT-008 | Lead |
| 4 | Is the tow a salvage magnet block or any ship? | Any ship with a salvage magnet; the playground's ships get one | Owner |
| 5 | Co-op heists are planar until 3D flight works in co-op (ADR-0123) | Accepted | Owner |

## 11. References

- [docs/design/places/README.md](../design/places/README.md): the owner's decisions, the Tollhouse Nine sheets and the Rock den.
- [ADR-0124](../decisions/ADR-0124-extraction-loop.md): the pivot. [ADR-0119](../decisions/ADR-0119-world-mechanisms.md): movers, mechanisms, portals. [ADR-0117](../decisions/ADR-0117-coop-one-code-path.md): deciders and appliers. [ADR-0123](../decisions/ADR-0123-experimental-3d-flight.md): 3D flight.
