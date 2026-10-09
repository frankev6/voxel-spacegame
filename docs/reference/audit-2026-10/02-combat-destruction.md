# Audit 02: Combat, physics, destruction, weapons, AI, abilities

**Scope.** Everything under `Assets/Game/Core/Combat/` (root, `AI/`, `Abilities/`), plus:
- `Assets/Game/Core/AsteroidMiner.cs`
- `Assets/Game/Core/CameraShake*.cs`
- `Assets/Game/Core/Game Logic/Projectile.cs`
- `Assets/Game/Prefabs/Projectiles/*`

I also read the parts of `Assets/Game/Core/VoxelShip/` that destruction touches:
- `ShipConstructionJobSystem`, `ShipColliderRebuildSystem`, `VoxelPhysicsUtils`
- `ShipMovementSystem`, `ShieldBubblePrefabAuthoring`, `ShipData` structs

**Method.**
- I read every in-scope file in full.
- I grepped the whole `Assets/Game` tree (code, `.unity`, `.prefab`, `.asset`) by type name and by script GUID from `.meta` files.
- I checked engine behaviour against the installed package sources in `Library/PackageCache` (Unity Physics `8d312bc11767`, Entities `b4620332ec2d`).
- I did not run the game. The editor log in `Logs/Editor.log` only covers today's re-import and contains no play-mode output.

**Legend.**
- **[V]** verified: read in code, asset or package source, with a file:line reference.
- **[I]** inferred: reasoning about runtime behaviour that I did not observe.
- Confidence is given per finding.

**Cross-reference.** The voxel-core auditor (`01-voxel-core.md`) found several of the same defects independently. Where we overlap I give their ID, for example "(= 01-B2)". Their §6 defines the target voxel storage (16³ "bricks", cells as data). My target architecture (§5) is written against that contract and does not redefine it.

---

## 0. TL;DR

1. **Combat cannot currently happen in the game [V].**
   - The only `CombatManager.SpawnShip` call spawns the player (`Assets/Game/Core/Galaxy Map/OpenWorldGalaxyManager.cs:399-405`).
   - `EncounterManager.StartEncounter` rejects every non-narrative encounter with an error (`Assets/Game/Core/Managers/EncounterManager.cs:177-192`).
   - So no AI ship ever exists. AI, ship-vs-ship weapons, ramming, shields and abilities are all dormant.
   - Today the only live damage paths are player weapons against asteroids, and splash damage that can hit the player's own voxels.
   - This makes a rewrite cheap: nothing in production depends on the current combat behaviour.
2. **The core pipeline has correctness bugs that will surface the moment combat is re-enabled:**
   - Hits resolve to the wrong voxel whenever a ship's compound collider has a power-of-two child count (C01, = 01-B2).
   - Physics events are consumed outside the fixed step, so ramming damage scales with FPS and projectile hits are dropped below 60 FPS (C02).
   - Projectile damage is applied around the post-step projectile position, with a splash radius smaller than the distance a bullet travels in one step. Many hits do zero damage (C03).
   - Ship death deletes the whole ship and all its in-flight debris in one frame (C04, = 01-B5).
   - Ship death can also make ECB playback throw ("entity does not exist") in the editor, or touch destroyed entities in release builds (C05).
3. **Destruction does not match the vision.**
   - Every destroyed voxel becomes its own physics body, with random velocity that ignores the ship's motion and the impact.
   - Disconnected sections dissolve into hundreds of single-voxel bodies instead of drifting off as hulks.
   - Under sustained fire the compound collider never rebuilds, so destroyed voxels stay as invisible blockers (C07, = 01-B7).
   - Collider blobs leak (C06, = 01-B3).
4. **Dead or unreachable features [V]:**
   - Shields can never be created (= 01-B4) and are never switched on.
   - Abilities are never granted, equipped or activated.
   - Flak cannons never fire (no prefab mapping).
   - Missile and flak base damage is 0.
   - Camera shake has no instance in any scene.
   - Asteroid "debris" is invisible and does not move.
   - Scrap magnetism is an empty system.
   - Mirror "beam combining" is never consumed.
   - Portal cooldown is commented out.
   - About 20 dead types and fields are listed in §3.
5. **Headline recommendation (§5):**
   - Rebuild combat as a **fixed-step, event-driven pipeline**: projectile raycasts → `DamageEvent` stream → one race-free damage resolver per ship → grid-space cell removal → incremental per-ship connectivity → island split into **hulk ships** or pooled **GPU debris**.
   - Ramming becomes an impulse-to-energy model.
   - Physics feedback is preserved by driving ships with forces instead of overwriting velocity.
   - VFX and audio are batched events (VFX Graph via `GraphicsBuffer`), not GameObjects per hit.
   - AI uses a spatial hash, staggered utility decisions and steering.
   - Abilities become blob-backed data plus effect systems.
   - Keep the good ideas (FireWeaponTag enableable, energy aggregation, turret math, cached collider singletons, personality presets). Replace almost everything else.

---

## 1. Current architecture map

### 1.1 What actually runs today (runtime reality) [V]

| Feature | Status | Evidence |
|---|---|---|
| Player ECS ship | Spawned | `OpenWorldGalaxyManager.cs:399-405` → `CombatManager.cs:43-68` |
| Enemy / AI ECS ships | **Never spawned** | Only `SpawnShip` call is `isPlayer: true` (grep). `EncounterManager.cs:187-191` logs "Encounter is not a NarrativeEncounter class!" and ends any `CombatEncounter`. Combat encounter assets with formations exist but are unused (`Assets/Resources/Encounters/Combat/encounter_combat_01.asset`). |
| Player weapons vs asteroids | Works (with C03 caveats) | `EntityLifetimeSystem.cs:114-117, 231-235`; `BeamSystem.cs:323-349` |
| Ship–asteroid ramming damage | **Never applied** | `ResolveVoxel` returns Null for asteroids (no `ShipVoxelIndex`), so the event is dropped at `ShipCollisionSystem.cs:135-137` (= 01-B12) |
| Shields | **Never exist, never on** | Prefab singleton lookup always fails (`ShipConstructionJobSystem.cs:87-92`, `ShieldBubblePrefabAuthoring.cs:26-27`; `ShieldBubblePrefabReference` is never added). `ShipEnergyComponent` is spawned disabled (`ShipConstructionJobSystem.cs:428`). Only `ShieldToggleAbilitySO.cs:138` enables it, and that ability is never used. |
| Abilities | **Never granted or used** | `AbilityController.AddAbility/EquipAbility/TryUseAbility` have no callers outside `AbilityController.cs` (grep) |
| Flak cannon | **Never fires** | `projectile_flak_shell.asset` has `VisualType: 6`. The database maps only types 0–5 (`Prefabs/Projectiles/ProjectileDatabaseAuthoring.prefab`). The null prefab branch then resets cooldown (`WeaponFireSystem.cs:153-167`). |
| Camera shake on ship death | **Never** | `CameraShake`/`CameraShakeController` are in no scene or prefab (GUID grep), so `ShipHealthSystem.cs:41-42` is always skipped |

### 1.2 System inventory [V]

Groups and ordering come from attributes. "Unordered" means no constraint relative to the other combat systems.

| System | File | Group / ordering | Kind | Jobs / Burst | Structural changes |
|---|---|---|---|---|---|
| `PhysicsSetup` | `Combat/PhysicsSetup.cs:7-24` | Initialization, OnCreate only | ISystem | – | Creates `PhysicsStep` singleton |
| `EnsurePhysicsSimulateSystem` | `Combat/EnsurePhysicsSimulateSystem.cs:7-32` | PhysicsBuildWorldGroup, before `BuildPhysicsWorld` | ISystem | main thread | `AddComponent<Simulate>(query)` (never matches, see §3) |
| `ShipColliderRebuildSystem` | `VoxelShip/ShipColliderRebuildSystem.cs:25-77` | **FixedStep**, before `PhysicsSystemGroup` | ISystem, no Burst | main thread, `CompleteDependency` every step (`:35`) | `RemoveComponent<ColliderRebuildRequired>` |
| `ShipCollisionSystem_Managed` | `Combat/ShipCollisionSystem.cs:40-102` | **FixedStep**, after `PhysicsSystemGroup` | SystemBase | `ICollisionEventsJob` + `Complete()` (`:91-92`) | none (fills singleton buffer) |
| `ShipConstraintSystem` | `VoxelShip/ShipMovementSystem.cs:192-235` | FixedStep, after physics | ISystem | Burst parallel | – |
| `WeaponInputSystem` | `Combat/WeaponInputSystem.cs:7-98` | Sim, before `WeaponTurretSystem` | ISystem (managed calls) | main thread | lazy singleton creation |
| `WeaponTurretSystem` | `Combat/WeaponTurretSystem.cs:7-247` | Sim, before `AIShipFindTargetSystem` | ISystem | Burst `ScheduleParallel` | – |
| `AIShipFindTargetSystem` | `Combat/AI/AIShipFindTargetSystem.cs:13-200` | Sim, unordered | ISystem | Burst parallel, O(N²) | – |
| `AIShipNavigationSystem` | `Combat/AI/AIShipNavigationSystem.cs:13-291` | Sim, after FindTarget, before `ShipMovementSystem` | ISystem | Burst parallel | – |
| `WeaponFireSystem` | `Combat/WeaponFireSystem.cs:13-363` | Sim, **unordered** (NetCode filter flags `:14-15`) | ISystem | Burst parallel | **BeginSim ECB**: Instantiate projectile + adds |
| `ProjectileHomingSystem` | `Combat/ProjectileHomingSystem.cs:8-134` | Sim, unordered | ISystem | Burst parallel + `Complete()` (`:47`) | – |
| `EnergySystem` | `Combat/EnergySystem.cs:9-64` | Sim, before `ShieldSystem` | ISystem | Burst parallel | – |
| `ShieldFlashDecaySystem` | `Combat/ShieldSystem.cs:147-183` | Sim, before `ShieldSystem` | ISystem | Burst parallel | – |
| `ShieldSystem` | `Combat/ShieldSystem.cs:12-141` | Sim, after Energy, before ProjectileCollision | ISystem | Burst parallel, O(P×E) | **EndSim ECB** destroy projectile |
| `BeamSystem` | `Combat/BeamSystem.cs:10-368` | Sim, after Energy | ISystem | Burst parallel | BeginSim ECB (FX events, asteroid death) |
| `ProjectileCollisionSystem` | `Combat/ProjectileCollisionSystem.cs:24-116` | Sim (**variable rate**), unordered | ISystem | `ITriggerEventsJob` | – (sets `HasHit` flag) |
| `EntityLifetimeSystem` | `Combat/EntityLifetimeSystem.cs:10-302` | Sim, **unordered** | ISystem | Burst parallel | BeginSim ECB: destroy, FX events, area damage effects, asteroid debris |
| `ShipCollisionDamageSystem` | `Combat/ShipCollisionSystem.cs:167-279` | Sim, after FixedStep and Lifetime | ISystem | main thread Burst, `CompleteDependency` (`:191`) | BeginSim ECB (sound events) |
| `VoxelConnectivitySystem` | `Combat/VoxelConnectivitySystem.cs:12-188` | Sim, before VoxelDestruction | ISystem | main thread Burst | – (writes `VoxelHealth`) |
| `VoxelDestructionSystem` | `Combat/VoxelDestructionSystem.cs:14-345` | Sim, after Beam and ProjectileCollision | ISystem | Burst single-thread `Schedule` over **all voxels** | BeginSim ECB, about 5 archetype moves per voxel |
| `ShipHealthSystem` | `Combat/ShipHealthSystem.cs:5-47` | Sim, after VoxelDestruction | SystemBase | main thread, calls MonoBehaviours | BeginSim ECB `Lifetime=0` on ship |
| `PortalTransformUpdateSystem` / `PortalTeleportSystem` | `Combat/PortalSystem.cs:11-145` | Sim, before `TransformSystemGroup` | ISystem | Burst parallel | (ECB created, never used) |
| `ScrapMagnetismSystem` | `Combat/ScrapMagnetismSystem.cs:9-85` | Sim | SystemBase | **empty OnUpdate** | – |
| `ProjectileHitVisualSystem` | `Combat/ProjectileHitVisualSystem.cs:5-37` | Presentation | SystemBase | main thread → GameObject pool | temp ECB destroy events |
| `CombatHudSystem` | `Combat/CombatHudSystem.cs:9-94` | Presentation | SystemBase | main thread, UI push | – |
| `ShipUISyncSystem` | `Combat/ShipUISyncSystem.cs:4-29` | Presentation | SystemBase | main thread, change filter | – |
| `ShieldBubbleVisibilitySystem` | `Combat/ShieldVisualSystem.cs:11-66` | Presentation | ISystem | Burst parallel | – |
| `PlayerWeaponArcSystem` | `Combat/PlayerWeaponArcSystem.cs:20-130` | Presentation | SystemBase | main thread | `EntityManager.CreateEntity` per player weapon (once) |

