# Implementation dispatch board

Owner authorization and revised goal: ADR-0062. Read this with HANDOFF and LEDGER on every resumed orchestration turn.

Coordinator thread: `01a12ab8-8b1d-7263-ac71-aa447e6af284`.

| Worker | Model | Thread | Worktree | Active task | State | Eligible follow-up candidates |
|---|---|---|---|---|---|---|
| Combat support | gpt-6.1-sol high | `01a133d2-c73e-7d11-a378-844fc4839836` | `/Users/frank/.codex/worktrees/6d4c/spaceship-game-v2` | HUD-001 | implementing; WPN-017 delivered 5354eff, integration pending | WPN-017, then ready weapon/harness tasks |
| Power and abilities | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41a7a8c1d570` | `/Users/frank/.codex/worktrees/5fab/spaceship-game-v2` | ABL-001 | implementing; PHY-022 1a9e98a and PWR-002 1203038 delivered pending validation | PWR-002, ABL-001 after integration |
| Presentation and content | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41823de6795b` | `/Users/frank/.codex/worktrees/1369/spaceship-game-v2` | UI-008 | implementing; CAM-008 delivered e67313b, integration pending | CAM-008, audio content after AUD-006 |

## Dispatch procedure

Read exact thread status; do not create replacement threads merely because a worker is idle. Review delivered commits, integrate compatible changes locally, validate through the lead-owned Unity channel, close ledger rows and assign the next eligible task. Workers never write central ledger/HANDOFF/dispatch files or launch Unity. Check all workers before and after long commands; use bounded multi-thread waits when no independent lead work remains. A blocked worker gets an eligible alternative when available, otherwise record why it waits. Do not assign frontier work to Sol.

Heartbeat: `spaceship-implementation-coordinator`, PAUSED fallback (previously every five minutes), targeting this coordinator thread. Frank cleared the legacy goal on 2026-10-13. The revised native orchestration goal is now active; use it for continuous implementation. The redundant heartbeat is paused.

Current lead priority: integrate PHY-014, then claim DMG-011 and DMG-012. Preserve Frank's running World until validation requires stopping it. Existing QualitySettings serialization delta is unrelated and must stay excluded.

Batch 1 complete: PHY-014 `094f430`, PWR-001 `eeed571`, MIG-011 `ca37e33` plus generated assets `8669cc7`. Compile zero CS warnings; EditMode 861/0/1; PlayMode 121/0/53. Session 92809 completed exit 0. Evidence `Logs/playtest/orchestration-batch1/`. Editor closed; QualitySettings original delta preserved.

Batch 2: WPN-017 integrated `ec9a6f1`; CAM-008 integrated `9d59a62` with lead Boot reader binding. PHY-022 delivered `1a9e98a` for review/integration. HUD-001 and UI-008 workers active. PWR-002 now claimed/dispatched after validated PWR-001; DMG-011 claimed for lead implementation. No batch-2 validation started yet.

Lead DMG-011 branch `task/DMG-011-chunk-bodies`: `49a7503` adds deterministic greedy boxes and COM-rebased exact mass/velocity helpers. Not compiled/tested yet; next implement RecordChunkJob, lifetime/pool integration, cleanup and five required tests. Keep collider/visual origins identical. Worker scope decisions sent: HUD-001 stages only canonical SPEC-51 editor type identities with EDT-001 TODO; UI-008 tests actual bounds now and UI-012 adds canonical audit later. Await worker spec questions and mirror in central ledger.

DMG-011 runtime now connected: chunk planning, serial pool slot selection/eviction, exact cell mass, colliders, lifetime shrink/disable, and cleanup after unload. Compile passed, zero warning CS. Q205 / SPEC-13 Q33 records canonical dissolve state and 256 slot limit. Next: author/run the five required ChunkBodyTests, inspect chunk rendering integration (existing VFX dissolve consumer does not itself draw cells), review/integrate PHY-022 and pending worker deliveries before batched validation. No Unity process currently active.

DMG-011 five required tests now pass (5/0/0), evidence Logs/playtest/dmg011/chunk-tests.xml. Pool test caught real RetiredChunkBlobs safety write omission; fixed by explicit RecordChunkJob.Retired field. Chunk renderer is absent (SPEC-63 explicitly leaves meshes to SPEC-13), so implement visible cell rendering before full-suite/live completion. Existing PropInstanced shader/material can supply palette-lit cube instancing; preserve approved palette. PWR-002 and PHY-022 reviewed, awaiting integration into next batch.
