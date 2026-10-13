# SPEC-24 — Abilities

| | |
|---|---|
| **Status** | Draft |
| **Area code** | ABL (task IDs are `ABL-001`, `ABL-002`, …) |
| **Module(s)** | `Game.Simulation` (folder `Assets/Game/Simulation/Abilities/`); `Game.Data` (`Assets/Game/Data/Definitions/Abilities/`) |
| **Depends on specs** | SPEC-02 (`SimClock`, `SimTime`, `ContentHash`, `AbilityActivationGroup`); SPEC-04 (`BlockDef.AbilityIndices`, DATA-010 `grantedAbilities`); SPEC-10 (`BlockInstance`); SPEC-13 (`Hulk`); SPEC-20 (`ShipCommand.Edges`, `PlayerShip`); SPEC-22 (`ShipPower.AbilityGrantMask/Tick`, `ShipStats.FunctionalRevision`, `ShipStatModifier`, `StatId`, `ShieldControl`, `ShieldState`); SPEC-63/64 (`FxEvent`, `SfxEvent`) |
| **Depended on by** | SPEC-10 (profile registration, ADR-0015); SPEC-22 (`AbilitySlot` state and cost); SPEC-04 (`AbilityDefinition` for DATA-010); SPEC-61 (HUD reads `AbilitySlot`); SPEC-72 (MIG-010 writes the two legacy abilities); SPEC-01 (`AbilityContent` boot singleton) |
| **Milestone** | M2 |

> Read [01-architecture.md](../01-architecture.md) (§5, §8), [02-conventions.md](../02-conventions.md), [SPEC-22](SPEC-22-power-shields-stats.md) decisions 6–8 and §5.3 step 7, and [SPEC-20](SPEC-20-flight-propulsion.md) decision 8 before this spec. This spec may not contradict them.

## 1. Purpose

Abilities are what blocks let a ship *do* besides fly and shoot: a speed boost from thrusters, a shield toggle from shield emitters. They come from blocks (`BlockDefinition.grantedAbilities`), so building changes the ability bar and losing the granting blocks removes the ability. The player triggers them with keys 1–4 and AI ships through their profile's ability rules (both via `ShipCommand.Edges`), energy is paid once through SPEC-22's grant handshake, and effects reach the ship only through the existing single-writer components (`ShipStatModifier`, `ShieldControl`). Timing is in fixed ticks: charges, cooldowns and durations are frame-rate independent. The old prototype hard-coded both abilities on the ship asset and read input in the variable-rate update; here abilities are data and the timing is deterministic.

## 2. Scope

**In scope:**
- `AbilityDefinition` (+ blob `AbilityDefsBlob`, singleton `AbilityDefsRef`), the two legacy abilities' v2 values.
- `AbilitySlot` buffer, `AbilityState`, `AbilityGridSetup`.
- `AbilitySystem` (kind-agnostic: slot sync from blocks, timers, activation, power handshake), one effect system per kind: `ShieldToggleEffectSystem`, `StatBoostEffectSystem`.
- Activation feedback (`FxEvent`/`SfxEvent`), tests.

**Out of scope (and who owns it instead):**
- Energy budget and the grant: SPEC-22. Applying stat multipliers: SPEC-22 `ShipStatsAggregateSystem` and its consumers (SPEC-20, SPEC-21).
- Shield behaviour after `ShieldControl` changes: SPEC-22. HUD ability bar: SPEC-61.
- AI use of abilities: not in v1 (SPEC-23 Q1).
- Damaging abilities: v1 has none; a future kind would write `DamageEvent`/`ExplosionRequest` with `ProducerKind.Ability` (SPEC-02 §4.12).

## 3. Decisions (binding)

