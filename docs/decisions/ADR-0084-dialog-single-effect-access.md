# ADR-0084 — Guarded single-effect access for dialog commands

**Status:** Accepted (Tech lead, 2026-10-13).

## Context

DLG-006's command lookup returns IEffect and must distinguish AbortBeatEffect and terminal/blocking flow. Generated command entries contain a CompiledEffectList with private nodes. Wrapping the list as a generic effect loses that concrete identity.

## Decision

Add CompiledEffectList.TryGetSingleEffect(out IEffect). It returns the existing node only when compilation is current, error-free and contains exactly one non-null effect; otherwise false with null. It neither parses nor compiles nor binds. Boot's existing content binding must precede execution. The ordinal DialogCommandTable validates every generated entry during construction and rejects malformed, duplicate or stale commands rather than reparsing at runtime.

## Consequences

No assembly references change; the compiled graph remains owned by its content definition. Existing list APIs remain unchanged. DLG-006's required command/lifecycle cases exercise the lookup; no separate accessor-only test suite is added.
