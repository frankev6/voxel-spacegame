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
| M0 | 60 | 0 | 0 | 0 | 6 | 0 | 66 | 18 |
| M1 | 100 | 0 | 0 | 0 | 0 | 0 | 100 | 10 |
| M2 | 87 | 0 | 0 | 0 | 0 | 0 | 87 | 0 |
| M3 | 180 | 0 | 0 | 0 | 0 | 0 | 180 | 1 |
| M4 | 14 | 0 | 0 | 0 | 0 | 0 | 14 | 0 |
| A0 | 7 | 1 | 0 | 4 | 4 | 0 | 16 | 1 |
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
| FND-002 | Bootstrap, GameRoot and explicit World creation | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | frontier | FND-001 | yes | todo |  |  |  |  |
| FND-003 | PauseService | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | standard | FND-001 | yes | todo |  |  |  |  |
| FND-004 | SettingsService and SettingsApplier | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | standard | FND-001, PERS-001 | no | todo |  |  |  |  |
| FND-005 | Composition root and BootSingletons | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | frontier | FND-002, FND-003, FND-004, SPEC-02, SPEC-04 | no | todo |  |  |  |  |
| FND-006 | SceneFlow transitions and teardown | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | frontier | FND-005 | no | todo |  |  |  |  |
| FND-007 | SimClock lifecycle and SimClockAdvanceSystem | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | frontier | FND-006 | no | todo |  |  |  |  |
| FND-014 | Static state audit and Play Mode re-entry | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | standard | FND-006 | no | todo |  |  |  |  |
| CORE-001 | Public contract types | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | standard | — | yes | done | claude (claude-opus-5-5) | 2026-10-10 | 4c3a121 | Drain-system lifecycle stub: open question 5 |
| CORE-002 | StableHash and content identity | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-001 | yes | todo |  |  |  |  |
| CORE-003 | Random streams and fixed-step time | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-002 | no | todo |  |  |  |  |
| CORE-004 | GameLog | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | standard | CORE-001 | yes | done | claude (claude-opus-5-5) | 2026-10-10 | eccf02b |  |
| CORE-005 | Services locator | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-001 | yes | todo |  |  |  |  |
| CORE-006 | EventBus and EventHub | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-004 | yes | todo |  |  |  |  |
| CORE-007 | Binary save primitives | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-002 | no | todo |  |  |  |  |
| CORE-008 | System groups and pipeline order | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-001 | yes | todo |  |  |  |  |
| CORE-009 | ECS→managed event bridge | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-006, CORE-008 | no | todo |  |  |  |  |
| CORE-010 | Math helpers | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | standard | CORE-001 | yes | in-progress | claude (claude-opus-5-5) | 2026-10-11 |  |  |
| CORE-011 | Settings model | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | standard | CORE-001 | yes | todo |  |  |  |  |
| CORE-012 | Simulation event channels | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-001, CORE-004 | yes | todo |  |  |  |  |
| INP-001 | Public contract types and the actions asset | [SPEC-03](specs/SPEC-03-input.md) | M0 | standard | CORE-001 | yes | todo |  |  |  |  |
| INP-002 | InputService contexts and display strings | [SPEC-03](specs/SPEC-03-input.md) | M0 | standard | INP-001 | no | todo |  |  |  |  |
| INP-003 | Rebinding and overrides | [SPEC-03](specs/SPEC-03-input.md) | M0 | standard | INP-002 | no | todo |  |  |  |  |
| DATA-001 | Public contract types | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | CORE-001 | yes | done | claude (claude-opus-5-5) | 2026-10-10 | 12a585b | `ContentDefinition.Id` cache already implemented (DATA-004 overlap) |
| DATA-002 | ValidationReport implementation | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | DATA-001 | yes | done | claude (claude-opus-5-5) | 2026-10-10 | fcaa9d3 |  |
| DATA-003 | Orientation tables | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | frontier | DATA-001 | yes | todo |  |  |  |  |
| DATA-004 | Definition self-validation and ID rules | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | DATA-002 | yes | todo |  |  |  |  |
| DATA-005 | ContentCatalog | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | frontier | DATA-004 | no | todo |  |  |  |  |
| DATA-006 | Block blob builder and placement maths | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | frontier | DATA-003, DATA-005 | no | todo |  |  |  |  |
| DATA-007 | ContentService and ECS singletons | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | frontier | DATA-006 | no | todo |  |  |  |  |
| DATA-008 | Editor database builder, postprocessor, validator runner and CLI | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | DATA-005 | no | todo |  |  |  |  |
| DATA-009 | System placeholder content and the database asset | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | DATA-008 | no | todo |  |  |  |  |
| PERS-001 | Public contract types | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | standard | CORE-001, DATA-001, VOX-001 | yes | todo |  |  |  |  |
| PERS-002 | Save paths, profiles, slots and meta files | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | standard | PERS-001 | no | todo |  |  |  |  |
| PERS-003 | Binary container, CRC32 and compression | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-001 | no | todo |  |  |  |  |
| PERS-004 | ID palette and SaveWriter/SaveReader integration | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-003, SPEC-02 | no | todo |  |  |  |  |
| PERS-005 | Save pipeline: capture, worker, atomic write, thumbnails, queue | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-002, PERS-004, SPEC-04 | no | todo |  |  |  |  |
| PERS-006 | Load pipeline and restore orchestration | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-005 | no | todo |  |  |  |  |
| PERS-007 | VOXS codec | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-001, SPEC-04 | no | todo |  |  |  |  |
| PERS-008 | Deferred VOXS snapshots in saves | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-007, PERS-005 | no | todo |  |  |  |  |
| PERS-009 | Autosave scheduler and save on quit | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | standard | PERS-006 | no | todo |  |  |  |  |
| PERS-010 | Fixture harness, container fixtures and the 04-B1 EditMode regression | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | standard | PERS-006 | no | todo |  |  |  |  |
| PERS-011 | Round-trip byte equality (framework level) | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | standard | PERS-006 | no | todo |  |  |  |  |
| PERS-012 | Save performance and size gates | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | frontier | PERS-008 | no | todo |  |  |  |  |
| VOX-001 | Public contract types | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M0 | standard | CORE-001, DATA-001 | yes | done | claude (claude-opus-5-5) | 2026-10-11 | c37ad48 | BrickRecord 28 B (ledger Q9); VOXS adapter members wait for VOX-009 (Q10) |
| AUD-001 | Public contract types | [SPEC-64](specs/SPEC-64-audio.md) | M0 | standard | CORE-001, DATA-001 | yes | todo |  |  |  |  |
| AUD-002 | Mixer asset, voice settings and import rules | [SPEC-64](specs/SPEC-64-audio.md) | M0 | standard | AUD-001 | no | todo |  |  |  |  |
| AUD-003 | AudioService core: root, single listener, voice pools, volumes, pause | [SPEC-64](specs/SPEC-64-audio.md) | M0 | frontier | AUD-002 | no | todo |  |  |  |  |
| DEV-001 | Public contract types | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | standard | CORE-001 | yes | todo |  |  |  |  |
| DEV-002 | Verify and document the as-built generic harness | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | standard | — | yes | todo |  |  |  |  |
| DEV-003 | DevRuntimeHost, command registry, operations, help/op/until | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | frontier | DEV-001, FND-001 | no | todo |  |  |  |  |
| DEV-004 | Runtime input injection and UI query | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | standard | DEV-003 | no | todo |  |  |  |  |
| DEV-005 | Scenario language: parser, expressions and built-in probes | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | frontier | DEV-003 | no | todo |  |  |  |  |
| DEV-006 | Scenario runner, results JSON and CLI/scripts | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | frontier | DEV-004, DEV-005 | no | todo |  |  |  |  |
| DEV-020 | Release-strip guarantee | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | standard | DEV-003 | no | todo |  |  |  |  |
| TST-001 | Test kit core | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M0 | frontier | CORE-001 | yes | todo |  |  |  |  |
| TST-008 | Iteration gates | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M0 | standard | TST-001 | no | todo |  |  |  |  |
| MIG-001 | Public contract types | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | standard | DATA-001 | yes | todo |  |  |  |  |
| MIG-002 | Legacy JSON index and strict reading | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | standard | MIG-001 | no | todo |  |  |  |  |
| MIG-003 | Legacy ID map | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | standard | MIG-001 | no | todo |  |  |  |  |
| MIG-004 | Asset writer, manifest, runner and report | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | frontier | MIG-002, MIG-003, SPEC-04 | no | todo |  |  |  |  |
| MIG-005 | Item converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | standard | MIG-004 | no | todo |  |  |  |  |
| MIG-006 | Block converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | standard | MIG-005 | no | todo |  |  |  |  |
| MIG-007 | Legacy Euler to orientation | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | frontier | MIG-001, SPEC-04 | no | todo |  |  |  |  |
| MIG-008 | Ship blueprint converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | frontier | MIG-006, MIG-007, SPEC-05, VOX-001 | no | todo |  |  |  |  |
| MIG-016 | Migration gate test | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | standard | MIG-008 | no | todo |  |  |  |  |
| FND-008 | PersistentId allocation and refill | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M1 | frontier | FND-007 | no | todo |  |  |  |  |
| FND-009 | GameSession: new game, end, IGameFlow, ISaveContext, SESS | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M1 | frontier | FND-006, FND-008, PERS-001 | no | todo |  |  |  |  |
| FND-010 | Fleet model, FLET and the player ship | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M1 | frontier | FND-009, VOX-001, PERS-001 | no | todo |  |  |  |  |
| FND-016 | Sandbox entry | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M1 | standard | FND-010 | no | todo |  |  |  |  |
| INP-004 | Context integration with SPEC-60 | [SPEC-03](specs/SPEC-03-input.md) | M1 | standard | INP-002, UI-001 | no | todo |  |  |  |  |
| INP-005 | PlayerInputSampleSystem | [SPEC-03](specs/SPEC-03-input.md) | M1 | frontier | INP-002, FLT-001, CAM-001, PHY-001 | no | todo |  |  |  |  |
| INP-006 | Camera and editor samplers | [SPEC-03](specs/SPEC-03-input.md) | M1 | standard | INP-002, CAM-001 | no | todo |  |  |  |  |
| INP-007 | Frame-rate independence test | [SPEC-03](specs/SPEC-03-input.md) | M1 | standard | INP-005, SPEC-20 | no | todo |  |  |  |  |
| DATA-011 | v2 block additions (reactor, cargo) | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M1 | standard | DATA-009 | no | todo |  |  |  |  |
| VOX-002 | SPIKE: memory model for the reference and thin-shell stations | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-001 | yes | todo |  |  |  |  |
| VOX-003 | Sparse-brick fallback (only if VOX-002 is no-go) | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-002 | no | todo |  |  |  |  |
| VOX-004 | Brick maths and mask operations | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-001 | yes | todo |  |  |  |  |
| VOX-005 | VoxelStructure storage core | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-004 | no | todo |  |  |  |  |
| VOX-006 | Block placement, removal and compaction | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-005, SPEC-04 | no | todo |  |  |  |  |
| VOX-007 | Clone, diff, block enumeration and mass | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-006 | no | todo |  |  |  |  |
| VOX-008 | Grid-space DDA raycast | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-005 | no | todo |  |  |  |  |
| VOX-009 | VOXS codec | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-007, SPEC-05 | no | todo |  |  |  |  |
| VOX-010 | Voxel definitions and config factory | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | standard | VOX-001 | yes | todo |  |  |  |  |
| VOX-011 | Structure registry and BlueprintLibrary | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-009, VOX-010 | no | todo |  |  |  |  |
| VOX-012 | ECS cell write and read helpers | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-004 | no | todo |  |  |  |  |
| VOX-013 | GridSpawnSystem: spawn path and singletons | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-006, VOX-011, VOX-012, REN-001, PHY-001, TST-001 | no | todo |  |  |  |  |
| VOX-014 | Rebuild, despawn and write-back | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-013 | no | todo |  |  |  |  |
| VOX-015 | BrickDirtyScheduleSystem | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-012, REN-001, PHY-001 | no | todo |  |  |  |  |
| VOX-016 | DeferredDisposalSystem | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-013 | no | todo |  |  |  |  |
| VOX-017 | Benchmark structures | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | standard | VOX-005 | no | todo |  |  |  |  |
| VOX-019 | Damage persistence scenario | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | standard | VOX-014 | no | todo |  |  |  |  |
| REN-001 | Public contract types | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | standard | VOX-001, DATA-001 | yes | todo |  |  |  |  |
| REN-002 | SPIKE: Entities Graphics with thousands of runtime brick meshes | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-001, SPEC-10 | no | todo |  |  |  |  |
| REN-003 | Fallback renderer (only if REN-002 is no-go) | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-002 | no | todo |  |  |  |  |
| REN-004 | Normals, packing and shape tables | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-001, SPEC-04 | no | todo |  |  |  |  |
| REN-005 | Snapshot gathering | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-004, SPEC-10 | no | todo |  |  |  |  |
| REN-006 | LOD0 mesher | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-004 | no | todo |  |  |  |  |
| REN-007 | LOD1 mesher | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-006 | no | todo |  |  |  |  |
| REN-008 | Brick and prop shaders, placeholder art | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-004 | no | todo |  |  |  |  |
| REN-009 | Render config definitions and factory | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | standard | REN-001, REN-008 | no | todo |  |  |  |  |
| REN-010 | BrickMeshBuildSystem | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-005, REN-006, REN-007 | no | todo |  |  |  |  |
| REN-011 | BrickMeshApplySystem, cleanup and statistics | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-010, SPEC-10 | no | todo |  |  |  |  |
| REN-012 | GridLodSystem, Dormant and LOD switching | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-011 | no | todo |  |  |  |  |
| REN-013 | Rendering gate suite | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-012, REN-008 | no | todo |  |  |  |  |
| REN-014 | Prop block instancing | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-011, REN-008 | no | todo |  |  |  |  |
| PHY-001 | Public contract types | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | standard | CORE-001, DATA-001, VOX-001 | yes | todo |  |  |  |  |
| PHY-002 | SPIKE: Unity Physics capabilities for nested voxel compounds | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-001 | no | todo |  |  |  |  |
| PHY-003 | Physics config definition, factory and PhysicsStep | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | standard | PHY-001, SPEC-04 | no | todo |  |  |  |  |
| PHY-004 | Brick box merger | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-001 | no | todo |  |  |  |  |
| PHY-005 | Brick blob builder and grid compound assembler | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-002, PHY-004 | no | todo |  |  |  |  |
| PHY-006 | Mass maths | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-001 | no | todo |  |  |  |  |
| PHY-007 | Pipeline singletons and access discipline | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-001 | no | todo |  |  |  |  |
| PHY-008 | GridBodyInitSystem | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-003, PHY-006, PHY-007, SPEC-10 | no | todo |  |  |  |  |
| PHY-009 | ColliderSnapshotSystem and install schedule | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-007, SPEC-10 | no | todo |  |  |  |  |
| PHY-010 | BrickColliderBuildSystem | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-005, PHY-009 | no | todo |  |  |  |  |
| PHY-011 | ColliderSwapSystem | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-010 | no | todo |  |  |  |  |
| PHY-012 | GridColliderCleanupSystem and teardown | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-011, SPEC-10 | no | todo |  |  |  |  |
| PHY-013 | GridMassUpdateSystem | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-006, PHY-008, DMG-001 | no | todo |  |  |  |  |
| PHY-015 | Debug, statistics and harness hooks | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | standard | PHY-011 | no | todo |  |  |  |  |
| PHY-016 | GridBodyFactory contract test for split creators | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | standard | PHY-008 | no | todo |  |  |  |  |
| PHY-017 | Physics behaviour scenarios | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | standard | PHY-011, PHY-013, SPEC-10 | no | todo |  |  |  |  |
| PHY-018 | Collider and mass determinism across frame rates | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-011, PHY-013 | no | todo |  |  |  |  |
| PHY-019 | Performance gates and memory | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-012, PHY-013, PHY-015, SPEC-10, TST-001 | no | todo |  |  |  |  |
| PHY-020 | Fallback: flat grid compound of boxes (only if PHY-002 is no-go for nested compounds) | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-002 | no | todo |  |  |  |  |
| DMG-001 | Public contract types | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M1 | standard | CORE-001, DATA-001, VOX-001 | yes | todo |  |  |  |  |
| FLT-001 | Public contract types | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | CORE-001, DATA-001, VOX-001 | yes | todo |  |  |  |  |
| FLT-002 | Grid-space and flight maths | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-001, SPEC-04 | no | todo |  |  |  |  |
| FLT-003 | Flight config definition and factory | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-001, SPEC-04 | no | todo |  |  |  |  |
| FLT-004 | Thrust allocator | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-001 | no | todo |  |  |  |  |
| FLT-005 | PlayerCommandApplySystem | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-001, DMG-001 | no | todo |  |  |  |  |
| FLT-006 | Thruster registration and ThrusterInitSystem | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-002, SPEC-10 | no | todo |  |  |  |  |
| FLT-007 | PropulsionSystem | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-002, FLT-004, FLT-006, DMG-001, SPEC-12 | no | todo |  |  |  |  |
| FLT-008 | PlanarConstraintSystem | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-001, SPEC-12 | no | todo |  |  |  |  |
| FLT-009 | Flight behaviour scenarios | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-005, FLT-007, FLT-008, SPEC-10 | no | todo |  |  |  |  |
| FLT-010 | Flight tuning against legacy blueprints | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-009, SPEC-72 | no | todo |  |  |  |  |
| FLT-011 | Flight performance and determinism | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-007, FLT-008, TST-001 | no | todo |  |  |  |  |
| WPN-001 | Public contract types | [SPEC-21](specs/SPEC-21-weapons.md) | M1 | standard | CORE-001, DATA-001, VOX-001, DMG-001, FLT-001 | no | todo |  |  |  |  |
| AI-001 | Public contract types | [SPEC-23](specs/SPEC-23-ai.md) | M1 | standard | CORE-001, DATA-001, VOX-001, WPN-001 | no | todo |  |  |  |  |
| UI-001 | Public contract types | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | standard | CORE-001, INP-001 | no | todo |  |  |  |  |
| UI-002 | Theme skeleton, panel settings and root UXML | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | standard | UI-001 | no | todo |  |  |  |  |
| UI-003 | UIRoot construction, layers and sound hooks | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | frontier | UI-002, CORE-001 | no | todo |  |  |  |  |
| UI-004 | UINavigator stack, pause token and input context | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | frontier | UI-003 | no | todo |  |  |  |  |
| UI-005 | Text entry context and world-pointer gate | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | frontier | UI-004 | no | todo |  |  |  |  |
| UI-006 | MVVM base, converters and number strings | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | frontier | UI-001 | no | todo |  |  |  |  |
| UI-007 | Base controls: buttons, panel, list, inputs, key hint, markers, HUD plates | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | standard | UI-002, UI-006 | no | todo |  |  |  |  |
| CAM-001 | Public contract types | [SPEC-62](specs/SPEC-62-camera.md) | M1 | standard | CORE-001 | yes | todo |  |  |  |  |
| CAM-002 | ADR: custom camera rig instead of Cinemachine | [SPEC-62](specs/SPEC-62-camera.md) | M1 | frontier | — | yes | todo |  |  |  |  |
| CAM-003 | Camera math helpers | [SPEC-62](specs/SPEC-62-camera.md) | M1 | standard | CAM-001 | no | todo |  |  |  |  |
| CAM-004 | Rig service, camera prefab, Menu/Hold modes and the sync system shell | [SPEC-62](specs/SPEC-62-camera.md) | M1 | frontier | CAM-003, FND-001 | no | todo |  |  |  |  |
| CAM-005 | Flight follow mode and camera input | [SPEC-62](specs/SPEC-62-camera.md) | M1 | standard | CAM-004, FLT-001, VOX-001, INP-001 | no | todo |  |  |  |  |
| CAM-011 | Camera performance and framing tests | [SPEC-62](specs/SPEC-62-camera.md) | M1 | standard | CAM-005, TST-001 | no | todo |  |  |  |  |
| VFX-001 | Public contract types | [SPEC-63](specs/SPEC-63-vfx.md) | M1 | standard | CORE-001, DATA-001 | yes | todo |  |  |  |  |
| AUD-004 | Listener regression and scene validation (R5/R7) | [SPEC-64](specs/SPEC-64-audio.md) | M1 | standard | AUD-003, SPEC-01 | no | todo |  |  |  |  |
| AUD-005 | UI sounds, PlayCue and stingers | [SPEC-64](specs/SPEC-64-audio.md) | M1 | standard | AUD-003 | no | todo |  |  |  |  |
| AUD-010 | Audio provenance table and release-build check | [SPEC-64](specs/SPEC-64-audio.md) | M1 | standard | AUD-002 | no | todo |  |  |  |  |
| DEV-007 | PlayMode test that runs every active scenario file | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | standard | DEV-006, SPEC-71 | no | todo |  |  |  |  |
| DEV-008 | newgame and sandbox commands, newgame.sh | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | standard | DEV-006, FND-001, PERS-001 | no | todo |  |  |  |  |
| DEV-009 | teleport, spawn and grid commands; snapshot game sections | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | standard | DEV-008, VOX-001, PHY-001 | no | todo |  |  |  |  |
| DEV-012 | Overlay renderer and voxel overlays (bricks, dirty, colliders) | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | frontier | DEV-009, REN-001, PHY-001 | no | todo |  |  |  |  |
| DEV-015 | In-game dev console | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | standard | DEV-003, UI-001, INP-001 | no | todo |  |  |  |  |
| DEV-016 | record and replay commands | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | standard | DEV-008, SPEC-71 | no | todo |  |  |  |  |
| DEV-019 | Draft regression scenario files | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M1 | standard | DEV-007 | no | todo |  |  |  |  |
| TST-002 | Record and replay | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M1 | standard | TST-001, FLT-001, INP-001 | no | todo |  |  |  |  |
| TST-003 | Game flow fixture and boot test hooks | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M1 | standard | TST-001, SPEC-01, SPEC-05 | no | todo |  |  |  |  |
| TST-004 | Integrated voxel gate suite (moved from SPEC-10 VOX-018) | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M1 | frontier | TST-001, SPEC-10, SPEC-11, SPEC-12 | no | todo |  |  |  |  |
| FND-012 | ShipSpawnService | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M2 | standard | FND-010, WPN-001, FLT-001, AI-001 | no | todo |  |  |  |  |
| DATA-010 | Weapon and ability links on blocks | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M2 | standard | DATA-007, WPN-001, ABL-001 | no | todo |  |  |  |  |
| PHY-014 | SplitGrace contact suppression | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M2 | standard | PHY-002, DMG-001 | no | todo |  |  |  |  |
| DMG-002 | DamageStreams and DamageStreamsSystem | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-001, SPEC-10, SPEC-02 | no | todo |  |  |  |  |
| DMG-003 | Damage config asset and boot hookup | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | standard | DMG-001 | no | todo |  |  |  |  |
| DMG-004 | Cell access, quantization and RemoveCell | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-002 | no | todo |  |  |  |  |
| DMG-005 | Point and blast damage | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-004 | no | todo |  |  |  |  |
| DMG-006 | DamageResolveSystem pipeline | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-005 | no | todo |  |  |  |  |
| DMG-007 | ImpactEventSystem (rams and knockback) | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-006, PHY-001 | no | todo |  |  |  |  |
| DMG-008 | Brick connectivity summaries | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-004 | no | todo |  |  |  |  |
| DMG-009 | IntegritySystem (race on the hybrid graph) | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-006, DMG-008 | no | todo |  |  |  |  |
| DMG-010 | SplitSystem: plan, particle tier, deferred detach and debris FX | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-009, VFX-001, AUD-001 | no | todo |  |  |  |  |
| DMG-011 | Chunk bodies (pool, collider, lifetime) | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-010, SPEC-12 | no | todo |  |  |  |  |
| DMG-012 | Hulk grids from splits | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-011, FND-001, SPEC-12 | no | todo |  |  |  |  |
| DMG-013 | ShipDeathSystem, despawn dissolve and the destroy pass | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-012 | no | todo |  |  |  |  |
| DMG-014 | Determinism hash and frame-rate determinism tests | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-013, TST-001 | no | todo |  |  |  |  |
| DMG-015 | Regression scenarios R25/R26 and soak | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | standard | DMG-014 | no | todo |  |  |  |  |
| DMG-016 | Performance gates | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-014 | no | todo |  |  |  |  |
| DMG-017 | Harness commands | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | standard | DMG-014, DEV-001 | no | todo |  |  |  |  |
| FLT-012 | Power and stat integration (M2) | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M2 | frontier | FLT-007, PWR-001 | no | todo |  |  |  |  |
| WPN-002 | Weapon content: definitions, blob, boot singletons | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-001, SPEC-04 | no | todo |  |  |  |  |
| WPN-003 | CombatHitTest | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-001, SPEC-10, AI-001, PWR-001 | no | todo |  |  |  |  |
| WPN-004 | WeaponInitSystem and mounts | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-002, SPEC-10, SPEC-20 | no | todo |  |  |  |  |
| WPN-005 | PlayerTargetingSystem | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-001, AI-001, SPEC-20 | no | todo |  |  |  |  |
| WPN-006 | WeaponAimSystem and lead | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-004, WPN-005 | no | todo |  |  |  |  |
| WPN-007 | WeaponFireSystem and the projectile store append | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-006, PWR-001 | no | todo |  |  |  |  |
| WPN-008 | ProjectileStepSystem | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-003, WPN-007, PWR-001, SPEC-13 | no | todo |  |  |  |  |
| WPN-009 | BeamStepSystem | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-008 | no | todo |  |  |  |  |
| WPN-010 | ExplosionSystem | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-008 | no | todo |  |  |  |  |
| WPN-011 | CombatGridSetup and turret visuals | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-004, PWR-001, ABL-001 | no | todo |  |  |  |  |
| WPN-012 | Combat scenarios and determinism | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-009, WPN-010, WPN-011, SPEC-13, SPEC-22 | no | todo |  |  |  |  |
| WPN-013 | Weapon performance gates | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-012, TST-001 | no | todo |  |  |  |  |
| WPN-014 | Weapon balance pass | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | frontier | WPN-012, SPEC-72 | no | todo |  |  |  |  |
| WPN-015 | Harness hooks | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-008 | no | todo |  |  |  |  |
| WPN-017 | ShipDestroyed payload and bridge | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-001, DMG-001, CORE-001 | no | todo |  |  |  |  |
| WPN-018 | Weapon power demand and weapon stats | [SPEC-21](specs/SPEC-21-weapons.md) | M2 | standard | WPN-004, WPN-007, PWR-001 | no | todo |  |  |  |  |
| PWR-001 | Public contract types | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | CORE-001, DATA-001, VOX-001, DMG-001 | no | todo |  |  |  |  |
| PWR-002 | Shield maths | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | PWR-001 | no | todo |  |  |  |  |
| PWR-003 | Tuning definition, baker and spawn helper | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | PWR-001, SPEC-04 | no | todo |  |  |  |  |
| PWR-004 | Registration and `PowerInitSystem` | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-003, WPN-001, VOX-001, SPEC-10 | no | todo |  |  |  |  |
| PWR-005 | `PowerAllocationSystem` | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-004, ABL-001 | no | todo |  |  |  |  |
| PWR-006 | `ShipStatsAggregateSystem` | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-004, SPEC-12 | no | todo |  |  |  |  |
| PWR-007 | `ShieldSystem` | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-005, PWR-006, PWR-002, DMG-001, VFX-001, SPEC-02 | no | todo |  |  |  |  |
| PWR-008 | Power budget validator and balance tests | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | PWR-006, SPEC-72, WPN-001 | no | todo |  |  |  |  |
| PWR-009 | Performance tests | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-005, PWR-006, PWR-007 | no | todo |  |  |  |  |
| PWR-010 | `StatusSystem`: inhibits and stat modifiers (ADR-0016) | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | PWR-001, PWR-005, SPEC-02 | no | todo |  |  |  |  |
| AI-002 | SpatialHash queries | [SPEC-23](specs/SPEC-23-ai.md) | M2 | frontier | AI-001 | no | todo |  |  |  |  |
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
| ABL-001 | Public contract types | [SPEC-24](specs/SPEC-24-abilities.md) | M2 | standard | CORE-001, DATA-001, PWR-001 | no | todo |  |  |  |  |
| ABL-002 | Ability content and boot singleton | [SPEC-24](specs/SPEC-24-abilities.md) | M2 | standard | ABL-001, SPEC-04 | no | todo |  |  |  |  |
| ABL-003 | AbilitySystem | [SPEC-24](specs/SPEC-24-abilities.md) | M2 | frontier | ABL-002, SPEC-20, SPEC-22 | no | todo |  |  |  |  |
| ABL-004 | Effect systems | [SPEC-24](specs/SPEC-24-abilities.md) | M2 | standard | ABL-003, SPEC-22 | no | todo |  |  |  |  |
| ABL-005 | Ability scenarios and frame-rate independence | [SPEC-24](specs/SPEC-24-abilities.md) | M2 | standard | ABL-004, SPEC-21 | no | todo |  |  |  |  |
| CAM-008 | Screen shake from FxEvents | [SPEC-62](specs/SPEC-62-camera.md) | M2 | standard | CAM-005, SPEC-63 | no | todo |  |  |  |  |
| VFX-002 | FxRig: slots, effects, buffers and binding | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | frontier | VFX-001, TST-001 | no | todo |  |  |  |  |
| VFX-003 | FxUploadSystem: drain, batch, submit, stats | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | frontier | VFX-002 | no | todo |  |  |  |  |
| VFX-004 | Template graphs and content assets for hits, explosions, muzzle and shield sparks | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-003 | no | todo |  |  |  |  |
| VFX-005 | Voxel debris particles and the 50k GPU gate | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | frontier | VFX-004 | no | todo |  |  |  |  |
| VFX-006 | Projectile tracers | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | frontier | VFX-002, WPN-001, CORE-001 | no | todo |  |  |  |  |
| VFX-007 | Beams clipped at the hit point | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-003, WPN-001 | no | todo |  |  |  |  |
| VFX-008 | Thruster plumes | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-002, FLT-001 | no | todo |  |  |  |  |
| VFX-009 | Shield shells and ripples | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-002, PWR-001, CORE-001 | no | todo |  |  |  |  |
| VFX-010 | Chunk-body dissolve hook | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-002, DMG-001 | no | todo |  |  |  |  |
| VFX-011 | Quality levels and the settings hook | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-003 | no | todo |  |  |  |  |
| VFX-012 | Regression and budget tests (02-C29, GC, upload) | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-005, VFX-006, VFX-007 | no | todo |  |  |  |  |
| AUD-006 | SfxEvent drain: coalescing, cooldown and voice allocation | [SPEC-64](specs/SPEC-64-audio.md) | M2 | frontier | AUD-005, TST-001 | no | todo |  |  |  |  |
| AUD-007 | Cue content, legacy import and the CombatAudioCues singleton | [SPEC-64](specs/SPEC-64-audio.md) | M2 | standard | AUD-006 | no | todo |  |  |  |  |
| AUD-008 | Music director, crossfades and scene contexts | [SPEC-64](specs/SPEC-64-audio.md) | M2 | standard | AUD-006, FND-001 | no | todo |  |  |  |  |
| AUD-009 | Loop voices: player engine and beams | [SPEC-64](specs/SPEC-64-audio.md) | M2 | standard | AUD-006, FLT-001, WPN-001 | no | todo |  |  |  |  |
| AUD-013 | Audio performance and allocation tests | [SPEC-64](specs/SPEC-64-audio.md) | M2 | standard | AUD-008, AUD-009 | no | todo |  |  |  |  |
| DEV-010 | aim and fire commands | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M2 | standard | DEV-009, WPN-001, INP-001, CAM-001 | no | todo |  |  |  |  |
| DEV-011 | perf command and gh_perf extensions | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M2 | standard | DEV-009, VFX-001, AUD-001, TST-001 | no | todo |  |  |  |  |
| DEV-013 | Overlays: islands and spatial hash | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M2 | standard | DEV-012, AI-001 | no | todo |  |  |  |  |
| TST-005 | Determinism runner and the combat determinism gate | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M2 | frontier | TST-002, TST-003, SPEC-13, SPEC-20, SPEC-21, SPEC-22, SPEC-23 | no | todo |  |  |  |  |
| TST-006 | Ten-minute combat soak | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M2 | standard | TST-005, SPEC-70 | no | todo |  |  |  |  |
| MIG-009 | Weapon converter and weapon links | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M2 | standard | MIG-006, MIG-011, WPN-001, SPEC-04 | no | todo |  |  |  |  |
| MIG-010 | Ability converter and ability grants | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M2 | standard | MIG-006, ABL-001, SPEC-04 | no | todo |  |  |  |  |
| MIG-011 | Audio cue converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M2 | standard | MIG-004, AUD-001 | no | todo |  |  |  |  |
| FND-011 | Load flow end to end (R28) | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M3 | frontier | FND-010, SPEC-30, SPEC-31 | no | todo |  |  |  |  |
| FND-013 | Blueprint Lab entry and exit | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M3 | frontier | FND-011, EDT-001 | no | todo |  |  |  |  |
| FND-015 | Boot from any scene and the defeat flow | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M3 | standard | FND-011, HUD-001 | no | todo |  |  |  |  |
| PERS-013 | Regression scenario R28: load from the pause menu | [SPEC-05](specs/SPEC-05-persistence.md) | M3 | frontier | PERS-009, PERS-010, SPEC-01, SPEC-20, SPEC-31, SPEC-61, TST-001 | no | todo |  |  |  |  |
| PERS-014 | Regression scenario 04-B1: load after a content change (end to end) | [SPEC-05](specs/SPEC-05-persistence.md) | M3 | standard | PERS-010, SPEC-01, SPEC-50, SPEC-61, TST-001 | no | todo |  |  |  |  |
| PERS-015 | Full-world round trip with all real participants | [SPEC-05](specs/SPEC-05-persistence.md) | M3 | frontier | PERS-011, PERS-012, SPEC-01, SPEC-30, SPEC-31, SPEC-33, SPEC-40, SPEC-41, SPEC-42, SPEC-50 | no | todo |  |  |  |  |
| VOX-020 | Incremental grid edits for the editor preview | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M3 | frontier | VOX-014 | no | todo |  |  |  |  |
| PHY-022 | Mass of restored cells (ADR-0016) | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M3 | standard | PHY-013, DMG-001 | no | todo |  |  |  |  |
| DMG-019 | Impulse and restore channels (ADR-0016) | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M3 | frontier | DMG-006, DMG-007, SPEC-02 | no | todo |  |  |  |  |
| AI-013 | Fleet data and formations | [SPEC-23](specs/SPEC-23-ai.md) | M3 | standard | AI-003 | no | todo |  |  |  |  |
| AI-014 | FleetSystem and member thinking | [SPEC-23](specs/SPEC-23-ai.md) | M3 | frontier | AI-013, AI-007 | no | todo |  |  |  |  |
| AI-015 | Fleet and swarm scenario and performance | [SPEC-23](specs/SPEC-23-ai.md) | M3 | standard | AI-014 | no | todo |  |  |  |  |
| GAL-001 | Public contract types | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M3 | standard | CORE-001, DATA-001, VOX-001, NAR-001 | no | todo |  |  |  |  |
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
| SYS-001 | Public contract types | [SPEC-31](specs/SPEC-31-star-system-streaming.md) | M3 | standard | CORE-001, DATA-001, GAL-001, WPN-001 | no | todo |  |  |  |  |
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
| NAR-001 | Public contract types | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | standard | CORE-001, DATA-001 | yes | todo |  |  |  |  |
| NAR-002 | Lexer and parser | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-001 | no | todo |  |  |  |  |
| NAR-003 | Type checker, registry, compiler and binder | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-002 | no | todo |  |  |  |  |
| NAR-004 | Condition and expression nodes | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | standard | NAR-003 | no | todo |  |  |  |  |
| NAR-005 | Effect nodes and atomic effect lists | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-004 | no | todo |  |  |  |  |
| NAR-006 | Fact catalog and reference resolvers | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | frontier | NAR-001 | no | todo |  |  |  |  |
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
| DLG-001 | Public contract types | [SPEC-41](specs/SPEC-41-dialog.md) | M3 | standard | NAR-001, UI-001 | no | todo |  |  |  |  |
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
| EDT-001 | Public contract types | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | standard | CORE-001, DATA-001, VOX-001, ECO-001, UI-001, HUD-001 | no | todo |  |  |  |  |
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
| UI-008 | Meter, Slot and TabBar controls | [SPEC-60](specs/SPEC-60-ui-framework.md) | M3 | standard | UI-007 | no | todo |  |  |  |  |
| UI-009 | Tooltip service | [SPEC-60](specs/SPEC-60-ui-framework.md) | M3 | standard | UI-008 | no | todo |  |  |  |  |
| UI-010 | Notification service | [SPEC-60](specs/SPEC-60-ui-framework.md) | M3 | standard | UI-008 | no | todo |  |  |  |  |
| UI-011 | Confirm dialog and text prompt | [SPEC-60](specs/SPEC-60-ui-framework.md) | M3 | standard | UI-005, UI-008 | no | todo |  |  |  |  |
| UI-012 | Layout, contrast and allegiance-colour audits | [SPEC-60](specs/SPEC-60-ui-framework.md) | M3 | frontier | UI-008 | no | todo |  |  |  |  |
| UI-013 | Accessibility options | [SPEC-60](specs/SPEC-60-ui-framework.md) | M3 | standard | UI-010, UI-012 | no | todo |  |  |  |  |
| HUD-001 | Public contract types | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | UI-001, CORE-001, CAM-001 | no | todo |  |  |  |  |
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
| CAM-007 | Occlusion pull-in | [SPEC-62](specs/SPEC-62-camera.md) | M3 | frontier | CAM-005, PHY-001 | no | todo |  |  |  |  |
| CAM-009 | Editor orbit mode | [SPEC-62](specs/SPEC-62-camera.md) | M3 | standard | CAM-005 | no | todo |  |  |  |  |
| CAM-010 | Map view transform | [SPEC-62](specs/SPEC-62-camera.md) | M3 | standard | CAM-001 | no | todo |  |  |  |  |
| VFX-013 | Floating-origin shift and clears | [SPEC-63](specs/SPEC-63-vfx.md) | M3 | frontier | VFX-003, GAL-001, SYS-001, FND-001 | no | todo |  |  |  |  |
| VFX-014 | Warp effect and strict config validation | [SPEC-63](specs/SPEC-63-vfx.md) | M3 | standard | VFX-004, TRV-001 | no | todo |  |  |  |  |
| AUD-011 | GATE: Owner confirms licences of the carried-over audio | [SPEC-64](specs/SPEC-64-audio.md) | M3 | standard | AUD-010 | no | todo |  |  |  |  |
| AUD-012 | Floating-origin shift and clears | [SPEC-64](specs/SPEC-64-audio.md) | M3 | frontier | AUD-006, GAL-001, SYS-001 | no | todo |  |  |  |  |
| DEV-014 | interact command and interaction overlay | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M3 | standard | DEV-009, SYS-001, INP-001 | no | todo |  |  |  |  |
| DEV-017 | inv, give and facts commands | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M3 | standard | DEV-009, ECO-001, NAR-001 | no | todo |  |  |  |  |
| DEV-018 | save, load and jump commands; toworld.sh | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M3 | standard | DEV-008, PERS-001, TRV-001, HUD-001 | no | todo |  |  |  |  |
| TST-007 | Whole-game frame budgets | [SPEC-71](specs/SPEC-71-testing-perf-gates.md) | M3 | frontier | TST-003, SPEC-31, SPEC-33, SPEC-61, SPEC-21 | no | todo |  |  |  |  |
| MIG-012 | Faction converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M3 | standard | MIG-008, ENC-001 | no | todo |  |  |  |  |
| MIG-013 | Loot table converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M3 | standard | MIG-008, ECO-001 | no | todo |  |  |  |  |
| MIG-014 | Body template stubs | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M3 | standard | MIG-004, GAL-001 | no | todo |  |  |  |  |
| MIG-015 | Hand-off package | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M3 | standard | MIG-002, MIG-003 | no | todo |  |  |  |  |
| INP-008 | Gamepad scheme | [SPEC-03](specs/SPEC-03-input.md) | M4 | standard | INP-003 | no | todo |  |  |  |  |
| REN-015 | LOD2 grid proxies | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M4 | frontier | REN-012 | no | todo |  |  |  |  |
| PHY-021 | Region compounds for very large grids (only if PHY-019 shows assembly over budget) | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M4 | frontier | PHY-019 | no | todo |  |  |  |  |
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
| ART-003 | Mood boards per direction | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | — | yes | review | claude (claude-opus-5-5) | 2026-10-10 | a3d0d92 | Needs an independent frontier review (no subagents this session). 13 public-domain images added (owner-approved) |
| ART-004 | Block-kit style test: five blocks per direction | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-002 | yes | review | claude (claude-opus-5-5) | 2026-10-10 | 5cebb6b | Needs independent frontier review and a second reviewer for the 150 m glyph test (criterion 3; likely fails for 1-cell-wide blocks, see log) |
| ART-005 | Interactive HTML design board | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-002, ART-003, ART-004 | no | review | claude (claude-opus-5-5) | 2026-10-10 | a6b2fe2 | Needs independent frontier review; Safari file:// check pending; ready for the owner's ART-006 shortlist |
| ART-006 | Owner shortlist of finalist directions | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | standard | ART-005 | no | done | frank (human) | 2026-10-11 | 6a64d71 | Owner chose one direction, NOCTURNE (2026-10-11), instead of two finalists |
| ART-007 | Brick material baker, preview mesher, reference shader and art validator | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-001 | yes | in-progress | claude (claude-opus-5-5) | 2026-10-10 |  | Baker, preview mesher and SH_BrickReference done on branch task/ART-007-brick-tools (083c482); ArtDatabaseValidator waits for CORE-002, DATA-004, DATA-005 (owner chose ART-004 first) |
| ART-008 | Look-test content for each finalist | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-004, ART-006, ART-007 | no | todo |  | 2026-10-11 |  | Re-scoped by the gate approval 2026-10-11: runs after the renderer exists (M1), as a check, not a gate |
| ART-009 | Look-test scenes with HUD mock | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-002, ART-008 | no | todo |  | 2026-10-11 |  | Re-scoped 2026-10-11: after the renderer exists, as a check |
| ART-010 | Look-test capture pack and metrics | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-009 | no | todo |  | 2026-10-11 |  | Re-scoped 2026-10-11: after the renderer exists, as a check |
| ART-011 | Owner look-test review and one revision round | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-010 | no | todo |  | 2026-10-11 |  | Re-scoped 2026-10-11: after the renderer exists, as a check |
| ART-012 | Finalize the design system for approval | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-011 | no | review | claude (claude-opus-5-5) | 2026-10-11 | d60cdf7 | Replaced by DESIGN-GUIDE.md (d60cdf7); golden captures wait for the in-engine look test. Needs independent frontier review |
| ART-013 | GATE: Owner approves design system | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | standard | ART-012 | no | done | frank (human) | 2026-10-11 | 6a64d71 | Approved 2026-10-11: NOCTURNE as DESIGN-GUIDE.md. Owner: "yes I approve" |
| AST-001 | Public contract types: asset manifest, naming rules and licence register | [SPEC-81](specs/SPEC-81-asset-production.md) | A0 | standard | ART-001 | yes | todo |  |  |  |  |
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

## Measurements

Record perf gate measurements here (date, task, scenario, numbers, machine).

| Date | Task | Gate | Result | Notes |
|---|---|---|---|---|

## Log

Newest first. One entry per finished task or notable event: what changed, measurements, follow-ups.

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
