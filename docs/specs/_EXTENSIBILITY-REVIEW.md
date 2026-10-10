# Extensibility review before buildout

**Status (2026-10-10): APPLIED.** The owner approved every item as recommended, with voxel small craft for X7. The outcomes, commits and deviations from the text below are in §5; the proposal text is kept as written.

## 1. Question and method

> Can a new *system* (not new content) be added without being "shoved in": for example fighter bays that launch swarms of small craft with tiny weapons, a new weapon delivery, or a new kind of destruction?

Method: four probe features were walked through every spec they would touch, counting the places where an existing spec's code path must be **edited** (not just registered with):

| Probe | What it needs |
|---|---|
| **P1 Fighter bay** | A block that stores small craft, launches them in combat, draws power, gives the ship a "launch/recall" ability, owns the craft (kill credit, allegiance), recovers and rebuilds them |
| **P2 New weapon delivery** | Mines, a point-defence gun, or an ion/EMP gun |
| **P3 New destruction or repair** | An ion hit that disables blocks without removing cells; a repair beam or repair drones that restore cells |
| **P4 New ship-level mechanic** | A tractor beam (pushes other ships), a cloaking field, a nebula that slows ships |

### 1.1 What already extends well (keep it)

- **Functional components by registration.** A spec appends its component types to `FunctionalComponentRegistration` and `GridSpawnSystem` puts them on new functional entities (SPEC-10 §4.5, SPEC-20 §5.2, SPEC-21 §5.2, SPEC-22 §5.1). This is the right pattern, and several proposals below generalise it.
- **Spawn hooks** (`IGridSpawnHook`, SPEC-10 decision 11) let higher modules tag new entities without Voxel knowing them.
- **`DamageStreams`** accepts any number of producers in registration order (SPEC-13 §4.4).
- **`ProjectileStore`** (rows, not entities) and the **debris tier ladder** (particles, then chunk bodies, then hulk grids) scale.
- **Init by flags.** `PowerInitSystem`, `WeaponInitSystem` and `ShipStatsAggregateSystem` fill zeroed components on the first tick (`Initialized` flags). Spawners only add *types*, which makes spawning data-only (used by X3).
- **`ShipCommand`** is the single control interface for player and AI ships. **`ShipStatModifier`** carries multiplicative effects.
- **Content is data-driven** and the condition/effect vocabulary is fail-closed.

### 1.2 What the probes hit today

| Touchpoint | P1 | P2 | P3 | P4 | Why it is an edit, not a registration |
|---|---|---|---|---|---|
| `FunctionKind` enum + `FunctionParams` fat struct (SPEC-04 §4.2, §4.6) | ✔ | | | ✔ | Closed enum with one sub-struct per kind; exactly one function per block |
| Functional prototype table "8 entries", "`FunctionKind` 1..7" (SPEC-10 §4.5, §5.7) | ✔ | | | ✔ | Fixed-size `FixedList` indexed by kind; a weak implementer hard-codes 8 |
| `PowerInitSystem` / `PowerAllocationSystem` (SPEC-22 decision 6, §5.2, §5.3) | ✔ | ✔ | | ✔ | Closed `PowerConsumerKind`; demand derived centrally from weapon, shield and thrust inputs |
| `ShipStatsAggregateSystem` + monolithic `ShipStats` (SPEC-22 §4.2, §5.4) | ✔ | ✔ | | ✔ | One `switch (Function.Kind)` and one field set per feature |
| `ShipSpawnService.Spawn` step 4 (SPEC-01 §5.7) | ✔ | | | | Hard-coded setup list, main thread only: a launch from the fixed step cannot use it |
| `AbilitySystem` (SPEC-24 §5.1 steps 6–7) + AI `Edges = 0` (SPEC-23 decision 6) | ✔ | | | ✔ | Kind `switch` at activation; **only the player ship can ever activate abilities** |
| `WeaponFireSystem` delivery `switch` + fat `WeaponDefinition` (SPEC-21 §4.1, §5.5) | | ✔ | | | Closed `WeaponDelivery`; every delivery's fields on every weapon |
| `ShieldHitStreams` (SPEC-22 §4.3) | | ✔ | | | Exactly three producer slots, `ShieldHitReadSet` has three named readers |
| `DamageConfig` named fields (`MulKinetic` … `KickRam`, SPEC-13 §4.5) | | ✔ | ✔ | | New damage type = new struct fields + `Multiplier` edit |
| Cell writes only from damage (SPEC-13 decision 1; `Amount <= 0` dropped) | | | ✔ | | No restore channel: repair is impossible at runtime (SPEC-40 Q10 leaves `repair_ship` a warning) |
| Disabling a block: `Powered` has one writer (SPEC-22 §5.3) | | ✔ | ✔ | ✔ | No inhibit input: EMP/ion/hacking must edit the power allocator |
| `PhysicsVelocity` writer list (architecture §8) | | | | ✔ | Tractor beam = new writer = ADR, although `ImpactEventSystem` already applies `PendingImpulse` |
| Attribution: `Source` = shooter root (SPEC-13 §4.2, SPEC-33 provocation, `ShipDestroyed`) | ✔ | ✔ | | | A fighter's kill or provocation is credited to the fighter, not to its carrier or the player |
| `GridSpawnRequest` from the fixed step is applied next **frame** (SPEC-10 §5.7, init group) | ✔ | ✔ | | | Launching through a request is frame-rate dependent and breaks the 30/60/144 FPS determinism gate |
| Swarm cost (06 gates: 30 ships, 50 AI ships) | ✔ | | | | No gate for many small grids; AI thinks per ship (fleets are M4) |

