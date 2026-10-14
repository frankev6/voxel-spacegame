# ADR-0080 — Cached world-event counter effects

**Status:** Accepted (Tech lead, 2026-10-13).

## Context

SPEC-40 §5.6 requires cached AddFactEffect instances, variable item acquisition counts, and WorldEvent provenance. The current effect binds a fixed authored amount and always records Effect provenance. Its proposed int[] cache cannot hold effect instances.

## Decision

Add an explicit pre-bound AddFactEffect(int boundSlot, bool allowReserved) constructor and Apply(ILogicContext, int amount, FactWriteSource source) overload for code-owned counters. Keep existing authored constructors, binding and Apply(context) behavior unchanged. Both paths check bounds, integer kind and reserved-write permission. The bridge caches AddFactEffect[] by slot, uses allowReserved:true, and supplies WorldEvent on each application. The overload does not parse or allocate per event. Bound effects belong to the catalog/context lifetime that created them and cannot be shared across sessions with different catalogs.

## Consequences

World-event writes retain correct tracing and variable counts without rebuilding graphs. NAR-012's required interaction and allocation cases cover the path in the next compatible batch. This is a contract prerequisite, not acceptance of the unfinished bridge. QST-005 owns beat execution; composition must not instantiate its throwing staged contract before that implementation exists.