1. **Abilities come from alive blocks.** The slot list of a ship is the distinct `AbilityIndices` of its alive instances (`Block != 0`, not `Destroyed`), ordered by the lowest granting instance index, at most 8 slots. Many thrusters granting the speed boost give one slot. Reason: the ability bar follows the build; SPEC-22 caps at 32, the HUD shows 4.
2. **Slots resync only when `ShipStats.FunctionalRevision` changes**, keeping the timers and charges of abilities that remain. Reason: architecture §11.1; no per-tick scans of instances.
3. **Keys map to slot positions**: `Ability1..4` edges activate slots 0..3. Slots 4..7 exist for future UI and AI. Reason: SPEC-03 bindings.
4. **Energy is one-shot and granted with one tick of latency** (SPEC-22 decision 8): an activation request puts the slot into `PendingPower` with `PendingCost`; the next tick's grant bit activates it, a missing bit denies it. Abilities with cost 0 activate in the request tick.
5. **`AbilitySystem` is kind-agnostic; one effect system per `AbilityKind` (review X5).** `AbilitySystem` owns charges, cooldowns, the power handshake, activation and, for kinds with `DurationTicks > 0`, `Active`/`ActiveUntilTick`. The effect system of a kind reads `ActivatedThisTick`/`Resynced`, applies the effect, and writes `Blocked` (kind-specific preconditions) and, for kinds without duration, `Active`. A new kind is an enum value plus an effect system. **Effects use existing single writers.** Stat boosts are posted as `StatModifierRequest`s (SPEC-22 `StatusChannels`, ADR-0016; key `(Ability, slot index, stat)`, `UntilTick = ActiveUntilTick`); shield toggles flip `ShieldControl.RequestedOn`, written only by `ShieldToggleEffectSystem`. Reason: architecture §8; SPEC-22 ownership table.
6. **Timing in ticks with fractional accumulators**; charges recharge one at a time. Reason: architecture §5.1.
7. **Hulks have no abilities**: all slots become `Unavailable`, modifiers are cleared.
8. **Folders and namespaces:** `Assets/Game/Simulation/Abilities/` → `Game.Simulation.Abilities`; definitions in `Assets/Game/Data/Definitions/Abilities/` → `Game.Data.Definitions`.

## 4. Data model

### 4.1 Content (`Game.Data.Definitions`)

```csharp
namespace Game.Data.Definitions
{
    public enum AbilityKind : byte { ShieldToggle = 0, StatBoost = 1 }                 // add at end only
    // Stat ids are SPEC-22's StatId, defined in Game.Data.Combat (architecture §3.2); no mirror enum.
    [System.Serializable] public struct AbilityStatMod { public Game.Data.Combat.StatId Stat; [UnityEngine.Min(0.01f)] public float Multiplier; }

    [ContentIdPrefix("ability_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Ability", fileName = "ability_new")]
    public sealed class AbilityDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private string displayName;
        [UnityEngine.SerializeField, UnityEngine.TextArea] private string description;
        [UnityEngine.SerializeField] private UnityEngine.Sprite icon;                 // placeholder until SPEC-81
        [UnityEngine.SerializeField] private AbilityKind kind;
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float energyCost;     // E, one-shot
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float cooldownSeconds; // minimum gap between uses
        [UnityEngine.SerializeField, UnityEngine.Range(1, 9)] private int maxCharges = 1;
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float chargeRechargeSeconds; // time to restore one charge (maxCharges > 1)
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float durationSeconds; // StatBoost effect length
        [UnityEngine.SerializeField] private AbilityStatMod[] statModifiers;            // StatBoost: 1..4 entries
        [UnityEngine.SerializeField, UnityEngine.Range(0, 1)] private float shieldEnableThreshold; // ShieldToggle: min Hp/MaxHp to switch on
        [UnityEngine.SerializeField] private AudioCueDefinition activationCue;
        [UnityEngine.SerializeField] private FxDefinition activationFx;
        // Public getters with the same names in PascalCase.
        public override void Validate(ValidationScope scope, ContentValidationContext context);   // ABL-E001..E005 (§7)
    }
}
```

v2 values (SPEC-72 MIG-010 writes exactly these):

| ID | Kind | Cost E | Cooldown s | Charges | Recharge s | Duration s | Modifiers | Shield threshold |
|---|---|---|---|---|---|---|---|---|
| `ability_speed_boost` | StatBoost | 15 | 0.5 | 2 | 3 | 1.0 | MaxSpeed × 1.8, Thrust × 2.0 | — |
| `ability_shield_toggle` | ShieldToggle | 0 | 1.0 | 1 | 0 | — | — | 0.3 |

### 4.2 Blob and singleton (`Game.Simulation.Abilities`)

