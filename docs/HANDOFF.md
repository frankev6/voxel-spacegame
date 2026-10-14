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

- DEV-021 reopened after owner playtest: most original pads were informational, and entry/console access was missing. Direct World Play now auto-enters; visible Console and Enter playground controls plus `tools/playtest/playground.sh` exist. Real inventory/settings/merchant screens and checkpoint course replace inert circles; 15 focused cases passed and live entry/trading verified. Console `playground reset` restores trials. Seven working areas including the real fleet screen (playground goto=fleet then F); campaign/story/editor are still absent. Fleet live screenshot verified after label/meter layout fixes.
- GAL-012 galaxy content and EDT-020 blueprint store/browser accepted; save/load/jump commands pass their three cases, but DEV-018 menu-route acceptance remains HUD-002-dependent. TRV-011 source/unit checks integrated; actual R30 run remains. Lead fixed live pink props caused by editor material-buffer layout. Exact hashes/results and test counts are in LEDGER newest log.
- Same three Sol6.1-high workers delivered inventory/merchant screens and opening content; now implementing escort hooks TRV-014, notification/hotkeys HUD-010 and loading/dock HUD-019. Main menu HUD-003, flight HUD HUD-007 and pause HUD-006 source delivered, pending integration. Territory/dialog/fleet accepted in the compatible batch; game left live at fleet screen. Preserve live game, owner QualitySettings and two untracked content metas. Full roadmap remains active; no broad test/performance loop.

## 3. Next

ENC-003 faction runtime, ECO-011 cargo capacity and HUD-018 editor host are accepted (16 focused cases). Playground combat now uses a genuinely hostile Raider faction. ENC-004 faction service/reset/save is now accepted (six service + two integration checks), including sandbox faction reset. EDT-008 connectivity is accepted with six required cases (97-case editor batch plus corrected leak-case rerun). EDT-009 full validation/remove-floating is accepted (11 required cases). Territory/dialog/fleet are accepted (16 distinct focused cases across batch/fixes; live fleet screenshot). EDT-011 atomic commit is accepted (eight required cases plus ten inventory regressions); ADR-0096 restores exact cargo on rejection. EDT-019 overlays are accepted (four required checks, Metal shader and 65,536-instance draw). EDT-022 host/input/presenter accepted (six required cases; native ownership scope documented in ledger). Next EDT-023 launcher and actual world return, then wire remaining features into the playground. Receive/recycle the exact three workers in ORCHESTRATION, validate compatible deliveries together, and complete frontier editor connectivity/validation/commit/overlays/host and real root navigation. Run R30 once with the integrated travel hooks; finish DEV-018 menu-route acceptance when real menu roots are installed. Never represent unfinished integrations as working triggers.
