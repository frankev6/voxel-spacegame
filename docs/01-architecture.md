# 01 — Architecture

**Status: BINDING.** Every spec and every line of code follows this document. To change it, write an ADR in `docs/decisions/` and get owner approval first. If a spec appears to contradict this document, this document wins; log the conflict in `docs/LEDGER.md` → *Open questions*.

Read [00-vision.md](00-vision.md) first for what the game is. This document says how it is built.

---

## 1. The rules (memorize these)

1. **Cells are data, not entities.** A voxel is a 4-byte cell inside a 16³ brick. Plain hull never becomes an entity or a GameObject. A cell becomes its own object only at the moment it breaks free (as debris).
2. **One voxel model everywhere.** The ship editor, the runtime and save files all use the same `VoxelStructure` layout. There is no conversion step that can lose information.
3. **One deterministic tick.** Gameplay simulation runs in `FixedStepSimulationSystemGroup` at exactly 60 Hz. Stages communicate through event streams produced and consumed in the same tick. Inside the fixed step there is exactly one structural-change sync point per tick (`GameplayEndCommandBufferSystem`); every fixed-step system that creates/destroys entities or adds/removes components records into its command buffer. Outside the fixed step, only the systems marked *structural* in §5 touch `EntityManager` directly.
4. **One writer per piece of state.** The ownership table in §8 names the only system or service allowed to write each piece of state. Everyone else reads or sends a request/event.
5. **Stable identity.** Content uses stable string IDs (`block_hull`). The galaxy is a seed plus saved deltas. No `UnityEngine.Random`, no `string.GetHashCode()`, no `System.Random` in anything that affects simulation or saves.
6. **Presentation is swappable.** All visuals and sounds are referenced through content definitions and theme tokens, never hard-coded. Every current art asset is a placeholder (see [design/DESIGN-SYSTEM.md](design/DESIGN-SYSTEM.md) and ADR-0009).
7. **Downward dependencies only.** Assemblies may only reference the assemblies listed for them in §3. Upward communication uses events and interfaces defined in `Game.Core`.

---

## 2. Technical shape of the game

| Topic | Decision | Source |
|---|---|---|
| Engine | Unity 6000.6.5f1, URP 17.6, Entities / Unity Physics / Entities Graphics 6.6 | ADR-0001 |
| Units | 1 Unity unit = 1 metre | — |
| Cell size | Per grid (`VoxelGrid.CellSize`). Default **0.5 m** for ships, stations and asteroids | ADR-0002 |
| Flight plane | Ships fly on the local **XZ plane** and rotate around **Y** only. Voxel structures are fully 3D. The plane lock is one component (`PlanarConstraint`) so it can be removed later | ADR-0003 |
| Gameplay physics | **Unity Physics (DOTS) only.** PhysX exists only because Unity Physics bakes from PhysX authoring components; PhysX never simulates (`Physics.simulationMode = Script`) | ADR-0004 |
| Fixed tick | 60 Hz (`FixedStepSimulationSystemGroup.Timestep = 1/60`), also `Time.fixedDeltaTime = 1/60` | — |
| UI | UI Toolkit only (no uGUI, no TextMeshPro). MVVM with Unity runtime data binding | ADR-0005 |
| Narrative | Typed `FactStore` + condition/effect vocabulary; dialog in Yarn Spinner 3 after a compatibility spike | ADR-0006 |
| Saves | Chunked binary container; binary voxel chunks; JSON (Newtonsoft) only inside non-voxel chunks | ADR-0007 |
| Platform | macOS (Apple Silicon) and Windows desktop, keyboard + mouse first; gamepad later | — |
| Determinism | Reproducible on one machine (same inputs + seed → same result). Not lockstep across machines | — |
| Out of scope for v1 | Multiplayer/netcode, modding, localization (re-added later with Yarn line IDs), planetary landing, ship interiors / first person, consoles | 00-vision |

---

## 3. Modules and assemblies

All game code lives under `Assets/Game/`. Each folder below is one assembly definition (`.asmdef`) with `autoReferenced: false` and `rootNamespace` equal to the assembly name. **A module may reference only the modules listed in its row.** The compile will fail if you add an undeclared reference — and you must not "fix" that by adding a reference; follow §3.2 instead.

| Assembly | Folder | May reference (game) | Purpose |
|---|---|---|---|
| `Game.Core` | `Core/` | — | IDs, stable hashing, deterministic RNG streams, `EventBus<T>`, `Services`, `ISaveParticipant` + `SaveWriter`/`SaveReader`, `InputContext`, pause tokens interface, logging, math helpers |
| `Game.Data` | `Data/` | Core | Content definitions (ScriptableObjects), `ContentDatabase`, block definitions and their blob, the 24-orientation tables, content validation hooks |
| `Game.Input` | `Input/` | Core | `Controls.inputactions`, the generated `Controls` class, `InputService` (contexts → action maps) |
| `Game.Persistence` | `Persistence/` | Core, Data | `SaveService`, the binary container, chunk registry, migrations, slots and profiles |
| `Game.Economy` | `Economy/` | Core, Data | Items, `InventoryModel`, `Wallet`, loot tables, merchants, pricing |
| `Game.Voxel` | `Voxel/` | Core, Data | `VoxelStructure`, grid/brick ECS data, spawning grids, brick meshing, brick colliders, mass properties, **damage resolution, integrity, splitting, debris** (the only code that writes cells) |
| `Game.Simulation` | `Simulation/` | Core, Data, Voxel | Ship commands, flight/propulsion, power, shields, weapons, projectiles, beams, rams, abilities, AI, spatial hash, factions/allegiance |
| `Game.Galaxy` | `Galaxy/` | Core, Data, Voxel, Simulation | Galaxy generation, coordinate frames, `OriginRebaseSystem`, star-system streaming, bodies on rails, interaction query, travel, encounter director, territory |
| `Game.Narrative` | `Narrative/` | Core, Data | `FactStore`, conditions/effects, story beats, quests, clues/mystery graph, dialog runner adapter |
| `Game.ShipEditor` | `ShipEditor/` | Core, Data, Voxel, Economy | `EditSession`, tools, undo/redo, bill of materials, validation, editor picking and overlays |
| `Game.UI` | `UI/` | Core, Data, Input | `UIRoot`, `UINavigator`, screens, views, view models, theme (USS tokens), shared UI services (tooltip, notifications, dialogs) |
| `Game.Presentation` | `Presentation/` | Core, Data, Input, Voxel, Simulation, Galaxy, Narrative, Economy, UI | The bridges between simulation and the player: input sampling → ECS, camera rig, VFX, audio, HUD/view-model sync |
| `Game.App` | `App/` | everything above | Composition root: Boot scene, `GameRoot`, service construction order, `GameSession`, `SceneFlow`, save participants for session-level state |
| `Game.Editor` | `Editor/` | everything (Editor platform only) | Project setup, importers, content validators, legacy converters, inspectors |
| `Game.DevTools.Editor` | `DevTools/Editor/` | everything (Editor platform only) | CLI playtest harness (`gh_*` commands), debug windows |
| `Game.Tests.Common` | `Tests/Common/` | everything (test-only: `defineConstraints: ["UNITY_INCLUDE_TESTS"]`) | Shared test kit (SPEC-71): `EcsTestWorld`, `GameTestBase`, `PerfRun`, fixtures, replay |
| `Game.Tests.EditMode` | `Tests/EditMode/` | everything (including `Game.Tests.Common`) | EditMode unit and integration tests |
| `Game.Tests.PlayMode` | `Tests/PlayMode/` | everything (including `Game.Tests.Common`) | PlayMode smoke, scenario and performance tests |

