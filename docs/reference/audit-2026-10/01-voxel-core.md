# Audit 01: Voxel core (data model, construction, rendering, colliders, movement, scaling)

Auditor scope: `Assets/Game/Core/VoxelShip/**` (including `Asteroid/`), `Assets/Game/Core/Camera/PlayerShipCameraFollowSystem.cs`, `Assets/Game/Meshes/Voxel Meshes`, voxel materials/shaders, voxel prefabs, and `Assets/Game/Scenes/EntitiesSubscene.unity`. I also traced the voxel removal path through `Assets/Game/Core/Combat/{VoxelDestructionSystem,VoxelConnectivitySystem,ShipCollisionSystem,EntityLifetimeSystem,ShipHealthSystem,CombatManager}.cs`. The combat auditor owns those files. I cover them here only where they decide voxel lifetime.

Method: I read every file in scope in full. I checked usage by grepping `Assets/Game` (code, `.unity`, `.prefab`, `.asset`, `.mat`) and by matching script GUIDs from `.meta` files. I checked engine behaviour against the package sources in `Library/PackageCache` (Entities 6.6.0 `b4620332ec2d`, Unity Physics 6.6.0 `8d312bc11767`, Entities Graphics 6.6.0 `3e7b8c38d309`).

Each claim is tagged with one of these markers:
- **[V]**: verified in code or package source. A file:line reference is given.
- **[I]**: inference or estimate. The reasoning is given, but I did not run the game or profile it.

All paths are relative to the repo root.

---

## 0. TL;DR

**Top bugs**

1. **Multi-cell blocks are ignored at runtime.** The editor treats a block as several rotated cells, but the runtime treats it as one root cell. As a result, the connectivity check detaches voxels that the editor considered connected, and most of a large block's volume has no collider. Examples are the 3x3x2 thrusters and the 7-cell cross core. See B1. **[V] High**
2. **`VoxelPhysicsUtils.ResolveVoxel` computes the compound key bit count incorrectly.** Whenever the ship's child count is an exact power of two, every hit damages the wrong voxel (child index / 2). See B2. **[V] High**
3. **Compound collider blobs leak.** They are never disposed, either when the collider is rebuilt or when the ship is destroyed. See B3. **[V] High**
4. **Shield blocks can never work.** The shield bubble prefab reference is never baked, so `ShieldEmitterComponent` is never added. See B4. **[V] High**
5. **Debris vanishes when its ship dies.** Destroyed voxels stay in the ship's `LinkedEntityGroup`, so ship death deletes every debris piece still in flight. Ship death itself happens at 40% voxel loss. See B5. **[V] Medium-High**
6. **Voxels are scene-bound but ships are not.** Voxel and thruster entities are instantiated from a subscene prefab and inherit its `SceneTag`, so they are destroyed when `GalaxyMapScene` unloads. Ship master entities survive as invisible colliders. See B6. **[V mechanism / I impact] Medium-High**
7. Further bugs:
   - Collider rebuilds are starved under sustained fire (B7).
   - The ship blob cache key ignores rotation, colour, modules and unresolved block IDs (B8).
   - The baked mesh map relies on persisted `string.GetHashCode` and has no baker dependencies (B9).
   - Asteroid belts are not shifted by the floating origin and are never destroyed (B10).
   - Negative seeds collapse about 50% of asteroids into one look (B11).
   - Ship–asteroid ramming never deals damage (B12).

**Top dead code**

- `ShipVoxelBuffer`, `ShipDefinitionAuthoring`, `PlayerShipCameraFollowSystem` (100% commented out).
- `GameObjectSyncSystem` together with `ObjectTransformSync` and `GameDatabase.SyncTransformDictionary`.
- `ShipBlobReference`, `VoxelPrefabReference`.
- `ShipDataBlob.AllModuleSlots`, `ShipVoxel.Size` and `ShipVoxel.IsExposed`.
- `ShipData.GetOrCreateBlobAsset`, its finalizer, and the blob it holds.
- `ShipStatCalculator.CalculateShipStats`, `GetNeighborPositions` and `ComputedVoxelStats`.
- Four `VoxelVisualData` fields.
- `shipVoxel_cube.obj` and `shipVoxel_verticalslab.obj`.

Proof is in §4.

**Headline recommendation:** stop representing voxels as entities. The cost model in §5 estimates that one entity per voxel costs about 300 B CPU and 112 B GPU per voxel. It also forces a full transform, bounds and GPU upload of every voxel every frame, plus a main-thread O(N log N) collider rebuild on every hit. My estimate is that it cannot go far beyond about 20–50k live voxels in the scene.

The proposed model:
- **Storage:** a ship is a sparse grid of 16³ *bricks*, holding 2–4 bytes per cell.
- **Rendering:** each brick gets a Burst-generated culled-face or greedy mesh. Detailed "prop" blocks are GPU-instanced.
- **Physics:** each brick gets a greedy-box collider, combined into a nested compound that is rebuilt incrementally.
- **Hits:** hits resolve to cells by a ship-local grid lookup or DDA, not by compound child index.
- **Splitting:** islands are found with incremental brick-level connectivity and split into new ships.
- **Debris:** small debris is pooled GPU-instanced particles, not ECS physics entities.
- **Functional blocks:** only thrusters, weapons, shields and reactors remain entities.

At 1M voxels this drops CPU memory from about 300 MB to about 10–20 MB, and per-frame transform cost from O(voxels) to O(bricks). Migration path: §6.12.

---

## 1. Current architecture map

### 1.1 Data flow: saved data to rendered and physics ship

```
BlockItem SO (Resources/Items, BlockMesh, Health, BlockSize/VoxelShape, Modifiers)
   │  bake time: VoxelPrefabAuthoring.Baker  → RenderMeshArray(1 material, N meshes) + BlockMeshMapBlob(keyHash→meshIndex)
   │  run time : BlockDatabaseInitSystem → BlockBlobBuilder.CreateBlockDatabase → BlockDatabaseBlob (by runtimeId)
   ▼
ShipData (managed, JSON/YAML: List<VoxelData{string blockKey, string[] moduleSlots, int xyz, float rotXYZ, byte color, bool IsDestroyed}>)
   │  ShipData.PrebakeStats() (editor save) → GlobalStats, dims, ALSO builds an unused ShipData._blobAsset
   │  CombatManager.CreateShipEntity → GameDatabase.GetOrCreateShipBlob(hash) → ShipBlobBuilder.CreateBlob → ShipDataBlob
   ▼
ShipBuildRequest entity (+ managed ShipBuildRequestManaged{CombatShip})
   │  InitializationSystemGroup: ShipConstructionJobSystem (IJobEntity, one Execute per *request*, Complete() immediately)
   │     → ECB(BeginInitialization, i.e. played back NEXT frame):
   │        ship master entity + per voxel: Instantiate(VoxelPrefab) + ~8–15 commands + SphereCollider blob
   │        CompoundCollider.Create(all spheres) → PhysicsCollider on ship
   │  next frame: ECB playback → ShipConstructionLinkSystem links CombatShip.ShipEntity, registers GO sync
   ▼
Entities: Ship master (LocalTransform, PhysicsCollider(compound), PhysicsMass, PhysicsVelocity, ShipVoxelIndex[], LinkedEntityGroup[], ...)
          Voxel ×N (child via Parent: LocalTransform, LocalToWorld, ShipVoxel, VoxelHealth, MaterialMeshInfo, VoxelColorProperty, ...)
          Thruster visual ×T (child of thruster voxel, PostTransformMatrix animated)
   │  TransformSystemGroup recomputes every voxel LocalToWorld each frame (parent moved)
   │  Entities Graphics (BRG): per-voxel instance, per-instance culling, uploads changed matrices
   ▼
GPU
```

Evidence for each step:
- Bake **[V]**: `VoxelPrefabAuthoring.cs:61-159`.
- Runtime block DB **[V]**: `BlockDatabaseInitSystem.cs:19-81` and `BlockBlobBuilder.cs:21-200`.
- Blob cache **[V]**: `GameDatabase.cs:62-77`.
- Request **[V]**: `CombatManager.cs:74-104`.
- Construction **[V]**: `ShipConstructionJobSystem.cs:67-166, 186-588`.
- Link **[V]**: `ShipConstructionLinkSystem.cs:10-46`.

### 1.2 Entity layouts **[V]**

**Ship master** (`ShipConstructionJobSystem.cs:200-268, 390-437`):
- Components: `ShipMaster`, `ShipVoxelIndex` buffer, `ShipHealth` (equal to the intact voxel count), `ExplodeOnDeath`, `ShipMovementComponent`, `ShipInputComponent`, `PotentialTargetElement` buffer, `LocalTransform`, `LocalToWorld`, `PlayerTag` or `AIShipTag`, `AIShipNavigationComponent`, `LinkedEntityGroup`.
- Physics: `PhysicsCollider` (compound of spheres), `PhysicsMass` (X/Z inverse inertia zeroed), `PhysicsGraphicalSmoothing`, `PhysicsVelocity`, `PhysicsDamping`, `PhysicsGravityFactor`, `Simulate`, `PhysicsWorldIndex`.
- Gameplay: `ShipEnergyComponent` (disabled), `ConnectivityCheckState`.
- Added later: `ShipUIReference` (`ShipConstructionLinkSystem.cs:35`) and `Child` (by `ParentSystem`).

**Voxel**. From the baked prefab (`VoxelPrefabAuthoring.cs:42-158`): `VoxelPrefabTag`, `ShipVoxel`, `VoxelHealth`, `VoxelParentRef`, `RenderMeshArray` (shared), `MaterialMeshInfo`, `PerInstanceCullingTag`, `WorldToLocal_Tag`, chunk components `ChunkWorldRenderBounds` and `EntitiesGraphicsChunkInfo`, `RenderBounds`, `WorldRenderBounds`, `RenderFilterSettings` (shared), and `LocalTransform` plus `LocalToWorld` (Dynamic).

