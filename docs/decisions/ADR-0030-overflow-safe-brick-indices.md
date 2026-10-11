# ADR-0030 — Overflow-safe indices for legal slope bricks

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous implementation instruction.

## Evidence

Tile a 16³ brick with 2,048 approved long-slope blocks: High at even Y, Low at odd Y, orientation zero. Paint each block `(x + 17*(y/2) + 67*z) mod 256`; adjacent blocks have different colours. There are 22,528 exposed polygons: no neighbour has a full opposing face. With AO fixed at 3 and maximum axis welding, axis polygons require 57,344 vertices and the specified unwelded slopes require 16,384. The **73,728-vertex lower bound** exceeds UInt16's 65,536 addressable vertices. Actual AO or the single-entry weld cache can only increase it. Reproducible enumeration: `Logs/tools/ren006-index-proof.py` (task evidence, ignored). The implemented mesher subsequently produced 77,568 vertices / 40,960 triangles with maximum index 77,567 in the required checkerboard report test; all indices are in range.

## Decision

- Keep one mesh per brick and eight-byte vertices. Both public mesher methods output `NativeList<uint>` indices, never truncating a vertex index.
- REN-010 chooses GPU `IndexFormat.UInt16` for at most 65,536 vertices, converts indices on copy, and otherwise chooses `UInt32` and copies the uint indices directly. REN-011 statistics derive index bytes from the same vertex-count threshold. Mesh rewrites may change index format without re-registering.
- Reference station, ordinary cubes and the isolated cube checkerboard keep two-byte GPU indices and the existing measured memory budgets. Dense painted slope bricks use four-byte GPU indices; report their actual memory. No budget is relaxed. Mesher scratch indices are four-byte values regardless of upload format.
- REN-006's existing checkerboard report also verifies the legal long-slope tiling has indices above 65,535 without wrapping. All eight specified tests remain; no new shape or visual asset is added.

## Alternatives

Truncation, dropped faces or failing to render legal player blocks are incorrect. Smaller bricks change storage/physics/save contracts; multiple meshes per brick change entity ownership and renderer costs. Conditional index width changes only the mesh upload representation and retains the established architecture.
