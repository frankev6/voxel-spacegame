# Implementation dispatch board

Owner authorization and revised goal: ADR-0062. Read this with HANDOFF and LEDGER on every resumed orchestration turn.

Coordinator thread: `01a12ab8-8b1d-7263-ac71-aa447e6af284`.

| Worker | Model | Thread | Worktree | Active task | State | Eligible follow-up candidates |
|---|---|---|---|---|---|---|
| Combat support | gpt-6.1-sol high | `01a133d2-c73e-7d11-a378-844fc4839836` | `/Users/frank/.codex/worktrees/6d4c/spaceship-game-v2` | DEV-018 | Implement real save/load/jump commands and CLI/menu flow | TRV-011 after delivery |
| Power and abilities | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41a7a8c1d570` | `/Users/frank/.codex/worktrees/5fab/spaceship-game-v2` | GAL-012 | Finish authored galaxy/body content and real service integration | QST-009 after delivery |
| Presentation and content | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41823de6795b` | `/Users/frank/.codex/worktrees/1369/spaceship-game-v2` | EDT-020 | Implement blueprint store/browser on integrated clipboard source | Editor host support after delivery |

## Operating rules

Owner correction 2026-10-14: all three workers implement substantial feature deliveries, not repeated review assignments. Batch source/integration commits at meaningful completion points. EDT-020 may implement against integrated EDT-017 source while the lead validates it; this explicit pipeline exception does not mark EDT-017 done or waive functional acceptance. Lead owns selection fixes and editor connectivity/validation/commit prerequisites. Do not use partial acceptance as an idle reason when useful independent implementation is available.

Owner authorization: ADR-0062. Exactly these three separate reusable GPT-6.1-Sol high threads; no replacements or subagents. Lead owns central docs/main, reviews, frontier work and the serialized Unity channel. Workers use isolated task branches, no Unity, no remotes and no central workflow docs. Read all three exact thread states at work boundaries; recycle to ready standard tasks. Record real waits. Advisory performance policy and art restrictions remain binding.

Revised native goal is active. Heartbeat `spaceship-implementation-coordinator` is paused as a fallback. Never falsely mark the roadmap achieved.

All three workers received Frank's fast-fixture correction: shorten incidental combat setup with fixture-only health/damage/cadence/distance changes, preserve required outcomes and production balance, use bounded fixed ticks and batch focused checks after implementation. No routine performance/soak runs. Budget test wall time as well as simulation time: use the smallest fixture and direct ticks that prove the requirement, avoid real-time combat waits, and run focused checks once per compatible implementation batch. Keep functional assertions intact; production balance is independent of fixture acceleration.

## Current lead state

- Owner correction implemented: DEV-018, GAL-012 and EDT-020 active in the exact three existing workers (snapshots338/357/313). Main source integrated; previous worker deliveries accepted in one compatible42 EditMode +4 graphics PlayMode batch. Selection repaired e28ae8a5, renderer previously accepted. Lead next connectivity/validation/commit/overlay/host implementation; no Unity active.
- Deliver feature-sized changes, run required focused checks together, and recycle the same workers promptly. Blueprint dependencies are concrete and accepted (SPEC-51 Q49); future follow-ups TRV-011/QST-009 still require actual delivery acceptance. Historical outcomes live in LEDGER, not repeated here.
