# ADR-0020 — Disable scene reload for the iteration contract

**Status:** Accepted (Tech lead, 2026-10-11; autonomous implementation authority from Frank)

## Context
SPEC-71 TST-008 requires both domain and scene reload disabled. ProjectSetup applied only DisableDomainReload, so its new acceptance test failed. Architecture §4.1 required domain reload disabled but did not specify the scene setting.

## Decision
Disable both reloads in the applied Editor settings and the settings guard. This fulfills the existing TST-008 contract; no runtime initialization API changes. Architecture §4.1 now states both settings explicitly. Boot/reentry and full PlayMode tests must pass with the setting applied.

## Consequences
Runtime initialization and teardown must not rely on Unity reloading scene objects between Play sessions. SubsystemRegistration still resets mutable statics. Existing second-Play and scene teardown tests verify these guarantees, and the M0 system review checks their integration.

## Alternatives considered
- Keep scene reload and weaken TST-008: avoids the settings change but silently drops its acceptance criterion.
- Disable both reloads: matches the specified iteration setup and retains the boot/reentry regression checks. Selected.