**Ordering facts that matter [V]:**
- Unconstrained systems in a group are ordered by **system type hash** (`Library/PackageCache/com.unity.entities@b4620332ec2d/Unity.Entities/ComponentSystemSorter.cs:128-131`). The order is deterministic but arbitrary, and renaming a class can reorder gameplay.
- `TransformSystemGroup` has no `UpdateInGroup` or ordering in Entities 6.6 (`Unity.Transforms/EndFrameTransformSystems.cs:12-13`). It therefore sits at a hash-determined position among the game systems. Whether `WeaponFireSystem`, `BeamSystem`, `ShieldSystem` and the AI read this frame's or last frame's `LocalToWorld` is accidental.
- `FixedStepSimulationSystemGroup` is `OrderFirst` in `SimulationSystemGroup` (`Unity.Entities/DefaultWorld.cs:327-329`), at 60 Hz with catch-up (`:351-352`). It runs 0, 1 or N times per frame.

### 1.3 Frame timeline (as it executes today) [V]

```
Initialization
  ShipConstructionJobSystem (BeginInit ECB) → ShipConstructionLinkSystem (EndInit ECB)
Simulation
  BeginSimulationECB.playback   ← all combat structural changes from LAST frame land here
  FixedStep ×(0..N)             60 Hz
    ShipColliderRebuildSystem   (main thread, full compound rebuild)
    PhysicsSystemGroup          (Build → Simulate → Export)
    ShipCollisionSystem_Managed (clears + refills ShipCollisionEventElement buffer)
    ShipConstraintSystem
  [variable-rate, hash-ordered except the constraints above]
    WeaponInput → WeaponTurret → AIFindTarget → AINavigation → ShipMovement (overwrites PhysicsVelocity)
    WeaponFireSystem (ECB → next frame)
    Energy → ShieldFlashDecay → Shield (EndSim ECB) → ProjectileCollision (reads trigger events of the LAST step only)
    BeamSystem, EntityLifetimeSystem (damage, explosions, destroy → next frame)
    ShipCollisionDamageSystem (reads the buffer; never clears it)
    VoxelConnectivity → VoxelDestruction (ECB → next frame) → ShipHealth (ECB → next frame)
    TransformSystemGroup (somewhere in here)
  EndSimulationECB.playback     ← only ShieldSystem's projectile destroys
Presentation
  ProjectileHitVisualSystem (GameObject per hit), AudioSystem, CombatHud, ShipUISync, ShieldBubbleVisibility, PlayerWeaponArc
```

### 1.4 Data-flow traces [V unless marked]

**A. Ballistic or missile weapon → voxel removal**

1. `WeaponTurretSystem` picks the closest entry of the ship's `PotentialTargetElement` buffer (`WeaponTurretSystem.cs:93-121`).
   - That buffer is refreshed every 0.5 s by `AIShipFindTargetSystem.cs:82-86`.
   - The turret rotates the local `LocalTransform` (`:159-185`) and enables `FireWeaponTag` for AI and autofire (`:218-227`).
   - For manual player fire, `WeaponInputSystem.cs:89-97` sets the tag from input.
2. `WeaponFireSystem` handles cooldown, burst and energy (`:126-189`), then records into the **BeginSim ECB**:
   - `Instantiate(ProjectilePrefab)`, `AddComponent<Simulate>`, `LocalTransform`, `ProjectileComponent`, the cached trigger `PhysicsCollider`, `PhysicsVelocity` and `LifetimeComponent`.
   - Optionally `ExplodeOnDeath` and `HomingComponent`.
   - A `PlaySoundEvent` entity (`:191-340`).
   - The projectile appears **next frame**, at a position computed from this frame's (possibly last frame's) `LocalToWorld`.
3. The projectile is a **kinematic body**: `PhysicsVelocity` with no `PhysicsMass` (`Library/PackageCache/com.unity.physics@8d312bc11767/Unity.Physics/ECS/Base/Systems/PhysicsWorldBuilder.cs:831`). Its sphere trigger filter is Projectile → Voxel (`WeaponFireSystem.cs:63-74`).
4. The physics step raises a `TriggerEvent` with the ship's compound body.
   - `ProjectileCollisionSystem` (variable rate) marks `HasHit` and skips the shooter by `shipId` (`:89-96`).
   - It stores `HitColliderKey`, and `HitPosition = projectile's current LocalTransform` (`:103-111`). That is the position *after* the step.
5. `EntityLifetimeSystem.LifetimeJob` sees `HasHit` and branches:
   - If `SplashRadius > 0` (true for **every** shipped projectile; see C03), it runs `ApplyAreaDamage`: `PhysicsWorld.OverlapSphere(hitPos, r)` → `ResolveVoxel(hit.Entity, hit.ColliderKey)` → `VoxelHealth.Health -= dmg × typeMultiplier` (`:223-255`).
   - Otherwise it does direct damage via `ResolveVoxel` (`:108-137`).
   - It creates a `ProjectileHitVisualEvent` entity and records `DestroyEntity(projectile)`, landing next frame.
6. `VoxelDestructionSystem` scans every attached voxel. For `Health <= 0`, `DetachVoxel` (`:209-343`):
   - `ShipHealth -= 1`
   - `ConnectivityCheckState.CheckPending = true`
   - nulls the `ShipVoxelIndex` slot
   - ECB `AddComponent(ship, ColliderRebuildRequired{0.1})`
   - subtracts reactor, capacitor and shield energy
   - destroys the voxel's children
   - ECB: `RemoveComponent<Parent>`, the scrap component set, sphere `PhysicsCollider`, `PhysicsMass(50)`, world `LocalTransform`, random XZ velocity of 2–8, damping, `Lifetime 8 s`, `ScrapTag`.
7. Next frame (after the 0.2 s throttle, one ship per frame), `VoxelConnectivitySystem` BFS-es from the core over **all voxels in the world filtered by parent** (`:104-115`) and sets `Health = 0` on unreachable voxels (`:165-179`). Step 6 then turns each of them into its own debris body.
8. `ShipColliderRebuildSystem`, 0.1 s after the **last** destruction (the timer is reset on every destruction; C07), rebuilds the whole sphere compound from the surviving voxels' `LocalToWorld`. It compacts `ShipVoxelIndex` and `ShipVoxel.VoxelBufferIndex` (`:79-202`), and leaks the old blob (C06).
9. `ShipHealthSystem`: when `ShipHealth < 40% MaxHealth` (`:29-30`) it sets `isDestroyed`, adds `Lifetime=0` to the ship, and calls:
   - `CombatManager.DestroyShip` → `MarkShipForSinking` and `CombatShip.SinkShip` → `Destroy(go, 5f)`
   - `ParticleSpawner.SpawnShipExplosion`
   - `CameraShakeController` (always null)

   Next frame `EntityLifetimeSystem` applies the ship's own `ExplodeOnDeath` (radius 1.5, damage 200 HE, from `ShipConstructionJobSystem.cs:220-226`) and destroys the ship. That destroy follows the `LinkedEntityGroup`, which contains **every voxel ever spawned, including in-flight debris** (`ShipConstructionJobSystem.cs:264-265, 321`).

**B. Beam weapon**

1. `WeaponFireSystem` instantiates the laser prefab as a **child of the weapon voxel**, with `BeamComponent{DPS = Damage×FireRate}` and lifetime `1/FireRate + 0.05` (`:194-224`).
2. Each frame `BeamSystem`:
   - drains `EnergyCost × dt` from the owner (`:166-173`)
   - ray-sphere tests every enabled shield emitter (`:186-220`)
   - runs an all-hits `CastRay` over `MaxRange` and skips own-ship voxels (`:228-273`)
   - scales the beam with `PostTransformMatrix` (`:283-288`)
   - writes `VoxelHealth`, asteroid health or shield energy from a parallel job (`:306-364`).

**C. Ship ramming**

1. In each fixed step, `ShipCollisionSystem_Managed` keeps only body pairs where both are ShipMaster or Asteroid (`:120-125`) and resolves both voxels; asteroids resolve to Null and are dropped (`:132-137`).
2. Impact speed is `|vA − vB|` of **post-solve linear velocities** (`:139-148`).
3. It enqueues events and copies them into a singleton buffer after `Complete()` (`:91-98`).
4. In the variable-rate group, `ShipCollisionDamageSystem` applies `(speed − 0.1) × 10` to both voxels and creates one sound entity **per event** (`:207-249`).
5. `ShipMovementSystem` then **overwrites** `PhysicsVelocity.Linear/Angular` from its arcade model every frame (`VoxelShip/ShipMovementSystem.cs:119, 181`), erasing the collision response (= 01-B13).

**D. Energy and shields:** `EnergySystem` regenerates, drains passively while `ShipEnergyComponent` is *enabled*, clamps, and auto-disables at 10% (`:43-62`). The enabled bit of a data component is used to mean "shields on". Shields never exist (see §1.1).

**E. AI:**
1. `AIShipFindTargetSystem` scans all ships per ship (O(N²)), at most every 0.5 s per ship. It finds the closest enemy (no range limit) and a random child voxel, and computes avoidance and outnumber counts (`:122-198`).
2. `AIShipNavigationSystem` orbits at `PreferredRange`, backs off when closer than 70%, and flees on health or outnumber. It writes `ShipInputComponent` (`:53-165`).
3. Movement uses only `TurnInput/ThrustInput/StrafeInput` (`ShipMovementSystem.cs:113, 146-155`).

**F. Abilities:**
- `AbilityController` (MonoBehaviour) is meant to hold SO *instances* (`AbilityController.cs:50-65`) and tick them in `Update` (`:37-44`).
- The SOs poke `EntityManager` directly (`BoostAbilitySO.cs:101-133`, `ShieldToggleAbilitySO.cs:123-172`).
- `CombatHudSystem` reads `EquippedAbilities` every frame (`CombatHudSystem.cs:45-70`). It is always empty.

### 1.5 Physics configuration [V]

| Body | BelongsTo | CollidesWith | Response | Motion |
|---|---|---|---|---|
| Ship compound (one sphere child per voxel, r = 0.5·scale) | Voxel | Projectile, Voxel; GroupIndex = −shipCollisionId | CollideRaiseCollisionEvents, friction 0, restitution 0 (default-initialised `Material`) | dynamic, inertia X/Z locked (`ShipConstructionJobSystem.cs:297-411`) |
| Asteroid compound (box children) | Voxel | Projectile, Voxel | CollideRaiseCollisionEvents | dynamic (`AsteroidConstructionJobSystem.cs:148-161, 406-428`) |
| Projectile sphere (r = 0.5·0.5·4 = 1.0 × body scale 1.0) | Projectile | Voxel | RaiseTriggerEvents | kinematic (no `PhysicsMass`) |
| Debris voxel sphere (r = 1·scale × body scale 0.5 = 0.25) | **Projectile** | Voxel | Collide | dynamic, mass 50 |
| Beam entity | – (no collider) | – | – | kinematic body (child entity, `PhysicsVelocity` baked by `ProjectileAuthoring`) |

Consequences of the filters:
- Projectiles can never hit debris, and debris never collides with debris.
- Debris collides with ships and asteroids, including the ghost collider of the cell it just left.
- `OverlapSphere` explosions cannot see debris, so the `ScrapTag` branches in `EntityLifetimeSystem` are unreachable (§3).

**Step settings.** `PhysicsSetup` creates `PhysicsStep{UnityPhysics, gravity 0, 4 iterations, MT}` from an object initializer (`PhysicsSetup.cs:15-21`). Every other field stays zero:
- `CollisionTolerance = 0` (default is 0.01)
- `SolverStabilizationHeuristicSettings` and `DirectSolverSettings` zeroed
- substeps clamped to 1 by the engine (`Unity.Physics/Dynamics/Simulation/Simulation.cs:307`)

The `PhysicsStepAuthoring` in `GalaxyMapScene.unity:165` sits in a regular scene, not a subscene, so it is never baked (dead config).

`GlobalShipScale` is 0.5, with projectile scale 4 (`Assets/Game/Scenes/EntitiesSubscene.unity:310-311`).

### 1.6 ECB and event patterns [V]

- **BeginSimulation ECB** (played at the start of the *next* frame): WeaponFire, EntityLifetime, VoxelDestruction, Beam, ShipCollisionDamage, ShipHealth, PortalTeleport (unused). Every spawn, destroy and detach therefore has one frame of latency.
- **EndSimulation ECB:** only `ShieldSystem` (`:39-40`).
- **Temp ECB with immediate playback:** `ProjectileHitVisualSystem` and `AudioSystem` (both main thread).
- **Events modelled as:**
  - entities: `ProjectileHitVisualEvent`, `PlaySoundEvent`
  - flags: `ProjectileComponent.HasHit`, `ConnectivityCheckState.CheckPending`
  - a tag: `ColliderRebuildRequired`
  - a singleton buffer: `ShipCollisionEventElement`

  There is no central damage or event bus. Four systems write `VoxelHealth` directly: Lifetime, Beam, ShipCollisionDamage, Connectivity.

