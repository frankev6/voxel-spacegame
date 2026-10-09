# 04 — Workflow for implementers

**Status: BINDING.** This is how every worker (human or AI) picks up, does and finishes work so that many people can build in parallel without drifting apart.

## 1. Before you write code

1. Read, in order: [AGENTS.md](../AGENTS.md) → [00-vision](00-vision.md) → [01-architecture](01-architecture.md) → [02-conventions](02-conventions.md) → [03-glossary](03-glossary.md) → the spec you'll work on → the specs it depends on (at least their *Decisions* and *Data model*).
2. Pick a task in [LEDGER.md](LEDGER.md):
   - status `todo`,
   - in the current milestone (see [ROADMAP.md](ROADMAP.md)),
   - every `Depends on` task is `done`.
3. Claim it: set status `in-progress`, put your name in *Owner*, today's date in *Updated*. Commit that one-line change on `main` (or tell the lead) before starting so two people don't take the same task.

## 2. While working

- Do **exactly** what the task says. If you discover extra work, add it as a new task to the spec (next free ID, `Depends on` filled in), run `tools/ledger-sync.py`, and leave it `todo`. Don't silently expand scope.
- **Ambiguity rule.** If the spec doesn't say how to do something:
  1. Check architecture, conventions and glossary. If they decide it, follow them.
  2. If the spec lists the question in *Open questions*, use its *Default*.
  3. Otherwise, choose the simplest option that keeps every binding rule, write it as a new row in the spec's *Open questions* with your choice as the default, and add an entry to the ledger *Open questions* section. Keep going.
- **Contradiction rule.** If two documents disagree, architecture beats spec, and an ADR beats architecture. Log the conflict in the ledger; don't pick a third way.
- **Contract changes.** If you need to change a type listed in architecture §6 or another module's public API, stop and write an ADR draft (`docs/decisions/ADR-NNNN-*.md`, status *Proposed*). Work on something else until it's accepted.
- Keep the Editor-driven loop fast: `tools/compile.sh` after edits, `tools/test.sh editmode <filter>` for your tests.

## 3. Finishing

