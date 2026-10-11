# Spaceship Game V2 (working title)

A Unity 6.6 DOTS game: build your ship block by block, travel star system to star system uncovering a mystery, and break massive voxel ships and stations apart cell by cell.

This repository is a clean restart of an earlier prototype (see [ADR-0001](docs/decisions/ADR-0001-fresh-project-clean-severance.md)). It currently contains the project skeleton, tooling and the full implementation plan; systems are built task by task from the specs.

## Start here

| If you are… | Read |
|---|---|
| Continuing someone's work | [docs/HANDOFF.md](docs/HANDOFF.md), then [AGENTS.md](AGENTS.md) |
| Implementing anything | [AGENTS.md](AGENTS.md), then [docs/README.md](docs/README.md) |
| Looking for a command or tool | [docs/08-tools.md](docs/08-tools.md) |
| The owner reviewing direction | [docs/00-vision.md](docs/00-vision.md), [docs/ROADMAP.md](docs/ROADMAP.md), [docs/design/DESIGN-GUIDE.md](docs/design/DESIGN-GUIDE.md) (approved 2026-10-11), the [look book](docs/design/lookbook/) |
| Checking progress | [docs/LEDGER.md](docs/LEDGER.md) |

## Open the project

- Unity **6000.6.5f1** (Apple Silicon or Windows). Open the folder in Unity Hub, or `~/.unity/bin/unity open .`
- First open imports everything (a few minutes). Then run `Game > Project Setup > Apply Required Settings` once if `ProjectSettingsGuardTests` complain.
- Press Play in any scene; the game boots through `Boot` (once SPEC-01 is implemented).

## Everyday commands

```bash
tools/compile.sh                 # compile; prints C# errors
tools/test.sh editmode           # EditMode tests (guards for settings, assembly rules, conventions)
tools/check-docs.py              # specs + ledger consistency
tools/playtest/g.sh snapshot     # live state of the running game via the Unity CLI
```

## Layout

```
AGENTS.md            rules for every worker
docs/                handoff, vision, architecture, conventions, glossary, workflow, testing, budgets, tools,
                     roadmap, ledger, specs/, decisions/ (ADRs), design/ (design guide, look book),
                     reference/ (prototype audit)
ArtSource/           Blender look-dev and style-test scripts (procedural, no imported art)
Assets/Game/         all game code and assets, one assembly per module (docs/01-architecture.md §3)
Import/Legacy/       content exported from the prototype; converter input only
tools/               compile/test/doc/art-check scripts and the playtest CLI loop
```

## Source control

Local git only for now. **Before the first push to GitHub**, enable Git LFS and migrate binaries as described in [ADR-0008](docs/decisions/ADR-0008-no-lfs-yet.md).
