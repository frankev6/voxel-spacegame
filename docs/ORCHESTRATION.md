# Implementation dispatch board

Owner authorization and revised goal: ADR-0062. Read this with HANDOFF and LEDGER on every resumed orchestration turn.

Coordinator thread: `01a12ab8-8b1d-7263-ac71-aa447e6af284`.

| Worker | Model | Thread | Worktree | Active task | State | Eligible follow-up candidates |
|---|---|---|---|---|---|---|
| Combat support | gpt-6.1-sol high | `01a133d2-c73e-7d11-a378-844fc4839836` | `/Users/frank/.codex/worktrees/6d4c/spaceship-game-v2` | HUD-012 | Implement real merchant screen/presenter and trade-session interaction | More ready UI integration after delivery |
| Power and abilities | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41a7a8c1d570` | `/Users/frank/.codex/worktrees/5fab/spaceship-game-v2` | QST-009 | Author exact opening chapter content and tags against accepted validators | Opening integration after concrete prerequisites |
| Presentation and content | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41823de6795b` | `/Users/frank/.codex/worktrees/1369/spaceship-game-v2` | HUD-011 | Implement inventory screen/presenter, pooled slots and drag/sort/stack | More ready UI integration after delivery |

## Operating rules

Owner correction 2026-10-14: all three workers implement substantial feature deliveries, not repeated review assignments. Batch source/integration commits at meaningful completion points. EDT-020 may implement against integrated EDT-017 source while the lead validates it; this explicit pipeline exception does not mark EDT-017 done or waive functional acceptance. Lead owns selection fixes and editor connectivity/validation/commit prerequisites. Do not use partial acceptance as an idle reason when useful independent implementation is available.

Owner authorization: ADR-0062. Exactly these three separate reusable GPT-6.1-Sol high threads; no replacements or subagents. Lead owns central docs/main, reviews, frontier work and the serialized Unity channel. Workers use isolated task branches, no Unity, no remotes and no central workflow docs. Read all three exact thread states at work boundaries; recycle to ready standard tasks. Record real waits. Advisory performance policy and art restrictions remain binding.

Native goal was verified paused on 2026-10-14; do not describe it as active or achieved. Existing heartbeat `spaceship-implementation-coordinator` was reactivated at its existing five-minute cadence under Frank’s keep-going/keep-three-workers-working request. It supplies continuation, with quiet unchanged-state notifications. No duplicate schedule or workers.

All three workers received Frank's fast-fixture correction: shorten incidental combat setup with fixture-only health/damage/cadence/distance changes, preserve required outcomes and production balance, use bounded fixed ticks and batch focused checks after implementation. No routine performance/soak runs. Budget test wall time as well as simulation time: use the smallest fixture and direct ticks that prove the requirement, avoid real-time combat waits, and run focused checks once per compatible implementation batch. Keep functional assertions intact; production balance is independent of fixture acceleration.

## Current lead state

- DEV-021 playground playable, required functional test and live image verified. DEV-018/GAL-012/EDT-020 compatible worker deliveries integrated; GAL-012 and EDT-020 accepted. TRV-011 hooks integrated and unit checks pass, actual R30 remains. Exact evidence and partial states in LEDGER.
- Workers dispatched to claimed HUD-012, QST-009 and HUD-011, same exact three threads. Lead finishes playground demonstration and keeps incoming screen integration moving; frontier editor/root work remains lead-owned. Preserve the live game after demonstration. No unchanged-state chatter or repeated review-only dispatches.
