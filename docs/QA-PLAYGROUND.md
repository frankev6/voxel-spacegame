# Playground QA handoff

Start with `Assets/Game/Scenes/World.unity` and Play, or run `tools/playtest/playground.sh`. Use the visible **Console** control (F1 also works). All commands below are in-game console commands. From a terminal, use `tools/playtest/g.sh dev --cmd 'COMMAND'`; pending operations can be polled with `tools/playtest/g.sh op --id N`.

## Entry, navigation and reset

- `playground` enters the isolated `dev-playground` profile, preserving a prior real campaign through normal exit-save.
- `playground status` lists the available areas and last result.
- `playground run=<id>` closes the console, moves to the area and activates it. Close any other screen first.
- `playground goto=<id>` moves without activation; close Console and press F within the ring.
- `playground reset` discards the current practice session and recreates the quiet sandbox, including from its Blueprint Lab or practice campaign. Saved blueprints/settings and already-written profile saves are not deleted. It never resets another profile's saved campaign.

## Playable systems

| Command | What to exercise |
|---|---|
| `playground run=flight` | Four-gate course. W/S forward/reverse; A/D turn; Q/E (or Shift+A/D) strafe; Ctrl boost; X brake; wheel zoom. |
| `playground run=range` | Space fires at the enlarged procedural station. Cut narrow bridges; observe detached sections, damage, colliders, debris, VFX/audio. |
| `playground run=combat` | One hostile fighter per reset. AI pursuit, firing, incoming damage and defeat flow. Reset before trying quiet features. |
| `playground run=power` | Live energy supply/demand, shield HP and ability charges/cooldowns. Use shield/boost buttons and normal equipped ability keys. |
| `playground run=shield` | Toggle the real shield; reports the fixed-step result. Cooldown/HP restrictions remain real. |
| `playground run=boost` | Request Speed Boost; hold W to feel the increased thrust. Charges, energy and cooldown remain real. |
| `playground run=salvage` | Prepare an approved fighter wreck. Recover its actual salvage with F at the HUD prompt, or repeat the command once ready; inspect transferred cargo. Three wrecks maximum per reset. |
| `playground run=cargo` | Actual inventory, test fuel/credits, sorting/stacking and item inspection. |
| `playground run=merchant` | Actual market, stock, prices, buying and selling. |
| `playground run=fleet` | Fleet roster and committed ship statistics. |
| `playground run=editor` | Creative Blueprint Lab: place/remove/rotate, mirror, undo/redo, overlays, validate and save. Reset directly to return. |
| `playground run=settings` | Real display/audio/control/accessibility settings and rebinding. Settings persist beyond reset. |
| `playground run=story` | Real survey quest, clues and ship log. Track/untrack, read clues, Rumors → Show on map. |
| `playground run=survey` | One palladium per activation up to three held; observe actual collection progress. Does not fake the ARK hand-in. |
| `playground run=map` | Actual galaxy map and rumor target. This sandbox preview does not start travel. |
| `playground run=campaign` | Start the real opening campaign in the same isolated profile. Dialogue, ship choice, opening battle, station interactions, rewards and gate travel. |
| `playground run=station` | In the practice campaign, after dialogue closes: move to ARK's interaction range and invoke its real interaction. No quest completion is granted. |

## Campaign and diagnostic commands

In the practice campaign, finish the opening dialogue/combat and use station options for quests, trade and the hangar. The hangar's Fleet → Edit opens the real docked editor. Ship Log and map buttons are available in the practice guide when in Flight. Gate travel retains real locks and requirements.

- `save slot=qa_manual`, `load slot=qa_manual`: real persistence in the active profile; save can refuse during combat/dialogue/transition. Use campaign mode for world persistence QA.
- `jump to=starsys_drift`: dev travel through the travel owner; destination must exist and normal lane restrictions still apply. Use `help cmd=jump` for current syntax.
- `overlay name=bricks`, `overlay name=colliders`, `overlay name=islands`, `overlay name=interaction`, `overlay name=none`: inspect live grids/interactions.
- `help`: current registered command directory; `help cmd=<name>`: exact arguments.

## Scope and unresolved work

This is a development playground, not final art/content or a completed milestone review. M4 depth/polish is deferred until Frank verifies M1–M3. The ledger retains unaccepted integration scenarios and advisory measurements; do not interpret working feature access as acceptance of every owning task. Previously delivered but unmerged source remains referenced in HANDOFF/LEDGER for review. Owner QualitySettings changes and two untracked content metadata files are intentionally excluded from implementation commits.

Consolidated implementation: 9805787b. Focused playground acceptance: 1 passed in 2.50s; compile clean, zero C# warnings. Evidence: Logs/playtest/qa-handoff/playground.xml and compile.log. Final readout wrapping/visibility checked in live screenshots. Full suites and independent QA have not been claimed. Existing startup warnings: unsupported first-child stylesheet selector, advisory blueprint power balance and GridSpawnSystem lookup creation. Goal, heartbeat and workers paused; quiet World left running.
