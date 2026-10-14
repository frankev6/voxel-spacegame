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

- Playground has seven functional areas, visible Console/entry controls and `playground reset`; direct World Play auto-enters. Real inventory/settings/trading/fleet interactions are wired. Campaign/story/editor pads remain pending actual interactions.
- 057f29ea installs real flight HUD, main-menu Blueprint Lab entry, persistent editor/profile storage and correct scene input roots. HUD-002's four required cases pass; live HUD and Lab screenshots verified. EDT-023 real Lab save/reload/return now passes; four fleet/dialog scenarios remain open. HUD-003 accepted b9f9cb46 after repairing test-panel lifetime; actual Continue passes.
- Exact three Sol6.1-high chats are actively implementing ENC-012 encounter commands, ENC-009 narrative/faction routing and ENC-013 fleet formations from main57dbe986 (ADR-0100). FND-015 defeat/startup and ENC-007 director are accepted; encounter save persistence f9c6f0d5 is accepted (3 binary checks plus real save/load). Preserve owner QualitySettings and two untracked content metas. Focused validation only; evidence in LEDGER.

## 3. Next

ENC-007a445825b is accepted: all ten functional cases pass across batch/fix, with two advisory fixtures authored/deferred. ENCT persistence is accepted f9c6f0d5, including same-ID story respawn and combat save refusal. Lead next integrates worker encounter commands/narrative/formations as their deliveries arrive. Integrate their compatible deliveries and finish fleet-editor/remaining playground features; retain original full roadmap.

Finish real fleet edit/return scenarios and a usable editor playground interaction. Defeat recovery is now integrated and verified. Integrate compatible territory/escort/hotkey deliveries; unblock frontier EncounterDirector, dialogue and map owners to feed the same three workers substantial standard features. Do not represent pending scene scenarios or unimplemented pads as working. HUD-009 production binding is now live; three opt-in performance fixture definitions remain, measurements advisory. DEV-018 actual menu-route and TRV-011 R30 acceptance remain open.
