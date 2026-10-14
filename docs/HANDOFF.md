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

## 2. Where things stand (2026-10-14)

- Interactive playground DEV-021 is playable: F1 `playground`, F activates nearby rings, F1 `playground reset` resets the complete isolated sandbox; `playground goto=range` jumps to the destruction trial. Eight pads, beacon map, quiet default, opt-in enemy. Live screenshot `Logs/playtest/playground-map.png`; unfinished UI/story/editor integrations are labeled honestly.
- GAL-012 galaxy content and EDT-020 blueprint store/browser accepted; save/load/jump commands pass their three cases, but DEV-018 menu-route acceptance remains HUD-002-dependent. TRV-011 source/unit checks integrated; actual R30 run remains. Lead fixed live pink props caused by editor material-buffer layout. Exact hashes/results and test counts are in LEDGER newest log.
- Same three Sol6.1-high workers now implement merchant UI HUD-012, opening content QST-009 and inventory UI HUD-011. Preserve live game, owner QualitySettings and two untracked content metas. Full roadmap remains active; no broad test/performance loop.

## 3. Next

Keep the playground usable and wire incoming inventory/merchant screens to its interaction pads. Receive/recycle the exact three workers in ORCHESTRATION, validate compatible deliveries together, and complete frontier editor connectivity/validation/commit/overlays/host and real root navigation. Run R30 once with the integrated travel hooks; finish DEV-018 menu-route acceptance when real menu roots are installed. Never represent unfinished integrations as working triggers.
