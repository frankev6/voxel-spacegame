# 05 — Testing, verification and the feedback loop

**Status: BINDING.** A task is not done until the checks below pass on your machine.

## 1. The commands

All scripts live in `tools/`, find the project root themselves, and put the Unity CLI (`~/.unity/bin/unity`) on `PATH`.

| Command | What it does | When |
|---|---|---|
| `tools/compile.sh` | If the Editor is open: recompile through the CLI and print errors. Otherwise: headless batch compile. Exit code ≠ 0 on any compile error | After every code change |
| `tools/test.sh editmode [filter]` | Runs EditMode tests (optionally filtered by name), prints a summary, writes `Logs/test-results/*.xml` | Before every commit |
| `tools/test.sh playmode [filter]` | Runs PlayMode tests | Before merging anything touching scenes, flow or ECS world setup |
| `tools/test.sh perf [filter]` | Runs `Performance` category tests and prints medians vs budgets | Tasks with a perf gate |
| `tools/check-docs.py` | Validates specs and ledger: unique task IDs, valid dependencies, every ledger row has a spec task, spec headers complete | After editing any doc |
| `tools/ledger-sync.py` | Adds ledger rows for new spec tasks (never deletes) | After adding tasks to a spec |
| `tools/playtest/*.sh` | Drive a running Editor: snapshot, logs, keys, mouse, screenshots, perf (see `tools/playtest/README.md`) | Manual and scenario checks |

The Editor can stay open while you work; the scripts talk to it through the Unity CLI and `com.unity.pipeline`. If a third-party modal dialog ever blocks the Editor, the CLI times out: close the dialog.

## 2. Test layout

| Assembly | Folder | Contents |
|---|---|---|
| `Game.Tests.Common` | `Assets/Game/Tests/Common/` | The shared test kit (SPEC-71 TST-001): `EcsTestWorld`, `GameTestBase`, `PerfRun`, `PerfSettings`, fixtures, replay helpers. Test-only (`UNITY_INCLUDE_TESTS`); referenced by both test assemblies |
| `Game.Tests.EditMode` | `Assets/Game/Tests/EditMode/<Module>/` | Pure logic, data structures, Burst jobs run synchronously, ECS systems in a test `World` |
| `Game.Tests.PlayMode` | `Assets/Game/Tests/PlayMode/<Area>/` | Scene flow, scenarios, save/load, performance |

- Mirror the module folder: tests for `Game.Voxel` go in `Tests/EditMode/Voxel/`.
- Category attributes: `[Category("Voxel")]` plus `[Category("Performance")]` or `[Category("Scenario")]` where relevant.
- ECS system tests derive from `GameTestBase` (leak and unexpected-log guard) and use `EcsTestWorld` (SPEC-71 TST-001), which creates a world with only the systems under test, steps fixed ticks (`StepTicks`) or frames (`StepFrame`) and disposes everything.
- Performance tests use `PerfRun` and assert with `PerfSettings.AssertReference` (SPEC-71); they never time with `Stopwatch` directly.
- A task whose tests need the kit lists `TST-001` in **Depends on** (not `SPEC-71`). Only tasks that need the game-flow fixture or replay (TST-002/003) depend on `SPEC-71`.

## 3. Required kinds of tests

| Kind | Required for | Example |
|---|---|---|
| Unit | Every pure function and data structure | Orientation group closure; DDA hit cell |
| System | Every ECS system | Resolver applies events deterministically regardless of job order |
| Property / fuzz | Encoders, meshers, splitters | Random structures: greedy-box volume = solid cell count |
| Round trip | Every serializer and converter | Save → load → save is byte-identical |
| Determinism | Simulation and generation | Same seed → same galaxy hash; same inputs → same cell-state hash at 30/60/144 FPS |
| Scenario (PlayMode) | Every bug class the old prototype had | Load from pause menu keeps the player position; editor round trip keeps position; jump lands at lane entry |
| Performance | Every gate in 06 that the task touches | 1M-cell station idle ≤ 2 ms |
| Leak | Anything allocating native memory | `NativeLeakDetection.Mode = Enabled`; test fails on leak |

## 4. Definition of Done (every task)

- [ ] Every acceptance criterion in the task is met and checked.
- [ ] Tests listed in the task exist and pass; existing tests still pass (`tools/test.sh editmode`).
- [ ] `tools/compile.sh` shows zero errors and zero new warnings in `Game.*` assemblies.
- [ ] No rule in [02-conventions.md](02-conventions.md) is broken (self-review checklist in [04-workflow.md](04-workflow.md)).
- [ ] Perf gates touched by the task are measured and recorded in the ledger log.
- [ ] Docs updated if behaviour or a contract changed (spec *Status*, glossary, architecture only via ADR).
- [ ] `docs/LEDGER.md` row set to `done` with the commit hash, and a log entry written.

## 5. Old-prototype regression scenarios

These bugs were reproduced in the old game (see `docs/reference/audit-2026-10/LEDGER.md`). Each must become a PlayMode scenario test in the spec that owns the behaviour:

| Old ID | Symptom | Owning spec |
|---|---|---|
| R28 | Loading from the pause menu teleports the player and unloads the star system | SPEC-05, SPEC-31 |
| R16 / R19 | Ship editor round trip drifts the player and softlocks interaction | SPEC-51, SPEC-01 |
| R30 | Lane jump flings the player far outside both star systems | SPEC-32 |
| R10 | Station impossible to interact with (radius from centre of a 300 m hull) | SPEC-31 |
| R15 | Ship editor duplicates items | SPEC-51, SPEC-50 |
| R25 / R26 | ECB exceptions when ships die | SPEC-13 |
| R9 | Each star system generated twice, differently | SPEC-30 |
| 04-B1 | Saves silently rejected after a content change | SPEC-05 |
