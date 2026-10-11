# 08 — Tools

**Status: living reference.** Every tool this project uses, how to run it and the traps found while using it. Update it when you add a tool or learn something the next worker would otherwise rediscover. What a check must pass is in [05-testing.md](05-testing.md); this file covers how to run things.

## 1. What is installed

| Tool | Version / location | Used for |
|---|---|---|
| Unity Editor | 6000.6.5f1 (`ProjectSettings/ProjectVersion.txt`), Hub install path | The game |
| Unity CLI | `~/.unity/bin/unity` (the `tools/` scripts put it on `PATH`) | Driving an open Editor through `com.unity.pipeline` |
| Python 3 | System `python3`, standard library (+ Pillow for `ArtSource/StyleTest`) | Doc checks, art checks, render drivers |
| Blender | 5.2 LTS at `/Applications/Blender.app/Contents/MacOS/Blender` (numpy is bundled) | Look-dev renders and the block style test |

Prefer the Unity CLI and the `tools/` scripts over opening the Editor by hand.

## 2. Everyday scripts

All scripts find the project root themselves, so you can run them from anywhere in the repository.

| Command | What it does |
|---|---|
| `tools/compile.sh` | Compiles the project and prints C# errors; exits 1 on any error. With an open Editor it recompiles through the CLI in seconds. Without one, it does a headless batch compile (§3) |
| `tools/test.sh editmode [filter]` | EditMode tests; results in `Logs/test-results/editmode.xml` |
| `tools/test.sh playmode [filter]` | PlayMode tests |
| `tools/test.sh perf [filter]` | `Performance` tests, with medians against the budgets |
| `tools/check-docs.py` | Spec and ledger consistency; run it after editing any doc |
| `tools/ledger-sync.py` | Adds ledger rows for new spec tasks (never deletes) |
| `tools/playtest/g.sh <cmd>` | Runs a command in the running Editor: snapshot, logs, UI clicks, keys, mouse, ECS reads. See [tools/playtest/README.md](../tools/playtest/README.md) |
| `tools/playtest/shot.sh`, `perf.sh`, `waitscene.sh`, `to.sh` | Screenshot, frame stats, wait for a scene, hard timeout (macOS has no `timeout`) |

**Where output goes:** `Logs/tools/` (script logs), `Logs/test-results/`, `Logs/playtest/`. Never write tool output to `Temp/` (Unity deletes it on exit) or `Assets/`.

## 3. Unity with no Editor open (headless)

These work with no Editor open (verified 2026-10-10):

- **Compile:** `tools/compile.sh` batch-compiles in about 10–20 s. It reports errors only, so check `Logs/tools/compile.log` for `warning CS` yourself: the Definition of Done allows no new warnings.
- **Tests:** `tools/test.sh editmode|playmode` run headless.
- **Call the Editor binary directly.** For batch runs, don't use the CLI's `unity run` or `unity test`: they wait on its sign-in helper and can hang in non-interactive shells. `tools/_env.sh` provides `unity_editor`, which returns the binary's path. Commands sent to a live Editor (`unity command …`) are fine.
- **One-off Editor jobs**, for example creating FontAssets:
  1. Write a temporary static method under `Assets/Game/Editor/` that ends with `EditorApplication.Exit(0)`.
  2. Run:

     ```bash
     . tools/_env.sh && "$(unity_editor)" -batchmode -nographics -projectPath "$PWD" -executeMethod <Type.Method> -logFile Logs/tools/<job>.log
     ```

  3. Delete the script and its `.meta` afterwards.
- **Player script compilation (development and release) without building:** use a one-off job that calls `UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts`.
  - Pass `ScriptCompilationSettings { target = BuildTarget.StandaloneOSX, group = BuildTargetGroup.Standalone, options = ScriptCompilationOptions.DevelopmentBuild }` for development, and `ScriptCompilationOptions.None` for release.
  - Use an output folder under `Temp/`.
  - The result's `assemblies` list contains `Game.App.dll` when compilation succeeded.
  - This checks the `#if UNITY_EDITOR || DEVELOPMENT_BUILD` wrapping of dev-only code (verified 2026-10-11, DEV-001).
  - "Function pointer types are not supported by the SerializationWeaver" lines about `Unity.Entities.dll` are noise.
- **Rendering or real shader compilation needs the GPU:**
  - Run `-batchmode` **without** `-nographics`. Metal works headless.
  - Set `ShaderUtil.allowAsyncCompilation = false`.
  - Throw away the first `camera.Render()` after `NewScene`, because it draws only the clear colour.
  - These runs re-save `ProjectSettings/QualitySettings.asset` with a version bump. Revert it with `git checkout -- ProjectSettings/QualitySettings.asset`.
