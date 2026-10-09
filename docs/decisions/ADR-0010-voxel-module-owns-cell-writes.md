# ADR-0010 — Game.Voxel owns damage resolution, integrity and splitting

**Status:** Accepted

## Context
The audit synthesis put damage in the Simulation module and integrity in Voxel. Splitting cell writes across two modules invites second writers.

## Decision
Everything that writes cells lives in `Game.Voxel` (SPEC-13): `DamageResolveSystem`, `IntegritySystem`, `SplitSystem`, `ShipDeathSystem`, `ImpactEventSystem`. `Game.Simulation` only produces `DamageEvent`, `ShieldHit` and consumes `ExplosionRequest` (`ExplosionSystem`, SPEC-21).

## Consequences
Single writer for cells is enforced by module boundaries, not just discipline.

## Alternatives considered
Damage in Simulation (rejected: two modules mutate cells).
