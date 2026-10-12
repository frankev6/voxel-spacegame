# ADR-0044 — Temporary scene-owned flight context before UI navigation

**Status:** Accepted (Tech lead, 2026-10-12; owner's playable-slice priority)

## Context

INP-005 and the flight/camera systems are implemented, but InputService stays in None until the future UI navigator exists. DEV-008's player.ready condition also requires Flight. Waiting for the whole UI prevents the imported-ship checkpoint requested by Frank.

## Decision

- Until UI-004 lands, `SceneInputContextBridgeSystem` in Game.App is the sole production caller of InputService.SetContext. Composition explicitly binds SceneFlow, IPauseService and InputService. It runs in GameInitGroup before PlayerInputSampleSystem, including outside gameplay, using the boot-owned PlayerCommand singleton as its update requirement.
- Context is None during transitions or pause, Flight in World, Editor in BlueprintLab, Menu in MainMenu. It reads current state before sampling, so deferred events cannot leave stale flight commands active. Escape toggles only a temporary pause token owned by this bridge in World; scene transitions and disposal release it. Other pause owners remain authoritative.
- UI-004 must unbind/remove this fallback when UINavigator takes ownership through InputServiceBridge. Never run two context owners. The public InputService API and action bindings do not change.
- FND-017 precedes DEV-008 and uses the existing CLI plus the real IGameFlow API for live validation. It selects the already imported Fighter as temporary starter and uses the approved DESIGN-GUIDE lighting; no new hull design or art approval is inferred.
- FLET v1 preserves identity, blueprint and voxel data. Position/velocity remain the documented M1 origin/zero spawn until canonical world-location persistence lands; the checkpoint must state that limitation.

## Consequences

The game can be flown and tested now. This is a small, explicitly removable bridge, not a second UI/navigation system. The UI handoff is recorded in SPEC-60 UI-004, SPEC-03 and architecture §8.