**Result:** today a fighter bay edits **9 specs' existing code paths**: SPEC-01, 04, 10, 12, 13/33, 22, 23, 24 and 06. After the proposals below it edits **none**. It adds one new spec plus registration rows (§4).

---

## 2. Proposals

Each item: problem → proposal → cost now → what it buys. "Cost now" is documentation only: no game code exists yet, so every item is cheapest today.

### X1 — Block function modules (ADR)

**Problem.**
- `FunctionKind` is closed and allows exactly one function per block.
- `FunctionParams` carries every kind's sub-struct on every block.
- `GridSpawnSystem` keeps one functional prototype per kind in a fixed list.
- Two central SPEC-22 systems `switch` on the kind.
- SPEC-51 categories and SPEC-80 art mapping also key on it.
- Combined blocks are impossible: a weapon with its own capacitor, a core that projects a shield, a hangar that draws power.
- `UtilityKind` is a second closed enum inside the first. Two of its three values have no behaviour anywhere (X11).

**Proposal.**
- **Modules list.** `BlockDefinition` gets `[SerializeReference] BlockModule[] modules`. `BlockModule` is an abstract class in `Game.Data.Blocks`. Each concrete module is designed by its owning spec:

  | Module | Owning spec |
  |---|---|
  | `CoreModule`, `ExplosiveModule` (reactor blast) | SPEC-13 |
  | `PowerSourceModule`, `PowerConsumerModule`, `ShieldEmitterModule` | SPEC-22 |
  | `ThrusterModule` | SPEC-20 |
  | `WeaponMountModule` | SPEC-21 |
  | `CargoModule` | SPEC-50 |
  | `AbilityGrantModule` | SPEC-24; replaces the DATA-010 `grantedAbilities` field |
  | later `HangarModule` | new spec |

- **Module IDs.** `BlockModuleId` is a `byte` from one ordered list in `Game.Data` (add at end only). `BlockDef` keeps `ulong ModuleMask` and a display-only `BlockCategory` (editor palette, art mapping), and drops `FunctionParams`.
- **Baking.** Each module type has a `BlockModuleBaker` that writes its own blob table: `BlobArray<TParams> Rows` + `BlobArray<int> RowByBlock` (−1 = absent). A Burst system reads only the table of the module it owns.
- **Registration and prototypes.** `FunctionalComponentRegistration` is keyed by `BlockModuleId`. `GridSpawnSystem` caches one functional prototype per distinct `ModuleMask` in a `NativeHashMap<ulong, Entity>`, so there is no fixed table.
- **Kinds.** `BlockKind.Functional` stays as is: a block is Functional iff it has a module that needs an entity.

**Cost now.**
- SPEC-04: §4.5, §4.6, §5.5 and validation rules.
- SPEC-10: §4.5 and §5.7 step 1/5c.
- SPEC-20, 21, 22 and 24: read their module table instead of `def.Function.X`.
- SPEC-51: category from `BlockCategory`.
- SPEC-72 MIG-006: writes modules.
- SPEC-80: mapping by category.
- About 15 task texts change; no task count change.

