# Independent review — scope and access

Review `/Users/frank/DEV/spaceship-game-v2`, a Unity 6.6 Entities/DOTS game.

## Scope

Independently assess the implemented M1–M3 systems and their integration through source review and gameplay. Choose your own review method, checks and priorities. M4 depth/scale/polish is outside the current implementation scope.

Use the project's vision, architecture, conventions, specifications and design guide as requirements. Record the Git revision you review and distinguish committed code from pre-existing local changes. Do not treat task completion labels as proof of correctness.

Implementation is paused. Do not change code/content, merge branches, resume workers, activate goals or schedules, or begin new implementation. Local Git only; no remote operations. You may operate the game and use isolated practice-profile saves for review. Preserve pre-existing working-tree changes.

## Independent first pass

Do not read the previous coordinator's QA findings, test-result summaries, known-issue lists or decision summaries before recording your own first-pass findings. In particular, defer `QA-DECISION-REGISTER.md`, the prior validation material in `QA-PLAYGROUND.md`, and historical review/acceptance commentary in HANDOFF, LEDGER and ORCHESTRATION. This prompt supplies the current pause and access information.

The project instructions may direct you to those operational documents. For this independent review, Frank's instruction is to defer their evaluative material until your first-pass findings are recorded. Consult underlying requirements as needed; do not substitute previous acceptance claims for your own assessment.

## Launch and console access

- Open `Assets/Game/Scenes/World.unity` in Unity and press Play.
- Alternatively, from the repository root run `tools/playtest/playground.sh`.
- In the game, use the visible **Enter playground** and **Console** controls. F1 also toggles Console.
- `playground` enters the isolated `dev-playground` profile. Entry may save a previously active campaign through its normal exit flow; use a disposable review profile if necessary.
- `playground status` lists available areas.
- `playground goto=<id>` moves to an area. Close Console and press F inside its ring to interact.
- `playground run=<id>` moves to an area and activates it. Close other screens first.
- `playground reset` returns to the quiet playground, including from the isolated practice campaign or Blueprint Lab. It discards current practice-session progress; saved blueprints, settings and previously written save slots are retained.

Available area IDs:

`flight`, `range`, `combat`, `cargo`, `settings`, `merchant`, `fleet`, `editor`, `story`, `survey`, `map`, `power`, `campaign`, `salvage`.

Additional access shortcuts:

- `playground run=shield`
- `playground run=boost`
- `playground run=station` — available in practice campaign after closing dialogue.

## Controls and other commands

- W/S: forward/reverse; A/D: turn; Shift+A/D: strafe; Ctrl: boost; X: brake.
- Space: fire; F: interact; mouse wheel: camera zoom; Esc: back/pause.
- `help` lists registered commands; `help cmd=<name>` shows arguments.
- `save slot=qa_manual` and `load slot=qa_manual` access persistence in the active profile.
- `jump to=starsys_drift` accesses the travel command.
- `overlay name=bricks`, `overlay name=colliders`, `overlay name=islands`, `overlay name=interaction`, `overlay name=none` access diagnostic views.

Terminal access while playing:

```sh
tools/playtest/g.sh dev --cmd 'COMMAND'
tools/playtest/g.sh op --id N
tools/playtest/g.sh snapshot
tools/playtest/shot.sh review-image-name
```

The operation command polls a pending operation ID returned by a command. Screenshots are saved under `Logs/playtest/`. Consult `docs/08-tools.md` for tool mechanics and safe handling of test side effects. Serialize Unity operations; do not run multiple test/editor jobs concurrently. Preserve the owner's preference for focused checks rather than routine long suites or performance loops. Performance targets are advisory.

## Report

Record your independent findings, evidence, reproduction steps and review limitations in your own structure. State what you did and did not inspect. Do not fix findings or mark owner decisions/milestones accepted.

After the first-pass report is recorded, stop. Frank can separately authorize reconciliation with prior findings and autonomous decision records. Keep implementation paused.
