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
- **UI foundation:** UI-001 (`01dcff0`) and UI-002 (`a494f84`) are merged: exact contracts, 123 shared classes, token-based theme, 1920×1080 panel and six-layer non-picking root. Services/controls remain explicit shells pending their owning tasks; these assets are not yet wired into boot. Latest suites: **714 EditMode passes + one existing advisory miss + one explicit skip**, **71 PlayMode passes + 12 GPU skips**, zero C# warnings. No new visual checkpoint is claimed.
- **Integration decisions:** accepted ADR-0044 gives a temporary Game.App scene/pause bridge sole input-context ownership, including Escape pause. **UI-004 must unbind/remove it before the navigator takes over.** Accepted ADR-0045 drains the existing physics initialization/cleanup at zero delta during paused teardown; the live restart exposed package cleanup entities surviving otherwise. Trap T2.15 records it. Neither change advances gameplay while paused.
- **Milestones:** M0 passed (71/71), with the in-session system review authorized by Frank; M1 continues. Renderer through REN-011, flight dynamics, physics/collider lifecycle and camera follow are integrated. Renderer timing remains unavailable/deferred and its two-pass initial tuning allowance is exhausted. MeshBuild128 remains around **0.88–0.92 ms** against the advisory 0.5 ms target, with zero tuning passes. Do not reopen benchmark work. All details and test results are in [LEDGER.md](LEDGER.md).
- **Owner/art boundaries:** NOCTURNE is approved. Questions 6(b), 6(c), 7 remain owner-only; use defaults. Frank should review the provisional Fighter starter (replaces four-cell ship_base), legacy flight tuning and deferred presentation quality; no new hulls or AI art were made. Earlier review items Q30 (palette fallback), Q48 (save commit point), Q57 (thin-shell stress policy), DATA-012 tuning and ADR-0033 (collider-share allocation) remain in the ledger. ART-004 remains in review for the 150 m glyph criterion; preserve `task/ART-007-brick-tools` with its unfinished validator.

## 3. Next

**Owner priority:** build playable, integrated slices; demonstrate them in the running game with screenshots. Performance targets are advisory, with at most two initial optimization passes per system across workers/handoffs. The first imported-ship rendering/flight/collision/save-load checkpoint has now been demonstrated.

1. **UI-003 next:** implement the root/service lifecycle and then UI-004 navigation and UI-005 focus/pointer gating; UI-006 binding support is also ready. Connect this to the existing playable slice and transfer input ownership exactly once. Full HUD screens are M3 work with their existing dependencies; do not claim those done through placeholders. Existing tokens remain placeholder values until the ready ART-014 task applies NOCTURNE; UI tasks consume them unchanged.
2. **Continue ready M1 gameplay work:** UI navigation/input integration, rendering LOD/props and the remaining system review. Transfer context ownership exactly once at UI-004. Preserve the playable loop and take fresh screenshots at meaningful checkpoints. Defer remaining numeric misses.
3. **Art track:** finish ART-007's validator when convenient, preserving owner-only art gates. No AI assets or unapproved new 3D hulls.

To try the current slice with the project Editor open, run `tools/playtest/newgame.sh 42`. It enters Play from Boot if needed and creates the imported Fighter in the harness profile. Controls: W/S thrust, A/D turn, Q/E strafe, Shift boost, X brake, middle-drag orbit, wheel zoom, Escape pause/resume. Menu screens and the galaxy backdrop are not implemented yet.
