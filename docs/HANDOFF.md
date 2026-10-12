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
| Performance targets are advisory (ADR-0042, owner 2026-10-12) | Build and connect systems, then run the game. Spec numbers never block completion or later phases. At most two initial optimization passes per system/feature across all workers and handoffs; record the remaining gap and move on. See [performance policy](decisions/ADR-0042-advisory-performance-targets.md) |
| Go wide, review systems later (ADR-0019) | No per-task review gate: tasks are `done` at their Definition of Done; an in-depth system review runs per milestone, before its gate (04-workflow §8) |
| Give options, not a single proposal | Every decision for Frank comes as 2–4 options with trade-offs, recommended first |
| No Starsector art and no copyrighted images in the repository | Procedural or own-work art only |
| Fonts are SIL OFL 1.1 only | |
| AI-generated assets only with Frank's approval of each asset | Ask before generating, one asset at a time |

**How Frank decides on look and design:**
- **Show rendered visuals, not process.** Full 3D scenes, ships and game screens come before spec polish.
- **Ship lines follow Starsector's depth without copying it.** Approved 2D silhouettes come before any 3D build. Form follows function. Use asymmetry with a reason, and refit variants. Human hulls are blocky, alien hulls smooth. Factions come from the existing lore in `Import/Legacy/content/Factions` and the DESIGN-GUIDE faction section.
- **Look-book ships are concepts, not final designs**, and the 2D ship lines are not approved yet.

## 2. Where things stand (2026-10-12, M0 passed; M1 underway)

- **M0 passed:** 71/71 tasks merged locally, including five review fixes. The [ledger](LEDGER.md) records the exit review, clean/safety-on runs, scene cycles and 0.176 s Play-to-menu. Frank authorized this session's review; it was not an independent second-author review.
- **Verified baseline through REN-009:** compile zero C# warnings; EditMode **696 passed + 1 explicit skipped**, PlayMode **65 passed + 6 GPU-only skipped**, REN-003 separate GPU allocator/pixel checks pass (timing unavailable), existing perf **47/47**; docs **505 tasks / zero errors**. Main contains all completed work. The latest physics tasks cover snapshots, batches, swaps, cleanup, body initialization, exact mass updates and stats/debug. Factory lifecycle and five physics scenarios pass; four frame patterns × 1/17 workers produce identical per-tick collider/mass/velocity records with zero late installs. PHY-019’s six cost/memory tests and full suites pass; zero late installs in the 30-ship damage stand-in. PHY-023 / ADR-0035 repairs an intermittent catch-up deadline race exposed during FLT-002: urgent batches finish in the variable-rate builder before the next six-tick frame; fixed-step installation stays nonblocking.
- **Voxel/render foundations:** all SPEC-10 M1 work is done. The sparse reference station retains **32,227,328 B** (corrected live collider overlap, ADR-0033) (under 36 MB); thin-shell **107,292,880 B** remains stress-only. Five-shape snapshots and LOD0/LOD1 meshers are done. ADR-0027 uses half-cell packed coordinates; ADR-0030 uses uint CPU indices with conditional UInt16/UInt32 GPU upload. DATA-012's approved slope kit is implemented; its tuning remains provisional. REN-008 supplies the shared canonical brick/prop surface and procedural texture/material inputs; 112 Metal/D3D11 variants and actual palette/AO/damage/shadow/prop pixels pass (ADR-0040 / Q133). Detailed measurements and Q51–133 are in the ledger/specs.
- **Integration underway:** REN-009 is merged (`6407fb5`): boot-owned render configuration, approved palette and explicit materials/meshes. REN-010 is merged (`be4b370`): functional EditMode checks pass; full suites have one advisory timing failure, **0.8640 ms** vs 0.5 ms for 128 bricks. Zero tuning passes; REN-011 is now merged (`18a0e45`): pooled apply/cleanup and explicit camera transform bridge, all six GPU integration checks passing (128-brick apply 0.317 ms, zero managed allocation).
- **Renderer foundation complete:** REN-003 (`de1b11d`) is merged locally, with accepted ADR-0032/0041. Allocator, growth/retirement, pixel parity (including shadows/damage) and 48 shader variants pass; fresh compile is warning-free, EditMode 693 passed + 1 skipped and headless PlayMode 65 passed + 6 GPU-only skipped. GPU timing/measurement problems remain explicitly unresolved. Performance is deferred under ADR-0042; continue integration; no end-to-end playable loop has been demonstrated.
- **Owner/art boundaries:** NOCTURNE is approved and binding. Questions 6(b), 6(c), 7 remain owner-only; use documented defaults. Frank should review Q30's palette fallback, Q40's temporary ship_base, Q48's save commit point, Q57's thin stress policy and DATA-012 tuning, and ADR-0033’s explicit collider-share reallocation (whole limits unchanged). No AI assets or new 3D ships were authored; ship silhouettes remain unapproved. ART-004 remains in review for the 150 m glyph criterion; `task/ART-007-brick-tools` retains the unmerged baker/mesher/shader and unfinished validator.
- **Tools:** no persistent Editor is open. Serialize Unity through the wrappers; restore incidental generated changes without overwriting work. Real GPU probes require `-force-gfx-mt` and fresh GPU timing samples (08-tools). T2.14 covers Burst component types/FixedString literal conversions. The old MIG-007 shutdown anomaly has not recurred after CORE-013; causality remains unproven.

## 3. Next

**Owner priority, 2026-10-12:** stop isolated renderer benchmark tuning; its initial two-pass allowance is already exhausted. Preserve the working renderer, finish functional verification/integration, connect player input and exercise launch → spawn → fly → save/load through the live harness. Numeric performance misses and missing timing data are deferred under ADR-0042, not blockers. No end-to-end playable loop has yet been demonstrated. This priority supersedes older benchmark-first wording below.

1. **Finish functional renderer integration:** REN-003 is closed with performance deferred. Renderer integration through REN-011 is done. Continue INP-005 player sampling, DEV-008 entry commands, then FND-017 imported-ship flight checkpoint. Do not restart the exhausted optimization allowance.
2. **Demonstrate the game:** connect INP-005 player sampling and remaining camera/UI bindings, add the needed DEV-008 harness entry, then execute FND-017: imported Fighter, approved World lighting, scene/pause context ownership, launch → spawn → fly → collide → save/load in the running game, with screenshots. Flight dynamics and camera tests exist; real player input and renderer integration are still incomplete. Continue ready M1 work and the system review; advisory performance misses never block subsequent phases. Detailed decisions, measurements and outstanding findings are in [LEDGER.md](LEDGER.md).
3. **Art track:** finish ART-007's validator on its existing branch when convenient; preserve all owner-only art restrictions. No AI-generated assets or unapproved new 3D ship designs.
