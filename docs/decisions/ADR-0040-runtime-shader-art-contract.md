# ADR-0040 — Runtime shaders use the approved art contract

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous technical-decision instruction.

## Context

SPEC-11 §4.4 predates the approved DESIGN-GUIDE and its binding DESIGN-SYSTEM §10.14/10.15 contract. It names different textures, mask channels, damage inputs and palette semantics. Following that older table would make runtime materials incompatible with ART-007's baker. REN-008 also asks for a legacy texture whose use conflicts with Frank's no-third-party-art instruction.

## Decision

- DESIGN-GUIDE and its incorporated art contract win. Brick shaders expose `_BrickAlbedoArray`, `_BrickMaskArray`, `_BrickNormalArray`, `_BrickLayerParams`, `_PaintPalette`, `_CrackTex`, `_DamageRamp`, and all scalar/per-grid properties in DESIGN-SYSTEM §10.14. The mask is metallic/smoothness/paint/emission, not the old SPEC-11 channel layout. Paint replaces hue while retaining texture luminance detail; team slots are 0 and 17–19. Material properties stay in UnityPerMaterial; per-grid properties have DOTS-instanced declarations with material defaults until their presentation writers exist.
- Keep the existing eight-byte vertex, half-cell coordinates, 42 normals, AO, two-bit damage and scorched bit unchanged. The optional exposed-face bit is absent; do not reinterpret scorch as exposure. Code owns decode and passes; `BrickSurface.hlsl` implements the art-facing surface contract for both the registered and future indirect shader. No art keywords.
- PropInstanced supports the specified `_PropInstances`/`_GridMatrices`/`_OrientMatrices` procedural path, alongside the DOTS transform path used by functional entities. It exposes DESIGN-SYSTEM §10.15's material and per-instance properties. No new art assets or material instances per entity.
- VoxelPlaceholderArtBuilder creates procedural neutral plate detail, flat normals, material masks and crack noise. No legacy bitmap is copied. It writes the arrays/materials and supporting art-contract textures under the requested replaceable-art folder; runtime references will arrive through REN-009 definitions. Editor generation paths are permitted; runtime paths remain forbidden.
- Use approved NOCTURNE data/defaults where available; retain a neutral technical placeholder where art has not been supplied. No new ship or visual direction is adopted. The older request for a three-slice crack array is superseded by the canonical crack-distance texture plus damage ramp; the three damage levels still have distinct thresholds.

## Consequences and validation

REN-008 verifies packed palette/AO/damage pixels, actual shadows and procedural prop drawing. Shader compatibility is checked through compiler diagnostics and SRP Batcher compatibility; unsupported platform compilation must be reported, never asserted as tested. REN-003 consumes this shared surface implementation. ART-007's reference shader remains separate and unmodified on its branch; its baker's canonical property names match this runtime contract. Future presentation tasks supply the already-specified per-grid/per-instance values; this task does not invent faction, damage or power writers.
