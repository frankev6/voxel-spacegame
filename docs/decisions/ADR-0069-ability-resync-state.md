# ADR-0069 — Ability resync state for empty slot lists

**Status:** Accepted (Tech lead, 2026-10-13)

## Problem

SPEC-24 signals resync only on surviving slots. Removing the last granting block leaves no slot to carry that signal, so the effect consumer cannot cancel the old boost. Its warn-once overflow requirement also needs persistent per-root bookkeeping.

## Decision

Keep AbilityState at eight bytes: replace its three reserved bytes with Resynced, OverflowWarned and one Reserved byte. AbilitySystem alone writes these fields. Resynced is cleared each update and set whenever the list changes, including becoming empty or first entering the hulk state. ABL-004 reads the root signal as well as slot flags and cancels obsolete modifiers even when there are no slots. OverflowWarned ensures the eight-slot warning is issued once per root lifetime.

When slots move during resync, pending grants retain the previous slot index because PowerAllocationSystem ran before this resync. Match old slots by AbilityIndex and interpret that tick's grant mask at their old index. No energy or power state is written by AbilitySystem. A removed ability does not activate.

## Consequences

No extra component, allocation, writer, assembly dependency or save format. Root registration stays unchanged. Start recharge when the first charge is spent so the specified 180 ticks do not become 181. Tests cover these cases within ABL-003's nine specified tests; ABL-004 owns effect cancellation.