```csharp
namespace Game.Simulation.Abilities
{
    using Unity.Entities; using Game.Core; using Game.Data.Combat; using Game.Data.Definitions; using Game.Simulation.Power;

    public struct AbilityStatModDef { public StatId Stat; public float Multiplier; }

    /// <summary>Baked ability; index 0 = none. Times in ticks.</summary>
    public struct AbilityDef
    {
        public AbilityKind Kind;
        public byte MaxCharges;
        public ushort Reserved;
        public float EnergyCost;
        public float CooldownTicks;          // cooldownSeconds × 60
        public float ChargeRechargeTicks;    // chargeRechargeSeconds × 60
        public uint DurationTicks;           // SimTime.SecondsToTicks(durationSeconds)
        public float ShieldEnableThreshold;
        public BlobArray<AbilityStatModDef> Mods;
        public ContentHash IdHash, ActivationCue, ActivationFx;
    }
    public struct AbilityDefsBlob { public BlobArray<AbilityDef> Abilities; }
    public struct AbilityDefsRef : IComponentData { public BlobAssetReference<AbilityDefsBlob> Value; }   // boot singleton

    public static class AbilityContent
    {
        public static void CreateSingletons(EntityManager em, Game.Data.Services.ContentCatalog catalog, System.Collections.Generic.List<Entity> created);
    }
}
```

### 4.3 Ship components (`Game.Simulation.Abilities`)

```csharp
namespace Game.Simulation.Abilities
{
    using Unity.Entities; using Game.Core;

    public enum AbilitySlotState : byte { Ready = 0, PendingPower = 1, Active = 2, Cooldown = 3, Unavailable = 4 }   // SPEC-22 reads PendingPower

    [System.Flags] public enum AbilitySlotFlags : byte { None = 0, ActivatedThisTick = 1, DeniedThisTick = 2, Resynced = 4 /* slot list rebuilt or hulk this tick */ }

    /// <summary>One ability of the ship. At most 8. Only writer: AbilitySystem. Field names fixed by SPEC-22 and SPEC-61 Q3.</summary>
    [InternalBufferCapacity(0)]
    public struct AbilitySlot : IBufferElementData                // 48 bytes
    {
        public ContentHash Ability;                 // SPEC-61
        public ushort AbilityIndex;                 // into AbilityDefsBlob
        public AbilitySlotState State;              // SPEC-22: PendingPower
        public AbilitySlotFlags Flags;              // this tick only
        public byte Charges;                        // SPEC-61
        public byte Active;                         // SPEC-61: 1 while a boost runs or the toggled shield is requested on
        public byte Blocked;                        // 1 = activation not allowed now; written only by the effect system of this slot's kind (§5.2)
        public byte Reserved;
        public float CooldownRemainingTicks;        // SPEC-61
        public float CooldownTicks;                 // SPEC-61
        public float ChargeRechargeRemainingTicks;  // > 0 while a charge is recharging
        public float PendingCost;                   // SPEC-22: E requested this tick
        public uint PendingSinceTick;
        public uint ActiveUntilTick;                // StatBoost
        public int FirstInstanceIndex;              // lowest granting BlockInstance index (ordering)
    }

    /// <summary>Per ship. Only writer: AbilitySystem.</summary>
    public struct AbilityState : IComponentData
    {
        public uint SyncedRevision;      // ShipStats.FunctionalRevision the slots were built from; 0 = never
        public byte SlotCount;
        public byte Reserved0;
        public ushort Reserved1;
    }

    /// <summary>Root composition for abilities (ADR-0015). Registered once by AbilitySystem.OnCreate.</summary>
    public static class AbilityGridSetup
    {
        public static ComponentTypeSet RootTypes { get; }   // AbilitySlot buffer (empty), AbilityState (zero)
        public static void Register(DynamicBuffer<Game.Voxel.Model.RootComponentRegistration> registrations);   // every combat profile
    }
}
```

Memory: 48 B per slot outside the chunk (≤ 8 per ship), `AbilityState` 8 B.

## 5. Systems, services and flow

### 5.0 Placement (all in `AbilityActivationGroup`, SPEC-02 §5.8, which runs after `WeaponFireSystem` and before `ColliderSwapSystem`)

