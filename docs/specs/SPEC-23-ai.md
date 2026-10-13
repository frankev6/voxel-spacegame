# SPEC-23 — AI, spatial hash and fleets

| | |
|---|---|
| **Status** | Draft |
| **Area code** | AI (task IDs are `AI-001`, `AI-002`, …) |
| **Module(s)** | `Game.Simulation` (folders `Assets/Game/Simulation/Spatial/`, `Assets/Game/Simulation/Ai/`); `Game.Data` (`Assets/Game/Data/Definitions/Ai/`) |
| **Depends on specs** | SPEC-02 (`SimClock`, `StableHash`, `RngPurpose.AiDecision`, `Planar`, groups, `OriginShiftRecord` per §10 Q4); SPEC-04 (`ContentDefinition`, `ContentCatalog`); SPEC-10 (`VoxelGrid`, `GridSource`, `GridSpawning`); SPEC-11 (`Dormant`); SPEC-13 (`Hulk`, `GridDamageStats`); SPEC-20 (`ShipCommand`, `PlayerShip`, `FlightState`, `FlightMath`, `PlayerCommandApplySystem`); SPEC-21 (`Allegiance`, `FactionRelations`, `ShipTarget`, `WeaponControl`, `PlayerTargetingSystem`); SPEC-22 (`ShipStats`, `ShieldState`) |
| **Depended on by** | SPEC-21 (`SpatialHash` for hits, explosions, targeting, lead); SPEC-01 (`AiSetup`, `AiConfig` boot singletons, `PlayerCombatStatus` for the save blocker); SPEC-13 (`SpatialHashBuildSystem` ordering); SPEC-31 (`SpatialBody` on celestial bodies; station turrets); SPEC-33 (spawns AI ships with profiles and anchors; fleets in M4); SPEC-61 (`PlayerCombatStatus` for the HUD combat indicator); SPEC-70 (spatial hash overlay, AI state readout) |
| **Milestone** | M2 (AI-001 in M1; fleets AI-013..AI-015 in M4) |

> Read [01-architecture.md](../01-architecture.md) (§5, §8, §11.9), [02-conventions.md](../02-conventions.md), [07-implementation-traps.md](../07-implementation-traps.md) §2, [SPEC-20](SPEC-20-flight-propulsion.md) §6 (AI writers) and [SPEC-21](SPEC-21-weapons.md) §3, §4.8 before this spec. This spec may not contradict them.

## 1. Purpose

This spec gives the game a cheap, deterministic picture of "what is where" and the ships that act on it. `SpatialHashBuildSystem` builds, once per tick, a list of proxies (every grid root and celestial body with a bounding sphere, velocity, allegiance and kind) bucketed in a 2D hash on the flight plane; weapons, explosions, targeting, the radar and the AI all query it instead of scanning entities. AI ships think on a staggered schedule (each ship every N ticks, bucketed by `PersistentId`): choose a target, pick a state (patrol, engage, flee, return), and set a steering goal. Every tick a light steering job turns the goal into the same `ShipCommand` the player produces, so AI ships fly with real thrusters and the same flight assist. Fleets with formations follow in M4. The old prototype had no shared broad phase (each system scanned all ships) and AI that set velocities directly; here everything goes through `ShipCommand` and the hash.

## 2. Scope

**In scope:**
- `SpatialHash` (proxies, 2D bucket table, segment/sphere queries, read/write handle discipline), `SpatialHashBuildSystem`, `SpatialBody` (celestial proxies registered by SPEC-31).
- `AiProfileDefinition` (+ blob), `AiConfig`, `AiBrain`, `AiSetup`, `AiDecisionSystem`, `AiSteeringSystem`, `AiAnchorShiftSystem`, `PlayerCombatStatus` + `PlayerCombatStatusSystem`.
- Station turret AI (target selection and autofire without steering).
- M4: fleets and formations (`FleetMember`, `FormationDefinition`, leader/member steering).
- Debug readouts for SPEC-70; AI scenarios, determinism and the 06 AI gate.

**Out of scope (and who owns it instead):**
- Turning `ShipCommand` into thrust: SPEC-20. Aiming, lead and firing: SPEC-21 (AI only sets `ShipTarget` and `WeaponControl`).
- Which ships spawn where and with which profile: SPEC-33 (encounters) and SPEC-31 (station garrisons). Faction relations content: SPEC-33.
- Ability effects: SPEC-24. This spec chooses when AI ships press abilities (profile ability rules, §5.4 step 8).
- Navigation around large obstacles with path finding: not in v1 (local avoidance only, §5.6).

## 3. Decisions (binding)

1. **One spatial hash per tick, built first in `GameplayPostPhysicsGroup`** from post-physics poses. Pre-physics readers (AI, targeting, aim) use the previous tick's hash; post-physics readers (projectiles, beams, explosions) use this tick's. Reason: architecture §5; one O(P log P) build serves every consumer.
2. **Proxies are sorted by `Entity.Index`; every query returns proxy indices in ascending order** (so ascending `Entity.Index`). Reason: deterministic consumers without their own sort (SPEC-21 §4.8).
3. **The hash is 2D on the XZ plane** with square cells of `CellSize` (128 m); proxies with radius above `LargeProxyRadius` (4 × `CellSize`) are kept in a separate list that every query scans. Exact tests are 3D. Reason: planar game; planets would otherwise fill thousands of cells.
4. **AI writes only these:** `ShipCommand` of non-player, non-hulk ships with `AiBrain`; `ShipTarget` and `WeaponControl` of those ships; `AiBrain`; `PlayerCombatStatus`. Reason: single writers (architecture §8; SPEC-20 §4.6; SPEC-21 §4.5).
5. **Thinking is staggered**: a ship thinks on ticks where `(Tick + (uint)PersistentId) % ThinkIntervalTicks == 0` (`ThinkIntervalTicks` from its profile, × `DormantThinkMultiplier` while `Dormant`). Steering runs every tick for every AI ship. Reason: architecture §11.9; the 1.5 ms gate.
6. **AI flies like the player**: steering produces `ShipCommand.Thrust/Turn/Flags/AimWorld`; it never touches physics. Weapons fire through SPEC-21 autofire (`WeaponControl.Autofire = 1`, `AimMode = TargetLead`), so AI writes `FireGroups = 0`. `Edges` carries only the ability edges chosen by the profile's ability rules on think ticks (§5.4 step 8), else 0, so AI ships use abilities exactly like the player (review X5). Reason: one flight and weapon model (SPEC-20 decision 1).
7. **Randomness** uses `Random.CreateFromIndex((uint)StableHash.Combine(StableHash.Combine(tick, persistentId), (ulong)RngPurpose.AiDecision))`. Reason: conventions §2.10; stable across runs and saves.
8. **AI state is not saved.** On load, AI ships start in `Idle` with their anchor at the loaded position; SPEC-33 re-applies encounter context. Reason: combat cannot be saved (SPEC-01 `InCombat`), so mid-fight AI state never needs to survive a load.
9. **Anchors are local-frame points kept consistent with the floating origin** by `AiAnchorShiftSystem`, which applies the Core `OriginShiftRecord` (§10 Q4) to every `AiBrain` the first tick after a shift. Reason: Simulation cannot read SPEC-30's `FrameOrigin`.
10. **Folders and namespaces:** `Assets/Game/Simulation/Spatial/` → `Game.Simulation.Spatial`; `Assets/Game/Simulation/Ai/` → `Game.Simulation.Ai` (jobs in `Ai/Jobs/`); definitions in `Assets/Game/Data/Definitions/Ai/` → `Game.Data.Definitions`.
11. **Handle discipline:** `SpatialHash` uses one write handle and an accumulated readers handle (§5.1); it is accessed with `SystemAPI.GetSingleton` (read-only) everywhere. Reason: SPEC-12 decision 17 (no `GetSingletonRW` sync points in the fixed step).

## 4. Data model

All types are **public contract** unless marked *internal*.

### 4.1 Spatial types (`Game.Simulation.Spatial`)