At instantiate time (`ShipConstructionJobSystem.cs:319-368, 440-586`) the construction job adds or sets:
- always: `Parent`, `VoxelColorProperty`;
- optionally: `ReactorComponent`, `CapacitorComponent`, `WeaponComponent` + `FireWeaponTag` + `Player/AIWeaponTag`, `MirrorComponent`/`MirrorTag`, `PortalComponent`, `ThrusterComponent`.

`ParentSystem` later adds `PreviousParent`.

The voxel prefab entity also carries the subscene `SceneTag` shared component, which every instance inherits (see B6).

**Thruster visual** (`ShipConstructionJobSystem.cs:566-585`): instance of `thruster_visual.prefab`, with `Parent` set to the thruster voxel, `PostTransformMatrix`, `ThrusterVisualTag` and `VoxelParentRef`.

**Shield bubble**: never created (B4).

### 1.3 Baking and subscene setup **[V]**

`Assets/Game/Scenes/EntitiesSubscene.unity` contains:
- `ShipVoxelEntityPrefab` (prefab instance, GUID `fbe3a25f…`): has `VoxelPrefabAuthoring` with `CommonMaterial` set to `MetallicShipMaterial`. The baker adds `Prefab` itself.
- `ShieldEntityPrefab` (GUID `9af04e30…`): has `ShieldBubblePrefabAuthoring`, plus a MeshRenderer using `semi_sphere_shield.obj` and `ShieldMat`.
- `ThrusterPrefabAuthoring` GameObject (`ThrusterVisualAuthoring`): references `Prefabs/Projectiles/thruster_visual.prefab`.
- `GlobalShipScale` GameObject (`GlobalShipScaleAuthoring`).
- `ProjectileDatabaseAuthoring` prefab instance.

The subscene is referenced only by `GalaxyMapScene.unity:210` with `AutoLoadScene: 1`.

`MetallicShipMaterial` uses `MetallicShipShader.shadergraph` (URP Lit). Its `_BaseColor` property is declared with `overrideHLSLDeclaration=True`, `hlslDeclarationOverride=3` (Hybrid Per Instance). So `[MaterialProperty("_BaseColor")] VoxelColorProperty` does work per instance **[V]** (parsed from the shadergraph JSON).

### 1.4 System ordering (voxel-relevant) **[V]** (from attributes)

| Group | System | Notes |
|---|---|---|
| Initialization (OrderFirst) | `BlockDatabaseInitSystem` (SystemBase) | One-shot. Waits for `GameDatabase.VisualData` and the voxel prefab, then sets `Enabled=false` (`:79-80`). |
| Initialization | `ShipConstructionJobSystem` → `ShipConstructionLinkSystem` | Construction writes to the **BeginInitialization** ECB (`:119-120`), so entities appear next frame. Link uses the EndInitialization ECB. |
| Initialization | `PhysicsSetup` (Combat) | Creates `PhysicsStep`. |
| Simulation → FixedStep | `ShipColliderRebuildSystem` [before `PhysicsSystemGroup`] | Main thread, `state.CompleteDependency()` every step (`:35`). |
| Simulation → FixedStep | `PhysicsSystemGroup` → `ShipConstraintSystem`, `ShipCollisionSystem_Managed` [after physics] | |
| Simulation (variable) | `ShipInputSystem` → `ShipMovementSystem` → `ThrusterVisualSystem` | Movement writes `PhysicsVelocity` *after* this frame's physics; it is consumed next fixed step. |
| Simulation | `AsteroidBeltCullingSystem` → `AsteroidLayoutJobSystem` → `AsteroidConstructionJobSystem` | Immediate ECB playback and main-thread structural changes. |
| Simulation | `VoxelConnectivitySystem` → `VoxelDestructionSystem` → `ShipHealthSystem` | Destruction writes to the **BeginSimulation** ECB (played back next frame). |
| Simulation | `EntityLifetimeSystem`, `ShipCollisionDamageSystem` | Destroys ships and applies voxel damage. |
| TransformSystemGroup | `GameObjectSyncSystem` (dead) before `LocalToWorldSystem`; `EcsToGameObjectSyncSystem` after it | GO sync calls `handle.Complete()` (`EcsToGameObjectSyncSystem.cs:91`). |
| Presentation | Entities Graphics | Per-voxel instances. |

### 1.5 Structural changes and sync points **[V]**

**Structural changes:**
- Construction: per voxel, `Instantiate` plus archetype moves for `Parent` and `VoxelColorProperty` (and special components). The prefab already owns `ShipVoxel`, `VoxelHealth`, `VoxelParentRef` and `LocalTransform`, so those are only value sets. `ParentSystem` adds `PreviousParent` and the ship's `Child` buffer later.
- Destruction: per destroyed voxel (`VoxelDestructionSystem.cs:290-342`), RemoveComponent `Parent`, then Add of the scrap set, `PhysicsCollider`, `PhysicsMass`, `PhysicsVelocity`, `PhysicsDamping`, `LifetimeComponent` and `ScrapTag`. That is several archetype moves per voxel. Each destruction also queues Add `ColliderRebuildRequired` on the ship. The rebuild later removes it on the main thread (`ShipColliderRebuildSystem.cs:73`).
- Asteroids: about 10 main-thread `EntityManager.Add*` calls per asteroid (`AsteroidConstructionJobSystem.cs:104-165`), capped at 8 asteroids per frame.

**Sync points:**
- `ShipConstructionJobSystem.cs:140` (`Dependency.Complete()` right after schedule).
- `ShipColliderRebuildSystem.cs:35` (every fixed step, even with nothing to rebuild).
- `EcsToGameObjectSyncSystem.cs:91`.
- `ShipCollisionDamageSystem` `CompleteDependency` (Combat).
- Every main-thread `EntityManager` structural change listed above.

### 1.6 Voxel removal flow **[V]**

1. Damage writes `VoxelHealth.Health`. Writers:
   - `EntityLifetimeSystem` (projectile hit or area, `:127, :245`)
   - `BeamSystem` (`:352-354`)
   - `ShipCollisionDamageSystem` (ramming)
   - `VoxelConnectivitySystem` (sets 0 on disconnected voxels, `:172-177`)
2. `VoxelDestructionSystem` scans **every** voxel with `Parent` each frame (`:159-191`). For Health ≤ 0 it:
   - decrements `ShipHealth`,
   - flags `ConnectivityCheckState.CheckPending`,
   - nulls the voxel's `ShipVoxelIndex` slot,
   - queues `ColliderRebuildRequired{0.1}`,
   - subtracts reactor, capacitor and shield energy,
   - destroys the voxel's children,
   - converts the voxel itself into a free physics debris body with an 8 s lifetime.
3. Next fixed step after the 0.1 s delay: `ShipColliderRebuildSystem` rebuilds the whole compound from the surviving voxels' `LocalToWorld` and compacts `ShipVoxelIndex` and `ShipVoxel.VoxelBufferIndex`.
4. `ShipHealthSystem`: when `Health < 0.4 * MaxHealth`, it adds `LifetimeComponent{0}` (`ShipHealthSystem.cs:30-33`). `EntityLifetimeSystem` then calls `DestroyEntity(ship)` (`:174`), which, through the `LinkedEntityGroup`, also destroys every voxel, thruster visual **and every in-flight debris piece** (B5).

---

## 2. Bugs

Severity reflects player-visible impact and risk. Confidence is in brackets.

### B1. Multi-cell blocks are ignored at runtime: false detachment and collider holes. High [high]

**Facts [V]:**
- `BlockItem` defines `BlockSize`, `VoxelShape` and `GetOccupiedCells()` (`Assets/Game/Core/Game Logic/BlockItem.cs:18-21, 59-88`).
- The ship editor builds connectivity from the **rotated occupied cells** (`ShipEditorManager.cs:889-897, 972-985`).
- At runtime, `BlockBlobData` has no shape data (`BlockDatabaseBlob.cs:12-40`) and `BlockBlobBuilder` never reads `BlockSize` or `VoxelShape`. `ShipVoxel.Size` exists but is never written (`ShipVoxel.cs:9`).
- `VoxelConnectivitySystem` puts only the **root cell** of each block into its grid map (`VoxelConnectivitySystem.cs:104-115`).
- Construction adds a single sphere of radius 0.5 cell at the root cell (`ShipConstructionJobSystem.cs:370-387`).

**Shipped data uses multi-cell blocks heavily [V]:**
- `block_thruster_medium` is 3×3×2 (VoxelShape): 8 ships, including `ship_bastion` and `ship_freighter`.
- `weapon_flak_cannon_medium` is 3×1×3.
- `block_capacitor_small` is 2×2×1.
- `weapon_ark_lance_medium` is 1×2×3.
- `block_core_cross` is a 7-cell cross (`Assets/Resources/Items/Blocks/Cores/block_core_cross.asset`), used by `ship_beetle`.

**Consequences:**
- **[V]** On the first connectivity check after any damage, every voxel whose only path to the core runs through a non-root cell of a multi-cell block gets Health 0 and flies off. On `ship_beetle` that includes anything attached to the arms of the cross core.
- **[I]** That cascade also lowers `ShipHealth` and can push the ship under the 40% death threshold early.
- **[V]** Most of a 3×3×2 block's volume has no collider, so projectiles and rams pass through it.
- **[V]** Damage capture for persistence keys on the root position only (`CombatManager.cs:186-213`).

**Fix:**
- Short term: add `BlobArray<int3> Cells` to `BlockBlobData` and rotate the cells by the block orientation at construction.
  - Add one box child per occupied cell, all mapping to the same voxel entity.
  - Insert every cell into the connectivity map.
- Long term: see §6.

### B2. `ResolveVoxel` computes the wrong number of key bits; wrong voxel is damaged for power-of-two child counts. High [high]

