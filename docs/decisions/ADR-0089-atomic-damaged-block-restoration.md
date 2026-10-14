# ADR-0089 — Atomic damaged-block restoration

**Status:** Accepted (Tech lead, 2026-10-13).

## Context

ADR-0082 permits damaged blocks with disjoint surviving cells to share origins. DraftWriter currently restores by placing a pristine footprint then clearing missing cells. Undo therefore rejects a valid restoration when another block occupies a missing cell, or would erase that neighbour if the overlap check were relaxed.

## Decision

Append source-only `VoxelEditKind.RestoreBlock` and `VoxelEdit.DamageOffset`. Add an `ApplyEdits(edits, damage, in content, touchedBricks)` overload; retain the existing overload unchanged, delegating with an empty damage view. Damage is a borrowed native byte array: canonical footprint HP bytes followed by little-endian FunctionHp bits, matching SPEC-51. Restore validates the complete damage record and all surviving cells before allocating staged storage. Missing cells neither require air nor receive writes. Zero surviving cells, malformed records and invalid functional HP reject the entire batch. Ordinary PlaceBlock semantics remain unchanged. DraftWriter emits one RestoreBlock operation for a damaged addition, in forward or reverse order, and calls the overload once.

The Game.Voxel transaction remains the only writer. No public runtime component, save format, assembly reference or package changes. Editor placement still protects missing-cell reservations; restoration does not authorize ordinary tools to fill them.

## Consequences

Undo preserves other blocks in missing footprint cells and restores exact HP/root flags/functional state. Validate this through the existing required ReservedMissingCell case and adapter round-trip tests; no extra test method or performance gate. Voxel and editor spec references and ledger record the ownership extension.
