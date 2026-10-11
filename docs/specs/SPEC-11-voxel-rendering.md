# SPEC-11 — Voxel rendering and LOD

| | |
|---|---|
| **Status** | Draft |
| **Area code** | REN (task IDs are `REN-001`, `REN-002`, …) |
| **Module(s)** | `Game.Voxel` (folder `Assets/Game/Voxel/Rendering/`, namespace `Game.Voxel.Rendering`); shaders in `Assets/Game/Voxel/Rendering/Shaders/`; definitions in `Game.Data` (`Assets/Game/Data/Definitions/Voxel/`); placeholder builder in `Game.Editor` (`Assets/Game/Editor/Voxel/`) |
| **Depends on specs** | SPEC-10 (ECS layout, `BrickMeshQueue` producers, `DeferredDisposalSystem`, prototypes, benchmark structures); SPEC-04 (`BlockDef.MaterialLayer`, `Shape`, `Kind`, `PropMeshIndex`, `ContentService.PropMeshes`, `OrientationTablesBlob`); SPEC-62 (writes `VoxelViewer`); SPEC-71 (perf harness); SPEC-80 (final look of the shader contract) |
| **Depended on by** | SPEC-10 (`VoxelRenderRuntime` readiness, queue types), SPEC-13 (`Dormant` read), SPEC-20/21/23 (`Dormant` read), SPEC-51 (editor preview uses the same mesher), SPEC-62 (writes `VoxelViewer`), SPEC-81 (fills the shader contract), SPEC-01 (creates `VoxelRenderConfig`) |
| **Milestone** | M1 (LOD2 in M4) |

> Read [01-architecture.md](../01-architecture.md) (§5.1, §11), [02-conventions.md](../02-conventions.md), [03-glossary.md](../03-glossary.md), [07-implementation-traps.md](../07-implementation-traps.md) §3 and [SPEC-10](SPEC-10-voxel-data-model.md) before this spec. This spec may not contradict them.

## 1. Purpose

This spec turns voxel bricks into pixels at a cost proportional to the number of *changed* bricks, not cells. Each non-empty brick owns one packed 8-byte-vertex culled-face mesh built by a Burst job from an 18³ snapshot, registered once with Entities Graphics and rewritten in place when the brick changes. It defines the mesher (face culling across brick borders, ambient occlusion, vertex welding), the per-frame build/apply budgets and priorities, the brick shader contract the art team fills after SPEC-80, LOD1 and LOD2, the `Dormant` tag, prop block instancing, the shadow policy, and the fallback renderer if Entities Graphics cannot carry thousands of runtime meshes. The old prototype drew every voxel as an entity with 12 triangles including hidden faces (~66 triangles per voxel, O(voxels) per frame; R24, 01 §5.1).

## 2. Scope

**In scope:**
- Packed vertex format, normal/tangent tables, shape tables (Cube, Wedge × 24 orientations).
- Snapshot gathering (18³ `MeshCell`s), LOD0 culled-face mesher with AO and welding, LOD1 downsampled greedy mesher, LOD2 per-grid proxy (M4).
- `BrickMeshBuildSystem`, `BrickMeshApplySystem`, `BrickRenderCleanupSystem`, `GridLodSystem` (LOD and `Dormant`), `PropRenderSystem`.
- Mesh lifetime: create, `RegisterMesh` once, in-place `ApplyAndDisposeWritableMeshData`, explicit bounds, `RenderBounds`, retire through `DeferredDisposalSystem`.
- Brick shader contract (`BrickLit.shader`, decode include, art-owned surface include), prop shader, palette, placeholder art builder, render config.
- Shadow policy, frustum culling via brick bounds, render statistics for the memory gate.
- Time-boxed spike on Entities Graphics with runtime meshes and the fallback renderer.

**Out of scope (and who owns it instead):**
- Which cells exist and when they change: SPEC-10 (spawn) and SPEC-13 (damage). Collider meshes: SPEC-12.
- Debris particles, hit effects, shield visuals: SPEC-63. Editor overlays (ghost, outline): SPEC-51.
- The camera and the `VoxelViewer` values: SPEC-62. Final textures, colours and materials: SPEC-80/81.
- Functional-entity animation (turret rotation): SPEC-21.

## 3. Decisions (binding)

1. **One mesh per brick, registered once.** A brick's `Mesh` is created and registered with `EntitiesGraphicsSystem.RegisterMesh` on its first build, referenced by `BatchMeshID` in `MaterialMeshInfo`, rewritten in place with `Mesh.ApplyAndDisposeWritableMeshData` on every rebuild, and unregistered and destroyed through `DeferredDisposalSystem` when the brick dies (architecture §11.3). No per-entity `RenderMeshArray`.
2. **One shared registered material for all bricks** (`BrickMaterial`), one for props (`PropMaterial`). `BlockDef.MaterialLayer` (0..63) is a `Texture2DArray` slice, not a separate material, so one draw-command family covers every block look. Per-cell variation comes from vertex data (layer, palette colour, damage), never from material instances.
3. **Packed vertex: 8 bytes** — `Position` UNorm8×4 `(x, y, z, 0)` with x,y,z = half-cell coordinates 0..32 inside the brick (ADR-0027), and `TexCoord0` UNorm8×4 `(normalIndex, layer, colour, aoDamageFlags)`. Shaders recover integers with `round(v * 255)` and multiply positions by 0.5 to obtain cell units. Indices are 16-bit (a brick never exceeds 49,152 vertices for 2,048 isolated cubes × 24; the mesher enforces the UInt16 limit for every input). Meshes are in cell units; the brick entity's `LocalTransform.Scale = CellSize`.
4. **Jobs that may outlive the frame read only snapshots.** `BrickMeshBuildSystem` copies each brick plus a 1-cell border from its 26 neighbours into an 18³ `uint` snapshot (23,328 B) owned by the system, then schedules the mesher on the snapshot. Results are applied only after `JobHandle.IsCompleted` (architecture §5.1).
5. **Culled faces, not greedy, at LOD0.** A face is emitted iff the neighbour cell (inside or outside the brick) does not cover it fully. Greedy merging is used only at LOD1/LOD2 because it fights per-cell colour, AO and damage. Coplanar vertices with identical attributes are welded inside each axis slice.
6. **Budgeted, prioritised rebuilds.** At most one mesh batch is in flight; a batch holds 16..128 bricks sized so the main-thread apply stays under 2.0 ms. Priority order: `Damage` > `Neighbour` > `Spawn` > `Lod`, FIFO within a priority (SPEC-10 enqueues; this spec dequeues).
7. **Per-brick LOD by distance with 10% hysteresis:** LOD0 below 400 m, LOD1 above; `Dormant` grids never use LOD0. A LOD switch is a rebuild of the same mesh at the other LOD (queued at `Lod` priority); the old LOD shows until the new one is applied.
8. **Shadows:** all bricks and functional entities use one `RenderFilterSettings` value with `ShadowCastingMode.On` (no per-brick shared values). LOD1 starts beyond the URP shadow distance (asserted at boot), so far geometry never costs shadow passes. LOD2 proxies and debris do not cast shadows.
9. **Shader is hand-written URP HLSL with DOTS instancing.** Code owns decoding (`BrickVertexDecode.hlsl`) and passes (`BrickLit.shader`); art owns one surface function (`BrickSurface.hlsl`) and the material's textures. Swapping the final look never touches C#.
10. **Prop blocks have no entities.** They are drawn with `Graphics.RenderMeshPrimitives`, one call per prop mesh, from GPU buffers rebuilt only when a brick's prop list changes; per-frame cost is O(grids with props).
11. **`Dormant`** (enableable tag on grid roots) is written only by `GridLodSystem` (architecture §8, §11.9) from the distance between the grid and `VoxelViewer.AnchorPosition` (the player ship), with hysteresis.
12. **Profiler markers** `<SystemName>.<Step>` on every expensive step (`BrickMeshBuildSystem.Snapshot`, `BrickMeshApplySystem.Apply`, …).

## 4. Data model

All types are **public contract** unless marked *internal*; folder `Assets/Game/Voxel/Rendering/`, namespace `Game.Voxel.Rendering`.

### 4.1 Packed formats and tables

