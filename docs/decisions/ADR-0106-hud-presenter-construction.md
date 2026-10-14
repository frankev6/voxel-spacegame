# ADR-0106 — HUD presenter construction

**Status:** Accepted (Tech lead, 2026-10-14).

## Context

SPEC-61 assigns three HUD presenters/router to Presentation and their composition to App. Their task and inputs are defined, but constructor/lifetime signatures are not. HUD synchronization and ShipLogQuery runtime completion are separate owners.

## Decision

Add the task-owned public HudNotificationPresenter(NotificationService, ContentCatalog), HudObjectivePresenter(HudViewModel), and ScreenHotkeyRouter(UINavigator, InputService, Func<string, UIScreen>, optional Action<string>) constructors. Each has Bind/Unbind/Dispose; objective Bind(ShipLogQuery) returns false when composition supplies no registered canonical query. The router's resolver is supplied by App with its actual registered screen instances (null for unavailable screens).

Use existing InputService action paths and UINavigator's canonical unique screen keys for routing. Presenter/router code does not alter bindings, register views, change input context directly, or invent gameplay sources. NotificationService remains the coalescing owner; the notification presenter maps actual deferred events and caches item names/keys. The objective presenter subscribes only to the actual ShipLogQuery and refreshes on its Changed event.

## Consequences

UiComposition owns all three objects. GameComposition calls BindHudPresenters(Input, Content.Catalog) after BindSceneRoots. Notifications and Flight/Menu performed handlers bind once and dispose before UIRoot/InputService. The resolver uses existing navigator registration, refreshes the real FleetPresenter before opening FleetView, and reports unavailable views without popping the current screen. It does not install shell map/log views.

The objective presenter binds automatically if the canonical ShipLogQuery is already registered. QST-008 can call Screens.BindHudObjectiveQuery(query) when registering it later; no per-frame discovery/poll is introduced. Session reset clears the objective and rebinds a registered owner for NewGame/Loaded, while Cleared remains unbound. Notifications and hotkeys retain their single app-lifetime subscription across resets. The required objective case now uses the actual QST-006 service fixture, including progress-only changes, idle identity, completion and unsubscribe; its old explicit Ignore is removed.

Existing public bindings, assembly references, input assets and simulation owners remain unchanged. Production quest acceptance still belongs to QST-008 registration. ADR-0097 remains the independent editor production decision; this HUD construction proposal is ADR-0106.
