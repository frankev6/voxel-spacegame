# ADR-0055 — Own split colliders before their first body-init tick

**Status:** Accepted (Tech lead, 2026-10-12)

## Context

FND-020 can create a physical hulk at gameplay-end and immediately exit the scene. Its provisional collider then never reaches GridBodyInitSystem's next-tick cache adoption. A restart integration run reported one persistent allocation; the ownership gap exists regardless of rendering.

## Decision

Add `GridUnadoptedCollider : ICleanupComponentData { BlobAssetReference<Collider> Value; }`. SplitSystem initializes it alongside the new body's provisional collider. GridBodyInitJob clears Value immediately after the cache adopts the same blob. GridColliderCleanupSystem retires any nonempty unadopted value on a dead grid through DeferredDisposal, then removes the cleanup component through the normal EndSimulation ECB. World shutdown disposes any still-unadopted values after jobs complete. Empty values are bookkeeping only; adopted blobs remain exclusively cache-owned.

DamageStreamsSystem also drains pending channels and clears cross-tick scratch state when gameplay stops. This preserves the channel allocations and pointer identity while preventing a prior session's core explosion or delayed detach from reaching a new session. Stop/teardown may complete dependencies; fixed-tick OnUpdate still cannot.

## Validation

FND-020's restart test immediately exits after damage/splitting, then creates a fresh station. Full PlayMode plus live restart/shutdown must show no stale jobs or native leak report. Existing body-factory creators without the new optional cleanup marker retain their current contract.
