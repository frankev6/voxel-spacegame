# SPEC-01 — Boot, services and scene flow

| | |
|---|---|
| **Status** | Draft |
| **Area code** | FND (task IDs are `FND-001`, `FND-002`, …) |
| **Module(s)** | `Game.App` (folder `Assets/Game/App/`); one attribute in `Game.Core` (`Assets/Game/Core/StaticStateOkAttribute.cs`); `GameConfigDefinition` in `Game.Data` (`Assets/Game/Data/Definitions/Config/`); project settings in `Game.Editor` (`ProjectSetup`) |
| **Depends on specs** | SPEC-02 (every Core contract: `Services`, `IPauseService`, `ISettingsService`, `IGameFlow`, `ISceneReady`, `ISceneExiting`, `ISessionModel`, `ISessionWriteback`, `INewGameInitializer`, `ISaveBlocker`, `GameplayActive`, `SimClock`, `PersistentId`, `PersistentIdAllocator`, `IPersistentIdAllocator`, `EventHub`, `GameSettings`), SPEC-03 (`InputService`), SPEC-04 (`ContentService`), SPEC-05 (`SaveService`, `ISaveContext`, participants), SPEC-10 (`BlueprintLibrary`, `VoxelStructureRegistry`, `GridSpawnApi`, `GridStructureSync`), SPEC-30/31 (`GalaxyService`, `GalaxyState`, star-system loading hooks), SPEC-40 (`NarrativeBootstrap`), SPEC-60/61 (`UIRoot`, `UiComposition`), SPEC-62 (`CameraRigService`), SPEC-63 (`FxRig`), SPEC-64 (`AudioService`), SPEC-70 (`DevRuntimeHost`) |
| **Depended on by** | Every spec that needs construction, the scene lifecycle, the session or `PersistentId`s; SPEC-05 (`SESS`, `FLET`, `ISaveContext`); SPEC-51 (lab entry/exit); SPEC-61 (root screens, presenters); SPEC-70 (new game, sandbox, `ShipSpawnService`) |
| **Milestone** | M0 (FND-010…FND-015 in M1/M3) |

> Read [01-architecture.md](../01-architecture.md) §4, [02-conventions.md](../02-conventions.md) §3, [03-glossary.md](../03-glossary.md) and [07-implementation-traps.md](../07-implementation-traps.md) §6 before this spec. This spec may not contradict them.

## 1. Purpose

This spec is the composition root.
- It creates the ECS World, constructs every managed service in one fixed order, and owns the scene lifecycle (`SceneFlow`) and the campaign session (`GameSession`).
- It also covers the player's fleet record and spawn, and persistent-ID allocation.
- Any scene can be played from the Editor; Enter Play Mode without domain reload leaves no state behind, and loading always restores in place.

It replaces the old prototype's scattered singletons, managers living in content scenes, `FindObjectOfType` wiring, and load/editor flows that teleported the player or unloaded the wrong star system (audit 01/04, R16/R19/R28).

## 2. Scope

**In scope:**
- `Bootstrap`, the `GameRoot` prefab and component, explicit World creation, and the service construction order and shutdown.
- `PauseService`, `SettingsService` (persistence and application of `GameSettings`), `SceneFlow` (all scene transitions, teardown, ready hooks, error recovery) and `GameSession` (implements `IGameFlow`, `IPersistentIdAllocator`, `ISaveContext`).
- The `SESS` and `FLET` save participants.
- `FleetModel`, the player-ship spawn and write-back, and `ShipSpawnService`.
- `SimClockAdvanceSystem`, `PersistentIdRefillSystem`, `BootSingletons`, `GameConfigDefinition`.
- The defeat flow, `StaticStateAuditTests`, and the boot-from-any-scene and re-entry tests.

**Out of scope (and who owns it instead):**
- What each service does internally: its own spec.
- The save container and slot files: SPEC-05.
- Loading and unloading star-system content, `PLOC`: SPEC-31.
- Galaxy generation: SPEC-30.
- UI screens and presenters (Main menu, Loading, Settings, Defeat dialog content): SPEC-61.
- Input maps: SPEC-03.
- Lab editing: SPEC-51.
- Root components of ships (SPEC-20/21/22/23/24): they come from the grid profile `ShipSpawnService` chooses (ADR-0015); this spec only adds the requester values (`Allegiance`, `AiBrain.ProfileIndex`, `StarSystemMember`).

## 3. Decisions (binding)

1. **The runtime ECS World is created explicitly by `Bootstrap`**, never by Entities' automatic bootstrap. The scripting define `UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_RUNTIME_WORLD` is set for the Standalone target, and `ProjectSetup` enforces it. `Bootstrap` calls `DefaultWorldInitialization.Initialize("Default World", false)` before instantiating `GameRoot`. Reason: two `BeforeSceneLoad` hooks have undefined order; the World must exist before any service touches it.
2. **`GameRoot` is the only object created at boot.** `Bootstrap` (`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`) loads `Resources/GameRoot` (the one allowed `Resources.Load`, conventions §3.2), instantiates it and marks it `DontDestroyOnLoad`. Content scenes contain no managers, cameras, audio listeners or `UIDocument`s. Reason: architecture §4.1.
3. **Services are constructed in the exact order of §5.2 and disposed in reverse.** No service constructs another; dependencies are constructor parameters. Everything that must be found later is registered in `Services`. Reason: architecture §4.2.
4. **`SceneFlow` is the only code that loads scenes, creates/destroys `GameplayActive`, `SimClock` and `WorldSceneActive`, and tears down gameplay entities.** Every transition is serialized: one at a time; a second request returns `FlowResult.Busy`. Reason: architecture §8.
5. **Teardown destroys every entity created after boot.** At the end of boot, `SceneFlow` snapshots the set of existing entities: config and system-owned singletons. Leaving the World or the Lab destroys every entity not in that set, in one batch. Systems therefore create singletons that own native memory in `OnCreate` and dispose them in `OnDestroy` (never lazily in `OnUpdate`). Systems that release resources for destroyed entities (cleanup components) never `RequireForUpdate<GameplayActive>`. Reason: one rule catches every module's entities without per-type lists.
6. **`GameplayActive` and `SimClock` exist in the World and the Blueprint Lab; `WorldSceneActive` (tag) exists only in the World.** Systems that need a loaded galaxy (streaming, encounters, travel, territory, origin rebasing) also `RequireForUpdate<WorldSceneActive>`. Reason: the Lab spawns, meshes and collides preview grids (SPEC-10/51) but has no galaxy.
7. **Campaign time is owned by `GameSession.UniversalTime`.** On World entry `SimClock` starts at it. On World exit, and before every save capture, it is written back. The Lab uses a throwaway clock starting at 0 that is never written back. Reason: SPEC-02 §5.9; editing never advances the campaign.
8. **Load restores in place.** Order:
   1. tear down gameplay;
   2. `SaveService.Restore` (participants mutate existing models);
   3. publish `SessionReset(Loaded)` exactly once;
   4. `GalaxyService.OnSessionRestored()`;
   5. load the World;
   6. the ready hooks place the player.

   Session models are never replaced by new objects. Loading never runs narrative effects (architecture §7.3). Reason: R28, 04-B1.
9. **`PersistentId`s are allocated only by `GameSession`.** Managed callers use `IPersistentIdAllocator.Allocate()`. Burst systems draw from the `PersistentIdAllocator` singleton, which `PersistentIdRefillSystem` refills with blocks of 4,096 when fewer than 1,024 remain. The saved counter is the next ungranted id, so unused ids in a granted block are skipped and never reused. Reason: architecture §8, SPEC-13 Decision 18.
10. **Pause = `Time.timeScale = 0`.** `PauseService` is the only writer of `Time.timeScale` (conventions add-on, enforced by `ConventionsTests`). UI and loading use unscaled time. Reason: zero fixed ticks run while paused (SPEC-02 §5.9).
11. **Settings live in `Application.persistentDataPath/settings.json`**, written with SPEC-05's `NewtonsoftSaveJsonCodec` (Game.App has no direct Newtonsoft reference, architecture §3.1). Saves are debounced 0.5 s unscaled and flushed on quit. Reason: no `PlayerPrefs` (not inspectable, not versioned).
12. **The player ship is a fleet record, not an ECS entity.** `FleetModel` owns each fleet ship's `VoxelStructure`. The player's grid is spawned from it with `WriteBack`, and `GridStructureSync.WriteBack` copies damage back before save capture, World exit and Lab entry. Its `PersistentId` never changes, including across edits. Reason: one voxel model (architecture §1 rule 2), R16/R19.
13. **Play from any scene** (Editor and development builds):
    - `Boot` → main menu;
    - `MainMenu` → main menu;
    - `World` → dev quick start (new game with `GameConfigDefinition.DevQuickStartSeed`);
    - `BlueprintLab` → creative Lab.

    Release builds always start at `Boot` → main menu. Reason: fast iteration (ROADMAP M0 exit gate).
14. **Failures recover to the main menu.** A ready hook that throws, or does not complete within `SceneReadyTimeoutSeconds` (30 s), triggers teardown, then `MainMenu` with `SceneEntryReason.ErrorRecovery`, and a notification. Nothing is left half-loaded. Reason: no soft-locks (R16/R19).

## 4. Data model

### 4.1 Core additions (public contract, owned here)

```csharp
// Assets/Game/Core/Components/WorldSceneActive.cs  (namespace Game.Core.Components)
/// <summary>Tag singleton: exists only while the World scene is ready (Decision 6). Created/destroyed by SceneFlow.</summary>
public struct WorldSceneActive : Unity.Entities.IComponentData { }

// Assets/Game/Core/StaticStateOkAttribute.cs  (namespace Game.Core)
/// <summary>Marks a static field that StaticStateAuditTests must accept (immutable after first use, or reset elsewhere). Reason required.</summary>
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class StaticStateOkAttribute : System.Attribute { public readonly string Reason; public StaticStateOkAttribute(string reason) { Reason = reason; } }
```

`WorldSceneActive` is one more Core ECS type next to `GameplayActive` (architecture §4.3 is updated in the same commit as FND-001).

### 4.2 Configuration (public contract)

```csharp
// Assets/Game/Data/Definitions/Config/GameConfigDefinition.cs  (namespace Game.Data.Definitions)
[ContentIdPrefix("config_")]
[UnityEngine.CreateAssetMenu(menuName = "Game/Content/Config/Game Config", fileName = "config_game")]
public sealed class GameConfigDefinition : ContentDefinition       // exactly one asset: config_game
{
    [SerializeField] private ShipBlueprintDefinition startBlueprint;     // player flagship for a new game
    [SerializeField] private string defaultCommanderName = "Commander";
    [SerializeField] private ulong devQuickStartSeed = 12345ul;
    [SerializeField] private float sceneReadyTimeoutSeconds = 30f;       // 5..120
    [SerializeField] private int persistentIdBlockSize = 4096;           // 256..65536
    [SerializeField] private int persistentIdRefillThreshold = 1024;     // < block size
    [SerializeField] private float defeatDelaySeconds = 3f;              // unscaled; 0..10
    [SerializeField] private float combatDamageWindowSeconds = 10f;      // SPEC-05 Q14
    [SerializeField] private float settingsSaveDebounceSeconds = 0.5f;
    // read-only PascalCase properties for every field (SPEC-04 property rule)
}
```

Validation (`Validate`):
- `FND-E001`: `startBlueprint` is null.
- `FND-E002`: a number is out of the range in its comment.
- `FND-E003`: there is not exactly one `GameConfigDefinition` (reported by `GameConfigValidator`, an `IContentDatabaseValidator`).

```csharp
// Assets/Game/App/Boot/GameRootConfig.cs  (namespace Game.App.Boot) — serialized on the GameRoot prefab
[System.Serializable]
public sealed class GameRootConfig
{
    public Game.Data.ContentDatabase ContentDatabase;               // Assets/Game/Content/ContentDatabase.asset
    public UnityEngine.UIElements.UIDocument UiDocument;            // child "UI" of the prefab, with PanelSettings
    public Game.UI.Data.UIConfig UiConfig;                          // SPEC-60
    public Game.Presentation.Data.CameraConfig CameraConfig;        // SPEC-62
}
```

Every other configuration is a content definition looked up through `ContentService.Catalog` (`config_voxel`, `config_damage`, `config_audio`, `config_vfx`, …).

### 4.3 Session, fleet and flow (public contract, `Game.App`)

