# ADR-0016 — Every single-writer state publishes a request channel

**Status:** Accepted (owner, 2026-10-10; extensibility review X4)

## Context
The single-writer rule (architecture §8) is right, but four owned states gave other features no way to ask for a change:
- **Velocity.** `PhysicsVelocity` impulses came only from a closed list of systems, so a tractor beam, a launch catapult or a gravity well needed an ADR.
- **Cells.** Only damage changed cells, so runtime repair was impossible (SPEC-40 Q10).
- **Block disabling.** `Powered` followed power state only, so EMP, ion or hacking had to edit SPEC-22.
- **Stat modifiers.** `ShipStatModifier` was written only from ability slots, so a nebula slow or a status effect had to edit SPEC-24.

## Decision
Each owner keeps sole write access and consumes a `SimEventChannel<T>` (SPEC-02 §4.12) that any feature may produce into. All four channels sort by `(target Index, ProducerKind, Sequence, gather position)`.

1. **`ImpulseRequest {Entity Body; float3 WorldImpulse, WorldPoint; ProducerKind Producer; uint Sequence; byte Flags}`.**
   - The channel is held by SPEC-13 `DamageStreams.Impulses` and applied by `ImpactEventSystem`, which is already a listed impulse writer, in the same pass and with the same planar clamp as the damage knockback.
   - Requests registered before `ImpactEventSystem` in tick N apply in tick N; later ones apply in tick N+1.
2. **`CellRestoreRequest {Entity Grid; CellRestoreMode Mode; int3 CellMin, CellMax; float HpFraction; ushort Block; byte Orient, Color; ProducerKind Producer; uint Sequence}`.**
   - The channel is held by `DamageStreams.Restores` and consumed by `DamageResolveSystem` (still the only cell writer) in a restore pass after the detach pass, under `DamageConfig.MaxRestoreCellsPerTick`.
   - **`Heal`** raises the HP of the solid cells and the function HP of the live instances in a box by a fraction of their maximum.
   - **`Place`** puts one palette block into one air cell of an existing brick. Restored cells are published as `CellRestored`, and SPEC-12 adds their mass.
   - Recreating destroyed functional blocks stays the editor's job (SPEC-51), because it needs structural changes and inventory.
3. **`InhibitRequest {Entity Target; uint DurationTicks; byte Reason; ProducerKind Producer; uint Sequence}`.**
   - The target is a functional entity, or a grid root meaning all of its blocks.
   - SPEC-22's new `StatusSystem` consumes it and writes `Inhibited {uint UntilTick; byte Reason}`. That component is registered on every functional entity through `FunctionalComponentRegistration` with `Module = None`, meaning all modules.
   - `PowerAllocationSystem` treats an inhibited block as not alive, so it draws nothing and its `Powered` bit is off. Every feature already gates on `Powered`.
4. **`StatModifierRequest {Entity Root; StatId Stat; ProducerKind Producer; ushort SourceId; float Multiplier; uint UntilTick; uint Sequence}`.**
   - `StatusSystem` consumes it and becomes the only writer of `ShipStatModifier`. It upserts by `(Producer, SourceId, Stat)`, removes an entry on `Multiplier == 1` or expiry, and drops expired entries every tick.
   - SPEC-24 abilities post requests on activation and cancellation. `AbilityModifierSyncSystem` and `AbilityModifiersDirty` are removed.
5. **Main-thread producers.** Narrative effects, dev commands and services post through `SimEventChannel<T>.PostFromMainThread` (SPEC-02). The post lands in an inbox the consumer reads first at its next `BeginRead`. This path is not frame-rate deterministic and is used only for non-replayed actions.

## Consequences
- Repair beams and drones, station repair, `repair_ship`, EMP and ion weapons, tractor beams, gravity wells, launch catapults, nebulae and slow fields become producers. None of them edits an owner.
- **Architecture §8.** The `PhysicsVelocity`, cell, `ShipStatModifier` and new `Inhibited` rows name their channel. The list of velocity writers is final: new mechanics use the impulse channel.
- **SPEC changes:**
  - SPEC-02: `PostFromMainThread`, `ProducerKind` values.
  - SPEC-10: `Module = None` registration.
  - SPEC-12: mass of restored cells.
  - SPEC-13: impulses and restores.
  - SPEC-22: `StatusSystem`, `Inhibited`.
  - SPEC-24: abilities post modifiers.
  - SPEC-40: `repair_ship` heals.
- **Cost:** four channels with small per-tick consumers, each O(requests); no work when idle.

## Alternatives considered
- **Add writers to §8 per feature (one ADR each).** Rejected: this is the friction the review found.
- **A generic "status effect" entity per effect.** Rejected: structural changes per hit, and an unbounded entity count under fire.