| System | Order inside the group | Burst | Requires |
|---|---|---|---|
| `AbilitySystem` | first (no attribute) | yes | `GameplayActive`, `AbilityDefsRef`, `SimClock` |
| `ShieldToggleEffectSystem` | `[UpdateAfter(typeof(AbilitySystem))]` | yes | `GameplayActive` |
| `StatBoostEffectSystem` | `[UpdateAfter(typeof(AbilitySystem))]` | yes | `GameplayActive`, `AbilityDefsRef`, `StatusChannels` |

Tick timeline for a costed ability pressed during frame f: tick N `PlayerCommandApplySystem` sets the edge → `AbilitySystem` sets `PendingPower`; tick N+1 `PowerAllocationSystem` grants (bit in `AbilityGrantMask`, `AbilityGrantTick = N+1`) → `AbilitySystem` activates and posts the modifier requests → tick N+2 SPEC-22 `StatusSystem` writes `ShipStatModifier` → `ShipStatsAggregateSystem` applies them (SPEC-22 change filter) → `PropulsionSystem` uses the new caps.

### 5.1 `AbilitySystem`

`AbilityJob : IJobEntity` (`ScheduleParallel`; each root by one worker) over roots with `AbilitySlot`, `AbilityState`; reads `ShipStats` (optional), `ShipPower` (optional), `ShipCommand` (optional), `BlockInstance`, `BlockDefsRef`, `Hulk`, `GridSpawning`, `PlayerShip`, `LocalTransform`; enqueues Fx/Sfx. Per ship (`tick = SimClock.Tick`):
1. Skip if `GridSpawning` enabled. Clear `Flags` of every slot.
2. **Hulk:** if the root has `Hulk` and any slot is not `Unavailable` → every slot `State = Unavailable`, `Active = 0`, `Flags |= Resynced`; return.
3. **Sync** (only if `ShipStats` exists with `Initialized` and `FunctionalRevision != SyncedRevision`):
   1. Walk `BlockInstance` in index order; for each alive instance, for each `a` in `BlockDefs[Block].AbilityIndices`: record `(a, instanceIndex)` the first time `a` is seen (≤ 8 distinct; further ones ignored with `GameLog.Burst.Warn` once).
   2. New slot list in that order: reuse the old slot with the same `AbilityIndex` (keep timers, charges, state; update `FirstInstanceIndex`), else a fresh slot `{ Ability = def.IdHash, AbilityIndex = a, State = Ready, Charges = MaxCharges, CooldownTicks = def.CooldownTicks }`.
   3. Every slot of the new list gets `Flags |= Resynced` (effect systems reconcile their effects, §5.3).
   4. `SlotCount`, `SyncedRevision = FunctionalRevision`. Without `ShipStats` (tests, M1 worlds) sync once when `SyncedRevision == 0` using `1` as the revision.
4. **Timers**, per slot `s`, `d = AbilityDefs[s.AbilityIndex]`:
   - `CooldownRemainingTicks = max(0, CooldownRemainingTicks − 1)`.
   - `Charges < d.MaxCharges`: if `ChargeRechargeRemainingTicks <= 0` → start: `ChargeRechargeRemainingTicks = max(1, d.ChargeRechargeTicks)`; else decrement; when it reaches `≤ 0` → `Charges++` (and restart if still below max). With `MaxCharges == 1` the charge returns when the cooldown ends (`Charges = 1` once `CooldownRemainingTicks == 0`).
   - `d.DurationTicks > 0 && Active == 1 && tick ≥ ActiveUntilTick` → `Active = 0` (effects expire by their own timers, e.g. modifier `UntilTick`).
