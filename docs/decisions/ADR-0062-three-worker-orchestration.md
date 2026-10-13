# ADR-0062 — Three persistent implementation workers

**Status:** Accepted (owner, 2026-10-13: “you would also be reviewer and fix any small code issues you find along the way. go ahead and run the 3 workers. CHANGE your goal! lets continue forth”)

## Decision

Use exactly three separate reusable Codex threads on GPT-6.1-Sol high, with isolated local worktrees. No subagents. The lead assigns ready standard tasks, owns central ledger claims/closeout and main integration, reviews delivered work and fixes small issues, and personally implements frontier tasks. Keep one active task per worker branch. New tasks start from integrated main; workers never merge main or touch remotes.

Workers implement assigned functional scope and spec-required tests together, then deliver a local implementation commit and evidence to the lead. They do not run Unity or alter the live Editor. The lead owns the shared compile/test channel, batches compatible deliveries, runs correctness/safety checks and required GPU/live evidence, and marks tasks done only after validation. Pending validation remains in-progress. This explicitly replaces repeated worker-level full-suite runs and independent ledger edits. Workers may run read-only checks and docs consistency locally. Preserve all existing art, safety and performance policy.

The lead polls all three threads at bounded work boundaries and before/after long tests, immediately resolves deliveries/blockers and reuses threads for new eligible work. A durable dispatch board holds exact thread/worktree/task state and follow-up queues. A heartbeat checks continuation when the executor is available; it never authorizes duplicate workers or concurrent Unity runs. Dependency waits are recorded, not disguised with busywork. Frank authorizes coordinator assignments and follow-ups to these worker threads; workers report in their own thread, which the lead reads.

## Goal

Continue the roadmap as playable integrated slices: physical breakup, complete combat and an enemy that fights back, then the M3 game loop and later milestones. Keep three workers supplied with eligible standard tasks while the lead handles frontier implementation, reviews, small fixes and integration. Performance targets remain advisory with at most two initial optimization passes per system across the team. Git stays local and owner art restrictions remain binding.

## Tracking

See `docs/ORCHESTRATION.md`. The prior app goal is blocked and cannot be replaced by the available tool while unfinished; do not falsely mark it achieved. Repository goal and scheduled continuation carry the owner instruction until the app goal can be updated.