```csharp
namespace Game.Simulation.Spatial
{
    using Unity.Collections; using Unity.Entities; using Unity.Jobs; using Unity.Mathematics;

    public enum ProxyKind : byte { Ship = 0, Station = 1, Asteroid = 2, Hulk = 3, Celestial = 4 }   // add at end only

    [System.Flags] public enum ProxyFlags : byte { None = 0, PlayerShip = 1, Dormant = 2, Shielded = 4, HasAi = 8 }

    /// <summary>One broad-phase object (64 bytes). Implements SPEC-23 §4.1.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 64)]
    public struct SpatialProxy
    {
        public Entity Entity;            // grid root or celestial body entity
        public float3 Center;            // local frame: grids = world centre of the cell bounds; bodies = LocalTransform.Position
        public float Radius;             // max(HullRadius, shield reach) — what queries test
        public float HullRadius;         // grids: half diagonal of the cell bounds; bodies: body radius
        public float3 Velocity;          // PhysicsVelocity.Linear or 0
        public ProxyKind Kind;
        public ProxyFlags Flags;
        public Allegiance Allegiance;    // Game.Simulation.Allegiance; None for bodies and grids without one
        public uint Reserved;
    }

    /// <summary>On celestial bodies that weapons and AI must see (SPEC-31 adds it). Centre = root LocalTransform.Position.</summary>
    public struct SpatialBody : IComponentData { public float Radius; }

    public struct SpatialHashState
    {
        public float CellSize;           // 128 m
        public float LargeProxyRadius;   // 4 × CellSize
        public uint BuiltTick;           // SimClock.Tick of the last build
        public int ProxyCount, LargeCount, EntryCount, CellCount;
    }

    /// <summary>Singleton owned by SpatialHashBuildSystem (created in OnCreate). Access rules: §5.1. Implements SPEC-23 §4.1.</summary>
    public struct SpatialHash : IComponentData
    {
        public NativeList<SpatialProxy> Proxies;         // sorted by Entity.Index
        public NativeList<int> Large;                    // proxy indices with Radius > LargeProxyRadius, ascending
        public NativeHashMap<long, int2> Cells;          // cell key → (start, count) in EntryIndices (lookup only, never iterated)
        public NativeList<int> EntryIndices;             // proxy indices grouped by cell, ascending within a cell
        public NativeReference<SpatialHashState> State;
        public Game.Data.Streams.GuardedHandles Access;  // main thread only (SPEC-02 §4.12): builder = Acquire/Release, readers = BeginRead/EndRead

        public static SpatialHash Create(float cellSize);   // Allocator.Persistent; capacities: proxies 512, entries 4,096
        public void Dispose();                               // Access.Dispose() (completes) first

        public static long CellKey(int2 cell) => ((long)cell.x << 32) ^ (uint)cell.y;
        public int2 CellOf(float3 p) => (int2)math.floor(p.xz / State.Value.CellSize);  // main-thread convenience; jobs pass CellSize

        // Job access (§5.1)
        public JobHandle BeginRead(JobHandle dependsOn) => Access.BeginRead(dependsOn);
        public void EndRead(JobHandle readerJob) => Access.EndRead(readerJob);
        public void CompleteAccess() => Access.CompleteAccess();   // never inside FixedStepSimulationSystemGroup

        // Queries (Burst, usable inside jobs; read-only)
        public int QuerySegment(float3 p0, float3 p1, float inflate, ref FixedList512Bytes<int> output);
        public int QuerySphere(float3 center, float radius, ref FixedList512Bytes<int> output);
        public SpatialProxy Get(int index) => Proxies[index];
        public SpatialHashQueryData AsReadOnly(); // ADR-0064: non-owning job view; excludes GuardedHandles
        public int FindIndex(Entity e);                  // binary search on Entity.Index (and Version); -1 if absent
    }
}
```

`QuerySegment`/`QuerySphere` contract: output cleared first; contains the indices of proxies whose sphere (`Radius + inflate` for segments) intersects the segment / sphere, ascending, without duplicates; the return value is the total number of matches (it may exceed `output.Capacity` = 127; extra matches are dropped from the list but counted).

### 4.2 AI content (`Game.Data.Definitions`)

```csharp
namespace Game.Data.Definitions
{
    public enum AiFacing : byte { FaceTarget = 0, Broadside = 1 }
    public enum AiRole : byte { Combat = 0, Turret = 1, Civilian = 2 }   // Turret: never steers; Civilian: never engages, flees from hostiles

    /// <summary>When an AI ship presses an ability (review X5). Add values at the end only.</summary>
    public enum AiAbilityTrigger : byte { EngageStart = 0, Fleeing = 1, HullBelow = 2, ShieldBelow = 3, ShieldOff = 4, HasTarget = 5 }

    [System.Serializable] public struct AiAbilityRule
    {
        public AbilityDefinition ability;            // pressed when the ship has a slot (0..3) with this ability that is Ready and not Blocked
        public AiAbilityTrigger trigger;
        [UnityEngine.Range(0, 1)] public float threshold; // HullBelow: MassFraction; ShieldBelow: Hp/MaxHp; else unused
    }

    [ContentIdPrefix("ai_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/AI Profile", fileName = "ai_new")]
    public sealed class AiProfileDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private AiRole role;
        [UnityEngine.SerializeField, UnityEngine.Range(1, 120)] private int thinkIntervalTicks = 10;
        [UnityEngine.SerializeField, UnityEngine.Min(100)] private float sensorRange = 1500;          // m: target search radius
        [UnityEngine.SerializeField, UnityEngine.Range(0.2f, 1.2f)] private float preferredRangeFraction = 0.7f; // of WeaponStats.MaxWeaponRange (SPEC-21)
        [UnityEngine.SerializeField, UnityEngine.Min(10)] private float minEngageRange = 80;           // m
        [UnityEngine.SerializeField] private AiFacing facing = AiFacing.FaceTarget;
        [UnityEngine.SerializeField, UnityEngine.Range(0, 1)] private float throttle = 0.85f;         // max fraction of the speed caps
        [UnityEngine.SerializeField, UnityEngine.Range(0, 1)] private float fleeHullFraction = 0.25f; // ShipStats.MassFraction below which it flees; 0 = never
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float leashRadius = 3000;            // m from the anchor; 0 = no leash
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float patrolRadius = 600;            // m around the anchor
        [UnityEngine.SerializeField, UnityEngine.Range(0, 2)] private float playerBias = 0.3f;        // score bonus for targeting the player
        [UnityEngine.SerializeField, UnityEngine.Range(0, 2)] private float stickiness = 0.25f;       // score bonus for the current target
        [UnityEngine.SerializeField, UnityEngine.Range(0, 60)] private int orbitFlipThinks = 12;      // thinks between orbit-direction flips (0 = never)
        [UnityEngine.SerializeField, UnityEngine.Range(0, 1.5f)] private float orbitStepRad = 0.35f;  // angle advanced around the target per think
        [UnityEngine.SerializeField] private bool avoidFriendlyFire = true;
        [UnityEngine.SerializeField] private AiAbilityRule[] abilityRules = System.Array.Empty<AiAbilityRule>();   // ≤ 8, checked in order (review X5)
        // Public getters with the same names in PascalCase; AbilityRules is IReadOnlyList<AiAbilityRule>.
        public override void Validate(ValidationScope scope, ContentValidationContext context);       // AI-E001..E003 (§7)
    }

    [ContentIdPrefix("config_")]
    public sealed class AiConfigDefinition : ContentDefinition      // single instance: config_ai
    {
        [UnityEngine.SerializeField, UnityEngine.Min(32)] private float hashCellSize = 128;
        [UnityEngine.SerializeField, UnityEngine.Range(1, 20)] private int dormantThinkMultiplier = 6;
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float combatBubbleRadius = 2000;    // m: PlayerCombatStatus counts engaging hostiles within this
        [UnityEngine.SerializeField, UnityEngine.Min(1)] private float arrivalRadius = 40;           // m
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float avoidanceMargin = 25;         // m added to radii for local avoidance
        [UnityEngine.SerializeField] private AiProfileDefinition defaultProfile;                     // used when AiSetup gets ContentId.None
        // Public getters.
        public override void Validate(ValidationScope scope, ContentValidationContext context); // AI-E002, implemented by AI-003
    }
}
```

Default content (AI-003 creates them): `ai_default` (Combat, values as above), `ai_turret` (Turret, thinkInterval 6, sensorRange 1,200), `ai_skittish` (Combat, fleeHullFraction 0.5, playerBias 0, throttle 1), `ai_civilian` (Civilian, fleeHullFraction 1). Ability rules (review X5): `ai_default` `[ability_shield_toggle on ShieldOff, ability_speed_boost on Fleeing]`; `ai_turret` `[ability_shield_toggle on ShieldOff]`; `ai_skittish` and `ai_civilian` `[ability_speed_boost on Fleeing]`.

### 4.3 AI ECS types (`Game.Simulation.Ai`)

