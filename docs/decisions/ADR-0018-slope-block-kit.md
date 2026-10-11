# ADR-0018 — Slope block kit: slope, hip corner, long slope 1×2

**Status:** Accepted (owner, 2026-10-11; ledger open question 6, part a)

## Context
The specs allowed two structural shapes, `Cube` and `Wedge` (SPEC-04 §4.2), packed in one bit (SPEC-11 §4.1). The look-dev hulls used free cut cells: thousands of shapes, too many for builders and for the mesher.

The slope study (`ArtSource/LookDev/ld_slopes.py`, look book tab *Slopes*) compared small builder kits. It built the same ships with each kit, using only that kit's blocks, and measured how cleanly each kit closes the shapes builders make.

- At 45° the corner where two slopes meet is the same shape in every orientation, so a slope plus one hip corner closes every edge.
- Each extra angle brings its own family of corners.

Owner, verbatim:
- "dude 20 slopes is insane my boy I though you were going to propose like 3 or 4. also remember it doesn't have to be all 1x1s it can be 1x2s or 1x3s or 2x3"
- "i wan something sensible so builders can build with"
- On seeing the WRAITH rebuilt with each kit: "option 2 is better"

## Decision
1. **The structural kit** is four placeable shapes: cube, slope 1×1 (the existing `Wedge`), hip corner 1×1 and long slope 1×2. Each rotates to any of the 24 orientations; none needs a mirrored variant.
2. **The long slope is one placeable block covering two cells.** It rises 1 cell over 2. Each cell stores its own piece shape, `LongSlopeHigh` and `LongSlopeLow`, so damage removes the two cells independently, like any other cells.
3. **`BlockShape` gains three values at the end:**

   ```csharp
   public enum BlockShape : byte { Cube = 0, Wedge = 1, HipCorner = 2, LongSlopeHigh = 3, LongSlopeLow = 4 }
   ```

   The geometry is in SPEC-04 §4.2.
4. **No other shapes.** Long-slope ridge corners, long-slope noses, and corners where a long slope meets a 45° slope are not in the kit. Builders end long slopes against flat faces, cubes or 45° pieces. More shapes need a new ADR.

## Consequences
**Spec changes:**

| Spec | Change |
|---|---|
| SPEC-04 §4.2 | The enum and the shape geometry table (applied with this ADR) |
| SPEC-04 `BlockDefinition` | Per-cell shapes for multi-cell structural blocks (open question added there) |
| SPEC-04 content table | `block_hull_hip` and `block_hull_slope_long` |
| SPEC-11 §4.1 | The snapshot shape field grows from 1 bit to 3 (open question added there, with a default layout) |
| SPEC-11 `VoxelNormals` | Grows from 18 to 42 normals: the 24 permutations and signs of (0, 1, 2)/√5 |
| SPEC-11 `BrickShapeTables` | Holds 5 shapes × 24 orientations |
| SPEC-11 REN-004 | Its criteria cover all five shapes |
| SPEC-12 | May treat every sloped shape as a cube for collision, as it already may for `Wedge` |

**Still open in ledger question 6:**
- (b) large hull blocks (2×2×2, 3×3×3) as single placeable blocks;
- (c) smooth signed-distance cells for alien hulls.

**Effect on the look book:** the look-dev ships keep their free cut cells, because they are concepts (DESIGN-GUIDE §10). Game hulls use this kit.

## Alternatives considered
- **Slope + hip corner only (45°).** Two blocks, and every edge closes. Rejected by the owner in favour of the sleeker long-slope noses.
- **Adding a long hip corner 2×2.** It closes ridges where two long slopes meet, but not noses, and every corner type adds a block. Not taken.
- **Free cut cells, or a large per-cell catalogue (11–253 shapes).** These are what the look-dev hulls use, but too many for builders. Rejected.
- **The classic builder set (slope, three-face corner, inverted corner).** It left diagonal edges stepped (8.4 % of cells off, against 1.5 % for slope + hip corner). Rejected.
