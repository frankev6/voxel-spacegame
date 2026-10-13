# Implementation dispatch board

Owner authorization and revised goal: ADR-0062. Read this with HANDOFF and LEDGER on every resumed orchestration turn.

Coordinator thread: `01a12ab8-8b1d-7263-ac71-aa447e6af284`.

| Worker | Model | Thread | Worktree | Active task | State | Eligible follow-up candidates |
|---|---|---|---|---|---|---|
| Combat support | gpt-6.1-sol high | `01a133d2-c73e-7d11-a378-844fc4839836` | `/Users/frank/.codex/worktrees/6d4c/spaceship-game-v2` | UI-011 | UI-011 active; MIG-009 validated and merged | Ready standard work after batch integration |
| Power and abilities | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41a7a8c1d570` | `/Users/frank/.codex/worktrees/5fab/spaceship-game-v2` | PWR-003 | dispatched from main 7b8cf39; DLG-001 d07fd9b and GAL-001 9c177ac pending validation | M2 power/ability work |
| Presentation and content | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41823de6795b` | `/Users/frank/.codex/worktrees/1369/spaceship-game-v2` | ART-014 | dispatched from main claim 010a9e2; UI-010 0c60754 delivered pending validation | Ready presentation work after integration |

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

- MIG-009 delivered `c48030b79dca12bf411d94a2777dcc0befd110ae`: converter plus three required tests, SPEC-72 Q20/Q21. Lead must generate remaining six weapons, catalog-register and validate strict DATA-010 links. Same worker claimed/dispatched UI-011 from main `b902672`; ten required dialog tests, SPEC-60 question numbers Q27+ reserved.
- DMG-012 hulk recorder extracted to `Jobs/RecordHulkJob.cs`; seven required `HulkSplitTests` pass (session 2041 exit 0), compile clean. Canonical root/brick/functional LEG order, stale parent membership filtering, and once-only empty-ID-block warning completed. Evidence `Logs/playtest/dmg012/hulk-focused.xml`. Full compatible-batch regression and live hulk check remain before closeout.

- Batch 3 source integrated on lead: INP-008 `33e24c3`, MIG-015 `5241ee5`, AST-001 `b175f2f`, UI-009 `c777fd7` plus fixture fix `a34c3c3`, MIG-009 `132a02a`, DATA-010 `161c8d2`. Compile passes. Targeted weapon generation exited 0; temporary script/meta deleted. Content commit `8204e88` supplies six new weapons, nine valid links, preserves plasma tuning and fills empty sounds. Content validation: zero errors/warnings. Full EditMode session 13469 found 7 integration fixture failures (916 passed). Fixed in `acdfe94`/`d15d72e`: inventory/protected migration outcomes, missing catalog fallback definitions, nullable binding groups and keyboard-only rebind scope. All 19 affected tests pass with compile clean; `77ecc4a` retains valid DATA-010 serialized grants/provenance. Full EditMode/PlayMode rerun session `67185` is running; do not mutate source or run another Unity process. Ledger Q217–222 mirror delivered spec decisions.

- Live hulk proof prepared at `Logs/playtest/dmg012/cut-port-bridge.cs.txt`: production projectiles resolve the player mount weapon index dynamically (new nine-weapon catalog changes indices), then cut the 3x station port bridge. Run only after current full suites finish; no live claim yet.

- Batch3 full EditMode rerun passed **923/0/1**; session `67185` continues PlayMode, still live. GAL-001 delivered `9c177ac6b3bd4901b9b472e3288a63f6792a30a3` (3 required tests), UI-010 delivered `0c607547e328b9c5054c82930c2e0d46973d9210` (7 required tests). Both await integration/validation. GAL worker recycled to ready DLG-001 from main claim `f85dcab`; UI-010 worker is in a real dependency wait. UI-011 worker notified to use SPEC-60 Q28 onward because UI-010 used Q25–27. No source mutation during the active suites.

- ART-014 scope review resolves the temporary presentation wait: its exact task mechanically applies already-approved Nocturne values and existing OFL fonts; it requires no new art decision or production. Claimed on main `010a9e2` and dispatched to the same presentation thread. Preserve live sky brightness/density and physical planet; audit references before removing only specified non-chosen assets. GAL-001 identity/event/stub review and UI-010 pool/coalescing/lifecycle/test review found no issue requiring a worker repair; shared UIRoot/config conflicts must retain all services on integration. PlayMode process/session `67185` remains active.

- Session `67185` completed **exit 0**, full EditMode **923/0/1**, PlayMode **135/0/53**. Evidence archived in batch3 `*-final.xml`; six worker tasks closed, DMG-012 awaits live proof. Original QualitySettings restored from backup; runner removed test scene. Fresh interactive Editor session `87955` launched, waiting for Pipeline startup before range.

- DMG-012 closed: live controlled production projectiles detach 2296-cell port section, functional ownership correct, collider Ready, velocity ~0.75 m/s, zero runtime errors. Screenshot `Logs/playtest/dmg012/station-hulk-separated.png`; readback `live-proof.txt`. Full validated batch ready for local main FF. Interactive Editor `87955` remains running range.

- Main includes validated DMG-012/batch3 at `5388604`. Lead claimed AI-002 (`7b8cf39`) on `task/AI-002-spatial-queries`: hash queries and five required reference tests implemented, validation pending. DLG-001 delivered `d07fd9b8189ae8cfedb477b6ec0177639a4077c1`, one required test; same worker recycled to ready PWR-003. Q223–224 resolve read-only overflow telemetry and bounded traversal.

- AI-002 `bb73f9b`: compile clean, all five required query tests pass (`Logs/playtest/ai002/query-focused.xml`). Integrated GAL-001 `6d49a49`, DLG-001 `67e0ffd`, UI-010 `6b3a919`; both UI service lifecycle/config/theme changes retained. Integrated compile clean. Focused grouped audit running before full regression; no done claim for this batch yet. Ledger Q225–227 mirror delivered resolutions. Editor stopped for validation.

- Live grouped filter `A|B|...` was rejected with no matching tests (session 79708; no result accepted). Closed idle Editor normally (87955 exit 0) and started the same grouped filter headlessly, session `79989`. Poll that handle; do not launch another Unity operation.

- Grouped audit 79989 passed 18/0/0. Extending the required ordered-query test to schedule Burst found embedded GuardedHandles incorrectly entering the job. ADR-0064 / Q228 repairs this with SpatialHashQueryData and AsReadOnly, keeping all spatial-container safety checks. Repaired compile clean and grouped audit session 25268 passed **18/0/0**, including actual Burst query/EndRead/disposal (`Logs/playtest/ai002/query-burst-integrated.xml`). Full suites still pending; integrate UI-011 delivery if ready before launching compatible batch.
