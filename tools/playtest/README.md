# Playtest loop (drive the running Editor from a terminal or an AI agent)

The Unity CLI (`~/.unity/bin/unity`) talks to the open Editor through `com.unity.pipeline`. These scripts wrap it so a
check takes seconds. Outputs go to `Logs/playtest/` (never `Temp/`, which Unity deletes on exit, and never `Assets/`).

## Scripts

| Script | What it does |
|---|---|
| `g.sh <cmd> [--args]` | Run any CLI command and print only the result. Harness commands may omit the `gh_` prefix |
| `rc.sh` | Recompile through the CLI, wait, print C# errors and Editor state (used by `tools/compile.sh`) |
| `shot.sh <name>` | Game-view screenshot to `Logs/playtest/<name>.png` |
| `perf.sh` | Frame timing, draw calls, triangles, memory + voxel entity counts |
| `range.sh [seed=7]` | Restart the real game and spawn the modular destruction test station; Space or left mouse fires |
| `newgame.sh [seed=1] [ship]` | Enter Play from Boot if needed, launch into the imported starter, poll readiness and print a snapshot; uses the isolated harness profile |
| `waitscene.sh <Scene>` | Block until a scene is active |
| `to.sh <secs> <cmd>` | Hard timeout wrapper (macOS has no `timeout`) |
| `coop.sh [start\|guest n\|stop\|status]` | Co-op spike (SPEC-90): the Editor hosts the playground, dev-build guests join (`tools/build-dev.sh` first). `COOP_HEADLESS=1` runs guests with `-nographics`: no window, full tick rate even when covered or with the screen locked |
| `coop-parity.sh [slot]` | Co-op (ADR-0117 §7): diff the host's and a guest's `net parity` line for that guest's ship and the world (cells, blocks, colliders, ability slots, shield, combat status, interaction target, hulk and salvage counts, factions); exit 1 on any difference |
| `coop-colliders.sh <grid id> [x z ...]` | Co-op: optionally blow holes at world x/z, then compare one grid's bricks, cells and collider parts on host and guest 1 |
| `coop-chunks.sh [secs]` | Co-op: cut the range bridges into chunk bodies, wait for them to expire, hash-check and scan guest logs |
| `coop-soak.sh [secs]` | Co-op NET-010 table: two guests fight at 0/50/100/150 ms; bandwidth, prediction error, command buffer, hashes, crash scan |

`newgame.sh` and `scenario.sh` are available. `toworld.sh` follows the travel integration. The optional ship argument is a start-scenario ID; leave it empty until start scenarios exist.

## Destruction test station

For the current testing phase, entering World with a player automatically spawns the enlarged station 2 m below its previous height (FND-021). Run `tools/playtest/range.sh 7` with the Editor open to reset the game and target. In an existing World session, `range` reuses the current target. Turn off `AutoSpawnInWorld` in the firing-range config when the test target is no longer wanted. Aim with the mouse and hold **Space** or left mouse to fire. W/S thrusts forward/reverse; A/D turns; Shift+A/D strafes; Ctrl boosts. Restart the range script to restore every cell and clear debris/projectiles.

The cyan connections identify cuts to try:

- Left: one thin bridge into a hollow pod carrying a thruster. Cut it to release the pod.
- Right: two independent bridges into a cargo/reactor platform. One surviving bridge should keep it attached.
- Centre: layered front/back walls, slopes, a long slope, cargo, reactor and core. Shoot through the front wall to reach interior/rear cells.
- Rear: a wider bridge and two reactors for multi-cell damage and blast-chain checks.

This is a procedural test fixture using existing blocks, not a finished station design. The first combat slice supports plasma projectiles and physical voxel pieces; beam weapons, shields, AI, final effects/audio, debris pooling and large-battle performance work remain later tasks.

## Harness commands (`Assets/Game/DevTools/Editor/GameTestHarness.cs`)

