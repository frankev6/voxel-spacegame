# ADR-0003 — Ships fly on a plane

**Status:** Accepted (default; owner may revisit)

## Context
The prototype's flight was planar (XZ plane, yaw only), which suits a top-down-ish camera and readable combat. Full 6-DOF changes AI, camera, controls and level design.

## Decision
Ships move on the local XZ plane (Y = 0) and rotate around Y only. Voxel structures are fully 3D. The constraint is isolated in `PlanarConstraint` + `PlanarConstraintSystem` (SPEC-20) and the inertia lock (SPEC-12), so removing it later is a contained change.

## Consequences
Simpler controls, AI and camera. Debris and chunks may leave the plane (they are not constrained). Some vertical gameplay ideas are out of scope for v1.

## Alternatives considered
Free 6-DOF (rejected for v1: cost and readability).
