# ADR-0068 — Explicit functional task dependencies

**Status:** Accepted (Tech lead, 2026-10-13; implements Frank's build-first instruction and ADR-0042)

## Problem

The dependency grammar permits only another module's contract task or an entire spec. Whole-spec expansion makes functional combat and ability work wait for advisory performance tests, and AI wait for weapon balance/owner feel sign-off. Using only a contract task would hide real runtime prerequisites.

## Decision

Permit an explicit existing task ID from another spec as a dependency. Preserve unknown-ID rejection, milestone ordering, cycle detection and the requirement that every listed dependency is done before claiming. Whole-spec and contract dependencies retain their existing meanings. This is workflow metadata only: assembly boundaries, APIs, single writers and runtime contracts are unchanged.

WPN-012, AI-006/010 and ABL-003/004/005 replace the affected whole-spec dependencies with all previously expanded functional tasks, excluding only DMG-016/PWR-009 performance measurements and WPN-013/014 performance/feel work where applicable. These excluded tasks retain their scope, tests and owner-sign-off obligations; they simply do not gate functional development.

## Consequences

Readiness can express actual implementation dependencies without false contract-only readiness or performance blockers. The spec template and checker accept explicit cross-spec task IDs; ledger-sync already expands these correctly. SPEC-21 Q70, SPEC-23 Q16–17, SPEC-24 Q6 and ledger Q286/Q290 record the affected scheduling decisions.
