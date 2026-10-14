# ADR-0092 — Draft prop instancing and slice bindings

**Status:** Accepted (Tech lead, 2026-10-14).

## Context

SPEC-51 EDT-018 requires conventional RenderMeshInstanced drawing with one shared prop material. The existing PropInstanced shader preserves paint only in its procedural live-world path; its conventional path hardcodes palette index zero. Editor slices also need to cut prop geometry at the same inclusive cell plane as bricks.

## Decision

Extend the existing shader with a conventional instanced `_DraftPaintIndex` (default zero) and property-block-only `_DraftSliceEnabled`, `_DraftSliceTop` and `_DraftWorldToCells`. In the conventional path, read the instance paint and clip fragments above the grid-local slice plane. The procedural live-world path retains its packed paint and ignores editor clipping. No texture, art, simulation or entity contract changes.

DraftRenderer borrows an explicit shared prop material and mesh table. It reuses 500-entry (below the 1,023 API ceiling) matrix and paint arrays, transforms local instances by GridToWorld, and supplies exact transformed batch bounds. Slicing filters wholly above-plane prop footprints and clips intersecting geometry in the shader. Brick draws also supply transformed mesh bounds. The host owns shared materials; the renderer never mutates them or allocates per-block materials.

## Consequences

Paint survives conventional instancing; the live indirect renderer remains unchanged. Material instancing must be enabled by the host/material asset. The six required EDT-018 cases and a graphics-enabled integration check validate this with the rest of the renderer; this source checkpoint does not establish visual acceptance.
