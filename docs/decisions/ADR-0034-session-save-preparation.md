# ADR-0034 — Session preparation and asynchronous save thumbnails

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous implementation instruction.

## Context

FND-009 connects the real session to SaveService. Calling RunWritebacks only from GameSession.SaveGameAsync misses timer/scene/quit autosaves, which call SaveService directly. Participants must still capture already-written-back managed state. SPEC-01 requires asynchronous slot readback with a two-frame limit, but ThumbnailRequest only holds completed pixels and SaveService currently discards a request whose pixels are not yet present.

## Decision

- Add `void PrepareCapture()` to `ISaveContext`. After the final save-block check succeeds and before any participant captures, SaveService invokes it once on the main thread. GameSession runs ordered writebacks there. A failure uses the existing capture-failure cleanup and leaves files untouched. No changes to ISaveParticipant or the on-disk format.
- Add `bool Pending` to `ThumbnailRequest`. Its producer sets Pending before returning an asynchronous request, then writes completed RGBA32 pixels (or null on error) and clears Pending on the main thread. The producer owns and releases GPU resources even if a save stops waiting.
- SaveService allows at most two frame yields after capture for pending thumbnails, before starting its background writer. Requests still pending, or without pixels, are omitted from that save. Worker serialization receives only completed pixel data. CaptureMs excludes that asynchronous wait; total duration includes it.
- GameSession owns thumbnail capture resources and receives its camera explicitly from composition once CAM supplies it. Until a camera exists, in the Lab, or with unavailable GPU readback, slot capture returns false. Use URP render requests, not a second scene camera or a lookup.

## Flow ordering

A scene exit previously set Exiting before requesting its autosave, so GameSession would block that same save as Transitioning. GameSession now reserves its flow operation, holds a preparation pause through the exit, performs a requested exit save while SceneFlow is Idle, and only then enters the transition. Other public flow calls return Busy during that reservation. Quit with saveFirst=false skips saving. Preparation failures and exit-writeback exceptions recover through the existing failure paths; this does not relax save blocks during scene teardown/loading.

## Alternatives

1. **An explicit preparation hook and bounded pending state (chosen).** One path covers manual, automatic and quit saves without module coupling.
2. Perform writebacks inside SESS.Capture. Violates participant capture's managed-snapshot precondition and makes correctness depend on chunk order.
3. Synchronous GPU readback. Adds an unbounded main-thread stall to capture and contradicts SPEC-01.

## Consequences

SPEC-01 and SPEC-05 declare the hook and pending state together. Existing contexts implement a no-op when they have no ECS state; their ready thumbnails remain unchanged. Preparation failure and bounded pending-result handling receive save edge-case tests. The camera binding remains at composition's CAM step, where the actual camera is constructed. No package, assembly-reference, wire-format or memory/performance-budget change.
