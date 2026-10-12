# ADR-0047 — Navigator-owned input before the full scene screens

**Status:** Accepted (Tech lead, 2026-10-12; preserves the owner's playable-slice priority)

## Context

UI-004 replaces ADR-0044's direct input writer. HUD-002's scene roots are M3 work, so an empty navigator would disable the existing playable ship. External pause and transitions must still neutralize held flight commands before input sampling.

## Decision

- Remove SceneInputContextBridgeSystem and its Escape pause token. UINavigator becomes the sole production caller through InputServiceBridge of InputService.SetContext.
- A temporary SceneNavigationBridgeSystem runs in GameInitGroup before PlayerInputSampleSystem. It only navigates: it selects an M1 scene root whose context is Flight, Editor, Menu or None, uses None during transitions, and pushes a non-pausing None-context blocker while a pause owner other than the navigator is active. It never writes input context or acquires a pause token.
- The M1 scene root is transparent metadata. Back in World or BlueprintLab opens a minimal Paused/Resume view; its pause token and Menu context belong to the navigator. A Resume button and Back both pop that view. This is the temporary checkpoint UI, not a claim that HUD-003/006/007 or the final NOCTURNE screen designs are finished.
- The three temporary views use authored UXML and existing theme classes. No new art or inline visual styles. HUD-002 replaces these roots and removes the temporary bridge/registrations before installing the full scene UI; input ownership stays in the navigator.
- Navigation reentrancy is queued, including Back callbacks. Bound both pending count and operations drained per outer call to 16 so a one-at-a-time callback loop cannot hang. A queued Pop returning true means accepted for deferred execution; actual membership is evaluated when drained. Failed screen pushes never focus or play an open sound for a hidden view.

## Consequences

The scene adapter reads current flow/pause state at the same point as the previous bridge, preserving real controls and pause safety while the full screen composition is pending. The UI framework keeps its top-screen context rule and one pause token. Future scene UI must replace, not coexist with, the temporary adapter.
