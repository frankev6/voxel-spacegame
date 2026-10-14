# ADR-0078 — Read-only navigation layer gates

**Status:** Accepted (Tech lead, 2026-10-13).

## Context

SPEC-40's presentation bridge must distinguish open Screens from Modals. UINavigator exposes only its top screen and total count; neither proves that another layer is absent from the stack.

## Decision

Add UINavigator.HasOpenLayer(UILayer), a read-only, allocation-free scan of the existing navigation stack. No caller receives or mutates the stack. NarrativeTickBridgeSystem uses it for the specified player-free and can-talk gates.

ManagedEventDrainSystem is OrderLast within PresentationBridgeGroup. The narrative bridge is also OrderLast and explicitly UpdateAfter that system; this preserves the required ordering within Unity's ordering partitions.

## Consequences

Navigation remains the single writer. Screens block player-free evaluation; Modals also block talking. The existing NAR-011 tick-bridge acceptance case covers event-drain ordering and gate behavior.
