# ADR-0110 — Map control construction and retained transforms

**Status:** Accepted (Tech lead, 2026-10-14).

## Decision

Implement SPEC-61 HUD-015 with the existing MapData and MapViewTransform contracts. MapCanvas exposes View get/set, SetData(MapData, int), Pan(float2), ZoomAt(float2, float), Frame(double2, double2, float paddingPx = 24), and PointClicked(int). Canvas geometry owns ViewSize; setters validate finite positive ordered zoom limits. Buttons and Menu actions use these methods through their owning screen, without a second input owner inside the control.

Keep one retained drawing child and 64 pooled labels. Changes of data reference/version, resolved style, size or zoom beyond the logarithmic 10% threshold invalidate drawing; pan and small zoom only update child transforms and label placement. Rendering callbacks only draw: prepare transforms/labels before generation, as UI Toolkit forbids changing render data during generation. Capture left-pointer gestures, latch a drag at four pixels even if it returns to its origin, and choose the nearest point within 12px with first-index ties.

Use theme tokens exclusively. Explicit unknown colour tokens and tokens beyond the 64-entry cache fall back to maps-color-0. Normal gated lanes use6/4px dashes. Extreme projected lines longer than 40960px use at most4096 proportionally spaced dashes; this prevents unbounded tessellation and non-advancing float loops without clipping geometry that panning may reveal. Non-finite geometry is ignored.

## Consequences

No assembly or simulation-state change. HUD-016 supplies actual map data/screens and button/action bindings. Five functional rendered-panel tests remain mandatory. Two advisory opt-in measurements are authored; pan measures inclusive main-thread frames rather than only command submission, and tessellation uses the actual rendering marker. Measurements are deferred under ADR-0042/0070; no tuning gate blocks the map screens.
