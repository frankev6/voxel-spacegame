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
| M0 | 62 | 0 | 0 | 0 | 0 | 0 | 62 | 2 |
| M1 | 97 | 0 | 0 | 0 | 0 | 0 | 97 | 1 |
| M2 | 52 | 0 | 0 | 0 | 0 | 0 | 52 | 0 |
| M3 | 116 | 0 | 0 | 0 | 0 | 0 | 116 | 0 |
| M4 | 9 | 0 | 0 | 0 | 0 | 0 | 9 | 0 |
| A0 | 16 | 0 | 0 | 0 | 0 | 0 | 16 | 1 |
| A1 | 30 | 0 | 0 | 0 | 0 | 0 | 30 | 0 |
| A2 | 3 | 0 | 0 | 0 | 0 | 0 | 3 | 0 |
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
| FND-001 | Public contract types | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | standard | CORE-001, DATA-001 | no | todo |  |  |  |  |
| FND-002 | Bootstrap, GameRoot and explicit World creation | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | frontier | FND-001 | no | todo |  |  |  |  |
| FND-003 | PauseService | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | standard | FND-001 | no | todo |  |  |  |  |
| FND-004 | SettingsService and SettingsApplier | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | standard | FND-001, PERS-001 | no | todo |  |  |  |  |
| FND-005 | Composition root and BootSingletons | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | frontier | FND-002, FND-003, FND-004, SPEC-02, SPEC-04 | no | todo |  |  |  |  |
| FND-006 | SceneFlow transitions and teardown | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | frontier | FND-005 | no | todo |  |  |  |  |
| FND-007 | SimClock lifecycle and SimClockAdvanceSystem | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | frontier | FND-006 | no | todo |  |  |  |  |
| FND-014 | Static state audit and Play Mode re-entry | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M0 | standard | FND-006 | no | todo |  |  |  |  |
| CORE-001 | Public contract types | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | standard | — | yes | todo |  |  |  |  |
| CORE-002 | StableHash and content identity | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-001 | no | todo |  |  |  |  |
| CORE-003 | Random streams and fixed-step time | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-002 | no | todo |  |  |  |  |
| CORE-004 | GameLog | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | standard | CORE-001 | no | todo |  |  |  |  |
| CORE-005 | Services locator | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-001 | no | todo |  |  |  |  |
| CORE-006 | EventBus and EventHub | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-004 | no | todo |  |  |  |  |
| CORE-007 | Binary save primitives | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-002 | no | todo |  |  |  |  |
| CORE-008 | System groups and pipeline order | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-001 | no | todo |  |  |  |  |
| CORE-009 | ECS→managed event bridge | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | frontier | CORE-006, CORE-008 | no | todo |  |  |  |  |
| CORE-010 | Math helpers | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | standard | CORE-001 | no | todo |  |  |  |  |
| CORE-011 | Settings model | [SPEC-02](specs/SPEC-02-core-utilities.md) | M0 | standard | CORE-001 | no | todo |  |  |  |  |
| INP-001 | Public contract types and the actions asset | [SPEC-03](specs/SPEC-03-input.md) | M0 | standard | CORE-001 | no | todo |  |  |  |  |
| INP-002 | InputService contexts and display strings | [SPEC-03](specs/SPEC-03-input.md) | M0 | standard | INP-001 | no | todo |  |  |  |  |
| INP-003 | Rebinding and overrides | [SPEC-03](specs/SPEC-03-input.md) | M0 | standard | INP-002 | no | todo |  |  |  |  |
| DATA-001 | Public contract types | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | CORE-001 | no | todo |  |  |  |  |
| DATA-002 | ValidationReport implementation | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | DATA-001 | no | todo |  |  |  |  |
| DATA-003 | Orientation tables | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | frontier | DATA-001 | no | todo |  |  |  |  |
| DATA-004 | Definition self-validation and ID rules | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | DATA-002 | no | todo |  |  |  |  |
| DATA-005 | ContentCatalog | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | frontier | DATA-004 | no | todo |  |  |  |  |
| DATA-006 | Block blob builder and placement maths | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | frontier | DATA-003, DATA-005 | no | todo |  |  |  |  |
| DATA-007 | ContentService and ECS singletons | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | frontier | DATA-006 | no | todo |  |  |  |  |
| DATA-008 | Editor database builder, postprocessor, validator runner and CLI | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | DATA-005 | no | todo |  |  |  |  |
| DATA-009 | System placeholder content and the database asset | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M0 | standard | DATA-008 | no | todo |  |  |  |  |
| PERS-001 | Public contract types | [SPEC-05](specs/SPEC-05-persistence.md) | M0 | standard | CORE-001, DATA-001, VOX-001 | no | todo |  |  |  |  |
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
| AUD-001 | Public contract types | [SPEC-64](specs/SPEC-64-audio.md) | M0 | standard | CORE-001, DATA-001 | no | todo |  |  |  |  |
| AUD-002 | Mixer asset, voice settings and import rules | [SPEC-64](specs/SPEC-64-audio.md) | M0 | standard | AUD-001 | no | todo |  |  |  |  |
| AUD-003 | AudioService core: root, single listener, voice pools, volumes, pause | [SPEC-64](specs/SPEC-64-audio.md) | M0 | frontier | AUD-002 | no | todo |  |  |  |  |
| DEV-001 | Public contract types | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | standard | CORE-001 | no | todo |  |  |  |  |
| DEV-002 | Verify and document the as-built generic harness | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | standard | — | yes | todo |  |  |  |  |
| DEV-003 | DevRuntimeHost, command registry, operations, help/op/until | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | frontier | DEV-001, FND-001 | no | todo |  |  |  |  |
| DEV-004 | Runtime input injection and UI query | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | standard | DEV-003 | no | todo |  |  |  |  |
| DEV-005 | Scenario language: parser, expressions and built-in probes | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | frontier | DEV-003 | no | todo |  |  |  |  |
| DEV-006 | Scenario runner, results JSON and CLI/scripts | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | frontier | DEV-004, DEV-005 | no | todo |  |  |  |  |
| DEV-020 | Release-strip guarantee | [SPEC-70](specs/SPEC-70-devtools-harness.md) | M0 | standard | DEV-003 | no | todo |  |  |  |  |
| MIG-001 | Public contract types | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M0 | standard | DATA-001 | no | todo |  |  |  |  |
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
| INP-005 | PlayerInputSampleSystem | [SPEC-03](specs/SPEC-03-input.md) | M1 | frontier | INP-002, FLT-001, CAM-001 | no | todo |  |  |  |  |
| INP-006 | Camera and editor samplers | [SPEC-03](specs/SPEC-03-input.md) | M1 | standard | INP-002, CAM-001 | no | todo |  |  |  |  |
| INP-007 | Frame-rate independence test | [SPEC-03](specs/SPEC-03-input.md) | M1 | standard | INP-005, SPEC-20 | no | todo |  |  |  |  |
| DATA-011 | v2 block additions (reactor, cargo) | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M1 | standard | DATA-009 | no | todo |  |  |  |  |
| VOX-001 | Public contract types | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | standard | CORE-001, DATA-001 | no | todo |  |  |  |  |
| VOX-002 | SPIKE: memory model for the reference and thin-shell stations | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-001 | no | todo |  |  |  |  |
| VOX-003 | Sparse-brick fallback (only if VOX-002 is no-go) | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-002 | no | todo |  |  |  |  |
| VOX-004 | Brick maths and mask operations | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-001 | no | todo |  |  |  |  |
| VOX-005 | VoxelStructure storage core | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-004 | no | todo |  |  |  |  |
| VOX-006 | Block placement, removal and compaction | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-005, SPEC-04 | no | todo |  |  |  |  |
| VOX-007 | Clone, diff, block enumeration and mass | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-006 | no | todo |  |  |  |  |
| VOX-008 | Grid-space DDA raycast | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-005 | no | todo |  |  |  |  |
| VOX-009 | VOXS codec | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-007, PERS-001 | no | todo |  |  |  |  |
| VOX-010 | Voxel definitions and config factory | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | standard | VOX-001 | no | todo |  |  |  |  |
| VOX-011 | Structure registry and BlueprintLibrary | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-009, VOX-010 | no | todo |  |  |  |  |
| VOX-012 | ECS cell write and read helpers | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-004 | no | todo |  |  |  |  |
| VOX-013 | GridSpawnSystem: spawn path and singletons | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-006, VOX-011, VOX-012, REN-001, PHY-001, SPEC-71 | no | todo |  |  |  |  |
| VOX-014 | Rebuild, despawn and write-back | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-013 | no | todo |  |  |  |  |
| VOX-015 | BrickDirtyScheduleSystem | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-012, REN-001, PHY-001 | no | todo |  |  |  |  |
| VOX-016 | DeferredDisposalSystem | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-013 | no | todo |  |  |  |  |
| VOX-017 | Benchmark structures | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | standard | VOX-005 | no | todo |  |  |  |  |
| VOX-018 | Integrated voxel gate suite | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | frontier | VOX-014, VOX-015, VOX-016, VOX-017, SPEC-11, SPEC-12 | no | todo |  |  |  |  |
| VOX-019 | Damage persistence scenario | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M1 | standard | VOX-014 | no | todo |  |  |  |  |
| REN-001 | Public contract types | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | standard | VOX-001, DATA-001 | no | todo |  |  |  |  |
| REN-002 | SPIKE: Entities Graphics with thousands of runtime brick meshes | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-001, SPEC-10 | no | todo |  |  |  |  |
| REN-003 | Fallback renderer (only if REN-002 is no-go) | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-002 | no | todo |  |  |  |  |
| REN-004 | Normals, packing and shape tables | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-001, SPEC-04 | no | todo |  |  |  |  |
| REN-005 | Snapshot gathering | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-004, VOX-013 | no | todo |  |  |  |  |
| REN-006 | LOD0 mesher | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-004 | no | todo |  |  |  |  |
| REN-007 | LOD1 mesher | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-006 | no | todo |  |  |  |  |
| REN-008 | Brick and prop shaders, placeholder art | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-004 | no | todo |  |  |  |  |
| REN-009 | Render config definitions and factory | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | standard | REN-001, REN-008 | no | todo |  |  |  |  |
| REN-010 | BrickMeshBuildSystem | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-005, REN-006, REN-007 | no | todo |  |  |  |  |
| REN-011 | BrickMeshApplySystem, cleanup and statistics | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-010, VOX-016 | no | todo |  |  |  |  |
| REN-012 | GridLodSystem, Dormant and LOD switching | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-011 | no | todo |  |  |  |  |
| REN-013 | Rendering gate suite | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-012, REN-008 | no | todo |  |  |  |  |
| REN-014 | Prop block instancing | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M1 | frontier | REN-011, REN-008 | no | todo |  |  |  |  |
| PHY-001 | Public contract types | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | standard | CORE-001, DATA-001, VOX-001 | no | todo |  |  |  |  |
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
| PHY-019 | Performance gates and memory | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-012, PHY-013, PHY-015, SPEC-10, SPEC-71 | no | todo |  |  |  |  |
| PHY-020 | Fallback: flat grid compound of boxes (only if PHY-002 is no-go for nested compounds) | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M1 | frontier | PHY-002 | no | todo |  |  |  |  |
| DMG-001 | Public contract types | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M1 | standard | CORE-001, DATA-001, VOX-001 | no | todo |  |  |  |  |
| FLT-001 | Public contract types | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | CORE-001, DATA-001, VOX-001 | no | todo |  |  |  |  |
| FLT-002 | Grid-space and flight maths | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-001, SPEC-04 | no | todo |  |  |  |  |
| FLT-003 | Flight config definition and factory | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-001, SPEC-04 | no | todo |  |  |  |  |
| FLT-004 | Thrust allocator | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-001 | no | todo |  |  |  |  |
| FLT-005 | PlayerCommandApplySystem | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-001, DMG-001 | no | todo |  |  |  |  |
| FLT-006 | Thruster registration and ThrusterInitSystem | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-002, SPEC-10 | no | todo |  |  |  |  |
| FLT-007 | PropulsionSystem | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-002, FLT-004, FLT-006, DMG-001, SPEC-12 | no | todo |  |  |  |  |
| FLT-008 | PlanarConstraintSystem | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-001, SPEC-12 | no | todo |  |  |  |  |
| FLT-009 | Flight behaviour scenarios | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | standard | FLT-005, FLT-007, FLT-008, SPEC-10 | no | todo |  |  |  |  |
| FLT-010 | Flight tuning against legacy blueprints | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-009, SPEC-72 | no | todo |  |  |  |  |
| FLT-011 | Flight performance and determinism | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M1 | frontier | FLT-007, FLT-008, SPEC-71 | no | todo |  |  |  |  |
| UI-001 | Public contract types | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | standard | CORE-001, INP-001 | no | todo |  |  |  |  |
| UI-002 | Theme skeleton, panel settings and root UXML | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | standard | UI-001 | no | todo |  |  |  |  |
| UI-003 | UIRoot construction, layers and sound hooks | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | frontier | UI-002, CORE-001 | no | todo |  |  |  |  |
| UI-004 | UINavigator stack, pause token and input context | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | frontier | UI-003 | no | todo |  |  |  |  |
| UI-005 | Text entry context and world-pointer gate | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | frontier | UI-004 | no | todo |  |  |  |  |
| UI-006 | MVVM base, converters and number strings | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | frontier | UI-001 | no | todo |  |  |  |  |
| UI-007 | Base controls: buttons, panel, list, inputs, key hint, markers, HUD plates | [SPEC-60](specs/SPEC-60-ui-framework.md) | M1 | standard | UI-002, UI-006 | no | todo |  |  |  |  |
| CAM-001 | Public contract types | [SPEC-62](specs/SPEC-62-camera.md) | M1 | standard | CORE-001 | no | todo |  |  |  |  |
| CAM-002 | ADR: custom camera rig instead of Cinemachine | [SPEC-62](specs/SPEC-62-camera.md) | M1 | frontier | — | yes | todo |  |  |  |  |
| CAM-003 | Camera math helpers | [SPEC-62](specs/SPEC-62-camera.md) | M1 | standard | CAM-001 | no | todo |  |  |  |  |
| CAM-004 | Rig service, camera prefab, Menu/Hold modes and the sync system shell | [SPEC-62](specs/SPEC-62-camera.md) | M1 | frontier | CAM-003, FND-001 | no | todo |  |  |  |  |
| CAM-005 | Flight follow mode and camera input | [SPEC-62](specs/SPEC-62-camera.md) | M1 | standard | CAM-004, FLT-001, VOX-001, INP-001 | no | todo |  |  |  |  |
| CAM-011 | Camera performance and framing tests | [SPEC-62](specs/SPEC-62-camera.md) | M1 | standard | CAM-005, TST-001 | no | todo |  |  |  |  |
| VFX-001 | Public contract types | [SPEC-63](specs/SPEC-63-vfx.md) | M1 | standard | CORE-001, DATA-001 | no | todo |  |  |  |  |
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
| FND-012 | ShipSpawnService | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M2 | standard | FND-010, WPN-001, FLT-001, AI-001 | no | todo |  |  |  |  |
| DATA-010 | Weapon and ability links on blocks | [SPEC-04](specs/SPEC-04-content-and-blocks.md) | M2 | standard | DATA-007, WPN-001, ABL-001 | no | todo |  |  |  |  |
| PHY-014 | SplitGrace contact suppression | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M2 | standard | PHY-002, DMG-001 | no | todo |  |  |  |  |
| DMG-002 | DamageStreams and DamageStreamsSystem | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-001, SPEC-10 | no | todo |  |  |  |  |
| DMG-003 | Damage config asset and boot hookup | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | standard | DMG-001 | no | todo |  |  |  |  |
| DMG-004 | Cell access, quantization and RemoveCell | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-002 | no | todo |  |  |  |  |
| DMG-005 | Point and blast damage | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-004 | no | todo |  |  |  |  |
| DMG-006 | DamageResolveSystem pipeline | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-005 | no | todo |  |  |  |  |
| DMG-007 | ImpactEventSystem (rams and knockback) | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-006, PHY-001 | no | todo |  |  |  |  |
| DMG-008 | Brick connectivity summaries | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-004 | no | todo |  |  |  |  |
| DMG-009 | IntegritySystem (race on the hybrid graph) | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-006, DMG-008 | no | todo |  |  |  |  |
| DMG-010 | SplitSystem: plan, particle tier, deferred detach and debris FX | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-009, VFX-001, AUD-001 | no | todo |  |  |  |  |
| DMG-011 | Chunk bodies (pool, collider, lifetime) | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-010, SPEC-12 | no | todo |  |  |  |  |
| DMG-012 | Hulk grids from splits | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-011, FND-001 | no | todo |  |  |  |  |
| DMG-013 | ShipDeathSystem, despawn dissolve and the destroy pass | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-012 | no | todo |  |  |  |  |
| DMG-014 | Determinism hash and frame-rate determinism tests | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-013, SPEC-71 | no | todo |  |  |  |  |
| DMG-015 | Regression scenarios R25/R26 and soak | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | standard | DMG-014 | no | todo |  |  |  |  |
| DMG-016 | Performance gates | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | frontier | DMG-014 | no | todo |  |  |  |  |
| DMG-017 | Harness commands | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M2 | standard | DMG-014, DEV-001 | no | todo |  |  |  |  |
| FLT-012 | Power and stat integration (M2) | [SPEC-20](specs/SPEC-20-flight-propulsion.md) | M2 | frontier | FLT-007, PWR-001 | no | todo |  |  |  |  |
| PWR-001 | Public contract types | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | CORE-001, DATA-001, VOX-001, DMG-001 | no | todo |  |  |  |  |
| PWR-002 | Shield maths | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | PWR-001 | no | todo |  |  |  |  |
| PWR-003 | Tuning definition, baker and spawn helper | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | PWR-001, SPEC-04 | no | todo |  |  |  |  |
| PWR-004 | Registration and `PowerInitSystem` | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-003, WPN-001, VOX-001, SPEC-10 | no | todo |  |  |  |  |
| PWR-005 | `PowerAllocationSystem` | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-004, FLT-001, ABL-001 | no | todo |  |  |  |  |
| PWR-006 | `ShipStatsAggregateSystem` | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-004, WPN-001, SPEC-12 | no | todo |  |  |  |  |
| PWR-007 | `ShieldSystem` | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-005, PWR-006, PWR-002, DMG-001, VFX-001 | no | todo |  |  |  |  |
| PWR-008 | Power budget validator and balance tests | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | standard | PWR-006, SPEC-72, SPEC-21 | no | todo |  |  |  |  |
| PWR-009 | Performance tests | [SPEC-22](specs/SPEC-22-power-shields-stats.md) | M2 | frontier | PWR-005, PWR-006, PWR-007 | no | todo |  |  |  |  |
| CAM-008 | Screen shake from FxEvents | [SPEC-62](specs/SPEC-62-camera.md) | M2 | standard | CAM-005, SPEC-63 | no | todo |  |  |  |  |
| VFX-002 | FxRig: slots, effects, buffers and binding | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | frontier | VFX-001, TST-001 | no | todo |  |  |  |  |
| VFX-003 | FxUploadSystem: drain, batch, submit, stats | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | frontier | VFX-002 | no | todo |  |  |  |  |
| VFX-004 | Template graphs and content assets for hits, explosions, muzzle and shield sparks | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | standard | VFX-003 | no | todo |  |  |  |  |
| VFX-005 | Voxel debris particles and the 50k GPU gate | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | frontier | VFX-004 | no | todo |  |  |  |  |
| VFX-006 | Projectile tracers | [SPEC-63](specs/SPEC-63-vfx.md) | M2 | frontier | VFX-002, WPN-001 | no | todo |  |  |  |  |
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
| MIG-009 | Weapon converter and weapon links | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M2 | standard | MIG-006, MIG-011, WPN-001, SPEC-04 | no | todo |  |  |  |  |
| MIG-010 | Ability converter and ability grants | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M2 | standard | MIG-006, ABL-001, SPEC-04 | no | todo |  |  |  |  |
| MIG-011 | Audio cue converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M2 | standard | MIG-004, AUD-001 | no | todo |  |  |  |  |
| FND-011 | Load flow end to end (R28) | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M3 | frontier | FND-010, SPEC-30, SPEC-31 | no | todo |  |  |  |  |
| FND-013 | Blueprint Lab entry and exit | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M3 | frontier | FND-011, EDT-001 | no | todo |  |  |  |  |
| FND-015 | Boot from any scene and the defeat flow | [SPEC-01](specs/SPEC-01-boot-services-sceneflow.md) | M3 | standard | FND-011, HUD-001 | no | todo |  |  |  |  |
| PERS-013 | Regression scenario R28: load from the pause menu | [SPEC-05](specs/SPEC-05-persistence.md) | M3 | frontier | PERS-009, PERS-010, SPEC-01, SPEC-20, SPEC-31, SPEC-61, SPEC-71 | no | todo |  |  |  |  |
| PERS-014 | Regression scenario 04-B1: load after a content change (end to end) | [SPEC-05](specs/SPEC-05-persistence.md) | M3 | standard | PERS-010, SPEC-01, SPEC-50, SPEC-61, SPEC-71 | no | todo |  |  |  |  |
| PERS-015 | Full-world round trip with all real participants | [SPEC-05](specs/SPEC-05-persistence.md) | M3 | frontier | PERS-011, PERS-012, SPEC-01, SPEC-30, SPEC-31, SPEC-33, SPEC-40, SPEC-41, SPEC-42, SPEC-50 | no | todo |  |  |  |  |
| VOX-020 | Incremental grid edits for the editor preview | [SPEC-10](specs/SPEC-10-voxel-data-model.md) | M3 | frontier | VOX-014 | no | todo |  |  |  |  |
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
| NAR-001 | Public contract types | [SPEC-40](specs/SPEC-40-narrative-core.md) | M3 | standard | CORE-001, DATA-001 | no | todo |  |  |  |  |
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
| EDT-021 | Editor UI panels | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | standard | EDT-001, SPEC-60, HUD-018 | no | todo |  |  |  |  |
| EDT-022 | Editor host, input, presenter and leave prompt | [SPEC-51](specs/SPEC-51-ship-editor.md) | M3 | frontier | EDT-011, EDT-015, EDT-019, EDT-021, CAM-009, INP-001 | no | todo |  |  |  |  |
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
| HUD-009 | HudSyncSystem | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | frontier | HUD-008, HUD-007 | no | todo |  |  |  |  |
| HUD-010 | HUD presenters and screen hotkeys | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-009, QST-001, ECO-001, INP-001 | no | todo |  |  |  |  |
| HUD-011 | Inventory screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002, SPEC-50 | no | todo |  |  |  |  |
| HUD-012 | Merchant screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002, SPEC-50 | no | todo |  |  |  |  |
| HUD-013 | Dialog host screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002 | no | todo |  |  |  |  |
| HUD-014 | Ship log screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002, SPEC-42 | no | todo |  |  |  |  |
| HUD-015 | MapCanvas control | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | frontier | HUD-001, CAM-010 | no | todo |  |  |  |  |
| HUD-016 | Galaxy map and star-system map screens | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-015, SPEC-30, SPEC-31 | no | todo |  |  |  |  |
| HUD-017 | Fleet screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002, EDT-001 | no | todo |  |  |  |  |
| HUD-018 | Ship editor host screen | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-002, EDT-001 | no | todo |  |  |  |  |
| HUD-019 | Hulk and loading screens | [SPEC-61](specs/SPEC-61-screens-hud.md) | M3 | standard | HUD-010 | no | todo |  |  |  |  |
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
| MIG-012 | Faction converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M3 | standard | MIG-008, ENC-001 | no | todo |  |  |  |  |
| MIG-013 | Loot table converter | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M3 | standard | MIG-008, ECO-001 | no | todo |  |  |  |  |
| MIG-014 | Body template stubs | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M3 | standard | MIG-004, GAL-001 | no | todo |  |  |  |  |
| MIG-015 | Hand-off package | [SPEC-72](specs/SPEC-72-legacy-migration.md) | M3 | standard | MIG-002, MIG-003 | no | todo |  |  |  |  |
| INP-008 | Gamepad scheme | [SPEC-03](specs/SPEC-03-input.md) | M4 | standard | INP-003 | no | todo |  |  |  |  |
| REN-015 | LOD2 grid proxies | [SPEC-11](specs/SPEC-11-voxel-rendering.md) | M4 | frontier | REN-012 | no | todo |  |  |  |  |
| PHY-021 | Region compounds for very large grids (only if PHY-019 shows assembly over budget) | [SPEC-12](specs/SPEC-12-voxel-physics.md) | M4 | frontier | PHY-019 | no | todo |  |  |  |  |
| DMG-018 | M4: per-brick component labels for >1M-cell stations | [SPEC-13](specs/SPEC-13-damage-destruction.md) | M4 | frontier | DMG-016 | no | todo |  |  |  |  |
| GAL-014 | Brick-level structure diffs | [SPEC-30](specs/SPEC-30-galaxy-generation.md) | M4 | frontier | GAL-010 | no | todo |  |  |  |  |
| NAR-018 | Narrative Debugger editing tools | [SPEC-40](specs/SPEC-40-narrative-core.md) | M4 | standard | NAR-017 | no | todo |  |  |  |  |
| DLG-013 | Line-ID export for localization | [SPEC-41](specs/SPEC-41-dialog.md) | M4 | standard | DLG-003 | no | todo |  |  |  |  |
| EDT-025 | Radial symmetry | [SPEC-51](specs/SPEC-51-ship-editor.md) | M4 | frontier | EDT-013 | no | todo |  |  |  |  |
| EDT-026 | In-world edit when docked | [SPEC-51](specs/SPEC-51-ship-editor.md) | M4 | frontier | EDT-023, SPEC-10, SPEC-11 | no | todo |  |  |  |  |
| ART-001 | Public contract types | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | standard | DATA-001 | no | todo |  |  |  |  |
| ART-002 | Direction token sheets and the token checker | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | standard | ART-001 | no | todo |  |  |  |  |
| ART-003 | Mood boards per direction | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | — | yes | todo |  |  |  |  |
| ART-004 | Block-kit style test: five blocks per direction | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-002 | no | todo |  |  |  |  |
| ART-005 | Interactive HTML design board | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-002, ART-003, ART-004 | no | todo |  |  |  |  |
| ART-006 | Owner shortlist of finalist directions | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | standard | ART-005 | no | todo |  |  |  |  |
| ART-007 | Brick material baker, preview mesher, reference shader and art validator | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-001 | no | todo |  |  |  |  |
| ART-008 | Look-test content for each finalist | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-004, ART-006, ART-007 | no | todo |  |  |  |  |
| ART-009 | Look-test scenes with HUD mock | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-002, ART-008 | no | todo |  |  |  |  |
| ART-010 | Look-test capture pack and metrics | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-009 | no | todo |  |  |  |  |
| ART-011 | Owner look-test review and one revision round | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-010 | no | todo |  |  |  |  |
| ART-012 | Finalize the design system for approval | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | frontier | ART-011 | no | todo |  |  |  |  |
| ART-013 | GATE: Owner approves design system | [SPEC-80](specs/SPEC-80-art-direction.md) | A0 | standard | ART-012 | no | todo |  |  |  |  |
| AST-001 | Public contract types: asset manifest, naming rules and licence register | [SPEC-81](specs/SPEC-81-asset-production.md) | A0 | standard | ART-001 | no | todo |  |  |  |  |
| AST-002 | Art import rules and asset validator | [SPEC-81](specs/SPEC-81-asset-production.md) | A0 | standard | AST-001 | no | todo |  |  |  |  |
| AST-003 | Licence audit of carried-over placeholders | [SPEC-81](specs/SPEC-81-asset-production.md) | A0 | standard | AST-001 | no | todo |  |  |  |  |
| ART-014 | Apply the approved direction | [SPEC-80](specs/SPEC-80-art-direction.md) | A1 | standard | ART-013 | no | todo |  |  |  |  |
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
