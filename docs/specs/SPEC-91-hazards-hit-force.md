# SPEC-91 — Hit force and world hazards

| | |
|---|---|
| **Status** | Ready (2026-10-16). Amended 2026-10-17 for the owner's mechanics review ([ADR-0119](../decisions/ADR-0119-world-mechanisms.md)), amendment waiting for owner review. Design: [ADR-0118](../decisions/ADR-0118-hit-force-hazards-tracks.md) |
| **Area code** | HAZ (task IDs are `HAZ-001`, `HAZ-002`, …) |
| **Module(s)** | `Game.Simulation` (`Simulation/Hazards/`, `Simulation/Mechanisms/` from the amendment, plus changes in `Flight/` and `Weapons/`), `Game.Voxel` (`ExternalPush`), `Game.Data` (definitions, module rows), `Game.Net` (replication), `Game.Presentation` (feedback), `Game.App` (playground pad) |
| **Depends on specs** | SPEC-13 (impulse channel, explosions), SPEC-20 (flight assist), SPEC-21 (weapons), SPEC-04 (block modules), SPEC-10 (grids), SPEC-23 (`SpatialHash`), SPEC-90 and ADR-0117 (co-op roles) |
| **Depended on by** | SPEC-92 (race tracks use zones, walls and mines); encounters and star-system locations later |
| **Milestone** | M4 (owner-directed track, 2026-10-16; ADR-0118 Consequences) |

> Read [01-architecture.md](../01-architecture.md), [02-conventions.md](../02-conventions.md) and [03-glossary.md](../03-glossary.md) before this spec. This spec may not contradict them; ADR-0118 records the contract changes it makes.

## 1. Purpose
Combat should have weight. A hit shoves the ship it lands on, by the shot's momentum against the target's mass, and every shot kicks the shooter back a little. A light racer struck by a railgun slug is thrown off its line and set spinning; a cruiser shrugs it off. Players can use that on purpose to redirect a rival, an obstacle or themselves.

The same pass adds the world's first hazards, built as foundations for the whole game rather than for one minigame:
- **zones**, in five kinds: boost sections, slow fields, force walls, gates and bounds;
- **mines**, which are small voxel devices that arm, sense, detonate and chain-react.

SPEC-92 builds race tracks from these pieces. Encounters and star-system locations will place the same pieces later.

## 2. Scope
**In scope:**
- shot momentum, knockback and recoil;
- carried velocity in flight assist;
- retuning all weapons against feel targets;
- impact feedback (camera shake, sparks);
- `WorldZone` with five effect kinds;
- field walls along polylines;
- the mine device: fuse module, two mine blueprints, anchor spring, cook-off and feedback;
- `HazardSpawn` for zones and mine fields;
- a playground pad to try all of it;
- co-op replication and parity for all of it.

**Out of scope (and who owns it instead):**
- race rules, tracks and racer ships: SPEC-92;
- ships laying mines (an ability or weapon): a later SPEC-24 or SPEC-21 task, which will use `HazardSpawn` from this spec;
- zones that deal damage over time (radiation, plasma clouds): later, as a sixth effect kind;
- AI that avoids zones and mines: later SPEC-23 work (open question 7);
- predicting a guest's own fire decisions: a separate co-op decision (open question 6).

## 3. Decisions (binding)
1. **ADR-0118 decisions 1–8 and 12** apply as written. Reason: they are this spec's contract changes.
2. **Feel targets drive tuning, not physical realism.** `projectileMass` is a design value. The tuning pass (HAZ-005) hits the §8 Δv targets on reference ships, and the masses follow from them. Reason: the owner asked for "umph" and control, not a ballistics simulator.
3. **Carry never adds speed the ship does not already have.** Each tick each carry axis is clamped to the ship's measured local velocity on that axis: same sign, no larger. Reason: after a crash into a wall, assist must not drive the ship back into it.
4. **Recoil is applied at the muzzle with torque.** Recoil from a centred spinal gun pushes straight back. Recoil from a wing turret also yaws the ship. Reason: the owner asked for recoil that is physical, and asymmetric mounts should matter (pillar 2).
5. **Zones never write cells or velocity directly.** Pushes go through `DamageStreams.Impulses` with `ProducerKind.Environment`, damage through a registered damage stream, and gate and bounds results through `ZoneContacts`. Reason: one writer per piece of state (architecture §8).
6. **A zone's push is applied at the centre of mass** (`ImpulseRequestFlags.AtCenterOfMass`). The exception is `Repel`, which pushes at the contact point, so a glancing wall hit turns the ship. Reason: boost sections should not spin ships, but walls should feel like walls.
7. **Mines are voxel devices** (ADR-0118 decision 7). They are shootable, pushable and replicated through the grid stream. Reason: mines must feel like the rest of the world.
8. **A mine triggers on ship roots only, by default on any ship after arming.** That includes the ship that laid it and its team. Debris, chunk bodies, hulks, devices and projectiles never trigger a mine. Reason: debris storms would set off whole fields, and an armed mine is everyone's problem (open question 3).
9. **Determinism.** Zone and mine processing is ordered by `(zone or mine PersistentId, body Entity.Index)`. Random scatter uses `RngStreams.For(seed, RngPurpose.HazardScatter, fieldIndex)`. No `UnityEngine.Random`. Reason: architecture §1.
10. **Co-op roles** (ADR-0117 decision 2):
    - on every machine: `ZoneSystem` pushes, the `MineAnchorJob` spring and `HazardFeedbackSystem`;
    - on the authority only: `ZoneSystem`'s damage and gate and bounds outputs, and `MineSystem`'s arm, trigger and detonate decisions.

    `RoleSystemPolicy` lists each entry with its decision.
11. **The wider mechanism set (amendment, owner 2026-10-17; ADR-0119).** The owner kept nine mechanics and cut the grapple: "i love all the mechanics except the grapple pylon (lets scratch that), and the "places that change" is less of a mechanic and more a design consideration using the other mechanics".
    - The mechanics: gravity wells, moving structures, blast doors and switches, engine wash, the mine family, gauntlet turrets, pulses with shadows (flares and reactors), lane-gate portals and energy canisters.
    - Every one is a general world system, used in exploration first and on race tracks second.
    - "Places that change" is a design rule: a place lists its changes as mechanism rules (ADR-0119 decision 9).

12. **Implementation choices (HAZ-002–008, 2026-10-17).** These differ from §4–§5 as written:
    - **Zone pose.** A zone's live pose is its `LocalTransform`, which `OriginRebaseSystem` already shifts, and path points are relative to it. `WorldZone.Position` is the spawn pose.
    - **Gate tracking.** `ZoneSystem` keeps each body's last centre of mass itself, not in a `ZoneTracking` component. A jump over 50 m in one tick (rebase, respawn, portal) skips that tick's crossings.
    - **Momentum.** `impulsePerHit` stays as the fallback for weapons with no projectile mass or beam force. The blob's `ImpulsePerHit` is the applied value (`HitMomentum × KnockbackScale`), so there is no asset migration.
    - **Mines.** `MineDefinition` carries the `ProximityFuseParams`, and the blueprint is the mine's body. A mine cooks off when it loses any cell. `MineAnchorSystem` follows rebases with the same 50 m jump rule.

## 4. Data model

### 4.1 Hit force (changes to existing types)
```csharp
// Game.Data.Definitions.Weapons.WeaponDefinition — impulsePerHit is removed (migrated by HAZ-003)
[SerializeField, Min(0)] private float projectileMass;   // kg per pellet; momentum = projectileMass × projectileSpeed
[SerializeField, Min(0)] private float beamForce;        // N while a beam pulse is on target (beams only)

// Game.Data.Definitions.Weapons.WeaponTuningDefinition — new global scales
[SerializeField, Min(0)] private float knockbackScale = 1f;  // × momentum → target impulse
[SerializeField, Min(0)] private float recoilScale = 1f;     // × momentum → shooter impulse
// explosionImpulsePerDamage stays; retuned in HAZ-005

// Game.Simulation.Weapons.WeaponDef (blob row) — ImpulsePerHit replaced by
public float HitMomentum;   // N·s per pellet (projectiles) or per pulse (beams), before scales
public float RecoilMomentum;// N·s per pellet at the muzzle (0 for beams)

// Game.Data.Combat.ProducerKind — appended
Recoil = 11, Hazard = 12
```