```csharp
/// 8-byte brick vertex. Lo = Position UNorm8x4 (half-cell x, y, z, 0); Hi = TexCoord0 UNorm8x4 (normal, layer, colour, aoDamageFlags).
public struct PackedBrickVertex
{
    public uint Lo, Hi;
    public static PackedBrickVertex Pack(int x, int y, int z, int normal, int layer, int colour, int ao, int damage, bool scorched)
    {   // ao 0..3 (3 = unoccluded), damage 0..3
        uint flags = (uint)(ao & 3) | ((uint)(damage & 3) << 2) | (scorched ? 16u : 0u);
        return new PackedBrickVertex { Lo = (uint)x | ((uint)y << 8) | ((uint)z << 16),
                                       Hi = (uint)normal | ((uint)layer << 8) | ((uint)colour << 16) | (flags << 24) };
    }
}

/// Snapshot cell, 32 bits.
/// ADR-0018: bits 0-1 class (0 air, 1 structural, 2 other solid), 2-4 shape, 5-9 orient,
/// 10-15 material layer, 16-23 colour, 24-25 damage (3 - (Hp >> 6)), 26 scorched; 27-31 reserved.
/// Full faces are read from BrickShapeTables.FullFaces[shape*24 + orient].
public static class MeshCell
{
    public static uint Encode(int cls, int shape, int orient, int layer, int colour, int damage, bool scorched);
    public static int Class(uint c) => (int)(c & 3);
    public static int Shape(uint c) => (int)((c >> 2) & 7);
    public static int Orient(uint c) => (int)((c >> 5) & 31);
    public static int Layer(uint c) => (int)((c >> 10) & 63);
    public static int Colour(uint c) => (int)((c >> 16) & 255);
    public static int Damage(uint c) => (int)((c >> 24) & 3);
    public static bool Scorched(uint c) => ((c >> 26) & 1) != 0;
    public const int SnapSize = 18, SnapCells = 5832;            // 18³
    public static int SnapIndex(int x, int y, int z) => (x + 1) + 18 * (y + 1) + 324 * (z + 1);   // x,y,z ∈ [-1, 16]
}

/// 42 normals (ADR-0018): 0..5 axis (BlockFace order PosX, NegX, PosY, NegY, PosZ, NegZ);
/// 6..17 edge diagonals: 6 + 4*pair + signs; pair 0 = (0,a,b), 1 = (a,0,b), 2 = (a,b,0); signs bit0 → a < 0, bit1 → b < 0; vectors / √2.
/// 18..41: 18 + 8*pair + 4*swap + signs; pairs/signs as above; swap 0 uses (a,b) magnitudes (1,2), swap 1 uses (2,1); vectors / √5.
/// Tangents: PosX (0,0,-1); NegX (0,0,1); PosY (1,0,0); NegY (1,0,0); PosZ (1,0,0); NegZ (-1,0,0);
/// diagonal pair 0 → (1,0,0), pair 1 → (0,1,0), pair 2 → (0,0,1). Bitangent = cross(normal, tangent). Long slopes use the same zero-axis tangent as their pair. Must equal BrickVertexDecode.hlsl.
public static class VoxelNormals
{
    public const int Count = 42; // ADR-0018: long-slope normals occupy 18..41
    public static float3 Normal(int i); public static float3 Tangent(int i);
    public static int IndexOf(int3 scaledNormal);                // scaledNormal components in {-2,-1,0,1,2} (ADR-0018); -1 if not in the table
}

public struct ShapeFace                                          // 52 bytes, blob element (Q11)
{
    public byte Plane;          // BlockFace of the cell side the polygon lies on, 255 = diagonal (never culled)
    public byte Normal;         // VoxelNormals index
    public byte VertexCount;    // 3 or 4
    public byte IsFull;         // 1 = covers the whole cell side
    public int3 V0, V1, V2, V3; // half-cell coordinates (0/1/2), ADR-0027, ordered so cross(V1-V0, V2-V0)·n > 0 (Unity clockwise front faces)
}

public struct BrickShapeTables                                   // blob, built once by BrickShapeTablesBuilder
{
    public BlobArray<ShapeFace> Faces;
    public BlobArray<ushort> FirstFace;  // [shape*24 + orient]
    public BlobArray<byte> FaceCount;    // [shape*24 + orient]  (Cube 6, Wedge 5, HipCorner 5, LongSlopeHigh 6, LongSlopeLow 5)
    public BlobArray<byte> FullFaces;    // [shape*24 + orient]  world BlockFaceMask of fully covered sides
}
```

Canonical shapes preserve SPEC-04 §4.2 and ADR-0018, using half-cell corner coordinates `[0,2]³` (ADR-0027): Cube is the full cell; Wedge `y+z≤2`; HipCorner `y+z≤2 && x+z≤2`; LongSlopeHigh `y+2z≤4`; LongSlopeLow `y+2z≤2`. The two long-slope pieces meet at a half-height edge and remain independently damageable. Full faces are all six for Cube, NegY/NegZ for Wedge and High, and NegZ for Hip/Low. Every canonical polygon is a triangle or quad. Rotate each vertex as `R(o)·(v−1)+1`, Plane through RotateFace and scaled normal through VoxelNormals.IndexOf(R·n), then reorder vertices to satisfy the winding rule. IndexOf matches the exact signed integer directions in the table; unsupported vectors return -1.

### 4.2 Components and singletons

`AABB` comes from the installed Entities package's `Unity.Mathematics.Extensions` assembly; Game.Voxel references it per ADR-0022.

```csharp
public struct VoxelRenderConfig : IComponentData            // singleton, created by SPEC-01 with VoxelRenderConfigFactory
{
    public UnityObjectRef<Material> BrickMaterial;           // BrickLit material
    public UnityObjectRef<Material> PropMaterial;            // PropInstanced material
    public UnityObjectRef<Texture2D> Palette;                // 256×1 RGBA32, point-filtered, built from VoxelPaletteDefinition
    public NativeArray<UnityObjectRef<Mesh>> PropMeshes;     // ContentService.PropMeshes, index = BlockDef.PropMeshIndex
    public int MeshBatchMin;          // 16
    public int MeshBatchMax;          // 128
    public float MeshApplyBudgetMs;   // 2.0
    public float Lod1Distance;        // 400 m
    public float Lod2Distance;        // 2,000 m (M4)
    public float LodHysteresis;       // 0.1
    public float DormantDistance;     // 3,000 m
    public byte ReleaseCpuMeshCopies; // 1 = Mesh.UploadMeshData(true) after apply (REN-002 decides the default)
}

public struct VoxelRenderRuntime : IComponentData           // singleton; Create() by SPEC-10 GridSpawnSystem; filled by BrickMeshApplySystem
{
    public BatchMaterialID BrickMaterial, PropMaterial;
    public BatchMeshID PlaceholderMesh;                      // 1-triangle mesh used by prototypes
    public UnityObjectRef<Mesh> Placeholder;
    public NativeArray<BatchMeshID> PropMeshIds;             // per PropMeshIndex
    public NativeArray<AABB> PropMeshBounds;                 // per PropMeshIndex, mesh units
    public BlobAssetReference<BrickShapeTables> Shapes;
    public UnityObjectRef<VoxelRenderManagedState> Managed;  // runtime ScriptableObject with reusable managed lists
    public byte IsReady;
    public static VoxelRenderRuntime Create() => default;    // allocation happens at registration
    public void Dispose() { if (PropMeshIds.IsCreated) PropMeshIds.Dispose(); if (PropMeshBounds.IsCreated) PropMeshBounds.Dispose(); if (Shapes.IsCreated) Shapes.Dispose(); }
}

public enum MeshPriority : byte { Damage = 0, Neighbour = 1, Spawn = 2, Lod = 3 }
public struct BrickMeshRequest { public Entity Brick; public Entity Grid; public MeshPriority Priority; }

public struct BrickMeshQueue : IComponentData                // singleton; producers: SPEC-10 systems; consumer: BrickMeshBuildSystem
{
    public NativeList<BrickMeshRequest> Damage, Neighbour, Spawn, Lod;   // FIFO lists
    public NativeArray<int> Heads;                           // 4 read positions
    public NativeHashMap<Entity, byte> Pending;              // brick → lowest priority value queued
    public Game.Data.Streams.GuardedHandles Access;          // main thread only; never passed to a job (SPEC-02 §4.12, SPEC-12 §5.1 pattern)
    public static BrickMeshQueue Create();                   // lists capacity 1,024, map capacity 4,096, Persistent; Access = GuardedHandles.Create()
    public void Dispose();                                   // Access.Dispose() (completes), then dispose every container
    public JobHandle Acquire(JobHandle dependsOn) => Access.Acquire(dependsOn);
    public void Release(JobHandle lastJob) => Access.Release(lastJob);
    public void CompleteAccess() => Access.CompleteAccess();  // never inside FixedStepSimulationSystemGroup
    /// Adds unless the brick is already queued with an equal or better priority; a better priority permanently invalidates
    /// the previous entry (Brick = Entity.Null), adds a new entry and updates Pending. Dequeue skips invalid/stale entries.
    /// This preserves FIFO if a consumed brick is requeued before its old list drains (Q15). Burst-compatible.
    public void Enqueue(Entity brick, Entity grid, MeshPriority p);
    public bool TryDequeue(out BrickMeshRequest r);          // Damage, Neighbour, Spawn, Lod order; skips stale entries; removes from Pending
    public int Count { get; }
}

public struct BrickMeshWork                                  // one brick in the in-flight batch
{
    public Entity Brick; public Entity Grid; public byte Lod;
    public int VertexCount, IndexCount;                      // written by the mesher
    public float3 BoundsMin, BoundsMax;                      // cell units, written by the mesher
    public int PropStart, PropCount;                         // into BrickMeshBatch.Props
}

public struct BrickMeshBatch : IComponentData                // singleton
{
    public NativeList<BrickMeshWork> Work;
    public NativeArray<uint> Snapshots;                      // MeshBatchMax × 5,832, Persistent, reused
    public NativeArray<BrickPropInstance> Props;             // MeshBatchMax × 512, Persistent, reused
    public JobHandle Handle;
    public byte InFlight;
    public uint ScheduledFrame;
    public float AvgApplyMicros;                             // EMA (α = 0.2), initial 15
    public static BrickMeshBatch Create(int maxBricks = 128);
    public void Dispose();
}

public struct BrickRenderState : ICleanupComponentData       // on every brick (added by SPEC-10 after Instantiate)
{
    public UnityObjectRef<Mesh> Mesh;    // default until the first build
    public BatchMeshID MeshId;           // Null until registered
    public int VertexCount, IndexCount;  // of the current content
    public byte ShownLod;                // 0 or 1; 255 = nothing applied yet
    public byte HasMesh;                 // 1 after the first apply
    public ushort Reserved;
}

public struct BrickLodDesired : IComponentData { public byte Lod; }   // written only by GridLodSystem

[InternalBufferCapacity(0)]
public struct BrickPropInstance : IBufferElementData               // on every brick; written only by BrickMeshApplySystem
{ public ushort Block; public ushort LocalIndex; public byte Orient; public byte Color; }   // root cell of a Prop block inside this brick

public struct Dormant : IComponentData, IEnableableComponent { }    // on every grid root; written only by GridLodSystem

public struct VoxelViewer : IComponentData                          // singleton; written by SPEC-62 CameraTargetSyncSystem
{ public float3 CameraPosition; public float3 AnchorPosition; /* player ship */ public byte Valid; }
```

