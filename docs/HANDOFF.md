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

- **M0 passed:** 71/71 tasks merged locally, including five review fixes. The [ledger](LEDGER.md) records the exit review, clean/safety-on runs, scene cycles and 0.176 s Play-to-menu. Frank authorized this session's review; it was not an independent second-author review.
- **Verified baseline:** compile zero C# warnings; EditMode **657 passed + 1 explicit skipped**, PlayMode **51 passed**, perf **38/38**; docs **505 tasks / zero errors**. Main contains all completed work. The latest physics tasks cover snapshots, batches, swaps, cleanup, body initialization, exact mass updates and stats/debug. Factory lifecycle and five physics scenarios pass; four frame patterns × 1/17 workers produce identical per-tick collider/mass/velocity records with zero late installs. PHY-019’s six cost/memory tests and full suites pass; zero late installs in the 30-ship damage stand-in. PHY-023 / ADR-0035 repairs an intermittent catch-up deadline race exposed during FLT-002: urgent batches finish in the variable-rate builder before the next six-tick frame; fixed-step installation stays nonblocking.
- **Voxel/render foundations:** all SPEC-10 M1 work is done. The sparse reference station retains **32,227,328 B** (corrected live collider overlap, ADR-0033) (under 36 MB); thin-shell **107,292,880 B** remains stress-only. Five-shape snapshots and LOD0/LOD1 meshers are done. ADR-0027 uses half-cell packed coordinates; ADR-0030 uses uint CPU indices with conditional UInt16/UInt32 GPU upload. DATA-012's approved slope kit is implemented; its tuning remains provisional. Detailed measurements and Q51–120 are in the ledger/specs.
- **Renderer fallback required:** REN-002 is no-go: movement delta **1.625 ms**, GPU median **6.435 ms**. Packed pixels, mutable uploads and churn/leak checks pass. Releasing CPU copies prevents later rewrites, so the default is false. [ADR-0032](decisions/ADR-0032-pooled-brick-renderer.md) is proposed for REN-003; resolve packed allocation slack and the memory gate before accepting its final contract. Whole-renderer integration still belongs to TST-004.
- **Owner/art boundaries:** NOCTURNE is approved and binding. Questions 6(b), 6(c), 7 remain owner-only; use documented defaults. Frank should review Q30's palette fallback, Q40's temporary ship_base, Q48's save commit point, Q57's thin stress policy and DATA-012 tuning, and ADR-0033’s explicit collider-share reallocation (whole limits unchanged). No AI assets or new 3D ships were authored; ship silhouettes remain unapproved. ART-004 remains in review for the 150 m glyph criterion; `task/ART-007-brick-tools` retains the unmerged baker/mesher/shader and unfinished validator.
- **Tools:** no persistent Editor is open. Serialize Unity through the wrappers; restore incidental generated changes without overwriting work. Real GPU probes require `-force-gfx-mt` and fresh GPU timing samples (08-tools). T2.14 covers Burst component types/FixedString literal conversions. The old MIG-007 shutdown anomaly has not recurred after CORE-013; causality remains unproven.

## 3. Next

1. **Next ready work:** FLT-007/008 propulsion and plane constraint, then remaining M1 renderer/camera/UI work. FND-016 sandbox entry is implemented (player optional, creative, serialized reset after teardown); DEV-008 owns its harness command. FND-010 now owns the fleet, sorted/deferred FLET snapshots, corrupt-structure fallback, player spawn and save/exit writeback. The headless player tests verify paused physical readiness and 50 removed cells surviving save/load; full renderer readiness remains REN integration. ADR-0036 feeds recoveries into the load report; Q117 repairs an allocation counter reset by damaged SESS without changing fleet ids. PHY-024 / ADR-0037 initializes fresh spawn bodies during paused loading; split adoption remains fixed-step. FLT-001/002/003/004/005/006 are done; player commands consume one pending press per tick and leave Cruise to travel (Q120). The allocator retains fixed-order PGS; Q119 records its redundant-layout symmetry limit and double dot-product accumulation for exact managed/Burst parity. FND-012 later replaces the direct M1 player request with ShipSpawnService; GAL-008/SYS add canonical positions. FND-008 allocation and FND-009 session flow/SESS are done; ADR-0034 provides save preparation and bounded optional thumbnails (camera binding: CAM-004). DamageStreams removal wiring remains conditional until DMG-002; restored-cell mass remains M3 PHY-022.
2. **Other ready M1 work:** REN-003 pooled fallback, shader/config and renderer integration, then flight/camera/UI work in ledger/ROADMAP order. M1 system review and its exit gates remain outstanding; continue later ready work without waiting for owner gates.
3. **Art track:** finish ART-007's validator on its existing branch, bring it up to date with main and merge locally when convenient. All owner-only art restrictions remain in force.