- **[V]** `VoxelPhysicsUtils.cs:63-72` computes `bits = 32 - lzcnt(count-1)` (1 when count ≤ 2).
- **[V]** Unity Physics uses `NumColliderKeyBits => (uint)(32 - math.lzcnt(NumChildren))` (`Library/PackageCache/com.unity.physics@8d312bc11767/Unity.Physics/Collision/Colliders/CompoundCollider.cs:1294`), and pops with `Value >> (32 - numSubKeyBits)` (`Collider.cs:1542-1547`).
- The two agree unless `NumChildren` is a power of two (2, 4, 8, … 256, 512, 1024 …). In that case the utility uses one bit too few and returns `realIndex >> 1`. That index is always in range, so no warning fires.
- **[I]** Ship sizes pass through powers of two as they lose voxels and the collider is compacted. Ships built with exactly 2^k voxels are wrong from spawn. Effect: hits and ramming damage a voxel roughly halfway toward the start of the buffer, and debris spawns away from the impact point.
- Callers: `EntityLifetimeSystem.cs:121,238`, `BeamSystem.cs:255`, `ShipCollisionSystem.cs:132-133`.
- **Fix (one line):** `return 32 - math.lzcnt((uint)count);`.
- **Better fix:** set `ColliderBlobInstance.Entity = voxel` (the field exists, `CompoundCollider.cs:422-439`) and read the leaf's `Entity` from the hit. Or resolve hits from the contact point in the ship grid (§6.4).

### B3. Compound collider blobs leak (rebuild and ship death). High at scale [high]

- **[V]** `ShipColliderRebuildSystem.cs:180-181` assigns a new `CompoundCollider.Create(...)` without disposing the old `physicsCollider.Value`.
- **[V]** Nothing disposes the ship's compound when the ship is destroyed. The only collider `Dispose` calls in the codebase are for temporary child blobs and pooled or cached colliders (grep of `.Dispose()`; see §8).
- **[V]** Unity Physics only auto-cleans colliders made "unique" through its internal `ColliderBlobCleanupData` (`PhysicsComponentExtensions.cs:39-90`, `ColliderBlobCleanupSystem.cs`). These colliders are not unique.
- **[I]** Size: one ~1k-voxel ship compound is a few hundred KB. Every rebuild (≥ every 0.1 s during combat) and every spawn and death leaks one blob. With the "massive ships" goal this becomes hundreds of MB per session.
- **Fix:** add an `ICleanupComponentData` holding the owned `BlobAssetReference<Collider>`, plus a deferred-dispose queue (dispose after the next `BuildPhysicsWorld`).

### B4. Shield blocks can never function. High [high]

- **[V]** `ShipConstructionJobSystem.cs:87-92` looks up `TryGetSingletonEntity<ShieldBubblePrefabTag>` and then reads `ShieldBubblePrefabReference`.
- **[V]** Why this fails:
  1. `ShieldBubblePrefabAuthoring` puts the tag on the **prefab entity itself** and adds `Prefab` (`ShieldBubblePrefabAuthoring.cs:24-27`). Default singleton queries exclude `Prefab` entities, so the lookup fails.
  2. No code anywhere adds `ShieldBubblePrefabReference` (grep: only declared at `:13` and read at `ShipConstructionJobSystem.cs:90`).
- **[V]** So `ShieldPrefab == Entity.Null`, and `CreateVoxelSpecial` skips the whole shield branch (`:444`). No `ShieldEmitterComponent` is ever added, which makes `ShieldSystem`, `ShieldVisualSystem`, the shield parts of `BeamSystem` and `ShieldToggleAbilitySO` (queries at `ShieldToggleAbilitySO.cs:152`) dead in practice.
- **Fix:** make it a singleton authoring with a `GameObject ShieldPrefab` field, the same pattern as `ThrusterVisualAuthoring.cs:9-28`.

### B5. Destroyed voxels remain in the ship `LinkedEntityGroup`; debris vanishes when the ship dies. Medium-High [high]

- **[V]** Every voxel, thruster visual and (would-be) bubble is appended to the ship's LEG (`ShipConstructionJobSystem.cs:321, 456, 584`). `DetachVoxel` never removes it (`VoxelDestructionSystem.cs:209-343`). The only other LEG write is `PlayerWeaponArcSystem.cs:61-64`.
- **[V]** The ship is destroyed when `ShipHealth < 40%` (`ShipHealthSystem.cs:30-33`, `EntityLifetimeSystem.cs:174`) and in `CombatManager.DespawnShip` (`:249`). `DestroyEntity` follows the LEG and silently skips entries that no longer exist (`EntityComponentStoreCreateDestroyEntities.cs:121-149`).
- **[V]** Consequences:
  - All debris still flying (8 s lifetime) disappears the moment the ship dies, which is exactly the "satisfying" moment.
  - The ship never "breaks apart": 60% of its voxels vanish in one frame.
- **Fix:** do not put voxels in the LEG at all. Destroy the attached children through the `Child` buffer or the brick list on death, and turn the remaining voxels into debris instead of deleting them.

### B6. Voxels are scene-bound (subscene `SceneTag`) but ship masters are not. Medium-High [mechanism high; gameplay impact medium]

- **[V]** On load, every entity of a section gets `SceneTag`, including prefabs (`Unity.Scenes/AsyncLoadSceneOperation.cs:163`).
- **[V]** `Instantiate` strips only `Prefab` and cleanup types (`EntityComponentStoreCreateArchetype.cs:121`), so voxel and thruster-visual instances keep the subscene's `SceneTag`.
- **[V]** Unloading destroys everything with that tag (`SceneSectionStreamingSystem.cs:893-895`).
- **[V]** The subscene lives in `GalaxyMapScene` and auto-loads (`GalaxyMapScene.unity:210-212`). `SceneLoader` defaults to `LoadSceneMode.Single`, and `FleetView.cs:477` / `DialogManager.cs:637` load `ShipEditorScene`.
- **[V]** Ship masters are created with `Ecb.CreateEntity` (no `SceneTag`).
- **[I]** Leaving the world scene destroys every voxel, while ship masters (compound collider, AI, velocity) survive. `ResetEntireState` despawns only the player ship (`OpenWorldGalaxyManager.cs:666-690`). Any other surviving ship is an invisible colliding ghost when you return.
- **[I]** Possibly related to the reported "data getting reset" class of bugs.
- **Fix:** remove `SceneTag` from instances (or create a runtime copy of the prefab without it once). Better: don't instantiate per-voxel prefabs at all (§6).

### B7. Collider rebuild starvation under sustained damage. Medium [high]

- **[V]** Every voxel death queues `ECB.AddComponent(ship, new ColliderRebuildRequired{DelayRemaining=0.1f})` (`VoxelDestructionSystem.cs:242`).
- **[V]** ECB AddComponent on an entity that already has the component **sets the value** (`EntityCommandBufferPlayback.cs:862-874` calls `AddComponentDuringStructuralChange(entity, type, data)`). So each new death resets the timer.
- **[V]** The rebuild runs only once `DelayRemaining` reaches 0 or less, decremented by the fixed dt (`ShipColliderRebuildSystem.cs:46-56`).
- **[I]** Effect: a beam or rapid fire that kills a voxel at least every 0.1 s keeps the stale collider indefinitely. Rams then hit "ghost" voxels. Projectiles that hit a nulled slot resolve to `Entity.Null` and are consumed for no damage (`EntityLifetimeSystem.cs:124-125`).
- **Fix:** only set the delay if the component is absent (check with a lookup), or store a first-dirty time with a maximum latency.

### B8. Ship blob cache key is incomplete; different ships share one blob. Medium-High [medium-high]

- **[V]** `GameDatabase.GetOrCreateShipBlob` keys on `ShipData.GetContentHash()` (`GameDatabase.cs:62-77`). That hash uses only dims, count, xyz and `runtimeBlockId` (`ShipData.cs:88-115`). It ignores rotation, colour, module slots and `GlobalStats`.
- **[V]** `runtimeBlockId` is `[NonSerialized]` (`ShipVoxel.cs:27`), so it is 0 after JSON or SO deserialization. `ShipBlobBuilder` resolves IDs only on a local struct copy (`ShipBlobBuilder.cs:53-59`), and `PrebakeStats` resolves on a `foreach` copy (`ShipData.cs:189-193`). Nothing writes the resolved ID back.
- **[I]** Two ships with the same silhouette but different blocks, colours, rotations or modules get the first ship's blob. That means wrong meshes, colours, weapons and stats on the second ship. A plain 32-bit hash collision does the same thing.
- **[V]** `ClearShipBlobCache` is never called (grep), so the cache grows for the whole session. Every distinct damage state of a ship adds a new blob.

### B9. Mesh map relies on persisted `string.GetHashCode` and the baker declares no dependencies. Medium [mechanism high]

- **[V]** The baker hashes the registry key with `key.GetHashCode()` and bakes `hash → meshIndex` into a blob (`VoxelPrefabAuthoring.cs:89-90, 117-136`). The runtime recomputes `blockKey.GetHashCode()` in `BlockBlobBuilder.cs:100-101`.
- **[I]** .NET explicitly does not guarantee `string.GetHashCode` across processes or runtimes; under CoreCLR it is randomised per process. If the player runtime ever differs from the editor runtime, every lookup misses and all blocks fall back to mesh 0 (`BlockBlobBuilder.cs:107-112`).
- **[V]** The baker calls `Resources.LoadAll<BlockItem>("Items")` (`:61`) with no `DependsOn(...)` on the BlockItems or meshes. **[I]** So adding, renaming or re-meshing a block does not rebake the subscene, and the mesh map goes stale. New blocks render with mesh 0 (or another wrong mesh) until a manual reimport. This is a likely source of "random wrong block" visual bugs.
- **[V]** The blob is created with `CreateBlobAssetReference(Persistent)` inside the baker without `AddBlobAsset` (`:133-135`). That bypasses baker blob deduplication and leaks on rebake.
- **Fix:**
  - Bake the key strings as `FixedString64Bytes`, or hash them with a stable hash (`xxHash3`).
  - Call `DependsOn` for every BlockItem and mesh.
  - Use `AddBlobAsset`.

### B10. Asteroid belts: stale centre after floating-origin shift; belts and asteroids never destroyed. Medium-High [high]