```csharp
namespace Game.App.Session
{
    /// <summary>One ship the player owns. Implements SPEC-01 §4.3.</summary>
    public sealed class FleetShipRecord : System.IDisposable
    {
        public ulong PersistentId;                         // never changes, including across edits
        public Game.Core.ContentId SourceBlueprint;        // where it came from (display only); None if built in the Lab
        public string Name;                                // 1..32 chars
        public Game.Voxel.VoxelStructure Structure;        // owned; disposed with the record
        public int StructureHandle = -1;                   // registered in VoxelStructureRegistry while spawned; -1 otherwise
        public void Dispose();                             // Unregisters (if handle ≥ 0) and disposes Structure; idempotent
    }

    /// <summary>The player's ships (M3: exactly one, the flagship). ISessionModel: reset in place.</summary>
    public sealed class FleetModel : Game.Core.Flow.ISessionModel, System.IDisposable
    {
        public System.Collections.Generic.IReadOnlyList<FleetShipRecord> Ships { get; }  // ascending PersistentId
        public FleetShipRecord Flagship { get; }            // null only between ResetToEmpty and new-game/restore
        public FleetShipRecord Find(ulong persistentId);    // null if absent
        public void Add(FleetShipRecord r, bool makeFlagship);   // takes ownership; InvalidOperationException on duplicate id
        public bool Remove(ulong persistentId);             // disposes; the flagship cannot be removed (returns false)
        public void ReplaceStructure(ulong persistentId, Game.Voxel.VoxelStructure s);   // editor commit; disposes the old structure
        public uint Revision { get; }                       // +1 on any change (UI change detection)
        public event System.Action Changed;                 // raised synchronously after Add, Remove, ReplaceStructure and
                                                            // ResetToEmpty, once per call (SPEC-50 CargoCapacitySync listens)
        public void ResetToEmpty();                         // disposes all records
        public void Dispose();
    }

    /// <summary>The campaign session. Implements IGameFlow (SPEC-02), IPersistentIdAllocator, ISaveContext (SPEC-05).</summary>
    public sealed class GameSession : Game.Core.Flow.IGameFlow, Game.Core.IPersistentIdAllocator,
                                      Game.Persistence.Services.ISaveContext, System.IDisposable
    {
        public bool HasActiveSession { get; }
        public bool IsCreativeSession { get; }
        public string CommanderName { get; }
        public ulong CampaignSeed { get; }                  // galaxy seed of this campaign
        public double UniversalTime { get; }                // campaign time (Decision 7)
        public double PlaytimeSeconds { get; }              // unscaled seconds in World, accumulated by Tick
        public ulong NextPersistentId { get; }              // next ungranted id; ≥ 1
        public FleetModel Fleet { get; }
        public Game.Galaxy.Data.GalaxyState Galaxy { get; }

        public void RegisterModel(Game.Core.Flow.ISessionModel model);           // reset order = registration order
        public void RegisterWriteback(Game.Core.Flow.ISessionWriteback w);       // sorted by WritebackOrder
        public void RegisterNewGameInitializer(Game.Core.Flow.INewGameInitializer i); // sorted by NewGameOrder
        public void RegisterSaveBlocker(Game.Core.Flow.ISaveBlocker b);
        public void RunWritebacks();                        // all writebacks in order; main thread; never inside a system update
        public void Tick(float unscaledDeltaTime);          // playtime; called by GameRoot.Update
        // IGameFlow, IPersistentIdAllocator and ISaveContext members as declared by SPEC-02 / SPEC-05
    }
}

namespace Game.App.Flow
{
    public readonly struct SandboxArgs                      // dev only: World scene without galaxy streaming
    {
        public readonly bool SpawnPlayer; public readonly ulong Seed;
        public SandboxArgs(bool spawnPlayer, ulong seed) { SpawnPlayer = spawnPlayer; Seed = seed; }
    }

    public enum SceneFlowState : byte { Idle = 0, Exiting = 1, Loading = 2, Entering = 3, Recovering = 4 }

    /// <summary>The only scene loader (Decision 4). Main thread. Implements SPEC-01 §5.4.</summary>
    public sealed class SceneFlow : System.IDisposable
    {
        public Game.Core.Flow.GameScene CurrentScene { get; }
        public SceneFlowState State { get; }
        public bool IsTransitioning => State != SceneFlowState.Idle;
        public float Progress { get; }                      // 0..1 for LoadingView
        public void RegisterReady(Game.Core.Flow.ISceneReady hook);      // sorted by SceneReadyOrder, then type full name
        public void RegisterExiting(Game.Core.Flow.ISceneExiting hook);  // sorted by SceneExitOrder, then type full name
        public void SetRootScreens(Game.UI.Views.IRootScreenSet screens);// SPEC-61 UiComposition; null-safe until set
        public UnityEngine.Awaitable<Game.Core.Flow.FlowResult> StartupAsync();                 // once, from GameRoot.Start
        public UnityEngine.Awaitable<Game.Core.Flow.FlowResult> EnterSandboxAsync(SandboxArgs args); // editor/dev builds only
        // New game, load, quit, lab: through GameSession (IGameFlow), which calls the internal TransitionAsync
        public void Dispose();
    }
}
```

`IRootScreenSet` is SPEC-61's interface exposing `MainMenu`, `FlightHud`, `ShipEditor` and `Loading` (`UiComposition` implements it).

### 4.4 Spawning (public contract)

```csharp
namespace Game.App.Spawning
{
    public struct ShipSpawnArgs
    {
        public Game.Core.ContentId Blueprint;               // blueprint source; ignored when Structure != null
        public Game.Voxel.VoxelStructure Structure;         // registered structure source (caller keeps ownership)
        public Unity.Mathematics.float3 Position;           // local frame
        public Unity.Mathematics.quaternion Rotation;       // yaw only (ADR-0003); other components are discarded
        public Unity.Mathematics.float3 LinearVelocity;
        public Game.Simulation.Allegiance Allegiance;       // SPEC-21/23 owner of the struct (architecture §6.4)
        public bool WithAi;                                 // adds SPEC-23 AI components
        public Game.Core.ContentId AiProfile;               // None = the faction default (SPEC-23)
        public bool IsPlayer;                               // PlayerShip tag, WriteBack, no StarSystemMember
        public ulong PersistentId;                          // 0 = allocate
    }

    /// <summary>The single entry point for spawning ships and stations at runtime (App, Galaxy services via an interface, harness).</summary>
    public sealed class ShipSpawnService : Game.Galaxy.Services.IShipSpawner
    {
        public ShipSpawnService(Unity.Entities.World world, Game.Voxel.Grid.VoxelStructureRegistry registry,
                                Game.Data.Services.ContentCatalog catalog, Game.Core.IPersistentIdAllocator ids,
                                Game.Galaxy.Services.GalaxyService galaxy);
        public Game.Core.Components.PersistentId Spawn(in ShipSpawnArgs args);   // returns the id; root entity via TryGetRoot
        public Game.Core.Components.PersistentId Spawn(Game.Core.ContentId blueprint, Unity.Mathematics.float3 position,
                                                       Unity.Mathematics.quaternion rotation, Game.Simulation.Allegiance allegiance, bool withAi); // SPEC-70 signature
        public bool TryGetRoot(Game.Core.Components.PersistentId id, out Unity.Entities.Entity root);   // O(1) map kept by a spawn hook
        public bool Despawn(Game.Core.Components.PersistentId id);               // GridSpawnApi.RequestDespawn
        // IShipSpawner (SPEC-31 §4.4), explicit implementations, mapping in §5.7:
        Game.Galaxy.Services.PersistentIdValue Game.Galaxy.Services.IShipSpawner.Spawn(in Game.Galaxy.Services.GalaxyShipSpawnArgs args);
        bool Game.Galaxy.Services.IShipSpawner.TryGetRoot(ulong persistentId, out Unity.Entities.Entity root);
        bool Game.Galaxy.Services.IShipSpawner.Despawn(ulong persistentId);
    }
}
```

`Game.App` references every module, so `ShipSpawnService` can call SPEC-20/21/23 setup helpers. Galaxy services (SPEC-31/33) receive it through `Game.Galaxy.Services.IShipSpawner` (declared in SPEC-31 §4.4; `ShipSpawnService` implements it, §5.7).

### 4.5 Internal types

```csharp
namespace Game.App.Boot
{
    public sealed class GameRoot : UnityEngine.MonoBehaviour      // on Assets/Game/App/Resources/GameRoot.prefab
    {
        [UnityEngine.SerializeField] private GameRootConfig config;
        internal static GameRoot Instance;                        // reset in SubsystemRegistration
        internal GameComposition Composition { get; private set; }
    }
    internal static class Bootstrap { }                           // BeforeSceneLoad entry
    internal sealed class GameComposition : System.IDisposable    // §5.2: builds and disposes every service in order
    { internal readonly System.Collections.Generic.List<System.IDisposable> DisposeStack; }
    internal static class BootSingletons                          // §5.3
    { internal static void CreateAll(Unity.Entities.EntityManager em, Game.Data.Services.ContentCatalog catalog); }
    /// <summary>Captures damaged persistent bodies into galaxy deltas before every save and World exit (§5.2 row 8.2a).</summary>
    internal sealed class GalaxyFlushWriteback : Game.Core.Flow.ISessionWriteback
    {
        internal GalaxyFlushWriteback(Game.Galaxy.Services.IGalaxyLiveDeltaSink sink);
        public int WritebackOrder => 90;                          // before FleetWriteback (100)
        public void WriteBackFromWorld();                         // sink.FlushStructures(); no-op when no WorldSceneActive exists
    }
    /// <summary>Tag on every entity BootSingletons creates (diagnostics only; teardown uses the snapshot, Decision 5).</summary>
    public struct BootSingleton : Unity.Entities.IComponentData { }
}
```

## 5. Systems, services and flow

### 5.1 Boot

