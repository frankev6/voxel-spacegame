# ADR-0031 — Reference Entities bounds in PlayMode rendering tests

**Status:** Accepted (Tech lead, 2026-10-11; autonomous implementation authority from Frank)

## Context
REN-002 must explicitly update RenderBounds when rewriting registered meshes. Its AABB field belongs to the installed Entities package's Unity.Mathematics.Extensions assembly (ADR-0022). Game.Tests.PlayMode needs an explicit reference even when retrieving RenderBounds through generic EntityManager methods.

## Decision
Add Unity.Mathematics.Extensions to Game.Tests.PlayMode. Architecture §3 already allows tests to reference every module and package. This uses the existing package without changing versions or runtime contracts.

## Consequences
The spike and later renderer integration tests can inspect and update exact bounds without reflection or proxy production APIs.

## Alternatives considered
- Direct existing-package reference: selected, compile-time checked.
- Reflection or a production helper solely for tests: unnecessary indirection.
