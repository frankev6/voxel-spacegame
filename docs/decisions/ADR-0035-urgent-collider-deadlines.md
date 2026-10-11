# ADR-0035 — Complete urgent collider work before catch-up deadlines

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous implementation instruction.

## Context

Two full EditMode runs during FLT-002 counted three late collider installs at six ticks per frame and 17 workers. A focused repeat passed. An IsCompleted check inside the fixed step cannot guarantee that detached snapshot jobs finish before a due tick; MaxBatchAgeFrames may also allow the batch to outlive that tick. Increasing the delay alone moves the same scheduling race.

## Decision

BrickColliderBuildSystem enforces a deadline boundary in VoxelRebuildGroup, outside the fixed step. Its SimClock.Tick is the next simulation tick. The next frame can execute through Tick + SimTime.MaxTicksPerFrame - 1.

- If any unbuilt urgent update is due within that horizon, an existing in-flight batch must complete and finalize before planning, even if younger than MaxBatchAgeFrames. This also frees the single batch slot for newly pending urgent work.
- After scheduling a batch, if it contains an assembly with such a deadline, complete and finalize it before returning from the variable-rate stage.
- Windows, snapshots, per-grid ordering, non-urgent brick/assembly budgets and install ticks are unchanged. Non-urgent batches and urgent batches outside the horizon retain asynchronous execution and the existing maximum-age rule. A pre-existing non-urgent backlog that blocks an urgent successor remains subject to the documented late-install diagnostic; this change does not silently remove non-urgent budgets.
- Only ColliderInstallJob changes PhysicsCollider at the due fixed tick. ColliderSwapSystem retains its IsCompleted-only handover and never waits on unfinished builds. No new system, group, component, public API, wire format or resource lifetime is introduced.
- Forced completions remain visible in ForcedBatchCompletes. The new BrickColliderBuildSystem.Deadline marker measures deadline completion and finalization; existing inclusive main-thread and worker gates remain unchanged.

## Alternatives

1. **Complete before the next catch-up frame (chosen).** Makes an urgent ready-grid update available before its due tick without a fixed-step wait.
2. Wait inside ColliderSwapSystem or chain the unfinished build into physics. Violates the fixed-step no-wait rule and moves the stall into simulation.
3. Raise delays, add sleeps to tests or retry until green. Does not guarantee completion and conceals the timing failure.

## Validation

Use a deliberately delayed detached batch to verify readiness before a following six-tick frame; retain the non-urgent age-limit test, fixed-step no-block test, strict zero-late determinism tests and all PHY-019 cost/memory gates. Record measured deadline cost in the ledger.