| Command (args, defaults) | Purpose |
|---|---|
| `gh_save` (`--slot`) | Manual save in the active profile through game flow; completes after the matching GameSaved |
| `gh_load` (`--slot`) | Load through game flow/PLOC and the real player spawner; completes after matching GameLoaded and player readiness |
| `gh_jump` (`--to`) | TravelService dev jump to a numeric system ID, authored content ID or unique authored display name; retains lane locks and refusal reasons |
| `snapshot [--logs true] [--mark true] [--ecs true] [--ui true]` | Frame/time, scenes, visible UI text, camera + AudioListener count, ECS summary, deduplicated logs since mark |
| `logs [--level all\|warn\|error] [--mark true] [--max 80]` | Deduplicated console since the mark: one line per unique message with a count and the first `Assets/Game` frame |
| `mark` | Move the log mark |
| `ui [--max 60] [--chars 200]` | Visible UI Toolkit text per document (buttons, labels, fields) with element names |
| `click --target <name or text> [--index 0]` | Click a visible element by name (`#name`) or text; `index` picks one of several matches |
| `key --keys W,Space [--hold 0.12] [--release false]` | Keyboard injection through Input System state events; `hold 0` keeps the keys down until `--release` |
| `mouse [--x -1] [--y -1] [--button none\|left\|right\|middle] [--down true] [--scroll 0] [--dx 0] [--dy 0]` | Virtual "AgentMouse" (game-view pixels, origin bottom-left, −1 keeps; native mouse disabled during play, restored on exit) |
| `unfocused_input [--enable true]` | Route input to the game while the Editor is unfocused (on by default in play mode) |
| `ecs [--filter ""] [--max 60]` | ECS component counts, optionally filtered by name |
| `entities --component <Type> [--max 10]` | Entities with a component and its field values |
| `get --type T [--path a.b] [--depth 1]` | Reflection read of a static or singleton member (debugging only; scenarios must not use it) |
| `invoke --type T --method M [--args a,b] [--path ""]` | Reflection call with primitive args (debugging only) |
| `find --type T [--max 30]` | Scene objects with a component type, with positions |
| `perf` | Main-thread ms (15-frame average), draws, triangles, GC alloc per frame, memory, entity totals. `--voxel` adds grid/brick/chunk counters, `--combat` projectile/beam/FX/audio counters, `--markers` the 15 slowest game markers of the last frame, `--json` one JSON object |
| `dialog` | Active dialog beat, node, page and numbered options as JSON (play mode) |
| `dialog_choose` | Choose a displayed dialog option by its UI number: `--n` 1..9 (play mode) |
| `spawn` | Request ships through the runtime registry (play mode): `--blueprint`, `--x`/`--z` local or `--dx`/`--dz` player-relative metres, `--yaw` or `--face player`, `--faction`, `--team`, `--count`, `--spacing`, `--ai` |
| `grid` | Inspect a voxel grid (play mode): `--id`, `--player` or `--nearest`; `--bricks` adds up to 200 brick rows |
| `inv` | Cargo, overflow, capacity and credits (play mode); `--max` limits occupied slots shown; `--item`/`--count`/`--credits` as in `give` |
| `give` | Grant an item or credits (play mode): `--item` with `--count`, or `--credits` |
| `facts` | Facts (play mode): `--get` name, `--set` name=value, `--list` prefix, `--eval` condition; no arguments lists all |
| `aim` | Project a target to the agent mouse (play mode): `--target` nearest_hostile, nearest, id:<PersistentId> or point with `--x`/`--z`; `--track` seconds |
| `fire` | Hold a real Flight fire action (play mode): `--group` 1..3, `--seconds` |
| `playground` | Start the interactive playground, entering Play if needed |
| `gh_dmg_hash` | One JSON line with the canonical ComputeWorldHash (hex string) and current tick; Play mode required |
| `gh_dmg_stats` | One JSON line with published prior-tick DamageTickStats and cumulative GridDamageStats, sorted by persistent id; Play mode required |
| `gh_dmg_hit --x <metres> --y <metres> --z <metres> --amount <positive> --radius <nonnegative>` | Queue one kinetic DamageEvent on the first ready damageable persistent grid whose root-local half-open bounds contain this local-frame world point; overlaps choose the lowest persistent id. Radius 0 targets one cell, positive radius uses the resolver's blast path. JSON reports queued, not applied |
| `gh_dmg_kill_core --persistentId <unsigned decimal id>` | Queue lethal kinetic cell hits for every remaining cell of the grid's live core, including multi-cell cores; JSON reports the grid id and number queued. Resolver/death systems apply damage and the hulk transition on a later tick |

Harness command names may omit the `gh_` prefix in `g.sh`. `HarnessCommandTests` checks that every `[CliCommand]` has a row here with all its arguments.

The four damage commands return compact JSON for both success and failure. Persistent ids are decimal strings to preserve all 64 bits. Damage commands post through the existing main-thread channel inbox; they never edit cells, HP or hulk state directly. Requests remain queued while simulation is paused. `LastTickStats` describes the previously published tick, while per-grid counters are cumulative after outstanding jobs finish.

