# ADR-0100 — Independent encounter follow-up prerequisites

**Status:** Accepted (Tech lead, 2026-10-14).

## Decision

After actual ENC-007 acceptance, use concrete completed owners for independent standard implementation:

- ENC-009: ENC-007 and NAR-011/012/020 provide actual director, logic adapter/event bridge and watchers. Aggregate SPEC-40 includes unrelated pending dialog-dependent checks.
- ENC-012: ENC-007, ENC-004 and DEV-003/005/006 provide the director, faction owner, commands, probes and CLI. It invokes these owners directly; neither narrative routing nor unfinished overlay/performance tools are prerequisites.
- ENC-013: ENC-006/007 and AI-013/014 provide real placement, spawning and formation data/steering. ENC-011 is a later combined integration check, including save and dialogue; it remains required and is not a prerequisite for implementing specified slot placement.

All task scope and tests remain unchanged. No stubs or invented owners; no new ship assets. Lead owns central docs/main and serialized validation; separate worker branches own their listed files. The formation worker changes only formation placement/assignment in the director; lead's ENC-008 save support is reconciled separately. This enables substantial independent implementation without claiming the later integrated scenarios complete.