`VoxelRenderManagedState : ScriptableObject` (public type for the public `VoxelRenderRuntime.Managed` field; its fields are internal; runtime instance, `HideFlags.DontSave`): `List<Mesh> ApplyList`, `Mesh Scratch`, `Mesh.MeshDataArray PendingData`, `bool HasPendingData`, `MaterialPropertyBlock[] PropBlocks`, `GraphicsBuffer[] PropInstanceBuffers`, `GraphicsBuffer GridMatrices`, `List<int> PropCounts`. Destroyed by `BrickMeshApplySystem.OnDestroy`.

`BrickRenderStats` (static): `BrickRenderTotals Gather(EntityManager em)` → `{ long GpuBytes; long CpuCopyBytes; int Meshes; long Vertices; long Indices; }` with `GpuBytes = Σ (VertexCount × 8 + IndexCount × 2)` and `CpuCopyBytes = ReleaseCpuMeshCopies ? 0 : GpuBytes`.

### 4.3 Definitions (`Assets/Game/Data/Definitions/Voxel/`, namespace `Game.Data.Definitions`)

```csharp
[ContentIdPrefix("config_")]
public sealed class VoxelRenderConfigDefinition : ContentDefinition   // single instance: config_voxel_render
{
    [SerializeField] private Material brickMaterial;       // BrickLit shader; placeholder until SPEC-81
    [SerializeField] private Material propMaterial;        // PropInstanced shader
    [SerializeField] private VoxelPaletteDefinition palette;
    [SerializeField, Range(16, 128)] private int meshBatchMin = 16;
    [SerializeField, Range(16, 256)] private int meshBatchMax = 128;
    [SerializeField, Min(0.25f)] private float meshApplyBudgetMs = 2.0f;
    [SerializeField, Min(50f)] private float lod1Distance = 400f;
    [SerializeField, Min(100f)] private float lod2Distance = 2000f;
    [SerializeField, Range(0f, 0.5f)] private float lodHysteresis = 0.1f;
    [SerializeField, Min(500f)] private float dormantDistance = 3000f;
    [SerializeField] private bool releaseCpuMeshCopies = true;
    // public getters with the same names (PascalCase)
}

[ContentIdPrefix("config_")]
public sealed class VoxelPaletteDefinition : ContentDefinition         // single instance: config_voxel_palette
{
    [SerializeField] private Color32[] colors = new Color32[256];       // index = PaletteEntry.Color / BlockInstance.Color
    [SerializeField] private string[] names = new string[256];          // editor/UI swatch names; empty = unused slot
    public IReadOnlyList<Color32> Colors => colors; public IReadOnlyList<string> Names => names;
}
```

`VoxelRenderConfigFactory.Create(VoxelRenderConfigDefinition def, IReadOnlyList<Mesh> propMeshes) → VoxelRenderConfig` builds the palette `Texture2D(256, 1, RGBA32, false) { filterMode = Point, wrapMode = Clamp }` and the `PropMeshes` array (`Allocator.Persistent`); `VoxelRenderConfigFactory.Destroy(ref VoxelRenderConfig c)` disposes the array and destroys the texture. Legacy paint colours 0..16 (`Import/Legacy/content/Items/Consumables/Paint/`) are entered in slots 0..16 of the placeholder palette.

### 4.4 Shader contract (`Assets/Game/Voxel/Rendering/Shaders/`)

| File | Owner | Content |
|---|---|---|
| `BrickVertexDecode.hlsl` | code | `BrickVertex DecodeBrickVertex(float4 posRaw, float4 uvRaw)` returning `positionCells` (float3), `normalIndex`, `layer`, `colour`, `ao` (0..1 = ao/3), `damage` (0..3), `scorched`; the 42-entry normal and tangent tables (ADR-0018) (identical to `VoxelNormals`); `float2 BrickFaceUV(float3 positionCells, uint normalIndex, float tileCells)` = `(dot(p, T), dot(p, B)) / tileCells` |
| `BrickLit.shader` | code | URP, `#pragma target 4.5`, `#pragma multi_compile _ DOTS_INSTANCING_ON`, `#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"`; passes `UniversalForward`, `ShadowCaster`, `DepthOnly`, `DepthNormals`; SRP-batcher `UnityPerMaterial` CBUFFER with every property below; vertex stage decodes, transforms with `TransformObjectToWorld` (object space = cell units); fragment builds `BrickSurfaceInput`, calls `BrickSurface`, then `UniversalFragmentPBR` |
| `BrickSurface.hlsl` | **art** (SPEC-81) | `void BrickSurface(BrickSurfaceInput i, inout SurfaceData s)`; placeholder implementation: albedo = `_AlbedoArray[layer].rgb × lerp(1, palette[colour].rgb, _AlbedoArray.a)`; normal from `_NormalArray`; metallic/smoothness/occlusion from `_MaskArray`; emission = `_MaskArray.b × _EmissionColor`; occlusion ×= `ao`; cracks: `damage > 0` → lerp albedo to `_CrackColor` by `_CrackArray[damage-1].r`; scorched → albedo × `_ScorchColor` |
| `PropInstanced.shader` | code | URP lit, procedural instancing `#pragma instancing_options procedural:PropSetup`; reads `StructuredBuffer<PropInstanceGpu> _PropInstances` and `StructuredBuffer<float4x4> _GridMatrices`, global `float3x3 _OrientMatrices[24]`; albedo = `_BaseMap` × palette colour |

Material properties (names are the contract): `_AlbedoArray`, `_NormalArray`, `_MaskArray`, `_CrackArray` (`Texture2DArray`, 3 slices = damage 1..3), `_TileCells` (float, default 1), `_EmissionColor` (HDR colour), `_CrackColor`, `_ScorchColor`; global `_VoxelPalette` (256×1, set once by `BrickMeshApplySystem`), global `_OrientMatrices`. `BrickSurfaceInput` = `{ float3 positionCells; float3 positionWS; float3 normalWS; float3 tangentWS; float3 bitangentWS; float2 uv; uint layer; uint colour; float ao; uint damage; uint scorched; }`.

## 5. Systems, services and flow

### 5.1 Snapshot gathering (`BrickSnapshotJob`, *internal*)

`IJobParallelFor` over the batch work items (inner batch 1), all lookups `[ReadOnly]`: `BrickCells`, `BrickSparseCells`, `BrickSlot`, `VoxelGrid`, `PaletteEntry`, `BlockInstance`, `BlockDefsRef` blob, `BrickShapeTables`. For item i (brick b with coord c in grid g):
1. Resolve the 27 brick buffers (offsets −1..1 per axis) through `BrickSlot` of g; missing → treated as air.
2. For every snapshot position (x,y,z) ∈ [−1,16]³ (5,832): pick the source brick and local index; `cell = BrickWrite.Get(dense, sparse, local)` (ADR-0023; snapshot remains dense); air → 0; else resolve `(Block, Orient, Color)` from the palette entry or instance; `def = Blocks[Block]`; class = `def.Kind == Structural ? 1 : 2`; for class 1: palette shape = `def.Shape`; instance shape = `def.GetShape(footprintIndex)`, where inverse orientation maps `gridCell - instance.Origin` into the unrotated footprint and a canonical Cells lookup finds the index (ADR-0028). A missing footprint match uses the missing-content fallback below. Layer = `def.MaterialLayer`; full-face coverage is looked up from `Shapes.FullFaces[shape*24+orient]` by the mesher (ADR-0018); damage = `3 − (Hp >> 6)`; write `MeshCell.Encode(...)` to `Snapshots[i*5832 + SnapIndex(x,y,z)]`. A palette/instance index out of range → class 1, Cube, layer 63, colour 0 (the "missing" look) and `GameLog.WarnOnce` from the apply system (the job sets a flag).
3. Props (interior only): for each cell that is a palette cell of a Prop block, or an instance cell with `InstanceRoot` whose block is Prop: append `BrickPropInstance{Block, LocalIndex, Orient, Color}` to `Props[i*512 + k]`; more than 512 → stop and set an overflow flag (`GameLog.WarnOnce`).
Cost: ~5,832 cell reads per brick (≈ 15 µs). The job is scheduled on `state.Dependency` and **completed before `BrickMeshBuildSystem.OnUpdate` returns** (VoxelRebuildGroup is not the fixed step; ≤ 128 bricks ≈ 0.3 ms wall on 8 workers). This is the only point that touches live ECS cell memory.

### 5.2 LOD0 mesher (`BrickMesher.MeshLod0`, Burst, *internal*)

