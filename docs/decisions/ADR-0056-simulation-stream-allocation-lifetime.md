# ADR-0056 — Simulation event streams may outlive four rendered frames

**Status:** Accepted (Tech lead, 2026-10-12)

## Context

FND-020 consumes reactor/core explosion registrations on the following simulation tick. Simulation ticks are independent of rendered frames, and pausing can delay that tick indefinitely. A TempJob allocation has a four-rendered-frame lifetime, so it cannot safely own these registrations. Live firing emitted expired-allocation warnings; shutdown ownership was otherwise clean.

## Decision

SimEventChannel allocates its registered NativeStreams with Allocator.Persistent. Existing EndRead, stale-registration cleanup and Dispose retain sole disposal ownership and their job fences. The read-set metadata arrays remain TempJob because they are consumed and scheduled for disposal within the same update. This changes allocation lifetime, not event order, API, tick semantics or delivery. Do not add fixed-step main-thread completion to mask the lifetime mismatch.

## Validation

Run the existing event-channel tests, FND-020 pause/restart coverage and full suites. Repeat live sustained fire and shutdown, checking for expired stream allocations and native leaks. This is a correctness fix, not a performance optimization pass.
