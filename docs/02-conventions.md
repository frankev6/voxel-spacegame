# 02 — Coding conventions

**Status: BINDING.** Code review rejects anything that breaks these rules. If a rule blocks you, write it up in `docs/LEDGER.md` → *Open questions*; do not work around it.

---

## 1. Files, namespaces, names

| Thing | Rule | Example |
|---|---|---|
| Namespace | Equals the assembly name, plus sub-folder if any | `Game.Voxel.Meshing` |
| File | One public type per file; file name = type name | `BrickMeshBuildSystem.cs` |
| Folders inside a module | `Components/`, `Systems/`, `Jobs/`, `Authoring/`, `Services/`, `Data/`, `Views/`, `ViewModels/` as needed. Don't invent others without a reason in the spec | — |
| Components | Nouns, **no** `Component` suffix | `VoxelGrid`, `ShipPower` |
| Tag components | End with `Tag` only when the name would otherwise be ambiguous | `PlayerShip`, `Hulk`, `GameplayActive` |
| Enableable components | States or adjectives | `Powered`, `BrickDirty` |
| Buffer elements | Named for **one** element | `BrickSlot`, `PowerConsumer` |
| Systems | `<Thing><Verb>System` or `<Verb><Thing>System`, always ending in `System` | `DamageResolveSystem` |
| Jobs | End with `Job` | `BrickMeshJob` |
| Events (ECS streams and managed) | Past-tense facts | `CellRemoved`, `StarSystemEntered` |
| Requests | End with `Request` | `GridSpawnRequest` |
| Services (managed) | End with `Service` | `SaveService` |
| ScriptableObject content | End with `Definition` | `BlockDefinition`, `ItemDefinition` |
| Blob structs | Short noun, the blob asset ends with `Blob` | `BlockDef`, `BlockDefsBlob` |
| View / ViewModel | `<Screen>View`, `<Screen>ViewModel` | `InventoryView`, `InventoryViewModel` |
| Content IDs | Lowercase `snake_case`, prefixed by category | `block_hull`, `ship_frigate`, `item_fuel`, `faction_vestari` |
| Fields | `camelCase` private, `PascalCase` public; no `m_` or `_` prefixes in new code, except `[SerializeField] private` fields which use `camelCase` | — |
| Constants | `PascalCase` | `BrickSize` |
| "System" | Always means an ECS system. Astronomical systems are "star systems", `StarSystem*` in code | `StarSystemId` |

## 2. ECS rules

