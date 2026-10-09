# ADR-0005 — UI Toolkit with MVVM; no uGUI or TextMeshPro

**Status:** Accepted

## Context
The prototype had 17 UIDocuments, inline styles, overlapping layers and pause/input bugs.

## Decision
All game UI uses UI Toolkit: one `UIDocument` owned by `UIRoot`, layered DOM, screens pushed on `UINavigator`, view models bound with Unity 6 runtime data binding. Styles only through `tokens.uss` variables and component classes. uGUI and TextMeshPro are not installed.

## Consequences
One navigation/pause/input owner. The look can be replaced wholesale when the design system is approved.

## Alternatives considered
uGUI (rejected: second UI stack, world-space canvases already caused bugs).
