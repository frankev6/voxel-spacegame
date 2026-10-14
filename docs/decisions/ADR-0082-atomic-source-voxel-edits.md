# ADR-0082 — Atomic source voxel edit batches

**Status:** Accepted (Tech lead, 2026-10-13).

## Context

SPEC-51 EDT-003 assumes VoxelEdit and VoxelStructure.ApplyEdits. SPEC-10 currently supplies individual source mutators only. ShipEditor must not write cells/instances directly, nor clone a whole ship for each command.

## Decision

Game.Voxel.Model owns VoxelEditKind/VoxelEdit and VoxelStructure.ApplyEdits(NativeArray<VoxelEdit>, in VoxelContent, NativeList<int3>). Use canonical VoxelContent rather than a single definitions blob because placement also requires orientation tables. Validate the entire ordered batch against an overlay containing only touched cells/instances, newly needed palettes and simulated free-slot operations. Ordinary invalid/capacity failures return false without changing source or touched output. Commit only after validation, in stable z/y/x cell order, appending unique sorted touched bricks. Borrowed/pinned and read-only rules remain those of VoxelStructure. Allocation failure/internal corruption remains an exception, not a recoverable validation result.

RemoveBlock/SetFunctionHp carry original Block/Orient along with origin, permitting footprint-bounded lookup when the root cell is missing. SetCellHp requires a surviving cell and nonzero HP; ClearCell removes a cell and adjusts instance CellsAlive. A batch may not leave a non-tombstoned instance with zero surviving cells: remove the whole block explicitly. No whole-ship rollback clone or second writer is introduced. New API is source-only; runtime ECS damage remains its existing owner.

VoxelStructureReadView exposes borrowed read-only native arrays plus canonical dense/uniform/sparse reads for Burst source jobs. DraftReader pins the structure while its scheduled enumeration/hash jobs run and completes them before unpinning. Enumeration emits palette cells and each live instance once, then sorts by origin; it must not require a surviving origin cell. Zero-cell/tombstoned instances are not placed blocks. DraftWriter accepts native diff/damage views directly; EDT-004/005 own the command containers.

SetFunctionHp accepts finite damage-produced negative values (including surviving non-functional blocks), rejects values above the definition maximum, and derives Destroyed using the same functional-kind/HP rule as VOXS loading. Canonical instance hashing sorts by origin, block, orientation, color, then HP bits and includes each identity alongside its HP. Damaged instances can share origins; sorting HP alone would erase the association.

## Consequences

EDT-003 can translate forward/inverse commands into a single atomic store call. Existing single-operation APIs are unchanged. The required adapter round-trip case will also assert rejected batches leave the source untouched; no additional test methods are added. Native reads/jobs and final adapter acceptance remain unfinished at this checkpoint.
