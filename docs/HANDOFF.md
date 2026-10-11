# Handoff: start here if you're continuing the work

**Status: living.** Read this first, then [AGENTS.md](../AGENTS.md). It covers the owner's standing rules, where the work stands and what comes next.

- Whoever ends a working session updates *Where things stand* and *Next*. Keep each to a few lines and link to the ledger rather than repeating it.
- Task status lives in [LEDGER.md](LEDGER.md), and decisions in the ADRs and specs. This page only points to them.

## 1. The owner's standing rules

Frank (the owner) set these for every worker. AGENTS.md §2 makes the git and art rules binding.

| Rule | In practice |
|---|---|
| Commit locally only; never push | No `git push` and no remote operations. Merging to local `main` is fine |
| Use the Unity CLI where possible | `tools/*.sh` and the CLI over manual Editor work ([08-tools.md](08-tools.md)) |
| Keep the ledger current; record owner decisions verbatim | Quote Frank's words in the ledger log and in the *Answer* column of open questions |
| No subagents unless Frank allows them | Work in one session |
| Go wide, review systems later (ADR-0019) | No per-task review gate: tasks are `done` at their Definition of Done; an in-depth system review runs per milestone, before its gate (04-workflow §8) |
| Give options, not a single proposal | Every decision for Frank comes as 2–4 options with trade-offs, recommended first |
| No Starsector art and no copyrighted images in the repository | Procedural or own-work art only |
| Fonts are SIL OFL 1.1 only | |
| AI-generated assets only with Frank's approval of each asset | Ask before generating, one asset at a time |

**How Frank decides on look and design:**
- **Show rendered visuals, not process.** Full 3D scenes, ships and game screens come before spec polish.
- **Ship lines follow Starsector's depth without copying it.** Approved 2D silhouettes come before any 3D build. Form follows function. Use asymmetry with a reason, and refit variants. Human hulls are blocky, alien hulls smooth. Factions come from the existing lore in `Import/Legacy/content/Factions` and the DESIGN-GUIDE faction section.
- **Look-book ships are concepts, not final designs**, and the 2D ship lines are not approved yet.

## 2. Where things stand (2026-10-11, M0 passed; M1 underway)

