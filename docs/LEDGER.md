# Progress ledger

**Living document.** The single place that says what is done, in progress, blocked and unanswered. Every worker updates it when they claim a task, finish a task, discover a question or measure a gate (see [04-workflow.md](04-workflow.md)).

- **Task rows** are created by `tools/ledger-sync.py` from the specs. Never add or delete rows by hand; edit only **Status, Owner, Updated, Commit, Notes**. *Ready* = every dependency is `done` (recomputed by the sync).
- **Statuses:** `todo`, `in-progress`, `blocked`, `review`, `done`, `dropped` (meanings in 04-workflow §4).
- To find work: current milestone in [ROADMAP.md](ROADMAP.md), `Status = todo`, `Ready = yes`, and a **Tier** you may take: `frontier` is reserved for frontier-class models such as Claude Opus or GPT Astra; `standard` is open to anyone. See [04-workflow §7](04-workflow.md#7-task-tiers-which-model-may-take-which-task).
- **Owner** is `name (model)`, e.g. `alex (claude-opus-5-5)` or `frank (human)`. Any task is `done` at its Definition of Done; systems get an in-depth review per milestone, logged as `system-review: name (model)` ([04-workflow §8](04-workflow.md), ADR-0019).
- `tools/check-docs.py` fails if a spec task has no row, a status is invalid, or a `done` row has no commit hash.

## Summary

<!-- summary:begin -->
| Milestone | todo | in-progress | blocked | review | done | dropped | total | ready to start |
|---|---|---|---|---|---|---|---|---|
| M0 | 0 | 0 | 0 | 0 | 71 | 0 | 71 | 0 |
| M1 | 1 | 0 | 1 | 0 | 107 | 0 | 109 | 0 |
| M2 | 56 | 3 | 0 | 0 | 32 | 0 | 91 | 5 |
| M3 | 168 | 4 | 0 | 0 | 8 | 0 | 180 | 9 |
| M4 | 13 | 1 | 0 | 0 | 0 | 0 | 14 | 2 |
| A0 | 6 | 2 | 0 | 1 | 7 | 0 | 16 | 0 |
| A1 | 30 | 0 | 0 | 0 | 0 | 0 | 30 | 1 |
| A2 | 3 | 0 | 0 | 0 | 0 | 0 | 3 | 0 |
<!-- summary:end -->

## Owner gates

| Gate | Spec | Status | Approved on | Notes |
|---|---|---|---|---|
| Design system / art direction approved | SPEC-80 | approved | 2026-10-11 | NOCTURNE, as [DESIGN-GUIDE.md](design/DESIGN-GUIDE.md). Owner: "yes I approve". M0–M4 and SPEC-81 (A1) are unblocked |
| Yarn Spinner 3 adoption | SPEC-41 | waiting for spike | — | Fallback format defined in SPEC-41 |

## Tasks

<!-- tasks:begin -->
| ID | Title | Spec | Milestone | Tier | Depends on | Ready | Status | Owner | Updated | Commit | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| FND-001 | Public contract types | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | standard | CORE-001, DATA-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 338c29a | GameRootConfig.UiConfig/CameraConfig wait for SPEC-60/62 |
| FND-002 | Bootstrap, GameRoot and explicit World creation | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | frontier | FND-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | ddccd7f | rows 1/3/5/14; OQ 7 |
| FND-003 | PauseService | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | standard | FND-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | f7b5e9b |  |
| FND-004 | SettingsService and SettingsApplier | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | standard | FND-001, PERS-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | a4c31aa | SettingsService + applier; 4 tests |
| FND-005 | Composition root and BootSingletons | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | frontier | FND-002, FND-003, FND-004, SPEC-02, SPEC-04 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 9caac2b | rows 1/2/3/5/6/14; OQ 8 |
| FND-006 | SceneFlow transitions and teardown | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | frontier | FND-005 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 1b67ff9 | teardown 200k: 3.5 ms; OQ 9; trap T2.12 |
| FND-007 | SimClock lifecycle and SimClockAdvanceSystem | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | frontier | FND-006 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 7e06cca | exact 120-tick and 10-min checks |
| FND-014 | Static state audit and Play Mode re-entry | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | standard | FND-006 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | ac9e5ba | 18 statics fixed/annotated; re-entry 13 ms |
| CORE-001 | Public contract types | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | standard | — | yes | done | claude (claude-opus-5-5) | 2026-10-10 | 4c3a121 | Drain-system lifecycle stub: open question 5 |
| CORE-002 | StableHash and content identity | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | b4a06ce | 0.046 µs per 16-char hash, 0 B GC; SPEC-02 vector labels corrected |
| CORE-003 | Random streams and fixed-step time | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-002 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 9a669c6 |  |
| CORE-004 | GameLog | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | standard | CORE-001 | yes | done | claude (claude-opus-5-5) | 2026-10-10 | eccf02b |  |
| CORE-005 | Services locator | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | d75b9ac |  |
| CORE-006 | EventBus and EventHub | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-004 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 3f0a613 | Drain1000 median 0.025 ms (gate 0.3), 0 B GC |
| CORE-007 | Binary save primitives | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-002 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 8d0f489 | Write1MiB median 2.29 ms (gate 3 ms) |
| CORE-008 | System groups and pipeline order | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 8edb9ad | No changes to the groups were needed |
| CORE-009 | ECS→managed event bridge | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-006, CORE-008 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 134dadc | idle 0.0001 ms; 1,000 in 0.11 ms; OQ 12, 13 |
| CORE-010 | Math helpers | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | standard | CORE-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 4998e40 |  |
| CORE-011 | Settings model | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | standard | CORE-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 8f2e125 |  |
| CORE-012 | Simulation event channels | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-001, CORE-004 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 7458403 | 0.9 µs per pair; read set de-nested (OQ 11) |
| CORE-013 | Own all simulation event cleanup jobs | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-012 | yes | done | astra (gpt-6) | 2026-10-11 | 125af7d | Own all cleanup jobs; zero-worker leaks and producer race fixed |
| INP-001 | Public contract types and the actions asset | [SPEC-03](specs/SPEC-03-input.md) | M0 | standard | CORE-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 943e0fd | Edit-mode Dispose trap: SPEC-03 Q4 |
| INP-002 | InputService contexts and display strings | [SPEC-03](specs/SPEC-03-input.md) | M0 | standard | INP-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | bad1ed2 | Also ApplyBindingOverrides/SaveBindingOverridesJson; SPEC-03 Q4, Q5 resolved |
| INP-003 | Rebinding and overrides | [SPEC-03](specs/SPEC-03-input.md) | M0 | standard | INP-002 | yes | done | astra (gpt-6) | 2026-10-11 | 7f46337 | Interactive rebind, conflict restore, Escape suppression, JSON and reset |
| DATA-001 | Public contract types | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | CORE-001 | yes | done | claude (claude-opus-5-5) | 2026-10-10 | 12a585b | `ContentDefinition.Id` cache already implemented (DATA-004 overlap) |
| DATA-002 | ValidationReport implementation | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | DATA-001 | yes | done | claude (claude-opus-5-5) | 2026-10-10 | fcaa9d3 |  |
| DATA-003 | Orientation tables | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | frontier | DATA-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 4339108 | Generation 0.083 ms (gate 1 ms) |
| DATA-004 | Definition self-validation and ID rules | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | DATA-002 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 8c7d0be | E048/E049 also implemented here |
| DATA-005 | ContentCatalog | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | frontier | DATA-004 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 37f513a |  |
| DATA-006 | Block blob builder and placement maths | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | frontier | DATA-003, DATA-005 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 8eb3aca |  |
| DATA-007 | ContentService and ECS singletons | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | frontier | DATA-006 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | fc1d170 | Synthetic2000 8.2 ms (gate 100), 150 defs 0.5 ms (gate 20); shipped-content run waits for DATA-009 |
| DATA-008 | Editor database builder, postprocessor, validator runner and CLI | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | DATA-005 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 4e68557 |  |
| DATA-009 | System placeholder content and the database asset | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | DATA-008 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | d710afe |  |
| PERS-001 | Public contract types | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | standard | CORE-001, DATA-001, VOX-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 75f6583 | Result-struct constructors: SPEC-05 Q16 |
| PERS-002 | Save paths, profiles, slots and meta files | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | standard | PERS-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 20bb0aa | META-chunk fallback completes with PERS-003; SPEC-05 Q17 |
| PERS-003 | Binary container, CRC32 and compression | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 5b594d6 | fuzz 1,000 flips + 200 truncations clean |
| PERS-004 | ID palette and SaveWriter/SaveReader integration | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-003, SPEC-02 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | e569704 | no §4.10 fallback needed |
| PERS-005 | Save pipeline: capture, worker, atomic write, thumbnails, queue | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-002, PERS-004, SPEC-04 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | d063181 | 11 tests; OQ SPEC-02 14, SPEC-05 18 |
| PERS-006 | Load pipeline and restore orchestration | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-005 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | d9532a4 | 14 tests, all §7 load rows; OQ 19 |
| PERS-007 | VOXS codec | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-001, SPEC-04 | yes | done | astra (gpt-6) | 2026-10-11 | f553a03 | VOXS v1 + Burst runs; 200 round trips, 1000 flips; Q30 |
| PERS-008 | Deferred VOXS snapshots in saves | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-007, PERS-005 | yes | done | astra (gpt-6) | 2026-10-11 | ec9a6ae | Persistent snapshots; worker round trip and all-path disposal; Q31 |
| PERS-009 | Autosave scheduler and save on quit | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | standard | PERS-006 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 5b1e06b | 10 tests; 0 B/frame |
| PERS-010 | Fixture harness, container fixtures and the 04-B1 EditMode regression | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | standard | PERS-006 | yes | done | astra (gpt-6) | 2026-10-11 | 6a50bb4 | Immutable v1 fixtures; 04-B1 real-content regression |
| PERS-011 | Round-trip byte equality (framework level) | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | standard | PERS-006, PERS-008 | yes | done | astra (gpt-6) | 2026-10-11 | ca73bba | Fresh binary/JSON/VOXS round trips; unknown chunk byte equality; Q32 |
| PERS-012 | Save performance and size gates | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-008 | yes | done | astra (gpt-6) | 2026-10-11 | 43b3c6f | All M0 save gates pass; 1M ship 15,230 B; capture 0.12 ms |
| PERS-016 | Release snapshots when session capture fails | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-008 | yes | done | astra (gpt-6) | 2026-10-11 | 9040fdd | Session callback failure cleanup and recovery; native leak checks |
| PERS-017 | Make save commit outcomes and slot metadata consistent | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-005, PERS-006 | yes | done | astra (gpt-6) | 2026-10-11 | 527aa5f | Post-commit sidecar failures warn; embedded metadata wins |
| PERS-018 | Handle null JSON metadata as unreadable | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | standard | PERS-002, PERS-006 | yes | done | astra (gpt-6) | 2026-10-11 | e5f9756 | Null/empty profile skip and nonfatal META warning |
| VOX-001 | Public contract types | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M0 | standard | CORE-001, DATA-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | c37ad48 | BrickRecord 28 B (ledger Q9); VOXS adapter members wait for VOX-009 (Q10) |
| AUD-001 | Public contract types | [SPEC-64](specs/SPEC-64-audio.md) | M0 | standard | CORE-001, DATA-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | f793298 | Validation codes: SPEC-64 Q12 |
| AUD-002 | Mixer asset, voice settings and import rules | [SPEC-64](specs/SPEC-64-audio.md) | M0 | standard | AUD-001 | yes | done | astra (gpt-6) | 2026-10-11 | d1d654c | Mixer 5 volumes; 64/512 voices; imports; SFX 8.79MiB |
| AUD-003 | AudioService core: root, single listener, voice pools, volumes, pause | [SPEC-64](specs/SPEC-64-audio.md) | M0 | frontier | AUD-002 | yes | done | astra (gpt-6) | 2026-10-11 | fdd1a3a | Single listener; 60 pooled voices; volumes/pause/follow/disposal |
| DEV-001 | Public contract types | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | standard | CORE-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | ecc6d9b |  |
| DEV-002 | Verify and document the as-built generic harness | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | standard | — | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 974ae81 |  |
| DEV-003 | DevRuntimeHost, command registry, operations, help/op/until | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | frontier | DEV-001, FND-001 | yes | done | astra (gpt-6) | 2026-10-11 | beab746 | Registry, operations, logs/events and CLI; idle 0 GC |
| DEV-004 | Runtime input injection and UI query | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | standard | DEV-003 | yes | done | astra (gpt-6) | 2026-10-11 | 47e764b | Timed state events; AgentMouse cleanup; real-panel UI query; VFX-015 follow-up: owned keyboard fallback when batch Editor disables native keyboard; existing input and range tests cover it |
| DEV-005 | Scenario language: parser, expressions and built-in probes | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | frontier | DEV-003 | yes | done | astra (gpt-6) | 2026-10-11 | 2f1e10c | Scenario parser, typed expressions and M0 probes; R28 parse verified |
| DEV-006 | Scenario runner, results JSON and CLI/scripts | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | frontier | DEV-004, DEV-005 | yes | done | astra (gpt-6) | 2026-10-11 | 74fe510 | Scenario runner, JSON, CLI/scripts; exits 0/1/2 verified |
| DEV-020 | Release-strip guarantee | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | standard | DEV-003 | yes | done | astra (gpt-6) | 2026-10-11 | b4e7461 | Real release compilation; namespace/assembly and build-content checks |
| TST-001 | Test kit core | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M0 | frontier | CORE-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | c082a4c | kit + perf hook; 8 gates on PerfRun; OQ 4–6 |
| TST-008 | Iteration gates | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M0 | standard | TST-001 | yes | done | astra (gpt-6) | 2026-10-11 | c6057e6 | Live iteration gates pass; both reloads disabled; reliable CLI test results |
| TST-009 | Serialize Unity tool operations and validate live-test identity | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M0 | frontier | TST-008 | yes | done | astra (gpt-6) | 2026-10-11 | 25598ab | Project lock, exact test inventory; overlap refusal verified |
| MIG-001 | Public contract types | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | standard | DATA-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | c327a64 |  |
| MIG-002 | Legacy JSON index and strict reading | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | standard | MIG-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 6c78f67 |  |
| MIG-003 | Legacy ID map | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | standard | MIG-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | 4955b51 |  |
| MIG-004 | Asset writer, manifest, runner and report | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | frontier | MIG-002, MIG-003, SPEC-04 | yes | done | astra (gpt-6) | 2026-10-11 | ab6e4b3 | Protected writes, manifest, runner and reports; 9 tests |
| MIG-005 | Item converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | standard | MIG-004 | yes | done | astra (gpt-6) | 2026-10-11 | 4b524c7 | 35 items, icon references, dropped-field report; repeat imports byte-stable |
| MIG-006 | Block converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | standard | MIG-005 | yes | done | astra (gpt-6) | 2026-10-11 | 3bb208c | 21 blocks; exact geometry/modules; byte-stable reruns |
| MIG-007 | Legacy Euler to orientation | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | frontier | MIG-001, SPEC-04 | yes | done | astra (gpt-6) | 2026-10-11 | b264f44 | Integer RyRxRz, one-degree snapping, all 64 Unity Euler combinations |
| MIG-008 | Ship blueprint converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | frontier | MIG-006, MIG-007, SPEC-05, VOX-001 | yes | done | astra (gpt-6) | 2026-10-11 | 270db7f | 13 ships; exact counts; VOXS verified; Q39/40 |
| MIG-016 | Migration gate test | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | standard | MIG-008 | yes | done | astra (gpt-6) | 2026-10-11 | 75cb5fd | Migration gate: 69 unchanged; full 0.580s; max ship 42.4ms |
| FND-008 | PersistentId allocation and refill | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M1 | frontier | FND-007 | yes | done | astra (gpt-6) | 2026-10-11 | 2718913 | Unique managed/Burst grants; saved high-water mark; explicit refill dependency wait |
| FND-009 | GameSession: new game, end, IGameFlow, ISaveContext, SESS | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M1 | frontier | FND-006, FND-008, PERS-001 | yes | done | astra (gpt-6) | 2026-10-11 | 8a8bd6f | Session flow/SESS; ordered save preparation; bounded thumbnail readback (ADR-0034) |
| FND-010 | Fleet model, FLET and the player ship | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M1 | frontier | FND-009, VOX-001, PERS-001 | yes | done | astra (gpt-6) | 2026-10-11 | 745e10a | Fleet/FLET ownership, player spawn/writeback, report warnings and recovered-id reservation |
| FND-016 | Sandbox entry | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M1 | standard | FND-010 | yes | done | astra (gpt-6) | 2026-10-11 | 4efcb47 | Serialized creative sandbox, optional player; normal campaign resets flags |
| FND-017 | Imported ship flight checkpoint | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M1 | frontier | FND-010, REN-001, INP-001, DEV-001 | yes | done | astra (gpt-6) | 2026-10-12 | 0e253e1 | Imported Fighter renders, flies, collides and saves/loads; real screenshots; ADR-0044/0045 |
| FND-018 | Nocturne flight environment and movement reference | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M1 | frontier | FND-017 | yes | done | astra (gpt-6) | 2026-10-12 | c55d61f | Animated procedural sky; physical planet/atmosphere; flight-reference particles |
| FND-019 | Preserve Nocturne sky across direct new-game restarts | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M1 | frontier | FND-018, FND-017 | yes | done | astra (gpt-6) | 2026-10-12 | 8f80083 | Deferred sky binding; three live direct restarts retain approved appearance |
| INP-004 | Context integration with SPEC-60 | [SPEC-03](specs/SPEC-03-input.md) | M1 | standard | INP-002, UI-001 | yes | done | astra (gpt-6) | 2026-10-12 | 5fcd282 | Real bridge/nav/sampler: held W neutral on pause, resumes without phantom edges |
| INP-005 | PlayerInputSampleSystem | [SPEC-03](specs/SPEC-03-input.md) | M1 | frontier | INP-002, FLT-001, CAM-001, PHY-001 | yes | done | astra (gpt-6) | 2026-10-12 | 66702c3 | Real input mapping, latched edges, collider/plane aim; ten required tests pass |
| INP-006 | Camera and editor samplers | [SPEC-03](specs/SPEC-03-input.md) | M1 | standard | INP-002, CAM-001 | yes | done | astra (gpt-6) | 2026-10-12 | a90d5ec | Editor camera + 34 action mappings; four required tests pass; ADR-0048 |
| INP-007 | Frame-rate independence test | [SPEC-03](specs/SPEC-03-input.md) | M1 | standard | INP-005, SPEC-20 | yes | done | astra (gpt-6) | 2026-10-12 | 4942ca7 | 30/60/144 FPS: identical 4/3 edges and 60 thrust ticks |
| DATA-011 | v2 block additions (reactor, cargo) | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M1 | standard | DATA-009 | yes | done | astra (gpt-6) | 2026-10-11 | 7158bf8 | Reactor/cargo registered; specified modules and existing art references validated |
| DATA-012 | Approved slope kit per-cell data and content | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M1 | frontier | DATA-006, DATA-009 | yes | done | astra (gpt-6) | 2026-10-11 | 88b13a0 | Canonical per-cell shapes, approved-kit validation and two definitions; ADR-0028/Q90 |
| PERS-019 | Release saves awaiting thumbnail frames during teardown | [SPEC-05](specs/SPEC-05-persistence.md) | M1 | frontier | PERS-005, PERS-008, PERS-009 | yes | done | astra (gpt-6) | 2026-10-12 | f5ea272 | Waiting snapshots released on teardown; 6 quit tests pass; tracked live shutdown 4 native leaks → 0 (Q178) |
| VOX-002 | SPIKE: memory model for the reference and thin-shell stations | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-001 | yes | done | astra (gpt-6) | 2026-10-11 | 6393ecd | No-go: reference 38,828,288 B retained-copy peak; ADR-0023 proposed |
| VOX-003 | Sparse-brick fallback (only if VOX-002 is no-go) | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-002, VOX-014 | yes | done | astra (gpt-6) | 2026-10-11 | 3d14ffa | Sparse fallback: 31.540 MB reference peak; ADR-0023/Q85; all gates pass |
| VOX-004 | Brick maths and mask operations | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-001 | yes | done | astra (gpt-6) | 2026-10-11 | 508c75f | Brick coordinates, faces and 4096-bit mask operations; 8 tests |
| VOX-005 | VoxelStructure storage core | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-004 | yes | done | astra (gpt-6) | 2026-10-11 | 3e2c25e | Dense/uniform storage, recycled pages, revisions, bounds and ownership; 7 tests |
| VOX-006 | Block placement, removal and compaction | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-005, SPEC-04 | yes | done | astra (gpt-6) | 2026-10-11 | 6a33cf7 | Placement/removal, palette/instance reuse, compaction; GetBlocks brought forward (Q65) |
| VOX-007 | Clone, diff, block enumeration and mass | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-006 | yes | done | astra (gpt-6) | 2026-10-11 | 37cb539 | Deep clone; canonical Burst diff; double mass sums/cache; 6 tests |
| VOX-008 | Grid-space DDA raycast | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-005 | yes | done | astra (gpt-6) | 2026-10-11 | 139396c | Deterministic DDA, empty-brick query skipping; 10k reference rays and ECS parity |
| VOX-009 | VOXS codec | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-007, VOX-017, SPEC-05 | yes | done | astra (gpt-6) | 2026-10-11 | dd88202 | VOXS adapter/hash; exact round trips; tombstone/worker fixes Q68–70; 8 tests |
| VOX-010 | Voxel definitions and config factory | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | standard | VOX-001 | yes | done | astra (gpt-6) | 2026-10-11 | edb5c0c | Blueprint setter, default config, boot singleton; 2 tests, Q64 |
| VOX-011 | Structure registry and BlueprintLibrary | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-009, VOX-010 | yes | done | astra (gpt-6) | 2026-10-11 | b630046 | Borrowed non-reused handles; immutable lazy cache, bounded eviction, boot ownership; 6 required tests |
| VOX-012 | ECS cell write and read helpers | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-004 | yes | done | astra (gpt-6) | 2026-10-11 | 0b52cc8 | Burst cell reads/writes, masks, counts, dirty flags and borders; Q63 |
| VOX-013 | GridSpawnSystem: spawn path and singletons | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-006, VOX-011, VOX-012, REN-001, VOX-021, PHY-001, DMG-001, TST-001 | yes | done | astra (gpt-6) | 2026-10-11 | b6d6708 | Budgeted Burst fill, profiles/stow, read-token teardown; 522/32, perf28; 1024 bricks 0.308 ms |
| VOX-014 | Rebuild, despawn and write-back | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-013 | yes | done | astra (gpt-6) | 2026-10-11 | d91def5 | Incremental saves/capture, rebuild identity, partial/stowed despawn; 528/32, perf29 |
| VOX-015 | BrickDirtyScheduleSystem | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-012, REN-001, VOX-021, PHY-001 | yes | done | astra (gpt-6) | 2026-10-11 | a7de460 | Serial Burst drain; 1/17-worker order identical; zero GC; Q86–87 |
| VOX-016 | DeferredDisposalSystem | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-013 | yes | done | astra (gpt-6) | 2026-10-11 | b0141a5 | Frame/tick-safe cleanup; paused100, no leaks, actual GPU unregister verified |
| VOX-017 | Benchmark structures | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | standard | VOX-005, VOX-006 | yes | done | astra (gpt-6) | 2026-10-11 | a80b2f7 | Five deterministic shapes; reference build 87.43 ms; count/dependency fixes Q66–67 |
| VOX-019 | Damage persistence scenario | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | standard | VOX-014 | yes | done | astra (gpt-6) | 2026-10-11 | 30d39f7 | 04-B3 + explicit save boundary; 50 fixed-step changes, matching hashes, zero leaks |
| VOX-021 | Mesh rebuild priority queue | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | REN-001 | yes | done | astra (gpt-6) | 2026-10-11 | bb07e09 | Guarded priority/FIFO mesh queue; stale promotion invalidation; 4 tests; Q75–76 |
| REN-001 | Public contract types | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | standard | VOX-001, DATA-001 | yes | done | astra (gpt-6) | 2026-10-11 | 42dfd86 | Rendering contracts; queue/batch ownership; ADR-0018 layout; Q51–53 |
| REN-002 | SPIKE: Entities Graphics with thousands of runtime brick meshes | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-001, SPEC-10 | yes | done | astra (gpt-6) | 2026-10-11 | 66a5baf | No-go: movement delta 1.625 ms, GPU 6.435 ms; REN-003 activated, ADR-0032 proposed |
| REN-003 | Fallback renderer (only if REN-002 is no-go) | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-002, REN-008 | yes | done | astra (gpt-6) | 2026-10-12 | de1b11d | Pooled geometry/pixels verified; performance deferred (ADR-0042), initial passes exhausted |
| REN-004 | Normals, packing and shape tables | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-001, SPEC-04 | yes | done | astra (gpt-6) | 2026-10-11 | 6b076af | Five shapes × 24 rotations; exact half-cell packing; 42 normals; Q88–89 |
| REN-005 | Snapshot gathering | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-004, SPEC-10, SPEC-04 | yes | done | astra (gpt-6) | 2026-10-11 | 93cd973 | 27-neighbour snapshots, per-cell slopes, bounded props and retained flags; ADR-0029/Q91 |
| REN-006 | LOD0 mesher | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-004 | yes | done | astra (gpt-6) | 2026-10-11 | 1de74e0 | LOD0 culling/AO/welding; 36.3 µs median; large slope indices safe; ADR-0030/Q92 |
| REN-007 | LOD1 mesher | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-006 | yes | done | astra (gpt-6) | 2026-10-11 | 5cbebeb | LOD1 exact coverage; triangle ratios 0.406% / 0.557% / 6.195% |
| REN-008 | Brick and prop shaders, placeholder art | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-004 | yes | done | astra (gpt-6) | 2026-10-11 | 4c504f4 | Canonical shaders/ADR-0040; GPU 4/4, 112 Metal/D3D11 variants; procedural assets |
| REN-009 | Render config definitions and factory | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | standard | REN-001, REN-008 | yes | done | astra (gpt-6) | 2026-10-12 | 6407fb5 | Validated materials/palette factory, explicit boot lifetime; Q138 |
| REN-010 | BrickMeshBuildSystem | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-005, REN-006, REN-007 | yes | done | astra (gpt-6) | 2026-10-12 | be4b370 | Snapshot-only Burst batches verified; performance deferred: 0.864 ms vs 0.5 ms, zero tuning passes |
| REN-011 | BrickMeshApplySystem, cleanup and statistics | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-010, SPEC-10 | yes | done | astra (gpt-6) | 2026-10-12 | 18a0e45 | Pooled apply/cleanup, shared registration, camera transforms; six GPU checks pass |
| REN-012 | GridLodSystem, Dormant and LOD switching | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-011 | yes | done | astra (gpt-6) | 2026-10-12 | 6b2f87e | Hysteresis, conditional versions, async apply recovery; live Fighter LOD1 → LOD0 |
| REN-013 | Rendering gate suite | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-012, REN-008 | yes | done | astra (gpt-6) | 2026-10-12 | 1461748 | Three GPU tests pass; real pipeline, MAE 0; moving delta 0.029626 ms, idle 0.117376 ms; no tuning |
| REN-014 | Prop block instancing | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-011, REN-008 | yes | done | astra (gpt-6) | 2026-10-12 | 37097fa | Applied prop cells, moving bounds, four GPU tests; 10k props 0.01465 ms |
| PHY-001 | Public contract types | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | standard | CORE-001, DATA-001, VOX-001 | yes | done | astra (gpt-6) | 2026-10-11 | 1f27ec6 | Physics contracts, exact filters, native/blob ownership; 4 required tests |
| PHY-002 | SPIKE: Unity Physics capabilities for nested voxel compounds | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-001 | yes | done | astra (gpt-6) | 2026-10-11 | dd85d8c | Nested GO; all ten checks recorded |
| PHY-003 | Physics config definition, factory and PhysicsStep | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | standard | PHY-001, SPEC-04 | yes | done | astra (gpt-6) | 2026-10-11 | e14cc32 | Default asset / E001-E002 / factories / both boot rows integrated |
| PHY-004 | Brick box merger | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-001 | yes | done | astra (gpt-6) | 2026-10-11 | e1679aa | Exact greedy box covers, 2/4-cell coarsening, bounds fallback; 7 tests |
| PHY-005 | Brick blob builder and grid compound assembler | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-002, PHY-004 | yes | done | astra (gpt-6) | 2026-10-11 | a54c29f | Eight tests; all merge levels / copied ownership / nested coordinates |
| PHY-006 | Mass maths | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-001 | yes | done | astra (gpt-6) | 2026-10-11 | 1c4b85f | Six tests; tensor / canonical Jacobi / planar locks / point velocity preservation |
| PHY-007 | Pipeline singletons and access discipline | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-001, PHY-005 | yes | done | astra (gpt-6) | 2026-10-11 | a936342 | Five tests; pools / ordered Burst finalize / ownership and counter audits; Q59–61 |
| PHY-008 | GridBodyInitSystem | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-003, PHY-006, PHY-007, SPEC-10 | yes | done | astra (gpt-6) | 2026-10-11 | 6b004e7 | Async root gather; factory/adoption/rebuild; Q101–102 |
| PHY-009 | ColliderSnapshotSystem and install schedule | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-007, SPEC-10 | yes | done | astra (gpt-6) | 2026-10-11 | db7c5b7 | Tick snapshots, deterministic buckets, six required tests pass |
| PHY-010 | BrickColliderBuildSystem | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-005, PHY-009 | yes | done | astra (gpt-6) | 2026-10-11 | 08211f4 | Snapshot-only budgeted batches; 1000 bricks / 4 batches, byte-identical compound |
| PHY-011 | ColliderSwapSystem | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-010 | yes | done | astra (gpt-6) | 2026-10-11 | 4542bf7 | Due-prefix installs; asynchronous finalize; Q98 empty-prefix ownership |
| PHY-012 | GridColliderCleanupSystem and teardown | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-011, SPEC-10 | yes | done | astra (gpt-6) | 2026-10-11 | 43a56cc | Deferred ownership; scene/world teardown; Q99–100 |
| PHY-013 | GridMassUpdateSystem | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-006, PHY-008, DMG-001 | yes | done | astra (gpt-6) | 2026-10-11 | 0c9720a | Deterministic recompute; incremental removal; point-velocity preservation |
| PHY-015 | Debug, statistics and harness hooks | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | standard | PHY-011 | yes | done | astra (gpt-6) | 2026-10-11 | a08fe17 | Guarded stats and cumulative reset; recursive grid-local box bounds |
| PHY-016 | GridBodyFactory contract test for split creators | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | standard | PHY-008 | yes | done | astra (gpt-6) | 2026-10-11 | 4db5812 | ECB-created hulk moves next tick; exact tick-28 install, one retirement, no native leaks |
| PHY-017 | Physics behaviour scenarios | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | standard | PHY-011, PHY-013, SPEC-10 | yes | done | astra (gpt-6) | 2026-10-11 | 374bfd4 | Five real-physics scenarios; ram momentum error 0; damage-hole exact install tick |
| PHY-018 | Collider and mass determinism across frame rates | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-011, PHY-013 | yes | done | astra (gpt-6) | 2026-10-11 | 8c26c92 | Four frame patterns × 1/17 workers; per-tick bytes/mass/velocity identical; zero late installs |
| PHY-019 | Performance gates and memory | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-012, PHY-013, PHY-015, SPEC-10, TST-001 | yes | done | astra (gpt-6) | 2026-10-11 | fcd09c2 | Six reference gates; zero late installs; ADR-0033 corrects collider overlap allocation |
| PHY-020 | Fallback: flat grid compound of boxes (only if PHY-002 is no-go for nested compounds) | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-002 | yes | done | astra (gpt-6) | 2026-10-11 | dd85d8c | not needed (PHY-002 go) |
| PHY-023 | Urgent collider deadlines across catch-up frames | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-018, PHY-019 | yes | done | astra (gpt-6) | 2026-10-11 | a912ab2 | ADR-0035 urgent deadline completion outside fixed step; strict determinism and full gates pass |
| PHY-024 | Spawn bodies while scene loading is paused | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-008, PHY-016, PHY-018 | yes | done | astra (gpt-6) | 2026-10-11 | e201b39 | Fresh bodies through end-init ECB at zero ticks; split adoption unchanged |
| DMG-001 | Public contract types | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M1 | standard | CORE-001, DATA-001, VOX-001 | yes | done | astra (gpt-6) | 2026-10-11 | 9310e7c | Public damage/restore/impulse contracts, 43 config defaults, table lookup; 4 tests; Q71–74 |
| FLT-001 | Public contract types | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | CORE-001, DATA-001, VOX-001 | yes | done | astra (gpt-6) | 2026-10-11 | 0ddf5bf | Unmanaged flight/input contracts; exact SPEC-03 fields; Q112 comment corrections |
| FLT-002 | Grid-space and flight maths | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-001, SPEC-04 | yes | done | astra (gpt-6) | 2026-10-11 | ec30fc9 | Grid geometry, gyro, caps and assist targets; eight required tests |
| FLT-003 | Flight config definition and factory | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-001, SPEC-04 | yes | done | astra (gpt-6) | 2026-10-11 | f21a981 | Exact defaults, FLT-E001 diagnostics, catalog asset and boot singleton |
| FLT-004 | Thrust allocator | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-001 | yes | done | astra (gpt-6) | 2026-10-11 | 3c34a39 | Fixed-order PGS, projected warm starts, bit-identical managed/Burst results; Q119 |
| FLT-005 | PlayerCommandApplySystem | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-001, DMG-001 | yes | done | astra (gpt-6) | 2026-10-11 | 9271f44 | Fixed-tick command bridge; nine latched edges; Cruise preserved; six required tests |
| FLT-006 | Thruster registration and ThrusterInitSystem | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-002, SPEC-10 | yes | done | astra (gpt-6) | 2026-10-11 | 2388784 | Four flying profiles, player-only tag, change-filtered Burst nozzle init; three tests |
| FLT-007 | PropulsionSystem | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-002, FLT-004, FLT-006, DMG-001, SPEC-12 | yes | done | astra (gpt-6) | 2026-10-11 | 52335f4 | Revision cache, allocator, impulses and hulk idling; eight required tests |
| FLT-008 | PlanarConstraintSystem | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-001, SPEC-12 | yes | done | astra (gpt-6) | 2026-10-11 | 0be9bf9 | Vertical impulse constraint; real integration; three required tests |
| FLT-009 | Flight behaviour scenarios | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-005, FLT-007, FLT-008, SPEC-10 | yes | done | astra (gpt-6) | 2026-10-11 | 4b4054d | Five physical scenarios; torque, ram, lost thruster, turn and frame-independent edges |
| FLT-010 | Flight tuning against legacy blueprints | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-009, SPEC-72 | yes | done | astra (gpt-6) | 2026-10-11 | c9526c2 | 13 blueprints measured; provisional yaw 180°/s² / 2.5 MN·m; all t90 ≤1.2 s; acceleration shortfall Q124, owner sign-off pending Q125 |
| FLT-011 | Flight performance and determinism | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-007, FLT-008, TST-001 | yes | done | astra (gpt-6) | 2026-10-11 | 0278edb | Six flight cost/GC gates pass; 600 ticks × 3 ships identical across 8 runs |
| WPN-001 | Public contract types | [SPEC-21](specs/SPEC-21-weapons.md) | M1 | standard | CORE-001, DATA-001, VOX-001, DMG-001, FLT-001 | yes | done | astra (gpt-6) | 2026-10-12 | c250d87 | 64 B projectiles; factions; unmanaged contracts; Q156 staging; performance deferred |
| AI-001 | Public contract types | [SPEC-23](specs/SPEC-23-ai.md) | M1 | standard | CORE-001, DATA-001, VOX-001, WPN-001 | yes | done | astra (gpt-6) | 2026-10-12 | d5f0523 | Spatial/AI/fleet contracts; 64 B proxy; Q157 staging; performance deferred |
| UI-001 | Public contract types | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | standard | CORE-001, INP-001 | yes | done | astra (gpt-6) | 2026-10-12 | 01dcff0 | Exact data/view contracts, 123 style names; service/control shells per spec |
| UI-002 | Theme skeleton, panel settings and root UXML | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | standard | UI-001 | yes | done | astra (gpt-6) | 2026-10-12 | a494f84 | Panel/theme/config and six non-picking layers; three asset tests pass; Q147 |
| UI-003 | UIRoot construction, layers and sound hooks | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | frontier | UI-002, CORE-001 | yes | done | astra (gpt-6) | 2026-10-12 | fdade56 | Root/document boot lifecycle, prewarm, sound hooks; live flight preserved; Q148–150 |
| UI-004 | UINavigator stack, pause token and input context | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | frontier | UI-003 | yes | done | astra (gpt-6) | 2026-10-12 | 23b3cf5 | Navigator owns context/pause; live Resume/Escape restore Fighter controls; ADR-0047 |
| UI-005 | Text entry context and world-pointer gate | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | frontier | UI-004 | yes | done | astra (gpt-6) | 2026-10-12 | 4cdc9a9 | Text focus/Back and live flight pointer gating; viewport fix Q153; 7 required tests pass |
| UI-006 | MVVM base, converters and number strings | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | frontier | UI-001 | yes | done | astra (gpt-6) | 2026-10-12 | 25da402 | Binding/converters; 6 EditMode + 3 PlayMode tests |
| UI-007 | Base controls: buttons, panel, list, inputs, key hint, markers, HUD plates | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | standard | UI-002, UI-006 | yes | done | astra (gpt-6) | 2026-10-12 | ce4b615 | Nine component sheets; gallery and KeyHint verified; contrast audit UI-012 |
| CAM-001 | Public contract types | [SPEC-62](specs/SPEC-62-camera.md) | M1 | standard | CORE-001 | yes | done | astra (gpt-6) | 2026-10-11 | f078fc9 | Exact contracts, unmanaged view, two required tests; deferred Fx members Q126 |
| CAM-002 | ADR: custom camera rig instead of Cinemachine | [SPEC-62](specs/SPEC-62-camera.md) | M1 | frontier | — | yes | done | astra (gpt-6) | 2026-10-11 | a0df398 | ADR-0038 Proposed, all five reasons; no package or runtime change |
| CAM-003 | Camera math helpers | [SPEC-62](specs/SPEC-62-camera.md) | M1 | standard | CAM-001 | yes | done | astra (gpt-6) | 2026-10-11 | 0e4a19d | Five specified tests; 30/60/144 FPS damping agrees within 1e-4 |
| CAM-004 | Rig service, camera prefab, Menu/Hold modes and the sync system shell | [SPEC-62](specs/SPEC-62-camera.md) | M1 | frontier | CAM-003, FND-001 | yes | done | astra (gpt-6) | 2026-10-11 | ece7b8b | Persistent rig, menu/hold, ECS view and boot bindings; ADR-0039 single listener; ten required tests |
| CAM-005 | Flight follow mode and camera input | [SPEC-62](specs/SPEC-62-camera.md) | M1 | frontier | CAM-004, FLT-001, VOX-001, INP-001 | yes | done | astra (gpt-6) | 2026-10-11 | f19e2a5 | Flight follow, input, cuts; exact moving-target damping (Q129–130) |
| CAM-011 | Camera performance and framing tests | [SPEC-62](specs/SPEC-62-camera.md) | M1 | standard | CAM-005, TST-001 | yes | done | astra (gpt-6) | 2026-10-11 | 111f9fd | CPU 0.002833 ms; 0 B/update; 10 s circle pitch/GC pass (Q131) |
| CAM-012 | Keep the imported player ship framed during fast close-zoom flight | [SPEC-62](specs/SPEC-62-camera.md) | M1 | frontier | CAM-005, FND-001 | yes | done | astra (gpt-6) | 2026-10-12 | 42287c3 | Bounds-aware focus keeps fast Fighter framed; live 53.39 m/s capture; Q154 |
| VFX-001 | Public contract types | [SPEC-63](specs/SPEC-63-vfx.md) | M1 | frontier | CORE-001, DATA-001 | yes | done | astra (gpt-6) | 2026-10-12 | bb25b99 | Guarded queue, shared kinds, GPU layouts and validation; ADR-0050 |
| AUD-004 | Listener regression and scene validation (R5/R7) | [SPEC-64](specs/SPEC-64-audio.md) | M1 | standard | AUD-003, SPEC-01 | yes | done | astra (gpt-6) | 2026-10-12 | bd35dac | Four scenes listener-free; same listener across real flow; mutation proven |
| AUD-005 | UI sounds, PlayCue and stingers | [SPEC-64](specs/SPEC-64-audio.md) | M1 | standard | AUD-003 | yes | done | astra (gpt-6) | 2026-10-12 | b8521e9 | UI/cue playback and ducking; four tests pass, 0 B over 1000 UI calls |
| AUD-010 | Audio provenance table and release-build check | [SPEC-64](specs/SPEC-64-audio.md) | M1 | standard | AUD-002 | yes | done | astra (gpt-6) | 2026-10-12 | 8879cfa | 19 clips: 2 Blocked, 17 Unverified; release guard and four tests |
| DEV-007 | PlayMode test that runs every active scenario file | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | standard | DEV-006, SPEC-71 | yes | done | astra (gpt-6) | 2026-10-12 | 8f8adc3 | Automatic file cases; live Boot smoke passes, added active case discovered, future-command draft ignored (Q176) |
| DEV-008 | newgame and sandbox commands, newgame.sh | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | standard | DEV-006, FND-001, PERS-001 | yes | done | astra (gpt-6) | 2026-10-12 | 0b4269c | Real launch/probes, isolated profile, live edit-mode launch 1.370 s; Q144–146 |
| DEV-009 | teleport, spawn and grid commands; snapshot game sections | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | standard | DEV-008, VOX-001, PHY-001 | yes | blocked | astra (gpt-6) | 2026-10-12 |  | Needs implemented FND-012 and SPEC-30/31 coordinates; contract-only Ready is premature (Q155) |
| DEV-012 | Overlay renderer and voxel overlays (bricks, dirty, colliders) | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | frontier | DEV-009, REN-001, PHY-001 | no | todo |  |  |  |  |
| DEV-015 | In-game dev console | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | standard | DEV-003, UI-001, INP-001 | yes | done | astra (gpt-6) | 2026-10-12 | 8cf2df1 | F1 console; history/completion, async results, 0 B closed; ADR-0049 |
| DEV-016 | record and replay commands | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | standard | DEV-008, SPEC-71 | yes | done | astra (gpt-6) | 2026-10-12 | f1f6cd2 | Console/CLI replay; real seed-7 Fighter 300 ticks, 0 m error; live 1790-tick recording and screenshot; Q175 |
| DEV-019 | Draft regression scenario files | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | standard | DEV-007 | yes | done | astra (gpt-6) | 2026-10-12 | f891b9d | Six drafts parse; active Boot passes and all six drafts are ignored; activation prerequisites Q177 |
| TST-002 | Record and replay | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M1 | standard | TST-001, FLT-001, INP-001 | yes | done | astra (gpt-6) | 2026-10-12 | 9349e78 | Sparse text recorder/replay; five required tests; even-tick 30/60/144 commands match |
| TST-003 | Game flow fixture and boot test hooks | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M1 | standard | TST-001, SPEC-01, SPEC-05 | yes | done | astra (gpt-6) | 2026-10-12 | 1392d35 | Real Boot/flow fixture; four tests; temp saves/settings and unchanged real profile timestamps |
| TST-004 | Integrated voxel gate suite (moved from SPEC-10 VOX-018) | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M1 | frontier | TST-001, SPEC-10, SPEC-11, SPEC-12 | yes | done | astra (gpt-6) | 2026-10-12 | a56c266 | Full pipeline: 15-frame readiness, idle 0.224481 ms; total memory performance deferred (Q174); zero tuning |
| FND-012 | ShipSpawnService | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M2 | standard | FND-010, WPN-001, FLT-001, AI-001 | yes | todo |  |  |  |  |
| FND-020 | Playable stationary-target shooting and physical breakup | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M2 | frontier | FND-017, FND-018, FLT-001, DMG-001, WPN-001, VFX-001 | yes | done | astra (gpt-6) | 2026-10-12 | 53e61db | Playable station/Space fire; review fixes: bounded integrity, canonical masks, allocation attribution, stronger image checks; default suites green; perf advisory |
| FND-021 | Automatic aligned test station in World | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M2 | standard | FND-020, SPEC-63 | yes | done | astra (gpt-6) | 2026-10-13 | c3bb396 | Automatic World station at Y=-2 m; idempotent range request; direct World Play verified; spec-required: 1 (existing feature test extended), extra: 0 |
| DATA-010 | Weapon and ability links on blocks | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M2 | standard | DATA-007, WPN-001, ABL-001 | yes | in-progress | sol (gpt-6.1-sol) | 2026-10-13 |  | Assigned reusable worker; central Unity validation |
| PHY-014 | SplitGrace contact suppression | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M2 | standard | PHY-002, DMG-001 | yes | done | combat (gpt-6.1-sol) | 2026-10-13 | 094f430 | Pair-specific grace; three required tests. Full EM 861/0/1, PM 121/0/53; compile clean. |
| DMG-002 | DamageStreams and DamageStreamsSystem | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-001, SPEC-10, SPEC-02 | yes | done | astra (gpt-6) | 2026-10-12 | e3377fa | Four channels, owned phase memory, stable explosion drains; 5 tests; Q179–180 |
| DMG-003 | Damage config asset and boot hookup | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | standard | DMG-001 | yes | done | astra (gpt-6) | 2026-10-12 | c1d6af2 | Shipped config/4 damage types/4 armor classes, validation and boot-owned table; 3 tests; Q181 |
| DMG-004 | Cell access, quantization and RemoveCell | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-002 | yes | done | astra (gpt-6) | 2026-10-12 | 8b20d15 | Cell writes, DDA, deterministic quantization and debris kinematics; 10 required tests pass |
| DMG-005 | Point and blast damage | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-004 | yes | done | astra (gpt-6) | 2026-10-12 | 7dbd025 | Point/blast/instance rules complete; 10 required tests; full suites green; Q189 occlusion fixture correction; zero tuning passes |
| DMG-006 | DamageResolveSystem pipeline | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-005 | yes | done | astra (gpt-6) | 2026-10-12 | 0018fd1 | Ordered gather/sort/plan, exclusive parallel grid resolution and canonical publication; seven tests; full suites green; Q190; repairs remain DMG-019 |
| DMG-007 | ImpactEventSystem (rams and knockback) | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-006, PHY-001 | yes | done | astra (gpt-6) | 2026-10-12 | 01d5ff2 | Canonical ram pairs, energy/hardness split, cooldown, next-tick planar knockback; six required tests; ADR-0058 |
| DMG-008 | Brick connectivity summaries | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-004 | yes | done | astra (gpt-6) | 2026-10-12 | 36e838d | Scanline brick flood, instance unions, touched checks and deterministic warm-up; 1000 reference masks; three required tests |
| DMG-009 | IntegritySystem (race on the hybrid graph) | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-006, DMG-008 | yes | done | astra (gpt-6) | 2026-10-12 | 1cf796e | Parallel hybrid race, canonical masks, retained seeds and forced retries; 300 reference structures; live Space cut detached 205 cells |
| DMG-010 | SplitSystem: plan, particle tier, deferred detach and debris FX | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-009, VFX-001, AUD-001 | yes | done | astra (gpt-6) | 2026-10-12 | e3045a8 | Canonical tier/budget planning, next-tick particles, complete deferred reseeding, debris/SFX events; four required tests; existing hulk path retained |
| DMG-011 | Chunk bodies (pool, collider, lifetime) | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-010, SPEC-12 | yes | done | astra (gpt-6) | 2026-10-13 | e2cbd81 | Physical pool, exact mass/momentum, colliders, lifetime/rendering; ADR-0063 clearance; full 901 EM / 128 PM green; live 264-cell slab |
| DMG-012 | Hulk grids from splits | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-011, FND-001, SPEC-12 | yes | in-progress | astra (gpt-6) | 2026-10-13 |  | Complete staged hulk recorder, lifecycle and seven required tests; DMG-011 integrated |
| DMG-013 | ShipDeathSystem, despawn dissolve and the destroy pass | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-012 | no | todo |  |  |  | partial in FND-020: initial core-loss and destroyed-instance cleanup / despawn dissolve, final death presentation and required tests |
| DMG-014 | Determinism hash and frame-rate determinism tests | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-013, TST-001 | no | todo |  |  |  | partial in DMG-006: CellStateHasher / integrated 600-tick frame-rate and worker-count scenarios |
| DMG-015 | Regression scenarios R25/R26 and soak | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | standard | DMG-014 | no | todo |  |  |  |  |
| DMG-016 | Performance gates | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-014 | no | todo |  |  |  |  |
| DMG-017 | Harness commands | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | standard | DMG-014, DEV-001 | no | todo |  |  |  |  |
| FLT-012 | Power and stat integration (M2) | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M2 | frontier | FLT-007, PWR-001 | yes | todo |  |  |  |  |
| FLT-013 | Owner flight-control correction before the firing range | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M2 | frontier | FLT-007, FLT-009, SPEC-03 | yes | done | astra (gpt-6) | 2026-10-12 | 1bc23e8 | Shift strafe, Ctrl boost, gentle coast, bounded assist; live Fighter verified; Q182 |
| WPN-002 | Weapon content: definitions, blob, boot singletons | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-001, SPEC-04 | no | todo |  |  |  | partial in FND-020: plasma definitions, blob and boot singleton / faction relations and full validation |
| WPN-003 | CombatHitTest | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-001, SPEC-10, AI-001, PWR-001 | yes | todo |  |  |  | partial in FND-020: ordered-grid broadphase and current-cell raycast / spatial hash, shields and required tests; partial in VFX-004: world contact normal from cell face / spatial hash, shields, shape-accurate collision and required tests |
| WPN-004 | WeaponInitSystem and mounts | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-002, SPEC-10, SPEC-20 | no | todo |  |  |  | partial in FND-020: installed mount initialization and reparenting / complete spec validation and required tests |
| WPN-005 | PlayerTargetingSystem | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-001, AI-001, SPEC-20 | no | todo |  |  |  |  |
| WPN-006 | WeaponAimSystem and lead | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-004, WPN-005 | no | todo |  |  |  | partial in FND-020: manual aim and arcs / targeting, lead, autofire and required tests |
| WPN-007 | WeaponFireSystem and the projectile store append | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-006, PWR-001 | no | todo |  |  |  | partial in FND-020: serial fire and projectile append / parallel stream flattening, power, beam delivery/pulses and full required tests; partial in VFX-004: guarded muzzle emission with overrides / parallel stream flattening, power, beams and full required tests |
| WPN-008 | ProjectileStepSystem | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-003, WPN-007, PWR-001, SPEC-13 | no | todo |  |  |  | partial in FND-020: movement, hull hits and lifetime; DMG-007 orders stepping after impact / parallel stream/compaction pipeline, homing, shields and full required tests; partial in VFX-004: guarded hull-hit emission with normal, velocity and overrides / parallel pipeline, homing, shields and full required tests |
| WPN-009 | BeamStepSystem | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-008 | no | todo |  |  |  |  |
| WPN-010 | ExplosionSystem | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-008 | no | todo |  |  |  | partial in FND-020: explosion requests into damage / shields, audio and required tests; partial in VFX-004: guarded explosion visual event / shields, audio and required tests |
| WPN-011 | CombatGridSetup and turret visuals | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-004, PWR-001, ABL-001 | no | todo |  |  |  |  |
| WPN-012 | Combat scenarios and determinism | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-009, WPN-010, WPN-011, SPEC-13, SPEC-22 | no | todo |  |  |  |  |
| WPN-013 | Weapon performance gates | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-012, TST-001 | no | todo |  |  |  |  |
| WPN-014 | Weapon balance pass | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-012, SPEC-72 | no | todo |  |  |  |  |
| WPN-015 | Harness hooks | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-008 | no | todo |  |  |  |  |
| WPN-017 | ShipDestroyed payload and bridge | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-001, DMG-001, CORE-001 | yes | done | combat (gpt-6.1-sol) | 2026-10-13 | ec9a6f1 | Canonical destruction event bridge; 4 required tests; full batch 901 EM / 128 PM, zero failures |
| WPN-018 | Weapon power demand and weapon stats | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-004, WPN-007, PWR-001 | no | todo |  |  |  |  |
| PWR-001 | Public contract types | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | CORE-001, DATA-001, VOX-001, DMG-001 | yes | done | power (gpt-6.1-sol) | 2026-10-13 | eeed571 | Canonical declarations; three required tests; Q202 deferred allocator ordering. Full batch green. |
| PWR-002 | Shield maths | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | PWR-001 | yes | done | power (gpt-6.1-sol) | 2026-10-13 | 08c5f01 | Shield intersections, containment and overlap; 5 required tests; full batch 901 EM / 128 PM, zero failures |
| PWR-003 | Tuning definition, baker and spawn helper | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | PWR-001, SPEC-04 | no | todo |  |  |  |  |
| PWR-004 | Registration and `PowerInitSystem` | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-003, WPN-001, VOX-001, SPEC-10 | no | todo |  |  |  |  |
| PWR-005 | `PowerAllocationSystem` | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-004, ABL-001 | no | todo |  |  |  |  |
| PWR-006 | `ShipStatsAggregateSystem` | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-004, SPEC-12 | no | todo |  |  |  |  |
| PWR-007 | `ShieldSystem` | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-005, PWR-006, PWR-002, DMG-001, VFX-001, SPEC-02 | no | todo |  |  |  |  |
| PWR-008 | Power budget validator and balance tests | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | PWR-006, SPEC-72, WPN-001 | no | todo |  |  |  |  |
| PWR-009 | Performance tests | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-005, PWR-006, PWR-007 | no | todo |  |  |  |  |
| PWR-010 | `StatusSystem`: inhibits and stat modifiers (ADR-0016) | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | PWR-001, PWR-005, SPEC-02 | no | todo |  |  |  |  |
| AI-002 | SpatialHash queries | [SPEC-23](specs/SPEC-23-ai.md) | M2 | frontier | AI-001 | yes | todo |  |  |  |  |
| AI-003 | AI content and boot singletons | [SPEC-23](specs/SPEC-23-ai.md) | M2 | standard | AI-001, SPEC-04 | no | todo |  |  |  |  |
| AI-004 | SpatialHashBuildSystem | [SPEC-23](specs/SPEC-23-ai.md) | M2 | frontier | AI-002, SPEC-10, PWR-001, DMG-001 | no | todo |  |  |  |  |
| AI-005 | AI setup and anchor shift | [SPEC-23](specs/SPEC-23-ai.md) | M2 | standard | AI-003, SPEC-02 | no | todo |  |  |  |  |
| AI-006 | AiDecisionSystem | [SPEC-23](specs/SPEC-23-ai.md) | M2 | frontier | AI-004, AI-005, SPEC-20, SPEC-21, ABL-001 | no | todo |  |  |  |  |
| AI-007 | AiSteeringSystem | [SPEC-23](specs/SPEC-23-ai.md) | M2 | standard | AI-006 | no | todo |  |  |  |  |
| AI-008 | PlayerCombatStatusSystem | [SPEC-23](specs/SPEC-23-ai.md) | M2 | standard | AI-006, DMG-001 | no | todo |  |  |  |  |
| AI-009 | Station turret AI | [SPEC-23](specs/SPEC-23-ai.md) | M2 | standard | AI-007 | no | todo |  |  |  |  |
| AI-010 | AI combat scenarios | [SPEC-23](specs/SPEC-23-ai.md) | M2 | standard | AI-007, AI-008, SPEC-21, SPEC-22 | no | todo |  |  |  |  |
| AI-011 | AI determinism | [SPEC-23](specs/SPEC-23-ai.md) | M2 | frontier | AI-010 | no | todo |  |  |  |  |
| AI-012 | AI and hash performance | [SPEC-23](specs/SPEC-23-ai.md) | M2 | frontier | AI-010, TST-001 | no | todo |  |  |  |  |
| ABL-001 | Public contract types | [SPEC-24](specs/SPEC-24-abilities.md) | M2 | standard | CORE-001, DATA-001, PWR-001 | yes | done | power (gpt-6.1-sol) | 2026-10-13 | 1bc7515 | Canonical ability contracts; 48-byte slot; 1 required test; full batch 901 EM / 128 PM, zero failures |
| ABL-002 | Ability content and boot singleton | [SPEC-24](specs/SPEC-24-abilities.md) | M2 | standard | ABL-001, SPEC-04 | no | todo |  |  |  |  |
| ABL-003 | AbilitySystem | [SPEC-24](specs/SPEC-24-abilities.md) | M2 | frontier | ABL-002, SPEC-20, SPEC-22 | no | todo |  |  |  |  |
| ABL-004 | Effect systems | [SPEC-24](specs/SPEC-24-abilities.md) | M2 | standard | ABL-003, SPEC-22 | no | todo |  |  |  |  |
| ABL-005 | Ability scenarios and frame-rate independence | [SPEC-24](specs/SPEC-24-abilities.md) | M2 | standard | ABL-004, SPEC-21 | no | todo |  |  |  |  |
| CAM-008 | Screen shake from FxEvents | [SPEC-62](specs/SPEC-62-camera.md) | M2 | standard | CAM-005, SPEC-63 | yes | done | content (gpt-6.1-sol) | 2026-10-13 | 9d59a62 | Impact shake and borrowed frame reader; Boot binding e265f13; 7 required tests; full batch 901 EM / 128 PM, zero failures |
| VFX-002 | FxRig: slots, effects, buffers and binding | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | frontier | VFX-001, TST-001 | yes | done | astra (gpt-6) | 2026-10-12 | f2b8118 | Session rig, graph validation, pooled/batched/instanced resources and binding; 5 GPU tests pass; final content budget advisory follow-up in VFX-004 |
| VFX-003 | FxUploadSystem: drain, batch, submit, stats | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | frontier | VFX-002 | yes | done | astra (gpt-6) | 2026-10-12 | f24d8c1 | Guarded drain even unbound, bounded batching/submission, lifetime accounting and stats; 12 required tests; visible content and boot binding next in VFX-004 |
| VFX-004 | Template graphs and content assets for hits, explosions, muzzle and shield sparks | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-003 | yes | done | astra (gpt-6) | 2026-10-12 | 7327e4c | Authored hit/muzzle/explosion/shield-spark/ram content, boot binding and real firing hooks; 3 required GPU/content tests; complete GPU/native memory total remains advisory follow-up |
| VFX-005 | Voxel debris particles and the 50k GPU gate | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | frontier | VFX-004 | yes | done | astra (gpt-6) | 2026-10-13 | b575efe | Mesh debris; RGB/velocity readback, 600-frame quality caps, 50k actual GPU particles; GPU timing unavailable/advisory; spec-required: 3, extra: 0 |
| VFX-006 | Projectile tracers | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | frontier | VFX-002, WPN-001, CORE-001 | yes | done | astra (gpt-6) | 2026-10-13 | b575efe | Guarded indexed tracer gather, origin correction, instanced shader; 5k median 0.0385 ms; spec-required: 6, extra: 0 |
| VFX-007 | Beams clipped at the hit point | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-003, WPN-001 | yes | done | astra (gpt-6) | 2026-10-13 | b575efe | Hit-clipped instanced beams and six-frame impact sparks; spec-required: 3, extra: 0 |
| VFX-008 | Thruster plumes | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-002, FLT-001 | yes | done | astra (gpt-6) | 2026-10-13 | b575efe | State-driven white-blue plumes, rotated nozzles and hulk suppression; spec-required: 4, extra: 0 |
| VFX-009 | Shield shells and ripples | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-002, PWR-001, CORE-001 | yes | done | astra (gpt-6) | 2026-10-13 | b575efe | Ellipsoid shells, eight timed ripples and hit flash; PWR gameplay remains open (ADR-0060); spec-required: 3, extra: 0 |
| VFX-010 | Chunk-body dissolve hook | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-002, DMG-001 | yes | done | astra (gpt-6) | 2026-10-13 | b575efe | Sole ChunkDissolve writer derives lifetime from EndTick; spec-required: 1, extra: 0 |
| VFX-011 | Quality levels and the settings hook | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-003 | yes | done | astra (gpt-6) | 2026-10-13 | b575efe | All quality caps applied without buffer reallocation; spec-required: 2, extra: 0 |
| VFX-012 | Regression and budget tests (02-C29, GC, upload) | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-005, VFX-006, VFX-007 | yes | done | astra (gpt-6) | 2026-10-13 | b575efe | Warmed real hit upload 0 B GC, fixed object count; upload timing misses advisory; spec-required: 4, extra: 0 |
| VFX-015 | Owner-requested complete effects batch and larger range | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | frontier | VFX-004 | yes | done | astra (gpt-6) | 2026-10-13 | b575efe | Bulk VFX-005…014 completed and validated together; station 3x, 153 cells wide, real Space fire; existing feature test extended; spec-required: 1, extra: 0 |
| AUD-006 | SfxEvent drain: coalescing, cooldown and voice allocation | [SPEC-64](specs/SPEC-64-audio.md) | M2 | frontier | AUD-005, TST-001 | yes | todo |  |  |  |  |
| AUD-007 | Cue content, legacy import and the CombatAudioCues singleton | [SPEC-64](specs/SPEC-64-audio.md) | M2 | standard | AUD-006 | no | todo |  |  |  |  |
| AUD-008 | Music director, crossfades and scene contexts | [SPEC-64](specs/SPEC-64-audio.md) | M2 | standard | AUD-006, FND-001 | no | todo |  |  |  |  |
| AUD-009 | Loop voices: player engine and beams | [SPEC-64](specs/SPEC-64-audio.md) | M2 | standard | AUD-006, FLT-001, WPN-001 | no | todo |  |  |  |  |
| AUD-013 | Audio performance and allocation tests | [SPEC-64](specs/SPEC-64-audio.md) | M2 | standard | AUD-008, AUD-009 | no | todo |  |  |  |  |
| DEV-010 | aim and fire commands | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M2 | standard | DEV-009, WPN-001, INP-001, CAM-001 | no | todo |  |  |  |  |
| DEV-011 | perf command and gh_perf extensions | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M2 | standard | DEV-009, VFX-001, AUD-001, TST-001 | no | todo |  |  |  |  |
| DEV-013 | Overlays: islands and spatial hash | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M2 | standard | DEV-012, AI-001 | no | todo |  |  |  |  |
| TST-005 | Determinism runner and the combat determinism gate | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M2 | frontier | TST-002, TST-003, SPEC-13, SPEC-20, SPEC-21, SPEC-22, SPEC-23 | no | todo |  |  |  |  |
| TST-006 | Ten-minute combat soak | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M2 | standard | TST-005, SPEC-70 | no | todo |  |  |  |  |
| MIG-009 | Weapon converter and weapon links | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M2 | standard | MIG-006, MIG-011, WPN-001, DATA-001 | yes | in-progress | sol (gpt-6.1-sol) | 2026-10-13 |  | Combat worker; supplies canonical links before strict DATA-010 integration; Q216 |
| MIG-010 | Ability converter and ability grants | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M2 | standard | MIG-006, ABL-001, SPEC-04 | no | todo |  |  |  |  |
| MIG-011 | Audio cue converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M2 | standard | MIG-004, AUD-001 | yes | done | content (gpt-6.1-sol) | 2026-10-13 | ca37e33 | 20 generated cues; lead fixture fix ed77024; one required + three migration safety tests. Full batch green. |
| FND-011 | Load flow end to end (R28) | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M3 | frontier | FND-010, SPEC-30, SPEC-31 | no | todo |  |  |  |  |
| FND-013 | Blueprint Lab entry and exit | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M3 | frontier | FND-011, EDT-001 | no | todo |  |  |  |  |
| FND-015 | Boot from any scene and the defeat flow | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M3 | standard | FND-011, HUD-001 | no | todo |  |  |  |  |
| PERS-013 | Regression scenario R28: load from the pause menu | [SPEC-05](specs/SPEC-05-persistence.md) | M3 | frontier | PERS-009, PERS-010, SPEC-01, SPEC-20, SPEC-31, SPEC-61, TST-001 | no | todo |  |  |  |  |
| PERS-014 | Regression scenario 04-B1: load after a content change (end to end) | [SPEC-05](specs/SPEC-05-persistence.md) | M3 | standard | PERS-010, SPEC-01, SPEC-50, SPEC-61, TST-001 | no | todo |  |  |  |  |
| PERS-015 | Full-world round trip with all real participants | [SPEC-05](specs/SPEC-05-persistence.md) | M3 | frontier | PERS-011, PERS-012, SPEC-01, SPEC-30, SPEC-31, SPEC-33, SPEC-40, SPEC-41, SPEC-42, SPEC-50 | no | todo |  |  |  |  |
| VOX-020 | Incremental grid edits for the editor preview | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M3 | frontier | VOX-014 | yes | todo |  |  |  |  |
| PHY-022 | Mass of restored cells (ADR-0016) | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M3 | standard | PHY-013, DMG-001 | yes | done | power (gpt-6.1-sol) | 2026-10-13 | a606bfc | Restored-cell mass accumulator; ownership fixture repair; 1 required test; full batch 901 EM / 128 PM, zero failures |
| DMG-019 | Impulse and restore channels (ADR-0016) | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M3 | frontier | DMG-006, DMG-007, SPEC-02 | yes | todo |  |  |  | partial in DMG-002: channels/contracts and readback / impulse consumer and bounded heal/place passes with nine required tests |
| AI-013 | Fleet data and formations | [SPEC-23](specs/SPEC-23-ai.md) | M3 | standard | AI-003 | no | todo |  |  |  |  |
| AI-014 | FleetSystem and member thinking | [SPEC-23](specs/SPEC-23-ai.md) | M3 | frontier | AI-013, AI-007 | no | todo |  |  |  |  |
| AI-015 | Fleet and swarm scenario and performance | [SPEC-23](specs/SPEC-23-ai.md) | M3 | standard | AI-014 | no | todo |  |  |  |  |
| GAL-001 | Public contract types | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | standard | CORE-001, DATA-001, VOX-001, NAR-001 | yes | in-progress | sol (gpt-6.1-sol) | 2026-10-13 |  | Power worker; exact galaxy contracts; lead validation |
| GAL-002 | Orbit math | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | standard | GAL-001 | no | todo |  |  |  |  |
| GAL-003 | Definitions, properties and validator | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | standard | GAL-001 | no | todo |  |  |  |  |
| GAL-004 | GalaxyDefBuilder and layout job | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | frontier | GAL-003 | no | todo |  |  |  |  |
| GAL-005 | Lane graph and galaxy hash | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | frontier | GAL-004 | no | todo |  |  |  |  |
| GAL-006 | Star-system content job | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | frontier | GAL-002, GAL-005 | no | todo |  |  |  |  |
| GAL-007 | StarSystemDelta, GalaxyState and the applier | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | frontier | GAL-006 | no | todo |  |  |  |  |
| GAL-008 | GalaxyService, ticker and bridge system | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | frontier | GAL-007, FND-001 | no | todo |  |  |  |  |
| GAL-009 | OriginRebaseSystem | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | frontier | GAL-001, SPEC-02 | no | todo |  |  |  |  |
| GAL-010 | GLXY save participant | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | frontier | GAL-007, PERS-001 | no | todo |  |  |  |  |
| GAL-011 | Galaxy map model | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | standard | GAL-008 | no | todo |  |  |  |  |
| GAL-012 | M3 galaxy content | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | standard | GAL-003, SPEC-72 | no | todo |  |  |  |  |
| GAL-013 | Performance gates | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | standard | GAL-005, GAL-006, GAL-009, GAL-011, TST-001 | no | todo |  |  |  |  |
| SYS-001 | Public contract types | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | standard | CORE-001, DATA-001, GAL-001, WPN-001 | no | todo |  |  |  | partial in VFX-015: StarSystemEntered/StarSystemEntryReason event contracts / remaining streaming contracts and producers still open |
| SYS-002 | Streaming config and boot singleton | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | standard | SYS-001, SPEC-04 | no | todo |  |  |  |  |
| SYS-003 | Structure builders | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | standard | SYS-001, SPEC-10 | no | todo |  |  |  |  |
| SYS-004 | Load and unload (T1) | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | frontier | SYS-002, SPEC-30, SPEC-10, SPEC-12 | no | todo |  |  |  |  |
| SYS-005 | BodyOnRailsSystem | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | standard | SYS-004 | no | todo |  |  |  |  |
| SYS-006 | T2 voxel bodies and damage persistence | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | frontier | SYS-003, SYS-004, SPEC-13, SPEC-21, SPEC-23 | no | todo |  |  |  |  |
| SYS-007 | T3 asteroid belts | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | frontier | SYS-006 | no | todo |  |  |  |  |
| SYS-008 | InteractionQuerySystem, docking and BodyInteracted | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | frontier | SYS-004, SPEC-20, SPEC-23 | no | todo |  |  |  |  |
| SYS-009 | PLOC participant | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | frontier | SYS-008, PERS-001 | no | todo |  |  |  |  |
| SYS-010 | IShipSpawner contract for Galaxy services | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | standard | SYS-001, FND-001 | no | todo |  |  |  |  |
| SYS-011 | Celestial visuals and distance compression | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | standard | SYS-004, SPEC-62 | no | todo |  |  |  |  |
| SYS-012 | Regression scenarios R9, R28 | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | standard | SYS-006, SYS-009, FND-001 | no | todo |  |  |  |  |
| SYS-013 | Jump integration hooks | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | frontier | SYS-006, SYS-008 | no | todo |  |  |  |  |
| SYS-014 | World gates: streaming performance and soak | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | frontier | SYS-007, SYS-013, TST-001 | no | todo |  |  |  |  |
| TRV-001 | Public contract types | [SPEC-32](specs/SPEC-32-travel.md) | M3 | standard | CORE-001, DATA-001, GAL-001, SYS-001, FLT-001, NAR-001, AUD-001 | no | todo |  |  |  |  |
| TRV-002 | Travel config definition and validator | [SPEC-32](specs/SPEC-32-travel.md) | M3 | standard | TRV-001, SPEC-04, SPEC-72 | no | todo |  |  |  |  |
| TRV-003 | Lane access: gated lanes, fuel, revision | [SPEC-32](specs/SPEC-32-travel.md) | M3 | standard | TRV-002, SPEC-30, SPEC-40 | no | todo |  |  |  |  |
| TRV-004 | Route planner | [SPEC-32](specs/SPEC-32-travel.md) | M3 | standard | TRV-001, SPEC-30 | no | todo |  |  |  |  |
| TRV-005 | Arrival solver | [SPEC-32](specs/SPEC-32-travel.md) | M3 | standard | TRV-001, SPEC-30 | no | todo |  |  |  |  |
| TRV-006 | TravelService jump timeline | [SPEC-32](specs/SPEC-32-travel.md) | M3 | frontier | TRV-003, TRV-005, TRV-007, SPEC-31, SPEC-30, FND-001 | no | todo |  |  |  |  |
| TRV-007 | PlayerControlLock consumption | [SPEC-32](specs/SPEC-32-travel.md) | M3 | standard | TRV-001, SPEC-20 | no | todo |  |  |  |  |
| TRV-008 | Gate interaction, gate info and notices | [SPEC-32](specs/SPEC-32-travel.md) | M3 | standard | TRV-006 | no | todo |  |  |  |  |
| TRV-009 | Travel presentation | [SPEC-32](specs/SPEC-32-travel.md) | M3 | standard | TRV-006, SPEC-60, SPEC-63, SPEC-64, SPEC-31 | no | todo |  |  |  |  |
| TRV-010 | Destination and route service | [SPEC-32](specs/SPEC-32-travel.md) | M3 | standard | TRV-004, TRV-006 | no | todo |  |  |  |  |
| TRV-011 | Narrative, dev probes and the R30 scenario | [SPEC-32](specs/SPEC-32-travel.md) | M3 | standard | TRV-008, TRV-010, SPEC-40, SPEC-70 | no | todo |  |  |  |  |
| TRV-012 | Jump integration, R30 regression and budget | [SPEC-32](specs/SPEC-32-travel.md) | M3 | standard | TRV-009, TRV-011, TST-001 | no | todo |  |  |  |  |
| ENC-001 | Public contract types | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M3 | standard | CORE-001, DATA-001, GAL-001, SYS-001, TRV-001, WPN-001, AI-001, NAR-001, ECO-001 | no | todo |  |  |  |  |
| ENC-002 | Definitions, config and validators | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M3 | standard | ENC-001, SPEC-04 | no | todo |  |  |  |  |
| ENC-003 | Faction singleton, index map and FactionStandingSystem | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M3 | frontier | ENC-002, SPEC-21, SPEC-13, SPEC-23 | no | todo |  |  |  |  |
| ENC-004 | FactionService and FACN | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M3 | frontier | ENC-003, PERS-001, FND-001 | no | todo |  |  |  |  |
| ENC-005 | TerritoryService (authored ownership) | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M3 | standard | ENC-002, SPEC-30 | no | todo |  |  |  |  |
| ENC-006 | Composition and placement | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M3 | standard | ENC-002, SPEC-30 | no | todo |  |  |  |  |
| ENC-007 | EncounterDirectorService | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M3 | frontier | ENC-004, ENC-005, ENC-006, SPEC-31, SPEC-32, SPEC-23 | no | todo |  |  |  |  |
| ENC-008 | ENCT participant | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M3 | frontier | ENC-007, PERS-001 | no | todo |  |  |  |  |
| ENC-009 | Narrative wiring | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M3 | standard | ENC-007, SPEC-40 | no | todo |  |  |  |  |
| ENC-010 | Legacy converter and M3 content | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M3 | standard | ENC-002, SPEC-72 | no | todo |  |  |  |  |
| ENC-011 | Integration and regression scenarios | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M3 | standard | ENC-008, ENC-009, ENC-010, TST-001 | no | todo |  |  |  |  |
| ENC-012 | Dev commands and probes | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M3 | standard | ENC-009, SPEC-70 | no | todo |  |  |  |  |
| NAR-001 | Public contract types | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | standard | CORE-001, DATA-001 | yes | done | combat (gpt-6.1-sol) | 2026-10-13 | e39b2d3 | Narrative contracts and precise future-task stubs; 2 required tests; full batch 901 EM / 128 PM, zero failures |
| NAR-002 | Lexer and parser | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-001 | yes | todo |  |  |  |  |
| NAR-003 | Type checker, registry, compiler and binder | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-002 | no | todo |  |  |  |  |
| NAR-004 | Condition and expression nodes | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | standard | NAR-003 | no | todo |  |  |  |  |
| NAR-005 | Effect nodes and atomic effect lists | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-004 | no | todo |  |  |  |  |
| NAR-006 | Fact catalog and reference resolvers | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-001 | yes | todo |  |  |  |  |
| NAR-007 | FactStore | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-006 | no | todo |  |  |  |  |
| NAR-008 | FACT save participant | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-007, PERS-001 | no | todo |  |  |  |  |
| NAR-009 | Import-time compile pipeline | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | standard | NAR-003, NAR-006 | no | todo |  |  |  |  |
| NAR-010 | NarrativeService tick and dependency index | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-007 | no | todo |  |  |  |  |
| NAR-011 | LogicContext and LogicWorldAdapter in Game.App | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-005, NAR-010, FND-001, ECO-001, QST-001 | no | todo |  |  |  |  |
| NAR-012 | World-event bridge | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-011, SYS-001, ENC-001 | no | todo |  |  |  |  |
| NAR-013 | Narrative validator framework and fact rules | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | standard | NAR-009 | no | todo |  |  |  |  |
| NAR-014 | Legacy converter: reading, mapping tables and logic translation | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | standard | NAR-003, MIG-001 | no | todo |  |  |  |  |
| NAR-015 | Legacy converter: Yarn emission, beats and preservation manifest | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-014, DLG-001, QST-001 | no | todo |  |  |  |  |
| NAR-016 | Legacy converter: quests, triggers, templates and flag assets | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | standard | NAR-015 | no | todo |  |  |  |  |
| NAR-017 | Narrative Debugger window (read-only) | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | standard | NAR-010 | no | todo |  |  |  |  |
| NAR-019 | Load-safety and determinism tests | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | standard | NAR-012 | no | todo |  |  |  |  |
| NAR-020 | LogicWatcher | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | standard | NAR-004 | no | todo |  |  |  |  |
| DLG-001 | Public contract types | [SPEC-41](specs/SPEC-41-dialog.md) | M3 | standard | NAR-001, UI-001 | yes | todo |  |  |  |  |
| DLG-002 | Dialog script parser | [SPEC-41](specs/SPEC-41-dialog.md) | M3 | frontier | DLG-001, SPEC-40 | no | todo |  |  |  |  |
| DLG-003 | Dialog validator rules and generated files | [SPEC-41](specs/SPEC-41-dialog.md) | M3 | standard | DLG-002 | no | todo |  |  |  |  |
| DLG-004 | Yarn Spinner 3 compatibility spike | [SPEC-41](specs/SPEC-41-dialog.md) | M3 | frontier | DLG-002 | no | todo |  |  |  |  |
| DLG-005 | GATE: Yarn Spinner adoption | [SPEC-41](specs/SPEC-41-dialog.md) | M3 | standard | DLG-004 | no | todo |  |  |  |  |
| DLG-006 | DialogService, memory and DLGS chunk | [SPEC-41](specs/SPEC-41-dialog.md) | M3 | frontier | DLG-001, SPEC-40 | no | todo |  |  |  |  |
| DLG-007 | Production dialog runner (path chosen by DLG-005) | [SPEC-41](specs/SPEC-41-dialog.md) | M3 | frontier | DLG-005, DLG-006, DLG-003 | no | todo |  |  |  |  |
| DLG-008 | Dialog presenter | [SPEC-41](specs/SPEC-41-dialog.md) | M3 | standard | DLG-006 | no | todo |  |  |  |  |
| DLG-009 | Dialog view screen | [SPEC-41](specs/SPEC-41-dialog.md) | M3 | standard | DLG-008, UI-001, INP-001 | no | todo |  |  |  |  |
| DLG-010 | Speakers and illustrations | [SPEC-41](specs/SPEC-41-dialog.md) | M3 | standard | DLG-001 | no | todo |  |  |  |  |
| DLG-011 | Dialog PlayMode scenarios | [SPEC-41](specs/SPEC-41-dialog.md) | M3 | standard | DLG-007, DLG-009, QST-001 | no | todo |  |  |  |  |
| DLG-012 | Harness commands for dialog | [SPEC-41](specs/SPEC-41-dialog.md) | M3 | standard | DLG-008, DEV-001 | no | todo |  |  |  |  |
| QST-001 | Public contract types | [SPEC-42](specs/SPEC-42-quests-mystery.md) | M3 | standard | CORE-001, DATA-001, NAR-001, DLG-001, GAL-001, ENC-001 | no | todo |  |  |  |  |
| QST-002 | Definitions, logic compile/bind and validator rules | [SPEC-42](specs/SPEC-42-quests-mystery.md) | M3 | standard | QST-001, SPEC-40 | no | todo |  |  |  |  |
| QST-003 | QuestService | [SPEC-42](specs/SPEC-42-quests-mystery.md) | M3 | frontier | QST-002, SPEC-40 | no | todo |  |  |  |  |
| QST-004 | ClueService | [SPEC-42](specs/SPEC-42-quests-mystery.md) | M3 | standard | QST-002, SPEC-40 | no | todo |  |  |  |  |
| QST-005 | BeatService | [SPEC-42](specs/SPEC-42-quests-mystery.md) | M3 | frontier | QST-003, QST-004, SPEC-41 | no | todo |  |  |  |  |
| QST-006 | Adapter, ship log and markers | [SPEC-42](specs/SPEC-42-quests-mystery.md) | M3 | standard | QST-005 | no | todo |  |  |  |  |
| QST-007 | QUST participant | [SPEC-42](specs/SPEC-42-quests-mystery.md) | M3 | frontier | QST-006, PERS-001 | no | todo |  |  |  |  |
| QST-008 | Bridge rules, player-free gating and registration | [SPEC-42](specs/SPEC-42-quests-mystery.md) | M3 | standard | QST-005, QST-006, SPEC-40, SPEC-32, SPEC-33 | no | todo |  |  |  |  |
| QST-009 | Opening chapter content | [SPEC-42](specs/SPEC-42-quests-mystery.md) | M3 | standard | QST-002, SPEC-30, SPEC-40 | no | todo |  |  |  |  |
| QST-010 | Opening chapter playthrough | [SPEC-42](specs/SPEC-42-quests-mystery.md) | M3 | standard | QST-008, QST-009, QST-007, SPEC-70, SPEC-32, SPEC-50 | no | todo |  |  |  |  |
| ECO-001 | Public contract types | [SPEC-50](specs/SPEC-50-economy-inventory.md) | M3 | standard | CORE-001, DATA-001, NAR-001, VOX-001, DMG-001, SYS-001, FLT-001 | no | todo |  |  |  |  |
| ECO-002 | Definitions, config and validators | [SPEC-50](specs/SPEC-50-economy-inventory.md) | M3 | standard | ECO-001, SPEC-04 | no | todo |  |  |  |  |
| ECO-003 | InventoryModel and Wallet | [SPEC-50](specs/SPEC-50-economy-inventory.md) | M3 | frontier | ECO-001 | no | todo |  |  |  |  |
| ECO-004 | Loot and EconomyService | [SPEC-50](specs/SPEC-50-economy-inventory.md) | M3 | standard | ECO-003, SPEC-40 | no | todo |  |  |  |  |
| ECO-005 | Merchants | [SPEC-50](specs/SPEC-50-economy-inventory.md) | M3 | standard | ECO-003 | no | todo |  |  |  |  |
| ECO-006 | INVT participant | [SPEC-50](specs/SPEC-50-economy-inventory.md) | M3 | frontier | ECO-004, ECO-005, PERS-001 | no | todo |  |  |  |  |
| ECO-007 | Salvage | [SPEC-50](specs/SPEC-50-economy-inventory.md) | M3 | frontier | ECO-003, SPEC-13, SPEC-31, SPEC-12 | no | todo |  |  |  |  |
| ECO-008 | App glue and wiring | [SPEC-50](specs/SPEC-50-economy-inventory.md) | M3 | standard | ECO-004, ECO-005, ECO-007, SPEC-33, SPEC-01 | no | todo |  |  |  |  |
| ECO-009 | M3 economy content | [SPEC-50](specs/SPEC-50-economy-inventory.md) | M3 | standard | ECO-002, SPEC-72 | no | todo |  |  |  |  |
| ECO-010 | Economy integration scenarios | [SPEC-50](specs/SPEC-50-economy-inventory.md) | M3 | standard | ECO-006, ECO-008, ECO-009, TST-001 | no | todo |  |  |  |  |
| EDT-001 | Public contract types | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | standard | CORE-001, DATA-001, VOX-001, ECO-001, UI-001, HUD-001 | no | todo |  |  |  | partial in HUD-001: canonical empty editor VM/command identities / properties, nested models and command members remain |
| EDT-002 | Editor content tables | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-001, SPEC-04 | no | todo |  |  |  |  |
| EDT-003 | Draft reader/writer adapter and canonical hash | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-002, SPEC-10 | no | todo |  |  |  |  |
| EDT-004 | Edit history and diff commands | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-001 | no | todo |  |  |  |  |
| EDT-005 | EditSession core | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-003, EDT-004 | no | todo |  |  |  |  |
| EDT-006 | Count tables and bill of materials | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-005, ECO-001 | no | todo |  |  |  |  |
| EDT-007 | Placement validator | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-005 | no | todo |  |  |  |  |
| EDT-008 | Connectivity tracker | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-005 | no | todo |  |  |  |  |
| EDT-009 | Full validation, issues and remove-floating | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-006, EDT-007, EDT-008 | no | todo |  |  |  |  |
| EDT-010 | Stats accumulator and diff | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | standard | EDT-006, PWR-001 | no | todo |  |  |  |  |
| EDT-011 | Commit pipeline and R15 regression | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-009, EDT-010, SPEC-50 | no | todo |  |  |  |  |
| EDT-012 | DDA picker and placement origin | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | standard | EDT-005 | no | todo |  |  |  |  |
| EDT-013 | Mirror symmetry | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-007 | no | todo |  |  |  |  |
| EDT-014 | Controller, tool framework and basic tools | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-012, EDT-013 | no | todo |  |  |  |  |
| EDT-015 | Line, box and fill tools | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | standard | EDT-014 | no | todo |  |  |  |  |
| EDT-016 | Replace and select/transform tools | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-014 | no | todo |  |  |  |  |
| EDT-017 | Copy, cut, paste and stamp | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | standard | EDT-016 | no | todo |  |  |  |  |
| EDT-018 | Draft renderer | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-005, SPEC-11 | no | todo |  |  |  |  |
| EDT-019 | Overlay renderer and shader | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-014, EDT-018 | no | todo |  |  |  |  |
| EDT-020 | Blueprint store, browser and name sanitising | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | standard | EDT-017, PERS-001, SPEC-60 | no | todo |  |  |  |  |
| EDT-021 | Editor UI panels | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | standard | EDT-001, SPEC-60, SPEC-61 | no | todo |  |  |  |  |
| EDT-022 | Editor host, input, presenter and leave prompt | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-011, EDT-015, EDT-019, EDT-021, SPEC-62, INP-001 | no | todo |  |  |  |  |
| EDT-023 | Lab and ship-edit scene flow; R16/R19 regression | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-022, SPEC-01, SPEC-31 | no | todo |  |  |  |  |
| EDT-024 | Editor performance gates | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-022, TST-001 | no | todo |  |  |  |  |
| UI-008 | Meter, Slot and TabBar controls | [SPEC-60](specs/SPEC-60-ui-framework.md) | M3 | standard | UI-007 | yes | done | content (gpt-6.1-sol) | 2026-10-13 | 90c13ad | Meter, Slot and scrolling TabBar; 7 required tests; full batch 901 EM / 128 PM, zero failures |
| UI-009 | Tooltip service | [SPEC-60](specs/SPEC-60-ui-framework.md) | M3 | standard | UI-008 | yes | in-progress | sol (gpt-6.1-sol) | 2026-10-13 |  | Assigned reusable worker; central Unity validation |
| UI-010 | Notification service | [SPEC-60](specs/SPEC-60-ui-framework.md) | M3 | standard | UI-008 | yes | in-progress | sol (gpt-6.1-sol) | 2026-10-13 |  | Presentation worker; notifications; lead batch validation |
| UI-011 | Confirm dialog and text prompt | [SPEC-60](specs/SPEC-60-ui-framework.md) | M3 | standard | UI-005, UI-008 | yes | todo |  |  |  |  |
| UI-012 | Layout, contrast and allegiance-colour audits | [SPEC-60](specs/SPEC-60-ui-framework.md) | M3 | frontier | UI-008 | yes | todo |  |  |  |  |
| UI-013 | Accessibility options | [SPEC-60](specs/SPEC-60-ui-framework.md) | M3 | standard | UI-010, UI-012 | no | todo |  |  |  |  |
| HUD-001 | Public contract types | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | UI-001, CORE-001, CAM-001 | yes | done | combat (gpt-6.1-sol) | 2026-10-13 | a983d88 | Screen/VM/snapshot contracts; static notification lifetime fix f847997; 3 required tests; full batch 901 EM / 128 PM, zero failures |
| HUD-002 | UI composition, screen registration and root switching | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | frontier | HUD-001, SPEC-60, FND-001 | no | todo |  |  |  |  |
| HUD-003 | Main menu and new game | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002 | no | todo |  |  |  |  |
| HUD-004 | Save and load slots | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002, PERS-001 | no | todo |  |  |  |  |
| HUD-005 | Settings screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002 | no | todo |  |  |  |  |
| HUD-006 | Pause screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002 | no | todo |  |  |  |  |
| HUD-007 | Flight HUD view and layout | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002 | no | todo |  |  |  |  |
| HUD-008 | HudSnapshotSystem | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | frontier | HUD-001, PWR-001, WPN-001, ABL-001, SYS-001, GAL-001, FLT-001, VOX-001, TST-001 | no | todo |  |  |  |  |
| HUD-009 | HudSyncSystem | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | frontier | HUD-008, HUD-007, TRV-001, ENC-001 | no | todo |  |  |  |  |
| HUD-010 | HUD presenters and screen hotkeys | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-009, QST-001, ECO-001, INP-001, TRV-001, NAR-001 | no | todo |  |  |  |  |
| HUD-011 | Inventory screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002, SPEC-50 | no | todo |  |  |  |  |
| HUD-012 | Merchant screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002, SPEC-50 | no | todo |  |  |  |  |
| HUD-013 | Dialog host screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002 | no | todo |  |  |  |  |
| HUD-014 | Ship log screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002, SPEC-42 | no | todo |  |  |  |  |
| HUD-015 | MapCanvas control | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | frontier | HUD-001, SPEC-62 | no | todo |  |  |  |  |
| HUD-016 | Galaxy map and star-system map screens | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-015, SPEC-30, SPEC-31, TRV-001, QST-001 | no | todo |  |  |  |  |
| HUD-017 | Fleet screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002, EDT-001 | no | todo |  |  |  |  |
| HUD-018 | Ship editor host screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002, EDT-001 | no | todo |  |  |  |  |
| HUD-019 | Loading and dock screens | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-010, SYS-001, ECO-001, EDT-001 | no | todo |  |  |  |  |
| HUD-020 | Full-screen layout regression suite | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-003, HUD-004, HUD-005, HUD-006, HUD-007, HUD-011, HUD-012, HUD-013, HUD-014, HUD-016, HUD-017, HUD-018, HUD-019 | no | todo |  |  |  |  |
| CAM-006 | Origin warp and teleport cut | [SPEC-62](specs/SPEC-62-camera.md) | M3 | frontier | CAM-005, GAL-001 | no | todo |  |  |  |  |
| CAM-007 | Occlusion pull-in | [SPEC-62](specs/SPEC-62-camera.md) | M3 | frontier | CAM-005, PHY-001 | yes | todo |  |  |  |  |
| CAM-009 | Editor orbit mode | [SPEC-62](specs/SPEC-62-camera.md) | M3 | standard | CAM-005 | yes | done | content (gpt-6.1-sol) | 2026-10-13 | 21e43a1 | Editor orbit, bounded pan/zoom, snap/focus and suppression; 6 required tests; full batch 901 EM / 128 PM, zero failures |
| CAM-010 | Map view transform | [SPEC-62](specs/SPEC-62-camera.md) | M3 | standard | CAM-001 | yes | done | power (gpt-6.1-sol) | 2026-10-13 | 014ed0b | Map transforms, zoom anchoring, framing and clamps; 6 required tests; full batch 901 EM / 128 PM, zero failures |
| VFX-013 | Floating-origin shift and clears | [SPEC-63](specs/SPEC-63-vfx.md) | M3 | frontier | VFX-003, GAL-001, SYS-001, FND-001 | no | done | astra (gpt-6) | 2026-10-13 | b575efe | Canonical rebase anchors, reset/system clears, idle reset; gameplay streaming remains open; spec-required: 5, extra: 0 |
| VFX-014 | Warp effect and strict config validation | [SPEC-63](specs/SPEC-63-vfx.md) | M3 | standard | VFX-004, TRV-001 | no | done | astra (gpt-6) | 2026-10-13 | b575efe | 600-particle pooled warp with oldest stealing, all 13 defaults strict; travel gameplay remains open; spec-required: 3, extra: 0 |
| AUD-011 | GATE: Owner confirms licences of the carried-over audio | [SPEC-64](specs/SPEC-64-audio.md) | M3 | standard | AUD-010 | yes | todo |  |  |  |  |
| AUD-012 | Floating-origin shift and clears | [SPEC-64](specs/SPEC-64-audio.md) | M3 | frontier | AUD-006, GAL-001, SYS-001 | no | todo |  |  |  |  |
| DEV-014 | interact command and interaction overlay | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M3 | standard | DEV-009, SYS-001, INP-001 | no | todo |  |  |  |  |
| DEV-017 | inv, give and facts commands | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M3 | standard | DEV-009, ECO-001, NAR-001 | no | todo |  |  |  |  |
| DEV-018 | save, load and jump commands; toworld.sh | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M3 | standard | DEV-008, PERS-001, TRV-001, HUD-001 | no | todo |  |  |  |  |
| TST-007 | Whole-game frame budgets | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M3 | frontier | TST-003, SPEC-31, SPEC-33, SPEC-61, SPEC-21 | no | todo |  |  |  |  |
| MIG-012 | Faction converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M3 | standard | MIG-008, ENC-001 | no | todo |  |  |  |  |
| MIG-013 | Loot table converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M3 | standard | MIG-008, ECO-001 | no | todo |  |  |  |  |
| MIG-014 | Body template stubs | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M3 | standard | MIG-004, GAL-001 | no | todo |  |  |  |  |
| MIG-015 | Hand-off package | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M3 | standard | MIG-002, MIG-003 | yes | in-progress | sol (gpt-6.1-sol) | 2026-10-13 |  | Worker power lane; typed handoff extraction; three required tests; lead-owned Unity validation |
| INP-008 | Gamepad scheme | [SPEC-03](specs/SPEC-03-input.md) | M4 | standard | INP-003 | yes | in-progress | sol (gpt-6.1-sol) | 2026-10-13 |  | Combat worker lane; ready later-milestone input work while lead validates combat batch |
| REN-015 | LOD2 grid proxies | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M4 | frontier | REN-012 | yes | todo |  |  |  |  |
| PHY-021 | Region compounds for very large grids (only if PHY-019 shows assembly over budget) | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M4 | frontier | PHY-019 | yes | todo |  |  |  |  |
| DMG-018 | M4: per-brick component labels for >1M-cell stations | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M4 | frontier | DMG-016 | no | todo |  |  |  |  |
| WPN-016 | Per-instance fire groups (M4) | [SPEC-21](specs/SPEC-21-weapons.md) | M4 | frontier | WPN-011 | no | todo |  |  |  |  |
| GAL-014 | Brick-level structure diffs | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M4 | frontier | GAL-010 | no | todo |  |  |  |  |
| TRV-013 | Supercruise (in-system hop) | [SPEC-32](specs/SPEC-32-travel.md) | M4 | frontier | TRV-012, SPEC-23, SPEC-61 | no | todo |  |  |  |  |
| TRV-014 | Escort transfer hooks | [SPEC-32](specs/SPEC-32-travel.md) | M4 | standard | TRV-012, AI-001, FND-001 | no | todo |  |  |  |  |
| ENC-013 | Encounter formations | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M4 | standard | ENC-011, AI-001 | no | todo |  |  |  |  |
| ENC-014 | Procedural territory and TerritoryEntered | [SPEC-33](specs/SPEC-33-encounters-factions.md) | M4 | standard | ENC-011 | no | todo |  |  |  |  |
| NAR-018 | Narrative Debugger editing tools | [SPEC-40](specs/SPEC-40-narrative-core.md) | M4 | standard | NAR-017 | no | todo |  |  |  |  |
| DLG-013 | Line-ID export for localization | [SPEC-41](specs/SPEC-41-dialog.md) | M4 | standard | DLG-003 | no | todo |  |  |  |  |
| EDT-025 | Radial symmetry | [SPEC-51](specs/SPEC-51-ship-editor.md) | M4 | frontier | EDT-013 | no | todo |  |  |  |  |
| EDT-026 | In-world edit when docked | [SPEC-51](specs/SPEC-51-ship-editor.md) | M4 | frontier | EDT-023, SPEC-10, SPEC-11 | no | todo |  |  |  |  |
| ART-001 | Public contract types | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | standard | DATA-001 | yes | done | claude (claude-opus-5-5) | 2026-10-10 | 386ebf5 | SPEC-80 Q9 answered: yes |
| ART-002 | Direction token sheets and the token checker | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | standard | ART-001 | yes | done | claude (claude-opus-5-5) | 2026-10-10 | c31fa16 | DRYDOCK `--color-hull` fixed to pass C4; sheets log 12 font-path import warnings until ART-008 |
| ART-003 | Mood boards per direction | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | — | yes | done | claude (claude-opus-5-5) | 2026-10-11 | a3d0d92 | 13 public-domain images added (owner-approved). Done under ADR-0019 (no per-task review) |
| ART-004 | Block-kit style test: five blocks per direction | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-002 | yes | review | claude (claude-opus-5-5) | 2026-10-10 | 5cebb6b | Open: the 150 m glyph test (criterion 3; likely fails for 1-cell-wide blocks, see log). Per-task review dropped (ADR-0019) |
| ART-005 | Interactive HTML design board | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-002, ART-003, ART-004 | no | done | claude (claude-opus-5-5) | 2026-10-11 | a6b2fe2 | Safari file:// check pending. Done under ADR-0019 (no per-task review) |
| ART-006 | Owner shortlist of finalist directions | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | standard | ART-005 | yes | done | frank (human) | 2026-10-11 | 6a64d71 | Owner chose one direction, NOCTURNE (2026-10-11), instead of two finalists |
| ART-007 | Brick material baker, preview mesher, reference shader and art validator | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-001 | yes | in-progress | claude (claude-opus-5-5) | 2026-10-11 | 083c482 | Baker, preview mesher and SH_BrickReference done on branch task/ART-007-brick-tools (083c482, unmerged). ArtDatabaseValidator is unblocked (CORE-002, DATA-004, DATA-005 done 2026-10-11) but not started |
| ART-008 | Look-test content for each finalist | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-004, ART-006, ART-007 | no | todo |  | 2026-10-11 |  | Re-scoped by the gate approval 2026-10-11: runs after the renderer exists (M1), as a check, not a gate |
| ART-009 | Look-test scenes with HUD mock | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-002, ART-008 | no | todo |  | 2026-10-11 |  | Re-scoped 2026-10-11: after the renderer exists, as a check |
| ART-010 | Look-test capture pack and metrics | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-009 | no | todo |  | 2026-10-11 |  | Re-scoped 2026-10-11: after the renderer exists, as a check |
| ART-011 | Owner look-test review and one revision round | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-010 | no | todo |  | 2026-10-11 |  | Re-scoped 2026-10-11: after the renderer exists, as a check |
| ART-012 | Finalize the design system for approval | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-011 | no | done | claude (claude-opus-5-5) | 2026-10-11 | d60cdf7 | Replaced by DESIGN-GUIDE.md (d60cdf7); golden captures wait for the in-engine look test. Done under ADR-0019 (no per-task review) |
| ART-013 | GATE: Owner approves design system | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | standard | ART-012 | yes | done | frank (human) | 2026-10-11 | 6a64d71 | Approved 2026-10-11: NOCTURNE as DESIGN-GUIDE.md. Owner: "yes I approve" |
| AST-001 | Public contract types: asset manifest, naming rules and licence register | [SPEC-81](specs/SPEC-81-asset-production.md) | A0 | standard | ART-001 | yes | in-progress | sol (gpt-6.1-sol) | 2026-10-13 |  | Worker presentation lane; contracts and planned manifest only, no art production |
| AST-002 | Art import rules and asset validator | [SPEC-81](specs/SPEC-81-asset-production.md) | A0 | standard | AST-001 | no | todo |  |  |  |  |
| AST-003 | Licence audit of carried-over placeholders | [SPEC-81](specs/SPEC-81-asset-production.md) | A0 | standard | AST-001 | no | todo |  |  |  |  |
| ART-014 | Apply the approved direction | [SPEC-80](specs/SPEC-80-art-direction.md) | A1 | standard | ART-013 | yes | todo |  |  |  |  |
| AST-004 | Fonts and type ramp | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | SPEC-80, AST-002 | no | todo |  |  |  |  |
| AST-005 | Brick layers: hull and internals (layers 0–19) | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | SPEC-80, SPEC-11, AST-002 | no | todo |  |  |  |  |
| AST-006 | Brick layers: station, asteroid, Otherness, debug and missing (layers 20–63) | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-005 | no | todo |  |  |  |  |
| AST-007 | Brick material, paint palette and damage look | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-006 | no | todo |  |  |  |  |
| AST-008 | Shared prop material and trim sheet | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | SPEC-80, SPEC-11, AST-002 | no | todo |  |  |  |  |
| AST-009 | Structural block kit | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-007 | no | todo |  |  |  |  |
| AST-010 | Propulsion block meshes | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-008 | no | todo |  |  |  |  |
| AST-011 | Weapon block meshes | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-008, SPEC-21 | no | todo |  |  |  |  |
| AST-012 | Power, shield, core and utility block meshes | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-008 | no | todo |  |  |  |  |
| AST-013 | Detail prop kit | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-008 | no | todo |  |  |  |  |
| AST-014 | Reference ship and station blueprints restyle | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-009, AST-010, AST-011, AST-012, AST-013, SPEC-10, SPEC-51, SPEC-72 | no | todo |  |  |  |  |
| AST-015 | Procedural sky | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | frontier | AST-007, SPEC-31 | no | todo |  |  |  |  |
| AST-016 | Stars, planets, moons and rings | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | frontier | AST-015, SPEC-31 | no | todo |  |  |  |  |
| AST-017 | Gates, lanes and anomalies | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-016, SPEC-32 | no | todo |  |  |  |  |
| AST-018 | VFX: destruction | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | frontier | AST-007, SPEC-63 | no | todo |  |  |  |  |
| AST-019 | VFX: weapons and projectiles | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-018, SPEC-21 | no | todo |  |  |  |  |
| AST-020 | VFX: ship systems and travel | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-018, SPEC-32 | no | todo |  |  |  |  |
| AST-021 | Editor overlay look | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-004, SPEC-51 | no | todo |  |  |  |  |
| AST-022 | UI kit: components and theme | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-004, SPEC-60 | no | todo |  |  |  |  |
| AST-023 | HUD kit | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-022, SPEC-61 | no | todo |  |  |  |  |
| AST-024 | System glyphs I: navigation, notices, markers, functions, input | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-022 | no | todo |  |  |  |  |
| AST-025 | System glyphs II: editor tools, stats, damage types | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-024 | no | todo |  |  |  |  |
| AST-026 | Rendered icons: icon renderer, blocks, ships and paints | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-009, AST-010, AST-011, AST-012, AST-013, AST-014 | no | todo |  |  |  |  |
| AST-027 | Drawn icons: items, officers, abilities and faction crests | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-024 | no | todo |  |  |  |  |
| AST-028 | Cursors | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-024, SPEC-60 | no | todo |  |  |  |  |
| AST-029 | Comms portraits, NORA avatar, main-menu backdrop, wordmark and app icon | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-022, AST-014, AST-015 | no | todo |  |  |  |  |
| AST-030 | Audio: UI and editor SFX | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | SPEC-80, AST-003, SPEC-64 | no | todo |  |  |  |  |
| AST-031 | Audio: combat, destruction and ship-system SFX | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-030, SPEC-21 | no | todo |  |  |  |  |
| AST-032 | Audio: music and ambience | [SPEC-81](specs/SPEC-81-asset-production.md) | A1 | standard | AST-030 | no | todo |  |  |  |  |
| AST-033 | A2: swap every placeholder reference | [SPEC-81](specs/SPEC-81-asset-production.md) | A2 | standard | AST-004, AST-007, AST-008, AST-016, AST-017, AST-019, AST-020, AST-021, AST-023, AST-025, AST-026, AST-027, AST-028, AST-029, AST-031, AST-032, SPEC-61, SPEC-63, SPEC-64 | no | todo |  |  |  |  |
| AST-034 | A2: delete placeholders and sweep for third-party content | [SPEC-81](specs/SPEC-81-asset-production.md) | A2 | standard | AST-033 | no | todo |  |  |  |  |
| AST-035 | A2: final look verification against golden images and budgets | [SPEC-81](specs/SPEC-81-asset-production.md) | A2 | frontier | AST-034 | no | todo |  |  |  |  |
<!-- tasks:end -->

## Open questions

Questions found while working. Use the *Default* until the *Decider* answers; then record the answer and update the spec.

| # | Date | Spec / task | Question | Default in use | Decider | Answer |
|---|---|---|---|---|---|---|
| 1 | 2026-10-09 | architecture §3, §5, §7.4, §8 | The integration pass applied architecture errata without an ADR (as the earlier errata commit `c4f09f6` did): `Game.Tests.Common`; galaxy services realised inside `GalaxyServicesBridgeSystem`; `BodyOnRailsSystem` and `FactionStandingSystem` in the fixed step; `SalvageMarkSystem`; jump flow; new single writers (`PlayerControlLock`, `FactionRelations`, `PlayerCombatStatus`, travel state, encounter instances, `VoxelGrid.Kind`); `PhysicsVelocity` exceptions (planar, mass re-expression, placement) | Errata stand as written | Owner | |
| 2 | 2026-10-09 | DESIGN-SYSTEM §11, SPEC-80 §4.4 | Faction and travel-cover tokens are *functional* (read by code before the design approval): their names are binding now, their values wait for §12 | Names binding, placeholder values | Owner (at ART-013) | |
| 3 | 2026-10-10 | architecture §5, §6, §7.1, §8; SPEC-01, 04, 10, 12, 13, 20–24, 33, 51, 72, 80; 06 | Extensibility review `docs/specs/_EXTENSIBILITY-REVIEW.md`: approve ADR items X1 (block function modules), X2 (power/stats contributions), X3 (ship profiles + stowed grids, tick-exact launch), X4 (request channels: impulse, cell restore, inhibit, stat-modifier sources); choose the small-craft representation X7; apply errata X5, X6, X8–X11 | Nothing applied; specs as written | Owner | **Answered 2026-10-10: apply all as recommended; X7 = voxel small craft.** Applied in `e3e6c86`…`6b3ee37` (ADR-0013…0017); outcomes in the review §5 |
| 4 | 2026-10-10 | ROADMAP, SPEC-80, SPEC-81 | Owner: "our first thing is to nail the look of the game in terms of blocks, ui, meshes, colors, textures, etc. nothing proceeds until the design system is spot on". Which code may run before `ART-013`? | Only what the look tests need: `CORE-001`, `DATA-001`, `DATA-002`, `DATA-004`, `DATA-005`, `DATA-008` (for `ART-007`'s validator), and `AST-001`…`AST-003`. Everything else in M0–M4 waits | Owner | **Answered 2026-10-10 (owner, verbatim):** "Whatever you deep necessary within the scope in order to showcase the most faithful depictions so that our decisions are based on accurate visuals". The lead worker decides which code runs before `ART-013`, limited to what makes the look tests faithful to the final game |
| 5 | 2026-10-10 | SPEC-02 CORE-001, CORE-009 | `ManagedEventDrainSystem` stub: throwing lifecycle methods would break Play Mode (SPEC-02 open question 10) | Empty `OnUpdate`/`OnDestroy` with `TODO(CORE-009)`; the public API throws | Tech lead | |
| 6 | 2026-10-10 | SPEC-04 §4.2, SPEC-11 §4, SPEC-12; look book round 3 | How do the look-dev hulls become game blocks? (a) Tapers: the hulls use cut cells (each 0.5 m cell clipped by up to three straight planes that continue across neighbours; 407–1,189 distinct shapes per direction, the 45° `Wedge` covers 8–21% of cut cells), the specs allow only `Cube` and `Wedge` (shape packed in one bit). Options: generic cut cells with a few quantised planes per cell (needs an ADR changing the packed format), a fixed slope catalogue (1:1, 1:2, 1:3 and their corners) with tapers limited to it, or Cube and Wedge only (stepped tapers). (b) Large hull blocks (2×2×2, 3×3×3) as one placeable block that a module splits back into cells. (c) Alien hulls as signed-distance cells meshed by surface nets | Specs as written (Cube, Wedge); the look book keeps showing cut cells, large blocks and smooth alien cells so the decision is made on the real look | Owner | **Study 2026-10-11 (look book, Slopes tab):** options: (1) slope + hip corner, all 45° (2 blocks, 1.5 % of cells off); (2) plus a long 1×2 slope for flat runs (3 blocks, edges step where it meets another slope); (3) plus a long hip corner (4 blocks). **(a) Answered 2026-10-11 (owner, verbatim): "option 2 is better"**: cube, slope, hip corner and long slope 1×2 ([ADR-0018](decisions/ADR-0018-slope-block-kit.md); SPEC-04 §4.2 updated; SPEC-04 and SPEC-11 open questions added for the per-cell shapes and the snapshot bits). Large blocks (b) and alien cells (c) are still open |
| 8 | 2026-10-11 | DESIGN-GUIDE §12, SPEC-80 `ART-013`, ROADMAP | The owner chose NOCTURNE and made DESIGN-GUIDE.md the source of truth. Does that count as the design-system approval (`ART-013`), so M0–M4 can start? | No: the gate stays open, M0–M4 wait | Owner | **Answered 2026-10-11 (verbatim): "yes I approve".** Gate passed; the look test runs after the renderer exists |
| 7 | 2026-10-10 | SPEC-33, DESIGN-SYSTEM factions; look book round 4 | Batch 3 builds the ship lines on the legacy factions and adds proposals: Triple Alliance house names (Pochteca Freight, Itztli Arms, Caisson Heavy Industries), ship names, refit codes (S) Scavenger, (I) Itztli, (K) Khuchad prize, the Khuchad port-armour/starboard-weapon doctrine, and redacted hints linking the Vanguard to the Aksharans. Where do batch 2's Knell choir and Caisson line sit in the roster? | Proposals only, nothing binding; Caisson is shown as the Alliance's third house; the Knell choir stays an independent order | Owner | |
| 9 | 2026-10-11 | SPEC-10 VOX-001 | `BrickRecord` fields total 28 bytes, but the spec says 32 (SPEC-10 open question 13) | 28 bytes, fields as written | Tech lead | |
| 10 | 2026-10-11 | SPEC-10 VOX-001, VOX-009; SPEC-05 PERS-001 | The VOXS adapter members need SPEC-05 types that PERS-001 creates after VOX-001 (SPEC-10 open question 14) | VOX-009 adds them; VOX-001 creates the files without them | Tech lead | |
| 11 | 2026-10-11 | SPEC-03 INP-001, INP-002 | The generated `Controls.Dispose()` errors in edit mode (SPEC-03 open question 4) | `InputService.Dispose` uses `DestroyImmediate` outside Play Mode | Tech lead | |
| 12 | 2026-10-11 | SPEC-03 §4.2, INP-002 | No task sets `shortcutKeysConsumeInput` in `ProjectSetup` (SPEC-03 open question 5) | INP-002 adds it | Tech lead | **Resolved 2026-10-11:** `ProjectSetup` creates `Assets/Game/Input/InputSystem.inputsettings.asset` with `shortcutKeysConsumeInput = true`, registers it, and `Check()` verifies it |
| 13 | 2026-10-11 | SPEC-01 FND-003, FND-004, SPEC-03 INP-002 | FND-003, FND-004 and INP-002 publish through `EventBus<T>` (CORE-006) but do not depend on it (SPEC-01 open question 6) | Both blocked until CORE-006 is done; add `CORE-006` to their *Depends on* | Tech lead | **Resolved 2026-10-11:** CORE-006 is done (3f0a613); the three tasks are unblocked |
| 14 | 2026-10-11 | SPEC-05 PERS-001 | Readonly result structs had no constructors (SPEC-05 open question 16) | All-field constructors added | Tech lead | |
| 15 | 2026-10-11 | SPEC-05 PERS-002 | Slot status when `meta.json` is valid but `world.sav` is missing (SPEC-05 open question 17) | `Ok` needs `world.sav`; else `BackupOnly`/`Corrupt` | Tech lead | |
| 16 | 2026-10-11 | SPEC-64 AUD-001 | Audio validation rules had no codes (SPEC-64 open question 12) | AUD-E001…E003 (cue), AUD-E010…E016 (config) | Tech lead | |
| 17 | 2026-10-11 | SPEC-02 CORE-012 | `SimEventReadSet.Readers` was a `NativeArray<NativeStream.Reader>`, a nested native container that jobs reject (SPEC-02 open question 11) | Resolved: readers handed out per job through `Reader(i)`; `SimEventGather` chains one job per registration | Tech lead | resolved in CORE-012 |
| 18 | 2026-10-11 | SPEC-02 CORE-009 | `SessionReset` is a §6.6 managed event and unmanaged, but has no ECS producer (SPEC-02 open question 12) | Resolved: `ManagedEventChannelsTests` skips a checked managed-only list | Tech lead | resolved in CORE-009 |
| 19 | 2026-10-11 | SPEC-02 CORE-009 | The drain's payload-hash tie-break hashed padding bytes, so equal-key events sorted nondeterministically (SPEC-02 open question 13) | Resolved: hash field bytes only | Tech lead | resolved in CORE-009 |
| 20 | 2026-10-11 | SPEC-71 TST-001 | Unity's rate managers can't step exact tick counts in tests (SPEC-71 open question 4) | Resolved: a counted rate manager; reduced worlds get ECB systems and group anchors | Tech lead | resolved in TST-001 |
| 21 | 2026-10-11 | SPEC-71 TST-001 | EditMode loops never end a profiler frame (SPEC-71 open question 5) | Resolved: `RunEcs` samples `CurrentValue` deltas | Tech lead | resolved in TST-001 |
| 22 | 2026-10-11 | SPEC-71 TST-001 | Reference-settings rule would leave the micro-gates unenforced in the Editor (SPEC-71 open question 6) | Resolved: `PerfRun.Gate` records and asserts micro-gates without the settings check | Tech lead | resolved in TST-001 |
| 23 | 2026-10-11 | SPEC-01 FND-002 | No §5.2 row constructed the `SettingsApplier` instance (SPEC-01 open question 7) | Resolved: row 14 constructs it before `ApplyAll` | Tech lead | resolved in FND-002 |
| 24 | 2026-10-11 | SPEC-01 FND-005 | `FND-E001` (no start blueprint) would fail every boot before ship blueprints exist (SPEC-01 open question 8) | Resolved: a warning on the definition, an error from `GameConfigValidator` once any blueprint exists | Tech lead | resolved in FND-005 |
| 25 | 2026-10-11 | SPEC-01 FND-006 | Startup in non-game scenes; the lazily created `WorldTime` singleton destroyed by teardown; calls into services that don't exist yet (SPEC-01 open question 9) | Resolved: non-game scenes left alone; `WorldTime` forced before the snapshot; missing collaborators are TODOs naming their tasks | Tech lead | resolved in FND-006 |
| 26 | 2026-10-11 | SPEC-01 FND-014 | Audit scope: auto-property backing fields, unmanaged readonly structs, generated code (SPEC-01 open question 10) | Resolved: backing fields audited, unmanaged readonly structs allowed, generated code and editor/test assemblies skipped | Tech lead | resolved in FND-014 |
| 27 | 2026-10-11 | SPEC-02 PERS-005 | No way to dispose the deferred blocks of an abandoned save (SPEC-02 open question 14) | Resolved: `SaveWriter.DisposeDeferredBlocks()`; deferred blocks must tolerate a second Dispose | Tech lead | resolved in PERS-005 |
| 28 | 2026-10-11 | SPEC-05 PERS-005 | Header ticks vs millisecond JSON; dropped-autosave result; queue capacity; where step 17 runs (SPEC-05 open question 18) | Resolved as listed there | Tech lead | resolved in PERS-005 |
| 29 | 2026-10-11 | SPEC-05 PERS-006 | A preserved newer-version chunk would collide with its participant's chunk on the next save (SPEC-05 open question 19) | Resolved: the participant is skipped and the newer chunk written back | Tech lead | resolved in PERS-006 |

| 30 | 2026-10-11 | SPEC-05 PERS-007 | VOXS runtime fields, full-palette fallback and bounded decompression (SPEC-05 Q20) | Reconstruct runtime state; deterministic Missing fallback; 256 MiB body/cell limits | Tech lead | Resolved as specified in SPEC-05 Q20; v1 wire format unchanged |

| 31 | 2026-10-11 | SPEC-05 PERS-008 | Codec Temp scratch fails on a managed background thread (SPEC-05 Q21) | TempJob scratch disposed before Encode returns | Tech lead | Resolved in PERS-008; Persistent capture ownership unchanged |
| 32 | 2026-10-11 | SPEC-05 PERS-011 | Deferred VOXS round trip had no snapshot-task prerequisite (SPEC-05 Q22) | Add PERS-008 | Tech lead | Resolved in PERS-011; fresh restore recipients prevent tests from accidentally reusing captured state |
| 33 | 2026-10-11 | SPEC-72 MIG-007 | Nonfinite Euler inputs and quarter-turn integer overflow (SPEC-72 Q12) | Reject nonfinite; reduce finite full turns before integer rounding | Tech lead | Resolved in MIG-007; exact cube matrices and the 1-degree tolerance remain unchanged |
| 34 | 2026-10-11 | SPEC-72 MIG-004 | Hand-edited link manifest adoption, first-run report equality, missing-field callbacks (SPEC-72 Q13) | Hash only owned/forced writes; stable reports after initial creation; guarded field setters; invalid manifests fail closed | Tech lead | Resolved in MIG-004; preserves protected assets on subsequent runs |
| 35 | 2026-10-11 | SPEC-71 TST-008 | Iteration setting and live CLI contract gaps (SPEC-71 Q7) | Both reloads disabled per ADR-0020; installed API schema; complete results and exit-status validation | Tech lead | Resolved in TST-008; wall-clock measurements include CLI overhead |
| 36 | 2026-10-11 | SPEC-03 INP-003 | Conflict removal loses an existing custom binding (SPEC-03 Q6) | Restore prior overrides on rejection; suppress Global during rebind; ADR-0021 for required test fixture | Tech lead | Resolved in INP-003; preserves the player’s old binding |
| 37 | 2026-10-11 | SPEC-72 MIG-005 | Placeholder path coupling and omitted dropped fields (SPEC-72 Q14) | Editor-only source-reference asset; reuse existing icons; report every dropped field | Tech lead | Resolved in MIG-005; no new art or owner look decision |
| 38 | 2026-10-11 | SPEC-72 MIG-006 | Deterministic SerializeReference identities (SPEC-72 Q15) | Managed-reference IDs equal each unique BlockModuleId | Tech lead | Resolved in MIG-006; repeated assets, manifest and report are byte-identical |
| 39 | 2026-10-11 | SPEC-72 MIG-008 / SPEC-10 | M0 blueprint validation depends on M1 stub (SPEC-72 Q16 / SPEC-10 Q15) | Bring the existing validator forward; keep setter/config work in VOX-010 | Tech lead | Resolved in MIG-008; no public API or assembly change |
| 40 | 2026-10-11 | SPEC-72 MIG-008 | Imported ships require a start blueprint (SPEC-72 Q17) | One-time empty-link assignment of ship_base for development | Tech lead | Resolved; Frank should review eventual gameplay starting ship |
| 41 | 2026-10-11 | SPEC-64 AUD-002 | Manual mixer creation conflicts with Frank’s CLI-only direction (SPEC-64 Q13) | Temporary installed-Editor API job plus runtime asset readback | Tech lead | Resolved in AUD-002; reflection stays in deleted one-off job |
| 42 | 2026-10-11 | SPEC-64 AUD-003 | M0 core precedes M2 audio config/cues; database lookup API mismatch (SPEC-64 Q3/Q14) | Silent null-config core; enumerate Definitions; normal routing with validated config | Tech lead | Resolved in AUD-003; full boot integration without placeholder cues |

| 43 | 2026-10-11 | SPEC-70 DEV-003 | Host precedes seven managed-event payloads and camera/player types (SPEC-70 Q13) | Three existing subscriptions, reserved zero counters and null lookups until owning tasks land | Tech lead | Resolved; staged contracts without speculative APIs |

| 44 | 2026-10-11 | SPEC-70 DEV-004 / SPEC-60 UI-003 | UI query precedes UIRoot (SPEC-70 Q14) | Explicit test root resolver; production UIDocument service registered by UI-003 | Tech lead | Resolved; unavailable until real UI exists, no scene search |

| 45 | 2026-10-11 | SPEC-70 DEV-005 | Expression grammar and R28 example contradictions (SPEC-70 Q15) | Highest unary precedence, uppercase tags, explicit future test registrations; active default | Tech lead | Resolved; no fake production features |

| 46 | 2026-10-11 | SPEC-70 DEV-006 | Result filename collisions and intentional failure fixture (SPEC-70 Q16) | Run ID/suffix filenames; negative fixture tag; JSON for start errors | Tech lead | Resolved; real failures retain exit codes without failing positive smoke selection |

| 47 | 2026-10-11 | SPEC-05 PERS-016 | Session capture exceptions leak snapshots after participants finish (SPEC-05 Q23) | Guard all capture stages and report ParticipantFailed with stage | Tech lead | Resolved; release blocks before ordinary completion and queue recovery |

| 48 | 2026-10-11 | SPEC-05 PERS-017 | Post-replacement sidecar errors falsely report failed saves; listing trusts stale JSON (SPEC-05 Q24) | world.sav replacement commits; later sidecar failure warns, embedded META wins | Tech lead | Resolved; no rollback of committed saves or misleading completion event |

| 49 | 2026-10-11 | SPEC-05 PERS-018 | JSON null bypasses object-read failure handling (SPEC-05 Q25) | Reject absent objects only at profile/META boundaries | Tech lead | Resolved; profile skip warning and non-fatal PERS-W013 |

| 50 | 2026-10-11 | SPEC-02 CORE-013 | EndRead drops native disposal handles; open-read disposal races writers (SPEC-02 Q15) | Keep all cleanup jobs in LastConsumer; complete pending writers before disposal | Tech lead | Resolved by explicit ownership chain; historical shutdown-crash relationship remains unproven |
| 51 | 2026-10-11 | SPEC-11 REN-001 | ShapeFace's listed fields occupy 52 bytes, not the stated 56 | Preserve fields; correct size assertion to 52 | Tech lead | Resolved in SPEC-11 Q11; measured by RenderContractTests |
| 52 | 2026-10-11 | SPEC-11 REN-001 | Public VoxelRenderRuntime.Managed references a class labelled internal | Public class, internal fields | Tech lead | Resolved in SPEC-11 Q12; preserves the specified runtime field |
| 53 | 2026-10-11 | SPEC-11 REN-001 | AABB is in an unreferenced assembly of the installed Entities package | Reference Unity.Mathematics.Extensions | Tech lead | Resolved in SPEC-11 Q13 by accepted ADR-0022; no package or module-boundary change |
| 54 | 2026-10-11 | SPEC-12 PHY-001 | Chunk-query prose conflicts with DebugAllQuery's exact filter | Gameplay queries exclude chunks except SpawnClearance; debug-all includes all bodies | Tech lead | Resolved in SPEC-12 Q10; exact filter code unchanged |
| 55 | 2026-10-11 | SPEC-12 PHY-001 | Initial capacities omitted for some pipeline containers | Cache 256; batch brick/child/apply 512, assemblies 64 | Tech lead | Resolved in SPEC-12 Q11; align related pools, retain dynamic growth |
| 56 | 2026-10-11 | SPEC-10 VOX-002 | Dense reference peak with retained mesh copies is 38,828,288 B, above 36 MB | No-go; VOX-003 todo, sparse-contract ADR proposed | Tech lead | Resolved: SPEC-10 Q16 and proposed ADR-0023; no gate relaxed |
| 57 | 2026-10-11 | SPEC-10 VOX-002 | Thin-shell peak is 162,606,784 B with both mesh copies; make it a hard gate? | Keep the documented stress-only default | Owner | Reported in SPEC-10 Q17; fallback estimates recorded, no new gate or visual policy assumed |
| 58 | 2026-10-11 | SPEC-12 PHY-002 | Installed collider bytes and PhysicsStep stabilization member differ from estimates | Use measured bytes and nested stabilization property; static broadphase field exists | Tech lead | Resolved in SPEC-12 Q12 / §4.8 / §5.13; no public game API change |
| 59 | 2026-10-11 | SPEC-12 PHY-007 | Discarded partial updates have no assembly to trigger freeing; invariant 2 omitted Discarded | Retain referenced slots until all batch results are consumed, then recycle | Tech lead | Resolved in SPEC-12 Q13 / §4.7 / §5.8; partial discard covered by the required finalization test |
| 60 | 2026-10-11 | SPEC-12 PHY-007 | Inner list capacities and PendingBricks semantics unspecified | 32 child entries / 8 update indices / 32 update bricks; PendingBricks = unscheduled entries | Tech lead | Resolved in SPEC-12 Q14; retain pool capacity and clear consumed mask slots |
| 61 | 2026-10-11 | SPEC-12 PHY-007 | Sorted insertion/audits reuse PHY-005's coordinate helper | Add the implementation dependency | Tech lead | Resolved in SPEC-12 Q15; PHY-005 was already done at claim time |

| 62 | 2026-10-11 | SPEC-10 VOX-003 | Sparse fallback lists callers that are stubs or absent | Add VOX-014 dependency; build dense foundations first | Tech lead | Resolved in SPEC-10 Q18; VOX-003 returned to todo, memory gate remains mandatory |

| 63 | 2026-10-11 | SPEC-10 VOX-012 | TryGetCell return value for air in an existing brick was ambiguous | Return true only for solid cells | Tech lead | Resolved in SPEC-10 Q19; Burst read coverage includes air and missing cells |

| 64 | 2026-10-11 | SPEC-10 VOX-010 / SPEC-01 | Boot table factory argument differs from declared definition parameter | Resolve config_voxel from catalog before calling factory | Tech lead | Resolved in SPEC-10 Q20 and SPEC-01 table; boot config is tagged before scene snapshot |

| 65 | 2026-10-11 | SPEC-10 VOX-006 | Compaction acceptance uses GetBlocks from dependent VOX-007 | Bring only GetBlocks forward | Tech lead | Resolved in SPEC-10 Q21; required equivalence uses real API, clone/diff/mass remain VOX-007 |

| 66 | 2026-10-11 | SPEC-10 VOX-017 | Benchmark brick counts contradict the specified geometry | Preserve geometry; correct asteroid to 408 bricks and Ship20k to 10 | Tech lead | Resolved in SPEC-10 Q22; asteroid 998,592 cells independently enumerated |
| 67 | 2026-10-11 | SPEC-10 VOX-017 / VOX-009 | Generator palette helper and codec benchmark dependencies were omitted | VOX-017 depends on VOX-006; VOX-009 depends on VOX-017 | Tech lead | Resolved in SPEC-10 Q23; no feature or acceptance scope removed |

| 68 | 2026-10-11 | SPEC-10 VOX-009 / SPEC-05 | VOXS repair collapses ordinary instance tombstones and shifts live indices | Preserve unreferenced block-zero slots before D3 repair | Tech lead | Resolved in SPEC-10 Q24 / SPEC-05 Q26; regression reproduced 3 slots collapsing to 1; no wire-format change |
| 69 | 2026-10-11 | SPEC-10 VOX-009 | Adapter omitted free-slot restoration and raw empty-palette normalization | Ascending free-slot reconstruction; reserved palette zero | Tech lead | Resolved in SPEC-10 Q25; deterministic reuse and usable decoded empty structures |
| 70 | 2026-10-11 | SPEC-10 VOX-009 | Temp sort allocation violates documented worker-safe Encode | Use a promptly disposed TempJob order array | Tech lead | Resolved in SPEC-10 Q26; managed-worker byte equality test |
| 71 | 2026-10-11 | SPEC-13 DMG-001 | Config and stream contracts name types from later tasks | Bring forward blob value types and declare opaque owner | Tech lead | Resolved in SPEC-13 Q18; implementation ownership stays DMG-002/003 |
| 72 | 2026-10-11 | SPEC-13 DMG-001 | Data definition cannot serialize a Voxel native table or derived hashes | Numeric tuning plus four cue references; derive runtime data | Tech lead | Resolved in SPEC-13 Q19; 43 matching numeric defaults |
| 73 | 2026-10-11 | SPEC-13 DMG-001 | DamageSetup helper bodies have no implementation task | Return the exact specified component lists now | Tech lead | Resolved in SPEC-13 Q20; no damage systems brought forward |
| 74 | 2026-10-11 | SPEC-10 VOX-013 | Unstow requires SplitGrace from DMG-001 | Add DMG-001 dependency before spawning | Tech lead | Resolved in SPEC-10 Q27; ready contract task taken first |
| 75 | 2026-10-11 | SPEC-10 VOX-021 / SPEC-11 REN-010 | Mesh queue implementation is behind a voxel dependency cycle | Extract specified queue methods/tests into ready VOX-021 | Tech lead | Resolved in SPEC-11 Q14 and SPEC-10 Q28; no contract change |
| 76 | 2026-10-11 | SPEC-11 queue / VOX-021 | Consumed/requeued brick can revive an older stale entry | Permanently invalidate the old entry on priority upgrade | Tech lead | Resolved in SPEC-11 Q15; required StaleSkipped test reproduces old FIFO/grid bug |
| 77 | 2026-10-11 | SPEC-10 VOX-013 | Double pinning and missing cancellation across released handles/scene exit | Registry-owned read tokens and explicit teardown cancellation | Tech lead | Accepted ADR-0024; SPEC-10 Q29 |
| 78 | 2026-10-11 | SPEC-10 VOX-013 / SPEC-03 INP-005 | Spawn ordering attribute would violate module references | Presentation input declares UpdateBefore(GridSpawnSystem) | Tech lead | Resolved SPEC-10 Q30 and SPEC-03 §5.2 |
| 79 | 2026-10-11 | SPEC-10 VOX-013 | ECB-only stow cannot inspect initialization or optional interpolation | Caller enforces precondition; Unstow ensures interpolation | Tech lead | Accepted ADR-0025; SPEC-10 Q31 |
| 80 | 2026-10-11 | SPEC-10 VOX-013 | Acceptance retained old 18-brick Ship20k count | Ten bricks and three functionals give fourteen LEG entries | Tech lead | Resolved SPEC-10 Q32, consistent with Q22 geometry |
| 81 | 2026-10-11 | SPEC-10 VOX-013 | Headless GPU cleanup component has no renderer to release it | Omit rendering cleanup on headless bricks | Tech lead | Resolved SPEC-10 Q33 |
| 82 | 2026-10-11 | SPEC-10 VOX-014 | CaptureChanges cannot distinguish empty from destroyed slots | Internal original-coordinate baseline, retained across saves | Tech lead | Accepted ADR-0026; SPEC-10 Q34; account for memory in VOX-003 |
| 83 | 2026-10-11 | SPEC-10 VOX-014 | Partial write-back risks deleting uncreated cells; rebuild overlaps an old spawn read | Terminal partial write-back and independent pending rebuild token | Tech lead | Accepted ADR-0026; SPEC-10 Q35 |
| 84 | 2026-10-11 | SPEC-10 VOX-014 | Global optimization and stale metadata mass cache violate incremental write-back | Optimize copied bricks only; invalidate metadata revisions | Tech lead | Resolved SPEC-10 Q36 |
| 85 | 2026-10-11 | SPEC-10 VOX-003 | Sparse fallback proposal lacks exact representation and source-page ownership | Accept ADR-0023 with six-byte sorted entries, flat fixed-capacity source pages and shared readers | Tech lead | Accepted after VOX-014; capacity slack and ADR-0026 baseline included in measurement |
| 86 | 2026-10-11 | SPEC-10 VOX-015 / SPEC-12 PHY-013 | Ready dirty scheduler orders against an absent mass system | Later mass system declares equivalent UpdateBefore | Tech lead | Resolved SPEC-10 Q38 / SPEC-12 Q16; final pipeline order unchanged |
| 87 | 2026-10-11 | SPEC-10 VOX-015 | Enableable IsEmpty synchronizes writers, contradicting the fixed-step no-sync rule | Structural IsEmptyIgnoreFilter early-out plus job enabled-mask filtering | Tech lead | Resolved SPEC-10 Q39 from installed EntityQueryImpl; all-disabled worlds may schedule an empty job |
| 88 | 2026-10-11 | SPEC-11 REN-004 | Long-slope half-cell geometry cannot use old integer whole-cell render coordinates | Keep byte layout; half-cell units decoded ×0.5 | Tech lead | Resolved SPEC-11 Q16 / accepted ADR-0027; five shapes, 120 orientations, exact geometry, unchanged bytes per vertex |
| 89 | 2026-10-11 | SPEC-11 REN-004 | Long-slope normal order/tangents unspecified | Pair, magnitude-swap, sign order and positive zero-axis tangent | Tech lead | Resolved SPEC-11 Q17; 42-entry C#/HLSL tables agree |
| 90 | 2026-10-11 | SPEC-04 DATA-012 / SPEC-11 | Approved long slope has no per-cell definition/blob support or implementation task | Canonical Shapes plus approved-kit validation and inherited angled-hull tuning | Tech lead | Resolved SPEC-04 Q14–15 / ADR-0028; no new shapes or visual assets |
| 91 | 2026-10-11 | SPEC-11 REN-005 | Missing orientation input and retained snapshot diagnostics | Canonical orientation table input and per-work SnapshotFlags | Tech lead | Resolved SPEC-11 Q18 / accepted ADR-0029; no duplicate rotation implementation |
| 92 | 2026-10-11 | SPEC-11 REN-006 | Legal painted long-slope brick needs at least 73,728 vertices | uint mesher indices; UInt16 upload when possible, UInt32 otherwise | Tech lead | Resolved SPEC-11 Q19 / SPEC-10 Q40 / accepted ADR-0030; reference-station GPU memory unchanged |
| 93 | 2026-10-11 | SPEC-11 REN-007 | LOD1 key zero conflicts with empty sentinel | Offset stored face keys by one and retain separate occupancy | Tech lead | Resolved SPEC-11 Q20; no public contract change |
| 94 | 2026-10-11 | SPEC-11 REN-002 | PlayMode RenderBounds requires an explicit package assembly reference | Add existing Unity.Mathematics.Extensions reference | Tech lead | Resolved SPEC-11 Q21 / ADR-0031; test-only dependency |
| 95 | 2026-10-11 | SPEC-11 REN-002 | Released meshes reject later writable-mesh apply | Default ReleaseCpuMeshCopies to false | Tech lead | Resolved SPEC-11 Q22; preserve mutable-mesh correctness and retained-copy memory accounting |
| 96 | 2026-10-11 | SPEC-12 PHY-010 | Mesh ordering names a system not yet implemented | Use REN-010's existing reverse ordering declaration | Tech lead | Resolved SPEC-12 Q17; same mesh-before-collider edge |
| 97 | 2026-10-11 | SPEC-12 PHY-010 | Partial predecessors must not allow newer assemblies to overtake | Require full assembly selection and revisit newly eligible candidates | Tech lead | Resolved SPEC-12 Q18; preserves Built-prefix and urgent priority rules |
| 98 | 2026-10-11 | SPEC-12 PHY-011 | Empty update retains installed geometry but older pending compounds must be retired | Preserve installed owner and clear logical children while freeing prefix | Tech lead | Resolved SPEC-12 Q19; no pending compound leak or stale child count |
| 99 | 2026-10-11 | SPEC-12 PHY-012 | Cleanup uses configuration without requiring that singleton | Optional config with default two-frame age limit | Tech lead | Resolved SPEC-12 Q20; cleanup survives config/gameplay teardown |
| 100 | 2026-10-11 | SPEC-12 PHY-012 | Previous-frame queue tick can shorten retirement lifetime after catch-up | Stamp the local retirement view with the current SimClock tick | Tech lead | Resolved SPEC-12 Q21; preserves two post-retirement ticks even when physics pauses |
| 101 | 2026-10-11 | SPEC-12 PHY-008 | IsEmpty synchronizes enableable query writers | Asynchronous filtering with structural-empty fast path | Tech lead | Resolved SPEC-12 Q22; no fixed-step query sync |
| 102 | 2026-10-11 | SPEC-12 PHY-008 | Rebuild can invalidate updates referenced by an unfinished batch | Detach/discard referenced slots and retire results at finalization | Tech lead | Resolved SPEC-12 Q23; cache guard protects immutable descriptor reads and later reuse |
| 103 | 2026-10-11 | SPEC-12 PHY-019 | Live overlap peaks at 3,275,424 B, above the original 2.5 MiB collider allocation | Allocate 3.5 MiB within unchanged 36/40 MB whole-voxel limits | Tech lead | Resolved SPEC-12 Q24 / accepted ADR-0033: four versions coexist; preserve install/retirement delays. Corrected reference model 32,227,328 B; TST-004 still owns integration |
| 104 | 2026-10-11 | SPEC-01 FND-008 | Allocation round trip precedes full GameSession/SESS implementation | Test exact UInt64 counter field; stage allocation-only session members | Tech lead | Resolved SPEC-01 Q11: FND-009 owns complete SESS/composition; restore clears outstanding grants |
| 105 | 2026-10-11 | SPEC-01 FND-008 | GetSingletonRW does not complete the prior Burst ID consumer | Explicit component-specific wait before refill/reset | Tech lead | Resolved SPEC-01 Q12: CompleteDependencyBeforeRW in initialization, exercised by outstanding consumer jobs |
| 106 | 2026-10-11 | SPEC-01 FND-008 | Valid zero refill threshold otherwise leaves the Burst pool empty forever | Empty blocks always refill; zero means no early top-up | Tech lead | Resolved SPEC-01 Q13; zero-threshold activation covered in NoSession_Empty |
| 107 | 2026-10-11 | SPEC-01/05 FND-009 | Direct autosaves miss session writebacks | ISaveContext.PrepareCapture before participant capture | Tech lead | Accepted ADR-0034 / SPEC-01 Q14 / SPEC-05 Q27 |
| 108 | 2026-10-11 | SPEC-01/05 FND-009 | Async slot readback cannot fit completed-only thumbnails | Pending DTO state; wait ≤ 2 frames; producer owns GPU cleanup | Tech lead | Accepted ADR-0034 / SPEC-01 Q15; no-camera/headless optional image omitted |
| 109 | 2026-10-11 | SPEC-01 FND-009 | Transitioning blocks its own exit save; false quit flag still saved | Reserve flow and save before Exiting; honor saveFirst=false | Tech lead | SPEC-01 Q16 / ADR-0034; writeback failures join recovery |
| 110 | 2026-10-11 | SPEC-01 FND-009 | Future galaxy/fleet/combat/camera contracts absent at current dependency stage | Keep task-owned integration points and configured SESS defaults | Tech lead | SPEC-01 Q17: GAL-008, FND-010, AI-008 and CAM-004 wiring remains with those tasks |
| 111 | 2026-10-11 | SPEC-01 FND-009 | New game can run without a selected profile | Create configured-default profile only when none is selected | Tech lead | SPEC-01 Q18; existing active profile and redirected save roots preserved |
| 112 | 2026-10-11 | SPEC-20 FLT-001 | Contract size/file-list and player-tag comments disagree with field/profile contracts | Keep fields; correct comments; include PropulsionStats | Tech lead | SPEC-20 Q9: 44-byte FlightThruster; ADR-0015 profile owns PlayerShip; no layout change. FLT-006 lists its required FlightSetup implementation and corrects the remaining memory-summary comment |
| 113 | 2026-10-11 | SPEC-12 PHY-023 | Background batch completion can miss an urgent install during six-tick catch-up | Finish deadline-critical work outside the fixed step | Tech lead | Accepted ADR-0035 / SPEC-12 Q25: next-frame deadline completion in the variable-rate builder; PHY-023 verified: delayed batch and strict determinism pass; full suites green |
| 114 | 2026-10-11 | SPEC-01 FND-010 | M1 player spawning references M2/M3 services and record handles lack an owner | Direct M1 grid request; record-bound registry borrow | Tech lead | SPEC-01 Q19–20: one WriteBack/PlayerShip request at origin; release handle on exit/dispose; no-session diagnostic entries skip player spawning |
| 115 | 2026-10-11 | SPEC-01 FND-010 / SPEC-05 | Recovered fleet records cannot warn through the active load report | Scoped restore-warning callback | Tech lead | ADR-0036, SPEC-01 Q21 / SPEC-05 Q28; no ISaveParticipant or binary-format change; missing fleet uses a new allocated id, corrupt flagship payload preserves its valid id |
| 116 | 2026-10-11 | SPEC-12 PHY-024 / FND-010 | Loading pause prevents the fixed tick needed for player collider readiness | Fresh spawn body initialization in GameInitGroup | Tech lead | SPEC-12 Q26 / ADR-0037; fresh roots only, end-init ECB; split adoption remains fixed-step |
| 117 | 2026-10-11 | SPEC-01 FND-010 | Partial save recovery can reuse an existing fleet id when SESS is missing, damaged or stale-low | Reserve through the highest restored fleet id | Tech lead | SPEC-01 Q22: composition callback advances only when necessary, clears grants and reports FND-W002; preserves higher counters and existing ship identities |
| 118 | 2026-10-11 | SPEC-01 FND-016 | Sandbox reset can destroy a live fleet before old World writeback; optional player defaults unspecified | Reset after teardown using the session operation reservation | Tech lead | SPEC-01 Q23: creative deterministic sandbox, no campaign initializers, optional live grid only, normal new-game/load resets flags; galaxy integration remains GAL-008 and harness dispatch DEV-008 |
| 119 | 2026-10-11 | SPEC-20 FLT-004 | General equal-throttle symmetry contradicts fixed-order PGS with a redundant gyro; stale bounds can invalidate warm starts | Preserve the specified solver and project initial bounds | Tech lead | SPEC-20 Q10–12: analytic symmetric fixture plus redundant counterexample; clamp warm starts; double dot-product accumulation fixes observed managed/Burst ULP drift; no new allocation objective |
| 120 | 2026-10-11 | SPEC-20 FLT-005 | No-player counter reset incorrectly consumes Cruise despite its travel ownership | Preserve Cruise while dropping pending ship edges | Tech lead | SPEC-20 Q13: all live/hulk/no-player paths leave Cruise untouched; nine ship counters retain specified per-tick/drop behavior |
| 121 | 2026-10-11 | SPEC-20 FLT-007 | Propulsion's required data omits module rows; half-gain braking has an exponential final approach | Require BlockModulesRef and distinguish saturated stopping time from assist convergence | Tech lead | SPEC-20 Q14–15: existing module API; unit-gain analytical stopping fixture plus default-gain convergence; physical impulse algorithm unchanged |
| 122 | 2026-10-11 | SPEC-20 FLT-008 | Per-job diagnostic flag is copied across parallel workers and repeats every tick | Atomic once-per-system warning latch | Tech lead | SPEC-20 Q16: four owned native bytes; one warning, no transform correction or per-tick synchronization |
| 123 | 2026-10-11 | SPEC-20 FLT-010 | Shipped tuning invalidates initial-default asset equality; measurement definitions unspecified | Preserve fallback defaults and measure configured content separately | Tech lead | SPEC-20 Q17: configured factory/boot equality, 0.25 s acceleration, 99% cap, 10 s turn rate, first 90° crossing, explicit 120 s/non-thrust limits |
| 124 | 2026-10-11 | SPEC-20 FLT-010 | Legacy acceleration targets cannot be achieved by straightforward shared-row thrust scaling | Record the shortfall; retain content rows and avoid inventing per-ship overrides | Tech lead | Resolved SPEC-20 Q18: all 13 blueprints are Small; one medium on 20,745 kg Red Elite needs ≥165,960 N for 8 m/s², while two mediums on 2,070 kg Striker need ≤20,700 N each for 20 m/s². Tuning evidence/proposal is complete; content balance and owner sign-off remain separate |
| 125 | 2026-10-11 | SPEC-20 FLT-010 | Approve provisional yaw tuning and the legacy balance follow-up | Option 1 below: 180°/s², 2.5 MN·m core gyro; retain thrust rows pending blueprint balance | Owner | **Pending owner sign-off**, requested here per autonomous-run instructions; Measurements gives three options. All 13 t90 values ≤1.2 s; powered acceleration 1.928–38.647 m/s² misses 8–20 on seven ships; ship_base has no thrusters (Q40). Medium/large classes unvalidated |
| 126 | 2026-10-11 | SPEC-62 CAM-001 | Deferring IFxFrameReader creates a CAM-004/CAM-008 dependency cycle | Empty interface now, typed event property at CAM-008 | Tech lead | Resolved SPEC-62 Q9; preserve Bind type without inventing VFX contracts |
| 127 | 2026-10-11 | SPEC-62 CAM-004 | Camera prefab listener contradicts sole AudioRoot listener | Retain audio ownership, bind rig pivot | Tech lead | Resolved SPEC-62 Q10 / ADR-0039; new read-only ListenerPivot contract; no duplicate listener |
| 128 | 2026-10-11 | SPEC-62 CAM-004 | Camera boot wiring precedes input/FX adapters and ordering peers | Bind available services, explicitly defer adapters | Tech lead | Resolved SPEC-62 Q11 / ADR-0039: null input/FX staging; FollowHeading; sensitivity clamp; existing VoxelViewer writer; Lab Hold until CAM-009 |
| 129 | 2026-10-11 | SPEC-62 CAM-005 | Input/rig specs apply orbit settings twice and duplicate adapter ownership | Raw pixels, one Input-namespace adapter, settings in rig | Tech lead | Resolved SPEC-62 Q12 / SPEC-03 Q7; CAM-005 Flight branch, INP-006 Editor branch/tests, zoom sensitivity once |
| 130 | 2026-10-11 | SPEC-62 CAM-005 | Endpoint damping misses moving-target FPS tolerance by 0.767 m | Exact exponential cascade with linear target motion | Tech lead | Resolved SPEC-62 Q13; original 30/60/144 X: 232.0124/231.2455/230.7731 m. Test/tolerance unchanged; equal-rate and small-step limits; task tier frontier |
| 131 | 2026-10-11 | SPEC-62 CAM-011 | M1 camera gate mentions M3 raycast/origin paths | Enforce no-occlusion 0.05 ms and zero GC now | Tech lead | Resolved SPEC-62 Q14; CAM-007 owns real-raycast timing ≤0.15 ms, CAM-006 owns origin accuracy; no stub timing accepted |
| 132 | 2026-10-11 | SPEC-11 REN-003 | Fallback needs missing shared surface shader; apply/cleanup systems are future work | REN-008 first; future REN-010/011 consume pooled contract | Tech lead | Resolved SPEC-11 Q23; added shader dependency and returned REN-003 to todo; no contract or gate success asserted |
| 133 | 2026-10-11 | SPEC-11 REN-008 | Old shader property/channel contract conflicts with approved design; legacy texture forbidden | Canonical art contract, procedural inputs | Tech lead | Accepted ADR-0040 / SPEC-11 Q24; preserve packed vertex and defer missing presentation writers to their features; no new art direction |
| 134 | 2026-10-11 | SPEC-11 REN-003 | Fallback allocation, memory growth and integration contracts were unspecified | Exact packed ranges, generational slots, GPU copy, explicit camera and future pooled apply/cleanup | Tech lead | Accepted ADR-0032 / SPEC-11 Q25; same 36 MB and timing gates; performance remains to be measured |
| 135 | 2026-10-11 | SPEC-11 REN-003 | Metal indirect draw and growth-copy behaviour differ from the initial assumptions | Separate CPU upload staging, GPU-only pool writes and explicit per-slot vertex offsets | Tech lead | ADR-0032 / SPEC-11 Q26: GPU readback isolates lost old indices and omitted base vertex; repaired path under validation |
| 136 | 2026-10-11 | SPEC-11 REN-003 | One indirect command per brick misses the Metal GPU gate | Coalesce selected index intervals; global GPU indices and per-vertex owners | Tech lead | Accepted ADR-0041 / SPEC-11 Q27; diagnostic GPU 22.000125 ms over 60 samples, final 300-sample gates unchanged |
| 137 | 2026-10-12 | SPEC-11 REN-003 | Metal frame timings include idle phase gaps; recorder lacks full thread timing | Diagnostic native execution union for all frame buffers; retain elapsed latency and raw render-thread roots | Tech lead | SPEC-11 Q28 with Unity's documented Metal timing semantics; unchanged 6 ms work limits, ≥300 complete frames. Diagnostic execution 5.714083 ms / elapsed 6.335625 ms; fresh validation unavailable (zero complete frames). Deferred under ADR-0042; no performance pass claimed. Owner should review this explicit measurement interpretation. |


| 138 | 2026-10-12 | SPEC-11 REN-009 / SPEC-01 boot | Render configuration lacks explicit prop-mesh input/disposal and old palette text conflicts with approved look | Boot-owned disposable factory result; approved REN-008 NOCTURNE palette | Tech lead | Resolved SPEC-11 Q29: internal boot signature supplies PropMeshes and retains cleanup in the composition stack; shared source assets remain unchanged. |
| 139 | 2026-10-12 | SPEC-11 REN-010 | EditMode runtime fixture requires AABB assembly reference | Direct test-only Unity.Mathematics.Extensions reference | Tech lead | Accepted ADR-0043 / SPEC-11 Q30, same installed package; runtime boundaries unchanged. |
| 140 | 2026-10-12 | SPEC-11 REN-011 | Cleanup/query timing and runtime palette material ownership | Exact deletion capture before drawing; shared runtime material copies; explicit camera bridge | Tech lead | Resolved SPEC-11 Q31 within ADR-0032; no per-brick materials or meshes. |
| 141 | 2026-10-12 | SPEC-03 INP-005 | UI gate and origin system are future work; physics singleton read does not wait | Null gate allows pointer until UI-003; GAL-009 precedes sampling; explicit query dependency completion | Tech lead | Resolved SPEC-03 Q8; Flight-disabled/unbound remains neutral, scene/pause context hookup belongs to FND-017. |
| 142 | 2026-10-12 | SPEC-01 FND-017 / SPEC-03 / SPEC-60 | Flight context waits for unfinished UI; DEV-008 waits for Flight | Temporary sole scene/pause bridge before sampling; UI-004 removes it | Tech lead | Resolved SPEC-01 Q24 / accepted ADR-0044. Existing Fighter starter and approved camera-relative lighting; FLET does not yet persist pose/velocity. |
| 143 | 2026-10-12 | SPEC-01 FND-017 | Real World restart leaves PhysicsTemporalCoherenceInfo after paused teardown | Drain existing physics initialization and end-fixed ECB at zero delta | Tech lead | Resolved SPEC-01 Q25 / accepted ADR-0045; package retains ownership; no gameplay tick. |
| 144 | 2026-10-12 | SPEC-70 DEV-008 | Start-scenario argument has no content or consumer | Explicitly reject nonempty selection | Tech lead | Resolved SPEC-70 Q17: empty uses the configured imported starter; no silent ignore or blueprint reinterpretation. |
| 145 | 2026-10-12 | SPEC-70 DEV-008 | Edit-mode launch needs a real runtime operation ID | Async boot then registry dispatch | Tech lead | Resolved SPEC-70 Q18: wait for settled MainMenu and host; set intro hook before owned boot, restore after dispatch. |
| 146 | 2026-10-12 | SPEC-70 DEV-008 | Sandbox exit may autosave an active player profile | Require harness for active sessions | Tech lead | Resolved SPEC-70 Q19: select/create harness only without a session; reject active other profiles and busy flow/save before profile changes. |
| 147 | 2026-10-12 | SPEC-60 UI-002 | Theme literal audit includes SPEC-80 direction token sources | Audit all UI-owned styles, exempt token inputs | Tech lead | Resolved SPEC-60 Q11: Directions/ is an authored token source alongside tokens.uss; component/screen styles stay covered. |
| 148 | 2026-10-12 | SPEC-60 UI-003 | Root precedes navigator and later UI services | Stage registration/build and real root lifecycle only | Tech lead | Resolved SPEC-60 Q12: keep flight context and Escape fallback until UI-004; defer pointer/focus, converters and service ticks to their owning tasks. |
| 149 | 2026-10-12 | SPEC-60 UI-003 | Input bridge requires package callback types | Reference existing Unity.InputSystem | Tech lead | Resolved SPEC-60 Q13 / accepted ADR-0046; no package or Game.* boundary change. |
| 150 | 2026-10-12 | SPEC-60 UI-003 | UI/audio sound enum ordinals differ | Named mapping and configured cue overrides | Tech lead | Resolved SPEC-60 Q14: Confirm maps to audio Success, no enum cast; audio playback/content remains its own task. |
| 151 | 2026-10-12 | SPEC-60 UI-004 | Deferred Back/Pop, callback loops and failed push focus | Bounded FIFO, accepted queued Pop, successful focus only | Tech lead | Resolved SPEC-60 Q15 / ADR-0047; postconditions recomputed after partial failures. |
| 152 | 2026-10-12 | SPEC-60 UI-004 / HUD-002 | M3 scene roots are absent during navigator handover | Temporary roots and App navigation adapter | Tech lead | Resolved SPEC-60 Q16 / ADR-0047: remove direct input writer, retain flight/pause via navigator; HUD-002 replaces adapter/views. |
| 153 | 2026-10-12 | SPEC-60 UI-005 | Screen.height is Editor-window sized inside ECS input sampling | Flip Y in the actual panel space | Tech lead | Resolved SPEC-60 Q17: live HUD click leaked into fire; use panel height after ScreenToPanel, and test against independent viewport dimensions. |
| 154 | 2026-10-12 | SPEC-62 CAM-012 | Distance-only look-ahead does not keep the hull framed | Clip output focus using solid bounds and camera frustum | Tech lead | Resolved SPEC-62 Q15: preserve filter state and requested zoom/orbit; center deliberate close inspection; internal bounds handoff, no public contract change. |
| 155 | 2026-10-12 | SPEC-70 DEV-009 | Contract-only Ready omits absent ship-spawn and universe services | Keep full scope blocked until canonical prerequisites exist | Tech lead | Resolved SPEC-70 Q20: FND-012 plus SPEC-30/31 coordinates/current-system state required; no alternate dev spawner or fabricated metadata. Continue WPN-001/AI-001 and other ready work. |
| 156 | 2026-10-12 | SPEC-21 WPN-001 | Contract staging cycle and stale task/type annotations | Canonical SpatialHash shell; include existing stats/shot types | Tech lead | Resolved SPEC-21 Q11: AI-001 fills its shell, VFX-001 fills FxDefinition; WeaponShot is 80 B by existing fields, ProjectileRow stays 64 B; no API or runtime behavior change. |
| 157 | 2026-10-12 | SPEC-23 AI-001 | Missing ability reference and proxy layout/staging details | Canonical ability shell and explicit 64 B layout | Tech lead | Resolved SPEC-23 Q6: ABL-001 fills AbilityDefinition; include prescribed ability-rule types, read-only content lists and config validation signature; preserve proxy fields with trailing padding. |
| 158 | 2026-10-12 | SPEC-60 UI-006 | Clock range and cache lifetime unspecified | Explicit duration formatting and immutable private caches | Tech lead | Resolved SPEC-60 Q18: floor seconds, invalid/negative zero, expand long minutes, range-check Get2Digits; register converters through the existing UIRoot hook. |
| 159 | 2026-10-12 | SPEC-01 FND-018 | Spatial cues requested before celestial streaming | Procedural distant placeholder and stationary local particles | Tech lead | Resolved SPEC-01 Q26: owner explicitly corrected the initial bake. Planet must be a physical finite sphere with surface/atmosphere, separate from the nebula/star sky. A subsequent request requires that sky to be procedural and animated; no sky texture remains. Owner retained the first procedural rendering brightness/density: “no don't reduce its good” (DESIGN-GUIDE decision 7). SYS-011 replaces staged cosmetics; no celestial persistence. |
| 160 | 2026-10-12 | SPEC-60 UI-007 | KeyHint unset-path/null resolver policy | Empty display until configured, reject missing resolver | Tech lead | Resolved SPEC-60 Q19; imports added through Game.tss. Contrast audit remains UI-012, as specified. |
| 161 | 2026-10-12 | SPEC-03 INP-006 / SPEC-51 | Editor sampler violates module boundary and needs undeclared types | App host, stage canonical frame/actions | Tech lead | Resolved SPEC-03 Q9 / SPEC-51 Q22 by accepted ADR-0048; no assembly changes. EDT-024 composes the existing sampler. |
| 162 | 2026-10-12 | SPEC-11 REN-012 | LOD restore skipped for invalid viewer or late async apply | Always enqueue; filter desired OR applied state | Tech lead | Resolved SPEC-11 Q32; no new public contract |
| 163 | 2026-10-12 | SPEC-11 REN-012 | Inclusive grid bounds omit volume extent/scale in distance sphere | CellMax + 1; transform centre and scale radius | Tech lead | Resolved SPEC-11 Q33; exact occupied cell volume |
| 164 | 2026-10-12 | SPEC-01 FND-019 | Direct restart borrows a camera Skybox pending deferred destruction | Bind after one frame, preserving scene ownership | Tech lead | Resolved SPEC-01 Q27; failing direct-restart regression proves the lifetime race |
| 165 | 2026-10-12 | SPEC-11 REN-014 | Gather-only prop draw bounds go stale as ships move | Recompute world union from per-grid local bounds with matrix uploads | Tech lead | Resolved SPEC-11 Q34; no public contract or art change |
| 166 | 2026-10-12 | SPEC-64 AUD-005 | Managed 2D playback precedes SFX allocator/music director | Use existing pools and victim rules; preserve base music gains; defer spatial path | Tech lead | Resolved SPEC-64 Q15; AUD-006 centralizes allocation and AUD-008 supplies crossfade gains |
| 167 | 2026-10-12 | SPEC-70 DEV-015 / SPEC-60 | Code-built console cannot use UXML-only root; teleport not implemented | Explicit CreateView fallback; registry-only completion | Tech lead | Accepted ADR-0049; SPEC-70 Q21 / SPEC-60 Q20 preserve canonical navigation and command scope |
| 168 | 2026-10-12 | SPEC-63 VFX-001 | FxKind would create Data→Voxel cycle | Shared enum in Game.Data.Definitions | Tech lead | Resolved ADR-0050 / SPEC-63 Q16: unchanged values, no new assembly reference. |
| 169 | 2026-10-12 | SPEC-63 VFX-001 | native queue lacks owned producer handles | GuardedHandles on FxEventQueue | Tech lead | Resolved ADR-0050 / SPEC-63 Q17: shared atomic enqueue, exclusive drain, guarded disposal; frontier scope. AUD-006 reconciles the existing SFX queue before asynchronous producers. |
| 170 | 2026-10-12 | SPEC-63 VFX-001 | obsolete validation API and unstated diagnostic codes | Canonical ValidationScope/context | Tech lead | Resolved ADR-0050 / SPEC-63 Q18: stable VFX codes, asset/field diagnostics, strict defaults until VFX-004 staged content relaxation. |
| 171 | 2026-10-12 | SPEC-71 TST-002 | Replay override absent from architecture; endpoints/metadata unclear | Explicit dev hooks and frame-stage replay contract | Tech lead | Resolved ADR-0051 / SPEC-71 Q8: sampler → replay → recorder, transient FrameIndex, defined script bounds, even-tick 30/60/144 guarantee only. |
| 172 | 2026-10-12 | SPEC-71 TST-003 | Fixture pseudo-API and scene-only boot do not match production lifecycle | Existing bootstrap/session APIs; isolated temp paths | Tech lead | Resolved SPEC-71 Q9: Common test-only friend access, explicit Bootstrap after Boot load, canonical new-game/sandbox calls; headless-only renderer readiness bypass. SPEC-01/05 boot path hooks already exist from FND-005; intro has no splash until HUD-003. |
| 173 | 2026-10-12 | SPEC-11 REN-013 | Gate wording predates pooled rendering; no screenshot baseline | Measure current renderer and inspect initial Unity reference | Tech lead | Resolved SPEC-11 Q35: include pooled submission in moving delta and all REN systems in idle share, use actual spawn/mesh/apply and fixed 1920×1080 capture. No renderer optimization or automatic baseline replacement. |



| 174 | 2026-10-12 | SPEC-71 TST-004 | Whole-voxel memory inspection cannot establish complete native/engine overhead | Report owned capacity and leave total peak unavailable | Tech lead | Resolved SPEC-71 Q10: real integrated mesh/collider readiness and inclusive updating-system markers; separately inspect owned capacity through damage overlap after source eviction. Below-budget accounting is inconclusive for total memory. Advisory misses/unavailable total are deferred, zero tuning. |

| 175 | 2026-10-12 | SPEC-70 DEV-016 | Absolute recording ticks and absent universe position prevent literal M1 replay acceptance | Shift the recording clock; compare real local flight position | Tech lead | Resolved SPEC-70 Q22: existing cmdrec text in `.pcr`, bounded stems, session-bound playback. Test real seed-7 Fighter input/physics for 300 exact ticks; extend local-position comparison to canonical UniversePosition when SPEC-30/31 exists. No fabricated coordinates or alternate spawner. |

| 176 | 2026-10-12 | SPEC-70 DEV-007 | Coroutine parameterization and unimplemented draft vocabulary | Native UnityTest ValueSource; header-only draft inspection | Tech lead | Resolved SPEC-70 Q23: one sorted case per smoke/regression file, drafts ignored before resolving steps, active cases use strict parser/real runner and the specified frame bound. |
| 177 | 2026-10-12 | SPEC-70 DEV-019 | Future editor input cannot yet be exercised | Keep explicit draft activation prerequisites | Tech lead | Resolved SPEC-70 Q24: provisional viewport clicks, refund text, changed paint colour and Ark leave option must be bound by owning systems before activation. Parse-only fixtures expose only declared future vocabulary; no production stubs. R30 verifies return target identity and both entry rings. |
| 178 | 2026-10-12 | SPEC-05 PERS-019 | Four native Fleet snapshots leaked on Play exit while quit-save awaited thumbnails | Track ownership until worker claims the capture | Tech lead | Resolved SPEC-05 Q29: synchronous waiting-snapshot disposal, exclusive worker handoff, no post-disposal completion/quit callback. Tracked stacks in Logs/playtest/m1-review/tracked-editor.log identify all four allocations. |
| 179 | 2026-10-12 | SPEC-13 DMG-002 | Tick stats reset races pending job writes; internal ownership details | Dependent reset job and explicit phase fences | Tech lead | Resolved SPEC-13 Q21: no fixed-step completion; stats reset depends on previous phase/readers, later writers chain it, and container copies preserve detach swaps. Main-thread stats reads complete publication outside fixed simulation. CellMask uses 64 inline ulongs. |
| 180 | 2026-10-12 | SPEC-13 DMG-002 | Shared stale-stream warning fails inside Burst owner (BC1016) | Build identical warning from unmanaged characters | Tech lead | Resolved SPEC-13 Q22: preserve existing logging and lifetime behavior; eliminate managed-string conversion. Full suite verifies actual Burst compilation, which focused asynchronous tests missed. |
| 181 | 2026-10-12 | SPEC-13 DMG-003 | Catalog-wide validation and boot damage-table ownership | Internal validation catalog; replace fallback config and own blob at boot | Tech lead | Resolved SPEC-13 Q23: definitions determine contiguous table dimensions; E005/E006 cover config values/ranges. Boot disposal completes jobs, resets the surviving singleton and releases its table. Four authored types plus a test-only fifth value; no production enum or existing public API change. |
| 182 | 2026-10-12 | SPEC-20 FLT-013 / SPEC-03 | Owner flight-control correction and shooting priority | ADR-0052; V2 implementation, physical directional thrust | Tech lead | Resolved SPEC-20 Q20: Shift strafes, Ctrl boosts, gentle thruster-limited coasting, capacity-bounded assist; prove actual Fighter controls. Stationary-target shooting is the next M2 checkpoint. Frank: “dont copy v1, PLEASE, just make your own version with our new architecture”. |
| 183 | 2026-10-12 | SPEC-13 DMG-004 | Cell-writer outputs, dirty urgency and ray reference resolution | Existing collider priority; per-grid scratch; stratified samples | Tech lead | Resolved SPEC-13 Q24: no new dirty bit; collider scheduler already marks runtime damage urgent. Preserve same-bin HP dirty state. Reference sampling includes each positive-length cell interval; no dropped thin crossings. |
| 184 | 2026-10-12 | SPEC-01 FND-020 / SPEC-13 / SPEC-21 / SPEC-63 | Owner requests bigger stationary target, firing and visible physical breakup now | ADR-0053 integrated production slice | Tech lead | Resolved SPEC-01 Q28: “Get that playable and show me it working.” Stage the existing systems with deterministic simple internals, real cells and physical hulk pieces; keep broader tasks open. Frank superseded the imported target: “dont use vestari, make a test station we can shoot, that has various possible combination of blocks and structures so we can test everything like breaking off, etc”. Use existing blocks in a procedural station with single/redundant bridges, layered hull, pods and functional/reactor sections. Frank also directed: “spacebar should be shooting”; Space and left-click fire primary, G remains tertiary. No copied V1 code or new hull/art gate. |
| 185 | 2026-10-12 | SPEC-10 / SPEC-13 | Split job cannot use GridPrototypes lookup because its native maps become nested containers | Separate plain GridSerialAllocator component, ADR-0054 | Tech lead | Resolved SPEC-10 Q41: both canonical root creators allocate from one ECS-tracked uint component. Brick prototype is passed by value; no unsafe bypass or fixed-step completion. |
| 186 | 2026-10-12 | SPEC-12 Q27 / SPEC-13 Q25 | Immediate post-split restart can precede collider adoption and retain pending combat events | Cleanup ownership and stop-lifecycle drain, ADR-0055 | Tech lead | Resolved: unadopted provisional blobs retain a cleanup owner, adopted blobs remain cache-owned; stopping gameplay clears cross-session pending channels and detach state. |
| 187 | 2026-10-12 | SPEC-02 Q16 | Event streams may await the next simulation tick beyond TempJob frame lifetime | Persistent stream allocation, ADR-0056 | Tech lead | Resolved: retain explicit channel disposal/fences; no event-order or API change and no fixed-step sync point. |
| 188 | 2026-10-12 | SPEC-13 Q26 / architecture §11.1 | Bound the staged full-grid integrity pass | 16384 occupied cells; queued seeds and once-per-grid warning | Tech lead | Resolved ADR-0057: DamageConfig.MaxFullFloodCells guards before flood allocation; IntegritySizeDeferred counts skips. DMG-008/009 own the replacement. |
| 189 | 2026-10-12 | SPEC-13 Q27 / DMG-005 | Blast occlusion test geometry and warning scope | Preserve 400-damage symmetry; use 1600 damage to reach shielded cells; warn once per resolver lifetime using native state | Tech lead | Resolved DMG-005; no owner gate |
| 190 | 2026-10-12 | SPEC-13 Q28 / DMG-006 | Deferred range outputs, hasher dependency and repair scope | Per-range native streams; canonical serial explosion publication; bring forward CellStateHasher; repair pass remains DMG-019 | Tech lead | Resolved; no public API change, fixed-step completion or owner gate |
| 191 | 2026-10-12 | SPEC-13 Q29 / SPEC-63 Q19 | Missing ram effect kind and opposite Unity collision normal convention | Append RamImpact = 13, 14-entry defaults; convert B-to-A normal at collection | Tech lead | Resolved ADR-0058; no new art, owner gate or enum renumbering |
| 192 | 2026-10-12 | SPEC-13 Q30 | Deterministic brick warm-up remainder and staged integration | Earliest sorted candidates get remainder; touched bricks outside quota | Tech lead | Resolved DMG-008; summaries precede full-flood size guard; DMG-009 retains graph traversal scope |
| 193 | 2026-10-12 | SPEC-13 Q31 | Capped seed prefix can miss the true anchor; staged cap retirement | Keep all seeds, defer overflow then force complete set; hybrid replaces full flood | Tech lead | Resolved DMG-009; canonical stream publication and internal node-count evidence; legacy cap fields preserved but inactive |
| 194 | 2026-10-12 | SPEC-13 Q32 | Deferred disconnected island reseeding and staged split presentation | Retain island plus anchor representatives; preserve hulk path; canonical Debris kind | Tech lead | Resolved DMG-010; chunks wait for DMG-011, visible FX stays VFX-003/004; no new owner gate |
| 195 | 2026-10-12 | SPEC-63 Q20 | Rig binding before consumer systems exist and palette double-conversion ambiguity | Internal consumer hook; session registry reset; linear palette unpacked without a second gamma conversion | Tech lead | Resolved VFX-002; actual VFX-004 content budget remains to measure when authored; no owner gate |
| 196 | 2026-10-12 | SPEC-63 Q21 | Upload counting before slot acceptance, lifetime-ring overwrite, palette index zero | Count accepted submissions; conservatively merge live buckets; flag selects palette including zero | Tech lead | Resolved VFX-003; internal fake-rig seam and combined hard cap documented; no owner gate |
| 197 | 2026-10-12 | SPEC-63 Q22 / SPEC-21 Q12 | Hit flash particle-count ambiguity and missing live FX producer hooks | Shared flash output; world contact normal and guarded queue producers | Tech lead | Resolved VFX-004 / ADR-0059; staged weapon criteria stay open, no owner gate |
| 198 | 2026-10-12 | SPEC-63 Q23 / VFX-015 | Owner requests all remaining effects in one batch and triple-size station | Final-only validation; canonical missing input contracts brought forward | Tech lead | Resolved ADR-0060; owning gameplay systems stay incomplete; no owner gate |
| 199 | 2026-10-13 | SPEC-63 Q24 / VFX-015 | Production graph validation, installed Unity buffer contract and approved effect palette | Rendered origin invariance plus submitted GPU readback; canonical buffer/channel wiring; design guide colours | Tech lead | Resolved ADR-0060; direct simulated-particle readback not claimed; unavailable timing remains visible |
| 200 | 2026-10-13 | SPEC-63 Q25 / VFX-015 | GPU validation found overwritten fixed-step event batches, suppressed pooled starts and a beam producer dependency | Per-frame graph simulation, Reinit/custom-event restart and targeted writer completion | Tech lead | Resolved ADR-0060; 50k actual debris and live warp verified; correctness fixes, no optimization pass |

| 201 | 2026-10-13 | SPEC-01 Q29 / FND-021 | Owner wants automatic World test station with hull crossing the firing plane | Production offset Y=-2 m and reversible AutoSpawnInWorld setting | Tech lead | Resolved ADR-0061; one scene-owned station after player readiness; range command idempotent |
| 202 | 2026-10-13 | SPEC-22 Q10 / PWR-001 | PowerDemandGroup references allocator before PWR-005 exists | Defer redundant UpdateBefore attribute; retain PWR-005 TODO and reciprocal ordering requirement | Tech lead | Resolved; no allocator stub or changed runtime ordering |
| 203 | 2026-10-13 | SPEC-21 Q13 / WPN-017 | Presentation ordering and stale HulkInfo field | Existing OrderLast drain orders bridge without upward reference; use FormerKind and complete attribution dependencies | Tech lead | Resolved; no assembly or effective ordering change |
| 204 | 2026-10-13 | SPEC-62 Q16 / CAM-008 | Camera FX reader missing; generic effect names differ | Borrow current-frame completed uploader drain; map actual hit/explosion/ram kinds and bind at boot | Tech lead | Resolved; synchronous reader, no second queue consumer |
| 205 | 2026-10-13 | SPEC-13 Q33 / DMG-011 | Chunk geometry origin, byte slot capacity and obsolete dissolve event | Use exact COM-rebased boxes/cells, validate 1..256 slots, canonical continuous ChunkDissolve state, reconstruct real slot handles | Tech lead | Resolved; five required lifecycle tests pass; live rendering pending |

| 206 | 2026-10-13 | SPEC-22 Q11 / PWR-002 | Finite float inputs can overflow intermediate ellipsoid math | Double offsets and normalized quadratic; retain tangent/epsilon rules, deterministic centre extension and explicit output precision limits | Tech lead | Resolved; validation pending |
| 207 | 2026-10-13 | SPEC-22 Q12 / PWR-002 | Fixed-step march misses tangencies | Independently select resolvable random domains and check boundaries directly; never filter using implementation | Tech lead | Resolved; validation pending |
| 208 | 2026-10-13 | SPEC-61 Q20 / HUD-001 | Editor constructor types precede dependent EDT-001 | Stage only canonical empty editor VM/command identities with precise EDT-001 TODOs | Tech lead | Resolved; remaining editor contracts stay open |
| 209 | 2026-10-13 | SPEC-60 Q21 / UI-008 | Control defaults unspecified | Clamp invalid fractions, zero fill for invalid maxima, cached labels, named children and explicit tab defaults | Tech lead | Resolved; validation pending |
| 210 | 2026-10-13 | SPEC-60 Q22 / UI-008 | Tab test depends on later frontier layout audit | Check actual panel bounds/clipping/scroll now; canonical audit invocation remains UI-012 | Tech lead | Resolved; no frontier implementation delegated |

| 211 | 2026-10-13 | SPEC-62 Q17 / CAM-010 | Float pixel API cannot ensure universal 1e-9 map round trips | Preserve API and double arithmetic; assert precision on independently conditioned pixel-representable fixtures and document general float pixel error | Tech lead | Resolved; no universal precision claim |
| 212 | 2026-10-13 | SPEC-62 Q18 / CAM-009 | Focus key has no selection payload | Focus whole configured bounds; hosts use FocusOn for selection. Editor getters expose editor pose; reduced motion snaps entry | Tech lead | Resolved; validation pending |
| 213 | 2026-10-13 | SPEC-40 Q12 / NAR-001 | Missing registry factory signatures | CreateNode takes bound objects and returns selected node interface as object; Invoke consumes runtime LogicValue arguments; bodies remain NAR-003 stubs | Tech lead | Resolved; existing contracts unchanged |
| 214 | 2026-10-13 | SPEC-62 §5.3 / CAM-008 integration | Framing test assumed no post-framing impact shake | Disable shake in framing fixture only, retaining strict viewport assertions and separate shake tests; production shake remains after framing as specified | Tech lead | Resolved; full PlayMode rerun pending |

| 215 | 2026-10-13 | SPEC-13 Q34 / ADR-0063 | Chunk contact reactivation converts overlap into a violent kick | Add per-pool-incarnation clearance latch; minimum grace preserved, recontact normal after clearance, other pairs unchanged | Tech lead | Implemented; 3 contact regressions and live 264-cell repeat pass, full-suite rerun pending |

| 216 | 2026-10-13 | SPEC-72 Q19 / MIG-009 | Broad dependency waits for strict link validation that needs migrated weapons | Use permitted DATA-001 contract dependency; DATA-007 implementation already complete and original migration/weapon prerequisites; existing serialized weapon field/bake contract is sufficient | Tech lead | Resolved; preserve E047 and batch missing canonical content before strict validation |

## Measurements

Record perf gate measurements here (date, task, scenario, numbers, machine).

| Date | Task | Gate | Result | Notes |
|---|---|---|---|---|
| 2026-10-13 | VFX-015 / VFX-005…014 | Complete effects (Apple M5 Pro, Metal) | Tracers5k **0.03850 ms** vs 0.3; mixed upload5k **0.26865 ms** vs 0.25; debris upload20k **1.01195 ms** vs 0.6; **50,000 actual debris**, GPU delta **unavailable** | 60 warm-up + 300 CPU samples; GPU timing supplied no sufficient positive unique samples (explicit null/FAIL). 1 PASS / 3 advisory FAIL/unavailable perf rows. Warmed hit upload **0 B GC**, owned GraphicsBuffers **3,172,864 B** (full GPU/native accounting inconclusive). Zero optimization passes. Evidence `Logs/playtest/vfx015/` |
| 2026-10-12 | DEV-016 | Real Fighter record/replay | **300 ticks**, recorded/replayed local position **(16.66402, 0, 43.55072) m**, **0 m error**; live CLI replay completes **1,790 ticks** | Production boot/newgame seed 7, input actions, imported ship and physics. Exact test controls the tick endpoint; live variable-frame capture is visual evidence. Full-suite advisory misses: Propulsion50Ships **0.1939 ms** vs 0.15, ThrusterRebuild **3.6237 µs** vs 2, Hit1MColliderJobs **0.6844 ms** vs 0.5; zero tuning |
| 2026-10-12 | TST-004 | Full voxel pipeline (Apple M5 Pro, Metal, 1920×1080) | Idle **0.224481 / 0.600458 ms median/p95**; spawn **15 frames**, **5.571041 ms max**; accounted reference memory **36,180,776 B**, thin shell **131,349,912 B** | 60 warm-up + 300 samples, 17 workers; mesh and installed collider readiness. Four damage buckets for memory; source evicted. Total memory **inconclusive**, private scratch/engine overhead excluded (Q174); performance deferred, **0 tuning passes**. Evidence `Logs/playtest/tst004/gpu.xml` |
| 2026-10-12 | REN-013 | Production renderer / screenshot (Apple M5 Pro, Metal, 1920×1080) | 1,008,128-cell / 872-brick station render share **0.117376 / 0.173208 ms median/p95**; 2,500-brick static **0.131499 ms**, moving **0.161125 ms**, delta **0.029626 ms** | Inclusive current renderer markers, 60 warm-up + 300 measured frames per state; real spawn/snapshot/build/apply. Ship20k **MAE 0**, **341,662 nonblack pixels**, ten applied bricks. All three GPU tests pass; zero tuning. Evidence Logs/playtest/ren013/ |
| 2026-10-11 | CAM-011 | Camera CPU / GC / circle (Apple M5 Pro, reference CPU settings, headless Editor) | Inclusive camera update median/p95 **0.002833 / 0.003000 ms** (≤0.05 ms); allocation median/p95/max **0/0/0 B** | 60 warm-up + 300 measured updates; includes ECS reads, InputCameraInput, rig, view state and VoxelViewer write. Circle: **185,964 frames / 10.009428 s / 80 m/s**, pitch **54.999989–55.000019°**, every measured update 0 B. Q131 defers actual-raycast 0.15 ms evidence to CAM-007; no missing path counted as passed |
| 2026-10-11 | CAM-005 | Moving-target camera at 30/60/144 FPS | Final X **230.4243 / 230.4243 / 230.4243 m** (printed precision), 0.05 m agreement gate passes | Target 100 m/s for 2 s; default look-ahead and half-lives. Original endpoint update: 232.0124 / 231.2455 / 230.7731 m; 30–60 error 0.766891 m. Q130 integrates the exponential cascade without relaxing the test |
| 2026-10-11 | FLT-011 | Propulsion worker / cache rebuild | 50 ships × 20 thrusters: median/p95 **0.08983 / 0.1081 ms** (budget 0.15 ms). 200-instance rebuild: **1.162 / 1.628 µs per ship** (budget 2 µs) | Apple M5 Pro, 17 workers, reference CPU settings, 60 warmup + 300 samples; actual Burst Jobs markers. Ship20k + 20 thrusters per root. Rebuild fixture adds 180 non-thruster functional instances and conservatively includes the whole zero-sweep propulsion job |
| 2026-10-11 | FLT-011 | Flight scheduling / player / plane / GC | Median/p95: propulsion main **0.001041 / 0.003417 ms** (0.03); player command **0.000666 / 0.000834 ms** (0.01); plane worker, 100 bodies **0.003166 / 0.007417 ms** (0.02). Managed allocation **0 B every sample** | 60 warmup + 300 samples; GC measured around the three flight system updates and their job completion, excluding fixture mutations, scenes and unrelated physics work |
| 2026-10-11 | FLT-011 | Exact flight determinism | **8/8 runs identical**, 600 ticks × 3 ships = 1800 records/run | Frame patterns 1, 2, 0/1 and 6 ticks/frame × 1/17 workers; compare every LocalTransform and PhysicsVelocity field by bits, never struct padding. Scripts exercise thrust, turn, boost, brake and coast |
| 2026-10-11 | FLT-009 | Off-centre thrust / ram | Lever 2.9999998 m; first-tick yaw −0.12755036 rad/s with gyro disabled. Ram A/B forward speeds 13.49946 / 16.50054 m/s; relative momentum error 3.1789144e-8 | Apple M5 Pro; real spawn, collider, propulsion and Unity Physics integration; non-rendered synthetic fixtures |
| 2026-10-11 | FLT-009 | Thruster loss / turn / edges | Acceleration 19.801975 → 10.204095 m/s²; yaw prediction 0.11423472 rad/s. Aft-only yaw 2.0909076 vs cap 2.0943952 rad/s after 180 ticks. Eight ability edges at each of 30/60/144 FPS | Shipped config; loss removes cells with BrickWrite and supplies the future damage system's instance-removal result |
| 2026-10-11 | PHY-023 | Urgent deadline completion | Reference 30-ship workload: median/p95 0/0 ms, max 0.085417 ms; six forced completions; zero late installs | Apple M5 Pro; Deadline marker, reference settings, 60 warmup + 600 measured ticks; worker median/p95 0.034209 / 3.338420 ms; same 38 perf gates pass |
| 2026-10-11 | PHY-023 | Six-tick catch-up diagnostic | Deadline completion max 0.528208 ms; all 12 determinism runs identical, zero late installs | Default safety settings, not a reference timing gate; one station + two ships, 96 sampled ticks/run, 1/17 workers. Deliberate 500 ms stalled batch test waits outside fixed step and installs exactly at tick 8 |
| 2026-10-11 | FND-008 | Persistent-ID check without refill | 0.000083 / 0.000084 ms median/p95; gate 0.01 ms | Apple M5 Pro; reference settings; Check profiler marker, 60 warmup + 300 initialization updates; valid zero threshold |
| 2026-10-11 | PHY-019 | Brick rebuild | Noise 19.9 / 23.5 µs median/p95; full 1.1 / 1.3 µs; slab 0.6 / 0.7 µs | Apple M5 Pro, Unity 6000.6.5f1, Burst on, safety/debugger/leaks off; 60 warmup + 300 frames; 1,000 distinct 50% noise masks; serial Run includes call overhead |
| 2026-10-11 | PHY-019 | Station idle / spawn | Idle 0.013833 / 0.017874 ms median/p95; spawn 0.024395 / 0.220916 ms, max 0.314877 ms; ready ≤ 8 frames | Inclusive physics-system + gameplay ECB markers; 42 completed spawns / 300 measured frames, after 60 warmup; CurrentValue deltas verified nonzero in headless mode |
| 2026-10-11 | PHY-019 | Radius-3 station hit | 0.308300 / 0.333600 ms median/p95 | 112 removed cells; 4 changed bricks + actual 872-child assembly; 60 warmup + 300 frames; serial Burst work plus Run overhead; remeshing remains SPEC-13's combined gate |
| 2026-10-11 | PHY-019 | 30-ship skirmish stand-in | Worker median/p95/mean 0.038312 / 3.620159 / 0.816310 ms; urgent median/max 300/300 bricks; 450 assemblies/s; zero late installs | 30 × Ship20k; 20 distinct occupied cells/ship/tick; 60 damage warmup + 600 measured ticks; real mass recompute excluded from collider-only worker sum; p95 reported, gate is median ≤ 2 ms |
| 2026-10-11 | PHY-019 | Live collider memory | Peak 3,275,424 B; steady 1,263,456 B; **original 2.5 MiB no-go**, revised 3.5 MiB pass | 300 frames / four damage buckets; unique ownership across cache, pending, retired, completed batch and deferred queue. ADR-0033 / Q103; corrected retained reference model 32,227,328 B; whole integration remains TST-004 |
| 2026-10-11 | REN-002 | Idle 2,500 registered meshes | Presentation 0.313042 / 0.339875 ms median/p95; active RenderLoop 0.746250 / 0.877166 ms; 5,035 draws | Apple M5 Pro, Unity 6000.6.5f1, Metal MultiThreaded, 1920×1080 linear target, URP 4 cascades / 300 m, safety off; 60 warmup + 300 CPU samples |
| 2026-10-11 | REN-002 | GPU idle | **6.434792 / 10.494958 ms median/p95 — no-go** vs 6 ms | 300 unique positive FrameTiming samples; capture continued beyond the 300 CPU samples because Editor GPU timings arrive sparsely; initial single-threaded results superseded |
| 2026-10-11 | REN-002 | Moving root delta | **1.625292 ms median delta — no-go** vs 0.5 ms | Paired static transform/presentation medians 0.029708 / 0.318000 ms; moving 0.030875 / 1.942125 ms; moving p95 0.037167 / 1.996417 ms; 300 samples each |
| 2026-10-11 | REN-002 | Rewrite 128 meshes | 0.101791 / 0.115875 ms apply median/p95; exact changed pixels; IDs unchanged | 300 batches, AllocateWritableMeshData + Burst fill + apply flags + explicit mesh/RenderBounds; apply marker includes both Mesh.bounds and ECS RenderBounds assignment, excludes fill |
| 2026-10-11 | REN-002 | UNorm8 packed format | Exact pixel decode, all 64 layers and 256 colour indices | 8-byte layout on Metal; no Float16 fallback needed; DX12/Vulkan unavailable on this host |
| 2026-10-11 | REN-002 | CPU copy release | 23,128 B before/after release, unchanged after 5 frames; later apply throws isReadable=false | Default ReleaseCpuMeshCopies=false; do not claim measured memory savings |
| 2026-10-11 | REN-002 | Lifetime and units | 10,000 rewrites + 2,000 delayed retire cycles: zero new native leaks/errors; single-cell hull size (1,1,1) | Native leak tracker reset after warmup, final count asserted 0; imported base_1x1_hull mesh is centred at origin; retired IDs read back null |

| 2026-10-11 | PHY-005 | 50% noise brick construction diagnostic | 9.7 µs median / 12.8 µs p95 | Apple M5 Pro; Burst Run including call overhead, 4 warmups / 101 samples, current test safety settings; formal gate remains PHY-019 |
| 2026-10-11 | PHY-002 | 1–2: Burst construction / copied ownership | 32 parallel persistent builds pass; disposed children leave 4/4 nested rays and distances correct | BoxCollider.Create + CompoundCollider.Create inside Burst IJobParallelFor |
| 2026-10-11 | PHY-002 | 3–5: nested contacts / None / pair disable | Nested: 1 pair, 16 contact points, 4 collision events; None: 0/0/0 and ray hit; disabled: 1/0/0 | Pair job between PhysicsCreateBodyPairsGroup and PhysicsCreateContactsGroup; separation normal 0.85 m versus 0.8 m suppressed |
| 2026-10-11 | PHY-002 | 6: collider MemorySize | Box 492 B; compounds64/1000 37,216 / 579,360 B | Aggregate overhead (including header/BVH/alignment) 89.5 / 87.36 B per child; child entry 40 B |
| 2026-10-11 | PHY-002 | 7: build64 / assemble872 / assemble3784 | Median 0.0231 / 0.2258 / 1.0585 ms; p95 0.0246 / 0.2298 / 1.0643 ms | Apple M5 Pro / Unity 6000.6.5f1 / Physics 6.6.0; Burst, safety/debugger/leaks off, 8 warmups + 51 samples; includes Run/call overhead; assembly uses distinct cached unit boxes |
| 2026-10-11 | PHY-002 | 8–9: installed API / Burst OnUpdate | IncrementalStaticBroadphase exists (bool); JobHandle.IsCompleted compiles and executes in Burst-attributed OnUpdate | Stabilization property is SolverStabilizationHeuristicSettings.EnableSolverStabilization; §5.13 corrected |
| 2026-10-11 | PHY-002 | 10: smoothed child pose / nested decision | **GO**; child LocalToWorld error 0 m over 30 frames at 120 Hz / 60 Hz physics; smoothing offset 0.500001 m | Installed smoothing + transform systems, Unity FixedRateCatchUpManager; headless transform readback, not pixel/GPU validation; PHY-020 not needed |
| 2026-10-11 | VOX-002 | Reference retained-copy peak | **38,828,288 B — no-go** for 36 MB headroom | 872 bricks; 504,192 faces; 570,496 welded vertices; 58 entities/chunk; 17 brick/meta chunks; Apple M5 Pro / Unity 6000.6.5f1 |
| 2026-10-11 | VOX-002 | Thin-shell stress | **162,606,784 B** retained-copy peak; 118,710,496 B released-copy model | 3,784 bricks; 2,086,936 faces; not a hard gate |
| 2026-10-11 | VOX-002 | Collider and source-map bytes | Box 492 B; 64-box compound 37,216 B; child entry 40 B; map header 64 B | Reference collider peak 2,587,904 B; exact data/type/capacity tables in SPEC-10 §8.2 |
| 2026-10-11 | VOX-002 | Sparse fallback projection | Reference 31,373,568 B before new overhead | Estimate only; VOX-003 must measure actual storage/headers/capacity slack |
| 2026-10-11 | PERS-007 | VOXS encode/decode 1M | 13.09 / 15.03 ms median; 13.27 / 15.06 ms p95 | Apple M5 Pro; gates 150 / 100 ms; 7 samples after 3 warmups |
| 2026-10-11 | PERS-012 | Capture / typical read+restore / 50 slots | 0.120 / 14.871 / 2.667 ms median; 0.127 / 15.307 / 8.872 ms p95 | Apple M5 Pro; 10 samples + 2 warmups; gates 5 / 500 / 50 ms |
| 2026-10-11 | PERS-012 | Reference VOXS encode / decode | 12.487 / 13.210 ms median; 12.736 / 13.469 ms p95 | Gates 150 / 100 ms |
| 2026-10-11 | PERS-012 | Reference ship compressed / damaged variant | 15,230 B / 34,810 B | Gate 81,920 B on pristine; damaged report only |
| 2026-10-11 | TST-008 | No-change compile / Play-to-menu / EditMode suite | 1.697 / 0.159 / 29.800 s median | Apple M5 Pro; 3 runs each through live CLI; gates 10 / 1 / 60 s; all PASS |
| 2026-10-11 | MIG-016 | Full migration / slowest ship / ship memory bound | 579.6 ms median (613.1 max) / 42.38 ms / 748,853 B | Apple M5 Pro; budgets 60 s / 200 ms / 10 MiB; all pass |

| 2026-10-11 | M0 system review | Reference perf pass | 23/23; capture 0.155 ms, read+restore 15.41 ms, list50 6.212 ms; event write/read 1.4/2.4 µs | Apple M5 Pro; safety/debugger/leaks off; separate safety-on 411/31 suite passes |


PHY-002 exact body-pair schedule (Physics 6.6.0): `state.Dependency = job.Schedule(SystemAPI.GetSingletonRW<SimulationSingleton>().ValueRW, ref SystemAPI.GetSingletonRW<PhysicsWorldSingleton>().ValueRW.PhysicsWorld, state.Dependency);` with `Execute(ref ModifiableBodyPair pair)` and `pair.Disable()`. The §5.12 group names and signature work unchanged. Local reproduction/evidence: `Logs/tools/phy002-PhysicsCapabilitiesSpike.cs`, `phy002-report.txt`, `phy002-results.xml`, `phy002-headless.log`. The disposable source is removed from Assets as specified.

### FLT-010 legacy flight tuning (2026-10-11)

Apple M5 Pro / Unity 6000.6.5f1; actual migrated immutable blueprints, canonical grid mass and Unity Physics at 60 Hz. Two independent clones per row measure straight full-W and turn-in-place. All 13 are **Small (<3,000 cells)**; no Medium/Large evidence exists in this catalog. Acceleration is local forward speed after 15 ticks divided by 0.25 s; cap time means **99% of 120 m/s**, avoiding the half-gain assist's asymptotic tail. Turn rate is at 10 s; t90 is the first positive 90° crossing. Forward runs last at least 10 s, then stop at cap or 120 s. Peak is within that window, not a separate stability gate. Counts show total usable thrusters and (small/medium/large). These are physics fixtures; no new ship art or layouts were authored.

**Baseline fallback — forward=120, yawCap=120, coreAccel=60, coreTorque=2000000.**

| Blueprint | Cells | Mass kg | Iyy kg m² | Forward N | Thrusters S/M/L | Accel m/s² (0–0.25 s) | t99% cap s | Turn deg/s (10 s) | t90 s | Peak forward m/s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| ship_base | 4 | 135.0 | 51.5 | 0 | 0 (0/0/0) | 0.000 | no forward thrust | 119.800 | 1.750 | 0.000 |
| ship_bastion | 361 | 9175.0 | 157831.1 | 120000 | 3 (0/3/0) | 13.079 | 9.083 | 119.800 | 1.500 | 120.002 |
| ship_beetle | 91 | 2650.0 | 6732.6 | 80000 | 2 (0/2/0) | 30.173 | 5.067 | 119.800 | 0.867 | 120.632 |
| ship_butterfly | 815 | 20780.0 | 399306.8 | 200000 | 8 (6/2/0) | 9.625 | 12.350 | 119.800 | 1.350 | 118.870 |
| ship_crescent | 306 | 8035.0 | 132640.1 | 120000 | 3 (0/3/0) | 14.935 | 7.967 | 119.800 | 1.350 | 120.014 |
| ship_fighter | 1005 | 24620.0 | 444116.5 | 180000 | 14 (13/1/0) | 7.311 | 16.250 | 119.800 | 1.217 | 118.812 |
| ship_freighter | 361 | 9175.0 | 157831.1 | 120000 | 3 (0/3/0) | 13.079 | 9.083 | 119.800 | 1.500 | 120.002 |
| ship_frigate | 127 | 3130.0 | 17988.0 | 80000 | 4 (4/0/0) | 25.559 | 4.650 | 119.800 | 0.933 | 120.262 |
| ship_icarus | 305 | 7975.0 | 132609.2 | 120000 | 3 (0/3/0) | 15.047 | 7.900 | 119.800 | 1.350 | 120.013 |
| ship_red_elite | 819 | 20745.0 | 321273.6 | 40000 | 1 (0/1/0) | 1.928 | 61.617 | 119.800 | 1.717 | 118.805 |
| ship_shuttle | 127 | 3130.0 | 17988.0 | 80000 | 4 (4/0/0) | 25.559 | 4.650 | 119.800 | 0.933 | 120.262 |
| ship_striker | 74 | 2070.0 | 4600.0 | 80000 | 2 (0/2/0) | 38.191 | 3.883 | 119.800 | 0.850 | 120.770 |
| ship_vestari | 972 | 24185.0 | 1108438.0 | 160000 | 8 (8/0/0) | 6.616 | 17.967 | 119.800 | 1.567 | 118.876 |

**Provisional shipped tuning — forward=120, yawCap=120, coreAccel=180, coreTorque=2500000.**

| Blueprint | Cells | Mass kg | Iyy kg m² | Forward N | Thrusters S/M/L | Accel m/s² (0–0.25 s) | t99% cap s | Turn deg/s (10 s) | t90 s | Peak forward m/s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| ship_base | 4 | 135.0 | 51.5 | 0 | 0 (0/0/0) | 0.000 | no forward thrust | 119.800 | 1.083 | 0.000 |
| ship_bastion | 361 | 9175.0 | 157831.1 | 120000 | 3 (0/3/0) | 13.079 | 9.083 | 119.800 | 1.050 | 120.002 |
| ship_beetle | 91 | 2650.0 | 6732.6 | 80000 | 2 (0/2/0) | 30.189 | 3.950 | 119.800 | 0.850 | 120.868 |
| ship_butterfly | 815 | 20780.0 | 399306.8 | 200000 | 8 (6/2/0) | 9.625 | 12.350 | 119.800 | 1.017 | 118.870 |
| ship_crescent | 306 | 8035.0 | 132640.1 | 120000 | 3 (0/3/0) | 14.935 | 7.967 | 119.800 | 1.017 | 120.014 |
| ship_fighter | 1005 | 24620.0 | 444116.5 | 180000 | 14 (13/1/0) | 7.311 | 16.250 | 119.800 | 1.000 | 118.812 |
| ship_freighter | 361 | 9175.0 | 157831.1 | 120000 | 3 (0/3/0) | 13.079 | 9.083 | 119.800 | 1.050 | 120.002 |
| ship_frigate | 127 | 3130.0 | 17988.0 | 80000 | 4 (4/0/0) | 25.559 | 4.650 | 119.800 | 0.883 | 120.301 |
| ship_icarus | 305 | 7975.0 | 132609.2 | 120000 | 3 (0/3/0) | 15.047 | 7.900 | 119.800 | 1.017 | 120.013 |
| ship_red_elite | 819 | 20745.0 | 321273.6 | 40000 | 1 (0/1/0) | 1.928 | 61.617 | 119.800 | 1.083 | 118.805 |
| ship_shuttle | 127 | 3130.0 | 17988.0 | 80000 | 4 (4/0/0) | 25.559 | 4.650 | 119.800 | 0.883 | 120.301 |
| ship_striker | 74 | 2070.0 | 4600.0 | 80000 | 2 (0/2/0) | 38.647 | 3.217 | 119.800 | 0.833 | 120.892 |
| ship_vestari | 972 | 24185.0 | 1108438.0 | 160000 | 8 (8/0/0) | 6.616 | 17.967 | 119.800 | 1.167 | 118.876 |

**Proposal and limits.** Apply `coreYawAccelDeg=180` and `coreMaxYawTorque=2,500,000` to the shipped asset; retain all other FlightConfig settings (120 m/s forward, 120°/s yaw cap, six allocation sweeps and existing assist gains). The intermediate 180°/s², 2 MN·m candidate left Vestari at 1.267 s; raising the torque cap to 2.5 MN·m brings it to 1.167 s. Every blueprint now meets the Small turning target. Fallback/definition defaults stay 60°/s² and 2 MN·m for reproducibility; boot uses the asset.

The acceleration target remains unmet: Beetle, Frigate, Shuttle and Striker exceed 20 m/s²; Fighter, Red Elite and Vestari are below 8 m/s². `ship_base` is the documented four-cell development placeholder with no thrusters (Q40), so it cannot reach the speed cap. Shared thrust-row scaling cannot reconcile the full-thrust mass ratios: Red Elite has one medium thruster and needs at least 165,960 N to accelerate at 8 m/s², while Striker has two of the same row and needs at most 20,700 N each to stay at 20 m/s². The measured allocator already uses almost all available forward force on both. Deliberately wasting force through torque imbalance is not a balance solution. Keep the existing small/medium/large rows at 20,000/40,000/40,000 N; SPEC-04/SPEC-72 content owners should revisit the approved layouts/masses or the class targets after Frank decides. Striker's migration already drops one overlapping legacy thruster; this report measures the valid migrated result, not the raw JSON count.

**Owner sign-off requested (Q125), pending; the autonomous run continues.** Options, recommended first:

1. **Keep the provisional yaw values**, then balance legacy blueprint propulsion/mass with the content owners after ship-design approval. Meets turning now; preserves physical thrust and the stated acceleration targets as follow-up work.
2. Keep the original 60°/s², 2 MN·m gyro for a heavier feel. Preserves fallback tuning, but nine of thirteen blueprints miss the 1.2 s turning target; acceleration still needs content work.
3. Keep the provisional yaw values and revise the Small acceleration target to preserve the legacy spread. Avoids layout changes, but accepts a roughly 20× acceleration range among powered ships and requires an explicit design decision.

Medium/large class tuning and the choice of a flyable starter remain unvalidated/unapproved. FLT-010's deliverable is the complete measurement/proposal and applied provisional config; its completion does not claim owner approval or that every balancing target passed.

## Log

### 2026-10-13 — DMG-011 complete: physical chunk breakup — astra (gpt-6)

1. Implemented deterministic 64-slot pool/eviction, exact cell mass and inherited motion, bounded greedy colliders, lifetime/dissolve rendering and deferred collider cleanup. Final contact repair `e2cbd81` (ADR-0063) prevents post-grace penetration impulses until parent clearance. All five acceptance criteria checked; spec-required: 5, extra: 0. Existing three PHY-014 tests include the required clearance/recontact regression, no new test count.
2. Compile clean with zero warning CS; full EditMode **901 passed / 0 failed / 1 skipped**, PlayMode **128 passed / 0 failed / 53 skipped**, both exit 0. Evidence `Logs/playtest/dmg011/editmode-repair.xml`, `playmode-repair.xml`. Live 264-cell slab visibly separates/tumbles at approximately 1.5 m/s and damps without the former approximately 102 m/s kick (`chunk-fixed-moving.png`, `contact-handoff-fixed.txt`). Zero optimization passes; this was functional physics repair.
3. Unity validation completed normally; test scene removed by runner, original QualitySettings delta restored with backup and excluded. Next lead task DMG-012 hulk creation/lifecycle. Three existing workers continue MIG-009, GAL-001 and UI-010. DATA-010/UI-009 delivered pending compatible batch; UI-009 fixture review repair `b8af3e3` must accompany its implementation.

### 2026-10-13 — DMG-011 live contact repair verified — astra (gpt-6)

1. ADR-0063 adds internal per-incarnation chunk parent-contact arming. After the existing minimum grace, the pair remains suppressed until exact collider clearance; future impacts collide normally. SplitSystem initializes/reset state and SplitGraceContactSystem is its only subsequent writer. Ordinary grid grace and unrelated pairs are unchanged. No velocity clamp or physics tuning changes.
2. Compile clean; all **3 SplitGraceTests pass**, including clearance/recontact assertions in the existing required test (spec-required: 3, extra: 0). Live repeat: 264 cells, age 59 velocity (-0.942,0,-1.130), age 73 (-0.938,0,-1.125), age 269 (-0.878,0,-1.054) m/s. No expiry kick; screenshot visibly shows detached tumbling wall slab. Evidence `Logs/playtest/dmg011/contact-handoff-fixed.txt`, `chunk-fixed-early.png`, `chunk-fixed-moving.png`, `contact-regression.xml`. Full regression rerun remains required before DMG-011 closeout.
3. Worker batch closeouts also integrated independently into main (`0561e29`), allowing DATA-010 and UI-009 claims (`6d3c2cd`). Their existing workers are implementing; MIG-015 `25ac3f0` and AST-001 `54c4fba` await review/integration. INP-008 worker hit provider capacity; retried the same thread/model and it resumed without lost work or duplication.

### 2026-10-13 — DMG-011 live contact handoff defect found — astra (gpt-6)

1. Live seed-7 production range: repeated controlled production projectiles cut a **264-cell** wall island. Chunk renderer is visible and initial separation velocity is approximately **(-0.95, 0, -1.14) m/s**. Screenshots `Logs/playtest/dmg011/chunk-early.png`, `chunk-moving.png`; commands/readbacks `live-cut.txt`. This is controlled projectile injection, not claimed keyboard input evidence.
2. **DMG-011 remains in-progress:** at age 59 ticks, velocity is (-0.942, 0, -1.130); after the 60-tick parent contact grace expires, age 73 velocity is **(-5.71, 100.72, -16.78) m/s**. Parent collider was already Ready and installed at tick 124 before grace ended at tick 162. Timeline `contact-handoff.txt` isolates the kick to contact reactivation; initial tumble likely rotated the piece into surviving parent geometry during suppression. Investigate and fix collision handoff; do not hide it with a velocity clamp or mark the feature done from isolated tests. This is correctness work, not a tuning pass.
3. Full compile and suites remain green at the tested revision: **901 EditMode / 128 PlayMode passed**, zero failures. Ten worker tasks closed in preceding entry. Editor currently running the damaged range. Preserve it until the contact fix needs recompilation/validation; QualitySettings serialization delta remains excluded.

### 2026-10-13 — Ten-task worker batch validated — astra (gpt-6)

1. Full compile: zero C# warnings. Full EditMode **901 passed / 0 failed / 1 skipped**; PlayMode **128 passed / 0 failed / 53 skipped**, both exit 0. Evidence `Logs/playtest/orchestration-batch2/` includes XML and required-test inventory. Framing fixture's unshaken setting resolves the earlier impact-shake interaction without weakening viewport limits or production shake.
2. Closed WPN-017 (spec-required: 4, extra: 0), CAM-008 (spec-required: 7, extra: 0), PHY-022 (spec-required: 1, extra: 0), PWR-002 (spec-required: 5, extra: 0), ABL-001 (spec-required: 1, extra: 0), HUD-001 (spec-required: 3, extra: 0), UI-008 (spec-required: 7, extra: 0), CAM-009 (spec-required: 6, extra: 0), CAM-010 (spec-required: 6, extra: 0), NAR-001 (spec-required: 2, extra: 0). Implementation hashes in rows; fixes and decisions above. No speculative tuning.
3. DMG-011 five required tests including explicit native leak checks pass, but live chunk renderer verification remains in progress. Editor opened for this check; full suite is finished. Same workers implement INP-008, MIG-015 and AST-001. Next ready M2 priority after integration is DATA-010 to unblock power/ability content. QualitySettings original serialization delta restored and excluded; test scene cleaned by runner.

### 2026-10-13 — DMG-011 integration validation and worker recycling — astra (gpt-6)

1. First corrected batch: compile clean, EditMode **887/0/1**; PlayMode **127/1/53**. One framing assertion failed when new impact shake shifted a corner after framing. SPEC-62 explicitly orders shake after framing; fixture now selects ShakeScale=0 while preserving all viewport limits and independent shake checks. No production effect reduction. Evidence Logs/playtest/orchestration-batch2/.
2. Added explicit warmed native leak detection to both existing chunk lifetime/unload tests; test count remains spec-required: 5, extra: 0. Live renderer verification still pending. Prepared production-projectile wall cut at Logs/playtest/dmg011/cut-wall.cs.txt; not executed yet.
3. Integrated CAM-010 `014ed0b` (spec-required: 6, extra: 0), CAM-009 `21e43a1` (spec-required: 6, extra: 0), NAR-001 `e39b2d3` (spec-required: 2, extra: 0). Compile passes; full EditMode/PlayMode running in session 77989. Q211–214 capture integration decisions. Zero tuning passes.
4. Same three workers recycled to INP-008, MIG-015 and AST-001. Main claims made through idle clean worker checkouts so the primary code under Unity validation stayed unchanged; no duplicate workers. Main INP-008 claim must merge into lead after Unity exits. All integrated tasks remain in-progress until validation and live requirements are satisfied.

### 2026-10-13 — DMG-011 / second batch integration in progress — astra (gpt-6)

1. Physical chunk pool, exact mass/inherited velocity, greedy colliders, expiry and deferred disposal implemented; five required tests pass, including pool eviction safety repair. Palette-lit chunk rendering compiles but still needs live verification. Test count: spec-required: 5, extra: 0. Zero optimization passes.
2. WPN-017, CAM-008, PHY-022, PWR-002, ABL-001, HUD-001 and UI-008 integrated; all remain in-progress pending combined validation. Lead fixed PHY-022 test NativeArray ownership alias after compile failure; subsequent compile passes. First full EditMode 886/1/1: static audit rejected 231 HUD cached notification fields; lead documented their readonly property-name-only lifetime (`f847997`) without changing audit coverage. Full EditMode rerun **887/0/1**; PlayMode running in session 56515. Results saved under Logs/playtest/orchestration-batch2/.
3. Same three workers reassigned to NAR-001, CAM-010 and CAM-009. Q206–210 mirror worker spec decisions and retained EDT-001/UI-012 work. No owner gate. Exact dispatch and commit evidence in ORCHESTRATION.md.

### 2026-10-13 — Validated worker batch integrated to main — astra (gpt-6)

1. WPN-017, CAM-008, PHY-022, PWR-002, ABL-001, HUD-001, UI-008, CAM-009, CAM-010 and NAR-001 validated together on lead branch: compile clean; full EditMode 901/0/1 and PlayMode 128/0/53. Individual required counts respectively 4/7/1/5/1/3/7/6/6/2; extra: 0 for each. Evidence Logs/playtest/orchestration-batch2/. Physical chunks remain on the lead branch while live contact repair is verified.
2. Main receives the completed worker implementations and readonly notification/NativeArray fixture repairs. Camera framing fixture explicitly disables shake to isolate the specified pre-shake framing contract. Next workers: DATA-010 and UI-009. Local integration only; no new owner gate or tuning pass.

### 2026-10-13 — PHY-014, PWR-001, MIG-011 batch complete — astra (gpt-6)

1. Full EditMode **861 passed, 0 failed, 1 skipped**, PlayMode **121 passed, 0 failed, 53 skipped**, both exit 0; compile zero CS warnings. Evidence `Logs/playtest/orchestration-batch1/{editmode,playmode}.xml`. PHY-014 spec-required: 3, extra: 0; PWR-001 spec-required: 3, extra: 0; MIG-011 spec-required: 1, extra: 3.
2. Migration generated all 20 mapped audio cues and updated database/manifest/report; legacy volumes and clip references verified by required real-export test. Audio remains development-only under existing provenance policy. No tuning or new owner gate.
3. Split-contact grace is now validated, unblocking chunk work. Next batch: delivered WPN-017/CAM-008, then worker PHY-022/HUD-001/UI-008. QualitySettings backup restored; unrelated original serialization delta remains excluded.

### 2026-10-13 — First worker batch integrated, validation pending — astra (gpt-6)

1. Reviewed and integrated PHY-014 `094f430`, PWR-001 `eeed571`, MIG-011 `ca37e33`. Status remains in-progress until central checks and audio assets complete. Tests authored: PHY-014 spec-required: 3, extra: 0; PWR-001 spec-required: 3, extra: 0; MIG-011 spec-required: 1, extra: 3 (migration safety).
2. Lead repaired audio test fixtures to load disposable source files instead of requiring Newtonsoft in the test assembly; compile passes. No assembly-boundary change or optimization pass. Q202 records deferred allocator ordering.
3. Reused combat/content threads for WPN-017/CAM-008. Power worker prepares PWR-002 read-only while its prerequisite is validated. Full-suite and generated-content evidence will follow under Logs/playtest/orchestration-batch1/.

### 2026-10-13 — Three-worker orchestration started — astra (gpt-6)

1. Frank authorizes three separate reusable GPT-6.1-Sol high threads and asks the lead to review/fix small issues while continuing implementation. ADR-0062 records the revised goal, standard/frontier split, isolated worktrees, central ledger ownership and batched lead-owned Unity validation. No subagents, remote Git or new owner gate.
2. Claimed and dispatched PHY-014 to Combat support, PWR-001 to Power and abilities, MIG-011 to Presentation and content. All three exact threads are verified active; IDs/worktrees and follow-up queues are in ORCHESTRATION.md. The lead prepares DMG-011/012 while the prerequisite split grace is implemented. Task results remain in-progress until integration validation; no tests claimed for this administrative setup.
3. Enabled the five-minute heartbeat `spaceship-implementation-coordinator` to reconcile the same threads and continue work when the executor is available. The native goal replacement tool rejected the request because the prior blocked goal is unfinished; its objective/status cannot be edited/resumed by the available tool. The owner-approved revised objective is durably recorded, and implementation continues without falsely marking the old goal achieved.

### 2026-10-13 — FND-021 lowered automatic World test station — astra (gpt-6)

1. **Implemented:** `c3bb396` lowers the production station **2 m**, to `(0,-2,45)`, so its three-cell floor straddles the firing plane. `AutoSpawnInWorld` is enabled for the current testing phase. The existing range service joins scene readiness immediately after the player, uses the normal spawn pipeline, and reuses its target on repeated range requests. Scene exit still releases the source; the next World entry gets a fresh station. ADR-0061 / Q201 records this reversible owner-requested setup.
2. **Validation:** compile zero CS warnings; full EditMode **854 passed, 0 failed, 1 explicit skip**, full PlayMode **118 passed, 0 failed, 53 expected skips**, both exit 0. All **four real-GPU range tests pass**, covering actual Space damage, holes, physical detachment and reset. Extended the existing owner-feature test for automatic spawn, height and idempotence: **spec-required: 1, extra: 0**, no new cases. No performance work or new owner gate.
3. **Live evidence/closeout:** opened `World.unity` directly and entered Play, without `newgame` or `range`. Verified exactly **two grids** (Fighter at origin and one static station at `(0,-2,45)`), both with colliders; no runtime errors, only the existing GridSpawnSystem lookup warning. Evidence `Logs/playtest/fnd021/` includes full-suite XMLs, GPU range XML, `direct-world.txt` and `world-auto-station.png`. Leave this World session open and unpaused for Frank. Controls, scale and Nocturne unchanged. Preserve/exclude the pre-existing QualitySettings serialization delta. Next remains PHY-014, then DMG-011/012.

### 2026-10-13 — VFX-015 complete effects batch and 3× firing station — astra (gpt-6)

1. **Implemented:** `b575efe` completes VFX-005…014 together, as Frank requested, then validates the whole batch. Session-owned mesh debris, tracers, clipped beams, white-blue plumes, shield ellipsoids/ripples, chunk dissolve, quality budgets, origin/reset handling and pooled warp are authored and wired. All 13 defaults validate strictly. This is integrated VFX validation, not the unfinished M2 milestone review.
2. **VFX-005/006:** debris event colour/velocity renders red and moves +X; 600-frame 20k/frame stress respects 50k/25k/10k quality caps; benchmark confirms **50,000 actual live particles**. Tracers cover dead rows, exact capped indices, pending rebase, system change, length/colour and 5k timing. Test counts: VFX-005 **spec-required: 3, extra: 0**; VFX-006 **spec-required: 6, extra: 0**.
3. **VFX-007…010:** beams end at the hit and emit sparks every six frames; destroyed roots draw none. Plumes follow nozzle rotation/intensity and exclude hulks. Shields validate active/HP gating, eight ripple ages and ellipsoid transforms. Chunk fading uses canonical EndTick, with a single writer. Counts: VFX-007 **spec-required: 3, extra: 0**; VFX-008 **spec-required: 4, extra: 0**; VFX-009 **spec-required: 3, extra: 0**; VFX-010 **spec-required: 1, extra: 0**.
4. **VFX-011…014:** quality reuses buffers; warmed 10k-hit updates allocate **0 managed bytes** and keep object count fixed. Production debris remains visually invariant across the rebase, with unchanged live count and submitted-buffer readback; huge/system shifts preserve warp, session reset clears all, idle anchors reset. Warp plays 600 particles and steals the oldest of two instances. Counts: VFX-011 **spec-required: 2, extra: 0**; VFX-012 **spec-required: 4, extra: 0**; VFX-013 **spec-required: 5, extra: 0**; VFX-014 **spec-required: 3, extra: 0**. Total **34 specified cases**, no extra cases.
5. **VFX-015 playable feature:** station geometry is **3× on each axis**, **153 cells wide** rather than 51, with **18,109 occupied cells**, normal cell size and original 45 m target offset. Existing breakaway reference test verifies the scaled bridge/masks. Actual mouse aim plus Space destroyed **42 cells**, with **48 tracers / 2 plumes / 41 estimated live debris** in a sampled firing/thrust frame. Screenshot: `Logs/playtest/vfx015/large-station-combat.png`; impact detail: `impact-debris.png`. The existing real-fire test is extended for the requested dimensions: **spec-required: 1, extra: 0**; no new feature test case.
6. **Final validation:** compile **zero CS warnings**; full EditMode **854 passed, 0 failed, 1 explicit skip**; full PlayMode **118 passed, 0 failed, 53 expected skips**, both exit 0. Grouped Metal VFX/range/input **32 passed, 0 failed, 0 skipped**. A temporary actual-material render verifies visible tracer/beam/plume/shield output (**1025 / 2455 / 4374 / 37440 nonbackground pixels**) and no shader errors; it caught/fixed the shield's reserved `point` token. Temporary script removed; final compile clean. Evidence: `final-editmode.xml`, `final-playmode.xml`, `final-gpu.xml`, `shader-proof.png`, `validation-summary.txt` under `Logs/playtest/vfx015/`.
7. **Performance remains advisory:** 5k tracers **0.03850 ms** vs 0.3; 5k mixed upload **0.26865 ms** vs 0.25; 20k debris upload **1.01195 ms** vs 0.6. The 50k GPU gate cannot obtain 300 positive unique timing samples on this Editor; its record is explicitly unavailable/FAIL, never a zero-time pass. Metal perf summary **1 PASS / 3 advisory FAIL/unavailable**. Headless `tools/test.sh perf Vfx` had zero EditMode cases/four GPU skips and exited 1 without records; actual GPU records are retained separately. Owned GraphicsBuffers **3,172,864 B**; full GPU/native memory remains inconclusive. **Zero optimization passes.**
8. **Correctness/decisions/next:** ADR-0060 / Q198–200 record brought-forward shield/entry contracts, canonical origin/tick inputs, production-graph validation, approved colours, per-render-frame event delivery, pooled restart and explicit producer completion. GPU input testing fixed DEV-004's disabled native keyboard fallback; its existing input tests pass. Rig leak checks isolate the unrelated Entities Graphics allocator, retain hard zero-leak assertions and release all owned buffers. PWR-001/SYS-001 stay todo with partial notes; beam weapon, shield simulation, travel and galaxy streaming are not claimed complete. Controls, Nocturne and Space fire are preserved. Next: **DMG-011/012 chunk/hulk tiers**, then ready M2 work; no new owner gate. Git stays local. The pre-existing QualitySettings serialization delta is preserved and excluded; no temporary InitTestScene or editor job is committed.

### 2026-10-12 — VFX-004 visible combat effects and live weapon integration — astra (gpt-6)

1. **Implemented:** `7327e4c` authors five VFX Graph templates and seven definitions (including ADR-0058 RamImpact), own procedural radial texture and staged config; registers content, binds the session rig before the boot entity snapshot, disposes it with the composition and applies VFX quality changes. Null-graphics runs retain the unbound queue drain. Eight hit sparks share one additional short flash output; explosion, muzzle and shield-spark counts/lifetimes follow SPEC-63. Six future effect families warn until their content tasks land. Migration validation regenerated its report with these six staged warnings and zero errors.
2. **Real gameplay / other-task files:** live testing exposed absent muzzle, hull-hit and explosion producers in the FND-020 slice. Guarded queue hooks now connect real Space firing to the graphs, preserving weapon overrides and source flags. ADR-0059 / Q197 / SPEC-21 Q12 / SPEC-63 Q22 adds the contact normal already available from the cell raycast. Other-task files: `Simulation/Weapons/{CombatHit,CombatHitTest,WeaponFireSystem,ProjectileStepSystem,ExplosionSystem}.cs` (WPN-003/007/008/010); `App/Boot/GameComposition.cs`, `App/Settings/SettingsApplier.cs` and their existing boot/range tests. Weapon rows retain todo and explicit exists/missing notes; shape-accurate collision, power, shields, parallel delivery and full weapon criteria remain open. No new owner gate, hull, AI asset or flight/environment change.
3. **Validation:** compile zero CS warnings. Fresh full headless EditMode **838 passed, 0 failed, 1 explicit skip**; PlayMode **117 passed, 0 failed, 36 expected skips**, both exit 0. Real-GPU VFX suite **8/8**, range **4/4**, boot composition **4/4**. The three new specified tests pass: authored definitions validate, ten effect objects/seven slots satisfy contracts, and 100 hits yield one batch with **800 spawn requests and 800 live particles**. The existing real-fire test additionally verifies muzzle and hit graph delivery. **spec-required: 3, extra: 0**; zero optimization passes. The reused interactive Editor's full EditMode run failed seven input/save/leak assertions; the unchanged tree passed in a clean headless process. Preserve both XMLs; 08-tools records this environment limitation rather than claiming the live full suite passed.
4. **Profiler attribution:** retained real-firing frames **20826–22825 (2,000 frames)** contain zero allocation samples attributed to weapons, projectiles, FxRig/FxUpload, damage or range. The **5,034 samples / 1,596,433 bytes** in the capture are save/thumbnail/serialization, worker and logging work, with two autosaves in the retained window. This is not an allocation-free-save claim. Capture used CPU allocation stacks with profileEditor=false; screenshots preceded capture, export followed disabling it, and prior profiler settings were restored. No recurring combat allocation was found to fix. Full paths/stacks: `Logs/playtest/vfx004/fire-alloc.tsv`; bounded analysis: `allocation-analysis.txt`.
5. **Visible evidence / advisory memory:** actual real firing produced live muzzle and impact particles; `Logs/playtest/vfx004/real-hit-sparks.png` and `pod-impact-2.png` show sparks on the damaged station, with the Fighter and Nocturne environment preserved. Readback observed 24 live hit particles and 32 muzzle particles. Authored rig GraphicsBuffers total **696,320 bytes**; upload/native and VFX Graph internal buffers are additional, so the full 12 MB content-set total remains unquantified and advisory. No tuning pass or progression gate. Temporary generator removed from Assets; its evidence copy remains under Logs. QualitySettings matches the exact pre-run backup and is excluded from commits; no InitTestScene remained after the idle test run.
6. **Next:** VFX-005 visible voxel-debris particles, then DMG-011/012 chunk/hulk tiers. Missing debris content still produces a bounded unresolved-effect warning; do not claim these new spark graphs render voxel debris. Local Git only. FND-020's six external-review fixes remain recorded under `53e61db`; final current-tree full-suite results above are green.


### 2026-10-12 — VFX-003 guarded event drain and upload — astra (gpt-6)

1. **Implemented:** `f24d8c1` — FxUploadSystem now automatically completes registered queue writers and drains every presentation frame, even unbound. FxDrainJob enforces the combined queue/pending-impact hard cap; FxBatchJob resolves slots, culls, applies quality/LOD, resolves palette/raw colours, caps per-slot/debris submissions and produces GPU-layout events. FxRig submits batched buffers and reuses pooled effects with cached IDs/attributes. Persistent counters and a conservative lifetime ring report actual accepted debris; origin-shift/session-clear completion retains TODO(VFX-013).
2. **Required evidence:** all **12** focused tests pass. **100,000** inputs leave an empty queue, with **65,536** processed and **34,464** over cap. Three fixed ticks produce three events submitted once; queued pending impacts merge and clear. Far debris keeps one in four; player-source hits bypass distance culling; slot overflow does not inflate spawned/live counts; palette index zero works; NaN positions are skipped. All three profiler markers report positive samples. The warmed CPU consumer path with the specified fake rig allocates **0 managed bytes over 100 updates**. Real graph output and its integrated allocation capture belong to the authored-content check next. **spec-required: 12, extra: 0**; zero optimization passes.
3. **Validation:** compile zero C# warnings; full EditMode **838 passed, 0 failed, 1 skipped**; full PlayMode **116 passed, 0 failed, 34 expected headless skips**, both exit 0. Docs checks pass. Evidence: `Logs/playtest/vfx003/`. Runner has stopped; no InitTestScene remains. QualitySettings matches its pre-existing backup and is excluded from commits. No remote operations or new owner gate.
4. **Resolved/next:** Q196 / SPEC-63 Q21 records accepted-only debris accounting, conservative ring wrapping, palette-flag precedence, the internal fake-rig seam and bounded unresolved diagnostics. **VFX-004 next:** author the hit/explosion/muzzle/spark graphs and definitions, bind the rig in GameComposition before the boot snapshot, and show actual output; VFX-005 supplies debris particles. Measure the complete GPU/native memory set then (advisory), and retain controls, Nocturne, test station and Space fire. Event queues no longer accumulate while unbound; visible debris is still not claimed. Continue DMG-011/012 afterwards.

### 2026-10-12 — VFX-002 session effect resources — astra (gpt-6)

1. **Implemented:** `f2b8118` — FxRig owns canonical slots/hash/default lookup, a 256-entry linear palette, batched GraphicsBuffers, pooled VisualEffects and High-budget instanced batches. Broken graph properties log once and disable only that definition. Quality changes reuse resources; world binding replaces prior rigs and owns VfxRuntimeConfig; Dispose releases GPU/native resources and child effects. VFX-003 submission and VFX-013 origin handling retain explicit TODOs. This does not yet consume the queue or render new debris.
2. **Required evidence:** five GPU tests pass: one batched plus four pooled children; missing FxEvents rejects only its definition; replacement binding and quality changes work; all definition hashes resolve in order; both captured GraphicsBuffers are invalid after Dispose and the warmed native leak check returns zero. Representative GPU buffers total **950,272 bytes**, below the advisory 12 MB target; actual VFX-004 content remains to measure when authored. **spec-required: 5, extra: 0**; zero optimization passes.
3. **Validation:** compile zero C# warnings; full EditMode **826 passed, 0 failed, 1 skipped**; full headless PlayMode **116 passed, 0 failed, 34 skipped**, both exit 0. The five additional graphics skips pass separately with a GPU (**5/5**). Docs checks pass. Evidence: `Logs/playtest/vfx002/`. Runner exited; no InitTestScene remains. QualitySettings matches its exact pre-run backup and is excluded from commits. No new art or owner gate; Git stays local.
4. **Resolved/next:** Q195 / SPEC-63 Q20 records the internal consumer binding hook and one-time palette colour conversion, avoiding double gamma conversion in upload. **VFX-003 next:** drain the existing damage queues even while unbound, batch/upload events, then author visible content and complete DMG-011/012. Controls, Nocturne, test station and Space fire remain unchanged; no fresh gameplay screenshot is claimed for this resource-only task.

### 2026-10-12 — DMG-010 split planning and debris events — astra (gpt-6)

1. **Implemented:** `e3045a8` — PlanSplitJob selects particle/chunk/hulk tiers, applies the shared cell budget and available hulk IDs/cap, and writes every accepted detach mask in canonical order. Particle cells leave the parent on the following resolver tick. Existing FND-020 hulk creation remains available for the playable station; chunk plans retain their seeds until DMG-011. EmitDebrisJob emits world position, parent motion plus kick, palette/material metadata and cube-root-scaled strided debris, plus one configured cell-break cue per grid with destroyed cells. This is event production, not completed visual/audio content.
2. **Required evidence:** all four tests pass. A five-cell island remains intact on the planning tick and emits five debris events when detached next tick, inheriting parent velocity `(7,0,2)`. Boundaries 8/9/500/501, functional blocks and 17-cell extents select the specified tiers. **5,000** removed cells emit **1,667** events (stride 3, size × cube-root(3)), plus one break cue. Two five-cell islands under a five-cell budget detach over two ticks in identical order with **1 and 17 workers**, preserving the twenty-cell anchor. **spec-required: 4, extra: 0**; zero optimization passes.
3. **Validation:** compile zero C# warnings; full EditMode **826 passed, 0 failed, 1 skipped**; full PlayMode **116 passed, 0 failed, 29 skipped**, both exit 0, including the production station shooting/pod scenarios. Docs check clean. Evidence: `Logs/playtest/dmg010/`. The prior live station demonstration remains DMG-009 evidence; no new visible-debris claim or screenshot this task. QualitySettings matches its pre-existing backup and is excluded; no stray InitTestScene remains after the runner stopped. Git stays local.
4. **Resolved question/next:** Q194 / SPEC-13 Q32 replaces impossible outside-island neighbour reseeding with occupied island and anchor representatives; it also preserves existing hulk recording and aligns the older CellDebris name to SPEC-63's Debris + PaletteColor contract. No owner gate or new art. VFX-002/003 are next priorities: the FX consumer is still absent, so producer events currently accumulate until world disposal; the specified upload path must drain even while unbound. Continue to visual content and DMG-011/012 chunks/hulk completion after that integration. Controls, Nocturne, procedural station and Space fire are unchanged.

### 2026-10-12 — DMG-009 hybrid integrity and live station breakup — astra (gpt-6)

1. **Implemented:** `1cf796e` — replaces the temporary full-grid flood with deterministic candidate/seed selection, parallel exclusive per-grid races, connected-brick/cell nodes, cross-brick face and instance links, a size-fair heap, union-find and O(1) FIFO concatenation. Touched summaries and warm-up run before traversal. Per-grid streams publish island masks in canonical grid/set/slot order. Node-budget work and seeds survive four deferrals; the fifth attempt is forced. The former occupied-cell cap is no longer used; its fields remain compatible. No managed gameplay allocations, fixed-step completion, public API/assembly change or new owner gate.
2. **Correctness/evidence:** six required tests pass; **300** seeded structures (including **100 core grids**) produce **7,863 islands** matching an independent full-grid BFS. Smaller/larger bridge anchor rules, instance links across bricks, four 100-node deferrals and exact forced/unlimited island equivalence pass. A radius-3 crater removes **93 cells** from the **1M-cell** station and visits **78 nodes** (advisory target ≤2,000), with no islands. Focused suite including existing connectivity/review checks: **11 passed**. Compile zero C# warnings; full EditMode **822 passed, 0 failed, 1 skipped**; full PlayMode **116 passed, 0 failed, 29 skipped**, both exit 0. **spec-required: 6, extra: 0**; existing cap/summary fixtures updated for the replacement implementation. Zero optimization passes.
3. **Live integration:** real seed-7 range, mouse aim and three seconds of **Space** fire severed the port bridge: **11 destroyed / 205 detached cells**. One physical pod retained its visible hull and moved at approximately **0.75 m/s**; no new runtime errors/warnings. Screenshots: `Logs/playtest/dmg009/station-before.png` and `station-after.png`; entity/velocity/log readback in `live.txt`. Full test/compile evidence is in that directory. The CLI launcher did not start an Editor process; after cancelling it, the installed Unity executable opened successfully. The live range remains open for inspection. Controls, Nocturne, procedural station and Space fire are preserved.
4. **Resolved decisions:** Q193 / SPEC-13 Q31 records two correctness fixes: a seed prefix cannot prove the anchor, so overflow retains the complete set and uses the same defer/force policy; randomized case 142 exposed equal 16-cell components, so early stopping must prove size/tie precedence before retaining the survivor. A NativeStream writer-copy defect found by the focused checks was fixed before the full suites. Remaining split particle/chunk tiers, debris FX and later damage tasks stay open; next is DMG-010. QualitySettings matches its pre-existing backup and is excluded; no stray InitTestScene remains. Git stays local.

### 2026-10-12 — DMG-008 brick connectivity summaries — astra (gpt-6)

1. **Implemented:** `36e838d` — BrickConnectivityCheck floods 16-bit X rows, propagates across Y/Z, and unions spatial components sharing an instance reference. Full and single-cell bricks bypass flooding. The summary records connectivity, instance presence and SolidCountAtCheck. IntegritySystem checks touched bricks and warms Unknown bricks in ascending slot order, with the global budget divided deterministically across sorted candidate grids. This runs before the temporary size guard, so oversized grids still gain summaries and retain seeds. DMG-009 remains responsible for using summaries in the hybrid race; its TODO and incomplete ledger notes remain.
2. **Validation:** the three required tests pass. **1,000** seeded masks at 10–90% density, alternating dense/sparse storage, exactly match an independent six-neighbour BFS (**163 connected / 837 split**). Instance links join spatially separated cells in both storage forms. Full/single-cell results and production warm-up selection pass, including a 3-check budget split 2/1 across two grids and touched-brick refresh with zero warm-up budget. Compile zero C# warnings; full EditMode **816 passed, 0 failed, 1 skipped**, full PlayMode **116 passed, 0 failed, 29 skipped**, both exit 0, including production firing-range tests. Evidence: `Logs/playtest/dmg008/`. **spec-required: 3, extra: 0**; zero optimization passes; no task-specific perf gate.
3. **Decision/closeout:** SPEC-13 Q30 / ledger Q192 records deterministic budget remainders and staged integration, with no new owner gate. Existing integrity code is updated for this task's required warm-up wiring; full-grid traversal, node budgets and forced retries are still DMG-009. Controls, Nocturne, procedural station and Space fire are untouched. QualitySettings matches the pre-existing backup and is excluded; no stray InitTestScene remains after the test runner stopped. Git stays local. Next: DMG-009 hybrid integrity and its station-cut checks.

### 2026-10-12 — DMG-007 collision damage and next-tick knockback — astra (gpt-6)

1. **Implemented:** `01d5ff2` — ImpactEventSystem applies resolver-owned pending impulses once on the next tick, projects planar linear/angular impulses, collects real Unity Physics contacts, sorts and aggregates canonical pairs, splits ram energy by contact hardness, enforces six-tick cooldowns and prunes stale pairs. Ram damage suppresses duplicate physical impulses; FX/SFX events use the existing queues. ProjectileStepSystem now follows impact processing. General ImpulseRequest consumption remains explicitly TODO(DMG-019).
2. **Evidence:** six required tests pass, including actual 20-ton physics collisions, resting contact, armour split, next-tick-only impulse application and planar projection. Head-on contact emits one event per side with approximately 3997.627 damage each and a 2 m radius; the next five contact ticks emit none. At 30/60/144 FPS, all runs emit exactly two events totalling **7995.25390625 damage**. Compile has zero C# warnings. Full EditMode **813 passed, 0 failed, 1 skipped**; full PlayMode **116 passed, 0 failed, 29 skipped**, both exit 0, including the four firing-range scenarios. Evidence: `Logs/playtest/dmg007/`. **spec-required: 6, extra: 0**; zero optimization passes.
3. **Scope/decision:** Q191 / SPEC-13 Q29 / SPEC-63 Q19 and ADR-0058 add the missing RamImpact enum value 13 and extend defaults to 14 entries without renumbering existing values. Unity normals are converted from B-to-A at collection. Other-task files: `Simulation/Weapons/ProjectileStepSystem.cs` (WPN-008 ordering); `Data/Definitions/Vfx/{FxKind,VfxConfigDefinition}.cs` and existing VFX validation test (VFX-001 contract extension). Upload and visual content remain VFX-003/004; audio cues remain unconfigured. Controls, Nocturne, station and Space fire are preserved. No new owner gate; local Git only. No stray InitTestScene remained after the idle run; QualitySettings matches its pre-existing backup and is excluded. Next: DMG-008 brick connectivity, then DMG-009 hybrid integrity.

### 2026-10-12 — DMG-006 canonical per-grid damage pipeline — astra (gpt-6)

1. **Implemented:** `0018fd1` — Gather validates events (including non-finite fields and incomplete spawning grids), stable Sort includes gather position, Plan merges event and detach-only ranges, Resolve runs one exclusive grid per worker, and Flatten publishes removals/touches/destroys/explosions in canonical range order. Only Flatten enables BrickDirty bits or adds cross-grid counters. Per-range streams and scratch lists retain disposal handles; no fixed-step Complete, new assembly reference, public API change or extra cell writer. Pending impulses, source/instigator stats and single revision increments are maintained.
2. **Determinism/functional evidence:** focused resolver + blast tests **10 passed** (seven new required resolver cases, three existing blast cases). Six runs of **2,000 events / 10 grids / 3 stream orders / 1 and 17 workers** give hash **8620D17719A337C1** and the same **500 removed-cell records**, comparing every field after checking current entity handles and normalizing grid identity by PersistentId. World recreation changes Entity versions; it does not change logical grid identity. Detach-only ranges, detach-before-hit, immunity, dropped-event counts/warnings, last applied source, once-per-tick revisions, dirty-bit publication and impulse reset/accumulation pass. Final compile has zero CS warnings; full EditMode **807 passed, 0 failed, 1 skipped** (exit 0). Full PlayMode **116 passed, 0 failed, 29 skipped** (exit 0), including all four production firing-range scenarios. No C#/Burst compilation errors; docs check clean. Pre-existing QualitySettings change is preserved and excluded; Git stayed local.
3. **Scope/decisions:** Q190 / SPEC-13 Q28 explains deferred stream construction and serial forwarding into the existing Explosion channel. CellStateHasher is brought forward for this task's specified check; DMG-014 keeps the full 600-tick integrated scenarios. Repair gathering, cell writes and budgets remain DMG-019 with a TODO at the exact insertion point. Controls, Nocturne, station and Space fire are unchanged. **spec-required: 7, extra: 0**; **zero optimization passes**, no task-specific perf gate. Evidence: `Logs/playtest/dmg006/`. Next: DMG-007 ramming and next-tick knockback.

### 2026-10-12 — DMG-005 point/blast and instance damage — astra (gpt-6)

1. **Implemented:** `7dbd025` completes the FND-020 point/blast/instance path with a once-per-resolver radius-clamp warning, one-cell penetration for sub-half-cell blasts, and reactor instigator fallback to the source grid when no shooter exists. The warning uses an owner-disposed native latch and Burst-safe logging; no managed hot-path state, extra sync point, new API or art change. Point DDA still carries overflow through air and armour; blast remains two-pass over pre-event HP.
2. **Required behavior:** all ten focused tests pass: 250 damage removes two 100-HP cells and leaves the third at 50 HP within one quantum; rays enter fresh holes; zero-direction hits stay in one cell; the 400-damage crater has 19 cells and all 48 cube symmetries; shielding reduces the stronger crater from **57 to 27** cells; 5/10 thruster cells trigger full collapse and queue its functional entity; function HP zero also collapses; reactor emits exactly one 400-damage, 4-cell-radius request; core loss clears the anchor and records the world centre. Repeated radius-clamped events warn once and stay inside the cap. Q189 / SPEC-13 Q27 explains why the independent occlusion comparison requires 1600 damage; no gameplay tuning or owner gate.
3. **Validation:** focused **10 passed, 0 failed**; compile zero CS warnings. Full EditMode **800 passed, 0 failed, 1 skipped** (exit 0). Full PlayMode **116 passed, 0 failed, 29 skipped** (exit 0), including all four firing-range scenarios. Docs check has zero errors/warnings. Local-only Git; pre-existing QualitySettings change excluded. **spec-required: 10, extra: 0**. No new perf gate in this task; **zero optimization passes**. Evidence: `Logs/playtest/dmg005/`. DMG-006 retains the remaining resolver work; later WPN/DMG/VFX rows stay open.

### 2026-10-12 — FND-020 external review fixes — astra (gpt-6)

1. **Default suite:** implemented in `53e61db`. Timing budgets now warn and continue through functional assertions; targets and PerfRun JSON pass/FAIL records are unchanged. Installed NUnit lacks `Assert.Warn`, so Unity warnings include measured value and target. First full validation: EditMode **788 passed, 0 failed, 1 skipped**, PlayMode **116 passed, 0 failed, 29 skipped**, both exit 0; compile zero CS warnings. Converted shared timing checks include Write1MiB, MeshBuild128, StructureStationEncode, Propulsion50Ships, ThrusterListRebuild and Hit1MColliderJobs. Direct timing-only checks also converted: managed drain idle, EcsTestWorld create/dispose and step overhead, renderer idle/moving/rewrite/render-thread/native GPU budgets, renderer/content/SFX footprint budgets and collider readiness. Shared positive footprint and readiness budgets are advisory too; zero managed allocation, functional capacity, measurement validity and functional timeouts stay hard. ADR-0042 / 05-testing now require green-or-real-failure default suites. Evidence: `Logs/playtest/fnd020-review/step1-*.xml`. Final full validation after all fixes: **EditMode 790 passed, 0 failed, 1 skipped; PlayMode 116 passed, 0 failed, 29 skipped; both exit 0**. Final compile has zero CS warnings. Explicit perf has 47 PASS and 2 FAIL records (Write1MiB 3.445 ms / 3; MeshBuild128 1.084 ms / 0.5), with zero test failures and the expected perf-summary exit 1; targets and JSON remain unchanged. Final evidence: `Logs/playtest/fnd020-review/{compile.log,editmode.xml,playmode.xml,perf-summary.txt}`.

2. **Integrity:** ADR-0057 / Q188 bounds the staged whole-grid flood at 16384 occupied cells through DamageConfig/definition. Oversized work keeps its seeds, warns once per grid identity and increments IntegritySizeDeferred. Masks now use one node pass into label/slot buckets and sorted canonical emission. Both required review tests pass: three repeated over-cap attempts preserve the same seed and zero masks, then raising the cap consumes it; the station bridge cut leaves 828 occupied cells and emits a 199-cell pod in 3 masks identical to the old scan. No tuning pass.
3. **Partial work:** DMG-005…013, WPN-002/003/004/006/007/008/010 and VFX-005/006 retain todo and explicit `partial in FND-020: exists / missing` notes. Restored task-ID TODOs cover remaining work. Other-task files in the original FND-020 implementation: `Voxel/Damage/Jobs/{CellWriter,PointDamage,BlastDamage,InstanceDamage,DamageAccess}.cs`; `Voxel/Damage/Systems/{DamageStreamsSystem,DamageResolveSystem,IntegritySystem,SplitSystem,ShipDeathSystem}.cs`; `Simulation/Weapons/{WeaponContent,CombatHitTest,WeaponInitSystem,WeaponAimSystem,WeaponFireSystem,ProjectileShotHandlerSystem,ProjectileStepSystem,ProjectileStore,ExplosionSystem}.cs`; `Data/Definitions/Weapons/{WeaponDefinition,WeaponTuningDefinition}.cs`; `Presentation/Vfx/ProjectileTracerBridgeSystem.cs`, `ProjectileTracer.shader` and associated weapon/tracer content assets. Paths are under `Assets/Game/`; full owning-task acceptance criteria and tests remain open.
4. **Allocation attribution:** CPU profiler GC.Alloc call stacks captured separately for Editor idle, real firing, warmed firing and screenshot/CLI work. The final warmed real-fire capture spans frames **1203–1426 (224 frames)** and contains **zero GC.Alloc samples**, including weapons/projectiles/tracers/damage/range. Real combat readback shows 13 destroyed station cells and 205 detached cells. Editor captures identify GameView toolbar/IMGUI, runtime bindings, Editor callbacks and Pipeline commands/HTTP responses; they are not gameplay allocations. The normal unprofiled harness readback reproduced **74222 B/frame**; profiler modes/sampling change the total (Editor capture median 8527 B/frame), so the historical ~75 KB counter is not a per-system allocation measurement. Screenshot reproduction attributes **3479471 B** to CaptureCommands PNG/base64 and **7847964 B** to Pipeline transport/serialization, including EncodeToPNG, Convert.ToBase64String, JsonConvert, Encoding.GetBytes and ResponseStream. This identifies the screenshot allocation source; the historical exact **10430788 B** frame has no saved raw stack and cannot be reconstructed byte-for-byte. Initial breakup also triggered one-time Editor Burst JIT compilation in render cleanup; warmed capture is clean. No recurring gameplay allocation was found to fix. Temporary profiler script removed; captures/readbacks and analysis are in `Logs/playtest/fnd020-review/`. Profiler export itself is allocation-heavy and excluded from captured intervals. This is correctness attribution, not an optimization pass.
5. **Image / tree / handoff:** the existing image test keeps whole-frame MAE and adds foreground-union MAE plus worst 64px-region MAE. GPU baseline passes with all errors zero. A temporary 64×64 missing-section mutation passes the old whole-frame check but fails the new localized check; restored the exact pre-mutation source with cp. QualitySettings restored from the pre-GPU backup, preserving its pre-existing unstaged serialization change; it is excluded from commits. No InitTestScene remains after completed test runs. HANDOFF now says cells/id/hash restore but pose/velocity do not (GAL-008).
6. **Test scope:** added only the two explicitly required integrity review tests and strengthened one existing screenshot test. FND-020 now has **spec-required: 6, extra: 0** (four original range tests plus these two; this follow-up adds two). No additional edge-case suite. The standing test-volume rule and per-task count requirement are recorded in 05-testing and 04-workflow. Final full suites are green; checks and evidence are recorded in item 1. Zero speculative optimization passes.

### 2026-10-12 — FND-020 playable shooting and test-station breakup — astra (gpt-6)

- **Implemented:** `ee607dd3` connects installed plasma mounts, mouse aim and **Space/left mouse** fire to the projectile store, current-cell hit testing, ordered damage, point/blast/instance removal, connectivity and physical grid splits. Hull meshes and colliders update, functional blocks transfer with their section, and core loss/explosions use the canonical damage channels. The procedural station has layered hull walls, slopes/long slope, a hollow single-bridge thruster pod, a redundant two-bridge cargo/reactor platform and a wider twin-reactor bay. `tools/playtest/range.sh 7` starts/reset the range; `range` works in the dev console. Tertiary fire moves to G. No V1 code, new ship hull or AI-generated asset.
- **Owner direction:** “spacebar should be shooting”; “dont use vestari, make a test station we can shoot, that has various possible combination of blocks and structures so we can test everything like breaking off, etc”. The imported Fighter remains the player; the target is the requested procedural fixture. Nocturne brightness, animated sky, sphere/atmosphere and spatial debris are preserved.
- **Live evidence:** inspected `Logs/playtest/station-final-before.png`, `station-final-firing-2.png`, `station-final-breakaway.png`, `station-final-damaged.png` and `station-final-reset.png`; firing/breakup images shown in the conversation. A three-second real Space hold destroyed **14 station cells** and detached **204**; the visible thruster pod moved at **(-0.7497753, 0, 0.01835992) m/s**. Further shots opened the centre hull and damaged separated grids. Two direct resets restored the intact range with **zero errors/allocation warnings**. Final shutdown has no native leak report. Readbacks/logs/scripts: `Logs/playtest/fnd020/`.
- **Validation:** compile zero C# warnings; final full EditMode **786 passed, 2 advisory timing misses, 1 explicit skip**; full headless PlayMode **116 passed, 0 failed, 29 expected skips**. All four required range tests pass: real Space fire/static target, front holes then rear-wall damage, moving rendered split with functional reparenting, and immediate restart. Pause is covered within real-fire testing. Earlier regressions fixed: rebind fixture key conflict, new writer buffer initialization, optional cleanup ECB lookup, migration weapon-link idempotence, and the boot disposal expectation. No correctness failure remains.
- **Measured/deferred performance:** live sustained fire, **20 samples of 15-frame Editor averages**: median **8.64 ms**, nearest-rank p95 **13.48 ms**, max **13.92 ms**. These include Editor/harness overhead and do **not** bound individual frame stutters or replace player-build profiling. Final advisory misses: Write1MiB **3.4503 ms / 3**, MeshBuild128 **1.0283 ms / 0.5**; station encode passed on the final run. **Zero optimization passes.**
- **Decisions/follow-ups:** Q184–187 / ADR-0053–0056 cover the staged canonical implementation, ECS-tracked root serial allocation, unadopted split-collider ownership and Persistent cross-tick event streams. Split bricks explicitly receive render cleanup state; runtime cleanup fixes were driven by live evidence. Block migration now resolves the new plasma links and retains byte-stable manifests. Broader DMG/WPN/VFX tasks remain open: serial/full-grid internals, physical-grid-only debris, missing power/shields/AI/beams, final impact effects/audio, pooling and large-battle work are explicit limitations. Continue with DMG-005's complete required tests/criteria, then dependency order; do not wait for owner art gates or advisory numbers. Existing owner decisions remain pending; no new owner question. Pre-existing QualitySettings serialization change is preserved unstaged; Git stayed local.

### 2026-10-12 — DMG-004 done; playable firing/breakup checkpoint queued — astra (gpt-6)

- **Implemented:** `8b20d15` adds deterministic damage maths, half-open DDA, dense/sparse cached cell access and the shared cell writer. Removal updates masks/counts/revisions, touches face-neighbour meshes, clears empty brick slots and emits cells with inherited rigid motion and reason-specific kicks. HP-only changes preserve dirty flags within one visual bin. Ten required tests pass, including 500 seeded rays with 10,000 reference samples each and 10,000 sub-quantum HP samples. Instance collapse/resolver integration follows.
- **Verification:** compile zero C# warnings; focused damage suite **22 passed**; full EditMode **785 passed, 3 advisory timing failures, 1 explicit skip**; full headless PlayMode **112 passed, 0 failed, 29 expected skips**. Evidence `Logs/playtest/dmg004/`. Timing misses: Write1MiB **3.4990 ms / 3**, MeshBuild128 **1.0913 ms / 0.5**, station encode **76.9774 ms / 60**. Performance deferred, **zero optimization passes**; no reruns to chase timing.
- **Decisions:** Q183 resolves the stale Urgent dirty flag and thin-cell reference sampling. Q184 / ADR-0053 records Frank's new explicit checkpoint: “Get that playable and show me it working.” **FND-020 is next:** larger stationary test-station target, real input/projectiles, holes and independently moving physical sections in one playable slice. Broader combat tasks remain open; simple staged internals retain V2 ownership and existing render/physics paths. No V1 code or new hull artwork.
- **Workspace:** an existing `ProjectSettings/QualitySettings.asset` serialization change was present before this run and remains unstaged; it was not authored or reverted by this task. No remote Git operation.


### 2026-10-12 — FLT-013 done — astra (gpt-6)

- **Changed:** V2-only control correction (`1bc23e8`, ADR-0052): rebindable Shift modifier redirects A/D into strafe with zero turn; default boost moves to Left Ctrl, with existing overrides preserved and boost suppressed during strafe mode. Neutral forward assist requests gentle 0.08/s deceleration through actual opposing thrusters. Generalized force requests are bounded by installed actuator capacity before the existing solver. No velocity overwrite, new engine, hull edit or copied V1 code.
- **Measured handling:** actual 24,620 kg Fighter has 180 kN forward and 40 kN reverse/per side. Production physics tests measure forward **7.22805**, reverse **1.53696**, right **1.55107**, left **1.55112 m/s²** after a 90° rotation; removing reverse thrusters removes reverse capability. Turn-then-strafe gives **3.24939 m/s** lateral change with **0°** measured facing drift. From 20 m/s, two seconds of coast leaves **17.10869**, full brake **16.83856 m/s**, eventually stopping; no instantaneous momentum rotation is promised.
- **Live proof:** fresh Editor/seed-7 Fighter, real key injection: S reaches **−4.65227 m/s**; Shift+D gives lateral thrust 1, Turn 0, no boost and stable facing; after **60.9505°** rotation, W gives **21.5889 m/s** local forward with negligible lateral drift; 189 coasting ticks reduce this to **16.8104 m/s**; Left Ctrl+W produces Boost. Zero runtime errors. Screenshot `Logs/playtest/fighter-controls-corrected.png`; full data/logs in `Logs/playtest/flt013/`.
- **Checks:** compile zero C# warnings; full EditMode **776 passed, 2 advisory timing misses, 1 explicit skip**; full headless PlayMode **112 passed, 0 failed, 29 expected skips**. Deferred Write1MiB **5.1710 ms** vs 3, MeshBuild128 **1.1423 ms** vs 0.5; zero performance tuning passes. Initial float-formula test needed a 2e-5 rounding tolerance. A live full-suite attempt aborted when Play stopped and left Pipeline status stale; cancelled, retained incomplete evidence, closed Editor, then completed the headless suite and a fresh live demonstration. Tools note records recovery without a Pipeline repair detour.
- **Decision/next:** SPEC-20 Q20 / ledger Q182 resolves the technical defaults under Frank's request: “dont copy v1, PLEASE, just make your own version with our new architecture”. Frank should assess control feel; this is not a blocking review. FLT-012 must request power for active coast/drift assist when power lands. Stationary-target firing range is the next M2 checkpoint: DMG-004 cell removal, damage resolution and production weapon integration, live sustained-fire timing; structural breakup follows. No shooting or destruction implementation is claimed by this task. Existing owner/art gates remain; local Git only.

### 2026-10-12 — DMG-003 done — astra (gpt-6)

- **Changed:** all 43 numeric config fields and four cue hashes project from content; nine shipped assets supply default config, four damage types and four armor classes. The table is indexed by authored type/class values, independent of catalog order. Boot replaces the eager fallback DamageConfig, owns the native table alongside rendering config, and restores Default before releasing it on shutdown. Implementation `c1d6af2`.
- **Decisions/validation:** SPEC-13 Q23 / ledger Q181 supplies the current catalog internally to editor/runtime definition validation. DMG-E001–E004 reject malformed multiplier arrays, duplicate/missing known damage types, invalid kicks and noncontiguous armor indices. E005/E006 cover config budgets, finite/nonnegative values and ranges. A test-only fifth numeric enum value works without a production enum or algorithm change; cue hashes copy correctly. No owner gate or tuning decision.
- **Checks:** all three required config tests pass, covering all 16 multipliers, kicks/shields/bypass, boot singleton replacement/disposal, extension and required validation failures. Compile zero C# warnings; final full EditMode **775 passed, 3 advisory timing misses, 1 explicit skip**; full PlayMode **109 passed, 0 failed, 29 GPU/draft skips**. Write1MiB **4.3121 ms** vs 3, MeshBuild128 **1.0744 ms** vs 0.5 and StructureStationEncode **76.8512 ms** vs 60 remain performance deferred, zero tuning. Evidence `Logs/playtest/dmg003/`. An earlier PlayMode run was cancelled for the missing-type validation correction; its incomplete log is labeled, and its generated test scene was removed after final verification.
- **Checkpoint/next:** DMG-002 and DMG-003 are complete and locally merged. Continue **DMG-004** cell health/removal, then point/blast resolver and ramming for the first visible damage checkpoint, followed by weapons. No cell-damage gameplay is claimed yet. M1 DEV-009/012 still await later world-location/current-system implementation; no new blocker. Frank can review resolved Q179–181; existing owner-only art/tuning items remain as recorded. Approved Nocturne environment unchanged.

### 2026-10-12 — DMG-002 done — astra (gpt-6)

- **Changed:** DamageStreamsSystem creates all four shared channels and persistent damage memory before the boot snapshot, publishes tick statistics through a dependent job, and swaps detach lists without redirecting pending jobs. Removed/restored readers and phase handles participate in teardown. Explosion drains gather and stably sort by source, producer, sequence and gather position; core-loss and retired-blob drains transfer pending results. Implementation `e3377fa`.
- **Correctness decisions:** SPEC-13 Q21 / ledger Q179 orders the stats reset and records phase ownership, deferred views and the 64-ulong cell mask. The full suite exposed BC1016 in the shared SimEventChannel stale-warning string constructor; Q22 / Q180 preserves the exact warning using unmanaged character appends. No public API change, logging suppression or optimization pass.
- **Checks:** five required tests pass with safety/leak checks, including 6,000 parallel events, stable explosion order at 1/8 workers, one-time stale warning and destruction with pending readers. Compile zero C# warnings. Full EditMode **773 passed, 2 advisory timing misses, 1 explicit skip**; full PlayMode **109 passed, 0 failed, 29 GPU/draft skips**. Write1MiB **3.8533 ms** vs 3 and MeshBuild128 **1.1164 ms** vs 0.5 remain performance deferred; zero tuning. Evidence: `Logs/playtest/dmg002/` (including the original Burst failure and corrected full results).
- **Next:** DMG-003 config/table → DMG-004 cell write path → point/blast resolver and ramming. This task is event plumbing, not visible destruction or weapons. Preserve approved Nocturne visuals. No new owner decision.

### 2026-10-12 — Built M1 systems reviewed — system-review: astra (gpt-6)

- **Scope and authority:** implemented M1 voxel storage/spawn/write-back, snapshot meshing and pooled presentation, collider/mass pipeline, flight/input/camera, UI pause/context and session persistence/cleanup. This is the owner-requested self-review (the explicit autonomous instruction overrides ADR-0019's independent-author condition); no independent reviewer is claimed. Compared implemented boundaries and edge-case behavior with SPEC-10/11/12/20/62 and workflow §7.2; later systems remain outside the built scope.
- **Finding fixed:** the four previously unexplained GPU Editor shutdown allocations are all FleetSaveParticipant VOXS capture buffers stranded in a quit-save thumbnail wait. A no-Play Editor control exits cleanly; EnabledWithStackTrace identifies all four allocation paths in `Logs/playtest/m1-review/tracked-editor.log`. PERS-019 (`f5ea272`, Q178) gives waiting captures explicit ownership through background handoff. Six quit tests pass; the identical tracked real Fighter lifecycle after repair reports **zero native leak reports**, with no runtime errors. Review/source checks also covered boot singleton lifetime, mesh results for destroyed bricks, exclusive collider ownership/deferred retirement, fixed-step install ordering, command edge consumption and single-owner UI pause/context.
- **Integrated gameplay evidence:** the current full PlayMode run passes off-centre thrust (3 m lever, **−0.12755036 rad/s** yaw), real ramming (**13.49946 / 16.50054 m/s**, relative momentum error **3.18e−8**), the independent momentum scenario (**0** relative error), and a 64-cell hole blocked before tick **16** and traversable from that install tick. Three ships × 600 motion records are bit-identical over four frame patterns with **1 and 17 workers**. Real imported Fighter record/replay also passes. Extracted evidence: `Logs/playtest/m1-review/integration-results.json`; complete results `Logs/playtest/pers019/playmode.xml`.
- **Rendering/scale evidence:** inspected retained TST-004 GPU results: **6 passed, 1 inconclusive**, including 1M-station render/readiness and REN-013's reference/moving-lattice cases. Accounted memory remains explicitly partial, not a total-memory pass. Live post-fix seed-7 Fighter flies with real W/Shift input, sampled at **18.29955 m/s** and z **134.3293 m**, one camera/listener, no runtime errors; screenshot `Logs/playtest/fighter-m1-reviewed.png`. Approved Nocturne brightness, planet and dust unchanged. Burst safety was enabled and native leak mode EnabledWithStackTrace in the reproduction.
- **Validation and advisory gaps:** compile zero C# warnings; EditMode **768 passed, 2 timing misses, 1 explicit skip**; PlayMode **106 passed, 3 timing misses, 29 expected GPU/draft skips**. Current timing misses: Write1MiB **3.4831 ms**, MeshBuild128 **1.1638 ms**, Propulsion50Ships **0.1730 ms**, ThrusterRebuild **2.8338 µs**, Hit1MColliderJobs **0.6019 ms**. Preserve TST-004's incomplete total-memory accounting and the existing spawn lookup advisory. No tuning passes used; renderer allowance remains exhausted.
- **Gate/next:** built M1 gameplay exit checks are satisfied: ships/station render and collide, the player flies, off-centre thrust turns the ship and ramming pushes bodies. Full milestone task completion remains **107/109** because DEV-009/012 require later world-location/current-system support; no claim those features exist. Continue M2 with **DMG-002** and its damage/weapon dependents, checking FND-012's real galaxy prerequisites before claiming it. No new owner-only decision; existing art/tuning/defaults remain pending as documented. Editor closed, QualitySettings restored, no remote operations.

### 2026-10-12 — PERS-019 done — astra (gpt-6)

- **Changed:** SaveService tracks captures awaiting thumbnail completion or worker dispatch; Dispose releases them synchronously under the worker-handoff lock. Already encoding workers retain sole ownership through their normal atomic write/release. Queued/new captures and completion/quit callbacks stop after disposal. No public API or wire-format change. SPEC-05 Q29 / ledger Q178; implementation `f5ea272`.
- **Tests:** DisposeDuringThumbnailWait proves immediate release without a frame, no subsequent encode/commit, and no delayed QuitAction. DisposeDuringEncoding holds a worker at encoding and proves disposal leaves its buffer live, then exactly one release and no service callback. Existing quit success/timeout/blocked/no-session tests also pass (**6/6**). Full-suite and real-Fighter leak reproduction evidence are recorded in the review above; `Logs/playtest/pers019/` retains focused/full XML, compile/live logs and sampled flight state.
- **Next:** DMG-002 starts the combat/destruction buildout. No performance tuning or visual changes.


### 2026-10-12 — DEV-019 done — astra (gpt-6)

- **Changed:** all six SPEC-70 regression drafts are checked in: station dialog, inventory-safe editor cancellation, paint-only commit round trip, ship death without ECB errors, pause-menu load and two-leg lane travel. Explicit parse-only fixtures contain only declared future vocabulary. No future gameplay is claimed implemented. SPEC-70 Q24 / ledger Q177 records the editor input/Ark dialog prerequisites before activation; implementation `f891b9d`.
- **Checks:** compile zero C# warnings; full EditMode **766 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **4.4635 ms**, MeshBuild128 **1.1224 ms**). ScenarioDraftTests.AllDrafts_Parse passes. Targeted PlayMode ScenarioFileTests **1 passed, 6 skipped**, each with reason `draft`; real Boot smoke passes. No runtime code changed; DEV-016 remains the latest full PlayMode suite. Evidence `Logs/playtest/dev019/`. Docs clean; zero optimization passes.
- **Next:** built M1 system review, then the first M2 spawning/combat slice. M1 is **106/108 done**: DEV-009/012 still await real world-location/current-system support. Check FND-012's actual galaxy-service prerequisites before claiming; ledger Ready describes declared dependencies, not implementation availability. Preserve approved Nocturne visuals and investigate the previously recorded GUI shutdown allocations in the review.


### 2026-10-12 — DEV-007 done — astra (gpt-6)

- **Changed:** ScenarioFileTests discovers sorted smoke/regression file stems as native UnityTest ValueSource cases, inherits the real GameFlowFixture, parses active scripts against the actual host and runs ScenarioRunner to completion. It prints/asserts the JSON artifact and imposes the requested timeout × 60 frame bound alongside the runner’s real-time limit. Draft headers are parsed before their future vocabulary is resolved; production parsing remains strict. SPEC-70 Q23 / ledger Q176; implementation `8f8adc3`.
- **Acceptance evidence:** adding two temporary files without changing code yields **2 passed, 1 ignored**: the new active file and canonical smoke_boot pass; the draft with unknown future command/probe is ignored with reason `draft`. The intentionally failing negative-tag fixture stays excluded. Temporary inputs are copied to Logs then removed; rerunning canonical files gives **1 passed**, with a real Boot/MainMenu and one listener. Result JSON paths are in `Logs/playtest/dev007/discovery.xml` and `canonical.xml`.
- **Checks:** compile zero C# warnings; full EditMode **765 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.4491 ms** vs 3; MeshBuild128 **1.0483 ms** vs 0.5). No runtime/boot/flow code changed; targeted PlayMode covers the new file cases, and the latest full PlayMode remains DEV-016’s **105 passed, 3 advisory timing misses, 23 GPU skips**. Docs clean. Zero tuning; no new art or owner decision.
- **Next:** DEV-019 six draft regressions, then built M1 system review and M2 combat. Preserve DEV-009/012’s documented future-system dependencies and the existing shutdown-allocation review item.

### 2026-10-12 — DEV-016 done — astra (gpt-6)

- **Changed:** `record start/stop/save name=...` and `replay name=...` share the console/scenario registry and `gh_record` / `gh_replay` CLI adapters. The host owns the recorder and playback operation; session changes, host disposal and timeout stop playback. Files use bounded stems under TestData/Replays and SPEC-71's existing text format with `.pcr` suffix. Sparse tick origins shift to the fresh session's current clock; spacing and duration are preserved. Implementation `f1f6cd2`; SPEC-70 Q22 / ledger Q175 documents local-position acceptance until canonical universe coordinates exist, and the frame-scheduling limitation.
- **Integrated acceptance:** the required test passes both focused and in the full suite. Real Boot → `newgame seed=7` → imported Fighter, real keyboard actions, input sampler and physics: record forward thrust, turning and strafing for **300 exact ticks**, save, start another seed-7 game and replay. Both end at **(16.66402, 0, 43.55072) m**, **0 m error** against 1e-4 m. The test drives the production world with one sampling update/fixed tick per step, restoring its player-loop/rate manager afterward; no alternate spawner or synthetic universe position. This does not claim arbitrary catch-up schedules reproduce the same endpoint.
- **Live game:** actual CLI start/stop/save and fresh-game replay all work. Retained `TestData/Replays/fighter_flight_demo.pcr` contains **1,790 ticks** of boosted forward flight, right strafe and coasting; operation 3 reports **succeeded: replayed 1790 ticks**. During playback the ship was sampled at **27.43835 m/s**; runtime logs have **zero error kinds**, one camera/listener. Visually inspected screenshot `Logs/playtest/fighter-flight-replay.png` shows the real Fighter beside the separate planet and procedural sky. Live sample positions are at different times and are not an endpoint comparison. Evidence `Logs/playtest/dev016/live-evidence.json`, recording copy and live Editor log.
- **Full checks:** compile zero C# warnings; EditMode **765 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.4907 ms** vs 3; MeshBuild128 **1.0356 ms** vs 0.5). PlayMode **105 passed, 3 advisory timing misses, 23 GPU-only skips**: Propulsion50Ships **0.1939 ms** vs 0.15, ThrusterRebuild200Instances **3.6237 µs** vs 2, Hit1MColliderJobs **0.6844 ms** vs 0.5. No functional failures; no tuning or reruns to chase those numbers. Docs clean. The GPU Editor again reports the same **four Persistent allocations only at process shutdown**; retain the existing M1 system-review follow-up, not a zero-leak claim. Editor closed and QualitySettings restored from the pre-run backup.
- **Next:** DEV-007 automatic scenario tests, DEV-019 draft regressions, then the built M1 system review and M2 combat. No owner decision or visual change; approved Nocturne brightness/density is preserved.

### 2026-10-12 — TST-004 done — astra (gpt-6)

- **Changed:** four required integrated voxel tests use real spawn, snapshots, mesher, pooled rendering and physics with a 1920×1080 URP camera. Readiness requires every brick mesh plus a valid installed collider. Inclusive updating Game.Voxel system markers and Entities Graphics are measured without nested marker double counting. Memory accounting evicts the spawn source, inspects allocated capacities/unique collider blobs and exercises four damage buckets over 300 frames; independent jobs are completed before inspection, separately from timing. Implementation `a56c266`; SPEC-71 Q10 / ledger Q174 states the measurement limits.
- **GPU evidence:** **6 passed, 1 inconclusive**, including all three REN-013 regressions. HollowStation1M: idle **0.224481 / 0.600458 ms median/p95**, spawn ready **frame 15**, worst voxel frame **5.571041 ms**. Accounted peak **36,180,776 B** (ECS chunks 671,744; buffer heap 7,321,824; mesh GPU 20,582,464; mesh native 267,780; mesh batch 3,491,864; queues 252,376; collider storage 989,668; blobs 2,603,056). Thin shell reports **131,349,912 B**. Reference CPU settings, 17 workers, 60 warm-up + 300 samples. **Total memory remains inconclusive**: private scratch/engine overhead is excluded; the JSON budget result applies only to the accounted capacities. GPU process exits 2 because of this explicit inconclusive result. **Performance deferred; zero tuning passes**, with no renderer changes.
- **Full checks:** compile zero C# warnings; EditMode **765 passed, 2 existing advisory misses, 1 explicit skip** (Write1MiB **5.1419 ms** vs 3; MeshBuild128 **1.4718 ms** vs 0.5); PlayMode **107 passed, 23 GPU-only skips**. Required headless perf wrapper: no EditMode matches, four GPU skips, no fresh records; actual measurements are from the GPU run above. No native-leak/aged-TempJob diagnostics in these PlayMode/GPU logs. Evidence `Logs/playtest/tst004/`. Docs clean.
- **Next:** DEV-016 live flight record/replay, then DEV-007/019, built M1 system review and M2 combat. No new owner decision or art change; preserve the approved Nocturne environment. The total-memory measurement gap does not block progression.

### 2026-10-12 — REN-013 done — astra (gpt-6)

- **Changed:** three required rendering regressions use real GridSpawn → snapshots → Burst mesher → apply → pooled submission, with physics present, a 1920×1080 URP camera and production content. The fixture owns a separate world, restores cameras/lighting/pipeline and disposes its resources. SPEC-11 Q35 / ledger Q173 accounts for ADR-0032's pooled renderer in the original gate wording. The initial Unity-rendered Ship20k reference was visually inspected before storage; tests never replace it automatically. Its first setup check was corrected to wait until deferred spawning adds GridSpawning before querying the enable bit.
- **GPU validation:** **3 passed**. Ship20k: **MAE 0**, **341,662 nonblack pixels**, all **10 bricks** applied. HollowStation1M: **1,008,128 cells / 872 bricks**, inclusive render share **0.117376 / 0.173208 ms median/p95**. Lattice2500: static **0.131499 ms**, moving **0.161125 ms**, delta **0.029626 ms**. Reference CPU settings, 17 workers, 60 warm-up + 300 measured frames per state. **Zero tuning passes**; existing renderer allowance remains exhausted.
- **Full checks:** compile zero C# warnings; EditMode **765 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.4633 ms** vs 3; MeshBuild128 **1.0460 ms** vs 0.5); headless PlayMode **107 passed, 19 GPU skips**. Required perf wrapper ran headlessly: no EditMode match, two GPU skips, no new records, so wrapper exits nonzero; the real GPU run above supplies both passing perf records. Evidence `Logs/playtest/ren013/`, plus the checked-in reference PNG. Docs clean.
- **Next:** TST-004 combined voxel readiness/memory checks, then DEV-007/016/019 and M1 system review. No new owner decision, hull design or art change; approved Nocturne environment preserved.

### 2026-10-12 — AUD-010 done — astra (gpt-6)

- **Changed:** editor-only audio provenance types, saved inventory of all **19 clips** and release-build preprocessor. `ComfyUI_00018_.mp3` and `upbeat.mp3` are **Blocked** with the specified note; the other **17 are Unverified**, source carried over from the prototype, licence unknown. A temporary Unity CLI authoring script created the asset and was deleted. Development builds remain permitted; release validation lists every referenced clip without exactly one Confirmed row.
- **Validation:** all **four required tests pass**. Inventory coverage detects a newly unlisted clip; existing cues reference no blocked clip; release validation names both unverified test clips, accepts their confirmed state and rejects a blocked state; development validation passes. Compile zero C# warnings; full EditMode **765 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.4249 ms** vs 3; MeshBuild128 **1.1375 ms** vs 0.5). Evidence `Logs/playtest/aud010/`. No runtime/boot/flow changes; latest full PlayMode remains TST-003's **107 passed, 16 GPU skips**. No owned perf gate or optimization pass.
- **Next:** REN-013 rendering regressions, then voxel integration/harness checks and M1 system review. AUD-011 owner licence review remains pending under its documented default; it does not block development. AUD-007 still owns audible cue/config content. No new owner decision; approved environment unchanged.

### 2026-10-12 — TST-003 done — astra (gpt-6)

- **Changed:** shared GameFlowFixture loads the actual Boot scene and uses the production bootstrap, session, spawner, collider readiness, input context and save/settings services. It supplies wait/pause helpers, last-20-log timeout diagnostics, leak detection and idempotent cleanup. Existing FND-005 hooks already redirect SPEC-01/05 paths; SPEC-71 Q9 / ledger Q172 reconciles obsolete pseudo-APIs and grants Common test-only boot access. Null-graphics runs alone bypass renderer readiness; GPU runs preserve it. The splash hook remains staged until HUD-003 creates a splash.
- **Validation:** all **four required tests pass**, including NewGame(42), creative sandbox, real save and settings files beneath Temp/TestRuns, unchanged real profile/settings timestamps, and deletion/reset in teardown. Full PlayMode **107 passed, 16 GPU skips**; full EditMode **761 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.4681 ms** vs 3; MeshBuild128 **1.0440 ms** vs 0.5). Compile zero C# warnings, docs clean. Evidence `Logs/playtest/tst003/`. Zero optimization passes.
- **Next:** AUD-010 audio provenance, REN-013 rendering regressions, then remaining M1 integration checks and system review. No new owner decision or art change.

### 2026-10-12 — TST-002 done — astra (gpt-6)

- **Changed:** sparse PlayerCommand recording, invariant `.cmdrec` text/files and development replay after the production input sampler. ADR-0051 / Q171 registers the override and hook order, defines exclusive endpoints, preserves finite float bits and all absolute edge counters, and excludes transient FrameIndex from files. The guarantee is the specified even-tick scripts at 30/60/144 FPS, not arbitrary catch-up schedules.
- **Validation:** all **five required tests pass**: record/file round trip, identical **120-tick ShipCommand** sequences at all three frame rates, one Interact press exactly once, exclusive-end stop and line-numbered parse errors. Compile zero C# warnings; full EditMode **761 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.4060 ms** vs 3; MeshBuild128 **1.0364 ms** vs 0.5); full PlayMode **103 passed, 16 GPU skips**. Evidence `Logs/playtest/tst002/`. Zero optimization passes.
- **Next:** TST-003 real boot/flow fixture, then remaining M1 integration checks and system review. DEV-016 will expose recording/replay commands; no live-flight replay demonstration is claimed here. Approved environment unchanged; no new owner decision.

### 2026-10-12 — AUD-004 done — astra (gpt-6)

- **Changed:** the two required listener regressions. EditMode inspects every enabled build scene, including inactive objects. PlayMode traverses Boot → MainMenu → World → BlueprintLab → MainMenu through production SceneFlow, finding exactly one enabled listener owned by the persistent GameRoot and the same instance throughout; no missing-listener warning occurs.
- **Mutation proof:** a temporary CLI Editor script added a listener to Boot. The scene test failed with **Boot: AUD004_MutatedListener contains an AudioListener**. Restored the exact pre-mutation scene with cp and verified byte equality; temporary script/meta removed. Evidence `Logs/playtest/aud004/`.
- **Validation:** compile zero C# warnings; full EditMode **756 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.4690 ms** vs 3, MeshBuild128 **1.0300 ms** vs 0.5); full PlayMode **103 passed, 16 GPU skips**. The focused listener flow also passes. Its log assertion targets missing-listener messages; Unity still fails unexpected error logs, while the pre-existing GridSpawnSystem lookup performance warning is not promoted to a listener failure. No runtime changes, no tuning passes, no new owner decision.
- **Next:** TST-002 command record/replay, then remaining M1 checks and system review. Audio cue content remains AUD-007; no audible-gameplay claim. Approved environment unchanged.

### 2026-10-12 — VFX-001 done — astra (gpt-6)

- **Changed:** shared effect kinds, event helpers/queue, chunk dissolve property, content definitions and validation, GPU instance layouts/counters, and the specified unbound FxRig contract stub. ADR-0050 / Q168–170 resolves the Data→Voxel enum cycle, owns native producer handles through GuardedHandles, and uses the canonical scoped validator. Registered FxEventQueue in the voxel test's documented module-singleton allowlist. No new assembly reference, asset, art or boot call to the throwing rig stub.
- **Validation:** all **six required tests pass**. FxEvent **56 B**, GPU event **80 B**, tracer/beam/plume **48 B**, shield **224 B**. The lifetime check schedules two concurrent 128-event writers, waits through main-thread enqueue (**257 events**), then disposes the world with another writer pending: **zero leaked allocations**, queue and guard disposed. Required invalid content combinations report asset/field diagnostics.
- **Full suites:** EditMode **755 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.4139 ms** vs 3, MeshBuild128 **1.0436 ms** vs 0.5); PlayMode **102 passed, 16 GPU skips**. Final compile has zero C# warnings and all six focused tests pass again after completing the readonly quality-budget accessors. Evidence `Logs/playtest/vfx001/`; docs clean. No performance gate owned by this contract task, zero optimization passes.
- **Next:** AUD-004 listener scene/flow regression, then remaining M1 checks and system review. FxRig resources/binding are VFX-002, draining VFX-003, actual effects later M2 tasks. AUD-006 must reconcile its existing SFX producer/drain guard ownership before asynchronous producers. No new owner decision; approved Nocturne environment preserved.

### 2026-10-12 — DEV-015 done — astra (gpt-6)

- **Changed:** F1 opens a themed code-built console through the existing navigator, with TextEntry context, world-pointer blocking and no simulation pause. Shared registry commands, 200 output lines, 50-entry history, Tab completion and pending-operation completion are connected. ADR-0049 / Q167 adds an explicit UIScreen programmatic-root fallback while retaining configured UXML behavior. Development guards remain intact.
- **Live game:** help appears, Up recalls help, newg completes to newgame; F1 closes and restores Flight. Real W input reached **14.64485 m/s** afterwards. Screenshot `Logs/playtest/fighter-dev-console.png`; evidence `Logs/playtest/dev015/`. No runtime errors. Live text-field keys need GameView KeyDown events rather than input-state injection; recipe recorded in 08-tools.
- **Validation:** all three required tests pass, including a real asynchronous newgame command. **0 B over 10,000 closed updates**, also passing in the separate Performance-category run. Perf wrapper reports no EditMode match or PerfRun JSON, so its wrapper exit is not claimed as a passing benchmark. Zero optimization passes.
- **Full suites:** compile zero C# warnings; EditMode **749 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.4777 ms** vs 3, MeshBuild128 **1.0393 ms** vs 0.5); headless PlayMode **102 passed, 16 GPU skips**. Docs clean. The GPU Editor shutdown reports four persistent allocations, matching the prior FND-019 shutdown log; retain both for M1 system-review diagnosis, separate from the clear runtime checks.
- **Next:** VFX-001 shared effect contracts, then remaining M1 integration and review. No new owner decision or art; preserve approved Nocturne environment. HANDOFF updated.

### 2026-10-12 — AUD-005 done — astra (gpt-6)

- **Changed:** UI cues use eight existing voices round-robin, cached cue lookup, shared per-cue cooldowns and nonrepeating clip selection. PlayCue and the 2D PlayCueAt path route through existing pools and buses. Stingers replace the prior one-shot and duck both music voices from retained base gains, without multiplying settings/source attenuation repeatedly. Q166 / SPEC-64 Q15 records the staged allocator/director integration; spatial allocation remains AUD-006 and music direction AUD-008.
- **Validation:** all four required PlayMode tests pass, including the ninth UI start stealing voice 0, shared enum/id cooldown, 2D cue routing, −8 dB (**0.398107**) duck factor after **0.2 s** supplied update time and restored base volumes after the **1 s** release. **0 B managed allocations over 1,000 warmed PlayUi calls**, also passed in the separate Performance-category run. Perf wrapper has no matching EditMode test or PerfRun JSON for this allocation assertion; its passing PlayMode result is retained. No optimization passes.
- **Full suites:** compile zero C# warnings; EditMode **749 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.4199 ms** vs 3, MeshBuild128 **1.0917 ms** vs 0.5); headless PlayMode **99 passed, 16 GPU skips**. Evidence `Logs/playtest/aud005/`; docs clean.
- **Boundary / next:** clips in tests are in-memory silent test data, not production assets. Null-config boot remains silent until AUD-007 registers cue content; no audible gameplay claim. No owner decision or new art. DEV-015 console next, then remaining M1 integration/review; performance gaps remain deferred.

### 2026-10-12 — REN-014 done — astra (gpt-6)

- **Changed:** PropRenderSystem gathers applied prop lists into flat mesh-sorted storage on change, uploads instance buffers only when needed, and draws once per mesh. GridMatrixJob updates ship transforms and conservative bounds every frame. Q165 / SPEC-11 Q34 fixes stale gather-only bounds. GPU resources share the existing world-owned managed state and dispose safely regardless of system teardown order.
- **Validation:** all four required GPU tests pass. Actual voxel cells pass through BrickSnapshotJob → BrickMeshJob → BrickMeshApplySystem → PropRenderSystem; clearing the cells removes the rendered prop. Pixel-area and moved-camera checks show the full test quad before/after moving 100 m; three world disposal cycles release buffers without leak warnings. No production test assets or art were created. Images/results: `Logs/playtest/ren014/`.
- **Measured:** 10,000 props on 20 moving grids: **0.01465 ms median / 0.0191 ms p95** steady update; rebuild **0.2838 ms median / 0.3015 ms p95**, against advisory 0.3 / 1 ms. **Zero optimization passes.** The required `tools/test.sh perf PropRenderTests` headless invocation has no EditMode match and skips its GPU PlayMode test; the actual measurement is from the separate passing GPU run, not that headless command.
- **Full suites:** compile zero C# warnings; EditMode **749 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.6408 ms** vs 3; MeshBuild128 **1.0536 ms** vs 0.5); headless PlayMode **95 passed, 16 GPU skips**. After the final test-only change to exercise actual cells, all four GPU tests pass again. No renderer tuning or new owner decision. Docs clean.
- **Next:** AUD-005 playback/cooldowns/stinger ducking, then remaining M1 integration and system review. Audio cue content remains AUD-007, so do not claim audible gameplay yet. HANDOFF updated; approved Nocturne appearance retained.

### 2026-10-12 — FND-019 done — astra (gpt-6)

- **Changed:** defer the scene environment's Start by one frame so a direct World reload cannot borrow the persistent camera's Skybox pending destruction. Existing ownership and exit cleanup remain intact. Q164 / SPEC-01 Q27 records the lifetime correction; no appearance change or new owner decision.
- **Validation:** regression failed on restart 1 before the fix; both environment tests now pass. Compile has zero C# warnings. Full EditMode **749 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.4134 ms** vs 3; MeshBuild128 **1.1039 ms** vs 0.5); full PlayMode **95 passed, 12 GPU skips**. No optimization passes; numeric misses deferred. Docs clean.
- **Live:** three consecutive direct NewGame calls retain the same camera, enabled Game/NocturneSky, brightness **1**, animation speed **0.012**, one environment and **1,400 particles**; zero runtime errors. Inspected capture `Logs/playtest/fighter-restart-sky-fixed.png`; proof and test results `Logs/playtest/fnd019/`.
- **Next:** REN-014 prop instancing, remaining ready M1 integration and system review. Preserve approved Nocturne brightness/star density and the physical planet. HANDOFF updated.

### 2026-10-12 — REN-012 done — astra (gpt-6)

- **Implementation:** `6b2f87e` adds GridLodSystem and its three jobs: grid dormancy from player distance, brick LOD from camera distance, conditional writes and guarded serial enqueue. Q162 / SPEC-11 Q32 repairs invalid-viewer restoration and late opposite-LOD mesh apply by watching both desired and applied state. Q163 / Q33 uses full inclusive cell bounds and root scale. No art/material changes.
- **Live game:** a second imported Fighter at (120, 0, 600) applied LOD1 on all **4/4 bricks**, then LOD0 on **4/4** during real W+boost flight. Player z **289.4137 m**, peak **72.65659 m/s**, Flight context; zero runtime errors. Inspected capture `Logs/playtest/fighter-distance-lod.png` (simulation held after the crossing, rendering active). Evidence `Logs/playtest/ren012/`. An initial demonstration waited too long after braking and overshot; the final check records the crossing itself.
- **Validation:** all six required checks pass. Compile zero C# warnings; full EditMode **749 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.3959 ms** vs 3; MeshBuild128 **1.1392 ms** vs 0.5). Full PlayMode **94 passed, 0 failed, 12 GPU skips**. Dedicated filtered perf records **0.00555 ms median / 0.0181 ms p95** scheduling 5,000 bricks against 0.1 ms; 0 initial tuning passes. The perf command exits 1 because its PlayMode half has no matching tests; its one actual gate passes, and the full PlayMode suite passes separately. Docs 509 tasks, clean.
- **Discovered follow-up / next:** FND-019 records a reproducible existing environment restart defect: direct NewGame within Play can lose the persistent camera's Skybox override. Fresh Play restores the approved procedural sky. `restart-missing-sky.png` and the observation note preserve evidence. Fix FND-019 next, then return to remaining M1 rendering/props and integration; do not retune the approved look. HANDOFF updated.

### 2026-10-12 — INP-007 done — astra (gpt-6)

- **Implementation:** `4942ca7` adds the two specified PlayMode tests using real queued keyboard events → PlayerInputSampleSystem → fixed-step PlayerCommandApplySystem → per-tick ShipCommand history. Time.captureDeltaTime drives 30/60/144 FPS. No production changes were needed.
- **Measured behavior:** each rate produces **120 ticks, 4 Interact, 3 Ability1 and 60 held-thrust ticks / 1.000000 s**. At 30 FPS all 60 rendered frames run two ticks; at 144 FPS **168 frames run zero ticks**, including an initial one-frame press that is retained correctly. Both required tests pass.
- **Validation:** compile zero C# warnings; full EditMode **743 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.4942 ms** vs 3; MeshBuild128 **1.1066 ms** vs 0.5). Full PlayMode **94 passed, 0 failed, 12 GPU skips**. Evidence `Logs/playtest/inp007/`; docs clean; no tuning.
- **Next:** REN-012 connects existing LOD meshes to camera distance and dormancy, followed by remaining ready M1 integration and system review. Approved Nocturne appearance unchanged; no new owner decision.

### 2026-10-12 — INP-006 done — astra (gpt-6)

- **Implementation:** `a90d5ec` adds the camera Editor branch (held orbit/pan, Ctrl-gated zoom, focus/view edges) and App-host sampler for the canonical frame and all 34 action bits. Disabled Editor map clears the frame; pointer permission is sampled through the UI gate. Existing Flight behavior is preserved. The full Blueprint Lab host remains EDT-024.
- **Resolved defect:** Q161 / SPEC-03 Q9 / SPEC-51 Q22, accepted ADR-0048 (`270bc4e`): the sampler lives in App/EditorHost as SPEC-51 already requires, avoiding a forbidden Presentation → ShipEditor reference. INP-006 declares the exact two input types early; EDT-001/024 reuse them. No assembly changes or substitute enum.
- **Validation:** all four required tests pass with real Input System devices: held-only orbit, one sensitivity/inversion application through the camera rig, Ctrl-wheel separation, Ctrl-Z without MirrorZ. Full EditMode **743 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.5094 ms** vs 3; MeshBuild128 **1.0316 ms** vs 0.5). Full PlayMode **92 passed, 0 failed, 12 GPU skips**. Compile zero C# warnings; docs clean. Evidence `Logs/playtest/inp006/`. No performance tuning.
- **Next:** INP-007 real input at 30/60/144 FPS, then remaining M1 rendering and integration. No change to the approved environment or new owner choice.

### 2026-10-12 — UI-007 done — astra (gpt-6)

- **Implementation:** `ce4b615` adds nine token-only component sheets, all base control variants/states, KeyHint binding text and the actual UXML controls gallery. Game.tss imports the components; the existing pause button uses the shared styling. Q160 / SPEC-60 Q19 records empty binding display behavior. Final HUD and contrast audit remain their specified later tasks (UI-012 / HUD-002).
- **Validation:** both required PlayMode tests pass; full PlayMode **92 passed, 0 failed, 12 GPU skips**. Compile zero C# warnings. Full EditMode **739 passed, 2 advisory timing misses, 1 explicit skip**: Write1MiB **4.2874 ms** vs 3; MeshBuild128 **1.1677 ms** vs 0.5. All functional checks and the theme token audit pass. Evidence `Logs/playtest/ui007/`. No tuning passes; timing gaps remain deferred.
- **Next:** INP-006 camera/editor samplers, then remaining M1 integration. Preserve the approved Nocturne environment. No new owner decision or game screenshot in this controls-only task.

### 2026-10-12 — FND-018 done — astra (gpt-6)

- **Implementation:** `c55d61f` connects the animated procedural Nocturne sky, a physical 1,400 m radius storm planet at (1400, −2000, 1700), its separate atmosphere shell and 1,400 stationary world-space cosmetic particles. Reused the own-work look-book surface source; fixed a UV bake seam. No new ships, AI assets, simulation writers or celestial save data. SYS-011 replaces the staged planet.
- **Owner directions:** “the planet should not be baked into the background, it should be a phisical sphere in the background with the texture and atmosphere so the ships can move above and past it”; then “can you make the background procedural and animated”. Both implemented; the sky texture was removed. Owner rejected reducing the first procedural rendering: **“no don't reduce its good”**. Its original brightness/star density are restored and binding in DESIGN-GUIDE §13 decision 7. Q159 / SPEC-01 Q26 records staging.
- **Actual game:** real boosted flight reached **120 m/s**, player z **7497.969 m**; planet stayed fixed while its camera depth changed **+2692.55 → −1605.44 m**, proving it was passed. Zero runtime errors. The final procedural-sky screenshot is `Logs/playtest/nocturne-procedural-sky.png`; movement/geometry evidence is in `Logs/playtest/fnd018/`. Earlier bake screenshots there are superseded and are not the final implementation.
- **Validation:** compile zero C# warnings; full EditMode **739 passed, 2 advisory timing misses, 1 explicit skip** (Write1MiB **3.4936 ms** vs 3; MeshBuild128 **1.1713 ms** vs 0.5); full headless PlayMode **90 passed, 12 GPU-only skips**. Required scene/motion/cleanup test passed in GPU mode and full headless mode. Docs 508 tasks, zero errors/warnings. No performance tuning; timing misses remain deferred under ADR-0042.
- **Next:** owner: “ok continue on with your main work you were doing before I asked you to add background and planet”. Resume UI-007, then remaining ready M1 input/rendering/integration and system review. Preserve the approved environment. HANDOFF updated.

### 2026-10-12 — UI-006 done — astra (gpt-6)

- **Implementation:** `25da402` connects UIRoot converters, immutable number strings and actual UXML notifications; Q158 resolves clock formatting. Six required EditMode and three PlayMode tests pass; unchanged values do not poll and notification exercise allocates 0 B.
- **Validation:** compile zero C# warnings; full EditMode 740 passed, one advisory MeshBuild128 miss (0.8894 ms), one explicit skip; PlayMode 89 passed, 12 GPU-only skips. Evidence `Logs/playtest/ui006/`. No optimization passes.
- **Owner steering / next:** “can you add the background from nocturne to the game, and a big placeholder planet (same as nocturne), and some small particle debris so we have some spacial reference when moving across because right now its improssible to tell if its moving at all. follow the nocturne art direction”. Prioritize this playable World presentation before UI-007; preserve approved reference and use procedural assets.

### 2026-10-12 — INP-004 done — astra (gpt-6)

- **Implementation 5fcd282:** the specified `ContextIntegrationTests.HeldKey_AcrossPause` uses the real InputService, InputServiceBridge, UINavigator, PauseService and PlayerInputSampleSystem in an isolated ECS world. A genuine Interact press seeds a nonzero counter; W stays held across opening a pausing screen and closing it through injected Escape / Global.Back.
- **Verified behavior:** pause disables Flight and produces **Thrust 0 in the same frame**, with timeScale 0 and unchanged edge counters. Closing restores Flight; the next Input System initial-state check restores **Thrust 1** without a new W press. All ten edge counters retain their values through pause, close and the following frame. No production fix was needed. Test devices, action ownership, native world and manual-update setting are cleaned up.
- **Validation:** focused PlayMode **1/1 passed**; compile **zero C# warnings**; full EditMode **734 pass, 1 advisory timing miss, 1 explicit skip** (MeshBuild128 **0.8795 ms** vs 0.5, performance deferred); full headless PlayMode **86 pass, 12 GPU-only skips**. Docs **507 tasks / zero errors**. Zero optimization passes. Evidence `Logs/playtest/inp004/`; pre-run QualitySettings restored.
- **Next:** UI-006 binding/converters, then UI-007 usable controls and remaining M1 tasks. AI-001 and WPN-001 are merged, but combat behavior remains M2. DEV-009 still needs the implemented canonical spawner and world-coordinate services. No new visual result or owner decision; the Fighter flight screenshot remains current. HANDOFF updated.

### 2026-10-12 — AI-001 done — astra (gpt-6)

- **Implementation d5f0523:** filled the canonical SpatialHash contract and added spatial proxy/body/state types, AI profile/config/state/brain/combat status, setup and content signatures, fleet membership, formation definitions and ability rules. This opens the targeting/hash/spawner implementation dependencies; AI flight/combat behavior remains in its M2 tasks, not claimed playable here.
- **Resolved Q157 / SPEC-23 Q6:** preserved the explicit **64 B** SpatialProxy contract with sequential trailing padding (listed fields occupy 48 B); no field removed or reordered and no optimization pass. Staged the canonical AbilityDefinition content identity for ABL-001; included the spec's ability-rule types omitted from its older task file list. Content lists are read-only; AI-E002 validation signature is staged for AI-003.
- **Validation:** compile **zero C# warnings**; both specified AiContractTests pass. Full EditMode **734 pass, 1 advisory timing miss, 1 explicit skip** (MeshBuild128 **0.8887 ms** vs 0.5; performance deferred, zero tuning). Full headless PlayMode **85 pass, 12 GPU-only skips**. Docs **507 tasks / zero errors**. Evidence `Logs/playtest/ai001/`; pre-run QualitySettings restored.
- **Next:** INP-004 real input-bridge held-key pause/resume regression, then remaining M1 integrations. DEV-009 remains blocked on implemented canonical spawn/world-coordinate services; contract-only Ready on FND-012 does not prove those services exist. No visual change or new owner decision; the verified Fighter flight screenshot remains current.

### 2026-10-12 — WPN-001 done — astra (gpt-6)

- **Implementation c250d87:** weapon definitions/tuning, baked data, mount/aim/fire/beam/target/stats components, projectile store and shot contracts, OwnedBy attribution, Allegiance and default faction relationships. ProjectileRow is **64 B**; all prescribed unmanaged checks pass; player/hostile factions are mutually hostile, neutral faction stays neutral, diagonals friendly. Runtime fire/aim/hit algorithms remain explicitly staged for their M2 tasks; this does not claim shooting is playable yet.
- **Resolved Q156 / SPEC-21 Q11:** include stats/shot/delivery types omitted from the older file list; stage only the canonical SpatialHash identity needed by CombatHitTest (AI-001 fills it), and the prescribed FxDefinition shell (VFX-001 fills it). Existing WeaponShot fields measure **80 B**, correcting its stale comment without changing fields. No assembly-reference or architecture changes; ShipDestroyed remains WPN-017.
- **Validation:** compile **zero C# warnings**; full EditMode **732 passes, 1 advisory timing miss, 1 explicit skip**, with all three WeaponContractTests passing. MeshBuild128 **0.8857 ms** vs 0.5 remains performance deferred, **zero optimization passes**. Full headless PlayMode **85 pass, 12 GPU-only skips**. Docs **507 tasks / zero errors**. Evidence copied under `Logs/playtest/wpn001/`; incidental QualitySettings restored from the pre-run backup.
- **Playable evidence retained:** the latest actual game screenshot remains `Logs/playtest/fighter-framing-corrected.png` (53.38865 m/s under W, complete imported Fighter visible), shown again to Frank. This contract task changes no visual result. The scene still lacks a backdrop and finished HUD/presentation.
- **Next:** AI-001 is now ready; complete canonical spatial/AI contracts toward the ship spawner and combat. DEV-009 remains blocked by implemented FND-012 and SPEC-30/31 coordinates, not by a performance target. HANDOFF updated; no new owner decision or art asset.

### 2026-10-12 — DEV-009 dependency audit — astra (gpt-6)

- The task's contract dependencies are done, but its mandated `ShipSpawnService` (FND-012, M2), `Allegiance`/AI contracts and `FrameOrigin`/`UniversePosition`/current-star-system services do not exist. No runtime implementation was made and no acceptance criterion is claimed.
- Resolved Q155 / SPEC-70 Q20: keep the complete task **blocked** on those canonical implementations, even though contract-only Ready remains yes. Do not bypass the spawn owner, ignore requested AI/allegiance, fabricate universe positions, or mark a local-only subset done. This does not block the active goal: WPN-001 is ready and opens AI-001, both prerequisites of FND-012; other M1 tasks are ready too.
- Next: implement WPN-001, then continue the remaining M1 work. Revisit DEV-009 when the canonical spawner and coordinate services are available. Docs consistency passes; no Unity code changed.

### 2026-10-12 — CAM-012 done — astra (gpt-6)

- **Implementation 42287c3:** the camera bridge supplies actual conservative solid bounds (CellMin/CellMax), not brick allocation extents. After the unchanged follow filter, the rig retains the largest desired focus displacement that keeps all eight bounds corners inside the camera frustum. If deliberate close inspection cannot fit the hull even when centered, the output centers it. Requested distance, pitch, yaw, input targets and damping state remain intact. Internal bounds handoff only; no new art or public contract.
- **Resolved Q154 / SPEC-62 Q15:** analytic clipping of the output translation against the camera's six frustum planes; Focus/DesiredFlightPosition use the corrected focus. Cached world bounds must shift with the future CAM-006 origin warp, while the framing displacement stays unchanged. Frank can review the close-zoom follow feel; no owner gate was introduced.
- **Actual GPU game:** isolated launch **1.365 s**; real W for 7.3 s reached **53.38865 m/s**. At the same ten-notch zoom and orbit as the prior clipped view, distance **41.77738 m**, yaw **24.99996°**, pitch **50.00005°**; projected solid bounds now lie within **x [0.3420966, 0.7831935], y [0, 0.6924891]**. The inspected capture `Logs/playtest/fighter-framing-corrected.png` shows the full Fighter and was shown to Frank. Before: `Logs/playtest/fighter-input-isolation.png`. Raw evidence under `Logs/playtest/cam012/`; **zero runtime errors**. Editor closed; incidental settings restored, including the headless importer version rewrite (original QualitySettings backup verified against the initial clean Git blob).
- **Validation:** compile **zero C# warnings**; full EditMode **729 pass, 1 advisory timing miss, 1 explicit skip** (MeshBuild128 **0.8737 ms** vs 0.5). Full headless PlayMode **85 pass, 12 GPU-only skips**; both new framing tests run against the real imported 1,005-cell Fighter at 53 m/s, with turns/orbit changes at 16:9/4:3, plus deliberate close inspection preserving input. Existing camera regression/performance tests pass: inclusive bridge median/p95 **0.005208/0.005708 ms** and **0 B/update**. Docs **507 tasks / zero errors**. Zero optimization passes.
- **Test cleanup correction:** the shared production fixture now settles the flow and quits an active test session before restoring its boot root. Previously restoring from World started an async quick-start that the next fixture destroyed, causing a disposed-World error before camera assertions. New tests also use the real scroll binding's 0.008333 scale and a 1 cm settling tolerance, rather than assuming exact 1/120 arithmetic. No assertions were removed.
- **Next:** ready DEV-009 spawn/teleport/grid inspection commands for repeatable multi-ship collision scenarios, then remaining M1 integrations and system review. Keep performance deferred; retain owner-only art boundaries. HANDOFF updated.

### 2026-10-12 — UI-005 done — astra (gpt-6)

- **Implementation 4cdc9a9:** root focus callbacks derive TextEntry context; Back blurs typing before navigating. The world-pointer gate blocks pickable UI, blocking screens and the stack-change frame plus one. UIRoot implements/registers the existing Core gate and is bound to the production PlayerInputSampleSystem; disposal unregisters callbacks and the interface slot. Seven prescribed PlayMode tests exercise real runtime panels and focus.
- **Live integration found and fixed Q153 / SPEC-60 Q17:** the first headless tests passed but real HUD clicks still set fire. An in-sampler probe proved `Screen.height` was **1427** (Editor window) while the game panel was **1080**; mouse (164,958) became y469 instead of y122. Convert to panel scale first, then flip against the actual panel height. Pointer tests now use the RenderTexture viewport independently of `Screen.height`, removing the shared mistaken assumption. CLI mouse state also differs from player-loop state, so final evidence was recorded by a temporary LateUpdate probe in the real game.
- **Actual GPU game after correction:** isolated launch **2.035 s**; held left mouse over the HUD button gave **gate=false/fire=0**, over empty world **gate=true/fire=1**. W gave thrust (0,1); focusing a field produced **TextEntry/thrust=0/fire=0**. With pause open and the field focused, the first real Escape restored Menu without closing, the second restored Flight. Real W moved the ship again (final forward velocity **53.19214 m/s**); **zero runtime errors**. Temporary test UI/probes were removed with Play exit; Editor closed and incidental settings restored. Evidence: `Logs/playtest/ui005/`, especially `live-input.txt`; the `*-before-fix*` files document the defect, not the final result.
- **Validation after the fix:** compile **zero C# warnings**; full EditMode **728 pass, 2 advisory timing failures, 1 explicit skip** (Write1MiB **3.4669 ms** vs 3; MeshBuild128 **1.0529 ms** vs 0.5). Full headless PlayMode **83 pass, 12 GPU-only skips**, including all seven new tests. Docs **507 tasks / zero errors**. **Zero optimization passes.**
- **Next gameplay finding:** the inspected actual capture `Logs/playtest/fighter-input-isolation.png` clips the Fighter at the bottom during fast close-zoom flight. Added ready **CAM-012** to SPEC-62 for bounds-aware framing and real before/after evidence; take it next, before UI support work. INP-004 and UI-006/007 remain ready. HANDOFF updated. No owner art decision or new asset was introduced.

### 2026-10-12 — UI-004 done — astra (gpt-6)

- **Implementation 23b3cf5:** UINavigator owns cached screen construction, stack/root operations, deferred callback navigation, one pause token, input context, covered screens, HUD hiding, focus and open/close sounds. Removes ADR-0044's direct input writer. The temporary App adapter selects transparent scene roots and an external-pause blocker through navigation only; World Back opens a minimal Paused/Resume overlay. HUD-002 replaces these temporary views/adapter with the full scene UI. No new art or performance tuning.
- **Resolved Q151–152 / SPEC-60 Q15–16 / ADR-0047:** bound both pending and drained reentrant operations to 16; failed pushes do not focus/play open sound. Stage real scene context through the navigator until the M3 screen roots exist, preserving the playable Fighter and external pause safety.
- **Validation:** compile **zero C# warnings**; all **15 prescribed navigator tests pass**, including **0 B across 10,000 Sync calls**. Full EditMode **728 pass, 2 advisory timing failures, 1 explicit skip**: Write1MiB **3.4472 ms** vs 3 ms, MeshBuild128 **1.0427 ms** vs 0.5 ms. Both deferred with zero tuning passes. Full headless PlayMode **76 pass, 12 GPU-only skips**, including real flight/pause/save-load regressions. Docs **506 tasks / zero errors**.
- **Actual GPU game:** `newgame.sh 42` launched in **2.027 s**. Real W reached **14.64485 m/s**; Escape opened Menu with one pause token. Resume click and a separate Escape close each restored Flight with zero tokens. Another W input reached **21.97824 m/s**. Live error log **zero errors**. Actual captures `Logs/playtest/fighter-navigation-paused.png` and `fighter-navigation-flight.png` were inspected and the paused view shown to Frank; raw evidence under `Logs/playtest/ui004/`. Focus/repaint the Game view before capture if its background render target is stale. Editor closed and incidental settings restored from backups.
- **Next:** UI-005 focus and pointer gating, so UI interaction cannot leak into flight/world clicks. UI-006 binding and UI-007 controls follow. Full HUD/menu design, backdrop and final art remain pending. HANDOFF updated; numeric misses do not block later work.

### 2026-10-12 — UI-003 done — astra (gpt-6)

- **Implementation fdade56:** boot step 9 now constructs the persistent UIRoot/InputServiceBridge from GameRoot's configured UIDocument/UIConfig. The root validates config/layers, caches screen assets, supports registration/prewarm, owns dynamic click/hover sound hooks and tears down callbacks/registrations. AppUiSoundSink honors configured cue overrides or explicit semantic mapping (UI/audio ordinals differ). Composition tests now include the UI construction/disposal step. No new screens, art or performance tuning.
- **Resolved Q148–150 / SPEC-60 Q12–14:** registration/build is staged before UI-004 stack operations; Back/context ownership remains with ADR-0044 until its coordinated removal. Focus/picking, converters, tooltip/toast ticks, dialogs and accessibility follow their owning tasks. Accepted ADR-0046 adds the installed Unity.InputSystem assembly reference needed by the prescribed bridge (no package change). Confirm maps to audio Success; actual audio playback/content remains its owning tasks.
- **Validation:** compile **zero C# warnings**; full EditMode **714 pass, 1 advisory failure, 1 explicit skip** (MeshBuild128 **0.8705 ms** vs 0.5); final full PlayMode **76 pass, 12 GPU-only skips**, including all five new UIRootTests and the existing playable/save-load checks. Docs **506 tasks / zero errors**.
- **Actual GPU game:** isolated `newgame.sh 42` launched in **1.740 s**, root service/document available with all six layers, real W reached **14.523 m/s**, Escape gave paused/None then resumed/Flight. Live error log **zero errors**. `Logs/playtest/fighter-ui-root.png` confirms the transparent empty UI leaves the Fighter visible; raw evidence under `Logs/playtest/ui003/`. The existing first-spawn lookup-performance warning remains advisory. Editor closed and incidental settings restored from backups.
- **Next:** UI-004 navigation (or ready UI-006/007 support), then UI-005 pointer/focus integration. **Do not strand flight in None by simply removing the fallback while the navigator has no scene roots:** HUD-002's root screens are later M3 work. Resolve that staging at the handover while preserving existing flight/Escape tests and the actual playable loop. Full HUD/menu presentation is still pending; this checkpoint connects the root without claiming visible screens.

### 2026-10-12 — UI-002 done — astra (gpt-6)

- **Implementation a494f84:** token-based base/utility sheets and ordered Game.tss imports, an accessibility sheet reserved for UI-013, 1920×1080 / MatchWidthOrHeight 0.5 PanelSettings, UIConfig links, and transparent UIRoot with six non-picking layers in the canonical order. Assets authored through the headless Unity temporary-script recipe; the script and meta were removed. Existing tokens and fonts unchanged; component/screen imports follow as those tasks land.
- **Validation:** all three ThemeFilesTests pass against imported assets and UI-owned styles. Compile **zero C# warnings**; full EditMode **714 pass, 1 advisory failure, 1 explicit skip** (MeshBuild128 **0.8698 ms** vs 0.5); full PlayMode **71 pass, 12 GPU-only skips**. Docs **506 tasks / zero errors**. No optimization passes; no new assets requiring art approval.
- **Resolved Q147 / SPEC-60 Q11:** Directions/ contains SPEC-80 token sources, so it is exempt from the no-literal UI style audit alongside tokens.uss/accessibility.uss. Component and screen sheets remain audited. No production style assertion was disabled.
- **Next:** UI-003 root/service composition and UI-004 navigator, then UI-005 focus/pointer integration; UI-006 binding support is also ready. Preserve the single input-context owner during that handover. No visible game change in this asset-only task: the latest real Fighter screenshot remains `Logs/playtest/fighter-command-launch.png`. HANDOFF updated.

### 2026-10-12 — UI-001 done — astra (gpt-6)

- **Implementation 01dcff0:** all 37 specified UI files: data/config types, service interfaces, complete UIScreen/ViewModelBase/TooltipContent/null sound/frame clock, explicit throwing service/control shells, stable UINames and all **123** catalogue classes. No service registration or change to current Flight context; shells are not wired into boot.
- **Validation:** compile **zero C# warnings**; the two required UIContractTests pass; full EditMode **711 pass, 1 advisory failure, 1 explicit skip** (unchanged MeshBuild128 **0.8759 ms** vs 0.5); full PlayMode **71 pass, 12 GPU-only skips**. Docs **506 tasks / zero errors**. No optimization or new art decisions.
- **Next:** UI-002 theme/panel/root assets, then UI-003/UI-004 composition/navigation. Existing tokens remain placeholder values until ART-014; UI tasks must not silently replace them. The imported Fighter remains the demonstrated playable slice; this contract task has no visual change to screenshot.

### 2026-10-12 — DEV-008 done — astra (gpt-6)

- **Implementation 0b4269c:** runtime newgame/sandbox commands and typed player.exists/player.ready probes call production flow, physics and input readiness. CLI wrappers enter Play from Boot asynchronously; newgame.sh polls the real operation within a 90 s deadline and prints a snapshot. Shared production test fixture retains real boot and isolated disk paths. Removed obsolete parser fixtures now provided by production registrations.
- **Live game:** from edit mode, `tools/playtest/newgame.sh 42` reached readiness in **1.370 s**; repeat **0.229 s**. Both player id **1**, spawn **(0,0,0)**. Real W input reached **14.64485 m/s** and z **102.1245 m** at observation. Actual GPU game screenshot `Logs/playtest/fighter-command-launch.png` shown to Frank; evidence under `Logs/playtest/dev008/`. Empty sandbox seed 17 completed with GameplayActive, no player, and false player probes. Old World autosave stayed in isolated **p001/auto_0**, **1,740 B**. No live errors; one existing GridSpawnSystem lookup-performance warning on first spawn, left advisory.
- **Validation:** compile **zero C# warnings**; full EditMode **709 pass, 1 advisory failure, 1 explicit skip** (MeshBuild128 **0.8758 ms** vs 0.5); full headless PlayMode **71 pass, 12 GPU-only skips**, including all three required command tests and the existing flight integration tests. Docs **506 tasks / zero errors**. No optimization passes. Unity settings restored from pre-run backups; no temporary asset scripts.
- **Resolved gaps:** Q144 / SPEC-70 Q17 explicitly rejects a nonempty start-scenario argument until that content/consumer exists; the default uses the configured Fighter. Q145 / Q18 makes pre-boot dispatch asynchronous and applies/restores the intro hook around the boot it owns. Q146 / Q19 prevents switching an active player session's profile before sandbox autosave; it requires harness ownership. None needs owner input.
- **Next:** UI-001 onward through the ready M1 UI foundation and navigation, preserving the working flight loop. Full HUD screens remain in M3 with their existing dependencies. No benchmark tuning; no new art decisions. HANDOFF updated with the one-command launch recipe.

### 2026-10-12 — FND-017 done — astra (gpt-6)

- **First actual playable checkpoint delivered.** Existing imported Fighter (**1,005 cells**) replaces the four-cell starter. Production pooled hulls and functional meshes render under the approved camera-relative key/fill/rim/ambient lighting. Real Input System W/S, A/D, Q/E, Shift/X and mouse orbit/zoom reach the existing flight/physics/camera systems. Escape pauses/resumes. Implementation **0e253e1**, no new ship designs or AI art.
- **Running-game evidence:** `Logs/playtest/fighter-after-save-load.png` (loaded ship, flown again, camera orbit) and `Logs/playtest/fighter-contact.png` (two imported ships in physical contact). Scripts and raw observations are under `Logs/playtest/flight-checkpoint/`; screenshots were shown to Frank. Two-second W input produced **14.523 m/s**; the deterministic collision run reached **16.93755 m/s** and rebounded at **−1.803858 m/s**, at z **19.72989 m** against a static imported Fighter at z 35. Commands used real input and GridSpawnApi, not direct velocity writes.
- **Live save/load:** isolated harness profile p003/manual_017, **1,743 B** save, player id **1**, blueprint ship_fighter, **1,005 cells**, voxel hash **58AE4237CE09B3B7** before/after. The loaded ship moved **2.010558 m** under real W input. Load reported **0 errors / 0 warnings**; the final live run had **zero errors**. FLET v1 does not preserve pose/velocity: it currently respawns at origin/zero (Q142). No HUD, galaxy backdrop or final functional-block presentation yet.
- **Integrated defects/decisions:** Q142 / accepted ADR-0044 assigns temporary sole input-context ownership to the App scene/pause bridge before sampling; **UI-004 must remove/unbind it** before taking ownership. Q143 / accepted ADR-0045 fixes the actual live restart error: paused fixed-step cleanup left PhysicsTemporalCoherenceInfo entities. Teardown now drains the existing package physics initialization and end-fixed ECB at zero delta, without simulating or manually removing coherence data. Trap T2.15 added. DEV-008 follows this checkpoint because its ready predicate needs Flight.
- **Validation:** compile **zero C# warnings**; all **3 GPU production-world integration tests pass** (`Logs/test-results/fnd017-gpu.xml`); full EditMode **709 pass, 1 advisory failure, 1 explicit skipped** (unchanged MeshBuild128 **0.8767 ms** vs 0.5 ms); full headless PlayMode **68 passed + 12 GPU-only skipped**. Docs **506 tasks / zero errors**. No optimization passes; the existing timing gap remains deferred. Temporary scene-authoring script removed; incidental Unity settings restored from backups.
- **Next:** DEV-008 new-game/sandbox entry and repeatable launch, then ready M1 UI/navigation/flight HUD and remaining systems. Owner review items are the provisional starter/art/tuning and the explicitly documented temporary context/persistence choices; none blocks gameplay progress. HANDOFF §2–3 updated.

### 2026-10-12 — INP-005 done — astra (gpt-6)

- Bound real Flight actions to the pre-boot-snapshot PlayerCommand singleton: analog controls, boost/brake, gated fire, latched action counters and physics picking excluding the player with flight-plane fallback. Unbound/disabled input is neutral. Q141 records the temporary permissive pointer gate until UI-003 and explicit physics dependency completion. Implementation **66702c3**.
- Validation: compile **zero C# warnings**; all **10** prescribed input tests pass including **0 B** warmed sampling; full EditMode **709 passed, 1 advisory timing failure, 1 explicit skipped** (known MeshBuild128 **0.8771 ms** vs 0.5 ms, unchanged/deferred); PlayMode **65 passed + 12 GPU-only skipped**; docs **506 tasks / zero errors**. No optimization passes.
- Next is the actual flight checkpoint. DEV-008's ready predicate itself requires Flight context, so FND-017 will supply that integration before the convenience command; use the existing CLI and real IGameFlow entry in the meantime.

### 2026-10-12 — REN-011 done — astra (gpt-6)

- Connected completed native mesh batches to the shared GPU pool, Burst applied-state/prop updates, exact bounds, shared functional mesh registration and runtime material ownership. Destroyed handles stop drawing before submission and retire for three frames, including outside gameplay. CameraTargetSyncSystem publishes an explicit VoxelRenderCamera; a Burst transform bridge updates applied brick matrices before drawing. Q140 resolves exact cleanup capture and source-material protection. Implementation **18a0e45**.
- GPU **6/6** prescribed tests pass: 20 retained-handle rebuilds, dead output disposal/retirement, empty draw suppression, bounds and moving transforms, 128-brick apply and zero managed allocation. Apply median/p95 **0.316950 / 0.327200 ms**, **0 B** per warmed apply. No tuning passes used.
- Validation: compile **zero C# warnings**; EditMode **699 pass, 1 advisory failure, 1 explicit skipped** (known MeshBuild128, 0.9008 ms); headless PlayMode **65 passed + 12 GPU-only skipped**; headless perf **47 pass, 1 advisory failure**, with the two new GPU perf tests skipped. Repeated mesh-build timing **0.9193 ms** remains deferred, unchanged 0.5 ms target. Docs **506 tasks / zero errors**.
- Added **FND-017** for the owner's concrete first-flight checkpoint: existing imported ship, approved World lighting, temporary input-context ownership until the UI navigator, real controls/collision/save-load and actual game screenshots. Execute it after INP-005 and DEV-008; no new ship designs. Rendering is now connected but no end-to-end playable checkpoint is claimed yet.

### 2026-10-12 — REN-010 done — astra (gpt-6)

- Added priority dequeue, completed ECS snapshots, independent Burst mesh jobs and owned unsafe result lists per work slot (ADR-0032). Only one batch stays in flight until apply consumes it; adaptive sizing, dormant LOD, destroyed requests and re-dirty queue behavior pass. Shutdown completes and frees all output; config capacity grows safely. ADR-0043 / Q139 adds the installed bounds assembly to EditMode tests.
- Validation: compile **zero C# warnings**; EditMode **699 passed, 1 advisory timing failure, 1 explicit skipped**; headless PlayMode **65 passed + 6 GPU-only skipped**; perf **47 pass, 1 advisory fail**; docs **505 tasks / zero errors**. MeshBuild128 median **0.8760 ms** in the full suite / **0.8640 ms** in perf versus **0.5 ms** target (whole-world update includes snapshot/scheduling). **Performance deferred**, zero optimization passes used; no thresholds changed. Implementation **be4b370**.
- Next: REN-011 apply/cleanup and explicit camera bridge, then INP-005 and DEV-008. Live-slice follow-ups discovered: World has no lights (use DESIGN-GUIDE §4.1 values); real input needs a context owner while the UI navigator is not implemented; the default start ship is still the four-cell ship_base. Resolve these in the playable integration work, without introducing new ship designs.

### 2026-10-12 — REN-009 done — astra (gpt-6)

- Boot now receives the pooled brick material, copy shader, approved NOCTURNE palette and ContentService prop meshes through explicit config assets. Factory copies all values and creates the 256×1 point/clamp RGBA32 texture; disposable boot ownership releases native storage and texture on shutdown/failure. Missing inputs/invalid palette length fail validation. Q138 resolves ownership and the superseded legacy-palette instruction.
- Three prescribed tests pass, including real boot singleton creation/disposal and exact palette pixels. Compile **zero C# warnings**, full EditMode **696 passed + 1 explicit skipped**, headless PlayMode **65 passed + 6 GPU-only skipped**, docs **505 tasks / zero errors**. Implementation **6407fb5**. No optimization passes used; next REN-010 mesh-building integration.

### 2026-10-12 — REN-003 done — astra (gpt-6)

- Implemented shared packed GPU pools, best-fit allocation, generational handles, three-frame retirement, growth copies, Burst culling and coalesced indirect draws using the canonical surface shader. Implementation **de1b11d**. REN-010/011 own the mesh-build/apply/camera integration; this task does not demonstrate a playable ship.
- Functional evidence: 10,000 allocator operations; growth/reuse and reference pixel parity **0/255** including damage/AO/shadows; **48** Metal/D3D11 variants. Reference station pool + native + non-render baseline **31,814,428 B**; integrated batch peak remains TST-004.
- Validation: compile **zero C# warnings**; EditMode **693 passed + 1 explicit skipped**; headless PlayMode **65 passed + 6 GPU-only skipped**; perf **47/47**. Separate GPU allocator/pixel tests pass. The latest dedicated timing test could not capture complete native frames: **performance deferred**, no GPU pass claimed. Earlier diagnostic execution/elapsed **5.714083 / 6.335625 ms** is diagnostic only (Q137). Initial two-pass allowance exhausted; no further isolated tuning.
- Next: REN-009 configuration, REN-010/011 integration, real player sampling, imported-ship flight and screenshots. This follows the owner's revised playable-slice goal.

### 2026-10-12 — Owner policy: build systems before performance tuning

- Frank, verbatim: “can you reword handoff or docs to say the spec performance goals are just suggestions but not blockers to move on to the next phases. no worker should spend more than 2 passes initially in optimizing blindly just for spec numbers.”
- Accepted **ADR-0042**: performance numbers across specs/roadmap/tests are advisory, never blockers for functional task completion, dependents or later phases. Maximum **two initial optimization passes per system/feature**, shared across workers/tasks/handoffs; benchmark troubleshooting counts. Record gaps or unavailable measurements and move on. Later tuning needs an integrated gameplay bottleneck or explicit owner direction.
- Correctness, safety, leaks, determinism, functional integration and explicit owner/art approvals still apply. Keep numeric failure reports truthful; no test thresholds or runtime code changed for this policy. Existing implementation/test status remains unchanged. REN-003 has exhausted its initial tuning allowance; prioritize renderer/input integration and a live gameplay check.

### 2026-10-11 — REN-008 done — astra (gpt-6)

- Implemented `BrickLit`, shared `BrickSurface`/material layout and `PropInstanced` with forward, shadow, depth and depth-normal passes. ADR-0040 / Q133 uses the approved art contract, retains the eight-byte vertex and keeps exposure distinct from scorch. No legacy bitmap, AI-generated asset or new ship geometry.
- Procedural builder and persisted materials/textures verified by asset readback: three 64×256² arrays, nine mips (BC7 albedo/mask, BC5 normals); three 2048² prop textures, twelve mips; correct sRGB/linear spaces, palette/parameter dimensions and canonical references. Provenance is in `docs/design/LICENSES.md`. Final art and presentation property writers remain with their existing tasks.
- GPU PlayMode **4/4** on multithreaded Metal; **112** Metal/D3D11 pass/stage/keyword variants compiled with zero errors. Both shaders report SRP Batcher code **0** after real draws. Palette/AO pixels, damage cracks (**15,633/28,900** surface pixels changed), three separately painted props on two grid transforms, and prop/brick shadows (**1,750 / 2,761** darker receiver pixels) pass. Captured pixels were visually inspected (`Logs/rendering/ren008-*.png`); logs/results: `ren008-gpu`, `ren008-assets`. Coroutines must submit procedural draws before camera culling (08-tools); teardown restores optional global matrices safely.
- Validation: compile **zero C# warnings**, EditMode **693 passed + 1 explicit skipped**, headless PlayMode **64 passed + 4 GPU-only skipped**, separate GPU **4 passed**, perf **47/47**, docs **505 tasks / zero errors**. Implementation **4c504f4**. REN-003 and REN-009 are now unblocked.


### 2026-10-11 — REN-003 prerequisite correction — astra (gpt-6)

- Inspection before implementation found no BrickSurface.hlsl (owned by REN-008) and no apply/cleanup systems (REN-010/011). Q132 / SPEC-11 Q23 adds REN-008 as the fallback shader prerequisite, returns REN-003 to todo, and assigns future callers to their existing tasks. ADR-0032 remains proposed; no runtime contract changed and no fallback success claimed.
- Next: implement and verify REN-008’s shared shader contract, then resume the pooled renderer with bounded allocation slack, deferred lifetime and measured memory. Docs: 505 tasks / zero errors. Claim branch contains only this prerequisite correction.

### 2026-10-11 — CAM-011 done — astra (gpt-6)

- Added the three prescribed PlayMode camera tests. Timing uses the whole CameraTargetSyncSystem marker (including nested rig/input); GC measures only that update, excluding fixture mutation/assertions. The 80 m/s circle advances from the production bridge’s clamped unscaled dt for at least ten seconds, checking pitch and zero allocation each frame.
- Q131 / SPEC-62 Q14 resolves future-path gates: enforce **0.05 ms without occlusion** now; CAM-007 owns a real-raycast fixture and **0.15 ms** timing, CAM-006 owns origin accuracy. Initial captured-clock assertion exposed that captureDeltaTime does not fix unscaledDeltaTime; documented in tools, corrected the scenario clock without changing runtime or loosening its checks.
- Reference CPU median/p95 **0.002833 / 0.003000 ms**; GC median/p95/max **0/0/0 B**. Circle **185,964 frames / 10.009428 s**, pitch **54.999989–55.000019°**, no camera allocation. No graphics timing claimed.
- Validation: compile **zero C# warnings**, full EditMode **693 passed + 1 explicit skipped**, full PlayMode **64 passed**, perf **47/47**, docs **505 tasks / zero errors**. Implementation: `111f9fd`. Next: renderer fallback contract and implementation, then remaining M1 renderer/input/UI work.


### 2026-10-11 — CAM-005 done — astra (gpt-6)

- Implemented flight follow, heading/orbit/recenter/zoom, bounded look-ahead and immediate new-target cuts. Boot now supplies the Flight-context InputCameraInput adapter.
- Q129 resolves double-applied orbit settings and adapter namespace/ownership across SPEC-03/62: raw pixel input, rig applies sensitivity/inversion once; INP-006 owns Editor sampling and adapter tests. Q130 fixes endpoint damping that missed the required FPS tolerance: exact exponential cascade with linear target motion, stable equal-rate/small-step limits. At 100 m/s for 2 s the 30/60/144 FPS final X values all print **230.4243 m**, passing the unchanged **0.05 m** tolerance.
- Validation: compile **zero C# warnings**, EditMode **693 passed + 1 explicit skipped**, PlayMode **61 passed**, perf **44/44**, docs **505 tasks / zero errors**. All eight prescribed camera tests pass; no new API/assembly dependencies or runtime static state. Implementation: `f19e2a5`. Next: CAM-011 inclusive camera timing/allocation and circle framing gates.


### 2026-10-11 — CAM-004 persistent camera and composition (astra, gpt-6)

- Implemented one app camera with configured FOV/clips, Menu drift, Hold, half-second blends/cuts, reduced-motion behavior, target storage and disposal. The presentation bridge creates MainCameraState in OnCreate, selects the interpolated player target, publishes camera state each frame and updates the existing optional VoxelViewer; no lazy singleton or fixed-step camera work.
- Created the prefab/config through a temporary Editor script, verified one Camera and zero listeners, then removed the script. Serialized clear color follows DESIGN-GUIDE's existing #020303 sky base. Composition uses the CameraRoot staging child, binds settings, scene modes and optional thumbnails, and disposes in reverse. The existing boot/composition expectations remain green.
- Accepted ADR-0038 for integration. Q127 / ADR-0039 resolves the conflicting listener instructions: AudioService remains sole owner; it follows a rig-owned ListenerPivot, cleared before disposal. Q128 documents null input/FX staging until CAM-005/CAM-008, FollowHeading default and future ordering peers; Lab uses Hold until CAM-009.
- All ten required tests pass. An initial pivot assertion exposed **7.6 µm** of parent-transform conversion roundoff; it now uses **0.1 mm** tolerance while stored target data remains exact. `SystemBase` requires qualified UnityEngine.Time; the tool reference records that compile trap.
- Validation: compile **zero C# warnings**, EditMode **685 passed + 1 explicit skipped**, PlayMode **61 passed**, perf **44/44**, docs **505 tasks / zero errors**. Enter-to-menu gate median/p95 **14.00 / 17.32 ms** (<1 s); camera-specific frame gates remain CAM-011. Implementation: `ece7b8b`. Next: CAM-005 flight follow and input.

### 2026-10-11 — CAM-003 camera math (astra, gpt-6)

- Implemented scalar/vector half-life damping, shortest-arc angle damping in (−180, 180], world-yaw/local-pitch orbit rotation, focus-relative position, length clamp and +Z heading. Pure math; no state, allocation or scene updates.
- All five required tests pass: one half-life reaches the midpoint, 30/60/144 steps agree with the one-second solution within **1e-4**, 170°→−170° crosses 180°, positive pitch looks downward at any tested yaw, and identity heading is zero.
- Validation: compile **zero C# warnings**, EditMode **675 passed + 1 explicit skipped**, PlayMode **61 passed**, docs **505 tasks / zero errors**. No new performance gate; existing perf baseline remains **44/44**. Implementation: `0e4a19d`.
- Next: CAM-004 rig/prefab/sync and composition binding. Reconcile SPEC-62's camera-prefab listener wording with SPEC-64's already implemented sole listener under AudioRoot before integrating; do not create a duplicate listener.

### 2026-10-11 — CAM-002 custom camera decision record (astra, gpt-6)

- Added proposed ADR-0038 using the required template: Unity Physics queries, ECS target proxies, context-owned input, bounded/testable behavior and deliberate package adoption. It records SPEC-62's existing custom-rig choice; alternatives include Cinemachine with proxies and manual input. Consequences cover loss of authoring tools, cinematic sequences outside v1 and a new ADR if that scope changes.
- Linked the decision from SPEC-62 and refreshed HANDOFF. No package, runtime or binding contract changes.
- Validation: compile **zero C# warnings**, EditMode **670 passed + 1 explicit skipped**, docs **505 tasks / zero errors**. Existing PlayMode **61 passed** and perf **44/44** baseline unchanged by this documentation-only task. Implementation: `a0df398`. Next: CAM-003 math helpers, then rig/follow.

### 2026-10-11 — CAM-001 camera contracts (astra, gpt-6)

- Added SPEC-62's modes, settings, config defaults, unmanaged `MainCameraState`, input interface, rig API shells and double-precision map-transform shell in their specified assemblies. Future operations throw task-labelled `NotImplementedException`; no camera is instantiated yet.
- The two required tests verify unmanaged state and all serialized configuration defaults. Q126 resolves the missing-VFX dependency cycle with an empty `IFxFrameReader` binding interface; CAM-008 adds the event property and `ShakeRule.Kind` after VFX contracts exist.
- Validation: compile **zero C# warnings**, EditMode **670 passed + 1 explicit skipped**, PlayMode **61 passed**, docs **505 tasks / zero errors**. No new performance gate or runtime update loop. Implementation: `f078fc9`. Next: CAM-002 decision record, CAM-003 math and the M1 rig/follow chain.

### 2026-10-11 — FLT-010 legacy flight tuning evidence (astra, gpt-6)

- Measured every migrated blueprint through real spawning and Unity Physics. The reproducible report prints both original fallback and shipped asset settings, including mass, Iyy, thrust/counts, 0.25 s forward acceleration, time to 99% speed cap, 10 s turn rate and first 90° turn. Full tables are in Measurements.
- Applied provisional core yaw **180°/s² / 2.5 MN·m**. All 13 Small blueprints now turn in **0.833–1.167 s**. The 2 MN·m intermediate candidate missed only Vestari (1.267 s). Other config fields and thrust rows remain unchanged. Q123 preserves original fallback defaults while checking configured boot output.
- Powered acceleration is **1.928–38.647 m/s²**; seven ships miss 8–20 and the Q40 base has no thrusters. Q124 records incompatible full-thrust mass ratios for shared-row scaling. Q125 requests owner sign-off with three options; **pending**, not an approval. Medium/Large classes lack examples. No new 3D ships or AI assets were created; content-layout balancing awaits the owner decision.
- Validation: compile **zero C# warnings**; EditMode **668 passed + 1 explicit skipped**, PlayMode **61 passed**, perf **44/44**, docs **505 tasks / zero errors**. Flight worker median **0.09054 ms**, cache rebuild **1.165 µs**, flight managed allocation **0 B**. Implementation: `c9526c2`.
- Next: CAM-001 and the ready M1 camera chain; renderer/UI work and the M1 system review remain. Continue autonomously while owner tuning decisions remain pending.

### 2026-10-11 — FLT-011 flight cost and determinism gates (astra, gpt-6)

- Three required performance tests add six passing gates: 50 Ship20k-sized roots × 20 thrusters, main-thread scheduling, player command application, 100-body plane work, 200-instance cache rebuild and zero managed allocation. Final medians: **0.08983 ms** propulsion workers, **0.001041 ms** propulsion main, **0.000666 ms** player apply, **0.003166 ms** plane workers, **1.162 µs** rebuild per ship, **0 B** managed allocation. Full measurements and scopes are above.
- The required determinism test compares every transform/velocity field by bits for 600 ticks × 3 ships. All **eight runs** (1, 2, 0/1, 6 ticks/frame × 1/17 workers) match; the scripted sequence includes thrust, turning, boost, braking and coast. No simulation implementation changes were needed.
- Initial worker marker names were absent: discovered exact **Burst Jobs** handles, required positive samples, and documented IJobEntity naming in 08-tools. Reference settings are scoped once around the performance fixture, then restored. A test-only nonzero-yaw assertion now checks the whole script rather than a neutral-command tick.
- Validation: compile **zero C# warnings**; EditMode **668 passed + 1 explicit skipped**, PlayMode **60 passed**; all **44/44** performance gates pass; docs **505 tasks / zero errors**. Implementation: `0278edb`. Next: FLT-010 legacy tuning evidence, then remaining M1 renderer/camera/UI work.

### 2026-10-11 — FLT-009 combined flight scenarios (astra, gpt-6)

- Added five required PlayMode scenarios using the real spawn/body/collider/flight/physics pipeline and non-rendered synthetic fixtures. Off-centre thrust (2.9999998 m lever, gyro disabled) produces −0.12755036 rad/s yaw. Ramming at 30 m/s produces 13.49946 / 16.50054 m/s forward velocities with **3.1789144e-8** relative momentum error.
- BrickWrite thruster removal reduces acceleration **19.801975 → 10.204095 m/s²**; the measured yaw matches applied thruster plus bounded gyro torque. Aft-only turning reaches **2.0909076 rad/s** against **2.0943952** cap. Exactly eight ability edges arrive at 30/60/144 FPS. Measurements repeat in focused and full suites.
- Validation: compile **zero C# warnings**; EditMode **668 passed + 1 explicit skipped**, PlayMode **56 passed**, perf **38/38**; docs **505 tasks / zero errors**. Existing GridSpawnSystem lookup-creation runtime warning is recorded in HANDOFF for the M1 system review; no warning suppression was added.
- Implementation: `4b4054d`. Next: FLT-011 flight cost/determinism, then FLT-010 legacy tuning evidence and remaining M1 work. No art assets or new player ship designs were authored.

### 2026-10-11 — FLT-008 planar impulse constraint (astra, gpt-6)

- Added the post-propulsion parallel impulse job: removes vertical velocity with position bias, preserves horizontal motion, and leaves every transform write to Unity Physics. Hulks remain constrained; non-planar and kinematic bodies are skipped.
- Three required tests use actual Unity Physics integration: live and hulk bodies return within **1 mm after 60 ticks**, off-centre impulses retain exactly zero pitch/roll, and unconstrained/kinematic bodies retain their vertical trajectory. Q122 replaces the copied per-job warning flag with one atomic diagnostic latch per system lifetime.
- The first full suite caught delayed Burst diagnostics from a FixedString constructor (T2.14); repaired to literal conversion. Final validation: compile **zero C# warnings**; EditMode **668 passed + 1 explicit skipped**, no Burst errors; PlayMode **51 passed**; perf **38/38**; docs **505 tasks / zero errors**. Physics main median/p95 **0.02467/0.2197 ms**, idle **0.01387/0.01850 ms**.
- Implementation: `0be9bf9`. Next: FLT-009 combined flight scenarios, then FLT-011 cost/determinism and FLT-010 tuning evidence.

### 2026-10-11 — FLT-007 physical propulsion (astra, gpt-6)

- Added revision-based thruster discovery with ordered warm-start carryover, assist targets, bounded allocation, at-position physical impulses, core gyro and smoothed visual intensity. The second job idles hulk thrusters once. All lookups refresh before scheduling; no fixed-step completion or direct velocity assignment.
- Eight required tests pass against real block/module blobs, covering speed caps, coast, brake, lateral/yaw assist, external impulses, revision rebuilds and hulk behavior. Q121 adds the omitted module-data guard and separates the analytical full-force braking fixture from the default half-gain convergence tail.
- Validation: compile **zero C# warnings**; EditMode **665 passed + 1 explicit skipped**, PlayMode **51 passed**, perf **38/38**, docs **505 tasks / zero errors**. Existing physics main median/p95 **0.02469/0.2005 ms**, idle **0.01383/0.01850 ms**; whole-flight cost/determinism gates remain FLT-011.
- Implementation: `52335f4`. Next: FLT-008 plane constraint, then flight scenarios and full-flight gates.

### 2026-10-11 — FLT-005 player command bridge (astra, gpt-6)

- Implemented fixed-tick player command application, one pending press per counter per tick, byte wrap, no-player/hulk edge dropping, and component-wise finite/clamped movement and aim. The consumed-counter singleton is created before the boot snapshot; the parallel-safe command job has no fixed-step synchronization point.
- Six required tests cover multiple player roots, AI isolation, edge clearing, wrap, hulks, no ship, and non-finite inputs. Q120 resolves the no-player reset inconsistency: Cruise remains untouched on every path.
- Validation: compile **zero C# warnings**; EditMode **657 passed + 1 explicit skipped**, PlayMode **51 passed**, perf **38/38**; docs **505 tasks / zero errors**. Physics spawn main median/p95 **0.02427/0.221 ms**; idle **0.01391/0.01896 ms**.
- Implementation: `9271f44`. Next: FLT-007 propulsion, then FLT-008 plane constraint and remaining M1 work.

### 2026-10-11 — FLT-004 deterministic thrust allocator (astra, gpt-6)

- Implemented allocation-free fixed-order projected Gauss–Seidel, clamped warm starts, fixed sweeps and weighted residual return. Internal production steps allow the required per-coordinate debug checks. Implementation `3c34a39`.
- Six required tests pass: **12,000** coordinate updates independently recomputed in double precision do not increase residual beyond the documented float tolerance; bounds include stale throttles/zero columns; symmetric analytic, off-centre/no-gyro and saturation cases pass. **100 seeded problems × 127 intermediate states** and two independently scheduled Burst solves match managed values bit for bit.
- **Q119 / SPEC-20 Q10–12:** retain Decision 7 over the overly broad symmetry criterion (redundant gyro solutions need not use equal throttles); document/test the counterexample. Project stale warm starts before residual construction. Initial strict-Burst/managed float reductions differed by one ULP in the returned residual; double product/sum accumulation with one float cast fixes parity without changing fields, objective or ordering.
- Final checks: compile **0 C# warnings**, EditMode **651 passed + 1 explicit skipped**, PlayMode **51 passed**, perf **38/38**, docs **505 tasks / 0 errors**. Existing physics spawn main median/p95 **0.02435 / 0.2214 ms** and idle **0.01381 / 0.01866 ms** remain green. Whole-flight allocation cost remains FLT-011, after PropulsionSystem exists.
- Next: FLT-005 player command application, then FLT-007 propulsion and FLT-008 plane constraint; remaining M1 work follows.

### 2026-10-11 — FND-016 sandbox entry (astra, gpt-6)

- Implemented SceneFlow.EnterSandboxAsync and GameSession.BeginSandbox: creative, deterministic seed, optional live player at origin and the ordinary flow reservation. World-to-World entry saves and tears down before resetting the fleet; release builds return NotAllowed. Implementation `4efcb47`.
- **Q118 / SPEC-01 Q23:** sandbox reset skips campaign initializers, seed 0 uses 1, and the no-player option resets on new game/load/end. GAL-008 owns clearing future galaxy state; DEV-008 owns harness dispatch.
- Required sandbox test covers player/empty options, no StarSystemMember anywhere in the fixture, Busy on concurrent entry, World-to-World transition and subsequent normal campaign player creation. Headless renderer isolation remains the FND-010 fixture's explicit constraint.
- Validation: compile **0 C# warnings**, EditMode **645 passed + 1 explicit skipped**, PlayMode **51 passed**, perf **38/38**, docs **505 tasks / 0 errors**. Existing gates: teardown 200k median/p95 **3.727 / 5.914 ms**; physics idle **0.01421 / 0.01833 ms**.
- Next: FLT-004/005/008, then remaining ready M1 work. Handoff updated; no owner wait.

### 2026-10-11 — FND-010 fleet persistence and player spawning (astra, gpt-6)

- Implemented sorted FleetModel ownership and mutation notifications, FLET v1 deferred VOXS snapshots, source/configured blueprint recovery, player ready/exit hooks and pre-capture/exit writeback. Registry borrows are released before edits/disposal; fleet thumbnail keys survive save cleanup. Implementation `745e10a` (initial ownership/report work `5459fc8`).
- Required tests prove byte-identical FLET re-save, flagship preservation and unchanged model identity, paused player readiness with **zero simulation ticks**, and **50 removed cells** still missing in both the restored structure and respawned grid. The 20k benchmark supplies damage-test geometry; no new ship art was authored.
- Accepted **ADR-0036 / Q114–115** routes recoverable participant warnings into the active load report and completion counts. Added scope/nesting/failure checks. **Q117 / SPEC-01 Q22** repairs missing, malformed and stale-low SESS counters before restored fleet ids could be reused; those three regressions pass.
- Final validation: compile **0 C# warnings**, EditMode **645 passed + 1 explicit skipped**, PlayMode **50 passed**, perf **38/38**, docs **505 tasks / 0 errors**. Existing physics gates remain green: station spawn main-thread median/p95 **0.02446 / 0.2245 ms**, idle **0.01381 / 0.0185 ms**; save-capture fixture median **0.1627 ms**. These retain their existing test scope, not full renderer/gameplay measurements.
- M1 location remains origin/identity yaw under Q114; FND-012 replaces the direct request boundary, GAL-008/SYS add canonical location. The headless tests disable renderer readiness only in their fixture; runtime keeps the renderer gate for REN integration. Next: FND-016 sandbox, then remaining ready M1 work.

### 2026-10-11 — PHY-024 paused spawn body initialization (astra, gpt-6)

- Accepted **ADR-0037 / SPEC-12 Q26 / ledger Q116**. GridSpawnBodyInitSystem initializes fresh roots after GridSpawnSystem and plays initial components through the end-init ECB. Existing GridBodyInitJob/factory and cache ownership are reused; fixed-step split adoption/rebuilds remain unchanged. No gameplay tick or pause release is needed for a provisional collider.
- Both required tests pass: exact mass, pose/velocity and unchanged SimClock at zero ticks; an existing split body is untouched until fixed-step adoption. The production initializer is included in the existing collider determinism and physics scenario fixtures. Four frame patterns and 1/17 workers still produce identical damage records and zero late installs.
- Performance accounting now includes GridSpawnBodyInitSystem and the end-init ECB as well as the existing inclusive stages. Reference station spawn main median/p95/max **0.024729 / 0.220084 / 0.257750 ms**, full collider ready in at most **7 frames** (42 spawns/300 frames); idle median/p95 **0.013749 / 0.017707 ms**. 30-ship damage has zero late installs; deadline completion max **0.513042 ms**, 11 forced completions (diagnostic), worker median **0.041354 ms**. No gate was relaxed.
- Full validation: compile **zero C# warnings**, EditMode **634 passed + 1 explicit skipped**, PlayMode **48 passed**, perf **38/38**, docs **505 tasks / zero errors**. Implementation `e201b39`.
- Next: rebase/resume FND-010's saved branch and finish fleet/FLET/player integration; then remaining M1 work.

### 2026-10-11 — FND-010 paused-spawn dependency found (astra, gpt-6)

- Code tracing found a real ready-hook deadlock: SceneFlow holds a pause token until the player collider exists, but GridBodyInitSystem runs only in fixed ticks. Added PHY-024 for fresh-spawn initialization outside simulation, preserving split adoption/determinism in the existing fixed pipeline.
- FND-010's initial fleet ownership and scoped restore-warning work is compile-clean but unfinished, committed only as `16df29b` on task/FND-010-fleet-player. ADR-0036 and questions 114–115 are reserved there. Resume/rebase that branch after PHY-024; its participant, integration and tests remain to do.

### 2026-10-11 — FLT-006 flight registration and thruster visuals (astra, gpt-6)

- FlightSetup registers the five root types on PlayerShip/NpcShip/SmallCraft/Dummy and PlayerShip only on the player profile. ThrusterInitSystem registers state/visual components at creation; its parallel Burst job computes grid-space nozzle centre, exhaust direction and radius only for changed FunctionalBlock chunks.
- Three required tests spawn a VOXS test blueprint with four yaw-oriented thrusters. They verify all six profile choices, thruster components, orientation/nozzle geometry and that an idle frame preserves a sentinel visual because its FunctionalBlock did not change. Initial fixture-only duplicate catalog insertion was corrected; all final gates pass.
- Extended existing **SPEC-20 Q9 / ledger Q112** documentation correction: FLT-006 lists its explicitly required FlightSetup implementation, and the remaining memory summary says 44 bytes per FlightThruster. No architecture or layout change.
- Full validation: compile **zero C# warnings**, EditMode **632 passed + 1 explicit skipped**, PlayMode **48 passed**, perf **38/38**, docs **504 tasks / zero errors**. Implementation `2388784`.
- Next: FND-010 fleet/FLET/player spawn and the M1 sandbox path; then remaining flight and renderer work.

### 2026-10-11 — FLT-003 flight configuration (astra, gpt-6)

- Implemented all 16 definition-to-component mappings and the specified FLT-E001 checks for nonpositive forward speed and fewer than one allocation sweep. Created config_flight with exact defaults through a temporary Unity Editor job, removed the job and rebuilt the content database.
- SPEC-01 integration note: BootSingletons now creates FlightConfig in its prescribed table position and tags it BootSingleton. The two required tests verify the default asset/factory/boot values and validation code, asset path and field. No new spec decisions.
- Full validation: compile **zero C# warnings**, EditMode **629 passed + 1 explicit skipped**, PlayMode **48 passed**, perf **38/38**, docs **504 tasks / zero errors**. Implementation `f21a981`.
- Next: FLT-006 flight profile and thruster registration, then FND-010 fleet spawning; continue other ready M1 work.

### 2026-10-11 — FLT-002 grid-space and flight maths (astra, gpt-6)

- Implemented instance/point/direction transforms, capped gyro torque, directional speed caps, world-to-local planar velocity and assist targets. Brake wins over boost; boost changes only the forward speed cap; coasting is clamped to the directional caps.
- All eight required tests pass, including the shipped two-cell thruster centres at orientations 0/8, all 24 orthonormal orientations and transformed round trips. Resumed after PHY-023 and verified the previously failing full-suite combination: strict collider determinism and zero-late gates pass.
- Full validation: compile **zero C# warnings**, EditMode **627 passed + 1 explicit skipped**, PlayMode **48 passed**, perf **38/38**, docs **504 tasks / zero errors**. Implementation `ec30fc9`.
- Next: FLT-003 config and FLT-006 profile registration, then fleet/player spawning and remaining M1 work. No new spec decisions.

### 2026-10-11 — PHY-023 urgent collider deadline repair (astra, gpt-6)

- Accepted **ADR-0035 / SPEC-12 Q25 / ledger Q113**. The variable-rate builder uses `SimTime.MaxTicksPerFrame` to finish deadline-critical work before the next catch-up frame. Existing in-flight work cannot monopolize the batch slot across that boundary. Newly scheduled due assemblies also finish before returning. Fixed-step swap remains nonblocking and remains the sole collider installer.
- A deliberately stalled 500 ms batch now finishes outside fixed-step execution and installs at exactly tick 8, with zero late installs. The non-urgent age limit, per-grid order, ownership and strict zero-late assertions remain intact; no sleeps or hidden waits were added to the determinism driver.
- Full EditMode determinism: 12 runs across four frame patterns and 1/17 workers have identical records and zero late installs. Catch-up deadline marker max **0.528208 ms** under normal safety settings (diagnostic). Reference 30-ship workload: deadline median/p95/max **0 / 0 / 0.085417 ms**, six forced completions; worker median/p95 **0.034209 / 3.338420 ms**, zero late installs. No budget was relaxed; the existing non-urgent-predecessor backlog diagnostic remains documented.
- Full validation: compile **zero C# warnings**, EditMode **619 passed + 1 explicit skipped**, PlayMode **48 passed**, perf **38/38**, docs **504 tasks / zero errors**. Implementation `a912ab2`. The two original FLT-002 failure reports remain preserved under Logs/test-results.
- Next: resume/rebase task/FLT-002-flight-math and rerun its complete validation, including the eight math tests that preceded the original failure. Then continue flight/config/registration and remaining M1 work.

### 2026-10-11 — FLT-002 validation exposed urgent collider deadline race (astra, gpt-6)

- The eight required math tests pass; compile is clean. Two full EditMode runs each recorded **624 passed / 1 failed / 1 explicit skipped**: ColliderDeterminismTests.InstallTicks_IndependentOfFramePattern counted **3 late installs** for six-tick frames and 17 workers. A focused two-test repeat passed; the full-suite failures are retained under Logs/test-results/flt002-{initial,repeated}-collider-late.xml.
- The current IsCompleted-only fixed-step handover can miss a due tick when the snapshot batch remains unfinished. **PHY-023 / SPEC-12 Q25 / ledger Q113** will complete deadline-critical work in the variable-rate stage, preserving the fixed-step no-wait rule and strict zero-late assertions. This is a real unresolved gate, not accepted noise.
- FLT-002 remains blocked on that repair, with its code committed only on task/FLT-002-flight-math. Resume it after PHY-023, rebase onto main and rerun its full validation. No owner input is needed.

### 2026-10-11 — FLT-001 shared flight contracts (astra, gpt-6)

- Added the specified command counters/edges and player tag, flight/thruster/config/stat components, planar tag in Game.Data, configuration definition fields and helper contracts. Only the explicitly allowed default/one-line helpers have bodies; FLT-002/003/004/006 own the remaining implementations. No runtime system or new config asset is registered yet.
- The two required contract tests verify SPEC-03's exact eight PlayerCommand fields, ten byte edge counters, flag values and unmanaged components. Measured `FlightThruster` **44 bytes**; corrected its stale 48-byte comment, added the omitted PropulsionStats file to the task list and aligned PlayerShip's ownership comment with ADR-0015 (**SPEC-20 Q9 / ledger Q112**). No field/layout change or new assembly dependency.
- Full validation: compile **zero C# warnings**, EditMode **617 passed + 1 explicit skipped**, PlayMode **48 passed**, perf **38/38**, docs **503 tasks / zero errors**. Implementation `0ddf5bf`.
- Next: FLT-002 geometry/flight helpers, then the flight registration needed by fleet spawning. Continue ready M1 work.

### 2026-10-11 — FND-009 session flow and SESS persistence (astra, gpt-6)

- Wired the composition-owned session, IGameFlow and persistent-ID allocator. New games reset existing models and run ordered initializers; restore keeps the session identity; exit writes back campaign time; scene recovery clears the session. SESS v1 preserves all six fields and rejects malformed state before mutating the session.
- Accepted **ADR-0034 / SPEC-01 Q14–18 / SPEC-05 Q27 / ledger Q107–111**: every save prepares ordered writebacks before participants; optional GPU thumbnails have a two-frame wait and producer-owned cleanup; exit saves run before Exiting under a flow reservation/pause, and quit-without-saving skips the save. New game creates a default profile only when none is selected. Future galaxy, fleet, combat-status and camera integrations remain with their named tasks.
- Required session tests and extra save edges pass: byte-identical real save/restore, reset/initializer/event order, save-block precedence, disallowed flows, failed preparation cleanup, malformed/truncated SESS atomicity, missing-chunk defaults and completed/late thumbnail handling. Headless capture omits the image; real camera pixel verification remains CAM-004.
- Full validation: compile **zero C# warnings**, EditMode **615 passed + 1 explicit skipped**, PlayMode **48 passed**, perf **38/38**, docs **503 tasks / zero errors**. Implementation `8a8bd6f`.
- Next: flight contract/registration prerequisites (FLT-001 onward) before FND-010 player spawning, whose specified PlayerShip/profile types are not present yet. Continue ready M1 work; no owner wait.

### 2026-10-11 — FND-008 persistent-ID allocation and refill (astra, gpt-6)

- Added the allocation portion of `GameSession`: managed `Allocate`, overflow-safe block reservation, next-ungranted saved counter, and session reset that clears outstanding ECS grants. `BootSingletons` creates the empty allocator before the boot snapshot; the managed initialization refill runs before GameInitGroup, including between scenes.
- Required tests pass: **10,000 managed + 10,000 scheduled Burst IDs are unique**; a saved/restored UInt64 counter resumes beyond all prior grants; missing/inactive sessions clear the pool. The mixed-source test leaves jobs outstanding across refill calls rather than hiding the dependency wait behind test-world completion.
- Resolved **SPEC-01 Q11–13 / ledger Q104–106**: allocation-only session members and exact counter-field round trip precede full FND-009 SESS/composition; concrete GameSession registration is required alongside interfaces. Installed Entities singleton access does not wait, so explicitly complete only allocator dependencies in initialization/reset. A valid zero refill threshold means refill only when empty.
- No-refill marker **0.000083 / 0.000084 ms median/p95** against 0.01 ms, reference settings, 60 warmup + 300 updates. Full validation: compile zero C# warnings, EditMode **606 passed + 1 explicit skipped**, PlayMode **48 passed**, perf **38/38**, docs **503 tasks / zero errors**. Implementation `2718913`.
- Next: FND-009 completes session flow, SESS and composition; fleet integration remains FND-010. Continue ready M1 work without owner waits.

### 2026-10-11 — PHY-019 collider performance and live memory (astra, gpt-6)

- Implemented the six required performance cases with asserted reference settings, 60-frame warm-up and 300 measured frames (600 damage ticks for the skirmish). Inclusive ECS markers count main-thread bookkeeping; five explicit worker markers count collider jobs. Nonzero/valid/running checks prevent absent headless profiler data from passing. The namespace-wide settings scope restores normal safety settings afterward.
- Focused measurements are in the table above. Full performance rerun: noise p95 **23.3 µs**, radius-3 station hit **0.3225 ms median**, idle **0.01415 ms median**; 30-ship worker median/p95 **0.04019 / 3.764 ms**, zero late installs. All timing gates pass; 300 urgent bricks/batch is below Q6's 512-brick trigger. The skirmish p95 remains visible even though its specified gate is median.
- Live ownership sampling found **3,275,424 B** of collider blobs, **26.6%** above the former three-version model. Recorded the original **2.5 MiB no-go**, accepted **ADR-0033 / SPEC-12 Q24 / ledger Q103**, and allocated **3.5 MiB** within the unchanged 36/40 MB whole-voxel limits. Corrected reference retained-copy model: **32,227,328 B**. No install or retirement lifetime was shortened; no duplicate blob owners were found. Full integrated memory/renderer accounting remains TST-004.
- Validation: compile **zero C# warnings**, EditMode **603 passed + 1 explicit skipped**, PlayMode **48 passed**, perf **37/37**, docs **503 tasks / zero errors**. Implementation `fcd09c2`; only runtime changes are profiling scopes. Updated HANDOFF and headless recorder guidance.
- Next: FND-008 persistent-ID allocation/refill and remaining ready M1 work, including the renderer fallback. No owner wait.

### 2026-10-11 — PHY-018 frame and worker determinism (astra, gpt-6)

- Added both required tests over the real fixed-step physics/collider/mass pipeline. One **1,008,128-cell station** and two **20,000-cell ships** each lose **512 cells** through eight fixed-tick 4×4×4 removals. BrickWrite/BrickDirty feed collider snapshots; the real mass recompute/application path handles the stand-in damage (DamageStreams remains DMG-002).
- All **four frame patterns** (1; alternating 2/3; alternating 0/1; 6 ticks/frame) and **1 / 17 workers** produce identical per-tick installed collider bytes, InstalledTick, PhysicsMass and PhysicsVelocity. Each run records **288 records / 96 ticks**; the full required pair executes 12 runs. Every prescribed install tick is also checked against the schedule, each collider changes, and **LateInstalls = 0** throughout.
- Non-urgent spawning is intentionally frame-driven by decision 6, so an identical ready state is prepared first with frame-boundary warm-up completion. The measured window uses production asynchronous batch scheduling; the recording job adds **no fixed-step completion**. Physics uses §5.13 settings; mass/velocity compare defined float bits rather than padding (T2.11).
- Validation: focused **2/2**; compile zero C# warnings; EditMode **603 passed + 1 explicit skipped**, PlayMode **42 passed**, perf **31/31**, docs **503 tasks / zero errors**. No production fixes or new decisions were necessary.
- Next: PHY-019 timing, skirmish and memory gates; renderer fallback and the remaining M1 integration follow in ROADMAP order.

### 2026-10-11 — PHY-017 integrated physics behaviour (astra, gpt-6)

- Added five required PlayMode scenarios in an isolated scene, using shipped content/physics config, procedural Ship20k/HollowStation1M geometry and the real fixed-step spawn, body, mass, dirty, snapshot, build, swap, physics and disposal systems. No production changes or new spec decisions were needed.
- Head-on ram at **20 m/s**: after 120 ticks both ships move (**8.829297 / 11.17070 m/s**), measured momentum relative error **0** (gate 2%). Off-centre impulse produces **0.007996768 rad/s yaw**, zero angular x/z; the planar spawn keeps its up axis and zero pitch/roll velocity through 120 ticks despite initial x/z angular input.
- A **4×4×4** removal uses BrickWrite/BrickDirty and mass recompute. The focused run's request at **tick 7** leaves the physics ray blocked until the prescribed **tick 16**, then clear at installation and afterward; zero late installs. A ready resting ship stays within **1 mm** during all **six provisional ticks** of an overlapping station (the count may depend on non-urgent build completion).
- Validation: focused **5/5**; compile zero C# warnings; EditMode **601 passed + 1 explicit skipped**, PlayMode **42 passed**, perf **31/31**, docs **503 tasks / zero errors**. The existing GridSpawn lookup-allocation advisory is unchanged; physics cost gates are next in PHY-019.
- Next: PHY-018 frame/worker determinism, then PHY-019 physics budgets and memory.

### 2026-10-11 — PHY-016 split-creator factory contract (astra, gpt-6)

- Added the required creator-role test with voxel root/brick components recorded through the gameplay-end ECB and the exact GridBodyFactory body sequence. XML documentation and test-result output publish the call order for SPEC-13 implementers.
- The full spawn/body/mass/physics/snapshot/build/swap/cleanup pipeline runs **20 lifecycle ticks**: creation at **20**, first movement at **21**, Split-priority full compound installed at **28** (RoundUp(21+7,4)). The old provisional blob enters deferred disposal exactly once, is freed, and root destruction drains the remaining colliders. Repeated lifecycle reports **zero new native leaks**; installs 1, late installs 0.
- Validation: focused **1/1**; compile zero C# warnings; EditMode **601 passed + 1 explicit skipped**, PlayMode **37 passed**, perf **31/31**, docs **503 tasks / zero errors**. No production API or spec decision changed.
- Next: PHY-017's five behaviour scenarios, followed by determinism and performance/memory gates.

### 2026-10-11 — PHY-015 collider statistics and debug boxes (astra, gpt-6)

- Implemented guarded main-thread statistics gathering and cumulative-counter reset. Live ownership gauges and last-batch fields survive reset. Development-only box extraction appends grid-local bounds, honors accumulated compound/box transforms, and uses child references so relative collider pointers remain valid. Harness integration uses these APIs in SPEC-70; no harness code was added here.
- Three required tests pass: one provisional box; a rotated three-level compound with **four box leaves covering all corners of 4,099 solid cells**; and installed/cached/pending/retired byte counters equal the sum of actual MemorySize values after a guarded job write. Caller output is preserved and reset clears only cumulative counters. The test child order was corrected to the assembler's canonical (z,y,x) precondition before the full run.
- Validation: focused **3/3**; explicit compile zero C# warnings; EditMode **600 passed + 1 explicit skipped**, PlayMode **37 passed**, perf **31/31**, docs **503 tasks / zero errors**. No new spec decision or public API change.
- Next: PHY-016 factory lifecycle, then PHY-017/018/019 integrated scenarios, determinism and gates. HANDOFF has been shortened to current state and next steps; detailed history remains in this ledger.

### 2026-10-11 — PHY-013 grid mass updates (astra, gpt-6)

- Implemented conditional removed-cell reading, exact incremental subtraction, asynchronous root/slot ordering, parallel per-brick sums and canonical serial reduction. One deduplicated application pass updates only non-empty Dynamic bodies and re-expresses velocity about the new mass frame. Dense and sparse cells and palette/instance block resolution share the existing voxel contracts.
- Six required tests pass. **1,000 seeded removals** across Destroyed/DetachedParticle/DetachedBody agree with full recompute within 1e-9 relative for accumulators, COM and tensor. **1,008,128 cells / four recomputes / 1 and 17 workers** produce bit-identical fields. Dissolution is ignored; Static/Kinematic masses remain untouched; zero cells leave the previous PhysicsMass/velocity intact.
- Removing **2,250 cells** from a non-planar spinning ship changes retained-point velocities by at most **7.82071e-6 m/s**, below 1e-3. PHY-008's empty-mass test now uses the real recompute system and creates the body on the following tick; its temporary boundary stub is gone.
- No new spec/API decision was needed. Applied T2.14's literal conversion to the existing Reexpress diagnostic so the full apply job compiles in Burst. DamageStreams integration remains conditional until DMG-002 implements its existing contract; the specified hand-built event tests cover removal/apply now. Restored cells remain M3 PHY-022.
- Validation: focused **7/7** (six new cases plus body-init integration); explicit compile zero C# warnings; EditMode **597 passed + 1 explicit skipped**, PlayMode **37 passed**, perf **31/31**, docs **503 tasks / zero errors**. Physics timing/memory gates remain PHY-019.
- Next: PHY-015 stats/debug, PHY-016 factory lifecycle and PHY-017/018/019 integrated physics checks; renderer fallback and shaders remain ready.

### 2026-10-11 — PHY-008 grid body initialization (astra, gpt-6)

- Added asynchronous root gathering, serial-order initialization/adoption and GameplayEnd ECB body creation. Factory helpers provide exact Static/Kinematic/Dynamic component sets, world index 0 and query-only provisional boxes. Initial velocities use the inertia frame; planar angular x/z are zero; existing damping and factory-caller velocity are preserved.
- All **eight required tests** pass using real GridSpawnSystem content/Ship20k/HollowStation1M. Exact component sets, bounds, preview filtering and initial mass/velocity pass. An overlapping station/ship remain stationary through **10 physics ticks**. The mass-wait test supplies the exact post-physics writer output at its boundary; full recomputation is the immediately following PHY-013 task.
- Rebuild keeps a real installed compound while discarding a slot referenced by a **500 ms unfinished batch**, without waiting for that batch. Cached/late-result ownership retires once, and every brick is requeued. Q101 / SPEC-12 Q22 avoids the synchronizing IsEmpty query path; Q102 / Q23 defines immutable-descriptor reads and deferred slot recycling during rebuild.
- Corrected installed-API/Burst issues during validation: six-component sets use FixedList128Bytes; component types use generic ReadWrite; FixedString logging uses literal conversion. New trap T2.14 records these. No binding public API or package changes.
- Validation: focused **8/8**; explicit compile zero C# warnings; EditMode **591 passed + 1 explicit skipped**, PlayMode **37 passed**, perf **31/31**, docs **503 tasks / zero errors**. Formal body-init timing remains PHY-019.
- Next: PHY-013 mass removal/recompute/application, then physics debug/scenarios/determinism/performance and renderer work as ready. Restored-cell mass is separately assigned to M3 PHY-022.

### 2026-10-11 — PHY-012 collider cleanup and teardown (astra, gpt-6)

- Added presentation cleanup before deferred disposal: complete aged/inactive batches, sweep missing roots in reverse, preserve in-flight discarded slots, clear inactive requests, and transfer every retired blob to the existing frame/tick queue. Production GridSpawnSystem/VoxelSingletons teardown owns all final disposal.
- Five required tests pass with real brick/assembly jobs and borrowed cached children: a dead record transfers its three existing blobs immediately and its two batch results after age-two finalization; disposal waits three frames and two ticks. Bulk unload of **32 linked groups / 96 collider blobs** and world disposal with a batch in flight report **zero new native leaks**, using EnabledWithStackTrace after warm-up. LEG cascade and inactive/config-absent cleanup pass.
- Q99 / SPEC-12 Q20 preserves cleanup without a config singleton by using the documented two-frame default. Q100 / Q21 fixes stale retirement ticks after catch-up: the local queue view takes current SimClock.Tick, preserving two future physics ticks if the game pauses. Singleton field ownership is unchanged. The paused catch-up case is included in the required no-gameplay test.
- Validation: focused **5/5**; explicit compile zero C# warnings; EditMode **583 passed + 1 explicit skipped**, PlayMode **37 passed**, perf **31/31**, docs **503 tasks / zero errors**. An initial test held ticks constant and correctly retained two blobs; its frame/tick progression was fixed. Formal cleanup timing remains PHY-019.
- Next: PHY-008 body initialization, then PHY-013 mass updates; renderer fallback/shader work remains ready.

### 2026-10-11 — PHY-011 collider swap (astra, gpt-6)

- Added Burst fixed-step scheduling for completed-batch handover and newest-due-prefix installation. The batch retains its finalization handle for teardown; cache and ECS dependencies order install after handover. No fixed-step Complete, CompleteDependency or GetSingletonRW calls.
- Six required tests cover exact urgent ticks, newer-wins retirement, once-only lateness, unfinished batches, state publication and destroyed roots. Swap returned in **0.0059 ms** while a deliberately **500 ms** job remained unfinished; this is an asynchronous-behaviour check, not the PHY-019 performance gate.
- Q98 / SPEC-12 Q19 resolves empty-prefix ownership: preserve the installed collider/tick/phase/box count, expose zero logical children, and retire older pending compounds while freeing updates.
- Validation: explicit compile zero C# warnings; focused **6/6**; EditMode **578 passed + 1 explicit skipped**, PlayMode **37 passed**, perf **31/31**, docs **503 tasks / zero errors**.
- Next: PHY-012 cleanup, then body initialization/mass updates and renderer work. No owner decision blocks the next task.

### 2026-10-11 — PHY-010 collider batch construction (astra, gpt-6)

- Implemented sealing, priority planning, bounded spawn/edit batches, uncapped eligible urgent work, owned mask/child copies, parallel brick/compound jobs and completed/aged-batch finalization outside the fixed step. No construction job contains an ECS lookup or type handle. Existing cache helpers retain blob ownership and incremental counters.
- Q96 / SPEC-12 Q17 puts the mesh-before-collider ordering declaration on later REN-010, which already specifies that reverse edge. Q97 / Q18 resolves partial predecessors: a newer update requires its older update to be Built or fully selected with an assembly in this batch. Revisited candidates allow an urgent successor into the same batch after that prerequisite becomes ready; partial selection never permits overtaking.
- Six required tests pass: window boundaries, non-urgent brick/assembly caps, 500 uncapped urgent bricks plus 16 budgeted bricks, partial predecessor ordering, ordered same-batch replacement verified by rays, and detached-job fields. **1,000 bricks / four batches / one final assembly; 579,872-byte compound byte-identical to single-shot assembly.** Completed-batch finalization is also exercised through the system.
- Validation: explicit compile zero C# warnings; focused **6/6**; EditMode **572 passed + 1 explicit skipped**, PlayMode **37 passed**, perf **31/31**, docs **503 tasks / zero errors**. The test's initial RaycastInput property-mutation compile error was corrected before successful validation. Formal collider timing/memory gates remain PHY-019.
- Next: PHY-011 installs due compounds without waiting in the fixed step, then PHY-012 cleanup. Body initialization, renderer fallback and shaders remain independent ready M1 work.

### 2026-10-11 — PHY-009 collider snapshot collection (astra, gpt-6)

- Implemented the specified install-tick arithmetic and window closure, plus ColliderSnapshotSystem after BrickDirtyScheduleSystem. The Burst collection job chains queue/cache ownership and ECS lookups; it never completes a job in the fixed step and retains copied masks only.
- Six required tests pass: ticks 0–20 match the 7-tick delay / 4-tick bucket table; same-grid requests coalesce by window and escalate priority; latest duplicate masks replace/reuse slots; destroyed, empty, mismatched or malformed bricks become removals; unknown serials are counted and dropped. The tick-order integration schedules a mask writer before the existing dirty producer and proves collection sees that write and remains detached from subsequent live edits.
- No SPEC-10 changes or contract changes were needed; its existing Acquire/Release producer integrates directly. Initial test compile used a nonexistent EntityManager lookup helper; it was corrected to a scheduled test writer system before validation.
- Validation: compile zero C# warnings; focused **6/6**; EditMode **566 passed + 1 explicit skipped**, PlayMode **37 passed**, perf **31/31**, docs **503 tasks / zero errors**. Collider pipeline performance/determinism gates remain PHY-018/019 as specified.
- Next: PHY-010 seals/plans/builds owned snapshots outside the fixed step. REN-003's concrete fallback, shader/config work and PHY-008 also remain ready; no owner decision is needed for this continuation.

### 2026-10-11 — REN-002 Metal renderer spike (astra, gpt-6)

- **No-go for 2,500 mutable registered brick meshes.** With one shared material, one moving root, 512 welded quads / 578 eight-byte vertices per mesh, 1920×1080, URP four shadow cascades over 300 m and Burst safety off, movement adds **1.625292 ms** versus the 0.5 ms gate. Idle GPU median **6.434792 ms**, p95 **10.494958 ms**, from 300 unique positive samples, exceeds 6 ms. Idle presentation **0.313042 ms** passes its own 0.6 ms gate; draw calls **5,035**. Final runs explicitly used Metal MultiThreaded; preliminary single-threaded measurements are superseded.
- All six explicit capability/measurement tests pass as probes; their numbers drive the no-go rather than an assertion hiding the remaining probes. Exact UNorm8 pixel decode covers all 64 layers and 256 palette indices. In-place apply, mesh bounds and ECS RenderBounds for 128 meshes take **0.101791 / 0.115875 ms median/p95**, with visible changed pixels and unchanged registration IDs. The final inclusive apply probe supersedes the narrower 0.112 ms marker. Churn: 10,000 rewrites, 2,000 three-frame retire cycles, retired IDs read back null and final native leak count **0** after warm-up reset.
- `UploadMeshData(true)` changes isReadable to false and prevents later writable-mesh apply. Runtime mesh memory stays **23,128 B** before/after and after five frames. Set ReleaseCpuMeshCopies default false (Q95 / SPEC-11 Q22). Existing retained-copy memory gate remains valid. Imported single-cell hull is centred at zero and exactly 1×1×1 units; SPEC-10 Q10 is resolved.
- ADR-0031 / Q94 permits the existing Entities AABB assembly in PlayMode tests. Proposed ADR-0032 activates the preselected pooled-renderer fallback; REN-003 must finalize contracts, bound allocator slack and prove gates. A full page per small brick would violate memory headroom, so its proposal explicitly flags that requirement. No runtime renderer architecture changes are silently made by the spike.
- Measurement limit: this Editor does not expose a usable full Render Thread frame counter; active RenderLoop is **0.746250 / 0.877166 ms**, and the zero full-thread counters are not a pass. GPU sampling is sparse, so the capture continues to 300 distinct timestamps. The movement failure independently decides no-go. Tools guidance records -force-gfx-mt and safe settings restoration.
- Validation after removing temporary code/shader and restoring settings: compile zero C# warnings; EditMode **560 passed + 1 explicit skipped**, PlayMode **37 passed**, perf **31/31**, docs **503 tasks / zero errors**. Evidence/source remains locally in `Logs/tools/ren002-*` and `Logs/test-results/ren002-*`; no visual assets were retained.
- Next: continue M1 physics snapshot collection (PHY-009); REN-003 remains ready for its concrete fallback implementation. Shader/config work and PHY-008 are also ready; no owner review is being awaited.

### 2026-10-11 — REN-007 LOD1 meshing (astra, gpt-6)

- Implemented 2×2×2 majority occupancy, deterministic structural-attribute selection, border occlusion and exact greedy rectangles. LOD1 positions decode to even cell coordinates; winding, AO and damage flags match the spec.
- Resolved Q93 / SPEC-11 Q20: raw layer/colour zero is valid, so stored face keys use raw key + 1 with separate solid occupancy. Regions containing only props occlude neighbours without emitting structural geometry.
- Triangle counts: HollowStation1M **1,008,384 → 4,096 (0.406%)**; Ship20k **12,200 → 68 (0.557%)**; 200 seeded noise bricks **1,876,500 → 116,258 (6.195%)**. All are below the 25% gate. Independent reference coverage includes key zero, ties, prop occupancy and random borders.
- Validation: explicit compile zero C# warnings; EditMode **560 passed + 1 explicit skipped**, PlayMode **37 passed**, perf **31/31**, docs **503 tasks / zero errors**. PlayMode was repeated after losing its wrapper exit status; the repeat exited 0.
- Next: REN-002 measures the actual Metal renderer and determines CPU mesh-copy policy before build/apply integration.

### 2026-10-11 — REN-006 LOD0 meshing (astra, gpt-6)

- Implemented row-mask face culling, all five shapes, half-cell AO, 33×33 slice welding, AO-based quad diagonals and exact cell-unit bounds. All 500 seeded snapshots match the brute-force triangle reference; independent winding/AO checks and the 289-vertex wall check pass.
- Accepted ADR-0030 / Q92 after proving a legal tiled long-slope brick needs at least 73,728 vertices. Actual meshing produces 77,568 vertices / 40,960 triangles, maximum index 77,567, without wrap. Both mesher contracts now return uint indices; REN-010 must choose UInt16 upload up to 65,536 vertices and UInt32 beyond, and REN-011 must count the actual width. Reference cube-station memory remains unchanged. No geometry is dropped.
- Damaged slab (529 vertices / 640 triangles), 1,000 synchronous single-threaded Burst samples with safety disabled: **36.3 µs median / 40.1 µs p95**, under 60 µs. Isolated painted cube checkerboard: 49,152 vertices / 24,576 triangles, **742.8 / 755.2 µs** (report only).
- Validation: explicit compile zero C# warnings; EditMode **557 passed + 1 explicit skipped**, PlayMode **37 passed**, performance **31/31**, docs **503 tasks / zero errors**. No test or budget was removed. The earlier 35.2 µs focused median is superseded by the full-run measurement above.
- Next: REN-007 downsampling/greedy LOD1. ART-007 and future preview/upload consumers must use ADR-0027 half-cell positions and ADR-0030 index width when integrated.

### 2026-10-11 — REN-005 render snapshots (astra, gpt-6)

- Implemented the Burst parallel snapshot job with read-only ECS lookups, 27 neighbour regions and disjoint 5,832-cell / 512-prop output slices. Structure previews share semantic decoding and support uniform/dense/sparse source records. All 24 long-slope rotations resolve High/Low from canonical footprint offsets.
- Accepted ADR-0029 / Q91: use canonical orientation tables instead of the now-unused shape-table helper input; retain invalid-reference and prop-overflow flags in BrickMeshWork for later apply logging. No jobs log or retain live cell memory beyond the completed snapshot job.
- Four required tests cover every Ship20k brick, all 27 random neighbouring bricks, exact half shapes, missing halos, malformed refs, prop roots and overflow. Initial test-helper compile mistakes were fixed before the successful runs.
- Validation: zero C# warnings; EditMode 551 passed + 1 explicit skipped, PlayMode 35 passed, performance 29/29; docs 503 tasks / zero errors. Grid spawn 1024 bricks 0.3121 ms median. Snapshot scheduling/system budget integration remains REN-010/013, as specified.

### 2026-10-11 — DATA-012 approved slope-kit data (astra, gpt-6)

- Accepted ADR-0028 / Q90 to implement SPEC-04 Q14's documented default. Optional authored shapes follow footprint cells through canonical baking; resolved root Shape supports palette cells, per-cell shapes support long-slope instances, and every resolved shape contributes to the database fingerprint. VOXS format is unchanged.
- Added hip and two-cell long-slope definitions, reusing the angled-hull icon and per-cell HP/mass. Provisional prices 4/8 and HP 50/mass 15 kg per cell are documented for gameplay tuning. Validation admits only the approved structural kit; owner gates for larger blocks remain intact.
- Shipped content now has 78 definitions / 26 blocks, hash `2C10F2E2AF56B986`, zero validation warnings. The migration suite reserialized 21 blocks with the empty optional field and updated its manifest hashes. Its initial report-comparison failure (Updated versus Unchanged) passed on the complete rerun; the retained assets now reproduce identically. Unity's empty-field YAML whitespace is retained for byte-stable migration.
- Validation: compile zero C# warnings; EditMode 547 passed + 1 explicit skipped, PlayMode 35 passed, perf 29/29, docs 503 tasks / zero errors. Content construction medians 0.3754 ms shipped and 5.392 ms synthetic 2000; synthetic 500-block blob approximately 95.9 KB including shape arrays, below 256 KB.
- Next: REN-005 gathers exact per-cell shapes using inverse orientation; later physics keeps its specified cube approximation. Temporary authoring script was removed; no new visual assets or ships.

### 2026-10-11 — REN-004 exact shape and packing tables (astra, gpt-6)

- Implemented all five approved shapes in 24 orientations: 648 outward-wound faces, closed surfaces and rotated full-face masks. C#/HLSL share 42 normals/tangents; packed vertices remain eight bytes and MeshCell remains four.
- Accepted ADR-0027 / Q88 for exact half-cell render coordinates; Q89 fixes long-slope normal ordering. No art or ship geometry was authored. Corrected isolated-cell vertex bound to 49,152; REN-006 still must enforce and verify its mixed-shape UInt16 limit.
- Validation: zero C# warnings, EditMode 544 passed + 1 explicit skipped, PlayMode 35 passed, performance 29/29 gates; docs 502 tasks, zero errors. Required six shape/packing tests pass. Reference build 82.26 ms; grid spawn 1024 bricks 0.307 ms median.
- Follow-up: SPEC-04 Q14's approved two-cell long slope lacks per-cell definition/blob data and is rejected by the current structural validator. Add and complete a dedicated M1 data task before snapshot gathering. ART-007's branch must adopt the half-cell packing convention when merged.

### 2026-10-11 — VOX-015 deterministic dirty scheduling (astra, gpt-6)

- Added the serial Burst IJobChunk drain: own mesh and collider damage work, face-neighbour remeshes, clear/disable of dirty state, and no own mesh for empty bricks. Cached ECS handles/lookups and native-only job fields preserve queue Acquire/Release ownership without a fixed-step completion or managed allocation.
- Q86 puts the equivalent mass-before-dirty annotation on the later PHY-013 system instead of referencing an absent type. Q87 corrects the spec's false no-sync claim for enableable-query IsEmpty: installed Entities synchronizes dirty writers there, so the main-thread early-out uses IsEmptyIgnoreFilter and the job iterator filters disabled chunks. An all-disabled world may schedule an empty job. Collider deduplication remains in its snapshot consumer, as specified by SPEC-12.
- All five required tests pass, including **zero managed allocation/update** and identical sequences over **10 runs each with 1 and 17 workers**, **682 collider / 1,024 mesh requests** across multiple archetypes. Compile zero C# warnings; full EditMode **538 passed + 1 explicit skipped**, PlayMode **35 passed**, perf **29/29**, docs **502 tasks / zero errors**.
- Implementation **a7de460**. All SPEC-10 M1 tasks are now done; its later renderer/physics dependents can proceed. VOX-020 remains the separate M3 editor-edit task.

### 2026-10-11 — VOX-019 damage persistence scenarios (astra, gpt-6)

- Added both required PlayMode scenarios with a test system in GameplayPostPhysicsGroup. One fixed tick changes exactly **50 cells** (40 HP/flag changes, 10 removals) and increments the grid revision once; the fleet-owned source stays unchanged until write-back. Logical cell hashes match after despawn/respawn from the same handle, leaving **19,990 solid cells**.
- The save-time case explicitly copies four dirty bricks, confirms a second write-back copies zero, and disables subsequent despawn write-back so it cannot conceal a failed save copy. Both scenarios use the deterministic test-world tick runner and test-local cell digests, independent of DMG-014's unimplemented world hasher. Each warms up, resets global native tracking, then verifies **zero remaining tracked allocations** after its measured run.
- Test-only implementation **30d39f7**; no runtime correction or technical decision needed. Compile zero C# warnings; full EditMode **533 passed + 1 explicit skipped**, PlayMode **35 passed**, perf **29/29**, docs **502 tasks / zero errors**. Next VOX-015 dirty scheduling. The existing GridSpawnSystem lookup-creation diagnostic is an optimization follow-up for the M1 system review; it does not affect the measured spawn gate (0.314 ms).

### 2026-10-11 — VOX-016 deferred disposal (astra, gpt-6)

- Added the Presentation OrderLast cleanup system and retirement helpers. Blobs wait three frames and two clock ticks, meshes wait three frames; cleanup continues without GameplayActive. Paused physics retains retired blobs until its clock reaches DueTick. GridSpawnSystem retains queue ownership and teardown disposal.
- Required tests verify two-frame survival/third-frame disposal, **100 paused updates** plus the exact resumed-tick boundary, and pending world teardown with **zero tracked native leaks**. PlayMode covers absent-renderer mesh destruction. A separate GPU-enabled Metal batch run verified a real Entities Graphics mesh registration survives two updates, unregisters on the third, and is destroyed at frame end (**1/1 passed**, `Logs/test-results/vox016-gpu.xml`). Restored the incidental QualitySettings serialization change from a pre-run copy.
- Compile zero C# warnings; full EditMode **533 passed + 1 explicit skipped**, PlayMode **33 passed**, perf **29/29**, docs **502 tasks / zero errors**. Implementation **b0141a5**. No spec decision or owner gate needed; next VOX-019 damage persistence integration.

### 2026-10-11 — VOX-003 sparse fallback and memory go (astra, gpt-6)

- Accepted ADR-0023/Q85 and implemented six-byte sorted sparse ECS cells, source sparse pages, exact 1025-cell densification, explicit dense repacking and shared reads throughout clone/diff/mass, compaction, codec, spawn/write-back and ray/placement paths. Uniform bricks and VOXS bytes remain unchanged; binding architecture and downstream snapshot/physics/damage specs are updated.
- Reference memory model now **31,539,808 B** retained-copy peak, below **36,000,000 B** with **4,460,192 B headroom**: **go**. Actual cells/masks 7,278,592 B, root heap 35,168 B (10,464 B baseline), all world/meta chunks 409,600 B; both mesh copies and collider overlap included. Source NativeBytes before release 11,059,664 B, after spawn/release 0 B. Thin-shell stress remains **107,292,880 B**. SPEC-10 §8.3 preserves exact boundaries and local measurement evidence; whole-renderer integration remains TST-004.
- Required 30,000-write sparse/dense fuzz and threshold tests pass. All 138 voxel tests and full EditMode **530 passed + 1 explicit skipped**, PlayMode **32 passed**, perf **29/29**. Compile zero C# warnings; docs **502 tasks / zero errors**. Sparse codec encode/decode **32.08/51.34 ms**, 100k-cell diff **1.256 ms** (10 ms budget), station build **82.52 ms**, dense spawn **0.318 ms**; unchanged write-back **0.00030 ms**.
- Implementation **3d14ffa**. M1 continues with remaining ready scheduling, cleanup and integration tasks; no owner decision blocks implementation.

### 2026-10-11 — VOX-014 grid lifecycle and damage write-back (astra, gpt-6)

- Rebuild replaces voxel children in batches while retaining root identity, serial, pose and unrelated components; new source reads are pinned at API submission and transferred independently of an unfinished old spawn. Despawn includes stowed roots, releases owned reads before terminal write-back, and destroys linked children.
- Write-back copies only bricks newer than SyncedRevision, removes destroyed source bricks, copies instance/palette metadata without ECS references, rebuilds free indices and invalidates cached mass. Only copied bricks are optimized. CaptureChanges remains relative to initialization even after a save and replaces its output containers.
- ADR-0026/Q82–84 resolves missing removal provenance and partial-spawn data loss: internal original-coordinate baselines distinguish destroyed from never-occupied/uncreated slots. The reference baseline adds **10,464 payload bytes**, plus buffer header/capacity overhead for VOX-003 to measure. Ordinary saves defer unfinished spawns; terminal despawn preserves uncreated source bricks. Required tests cover a 2500-brick partial despawn retaining 2499 bricks after exactly one destruction.
- All six lifecycle tests pass; the existing changed-only test also measures unchanged reference-station write-back: **0.00030 ms median / 0.00040 ms p95**, twenty samples, five warmups, against 0.2 ms. The full run caught a Temp allocator on a Burst cleanup collector; changed to TempJob and reran all checks. Spawn remains **0.316 ms median** for 1024 dense bricks.
- Compile zero C# warnings; full EditMode **528 passed + 1 explicit skipped**, PlayMode **32 passed**, perf **29/29**, docs **502 tasks / zero errors**. Implementation **d91def5**. VOX-003 is now ready: accept/refine ADR-0023 and implement/measure the sparse fallback across the completed dense foundations.

### 2026-10-11 — VOX-013 budgeted grid spawning (astra, gpt-6)

- Implemented spawn singletons before the boot snapshot, batched root/brick/functional creation, cell/mask and transform filling, linked groups/brick slots, hooks, root profiles and module-union prototypes. Cached source mass initializes GridMass without a recompute; headless grids omit rendering and GPU cleanup components. Fixed-step stow/unstow uses ECB playback for the whole linked group and resets pose, velocity, interpolation and optional grace.
- ADR-0024 gives each request one registry-owned read token, retained across released public handles and cancelled on completion, destroyed requests/roots, scene exit or composition/world teardown. Composition cancels reads before structure owners dispose. ADR-0025 resolves the ECB-only stow API's impossible conditional component inspection. Q77–81 also record input/spawn ordering without a forbidden module reference, corrected Ship20k entity counts, and headless cleanup ownership.
- All eleven required integration tests pass: Ship20k has ten bricks plus three functional entities (fourteen linked entries); all cells/masks match; Lattice2500 completes in exactly three 1024-brick-budget updates; source pin count returns to zero. Shutdown testing found and removed a forbidden GetExistingSystemManaged call inside OnDestroy. The required 1024 dense-brick gate measures **0.308 ms median / 0.360 ms p95**, twenty samples, five warmups, headless, against 6 ms.
- Compile zero C# warnings; full EditMode **522 passed + 1 explicit skipped**, PlayMode **32 passed**, perf **28/28**, docs **502 tasks / zero errors**. Implementation **b6d6708**. Next VOX-014 rebuild/despawn/write-back, then VOX-003's complete sparse fallback.

### 2026-10-11 — VOX-021 mesh priority queue (astra, gpt-6)

- Q75 extracts the existing queue methods and four tests from REN-010 to break its cycle through voxel spawning. The task lives in SPEC-10 so producers have explicit same-spec dependencies while REN-010 retains its existing SPEC-10 dependency through REN-005; dependency grammar stays unchanged. Total ledger: **502 tasks**.
- Implemented Damage/Neighbour/Spawn/Lod FIFO queues, equal/worse-priority deduplication, upgrades, pending count, stale skipping and drained-list reuse. Burst producers carry native queue containers, construct a local view inside their job, and leave Access guards on the main thread; the fixed-step producer/main-thread consumer test passes with safety checks on.
- Required StaleSkipped exposed an old request reviving after a promoted brick was consumed and requeued. Q76 permanently invalidates the prior entry on upgrade, preserving fresh FIFO/grid data without changing public fields. Evidence: ignored Logs/tools/vox021-stale-before-fix.xml. The 2,048-brick promotion/stale test takes **0.0087 s** (focused test, not a performance gate).
- Compile zero C# warnings; full EditMode **511 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **27/27**, docs zero errors. Next VOX-013 spawning, then VOX-014 lifecycle and the sparse fallback.

### 2026-10-11 — DMG-001 damage contracts (astra, gpt-6)

- Added every public §4.2–4.6 type, including impulse/restore channels and SplitGrace, the byte DamageType enum, default config and table-backed armor multiplier lookup. Non-contract implementation methods remain task-labelled stubs. All value types are unmanaged; IntegritySeed and ChunkCell use zero inline capacity.
- Q71–73 resolve missing supporting blob declarations, authoring/runtime separation (43 numeric defaults plus four cue references), and the otherwise unowned DamageSetup component lists. Q74 adds the actual DMG-001 dependency of grid unstow to VOX-013.
- Four required tests pass, including all 16 v1 table multipliers, absent-table/unknown-type/class fallback and matching authoring defaults. Compile zero C# warnings; full EditMode **507 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **27/27**, docs zero errors.
- Spawn preparation also exposed a queue dependency cycle: VOX-013 needs BrickMeshQueue.Enqueue, currently deferred to REN-010, which depends on SPEC-10 through REN-005. Next split the specified queue implementation/tests into an independently ready contract-support task, then implement VOX-013.

### 2026-10-11 — VOX-008 grid-space raycasting (astra, gpt-6)

- Implemented allocation-free DDA with direction normalization, zero-direction rejection, distance-zero interior hits, X/Y/Z boundary ordering and empty-brick lookup skipping. Structure masks and the shared ECS reader provide equivalent query adapters. Boundary distances use doubles internally to avoid accumulated drift; public grid-local inputs/results stay floats.
- Five required tests passed. **10,000** seed-123 rays through a seed-7 random 64³ structure matched the independent 0.001-cell reference (**6,028 hits**, 0.114 s focused test). The reference also checks tiny segments with slabs so grazing intersections between samples are not missed. **1,000** further rays matched structure/ECS results. A ray through 100 empty bricks made **101 cell + 100 brick queries**, below 300.
- Compile zero C# warnings; full EditMode **503 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **27/27**; docs zero errors. Next DMG-001 contracts (SplitGrace is needed by grid unstow), then VOX-013 spawning and VOX-014 lifecycle before the sparse fallback.

### 2026-10-11 — VOX-011 registry, blueprint cache and boot ownership (astra, gpt-6)

- Registry handles are session-unique and borrowed; release/destruction never disposes owner storage. Spawn hooks retain registration order and deduplicate by identity.
- Blueprint library decodes once per cached hash, marks structures read-only, logs one failure per invalid payload and evicts only unpinned structures above 65,536 cells. Disposal releases all cached native storage after readers unpin. Boot registers both services and a BootSingleton-tagged registry reference before the scene snapshot; failure/normal teardown remains reverse ordered.
- Six required tests passed, including 65,536/69,632-cell eviction boundaries and zero native leaks. Full validation: compile zero C# warnings, EditMode **498 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **27/27**, docs zero errors.
- Next: VOX-008 raycast, then spawning/lifecycle. VOX-013 must cancel outstanding source pins before service teardown and scene destruction; the library intentionally refuses to free pinned storage.

### 2026-10-11 — VOX-009 structure VOXS adapter (astra, gpt-6)

- Implemented canonical structure-to-source expansion, encoding through the existing codec, full-payload decoding, palette/instance restoration, uniform optimization and content hash. Source buffers own only coordinate/cell copies. No second byte format was introduced.
- Regression proved raw repair collapsed a valid three-slot instance table to one; unreferenced block-zero tombstones now retain their slots, while existing D3 drift repair still applies (Q68 / SPEC-05 Q26). Restored free indices are ascending/LIFO; raw empty palettes get reserved slot zero (Q69). Worker-safe encoding uses TempJob sort storage (Q70). Failure evidence: Logs/tools/vox009-tombstone-before-fix.xml.
- Eight tests pass: required five plus tombstone reuse, partial/truncation rejection and managed-worker byte equality. Empty, Ship20k, reference station, asteroid and 5,000-op damaged/tombstoned structures round-trip byte-identically, compressed and uncompressed. Reference adapter+encode **22.48 ms median / 23.75 ms p95**, decode+adapter **58.21 / 60.42 ms**, budgets 60/150 ms (11 samples). Separate perf records preserve both gates.
- Validation: compile zero C# warnings; full EditMode **492 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **27/27**, docs zero errors. Next VOX-011 registry/cache and boot integration.

### 2026-10-11 — VOX-017 benchmark structures (astra, gpt-6)

- Implemented reference/thin-shell stations, solid asteroid, Ship20k box and 2,500-brick lattice with deterministic per-brick generation and uniform interiors. Geometry stayed as specified. Independent enumeration corrected the asteroid to **998,592 cells / 408 bricks** and Ship20k to **10 bricks** (Q66); explicit helper/codec dependencies are Q67.
- Both specified tests pass. Reference **1,008,128 cells / 872 bricks**; thin shell **1,043,464 / 3,784**; ship **20,000 / 10**; lattice **2,500 / 2,500**. Reference build **87.43 ms median / 88.30 ms p95**, budget 300 ms (11 samples, three warmups, Apple M5 Pro).
- Validation: compile zero C# warnings; full EditMode **484 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **25/25**, docs zero errors. Handoff updated. VOX-009 next; investigate the observed tombstone/drift-repair mismatch in its byte-identical adapter acceptance.

### 2026-10-11 — VOX-007 clone, diff and mass (astra, gpt-6)

- Implemented deep native clone with exact list capacities and reset pin/read-only flags; Burst IJob diff with sorted brick union, unchanged dense-page skip, resolved palette comparisons and instance multiset merge; per-brick parallel double mass sums with canonical serial reduction and revision cache. GetBlocks remains the implementation brought forward in VOX-006.
- Six specified tests pass: clone independence, 1,000-op edited-target reconstruction (including compacted palette/instance indices), HP/flag-independent diffs, brute-force mass within 1e-9 at three cell sizes, cached mass invalidation and three bit-identical computations. Diff of 100,000 cells / 10 changed blocks: **0.1274 ms median / 0.1379 ms p95**, budget 10 ms (21 samples, five warmups, Apple M5 Pro).
- Validation: compile zero C# warnings; full EditMode **482 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **24/24**, docs zero errors. Next benchmark generators, then VOX-009 structure codec.

### 2026-10-11 — VOX-006 block placement and compaction (astra, gpt-6)

- Implemented whole-footprint validation before placement, palette deduplication, one-instance multi-cell references/root flags, removal from any footprint cell, tombstone-slot reuse and deterministic palette/instance/page compaction. GetBlocks was brought forward from VOX-007 for the required acceptance oracle (Q65).
- Seven specified tests pass: all 16 thruster orientations, each of ten removal cells (including brick crossings), byte-identical occupied rejection, dedup/reuse, and seed-7 histories of 5,000 edits. Compaction preservation **0.015 s**, determinism **0.167 s**. The capacity assertion caught NativeList’s rounding setter; TrimExcess now shrinks the 45,056-cell test page pool exactly, preserving HP/flags and releasing unused capacity.
- Validation: compile zero C# warnings; full EditMode **476 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **23/23**, docs zero errors. Next VOX-007 clone/diff/mass, then the structure codec and spawning.

### 2026-10-11 — VOX-010 voxel definitions and boot configuration (astra, gpt-6)

- Implemented the editor payload setter (owned byte copy and cached summary), config factory, default config_voxel asset and database registration. Boot creates VoxelConfig in table order and tags it before the scene snapshot. Q64 corrects the stale catalog-taking call in SPEC-01 to the declared definition-taking factory.
- Two specified tests verify failing/passing E001–E003 fixtures, payload metadata, nondefault factory values, shipped defaults and real ContentService-to-boot composition. One-off authoring reported **0 errors / 0 warnings**; its script/meta were removed, evidence retained under Logs/tools/vox010-*.
- Validation: compile zero C# warnings; full EditMode **469 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **23/23**, docs zero errors. Handoff updated; next block placement/compaction and remaining M1 foundations.

### 2026-10-11 — VOX-012 ECS cell helpers (astra, gpt-6)

- Implemented BrickWrite and GridCellReader with read-only lookups refreshed per update. Writes preserve no-op state and maintain solid masks/counts, revisions, mesh/collider flags and cumulative border masks. Reader returns false for air (Q63 / SPEC-10 Q19).
- Four specified tests pass, including all 4,096 cell positions for boundary/HP-tier changes (**0.030 s**) and Burst write/read jobs with safety checks enabled. Negative coordinates, air inside a brick, empty slots, outside-grid lookup and missing root are covered in the Burst test.
- Validation: compile zero C# warnings; full EditMode **467 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **23/23**, docs zero errors.

### 2026-10-11 — VOX-005 structure storage (astra, gpt-6)

- Implemented persistent structure ownership, dense pages and uniform records, free-page reuse, swap-back map repair, mask/count/revision maintenance, lazy bounds, canonical z/y/x ordering, pin/read-only guards, clear and idempotent disposal. Corrected the stale 32-byte spec comment to Q13’s established 28-byte BrickRecord.
- Seven specified tests pass. The 100,000-write seeded dictionary/bounds comparison took **0.503 s**; uniform expansion preserves all 4,096 values, reused pages contain no stale cells, and leak detection reports clean disposal.
- Validation: compile zero C# warnings; full EditMode **463 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **23/23**, docs zero errors. Sparse migration remains VOX-003 after VOX-014 foundations.

### 2026-10-11 — VOX-003 dependency correction (astra, gpt-6)

- Inspection found VoxelStructure/BrickWrite/GridCellReader stubs and no GridSpawnSystem or structure codec. VOX-003 cannot verify its required cross-path equivalence yet. Added VOX-014 as a transitive foundation dependency and returned the task to todo (Q62 / SPEC-10 Q18).
- ADR-0023 remains proposed; no binding contract or code changed. Continue VOX-005, then remaining ready M1 foundations; sparse implementation and its 36 MB measurement remain required. Documentation checks pass.

Newest first. One entry per finished task or notable event: what changed, measurements, follow-ups.

### 2026-10-11 — PHY-007 collider pipeline data and ownership (astra, gpt-6)

- Added record/map swap-back repair, reusable mask/update pools, collecting lookup maintenance, sorted child handover, retirement and incremental byte/count statistics. Finalize runs as a Burst job over C/B only; Access and batch State stay on the main thread. All three singleton summaries document the required access pattern.
- Debug invariant checks cover ordered update prefixes, live batch references, exactly one blob owner (borrowed assembly snapshots excluded), collecting maps, mask/free pools and counters. Five required tests pass: **10,000 record operations** in **0.480 s**, **1,000 randomized pipeline cycles** in **0.064 s**, ordered multi-update replacement/removal, partial/full discard, and leak-free teardown with a pending build plus blobs in every owner.
- Q59 clarifies that partial discarded updates remain allocated until the batch finishes, even without an assembly. Q60 defines inner capacities and PendingBricks as unscheduled entries. Q61 adds the already-completed PHY-005 dependency for the shared coordinate comparer. No public contract/assembly change.
- Compile zero C# warnings; full headless EditMode **456 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **23/23**, all process exits 0; docs zero errors/warnings. Implementation `a936342`; handoff updated. Next: resolve ADR-0023 and implement VOX-003's measured sparse fallback.

### 2026-10-11 — PHY-006 deterministic mass maths (astra, gpt-6)

- Implemented double-precision tensor construction, 16-sweep cyclic Jacobi with stable eigenvalue order and canonical right-handed axes, dynamic/kinematic PhysicsMass, corner-based angular expansion and velocity re-expression. Planar inertia locks x/z exactly. Non-finite re-expression preserves the old velocity and logs once through a Burst-compatible atomic flag reset at SubsystemRegistration.
- Six required tests pass: analytic boxes at three scales; L-shape plus **500 random structures** reconstruct tensors within **1e-9 relative**; **128** repeated managed/Burst eigensystems give bit-identical repeats in each execution mode; **1,000** planar/free mass-frame changes preserve **8** sampled point velocities each within **1e-4 m/s**. The test oracle sums cells independently because VOX-007 is not this task's dependency.
- Compile zero C# warnings; full headless EditMode **451 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **23/23**, all process exits 0; docs zero errors/warnings. Implementation `1c4b85f`; handoff updated. No contract change or new spec question. Next: PHY-007 pipeline containers/ownership.

### 2026-10-11 — PHY-003 physics configuration and boot (astra, gpt-6)

- Added the default config_physics asset, field-copy factory and PhysicsStep factory with the installed API verified by PHY-002. Validator reports PHY-E001 for delay ≤ 6 and PHY-E002 for bevel > 0.25, with asset path and field.
- SPEC-01 coordination: its §5.3 already specified both rows; BootSingletons now creates PhysicsVoxelConfig and PhysicsStep in that order before the boot-entity snapshot, with BootSingleton tags. No lazy singleton creation or contract change.
- Four required tests pass, including real content → factory → boot singleton readback and the two validation failures. Temporary asset-authoring script removed; authoring report 0 errors / 0 warnings.
- Compile zero C# warnings; full headless EditMode **445 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **23/23**, all process exits 0; docs zero errors/warnings. Implementation `e14cc32`. Next: PHY-006 mass maths.

### 2026-10-11 — DATA-011 reactor and cargo content (astra, gpt-6)

- Authored and registered `block_reactor_small` (8 cells; PowerSource 60 + Explosive radius 4 / damage 400) and `block_cargo_small` (2 cells; Cargo 50), with every SPEC-04 §4.10 scalar, category, price and rarity. Reused the exact existing mesh/sprite references. No new art, ship design or owner decision.
- Temporary CLI Editor authoring job removed after success; report in Logs/tools/data011-authoring-report.txt has **0 errors / 0 warnings**. Existing project validation and the required V2Additions test check authored values, baked masks/categories/footprints and presentation references.
- Compile zero C# warnings; full headless EditMode **441 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **23/23**, all Unity processes exit 0. Shipped content construction **0.340 ms median / 0.352 ms p95**. Docs zero errors/warnings. Implementation `7158bf8`; handoff updated. Next: PHY-003, now unblocked.

### 2026-10-11 — PHY-005 brick and grid collider builders (astra, gpt-6)

- Implemented SPEC-12 §5.3/§5.4: empty → Null, one box stays a BoxCollider, multiple boxes become a copied compound with temporary child disposal. Grid assembly preserves caller-owned children and applies brick coordinates × 16 × cell size. Coordinate comparison is (z, y, x) without integer-subtraction overflow.
- Eight required tests pass under Burst/managed use. Coverage checks nine seeded masks × four caps, proves all four merge levels were reached, casts quarter-cell and cell-centre lattices in both directions plus cell-local segments, and checks exact air stays empty. Nested positive/negative offsets, sorted child transforms, child disposal and leak detection pass.
- Diagnostic 50% noise brick build: **9.7 µs median / 12.8 µs p95**, current test safety settings; formal reference gate remains PHY-019. Compile zero C# warnings, full headless EditMode **440 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **23/23**, all process exits 0; docs zero errors/warnings. Implementation `a54c29f`. No new spec question or contract change.

### 2026-10-11 — PHY-002 nested physics capability spike (astra, gpt-6)

- **GO for nested compounds.** All ten answers are recorded in Measurements. Two explicit headless tests pass; copied children survive disposal, nested bodies produce 16 contact points / 4 events, None stays queryable without contacts, and body-pair disabling suppresses contacts/events. PHY-020 is done as not needed, exactly as its conditional scope specifies.
- Smoothed child LocalToWorld matches the parent's rendered pose with **0 m error** over 30 frames; the parent differs from simulation by up to **0.500001 m**. The check uses Unity's rate manager after the counted test helper's clock offset saturated interpolation; no production smoothing change. Evidence is transform readback, not a GPU screenshot.
- Box **492 B**, 64-/1,000-box compounds **37,216 / 579,360 B**; build64 median **23.1 µs**, assembly872/3784 **0.2258 / 1.0585 ms**. Q58 corrects SPEC-12's size estimates and stabilization member. These synthetic timings do not close PHY-019's live-pipeline gates.
- Disposable source removed; reproduction and reports retained in ignored Logs/tools. Compile zero C# warnings; full headless EditMode **432 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **23/23**, all Unity processes exit 0; docs zero errors/warnings. Implementation `dd85d8c`. Next: PHY-005 builder/assembler, then remaining ready M1 work.

### 2026-10-11 — PHY-004 brick box merger (astra, gpt-6)

- Implemented the specified x-run/y/z greedy cover, conservative 2/4-cell super-cell covers and bounded fallback. Scratch masks are stack values; output storage is the only native allocation. No public contract change or new spec decision.
- Seven required tests pass: exact union/disjointness for **10,000** seeded masks across five densities plus structured masks; coarse supersets; box limits and bounds fallback; full/slab/checkerboard; repeated/Burst-versus-managed order. The 10,000-mask test took **1.389 s** in the full suite.
- Compile zero C# warnings; full live EditMode **432 passed** in **40.37 s**, PlayMode **31 passed**, perf **23/23**; docs zero errors/warnings. Implementation `e1679aa`. Next: PHY-002's installed-Physics capability spike; sparse storage still requires ADR-0023 resolution.

### 2026-10-11 — VOX-002 memory spike completed (astra, gpt-6)

- Both explicit headless reports pass with exact expected cell/brick counts. Reference has 504,192 faces / 570,496 welded vertices; thin shell has 2,086,936 / 2,356,632. Matching runtime-render ECS layout holds 58 entities per chunk, including BrickRenderState added after Instantiate; meta chunks are included in byte totals.
- **No-go:** reference retained-copy peak **38,828,288 B**, exceeding 36 MB (and 36 MiB). VOX-003 remains todo and ADR-0023 is proposed. Sparse payload projection is 31,373,568 B before additional headers/slack; it is not an implementation pass.
- Thin-shell retained-copy peak **162,606,784 B** is report-only (Q57). No owner gate changed. Full per-item, geometry, mesh, compound and map-capacity measurements are in SPEC-10 §8.2 and local evidence Logs/tools/vox002-*. The spike source is removed as specified; its diagnostic copy is retained under Logs/tools for reproducibility.
- After removing the temporary spike: headless compile zero C# warnings; EditMode **425 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **23/23**, all processes exit 0; docs zero errors/warnings. Implementation/evidence commit `6393ecd`.

### 2026-10-11 — PHY-001 physics contracts (astra, gpt-6)

- Added SPEC-12 public contract types, exact category/query filters and materials, config definition, internal pipeline layouts, and task-labelled algorithm stubs. Queue/cache/batch factories allocate their native storage; teardown completes access/jobs and releases installed, cached, pending, retired and unclaimed-result blobs, preserving borrowed child snapshots.
- Required filter and lifetime tests pass. Q54 clarifies the diagnostic DebugAllQuery exception without changing filters; Q55 supplies initial capacities for otherwise unspecified lists/maps.
- Compile zero C# warnings; full live EditMode **425 passed**, PlayMode **31 passed**, perf **23/23**; docs zero errors/warnings. Implementation `1f27ec6`. M1 has brick maths and render/physics contracts; next is VOX-002's measured memory spike.

### 2026-10-11 — REN-001 rendering contracts (astra, gpt-6)

- Added SPEC-11 public formats, blobs, components, configs and helper signatures; future algorithms throw task-labelled stubs. Queue/batch factories allocate Persistent containers, and disposal waits for their jobs before releasing memory. Container singleton audit includes the four approved rendering owners.
- Applied ADR-0018's three-bit snapshot shape layout and 42-normal contract. Q51 corrects ShapeFace's arithmetic size to **52 B** without changing fields; Q52 makes the managed-state type public with internal fields; Q53 / accepted ADR-0022 adds the existing Entities mathematics-extensions assembly required by AABB.
- Required layout and allocation tests pass, including jobs pending at disposal. Full live EditMode **421 passed**, PlayMode **31 passed**, perf **23/23**, compile zero C# warnings, docs zero errors/warnings. Implementation `42dfd86`. Next: PHY-001 contracts, then VOX-002's measured memory gate.

### 2026-10-11 — VOX-004 brick maths and masks (astra, gpt-6)

- Implemented slot bounds/indexing, border faces/directions, mask bit/row access, counts, bounds, full/empty checks and ECS buffer copies. Existing negative-coordinate helpers retain SPEC-10 semantics.
- Eight required tests cover 10,000 Burst coordinate round trips, exterior slots/corners, random masks against independent brute-force bounds/counts, and real ECS buffer round trips. No spec changes or new decisions.
- Compile zero C# warnings; full live EditMode **419 passed**, PlayMode **31 passed**, all performance tests passed (18 EditMode + 3 PlayMode); docs zero errors/warnings. Implementation `508c75f`.

### 2026-10-11 — M0 exit gate passed — system-review: astra (gpt-6)

- **Scope:** all 66 original M0 tasks plus five review repairs (71/71). Reviewed boot/composition and teardown, shared state/event ownership and deterministic order, content identities/blobs/validation, VOXS and save capture/read/write lifetimes, repeatable migration, input/audio disposal, dev scenarios and release exclusion. Applied the workflow §7.2/§8 and implementation-trap checklist where these systems exist; rendering, collision and gameplay systems remain M1+ scope. Frank explicitly authorized this session's review; it is **not an independent second-author review**.
- **Findings fixed:** TST-009 shared Unity tool/test ownership; PERS-016 abandoned capture snapshots; PERS-017 post-commit sidecar outcomes and stale metadata; PERS-018 null JSON objects; CORE-013 dropped disposal handles and open-read producer races. Each has a claimed task, implementation, regression evidence and ledger closure. Decisions Q47–50 resolve spec omissions without public layout/API changes.
- **Functional/safety evidence:** clean headless EditMode **411 passed + 1 explicit skipped**, PlayMode **31 passed**, all processes exit 0. A separate live pass with Burst safety checks, job debugger and EnabledWithStackTrace leak detection held on also passes **411/31** (Logs/tools/m0-safety-review.json and XML). Six Python tool tests pass. Latest compile has zero C# warnings; check-docs reports **33 specs / 501 tasks / zero errors or warnings**.
- **Boot/re-entry:** Boot, MainMenu and World each passed two complete start/stop cycles (m0-boot-review.json). Running: exactly one GameRoot, Default World and listener, no runtime errors/exceptions. Stopped: zero Default Worlds, services and pending events; timeScale 1. Iteration medians **2.223 s compile / 0.176 s Play-to-menu / 35.396 s EditMode** are within 10/1/60 s; the final safety suite took 36.93 s.
- **Persistence/content integration:** round-trip byte equality and unknown/newer chunk preservation pass, including actual native VOXS snapshots and failure cleanup. Legacy migration repeats all **35 items, 21 blocks and 13 ships** with stable bytes and zero validator errors; existing documented warnings remain nonfatal.
- **Performance:** explicit reference-setting pass (Burst safety/job debugger off, leak detection disabled) passes **23/23** records. Capture **0.155 ms**, read+restore **15.41 ms**, list 50 slots **6.212 ms**; encode/decode 1M **13.19/13.33 ms**; reference compressed ship **15,230 B**; event write/read pair **1.4/2.4 µs**; 200k teardown **7.691 ms**. Evidence retained under Logs/tools/m0-reference-perf. Restoring Editor settings returned a transport timeout; fresh readback confirmed all original values restored before continuing.
- **Shutdown follow-up:** clean headless functional/perf logs contain zero aged TempJob warnings or native-crash markers after CORE-013. Four regressions proved the concrete ownership defect. The old MIG-007 native crash remains retained; its exact cause is unproven and no crash reproduced in the clean runs. Reinvestigate if it recurs; no known M0 gate failure remains.
- **Gate result:** M0 passes ROADMAP's objective gates. No separate M0 owner-signoff requirement is specified. Continue M1 in ROADMAP order; owner art/narrative gates remain intact. Frank should review Q30's bounded/full-palette fallback, Q40's development starting ship, and Q48's commit-point clarification; no owner question blocks implementation.

### 2026-10-11 — CORE-013 event stream cleanup ownership (astra, gpt-6)

- Q50 / SPEC-02 Q15 fixes discarded NativeStream/NativeArray disposal handles. LastConsumer now retains prior cleanup, readers and all disposal jobs; an abandoned open read completes its registered writers before disposal. No public contract layout or ordering changes. Trap T2.13 documents the rule.
- Four zero-worker regressions failed on old code (three leak assertions plus an explicit writer/deallocation safety error) and pass after the fix. Evidence: Logs/tools/core013-before-fix.xml/.log and core013-after-fix.xml.
- Clean headless compile zero C# warnings; EditMode **411 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **23/23**, all processes exited 0. Event write pair **1.3 µs** median; read pair also passes the 5 µs gate and zero-GC assertion.
- All four headless test logs now have **zero** aged TempJob warnings and native-crash markers. The preceding perf run retained in Logs/tools/core013-before-fix-perf-shutdown.log had aged allocations at shutdown despite exit 0. The original MIG-007 crash is not reproduced and its exact cause remains unproven; this concrete ownership defect is fixed.
- Implementation `125af7d`. M0 **71/71**; final system-review safety pass remains before gate recording.

### 2026-10-11 — PERS-018 null metadata handling (astra, gpt-6)

- JSON null/empty input now counts as an unreadable object at profile and META boundaries. Listing skips a malformed profile with one warning; load keeps valid participants and adds PERS-W013. Generic JSON null semantics remain available to participants. Q49 / SPEC-05 Q25 records the boundary choice.
- Implementation `e5f9756`; clean headless compile zero C# warnings, EditMode **407 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **23/23**. Every headless process exited 0. Listing 50 slots **5.483 ms** median; capture **0.121 ms**.
- Review evidence: two start/stop cycles each from Boot, MainMenu and World passed (Logs/tools/m0-boot-review.json): one root/world/listener, no runtime errors/exceptions; after exit zero services, pending events and Default Worlds, timeScale 1. A separate headless baseline passed 403 tests and exited 0 before this fix.
- M0 **70/70**, review ongoing. The historic shutdown crash did not reproduce, but its cause is not established. Code review found SimEventChannel drops EndRead disposal handles and can outlive Dispose; that is the next repair.

### 2026-10-11 — PERS-017 save commit outcomes and metadata (astra, gpt-6)

- M0 review found that a metadata write failure after File.Replace reported failure despite a committed world.sav and advanced backup. Q48 / SPEC-05 Q24 makes replacement the commit point: later sidecar failures warn and return success. The complete container remains loadable and async completion/GameSaved reflect the disk outcome.
- Listing compares readable embedded META with the sidecar and prefers committed data on mismatch (MetaMissing), including same-timestamp saves. Total listing reads remain bounded to 64 KiB per slot, with at most 32 KiB for the optional sidecar. Oversized sidecars fall back to the container prefix.
- Real filesystem failure tests cover first save, overwrite, backup equality and async event/state. Compile zero C# warnings; full EditMode **403 passed**, PlayMode **31 passed**, perf **23/23**; listing 50 slots **5.392 ms** median / **14.12 ms** p95, against 50 ms.
- Implementation `527aa5f`. M0 **69/69**, broader review continues. Next review checks include malformed profile/META JSON null handling and clean headless exit reproduction.

### 2026-10-11 — PERS-016 session capture failure cleanup (astra, gpt-6)

- M0 review found native snapshots remained owned by an abandoned CapturedSave when session metadata or thumbnail capture threw. All main-thread capture stages now release writers/blocks and report ParticipantFailed through normal completion and queue cleanup.
- Q47 / SPEC-05 Q23 resolves the omitted context-callback failure semantics. Tests prove unchanged world/backup/meta files, no encoding, exactly one failure completion, a working subsequent save, and actual native snapshot disposal on all three callback failures with leak detection enabled.
- Implementation `9040fdd`; compile zero C# warnings; full EditMode **399 passed**, PlayMode **31 passed**, perf **23/23**. Capture **0.124 ms** median. M0 **68/68**, review continues; post-replacement metadata failure remains the next confirmed finding.
- Review iteration run (before this fix): no-change compile **2.223 s**, Play-to-menu **0.176 s**, full EditMode **35.396 s**, all three gates passed. Logs/iteration/results.json holds three samples per gate.

### 2026-10-11 — TST-009 Unity tool ownership and test identity (astra, gpt-6)

- Compile, test, iteration and playtest wrappers share an OS project lock, including nested commands. Process exit releases ownership. Live tests preflight external activity and verify the exact requested test inventory plus summary counts before writing XML; failed preflight preserves previous results.
- Six Python unit tests pass. Real overlap checks rejected a competing compile without changing its log and rejected PlayMode while an external Pipeline request ran, preserving previous XML. That external request completed normally (4 passed). Evidence: Logs/tools/tst009-overlap.log and tst009-external-run.json.
- Compile clean with zero C# warnings; full EditMode **395 passed**, PlayMode **31 passed**, perf **23/23**. The installed Pipeline still exposes no server run ID: direct GUI/CLI bypasses the lock, and identical-suite external replacement cannot be distinguished. Inventory validation detects wrong mode/filter, without claiming stronger server identity.
- Implementation `25598ab`. M0 **67/67** including this review fix; broader system review continues.

### 2026-10-11 — M0 system review started (astra, gpt-6)

- Scope: boot/composition/scene teardown, deterministic core/content contracts, persistence/VOXS, legacy migration, input/audio lifetimes, harness and release strip. Frank's explicit instruction authorizes this session to perform the review; it is not an independent second-author review.
- First confirmed finding: Pipeline has one shared async result channel with no run ID or result-mode field. Earlier overlapping EditMode/PlayMode requests returned the wrong suite. Added TST-009 for project-level tool ownership, preflight and exact requested test-inventory checks. No gate pass is claimed while review/fixes remain.
- Revisit the retained MIG-007 headless shutdown crash with clean headless functional/perf runs; previous live runs and the DEV-020 one-off job exited cleanly, but that does not establish its original cause.

### 2026-10-11 — DEV-020 release-strip guarantee (astra, gpt-6)

- Slow EditMode fixture compiles actual non-development player scripts into Logs/tools/release-scripts and checks the Game.App bytes and produced assembly names. Build preprocessor rejects dev components/scripts in enabled build scenes, prefab dependencies, Resources and preloaded assets; scene callback covers scenes processed by explicit build requests.
- Mutation readback: removing DevResultKind's guard made the namespace test fail (1 passed, 1 failed, evidence Logs/tools/dev020-missing-guard-results.xml). Source restored with cp. A temporary headless job verified clean content passes, a Resources prefab with DevLogCapture is rejected, and restored content passes (Logs/tools/dev020-build-check.log); job/probe assets removed.
- Compile zero C# warnings; clean full EditMode **395 passed**, PlayMode **31 passed**, perf **23/23**. Restored release Game.App.dll has no dev namespace and no DevTools DLL is emitted.
- Implementation `b4e7461`. All original M0 tasks **66/66** done. System review now begins; the milestone gate is not yet claimed. Frank explicitly authorized this session to review its integrated implementation; this is not represented as an independent review.

### 2026-10-11 — DEV-006 scenario execution and CLI (astra, gpt-6)

- Host pumps bounded scenario steps after operations. Runner handles commands/pending operations, expressions, variables, frame/tick/condition waits, error allowances, timeouts and per-run logs/events; every terminal result writes UTF-8 JSON without BOM. Editor driver starts Boot, retains run results across Play exit and supports status/stop. Shell scripts run single files or selected active tags.
- Q46 / SPEC-70 Q16 adds run IDs/collision suffixes to filenames and marks the intentional failure fixture with the negative tag. Direct shell readback: smoke_boot **exit 0**, smoke_fail **exit 1**, syntax/unknown-probe error **exit 2**, all JSON parsed and required fields verified. Two simultaneous long runs ended with separate `play mode exited` JSON after stopping Play. Tagged smoke suite passed and wrote summary.json.
- Compile zero C# warnings; full EditMode **393 passed**, PlayMode **31 passed**, perf **23/23**. Five specified tests pass. Scenario waiting step **0.0000385 ms** median against **0.1 ms** budget; idle host remains allocation-free.
- Implementation `74fe510`. M0 **65/66**; DEV-020 is the final task before system review.

### 2026-10-11 — DEV-005 scenario language and probes (astra, gpt-6)

- Added location-preserving tokenizer/parser, headers and statements, typed expression trees, variables and explicit probe registry. M0 probes cover scene/frame/tick, context/pause, ready/UI, per-run logs/events, cached ECS queries, enabled listeners and maths. `until` now evaluates real expressions. Scenario contexts own/dispose cached queries and independent counter baselines.
- Q45 / SPEC-70 Q15 resolves unary precedence, uppercase R28 tags and omitted-status default; the exact R28 example parses with explicit test-only future registrations, while production rejects unknown commands/probes.
- Compile zero C# warnings; full EditMode **393 passed**, PlayMode **26 passed**, perf **22/22**. Seven specified parser/evaluation tests pass. Live CLI confirms boot services/entities and one listener, math and event probes, and mixed-kind errors. Temporary test scene's extra camera listener was correctly counted; Boot scene is clean. M0 input context is still None until INP-004.
- Implementation `2f1e10c`. M0 **64/66**; next DEV-006, then DEV-020 and system review.

### 2026-10-11 — DEV-004 runtime input and UI query (astra, gpt-6)

- Runtime key/mouse/click/ui commands share the registry with their existing CLI wrappers. Timed keyboard/mouse release uses unscaled time; action presses follow resolved controls; teardown releases keys, removes owned virtual devices and restores disabled native mice. Idle pump remains allocation-free.
- UI queries inspect visible panel elements and submit buttons through NavigationSubmitEvent. Q44 / SPEC-70 Q14 records the missing M0 UI dependency: real-panel tests use an explicit resolver, and UI-003 will register its owned document for production queries. No placeholder UI or global scene search.
- Compile zero C# warnings; full EditMode **386 passed**, PlayMode **26 passed**, perf **22/22**. Three specified tests pass. CLI readback confirms all four wrappers route to the host; absent M0 UI reports unavailable. Host idle **0.0000067 ms**, zero GC allocation markers.
- Implementation `47e764b`. M0 **63/66**; remaining DEV-005/006/020.

### 2026-10-11 — DEV-003 runtime harness core (astra, gpt-6)

- Boot step 16 now owns/disposes the dev host. Explicit command registry, invariant quoted arguments, ordered async operations with realtime timeouts, thread-safe bounded log capture and typed managed-event counters. Shared help/op/until commands and CLI wrappers; manual CLI readback lists all three commands and confirms failure/missing-expression paths.
- Q43 / SPEC-70 Q13 records staging: three existing event payloads are subscribed now, seven future names reserved; camera/player lookups wait for their owning types. DEV-005 supplies expressions as specified.
- Compile has zero C# warnings; full EditMode **386 passed**, PlayMode **23 passed**, perf **22/22**. Idle host **0.0000016 ms**, **zero GC allocation markers** across 31,000 calls. Six specified tests added; boot order/child assertions updated for the new host.
- Implementation `beab746`. M0 **62/66**; continue DEV-004.

### 2026-10-11 — AUD-003 audio service core (astra, gpt-6)

- Composed the audio service at Boot step 4, adopting the existing empty AudioRoot placeholder. One persistent listener and exactly 60 routed voices; deferred volume writes, pause propagation, target following and idempotent native/managed cleanup.
- Q42 records silent M0 startup without config_audio (cue authoring remains AUD-007). Content enumeration uses the actual database API. No playback selection was pulled forward from later tasks.
- Validation: compile zero C# warnings; full EditMode **382 passed**, PlayMode **21 passed**, perf **21/21**. Added volume math and the five required service regressions. Boot initially caught duplicate AudioRoot creation; fixed by adopting the prefab placeholder and reran all suites.
- Implementation `fdd1a3a`. M0 **61/66**; next DEV-003.

### 2026-10-11 — AUD-002 mixer and audio imports (astra, gpt-6)
- Authored GameMixer with Master plus Music/Sfx/Ui/Ambience, all five correctly named volume parameters, one Default snapshot and an all-groups Editor view. Q41 / SPEC-64 Q13 resolves the manual-UI instruction against Frank’s CLI-only direction: a temporary job used the installed Editor authoring API; runtime API readback and tests verified the saved asset. The job and meta are removed; no runtime reflection.
- Configured and read back **64 real / 512 virtual voices**. Import postprocessor applies streaming Vorbis 0.6 to music, mono/preloaded Decompress On Load Vorbis 0.7 to SFX, and Compressed In Memory 0.6 to ambience. Reimported the existing **5 music + 14 SFX** clips. SFX sample memory **9,213,104 B (8.79 MiB)**, below 24 MiB; no new media assets.
- Six specified tests pass; full live **381 EditMode**, **16 PlayMode**, **21/21 performance records**; compile zero C# warnings; docs clean. M0 **60/66 done**; AUD-003 and DEV-003 ready.

### 2026-10-11 — MIG-016 real migration gate (astra, gpt-6)
- Four permanent gate tests run the real migration twice, check zero validation errors, byte-identical assets/report/manifest, all **69 Unchanged** outcomes and the exact 13-ship table. The performance test also measures each ship's first asset write in a disposable content folder with unchanged copied legacy inputs; temporary assets/input folders are removed.
- Apple M5 Pro: full migration **579.6 ms median / 613.1 ms max** (60 s budget); slowest first-write ship **42.38 ms** (200 ms budget); per-ship working-set upper bound **748,853 B** (10 MiB budget). Memory combines managed heap growth with no intervening collection, conservative source/decoded/RLE native buffers and measured generated-asset storage. Unity's managed allocation-by-thread API returns zero and GC.Alloc profiler samples are durations, so neither is treated as byte evidence.
- Compile zero C# warnings, full live **375 EditMode**, **16 PlayMode**, **21/21 performance records**; docs clean. M0 **59/66 done**. HANDOFF updated; audio and dev runtime remain.

### 2026-10-11 — MIG-008 ship blueprint migration (astra, gpt-6)
- Imported all **13** legacy ships with exact SPEC-72 §5.4 counts. Functional-first overlap resolution, cube orientation and colour validation, instance-root flags, negative brick coordinates and single-core anchors are verified. Every payload decodes back to identical arrays and each instance owns its full footprint. Five coreless ships remain coreless under the documented owner default; overlap drops remain as specified.
- Q39 brings the existing blueprint validator forward from M1 so M0 content can validate. Q40 assigns the empty development start-blueprint link to **ship_base** (four cells, one core); Frank should choose the gameplay starting ship later. Description/price cannot fit the blueprint contract and are explicitly reported as dropped. Temporary Editor job removed.
- Real reruns: **69 Unchanged**, byte-identical manifest/report, **0 errors**, 67 specified migration warnings and 77 informational entries. Eight new tests pass. Full live suites: **371 EditMode**, **16 PlayMode**, **18/18 perf**; zero C# warnings; docs clean. Content construction **0.308 ms median**. M0 **58/66 done**; MIG-016 ready.

### 2026-10-11 — MIG-006 block migration (astra, gpt-6)
- Generated all **21** SPEC-04 block definitions, including independent footprint/clearance checks, exact module values, orientations, attachment masks, and existing prop-mesh references. Expected warnings only: 2 mesh-bounds, 3 overlap-clearance cells and the portal clearance fix (plus the existing missing-start-blueprint warning). No new art.
- Q38 / SPEC-72 Q15 registers stable BlockModuleId reference IDs **before** serialization; Unity rejects reassigning an already serialized reference. Two real reruns report **56 Unchanged**, with byte-identical assets, manifests and reports. The temporary generation job was removed.
- All four specified tests pass. Full sequential live suites: **363 EditMode**, **16 PlayMode**, **18/18 perf**; zero C# warnings; docs clean. Shipped content construction **0.302 ms median**. An overlapping suite request exposed the live runner's shared result channel; that run was discarded and all checks rerun sequentially. Add a concurrency guard in M0 system review.
- M0 **57/66 done**. Next: MIG-008 ship conversion; DEV-003 and AUD-002 also ready. HANDOFF updated.

### 2026-10-11 — MIG-005 non-block item migration (astra, gpt-6)
- Generated all **35** item definitions with the exact SPEC-72 value table, unchanged descriptions and assigned legacy-named sprite sub-assets. Added the Editor-only source-reference asset (Q37 / SPEC-72 Q14), so production converter code does not depend on the placeholder folder. No new art. The report lists **30** intentional data-drop/normalization entries, including socket fields omitted from the abbreviated spec table.
- The temporary headless generation script and meta were deleted after success. Two repeat imports report **35 Unchanged**, with byte-identical manifest and report. Content validation has **0 errors**; only the existing missing-start-blueprint warning remains. Directory refresh now runs only when a new asset folder needs importing.
- The three specified converter tests check all fields against the independent table, all icon names/descriptions and dropped-field reports. Compile: zero C# warnings. Full live EditMode **359 passed**, full PlayMode **16 passed**, performance **18/18 pass**. Shipped ContentService construction **0.173 ms median** (20 ms gate). Docs clean. M0 **56/66 done**; MIG-006 is ready.

### 2026-10-11 — INP-003 interactive rebinding (astra, gpt-6)
- Implemented interactive rebind/cancel, same-map and Global conflict detection, map restoration for the current context, per-binding/all resets and disposal. Global is disabled during rebind, so Escape cancels without firing Back.
- Four specified InputTestFixture tests pass: G survives JSON round-trip, W conflicts with Flight/Thrust, F5 conflicts with Global/QuickSave, Escape is consumed, a second operation cancels the first, reset restores F, and malformed JSON falls back safely. Q36 / SPEC-03 Q6 preserves an existing custom binding on rejection. ADR-0021 adds only the already-installed Input System test-fixture assembly reference; no package or runtime dependency changes.
- Compile: zero C# warnings. Full live EditMode **356 passed** (explicit guard excluded), full PlayMode **16 passed**, performance **18/18 pass**; docs clean. Removed incidental font cache serialization. M0 **55/66 done**. Next: MIG-005 item conversion.

### 2026-10-11 — TST-008 iteration gates and live tooling (astra, gpt-6)
- Added the three-run iteration script. Apple M5 Pro, Unity 6000.6.5f1, a task-owned headless Editor with the live CLI: medians **1.697 s** no-change compile (10 s), **0.159 s** Play-to-menu (1 s), **29.800 s** full EditMode (60 s). Timings include CLI transport/script overhead. All three rows PASS, including restoration of scenes and test hooks; Play checks use a disposable profile.
- The settings test found scene reload still enabled. ADR-0020 and Q35 / SPEC-71 Q7 apply and guard both reload flags, with existing boot/reentry regressions passing. Corrected nested CLI instance discovery and obsolete live test arguments; live tests now poll complete structured results and export XML. Stale results are removed, and process/CLI failures cannot be hidden by passing XML. Manual failure probes rejected failed tests and failed commands.
- Full live EditMode **352 passed** in each of the final three runs (the explicit leak-guard test is excluded by the CLI); full PlayMode **16 passed**, perf **18/18 pass**. Compile has zero C# warnings; docs clean. Removed incidental dynamic font-cache serialization after live tests. M0 **54/66 done**.
- The task-owned Editor remains open for the next implementation tasks. No repeat of the earlier native shutdown crash during these live runs; its retained evidence remains for the M0 system review.

### 2026-10-11 — MIG-004 protected migration writer and runner (astra, gpt-6)
- Implemented asset creation and GUID-preserving updates, strict sorted manifests, guarded field writes, empty-only links, converter discovery/order, catalog rebuilds, Force confirmation, CLI and deterministic reports. CLI smoke run exits 0 with zero errors and the existing missing-start-blueprint warning. Committed the initial empty manifest/report; converters follow.
- Nine migration tests cover unchanged bytes/GUID, hand edits and Force, protected link fills across manifest reloads, link arrays, missing fields, invalid manifests, path escapes/type collisions, deletion/recreation and failing fill callbacks. Q34 / SPEC-72 Q13 resolves manifest adoption of hand edits and defines report byte equality across unchanged runs. No owner decision required.
- Compile: zero C# warnings. Full EditMode **351 passed + 1 explicit skipped**; perf **18/18 pass**. Latest full PlayMode baseline remains **16 passed**; changes are Editor-only. Docs clean. M0 **53/66**; MIG-005 is ready.
- Tooling follow-up for TST-008: the prior MIG-007 perf run wrote passing results but had a native Mono shutdown crash after native allocation lifetime notices. Preserved `Logs/tools/mig007-perf-editmode-shutdown-crash.log` and its dump under Logs. The current full/perf runs passed without that crash; strengthen test-process exit handling during TST-008.

### 2026-10-11 — MIG-007 legacy Euler conversion (astra, gpt-6)
- Implemented exact integer Ry·Rx·Rz rotation matching with the specified one-degree snapping tolerance. All 64 right-angle combinations match Unity Quaternion.Euler on all three axes; shipped export vectors and wraparound cases pass.
- Q33 / SPEC-72 Q12 defines nonfinite rejection and safe modulo reduction before integer conversion for extreme finite angles. No runtime or public-contract change.
- Compile has zero C# warnings; full EditMode **342 passed + 1 explicit skipped**; performance **18/18 pass**. Latest full PlayMode baseline remains **16 passed**; this task is Editor-only. Docs clean. M0 **52/66 done**.

### 2026-10-11 — PERS-012 save performance and size gates (astra, gpt-6)
- Added the specified workloads: capture of five 20k-cell ships plus the reference 1M-cell ship and six small participants; typical save with SESS 1 KB, 2,000 facts, 30 quests, 200 stacks, five ships, 50 star-system delta headers, ten 50-brick Partial VOXS structures and PLOC. They use real snapshots, container compression and VOXS restore.
- Apple M5 Pro, Unity 6000.6.5f1, **10 measurements after 2 warmups**. Medians / p95: capture (including disposal) **0.120 / 0.127 ms** (5 ms); typical read + restore **14.871 / 15.307 ms** (500 ms); 50-slot listing **2.667 / 8.872 ms** (50 ms); VOXS encode **12.487 / 12.736 ms** (150 ms); decode **13.210 / 13.469 ms** (100 ms). These are method micro-gates per SPEC-71 Q6, not rendered-frame measurements.
- Reference ship FLET payload **15,230 compressed bytes** (81,920-byte gate); 1% damage variant **34,810 bytes**, report only. Damage uses an independent seeded stream so thruster positions are identical. Autosave idle averages **0.0000046 ms per call** in 1,000-call batches; existing zero-allocation checks pass. Framework byte-equality gate passes in PERS-011. The full load-to-playable gate belongs to M3/PERS-015.
- Compile: no C# warnings. Full EditMode **339 passed + 1 explicit skipped**, **37.3 s**; performance suite **18/18 pass** (17 gated records and one size report). Full PlayMode baseline remains **16 passed** from PERS-008; only tests changed. Docs clean.
- All M0 persistence tasks are done. M0 total **51/66**; remaining ready work is migration, dev runtime, input rebinding, audio setup and iteration gates. HANDOFF updated. No new owner decisions or remote operations.

### 2026-10-11 — PERS-010 container fixtures and 04-B1 regression (astra, gpt-6)
- Added SaveFixtureHarness (write-once files, chunk readers and test service construction), the Editor fixture menu, and three immutable v1 containers: minimal **375 B**, missing IDs **393 B**, unknown chunk **400 B**. Generated via the documented temporary headless Editor script, then removed the script and meta. The menu invokes the test harness by reflection, preserving assembly boundaries.
- Tests load minimal state, preserve ZZZZ payload/version, compare current-writer bytes with the fixtures, and reject overwriting a fixture. The real shipped ContentService regression replaces both removed IDs with their placeholders, reports exactly **2 PERS-W001** warnings, and raises **OperationCompleted.Success = true**, **WarningCount = 2**.
- Compile: zero C# warnings. Full EditMode **333 passed + 1 explicit skipped**; perf **12/12 passed**; docs clean. Full PlayMode baseline remains **16 passed** (PERS-008); this task adds Editor/test code only.
- M0 **50/66 done**. Next persistence task: PERS-012 (capture, size, typical load and listing gates). No new owner decisions; git stays local.

### 2026-10-11 — PERS-011 framework save round trip (astra, gpt-6)
- Three deterministic participants exercise binary values/content IDs, JSON state and two deferred VOXS structures. Source arrays are disposed before loading into fresh participants, so the re-save cannot accidentally reuse original state.
- Save to a second slot is byte-identical excluding the specified volatile fields. An unknown version-42 chunk survives with identical flags, payload and seeded ID palette. A changed binary value is detected at that chunk's table entry.
- Added the missing PERS-008 prerequisite (Q32 / SPEC-05 Q22); no runtime code or format change. Full EditMode **329 passed + 1 explicit skipped**, perf **12/12 passed**; compile and docs clean. Most recent full PlayMode remains **16 passed** from PERS-008; PERS-011 changes tests/docs only.
- M0 **49/66 done**. HANDOFF updated; PERS-010 fixtures and PERS-012 save gates remain ready. No owner decision added beyond Q30 already recorded.

### 2026-10-11 — PERS-008 deferred VOXS snapshots (astra, gpt-6)
- `VoxsSnapshotBlock` copies all five arrays into owned Persistent allocations at capture. Main-thread mutation cannot change worker bytes; Dispose is idempotent and Encode after disposal throws explicitly. Partial capture allocations are released if copying fails.
- The fake structure participant saves and restores two VOXS payloads through the real SaveService. Tests cover Task.Run and SaveAsync worker execution, main-thread capture failure after a structure participant, filesystem write failure, encoder failure, and zero remaining native leaks.
- Integration found Unity rejects `Allocator.Temp` on managed worker threads. Q31 / SPEC-05 Q21 and trap T2.10 record the fix: one TempJob scratch buffer, disposed before Encode returns. No wire-format or public-contract change.
- Checks: zero C# warnings; full EditMode **326 passed + 1 explicit skipped**, PlayMode **16 passed**, perf **12/12 passed**, docs clean. Codec medians with worker-safe scratch: encode **12.65 ms**, decode **14.51 ms** (gates 150/100 ms).
- M0 **48/66 done**; PERS-012 is ready. HANDOFF §2–3 updated. Decisions for Frank: Q30's pathological full-palette fallback and decoded-data cap; Q31 is a tool/runtime correction. Git remains local.

### 2026-10-11 — PERS-007 VOXS codec (astra, gpt-6)
- Implemented the v1 VOXS encoder/decoder, immutable block-name snapshot and idempotent native disposal. Burst direct-call run loops choose X/Y/Z traversal by byte cost, with uniform-brick encoding; Deflate rejects truncated streams and trailing compressed bytes. Sorting and content IDs preserve deterministic bytes.
- Content repair covers Missing IDs, palette-to-instance drift, instance-to-palette drift, compaction and anchor remapping. Tech-lead decision Q30 / SPEC-05 Q20 documents reconstructed runtime instance state, full-palette fallback and 256 MiB decoded-data limits. Frank should review the pathological full-palette fallback (slot 32767 becomes Missing) and resource cap; normal saves retain the specified layout.
- Tests: 200 seeded 1–40-brick round trips; reference ellipsoid with 300 thrusters; element equality; traversal choice; all format limits; truncation and trailing bytes; 1,000 byte flips; full-palette drift; managed/Burst equality; zero native leaks. Full EditMode **323 passed + 1 explicit skipped** (32.1 s), PlayMode **16 passed**, perf **12/12 passed**. Compile: zero C# warnings; doc check clean.
- Apple M5 Pro: 1M-cell encode median **13.09 ms**, p95 **13.27 ms** (150 ms gate); decode median **15.03 ms**, p95 **15.06 ms** (100 ms gate). PERS-012 owns save/container size and capture gates.
- Follow-up: PERS-008 is ready; M0 now 47/66 done. No remote operations.

### 2026-10-11 — PERS-009 autosave scheduler and save on quit (claude, claude-opus-5-5)
- `AutosaveScheduler` per SPEC-05 §5.9:
  - the 300 s timer counts unpaused time only;
  - Timer and StarSystemEntered requests within 60 s of a save are dropped; SceneTransition always goes through;
  - a blocked request re-checks every 10 s;
  - 0 B per idle frame.
- `SaveService` subscribes `Application.wantsToQuit` while playing and unsubscribes in `Dispose`. `HandleWantsToQuit` approves at once with no session or when blocked; otherwise it saves with reason `Quit` (jumping the queue), gives up after 10 s, then quits.
- Wired: `GameRoot.Update` drives the scheduler. SceneFlow requests the scene-transition autosave on leaving the World (not for a load, new game or recovery) and calls `NotifySaved` after entering by new game or load. Saves and loads call `NotifySaved`.
- Tests: `AutosaveSchedulerTests` (6) and `QuitSaveTests` (4). EditMode 309/309 (+1 explicit), PlayMode 16/16.

### 2026-10-11 — PERS-006 load pipeline and restore orchestration (claude, claude-opus-5-5)
- Load path per SPEC-05 §5.6:
  - `ReadNow`: world.sav, falling back to world.bak (`PERS-W010`); failures are `NotFound`, `NewerFormat` and `Unreadable`; META (`PERS-W013`) and IDPL (`PERS-E012`, empty palette) checks; staged thumbnails;
  - `ReadAsync`: queued; on failure it raises `OperationCompleted` and changes nothing else;
  - `Restore`: participants in registry order, each isolated. Codes: `I020` missing chunk, `W021` newer chunk (preserved), `E022`/`E023` throwing participant, `W024` unread bytes, `I025` unknown chunk preserved, `W001` unknown content, `I002` other content. It also sets the session thumbnails, `CurrentSlot`, `profile.json`, `LastLoadReport`, `GameLoaded` and `OperationCompleted`. A second restore throws.
- The next save seeds the palette from the loaded one, so preserved chunks keep valid indices. A preserved newer-version chunk is written back in place of its participant's capture (SPEC-05 open question 19, ledger 29). `LoadedSave` gains an internal constructor, palette and staged thumbnails.
- Tests: `LoadPipelineTests` (14), one per §7 load row, each with its exact report code:
  - registry order;
  - missing, older and newer chunks; a throwing participant; unread bytes;
  - `ZZZZ` kept byte-for-byte with its palette references valid, and dropped by `ResetForNewGame`;
  - backup fallback; corrupt chunks in both files; both headers corrupt; a newer format; damaged META/IDPL;
  - unknown content becomes the `block_missing` placeholder, with `W001` + `I002`;
  - a failed read touches nothing; a double restore throws; `GameLoaded`.
- EditMode 299/299 (+1 explicit), PlayMode 16/16.

### 2026-10-11 — PERS-005 save pipeline (claude, claude-opus-5-5)
- `SaveService` save path per SPEC-05 §5.3–§5.4:
  - `Register` (registry order; reserved, unknown and duplicate keys refused), `AttachContext`, `CanSave`;
  - the operation queue: a busy autosave is dropped, Quit jumps the queue, more than 4 waiting gives `IoError`;
  - `CaptureNow` on the main thread, then `WriteCaptured` on a background thread: deferred encode, IDPL after the participants, META, container, tmp with write-through, then replace into world.sav with world.bak;
  - thumbnails, `meta.json` last, `DiskFull`/`IoError` classification with tmp cleanup, the main-thread finish with `GameSaved` and `OperationCompleted`;
  - `ResetForNewGame`, the session thumbnail cache, `TryLoadThumbnail`, `Dispose`.
- New `SaveMessages` holds the §5.8 texts.
- SPEC-02 gains `SaveWriter.DisposeDeferredBlocks()` (open question 14, ledger 27). SPEC-05 open question 18 (ledger 28) covers the millisecond `SavedUtc`, the dropped-autosave result, the queue capacity and where step 17 runs.
- The composition now disposes `SaveService` (row 6).
- Tests: `SavePipelineTests` (11), with `FakeSaveContext` and `FakeParticipant`/`FakeDeferredBlock`:
  - chunk order META, IDPL, TSTA, TSTB, TSTC, with `meta.json` equal to META except `WorldBytes`;
  - world.bak byte-for-byte;
  - a capture failure and a write failure leave every file byte-identical with no `.tmp`;
  - deferred blocks disposed on success, encode failure and blocked paths;
  - a blocked manual save is reported and a blocked autosave is silent;
  - the queue rules, with FIFO order checked;
  - thumbnails written, deleted, cached and not rewritten when unchanged.
- EditMode 283/283 (+1 explicit), PlayMode 16/16.

### 2026-10-11 — FND-014 static state audit and Play Mode re-entry (claude, claude-opus-5-5)
- `StaticStateAuditTests` checks every static field in the runtime `Game.*` assemblies.
  - A field passes if its type has a SubsystemRegistration reset, if it is a readonly immutable type (unmanaged structs included), or if it carries `[StaticStateOk]`.
  - Static auto-property backing fields are audited too. A self-test proves that `static int x` and a static auto-property are caught.
- The first run found 18 fields:
  - `PerfMarkers` now disposes its recorders in SubsystemRegistration; they would otherwise have kept recording across Play sessions;
  - the rest (slots reset by `Services`/`EventHub`/`Bootstrap`, regexes, encodings, constant tables, a stateless comparer, test hooks) carry a `[StaticStateOk]` reason.
  - SPEC-01 open question 10, ledger 26.
- `PlayModeReentryTests` (EditMode): Boot → MainMenu, exit, enter again. Services and event-channel counts match, there is exactly one GameRoot, and nothing is pending. Enter-to-menu takes **13 ms** median (gate ≤ 1 s; the scenes are still empty).
- `EventHub.ChannelCount` added (internal). EditMode 272/272 (+1 explicit), PlayMode 16/16.

### 2026-10-11 — FND-007 SimClock lifecycle and SimClockAdvanceSystem (claude, claude-opus-5-5)
- `SimClockAdvanceSystem`: a Burst `ISystem`, `OrderLast` in `GameplayPostPhysicsGroup` after the GameplayEnd ECB. It requires `GameplayActive` and `SimClock`, and recomputes `UniversalTime` from the tick.
- `SimClockWriteback` (order 0) writes back only when the clock sits with `WorldSceneActive`, so the Lab never writes. It takes a setter, so FND-009 passes `t => session.UniversalTime = t`. SimClock creation at World and Lab entry came with FND-006.
- Tests:
  - EditMode `SimClockTests` (4): 120 ticks give exactly start + 2.0, and 10 minutes give no drift; pre- and post-physics systems read N during tick N; no advance on zero-delta frames or without `GameplayActive`; Lab vs World write-back.
  - PlayMode: in the real Default World the clock runs in the World, stops under a pause token and resumes. The test waits on conditions, because headless frames can be about 1 ms.
- EditMode 268/268 (+1 explicit), PlayMode 16/16.

### 2026-10-11 — FND-006 SceneFlow transitions and teardown (claude, claude-opus-5-5)
- `SceneFlow` per SPEC-01 §5.4:
  - serialized transitions (a second request answers `Busy` at once);
  - a pause token for the transition;
  - `SceneTransitionStarted`/`Finished` events;
  - exiting hooks, then teardown: destroy `GameplayActive`, then every entity outside the boot snapshot in one batch, then up to 10 cleanup frames;
  - World/Lab entry entities; ready hooks with a total unscaled timeout;
  - recovery to the main menu with `ErrorRecovery` and `success: false`;
  - `StartupAsync`, the hook registries and `SnapshotBootEntities`.
- `GameSceneNames` added. The composition gains rows 7 (flow) and 15 (snapshot); `GameRoot.Start` runs `StartupAsync`.
- **Bug found by the round-trip test:** Entities creates its `WorldTime` singleton on the first `World.SetTime`, after the boot snapshot, so every teardown destroyed the world clock. The snapshot now forces it first. Logged as SPEC-01 open question 9 (ledger 25) and trap T2.12, with T2.11 for the CORE-012/009 lessons.
- Not wired yet, until their types exist: session calls (FND-009), autosave (PERS-009), root screens and notifications (HUD-002, UI-003). Non-game scenes are left alone at startup.
- Tests: PlayMode `SceneFlowTests` (6):
  - MainMenu → World → MainMenu twice, with no leftovers, including prefab and disabled entities;
  - the Lab has `GameplayActive` without `WorldSceneActive`;
  - `Busy`;
  - a throwing hook and a 1 s timeout both recover to the main menu;
  - teardown of 200,000 entities: **3.5 ms median** (gate ≤ 50 ms).
- EditMode 264/264 (+1 explicit), PlayMode 15/15.

### 2026-10-11 — FND-005 composition root and BootSingletons (claude, claude-opus-5-5)
- `GameComposition.Build` runs §5.2 rows 1 (settings), 2 (content, content singletons, `BootSingletons.CreateAll`, `config_game`; no voxel lines yet), 3 (input), 5 (pause), 6 (save) and 14 (applier).
  - Rows 7, 8, 15 and 16 are comments naming FND-006, FND-009 and DEV-003; their types don't exist yet.
  - It records a step log, and disposal order on dispose. Test hooks inject a failing step and keep Play Mode running in tests.
  - `SaveService` joins `DisposeStack` in PERS-005: its `Dispose` throws until then.
- `GameRootTestHooks` (`Game.App.Testing`, SPEC-71 shape, Editor and dev builds) redirect the save and settings paths in `GameRoot.Awake`.
- `BootSingletons.CreateAll` tags the content singletons with `BootSingleton` and has a `Create<T>` helper for the factory rows.
- `config_game.asset` is added to the database.
  - `GameConfigDefinition.Validate` reports `FND-E002` ranges, and `FND-E001` as a warning.
  - The new `GameConfigValidator` enforces `FND-E003` and raises `FND-E001` to an error once ship blueprints exist (SPEC-01 open question 8, ledger 24).
- Tests:
  - PlayMode `CompositionTests` (4): order and reverse disposal; a throwing step disposes the earlier services and logs one error; boot singletons are tagged; with the hooks set, nothing is written under `persistentDataPath`, and the settings and a profile land in the temp folder.
  - `GameConfigValidatorTests` (3).
- EditMode 264/264 (+1 explicit), PlayMode 9/9.

### 2026-10-11 — PERS-004 ID palette and SaveWriter/SaveReader integration (claude, claude-opus-5-5)
- `SaveIdPalette` per SPEC-05 §4.3:
  - indices follow first use; `None` ↔ 0xFFFF;
  - `SeededFrom` keeps the loaded prefix and leaves the source untouched;
  - `Parse` throws `SaveFormatException` on truncation, more than 65,535 entries, an entry over 256 bytes or trailing bytes;
  - the writer refuses the 65,536th distinct id and ids over 256 bytes, so it never writes what it can't read back.
- **§4.10 check:** SPEC-02's `SaveWriter`/`SaveReader` have every capability §4.10 assumes, so **no fallback applies**. The only name difference is `WriteByteArray(ReadOnlySpan<byte>)` in place of `byte[]`, and the reader also takes a `byte[]`.
- `SaveIdPaletteTests` (7) pass, including a round trip through `SaveWriter.WriteContentId` → `SaveReader.ReadContentId` (u16 LE indices).

### 2026-10-11 — PERS-003 binary container, CRC32 and compression (claude, claude-opus-5-5)
- Implemented:
  - `Crc32`: IEEE, slice-by-8, plus `Append`;
  - `SaveContainerWriter`: format 1. It deflates only when the payload is ≥ `minCompressBytes` and the result is shorter, and output is deterministic;
  - `SaveContainerReader`: §5.5 steps 1–7. It never throws; a bad chunk is marked corrupt with `PERS-E011`. An uncompressed chunk whose raw length ≠ stored length is treated as a corrupt chunk;
  - `SaveFileComparer`: skips volatile header bytes, the META payload, and the META entry's flags as well as its lengths and CRC, since a different META can cross the deflate threshold.
- `ListSlots` META fallback wired. A new internal `SaveContainerReader.TryReadChunkFromPrefix` checks the header and table CRC on the first 64 KB and extracts `META`; a full `Read` would reject a prefix on the total-length check.
- Tests:
  - known vectors;
  - round trips for 0 B, 63 B, 64 B, incompressible and 10 MB payloads, with the deflate flag set only when smaller;
  - deterministic bytes;
  - every corruption class, including unknown header and chunk flags behind a valid CRC;
  - comparer first-difference reporting;
  - META read from a 200 KB file;
  - fuzz: 1,000 single-byte flips and 200 truncations never throw and are never reported `Ok`.
- EditMode 254/254 (+1 explicit).

### 2026-10-11 — FND-002 Bootstrap, GameRoot and explicit World creation (claude, claude-opus-5-5)
- Boot sequence:
  - `Bootstrap` (`BeforeSceneLoad`) creates the `Default World` explicitly, then instantiates `Resources/GameRoot` as `DontDestroyOnLoad`.
  - `SubsystemRegistration` resets `GameRoot.Instance`.
  - `GameRoot` builds the composition in `Awake`, ticks the settings debounce, flushes on quit, and in `OnDestroy` disposes the composition, resets `EventHub`, removes and disposes the World, and clears `Instance`.
- `GameComposition.Build` runs §5.2 rows 1 (settings), 3 (input, INP-002), 5 (pause) and 14 (applier). On failure it disposes in reverse, logs, rethrows and stops Play Mode in the Editor. Later rows are comments that name their tasks. Row 14 also constructs the `SettingsApplier` instance (SPEC-01 open question 7, ledger 23).
- `GameRoot.prefab` has children `UI` (an empty `UIDocument` until UI-003), `AudioRoot` and `CameraRoot`, and is wired to `ContentDatabase.asset`. `ProjectSetup` adds and checks `UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_RUNTIME_WORLD` for Standalone; the define is now applied.
- Tests:
  - PlayMode `BootTests`: one GameRoot and one Default World; a second boot is a no-op; destroying GameRoot disposes the World and resets services.
  - EditMode `BootPlayModeExitTests`: a real enter/exit of Play Mode disposes the World and clears `Instance`.
  - The guard fails without the define.
  - No `settings.json` was written by the test runs. EditMode 243/243 (+1 explicit), PlayMode 5/5.

### 2026-10-11 — TST-001 test kit core (claude, claude-opus-5-5)
- New `Game.Tests.Common` assembly, referenced by both test assemblies. It holds:
  - `EcsTestWorld`, with its own counted rate manager: exact `StepTicks` and capped catch-up `StepFrame`. It auto-advances `SimClock` and adds groups, ECB systems and ordering anchors, so reduced worlds log no warnings.
  - `GameTestBase`, with tracked worlds and leak detection;
  - `PerfStat`, `PerfSettings`, `PerfRun` (JSON records in `Logs/perf/`, plus `PerfRun.Gate` for loops a test times itself);
  - `Game.Core.Diagnostics.PerfMarkers`, which discovers `Type.Step` markers.
- `tools/test.sh perf` runs EditMode and PlayMode `Performance` tests, then the new `tools/perf_summary.py` (PASS/FAIL table, exit 1 on a failing record). All 8 existing gates moved onto `PerfRun` and all pass.
- Deviations (SPEC-71 open questions 4–6, ledger 20–22):
  - a fresh Unity catch-up manager plus float 1/60 gives 2 ticks for 0.05 s;
  - EditMode never ends a profiler frame, so `RunEcs` reads `CurrentValue` deltas;
  - the micro-gates record without the reference-settings check, which would leave them unenforced in the Editor.
- Measured: `EcsTestWorld.Create`+`Dispose` within 20 ms and `StepTicks(1)` within 0.1 ms (both tested). EditMode 241/241 (+1 explicit leak guard, which fails as designed), PlayMode 3/3.

### 2026-10-11 — CORE-009 ECS→managed event bridge (claude, claude-opus-5-5)
- `ManagedEventDrainSystem` per SPEC-02 §5.6. It registers channels idempotently, one named singleton queue each. Each frame it completes the producers, sorts the batch by (Tick, SortKey, payload hash), publishes, then runs `EventHub.DrainAll`. `OnDestroy` disposes every queue.
- **Spec defect fixed (SPEC-02 open question 13, ledger 19):** the payload hash covered padding bytes, which are undefined, so equal-key events sorted differently between runs. The 20-run test caught it. The hash now covers field bytes only.
- **Test scope (open question 12, ledger 18):** `ManagedEventChannelsTests` skips the managed-only `SessionReset`. No ECS-produced payload exists yet, so the test checks none until SPEC-13/30 add theirs.
- Tests: a parallel Burst producer on 8 workers (1,000 events) delivers sorted and identical over 20 runs. Two producers on one queue raise no safety errors. World disposal leaks nothing (`CheckForLeaks`). Registering twice creates one singleton. The transfer publishes before `DrainAll`.
- Measured: idle drain 0.0001 ms median, 0 B GC (gate ≤ 0.02 ms). Transfer of 1,000 envelopes 0.11 ms median in PlayMode (gate ≤ 0.5 ms). EditMode 224/224, PlayMode 3/3.

### 2026-10-11 — CORE-012 simulation event channels (claude, claude-opus-5-5)
- `SimEventChannel<T>` (one Persistent data block behind a pointer), `SimEventReadSet`, `SimEventGather` per SPEC-02 §5.13. Stale registrations are disposed at the `BeginTick` one full tick after they were written, with one warning per channel; their disposal handles join `LastConsumer`.
- **Spec defect fixed (SPEC-02 open question 11, ledger 17):** the specified `NativeArray<NativeStream.Reader>` is a nested native container, and every job rejects it. The read set now hands out readers through `Reader(i)`, and the gather chains one Burst job per registration. Consumers in SPEC-13/22 already go through `SimEventGather`, so nothing else changes. Each event type's owner adds `RegisterGenericJobType(typeof(SimEventGather.Job<TEvent>))`.
- Tests: two parallel `IJobChunk` producers on 8 workers (3,000 + 2,000 events over many chunks) gather in registration-then-index order, identical over 20 runs. Late registrations arrive in the next tick. Stale streams leave no leak by `CheckForLeaks`; the test fails if the dispose is removed. Misuse throws. The main-thread inbox comes first. `GuardedHandles` with one writer and two readers raises no safety errors.
- Measured: `BeginWrite`+`EndWrite` and `BeginRead`+`EndRead` each 0.9 µs median, 0 B GC (gate ≤ 5 µs). EditMode 215/215.

### 2026-10-11 — FND-004 settings service (claude, claude-opus-5-5)
- `SettingsService` loads and saves `settings.json` under `persistentDataPath` with an atomic temp-file write and a 0.5 s save debounce. A corrupt file falls back to defaults, is renamed to `settings.broken.json` and logs a Settings warning. `Apply` publishes `SettingsChanged` only for the sections that changed.
- `SettingsApplier` applies video (skipped in batch mode), audio volumes and input settings. Consumers that don't exist yet (audio mixer, camera, UI scale) are reached through `Services.TryGet` and skipped when absent; comments mark where PERS-009, UI and camera tasks hook in.
- `SettingsServiceTests` (4) pass; EditMode 208/208.

### 2026-10-11 — DATA-009 placeholder content and database (claude, claude-opus-5-5)
- `Assets/Game/Content/` area folders (empty ones keep a `.gitkeep`; Unity ignores dotfiles), `ContentDatabase.asset`, `block_missing` (material layer 63) and `item_missing`, with the SPEC-04 §4.10 values.
- `ProjectContentTests` (2) pass; the shipped-content performance test now runs on the real database. EditMode 204/204. Unblocks the ART-007 validator chain, MIG-004 and VOX-010.

### 2026-10-11 — DATA-008 editor content tooling (claude, claude-opus-5-5)
- Database builder, postprocessor, validator runner (DATA-E010/E011/W012, extension validators ordered by `Order`, a throwing validator becomes DATA-E099), CLI with exit codes, build gate, the Game/Content menu, the mirror-partner creator, and drawers for orientation and face masks.
- `ContentValidatorRunnerTests` (7) pass. The real `ContentDatabase.asset` and its placeholders arrive with DATA-009.

### 2026-10-11 — DATA-007 ContentService (claude, claude-opus-5-5)
- `ContentService` per SPEC-04 §5.1–§5.2. The runtime validation pass uses the new internal `ContentValidationContext` constructor. Blobs are disposed if construction fails after allocating them. A world gets at most one `ContentSingletons` entity.
- Measured: 2,000 definitions with 500 blocks construct in a median 8.2 ms (gate ≤ 100 ms), with a block blob of about 90 KB (gate ≤ 256 KB). No content database ships yet (DATA-009), so the shipped-content test runs on a synthetic 150-definition set: 0.5 ms (gate ≤ 20 ms). It switches to the real asset automatically once that exists.
- 6 tests pass; EditMode 195/195.

### 2026-10-11 — DATA-006 block blob and placement maths (claude, claude-opus-5-5)
- `BlockDefsBlobBuilder` and `BlockModulesBlob` per SPEC-04 §5.5; `BlockPlacementMath` per §5.6. The hash ignores prop meshes and changes with any hashed field or module row.
- `MirrorPlace` is exact for the chiral 4-cell pair, the 2×2×1 capacitor and the 10-cell thruster cross, over 24 orientations × 3 axes × planes {0, 1, 7, −3}; mirroring twice returns the original block, orientation and origin.
- 11 tests pass; EditMode 189/189. Unblocks DATA-007, VOX-006 and MIG-007.

### 2026-10-11 — DATA-005 ContentCatalog (claude, claude-opus-5-5)
- `ContentCatalog` per SPEC-04 §5.4 and §4.9. Indices are 1-based and ordinal per family whatever the input order; lookups by ID and by hash allocate 0 B; the identity hash does not depend on input order.
- Error paths in a runtime pass read `content:<id>`; DATA-008's editor pass supplies asset paths. `UnresolvedContentId` gains a constructor (its readonly fields need one).
- 9 tests pass; EditMode 178/178. Unblocks DATA-006, DATA-008 and MIG-004.

### 2026-10-11 — DATA-003 orientation tables (claude, claude-opus-5-5)
- The 24-orientation group per SPEC-04 §5.3, managed and as a blob. Every test vector matches (identity and yaw, AxisTurn, Inverse, Mirror on X, Y and Z, Compose, FaceTable, orientations 1/5/16/20). Composition is closed and associative over all 24³ triples, and the blob equals the managed tables.
- Measured: generation takes a median 0.083 ms (gate ≤ 1 ms). EditMode 169/169.

### 2026-10-11 — CORE-007 binary save primitives (claude, claude-opus-5-5)
- `SaveWriter`, `SaveReader` and `SaveKey` per SPEC-02 §5.7. Every encoding example matches byte for byte. A fuzz test with 10,000 seeded truncations and bit flips raises only `SaveFormatException` (or succeeds).
- Measured: 1 MiB of mixed primitives in a median 2.29 ms (gate ≤ 3 ms) in the Mono editor. The first version took 3.00 ms; single-pass varints and an ASCII fast path for strings brought it down.
- 22 tests pass; EditMode 155/155.

### 2026-10-11 — CORE-003 random streams and fixed-step time (claude, claude-opus-5-5)
- `RngStreams` and `SimTime.SecondsToTicks` per SPEC-02 §5.2; all five frozen `DeriveSeed` vectors match; `ForEntityTick` agrees between Burst and managed code.
- 6 tests pass; EditMode 133/133.

### 2026-10-11 — CORE-008 system groups and pipeline order (claude, claude-opus-5-5)
- The group declarations from CORE-001 already match SPEC-02 §5.8. `SystemGroupOrderTests` (9) cover every criterion on the full default world (Unity Physics and Entities Graphics included), plus the `GameplayActive` requirement and same-tick ECB playback on a reduced world.
- Note for test writers: the ECB singleton sits on the system's entity, so queries need `EntityQueryOptions.IncludeSystems`. A reduced world needs a `PhysicsSystemGroup` for the pre- and post-physics groups to sort correctly.
- EditMode 127/127.

### 2026-10-11 — CORE-005 Services (claude, claude-opus-5-5)
- `Services` per SPEC-02 §5.3. 7 tests pass, including a 0 B `TryGet` and a reset that clears types registered again after an earlier reset. EditMode 118/118.

### 2026-10-11 — INP-002 InputService contexts (claude, claude-opus-5-5)
- `InputService` per SPEC-03 §5.1: contexts, display strings with a cache, sensitivities, and a `Dispose` that works in edit mode. `ApplyBindingOverrides` and `SaveBindingOverridesJson` came forward from INP-003 because the constructor needs them; rebinding and reset remain INP-003.
- `ConventionsTests` enforces decision 1. `ProjectSetup` creates and registers `InputSystem.inputsettings.asset` with `shortcutKeysConsumeInput = true` (ledger question 12 resolved).
- Found: `EditorBuildSettings` still names a project-wide actions asset from the template that no longer exists; harmless and left as is.
- 5 tests pass; EditMode 111/111.

### 2026-10-11 — FND-003 PauseService (claude, claude-opus-5-5)
- `PauseService` in `Game.App.Services` per SPEC-01 §5.6; `ConventionsTests` enforces decision 10, so `Time.timeScale` is assigned only in `PauseService.cs`.
- 4 tests pass; EditMode 106/106. Ledger question 13 resolved.

### 2026-10-11 — CORE-006 EventBus and EventHub (claude, claude-opus-5-5)
- Implemented per SPEC-02 §5.5. Every §7 row about handlers, cascades and re-entrancy is covered by a test.
- Measured: publishing and draining 1,000 events with 3 handlers takes a median 0.025 ms (gate ≤ 0.3 ms) and allocates 0 B after warm-up.
- Unblocks FND-003, FND-004 and INP-002 (ledger question 13) and CORE-009. EditMode 102/102.

### 2026-10-11 — CORE-002 StableHash and content identity (claude, claude-opus-5-5)
- `StableHash` on Unity xxHash3 per SPEC-02 §5.1; every frozen string, sanity-buffer (both seeds) and `Combine` vector matches, and a Burst job agrees with managed code.
- SPEC-02 §5.1 labelled two vector strings wrongly ("97-char", "20 bytes"); they are 95 characters and 19 UTF-8 bytes, and the frozen hashes match those strings. The labels are corrected.
- Measured: 0.046 µs per `Hash64` of a 16-character id (gate ≤ 0.3 µs), 0 B GC. 14 tests; EditMode 89/89.

### 2026-10-11 — ADR-0019: system reviews instead of per-task reviews (frank, human; recorded by claude, claude-opus-5-5)
- **Owner, verbatim:** "bro you are the frontier!", then "also remove hardcoded blockers requiring review for every small implementation. our first buildout i would rather we go wide and then have an in-depth review when systems are built, because reviewing one item at a time misses the larger picture of those components working together holistically, and also just slows us down"
- [ADR-0019](decisions/ADR-0019-system-reviews-instead-of-per-task-reviews.md) accepted. Tasks of either tier are `done` at their Definition of Done, and dependents start at once. Each milestone gets an in-depth system review before its exit gate ([04-workflow §8](04-workflow.md)), logged as `system-review: name (model)`.
- Updated: 04-workflow §4, §7 and the new §8; AGENTS.md §2; ROADMAP; HANDOFF; the ledger header; `tools/check-docs.py` no longer requires `reviewed-by:`.
- `ART-003`, `ART-005` and `ART-012` were waiting only for a review and are now `done`. `ART-004` stays in `review` for its open 150 m glyph criterion.

### 2026-10-11 — AUD-001 audio contracts (claude, claude-opus-5-5)
- Stream types (`SfxEvent` is 32 bytes, `SfxEventQueueCreateSystem`, `CombatAudioCues`, `SfxEvents`), Core audio types, both audio definitions with validation, the Presentation types, a no-op `AudioService` and an empty `AudioRoot`.
- SPEC-64 open question 12 (ledger 16): the audio validation codes.
- 6 tests pass (`AudioContractTests`, `SfxEventQueueTests`, `AudioDefinitionValidationTests`). `VoxelContractTests` now allows `SfxEventQueue` among the module singletons that hold containers. EditMode 75/75.

### 2026-10-11 — PERS-002 profiles and slots (claude, claude-opus-5-5)
- `SavePaths`, `NewtonsoftSaveJsonCodec`, and the profile and slot part of `SaveService`: profile files are written to a temporary file and then replaced; `ListSlots` never throws and sorts newest first.
- `MetaMissing` slots read their `META` chunk through `SaveContainerReader`, which arrives with PERS-003; until then their `Meta` is null.
- SPEC-05 open question 17 (ledger 15): a valid `meta.json` without `world.sav` is not `Ok`.
- 6 tests pass; EditMode 69/69.

### 2026-10-11 — MIG-002 legacy JSON index and strict reading (claude, claude-opus-5-5)
- `LegacyContentIndex` (ordinal order, `$path`/`$type` required, duplicate `$path` reported), `LegacyJson` (`MissingMemberHandling.Error`, metadata read as ordinary properties, no date parsing, a new settings object per call) and `LegacyRefConverter`. Every MIG-E002 names the file and the JSON path.
- Real export: 155 files indexed with no errors; every file of the 26 DTO `$type`s reads strictly with the MIG-001 DTOs, unchanged; every `Assets/Resources/` content reference resolves.
- `LegacyJsonTests` (4) pass; EditMode 63/63.

### 2026-10-11 — MIG-003 legacy ID map (claude, claude-opus-5-5)
- `LegacyIdMap.Default`: 145 rows from SPEC-72 §4.4, with audio cues from SPEC-64 §6.4. Alias rows (`1`, `500`, `satelite_1`, …) carry a Note that starts with "alias of".
- `LegacyIdMapTests` (3) pass: over 120 keys in the legacy export resolve (the blank `entityId`, `encounterId` and `systemId` values in the export are skipped); every v2 ID is valid and unique apart from aliases. EditMode 59/59.

### 2026-10-11 — DEV-002 harness docs (claude, claude-opus-5-5)
- `GameTestHarness.cs` agrees with SPEC-70 §6.1; only the defaults of `gh_get`, `gh_invoke` and `gh_find` were missing from the table.
- `tools/playtest/README.md` gives each of the 15 commands its own row with all its arguments and defaults, and gains a *Game commands* section for later tasks to append to.
- `HarnessCommandTests.EveryCliCommand_IsDocumentedInReadme` passes, and fails when an argument is removed from the README (checked); EditMode 56/56.

### 2026-10-11 — DEV-001 dev runtime contracts (claude, claude-opus-5-5)
- 13 files in `Assets/Game/App/DevRuntime/`, all inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. The `DevCommandResult` factories are real: `Text` is never null, and `Pending` requires an operation id of at least 1.
- `DevContext` leaves out `Input`, `Ui`, `Logs` and `Events` until DEV-003/004 create those types; `DevRuntimeHost` is a shell with `Create`.
- Checked: development and release player script compilation both succeed (63 and 61 assemblies); the recipe is in [08-tools.md §3](08-tools.md). `DevContractTests` pass; EditMode 55/55.

### 2026-10-11 — MIG-001 legacy migration contracts (claude, claude-opus-5-5)
- 49 files in `Game.Editor`: the `Game.Editor.Migration` contract types (§4.2, §4.7) and 30 DTOs in `Game.Editor.Migration.Dto` (§4.3). The `LegacyRef` converter is a stub for MIG-002.
- SPEC-72 §4.3 now lists the `GalaxyData` and `OpenWorldGalaxyConfig` properties (read from the legacy files) and the C# naming: the item's `Name` is `ItemName`, and inline objects are nested classes.
- `LegacyDtoShapeTests` reads Newtonsoft's attribute by reflection, because the test assembly does not reference Newtonsoft. EditMode 54/54.

### 2026-10-11 — PERS-001 persistence contract types (claude, claude-opus-5-5)
- 40 files: `Game.Persistence` Data/Container/Services types and the `Game.Voxel.Data` VOXS types of SPEC-05 §4.5–§4.7; stubs name PERS-002…PERS-009. `Game.Persistence` still references only Core, Data and packages.
- `SaveChunkRegistry` implemented: `Default` holds the ten participant rows of §4.8 (META and IDPL stay with the container); the constructor rejects bad or reserved keys, duplicates and orders that are not positive multiples of 10.
- SPEC-05 open question 16: the readonly result structs get all-field constructors.
- `SaveChunkRegistryTests` (2) pass; EditMode 53/53.
- VOX-009 can now add the VOXS adapter members deferred by SPEC-10 open question 14.

### 2026-10-11 — DATA-004 ID rules and definition validation (claude, claude-opus-5-5)
- `ContentIdRules`, `ItemDefinition.Validate` (E020–E024, W023; placeholders `block_missing`/`item_missing` need no icon), `BlockDefinition.Validate` (E030–E045), `TryGetModule`, `ComputeMirrorOffset`, and `Validate` overrides with the E041 ranges for the seven v1 modules.
- Also implemented: DATA-E048 (null or duplicate module) and DATA-E049 (category against kind). They are per-definition rules in §5.8 that no task named, and the block validator has to handle null modules anyway.
- `TestContentFactory` (in-memory definitions through `SerializedObject`) is ready for VOX-006 and later tasks.
- `DefinitionValidationTests` (7) pass; EditMode 50/50.
- Found: `BlockShape` in code still has only `Cube` and `Wedge`; ADR-0018 changed the spec but not the code. Applied in the next commit.

### 2026-10-11 — INP-001 input asset and contracts (claude, claude-opus-5-5)
- `Controls.inputactions` holds Global (5), Flight (27), Menu (5), Dialog (10) and Editor (51 actions) with the bindings of SPEC-03 §4.2; `Controls.cs` is generated by the Input System importer (version 1.20.0) and committed. `InputService` is a stub (INP-002/003); `RebindStatus` and `RebindResult` are complete.
- `InputAssetTests` (3) pass; EditMode 43/43. Shared Editor controls (`<Mouse>/delta` for Orbit/Pan, the wheel for ToolWheel/Zoom) are allowed by the spec and listed in the duplicate test.
- Headless generation note: the importer writes `Controls.cs` only when the scripts that reference `Controls` are absent from that import, so it was generated in a first pass with them moved aside.
- New SPEC-03 open question 4: `Controls.Dispose()` errors in edit mode; default for INP-002 recorded.
- Not done here: `InputSystem.settings.shortcutKeysConsumeInput` in `ProjectSetup` (no task names it yet).

### 2026-10-11 — CORE-011 settings model (claude, claude-opus-5-5)
- `GameSettings` and its five sections implemented per SPEC-02 §4.9/§5.11, with an internal `SettingsClamp` helper. Invalid `FullScreenMode` values reset to the default; `TargetFrameRate` other than -1 clamps to [30, 500]; a negative `QualityLevel` becomes -1.
- `GameSettingsTests` (7) pass; EditMode 40/40.

### 2026-10-11 — CORE-010 math helpers (claude, claude-opus-5-5)
- `MathD` and `Planar` implemented with the exact formulas of SPEC-02 §4.10/§5.10.
- `PlanarTests` (4) and `MathDTests` (2) pass, including the 1 mm round trip at origins up to 7.7e10 m; EditMode 33/33.

### 2026-10-11 — FND-001 boot and flow contract types (claude, claude-opus-5-5)
- SPEC-01 §4 types as listed in the task: `WorldSceneActive`, `StaticStateOkAttribute`, `GameConfigDefinition` (fields, ranges and defaults of §4.2; `Validate` is FND-005), `GameRootConfig` (`ContentDatabase`, `UiDocument`; `UiConfig` and `CameraConfig` come with SPEC-60/62), `SceneFlowState`, `SandboxArgs`, `ShipSpawnArgs` (no `Allegiance` until FND-012), shells of `GameRoot`, `Bootstrap`, `GameComposition`, `BootSingletons`, `BootSingleton`.
- Architecture §4.3 (`WorldSceneActive` only in the World, SPEC-01 decision 6), §5 tree (`PersistentIdRefillSystem`, `SimClockAdvanceSystem`), §6.1 and §3 table.
- `FoundationContractTests` (2) pass; EditMode 27/27; no compile warnings.

### 2026-10-11 — VOX-001 voxel contract types (claude, claude-opus-5-5)
- 68 files: every SPEC-10 §4 type in `Game.Voxel` (`Model/`, `Grid/`) and the two voxel definitions in `Game.Data`, including the ADR-0015 additions (`GridProfile`, `RootComponentRegistration`, `GridStowApi`). Stub bodies name the implementing task (VOX-004…VOX-020).
- `VoxelContractTests` (4 tests) pass; EditMode 25/25; compile has no warnings.
- Open questions: ledger 9 (`BrickRecord` is 28 bytes, not 32) and 10 (the VOXS adapter members wait for VOX-009).
- Unblocks `PERS-001` (with `VOX-001` done) and `FND-001`'s `GameConfigDefinition.startBlueprint`.

### 2026-10-11 — Tools reference and handoff page (claude, claude-opus-5-5)
- **Owner, verbatim:** "before we start M0, do we have somewhere documenting tool usage for this project? and also is there a "start here" type of file for new workers continuing work?", then "please fix all".
- New [08-tools.md](08-tools.md): every tool, the headless Unity recipes and traps (until now only in a worker's private notes), art and Blender tools, git rules.
- New [HANDOFF.md](HANDOFF.md): the owner's standing rules, where things stand, what's next. AGENTS.md links it first.
- AGENTS.md §2: the art rule now points to DESIGN-GUIDE.md, and "git stays local, never push" is a non-negotiable. README.md and docs/README.md no longer say the design system awaits approval.
- Not changed: the binding docs 01–03 still link DESIGN-SYSTEM.md for tokens, which stays valid because the guide adopts its §10–§11.

### 2026-10-11 — GATE: design system approved, NOCTURNE (frank, human; recorded by claude, claude-opus-5-5)
- **Owner, verbatim** (answering whether NOCTURNE plus DESIGN-GUIDE.md counts as the design-system approval):

  > 1. yes I approve
  > 2. lets model the different sloped blocks so we can see how many are needed (ideally as least as possible)

- `ART-013` is done and the *Owner gates* row is approved. DESIGN-GUIDE.md is BINDING. SPEC-80 §12 is filled in. The ROADMAP's design-first hold is lifted, so M0 may start and SPEC-81 (A1) is unblocked; `ART-014` is next on the art track.
- **Deviations from SPEC-80, by the owner's choice:**
  - One direction was chosen instead of two finalists (`ART-006`).
  - The in-engine look test (`ART-008`…`ART-011`) moves to after the renderer exists, as a check rather than a gate.
  - `ART-012` was replaced by the guide (in review).
  - DESIGN-SYSTEM.md keeps its direction sections in place as the proposal record, instead of moving them to Appendix A.
- **Next:** the slope study for open question 6 (point 2 above).

### 2026-10-11 — Slope kit chosen: cube, slope, hip corner, long slope 1×2 (owner; recorded by claude, claude-opus-5-5)
- **Owner, verbatim**, after the WRAITH was redesigned for options 1 and 2 (after asking "make both option 1 and option 2 look like good ships"):

  > option 2 is better

- **ADR-0018 is accepted.** `BlockShape` gains `HipCorner`, `LongSlopeHigh` and `LongSlopeLow`. The long slope is one placeable 1×2 block whose two cells store their own piece shape.
- **Spec changes:**
  - SPEC-04 §4.2: the enum and the geometry table.
  - SPEC-04 open question: per-cell shapes for multi-cell structural blocks, plus the two content rows.
  - SPEC-11 open question: a 3-bit shape field (full faces move to a table lookup) and 42 normals.
- DESIGN-GUIDE §5.1 and §13 are updated, and the look book's Slopes tab marks option 2 as chosen.
- Ledger question 6 (b) large blocks and (c) alien cells are still open.

### 2026-10-11 — Slope study: how few sloped blocks builders need (claude, claude-opus-5-5)
- **Owner, verbatim, during the study:**

  > dude 20 slopes is insane my boy I though you were going to propose like 3 or 4. also remember it doesn't have to be all 1x1s it can be 1x2s or 1x3s or 2x3

  > you're optimizing for visual parity with the existing ships which i don't care about, i wan something sensible so builders can build with

- **First pass, set aside.** It counted the shapes the look-dev hulls use per cell:
  - 3,129 distinct cut shapes;
  - snapped to a 1:1 grid, 40 shapes, of which 11 cover 99 % of cut cells;
  - on a 1:2 grid, 253 shapes.

  The owner rejected this: it optimises for parity with the concept ships, and it counts per-cell pieces rather than placeable blocks.
- **Builder kit study** (`ArtSource/LookDev/ld_slopes.py`, look book tab **Slopes**):
  - A block is one placeable block that rotates freely (the long slope is a single 1×2 block), and the cube is always included.
  - Each kit builds the same demo frigate on its own angles, using only its blocks.
  - "Cells off" is the share of cut cells, across every round-3 hull and the HALO, that the kit cannot build within 10 %.

  | Kit | Cells off |
  |---|---|
  | Today's spec (slope only) | 9.0 % |
  | Classic set (slope, three-face corner, inverted corner) | 8.4 % |
  | **Slope + hip corner** | **1.5 %** |
  | + long slope 1×2 | 3.3 % |
  | + long hip corner 2×2 | 2.3 % |

- **Finding:**
  - At 45° the corner where two slopes meet is the same block in every orientation, so two blocks close everything.
  - Every extra angle brings its own family of corners: a ridge corner, a nose corner, and one for meeting a 45° edge.
- Open question 6 is updated with the options; it waits for the owner.

### 2026-10-11 — Design guide: NOCTURNE as the source of truth (claude, claude-opus-5-5)
- **Owner decision (verbatim, 2026-10-11):**

  > to be honest im in love with the NOCTURNE version so we will use it as the main guide, however, we must keep the look book as it is, as a visual reference. and make a new Design system guideline to use as the source of truth. this is because i don't want to lose the drydock, or aliens, or the 2d ship ideas (which btw are not yet approved)

- **New `docs/design/DESIGN-GUIDE.md`**, the source of truth for the look. It describes NOCTURNE as rendered in the look book (industrial gothic horror), not the proposal's early glossy NOCTURNE.
  - Status labels: Binding, Guide, Reference, Not approved.
  - Colour, light and sky values are taken from `ld_styles.py`.
  - Hull language, plating, the round-4 ship design rules, factions, the alien rules and the UI (unchanged, per round 2).
  - VFX and damage are now physical (ember, scorch, smoke).
  - A reference library that keeps DRYDOCK, OPERA, the aliens, the silhouettes (not approved) and the HALO concept.
  - It adopts DESIGN-SYSTEM.md §4 and §10–§11 by reference, so specs citing them still hold.
- DESIGN-SYSTEM.md's header and SPEC-80 decision 2 now point to the guide. The look book and silhouette page are unchanged.
- `ART-006` (shortlist): the owner chose a single direction, NOCTURNE. Statuses are left as they are until open question 8 is answered.

### 2026-10-10 — Look book round 4, in progress: second ship lines, plated hulls, silhouettes for approval (claude, claude-opus-5-5)
- **Owner feedback (verbatim, 2026-10-10)**, five messages. The Starsector screenshots that came with them are reference only and are not stored (no Starsector art in the repo); the screenshots of our own renders are not stored again.

  > that alien behemoth looks so scary i love it. every single ship in both nocturne and drydock are all just a long rectangle with a taper in the front right now. can we make another ship line for each ( another frigate, cargo ship, cruiser, dreadnaught group), this time the ship should not adhere to be a. boring rectangle I want some actually interesting ships that give the player a sense of ownership of something unique. i've attached an image of ships from a 2d game called starsector, i do not want to copy blindly any designs but the point i want to make is do you see how all ships have a different feel and look. and normally what unifies a ship line is a similar build pattern and colors, in the case of starsector they have high tech ships, midline and low tech. this will require new textures, and perhaps new hull shapes as well. be creative, i don't want the same cookie cutter stuff. I like how with the aliens you branched out. lets do the same here (image 3) also look how within a ship the hull itself is so intricate which gives a sense of scale. we can achieve something of this caliber with texturing and the procedural hulls. lets focus all our efforts in nocturne and drydock specifically.

  > thats just another big rectangle bro

  > and this just another big rectangle

  > how about this. make 2d sihlouetes first and we'll approve the silhouttes then build the ships referencing those

  > again i invite you to look at the starsector ones. don't be so "impractical" with your siloouethes, for example the a, b, c are just so goofy, its supposed to be the future and you're making hammers and hooks?? when i said don't make a rectangle you overcorrected and started optimizing for the wrong thing. if you do a black and white of the starsector ships what makes them unique is not only their silouette which is very memorable but also their distinct color design between different ship lines and how a shipline group shares similar colors and patterns within itself. how about this, one thing that is notable in starsector is right tool for the job, each ship has its own niche where its really good at that job, so bigger doesn't necessarily mean the best, a group of 5 skilled frigates can EASILY down a cruiser class if used correctly. im not just looking for weird shapes, im looking for a form follows function and each ship tells a story. some of your options I do like, for example the crucible is cool, and the gyre, but everything else honestly quite goofy

  > ok and id like you to look at the starsector ships image once again, do you see how a lot of ships are not perfectly symetrical? theres always tiny (sometimes larger) asymetries in the designs.
  > keep the ones you've made on this round because they aren't bad, but make a new batch playing around with some asymetry, and some bulkier classes, some more slick, some hybrid ships. some ships that were meant for cargo but got re-specced for war so theres a cargo version and a modded war version.
  >
  > before you make the new batch, please do research on starsector because its the biggest inspo for this project. look at the factions, experimental ships, different functions of ships, redacted ships, weapons, etc. look at how many ships like the falcon have a pirate modded version that is faster but the hull is weaker, etc. like I'm looking for that level of depth from the silouettes and explanations of each of our ships. like what factions/civilizations, what level of tech, what is their niche, any mods, etc.

- **Plated hull textures** (`ArtSource/LookDev/ld_plating.py`). A procedural 6 m plating tile: recursive plate splits on a 1.5 m grid, bolts, grilles, hatches, stencil digits, pipes, vents and windows. It is box-projected in object space at the same human scale on every block, and paint is chosen per top-level panel so it reads as plates, not camo. Two liveries so far: `caisson` (DRYDOCK) and `knell` (NOCTURNE).
- **New hull pieces and blocks.** Hull pieces (`ld_ships.py`): `sweep`, `drum`, `bell`, `shell` (a hollow lathe) and `slab`. The convex-hull planes are now computed in float64 from the original points, which fixed a 1e-6 sliver gap. Kit 4 (`ld_kit4.py`): beacon, piston, claw, coil, toll, censer core, bell, candle and gargoyle. The build check gains back-mounted and side-mounted blocks, and small cells that overlap a large block count as attached.
- **First 3D drafts of DRYDOCK line B** passed the build check but were rejected as rectangles. **Process change (owner):** 2D plan-view silhouettes are approved before any 3D build.
- **Silhouettes v1** (novelty tool shapes) were rejected and are kept as `docs/design/lookbook/silhouettes-v1.html`.
- **Batch 2** (`silhouettes.html`, tab "Batch 2"): one role-driven line B per direction, 12 ships each, with a job and a weakness per ship. The DRYDOCK line is Caisson Heavy Industries (mining and salvage). The NOCTURNE line is the Knell choir, a militant mortuary order whose ships are laid out like cathedrals.
- **Starsector research** (the wiki's ship, faction, refit, hull-mod, weapon and hidden-faction pages, plus a study of the sprites in the browser; nothing stored). Findings are on the page:
  - tech tiers are philosophies, not upgrades;
  - factions reuse hulls with their own paint and mods;
  - civilian hulls get war conversions that cost cargo, hull and reliability;
  - pirate refits trade hull for speed, and military refits do the opposite;
  - asymmetry follows use;
  - every hull has a niche and a counter;
  - redacted ships break the rules;
  - every hull has a codex entry.
- **Batch 3** (`ArtSource/LookDev/sil_batch3.py`, tab "Batch 3"): the owner's own factions as Starsector-style ship lines. The lore comes from `Import/Legacy/content/Factions` and the colours from the DESIGN-SYSTEM faction liveries, with a DRYDOCK/NOCTURNE paint toggle.
  - **Khuchad Clan**, low tech and bulky: CORSAC, KULAN, ARGALI, ORDU. Pack-and-chief doctrine. Armour is on the port side (the shield arm) and the ram or gun is offset to starboard (the lance arm).
  - **Triple Alliance**, midline hybrids: AUDITOR, TALLY, ESCROW, CONSORTIUM. Standard spines with module bays and house colour bands.
  - **Vestari**, high tech and slick: EDICT, CRADLE, MANDATE, QUORUM. Each has one offset speaker's fin.
  - **Scavengers**: KING'S COURT, three wrecks welded together.
  - **[REDACTED]**: Vanguard WARDEN and SPINDLE, and the Silence.
  - **Refit bay**, cargo hulls re-specced for war:
    - PARCEL → (S) Jackrabbit and (I) Escort;
    - TALLY → (I) Q-ship and (S) Gunbarge;
    - WELLHEAD tanker → (K) Khuchad battle-carrier;
    - CRADLE → the Stolen CRADLE (S).

  Every card has a designation and maker, a story, its fit, ship system, built-ins, asymmetry, variants, what it is weak to, and six stat bars. Refit cards show stat deltas and +/− change lists.
- **Owner feedback (verbatim, 2026-10-10)** on batch 3:

  > i like this batch 3 a lot more.
  >
  > you know how one of the game's systems is ramming. and some ships are meant to be used as rammers. where is that here if it is at all?

  > ok and btw based on our lore these are the clans, but we are missing the human faction with the ARK tech and their ships. keep in mind humans are strangers in this galaxy

- **Batch 3 additions.**
  - **ARK expedition** (the humans, strangers in the galaxy): PETREL pathfinder, GARDENER fabricator tender, ICEBREAKER ram cruiser and HOLDFAST mobile drydock.
    - The lore comes from the legacy narrative: earlier Arks lost ahead of this one, and the Silence behind it.
    - The tier badge is "ARK tech". The hulls are Earth patterns printed by the ARK: crew-repairable, with adapter points for clan parts, red and green nav lights, and hazard stripes.
    - The refit bay has a new row: PETREL → Mission 12 (an earlier Ark's wreck, with Silence damage) → Gone native (Vestari emitter, Khuchad slab, Alliance module).
  - **Rammers section.**
    - Ram damage is collision impulse, so a rammer needs a mass-forward replaceable prow, a load-bearing spine, thrust plus reverse thrust, and nothing fragile at the bow.
    - New ships: TARPAN (Khuchad ram frigate) and TALLY (S) FIRESHIP (a one-way Scavenger refit), alongside the ICEBREAKER.
    - Every card now has a seventh stat, Ram.
  - **The rammers default was chosen without an answer** (option 1 of three offered: a dedicated rammers set).
- **Owner request (verbatim, 2026-10-11):**

  > make a new additional ship line for ark targetted specifically for war. these ships are just strong punchers and they work well together and are the main bulk of the ARK force.
  >
  > also make a silhouette for the ARK its a station size mothership exploration vessel.

- **Batch 3 additions, continued.**
  - **ARK war line** (palette `arkw`: graphite hulls, bone armour blocks, safety-yellow magazines, a red fire-team stripe): SQUALL strike frigate (spinal coilgun, flies in pairs), HALO fire-control destroyer (point defence for the team, marks the target), TEMPEST line cruiser (the bulk of the fleet, every gun forward, prow of standard armour blocks) and THUNDERHEAD battleship (four heavy triples, a flared armour mantlet in place of shields).
    - Teamwork is built in: a fire link lets every linked gun volley on the HALO's mark, and standard blocks let armour swap between hulls.
    - Shared build rule: heat to port, ammunition to starboard.
    - A fire-team plate shows the formation at true scale against a Khuchad ARGALI.
    - New card class: Destroyer (100 m scale).
  - **THE ARK**: the expedition mothership, 1,499 m long and 727 m wide, on a wide card turned bow-right.
    - From the bow: an ice-and-rock impact plug, the command block and dishes, two habitat drums (one holds the seed vault), a foundry fed by a captured asteroid to port, shipyard gantries to starboard, a hangar deck, then the reactor, radiators, propellant tanks and seven fusion engines.
    - A HOLDFAST, a GARDENER and an ICEBREAKER are docked at true scale.
    - The starboard radiator wing was cut down to print the first fleet; its bare frame remains.
- **Owner pick (verbatim, 2026-10-11):**

  > ok the first 3d block ship we'll build out is Batch 3: HALO (ARK war line)

- **HALO built in 3D** (`ArtSource/LookDev/ld_ark.py`). It follows the approved plan view: aft engine block, main hull, the waist under the bridge, forward point-defence sponsons and the bone armour prow.
  - Port side: a radiator deck with 16 fins, and the fire-control dish on a lattice boom. Starboard side: the flak magazine with its flak turret.
  - Weapons: 6 point-defence turrets and a twin coilgun turret. The red fire-team stripe runs across the hull.
  - It passes the build check: 108,345 cells, 32 modules, every block attached and every module on its mount face.
  - New `arkw` livery for DRYDOCK and NOCTURNE, and new `ark` plating (even 1.5 m blocks, bolts, stencilled pattern numbers).
  - New render stage `lookdev.py --stage r4`, which writes `img/r4/<direction>/halo_{hero,game,rear,plan,detail}.jpg` and adds the check results to `buildcheck.js` under `r4`.
  - The silhouette build now writes reference SVGs for built ships and marks their cards "Built in 3D".
  - Look book: new **ARK · round 4** tab, with the approved silhouette beside the 3D plan view and the shots in both worlds.
  - **Open:** turrets, the flak and the engines are kit blocks at twice their frigate size, as the cruisers already do with the triple battery. A destroyer needs its own kit sizes, or a rule for scaled blocks.
  - **Answered (owner, verbatim, 2026-10-11):**

    > its fine this is just visual concepts not necessarily the final look of the ships.

    The look-dev ships are concepts, not final hulls, so the block sizes are not a kit decision.
- **Next:** the owner marks silhouettes from either batch; the approved plan views are then built in 3D with the new pieces, plating and blocks, checked, rendered and added to the look book.

### 2026-10-10 — Look book round 3: connected ships, cruisers and dreadnoughts, capital weapons, defended stations, five aliens (claude, claude-opus-5-5)
- **Owner feedback (verbatim, 2026-10-10)** on the round-2 look book. The four screenshots that came with it are of our own renders and are not stored again:

  > BROTHER YOU COOKED WITH ALL of these wow.
  >
  > can you please revise the ships in all 3 versions, they all have blocks floating, wings not connected, cannons in the middle of nowhere. also how did you do the front taper in the nocturne ships, we can do that with blocks? if so then very impresive.
  >
  > regarding aliens, they look really good, can you give me 3 more variants that look unsettling and out of this world completely, one of them a behemoth, bigger than all of others, like a biological mothership .
  >
  > i really like the new 3d visual direction you took with nocturne its very warhammer 40k, but the colors can look a bit dull, can we intensify a little and im not just asking you to make it more saturated, im asking slightly lower lows, and slightly higher highs, like for example some more coloration on the planet so its got some slight differences, and the background not all green, like some purple on on the side or other colors, like i love how the opera background isn't just one color.
  >
  > for drydock and noctune make a new cruiser class and dreadnaught class, the size comparisons should be clear to give a sense of scale.
  >
  > for drydock rethink the station entirely  because it just looks very odd, i don't like the box look, should look like a busy space hub port with multiple connections and ships around. also in general space stations should have defenses, they are made of the same voxels because they will be attacked as well.
  >
  > regarding blocks, i want to say for weapons you've gone out of the 1x1 and they are looking amazing. but remember we can have bigger hulls like 3x3x3 etc. bigger ships will be better to build with larger hull pieces than always relying one 1x1s. especially when we get to dreadnaught levels. on that same dreadnaught note, make even larger more sophisticated weapons for the large ships, like the tiny railguns and turrets we have so far are good for smaller ships but these dreadnaughts and cruisers may have large 10xYxZ level weapons that take a lot of energy to use. and doesnt have to be a convetional BIG CANNON GO BOOM. like we can get creative with some of these and get weird like having mini black hole powered weapons. go crazy

- **Response: look book round 3** in `docs/design/lookbook/index.html`. Renders are in `img/r3/<direction>/`. Rounds 1 and 2 are in the "Earlier rounds" tab, and round 2's code is at `f3d4c7d`.
- **Build check (new `lookdev.py --stage check`).** Every ship and station is checked against the rules the game's builder will enforce:
  - every block joins through real shared faces (edge or corner contact does not count);
  - every module sits on its mount face (turrets and masts on their base, engines on their front);
  - each block's own geometry must reach its mount face.

  All 13 designs (ships and stations) pass. Results are in `docs/design/lookbook/buildcheck.js`.

  Fixes found by the check:
  - underside turrets were mounted right way up, so they hung in the air; they now hang upside down from their base;
  - the MULE's sponsons had a 0.5 m gap to the hull;
  - the WRAITH's tail fins and the CANTICLE's stern spires stood on chamfers;
  - the CANTICLE's railguns hung off the hull side and are now side-mounted on the wall;
  - the LARK's wing roots, cockpit and fins were not seated;
  - THE CROWN's spokes did not reach the hub;
  - the railgun, laser lance and harpoon floated 4 cm above their own base;
  - hairline loft slivers are pruned.

  Root-cause fix in the mesher: when two straight-sided pieces cut the same cell (e.g. ring segments meeting on a 45° seam), the cell now takes their exact convex union. The old rule kept only the larger piece, which left a sawtooth crack along diagonal seams.
- **Tapers are blocks, but not two shapes.** Every 0.5 m cell along a taper is one block cut by straight planes, and the same planes continue across neighbouring cells, which is why the edges line up. The hulls use many distinct cut shapes (counted up to rotation and mirroring):
  - DRYDOCK: 880 distinct shapes in 17,524 cut cells; the spec's 45° Wedge covers 21% of them
  - NOCTURNE: 1,189 distinct in 23,090; Wedge 21%
  - OPERA: 407 distinct in 19,810; Wedge 8%

  The specs today allow only `Cube` and a 45° `Wedge` (SPEC-04 §4.2; SPEC-11 packs the shape in one bit). Recorded as open question 6 (owner).
- **Cruisers and dreadnoughts (DRYDOCK, NOCTURNE).** The scale ladder is light ~18 m, frigate ~50 m, cruiser ~115 m, dreadnought ~250 m, with line-up renders labelled with name, class and length.
  - Cruisers build their hull from 2×2×2 blocks (1 m) and dreadnoughts from 3×3×3 blocks (1.5 m), on a second, coarser layer.
  - A module placed into a big block splits it back into 0.5 m cells of exactly the same shape.
  - Big hulls get windows on 3 m deck rows and fewer small fittings, so the size reads as calm.
  - DRYDOCK: STEVEDORE (container ledges, two triple batteries, drone bay, graviton snare) and ANVIL (forked bow, spinal particle lance in a dorsal trench, singularity projector, three triple batteries, nova silos, drone bays, nine capital engines).
  - NOCTURNE: REQUIEM (nave with spires, lightning tower and organ-pipe silos at the stern, side-mounted railguns) and SEPULCHRE (castle with a forest of spires, flying buttresses, keel spikes, an organ of nova silos, lightning towers, singularity projector, triple batteries, sponsons with drone bays).
- **Capital kit (all three directions, each in its own style).** The kit is now 53 blocks.
  - Large hull: Hull 2×2×2, Hull 3×3×3, Slope 3×3×3, Corner 3×3×3, Long slope 3×6×3, Armour block 3×3×3, Truss 3×6×3.
  - Capital weapons: Triple battery 12×18×8, Spinal particle lance 12×80×12, Singularity projector 16×24×16, Storm coil 10×10×20, Graviton snare 14×12×12, Nova silo 6×6×14, Drone carrier bay 16×24×6.
  - Capital engine 8×12×8.
  - Dreadnoughts mount larger variants of the battery and singularity projector.
  - The look book gives each capital weapon a role and an energy cost (look-dev numbers, not balance).
- **PORT KESSLER replaces KESSLER YARD.** It is a trade hub:
  - a central spindle with a defended ledge and a traffic-control crown;
  - four 80 m docking piers with yellow gantries, container yards and guide lights;
  - berth heads with ships docked and traffic coming and going.
- **Station defences, built from the same blocks as the stations:**
  - PORT KESSLER: heavy cannons and flak on the ledge, a graviton snare on top, a triple battery and flak on every berth head, autocannons along the piers.
  - THE ROOD: heavy cannons between the spire rows, flak on deck and hanging under it.
  - THE CROWN: flak on the upper rim and under the lower rim, heavy cannons on the spokes.
- **Three new aliens:**
  - LAMPREY: eyeless; a funnel mouth with rings of teeth; seven gill pores per side.
  - CRINOID: five-fold symmetric sea lily; ten feathered arms; a ring of eyes; a trailing stalk.
  - GRAVID: the behemoth carrier, ~480 m, nearly twice the dreadnought. It has a banded brood body with egg-chamber pits, an armoured head, horns, sail spines, birthing orifices and trailing tendrils.

  The alien field is now sampled on a dense grid with per-primitive bounds and meshed by a vectorised surface-nets mesher. GRAVID is sampled at 2 m in the renders. Big aliens get larger carapace plates with veins kept thin.
- **NOCTURNE colour range.**
  - The image has more contrast, not more saturation: a darker ambient and background, a stronger sun and rim light, brighter windows, engines and strobes.
  - A violet nebula region and a low amber glow join the green smoke.
  - The storm planet gets rust, bruise-violet and teal bands, swirls, a dark polar hood and one great pale storm.
  - UI unchanged.
- **New shots:**
  - DRYDOCK and NOCTURNE: cruiser and dreadnought close-ups, the class line-up, the fleet, the superweapon fight against GRAVID, LAMPREY, CRINOID, GRAVID and the alien line-up.
  - PORT KESSLER with docked ships and traffic.
- **Next:** the owner reviews round 3, and decides open question 6 (how hull shapes become game blocks) before the voxel specs are implemented.

### 2026-10-10 — Look book round 2: polished DRYDOCK, NOCTURNE as industrial gothic horror, new OPERA, alien enemies (claude, claude-opus-5-5)
- **Owner decision and feedback (verbatim, 2026-10-10)** on the round-1 look book. The two reference screenshots that came with it are not stored in the repo (one is a page of copyrighted cover images):

  > really good exploration in all of them, here are the finalists:
  >
  > 3D:
  >
  > * DRYDOCK
  > * NOCTURNE
  > * REEF
  >
  > UI:
  >
  > * DRYDOCK
  > * NOCTURNE
  >
  > VISUAL STYLE:
  >
  > * DRYDOCK
  > * PULP HORIZON (I want to love this one so much and Im sure with some changes it would be in my top 2 favorites, but lets leave it for now and focus on the other finalists)
  >
  > 1 DRYDOCK - overall, the strongest most cohesive option, it could however borrow some things from other designs, like making the background more insteresting than just black empty space (nebulas, stars, background subtle color differences).  some stronger color definitions in the 3d scenes for blocks (too much light gray, dark gray) whereas it would really level up with visual changes, and other scene items like planet rings, proper planet atmosphere (the atmosphere right now looks like a shield on the planet lol). I like the industrial feeling drydock has in the 3d as well as the UI, lets find polishing in both areas. also give me crazier weapon variants, like big guns, lasers, etc get creative. also for the EXISTING blocks in drydock you need to give them all a polish pass as well, some of the blocks like the cockpit, core blocks, reactor, are either troubled in terms of clipping 3d or don't look as nice or they are borrowing too much from my original placeholders
  >
  > 2 NOCTURNE - great UI, 3d very similar to drydock (in fact looks like a different civilization within the same universe as drydock) how could we give it its own stronger unique style? I love the background, but still not experimental enough with it. 3d blocks are too boring as they are all the same color, and the black vents just look like pixel errors, for this next pass lets try going out of our comfort zone and giving Nocturne a very distinct horror sci-fi look like the ALIEN franchise movies. (no ui changes, its good)
  >
  > 3 REEF - wow the ships and blocks are very out of the conventional versions. this would be amazing to integrate into one of the others as an alien civilization. UI not good so don't focus on it. what i want you to explore is if we can have alien enemy ships like these in the worlds of drydock and nocturne, so if we find a way to get a similar look for the reef ships without needing to do per voxel smoothing, really cool green pattern over the ships, also try other coloring rather than gray. so rather than polishing this one, i want to see ships like this in drydock and nocturne.
  >
  > GENERAL ISSUE I SAW WITH SHIPS TO KEEP IN MIND FOR YOUR POLISH PASS (IMAGE INCLUDED):
  > can you see how a lot of ships, the corners are not aligned at all and it gives it this glitchy/ jagged look, i don't know if thats what you were going for but it looks like a mistake honestly. so keep this in mind because i saw it in all 6 versions.
  >
  > YOUR deliverables will be a POLISHED DRYDOCK and POLISHED NOCTURNE with all the feedback above as well as your own best judgements. and I want a 3rd NEW variant based on the look of SPACE OPERAS. as well as inspired by things like DUNE, FIFTH ELEMENT, ODDESSEY, NO MANS SKY

  Follow-up during the work (verbatim): "i never said to "smoothing hull edges" btw, i said lets see if we can have some enemy alien ships that look like the REEF ones". Human hulls stay blocky; only the alien enemies use a smooth shell.
- **Response: look book round 2** in the same page, `docs/design/lookbook/index.html`. Tabs: DRYDOCK, NOCTURNE, OPERA, ALIENS, Compare, and a Round 1 archive. Round-1 renders stay in `img/<direction>/`; round 2 is in `img/r2/<direction>/`. `ArtSource/LookDev/` now builds only the three round-2 directions; the round-1 code is at `1534b2e`.
- **Jagged corners fixed at the source.** The per-cell auto-slope pass is gone. Hulls are built from straight-sided pieces: lofts between octagonal sections, and general convex pieces (spires, geodesic spheres, faceted rings). Each cell is clipped exactly by the piece's planes, so a slope is one plane across every cell it crosses. Coplanar cell faces are merged before the bevel, which removed the pinched triangles at T-junctions.
- **DRYDOCK polish.**
  - Galactic band with dust lanes, rust and teal nebula, warm-to-cool gradient.
  - Faction liveries (signal blue, oxide red, hauler orange, teal) and function colours on blocks: weapons oxide red, power teal, cargo orange, engines graphite with heat-tinted nozzles.
  - Planet rings lit by the sun and shadowed by the planet. The new atmosphere is computed from the ray's closest approach, so it hugs the limb and fades out.
  - Seven new weapons in every direction's style: rotary cannon, heavy twin cannon, spinal mass driver, arc projector, plasma mortar, torpedo launcher, salvage harpoon. The kit is now 38 blocks.
  - Existing blocks redone, away from the placeholder look (glowing cube in a cage, glowing cylinders, dome shield, dish sensor):
    - cockpit: a cab with a window band;
    - reactor: a fission pile with control rods;
    - cores: a gyro, a gimbal, a foundry heart and a linear accelerator;
    - capacitor: a rack of cell modules;
    - shield: an insulator stack with emitter horns;
    - sensor: a lattice mast with a radar bar;
    - cargo: corrugated containers.
  - New ships MULE (salvage gunship) and BULWARK (container frigate with colour-coded container stacks).
  - Station KESSLER YARD rebuilt.
  - UI polish: hazard-striped headers, rivets, function-coloured tiles and weapon slots, stencil discovery stamp, backed discovery title.
- **NOCTURNE: industrial gothic horror (ALIEN-franchise mood). UI unchanged.**
  - Hunched, ribbed housings, spires, hoses, slit windows, red strobes.
  - Worn grey plating with ochre panels, oxblood weapons, mustard power, olive cargo, rain streaks and rust.
  - Vents are deep ribbed grilles with an ember glow instead of black squares.
  - Smoke-and-dust sky with a sickly glow, and a thin haze that catches work lights.
  - Ships WRAITH (dropship) and CANTICLE (refinery tug with spires). Station THE ROOD: an inverted-cathedral refinery under tow. Storm planet with night-side lightning.
- **Alien enemies (REEF brought into DRYDOCK and NOCTURNE).**
  - Same 0.5 m grid: each alien cell stores one signed-distance byte instead of a block shape, and a surface-nets mesher builds the smooth shell in one pass, with no smoothing step and no subdivision. Damage removes cells and leaves organic craters with glowing flesh.
  - HUSK (skimmer) and BROODMOTHER (armoured isopod: carapace plates, side spines, sacs, eyes, legs).
  - Carapace shader with a glowing vein network. Four colourways: bone & jade (DRYDOCK default), obsidian & acid (NOCTURNE default), blood coral, oil slick.
  - Combat in both worlds is now against them.
- **OPERA (new): space opera** from the owner's references (space-opera covers, DUNE, THE FIFTH ELEMENT, 2001, NO MAN'S SKY).
  - Cream hulls in bold liveries with paint bands, gold trim, chrome visors.
  - Ships LARK (swept-wing courier) and SOVEREIGN (geodesic command sphere, spine with a spinal gun, ring, engine monolith). Station THE CROWN (double wheel).
  - Painted ultramarine-to-magenta nebula skies; a banded gas giant with rings.
  - Art-deco blocks; cores are an armillary sphere, an orrery and cut gems.
  - New UI theme: gold rules and diamonds, octagonal tiles, compass radar, gilded centred discovery title. Fonts Cinzel and Josefin Sans, both SIL OFL 1.1.
- **Next:** the owner reviews round 2: polish more, pick one, or mix, then in-engine look tests.

### 2026-10-10 — 3D look book: six directions, ships, stations, planets, block kits, game screens (claude, claude-opus-5-5)
- **Owner feedback (verbatim, 2026-10-10)** on the ART-004/ART-005 output: "1. I was looking for atleast 6 DIFFERENT options 2. its a 3D game so when i asked for art directions I meant build 2 different varied ships for each look, and a station, and showcase how it would look like, how the ship, the station, the planets, etc. how the scenes would look like. don't get me wrong the UIs you've done so far are great for all 3 but its a 3D game and theres no emphasis on this at all! 3. the nocture UI looks great but the blocks being dark is just silly and bad contrast, try something new 4. art direction of blocks doesn't mean ALL BLOCKS ARE THE EXACT SAME, SLIGHTLY DIFFERENT TEXTURE. the point is I want to see how for example if we had angled blocks instead of just cubed. I want to see different interesting weapons. how modules could visually change the ships. how the cores should be really interesting blocks, like don't follow the old placeholders, but look at how we just used the grid (2x2, 3x3, 3x5) as a boundary but able to experiment a lot within, its like how in minecraft an enchanting table takes 1 block of space but its still  intricate within that space. not every block has to be super intricate, like hulls don't need to look crazy all the time. 5. one big thing I wanted to see if it was possible was some kind of procedural look where some hulls will have slightly different texture like windows, or latches, or wiring/tubes, not all but some in order to make it feel like the proper exterior of a spaceship, not a smooth boring thing. 6. i asked for inventory icons, i got no inventory or other game related screen mockups such as dialog discovery, etc. right now your mockups looks like its for a website and we're trying to make a video game in 3d 7. i need atmosphere, environment, background, intersting planet, ship - all things together make a visual design, but you understand i cannot pick an option if I just have an 8x8 grid of cubes as my only thing to base it on. 8. you spent so many hours following spec mumbo jumbo instead of optimizing for what the designer of a 3d games is actually looking for in order to make decisions, how is reduce motion more important than any of the other 7 points I mentioned above, yet you did that and none of the above"
- **Response: 3D look book** `docs/design/lookbook/index.html` (opens from file://), generated by `ArtSource/LookDev/` (Blender 5.2.2, fully procedural, about 4 minutes for everything).
  - **Six directions:** DRYDOCK, NOCTURNE (reworked: pearl ceramic hulls with light-lines instead of dark hulls), PULP HORIZON, and three new ones: ORBITAL (NASA-punk), REEF (grown biotech) and FACET (low-poly bold).
  - **Per direction:** two different ships, a station, a planet with atmosphere and moon, and a space environment. Shots:
    - hero composition;
    - free flight at the game camera (55° pitch, 40° FOV);
    - combat with carved cell damage, debris, explosions, beams, tracers and a shield hit;
    - station, planet reveal, Blueprint Lab on the 0.5 m grid;
    - ship close-ups.
  - **Block kit (31 blocks per direction, each built differently per direction within its footprint):**
    - hull, slope, corner, inner corner, 1×2 slope, rounded edge, heavy armour, girder;
    - four procedural hull variants: windows, hatch and latches, vents, pipes;
    - cockpit and five weapons: autocannon, railgun, missile pod, beam lance, flak;
    - modules that change the silhouette: two thrusters, reactor, shield, capacitor, cargo, radiator, sensor mast, solar wing;
    - intricate cores at 1×1×1, 2×2×2, 3×3×3 and 3×5×3.
  - **Procedural hull greebles** (windows, hatches, vents, pipe runs, plates, lights, antennas, and per-direction extras) are placed by a per-cell hash, with coherent runs for windows and pipes. A hull-variant slab render shows them per direction.
  - **Game screens** at 1920×1080, composited over the renders in each direction's own UI style: combat HUD, Blueprint Lab inventory with the rendered block icons, station dialog with choices and a clue tag, and a new-system discovery screen with clue and salvage.
  - **Compare tab:** all six side by side.
- **Size:** images total 18 MB (icons are 256 px WebP).
- The ART-005 board (`docs/design/board/`) stays as the token and contrast reference. The look book replaces it as the page for choosing a direction.
- **Next:** the owner picks finalists from the look book; the chosen directions then go to in-engine look tests.

### 2026-10-10 — ART-005 design board, in review (claude, claude-opus-5-5)
- **What was built.** `docs/design/board/{index.html,board.css,board.js}`: plain HTML, CSS and JS with no build step and no `fetch`. The only external host is Google Fonts.
  - **Tabs:** DRYDOCK, NOCTURNE, PULP HORIZON and Compare & score.
  - **Per direction:**
    - all 25 UI colour tokens, read live from the generated token CSS with WCAG badges on bg and surface-1, plus faction tokens;
    - hull neutrals, function colours and the team-slot table;
    - a type specimen for every text token plus mono and body-strong;
    - every component state listed in the task, styled per §x.8: DRYDOCK accent bar and stencil caps; NOCTURNE translucent panels, corner brackets, ghost buttons and thin bars; PULP HORIZON print-shadow cards and chunky bars;
    - a 1920 × 1080 HUD mock on the §10.4.2 zones with each direction's variant (NOCTURNE hull and shield arcs, PULP HORIZON dashboard strip), with a zone-outline toggle;
    - ART-004 renders at true pixel size, enlarged with hard pixels;
    - the damage strip;
    - VFX colour and timing pulses at the §x.7 durations;
    - space swatches;
    - the public-domain mood images with a link to each board.
  - **Global toggles:** colour-vision simulation (SVG `feColorMatrix` with the same Machado matrices as `check_tokens.py`, applied to the whole page) and reduced motion.
  - **Scorecard:** editable §8.3 scores with weighted totals, the §1.3 approval fields, and "Copy as Markdown", which outputs the §1.3 block plus the score table. Saved in `localStorage`, with every access wrapped in `try/catch`.
- **Check.** `tools/art/check_art_docs.py board` verifies that tokens match the sheets (`export_board_tokens --check`), that only the allowed hosts appear, that every referenced local file exists (90 files), that HUD zones equal §10.4.2, that the CVD matrices equal `MACHADO`, and page weight. Result: 0 errors. A negative test with a foreign host and a moved zone produced 2 errors.
- **Measurements.**
  - Headless Chrome from `file://`: 0 console messages; a positive-control page that logs an error was captured, so capture works.
  - HUD zone rectangles read back from the DOM equal §10.4.2 exactly.
  - Page weight 11.3 MB (budget 25 MB); first contentful paint 220 ms (budget 3 s).
  - Fonts load from Google Fonts; an offline banner appears when they don't.
- **Not verified:** Safari from `file://`. The owner or reviewer should open `docs/design/board/index.html` in Safari once.
- **Status.** `review` (frontier). It started before the ART-003 and ART-004 reviews by owner decision. The next step is ART-006: the owner's shortlist, using this board.

### 2026-10-10 — Owner answers: glyph rule at 150 m, ART-005 before reviews
- **Glyph rule (after the ART-004 finding):** "Colour at 150 m (Recommended)". At 150 m, 1-cell-wide functional blocks are identified by silhouette and function colour or light; their glyph is a 50 m feature. Applied in DESIGN-SYSTEM §3 P3, §4 R1, §5.5 and §12 row 1, and in SPEC-80 `ART-004` criterion 3 (now: glyph recognisable at 50 m, function colour identifiable at 150 m).
- **Next task:** "Build ART-005 board now (Recommended)". The owner lets ART-005 start while ART-003 and ART-004 are still in review; fixes from those reviews flow into the board afterwards.

### 2026-10-10 — ART-004 block-kit style test, in review (claude, claude-opus-5-5)
- **What was built.** `ArtSource/StyleTest/build_style_test.py` runs in two modes. Under the system Python (with Pillow) it drives the build and composes the composite. Inside Blender 5.2.2 it builds and renders, per direction:
  - procedural 256 px `hull_plain` and `hull_heavy` layers for damage stages 0–3, saved under `ArtSource/StyleTest/<dir>/textures/` (normal maps 16-bit);
  - the five blocks, following §x.5, §10.6 and §10.15;
  - lighting and post per §x.4: key pitch 35°/25°/45°, fill or rim light, ambient, bloom, saturation, and contrast pivoted on mid-grey in log space;
  - orthographic renders at 55° pitch, at 148 / 14.8 / 4.9 / 2.5 px per cell, plus the damage strip, `stats.json` and `style_test.blend`.
- **Measurements.** Pixels per cell are exact: within 0 % of §4. Triangles, LOD0 against budget (hull patches against 2 per exposed face):

  | Block | DRYDOCK | NOCTURNE | PULP | Budget |
  |---|---|---|---|---|
  | hull | 96 | 96 | 96 | 192 |
  | hull_angled | 104 | 104 | 104 | 192 |
  | thruster | 188 | 184 | 136 | 600 |
  | turret (base + yaw) | 324 | 320 | 272 | 600 |
  | core | 88 | 84 | 82 | 150 |

  `check_art_docs.py blocks` reports 0 errors.
- **Calibration and fixes found along the way.**
  - A Blender sun of strength 1 lights white diffuse to 1/π, so URP intensities are multiplied by π.
  - Texture PNGs are stored as Non-Color with sRGB encoded by hand; Blender's own save encoded twice and tilted normals 33°.
  - `ShaderNodeMix` colour sockets are picked by identifier (`A_Color` and so on).
  - In Blender 5 the glare type is a menu input.
- **Findings for the owner and the reviewer.**
  1. **1-cell-wide functional blocks at 150 m.** A glyph on a 1 × 1 face is about 5 px in every direction. Only the function colour reads; the chevron, crosshair and diamond do not. Acceptance criterion 3 (glyph recognisable at 150 m) will likely fail for all three blocks in all three directions. This is a DESIGN-SYSTEM rule question (R1 already says 4 px needs at least 2 cells), not a modelling fix.
  2. **NOCTURNE.** Obsidian plates almost vanish at 150 and 300 m; only seams and rim light carry them (§6.9 risk confirmed). Emissive glyphs with bloom make the functional blocks the most legible of the three. Damage cracks read strongly.
  3. **PULP HORIZON.** The constant 1.5 px ink outline swamps the 1-cell blocks at 150–300 m; the turret becomes an ink blob. Flat fields and halftone damage read well at 50 m.
  4. **DRYDOCK.** Bone and gunmetal plates read clearly. The 1-cell function band dominates 2-cell blocks. Stencil codes (`THR-S`, `WPN-PL`, `CORE`) read at 5 m but not at 50 m.
- **Interpretations.**
  - Glyphs, bands and stencils are thin decal geometry, counted in the budgets, not trim-sheet textures.
  - The angled patch is 4 × 4 wedges, a stepped slope.
  - The core follows §5.3 (green top panel and beacon) rather than a full 1-cell band.
  - Stencil text uses the placeholder Office Code Pro Bold (OFL); the direction fonts arrive with ART-008.
  - Each 300 m render is a few pixels; the composite enlarges it with nearest-neighbour pixels.
- **Status.** `review` (frontier). It needs an independent frontier review, and a second reviewer who has not seen the block names must judge the 150 m glyph renders (criterion 3); name that reviewer here. `.gitattributes` now marks `*.blend` binary, `.gitignore` ignores `*.blend1`, and outputs total 8.2 MB (board budget 25 MB).

### 2026-10-10 — ART-007 progress; owner picks ART-004 next (claude, claude-opus-5-5)
- **ART-007 progress (branch `task/ART-007-brick-tools`, 083c482).**
  - `BrickMaterialBaker`: bakes 61 distinct layers in 1.9 s (budget 60 s). Arrays are BC7/BC7/BC5 at 256 × 256 × 64 with 9 mips, plus layer params and palette textures, and an input hash for ART-W001.
  - `BrickPreviewMeshBuilder`: a 16³ cube gives 3,072 triangles.
  - `SH_BrickReference`: hand-written URP HLSL, which SPEC-80 §5.3 allows, so the file is `.shader` rather than `.shadergraph`.
- **Shader checks** (batch mode with a Metal device):
  - `ShaderHasError` false.
  - SRP Batcher compatibility code 0 (compatible). The Material-inspector screenshot the acceptance criteria ask for still needs the GUI Editor.
  - Test-hull renders in all three directions' §10.14.3 parameters show paint, team colours, emissive, scorch, cracks with embers, exposed internals, halftone and the toon ramp.
- **Tests:** 9 tests (baker 7, mesher 4, shader 2 — the counts include extras beyond the spec list). EditMode 34/34.
- **Extensions and interpretations:**
  - `PreviewCell.FlagSolid` (bit 1) marks solid cells; the spec defines only bit 0.
  - `uv1` is the face UV in cells, so the shader can tile macro-panels.
  - Paint keeps the texture's luminance relative to the layer mean (its last mip).
  - An empty slot (`copyOf` −1, no textures) copies layer 63.
- **Pending:** `ArtDatabaseValidator`, which needs `ContentCatalog`, so the chain is CORE-002 → DATA-004 → DATA-005.
- **Owner answer (sequencing):** "ART-004 block kit (Recommended)". The ART-007 validator is parked until after ART-004.

### 2026-10-10 — DATA-002 ValidationReport, done (claude, claude-opus-5-5)
- Implemented `ValidationReport`, `ValidationScope`, `ValidationEntry` and `ContentValidationException` per SPEC-04 §4.8.
  - Null strings become `""`.
  - `Sorted()` orders ordinally by AssetPath, Field, Code and Message, then by severity.
  - `ToText` prints one `[E] CODE path field: message` line per entry, then `... N more` when capped, then `N errors, M warnings, K infos`.
  - `LogTo` writes errors first, so the cap never hides an error behind warnings. It ends with one summary line (`Context: summary (N not shown)`) at the highest severity present.
  - The exception message is the context plus `ToText(50)`.
- Contract additions: internal constructors for `ValidationEntry` and `ValidationScope`, which the public contract cannot build otherwise.
- `ValidationReportTests`: 5 tests (the 4 in the spec plus `Exception_CarriesReport`). Compile has 0 warnings; EditMode 21/21.
- `ContentValidationContext` is left for DATA-004.

### 2026-10-10 — CORE-004 GameLog, done (claude, claude-opus-5-5)
- `GameLog` implemented per SPEC-02 §5.4: `[Category] message` format, per-category minimum levels (ordinal dictionary, default Info in the Editor and development builds, Warning in release), `Exception` (never filtered), `WarnOnce`/`ErrorOnce` counters with a `"<null>"` key for null, `GameLog.Burst.Warn/Error`, and a `SubsystemRegistration` reset.
- `GameLogTests`: 6 tests, all passing. The Burst test schedules a `[BurstCompile(CompileSynchronously = true)]` job. Inside Burst a `FixedString128Bytes` must come from a string literal; the `new FixedString128Bytes(string)` constructor fails with BC1016.
- Compile: 0 warnings. EditMode: 16/16 passing.
- Done now so that `ValidationReport.LogTo` (DATA-002) can use it, on the way to ART-007 (owner answer to question 4).

### 2026-10-10 — Owner answers: Blender install, ART-003 review, next task
- **Blender install:** "I download the DMG (Recommended)". Blender 5.2.2 LTS (`blender-5.2.2-macos-arm64.dmg`, 346 MB, from download.blender.org) is installed at `/Applications/Blender.app`. Its SHA-256 matches the published `blender-5.2.2.sha256`, it is notarized by the Stichting Blender Foundation (68UA947AUU), and it runs headless (`-b`, Python 3.13.13). ART-004 is unblocked.
- **ART-003 review:** "Leave it at review". ART-003 stays `review`, and ART-005 stays blocked until an independent frontier review.
- **Next task:** "ART-007 look-test tools (Recommended)".

### 2026-10-10 — ART-003 mood-board images (claude, claude-opus-5-5)
- Added 13 public-domain images from Wikimedia Commons under the owner's approval: DRYDOCK 6 (ISS render, Apollo 15 service module, McCandless MMU, *Earthrise*, Apollo 11 command module after re-entry, ISO 7010 warning layout), NOCTURNE 3 (Hubble *Pillars of Creation* 2014, Webb *Cosmic Cliffs* via STScI, USGS Kamoamoa fissure), PULP HORIZON 4 (NASA Ames Stanford torus and Bernal sphere paintings by Don Davis and Rick Guidice).
- Each is JPEG quality 85, long side ≤ 1600 px, ≤ 400 KB, with its Commons source and licence line; 3.7 MB in total. Licence taken from Commons metadata; CC BY and CC BY-SA versions (ESA releases, McCandless retouches, heat-shield close-ups) were left out. No public-domain image was found for ship-breaking yards, the Chrysler Building or Ben Day dots; they stay links.
- `check_art_docs.py moodboards`: 0 errors. ART-003 stays `review` (commit a3d0d92) until an independent frontier review.

### 2026-10-10 — Owner answers on the art track
- **ART-004 tool:** "Install Blender". ART-004 follows SPEC-80 as written (Blender plus a Python build script).
- **Mood-board images:** the full public-domain set (about 8–12 images per direction), downloads approved.
- **Pre-gate code (question 4):** answered; see *Open questions*.

### 2026-10-10 — ART-002 direction token sheets and checker (claude, claude-opus-5-5)
- **What.** `Assets/Game/UI/Theme/Directions/tokens-{drydock,nocturne,pulp-horizon}.uss`, 67 tokens each: the 44 binding tokens with their §8.1 column plus every §11 token, generated from the DESIGN-SYSTEM tables. `tools/art/check_tokens.py` (with `--verify-doc`), `tools/art/test_check_tokens.py` and `tools/art/export_board_tokens.py`, which writes `docs/design/board/tokens/*.css` scoped to `[data-direction]`, with Google Fonts imports that all return HTTP 200.
- **Design-system correction.** The checker reproduces all 36 §8.1 contrast values exactly. In §10.2 it found DRYDOCK hull–energy at **11.99, below the C4 minimum of 12**; the table's "12.0" had rounded up. DRYDOCK `--color-hull` changed from `#E6DCC8` to `#E5DCC8` (one channel by 1, ΔE 0.5), which gives hull–energy 12.4 and hull–shield 29.6. Three other §10.2 cells were corrected by 0.1–0.2 to the computed values (placeholder ally–neutral 25.1, placeholder hull–shield 19.3, PULP ally–enemy 49.6), with a note under the table. Method: Machado severity 1.0 in linear RGB, clamped to [0, 1], then CIEDE2000.
- **Checks.** `check_tokens.py --verify-doc`: 4 sheets, 0 errors, 0.01 s (budget 2 s). Every sheet value equals DESIGN-SYSTEM; T1, T2 (`--color-border-strong` worst 3.15 / 3.42 / 3.85) and C4 pass. `test_check_tokens.py`: 8/8 pass, including Sharma 2005 pairs 1, 7, 17 and 25 to ±0.0001, and removal of each of the 44 binding tokens failing with the token named. `tools/compile.sh` clean. EditMode 10/10.
- **Known noise.** Importing the three sheets logs one "Invalid asset path" warning per look-test font URL (12 in total): `Assets/Game/Art/LookTest/Fonts/` is filled by ART-008, as SPEC-80 §4.4 specifies. Compiler warnings: none.

### 2026-10-10 — ART-001 art contract types, tokens and placeholder fonts (claude, claude-opus-5-5)
- **What.** The 15 types of SPEC-80 §4.1–§4.3 in `Assets/Game/Data/Definitions/Art/`. `tokens.uss` rewritten byte-for-byte to §4.4: the bootstrap version predated SPEC-80 and pointed at the unlicensed Kontrapunkt font. `OfficeCodePro-{Regular,Bold}_SDF.asset` are TextCore FontAssets with the DESIGN-SYSTEM §10.9 settings: SDF AA, 90 pt sampling, padding 9, 1024² atlas, dynamic, multi-atlas. They were made by a throwaway editor script that was then deleted. Unity clears dynamic glyph data on save, so the §10.9 character set fills in at runtime. Folders `Content/Art`, `Art/LookTest` and `Editor/Art` were added, plus the glossary section *Art and presentation* (SPEC-80 Q13).
- **Open question 9 answered: yes.** `TokenFontTests.FontBodyToken_ResolvesToPlaceholderFontAsset` (PlayMode, with a test-only `TokenFontTest.uss`) resolves `-unity-font-definition: var(--font-body)` to the font asset, so the fallback classes `.font-*` are not needed.
- **Checks.** `tools/compile.sh` clean with zero warnings. EditMode 10/10 pass (`ArtContractTests` ×4 is new). PlayMode 2/2 pass. Reimporting `tokens.uss` after the fonts exist logs no warnings.

### 2026-10-10 — DATA-001 public contract types (claude, claude-opus-5-5)
- **What.** 60 files transcribed from SPEC-04 §4.2–§4.9 in `Game.Data` (`Blocks/`, `Components/`, `Definitions/{Common,Items,Blocks}/`, `Validation/`, `Services/`, `ContentDatabase`), plus `Tests/EditMode/Data/ContractShapeTests.cs`. Stub bodies throw `NotImplementedException("DATA-00x")` naming the task that implements them.
- **Choices.** `ContentDefinition.Id` caching is implemented now (two lines), because the spec's cache fields would otherwise be unused and warn; DATA-004 keeps ID rules and validation. `OrientationTables` does not hold its `static readonly OrientationData` yet: with a throwing generator, the static initialiser would fail on first touch, so DATA-003 adds it. `OrientationData` span properties are expression-bodied, since a class cannot store a `ReadOnlySpan`. The empty folder `Assets/Game/Editor/Content/` is left to DATA-008, because git does not keep empty folders.
- **Checks.** `tools/compile.sh` clean with zero warnings. EditMode 6/6 pass (`ContractShapeTests` ×2 plus the four `Project` guards).

### 2026-10-10 — CORE-001 public contract types (claude, claude-opus-5-5)
- **What.** 88 files transcribed from SPEC-02 §4.1–§4.12 into `Game.Core` (root, `Components/`, `Systems/`, `Events/`, `Save/`, `Flow/`, `Settings/`), `Game.Data` (`Components/`, `Combat/ProducerKind`, `Streams/`) and `Game.Presentation/Systems`. Bodies not given in §4 throw `NotImplementedException("CORE-0xx")`; `GuardedHandles` is complete, including `Create`/`Dispose`.
- **Choices.** `Game.Core.asmdef` now references `Unity.Transforms` (part of the Entities package, allowed by architecture §3.1) because the §4.11 groups order against `TransformSystemGroup`. `PersistentIdAllocator` and `IPersistentIdAllocator` are created as §4.1 specifies, although the task's *Files* list omits them. The internal `EventChannel<T>` fields are left to CORE-006, since unused fields would add warnings. `ManagedEventDrainSystem` lifecycle methods stay empty (open question 5).
- **Checks.** `tools/compile.sh` clean with zero warnings (headless). EditMode `Project` tests 4/4 pass (`AssemblyRulesTests`, `ConventionsTests`, settings guard). PlayMode smoke test 1/1 passes; the Play Mode log has no Entities "Ignoring invalid" ordering warning and no game-code errors.

### 2026-10-10 — ART-003 mood boards, in review (claude, claude-opus-5-5)
- **Deliverables.** `docs/design/moodboards/{drydock,nocturne,pulp-horizon}.md` (22, 19 and 19 references in the six groups, three anti-references each) and a format README; `tools/art/check_art_docs.py moodboards` (counts, required fields, one-sentence takes, touchstones matched against DESIGN-SYSTEM §x.2, JPEG size, long side, IJG quality and licence lines).
- **Check.** `check_art_docs.py moodboards`: 0 errors. Negative cases (missing touchstones, a touchstone not in §x.2, a two-sentence take, a missing year, a CC BY image) each fail as expected. Every external link returned HTTP 200 when written.
- **No images yet.** Downloading files needs the owner's OK. Planned sets: NASA public-domain photographs (DRYDOCK), NASA-only Hubble/Webb releases (NOCTURNE; ESA-credited versions are CC BY 4.0 and stay links), the NASA Ames colony paintings by Rick Guidice and Don Davis (PULP HORIZON).
- **Status `review`.** Frontier tier: `done` needs an independent frontier review (`reviewed-by:`).

### 2026-10-10 — Design system first (owner decision)
- **Owner, verbatim:** "our first thing is to nail the look of the game in terms of blocks, ui, meshes, colors, textures, etc. nothing proceeds until the design system is spot on".
- ROADMAP now puts the A0 art track ahead of every milestone; the owner-gates row notes that `ART-013` also blocks M0–M4. The look-test prerequisites that may run first are open question 4 (default in use).
- Not started: the adversarial spec review pass is deferred behind the gate.

### 2026-10-10 — Extensibility review applied (lead, claude-opus-5-5)
- **Owner decision.** Apply all items as recommended; voxel small craft for X7.
- **Commits.** Ten commits, `e3e6c86` through `6b3ee37`:
  - X9: shared combat enums.
  - X8: `SimEventChannel<T>` + `GuardedHandles`, new task CORE-012.
  - X1/X11: block modules, ADR-0013.
  - X2: feature-owned power demand and stats, ADR-0014.
  - X3: grid profiles, stowed and transient grids, ADR-0015.
  - X4: request channels for impulses, restores, inhibits and stat modifiers, ADR-0016.
  - X6: instigator.
  - X5: AI ability rules and kind-agnostic ability and weapon lifecycles.
  - X10: damage tables.
  - X7: voxel small craft, fleets and wings at M3, swarm and launch gates, ADR-0017.
  - X12: open questions.
- **New tasks:** CORE-012, WPN-018, DMG-019, PHY-022, PWR-010. AI-013…015 moved to M3. `check-docs`: 33 specs, 496 tasks, 0 errors.
- **Corrections to the review.**
  - AI ability use was SPEC-23 Q1's documented default (now answered).
  - SPEC-31 already armed stations; the issue was a duplicated helper list.
  - Details are in `docs/specs/_EXTENSIBILITY-REVIEW.md` §5.

### 2026-10-10 — Extensibility review before buildout (lead, claude-opus-5-5)
- Walked four probe features (fighter bay, new weapon delivery, ion/repair destruction, tractor/cloak/nebula) through every spec. Today a fighter bay must edit existing code paths in 9 specs; the proposals reduce that to registrations plus one new spec.
- Found one real bug: AI ships can never use abilities (SPEC-24 §5.1 step 6 is player-only; SPEC-23 decision 6 writes `Edges = 0`). Also found frame-rate-dependent spawning from the fixed step, three hand-written stream registries, two mirror enums and two dead utility kinds.
- Proposals X1–X12 in `docs/specs/_EXTENSIBILITY-REVIEW.md`; nothing applied. Open question 3 needs the owner.

### 2026-10-09 — Integration pass: all 93 cross-spec items applied (lead, claude-opus-5-5)
- Architecture errata (§3 test kit, §5 galaxy ticker/salvage/rails/factions, §7.4 jump, §8 writers) and SPEC-01 wiring for every service and boot singleton of SPEC-12…50 (construction rows 8.2a–8.4, 10.1; `GalaxyFlushWriteback`; `IShipSpawner`; `FleetModel.Changed`; test hooks for save and settings paths).
- Core and data: `OriginShiftRecord`, `RngPurpose.Salvage`, read-only event producer pattern (SPEC-02/63/64), full content-ID prefix registry (SPEC-04, including missing VFX/audio prefixes; `config_vfx`/`config_audio`), SPEC-05 uses Core save enums and owns its quit hook.
- Voxel: SPEC-10 has no codec of its own any more (adapter over SPEC-05 `VoxsCodec`; Appendix A removed); tick-gated `DeferredDisposal` (safe while paused); queue access handles (SPEC-10/11); SPEC-13 hulks through `GridBodyFactory`, `DamageImmune`, `LastDamageSource`, chunk-blob cleanup, `GridDissolveRequest` rename.
- Simulation and galaxy: `ShipDestroyed` payload and bridge (new task WPN-017), `ShieldHitStreams` registration API, gyro torque, autofire power demand, player control lock, cursor-pick aim, `Cruise` edge from M0 (replays have 10 edge bytes), origin placement with velocity re-expression, `CompiledCondition` lanes, revisions, opening-chapter content in GAL-012.
- Narrative, UI, tools: SPEC-40 adopts the real event payloads and concrete wiring; SPEC-61 reworked against the real sources (dock screen added, defeat through SPEC-01's dialog, gate prompt and route readout, notices, `ShipLogQuery`, faction colours); SPEC-51 uses `IGameFlow` and block/item identity; SPEC-70 entry points, actions and probes corrected; SPEC-72 writes the v2 values; SPEC-80/81 tokens and the jump-cover timing contract.
- Drift found and fixed beyond the list: SPEC-51 and SPEC-70 called APIs that SPEC-01/05/40 never defined; SPEC-72 used `sfx_*` audio IDs against SPEC-64's `cue_*`; SPEC-70 fire/console bindings disagreed with SPEC-03; SPEC-63 tracers still read projectile entities.
- `check-docs`: 33 specs, 491 tasks, 0 errors. Open questions 1–2 above need the owner. Next: the adversarial review pass.

### 2026-10-09 — Specs SPEC-32, 33, 42, 50, 71 written; spec set complete (lead, claude-opus-5-5)
- Wrote SPEC-32 (travel: gate jumps with a fixed spool/transit/arrival timeline, analytic arrival inside the target system (R30), fact-gated lanes through `LogicWatcher`, fuel at commit, routes, `PlayerControlLock`, M4 supercruise hop and escorts), SPEC-33 (factions with a fixed-step sole writer of `FactionRelations`, provocation, seeded encounter director, story encounters that persist until defeated, territory), SPEC-50 (200-slot inventory with capacity prefix and overflow, atomic delta sets for the editor (R15), loot, merchants (06-A13), salvage), SPEC-42 (story beats, declarative quests with baselines, clues/rumors/mysteries, ship log, legacy placement table, opening chapter: Ark Reach, The Drift, Whisper) and SPEC-71 (test kit `EcsTestWorld`/`GameTestBase`/`PerfRun`, replay, `GameFlowFixture`, voxel/determinism/soak/frame-budget/iteration gates).
- Dependency fixes: VOX-018 moved to SPEC-71 TST-004 (ledger row removed by hand, the only manual row change); kit dependencies changed from `SPEC-71` to `TST-001`; VOX-001 moved to M0; REN-005/REN-011, EDT-021/EDT-022, HUD-015 use whole-spec tokens. `check-docs`: 33 specs, 490 tasks, 0 errors, 0 warnings.
- Cross-spec follow-ups now items 1–93 in `docs/specs/_INTEGRATION-TODO.md` (16 and 52 done); next: apply them, then the adversarial review pass (weak-implementer simulation, consistency, tiers, scale notes).

### 2026-10-09 — Specs SPEC-12, 20, 21, 23, 24, 31 written (lead, claude-opus-5-5)
- Wrote SPEC-12 (voxel physics: two-level compound colliders, deterministic install ticks, exact incremental mass with velocity re-expression, `PhysicsLayers`), SPEC-20 (flight: `ShipCommand`/`PlayerCommand`, thruster allocation by projected Gauss–Seidel, core gyro, planar constraint by impulses), SPEC-21 (weapons: struct-of-arrays `ProjectileStore`, cell-exact hit tests through the spatial hash, beams, explosions, `Allegiance`/`FactionRelations`), SPEC-23 (AI: `SpatialHash`, staggered decisions, steering through `ShipCommand`, `PlayerCombatStatus`, M4 fleets), SPEC-24 (abilities from blocks, one-tick power grant), SPEC-31 (star-system streaming T1–T3, bodies on rails in the fixed step, R10 surface-distance interaction, `PLOC`, damage persistence of small bodies).
- Fixed dependency cycles: VOX-001 no longer depends on REN-001/PHY-001 (VoxelSingletons moved to VOX-013); PWR-008 depends on WPN-001 instead of SPEC-21; INP-007 depends on SPEC-20.
- 53 cross-spec follow-ups are recorded in `docs/specs/_INTEGRATION-TODO.md` for the review pass (architecture §5/§8 errata, SPEC-02/30 `OriginShiftRecord`, SPEC-22 `ShieldHitStreams` registration API, SPEC-63/64 producer pattern, SPEC-10/13 contract alignment).
- Remaining specs: SPEC-32 travel, SPEC-33 encounters/factions, SPEC-42 quests/mystery, SPEC-50 economy, SPEC-71 testing (moves VOX-018 there). `check-docs`: 28 specs, 435 tasks, 67 errors — all from the five unwritten specs or listed in integration item 52.

### 2026-10-09 — Project bootstrap (lead)
- Created the Unity 6000.6.5f1 project with the Unity CLI (URP template, stripped of samples), package manifest per ADR-0011, 17 assembly definitions per architecture §3, `ProjectSetup` (required settings + guard test), assembly-rule and conventions guard tests, PlayMode boot smoke test, ported the generic CLI harness and playtest scripts, `tools/compile.sh`, `tools/test.sh`, `tools/check-docs.py`, `tools/ledger-sync.py`.
- Carried over placeholder art (`Assets/Game/Art/_Placeholder/`), audio (`Assets/Game/Audio/`), and the legacy content export (`Import/Legacy/`). The prototype audit is under `docs/reference/audit-2026-10/`.
- Wrote the binding foundation docs (00–06, ROADMAP, ADR-0001…0012) and the system specs (SPEC-01…SPEC-81).