### 1.7 Managed ↔ unmanaged boundaries [V]

| Direction | Bridge |
|---|---|
| MB → ECS | `CombatManager.CreateShipEntity` creates a `ShipBuildRequest` plus managed `ShipBuildRequestManaged` (`CombatManager.cs:74-104`). `ShipConstructionLinkSystem` back-fills `CombatShip.ShipEntity` and adds the managed `ShipUIReference`. |
| ECS → MB | `ShipHealthSystem` → `CombatManager.DestroyShip`, `ParticleSpawner`, `CameraShakeController` (`:35-42`). `ShipUISyncSystem` → `CombatShip.UpdateHealth`. `CombatHudSystem` → `UIManager.galaxyView`. `ProjectileHitVisualSystem` → `ParticleSpawner`/`SimplePool` and `AudioManager`. |
| Input | `WeaponInputSystem` reads `InputManager.Instance`, `Camera.main`, and legacy `Input.GetKey` as fallback (`:28-87`). Active input handling is "Both" (`ProjectSettings/ProjectSettings.asset:808`). |
| Abilities | ScriptableObject instances call `World.DefaultGameObjectInjectionWorld.EntityManager` directly. |
| Dual physics | The player wrapper prefab still carries PhysX components (SphereCollider, `OpenWorldPlayerController`) next to the Unity Physics ship (`Assets/Game/Prefabs/NEW COMBAT SHIP PLAYER.prefab`; component list via GUID scan). |

---

## 2. Bugs

Severity reflects impact **once combat is re-enabled**. Items marked *(latent)* cannot trigger today because no enemy ships spawn (§1.1).

| ID | Severity | Conf. | Title |
|---|---|---|---|
| C01 | High | High | Hit → voxel resolution wrong for power-of-two compound sizes (= 01-B2) |
| C02 | High | High | Physics events consumed outside the fixed step: ram damage ∝ FPS; hits dropped below 60 FPS |
| C03 | High | Med-High | Projectile damage centred on post-step position with radius < step travel → many zero-damage hits |
| C04 | High | High | Ship death deletes the ship and all its flying debris in one frame (= 01-B5) |
| C05 | High *(latent)* | Medium | Ship death can throw during ECB playback / touch destroyed entities in builds |
| C06 | High at scale | High | Compound collider blobs leak (= 01-B3) |
| C07 | Med-High | High | Collider rebuild starves under sustained fire → invisible blockers (= 01-B7) |
| C08 | Medium | High | Parallel read-modify-write races on health, energy, mirror |
| C09 | Med-High | High / Med | Ramming model: velocity overwrite, post-solve speed, per-step grinding, audio flood |
| C10 | Medium | High | Ship ↔ asteroid collisions never damage (= 01-B12) |
| C11 | Medium | High / Med | Debris ignores ship velocity and impact; spawns inside its own ghost collider |
| C12 | Medium | High | Severed sections become hundreds of 1-voxel bodies; coreless ships never split (partly = 01-B17) |
| C13 | High (feature) | High | No path spawns enemy ECS ships |
| C14 | Medium | High | Shields can never exist (= 01-B4) or be enabled |
| C15 | Medium | High | Abilities never granted, equipped or activated; Overclock is a copy of Boost |
| C16 | Medium | High | Flak cannon never fires; missile and flak deal 0 base damage |
| C17 | Medium *(latent)* | High | Homing missiles lock onto allies and neutrals |
| C18 | Medium | High | Fire rate is frame-rate dependent and capped at one shot per frame |
| C19 | Low-Med | High | Projectiles spawn a frame late, from stale transforms, without shooter velocity |
| C20 | Low-Med | Medium | Vertical spread in a planar game makes low-accuracy weapons miss |
| C21 | Medium | High / Med | Splash ignores shooter and allegiance; asteroid splash multiplied per collider leaf |
| C22 | Medium | High / Med | Beam defects: double energy cost, no out-of-energy stop, overlap double damage, orphaned beams, constant FX RNG |
| C23 | Low *(dormant)* | High | Beam prefab is baked as a "projectile" + kinematic body; shield and portal logic treat its local position as world |
| C24 | Low *(dormant)* | Medium | Shield absorb and hull hit can both apply for the same projectile |
| C25 | Medium *(latent)* | High | AI logic: unreachable disengage, endless flee, wrong roll maths, out-of-range Timid, no sensor range |
| C26 | Medium *(latent)* | High | Player death is not handled; sink state is dead |
| C27 | Low-Med | Medium | `UnityEngine.Time.frameCount` inside Burst-compiled `OnUpdate` |
| C28 | Medium | Medium | Main-thread `VoxelHealth` write without completing writers (= 01-B15) |
| C29 | Medium (perf) | High | Hit-VFX "pool" degenerates to Instantiate/Destroy per hit; ship-explosion objects never released; missing null check |
| C30 | Medium | Medium | Hash-dependent order between fire, turret, lifetime, collision and destruction; stale collider keys |
| C31 | Low | High | `PhysicsStep` partially initialised; scene `PhysicsStepAuthoring` never baked |
| C32 | Medium | High / Med | Sphere colliders per voxel: holes at edges and corners, bumpy contacts; multi-cell blocks get one sphere (01-B1) |
| C33 | Low | High | `ShipEnergyComponent` enable bit means "shields on": a query hazard |
| C34 | Low | High | Minor defects (log spam, per-frame GC, comments that lie, etc.) |

### C01. Wrong voxel resolved from `ColliderKey` (= 01-B2). High [V]

- `VoxelPhysicsUtils.cs:37-39, 63-72` uses `bits = 32 − lzcnt(count − 1)`, and 1 when count ≤ 2.
- Unity Physics uses `NumColliderKeyBits => 32 − lzcnt(NumChildren)` (`Library/PackageCache/com.unity.physics@8d312bc11767/Unity.Physics/Collision/Colliders/CompoundCollider.cs:1294`) and stores the child index in the top bits (`Collider.cs:1526-1547`).
- For `NumChildren = 2^k` the utility returns `index >> 1`. That index is always in bounds, so no warning fires.
- Effects:
  - Wrong voxels take damage.
  - The upper half of the buffer is invulnerable until the next rebuild changes the count.
  - Debris spawns away from the impact.
- All callers are affected: `EntityLifetimeSystem.cs:121, 238`, `BeamSystem.cs:255`, `ShipCollisionSystem.cs:132-133`.
- **Fix:** `32 - lzcnt((uint)count)`. Better: set `ColliderBlobInstance.Entity` (`CompoundCollider.cs:422-439`) and read the leaf. Best: resolve in grid space from the hit point (§5).

### C02. Physics events consumed outside the fixed step. High [V code, I magnitude]

- **Ramming [V]:**
  - The producer clears and refills the buffer **each fixed step** (`ShipCollisionSystem.cs:71-72`).
  - The consumer runs in the variable-rate group (`:167-169`) and never clears the buffer.
  - **[I]** At 144 FPS about 58% of frames run no fixed step, so the stale events are applied again, about 2.4× per step on average.
  - **[I]** At 30 FPS two steps run per frame, and the first step's events are overwritten before they are consumed (about 0.5×).
  - Ram damage therefore scales linearly with frame rate.
- **Projectiles [V]:**
  - `ProjectileCollisionSystem` schedules `ITriggerEventsJob` from `SimulationSystemGroup` (`:24, 55`). It only sees the **last** step's events.
  - Below 60 FPS, a trigger from an earlier step in the same frame is lost, and the projectile may already be past the hull.
  - Above 60 FPS the same events are re-read; `HasHit` (`:89`) makes that idempotent.
- **Fix:** consume events in `FixedStepSimulationSystemGroup` right after `PhysicsSimulationGroup`. Better: stop using triggers for projectiles (§5.4).

### C03. Projectile damage misses even when the hit registers. High [V data, I geometry]

**[V] Facts:**
- Every shipped projectile has `ExplosionRadius > 0`: bullet 0.5, others 1 (`Assets/Resources/Items/Blocks/Weapons/Projectiles/*.asset`). So **all** hits take the `ApplyAreaDamage` path (`EntityLifetimeSystem.cs:102-107`). The direct-hit branch (`:108-137`) is dead for current content.
- The overlap is centred on `HitPosition`, which is the projectile transform **after** the physics step (`ProjectileCollisionSystem.cs:103-107`, comment "simplified for Triggers").
- `ExplosionRadius` is not scaled by ship scale (`ShipConstructionJobSystem.cs:508`). Voxel sphere radius is 0.25 world units. Projectile speed is 150 u/s, which is 2.5 u per 60 Hz step.

**[I] Consequence:**
- The trigger fires when the r = 1.0 projectile sphere reaches a voxel sphere. The splash then searches a 0.5–1.0 radius around a point up to about 2.5 u further on.
- For hulls 1–4 voxels thick (0.5–2 u), that point is often past the hull. The overlap finds nothing, yet the hit FX plays and the projectile is destroyed.
- Damage per shot becomes random and frame-phase dependent.

**Fix:** raycast or sphere-cast from the previous to the current position and use the exact hit point and normal (§5.4). Scale splash radii by cell size.

### C04. Death deletes the ship and all its debris instantly (= 01-B5). High (vision) [V]

- `ShipHealthSystem.cs:29-33` adds `Lifetime=0`, and `EntityLifetimeSystem.cs:172-175` destroys the ship root.
- The root's LEG holds every voxel spawned (`ShipConstructionJobSystem.cs:264-265, 321`). `DetachVoxel` never removes entries (`VoxelDestructionSystem.cs:209-343`).
- So up to 60% of a ship (everything above the 40% death threshold) disappears in one frame, along with every debris piece still flying from it.
- The only feedback is one pooled particle effect.
- `MarkShipForSinking` (`CombatManager.cs:114-140`) and `ShipMovementSystem`'s sink branch (`:46-51`) never get to run, because the entity is gone next frame.

### C05. ECB playback hazard on ship death. High *(latent)* [V mechanism, I ordering]

- **Frame N+1 [V]:** `EntityLifetimeSystem` processes the ship's `Lifetime=0`.
  - It applies the ship's `ExplodeOnDeath` (radius 1.5, 200 HE ×2 = 400) to **its own voxels** through `ApplyAreaDamage`. That is an immediate `VoxelHealth` write (`:158-161`).
  - It records `DestroyEntity(ship)`.
- If `VoxelDestructionSystem` runs after `EntityLifetimeSystem` in that frame (the two are unordered; hash order), it sees those voxels at Health ≤ 0. It records `RemoveComponent<Parent>`, `AddComponent(ComponentTypeSet)`, and `AddComponent(ship, ColliderRebuildRequired)` into a **later-created** buffer of the same ECB system.
- **Playback [V]:**
  - The destroy runs first and follows the LEG.
  - The later `AddComponent` on a destroyed voxel or ship hits `AssertCanAddComponents` → `InvalidOperationException("The entity does not exist")` (`Unity.Entities/EntityComponentStoreDebug.cs:514-518`).
  - That aborts the rest of the playback.
  - The assertion is `[Conditional(ENABLE_UNITY_COLLECTIONS_CHECKS)]`, so in release builds there is no check, and the operation runs on a stale entity slot.
- The same hazard exists for any detach recorded in the same frame as a ship destroy.
- **Fix:**
  - Give all structural combat changes one explicit order.
  - Have the ship-death path consume its own pending detaches (mark the ship `Dying`, skip it in destruction).
  - Never destroy through a LEG that contains gameplay-owned children.

### C06. Collider blob leak (= 01-B3). High at scale [V]

- `ShipColliderRebuildSystem.cs:178-181` overwrites `physicsCollider.Value` with a new `CompoundCollider.Create` and never disposes the old one.
- Ship destruction never disposes the compound created in `ShipConstructionJobSystem.cs:392-394`.
- Grep for `.Dispose()` on colliders finds only the temporary child blobs and the cached debris and projectile colliders.
- **[I]** At massive scale, each rebuild leaks one full compound (hundreds of KB to MB) at up to 10 Hz per ship under fire.

### C07. Collider rebuild starvation → invisible blockers (= 01-B7). Med-High [V mechanism, I visibility]

- Each detach records `AddComponent(ship, ColliderRebuildRequired{DelayRemaining=0.1})` (`VoxelDestructionSystem.cs:242`).
- `AddComponent` on an existing component **sets** the value (`EntityDataAccess.cs:846-854`), so every destroyed voxel resets the timer.
- `ShipColliderRebuildSystem.cs:46-56` only rebuilds after 0.1 s with no new destructions.
- While a ship loses a voxel at least every 100 ms, which is normal under focused fire, the ghost spheres of every destroyed voxel stay in the compound:
  - Projectiles "hit" them. `ResolveVoxel` returns Null, the projectile is consumed and plays FX, and does no damage. You can't shoot through the hole you just made.
  - Ships collide with geometry that is no longer there.
  - Freshly spawned debris starts overlapping its own ghost (C11).
