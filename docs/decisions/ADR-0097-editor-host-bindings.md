# ADR-0097 — Scene editor and persistent screen bindings

**Status:** Accepted (Tech lead, EDT-022, 2026-10-14).

## Decision

EditorHostServices holds the existing UI, input, camera, content/config and optional blueprint-store owners. It registers the editor/browser once, owns their persistent models and forwards commands only to the currently attached presenter. Each scene host attaches on successful construction and detaches before disposal; late modal callbacks check disposal. The launcher supplies the actual leave/settings/name callbacks. No substitute game flow or save owner is introduced. Normal scene-root composition must reuse this editor screen rather than register a duplicate.

ShipEditorController exposes read-only SelectedCount for the public presenter contract. Internal selection storage remains private. The host owns session/context/controller/renderers, borrows all app owners, and releases native resources when its active scene unloads. Palette swatches and prop material come from the actual render definition.

## Consequences

Repeated editor visits retain UI bindings without retaining native sessions. EDT-023 supplies launch/return and commit targets; HUD-002 installs normal roots. Rendering and UI can be validated now without advertising a nonexistent launcher.
