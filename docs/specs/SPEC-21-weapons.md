# SPEC-21 — Weapons, projectiles, beams and explosions

| | |
|---|---|
| **Status** | Draft |
| **Area code** | WPN (task IDs are `WPN-001`, `WPN-002`, …) |
| **Module(s)** | `Game.Simulation` (folders `Assets/Game/Simulation/Weapons/`, `Assets/Game/Simulation/` root for `Allegiance`/`FactionRelations`); `Game.Data` (`Assets/Game/Data/Definitions/Weapons/`); `Game.Presentation` (`Assets/Game/Presentation/Weapons/` for the turret visual sync) |
| **Depends on specs** | SPEC-02 (`SimClock`, `SimTime`, `StableHash`, `ContentHash`, groups, `GameLog.Burst`); SPEC-04 (`BlockDefsRef`, `OrientationTablesRef`, `WeaponMountParams`, `ContentDefinition`, `ContentCatalog`, DATA-010 weapon link); SPEC-10 (`VoxelGrid`, `BlockInstance`, `FunctionalBlock`, `FunctionalComponentRegistration`, `GridCellReader`, `VoxelRaycast`, `GridSpawning`, `GridSpawnFlags.EditorPreview`); SPEC-12 (query contract, decision 4); SPEC-13 (`DamageEvent`, `DamageStreams`, `ExplosionRequest`, `DamageProducer`, `DamageType`, `Hulk`); SPEC-20 (`ShipCommand`, `PlayerShip`, `PlayerCommandApplySystem`, `GridSpaceMath`, `PlanarConstraintSystem`); SPEC-22 (`ShieldState`, `ShieldMath`, `ShieldHit`, `ShieldHitStreams`, `ShipStats` multipliers, `Powered`, `PowerGridSetup`); SPEC-23 (`SpatialHash`, `SpatialProxy`); SPEC-24 (`AbilityGridSetup`); SPEC-63/64 (`FxEvent`, `FxEventQueue`, `SfxEvent`, `SfxEventQueue`, `CombatAudioCues`) |
| **Depended on by** | SPEC-01 (`CombatGridSetup`, `Allegiance`, `WeaponContent` boot singletons); SPEC-04 (`WeaponDefinition` for DATA-010); SPEC-22 (`WeaponDefsBlob` draw, fire groups, DPS, range); SPEC-23 (`ShipTarget`, `WeaponControl`, `Allegiance`, `FactionRelations`, ranges); SPEC-24 (damage abilities reuse the producers); SPEC-30 (shifts the projectile store on rebase); SPEC-33 (fills `FactionRelations`); SPEC-61 (`WeaponState`, `ShipTarget`); SPEC-63 (`BeamState`, projectile store, muzzle/hit/explosion events); SPEC-64 (fire cues, `BeamState`); SPEC-70 (aim/fire commands); SPEC-72 (MIG-009 writes §4.9 values) |
| **Milestone** | M2 (WPN-001 in M1, WPN-016 in M4) |

> Read [01-architecture.md](../01-architecture.md) (§5, §5.1, §6.3–§6.6, §8, §11), [02-conventions.md](../02-conventions.md), [07-implementation-traps.md](../07-implementation-traps.md) §2 and §4, [SPEC-12](SPEC-12-voxel-physics.md) decision 4, [SPEC-13](SPEC-13-damage-destruction.md) §4–§5.1 and [SPEC-22](SPEC-22-power-shields-stats.md) §4.3–§4.4 before this spec. This spec may not contradict them.

## 1. Purpose

Weapons are functional blocks: each weapon block has an entity that aims (turrets traverse within an arc), cools down in fixed ticks, fires bursts of projectiles or beam pulses, and draws power while its fire group is held. Projectiles live in one struct-of-arrays store, step in a parallel Burst job, and hit **cells**, not colliders: a segment query over the spatial hash finds candidate grids, then each candidate is tested against its analytic shield and, through the shield, against its cells with the voxel DDA. Hits become `ShieldHit`s, `DamageEvent`s and `ExplosionRequest`s in deterministic streams; the explosion system turns blasts into shield and hull damage per overlapped grid. The result is exact (no wrong-cell hits, 01-B2), lag-free with respect to colliders (SPEC-12 decision 4), deterministic at any frame rate, and cheap enough for 5,000 live projectiles. The old prototype drained energy per shot and per second twice (02-C22), let beams draw through targets (R26), and spawned and destroyed projectile entities every shot; none of that is possible here.

## 2. Scope

**In scope:**
- `WeaponDefinition` (+ blob `WeaponDefsBlob`, singleton `WeaponDefsRef`), the v2 weapon value table (§4.9), legacy mapping rules for SPEC-72.
- Weapon functional components: `WeaponState`, `WeaponMount`, `WeaponAim`, `BeamState`; ship root components: `WeaponControl`, `ShipTarget`; `Allegiance`, `FactionRelations` (default content; SPEC-33 fills it); `CombatGridSetup`.
- Systems: `WeaponInitSystem`, `PlayerTargetingSystem`, `WeaponAimSystem`, `WeaponFireSystem`, `ProjectileStepSystem`, `BeamStepSystem`, `ExplosionSystem`, `WeaponVisualSyncSystem`.
- `ProjectileStore` (struct-of-arrays projectiles), homing, flak fuses, `CombatHitTest` (shield/hull/celestial narrow phase).
- Muzzle, hit and explosion `FxEvent`/`SfxEvent`s; turret rotation visuals.
- Tests: hit exactness, determinism, the 5,000-projectile gate, weapon balance against SPEC-22 R3.

**Out of scope (and who owns it instead):**
- Applying damage to cells, integrity, splits: SPEC-13. Shield HP and overflow: SPEC-22 `ShieldSystem`. Power grants and `Powered`: SPEC-22.
- The spatial hash and AI target choice: SPEC-23 (this spec states the API it needs, §4.8). Faction content and relation changes: SPEC-33.
- Drawing tracers, beams, muzzle flashes and explosions: SPEC-63. Playing sounds: SPEC-64.
- Ability-driven damage: SPEC-24 (uses the same producer contracts).
- Per-instance fire-group assignment in the editor: M4 (WPN-016); v1 uses each weapon's default groups.

## 3. Decisions (binding)

