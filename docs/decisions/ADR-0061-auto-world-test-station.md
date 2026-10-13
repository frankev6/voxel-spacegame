# ADR-0061 — Automatically spawn the aligned test station

**Status:** Accepted (Tech lead, 2026-10-13; explicit owner request)

## Decision

Add `FiringRangeConfigDefinition.AutoSpawnInWorld`, default false for new definitions and enabled in the production range config for the current testing phase. FiringRangeService implements the existing scene-ready hook at PlayerShip + 1, after the player's spawn completes. It requests the same static station through the existing grid spawn pipeline and waits for its spawn completion. No player means no target. Existing scene-exit ownership remains unchanged.

Lower the production offset from Y=0 to Y=-2 metres, preserving X/Z and the three-times-size geometry. Its three-cell floor then straddles the firing plane instead of beginning at its edge. Repeated Spawn/range calls return the existing target; restarting World creates a fresh one.

## Consequences

Direct World Play, new games and World returns with a player get the temporary test target automatically. The setting is reversible when the real encounter system replaces the range. No new gameplay service or architectural dependency is introduced. One existing real-fire feature test gains automatic-spawn, placement and idempotence assertions; existing restart coverage remains in use.