- **[V]** Belts are created without a `LocalTransform`. The centre is stored as `CenterWorldPosition` (`EcsSolarSystemGenerator.cs:196-213`).
- **[V]** The floating-origin shift only moves `LocalTransform` on `SolarSystemEntityTag` entities (`OpenWorldGalaxyManager.cs:264-288`), so the belt centre is never shifted.
- **[V]** `AsteroidBeltCullingSystem` computes player angle and distance, and spawns new asteroids, from that stale centre (`AsteroidBeltCullingSystem.cs:63-91, 147-160`; `AsteroidLayoutJobSystem.cs:73-77`).
- **[V]** `EcsSolarSystemGenerator.DestroySystem` destroys only `systemCenterEntity` (`:355-376`). Belt entities and their spawned asteroids are never destroyed, and revisiting a system creates new belts.
- **[I]** After the first origin shift (threshold 5000), belt sectors spawn or despawn in the wrong places (`distFromRing > 8000` despawns everything). Belts accumulate across visits. This plausibly contributes to "solar systems/asteroids not showing up". The galaxy auditor should cross-check.

### B11. Half of all asteroids look identical. Medium (visual) [high]

- **[V]** `Seed = (int)rng.NextUInt()` (`AsteroidBeltCullingSystem.cs:155`) is negative about 50% of the time. `new Random((uint)(seed > 0 ? seed : 1))` (`AsteroidLayoutJobSystem.cs:45`) maps every negative seed to 1, so those asteroids get the **same variant and the same rotation**.
- **Fix:** `Random.CreateFromIndex((uint)seed)`.

### B12. Ship–asteroid collisions never deal damage. Medium [high]

- **[V]** `ShipCollisionJob` calls `ResolveVoxel` for both bodies. Asteroids have no `ShipVoxelIndex`, so the result is Null and the event is dropped at `ShipCollisionSystem.cs:136`.
- **[V]** The asteroid branch in `ShipCollisionDamageSystem` (`:218-234`) is therefore unreachable.

### B13. Movement overwrites physics velocity, so ramming has no physical response. Medium (design bug) [high]

- **[V]** `ShipMovementSystem` sets `pVelocity.Linear = movement.CurrentVelocity` and `velocity.Angular = (0, turn, 0)` every frame (`ShipMovementSystem.cs:119, 181`). Physics never feeds back into `CurrentVelocity`. Any collision impulse is therefore wiped the next frame, and ships behave like infinite-mass bulldozers against each other.
- **[V]** The system runs in the variable-rate `SimulationSystemGroup` (`:13`), after the fixed physics step. Drag uses `lerp(v, 0, drag*dt)` (`:107, 115, 158, 173`), which is frame-rate dependent.
- **[V]** Speed equals total thrust regardless of mass (`ShipConstructionJobSystem.cs:228-236`).
- **Fix:** apply thruster forces as impulses in FixedStep before `PhysicsSystemGroup`. Use exponential damping. Do not overwrite velocity.

### B14. Mass and centre of mass never updated after damage. Low-Medium [high]

- **[V]** `PhysicsMass` is created once (`ShipConstructionJobSystem.cs:397-401`) and is not touched by the rebuild. **[I]** A ship that has lost half its hull keeps spinning around its original centre of mass with the original inertia.

### B15. Main-thread `VoxelHealth` write without completing jobs; parallel read-modify-write races. Medium [medium]

- **[V]** `VoxelConnectivitySystem` writes `VoxelHealth` through a `ComponentLookup` on the main thread (`:172-177`) without `CompleteDependency`.
- **[V]** `BeamSystem` (`:131`, scheduled `:109`) and `EntityLifetimeSystem` (`:68`, `ScheduleParallel` at `:55`) write `VoxelHealth` from jobs using `[NativeDisableParallelForRestriction]`. Neither has an ordering constraint relative to `VoxelConnectivitySystem`.
- **[I]** Depending on the sort order, this produces intermittent safety exceptions in the editor, or a race in builds.
- **[V/I]** Separately, two projectiles hitting the same voxel in parallel jobs is a lost-update race on `Health`. The combat auditor owns this; it is listed here because it decides voxel death.

### B16. `ShipData` finalizer disposes a blob on the GC thread; PrebakeStats builds an unused blob. Low-Medium [high]

- **[V]** `~ShipData` disposes `_blobAsset` (`ShipData.cs:139-146`).
- **[V]** `PrebakeStats` creates that blob (`:253-254`), but the spawn path uses the `GameDatabase` cache instead (`CombatManager.cs:77`). `GetOrCreateBlobAsset` has no callers.
- **[I]** This is wasted work plus a finalizer-thread native free of Persistent memory. It is fragile under domain reload.

### B17. Connectivity has gaps and does not scale. Medium [high]

- **[V]** Ships without a Core block never run connectivity (`VoxelConnectivitySystem.cs:64-68`). `ship_bastion`, `ship_freighter`, `ship_icarus`, `ship_red_elite` and `ship_striker` have no core (block histogram of `Assets/Resources/Items/Ships/*.asset`), so islands on those ships never fall off.
- **[V]** Each check gathers voxels by iterating every `ShipVoxel` in the world and filtering by parent (`:104-115`). That is O(all voxels) per check.
- **[V]** Disconnected islands are never split into new ships. Each voxel becomes its own debris rigid body.

### B18. Floating origin shift does not move debris or projectiles. Low-Medium [high]

- **[V]** `ShiftAllEcsShips` and `ShiftAllEcsEntities` move only `ShipMaster` and `SolarSystemEntityTag` roots (`OpenWorldGalaxyManager.cs:264-325`). Unparented debris voxels (with `ScrapTag`) and projectiles are left behind.
- **[I]** On a shift, every flying debris piece appears to jump about 5000 units.

### B19. Smaller defects

| # | Defect | Evidence | Sev [conf] |
|---|---|---|---|
| a | Thruster flame scaled by `ShipScale` twice (`PostTransformMatrix` × parent voxel scale), so it scales quadratically. The flame offset is a fixed `(0,0,1)` regardless of block depth (1×1×2 and 3×3×2 thrusters). | `ThrusterVisualSystem.cs:111-113`, `ShipConstructionJobSystem.cs:572-577` | Low [high] |
| b | Voxel prefab `RenderBounds` is the union of **all** block meshes (`flak_cannon_3x3x3`), so every 1×1 hull voxel has a ~3×3×3 culling/shadow box. | `VoxelPrefabAuthoring.cs:141-149` | Low [high] |
| c | The rebuild reconstructs local transforms from last frame's `LocalToWorld` via a matrix inverse, instead of the exact `GridPosition*scale`. This adds float drift, and gives a zero matrix for voxels whose LTW was not computed yet. | `ShipColliderRebuildSystem.cs:95-146` | Low [high] |
| d | `ShipColliderRebuildSystem` has no `RequireForUpdate<ColliderRebuildRequired>`, so it runs a `CompleteDependency` sync point every fixed step. | `:31-35` | Low [high] |
| e | Debris collider is `Radius = 1*shipScale` and then scaled again by `LocalTransform.Scale = ShipScale`. | `VoxelDestructionSystem.cs:94-98, 301-306` | Low [high] |
| f | Debris filter is `BelongsTo=Projectile`. The explosion `OverlapSphere` uses `CollidesWith=Voxel`, and debris has no `ShipVoxelIndex`, so the "destroy debris caught in explosion/hit" branches are unreachable. | `EntityLifetimeSystem.cs:130, 248` | Low [high] |
| g | Children of a destroyed voxel are destroyed inside the `ShipEnergy` block (wrong comment, wrong scope). | `VoxelDestructionSystem.cs:270-278` | Low [high] |
| h | `PrebakeStats` dimensions are off by one (`max-min`, not `+1`). | `ShipData.cs:226-228` | Low [high] |
| i | Stray `#pragma warning restore CS0618` without a matching disable. | `ShipVoxel.cs:68` | Trivial |
| j | Asteroid fallback mesh writes `_voxelCounts[0]` instead of `[variant]`. A variant with zero voxels also has no collider. | `AsteroidConstructionJobSystem.cs:264-270, 481` | Low [high] |
| k | Asteroid pooled collider is shared, despite the "Clone" comment. Mass comes from unscaled `MassProperties` while `LocalTransform.Scale` is 0.5–2. Pool meshes are never destroyed in `OnDestroy`. | `:142-152, 58-68` | Low [high] |
| l | Asteroid "debris" is created with transform and velocity but no render, collider, `PhysicsMass` or `PhysicsWorldIndex`, so it is invisible and inert. | `EntityLifetimeSystem.cs:261-300` | Low [high] |
| m | A build request with an invalid blob never gets a result and logs an error every frame forever. | `ShipConstructionJobSystem.cs:147-159` | Low [high] |
| n | `ShieldBubblePrefabAuthoring` manually adds `LocalTransform` and `LocalToWorld` on top of `TransformUsageFlags.Dynamic`. Dormant because of B4. | `ShieldBubblePrefabAuthoring.cs:24-32` | Low [medium] |
| o | `ColorPalette` (managed static `Color[]`) is read inside a Burst job. It works only because Burst constant-folds static readonly arrays, which is fragile. | `ShipConstructionJobSystem.cs:366` | Low [medium] |
| p | `EditorBuildSettings` lists `Assets/Game/Scenes/CombatScene.unity`, which does not exist. Other auditor. | `ProjectSettings/EditorBuildSettings.asset:15` | Low [high] |
| q | Ship "sinking" never plays. `ShipHealthSystem` destroys the entity on the next ECB playback while `CombatManager.MarkShipForSinking` sets `IsSinking`. | `ShipHealthSystem.cs:30-37`, `CombatManager.cs:114-139` | Low [high] |
| r | `OpenWorldCombatRefactorGuide.md` is stale. It lists `ShipCreationSystem.cs` (does not exist) and calls `GameObjectSyncSystem` the ship sync mechanism (it is dead; the real one is `EcsToGameObjectSyncSystem`). | `OpenWorldCombatRefactorGuide.md:116-134` | Doc |

---

## 3. What is actually used

