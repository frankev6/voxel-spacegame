# ADR-0095 — Editor floating-removal command

**Status:** Accepted (EDT-009, 2026-10-14; Tech lead).

## Context

SPEC-51 §5.6 requires RemoveFloatingParts, and the UI command exists, but the EditSession public contract omits its entry point. App must invoke the session's single-writer operation.

## Decision

Add `bool EditSession.RemoveFloatingParts(ApplyReport report)`. It builds one AllOrNothing removal batch, with stroke ID zero and label "Remove floating parts", through the existing TryApply path. False means no removal or validation refusal; report contains the normal result. No new assembly references or alternate draft writer.

For damaged blocks, use surviving owned cells rather than assuming the origin is occupied. Retain the core's first surviving island (largest island if no core). Remove non-core blocks with any surviving cells outside that island. A split damaged core remains an explicit disconnected validation issue; this command never deletes the core to hide it.

## Consequences

The host can wire the existing UI command without rebuilding connectivity or modifying cells itself. Undo restores removed blocks and damage in one operation. Validation always has an unthrottled internal path for commit; public issue refresh is limited to once per 0.25 seconds after edits.
