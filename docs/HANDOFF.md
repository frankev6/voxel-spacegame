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

- Playground has seven functional areas, visible Console/entry controls and `playground reset`; direct World Play auto-enters. Real inventory/settings/trading/fleet interactions are wired. Production flight HUD and creative Lab work; four fleet/dialog editor scenarios remain open. Campaign/story/editor pads are not advertised as complete.
- This batch accepted ENCT persistence f9c6f0d5, encounter commands a9adf708, authored formations078e8058, narrative routes aae617cd and live voxel overlays53b1466a. All focused functional checks passed; compile clean. Overlay screenshot: Logs/playtest/voxel-overlays-live.png. F1: `overlay name=bricks`, `overlay name=colliders`, `overlay name=none`.
- DLG-007 d835fa49 now supplies the actual compiled dialog/service/presenter: eleven conformance checks pass and live opening choices work (Logs/playtest/dialog-runner-live.png). Same three Sol6.1-high chats implement DLG-011 input scenarios, QST-006 narrative adapter/log/markers and ENC-011 encounter scenarios; combined DEV-013/014 bdbefa75 delivered for integration. BeatService30762271 is accepted with nine functional checks. Missing BeatService/bridge composition remains explicit for automatic story scenarios. Preserve owner QualitySettings and two untracked content metas. Exact status/evidence in LEDGER and ORCHESTRATION.

## 3. Next

Keep all three recorded workers on their current substantial assignments and integrate compatible deliveries locally. Lead next compatible overlay integration and remaining narrative registration prerequisites and remaining real playground/editor interactions; no substitute owners or text-only feature pads. ENC-011's actual test-dialog scenario still depends on QST-008; DLG-007 is accepted; other real gameplay scenarios proceed.

Retain full roadmap. Pending compatible deliveries include territory e0157b39, escort2e03c4cb and HUD hotkeys aee6a04d (its ADR number needs reconciliation). HUD-019 needs actual ECO-008 trade launcher; ECO-007 salvage is still missing. HUD-009 production binding is live, with three opt-in performance fixture definitions remaining. Focused batches only, performance advisory, no routine full suites or soaks.
