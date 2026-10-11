# Documentation index

Continuing someone's work? Start with [HANDOFF.md](HANDOFF.md). Otherwise read in this order. Documents marked **binding** may only change through an ADR.

| # | Document | What it answers | |
|---|---|---|---|
| 0 | [00-vision.md](00-vision.md) | What is the game? What's in and out of scope? | binding (scope) |
| 1 | [01-architecture.md](01-architecture.md) | Modules, frame pipeline, shared types, who writes what, coordinate frames, spec map | **binding** |
| 2 | [02-conventions.md](02-conventions.md) | How code is written | **binding** |
| 3 | [03-glossary.md](03-glossary.md) | What words mean | **binding** |
| 4 | [04-workflow.md](04-workflow.md) | How to claim, do and finish a task; ambiguity and contradiction rules; **task tiers** (which model may take which task) | **binding** |
| 5 | [05-testing.md](05-testing.md) | Commands, test kinds, Definition of Done, regression scenarios | **binding** |
| 6 | [06-performance-budgets.md](06-performance-budgets.md) | Perf gates | **binding** |
| 7 | [07-implementation-traps.md](07-implementation-traps.md) | The DOTS, physics, rendering, UI and save mistakes that compile but fail later; review checklist | **binding** |
| 8 | [08-tools.md](08-tools.md) | Every tool and how to run it: Unity CLI and headless runs, playtest loop, art and Blender tools, git; known traps | living |
| — | [HANDOFF.md](HANDOFF.md) | Owner's standing rules, where the work stands, what's next | living |
| — | [ROADMAP.md](ROADMAP.md) | Milestones M0–M4 and the art track, with exit gates | binding (order) |
| — | [LEDGER.md](LEDGER.md) | Task status, open questions, measurements, log | living |
| — | [specs/](specs/) | One spec per system with numbered tasks ([template](specs/_TEMPLATE.md)) | per spec |
| — | [decisions/](decisions/) | ADRs | binding when accepted |
| — | [design/DESIGN-GUIDE.md](design/DESIGN-GUIDE.md) | The look: colour, light, hulls, blocks, factions, UI tokens and fonts (NOCTURNE) | **binding** (approved 2026-10-11) |
| — | [design/DESIGN-SYSTEM.md](design/DESIGN-SYSTEM.md) | The original six-direction proposal | reference (superseded by the guide) |
| — | [design/lookbook/](design/lookbook/) | Rendered 3D concepts, silhouettes, the slope study | reference (concepts, not final) |
| — | [reference/audit-2026-10/](reference/audit-2026-10/) | Audit of the old prototype: why this architecture exists | background only |

## Spec list

See [01-architecture.md §10](01-architecture.md#10-spec-map-and-area-codes) for the full map of specs, area codes and modules.
