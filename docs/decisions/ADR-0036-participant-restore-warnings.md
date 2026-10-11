# ADR-0036 — Participant warnings in the active load report

**Status:** Accepted (Tech lead, 2026-10-11; autonomous technical-gap authority)

## Context

SPEC-01 requires a fleet structure rebuilt from its blueprint to warn in the load report. ISaveParticipant.Restore has no report parameter, and SaveService.LastLoadReport refers to the previous completed load until the current restore finishes. Logging alone omits the warning from GameLoaded and the load UI.

## Decision

- Preserve the binding ISaveParticipant interface and binary formats.
- Add `SaveService.ReportRestoreWarning(string code, string chunkKey, string field, string message)`. It appends to the active synchronous restore report and throws InvalidOperationException outside Restore.
- Scope that report with try/finally around participant restoration; reject nested Restore calls. No report reference survives the operation.
- Composition supplies FleetSaveParticipant a warning callback using this method. Standalone participants may instead receive a test sink or use GameLog.Warn.
- The ordinary report logging, GameLoaded warning count and OperationCompleted result include these warnings once.

## Consequences

Recoverable participant failures remain successful loads with visible diagnostics. No participant gains access to persistence internals or changes another participant's data. The API works for future recoverable participant warnings without another interface change.