**Buys.** A new block function is a new module class, a baker and a registration. Mixed blocks become free.

*Minimal alternative if X1 is rejected:* keep `FunctionKind`, but size the prototype table from the enum count. Also turn the SPEC-22 switches into per-kind contribution jobs (X2). Single-function blocks remain.

### X2 — Contribution protocols for power and stats (ADR)

**Power: problem.**
- SPEC-22 decision 6 derives demand inside `PowerAllocationSystem` by reading `ShipCommand.FireGroups`, thrust and `ShieldControl`.
- `PowerConsumerKind` is `{Weapon, ShieldEmitter, Thruster}`.
- A hangar, tractor beam or cloak must edit the allocator.

**Power: proposal.**
- **Consumer data.** Every block with a `PowerConsumerModule {Draw, Priority}` becomes a consumer; the data comes from the module, not the kind.
- **Demand.** Demand is an enableable `PowerRequest` on the functional entity, written by the one system that owns that block's feature. Those writers sit in a new `PowerDemandGroup`, at the end of `ShipCommandGroup`, before `PowerAllocationSystem`.
  - `WeaponFireSystem`'s demand job writes it from fire groups or autofire.
  - Propulsion writes it from thrust.
  - Shields write it from `ShieldControl`.
  - A hangar writes it from its launch queue.
- **Allocator.** The allocator reads only enable bits.
- **Guarantees kept.** Same-tick response stays, because demand is written before allocation in the same tick. The single writer per field stays: one feature owns each module type's request bits.
- **Abilities.** One-shot ability costs stay as they are.

**Stats: problem.** `ShipStats` holds every feature's totals, and one aggregate job switches over all kinds.

**Stats: proposal.**
- **What stays in `ShipStats`.** Only the shared part: mass, `MassFraction`, bounds, `FunctionalSignature`/`FunctionalRevision`, and multipliers as an array indexed by `StatId`.
- **Feature-owned stats.** Each feature owns its totals and recomputes them with a change filter when `FunctionalRevision` changes:

  | Component | Owning spec | Contents |
  |---|---|---|
  | `PropulsionStats` | SPEC-20 | thrust, torque, accelerations, turn rate |
  | `WeaponStats` | SPEC-21 | DPS, max range, counts |
  | `PowerStats`, `ShieldStats` | SPEC-22 | |
  | later `HangarStats` | new spec | |

- **Readers.** SPEC-23 reads `WeaponStats.MaxWeaponRange` and `PropulsionStats.ForwardAccel`. SPEC-61 is unchanged (`MassFraction`).

**Cost now.**
- SPEC-22: §3 decision 6, §4.1, §4.2, §5.2–5.4.
- SPEC-20 and SPEC-21: one aggregate job and one demand job each.
- SPEC-23, 24 and 61: reader lines.
- Architecture §5: add `PowerDemandGroup`. §8: replace the `ShipStats` row with one row per stats component.

**Buys.** A new consumer or stat is local to its feature. The 30-ship gate also improves, because a weapon loss recomputes only `WeaponStats`.

### X3 — Ship composition by registration, and tick-exact launch (ADR)

**Problem.**
- **Composition is a hard-coded list.** `ShipSpawnService.Spawn` step 4 (SPEC-01 §5.7) adds `CombatGridSetup`→Power→Ability, Planar, Flight, AI and `StarSystemMember` in managed code, and it is "never called inside a system update". Every new ship-level feature edits it.
- **No Burst spawner can build a ship.** A hangar or a reinforcement wave cannot produce one.
- **A spawn request from the fixed step is not tick-exact.** `GridSpawnSystem` converts requests in `GameInitGroup`, once per frame. A request recorded at tick N is applied after 0–6 further ticks depending on frame rate, so it would break the determinism gate (06).

**Proposal.**
1. **`ShipProfile` registration.**
   - `ShipProfile` is a `byte` with values PlayerShip, NpcShip, Station, SmallCraft (add at end).
   - `RootComponentRegistration {ShipProfile, ComponentType}` is a singleton buffer, the root-level twin of `FunctionalComponentRegistration`. Each spec appends its root types from a registration system.
   - `GridSpawnRequest` gains `ShipProfile Profile`. `GridSpawnSystem.ConvertToRoot` adds the profile's `ComponentTypeSet` in its existing batch.
   - Per-ship values that are not derivable go into one unmanaged `ShipSpawnData {Allegiance; ushort AiProfileIndex; Entity Owner; ulong OwnerPersistentId}` on the request. Every owner initialises from zero plus `ShipSpawnData` on the first tick. `AiSetup`'s anchor-from-pose moves into an `AiInit` step.
   - `ShipSpawnService` becomes a thin managed wrapper that writes the same request.
