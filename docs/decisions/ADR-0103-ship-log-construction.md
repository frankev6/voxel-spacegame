# ADR-0103 — Ship log construction and narrative owner binding

**Status:** Accepted (Tech lead, 2026-10-14).

## Decision

Keep the parameterless NarrativeLogicAdapter and its existing BindDialog(DialogService) hook. Add BindServices(QuestService, ClueService, BeatService), with atomic null validation, so LogicContext can exist before constructing the services that consume it. Every adapter method forwards to the real bound owner; an unbound route fails explicitly.

Construct ShipLogQuery(ContentCatalog, FactStore, QuestService, ClueService, BeatService) after those owners exist. It implements IDisposable to remove its quest/clue/mystery/fact and internal beat-hook subscriptions. This replaces the unimplemented implicit parameterless query construction. Read-model methods and existing view structs remain unchanged.

An internal BeatService hook flushes query changes in finally from AfterEvaluation, including player-busy/dialog-active paths. QuestService emits an internal read-model invalidation after dependency-driven evaluation; ShipLogQuery compares preallocated current-stage progress caches before notifying. This closes progress-only notification gaps without polling or new public gameplay state writers. ClueService's existing marker-dirty consume hook handles silent restore/reset invalidation.

## Composition and lifetime

QST-008 creates the adapter before LogicContext, binds the actual dialog through BindDialog, constructs QuestService and ClueService, and constructs BeatService after the presenter subscribes to DialogService.Ended. BindServices then attaches all three owners. Construct/register ShipLogQuery for the HUD, ship-log UI, maps and debugger. Register only the actual quest/clue/beat systems with NarrativeService; BeatService forwards the query flush, so there is no extra query system or manual per-frame call.

Dispose ShipLogQuery before disposing BeatService/session composition. Preserve the independent existing dialog hook and the canonical fact/context/catalog instances. QUST and App composition remain with QST-007/008. No package, assembly-reference, shared data-contract or Galaxy dependency changes are introduced.
