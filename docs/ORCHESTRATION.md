# Implementation dispatch board

Owner authorization and revised goal: ADR-0062. Read this with HANDOFF and LEDGER on every resumed orchestration turn.

Coordinator thread: `01a12ab8-8b1d-7263-ac71-aa447e6af284`.

| Worker | Model | Thread | Worktree | Active task | State | Eligible follow-up candidates |
|---|---|---|---|---|---|---|
| Combat support | gpt-6.1-sol high | `01a133d2-c73e-7d11-a378-844fc4839836` | `/Users/frank/.codex/worktrees/6d4c/spaceship-game-v2` | MIG-009 | implementing; INP-008 eecb3e0 reviewed, pending validation | Weapon content/links batch with DATA-010 |
| Power and abilities | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41a7a8c1d570` | `/Users/frank/.codex/worktrees/5fab/spaceship-game-v2` | GAL-001 | dispatched; DATA-010 9b56175 reviewed, pending MIG-009 compatible validation | Galaxy contract-dependent standard work |
| Presentation and content | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41823de6795b` | `/Users/frank/.codex/worktrees/1369/spaceship-game-v2` | UI-010 | dispatched; UI-009 76014c4 delivered, pending validation | Ready presentation work; audio content after AUD-006 |

## Operating rules

Owner authorization: ADR-0062. Exactly these three separate reusable GPT-6.1-Sol high threads; no replacements or subagents. Lead owns central docs/main, reviews, frontier work and the serialized Unity channel. Workers use isolated task branches, no Unity, no remotes and no central workflow docs. Read all three exact thread states at work boundaries; recycle to ready standard tasks. Record real waits. Advisory performance policy and art restrictions remain binding.

Revised native goal is active. Heartbeat `spaceship-implementation-coordinator` is paused as a fallback. Never falsely mark the roadmap achieved.

## Current lead state

- DMG-011 completed and fast-forward merged into local main at `b87aec3`; final runtime repair `e2cbd81`. DMG-012 claimed on main `d40c19e`; lead now on `task/DMG-012-hulk-grids`.
- Physical chunks: pool, exact mass/momentum, greedy colliders, retirement and palette-lit rendering verified. Five required tests including native leak checks pass. ADR-0063 clearance contact repair passes existing three contact tests and live 264-cell slab separation proof.
- Full validation session 46677 completed exit 0: compile zero CS warnings, EditMode **901/0/1**, PlayMode **128/0/53**. Evidence `Logs/playtest/dmg011/{editmode-repair,playmode-repair}.xml`, `contact-handoff-fixed.txt`, `chunk-fixed-moving.png`.
- No Unity process active; runner cleaned test scene. Original QualitySettings delta restored from `Logs/playtest/orchestration-batch2/QualitySettings.before.asset` and remains excluded.
- Lead next: extract/complete staged hulk recorder and seven required lifecycle tests under DMG-012. Next compatible worker batch includes MIG-009 content before strict DATA-010, plus pending deliveries below.

## Validated and pending batches

- Batch 1: PHY-014/PWR-001/MIG-011, full 861/0/1 EditMode and 121/0/53 PlayMode. Evidence `Logs/playtest/orchestration-batch1/`.
- Ten-task batch: WPN-017, CAM-008, PHY-022, PWR-002, ABL-001, HUD-001, UI-008, CAM-009, CAM-010, NAR-001 done. Compile clean; full 901/0/1 EditMode and 128/0/53 PlayMode. Evidence `Logs/playtest/orchestration-batch2/`. Independently integrated to main `0561e29` while chunk repair stayed on lead branch; DATA-010/UI-009 claimed `6d3c2cd` and dispatched.
- MIG-015 delivery `25ac3f0a740f4391b5db73973ca9020a87d3c95b`: typed migration handoff, 3 required tests, SPEC-72 Q18. Reviewed extraction and required tests; await integration, mirror and validation.
- AST-001 delivery `54c4fba38a658d4990f07a99876edc467b0734a5`: art contracts and 1,019 planned manifest rows, 4 required tests, SPEC-81 Q15–20. Reviewed rules, planned-only manifest and provenance preservation; await integration, mirrors and validation. No produced/licensed asset claim.
- INP-008 hit temporary provider capacity; same thread/model resumed successfully. Do not duplicate it.

- INP-008 delivered `eecb3e05926e1a318b838e118a541a1e10a27730`, 2 required tests, SPEC-03 Q10/Q11. Lead reviewed aim and device-label flow; Unity validation/hardware ergonomics pending. Do not claim physical controller testing.
- MIG-009 exact dependency resolved in SPEC-72 Q19 / ledger Q216: use permitted DATA-001 contract dependency (DATA-007 implementation is done) rather than all SPEC-04, because serialized weapon field/bake already exists and DATA-010 strict validation needs the six missing links. Initial DATA-007 dependency was rejected by docs checker and corrected on main `8b0016e`; merged into lead, check-docs passes. MIG-009 claimed/dispatched to same combat worker. Keep E047 strict; integrate migration content before DATA-010.
- Repair full validation completed green; see current lead state above.

- DATA-010 delivered `9b56175d254bd18df77898611583f7a8ff95eaaf`: strict E047, weapon stat contributions, authored-order ability bake/hash; 3 required tests, 0 extra. Lead reviewed source and tests; integrate with MIG-009 content. Same worker recycled to GAL-001, claimed on main `ba5be3b`, dispatched successfully. Combat MIG-009 and presentation UI-009 remain active.

- UI-009 delivered `76014c4691bb0f3f5f8962fe4f8a0853f6803438`: tooltip pooling/delay/cancellation/positioning and lifecycle, 7 required tests, 0 extra; SPEC-60 Q23–24 need central mirror. Lead inspected service; full integration review/validation pending. Same presentation worker dispatched UI-010 from main claim `654ee31`; keep shared UI lifecycle edits compatible.

- UI-009 lead review fix `b8af3e35a18846fa974d4a7fede06d5827e1bf07` is on its inactive task branch: reset the fixture fill counter for each test and replace frame-count scheduler deadlines with 5-second real-time bounds. Seven required tests remain unchanged in count. Primary source remains untouched during active Unity validation; integrate both UI-009 commits together.
