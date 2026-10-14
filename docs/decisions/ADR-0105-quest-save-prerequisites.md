# ADR-0105 — Quest save construction and concrete prerequisites

**Status:** Accepted (Tech lead, 2026-10-14).

## Decision

QST-007 serializes the completed QuestService, ClueService and BeatService state directly. Its concrete prerequisites are QST-003, QST-004, QST-005 and PERS-001; it does not consume ShipLogQuery or NarrativeLogicAdapter. This permits save implementation while QST-006 completes, without claiming its acceptance.

Construct QuestSaveParticipant with the shared ContentCatalog, FactStore, QuestService, ClueService and BeatService. Snapshot/restore helpers and raw unknown records stay internal to Narrative and owned by their services, so NewGame clears them. Parse and validate the entire payload before replacing live state; restore performs no effects, notifications or facts writes. NarrativeService retains responsibility for its subsequent OnRestored evaluation. Production registration remains QST-008 after the real participant exists.

## Consequences

No format or module-boundary change; version1 follows SPEC-42 §4.6. All required functional checks remain mandatory. Unknown IDs and baseline names survive round trips, while unresolved queued beats remain dormant rather than being discarded. Advisory capture timing never delays integration.