- **M0 Foundations passed:** 71/71 tasks, including five system-review fixes, are merged locally. The [ledger](LEDGER.md) records the review scope, evidence and exit gate. Frank authorized this session's review; it was not an independent second-author review.
- **Verified baseline:** latest compile zero C# warnings; headless EditMode **507 passed + 1 explicit skipped**, PlayMode **31 passed**, perf **27/27**; docs **501 tasks / zero errors**. The M0 exit review separately verified clean headless and safety-on runs (411/31 at that point), six Python tool checks, two boot/re-entry cycles per scene, and **0.176 s** Play-to-menu.
- **M1 progress:** VOX-004 brick maths/masks, REN-001 rendering contracts and PHY-001 physics contracts, PHY-004 box merging, PHY-002 nested-physics capabilities, PHY-005 collider builders, DATA-011 reactor/cargo content, PHY-003 physics configuration/boot PHY-006 mass maths and PHY-007 pipeline data/ownership are done and merged. Queue/batch teardown covers pending jobs and owned blobs. Q51–55 and Q58–61 resolve contract arithmetic/accessibility, one existing-package reference (ADR-0022), the debug-query exception and unspecified container capacities and installed Physics API/size corrections and discarded partial-batch ownership. Nested compounds are a go; PHY-020 is not needed. Rendering uses ADR-0018's expanded snapshot shape bits.
- **Voxel foundations:** VOX-005 persistent dense/uniform structure storage, VOX-012 Burst cell read/write helpers, and VOX-010 blueprint/config authoring and boot singleton are done. The 100,000-write storage fuzz passed in 0.503 s. Q62 fixes sparse-task sequencing; Q63 defines TryGetCell as solid-only; Q64 corrects the boot factory argument. VOX-006 placement/compaction, VOX-007 clone/diff/mass, and VOX-017 benchmark generators are now done: 100k-cell diff 0.127 ms median; reference build 87.43 ms. Q65 brings GetBlocks forward; Q66 corrects benchmark counts without changing geometry; Q67 makes generator/codec dependencies explicit. VOX-009 codec and VOX-011 registry/cache are done: station encode/decode 22.48/58.21 ms median; Q68–70 preserve tombstones, reconstruct free indices and fix worker allocation lifetime. Boot owns the immutable blueprint cache and registry. VOX-008 raycasting passed 10,000 seeded reference rays and ECS parity; DMG-001 adds the damage/restore/impulse contracts and SplitGrace (Q71–74).
- **Memory gate:** VOX-002 measured **38,828,288 B** reference peak with retained mesh CPU copies: **no-go** for 36 MB headroom. [ADR-0023](decisions/ADR-0023-sparse-brick-memory-fallback.md) proposes the required sparse fallback; VOX-003 is todo and must measure its actual result. Thin shell **162,606,784 B** remains stress-only. Q56–57 and SPEC-10 §8.2 retain exact measurements and estimate limits.
- **What exists:** correctly composed boot/content/input/audio/pause/save/scene flow; deterministic core and native event streams; VOXS/save framework with round-trip and failure-path tests; repeatable migration of 35 items, 21 blocks and 13 ships; dev commands/scenarios and release stripping. Gameplay, voxel rendering/physics, camera and screens are M1+. Audio remains silent until AUD-007 config/cues in M2.
- **Review repairs:** TST-009 serializes Unity tools and validates test inventories. PERS-016/017/018 fix capture leaks, committed-save reporting/stale sidecars, and null JSON metadata. CORE-013 retains all disposal jobs and handles abandoned reads (trap T2.13). Decisions are Q47–50 in the ledger/specs.
- **Shutdown anomaly:** old MIG-007 crash evidence is retained. After CORE-013, clean headless runs exit 0 with no aged TempJob warnings. The old crash's exact cause remains unproven; investigate if it recurs.
- **Owner decisions:** NOCTURNE is approved and binding ([DESIGN-GUIDE.md](design/DESIGN-GUIDE.md), ART-013); slope kit is ADR-0018. Questions 6(b), 6(c), 7 remain owner-only. Frank should also review Q30's bounded/full-palette fallback, Q40's temporary ship_base starting ship, and Q48's save commit-point clarification. No new art or ships were authored.
- **Tools:** the task-owned persistent Editor was closed for PHY-002; headless Unity checks exit normally. Serialize tool operations; direct GUI/CLI bypasses the wrapper lock. Restore incidental font glyph-cache diffs before commits. Safety-setting restoration can outlast Pipeline's 5 s main-thread timeout: read back before assuming failure or retrying.
- **Branches:** main contains completed work; task/ART-007-brick-tools still has the unmerged baker/mesher/shader and unfinished validator. ART-004 remains in review for the 150 m glyph criterion.

## 3. Next

1. **M1:** choose ready voxel/core/render/physics contract tasks. VOX-004 and REN-001/PHY-001 are done; VOX-002 recorded no-go. Resolve proposed ADR-0023 before changing the dense-cell contract in VOX-003. PHY-002 confirmed nested compounds and PHY-005 is implemented; DATA-011/PHY-003 added content and boot physics configuration, and PHY-006 implements mass maths. PHY-007 is also done. Q62 corrects VOX-003 sequencing: its callers are stubs/absent, so VOX-005/006/007/010/012/017 are done; VOX-009 codec and VOX-011 registry are also done; VOX-008 and DMG-001 are done too. Split the queue methods/tests from REN-010 (its SPEC-10 dependency otherwise cycles through spawning), then implement VOX-013/014 (cancel pinned sources before service teardown), then resolve ADR-0023 and implement/measure the complete sparse fallback.
2. **ART-007:** finish the validator on its existing branch, bring it up to date with main and merge locally when convenient.
3. Keep advancing ready M1 tasks, then its system review and exit gate. Art-track owner gates and no-new-3D-ship restrictions stay in force.