### 4.2 Carried velocity (public contract)
```csharp
// Game.Voxel.Damage — written only by ImpulseApplySystem; on every grid root (DamageSetup.GridRootComponents)
public struct ExternalPush : IComponentData
{
    public float3 LinearDv;   // world m/s applied by impulses this tick (sum)
    public float YawDv;       // rad/s about +Y applied this tick
    public uint Tick;         // tick it was applied in
}

// Game.Simulation.Flight.FlightState — two fields added (writer PropulsionSystem)
public float2 Carry;          // ship-local planar m/s added to assist targets (x = strafe, y = forward)
public float CarryYaw;        // rad/s added to the yaw-rate target

// Game.Data.Definitions.Flight.FlightConfigDefinition — new fields (config_flight)
carryDecel = 8;           // m/s² carry bleeds toward zero
carryBrakeDecel = 30;     // m/s² while Brake is held
carryYawDecel = 2.1f;     // rad/s² (≈120°/s²)
maxCarry = 120;           // m/s, clamp per axis
maxCarryYaw = 3.5f;       // rad/s
```

### 4.3 Zones (public contract, `Game.Simulation.Hazards`)
`ZoneShape`, `ZoneEffectKind` and `ZoneEffectFlags` live in `Game.Data.Hazards` (HAZ-001), because the authoring definitions in `Game.Data` need them and Data may not reference Simulation.
```csharp
public enum ZoneShape : byte { Box = 0, Circle = 1, Path = 2 }          // Path = capsule chain along a polyline
public enum ZoneEffectKind : byte { Accelerate = 0, Drag = 1, Repel = 2, Gate = 3, Bounds = 4 }   // append only

public struct WorldZone : IComponentData
{
    public ZoneShape Shape;
    public float2 HalfExtents;  // Box: half width (local x) and half length (local z); Circle: x = radius
    public float PathRadius;    // Path: capsule radius
    public float Yaw;           // radians; zone forward = local +Z
    public float2 Position;     // local-frame XZ (shifted by OriginRebaseSystem like other roots)
    public ulong PersistentId;  // sort key and replication id
    public ushort Group;        // owner group (a track, an encounter); 0 = none
}
[InternalBufferCapacity(0)] public struct ZonePathPoint : IBufferElementData { public float2 Point; }  // Path only, ≥ 2

[InternalBufferCapacity(1)]
public struct ZoneEffect : IBufferElementData
{
    public ZoneEffectKind Kind;
    public float A, B, C;      // Accelerate: A = target speed m/s, B = accel m/s²
                               // Drag: A = drag per second (fraction of speed), B = min speed m/s
                               // Repel: A = stiffness 1/s², B = damping 1/s, C = contact HP per m/s of approach
                               // Gate: A = direction sign (+1, −1 or 0 = both); Bounds: none (inside = in bounds)
    public ushort Id;          // Gate: gate id; Bounds: bounds id; otherwise 0
    public byte Flags;         // ZoneEffectFlags: AffectsShips = 1, AffectsDebris = 2, AffectsDevices = 4
}

// Output of ZoneSystem for deciders (authority): one NativeList per tick in ZoneContacts singleton
public struct ZoneContact { public Entity Body; public ulong Zone; public ZoneEffectKind Kind; public ushort Id; public sbyte Crossed; public byte Inside; }
public struct ZoneContacts : IComponentData { /* NativeList<ZoneContact> Current; written by ZoneSystem, read by deciders after it */ }
```

### 4.4 Mines (public contract)
```csharp
// Game.Voxel.Model.GridKind — appended
Device = 4

// Game.Data.Blocks.BlockModuleId — appended
ProximityFuse = 10

// Game.Data.Blocks.Modules.ProximityFuseModule (row in BlockModulesBlob)
public struct ProximityFuseParams
{
    public float ArmSeconds;        // after spawn or after re-arm
    public float TriggerRadius;     // m from the fuse block centre to a ship's nearest bounding sphere surface
    public float FuseSeconds;       // trigger → detonation (blink fast meanwhile)
    public float BlastRadius;       // m
    public float BlastDamage;       // at centre
    public DamageType BlastType;    // Explosive by default
    public float MinTriggerMass;    // kg; lighter roots never trigger
    public byte TriggerFilter;      // 0 = any ship, 1 = hostile to the mine's allegiance only
}

// Game.Simulation.Hazards — on the device root; written only by MineSystem (authority), replicated
public enum MinePhase : byte { Arming = 0, Armed = 1, Triggered = 2, Spent = 3 }
public struct MineState : IComponentData { public MinePhase Phase; public uint PhaseTick; public Entity TriggeredBy; }

// Optional anchor spring; read by MineAnchorJob (every machine)
public struct MineAnchor : IComponentData { public float2 Point; public float Stiffness; public float Damping; public float MaxAccel; }

// Content
// MineDefinition (ContentDefinition): blueprint (ShipBlueprintDefinition with a ProximityFuse block), anchor defaults, radar icon
// MineFieldDefinition (ContentDefinition): mine, count, area shape (box/circle/path band), min spacing, anchored
```

### 4.5 Spawning (public contract)
```csharp
// Game.Simulation.Hazards — definitions for authoring zones
public sealed class ZoneDefinition : ContentDefinition  // shape, size, effects; placed with a pose
// HazardSpawn (static, managed + ECS): creates zone entities and mine-field GridSpawnRequests
public static class HazardSpawn
{
    public static Entity Zone(EntityManager em, ZoneDefinition def, float2 position, float yaw, ushort group, ulong persistentId);
    public static Entity PathZone(EntityManager em, ZoneDefinition def, ReadOnlySpan<float2> points, ushort group, ulong persistentId);
    public static int MineField(EntityManager em, MineFieldDefinition def, float2 position, float yaw, ulong seed, ushort group,
                                IPersistentIdAllocator ids, Span<Entity> outRequests);   // returns mines requested
    public static void DespawnGroup(EntityManager em, ushort group);   // zones + devices of a group
}
```

### 4.6 The mechanism set (amendment, ADR-0119)
```csharp
// Game.Data.Hazards.ZoneEffectKind — appended
Attract = 5,   // A = GM (m³/s²), B = minimum radius m (softening)
Pulse = 6      // A = damage per pulse, B = shield drain per pulse, C = source: 0 = point at the zone centre, 1 = direction (the zone yaw)
// Game.Data.Blocks.BlockModuleId — appended
Switch = 11, Pickup = 12

// Game.Simulation.Hazards — zones switch on and off (any kind); written by ZoneSystem from the clock and by mechanism actions
public struct ZoneSchedule : IComponentData { public uint PeriodTicks, OnTicks, PhaseTicks; public byte Enabled; }

// Game.Data.Blocks.ProximityFuseParams — appended fields
public MinePayload Payload;     // Blast (default), Repulse, Emp, Gravity, None
public float PayloadStrength;   // Repulse: N·s at the centre; Emp: seconds of thruster inhibit; Gravity: GM for PayloadSeconds
public float PayloadSeconds;    // Gravity: lifetime of the temporary Attract zone
public float SeekSpeed;         // m/s; > 0 makes an armed mine drift toward its nearest valid target
public enum MinePayload : byte { Blast = 0, Repulse = 1, Emp = 2, Gravity = 3, None = 4 }

// Game.Simulation.Mechanisms (new folder and namespace)
public enum MoverPath : byte { Rotate = 0, Rail = 1, Drift = 2, Toggle = 3 }
public struct Mover : IComponentData        // on kinematic roots only; writer of its PhysicsVelocity: MoverSystem
{ public MoverPath Path; public float3 Origin; public float Rate; public float Speed; public uint StartTick; public byte Playing, Reverse; public ushort Group;
  public float Yaw; public float3 Velocity; public byte PingPong; }   // HAZ-013: Rotate/Drift start yaw, Drift velocity, Rail back-and-forth
// MoverMath.Pose(mover, points, tick): Rotate = Origin at Yaw + Rate·t; Drift = Origin + Velocity·t at Yaw + Rate·t;
// Rail = Speed·t along the points (a closed loop, or back and forth with PingPong; Reverse runs backwards);
// Toggle = points[0] → points[1] over 1 / Rate s (Reverse travels back). A mover that is not playing holds its StartTick pose.
[InternalBufferCapacity(0)] public struct MoverPoint : IBufferElementData { public float3 Pose; public float Yaw; }   // Rail and Toggle points
public struct MechanismAction { public MechanismActionKind Kind; public ushort Group; public float Value; }        // record written by MechanismSystem
public enum MechanismActionKind : byte { MoverPlay, MoverReverse, MoverSpeed, ZoneEnable, ZoneDisable, ZonePeriod, MinesArm, MinesDisarm,
                                         TargetPolicy, PortalJam, SpawnHazardGroup, NarrativeEffect }   // append only
public struct SwitchParams { public float TriggerDamage; public ushort Group; }     // BlockModuleId.Switch row
public struct PickupParams { public float Energy; public float Reach; }             // BlockModuleId.Pickup row
public struct PortalLink : IComponentData { public ulong Other; public float JamSeconds; public uint JammedUntilTick; }   // on a Gate zone
public struct PortalTransit { public Entity Body; public ulong From, To; public uint Tick; }   // authority record; applied on every machine
public struct TargetOverride : IComponentData { public Entity Target; public byte Policy; }   // on a turret group root (0 none, 1 leader, 2 fixed)
public struct EnergyGrantRequest { public Entity Ship; public float Energy; public ProducerKind Producer; public uint Sequence; }  // SPEC-22 channel

// HAZ-014: placed mechanisms and the action channel (Game.Simulation.Mechanisms)
public struct Mechanism : IComponentData { public ushort Group; }   // + buffers MechanismRule { Kind, Group, Value, Offset, ActionStart, ActionCount }
                                                                    //   and MechanismRuleAction { Kind, Group, Value, DelayTicks } (HazardSpawn.Mechanism)
public struct MechanismGroup : IComponentData { public ushort Group; }   // on roots whose Switch blocks and instances fire this group's triggers
public struct MechanismActions : IComponentData { public NativeList<MechanismAction> Current; public uint Tick; }   // writer: MechanismSystem
public struct RaceLapSignal : IComponentData { public int LeaderLap; }   // writer: RaceSystem; read by RaceLap triggers
// Action values (MechanismActionMath, poses stay continuous): MoverPlay Value < 0 pauses, else plays; MoverReverse Value > 0 runs backwards
// (Toggle: to the first point), < 0 forwards (to the second), 0 flips; MoverSpeed Rotate/Drift rad/s, Rail m/s, Toggle travel s;
// ZonePeriod Value = period s, keeping the on share. Mover gains PausedTick.

// Content
// MechanismDefinition: rules { trigger (kind, group or zone or block, value), actions[] }; placed with a group id by HazardSpawn
```

