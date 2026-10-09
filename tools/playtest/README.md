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

| Command | Purpose |
|---|---|
| `snapshot [--ui --ecs --logs --mark]` | Frame/time, scenes, visible UI text, camera + AudioListener count, ECS summary, deduplicated logs since mark |
| `logs [--level all/warn/error] [--mark]`, `mark` | Deduplicated console: one line per unique message with a count and the first `Assets/Game` frame |
| `ui`, `click --target <name or text>` | Read visible UI Toolkit text; click elements by name or label |
| `key --keys W,Space [--hold s] [--release]` | Keyboard injection through Input System state events |
| `mouse --x --y --button left/right --down --dx --dy --scroll` | Virtual "AgentMouse" (native mouse disabled during play, restored on exit) |
| `unfocused_input [--enable]` | Route input to the game while the Editor is unfocused (on by default in play mode) |
| `ecs [--filter]`, `entities --component <Type>` | ECS counts and component values (reflection) |
| `get --type T --path a.b`, `invoke --type T --method M --args`, `find --type T` | Reflection reads/calls and scene lookups |
| `perf` | Main-thread ms, draws, triangles, GC alloc per frame, memory, entity total |

Useful built-ins: `editor_play`, `editor_stop`, `open_scene`, `recompile`, `recompile_status`, `editor_status`,
`capture_game_view`, `get_performance_stats`, `run_tests`, `eval`, `run_script`. List them with `unity command`.

## Gotchas learned the hard way

- `capture_game_view --save_path` always writes under `Assets/` (creating `.meta` files). `shot.sh` fetches the image inline instead.
- The official `simulate_key` does not reach `Keyboard.current`; use `g.sh key`.
- Unfocused Editors drop keyboard/mouse unless input settings are relaxed; the harness does that during play only.
- A modal dialog in the Editor blocks its main thread and the CLI times out. Close the dialog.
- `eval` bodies can't contain `using` lines; use `run_script --file <path outside Assets>` for full C# files.
