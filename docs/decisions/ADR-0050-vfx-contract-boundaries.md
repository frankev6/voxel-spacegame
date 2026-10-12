# ADR-0050 — Shared VFX kinds and guarded queue ownership

**Status:** Accepted (Tech lead, 2026-10-12)

## Context

SPEC-63 puts FxKind in Game.Voxel while Game.Data's FxDefinition needs that type. Data cannot reference Voxel. The specified native queue also lacks architecture §3.2's GuardedHandles, so component dependencies alone would not guarantee completion of all native queue producers. Its validation signature predates the implemented SPEC-04 API.

## Decision

- FxKind lives in Game.Data.Definitions, at Data/Definitions/Vfx/FxKind.cs. Preserve every byte value. FxEvent and all remaining stream types stay in Game.Voxel.PresentationStreams. No new assembly reference or duplicate enum.
- FxEventQueue embeds GuardedHandles Access. Concurrent atomic enqueues are shared access: main-thread scheduling calls Access.BeginRead and Access.EndRead around the scheduled writer. Only the NativeQueue.ParallelWriter enters the job. Draining/clearing is exclusive, using Access.Acquire/Release, or CompleteAccess outside fixed-step. The creation system owns and disposes both queue and guard, completing the guard before freeing the queue. EnqueueMainThread completes guarded access first. This is a native-memory contract correction, so VFX-001 is frontier.
- Use the canonical ContentDefinition.Validate(ValidationScope, ContentValidationContext). Report asset-bound fields and stable VFX error codes. No compatibility overload or change to the data validator API.
- VFX-001 implements the stated strict missing-default checks. VFX-004 explicitly relaxes missing defaults for its six later-content kinds until VFX-014 restores strictness; this staged content policy does not change the queue or layout contracts.
- FxRig remains an unbound, throwing contract stub until VFX-002. Do not hook a throwing stub into boot. VFX-003 owns the drain implementation; its documented producer/consumer pattern is corrected now so later implementations use the guard.

## Consequences

Definitions and producers share one enum without a dependency cycle. Producers may enqueue concurrently while consumers and world disposal wait for their actual jobs. GPU layouts remain 56/80/48/224 bytes as specified. The existing SfxEventQueue is outside this task; AUD-006 must reconcile its producer/drain ownership with the same architecture rule before introducing asynchronous producers.