- **Fix:** set the delay only when the component is absent, or use a max-latency deadline. Better: rebuild dirty bricks every tick in a job (§5.9).

### C08. Parallel lost-update races. Medium [V]

The following jobs do read-modify-write on shared entities through `[NativeDisableParallelForRestriction]` lookups:

- `EntityLifetimeSystem.LifetimeJob` (`ScheduleParallel`, `:55`) writes `VoxelHealth` (`:218-220`) and `AsteroidHealthComponent` (`:194-203`).
- `BeamSystem.BeamJob` (`:109`) writes `VoxelHealth`, `ShipEnergyComponent`, `MirrorComponent` and `AsteroidHealthComponent` (`:170-172, 300-303, 310-312, 330-333, 352-354`).
- `WeaponFireSystem.FireJob` (`:106`) writes `ShipEnergyComponent` (`:171-188`).

Two projectiles or beams on the same voxel, or two weapons on the same ship in different chunks, lose updates. The `wasAlive` death checks can also fire zero times or twice.

**Fix:** a single-writer damage resolver (§5.3). For energy, a per-ship power allocation pass.

### C09. Ramming is physically and numerically wrong. Med-High [V / I]

1. **[V]** The arcade controller overwrites `PhysicsVelocity` every frame (`ShipMovementSystem.cs:119, 181`, = 01-B13). `ExternalImpulse` ("for ramming/knockback", `ShipMovementComponent.cs:20`) is never written by anything (grep). Ships do not bounce or transfer momentum; they keep pushing into each other.
2. **[V]** Impact speed uses **post-solve** linear velocities (`ShipCollisionSystem.cs:139-148`). It reads after `ExportPhysicsWorld`.
   - **[I]** With restitution 0, the solver has already removed the approaching normal velocity, so head-on rams measure about 0 while sliding contacts measure full speed.
   - Angular velocity at the contact point is ignored.
3. **[V]** Unity Physics raises collision events for every contacting leaf pair, every step, so pressed-together ships take damage continuously. There is no per-pair cooldown or energy budget (`:207-239`).
4. **[V]** Damage is symmetric and ignores mass, hardness and HP.
5. **[V]** Every event creates a `PlaySoundEvent` entity (`:241-249`). Combined with C02 this means hundreds of sound entities per frame during contact.
6. **[V]** `CalculateDetails` is called per event in the job (`:157`).

### C10. Ship ↔ asteroid collisions never damage (= 01-B12). Medium [V]

- `ResolveVoxel(asteroid, ...)` returns Null (no `ShipVoxelIndex` buffer, `VoxelPhysicsUtils.cs:23-24`), so the event is dropped (`ShipCollisionSystem.cs:135-137`).
- The asteroid branches in `ShipCollisionDamageSystem` (`:218-234, 264-278`) are unreachable.

### C11. Debris motion is disconnected from the ship. Medium [V / I]

- **[V]** Debris velocity is a random XZ direction at 2–8 u/s, plus random spin (`VoxelDestructionSystem.cs:309-328`). It ignores:
  - the ship's linear velocity (ships move up to `MaxSpeed = thrusterForce`, and boost at 200 u/s; `ShipMovementComponent.cs:39-45`)
  - the ship's angular velocity (ω × r)
  - the impact direction and impulse.

  **[I]** Pieces knocked off a fast ship appear to be yanked backwards and stop in space.
- **[V]** The debris sphere (Projectile layer, `:98-102`) is created at the exact position of the voxel's ghost child, which stays in the ship compound until rebuild (C07).
  - **[I]** Unity Physics depenetrates them at up to 3 m/s (`Solver.cs:410-413`), pushing debris out and the ship back.
  - **[I]** A connectivity cut that releases hundreds of voxels at once yields a visible lurch or spin of the parent (Y-rotation is not locked).
- **[V]** Debris lifetime is a fixed 8 s with no fade (`:336-339`). There is no pooling and no cap.

### C12. Severed sections dissolve instead of breaking off. Medium (vision) [V]

- `VoxelConnectivitySystem` sets `Health = 0` on every unreachable voxel (`:165-179`). `VoxelDestructionSystem` then turns each into an independent 1-voxel rigid body. A severed wing becomes confetti, not a drifting wreck.
- Ships without a Core block skip connectivity entirely (`:63-68`). Per 01-B17, five shipped ships have no core.
- The BFS includes voxels that are already dead but not yet detached (`Health <= 0`, `Parent` still present), so it can bridge across a just-destroyed voxel. The next check catches this only if another voxel dies.
- Only one ship is checked per frame, with a 0.2 s throttle per ship (`:23, 44-90`).
- Multi-cell blocks only register their root cell (01-B1). That causes false detachment.

### C13. No enemy ships can spawn. High (feature) [V]

- `CombatManager.SpawnShip` has a single caller, which uses `isPlayer: true` (`OpenWorldGalaxyManager.cs:399-405`).
- `EncounterManager.StartEncounter` handles only `NarrativeEncounter` (`EncounterManager.cs:177-192`).
- `DialogManager.ResolveStartCombat` and `NarrativeActionRuntime.ResolveStartCombat` load a `CombatEncounter` and call `StartEncounter`, which errors out (`DialogManager.cs:579-593`, `NarrativeActionRuntime.cs:340-351`).
- **The "seamless open world combat" refactor stopped after the scene loading was removed.** The guide (`OpenWorldCombatRefactorGuide.md`) is stale on this point: it describes `CombatManager` as 793 lines with `SpawnShips`, but the file is now 271 lines and `SpawnShips` is gone.

### C14. Shields can never exist or turn on (= 01-B4). Medium [V]

1. `ShipConstructionJobSystem.cs:87-92` uses `TryGetSingletonEntity<ShieldBubblePrefabTag>`.
   - The tag is baked onto the prefab entity itself, together with `Prefab` (`ShieldBubblePrefabAuthoring.cs:26-27`), so default queries exclude it.
   - `ShieldBubblePrefabReference` is never added (grep: declared at `:13`, read at `ShipConstructionJobSystem.cs:90`).
   - So `ShieldPrefab == Entity.Null`, and no `ShieldEmitterComponent` is ever created (`:444`).
2. Even if it were, `ShipEnergyComponent` is spawned disabled (`:428`) and only `ShieldToggleAbilitySO.cs:138` enables it (C15).

`ShieldSystem`, `ShieldBubbleVisibilitySystem`, `ShieldFlashDecaySystem` and the shield branch of `BeamSystem` are all dead at runtime.

### C15. Abilities are not wired. Medium [V]

- `AddAbility`, `EquipAbility` and `TryUseAbility` have no callers outside `AbilityController.cs` (grep across `Assets/Game`).
- `ShipData` computes `AbilityIds` (`ShipData.cs:200-208, 238`) but nothing turns them into instances.
- `OverclockAbilitySO` is a line-for-line copy of `BoostAbilitySO`: it sets `IsBoosting` (`OverclockAbilitySO.cs:101-133`). It has no asset (GUID grep).
- `ShieldToggleAbilitySO`:
  - resolves the ship by linear scan of `shipId` instead of `CombatShip.ShipEntity` (`:53-76`)
  - logs on every call (`:32, 55, 61, 66, 73, 107, 115, 135`)
  - creates queries per use (`:58, 156`).

### C16. Flak never fires; missile and flak do zero base damage. Medium [V]

- **Flak:** `projectile_flak_shell.asset` has `VisualType: 6` (FlakShell). The projectile database lists types 0–5 only. Construction leaves `ProjectilePrefab = Entity.Null` (`ShipConstructionJobSystem.cs:482-494`), and `WeaponFireSystem.cs:153-167` silently resets the cooldown. `weapon_flak_cannon_medium` is non-functional.
- **Damage:** `projectile_missile.asset` and `projectile_flak_shell.asset` have `Damage: 0`. `ProjectileDamage = projData.Damage` (`BlockBlobBuilder.cs:233`), and `ApplyTo(0) = (0 + additive) × (1 + mult)`. Without flat weapon-damage modules these deal 0. `ExplodeOnDeath` damage is also 0, so the `Damage > 0` gate at `EntityLifetimeSystem.cs:158` skips the explosion.

### C17. Homing locks onto allies. Medium *(latent)* [V]

`ProjectileHomingSystem.cs:77-79` only skips destroyed ships and the shooter's `shipId`, despite the comment "same allegiance". It also accepts a voxel entity as its initial target (`WeaponFireSystem.cs:315`, from `WeaponTurretSystem.cs:105-109`). That voxel keeps its `LocalToWorld` after being detached, so missiles chase debris.

### C18. Fire rate depends on frame rate. Medium [V]

- The cooldown counts down by `dt` and is **reset** to `1/FireRate` when a burst completes (`WeaponFireSystem.cs:126-134, 353`). The overshoot is discarded.
- At most one shot per frame is possible (`:139-151`).
- **[I]** Example: a 20 shots/s weapon fires about 15/s at 30 FPS, and about 20/s at 60 FPS minus the overshoot.
- **Fix:** `cooldown += 1/rate`, loop while `cooldown <= 0` with a sub-step spawn offset, and run in the fixed step.

### C19. Spawn latency and stale transforms. Low-Med [V / I]

- Projectiles are recorded into the BeginSim ECB, so they appear next frame (`:89-90`).
- Their position comes from `LocalToWorld`, which depends on where `TransformSystemGroup` landed in the hash order. It is also the *graphically smoothed* transform (`PhysicsGraphicalSmoothing`, `ShipConstructionJobSystem.cs:404`).
- Velocity is `forward × speed` and does not include the shooter's velocity (`:282-286`).
- **[I]** A ship strafing at 20 u/s sees its bullets lag and drift by about 0.3–0.7 u per frame of latency.

### C20. Vertical spread in a planar game. Low-Med [V / I]

- Pitch spread is `(1 − accuracy) × 10°` (`WeaponFireSystem.cs:241-242`). Shipped accuracies go down to 0.5, which gives ±5°.
- **[I]** At 100 u that is ±8.7 u vertically, against ships only a few cells tall. Shots leave the play plane, and nothing removes their Y component.

### C21. Splash has no source or allegiance filter; asteroid splash is multiplied. Medium [V / I]

- `ApplyAreaDamage` damages every voxel in range, including the shooter's own and its allies' (`EntityLifetimeSystem.cs:223-255`). With C19 and C20, a projectile can detonate against an asteroid right next to its own ship.
- For asteroids, `OverlapSphere` returns one `DistanceHit` per box leaf in range. `ApplyAsteroidDamage` is called per hit (`:231-235`). **[I]** Asteroid damage is multiplied by the number of leaves overlapped.

### C22. Beam defects. Medium [V / I]

1. **[V]** Energy is charged per shot by `WeaponFireSystem.cs:171-189` *and* per second by `BeamSystem.cs:166-173`. The beam never stops when energy runs out; it can drive energy negative until the next `EnergySystem` clamp.
2. **[V]** Lifetime `1/FireRate + 0.05` (`WeaponFireSystem.cs:220-223`) overlaps the next beam, giving 0.05 s of double DPS each cycle.
3. **Orphaned beams:**
   - **[V]** Beams are parented to the weapon but are not in the ship LEG.
   - **[I]** When the weapon entity is destroyed (ship death), `ParentSystem` un-parents the beam. Its parent-local `LocalTransform` (position = `MuzzlePoint`, identity rotation) becomes world space.
   - **[I]** For up to `1/FireRate` s (1.7 s for the small beam turret) it keeps raycasting 150 u along +Z from near the world origin, owned by nobody. That is near the player under the floating origin.
   - Today the only beam owner is the player; this matters when enemies exist.
4. **[V]** Hit-FX sampling uses `Random.CreateFromIndex(sortKey + (uint)(dt×1000))` (`BeamSystem.cs:342-343, 357-358`). For a stable frame time the seed is constant per beam slot, so a given beam either always or never spawns FX.
5. **[V]** The all-hits `CastRay` builds a list and loops it (`:236-273`). A closest-hit query with a custom filter collector is cheaper.

### C23. The beam prefab is a "projectile". Low *(dormant)* [V]

- `base_projectileLaserBeam.prefab` uses `ProjectileAuthoring`, which bakes `ProjectileComponent`, `PhysicsVelocity`, `Simulate`, `PhysicsWorldIndex` and `Lifetime` (`ProjectileAuthoring.cs:15-30`).
- Therefore:
  - `ShieldSystem` (`:80-81`) and `PortalTeleportSystem` (`PortalSystem.cs:92-93`) iterate beams and treat their **parent-local** `LocalTransform.Position` as world.
  - Unity Physics includes beams in the dynamic body query, which has no `Parent` exclusion (`PhysicsWorldData.cs:143-147`), as collider-less kinematic bodies.

### C24. Double effect of shield and hull hit. Low *(dormant)* [I]

