# ADR-0077 — Narrative body removal through the galaxy owner

**Status:** Accepted (Tech lead, 2026-10-13).

## Context

SPEC-40 RemoveBody must remove the target rather than preserve a destroyed station hulk. GalaxyService exposes only destruction and story-spawn removal. Deleting a saved structure while a live spawn still reads it is unsafe, and normal streaming capture can resurrect a deleted snapshot.

## Decision

Add GalaxyService.RemoveBody(BodyId) and IGalaxyLiveDeltaSink.PrepareBodyRemoval(BodyId). The service calls the live sink before clearing the structure snapshot and marking the existing destroyed key; story spawns also use their existing Removed tombstone. No save schema changes are needed: a destroyed body with no structure snapshot is absent, while a destroyed body with a snapshot remains a hulk.

The loader suppresses capture for the target's active/retiring T2 leases, transfers any delta-owned source to its lease before removing the snapshot record, and requests ordinary retirement. The source remains alive until spawn readers and grid retirement finish. Flush/save cannot capture the removed lease again. Anchor creation excludes destroyed bodies without snapshots, on both refresh and restore. Destruction remains unchanged for surviving hulks.

## Consequences

Narrative code calls one galaxy operation; it does not mutate deltas or ECS state directly. Removal is persistent without a new GLXY version. Source retirement remains serialized with the existing streaming lifecycle. The NAR-011 routing acceptance case must cover removal and snapshot suppression.
