# SPEC-22 — Power, shields and ship stats

| | |
|---|---|
| **Status** | Draft |
| **Area code** | PWR (task IDs are `PWR-001`, `PWR-002`, …) |
| **Module(s)** | `Game.Simulation` (folder `Assets/Game/Simulation/Power/`, namespace `Game.Simulation.Power`); tuning definition in `Game.Data` (`Assets/Game/Data/Definitions/Power/`) |
| **Depends on specs** | SPEC-02 (`SimClock`, `ContentHash`, groups, `GameplayEndCommandBufferSystem`, `GameLog`); SPEC-04 (`BlockDefsBlob`, block modules (ADR-0013): `BlockModulesRef`, `PowerSourceParams`, `PowerStorageParams`, `ThrusterParams`, `ShieldEmitterParams`, `WeaponMountParams`); SPEC-10 (`VoxelGrid`, `BlockInstance`, `FunctionalBlock`, `FunctionalComponentRegistration`, `GridSpawning`, `VoxelRaycast`, `GridCellReader`); SPEC-12 (`PhysicsMass` writer); SPEC-13 (`DamageEvent`, `DamageStreams`, `Hulk`, `DamageType`); SPEC-20 (`ShipCommand`); SPEC-21 (`WeaponDefsBlob` draw and fire groups, `GridSpaceMath`); SPEC-24 (`AbilitySlot`, `ShipStatModifier` writer); SPEC-63 (`FxEventQueue`, `FxEvents`) |
| **Depended on by** | SPEC-20 (`ShipStats` accelerations and multipliers, `Powered` on thrusters), SPEC-21 (`PowerConsumer` grants, `ShieldState`, `ShieldHit`, `ShieldHitStreams`, `ShieldMath`, `ShipStats` multipliers), SPEC-23 (`ShipStats`, `ShieldState`), SPEC-24 (`ShipPower.AbilityGrantMask`, `ShieldControl`, `ShipStatModifier`), SPEC-61 (HUD reads `ShipPower`, `ShieldState`, `ShipStats`), SPEC-63 (`ShieldState`, `ShieldRipple`), SPEC-51 (stat names shown in the editor) |
| **Milestone** | M2 |

> Read [01-architecture.md](../01-architecture.md), [02-conventions.md](../02-conventions.md), [03-glossary.md](../03-glossary.md) and [07-implementation-traps.md](../07-implementation-traps.md) before this spec. This spec may not contradict them.

## 1. Purpose

Every ship has an energy budget, an analytic shield and a set of aggregated stats. Reactors and the core generate energy, capacitors and the core store it, and weapons, shield emitters and thrusters consume it. One system allocates energy per ship per tick by priority and sets `Powered` enable bits; nobody else writes energy. One system aggregates stats (mass, thrust, turn rate, power, DPS, shield capacity) and recomputes the expensive part only when the ship's set of working functional blocks changes. One system owns shield HP: it regenerates, resolves `ShieldHit`s and converts overflow into hull `DamageEvent`s. The old prototype wrote shared energy from parallel jobs (02-C08), used the enable bit of its energy component to mean "shields on" (02-C33), could never create or enable shields (02-C14) and drained all energy in about 10 s of autofire (LEDGER R21). This spec fixes all four by construction and by the energy balance table in §4.9.

## 2. Scope

**In scope:**
- `ShipPower`, `PowerConsumer`, `Powered`, `PowerLink`, `ShipStats`, `ShipStatModifier`, `StatusSystem` with `StatModifierRequest`/`InhibitRequest` channels and `Inhibited` (ADR-0016), `ShieldState`, `ShieldRipple`, `ShieldControl`, `ShieldHit`, `ShieldHitStreams`, `ShieldMath`, `PowerTuning`, `PowerTuningDefinition`, `PowerGridSetup`.
- Reactors, cores and capacitors as energy sources; weapons, shield emitters and thrusters as consumers.
- `PowerInitSystem`, `PowerAllocationSystem` (priorities, brownout), `ShipStatsAggregateSystem` (dirty-only), `ShieldSystem` (fit, regen, hits, overflow).
- Hulk handling (no power, no shield), the shield toggle state (`ShieldControl`), the energy balance rules and the blueprint power-budget validator.

**Out of scope (and who owns it instead):**
- Which blocks exist and their raw parameters: SPEC-04 (block modules). This spec retunes the shield emitter values (§4.9, open question 1).
- Thruster impulses and the speed cap: SPEC-20 (reads `ShipStats` multipliers and `Powered`).
- Weapon fire, beams, projectile casts, explosions and the analytic shield test inside them: SPEC-21 (uses `ShieldMath`, writes `ShieldHitStreams`).
- Ability activation and the shield toggle ability: SPEC-24 (writes `ShieldControl`, posts `StatModifierRequest`s, reads `ShipPower.AbilityGrantMask`).
- Cell damage, `Hulk` tagging, `DamageStreams`: SPEC-13. Shield and tracer drawing: SPEC-63. HUD: SPEC-61.
- Saving energy or shield HP: not saved (Decision 12).

## 3. Decisions (binding)

1. **Files sit directly in `Assets/Game/Simulation/Power/` with namespace `Game.Simulation.Power`.** Reason: five specs share `Game.Simulation`; one area folder per spec (same rule as SPEC-10 Decision 15) prevents file collisions.
2. **Energy is a per-ship scalar pool.** `ShipPower.Stored` (E) is refilled by `Generation` (E/s) and capped by `Capacity` (E). There is no per-block energy. Reason: simple, deterministic, readable on the HUD.
3. **Consumers are all-or-nothing per tick.** A requesting consumer either gets its full `Draw × dt` or nothing. Reason: no fractional weapon states; easy to test.
4. **Allocation order is fixed: priority descending, then `InstanceIndex` ascending, then `BlockModuleId`.** Each consumer's priority is set by the feature that owns its module, from `PowerTuning`: shield emitters 200, thrusters 150, ability one-shot costs 120, weapons 100 (a later feature adds its own tuning field). Reason: shields and engines stay up longest; weapons brown out first; order is deterministic.
5. **A denied consumer stays off for `BrownoutHoldTicks` (30 ticks = 0.5 s).** Reason: no flicker at the budget edge.
6. **Demand is written by the feature that owns each consuming module (ADR-0014).** Every power-consuming module registers a `PowerRequest` element on its functional entity (`PowerRegistration.RegisterConsumer`); its owner fills `Draw`/`Priority` at init and writes `Requesting` every tick from a system in `PowerDemandGroup` (after `ShipCommandGroup`, before `PowerAllocationSystem`). The allocator never inspects weapons, thrusters or shields. Ability one-shot costs keep their per-ship channel (`AbilitySlot.PendingPower`, decision 8). Reason: same-tick response, single writer per field, and a new consumer never edits this spec.
7. **Weapons draw while their fire group is held, not per shot.** Reason: the draw table in §4.9 is per second and sustainable; the old per-shot drain plus per-second beam drain double-charged (02-C22).
8. **Ability energy is a one-shot cost granted with one tick of latency** through `ShipPower.AbilityGrantMask` (§5.3 step 7). Reason: only `PowerAllocationSystem` writes `ShipPower`.
9. **Stats are shared plus feature-owned (ADR-0014), each split into cheap and expensive parts.** `ShipStats` holds only the shared part (mass, mass fraction, bounds, alive-functional signature and `FunctionalRevision`); `ShipStatMultipliers` holds the product of active modifiers per `StatId`. Every feature with ship-level totals owns a `<Feature>Stats` root component recomputed by its own system when `FunctionalRevision` changes (here `PowerStats`, `ShieldStats`; SPEC-20 `PropulsionStats`; SPEC-21 `WeaponStats`). Mass-derived values update when `PhysicsMass` changes. Reason: architecture §11.1 (incremental, never global); a new feature never edits the aggregate.
10. **One shield per ship: an analytic ellipsoid in grid space** fitted to the grid's spawn bounds (`VoxelGrid.CellMin/CellMax`) plus padding, refitted only when the alive functional set changes. All powered alive emitters add their capacity and regen to it. Reason: architecture §6.4; cheap segment tests.
11. **Shield HP changes only in `ShieldSystem`.** Producers (SPEC-21) test shields analytically against last tick's `ShieldState` and emit `ShieldHit`; `ShieldSystem` applies hits in a stable order and converts overflow into `DamageEvent`s. Reason: architecture §8.
12. **Energy and shield HP are not saved.** A loaded or newly spawned ship starts with `Stored = Capacity` and full shields. Reason: saves happen outside combat (00-vision M3 scope); no extra save chunk.
13. **Root components come from the grid profile (ADR-0015).** `PowerGridSetup.Register` adds `RootTypes` to every combat profile; `GridSpawnSystem` adds them zeroed at conversion. Functional-entity components are registered through SPEC-10's `FunctionalComponentRegistration`. Values, including first-init ones, are set by `PowerInitSystem` in `GameInitGroup`. Reason: no structural changes in Simulation systems; any spawner, Burst ones included, gets the same ship; spawn-frame initialisation is independent of frame rate.
14. **Hulks have no power and no shield.** Reason: glossary *Hulk*.
15. **Dormant grids skip the stats recompute** (architecture §11.9) but keep power allocation and shield regen (both O(consumers), cheap).
16. **Grids with `GridSpawning` enabled are skipped by every per-tick system here.** Reason: their physics body and bricks are not ready (SPEC-10 Decision 14).

## 4. Data model