1. **Unmanaged components only.** `class : IComponentData` is forbidden (deprecated in Entities 6.6). Reference Unity objects with `UnityObjectRef<T>`.
2. **`ISystem` + `[BurstCompile]`** for every system, on both the struct and `OnCreate`/`OnUpdate`/`OnDestroy`. `SystemBase` is allowed only for managed bridge systems in `Game.Presentation` and `Game.App` whose names end with `BridgeSystem` or which the architecture pipeline lists as managed (e.g. `PlayerInputSampleSystem`, which lives in `Game.Presentation` because `Game.Input` has no Entities reference).
3. **Every system has `[UpdateInGroup(...)]`** naming a group from [01-architecture.md §5](01-architecture.md#5-frame-pipeline-system-groups-and-ordering). Ordering inside a group uses `[UpdateBefore]`/`[UpdateAfter]` against named systems only when the spec requires it.
4. **Every gameplay system calls `state.RequireForUpdate<GameplayActive>()`** (or a narrower singleton) in `OnCreate`.
5. **Structural changes** (create/destroy entities, add/remove components, change shared components) happen only:
   - through an `EntityCommandBuffer` from `GameplayEndCommandBufferSystem` (fixed step), `BeginInitializationEntityCommandBufferSystem` (spawning), or `EndSimulationEntityCommandBufferSystem` (variable-rate groups);
   - or directly with `EntityManager` in the two systems the architecture marks *structural*: `GridSpawnSystem` (init group) and `StarSystemStreamingSystem` (WorldGroup). Nowhere else. In particular `SplitSystem` **records** into the `GameplayEndCommandBufferSystem` buffer.
   - Use batch APIs: `CreateEntity(archetype, NativeArray<Entity>)`, `Instantiate(prefab, NativeArray<Entity>)`, `DestroyEntity(query)`, `AddComponent(query, ...)`. Never loop `AddComponent`/`SetComponent` per entity on the main thread for more than 100 entities (architecture §11).
6. **Prefer enableable components over add/remove** for anything that toggles at runtime (powered, dirty, dead projectile).
7. **No sync points in hot paths.** `Complete()`, `EntityManager.GetComponentData` on main thread inside `OnUpdate`, and `CompleteDependency()` are forbidden in fixed-step systems unless the spec says so with a reason.
8. **Jobs:** `IJobEntity` or `IJobChunk` by default, `ScheduleParallel` when safe. Read-only lookups are `[ReadOnly]`. Never write the same component from two jobs in one tick (single-writer table, architecture §8).
9. **Events between stages** use the streams named in the architecture. Streams that change simulation state use `NativeStream`. Each producer writes at the foreach index `ChunkIndexInQuery` (or the parallel-for index), and the consumer reads indices in order. When several producers feed one consumer, the consumer sorts by `(Grid.Index, Source.Index, producer kind, sequence)`. `NativeQueue.ParallelWriter` is allowed **only** for presentation streams (`FxEvent`, `SfxEvent`) and managed-event queues. Rule of thumb: if the order could change a cell, an HP value or a velocity, it is not a `NativeQueue`.
10. **Randomness:** `RngStreams.For(seed, purpose, salt)`; for per-entity, per-tick randomness use `Random.CreateFromIndex((uint)StableHash.Combine(tick, entity.Index ^ salt))`. Never `UnityEngine.Random`, `System.Random`, or `Guid` for gameplay.
11. **Blob assets** are created once, stored in a cleanup component or a service that disposes them, and never rebuilt per frame.
12. **Native containers:** `Allocator.Temp` inside jobs/one frame, `Allocator.TempJob` for jobs that finish within 4 frames, `Allocator.Persistent` only in an owner that disposes in `OnDestroy`. Every `Persistent` allocation has a matching dispose; leaks fail the leak-detection test.
13. **Floats:** gameplay inside the local frame uses `float`/`float3`. Anything stored across star systems or in saves uses `double`/`double3` (`UniversePosition`).
14. **No managed types in ECS data.** No `string`, class, array or `ContentId` in components, buffers, blobs, jobs or events. Use `ContentHash`, dense runtime indices, `FixedString*Bytes` (debug names only) and `UnityObjectRef<T>`.
15. **Lookups and handles:** `ComponentLookup`/`BufferLookup`/type handles are created in `OnCreate` and `.Update(ref state)` at the top of every `OnUpdate`. Writing through a lookup from a parallel job needs `[NativeDisableParallelForRestriction]` **and** a written argument in the job comment for why no two threads touch the same entity.
16. **Command buffers:** `EntityCommandBuffer.ParallelWriter` sort key is `[ChunkIndexInQuery] int chunkIndex` (in `IJobEntity`) or `unfilteredChunkIndex` (in `IJobChunk`), never a constant or a thread index.
17. **Main-thread queries:** `EntityQuery.ToEntityArray`/`ToComponentDataArray`/`CalculateEntityCount` force completion of jobs writing those types. They are forbidden in fixed-step `OnUpdate` unless the spec says so with a reason; prefer `ToEntityListAsync` or a job.
18. **Profiling:** expensive steps get a `static readonly ProfilerMarker` named `<SystemName>.<Step>` (e.g. `DamageResolveSystem.Rasterize`). Burst jobs are profiled automatically by name.
19. **Physics:**
    - Collision filters come only from `PhysicsLayers` (SPEC-12).
    - Velocity changes are impulses (`PhysicsVelocity.ApplyLinearImpulse`/`ApplyAngularImpulse` with the body's `PhysicsMass`), never assignments (architecture §8).
    - Runtime-created collider blobs are owned and disposed through `DeferredDisposalSystem`, never `Dispose()`d while a physics step may still read them.

## 3. Managed (MonoBehaviour / plain C#) rules

1. **No mutable statics.** If you truly need one, reset it in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method in the same file.
2. **No singletons that create themselves**, no `FindObjectOfType`/`FindFirstObjectByType`, no `GameObject.Find`, no `SendMessage`, no `Resources.Load` (except `Bootstrap` loading `GameRoot`). Get dependencies from `Services` in `Awake`/constructor.
3. **MonoBehaviours are thin.** Logic goes into plain C# classes that tests can construct without a scene.
4. **Events:** subscribe in `OnEnable`, unsubscribe in `OnDisable`. Use `EventBus<T>`, not C# static events.
5. **Async:** `Awaitable` (Unity 6) for scene flow and I/O. No `async void` except Unity message methods, and those must catch and log.
6. **Allocation:** nothing allocates per frame in Update paths (no LINQ, no string formatting, no closures, no `foreach` over non-struct enumerators). Format strings only when a value changed.
7. **Serialization:** content uses ScriptableObjects; saves use the persistence container; JSON uses Newtonsoft with `MissingMemberHandling.Error`. Never `JsonUtility` for saves.

## 4. UI rules

1. UI Toolkit only. One `UIDocument` (owned by `UIRoot`). Screens are `VisualElement` trees loaded from UXML into layers.
2. **No inline styles** and **no hard-coded colours, sizes or fonts** in UXML or C#. Use classes from the theme and the variables in `Assets/Game/UI/Theme/tokens.uss` (see [design/DESIGN-SYSTEM.md](design/DESIGN-SYSTEM.md)).
3. Views bind to view models (`[CreateProperty]` + `INotifyBindablePropertyChanged`). Views never read ECS, services or session state directly.
4. Every screen declares `Context` (input context), `PausesSimulation` and `BlocksWorldPointer`. Close buttons call `Pop`; Esc always calls `UINavigator.Back()`.

## 5. Art and presentation rules (placeholder policy)

- Everything in `Assets/Game/Art/_Placeholder/` will be replaced after the owner approves the design system. **Never reference a placeholder asset from code by path or name.** Reference it from a content definition, a theme token, or a config ScriptableObject so it can be swapped in one place.
- Do not tune gameplay to a placeholder's look (for example, sizing a hitbox to a mesh). Gameplay sizes come from cells and definitions.
- Do not add new third-party art. New art is produced only under SPEC-81, after approval of SPEC-80.

## 6. Errors and logging

- Use `GameLog.Info/Warn/Error(category, message)` from `Game.Core` (it wraps `Debug.Log` and can be filtered per category). No bare `Debug.Log` in committed code.
- Never log per frame. If something can repeat, log once and count (`GameLog.WarnOnce(key, ...)`).
- Content and data errors fail loudly at import or boot (validator), never silently at runtime.
- Exceptions inside event handlers are caught by `EventBus` and logged; they must not stop other handlers.

## 7. Tests

- Every task ships with the tests its spec lists. EditMode tests for pure logic and ECS worlds created in the test (`World` + `SystemHandle`), PlayMode tests for scene flow and scenarios.
- Test names: `MethodOrBehaviour_Condition_ExpectedResult`.
- Tests are deterministic: fixed seeds, fixed tick counts, no wall-clock waits longer than needed.
- See [05-testing.md](05-testing.md) for commands and gates.

## 8. Comments and docs

- Comment *why*, not *what*. Public types get a one-line `/// <summary>` that names the spec section they implement, e.g. `/// Implements SPEC-13 §4.2 (blast rasterization).`
- `TODO` is allowed only with a task ID: `// TODO(DMG-014): hulk salvage`. A bare TODO fails review.

## 9. Git

- Branch per task: `task/<TASK-ID>-<short-slug>` (e.g. `task/VOX-003-brick-storage`).
- Commit message: `<TASK-ID>: <imperative summary>` (e.g. `VOX-003: Add brick cell storage and solid mask`).
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/`, `Build/`. Always commit `.meta` files with their assets.
- One task per branch. Rebase on `main` before merging.