## 5. Systems, services and flow

| System | Group and order | Role | Reads | Writes |
|---|---|---|---|---|
| `ImpulseApplySystem` (changed) | as today | Both | requests, `PendingImpulse` | `PhysicsVelocity` (impulses), **`ExternalPush`** |
| `PropulsionSystem` (changed) | as today | Both | `ExternalPush` (Tick − 1) | `FlightState.Carry`, `CarryYaw` |
| `WeaponFireSystem` (changed) | as today | Both | `WeaponDef.RecoilMomentum` | recoil `ImpulseRequest`s |
| `ProjectileStepSystem`, `BeamStepSystem` (changed) | as today | Both | `HitMomentum`, `KnockbackScale` | `DamageEvent.WorldImpulse` |
| `ZoneSystem` | `GameplayPostPhysicsGroup`, after `SpatialHashBuildSystem`, before `ImpulseApplySystem` | Both (pushes); authority (damage, contacts) | `WorldZone`, `ZoneEffect`, `SpatialHash`, poses, `PhysicsMass`, `PhysicsVelocity` | `Impulses` (Environment), damage stream (Hazard), `ZoneContacts` |
| `MineSystem` | after `ZoneSystem`, before `ExplosionSystem` | AuthorityOnly | `MineState`, fuse rows, `SpatialHash`, allegiance | `MineState`, `ExplosionRequest`, damage stream (self-destruct) |
| `MineAnchorJob` (in `MineAnchorSystem`) | after `MineSystem`, before `ImpulseApplySystem` | Both | `MineAnchor`, poses | `Impulses` (Hazard) |
| `HazardFeedbackSystem` | `PresentationBridgeGroup`, before `FxUploadSystem` | Both | `MineState`, zone contacts of `PlayerShip` | `FxEvent`, `SfxEvent` |
| `ZoneVisualSystem` | `PresentationBridgeGroup` | Both | `WorldZone` | pooled zone visuals (boost chevrons, wall shimmer) |

### 5.1 Hit force
1. **Fire.** For each round, `WeaponFireSystem` (or the projectile spawn it hands to) computes `J = RecoilMomentum × RecoilScale × pellets` along the muzzle's world forward. It posts `ImpulseRequest { Body = shooter root, WorldImpulse = −J, WorldPoint = muzzle, Producer = Recoil, Sequence = shotId }`.
2. **Hit.** `ProjectileStepSystem` sets `WorldImpulse = dir × HitMomentum × KnockbackScale`. `BeamStepSystem` does the same per pulse. Explosions keep `ExplosionJob`'s falloff push. `DamageResolveSystem` accumulates them into `PendingImpulse` as today.
3. **Apply.** `ImpulseApplySystem` applies the pending and requested impulses (SPEC-13 §5.2). For every root it changes, it sums `Δv = Σ J × InverseMass` (planar) and `Δω_y` from the same angular term it applies, and writes `ExternalPush { LinearDv, YawDv, Tick }`.
4. **Carry** (`PropulsionJob`, before targets; `dt` = fixed step):
   1. If `ExternalPush.Tick == Tick − 1`: `Carry += ToLocalPlanar(LinearDv, rot)` and `CarryYaw += YawDv`.
   2. **Clamp to reality**, per axis `a ∈ {x, y}`: if `sign(Carry.a) ≠ sign(local.a)`, set `Carry.a = 0`; else `Carry.a = sign × min(|Carry.a|, |local.a|)`. `CarryYaw` is clamped the same way against `ωy`. Then clamp each to `maxCarry` and `maxCarryYaw`.
   3. **Targets.** `tx += Carry.x`, `tz += Carry.y` and `tω += CarryYaw`, added after the command targets are computed, including in the `Brake` case.
   4. **Decay.** `d = (Brake ? carryBrakeDecel : carryDecel) × dt`. `Carry = Carry × max(0, |Carry| − d) / |Carry|`, so it shrinks as a vector. `CarryYaw` shrinks toward 0 by `carryYawDecel × dt`.
5. **Feedback.** A root whose `|LinearDv| ≥ 1.5 m/s` in one tick emits an `FxEvent` of kind `Impact` with `Scale = |Δv|` (`ExternalPushFeedbackSystem`, after `ImpulseApplySystem`; `FxKind.Impact` = 14 has no visual default). The 1.5 m/s line was lowered from 2 when the measured table became the baseline (HAZ-005), so a siege shell on the starter fighter (1.7 m/s) shakes too. CAM-008's screen shake already listens to `FxEvent` scale for the `PlayerShip`.

### 5.2 Zones (`ZoneSystem`, per tick)
1. Gather zones (`WorldZone`, `ZoneEffect`, `ZonePathPoint`) sorted by `PersistentId`.
2. For each zone, query `SpatialHash.QuerySphere` (circle) or `QuerySegment` (box diagonal, path segments) with the zone's bound. For each candidate root, its kind must pass the effect's `Flags`, and it must have `PhysicsMass` with `InverseMass > 0` for push effects. Sort candidates by `Entity.Index`.
3. **Test** in the zone's local frame. Box and circle use the root's centre of mass. Path uses the distance to the nearest segment minus the root's bounding radius (`ShipStats` bounds). Gate is a segment crossing of `(previous COM, COM)` against the gate line, with the sign from the zone forward. The previous COM is kept in `ZoneTracking { float2 LastCom }` on roots, which `ZoneSystem` adds and writes.
4. **Effects.**
   - **Accelerate.** `s = dot(v, fwd)`. `Δ = min(B × dt, max(0, A − s))`. Post `J = M × Δ × fwd` at the centre of mass.
   - **Drag.** `Δv = −v × min(1, A × dt)`, where `v` is the planar velocity and the minimum speed is B. Post `J = M × Δv` at the centre of mass.
   - **Repel.** Penetration `p` (m) past the wall's surface along the normal `n` (pointing back into the track), and approach speed `u = −dot(v, n)`. Post `J = M × (A × p × dt + B × max(0, u) × dt + max(0, u)) × n` at the contact point. The last term removes the approach speed in one tick, which reads as a bounce. Authority: if `u ≥ 3 m/s`, write a `DamageEvent` of `Type = Ram`, `Amount = C × u`, `Radius` = 1.5 cells, at the contact point, with `Producer = Hazard`.
   - **Gate and Bounds.** Append a `ZoneContact`. The authority only consumes them.
5. Pushes are written through `Impulses.BeginWrite(ref state, ProducerKind.Environment, …)`. `Sequence = (zoneOrdinal << 16) | candidateOrdinal`.

