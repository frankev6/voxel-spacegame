# ADR-0048 — Editor input sampling belongs to the App host

**Status:** Accepted (Tech lead, 2026-10-12)

## Context

SPEC-03 places EditorInputSampler in Presentation and references a nonexistent Game.ShipEditor.EditorInputFrame. Architecture §3 forbids Presentation from referencing ShipEditor. SPEC-51 already assigns this sampler to App/EditorHost and defines the canonical frame in Game.ShipEditor.Data. INP-006 precedes EDT-001, so a test-only enum copy cannot make the production sampler compile.

## Decision

- Implement Game.App.EditorHost.EditorInputSampler, as SPEC-51 specifies. Its Sample method takes ref Game.ShipEditor.Data.EditorInputFrame. No assembly reference changes.
- INP-006 declares the exact EditorActions and EditorInputFrame types from SPEC-51 §4.2. EDT-001 reuses these types; EDT-024 composes the existing sampler instead of implementing a second one.
- Secondary has pressed/released fields only, matching the canonical frame. Primary also has held. The sampler fills every field each frame and clears the entire frame when the Editor map is disabled.
- InputCameraInput stays in Presentation and receives the Editor branch. Raw orbit and pan pixels are left for the camera rig; zoom sensitivity is applied once in the adapter.

## Consequences

The input task is buildable before the full ship editor without breaking module boundaries or introducing duplicate contracts. These types alone do not implement a usable Blueprint Lab; EDT-024 remains responsible for host composition and its lifecycle.
