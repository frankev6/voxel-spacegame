# ADR-0042 — Advisory performance targets; build systems before tuning

**Status:** Accepted (owner, 2026-10-12)

## Context

Initial implementation spent too long tuning isolated benchmarks while the game systems were not yet connected into a playable loop. Earlier documents made numeric performance targets mandatory completion and milestone gates, which encouraged this behaviour.

Owner, verbatim (2026-10-12):

> can you reword handoff or docs to say the spec performance goals are just suggestions but not blockers to move on to the next phases. no worker should spend more than 2 passes initially in optimizing blindly just for spec numbers.

## Decision

1. **Performance numbers are suggestions, not progression blockers.** Timing, FPS, memory footprint, allocation-rate, throughput and geometry-count targets in any spec, acceptance checklist, roadmap, test or earlier ADR are advisory. Missing a number does not prevent finishing a functional task, merging it locally, starting its dependents, or advancing to the next phase/milestone. This decision takes precedence over older wording such as “perf gate”, “must pass”, “no-go” or “not done” where the only issue is a performance target.
2. **At most two initial optimization passes per system or feature.** A pass is one bounded, targeted change followed by measurement and a keep/revert decision. Zero passes are needed when no demonstrated problem warrants tuning. Use a baseline/profile and a concrete hypothesis; do not optimize blindly to satisfy a spec number. Benchmark/profiler troubleshooting counts toward the same limit when it is being pursued only to establish or improve that number. The count carries across tasks, workers, branches and handoffs; changing measurement tools does not reset it. Existing work that has already exceeded two passes moves on now.
3. **Record and move on.** Keep the stated target and truthful results, including missed numeric targets or unavailable measurements. Record the pass count, remaining concern and deferred follow-up in the ledger. Functional tasks may be `done` with `performance deferred` in Notes once their functional Definition of Done is met. Do not add deferred tuning as a prerequisite for working features or later phases. Do not relabel a failed or unmeasured benchmark as passing, relax a threshold to manufacture green results, or keep rerunning it hoping to pass.
4. **Build and exercise the game.** Prioritize connected systems and a live gameplay check through the existing Unity CLI/playtest harness. Return to optimization as a separate, scoped effort after an integrated playtest/profile shows a player-visible bottleneck, or when Frank explicitly requests it. A later system review records performance findings without making the suggested numbers blockers again.
5. **Correctness remains required.** Compile errors, broken behaviour, crashes, data corruption, nondeterminism, leaks, unsafe lifetimes and actual inability to run the game remain defects to fix. A memory-footprint target is advisory; a leak or out-of-memory failure is not. Architecture, ownership, packed data formats and functional capacity requirements remain binding. Safety limits and format bounds are not performance suggestions. Fixing a demonstrated functional defect is not speculative optimization.

## Consequences

- AGENTS, HANDOFF, architecture scalability guidance, workflow, testing, performance budgets, ROADMAP and SPEC-71 use this policy. The default suites are green-or-real-failure: performance-only timing, footprint and readiness assertions warn with measured values and unchanged targets; functional assertions in mixed tests remain hard failures. PerfRun JSON and the explicit perf summary retain FAIL records for missed targets.
- Numeric milestone targets become measurements and follow-ups. Functional integration, system reviews and explicit owner/art approvals still apply.
- The renderer benchmark investigation stops blocking renderer/input integration. This policy does not itself mark REN-003 or any other implementation complete or claim a playable game exists.
- No runtime code, test thresholds or architectural data contracts change in this documentation decision.

## Alternatives considered

- Keep optimizing each task until every number passes: rejected by the owner because it stalls playable systems.
- Remove performance tests and budgets: rejected; measurements remain useful evidence for later prioritization.
- Allow unlimited tuning whenever a microbenchmark misses its target: rejected; further effort needs evidence from the integrated game or an explicit owner request.
