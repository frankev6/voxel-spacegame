# ADR-0124 — The extraction loop: the den, the board and the heist replace the open world

**Status:** Accepted (2026-11-10). The owner directed the pivot and approved the design ("this looks good, and well thought out proceed with building it onto the game").

## Context

On 2026-11-10 the owner pivoted the game from an open-world exploration slice to co-op extraction: a crew lives in a den, takes jobs from a board, leaves through a gate, steals a station core and brings it home. His words are recorded in [docs/design/places/README.md](../design/places/README.md). The first heist place (Tollhouse Nine) and the den (the Rock) were agreed as 2D sketches the same day.

## Decision

1. The game's loop is den → board → gate → job → gate → den. The exploration slice (docs/design/exploration/exploration-slice.md) is superseded; its systems (cruise, the Hollow system) stay and serve the loop.
2. The systems are specified in [SPEC-93](../specs/SPEC-93-extraction.md): the magnet tow, the cargo rule, the station core and its tethers, the transponder, watchers and identification, the alarm director, the socket and den power, den building from the ship.
3. Places are built from 2D sketches the owner has agreed (ArtSource/Places/sketch2d.py), then a 3D layout, then blueprints, the way the Hollow is built (DEV-023).
4. The places live in the playground's star system for now, beside the Hollow; galaxy travel between systems is not part of the loop yet.
5. `ProducerKind.Tow = 13` is appended (SPEC-13 contract: append only). AI treats a `Wanted` ship as hostile regardless of faction stance (SPEC-23 amendment).

## Consequences

- The roadmap's open-world milestones (exploration phases 2–7) stop; the extraction tasks EXT-001…012 take their place on the owner-directed track.
- Co-op extraction needs new replicated state (EXT-012) and 3D co-op flight (ADR-0123) before heists are 3D in co-op.

## Alternatives rejected

- A wreck as the heist station, a box with the core in it, a labyrinth built for the puzzle: rejected by the owner in the design rounds.
- Upgrades bought from a menu: rejected ("its not a dumb "pay 1000 to build a laboratory" type of station").
