# ADR-0006 — Narrative: FactStore truth, Yarn Spinner 3 for dialog (after a spike)

**Status:** Accepted with a gate

## Context
The prototype had four action vocabularies, five condition dialects and 42 write-only flags.

## Decision
All narrative state is facts in one `FactStore`. One condition/effect vocabulary (in `Game.Data.Logic`) is compiled at import and fails closed. Dialog is authored in Yarn Spinner 3 **if** the SPEC-41 compatibility spike passes and the owner approves (gate); otherwise the in-house fallback format in SPEC-41 is used. Localization is deferred and will use Yarn line IDs.

## Consequences
Adding Yarn requires a package ADR after the spike.

## Alternatives considered
Ink (rejected: opaque state fights "facts are truth"); keeping JSON events (rejected: unvalidated, string-typed).
