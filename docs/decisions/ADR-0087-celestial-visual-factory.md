# ADR-0087 — Loader-owned celestial visual creation

**Status:** Accepted (Tech lead, 2026-10-13).

## Decision

SYS-011 adds `ICelestialVisualFactory` in `Game.Galaxy.Services` with `Unity.Entities.Entity Create(Unity.Entities.Entity anchor, Game.Data.Definitions.BodyVisualDefinition definition, float radiusM)`. Presentation already references Galaxy, so no Data-owned rendering contract or reverse assembly reference is needed. The factory captures its World; the Presentation celestial system implements it and owns registered rendering IDs. App creates/resolves that system and injects it through a final optional loader constructor argument, preserving existing headless/test callers.

The loader calls the factory during the same T1 creation phase, after anchor data is filled and before readiness/entry notification. The factory creates the child render entity and its Parent/transform/render properties and returns it (Entity.Null for unavailable visuals). The loader alone assigns StarSystemBody.Visual and ensures the returned child has StarSystemMember for the loaded system. Definition registration occurs once per definition per World; no render API enters Galaxy.

Every anchor refresh, body removal and unload destroys its child before deleting/replacing the anchor. Failed creation leaves no orphan: the factory cleans its partial entity, and loader error cleanup retires already-linked children. Presentation owns visual LocalTransform; loader owns the Visual link and lifecycle. Initial emission comes from the definition; TRV-009 later owns gate-emission updates. No new assets, packages or assembly references.

## Consequences

This completes the existing SPEC-31 creation/lifetime contract without moving the single writer. Three SYS-011 cases must exercise actual registration, angular-size preservation and grid hiding. Headless callers can omit the factory; production composition supplies it.
