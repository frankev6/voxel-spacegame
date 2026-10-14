# Independent reviewer handoff prompt

You are independently reviewing and playtesting Spaceship Game V2 at `/Users/frank/DEV/spaceship-game-v2` (Unity 6.6, Entities/DOTS). Frank has paused implementation and all three implementation workers. Review the implemented M1–M3 systems, their integration and the decisions made autonomously. M4 depth/scale/polish is deferred until Frank verifies M1–M3.

## Boundaries

Keep implementation paused. Do not resume the native goal, heartbeat, workers or roadmap, create agents, modify gameplay/content/settings assets, merge pending branches, or fix findings during this review. Produce a report; Frank decides the follow-up. Local Git only: no remote operations. Diagnostic gameplay interactions and isolated test-profile save/load are permitted. Preserve existing owner changes to `ProjectSettings/QualitySettings.asset`, `Assets/Game/Content/_Test.meta` and `Assets/Game/Content/_Test 1.meta`.

Baseline: local main `4fa507ad`; consolidated gameplay implementation `9805787b`. Later documentation-only commits may follow. Inspect current Git state and separate those from actual gameplay changes. Latest worker sources were power `be333788`, salvage `00e379f1`, campaign `73a8bb89`; integrated as `1b6a5a47`, `ff21a6bb`, `7f074453`, then reviewed/fixed by the lead in `9805787b`. Worker #2 finished before consolidation. All three are idle; their worktrees are preserved.

## Read first

1. `docs/HANDOFF.md`, `AGENTS.md`, `docs/ORCHESTRATION.md` — the explicit QA pause supersedes historical keep-working directions.
2. `docs/QA-PLAYGROUND.md` — exact feature access, expected behavior and reset semantics.
3. `docs/LEDGER.md` — current accepted/open task rows, newest evidence and remaining gates. Older row notes can lag later integration.
4. `docs/QA-DECISION-REGISTER.md` — complete snapshot of all 500 recorded ledger questions, index of all 113 actual ADRs and links to Decisions/§10 in all 33 specs. Audit it, not just the recent summary.
5. Relevant specs, architecture single-writer/module rules, design guide, `docs/08-tools.md`, and ADR-0042/0062/0070.

## Decision audit: explicitly requested by Frank

The lead was authorized to choose technical defaults without asking on each spec gap. Those are agent decisions, not individual human approvals. Review every Tech lead/default decision in the register and local spec tables, including correctness, scope and consistency with owner direction. Some early decisions were made by predecessor workers: use Git provenance rather than attributing all 500 rows to the last coordinator. An ADR marked Accepted without owner attribution is not human sign-off. Preserve explicit owner decisions and unanswered owner questions separately. Identify any implemented deviation missing from the register.

Prioritize the observable autonomous choices listed in the register: isolated profiles/reset retention, temporary shield fitting, salvage wreck construction/cap, campaign entry, preview story/map behavior, console input-context handling, intro ordering, dock/dialog navigation, pending-grid rebasing, hand-in proof and load side-effect suppression. Also inspect earlier provisional yaw tuning (ledger125), which remains pending owner sign-off. No approval is implied by this handoff.

## Launch and access

Open `Assets/Game/Scenes/World.unity` and press Play, or run `tools/playtest/playground.sh` with the Editor available. Direct World Play should automatically enter the playground. Visible **Enter playground** and **Console** controls must work; F1 is optional.

In Console:
- `playground`: enter the isolated practice profile.
- `playground status`: directory and last result.
- `playground run=<id>`: close Console, move to the feature and activate it. Close other screens first.
- `playground goto=<id>`: relocate only; close Console and press F inside the ring.
- `playground reset`: restore quiet sandbox, including directly from its Lab/campaign.

Terminal equivalents: `tools/playtest/g.sh dev --cmd 'COMMAND'`; poll pending operation IDs with `tools/playtest/g.sh op --id N`. Use the actual in-game Console as well: CLI success alone does not prove the human UI works.

## Functional playtest checklist

Use both direct console shortcuts and actual F interactions. Record expected versus observed results, especially a refusal, missing screen or text-only substitute.

