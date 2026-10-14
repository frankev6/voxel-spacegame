# ADR-0112 — Explicit map route edges

**Status:** Accepted (Tech lead, 2026-10-14).

## Decision

Append bool Route to the existing MapLine presentation contract, default false. GalaxyMapPresenter sets it only for consecutive edges from the current system through TravelService.Route. MapCanvas accents line.Route rather than every lane touching a destination point. Destination point flags still identify only the actual destination.

## Rationale and consequences

A destination point alone cannot identify a multi-hop route or distinguish an unrelated branch touching that point. Clearing or changing a route rebuilds these flags from the canonical travel owner. No travel state, package, assembly reference or simulation writer changes. Extend HUD-016's existing Destination_RouteDrawnAndCleared test to check actual route edge selection and clearing; add no test case. Lead mirrors global question492.
