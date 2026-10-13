# ADR-0070 — Focused validation and opt-in long runs

**Status:** Accepted (owner, 2026-10-13).

## Context

Frank rejected three-minute tests and the implementation time lost to repeated validation and test repair. Full runs included Performance tests and a ten-minute destruction soak, leaving three delivered workers waiting.

## Decision

Owner correction (2026-10-13): compile once per compatible batch and run focused functional checks for changed behavior. Do not require a full suite per task, commit or worker delivery. Passing relevant acceptance checks unlock task closeout and dependent work; unrelated benchmark/soak failures remain recorded follow-ups. Broad functional regression belongs at meaningful integrated gameplay checkpoints or milestone review. Performance and long soak tests are opt-in only. A routine test approaching three minutes is a workflow defect: stop repeated diagnosis, record it and isolate/shorten its fixture; never increase timeouts or weaken functional assertions to obtain green. Preserve spec-required coverage, add no speculative tests, and prioritize implementation and live gameplay.

Default EditMode/PlayMode commands exclude Performance and LongRunning. Explicit perf/soak commands retain original measurements, durations and hard functional assertions. The installed live Pipeline API cannot combine/exclude categories: focused ordinary name selections remain supported; a selection containing excluded tests fails before dispatch with instructions to narrow it or use headless mode. No implicit fallback launches an expensive suite.

## Consequences

Focused acceptance is required; broad regression is no longer a per-task dependency. Record exact tested scope and deferred failures, never claim full-suite green from focused results. Unity remains serialized, but unrelated workers continue implementing while it runs. Test/tool infrastructure failures do not justify repeated unchanged runs.