| File | Status | Evidence |
|---|---|---|
| `ShipConstructionJobSystem.cs`, `ShipConstructionLinkSystem.cs`, `ShipBuildRequest.cs` | Used | `CombatManager.cs:86-101` |
| `BlockDatabaseInitSystem.cs`, `BlockBlobBuilder.cs`, `BlockDatabaseBlob.cs` | Used | singleton consumed at `ShipConstructionJobSystem.cs:84-85` |
| `VoxelPrefabAuthoring.cs` | Used | on `ShipVoxelEntityPrefab.prefab`, which sits in the subscene |
| `ThrusterVisualAuthoring.cs`, `GlobalShipScaleAuthoring.cs` | Used | subscene (`EntitiesSubscene.unity:262, 307`) |
| `ShieldBubblePrefabAuthoring.cs` | Present but **functionally dead** | B4 |
| `ShipColliderRebuildSystem.cs`, `VoxelPhysicsUtils.cs` | Used (buggy) | B2, B3 |
| `ShipMovementSystem.cs` (+`ShipConstraintSystem`), `ShipInputSystem.cs`, `ShipMovementComponent.cs`, `ShipInputComponent.cs` | Used | `activeInputHandler: 2` (Both), so legacy `Input.GetAxis` works (`ProjectSettings.asset:808`) |
| `ThrusterVisualSystem.cs`, `ThrusterComponent.cs` | Used | |
| `EcsToGameObjectSyncSystem.cs` | Used | `ShipConstructionLinkSystem.cs:39`, `EcsSolarSystemEntityInstance.cs:75` |
| `ShipData.cs`, `ShipVoxel.cs`, `ShipDataBlob.cs`, `ShipBlobBuilder.cs`, `ColorPalette.cs`, `ShipEnergyComponent.cs`, `ReactorComponent.cs`, `CapacitorComponent.cs`, `VoxelParentRef.cs`, `VoxelColorProperty.cs`, `GlobalShipScale.cs` | Used (with dead members, §4) | |
| `ShipStatCalculator.cs` | Partially used | only `CalculateGlobalShipStats` (`ShipData.cs:242`) |
| `VoxelVisualData.cs` | Partially used | `CommonMaterial` is used by asteroids only; `WeaponArcMaterial` and `QuadMesh` by `PlayerWeaponArcSystem` |
| `Asteroid/*` | Used | belts created by `EcsSolarSystemGenerator.cs:196-219` |

---

## 4. Dead, deprecated and duplicate code (with proof)

| Item | Proof of non-use | Action |
|---|---|---|
| `ShipVoxelBuffer` (`ShipVoxelBuffer.cs`) | Referenced only in its own file (grep). Duplicates `ShipVoxelIndex`. | Delete |
| `ShipDefinitionAuthoring` | GUID `70909ad3…` appears in no scene, prefab or asset. Its baker body is commented out (`:16-19`). | Delete |
| `Camera/PlayerShipCameraFollowSystem.cs` | Every line is commented out (`:1-61`). | Delete |
| `GameObjectSyncSystem` + `ObjectTransformSync` | Nothing adds `ObjectTransformSync` (only `GameObjectSyncSystem.cs:9` and a comment in `ShipBuildRequest.cs:20`). `RequireForUpdate<ObjectTransformSync>` (`:22`) means it never runs. | Delete |
| `GameDatabase.SyncTransformDictionary`, `RegisterSyncTransform`, `TryGetSyncObject`, `UnregisterSyncTransform` | No callers outside `GameDatabase.cs:12, 157-176` (grep). | Delete (GameDatabase owner) |
| `ShipBlobReference` | Defined only (`ShipDataBlob.cs:53-56`). | Delete |
| `VoxelPrefabReference` | Defined only (`VoxelPrefabAuthoring.cs:23-26`). | Delete |
| `ShipDataBlob.AllModuleSlots`, `VoxelBlobData.ModuleSlotsStart/Count` | Allocation commented out (`ShipBlobBuilder.cs:47, 72-79`). The fields carry meaningless values. | Delete, or implement modules properly |
| `ShipData.GetOrCreateBlobAsset`, `InvalidateBlobAsset`, `_blobAsset`, finalizer | No external callers. Spawning uses the `GameDatabase` cache. | Delete (B16) |
| `ShipStatCalculator.CalculateShipStats`, `GetNeighborPositions`, `ComputedVoxelStats`, commented `BuildSpatialMap` | No callers (grep). | Delete |
| `ShipVoxel.Size`, `ShipVoxel.IsExposed` | Never written or read (grep for `.Size` matches only projectile code). | Delete, or implement (B1) |
| `VoxelVisualData.ProjectileMaterial/EnemyMaterial/ShieldMesh/ShieldMaterial` | 0 code references (grep). | Delete fields |
| `GameDatabase.ShipMeshDictionary`, `RegisterVisualData`, `TryGetVisual`, `_registry` | No callers outside `GameDatabase.cs`. | Delete (GameDatabase owner) |
| `ShipInputComponent.PitchInput/RollInput` | Always set to 0 (`ShipInputSystem.cs:101-102`); never read by movement. | Keep only if 3D flight is planned |
| `ShipMovementComponent.IsSinking/SinkTimer` | Effectively dead (B19q). | Remove with the sinking redesign |
| `ShieldBubblePrefabAuthoring` + `ShieldEntityPrefab` in the subscene | Functionally dead (B4). | Fix or remove |
| `Meshes/Voxel Meshes/shipVoxel_cube.obj`, `shipVoxel_verticalslab.obj` | 0 GUID references across `.asset/.prefab/.unity/.mat`. | Delete |
| `VoxelColorProperty` doc comment ("`_VoxelColor`") | The attribute actually maps `_BaseColor` (`VoxelColorProperty.cs:7-9`). | Fix comment |
| `ColorPalette` comment "Index 0 reserved for inherit from ship primary" | There is no primary or secondary colour concept at runtime; index 0 is just grey (`ColorPalette.cs:12-16`). | Fix comment or implement |
| `CollisionIdTracker` creation guarded by `HasComponent(state.SystemHandle)` | Odd but harmless. A plain singleton entity is enough (`ShipConstructionJobSystem.cs:50-55`). | Simplify |

---

## 5. Performance and scalability

### 5.1 Per-voxel cost model (current)

**CPU chunk data per voxel entity [V types / I sizes]:**

| Component | Bytes |
|---|---|
| Entity | 8 |
| `LocalTransform` | 32 |
| `LocalToWorld` | 64 |
| `ShipVoxel` | 48 (`int` + 2×`float3` + `int3` + `bool` + `int`, padded) |
| `VoxelHealth` | 12 |
| `VoxelParentRef` | 8 |
| `Parent` | 8 |
| `PreviousParent` | 8 |
| `MaterialMeshInfo` | 12 (`RenderMeshArray.cs:52-60`) |
| `RenderBounds` | 24 |
| `WorldRenderBounds` | 24 |
| `VoxelColorProperty` | 16 |
| **Total** | **≈ 264 B** |

That gives about 60 voxels per 16 KB chunk. Adding the global entity table (~16 B), the `ShipVoxelIndex` entry (8), the LEG entry (8) and the ship `Child` entry (8) brings it to **≈ 300 B per voxel**.

**GPU per instance [I]:** `unity_ObjectToWorld` (48 B, 3×4) + `unity_WorldToObject` (48 B, registered at `EntitiesGraphicsSystem.cs:1088`) + `_BaseColor` (16 B) = **112 B**.

**Physics compound [I]:** about 100–200 B per child (child header, sphere blob copy and BVH node share).

| Live voxels | ECS CPU | GPU instance data | Matrix upload per frame when ships move | Chunks | Compound size |
|---|---|---|---|---|---|
| 10k | ~3 MB | ~1.1 MB | ~1 MB/frame | ~170 | ~1–2 MB |
| 100k | ~30 MB | ~11 MB | ~10 MB/frame (≈0.6 GB/s at 60 fps) | ~1.7k | ~10–20 MB |
| 1M | ~300 MB | ~112 MB | ~96 MB/frame (≈5.8 GB/s) | ~17k | ~100–200 MB |

Memory alone is survivable. The per-frame costs are what kill this approach:
- **Transforms [V mechanism]:** every voxel is a child of a moving ship, so `LocalToWorldSystem` recomputes every voxel's LTW every frame. Entities Graphics then recomputes `WorldRenderBounds`, re-culls each instance (`PerInstanceCullingTag`) and re-uploads each changed matrix. All of this is O(voxels) per frame. [I] At 1M voxels that is about 5–15 ms of multithreaded CPU for transforms and bounds, plus a GPU upload bandwidth that is not realistic.
- **Triangles [V mesh data / I totals]:** `hull_1x1` is 12 triangles (`shipVoxels.obj`). Each voxel draws all six faces even when buried. A solid N³ block draws 12N³ triangles where a culled-face mesh would draw about 12N². With shadows on (2 cascades, 500 m, `URP Asset.asset:57-58`), the geometry is drawn about 3×. [I] A 1M-voxel ship is about 36M triangles per frame, of which over 80% are invisible internal faces.
- **Draw commands [I]:** one material, about 10 meshes, about 17k chunks with mixed meshes. That becomes thousands to tens of thousands of BRG draw ranges per pass. Weapon meshes are 1–3k triangles each (`flak_cannon_3x3x3` 2398, `missile_launcher` 3266), which is fine at the current small counts.

### 5.2 Construction cost

- **[V]** Construction runs as one `Execute` per request entity, so a single ship builds on a single thread. It is followed by `Complete()`.
- **[V]** Per voxel: one `SphereCollider.Create` (a Persistent malloc) plus about 8–15 ECB commands.
- **[V]** ECB playback happens on the main thread at the next `BeginInitialization`.
- **[I]** Playback with `Instantiate` plus 2–3 archetype moves per voxel is about 2–4 µs per voxel. That is roughly 30 ms for 10k voxels, 0.3 s for 100k and **3+ s for 1M**, all on the main thread. Then `CompoundCollider.Create` adds 0.2–1 s at 1M.
- **Cheap improvements:**
  - Reuse **one** child collider blob for every instance (`Create` copies the children).
  - Batch-instantiate with `EntityManager.Instantiate(prefab, NativeArray)` and fill values with an `IJobChunk`.
  - Pre-build a runtime prefab that already has `Parent` and `VoxelColorProperty`.

