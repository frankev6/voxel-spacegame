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

## 2. Where things stand (2026-10-14, QA-001)

- **QA-001 on `task/QA-001-owner-findings`** (local only, 25+ commits on top of DEV-021). Frank's plan, verbatim: "1. you fix everything mentioned in your findings and review 2. QA 3. I review 4. we start working on m4 when everything is solid here". Steps 1–2 are done; **step 3, Frank's review, is next**. See the newest LEDGER log entry.
- Done:
  - owner findings: flight feel (ADR-0114), salvage, radar, editor, modal order, shake, hits, shield, inventory, sounds and the boundary snap;
  - review findings: coreless blueprints, the paused-opening test root cause, the external-pause blocker, stale TODOs and docs repair;
  - the NOCTURNE visual pass: plating, machinery, team colours, post, UI skin and the menu backdrop;
  - the second polish pass: the look book's plating baked to textures (`ArtSource/Bricks/bake_plating.py`; rerun it and Game/Art/Bake NOCTURNE Brick Look after changing `ld_plating.py`), ambient and reflections for pooled draws, §4.1 light azimuths, and a light rig in the Lab;
  - Xeno grown hulls (ADR-0115: HUSK, BROODMOTHER, playground pad 15, campaign xeno encounters).
- Workers stay paused. Owner QualitySettings and two untracked content metas remain intentionally uncommitted.

## 3. Next

An independent reviewer still starts at [scope and access only](QA-REVIEWER-PROMPT.md).

1. Frank reviews QA-001 in the playground: pads 01–16, with 15 for the aliens and 16 for the new NOCTURNE WRAITH frigate and REQUIEM cruiser; the main menu; a new campaign.
2. Waiting on Frank:
   - download approval for the OFL fonts (AST-004);
   - art approval of the procedural organ meshes, the magnet block, the UI sounds and the baked plating textures (`docs/design/asset-manifest.csv` status `review`);
   - Q125 flight balance by play.
3. Not done yet:
   - navigation strobes, which need per-grid hull anchors;
   - GRAVID, LAMPREY and CRINOID (the builder supports them);
   - per-faction team colours in the pooled path (currently material-wide ARK).
4. Merge to local `main` after Frank's review. M4 starts only when Frank says the M1–M3 base is solid.
