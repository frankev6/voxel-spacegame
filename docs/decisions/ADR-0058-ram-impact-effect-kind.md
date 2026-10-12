# ADR-0058 — Complete the ram-impact presentation contract

**Status:** Accepted (Tech lead, 2026-10-12)

## Context

SPEC-13 §5.2 requires an FxEvent with Kind = RamImpact. The shared SPEC-63 FxKind enum ends at ShieldShell = 12, so the specified producer cannot emit that event without extending the shared contract. Frank authorizes autonomous spec-defect resolution with no new owner gates.

## Decision

Append `RamImpact = 13` to Game.Data.Definitions.FxKind and SPEC-63. Preserve every existing numeric value. Extend the indexed defaults array/validation from 13 to 14 entries; no VfxConfig asset has been authored yet. DMG-007 publishes contact position, A-to-B normal and energy-based scale as specified. Effect upload remains VFX-003 and visual content remains VFX-004; this adds no asset or art-direction decision.

## Consequences

The collision producer uses a named, typed event without substituting another effect kind. Existing serialized enum values remain stable. Missing presentation defaults follow SPEC-63's existing staged validation policy. SPEC-13 Q29 and ledger Q191 record this resolution.
