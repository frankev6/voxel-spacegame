# ADR-0043 — EditMode render-runtime assembly reference

- **Status:** Accepted
- **Date:** 2026-10-12
- **Decider:** Tech lead

## Context

REN-010 tests instantiate and dispose VoxelRenderRuntime. Its NativeArray<AABB> field requires the installed Unity.Mathematics.Extensions assembly as a direct compiler reference, as REN-002 previously required for PlayMode (ADR-0031).

## Decision

Add Unity.Mathematics.Extensions to Game.Tests.EditMode. Architecture §3 already permits every assembly for this test-only module. No package version or runtime dependency changes. Tests exercise the actual public runtime contract rather than a substitute type.

## Consequences

EditMode mesh pipeline tests can compile; the production module boundaries remain unchanged.
