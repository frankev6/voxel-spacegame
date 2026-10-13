# ADR-0059 — Preserve the contact normal for hit effects

**Status:** Accepted (Tech lead, 2026-10-12)

## Context

SPEC-63 hit effects require the outward surface normal, but SPEC-21 CombatHit drops the entered face already computed by VoxelRaycast. VFX-004's live range check also found that the staged weapon systems do not emit their specified muzzle, hit or explosion events. Frank authorizes autonomous integration and spec-defect resolution without new owner gates.

## Decision

Append `float3 WorldNormal` to CombatHit. The staged voxel-cell raycast transforms its entered-face normal into world space; a ray starting inside a solid cell falls back to the opposite ray direction. This describes the current cell-level collision, not a new shape-accurate slope narrow phase. No persisted format or enum changes.

Bring the SPEC-63 event producer hooks forward into the existing ordered WeaponFireSystem, ProjectileStepSystem and ExplosionSystem. Register each atomic queue writer through the existing FxEventQueue guard (ADR-0050). Muzzle emits once per firing mount/tick, using twice ProjectileRadius as calibre; hit uses the resolved normal and target linear velocity; explosion emits once per valid request and inherits source linear velocity when available. Preserve weapon FX overrides and PlayerSource flags. Full WPN-003/007/008/010 work remains todo with explicit partial notes.

## Consequences

Real firing drives the authored graphs without redoing collision queries or fabricating a presentation-only hit. The staged systems' other TODOs remain. SPEC-21 Q12, SPEC-63 Q22 and ledger Q197 record the integration and remaining scope.