1. Run the full Definition of Done in [05-testing.md §4](05-testing.md#4-definition-of-done-every-task).
2. Self-review against this checklist:
   - [ ] No managed `IComponentData`, no `SystemBase` outside allowed modules, every system has `[UpdateInGroup]` and `RequireForUpdate`.
   - [ ] No new assembly reference outside the architecture §3 table.
   - [ ] No mutable statics without a `SubsystemRegistration` reset; no `Resources.Load`, `Find*`, self-creating singletons.
   - [ ] No `UnityEngine.Random`, `System.Random`, `GetHashCode` in simulation or saves.
   - [ ] No per-frame allocations in hot paths; no logs per frame.
   - [ ] No hard-coded colours, sizes, fonts or asset paths; placeholders referenced through definitions or tokens.
   - [ ] Every `TODO` has a task ID.
   - [ ] Tests named and placed per conventions §7.
3. Update [LEDGER.md](LEDGER.md): status `done`, commit hash, and one entry in the *Log* (what changed, measurements, follow-ups).
4. Commit `<TASK-ID>: <summary>` on `task/<TASK-ID>-<slug>`, then merge to `main` (rebase first). One task per merge.

## 4. Statuses

| Status | Meaning |
|---|---|
| `todo` | Ready to be claimed when dependencies are done |
| `in-progress` | Claimed; owner is working on it |
| `blocked` | Can't proceed; the *Notes* column says on what (task ID, ADR or owner decision) |
| `review` | Implementation finished, waiting for review or an owner check |
| `done` | Definition of Done met, merged |
| `dropped` | Removed from scope by an ADR or the owner; keep the row |

## 5. Decisions and ADRs

- Binding decisions live in `docs/decisions/ADR-NNNN-title.md` (template: `ADR-0000-template.md`).
- An ADR is *Proposed* until the owner (Frank) or the tech lead marks it *Accepted*. Only accepted ADRs change rules.
- When an ADR changes a spec, the same commit edits the spec and links the ADR.

## 6. Owner gates

Some work needs the owner's explicit approval before it starts or ships. These appear as tasks whose title starts with **GATE:**. A gate task is done only when the owner's approval (date and note) is written into the spec and the ledger. Current gates:

- **Art direction / design system** (SPEC-80): blocks all of SPEC-81 asset production.
- **Yarn Spinner adoption** (SPEC-41): after the compatibility spike.
- Any change to [00-vision.md](00-vision.md) scope.

## 7. Task tiers (which model may take which task)

Every task in a spec has a **Tier**. It says how much capability the task needs so that the foundation is built by the strongest workers and nothing subtle ships broken.

| Tier | Who may take it | Extra requirement before `done` |
|---|---|---|
| `frontier` | Frontier-class models at their highest reasoning setting (Claude Opus class, GPT Astra class), or a senior human engineer | An **independent review** by a second frontier-class session that did not write the code. Record `reviewed-by: name (model)` in the ledger *Notes*. |
| `standard` | Standard-class models (Claude Sonnet class, GPT Sol class) or better | The normal Definition of Done |

### 7.1 What makes a task `frontier`

A task is `frontier` if **any** of these hold. When in doubt, it is `frontier`.

- **F1 Foundation.** Other systems build on it and it is expensive to change later: boot and service lifecycle, ECS world and system-group wiring, the voxel cell/brick data model and the cell write path, the damage event pipeline, content baking into blobs, the save container and versioning, origin rebasing, RNG streams, hashing and determinism.
- **F2 Concurrency and memory safety.** Burst jobs with parallel writers; `NativeStream` / `NativeParallelMultiHashMap` producers and consumers; job dependencies chained across systems; unsafe code (`UnsafeList`, pointers); native allocations that live longer than one frame; deferred disposal.
- **F3 Silent-corruption algorithms.** Bugs show up as wrong data instead of a crash: connectivity/integrity and splitting, greedy meshing and collider merging, face culling across brick borders, binary encoding/decoding and migrations, deterministic ordering of events.
- **F4 Budgeted hot paths.** The task owns a number in [06-performance-budgets.md](06-performance-budgets.md): mesher, collider build, damage resolve, spatial hash, debris, streaming.
- **F5 Cross-module flows.** Three or more systems interact with ordering constraints: scene transitions with streaming, wiring the frame pipeline, saving and loading the whole world.
- **F6 Design or spike work.** The deliverable is a decision, a format, a vocabulary or a measurement, not code against an exact spec.

Everything else is `standard`. Typical `standard` tasks:
- transcribing contract types that the spec gives as exact code;
- definition classes listed field by field;
- converters with an explicit field map;
- UI views bound to an existing view model;
- harness commands;
- tests for behaviour that is already specified;
- VFX/audio hookups through existing events;
- content authoring.

Contract tasks (`-001`) are `standard` when the spec's §4 gives the exact code, and `frontier` when it leaves type design to the implementer.

### 7.2 Rules

1. **A standard-class model must not claim a `frontier` task**, even if it is the only ready task. Pick another task or stop.
2. **Owner format** in the ledger is `name (model)`, for example `alex (claude-opus-5-5)` or `frank (human)`. `tools/check-docs.py` rejects a `frontier` task owned by a non-frontier model, and a `frontier` task marked `done` without `reviewed-by:`.
3. **Escalate, don't push through.** If a `standard` task turns out to need a frontier concern, stop: set it `blocked` with *Notes* `needs-frontier: <reason>` and add an Open questions row. Frontier concerns here include:
   - changing a Burst job's parallel writes;
   - changing a contract type;
   - changing a binary format;
   - changing system ordering;
   - meeting a perf budget the spec's algorithm doesn't reach.
4. **What a frontier review checks:**
   - conformance to the spec's Decisions and Data model;
   - job safety, with tests run with Burst safety checks and leak detection on (`NativeLeakDetection.Mode = EnabledWithStackTrace`);
   - determinism;
   - every row of the spec's edge-case table;
   - the perf gate measured and logged in *Measurements*;
   - no hidden allocation in hot paths.
5. **Who changes a tier.** Only the owner or tech lead changes a task's tier, by editing the spec and giving the reason.