**Paths and test hooks (TST-003).** `GameRoot.Awake`, before step 1 of §5.2, resolves two paths. In the Editor and in development builds (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`) it reads SPEC-71's `Game.App.Testing.GameRootTestHooks`; in release builds the hooks are not compiled in and the defaults are used:
- `savePaths = GameRootTestHooks.SaveRoot != null ? new SavePaths(GameRootTestHooks.SaveRoot) : SavePaths.CreateDefault()`;
- `settingsPaths = GameRootTestHooks.SettingsPath != null ? new SettingsPaths(GameRootTestHooks.SettingsPath) : SettingsPaths.Default`;
- `GameRootTestHooks.SkipIntroScreens` is passed to `SceneFlow.StartupAsync` (the MainMenu splash is skipped).

`SettingsPaths` (`Game.App.Settings`) is `sealed class { public SettingsPaths(string filePath); public static SettingsPaths Default { get; } /* persistentDataPath/settings.json */ public string FilePath { get; } public string TempPath { get; } /* FilePath + ".tmp" */ public string BrokenPath { get; } /* settings.broken.json next to FilePath */ }`.

`Bootstrap.Boot()` (`BeforeSceneLoad`):
1. If `GameRoot.Instance != null`, return (domain reload is off; this should not happen because of step 0 below, but it is cheap insurance).
2. If `World.DefaultGameObjectInjectionWorld == null`, call `DefaultWorldInitialization.Initialize("Default World", false)`. This creates every system and adds the world to the player loop.
3. `var prefab = Resources.Load<GameRoot>("GameRoot")`. If it is missing, `GameLog.Error` and `throw new System.InvalidOperationException("GameRoot prefab missing")`.
4. Instantiate the prefab and call `Object.DontDestroyOnLoad`. `GameRoot.Awake` runs `GameComposition.Build()` (§5.2).

Step 0: `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] static void ResetStatics() { GameRoot.Instance = null; }`.

`GameRoot.Start` calls `SceneFlow.StartupAsync()` (§5.4.6). `GameRoot.Update`:
1. `save.Autosave.Update(Time.unscaledDeltaTime)`;
2. `session.Tick(Time.unscaledDeltaTime)`;
3. `uiRoot.Tick(Time.unscaledDeltaTime)`;
4. `settings.Tick(Time.unscaledDeltaTime)` (debounced save).

`GameRoot.OnApplicationQuit`: `settings.Flush()`. `GameRoot.OnDestroy`:
1. `Composition.Dispose()` (reverse order);
2. `EventHub.Reset()`;
3. if the default world `IsCreated`: `ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(world)` and `world.Dispose()`;
4. `GameRoot.Instance = null`.

### 5.2 Construction order (`GameComposition.Build`)

Every row is one step. A step's object is pushed onto `DisposeStack` when it is `IDisposable`, and registered in `Services` under the listed type(s). A step whose type does not exist yet (its spec's task not done) is a commented placeholder line naming the task that adds it. Each spec's construction task adds exactly its own line here.

| # | Step | Registered as | Added by |
|---|---|---|---|
| 1 | `settings = new SettingsService(new NewtonsoftSaveJsonCodec(), settingsPaths)` (§5.1); `settings.Load()` | `SettingsService`, `ISettingsService` | FND-004 |
| 2 | `content = new ContentService(config.ContentDatabase)` (validates; throws `ContentValidationException` → boot fails loudly); `content.CreateSingletons(em)`; `Push(BootSingletons.CreateAll(em, content.Catalog, content.PropMeshes))` (§5.3); `blueprints = new BlueprintLibrary(...)`; `registry = VoxelStructureRegistry.CreateRuntime(blueprints)`; create `VoxelStructureRegistryRef` singleton | `ContentService`, `BlueprintLibrary`, `VoxelStructureRegistry` | FND-005, DATA-007, VOX (registry task) |
| 3 | `input = new InputService(settings.Current.Controls)` | `InputService` | INP (service task) |
| 4 | `audio = new AudioService(audioConfig, config.ContentDatabase, SettingsMapping.ToVolumes(settings.Current.Audio), transform)`; `audio.BindTo(world)` (the world exists since step 2) | `AudioService`, `IAudioService` | AUD (service task) |
| 5 | `pause = new PauseService()`; `audio.BindPause(pause)` | `PauseService`, `IPauseService` | FND-003 |
| 6 | `save = new SaveService(savePaths, content, pause, new SaveServiceConfig())` (§5.1; SPEC-05 subscribes `Application.wantsToQuit` itself) | `SaveService` | PERS (service task) |
| 7 | `flow = new SceneFlow(world, pause, save, gameConfig)` | `SceneFlow` | FND-006 |
| 8 | `session = new GameSession(flow, save, world, content, gameConfig)`; `save.AttachContext(session)`; register `SessionSaveParticipant` (`SESS`), `FleetSaveParticipant` (`FLET`); `Services.Register<GameSession>(session)`, `<IGameFlow>(session)`, `<IPersistentIdAllocator>(session)` | `GameSession`, `IGameFlow`, `IPersistentIdAllocator` | FND-009 |
| 8.1 | `ticker = new GalaxyFrameTicker()`; `galaxy = new GalaxyService(content.Catalog, session.Galaxy, world, ticker)`; register `GalaxySaveParticipant` | `GalaxyFrameTicker`, `GalaxyService` | GAL-008 |
| 8.2a | `docking = new DockingService()`; `loader = new StarSystemLoader(galaxy, world, registry, blueprints, session, content.Catalog)`; `ticker.Register(loader)`; `flow.RegisterReady(loader)`; `flow.RegisterExiting(loader)`; `session.RegisterWriteback(new GalaxyFlushWriteback(loader))` (§4.5; order 90, so it runs before `FleetWriteback`); register `PlayerLocationSaveParticipant(session.Galaxy, docking)` (`PLOC`) | `DockingService`, `StarSystemLoader`, `IGalaxyLiveDeltaSink` | SYS service task |
| 8.2b | `travel = new TravelService(galaxy, loader, docking, world, content.Catalog, pause, audio)`; `ticker.Register(travel)`; `session.RegisterSaveBlocker(travel)`; `flow.RegisterExiting(travel)` | `TravelService`, `ILaneAccess` | TRV-006 |
| 8.3 | `spawner = new ShipSpawnService(world, registry, content.Catalog, session, galaxy)` | `ShipSpawnService`, `IShipSpawner` | FND-012 |
| 8.3a | `factions = new FactionService(world, content.Catalog)`; `territory = new TerritoryService(galaxy, factions, content.Catalog)`; `encounters = new EncounterDirectorService(galaxy, loader, travel, docking, spawner, factions, territory, world, content.Catalog)`; `ticker.Register(encounters)`; `ticker.Register(territory)`; `flow.RegisterExiting(encounters)`; `session.RegisterNewGameInitializer(factions)`; register the `FACN` and `ENCT` participants | `FactionService`, `TerritoryService`, `IStarSystemOwnership`, `EncounterDirectorService` | ENC-007 |
| 8.3b | `mapModel = new GalaxyMapModel(galaxy, travel, territory)`; either argument may be `null` while its task is not done (SPEC-30 §5.12: all lanes open, no owner) | `GalaxyMapModel` | GAL-011 |
| 8.4 | `economy = new EconomyService(content.Catalog, content.Catalog.Get<EconomyConfigDefinition>("config_economy"), () => session.CampaignSeed, () => session.UniversalTime)`; register `InventorySaveParticipant` (`INVT`); `cargoSync = new CargoCapacitySync(economy.Inventory, session.Fleet, content.Catalog, economyConfig)` registered as `INewGameInitializer` (order 40); `economy` registered as `INewGameInitializer` (order 50) and `ISessionModel`; `salvage = new SalvageService(world, content.Catalog, economyConfig, audio)`; `merchants = new MerchantDirectory(content.Catalog, galaxy)` (`economyConfig` = the `config_economy` definition) | `EconomyService`, `ILogicEconomy`, `InventoryModel`, `ICargo`, `Wallet`, `MerchantService`, `CargoCapacitySync`, `SalvageService`, `MerchantDirectory` | ECO-008 |
| 8.5 | `NarrativeBootstrap.Build(...)` (SPEC-40 §5.3; throws → boot fails) | per SPEC-40 | NAR boot task |
| 9 | `inputBridge = new InputServiceBridge(input)`; `ui = new UIRoot(config.UiDocument, config.UiConfig, pause, inputBridge, new AppUiSoundSink(audio), new UnityFrameClock())`; `inputBridge.BindBack(ui.HandleBackPressed)`; `Services.Register<IWorldPointerGate>(ui)` | `UIRoot`, `UINavigator` | UI (root task) |
| 9.1 | `world.GetExistingSystemManaged<PlayerInputSampleSystem>().Bind(input, ui)` (SPEC-03 INP-005; `ui` is the `IWorldPointerGate` of step 9); on dispose `Unbind()` | — | INP-005 |
| 10 | `composition = new UiComposition(ui, ...)`; `flow.SetRootScreens(composition)` | `UiComposition` | HUD-002 |
| 10.1 | `launcher = new MerchantLauncher(economy, merchants, factions, territory, encounters, loader, ui.Navigator)` (needs the navigator of step 9) | `MerchantLauncher` | ECO-008 |
| 11 | `camera = new CameraRigService(config.CameraConfig, transform, new InputCameraInput(input))`; `world.GetExistingSystemManaged<CameraTargetSyncSystem>()` bind (SPEC-62 §5); listener pivot, scene modes and optional thumbnail binding per ADR-0039; null input/FX staging until CAM-005/CAM-008 | `CameraRigService` | CAM (service task) |
| 12 | `fx = FxRig.Create(transform, vfxConfig, content.Catalog.All<FxDefinition>(), cellPalette, (VfxQuality)settings.Current.Video.VfxQuality)` (the setting is a `byte`, SPEC-02; the enum is SPEC-63's); `fx.BindTo(world)` | `FxRig` | VFX (rig task) |
| 13 | SPEC-41 `DialogPresenter` (after `UIRoot`) | per SPEC-41 | DLG presenter task |
| 14 | `SettingsApplier.ApplyAll(settings.Current)` (§5.6): pushes every section to its consumer once | — | FND-004 |
| 15 | `flow.SnapshotBootEntities()` (Decision 5) | — | FND-006 |
| 16 | `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`: `dev = DevRuntimeHost.Create(transform)`; `Services.Register(dev)` | `DevRuntimeHost` | DEV host task |

Rules:
- `em` = `World.DefaultGameObjectInjectionWorld.EntityManager`.
- `SettingsMapping` (`Game.App.Settings`, FND-004) holds the pure conversions between settings sections and consumer types: `static AudioVolumes ToVolumes(AudioSettingsData a)` copies `MasterVolume → Master`, `MusicVolume → Music`, `SfxVolume → Sfx`, `UiVolume → Ui`, `AmbienceVolume → Ambience`.
- **M4:** the fleet task implements SPEC-32's `IJumpEscorts` (§5.11 of SPEC-32) in `Game.App` and registers it; until then `TravelService` uses its no-escort default.
- If any step throws, `Build` disposes everything already on `DisposeStack` (in reverse), logs `GameLog.Exception("Boot", ...)`, and rethrows. In the Editor this stops Play Mode (`EditorApplication.isPlaying = false` through `#if UNITY_EDITOR`).
- **Shutdown** (`Dispose`) runs `DisposeStack` in reverse, then `Services.Reset()`. Each `Dispose` is wrapped in try/catch so one failure cannot leak the rest.

### 5.3 `BootSingletons.CreateAll`

Creates ECS configuration singletons from content, once per boot, **in table order**. Each line is added by the task named. Every created entity gets `BootSingleton`, and `EntityManager.SetName("Cfg.<Type>")` in the Editor. Factories with a `List<Entity> created` parameter append the entities they made; `CreateAll` tags those. A factory that returns a component value is written with `em.CreateSingleton(value)`. Singletons that own native memory are created here or in their system's `OnCreate`, never lazily inside an update.

