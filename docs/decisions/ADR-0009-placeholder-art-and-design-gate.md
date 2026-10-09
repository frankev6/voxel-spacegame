# ADR-0009 — All current art is placeholder; production art waits for design-system approval

**Status:** Accepted (owner, 2026-10-09)

## Context
The owner stated that all mesh, visual and UI assets are placeholders and that he must approve one coherent design system before new assets are made.

## Decision
Carried-over art lives in `Assets/Game/Art/_Placeholder/` and is never referenced from code directly. SPEC-80 produces the design-system proposal; the task "GATE: Owner approves design system" blocks every SPEC-81 asset-production task. Placeholders are removed and the folder deleted in step A2.

## Consequences
Gameplay work proceeds with placeholders; all visuals stay swappable through definitions and tokens.

## Alternatives considered
Producing art now (rejected by the owner).
