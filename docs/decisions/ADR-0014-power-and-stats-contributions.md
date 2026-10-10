# ADR-0014 — Power demand and ship stats are contributed by each feature

**Status:** Accepted (owner, 2026-10-10; extensibility review X2)

## Context
Two problems in SPEC-22 forced every new ship feature (hangar, tractor beam, cloak) to edit it:
- **Power demand.** `PowerAllocationSystem` derived demand itself from `ShipCommand.FireGroups`, thrust and `ShieldControl`, and knew a closed `PowerConsumerKind {Weapon, ShieldEmitter, Thruster}`.
- **Ship stats.** `ShipStatsAggregateSystem` summed every feature's totals into one monolithic `ShipStats` with one field set per feature.

## Decision
1. **Power requests are feature-owned.** A functional entity whose block has a power-consuming module carries a `PowerRequest` buffer: one element per consuming module of that block, `{BlockModuleId Module; byte Priority; byte Requesting; float Draw}`.
   - The feature that owns the module writes its element: `Draw` and `Priority` once at init (in `GameInitGroup`, before `PowerInitSystem`), and `Requesting` every tick.
   - It writes `Requesting` from a system in **`PowerDemandGroup`**, a new group declared by SPEC-22 in `GameplayPrePhysicsGroup` that runs after `ShipCommandGroup` and before `PowerAllocationSystem`, so demand still answers the trigger in the same tick.
   - `PowerRegistration.RegisterConsumer(module)` registers `PowerRequest`, `PowerLink` and `Powered` for a module id.
2. **The allocator is kind-agnostic.** `PowerInitSystem` builds the per-ship consumer list from the `PowerRequest` elements. `PowerAllocationSystem` reads only `Requesting`, grants by priority, and writes `Powered`. A block with several consumers is powered only when all of them are granted. Ability one-shot costs keep their existing per-ship channel (`AbilitySlot.PendingPower`).
3. **Shared ship stats stay small.** `ShipStats` keeps only what every feature shares: mass, mass fraction, bounds, the alive-functional signature and `FunctionalRevision`. Multipliers move to `ShipStatMultipliers`, an array indexed by `StatId`, so a new stat is a new enum value and not a new field.
4. **Feature stats are feature-owned.** Each feature that has ship-level totals owns one `<Feature>Stats` root component and one stats system after `ShipStatsAggregateSystem`. The system recomputes when `ShipStats.FunctionalRevision` differs from the component's `SourceRevision`, and recomputes its mass-derived values when mass or multipliers change.

   | Component | Owning spec |
   |---|---|
   | `PropulsionStats` | SPEC-20 |
   | `WeaponStats` | SPEC-21 |
   | `PowerStats`, `ShieldStats` | SPEC-22 |

## Consequences
- A new consumer (a hangar drawing power while launching) registers its module, fills its element at init and writes `Requesting` in `PowerDemandGroup`. A new stat is a `StatId` value plus the feature's own stats component. No SPEC-22 system changes.
- A weapon loss recomputes only `WeaponStats` and `PowerStats` work, so the hot path under fire gets cheaper.
- **Architecture §5:** `PowerDemandGroup` is added to the tree.
- **Architecture §8:** `PowerRequest` gets a row per field. The single `ShipStats` row becomes the shared part plus one row per feature stats component.
- **Readers:** SPEC-23 reads `WeaponStats.MaxWeaponRange` and `PropulsionStats.ForwardAccel`. SPEC-20/21 read `ShipStatMultipliers`.
- **Cost:** one `BufferLookup` read per consumer per tick (`Requesting`), and up to four small change-filtered jobs instead of one.

## Alternatives considered
- **Keep central demand derivation and add kinds as they come.** Rejected: every feature would edit SPEC-22, which is the problem this ADR solves.
- **One `PowerRequested` enable bit per entity.** Rejected: a block with two consuming modules would have two writers for one bit.
- **Keep the monolithic `ShipStats` and let features add fields.** Rejected: one writer would have to understand every feature.