### 5.3 Mines (`MineSystem`, authority)
1. **Arming → Armed** after `ArmSeconds`, counted in ticks from `PhaseTick`.
2. **Armed.** Query `SpatialHash.QuerySphere(fuseWorld, TriggerRadius + 64)`. A candidate must be a root with `GridKind.Ship`, without `Hulk`, with mass ≥ `MinTriggerMass`, and must pass the `TriggerFilter` (allegiance vs the mine's `Allegiance`), with distance from the fuse to the bounding sphere ≤ `TriggerRadius`. The first candidate by `Entity.Index` moves the mine to `Triggered`, with `TriggeredBy` set.
3. **Triggered → detonate** after `FuseSeconds`:
   - post `ExplosionRequest { WorldPos = fuse, Radius = BlastRadius, Amount = BlastDamage, Type = BlastType, Source = mine root }`, carrying the mine's `OwnedBy` as instigator when it has one;
   - post a self-destruct `DamageEvent` large enough to destroy the fuse instance, which flags the mine `NoImpulse`;
   - set `Spent`.
4. **Fuse destroyed by damage** while `Arming`, `Armed` or `Triggered`: the existing `Explosive` cook-off on the charge block decides whether it blows (the mine blueprints carry one), and the mine goes `Spent`. A spent device with no live functional block is despawned by `StructuralDestroySystem`'s device rule (HAZ-008).
5. **Anchor.** `MineAnchorJob`, on every machine, posts `J = M × clamp(Stiffness × (anchor − pos) − Damping × v, MaxAccel) × dt` at the centre of mass.

### 5.4 Co-op flow
- Zones are spawned on the authority and sent once as `ZoneSpawn` messages (shape, effects, points, `PersistentId`, group), and on join in the snapshot. Guests build them with the same `HazardSpawn.Zone`. `DespawnGroup` is sent as `ZoneGroupDespawn`.
- Mines are grids and replicate as grids (ADR-0116 §7). `MineState` is sent reliably on change as `MineStates`.
- **Impulse echo (HAZ-010).** When the authority applies impulses to a guest-piloted root in `ImpulseApplySystem`, it sends that guest `PushEcho { tick, LinearDv, YawDv }`. The guest posts the same velocity change to its predicted body as an `ImpulseRequest` of `Producer = Hazard` (at the centre of mass, angular folded in) and marks the reconciliation records from that tick on, so the next correction does not apply it twice. Recoil and zone pushes on the guest's own ship are not echoed: the guest computes them itself.

## 6. Public API
- `HazardSpawn` (§4.5). Preconditions: content resolved, `persistentId` unique, path has ≥ 2 points. On bad input it throws `ArgumentException` naming the definition, in the editor and in dev builds.
- `WeaponDefinition.ProjectileMass`, `BeamForce`; `WeaponTuningDefinition.KnockbackScale`, `RecoilScale`.
- Dev commands (HAZ-009): `hazard zone <id> [x z yaw]`, `hazard mines <field> [x z yaw seed]`, `hazard clear`, `hazard push <N·s>` (shoves the player ship forward), `hazard info` (zone contacts and mine states near the player).

## 7. Edge cases and failure behaviour
| Situation | Required behaviour |
|---|---|
| Ship passes a gate at 300 m/s (5 m per tick) | Segment crossing catches it; one contact per crossing |
| Ship sits on a gate line and jitters | Crossing needs a sign change of the signed distance; a pilot gets at most one contact per gate per 30 ticks |
| Boost section overlaps a wall | Effects apply independently in `PersistentId` order |
| Knockback pushes a ship into a wall | Solver handles contact; carry clamps to measured velocity, so assist does not keep pushing |
| Recoil on a ship with no mass (kinematic replica on a guest) | Request dropped by `ImpulseApplySystem` (no inverse mass), counted |
| Mine triggered by two ships in one tick | Lowest `Entity.Index` is `TriggeredBy`; one detonation |
| Mine field chain reaction (20 mines) | Each blast is a normal `ExplosionRequest`; cook-offs cascade over ticks, never in one tick (explosions damage next tick) |
| Mine shot while `Arming` | Cook-off rule (§5.3.4); no proximity trigger |
| Origin rebase | `WorldZone.Position`, path points, `MineAnchor.Point` and `ZoneTracking.LastCom` are shifted by `OriginRebaseSystem` (HAZ-006/008 add them to its shift list) |
| Zone contains hulks or debris | Push applies only if its `Flags` allow; defaults: ships and devices yes, debris no (chunk bodies count as debris) |
| Guest receives `PushEcho` for a tick already reconciled | Applies the remaining difference only; never twice (HAZ-010 test) |

## 8. Performance budget
Advisory (ADR-0042):
- `ZoneSystem` under 0.15 ms main-thread scheduling, and under 0.3 ms of jobs, with 100 zones and 30 ships;
- `MineSystem` under 0.1 ms with 200 mines;
- 200 mines must not add more than 1 ms per tick to physics in an idle field.

**Feel targets for HAZ-005** (measured Δv on the first hit tick, with fresh reference ships, flight assist on, and the playground's starter fighter as "light"):

| Shot | Light ship (starter fighter) | NOCTURNE WRAITH frigate | NOCTURNE cruiser |
|---|---|---|---|
| Small plasma, centre hit | 0.6–1.0 m/s | 0.2–0.4 m/s | under 0.05 m/s |
| Railgun, end hit (bow) | 4–6 m/s and a visible yaw kick (≥ 30°/s) | 1.5–2.5 m/s | about 0.3 m/s |
| Siege cannon blast at 10 m | 10–15 m/s | 4–6 m/s | about 1 m/s |
| Recoil, small plasma burst (shooter) | 0.1–0.2 m/s per round | — | — |
| Recoil, railgun (shooter) | 1–2 m/s | — | — |

With `carryDecel = 8 m/s²`, a 5 m/s shove plays out over about 0.6 s, and a 40 m/s boost surge over about 5 s.

**Measured (HAZ-005, 2026-10-17, `HitForceFeelTests.Table_WithinTargets`)** with the shipped values (`knockbackScale` 1, `recoilScale` 0.17, `explosionImpulsePerDamage` 60). The owner flew it and said it "feels right", so **this measured table is the baseline**; the test holds each row within ±15%.

| Shot | Starter fighter (24.6 t) | NOCTURNE WRAITH (70.4 t) | NOCTURNE REQUIEM (396 t) |
|---|---|---|---|
| Small plasma, centre hit (22.5 kg at 400 m/s) | 0.37 m/s | 0.13 m/s | 0.023 m/s |
| Railgun, bow hit (50 kg at 1,200 m/s) | 2.44 m/s, yaw kick 27°/s | 0.85 m/s, 6°/s | 0.15 m/s |
| Siege cannon, direct hit (95 kg at 420 m/s, 4 m blast) | 1.68 m/s | 0.70 m/s | 0.10 m/s |
| Recoil, small plasma round (shooter) | 0.06 m/s | — | — |
| Recoil, railgun (shooter) | 0.41 m/s | — | — |

Why the rows sit below the first targets:
- The targets assumed a light ship of about 10 t; the starter fighter is 24.6 t. A centre hit carries exactly the shot's momentum (the plasma row is 9,000 N·s ÷ the mass to within 1%).
- "Siege blast at 10 m" cannot happen: the siege blast radius is 4 m. A direct hit is shell momentum alone (95 × 420 = 39,900 N·s): `ExplosionJob` scales a root's blast push by `(1 − d / radius)²`, where `d` is the distance from the blast to the centre of the root's **grid bounds**, not to the cells it hits. A shell bursting on the hull of any ship wider than its blast radius is too far from that centre to push it. Mines (`Knockback` set, larger radii) still push as designed. Pushing from the hit cells instead is a follow-up, not part of this baseline.
- Recoil rows are measured on a 4.9 t test hull and converted to the fighter by mass; recoil is the shot's momentum × 0.17.

## 9. Tasks

### HAZ-001 — Public contract types
- **Milestone:** M4
- **Tier:** standard — transcribes §4 with no behaviour
- **Depends on:** —
- **Do:** Add every type of §4:
  - `ExternalPush`;
  - the `FlightState` and `FlightConfig` fields;
  - the `ProducerKind`, `GridKind` and `BlockModuleId` appends;
  - the `WeaponDefinition` and `WeaponTuningDefinition` fields;
  - the zone and mine components, enums and definitions;
  - `ZoneContacts`, and a `HazardSpawn` that throws `NotImplementedException`.

  Also add `RngPurpose.HazardScatter`. Add the ADR-0118 rows to architecture §6 and §8, and `ExternalPush` to `DamageSetup.GridRootComponents`.
- **Files:**
  - `Assets/Game/Voxel/Damage/ExternalPush.cs`;
  - `Assets/Game/Simulation/Hazards/*.cs` (new folder; namespace `Game.Simulation.Hazards`);
  - `Assets/Game/Data/Definitions/Hazards/*.cs`;
  - `Assets/Game/Data/Blocks/BlockModuleId.cs`, `Assets/Game/Data/Combat/ProducerKind.cs`, `Assets/Game/Voxel/Model/GridKind.cs`;
  - `docs/01-architecture.md`.
- **Acceptance criteria:**
  - [ ] Everything compiles.
  - [ ] Existing tests pass unchanged; `ExternalPush` is on every grid root.
  - [ ] Enum appends do not renumber existing values.
- **Tests:** `HazardContractTests.EnumValues_Appended`, `.GridRoot_HasExternalPush`.

### HAZ-002 — Carried velocity in flight assist
- **Milestone:** M4
- **Tier:** frontier — changes the flight assist every ship uses and the impulse applier
- **Depends on:** `HAZ-001`
- **Do:**
  - `ImpulseApplySystem` writes `ExternalPush` (§5.1 step 3), from both jobs, per root, deterministically.
  - `PropulsionJob` folds it in, clamps, adds it to the targets and decays it (§5.1 step 4).
  - Add the `config_flight` values. `FlightConfig.Default` keeps carry decel at a large value (1e6), so isolated flight fixtures behave as before.
  - Record the change as SPEC-20 decision 15, citing ADR-0118.
- **Files:**
  - `Assets/Game/Voxel/Damage/Systems/ImpulseApplySystem.cs` and its jobs;
  - `Assets/Game/Simulation/Flight/PropulsionJob.cs`, `FlightMath.cs`;
  - `Assets/Game/Data/Definitions/Flight/FlightConfigDefinition.cs`, `Assets/Game/Content/Config/config_flight.asset`;
  - `docs/specs/SPEC-20-flight-propulsion.md`.
- **Acceptance criteria:**
  - [ ] A 5 m/s sideways shove on a coasting fighter is still at least 2.5 m/s after 0.3 s and is gone within 1 s.
  - [ ] A shove into a static wall leaves the ship at rest against it within 0.5 s, and assist does not push into the wall.
  - [ ] Braking removes carry in under 0.3 s at 5 m/s.
  - [ ] All SPEC-20 flight scenarios and `PropulsionTests` pass.
  - [ ] Determinism hashes match across two runs.
- **Tests:** `CarryTests.SidewaysShove_Carries`, `.WallContact_DoesNotDriveIn`, `.Brake_ClearsCarry`, `.YawKick_Carries`, `.Deterministic`; `ImpulseApplyTests.ExternalPush_SumsPerRoot`.

### HAZ-003 — Momentum hit model
- **Milestone:** M4
- **Tier:** standard — data migration and two formula changes with exact rules
- **Depends on:** `HAZ-001`
- **Do:**
  - Replace `impulsePerHit` with `projectileMass` and `beamForce` in the definition, the validator (`WPN-E006` becomes "must be finite and nonnegative" for both) and the `WeaponDef` blob (`HitMomentum`, `RecoilMomentum`).
  - Migrate the 11 weapon assets with an editor migration:
    - projectiles get `projectileMass = impulsePerHit / projectileSpeed`;
    - beams get `beamForce = impulsePerHit / beamPulseSeconds`.

    Same push as today until HAZ-005 retunes it.
  - Projectile and beam step use `HitMomentum × KnockbackScale`.
- **Files:**
  - `Assets/Game/Data/Definitions/Weapons/WeaponDefinition.cs`, `WeaponTuningDefinition.cs`;
  - `Assets/Game/Simulation/Weapons/WeaponDef.cs`, `WeaponContent.cs`, `ProjectileStepJob.cs`, `BeamStepJob.cs`;
  - `Assets/Game/Editor/Content/WeaponMomentumMigration.cs`;
  - `Assets/Game/Content/Weapons/*.asset`.
- **Acceptance criteria:**
  - [ ] Knockback per hit is unchanged within 1% for every weapon before tuning.
  - [ ] No asset keeps `impulsePerHit`.
  - [ ] The validator rejects negative or non-finite values.
- **Tests:** `WeaponMomentumTests.Migration_PreservesImpulse`, `.Projectile_UsesMomentum`, `.Beam_UsesForcePerPulse`; existing `WeaponContentTests` pass.

### HAZ-004 — Recoil
- **Milestone:** M4
- **Tier:** standard — one producer on an existing channel, rules in §5.1
- **Depends on:** `HAZ-003`
- **Do:** Post recoil requests per fired round at the muzzle (§5.1 step 1), with `ProducerKind.Recoil` and `Sequence` = shot id. Register the writer before `ImpulseApplySystem` reads. Beams post none.
- **Files:** `Assets/Game/Simulation/Weapons/WeaponFireSystem.cs` (and its fire job), `Assets/Game/Tests/EditMode/Simulation/Weapons/RecoilTests.cs`.
- **Acceptance criteria:**
  - [ ] A centred fixed gun pushes its ship straight back.
  - [ ] An off-centre turret also yaws it, with the sign of the cross product.
  - [ ] A burst of N rounds posts N requests in one tick with distinct sequences.
  - [ ] No recoil on a guest's kinematic replicas (dropped, counted).
- **Tests:** `RecoilTests.Centred_PushesBack`, `.OffCentre_Yaws`, `.Burst_OnePerRound`, `.Beam_NoRecoil`.

### HAZ-005 — Hit-force tuning and impact feedback
- **Milestone:** M4
- **Tier:** standard — tuning against the §8 table, with measurement scripts
- **Depends on:** `HAZ-002`, `HAZ-004`
- **Do:**
  - Write a PlayMode measurement fixture: reference ships, one shot each, Δv on the first tick.
  - Set `projectileMass`, `beamForce`, `knockbackScale`, `recoilScale` and `explosionImpulsePerDamage` to the §8 targets.
  - Add the `Impact` `FxEvent` (§5.1 step 5) and check CAM-008 shakes on it for the player ship.
  - Record the measured table in this spec's §8 and in the ledger log.
  - Ask the owner to feel it in the playground. That is not a gate.
- **Files:**
  - `Assets/Game/Tests/PlayMode/Combat/HitForceFeelTests.cs`;
  - `Assets/Game/Content/Weapons/*.asset`, `Assets/Game/Content/Tuning/*`;
  - `Assets/Game/Simulation/Flight/PropulsionJob.cs` (feedback event) or a small `ExternalPushFeedbackSystem`.
- **Acceptance criteria:**
  - [x] Every row of the §8 table is within range, or recorded with the reason it is not (measured table and reasons in §8; the owner took the measured feel as the baseline).
  - [ ] Existing ram, AI combat and turret-lead scenarios pass.
  - [x] The camera shakes on a railgun hit to the player (an `Impact` event at 2.4 m/s against the 1.5 m/s line, and its shake rule; not watched live).
- **Tests:** `HitForceFeelTests.Table_WithinTargets`; the existing AI combat, ram damage and turret-lead tests pass.

### HAZ-006 — Zones: shapes, query and push effects
- **Milestone:** M4
- **Tier:** frontier — new deterministic simulation system on every role, origin rebase and ordering
- **Depends on:** `HAZ-002`
- **Do:**
  - Implement `ZoneSystem` §5.2 for `Accelerate`, `Drag`, `Gate` and `Bounds`, with `ZoneTracking` and `ZoneContacts`.
  - Implement `HazardSpawn.Zone`, `PathZone` and `DespawnGroup`.
  - Make `OriginRebaseSystem` shift zone positions, path points and `LastCom` (an ADR-0118 allowance: zone data is placement).
  - Add three zone definitions: `zone_boost_strip`, `zone_slow_field` and `zone_gate`.
  - Add a minimal `ZoneVisualSystem`: boost chevrons and gate posts as pooled meshes, NOCTURNE tokens.
- **Files:** `Assets/Game/Simulation/Hazards/ZoneSystem.cs`, `ZoneJobs.cs`, `HazardSpawn.cs`; `Assets/Game/Galaxy/Systems/OriginRebaseSystem.cs`; `Assets/Game/Presentation/Hazards/ZoneVisualSystem.cs`; `Assets/Game/Content/Hazards/*.asset`.
- **Acceptance criteria:**
  - [ ] A fighter through a boost strip (target 140 m/s, 60 m/s²) gains to the strip's target and keeps more than half the gain 2 s after leaving.
  - [ ] A slow field halves speed within 1 s.
  - [ ] A gate yields one contact per crossing at 300 m/s, and none while hovering on the line.
  - [ ] Results are identical across two runs and across an origin rebase mid-strip.
- **Tests:** `ZoneTests.Boost_AcceleratesAndCarries`, `.Drag_Slows`, `.Gate_FastCrossing_Once`, `.Gate_Hover_NoContact`, `.Rebase_Stable`, `.Deterministic`.

### HAZ-007 — Field walls
- **Milestone:** M4
- **Tier:** standard — one effect kind on HAZ-006's system, rules in §5.2
- **Depends on:** `HAZ-006`
- **Do:**
  - Implement `Repel`: spring and damper with the bounce term, applied at the contact point, and contact damage on the authority.
  - Add `zone_field_wall` (path).
  - Visual: a shimmering ribbon along the path, plus sparks (`FxEvent` `Impact`) and a sound (`SfxEvent`) on contact, scaled by approach speed.
- **Files:** `Assets/Game/Simulation/Hazards/ZoneJobs.cs`; `Assets/Game/Presentation/Hazards/ZoneVisualSystem.cs`; `Assets/Game/Content/Hazards/zone_field_wall.asset`.
- **Acceptance criteria:**
  - [ ] A ship at 80 m/s into a wall at 45° leaves along the wall and is turned by the contact.
  - [ ] It takes ram damage proportional to the approach speed.
  - [ ] It never ends up outside a closed wall loop after 1,000 random impacts in a soak.
- **Tests:** `FieldWallTests.Glancing_TurnsAndSlides`, `.HeadOn_Bounces`, `.Damage_ScalesWithApproach`, `.NoTunnelling_Soak`.

### HAZ-008 — Mines
- **Milestone:** M4
- **Tier:** frontier — new grid kind, fuse decider, cook-off and despawn interplay with damage and splits
- **Depends on:** `HAZ-006`, `HAZ-003`
- **Do:**
  - Implement `GridKind.Device`. It gets no core requirement, no hulk transition and no salvage mark. Despawn it when no live functional block remains (`StructuralDestroySystem` device rule).
  - Add the `ProximityFuse` module row and its validator.
  - Implement `MineSystem` §5.3 and `MineAnchorJob`.
  - Implement `HazardSpawn.MineField`.
  - Make two blueprints (design them first as small plan-view sketches for the owner, two options each):
    - `device_mine_contact`: about 3×3×3 cells, quick fuse, small blast;
    - `device_mine_heavy`: about 5×5×5 cells, slow fuse, big blast, blinks longer.
  - Add mine definitions and a mine field definition.
  - Implement `HazardFeedbackSystem`: slow blink when armed, fast blink and rising beep when triggered, a radar marker within 300 m.
- **Files:** `Assets/Game/Simulation/Hazards/MineSystem.cs`, `MineAnchorSystem.cs`; `Assets/Game/Voxel/Damage/Systems/StructuralDestroySystem.cs`; `Assets/Game/Data/Blocks/Modules/ProximityFuseModule.cs`; `Assets/Game/Content/Blocks/block_proximity_fuse.asset`; `Assets/Game/Content/Hazards/*`; `Assets/Game/Presentation/Hazards/HazardFeedbackSystem.cs`; `Assets/Game/Editor/Content/HazardContentBuilder.cs`.
- **Acceptance criteria:**
  - [ ] A mine arms after `ArmSeconds`.
  - [ ] A fighter passing within `TriggerRadius` triggers it; it blinks, then detonates after `FuseSeconds`.
  - [ ] The blast damages and shoves the fighter; the mine is gone afterwards.
  - [ ] Debris and hulks never trigger it.
  - [ ] Shooting a mine cooks it off and sets off a neighbour within blast radius.
  - [ ] A shoved anchored mine returns within 10 s.
  - [ ] Two identical runs give identical outcomes.
- **Tests:** `MineTests.Arms_Triggers_Detonates`, `.Debris_DoesNotTrigger`, `.Shot_CooksOff_ChainsNeighbour`, `.Anchor_Returns`, `.TwoShipsSameTick_OneDetonation`, `.Deterministic`.

### HAZ-009 — Hazard playground pad and dev commands
- **Milestone:** M4
- **Tier:** standard — playground content and commands on finished systems
- **Depends on:** `HAZ-005`, `HAZ-007`, `HAZ-008`
- **Do:**
  - Add pad `17  HAZARDS / HIT FORCE`. Pressing F spawns, at 900 m east:
    - a boost strip into a field-wall bowl;
    - a small mine field;
    - a slow field;
    - three drifting light target ships (no AI) to knock around.
  - Playground reset clears them (`DespawnGroup`).
  - Add the §6 dev commands.
- **Files:** `Assets/Game/App/DevRuntime/Playground/DevPlayground.cs`, `DevPlaygroundHazards.cs`; `Assets/Game/App/DevRuntime/Commands/HazardDevCommand.cs`.
- **Acceptance criteria:**
  - [ ] The pad spawns everything, and reset removes it with no leaked entities.
  - [ ] Dev commands work from `tools/playtest/g.sh`.
  - [ ] Screenshot proof in the ledger log.
- **Tests:** `PlaygroundHazardTests.Spawn_Reset_NoLeaks`; `HazardDevCommandTests.Commands`.

### HAZ-010 — Hazards and hit force in co-op
- **Milestone:** M4
- **Tier:** frontier — replication and own-ship prediction interplay
- **Depends on:** `HAZ-009`, `NET-018`
- **Do:**
  - Add the messages `ZoneSpawn`, `ZoneGroupDespawn`, `MineStates` and `PushEcho` (§5.4), and bump the protocol.
  - Add the role-policy entries.
  - Echo pushes to guest-piloted ships and mark reconciliation records.
  - Extend `coop-parity.sh` with zone count, mine phases and carry.
  - Check live with `coop.sh` at `net sim 80 10 1`.
- **Files:** `Assets/Game/Net/Protocol/*`, `Replication/GuestReplicator.cs`, `Systems/*`; `Assets/Game/App/Session/RoleSystemPolicy.cs`; `tools/playtest/coop-parity.sh`.
- **Acceptance criteria:**
  - [ ] A guest sees the same zones and mine phases.
  - [ ] A guest's boost strip feels immediate (no round trip).
  - [ ] A host railgun hit on a guest shows its push within one RTT, without overshoot greater than 10% of Δv.
  - [ ] Parity diff is empty; `NetWriterRulesTests` passes.
- **Tests:** `HazardReplicationTests.ZoneSpawn_RoundTrip`, `.MineStates_RoundTrip`, `PushEchoTests.NoDoubleApply`; parity run recorded.

### HAZ-011 — Hazard scenarios and determinism gate
- **Milestone:** M4
- **Tier:** standard — scenario tests over finished systems
- **Depends on:** `HAZ-009`
- **Do:** Write a PlayMode scenario:
  - a fighter boosts through a strip;
  - it is knocked by a railgun into a wall;
  - it crosses a gate;
  - it triggers a mine.

  Hash simulation state each tick across two runs.
- **Files:** `Assets/Game/Tests/PlayMode/Hazards/HazardScenarioTests.cs`.
- **Acceptance criteria:**
  - [ ] The scenario passes.
  - [ ] Per-tick hashes match across runs, and across 30 and 144 FPS frame pacing.
- **Tests:** `HazardScenarioTests.FullRun`, `.Deterministic_AcrossFrameRates`.

### HAZ-012 — Contract types for the mechanism set
- **Milestone:** M4
- **Tier:** standard — transcribes §4.6 with no behaviour
- **Depends on:** `HAZ-001`
- **Do:**
  - Add every §4.6 type, including the `Switch` and `Pickup` module classes and rows, `MechanismDefinition`, the `ZoneEffectKind` appends and the `ProximityFuseParams` fields.
  - Add the ADR-0119 rows to architecture §6 and §8.
  - Add the `EnergyGrantRequest` channel to SPEC-22's power streams, with no consumer yet.
- **Files:** `Assets/Game/Simulation/Mechanisms/*.cs`; `Assets/Game/Data/Hazards/*.cs`; `Assets/Game/Data/Blocks/*`; `Assets/Game/Data/Definitions/Hazards/MechanismDefinition.cs`; `Assets/Game/Simulation/Power/*Streams*.cs`; `docs/01-architecture.md`.
- **Acceptance criteria:**
  - [x] Compiles; existing tests pass.
  - [x] Enum appends keep every existing value.
- **Tests:** `MechanismContractTests.EnumValues_Appended`, `.Modules_Registered`.

### HAZ-013 — Moving structures
- **Milestone:** M4
- **Tier:** frontier — a new kinematic velocity writer that collides with dynamic ships
- **Depends on:** `HAZ-012`
- **Do:**
  - Implement `MoverSystem` (ADR-0119 decision 1) for `Rotate`, `Rail` (loop and ping-pong), `Drift` and `Toggle`, tick-exact from `StartTick`.
  - Movers spawn kinematic (SPEC-12 motion), and their poses shift with origin rebase.
  - Add a playground rotating arm and a door to try them.
- **Files:** `Assets/Game/Simulation/Mechanisms/MoverSystem.cs`, `MoverMath.cs`; `Assets/Game/Galaxy/Systems/OriginRebaseSystem.cs`; tests.
- **Acceptance criteria:**
  - [x] A rotating arm sweeps at its rate with no drift over 10 minutes.
  - [x] A ship in its path is batted aside and takes ram damage.
  - [x] A toggle door opens and closes in its duration.
  - [x] Two runs are identical.
- **Tests:** `MoverTests.Rotate_TickExact`, `.Rail_PingPong`, `.Toggle_Duration`, `.BatsShip`, `.Deterministic`.
- **Done (2026-10-17):** `MoverMath.Pose` is a pure function of path and tick; `MoverSystem` sets the kinematic velocity that lands each mover on it. Measured: yaw error under 1e-3 rad over 10 minutes; the starter fighter is batted to 23 m/s and loses 4 cells. A 1.9 t synthetic hull is shoved without ram damage, because each tick of a sweep is under the 20 kJ ram floor. Playground pad 17: sweeper arm and blast door.

### HAZ-014 — Mechanisms: switches and actions
- **Milestone:** M4
- **Tier:** frontier — a decider that drives five other features through their owners
- **Depends on:** `HAZ-013`, `HAZ-006`
- **Do:**
  - Implement `MechanismSystem` (authority). Triggers: switch damaged, instance destroyed, zone or gate contact, timer, race lap (read through an interface), narrative `ICondition`. Actions are records consumed by their owners.
  - Add the `Switch` block and a blast door blueprint.
  - Implement `HazardSpawn` support for mechanism groups.
- **Files:** `Assets/Game/Simulation/Mechanisms/MechanismSystem.cs`, `MechanismActions.cs`; `Assets/Game/Content/Blocks/block_switch.asset`; `Assets/Game/Content/Hazards/*`.
- **Acceptance criteria:**
  - [x] Shooting a switch slams its door for its time, then the door reopens.
  - [x] A timer toggles a zone.
  - [x] Each action changes only state its owner writes (the role-policy test passes).
- **Tests:** `MechanismTests.Switch_SlamsDoor`, `.Timer_TogglesZone`, `.Actions_RouteToOwners`.
- **Done (2026-10-17):**
  - `MechanismSystem` (authority, post-physics after damage, zones and mines) reads switch hits (function HP drop ≥ `TriggerDamage`), destroyed instances, gate crossings and bounds entries from `ZoneContacts`, timers and `RaceLapSignal`. It writes due actions into `MechanismActions.Current`. `MoverSystem` and `ZoneSystem` apply them on their next update.
  - Content: `block_switch`, `mechanism_blast_door` and `ship_track_switch`; playground pad 17 has a switch door.
  - Tests: `MechanismTests.Switch_SlamsDoor` (PlayMode, a real plasma shot), `.Timer_TogglesZone`, `MechanismRoutingTests.Actions_RouteToOwners`, and `MechanismActionMathTests` (continuity).
  - **Open:** the narrative `ICondition` trigger needs a managed bridge. Mine, turret, portal, spawn and narrative actions are written but have no owner yet (HAZ-018 to HAZ-022).

### HAZ-015 — Gravity wells and zone schedules
- **Milestone:** M4
- **Tier:** standard — two additions to HAZ-006's zone system, rules in ADR-0119 decision 4
- **Depends on:** `HAZ-006`, `HAZ-012`
- **Do:** Add the `Attract` effect (every machine) and `ZoneSchedule` gating for every kind. Add `zone_gravity_moon` content.
- **Files:** `Assets/Game/Simulation/Hazards/ZoneJobs.cs`; `Assets/Game/Content/Hazards/zone_gravity_moon.asset`.
- **Acceptance criteria:**
  - [x] A coasting ship curves into an orbit-like arc around a well.
  - [x] A scheduled zone is on exactly its `OnTicks` per period.
- **Tests:** `GravityZoneTests.Attract_CurvesPath`, `ZoneScheduleTests.OnOff_TickExact`.
- **Done (2026-10-17):** `ZoneSystem` skips a scheduled zone off its on ticks (`ZoneSchedule.IsOn`) and applies `Attract` as `GM / max(r, rmin)²` toward the centre. `zone_gravity_moon`: GM 720 000 m³/s², softened inside 40 m, reach 450 m (a 60 m/s circular orbit at 200 m).

### HAZ-016 — Pulses with shadows (flares and reactors)
- **Milestone:** M4
- **Tier:** frontier — authority raycasts against voxel colliders, deterministic per pulse
- **Depends on:** `HAZ-015`
- **Do:**
  - Add the `Pulse` effect: point or direction source, damage plus shield drain on its scheduled ticks, skipped when the ray from the source to a root's centre of mass hits another grid first.
  - Visual: a pulse ring or a flare sweep, with the shadows shown.
- **Files:** `Assets/Game/Simulation/Hazards/PulseJob.cs`; `Assets/Game/Presentation/Hazards/ZoneVisualSystem.cs`.
- **Acceptance criteria:**
  - [x] A ship in the open takes the pulse; a ship behind a wreck does not.
  - [x] Shield drain applies before hull damage.
  - [x] It is deterministic.
- **Tests:** `PulseTests.Open_TakesPulse`, `.Shadowed_Safe`, `.Shield_DrainsFirst`, `.Deterministic`.
- **Done (2026-10-17):**
  - `PulseSystem` (authority) fires a Pulse zone on the first tick of each window of its `ZoneSchedule` (`PulseMath.Fires`; a pulse zone with no schedule never fires; `HazardSpawn.Schedule` sets one).
  - Each live ship with its centre of mass inside is hit unless a physics ray from the source to that centre hits another grid or chunk first. The source is the zone centre for C = 0 (a reactor), or a point far back along the zone's forward for C = 1 (a flare).
  - A raised shield takes A + B as one shield hit, so it drains first and `ShieldSystem` passes any overflow to the hull. A ship without a shield takes A as energy damage on the face toward the source.
  - Content: `zone_pulse_reactor` (A 260, B 240 over 140 m) and `zone_solar_flare` (A 180, B 320 over a 600 m square).
  - Tests: three pulses take ~1050 off a ship in the open, 0 behind a wreck slab; a 2500-HP shield loses 500 per pulse with the hull untouched; two runs match exactly.
  - Visual (`ZoneVisualSystem`, every machine): the reach rim and the lit ground brighten over the 0.75 s before a pulse, then a ring sweeps out in 0.3 s. Shadows show as dark wedges, cut by the same rays. See [pulse_reactor.jpg](../design/racing/img/pulse_reactor.jpg): the warning above, the pulse below.
  - Playground pad 19 HAZARDS puts the reactor, a slab to hide behind, five canisters and one mine of each payload in one place. Each payload mine now has its own body and name: a band of blue (repulsor), cyan (EMP), orange (seeker) or purple (gravity).

### HAZ-017 — Engine wash
- **Milestone:** M4
- **Tier:** standard — a derived push from existing thruster state
- **Depends on:** `HAZ-002`
- **Do:** Implement `EngineWashSystem` (ADR-0119 decision 5): a cone behind each firing thruster, with strength from `MaxForce × Intensity`, never pushing its own ship. Heat damage is authority only.
- **Files:** `Assets/Game/Simulation/Hazards/EngineWashSystem.cs`.
- **Acceptance criteria:**
  - [x] A tailgating fighter 30 m behind a boosting ship is pushed back measurably.
  - [x] The owner ship feels nothing extra.
  - [ ] A dead engine section set to fire (by a mechanism) pushes across a straight.
- **Tests:** `EngineWashTests.Tailgater_Pushed`, `.Owner_Unaffected`.
- **Done except the mechanism criterion (2026-10-17):** reach `12 m × √(MaxForce × Intensity / 10 kN)` (Sprint M 37 m, Torch L 93 m), a 15° cone, a push of `0.3 × MaxForce × Intensity` falling off linearly to the reach, and heat inside the first 30 % (authority). A dead engine section fired by a mechanism waits for HAZ-014.

### HAZ-018 — Mine payloads and seekers
- **Milestone:** M4
- **Tier:** standard — payload branches on HAZ-008's decider
- **Depends on:** `HAZ-008`, `HAZ-015`
- **Do:**
  - Implement the payloads: `Repulse` (impulse only), `Emp` (`InhibitRequest` on thrusters), `Gravity` (a temporary `Attract` zone) and `None`, plus seeker drift.
  - Add three mine contents: `mine_repulsor`, `mine_emp` and `mine_seeker`.
- **Files:** `Assets/Game/Simulation/Hazards/MineSystem.cs`; `Assets/Game/Content/Hazards/*`.
- **Acceptance criteria:**
  - [x] A repulsor shoves without damage.
  - [x] An EMP stops thrust for its seconds while carry keeps the ship drifting.
  - [x] A seeker reaches a slow ship and not a fast one.
- **Tests:** `MinePayloadTests.Repulse_NoDamage`, `.Emp_InhibitsThrusters`, `.Gravity_TemporaryWell`, `.Seeker_ChasesSlowShip`.
- **Done (2026-10-17):** every payload except None shows as a blast. Only Blast deals damage.
  - **Repulse:** throws directly, `PayloadStrength × (1 − d/r)²` to each body's centre of mass. A blast without damage carries no push.
  - **Emp:** inhibits every thruster of each ship in reach for `PayloadSeconds`.
  - **Gravity:** leaves an `Attract` zone (`MineWell`, softened inside 15 m) for `PayloadSeconds`.
  - **Seeker:** an armed seeker drifts toward the nearest ship within 8 trigger radii, at most `SeekSpeed`.
  - Content: `mine_repulsor` (1.2 MN·s over 32 m), `mine_emp` (3 s over 30 m), `mine_seeker` (22 m/s, unanchored) and `mine_gravity` (GM 18 000 over 120 m for 6 s).
  - `MinePayloadTests` 4/4.

### HAZ-019 — Energy canisters
- **Milestone:** M4
- **Tier:** standard — one device module and one channel consumer
- **Depends on:** `HAZ-012`, `HAZ-008`
- **Do:**
  - Add the `Pickup` module: a ship within reach is granted energy (`EnergyGrantRequest`, applied by `PowerAllocationSystem`), and the canister despawns.
  - Add `device_canister`, carrying an `Explosive` block so a shot sets it off.
- **Files:** `Assets/Game/Simulation/Hazards/PickupSystem.cs`; `Assets/Game/Simulation/Power/PowerAllocationSystem.cs`; content.
- **Acceptance criteria:**
  - [x] Flying through a canister raises stored energy by its amount, once.
  - [x] Shooting one explodes it.
- **Tests:** `CanisterTests.Pickup_GrantsOnce`, `.Shot_Explodes`.
- **Done (2026-10-17):** `PickupSystem` (authority) grants a canister to the nearest ship whose bounding sphere comes within its reach (ties by `Entity.Index`) and despawns it; `PowerAllocationSystem` adds the grant to `ShipPower.Stored`, capped, before allocating. `block_canister` (from the small capacitor: `Pickup` 60 energy over 5 m, `Explosive` 500 over 6 cells) in `ship_device_canister`, spawned kinematic by `HazardSpawn.Canister`. A fighter flying through gains 60.7 (with a tick of generation) once; two plasma bolts break one and its blast takes cells off a slab 3 m away. The look is the capacitor's for now.

### HAZ-020 — Gauntlet turrets
- **Milestone:** M4
- **Tier:** standard — a target override read by existing turret targeting
- **Depends on:** `HAZ-012`
- **Do:**
  - Add `TargetOverride` handling in SPEC-23's station-turret targeting: a valid override wins.
  - A mechanism action can set or clear a group's override.
  - `RaceSystem` writes the leader (SPEC-92).
- **Files:** `Assets/Game/Simulation/Ai/*Turret*`; tests.
- **Acceptance criteria:**
  - [x] Turrets with a leader override fire at the leader and switch within one second when the lead changes.
  - [x] With no override they behave as before.
- **Tests:** `TargetOverrideTests.Leader_Targeted`, `.None_Unchanged` (in `TurretAiTests.cs`, on its station fixture).
- **Done (2026-10-17):** `RaceSystem` writes the leader's ship (place 1, not finished, while racing) to `RaceLapSignal.Leader`. `TargetOverrideSystem` (authority, before `AiDecisionSystem`) owns `TargetOverride`: it applies `TargetPolicy` actions to the AI roots of the action's group (Value 0 clears, 1 follows the leader) and points leader overrides at `RaceLapSignal.Leader`. In `AiThinkJob` a valid override (a live ship or station in the hash, inside the sensor) wins over the normal choice, hostile or not. A station turret took a leader 500 m out over a hostile at 300 m, fired, and switched 6 ticks after the lead changed; with no override, or a cleared one, it kept the nearest hostile. No gauntlet is placed on a track yet.

### HAZ-021 — Lane-gate portals
- **Milestone:** M4
- **Tier:** frontier — a second root-transform writer, with own-ship prediction
- **Depends on:** `HAZ-006`, `HAZ-012`
- **Do:**
  - Implement `PortalDecideSystem` (authority: crossing and jam) and `PortalTransitSystem` (every machine: move and rotate the pose and velocity, reset smoothing), as in ADR-0119 decision 3.
  - Visual: a jammed portal shows it.
- **Files:** `Assets/Game/Simulation/Mechanisms/PortalDecideSystem.cs`, `PortalTransitSystem.cs`; visuals.
- **Acceptance criteria:**
  - [ ] A ship at 150 m/s exits at the paired gate with its speed kept, rotated to the exit frame.
  - [ ] A second ship within the jam time passes straight through the gate.
  - [ ] Debris never transits.
- **Tests:** `PortalTests.Transit_KeepsSpeed`, `.Jam_Blocks`, `.Debris_Ignored`.

### HAZ-022 — The mechanism set in co-op
- **Milestone:** M4
- **Tier:** frontier — replication of schedules, mechanism and portal state under prediction
- **Depends on:** `HAZ-010`, `HAZ-014`, `HAZ-016`, `HAZ-018`, `HAZ-019`, `HAZ-020`, `HAZ-021`
- **Do:**
  - Replicate zone schedules and enables, mechanism action results (mover play state), mine payload effects, portal transits and jams, and target overrides.
  - A guest applies its own ship's transits.
  - Extend `coop-parity.sh`.
- **Files:** `Assets/Game/Net/*`; `Assets/Game/App/Session/RoleSystemPolicy.cs`; `tools/playtest/coop-parity.sh`.
- **Acceptance criteria:**
  - [ ] A guest sees doors, arms, pulses and jams match the host.
  - [ ] A guest's own portal transit is not corrected back.
  - [ ] The parity diff is empty.
- **Tests:** `MechanismReplicationTests.RoundTrip`, `PortalTransitTests.GuestOwnShip`; parity run recorded.

## 10. Open questions
| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | Are the §8 feel targets the right strength? | As written; tune in HAZ-005 and adjust after the owner flies it | Owner. **Answered 2026-10-17:** the owner flew it: "feels right" |
| 2 | Should knocked ships drift (carry), or snap back under assist? | Carry, decaying at 8 m/s² | Owner |
| 3 | Who sets off a mine: any ship after arming, or only hostiles to whoever laid it? | Any ship (decision 8); hostile-only is a per-mine filter | Owner |
| 4 | Are mines visible? | Yes: blinking light when armed, radar marker within 300 m | Owner |
| 5 | Does a shot mine explode (chain reactions) or fizzle? | Explodes | Owner |
| 6 | Predict a guest's own fire decisions (so guest shots and recoil feel instant)? | No; recoil already runs where the shot is fired | Tech lead |
| 7 | Should AI avoid mines and walls? | Not in this spec; AI uses walls only through its existing obstacle avoidance | Tech lead |
| 8 | Should drifting debris be pushed by boost strips and walls? | No (flags default to ships and devices) | Tech lead |

## 11. References
- ADR-0118; ADR-0114 (core assist); ADR-0016 (request channels); ADR-0116 and ADR-0117 (co-op).
- SPEC-13 §5.2 (impulses), SPEC-20 §5.3 (propulsion), SPEC-21 (weapon data), CAM-008 (screen shake from `FxEvent`).