```csharp
namespace Game.Simulation.Ai
{
    using Unity.Entities; using Unity.Mathematics;

    /// <summary>Baked profile (angles radians, times ticks).</summary>
    public struct AiProfile
    {
        public Game.Data.Definitions.AiRole Role;
        public Game.Data.Definitions.AiFacing Facing;
        public byte AvoidFriendlyFire;
        public byte Reserved;
        public int ThinkIntervalTicks;
        public float SensorRange, PreferredRangeFraction, MinEngageRange, Throttle, FleeHullFraction;
        public float LeashRadius, PatrolRadius, PlayerBias, Stickiness, OrbitStepRad;
        public int OrbitFlipThinks;
        public Game.Core.ContentHash IdHash;
        public BlobArray<AiAbilityRuleDef> AbilityRules;   // baked from abilityRules (ability → AbilityDefsBlob index)
    }
    public struct AiAbilityRuleDef { public ushort AbilityIndex; public Game.Data.Definitions.AiAbilityTrigger Trigger; public float Threshold; }
    public struct AiProfilesBlob
    {
        public BlobArray<AiProfile> Profiles;       // [0] = default profile copy; [i] = IndexOf<AiProfileDefinition>
        public BlobArray<FormationDef> Formations; // ADR-0065: [0] empty; [i] = IndexOf<FormationDefinition>
    }

    /// <summary>Boot singleton (SPEC-01 BootSingletons) from config_ai and all AiProfileDefinitions.</summary>
    public struct AiConfig : IComponentData
    {
        public BlobAssetReference<AiProfilesBlob> Profiles;
        public float HashCellSize, CombatBubbleRadius, ArrivalRadius, AvoidanceMargin;
        public int DormantThinkMultiplier;
        public ushort DefaultProfileIndex;
    }

    public enum AiState : byte { Idle = 0, Patrol = 1, Engage = 2, Flee = 3, Return = 4, Hold = 5 }

    /// <summary>Per-ship AI state on the root. Only writers: AiDecisionSystem (all fields except Anchor shifts) and AiAnchorShiftSystem (Anchor, GoalPosition).</summary>
    public struct AiBrain : IComponentData
    {
        public AiState State;
        public sbyte OrbitSign;          // +1 or −1
        public byte FireBlocked;         // 1 = an ally is in the line of fire (autofire suspended)
        public byte Initialized;         // 0 = zeroed by profile conversion; AiAnchorShiftSystem completes it (§5.3 step 0)
        public byte AbilityEdges;        // bits 0..3 = Ability1..4 chosen on the last think (§5.4 step 8)
        public AiState PreviousState;    // state before the last think (EngageStart trigger)
        public ushort Reserved2;
        public uint AbilityEdgesTick;    // tick the edges belong to; steering writes them only on that tick
        public ushort ProfileIndex;
        public ushort ThinkCount;
        public float3 Anchor;            // local frame
        public float3 GoalPosition;      // local frame
        public float GoalHeadingYaw;     // radians (Planar convention)
        public float GoalSpeedFraction;  // 0..1 of the speed caps
        public uint LastThinkTick;
        public uint TargetSinceTick;
        public uint ShiftSequence;       // last OriginShiftRecord.Sequence applied
    }

    /// <summary>Singleton read by SPEC-01 (save blocker) and SPEC-61 (HUD). Only writer: PlayerCombatStatusSystem.</summary>
    public struct PlayerCombatStatus : IComponentData
    {
        public ushort HostilesEngaging;  // AI ships in Engage with ShipTarget == player ship, within CombatBubbleRadius
        public ushort Reserved;
        public uint LastDamagedTick;     // GridDamageStats.LastDamageTick of the player ship (0 = never)
    }

    /// <summary>Root composition for AI (ADR-0015). AiBrain is registered for GridProfile NpcShip, Station and SmallCraft; a requester that
    /// wants a specific profile adds `AiBrain { ProfileIndex }` before conversion (0 = the default profile copy). AiAnchorShiftSystem
    /// completes the brain on its first update (§5.3 step 0).</summary>
    public static class AiSetup
    {
        public static void Register(DynamicBuffer<Game.Voxel.Model.RootComponentRegistration> registrations);   // called by AiAnchorShiftSystem.OnCreate
        /// Managed: IndexOf<AiProfileDefinition>(profile); ContentId.None or unknown → 0 (default; unknown also logs GameLog.Warn once).
        public static ushort ProfileIndexOf(Game.Data.Services.ContentCatalog catalog, Game.Core.ContentId profile);
    }

    public static class AiContent
    {
        public static void CreateSingletons(EntityManager em, Game.Data.Services.ContentCatalog catalog, System.Collections.Generic.List<Entity> created);
    }
}
```

### 4.4 Fleets and wings (M3, `Game.Simulation.Ai`; ADR-0017)

```csharp
namespace Game.Simulation.Ai
{
    /// <summary>Immutable formation row in AiProfilesBlob (ADR-0065). BootSingletons owns the existing Profiles blob.</summary>
    public struct FormationDef
    {
        public Game.Core.ContentHash IdHash;
        public Unity.Entities.BlobArray<Unity.Mathematics.float2> Slots; // authored leader-local metres: x right, y forward
    }

    /// <summary>On group members (not on the leader): encounter fleets (SPEC-33) and carrier wings alike (ADR-0017). Writer: the spawner, FleetSystem on leader loss.</summary>
    public struct FleetMember : Unity.Entities.IComponentData
    {
        public Unity.Entities.Entity Leader;
        public byte Slot;                // index into the formation's slot list
        public ushort FormationIndex;    // FormationDefinition index
    }
}
namespace Game.Data.Definitions
{
    [ContentIdPrefix("formation_")]
    public sealed class FormationDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private UnityEngine.Vector2[] slots;   // leader-local XZ offsets in metres (x right, y forward)
        public System.Collections.Generic.IReadOnlyList<UnityEngine.Vector2> Slots => slots;
        public override void Validate(ValidationScope scope, ContentValidationContext context); // AI-E004 (AI-013, Q15)
    }
}
```

### 4.5 Memory

Per AI ship: `AiBrain` ≈ 72 B. Hash: 64 B per proxy + ≈ 4 entries per proxy × 4 B + map ≈ 20 B per cell; 1,000 proxies ≈ 100 KB.

## 5. Systems, services and flow

### 5.0 Placement