- `ShieldSystem` destroys absorbed projectiles through the **EndSim** ECB (`:39-40, 135`).
- In the same frame, `ProjectileCollisionSystem` and `EntityLifetimeSystem` may already have registered a hull hit from the previous fixed step.
- **[I]** That records damage and FX through the BeginSim ECB. Both apply.

### C25. AI logic errors. Medium *(latent)* [V]

- **Health disengage is unreachable** for every spawned AI:
  - All AI ships are created `Balanced` (`ShipConstructionJobSystem.cs:261`), whose threshold is 0.3 (`AIShipNavigationComponent.cs:69-72`).
  - Ships die at 0.4 (`ShipHealthSystem.cs:30`).
  - Only `Timid` (0.5) could ever flee.
- **Fleeing never ends:**
  - It resets only when `health > threshold + 0.1` (`AIShipNavigationSystem.cs:124-131`), but `ShipHealth` only ever decreases (grep: written only in destruction and construction).
  - The flee destination is re-projected 200 u ahead of the ship every frame (`:118-121`). A fleeing AI leaves the system forever.
- **Roll maths:**
  - `QuaternionToEuler` (`:270-289`) is a ZYX decomposition. For any yaw beyond ±90° it returns x = z = ±180°. **[V by algebra:** for `q = (0, sin θ/2, 0, cos θ/2)`, `atan2(0, cos θ)` is π when cos θ < 0.**]**
  - So `RollInput` saturates at ±1 for half of all headings (`:187-191`).
  - It is inert only because `ShipMovementSystem` ignores Pitch and Roll (`:113`). Those calculations are dead work.
- **Timid** `PreferredRange = 200` (`:61`) is greater than the hard-coded weapon `MaxRange = 150` (`ShipConstructionJobSystem.cs:507`), so it orbits out of range and never fires (`WeaponTurretSystem.cs:113`).
- **Target acquisition has no range limit** (`AIShipFindTargetSystem.cs:157-176`). In an open world every hostile ship in the loaded bubble converges on the closest enemy.
- **All ships re-target on the same tick** (`NextTargetingCheck = now + 0.5`, `:86`), which creates a periodic O(N²) spike.
- The target voxel is a random `Child`. That may be a thruster visual, shield bubble or weapon-arc entity, since all are children (`:166-175`).

### C26. Player death is unhandled. Medium *(latent)* [V]

- `CombatManager.OnShipDestroyed` has no subscribers (grep), and `EncounterManager.HandlePlayerDefeat` (`EncounterManager.cs:483`) has no callers.
- When the player ship dies, the entity disappears and the wrapper GameObject is destroyed after 5 s (`CombatShip.cs:65-79`). No defeat flow follows.
- `CaptureShipDamage` and `DespawnShips` are unused, so damage is never persisted.

### C27. Managed call in a Burst-compiled `OnUpdate`. Low-Med [V code, I compiler behaviour]

- `VoxelDestructionSystem.OnUpdate` is `[BurstCompile]` (`:86`) and reads `UnityEngine.Time.frameCount` (`:133`).
- **[I]** Burst cannot call that managed property; the usual error is BC1016. In the editor the method falls back to Mono and logs a Burst error. AOT player builds may report a Burst compile error.
- **Fix:** use `SystemAPI.Time.ElapsedTime` or a tick counter for the seed. Check the Burst Inspector.

### C28. Main-thread `ComponentLookup` write without completing writers (= 01-B15). Medium [V / I]

- `VoxelConnectivitySystem` writes `_voxelHealthLookup[...]` on the main thread (`:172-177`). It only completes dependencies for the types in its `SystemAPI.Query`.
- `EntityLifetimeSystem` and `BeamSystem` jobs write `VoxelHealth` and are unordered relative to it.
- **[I]** Depending on hash order, this throws a safety exception in the editor, or races in builds.

### C29. Hit VFX and audio. Medium (perf) [V]

- `ParticleSpawner` "pools" `CFXR Explosion 1`, but that prefab has `stopAction: 2` (Destroy) (`Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR Explosion 1.prefab:49`), and nobody calls `SimplePool.Despawn` for it (grep).
  - After the 50 preloaded instances (`ParticleSpawner.cs:18`), every hit runs `GameObject.Instantiate`, `AddComponent<PoolMember>` (`SimplePool.cs:146-155`) and later `Destroy`.
- The ship explosion prefab has `stopAction: 0` and is never despawned, so each ship death beyond 5 leaves a permanent active GameObject.
- `ProjectileHitVisualSystem.cs:21` dereferences `ParticleSpawner.Instance` without a null check. Outside `GalaxyMapScene` (the only scene with a spawner, `GalaxyMapScene.unity:970-975`) this throws every frame. The temp ECB is never played back, so events accumulate.
- `AudioSystem` returns early when `AudioManager.Instance == null` without destroying events (`Assets/Game/Core/Audio/AudioSystem.cs:14`), so they accumulate the same way.
- `PlayRandomHitSound` runs once per hit, and there is one `PlaySoundEvent` entity per shot (`WeaponFireSystem.cs:324-340`).

### C30. Order-dependent behaviour. Medium [V attributes, I effects]

- **`WeaponFireSystem` is unordered relative to `WeaponInputSystem`/`WeaponTurretSystem`.** If the hash order is Input → Fire → Turret, manual fire ignores turret alignment: input enables the tag, Fire fires, then Turret disables it.
- **`EntityLifetimeSystem` vs `ProjectileCollisionSystem`.**
  - If Lifetime runs first, a hit is resolved one frame later.
  - **[I]** A collider rebuild in the next frame's fixed step compacts `ShipVoxelIndex`, so the stored `HitColliderKey` now indexes a **different** voxel.
- **Lifetime vs Destruction:** this produces C05.
- **Readers of `LocalToWorld`** (Fire, Beam, Shield, AI, Turret) vs `TransformSystemGroup`: either fresh or one frame stale.

### C31. Physics step configuration. Low [V]

- `PhysicsSetup` builds `PhysicsStep` from an object initializer instead of `PhysicsStep.Default` (`PhysicsSetup.cs:15-21` vs `Unity.Physics/ECS/Base/Components/PhysicsComponents.cs:395-410`). `CollisionTolerance` and the solver heuristic and direct-solver settings are zero.
- The authoring in `GalaxyMapScene.unity:165` is not in a subscene and is never baked. Two sources of truth, one of which is dead.

### C32. Sphere colliders per voxel. Medium [V geometry, I gameplay]

- Each cell is a sphere of radius `0.5 × cell` (`ShipConstructionJobSystem.cs:370-378`; rebuild `ShipColliderRebuildSystem.cs:148-155`).
- **[I]** Neighbours touch only at face centres. A ray parallel to an axis through a cell edge or corner line (0.707 cell from centres) misses every sphere. Beams can pass between layers or columns of multi-layer hulls. Contacts between ships are "cobblestone" bumpy.
- Multi-cell blocks get a single root sphere (01-B1). Most of a 3×1×3 flak cannon or 3×3×2 thruster has no collision.

### C33. Enable-bit overloading. Low [V]

`ShipEnergyComponent : IEnableableComponent` uses enabled to mean "shields on" (`ShipEnergyComponent.cs:4-7`). Any future `WithAll<ShipEnergyComponent>` query silently skips ships with shields off. `EnergySystem` already needed `IgnoreComponentEnabledState` to work around this (`EnergySystem.cs:38`).

### C34. Minor [V]

- `CombatHudSystem` allocates `new bool[5]` and calls `GetComponent<AbilityController>()` plus `InitializeAbilityBar` every frame (`:44-52`).
- Comments disagree with the constants (`WeaponTurretSystem.cs:69-70`: "5 degrees" and "10 degree" vs 15).
- `ShipData.cs:22-26` says "under 30%"; the code uses 40%.
- `WeaponInputSystem` falls back to legacy `Input.GetKey(KeyCode.Space)` (`:86`).
- `ShieldToggleAbilitySO` logs per use (C15).
- `PlayerWeaponArcSystem` creates a new `RenderMeshArray` per arc (`:78-81`) and has a duplicated comment block (`:31-36`).
- `DetachVoxel` destroys children inside the "has energy component" block (`VoxelDestructionSystem.cs:271-278`). That is only correct because every ship gets a `ShipEnergyComponent` (`ShipConstructionJobSystem.cs:421`).
- The class `PortalInteractable` lives in `PortalTrigger.cs`. The class/file name mismatch breaks component serialization (§3).
- Hard-coded `MaxRange = 150` for every weapon (`ShipConstructionJobSystem.cs:507`). `ShipMaster.MaxRange = 300` is never read.

---

## 3. Dead, deprecated and duplicate code

Proof method: type or member grep across `Assets/Game` for `*.cs`, and script-GUID grep across `*.unity`, `*.prefab` and `*.asset`.

| Item | Location | Proof [V] | Action |
|---|---|---|---|
| `ScrapMagnetismSystem` | `Combat/ScrapMagnetismSystem.cs` | `OnUpdate` body fully commented (`:33-79`). Runs every frame (no `RequireForUpdate`). `[BurstCompile]` on a SystemBase is meaningless. | Delete |
| `ShieldComponent` `[Obsolete]` | `Combat/ShieldComponent.cs` | No references | Delete |
| `ShieldHitEvent` | `Combat/ShieldHitEvent.cs` | No references | Delete |
| `ProjectileHit` | `Combat/WeaponComponent.cs:4-7` | No references | Delete |
| `AIWeaponTag`, `MirrorTag` | `WeaponComponent.cs:50-52`, `MirrorComponent.cs:25` | Added at construction (`ShipConstructionJobSystem.cs:475, 537`), never queried | Delete |
| `MirrorComponent.AccumulatedDamage/BeamCount/BeamDamageMultiplier/BeamThickness` | `MirrorComponent.cs:10-19` | Written by `BeamSystem.cs:115-118, 300-303`, never consumed. Multiplier and thickness are never read at runtime. | Implement or delete |
| `BeamComponent.DamageAccumulator` | `BeamComponent.cs:9` | Only set to 0 (`WeaponFireSystem.cs:216`) | Delete |
| `JustTeleportedTag`, `TeleportCooldownJob` | `PortalComponent.cs:37-40`, `PortalSystem.cs:78-82, 122, 129-144` | Scheduling and add are commented out | Delete or re-enable |
| `CombatResult` | `Combat/CombatResult.cs` | No references (old battle-scene outcome) | Delete |
| `ExtractionRiftTrigger` | `Combat/ExtractionRiftTrigger.cs` | Stub "not yet implemented" (`:9-10`). Prefab is referenced only by `encounter_extraction.asset`, a combat encounter that cannot start (C13). | Delete with the encounter or redesign |
| `PortalInteractable` in `PortalTrigger.cs` | `Combat/PortalTrigger.cs:3` | Class/file mismatch, so a MonoBehaviour cannot bind. Only used by `CombatExitPortal.prefab`, which is referenced nowhere. | Delete both |
| `OverclockAbilitySO` | `Abilities/OverclockAbilitySO.cs` | Copy of `BoostAbilitySO`; no asset | Delete |
| `BoostCameraController` | `Abilities/BoostCameraController.cs` | "TODO:REMOVE TEMPORARY" (`:4`); no scene or prefab reference | Delete |
| `AsteroidMiner` | `Core/AsteroidMiner.cs` | No scene or prefab reference. GameObject `Physics.Raycast` mining from the pre-ECS era. | Delete |
| `CameraShake`, `CameraShakeController` | `Core/CameraShake*.cs` | No scene or prefab reference, so `ShipHealthSystem.cs:41-42` is dead | Replace (Cinemachine impulse) or add to scene |
| `EnsurePhysicsSimulateSystem` | `Combat/EnsurePhysicsSimulateSystem.cs` | `Simulate` "is added by default to all entities" (`Unity.Entities/IComponentData.cs:408`), so the `WithNone<Simulate>` query never matches. Explicit `AddComponent<Simulate>` calls are also redundant (`WeaponFireSystem.cs:192`, `ProjectileAuthoring.cs:16`, scrap set `VoxelDestructionSystem.cs:58`). | Delete |
| NetCode remnants | `WeaponFireSystem.cs:14-15, 244` | `WorldSystemFilter(Server/Client/Local)` and "NetworkTime" comment. No NetCode package in use. | Remove |
| `IsSinking`/`SinkTimer`/`MarkShipForSinking` | `ShipMovementComponent.cs:29-30`, `ShipMovementSystem.cs:46-51`, `CombatManager.cs:114-140` | Ship is destroyed the next frame (C04) | Replace with a real death sequence |
| `CombatManager` leftovers | `CombatManager.cs:3, 18-21, 173-185, 260-270` | `using QFSW.QC` unused. `defaultSpawnSpacing`/`maxShipsPerRow` unused. `aiShipPrefab` is only reachable with `isPlayer:false` (never). `CaptureShipDamage`/`DespawnShips` have no callers. | Fold into the new spawner |
| `ShipMaster.MaxRange`, `ShipMaster.targetShipPos` | `ShipData.cs:10-11` | Never read | Delete |
| `AIShipNavigationComponent.NextTargetCheckTime` | `:33, 50` | Never read | Delete |
| AI pitch/roll computation | `AIShipNavigationSystem.cs:183-191, 270-289` | Ignored by movement (`ShipMovementSystem.cs:113`) and mathematically wrong (C25) | Delete |
| Direct-hit branch of `LifetimeJob` | `EntityLifetimeSystem.cs:108-137` | Unreachable for shipped data (all `ExplosionRadius > 0`) | Superseded by §5.4 |
| `ScrapTag` branches in `LifetimeJob` | `EntityLifetimeSystem.cs:130-133, 248-251` | Filters prevent projectiles and overlaps from ever touching debris (§1.5) | Delete |
| Asteroid debris spawning | `EntityLifetimeSystem.cs:261-300` | Creates entities with no render, no collider and no `PhysicsWorldIndex`: invisible and motionless for 3–15 s | Replace with VFX |
| Shield pipeline | `ShieldSystem.cs`, `ShieldVisualSystem.cs`, `ShieldToggleAbilitySO.cs`, shield part of `BeamSystem` | Unreachable (C14) | Rewrite (§5.10) |
| Ability pipeline | `Abilities/*`, `CombatHudSystem.cs:43-70` | Never populated (C15) | Rewrite (§5.12) |
| AI pipeline | `AI/*` | No AI ship ever spawned (C13) | Rewrite (§5.11) |
| Unreferenced projectile prefabs | `Prefabs/Projectiles/base_projectilePrefab.prefab`, `base_projectileLighting.prefab` | Not in `ProjectileDatabaseAuthoring.prefab` and no other reference (GUID grep) | Delete |
| `ParticleSpawner.explosionParticle`, `SpawnSmoke` | `ParticleSpawner.cs:8, 19, 34-37` | Preload commented out; `SpawnSmoke` has no callers | Delete |
| `PhysicsStepAuthoring` in `GalaxyMapScene` | `GalaxyMapScene.unity:165` | Not in a subscene, so never baked | Move into subscene, delete `PhysicsSetup` |
| Build settings entry `CombatScene.unity` | `ProjectSettings/EditorBuildSettings.asset` | File deleted in commit `442a4e7b` | Remove entry |
| `ShipVoxel.Size`, `ShipVoxel.IsExposed` | `ShipVoxel.cs:9-10` | Never written or read | Replace with real cell data (01-B1) |
| `ProjectileComponent.ProjectileSize` | `ProjectileComponent.cs:10` | Written, never read | Delete |

