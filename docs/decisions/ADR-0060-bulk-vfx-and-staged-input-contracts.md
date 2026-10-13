# ADR-0060 — Complete VFX as one owner-requested batch

**Status:** Accepted (Tech lead, 2026-10-12; owner explicitly requests bulk implementation and final-only testing)

## Decision

VFX-015 coordinates VFX-005…014 on one branch. Implement the full batch before validation; fix discovered failures in the final validation phase. Mark individual rows done only after their functional checks pass.

Bring SPEC-22 ShieldState, ShieldFlags and ShieldRipple, and SPEC-31 StarSystemEntered/StarSystemEntryReason forward unchanged in their canonical modules. Their owning tasks stay incomplete. VFX consumes existing Core OriginShiftRecord for same-frame rebases rather than inventing a second OriginShifted singleton: SPEC-30 already specifies this mirror, while OriginShifted is a deferred managed event. SessionReset and StarSystemEntered clear the rig. Origin and warp effects can be verified with these canonical inputs before gameplay travel/streaming lands; those producers remain their owning tasks.

Use SPEC-13's actual ChunkBody.EndTick and SimClock.Tick to derive remaining seconds, instead of adding the obsolete TimeLeft field mentioned by SPEC-63. The Presentation system remains sole writer of ChunkDissolve.

Triple the station's occupied coordinate ranges using more ordinary cells, retaining its layered bays, bridges and functional structures. Keep the renderer/physics cell size unchanged. Enlarge the fixture through its generator, not LocalTransform.Scale.

## Consequences

All effect consumers/content can be completed without falsely completing power, galaxy or travel systems. One final compile, full suite and grouped GPU/live validation replaces repeated per-task suite runs. Performance remains advisory; no new owner gates. SPEC-63 Q23 and ledger Q198 record this scope.
