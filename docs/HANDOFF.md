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

## 2. Where things stand (2026-10-11, M0 in progress)

- **Design gate passed.** NOCTURNE is binding, as [DESIGN-GUIDE.md](design/DESIGN-GUIDE.md) (`ART-013`). The slope kit is in [ADR-0018](decisions/ADR-0018-slope-block-kit.md).
- **M0 Foundations: 37 of 66 tasks done** (see the [ledger](LEDGER.md) summary and log). Done so far:
  - Core: `CORE-001`…`CORE-012` (all);
  - Data: `DATA-001`…`DATA-009` (all);
  - plus `FND-001`…`FND-004`, `INP-001`/`002`, `PERS-001`/`002`, `MIG-001`…`003`, `DEV-001`/`002`, `AUD-001`, `VOX-001` and `TST-001`.
- **The test kit exists** (`Game.Tests.Common`, TST-001): `EcsTestWorld`, `GameTestBase` and `PerfRun`. `tools/test.sh perf` ends with a PASS/FAIL table, and every perf gate so far passes. See [08-tools.md](08-tools.md) §3.
- **Boot works** (FND-002): `Bootstrap` creates the Default World, then `GameRoot` composes settings, input, pause and the settings applier. `SceneFlow` (FND-006) is next on that path.
- **Spec defects found and fixed while building**, each with a resolved open question in its spec and in the ledger (questions 17–23):
  - SPEC-02 nested native containers and padding in the event-sort hash;
  - the SPEC-71 rate managers;
  - the SPEC-01 settings applier.
- **Still open for Frank:**
  - ledger questions 6 (b): large hull blocks;
  - 6 (c): smooth alien cells;
  - 7: factions and rosters.
  None of these blocks M0.
- **Branches:** `main` holds everything. Only `task/ART-007-brick-tools` is unmerged: its art validator now has all its dependencies (`CORE-002`, `DATA-004`, `DATA-005` are done).
- **Reviews:** no per-task reviews (ADR-0019). The M0 system review runs before the M0 exit gate. `ART-004` stays in `review` for its 150 m glyph criterion.

## 3. Next

1. **M0, going wide** (ready now: `PERS-003`, `PERS-007`, `MIG-004`, `MIG-007`, `DEV-003`, `INP-003`, `AUD-002`, `TST-008`):
   - then `FND-005` → `FND-006` → `FND-007`/`FND-014`;
   - the persistence chain `PERS-004`…`PERS-012`;
   - the migration chain `MIG-005`…`MIG-016`;
   - the dev harness `DEV-004`…`DEV-006`.
2. **`ART-007`:** finish the art validator on its branch and merge it.
3. **M0 system review** (04-workflow §8) once the M0 systems exist, then the M0 exit gate.
4. **Art track:** `ART-014` applies the NOCTURNE tokens and fonts in Unity (A1). The in-engine look test (`ART-008`…`ART-011`) waits for the voxel renderer (M1).
