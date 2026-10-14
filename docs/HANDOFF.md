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

- **Paused for independent QA at Frank's request. Do not resume implementation, workers or scheduling until Frank explicitly resumes.** Latest three deliveries consolidated in 9805787b; all three recorded worker threads idle, with clean worktrees retained. Native goal and heartbeat remain paused.
- Playground has fourteen playable areas and direct `playground run=<id>` shortcuts. New power/shield/boost controls, actual salvage transfer and isolated opening campaign supplement all eleven previous trials. `playground reset` returns from the practice campaign or its Blueprint Lab. World Play auto-enters; visible Console/entry controls remain. Start with [QA-PLAYGROUND](QA-PLAYGROUND.md) for exact feature commands and limitations.
- Compile clean/zero C# warnings; expanded existing playground acceptance passes in 2.50s. Live power/inventory screenshots inspected, real salvage transfers 33 items including 20 scrap; quiet playground reset left running. Evidence Logs/playtest/qa-handoff and newest LEDGER entry. No full-suite or final milestone/owner acceptance claimed.
- Earlier opening chapter4bb3198b and load safety9d6c7c96/080e2390 accepted. Historical delivered scenarios ECO-010, ENC-011, DLG-011, TRV-011/014 and NAR-018 still have pending lead acceptance; M4 sources remain preserved/deferred. Do not accidentally mark those accepted from the playground pass. Owner QualitySettings and two untracked content metas remain intentionally uncommitted.

## 3. Next

Independent reviewer entry point: [scope and access only](QA-REVIEWER-PROMPT.md). Frank corrected the earlier directed checklist: first-pass reviewers must defer previous findings, acceptance summaries and decision-audit material until recording their own findings. Historical QA/decision documents are retained for a separately authorized reconciliation, not first-pass briefing. Implementation remains paused.

Wait for the independent reviewer/QA and Frank's instructions. Address reported M1–M3 functional issues and verify those systems with Frank before further M4 depth/scale/polish. No automatic worker recycling during this pause. Existing startup warnings and open task/gate details remain in LEDGER; advisory performance targets do not justify new benchmark or soak loops.