### 5.3 Destruction cost

**Per destroyed voxel:**
- **[V]** About 12 ECB commands and about 4 archetype moves.
- **[V]** A full-ship collider rebuild on the main thread, not Burst-compiled: N `SphereCollider.Create` calls, one O(N log N) BVH build, and N `EntityManager.SetComponentData` calls (`ShipColliderRebuildSystem.cs:131-189`).
- **[I]** That is about 5–15 ms per rebuild for 10k voxels and 0.5–1.5 s for 1M, repeated every 0.1 s under fire (or starved, see B7).

**Per frame regardless of damage:**
- **[V]** `VoxelDestructionJob` scans every voxel with `Parent`, single-threaded (`.Schedule`, `VoxelDestructionSystem.cs:137-155`).
- **[V]** Each connectivity check scans every voxel in the world.

**Per destroyed voxel in the physics world [V]:** a full Unity Physics dynamic body (`VoxelDestructionSystem.cs:290-342`). [I] A big hull section breaking off at once means thousands of simultaneous rigid bodies.

### 5.4 Burst, job and main-thread gaps **[V]**

- **Main-thread, not Burst-compiled:**
  - `ShipColliderRebuildSystem` (no `[BurstCompile]`).
  - `AsteroidConstructionJobSystem` (SystemBase, managed mesh generation using `float3[]` and managed arrays, `:279-312`).
  - `ShipConstructionLinkSystem`, `BlockDatabaseInitSystem`, `ShipInputSystem` (managed is OK for these).
  - `EcsToGameObjectSyncSystem` (job, then immediate `Complete`).
- **Single-threaded jobs:** `VoxelDestructionSystem`. The connectivity BFS runs inside `OnUpdate`.
- **Managed allocations per spawn:**
  - `ShipBlobBuilder` / `ShipData` lists.
  - `GetContentHash` iterates every voxel on every spawn.
  - `CombatManager.MarkDestroyedVoxels` allocates a `HashSet`.
- **ISystem attribute consistency [I, low]:** `ThrusterVisualSystem` puts `[BurstCompile]` on the struct (`:14`), while `ShipMovementSystem`, `ShipConstructionJobSystem` and `VoxelDestructionSystem` mark methods only. Check in the Burst Inspector that `OnUpdate` really is compiled.

### 5.5 Verdict

**[I]** One entity per voxel, a single compound of spheres rebuilt wholesale, and one debris rigid body per destroyed voxel cannot reach 100k–1M voxel ships or stations. The ceiling is probably about **20–50k live voxels** in the scene, with combat hitches well before that because of the rebuilds.

The current design does get some things right, and these should be kept:
- the ship as a single rigid body with a compound collider;
- blob-based block definitions;
- `MaterialMeshInfo` indexing into one `RenderMeshArray`;
- the per-instance colour property;
- the culled-face mesh generator already written for asteroids (`AsteroidConstructionJobSystem.cs:273-357`), which is a ready seed for a chunk mesher.

---

## 6. Recommended target architecture

### 6.1 Principles

1. **Cells are data, not entities.** A ship or station is one root entity plus a small number of **brick** entities (16³ cells each) and **functional block** entities (thrusters, weapons, shields, reactors, doors, …). Plain hull has no entity.
2. **Cells are the unit of storage, damage, collision and connectivity. Blocks are the unit of authoring and function.** Multi-cell "Tetris" blocks are a block instance that occupies several cells.
3. **Render per brick, not per voxel.** Brick meshes are remeshed only when the brick is dirty. Per-frame cost is O(bricks): one matrix per brick, or one per ship with a ship-space shader.
4. **Physics per brick.** Each brick is a box-merged sub-compound inside one ship compound. Only dirty bricks are rebuilt.
5. **Hits resolve in grid space.** Never map compound child indices to voxels.
6. **Destruction is event-driven and batched per ship.** No per-frame scans and no parallel read-modify-write races.
7. **Debris is cheap.** Small debris is pooled GPU particles. Large islands become new ships, which get physics, connectivity and all the rest for free.

### 6.2 Data layout (sketches)

```csharp
// ---------- Block definitions (blob, built once from BlockItem SOs) ----------
public struct BlockDef {
    public ushort Id;                 // stable id (bake from registry key, not GetHashCode)
    public BlockKind Kind;            // Structural (meshed), Prop (instanced mesh), Functional (entity)
    public byte  MaterialLayer;       // texture-array slice / PBR set for structural faces
    public half  CellMaxHp;           // per-cell hp
    public float CellMass;            // kg per cell
    public int   PropMeshIndex;       // for Prop/Functional: index into prop RenderMeshArray
    public BlobArray<byte3> Cells;    // occupied cells relative to root (unrotated)
    public FunctionParams Function;   // union-ish: thrust, weapon, shield, reactor ...
}
public struct BlockDefsBlob { public BlobArray<BlockDef> Defs; }

// ---------- Per-cell storage (2–4 bytes) ----------
public struct VoxelCell                      // 0 == empty
{
    public ushort BlockOrInstance;           // high bit=0: BlockDef id (single-cell block)
                                             // high bit=1: BlockInstance index (multi-cell / functional)
    public byte   Orientation;               // 0..23 cube rotation group (replaces float3 Euler)
    public byte   Hp;                        // quantised 0..255 of CellMaxHp (or damage level for cracks)
}

// ---------- Ship root ----------
public struct VoxelGrid : IComponentData {
    public int3   BrickMin, BrickDims;       // brick-space AABB of the ship
    public float  CellSize;                  // replaces GlobalShipScale per ship (stations can differ)
    public uint   Version;                   // bumps on any cell change
}
[InternalBufferCapacity(0)]
public struct BrickSlot : IBufferElementData { public Entity Brick; }   // dense BrickDims.x*y*z table (O(1) lookup)
public struct ShipMassState : IComponentData { public float Mass; public float3 MassMoment; public float3x3 InertiaAccum; }
[InternalBufferCapacity(0)]
public struct BlockInstance : IBufferElementData {   // multi-cell & functional blocks
    public ushort DefId; public int3 RootCell; public byte Orientation;
    public float Hp; public Entity FunctionalEntity; // Null for pure multi-cell structure
}

// ---------- Brick (16^3) ----------
public struct Brick : IComponentData { public Entity Ship; public int3 Coord; public ushort SolidCount; }
[InternalBufferCapacity(0)] public struct BrickCells : IBufferElementData { public VoxelCell Value; } // 4096 entries
public struct BrickSolidMask : IComponentData { public FixedBytes512 Bits; } // 4096 bits, used for meshing/connectivity via bit ops
public struct BrickDirty : IComponentData, IEnableableComponent { public byte Flags; } // Mesh|Collider|Connectivity
// Brick render: LocalTransform (child of ship), LocalToWorld, MaterialMeshInfo (runtime-registered mesh id), RenderBounds
// Brick physics: its sub-compound blob lives in a cleanup component so it is always disposed
public struct BrickColliderBlob : ICleanupComponentData { public BlobAssetReference<Collider> Value; }

// ---------- Functional block entity ----------
public struct FunctionalBlock : IComponentData { public Entity Ship; public int InstanceIndex; public int3 RootCell; }
// + ThrusterComponent / WeaponComponent / ShieldEmitterComponent / ReactorComponent (reuse existing ones)

// ---------- Events ----------
public struct DamageEvent { public Entity Ship; public float3 ShipLocalPoint; public float3 ShipLocalDir; public float Radius; public float Amount; public DamageType Type; }
public struct CellDestroyedEvent { public Entity Ship; public int3 Cell; public ushort DefId; public float3 WorldPos; public float3 WorldVel; }
```

**Why 16³ bricks:**
- 4096 cells × 4 B = 16 KB of cells; the solid bitmask is 512 B.
- [I] A culled-face remesh of one brick is about 20–60 µs in Burst, and a greedy-box collider build is about 50–150 µs. One hit dirties one to eight bricks, which is affordable in a frame.
- 32³ bricks cut the entity count by 8× but cost 8× more per dirty brick. That suits static stations better than ships that are being shot. A sensible split is 16³ for ships and 32³ (or merged static meshes) for undamaged stations and asteroids.

**Sparse storage:** only bricks containing at least one cell exist. A 1000×200×200-cell hollow capital ship has about 0.9M shell cells. [I] That is roughly 2–3k bricks, about 30–60 MB of cells at 16 KB/brick when shell bricks are partially empty. Using `VoxelCell` at 2 B, or a per-brick palette for uniform bricks, brings this to about 10–20 MB.

### 6.3 Rendering: options compared

| Option | Per-frame CPU | Triangles | Update on hit | Fits detailed block meshes? | Verdict |
|---|---|---|---|---|---|
| A. Entities Graphics, entity per voxel (current) | O(voxels): LTW, bounds, cull, upload | 12/voxel incl. internal | trivial | yes | Does not scale |
| B. GPU instancing of **surface** voxels (BRG or `Graphics.RenderMeshIndirect`), ship-space instance buffer (packed `int3` + def + orientation, 8 B), one matrix per ship | O(ships) | 12 × surface voxels (internal faces still drawn for surface cubes) | remove/add instance + expose neighbours | yes (draw per mesh type) | Good for **props** and **debris** |
| C. Per-brick **culled-face** mesh (Burst, `Mesh.MeshDataArray`), rendered with Entities Graphics via `EntitiesGraphicsSystem.RegisterMesh` | O(bricks) | ~2 tris per exposed face | remesh 1–8 bricks async | cube and slope families only | **Default for structure** |
| D. Per-brick **greedy** mesh | O(bricks) | lowest | remesh cost ~2–3× C | same as C; merging fights per-cell colour, AO and damage decals | Optional LOD1+ and undamaged stations |

