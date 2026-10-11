# ADR-0019 — System reviews instead of per-task frontier reviews

**Status:** Accepted (owner, 2026-10-11)

## Context
04-workflow §7 required every `frontier` task to get an independent review by a second frontier-class session before it could be `done`, and dependents start only on `done`. With one worker and no subagents, every frontier task stopped the work behind it. Almost all of M0 is frontier-tier, so the build stalled one task at a time.

Owner, verbatim (2026-10-11):
- On how frontier reviews should happen: "bro you are the frontier!"
- "also remove hardcoded blockers requiring review for every small implementation. our first buildout i would rather we go wide and then have an in-depth review when systems are built, because reviewing one item at a time misses the larger picture of those components working together holistically, and also just slows us down"

## Decision
1. **No per-task review gate.** A task of either tier is `done` when it meets the Definition of Done (05-testing §4) and the self-review checklists (04-workflow §3, 07-implementation-traps). It no longer needs `reviewed-by:` in the ledger, and its dependents may start at once.
2. **Tiers still decide who may take a task.** `frontier` tasks remain reserved for frontier-class models (04-workflow §7.1–§7.2); the review requirement of §7.2 rule 4 becomes a checklist for the system review.
3. **System reviews.** When the systems of a milestone are built, and always before its exit gate (ROADMAP), a frontier-class session that did not write the code does an in-depth review of those systems working together. It covers the §7.2 rule 4 checklist (spec conformance, job safety with leak detection, determinism, edge-case tables, perf gates, hot-path allocations), the cross-module flows, and integration tests. Findings become new tasks in the owning specs (`tools/ledger-sync.py`) or ledger log entries; a milestone passes its gate only when its system review is logged in the ledger with `system-review: name (model)`.
4. **The owner may ask for a review of any part at any time.** That is the only per-task review.

## Consequences
- 04-workflow §4 (`review` status), §7 and the ledger header are updated; AGENTS.md §2 no longer requires a per-task review; `tools/check-docs.py` no longer rejects a `done` frontier task without `reviewed-by:`.
- Tasks waiting only for a per-task review (`ART-003`, `ART-005`, `ART-012`) become `done`. `ART-004` stays in `review` for its open acceptance criterion (the 150 m glyph test), not for the review.
- Bugs are found later, at the system review, and can ripple through work built on them. The system review's integration focus is meant to catch the problems a one-task review misses.

## Alternatives considered
- **A review subagent per frontier task.** Keeps reviews independent and continuous, but the owner wants no subagents and finds one-task reviews miss how components work together.
- **Building on `review` with batched reviews.** Still one review per task, only reordered; the owner preferred reviewing whole systems.
- **Waiting for each review.** Safest; the slowest by far.