| Singleton | Created by (call) | Added by |
|---|---|---|
| `BlockDefsRef`, `OrientationTablesRef` | `ContentService.CreateSingletons` (step 2) | DATA-007 |
| `VoxelConfig` | `VoxelConfigFactory.Create(catalog.Get<VoxelConfigDefinition>("config_voxel"))` | VOX config task |
| `VoxelRenderConfig` | `VoxelRenderConfigFactory.Create(catalog.Get<VoxelRenderConfigDefinition>("config_voxel_render"), content.PropMeshes)`; returned boot lifetime disposes it (SPEC-11 Q29) | REN config task |
| `PhysicsVoxelConfig` | `PhysicsVoxelConfigFactory.Create(catalog.Get<PhysicsVoxelConfigDefinition>("config_physics"))` | PHY config task |
| `PhysicsStep` | `PhysicsVoxelConfigFactory.CreatePhysicsStep(catalog.Get<PhysicsVoxelConfigDefinition>("config_physics"))` | PHY config task |
| `DamageConfig` | `DamageConfig.FromDefinition(catalog.Get<DamageConfigDefinition>("config_damage"))` with `Table = DamageContent.BuildTable(catalog)` (SPEC-13 §4.7; disposed at shutdown with the other boot blobs) | DMG config task |
| `PowerTuning` (entity "PowerSingletons"; `ShieldHitStreams` is created by SPEC-22's `ShieldSystem.OnCreate`) | `new PowerContent(catalog).CreateSingletons(em)` | PWR content task |
| `FlightConfig` | `FlightConfigFactory.Create(catalog.Get<FlightConfigDefinition>("config_flight"))` | FLT config task |
| `FactionRelations`, `FactionStanding`, `FactionRelationRequest`, `FactionState` | `FactionContent.CreateSingletons(em, catalog, catalog.Get<EncounterConfigDefinition>("config_encounters"), created)` — **before** `WeaponContent`, whose default `FactionRelations` is then skipped | ENC-001 |
| `WeaponDefsRef`, `WeaponTuning`, default `FactionRelations` | `WeaponContent.CreateSingletons(em, catalog, created)` | WPN content task |
| `AiConfig` | `AiContent.CreateSingletons(em, catalog, created)` | AI content task |
| `AbilityDefsRef` | `AbilityContent.CreateSingletons(em, catalog, created)` | ABL content task |
| `StreamingConfig` | `StreamingConfigFactory.Create(catalog.Get<StreamingConfigDefinition>("config_streaming"))` | SYS config task |
| `EconomyConfigRef` | `EconomyConfigRefFactory.Create(catalog.Get<EconomyConfigDefinition>("config_economy"))` | ECO-007 |
| `PersistentIdAllocator { Next = 0, End = 0 }` | FND-008 | FND-008 |

### 5.4 `SceneFlow`

#### 5.4.1 Transition algorithm `TransitionAsync(GameScene target, SceneEntryReason reason, BlueprintLabArgs lab, Func<Awaitable> betweenScenes)`

Main thread; `async Awaitable<FlowResult>`.
0. `GameSession` reserves the flow operation before awaiting an exit save. Other `IGameFlow` calls return `Busy`. A preparation pause covers the save and transition. The exit save runs while SceneFlow is still Idle, so GetBlockReason does not reject the save as Transitioning. Quit with saveFirst=false skips saving; explicit quit-save failure returns without teardown.
1. If `State != Idle`, return `Busy`. Set `State = Exiting` and `Progress = 0`. `EventBus.Publish(SceneTransitionStarted(current, target, reason))`. `pauseToken = pause.Acquire("SceneFlow")`. Show `Loading` as root (`ui.Navigator.SetRoot(screens.Loading)`); input context becomes `Menu` through the navigator.
2. **Exit the current scene**, if it is World or BlueprintLab:
   1. `session.RunWritebacks()`. This includes the SimClock write-back (World only) and the fleet write-back (§5.7).
   2. The session has already performed the requested exit save before entering Exiting (step 0, ADR-0034). No second save is requested here.
   3. `ISceneExiting` hooks in order, each in try/catch.
   4. **Teardown:**
      1. `em.CompleteAllTrackedJobs()`;
      2. destroy the `GameplayActive`/`SimClock`/`WorldSceneActive` entity;
      3. `all = em.GetAllEntities(Allocator.Temp)`; collect every entity not in `bootEntities` into a `NativeList`; `em.DestroyEntity(list.AsArray())`;
      4. await frames (`Awaitable.NextFrameAsync`) until no entity outside `bootEntities` remains, or 10 frames have passed. Cleanup systems run in those frames. If entities remain after 10 frames, `GameLog.Error` lists up to 10 archetypes (`em.GetChunk(e).Archetype`) and destroys them again.
3. `State = Loading`. If `betweenScenes != null`, `await betweenScenes()`. This is where load runs `save.Restore` and where new game resets models (§5.5). Exceptions go to step 7.
4. `await SceneManager.LoadSceneAsync(target.ToSceneName(), LoadSceneMode.Single)`; `Progress` follows the async operation (0.1..0.6).
5. `State = Entering`. Enter the target:
   - **World:** create one entity with `GameplayActive`, `SimClock { Tick = 0, UniversalTime = session.UniversalTime, SessionStartUniversalTime = session.UniversalTime }` and `WorldSceneActive`.
   - **BlueprintLab:** the same, without `WorldSceneActive`, starting at 0.
   - **MainMenu:** nothing.

   Then the `ISceneReady` hooks in order. For each:
   1. call `OnSceneReady(ctx)`;
   2. poll `IsSceneReadyComplete(ctx)` once per frame until true;
   3. the total time across all hooks is capped at `SceneReadyTimeoutSeconds` (unscaled), after which go to step 7.

   `Progress` = 0.6 + 0.4 × hooksDone / hookCount.
6. Set the root: MainMenu → `screens.MainMenu`; World → `screens.FlightHud`; BlueprintLab → `screens.ShipEditor`. Release `pauseToken`. `State = Idle`, `CurrentScene = target`. Publish `SceneTransitionFinished(target, reason, true)`. If `target == World` and the reason is `NewGame` or `LoadGame`, call `save.Autosave.NotifySaved()` (no immediate autosave). Return `Ok`.
7. **Recovery.** Any exception, or the timeout, in steps 3–5:
   1. `State = Recovering`;
   2. `GameLog.Exception`;
   3. teardown as in step 2.4 (no save);
   4. `session.EndSession()` (clears models, `HasActiveSession = false`);
   5. load `MainMenu` with `ErrorRecovery`;
   6. release the token; `State = Idle`;
   7. publish `SceneTransitionFinished(MainMenu, ErrorRecovery, false)`;
   8. `ui.Notifications.Show("Something went wrong while loading. Returned to the main menu.", Error)`;
   9. return `Failed`.

   A failure while recovering is logged and leaves `MainMenu` loaded.

#### 5.4.2 Who registers ready and exiting hooks (orders from `SceneHookOrder`, SPEC-02)

| Order | Hook | Owner |
|---|---|---|
| 50 Session | `GameSession` (World: starts playtime; Lab: nothing) | FND-009 |
| 100 Galaxy | SPEC-31 `StarSystemLoader` (World: load the current star system, set the origin at the player location) | SYS |
| 200 PlayerShip | `PlayerShipSpawner` (World: spawn the flagship at `GalaxyState.PlayerLocation`; complete when the root has `PhysicsCollider` and `GridSpawning` is disabled) | FND-010 |
| 300 Camera | SPEC-62 (mode `Flight`/`Editor`/`Menu`) | CAM |
| 400 Ui | SPEC-61 presenters | HUD |
| 500 Narrative | SPEC-40 `SetWorldReady` is driven by its bridge; no hook needed | — |
| 700 Editor | SPEC-51 `ShipEditorHost.Create` (BlueprintLab) | EDT |

#### 5.4.3 Flows exposed through `GameSession` (`IGameFlow`)

| Call | Allowed from | Steps |
|---|---|---|
| `NewGameAsync(args)` | MainMenu, World (after confirm) | `TransitionAsync(World, NewGame, betweenScenes: session.BeginNewGame(args))` |
| `LoadGameAsync(slotId)` | MainMenu, World | `r = await save.ReadAsync(slot)`; failure → `Failed` with no transition. Else `TransitionAsync(World, LoadGame, betweenScenes: session.RestoreFrom(r))` |
| `SaveGameAsync(slotId, reason)` | World | `CanSave` false → `Blocked`; `await save.SaveAsync(...)` (PrepareCapture performs the writebacks) |
| `QuitToMainMenuAsync(saveFirst)` | World, BlueprintLab | `saveFirst` → save as above, then `TransitionAsync(MainMenu, QuitToMenu, betweenScenes: session.EndSession)` |
| `EnterBlueprintLabAsync(args)` | MainMenu (Creative), World (FleetShip, docked — SPEC-51 checks) | FleetShip: capture the player pose into `GalaxyState.PlayerLocation` (`galaxy.CapturePlayerLocation()`) before teardown. `TransitionAsync(BlueprintLab, EnterBlueprintLab, lab: args)` |
| `ExitBlueprintLabAsync()` | BlueprintLab | Creative → `TransitionAsync(MainMenu, ExitBlueprintLab)`. FleetShip → `TransitionAsync(World, ExitBlueprintLab)`: the star system regenerates and the player respawns from the (possibly replaced) structure with the same `PersistentId` at the captured location |
| `QuitToDesktop()` | any | `settings.Flush()`; `Application.Quit()` (SPEC-05's quit-save hook decides about a final save) |

`IGameFlow` methods never throw. They return `NotAllowed` when called from a scene not listed, and `Busy` during a transition.

#### 5.4.4 `EnterSandboxAsync(args)` (dev only)

1. Reserve the session operation (Busy if another operation/save is active), pause, and perform the ordinary exit save when leaving World.
2. `TransitionAsync(World, DevQuickStart, betweenScenes: ...)`: after teardown, `session.BeginSandbox(args.Seed)` resets in place with `IsCreativeSession = true`, no campaign initializers or galaxy history, and the requested optional-player flag (Q23).
3. `StarSystemLoader` sees `GalaxyService.Galaxy == null` and loads nothing.
4. `PlayerShipSpawner` spawns the flagship at local (0,0,0) when `args.SpawnPlayer`.

#### 5.4.5 `SnapshotBootEntities()`

`bootEntities = new NativeHashSet<Entity>(count, Allocator.Persistent)` of `em.GetAllEntities(Allocator.Temp)`. Disposed in `SceneFlow.Dispose`.

#### 5.4.6 `StartupAsync()`

`active = SceneManager.GetActiveScene().name` mapped to a `GameScene`.
- **Boot:** `TransitionAsync(MainMenu, Startup)`.
- **MainMenu:** treat it as entered: run the ready hooks for MainMenu, set the root and `CurrentScene` (no reload).
- **World** (Editor/dev): `NewGameAsync(new NewGameArgs(defaultCommanderName, devQuickStartSeed, false, ContentId.None))` with reason `DevQuickStart`. The World scene is reloaded (Single) so the entry path is identical to a normal new game. In release, use MainMenu.
- **BlueprintLab** (Editor/dev): `EnterBlueprintLabAsync(new BlueprintLabArgs(Creative, 0, ContentId.None))` with a session from `BeginSandbox(1)`.

### 5.5 `GameSession` behaviour

- **`BeginNewGame(args)`** (between scenes; until a profile picker exists, create a profile using the configured default commander name only if no active profile is selected):
  1. `save.ResetForNewGame()`;
  2. every registered `ISessionModel.ResetToEmpty()` in registration order;
  3. `CommanderName = args.CommanderName`;
  4. seed: `CampaignSeed = args.GalaxySeed != 0 ? args.GalaxySeed : (ulong)System.Diagnostics.Stopwatch.GetTimestamp() | 1`. The only non-deterministic seed source; it is saved, so loads stay deterministic;
  5. `UniversalTime = 0`; `PlaytimeSeconds = 0`; `NextPersistentId = 1`;
  6. `galaxy.NewGame(CampaignSeed)`;
  7. flagship: `structure = blueprints.TryGet(startBlueprint).Clone()`, `id = Allocate()`, `Fleet.Add(new FleetShipRecord { PersistentId = id, SourceBlueprint = startBlueprint.Id, Name = startBlueprint display name, Structure = structure }, makeFlagship: true)`;
  8. `start = galaxy.GetStartLocation()`; `session.Galaxy.PlayerLocation = start`; `HasPlayerLocation = true`;
  9. every `INewGameInitializer.InitializeNewGame(args)` in `NewGameOrder` (SPEC-50 starting cargo and credits, SPEC-40 facts);
  10. `EventBus.Publish(new SessionReset(SessionResetReason.NewGame))`;
  11. `HasActiveSession = true`.
- **`RestoreFrom(loadResult)`** (between scenes):
  1. `report = save.Restore(loadResult.Save)`. Participants restore `SESS`, `FLET`, `GLXY`, `PLOC` and the rest in place.
  2. Publish `SessionReset(Loaded)` exactly once.
  3. `galaxy.OnSessionRestored()` (Decision 8 owns the ordering; Q17).
  4. `HasActiveSession = true`.
  5. If `report.ErrorCount > 0`, show a notification "Loaded with N problems; see the log". Missing content never blocks the load (04-B1).
- **`EndSession()`**: every model `ResetToEmpty()`; publish `SessionReset(Cleared)`; `HasActiveSession = false`.
- **`Allocate()`**: `if (NextPersistentId == ulong.MaxValue) throw new InvalidOperationException(); return NextPersistentId++;`
- **`GetBlockReason()`** (SPEC-05 `ISaveContext`), in order:
  - no session → `NoSession`;
  - `flow.IsTransitioning` → `Transitioning`;
  - `CurrentScene == BlueprintLab` → `EditorOpen`;
  - player defeated (§5.9) → `PlayerDead`;
  - any registered `ISaveBlocker` returning non-`None` → that value;
  - the `PlayerCombatStatus` singleton (SPEC-23) has `HostilesEngaging > 0`, or `SimClock.Tick − LastDamagedTick < combatDamageWindowSeconds × 60` → `InCombat`. Read it through a cached `EntityQuery` with `query.CompleteDependency()` before `GetSingleton` (its writer is a scheduled job), never `GetSingletonRW`;
  - else `None`.

  SPEC-05 uses SPEC-02's `Game.Core.Save.SaveBlockReason` directly (SPEC-05 has no enum of its own).
- **`DescribeForMeta(meta)`**: `CommanderName`; `LocationText = galaxy.GetStarSystemName(current)`; `PlaytimeSeconds`.
- **Thumbnails** ([ADR-0034](../decisions/ADR-0034-session-save-preparation.md)): `PrepareCapture` runs writebacks for every save before participant capture. `TryCaptureSlotThumbnail` renders the main camera to a 320×180 `RenderTexture` and reads it back with `AsyncGPUReadback`. The request carries the pending readback; SPEC-05 waits ≤ 2 frames. In the Lab it returns false.

### 5.6 `PauseService`, `SettingsService`, `SettingsApplier`

**`PauseService`**:
- A `List<(int id, string reason)>`; ids increment from 1. `Acquire` adds an entry and returns `new PauseToken(id, this)`; `Release` removes it.
- When the count changes 0 → 1: `Time.timeScale = 0`, publish `PauseStateChanged(true, n)`. When it changes 1 → 0: `Time.timeScale = 1`, publish `PauseStateChanged(false, 0)`. Otherwise publish only when `IsPaused` changes.
- `Dispose` releases everything and sets `Time.timeScale = 1`.

**`SettingsService`** (`ISettingsService`):
- `Load()`:
  1. read `settings.json` if present;
  2. JSON → `GameSettingsDto` (`version`, then one object per section) → `GameSettings`;
  3. on any error, keep defaults, rename the broken file to `settings.broken.json` and `GameLog.Warn`;
  4. `Clamp()`.
- `Apply(edited, sections)`:
  1. `Current.CopyFrom(edited, sections)`; `Current.Clamp()`;
  2. `changed = Current.DiffersFrom(previousSnapshot) & sections`;
  3. if `changed != None`: publish `SettingsChanged(changed)`, mark dirty, and restart the debounce timer.
- `Tick(dt)` writes when dirty and the timer has elapsed. The write goes to `settings.json.tmp`, followed by `File.Replace`. `Flush()` writes now if dirty.

**`SettingsApplier`** (`Game.App`): subscribes to `SettingsChanged` and applies only the changed sections.
- **Video:** `Screen.SetResolution`, `QualitySettings.SetQualityLevel(clamped)`, `QualitySettings.vSyncCount`, `Application.targetFrameRate`, `fx.SetQuality`.
- **Audio:** `audio.ApplyVolumes(SettingsMapping.ToVolumes(settings.Current.Audio))`.
- **Controls:** `input.ApplyBindingOverrides(json)`, plus `camera.ApplySettings` for camera options.
- **Gameplay:** `save.Autosave` interval.
- **Accessibility:** `ui.ApplyAccessibility(...)` (text-scale index mapped per SPEC-02 Q9), plus the camera shake scale.

### 5.7 Fleet, player ship spawn and write-back (`PlayerShipSpawner`, `FleetWriteback`)

- **`PlayerShipSpawner : ISceneReady`** (order 200), World only:
  1. `rec = Fleet.Flagship`. If null, `GameLog.Error` and complete immediately; the HUD shows "no ship".
  2. `rec.StructureHandle = registry.Register(rec.Structure)`.
  3. `pos = MathD.ToLocal(Galaxy.PlayerLocation.PositionM, FrameOrigin.OriginInStarSystem)` (FrameOrigin was set by the Galaxy hook at order 100). In the sandbox: `(0,0,0)`.
  4. `spawner.Spawn(new ShipSpawnArgs { Structure = rec.Structure, Position = pos, Rotation = Planar.YawRotation(Galaxy.PlayerLocation.YawRad), IsPlayer = true, Allegiance = Allegiance.Player, PersistentId = rec.PersistentId })`.
  5. Complete when the root exists, `GridSpawning` is disabled and `PhysicsCollider` is present, polled each frame.
- **`FleetWriteback : ISessionWriteback`** (order 100). If a player root exists:
  1. `GridStructureSync.WriteBack(em, root, registry)`;
  2. `galaxy.CapturePlayerLocation()`.

  It runs before every save capture and on World exit.
- **`SimClockWriteback : ISessionWriteback`** (order 0). If a `SimClock` singleton exists in the World, `session.UniversalTime = SimClock.UniversalTime`.
- **`ShipSpawnService.Spawn(args)`**:
  1. `id = args.PersistentId != 0 ? args.PersistentId : ids.Allocate()`.
  2. `p = new GridSpawnParams { Pose = new RigidTransform(yawOnly(args.Rotation), args.Position), LinearVelocity = args.LinearVelocity, Kind = GridKind.Ship, Motion = GridMotion.Dynamic, Flags = args.IsPlayer ? WriteBack : 0, Profile = args.IsPlayer ? GridProfile.PlayerShip : args.WithAi ? GridProfile.NpcShip : GridProfile.Dummy, PersistentId = id }` (the flying profiles imply `PlanarInertia`, SPEC-10 §5.13). Here `yawOnly(q) = Planar.YawRotation(Planar.YawFromForward(math.mul(q, new float3(0, 0, 1))))`.
  3. `root = args.Structure != null ? GridSpawnApi.RequestSpawn(em, registry, registry.Register(args.Structure), p) : GridSpawnApi.RequestSpawnBlueprint(em, catalog.Get<ShipBlueprintDefinition>(args.Blueprint).Hash, p)`.
  4. Add the requester values on `root` with the main-thread `EntityManager` (allowed: `ShipSpawnService` is never called inside a system update); every other gameplay component comes from the profile at conversion (ADR-0015):
     - `em.AddComponentData(root, args.Allegiance)` (SPEC-21; never part of a profile);
     - if `!args.IsPlayer`: `em.AddSharedComponent(root, new StarSystemMember { Id = galaxy.State.CurrentStarSystem })`;
     - if `args.WithAi`: `em.AddComponentData(root, new AiBrain { ProfileIndex = AiSetup.ProfileIndexOf(catalog, args.AiProfile) })` (SPEC-23 completes it on its first update).
  5. Record `id → root` in a `Dictionary<ulong, Entity>` maintained by an `IGridSpawnHook`. Entries are removed on despawn, detected when `TryGetRoot` finds a dead entity.
  6. Return `id`.

  Bricks and functional entities receive `StarSystemMember` through SPEC-31's spawn hook (SPEC-10 §10 Q7).
- **`IShipSpawner.Spawn(in GalaxyShipSpawnArgs g)`** (SPEC-31 §4.4): build `ShipSpawnArgs { Blueprint = g.Blueprint, Position = g.Position, Rotation = g.Rotation, LinearVelocity = g.LinearVelocity, Allegiance = g.Allegiance, WithAi = g.WithAi, AiProfile = g.AiProfile, IsPlayer = false, PersistentId = 0 }` and run the steps above, except that step 4 uses `StarSystemMember { Id = g.StarSystem }` instead of the current star system. If `g.Encounter.Value != 0`, the caller (SPEC-33) adds `EncounterTag` itself. Returns `new PersistentIdValue(id.Value)`. `TryGetRoot(ulong)` and `Despawn(ulong)` forward to the `PersistentId` overloads.

### 5.8 ECS systems owned here

**`SimClockAdvanceSystem`** (`Game.App/Systems`, Burst `ISystem`). It sits in `GameplayPostPhysicsGroup` with `OrderLast = true` and `[UpdateAfter(typeof(GameplayEndCommandBufferSystem))]`. Requires `GameplayActive` and `SimClock`.
- Main thread: `ref var c = ref SystemAPI.GetSingletonRW<SimClock>().ValueRW; c.Tick += 1; c.UniversalTime = c.SessionStartUniversalTime + c.Tick * SimTime.FixedTimestepD;`.

**`PersistentIdRefillSystem`** (`Game.App/Systems`, managed `SystemBase`, ending in neither `BridgeSystem` nor listed in §5 of the architecture, so this spec lists it there in FND-008's commit). It sits in `InitializationSystemGroup` with `[UpdateBefore(typeof(GameInitGroup))]`. It has no `RequireForUpdate<GameplayActive>`, so it also tops up between scenes. Each frame:
1. `EntityManager.CompleteDependencyBeforeRW<PersistentIdAllocator>()`, then `ref var a = ref SystemAPI.GetSingletonRW<PersistentIdAllocator>().ValueRW`. This initialization-time wait completes readers/writers of only the allocator, including last frame's `SplitSystem`; singleton access itself never completes them (Q12).
2. If `a.Next == a.End || a.End − a.Next < threshold` (Q13: empty always refills, including valid threshold 0) and `Services.TryGet<GameSession>(out s) && s.HasActiveSession`: grant a new block, `start = s.GrantBlock(blockSize)`.

   `GrantBlock` returns the old `NextPersistentId` and advances it by `blockSize`. The singleton then becomes `a.Next = start`, `a.End = start + blockSize`. Unused ids of the old block are dropped (Decision 9).
3. When no session is active, set `a = default` (empty), so Burst consumers defer.

Session start/load/end also clears outstanding ECS grants after that same component dependency completion, before changing the counter. FND-008 implements the allocation portion of `GameSession` with internal initialization/reset methods; FND-009 calls them from the complete session lifecycle. Its allocation round-trip test uses the exact SESS `UInt64` counter field; full SESS serialization and composition remain FND-009 (Q11).

### 5.9 Defeat flow

`DefeatWatcher`, in `Game.App`, subscribes `EventBus<ShipDestroyed>`. When it sees `IsPlayer == true` and the session is in the World:
1. Set `PlayerDefeated = true`; saving is now blocked with `PlayerDead`.
2. After `defeatDelaySeconds`, unscaled, push SPEC-61's `ConfirmDialog` with the title "Ship destroyed", the body "Your flagship was destroyed.", and two buttons:
   - "Load last save": `LoadGameAsync(save.CurrentSlot)`, or the newest autosave when there is no current slot;
   - "Main menu": `QuitToMainMenuAsync(false)`.
3. `PlayerDefeated` resets on `SessionReset`.

If no save exists, "Load last save" is disabled.

### 5.10 Save participants

**`SESS`** (order 10, version 1). Written in this order:
1. `WriteString(CommanderName)`;
2. `WriteUInt64(CampaignSeed)`;
3. `WriteDouble(UniversalTime)`;
4. `WriteDouble(PlaytimeSeconds)`;
5. `WriteUInt64(NextPersistentId)`;
6. `WriteBool(IsCreativeSession)`.

`Restore` reads the same fields in the same order. `fromVersion == 0` resets everything to new-game defaults.

**`FLET`** (order 70, version 1). Written in this order:
1. `WriteVarUInt32(count)`;
2. per ship, in ascending `PersistentId` order: `WriteUInt64(PersistentId)`, `WriteContentId(SourceBlueprint)`, `WriteString(Name)`, `WriteBool(isFlagship)`, `WriteDeferred(VoxsSnapshotBlock.Capture(source(Structure), names))`.

`Restore`:
1. `Fleet.ResetToEmpty()`;
2. read the records, decoding each structure with `VoxsCodec.TryDecode` and the SPEC-10 adapter;
3. a ship whose structure fails to decode is rebuilt from its `SourceBlueprint` (`GameLog.Warn`). If that is also missing, the ship is dropped, unless it is the flagship, which is replaced with `startBlueprint` and its id kept;
4. before publishing the records, reserve every valid restored fleet id through GameSession (Q22);
5. exactly one flagship is enforced: the first record flagged as flagship wins; if none is flagged, the lowest id.

## 6. Public API

| API | Preconditions | Postconditions / errors |
|---|---|---|
| `IGameFlow.*` (on `GameSession`) | Main thread | §5.4.3; never throws; returns `FlowResult` |
| `SceneFlow.StartupAsync` | Called once by `GameRoot.Start` | A second call returns `NotAllowed` |
| `SceneFlow.EnterSandboxAsync` | Editor or development build | `NotAllowed` in release |
| `IPersistentIdAllocator.Allocate` | Main thread | Unique, ≥ 1; `InvalidOperationException` on overflow |
| `ShipSpawnService.Spawn` (and `IShipSpawner.Spawn`) | Main thread, outside the fixed step, no `SystemAPI`/`Entities` iteration in progress, `GameplayActive` exists. `GalaxyFrameTicker` callbacks qualify (they run from `GalaxyServicesBridgeSystem.OnUpdate` with no iteration open) | Returns the id; the grid appears after the next `GridSpawnSystem` update; unknown blueprint → `PersistentId(0)` + `GameLog.Error` |
| `FleetModel.ReplaceStructure` | The ship is not currently spawned (`StructureHandle == -1`) | `InvalidOperationException` otherwise (SPEC-51 commits happen in the Lab) |
| `PauseService.Acquire/Release` | Main thread | §5.6 |
| `SettingsService.Apply` | Main thread | §5.6; publishes `SettingsChanged` only for changed sections |

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| Play pressed in `World` | Dev quick start: identical path to a new game (scene reloaded once) |
| Play pressed twice without domain reload | `GameRoot.Instance` reset by `SubsystemRegistration`; a fresh World; no services leaked (FND-014) |
| `ContentDatabase` has validation errors | Boot throws `ContentValidationException` with the report; Play Mode stops in the Editor |
| Load requested during a transition | `Busy` |
| Load requested while paused (pause menu) | Allowed; the pause token of the menu is released when the navigator clears screens on `SetRoot(Loading)`; the transition holds its own token (R28) |
| Save requested while in combat | `Blocked` (`InCombat`); UI shows the reason |
| A ready hook throws | Recovery to MainMenu (§5.4.1 step 7); notification |
| Teardown leaves entities after 10 frames | Error log naming archetypes; destroyed again; transition continues |
| Player ship destroyed during a transition | Ignored (`DefeatWatcher` checks `CurrentScene == World && !IsTransitioning`) |
| Quit while saving | SPEC-05's quit hook waits for the save (≤ 10 s) |
| `settings.json` corrupt | Defaults, file renamed, warning |
| Fleet flagship missing after load | Replaced by `startBlueprint` with the old id; warning in the load report |
| Two `GameConfigDefinition`s | FND-E003 at validation; boot fails |
| Persistent id counter overflow | `InvalidOperationException` (unreachable in practice: 2⁶⁴) |

## 8. Performance budget

| Gate | Target | Measured by |
|---|---|---|
| Enter Play Mode to MainMenu ready (domain reload off) | ≤ 1 s (06 iteration gate) | `BootPerfTests.EnterPlayMode_ToMenu` (EditMode `EnterPlayMode` instruction) |
| Load a typical save to playable | ≤ 2 s (06 save gate) | `LoadFlowTests.TypicalSave_Under2s` |
| Teardown of 200,000 entities | ≤ 50 ms main thread (once per transition, behind the loading screen) | `SceneFlowTests.Teardown_200k` |
| `GameRoot.Update` per frame | ≤ 0.05 ms, 0 B allocated | `GameRootTests.Update_NoAlloc` |
| `PersistentIdRefillSystem` | ≤ 0.01 ms when no refill | profiler marker `PersistentIdRefillSystem.Check` |

**Scale notes:**
- Teardown is O(entities) once per transition, using `NativeHashSet` membership.
- Ready hooks are polled once per frame, which costs O(hooks).
- No per-frame allocation anywhere in this spec. `Awaitable` continuations are allocated only per transition.

## 9. Tasks

### FND-001 — Public contract types
- **Milestone:** M0
- **Tier:** standard — transcribes the exact code of §4
- **Depends on:** `CORE-001`, `DATA-001`
- **Do:** Create every public type of §4 with the stated namespaces and files. Bodies are `throw new System.NotImplementedException()` except trivial getters. Types from modules not yet created (`VoxelStructure`, `GalaxyState`, `UIRoot`, `Allegiance`, `IRootScreenSet`) are referenced only from the files of the later tasks that need them. FND-001 creates `WorldSceneActive`, `StaticStateOkAttribute`, `GameConfigDefinition`, `GameRootConfig` (with only the fields whose types exist), `SceneFlowState`, `SandboxArgs`, `ShipSpawnArgs` (fields typed as they exist; `Allegiance` added by FND-012) and empty shells of `GameRoot`, `GameComposition`, `BootSingletons`, `BootSingleton`. Update architecture §4.3 with `WorldSceneActive` and add `PersistentIdRefillSystem` and `SimClockAdvanceSystem` to the §5 tree.
- **Files:** `Assets/Game/Core/Components/WorldSceneActive.cs`; `Assets/Game/Core/StaticStateOkAttribute.cs`; `Assets/Game/Data/Definitions/Config/GameConfigDefinition.cs`; `Assets/Game/App/{Boot,Flow,Session,Spawning}/*.cs`; `docs/01-architecture.md`
- **Acceptance criteria:**
  - [ ] `tools/compile.sh` clean; `AssemblyRulesTests` pass.
  - [ ] `GameConfigDefinition` has the fields and ranges of §4.2.
- **Tests:** `FoundationContractTests.WorldSceneActive_IsTag`, `.GameConfig_Defaults`.

### FND-002 — Bootstrap, GameRoot and explicit World creation
- **Milestone:** M0
- **Tier:** frontier — F1: every play session starts here; ordering with Entities bootstrap
- **Depends on:** `FND-001`
- **Do:**
  1. Implement `Bootstrap` and the `GameRoot` lifecycle (§5.1).
  2. Create `Assets/Game/App/Resources/GameRoot.prefab` with children `UI` (`UIDocument` + `PanelSettings` from SPEC-60, or an empty `UIDocument` until UI-003), `AudioRoot` and `CameraRoot` (empty transforms).
  3. Extend `ProjectSetup` to add `UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_RUNTIME_WORLD` to the Standalone scripting defines, and to `Check()` it.
  4. `GameComposition.Build` initially runs steps 1, 5, 7 and 15 (the others are placeholders).
- **Files:** `Assets/Game/App/Boot/{Bootstrap,GameRoot,GameComposition}.cs`; `Assets/Game/App/Resources/GameRoot.prefab`; `Assets/Game/Editor/ProjectSetup.cs`; `Assets/Game/Tests/PlayMode/Boot/BootTests.cs`
- **Acceptance criteria:**
  - [ ] Pressing Play in an empty scene creates exactly one `GameRoot` and one `Default World`.
  - [ ] `ProjectSettingsGuardTests` fails when the define is removed.
  - [ ] Exiting Play Mode disposes the World and leaves `GameRoot.Instance == null`.
- **Tests:** `BootTests.OneGameRoot_OneWorld`, `.ExitPlay_DisposesWorld`; `ProjectSettingsGuardTests` (existing) extended.

### FND-003 — PauseService
- **Milestone:** M0
- **Tier:** standard — algorithm fully given in §5.6
- **Depends on:** `FND-001`
- **Do:**
  1. Implement `PauseService` (§5.6).
  2. Add a `ConventionsTests` rule: `Time.timeScale =` appears only in `PauseService.cs`.
- **Files:** `Assets/Game/App/Services/PauseService.cs`; `Assets/Game/Tests/EditMode/App/PauseServiceTests.cs`; `Assets/Game/Tests/EditMode/Project/ConventionsTests.cs`
- **Acceptance criteria:**
  - [ ] Two tokens: `IsPaused` stays true until both are released.
  - [ ] Releasing a token twice is a no-op.
  - [ ] `PauseStateChanged` is published once per transition of `IsPaused`.
  - [ ] `timeScale` is restored to 1 on `Dispose`.
- **Tests:** `PauseServiceTests.RefCounted`, `.DoubleRelease_NoOp`, `.PublishesOnTransitionsOnly`, `.Dispose_RestoresTimeScale`.

### FND-004 — SettingsService and SettingsApplier
- **Milestone:** M0
- **Tier:** standard — file I/O, DTO and apply table fully given in §5.6
- **Depends on:** `FND-001`, `PERS-001`
- **Do:** Implement `SettingsService`, `SettingsPaths` (§5.1), `SettingsMapping` (§5.2 rules), `GameSettingsDto` (one DTO per section, `version`), and `SettingsApplier`. Apply calls for consumers that do not exist yet are behind null checks on `Services.TryGet`. Wire steps 1 and 14.
- **Files:** `Assets/Game/App/Settings/{SettingsService,SettingsPaths,SettingsMapping,GameSettingsDto,SettingsApplier}.cs`; `Assets/Game/Tests/EditMode/App/SettingsServiceTests.cs`
- **Acceptance criteria:**
  - [ ] Round trip: defaults → change every field → save → load into a new service → `ValueEquals` for every section.
  - [ ] A corrupt file yields defaults, is renamed to `settings.broken.json`, and produces one warning.
  - [ ] `Apply` with no actual change publishes nothing and does not schedule a save.
  - [ ] The debounce writes once for 10 changes within 0.5 s.
- **Tests:** `SettingsServiceTests.RoundTrip`, `.Corrupt_FallsBack`, `.NoChange_NoEvent`, `.Debounce_SingleWrite` (temp directory).

### FND-005 — Composition root and BootSingletons
- **Milestone:** M0
- **Tier:** frontier — F1/F5: construction order and lifetime of every service
- **Depends on:** `FND-002`, `FND-003`, `FND-004`, `SPEC-02`, `SPEC-04`
- **Do:**
  1. Implement `GameComposition.Build` and `Dispose` with every step of §5.2 whose type exists at M0 (1, 2 without the voxel lines, 3 when INP is done, 5, 6, 7, 8, 14, 15, 16), plus the placeholder comment lines for the rest. Resolve `savePaths`/`settingsPaths` through `GameRootTestHooks` as in §5.1 (create `Assets/Game/App/Testing/GameRootTestHooks.cs` with the SPEC-71 §4.4 shape if TST-003 has not yet).
  2. Implement `BootSingletons.CreateAll` with its first rows.
  3. Add `GameConfigValidator` and `config_game.asset`.
- **Files:** `Assets/Game/App/Boot/{GameComposition,BootSingletons}.cs`; `Assets/Game/App/Testing/GameRootTestHooks.cs`; `Assets/Game/Editor/Content/Validators/GameConfigValidator.cs`; `Assets/Game/Content/Config/config_game.asset`; `Assets/Game/Tests/PlayMode/Boot/CompositionTests.cs`
- **Acceptance criteria:**
  - [ ] Services are constructed in §5.2 order, verified by a test hook that records construction order, and disposed in reverse.
  - [ ] A throwing step disposes earlier services and stops boot with one error.
  - [ ] `BootSingletons` entities carry `BootSingleton`.
  - [ ] With `GameRootTestHooks.SaveRoot` and `.SettingsPath` set to a temp folder, a boot writes nothing under `Application.persistentDataPath`.
- **Tests:** `CompositionTests.Order_AndReverseDispose`, `.ThrowingStep_DisposesEarlier`, `.BootSingletons_Tagged`, `.TestHooks_RedirectPaths`.

### FND-006 — SceneFlow transitions and teardown
- **Milestone:** M0
- **Tier:** frontier — F5/F1: scene lifecycle, entity teardown and recovery across all modules
- **Depends on:** `FND-005`
- **Do:** Implement `SceneFlow` (§5.4.1, §5.4.5, §5.4.6, and recovery), creation and destruction of `GameplayActive`/`SimClock`/`WorldSceneActive`, the hook registries, and `SnapshotBootEntities`. Until UI-003 exists, root-screen calls are skipped when `screens == null`.
- **Files:** `Assets/Game/App/Flow/{SceneFlow,GameSceneNames}.cs`; `Assets/Game/Tests/PlayMode/Boot/SceneFlowTests.cs`
- **Acceptance criteria:**
  - [ ] MainMenu → World → MainMenu → World: after each exit, no entity outside the boot set remains, and `GameplayActive` exists only while in World.
  - [ ] A second transition request during a transition returns `Busy`.
  - [ ] A ready hook that throws ends in MainMenu with `SceneTransitionFinished(success: false)`.
  - [ ] A ready hook that never completes triggers recovery after the timeout (test timeout set to 1 s).
  - [ ] An entity created during World (any archetype) is gone after exit.
  - [ ] Teardown of 200,000 entities takes ≤ 50 ms.
- **Tests:** `SceneFlowTests.RoundTrip_NoLeftovers`, `.Busy_DuringTransition`, `.ThrowingHook_Recovers`, `.TimeoutHook_Recovers`, `.Teardown_200k` (Performance).

### FND-007 — SimClock lifecycle and SimClockAdvanceSystem
- **Milestone:** M0
- **Tier:** frontier — F1: the tick every deterministic system reads
- **Depends on:** `FND-006`
- **Do:** Implement `SimClockAdvanceSystem` (§5.8) and `SimClockWriteback` (§5.7), plus SimClock creation in World and Lab (§5.4.1 step 5).
- **Files:** `Assets/Game/App/Systems/SimClockAdvanceSystem.cs`; `Assets/Game/App/Session/SimClockWriteback.cs`; `Assets/Game/Tests/EditMode/App/SimClockTests.cs`
- **Acceptance criteria:**
  - [ ] In a test world stepping 120 fixed ticks, `Tick == 120` and `UniversalTime == start + 2.0` exactly.
  - [ ] A system ordered before `SimClockAdvanceSystem` reads `Tick == N` during tick N.
  - [ ] Paused (`timeScale = 0`), no tick advances.
  - [ ] Lab ticks never change `GameSession.UniversalTime`.
- **Tests:** `SimClockTests.Advances_Exactly`, `.ReadOrder`, `.Paused_NoAdvance`, `.Lab_DoesNotWriteBack`.

### FND-008 — PersistentId allocation and refill
- **Milestone:** M1
- **Tier:** frontier — F2/F1: main-thread refill of a singleton that Burst jobs consume
- **Depends on:** `FND-007`
- **Do:**
  1. Implement `GameSession.Allocate` and `GrantBlock`.
  2. Implement `PersistentIdRefillSystem` (§5.8).
  3. Add the `PersistentIdAllocator` row to `BootSingletons`.
- **Files:** `Assets/Game/App/Systems/PersistentIdRefillSystem.cs`; `Assets/Game/App/Session/GameSession.cs` (allocation part); `Assets/Game/Tests/EditMode/App/PersistentIdTests.cs`
- **Acceptance criteria:**
  - [x] 10,000 managed and 10,000 singleton allocations interleaved yield 20,000 unique ids.
  - [x] After save and load, new ids are greater than every id issued before the save.
  - [x] With no session, the singleton is empty.
- **Tests:** `PersistentIdTests.Unique_MixedSources`, `.AfterLoad_Monotonic`, `.NoSession_Empty`.

### FND-009 — GameSession: new game, end, IGameFlow, ISaveContext, SESS
- **Milestone:** M1
- **Tier:** frontier — F5: orchestrates save, galaxy, models and scene flow
- **Depends on:** `FND-006`, `FND-008`, `PERS-001`
- **Do:** Implement `GameSession` (§5.4.3, §5.5) except the fleet parts (FND-010), `SessionSaveParticipant` (§5.10), and the `ISaveContext` members. Galaxy calls are skipped while `GalaxyService` is not registered (M1); the new game then starts in the sandbox path.
- **Files:** `Assets/Game/App/Session/{GameSession,SessionSaveParticipant}.cs`; `Assets/Game/Tests/EditMode/App/{GameSessionTests,SessionSaveParticipantTests}.cs`
- **Acceptance criteria:**
  - [x] `NewGameAsync` resets every registered model once, runs initializers in order, and publishes exactly one `SessionReset(NewGame)`.
  - [x] `GetBlockReason` returns each reason under its condition (table-driven test).
  - [x] `SESS` round trip is byte-identical.
  - [x] `IGameFlow` calls from a disallowed scene return `NotAllowed` and never throw.
- **Tests:** `GameSessionTests.NewGame_ResetsAndInitializes`, `.BlockReasons_Table`, `.DisallowedScene_NotAllowed`; `SessionSaveParticipantTests.RoundTrip`.

### FND-010 — Fleet model, FLET and the player ship
- **Milestone:** M1
- **Tier:** frontier — F3/F5: write-back of damaged cells and identity across spawn, save and edit
- **Depends on:** `FND-009`, `VOX-001`, `PERS-001`
- **Do:** Implement `FleetModel` (including the `Changed` event, raised once per mutating call after the change), `FleetShipRecord`, `FleetSaveParticipant` (§5.10), `PlayerShipSpawner` (§5.7), and `FleetWriteback`.
- **Files:** `Assets/Game/App/Session/{FleetModel,FleetShipRecord,FleetSaveParticipant,PlayerShipSpawner,FleetWriteback}.cs`; `Assets/Game/Tests/EditMode/App/FleetTests.cs`; `Assets/Game/Tests/PlayMode/App/PlayerShipTests.cs`; `Assets/Game/Tests/EditMode/Persistence/LoadPipelineTests.cs` (ADR-0036 warning scope)
- **Acceptance criteria:**
  - [x] New game: the flagship spawns at the start location with its `PersistentId`, `PlayerShip` tag, and no `StarSystemMember`.
  - [x] Damage 50 cells → save → load: the respawned ship has the same 50 cells missing.
  - [x] An undecodable structure is rebuilt from its blueprint; the flagship is never lost.
  - [x] `FLET` round trip is byte-identical.
  - [x] `Add`, `Remove`, `ReplaceStructure` and `ResetToEmpty` each raise `Changed` exactly once; a refused `Remove` (flagship) raises nothing.
- **Tests:** `FleetTests.Changed_OncePerMutation`, `FleetTests.FlagshipRules`, `.FLET_RoundTrip`, `.CorruptStructure_Fallback`, `.RestoredFleet_RepairsSessionCounter`; `PlayerShipTests.Spawn_AtStart`, `.Damage_SurvivesSaveLoad`; `LoadPipelineTests.ParticipantWarning_ReachesReportAndCompletion_OnlyDuringRestore`, `.ParticipantWarning_FailedRestore_ClosesScopeAndAllowsNextLoad` (ADR-0036).

### FND-011 — Load flow end to end (R28)
- **Milestone:** M3
- **Tier:** frontier — F5: teardown, restore in place, regeneration and respawn ordering
- **Depends on:** `FND-010`, `SPEC-30`, `SPEC-31`
- **Do:** Implement `LoadGameAsync` / `RestoreFrom` (§5.5), and the PlayMode regression R28: load from the pause menu keeps the player position and the star system.
- **Files:** `Assets/Game/App/Session/GameSession.cs`; `Assets/Game/Tests/PlayMode/App/LoadFlowTests.cs`
- **Acceptance criteria:**
  - [ ] Save in star system S at position P → fly 3 km → open the pause menu → load. The player is in S within 0.01 m of P (`UniversePosition`), and the star system has the same body count.
  - [ ] `SessionReset(Loaded)` is published exactly once per load.
  - [ ] `GalaxyService.GenerationCount` increments by exactly 1 per load.
  - [ ] A typical save loads to playable in ≤ 2 s.
- **Tests:** `LoadFlowTests.R28_PauseMenuLoad_KeepsPosition`, `.SessionReset_Once`, `.Galaxy_GeneratedOnce`, `.TypicalSave_Under2s`.

### FND-012 — ShipSpawnService
- **Milestone:** M2
- **Tier:** standard — exact call sequence in §5.7
- **Depends on:** `FND-010`, `WPN-001`, `FLT-001`, `AI-001`
- **Do:** Implement `ShipSpawnService` (§5.7) including the id → root map via `IGridSpawnHook`, the explicit `IShipSpawner` members (§5.7) and step 8.3. Register it as both `ShipSpawnService` and `IShipSpawner`.
- **Files:** `Assets/Game/App/Spawning/{ShipSpawnService,ShipSpawnArgs}.cs`; `Assets/Game/Tests/PlayMode/App/ShipSpawnServiceTests.cs`
- **Acceptance criteria:**
  - [ ] An NPC spawn has `StarSystemMember` equal to the current star system; the player spawn has none.
  - [ ] Rotation with pitch and roll is reduced to yaw.
  - [ ] The request carries `Profile = PlayerShip` / `NpcShip` / `Dummy` for player / AI / non-AI spawns, `Allegiance` with the given value and (AI) `AiBrain.ProfileIndex`; after one `GridSpawnSystem` update the root has the profile's components (ADR-0015).
  - [ ] An unknown blueprint returns id 0 with one error.
  - [ ] `TryGetRoot` works after spawn and is false after despawn.
  - [ ] `IShipSpawner.Spawn` with `StarSystem = S` gives the root `StarSystemMember { Id = S }`; after one `GridSpawnSystem` update the ship has `ShipCommand`, `FlightState` and `PlanarConstraint` (from its profile).
- **Tests:** `ShipSpawnServiceTests.GalaxyArgs_UsesStarSystem`, `ShipSpawnServiceTests.Npc_HasStarSystemMember`, `.Player_NoMember`, `.YawOnly`, `.UnknownBlueprint`, `.RootLookup`.

### FND-013 — Blueprint Lab entry and exit
- **Milestone:** M3
- **Tier:** frontier — F5: pose capture, write-back, scene swap and respawn with the same identity (R16/R19)
- **Depends on:** `FND-011`, `EDT-001`
- **Do:** Implement `EnterBlueprintLabAsync` / `ExitBlueprintLabAsync` (§5.4.3) for both modes, and the Lab clock rule.
- **Files:** `Assets/Game/App/Session/GameSession.cs`; `Assets/Game/Tests/PlayMode/App/LabFlowTests.cs`
- **Acceptance criteria:**
  - [ ] FleetShip round trip without commit: the player returns within 1 m of the captured position with the same `PersistentId` and the same cells.
  - [ ] With a commit (structure replaced): same id, new cell count.
  - [ ] Campaign `UniversalTime` is unchanged by time spent in the Lab.
- **Tests:** `LabFlowTests.RoundTrip_NoCommit`, `.RoundTrip_Commit_SameId`, `.CampaignTime_Unchanged`.

### FND-014 — Static state audit and Play Mode re-entry
- **Milestone:** M0
- **Tier:** standard — reflection rules and test steps fully given here
- **Depends on:** `FND-006`
- **Do:**
  1. `StaticStateAuditTests`: for every type in the `Game.*` assemblies, every static field that is not `const` must satisfy one of three conditions:
     - its declaring type has a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` method;
     - it is `static readonly` of an immutable type (primitives, `string`, enums, `System.Type`, `Unity.Profiling.ProfilerMarker`, `Unity.Burst.SharedStatic<T>`, `ReadOnlyCollection<T>`, `ImmutableArray<T>`, or arrays of these that are never written outside the static constructor — arrays are listed only with `[StaticStateOk]`);
     - it carries `[StaticStateOk("reason")]`.

     Compiler-generated fields are skipped.
  2. `PlayModeReentryTests` (EditMode `[UnityTest]` using `EnterPlayMode`/`ExitPlayMode` instructions):
     1. enter, then wait for MainMenu ready;
     2. record the `Services` count and the number of `EventHub` channels;
     3. exit and enter again;
     4. assert the counts are the same, exactly one `GameRoot` exists, and `EventHub.PendingCount == 0`;
     5. measure enter-to-menu time (≤ 1 s).
- **Files:** `Assets/Game/Tests/EditMode/Project/StaticStateAuditTests.cs`; `Assets/Game/Tests/EditMode/Boot/PlayModeReentryTests.cs`
- **Acceptance criteria:**
  - [ ] Both tests pass on the M0 codebase.
  - [ ] Adding `static int x;` to any `Game.*` type makes the audit fail with the field name.
- **Tests:** `StaticStateAuditTests.AllStaticsReset`, `PlayModeReentryTests.SecondPlay_Clean`, `.EnterToMenu_Under1s` (Performance).

### FND-015 — Boot from any scene and the defeat flow
- **Milestone:** M3
- **Tier:** standard — behaviour fully given in §5.4.6 and §5.9
- **Depends on:** `FND-011`, `HUD-001`
- **Do:**
  1. Implement the `StartupAsync` mapping (§5.4.6) and `DefeatWatcher` (§5.9).
  2. Write PlayMode tests that start from each of the four scenes.
- **Files:** `Assets/Game/App/Flow/SceneFlow.cs`; `Assets/Game/App/Session/DefeatWatcher.cs`; `Assets/Game/Tests/PlayMode/Boot/AnySceneStartTests.cs`; `Assets/Game/Tests/PlayMode/App/DefeatTests.cs`
- **Acceptance criteria:**
  - [ ] Starting in each scene reaches its documented state within 10 s with zero errors.
  - [ ] Player ship destroyed → saving blocked with `PlayerDead`; after 3 s the dialog shows; "Load last save" loads.
- **Tests:** `AnySceneStartTests.Boot`, `.MainMenu`, `.World_DevQuickStart`, `.BlueprintLab_Creative`; `DefeatTests.PlayerDeath_DialogAndLoad`.

### FND-016 — Sandbox entry
- **Milestone:** M1
- **Tier:** standard — steps in §5.4.4
- **Depends on:** `FND-010`
- **Do:** Implement `EnterSandboxAsync` and `GameSession.BeginSandbox` (§5.4.4). The harness `sandbox` command (SPEC-70) calls it.
- **Files:** `Assets/Game/App/Flow/SceneFlow.cs`; `Assets/Game/App/Session/GameSession.cs`; `Assets/Game/Tests/PlayMode/App/SandboxTests.cs`; `Assets/Game/App/Session/PlayerShipSpawner.cs` (optional player); existing `PlayerShipTests` fixture reused
- **Acceptance criteria:**
  - [x] Sandbox with `SpawnPlayer = true`: the World is ready, the player ship is at the origin, and no `StarSystemMember` entities exist.
- **Tests:** `SandboxTests.Sandbox_SpawnsPlayer`.

### FND-017 — Imported ship flight checkpoint
- **Milestone:** M1
- **Tier:** frontier — F5: live integration of renderer, controls, physics, camera and save/load
- **Depends on:** `FND-010`, `REN-001`, `INP-001`, `DEV-001`
- **Do:** After the REN-011 and INP-005 implementations are done (before DEV-008, whose ready predicate needs Flight context), deliver the owner's 2026-10-12 playable-slice priority: use the existing imported Fighter as the temporary start blueprint (replacing Q7/Q19's four-cell ship_base default); apply DESIGN-GUIDE §4.1 lighting values to World; connect scene/pause input contexts while the future UI navigator is unavailable. Document the temporary context owner with an accepted ADR before changing that single-writer contract, and explicitly hand ownership to UI-004 when it lands. Exercise boot → imported player ship → real input → collision → save/load with the production renderer and camera, capture screenshots of the running game, and record any remaining limitations honestly. No new hulls or AI art.
- **Files:** `Assets/Game/Scenes/World.unity`; `Assets/Game/Content/Config/config_game.asset`; `Assets/Game/App/Boot/{GameComposition,SceneInputContextBridgeSystem,WorldLightingRig}.cs`; `Assets/Game/App/Flow/SceneFlow.cs` (ADR-0045 paused physics drain); `Assets/Game/Tests/PlayMode/App/PlayableShipTests.cs`; `docs/HANDOFF.md`; `docs/LEDGER.md`
- **Acceptance criteria:**
  - [x] Imported ship hull and functional meshes visibly render in the running World with the production camera and documented lighting.
  - [x] W/S thrust, A/D turn, Q/E strafe, braking and camera orbit/zoom act through InputService; menus/loading/pause neutralize flight input.
  - [x] A real collider interaction is observed; save/load restores the imported ship's voxel data and usable camera/controls. Record pose/velocity persistence limits until their owning world-state spec is implemented.
  - [x] Screenshots (or a short recording) and before/after input evidence are supplied to the owner; no renderer test image is presented as gameplay.
- **Tests:** `PlayableShipTests.Controls_MoveImportedShip`, `.SceneAndPause_NeutralizeFlight`, `.SaveLoad_RestoresImportedShip` (PlayMode); live GPU screenshots and collision observation supplement these functional tests.

### FND-018 — Nocturne flight environment and movement reference
- **Milestone:** M1
- **Tier:** frontier — F5: owner-directed live presentation integration
- **Depends on:** `FND-017`
- **Do:** Deliver Frank's requested Nocturne background, large placeholder storm planet and sparse nearby particle debris. Reuse the own-work procedural look-book sky/planet source and DESIGN-GUIDE §4, preserving the look book. Render a fully procedural, animated star/nebula sky: fixed hashed stars, slowly evolving smoke/filaments and dust lanes, Nocturne palette with lower-hemisphere features; no sky texture. VESPER-9 is a physical mesh sphere at a fixed finite world position, with a separately baked procedural surface and a separate atmosphere shell. Flying above/past it must change its apparent position/size. Use scene-owned presentation-only world-space particles for near parallax. Keep particles stationary until recycled outside the useful viewing region, fade recycling, and do not create physics/gameplay debris or a second simulation writer. Restore camera settings and destroy owned resources on scene exit. This staged sphere is replaced by SPEC-31 celestial streaming; it has no landing/collision or persistence.
- **Files:** `ArtSource/LookDev/export_nocturne_world.py`; `Assets/Game/Content/Environment/Nocturne/`; `Assets/Game/Presentation/Environment/NocturneFlightEnvironment.cs`; `Assets/Game/Presentation/Shaders/{SpatialDebris,NocturneAtmosphere,NocturneSky}.shader`; `Assets/Game/Scenes/World.unity`; `Assets/Game/Tests/PlayMode/App/NocturneEnvironmentTests.cs`
- **Acceptance criteria:**
  - [x] Actual World renders animated procedural Nocturne smoke/filaments and fixed stars and a large VESPER-9 placeholder from the existing procedural source, with the imported ship readable.
  - [x] Real thrust produces nearby debris parallax; stationary flight does not produce fake drift. Camera orbit remains usable.
  - [x] World exit/re-entry removes/recreates the presentation and restores camera state without leaked objects or errors.
  - [x] Inspected screenshots and real-input evidence are supplied to Frank.
- **Tests:** `NocturneEnvironmentTests.WorldEnvironment_EntersMovesAndCleansUp` (PlayMode); actual GPU screenshots before/after real W input. Compile and full EditMode/PlayMode suites. Performance advisory; no initial tuning required.

### FND-019 — Preserve Nocturne sky across direct new-game restarts
- **Milestone:** M1
- **Tier:** frontier — F5: scene teardown and persistent-camera presentation lifetime
- **Depends on:** `FND-018`, `FND-017`
- **Do:** Fix the loss of the persistent camera's Skybox override after NewGame is called while already in World. Keep the approved procedural material, brightness and star density. Make teardown/rebinding safe across consecutive World instances and preserve exit cleanup. Reproduce the direct restart path through the real session, not only World → menu → World with a frame between transitions.
- **Files:** `Assets/Game/Presentation/Environment/NocturneFlightEnvironment.cs`; `Assets/Game/Tests/PlayMode/App/NocturneEnvironmentTests.cs`
- **Acceptance criteria:**
  - [x] Repeated direct NewGame calls retain an enabled camera Skybox using Game/NocturneSky after settling; only one environment and particle system remain.
  - [x] Leaving World restores the camera's prior clear/sky state with no leftover environment objects.
  - [x] Live fresh launch and direct restart preserve the approved appearance; zero runtime errors.
- **Tests:** `NocturneEnvironmentTests.NewGameRestart_PreservesSky`; retain `WorldEnvironment_EntersMovesAndCleansUp`.

## 10. Open questions

| # | Question | Default if unanswered | Decider | Answer |
|---|---|---|---|---|
| 1 | Should a new game ask for a seed? | No; seed 0 → generated (§5.5); the dev console can pass one | Owner |  |
| 2 | Autosave when leaving the World for the main menu | Yes (Decision in §5.4.1 step 2.2) unless the player chose "quit without saving" | Owner |  |
| 3 | Defeat options | "Load last save" and "Main menu"; no respawn | Owner |  |
| 4 | Multiple ships in the fleet (M4) | `FleetModel` supports many; M3 uses only the flagship | Owner |  |
| 5 | Release builds starting in a non-Boot scene | Not supported; always Boot | Tech lead |  |
| 6 | FND-003 publishes `PauseStateChanged` through `EventBus<T>` (CORE-006), but its *Depends on* lists only FND-001, so it was ready before the bus exists (found 2026-10-11) | FND-003 waits for CORE-006 (status `blocked`); add `CORE-006` to its *Depends on* | Tech lead |  |
| 7 | §5.2 row 14 calls `SettingsApplier.ApplyAll`, but no row constructs the `SettingsApplier` instance that follows `SettingsChanged`; and FND-004 (row 1) and INP-002 (row 3) were done before `GameComposition` existed, so their lines had no file to go in (found in FND-002, 2026-10-11) | **Resolved in FND-002:** row 14 also constructs `new SettingsApplier(settings)` (pushed onto `DisposeStack`) before `ApplyAll`; FND-002 added rows 1, 3 and 14 for the finished tasks. Rows 7 and 15 stay placeholders until FND-006 creates `SceneFlow` | Tech lead |  |
| 8 | `FND-E001` makes a missing start blueprint an error, but no ship blueprint exists before VOX-010/MIG-008 (M1), and a runtime validation error fails boot (found in FND-005, 2026-10-11) | **Resolved in FND-005:** `GameConfigDefinition.Validate` reports `FND-E001` as a warning ("New Game is unavailable"); `GameConfigValidator` raises it to an error as soon as the database holds any `ShipBlueprintDefinition`, and enforces `FND-E003`. Also: `[StaticStateOk]` is field-only, so `GameRootTestHooks` carries it on each field; `SaveService` joins `DisposeStack` in PERS-005, when its `Dispose` exists | Tech lead |  |
| 9 | FND-006 against what exists at M0 (found in FND-006, 2026-10-11) | **Resolved in FND-006:** (a) `StartupAsync` leaves scenes that are not one of the four alone (PlayMode test scenes, look-dev scenes), so tests are not hijacked into the main menu. (b) Entities creates its `WorldTime` singleton lazily on the first `World.SetTime`, which happened after the boot snapshot, so every teardown destroyed it; `SnapshotBootEntities` now calls `world.SetTime(world.Time)` first (07-implementation-traps T2.12). (c) `SetRootScreens` and every root-screen and notification call wait for `IRootScreenSet` (HUD-002) and `UIRoot` (UI-003); the session calls (write-backs, `EndSession`, `UniversalTime`) wait for FND-009; the autosave calls for PERS-009. Until FND-009/FND-013, starting in World or BlueprintLab enters that scene directly | Tech lead |  |
| 10 | FND-014's audit rule skips compiler-generated fields, which would also skip the backing fields of static auto-properties (`static int Count { get; set; }`); and it lists only a few immutable types (found in FND-014, 2026-10-11) | **Resolved in FND-014:** auto-property backing fields are audited (annotate with `[field: StaticStateOk(...)]`); readonly unmanaged structs count as immutable; source-generated code (the Entities type registry, Burst direct-call stubs) is skipped; only runtime `Game.*` assemblies are audited (editor and test assemblies never live inside a Play session). Regexes, encodings, constant tables and stateless comparers carry `[StaticStateOk]` with their reason | Tech lead |  |
| 11 | FND-008 requires an allocation save/load test before FND-009 implements GameSession/SESS | Build only the allocation portion and test the exact saved counter field | Tech lead | Resolved 2026-10-11: internal world/config initializer and ResetPersistentIds feed FND-009's later lifecycle; the UInt64 counter round trip discards pre-load grants. FND-009 also registers the concrete GameSession required by the refill lookup, alongside its interfaces. Full SESS/container integration remains FND-009; no substitute save format. |
| 12 | §5.8 incorrectly says GetSingletonRW completes outstanding allocator jobs | Explicit component-specific dependency completion in initialization | Tech lead | Resolved 2026-10-11: installed Entities API says neither singleton read nor write waits. CompleteDependencyBeforeRW precedes refill and session-reset access; the mixed-source test deliberately leaves its Burst consumer scheduled across refill calls. |

| 13 | Config validation permits threshold 0, but a strict remaining < threshold test never refills an empty block | Always refill an empty block; zero disables only early refill | Tech lead | Resolved 2026-10-11: preserve the valid config range and test zero-threshold session activation in NoSession_Empty. |

| 14 | Direct timer/quit saves do not call GameSession.SaveGameAsync, so their snapshots miss writebacks | PrepareCapture in the save context | Tech lead | Resolved FND-009 / ADR-0034: invoke once after the final block check, before all participants; preparation failure leaves files untouched. |
| 15 | Async thumbnail readback cannot fit the completed-pixel DTO, and the M1 camera does not exist yet | Pending thumbnail state and explicit camera binding | Tech lead | Resolved FND-009 / ADR-0034: at most two frame yields before background writing; late/failed images omitted, producer still frees GPU resources. CAM-004 binds its camera through composition; headless/no-camera returns false. |
| 16 | Entering Exiting before requesting an autosave blocks that same save; quit-without-saving also requested one | Reserve the flow call and save while SceneFlow is still Idle | Tech lead | Resolved FND-009: operation reservation rejects competing IGameFlow calls, exit save precedes transition, and saveFirst=false skips it. Exceptions during exit writebacks also enter SceneFlow recovery. |
| 17 | Session dependencies do not include the later galaxy, fleet, combat-status or camera types | Implement current session/flow and explicit task-owned integration points | Tech lead | Resolved FND-009: GAL-008 adds galaxy state/calls; FND-010 fleet; AI-008 the cached combat-status read after AI-001; CAM-004 binds the camera. Existing ISaveBlocker covers save vetoes meanwhile; no parallel substitute models. Decision 8 takes precedence over the old restore paragraph: publish Loaded before galaxy notification. Missing SESS defaults use configured commander/seed, zero time, counter 1 and creative=false. |
| 18 | New game can be invoked before a profile picker has selected a profile | Create one using the configured commander label only when no active profile exists | Tech lead | Resolved FND-009: preserve an already selected profile; create under SaveService's configured root, including test path redirection. |

| 19 | FND-010 is M1, but its spawn path calls M2 FND-012 and M3 galaxy types | Use the existing grid request API for the M1 player path | Tech lead | Resolved FND-010: PlayerShipSpawner issues one WriteBack/PlayerShip-profile request at origin with identity yaw, retaining its root and exactly one registry handle on the fleet record. No StarSystemMember or unavailable Allegiance/AI types are fabricated. FND-012 replaces this request boundary with ShipSpawnService; GAL-008/SYS add the canonical location integration. Register player hooks only for active sessions; no-session diagnostic World entries have no player. |
| 20 | FleetShipRecord has a handle but no registry owner; exit must release the borrow before later edits | Bind the registry internally when registering; release on scene exit/dispose | Tech lead | Resolved FND-010: record owns its structure and registry borrow, never the registry itself. Player exit clears the root and releases the borrow after writebacks; recovery/disposal also releases it. ReplaceStructure rejects a live handle. |
| 21 | Recovered FLET structures must warn in the load report, but ISaveParticipant exposes no report | Scoped SaveService warning callback | Tech lead | Resolved FND-010 / ADR-0036: composition routes recoveries to the active report without changing ISaveParticipant or the FLET format. Missing/empty FLET uses the configured start blueprint and allocates a new id after SESS restoration; corrupt flagship payloads retain their valid saved id. |

| 22 | Missing/damaged SESS resets the allocation counter below ids surviving in FLET | Advance the counter past valid restored fleet ids before allocating any fallback | Tech lead | Resolved FND-010: the composition callback reserves through the highest valid FLET id, including a dropped record, while preserving any higher SESS counter. Repair clears outstanding grants and reports FND-W002 during participant restore. Missing, malformed and stale-low SESS cases are tested; no file-format or allocator-interface change. |

| 23 | Sandbox entry resets the session before old World writeback, and its optional player/session defaults are unspecified | Reset between scenes under the normal operation reservation | Tech lead | Resolved FND-016: save/teardown precede sandbox reset; creative session, configured start fleet, zero times and seed 0 mapped to 1; campaign initializers do not run. IsSandbox is runtime state for GAL-008 to clear rather than generate the galaxy. SpawnPlayer=false suppresses only the live grid; new game/load/end reset that flag. Release builds return NotAllowed; DEV-008 owns harness dispatch. |

| 24 | The first playable slice needs input before UI-004 and DEV-008 itself waits for Flight | Temporary sole scene context owner; execute FND-017 first | Tech lead | Resolved ADR-0044: current scene/pause sampled before player input; UI-004 removes fallback. Existing Fighter is the temporary starter; approved lights track camera yaw. FLET restores ship data, not pose/velocity until world-location persistence. |

| 25 | Paused teardown leaves package-owned temporal-coherence cleanup entities and a stale physics world | Dispatch the existing physics initialization and end-fixed ECB once at zero delta after destruction | Tech lead | Resolved ADR-0045: no simulation tick, unpause, manual component removal or broadphase mutation. Production-world/live restart exposed this during FND-017. |

| 26 | Owner needs spatial cues before SPEC-31 celestial streaming exists | Scene-owned distant placeholder and local cosmetic particles | Tech lead | Resolved FND-018 after owner correction: sky contains only stars/nebulae and, after a second explicit owner request, is procedural and animated rather than a baked panorama. The planet is a physical 1,400 m radius sphere at (1400, -2000, 1700), with its own texture and 3% atmosphere shell; ships can pass above it. No celestial identity, landing/collision or save data yet. Cosmetic particles and the finite sphere both supply parallax; SYS-011 replaces the staged presentation. |

| 27 | Direct World reload lets a new environment borrow the old scene’s Skybox while its Destroy is pending | Wait one frame before acquiring the persistent-camera override | Tech lead | Resolved FND-019: Start yields once, then captures/restores the prior camera state and creates the sky/debris. Destroy remains deferred and owned; no persistent extra component, per-frame repair, or material change. Regression failed on restart 1 before this fix. |

## 11. References

- Architecture §4 (scenes, services, ECS world), §7.3, §8; conventions §3; traps T6.1.
- Audit: `docs/reference/audit-2026-10/01-*`, `04-*` (04-B1, 04-B16), `LEDGER.md` R16, R19, R28.
- Unity: `DefaultWorldInitialization`, `UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_RUNTIME_WORLD`, `ScriptBehaviourUpdateOrder`, Enter Play Mode options, `Awaitable`.