1. **Hits are tested against cells and analytic shields, never against physics colliders.** Broad phase: `SpatialHash.QuerySegment`/`QuerySphere` (SPEC-23) over proxies of every grid root and celestial body. Narrow phase: shield ellipsoid (`ShieldMath`, last tick's `ShieldState`), then `VoxelRaycast` through the grid's cells, then celestial spheres. Reason: SPEC-12 decision 4 (colliders lag and are conservative); exact cells (01-B2); determinism.
2. **Projectiles are rows in `ProjectileStore`, not entities.** Firing appends, stepping updates in place, dead rows are removed by a stable compaction at the end of the step. No structural change ever happens for a projectile. Reason: 5,000-projectile gate; zero ECB traffic; deterministic order by construction.
3. **Every event a projectile, beam or explosion produces is written to `NativeStream`s** with foreach index = batch/chunk index (`DamageStreams`, `ShieldHitStreams`), and carries `(Source = shooter grid root, Producer, Sequence = ShotId)`; consumers sort by SPEC-13/SPEC-22 keys. Fx/Sfx go to their `NativeQueue`s. Reason: architecture §5.1.
4. **A projectile never hits its own shooter grid.** It hits everything else in its path, allies included (friendly fire). Reason: physical consistency; AI avoids firing through allies (SPEC-23).
5. **Projectiles inherit the shooter's linear velocity** (not angular). Reason: shots fired at speed stay readable relative to the ship; AI lead accounts for it.
6. **Aim is a 3D point.** Turrets yaw about the ship's up axis within `±TraverseHalfArc` at `TraverseRate`; elevation toward the aim point is instant and clamped to `±MaxElevation`. Fixed weapons (`TraverseHalfArc = 0`) fire along their mount direction with elevation toward the aim point and a convergence yaw of at most `FixedConvergenceDeg` (3°). Reason: what the player points at is what is hit; fixed guns converge on the target.
7. **Fire timing is in fixed ticks with fractional accumulators** (`CooldownRemainingTicks += 60 / rate` per trigger; `−1` per tick; fire when `≤ 0`). Bursts use the same accumulator per round. Reason: exact average rates; frame-rate independence (architecture §5.1).
8. **Randomness** (spread, flak fuses) comes from `Random.CreateFromIndex((uint)StableHash.Combine(StableHash.Combine(StableHash.Combine(tick, shooterPersistentId), shotId), (ulong)RngPurpose.WeaponSpread))`. Reason: conventions §2.10; stable across runs and saves.
9. **Weapons draw power while their fire group is held (or autofire is engaged with a target), not per shot** (SPEC-22 decision 7). A weapon fires only if its entity has no `Powered` component or the bit is enabled.
10. **Beams are pulses.** A trigger starts a pulse of `BeamPulseTicks`; each tick of the pulse the beam casts once from the muzzle and applies `Damage / BeamPulseTicks` to the first thing hit; `BeamState.EndWorld` is that hit point or the range end (never past the target, R26).
11. **Explosions** come only from `ExplosionRequest`s (SPEC-13 contract): projectiles (on hit or fuse), reactors and core breaches (SPEC-13), abilities (SPEC-24). `ExplosionSystem` is the single consumer. A blast first meets each overlapped grid's shield (blast `ShieldHit`), else damages its hull (blast `DamageEvent`). Reason: shields first (architecture §6.3).
12. **Fire groups in v1 come from `WeaponDefinition.defaultFireGroups`** (bit g = fire group g, SPEC-20 `ShipCommand.FireGroups`). Reason: no per-instance storage until M4.
13. **Ship-level aim modes:** `WeaponControl.Autofire == 0` → weapons aim at `ShipCommand.AimWorld` and fire when their group is held. `Autofire == 1` → weapons aim at `ShipTarget` with lead and fire when the target is in range and arc. The player toggles autofire with the `AutofireToggle` edge; AI ships use autofire.
14. **Dormant ships (SPEC-11) do not aim or fire** (architecture §11.9); their projectiles already in flight keep stepping.
15. **Placement and folders:** `Assets/Game/Simulation/Weapons/` → `Game.Simulation.Weapons` (jobs in `Weapons/Jobs/`); `Allegiance`, `FactionRelations`, `FactionStance`, `CombatGridSetup` in `Assets/Game/Simulation/` → `Game.Simulation` (SPEC-01 references `Game.Simulation.Allegiance`); `WeaponDefinition` and enums in `Assets/Game/Data/Definitions/Weapons/` → `Game.Data.Definitions`; `WeaponVisualSyncSystem` in `Assets/Game/Presentation/Weapons/` → `Game.Presentation.Weapons`.
16. **Singleton access** follows SPEC-12 §5.1 (handle-guarded, `GetSingleton` read-only, `Acquire/Release`) for `ProjectileStore`; Fx/Sfx queues are taken read-only with `[NativeDisableContainerSafetyRestriction]` on their `ParallelWriter` (concurrent enqueue is thread-safe; the drain completes all readers). No `GetSingletonRW` on a shared stream singleton in the fixed step.

## 4. Data model

All types are **public contract** unless marked *internal*.

### 4.1 Content (`Game.Data.Definitions`)

```csharp
namespace Game.Data.Definitions
{
    public enum WeaponDelivery : byte { Projectile = 0, Beam = 1, Missile = 2 }               // add at end only
    public enum ProjectileVisual : byte { StandardBullet = 0, RailgunSlug = 1, FlakShell = 2, Missile = 3, LaserBeam = 4 }
    [System.Flags] public enum WeaponFireGroups : byte { None = 0, Group1 = 1, Group2 = 2, Group3 = 4 }   // bit g = ShipCommand fire group g

    /// <summary>One weapon type. Linked from BlockDefinition.weapon (SPEC-04 DATA-010). Implements SPEC-21 §4.1.</summary>
    [ContentIdPrefix("wpn_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Weapon", fileName = "wpn_new")]
    public sealed class WeaponDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private string displayName;
        [UnityEngine.SerializeField] private WeaponDelivery delivery;
        [UnityEngine.SerializeField] private Game.Data.Combat.DamageType damageType;    // Kinetic, Energy or Explosive (Ram is rejected, WPN-E008)
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float damage;              // per round (projectile/missile direct hit) or per pulse (beam)
        [UnityEngine.SerializeField, UnityEngine.Min(1)] private float penetrationCells = 1; // point hits: max solid cells destroyed along the ray
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float explosionRadius;     // m; 0 = no explosion
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float explosionDamage;     // raw damage at the blast centre
        [UnityEngine.SerializeField] private bool explodeOnExpire;                          // fuse: explode at end of life (flak, missiles)
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float impulsePerHit;       // N·s along the shot direction (knockback)
        [UnityEngine.SerializeField, UnityEngine.Min(0.01f)] private float fireRate = 1;    // triggers per second
        [UnityEngine.SerializeField, UnityEngine.Min(1)] private int burstCount = 1;        // rounds per trigger
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float burstInterval;       // s between rounds of a burst
        [UnityEngine.SerializeField, UnityEngine.Range(1, 32)] private int pelletsPerRound = 1;
        [UnityEngine.SerializeField, UnityEngine.Range(0, 45)] private float spreadDeg;     // cone half-angle per pellet
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float projectileSpeed;     // m/s (projectile, missile)
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float projectileRadius = 0.1f; // m: hit-test inflation and tracer width
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float lifetimeMin = 1, lifetimeMax = 1; // s (min = max: fixed)
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float homingTurnRateDeg;   // missiles: deg/s; 0 = straight
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float beamRange;           // m (beam)
        [UnityEngine.SerializeField, UnityEngine.Min(1f / 60f)] private float beamPulseSeconds = 0.5f;
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float beamWidth = 0.3f;    // m (visual)
        [UnityEngine.SerializeField, UnityEngine.Range(0, 180)] private float traverseHalfArcDeg; // 0 = fixed
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float traverseRateDeg = 180;
        [UnityEngine.SerializeField, UnityEngine.Range(0, 60)] private float maxElevationDeg = 20;
        [UnityEngine.SerializeField, UnityEngine.Range(0, 30)] private float fireConeDeg = 3;   // turrets fire when within this of the desired yaw
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float powerDraw;            // E/s while the group is held
        [UnityEngine.SerializeField] private WeaponFireGroups defaultFireGroups = WeaponFireGroups.Group1;
        [UnityEngine.SerializeField] private ProjectileVisual visual;
        [UnityEngine.SerializeField] private UnityEngine.Color tracerColor = UnityEngine.Color.white;
        [UnityEngine.SerializeField] private AudioCueDefinition fireCue;     // null = silent
        [UnityEngine.SerializeField] private FxDefinition muzzleFx;          // null = FxKind default (SPEC-63 Q8)
        [UnityEngine.SerializeField] private FxDefinition hitFx;             // null = FxKind default
        // Public getters with the same names in PascalCase.
        public override void Validate(ValidationScope scope, ContentValidationContext context);   // WPN-E001..E006 (§7)
    }
}
```

### 4.2 Blob and singleton (`Game.Simulation.Weapons`)

```csharp
namespace Game.Simulation.Weapons
{
    using Unity.Entities; using Unity.Mathematics; using Game.Core; using Game.Data.Combat; using Game.Data.Definitions;

    /// <summary>Baked weapon. Index 0 = "none". Times in ticks, angles in radians. Implements SPEC-21 §4.2.</summary>
    public struct WeaponDef
    {
        public ushort Index;
        public WeaponDelivery Delivery;
        public DamageType Type;
        public ProjectileVisual Visual;
        public byte DefaultFireGroups;       // WeaponFireGroups bits
        public byte ExplodeOnExpire;
        public byte IsFixed;                 // TraverseHalfArc == 0
        public float Damage, PenetrationCells, ExplosionRadius, ExplosionDamage, ImpulsePerHit;
        public float TriggerIntervalTicks;   // 60 / fireRate
        public int BurstCount;
        public float BurstIntervalTicks;     // burstInterval × 60
        public int PelletsPerRound;
        public float SpreadRad;
        public float ProjectileSpeed, ProjectileRadius;
        public uint LifetimeMinTicks, LifetimeMaxTicks;   // SimTime.SecondsToTicks, ≥ 1
        public float HomingTurnRateRad;      // per second
        public float BeamRange;
        public uint BeamPulseTicks;          // ≥ 1
        public float BeamWidth;
        public float TraverseHalfArcRad, TraverseRateRad, MaxElevationRad, FireConeRad;
        public float PowerDraw;              // E/s (SPEC-22 reads)
        public float Range;                  // m: projectile/missile speed × lifetimeMax; beam BeamRange (SPEC-22, SPEC-23 read)
        public float SustainedDps;           // §4.9 formula (SPEC-22 reads)
        public ContentHash FireCue, MuzzleFx, HitFx, IdHash;
        public uint TracerColor;             // sRGB RGBA8 packed R | G<<8 | B<<16 | A<<24
    }

    public struct WeaponDefsBlob { public BlobArray<WeaponDef> Weapons; }    // [0] = none (all zero), [i] = IndexOf<WeaponDefinition>
    public struct WeaponDefsRef : IComponentData { public BlobAssetReference<WeaponDefsBlob> Value; }   // boot singleton

    public struct WeaponTuning : IComponentData           // boot singleton from config_weapons (WeaponTuningDefinition, §4.7)
    {
        public int MaxProjectiles;           // 8192
        public float FixedConvergenceRad;    // radians(3)
        public float ExplosionImpulsePerDamage; // 2 N·s per damage point at the centre
        public float MaxTargetRange;         // 2500 m (TargetNext)
        public float UnderCursorRadius;      // 30 m (TargetUnderCursor)
        public int MaxCandidates;            // 32 broad-phase candidates per query
    }

    /// <summary>Boot factory (SPEC-01 BootSingletons): WeaponDefsRef, WeaponTuning and the default FactionRelations.</summary>
    public static class WeaponContent
    {
        public static void CreateSingletons(EntityManager em, Game.Data.Services.ContentCatalog catalog, System.Collections.Generic.List<Entity> created);
        public static BlobAssetReference<WeaponDefsBlob> BuildBlob(Game.Data.Services.ContentCatalog catalog);   // §5.1
        public static float ComputeSustainedDps(in WeaponDef w);   // §4.9
    }
}
```

### 4.3 Allegiance and factions (`Game.Simulation`)

```csharp
namespace Game.Simulation
{
    using Unity.Collections; using Unity.Entities;

    /// <summary>Faction and team of a grid root (architecture §6.4). Written only at spawn (CombatGridSetup); SPEC-33 may reassign through its own system.</summary>
    public struct Allegiance : IComponentData, System.IEquatable<Allegiance>
    {
        public byte Faction;    // index into FactionRelations; 255 = none (asteroids, derelicts)
        public byte Team;       // encounter/fleet group; same Faction + same Team = wingmates
        public static Allegiance Player => new Allegiance { Faction = 0, Team = 0 };
        public static Allegiance None => new Allegiance { Faction = 255, Team = 0 };
        public bool Equals(Allegiance o) => Faction == o.Faction && Team == o.Team;
    }

    public enum FactionStance : byte { Neutral = 0, Friendly = 1, Hostile = 2 }

    /// <summary>Singleton relation matrix. Default created by WeaponContent (factions 0 player, 1 hostile, 2 neutral);
    /// SPEC-33 rebuilds it from FactionDefinitions. Only writer after boot: SPEC-33's faction system.</summary>
    public struct FactionRelations : IComponentData
    {
        public NativeArray<byte> Stance;     // Count × Count, [a * Count + b], FactionStance values; diagonal = Friendly
        public int Count;                    // ≤ 64
        public FactionStance Get(byte a, byte b) =>
            a >= Count || b >= Count ? FactionStance.Neutral : (FactionStance)Stance[a * Count + b];
        public bool IsHostile(in Allegiance a, in Allegiance b) =>
            a.Faction != 255 && b.Faction != 255 && a.Faction != b.Faction && Get(a.Faction, b.Faction) == FactionStance.Hostile;
        public static FactionRelations CreateDefault(Allocator allocator);   // 3×3: 0↔1 Hostile, 2 Neutral to all, diagonal Friendly
        public void Dispose();
    }

    /// <summary>Spawn helper called by ShipSpawnService (SPEC-01) and SPEC-31/33 spawners on the request entity, main thread.</summary>
    public static class CombatGridSetup
    {
        /// Adds Allegiance(value), WeaponControl, ShipTarget; then PowerGridSetup.AddComponents (SPEC-22) and AbilityGridSetup.Add (SPEC-24).
        public static void AddAll(EntityManager em, Entity root, Allegiance allegiance);
    }
}
```

### 4.4 Weapon entity components (`Game.Simulation.Weapons`, registered for `FunctionKind.Weapon`)

```csharp
namespace Game.Simulation.Weapons
{
    using Unity.Entities; using Unity.Mathematics; using Game.Core;

    /// <summary>Static geometry, grid space. Only writer: WeaponInitSystem (new or re-parented weapon entities).</summary>
    public struct WeaponMount : IComponentData
    {
        public float3 PivotLocal;        // grid-local metres: footprint centre (GridSpaceMath.InstanceCenter)
        public float3 MuzzleLocal;       // grid-local metres at yaw 0: BlockPointToGrid(WeaponMountParams.MuzzleOffsetCells)
        public float3 ForwardLocal;      // grid-local unit: BlockDirToGrid(+Z)
        public float3 UpLocal;           // grid-local unit: BlockDirToGrid(+Y)
        public ushort WeaponIndex;       // BlockDef.Function.Weapon.WeaponIndex; 0 = unarmed (never fires)
        public byte Initialized;         // 1 after WeaponInitSystem filled it
        public byte Reserved;
    }

    /// <summary>Turret state. Only writer: WeaponAimSystem.</summary>
    public struct WeaponAim : IComponentData
    {
        public float Yaw;                // radians, offset from ForwardLocal about UpLocal, clamped to ±TraverseHalfArc
        public float DesiredYaw;         // radians (unclamped desire, for HUD/debug)
        public float3 FireDirLocal;      // grid-local unit direction a round fires along this tick (before spread)
        public byte HasAim;              // 1 if an aim point or target solution existed this tick
        public byte Aligned;             // 1 if |DesiredYaw − Yaw| ≤ FireCone (always 1 for fixed weapons with HasAim)
        public byte InRange;             // autofire: target within Range
        public byte Reserved;
    }

    /// <summary>Fire timing. Only writer: WeaponFireSystem. Field names fixed by SPEC-61 Q2.</summary>
    public struct WeaponState : IComponentData
    {
        public byte Group;                   // fire-group bits (= WeaponDef.DefaultFireGroups in v1)
        public byte Firing;                  // 1 if a round or pulse was released this tick
        public ushort RoundsLeftInBurst;     // remaining rounds of the current burst
        public float CooldownRemainingTicks; // trigger accumulator (decision 7)
        public float CooldownTicks;          // = TriggerIntervalTicks / FireRateMultiplier (HUD)
        public float BurstRemainingTicks;    // burst accumulator
        public uint ShotCounter;             // rounds fired since spawn (ShotId low 16 bits)
    }

    /// <summary>Beam pulse state on every weapon entity (Active stays 0 for non-beams). Only writer: WeaponFireSystem (start/stop)
    /// and BeamStepSystem (EndWorld, HitSomething). Field names fixed by SPEC-63 §6.5.</summary>
    public struct BeamState : IComponentData
    {
        public float3 MuzzleLocal;       // grid space muzzle this tick (after turret yaw)
        public float3 EndWorld;          // local-frame world point where the beam ends (hit point or range end)
        public float Width;              // m
        public byte Active;              // 1 while a pulse is running
        public byte HitSomething;
        public ushort TicksLeft;         // remaining ticks of the pulse
        public ContentHash Fx;           // WeaponDef.HitFx (beam look override)
    }
}
```

### 4.5 Ship root components (`Game.Simulation.Weapons`)

```csharp
namespace Game.Simulation.Weapons
{
    public enum WeaponAimMode : byte { AimPoint = 0, TargetLead = 1 }

    /// <summary>Per-ship weapon control. Writers: PlayerTargetingSystem for the PlayerShip; SPEC-23 for AI ships (decision 13).</summary>
    public struct WeaponControl : Unity.Entities.IComponentData
    {
        public byte Autofire;            // 1 = fire at ShipTarget when in range and arc
        public WeaponAimMode AimMode;    // Autofire implies TargetLead
        public ushort Reserved;
    }

    /// <summary>The ship's current target (SPEC-61 Q4). Writers: PlayerTargetingSystem (player), SPEC-23 (AI). Entity.Null = none.</summary>
    public struct ShipTarget : Unity.Entities.IComponentData
    {
        public Unity.Entities.Entity Value;
        public uint SinceTick;           // SimClock.Tick when Value last changed
    }
}
```

### 4.6 `ProjectileStore` (`Game.Simulation.Weapons`)

```csharp
namespace Game.Simulation.Weapons
{
    using Unity.Collections; using Unity.Entities; using Unity.Jobs; using Unity.Mathematics; using Game.Data.Combat;

    [System.Flags] public enum ProjectileFlags : byte { None = 0, Homing = 1, ExplodeOnExpire = 2, Dead = 4, PlayerSource = 8 }

    /// <summary>One live projectile (64 bytes). Implements SPEC-21 §4.6.</summary>
    public struct ProjectileRow
    {
        public float3 Position;          // local frame, end of the last step
        public float3 Velocity;          // m/s
        public Entity Source;            // shooter grid root
        public Entity Target;            // homing target root or Entity.Null
        public uint ShotId;              // (instance index & 0xFFFF) << 16 | (ShotCounter & 0xFFFF)
        public uint DeathTick;           // SimClock.Tick at which it expires
        public ushort Weapon;            // WeaponDef index
        public DamageType Type;
        public ProjectileFlags Flags;
        public float DamageMultiplier;   // shooter ShipStats.DamageMultiplier at fire time (1 without SPEC-22)
    }

    /// <summary>Singleton owned by ProjectileStepSystem (created in OnCreate). Access per SPEC-12 §5.1. Read by SPEC-63 tracers. Follows the
    /// floating origin itself through the Core OriginShiftRecord (ApplyPendingShift); SPEC-30 never touches it.</summary>
    public struct ProjectileStore : IComponentData
    {
        public NativeList<ProjectileRow> Rows;           // fire order; compacted stably after each step
        public NativeReference<JobHandle> Access;        // main thread only
        public NativeReference<ProjectileStoreStats> Stats;
        public NativeReference<uint> AppliedShiftSequence;   // last OriginShiftRecord.Sequence applied to Rows (read by SPEC-63 to correct a pending shift)
        public static ProjectileStore Create(int capacity);   // Allocator.Persistent
        public void Dispose();                                // completes Access first
        public JobHandle Acquire(JobHandle dependsOn) => JobHandle.CombineDependencies(dependsOn, Access.Value);
        public void Release(JobHandle lastJob) { Access.Value = lastJob; }
        public void CompleteAccess() { Access.Value.Complete(); }   // never in FixedStepSimulationSystemGroup
        /// Inside a single-threaded job: if record.Sequence != applied → (StarSystemChanged == 1 or Sequence − applied > 1 (a rebase was missed)) ? clear all rows : Position += Delta for every row; applied = Sequence. Idempotent.
        public static void ApplyPendingShift(ref NativeList<ProjectileRow> rows, in Game.Core.Components.OriginShiftRecord record, ref NativeReference<uint> applied);
    }

    public struct ProjectileStoreStats { public int Live, PeakLive, Fired, DroppedFull, Hits, Expired; }
}
```

### 4.6b `ShipDestroyed` (managed event, `Assets/Game/Core/Events/ShipDestroyed.cs`, namespace `Game.Core.Events`)

This spec owns the payload and its bridge (architecture §6.6; SPEC-02 §5.6 rules 1–3). A ship or station counts as destroyed when its core is lost (SPEC-13 `GridCoreLost`); the hulk that remains is not a second event.

```csharp
public readonly struct ShipDestroyed
{
    public readonly ulong PersistentId;               // the grid's PersistentId (0 for unsaveable test grids)
    public readonly Game.Core.ContentHash Blueprint;  // GridSource.Blueprint (0 for structure spawns such as the player's fleet ships)
    public readonly Game.Core.ContentHash Encounter;  // SPEC-33 EncounterTag.Encounter (0 = none; filled from M3, ENC-007)
    public readonly uint EncounterInstance;           // SPEC-33 EncounterTag.Instance (0 = none)
    public readonly uint Tick;                        // SimClock.Tick of the core loss
    public readonly byte IsPlayer;                    // 1 when the grid had PlayerShip
    public readonly byte Faction;                     // Allegiance.Faction (255 = none / no Allegiance)
    public readonly byte Kind;                        // (byte)HulkInfo.Kind: the GridKind before death (Ship = 0, Station = 1, Asteroid = 2)
    public ShipDestroyed(ulong persistentId, Game.Core.ContentHash blueprint, Game.Core.ContentHash encounter, uint encounterInstance,
                         uint tick, byte isPlayer, byte faction, byte kind);
}
```

`ShipDestroyedBridgeSystem` (`Assets/Game/Simulation/Weapons/ShipDestroyedBridgeSystem.cs`, `ISystem`, main thread, not Burst-compiled): `PresentationBridgeGroup`, `[UpdateBefore(typeof(ManagedEventDrainSystem))]`; requires `DamageStreams` only (not `GameplayActive`: a core loss in the last tick before a scene exit is still reported). Per frame:
1. `streams.DrainCoreLost(pending)` (`pending`: persistent `NativeList<GridCoreLost>`, cleared each frame). Empty → return.
2. `em.CompleteDependencyBeforeRO` for `PlayerShip`, `Allegiance`, `GridSource`, `HulkInfo` and (from M3) `EncounterTag`; then for each event in list order: if `em.Exists(Grid)`: `IsPlayer = HasComponent<PlayerShip>`, `Faction = Allegiance.Faction` (255 if absent), `Blueprint = GridSource.Blueprint`, `Kind = (byte)HulkInfo.Kind` (0 if absent), `Encounter`/`EncounterInstance` from `EncounterTag` when present; if it no longer exists, those fields are 0/255.
3. `em.CompleteDependencyBeforeRO<ManagedEventQueue<ShipDestroyed>>()`; enqueue `ManagedEventEnvelope<ShipDestroyed> { Tick = e.Tick, SortKey = e.PersistentId, Payload }` (main-thread producer, SPEC-02 §5.6). `ManagedEventDrainSystem` publishes it the same frame.

`Game.Simulation/AssemblyInfo.cs` registers `ManagedEventQueue<ShipDestroyed>`; `ManagedEventChannels.RegisterAll` gets `system.RegisterChannel<ShipDestroyed>();`. Consumers: SPEC-01 `DefeatWatcher` (`IsPlayer == 1`), SPEC-40 narrative bridge, SPEC-33 director (`EncounterDefeated`), SPEC-63/64 nothing (they react to `CellRemoved`/FX).

### 4.7 Tuning definition

`WeaponTuningDefinition : ContentDefinition` (`config_weapons`, prefix `config_`, `Assets/Game/Data/Definitions/Weapons/`): one serialized field per `WeaponTuning` field with the defaults in §4.2 (degrees in the asset, converted by the factory). Validation `WPN-E007`: `maxProjectiles < 256` or `maxCandidates < 4`.

### 4.8 Required from SPEC-23 (`SpatialHash`)

This spec relies on SPEC-23 §4.1 (`SpatialHash`, `SpatialProxy`, queries) and its access rules (§5.1 there: `hash.BeginRead(state.Dependency)` before scheduling a reading job, `hash.EndRead(handle)` after). The hash is rebuilt every tick by `SpatialHashBuildSystem` (`OrderFirst` in `GameplayPostPhysicsGroup`); pre-physics systems read the previous tick's hash. The members used:
- `SpatialProxy { Entity Entity; float3 Center; float Radius; float HullRadius; float3 Velocity; ProxyKind Kind; Allegiance Allegiance; ProxyFlags Flags; }` with `ProxyKind { Ship, Station, Asteroid, Hulk, Celestial }`. Grid proxies: `Center` = world centre of the cell bounds, `HullRadius` = half-diagonal of the cell bounds, `Radius = max(HullRadius, shield bounding distance)`. Celestial proxies: `Radius = HullRadius` = body radius.
- `int QuerySegment(float3 p0, float3 p1, float inflate, ref FixedList512Bytes<int> output)`: indices of proxies whose sphere (radius + inflate) intersects the segment, **ascending by `Entity.Index`**; returns the total match count (may exceed the list capacity).
- `int QuerySphere(float3 c, float r, ref FixedList512Bytes<int> output)`: same contract for a sphere.
- `SpatialProxy Get(int index)`.

### 4.9 v2 weapon values (initial; SPEC-72 MIG-009 writes exactly these, WPN-014 retunes)

`SustainedDps` = projectile/missile: `(Damage + ExplosionDamage × 0.5) × PelletsPerRound × BurstCount × FireRate`; beam: `Damage × FireRate` (`0.5` = average blast falloff weight). `Range` as in §4.2.

| ID | Delivery | Type | Dmg | Pen | Expl r m / dmg | Fuse | Rate /s | Burst / interval s | Pellets | Spread ° | Speed m/s | Life s | Homing °/s | Beam range m / pulse s | Arc ° / rate °/s | Elev ° | Draw E/s | Groups | Visual | SustainedDps |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `wpn_plasma_turret_small` | Projectile | Kinetic | 60 | 1 | 0 / 0 | no | 2 | 1 / 0 | 1 | 2.0 | 400 | 1.5 | 0 | — | 5 / 360 | 20 | 1.5 | 1 | StandardBullet | 120 |
| `wpn_plasma_turret_small_fixed` | Projectile | Kinetic | 60 | 1 | 0 / 0 | no | 2.5 | 1 / 0 | 1 | 0.5 | 400 | 1.5 | 0 | — | 0 / 0 | 20 | 1.5 | 1 | StandardBullet | 150 |
| `wpn_plasma_turret_medium` | Projectile | Kinetic | 70 | 2 | 0 / 0 | no | 1.5 | 3 / 0.05 | 1 | 0.5 | 450 | 1.6 | 0 | — | 90 / 180 | 20 | 4 | 1 | StandardBullet | 315 |
| `wpn_small_beam_turret` | Beam | Energy | 150 | 1 | 0 / 0 | no | 0.6 | 1 / 0 | 1 | 0 | — | — | 0 | 450 / 0.5 | 90 / 180 | 20 | 4 | 2 | LaserBeam | 90 |
| `wpn_fixed_beam_turret_small` | Beam | Energy | 180 | 1 | 0 / 0 | no | 0.6 | 1 / 0 | 1 | 0 | — | — | 0 | 450 / 0.5 | 0 / 0 | 20 | 4 | 2 | LaserBeam | 108 |
| `wpn_railgun_medium` | Projectile | Kinetic | 250 | 6 | 0 / 0 | no | 0.3 | 4 / 0.1 | 1 | 0.5 | 1200 | 1.0 | 0 | — | 30 / 90 | 20 | 8 | 1 | RailgunSlug | 300 |
| `wpn_ark_lance_medium` | Beam | Energy | 900 | 2 | 0 / 0 | no | 0.1 | 1 / 0 | 1 | 0 | — | — | 0 | 900 / 1.0 | 35 / 60 | 20 | 10 | 2 | LaserBeam | 90 |
| `wpn_flak_cannon_medium` | Projectile | Explosive | 0 | 1 | 2.5 / 60 | yes | 1 | 1 / 0 | 8 | 6.0 | 300 | 0.5–1.5 | 0 | — | 0 / 0 | 20 | 5 | 4 | FlakShell | 240 |
| `wpn_missile_launcher_small` | Missile | Explosive | 0 | 1 | 3.0 / 200 | yes | 0.3 | 6 / 0.05 | 1 | 2.0 | 180 | 5.0 | 90 | — | 0 / 0 | 20 | 2 | 4 | Missile | 180 |

Common to all: `projectileRadius` 0.1 m (flak 0.2, missile 0.3); `fireConeDeg` 3; `impulsePerHit` = `Dmg × 2` N·s; beam width 0.3 m (ark lance 0.6); `fireCue` from SPEC-72's legacy sound map; `muzzleFx`/`hitFx` null. Legacy-to-field rules (for traceability; the table above wins): accuracy `a` → `spreadDeg = (1 − a) × 10`; legacy speed/size × 0.5; `IsRotating == false` or `MaxRotationAngle == 0` → fixed.

## 5. Systems, services and flow

### 5.0 Placement and tick timeline

| System | Group, order | Burst | Requires |
|---|---|---|---|
| `WeaponInitSystem` | `GameInitGroup`, `[UpdateAfter(typeof(GridSpawnSystem))]` | yes | `GameplayActive`, `BlockDefsRef`, `OrientationTablesRef` |
| `PlayerTargetingSystem` | `ShipCommandGroup`, `[UpdateAfter(typeof(PlayerCommandApplySystem))]` | yes | `GameplayActive`, `SpatialHash`, `FactionRelations`, `WeaponTuning`, `SimClock` |
| `WeaponAimSystem` | `GameplayPrePhysicsGroup`, `[UpdateAfter(typeof(PlanarConstraintSystem))]` | yes | `GameplayActive`, `WeaponDefsRef`, `WeaponTuning`, `SpatialHash` |
| `WeaponFireSystem` | `GameplayPrePhysicsGroup`, `[UpdateAfter(typeof(WeaponAimSystem))]`, `[UpdateBefore(typeof(AbilityActivationGroup))]` | yes | `GameplayActive`, `WeaponDefsRef`, `WeaponTuning`, `ProjectileStore`, `SimClock` |
| `ProjectileStepSystem` | `GameplayPostPhysicsGroup`, `[UpdateAfter(typeof(ImpactEventSystem))]` | yes | `GameplayActive`, `DamageStreams`, `ShieldHitStreams`, `SpatialHash`, `WeaponDefsRef`, `SimClock` |
| `BeamStepSystem` | `GameplayPostPhysicsGroup`, `[UpdateAfter(typeof(ProjectileStepSystem))]`, `[UpdateBefore(typeof(DamageResolveSystem))]` | yes | same |
| `ExplosionSystem` | `GameplayPostPhysicsGroup`, `[UpdateAfter(typeof(BeamStepSystem))]`, `[UpdateBefore(typeof(DamageResolveSystem))]` | yes | `GameplayActive`, `DamageStreams`, `ShieldHitStreams`, `SpatialHash`, `WeaponTuning` |
| `WeaponVisualSyncSystem` | `WorldGroup` (variable rate, before transforms) | yes | `GameplayActive` |
| `ShipDestroyedBridgeSystem` | `PresentationBridgeGroup`, `[UpdateBefore(typeof(ManagedEventDrainSystem))]` | no (main thread) | `DamageStreams` (§4.6b) |

SPEC-22's `ShieldSystem` declares `[UpdateAfter(typeof(ExplosionSystem))]`. `ProjectileStepSystem.OnCreate` creates the `ProjectileStore` singleton (capacity `WeaponTuning.MaxProjectiles` if present, else 8,192); `OnDestroy` completes and disposes it. It implements `ISystemStartStop`: `OnStartRunning` and `OnStopRunning` clear the store (`CompleteAccess`, `Rows.Clear()`), so no projectile survives a scene change.

```mermaid
sequenceDiagram
    participant T as PlayerTargetingSystem
    participant A as WeaponAimSystem
    participant F as WeaponFireSystem
    participant PH as Physics
    participant P as ProjectileStepSystem
    participant B as BeamStepSystem
    participant X as ExplosionSystem
    participant S as ShieldSystem (SPEC-22)
    participant R as DamageResolveSystem (SPEC-13)
    T->>T: target edges → ShipTarget, autofire toggle
    A->>A: desired direction per weapon, turret yaw step
    F->>F: cooldowns, bursts → shots stream → append to ProjectileStore; beam pulses start
    PH->>PH: physics step
    P->>P: step rows: hash segment query → shield / cells / celestial → streams; compaction
    B->>B: active beams: one cast per beam → streams; BeamState.EndWorld
    X->>X: drain ExplosionRequests → blast ShieldHits / DamageEvents
    S->>R: ShieldHit resolution, overflow DamageEvents
```

### 5.1 `WeaponContent.BuildBlob` and boot

1. `weapons = catalog.All<WeaponDefinition>()`; allocate `Weapons` with `Count + 1`; entry 0 all zero.
2. Entry `i+1` from definition `i`: copy fields; `TriggerIntervalTicks = 60 / fireRate`; `BurstIntervalTicks = burstInterval × 60`; `LifetimeMin/MaxTicks = max(1, SimTime.SecondsToTicks(...))`; `BeamPulseTicks = max(1, SecondsToTicks(beamPulseSeconds))`; degrees → radians; `IsFixed = traverseHalfArcDeg == 0`; `Range` and `SustainedDps` per §4.2/§4.9; hashes = `def.Id.Hash` or 0 for null references; `TracerColor` packed from `tracerColor` (sRGB bytes).
3. `CreateSingletons`: one entity each for `WeaponDefsRef`, `WeaponTuning` (from `config_weapons`, `WeaponTuningDefinition`) and `FactionRelations.CreateDefault(Allocator.Persistent)` (unless one exists); append to `created` (SPEC-01 tags them `BootSingleton`). Disposal: SPEC-01's dispose stack disposes the blob and `FactionRelations` (the factory returns them through `created`; SPEC-01 owns the order).

### 5.2 `WeaponInitSystem`

`OnCreate` (`[CreateAfter(typeof(GridSpawnSystem))]`): register `WeaponState`, `WeaponMount`, `WeaponAim`, `BeamState` for `FunctionKind.Weapon` in `FunctionalComponentRegistration`.

Per frame: `WeaponMountInitJob : IJobEntity` (`ScheduleParallel`) over `FunctionalBlock`, `WeaponMount`, `WeaponState`, `BeamState`, **`WithChangeFilter<FunctionalBlock>`**: `inst = BlockInstance[Grid][InstanceIndex]`, `def = Blocks[inst.Block]`; not a weapon → skip. `w = WeaponDefs[def.Function.Weapon.WeaponIndex]`. `PivotLocal = InstanceCenter(...)`; `MuzzleLocal = BlockPointToGrid(def.Function.Weapon.MuzzleOffsetCells, ...)`; `ForwardLocal = BlockDirToGrid((0,0,1))`; `UpLocal = BlockDirToGrid((0,1,0))`; `WeaponIndex`; `Initialized = 1`. On first initialisation only (`Initialized` was 0): `WeaponState { Group = w.DefaultFireGroups, CooldownRemainingTicks = 0, CooldownTicks = w.TriggerIntervalTicks }`; `BeamState.Width = w.BeamWidth`, `Fx = w.HitFx`. Re-parented entities (SPEC-13 hulk splits) keep their `WeaponState`.

### 5.3 `PlayerTargetingSystem`

One `IJobEntity` (`Schedule`, single-threaded) over roots with `PlayerShip`, `ShipCommand`, `ShipTarget`, `WeaponControl`, `Allegiance`, `LocalTransform`, `WithNone<Hulk>`; reads `SpatialHash` (previous tick), `FactionRelations`, `EntityStorageInfoLookup`, `ComponentLookup<Hulk>`.
1. **Validity:** if `ShipTarget.Value` no longer exists → `Value = Null`.
2. **`AutofireToggle` edge:** `Autofire ^= 1`; `AimMode = Autofire ? TargetLead : AimPoint`.
3. **`TargetClear` edge:** `Value = Null`.
4. **`TargetNext` edge:** candidates = proxies with `Kind ∈ {Ship, Station}` and `FactionRelations.IsHostile(own Allegiance, proxy.Allegiance)`, within `MaxTargetRange` of the ship (hulks and celestial bodies are never candidates, §10 Q9). Sort by `(distance from the ship rounded to 0.01 m, Entity.Index)`; pick the entry after the current target in that order (wrap to the first); no candidates → `Value = Entity.Null`.
5. **`TargetUnderCursor` edge** (only if `ShipCommand.AimValid`): `QuerySphere(AimWorld, UnderCursorRadius)`; among grid proxies (any allegiance, not self), pick the one minimising `distance(AimWorld, proxy sphere surface)`, ties by `Entity.Index`.
6. On change: `SinceTick = Tick`.

### 5.4 `WeaponAimSystem`

`WeaponAimJob : IJobEntity` (`ScheduleParallel`) over weapon entities (`FunctionalBlock`, `WeaponMount`, `WeaponAim`, `WeaponState`). Lookups `[ReadOnly]`: root `LocalTransform`, `PhysicsVelocity`, `ShipCommand`, `WeaponControl`, `ShipTarget`, `Hulk`, `Dormant`, `GridSpawning`; target root `LocalTransform` and `PhysicsVelocity` (velocity of the target from the proxy is acceptable); `WeaponDefsRef`. Per weapon (`w = WeaponDefs[WeaponIndex]`; skip if `WeaponIndex == 0`, `Initialized == 0`, root missing, `Hulk`, `Dormant` enabled or `GridSpawning` enabled → `HasAim = 0`, keep `Yaw`):
1. `rootT` = root `LocalTransform` (pre-physics pose, traps T2.7). `muzzleWorld` = `rootT.TransformPoint(PivotLocal + rotate(yawQ, MuzzleLocal − PivotLocal))` with `yawQ = quaternion.AxisAngle(UpLocal, Yaw)`.
2. **Aim point:** `AimMode == TargetLead` and target exists → lead point `L` from §5.4.1 with `shotSpeed = w.ProjectileSpeed` (beams: target centre; missiles: target centre); `InRange = distance(muzzleWorld, L) ≤ w.Range`. Else `cmd.AimValid == 1` → `L = cmd.AimWorld`, `InRange = 1`. Else `HasAim = 0`, keep `Yaw`, return.
3. `d = normalize(rootT.InverseTransformDirection(L − muzzleWorld))` (grid-local). Decompose relative to the mount: `h = d − UpLocal·dot(d, UpLocal)`; `DesiredYaw = atan2(dot(cross(ForwardLocal, h), UpLocal), dot(ForwardLocal, h))`; `elev = asin(clamp(dot(d, UpLocal), −1, 1))` clamped to `±MaxElevation`.
4. **Turret** (`!IsFixed`): `target = clamp(DesiredYaw, −arc, arc)`; `Yaw += clamp(target − Yaw, −TraverseRate·dt, +TraverseRate·dt)`; `Aligned = abs(DesiredYaw − Yaw) ≤ FireCone`. **Fixed:** `Yaw = clamp(DesiredYaw, −FixedConvergence, +FixedConvergence)`; `Aligned = abs(DesiredYaw) ≤ FixedConvergence + FireCone` (player manual fire ignores `Aligned` for fixed weapons).
5. `FireDirLocal = rotate(quaternion.AxisAngle(UpLocal, Yaw), ForwardLocal) × cos(elev) + UpLocal × sin(elev)` (unit).
6. `HasAim = 1`.

#### 5.4.1 Lead point
Inputs: muzzle `m`, shooter velocity `vs` (root `PhysicsVelocity.Linear`), target centre `c` (proxy centre), target velocity `vt`, shot speed `s`. Relative velocity `vr = vt − vs` (shots inherit `vs`, decision 5). Solve `|c − m + vr·t| = s·t` for the smallest `t > 0`: `a = dot(vr,vr) − s²`, `b = 2·dot(c − m, vr)`, `k = dot(c − m, c − m)`; if `|a| < 1e-6` → `t = −k / b` if `b < 0` else no solution; else `disc = b² − 4ak`, `disc < 0` → no solution; `t` = smallest positive root. No solution or `t > lifetimeMax` → `L = c` (aim at the centre). Else `L = c + vr·t`.

### 5.5 `WeaponFireSystem`

Job 1 `WeaponFireJob : IJobChunk` (`ScheduleParallel`) over weapon entities; writes `WeaponState`, `BeamState` (own entity) and a `NativeStream` of `ShotRequest { float3 Position; float3 Velocity; Entity Source; Entity Target; uint ShotId; uint DeathTick; ushort Weapon; DamageType Type; ProjectileFlags Flags; float DamageMultiplier; }` at foreach index `unfilteredChunkIndex` (`forEachCount = query.CalculateChunkCountWithoutFiltering()`). Also enqueues muzzle `FxEvent`/`SfxEvent`. Per weapon (`w`, root data as in §5.4; `tick = SimClock.Tick`):
1. Skip (and set `Firing = 0`) if `WeaponIndex == 0`, root missing, `Hulk`, `Dormant`, `GridSpawning`, or `HasAim == 0`.
2. Cooldowns: `CooldownRemainingTicks = max(0, CooldownRemainingTicks − 1)`; `CooldownTicks = w.TriggerIntervalTicks / FireRateMultiplier` (`ShipStats.FireRateMultiplier`, 1 if absent); `if (RoundsLeftInBurst > 0) BurstRemainingTicks −= 1`.
3. **Wants to fire:** `held = (cmd.FireGroups & Group) != 0`; `auto = WeaponControl.Autofire == 1 && target exists && InRange && hostile(target)`; `wants = (held || auto) && (Aligned || (IsFixed && held))`; powered = entity has no `Powered` or it is enabled.
4. **Trigger:** if `wants && powered && RoundsLeftInBurst == 0 && CooldownRemainingTicks ≤ 0 && (beam ? BeamState.Active == 0 : true)` → `CooldownRemainingTicks += CooldownTicks`; beams: start pulse (`Active = 1`, `TicksLeft = BeamPulseTicks`), emit muzzle Fx/Sfx, done; others: `RoundsLeftInBurst = BurstCount`, `BurstRemainingTicks = 0`.
5. **Rounds:** while `RoundsLeftInBurst > 0 && BurstRemainingTicks ≤ 0 && powered`: release one round (step 6); `RoundsLeftInBurst−−`; `BurstRemainingTicks += BurstIntervalTicks` (0 → all rounds this tick).
6. **Release a round:** `ShotCounter++`; `shotId = ((uint)InstanceIndex & 0xFFFF) << 16 | (ShotCounter & 0xFFFF)`; `rng` per decision 8; for each pellet `k`: direction = `FireDirLocal` rotated by a random cone sample (`θ = SpreadRad × sqrt(rng.NextFloat())`, `φ = 2π × rng.NextFloat()`) → world via `rootT`; `pos = muzzleWorld`; `vel = dir × ProjectileSpeed + vs`; `death = tick + (LifetimeMin == LifetimeMax ? LifetimeMax : rng.NextUInt(LifetimeMin, LifetimeMax + 1))`; flags: `Homing` if `HomingTurnRate > 0`, `ExplodeOnExpire`, `PlayerSource` if the root has `PlayerShip`; `Target = ShipTarget.Value` for homing; write `ShotRequest` (pellets share `shotId`; their `Sequence` uniqueness is restored by `ShotId + k` in the low bits: `shotId + (uint)k`, with `ShotCounter += PelletsPerRound − 1`). Emit one muzzle `FxEvent { Muzzle, muzzleWorld, dir, vs, Scale = 0.25 + 0.05 × Damage^0.5 (m), Color = TracerColor, Fx = MuzzleFx }` and one `SfxEvent(FireCue)` per round (`PlayerSource` flag for the player).
7. `Firing = 1` if any round or pulse started this tick, else 0.

Job 2 `ShotAppendJob : IJob` (after job 1; `ProjectileStore.Acquire/Release`): first `ProjectileStore.ApplyPendingShift(...)` with the `OriginShiftRecord` read through a `ComponentLookup` on its entity (new rows are already in the new frame, so old rows must be shifted before appending); then read the stream indices `0..forEachCount−1` in order, append rows; if `Rows.Length == MaxProjectiles` → drop the shot, `DroppedFull++` (the determinism test asserts 0). `Fired++`.

### 5.6 `ProjectileStepSystem`

1. `streams` = `DamageStreams` (SPEC-13); `n = Rows.Length` is not known on the main thread, so the stream index count is a fixed upper bound: `batches = ceil(MaxProjectiles / 64)`. `dw = streams.BeginDamageWrite(ref state, batches)`, `ew = streams.BeginExplosionWrite(ref state, batches)`, `sw = ShieldHitStreams.BeginWrite(ref state, ShieldHitProducer.Projectiles, batches)` (§10 Q3).
2. Job `ProjectileShiftJob : IJob` (`ApplyPendingShift`; a no-op when `ShotAppendJob` already applied it this tick), then job `ProjectileStepJob : IJobParallelForBatch` over `Rows` (deferred length; batch size 64), dependencies: `state.Dependency`, `ProjectileStore.Acquire`, stream begin handles. Every batch calls `BeginForEachIndex(startIndex / 64)` on all three writers (empty index when the batch has no events), then for each row `r` (skip `Dead`):
   1. **Homing:** `Target` exists → `toT = normalize(targetCentre − Position)`; rotate `Velocity` toward `toT` by at most `HomingTurnRateRad × dt` (Rodrigues about `cross(v, toT)`), keep speed.
   2. `p0 = Position`, `p1 = p0 + Velocity × dt`.
   3. `hit = CombatHitTest.Segment(p0, p1, ProjectileRadius, Source, ...)` (§5.9).
   4. **Hull hit** on grid `G` at world point `h`, world dir `u = normalize(Velocity)`: if `Damage > 0`: write `DamageEvent { Grid = G, LocalPoint = WorldToGridPoint(h), LocalDir = WorldToGridDir(u), Amount = Damage × DamageMultiplier, Radius = 0, Penetration = PenetrationCells, Type, Source, WorldImpulse = u × ImpulsePerHit, Producer = Projectile, Sequence = ShotId, Flags = None }`. If `ExplosionRadius > 0`: write `ExplosionRequest { WorldPos = h, Radius, Amount = ExplosionDamage × DamageMultiplier, Type = Explosive, Source, Producer = Projectile, Sequence = ShotId }`. Hit `FxEvent` (`HitKinetic` for Kinetic/Explosive, `HitEnergy` for Energy) at `h`, `Dir = −u`, `Vel` = hit grid velocity, `Scale = clamp(Damage / 50, 0.5, 3)`, `Fx = HitFx`; hit `SfxEvent` from `CombatAudioCues` by type, volume `clamp(Damage / 200, 0.3, 1)`. Mark `Dead`, `Hits++`.
   5. **Shield hit** on grid `G` at local surface point `q` (grid space): write `ShieldHit { Grid = G, LocalPoint = q, Amount = Damage × DamageMultiplier, Type, Source, LocalDir = WorldToGridDir(u), Penetration = PenetrationCells, Radius = 0, WorldImpulse = u × ImpulsePerHit, Flags = None, ProducerKind = 1, Sequence = ShotId }`; explosive rounds additionally write the `ExplosionRequest` at the world surface point. No Fx here (SPEC-22 `ShieldSystem` emits ripples). Mark `Dead`.
   6. **Celestial hit:** hit Fx only; mark `Dead`.
   7. **No hit:** `Position = p1`; if `tick ≥ DeathTick`: `ExplodeOnExpire` → `ExplosionRequest` at `p1`; mark `Dead`, `Expired++`.
   8. `EndForEachIndex` on all writers per batch.
3. Job `ProjectileCompactJob : IJob`: stable compaction removing `Dead` rows; update `Live`, `PeakLive`.
4. `streams.EndDamageWrite(h)`, `EndExplosionWrite(h)`, `ShieldHitStreams.EndWrite(Projectiles, h)`; `ProjectileStore.Release(h)`; `state.Dependency = h`.

Explosion requests written here are drained this tick by `ExplosionSystem` (registered before the drain).

### 5.7 `BeamStepSystem`

`BeamStepJob : IJobChunk` (`ScheduleParallel`) over weapon entities with `BeamState`, `WeaponAim`, `WeaponMount`, `FunctionalBlock`; streams at foreach index `unfilteredChunkIndex`. Per entity with `Active == 1`:
1. Root missing, `Hulk` or not powered → `Active = 0`, `TicksLeft = 0`, continue.
2. `muzzleWorld`/`MuzzleLocal` as §5.4 step 1 (post-physics pose this time, so the beam starts where the ship is drawn); `dir = rootT.TransformDirection(FireDirLocal)`; `p1 = muzzleWorld + dir × BeamRange`.
3. `hit = CombatHitTest.Segment(muzzleWorld, p1, BeamWidth × 0.5, Source = root, ...)`.
4. `amount = Damage × DamageMultiplier / BeamPulseTicks`; hull hit → `DamageEvent` (`Penetration = PenetrationCells`, `Producer = Beam`, `Sequence = (uint)InstanceIndex`, `WorldImpulse = dir × ImpulsePerHit / BeamPulseTicks`); shield hit → `ShieldHit` (`ProducerKind = 2`, same sequence); `EndWorld = hit point`, `HitSomething = 1`; no hit → `EndWorld = p1`, `HitSomething = 0`. Hit Fx every 6th tick of the pulse (`TicksLeft % 6 == 0`) at the hit point.
5. `TicksLeft−−`; `0` → `Active = 0`.

### 5.8 `ExplosionSystem`

1. `list = NativeList<ExplosionRequest>(Allocator.TempJob)`; `h0 = streams.ScheduleDrainExplosions(list, state.Dependency)` (sorted by SPEC-13).
2. `ExplosionJob : IJob` after `h0` (single-threaded; requests are few), damage writer with `forEachCount = 1`, shield writer (`Explosions`) with `forEachCount = 1`. For each request `e` in order:
   1. `QuerySphere(e.WorldPos, e.Radius)` → grid proxies (skip `Celestial`), ascending `Entity.Index`.
   2. For each grid `G` (skip if `GridSpawning`, `EditorPreview` grids are never damaged): `c = WorldToGridPoint(e.WorldPos)`. Shield `Active` and `ShieldMath.SphereOverlaps(c, e.Radius, Center, Radii)` → `ShieldHit { Grid = G, LocalPoint = c, Amount, Type, Source, LocalDir = 0, Penetration = 0, Radius, WorldImpulse = impulse, Flags = Blast, ProducerKind = 3, Sequence }`. Else, if the sphere intersects the grid's cell bounds box (`CellMin·s .. (CellMax+1)·s`, grid space): `DamageEvent { Grid = G, LocalPoint = c, LocalDir = 0, Amount, Radius, Penetration = 0, Type, Source, WorldImpulse = impulse, Producer = Explosion, Sequence = e.Sequence }`. `impulse = normalize(gridCentreWorld − e.WorldPos) × Amount × ExplosionImpulsePerDamage × (1 − clamp(dist / Radius, 0, 1))²` (zero vector if the centre coincides).
   3. One `FxEvent { Radius < 4 ? ExplosionSmall : ExplosionLarge, e.WorldPos, (0,1,0), Vel = 0, Scale = Radius }` and one `SfxEvent` (`CombatAudioCues.ExplosionSmall/Large`).
3. Dispose `list` with the job handle; end writers; `state.Dependency = handle`.

### 5.9 `CombatHitTest` (narrow phase, Burst, used by projectiles and beams)

```csharp
namespace Game.Simulation.Weapons
{
    public enum HitKind : byte { None = 0, Hull = 1, Shield = 2, Celestial = 3 }
    public struct CombatHit { public HitKind Kind; public Entity Grid; public float Fraction; public float3 WorldPoint; public float3 ShieldLocalPoint; }

    public struct CombatHitTest   // job field; Create in OnCreate, Update first in OnUpdate
    {
        public static CombatHitTest Create(ref SystemState state);
        public void Update(ref SystemState state);
        /// First hit along p0→p1 (fraction 0..1), ignoring `ignore`. Deterministic: ties broken by lower Entity.Index.
        public CombatHit Segment(float3 p0, float3 p1, float inflate, Entity ignore, in SpatialHash hash, int maxCandidates);
    }
}
```
Algorithm:
1. `n = hash.QuerySegment(p0, p1, inflate, ref list)`; if `n > list.Capacity` → `GameLog.Burst.Warn` once and test only the listed ones (sorted by `Entity.Index`: deterministic).
2. `best = None, bestF = +∞`. For each proxy `i` (ascending `Entity.Index`), `e = proxy.Entity`, skip `e == ignore`:
   - **Celestial:** ray–sphere with `HullRadius` → `f`; if `f < bestF` → Celestial.
   - **Grid** (skip if `GridSpawning` enabled or `GridSource.Flags & EditorPreview`): `t = LocalTransform[e]` (current pose); `a = t.InverseTransformPoint(p0)`, `b = t.InverseTransformPoint(p1)` (grid-local metres).
     - Shield: if `ShieldState` exists, `Active == 1` and `ShieldMath.SegmentEnters(a, b, Center, Radii, out fs)` and `fs < bestF` → Shield at `fs` (`ShieldLocalPoint = a + (b − a) × fs`).
     - Hull (only if no shield hit on this grid at `fs ≤` the hull fraction): `s = VoxelGrid.CellSize`; `VoxelRaycast.Cast(ref GridCellQuery{Reader, e}, a / s, (b − a) / s, maxCells = length(b − a) / s + 1, out vh)`; if hit and `f = vh.DistanceCells × s / length(b − a)` is `≤ 1` and `< bestF` → Hull at `f`.
3. `WorldPoint = p0 + (p1 − p0) × bestF`.

Complexity: O(candidates × (shield test + DDA over ≤ segment cells with empty-brick skipping)).

### 5.10 `WeaponVisualSyncSystem`

`IJobEntity` (`ScheduleParallel`) over weapon entities with `WeaponAim`, `WeaponMount`, `LocalTransform`, `FunctionalBlock`, `WithChangeFilter<WeaponAim>`: skip fixed weapons. `rest` = the entity's spawn `LocalTransform` (position `(Origin + 0.5)·s`, rotation `R(orient)`, scale `s`, SPEC-10 §4.5); `yawQ = quaternion.AxisAngle(UpLocal, Yaw)`; `LocalTransform.Position = PivotLocal + rotate(yawQ, restPosition − PivotLocal)`, `Rotation = mul(yawQ, R(orient))`, `Scale = s`. This is the only writer of a weapon entity's `LocalTransform` after spawn; nothing in the simulation reads it (traps T2.7).

### 5.11 Determinism notes

- Shot order in the store = weapon chunk order (stream index order), deterministic for a given entity layout; event order downstream is re-sorted by SPEC-13/22 keys, so it does not matter.
- Random draws depend on `(tick, shooter PersistentId, ShotId)` only.
- No Unity Physics query is used by any system in this spec.

## 6. Public API

| Call / type | Pre-conditions | Post-conditions / errors |
|---|---|---|
| `CombatGridSetup.AddAll(em, root, allegiance)` | Main thread, outside system updates; `root` is a spawn request entity | Adds `Allegiance`, `WeaponControl`, `ShipTarget` and the SPEC-22/24 setup components; calling twice throws `InvalidOperationException` |
| `WeaponContent.CreateSingletons` | Boot (SPEC-01) | `WeaponDefsRef`, `WeaponTuning`, default `FactionRelations` |
| `ProjectileStore.ApplyPendingShift` | Inside this spec's single-threaded jobs only | Rows follow the floating origin (Core `OriginShiftRecord`, SPEC-23 Q4); star-system change clears the store |
| `ProjectileStore` presentation read (SPEC-63) | Main thread outside the fixed step after `CompleteAccess()`, or a job chained with `Acquire`/`Release` | Rows are the state at the end of the last tick |
| `FactionRelations.IsHostile` | — | Same faction never hostile; 255 never hostile |
| `CombatHitTest.Segment` | Created in `OnCreate`, `Update` called this frame; `SpatialHash` of this tick | Deterministic first hit |
| Damage producers outside this spec (SPEC-24) | Follow §5.6 step 2 stream rules | — |

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| Weapon block without a linked `WeaponDefinition` (`WeaponIndex 0`) | Never aims or fires; DATA-E047 prevents this in content |
| Target destroyed while missiles fly | Missiles continue straight; target cleared from `ShipTarget` next tick |
| Shooter destroyed while its projectiles fly | They continue; `Source` stays the dead entity (attribution only) |
| Projectile starts inside another grid (muzzle inside a hull it is docked against) | DDA reports distance 0, hit at the muzzle |
| Store full (`MaxProjectiles`) | Shot dropped, `DroppedFull++`, `GameLog.Burst.Warn` once |
| More than `MaxCandidates` proxies along a segment | Warn once; test the first `MaxCandidates` by `Entity.Index` |
| Beam pulse when the weapon loses power mid-pulse | Pulse stops immediately (`Active = 0`) |
| Explosion with radius > SPEC-13 `MaxBlastRadiusCells × CellSize` | Passed through; SPEC-13 clamps with a warning; WPN-E004 forbids such content |
| Hulk grid | Its weapons never aim or fire; it is still hit by everything |
| Player has no target and autofire on | Weapons do not fire automatically; manual fire still works |
| `AimValid == 0` | Weapons keep their current yaw and do not start new manual fire (SPEC-03 edge case) |
| Scene change | Store cleared by `OnStopRunning`/`OnStartRunning`; beams inactive (weapon entities are destroyed) |
| Star-system change during a rebase | `ApplyPendingShift` clears the store on the next tick (`StarSystemChanged == 1`); SPEC-63 hides tracers while `AppliedShiftSequence != FrameOrigin.ShiftSequence` |
| Content errors | WPN-E001 `fireRate ≤ 0`; E002 projectile/missile with `projectileSpeed ≤ 0`; E003 beam with `beamRange ≤ 0`; E004 `explosionRadius > 6 m`; E005 `lifetimeMin > lifetimeMax`; E006 `damage == 0 && explosionDamage == 0`; E007 tuning (§4.7); E008 `damageType == Ram` |

## 8. Performance budget

| Gate / budget | Target | Test (task) |
|---|---|---|
| 5,000 live projectiles (06 combat gate) | `ProjectileStepSystem` ≤ 1 ms wall per tick median including stream writes and compaction, with 30 grids in the hash | `ProjectilePerfTests.FiveThousand_Under1ms` (WPN-013) |
| `WeaponFireSystem` + `WeaponAimSystem`, 30 ships × 20 weapons | ≤ 0.25 ms wall per tick | `WeaponPerfTests.ThirtyShips_AimFire` (WPN-013) |
| `BeamStepSystem`, 100 active beams | ≤ 0.1 ms wall | WPN-013 |
| `ExplosionSystem`, 64 requests per tick | ≤ 0.1 ms | WPN-013 |
| Main thread per system | ≤ 0.03 ms (scheduling only) | WPN-013 |
| GC | 0 B per tick | WPN-013 |

**Scale notes:**
- Projectile step: O(P × (hash cells along a segment + candidates)). Segments are short (≤ 20 m at 1,200 m/s), so typically 1–2 hash cells and 0 candidates; cost is dominated by the row read/write (64 B). Expected ≈ 0.1–0.2 µs per projectile on a worker → 5,000 ≈ 0.75 ms worker time, ≈ 0.15 ms wall on 6+ workers.
- Memory: 64 B × 8,192 = 512 KB store; stream memory per tick bounded by hits.
- Weapons: O(W) per tick, all lookups grouped per root; 600 weapons ≈ 60 µs of worker time.
- Beams: one long segment per active beam per tick; the hash walk is O(length / hash cell).
- Rejected approaches: projectile entities (structural churn, ECB load); Unity Physics raycasts for hits (collider lag, nondeterminism, extra cost); per-shot power drain; `NativeQueue` for any damage-producing event.

## 9. Tasks

### WPN-001 — Public contract types
- **Milestone:** M1
- **Tier:** standard — transcribes the exact code of §4 (bodies throw `NotImplementedException`, except `Allegiance`, `FactionRelations.Get/IsHostile/CreateDefault/Dispose`, `ProjectileStore` one-liners)
- **Depends on:** `CORE-001`, `DATA-001`, `VOX-001`, `DMG-001`, `FLT-001`
- **Do:** Create every type of §4.1–§4.6 and §5.9's public types in the folders of decision 15. `WeaponDefinition` references SPEC-63's `FxDefinition` and SPEC-64's `AudioCueDefinition`; if they do not exist yet, create them as empty `ContentDefinition` subclasses with a `// TODO(VFX-001)` / `// TODO(AUD-001)` note (their tasks fill them).
- **Files:** `Assets/Game/Data/Definitions/Weapons/{WeaponDelivery,ProjectileVisual,WeaponFireGroups,WeaponDefinition,WeaponTuningDefinition}.cs`; `Assets/Game/Simulation/{Allegiance,FactionStance,FactionRelations,CombatGridSetup}.cs`; `Assets/Game/Simulation/Weapons/{WeaponDef,WeaponDefsBlob,WeaponDefsRef,WeaponTuning,WeaponContent,WeaponMount,WeaponAim,WeaponState,BeamState,WeaponAimMode,WeaponControl,ShipTarget,ProjectileFlags,ProjectileRow,ProjectileStore,ProjectileStoreStats,HitKind,CombatHit,CombatHitTest}.cs`
- **Acceptance criteria:**
  - [ ] Compiles; components unmanaged; `ProjectileRow` is 64 bytes (`UnsafeUtility.SizeOf`).
  - [ ] `FactionRelations.CreateDefault`: 0↔1 hostile, 2 neutral, diagonal friendly.
- **Tests:** `WeaponContractTests.ProjectileRow_Is64Bytes`, `.FactionDefaults`, `.Components_AreUnmanaged`.

### WPN-002 — Weapon content: definitions, blob, boot singletons
- **Milestone:** M2
- **Tier:** standard — field-by-field bake in §5.1 and validation codes in §7
- **Depends on:** `WPN-001`, `SPEC-04`
- **Do:** Implement `WeaponDefinition`/`WeaponTuningDefinition` validation, `WeaponContent.BuildBlob`, `ComputeSustainedDps`, `CreateSingletons`; create `config_weapons.asset`. Ledger note to SPEC-01 for its BootSingletons row.
- **Files:** `Assets/Game/Data/Definitions/Weapons/*.cs`, `Assets/Game/Simulation/Weapons/WeaponContent.cs`, `Assets/Game/Content/Config/config_weapons.asset`, `Assets/Game/Tests/EditMode/Simulation/Weapons/WeaponContentTests.cs`
- **Acceptance criteria:**
  - [ ] Bake of the §4.9 table gives the listed `SustainedDps` (±0.5) and `Range` values; entry 0 is all zero.
  - [ ] `WeaponDefinition.DamageType` is `Game.Data.Combat.DamageType`; a weapon with `Ram` reports WPN-E008.
  - [ ] Every validation code is reported with asset path and field.
- **Tests:** `WeaponContentTests.Bake_Table49_Values`, `.Entry0_IsNone`, `.Validation_Codes`, `.RamDamageType_Rejected`.

### WPN-003 — CombatHitTest
- **Milestone:** M2
- **Tier:** frontier — F3: a wrong narrow phase silently damages the wrong cells or misses shields
- **Depends on:** `WPN-001`, `SPEC-10`, `AI-001`, `PWR-001`
- **Do:** Implement §5.9 against a test `SpatialHash` (SPEC-23 contract; a test double that returns all proxies sorted by index is acceptable until SPEC-23's implementation lands).
- **Files:** `Assets/Game/Simulation/Weapons/CombatHitTest.cs`, `Assets/Game/Tests/EditMode/Simulation/Weapons/CombatHitTestTests.cs`
- **Acceptance criteria:**
  - [ ] Against `Ship20k` rotated by random yaws: a segment through a known solid cell reports that cell's entry point (±0.01 m); through a hole it reaches the next solid cell; the shooter grid is ignored.
  - [ ] Active shield in front of the hull → Shield hit at the ellipsoid; inactive → hull.
  - [ ] Two grids along the segment → the nearer; equal fraction → lower `Entity.Index`.
- **Tests:** `CombatHitTestTests.HitsExactCell_RandomYaw`, `.ThroughHole_NextCell`, `.IgnoresShooter`, `.ShieldBeforeHull`, `.NearestGridWins`, `.TieBreakByIndex`, `.Celestial`.

### WPN-004 — WeaponInitSystem and mounts
- **Milestone:** M2
- **Tier:** standard — formulas in §5.2
- **Depends on:** `WPN-002`, `SPEC-10`, `SPEC-20`
- **Do:** Implement §5.2.
- **Files:** `Assets/Game/Simulation/Weapons/{WeaponInitSystem,WeaponMountInitJob}.cs`, `Assets/Game/Tests/EditMode/Simulation/Weapons/WeaponInitTests.cs`
- **Acceptance criteria:**
  - [ ] Every weapon entity of a spawned `ship_fighter` has an initialised mount; muzzle points match `GridSpaceMath` for the block's orientation; `WeaponState.Group` = default groups.
  - [ ] Re-parented weapon (hulk split test double) keeps `WeaponState`.
- **Tests:** `WeaponInitTests.Mounts_Initialized`, `.Muzzle_MatchesOrientation`, `.Reparent_KeepsState`.

### WPN-005 — PlayerTargetingSystem
- **Milestone:** M2
- **Tier:** standard — selection rules in §5.3
- **Depends on:** `WPN-001`, `AI-001`, `SPEC-20`
- **Do:** Implement §5.3.
- **Files:** `Assets/Game/Simulation/Weapons/PlayerTargetingSystem.cs`, `Assets/Game/Tests/EditMode/Simulation/Weapons/PlayerTargetingTests.cs`
- **Acceptance criteria:**
  - [ ] `TargetNext` cycles hostile ships by distance, wraps, ignores neutrals and allies; `TargetUnderCursor` picks the grid nearest the aim point; `TargetClear` clears; `AutofireToggle` toggles; dead targets clear.
- **Tests:** `PlayerTargetingTests.Next_CyclesByDistance`, `.Next_IgnoresNonHostile`, `.UnderCursor_Nearest`, `.Clear`, `.Autofire_Toggles`, `.DeadTarget_Cleared`.

### WPN-006 — WeaponAimSystem and lead
- **Milestone:** M2
- **Tier:** frontier — F3: aim and lead errors make every weapon miss in subtle ways
- **Depends on:** `WPN-004`, `WPN-005`
- **Do:** Implement §5.4 and §5.4.1.
- **Files:** `Assets/Game/Simulation/Weapons/{WeaponAimSystem,WeaponAimJob,LeadMath}.cs`, `Assets/Game/Tests/EditMode/Simulation/Weapons/WeaponAimTests.cs`
- **Acceptance criteria:**
  - [ ] Turret yaw never exceeds the arc and never moves faster than the traverse rate; fixed weapons converge by at most 3°.
  - [ ] Lead: a projectile fired with the computed lead at a constant-velocity target hits within 0.1 m (analytic test).
  - [ ] Dormant, hulk and spawning ships: `HasAim = 0`.
- **Tests:** `WeaponAimTests.Turret_ArcAndRate`, `.Fixed_Convergence`, `.Lead_HitsMovingTarget`, `.Elevation_Clamped`, `.SkipsDormantHulkSpawning`.

### WPN-007 — WeaponFireSystem and the projectile store append
- **Milestone:** M2
- **Tier:** frontier — F2/F3: parallel stream writes, fractional cooldowns, deterministic shot ids
- **Depends on:** `WPN-006`, `PWR-001`
- **Do:** Implement §5.5 (both jobs), store creation/clearing in `ProjectileStepSystem.OnCreate`/`ISystemStartStop` (create the system class here with an empty `OnUpdate` if WPN-008 is not done).
- **Files:** `Assets/Game/Simulation/Weapons/{WeaponFireSystem,WeaponFireJob,ShotAppendJob,ShotRequest}.cs`, `Assets/Game/Tests/EditMode/Simulation/Weapons/WeaponFireTests.cs`
- **Acceptance criteria:**
  - [ ] A weapon at 2.5/s fires exactly 150 triggers in 3,600 ticks; a 3-round burst at 0.05 s spaces rounds 3 ticks apart.
  - [ ] Unpowered weapon never fires; hulk never fires; beam pulse lasts exactly `BeamPulseTicks`.
  - [ ] Store rows appended in stream order; `ShotId` unique per pellet; full store drops and counts.
  - [ ] Same scripted inputs at 1, 2 and 6 ticks per frame → identical store contents per tick.
- **Tests:** `WeaponFireTests.Rate_ExactOverAMinute`, `.Burst_Spacing`, `.Unpowered_NoFire`, `.Beam_PulseLength`, `.ShotIds_Unique`, `.StoreFull_Drops`, `.Deterministic_FramePatterns`.

### WPN-008 — ProjectileStepSystem
- **Milestone:** M2
- **Tier:** frontier — F2/F4: parallel NativeStream producers into three consumers; owns the 5,000-projectile gate
- **Depends on:** `WPN-003`, `WPN-007`, `PWR-001`, `SPEC-13`
- **Do:** Implement §5.6 (homing, hits, fuses, compaction). Uses `ShieldHitStreams.BeginWrite/EndWrite` (SPEC-22 §4, answered Q3).
- **Files:** `Assets/Game/Simulation/Weapons/{ProjectileStepSystem,ProjectileStepJob,ProjectileCompactJob}.cs`, `Assets/Game/Tests/EditMode/Simulation/Weapons/ProjectileStepTests.cs`
- **Acceptance criteria:**
  - [ ] Hull hit writes one `DamageEvent` with the §5.6 fields; shield hit writes one `ShieldHit`; explosive rounds also write an `ExplosionRequest`; fuse expiry explodes at `p1`.
  - [ ] Homing missile turns toward a moving target at ≤ its turn rate.
  - [ ] Compaction keeps fire order; `Live` matches.
  - [ ] Stream content is identical with worker count 1 and max.
- **Tests:** `ProjectileStepTests.HullHit_Event`, `.ShieldHit_Event`, `.Explosive_Request`, `.Fuse_Explodes`, `.Homing_TurnRate`, `.Compaction_StableOrder`, `.Streams_WorkerCountIndependent`.

### WPN-009 — BeamStepSystem
- **Milestone:** M2
- **Tier:** standard — algorithm in §5.7 on top of `CombatHitTest`
- **Depends on:** `WPN-008`
- **Do:** Implement §5.7.
- **Files:** `Assets/Game/Simulation/Weapons/{BeamStepSystem,BeamStepJob}.cs`, `Assets/Game/Tests/EditMode/Simulation/Weapons/BeamStepTests.cs`
- **Acceptance criteria:**
  - [ ] Per pulse, total `Amount` written = `Damage` (± 0.1%); `EndWorld` is the hit point, never beyond the target (R26).
- **Tests:** `BeamStepTests.PulseDamage_Sums`, `.EndWorld_AtHit`, `.PowerLoss_StopsPulse`.

### WPN-010 — ExplosionSystem
- **Milestone:** M2
- **Tier:** frontier — F5: single consumer of SPEC-13's explosion streams, shields first, ordering with SPEC-22
- **Depends on:** `WPN-008`
- **Do:** Implement §5.8.
- **Files:** `Assets/Game/Simulation/Weapons/{ExplosionSystem,ExplosionJob}.cs`, `Assets/Game/Tests/EditMode/Simulation/Weapons/ExplosionTests.cs`
- **Acceptance criteria:**
  - [ ] A blast overlapping two grids yields one event per grid (shield hit for the shielded one, damage event for the other); requests from the same tick's projectiles are processed this tick; reactor requests of tick N in tick N+1.
- **Tests:** `ExplosionTests.ShieldFirst`, `.TwoGrids_TwoEvents`, `.SameTickProjectileRequests`, `.Impulse_Falloff`.

### WPN-011 — CombatGridSetup and turret visuals
- **Milestone:** M2
- **Tier:** standard — call sequence and formula given (§4.3, §5.10)
- **Depends on:** `WPN-004`, `PWR-001`, `ABL-001`
- **Do:** Implement `CombatGridSetup.AddAll` and `WeaponVisualSyncSystem` (§5.10).
- **Files:** `Assets/Game/Simulation/CombatGridSetup.cs`, `Assets/Game/Presentation/Weapons/WeaponVisualSyncSystem.cs`, `Assets/Game/Tests/EditMode/Simulation/Weapons/CombatGridSetupTests.cs`
- **Acceptance criteria:**
  - [ ] `AddAll` adds exactly the listed components; a turret's entity rotates about its pivot by `Yaw`; fixed weapons never move.
- **Tests:** `CombatGridSetupTests.AddsComponents`, `.Twice_Throws`, `WeaponVisualTests.Turret_RotatesAboutPivot`.

### WPN-012 — Combat scenarios and determinism
- **Milestone:** M2
- **Tier:** frontier — F5: the full fire → hit → damage pipeline across SPEC-13/21/22 at different frame rates
- **Depends on:** `WPN-009`, `WPN-010`, `WPN-011`, `SPEC-13`, `SPEC-22`
- **Do:** PlayMode scenarios: (1) player plasma on a static hulk: holes appear exactly along the shot line; (2) shielded target absorbs, then overflows to hull; (3) flak burst damages two adjacent small ships; (4) homing missiles follow a strafing target; (5) beam does not pass through a target (R26); (6) 4-ship skirmish for 600 ticks at 30/60/144 simulated FPS and worker counts 1/max → identical `CellStateHasher` hash and `ProjectileStoreStats` (`DroppedFull == 0`).
- **Files:** `Assets/Game/Tests/PlayMode/Simulation/Weapons/{CombatScenarioTests,CombatDeterminismTests}.cs`
- **Acceptance criteria:**
  - [ ] Each scenario as stated; the skirmish hash identical across all runs.
- **Tests:** `CombatScenarioTests.Plasma_HolesAlongLine`, `.Shield_AbsorbThenOverflow`, `.Flak_TwoShips`, `.Missiles_Track`, `.Beam_StopsAtTarget`; `CombatDeterminismTests.Skirmish_IdenticalHash`.

### WPN-013 — Weapon performance gates
- **Milestone:** M2
- **Tier:** frontier — F4: owns the 5,000-projectile gate
- **Depends on:** `WPN-012`, `TST-001`
- **Do:** Performance tests of §8 with reference settings; record results in the ledger *Measurements*.
- **Files:** `Assets/Game/Tests/PlayMode/Simulation/Weapons/{ProjectilePerfTests,WeaponPerfTests}.cs`
- **Acceptance criteria:**
  - [ ] Every §8 target met or an *Open questions* row with numbers and a proposal.
- **Tests:** `ProjectilePerfTests.FiveThousand_Under1ms`, `WeaponPerfTests.ThirtyShips_AimFire`, `.Beams100`, `.Explosions64`, `.ZeroGc`.

### WPN-014 — Weapon balance pass
- **Milestone:** M2
- **Tier:** frontier — F6: tuning values against SPEC-22 rules R1–R3 and an owner sign-off
- **Depends on:** `WPN-012`, `SPEC-72`
- **Do:** For each SPEC-22 reference ship, measure in a test arena: hull time-to-kill and shield break time under the same-class reference loadout at 60% hit rate; adjust §4.9 values so R3 holds (shield breaks in 15–50% of hull TTK) and record the before/after table in the ledger; request owner sign-off on feel.
- **Files:** `Assets/Game/Tests/PlayMode/Simulation/Weapons/WeaponBalanceReport.cs`, `docs/specs/SPEC-21-weapons.md` (§4.9 values), `Assets/Game/Content/Weapons/*.asset`
- **Acceptance criteria:**
  - [ ] R3 holds for every reference ship; table recorded; owner sign-off requested in *Open questions*.
- **Tests:** `WeaponBalanceReport.R3_HoldsForReferenceShips`.

### WPN-015 — Harness hooks
- **Milestone:** M2
- **Tier:** standard — read-only accessors for SPEC-70
- **Depends on:** `WPN-008`
- **Do:** `WeaponDebug.Snapshot(EntityManager, Entity root, List<WeaponDebugRow>)` (weapon index, group, yaw, cooldown, firing, beam active) and `ProjectileStore` stats accessor for `gh_perf`.
- **Files:** `Assets/Game/Simulation/Weapons/WeaponDebug.cs`, tests
- **Acceptance criteria:**
  - [ ] Snapshot lists every weapon of the player ship with current values.
- **Tests:** `WeaponDebugTests.Snapshot_ListsWeapons`.

### WPN-017 — ShipDestroyed payload and bridge
- **Milestone:** M2
- **Tier:** standard — payload and per-frame steps fully given in §4.6b
- **Depends on:** `WPN-001`, `DMG-001`, `CORE-001`
- **Do:** Create `ShipDestroyed` (§4.6b) and `ShipDestroyedBridgeSystem`; register the queue and the channel (SPEC-02 §5.6 rule 3). Change SPEC-13's `DrainCoreLost` comment to name this system as its caller. The `EncounterTag` read is added by SPEC-33 ENC-007 (step 2 of its Do).
- **Files:** `Assets/Game/Core/Events/ShipDestroyed.cs`; `Assets/Game/Simulation/Weapons/ShipDestroyedBridgeSystem.cs`; `Assets/Game/Simulation/AssemblyInfo.cs`; `Assets/Game/Presentation/Systems/ManagedEventChannels.cs` (one line); `Assets/Game/Tests/EditMode/Simulation/Weapons/ShipDestroyedBridgeTests.cs`
- **Acceptance criteria:**
  - [ ] A core loss on an AI ship with `Allegiance.Faction = 3` spawned from blueprint B publishes exactly one `ShipDestroyed { Blueprint = B, Faction = 3, IsPlayer = 0, Kind = 0 }` on the `EventBus` the same frame.
  - [ ] A core loss on the player ship gives `IsPlayer = 1`.
  - [ ] Two core losses in one tick are published in ascending `PersistentId` order.
  - [ ] A grid destroyed before the bridge runs still yields an event with its `PersistentId` and `Faction = 255`.
- **Tests:** `ShipDestroyedBridgeTests.AiShip_FieldsFilled`, `.Player_IsPlayer`, `.SameTick_SortedById`, `.DestroyedGrid_StillReported`.

### WPN-016 — Per-instance fire groups (M4)
- **Milestone:** M4
- **Tier:** frontier — F1: adds per-instance data to the structure format and saves
- **Depends on:** `WPN-011`
- **Do:** Store a fire-group byte per weapon instance in the structure (SPEC-10 `BlockInstance.Reserved` or a new instance side table, through SPEC-10/SPEC-05 owners), edit it in SPEC-51, and have `WeaponInitSystem` read it instead of the default.
- **Files:** to be listed by the SPEC-10/SPEC-05 owners when scheduled
- **Acceptance criteria:**
  - [ ] Fire groups chosen in the editor survive save/load and drive firing.
- **Tests:** `FireGroupTests.EditorAssignment_RoundTrips`.

## 10. Open questions

| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | `WeaponDefinition` (Game.Data) cannot reference SPEC-13's `DamageType` (Game.Voxel). Keep the mirror enum `WeaponDamageType` with equal values (WPN-002 tests equality), or move `DamageType` down to Game.Data through an ADR? | **Answered (2026-10-10, owner, extensibility review X9):** `DamageType` moved to `Game.Data.Combat` (architecture §3.2); the mirror enum and its test are deleted | Owner |
| 2 | Friendly fire on (decision 4)? | Yes | Owner |
| 3 | SPEC-22 `ShieldHitStreams`: producers access it with `GetSingletonRW` and write fields on the main thread, which completes the previous producer's jobs (sync points). Change to a registration API like SPEC-13 `DamageStreams`: `BeginWrite(ref state, ShieldHitProducer, forEachCount) → NativeStream.Writer`, `EndWrite(producer, JobHandle)`; consumers combine stored handles | **Applied** in SPEC-22 §4 (`ShieldHitProducer`, `BeginWrite/EndWrite/BeginRead/EndRead`) | SPEC-22 |
| 4 | SPEC-63 tracers read `ProjectileStore` rows (§6) instead of projectile entities | **Applied** in SPEC-63 §5.3 (2026-10-09). Yes | SPEC-63 |
| 5 | The store follows the floating origin through the Core `OriginShiftRecord` (SPEC-23 Q4), which SPEC-02/SPEC-30 must add | **Applied** in SPEC-02 §4.3, SPEC-30 §5.10 (2026-10-09). Yes | SPEC-02, SPEC-30 |
| 6 | SPEC-03 aim: ray from the camera through the cursor against `PhysicsLayers.PickingQuery` (excluding the player's grid) first, plane `Y = 0` as fallback, so the 3D aim point is on what the cursor covers | **Applied** in SPEC-03 §5.2 (2026-10-09). Yes | SPEC-03 |
| 7 | SPEC-63/64 producer pattern: take `FxEventQueue`/`SfxEventQueue` with `GetSingleton` (read-only) and `[NativeDisableContainerSafetyRestriction]` on the `ParallelWriter`, instead of `GetSingletonRW` in fixed-step systems | **Applied** in SPEC-02 §5.6, SPEC-63 §6.2, SPEC-64 §6.2 (2026-10-09). Yes | SPEC-63, SPEC-64 |
| 8 | SPEC-72 MIG-009 writes the §4.9 values (not the unit-converted legacy values) | **Applied** in SPEC-72 §4.6, MIG-009 (2026-10-09). Yes | SPEC-72 |
| 9 | Should hulks be targetable with `TargetNext`? | No (only hostile ships and stations); `TargetUnderCursor` can pick anything | Owner |

## 11. References

- Audit (background only): `docs/reference/audit-2026-10/02-combat-destruction.md` C22 (double energy drain), R26 (beam through targets), 01-B2 (wrong-cell hits).
- SPEC-72 §4.6 legacy weapon tables. SPEC-22 §4.9 energy balance.
- Unity: `IJobParallelForBatch`, `NativeStream`, `ISystemStartStop`.
