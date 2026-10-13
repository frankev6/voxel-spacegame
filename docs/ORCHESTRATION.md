# Implementation dispatch board

Owner authorization and revised goal: ADR-0062. Read this with HANDOFF and LEDGER on every resumed orchestration turn.

Coordinator thread: `01a12ab8-8b1d-7263-ac71-aa447e6af284`.

| Worker | Model | Thread | Worktree | Active task | State | Eligible follow-up candidates |
|---|---|---|---|---|---|---|
| Combat support | gpt-6.1-sol high | `01a133d2-c73e-7d11-a378-844fc4839836` | `/Users/frank/.codex/worktrees/6d4c/spaceship-game-v2` | MIG-009 | implementing; INP-008 eecb3e0 reviewed, pending validation | Weapon content/links batch with DATA-010 |
| Power and abilities | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41a7a8c1d570` | `/Users/frank/.codex/worktrees/5fab/spaceship-game-v2` | GAL-001 | dispatched; DATA-010 9b56175 reviewed, pending MIG-009 compatible validation | Galaxy contract-dependent standard work |
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
- MIG-015 delivery `25ac3f0a740f4391b5db73973ca9020a87d3c95b`: typed migration handoff, 3 required tests, SPEC-72 Q18. Reviewed extraction and required tests; await integration, mirror and validation.
- AST-001 delivery `54c4fba38a658d4990f07a99876edc467b0734a5`: art contracts and 1,019 planned manifest rows, 4 required tests, SPEC-81 Q15–20. Reviewed rules, planned-only manifest and provenance preservation; await integration, mirrors and validation. No produced/licensed asset claim.
- INP-008 hit temporary provider capacity; same thread/model resumed successfully. Do not duplicate it.

- INP-008 delivered `eecb3e05926e1a318b838e118a541a1e10a27730`, 2 required tests, SPEC-03 Q10/Q11. Lead reviewed aim and device-label flow; Unity validation/hardware ergonomics pending. Do not claim physical controller testing.
- MIG-009 exact dependency resolved in SPEC-72 Q19 / ledger Q216: use permitted DATA-001 contract dependency (DATA-007 implementation is done) rather than all SPEC-04, because serialized weapon field/bake already exists and DATA-010 strict validation needs the six missing links. Initial DATA-007 dependency was rejected by docs checker and corrected on main `8b0016e`; merged into lead, check-docs passes. MIG-009 claimed/dispatched to same combat worker. Keep E047 strict; integrate migration content before DATA-010.
- Repair full EditMode **901/0/1** passes; saved `Logs/playtest/dmg011/editmode-repair.xml`. PlayMode session 46677 remains running; source unchanged during validation.

- DATA-010 delivered `9b56175d254bd18df77898611583f7a8ff95eaaf`: strict E047, weapon stat contributions, authored-order ability bake/hash; 3 required tests, 0 extra. Lead reviewed source and tests; integrate with MIG-009 content. Same worker recycled to GAL-001, claimed on main `ba5be3b`, dispatched successfully. Combat MIG-009 and presentation UI-009 remain active.