2. **Stowed grids for tick-exact entry.**
   - A carrier's craft are spawned normally when the carrier spawns (time-sliced, outside combat timing) and then **stowed**: `ecb.SetEnabled(root, false)` disables the whole `LinkedEntityGroup`, so physics, rendering, the spatial hash and every system skip them.
   - **Launch.** The owning fixed-step system records, in the GameplayEnd ECB, `SetEnabled(root, true)` plus the pose and initial velocity. This extends the initialisation exemption that architecture §8 gives new bodies to re-enabled ones. The craft exists at the end of tick N at any frame rate.
   - **Recovery.** Recovery is the reverse. Rebuilding destroyed craft uses the normal (frame-timed) spawn path, which is fine because a rebuild timer is not combat-critical.
3. **`GridSpawnFlags.Transient`.** `PersistentId == 0` becomes legal (no warning). The grid is excluded from save capture and from `StarSystemDelta`, and destroyed on unload and load. Craft that are out of the bay when the game saves are counted back into the carrier's bay record (saves already require no combat).

**Cost now.**
- SPEC-01: §4.4, §5.7 and FND-012.
- SPEC-10: §4.4, §5.7 step 5b and §7.
- SPEC-20, 21, 22, 23, 24 and 31: replace `*Setup.Add` helpers with registration systems.
- SPEC-05: transient exclusion.
- Architecture §7.1: rewrite.
- **Note.** SPEC-33 encounter reinforcements spawned mid-combat have the same frame-timing issue. With this in place they can use stowed waves or accept frame timing explicitly.

**Buys.** Every spawner produces identical ships, including Burst ones. New ship-level features register types. Launches are deterministic and cost nothing at launch time.

### X4 — Every single-writer state gets a request channel (ADR)

**Problem.** The single-writer rule (architecture §8) is right, but several owned states have no published way for *another* feature to ask for a change, so new features must edit the owner:

- **Impulses.** Only `PropulsionSystem`, `ImpactEventSystem` and others may touch `PhysicsVelocity`. A tractor beam, launch catapult or gravity well needs an ADR.
- **Cells.** Only damage can change cells, so runtime repair is impossible (SPEC-40 Q10).
- **Blocks.** `Powered` is written only from power state, so EMP, ion or hacking must edit SPEC-22.
- **Stat modifiers.** `ShipStatModifier` is written only by `AbilityModifierSyncSystem`, so a nebula slow or status effect must edit SPEC-24.

**Proposal.** Four channels, each consumed by the existing owner. The owner stays the only writer; producers only append. All four use X8's generic channel.

| Channel | Producers (examples) | Consumer (unchanged owner) | Rule |
|---|---|---|---|
| `ImpulseRequest {Entity Body; float3 WorldImpulse, WorldPoint; ProducerKind; uint Sequence}` | Damage knockback (replaces `PendingImpulse` writes), tractor beams, launch catapults, gravity wells | `ImpactEventSystem` applies them next tick in sorted order | §8 `PhysicsVelocity` row: "impulses from the channel"; no new writers ever |
| `CellRestoreRequest {Entity Grid; int3 CellMin, CellMax; float HpAmount; byte Mode}` (Mode: heal existing cells, or re-add missing cells from `GridSource`) | Repair beam, repair drones, station repair service, `repair_ship` effect | `DamageResolveSystem` (still the only cell writer) runs a restore pass after the detach pass | Re-added cells go through the same `BrickWrite.Set`, so meshes, colliders and mass update incrementally |
| `InhibitRequest {Entity Target; uint UntilTick; byte Reason}` (Target: functional entity or grid root, meaning all its blocks) | Ion and EMP hits, hacking, overheating | A `StatusSystem` writes `InhibitedUntilTick`; `PowerAllocationSystem` job B computes `Powered = powered && tick ≥ InhibitedUntilTick` | `Powered` keeps one writer; its meaning becomes "operational" |
| `StatModifierSource` (buffer per source kind: abilities, status effects, environment) | Abilities (SPEC-24), status effects, nebulae | `StatModifierSyncSystem` (renamed from `AbilityModifierSyncSystem`, now neutral) merges the sources into `ShipStatModifier` | `StatId` stays add-at-end |

