# Implementation dispatch board

Owner authorization and revised goal: ADR-0062. Read this with HANDOFF and LEDGER on every resumed orchestration turn.

Coordinator thread: `01a12ab8-8b1d-7263-ac71-aa447e6af284`.

| Worker | Model | Thread | Worktree | Active task | State | Eligible follow-up candidates |
|---|---|---|---|---|---|---|
| Combat support | gpt-6.1-sol high | `01a133d2-c73e-7d11-a378-844fc4839836` | `/Users/frank/.codex/worktrees/6d4c/spaceship-game-v2` | ECO-008 | DLG-011571ae6f6 delivered pending QST-006/008; now merchant/salvage production wiring | Salvage accepted a7a433f2; no Unity |
| Power and abilities | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41a7a8c1d570` | `/Users/frank/.codex/worktrees/5fab/spaceship-game-v2` | ENC-014 | ENC-0113415b964 delivered pending acceptance/dialogue prerequisite; implementing live territory composition | Reuse e0157b39 with real production lifecycle; no Unity |
| Presentation and content | gpt-6.1-sol high | `01a133d2-c73f-73f0-b8d9-41823de6795b` | `/Users/frank/.codex/worktrees/1369/spaceship-game-v2` | QST-006 | Combined overlays bdbefa75 delivered; adapter/log/markers now dispatched | Accepted BeatService30762271; no Unity |

## Operating rules

Owner correction 2026-10-14: all three workers implement substantial feature deliveries, not repeated review assignments. Batch source/integration commits at meaningful completion points. EDT-020 may implement against integrated EDT-017 source while the lead validates it; this explicit pipeline exception does not mark EDT-017 done or waive functional acceptance. Lead owns selection fixes and editor connectivity/validation/commit prerequisites. Do not use partial acceptance as an idle reason when useful independent implementation is available.

Owner authorization: ADR-0062. Exactly these three separate reusable GPT-6.1-Sol high threads; no replacements or subagents. Lead owns central docs/main, reviews, frontier work and the serialized Unity channel. Workers use isolated task branches, no Unity, no remotes and no central workflow docs. Read all three exact thread states at work boundaries; recycle to ready standard tasks. Record real waits. Advisory performance policy and art restrictions remain binding.

Native goal was previously paused, but get_goal now confirms active on 2026-10-14 at this continuation. Keep the full objective intact. The existing heartbeat remains configured; do not create another goal or schedule. No duplicate workers.

All three workers received Frank's fast-fixture correction: shorten incidental combat setup with fixture-only health/damage/cadence/distance changes, preserve required outcomes and production balance, use bounded fixed ticks and batch focused checks after implementation. No routine performance/soak runs. Budget test wall time as well as simulation time: use the smallest fixture and direct ticks that prove the requirement, avoid real-time combat waits, and run focused checks once per compatible implementation batch. Keep functional assertions intact; production balance is independent of fixture acceleration.

## Current lead state

- DEV-021 reopened after owner playtest exposed informational-only pads and inaccessible entry. Direct World startup, visible Console/entry controls and actual inventory/settings/trading interactions are integrated; 15-case focused batch and live entry/trading passed. DEV-018/GAL-012/EDT-020 compatible worker deliveries integrated; GAL-012 and EDT-020 accepted. TRV-011 hooks integrated and unit checks pass, actual R30 remains. Exact evidence and partial states in LEDGER.
- ENC-003 accepted a2467740; canonical faction runtime and actual Raider hostility in playground. ECO-011/HUD-018 accepted; GAL-013 compiled with advisory measurements deferred. ENC-004 accepted 3b4929e9: service/save/reset, six service and two integration checks passed. Next combat assignment ENC-005 territory. HUD-013 and HUD-017 workers actively implementing.
- Real remaining dependencies: full ECO-008 launcher needs encounter member lookup ENC-007; QST-009 delivered source needs actual compiled dialogue program DLG-005/007. No fake owners. Editor was stopped before validation; preserve that state. Owner QualitySettings and two content metas remain excluded.

- EDT-008 accepted c86bd67b: incremental connectivity and bounded/async split jobs integrated into edit lifecycle, six required cases passed. Lead next EDT-009 validation/removal then EDT-011 commit. All three existing worker handles confirmed active during this delivery.

- EDT-009 accepted 56d65d57: full validation, throttled issues and atomic floating removal; all 11 explicit acceptance cases pass. Lead next compatible integration includes ENC-005 0322c7f9, HUD-017 e7f4a957 and HUD-013 6222e0a3, then EDT-011 commit/host. Worker assignments in the table supersede historical completed-task notes above.

- Compatible territory/dialog/fleet integration accepted0f00dd27;16 distinct focused cases pass after documented fixture/live-layout fixes. Seventh playground fleet area visually verified at Logs/playtest/playground-fleet-live.png; game left running there. Next lead EDT-011 commit, then overlays/host/root.

- EDT-011 accepted b60fde52/7d95a992: eight required commit and ten inventory cases pass; exact cargo checkpoint ADR-0096. HUD-007 d0ac700a pending integration. Presentation now completing HUD-005/004, pause and main-menu workers active. Lead next editor overlays/host and compatible HUD integration.

- EDT-019 accepted 38c38243: all four required overlay checks pass, including Metal support and 65,536 instances in one category draw. Next EDT-022 host and compatible pending HUD deliveries; updated active table is authoritative.

- EDT-022 accepted 1448b2fb/b5922750: six required host behaviors pass across initial and focused lifetime results. Native check attributes Toolkit caches explicitly and forbids editor allocations; launcher remains EDT-023. Pending compatible HUD003/006/007 hashes in ledger; active table tracks recycled workers.

- EDT-023 claimed: real launcher/commit targets/world return. TRV-014 delivery 2e03c4cb received; economy worker has a recorded dependency wait (no ready standard implementation without missing frontier owners). FND-015 active on the same presentation thread.

- EDT-023 source56209106 integrated as explicitly partial: actual launcher/targets and frame-aware pose return compile clean; eight existing commit cases pass. Next install profile-aware blueprint storage and persistent editor binding in GameComposition, reuse its screen in UiComposition, finish actual roots/startup, then five real scenario checks. No editor pad advertised. FND-013 partial row retained.

- HUD-006/007 accepted67c25036: 4/5 specified cases pass. HUD-0033/5: two failures assigned to its original worker, now active. Lead next HUD-009 actual sync before replacing temporary roots and installing EDT-023. No empty HUD source or fake interaction owner.

- HUD-00939074534 source integrated: six required EditMode behaviors accepted, production Bind/root wiring and three performance fixture definitions remain. All three workers delivered and are in explicitly recorded dependency waits. Next lead actual HUD/editor composition, not another empty screen or placeholder owner.

- Production roots accepted 057f29ea: HUD-002 four required cases and live flight HUD/creative Lab verified. EDT-023 scenario acceptance remains open. HUD-003 four of five pass, Continue correction returned to original worker; other two deliveries retain genuine dependency waits.

- b9f9cb46 accepts HUD-003 after actual Continue passes (worker b1da267c fixes fixture panel lifetime). EDT-023 real Lab save/reload/MainMenu return passes; four fleet/dialog scenarios remain. All three recorded workers delivered, genuine frontier-owner dependency waits; no duplicate/review-only assignments.

- FND-01518910e5a accepted: root-owned defeat recovery, actual save/load/menu and all scene starts pass. Same three workers delivered/accepted; next lead must unblock EncounterDirector/dialog/map/dev-overlay owners for substantial ready standard follow-ups. No routine test queues.

- ENC-007247fd49a replaces the throwing shell and is composed; three spawn/ambush/cap checks pass. Lead retains schedule/defeat/despawn/pending acceptance and two advisory fixtures before acceptance. All three exact workers confirmed idle with genuine dependencies; director completion unlocks standard follow-ups.

- ENC-007 accepted a445825b (ten functional cases across batch/fix; two advisory fixtures authored, deferred). ADR-0100 and claim57dbe986 unlock same three workers on ENC-012 commands, ENC-009 narrative and ENC-013 formations. Lead next ENC-008 save persistence; serialize primary Unity checks only after compatible deliveries.

- ENC-008 f9c6f0d5 accepted: 3 binary checks and actual SaveGame/LoadGame same-instance respawn/combat refusal pass. Worker ENC-012230f64b8 delivered; others implementing. Integrate compatible encounter deliveries next.

- ENC-012a9adf708 and ENC-013078e8058 accepted with two focused cases each. Those two workers have genuine ready-standard dependency waits; ENC-009 worker still finishing source. Next lead DEV-012 unlocks standard overlay/interaction work; ENC-009 acceptance unlocks ENC-011.

- DEV-01253b1466a accepted with three focused cases and real GPU screenshot; ENC-009aae617cd accepted with five routing cases. All three same workers now dispatched implementation: DEV-014 interaction, DEV-013 island/spatial overlays, ENC-011 actual encounter scenarios. Prior dependency waits superseded; only dialogue scenario remains genuinely dependent on DLG-007/QST-008. Lead next actual dialogue owner and remaining playground integration.

- DLG-007 d835fa49 accepted: eleven conformance checks and live dialog choice/pause verified. Combat worker recycled to now-ready DLG-011; DEV-013 and ENC-011 continue. Pending compatible overlays remain for lead integration. Next frontier BeatService unlocks automatic story interactions.

- QST-00530762271 accepted with nine required functional cases; two advisory fixtures deferred. Presentation recycled to ready QST-006. Combined DEV-013/014 accepted38d812a3 with five focused cases and live rendering; other workers DLG-011 and ENC-011 remain active.

- ECO-007a7a433f2 accepted: five functional salvage cases, two advisory fixtures deferred. Combat worker recycled to ready ECO-008 real launcher/production wiring. DLG-011571ae6f6 retains actual intro QST-006/008 prerequisite; no substitute queue. Other workers QST-006 and ENC-011 continue.

- CAM-0078da588cd accepted: four required occlusion checks and seven flight regressions pass; real-ray advisory measurement deferred. ENC-0113415b964 delivered pending focused acceptance and QST-008 dialogue case; same worker now finishes ENC-014 production territory wiring. ECO-008 and QST-006 remain active.

- ENC-011d950ca41 integrated: four real combat/pending-save scenarios pass; neutral derelict correctly blocked initial raider shot, fixed fixture placement rather than runtime logic. Dialogue defeat remains QST-008; separate opt-in battle deferred. All three workers confirmed active; no replacements.
