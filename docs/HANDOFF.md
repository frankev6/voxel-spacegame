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
- **M0 Foundations: 48 of 66 tasks done** (see the [ledger](LEDGER.md) summary and log). Done:
  - Core: `CORE-001`…`012`;
  - Data: `DATA-001`…`009`;
  - Boot: `FND-001`…`007` and `FND-014`;
  - Persistence: `PERS-001`…`009`;
  - plus `INP-001`/`002`, `MIG-001`…`003`, `DEV-001`/`002`, `AUD-001`, `VOX-001` and `TST-001`.
- **What runs today:** Play from Boot creates the Default World and GameRoot. Settings, content, input, pause, save and SceneFlow are composed in order. MainMenu ↔ World ↔ BlueprintLab transitions tear everything down cleanly. SimClock ticks in the World and stops on pause. Saves and loads work end to end (atomic, with backup and never-reject loading), with autosave and a quit save.
- **Not yet:** no gameplay. There are no voxel ships, rendering, flight, combat or UI screens: those are M1+ (voxel model and renderer first). The scenes are still empty.
- **Spec defects found and fixed while building**, each with a resolved open question in its spec and the ledger (questions 17–31):
  - nested native containers, and padding in a sort hash (SPEC-02);
  - test rate managers (SPEC-71);
  - the settings applier, the start blueprint, the `WorldTime` singleton destroyed by teardown, and the static audit scope (SPEC-01);
  - deferred-block disposal, millisecond timestamps, and newer chunks on resave (SPEC-05);
  - VOXS runtime-state reconstruction, bounded decoding and the full-palette Missing fallback (Q30); managed save workers require TempJob rather than Temp scratch (Q31).
- **Latest implementation:** `PERS-007` provides deterministic VOXS encoding/decoding and `PERS-008` adds immutable native snapshots. Worker save/load and success/failure disposal paths are covered. Codec 1M-cell encode/decode measured about 13/15 ms on the Apple M5 Pro.
- **Frank should review:** Q30's exceptional full-palette fallback (slot 32767 becomes Missing) and 256 MiB decoded body/cell limits. These use the authorized tech-lead default and do not block work.
- **Still open for Frank:** ledger questions 6 (b) large hull blocks, 6 (c) smooth alien cells, 7 factions and rosters. None blocks M0.
- **Branches:** `main` holds everything except `task/ART-007-brick-tools`.
- **Reviews:** no per-task reviews (ADR-0019). The M0 system review runs before the M0 exit gate. `ART-004` stays in `review` for its 150 m glyph criterion.

## 3. Next

1. **M0, remaining 18:**
   - ready now: `PERS-010`/`011`/`012`, `MIG-004`, `MIG-007`, `DEV-003`, `INP-003`, `AUD-002`, `TST-008`;
   - then `MIG-005`…`016`, `DEV-004`…`006`/`020`, `AUD-003`.
2. **`ART-007`:** finish the art validator on its branch and merge it.
3. **M0 system review** (04-workflow §8), then the M0 exit gate. After that, M1: the voxel model, renderer and physics, the first visible ships.
4. **Art track:** `ART-014` applies the NOCTURNE tokens and fonts in Unity (A1).