5. **Power handshake** for slots in `PendingPower`: if `ShipPower` is absent → granted; else granted iff `ShipPower.AbilityGrantTick == tick && (AbilityGrantMask & (1u << index)) != 0`. Granted → **activate** (step 7). Not granted and `tick > PendingSinceTick` → denied: `State = Ready`, `Flags |= DeniedThisTick`, `PendingCost = 0`.
6. **Requests** (player and AI alike: every `ShipCommand` writer may set ability edges, SPEC-23 §5.4 step 8): for `k = 0..3` with edge `Ability{k+1}` set in `ShipCommand.Edges` and slot `k` existing: the request is valid if `State ∈ {Ready}`, `Charges > 0`, `CooldownRemainingTicks == 0` and `Blocked == 0`. Invalid → `Flags |= DeniedThisTick`. Valid: `d.EnergyCost > 0` → `State = PendingPower`, `PendingCost = d.EnergyCost`, `PendingSinceTick = tick`; else activate now (step 7).
7. **Activate:** `Charges−−`; `CooldownRemainingTicks = CooldownTicks`; `PendingCost = 0`; `Flags |= ActivatedThisTick`; if `d.DurationTicks > 0` → `Active = 1`, `ActiveUntilTick = tick + d.DurationTicks`. If `d.ActivationCue.Value != 0`: enqueue `SfxEvent { Cue = ActivationCue, Pos = root position, Volume = 1 }`. If `d.ActivationFx.Value != 0`: enqueue `FxEvent { Kind = FxKind.Muzzle, Fx = ActivationFx, Pos = root position, Dir = root forward, Vel = root velocity, Scale = 2 }` (the `Fx` hash selects the look; `Muzzle` is only the fallback kind). Both carry the `PlayerSource` flag for the player ship.
8. **State:** for each slot not `PendingPower`/`Unavailable`: `State = Active == 1 && d.DurationTicks > 0 ? Active : (CooldownRemainingTicks > 0 || Charges == 0) ? Cooldown : Ready`.

### 5.2 `ShieldToggleEffectSystem` (kind ShieldToggle)

`IJobEntity` (`ScheduleParallel`) over roots with `AbilitySlot`, `ShieldControl`, `ShieldState`; for each slot of kind ShieldToggle: if `ActivatedThisTick` → `RequestedOn ^= 1` (once per tick even with several such slots); then `Active = RequestedOn`; `Blocked = RequestedOn == 0 && !(ShieldState.MaxHp > 0 && ShieldState.Hp ≥ d.ShieldEnableThreshold × MaxHp) ? 1 : 0`. The only writer of `ShieldControl` after spawn, and of `Active`/`Blocked` of ShieldToggle slots.

### 5.3 `StatBoostEffectSystem` (kind StatBoost; ADR-0016 requests)

`IJobEntity, IJobEntityChunkBeginEnd` (`ScheduleParallel`) over roots with `AbilitySlot`; writes `StatModifierRequest`s through `StatusChannels.StatModifiers.BeginWrite(ref state, ProducerKind.Ability, chunkCount)` at foreach index `unfilteredChunkIndex`. Per root:
1. If any slot has `Resynced`: post a cancel (`Multiplier = 1`) for every `(SourceId = s, Stat)` with `s` in 0..7 and every `StatId` value (`Sequence = s × 16 + stat`), then re-post `{ Root, Stat, Ability, SourceId = s, Multiplier, UntilTick = ActiveUntilTick, Sequence = 1000 + s × 8 + mod }` for every StatBoost slot `s` with `Active == 1`.
2. Else for every StatBoost slot `s` with `ActivatedThisTick`: post one request per mod (`Sequence = 1000 + s × 8 + mod`).
StatBoost slots are never `Blocked` (the system writes 0). `StatusSystem` (SPEC-22) applies the requests the next tick.

## 6. Public API

| Call / type | Pre-conditions | Post-conditions / errors |
|---|---|---|
| `AbilityGridSetup.Register(buffer)` | Once, from `AbilitySystem.OnCreate` (`[CreateAfter(typeof(GridSpawnSystem))]`) | Grids converted with a combat profile carry `AbilitySlot`, `AbilityState` |
| `AbilityContent.CreateSingletons` | Boot (SPEC-01) | `AbilityDefsRef` |
| `AbilitySlot` (read) | SPEC-22, SPEC-61 | Values of this tick after `AbilitySystem` ran, else of the previous tick |

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| Press while in cooldown or without charges | Denied (`DeniedThisTick`), no energy requested |
| Grant denied (not enough energy) | Slot returns to `Ready` the tick after the request; HUD shows the denial |
| Granting block destroyed while a boost runs | Next resync removes the slot and posts cancels; `StatusSystem` removes the modifiers the next tick |
| Shield toggle on below the threshold | Denied |
| Shield toggle off | Always allowed |
| More than 8 distinct abilities on a ship | First 8 by instance order; warn once |
| Ship becomes a hulk | Every slot `Unavailable`; modifiers cleared |
| No `ShipStats` | Sync once at the first update |
| No `ShipPower` (non-combat test world) | Costs are treated as granted |
| Content | ABL-E001 StatBoost without modifiers or with > 4; ABL-E002 StatBoost `durationSeconds ≤ 0`; ABL-E003 `maxCharges > 1` with `chargeRechargeSeconds ≤ 0`; ABL-E005 ShieldToggle with `durationSeconds > 0` (toggles own `Active` through their effect system) (ABL-E004 is retired: it was the mirror-enum check); ABL-E006 invalid kind or nonfinite/out-of-range scalar values (Q3) |

