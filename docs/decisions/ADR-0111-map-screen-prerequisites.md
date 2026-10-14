# ADR-0111 — Concrete map-screen prerequisites

**Status:** Accepted (Tech lead, 2026-10-14).

## Decision

HUD-016 consumes HUD-015's accepted control, GAL-008/011's galaxy/service map model, SYS-007/008's loader/interactions, TRV-006's travel owner and QST-006's canonical marker/query API. Replace aggregate SPEC-30/31 prerequisites with these concrete completed tasks. Remaining streaming soak/performance and M4 brick-diff tasks do not supply a map-screen dependency.

## Consequences

Implement all existing view/presenter/data/navigation requirements and seven specified tests. Actual query registration remains QST-008 integration; no substitute query or hidden fake map. UiComposition owns the screen instances and navigation hooks. The ship-log Show on map route must select the real map target once both views are integrated. No changed simulation contract or owner gate.