| System | Group, order | Burst | Requires |
|---|---|---|---|
| `SpatialHashBuildSystem` | `[UpdateInGroup(typeof(GameplayPostPhysicsGroup), OrderFirst = true)]`, no ordering attributes (it runs before every normal member, including SPEC-13's `ImpactEventSystem`; SPEC-02 forbids cross-bucket attributes) | yes | `GameplayActive` |
| `AiAnchorShiftSystem` | `ShipCommandGroup`, `[UpdateAfter(typeof(PlayerTargetingSystem))]` | yes | `GameplayActive`, `OriginShiftRecord` |
| `AiDecisionSystem` | `ShipCommandGroup`, `[UpdateAfter(typeof(AiAnchorShiftSystem))]` | yes | `GameplayActive`, `AiConfig`, `SpatialHash`, `FactionRelations`, `SimClock` |
| `AiSteeringSystem` | `ShipCommandGroup`, `[UpdateAfter(typeof(AiDecisionSystem))]` | yes | `GameplayActive`, `AiConfig`, `FlightConfig` |
| `PlayerCombatStatusSystem` | `ShipCommandGroup`, `[UpdateAfter(typeof(AiSteeringSystem))]` | yes | `GameplayActive`, `AiConfig` |

`SpatialHashBuildSystem.OnCreate` creates the `SpatialHash` singleton (cell size 128; `AiConfig.HashCellSize` replaces it on first update if different) and `PlayerCombatStatus` is created by `PlayerCombatStatusSystem.OnCreate`. Both dispose/destroy in `OnDestroy`.

### 5.1 `SpatialHash` access rules

- **Builder** (`SpatialHashBuildSystem` only): `dep = hash.Access.Acquire(state.Dependency)`; schedule the build chain; `hash.Access.Release(h); state.Dependency = h`.
- **Job readers** (any fixed-step system): `var hash = SystemAPI.GetSingleton<SpatialHash>(); var dep = hash.BeginRead(state.Dependency);` schedule jobs with `hash.AsReadOnly()` (`SpatialHashQueryData`) as a `[ReadOnly]` field; keep the owning hash and its `GuardedHandles` on the main thread (ADR-0064); `hash.EndRead(handle); state.Dependency = handle;`.
- **Main-thread readers** outside the fixed step (HUD radar, harness): `hash.CompleteAccess()` first.
- Never `GetSingletonRW<SpatialHash>`; never write proxy data outside the builder.

### 5.2 `SpatialHashBuildSystem`

Per tick (markers `SpatialHashBuildSystem.Gather`, `.Sort`, `.Bucket`):
1. **Gather** (`IJobChunk`, `ScheduleParallel` into a `NativeStream`, foreach index = `unfilteredChunkIndex`) over grid roots (`VoxelGrid`, `LocalTransform`; optional `PhysicsVelocity`, `ShieldState`, `Allegiance`, `Hulk`, `Dormant`, `PlayerShip`, `AiBrain`, `GridSpawning`) — skip roots with `GridSpawning` enabled — and over bodies (`SpatialBody`, `LocalTransform`):
   - grid: `s = CellSize`; `cMin = CellMin × s`, `cMax = (CellMax + 1) × s` (grid-local); `localCentre = (cMin + cMax) / 2`; `Center = LocalTransform.TransformPoint(localCentre)`; `HullRadius = length(cMax − cMin) / 2`; shield reach = `ShieldState.Active == 1 || MaxHp > 0 ? length(ShieldState.Center − localCentre) + ShieldState.BoundingRadius : 0`; `Radius = max(HullRadius, reach)`; `Kind` from `VoxelGrid.Kind` (`Hulk` tag → `Hulk`); `Flags` from the optional components (`Shielded` when `ShieldState.Active == 1`); `Velocity` = `PhysicsVelocity.Linear` or 0; `Allegiance` or `Allegiance.None`.
   - body: `Center = LocalTransform.Position`, `HullRadius = Radius = SpatialBody.Radius`, `Kind = Celestial`.
2. **Sort** (`IJob`): read the stream into `Proxies`; sort by `(Entity.Index, Entity.Version)`.
3. **Bucket** (`IJob`): `Large` = indices with `Radius > LargeProxyRadius`. For the others, for each cell `(x, z)` overlapped by the XZ square `Center.xz ± Radius`, emit `(CellKey, proxyIndex)` into a temp list; sort by `(key, proxyIndex)`; fill `EntryIndices` in that order and `Cells[key] = (start, count)`. Update `State` (`BuiltTick = SimClock.Tick`, counts).
Complexity: O(P log P + E log E), E = entries (≈ 1–16 per proxy).

**Query algorithms** (both: `output.Clear()`, collect candidates into a `FixedList4096Bytes<int>` scratch, then sort + dedupe into `output`):
- `QuerySphere(c, r)`: every cell overlapped by the XZ square `c.xz ± r`; each cell's entries plus all `Large`; keep proxy `i` if the 3D `distance(c, Center_i) ≤ r + Radius_i`. (A 1,500 m sensor query covers ≤ 25 × 25 cells; the cell lookups dominate its cost, §8.)
- `QuerySegment(p0, p1, inflate)`: cells overlapped by the XZ AABB of the segment expanded by `inflate` (beams of 900 m cover ≤ 64 cells with the default size); entries plus `Large`; keep proxy `i` if the 3D distance from `Center_i` to the segment ≤ `Radius_i + inflate`.
- Scratch overflow (more raw candidates than `FixedList4096Bytes<int>.Capacity`, currently 1,023): fall back to scanning all proxies linearly (correct, slower). Queries remain read-only; shared overflow telemetry is deferred (Q7). Queries spanning more than 4,096 hash cells or outside representable cell coordinates also use the same complete scan (Q8).

### 5.3 `AiAnchorShiftSystem`

`IJobEntity` (single-threaded) over `AiBrain` + `LocalTransform`, reading the Core singleton `OriginShiftRecord` (§10 Q4) through a `ComponentLookup` on its entity.
0. **First update** (`Initialized == 0`): `State = Idle`, `Anchor = GoalPosition = LocalTransform.Position`, `GoalHeadingYaw` = current yaw, `OrbitSign = +1`, `ShiftSequence = record.Sequence`, `Initialized = 1` (the requester's `ProfileIndex` is kept); continue with the next brain.
For each other brain with `ShiftSequence != record.Sequence`:
1. If `record.StarSystemChanged == 1` or `record.Sequence − ShiftSequence > 1` (a rebase was missed because two rebases happened before any fixed tick) → `Anchor = GoalPosition = LocalTransform.Position` (old anchors are meaningless or unrecoverable).
2. Else `Anchor += record.Delta`, `GoalPosition += record.Delta`.
3. `ShiftSequence = record.Sequence`.
Brains created after a rebase start with `ShiftSequence = record.Sequence` (step 0).

### 5.4 `AiDecisionSystem`

`AiThinkJob : IJobEntity` (`ScheduleParallel`) over roots with `AiBrain`, `ShipTarget`, `WeaponControl`, `Allegiance`, `LocalTransform`, `PersistentId`, `WithNone<PlayerShip, Hulk>`; reads the hash (`BeginRead/EndRead`), `FactionRelations`, `ShipStats`, `WeaponStats` (optional), `Dormant`, `GridSpawning`, `PhysicsVelocity`. Writes only its own entity's `AiBrain`, `ShipTarget`, `WeaponControl`.
1. Skip if `GridSpawning` enabled. `interval = profile.ThinkIntervalTicks × (Dormant ? DormantThinkMultiplier : 1)`; skip unless `(Tick + (uint)PersistentId.Value) % interval == 0`. `ThinkCount++`, `LastThinkTick = Tick`; `rng` per decision 7.
2. **Target validity:** current `ShipTarget.Value` must exist in the hash (`FindIndex ≥ 0`), be `Kind ∈ {Ship, Station}` and hostile; else clear.
3. **Target choice** (Role `Civilian`: never; otherwise every think): `QuerySphere(own centre, SensorRange)`; candidates = hostile proxies of kind Ship/Station other than self. `score = d / max(1, range) − (PlayerShip flag ? PlayerBias : 0) − (index == current ? Stickiness : 0)` with `d` = centre distance and `range = WeaponStats.MaxWeaponRange` (or `SensorRange` without stats); lowest score wins, ties by lower proxy index. Write `ShipTarget` (and `SinceTick` on change).
4. **State** (`hull = ShipStats.MassFraction` or 1; `toAnchor = |pos − Anchor|`):
   - Role `Turret` → `Hold`.
   - `FleeHullFraction > 0 && hull < FleeHullFraction` and a hostile exists within `SensorRange` → `Flee`.
   - Role `Civilian` and a hostile within `SensorRange` → `Flee`.
   - Target set → `Engage`, unless `LeashRadius > 0 && toAnchor > 1.5 × LeashRadius` → `Return` (and clear the target).
   - No target: `LeashRadius > 0 && toAnchor > LeashRadius` → `Return`; else `Patrol` if `PatrolRadius > 0`, else `Idle`.
   - `Return` ends when `toAnchor < ArrivalRadius × 2`.
5. **Goal** (`range = max(MinEngageRange, WeaponStats.MaxWeaponRange × PreferredRangeFraction)`; positions on the plane, `y = 0`):
   - `Engage`: `toSelf = normalize(xz(pos − targetCentre))` (zero → +X); every `OrbitFlipThinks` thinks flip `OrbitSign` with probability 0.5; `dir = rotateY(toSelf, OrbitSign × OrbitStepRad)`; `GoalPosition = targetCentre + dir × range`; `GoalHeadingYaw = FaceTarget ? yaw toward the target : yaw of (target direction rotated by OrbitSign × 90°)`; `GoalSpeedFraction = Throttle`.
   - `Flee`: away from the nearest hostile: `GoalPosition = pos + normalize(xz(pos − threat)) × 1500`; heading along it; speed 1.
   - `Return`: `GoalPosition = Anchor`; heading toward it; speed `Throttle`.
   - `Patrol`: when within `ArrivalRadius` of the goal or on the first think: `GoalPosition = Anchor + rotateY((rng.NextFloat() × PatrolRadius, 0, 0), rng.NextFloat(0, 2π))`; heading toward it; speed `0.5 × Throttle`.
   - `Idle`/`Hold`: `GoalPosition = pos`, heading = current, speed 0.
6. **Local avoidance:** `QuerySegment(pos, GoalPosition, ownHullRadius)`; the first proxy (by index) that is not self and not the target, whose hull (`HullRadius + ownHullRadius + AvoidanceMargin`) the segment passes through → shift `GoalPosition` sideways (perpendicular in XZ, toward the side of the segment the proxy centre is not on) by that clearance.
8. **Abilities** (review X5): `AbilityEdges = 0`, `AbilityEdgesTick = Tick`, `PreviousState` = the state before step 4. If the root has an `AbilitySlot` buffer (SPEC-24, read-only, last tick's values): for each rule of the profile in order, find slot `k < 4` with `AbilityIndex == rule.AbilityIndex`, `State == Ready`, `Blocked == 0`; if found and the trigger holds, `AbilityEdges |= 1 << k`. Triggers: `EngageStart` = state became `Engage` this think; `Fleeing` = state `Flee`; `HullBelow` = `ShipStats.MassFraction < Threshold`; `ShieldBelow` = `ShieldState.MaxHp > 0 && Hp < Threshold × MaxHp`; `ShieldOff` = `ShieldState.MaxHp > 0 && ShieldControl.RequestedOn == 0`; `HasTarget` = target set.
7. **Weapons:** `WeaponControl = { Autofire = (State == Engage || State == Hold) && target set && FireBlocked == 0 ? 1 : 0, AimMode = TargetLead }`. `FireBlocked`: if `AvoidFriendlyFire`, `QuerySegment(pos, targetCentre, 2)` contains a non-hostile proxy (not self, not target) whose hull the segment crosses before the target → 1, else 0.

### 5.5 `AiSteeringSystem`

`AiSteerJob : IJobEntity` (`ScheduleParallel`) over roots with `AiBrain`, `ShipCommand`, `FlightState`, `LocalTransform`, `WithNone<PlayerShip, Hulk>`. Every tick:
1. `cmd = default` (`FireGroups = 0` — decision 6); `cmd.Edges = AbilityEdgesTick == Tick ? Ability1..4 bits from AbilityEdges : 0`. Roles `Turret`, or no `FlightState` → write `cmd` with `AimWorld = target centre`, `AimValid = target ? 1 : 0` and return.
2. `fwd = Planar.ForwardFromYaw(Planar.YawFromForward(rotate(rot, (0,0,1))))`; `e = Planar.WrapAngle(GoalHeadingYaw − currentYaw)`; `Turn = clamp(e / (FlightState.YawRateCap × 0.5), −1, 1)` (cap 0 → Turn 0).
3. `to = GoalPosition − pos` (XZ); `dist = length(to)`; arrival: `dist < ArrivalRadius` → desired world velocity 0; else `vDes = normalize(to) × min(GoalSpeedFraction × FlightState.ForwardCap, sqrt(2 × aBrake × max(0, dist − ArrivalRadius)))` with `aBrake = max(0.5, 0.5 × PropulsionStats.ForwardAccel)` (or 2 m/s² without stats).
4. Local: `v = FlightMath.ToLocalPlanar(vDes, rot)`; `Thrust.x = clamp(v.x / max(1, StrafeCap), −1, 1)`; `Thrust.y = v.y ≥ 0 ? clamp(v.y / max(1, ForwardCap), 0, 1) : clamp(v.y / max(1, ReverseCap), −1, 0)`; if `|Thrust.y| < 0.002` and the current forward speed exceeds 1 m/s, set `Thrust.y = −0.002 × sign(current forward speed)` so flight assist stops the ship instead of coasting (SPEC-20 decision 4).
5. `AimWorld` = target centre (weapons lead themselves), `AimValid = target set ? 1 : 0`. Write `ShipCommand`.

### 5.6 `PlayerCombatStatusSystem`

`IJob` (single-threaded): find the player ship (a `NativeList` from `ToEntityListAsync` of `PlayerShip`); count AI roots with `AiBrain.State == Engage`, `ShipTarget.Value == player`, and centre within `CombatBubbleRadius` of the player (use the hash proxies). `LastDamagedTick = GridDamageStats.LastDamageTick` of the player root if present. Write the singleton through a `ComponentLookup` on its entity (no main-thread access). No player → `HostilesEngaging = 0`, `LastDamagedTick` unchanged.

### 5.7 Fleets and wings (M3; ADR-0017)

Members think cheaply: in `AiDecisionSystem`, a root with `FleetMember` whose `Leader` is alive and not a hulk skips steps 3 (target choice), 5 (goal) and 6 (avoidance) and copies the leader's `ShipTarget` instead; only leaders and lone ships pay for hash queries, so decision cost is O(groups). `FleetSystem` (`ShipCommandGroup`, after `AiDecisionSystem`, before `AiSteeringSystem`): for each member with a live leader: `GoalPosition = leaderPos + rotateY((slot.x, 0, slot.y), leaderYaw)`, `GoalHeadingYaw = leaderYaw`, `GoalSpeedFraction = 1` (members can catch up), state `Engage` follows the leader's target when the leader engages (copy `ShipTarget`). Leader lost (destroyed or hulk): promote the member with the lowest `Slot` (record new `Leader` on every member through the GameplayEnd ECB is not needed: `FleetMember.Leader` is a plain write by `FleetSystem`, single writer).

## 6. Public API

| Call / type | Pre-conditions | Post-conditions / errors |
|---|---|---|
| `SpatialHash` queries | §5.1 access; inside a job or after `CompleteAccess()` | Ascending, deduplicated indices; total count returned |
| `SpatialBody` | Added by SPEC-31 to celestial roots with colliders | Appears as a `Celestial` proxy next tick |
| `AiSetup.Register(buffer)` / `ProfileIndexOf(catalog, id)` | Once from `AiAnchorShiftSystem.OnCreate` / managed spawners | AI profiles get `AiBrain`; requesters pass the index through `AiBrain.ProfileIndex`; unknown profile → 0 with one warning |
| `AiContent.CreateSingletons` | Boot (SPEC-01) | `AiConfig` with the profiles blob |
| `PlayerCombatStatus` | Read on the main thread outside system updates (SPEC-01 save blocker) after `EntityManager.CompleteDependencyBeforeRO<PlayerCombatStatus>()` | Values of the last tick |

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| No hostile in range | Patrol/Idle; `Autofire = 0`; `ShipTarget` cleared |
| Target becomes a hulk or is destroyed | Cleared at the next think; autofire stops (SPEC-21 also requires a hostile target) |
| Unarmed ship (`MaxWeaponRange == 0`) with role Combat | Treated as Civilian for state choice (flees hostiles) |
| No `ShipStats`/`WeaponStats`/`PropulsionStats` (before SPEC-22 init) | Defaults: hull 1, range = `SensorRange × 0.5`, `aBrake = 2` |
| Player ship has `AiBrain` (bug) | Excluded by `WithNone<PlayerShip>` everywhere |
| Ship far away (`Dormant`) | Thinks every `interval × 6` ticks; steering continues |
| Proxy count > 127 in one query | Output truncated to the lowest indices; consumers use the count to warn once |
| Origin rebase | Anchors and goals shift by the record delta (§5.3); star-system change resets anchors |
| Two AI ships each other's target, both fleeing | Allowed; flee goals are recomputed each think |
| Load | AI restarts in `Idle` (decision 8) |
| Content | AI-E001 `thinkIntervalTicks < 1`; AI-E002 `config_ai.defaultProfile` null; AI-E003 more than 8 `abilityRules` or a rule without `ability`; AI-E004 empty/non-finite formation slots or a pair closer than 20 m (Q15) |

## 8. Performance budget

| Item | Budget | Test (task) |
|---|---|---|
| AI, 50 ships (06 gate) | ≤ 1.5 ms total per frame (decision + steering + status, all jobs, median) | `AiPerfTests.FiftyShips_Under1_5ms` (AI-012) |
| `SpatialHashBuildSystem`, 1,000 proxies | ≤ 0.3 ms wall | `SpatialHashPerfTests.Build1000` (AI-012) |
| `QuerySegment` (20 m segment, 1,000 proxies) | ≤ 0.2 µs median | `SpatialHashPerfTests.QuerySegment` (AI-012) |
| `QuerySphere` (r = 1,500 m) | ≤ 2 µs median | AI-012 |
| GC | 0 B per tick | AI-012 |

**Scale notes:**
- Thinking: 50 ships / 10-tick interval = 5 thinks per tick × ≈ 10 µs (one sphere query + 2 segment queries + scoring) ≈ 50 µs worker per tick. Steering: O(ships) × ≈ 0.3 µs.
- The hash is the only all-pairs structure; nothing in this spec loops over all ships per ship.
- Rejected approaches: per-ship scans of all entities; `NativeParallelMultiHashMap` iteration for query output order; AI writing `PhysicsVelocity`; per-tick target re-selection for every ship.

## 9. Tasks

### AI-001 — Public contract types
- **Milestone:** M1
- **Tier:** standard — transcribes the exact code of §4 (bodies throw `NotImplementedException`, except `CellKey`, `BeginRead`, `EndRead`, `CompleteAccess`, `Get`)
- **Depends on:** `CORE-001`, `DATA-001`, `VOX-001`, `WPN-001`
- **Do:** Create every type of §4.1–§4.4 in the folders of decision 10. Fill the canonical SpatialHash shell staged by WPN-001. If AbilityDefinition is not present yet, stage only its ContentDefinition identity with TODO(ABL-001); ABL-001 fills it (§10 Q6).
- **Files:** `Assets/Game/Simulation/Spatial/{ProxyKind,ProxyFlags,SpatialProxy,SpatialBody,SpatialHashState,SpatialHash}.cs`; `Assets/Game/Simulation/Ai/{AiProfile,AiAbilityRuleDef,AiProfilesBlob,AiConfig,AiState,AiBrain,PlayerCombatStatus,AiSetup,AiContent,FleetMember}.cs`; `Assets/Game/Data/Definitions/Ai/{AiFacing,AiRole,AiAbilityTrigger,AiAbilityRule,AiProfileDefinition,AiConfigDefinition,FormationDefinition}.cs`
- **Acceptance criteria:**
  - [ ] Compiles; `SpatialProxy` is 64 bytes; components unmanaged.
- **Tests:** `AiContractTests.SpatialProxy_Is64Bytes`, `.Components_AreUnmanaged`.

### AI-002 — SpatialHash queries
- **Milestone:** M2
- **Tier:** frontier — F3/F4: every hit test and target choice depends on complete, ordered query results
- **Depends on:** `AI-001`
- **Do:** Implement `SpatialHash` containers, `Create/Dispose`, `QuerySegment`, `QuerySphere`, `FindIndex` (§4.1, §5.2 query algorithms), testable on hand-filled data.
- **Files:** `Assets/Game/Simulation/Spatial/{SpatialHash,SpatialHashQueryData}.cs`, `Assets/Game/Tests/EditMode/Simulation/Spatial/SpatialHashQueryTests.cs`
- **Acceptance criteria:**
  - [ ] Against a brute-force reference on 10,000 random proxies (radii 1–500 m plus 10 large ones up to 50 km) and 10,000 random queries: identical result sets, ascending, no duplicates.
  - [ ] Count returned when output overflows; scratch overflow falls back to the linear scan with the same result.
- **Tests:** `SpatialHashQueryTests.Segment_MatchesBruteForce`, `.Sphere_MatchesBruteForce`, `.Ascending_NoDuplicates`, `.Overflow_CountsAll`, `.LargeProxies_AlwaysConsidered`.

### AI-003 — AI content and boot singletons
- **Milestone:** M2
- **Tier:** standard — fields and defaults in §4.2
- **Depends on:** `AI-001`, `SPEC-04`
- **Do:** Implement the definitions' getters/validation, `AiContent.CreateSingletons` (profiles blob: entry 0 = default profile copy), the four default profile assets and `config_ai.asset`. Ledger note to SPEC-01 for the BootSingletons row.
- **Files:** `Assets/Game/Data/Definitions/Ai/*.cs`, `Assets/Game/Simulation/Ai/AiContent.cs`, `Assets/Game/Content/Ai/{ai_default,ai_turret,ai_skittish,ai_civilian}.asset`, `Assets/Game/Content/Config/config_ai.asset`, `Assets/Game/Tests/EditMode/Simulation/Ai/AiContentTests.cs`
- **Acceptance criteria:**
  - [ ] Blob values equal the assets (angles in radians); validation codes reported.
- **Tests:** `AiContentTests.Blob_MatchesAssets`, `.Validation_Codes`.

### AI-004 — SpatialHashBuildSystem
- **Milestone:** M2
- **Tier:** frontier — F2/F5: parallel gather into a stream, handle discipline shared by six consumer systems
- **Depends on:** `AI-002`, `SPEC-10`, `PWR-001`, `DMG-001`
- **Do:** Implement §5.1–§5.2 (build system, singleton lifetime, access rules) and document the reader pattern in the `SpatialHash` XML summary.
- **Files:** `Assets/Game/Simulation/Spatial/{SpatialHashBuildSystem,ProxyGatherJob,ProxySortJob,ProxyBucketJob}.cs`, `Assets/Game/Tests/EditMode/Simulation/Spatial/SpatialHashBuildTests.cs`
- **Acceptance criteria:**
  - [ ] Every non-spawning grid root and every `SpatialBody` becomes one proxy with the §5.2 values; proxies sorted by `Entity.Index`.
  - [ ] A reader job scheduled in the same tick after the build sees this tick's data; the next build waits for it (no safety errors with leak detection and safety checks on).
  - [ ] Identical hash contents with worker counts 1 and max.
- **Tests:** `SpatialHashBuildTests.ProxyValues`, `.SortedByIndex`, `.ReaderOrdering_NoSafetyErrors`, `.WorkerCountIndependent`, `.SkipsSpawning`.

### AI-005 — AI setup and anchor shift
- **Milestone:** M2
- **Tier:** standard — exact rules in §4.3 and §5.3
- **Depends on:** `AI-003`, `SPEC-02`
- **Do:** Implement `AiSetup.Register`/`ProfileIndexOf` and `AiAnchorShiftSystem` including step 0 (ADR-0015).
- **Files:** `Assets/Game/Simulation/Ai/{AiSetup,AiAnchorShiftSystem}.cs`, `Assets/Game/Tests/EditMode/Simulation/Ai/AiSetupTests.cs`
- **Acceptance criteria:**
  - [ ] Unknown profile → index 0 + one warning; after the first update the anchor = the spawn position and `Initialized == 1`; a requester-set `ProfileIndex` is kept; a shift record moves anchors and goals by its delta exactly once; a star-system change resets them to the ship position.
- **Tests:** `AiSetupTests.UnknownProfile_Default`, `.Anchor_FromPose`, `.Shift_AppliedOnce`, `.StarSystemChange_ResetsAnchor`.

### AI-006 — AiDecisionSystem
- **Milestone:** M2
- **Tier:** frontier — F3/F5: staggered deterministic decisions over the hash, writes three components other systems consume
- **Depends on:** `AI-004`, `AI-005`, `SPEC-20`, `WPN-001`, `WPN-002`, `WPN-003`, `WPN-004`, `WPN-005`, `WPN-006`, `WPN-007`, `WPN-008`, `WPN-009`, `WPN-010`, `WPN-011`, `WPN-012`, `WPN-015`, `WPN-017`, `WPN-018`, `ABL-001`
- **Do:** Implement §5.4 (including step 8, ability rules) and the `abilityRules` bake in `AiContent`.
- **Files:** `Assets/Game/Simulation/Ai/{AiDecisionSystem,AiThinkJob}.cs`, `Assets/Game/Tests/EditMode/Simulation/Ai/AiDecisionTests.cs`
- **Acceptance criteria:**
  - [x] Each ship thinks exactly on its stagger ticks (and 6× less often while dormant).
  - [x] Target choice prefers nearer hostiles, honours player bias and stickiness, never picks allies, neutrals, hulks or itself.
  - [x] State transitions of §5.4 step 4 hold in table-driven tests; leash and return work.
  - [x] Friendly-fire block suspends autofire while an ally crosses the line of fire.
  - [x] Ability rules: a fleeing ship with a Ready speed-boost slot sets that slot's edge on its think tick; a Blocked or cooling-down slot is never pressed; `AbilityEdgesTick` identifies the production tick. AI-007 owns and verifies delivery into `ShipCommand.Edges` on that tick only (Q20).
- **Tests:** `AiDecisionTests.AbilityRules_Fleeing_Boosts`, `.AbilityRules_RespectsBlocked`, `AiDecisionTests.ThinkSchedule`, `.Target_Scoring`, `.Target_NeverAlly`, `.States_Table`, `.Leash_Return`, `.FriendlyFire_Blocks`, `.Avoidance_ShiftsGoal`.

### AI-007 — AiSteeringSystem
- **Milestone:** M2
- **Tier:** standard — closed-form steering in §5.5 on top of SPEC-20
- **Depends on:** `AI-006`
- **Do:** Implement §5.5.
- **Files:** `Assets/Game/Simulation/Ai/{AiSteeringSystem,AiSteerJob}.cs`, `Assets/Game/Tests/EditMode/Simulation/Ai/AiSteeringTests.cs`
- **Acceptance criteria:**
  - [ ] Every AI ship's `ShipCommand` is rewritten every tick with `FireGroups = 0`, and `Edges` = the ability edges of this tick's think (0 on other ticks).
  - [ ] A ship given a goal 500 m away arrives within `ArrivalRadius` and stops (speed < 1 m/s) without overshooting by more than 20 m (with SPEC-20 propulsion in the test world).
  - [ ] Turrets never get thrust.
- **Tests:** `AiSteeringTests.RewritesEveryTick` (including real AI-006 ability-edge delivery on the think tick and zero edges on intervening ticks), `.ArrivesAndStops`, `.FacesHeading`, `.Turret_NoThrust`.

### AI-008 — PlayerCombatStatusSystem
- **Milestone:** M2
- **Tier:** standard — counting rule in §5.6
- **Depends on:** `AI-006`, `DMG-001`
- **Do:** Implement §5.6 and the singleton lifetime.
- **Files:** `Assets/Game/Simulation/Ai/PlayerCombatStatusSystem.cs`, `Assets/Game/Tests/EditMode/Simulation/Ai/PlayerCombatStatusTests.cs`
- **Acceptance criteria:**
  - [ ] Counts only engaging hostiles targeting the player inside the bubble; `LastDamagedTick` follows `GridDamageStats`.
- **Tests:** `PlayerCombatStatusTests.CountsEngagingHostiles`, `.LastDamagedTick_Follows`, `.NoPlayer_Zero`.

### AI-009 — Station turret AI
- **Milestone:** M2
- **Tier:** standard — Role `Turret` behaviour already specified in §5.4–§5.5
- **Depends on:** `AI-007`
- **Do:** Scenario: a static station with turret weapons, `ai_turret` profile and a hostile ship approaching: the station targets it and fires; it never moves.
- **Files:** `Assets/Game/Tests/PlayMode/Simulation/Ai/TurretAiTests.cs`
- **Acceptance criteria:**
  - [ ] The station's `ShipTarget` is the intruder within one think; turrets fire when in range; station pose unchanged.
- **Tests:** `TurretAiTests.Station_TargetsAndFires`, `.Station_NeverMoves`.

### AI-010 — AI combat scenarios
- **Milestone:** M2
- **Tier:** standard — PlayMode tests of specified behaviour
- **Depends on:** `AI-007`, `AI-008`, `WPN-001`, `WPN-002`, `WPN-003`, `WPN-004`, `WPN-005`, `WPN-006`, `WPN-007`, `WPN-008`, `WPN-009`, `WPN-010`, `WPN-011`, `WPN-012`, `WPN-015`, `WPN-017`, `WPN-018`, `PWR-001`, `PWR-002`, `PWR-003`, `PWR-004`, `PWR-005`, `PWR-006`, `PWR-007`, `PWR-008`, `PWR-010`
- **Do:** Scenarios: (1) one AI frigate vs a static player hulk-dummy: closes to preferred range, orbits, hits; (2) damaged AI below flee fraction runs away; (3) two allied AI ships do not shoot through each other; (4) leash: chasing beyond 1.5 × leash returns to the anchor.
- **Files:** `Assets/Game/Tests/PlayMode/Simulation/Ai/AiScenarioTests.cs`
- **Acceptance criteria:**
  - [ ] As stated, over fixed tick counts.
- **Tests:** `AiScenarioTests.Engage_OrbitsAndHits`, `.Flee_BelowThreshold`, `.Allies_NoFriendlyFire`, `.Leash_Returns`.

### AI-011 — AI determinism
- **Milestone:** M2
- **Tier:** frontier — F3/F5: determinism of a multi-system loop (hash → decision → steering → flight → weapons)
- **Depends on:** `AI-010`
- **Do:** 4 AI ships vs 4 AI ships for 1,200 ticks at frame patterns 1, 2, 0/1 and 6 ticks per frame, worker counts 1 and max; compare per tick: `AiBrain`, `ShipTarget`, `ShipCommand`, root poses and the `CellStateHasher` hash at the end.
- **Files:** `Assets/Game/Tests/PlayMode/Simulation/Ai/AiDeterminismTests.cs`
- **Acceptance criteria:**
  - [ ] Identical across all runs.
- **Tests:** `AiDeterminismTests.FourVsFour_Identical`.

### AI-012 — AI and hash performance
- **Milestone:** M2
- **Tier:** frontier — F4: owns the 06 AI gate
- **Depends on:** `AI-010`, `TST-001`
- **Do:** Performance tests of §8 (50 AI ships in combat with weapons and flight active; 1,000 proxies for hash micro-benchmarks); record in the ledger *Measurements*.
- **Files:** `Assets/Game/Tests/PlayMode/Simulation/Ai/{AiPerfTests,SpatialHashPerfTests}.cs`
- **Acceptance criteria:**
  - [ ] All §8 budgets met or an *Open questions* row with numbers.
- **Tests:** `AiPerfTests.FiftyShips_Under1_5ms`, `SpatialHashPerfTests.Build1000`, `.QuerySegment`, `.QuerySphere`.

### AI-013 — Fleet data and formations
- **Milestone:** M3
- **Tier:** standard — data types and content only
- **Depends on:** `AI-003`
- **Do:** `FormationDefinition` validation (≥ 1 slot, no two slots closer than 20 m), blob baking into `AiConfig` (formations array), three formations: `formation_line`, `formation_wedge`, `formation_column`.
- **Files:** `Assets/Game/Data/Definitions/Ai/FormationDefinition.cs`, `Assets/Game/Content/Ai/formation_*.asset`, tests
- **Acceptance criteria:**
  - [ ] Formations bake; validation reports spacing errors.
- **Tests:** `FormationTests.Bake`, `.Validation_Spacing`.

### AI-014 — FleetSystem and member thinking
- **Milestone:** M3
- **Tier:** frontier — F5: leader/member coordination and leader promotion across decision and steering
- **Depends on:** `AI-013`, `AI-007`
- **Do:** Implement §5.7, including the member short-cut in `AiDecisionSystem`.
- **Files:** `Assets/Game/Simulation/Ai/{FleetSystem,AiThinkJob}.cs`, `Assets/Game/Tests/EditMode/Simulation/Ai/FleetTests.cs`
- **Acceptance criteria:**
  - [ ] Members hold slots within 15 m while the leader cruises; members engage the leader's target; leader loss promotes the lowest slot deterministically.
  - [ ] A member with a live leader performs no `QuerySphere` on its think (counted by a test hook).
- **Tests:** `FleetTests.HoldsSlots`, `.FollowsLeaderTarget`, `.LeaderLoss_Promotes`.

### AI-015 — Fleet and swarm scenario and performance
- **Milestone:** M3
- **Tier:** standard — tests of specified behaviour
- **Depends on:** `AI-014`
- **Do:** Two 8-ship fleets fight; measure AI cost; check formations visually through the harness. Swarm gate (06, ADR-0017): 30 ships plus 100 single-brick `GridProfile.SmallCraft` grids in groups of 10, all fighting; record the result in the ledger *Measurements*.
- **Files:** `Assets/Game/Tests/PlayMode/Simulation/Ai/FleetScenarioTests.cs`
- **Acceptance criteria:**
  - [ ] AI total stays ≤ 1.5 ms with 16 fleet ships plus 34 others.
  - [ ] Swarm: main thread ≤ 10 ms median and AI ≤ 1.5 ms with 30 ships + 100 craft (06).
- **Tests:** `FleetScenarioTests.TwoFleets_Under1_5ms`, `.Swarm_30Ships100Craft` (`Performance`).

## 10. Open questions

| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | Should AI use abilities in v1? | **Answered (2026-10-10, owner, extensibility review X5): yes**, through per-profile `AiAbilityRule`s (§4.2, §5.4 step 8) | Owner |
| 2 | SPEC-22 power demand: a weapon also requests power while `WeaponControl.Autofire == 1` and `ShipTarget` is set (AI and player autofire never hold fire groups) | **Applied** in SPEC-22 §5.3 (2026-10-09). Yes | SPEC-22 |
| 3 | AI awareness of the player: always perfect inside `SensorRange` (no stealth) | Yes | Owner |
| 4 | SPEC-02/SPEC-30: add the Core singleton `OriginShiftRecord { uint Sequence; float3 Delta; byte StarSystemChanged; }` (`Game.Core.Components`). `OriginRebaseSystem.OnCreate` creates it; its `DecideJob` writes the values of the latest rebase together with `FrameOrigin` (`Sequence = FrameOrigin.ShiftSequence`). Simulation owners of cached positions (AI anchors here, `ProjectileStore` in SPEC-21) react to it because Game.Simulation cannot read `FrameOrigin` | **Applied** in SPEC-02 §4.3, SPEC-30 §5.10 (2026-10-09). Add the record (last rebase only; consumers detect gaps by `Sequence`) | Tech lead |
| 5 | Architecture §5 comment says "ShipProxy hash"; SPEC-70 reads `ShipProxy` positions | **Applied** in architecture §5, SPEC-70 §6.4 (2026-10-09). Rename to `SpatialProxy` in both | Tech lead |
| 6 | AI-001's file list omits the ability-rule types and references an absent AbilityDefinition; SpatialProxy's listed fields naturally occupy 48 bytes despite the 64-byte acceptance contract | **Resolved (2026-10-12):** include the existing ability-rule types, and stage the canonical Game.Data.Definitions.AbilityDefinition identity with TODO(ABL-001), as WPN-001 stages FxDefinition. Honor the explicit 64-byte proxy contract with Sequential Size=64 (trailing padding; existing fields/offsets unchanged). This is contract transcription, not a performance optimization. Expose FormationDefinition.Slots and profile AbilityRules as read-only lists, and declare AiConfigDefinition's AI-E002 validation override for AI-003. | Tech lead |

| 7 | Query overflow requests a counter in State, but State has no such field and §5.1 parallel readers are read-only; scratch capacity is 1,023 rather than 1,024 ints. | **Resolved:** use the actual Capacity before every add; scan all proxies on overflow, preserving exact total and lowest-index output. Do not mutate State from queries or add unsafe shared writes. Shared overflow telemetry needs a future explicit owning-writer contract; no telemetry claim in AI-002. | Tech lead |
| 8 | Very large query bounds can enumerate billions of empty cells or overflow integer cell loops. | **Resolved:** above 4,096 enumerated cells or outside representable coordinates, use the same exact linear fallback; normal bounded queries retain the specified hash/sort/dedupe algorithm. This bounds traversal work without losing candidates. | Tech lead |

| 9 | Passing the whole hash into a Burst job registers main-thread GuardedHandles as job inputs and breaks EndRead/disposal. | **Resolved:** ADR-0064 adds a non-owning SpatialHashQueryData view with only spatial containers; AsReadOnly for job fields, owner handles stay on main thread. Existing owner query APIs delegate without safety suppression. Required ordered-query test covers the real Burst access lifecycle. | Tech lead |
| 10 | AI-003's blob parity criterion includes a rules field whose bake is explicitly assigned to AI-006. | AI-003 bakes every scalar and the default profile copy, with separately allocated empty AbilityRules arrays and an AI-006 TODO. Author/validate the specified ability rules now; AI-006 owns their runtime bake and consumption. | Tech lead — resolved by the coordinator's AI-003 boundary instruction; central mirror pending. |
| 11 | §7 names only the three example validation failures, although the authored fields have additional numeric ranges and enum domains. | Use AI-E001 for all invalid profile scalar ranges/enums, AI-E002 for invalid config ranges/default or zero/multiple configs, and AI-E003 for invalid rule count/ability/trigger/threshold. Float tunables must be finite. Reject a default profile outside the catalog before allocating the blob. Preserve formation getters; formation behavior remains AI-013. | Tech lead — resolved by AI-003; central mirror pending. |
| 12 | AI-005 names PlayerTargetingSystem as its ordering predecessor, but WPN-005 had not supplied that type. | **Resolved WPN-005:** targeting now occupies the normal ShipCommandGroup bucket after OrderFirst player command application (SPEC-02 decision 13); AiAnchorShiftSystem declares UpdateAfter(PlayerTargetingSystem) in that same normal bucket. AI-006 still orders after AiAnchorShiftSystem. No alternate targeting system, root composition or origin producer was introduced. | Tech lead — resolved by WPN-005; central mirror pending. |
| 13 | The canonical OriginShiftRecord creator/writer (GAL-009 OriginRebaseSystem) is absent, although AI-005's declared dependencies are ready. | Implement and test §5.3 exactly with the canonical record in the test worlds. Production updates retain RequireForUpdate<OriginShiftRecord> and wait for GAL-009 to create it; no AI-owned singleton or origin producer is introduced. First-init retains ProfileIndex and snapshots the current sequence; unsigned sequence wrap is adjacent, gaps reset cached points, and existing brains retain their other decision fields on a system change. This remains an explicit live integration dependency for the lead. | Tech lead — AI-005 default; central mirror pending. |
| 14 | AI-013 requests a formations array in AiConfig without specifying its blob shape or disposal. | **Resolved by accepted [ADR-0065](../decisions/ADR-0065-formation-blob-storage.md):** extend the existing boot-owned AiProfilesBlob with BlobArray<FormationDef> Formations; each row has ContentHash IdHash and BlobArray<float2> Slots. Use one-based FormationDefinition catalog indices, reserve an empty row 0, retain authored slot order and X/right, Y/forward metre coordinates. Access by ref through AiConfig.Profiles.Value.Formations. The sole existing Profiles disposal path owns the array; AI-014 owns steering. | Tech lead — accepted centrally 2026-10-13; mirror pending. |
| 15 | AI-013 does not name a validation code, finite-coordinate policy or exact default formation offsets. | **AI-013 default:** AI-E004 reports empty arrays, non-finite offsets and every pair closer than 20 m (exactly 20 m is valid). Author ten follower slots per formation using SPEC-33 ENC-013's 40 m pattern: line alternates ±40, ±80, … ±200 m at forward 0; wedge uses those paired X values at forward −40, −80, … −200 m; column is X 0, forward −40 through −400 m. The leader is not a follower slot. No byte-slot cap or steering behavior is added. | Tech lead — default recorded for central mirror. |
| 16 | AI-006 broad spec dependencies include advisory performance or owner feel work | Replace the broad dependency with its explicit same-milestone functional prerequisites, excluding only WPN-013, WPN-014. Those tasks retain their full scope and tests but do not block this implementation. No runtime contract or functional prerequisite is removed; owner feel sign-off remains pending in its own task. Follows ADR-0042 and the owner instruction to build systems before tuning. | Tech lead |

## 11. References

- Audit (background only): `docs/reference/audit-2026-10/02-combat-destruction.md` (AI and targeting notes).
- Reynolds, "Steering Behaviors For Autonomous Characters" (1999) — arrival and local avoidance, background only.
- SPEC-20 §6 AI writer contract; SPEC-21 §4.8 hash contract.

| 17 | AI-010 still expands whole weapon/power specs, including advisory performance and owner feel work | Under ADR-0068, retain every same-milestone functional weapon/power prerequisite explicitly, excluding only WPN-013/014 and PWR-009. AI-007/008 and full combat WPN-012 remain required. Performance and balance tasks retain their full scope; no runtime contract or owner gate changes. | Tech lead |

| 18 | An authored rule can reference an ability asset outside the catalog, producing index zero during the AI-006 bake | Reject absent non-null ability references with AI-E003 before allocating the profiles blob. Bake rules in authored order with the canonical ability-family index, trigger and threshold; independently allocate the default profile's rules. Existing content parity/validation tests cover this completion of the AI-003 staging boundary. | Tech lead |

| 19 | AI-006 state/range/avoidance prose has conflicting boundary instructions | Apply §7's half-sensor fallback when weapon stats are absent. Return remains latched until within twice ArrivalRadius, with turret and threatened-flee priority retained; clear its target while returning. Include AvoidanceMargin in the broadphase inflate so the specified hull clearance cannot be culled before the exact check. Idle/Hold do not acquire movement goals from avoidance. Warn once per decision-system lifetime and atomically count truncations in system-owned native telemetry; telemetry never affects simulation decisions. | Tech lead |

| 20 | AI-006 acceptance asks for ShipCommand delivery implemented by its dependent AI-007 | AI-006 validates the ability decision and production tick; AI-007 RewritesEveryTick validates the production decision-to-command path and suppression on intervening ticks. Preserve the end-to-end requirement in AI-007 rather than blocking its prerequisite on absent downstream steering. No runtime contract, behavior, test count or milestone exit scope changes. | Tech lead |
| 21 | AI-007 lists FlightState in its required query but also requires command rewriting without it; target-centre resolution and startup commands are abbreviated | Query AiBrain/ShipCommand/LocalTransform with PlayerShip/Hulk excluded, read FlightState optionally through a cached lookup, and require SimClock for exact ability-edge delivery. Turret/no-flight roots receive fresh zero-thrust/zero-turn commands with this tick's four masked ability bits and valid aim. Resolve aim from current target LocalTransform and occupied VoxelGrid bounds using the same centre calculation as WeaponAimJob; missing targets, hulks and enabled spawning targets clear aim without changing ShipTarget or WeaponControl. Enabled spawning roots receive a default command. Retain §5.5's formulas and optional 2m/s² braking fallback; do not write brain, flight or physics. Exactly four tests: real scheduled AiDecisionSystem ability generation and think/intervening delivery, real PropulsionStatsSystem/PropulsionSystem/PlanarConstraintSystem plus Unity Physics arrival, wrapped headings/local thrust/braking, and turret/no-flight/stale-target/startup behavior. Station composition currently registers AiBrain but neither FlightState nor ShipCommand; stationary steering tests supply ShipCommand. Lead must confirm station command composition for ability-edge consumption during AI-009 integration; AI-007 preserves existing registration contracts. | Tech lead — AI-007 default; central mirror, station composition review and lead Unity validation pending. |
| 22 | AI-008 abbreviates hostility, radius/overflow, incomplete player state and singleton lifetime details | Create the status entity in OnCreate before the boot snapshot and destroy it after completing the system dependency in OnDestroy. Gather PlayerShip asynchronously; exactly one tagged player is required, otherwise count zero and retain LastDamagedTick. With a player, absent GridDamageStats means tick zero; damage tracking still works without a proxy. Count live Ship/Station proxies with current Engage/target/allegiance, excluding self, hulks and enabled spawning; current threat-to-player FactionRelations must be Hostile (additional runtime prerequisites: hash and relations). Use 3D proxy-centre distance inclusive at the configured radius, independent of proxy radius; scan the single guarded read-only proxy view so fixed-list overflow cannot truncate the count, saturating the ushort output. Invalid/nonfinite spatial data counts zero. No main-thread gather/value reads or fixed-step completion; TempJob list disposal is chained. Preserve the specified ordering after the parallel AI-007 delivery; no steering, save-blocker wiring, contracts or tuning in AI-008. | Tech lead — AI-008 default; lead validation/central mirror pending |

| 24 | AI-014: representation and tie-breaking after leader loss? | Resolved: flat fleets only. Schedule lone/leader decisions, snapshot their target/state, then member decisions on the same tick; no concurrent lookup of writable brain/target. Live members skip sphere target search and avoidance but keep abilities and friendly-fire segment checks. A cumulative internal sphere-query counter supports the required test. A promoted root keeps FleetMember with Leader=null (treated as a lone leader by decisions), avoiding structural changes. Other members retain their slot/formation and point at the promoted root. Pick lowest Slot, then PersistentId, then entity index for duplicate IDs. Destroyed/hulk/spawning roots are not candidates; invalid formation/slot leaves its existing goal intact. | Tech lead |
