# ADR-0029 — Snapshot orientation input and diagnostic retention

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous implementation instruction.

## Context

REN-005 must resolve the approved long slope's per-cell shape by inverse orientation, but FromStructure has no orientation-table input. The shape-table input is unused since ADR-0018 moved full-face coverage to the mesher. SPEC-11 also requires snapshot warning flags for later apply, but BrickMeshWork has nowhere to retain them.

## Decision

- Replace FromStructure's unused `ref BrickShapeTables` parameter with `ref OrientationTablesBlob`. The ECS job takes the same immutable orientation blob. There are no implemented callers yet. Use the canonical inverse table, not a second rotation implementation.
- Add `byte SnapshotFlags` to BrickMeshWork: bit 0 means invalid cell reference/content/footprint/shape/orientation, bit 1 means more than 512 interior prop roots. Snapshot resets it for each work item; the later mesher preserves it and the apply system issues the already-specified WarnOnce messages. No job logs.
- Invalid references include a reserved block-zero palette/instance entry, block index outside the blob, orientation outside 0..23, invalid shape, palette entry requiring an instance, or instance offset absent from its footprint. Encode Cube, orientation 0, layer 63, colour 0, preserving cell damage/scorch. Air remains zero. Invalid cells do not emit prop roots.
- Parallel snapshot/prop arrays use disjoint fixed strides (5,832 / 512 per work index); only their parallel-for index-range restriction is disabled. ECS lookups stay read-only, and Work is written only at the job's own index. Source reads support uniform, dense and sparse records.

## Consequences

REN-005 explicitly depends on SPEC-04, which includes DATA-012 at M1 under the cross-spec dependency grammar. Four required tests cover snapshots, approved slope rotations, sparse/dense/uniform parity, missing borders, bad refs and prop bounds. This changes a pre-implementation public helper signature and work record under an accepted ADR; batch ownership, saved data, scheduling and mesh formats do not change.
