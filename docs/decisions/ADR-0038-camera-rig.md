# ADR-0038 — Custom camera rig instead of Cinemachine

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), for CAM-004 under Frank’s autonomous technical-decision instruction

## Context
The old-prototype audit, `docs/reference/audit-2026-10/03-galaxy-world.md` §5.11, recommended adopting Cinemachine for flight, tactical and editor views. ADR-0011 deliberately deferred the package until SPEC-62 selected a camera design. V2 now has ECS ships, Unity Physics colliders and context-owned input; SPEC-62 §3 decision 1 selects a small custom rig suited to those boundaries. CAM-002 records that choice before the rig implementation; no package is added by this ADR.

## Decision
Implement `CameraRigService` and `CameraTargetSyncSystem` as specified by SPEC-62, without installing Cinemachine. The five reasons are:

1. Cinemachine's standard de-occluder/decollider uses PhysX queries. V2's runtime colliders belong to Unity Physics (ADR-0004), so the rig needs the specified Unity Physics ray query and own-grid exclusion.
2. Cinemachine targets GameObject `Transform`s. Following the interpolated ECS `LocalToWorld` would require a synced proxy; the custom bridge feeds that state directly to a plain C# service.
3. `CinemachineInputAxisController` reads input directly. Camera input must instead pass through the existing `InputService` contexts and the `ICameraInput` adapter, preserving the architecture's input owner.
4. The required behavior is limited to follow, orbit, zoom, shake, mode blends and origin warp. Pure math helpers and an injected input interface make those behaviors directly testable, including frame-rate independence and the old origin-shift regression.
5. Every package addition requires a deliberate ADR (ADR-0011). A package plus physics, transform and input adapters adds integration work without removing the small custom layer this game already needs.

`Game.App` owns one camera under `GameRoot`, created from `CameraConfig.CameraPrefab`. Only `CameraTargetSyncSystem` reads ECS for it and publishes `MainCameraState`; `CameraRigService` owns the camera transform. Flight reads the smoothed target, uses unscaled half-life damping, warps on origin shifts and cuts on target replacement/teleport. This records the existing SPEC-62 contract; it does not change gameplay ownership or module references.

## Consequences
Camera behavior stays compatible with Unity Physics, ECS interpolation and input contexts. Tests can supply camera input and target poses without proxy GameObjects or package-specific authoring state. The implementation must maintain its own follow/orbit math, blending, occlusion, shake and regression tests; camera cost remains subject to SPEC-62 §8.

There are no Cinemachine authoring tools or cinematic-sequence workflow. Cinematic sequences remain outside v1. If cutscenes become part of the approved scope, revisit the camera integration through a new ADR rather than adding a second camera owner or installing a package implicitly. Content scenes continue to contain no cameras, and listener ownership stays with AudioService, bound through the rig’s pivot (ADR-0039 / SPEC-64).

## Alternatives considered
- **Cinemachine 3 with proxy transforms and manual axis input.** Provides mature authoring and blending tools, but still needs ECS proxies, custom Unity Physics occlusion and a context-aware input adapter. That integration is disproportionate to the present scope.
- **Reuse the prototype camera.** Rejected by the no-old-code rule and its documented edge-on pitch, frame-rate-dependent follow and origin-warp regressions.
- **A camera in each content scene.** Rejected: multiple lifetimes complicate listener ownership, mode continuity and the missing-camera regression; SPEC-62 requires a single app camera.
