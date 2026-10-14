# ADR-0083 — Interaction docking composition

**Status:** Accepted (Tech lead, 2026-10-13).

## Context

SYS-008 owns query events and docking but the implemented loader constructor has no docking service input. The live scene must expose the same state that its BodyInteracted handler writes.

## Decision

Add an optional trailing DockingService constructor input to StarSystemLoader. Existing isolated loader callers get a loader-owned state instance; production composition constructs and registers one service and injects it. Subscribe at construction, unsubscribe on disposal, ignore stale-system/unknown-body events, mark canonical body interaction and dock through this shared owner. Scene unload undocks once. Marking the Interacted flag does not rebuild streamed anchors; it has no geometry effect.

Dock is idempotent for the current body, rejects invalid BodyId, and publishes one DockStateChanged per actual state change. Undock is idempotent and reports the previous body's identity. RestoreDocked remains SYS-009 until PLOC restore lifecycle is implemented.

## Consequences

No UI or merchant behavior is fabricated. SYS-008's real query, docking/event acceptance and singleton lifetime remain required before task closeout. Public constructor compatibility is preserved; assembly references do not change.
