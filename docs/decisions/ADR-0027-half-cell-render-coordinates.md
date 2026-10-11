# ADR-0027 — Half-cell coordinates for the approved slope kit

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous implementation instruction.

## Context

ADR-0018 approved five cell shapes, including LongSlopeHigh and LongSlopeLow. Their SPEC-04 planes intersect cell edges halfway: a whole-cell integer corner cannot represent them. SPEC-11 retained the old 0/1 ShapeFace coordinates and 0..16 packed positions, despite requiring the five-shape tables. Rounding would change approved geometry; float vertices would increase the memory budget.

## Decision

- ShapeFace keeps its exact fields and 52-byte layout. Its int3 vertices now use **half-cell units**: 0, 1, 2 span one cell. Rotate exactly about (1,1,1): `R * (v - 1) + 1`. The canonical SPEC-04 solid regions and all 24 rotations are unchanged.
- PackedBrickVertex keeps eight bytes, its signature and bit layout. Pack's x/y/z arguments are half-cell positions 0..32 inside a brick. The shader decodes `round(posRaw.xyz * 255) * 0.5` into cell units. LocalTransform.Scale remains CellSize; mesh bounds, face UVs and public mesher bounds remain in cell units.
- LOD0 adds `cell * 2` to ShapeFace vertices and uses a 33×33 half-cell welding grid. At a fractional face vertex, AO samples its enclosing lattice corners and uses the minimum integer AO level; whole-cell corners retain the original formula. LOD1 emits its whole-cell corners multiplied by two for packing. All other attributes and the dense 18³ snapshot are unchanged.
- Correct the old wedge-only vertex bound: a checkerboard of isolated cubes uses 2,048×24 = **49,152** vertices, not 36,864. All five approved shapes have at most six four-vertex faces, so the same 49,152 isolated-cell bound applies; complex mixtures must still enforce the UInt16 index limit in the mesher rather than silently wrap. This task does not waive REN-006's exhaustive worst-case check.
- REN-004 implements all **5×24** shape/orientation entries, as already required by ADR-0018. Canonical half-cell inequalities: Wedge `y+z≤2`; HipCorner `y+z≤2` and `x+z≤2`; LongSlopeHigh `y+2z≤4`; LongSlopeLow `y+2z≤2`, bounded by [0,2]³. Full-side coverage remains SPEC-04's table.

## Consequences

Memory per vertex and the measured SPEC-10 station memory model are unchanged. The future mesher, renderer spike and code-owned shader decode use the same units. Any in-progress art-branch packed-vertex code must migrate when merged; no ship, asset, palette or art-direction change is authorized by this ADR. No saved format changes: VOXS stores cells, not render vertices.

## Alternatives

- Round half-cell vertices: rejected because it changes the owner's approved shapes and introduces seams.
- Float16 position format: representable, but increases each vertex from 8 to 12 bytes without a need to do so.
- Half-cell integer positions: selected; exact representation and rotation, unchanged vertex bandwidth and public field layouts.
