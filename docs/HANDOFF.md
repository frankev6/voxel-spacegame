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

## 2. Where things stand (2026-10-11, M0 system review in progress)

- **Design gate passed.** NOCTURNE is binding, as [DESIGN-GUIDE.md](design/DESIGN-GUIDE.md) (`ART-013`). The slope kit is in [ADR-0018](decisions/ADR-0018-slope-block-kit.md).
- **M0 Foundations: 67 of 67 tasks done, including TST-009; system review in progress** (see the [ledger](LEDGER.md) summary and log). Done:
  - Core: `CORE-001`…`012`;
  - Data: `DATA-001`…`009`;
  - Boot: `FND-001`…`007` and `FND-014`;
  - Persistence: `PERS-001`…`012`;
  - plus `INP-001`…`003`, `MIG-001`…`008`/`016`, `DEV-001`…`006`/`020`, `AUD-001`…`003`, `VOX-001` and `TST-001`/`008`.
- **What runs today:** Play from Boot creates the Default World and GameRoot. Settings, content, input, audio, pause, save and SceneFlow are composed in order. MainMenu ↔ World ↔ BlueprintLab transitions tear everything down cleanly. SimClock ticks in the World and stops on pause. Saves and loads work end to end (atomic, with backup and never-reject loading), with autosave and a quit save.
- **Not yet:** no gameplay. There are no voxel ships, rendering, flight, combat or UI screens: those are M1+ (voxel model and renderer first). The scenes are still empty.
- **Spec defects found and fixed while building**, each with a resolved open question in its spec and the ledger (questions 17–46):
  - nested native containers, and padding in a sort hash (SPEC-02);
  - test rate managers (SPEC-71);
  - the settings applier, the start blueprint, the `WorldTime` singleton destroyed by teardown, and the static audit scope (SPEC-01);
  - deferred-block disposal, millisecond timestamps, and newer chunks on resave (SPEC-05);
  - VOXS runtime-state reconstruction, bounded decoding and the full-palette Missing fallback (Q30); managed save workers require TempJob rather than Temp scratch (Q31).
- **Migration:** MIG-007 converts all cube Euler rotations; MIG-004 adds the protected asset writer, manifest, runner and reports. Q34 prevents an empty-link fill from making hand-edited fields overwritable. MIG-005/006 import all **35 items and 21 blocks**, with exact values and stable repeated bytes. Q37 configures existing art sources; Q38 fixes stable module-reference IDs. MIG-008 adds all **13 ship blueprints**, with exact expected counts and verified VOXS; MIG-016 gates repeated real imports. Latest live EditMode is **395 passed** (explicit guard excluded); PlayMode **31**, perf **23/23**. INP-003 supports rebinding with conflict/escape protection. TST-008 measures **1.70 s compile / 0.16 s Play-to-menu / 29.80 s EditMode**; both reloads are disabled (ADR-0020). Live CLI discovery/results handling is fixed and process failures are no longer hidden by XML. A previous headless perf shutdown crash remains recorded for M0 review; live runs did not repeat it. TST-009 now serializes tool operations and validates exact live-test inventories; real overlapping requests were refused safely. Raw GUI/CLI calls still bypass the wrapper lock and Pipeline supplies no run ID.
- **Dev harness:** DEV-003…006 compose the host, commands, async operations, log/event capture, timed input, UI query, typed scenario language and runner. CLI and shell runs produce JSON with verified 0/1/2 exit codes, including interrupted runs. Q43 stages future event/player/camera types; Q44 requires UI-003 to register its owned UIDocument. Q45 fixes grammar; Q46 prevents result-file collisions. DEV-020 verifies real release assemblies and rejects serialized dev components; both missing-guard and prefab mutations were caught.
- **Audio core:** AUD-002/003 supply the five-channel mixer, import settings (SFX 8.79 MiB), one listener and 60 pooled voices, volume/pause/follow/disposal. Q41 uses a temporary Editor mixer-authoring job; Q42 keeps M0 silent until AUD-007 authors config_audio/cues in M2.
- **Latest persistence implementation:** `PERS-007` provides deterministic VOXS encoding/decoding and `PERS-008` adds immutable native snapshots. Worker save/load and success/failure disposal paths are covered. `PERS-011` verifies byte-identical save/load/save into fresh binary, JSON and voxel participants, with unknown chunks preserved. `PERS-010` commits immutable v1 fixtures and proves removed content loads successfully with two explicit warnings. `PERS-012` measures all M0 save gates: reference ship ~15 KB compressed, capture well below 5 ms, typical read/restore ~15 ms, and 50-slot listing ~3 ms on the Apple M5 Pro. Full measurements are in the ledger.
- **Frank should review:** Q30's exceptional full-palette fallback (slot 32767 becomes Missing) and 256 MiB decoded body/cell limits. Q40 uses `ship_base` as the temporary development starting ship; Frank should choose the gameplay starting ship later. Q39 moves the existing blueprint validation body into M0 so content validates. These use the authorized tech-lead default and do not block work.
- **Still open for Frank:** ledger questions 6 (b) large hull blocks, 6 (c) smooth alien cells, 7 factions and rosters. None blocks M0.
- **Tool state:** a task-owned headless Editor is open for fast live CLI work (log `Logs/Editor.log`); close it when no longer useful. Live tests may serialize dynamic font glyph caches; restore that incidental diff before commits.
- **Branches:** `main` holds everything except `task/ART-007-brick-tools`.
- **Reviews:** no per-task reviews (ADR-0019). The M0 system review runs before the M0 exit gate. `ART-004` stays in `review` for its 150 m glyph criterion.

## 3. Next

1. **M0 system review:** all 67 tasks are done, including the shared live-test result fix. Review integrated boot/save/migration/harness flows and the recorded headless shutdown anomaly, then record the exit gate. Frank explicitly requested a review by this session.
2. **`ART-007`:** finish the art validator on its branch and merge it.
3. After the M0 review and exit gate, M1: the voxel model, renderer and physics, the first visible ships.
4. **Art track:** `ART-014` applies the NOCTURNE tokens and fonts in Unity (A1).
