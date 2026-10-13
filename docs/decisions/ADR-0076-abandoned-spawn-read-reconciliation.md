# ADR-0076 — Reconcile abandoned spawn reads at managed teardown

**Status:** Accepted (Tech lead, 2026-10-13).

## Context

SYS-006 destroys its owned grid entities on system unload and must release owned voxel sources before leaving the World scene. Waiting for the next GridSpawnSystem update can retain read pins after the scene has stopped ticking. Cancelling every registry read would also affect unrelated live requests.

## Decision

Expose VoxelStructureRegistry.ReconcileAbandonedReads(EntityManager) as a managed teardown boundary. It completes tracked jobs and invokes the registry's existing token reconciliation. Only tokens whose bound entity or matching read component no longer exists are released. Active requests keep their pins. The registry remains the single writer of its token table and pin bookkeeping.

Galaxy calls this after destroying its owned entities and on failed request construction. Cleanup-only entities may outlive their source: they contain neither VoxelGrid nor GridSpawnRequest and their collider/render retirement does not borrow that source.

## Consequences

No ECS system is manually advanced, no unrelated read is cancelled, and scene shutdown can synchronously release abandoned sources. Normal GridSpawnSystem reconciliation remains unchanged. No new owner gate.