**Duplicate logic [V]:**
- The damage-type multiplier switch is duplicated (`EntityLifetimeSystem.cs:186-192, 210-216`). Beam and ram damage ignore type entirely.
- The asteroid "wasAlive → Lifetime 0" pattern appears three times (`EntityLifetimeSystem.cs:182-204`, `BeamSystem.cs:328-339`, `ShipCollisionSystem.cs:264-278`).
- The shield ray/sphere test is implemented separately in `ShieldSystem` (point-in-sphere) and `BeamSystem` (ray-sphere).
- Emitter arrays are gathered twice per frame (`ShieldSystem.cs:43-51`, `BeamSystem.cs:71-79`).
- The all-ships array is gathered twice per frame (`AIShipFindTargetSystem.cs:33-35`, `ProjectileHomingSystem.cs:29-35`).

**`OpenWorldCombatRefactorGuide.md`.** Treat it as historical. Its "files that need NO changes" table includes systems with the defects above. Its `CombatManager` line references (793 lines) no longer match the code.

---

## 4. Performance

The cost model assumes the vision of thousands to millions of cells per ship and dozens of ships. Today's per-voxel-entity model (owned by 01) is the main limiter. The combat-specific costs are below.

### 4.1 Per-frame scans that scale with total voxels [V]

| Where | Cost | Note |
|---|---|---|
| `VoxelDestructionSystem` | O(all attached voxels) per frame, **single-threaded** `.Schedule` (`:137-155`) | Runs even when nothing died. Needs an event list or change filter. |
| `VoxelConnectivitySystem.RunConnectivityCheck` | O(all voxels in the world) per check, main thread (`:104-115`), plus BFS with Temp hash maps | Should iterate the ship's own buffer or grid, in a job, per ship in parallel |
| `ShipColliderRebuildSystem` | O(N log N) compound build **plus N `SphereCollider.Create` blob allocations** (`:148-155`), main thread, `EntityManager.SetComponentData` per voxel (`:167-172`), `CompleteDependency()` every fixed step even with nothing to do (`:35`) | Use one shared child blob (Create copies), a job, and per-brick dirty rebuilds |
| `ShipConstructionJob` | N sphere blob allocations per ship (`:370-378`) | Same fix |

### 4.2 Structural-change storms on big destruction [V counts, I magnitude]

Per destroyed voxel, `DetachVoxel` issues:
- `RemoveComponent<Parent>`
- `AddComponent(ComponentTypeSet)`, a new archetype that includes the `PhysicsWorldIndex` shared component
- `AddComponent(PhysicsCollider)`
- `AddComponent(PhysicsMass)`
- `AddComponent<ScrapTag>`

That is about **5 archetype moves per voxel**, plus value sets, plus `AddComponent(ColliderRebuildRequired)` on the ship (`:242, 291-342`).

Then:
- `ParentSystem` removes each child from the ship's `Child` buffer. **[I]** That is a linear search per removal, so O(k·N) for k detached voxels.
- Every debris piece is a dynamic body in a **non-incremental** broadphase that is rebuilt every step. `IncrementalDynamicBroadphase` is false (`PhysicsSetup` default).

**[I] Example.** One connectivity cut that frees 2,000 voxels on a 10k-voxel ship causes, in one ECB playback:
- about 10k chunk moves
- about 10⁷ `Child` element shifts
- 2,000 new rigid bodies
- a full 8k-child compound rebuild with 8k blob allocations

That is a multi-frame hitch.

### 4.3 Physics-side costs [V / I]

- Projectiles are physics bodies with triggers. Each one is in the broadphase every step, and each shot causes an ECB `Instantiate` plus an archetype change for the collider add (`WeaponFireSystem.cs:191, 280`).
  - **[I]** 5k bullets become 5k extra bodies and trigger pairs per step. Raycasting projectiles in a Burst job removes this entirely (§5.4).
- Every voxel child pair raises collision events: CollideRaiseCollisionEvents on all children (`ShipConstructionJobSystem.cs:376`). Grinding contacts between large ships create hundreds of events per step, each with `CalculateDetails` (`ShipCollisionSystem.cs:157`) and a sound entity (`:241-249`).
- Ships carry `PhysicsGraphicalSmoothing` (`ShipConstructionJobSystem.cs:404`). That is fine, but combat logic samples the smoothed `LocalToWorld` instead of the physics transform (C19).

### 4.4 Quadratic and redundant work [V]

| System | Complexity | Extra |
|---|---|---|
| `AIShipFindTargetSystem` | O(N²) every 0.5 s, **all ships on the same tick** | Allocates 3 TempJob arrays every frame |
| `ShieldSystem` | O(projectiles × emitters) per frame | 4 TempJob arrays per frame whenever any projectile exists, even with zero emitters |
| `BeamSystem` | O(beams × emitters) + all-hits raycast | **No `RequireForUpdate<BeamComponent>`**: 4 arrays + 2 jobs every frame with no beams (`:27-110`) |
| `ProjectileHomingSystem` | O(missiles × ships) | **No `RequireForUpdate<HomingComponent>`**, and a `Complete()` sync point **every frame** (`:47`) |
| `WeaponTurretSystem` | O(weapons × targets); writes `ref LocalTransform` of every weapon each frame | Bumps change versions on all weapon chunks, including non-rotating weapons |
| `FindPotentialTargetsJob` | `ref ShipMaster` for every ship each frame | Change-version churn |

### 4.5 Main-thread managed work [V]

- `ShipCollisionSystem_Managed`: SystemBase, `Complete()` and a `NativeQueue` TempJob allocation every fixed step.
- `ShipHealthSystem`: calls into MonoBehaviours inside a query.
- `ProjectileHitVisualSystem`: `GameObject.Instantiate` per hit after pool exhaustion (C29).
- `CombatHudSystem`: GC allocation per frame.
- `WeaponInputSystem`: `EntityManager.SetComponentEnabled` per player weapon per frame, a sync on `FireWeaponTag`.
- `PlayerWeaponArcSystem`: main-thread entity creation (one-off).

### 4.6 Burst coverage [V]

- **Burst jobs:** fire, turret, AI, lifetime, beam, shield, energy, homing, destruction.
- **Main-thread or managed:** connectivity (Burst main thread), collider rebuild (no Burst), collision buffer copy, health, HUD, VFX, audio.
- `VoxelDestructionSystem.OnUpdate` probably falls back to Mono (C27).

---

## 5. Recommended target architecture

Treat the current code as a prototype.

### 5.0 Goals and principles

1. **Everything that changes the simulation runs in `FixedStepSimulationSystemGroup`,** at a fixed tick, in explicit sub-groups. Presentation interpolates.
2. **Events, not flags.** Every stage writes a stream of plain structs. The next stage consumes and clears it in the same tick.
3. **Single writer per datum.** All damage goes through one resolver that owns cell HP. No `[NativeDisableParallelForRestriction]` writes to shared entities.
4. **Grid space is truth.**
   - Hits, explosions and rams are converted to ship-local cell coordinates.
   - Colliders exist for broadphase and contact, never for identifying cells.
   - This follows 01 §6: cells are data in 16³ bricks, functional blocks are entities.
5. **Structural changes are batched per tick** into one ECB system at the end of the combat group. Prefer enableable flags and pooled entities over add/remove.
6. **Visual mass, physical sparsity.**
   - Thousands of flying cubes are GPU particles.
   - Dozens of mid-size chunks are pooled rigid bodies.
   - A few large islands become full **hulk ships**.
7. **Ships are physical.** Drive with forces and impulses under a speed cap, so collisions and explosions produce real knockback.

### 5.1 Group layout and ordering

```csharp
// All inside FixedStepSimulationSystemGroup (60 Hz).
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))] [UpdateBefore(typeof(PhysicsSystemGroup))]
public partial class CombatPrePhysicsGroup : ComponentSystemGroup {}
//   ShipCommandSystem        (player input snapshot / AI decisions → ShipCommand)
//   ShipPropulsionSystem     (ShipCommand → forces/impulses on PhysicsVelocity; speed cap; NO overwrite)
//   PowerAllocationSystem    (per-ship energy budget → Powered enable bits)
//   WeaponAimSystem          (turret rotation, lead solve)
//   WeaponFireSystem         (cooldown accumulator → ProjectileSpawn stream / beam on/off)
//   ColliderSwapSystem       (install brick colliders rebuilt last tick; dispose old)

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))] [UpdateAfter(typeof(PhysicsSystemGroup))]
public partial class CombatPostPhysicsGroup : ComponentSystemGroup {}
//   ImpactEventSystem        (ICollisionEventsJob → RamImpact stream; consumes this step's events)
//   ProjectileStepSystem     (cast prev→next vs shields + CollisionWorld → DamageEvent + FxEvent)
//   BeamSystem               (closest-hit cast → DamageEvent per tick)
//   ExplosionSystem          (DamageEvent with Radius>0 → grid rasterization per ship)
//   DamageResolveSystem      (bucket by ship → apply to cells/blocks → CellRemoved stream)
//   IntegritySystem          (incremental islands per ship → IslandEvent)
//   SplitSystem              (islands → hulk ships / debris spawns; removes cells)
//   BrickDirtySystem         (schedule remesh + collider rebuild jobs for dirty bricks)
//   ShipDeathSystem          (core loss / threshold → death sequence, not deletion)
//   CombatEndECBSystem       (custom EntityCommandBufferSystem: the ONLY combat structural sync point)

// PresentationSystemGroup: FxUploadSystem (VFX Graph), AudioEventSystem, HudSyncSystem
```

### 5.2 Damage model

**Two levels, cells and blocks:**
- **Cells** carry structural HP (quantised byte or `half`) and a material id (armour class). They are the unit of removal.
- **Block instances** carry function and function HP: weapons, thrusters, reactors, shields, cores, multi-cell structures. A block is disabled when its function HP hits 0 or when a configured fraction of its cells is gone.
  - Functional blocks are entities (01 §6). Plain hull is not.
- **Ship death is a state, not a deletion.** On core loss the ship becomes a hulk: AI and power off, the ship stays physical and damageable, and secondary explosions come from reactors. Despawn only by distance or time budget, through the same "convert to debris" path, so nothing pops.

**Damage types** become a small blob table: `Multiplier[DamageType, Material]` plus a penetration factor. One code path applies it.

### 5.3 Damage event bus and resolver

