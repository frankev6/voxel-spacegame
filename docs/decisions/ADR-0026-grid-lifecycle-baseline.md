# ADR-0026 — Grid lifecycle baseline and partial write-back

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous implementation instruction.

## Context

CaptureChanges has no registry argument, and an empty BrickSlot alone cannot distinguish an originally empty slot from a destroyed brick. A partially spawned grid also has empty slots for bricks it has not created yet. Treating those as deletions loses saved cells. Rebuild may be requested while the old spawn is still reading its source.

## Decision

- Spawn and rebuild store an internal zero-inline-capacity GridSpawnBrick buffer of original occupied coordinates in source-record order (12 bytes per brick). CaptureChanges compares this baseline with live slots, so originally empty space is never a removal. The baseline resets at rebuild; ordinary write-back never erases it. This adds 10,464 payload bytes for the 872-brick reference station, plus its buffer header/capacity overhead to be measured in VOX-003.
- CaptureChanges replaces its output containers, copies root palette and instance metadata (with Functional references cleared), copies existing bricks changed since initialization (ChangedRevision > 0), and returns sorted removed original coordinates. It is independent of the last save's SyncedRevision. Output CellSize follows the root.
- Ordinary WriteBack skips an unfinished spawn without advancing SyncedRevision: its complete original geometry already exists in the source and its read lease forbids mutation. Terminal despawn completes jobs, ends both pending rebuild and current spawn leases, then writes changed existing bricks and removes only baseline coordinates already traversed by NextBrick. Uncreated source bricks survive. Another active reader still forbids mutation.
- RequestRebuild pins its new source immediately using a separate internal RebuildReadToken. Replacing that request releases its previous pending read. Conversion adopts the pending read, releases the old spawn read, keeps the root, serial, pose, identity and unrelated components, increments VoxelGrid.Revision, and resets SyncedRevision to that new baseline. Invalid rebuilds log once and retain the existing grid. Teardown cancels both read kinds.
- Write-back copies instance metadata, clears ECS entity references, rebuilds deterministic free-instance indices and invalidates cached structure mass when metadata changes. It optimizes only copied bricks, never scans unchanged cells. Existing public types and API signatures remain unchanged.

## Consequences

Sparse memory measurements must include the baseline buffer. Runtime code that creates persistent roots without GridSpawnSystem must establish the same baseline before using CaptureChanges. The six lifecycle tests cover changed-only copies, unchanged repeat saves, destroyed bricks, partial despawn preservation, rebuild identity, full destruction and deltas that survive a prior write-back.
