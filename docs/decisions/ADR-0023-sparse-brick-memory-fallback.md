# ADR-0023 — Sparse bricks for the measured voxel memory fallback

**Status:** Proposed (VOX-002 no-go, 2026-10-11)

## Context
VOX-002 measured the reference station with 1,008,128 cells, 872 bricks and 504,192 exposed faces. The dense model costs **38,828,288 B** at peak with CPU mesh copies retained: 14,733,312 B cells/masks, 278,528 B brick/meta chunks, 21,228,544 B mesh GPU+CPU payload, and 2,587,904 B cached/installed/pending/retired colliders. This exceeds the 36 MB headroom gate even if MB is read as MiB. Mesh-copy release cannot satisfy this gate's explicit retained-copy assumption.

## Proposed decision
Activate SPEC-10 VOX-003's sparse fallback. Add a capacity-zero `BrickSparseCells` buffer with `{ ushort Local; Cell Value; }` (6 bytes). Bricks at or below 1,024 solid cells may use a sorted sparse buffer; denser bricks use 4,096 dense cells. Preserve the 512-byte solid mask and cell semantics. Define an unambiguous representation invariant and common read/write helpers before changing callers; densify when a write crosses the threshold. Avoid per-cell structural changes by predeclaring the needed buffers on brick archetypes.

VOX-003 must update SPEC-10 §4/§5 and every affected reader/writer, including structure storage, spawn/write-back, meshing snapshots, collider/mass reads and persistence adapters. Persisted VOXS bytes and deterministic cell order remain unchanged. Its required equivalence/densification tests and measured memory gate must pass before closure. This proposal alone does not authorize code to diverge from the current binding dense-cell contract.

## Consequences
728 reference bricks, containing 745,472 cells, qualify. Replacing their dense payloads with 6-byte sparse entries projects **31,373,568 B** total before extra buffer headers, sparse capacity slack and changed chunk packing. This is an estimate; VOX-003 must measure the actual implementation.

Thin-shell geometry remains a stress report, not a 40 MB gate. All 3,784 thin-shell bricks qualify, but sparse storage alone still projects 106,870,512 B with LOD0 meshes and both copies. The documented additional LOD policy would need separate implementation/validation and any owner-gate decision; this ADR does not change that scope.

## Alternatives considered
- **Sparse fallback:** recommended. Follows the specified no-go path and targets measured empty-cell waste.
- **Require CPU mesh-copy release:** predicts 28,214,016 B dense, but does not satisfy VOX-002's retained-copy criterion; REN-002 has not yet verified release/rewrite support.
- **Relax the headroom gate:** retain dense storage under the eventual 40 MB limit. Rejected as an implementation shortcut; it changes the specified gate and leaves little room for overhead.
