# ADR-0067 — Guarded audio event queue ownership

**Status:** Accepted (Tech lead, 2026-10-13)

## Context

ADR-0050 and ledger question 169 require AUD-006 to reconcile SFX native queue ownership. SPEC-64 §6.2 assumes component dependency tracking completes jobs that hold only a copied NativeQueue writer. Native container aliases require the explicit shared-memory ownership rule in architecture §3.2/§5.1.

## Decision

SfxEventQueue embeds the existing GuardedHandles Access, exactly as FxEventQueue does. Producer scheduling uses a read-only singleton, Access.BeginRead/EndRead and a NativeQueue.ParallelWriter; the atomic enqueue jobs alone disable container safety restrictions. The guard remains on the scheduling thread. The presentation drain and managed enqueue complete guarded access before touching the queue. The creation system owns both native allocations and completes all guarded producers before disposing either.

Update all existing ram, split and ship-death producers in the same change. No event layout, assembly reference, gameplay ordering or audio content changes. Later weapon and audio producers follow this pattern. This is a correctness/lifetime correction, not an optimization pass.

## Consequences

Atomic producers may overlap safely; drain/clear and world teardown wait for their actual job handles. The audio system continues draining even when unbound. Existing required drain and integrated damage tests verify the affected lifetime paths; no additional test cases are introduced.
