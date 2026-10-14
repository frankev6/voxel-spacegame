# ADR-0085: Announce restored docking through scene readiness

- Status: Accepted
- Date: 2026-10-13
- Decider: Tech lead

## Context

SYS-009 restores PLOC before World loads. Loader teardown currently undocks, and announcing during participant restore would reach UI before the ship and camera exist.

## Decision

DockingService retains its existing public API. RestoreDocked restores silently and marks pending notification. The existing StarSystemLoader subscribes to successful World SceneTransitionFinished and invokes an internal completion method after all readiness hooks succeed. Loader teardown preserves pending restored docking. Completion validates the loaded body and emits one restored docking event, or clears an unavailable body with a warning. Ordinary Dock/Undock retain their existing behavior; anchor replacement/removal also clears stale live docking. The loader unsubscribes on disposal. No assembly references change.

PLOC parses before committing state. Missing PLOC clears stale pose/docking. Missing GLXY regeneration preserves an independently restored PLOC pose; normal loader validation still handles a system that no longer exists. No new owner gate.

## Consequences

Boot registers the PLOC order90 participant with the existing shared docking owner. Pending notification is transient and never serialized. Tests use direct hook calls, not time delays.
