# AGENTS.md — rules for every worker (human or AI)

You are implementing **Spaceship Game V2**, a Unity 6.6 DOTS game: build ships from functional voxel blocks, fly between star systems uncovering a mystery, and watch massive voxel ships and stations break apart realistically.

This file is the entry point for anyone writing code here. It is short on purpose; the binding details are in `docs/`. [README.md](README.md) is the project's public face for people; keep worker instructions here, not there. **Continuing someone's work? Read [docs/HANDOFF.md](docs/HANDOFF.md) first**: the owner's standing rules, where things stand and what's next.

| If you are… | Read |
|---|---|
| Continuing someone's work | [docs/HANDOFF.md](docs/HANDOFF.md), then this file |
| Implementing anything | This file, then [docs/README.md](docs/README.md) |
| Looking for a command or tool | [docs/08-tools.md](docs/08-tools.md) |
| Reviewing direction with the owner | [docs/00-vision.md](docs/00-vision.md), [docs/ROADMAP.md](docs/ROADMAP.md), [docs/design/DESIGN-GUIDE.md](docs/design/DESIGN-GUIDE.md) |
| Checking progress | [docs/LEDGER.md](docs/LEDGER.md) |

## 1. Read before touching anything

1. [docs/00-vision.md](docs/00-vision.md): what the game is and isn't
2. [docs/01-architecture.md](docs/01-architecture.md): modules, frame pipeline, shared contracts, single-writer table (**binding**)
3. [docs/02-conventions.md](docs/02-conventions.md): coding rules (**binding**)
4. [docs/03-glossary.md](docs/03-glossary.md): words and their exact meanings (**binding**)
5. [docs/04-workflow.md](docs/04-workflow.md): how to claim, do and finish a task
6. [docs/05-testing.md](docs/05-testing.md) and [docs/06-performance-budgets.md](docs/06-performance-budgets.md): checks and gates
7. [docs/07-implementation-traps.md](docs/07-implementation-traps.md): the DOTS/physics/rendering/UI mistakes that compile but fail later (**binding**, with a review checklist)
8. [docs/08-tools.md](docs/08-tools.md): every tool, how to run Unity headless, Blender look-dev, and their traps
9. The spec for your task in [docs/specs/](docs/specs/), plus the *Decisions* and *Data model* of every spec it depends on
10. [docs/LEDGER.md](docs/LEDGER.md): what is done, in progress and blocked
11. For anything visual (art, UI, blocks, ships): [docs/design/DESIGN-GUIDE.md](docs/design/DESIGN-GUIDE.md) (**binding** source of truth for the look)

## 2. The non-negotiables

