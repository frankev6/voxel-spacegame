# ADR-0079 — Explicit sandbox NPC spawning

**Status:** Accepted (Tech lead, 2026-10-13).

## Context

DEV-009's spawn command uses ShipSpawnService, whose NPC membership guard rejects the deliberately galaxy-free sandbox. The sandbox player and station already omit star-system membership and are cleaned up by SceneFlow's non-boot teardown.

## Decision

The public ShipSpawnService spawn path may omit NPC StarSystemMember only when its composition-bound allocator is GameSession and IsSandbox is true. Missing galaxy state alone is never evidence of sandbox mode. Campaign NPC requests still require a current star system. The explicit IShipSpawner path continues to require its supplied star system even during sandbox sessions. No public signatures, components or profiles change.

DEV-009 removes its redundant current-system precheck and delegates validation to this canonical owner. Extend its existing one-cell spawn fixture to cover an unscoped sandbox NPC and strict campaign/interface rejection; no additional test cases or timed combat.

## Consequences

Sandbox ship commands work without fabricating a galaxy or adding a second spawner. Campaign unload membership remains unchanged. SceneFlow owns cleanup of unscoped sandbox grids and their children.
