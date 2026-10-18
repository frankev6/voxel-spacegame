# ADR-0121 — Large hull blocks: 2×2×2 and 3×3×3 cubes as one placeable block

**Status:** Accepted (owner, 2026-10-17; ledger open question 6, part b)

## Context
Open question 6(b) asked whether hulls should have large blocks: a 2×2×2 or 3×3×3 cube placed as one block. On the owner review page the options were:
- no large blocks;
- editor stamps that place ordinary cells;
- real large blocks that stay one block.

Owner, verbatim: "Yes, as real large blocks".

The review page said this needed a brick-format change and saved memory. Both claims were wrong:
- The format already supports it. SPEC-04 decision 13 gives every multi-cell block a `BlockInstance`, and every footprint cell points at it. Reactors, engines and the two-cell long slope (ADR-0018) work this way today. Only validation (`DATA-E036`) keeps structural blocks to the single-cell kit and the long slope, and the decision itself calls other multi-cell structural kits an owner gate.
- It does not save memory. A large block is one `BlockInstance` record plus cells that reference it, so it costs a little more than the same cells taken from the palette.

## Decision
1. **Two new structural blocks:** `block_hull_large_2` (2×2×2, 8 cells) and `block_hull_large_3` (3×3×3, 27 cells).
   - Both are cubes of the hull material, with the hull's per-cell HP and mass.
   - The root cell is the corner `(0,0,0)`, and the footprint fills +X+Y+Z.
   - They are achiral and need no orientation variants. All 24 orientations are allowed, so a builder can grow a large block from any corner.
2. **They are ordinary multi-cell instances.**
   - The editor places, selects, moves, deletes and mirrors each one as one block, and the bill of materials counts it as one item.
   - Damage removes cells one at a time, like any other cells, and `CellsAlive` falls. The instance is gone when its last cell goes.
   - Repair restores the missing cells of the instance.
   - Salvage returns the block item only while it is intact. A damaged large block salvages as the hull cells it still has.
3. **They look like one plate.**
   - Each cell is still meshed on its own (SPEC-04 decision 13), but the hull material's panel layout follows the instance, not the cell.
   - The mesher passes each cell's offset inside its footprint, so seams and greebles span the whole block instead of repeating every 0.5 m.
   - This is a mesher input. The packed brick format does not change.
4. **Validation:** `DATA-E036` also accepts a structural block whose footprint is a full cube box of side 2 or 3, with every cell `Cube`. No other large shapes are added. Large slopes and wedges need a new ADR.

## Consequences
- **SPEC-04:** decision 13 and the `DATA-E036` row name the two cube boxes. Task DATA-013 adds the blocks, the validation and the tests.
- **SPEC-11:** the brick mesher reads the footprint offset of instance cells for the panel UVs. Single-cell blocks pass offset 0, so they look the same as today.
- **The ship editor** needs no new tool: large blocks are on the palette like the engines.
- **Memory:** a hull built mostly from large blocks has one instance per 8 or 27 cells. That is acceptable for ships; huge stations should keep using plain hull cells.

## Implementation notes (DATA-013, 2026-10-17)
- **Collapse rule:** the multi-cell collapse rule (`InstanceDestroyCellFraction`, half the footprint) would have removed a large block after half its cells were gone. Structural cube boxes are exempt, which decision 2 needs.
- **Panel layout:** carried with no new storage:
  - the render snapshot reuses a large cell's orient bits for its offset, since a cube looks the same in every orientation;
  - the packed vertex uses the top byte of `Lo`, which only smooth Xeno vertices used before.

## Alternatives rejected
- **Editor stamps:** the owner chose real blocks.
- **A packed large-cell format** (one record for 8 or 27 cells): damage would have to split the record back into cells, and every system that reads cells would need a new case. That is a large change for a builder convenience.

## Amendment (2026-10-18, DEV-023): a 4×4×4 block
Owner, for the Hollow (hub option E): "so that the game doesn't lag the asteroid should be built of larger voxels", then "no, i meant 2x2s and 3x3s or even 4x4s". `block_hull_large_4` (4×4×4, 64 cells) joins the two blocks above on the same terms, and `DATA-E036` accepts a full cube of side 2, 3 or 4. The Hollow's rock is built almost entirely of 2×2×2 and 4×4×4 blocks at 2 m cells. As the Context says, large blocks do not save memory per cell. The rock stays cheap because only its shell is stored.