All types are **public contract** unless marked *internal*. Each file holds one type and starts with `/// <summary>Implements SPEC-22 §4.x.</summary>`. Units: energy E, power E/s, HP abstract, lengths metres in grid space (SPEC-10 Decision 6: cell `c` occupies `[c, c+1) × CellSize` from the grid origin, the root's `LocalTransform`).

### 4.1 Power (`Assets/Game/Simulation/Power/`)

```csharp
namespace Game.Simulation.Power
{
    [System.Flags] public enum ShipPowerFlags : byte { None = 0, Initialized = 1, Brownout = 2, Dead = 4 }

    /// <summary>Per-ship energy state on the grid root. Only writer: PowerAllocationSystem (and PowerInitSystem at init).</summary>
    public struct ShipPower : IComponentData
    {
        public float Stored;            // E, 0..Capacity
        public float Capacity;          // E = Σ alive core.PowerStorage + capacitor.PowerStorage
        public float Generation;        // E/s = Σ alive core.PowerGeneration + reactor.PowerGeneration; 0 on hulks
        public float DemandPerSecond;   // E/s requested this tick (HUD)
        public float GrantedPerSecond;  // E/s granted this tick (HUD)
        public uint AbilityGrantMask;   // bit i = AbilitySlot i's pending one-shot cost was granted this tick
        public uint AbilityGrantTick;   // SimClock.Tick the mask belongs to
        public ShipPowerFlags Flags;
    }

    [System.Flags] public enum PowerConsumerFlags : byte { None = 0, Granted = 1, Requesting = 2, Alive = 4, BrownedOut = 8 }

    /// <summary>One energy consumer of the ship = one PowerRequest element of one functional entity, sorted by (Priority desc,
    /// InstanceIndex asc, Module asc). Built by PowerInitSystem; per-tick fields written only by PowerAllocationSystem.</summary>
    [InternalBufferCapacity(0)]
    public struct PowerConsumer : IBufferElementData
    {
        public Entity Functional;       // the block's functional entity
        public int InstanceIndex;       // index into the grid's BlockInstance buffer
        public Game.Data.Blocks.BlockModuleId Module;   // the consuming module
        public byte Priority;           // copied from the PowerRequest element at init
        public byte RequestIndex;       // index of the element in the entity's PowerRequest buffer
        public PowerConsumerFlags Flags;
        public float Draw;              // E/s while requesting (copied at init)
        public uint DeniedUntilTick;    // brownout hold: no request is granted while SimClock.Tick < this
    }

    /// <summary>One power-consuming module of a functional entity (ADR-0014). One element per consuming module of the block.
    /// Writers, per element: the feature that owns `Module` (Draw and Priority once at init in GameInitGroup before PowerInitSystem;
    /// Requesting every tick from a system in PowerDemandGroup); ConsumerIndex: PowerInitSystem. Nobody else writes it.</summary>
    [InternalBufferCapacity(2)]
    public struct PowerRequest : IBufferElementData   // 12 bytes
    {
        public Game.Data.Blocks.BlockModuleId Module;
        public byte Priority;           // from PowerTuning (owner's field)
        public byte Requesting;         // 1 = wants its Draw this tick
        public byte Reserved;
        public float Draw;              // E/s while requesting, ≥ 0
        public short ConsumerIndex;     // index in the grid's PowerConsumer buffer; −1 until PowerInitSystem bound it
        public short Reserved2;
    }

    /// <summary>Demand writers run here (ADR-0014): after ShipCommandGroup (OrderFirst), before PowerAllocationSystem. Members order
    /// themselves only relative to each other if needed; they never touch another module's element.</summary>
    [UpdateInGroup(typeof(Game.Core.Systems.GameplayPrePhysicsGroup))]
    [UpdateBefore(typeof(PowerAllocationSystem))]
    public partial class PowerDemandGroup : ComponentSystemGroup { }

    /// <summary>Registration helper for power-consuming modules (call from a [CreateAfter(typeof(GridSpawnSystem))] system's OnCreate).</summary>
    public static class PowerRegistration
    {
        /// Appends {module, PowerRequest}, {module, PowerLink} and {module, Powered} to the FunctionalComponentRegistration buffer (SPEC-10).
        public static void RegisterConsumer(DynamicBuffer<Game.Voxel.Model.FunctionalComponentRegistration> registrations, Game.Data.Blocks.BlockModuleId module);
        /// Init helper for owners: finds the element with `module` (appends it if missing) and sets Draw and Priority; Requesting = 0.
        public static void SetConsumer(DynamicBuffer<PowerRequest> requests, Game.Data.Blocks.BlockModuleId module, float draw, byte priority);
        /// Demand helper: sets Requesting of the element with `module` (no-op if missing). Linear scan over ≤ 4 elements.
        public static void SetRequesting(DynamicBuffer<PowerRequest> requests, Game.Data.Blocks.BlockModuleId module, bool requesting);
    }

    /// <summary>Enable bit on functional entities: the block has power this tick. Only writer: PowerAllocationSystem.</summary>
    public struct Powered : IComponentData, IEnableableComponent { }

    /// <summary>On every functional entity of a power-consuming module (registered by PowerRegistration.RegisterConsumer, so it starts
    /// zeroed). Only writer: PowerInitSystem.</summary>
    public struct PowerLink : IComponentData
    {
        public short ConsumerCount;     // PowerRequest elements bound to PowerConsumer entries; 0 = not a consumer
        public byte Bound;              // 0 = new entity (spawn, rebuild or edit), 1 = bound by PowerInitSystem
        public byte Reserved;
    }
}
```

### 4.2 Stats

```csharp
namespace Game.Simulation.Power
{
    using Game.Data.Combat;   // StatId

    [System.Flags] public enum ShipStatsFlags : byte { None = 0, Initialized = 1, HasMass = 2 }

    // StatId is designed here but lives in Assets/Game/Data/Combat/StatId.cs (namespace Game.Data.Combat, architecture §3.2) so ability
    // content uses it directly:  public enum StatId : byte { Thrust = 0, MaxSpeed = 1, FireRate = 2, WeaponDamage = 3, ShieldRegen = 4, TurnRate = 5 }  // add at end only (≤ 15 values)

    /// <summary>Shared ship stats on the grid root (architecture §6.4, ADR-0014). Only writer: ShipStatsAggregateSystem.
    /// Feature totals live in feature-owned components (PowerStats, ShieldStats here; SPEC-20 PropulsionStats; SPEC-21 WeaponStats).</summary>
    public struct ShipStats : IComponentData
    {
        // Mass part: recomputed when PhysicsMass changed
        public float Mass;              // kg = 1 / PhysicsMass.InverseMass; 0 until the body exists
        public float InitialMass;       // kg, first non-zero Mass seen; never changes afterwards
        public float MassFraction;      // Mass / InitialMass (1 at spawn), hull-health proxy for AI and HUD
        // Functional part: recomputed when the alive functional set changed
        public int AliveFunctionalCount;
        public float3 BoundsMin, BoundsMax;   // grid space, from VoxelGrid.CellMin/CellMax: CellMin·CellSize .. (CellMax+1)·CellSize
        public float3 BoundsCenter;     // (BoundsMin + BoundsMax) / 2
        public float BoundingRadius;    // length(BoundsMax - BoundsCenter)
        public ulong FunctionalSignature;     // §5.4 step 2
        public uint FunctionalRevision;       // +1 every time FunctionalSignature changes; feature stats and SPEC-24 resync on change
        public ShipStatsFlags Flags;
    }

    /// <summary>Product of active ShipStatModifier multipliers per StatId (1 if none). Only writer: ShipStatsAggregateSystem.</summary>
    public struct ShipStatMultipliers : IComponentData
    {
        public FixedList64Bytes<float> Values;   // index = (int)StatId; length = highest StatId + 1; filled with 1 at init
        public float Get(StatId id) => (int)id < Values.Length ? Values[(int)id] : 1f;
    }

    /// <summary>Power totals of alive blocks (feature stats, ADR-0014). Only writer: ShipStatsAggregateSystem (power job).</summary>
    public struct PowerStats : IComponentData
    {
        public float Generation;        // E/s = Σ PowerSource rows
        public float Capacity;          // E = Σ PowerStorage rows
        public float DrawAtFullDemand;  // E/s = Σ consumer Draw
        public int AliveSourceCount;    // blocks with a PowerSource or PowerStorage module
        public uint SourceRevision;     // ShipStats.FunctionalRevision these totals were built from
    }

    /// <summary>Shield totals of alive emitters (feature stats). Only writer: ShipStatsAggregateSystem (power job).</summary>
    public struct ShieldStats : IComponentData
    {
        public float Capacity, Regen, RechargeDelay, Draw;   // Σ capacity, Σ regen/s, min delay (s), Σ draw (E/s)
        public int AliveEmitterCount;
        public uint SourceRevision;
    }

    /// <summary>Active stat modifier on the grid root. Only writer: StatusSystem (§5.3b, ADR-0016), from StatModifierRequests.</summary>
    [InternalBufferCapacity(4)]
    public struct ShipStatModifier : IBufferElementData   // 12 bytes
    {
        public StatId Stat;
        public Game.Data.Combat.ProducerKind Producer;   // who asked: Ability, Status, Environment…
        public ushort SourceId;         // the producer's key (abilities: slot index)
        public float Multiplier;        // > 0
        public uint UntilTick;          // removed when SimClock.Tick ≥ UntilTick; uint.MaxValue = until cancelled
    }

    /// <summary>Request to set, refresh or cancel a stat modifier (ADR-0016). Upsert key (Root, Producer, SourceId, Stat);
    /// Multiplier == 1 or UntilTick ≤ current tick cancels.</summary>
    public struct StatModifierRequest
    {
        public Entity Root; public StatId Stat; public Game.Data.Combat.ProducerKind Producer; public ushort SourceId;
        public float Multiplier; public uint UntilTick; public uint Sequence;
    }

    /// <summary>Request to switch blocks off (EMP, ion, hacking; ADR-0016). Target = a functional entity, or a grid root = all its blocks.</summary>
    public struct InhibitRequest
    {
        public Entity Target; public uint DurationTicks; public byte Reason; public Game.Data.Combat.ProducerKind Producer; public uint Sequence;
    }

    /// <summary>On every functional entity (registered with Module = None). Only writer: StatusSystem. Inhibited while SimClock.Tick < UntilTick.</summary>
    public struct Inhibited : IComponentData { public uint UntilTick; public byte Reason; }

    /// <summary>Singleton holding the two status channels. Created and disposed by StatusSystem.</summary>
    public struct StatusChannels : IComponentData
    {
        public Game.Data.Streams.SimEventChannel<StatModifierRequest> StatModifiers;
        public Game.Data.Streams.SimEventChannel<InhibitRequest> Inhibits;
    }
    }
}
```

### 4.3 Shields

```csharp
namespace Game.Simulation.Power
{
    [System.Flags] public enum ShieldFlags : byte { None = 0, Initialized = 1, Collapsed = 2, FillOnNextUpdate = 4 }

    /// <summary>Analytic ellipsoid shield on the grid root (architecture §6.4). Only writer: ShieldSystem (PowerInitSystem sets
    /// FillOnNextUpdate at init).</summary>
    public struct ShieldState : IComponentData
    {
        public float Hp, MaxHp;         // MaxHp = Σ Capacity of alive, powered emitters
        public float RegenPerSecond;    // Σ RegenPerSecond of alive, powered emitters × ShieldRegenMultiplier
        public uint RegenResumeTick;    // regen allowed when SimClock.Tick ≥ this
        public float3 Center;           // grid space metres
        public float3 Radii;            // semi-axes, metres, each ≥ ShieldMinRadiusMetres
        public float BoundingRadius;    // cmax(Radii)
        public uint ShapeRevision;      // ShipStats.FunctionalRevision used for the last fit
        public byte Active;             // 1 = shield is up and blocks hits (SPEC-63 reads this name)
        public byte RippleHead;         // next ShieldRipple slot to overwrite, 0..7
        public ShieldFlags Flags;
    }

    /// <summary>Ring buffer of the last 8 shield impacts for the shell shader (SPEC-63). Only writer: ShieldSystem.</summary>
    [InternalBufferCapacity(8)]
    public struct ShieldRipple : IBufferElementData
    {
        public float3 LocalPoint;       // grid space, on the ellipsoid
        public uint StartTick;          // SimClock.Tick of the hit; 0 = unused
        public float Strength;          // 0..1 = min(1, absorbed / 100)
    }

    /// <summary>Requested shield state. Only writer: ShieldToggleEffectSystem (SPEC-24). Spawned as RequestedOn = 1.</summary>
    public struct ShieldControl : IComponentData { public byte RequestedOn; }

    [System.Flags] public enum ShieldHitFlags : byte { None = 0, Blast = 1 }

    /// <summary>Shield impact event (architecture §6.4 binding fields first). Producers: SPEC-21.</summary>
    public struct ShieldHit
    {
        public Entity Grid;             // binding: the shielded grid root
        public float3 LocalPoint;       // binding: grid space, on the ellipsoid surface (blast: blast centre in grid space)
        public float Amount;            // binding: raw damage before shield type multiplier
        public Game.Data.Combat.DamageType Type;    // binding (designed by SPEC-13, architecture §3.2)
        public Entity Source;           // binding: attacking grid root or Entity.Null
        public float3 LocalDir;         // unit direction of travel in grid space (blast: zero)
        public float Penetration;       // cells, copied to the overflow DamageEvent
        public float Radius;            // blast radius in metres (0 for point hits)
        public float3 WorldImpulse;     // copied to the overflow DamageEvent
        public ShieldHitFlags Flags;
        public Game.Data.Combat.ProducerKind Producer;   // SPEC-02 §4.12: Projectile, Beam, Explosion, Ability…
        public uint Sequence;           // producer sequence for the stable sort key
        public Entity Instigator;       // copied to the overflow DamageEvent (review X6)
    }

    /// <summary>Singleton holding the ShieldHit channel (SPEC-02 §4.12). Created by ShieldSystem.OnCreate (SimEventChannel<ShieldHit>.Create("shield_hits"))
    /// and disposed in its OnDestroy. Taken with SystemAPI.GetSingleton (read-only) by producers and the consumer: no sync points.
    /// Producers (SPEC-21 projectiles, beams, explosions, and any later spec): Hits.BeginWrite(ref state, ProducerKind.X, n) → writer job →
    /// Hits.EndWrite(handle). The single consumer is ShieldSystem (Hits.BeginTick, BeginRead, EndRead).</summary>
    public struct ShieldHitStreams : IComponentData
    {
        public Game.Data.Streams.SimEventChannel<ShieldHit> Hits;
    }
}
```

`ShieldHit` fields after the binding five are additions owned by this spec. `DamageType` is `Game.Data.Combat.DamageType` (designed by SPEC-13, architecture §3.2).

### 4.4 `ShieldMath` (public, static, Burst-compatible, no allocations)

```csharp
namespace Game.Simulation.Power
{
    public static class ShieldMath
    {
        /// Segment p0→p1 (grid space) against the ellipsoid. True if the segment starts outside and enters it;
        /// t = entry fraction in [0,1]. Starting inside never hits (shots fired from inside pass out).
        public static bool SegmentEnters(float3 p0, float3 p1, float3 center, float3 radii, out float t);
        /// True if p (grid space) is inside or on the ellipsoid.
        public static bool Contains(float3 p, float3 center, float3 radii);
        /// Conservative sphere test: true if a sphere (centre c, radius r, grid space) may overlap the ellipsoid.
        public static bool SphereOverlaps(float3 c, float r, float3 center, float3 radii);
        /// Closest point on the ellipsoid surface along the ray from the centre towards p (p ≠ center).
        public static float3 SurfacePointToward(float3 p, float3 center, float3 radii);
        /// Outward unit normal at a surface point.
        public static float3 Normal(float3 surfacePoint, float3 center, float3 radii);
    }
}
```

Algorithms (all in grid space, `inv = 1 / radii`):
- `SegmentEnters`: `a = (p0 − center) * inv`, `b = (p1 − center) * inv`, `d = b − a`. If `dot(a,a) ≤ 1` → false. Solve `|a + t·d|² = 1`: `A = dot(d,d)`, `B = 2·dot(a,d)`, `C = dot(a,a) − 1`. If `A < 1e-12` → false. `disc = B² − 4AC`; `disc < 0` → false. `t = (−B − sqrt(disc)) / (2A)`; return `0 ≤ t ≤ 1`.
- `Contains`: `dot(q,q) ≤ 1` with `q = (p − center) * inv`.
- `SphereOverlaps`: `q = (c − center) * inv`; true if `length(q) ≤ 1 + r / cmin(radii)`.
- `SurfacePointToward`: `q = (p − center) * inv`; `center + (q / length(q)) * radii`.
- `Normal`: `normalize((surfacePoint − center) * inv * inv)`.

**Numerical contract (PWR-002, Q11):** inputs are finite grid-space floats, all radii positive, and sphere radius nonnegative. Evaluate offsets, squared lengths and normalization in double; scale all quadratic coefficients by the same positive factor before evaluating the discriminant so finite float inputs cannot overflow it. Keep the `A < 1e-12` threshold in the original normalized ellipsoid space. Misses write `t = 0`; a discriminant of zero counts as a tangent hit. A centre query, outside the documented ray/surface domains, has a deterministic +X extension: the surface point is `center + (radii.x,0,0)` and the normal is `(1,0,0)`. Results are cast to the public float types; a surface position beyond the representable float range may become infinity, but finite valid inputs never produce NaN. Double intermediates prevent overflow; they do not promise an exact intersection classification for geometry below double precision at extreme coordinate/radius ratios.

**Reference march (PWR-002, Q12):** keep 10,000 seeded cases and 1,000 steps. Construct random crossings with normalized perpendicular offset at most 0.5 and endpoints 2–4 radii along opposite directions; construct misses with perpendicular offset 1.25–2, inside starts within 0.5 radii, and zero-length outside segments. These cases are selected before calling `ShieldMath`; no case is filtered by its output. The crossing interval is wider than a march step. Prescribed tangency, endpoint, inside/boundary-start and zero-length cases are checked directly because fixed samples cannot resolve every arbitrarily short crossing or tangent.

### 4.5 Tuning

```csharp
namespace Game.Data.Definitions
{
    /// <summary>Power and shield tunables. Exactly one asset (`tuning_power`) in the database. Implements SPEC-22 §4.5.</summary>
    [ContentIdPrefix("tuning_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Tuning/Power", fileName = "tuning_power")]
    public sealed class PowerTuningDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private byte priorityShieldEmitter = 200;
        [UnityEngine.SerializeField] private byte priorityThruster = 150;
        [UnityEngine.SerializeField] private byte priorityAbility = 120;
        [UnityEngine.SerializeField] private byte priorityWeapon = 100;
        [UnityEngine.SerializeField, UnityEngine.Min(1)] private int brownoutHoldTicks = 30;
        [UnityEngine.SerializeField, UnityEngine.Min(1f)] private float shieldPaddingScale = 1.2f;     // × half extents
        [UnityEngine.SerializeField, UnityEngine.Min(0f)] private float shieldPaddingMetres = 2f;      // + per axis
        [UnityEngine.SerializeField, UnityEngine.Min(0.5f)] private float shieldMinRadiusMetres = 3f;
        [UnityEngine.SerializeField, UnityEngine.Range(0f, 1f)] private float shieldRestartFraction = 0.25f;
        [UnityEngine.SerializeField] private float shieldMultKinetic = 0.8f, shieldMultEnergy = 1.25f, shieldMultExplosive = 1f, shieldMultRam = 1f;
        [UnityEngine.SerializeField, UnityEngine.Min(1f)] private float sustainWarningSeconds = 30f;
        // getters with the same names in PascalCase
    }
}

namespace Game.Simulation.Power
{
    /// <summary>ECS singleton baked from PowerTuningDefinition by PowerContent. Read-only at runtime.</summary>
    public struct PowerTuning : IComponentData
    {
        public byte PriorityShieldEmitter, PriorityThruster, PriorityAbility, PriorityWeapon;
        public int BrownoutHoldTicks;
        public float ShieldPaddingScale, ShieldPaddingMetres, ShieldMinRadiusMetres, ShieldRestartFraction;
        // Per-type shield multipliers moved to SPEC-13's DamageTableBlob (DamageTypeRow.ShieldMultiplier, review X10).
        public float SustainWarningSeconds;
        public static PowerTuning Defaults => new PowerTuning { PriorityShieldEmitter = 200, PriorityThruster = 150, PriorityAbility = 120,
            PriorityWeapon = 100, BrownoutHoldTicks = 30, ShieldPaddingScale = 1.2f, ShieldPaddingMetres = 2f, ShieldMinRadiusMetres = 3f,
            ShieldRestartFraction = 0.25f, SustainWarningSeconds = 30f };
    }

    /// <summary>Managed baker (SPEC-01 calls it at boot after ContentService.CreateSingletons).</summary>
    public sealed class PowerContent
    {
        public PowerContent(Game.Data.Services.ContentCatalog catalog);   // reads the single PowerTuningDefinition; throws
                                                                          // ContentValidationException if 0 or >1 exist
        public PowerTuning Tuning { get; }
        public void CreateSingletons(EntityManager em);                    // idempotent; one entity "PowerSingletons" with PowerTuning
                                                                          // (ShieldHitStreams is created by ShieldSystem.OnCreate)
        public void DestroySingletons(EntityManager em);
    }
}
```

### 4.6 Spawn-time setup helper

```csharp
namespace Game.Simulation.Power
{
    /// <summary>Root composition of powered grids (ADR-0015). Registered once; GridSpawnSystem adds the types zeroed at conversion.</summary>
    public static class PowerGridSetup
    {
        /// ShipPower, PowerConsumer, ShipStats, ShipStatMultipliers, PowerStats, ShieldStats, ShipStatModifier, ShieldState,
        /// ShieldRipple, ShieldControl (all zero/empty). First-init values (ShieldControl.RequestedOn = 1, 8 ripple elements) are set by
        /// PowerInitJob (§5.2 step 2.5) the first time it initialises a grid.
        public static ComponentTypeSet RootTypes { get; }   // the 10 types above
        /// Appends RootTypes for every combat profile (PlayerShip, NpcShip, Station, SmallCraft, Dummy). Called by PowerRegistrationSystem.OnCreate.
        public static void Register(DynamicBuffer<Game.Voxel.Model.RootComponentRegistration> registrations);
    }
}
```

### 4.7 Ownership summary

| State | Only writer |
|---|---|
| `ShipPower`, `PowerConsumer` per-tick fields, `Powered` | `PowerAllocationSystem` (init values: `PowerInitSystem`) |
| `PowerLink`, `PowerConsumer` table layout, `PowerRequest.ConsumerIndex` | `PowerInitSystem` |
| `PowerRequest` element of module m | The feature that owns m: `Draw`/`Priority` at init, `Requesting` in `PowerDemandGroup` (shield emitters: `PowerInitSystem` and `ShieldPowerDemandSystem`) |
| `ShipStats`, `ShipStatMultipliers`, `PowerStats`, `ShieldStats` | `ShipStatsAggregateSystem` |
| `ShieldState`, `ShieldRipple` | `ShieldSystem` (`PowerInitSystem` only sets `Flags = FillOnNextUpdate` at init) |
| `ShieldControl` | `ShieldToggleEffectSystem` (SPEC-24) |
| `ShipStatModifier`, `Inhibited` | `StatusSystem` (from `StatusChannels`, ADR-0016; producers: SPEC-24 abilities, status and environment features) |
| `ShieldHitStreams.Hits` registrations | Any producer through `Hits.BeginWrite`/`EndWrite` (SPEC-21 today: `ProjectileStepSystem`, `BeamStepSystem`, `ExplosionSystem`); channel owned and read by `ShieldSystem` |

### 4.8 Memory

Per combat grid root: `ShipPower` 28 B, `ShipStats` ≈ 72 B, `ShipStatMultipliers` 64 B, `PowerStats` 20 B, `ShieldStats` 24 B, `ShieldState` 64 B, `ShieldRipple` 8 × 20 B in chunk, `PowerConsumer` 24 B per consumer outside the chunk, `ShipStatModifier` 4 × 8 B. Per consuming functional entity: `PowerLink` 4 B + `Powered` 0 B (enable bit) + `PowerRequest` 2 × 8 B in chunk. A 1M-cell station with 2,000 functional blocks costs ≈ 56 KB. Asteroids and hulks split from ships get none of these (Decision 13).

### 4.9 Energy and shield balance (initial values)

Energy sources and consumers come from SPEC-04 modules (`PowerSource`, `PowerStorage`, `Thruster`, `ShieldEmitter`) and from SPEC-21 `WeaponDefinition.powerDraw`. **This spec retunes `block_shield_small`** (SPEC-04 open question 11): capacity 300 → **3,000**, regen 15/s → **120/s**, delay 3 s → **4 s**, draw 10 → **8 E/s**. Reason: SPEC-21 weapons remove ~1 hull cell (100 HP) per hit, so a 300 HP shield would vanish in one burst.

**Rules (checked by `PowerBudgetValidator`, §5.6, and by `PowerBalanceTests`):**
- R1 Sustain: from full storage, a ship holding every fire group with shields up sustains ≥ 30 s before its first brownout: `Capacity / max(0, WeaponDraw + ShieldDraw + ThrusterDraw − Generation) ≥ 30` (infinite if the denominator is 0).
- R2 Recharge: with weapons idle and shields up, `Capacity / (Generation − ShieldDraw) ≤ 10 s`.
- R3 Shield break time: with the SPEC-21 reference loadout of the same class firing at 60 % hit rate, the shield breaks in 15–50 % of the hull time-to-kill (SPEC-21 §4.9).

Reference ships (legacy blueprints as converted by SPEC-72; weapon draws from SPEC-21 §4.9; core = gen 40, storage 200):

| Ship | Sources | Gen E/s | Cap E | Weapons (count × draw) | WeaponDraw | Shields | Sustain (R1) | Recharge (R2) |
|---|---|---|---|---|---|---|---|---|
| `ship_frigate` | core | 40 | 200 | 4 plasma fixed × 1.5 | 6 | 0 | ∞ | 5 s |
| `ship_fighter` | core | 40 | 200 | 8 plasma fixed × 1.5, 2 plasma medium × 4, 4 beam small × 4 | 36 | 0 | ∞ | 5 s |
| `ship_vestari` | core + 3 capacitors | 40 | 320 | 8 beam small × 4, 1 railgun × 8 | 40 | 0 | ∞ | 8 s |
| `ship_butterfly` | core | 40 | 200 | 2 flak × 5, 6 beam small × 4, 2 missile × 2, 2 plasma medium × 4 | 46 | 0 | 33 s | 5 s |
| `ship_crescent` | core | 40 | 200 | 6 beam small × 4, 14 plasma small × 1.5 | 45 | 0 | 40 s | 5 s |
| fighter + 1 `block_shield_small` | core | 40 | 200 | as fighter | 36 | 8 | 50 s | 6.3 s |

The old prototype's "energy 200 → 1 in ~10 s of autofire" is impossible with these numbers: the worst legacy ship (butterfly) browns out after 33 s, then keeps the weapons the remaining generation can pay for (40 of 46 E/s: about 87 % of its DPS).

## 5. Systems, services and flow

```mermaid
flowchart LR
    subgraph Init[GameInitGroup]
      GS[GridSpawnSystem VOX] --> FI[feature inits: ThrusterInit FLT, WeaponInit WPN] --> PI[PowerInitSystem]
    end
    subgraph Pre[GameplayPrePhysicsGroup]
      SC[ShipCommandGroup] --> PD[PowerDemandGroup: thruster FLT, weapon WPN, shield PWR demand] --> PA[PowerAllocationSystem] --> SA[ShipStatsAggregateSystem] --> FS[feature stats: PropulsionStats FLT, WeaponStats WPN]
    end
    subgraph Post[GameplayPostPhysicsGroup]
      PS[ProjectileStepSystem WPN] --> BS[BeamStepSystem WPN] --> EX[ExplosionSystem WPN] --> SH[ShieldSystem] --> DR[DamageResolveSystem DMG]
    end
    Init --> Pre --> Post
```

Every system: `[BurstCompile]` `ISystem`, `RequireForUpdate<GameplayActive>()`, `RequireForUpdate<PowerTuning>()`, `RequireForUpdate<BlockDefsRef>()`, `RequireForUpdate<BlockModulesRef>()`; lookups created in `OnCreate` and `.Update(ref state)` first in `OnUpdate`; `ProfilerMarker`s named `<System>.<Step>`; no `Complete()`.

### 5.1 Registration (`PowerRegistrationSystem`)

`[UpdateInGroup(typeof(GameInitGroup))] [CreateAfter(typeof(GridSpawnSystem))]`, `OnCreate` only (then `state.Enabled = false`): `PowerRegistration.RegisterConsumer(buffer, BlockModuleId.ShieldEmitter)` (this spec owns shield emitters) and `{ Module = None, Inhibited }` (every functional entity, ADR-0016). Thrusters and weapon mounts are registered by SPEC-20 and SPEC-21 the same way; a later consuming module (a hangar) registers itself. Blocks with only `PowerSource`/`PowerStorage` modules need no component: generation and capacity come from their rows.

**Adding a power consumer (pattern for every spec, ADR-0014):** (1) register with `RegisterConsumer(module)`; (2) in a `GameInitGroup` system `[UpdateAfter(typeof(GridSpawnSystem))] [UpdateBefore(typeof(PowerInitSystem))]`, for new functional entities of the module (`PowerLink.Bound == 0`) call `SetConsumer(requests, module, draw, priority)`; (3) in a system `[UpdateInGroup(typeof(PowerDemandGroup))]` write `SetRequesting(requests, module, …)` every tick from this tick's inputs; (4) gate the feature on `Powered` (enabled = every consumer of the block granted).

### 5.2 `PowerInitSystem`

`[UpdateInGroup(typeof(GameInitGroup))] [UpdateAfter(typeof(GridSpawnSystem))]`. Reads `BlockDefsRef`, `BlockModulesRef`, `PowerTuning`, `VoxelGrid`, `BlockInstance`, `PowerRequest`. Writes `PowerLink`, `PowerConsumer`, `ShipPower`, the shield emitters' `PowerRequest` elements, `ShieldState.Flags`, `ShipStats.Flags`. No structural changes.

0. **Job `ShieldEmitterRequestInitJob`** (`IJobEntity`, `ScheduleParallel`, functional entities with `PowerRequest`, `PowerLink`, `FunctionalBlock`, `[WithChangeFilter(typeof(PowerLink))]`, `PowerLink.Bound == 0`): if the block has a `ShieldEmitter` row → `SetConsumer(requests, ShieldEmitter, row.PowerDraw, PriorityShieldEmitter)`.
1. **Job `PowerRebindDetectJob`** (`IJobEntity`, `.Schedule()` single-threaded, `[WithChangeFilter(typeof(PowerLink))]`): for each functional entity with `PowerLink.Bound == 0`, clear `ShipPowerFlags.Initialized` on `FunctionalBlock.Grid` through a `ComponentLookup<ShipPower>` (skip if the grid has no `ShipPower`). Single-threaded because two entities may share a grid.
2. **Job `PowerInitJob`** (`IJobEntity`, `ScheduleParallel`, roots with `ShipPower`, `WithNone<GridSpawnRequest>`): skip if `Initialized` is set. Else:
   1. Build a `NativeList<PowerConsumer>(Allocator.Temp)`: for instance `i` in index order with `Block != 0` and `Functional` existing with a `PowerRequest` buffer: for each element `r` (index `k`): `{ Functional, InstanceIndex = i, Module = r.Module, Priority = r.Priority, RequestIndex = k, Draw = r.Draw, Flags = None, DeniedUntilTick = 0 }`.
   2. Sort with `NativeSortExtension.Sort` by `(Priority desc, InstanceIndex asc, Module asc)`; copy into the `PowerConsumer` buffer (clear first).
   3. For every consumer at sorted position `j`: `PowerRequest[c.Functional][c.RequestIndex].ConsumerIndex = j`. For every live instance with `Functional != Entity.Null` that has `PowerLink`: write `PowerLink { ConsumerCount = its number of consumers, Bound = 1 }`. *Parallel write through a lookup: each functional entity belongs to exactly one grid and each grid is processed by one worker, so writes are disjoint (comment required above `[NativeDisableParallelForRestriction]`).*
   4. `Capacity = Σ` over alive (`Block != 0 && (Flags & Destroyed) == 0`) instances of their `PowerStorage` rows' `Storage`; `Generation = Σ` their `PowerSource` rows' `Generation`. `ShipPower = { Stored = Capacity, Capacity, Generation, Flags = Initialized }`.
   5. `ShieldState.Flags = FillOnNextUpdate` (all other fields left; `ShieldSystem` fits and fills). **First initialisation of the grid** (`ShieldRipple.Length == 0`): resize `ShieldRipple` to 8 zeroed elements and set `ShieldControl.RequestedOn = 1` (shields start up; later re-inits keep the player's toggle). `ShipStats.Flags &= ~Initialized` (forces a full recompute next tick).
3. Complexity: O(instances of new grids · log). Zero work for existing grids except the change-filtered chunk scan.

### 5.3 `PowerAllocationSystem`

`[UpdateInGroup(typeof(GameplayPrePhysicsGroup))]`, `[UpdateAfter(typeof(PowerDemandGroup))]` (the group itself declares `UpdateBefore` this system; `ShipCommandGroup` is OrderFirst). Reads `PowerRequest` (read-only `BufferLookup`), `PowerStats` (last tick's generation and capacity), `BlockInstance`, `AbilitySlot` (SPEC-24, optional `BufferLookup`), `Hulk`, `GridSpawning`, `SimClock`. Writes `ShipPower`, `PowerConsumer`, `Powered`.

**Job A `PowerAllocateJob`** (`IJobEntity`, `ScheduleParallel` over roots with `ShipPower`, `PowerConsumer`, `PowerStats`; one worker per root):
1. If `!Initialized` or `GridSpawning` enabled → return.
2. If the root has `Hulk`: `Stored = 0, Generation = 0, Capacity = 0`, every consumer `Flags = None` (keep `Alive` bit computed as below), `AbilityGrantMask = 0`, `Flags |= Dead`; return.
3. `tick = SimClock.Tick`, `dt = 1/60`. If `PowerStats.SourceRevision != 0`: `Generation = PowerStats.Generation`, `Capacity = PowerStats.Capacity`, `Stored = min(Stored, Capacity)`.
4. `budget = Stored + Generation × dt`. `demand = 0`, `granted = 0`, `brownout = false`.
5. (No command inspection: demand comes from `PowerRequest`, decision 6.)
6. For each consumer `c` in buffer order (already sorted), process in two passes split at `PriorityAbility`: pass 1 all consumers with `Priority > PriorityAbility`, then step 7, then pass 2 the rest. Per consumer:
   - `alive = inst.Block != 0 && (inst.Flags & Destroyed) == 0 && tick ≥ Inhibited[c.Functional].UntilTick` for `inst = BlockInstance[c.InstanceIndex]` (an inhibited block neither draws nor counts as powered).
   - `requesting = alive && PowerRequest[c.Functional][c.RequestIndex].Requesting == 1` (an entity or element that no longer exists → false).
   - If `requesting`: `demand += c.Draw`. If `tick < c.DeniedUntilTick` → not granted. Else `need = c.Draw × dt`; if `need ≤ budget` → granted, `budget −= need`, `granted += c.Draw`; else not granted, `c.DeniedUntilTick = tick + BrownoutHoldTicks`, `brownout = true`.
   - `c.Flags = (alive ? Alive : 0) | (requesting ? Requesting : 0) | (granted ? Granted : 0) | (tick < c.DeniedUntilTick ? BrownedOut : 0)`.
7. Abilities: `mask = 0`. If the root has an `AbilitySlot` buffer: for slot `s` in index order (`s < 32`) with `State == PendingPower`: `need = slot.PendingCost`; if `need ≤ budget` → `budget −= need`, `mask |= 1u << s`. `AbilityGrantMask = mask`, `AbilityGrantTick = tick`.
8. `Stored = clamp(budget, 0, Capacity)`; `DemandPerSecond = demand`; `GrantedPerSecond = granted`; `Flags` = `Initialized | (brownout ? Brownout : 0)`.

**Job B `PoweredApplyJob`** (`IJobChunk`, `ScheduleParallel` over functional entities with `FunctionalBlock`, `PowerLink`, `Powered`; depends on A): for entity `k`: `g = FunctionalBlock.Grid`; if `g` has no `ShipPower` → enabled = true (non-combat grid, nothing to gate). Else `p = ShipPower[g]`: if `p.Flags` has `Dead` or not `Initialized` → false; else if `ConsumerCount == 0` → `alive(instance) && tick ≥ Inhibited.UntilTick`; else true iff for **every** element `e` of the entity's `PowerRequest` buffer, `c = PowerConsumer[g][e.ConsumerIndex]` has `Alive` and not `BrownedOut`. Write with `chunk.SetComponentEnabled(ref poweredHandle, k, value)` — each chunk belongs to one worker, so enable-bit writes are race-free (traps T2.6).

Complexity: O(consumers + ability slots) per ship, O(functional entities) for job B. Budget §8.

### 5.3b `StatusSystem` (ADR-0016)

`[UpdateInGroup(typeof(GameplayPrePhysicsGroup))] [UpdateAfter(typeof(PowerDemandGroup))] [UpdateBefore(typeof(PowerAllocationSystem))]`. `OnCreate` creates `StatusChannels` (`SimEventChannel<StatModifierRequest>.Create("stat_modifiers")`, `<InhibitRequest>("inhibits")`); `OnDestroy` disposes them. Per tick (`tick = SimClock.Tick`; `BeginTick(tick)` on both):
1. **Inhibits:** `BeginRead` → gather → stable sort by `(Target.Index, Producer, Sequence, gather position)`; one `IJob`: target with `Inhibited` → `UntilTick = max(UntilTick, tick + DurationTicks)`, `Reason`; target that is a grid root → the same for every live instance's `Functional` (via `BlockInstance`); others dropped. Writes through lookups, single-threaded (requests are few).
2. **Stat modifiers:** `BeginRead` → gather → stable sort by `(Root.Index, Producer, SourceId, Stat, Sequence, gather position)`; one `IJob` per tick: for each request on a root with `ShipStatModifier`: find the element with the same `(Producer, SourceId, Stat)`; cancel (`Multiplier == 1` or `UntilTick ≤ tick`) → remove it; else upsert `{Multiplier, UntilTick}`.
3. **Expiry:** `IJobEntity` over roots with a non-empty `ShipStatModifier` (`ScheduleParallel`): remove elements with `tick ≥ UntilTick` (stable removal).
Cost: O(requests + roots with modifiers). Changes to `ShipStatModifier` trip `ShipStatsAggregateSystem`'s `modChanged` filter the same tick.

### 5.4 `ShipStatsAggregateSystem`

`[UpdateInGroup(typeof(GameplayPrePhysicsGroup))] [UpdateAfter(typeof(PowerAllocationSystem))]`. Reads `BlockInstance`, `BlockDefsRef`, `BlockModulesRef`, `VoxelGrid`, `PhysicsMass`, `ShipStatModifier`, `PowerRequest`, `Dormant` (SPEC-11), `Hulk`, `GridSpawning`. Writes `ShipStats`, `ShipStatMultipliers`, `PowerStats`, `ShieldStats`. Feature stats of other specs run after it (`[UpdateAfter(typeof(ShipStatsAggregateSystem))]`): SPEC-20 `PropulsionStatsSystem`, SPEC-21 `WeaponStatsSystem`.

**Job 1 `ShipStatsJob`** (`IJobChunk`, `ScheduleParallel` over roots with `ShipStats`, `ShipStatMultipliers`, `BlockInstance`, `VoxelGrid`; `WithNone<Hulk>`). Per chunk: `instChanged = chunk.DidChange(ref blockInstanceHandle, LastSystemVersion)`, `massChanged = chunk.DidChange(ref physicsMassHandle, …)`, `modChanged = chunk.DidChange(ref modifierHandle, …)`. Per entity:
1. Skip if `GridSpawning` enabled or `Dormant` enabled.
2. `full = !Initialized`. If `!full && instChanged`: compute `sig` over alive functional instances in index order: `count++`, `h = StableHash.Combine(h, (ulong)i)` starting from `h = 0x9E3779B97F4A7C15`; `sig = StableHash.Combine(h, count)`. If `sig ≠ FunctionalSignature` → `full = true`.
3. If `full`: `AliveFunctionalCount` = count of alive functional instances; bounds from `VoxelGrid.CellMin/CellMax` and `CellSize` (§4.2); `FunctionalSignature = sig` (recomputed if step 2 was skipped); `FunctionalRevision++`; set `Initialized`.
4. If `full || massChanged`: if the root has `PhysicsMass` with `InverseMass > 0`: `Mass = 1 / InverseMass`, `HasMass`; if `InitialMass == 0` → `InitialMass = Mass`; `MassFraction = Mass / InitialMass`. Without mass: `MassFraction = 1`.
5. If `full || modChanged`: `ShipStatMultipliers.Values` = for each `StatId` value the product of `Multiplier` over modifier elements with that `Stat` (1 if none).

**Job 2 `PowerStatsJob`** (`IJobEntity`, `ScheduleParallel` over roots with `ShipStats`, `PowerStats`, `ShieldStats`, `BlockInstance`; depends on job 1; `WithNone<Hulk>`): if `PowerStats.SourceRevision == ShipStats.FunctionalRevision` → return. Else zero both and, for each alive instance (`def.Kind == Functional`), for each row the block has (`BlockModulesBlob.TryGet`): `PowerSource` → `Generation += row.Generation`; `PowerStorage` → `Capacity += row.Storage`; `AliveSourceCount++` once per block with either; `ShieldEmitter` → `ShieldStats.Capacity += row.Capacity`, `Regen += row.RegenPerSecond`, `RechargeDelay = AliveEmitterCount == 0 ? row.RechargeDelaySeconds : min(…)`, `Draw += row.PowerDraw`, `AliveEmitterCount++`. `DrawAtFullDemand = Σ` `Draw` of the instance's `PowerRequest` elements (any module). Set both `SourceRevision = FunctionalRevision`.

Complexity: O(1) per untouched ship; O(instances) for ships whose `BlockInstance` chunk changed (signature) and once more per feature when the revision changed. Feature stats are read by others one tick later at worst.

### 5.5 `ShieldSystem`

`[UpdateInGroup(typeof(GameplayPostPhysicsGroup))] [UpdateAfter(typeof(ExplosionSystem))]` (the next system, `DamageResolveSystem`, declares `UpdateAfter(ShieldSystem)`). Reads `ShieldHitStreams`, `PowerConsumer`, `ShipStats`, `ShieldControl`, `LocalTransform`, `PhysicsVelocity`, `Hulk`, `GridSpawning`, `SimClock`, `PowerTuning`, `GridCellReader` (SPEC-10). Writes `ShieldState`, `ShieldRipple`, its own `DamageStreams` producer slot (`ShieldOverflow`, SPEC-13), `FxEventQueue`.

**Job 1 `ShieldHitGatherJob`** (`IJob`): on the main thread `streams.Hits.BeginTick(SimClock.Tick)`, then `set = streams.Hits.BeginRead(ref state)`. `SimEventGather` reads every registration in order, foreach index ascending, into `NativeList<ShieldHit> hits` (`Allocator.TempJob`). Sort by `(Grid.Index, Source.Index, Producer, Sequence, gather position)` (architecture §5.1). After job 2 is scheduled: `streams.Hits.EndRead(job2Handle)`. Build `NativeParallelHashMap<Entity, int2> ranges` (start, count) per grid in one linear pass. Hash map is used for lookup only (traps T1.5).

**Job 2 `ShieldUpdateJob`** (`IJobChunk`, `ScheduleParallel` over roots with `ShieldState`, `ShieldRipple`, `ShipStats`, `PowerConsumer`; depends on job 1). Writes overflow through `DamageStreams.Damage.BeginWrite(ref state, ProducerKind.ShieldOverflow, chunkCount)` at foreach index = `unfilteredChunkIndex`. Per entity, with `tick`, `dt = 1/60`, `T = PowerTuning`:
1. If `GridSpawning` enabled → skip (hits on it are dropped). If `Hulk`: `Hp = 0, MaxHp = 0, Active = 0`; every hit in range → full overflow (step 6 overflow rule with `absorbed = 0`); continue.
2. **Capacity and regen:** iterate consumers with `Module == ShieldEmitter`: `on = (Flags & Alive) && (Flags & Granted)`; `cap += on ? row.Capacity : 0` (`row` = the `ShieldEmitterParams` row of `BlockInstance[c.InstanceIndex].Block`), `regen += on ? row.RegenPerSecond : 0`, `delay = min delay of alive emitters` (default 4 s if none). `MaxHp = cap`; `RegenPerSecond = regen × ShipStatMultipliers.Get(StatId.ShieldRegen)`; `Hp = min(Hp, MaxHp)`.
3. **Shape:** if `ShapeRevision ≠ ShipStats.FunctionalRevision` or `!Initialized`: `half = (BoundsMax − BoundsMin) / 2`; `Radii = max(half × ShieldPaddingScale + ShieldPaddingMetres, ShieldMinRadiusMetres)`; `Center = BoundsCenter`; `BoundingRadius = cmax(Radii)`; `ShapeRevision = FunctionalRevision`; set `Initialized`.
4. **Fill:** if `FillOnNextUpdate`: `Hp = MaxHp`, clear the flag and `Collapsed`.
5. **Up/down:** `requested = ShieldControl.RequestedOn == 1`. If `!requested || MaxHp ≤ 0` → `Active = 0`. Else if `Collapsed` → `Active = Hp ≥ ShieldRestartFraction × MaxHp ? 1 : 0` (clear `Collapsed` when it becomes 1). Else `Active = Hp > 0 ? 1 : 0`.
6. **Hits** (if `ranges` has the grid): for each hit in sorted order: `mult = DamageConfig.Table.Types[(int)Type].ShieldMultiplier` (1 if out of range); `eff = Amount × mult`.
   - If `Active == 1`: `absorbed = min(eff, Hp)`; `Hp −= absorbed`; `RegenResumeTick = tick + round(delay × 60)`; write ripple `RippleHead` = `{ LocalPoint, StartTick = max(tick, 1), Strength = min(1, absorbed / 100) }`, `RippleHead = (RippleHead + 1) & 7`; enqueue `FxEvents.ShieldRipple(worldPoint, worldNormal, PhysicsVelocity.Linear, min(1, absorbed / 100))` (world = root `LocalTransform` applied to `LocalPoint` / `ShieldMath.Normal`). If `Hp ≤ 0`: `Hp = 0`, `Active = 0`, set `Collapsed`.
   - Else `absorbed = 0`.
   - Overflow: `rest = (eff − absorbed) / mult` (raw units). If `rest > 0.001`: point hit (`(Flags & Blast) == 0`): `o = LocalPoint / CellSize`, `VoxelRaycast.Cast(ref GridCellQuery{Reader, Grid}, o, LocalDir, maxCells = 2 × BoundingRadius / CellSize, out hit)`; if hit → write `DamageEvent { Grid, LocalPoint = (o + normalize(LocalDir) × (hit.DistanceCells + 0.05)) × CellSize, LocalDir, Amount = rest, Radius = 0, Penetration, Type, Source, WorldImpulse × rest / Amount, Producer = ShieldOverflow, Instigator }`; if no hit → dropped (the shot would have missed the hull). Blast hit: `DamageEvent { Grid, LocalPoint, LocalDir = 0, Amount = rest, Radius, Penetration = 0, Type, Source, WorldImpulse × rest / Amount, Producer = ShieldOverflow, Instigator }` (the hit's `Instigator` is copied).
7. **Regen:** if `Active == 1 || Collapsed`, `requested`, `tick ≥ RegenResumeTick`: `Hp = min(MaxHp, Hp + RegenPerSecond × dt)`. Then re-run step 5's collapsed check.

Job 2 also disposes nothing: the hits list and map are disposed with `.Dispose(state.Dependency)` after job 2. Complexity: O(H log H) gather with H hits per tick (≤ 4,096 expected), O(emitters) per shielded ship.


### 5.5b `ShieldPowerDemandSystem`

`[UpdateInGroup(typeof(PowerDemandGroup))]`, Burst `IJobEntity` (`ScheduleParallel`) over functional entities with `PowerRequest`, `FunctionalBlock`: `root = FunctionalBlock.Grid`; `PowerRegistration.SetRequesting(requests, ShieldEmitter, ShieldControl[root].RequestedOn == 1)` (no `ShieldControl` → false). The only writer of the shield emitters' `Requesting`.
### 5.6 `PowerBudgetValidator` (editor and boot)

`IContentDatabaseValidator` (SPEC-04 §4.8), `Order = 220`. For every `ShipBlueprintDefinition` (SPEC-10): decode the structure through `BlueprintLibrary`, sum Generation, Capacity, WeaponDraw, ShieldDraw, ThrusterDraw over its functional blocks with the formulas of §5.4, and emit **warning** `PWR-W001` "`<id>`: sustain `<s>` s < 30 s (rule R1)" when R1 fails and `PWR-W002` when R2 fails. Never an error: content may intentionally break the rule (for example a glass-cannon boss).

### 5.7 Hulk transition

When SPEC-13 adds `Hulk`: next `PowerAllocationSystem` tick empties power and clears every `Powered` bit (job B), the next `ShieldSystem` run drops the shield to 0 and converts any remaining hits into hull damage, and `ShipStatsAggregateSystem` stops updating the root (its last stats stay readable). No component is removed.

## 6. Public API

| API | Pre-conditions | Post-conditions / errors |
|---|---|---|
| `PowerGridSetup.Register(buffer)` | Once, from `PowerRegistrationSystem.OnCreate` | Grids converted with a combat profile carry the 10 root types (§4.6), zeroed |
| `PowerContent(catalog)` / `CreateSingletons(em)` | Exactly one `PowerTuningDefinition` | Creates the `PowerSingletons` entity once; second call is a no-op; zero or two tuning assets → `ContentValidationException` with code `PWR-E001` |
| `ShieldMath.*` | Radii all > 0 | Pure; never NaN for finite inputs; `SegmentEnters` false for zero-length segments |
| Readers (HUD, AI, WPN) | — | Read `ShipPower`, `ShipStats`, `ShieldState` fields as documented; values are from the current tick after the writer ran, else from the previous tick |
| `ShieldHitStreams.Hits` producers | `GetSingleton<ShieldHitStreams>()` (read-only); `w = Hits.BeginWrite(ref state, ProducerKind.X, forEachCount)`; schedule the writer job; `Hits.EndWrite(handle)`. Never `GetSingletonRW`, never write fields | Read by `ShieldSystem` this tick if registered before it, else next tick (SPEC-02 §5.13) |

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| Grid has no core, reactor or capacitor | `Generation = Capacity = 0`; consumers with `Draw = 0` (thrusters) are granted; everything else browns out; shield stays down |
| Legacy coreless blueprints (`ship_striker`, `ship_red_elite`, `ship_icarus`, `ship_bastion`, `ship_freighter`) | Same as above: they cannot fire. Open question 3 |
| Consumer's block destroyed (`Flags & Destroyed`) | `Alive` bit clear, never requests, `Powered` disabled next tick, stats drop at next signature check |
| All shield emitters destroyed | `MaxHp = 0`, `Active = 0`, shield shell hidden; incoming hits go to the hull (producers see `Active = 0` next tick; same-tick hits overflow fully) |
| Shield emitter browned out | It contributes no capacity; `Hp` clamps to the new `MaxHp` |
| `ShieldControl.RequestedOn = 0` | `Active = 0`, no draw, no regen; `Hp` kept; on again → up immediately if `Hp ≥ 25 %` of `MaxHp`, else waits for regen |
| Hits on a grid that has no `ShieldState` | Cannot happen (producers only emit `ShieldHit` for grids with `Active == 1`); if it does, ignored |
| A producer did not run this tick | Nothing registered, nothing read. A registration nobody read for a whole tick is disposed at the next `BeginTick` with one warning (SPEC-02 §5.13) |
| `ShipCommand` missing on a combat grid | Treated as zero (no weapon demand, no thrust demand) |
| `PhysicsMass` missing or `InverseMass == 0` (kinematic, static, or not yet created) | `Mass = 0`, accelerations 0, `MassFraction = 1`; stats recompute when it appears (change filter) |
| Rebuild or edit replaces functional entities | `PowerLink.Bound == 0` on the new entities triggers a re-init of that grid in the same frame (§5.2 step 1) |
| More than 32 ability slots | Slots ≥ 32 never get energy (SPEC-24 caps slots at 8) |
| `Stored` would go negative | Impossible: grants only happen when `need ≤ budget`; `Stored` is clamped to `[0, Capacity]` |
| Grid becomes `Hulk` mid-tick | Handled at the next tick per §5.7; the current tick's grants stand |
| Dormant grid | Power and shields update; stats do not |

## 8. Performance budget

| Measure | Budget | How |
|---|---|---|
| `PowerAllocationSystem` (jobs A+B), 30 ships × 60 consumers + 2,000 functional entities | ≤ 0.10 ms total job time, ≤ 0.02 ms main thread | `PowerPerfTests.Allocation_30Ships` |
| `ShipStatsAggregateSystem`, 30 ships under fire (every `BlockInstance` chunk changed, 500 instances each, 1 full recompute per tick) | ≤ 0.10 ms total job time | `PowerPerfTests.StatsAggregate_30ShipsUnderFire` |
| `ShieldSystem`, 30 shielded ships, 2,000 hits per tick | ≤ 0.15 ms total job time | `PowerPerfTests.Shield_2000Hits` |
| GC allocations | 0 B per tick | all perf tests assert `GC.Alloc` count 0 |

These are this spec's share of the skirmish gate (30 ships × 20k cells, main thread ≤ 10 ms median) in [06-performance-budgets.md](../06-performance-budgets.md). Measured with Burst on, safety checks off, 300 measured ticks after 60 warm-up ticks, median and p95 recorded in the ledger log.

## 9. Tasks

### PWR-001 — Public contract types
- **Milestone:** M2
- **Tier:** standard — transcribes the types in §4.1–§4.6 exactly
- **Depends on:** `CORE-001`, `DATA-001`, `VOX-001`, `DMG-001`
- **Do:** Create every type of §4.1–§4.6 with the exact fields, flags and comments; `ShieldMath`, `PowerGridSetup.Register`, `PowerRegistration` and `PowerContent` bodies throw `NotImplementedException("PWR-0xx")` naming the implementing task; `PowerTuning.Defaults` and `ShipStatMultipliers.Get` are real; `PowerDemandGroup` is complete as written. `StatId` goes to `Assets/Game/Data/Combat/StatId.cs` (namespace `Game.Data.Combat`).
- **Files:** `Assets/Game/Simulation/Power/{ShipPowerFlags,ShipPower,PowerConsumerFlags,PowerConsumer,PowerRequest,PowerDemandGroup,PowerRegistration,Powered,PowerLink,ShipStatsFlags,ShipStats,ShipStatMultipliers,PowerStats,ShieldStats,ShipStatModifier,StatModifierRequest,InhibitRequest,Inhibited,StatusChannels,ShieldFlags,ShieldState,ShieldRipple,ShieldControl,ShieldHitFlags,ShieldHit,ShieldHitStreams,ShieldMath,PowerTuning,PowerContent,PowerGridSetup}.cs`; `Assets/Game/Data/Definitions/Power/PowerTuningDefinition.cs`; `Assets/Game/Data/Combat/StatId.cs`
- **Acceptance criteria:**
  - [ ] All files compile in `Game.Simulation` / `Game.Data` with no new assembly reference.
  - [ ] `UnsafeUtility.SizeOf<PowerConsumer>() == 24`, `SizeOf<PowerLink>() == 4`, `SizeOf<PowerRequest>() == 12`, `SizeOf<ShieldRipple>() == 20`.
  - [ ] `PowerTuning.Defaults` equals the `PowerTuningDefinition` field defaults.
- **Tests:** `Assets/Game/Tests/EditMode/Simulation/Power/PowerContractTests.cs`: `Sizes_MatchSpec`, `TuningDefaults_MatchDefinitionDefaults`, `StatId_ValuesAreStable`.

### PWR-002 — Shield maths
- **Milestone:** M2
- **Tier:** standard — exact formulas in §4.4
- **Depends on:** `PWR-001`
- **Do:** Implement `ShieldMath` exactly as §4.4 (Burst-compatible, `math` only).
- **Files:** `Assets/Game/Simulation/Power/ShieldMath.cs`
- **Acceptance criteria:**
  - [x] Segment from (−10,0,0) to (10,0,0) against a unit sphere at origin enters at `t = 0.45`.
  - [x] Segment starting inside never enters; zero-length segment never enters.
  - [x] Ellipsoid radii (4,1,2): `Contains((3.9,0,0))` true, `Contains((0,1.1,0))` false.
  - [x] 10,000 seeded random segments (`RngStreams.For(1, RngPurpose.Test, 0)`) agree with a brute-force 1,000-step march within 1e-3 in `t`.
- **Tests:** `Assets/Game/Tests/EditMode/Simulation/Power/ShieldMathTests.cs`: `SegmentEnters_UnitSphere`, `SegmentEnters_FromInside_False`, `Contains_Ellipsoid`, `SphereOverlaps_Conservative`, `SegmentEnters_MatchesBruteForce`.

### PWR-003 — Tuning definition, baker and spawn helper
- **Milestone:** M2
- **Tier:** standard — field-by-field definition plus a small baker
- **Depends on:** `PWR-001`, `SPEC-04`
- **Do:** Implement `PowerTuningDefinition` getters and `Validate` (all fields within their attribute ranges; `PWR-E002` otherwise), `PowerContent` (§4.5, §6), `PowerGridSetup.RootTypes`/`Register` (§4.6, ADR-0015). Author the asset `Assets/Game/Content/Tuning/tuning_power.asset` with the defaults and add it to the database through the SPEC-04 builder.
- **Files:** `Assets/Game/Data/Definitions/Power/PowerTuningDefinition.cs`; `Assets/Game/Simulation/Power/{PowerContent,PowerGridSetup}.cs`; `Assets/Game/Content/Tuning/tuning_power.asset`
- **Acceptance criteria:**
  - [ ] `PowerContent` with zero or two tuning assets throws `ContentValidationException` containing `PWR-E001`.
  - [ ] `CreateSingletons` twice creates one entity holding `PowerTuning`.
  - [ ] `Register` appends the 10 types for each of the five combat profiles and nothing for `None`.
- **Tests:** `Assets/Game/Tests/EditMode/Simulation/Power/PowerContentTests.cs`: `NoTuning_Throws`, `TwoTunings_Throws`, `CreateSingletons_Idempotent`, `GridSetup_RegistersForCombatProfiles`.

### PWR-004 — Registration and `PowerInitSystem`
- **Milestone:** M2
- **Tier:** frontier — F1 foundation of every combat grid; F2 parallel lookup writes
- **Depends on:** `PWR-003`, `WPN-001`, `VOX-001`, `SPEC-10`
- **Do:** Implement `PowerRegistration` (§4.1), `PowerRegistrationSystem` (§5.1, also calling `PowerGridSetup.Register`) and `PowerInitSystem` (§5.2) with its three jobs, including the first-init values of step 2.5. Tests use a test-only `TestConsumerInitSystem` (GameInitGroup, before `PowerInitSystem`) that fills `Thruster` and `WeaponMount` elements from their rows, so this task does not wait for SPEC-20/21.
- **Files:** `Assets/Game/Simulation/Power/{PowerRegistration,PowerRegistrationSystem,PowerInitSystem,ShieldEmitterRequestInitJob,PowerRebindDetectJob,PowerInitJob}.cs`; `Assets/Game/Tests/EditMode/Simulation/Power/TestConsumerInitSystem.cs`
- **Acceptance criteria:**
  - [ ] Spawning `ship_fighter` (EcsTestWorld + `GridSpawnSystem`) yields 14 weapon, 14 thruster and 0 shield consumers sorted by (priority desc, instance asc); every consuming functional entity has `PowerLink.Bound == 1`; `Stored == Capacity == 200`, `Generation == 40`.
  - [ ] A `GridRebuildRequest` with a structure that adds one shield emitter re-initialises the grid in the same frame (29 consumers: 14 weapons + 14 thrusters + 1 shield, shield first; Q14).
  - [ ] Grids without `ShipPower` are ignored; non-consumer functional entities have no `PowerRequest` and `ConsumerCount == 0`; every bound element's `ConsumerIndex` points at a consumer with the same `Functional` and `Module`.
  - [ ] A functional entity with two consuming modules yields two bound consumers on that entity. The `Powered` all-modules-not-browned-out behavior is implemented and verified by PWR-005, its sole writer (§5.3; Q14).
  - [ ] No structural change outside `OnCreate`; leak detection clean.
- **Tests:** `Assets/Game/Tests/EditMode/Simulation/Power/PowerInitSystemTests.cs`: `Spawn_BuildsSortedConsumers`, `Spawn_SetsFullStorage`, `Rebuild_Reinitialises`, `NoShipPower_Ignored`, `NonConsumer_IndexMinusOne`.

### PWR-005 — `PowerAllocationSystem`
- **Milestone:** M2
- **Tier:** frontier — F2 enable bits from parallel jobs; F4 budgeted hot path
- **Depends on:** `PWR-004`, `ABL-001`
- **Do:** Implement jobs A and B exactly as §5.3, including the two-pass split for ability costs and the hulk branch. Tests drive demand by setting `PowerRequest.Requesting` directly.
- **Files:** `Assets/Game/Simulation/Power/{PowerAllocationSystem,PowerAllocateJob,PoweredApplyJob}.cs`
- **Acceptance criteria:**
  - [ ] Gen 40, cap 200, one weapon group held with draw 46 E/s: first brownout at tick 1,980 ± 1 (33 s); afterwards the lowest-priority, highest-index weapons stay off for ≥ 30 ticks at a time.
  - [ ] Shield emitters keep `Granted` while weapons brown out.
  - [ ] An ability slot in `PendingPower` with cost 20 and `Stored ≥ 20` sets its bit in `AbilityGrantMask` with `AbilityGrantTick == Tick`; with `Stored = 10` and generation 0 the bit stays 0.
  - [ ] `Hulk` → `Stored = 0`, every `Powered` bit disabled the same tick.
  - [ ] Results identical when the same scenario runs with 1 and with 8 worker threads (`JobsUtility.JobWorkerCount`).
- **Tests:** `Assets/Game/Tests/EditMode/Simulation/Power/PowerAllocationSystemTests.cs`: `Brownout_AfterSustainTime`, `Brownout_HoldsThirtyTicks`, `Shields_OutlastWeapons`, `AbilityCost_GrantedOrDenied`, `Hulk_ClearsPower`, `Deterministic_AcrossWorkerCounts`.

### PWR-006 — `ShipStatsAggregateSystem`
- **Milestone:** M2
- **Tier:** frontier — F3 change-filter/signature logic silently goes stale if wrong; F4 hot path
- **Depends on:** `PWR-004`, `SPEC-12`
- **Do:** Implement §5.4 (job 1 with `DidChange` filters, signature, mass and multiplier parts; job 2 `PowerStatsJob`). Propulsion and weapon totals are not computed here (SPEC-20 `PropulsionStatsSystem`, SPEC-21 `WeaponStatsSystem`, ADR-0014).
- **Files:** `Assets/Game/Simulation/Power/{ShipStatsAggregateSystem,ShipStatsJob,PowerStatsJob}.cs`
- **Acceptance criteria:**
  - [ ] `ship_fighter` after spawn: `PowerStats.Generation == 40`, `PowerStats.Capacity == 200`, `AliveFunctionalCount` equals its functional block count, `FunctionalRevision == 1`, `PowerStats.SourceRevision == 1`.
  - [ ] Destroying one weapon (set `BlockInstance.Flags |= Destroyed` in the test) increments `FunctionalRevision` once and recomputes `PowerStats`; damaging hull cells only (no functional change) does not increment it.
  - [ ] Changing `PhysicsMass` updates `Mass` and `MassFraction` without a revision change.
  - [ ] A `ShipStatModifier { Stat = Thrust, Multiplier = 3, UntilTick = max }` makes `ShipStatMultipliers.Get(StatId.Thrust) == 3`; other stats stay 1.
  - [ ] Dormant ships are not updated; hulks are not updated.
- **Tests:** `Assets/Game/Tests/EditMode/Simulation/Power/ShipStatsAggregateSystemTests.cs`: `Spawn_FullRecompute`, `WeaponDestroyed_IncrementsRevision`, `HullDamageOnly_NoRecompute`, `MassChange_CheapUpdateOnly`, `Modifier_AppliesMultiplier`, `PowerStats_FollowRevision`, `Dormant_Skipped`, `Hulk_Skipped`.

### PWR-007 — `ShieldSystem`
- **Milestone:** M2
- **Tier:** frontier — F2 NativeStream consumer and sorted per-grid parallel apply; F3 ordering
- **Depends on:** `PWR-005`, `PWR-006`, `PWR-002`, `DMG-001`, `VFX-001`, `SPEC-02`
- **Do:** Create the `ShieldHitStreams` singleton with its `SimEventChannel<ShieldHit>` (SPEC-02, not re-implemented) in `ShieldSystem.OnCreate` and dispose it in `OnDestroy`; implement `ShieldPowerDemandSystem` (§5.5b), §5.5 jobs 1 and 2, ripple ring, FX events, overflow `DamageEvent`s through SPEC-13's `ShieldOverflow` producer slot, hulk branch.
- **Files:** `Assets/Game/Simulation/Power/{ShieldSystem,ShieldHitGatherJob,ShieldUpdateJob,ShieldPowerDemandSystem}.cs`
- **Acceptance criteria:**
  - [ ] Fresh ship with one emitter: `MaxHp == 3000`, `Hp == 3000`, `Active == 1` after the first update; radii ≥ 3 m and contain every bound corner.
  - [ ] 10 Energy hits of 300: shield absorbs `10 × 375 = 3750` → collapses on the 8th hit; overflow of the 8th and the full 9th/10th hits become `DamageEvent`s whose `LocalPoint` lies inside a solid cell's bounds (± 0.05 cell).
  - [ ] Regen starts exactly 240 ticks after the last hit at 120 HP/s; shield re-activates at 750 HP.
  - [ ] Ripple ring wraps after 8 hits; `StartTick` never 0 for a written slot.
  - [ ] Same hit set fed in shuffled stream order produces bit-identical `ShieldState` and identical `DamageEvent` sequences.
  - [ ] Hulk: any hit becomes full overflow; `Active == 0`.
- **Tests:** `Assets/Game/Tests/EditMode/Simulation/Power/ShieldSystemTests.cs`: `Init_FillsAndFits`, `Hits_CollapseAndOverflow`, `Overflow_HitsHullCell`, `Regen_AfterDelay`, `Restart_AtQuarter`, `RippleRing_Wraps`, `Order_IndependentOfStreamOrder`, `Hulk_AllOverflow`, `ToggleOff_NoDrawNoBlock`.

### PWR-010 — `StatusSystem`: inhibits and stat modifiers (ADR-0016)
- **Milestone:** M2
- **Tier:** standard — two sorted channel consumers and an expiry job, all steps in §5.3b
- **Depends on:** `PWR-001`, `PWR-005`, `SPEC-02`
- **Do:** Implement §5.3b, the `Inhibited` registration (§5.1) and the inhibit checks in `PowerAllocateJob`/`PoweredApplyJob` (§5.3).
- **Files:** `Assets/Game/Simulation/Power/{StatusSystem,PowerAllocateJob,PoweredApplyJob,PowerRegistrationSystem}.cs`, `Assets/Game/Tests/EditMode/Simulation/Power/StatusSystemTests.cs`
- **Acceptance criteria:**
  - [ ] An `InhibitRequest` of 120 ticks on a weapon entity disables its `Powered` bit for exactly 120 ticks and removes its draw; on a grid root it inhibits every block.
  - [ ] Two modifier requests with the same key keep only the later one; a cancel removes it; an entry with `UntilTick = tick + 60` disappears at that tick; `ShipStatMultipliers` follows in the same tick.
  - [ ] Results do not depend on producer system order or worker count.
- **Tests:** `StatusSystemTests.Inhibit_Entity`, `.Inhibit_Root`, `.Modifier_Upsert`, `.Modifier_Cancel`, `.Modifier_Expires`, `.Deterministic`.

### PWR-008 — Power budget validator and balance tests
- **Milestone:** M2
- **Tier:** standard — rules and numbers are given in §4.9 and §5.6
- **Depends on:** `PWR-006`, `SPEC-72`, `WPN-001`
- **Do:** Implement `PowerBudgetValidator` (§5.6). Update `block_shield_small` values per §4.9 (through SPEC-04/SPEC-72's value table owner; see open question 1). Write `PowerBalanceTests` computing R1/R2 for the reference ships of §4.9 from the converted blueprints and asserting the table values (± 1 s).
- **Files:** `Assets/Game/Editor/Content/PowerBudgetValidator.cs`; `Assets/Game/Tests/EditMode/Simulation/Power/PowerBalanceTests.cs`
- **Acceptance criteria:**
  - [ ] Validator warns `PWR-W001` for a test blueprint with draw 100 E/s and gen 40; no warning for `ship_fighter`.
  - [ ] Every row of the §4.9 table is reproduced within ± 1 s.
- **Tests:** `PowerBudgetValidatorTests.HighDraw_Warns`, `.Fighter_NoWarning`; `PowerBalanceTests.ReferenceShips_MatchTable`.

### PWR-009 — Performance tests
- **Milestone:** M2
- **Tier:** frontier — F4 owns perf gates
- **Depends on:** `PWR-005`, `PWR-006`, `PWR-007`
- **Do:** Implement the three perf tests of §8 in `Game.Tests.PlayMode` (category `Performance`) with seeded content (`BenchmarkStructures` from SPEC-10 plus SPEC-21 weapons); record medians and p95 in the ledger log.
- **Files:** `Assets/Game/Tests/PlayMode/Combat/PowerPerfTests.cs`
- **Acceptance criteria:**
  - [ ] All three budgets in §8 met; 0 GC allocations per tick.
- **Tests:** `PowerPerfTests.Allocation_30Ships`, `.StatsAggregate_30ShipsUnderFire`, `.Shield_2000Hits`.

## 10. Open questions

| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | Retune `block_shield_small` (SPEC-04 §4.10 row) to capacity 3,000, regen 120/s, delay 4 s, draw 8 E/s | Yes; SPEC-04 updates its value table and SPEC-72 writes these values | SPEC-04 / owner |
| 2 | Who calls `PowerGridSetup.AddComponents` (and SPEC-21's `WeaponGridSetup`, SPEC-24's `AbilityGridSetup`) | **Superseded by ADR-0015:** nobody; the types come from the grid profile (`Register`), and spawners choose the profile (SPEC-01/31/33) | SPEC-01 / SPEC-31 |
| 3 | Legacy coreless blueprints have no power | Keep: they cannot fire; encounter content must not use them as combatants until SPEC-72 or the owner adds a core | Owner / SPEC-72 |
| 4 | Thruster power draw (SPEC-04 OQ 4) | 0 E/s; thrusters are still consumers (always granted) so a later non-zero draw works without code changes | SPEC-20 |
| 5 | Does SPEC-20's `PropulsionSystem` read the `Thrust`, `MaxSpeed`, `TurnRate` multipliers (`ShipStatMultipliers.Get`, ADR-0014) and skip thrusters whose `Powered` bit is off | Yes (needed for the speed boost ability and brownouts) | SPEC-20 |
| 6 | SPEC-13 `DamageStreams` producer slot for shield overflow | SPEC-13 provides a producer kind `ShieldOverflow` with a per-tick `NativeStream` owned by `ShieldSystem` (same owner-allocates pattern as `ShieldHitStreams`) | SPEC-13 |
| 7 | Should energy and shield HP be saved | No (Decision 12) | Owner |
| 8 | Should a ship's `InitialMass` survive save/load so `MassFraction` stays meaningful after loading a damaged ship | No for M3: after load `InitialMass` = loaded mass; revisit when SPEC-05 saves ship damage | SPEC-05 |
| 9 | `ShipStats.MassFraction` as the AI/HUD hull-health measure | Yes (cheap, maintained incrementally by SPEC-12) | SPEC-23 / SPEC-61 |
| 10 | PWR-001's `PowerDemandGroup.UpdateBefore(PowerAllocationSystem)` names the PWR-005 system before it exists | Defer the redundant group attribute until PWR-005; its already specified `UpdateAfter(PowerDemandGroup)` preserves the same ordering edge. Keep a PWR-005 TODO; do not introduce an allocator stub | Tech lead |
| 11 | The exact float formulas can overflow or divide by zero despite §6's finite-input no-NaN guarantee | Use double offsets/normalization and a uniformly scaled quadratic; preserve the original 1e-12 threshold and tangent rule, write t=0 on misses and use a deterministic +X centre-query extension. Float output range and extreme-ratio precision limits are explicit in §4.4 | Tech lead |
| 12 | A 1,000-step march can miss grazing crossings and tangencies | Select the 10,000 seeded random cases independently in the resolvable domains documented in §4.4; check prescribed boundary cases directly. Never filter by implementation outcome | Tech lead |
| 13 | Minimum-only float attributes do not exclude infinity, and `shieldMult*` fields remain on the authoring definition after their runtime ownership moved to SPEC-13. | Require finite values for the five float tunables carrying Min/Range attributes, in addition to their stated ranges; byte priorities fit their storage range inherently. Preserve legacy `shieldMult*` fields/getters without inventing ranges or baking them into PowerTuning; DamageTableBlob remains the multiplier source. | Tech lead — resolved by PWR-003; central mirror pending. |

| 14 | PWR-004 lists 28 base consumers but 15 after adding a shield, requires links on non-consumers despite §5.1, and includes PWR-005-owned Powered writes | Use 29 after the one-shield rebuild, bind only consuming functional entities, and verify two module bindings in the existing spawn test. Powered enable bits remain solely PWR-005 behavior; registration/init never writes them. Keep the five required test methods and exact initial energy values. | Tech lead |

## 11. References

- Audit: `docs/reference/audit-2026-10/02-combat-destruction.md` C08 (parallel energy races), C14 (shields never exist), C22 (beam double drain), C33 (enable bit overloading), §5.10 (beams, shields, energy); `LEDGER.md` R21 (energy 200 → 1 in ~10 s).
- `00-synthesis.md` §6f (power, shields).
- Unity: `IJobChunk` with `DidChange`, `ArchetypeChunk.SetComponentEnabled`, `NativeStream`, Unity Physics `PhysicsMass`.
