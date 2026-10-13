# ADR-0065 — Store formations in the boot-owned AI blob

**Status:** Accepted (Tech lead, 2026-10-13; owner-authorized spec-gap resolution).

## Context

SPEC-23 AI-013 requires formation baking into AiConfig but does not declare the formation blob layout. AiConfig already owns an AiProfilesBlob through the established boot lifetime. FleetMember stores a ushort FormationIndex and byte Slot. A second owner or a managed array in simulation would add unnecessary lifetime and access rules.

## Decision

Extend Game.Simulation.Ai.AiProfilesBlob with `BlobArray<FormationDef> Formations`. Add Game.Simulation.Ai.FormationDef with `Game.Core.ContentHash IdHash` and `BlobArray<Unity.Mathematics.float2> Slots`. Slots are leader-local metre offsets (x right, y forward) in authored order. Use ContentCatalog's one-based FormationDefinition indices; index zero is an empty formation with no follower offsets.

AiContent bakes this array with the profiles into the existing persistent blob. BootSingletons retains the same sole ownership and disposal path. No new singleton, assembly reference, runtime writer or save-format field is introduced. FleetMember.FormationIndex and Slot address the baked array and its offsets. AI-014 owns runtime formation steering; AI-013 does not claim that behavior.

## Consequences

Consumers can read immutable formation offsets from the same AI blob already available to simulation jobs. Existing profile indices and defaults remain unchanged. AI-013's two required tests verify content-index correspondence, exact offsets and spacing validation; no extra test suite or optimization pass is required. SPEC-23 records the concrete layout and procedural formation defaults.
