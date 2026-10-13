# Implementation dispatch board

Owner authorization and revised goal: ADR-0062. Read this with HANDOFF and LEDGER on every resumed orchestration turn.

Coordinator thread: `01a12ab8-8b1d-7263-ac71-aa447e6af284`.

| Worker | Model | Thread | Worktree | Active task | State | Eligible follow-up candidates |
|---|---|---|---|---|---|---|
| Combat support | gpt-6.1-sol high | `01a133d2-c73e-7d11-a378-844fc4839836` | `/Users/frank/.codex/worktrees/6d4c/spaceship-game-v2` | INP-008 | implementing; NAR-001 integrated e39b2d3, validation pending | Ready narrative/weapon work after batch closeout |
| Power and abilities | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41a7a8c1d570` | `/Users/frank/.codex/worktrees/5fab/spaceship-game-v2` | DATA-010 | implementing; MIG-015 delivered 25ac3f0, pending review | Ready power/ability content |
| Presentation and content | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41823de6795b` | `/Users/frank/.codex/worktrees/1369/spaceship-game-v2` | UI-009 | implementing; AST-001 delivered 54c4fba, pending review | Ready presentation work; audio content after AUD-006 |

## Operating rules

Owner authorization: ADR-0062. Exactly these three separate reusable GPT-6.1-Sol high threads; no replacements or subagents. Lead owns central docs/main, reviews, frontier work and the serialized Unity channel. Workers use isolated task branches, no Unity, no remotes and no central workflow docs. Read all three exact thread states at work boundaries; recycle to ready standard tasks. Record real waits. Advisory performance policy and art restrictions remain binding.

Revised native goal is active. Heartbeat `spaceship-implementation-coordinator` is paused as a fallback. Never falsely mark the roadmap achieved.

## Current lead state

- Branch `task/DMG-011-chunk-bodies`, latest repair `e2cbd81`, merged main at `00ea50b`.
- Physical chunks: pool, exact mass/momentum, greedy colliders, retirement and palette-lit rendering implemented. Five required tests including explicit leak checks pass.
- Live 264-cell wall cut exposed a ~102 m/s kick after 60-tick parent grace. ADR-0063 adds per-incarnation contact arming only after geometric clearance. All three existing SplitGraceTests pass with overlap/clearance/recontact coverage. Live repeat stays near 1.5 m/s then damps. Evidence `Logs/playtest/dmg011/contact-handoff-fixed.txt`, `chunk-fixed-moving.png`.
- **Full validation now running: exec session 46677**, compile then EditMode then PlayMode. Editor session 39677 exited normally. Do not restart a live process or run concurrent Unity. Preserve original QualitySettings delta; backup `Logs/playtest/orchestration-batch2/QualitySettings.before.asset`.
- After green full suites: close DMG-011 and merge locally, then claim DMG-012 hulk completion. Review next worker deliveries in a compatible batch. Preserve live play where practical; no new owner gates.

## Validated and pending batches

- Batch 1: PHY-014/PWR-001/MIG-011, full 861/0/1 EditMode and 121/0/53 PlayMode. Evidence `Logs/playtest/orchestration-batch1/`.
- Ten-task batch: WPN-017, CAM-008, PHY-022, PWR-002, ABL-001, HUD-001, UI-008, CAM-009, CAM-010, NAR-001 done. Compile clean; full 901/0/1 EditMode and 128/0/53 PlayMode. Evidence `Logs/playtest/orchestration-batch2/`. Independently integrated to main `0561e29` while chunk repair stayed on lead branch; DATA-010/UI-009 claimed `6d3c2cd` and dispatched.
- MIG-015 delivery `25ac3f0a740f4391b5db73973ca9020a87d3c95b`: typed migration handoff, 3 required tests, SPEC-72 Q18. Await lead review, mirror and validation.
- AST-001 delivery `54c4fba38a658d4990f07a99876edc467b0734a5`: art contracts and 1,019 planned manifest rows, 4 required tests, SPEC-81 Q15–20. Await lead review, mirrors and validation. No produced/licensed asset claim.
- INP-008 hit temporary provider capacity; same thread/model resumed successfully. Do not duplicate it.
