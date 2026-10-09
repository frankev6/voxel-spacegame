# Progress ledger

**Living document.** The single place that says what is done, in progress, blocked and unanswered. Every worker updates it when they claim a task, finish a task, discover a question or measure a gate (see [04-workflow.md](04-workflow.md)).

- **Task rows** are created by `tools/ledger-sync.py` from the specs. Never add or delete rows by hand; edit only **Status, Owner, Updated, Commit, Notes**. *Ready* = every dependency is `done` (recomputed by the sync).
- **Statuses:** `todo`, `in-progress`, `blocked`, `review`, `done`, `dropped` (meanings in 04-workflow §4).
- To find work: current milestone in [ROADMAP.md](ROADMAP.md), `Status = todo`, `Ready = yes`, and a **Tier** you may take: `frontier` is reserved for frontier-class models such as Claude Opus or GPT Astra, and needs an independent review; `standard` is open to anyone. See [04-workflow §7](04-workflow.md#7-task-tiers-which-model-may-take-which-task).
- **Owner** is `name (model)`, e.g. `alex (claude-opus-5-5)` or `frank (human)`. A `frontier` task is `done` only with `reviewed-by: name (model)` in *Notes*.
- `tools/check-docs.py` fails if a spec task has no row, a status is invalid, or a `done` row has no commit hash.

## Summary

<!-- summary:begin -->
| Milestone | todo | in-progress | blocked | review | done | dropped | total | ready to start |
|---|---|---|---|---|---|---|---|---|
<!-- summary:end -->

## Owner gates

| Gate | Spec | Status | Approved on | Notes |
|---|---|---|---|---|
| Design system / art direction approved | SPEC-80 | waiting for proposal review | — | Blocks all SPEC-81 asset production |
| Yarn Spinner 3 adoption | SPEC-41 | waiting for spike | — | Fallback format defined in SPEC-41 |

## Tasks

<!-- tasks:begin -->
| ID | Title | Spec | Milestone | Tier | Depends on | Ready | Status | Owner | Updated | Commit | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
<!-- tasks:end -->

## Open questions

Questions found while working. Use the *Default* until the *Decider* answers; then record the answer and update the spec.

| # | Date | Spec / task | Question | Default in use | Decider | Answer |
|---|---|---|---|---|---|---|

## Measurements

Record perf gate measurements here (date, task, scenario, numbers, machine).

| Date | Task | Gate | Result | Notes |
|---|---|---|---|---|

## Log

Newest first. One entry per finished task or notable event: what changed, measurements, follow-ups.

### 2026-10-09 — Project bootstrap (lead)
- Created the Unity 6000.6.5f1 project with the Unity CLI (URP template, stripped of samples), package manifest per ADR-0011, 17 assembly definitions per architecture §3, `ProjectSetup` (required settings + guard test), assembly-rule and conventions guard tests, PlayMode boot smoke test, ported the generic CLI harness and playtest scripts, `tools/compile.sh`, `tools/test.sh`, `tools/check-docs.py`, `tools/ledger-sync.py`.
- Carried over placeholder art (`Assets/Game/Art/_Placeholder/`), audio (`Assets/Game/Audio/`), and the legacy content export (`Import/Legacy/`). The prototype audit is under `docs/reference/audit-2026-10/`.
- Wrote the binding foundation docs (00–06, ROADMAP, ADR-0001…0012) and the system specs (SPEC-01…SPEC-81).
