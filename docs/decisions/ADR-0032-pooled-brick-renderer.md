# ADR-0032 — Pooled brick renderer after the Entities Graphics spike

**Status:** Proposed — 2026-10-11, Tech lead (astra, gpt-6)

## Context
REN-002's real Metal spike renders 2,500 distinct registered brick meshes (578 packed vertices / 512 welded quads each), one shared material and one moving parent. Exact UNorm8 decoding, in-place rewrites and delayed retirement work, but the measured moving presentation delta exceeds SPEC-11's 0.5 ms gate. The ledger Measurements table contains the final timing evidence and settings. The initial single-threaded batch measurements are superseded by explicit multithreaded rendering measurements.

`UploadMeshData(true)` makes subsequent `ApplyAndDisposeWritableMeshData` throw because the destination mesh is not readable. The measured native memory counter also does not decrease. Retain CPU mesh copies for this mutable-mesh path; the reference station already fits its memory gate with copies retained (VOX-003).

## Proposed decision
Implement SPEC-11 §5.10 through REN-003: a shared packed vertex pool and index pool, explicit per-brick allocations, Burst frustum culling and indirect draw commands. Entities Graphics continues to render functional entities. Preserve the eight-byte decoded vertex/shader contract, deterministic meshing, deferred GPU resource retirement and all existing memory/performance gates.

Before accepting and implementing, resolve the exact allocation/ownership contract and update architecture §11.3 plus affected SPEC-10/11 types together. In particular, allocating an entire 4,096-vertex / 6,144-index page independently to every small brick would waste enough memory to violate the reference gate. Pool pages must support packed ranges or an equivalently measured bounded-slack scheme. Account for four-byte indirect indices and allocator capacity, and include the retained CPU/GPU state actually used. This is not permission to relax the 36 MB headroom gate.

## Alternatives
- Keep one mutable registered mesh per brick: capability checks pass, measured movement/GPU costs miss the specified gates.
- Shared pooled indirect drawing: the spec's preselected fallback; must independently prove the same gates before adoption.
- Reduce visible bricks or remove shadow cascades: rejected as a way to pass this spike; those changes alter the required workload.

## Acceptance boundary
REN-002 records a no-go and activates REN-003. This proposed ADR does not itself change runtime contracts or claim the fallback passes. Frank's autonomous technical-decision instruction permits the implementing tech lead to accept the concrete contract once its details are resolved.
