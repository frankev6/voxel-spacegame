# ADR-0037 — Fresh spawn bodies initialize without gameplay ticks

**Status:** Accepted (Tech lead, 2026-10-11; autonomous technical-gap authority)

## Context

SceneFlow keeps its pause token while PlayerShipSpawner waits for GridSpawning to finish and PhysicsCollider to exist. GridSpawnSystem runs during initialization, but GridBodyInitSystem only runs in the fixed step. A fresh player therefore cannot become ready while loading is paused.

## Decision

- Add GridSpawnBodyInitSystem to GameInitGroup, after GridSpawnSystem. It selects GridBodyInit-enabled roots with GridSpawning, GridRebuilt and GridMassRecompute disabled and no PhysicsCollider.
- Reuse GridBodyInitJob and GridBodyFactory, the existing dependency ownership, provisional blob cache and first-build queue. Record initial body components into EndInitializationEntityCommandBufferSystem, which plays back after GameInitGroup.
- This only changes initial body creation for fresh, fully converted spawns. Existing-body split adoption and rebuild requests remain in GridBodyInitSystem in GameplayPrePhysicsGroup, retaining their simulation tick and install-deadline rules.
- The initialized body keeps the requested pose and velocity. Neither SimClock nor gameplay advances to satisfy readiness. The provisional collider is sufficient for the existing ready contract; final collider construction/installation continues through the ordinary pipeline.
- Keep the fixed-step initializer as the fallback for callers whose mass is not prefetched. No duplicate body is created: the shared job disables GridBodyInit, and end-init ECB playback finishes before the fixed step.

## Consequences

Paused loading can finish without releasing anyone's pause token. Fresh spawn conversion was already frame-driven; damage and split timing remain fixed-step. No new group, component layout, package or assembly reference is introduced. Collider ownership and retirement stay with the current cache owner.
