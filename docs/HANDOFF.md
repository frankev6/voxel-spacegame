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

## 2. Where things stand (2026-10-12)

- **Playable slice:** the imported **1,005-cell Fighter** renders through the production hull/functional renderer, flies with real controls, collides, and survives save/load. Actual evidence: `Logs/playtest/flight-checkpoint/`, `fighter-after-save-load.png`, `fighter-contact.png`. FLET v1 restores cells/id/hash; pose and velocity reset until canonical world-location persistence lands. HUD and functional art remain provisional.
- **Current game view:** `Logs/playtest/fighter-flight-replay.png` (DEV-016 live playback); restart regression reference `fighter-restart-sky-fixed.png`. FND-018's approved procedural animated Nocturne sky, separate **1,400 m radius** VESPER-9 sphere/atmosphere and **1,400** stationary debris particles provide spatial reference. Frank said **“no don't reduce its good”**: preserve brightness/star density (DESIGN-GUIDE decision 7). FND-019 (`8f80083`) fixes direct-NewGame sky lifetime; three consecutive live restarts pass with zero errors. SYS-011 replaces the staged planet; no landing/collision/save identity yet.
- **Main work resumed:** UI-007 shared controls, INP-006 camera/editor sampling (ADR-0048), INP-007 frame-rate input checks, REN-012 LOD/dormancy, REN-014 prop instancing, AUD-005 UI/cue playback, DEV-015 console and VFX-001 effect contracts are implemented. REN-012 live approach switched **4/4 Fighter bricks LOD1 → LOD0**, with real boosted flight at **72.65659 m/s** (`fighter-distance-lod.png`). REN-014 (`37097fa`) renders applied prop cells on moving grids, follows their bounds and removes deleted cells; all **four GPU tests pass**, including real snapshot/mesh/apply integration. Proof: `Logs/playtest/ren014/`. VFX-001 is contracts only (FxRig implementation is VFX-002); no plumes yet. Audio playback exists but the game remains silent until AUD-007 supplies config/cues. AUD-010 inventories 19 clips (2 Blocked, 17 Unverified); AUD-011 retains the owner licence gate without blocking development.
- **Controls/UI:** UI-001–007 navigation, pause, text focus and pointer isolation are integrated. CAM-012 keeps the full occupied hull framed during fast close-zoom flight. INP-007 gives identical **4 Interact / 3 Ability1 / 60 thrust ticks** at 30/60/144 FPS. Blueprint Lab host is EDT-024; HUD-002 must replace ADR-0047's temporary navigator adapter/views. ADR-0045's paused physics cleanup (T2.15) remains required. WPN-001 and AI-001 contracts exist; combat/AI behavior is still M2.
- **Latest validation:** compile zero C# warnings; full EditMode **765 passed, 2 advisory timing misses, 1 explicit skip**; headless PlayMode **105 passed, 3 advisory timing misses, 23 GPU-only skips**. REN-014 final focused GPU run **4 passed**. 10,000 props/20 moving grids cost **0.01465 ms median**, rebuild **0.2838 ms**, with zero tuning passes. Write1MiB **3.4491 ms** vs 3 and MeshBuild128 **1.0483 ms** vs 0.5 remain deferred. The renderer's earlier two-pass tuning allowance is exhausted; do not reopen benchmark troubleshooting. Latest PlayMode timing misses are Propulsion50Ships **0.1939 ms**, ThrusterRebuild **3.6237 µs** and Hit1MColliderJobs **0.6844 ms**; no functional failures or tuning. Details and exact commits live in [LEDGER.md](LEDGER.md).
- **Milestones/owner boundaries:** M0 passed **71/71**; M1 continues. No new owner decision in the latest tasks. Existing review items remain Q6(b), Q6(c), Q7, Q30, Q48, Q57, DATA-012 tuning, ADR-0033, the provisional Fighter starter/legacy flight tuning and CAM-012 close-zoom feel. Use documented defaults. ART-004 remains in review for the 150 m glyph criterion; preserve unmerged `task/ART-007-brick-tools` and its unfinished validator. No new hulls or AI assets.

## 3. Next

**Owner priority:** build playable, integrated slices and demonstrate them in the running game. Performance targets are advisory, with at most two initial optimization passes per system across workers/handoffs. The imported-ship rendering/flight/collision/save-load checkpoint is demonstrated. Continue the main work; preserve the approved environment.

1. **DEV-019 six regression drafts next**, then M1 system review. DEV-007 (`8f8adc3`) discovers active scenario files automatically; canonical Boot smoke passes, a newly added case is discovered and a future-command draft is ignored (`Logs/playtest/dev007/`). DEV-016 (`f1f6cd2`) now exposes console/CLI recording and replay: the real seed-7 Fighter ends at exactly the same local position after **300 ticks (0 m error)**. A live **1,790-tick** recording completes through the CLI; reusable `TestData/Replays/fighter_flight_demo.pcr`, screenshot `fighter-flight-replay.png`, evidence `Logs/playtest/dev016/`. Q175 limits the M1 check to local coordinates and controlled tick endpoints; no universe position is fabricated. TST-004 (`a56c266`) reports station idle **0.224481 ms**, readiness **15 frames**, max spawn **5.571041 ms**, accounted reference memory **36,180,776 B**; total memory **inconclusive** (Q174), zero tuning.

2. **Finish ready M1 integration and system review:** Automatic scenario execution and drafts, then the built M1 system review (record advisory gaps without more tuning). Continue toward M2 combat. DEV-009 is blocked despite contract-only Ready: implemented FND-012 and SPEC-30/31 coordinates/current-system state are missing (Q155). Preserve its scope; no alternate spawner or fabricated universe metadata. Investigate the four persistent allocations reported only at GPU Editor shutdown in FND-019, DEV-015 and DEV-016 logs during the system review; runtime error checks were clear. Later ready work can proceed past owner-only gates with pending sign-off recorded.
3. **Art:** finish ART-007's validator when convenient; preserve owner-only art gates. ART-014 owns NOCTURNE tokens; no AI assets or unapproved new 3D hulls.

To try the current slice with the project Editor open, run `tools/playtest/newgame.sh 42`. It enters Play from Boot and creates the imported Fighter in the harness profile. W/S thrust, A/D turn, Q/E strafe, Shift boost, X brake, middle-drag orbit, wheel zoom, Escape pause/resume. Menu screens and canonical galaxy streaming are not implemented yet. For screenshots keep the Editor unpaused (Editor pause stops pooled hull submission); an owned IPauseService token may pause simulation while rendering continues.
