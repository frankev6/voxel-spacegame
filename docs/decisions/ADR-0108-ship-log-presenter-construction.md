# ADR-0108 — Ship log presenter construction and map navigation

**Status:** Accepted (HUD-014 integration, 2026-10-14).

## Decision

Construct ShipLogPresenter(ShipLogViewModel, ContentCatalog, optional Func<ContentId, bool> showOnMap, optional Action<string> unavailable). Bind/Unbind/Dispose subscribe only to the canonical ShipLogQuery. Existing IShipLogCommands and all view-model public properties remain unchanged. The presenter pools row models, refreshes on query changes and OnPush through SelectTab, retains selected content identity across refreshes, and delegates tracking/seen writes to the real query.

UiComposition.BindShipLogQuery(query) creates the presenter and registers the actual ShipLogView once. QST-008 calls this after registering its canonical query; no replacement query or blank view is installed. Session reset clears selection/rebinds the registered query for new/load, leaves clear unbound, and keeps ShipLogModel identity. Dispose the presenter before UI/query teardown.

UiComposition.BindShipLogMapNavigation(Func<ContentId, bool>) supplies the later real map navigation. Input is the authored star-system ContentId, not a generated system index. HUD-016 resolves it through StarSystemId.FromAuthored and opens/selects via its real ShowGalaxyMapSelected(StarSystemId) owner. Return false without stack mutation when navigation is unavailable; the ship-log command posts an explicit Info notice and preserves the current view. TODO(HUD-016) remains until that hookup and the prescribed map acceptance pass.

## Validation and limits

Exactly the three specified cases are authored. The selection case uses real fact/context/quest/clue/beat/dialog/query owners with a tiny content fixture; external world/economy boundaries are inert and fail unexpected writes. The layout case uses view-model boundary data for 64 quests/256 clues and checks all logical sizes/tabs with virtualized scrolling. The map case uses actual GameFlowFixture registration/content/navigation and conditionally defers only while QST-008 or HUD-016 registration is absent, so it automatically becomes active after integration. It never substitutes a map or reports opening one from a fake callback. No new assemblies, packages or binding data types.
