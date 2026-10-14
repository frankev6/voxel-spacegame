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

- Playground has eleven functional areas, visible Console/entry controls and `playground reset`; direct World Play auto-enters. Creative Lab: `playground goto=editor`, F; exit Lab to MainMenu and reset to return. Story/log preview: `playground goto=story`, F; supplies: `playground goto=survey`, F once per unit; rumor map: `playground goto=map`, F. Live screenshot Logs/playtest/playground-story-live.png. Corrected controls, Nocturne, actual destruction/combat, inventory, trading and fleet remain integrated.
- EDT-02371bbad74 accepted: all five actual station-dialog/fleet editor round trips pass in4.2s, including removal/commit, pose/identity, repeat interaction, three trips and Lab save. Successful dev interaction now releases its simulated key immediately. Full evidence and earlier accepted systems are in LEDGER.
- QST-008841a3882/64156bf6 canonical story registration accepted; sandbox excludes campaign intro. QST-009f468139e imported98-node opening program. Full opening4bb3198b accepted: actual route through Whisper8.512s and save/load no-replay4.520s; real maps HUD-01647ddeaa4 and ship log HUD-014a754ae35 accepted with ten required functional checks, including actual rumor-to-map navigation. No complete campaign/story/map playground trial claimed yet.
- Exactly three recorded workers delivered QST-010, NAR-019 and DEV-021; all are accepted. They have genuine ready-standard dependency waits while lead integrates the remaining deliveries and takes next frontier work. ECO-010, ENC-011, DLG-011, TRV-011/014 and NAR-018 deliveries await lead acceptance. Full opening now passes; story/survey/map playground beacons and canonical rumor-scope repair pass the expanded entry/reset case; live F activation/log screenshot verified with zero new errors. Lead also fixed dock navigation covering dialogue; introductory ordering regression passes. Preserve owner QualitySettings and two untracked content metas; local Git only. See ORCHESTRATION.

## 3. Next

Owner sequencing correction (2026-10-14): finish and connect M1–M3 systems, then let Frank verify them before starting further M4 depth, scale or polish. Preserve already-delivered M4 work without advancing it. Current playground work exposes existing M1–M3 systems; it does not authorize new M4 features. Advisory performance work remains deferred unless an integrated gameplay defect requires it.

Keep all three recorded workers on their current substantial assignments and integrate compatible deliveries locally. Lead next compatible map/log/story deliveries and remaining gameplay scenarios; no substitute owners or text-only feature pads. ENC-011's actual test-dialog scenario can now use accepted QST-008; DLG-007 is accepted; other real gameplay scenarios proceed.

Retain full roadmap. Procedural territory461ffc36 is accepted (four required cases plus five regressions); authored seeds remain0. Pending compatible deliveries include escort2e03c4cb; HUD hotkeys/dock launchers are accepted. ECO-007 salvage is accepted; ECO-008 production registration is accepted. HUD-009 production binding is live, with three opt-in performance fixture definitions remaining. Focused batches only, performance advisory, no routine full suites or soaks.
