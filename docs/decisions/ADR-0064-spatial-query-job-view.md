# ADR-0064 — Separate spatial query data from main-thread handles

**Status:** Accepted (Tech lead, 2026-10-13; owner-authorized correctness fixes and spec-gap resolution).

## Evidence

AI-002's required ordered-query test, extended to run its query inside a Burst IJob, fails when the complete SpatialHash is a job field. Unity tracks the embedded GuardedHandles native references as job inputs. EndRead/CompleteAccess then touch metadata held by the job; teardown reports unsafe disposal. The same queries pass on the main thread. SPEC-02 says GuardedHandles must never enter a job, whereas SPEC-23 previously instructed consumers to pass the owning hash itself.

## Decision

Add SpatialHashQueryData containing only the five spatial data containers and their read-only query methods. SpatialHash.AsReadOnly returns this non-owning view. Jobs receive the view; the owning hash remains on the main thread for BeginRead/EndRead and disposal. Existing SpatialHash query methods delegate to the same implementation and keep their signatures. No safety-check suppression, extra allocation, state writer or assembly reference is introduced. Future job-facing hit-test overloads take the query view; existing owning-hash public APIs remain wrappers where needed.

## Consequences

All spatial data retain Unity safety tracking. The view must not outlive its owner or be used without the owner's dependency guard. AI-004 and later consumers use AsReadOnly when assigning job fields. The existing required test verifies actual Burst scheduling, ordered output, reader registration and completed disposal; no extra test is added. This is a correctness repair, not a tuning pass.