- **Respect the task tier.** Each task is `frontier` (reserved for Claude Opus / GPT Astra class models at their highest reasoning setting) or `standard` (Claude Sonnet / GPT Sol class may take it). If you are a standard-class model, never claim a `frontier` task. Sign the ledger *Owner* as `name (model)`. Rules: [04-workflow §7](docs/04-workflow.md#7-task-tiers-which-model-may-take-which-task). No task waits for a per-task review; systems get an in-depth review when built ([04-workflow §8](docs/04-workflow.md), ADR-0019).
- **Work only on a claimed task from the ledger** whose dependencies are `done`. One task per branch: `task/<ID>-<slug>`, commits `<ID>: <summary>`.
- **Never invent architecture.** If something isn't specified: architecture → conventions → glossary → the spec's *Open questions* default → otherwise pick the simplest option that keeps every rule, record it as a new open question with your default, and continue. Never silently diverge.
- **Never change a binding contract** (architecture §6 types, another module's public API, assembly references, packages) without an accepted ADR in `docs/decisions/`.
- **Respect module boundaries.** Each assembly may reference only what architecture §3 allows. `AssemblyRulesTests` enforces this; don't "fix" it by editing the test.
- **One writer per piece of state** (architecture §8). If you need to change state you don't own, send an event or request.
- **Simulation is deterministic and fixed-step.** No `UnityEngine.Random`, `System.Random` or `GetHashCode` in simulation or saves. No `Time.deltaTime` in gameplay.
- **Art follows [DESIGN-GUIDE.md](docs/design/DESIGN-GUIDE.md)** (approved 2026-10-11). Never reference `Assets/Game/Art/_Placeholder/` from code; use definitions and theme tokens. No copyrighted or third-party images (no Starsector art); fonts are SIL OFL 1.1 only; AI-generated assets only with the owner's approval of each asset.
- **Push only when the owner says so.** `origin` is the public GitHub repo `frankev6/voxel-spacegame`. Commit locally on task branches; pushing publishes publicly, so every push needs the owner's go-ahead. Images, audio, fonts and models are in Git LFS ([ADR-0008](docs/decisions/ADR-0008-no-lfs-yet.md)); install Git LFS before cloning.
- **No old code.** The old prototype is a reference for feel and content only. Content arrives through `Import/Legacy/` and the converters in SPEC-72.
- **Build systems before tuning** ([ADR-0042](docs/decisions/ADR-0042-advisory-performance-targets.md)): performance numbers in specs are suggestions, never blockers for task completion or later phases. At most **two initial optimization passes per system/feature**, carried across workers and handoffs; benchmark troubleshooting counts too. Record misses or unavailable measurements and move on to integration and live gameplay. Revisit tuning only for a demonstrated integrated bottleneck or an explicit owner request.
- **Done means functional work is done**: functional acceptance criteria checked, correctness/safety tests passing, `tools/compile.sh` clean, ledger updated with the commit hash and a log entry. Report performance-only failures honestly as deferred; they do not keep the task open ([05-testing §4](docs/05-testing.md)).

## 3. Commands

```bash
tools/compile.sh                      # compile (uses the open Editor via the Unity CLI, or batch mode)
tools/test.sh editmode [filter]       # EditMode tests
tools/test.sh playmode [filter]       # PlayMode tests
tools/test.sh perf [filter]           # advisory performance measurements
tools/check-docs.py                   # spec/ledger consistency
tools/ledger-sync.py                  # add ledger rows for new spec tasks
tools/playtest/g.sh snapshot          # live game state from the running Editor (see tools/playtest/README.md)
```

First open of the project imports everything (a few minutes); run `Game > Project Setup > Apply Required Settings` once if `ProjectSettingsGuardTests` complain. Play from any scene boots through `Boot`.

The Unity CLI lives at `~/.unity/bin/unity` (the scripts add it to `PATH`). The project uses `com.unity.pipeline` so the CLI can drive an open Editor. The full tool reference, including headless Unity runs, art tools and known traps, is [docs/08-tools.md](docs/08-tools.md).

## 4. Where things go

| What | Where |
|---|---|
| Game code | `Assets/Game/<Module>/` (one asmdef per module, architecture §3) |
| Content definitions (ScriptableObject classes) | `Assets/Game/Data/Definitions/<Area>/` |
| Content assets (instances) | `Assets/Game/Content/<Area>/` |
| Logic vocabulary (conditions/effects) | `Assets/Game/Data/Logic/` |
| Theme tokens and UI styles | `Assets/Game/UI/Theme/` |
| Scenes | `Assets/Game/Scenes/` (Boot, MainMenu, World, BlueprintLab) |
| Tests | `Assets/Game/Tests/EditMode/<Module>/`, `Assets/Game/Tests/PlayMode/<Area>/` |
| Editor tools, importers, converters | `Assets/Game/Editor/` |
| CLI harness commands | `Assets/Game/DevTools/Editor/` |
| Placeholder art (do not reference from code) | `Assets/Game/Art/_Placeholder/` |
| Legacy content (converter input only) | `Import/Legacy/` |
| Decisions | `docs/decisions/ADR-NNNN-*.md` |

## 5. When you're unsure

Write the question into the spec's *Open questions* table and the ledger's *Open questions* section, with the default you're using, and keep going. Questions only the owner can answer (scope, look, narrative) are marked `Decider: Owner`.
