# ADR-0104 — Concrete salvage prerequisites

**Status:** Accepted (Tech lead, 2026-10-14).

## Decision

ECO-007 consumes accepted cargo (ECO-003), hulk state/creation (DMG-012), mass (PHY-013), interaction ownership (SYS-008), and despawn (VOX-014). Use these concrete prerequisites instead of whole SPEC-12/13/31, which include unrelated advisory performance gates and later large-grid work. Keep the specified marking, deterministic yields, partial-transfer ledger, despawn and seven checks unchanged.

## Consequences

Implement salvage now. ECO-008 still owns production service registration and merchant wiring. ECO-007 owns the specified boot singleton factory and placement; no assembly-reference change is needed. Performance measurements remain opt-in and advisory.
