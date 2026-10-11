# ADR-0021 — Reference the Input System test fixture

**Status:** Accepted (Tech lead, 2026-10-11; autonomous implementation authority from Frank)

## Context
SPEC-03 INP-003 explicitly requires simulated rebinding through InputTestFixture. The installed Input System package already provides Unity.InputSystem.TestFramework, but Game.Tests.EditMode did not reference it.

## Decision
Add Unity.InputSystem.TestFramework to Game.Tests.EditMode's references. Architecture §3 permits test assemblies to reference all required assemblies. No runtime references or package versions change.

## Consequences
Rebinding tests use an isolated Input System runtime and a controlled clock, restoring real devices after teardown. The fixture assembly stays test-only.

## Alternatives considered
- Reference the supplied fixture: follows INP-003 and isolates device state. Selected.
- Hand-roll global Input System reset and timing: duplicates the package test harness and violates the specified test approach.
