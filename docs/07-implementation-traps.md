# 07 — Implementation traps (wrong → right)

**Status: BINDING.** These are the mistakes that compile, pass a quick test, and then cost 10–100× performance, corrupt data, or break determinism weeks later. Each rule exists because of the [architecture](01-architecture.md) (§5.1 timing rules, §11 scalability rules) or the [conventions](02-conventions.md). Code review and frontier reviews ([04-workflow §7](04-workflow.md#7-task-tiers-which-model-may-take-which-task)) check every applicable item. If a spec seems to require a trap, the spec is wrong: log it in the ledger *Open questions*.

How to use this file: before you start a task, read the sections whose tags match your module. Before you mark it `review`, go through the checklist at the end.

---

## 1. ECS data and memory `[all ECS code]`

### T1.1 Managed data in components or jobs
- **Wrong:** `struct GridSpawnRequest : IComponentData { public ContentId Blueprint; }`. `ContentId` holds a `string`, so the component becomes managed and cannot be used from Burst.
- **Right:** `public ContentHash Blueprint;`, or a dense runtime index (`BlockIndex`). Use strings only in managed code, definitions and saves.

### T1.2 Big buffers inside the chunk
- **Wrong:** `public struct BrickCells : IBufferElementData { public Cell Value; }` with the default capacity. Each brick wastes 128 B of chunk space and fewer bricks fit per chunk.
- **Right:** `[InternalBufferCapacity(0)] public struct BrickCells : IBufferElementData { ... }`. Apply this to any buffer typically larger than 128 B.

### T1.3 High-cardinality shared components
- **Wrong:** a shared component whose value is unique per grid (`GridOwner`, a per-ship material, a per-brick `RenderMeshArray`). Every grid then gets its own chunks, often holding one entity each, and iteration and memory get 10–100× worse.
- **Right:** use shared components only for values with fewer than about 8 distinct values (`StarSystemMember`). Per-grid data goes in a normal component.

### T1.4 Per-entity structural changes
- **Wrong:**
  ```csharp
  for (int i = 0; i < bricks; i++)
  {
      var e = em.CreateEntity();
      em.AddComponent<Brick>(e);
      em.AddBuffer<BrickCells>(e);
      // ...
  }
  ```
  Every `AddComponent` moves the entity between chunks.
- **Right:**
  ```csharp
  var archetype = em.CreateArchetype(typeof(Brick), typeof(BrickCells), /* ... */);
  // once, cached in OnCreate
  em.CreateEntity(archetype, entities);  // NativeArray<Entity>
  // then fill Brick and BrickCells from a Burst job via ComponentLookup / BufferLookup
  ```

### T1.5 Hash-map iteration order used as simulation order
- **Wrong:** iterating a `NativeParallelHashMap`/`NativeHashMap` (or a managed `Dictionary` after removals) and applying effects in that order. The order depends on insertion history and thread timing.
- **Right:** copy the keys to a list, sort by a stable key, then apply. Hash maps are for lookup, never for ordering.

### T1.6 Forgetting `LinkedEntityGroup`
- **Wrong:** destroying a grid root with `DestroyEntity(root)` leaves its bricks and functional entities orphaned and still rendered.
- **Right:** `GridSpawnSystem` gives the root a `LinkedEntityGroup` (`[InternalBufferCapacity(0)]`) listing the root, every brick and every functional entity. Destroying the root then destroys the group. Star-system unload uses the shared-filter batch destroy.

### T1.7 Blob access by value
- **Wrong:** `var defs = blob.Value.Defs;`. This copies a `BlobArray` header out of the blob. It is invalid, and reads garbage.
- **Right:** `ref var defs = ref blob.Value.Defs;`. Always access blobs through `ref`. Blobs are built with `BlobBuilder(Allocator.Temp)` → `CreateBlobAssetReference<T>(Allocator.Persistent)` → `builder.Dispose()`, and the owner disposes the reference.

---

## 2. Jobs, dependencies and determinism `[Voxel, Simulation, Galaxy]`

### T2.1 Losing the dependency chain
- **Wrong:** `new MyJob{...}.Schedule();`. The job is not chained to `state.Dependency`, which causes safety errors or silent races with other systems.
- **Right:** `state.Dependency = new MyJob{...}.Schedule(state.Dependency);`. `IJobEntity.Schedule()` / `ScheduleParallel()` without arguments chain automatically, but only inside an `ISystem` `OnUpdate`.

### T2.2 Completing in the fixed step
- **Wrong:** `handle.Complete()`, `state.CompleteDependency()`, or `query.ToComponentDataArray(Allocator.Temp)` inside a fixed-step `OnUpdate`. Each one is a sync point that stalls every worker thread, up to 6 times per frame.
- **Right:** schedule the work and let the next system depend on it. Cache-building jobs check `handle.IsCompleted` in a later frame and install the results then.

### T2.3 Jobs that span frames reading live ECS memory
- **Wrong:** scheduling a mesher that reads `BufferLookup<BrickCells>` and finishes next frame. The next fixed step writes those cells, so you get a safety exception, or a forced `Complete()` hitch.
- **Right:** before scheduling, copy the brick's cells plus a 1-cell border from neighbours (18³ `Cell`s, 23 KB) into a `NativeArray` the build system owns (pooled, `Allocator.Persistent`). The job reads only that snapshot (architecture §5.1).

### T2.4 `NativeQueue` for simulation events
- **Wrong:** `NativeQueue<DamageEvent>.ParallelWriter` in the projectile job. The dequeue order changes run to run, so cell state differs, so the determinism gate fails.
- **Right:**
  ```csharp
  // NativeStream.ScheduleConstruct(out stream, forEachCount, ...)
  writer.BeginForEachIndex(chunkIndexInQuery);
  writer.Write(evt);
  // ...
  writer.EndForEachIndex();
  ```
  The consumer reads indices `0..forEachCount-1` in order. `NativeQueue` is only for Fx/Sfx and managed-event streams.

### T2.5 ECB sort keys
- **Wrong:** `ecb.ParallelWriter.Instantiate(0, prefab)`, or using a thread index as the sort key. Playback order is then nondeterministic.
- **Right:** `[ChunkIndexInQuery] int chunkIndex` in `IJobEntity` (or `unfilteredChunkIndex` in `IJobChunk`) is the sort key.

### T2.6 Parallel writes through lookups
- **Wrong:** `ComponentLookup<T>` written from `ScheduleParallel` with `[NativeDisableParallelForRestriction]` added "to make the error go away", while two threads can touch the same entity.
- **Right:** partition the work so each entity is written by exactly one index; for example, the resolver is parallel over **grids** and each grid's events are handled by one worker. Write the reason in a comment above the attribute. If you can't prove disjointness, schedule single-threaded.

### T2.7 Stale transforms inside the fixed step
- **Wrong:** reading a brick's or child's `LocalToWorld` in a fixed-step system. `TransformSystemGroup` runs after the fixed step, so the value is from the previous frame.
- **Right:** compute world-space values from the root's `LocalTransform` and grid-local coordinates: `math.transform(rootLocalTransform.ToMatrix(), localPos)`, or the equivalent `float3`/`quaternion` math.

### T2.8 Frame time in gameplay
- **Wrong:** `UnityEngine.Time.deltaTime` or `Time.time` in any simulation code.
- **Right:** `SystemAPI.Time.DeltaTime` inside the fixed group (which is exactly 1/60), or `SimClock.Tick` / `UniversalTime`. Cooldowns count ticks.

### T2.9 Lookups not updated
- **Wrong:** `lookup = state.GetComponentLookup<T>()` inside `OnUpdate` every frame, or created in `OnCreate` and never updated.
- **Right:** create it in `OnCreate`, then call `lookup.Update(ref state)` first thing in `OnUpdate`.

### T2.10 Allocator lifetimes
- **Wrong:** `Allocator.Temp` passed into a scheduled job; `TempJob` kept across frames; `Persistent` without a dispose.
- **Right:**
  - `Temp` for main-thread or inside-job scratch;
  - `TempJob` (or `state.WorldUpdateAllocator`) for per-tick job data, disposed with `.Dispose(state.Dependency)`;
  - `Persistent` only in an owner with an `OnDestroy` dispose.

  Run tests with `NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace`.

---

## 3. Rendering `[Voxel/REN, Presentation, Art]`

### T3.1 One `RenderMeshArray` per brick
- **Wrong:** `RenderMeshUtility.AddComponents(e, em, desc, new RenderMeshArray(mats, new[]{ brickMesh }), MaterialMeshInfo.FromRenderMeshArrayIndices(0,0))` for every brick. This creates a unique shared component per brick, so one entity per chunk, so the batching collapses.
- **Right:**
  1. Once at startup, register the shared materials: `BatchMaterialID mat = entitiesGraphicsSystem.RegisterMaterial(material)`.
  2. Create **one** prototype render entity with `RenderMeshUtility.AddComponents` and a single shared `RenderMeshArray`.
  3. Instantiate bricks from the prototype in a batch.
  4. Per brick, register its `Mesh` once: `BatchMeshID mesh = entitiesGraphicsSystem.RegisterMesh(brickMesh)`.
  5. Set `new MaterialMeshInfo(mat, mesh)` on the brick.

### T3.2 Re-registering meshes on every rebuild
- **Wrong:** creating a new `Mesh` and registering it each time a brick is remeshed. This leaks `BatchMeshID`s and causes GC churn.
- **Right:** keep the brick's `Mesh` and its `BatchMeshID`. On rebuild:
  1. `Mesh.ApplyAndDisposeWritableMeshData(dataArray, meshes, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers)`;
  2. set `mesh.bounds` and the submesh bounds explicitly;
  3. update `RenderBounds`.

  Unregister and destroy the mesh only when the brick dies, through `DeferredDisposalSystem`.

### T3.3 Fat vertices
- **Wrong:** `Vector3` position + `Vector3` normal + `Vector2` uv + `Color` per vertex, which is 48 B.
- **Right:** use the packed format from SPEC-11, at most 8 B per vertex. Brick-local positions fit in bytes (0..16). The normal is a face index, and the material/palette index is a byte. The shader decodes it. Use 16-bit indices: a brick never exceeds 65,535 vertices.

### T3.4 Material instances for variation
- **Wrong:** `new Material(baseMat)` per ship to tint it or show damage heat.
- **Right:** use a material-property component (`[MaterialProperty("_DamageTint")]`) or the palette. Materials are shared and registered once.

### T3.5 Meshing hidden faces across brick borders
- **Wrong:** treating everything outside the brick as air. Every brick border then gets faces, which doubles the triangles in solid regions and creates visible seams.
- **Right:** cull against the neighbour cells in the 18³ snapshot. When a neighbour brick changes along a shared face, both bricks are marked dirty (`BrickDirtyScheduleSystem`).

---

## 4. Physics `[Voxel/PHY, Simulation]`

### T4.1 Assigning velocity
- **Wrong:** `velocity.Linear = desired;` in propulsion or ramming code. This breaks momentum, torque from off-centre thrusters, and collisions.
- **Right:** `velocity.ApplyLinearImpulse(mass, impulse)` / `ApplyImpulse(mass, pos, rot, impulse, point)`. Only Unity Physics integrates (architecture §8).

### T4.2 Disposing colliders the physics step still reads
- **Wrong:** `oldCollider.Dispose()` immediately after assigning a new `PhysicsCollider`. `BuildPhysicsWorld`, or a query in flight, may still reference it, so you crash or read garbage.
- **Right:** push the old blob to `DeferredDisposalSystem` with a release frame at least 3 frames later.

### T4.3 Rebuilding the whole grid collider
- **Wrong:** recomputing one big compound of every box in the grid whenever a cell is removed.
- **Right:** use two levels (architecture §11.8).
  - Each brick has its own blob (a compound of merged boxes, at most 4,096 children).
  - The grid compound's children are the cached brick blobs.
  - A hit rebuilds one brick blob, plus the grid compound over the cached children: an O(bricks) BVH build, budgeted.
  - The SPEC-12 spike must confirm nested-compound support and the `ColliderKey` bit budget: 16 bits for bricks plus 12 bits for boxes is 28 of the 32 bits.

### T4.4 Collision events that never fire
- **Wrong:** writing an `ICollisionEventsJob` and getting no events.
- **Right:** collision events are raised only for colliders whose material has `CollisionResponsePolicy.CollideRaiseCollisionEvents`. Set this in the collider creation code from `PhysicsLayers`/SPEC-12, for grids that need ram damage.

### T4.5 Filters defined inline
- **Wrong:** `new CollisionFilter { BelongsTo = 1u << 3, CollidesWith = ~0u }` written in a weapon file.
- **Right:** `PhysicsLayers.ProjectileQuery` (SPEC-12). That is the only place filters are defined.

### T4.6 Physics queries from the wrong place
- **Wrong:** using `PhysicsWorldSingleton` from a system outside the fixed step, or before `PhysicsSystemGroup`, and expecting this tick's bodies.
- **Right:** casts that must see this tick's state run in `GameplayPostPhysicsGroup`. Get the world with `SystemAPI.GetSingleton<PhysicsWorldSingleton>()`, which registers the dependency.

---

## 5. Floating origin and coordinates `[Galaxy, Presentation]`

### T5.1 Moving things outside `OriginRebaseSystem`
- **Wrong:** a system that subtracts an offset from positions "to keep numbers small".
- **Right:** only `OriginRebaseSystem` shifts root transforms, and listeners react to `OriginShifted`: the camera, trails, and VFX particle buffers.

### T5.2 Storing local floats as persistent positions
- **Wrong:** saving `LocalTransform.Position`.
- **Right:** save `UniversePosition` (`double3`, star-system frame), and convert on load.

---

## 6. Managed code, UI and domain reload `[App, UI, Presentation, Narrative, Economy]`

### T6.1 Statics surviving Play Mode
- **Wrong:** `static List<X> cache = new();`. With domain reload off, it keeps the previous play session's data.
- **Right:** no mutable statics. If one is unavoidable, reset it in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method in the same file. `EventBus<T>` and `Services` already do this.

### T6.2 Per-frame UI allocation
- **Wrong:** `label.text = $"{hp}/{max}"` every frame; `root.Q<Label>("hp")` every frame; LINQ in view models; rebuilding list items.
- **Right:**
  - Query elements once, in the view's constructor or bind step.
  - Format only when the value changes (compare it with the cached previous value).
  - Use `ListView` with `makeItem`/`bindItem` (pooled).
  - The HUD reads one snapshot struct per frame written by `HudSyncSystem`.

### T6.3 Views reading game state
- **Wrong:** a view that calls `Services.Get<InventoryModel>()` or queries ECS.
- **Right:** views bind only to their view model. View models are fed by Presentation bridges or by services through events.

### T6.4 Event handler leaks
- **Wrong:** subscribing in `Awake` and never unsubscribing.
- **Right:** subscribe in `OnEnable` and unsubscribe in `OnDisable` (or in `Dispose` for plain classes).

### T6.5 Narrative polling
- **Wrong:** evaluating every quest condition every frame.
- **Right:** evaluate conditions when a fact they read changes. Each compiled condition knows its fact keys, and `FactChanged` triggers the re-evaluation. Interned integer fact keys mean no string comparisons.

---

## 7. Saves and content `[Persistence, Data, Editor]`

### T7.1 Saving runtime indices or entities
- **Wrong:** saving `BlockIndex`, `Entity`, `Entity.Index`, or a list position that depends on content order.
- **Right:** save content as `ContentId` strings in per-grid palettes, objects as `PersistentId`, generated bodies as `BodyId`.

### T7.2 Unversioned or lossy formats
- **Wrong:** `BinaryFormatter`, `JsonUtility`, or writing floats with `ToString()`.
- **Right:** use the SPEC-05 container: each chunk carries a 4-char key and a version, little-endian, floats written as raw bits, and every format change ships with a migration and a round-trip test.

### T7.3 Heavy work on the main thread at save time
- **Wrong:** encoding and compressing all grids inside `Capture`.
- **Right:** `Capture` copies native data into owned buffers (memcpy brick cell arrays) within the 5 ms budget. Encoding and compression run on a worker thread.

### T7.4 Silent content errors
- **Wrong:** `if (def == null) return;` when a referenced content ID is missing.
- **Right:** validators fail at import or boot with the asset path and field. At runtime a missing reference is a logged error with a defined fallback, never a silent skip.

---

## 8. Tests that lie `[Tests]`

### T8.1 Testing with safety checks off
- **Right:** EditMode and PlayMode tests run with Burst safety checks and leak detection on. Perf tests run with safety checks off (06 reference settings), and the test asserts that setting.

### T8.2 Determinism tests that can't fail
- **Wrong:** running the same scenario twice at the same frame rate in the same process and comparing.
- **Right:** run the scripted input at 30, 60 and 144 simulated FPS and compare cell-state hashes (SPEC-71). Also run with the job worker count set to 1 and to the maximum.

### T8.3 Wall-clock waits
- **Wrong:** `yield return new WaitForSeconds(2)` waiting for "the mesh to be ready".
- **Right:** step a fixed number of ticks or frames, or wait on an explicit condition with a frame timeout.

---

## Review checklist (copy into your PR / ledger note)

- [ ] T1: no managed data in ECS; big buffers have capacity 0; no high-cardinality shared components; batch structural changes; no hash-map order; `LinkedEntityGroup` set; blobs accessed by `ref`.
- [ ] T2: dependencies chained; no `Complete()` in the fixed step; cross-frame jobs use snapshots; `NativeStream` for simulation events; ECB sort keys from chunk index; parallel writes proven disjoint; no `LocalToWorld` or `Time.deltaTime` in simulation; lookups updated; allocators correct.
- [ ] T3: registered material/mesh IDs; meshes updated in place; packed vertices; no material instances; cross-brick culling.
- [ ] T4: impulses only; deferred collider disposal; two-level colliders; collision-event flags; `PhysicsLayers` only; queries post-physics.
- [ ] T5–T8: origin rules; saves by stable IDs; UI without per-frame allocation; condition evaluation on change; tests with safety on, real determinism.
