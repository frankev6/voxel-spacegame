# ADR-0022 — Reference Entities' mathematics extensions for voxel bounds

**Status:** Accepted (Tech lead, 2026-10-11; autonomous implementation authority from Frank)

## Context
SPEC-11's VoxelRenderRuntime.PropMeshBounds uses Unity.Mathematics.AABB. The installed Entities package provides this type in Unity.Mathematics.Extensions, which Game.Voxel did not reference. Compilation of the specified contract fails without it.

## Decision
Add Unity.Mathematics.Extensions to Game.Voxel's assembly references. This assembly belongs to the existing Entities package, which architecture §3.1 already permits in Voxel. No package versions, module boundaries or specified fields change.

## Consequences
Voxel render bounds use the same AABB type as Entities Graphics. The reference is explicit and covered by compilation and the assembly rules checks.

## Alternatives considered
- Reference the package's existing type. Selected; preserves the SPEC-11 contract.
- Substitute a custom bounds type. Rejected; diverges from Entities Graphics and requires conversions.
