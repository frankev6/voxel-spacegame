# Implementation dispatch board

Owner authorization and revised goal: ADR-0062. Read this with HANDOFF and LEDGER on every resumed orchestration turn.

Coordinator thread: `01a12ab8-8b1d-7263-ac71-aa447e6af284`.

| Worker | Model | Thread | Worktree | Active task | State | Eligible follow-up candidates |
|---|---|---|---|---|---|---|
| Combat support | gpt-6.1-sol high | pending | pending | PHY-014 | launching | WPN-017, then ready weapon/harness tasks |
| Power and abilities | gpt-6.1-sol high | pending | pending | PWR-001 | launching | PWR-002, ABL-001 after integration |
| Presentation and content | gpt-6.1-sol high | pending | pending | MIG-011 | launching | CAM-008, audio content after AUD-006 |

## Dispatch procedure

Read exact thread status; do not create replacement threads merely because a worker is idle. Review delivered commits, integrate compatible changes locally, validate through the lead-owned Unity channel, close ledger rows and assign the next eligible task. Workers never write central ledger/HANDOFF/dispatch files or launch Unity. Check all workers before and after long commands; use bounded multi-thread waits when no independent lead work remains. A blocked worker gets an eligible alternative when available, otherwise record why it waits. Do not assign frontier work to Sol.

Current lead priority: integrate PHY-014, then claim DMG-011 and DMG-012. Preserve Frank's running World until validation requires stopping it. Existing QualitySettings serialization delta is unrelated and must stay excluded.