- **Native memory in tests:**
  - `UnsafeUtility.CheckForLeaks()` returns the tracked allocations still alive *and resets* the tracked set, writing its report with `Debug.Log`. Call it once after a warm-up as a baseline, then again at the end; skip `LogAssert.NoUnexpectedReceived` in that test.
  - A `NativeArray` of native containers (for example `NativeStream.Reader`) is a nested container: any job that holds it throws, its `Dispose(JobHandle)` included. Give each job its container in its own field (CORE-012).
- **SRP Batcher check:** call the internal `ShaderUtil.GetSRPBatcherCompatibilityCode(shader, 0)` by reflection. A result of 0 means the shader is compatible.
- **Textures under `-nographics`:** `EditorUtility.CompressTexture` and `Texture2DArray.SetPixelData` work; `Graphics.CopyTexture` does not.
- **Log noise you can ignore:**
  - In PlayMode runs under `-nographics`, stack frames named `GetOrCreateSystemsAndLogException` are Unity's "no graphics device" notices, not errors.
  - USS files with `url()` paths to missing assets log one import warning per path; these clear once the fonts land.
- **Project settings guard:** if `ProjectSettingsGuardTests` fail, apply the required settings with:

  ```bash
  . tools/_env.sh && "$(unity_editor)" -batchmode -nographics -quit -projectPath "$PWD" -executeMethod Game.Editor.ProjectSetup.ApplyAllAndQuit -logFile Logs/tools/setup.log
  ```

  In the Editor, the same thing is the menu item *Game > Project Setup > Apply Required Settings*.

## 4. Art and design tools

| Tool | Command | Notes |
|---|---|---|
| Token checker | `python3 tools/art/check_tokens.py [--verify-doc]` | UI token sheets: contrast and colour-blind distance |
| Board token export | `python3 tools/art/export_board_tokens.py [--check]` | `.uss` tokens to the design board's CSS |
| Art doc checks | `python3 tools/art/check_art_docs.py <moodboards\|blocks\|board>` | SPEC-80 deliverables |
| Token checker tests | `python3 tools/art/test_check_tokens.py` | |
| Block style test | `python3 ArtSource/StyleTest/build_style_test.py [--direction …]` | Five blocks per direction, rendered in Blender (ART-004) |
| Look-dev renders | `python3 ArtSource/LookDev/lookdev.py [--direction …] [--stage …] [--draft]` | Ships, stations, planets and scenes for the look book. Stages and files: [ArtSource/LookDev/README.md](../ArtSource/LookDev/README.md) |
| Build check | `python3 ArtSource/LookDev/lookdev.py --stage check` | Attachment, mount faces and the shape catalogue (~30 s, no rendering) |
| Silhouettes | `python3 ArtSource/LookDev/silhouettes.py` | Writes `docs/design/lookbook/silhouettes.html`, plus reference SVGs for ships built in 3D |
| Slope study | `/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python ArtSource/LookDev/ld_slopes.py -- render [--draft]` | Block diagrams and demo ships for [ADR-0018](decisions/ADR-0018-slope-block-kit.md) |

**Viewing the look book and the board.** Serve the repository locally rather than opening the files directly, so every browser loads the scripts and images the same way:

```bash
python3 -m http.server 8765 --bind 127.0.0.1
```

Then open `http://127.0.0.1:8765/docs/design/lookbook/` (the look book) or `http://127.0.0.1:8765/docs/design/board/` (the design board). When a script changes, bump the `?r…` query on its `<script>` tag in `index.html` so browsers reload it.

**Art rules:** these apply to every tool above (AGENTS.md §2, [DESIGN-GUIDE.md](design/DESIGN-GUIDE.md)):
- assets are procedural or own work;
- no copyrighted or third-party images (no Starsector art);
- fonts are SIL OFL 1.1 only;
- AI-generated assets only with the owner's approval of each asset.

## 5. Git

- **Local only. Never push** (AGENTS.md §2). Before any first push, ADR-0008 requires Git LFS.
- **Task work:** one branch per task, `task/<ID>-<slug>`, with commits `<ID>: <summary>` ([04-workflow.md](04-workflow.md)).
- **Look-dev and design work** uses descriptive branches (`task/lookdev-r4`, `task/design-guide`).
- **AI workers** end commit messages with their `Co-Authored-By:` trailer.
- **Branch state** and what still needs merging to `main` are in [HANDOFF.md](HANDOFF.md).