## Game commands (SPEC-70 §6.3)

Runtime dev commands, reachable as `gh_<name>` and in the in-game console. Each SPEC-70 task appends its rows here as its command lands. `gh_key`, `gh_mouse`, `gh_click` and `gh_ui` now route to this registry whenever the runtime host exists; their generic Editor fallback is preserved. The registry names and arguments match the generic table above. Input holds use unscaled time; native mice disabled by AgentMouse are restored when the host is destroyed. UI commands report unavailable until UI-003 supplies its document.

| Command (args, defaults) | Purpose |
|---|---|
| `gh_newgame` (`--seed`, `--ship=""`, `--intro=false`, `--profile=harness`) | Start a campaign; enter Play from Boot if needed; returns a runtime operation ID |
| `gh_sandbox` (`--player=true`, `--seed=1`) | Enter a sandbox in the harness profile; enter Play if needed |
| `gh_record` (`--action`, `--name=""`) | `start`, `stop`, or `save`; save uses a file stem beneath TestData/Replays with a .pcr suffix |
| `gh_replay` (`--name`) | Replay from the current simulation tick; returns an operation ID until playback ends |
| `gh_help` (`--cmd=""`) | List runtime commands or show one usage; play mode required |
| `range` (via `gh_dev --cmd range` or the console) | Spawn the test station in World. Left pod has one bridge; right pod has two; rear bay has paired reactors. New game resets all pieces and projectiles |
| `gh_encounter` (`--spawn`, `--combat`, `--director=on\|off`, `--list=false`, `--count=1`, `--anchor`) | List definitions/active instances by default; Dev spawn uses 1–10 encounter instances at the player or a body story tag. Combat uses StartCombat; zero created is reported as queued/refused, never as spawn success. Sandbox has no galaxy |
| `gh_relation` (`--faction`, `--set`, `--add`) | Read relation/stance, or queue one Int32 set/add through FactionService. Relations clamp to −100..100; the matrix changes at the next fixed tick |
| `gh_teleport` (`--x`, `--z`, `--sx`, `--sz`, `--yaw`, `--near`, `--offset=40`) | Move in one coordinate mode, or to body centre minus Z by surfaceRadius + non-negative offset. Near matches a loaded body key, case-insensitive display name or story tag, nearest to the player |
| `gh_overlay` (`--name`, `--on=true`, `--id=0`) | Toggle bricks, dirty, colliders, islands, spatial, interaction, all or none; no name reports status, skips and truncation. Islands use the supplied PersistentId or default player, with a 60-frame cache and 2M-cell cap. Spatial shows occupied XZ cells within 1 km. All six overlays share the bounded line mesh |
| `gh_interact` (`--body`) | With a body query, move to surfaceRadius + half its interactionRange, wait for the real in-range target, then press bound Flight/Interact for 0.1 s. Returns an operation; success requires the matching BodyInteracted event. Without body, use the current legal target. Unbound actions fail before movement |
| `gh_dev` (`--cmd`) | Execute any runtime command line |
| `gh_op` (`--id`) | Poll `running/succeeded/failed` status without blocking |
| `gh_until` (`--cond`) | Evaluate a typed scenario expression once; returns true/false |
| `gh_scenario` (`--file`) | Start a file; enter Play from Boot if needed; JSON run ID |
| `gh_scenario_status` (`--run`) | JSON outcome, exit code, line, elapsed seconds and result path |
| `gh_scenario_stop` (`--run`) | Stop and write an error result |

Useful built-ins: `editor_play`, `editor_stop`, `open_scene`, `recompile`, `recompile_status`, `editor_status`,
`capture_game_view`, `get_performance_stats`, `run_tests`, `eval`, `run_script`. List them with `unity command`.

`toworld.sh` enters Boot/Play, clicks the real New Game menu, submits the commander name and advances opening dialog input until Flight/player readiness, then prints a snapshot. It uses the normal menu seed; explicit seed arguments fail while the menu has no seed selector. Live acceptance requires HUD-002 to bind the real MainMenu root (SPEC-70 Q31).

Travel scenario probes are `travel.phase`, `travel.arrival_gap`, `travel.inside_ring`, `travel.destination` and `travel.route_hops`. `r30_lane_jump_entry` is active and checks the real Drift/Ark round trip: both arrivals finish Idle inside the lane ring with a 0–200 m gate gap.

