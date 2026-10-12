# ADR-0049 — Explicit programmatic UI views

**Status:** Accepted (Tech lead, 2026-10-12)

## Context

DEV-015 requires a console built in C# with theme classes and no dev UXML/USS assets. UIRoot currently requires a UIConfig ScreenAsset for every UIScreen, so the console cannot use the canonical navigator without extending that contract. Its spec also uses the older name Screen; SPEC-60's canonical type is UIScreen.

## Decision

- Add `public virtual VisualElement CreateView() => null` to UIScreen. A screen may explicitly provide a programmatic root; the default preserves the existing UXML-only contract.
- UIRoot.BuildScreen first uses a registered ScreenAsset. When none exists, it calls CreateView and rejects null with the existing missing-screen error. It applies the same root name, theme classes, layer, build-once caching and lifecycle to either source.
- DevConsoleScreen overrides CreateView and builds its controls with shared theme classes only. DevRuntimeHost owns the console, subscribes through it to Global/DevConsole, ticks pending command completions, and disposes it before UI teardown. UINavigator remains the only context/pause owner.
- No new assembly references, dev assets, scene listeners or additional UIDocument. Dev console types stay inside the existing Editor/development compilation guards.

## Consequences

Programmatic screens can use existing navigation and input isolation without injecting fabricated content assets. Other screens still fail loudly when their configured view is missing. DEV-015's required tests cover this path through the actual boot UI. Tab completion only offers registered commands; until DEV-009 lands, production completes e.g. newg to newgame, while the specified tel-to-teleport check uses a test-only registry entry.