**Recommendation: C + B.**
- **Structural blocks** (hull, armour, slopes, which are about 95% of cells) use culled-face brick meshes.
  - Compact vertex format: packed position (3×5 bits within the brick), face id (3 bits), texture layer (8–10 bits), AO (2 bits per vertex), colour index (8 bits). About 8 B per vertex.
  - Shader uses a `Texture2DArray`.
  - Cracks or damage are encoded from `Hp`.
  - Per-voxel colour is supported without per-instance properties.
- **Prop blocks** (turret bodies, thrusters, engines, antennae) are instanced with B from a per-ship `GraphicsBuffer`. If a prop needs its own transform (a rotating turret), it is a functional entity that renders through Entities Graphics as today. There are only hundreds of these, not millions.
- **Debris** is a pooled B instance buffer simulated by a Burst job.
- **Remesh pipeline:**
  - `BrickDirty` is set, then `BrickMeshJob` runs (a parallel `IJobChunk` over dirty bricks with a per-frame budget). It writes into `Mesh.AllocateWritableMeshData`.
  - On the main thread, `ApplyAndDisposeWritableMeshData` runs next frame, followed by a `MaterialMeshInfo` swap.
  - The old mesh is released through a deferred-destroy queue.
- **Shadows:** brick meshes cast shadows. Disable shadows for debris and far LODs.

### 6.4 Physics

**Ship collider: nested compound.**
- Top-level children are brick sub-compounds.
- Each brick sub-compound is made of **greedy-merged boxes** from a 3D box decomposition of `BrickSolidMask`: grow runs along X, then Y, then Z.
- [I] Typical hull reduces 10–100× (a solid 16³ brick becomes 1 box).
- Set `ColliderBlobInstance.Entity = brickEntity` for traceability.
- On damage: rebuild only the dirty bricks' sub-compounds in a Burst job, then re-create the top-level compound. The top level has `#bricks` children, so it costs a memcpy of the brick blobs plus a BVH over about 1–3k entries.
- Swap the result in the FixedStep before `BuildPhysicsWorld`, and dispose the previous blob one step later.

**Hit resolution: grid first, physics second.**
- Projectiles and beams: use the physics raycast only to find the ship body (or a ship OBB broadphase). Transform the ray into ship-local grid space, then march it with **3D DDA** across the bricks and cells until the first solid cell. This is exact, independent of collider staleness, and cheap (about 10–100 cell steps).
- Contacts (ramming): take the contact point, convert to ship-local, step half a cell along `-normal`, and floor to a cell. Then apply a damage sphere of radius proportional to the impulse.
- Explosions: find overlapped ships with `OverlapSphere`, then iterate cells in the sphere's grid AABB.

**Mass:** maintain `ShipMassState` incrementally. On each cell removal subtract `m`, `m·p` and the parallel-axis inertia terms. Write `PhysicsMass` once per frame per dirty ship.

**Movement:** thrusters are functional entities that produce a local force at their cell. `ShipThrustSystem` (FixedStep, before physics) applies `ApplyImpulse(force*dt, worldPoint)`, which gives torque from off-centre thrusters for free. Use exponential damping and **never overwrite velocity**. Keep the 2D plane constraint as an option (inertia lock plus Y projection) while the game is top-down.

**Ownership:** every runtime-created blob (brick sub-compound, ship compound) lives in an `ICleanupComponentData`, and a cleanup system disposes it.

### 6.5 Destruction pipeline

1. **Producers** (projectiles, beams, explosions, rams) write `DamageEvent`s into a `NativeStream` or a per-thread `NativeQueue`. They never write cell HP directly.
2. **`VoxelDamageApplySystem`** sorts or buckets events by ship, then runs one job per ship, so there are no races. It decrements cell or block HP, and collects destroyed cells into `CellDestroyedEvent`s and dirty brick flags.
   - When a multi-cell block's HP reaches 0, all of its cells are removed together. Optionally, cells are removed individually and the block's function is disabled below a threshold.
