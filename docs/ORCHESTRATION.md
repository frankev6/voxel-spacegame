# Implementation dispatch board

Owner authorization and revised goal: ADR-0062. Read this with HANDOFF and LEDGER on every resumed orchestration turn.

Coordinator thread: `01a12ab8-8b1d-7263-ac71-aa447e6af284`.

| Worker | Model | Thread | Worktree | Active task | State | Eligible follow-up candidates |
|---|---|---|---|---|---|---|
| Combat support | gpt-6.1-sol high | `01a133d2-c73e-7d11-a378-844fc4839836` | `/Users/frank/.codex/worktrees/6d4c/spaceship-game-v2` | ENC-005 | Implement authored territory ownership and generation rebuild; ENC-004 accepted | Encounter/merchant integration after delivery |
| Power and abilities | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41a7a8c1d570` | `/Users/frank/.codex/worktrees/5fab/spaceship-game-v2` | HUD-013 | Implement full dialog host view and five required cases; GAL-013 delivered | Ready standard implementation |
| Presentation and content | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41823de6795b` | `/Users/frank/.codex/worktrees/1369/spaceship-game-v2` | HUD-017 | Implement fleet view/presenter and three required cases; actual edit launcher remains EDT-023 | More ready UI integration after delivery |

## Operating rules

Owner correction 2026-10-14: all three workers implement substantial feature deliveries, not repeated review assignments. Batch source/integration commits at meaningful completion points. EDT-020 may implement against integrated EDT-017 source while the lead validates it; this explicit pipeline exception does not mark EDT-017 done or waive functional acceptance. Lead owns selection fixes and editor connectivity/validation/commit prerequisites. Do not use partial acceptance as an idle reason when useful independent implementation is available.

Owner authorization: ADR-0062. Exactly these three separate reusable GPT-6.1-Sol high threads; no replacements or subagents. Lead owns central docs/main, reviews, frontier work and the serialized Unity channel. Workers use isolated task branches, no Unity, no remotes and no central workflow docs. Read all three exact thread states at work boundaries; recycle to ready standard tasks. Record real waits. Advisory performance policy and art restrictions remain binding.

Native goal was previously paused, but get_goal now confirms active on 2026-10-14 at this continuation. Keep the full objective intact. The existing heartbeat remains configured; do not create another goal or schedule. No duplicate workers.

All three workers received Frank's fast-fixture correction: shorten incidental combat setup with fixture-only health/damage/cadence/distance changes, preserve required outcomes and production balance, use bounded fixed ticks and batch focused checks after implementation. No routine performance/soak runs. Budget test wall time as well as simulation time: use the smallest fixture and direct ticks that prove the requirement, avoid real-time combat waits, and run focused checks once per compatible implementation batch. Keep functional assertions intact; production balance is independent of fixture acceleration.

## Current lead state

- DEV-021 reopened after owner playtest exposed informational-only pads and inaccessible entry. Direct World startup, visible Console/entry controls and actual inventory/settings/trading interactions are integrated; 15-case focused batch and live entry/trading passed. DEV-018/GAL-012/EDT-020 compatible worker deliveries integrated; GAL-012 and EDT-020 accepted. TRV-011 hooks integrated and unit checks pass, actual R30 remains. Exact evidence and partial states in LEDGER.
- ENC-003 accepted a2467740; canonical faction runtime and actual Raider hostility in playground. ECO-011/HUD-018 accepted; GAL-013 compiled with advisory measurements deferred. ENC-004 accepted 3b4929e9: service/save/reset, six service and two integration checks passed. Next combat assignment ENC-005 territory. HUD-013 and HUD-017 workers actively implementing.
- Real remaining dependencies: full ECO-008 launcher needs encounter member lookup ENC-007; QST-009 delivered source needs actual compiled dialogue program DLG-005/007. No fake owners. Editor was stopped before validation; preserve that state. Owner QualitySettings and two content metas remain excluded.