Encounter probes are `encounters.active`, `encounters.ships`, `relation("faction_id")`, `stance("faction_id")` and `encounter_defeated("encounter_id")`. Defeats count real EncounterDefeated events since each scenario started; a concurrent or later run has its own baseline. Unknown IDs and unavailable owners fail explicitly. Example: `g.sh encounter --spawn encounter_raiders`, then `g.sh until --cond 'encounters.ships >= 2'`; `g.sh relation --faction faction_vestari --set -50` queues hostility for the next tick.

Interaction probes are `interaction.target` (current body display name or empty) and `interaction.inrange` (the real query flag). `overlay name=interaction` draws a 32-segment XZ circle at surfaceRadius + interactionRange per live interactable and a green/red player-to-target line from that flag. Example: `g.sh interact --body ark_station`, then poll its operation via `g.sh op --id <id>`. Station Dock/BodyInteracted is functional acceptance; the draft dialog route remains with its dialogue owner (SPEC-70 Q29).

## Gotchas learned the hard way

- `capture_game_view --save_path` always writes under `Assets/` (creating `.meta` files). `shot.sh` fetches the image inline instead.
- **Don't capture while the Editor is paused.** `shot.sh` with `EditorApplication.isPaused` set renders the frame without the voxel bricks: hulls, rock and stations are all missing, and only props and FX remain. Repeated pause, capture, resume cycles make the whole map flicker for anyone watching the Editor (seen 2026-10-18 during the ABL-006 HUD shots). Capture unpaused, triggered by a probe.
- The official `simulate_key` does not reach `Keyboard.current`; use `g.sh key`.
- Unfocused Editors drop keyboard/mouse unless input settings are relaxed; the harness does that during play only.
- A modal dialog in the Editor blocks its main thread and the CLI times out. Close the dialog.
- `eval` bodies can't contain `using` lines; use `run_script --file <path outside Assets>` for full C# files.

## Scenario scripts

`scenario.sh <name or path> [timeout=600]` starts and polls a scenario once per second. It prints the outcome and JSON path; exits 0 (pass/draft), 1 (failed), 2 (error), or 3 (Editor unreachable). `scenarios.sh [tag=regression]` runs active files with that tag, in name order, and writes `Logs/playtest/scenarios/summary.json`. Use `tag=smoke` for the Boot smoke. The intentional `smoke_fail` CLI fixture is tagged `negative`, so it does not fail smoke/regression suites. Result filenames include a run ID to avoid overwriting concurrent runs in the same second. `report name = expr` lines (DEV-027) are printed after the outcome, one `name = value` a line. Repros for the Hollow, tagged `hollow` (not in the suite): `scenario.sh hollow_hostile_entrance` (the Clan turned hostile at the entrance: seconds to kill a WRAITH, damage to their stations and ships, splits) and `scenario.sh hollow_traffic_calm` (a minute parked at the Market: player hull, traffic damage, Clan relation). To play one yourself: with the Editor open and stopped, `scenario.sh xeno_swarm timeout=1800`, click into the Game view, turn round from the entrance and help the Clan hold the Hollow against three waves of Xeno ships from the south (8 husks; 12 husks from both flanks; 14 husks and 2 broodmothers), with their patrols and wall guns. It prints how long each wave took, the Clan ships lost and the station damage, and fails with the wave you fell in. Emergency repairs (ABL-006): `scenario.sh emergency_repair` blows the WRAITH's tail off with the `damage` command, presses the repair and reports engines, thrust, cells and hull items before and after; `emergency_repair_brick` blasts hard enough to delete whole bricks and checks they come back.

`tools/test.sh playmode ScenarioFileTests` discovers one case per `.scenario` file tagged `smoke` or `regression`, boots the real game, and prints the result JSON path. Drafts are ignored before resolving future commands/probes. Active files retain strict parsing and the file timeout, with a second test bound of `timeout × 60` frames.

## Flight recording

With a ready player, run `g.sh record --action start`, fly with the normal controls, then `g.sh record --action stop` and `g.sh record --action save --name flight_demo`. After a fresh game with the same seed, use `g.sh replay --name flight_demo` and poll the returned ID with `g.sh op --id N`. Console equivalents are `record start`, `record stop`, `record save name=flight_demo` and `replay name=flight_demo`.

Files use SPEC-71's `cmdrec 1` text format with a `.pcr` suffix. Names allow 1–80 letters, digits, underscores or hyphens, without an extension. Playback preserves tick spacing and starts at the current tick; it does not restore a ship's starting pose. Session changes and timeout stop playback. Exact endpoint comparison currently uses local position and one fixed tick per input frame; arbitrary catch-up schedules retain SPEC-71 Q8's limitations.

