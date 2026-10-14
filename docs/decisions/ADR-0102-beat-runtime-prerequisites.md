# ADR-0102 — Concrete beat runtime prerequisites

**Status:** Accepted (Tech lead, 2026-10-14).

## Decision

QST-005 consumes completed QuestService, ClueService, dialog contracts, DialogService, the production runner and presenter. Replace its aggregate SPEC-41 dependency with QST-003, QST-004, DLG-006, DLG-007 and DLG-008. Pending package choice, portraits, localization and scenario coverage do not prevent implementing this runtime owner. No acceptance criterion is waived and no package decision is inferred.

## Consequences

Implement the specified BeatService now; QST-006 adapter and QST-008 world registration retain their own responsibilities. The presenter must subscribe before BeatService so terminal effects run after the dialog screen closes. Save integration remains QST-007.
