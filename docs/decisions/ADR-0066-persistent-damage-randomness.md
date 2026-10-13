# ADR-0066 — Persistent identity for cell damage randomness

**Status:** Accepted (Tech lead, 2026-10-13; owner-authorized spec-gap resolution).

## Context

DMG-014's equivalent fresh Worlds assign different Entity.Index values to persistent ships (for example id 100 received 524563 then 524560). SPEC-13's index-seeded stochastic HP rounding therefore produces different cell hashes even with identical ticks, cells, events and persistent identities. Entity allocation is not durable gameplay identity.

## Decision

CellWriter uses a grid's nonzero PersistentId.Value as its 64-bit random identity for HP quantization and removed-cell kicks. Grids without a nonzero persistent id retain the entity-index fallback. DamageAccess caches and updates a read-only PersistentId lookup; the resolver passes the identity to its exclusive writer. Retain the public integer DamageMath.Dither01 helper for existing callers; the runtime uses an internal ulong overload with the same tick/cell/ordinal hash formula.

This does not change event ordering, stream contracts, save bytes, damage thresholds or test acceptance. Presentation-only dissolution and separate chunk spin retain their existing seeds; this decision concerns authoritative cell rounding and the matching CellWriter kick stream.

## Consequences

Persistent ships no longer depend on unrelated world allocation history for fractional HP loss. Existing saves remain readable; future rounding differs from the old allocator-dependent behavior. DMG-014's existing three required tests retain their six-configuration hash gate and one-quantum sensitivity assertion. No extra tests or optimization pass.
