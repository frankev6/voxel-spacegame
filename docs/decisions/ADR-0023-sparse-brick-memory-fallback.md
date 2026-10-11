# ADR-0023 — Sparse bricks for the measured voxel memory fallback

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous implementation instruction; VOX-013/014 foundations are now complete.

## Context
VOX-002 measured the reference station with 1,008,128 cells, 872 bricks and 504,192 exposed faces. The dense model costs **38,828,288 B** at peak with CPU mesh copies retained: 14,733,312 B cells/masks, 278,528 B brick/meta chunks, 21,228,544 B mesh GPU+CPU payload, and 2,587,904 B cached/installed/pending/retired colliders. This exceeds the 36 MB headroom gate even if MB is read as MiB. Mesh-copy release cannot satisfy this gate's explicit retained-copy assumption.

## Decision
Activate SPEC-10 VOX-003's sparse fallback. Add a capacity-zero `BrickSparseCells` buffer with `{ ushort Local; Cell Value; }` (6 bytes). Bricks at or below 1,024 solid cells may use a sorted sparse buffer; denser bricks use 4,096 dense cells. Preserve the 512-byte solid mask and cell semantics. Define an unambiguous representation invariant and common read/write helpers before changing callers; densify when a write crosses the threshold. Avoid per-cell structural changes by predeclaring the needed buffers on brick archetypes.

VOX-003 must update SPEC-10 §4/§5 and every affected reader/writer, including structure storage, spawn/write-back, meshing snapshots, collider/mass reads and persistence adapters. Persisted VOXS bytes and deterministic cell order remain unchanged. Its required equivalence/densification tests and measured memory gate must pass before closure. The implementation updates the binding storage contract and every existing affected caller in this task; no dense-only runtime path may remain for a sparse-capable grid.

### Representation and migration details

- `BrickSparseCells` is a zero-inline-capacity buffer element with `[StructLayout(LayoutKind.Sequential, Pack = 2)]`: ushort Local followed by the unchanged four-byte Cell, exactly six bytes. Entries are sorted by Local, unique and solid. Both buffers exist on every brick archetype; dense means BrickCells.Length == 4096 and sparse length zero; sparse means dense length zero and 0..1024 sparse entries. Empty storage is only a transient state until the owner destroys an empty brick. The 512-byte mask is unchanged.
- Sparse writes insert/remove in sorted order. Inserting the 1025th solid cell densifies in place, copies the entries, and releases the sparse heap allocation. Dense damage writes remain dense to avoid allocation churn. Spawn and explicit structure optimization choose sparse storage for at most 1024 solid cells.
- `BrickRecord.Page` stays four bytes: -1 is uniform, nonnegative selects a dense 4096-cell page, and values <= -2 encode sparse page index as -2-Page. `SolidCount` is the sparse entry count. `VoxelStructureData` adds flat SparsePages and FreeSparsePages arrays. Each source sparse page reserves 1024 entries for predictable mutation and simple ownership; unused capacity is reported, never counted as six bytes per occupied source cell. ECS buffers size to their actual entries. No nested native containers are introduced.
- A common read helper handles dense/uniform/sparse source records. `BrickWrite.Get/Set` gain the sparse-buffer argument; GridCellReader adds its read-only sparse lookup. Every current caller and the corresponding SPEC-10/13 API examples migrate together. Full dense snapshots required by VOXS and render jobs are expanded at their adapter boundary; serialized bytes remain unchanged.
- Clone, compaction, diff, mass, ray/placement reads, codec conversion, spawning, write-back and delta capture all preserve cell/HP/flag/instance semantics. Sparse mass iteration preserves ascending local-cell order. Only the owning structure releases source pages; masks and revision rules stay intact.
- VOX-003's memory measurement uses actual buffer capacities and chunk counts, includes ADR-0026's root baseline, and retains both mesh CPU/GPU payloads plus the established collider peak. Source storage is reported separately and is excluded from the same post-spawn model as VOX-002; blueprint sources are evicted after spawn. Whole-renderer integration remains TST-004.

## Consequences
728 reference bricks, containing 745,472 cells, qualify. Replacing their dense payloads with 6-byte sparse entries projects **31,373,568 B** total before extra buffer headers, sparse capacity slack and changed chunk packing. This was the preimplementation estimate. VOX-003 measured **31,539,808 B** retained-copy peak, including actual buffer capacities, all world/meta chunks and 35,168 B root heap (10,464 B removal baseline): **go** below 36 MB. SPEC-10 §8.3 records the exact boundary and component totals.

Thin-shell geometry remains a stress report, not a 40 MB gate. All 3,784 thin-shell bricks qualify, but sparse storage alone still projects 106,870,512 B with LOD0 meshes and both copies. The documented additional LOD policy would need separate implementation/validation and any owner-gate decision; this ADR does not change that scope. The measured sparse LOD0 result is **107,292,880 B**, retaining both copies.

## Alternatives considered
- **Sparse fallback:** recommended. Follows the specified no-go path and targets measured empty-cell waste.
- **Require CPU mesh-copy release:** predicts 28,214,016 B dense, but does not satisfy VOX-002's retained-copy criterion; REN-002 has not yet verified release/rewrite support.
- **Relax the headroom gate:** retain dense storage under the eventual 40 MB limit. Rejected as an implementation shortcut; it changes the specified gate and leaves little room for overhead.
