# ADR-0073 — Persist the camera orientation setting

- Status: Accepted
- Date: 2026-10-13
- Decider: Tech lead
- Scope: HUD-005, SPEC-02 control settings, SPEC-61 settings screen

## Context

SPEC-61 exposes camera orientation, but ControlSettingsData has no corresponding persisted value and SettingsApplier always selects FollowHeading. A UI-only choice would not implement the specified behavior.

## Decision

Add `byte CameraYawModeIndex = 0` to ControlSettingsData: 0 is FollowHeading, 1 is Free. Copy, compare and clamp it with the Controls section; invalid values reset to 0. Core must not reference Presentation. SettingsApplier maps the validated index to Presentation.CameraYawMode, and the settings screen reads/writes this field through the real settings service. Existing JSON without the additive field retains 0, so no version bump or destructive migration is needed.

## Consequences

The setting persists and changes actual camera behavior, while existing player settings keep FollowHeading. Cover round-trip and applied selection within HUD-005's required settings acceptance instead of adding a separate broad suite. Other camera behavior and module references remain unchanged.
