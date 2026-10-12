# ADR-0057 — Bound the staged full-grid integrity fallback

**Status:** Accepted (Tech lead, owner-requested external review fix, 2026-10-12)

## Context

ADR-0053 permits a temporary full-grid connectivity flood for the playable range. Architecture §11.1 requires an explicit decision and bound for any whole-grid cell pass. The fallback also rescanned every node for every component/slot pair.

## Decision

Add `DamageConfig.MaxFullFloodCells`, authored through DamageConfigDefinition, default **16384** occupied cells. Count occupied cells from brick metadata before allocating nodes or flooding. Oversized changed grids retain existing seeds, or receive the destroyed-cell seed when newly changed; increment `DamageTickStats.IntegritySizeDeferred` and warn once per grid identity. Never force this fallback past its cap or discard pending work. It retries when the size drops under the configured cap; DMG-008/009 replace it with the specified brick summaries and budgeted hybrid race.

Build island masks in one node pass, bucketing by component label and slot index. Sort only the resulting buckets by label/slot before emitting masks, preserving canonical PendingDetach order.

## Consequences

This is a bounded temporary exception under architecture §11.1, not station-scale integrity completion. The cap covers the procedural test station. The guard is a work bound, not an advisory benchmark target. No owner gate is added. Required review tests cover repeated deterministic deferral with retained seeds and exact mask equivalence to the previous slot scan on the station breakaway.
