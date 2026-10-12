# ADR-0039 — Bind the camera to the audio service's sole listener

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous technical-decision instruction

## Context
SPEC-62 CAM-004 asks for an AudioListener on MainCamera.prefab, while SPEC-64 decisions and its implemented AUD-003 service require the only listener under AudioRoot and prohibit listeners in prefabs/scenes. Adding the camera listener would violate that existing ownership and fail the listener regression. The camera also needs its scheduled SPEC-01 composition bindings before the later input and VFX adapters exist.

## Decision
Keep AudioService as the sole creator, owner and disposer of AudioListener. MainCamera.prefab contains Camera and UniversalAdditionalCameraData only. CameraRigService owns a separate child transform exposed as `public Transform ListenerPivot { get; }`; in Flight with a target it follows the target's position with camera rotation, otherwise it follows the camera pose. GameComposition calls the existing `AudioService.SetListenerTarget` when creating the rig and `ClearListenerTarget` before disposing it. The rig destroys its pivot with its camera; it does not create an AudioListener or discover audio through a global lookup.

Add the already specified `CameraConfig` field to GameRootConfig and wire the camera at SPEC-01 step 11, before settings propagation and the boot snapshot. Construct with null input (Sample is a no-op) until CAM-005 installs InputCameraInput, and bind a null Fx reader until CAM-008 installs the typed adapter. Scene-ready hooks choose Menu, Flight or Hold (the Lab awaits CAM-009). Bind the existing optional thumbnail camera. Settings map mouse sensitivity/inversion and accessibility shake/reduce-motion; FollowHeading remains the documented default because persisted controls do not yet contain a yaw-mode setting. Clamp camera sensitivity to its specified range without changing the input-service range.

CameraTargetSyncSystem creates MainCameraState in OnCreate, and updates the existing optional VoxelViewer singleton as SPEC-11 requires. No new assembly or package reference is needed. ADR-0038's custom rig decision is accepted alongside this integration.

## Consequences
There remains exactly one listener through boot and content-scene transitions. Audio and camera retain separate ownership and disposal; the app supplies their binding. The listener pivot is one new public read-only transform contract, and settings and scene-flow wiring become testable with the existing composition tests. Flight, occlusion, origin warp, shake and editor orbit algorithms remain owned by their specified later tasks.

## Alternatives considered
- Add another listener to the camera: violates SPEC-64 and creates duplicate listener warnings.
- Move the listener out of AudioService: breaks its existing construction, no-camera and lifetime contracts for no functional gain.
- Make the rig locate AudioService globally: hides a cross-service dependency; app composition already owns this binding.
