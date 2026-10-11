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
| `waitscene.sh <Scene>` | Block until a scene is active |
| `to.sh <secs> <cmd>` | Hard timeout wrapper (macOS has no `timeout`) |

Game-flow scripts (`newgame.sh`, `toworld.sh`, `scenario.sh`) are added by SPEC-70 once the game exists.

## Harness commands (`Assets/Game/DevTools/Editor/GameTestHarness.cs`)

| Command (args, defaults) | Purpose |
|---|---|
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
| `perf` | Main-thread ms (15-frame average), draws, triangles, GC alloc per frame, memory, entity totals |

Harness command names may omit the `gh_` prefix in `g.sh`. `HarnessCommandTests` checks that every `[CliCommand]` has a row here with all its arguments.

## Game commands (SPEC-70 §6.3)

Runtime dev commands, reachable as `gh_<name>` and in the in-game console. Each SPEC-70 task appends its rows here as its command lands. `gh_key`, `gh_mouse`, `gh_click` and `gh_ui` now route to this registry whenever the runtime host exists; their generic Editor fallback is preserved. The registry names and arguments match the generic table above. Input holds use unscaled time; native mice disabled by AgentMouse are restored when the host is destroyed. UI commands report unavailable until UI-003 supplies its document.

| Command (args, defaults) | Purpose |
|---|---|
| `gh_help` (`--cmd=""`) | List runtime commands or show one usage; play mode required |
| `gh_dev` (`--cmd`) | Execute any runtime command line |
| `gh_op` (`--id`) | Poll `running/succeeded/failed` status without blocking |
| `gh_until` (`--cond`) | Evaluate once; expressions unavailable until DEV-005 |

Useful built-ins: `editor_play`, `editor_stop`, `open_scene`, `recompile`, `recompile_status`, `editor_status`,
`capture_game_view`, `get_performance_stats`, `run_tests`, `eval`, `run_script`. List them with `unity command`.

## Gotchas learned the hard way

- `capture_game_view --save_path` always writes under `Assets/` (creating `.meta` files). `shot.sh` fetches the image inline instead.
- The official `simulate_key` does not reach `Keyboard.current`; use `g.sh key`.
- Unfocused Editors drop keyboard/mouse unless input settings are relaxed; the harness does that during play only.
- A modal dialog in the Editor blocks its main thread and the CLI times out. Close the dialog.
- `eval` bodies can't contain `using` lines; use `run_script --file <path outside Assets>` for full C# files.
