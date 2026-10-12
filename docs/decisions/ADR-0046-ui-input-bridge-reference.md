# ADR-0046 — UI input bridge package reference

**Status:** Accepted (Tech lead, 2026-10-12)

## Context

SPEC-60 UI-003 implements InputServiceBridge over InputService.Controls.Global.Back. The action's performed callback uses Unity.InputSystem types, while Game.UI currently references Game.Input but not the package assembly that owns those types. Assembly references do not propagate transitively.

## Decision

Add the existing Unity.InputSystem assembly to Game.UI. No package version, Game.* module dependency, shared contract or single-writer rule changes. InputServiceBridge forwards context and bindings and owns its Back subscription. Composition does not bind Back or transfer context ownership until UI-004 removes the ADR-0044 fallback.

## Consequences

The specified bridge compiles without reflection or a new InputService API. UI still accesses input through its declared Game.Input dependency and continues to have no ECS access. UI-004 owns the coordinated navigation handover.
