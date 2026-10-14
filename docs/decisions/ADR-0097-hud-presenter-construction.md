# ADR-0097 — HUD presenter construction

**Status:** Proposed (HUD-010, 2026-10-14; lead acceptance pending).

## Context

SPEC-61 assigns three HUD presenters/router to Presentation and their composition to App. Their task and inputs are defined, but constructor/lifetime signatures are not. HUD synchronization and ShipLogQuery runtime completion are separate owners.

## Proposal

Add the task-owned public HudNotificationPresenter(NotificationService, ContentCatalog), HudObjectivePresenter(HudViewModel), and ScreenHotkeyRouter(UINavigator, InputService, Func<string, UIScreen>, optional Action<string>) constructors. Each has Bind/Unbind/Dispose; objective Bind(ShipLogQuery) returns false when its actual runtime owner is unavailable. The router's resolver is supplied by App with its actual registered screen instances (null for unavailable screens).

Use existing InputService action paths and UINavigator's canonical unique screen keys for routing. Do not alter bindings, register screens, change input context directly, or invent gameplay sources. NotificationService remains the coalescing owner; the notification presenter maps actual deferred events and caches item names/keys. The objective presenter subscribes only to the actual ShipLogQuery and refreshes on its Changed event.

## Consequences

App retains production registration, source binding, session reset and disposal order. QST-006 remains the concrete dependency for the tracked-objective integration case; the current explicit stub is reported as unavailable, never substituted with fake query data. Existing public bindings, assembly references, input assets and simulation owners remain unchanged.
