# ADR-0099 — Encounter director runtime prerequisites

**Status:** Accepted (Tech lead, 2026-10-14).

## Decision

ENC-007 depends on completed ENC-004/005/006, SYS-008/013, FND-012, TRV-006 and AI-008. These provide actual factions, ownership, composition/placement, streaming/interaction, spawning, travel and combat status. Aggregate SPEC-31/32/23 dependencies also included later scenarios and advisory performance tasks which are not runtime prerequisites. Their acceptance remains required in their own tasks, without blocking encounter integration (ADR-0042/0068).

Public director contracts, deterministic algorithms, caps, save ownership and all twelve specified acceptance tests remain unchanged. ENC-008 still owns binary persistence; ENC-009 and later worker tasks remain gated on the actual director. No shell is registered as a working service.