## 8. Performance budget

| Item | Budget |
|---|---|
| All three systems, 50 ships × 4 slots | ≤ 0.05 ms wall per tick |
| Resync | O(instances) only on `FunctionalRevision` change |
| GC | 0 B per tick |

Scale notes: per ship per tick O(slots ≤ 8); the instance walk happens only on resync, which SPEC-22 already limits to functional-set changes.

## 9. Tasks

### ABL-001 — Public contract types
- **Milestone:** M2
- **Tier:** standard — transcribes the exact code of §4
- **Depends on:** `CORE-001`, `DATA-001`, `PWR-001`
- **Do:** Create every type of §4 in the folders of decision 8; `AbilityDefinition` references SPEC-63's `FxDefinition` and SPEC-64's `AudioCueDefinition` (create empty shells with `// TODO(VFX-001)` / `// TODO(AUD-001)` if they do not exist).
- **Files:** `Assets/Game/Data/Definitions/Abilities/{AbilityKind,AbilityStatMod,AbilityDefinition}.cs`; `Assets/Game/Simulation/Abilities/{AbilityStatModDef,AbilityDef,AbilityDefsBlob,AbilityDefsRef,AbilityContent,AbilitySlotState,AbilitySlotFlags,AbilitySlot,AbilityState,AbilityGridSetup}.cs`
- **Acceptance criteria:**
  - [x] Compiles; `AbilitySlot` is 48 bytes; `AbilityStatMod.Stat` is `Game.Data.Combat.StatId`.
- **Tests:** `AbilityContractTests.Slot_Is48Bytes`.

### ABL-002 — Ability content and boot singleton
- **Milestone:** M2
- **Tier:** standard — fields, values and validation are listed
- **Depends on:** `ABL-001`, `SPEC-04`
- **Do:** Getters and validation (including defensive Q3); `AbilityContent.CreateSingletons` (blob, entry 0 = none; boot integration/lifetime handoff Q4); `AbilityGridSetup.RootTypes`/`Register` (called from `AbilitySystem.OnCreate`, ADR-0015). Ledger note to SPEC-01 for the BootSingletons row. The two assets are written by SPEC-72 MIG-010; create hand-made test assets in the test folder. Abilities follow ContentCatalog’s 1-based ordinal family indices; fractional cooldown/recharge ticks are retained, duration uses SimTime.SecondsToTicks. Validate before native allocation; cue/fx references must belong to the catalog (DATA-E010).
- **Files:** `Assets/Game/Data/Definitions/Abilities/AbilityDefinition.cs`, `Assets/Game/Simulation/Abilities/{AbilityContent,AbilityGridSetup}.cs`, `Assets/Game/Tests/EditMode/Simulation/Abilities/{AbilityContentTests.cs,Fixtures/ability_*.asset}`
- **Acceptance criteria:**
  - [ ] Blob entries equal the §4.1 table (seconds → ticks); validation codes reported.
- **Tests:** `AbilityContentTests.Blob_MatchesTable`, `.Validation_Codes`, `.GridSetup_AddsComponents`.

### ABL-003 — AbilitySystem
- **Milestone:** M2
- **Tier:** frontier — F5: slot lifecycle across SPEC-22's grant handshake, SPEC-20 edges and block destruction
- **Depends on:** `ABL-002`, `SPEC-20`, `SPEC-22`
- **Do:** Implement §5.1.
- **Files:** `Assets/Game/Simulation/Abilities/{AbilitySystem,AbilityJob}.cs`, `Assets/Game/Tests/EditMode/Simulation/Abilities/AbilitySystemTests.cs`
- **Acceptance criteria:**
  - [ ] Slots follow §3 decision 1 ordering and survive resync with their timers.
  - [ ] Speed boost: two presses spend two charges; a third press is denied; one charge returns after 180 ticks; each boost lasts 60 ticks.
  - [ ] Costed activation happens exactly one tick after the request when granted; denied leaves the slot `Ready`.
  - [ ] Shield toggle respects the threshold when switching on (through `Blocked`, written by its effect system).
  - [ ] An ability edge written by a non-player ship's `ShipCommand` activates its slot exactly like the player's (review X5).
