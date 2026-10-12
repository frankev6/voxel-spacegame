# ADR-0045 — Drain the physics build during paused scene teardown

**Status:** Accepted (Tech lead, 2026-10-12)

## Context

The first real imported-ship World restart logged surviving PhysicsTemporalCoherenceInfo cleanup entities after ten frames. Unity Physics owns their cleanup in PhysicsBuildWorldGroup and plays it back at EndFixedStepSimulation. SceneFlow pauses before destroying roots, so that fixed-step cleanup cannot run. Isolated player fixtures had not run the full incremental broadphase.

## Decision

After completing jobs and destroying non-boot entities, SceneFlow updates the existing PhysicsInitializeGroup once with zero delta time, completes its jobs, then plays the existing EndFixedStepSimulation ECB and completes jobs again. The temporary World time is restored in finally. This drains the package-owned invalidation/cleanup and rebuilds its now-empty collision world before deferred collider disposal. Worlds without a physics initialization group skip this step.

Do not manually remove PhysicsTemporalCoherenceInfo or mutate broadphase storage. Do not unpause, run gameplay, advance SimClock, or run a physics simulation tick. Normal package systems remain their state owners; this narrowly scoped shutdown dispatch is an exception to normal frame scheduling, not a new writer.

## Validation

FND-017's production-world controls/scene/save-load tests and live repeated World entry exercise this path, including static imported obstacle removal. Retain the existing ten-frame bound for other cleanup systems.
