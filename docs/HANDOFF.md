# Handoff: start here if you're continuing the work


**Active correction (2026-11-06):** Frank rejected closing the performance goal on subsystem benchmarks. TST-011 resumes actual average FPS and end-to-end game loading optimization; the previous round's measured gains remain valid, but the requested integrated outcome is not complete.

**Status: living.** Read this first, then [AGENTS.md](../AGENTS.md). It covers the owner's standing rules, where the work stands and what comes next.

- Whoever ends a working session updates *Where things stand* and *Next*. Keep each to a few lines and link to the ledger rather than repeating it.
- Task status lives in [LEDGER.md](LEDGER.md), and decisions in the ADRs and specs. This page only points to them.

## 1. The owner's standing rules

Frank (the owner) set these for every worker. AGENTS.md §2 makes the git and art rules binding.

| Rule | In practice |
|---|---|
| Commit locally; push only on the owner's word | `origin` is the public GitHub repo `frankev6/voxel-spacegame`. Merging to local `main` is fine; any push needs Frank's go-ahead |
| Use the Unity CLI where possible | `tools/*.sh` and the CLI over manual Editor work ([08-tools.md](08-tools.md)) |
| Keep the ledger current; record owner decisions verbatim | Quote Frank's words in the ledger log and in the *Answer* column of open questions |
| Fast validation, owner correction 2026-10-13 | Focused functional acceptance per compatible batch; broad regression at gameplay checkpoints; benchmarks/soaks opt-in. No repeated three-minute test loops or worker queues behind unrelated tests. ADR-0070 supersedes older full-suite requirements below |
| Fast fixtures, owner correction 2026-10-13 | Use weaker targets, faster firing/higher damage and short distances in tests where those values are incidental; bounded fixed ticks, no wall-clock combat waits. Preserve assertions and production balance; batch checks after implementation |
| Three reusable separate worker threads authorized 2026-10-13 | GPT-6.1-Sol high, isolated worktrees; lead reviews, fixes and integrates; no subagents. See ADR-0062 and ORCHESTRATION.md |
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

## 2. Where things stand (2026-11-10)

- **Latest: the extraction loop (SPEC-93, ADR-0124), built overnight 2026-11-10 on Frank's word** ("proceed with building it onto the game, write the systems first ... be autonomous and test on your own as well"). Local commits only, on `task/EXT-008-den`, merged to local `main`:
  - the systems (EXT-002–006, 008): the magnet tow, the cargo rule and stowing, the station core on tethers with district blackouts, transponders, watchers, suspicion and Wanted, the alarm director, the den's socket and power;
  - the places in the playground (EXT-007–010): Tollhouse Nine at (+9 km, −7 km) and the Rock at (−6.5 km, +5.5 km), joined by a lane-gate pair; the board's six jobs; dig, build and the den attack. `extraction status`, `goto=den|lantern|table|cradle|<poi>`, `tools/build-extraction.sh` rebuilds the content;
  - verified live: dig, plate, gun, cut, alarm, tow, stow, gate, socket, Installed; `ExtractionLoopTests` (PlayMode) passes. Open defaults are SPEC-93 §10 questions 6–11.

- **Latest: owner-directed performance round completed locally.** Lead + two Astra light workers audited runtime/loading/authoring/tooling. Largest Hollow blueprint reconstruction is **3.16x faster** (19.75 → 6.24 ms median), catalog refresh 2.04x, integrity selection 5.81–10.90x, box removal 1.81x. Quality and physics settings unchanged. Combined 52 tests and final overlapping 32 codec tests pass; compile clean. [Audit, evidence and limitations](performance/2026-11-audit.md). No claim of doubled whole-game FPS; no push.

- **Last game work: 2026-11-04/05, by Daniel, the second developer** (pushed to `origin/main`):
  - the control sheet (DEV-029) and its Flight configs panel (DEV-030);
  - experimental 3D flight (FLT-016, ADR-0123; DEV-031 `flight3d`) and the `freeaim` camera (CAM-013);
  - playground entry from any scene (DEV-032);
  - grouped settings controls (HUD-021).
- **Before that:** exploration slice phase 1 on 2026-10-21 (TRV-015 in-world cruise, ADR-0122; DEV-028 the Hollow system).
- Fetch and merge `origin/main` before starting: Daniel pushes there. The newest LEDGER log entries have the detail.
- **Since QA-001 (2026-10-14), all merged:**
  - QA-001 itself;
  - co-op (NET-001…019, ADR-0117);
  - racing and hazards (SPEC-91, SPEC-92, ADR-0118–0121);
  - the playground hub, then the Hollow as the playground;
  - the Xeno swarm scenario;
  - DMG-020 and ABL-006;
  - the feel prototype (auto-aim, camera presets, VESHA);
  - the HUD redesign;
  - phase 1.
  [HANDOFF-ASTRA.md](HANDOFF-ASTRA.md) summarises each.
- **Known issues:**
  - loading a save can fail with "No participant restore is active" (`GameSession.RestoreFrom`, `GameSession.cs:259`);
  - `tools/check-docs.py` reports DMG-020 undefined in any spec;
  - EditMode tests already failing on `main`: `ConventionsTests`, `GamepadTests.AllFlightActionsBound`, `SettingsViewTests.Slider_PersistsImmediately`, `ThemeFilesTests` (toast.uss). [HANDOFF-ASTRA.md](HANDOFF-ASTRA.md) §5 has the details;
  - the heat zone does nothing until phase 6.
- **Paused:** the three GPT-6.1-Sol workers in [ORCHESTRATION.md](ORCHESTRATION.md) (paused since 2026-10-14). That page is out of date.
- **Uncommitted on purpose:** the URP global settings asset that Unity rewrites on open.

## 3. Next

Performance follow-ups are ranked in the [audit](performance/2026-11-audit.md). Further runtime tuning needs a focused integrated capture (render/presentation waits and sampler overhead); codec has used two initial passes. Continue the gameplay priorities below unless Frank directs more optimization.

1. Extraction: fly each job's route out of Tollhouse Nine (EXT-007 acceptance asks for four of six), then co-op replication (EXT-012); Frank answers SPEC-93 §10 questions 6–11.
2. (Superseded by the extraction pivot, 2026-11-10) Exploration slice phase 2: "?" contacts and the loud ping. Phases 3–7 follow in order ([exploration-slice.md](design/exploration/exploration-slice.md)), each proven by a playground scenario before the next.
3. Fix the save-load error above.
4. Still waiting on Frank:
   - art approval of the magnet block, the UI sounds and the baked plating and debris textures (`docs/design/asset-manifest.csv` status `review`);
   - whether small debris chunks drop scrap.
5. Not done yet:
   - navigation strobes (per-grid hull anchors);
   - GRAVID, LAMPREY and CRINOID;
   - per-faction team colours in the pooled path.

An independent reviewer still starts at [scope and access only](QA-REVIEWER-PROMPT.md).
