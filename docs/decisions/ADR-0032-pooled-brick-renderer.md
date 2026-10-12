# ADR-0032 — Pooled brick renderer after the Entities Graphics spike

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank’s autonomous technical-decision instruction. Performance adoption remains subject to REN-003’s unchanged gates.

## Context
REN-002's real Metal spike renders 2,500 distinct registered brick meshes (578 packed vertices / 512 welded quads each), one shared material and one moving parent. Exact UNorm8 decoding, in-place rewrites and delayed retirement work, but the measured moving presentation delta exceeds SPEC-11's 0.5 ms gate. The ledger Measurements table contains the final timing evidence and settings. The initial single-threaded batch measurements are superseded by explicit multithreaded rendering measurements.

`UploadMeshData(true)` makes subsequent `ApplyAndDisposeWritableMeshData` throw because the destination mesh is not readable. The measured native memory counter also does not decrease. Retain CPU mesh copies for this mutable-mesh path; the reference station already fits its memory gate with copies retained (VOX-003).

## Decision

Implement SPEC-11 §5.10 through REN-003. One shared eight-byte raw vertex buffer and uint index buffer per brick material replace individual registered meshes. Functional entities keep Entities Graphics. This changes architecture §11.3 and its brick single-writer row, not meshing or simulation.

### Allocation, lifetime and memory

- `PageAllocator` owns a native, address-sorted free list. Allocation is best-fit in exact element counts; adjacent frees coalesce. Pages (4,096 vertices / 6,144 indices) are capacity-growth granularity, never a minimum allocation per brick. Larger bricks may span pages.
- The pool retains no CPU geometry copy. Default reservation is 160 vertex pages, 512 index pages and 1,024 brick slots: 655,360 vertices / 3,145,728 indices / 17,825,792 geometry bytes. This holds the measured reference station's 570,496 vertices and 3,025,152 indices without growth. It is a reservation, not a maximum or a changed workload. Tests may request smaller reservations to exercise growth.
- Rewrites reuse ranges when they fit. Replacement and deletion retire old ranges for at least three rendered frames; empty meshes stop drawing and retire geometry. Reused slots carry generation numbers; stale handles cannot update/delete another brick. Slot zero means unallocated.
- Capacity grows by at least 50%, rounded to pages. `BrickPoolCopy.compute` copies old raw-buffer words to larger GPU buffers; old buffers retire after three frames. No synchronous GPU readback or permanent CPU geometry mirror. The configured compute shader is passed explicitly through the content definition/config; no runtime path lookup.
- Counts report active plus retired GPU capacity, all native allocator/slot/matrix/argument/retirement storage and pending geometry where applicable. The 36 MB reference headroom gate is unchanged. REN-003 measures pool storage against the corrected preexisting non-render baseline (32,227,328 minus 21,228,544 old geometry bytes); whole-renderer/ECS/batch integration is still TST-004. No future integration result is presumed.

### Submission and ownership

- `BrickPoolRenderer` is an explicitly owned, disposable managed adapter, one per brick material. Its constructor accepts that material, the copy shader and optional initial capacities. Its bounded mutation API allocates/rewrites/releases a generational handle, updates transforms, reserves capacity and advances retirement. Burst `BrickCullJob` builds indirect commands from local AABBs and world matrices, including off-frustum bricks within shadow distance. Matrix uploads cover changed slots only; argument buffers are reused.
- `Render(Camera, shadowDistance)` takes the camera explicitly and submits one `Graphics.RenderPrimitivesIndexedIndirect` call for the material, with shadows. No `Camera.main`, object search, module back-reference or static singleton. `BrickLitIndirect` pulls packed vertices, uses the installed `UnityIndirect.cginc` base-instance semantics to locate its brick matrix, and calls the REN-008 surface code for all four passes.
- `BrickRenderState` replaces Mesh/MeshId with `BrickPoolHandle Handle`, `PoolRange Vertices, Indices`, existing counts/LOD/applied flags. `GridSpawnSystem` creates brick cleanup state when rendering but no brick MaterialMeshInfo or other Entities Graphics components; local bounds belong to the pool. Functional prototype registration is unchanged. `BrickMeshApplySystem` remains sole owner of brick render content; render submission owns only derived visibility/argument/matrix upload caches.
- REN-011 creates and disposes the pool in `VoxelRenderManagedState`. Cleanup calls pool retirement even outside gameplay, and deletes cleanup components after transferring ownership. World disposal releases every active/retired buffer. Existing mesh retirement remains for functional/proxy meshes; it is not misused for pool ranges.

### Deferred integration (REN-010/011)

These systems do not exist yet (SPEC-11 Q23). Their specs now require pooled output/callers; REN-003 does not install placeholder systems or claim their gates.

- REN-010 stores one owned `UnsafeList<PackedBrickVertex>` and `UnsafeList<uint>` per batch work item (inside a native result array). Each meshing job writes only its own result, after snapshot gathering. The lists are explicit native ownership, not nested NativeContainers; no ECS dependency crosses frames. Disposal completes the batch and releases all outputs. REN-011 uploads completed outputs, disposes their geometry after apply (including dead-brick results), and records pool handles/ranges. No MeshDataArray, scratch Mesh or mesh registration for bricks.
- The render bridge consumes an explicit camera reference published by CameraTargetSyncSystem through a Voxel-owned view contract; it runs after that system, preserving module direction. The new binding and its sole-writer registration land with REN-011, which actually consumes them. REN-003's standalone API/tests pass their camera directly.
- REN-009 binds the indirect brick material and `BrickPoolCopy` compute shader in its definition/config. The obsolete CPU-copy switch remains false for compatibility and has no effect on pooled geometry.
- REN-012 toggles pool visibility for future proxy/LOD use instead of MaterialMeshInfo. REN-013/014 and TST-004 keep their original workload and timing limits.

## Alternatives
- Keep one mutable registered mesh per brick: capability checks pass, measured movement/GPU costs miss the specified gates.
- Shared pooled indirect drawing: the spec's preselected fallback; must independently prove the same gates before adoption.
- Reduce visible bricks or remove shadow cascades: rejected as a way to pass this spike; those changes alter the required workload.

## Acceptance boundary
REN-002 records a no-go and activates REN-003. This accepted contract authorizes implementation, not a claim that the fallback passes. REN-003 must record actual allocation, pixel and timing evidence.