```mermaid
flowchart TB
    App[Game.App] --> Pres[Game.Presentation]
    App --> Pers[Game.Persistence]
    App --> ShipEd[Game.ShipEditor]
    Pres --> UI[Game.UI]
    Pres --> Gal[Game.Galaxy]
    Pres --> Narr[Game.Narrative]
    Pres --> Eco[Game.Economy]
    Pres --> Inp[Game.Input]
    Gal --> Sim[Game.Simulation]
    Sim --> Vox[Game.Voxel]
    ShipEd --> Vox
    ShipEd --> Eco
    UI --> Inp
    UI --> Data[Game.Data]
    Vox --> Data
    Narr --> Data
    Eco --> Data
    Pers --> Data
    Data --> Core[Game.Core]
    Inp --> Core
```

### 3.1 Third-party and Unity packages per module

| Package | Allowed in |
|---|---|
| Entities, Collections, Burst, Mathematics | Core, Data, Voxel, Simulation, Galaxy, ShipEditor, Presentation, App, tests. `Game.Core` uses Entities only to declare the custom system groups, `GameplayActive`, `WorldSceneActive`, `SimClock` and `GameplayEndCommandBufferSystem` |
| Unity Physics | Voxel, Simulation, Galaxy, Presentation, App, tests. `Game.Core` references it only for `[UpdateBefore/After(typeof(PhysicsSystemGroup))]` on its group declarations |
| Entities Graphics | Voxel, Presentation |
| Input System | Input, Presentation, App, DevTools, tests |
| UI Toolkit (`UnityEngine.UIElements`) | UI, Presentation, App, Editor, DevTools |
| Newtonsoft JSON | Persistence, Data (importers), Narrative (importers), Editor |
| VFX Graph | Presentation |
| Unity Pipeline (`com.unity.pipeline`) | DevTools.Editor only |

Adding any package to `Packages/manifest.json` requires an ADR.

### 3.2 Content definitions and the logic vocabulary live in `Game.Data`

Two kinds of types are needed by many modules, so they sit low in the graph even though a higher-level spec designs them:

- **Every content definition class** (`*Definition : ContentDefinition : ScriptableObject`) lives in `Assets/Game/Data/Definitions/<Area>/` (namespace `Game.Data.Definitions`), so the single `ContentDatabase` can reference all of them. The spec of the area that uses a definition designs its fields and owns the task that creates it (for example SPEC-21 designs `WeaponDefinition`), but the file goes in `Game.Data`. Definitions hold data only — no behaviour that needs higher modules.
- **Shared combat enums** (`DamageType`, designed by SPEC-13; `StatId`, designed by SPEC-22; `ProducerKind`, designed by SPEC-02) live in `Assets/Game/Data/Combat/` (namespace `Game.Data.Combat`), so content definitions and every runtime module use the one enum instead of mirrors. The designing spec owns the values and the task that creates the file.
- **Simulation event channels and guarded access** (`SimEventChannel<T>`, `GuardedHandles`, designed by SPEC-02 §4.12) live in `Assets/Game/Data/Streams/` (namespace `Game.Data.Streams`): they need Entities and Collections types that `Game.Core` may not declare, and every simulation module references `Game.Data`. Every deterministic simulation stream is a `SimEventChannel<T>`; every singleton that owns native memory shared across systems embeds a `GuardedHandles`. No spec writes its own stream registry or handle bookkeeping.
- **The condition/effect logic vocabulary** (`ICondition`, `IEffect`, the compiled node types and the text compiler) lives in `Assets/Game/Data/Logic/` (namespace `Game.Data.Logic`). It is designed by SPEC-40 (Narrative core) and used by narrative, quests, encounters and spawn rules. Conditions and effects talk to the game only through the `ILogicContext` interface (also in `Game.Data.Logic`), which `Game.App` implements by routing to `FactStore`, inventory, galaxy and simulation services.

### 3.3 How a lower module talks to a higher one

- **Events:** the lower module raises a typed event (`EventBus<T>` for managed code, an ECS event stream for Burst code). The higher module subscribes.
- **Interfaces:** the lower module declares an interface in `Game.Core` (or in itself); `Game.App` registers the implementation in `Services`.
- **Singletons:** ECS systems read configuration and session data through singleton components, written once by `Game.App` or the owning module. ECS never reads managed statics.

---

## 4. Runtime topology

### 4.1 Scenes

| Scene | Build index | Contains |
|---|---|---|
| `Boot` | 0 | The `GameRoot` prefab only |
| `MainMenu` | 1 | Menu backdrop only. No managers |
| `World` | 2 | Lighting/volume only. Everything else is spawned at runtime from data |
| `BlueprintLab` | 3 | Empty build grid environment for the ship editor (no economy) |