```csharp
public struct DamageEvent            // produced by projectiles, beams, explosions, rams, abilities
{
    public Entity Ship;              // root of the struck grid (ship, station, asteroid, hulk)
    public float3 LocalPoint;        // ship-grid space (cells), precise hit point
    public float3 LocalDir;          // incoming direction in grid space (for penetration / debris kick)
    public float  Amount;
    public float  Radius;            // 0 = point/penetrating, >0 = blast (rasterized)
    public float  Penetration;       // cells of armour-equivalent it may pass through
    public DamageType Type;
    public Entity SourceShip;        // attribution, friendly-fire policy, AI threat
    public float3 WorldImpulse;      // knockback applied to the ship body (and debris)
}

public struct CellRemoved { public Entity Ship; public int3 Cell; public ushort Material; public float3 WorldPos; public float3 WorldVel; public float3 Kick; }

// Singleton holding per-tick streams (allocated once, cleared each tick)
public struct CombatStreams : IComponentData
{
    public NativeStream Damage;          // many writers (projectile/beam/ram jobs)
    public NativeList<CellRemoved> Removed;
    public NativeList<FxEvent> Fx;       // hit sparks, explosions, shield ripples
    public NativeList<SfxEvent> Sfx;
}
```

**`DamageResolveSystem`:**
1. Read the stream and bucket events by `Ship` with `NativeParallelMultiHashMap<Entity, DamageEvent>`. Sort per ship by (`SourceShip`, order) for reproducibility.
2. Run `IJobParallelForDefer` over **ships**. Each worker owns one ship's grid, so there are no races.
   - **Point damage:** start at the hit cell, then march along `LocalDir` with 3D DDA for `Penetration` cells. Subtract HP with the material table. Overflow damage continues to the next cell (armour piercing for free).
   - **Blast damage:** rasterize the sphere in grid space over the cell AABB of radius r. Use falloff `(1 − d/r)²`.
     - Optional occlusion: attenuate by the HP of cells on the DDA line from the centre. This gives shaped craters and lets armour shield the interior.
   - Cells reaching 0 go to `Removed` with world position, world velocity `v + ω × r`, and a kick along the blast normal or `LocalDir`.
   - Block-function HP is updated through the cell → block-instance map.
3. Apply `WorldImpulse` to the ship's `PhysicsVelocity` with `ApplyImpulse` at the hit point. Hits and explosions now push ships.

Cost **[I]**: a 5-cell-radius blast touches about 520 cells. That is tens of microseconds in Burst, versus a physics overlap that returns hundreds of leaves with no grid knowledge.

### 5.4 Projectiles: casts, not bodies

```csharp
public struct Projectile : IComponentData
{
    public float3 Velocity;          // world, includes shooter velocity at fire time
    public float  Radius;            // 0 → ray; >0 → sphere cast (flak/plasma)
    public float  Damage, SplashRadius, Penetration;
    public DamageType Type;
    public Entity SourceShip;
    public int    SourceBodyIndex;   // ignore in casts (refreshed if world rebuilt)
    public float  TimeLeft;
    public byte   Flags;             // Homing, ExplodeOnExpire, ...
}
public struct ProjectileDead : IComponentData, IEnableableComponent {}  // pooled, never destroyed per hit
```

**`ProjectileStepJob`** (Burst, parallel, after physics each tick):
1. Compute the segment `p0 → p1 = p0 + v·dt`. This is CCD by construction.
2. Test the segment against **analytic shields** of nearby ships (§5.10) and stop at the first intersection.
3. Run `CollisionWorld.CastRay` or `SphereCast` with a custom `ICollector` that ignores `SourceBodyIndex` (and allies, depending on policy) and keeps the closest hit.
4. On a hit:
   - Transform the hit point into the target body's grid space (`inverse(body.WorldFromBody)`).
   - Nudge the point `0.01 × cell` inward along `−normal` and `floor` it. This gives the **exact cell** with no collider child mapping.
   - Emit a `DamageEvent` and an `FxEvent`, and set `ProjectileDead`.
5. On expiry with `ExplodeOnExpire`, emit a blast `DamageEvent`.

**Pooling.** Disable instead of destroy, and re-use projectile entities (Entities Graphics instancing) up to about 10–20k. Beyond that, keep projectiles in a `NativeList` and render through a VFX Graph or `BatchRendererGroup` buffer, with no entities at all.

**Spawning.** Spawn inside `WeaponFireSystem` in the same tick:
- Use the physics transform of the ship (not the smoothed LTW).
- Add a sub-tick offset for multi-shot frames.
- Inherit the shooter's velocity.
- Remove pitch spread for the planar game, or make it data-driven.

**Homing:**
- Steer with proportional navigation (`a = N · Vc · λ̇`).
- Filter targets by allegiance via the spatial grid (§5.11).
- Target ship roots or block instances. Never raw voxel entities.

### 5.5 Explosions

- Model explosions as `DamageEvent{Radius>0}`.
- Find candidate ships with `CollisionWorld.OverlapAabb`. It returns **body** indices, not leaves, so the per-leaf asteroid multiplication (C21) disappears.
- Rasterize per ship in grid space (§5.3).
- Apply an impulse to each body, `J ∝ Amount · (1 − d/r)` along `(bodyCOM − centre)`, so blasts shove ships, hulks and debris.
- Chain reactions: reactor or ammo blocks reaching 0 function HP emit their own blast event next tick. This gives cheap, dramatic cascades.

### 5.6 Ramming

- **Propulsion.** Thrust becomes `PhysicsVelocity.ApplyLinearImpulse(mass, F·dt)` and torque, with a soft speed cap: drag force beyond `MaxSpeed`, exponential damping `v *= exp(−k·dt)`. The solver's collision response is preserved, which gives knockback, spin and glancing blows.
- **`ImpactEventSystem`** (fixed step, right after `PhysicsSimulationGroup`):
  1. Use `CollisionEvent.CalculateDetails(ref world)` to get `EstimatedImpulse` and the contact points.
  2. Compute the approach speed from a `PrevVelocity` component written in `CombatPrePhysicsGroup`: `v_n = dot(vB − vA + ωB×rB − ωA×rA, n)`, pre-solve.
  3. Energy `E = ½ μ v_n²`, with `μ = mA·mB/(mA+mB)`. Below a threshold, do nothing.
  4. Split the energy between the ships by inverse hardness at the contact cells and per-ship structural toughness.
  5. Emit one `DamageEvent` per ship per **pair per N ticks**, deduplicated through a `NativeParallelHashMap<(A,B), lastTick>`. Use `Radius ≈ contact patch` and `Penetration ∝ E / hardness`, so a heavy ship carves into a light one.
- **Sound.** At most one `SfxEvent` per pair per cooldown, with volume ∝ E.

### 5.7 Structural integrity and splitting

**`IntegritySystem`**, per ship with removals this tick, as one Burst job per ship, all ships in parallel:
1. Seeds are the 6-neighbours of removed cells that are still solid.
2. Use a tick-local `anchored` bitset (bit ops on the 4096-bit brick solid mask, 01 §6.2).
3. For each unvisited seed, flood-fill with a work queue until either:
   - it touches the anchor set (core cells, or any cell already marked `anchored` this tick); then mark everything visited as anchored and stop early, or
   - it exhausts; that is an **island**: emit `IslandEvent{Ship, CellList}`.
4. This is incremental. Cost is O(island size + path to anchor), not O(ship).
5. For huge ships, add a **brick-level graph** (brick adjacency through shared-face solid masks). Most floods run on bricks and only descend into cells at the cut.
6. Coreless ships use the largest component as the anchor. That fixes 01-B17.

**`SplitSystem` policy by island size:**

| Island cells | Result |
|---|---|
| 1–8 | **GPU debris:** `CellRemoved` to VFX (§5.8). No entity. |
| 9–~500 | **Chunk debris:** pooled rigid-body entity. Greedy box collider and small greedy mesh built in a job. Lifetime 10–30 s with dissolve shader. Damageable as a grid. |
| > ~500, or containing functional blocks | **Hulk ship:** new grid root (01 bricks) with copied cells and block instances. Mass and inertia come from cell masses. Velocity is `v + ω × (com_island − com_ship)`. Allegiance None, no AI. Salvageable, which fits the mystery and exploration loop. |

Re-sum the parent's mass properties (01-B14) and mark its bricks dirty.

### 5.8 Debris and VFX

- **`FxUploadSystem`** (presentation, main thread, about 0.1 ms):
  - One `VisualEffect` per effect family: hits, blasts, cell debris, shield ripple, sparks, smoke.
  - Each frame, copy the tick's `FxEvent`/`CellRemoved` arrays into a `GraphicsBuffer`.
  - Set `vfx.SetGraphicsBuffer("Events", buf); vfx.SetInt("Count", n); vfx.SendEvent("Spawn")`.
  - The VFX Graph spawn context reads its index into the buffer: position, velocity, colour (palette index from cell material), size.
  - Output: **cube mesh particles** with per-particle colour, spin, collision-free, 3–6 s life, scale-to-zero fade.
  - **[I]** This handles 100k flying cubes at under a millisecond of GPU time, and replaces `ParticleSpawner` and `SimplePool` entirely.
- **Caps and LOD.** Global budget per frame, e.g. 20k new debris particles. Far explosions emit fewer, bigger particles.
- **Audio.** Aggregate `SfxEvent` per frame by (sound id, spatial cell). Cap voices, and scale volume by event count.

### 5.9 Colliders (combat contract with 01)

- Each 16³ brick has a **box-merged** sub-compound: greedy boxes over the solid mask, typically 5–60 boxes instead of up to 4096 spheres. The ship compound has one child per brick. **[V]** Nested compounds are supported, with a total key budget of 32 bits (`CompoundCollider.cs:534-541`). For example, 4096 bricks (13 bits) plus 64 boxes per brick (7 bits) fits easily.
  - Alternatively, a flat compound of all brick boxes.
- Store the brick index in `ColliderBlobInstance.Entity` (or custom tags) for debugging. Combat never needs it, because hits resolve in grid space (§5.4).
- Rebuild only dirty bricks, in Burst jobs started at the end of the post-physics group. `ColliderSwapSystem` installs them next tick before physics.
- Old blobs are disposed through a cleanup-component deferred queue. This fixes C06.
- Ship colliders get no collision events by default. Enable `RaiseCollisionEvents` only on hull materials that should ram, to keep event volume low.
- Use a proper `Material` with friction around 0.3 and restitution around 0.1.

### 5.10 Beams, shields, energy

- **Beam.**
  - One persistent entity per beam weapon, toggled on and off (enableable), never re-instantiated per cycle.
  - Cast in `BeamSystem` each tick: shield test, then closest-hit `CastRay` with an ignore-self collector.
  - Emit `DamageEvent{Amount = DPS·dt, Penetration small}` and continuous `FxEvent` throttled by time, not by an RNG seeded per slot.
  - Energy comes only from `PowerAllocationSystem`.
  - The beam is a child of the weapon block entity, and that block entity's death disables the beam. No orphans.
- **Shields.**
  - Per-ship `ShieldState{Hp, MaxHp, Regen, RegenDelay, Active}` aggregated from emitter blocks. The shape is an ellipsoid or sphere in ship-local space, or per-emitter hemispheres if that design is kept.
  - Projectiles and beams intersect it analytically in their cast jobs (segment vs ellipsoid in local space).
  - A hit drains `ShieldState.Hp`, not raw energy, and emits a `ShieldRippleFx` with the local impact point (shader ripple ring buffer of 8 hits per ship).
  - Toggling is a field or tag, not the enable bit of `ShipEnergyComponent`.
- **Power.** Use `ShipPower{Generation, Capacity, Stored}` plus a `DynamicBuffer<PowerConsumer>{Priority, Demand, Entity}`. Each tick a single job allocates by priority and sets a `Powered` enable bit on consumers. Weapons, beams and shields read `Powered`, so there is no shared-energy write from parallel jobs (C08).

### 5.11 AI at fleet scale

- **Spatial index.** `SpatialHash` singleton, a `NativeParallelMultiHashMap<int2, ShipProxy>` with about 100 u cells. Rebuilt per tick in Burst.
  - `ShipProxy{Entity, pos, vel, radius, allegiance, threat, fleetId}`.
  - Also serves homing, shields broadphase, avoidance and explosions.
- **Perception at 5 Hz, staggered** (`(entity.Index + tick) % 12 == 0`).
  - Sensor-range query gives a `DynamicBuffer<Contact>` sorted by a threat score: distance, weapons, damage dealt to me (from `DamageEvent.SourceShip`), and orders.
- **Decision: utility AI at 2–5 Hz.**
  - Actions: Engage(target), Kite, Ram, Flank, Retreat-to-fleet, Escort, Patrol, Salvage.
  - Considerations are curves over normalized inputs: hull fraction, power fraction, relative fleet strength in radius, distance / preferred range, weapon arcs available, order priority.
  - Personalities become **curve sets in a blob**. The existing `AIShipNavigationComponent.Create` numbers are a reasonable first data set.
