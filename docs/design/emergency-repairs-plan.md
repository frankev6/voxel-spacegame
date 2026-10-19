# Emergency repairs — plan (ABL-006)

Owner, 2026-10-18: "plan out what it would take to have a "emergency repairs" ability where you fix a percentage of your lost/weakened ship at the heavy cost of energy and random hulls from your inventory if you're a player. it should prioritize fixing missing engines first."

Status: planned, not started. Defaults below are proposals; the open questions at the end are the owner's.

## What already exists

About 40% of the machinery is in the code:

- `CellRestoreRequest` (Heal / Place) runs in `DamageResolveSystem`, and `ProducerKind.Repair` exists (DMG-019, ADR-0016).
- Abilities fire from the hotbar through `AbilitySystem` (SPEC-24), cost energy through the one-shot power grant, and AI profiles already fire abilities on a `HullBelow` rule.

Two pieces are missing:

1. **Rebuilding a lost block or an emptied brick.**
   - `PlaceCell` only places single-cell palette blocks into bricks that still exist.
   - Empty bricks are deleted (`CellWriter.RemoveCell`).
   - A destroyed block instance stays destroyed (SPEC-13 §5.3.1).
2. **A record of what the ship should look like.**
   - A palette cell that is gone leaves no trace.
   - The player's fleet copy takes the damage on write-back (`GridStructureSync`).
   - So the design has to be kept separately.

## How it plays (proposed defaults)

- **Activation.**
  - `ability_emergency_repair` on the normal ability bar.
  - Abilities come from blocks (SPEC-24), so it is granted by the core blocks: every ship that is not a wreck has it.
- **Amount.** Each use restores **35% of the missing design cells**, counted in cells. Cells are what mass, the core floor (DMG-020) and core capacity count. It also heals every surviving damaged cell and block by 50%, for free.
- **Timing.** Paid and planned on the press; the work lands over a **2 s channel**, engines first.
  - Damage does not interrupt the channel, but rebuilt cells can be shot off again.
  - Becoming a wreck ends it.
- **Cost.** **150 energy** of the core's 200; cooldown **90 s**.
  - Not enough energy: the press is refused and nothing is spent.
- **Hull items (players only).**
  - 1 hull item per 4 rebuilt cells, rounded up.
  - Each item is drawn at random from the `block_hull*` stacks in the hold, weighted by count. A multi-cell hull item counts its cells.
  - The items are raw material: rebuilt cells always match the design, whatever paid for them.
  - No hull items: the slot shows "No hull plating".
  - Too few: the repair shrinks to what they cover.
- **AI ships** pay energy only and use it below 50% hull.
- **Wrecks** cannot repair, and repair never revives one. A ship just above its core floor climbs back up.

## Repair order

A pure, deterministic planner, ordered:

1. **Missing engines** (`Thruster` blocks).
   - The direction with the least thrust left goes first.
   - Each engine is rebuilt whole. The first one is always allowed, even over budget.
2. **Other functional blocks**: power, then shields, then weapons, then the rest. The core is never planned.
3. **Hull**, breadth-first outward from live cells.

Every planned cell must touch a live or already-planned cell, so nothing is rebuilt floating. If an engine's strut is gone, the bridging hull joins the engine's wave.

Where the missing parts went:
- **Knocked off**: rebuilt in place. The broken-off piece stays behind as salvage.
- **Destroyed in place**: the same rebuild.
- **Weakened but present**: healed.

## What has to be built

| Piece | Work |
|---|---|
| Ability content | `AbilityKind.EmergencyRepair`; fields for repair and heal fraction, cells per hull item, whether items are needed. |
| Design snapshot | `ShipDesignRef`, a blob of the design's cells, made at spawn: from the player's fleet record (`FleetShipRecord.Design`, new), or from a per-blueprint cache for AI. |
| Save | Fleet save v2 stores the design. A v1 save migrates with "design = current structure", which accepts the old losses. |
| Damage | `CellRestoreMode.RebuildInstance`: refill a block's footprint, clear `Destroyed`, restore its HP. |
| New appliers on every machine | Create missing bricks a tick before cells land. Recreate functional entities, so turrets come back. |
| Planner and effect system | Authority only, in the ability group after `AbilitySystem`; feeds a wave of restores each tick. |
| Inventory bridge | Authority only, like `SalvageService`: credits hull cells to the ship and takes the items atomically; random choice seeded by tick and ship id; co-op guests draw from the host's shared hold. |
| HUD and feedback | Cost tooltip, blocked reason, channel ring, a "Repairs: 2 engines, 340 cells, used 6 Hull" notice, weld sparks and a channel loop. |
| Co-op | Activation already replicates. New reliable records for created bricks and revived blocks must arrive before the cell deltas, or guests report desyncs. |

### What adding cells back at runtime triggers

- **Mesh and collider.** They rebuild through the dirty-brick path.
- **Mass and ship stats.**
  - Mass rises through `GridMassAddJob`.
  - Ship stats re-hash, so thrust, power, weapons, shields and the ability bar update.
- **Cell bounds.** They never shrink, so rebuilt cells always fit.
- **Core floor (DMG-020).** It reads the live cell count, so repairs lift a ship back above its floor with no extra work.
- **Integrity.** Touched bricks are re-checked; this needs a test.

## Tests

**EditMode** (planner):
- engines first;
- the bridging strut is included;
- the budget fraction is respected;
- the first engine is allowed over budget;
- deterministic results.

**EditMode** (rebuild and effect):
- the rebuild revives a block, and drops a request into an occupied cell;
- the effect is blocked without hull items;
- AI pays energy only;
- no energy means nothing is spent;
- wrecks are unavailable.

**EditMode** (bridge): weighted random, seeded; the item take is atomic.

**Save**: the v2 round trip, and v1 migration.

**PlayMode**: shoot off every rear engine, repair, and forward thrust returns within 2 s. Mass rises, a ray hits a rebuilt cell, and co-op parity stays clean.

**Scenario** `emergency_repair`: reports engines and cells restored, energy and hull spent.

## Size and risks

About **9–12 agent-days**:

| Task | Days |
|---|---|
| Content | 0.5 |
| Design snapshot and cache | 1.5 |
| Fleet save v2 | 1 |
| Rebuild, brick and revive appliers | 2.5 |
| Planner and effect | 1.5 |
| Inventory bridge and HUD | 1.5 |
| Co-op records | 1.5 |
| Tests and scenario | 1 |

Risks:
- Creating bricks at runtime is new structural work: rendering, colliders, linked groups.
- Revived turrets.
- Stale integrity caches.
- Guest desyncs if records and deltas arrive out of order.
- Collider rebuild spikes on big ships; spread over the channel.

## Open questions (recommended default first)

1. Where does it come from? **Granted by core blocks**, or a new "repair forge" block you choose to fit. Granting it from the core may shift other abilities' keys.
2. Instant or channelled? **2 s channel, not interrupted by damage.**
3. Starting numbers? **35% of missing cells, 150 energy, 90 s cooldown, 4 cells per hull item.**
4. Can it rebuild a destroyed core? **No.** That is death.
5. Rebuild the hull that connects a cut-off engine automatically? **Yes, within the budget.**
6. Does healing damaged cells cost items? **No.**
7. AI ships get it? **Yes, energy only, slower cooldown on hard profiles.**
