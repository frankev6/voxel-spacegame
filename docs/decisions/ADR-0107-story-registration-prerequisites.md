# ADR-0107 — Concrete story registration prerequisites

**Status:** Accepted (Tech lead, 2026-10-14).

## Decision

QST-008 consumes the completed quest, clue, beat, adapter, query and save owners, narrative context and world bridge, travel and encounter services, and dialog presenter. Replace aggregate SPEC-40/32/33 prerequisites with QST-005/006/007, NAR-011/012, TRV-006, ENC-007/009 and DLG-008. Those concrete dependencies are done. Unrelated scenario, content and performance work in those specs is not a registration prerequisite.

## Consequences

No public contract or behavior changes. QST-008 still implements every routing/gating/registration requirement and five specified functional checks. Lead validates the actual integrated composition; source delivery alone does not mark it done.
