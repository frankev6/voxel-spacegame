# ADR-0096 — Exact editor inventory rollback

**Status:** Accepted (EDT-011, 2026-10-14; Tech lead).

## Context

SPEC-51 §5.9 suggests inverted inventory deltas after a target rejects a ship. Inversion restores counts only when capacity permits, rearranges slots, and cannot restore consumed overflow when normal cargo is full. A rejected editor commit must preserve inventory exactly.

## Decision

InventoryModel adds an opaque, owner-bound managed `Checkpoint`, `CaptureCheckpoint()` and `RestoreCheckpoint(Checkpoint)`. The inventory owner copies its bounded 200 slots and capacity; restore validates ownership and uses its normal state/count/event update path. The editor captures immediately before TryApplyAtomic, invokes its synchronous target, and restores the checkpoint on rejection. No cell or inventory state is written by another module. No assembly changes.

Preallocate both the transferred structure and new editor baseline before mutating inventory or calling the target. Rejected targets never own the clone; successful targets do. Reset original count/stat baselines and history on success. Guard reentrant editor mutations during commit. Exceptions from a contract-breaking target produce rejection with rollback; observer exceptions after accepted commit do not masquerade as a failed commit.

## Consequences

Failed editor commits preserve slot identities, missing-item IDs, overflow, capacity and counts. Managed checkpoint allocation is bounded and occurs only on explicit economy commits. It is a synchronous rollback token, not a save format or a general undo store. The required target-rejection case covers this defect without adding extra test methods.