3. **Connectivity (incremental).** Each brick keeps its local connected components, computed with a bitmask flood fill, plus labels on its six faces. Only dirty bricks recompute. A union-find over (brick, component) nodes across faces then gives global islands in roughly O(#components).
   - Fast reject: if a removed cell had at most one solid neighbour, it cannot split the ship, so skip.
   - The **anchor** island is the one containing the core. If the ship has no core, use the largest island (this fixes B17 for core-less ships and stations).
4. **Split.** Each non-anchor island with K or more cells (e.g. K = 32) becomes a **new ship**:
   - copy its bricks and cells into a new `VoxelGrid` and remove them from the parent;
   - move its functional block entities to the new ship;
   - new velocity is `v + ω × r`;
   - the new ship gets its own collider and mass.
   It inherits everything else (rendering, hits, further splitting) automatically.
5. **Debris.** Islands smaller than K, and individually destroyed cells, become `DebrisParticle { float3 pos, vel; quaternion rot; float3 angVel; ushort def; byte colour; half life }` in a pooled ring buffer (e.g. 50k).
   - A Burst job integrates them: ballistic motion with drag, optionally bouncing off ship OBBs or grid lookups.
   - They are drawn with `Graphics.RenderMeshIndirect` (cube mesh, per-instance colour) and fade out.
   - No entities and no physics bodies.
   - Optionally, the largest few chunks (≤ N per frame) become small physics rigid bodies with box colliders for tumbling interaction.
6. **Functional aggregation.** A `CellDestroyedEvent` that hits a functional block disables or destroys its entity. Ship energy and thrust aggregates are recomputed from the functional list for dirty ships only.
7. **Death** is "no anchor cells left" or "core destroyed". The remaining structure explodes into islands and debris; it is not deleted (fixes B5).

### 6.6 Multi-cell blocks vs single voxels

Support both in one model:
- **Single-cell structural blocks** (hull, armour, slopes) store the `BlockDef` id directly in the cell. This needs zero extra memory.
- **Multi-cell or functional blocks** store a `BlockInstance` index in each occupied cell. The instance owns the HP pool, the orientation and the functional entity.
  - The structural mesher skips cells flagged `Prop`, because the prop mesh draws them.
  - Physics and connectivity treat those cells as solid.
- Orientation becomes a **byte (0–23)** instead of a float Euler triplet. `BlockDef.Cells` is rotated by a 24-entry lookup table. This keeps the editor and runtime identical, which removes the B1 class of bugs.

### 6.7 LOD

- **Render LOD:**
  - LOD0: culled-face brick meshes.
  - LOD1: 2×-downsampled bricks (a cell is solid if at least 4 of its 8 children are), meshed greedily.
  - LOD2: one merged ship mesh, or an impostor or billboard.
  - Swap by switching `MaterialMeshInfo` on the brick entities, or by disabling the brick entities and enabling a proxy entity.
- **Physics LOD:**
  - Dormant ships far from the player (no possible interaction) use a single box or convex hull, or `PhysicsWorldIndex` 1 (not simulated).
  - The full brick compound is used only within interaction range.
- **Simulation LOD:** reduce the AI and weapon tick rate by distance.
- **Streaming:** each solar system has its own entity set. Belts and stations are unloaded and serialized when the player leaves (fixes the B10 class of bugs).

### 6.8 Floating origin and galaxy scale

- Store authoritative positions as `double3` (or `int3 sector + float3 local`) in a `WorldPosition` component for persistence, maps and AI.
- Simulate and render in a float **local frame per solar system** (Unity Physics is stable within a few km).
- Shift the origin with **one** ECS system that offsets every **root** `LocalTransform` (`WithNone<Parent>`): ships, debris, projectiles, asteroids, belts' data centres. It must also reset the `PhysicsGraphicalSmoothing` and interpolation history and the debris particle buffer. This fixes B10 and B18.
- An even simpler alternative: re-centre only on system entry, and keep each system within about 10 km.

### 6.9 Persistence

- Replace `List<VoxelData{string blockKey, string[] moduleSlots, ...}>` (about 170 B per voxel in YAML; `ship_fighter.asset` is 165 KB for 965 voxels) with a **binary blueprint**:
  - a block palette (registry key → local id);
  - per brick, either palette-indexed RLE or a raw `ushort[4096]` plus `byte` orientation, gzip or lz4-compressed;
  - a block instance table.
- Store damage as a per-brick removed-cell bitset diff against the blueprint.
- Cache blueprint blobs by a stable 64-bit content hash (`xxHash3` over the serialized bytes). This fixes B8.

### 6.10 System pipeline (target)

```
InitializationSystemGroup
  BlockDefsInitSystem            (blob from SOs; stable ids)
  ShipSpawnSystem                (blueprint → root + bricks + functional entities; cells filled by IJobParallelFor per brick)

SimulationSystemGroup
  FixedStepSimulationSystemGroup
    ShipThrustSystem             (functional thrusters → impulses)              [before PhysicsSystemGroup]
    ShipColliderSwapSystem       (install colliders built last frame; queue old blobs for disposal)
    PhysicsSystemGroup
    ShipContactDamageSystem      (collision events → DamageEvents, using impulse)
  WeaponFireSystem / ProjectileSystem / BeamSystem   → DamageEvents (grid DDA hit resolution)
  VoxelDamageApplySystem         (per-ship job; HP; CellDestroyedEvents; BrickDirty)
  ConnectivitySystem             (incremental per dirty brick; island union-find) → SplitRequests
  ShipSplitSystem                (structural changes via one ECB; new ships)
  DebrisSpawnSystem / DebrisSimulationSystem (Burst, pooled buffer)
  FunctionalBlockAggregationSystem (energy/thrust/shields for dirty ships)
  ShipMassUpdateSystem
  BrickRemeshSystem              (budgeted async jobs → MeshDataArray)
  BrickColliderBuildSystem       (budgeted async jobs → sub-compound + top compound)
  ShipLodSystem
  TransformSystemGroup           (only roots + bricks + functional blocks: O(bricks))
PresentationSystemGroup
  BrickMeshApplySystem (main thread apply, deferred mesh destroy)
  PropInstanceRenderSystem, DebrisRenderSystem (RenderMeshIndirect)
  EntitiesGraphicsSystem
  DeferredDisposalSystem (blobs/meshes released N frames later)
```

### 6.11 Target cost estimates [I]

For a 1M-cell capital ship (about 2.5k bricks, about 300 functional blocks):

| Item | Estimate |
|---|---|
| CPU memory | ~15–40 MB (cells + masks + meshes + colliders) |
| Entities | ~3k |
| Per-frame transform and render cost | ~3k matrices |
| Triangles | culled faces of the shell, ~1–2M, or ~0.2M at LOD1 |
| One explosive hit (radius 3 cells) | 1–4 dirty bricks: remesh ~0.2 ms, collider ~0.5 ms, connectivity ~0.1 ms, all off the main thread |
| Spawn | cells memcpy'd from the blueprint, plus ~2.5k brick meshes generated over a few frames (~50–100 ms total, async) |

### 6.12 Migration path

Existing pieces worth keeping or reusing:
- `BlockItem` authoring (`BlockSize`, `VoxelShape`, `Modifiers`).
- `BlockDatabaseBlob` / `BlockBlobBuilder`: extend into `BlockDefsBlob` with shapes, mass and a stable id.
- `ShipBlobBuilder`: becomes the blueprint-to-blob step.
- The ship-as-one-rigid-body and compound concept.
- Collision layers and filters.
- `ShipEnergyComponent`, `ThrusterComponent`, `WeaponComponent` and the shield components: these become functional-block data.
- The thruster-visual approach (`PostTransformMatrix` scaling).
- The BFS logic (the editor's multi-cell version in `ShipEditorManager.cs:889-930` is the correct one).
- `EcsToGameObjectSyncSystem`, until UI and camera read ECS directly.
- The culled-face mesher in `AsteroidConstructionJobSystem.cs:273-357`: port it to Burst and the `MeshDataArray` API.

Phases:

- **Phase 0, stabilise (1–3 days).** Apply the quick wins in §7: B2, B3, B4, B5, B7, B11, B12, the B1 interim fix, B6 and B9 dependencies. Delete the dead code in §4.
- **Phase 1, grid alongside entities.** Build `VoxelGrid` and bricks from the existing `ShipDataBlob` at construction, keeping the current voxel entities for rendering.
  - Switch hit resolution (projectiles, beams, explosions, rams) to grid DDA.
  - Move damage to the `DamageEvent` path.
  - Connectivity on the grid, with multi-cell support.
  - Voxel entities become pure visuals driven from grid state. Destroying a cell destroys its visual entity.
  - Result: correctness fixed, and the scans and races are removed.
- **Phase 2, brick rendering.**
  - Add the Burst brick mesher and brick entities.
  - Stop instantiating voxel entities for structural blocks.
  - Props and functional blocks keep the entity path (now hundreds, not thousands).
  - Debris particle pool with `RenderMeshIndirect`.
- **Phase 3, brick physics.**
  - Greedy-box brick sub-compounds and nested compound.
  - Incremental rebuild jobs, cleanup-component ownership, incremental mass.
  - Force-based movement.
- **Phase 4, splitting and LOD.**
  - Island split into new ships.
  - Render and physics LOD.
  - Dormant far ships.
- **Phase 5, persistence and world.**
  - Binary blueprints and damage diffs.
  - The ship editor uses the same grid and mesher (no GameObject per block), so massive builds are editable.
  - Single floating-origin system; per-system streaming of belts and stations.

---

## 7. Quick wins (small, high value)

1. **Fix the key-bit math** (B2): `VoxelPhysicsUtils.cs:63-72` becomes `return 32 - math.lzcnt((uint)count);`. Better: set `ColliderBlobInstance.Entity = voxel` at `ShipConstructionJobSystem.cs:380-387` and read the leaf entity.
2. **Dispose replaced compounds** (B3): in `ShipColliderRebuildSystem.cs:180`, dispose the old blob after the swap, deferred one step. Add a cleanup component so ship death also disposes it.
3. **Shields** (B4): add a `GameObject ShieldPrefab` singleton authoring (copy `ThrusterVisualAuthoring.cs`) and drop the `Prefab` and transform adds in `ShieldBubblePrefabAuthoring.cs:24-32`.
4. **Keep debris alive on ship death** (B5): stop appending voxels to the LEG (`ShipConstructionJobSystem.cs:321`), and destroy attached voxels via the `Child` buffer on death. Or remove them from the LEG in `DetachVoxel`.
5. **Do not reset the rebuild delay** (B7): in `VoxelDestructionSystem.cs:242`, add the component only if `!HasComponent`. Add `RequireForUpdate<ColliderRebuildRequired>()` to the rebuild system (B19d). Build children from `ShipVoxel.GridPosition * scale` instead of inverted LTWs (B19c).
6. **One shared child collider** (B19c and §5.2): create one box or sphere blob per ship and reuse it for every `ColliderBlobInstance`. Use `BoxCollider` (the spheres leave gaps at edges and corners).
7. **Interim multi-cell support** (B1): add shape cells to `BlockBlobData`, emit one collider child per rotated cell, and insert all cells into the connectivity map.
8. **Asteroid seed** (B11): `AsteroidLayoutJobSystem.cs:45` becomes `Random.CreateFromIndex((uint)seed)`.
9. **Asteroid ram damage** (B12): handle `AsteroidTag` before the null-voxel early-out at `ShipCollisionSystem.cs:132-137`.
10. **Blob cache key** (B8): resolve `runtimeBlockId` in place at load time (write back into the list). Hash rotation, colour, modules and stats with a 64-bit hash, or cache per `ShipData` instance. Call `ClearShipBlobCache` on world unload.
11. **Baker hygiene** (B9): `DependsOn(block)` and `DependsOn(block.BlockMesh)` for every BlockItem in `VoxelPrefabAuthoring.cs:78-96`. Use a stable hash or `FixedString` keys. Use `AddBlobAsset`.
12. **SceneTag** (B6): after instantiating voxels, or once on a runtime copy of the prefab, `RemoveComponent<SceneTag>()`. Alternatively despawn every ship on world-scene unload.
13. **Belt fixes** (B10): give belts a `LocalTransform` (or shift `CenterWorldPosition` in the origin shift), and destroy belts and asteroids in `EcsSolarSystemGenerator.DestroySystem`.
14. **Per-mesh `RenderBounds`** (B19b): put mesh AABBs into `BlockBlobData` and set `RenderBounds` per voxel at construction.
15. **Stop scanning every voxel each frame**: in `VoxelDestructionSystem`, use `WithChangeFilter<VoxelHealth>()` as a stopgap, or have the damage writers emit "died" events. In connectivity, gather voxels from the ship's `ShipVoxelIndex` buffer instead of a world query (`VoxelConnectivitySystem.cs:104-115`). Complete dependencies before main-thread `VoxelHealth` writes (B15).
16. **Movement** (B13): move `ShipMovementSystem` into `FixedStepSimulationSystemGroup` before `PhysicsSystemGroup`. Use `v *= math.exp(-drag*dt)`. Add the thrust delta instead of overwriting `PhysicsVelocity.Linear`.
17. **Delete dead code** (§4): about 400 lines plus two unused mesh assets.

---

## 8. Evidence index (selected greps and package-source checks)

- `grep -rn "\.Dispose()" Assets/Game` filtered to collider and blob: only temporary child blobs (`ShipConstructionJobSystem.cs:416`, `ShipColliderRebuildSystem.cs:196`), the asteroid pool (`AsteroidConstructionJobSystem.cs:65`), `ShipData._blobAsset` (`ShipData.cs:78,144`) and `GameDatabase.ClearShipBlobCache` (`GameDatabase.cs:86`, never called). No ship compound disposal.
- `ShieldBubblePrefabReference` appears only at `ShieldBubblePrefabAuthoring.cs:13` and `ShipConstructionJobSystem.cs:90`.
- `ObjectTransformSync` appears only at `GameObjectSyncSystem.cs:9,22,31` and a comment at `ShipBuildRequest.cs:20`.
- Script GUID usage:
  - `ShipDefinitionAuthoring` `70909ad3…`: none.
  - `GlobalShipScaleAuthoring` and `ThrusterVisualAuthoring`: `EntitiesSubscene.unity`.
  - `ShieldBubblePrefabAuthoring`: `ShieldEntityPrefab.prefab`.
  - `VoxelPrefabAuthoring`: `ShipVoxelEntityPrefab.prefab`.
  - `VoxelVisualData`: `Resources/GameSettings/VoxelVisualData.asset`.
- Unity Physics key bits: `com.unity.physics@8d312bc11767/Unity.Physics/Collision/Colliders/CompoundCollider.cs:1294` (`32 - lzcnt(NumChildren)`), `:328-332`, `Collider.cs:1526-1547`.
- Compound child `Entity` field: `CompoundCollider.cs:422-439`.
- ECB add-on-existing sets the value: `com.unity.entities@b4620332ec2d/Unity.Entities/EntityCommandBufferPlayback.cs:862-874`.
- LEG destroy skips missing entities: `EntityComponentStoreCreateDestroyEntities.cs:121-149`.
- `SceneTag` on all loaded entities including prefabs: `Unity.Scenes/AsyncLoadSceneOperation.cs:163`. Unload destroys by `SceneTag` with `IncludePrefab`: `SceneSectionStreamingSystem.cs:189-193, 893-895`. Instantiate keeps shared components: `EntityComponentStoreCreateArchetype.cs:121`.
- Shader instancing: `MetallicShipShader.shadergraph`, `_BaseColor` with `overrideHLSLDeclaration: true`, `hlslDeclarationOverride: 3`.
- Shipped ship sizes: 50–965 voxels each (`Assets/Resources/Items/Ships/*.asset`, counted by `blockKey:`). Mesh triangles: `hull_1x1` = 12, `flak_cannon_3x3x3` = 2398, `missile_launcher_2x1` = 3266 (`shipVoxels.obj`).
