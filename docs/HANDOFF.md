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

## 2. Where things stand (2026-10-12, playable imported ship and repeatable launch demonstrated)

- **Playable checkpoint:** INP-005 is merged (`66702c3`). FND-017 is merged (`0e253e1`) and connects the existing **1,005-cell Fighter**, production pooled hull/functional rendering, approved World lighting, real W/S/A/D/Q/E/boost/brake input, orbit/zoom camera, pause and save/load. Actual game captures are `Logs/playtest/fighter-after-save-load.png` and `Logs/playtest/fighter-contact.png`; live evidence is under `Logs/playtest/flight-checkpoint/`. The scene currently has no HUD or star-system backdrop; functional art remains provisional.
- **Live evidence:** thrust reached **16.93755 m/s**, collision with a second imported static Fighter reversed forward velocity to **−1.803858 m/s** at z **19.72989 m**. Save/load retained player id **1**, all **1,005 cells**, and the same voxel hash **58AE4237CE09B3B7**; real W moved the loaded ship again. Final live run logged **zero errors**. FLET v1 restores ship data, but pose/velocity reset to origin/zero until canonical world-location persistence lands. Three new GPU PlayMode integration tests pass; full EditMode has **709 passes, one unchanged advisory timing failure and one explicit skip**; headless PlayMode **68 passes + 12 GPU-only skips**; compile has zero C# warnings.
- **Repeatable launch:** DEV-008 is merged (`0b4269c`): `tools/playtest/newgame.sh 42` enters Play from Boot, selects the isolated harness profile, and waits for the real player/physics/Flight context. Live edit-mode launch **1.370 s**, restart **0.229 s**, same id/position. New capture `Logs/playtest/fighter-command-launch.png`; launch evidence under `Logs/playtest/dev008/`. Full headless PlayMode now **71 passed + 12 GPU skips**; EditMode **709 passed + unchanged advisory timing miss + explicit skip**. Nonempty start-scenario selection is explicitly unavailable until implemented (Q144–146).
- **UI integrated through UI-004:** UI-001/002 contracts/theme, UI-003 boot root and UI-004 navigation (`23b3cf5`) are merged. Navigator now owns context and pause; ADR-0047 replaces the old direct writer with a temporary scene-navigation adapter. A minimal Paused/Resume overlay works in the actual game. Live launch **2.027 s**, W **14.64485 m/s**, Resume click and Escape each restore Flight/zero pause tokens, W again **21.97824 m/s**, **zero errors**. Captures: `Logs/playtest/fighter-navigation-paused.png` and `fighter-navigation-flight.png`; evidence `Logs/playtest/ui004/`. This is provisional UI, not the final HUD.
- **UI input isolation:** UI-005 (`4cdc9a9`) connects text focus/Back and world-pointer picking to real flight input. Live checks prove HUD mouse fire is suppressed, empty-world fire works, typing suppresses thrust/fire, and two Esc presses leave typing then close pause. Q153 fixes Editor-window `Screen.height` leaking into ECS picking: flip coordinates in actual panel space instead. Evidence `Logs/playtest/ui005/`; zero runtime errors. The final inspected screenshot `Logs/playtest/fighter-input-isolation.png` exposes **fast close-zoom camera clipping**, now ready CAM-012; this is the next gameplay fix.
- **Latest validation:** compile zero C# warnings; full EditMode **728 passes, 2 advisory timing misses, 1 explicit skip**; headless PlayMode **83 passes, 12 GPU-only skips**. All seven new UI input tests pass. Write1MiB **3.4669 ms** vs 3 ms and MeshBuild128 **1.0529 ms** vs 0.5 ms are deferred, with no tuning. HUD-002 explicitly replaces ADR-0047's temporary adapter/views. ADR-0045's zero-delta physics cleanup remains required during paused teardown (trap T2.15).
- **Milestones:** M0 passed (71/71), with the in-session system review authorized by Frank; M1 continues. Renderer through REN-011, flight dynamics, physics/collider lifecycle and camera follow are integrated. Renderer timing remains unavailable/deferred and its two-pass initial tuning allowance is exhausted. MeshBuild128 remains around **0.88–0.92 ms** against the advisory 0.5 ms target, with zero tuning passes. Do not reopen benchmark work. All details and test results are in [LEDGER.md](LEDGER.md).
- **Owner/art boundaries:** NOCTURNE is approved. Questions 6(b), 6(c), 7 remain owner-only; use defaults. Frank should review the provisional Fighter starter (replaces four-cell ship_base), legacy flight tuning and deferred presentation quality; no new hulls or AI art were made. Earlier review items Q30 (palette fallback), Q48 (save commit point), Q57 (thin-shell stress policy), DATA-012 tuning and ADR-0033 (collider-share allocation) remain in the ledger. ART-004 remains in review for the 150 m glyph criterion; preserve `task/ART-007-brick-tools` with its unfinished validator.

## 3. Next

**Owner priority:** build playable, integrated slices; demonstrate them in the running game with screenshots. Performance targets are advisory, with at most two initial optimization passes per system across workers/handoffs. The first imported-ship rendering/flight/collision/save-load checkpoint has now been demonstrated.

1. **CAM-012 next:** correct the camera framing revealed by real fast flight at close zoom, preserving player zoom/orbit intent; show an actual before/after capture. The current screenshot clips part of the Fighter even though it fitted while stationary. This is functional gameplay work, not numeric performance tuning.
2. **Continue ready M1 integration:** INP-004 held-key regression, UI-006 binding and UI-007 controls, rendering LOD/props and the remaining system review. UI-005 focus/pointer isolation is now complete. Preserve real flight/Escape behavior. Full HUD screens remain M3; HUD-002 must replace the temporary ADR-0047 adapter/views. ART-014 owns applying NOCTURNE tokens. Defer numeric misses.
3. **Art track:** finish ART-007's validator when convenient, preserving owner-only art gates. No AI assets or unapproved new 3D hulls.

To try the current slice with the project Editor open, run `tools/playtest/newgame.sh 42`. It enters Play from Boot if needed and creates the imported Fighter in the harness profile. Controls: W/S thrust, A/D turn, Q/E strafe, Shift boost, X brake, middle-drag orbit, wheel zoom, Escape pause/resume. Menu screens and the galaxy backdrop are not implemented yet.