1. **Flight:** `playground run=flight`. Complete all four gates. W/S forward/reverse; A/D turn; Shift+A/D strafe; Ctrl boost; X brake; wheel zoom. Check heading, drift damping, directional thrust, camera and spatial reference.
2. **Destruction:** `playground run=range`. Hold Space at the station. Cut single/redundant bridges, hit layered hull and reactor sections. Check damage, detached pieces, collision updates, VFX/audio and visible stalls. Reset restores the entire target.
3. **Combat:** `playground run=combat`. Verify hostile pursuit/firing and incoming damage/defeat. Repeating activation must not multiply enemies. Reset before quiet trials.
4. **Power/abilities:** `playground run=power`; `playground run=shield`; `playground run=boost`. Exercise UI buttons and normal ability bindings too. Check energy, shield HP/state, boost thrust, charges/cooldowns, incoming shield damage and regeneration. Observe actual effects, not only labels.
5. **Salvage:** `playground run=salvage` prepares a static Fighter wreck without granting cargo. After readiness, F at its Salvage prompt or repeat the command. Check actual cargo transfer and inventory display. With full cargo, remaining salvage should stay on the same wreck; issue no more than three wrecks per reset.
6. **Cargo/trade:** `playground run=cargo`, then `playground run=merchant`. Check sorting, stacking, drag/inspection, buys/sells, stock, credits, capacity and refusal feedback. Close each screen before another run command.
7. **Fleet/editor:** `playground run=fleet`, `playground run=editor`. Inspect designs/stats; place/remove/rotate/mirror blocks, undo/redo, inspect overlays, validate and save/reopen a blueprint. Test `playground reset` directly from the Lab. Saved blueprints survive reset.
8. **Settings:** `playground run=settings`. Audio/display/accessibility/rebinding and persistence. Note any deliberately changed settings and restore them for Frank; reset does not restore settings.
9. **Story/log/map:** `playground run=story`; repeat `playground run=survey` up to three units, closing the log each time; `playground run=map`. Check real objective progress, tracking, clue seen state, rumor-to-map selection, panning/zoom and markers. These preview pads do not complete ARK hand-in or start travel.
10. **Actual campaign:** reset, then `playground run=campaign`. Play opening dialogue/ship choice and battle. When dialogue closes, `playground run=station` invokes real ARK interaction. Check station quests/trade, hangar Fleet → Edit, committed ship changes and return. Follow actual log objectives, rewards and lane unlocks through the opening route to Whisper. Campaign guide buttons should open actual log/maps/fleet. No automatic progress grants.
11. **Persistence/travel:** in practice campaign and outside combat/dialogue, `save slot=qa_manual`, change observable state, `load slot=qa_manual`. Check cargo/credits, quests, clues, location, ship changes, no replayed rewards/effects. Exercise normal lane travel; `jump to=starsys_drift` is an additional dev-owner path and retains restrictions. Do not bypass locks to claim progression works.
12. **Reset/lifecycle:** repeat resets after battle, salvage, preview quests, Lab and campaign. Check enemies, debris, projectiles, cargo grants and narrative demo state clear; input/camera/UI recover; no duplicate beacons/listeners or errors. Save files already written, saved blueprints and settings deliberately survive. Verify another real profile is not overwritten.
13. **Inspection:** use `overlay name=bricks`, `colliders`, `islands`, `interaction`, and `overlay name=none`. Use `help` / `help cmd=<name>` for exact registered command syntax. Capture screenshots/logs for failures.

## Already known issues, limitations and pending work

- Existing startup warnings: unsupported `first-child` stylesheet selectors in save-slots/blueprint-browser; advisory blueprint power-sustain/recharge warnings; GridSpawnSystem lookup creation warning. Record impact separately from new failures. Zero C# warnings does not mean zero runtime/content warnings.
- Weapon/ship balance is unfinished. Provisional gyro tuning has not received owner sign-off; several legacy ships miss acceleration targets; medium/large balance remains unvalidated.
- Final art/content is unfinished. Seven dialogue portrait/illustration sprites remain unavailable; carried-over audio licence confirmation is pending. No new AI assets, ships or art substitutions during QA.
- M2 determinism and some integrated M3 save/travel/economy/encounter/UI acceptance remain open. Working playground access does not close every owning task or certify all save/load combinations.
- Historical delivered ECO-010, ENC-011, DLG-011, TRV-011/014 and NAR-018 work remains pending lead acceptance in varying integrated/unmerged states. Use exact ledger/source evidence; do not merge it during this review. M4 remains deferred.
- Full-screen layout coverage is outstanding. Check readability, modal layering and input at different practical game-view sizes. Recent clipping/overlay defects in power instruments were fixed and checked live; independently recheck them.
- Playground is a development composition: a shield emitter is fitted through editor/fleet owners before spawning its ship; salvage starts from a decommissioned approved blueprint; cargo/survey/trade pads intentionally grant stated test resources. Campaign portal does not grant quest completion.
- Reset discards current practice state, not saved blueprints/settings/previously written saves. Save refusals during combat/dialogue/transition and ability cooldown/energy refusals can be expected behavior; verify clear feedback.
- Latest consolidation evidence: compile clean/zero warning CS, existing expanded PlaygroundTests.Enter_Interact_Reset passed once in 2.4998576s. It covers prior pads, console context, actual shield/boost, salvage cargo, Lab reset, campaign intro/reset. Subsequent presentation-only fixes were compiled and checked live. This is not a full-suite pass or independent acceptance.
- Evidence files: `Logs/playtest/qa-handoff/playground.xml`, `compile.log`; screenshots `Logs/playtest/playground-qa-power.png`, `playground-qa-salvage.png` (before final instrument visibility fix), `playground-qa-inventory.png` (after fix). Earlier opening-route/save evidence is linked in LEDGER.

## Testing discipline and deliverable

Serialize Unity use. Prefer direct play and focused checks for demonstrated concerns. Do not run routine three-minute suites, ten-minute soaks or repeated performance loops. Performance targets are advisory; functional failures remain failures. Distinguish measured stutter from a spec-number miss. Follow 08-tools for test side effects and preserve owner files; do not restore uncommitted work with git checkout.

Deliver a review report with severity, precise reproduction commands/inputs, expected/actual behavior, file/line where known, evidence path, and whether each finding is new, already known, a questionable agent decision, expected refusal, unfinished scope or unverified. Provide a feature-by-feature pass/fail/not-tested matrix and a separate decision-audit section identifying choices Frank should revisit. State what was not tested. Keep implementation, workers, goal and scheduling paused; do not silently fix or mark milestones/owner decisions accepted.
