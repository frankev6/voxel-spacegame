# ADR-0075 — Effect continuation and rejection hooks

**Status:** Accepted (Tech lead, 2026-10-13).

## Context

SPEC-40 requires EffectRejected tracing from Data-owned effect lists. SPEC-41 requires OpenMerchantEffect to pass the dialog continuation to ILogicWorld.OpenMerchant. ILogicContext exposes neither operation, and Data must not reference Narrative or App.

## Decision

Add the optional Data-owned ILogicEffectHost interface beside ILogicContext. It exposes read/write Action BlockingCommandCompleted and void EffectRejected(). The setter lets the dialog owner install and restore its continuation through the Data interface without referencing the App implementation. The app LogicContext (NAR-011) implements it; the dialog owner (DLG-006) installs/restores the current continuation through its context integration, and the trace owner receives rejection events. No service locator or cross-module reference is added.

CompiledEffectList sends rejected preflight events to this interface when present. Contexts without a trace host remain usable. OpenMerchantEffect requires a non-null continuation, passes that exact delegate to ILogicWorld, and rejects execution when it is missing rather than leaving dialog permanently blocked or pretending it completed. The callback is captured before calling the world owner.

## Consequences

Existing ILogicContext implementations remain source-compatible. Production callback and trace integration remain explicit NAR-011/DLG-006 work. Effect tests supply a real recording host; they do not replace the production owner. No new owner approval gate is introduced.