Input: one 18³ snapshot, `BrickShapeTables`; output: vertices (`PackedBrickVertex`), 16-bit indices, bounds. Scratch is `Allocator.Temp`.
1. **Row masks.** For each of the 18×18 rows (y,z ∈ [−1,16]) build 18-bit masks: `solid` (class ≠ 0), `structural` (class 1), and `full[f]` for the 6 faces (`Shapes.FullFaces[shape*24+orient]` bit f, ADR-0018). O(5,832).
2. **Axis faces**, for each direction `d` (PosX, NegX, PosY, NegY, PosZ, NegZ) and each slice s = 0..15 along d's axis:
   - For each row inside the slice: `candidates` = structural cells (bits 1..16 of the row) whose shape has a polygon with `Plane == d` (Cube always; Wedge per table — precompute per row a mask `hasPlane[d]` from the cells' (shape, orient)); `hidden` = cells whose neighbour across d has `full[opposite(d)]`; for ±X the neighbour bit is the row shifted by 1; for ±Y/±Z it is the adjacent row. `exposed = candidates & ~hidden`.
   - Iterate set bits of `exposed` (`math.tzcnt`). For the cell's polygon on plane d: compute per-vertex AO: for the corner with tangent signs (su, sv) on the face's two in-plane axes: `s1 = solid(cell + n + su·u)`, `s2 = solid(cell + n + sv·v)`, `cr = solid(cell + n + su·u + sv·v)`; `ao = (s1 && s2) ? 0 : 3 − (s1 + s2 + cr)`.
   - **Welding:** a 33×33 half-cell table per (d, s) plane, reset by a stamp counter, stores `(packed vertex, index)` per corner position; a vertex with identical packed value at the same corner reuses the index, otherwise a new vertex is appended and stored.
   - **Indices:** quads emit two triangles; if `ao0 + ao2 < ao1 + ao3` use diagonal (v1, v3): `(v0,v1,v3),(v1,v2,v3)`, else `(v0,v1,v2),(v0,v2,v3)`; triangles emit `(v0,v1,v2)`. Vertex order comes from `ShapeFace` (clockwise front faces).
3. **Diagonal faces** (all sloped polygons, `Plane == 255`), cell order z,y,x: emitted without welding, AO = 3, never culled.
4. Pack vertices in half-cell units (`cell*2 + ShapeFace vertex`); bounds = min/max of emitted coordinates multiplied by 0.5 (cell units). Fractional face-vertex AO takes the minimum of its enclosing integer-corner AO levels (ADR-0027); whole-cell corners use the formula above. Zero vertices → empty result.
Complexity: O(5,832 + exposed faces). Typical surface brick (≤ 1,024 faces): ≤ 60 µs (gate). The isolated-cube checkerboard has 49,152 vertices; enforce the UInt16 index limit for all inputs (ADR-0027).

### 5.3 LOD1 mesher (`BrickMesher.MeshLod1`, Burst, *internal*)

1. Downsample the 16³ interior to 8³ super-cells of 2×2×2: solid if ≥ 4 of 8 children have class ≠ 0; attribute key = most frequent `(layer << 8 | colour)` among class-1 children (tie → smallest key); a solid super-cell with no class-1 child is skipped (functional entities draw themselves).
2. Border neighbours (for culling only): the neighbour super-cell across a brick face counts as solid if ≥ 2 of the 4 border snapshot cells facing the super-cell are solid. Diagonal neighbours are ignored.
3. For each direction and each of the 8 slices: build an 8×8 grid of keys for exposed faces (0 = none); greedy-merge rectangles of equal key (grow along u, then v); emit one quad per rectangle with super-cell corner coordinates ×4 for packing (half-cell units, 0..32; bounds remain cell units), AO = 3, damage 0, normal index = d.
4. Bounds as in LOD0.
Gate: LOD1 triangles ≤ 25% of LOD0 for the same brick set (measured on `HollowStation1M`, `Ship20k` and 200 random-noise bricks).

### 5.4 `BrickMeshBuildSystem`

- **Group:** `VoxelRebuildGroup`, first (`[UpdateBefore(typeof(BrickColliderBuildSystem))]`). `ISystem`; `OnUpdate` not Burst-compiled (calls `Mesh.AllocateWritableMeshData`); jobs are Burst. Requires `GameplayActive`, `VoxelRenderRuntime`, `BrickMeshQueue`, `BrickMeshBatch`.
- **Reads:** queue, `BrickLodDesired`, `Dormant`, cell data (snapshot job only). **Writes:** `BrickMeshQueue` (dequeue), `BrickMeshBatch`.
- Per frame:
  1. `VoxelRenderRuntime.IsReady == 0` or `BrickMeshBatch.InFlight == 1` → return.
  2. `n = clamp((int)(MeshApplyBudgetMs * 1000 / AvgApplyMicros), MeshBatchMin, MeshBatchMax)`.
  3. `queue.CompleteAccess()` (main thread pattern, SPEC-12 §5.1), then dequeue up to n requests (marker `Dequeue`). Skip a request whose brick no longer exists or lacks `Brick`. `Lod` = `BrickLodDesired.Lod`, forced to ≥ 1 when the grid has `Dormant` enabled. Fill `Work`.
  4. No items → return.
  5. Schedule `BrickSnapshotJob` (§5.1) on `state.Dependency`, complete it (marker `Snapshot`).
  6. `data = Mesh.AllocateWritableMeshData(Work.Length)`; store in the managed state (`PendingData`, `HasPendingData = true`).
  7. Schedule `BrickMeshJob : IJobParallelFor` (inner batch 1) over the items, **not** chained to `state.Dependency` (it reads only `Snapshots`, the shape blob and its own `Work` slot): runs `MeshLod0` or `MeshLod1` into Temp lists, then on `data[i]`: `SetVertexBufferParams(vertexCount, layout)` with the two UNorm8×4 attributes (layout `NativeArray<VertexAttributeDescriptor>` held by the managed state, created once), `SetIndexBufferParams(indexCount, IndexFormat.UInt16)`, copy, `subMeshCount = 1`, `SetSubMesh(0, new SubMeshDescriptor(0, indexCount) { bounds, firstVertex = 0, vertexCount }, DontRecalculateBounds | DontValidateIndices | DontNotifyMeshUsers)`; writes counts and bounds to `Work[i]`.
  8. `Handle = job handle; InFlight = 1; ScheduledFrame = frame`.
- Per-frame cost when idle: one queue `Count` check (< 5 µs).

### 5.5 `BrickMeshApplySystem`

- **Group:** `PresentationBridgeGroup`, first. `ISystem`; `OnUpdate` not Burst-compiled (Mesh and Entities Graphics APIs). The only writer of brick `MaterialMeshInfo`, `RenderBounds`, `BrickRenderState`, `BrickPropInstance`.
- **First update (registration, marker `Register`):** `egs = World.GetExistingSystemManaged<EntitiesGraphicsSystem>()`; `BrickMaterial = egs.RegisterMaterial(config.BrickMaterial)`, `PropMaterial = egs.RegisterMaterial(config.PropMaterial)`; create the placeholder mesh (1 degenerate triangle with the brick layout) and register it; register every prop mesh (`PropMeshIds`, `PropMeshBounds` from `mesh.bounds`); build `Shapes` with `BrickShapeTablesBuilder.Build(ref OrientationTablesRef.Value.Value)`; create the managed state; `Shader.SetGlobalTexture("_VoxelPalette", config.Palette)`; `Shader.SetGlobalMatrixArray`-equivalent upload of `_OrientMatrices` (24 float3x3 packed as float4x4); warn once if `Lod1Distance < UniversalRenderPipelineAsset.shadowDistance`; `IsReady = 1`.
- **Per frame:**
  1. `InFlight == 0` → go to step 6. If `!Handle.IsCompleted` and `frame − ScheduledFrame < 4` → go to step 6. Else `Handle.Complete()` (non-blocking when completed; after 4 frames it bounds the wait).
  2. Main-thread loop over `Work` (marker `Apply`, measured with a `Stopwatch` for `AvgApplyMicros`): brick alive and `HasMesh == 0` → `mesh = new Mesh { name = "Brick", hideFlags = HideAndDontSave }`, `id = egs.RegisterMesh(mesh)`, remember both; alive → `ApplyList.Add(brick mesh)`; dead → `ApplyList.Add(Scratch)`.
  3. `Mesh.ApplyAndDisposeWritableMeshData(PendingData, ApplyList, DontRecalculateBounds | DontValidateIndices | DontNotifyMeshUsers | DontResetBoneBounds)`; `HasPendingData = false`.
  4. For each alive item: `mesh.bounds = new Bounds((min+max)/2, max−min)`; if `ReleaseCpuMeshCopies == 1` → `mesh.UploadMeshData(true)`.
  5. Schedule and complete `ApplyResultsJob : IJobParallelFor` (writes through lookups; `[NativeDisableParallelForRestriction]` justified: each work item is a different brick, guaranteed by `BrickMeshQueue.Pending`): `BrickRenderState{Mesh, MeshId, VertexCount, IndexCount, ShownLod = Lod, HasMesh = 1}`; `MaterialMeshInfo = new(BrickMaterial, MeshId)` and its enable bit = `VertexCount > 0`; `RenderBounds = AABB(bounds)`; replace `BrickPropInstance` contents with `Props[PropStart..PropStart+PropCount)`. `InFlight = 0`; `ApplyList.Clear()`; update `AvgApplyMicros`.
  6. Nothing else per frame.
- Complexity O(batch); 0 GC after warm-up except `new Mesh` on a brick's first build.

### 5.6 `BrickRenderCleanupSystem`

- **Group:** `PresentationSystemGroup`, `[UpdateBefore(typeof(DeferredDisposalSystem))]`. Burst `ISystem`. Requires `DeferredDisposalQueue` only — **not** `GameplayActive` (SPEC-01 decision 5: bricks destroyed by scene teardown are cleaned up in the frames after it).
- Query: `BrickRenderState` without `Brick` (destroyed bricks keep only cleanup components). Job (`Schedule`, single worker) appends `RetireMesh(Mesh, MeshId)` to `DeferredDisposalQueue` for entries with `HasMesh == 1`; then `BeginInitializationEntityCommandBufferSystem` ECB `RemoveComponent<BrickRenderState>(query)` (query-wide, one command).
- Cost O(destroyed bricks).

### 5.7 `GridLodSystem`

- **Group:** `WorldGroup`, last. Burst `ISystem`. Requires `GameplayActive`, `VoxelViewer`, `VoxelRenderConfig`.
- **Writes:** `Dormant` enable bits (sole writer), `BrickLodDesired` (sole writer); enqueues `Lod` requests.
- Per frame (markers `Grids`, `Bricks`, `Enqueue`):
  1. `VoxelViewer.Valid == 0` → all grids non-dormant and all bricks LOD0 desired (only writes values that change); return.
  2. `GridDormancyJob : IJobEntity` (`ScheduleParallel`) over roots: bounding sphere of the grid (centre = `LocalToWorld` of the `CellMin..CellMax` centre × CellSize, radius = half diagonal × CellSize); `d = max(0, |centre − AnchorPosition| − radius)`; currently dormant → stays dormant while `d > DormantDistance × (1 − LodHysteresis)`; currently active → becomes dormant when `d > DormantDistance`.
  3. `BrickLodJob : IJobEntity` (`ScheduleParallel`) over bricks with `BrickRenderState`: sphere centre = `math.transform(LocalToWorld.Value, 8)`, radius = `8√3 × scale` (scale = length of LTW column 0); `d = max(0, |centre − CameraPosition| − radius)`; current desired 0 → 1 when `d > Lod1Distance`; 1 → 0 when `d < Lod1Distance × (1 − LodHysteresis)`; grid dormant → 1. Write only on change.
  4. `LodEnqueueJob : IJobChunk` (`Schedule`, change filter on `BrickLodDesired`): `HasMesh == 1` and `Desired != ShownLod` → `BrickMeshQueue.Enqueue(brick, grid, Lod)`. Job access per SPEC-12 §5.1: `q = SystemAPI.GetSingleton<BrickMeshQueue>()` (read-only), schedule on `q.Acquire(state.Dependency)`, then `q.Release(handle)`.
- Complexity O(bricks) per frame in parallel jobs (2,500 bricks ≈ 20 µs worker time); 0 main-thread loops.

### 5.8 `PropRenderSystem`

- **Group:** `PresentationBridgeGroup`, `[UpdateAfter(typeof(BrickMeshApplySystem))]`. `ISystem`; `OnUpdate` not Burst-compiled (GraphicsBuffer and `Graphics` calls).
- Per frame:
  1. If the change-filtered query over `BrickPropInstance` reports a change, or a grid with props was created or destroyed: `PropGatherJob` (Burst, single job) collects every brick's prop list into per-prop-mesh lists of `PropInstanceGpu { float3 LocalCellCenter; uint Packed; /* orient 5 bits | colour 8 bits << 5 | gridSlot 16 bits << 13 */ }` (local = brick coord × 16 + local + 0.5, cell units), assigns a dense `gridSlot` per root, and per-mesh world bounds; upload with `GraphicsBuffer.SetData` (buffers grow ×2 when needed, old ones released).
  2. Every frame: `GridMatrixJob` writes `LocalToWorld × scale(CellSize)` per grid slot; upload `_GridMatrices` (O(grids with props)).
  3. For each prop mesh with count > 0: `Graphics.RenderMeshPrimitives(new RenderParams(PropMaterial) { matProps = PropBlocks[m], worldBounds = bounds[m], shadowCastingMode = On, receiveShadows = true }, mesh, 0, count)`.
- Budget ≤ 0.3 ms main thread per frame for 10,000 props; ≤ 1 ms on a rebuild frame.

### 5.9 LOD2 (M4, `GridProxySystem`)

- **Group:** `WorldGroup`, `[UpdateAfter(typeof(GridLodSystem))]`. For grids whose bounding-sphere distance to the camera exceeds `Lod2Distance` (hysteresis 10%): a proxy entity (child of the root, created by `GridSpawnSystem` at spawn with `MaterialMeshInfo` disabled) gets a mesh built from 4×-downsampled brick masks (4³ super-cells per brick, solid if ≥ 32 of 64 cells, greedy merge over the whole grid, AO 3, layer and colour of the grid's most common palette entry); the proxy's `MaterialMeshInfo` is enabled and every brick's is disabled (job). Proxy rebuild when `VoxelGrid.Revision` changed and the grid is at LOD2, at most 1 proxy rebuild per frame, job on a mask snapshot. Proxy `RenderFilterSettings` has `ShadowCastingMode.Off` (a second shared value; cardinality 2).

### 5.10 Fallback renderer (only if REN-002 is no-go; needs an ADR)

`BrickPoolRenderer` replaces registered meshes:
1. One vertex pool (`GraphicsBuffer`, `Target.Raw`, 8-byte vertices) and one index pool (`Target.Index`, 32-bit) per material, paged in 4,096-vertex / 6,144-index pages with a native free-list allocator; `BrickRenderState` stores page runs instead of `Mesh/MeshId`.
2. The mesher writes into `NativeArray`s instead of `MeshDataArray`; apply uploads with `GraphicsBuffer.SetData(array, 0, dstStart, count)` (no CPU copy is kept).
3. Per frame, a Burst culling job tests each brick's world AABB against the camera frustum (6 planes) and adds every brick within the shadow distance for shadow casting; it writes one `GraphicsBuffer.IndirectDrawIndexedArgs` per visible brick (`indexCountPerInstance`, `instanceCount = 1`, `startIndex`, `baseVertexIndex`, `startInstance = brick slot`) and a per-brick `float4x4` buffer updated only for bricks whose `LocalToWorld` changed.
4. One `Graphics.RenderPrimitivesIndexedIndirect(rp, MeshTopology.Triangles, indexPool, args, commandCount)` per material with shadows on; shader `BrickLitIndirect.shader` uses `UnityIndirect.cginc` (`InitIndirectDrawArgs`, `GetIndirectInstanceID`) and vertex pulling from the raw pool by `SV_VertexID`; it includes the same `BrickVertexDecode.hlsl` and `BrickSurface.hlsl`.
5. Entities Graphics still draws functional entities; bricks lose `MaterialMeshInfo`.

## 6. Public API

| Call / type | Pre | Post / errors |
|---|---|---|
| `BrickMeshQueue.Enqueue(brick, grid, p)` | SPEC-12 §5.1 access: inside a job scheduled with `Acquire`/`Release`, or on the main thread after `CompleteAccess()`; never through `GetSingletonRW` | Queued unless already queued with equal or better priority |
| `VoxelRenderRuntime.IsReady` | — | 1 after `BrickMeshApplySystem`'s first update; SPEC-10 spawn waits for it |
| `Dormant` | — | Enabled ⇔ grid farther than `DormantDistance` from `VoxelViewer.AnchorPosition` (hysteresis); readers skip expensive work |
| `BrickRenderStats.Gather(em)` | Main thread | Mesh memory totals for the memory gate |
| `BrickMesher.MeshLod0/MeshLod1(NativeArray<uint> snapshot, ref BrickShapeTables, NativeList<PackedBrickVertex>, NativeList<ushort>, out float3 min, out float3 max)` | Snapshot of 5,832 cells | Pure; used by SPEC-51 for editor previews of unspawned structures |
| `BrickSnapshotUtility.FromStructure(VoxelStructureData, int3 brick, ref BlockDefsBlob, ref BrickShapeTables, NativeArray<uint> out)` | Brick exists | Same snapshot as §5.1 from a `VoxelStructure` (editor, tests) |

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| Brick destroyed while its build is in flight | Its data is applied to the scratch mesh; nothing else happens; cleanup retires its mesh if it had one |
| Brick re-dirtied while in flight | It was removed from `Pending` at dequeue, so it is queued again and rebuilt after this batch |
| Mesh result with 0 vertices (fully enclosed brick) | Mesh keeps its registration; `MaterialMeshInfo` disabled |
| Neighbour brick missing (grid edge or not yet spawned) | Treated as air: border faces are emitted; when it spawns, SPEC-10 enqueues it, and its first build marks nothing — the edge brick keeps faces until it is next rebuilt. `GridSpawnSystem` enqueues spawned bricks in brick-record order, so most neighbours exist before their meshes build; remaining hidden-face overdraw is accepted |
| Diagonal-neighbour change (edge/corner of a brick) | AO of the diagonal brick is not rebuilt until it is next dirtied (accepted cosmetic error) |
| Wedge faces next to wedge faces | Partial faces are culled only by full neighbour faces; coincident back-to-back triangles are emitted (back-face culled) |
| `VoxelViewer.Valid == 0` (menus, first frame) | Everything LOD0, nothing dormant |
| Batch not complete after 4 frames | `Complete()` in `BrickMeshApplySystem` (presentation; bounded) |
| More than 512 props in one brick | Extra props not drawn; `GameLog.WarnOnce("Voxel", "prop overflow")` |
| Palette index or block index out of range in a cell | Drawn with layer 63 colour 0; `GameLog.WarnOnce` |
| `Lod1Distance` below the URP shadow distance | `GameLog.Warn` once at registration; still runs |
| Domain reload disabled, second Play | Runtime singleton disposed in `GridSpawnSystem.OnDestroy`; managed state destroyed in `BrickMeshApplySystem.OnDestroy`; meshes destroyed through the disposal queue or scene teardown |

## 8. Performance budget

| Gate | Target | Test (task) |
|---|---|---|
| Remesh one dirty brick (Burst job) | ≤ 60 µs | `BrickMesherPerfTests.Lod0_TypicalDamagedHullBrick_Under60us` (REN-006): brick inside a 16×16×4 slab with a radius-3 crater at the top centre, slab continuing into all side neighbours; median of 1,000 single-threaded runs, safety off |
| Triangles | ≤ 2 per exposed face at LOD0; LOD1 ≤ 25% of LOD0 | `BrickMesherTests.Lod0_TwoTrianglesPerExposedFace`, `BrickLod1Tests.TriangleRatio_AtMost25Percent` |
| Ship moving, 2,500 bricks | ≤ 0.5 ms main thread (TransformSystemGroup + Entities Graphics delta vs static) | `BrickRenderPerfTests.MovingLattice2500_TransformUnder0_5ms` (REN-013) |
| Share of idle 1M station (2 ms total, SPEC-10) | ≤ 1.2 ms for Entities Graphics + all REN systems | `BrickRenderPerfTests.IdleStation1M_RenderShare` (REN-013) |
| Spawn 1M: meshing part | 872 bricks meshed in ≤ 10 frames at 128 bricks/batch; apply ≤ 2.5 ms per frame | measured in SPEC-71 TST-004 |

Own budgets: `BrickMeshApplySystem` ≤ 2.5 ms main thread for a 128-brick batch; `GridLodSystem` ≤ 0.1 ms main thread (scheduling) for 5,000 bricks; `BrickMeshBuildSystem` ≤ 0.5 ms main thread including the completed snapshot job for 128 bricks; mesh memory per §8.2 of SPEC-10 (≈ 21.6 B per exposed welded face).

## 9. Tasks

### REN-001 — Public contract types
- **Milestone:** M1
- **Tier:** standard — transcribes the exact code in §4 (including the given `Create`/`Dispose` bodies)
- **Depends on:** `VOX-001`, `DATA-001`
- **Do:** Create every type of §4.1–§4.3 with exact fields and signatures; method bodies `throw new System.NotImplementedException()` except those given in §4 and the `MeshCell` accessors; `BrickMeshQueue.Create/Dispose` and `BrickMeshBatch.Create/Dispose` allocate/dispose exactly the containers listed (Persistent). Create the folder `Assets/Game/Voxel/Rendering/Shaders/`.
- **Files:** `Assets/Game/Voxel/Rendering/{PackedBrickVertex,MeshCell,VoxelNormals,ShapeFace,BrickShapeTables,BrickShapeTablesBuilder,VoxelRenderConfig,VoxelRenderConfigFactory,VoxelRenderRuntime,VoxelRenderManagedState,MeshPriority,BrickMeshRequest,BrickMeshQueue,BrickMeshWork,BrickMeshBatch,BrickRenderState,BrickLodDesired,BrickPropInstance,Dormant,VoxelViewer,BrickRenderStats,BrickRenderTotals,PropInstanceGpu,BrickMesher,BrickSnapshotUtility}.cs`; `Assets/Game/Data/Definitions/Voxel/{VoxelRenderConfigDefinition,VoxelPaletteDefinition}.cs`
- **Acceptance criteria:**
  - [ ] `tools/compile.sh` passes with zero warnings.
  - [ ] `SizeOf<PackedBrickVertex>() == 8`, `SizeOf<ShapeFace>() == 52`, `SizeOf<BrickPropInstance>() == 6`.
  - [ ] `BrickPropInstance` has `[InternalBufferCapacity(0)]`; `Dormant` is enableable.
- **Tests:** `RenderContractTests.StructSizes_MatchSpec`, `.QueueCreateDispose_NoLeaks`.

### REN-002 — SPIKE: Entities Graphics with thousands of runtime brick meshes
- **Milestone:** M1
- **Tier:** frontier — F6: go/no-go for the rendering architecture
- **Depends on:** `REN-001`, `SPEC-10`
- **Do:** Time-box 3 days; throwaway code in `Assets/Game/Tests/PlayMode/Voxel/Spikes/`. Build a PlayMode spike scene (1920×1080, URP with 4 shadow cascades over 300 m, safety checks off) and measure with `ProfilerRecorder` (median/p95 of 300 frames):
  - (a) 2,500 child entities of one root, each with its own registered `Mesh` (~512 welded quads in the reference-station pattern, 8-byte vertex layout), one registered material, instantiated from one `RenderMeshUtility` prototype: idle main-thread time of `EntitiesGraphicsSystem` + presentation, render thread, GPU, draw calls.
  - (b) The same with the root moving every frame: TransformSystemGroup + Entities Graphics main-thread delta.
  - (c) 128 meshes per frame rewritten in place (`AllocateWritableMeshData` + Burst fill + `ApplyAndDisposeWritableMeshData` with `DontNotifyMeshUsers`, explicit bounds, `RenderBounds` update), without re-registering: verify on-screen change (pixel readback of a test brick) and main-thread apply time.
  - (d) `Position` UNorm8×4 + `TexCoord0` UNorm8×4 accepted by `SetVertexBufferParams` on Metal (and DX12/Vulkan if available) and decoded exactly by a minimal `BrickLit` (pixel test of colours per layer/colour index).
  - (e) `UploadMeshData(true)` after apply: CPU memory drop (`Profiler.GetRuntimeMemorySizeLong`) and whether a later in-place apply to the non-readable mesh still works.
  - (f) 10,000 in-place rewrites plus 2,000 unregister/destroy cycles through a 3-frame delay: zero errors, zero leaks.
  - (g) `hull_1x1` placeholder mesh bounds ≈ 1 unit (SPEC-10 OQ 10).
  Record results in the ledger *Measurements* and set `ReleaseCpuMeshCopies` default from (e).
- **Files:** `Assets/Game/Tests/PlayMode/Voxel/Spikes/EntitiesGraphicsSpike.cs` (temporary); `docs/LEDGER.md`
- **Acceptance criteria:**
  - [ ] **Go** if: (a) main thread ≤ 0.6 ms, render thread ≤ 6 ms, GPU ≤ 6 ms; (b) ≤ 0.5 ms; (c) visible change and ≤ 2.5 ms for 128; (d) exact decode; (f) clean. Otherwise **no-go**: REN-003 becomes `todo`, an ADR draft for the fallback renderer is written.
  - [ ] If (d) fails: switch to `Position` Float16×4 (x, y, z, packed normal/ao/damage as an integer-valued half) + `TexCoord0` UNorm8×4 (12 B), update §3.3 and SPEC-10 §8.2 numbers.
- **Tests:** `EntitiesGraphicsSpike.Idle2500`, `.Moving2500`, `.InPlaceRewrite128`, `.PackedFormatDecode`, `.CpuCopyRelease`, `.Churn10000` (`[Explicit]`).

### REN-003 — Fallback renderer (only if REN-002 is no-go)
- **Milestone:** M1
- **Tier:** frontier — F2/F4: GPU buffer pools, indirect drawing, budgets
- **Depends on:** `REN-002`
- **Do:** If REN-002 records **go**, set `dropped` with the measurement reference. If **no-go**, after the ADR is accepted implement §5.10 and switch `BrickMeshApplySystem`/`BrickRenderCleanupSystem` to it; update §3 decisions 1–3 in the same commit.
- **Files:** `Assets/Game/Voxel/Rendering/{BrickPoolRenderer,PageAllocator,BrickCullJob}.cs`; `Assets/Game/Voxel/Rendering/Shaders/BrickLitIndirect.shader`; `Assets/Game/Tests/PlayMode/Voxel/BrickPoolRendererTests.cs`
- **Acceptance criteria:**
  - [ ] All REN tests pass on the fallback; REN-002 (a)–(c) targets met.
- **Tests:** `BrickPoolRendererTests.PageAllocator_NoOverlap_Fuzz`, `.Render_MatchesReferenceScreenshot`, `.Idle2500_UnderBudget`.

### REN-004 — Normals, packing and shape tables
- **Milestone:** M1
- **Tier:** frontier — F3: a table error silently corrupts every mesh
- **Depends on:** `REN-001`, `SPEC-04`
- **Do:** Implement `PackedBrickVertex.Pack`, `MeshCell` encode/decode, `VoxelNormals` (§4.1 order), `BrickShapeTablesBuilder.Build(ref OrientationTablesBlob)` (all five canonical ADR-0018 shapes, 24 orientations, winding re-order, `FullFaces`), and `BrickVertexDecode.hlsl` tables.
- **Files:** `Assets/Game/Voxel/Rendering/{PackedBrickVertex,MeshCell,VoxelNormals,BrickShapeTablesBuilder}.cs`; `Assets/Game/Voxel/Rendering/Shaders/BrickVertexDecode.hlsl`; `Assets/Game/Tests/EditMode/Voxel/Rendering/{ShapeTablesTests,PackingTests}.cs`
- **Acceptance criteria:**
  - [x] For all 120 (shape, orient): every polygon satisfies `cross(v1−v0, v2−v0)·n > 0`, its vertices lie on its plane, and `IsFull` faces cover the whole side; the union of polygons is a closed surface (each edge shared by exactly two polygons).
  - [x] FullFaces equals the rotated canonical mask for every shape: Cube all six, Wedge/High NegY and NegZ, Hip/Low NegZ.
  - [x] Pack/unpack round-trips every field; C# normals/tangents equal the HLSL tables (parsed from the `.hlsl` text).
- **Tests:** `ShapeTablesTests.Winding_AllShapesAllOrients`, `.ClosedSurface`, `.FullFaces_MatchRotation`; `PackingTests.Vertex_RoundTrip`, `.MeshCell_RoundTrip`, `.Normals_MatchHlsl`.

### REN-005 — Snapshot gathering
- **Milestone:** M1
- **Tier:** frontier — F2/F3: the only reader of live cell memory for meshing; cross-brick borders
- **Depends on:** `REN-004`, `SPEC-10`
- **Do:** Implement `BrickSnapshotJob` (§5.1) and `BrickSnapshotUtility.FromStructure`.
- **Files:** `Assets/Game/Voxel/Rendering/{BrickSnapshotJob,BrickSnapshotUtility}.cs`; `Assets/Game/Tests/EditMode/Voxel/Rendering/SnapshotTests.cs`
- **Acceptance criteria:**
  - [ ] ECS snapshot equals the structure snapshot for every brick of `Ship20k` and a random 3×3×3-brick structure (all 26 neighbour directions populated).
  - [ ] Missing neighbours read as air; out-of-range refs produce the missing look and set the flag.
  - [ ] Prop lists contain exactly the prop roots inside the brick.
- **Tests:** `SnapshotTests.EcsEqualsStructure`, `.MissingNeighbours_Air`, `.BadRef_MissingLook`, `.Props_Collected`.

### REN-006 — LOD0 mesher
- **Milestone:** M1
- **Tier:** frontier — F3/F4: face culling across borders, welding, owns the 60 µs gate
- **Depends on:** `REN-004`
- **Do:** Implement `BrickMesher.MeshLod0` per §5.2 with row masks, AO, slice welding, AO-based diagonal flip.
- **Files:** `Assets/Game/Voxel/Rendering/BrickMesher.cs`; `Assets/Game/Tests/EditMode/Voxel/Rendering/BrickMesherTests.cs`; `Assets/Game/Tests/PlayMode/Voxel/BrickMesherPerfTests.cs`
- **Acceptance criteria:**
  - [ ] For 500 random snapshots (seed 11), the set of emitted faces equals a brute-force reference (a face exists iff the cell is structural and the neighbour does not fully cover that side), including faces on brick borders.
  - [ ] Triangles == 2 × exposed quads + exposed triangles; ≤ 2 per exposed face.
  - [ ] Every triangle's winding matches its face normal; AO values equal a brute-force evaluation.
  - [ ] A uniform-colour 16×16 wall face emits 289 vertices (welding works).
  - [ ] Typical damaged hull brick ≤ 60 µs median (Burst, safety off, single thread).
- **Tests:** `BrickMesherTests.Faces_MatchBruteForce`, `.BorderCulling_UsesNeighbours`, `.Lod0_TwoTrianglesPerExposedFace`, `.Winding_MatchesNormals`, `.AO_MatchesBruteForce`, `.Welding_FlatWall289`; `BrickMesherPerfTests.Lod0_TypicalDamagedHullBrick_Under60us`, `.Lod0_Checkerboard_Report`.

### REN-007 — LOD1 mesher
- **Milestone:** M1
- **Tier:** frontier — F3: greedy merging
- **Depends on:** `REN-006`
- **Do:** Implement `BrickMesher.MeshLod1` per §5.3.
- **Files:** `Assets/Game/Voxel/Rendering/BrickMesher.cs`; `Assets/Game/Tests/EditMode/Voxel/Rendering/BrickLod1Tests.cs`
- **Acceptance criteria:**
  - [ ] Greedy rectangles exactly tile the exposed super-cell faces (area check against a brute-force count).
  - [ ] LOD1 triangles ≤ 25% of LOD0 over `HollowStation1M`, `Ship20k` and 200 random-noise bricks.
  - [ ] All positions are within 0..16 and even.
- **Tests:** `BrickLod1Tests.Greedy_TilesExactly`, `.TriangleRatio_AtMost25Percent`, `.Positions_EvenAndInRange`.

### REN-008 — Brick and prop shaders, placeholder art
- **Milestone:** M1
- **Tier:** frontier — F1: shader contract every future asset depends on; DOTS-instancing URP HLSL
- **Depends on:** `REN-004`
- **Do:** Write `BrickLit.shader`, `BrickSurface.hlsl` (placeholder implementation of §4.4), `PropInstanced.shader`; an editor utility `VoxelPlaceholderArtBuilder` (menu `Game/Voxel/Build Placeholder Brick Art`) that builds `Texture2DArray` assets (64 slices, 256×256: slice 0 from `metalpatern.png`, slices 1..62 tinted copies, slice 63 magenta-black checker "missing"; normal array flat; mask array metallic 0.6 smoothness 0.4 no emission; crack array 3 procedural noise masks) and the materials `BrickPlaceholder.mat` and `PropPlaceholder.mat` under `Assets/Game/Art/_Placeholder/`.
- **Files:** `Assets/Game/Voxel/Rendering/Shaders/{BrickLit.shader,BrickSurface.hlsl,PropInstanced.shader}`; `Assets/Game/Editor/Voxel/VoxelPlaceholderArtBuilder.cs`; `Assets/Game/Art/_Placeholder/Textures/{BrickAlbedoArray,BrickNormalArray,BrickMaskArray,BrickCrackArray}.asset`; `Assets/Game/Art/_Placeholder/Materials/{BrickPlaceholder,PropPlaceholder}.mat`; `Assets/Game/Tests/PlayMode/Voxel/BrickShaderTests.cs`
- **Acceptance criteria:**
  - [ ] Shaders compile for Metal and Direct3D 11 with zero errors and are SRP-batcher compatible (Frame Debugger check recorded).
  - [ ] A rendered test brick shows the expected palette colour per colour index, AO darkening at inner corners, crack overlay at damage 3, and casts a shadow.
  - [ ] No C# file references the placeholder assets (conventions guard passes).
- **Tests:** `BrickShaderTests.PaletteColour_PerIndex`, `.CracksVisible_AtDamage3`, `.ShadowCast`, `.PropInstances_Render`.

### REN-009 — Render config definitions and factory
- **Milestone:** M1
- **Tier:** standard — definitions listed field by field
- **Depends on:** `REN-001`, `REN-008`
- **Do:** Implement `VoxelRenderConfigDefinition`, `VoxelPaletteDefinition` (legacy colours 0..16 in the placeholder asset), `VoxelRenderConfigFactory.Create/Destroy`. Create `config_voxel_render.asset` and `config_voxel_palette.asset` in `Assets/Game/Content/Config/`.
- **Files:** `Assets/Game/Data/Definitions/Voxel/{VoxelRenderConfigDefinition,VoxelPaletteDefinition}.cs`; `Assets/Game/Voxel/Rendering/VoxelRenderConfigFactory.cs`; `Assets/Game/Content/Config/{config_voxel_render,config_voxel_palette}.asset`; `Assets/Game/Tests/EditMode/Voxel/Rendering/RenderConfigTests.cs`
- **Acceptance criteria:**
  - [ ] Factory output matches the definition values; palette texture pixels equal the colours; `Destroy` leaks nothing.
  - [ ] Validation: missing materials or palette → error; palette length ≠ 256 → error.
- **Tests:** `RenderConfigTests.Factory_CopiesValues`, `.PaletteTexture_MatchesColours`, `.Validation_Errors`.

### REN-010 — BrickMeshBuildSystem
- **Milestone:** M1
- **Tier:** frontier — F2/F4: cross-frame jobs, budgets, queue priorities
- **Depends on:** `REN-005`, `REN-006`, `REN-007`
- **Do:** Implement `BrickMeshBuildSystem` per §5.4 with `BrickMeshJob`; the queue methods are implemented by VOX-021 (Q14).
- **Files:** `Assets/Game/Voxel/Rendering/{BrickMeshBuildSystem,BrickMeshJob}.cs`; `Assets/Game/Tests/EditMode/Voxel/Rendering/BrickMeshBuildSystemTests.cs`
- **Acceptance criteria:**
  - [ ] Never more than one batch in flight; batch size stays within `[MeshBatchMin, MeshBatchMax]`.
  - [ ] The mesh job has no ECS dependencies (job debugger: safety system reports no lookups on it).
  - [ ] Main-thread cost ≤ 0.5 ms for a 128-brick batch (snapshot included).
- **Tests:** `BrickMeshBuildSystemTests.OneBatchInFlight`, `.AdaptiveBatchSize`, `.DestroyedBricks_Skipped`, `.MainThreadCost`.

### REN-011 — BrickMeshApplySystem, cleanup and statistics
- **Milestone:** M1
- **Tier:** frontier — F2/F4: mesh lifetime, registration, deferred disposal, apply budget
- **Depends on:** `REN-010`, `SPEC-10`
- **Do:** Implement `BrickMeshApplySystem` (§5.5) with `ApplyResultsJob`, registration, managed state; `BrickRenderCleanupSystem` (§5.6); `BrickRenderStats.Gather`.
- **Files:** `Assets/Game/Voxel/Rendering/{BrickMeshApplySystem,ApplyResultsJob,BrickRenderCleanupSystem,VoxelRenderManagedState,BrickRenderStats}.cs`; `Assets/Game/Tests/PlayMode/Voxel/BrickMeshApplyTests.cs`
- **Acceptance criteria:**
  - [ ] Each brick registers exactly one mesh in its lifetime (registration counter) across 20 rebuilds; `MaterialMeshInfo.MeshID` never changes for a living brick.
  - [ ] A destroyed brick's mesh is unregistered and destroyed 3 frames later; entity count of cleanup-only entities returns to 0.
  - [ ] Empty results disable `MaterialMeshInfo`; `RenderBounds` equals the mesh bounds.
  - [ ] Apply of 128 bricks ≤ 2.5 ms main thread; 0 GC per frame after warm-up excluding first-build `new Mesh`.
- **Tests:** `BrickMeshApplyTests.RegisterOnce_RewriteInPlace`, `.DestroyedBrick_MeshRetired`, `.EmptyMesh_Hidden`, `.Bounds_Updated`, `.ApplyBudget128`, `.NoGcSteadyState`.

### REN-012 — GridLodSystem, Dormant and LOD switching
- **Milestone:** M1
- **Tier:** frontier — F4/F5: drives LOD rebuilds and the simulation-wide `Dormant` flag
- **Depends on:** `REN-011`
- **Do:** Implement `GridLodSystem` (§5.7) with its three jobs.
- **Files:** `Assets/Game/Voxel/Rendering/{GridLodSystem,GridDormancyJob,BrickLodJob,LodEnqueueJob}.cs`; `Assets/Game/Tests/EditMode/Voxel/Rendering/GridLodSystemTests.cs`
- **Acceptance criteria:**
  - [ ] A brick crossing 400 m switches to LOD1; it switches back below 360 m and not between 360 and 400 m.
  - [ ] A grid beyond 3,000 m from the anchor becomes dormant; returns below 2,700 m; its bricks never desire LOD0 while dormant.
  - [ ] Only changed values are written (change version unchanged on idle frames).
  - [ ] Main-thread cost ≤ 0.1 ms for 5,000 bricks.
- **Tests:** `GridLodSystemTests.Lod_Hysteresis`, `.Dormant_Hysteresis`, `.Dormant_ForcesLod1`, `.Idle_NoWrites`, `.InvalidViewer_AllLod0`, `.MainThreadCost5000`.

### REN-013 — Rendering gate suite
- **Milestone:** M1
- **Tier:** frontier — F4: owns the moving-ship transform gate
- **Depends on:** `REN-012`, `REN-008`
- **Do:** PlayMode performance tests (safety off, 1920×1080, 60-frame warm-up, 300 frames): `Lattice2500` grid moved every frame on a circle (transform gate); `HollowStation1M` idle in view (render share); a screenshot comparison of `Ship20k` against a stored reference image (tolerance: mean absolute error ≤ 2/255) to catch shader/mesher regressions.
- **Files:** `Assets/Game/Tests/PlayMode/Voxel/{BrickRenderPerfTests,BrickRenderScreenshotTests}.cs`; `Assets/Game/Tests/PlayMode/Voxel/Reference/Ship20k.png`
- **Acceptance criteria:**
  - [ ] Moving 2,500 bricks: transform + Entities Graphics main-thread delta ≤ 0.5 ms median.
  - [ ] Idle station render share ≤ 1.2 ms median.
  - [ ] Screenshot matches the reference.
- **Tests:** `BrickRenderPerfTests.MovingLattice2500_TransformUnder0_5ms`, `.IdleStation1M_RenderShare`; `BrickRenderScreenshotTests.Ship20k_MatchesReference`.

### REN-014 — Prop block instancing
- **Milestone:** M1
- **Tier:** frontier — F2: GraphicsBuffer lifetime and change-driven uploads
- **Depends on:** `REN-011`, `REN-008`
- **Do:** Implement `PropRenderSystem` (§5.8) with `PropGatherJob` and `GridMatrixJob`, using a test Prop block definition (`block_test_prop`, created by `TestContentFactory` in tests only).
- **Files:** `Assets/Game/Voxel/Rendering/{PropRenderSystem,PropGatherJob,GridMatrixJob,PropInstanceGpu}.cs`; `Assets/Game/Tests/PlayMode/Voxel/PropRenderTests.cs`
- **Acceptance criteria:**
  - [ ] 10,000 props on 20 moving grids render (screenshot check of one prop) at ≤ 0.3 ms main thread per frame; rebuild frame ≤ 1 ms.
  - [ ] Removing a prop's cell removes the instance after the brick's next apply.
  - [ ] Buffers are released on `OnDestroy` (no GraphicsBuffer leak warnings).
- **Tests:** `PropRenderTests.Props_Render`, `.PropRemoved_Disappears`, `.Budget10000`, `.NoBufferLeaks`.

### REN-015 — LOD2 grid proxies
- **Milestone:** M4
- **Tier:** frontier — F3/F4: greedy over whole grids, swaps visibility of thousands of entities
- **Depends on:** `REN-012`
- **Do:** Implement `GridProxySystem` (§5.9) and the proxy entity creation in SPEC-10's `GridSpawnSystem` (one extra child per root).
- **Files:** `Assets/Game/Voxel/Rendering/{GridProxySystem,GridProxyMeshJob}.cs`; `Assets/Game/Voxel/Grid/GridSpawnSystem.cs`; `Assets/Game/Tests/PlayMode/Voxel/GridProxyTests.cs`
- **Acceptance criteria:**
  - [ ] Beyond 2,000 m a grid draws exactly one proxy draw and no brick draws; returns below 1,800 m.
  - [ ] Proxy triangles ≤ 5% of LOD0 for `HollowStation1M`.
  - [ ] At most one proxy rebuilt per frame.
- **Tests:** `GridProxyTests.Switch_Hysteresis`, `.TriangleBudget`, `.OneRebuildPerFrame`.

## 10. Open questions

| # | Question | Default if unanswered | Decider | Answer |
|---|---|---|---|---|
| 1 | Who writes `VoxelViewer` and when? | SPEC-62 `CameraTargetSyncSystem` every frame: `CameraPosition` = main camera, `AnchorPosition` = player ship root position (camera target), `Valid = 1` in gameplay | SPEC-62 | |
| 2 | Is `Dormant` based on the player ship or the camera? | Player ship (`AnchorPosition`); architecture §11.9 says "far from the player" | Tech lead | |
| 3 | `Dormant` is a simulation-affecting flag written in a variable-rate group; determinism tests | Determinism scenarios keep every grid within 2,000 m of the player so `Dormant` never toggles | SPEC-71 | |
| 4 | Final palette: how many colours and who owns them | 256 slots, placeholder fills legacy 0..16; SPEC-80 replaces the asset | SPEC-80 | |
| 5 | Transparent or glass blocks | Not supported in v1; a second material and per-brick second entity would need an ADR | Owner | |
| 6 | Should hulks render darker (dead ship look)? | `[MaterialProperty("_HulkTint")]` component reserved; SPEC-63/80 decide | SPEC-80 | |
| 7 | URP shadow distance | 300 m in `URP_Default.asset` (SPEC-62/80 owns the asset); LOD1 at 400 m | SPEC-62 | |
| 8 | Damage tier thresholds | `damage = 3 − (Hp >> 6)` (fixed; matches `BrickWrite` border-tier rule) | SPEC-13 | |
| 9 | Prop mesh authoring scale and pivot | 1 unit = 1 cell, pivot at root cell centre (SPEC-04 §11) | SPEC-81 | |
| 10 | ADR-0018 adds three shapes (`HipCorner`, `LongSlopeHigh`, `LongSlopeLow`): the snapshot shape field needs 3 bits, `VoxelNormals` needs the 24 normals `(0,1,2)/√5` (permutations and signs), and `BrickShapeTables` covers 5 × 24 entries | Snapshot layout: bits 0-1 class, 2-4 shape, 5-9 orient, 10-15 layer, 16-23 colour, 24-25 damage, 26 scorched, 27-31 free; full faces are no longer stored per cell but read from `BrickShapeTables.FullFaces[shape*24+orient]` during meshing. `VoxelNormals.Count` = 42 (indices 18-41 the long-slope normals); REN-004 criteria apply to all 5 shapes | SPEC-11 lead | Resolved 2026-10-11: applied the ADR-0018 default layout; encode drops fullFaces, coverage comes from shape tables. REN-004 implements all five shapes. |
| 11 | ShapeFace is labelled 56 bytes but its exact fields occupy 52 | Keep the listed fields; correct the size assertion to 52 | Tech lead | Resolved 2026-10-11 in REN-001: four byte fields + four 12-byte int3 fields = 52, with 4-byte alignment; no artificial padding or field change. |
| 12 | Public VoxelRenderRuntime.Managed names an internal class, which C# rejects | Make the class public while keeping its implementation fields internal | Tech lead | Resolved 2026-10-11 in REN-001: class visibility permits the specified public field; mutable managed state remains module-owned. |
| 13 | The specified AABB type is in an unreferenced Entities assembly | Add the installed Unity.Mathematics.Extensions reference | Tech lead | Resolved 2026-10-11 by accepted ADR-0022; Game.Voxel already permits the Entities package, with no new package or field change. |


| 14 | Spawning needs BrickMeshQueue.Enqueue but REN-010 depends on finished SPEC-10 through snapshot gathering | Split the existing queue methods and four tests into SPEC-10 VOX-021, depending only on REN-001 | Tech lead | Resolved 2026-10-11: VOX-013/015 depend on VOX-021; REN-010 already depends on SPEC-10 through REN-005. This keeps the contract-only cross-spec dependency grammar. No new queue API or renderer implementation is introduced. |

| 15 | Priority-only stale detection revives an old request if the brick is consumed and requeued at its old priority before draining | Invalidate the prior entry permanently on upgrade, without changing public storage fields | Tech lead | Resolved 2026-10-11: StaleSkipped reproduced wrong FIFO order and would return the old grid. Upgrade scans the old priority list backward and clears that entry; pending-map priority and FIFO lists otherwise retain their contract. |

| 16 | Approved long slopes need half-cell vertices but the old format only encodes whole corners | Keep eight-byte vertices and use half-cell integer coordinates | Tech lead | Resolved 2026-10-11 by accepted ADR-0027: exact five-shape geometry, shader ×0.5 position decode, 33×33 weld grid, cell-unit bounds and UVs; REN-004 criteria cover 120 entries. |
| 17 | Long-slope normal ordering and tangents were unspecified | Pair-major, then magnitude swap, then sign bits; positive zero-axis tangent | Tech lead | Resolved 2026-10-11: §4.1 gives exact indices 18..41 and C#/HLSL parity tests. No visual or shape change. |

## 11. References

- Audit: `00-synthesis.md` §6c; `01-voxel-core.md` §5.1, §6.3, §6.7, B19b; `07-project-health.md` §5 (render settings); LEDGER R22, R24.
- Binding: architecture §5.1, §11.3–§11.5, §11.9–§11.11; [07-implementation-traps.md](../07-implementation-traps.md) T2.3, T3.1–T3.5.
- Unity: `EntitiesGraphicsSystem.RegisterMesh/RegisterMaterial/UnregisterMesh`, `MaterialMeshInfo(BatchMaterialID, BatchMeshID)`, `RenderMeshUtility.AddComponents`, `Mesh.AllocateWritableMeshData`, `Mesh.ApplyAndDisposeWritableMeshData`, `Graphics.RenderMeshPrimitives`, `Graphics.RenderPrimitivesIndexedIndirect`, `UnityIndirect.cginc`.
- Prior art: 0fps "Ambient occlusion for Minecraft-like worlds" (vertex AO rule and diagonal flip).