**Cost now.**
- SPEC-13: §4.3 and §5.2; restore pass in §5.3.
- SPEC-22: §5.3 job B.
- SPEC-24: §5.3.
- Architecture §8: four rows reworded.
- SPEC-40: `repair_ship` becomes real (Q10 answered by this).

**Buys.** P3 and P4 need no owner edits. Repair, EMP, tractor beams and nebulae become ordinary features.

### X5 — Lifecycle core + typed handlers for weapons and abilities (errata; one bug)

**Bug (apply regardless).** Since SPEC-24 §5.1 step 6 handles only the player ship and SPEC-23 decision 6 writes `Edges = 0`, **AI ships can never use abilities**: no shield toggle, no boost, no "launch fighters".

**Fix.**
- Remove the player-only condition.
- Add `AiAbilityRule[]` to `AiProfileDefinition`: `{slot category, trigger: EngageStart | HullBelow x | Fleeing | ShieldBelow x}`.
- `AiDecisionSystem` sets ability edges on think ticks, and `AiSteeringSystem` writes them into `ShipCommand.Edges` for that tick.

**Weapons.**
- **Generic core.** `WeaponFireSystem` keeps aim, cooldown, burst, power and `Powered` gating, and emits `WeaponShot {Entity Weapon, Grid; float3 Muzzle, Dir; uint ShotId; ushort Def}` rows.
- **Delivery handlers.** Each `WeaponDelivery` has a small handler system:
  - `ProjectileShotHandler`: appends to `ProjectileStore` (projectile and missile);
  - `BeamShotHandler`: starts a pulse;
  - later `MineShotHandler`, and an ion delivery that sends `InhibitRequest`s.
- **Definition params.** `WeaponDefinition` keeps the shared fields. Delivery-specific fields move into `[SerializeReference] WeaponDeliveryParams` subclasses, the same module idea as X1.

**Abilities.**
- `AbilitySystem` handles only the generic lifecycle: charges, cooldown, power handshake, and duration from the definition.
- One effect system per `AbilityKind` reads `ActivatedThisTick`. This is already true for `ShieldToggleEffectSystem`.
- Kind-specific preconditions (the shield threshold) are published by the effect system as `AbilitySlot.Blocked`, one tick earlier.

**Cost now.**
- SPEC-21: §5.5, plus a split into two handler sections.
- SPEC-24: §5.1 steps 6–7.
- SPEC-23: §4.2, §5.4 and §5.5.
- Three new tasks.

**Buys.** A new delivery or ability kind is one handler system plus data.

### X6 — Instigator (who is responsible) (errata)

**Problem.** `DamageEvent.Source` and `ShieldHit.Source` are the shooter root. With launched craft, mines or drones:
- SPEC-33 provocation (`LastDamageSource == player`) misses the player's own fighters;
- `ShipDestroyed` cannot credit the carrier;
- an allied craft's allegiance must be copied by hand.

**Proposal.**
- **`OwnedBy`.** `OwnedBy {Entity Owner; ulong OwnerPersistentId}` goes on any root launched or spawned by another (X3 `ShipSpawnData.Owner`). Allegiance is inherited at spawn.
- **Producers.** Producers resolve `Instigator = OwnedBy.Owner ?? Source` once per shot (stored in `ProjectileRow` and `WeaponShot`).
- **New fields.**
  - `DamageEvent`, `ShieldHit`, `ExplosionRequest`: `Entity Instigator`, added at the end. The sort key is unchanged.
  - `GridDamageStats`: `LastDamageInstigator`.
  - `ShipDestroyed`: `KillerInstigatorPersistentId`.
- **Readers.** SPEC-33 provocation and kill credit read the instigator.

**Cost now.**
- SPEC-13: §4.2 and §4.3.
- SPEC-21: §4.6, §4.6b and §5.6–5.8.
- SPEC-22: §4.3.
- SPEC-33: two lines.

Cheap now; later it would touch every producer and the save of stats.

### X7 — Small-craft tier, group AI and gates (owner decision)

