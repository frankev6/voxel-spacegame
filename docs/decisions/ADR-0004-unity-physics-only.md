# ADR-0004 — Unity Physics is the only gameplay physics

**Status:** Accepted

## Context
The prototype mixed PhysX and Unity Physics paths.

## Decision
All gameplay collision and dynamics use Unity Physics (DOTS). PhysX (`com.unity.modules.physics`) stays installed only because Unity Physics bakes from PhysX authoring components; `Physics.simulationMode = Script` so it never simulates. Projectiles are casts, not bodies. Nobody assigns `PhysicsVelocity` directly; motion comes from impulses.

## Consequences
One physics world, deterministic on one machine. `ProjectSettingsGuardTests` checks the simulation mode.

## Alternatives considered
PhysX for some features (rejected: two worlds, two sets of bugs).
