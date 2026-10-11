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

## 2. Where things stand (2026-10-11)

- **Design gate passed.** Frank approved NOCTURNE, written up as [DESIGN-GUIDE.md](design/DESIGN-GUIDE.md), the binding source of truth (`ART-013`, 2026-10-11).
  - [DESIGN-SYSTEM.md](design/DESIGN-SYSTEM.md) and the [look book](design/lookbook/) stay as reference.
  - The ROADMAP's design-first hold is lifted, so M0 may start.
- **Slope kit chosen:** cube, slope, hip corner and long slope 1×2 ([ADR-0018](decisions/ADR-0018-slope-block-kit.md)).
  - SPEC-04 §4.2 is updated.
  - SPEC-04 open question 14 and SPEC-11 open question 10 hold the per-cell shapes and the snapshot bits.
- **Still open for Frank:**
  - ledger open question 6 (b), large hull blocks (2×2×2, 3×3×3) as single placeable blocks;
  - 6 (c), smooth alien cells;
  - question 7, the faction and roster proposals.
  None blocks M0.
- **Branches:**
  - `main` is at round 3 of the look book (`e25acbc`).
  - `task/design-guide` contains `task/lookdev-r4` and fast-forwards `main` cleanly: it holds round 4, the design guide, the gate, the slope study, ADR-0018 and this page.
  - `task/ART-007-brick-tools` (`083c482`) is not merged. `ART-007` is in progress: its art validator waits for `CORE-002`, `DATA-004` and `DATA-005`.
- **Reviews:** per-task reviews are gone (ADR-0019); `ART-004` stays in `review` for its open 150 m glyph criterion.
- **Done in M0:** `CORE-001`, `CORE-004`, `DATA-001`, `DATA-002`.

## 3. Next

1. Fast-forward `main` to `task/design-guide` (locally).
2. **Start M0 Foundations.** The ledger shows 17 M0 tasks ready.
   - The `standard` ones include `FND-001`, `CORE-010`, `CORE-011`, `INP-001`, `DATA-004`, `VOX-001`, `AUD-001`, `DEV-001`, `DEV-002` and `MIG-001`.
   - The `frontier` ones (`CORE-002`, `CORE-005`, `CORE-006`, `CORE-008`, `CORE-012`, `DATA-003`, `TST-001`) need a frontier-class model and a review.
3. **Art track:** `ART-014` applies the NOCTURNE tokens and fonts in Unity (A1).
4. **Later:**
   - the in-engine look test (`ART-008`…`ART-011`) runs once the voxel renderer exists (M1);
   - more ships get built in 3D only when Frank picks them.
