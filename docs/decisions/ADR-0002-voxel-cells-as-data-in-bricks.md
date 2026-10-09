# ADR-0002 — Voxels are 4-byte cells in 16³ bricks

**Status:** Accepted

## Context
Massive destructible structures (up to ~1M cells) are the core vision. One entity per voxel costs ~0.14 µs per idle voxel per frame and ~300 B each.

## Decision
A cell is 4 bytes (palette-or-instance ref, quantised HP, flags) stored in sparse 16³ bricks. Bricks are the unit of meshing, collision, dirtiness and parallelism. Plain hull never becomes an entity; functional blocks do. Default cell size 0.5 m, stored per grid. The editor, runtime and saves share one layout (`VoxelStructure`).

## Consequences
Per-frame cost scales with bricks and with changes, not with cell count. Multi-cell blocks need an instance table. Meshing, colliders and integrity must be rebuilt incrementally per brick.

## Alternatives considered
Entity per voxel (rejected: does not scale); 32³ bricks everywhere (rejected for ships: 8× cost per dirty brick; allowed later for static stations).