- **Steering every tick:**
  - arrive/orbit/pursuit with lead
  - separation from the spatial grid
  - predictive obstacle avoidance (sphere-cast ahead against asteroids and big hulls)
  - formation-slot seek

  Output is a desired velocity and heading. A PD controller turns that into thrust and torque within the ship's actual limits.
- **Fleets.** A `Fleet` entity holds the formation slot buffer and the shared target. Members steer to their slot unless their utility picks Engage.
- **Weapon targeting** is per weapon group. Pick a target and **sub-system** (block instance: reactors, weapons, thrusters). Solve lead with `|Δp + Δv·t| = s·t`. Fire only within the data-driven arc.
- **LOD.** Outside the simulation bubble, fleets are abstract strength numbers in the galaxy sim. Spawn ECS ships when they enter the bubble, which is where the missing C13 spawner belongs.

### 5.12 Abilities: data-driven

```csharp
public struct AbilityDef { public FixedString32Bytes Id; public AbilityEffect Effect; public float Cooldown, Duration, EnergyCost; public byte MaxCharges; public float4 Params; }
public struct AbilityDefsBlob { public BlobArray<AbilityDef> Defs; }
public struct AbilitySlot : IBufferElementData { public ushort DefIndex; public byte Charges; public float Cooldown, Active; }
public struct AbilityActivateRequest : IComponentData, IEnableableComponent { public byte Slot; }
```

- `AbilitySO` assets remain **authoring only** (icon, description, numbers) and are baked into the blob.
- Slots are granted at construction from the block data (`ShipData.AbilityIds` already exists).
- Player input or AI utility sets `AbilityActivateRequest`. `AbilitySystem` validates cooldown, charges and power, and starts the effect.
- One small system per `AbilityEffect`: Boost, ShieldOverdrive, Overclock (a `StatModifier` buffer that weapons read), EMP pulse (emits a `DamageEvent` of type EMP), and so on.
- The HUD reads `AbilitySlot`. Delete the MonoBehaviour controller and the SO-side state.

### 5.13 Determinism and fixed step

- All gameplay runs in the fixed step, at a constant dt.
- Input is sampled into a per-tick `PlayerCommand`.
- RNG uses `Random.CreateFromIndex(hash(tick, entity.Index, salt))`. No `UnityEngine.Time`, no `frameCount`.
- Damage is applied in a sorted order and single-writer, so results do not depend on thread scheduling.
- This is **reproducible on one machine**, not cross-platform lockstep, which is enough for a single-player game and replays/debugging.
- If multiplayer is ever planned, it needs the NetCode-style split of predicted and server state. Remove the current half-NetCode filters until then.

### 5.14 What to keep

| Keep | Why |
|---|---|
| `FireWeaponTag : IEnableableComponent` pattern | Cheap and correct way to gate firing |
| `WeaponComponent` field set (split into static blob data + small runtime state) | Content already depends on it |
| Turret rotation and arc clamp maths (`WeaponTurretSystem.cs:156-201`) | Works; move it into `WeaponAimSystem` |
| Energy aggregation at construction and decrement on loss (`ShipConstructionJobSystem.cs:353-361`, `VoxelDestructionSystem.cs:247-288`) | Same idea as the power model, driven by block-instance death |
| Cached shared collider singletons (`ProjectileColliderCache`, `DebrisColliderCache`) | Right idea: shared blobs disposed in `OnDestroy` |
| Personality presets (`AIShipNavigationComponent.Create`) | First data for utility curves |
| BFS-from-core concept | Becomes the incremental integrity pass |
| "Convert in place" of a detached cell into a body (`VoxelDestructionSystem.cs:291-342`) | Pattern for chunk debris (batched and pooled) |
| Material-property overrides for shield flash and alpha, arc angle and colour | Good Entities Graphics usage |
| `ShipConstructionLinkSystem` managed link via `ShipBuildRequestManaged` | Fine bridge for UI, until UI reads ECS directly |

### 5.15 Migration path

**Phase 0: stop the bleeding (1–3 days).** See §6. These are mostly one-liners. Exit when ram damage is FPS-independent, hits land on the right cell, there are no leaks and no ECB exceptions.

**Phase 1: pipeline and ordering (≈1–2 weeks).**
1. Create `CombatPrePhysicsGroup`, `CombatPostPhysicsGroup` and `CombatEndECBSystem`. Move every combat system into them with explicit order, and move movement to forces.
2. Introduce `CombatStreams` and `DamageResolveSystem`. Route Lifetime, Beam and ShipCollisionDamage through `DamageEvent` and delete their direct `VoxelHealth` writes. Per-voxel entities are still fine at this point; the resolver maps cell to entity through a temporary `NativeHashMap<int3, Entity>` per ship.
3. Replace trigger-based projectile hits with `ProjectileStepJob` casts. Keep projectile entities and pool them.
4. Add a minimal enemy spawner from `CombatEncounter` formations (C13), so everything above can be tested.

Exit when a 10-ship skirmish runs at stable damage numbers at 30, 60 and 144 FPS.

**Phase 2: physicality (≈1–2 weeks).**
- Impulse-based ramming with dedupe.
- Explosion rasterization with impulses.
- Debris inherits velocity.
- Hulk-on-death instead of deletion.
- VFX Graph hit and debris upload, replacing `ParticleSpawner`.

**Phase 3: grid storage (≈3–6 weeks, joint with 01).**
- Bricks, cells as data, functional block entities, brick meshes and box colliders, dirty rebuild jobs.
- The resolver switches from the voxel-entity map to brick cells.
- Remove the per-voxel `VoxelHealth`/`ShipVoxel`/`ShipVoxelIndex`/`VoxelPhysicsUtils`.

**Phase 4: integrity and splitting (≈2 weeks).** Incremental islands, the size-based split policy, hulk ships, chunk debris pool and caps.

**Phase 5: AI, shields, power, abilities (≈3–4 weeks).** Spatial hash, utility AI, fleets, analytic shields, power allocation, data-driven abilities, HUD reading ECS.

**Benchmarks to gate the phases [I targets]:**
- 30 ships × 20k cells at 60 FPS.
- 5k live projectiles under 1 ms CPU.
- A 2k-cell cut, including island detection and split, under 2 ms amortized.
- A blast of radius 6 cells under 50 µs per ship.
- Zero GC allocations per frame in combat.

---

## 6. Quick wins (ordered by value / effort)

| # | Change | Where | Fixes |
|---|---|---|---|
| 1 | Bits: `return 32 - math.lzcnt((uint)count);` (or read leaf entity) | `VoxelShip/VoxelPhysicsUtils.cs:63-72` | C01 |
| 2 | Move `ShipCollisionDamageSystem` into FixedStep right after `ShipCollisionSystem_Managed`, or clear the buffer after consuming. Move `ProjectileCollisionSystem` into FixedStep after `PhysicsSimulationGroup`. | `ShipCollisionSystem.cs:167-169`, `ProjectileCollisionSystem.cs:24` | C02 |
| 3 | Dispose the old compound before assigning; add cleanup disposal on ship destroy | `ShipColliderRebuildSystem.cs:178-181` | C06 |
| 4 | Only set `ColliderRebuildRequired` when absent (`HasComponent` check via lookup, or enableable component with a deadline) | `VoxelDestructionSystem.cs:242` | C07 |
| 5 | Stop adding voxels to the ship LEG; on death, convert the remaining voxels to debris instead of destroying | `ShipConstructionJobSystem.cs:321`, `ShipHealthSystem.cs:29-43` | C04, C05 |
| 6 | Add explicit `[UpdateAfter]`: Fire after Turret; Lifetime after ProjectileCollision; Destruction after Lifetime, ShipCollisionDamage and Connectivity; Connectivity after Lifetime and Beam | the listed systems | C05, C28, C30 |
| 7 | Hit position: store `PrevPosition` on projectiles. On a trigger hit, `CastRay(prev → current)` against the hit body and use the exact hit point. Stopgap: set `ExplosionRadius = 0` for bullets so the direct-hit (`ColliderKey`) path is used. | `ProjectileCollisionSystem.cs:103-111`, projectile assets | C03 |
| 8 | Debris velocity: `ship.Linear + cross(ship.Angular, r) + random small + kick` | `VoxelDestructionSystem.cs:309-328` | C11 |
| 9 | Add `FlakShell` (type 6) to `ProjectileDatabaseAuthoring.prefab`; give missile and flak non-zero damage | prefab and assets | C16 |
| 10 | Homing: skip same allegiance (`targetMaster.allegiance == source allegiance`) and target ship roots only | `ProjectileHomingSystem.cs:77-79`, `WeaponFireSystem.cs:315` | C17 |
| 11 | Cooldown accumulator: `FireCooldown += 1f / FireRate` | `WeaponFireSystem.cs:353` | C18 |
| 12 | `RequireForUpdate<BeamComponent>()` in `BeamSystem`, `RequireForUpdate<HomingComponent>()` and drop `Complete()` in Homing (dispose arrays with `.Dispose(state.Dependency)`), early-out in `ShieldSystem` when there are no emitters | `BeamSystem.cs:28-31`, `ProjectileHomingSystem.cs:15-19, 47-50`, `ShieldSystem.cs:48` | §4.4 |
| 13 | Replace `UnityEngine.Time.frameCount` with a `SystemAPI.Time.ElapsedTime`-based seed | `VoxelDestructionSystem.cs:133` | C27 |
| 14 | Hit VFX: set `CFXR Explosion 1` stop action to Disable (1) and despawn on stop, or switch to VFX Graph; null-check `ParticleSpawner.Instance` | `ParticleSpawner.cs`, `ProjectileHitVisualSystem.cs:21` | C29 |
| 15 | Fix shield prefab singleton (authoring with `GameObject` field, like `ThrusterVisualAuthoring`) | `ShieldBubblePrefabAuthoring.cs` | C14 |
| 16 | Use `PhysicsStep.Default` and override gravity; move `PhysicsStepAuthoring` into the subscene or delete it | `PhysicsSetup.cs:15-21` | C31 |
| 17 | Delete the dead items in §3. About 1,000 lines and 6 assets, with no behaviour change. | §3 | hygiene |
| 18 | Remove vertical spread (`spreadAngleVertical = 0`) | `WeaponFireSystem.cs:241-242` | C20 |
| 19 | One sound per collision pair per 0.25 s; don't create `PlaySoundEvent` entities per event | `ShipCollisionSystem.cs:241-249` | C09, §4.3 |
| 20 | Remove `EditorBuildSettings` entry for the deleted `CombatScene.unity` | `ProjectSettings/EditorBuildSettings.asset` | hygiene |

---

## Appendix: key evidence commands

- Script usage: `grep -rl <guid> Assets --include='*.unity' --include='*.prefab' --include='*.asset'`, with each GUID from the `.cs.meta`. Results:
  - `CameraShake`, `CameraShakeController`, `AsteroidMiner`, `BoostCameraController`, `OverclockAbilitySO`: 0 references.
  - `PortalTrigger.cs`: only `CombatExitPortal.prefab`, itself unreferenced.
  - `ParticleSpawner`: only `GalaxyMapScene.unity`.
- Caller checks: grep found no callers for `AddAbility|EquipAbility|TryUseAbility`, `OnShipDestroyed`, `HandlePlayerDefeat()`, `CaptureShipDamage`, `DespawnShips`. `SpawnShip(` has one caller, `OpenWorldGalaxyManager.cs:399`.
- Package sources:
  - Compound key bits: `Library/PackageCache/com.unity.physics@8d312bc11767/Unity.Physics/Collision/Colliders/CompoundCollider.cs:1294`; `Collider.cs:1526-1552`.
  - Kinematic when no `PhysicsMass`: `PhysicsWorldBuilder.cs:831`.
  - Dynamic body query has no Parent exclusion: `PhysicsWorldData.cs:143-147`.
  - Depenetration defaults: `Dynamics/Solver/Solver.cs:410-413`.
  - Substep clamp: `Dynamics/Simulation/Simulation.cs:307`.
  - Entities: `Simulate` added by default (`Unity.Entities/IComponentData.cs:408`); system sort tie-break by type hash (`ComponentSystemSorter.cs:128-131`); `FixedStepSimulationSystemGroup` OrderFirst at 1/60 (`DefaultWorld.cs:327-352`); `TransformSystemGroup` has no group attribute (`Unity.Transforms/EndFrameTransformSystems.cs:12-13`); add-component existence assert is debug-only (`EntityComponentStoreDebug.cs:514-518`); destroy skips missing LEG members (`EntityComponentStoreCreateDestroyEntities.cs:121-140`).
- Data:
  - Projectile assets (`Assets/Resources/Items/Blocks/Weapons/Projectiles/*.asset`): `ExplosionRadius` 0.5/1, `Damage` 0 for missile and flak, `VisualType` 6 for flak.
  - The database prefab maps types 0–5 only.
  - `GlobalShipScale` 0.5, projectile scale 4 (`EntitiesSubscene.unity:310-311`).
  - Hit FX `stopAction: 2` (`CFXR Explosion 1.prefab:49`).
