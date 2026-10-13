# ADR-0072 — HUD composition sequencing

**Status:** Accepted (Tech lead, 2026-10-13; Frank's playable-slice instruction and ADR-0068)

## Problem

HUD-002 depends on all of SPEC-60, including independent audits/accessibility work. Its composition instructions also require the completed HUD synchronizer, hotkeys and screen presenters even though their tasks depend on HUD-002. This makes the stated sequence circular. The existing M1 adapter and playable menu cannot simply be removed while replacement screens are empty shells.

## Decision

HUD-002 depends explicitly on UI-001 through UI-011, HUD-001 and FND-001. UI-012/013 remain required work, but do not gate registering screens or scene roots.

Implement HUD-002's app-lifetime view-model ownership, registration, root switching, session rebinding and navigator ownership first. Its four functional acceptance cases remain unchanged. Replace the temporary adapter only when replacement root behavior preserves the existing playable menu, loading, pause and flight controls. Bring forward the minimum real root behavior where necessary, with precise TODOs and partial ledger notes for its owning screen task; do not install blank roots or no-op command facades to satisfy tests.

Feature presenters, complete screen layouts, HUD synchronization and hotkey bindings are completed and installed by their owning HUD-003 through HUD-013 tasks as their real services become available. HUD-007 retains UI-012's audit requirements, and HUD-009 retains real travel/route requirements. HUD-002 is not evidence that those features or the full UI composition are finished. Each owning task must include its production composition hookup, not only isolated tests.

## Consequences

The composition foundation can unlock independently implementable screens without inventing services or discarding downstream obligations. No assembly/API contract or owner art decision changes. Full-screen and integrated gameplay acceptance remain required by the owning tasks and milestone review; performance measurements remain advisory.