- **Tests:** `AbilitySystemTests.Slots_FromBlocks_Ordered`, `.Resync_KeepsTimers`, `.Boost_ChargesAndRecharge`, `.Boost_Duration`, `.Grant_OneTickLatency`, `.Grant_Denied`, `.ShieldToggle_Threshold`, `.Hulk_Unavailable`, `.AiEdge_Activates`.

### ABL-004 — Effect systems
- **Milestone:** M2
- **Tier:** standard — two small writers specified in §5.2–§5.3
- **Depends on:** `ABL-003`, `SPEC-22`
- **Do:** Implement `ShieldToggleEffectSystem` (§5.2) and `StatBoostEffectSystem` (§5.3).
- **Files:** `Assets/Game/Simulation/Abilities/{ShieldToggleEffectSystem,StatBoostEffectSystem}.cs`, `Assets/Game/Tests/EditMode/Simulation/Abilities/AbilityEffectTests.cs`
- **Acceptance criteria:**
  - [ ] A toggle flips `ShieldControl.RequestedOn` once; an active boost appears in `ShipStatModifier` (producer `Ability`, `SourceId` = slot) one tick after activation and disappears when it ends or its block is destroyed.
- **Tests:** `AbilityEffectTests.Toggle_FlipsOnce`, `.Boost_ModifiersAddedAndRemoved`.

### ABL-005 — Ability scenarios and frame-rate independence
- **Milestone:** M2
- **Tier:** standard — PlayMode tests of specified behaviour
- **Depends on:** `ABL-004`, `SPEC-21`
- **Do:** PlayMode: a ship with thrusters and a shield emitter: (1) boost raises top speed by 80% for 1 s (with SPEC-20/22 running); (2) shield toggled off and on; (3) destroying all thrusters removes the boost slot; (4) the same scripted presses at 30/60/144 FPS give identical slot histories per tick.
- **Files:** `Assets/Game/Tests/PlayMode/Simulation/Abilities/AbilityScenarioTests.cs`
- **Acceptance criteria:**
  - [ ] As stated.
- **Tests:** `AbilityScenarioTests.Boost_RaisesTopSpeed`, `.Shield_Toggle`, `.ThrustersLost_RemovesBoost`, `.FrameRateIndependent`.

## 10. Open questions

| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | Should several granting blocks strengthen an ability (e.g. more thrusters → stronger boost)? | No: one slot, fixed strength | Owner |
| 2 | Should the player be able to reorder the ability bar? | No in v1 (block order decides) | Owner |
| 3 | Inspector ranges do not validate serialized floats/enums; duplicate boost stats or a positive duration rounding to zero would break the effect/request semantics | Reject invalid/duplicate stats and multipliers below 0.01 as ABL-E001; require a boost duration rounding to at least one tick as E002; enforce 1..9 charges and multi-charge recharge as E003; E006 covers unknown kinds/nonfinite or negative scalars, invalid threshold and float-tick overflow | Tech lead |
| 4 | Boot row/lifetime are not wired yet and MIG-010's production definitions are absent; the factory has no owned teardown API | Lead adds AbilityContent.CreateSingletons before the boot snapshot, after the AI row when present and before streaming/economy/allocator, retains its persistent blob in the boot lifetime and disposes after jobs complete/entity destruction. An empty ability family publishes one none entry; never invent production definitions or require their IDs. ABL-003 calls AbilityGridSetup.Register once from AbilitySystem.OnCreate after GridSpawnSystem. | Tech lead |
| 5 | Hand-authored test definitions under Tests violate the binding DATA-E011 production-content location rule | Store the two hand-authored fixture payloads as JSON under the test folder and load them into transient AbilityDefinition instances via EditorJsonUtility. Keep DATA-E011 strict and production content unchanged; the same three required tests remain. | Tech lead |

## 11. References

- SPEC-72 §4.6 legacy ability values. SPEC-22 §5.3 step 7 (grant), §4.2 (`ShipStatModifier`).
