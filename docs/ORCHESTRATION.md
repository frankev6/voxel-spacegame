# Implementation dispatch board

Owner authorization and revised goal: ADR-0062. Read this with HANDOFF and LEDGER on every resumed orchestration turn.

Coordinator thread: `01a12ab8-8b1d-7263-ac71-aa447e6af284`.

| Worker | Model | Thread | Worktree | Active task | State | Eligible follow-up candidates |
|---|---|---|---|---|---|---|
| Combat support | gpt-6.1-sol high | `01a133d2-c73e-7d11-a378-844fc4839836` | `/Users/frank/.codex/worktrees/6d4c/spaceship-game-v2` | PHY-014 | implementing | WPN-017, then ready weapon/harness tasks |
| Power and abilities | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41a7a8c1d570` | `/Users/frank/.codex/worktrees/5fab/spaceship-game-v2` | PWR-001 | implementing | PWR-002, ABL-001 after integration |
| Presentation and content | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41823de6795b` | `/Users/frank/.codex/worktrees/1369/spaceship-game-v2` | MIG-011 | implementing | CAM-008, audio content after AUD-006 |

## Dispatch procedure

Read exact thread status; do not create replacement threads merely because a worker is idle. Review delivered commits, integrate compatible changes locally, validate through the lead-owned Unity channel, close ledger rows and assign the next eligible task. Workers never write central ledger/HANDOFF/dispatch files or launch Unity. Check all workers before and after long commands; use bounded multi-thread waits when no independent lead work remains. A blocked worker gets an eligible alternative when available, otherwise record why it waits. Do not assign frontier work to Sol.

Heartbeat: `spaceship-implementation-coordinator`, ACTIVE every five minutes, targeting this coordinator thread. The legacy native goal remains blocked because create_goal rejects replacement and update_goal cannot edit/resume it. This is an app-tool limitation, not a project blocker.

Current lead priority: integrate PHY-014, then claim DMG-011 and DMG-012. Preserve Frank's running World until validation requires stopping it. Existing QualitySettings serialization delta is unrelated and must stay excluded.
