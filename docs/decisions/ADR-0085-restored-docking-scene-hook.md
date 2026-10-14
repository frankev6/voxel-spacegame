# ADR-0085: Announce restored docking through scene readiness

- Status: Accepted
- Date: 2026-10-13
- Decider: Tech lead

## Context

SYS-009 restores PLOC before World loads. Loader teardown currently undocks, and announcing during participant restore would reach UI before the ship and camera exist.

## Decision

DockingService implements the existing ISceneReady contract at SceneHookOrder.Ui. Its existing RestoreDocked API restores silently and marks pending notification. Galaxy's loader installs an internal body-validation callback; no assembly reference changes. Loader teardown preserves pending restored docking. Once the sequential player/camera hooks have completed, the docking hook validates the current loaded body, emits one restored docking event, or clears an unavailable body with a warning. Ordinary Dock/Undock retain their existing behavior.

PLOC parses before committing state. Missing PLOC clears stale pose/docking. Missing GLXY regeneration preserves an independently restored PLOC pose; normal loader validation still handles a system that no longer exists. No new owner gate.

## Consequences

Boot registers the shared docking scene hook and PLOC order90 participant. Pending notification is transient and never serialized. Tests use direct hook calls, not time delays.
