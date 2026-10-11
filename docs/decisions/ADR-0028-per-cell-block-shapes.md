# ADR-0028 — Per-cell shapes for the approved structural kit

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous implementation instruction.

## Context

ADR-0018 approves one two-cell long-slope block, but the current definition/blob only stores one shape and DATA-E036 rejects every multi-cell structural footprint. SPEC-04 Q14 already proposes optional shapes parallel to authored cells. This is missing implementation scope, not a new visual design.

## Decision

- Add optional serialized `BlockShape[] shapes` and public `Shapes` / `GetShape(int footprintIndex)` to BlockDefinition. Null/empty inherits `shape`; explicit arrays must match Cells, use approved enum values and be structural only.
- Add `BlobArray<BlockShape> Shapes` and `GetShape(int footprintIndex)` to BlockDef. The builder materializes resolved shapes in canonical Cells order and sets scalar Shape to the resolved root shape. GetShape falls back to Shape for empty arrays (existing synthetic callers); production arrays always match Cells. Air has empty arrays. Include resolved shape bytes immediately after canonical Cells in BlockDefsHash. This is an in-memory schema/database fingerprint change, not a saved VOXS format change.
- Keep DATA-E036 for structural footprints outside the approved kit. Single cells accept Cube/Wedge/HipCorner only; the sole multi-cell form is exactly root High plus +Y Low, with scalar shape High. Long-slope halves cannot be authored as independent placeable blocks. DATA-E050 rejects invalid enum values; E051 rejects nonempty mismatched lengths; E052 rejects any non-structural shapes override. Existing E037 still validates non-structural scalar Shape.
- Add DATA-012 in M1 to implement this and author `block_hull_hip` / `block_hull_slope_long`. Initial gameplay values inherit angled hull per cell: HP 50, mass 15 kg, material/armor 0, all orientations/attachments, no modules/prop. Price 4 for hip, 8 for the two-cell slope; Common, stack 999, reuse angled-hull icon. These provisional tuning values await normal gameplay tuning, not art approval.
- Snapshot gathering resolves an instance's cell offset using inverse orientation applied to `gridCell - instance.Origin`, finds it in canonical Cells, and uses GetShape. Palette cells use root Shape. An unmatched offset follows the existing missing-content fallback. Damage may remove either cell without changing the surviving cell's shape.

## Consequences

Every baked block gains one eight-byte BlobArray header plus one shape byte per footprint cell (allocator alignment applies). Existing content hashes change deterministically; no serialized cell bits or instance layout change. Larger hull blocks and additional shapes remain owner decisions. Physics retains its documented cube approximation for slopes. REN-005 consumes these data; no renderer implementation or new visual asset is introduced here.
