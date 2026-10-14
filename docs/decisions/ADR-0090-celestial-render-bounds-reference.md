# ADR-0090 — Celestial render bounds engine reference

**Status:** Accepted (Tech lead, 2026-10-13).

## Context

SYS-011 registers dynamic mesh/material IDs and must supply RenderBounds for its generated render prefab. Unity.Rendering.RenderBounds.Value uses AABB from Unity.Mathematics.Extensions; Game.Presentation did not reference that installed engine assembly.

## Decision

Add Unity.Mathematics.Extensions to Game.Presentation's assembly references so celestial registration can populate authored mesh bounds. No package addition, Game-module dependency, component ownership or public API changes. Retain the specified Presentation/World folder/namespace and qualify existing ECS World type references where the new namespace would shadow them.

## Consequences

Actual bounds remain tied to the authored mesh instead of a guessed constant or missing culling data. Validate with clean compile, the assembly rules check and SYS-011's real graphics registration case. No performance gate or owner decision.