**Representation (decide now).**
- **(a) Recommended: voxel small craft.** Each craft is a normal grid, usually a single brick: destructible like everything else, with no new rendering, physics or damage path. Fixed cost per craft is one root plus one brick plus a few functional entities, with a compound collider of one child. Estimate: about 20–30 KB and a few µs per tick across systems.
- **(b) Fallback: a `DroneStore`.** Struct-of-arrays rows like `ProjectileStore`, with steering, HP and a tiny weapon through `CombatHitTest`; not voxel-destructible. Keep it documented as the escape hatch if (a) misses the gate. Do not build both.

**Group AI (pull forward from M4).**
- Generalise `FleetMember` (SPEC-23 §4.4) into `GroupMember {Entity Leader; byte Slot; ushort FormationIndex}`, available from M3.
- Only the group leader (or the carrier's group brain) runs target choice. Members steer to formation slots or attack the leader's target with a staggered offset. AI decision cost becomes O(groups), not O(craft).
- Player orders to the player's own craft (attack my target, return) are an `GroupOrder` on the leader. They are written by the new feature's system from a new input edge (SPEC-03 adds actions at the end, as with `Cruise`).

**Launch collisions.** `PhysicsLayers` (SPEC-12, the only place filters are defined) gets `WithGroup(filter, int groupIndex)`. Carrier and craft share a negative `GroupIndex` during launch and recovery windows, which Unity Physics treats as "never collide".

**Gates (06).** Add:
- "Swarm: 30 ships + 100 small craft (≤ 1 brick each), all fighting: main thread ≤ 10 ms median; AI ≤ 1.5 ms including craft";
- "Launch 20 craft in one tick: no frame over 8 ms".

**Cost now.**
- SPEC-23: §4.4, §5.7, and fleet tasks moved to M3.
- SPEC-12: one helper.
- 06: two rows.
- SPEC-71: one perf test.

### X8 — One event channel type and one guarded-access type (errata, redundancy)

**Problem.**
- **Three hand-written stream registries with different shapes.** `DamageStreams` (open producers), the explosion half of it (open), and `ShieldHitStreams` (exactly three producers, named reader fields, its own unsafe data struct).
- **Three producer enums for one concept.** `DamageProducer`, `ShieldHitProducer` and `CombatProducerKind`.
- **Five singletons each re-implement handle guarding.** `ProjectileStore` (`Acquire`/`Release`/`CompleteAccess`), `SpatialHash` (`WriteHandle`/`ReadersHandle`), `BrickMeshQueue` and `ColliderDirtyQueue` each have their own variant.
- **Weak implementers.** Each copy is another place for a weak implementer to create a sync point or a race. The integration pass fixed several of exactly these (items 30, 38).

**Proposal.**
- **`SimEventChannel<T>`.** Add it to Core (SPEC-02):
  - open producer registration in call order;
  - `BeginWrite(ref state, ProducerKind, forEachCount)` / `EndWrite`;
  - consumer `BeginRead` returns every registration in order, then `EndRead` disposes;
  - the sort-key contract `(Grid.Index, Source.Index, ProducerKind, Sequence)` is stated once.

  Damage, explosion, shield hits and X4's four channels are all instances. One `ProducerKind` enum lives in `Game.Data`.
- **`GuardedHandles`.** Add a Core struct `GuardedHandles {Acquire, Release, BeginRead, EndRead, CompleteAll}`, which every native-memory singleton embeds.

**Cost now.**
- SPEC-02: new §4.12 with one task.
- SPEC-13 §4.4 and SPEC-22 §4.3 shrink to "an instance of".
- SPEC-11, 12, 21, 23, 63 and 64: reference the helper.
- Net: less specification and less code.

### X9 — Remove mirror enums (errata, redundancy)

`WeaponDamageType` mirrors `DamageType` (SPEC-21 §4.1), and `AbilityStat` mirrors `StatId` (SPEC-24 §4.1). Both exist only because the real enum lives above `Game.Data`, and both need an equality test.

Move `DamageType`, `StatId` and `ProducerKind` into `Game.Data` (`Game.Data.Combat`), which Voxel and Simulation both reference, and delete the mirrors and their tests. This is an architecture §6.3 table edit of the owning module only; the names stay.

### X10 — Damage and armour as tables (errata)

`DamageConfig` has named `MulKinetic`/`MulEnergy`/`MulExplosive`/`MulRam` and `KickKinetic` … `KickRam` fields, and `ArmorClass` is fixed at 0..3.

**Proposal.**
- Add `DamageTypeDefinition` (prefix `dmg_`): per armour class multiplier, kick, shield multiplier, and flags `BypassShields` (ram) and `NoCellDamage` (ion: the hit only produces an `InhibitRequest`).
- Add `ArmorClassDefinition` (prefix `armor_`).
- Bake both into a `DamageTable` blob indexed `[type × armorClass]`.

`DamageType` stays an add-at-end enum, but a new type is a content row and one enum value, with no struct or method edits.

**Cost now.** SPEC-13 §4.5, §4.7 and DMG-003; SPEC-04 prefix table; SPEC-72 writes the four rows.

### X11 — Dead contract: `UtilityKind.BeamCombiner` and `ProjectilePortal` (errata, bloat)

**Problem.**
- SPEC-04 §4.6 defines both utility kinds and their parameters, but says no v1 spec implements them (SPEC-04 Q6).
- SPEC-72 still migrates three legacy blocks into them.
- The result is shipped blocks that do nothing, plus contract surface every implementer must carry.

**Proposal.**
- Remove both kinds and their fields from the v1 contract.
- SPEC-72 migrates those blocks as non-functional props, keeping the mesh and the item, and lists them in the migration report as "behaviour deferred".
- With X1 they come back later as ordinary modules without touching anything.

### X12 — Notes for later (no change now)

- **Point defence against projectiles.** Projectiles are not in the spatial hash, and `ProjectileRow` has no HP. Point-defence guns against grids, including fighters, work today; against missiles they need a per-tick projectile grid. Record this as a SPEC-21 open question. Reserve `ProjectileFlags.Targetable` (add at end) now so the row layout is not reshuffled later.
- **HUD for new systems.** SPEC-61 should expose an `IShipSystemPanel` registry (view model + UXML) so a hangar panel is a registration, not a HUD edit. Record as a SPEC-61 open question.

---

## 3. Recommendation

| Item | Kind | Recommend | Why now |
|---|---|---|---|
| X5 AI ability use | errata (bug) | **Apply** | AI is currently unable to use any ability |
| X6 Instigator | errata | **Apply** | One field now versus every producer later |
| X9 Mirror enums | errata | **Apply** | Pure deletion |
| X11 Dead utility kinds | errata | **Apply** | Pure deletion |
| X8 Channel + guarded access | errata | **Apply** | Removes the most error-prone duplicated code before anyone writes it |
| X10 Damage tables | errata | **Apply** | Small, data-only |
| X1 Block modules | ADR | **Approve** | Root of most rigidity; the most expensive one to change after DATA/VOX tasks start |
| X2 Power/stats contributions | ADR | **Approve** | Removes the two central switches |
| X3 Profiles + stowed grids | ADR | **Approve** | Needed for any deterministic in-combat spawn |
| X4 Request channels | ADR | **Approve** | Makes repair, EMP, tractor beams and environment effects possible without edits |
| X7 Small craft | owner decision | **(a) voxel craft + group AI + gates** | Decides the swarm cost model before SPEC-23 is built |
| X12 | notes | Record as open questions | — |

Order of application: X9 → X8 → X1 → X2 → X3 → X4 → X6 → X5 → X10 → X11 → X7. Contracts come first so later items reference the new types. Each step keeps `check-docs` at 0 errors and is one commit.

## 4. The fighter bay after the proposals

A new spec (for example SPEC-25 "Carriers and small craft", area `CRF`, M3 or M4) **adds**:

- **Content.** `HangarModule {capacity, craftBlueprint, launchIntervalSeconds, recoverRadius, rebuildSeconds, groupFormation}` with a baker and a registration. `PowerConsumerModule` and `AbilityGrantModule(ability_launch_wing, ability_recall_wing)` go on the same block (X1).
- **Systems.**
  - `HangarSystem` (fixed step). It spawns stowed craft when the carrier spawns, through `ShipProfile.SmallCraft`, `Transient` and `ShipSpawnData.Owner` (X3). It launches with `SetEnabled` and initial pose/velocity, at tick-exact times. It recovers and rebuilds on a timer, and writes `PowerRequest` while launching (X2).
  - `HangarStats` aggregation (X2).
  - `LaunchWingEffectSystem` and `RecallWingEffectSystem` (X5). AI uses them through `AiAbilityRule` (X5).
- **Behaviour and presentation.**
  - Craft fight as a `GroupMember` wing with group thinking (X7) and credit kills to the carrier through `OwnedBy` (X6).
  - Launch collisions use `PhysicsLayers.WithGroup` (X7).
  - A HUD panel via `IShipSystemPanel` (X12).
  - A save participant stores per-carrier bay counts.
- **Registration rows elsewhere.** `BlockModuleId.Hangar`, `AbilityKind` values, `ShipProfile.SmallCraft` (if not already present), one SPEC-01 boot-singleton row, and a §5 placement row.

No existing system's code path changes. The same holds for P2 (a new handler plus data), P3 (`InhibitRequest`/`CellRestoreRequest` producers plus a `DamageTypeDefinition`) and P4 (an `ImpulseRequest` producer, `StatModifierSource`, or `InhibitRequest`).

## 5. Outcome (2026-10-10)

| Item | Commit | ADR | Applied as proposed? |
|---|---|---|---|
| X9 Mirror enums | `e3e6c86` | — | Yes. `DamageType` and `StatId` are in `Game.Data.Combat` |
| X8 Channel + guarded access | `05b264e` | — | Yes. `SimEventChannel<T>` and `GuardedHandles` live in `Game.Data.Streams` (SPEC-02 §4.12, new task CORE-012), because Core may not declare `SystemState` users. The singletons keep their `Acquire`/`Release`/`CompleteAccess` names |
| X1 + X11 Block modules, dead utility kinds | `c14cc48` | 0013 | Yes, with two staging choices: `WeaponMountModule` exists from M0 with the muzzle, and DATA-010 adds the `weapon` link at M2 (weapon blocks stay functional before M2); ability grants stay a block list. Portal and mirrors migrate as props |
| X2 Power and stats contributions | `6cbe31a` | 0014 | Yes. Demand is a `PowerRequest` buffer element per consuming module (a block may hold several), with `ConsumerIndex` bound by `PowerInitSystem`. New task WPN-018 |
| X3 Profiles, stowed and transient grids | `a7806e9` | 0015 | Yes. `Allegiance` is deliberately in no profile (its zero value is the player faction). **Correction:** SPEC-31 already gave armed stations combat and turret AI by calling the helpers; the issue was a second copy of the sequence, not missing components |
| X4 Request channels | `f57d425` | 0016 | Yes. Restores have two modes, `Heal` and `Place` (one palette cell); recreating destroyed functional blocks stays the editor's job. `StatusSystem` (SPEC-22) owns inhibits and stat modifiers. `PostFromMainThread` added for narrative/dev producers. New tasks DMG-019, PHY-022, PWR-010 |
| X6 Instigator | `e0d0c8e` | — | Yes. `OwnedBy` lives in `Game.Data.Components`, so SPEC-13 rams can read it. `ShipDestroyed` gained killer fields |
| X5 AI abilities and lifecycles | `881f68a` | — | Yes, with one correction and one reduction. **Correction:** AI not using abilities was SPEC-23's open question 1 with the default "no", not an oversight; it is now answered "yes" through per-profile `AiAbilityRule`s. **Reduction:** the v1 deliveries keep their flat `WeaponDefinition` fields, and only new deliveries use the optional `[SerializeReference] WeaponDeliveryData`. This avoids reshuffling SPEC-72's weapon migration |
| X10 Damage tables | `36177a0` | — | Yes. `dmg_*` and `armor_*` content and `DamageTableBlob`; `PowerTuning.ShieldTypeMultiplier` moved into the table |
| X7 Small craft | `a2840ef` | 0017 | Yes, with one simplification: launch overlap uses SPEC-13's existing `SplitGrace` contact suppression (set by `Unstow`) instead of a collision-filter group index. Grid colliders are cached per brick and have a single writer, so changing filters would have added a writer. `FleetMember` keeps its name and now covers wings |
| X12 Notes | `6b3ee37` | — | SPEC-21 Q10 (point defence; `ProjectileFlags.Targetable` reserved) and SPEC-61 Q19 (ship-system HUD panels) |

`check-docs` after every commit: 33 specs, 0 errors (496 tasks at the end).