- Managers never live in content scenes. `Bootstrap` (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`) guarantees `GameRoot` exists whichever scene you press Play in.
- **Enter Play Mode Options are on with domain and scene reload disabled** (ADR-0020). Every static field must be reset in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method. Prefer no mutable statics at all.
- `SceneFlow` (in `Game.App`) is the only code that loads or unloads scenes.

### 4.2 Services (managed side)

`GameRoot.Awake` constructs services **in this exact order** and registers them in the typed `Services` locator (`Game.Core`):

1. `SettingsService` (player prefs: audio, graphics, controls)
2. `ContentService` (wraps `ContentDatabase`, builds the block blob)
3. `InputService`
4. `AudioService`
5. `PauseService` (ref-counted pause tokens)
6. `SaveService`
7. `SceneFlow`
8. `GameSession` (holds runtime state; restored in place on load)
9. `UIRoot` + `UINavigator`

Shutdown runs in reverse order. No service may create another service. No `FindObjectOfType`, no `Resources.Load` (except `GameRoot` itself), no self-creating singletons.

### 4.3 ECS world

- One default `World`. Systems are created automatically but **every gameplay system declares `state.RequireForUpdate<GameplayActive>()`** (or a more specific singleton) so nothing runs in the menu.
- `SceneFlow` creates the `GameplayActive` singleton entity when the World scene is ready and destroys it on exit.
- `GameplayActive` and `SimClock` exist in the World and the Blueprint Lab; the tag `WorldSceneActive` (Core, SPEC-01 §4.1) exists only in the World. Systems that need a loaded galaxy (streaming, encounters, travel, territory, origin rebasing) also `RequireForUpdate<WorldSceneActive>` (SPEC-01 decision 6).
- Entities that belong to a loaded star system carry the **shared** component `StarSystemMember { StarSystemId }` (`ISharedComponentData`, low cardinality: at most 2 star systems are loaded). This includes grid roots, their bricks and functional entities. Unloading a star system is one `EntityManager.DestroyEntity(query)` with a shared-component filter: a chunk-level batch destroy. The player ship and its children carry **no** `StarSystemMember`, because they travel between star systems.
- No subscenes for gameplay content in v1. Configuration singletons are created by `Game.App` at boot from the `ContentDatabase` (`GameConfig` singleton, block blob singleton).

---

## 5. Frame pipeline (system groups and ordering)

This is the complete group tree. Every new system must be placed in exactly one of these groups with explicit `[UpdateInGroup]` and, where order matters, `[UpdateBefore]`/`[UpdateAfter]`. The spec that owns a system says where it goes; the spec may not invent a new group without an ADR.

```
InitializationSystemGroup
├─ PersistentIdRefillSystem              FND  managed; UpdateBefore GameInitGroup; tops up the PersistentIdAllocator block (also between scenes)
└─ GameInitGroup                         (custom; OrderLast)
   ├─ OriginRebaseSystem                 GAL  single floating-origin owner
   ├─ PlayerInputSampleSystem            INP  managed; writes PlayerCommand singleton
   ├─ GridSpawnSystem                    VOX  structural; GridSpawnRequest → grid root + bricks + functional entities (batch APIs)
   └─ GridSpawnBodyInitSystem            PHY  fresh spawn bodies through end-init ECB, including while paused (ADR-0037)

SimulationSystemGroup
├─ FixedStepSimulationSystemGroup        60 Hz, runs 0..N times per frame
│  ├─ GameplayPrePhysicsGroup            (custom; UpdateBefore PhysicsSystemGroup)
│  │  ├─ FactionStandingSystem           ENC  provocation + relation requests → FactionRelations (before ShipCommandGroup)
│  │  ├─ ShipCommandGroup                (custom)
│  │  │  ├─ PlayerCommandApplySystem     FLT  PlayerCommand → player ship's ShipCommand
│  │  │  └─ AI decision + steering       AI   → ShipCommand (staggered)
│  │  ├─ BodyOnRailsSystem               SYS  analytic orbits from UniversalTime (tick-exact moving colliders)
│  │  ├─ PowerDemandGroup                PWR  (custom, ADR-0014) each consuming feature writes its PowerRequest.Requesting
│  │  ├─ StatusSystem                    PWR  inhibit + stat-modifier channels → Inhibited, ShipStatModifier (ADR-0016)
│  │  ├─ PowerAllocationSystem           PWR  grants by priority; sets Powered enable bits
│  │  ├─ ShipStatsAggregateSystem        PWR  shared ShipStats, ShipStatMultipliers, PowerStats/ShieldStats for dirty ships
│  │  ├─ feature stats systems           FLT/WPN PropulsionStats, WeaponStats (ADR-0014)
│  │  ├─ PropulsionSystem                FLT  thruster impulses; never overwrites velocity
│  │  ├─ PlanarConstraintSystem          FLT  keeps Y = 0 and rotation about Y only
│  │  ├─ WeaponAimSystem                 WPN
│  │  ├─ WeaponFireSystem                WPN  spawns projectiles, toggles beams
│  │  ├─ AbilityActivationGroup          ABL
│  │  └─ ColliderSwapSystem              PHY  installs rebuilt brick colliders; queues old blobs
│  ├─ PhysicsSystemGroup                 (Unity Physics)
│  └─ GameplayPostPhysicsGroup           (custom; UpdateAfter PhysicsSystemGroup)
│     ├─ SpatialHashBuildSystem          AI   SpatialProxy hash for this tick (OrderFirst)
│     ├─ ImpactEventSystem               DMG  collision events → ram DamageEvents
│     ├─ ProjectileStepSystem            WPN  casts → DamageEvents, FxEvents
│     ├─ BeamStepSystem                  WPN
│     ├─ ExplosionSystem                 WPN  ExplosionRequest → ShieldHits / DamageEvents per overlapped grid
│     ├─ ShieldSystem                    PWR  regen + ShieldHit → ShieldState HP; overflow → DamageEvents
│     ├─ DamageResolveSystem             DMG  parallel over grids, each grid's events by one worker; the ONLY cell-HP writer
│     ├─ IntegritySystem                 DMG  islands after removals
│     ├─ SplitSystem                     DMG  debris particles / chunk bodies / hulk grids (records into the GameplayEnd ECB)
│     ├─ ShipDeathSystem                 DMG  core lost → Hulk state
│     ├─ GridMassUpdateSystem            PHY  incremental mass, centre of mass, inertia
│     ├─ BrickDirtyScheduleSystem        VOX  marks bricks for remesh / collider rebuild
│     ├─ GameplayEndCommandBufferSystem  CORE THE ONLY structural sync point in the fixed step
│     └─ SimClockAdvanceSystem           FND  OrderLast, after the ECB system: Tick += 1, UniversalTime from Tick
├─ VoxelRebuildGroup                     (custom; after FixedStep; variable rate)
│  ├─ BrickMeshBuildSystem               REN  budgeted Burst jobs → MeshDataArray
│  └─ BrickColliderBuildSystem           PHY  budgeted Burst jobs → blobs, swapped next tick
├─ WorldGroup                            (custom; variable rate)
│  ├─ GalaxyServicesBridgeSystem         GAL  OrderFirst; main thread; runs the GalaxyFrameTicker services in TickOrder:
│  │                                          StarSystemLoader.Tick (100, SYS, structural; the "StarSystemStreamingSystem"),
│  │                                          TravelService.Tick (200, TRV), EncounterDirectorService.Tick (300, ENC; the
│  │                                          "EncounterDirectorSystem"), TerritoryService.Tick (310, ENC)
│  ├─ InteractionQuerySystem             SYS  nearest interactable → singleton
│  ├─ SalvageMarkSystem                  ECO  structural; Interactable(Salvage) on new hulks (one batch)
│  └─ GridLodSystem                      REN  LOD + dormancy
└─ TransformSystemGroup                  (Unity)

PresentationSystemGroup
├─ PresentationBridgeGroup               (custom)
│  ├─ BrickMeshApplySystem               REN  main thread; swaps MaterialMeshInfo
│  ├─ FxUploadSystem                     VFX  FxEvents → VFX Graph event buffers
│  ├─ AudioEventDrainSystem              AUD  SfxEvents → voice-capped playback
│  ├─ CameraTargetSyncSystem             CAM
│  ├─ HudSyncSystem                      HUD  ECS → view models, only on change
│  └─ ManagedEventDrainSystem            CORE ECS NativeQueue events → EventBus<T>
├─ (Entities Graphics)
└─ DeferredDisposalSystem                VOX  OrderLast; frees meshes/blobs N frames later
```

Codes on the right are spec area codes (§10). The tree lists the groups and the systems that order work *between* modules; each spec's placement table (its §5) places its remaining systems inside these groups with explicit attributes.

### 5.1 Timing rules

- Anything that changes simulation state runs in `FixedStepSimulationSystemGroup`. Variable-rate groups may only build caches (meshes, colliders, LOD), stream, or present.
- Input is sampled once per frame into `PlayerCommand`. Edge-triggered actions (fire once, toggle) are **latched** until a fixed tick consumes them, so a 144 FPS frame and a 30 FPS frame produce the same gameplay.
- Cooldowns and rates use accumulators in fixed ticks (`Tick` counter), never `Time.deltaTime`.
- Presentation interpolates from the physics state using `PhysicsGraphicalSmoothing`; gameplay logic always reads the physics transform (`LocalTransform` inside the fixed step), never the smoothed `LocalToWorld`.
- **Catch-up cap.** `Time.maximumDeltaTime = 0.1`, so at most 6 fixed ticks run in one frame. Below 10 FPS the game slows down instead of spiralling. (`ProjectSetup` enforces it.)
- **Jobs that outlive their frame never touch ECS memory.** Mesh, collider and other cache-building jobs that may run across frames read only **snapshots**: native buffers the scheduling system copied the inputs into (for example the brick's 16³ cells plus a 1-cell border from neighbours, 18³). This prevents both sync points and the safety errors that arise when the fixed step writes those cells next frame. Results are installed by a later system after `JobHandle.IsCompleted` is true; nobody calls `Complete()` on them in the fixed step.
- **Deterministic event order.** Streams that change simulation state (`DamageStreams`, `ShieldHit`, `ExplosionRequest`, `CellRemoved`, collision-derived events) are written with `NativeStream`, registered through `SimEventChannel<T>` (§6.2). Each producer writes at a foreach index derived from the chunk index in its query, and consumers read indices in order, so content order does not depend on thread timing. Where several producers feed one consumer, the consumer sorts by a stable key `(Grid.Index, Source.Index, producer kind, sequence)` before applying. `NativeQueue` is allowed only for presentation streams (`FxEvent`, `SfxEvent`, managed events), whose order does not affect simulation. `EntityCommandBuffer.ParallelWriter` always uses `ChunkIndexInQuery` (or `unfilteredChunkIndex`) as its sort key.

---

## 6. Shared contracts

These types cross module boundaries. **Their names and owning module are binding.** The owning spec defines the remaining fields; consuming specs must use them as-is and request changes through the owning spec's *Open questions*.

### 6.1 Core (`Game.Core`, spec SPEC-02)

| Type | Shape (binding parts) | Notes |
|---|---|---|
| `ContentId` | `readonly struct { string Value; ContentHash Hash; }` — **managed** (holds a string) | Hash = `StableHash.Hash64(Value)` (xxHash3-64 over UTF-8). Lowercase `snake_case`. Used in definitions, managed code and saves. **Never inside an `IComponentData`, buffer, blob, job or ECS event** |
| `ContentHash` | `readonly struct : IEquatable<ContentHash> { ulong Value; }` — unmanaged | The only way ECS/Burst data refers to content that has no dense runtime index (blueprints, audio cues, factions in events). `ContentDatabase` resolves it; boot fails if two content IDs hash equal. Hot per-element lookups use dense runtime indices instead (`BlockIndex`) |
| `PersistentId` | `IComponentData { ulong Value; }` — unmanaged | Stable identity of a runtime-spawned object across save/load (every grid root, fleet, encounter). Allocated monotonically by `GameSession` (SPEC-01), saved with the session, never reused. `Entity` is never saved or used as a cross-session reference |
| `StableHash` | static: `Hash64(string)`, `Hash64(ReadOnlySpan<byte>)`, `Combine(ulong, ulong)` | Never use `GetHashCode` for anything persisted or simulated |
| `Tick` | `uint` fixed-step counter in `SimClock` singleton | Starts at 0 per session |
| `UniversalTime` | `double` seconds since campaign start, in `SimClock` | Saved |
| `RngStreams` | `static Unity.Mathematics.Random For(ulong seed, RngPurpose purpose, ulong salt)` | All randomness. `RngPurpose` is an enum; add values, never reorder |
| `EventBus<T>` | `Subscribe`, `Unsubscribe`, `Publish` (deferred), `Drain()` once per frame | Per-handler try/catch; re-entrancy safe |
| `Services` | `Register<T>`, `Get<T>`, `TryGet<T>`, `Reset()` | Reset on `SubsystemRegistration` |
| `GameplayActive`, `WorldSceneActive`, `SimClock`, custom groups, `GameplayEndCommandBufferSystem` | ECS declarations | The only Entities types in Core (with `PersistentId` and `PersistentIdAllocator`) |
| `IPersistentIdAllocator` / `PersistentIdAllocator` | managed `ulong Allocate()` / singleton `{ ulong Next, End; }` | `GameSession` implements the interface and refills the singleton block that Burst systems draw from |
| `ISaveParticipant` | `string Key {get;}` (4-char), `int Version {get;}`, `void Capture(SaveWriter)`, `void Restore(SaveReader, int fromVersion)` | Implemented by owning modules |
| `InputContext` | `enum { None, Flight, Editor, Menu, Dialog, TextEntry }` | Plus the always-on Global map |
| `IPauseService` | `PauseToken Acquire(string reason)`; `bool IsPaused` | Ref-counted |

### 6.2 Data (`Game.Data`, spec SPEC-04)

| Type | Shape | Notes |
|---|---|---|
| `BlockIndex` | `ushort`; 0 = air | Runtime index, rebuilt every boot from `ContentDatabase` order. **Never saved** — saves use `ContentId` palettes |
| `Orientation` | `byte` 0..23 | The 24 proper rotations of a cube. `OrientationTables` blob: `Rotate(int3, o)`, `Compose(a,b)`, `Mirror(o, axis)`, `Inverse(o)` |
| `BlockKind` | `enum : byte { Structural, Prop, Functional }` | Structural = brick-meshed; Prop = instanced mesh; Functional = has an entity (iff it has at least one module) |
| `BlockDef` | blob struct: `Index`, `MirrorIndex`, `Kind`, `Cells` (footprint), `Shapes` (resolved per-cell, ADR-0028), `Clearance`, `AllowedOrientations` (24-bit mask), `AttachFaces`, `CellHp`, `CellMass`, `MaterialLayer`, `PropMeshIndex`, `ModuleMask`, `Category` | Built from `BlockDefinition` ScriptableObjects |
| `BlockModule` / `BlockModuleId` / `BlockModulesBlob` | serializable module classes on `BlockDefinition`; append-only id list; one blob of module rows (singleton `BlockModulesRef`) | ADR-0013. A block function is a module; systems read the rows of the modules they own and register functional components per module id |
| `BlockDefsBlob` | `BlobArray<BlockDef>` indexed by `BlockIndex` | ECS singleton `BlockDefsRef` |
| `ContentDatabase` | ScriptableObject with direct references to every content asset | The only content entry point |
| `DamageType` | `enum : byte { Kinetic, Energy, Explosive, Ram }` in `Game.Data.Combat` | Designed by SPEC-13 (§3.2). Add values at the end only |
| `StatId` | `enum : byte` in `Game.Data.Combat` | Designed by SPEC-22 (§3.2). Add values at the end only |
| `ProducerKind` | `enum : byte` in `Game.Data.Combat` | Who produced a simulation event; part of every sort key. Designed by SPEC-02. Add values at the end only |
| `SimEventChannel<T>` | per-tick registry of `NativeStream`s: `BeginWrite(ref state, ProducerKind, n)`/`EndWrite`, single consumer `BeginRead`/`EndRead`, `BeginTick` | SPEC-02 §4.12. Open producer registration in call order; every deterministic simulation stream is one |
| `GuardedHandles` | `Acquire`/`Release` (exclusive), `BeginRead`/`EndRead` (shared), `CompleteAccess` | SPEC-02 §4.12. The only job-handle guard for shared native singletons |

### 6.3 Voxel (`Game.Voxel`, specs SPEC-10 … SPEC-13)

| Type | Shape (binding parts) | Notes |
|---|---|---|
| `Cell` | 4 bytes: `ushort Ref` (bit 15 = 0 → palette index; bit 15 = 1 → `BlockInstance` index; 0 = air), `byte Hp`, `byte Flags` | `Hp` is quantised 0..255 of the block's `CellHp` |
| `PaletteEntry` | `ushort Block` (BlockIndex), `byte Orient`, `byte Color` | Per grid |
| `VoxelStructure` | Managed class owning `NativeList`s: palette, instances, bricks (coord → 4096 cells) | Same layout as ECS; used by editor, saves, tests |
| `VoxelGrid` | `IComponentData`: `int3 BrickMin, BrickDims; float CellSize; uint Revision; int AnchorInstance; GridKind Kind` | Root entity of a ship, station, asteroid or hulk |
| `GridKind` | `enum : byte { Ship, Station, Asteroid, Hulk }` | |
| `Brick` | `IComponentData`: `Entity Grid; int3 Coord; ushort SolidCount` + buffers `BrickCells` (4096 × `Cell` when dense) and `BrickSparseCells` (0..1024 sorted six-byte entries when sparse), plus `BrickSolidMask` (512 B) + enableable `BrickDirty` | 16³ cells. Exactly one cell buffer owns storage; dense is selected on insertion of the 1025th solid cell (ADR-0023). All three buffers are `[InternalBufferCapacity(0)]` |
| `BlockInstance` | buffer element on the grid: `ushort Block; byte Orient, Color; int3 Origin; float FunctionHp; Entity Functional` | Multi-cell and functional blocks |
| `FunctionalBlock` | `IComponentData`: `Entity Grid; int InstanceIndex` | On thruster/weapon/reactor/shield/core entities |
| `GridSpawnRequest` | entity with `ContentHash Blueprint` **or** `int StructureHandle` (index into the managed `VoxelStructureRegistry`), pose, `GridKind`, `GridProfile`, `PersistentId` | Consumed by `GridSpawnSystem`. Unmanaged only. Requester components on it (allegiance, AI profile, star system, owner) stay on the root (ADR-0015) |
| `PhysicsLayers` | static class: the named `CollisionFilter`s for grids, debris, chunk bodies, bodies, sensors and query filters for projectiles, explosions and picking | Owned by SPEC-12. **The only place collision filters are defined** |
| `DamageEvent` | `Entity Grid; float3 LocalPoint, LocalDir; float Amount, Radius, Penetration; DamageType Type; Entity Source; float3 WorldImpulse` | Grid-space. Written to `DamageStreams` by producers |
| `CellRemoved` | `Entity Grid; int3 Cell; ushort Block; byte Color; float3 WorldPos, WorldVel, Kick` | Output of the resolver; consumed by split, VFX, audio, salvage |
| `ImpulseRequest`, `CellRestoreRequest` | request channels held by `DamageStreams` (SPEC-13, ADR-0016) | Any feature may post: pushes are applied by `ImpactEventSystem`, restores (heal, place) by `DamageResolveSystem` |
| `ExplosionRequest` | `float3 WorldPos; float Radius, Amount; DamageType Type; Entity Source` | Defined here so Voxel code (reactor chain reactions) can request explosions; consumed by `ExplosionSystem` in Simulation (SPEC-21), which tests shields first |
| `Hulk` | tag | Dead ship: still physical and damageable, no AI/power |

### 6.4 Simulation (`Game.Simulation`, specs SPEC-20 … SPEC-24)

| Type | Shape (binding parts) | Notes |
|---|---|---|
| `ShipCommand` | per ship: `float2 Thrust` (x = strafe, y = forward, −1..1), `float Turn` (−1..1), `float3 AimWorld`, `uint FireGroups` (bitmask), `byte Flags` (boost, brake…) | The single interface between "who controls the ship" (player or AI) and "how the ship moves/fires" |
| `PlayerCommand` | singleton: same fields as `ShipCommand` plus latched edge flags | Written only by `PlayerInputSampleSystem` |
| `PlayerShip` | tag on the player's current ship | |
| `Allegiance` | `byte Faction; byte Team` | `ContentId` → byte index via `FactionTable` |
| `OwnedBy` | `Entity Owner; ulong OwnerPersistentId` in `Game.Data.Components` (designed by SPEC-21) | On roots launched or spawned by another root. Damage, shield-hit and explosion events carry `Instigator` = owner (or the shooter); provocation and kill credit use it |
| `ShipStats` | shared part: mass, mass fraction, bounds, `FunctionalRevision`; multipliers in `ShipStatMultipliers` | Written only by `ShipStatsAggregateSystem`. Feature totals live in feature-owned stats components (`PropulsionStats`, `WeaponStats`, `PowerStats`, `ShieldStats`; ADR-0014) |
| `PowerRequest` | buffer on functional entities: `{BlockModuleId Module; byte Priority, Requesting; float Draw; short ConsumerIndex}` | One element per power-consuming module; written by the module's owning feature (ADR-0014) |
| `ShipPower` + buffer `PowerConsumer` + enableable `Powered` | | |
| `ShieldState` | analytic ellipsoid in grid space + HP | |
| `ShieldHit` | event: `Entity Grid; float3 LocalPoint; float Amount; DamageType Type; Entity Source` | Producers test shields first (analytically) and emit this instead of a `DamageEvent` |
| `SpatialHash` | singleton, rebuilt per tick | Used by AI, homing, explosions, radar |

### 6.5 Galaxy (`Game.Galaxy`, specs SPEC-30 … SPEC-33)

| Type | Shape | Notes |
|---|---|---|
| `StarSystemId` | `readonly struct { uint Value; }` | Use "star system" in prose and `StarSystem*` in code — "system" alone always means an ECS system |
| `BodyId` | `StarSystemId System; BodyRole Role; ushort Slot, SubSlot` | Structural, never derived from rolled values |
| `UniversePosition` | `double3` metres relative to the current star | On every persistent root entity |
| `FrameOrigin` | singleton: `double3 OriginInStarSystem` | Owned by `OriginRebaseSystem` |
| `OriginShifted` | event `{ float3 Delta }` | Camera, VFX and trails listen |
| `StarSystemMember` | `ISharedComponentData { StarSystemId Id; }` | Unload = batch destroy by shared filter. Not on the player ship |

### 6.6 Presentation event streams

| Stream | Producer → consumer | Notes |
|---|---|---|
| `FxEvent` | Simulation/Voxel → `FxUploadSystem` | `FxKind Kind; float3 Pos, Dir, Vel; float Scale; uint Color` |
| `SfxEvent` | Simulation/Voxel → `AudioEventDrainSystem` | `ContentHash Cue; float3 Pos; float Volume` |
| Managed events | via `ManagedEventDrainSystem` → `EventBus<T>` | `ShipDestroyed`, `StarSystemEntered`, `BodyInteracted`, `BodyDestroyed`, `LaneJumped`, `ItemAcquired`, `FactChanged`, `SessionReset`, `GameSaved`, `GameLoaded` |

### 6.7 Content and logic (`Game.Data`, specs SPEC-04 and SPEC-40)

| Type | Shape | Notes |
|---|---|---|
| `ContentDefinition` | abstract `ScriptableObject` with `[SerializeField] string id` → `ContentId Id` | Base of every definition. Designed by SPEC-04 |
| `*Definition` | e.g. `BlockDefinition`, `ItemDefinition`, `ShipBlueprintDefinition`, `WeaponDefinition`, `FactionDefinition`, `BodyTemplateDefinition`, `EncounterDefinition`, `AbilityDefinition`, `AudioCueDefinition`, `StoryBeatDefinition`, `QuestDefinition`, `ClueDefinition` | All in `Game.Data.Definitions`; fields designed by the owning area spec |
| `ICondition` / `IEffect` | `bool Evaluate(ILogicContext)`, `void Apply(ILogicContext)`, `void Validate(ValidationReport)` | Compiled from a text syntax at import. Unknown function = import error (fail closed) |
| `ILogicContext` | facts, inventory, location, time and world queries/commands | Implemented in `Game.App` |
| `ValidationReport` | collects errors/warnings with asset path + field | Used by every importer and validator |

---

## 7. Key flows

### 7.1 Spawn a ship

1. Game code asks `ShipSpawnService.Spawn(blueprintOrStructure, pose, allegiance)` (Game.App / Galaxy), which creates a `GridSpawnRequest` entity with a `GridProfile` and adds the requester values (`Allegiance`, `AiBrain.ProfileIndex`, `StarSystemMember`). Any system may do the same through an ECB (ADR-0015).
2. `GridSpawnSystem` (init group) creates the grid root with the profile's registered root components (`RootComponentRegistration`), one entity per non-empty brick (cells memcpy'd from the structure), and one entity per functional block with its modules' registered components. It marks every brick `BrickDirty = Mesh | Collider`. Owners initialise their zeroed components on their first update.
2b. Grids that must appear at an exact tick (launched craft) are spawned ahead, **stowed** (`GridStowApi.Stow`, disabled), and **unstowed** from the fixed step through the GameplayEnd ECB (SPEC-10 §5.13).
3. Bricks get meshes and colliders over the next frames from `VoxelRebuildGroup` (budgeted). The root gets a provisional box collider until the first full compound is ready.

### 7.2 One hit

`ProjectileStepSystem` casts → finds a grid → converts to grid space → `DamageEvent` → `DamageResolveSystem` (DDA or blast rasterization, writes `Cell.Hp`, removes cells, emits `CellRemoved`, marks bricks dirty) → `IntegritySystem` → `SplitSystem` → `GameplayEndCommandBufferSystem` → `VoxelRebuildGroup` rebuilds 1–8 bricks → `ColliderSwapSystem` installs the new collider next tick → `FxUploadSystem` sprays debris.

### 7.3 Save and load

`SaveService.Save(slot)`: `ISaveContext.PrepareCapture()` runs ordered writebacks (ADR-0034), then each `ISaveParticipant` captures a snapshot on the main thread → a worker serializes and compresses → atomic replace. `Load(slot)`: `SceneFlow` unloads gameplay, `GameSession` restores **in place** and raises `SessionReset` once, the galaxy regenerates from the seed and applies deltas, then the player ship spawns at the saved `UniversePosition`. Loading never executes narrative effects.

### 7.4 Jump to another star system

Interact at a lane gate → `TravelService.RequestJump` (SPEC-32) → Spooling (controls locked through `PlayerControlLock`, cover fades in) → commit: the arrival point is solved analytically, fuel is taken, a pause token stops the fixed step → `StarSystemLoader` unloads the current star system (`StarSystemMember` query) and builds the target from `(GalaxySeed, StarSystemId)` plus its `StarSystemDelta` → `OriginRebaseSystem` re-centres and places the player ship at the arrival point (one `OriginRebaseRequest` with placement) → the pause is released when the arrival area is ready → Arriving (brake, cover fades out) → `StarSystemEntered` and `LaneJumped` events.

### 7.5 Edit a ship

`EditSession` copies the ship's `VoxelStructure` to a draft → tools produce `DiffCommand`s with undo/redo → commit computes the bill of materials against the original, checks the inventory, applies it, swaps the structure, marks all bricks dirty. Cancel discards the draft. No inventory change ever happens before commit.

---

## 8. State ownership (single writer)

| State | Only writer | Everyone else |
|---|---|---|
| Cell contents and `Cell.Hp` | `DamageResolveSystem` at runtime (damage events and the `CellRestoreRequest` channel, ADR-0016); `EditSession.Commit` in the editor | Read only; other features post `CellRestoreRequest`s |
| `BlockInstance.FunctionHp` | `DamageResolveSystem` | Read |
| `VoxelGrid.Revision`, `BrickDirty` | `DamageResolveSystem`, `SplitSystem`, `GridSpawnSystem` | Read |
| Brick render mesh (`MaterialMeshInfo`) | `BrickMeshApplySystem` | — |
| Grid `PhysicsCollider` | `ColliderSwapSystem` | — |
| `PhysicsMass` | `GridMassUpdateSystem` | — |
| `VoxelGrid.Kind` | `GridSpawnSystem` (initial), `ShipDeathSystem` (Ship → Hulk) | Read |
| `PhysicsVelocity` | Unity Physics + `PropulsionSystem` (impulses only) + `ImpactEventSystem` (impulses only: collisions, damage knockback and the `ImpulseRequest` channel, ADR-0016; **every new mechanic that pushes bodies posts to that channel**) + `PlanarConstraintSystem` (vertical impulses only) + `GridMassUpdateSystem` (exact re-expression about the new centre of mass only) + `OriginRebaseSystem` (placement re-expression only: rotates the player's linear velocity by the placement yaw change). **Nobody else assigns velocity.** Creating `PhysicsCollider`/`PhysicsMass`/`PhysicsVelocity` on a new body (`GridSpawnBodyInitSystem`, `GridBodyInitSystem`, `SplitSystem` through `GridBodyFactory`), and re-enabling a stowed grid with its pose and velocities (`GridStowApi.Unstow`, ADR-0015), is initialisation, not a write | Read |
| `ShipCommand` (player ship) | `PlayerCommandApplySystem` | Read |
| `ShipCommand` (AI ship) | `AiSteeringSystem` | Read |
| `PlayerControlLock` | `TravelService` | Read by `PlayerCommandApplySystem` |
| `FactionRelations` (after boot), faction standings | `FactionStandingSystem` (requests from `FactionService`) | Read |
| `PlayerCombatStatus` | `PlayerCombatStatusSystem` | Read |
| `ShipStats`, `ShipStatMultipliers`, `PowerStats`, `ShieldStats` | `ShipStatsAggregateSystem` | Read |
| `ShipStatModifier`, `Inhibited` | `StatusSystem` (SPEC-22) from the `StatModifierRequest` / `InhibitRequest` channels (ADR-0016) | Post requests |
| `PropulsionStats` / `WeaponStats` | `PropulsionStatsSystem` (SPEC-20) / `WeaponStatsSystem` (SPEC-21) | Read |
| `PowerRequest` element of module m | The feature that owns m (`Draw`/`Priority` at init, `Requesting` in `PowerDemandGroup`); `ConsumerIndex`: `PowerInitSystem` | Read by `PowerAllocationSystem` |
| `Powered` bits, `ShipPower` | `PowerAllocationSystem` | Read |
| `ShieldState` HP | `ShieldSystem` (resolves shield hits before grid damage) | Read |
| Root `LocalTransform` shift for floating origin | `OriginRebaseSystem` | — |
| `FrameOrigin`, `UniversePosition` sync | `OriginRebaseSystem` | Read |
| Loaded star system set | `StarSystemLoader` (the "StarSystemStreamingSystem" of §5) | Read |
| Travel state (phase, destination, route) | `TravelService` | Read |
| Encounter instances | `EncounterDirectorService` | Read |
| Facts | `FactStore` (via effects only) | Read via conditions |
| Inventory, wallet | `InventoryModel` / `Wallet` methods called by commit/merchant/loot code | Read through view models |
| Pause state | `PauseService` (tokens from `UINavigator` and others) | Read |
| Active input context | `InputService` (set by `UINavigator`) | Read |
| `Dormant` enable bit on grids | `GridLodSystem` | Read (expensive systems skip dormant grids) |
| `PersistentId` allocation | `GameSession.AllocatePersistentId()` (the counter is saved) | Read; `GridSpawnSystem` copies it from the request onto the grid root |
| Scenes | `SceneFlow` | — |
| Save files | `SaveService` | — |

---

## 9. Coordinate frames and the floating origin

| Frame | Type and unit | Used for |
|---|---|---|
| Galaxy | `double2`, light-years | Galaxy map, lanes, territory |
| Star system | `double3`, metres relative to the star | Orbits, stations, fleets on rails, saves (`UniversePosition`) |
| Local | `float3`, metres relative to `FrameOrigin` | Unity Physics, voxel grids, rendering |

- `OriginRebaseSystem` re-centres when the player ship is more than **2,000 m** from the local origin, and always on star-system entry. It shifts every **root** `LocalTransform` (`WithNone<Parent>`), resets graphical smoothing history, shifts debris particle buffers, and raises `OriginShifted`.
- Bodies on rails compute their position as `f(UniversalTime, orbit)` in double precision and convert to local each frame. Their velocity is analytic.
- Nothing else may move the origin or shift root transforms.

---

## 10. Spec map and area codes

| Code | Spec | Module |
|---|---|---|
| FND | [SPEC-01 Boot, services and scene flow](specs/SPEC-01-boot-services-sceneflow.md) | App |
| CORE | [SPEC-02 Core utilities](specs/SPEC-02-core-utilities.md) | Core |
| INP | [SPEC-03 Input and input contexts](specs/SPEC-03-input.md) | Input, Presentation |
| DATA | [SPEC-04 Content database and block definitions](specs/SPEC-04-content-and-blocks.md) | Data |
| PERS | [SPEC-05 Persistence](specs/SPEC-05-persistence.md) | Persistence |
| VOX | [SPEC-10 Voxel data model and spawning](specs/SPEC-10-voxel-data-model.md) | Voxel |
| REN | [SPEC-11 Voxel rendering and LOD](specs/SPEC-11-voxel-rendering.md) | Voxel |
| PHY | [SPEC-12 Voxel physics and mass](specs/SPEC-12-voxel-physics.md) | Voxel |
| DMG | [SPEC-13 Damage, integrity and destruction](specs/SPEC-13-damage-destruction.md) | Voxel |
| FLT | [SPEC-20 Flight and propulsion](specs/SPEC-20-flight-propulsion.md) | Simulation |
| WPN | [SPEC-21 Weapons, projectiles and beams](specs/SPEC-21-weapons.md) | Simulation |
| PWR | [SPEC-22 Power, shields and ship stats](specs/SPEC-22-power-shields-stats.md) | Simulation |
| AI | [SPEC-23 AI and fleets](specs/SPEC-23-ai.md) | Simulation |
| ABL | [SPEC-24 Abilities](specs/SPEC-24-abilities.md) | Simulation |
| GAL | [SPEC-30 Galaxy generation and coordinate frames](specs/SPEC-30-galaxy-generation.md) | Galaxy |
| SYS | [SPEC-31 Star-system streaming, bodies and interaction](specs/SPEC-31-star-system-streaming.md) | Galaxy |
| TRV | [SPEC-32 Travel](specs/SPEC-32-travel.md) | Galaxy |
| ENC | [SPEC-33 Encounters and factions](specs/SPEC-33-encounters-factions.md) | Galaxy, Simulation |
| NAR | [SPEC-40 Narrative core](specs/SPEC-40-narrative-core.md) | Narrative |
| DLG | [SPEC-41 Dialog](specs/SPEC-41-dialog.md) | Narrative, UI |
| QST | [SPEC-42 Quests, clues and the mystery](specs/SPEC-42-quests-mystery.md) | Narrative |
| ECO | [SPEC-50 Economy and inventory](specs/SPEC-50-economy-inventory.md) | Economy |
| EDT | [SPEC-51 Ship editor](specs/SPEC-51-ship-editor.md) | ShipEditor |
| UI | [SPEC-60 UI framework](specs/SPEC-60-ui-framework.md) | UI |
| HUD | [SPEC-61 Screens and HUD](specs/SPEC-61-screens-hud.md) | UI, Presentation |
| CAM | [SPEC-62 Camera](specs/SPEC-62-camera.md) | Presentation |
| VFX | [SPEC-63 Visual effects](specs/SPEC-63-vfx.md) | Presentation |
| AUD | [SPEC-64 Audio](specs/SPEC-64-audio.md) | Presentation |
| DEV | [SPEC-70 Dev tools and CLI harness](specs/SPEC-70-devtools-harness.md) | DevTools |
| TST | [SPEC-71 Testing and performance gates](specs/SPEC-71-testing-perf-gates.md) | Tests |
| MIG | [SPEC-72 Legacy content migration](specs/SPEC-72-legacy-migration.md) | Editor |
| ART | [SPEC-80 Art direction and design system](specs/SPEC-80-art-direction.md) | Art (docs, definitions) |
| AST | [SPEC-81 Asset production](specs/SPEC-81-asset-production.md) | Art (assets) |

---

## 11. Scalability rules (binding)

The game must hold 1M-cell stations, 30-ship battles and 50,000 debris particles at 60 FPS (see [06-performance-budgets.md](06-performance-budgets.md)). These rules are where naïve DOTS code usually loses 10–100×. Code review rejects violations. Concrete wrong/right examples are in [07-implementation-traps.md](07-implementation-traps.md).

1. **Incremental, never global.** Every piece of derived per-grid data is updated for **dirty pieces only**: meshes, colliders, mass, stats, integrity, LOD. Any pass that touches every cell of a grid needs a spec decision that names it and a budget in 06. Removing one cell must cost O(brick) for rebuilds plus O(bricks of that grid) at most for re-assembly, never O(cells of the grid).
2. **Time-sliced with explicit budgets.** Unbounded work, such as remeshing after a big explosion, spawning a 1M-cell grid or streaming a star system, is queued and processed under a per-frame budget measured in microseconds or items, in a stated priority order. Examples of priority: colliders of grids near the player before meshes of far grids; visible before hidden. No frame may exceed the 8 ms voxel spike budget.
3. **Rendering through registered IDs.**
   - Every brick mesh is a `Mesh` registered **once** with `EntitiesGraphicsSystem.RegisterMesh`, referenced by `BatchMeshID` in `MaterialMeshInfo`, and rewritten **in place** (`Mesh.ApplyAndDisposeWritableMeshData`, explicit bounds, then update `RenderBounds`). It is unregistered and destroyed through `DeferredDisposalSystem`.
   - Materials are shared, one per material layer, registered once.
   - Per-instance variation comes from material-property components or the palette, never from material instances.
   - **Never** give entities their own `RenderMeshArray`.
4. **Shared components only at low cardinality.** Allowed: `StarSystemMember` (≤ 2 values) and the components Entities Graphics adds itself. A shared component whose value is unique per grid, per brick or per ship splits chunks down to one entity each and is forbidden.
5. **Large buffers live outside chunks.** Any dynamic buffer that is typically larger than 128 bytes is declared `[InternalBufferCapacity(0)]`.
6. **Batch structural changes.**
   - Create many entities with `EntityManager.CreateEntity(archetype, NativeArray<Entity>)` or `Instantiate(prefab, NativeArray<Entity>)`, then fill them from a parallel job via lookups.
   - Destroy by query.
   - Never use a per-entity `AddComponent` / `SetComponent` loop on the main thread for more than 100 entities.
7. **No main-thread per-entity loops in hot paths.** Fixed-step and per-frame work over more than 100 entities runs in Burst jobs (`IJobEntity`/`IJobChunk`, `ScheduleParallel` when writes are disjoint).
8. **Physics colliders are two-level.**
   - Each brick has its own collider blob (merged boxes).
   - The grid's `PhysicsCollider` is a compound over the brick blobs, so a changed brick rebuilds only its own blob and the compound is re-assembled from cached children.
   - Replaced blobs are disposed through `DeferredDisposalSystem` after the physics step that last used them (at least 3 frames).
   - Never rebuild every brick's collider because one changed.
9. **Simulation level of detail.** Grids far from the player go **dormant**: an enableable `Dormant` tag, owned by `GridLodSystem` (SPEC-11), switches off AI thinking, weapon aiming, stats recompute and mesh LOD0. Physics stays on. AI decisions are staggered across ticks: each AI ship thinks every N ticks, in buckets by `PersistentId`.
10. **Memory is budgeted, not hoped for.** Specs that store per-cell or per-vertex data state their bytes per item and the total for the 1M-cell reference station, and those totals must add up to the 06 memory gate. Packed vertex formats are required for brick meshes.
11. **Measure before optimising, and measure after.** Every system with a budget gets a performance test (`Game.Tests.PlayMode`, category `Performance`) and `ProfilerMarker`s named `<SystemName>.<Step>` around its expensive steps. `gh_perf` reads them through `ProfilerRecorder`.
