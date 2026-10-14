# ADR-0091 — Editor controller construction and read-only state

**Status:** Accepted (Tech lead, 2026-10-14).

## Context

SPEC-51 defines controller behavior but omits its constructor and the palette/hover state the App presenter must read. Its shell has no dependencies and cannot implement tools or the leave branch of cancellation.

## Decision

Add `ShipEditorController(EditSession session, EditorContext content, Action requestLeave = null)`. It borrows the session/content and owns tool workspaces/previews. Add read-only SelectedBlock, Orient, Color, SliceEnabled, SliceLevel, XRay, BoxMode, Hover and LastError properties. Existing public method and event signatures remain unchanged. An optional leave callback serves the last cancel-chain branch; the App host supplies it. Internal registration and selection state support the later selection/clipboard tasks; their behavior remains explicitly TODO until implemented.

The placement index maintains a separate set of core identities alongside its other indexes. Mirror defaults query that small set, choosing lexicographic origin/block/orientation/colour order on an invalid multi-core draft, instead of rescanning the ship on every toggle. Box removal visits its bounded volume and collects exact block identities before changing cells.

## Consequences

No Game-module references or voxel writer ownership change. The controller disposes only its own storage. Seven SPEC-51 EDT-014 functional cases cover the implementation; unrelated suites and performance runs are not required. Host/render integration remains later tasks, not claimed by these controller tests.