`TestData/Replays/fighter_flight_demo.pcr` is a live seed-7 Fighter recording: 1,790 ticks, forward thrust/boost followed by right strafe and coasting. Start a fresh seed-7 game before replaying it. The automatic 300-tick acceptance test provides the exact endpoint comparison; this variable-frame recording is a visual demonstration.

## Flight controls (FLT-013)

W/S requests forward/reverse thrust, A/D turns, Shift+A/D strafes while the turn command is zero, Q/E also strafes, Left Ctrl boosts, X brakes. Releasing thrust gently slows the ship through its opposing thrusters. Small maneuvering thrusters take longer to brake than the main engines take to accelerate. All movement follows the current ship orientation and physical thrust/mass; rotation does not instantly rotate momentum.

## Interactive development playground (DEV-021)

**Open World and press Play.** Direct Editor launches of World automatically enter the isolated playground. From World or Main Menu, click **Enter playground** at the upper right. Click **Console** to type commands; F1 is optional and the console has a Close control.

From a terminal at the repository root, run `tools/playtest/playground.sh`. This enters Play if necessary, starts the playground and waits for readiness. No manual console setup is needed.

**Reset everything with `playground reset`** (or the Quartermaster's station, which resets the Hollow). This restores the player and the Hollow and clears enemies, broken pieces, projectiles, inventory grants, facts and trial state. Entry uses the separate `dev-playground` profile and saves any prior real campaign before switching; a failed save stops entry safely.

**The Hollow (DEV-023 option E).** The playground is the Khuchad Clan's haven in an asteroid. You spawn at its entrance, outside the Maw. Inside, the Ring and the side tunnels lead to the stations: the Market, the Quartermaster (resets the Hollow), the Gunsmith, Parts, Salvage and Smuggler dens, and the Docks (energy canisters). F in a station's circle activates it. The light strips along the walls take the colour of the next station that way. The ARK Proving Grounds and their exhibit districts are retired (owner, 2026-10-18).

**Feel prototype (2026-10-21).** `camera [topdown|chase|cursor|orbit] [assist=on|off]` switches the flight camera live and turns auto-aim on or off.
- The game starts on `cursor`: a low camera that turns while the cursor is near a side of the screen.
- `chase` sits behind the ship and turns with it.
- `orbit` is mouse look: the cursor is locked, the reticle aims, and moving the mouse up looks farther.
- `topdown` is the old 55° camera.

Auto-aim is on by default. A cursor within about 50 px (at 1080p) of a hostile ship aims at it, and the guns lead it. An amber bracket marks the lock.

**The race.** A portal inside the Hollow (TRACK 01 · RACE PORTAL, in an alcove off the Ring south-east of the Maw junction, at 80, −150) takes you to Track 01: the race opens and puts every pilot on the grid; F starts the countdown; `race end` brings you back beside the portal. `race open` also opens it from anywhere.

Console: `playground goto=hollow` (the entrance), `goto=orbit` (VESHA's tether, the feel prototype's planet below the plane), `goto=<station>` (market, quartermaster, den_gunsmith, den_parts, den_salvage, den_smuggler, docks) or `goto=hollow@x,z[,yaw]` (anywhere, in the Hollow's metres); `playground run=<station>` goes there and presses F. Screens and trials that are only menus stay console shortcuts: `playground run=cargo | settings | fleet | story | survey | map | power | campaign | editor | race`, and `run=shield` / `run=boost` trigger the equipped abilities. `playground status` lists every id. The Hollow's content comes from *Game/Content/Build Hollow*. For a hostile to fight, `spawn blueprint=ship_nocturne_wraith dz=330 face=player faction=faction_raiders team=2 ai=true`; `range` spawns the breakable test station.

Story, survey and map use the registered quest/clue owners, log query and map presenters; the generated galaxy is metadata only, with no body load or player relocation, and reset clears them. `editor` opens the creative Blueprint Lab; `playground reset` returns directly from it and from the practice campaign. Full reviewer checklist: [QA-PLAYGROUND](../../docs/QA-PLAYGROUND.md).

CLI while playing: `tools/playtest/g.sh dev --cmd "playground reset"`; poll its returned operation with `tools/playtest/g.sh op --id N`.

Live voxel inspection: `g.sh overlay --name bricks`, `--name dirty` or `--name colliders`; `--on false` disables one, `--name none` clears all. `g.sh overlay` reports edge count and truncation. The same `overlay name=...` commands work in the F1 console. These inspect grids within 300 m of the player.
