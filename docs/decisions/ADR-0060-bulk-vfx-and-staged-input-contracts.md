# ADR-0060 — Complete VFX as one owner-requested batch

**Status:** Accepted (Tech lead, 2026-10-12; owner explicitly requests bulk implementation and final-only testing)

## Decision

VFX-015 coordinates VFX-005…014 on one branch. Implement the full batch before validation; fix discovered failures in the final validation phase. Mark individual rows done only after their functional checks pass.

Bring SPEC-22 ShieldState, ShieldFlags and ShieldRipple, and SPEC-31 StarSystemEntered/StarSystemEntryReason forward unchanged in their canonical modules. Register the star-system event channel at boot, following CORE-009. Their owning tasks stay incomplete. VFX consumes existing Core OriginShiftRecord for same-frame rebases rather than inventing a second OriginShifted singleton: SPEC-30 already specifies this mirror, while OriginShifted is a deferred managed event. SessionReset and StarSystemEntered clear the rig. Origin and warp effects can be verified with these canonical inputs before gameplay travel/streaming lands; those producers remain their owning tasks.

Use SPEC-13's actual ChunkBody.EndTick and SimClock.Tick to derive remaining seconds, instead of adding the obsolete TimeLeft field mentioned by SPEC-63. The Presentation system remains sole writer of ChunkDissolve.

Triple the station's occupied coordinate ranges using more ordinary cells, retaining its layered bays, bridges and functional structures. Keep the renderer/physics cell size unchanged. Enlarge the fixture through its generator, not LocalTransform.Scale.

## Consequences

All effect consumers/content can be completed without falsely completing power, galaxy or travel systems. One final compile, full suite and grouped GPU/live validation replaces repeated per-task suite runs. Performance remains advisory; no new owner gates. SPEC-63 Q23 and ledger Q198 record this scope.

## Validation and authored layout

Keep the existing Vfx test and tracer-system locations; place instanced materials beside their content definitions. The approved DESIGN-GUIDE §9 colours override older example colours in SPEC-63: white-blue plumes and pale jade shields. Unity Physics 6 stores MostRecentFixedTime in a singleton buffer indexed by physics world, not an IComponentData singleton; use index 0 when present.

The origin test uses the production debris graph: freeze its live particles, compare rendered 64×64 images before/after the same-frame anchor/camera shift, and read back 16 submitted GPU records to verify the numeric anchor transform within 0.01 m. This replaces the proposed instrumented test graph; the numeric readback is of submitted positions, not private simulated particle storage. The image comparison and unchanged live count verify that production particles follow without respawning. Missing GPU timing remains an explicit unavailable perf record, not a zero-time pass. See SPEC-63 Q24 / ledger Q199.

## Event delivery and producer synchronization

GPU integration exposed a read-before-producer-completion in beam gathering. State bridges explicitly complete the writers of the components they read before their synchronous gather. Authored graphs use per-render-frame DeltaTime and continue simulation offscreen: fixed-step VFX could consume an overwritten shared event buffer when multiple render frames arrived between simulation steps (50k requested became 30k). Offscreen suspension would also invalidate the lifetime estimate. Culling still applies to rendering and CPU event selection. Pooled restart uses Reinit followed by its custom event, without an intervening Stop event that can suppress the start. These are correctness fixes, not optimization passes (Q25 / ledger Q200).

A one-off Metal render of all four instanced materials also caught `point` being a reserved shader token in the shield varying. Rename it `surfacePoint`; CPU gather tests alone cannot establish shader compilation or visible output. The temporary render job is removed after capturing its evidence.
