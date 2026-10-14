# ADR-0088 — Editor batch storage and application report

**Status:** Accepted (Tech lead, 2026-10-13).

## Decision

Complete SPEC-51's staged EditBatch and ApplyReport identities in Game.ShipEditor. EditBatch owns persistent native lists Ops (DiffOp), Damage (byte), Groups (BatchGroup), with Clear and idempotent Dispose. Tools retain ownership; EditSession copies accepted data into its owned DiffCommand. Empty Groups means one implicit group spanning all ops. Explicit groups must form an ordered, nonoverlapping partition; malformed input is rejected before writes. Placement validation itself remains the explicit EDT-007 follow-up.

ApplyReport owns a reusable per-group PlacementError list exposed read-only, AcceptedGroups and OpsApplied, reset on every attempt. Session/validator alone populate it. DirtyBrickSet is internal session state, with Add(brick), AddCell(cell), Clear and Drain(output), which clears the supplied native output then empties the set. Arithmetic floor division by 16 preserves negative coordinates; only exact brick faces add neighbours. Dirty order does not alter simulation or persistent data.

No new assembly references, other-module API changes or premature validator/connectivity/BOM behavior. EDT-005 still supplies the specified pass-through placement stage and retains explicit later-task markers.
