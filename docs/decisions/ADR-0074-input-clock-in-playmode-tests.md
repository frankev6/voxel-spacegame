# ADR-0074 — Input clock control in PlayMode acceptance

- Status: Accepted
- Date: 2026-10-13
- Decider: Tech lead
- Scope: HUD-005 required rebinding acceptance

## Context

The required rebinding case needs to advance the Input System clock. The existing EditMode test assembly already references Unity.InputSystem.TestFramework; PlayMode does not. Waiting out real time would contradict the owner's fast-fixture policy.

## Decision

Allow Game.Tests.PlayMode to reference Unity.InputSystem.TestFramework, the already installed test-only assembly. Use its fixture to advance input time directly and restore test input state afterward. Keep the required timeout/cancellation behavior and functional assertions unchanged. No production assembly reference or new package is introduced.

## Consequences

HUD-005 can exercise the real rebind flow without incidental wall-clock waiting. This does not authorize removing timeouts from production or loosening module boundaries elsewhere.
