# SPEC-10 — Voxel data model and spawning

| | |
|---|---|
| **Status** | Draft |
| **Area code** | VOX (task IDs are `VOX-001`, `VOX-002`, …) |
| **Module(s)** | `Game.Voxel` (folders `Assets/Game/Voxel/Model/` and `Assets/Game/Voxel/Grid/`); definitions in `Game.Data` (`Assets/Game/Data/Definitions/Voxel/`) |
| **Depends on specs** | SPEC-02 (`ContentId`, `ContentHash`, `PersistentId`, `StableHash`, `GameLog`, `GameplayActive`); SPEC-04 (`BlockDef`, `BlockDefsBlob`, `OrientationTablesBlob`, `BlockPlacementMath`, `ContentCatalog`, `ContentService.CreateForTests`); SPEC-05 (VOXS byte layout); SPEC-11 and SPEC-12 (their contract types, added to entities and singletons at spawn); SPEC-71 (`EcsTestWorld`, perf harness) |
| **Depended on by** | SPEC-11, SPEC-12, SPEC-13 (cell write helpers, ECS layout, prototypes), SPEC-20/21/22 (functional entities, `FunctionalComponentRegistration`), SPEC-05 (codec), SPEC-01 (creates the registry and config singletons), SPEC-31 (spawn/despawn, `IGridSpawnHook`, write-back), SPEC-51 (`VoxelStructure` editing, `GridEditRequest`), SPEC-72 (blueprint encoding), SPEC-70 (benchmark structures) |
| **Milestone** | M1 (VOX-020 in M3) |

> Read [01-architecture.md](../01-architecture.md) (especially §5.1 and §11), [02-conventions.md](../02-conventions.md), [03-glossary.md](../03-glossary.md) and [07-implementation-traps.md](../07-implementation-traps.md) before this spec. This spec may not contradict them.

## 1. Purpose

This spec defines how voxel data is stored and how a stored structure becomes live ECS entities. A grid's cells live in sparse 16³ bricks of 4-byte cells; the same layout exists outside ECS as `VoxelStructure` (editor, saves, tests, spawn source) and inside ECS as one root entity, one entity per non-empty brick and one entity per functional block. It defines the brick coordinate maths, the solid mask, the dirty/revision bookkeeping that drives incremental remeshing (SPEC-11) and collider rebuilds (SPEC-12), the structure registry and blueprint loading, despawn with damage write-back, deferred disposal of meshes and blobs, and the memory model for the 1M-cell reference station. The old prototype made every voxel an entity (~300 B and O(voxels) per frame, 01 §5), ignored multi-cell footprints at runtime (01-B1), never persisted damage (04-B3) and keyed baked data on unstable hashes (01-B8/B9). This spec makes those impossible by construction.

## 2. Scope

**In scope:**
- `Cell`, `CellFlags`, `PaletteEntry`, `BlockInstance`, `GridKind` and every other §6.3 type owned by the voxel data model.
- Brick maths (cell ↔ brick ↔ local index), the solid-mask bit layout and bit operations.
- `VoxelStructure` (managed owner) and `VoxelStructureData` (native storage): sparse paged bricks, uniform-brick compression, cell get/set, block place/remove with footprint and orientation, compaction, clone, diff, revision, mass accumulation, content hash.
- `VoxelStructureRegistry` (handles used by `GridSpawnRequest`), `BlueprintLibrary`, `ShipBlueprintDefinition`, VOXS codec (conversion between `VoxelStructure` and the SPEC-05 VOXS payload).
- Grid-space DDA raycast over a structure and over a live grid.
- ECS layout: `VoxelGrid`, `BrickSlot`, `Brick`, `BrickCells`, `BrickSolidMask`, `BrickDirty`, `FunctionalBlock`, spawn/rebuild/despawn/edit requests.
- `GridSpawnSystem` (incl. the `StarSystemMember` tagging hook and `PersistentId` copy), `BrickDirtyScheduleSystem`, `DeferredDisposalSystem`; write-back API; cell write helpers.
- Memory model, benchmark structures and the integrated voxel perf-gate suite.

**Out of scope (and who owns it instead):**
- Block definitions, orientation tables, placement maths: SPEC-04.
- VOXS byte layout and the chunk container, IDPL, Missing reporting: SPEC-05 (this spec implements the encoder/decoder against it).
- Meshing, materials, LOD, the `Dormant` tag, prop rendering: SPEC-11. Colliders, mass, physics body components, `PhysicsLayers`: SPEC-12.
- Damage resolution, integrity, splitting (including creating hulk grids through the GameplayEnd ECB), ship death: SPEC-13 (uses this spec's write helpers and prototypes).
- Edit sessions, undo/redo, BOM, validation rules: SPEC-51. Star-system streaming, deltas and the `StarSystemMember` value: SPEC-31. `PersistentId` allocation: SPEC-01 (`GameSession`).

## 3. Decisions (binding)

1. **Cells are 4 bytes: `ushort Ref`, `byte Hp`, `byte Flags`; air is `default(Cell)` (all zero).** Reason: architecture §6.3; memory at 1M cells.
2. **`Ref` bit 15 = 0 → palette index 1..32767; bit 15 = 1 → instance index 0..32767; `Ref == 0` → air. Palette index 0 is reserved and never referenced.** Reason: architecture §6.3; one compare tests air.
3. **A solid cell always has `Hp ≥ 1`; `Hp == 255` is full health.** The resolver turns a cell to air instead of writing `Hp = 0`. Reason: `Hp` is never ambiguous with "destroyed".
4. **Palette ⇔ `!BlockDef.RequiresInstance`; instance ⇔ `RequiresInstance`** (SPEC-04 decision 13). Single-cell Structural and single-cell Prop blocks use the palette; Functional and multi-cell blocks use a `BlockInstance`, and every footprint cell points at it. Reason: 01-B1 by construction.
5. **Bricks are 16³ and stored in linear order `index = x | y << 4 | z << 8` (x fastest).** Morton order is not used. Reason: a mask row (16 x-cells) is one `ushort`; meshing, greedy boxes and downsampling work on rows with shifts; structure ↔ ECS copies are one `memcpy`.
6. **Brick coordinate = `cell >> 4`, local = `cell & 15`** (arithmetic shift, correct for negative cells). Grid space: cell `c` occupies `[c, c+1) × CellSize` metres from the grid origin; the grid origin is the root entity's `LocalTransform`.
7. **ECS bricks are always dense: `BrickCells` has exactly 4,096 entries** (architecture §6.3). `VoxelStructure` additionally compresses full bricks whose 4,096 cells are identical into one `Cell` ("uniform brick", `Page == -1`); spawn expands them with a replicate copy. Air bricks never exist in either form. Reason: binding ECS contract stays simple for SPEC-13; cached blueprints and saves of solid asteroids/armour shrink 30×.
8. **`BrickCells` and `BrickSolidMask` are `[InternalBufferCapacity(0)]` buffers** (4,096 `Cell`s and 64 `ulong` words), so their 16 KB + 512 B live outside the chunk (architecture §6.3, §11.5).
9. **ECS refers to structures only by `int StructureHandle`** into the managed `VoxelStructureRegistry`, and to blueprints by `ContentHash`. The registry is a runtime `ScriptableObject` reached from ECS through the singleton `VoxelStructureRegistryRef { UnityObjectRef<VoxelStructureRegistry> }`; only `GridSpawnSystem` (main thread) dereferences it inside ECS. No managed type appears in any component; per-entity components never hold native containers (only module singletons such as `DeferredDisposalQueue` do, and jobs receive those containers as job fields). Reason: architecture §6.3, conventions §2.14, §3.3 "ECS never reads managed statics".
10. **A spawn request entity becomes the grid root.** Components the requester puts on it (`Allegiance`, `StarSystemMember`, `PlayerShip`, AI state) stay on the root. `GridSpawnSystem` adds `PersistentId` from `GridSpawnRequest.PersistentId`. Reason: Voxel cannot reference Simulation/Galaxy types.
11. **`StarSystemMember` tagging hook:** after creating brick or functional entities for a root, `GridSpawnSystem` calls every registered `IGridSpawnHook.OnEntitiesCreated(em, root, created)`. SPEC-31 registers the hook that batch-adds the root's `StarSystemMember` (if any) to `created`. Reason: architecture §6.5 puts `StarSystemMember` on roots, bricks and functional entities; `Game.Voxel` cannot name the type.
12. **The root's `LinkedEntityGroup` (`[InternalBufferCapacity(0)]`, Unity's type) contains the root, every brick and every functional entity.** Destroying the root destroys the grid. Stale entries of bricks destroyed earlier are harmless (destroy skips them; versions differ). SPEC-13 removes bricks it moves into another grid from the old root's group.
13. **Structural changes:** only `GridSpawnSystem` uses `EntityManager` directly, always with batch APIs (`Instantiate(prefab, NativeArray)`, `AddComponent(NativeArray, type)`, `DestroyEntity(NativeArray)`). It also creates and disposes every `Game.Voxel` runtime singleton (VOX, REN and PHY types, through their contract `Create()`/`Dispose()` methods) in its `OnCreate`/`OnDestroy`. Every other system in this spec records into an ECB from an allowed ECB system. Reason: conventions §2.5.
14. **Revision bookkeeping:** `VoxelGrid.Revision` increments once per tick per grid that changed; every brick changed in that tick gets `Brick.ChangedRevision = VoxelGrid.Revision`. Write-back copies bricks with `ChangedRevision > GridSource.SyncedRevision`. Reason: save capture and despawn copy only changed bricks (O(changed bricks), not O(grid)).
15. **Rebuild work is incremental and per brick (architecture §11.1).** Fixed-step writers set `BrickDirty`; `BrickDirtyScheduleSystem` moves dirty bricks (and face neighbours for meshing) into the SPEC-11 mesh queue and SPEC-12 collider queue; spawn, rebuild and edit enqueue directly. Nothing rebuilds a whole grid's meshes or colliders because one cell changed.
16. **Spawn is time-sliced (architecture §11.2):** at most `VoxelConfig.SpawnBricksPerFrame` (1,024) brick entities are created per frame across all requests, oldest request first; the grid carries `GridSpawning` (enabled) until complete. SPEC-13 and SPEC-12 skip spawning grids.
17. **Folder/namespace layout of `Game.Voxel`:** four specs share the module, so each owns an area folder: `Model/` + `Grid/` (SPEC-10, namespaces `Game.Voxel.Model`, `Game.Voxel.Grid`), `Rendering/` (SPEC-11), `PhysicsBody/` (SPEC-12), `Damage/` (SPEC-13, recommended). Files sit directly in the area folder (one namespace per area). Reason: conventions §1 sub-folder rule; no file collisions between parallel specs.
18. **Reference station for the memory gate** is `HollowStation1M` (§5.11): a hollow box 256×128×256 cells with 4-cell (2 m) walls, one structural block, one colour. The thin-shell variant (1-cell walls) is a reported stress case, not a gate (§8.2). Reason: 06 does not define the geometry; 2 faces per cell make any 1M-cell 1-cell shell exceed 40 MB at LOD0.
19. **Profiler markers** are `static readonly ProfilerMarker` fields named `<SystemName>.<Step>` (e.g. `GridSpawnSystem.CreateBricks`).

## 4. Data model

All types below are **public contract** unless marked *internal*. Each file holds one type and starts with `/// <summary>Implements SPEC-10 §4.x.</summary>`.

### 4.1 Cells, palette, instances (`Assets/Game/Voxel/Model/`, namespace `Game.Voxel.Model`)

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct Cell : IEquatable<Cell>                      // 4 bytes (architecture §6.3)
{
    public ushort Ref;     // 0 = air; bit15=0: palette index 1..32767; bit15=1: instance index (Ref & 0x7FFF)
    public byte Hp;        // 1..255 for solid cells (255 = full = BlockDef.CellHp); 0 only for air
    public byte Flags;     // CellFlags
    public const ushort InstanceBit = 0x8000;
    public const ushort MaxPaletteIndex = 0x7FFF;          // 32767
    public const int MaxInstanceIndex = 0x7FFF;            // 32767
    public bool IsAir => Ref == 0;
    public bool IsInstance => (Ref & InstanceBit) != 0;
    public int InstanceIndex => Ref & 0x7FFF;              // valid only if IsInstance
    public ushort PaletteIndex => Ref;                     // valid only if !IsInstance && !IsAir
    public static Cell Palette(ushort paletteIndex, byte hp = 255, byte flags = 0) => new Cell { Ref = paletteIndex, Hp = hp, Flags = flags };
    public static Cell Instance(int instanceIndex, byte hp = 255, byte flags = 0) => new Cell { Ref = (ushort)(InstanceBit | instanceIndex), Hp = hp, Flags = flags };
    public bool Equals(Cell o) => Ref == o.Ref && Hp == o.Hp && Flags == o.Flags;
    public override int GetHashCode() => Ref | (Hp << 16) | (Flags << 24);   // in-memory only, never persisted
}

[Flags] public enum CellFlags : byte
{
    None = 0,
    InstanceRoot = 1,      // this cell is footprint cell (0,0,0) of its BlockInstance
    Scorched = 2,          // cosmetic burn mark; written by SPEC-13; SPEC-11 darkens the face
    // bits 2..7 reserved; claim through SPEC-10 open questions
}

public struct PaletteEntry : IBufferElementData             // 4 bytes; per grid; buffer element on the grid root
{
    public ushort Block;   // BlockIndex (SPEC-04); 0 only in entry 0 (reserved) or an unused entry
    public byte Orient;    // 0..23
    public byte Color;     // paint palette index (SPEC-11 VoxelPaletteDefinition)
}

[Flags] public enum BlockInstanceFlags : byte { None = 0, Destroyed = 1 }   // Destroyed: function lost (SPEC-13); cells may remain

[InternalBufferCapacity(0)]
public struct BlockInstance : IBufferElementData             // 32 bytes; buffer element on the grid root
{
    public ushort Block;          // BlockIndex; 0 = free slot (tombstone)
    public byte Orient;           // 0..23
    public byte Color;
    public int3 Origin;           // grid cell of footprint cell (0,0,0)
    public float FunctionHp;      // absolute HP, starts at BlockDef.FunctionHp; runtime writer: DamageResolveSystem only
    public Entity Functional;     // functional entity, or Entity.Null (Structural/Prop kinds, or outside ECS)
    public ushort CellsAlive;     // footprint cells still solid; starts at BlockDef.Cells.Length
    public BlockInstanceFlags Flags;
    public byte Reserved;
}

public enum GridKind : byte { Ship = 0, Station = 1, Asteroid = 2, Hulk = 3 }       // binding (architecture §6.3)
public enum GridMotion : byte { Dynamic = 0, Kinematic = 1, Static = 2 }
[Flags] public enum GridSpawnFlags : ushort
{
    None = 0,
    WriteBack = 1,        // the source structure is owned by this grid: despawn and save write damage back into it
    PlanarInertia = 2,    // SPEC-12 locks rotation to the Y axis (ADR-0003); set together with SPEC-20's PlanarConstraint
    NoRender = 4,         // create bricks without render components (headless tools and tests)
    EditorPreview = 8,    // grid edited through GridEditRequest; SPEC-13 never damages it
}

public struct BlockPlacement : IEquatable<BlockPlacement>  // a placed block (palette cell or instance); Equals compares all four fields
{ public ushort Block; public int3 Origin; public byte Orient; public byte Color; }

public enum PlaceResult : byte { Ok = 0, Occupied = 1, InvalidBlock = 2, OrientationNotAllowed = 3, PaletteFull = 4, InstancesFull = 5, OutOfRange = 6, ReadOnly = 7 }

public struct VoxelContent                                   // the two SPEC-04 blobs, passed together
{ public BlobAssetReference<BlockDefsBlob> Blocks; public BlobAssetReference<OrientationTablesBlob> Orientations; }
```

### 4.2 Brick maths and masks (`Model/`)

```csharp
public static class BrickMath                                // Burst-compatible, no allocation
{
    public const int Size = 16, Shift = 4, Mask = 15, CellsPerBrick = 4096, MaskWords = 64;
    public static int3 BrickOf(int3 cell) => cell >> Shift;
    public static int3 LocalOf(int3 cell) => cell & Mask;
    public static int LocalIndex(int3 local) => local.x | (local.y << 4) | (local.z << 8);
    public static int LocalIndex(int x, int y, int z) => x | (y << 4) | (z << 8);
    public static int3 LocalFromIndex(int i) => new int3(i & 15, (i >> 4) & 15, i >> 8);
    public static int3 CellOf(int3 brick, int localIndex) => (brick << Shift) + LocalFromIndex(localIndex);
    public static int SlotIndex(int3 brick, int3 brickMin, int3 brickDims);   // -1 if outside; else d.x + dims.x*(d.y + dims.y*d.z), d = brick - brickMin
    public static float3 CellMinCorner(int3 cell, float cellSize) => (float3)cell * cellSize;
    public static float3 CellCenter(int3 cell, float cellSize) => ((float3)cell + 0.5f) * cellSize;
    public static int3 CellAt(float3 gridLocalPoint, float cellSize) => (int3)math.floor(gridLocalPoint / cellSize);
    public static BlockFaceMask BorderFaces(int localIndex); // brick faces the cell touches: x==0 → NegX, x==15 → PosX, y==0 → NegY, ...
    public static int3 FaceDirection(BlockFace f);           // PosX→(1,0,0), NegX→(-1,0,0), PosY, NegY, PosZ, NegZ (SPEC-04 order)
}

/// 4096-bit solid mask as a value (structure storage, snapshots, collider batches).
/// Bit i = cell with local index i is solid (Ref != 0). Word w = i >> 6, bit b = i & 63.
/// With i = x + 16y + 256z: w = (y >> 2) + 4z, b = x + 16*(y & 3).
/// Row (y, z) — the 16 x-cells — is (ushort)(Words[(y>>2) + 4z] >> (16*(y&3))).
public unsafe struct BrickMaskBits                           // 512 bytes
{
    public fixed ulong Words[64];
    public bool Get(int i);
    public void Set(int i, bool solid);
    public ushort Row(int y, int z);
    public void SetRow(int y, int z, ushort bits);
    public int CountSolid();                                 // math.countbits over 64 words
    public bool IsEmpty();
    public bool IsFull();
    public bool TryGetBounds(out int3 min, out int3 max);    // inclusive local bounds; false if empty
    public static BrickMaskBits Full();
    public void CopyFrom(DynamicBuffer<BrickSolidMask> buffer);   // buffer.Length must be 64
    public void CopyTo(DynamicBuffer<BrickSolidMask> buffer);     // resizes to 64
}

/// ECS form of the mask (architecture §6.3: 512 B per brick, capacity 0).
[InternalBufferCapacity(0)] public struct BrickSolidMask : IBufferElementData { public ulong Bits; }   // exactly 64 elements
```

### 4.3 Structure storage (`Model/`)

```csharp
public struct StructureMassData                              // accumulators; double so 1M-cell sums do not drift
{
    public double Mass;            // Σ m (kg)
    public double3 FirstMoment;    // Σ m·p (p = cell centre, grid-local metres)
    public double3 SecondDiag;     // Σ m·(px², py², pz²)
    public double3 SecondOff;      // Σ m·(px·py, py·pz, pz·px)
    public double CellInertia;     // Σ m·s²/6 (each cell's own cube inertia about its centre, s = CellSize)
    public uint CellCount;
}

public struct StructureHeader
{
    public float CellSize;         // metres; default 0.5
    public uint Revision;          // +1 on every mutating call
    public int3 CellMin, CellMax;  // inclusive solid bounds; valid when BoundsValid == 1
    public byte BoundsValid;       // 0 after any mutation; recomputed lazily
    public byte ReadOnly;          // 1 for blueprint structures: mutators refuse
    public int PinCount;           // > 0 while a spawn or rebuild reads it: mutators throw
    public uint MassRevision;      // Revision at which Mass was computed; 0 = never
    public StructureMassData Mass;
}

public struct BrickRecord                                    // 32 bytes
{
    public int3 Coord;             // brick coordinate
    public int Page;               // page number in CellPages (page p = cells [p*4096, p*4096+4096)); -1 = uniform
    public Cell Uniform;           // valid when Page == -1
    public ushort SolidCount;      // 1..4096
    public ushort Reserved;
    public uint ChangedRevision;   // Header.Revision of the last change to this brick
}

public struct VoxelStructureData                             // copy = same storage. Only VoxelStructure disposes.
{
    public NativeReference<StructureHeader> Header;
    public NativeList<PaletteEntry> Palette;                 // [0] reserved (Block 0)
    public NativeList<BlockInstance> Instances;
    public NativeList<int> FreeInstances;                    // tombstoned instance indices, LIFO reuse
    public NativeList<BrickRecord> Bricks;                   // unordered; SortedBrickOrder gives canonical order
    public NativeList<BrickMaskBits> Masks;                  // parallel to Bricks
    public NativeHashMap<int3, int> BrickIndex;              // coord → index into Bricks (lookup only, never iterated)
    public NativeList<Cell> CellPages;                       // dense pages of 4096 cells, local linear order
    public NativeList<int> FreePages;                        // free page numbers, LIFO reuse
    public bool IsCreated => Header.IsCreated;
}

public struct StructureBlock { public BlockPlacement Placement; public int InstanceIndex; /* -1 = palette cell */ }

public struct StructureDiff : IDisposable
{ public NativeList<BlockPlacement> Removed; public NativeList<BlockPlacement> Added; public void Dispose(); }

public sealed class VoxelStructure : IDisposable             // behaviour in §5.1–§5.4
{
    public VoxelStructure(float cellSize = 0.5f);            // Allocator.Persistent; Palette gets entry 0
    public VoxelStructureData Data { get; }
    public float CellSize { get; }
    public uint Revision { get; }
    public bool IsReadOnly { get; }
    public bool IsPinned { get; }
    public int BrickCount { get; }
    public int SolidCellCount { get; }                       // Σ SolidCount, O(bricks)
    public bool TryGetCellBounds(out int3 min, out int3 max);
    public Cell GetCell(int3 cell);
    public void SetCell(int3 cell, Cell value);              // raw write (codec, editor internals, tests)
    public PlaceResult TryPlaceBlock(in BlockPlacement p, in VoxelContent content);
    public bool TryRemoveBlockAt(int3 cell, in VoxelContent content, out BlockPlacement removed);
    public bool TryGetBlockAt(int3 cell, out BlockPlacement block, out int instanceIndex);
    public ushort FindOrAddPalette(PaletteEntry e);          // returns 0 if full (32767 entries used)
    public void SetBrickDense(int3 coord, NativeArray<Cell> cells4096);   // replaces/creates a brick; deletes it if all air
    public void SetBrickUniform(int3 coord, Cell value);     // value must be solid
    public bool RemoveBrick(int3 coord);
    public void OptimizeStorage();                           // full identical dense bricks → uniform; frees pages
    public void Compact();                                   // §5.3
    public void SetReadOnly();                               // irreversible
    public void Pin(); public void Unpin();                  // PinCount ±1 (Unpin below 0 throws)
    public NativeArray<int> SortedBrickOrder(Allocator a);   // indices into Bricks sorted by (z, y, x)
    public void GetBlocks(NativeList<StructureBlock> output);// §5.4 canonical order
    public VoxelStructure Clone();                           // deep copy; not pinned, not read-only
    public static StructureDiff Diff(VoxelStructure from, VoxelStructure to, Allocator a);
    public StructureMassData GetMass(in VoxelContent content);   // cached by Revision
    public ulong ComputeContentHash(IBlockIdMap ids);            // StableHash of the VOXS payload
    public long NativeBytes { get; }                         // Σ capacities × element sizes of all containers
    public void Clear();
    public void Dispose();                                   // throws InvalidOperationException if pinned; second call is a no-op
}
```

### 4.4 Registry, hooks and requests (`Model/` types, `Grid/` services)

```csharp
// Grid/ (namespace Game.Voxel.Grid)
public interface IGridSpawnHook
{   /// Called by GridSpawnSystem on the main thread, inside its structural update, after it created
    /// brick or functional entities for `root` (root already carries every requester component).
    /// May make batch structural changes on `created` only.
    void OnEntitiesCreated(EntityManager em, Entity root, NativeArray<Entity> created);
}

public sealed class VoxelStructureRegistry : ScriptableObject // runtime instance only (CreateInstance, HideFlags.DontSave)
{
    public static VoxelStructureRegistry CreateRuntime(BlueprintLibrary blueprints);
    public int Register(VoxelStructure structure);           // handle ≥ 0; a handle is never reused in a session
    public void Release(int handle);                          // forgets the structure; does NOT dispose it (owner disposes)
    public VoxelStructure Get(int handle);                    // null if released or out of range
    public bool TryGetBlueprint(ContentHash blueprint, out VoxelStructure structure);   // via BlueprintLibrary
    public void AddSpawnHook(IGridSpawnHook hook);
    public void RemoveSpawnHook(IGridSpawnHook hook);
    public IReadOnlyList<IGridSpawnHook> SpawnHooks { get; }
}

public sealed class BlueprintLibrary : IDisposable            // owned by SPEC-01's composition root
{
    public BlueprintLibrary(ContentCatalog catalog, VoxelContent content, IBlockIdMap ids);
    public bool TryGet(ContentHash blueprint, out VoxelStructure structure);   // decodes on first use; read-only; cached
    public void ReleaseUnpinnedLarge();                       // disposes cached structures with > 65,536 cells and PinCount == 0
    public int CachedCount { get; }
    public void Dispose();                                    // disposes every cached structure
}

// Model/ — ECS components
public struct VoxelStructureRegistryRef : IComponentData { public UnityObjectRef<VoxelStructureRegistry> Value; }  // singleton, created by SPEC-01

public struct GridSpawnRequest : IComponentData              // architecture §6.3; the entity becomes the root
{
    public ContentHash Blueprint;          // non-zero: spawn from this blueprint (read-only source, WriteBack forbidden)
    public int StructureHandle;            // ≥ 0: spawn from this registered structure; -1 = none. Exactly one source is set
    public ulong PersistentId;             // copied to the root's PersistentId; 0 only for EditorPreview grids and tests
    public RigidTransform Pose;            // local frame; position of the grid origin (cell (0,0,0) min corner)
    public float3 LinearVelocity;          // m/s, world; Dynamic only
    public float3 AngularVelocity;         // rad/s, world; Dynamic only
    public GridKind Kind;
    public GridMotion Motion;
    public GridSpawnFlags Flags;
}
public struct GridRebuildRequest : IComponentData { public int StructureHandle; public GridSpawnFlags Flags; }   // on an existing root
public struct GridDespawnRequest : IComponentData, IEnableableComponent { }      // on every root, disabled; enable to despawn
public struct GridSpawning : IComponentData, IEnableableComponent { public int NextBrick; }   // enabled until every brick exists
public struct GridRebuilt : IComponentData, IEnableableComponent { }              // enabled by GridSpawnSystem; SPEC-12 consumes
public struct GridBodyInit : IComponentData, IEnableableComponent                 // enabled at spawn; SPEC-12 consumes and disables
{ public GridMotion Motion; public float3 LinearVelocity; public float3 AngularVelocity; }
public struct GridSource : IComponentData                    // on the root
{ public int StructureHandle; /* -1 for blueprint spawns */ public ContentHash Blueprint; public uint SyncedRevision; public GridSpawnFlags Flags; }
[InternalBufferCapacity(0)] public struct GridEditedBrick : IBufferElementData { public int3 Coord; }   // M3, on the root
public struct GridEditRequest : IComponentData, IEnableableComponent { }         // M3: enable after filling GridEditedBrick
```

### 4.5 Grid and brick components (`Model/`)

```csharp
public struct VoxelGrid : IComponentData                     // on the root (architecture §6.3)
{
    public int3 BrickMin, BrickDims;   // BrickSlot table bounds; BrickDims product ≤ 262,144
    public float CellSize;
    public uint Revision;              // writers: DamageResolveSystem, SplitSystem, GridSpawnSystem
    public int AnchorInstance;         // core instance index or -1 (largest island anchors); written at spawn, afterwards by SPEC-13
    public GridKind Kind;              // written at spawn, afterwards by ShipDeathSystem (SPEC-13)
    public uint Serial;                // session-unique creation number ≥ 1; deterministic ordering and SPEC-12 cache key
    public int3 CellMin, CellMax;      // inclusive solid bounds at spawn/rebuild; never shrunk at runtime (conservative)
}

[InternalBufferCapacity(0)] public struct BrickSlot : IBufferElementData { public Entity Brick; }  // dense table on the root

public struct Brick : IComponentData                         // 28 bytes
{
    public Entity Grid;
    public int3 Coord;
    public ushort SolidCount;          // 1..4096 while the entity lives; a writer that reaches 0 destroys the brick (SPEC-13, via ECB)
    public ushort Reserved;
    public uint ChangedRevision;
}

[InternalBufferCapacity(0)] public struct BrickCells : IBufferElementData { public Cell Value; }   // exactly 4096 entries

[Flags] public enum BrickDirtyFlags : byte { None = 0, Mesh = 1, Collider = 2 }

public struct BrickDirty : IComponentData, IEnableableComponent
{
    public BrickDirtyFlags Flags;
    public BlockFaceMask BorderMask;   // brick faces whose border layer changed → face neighbours need remeshing (SPEC-04 mask)
}

public struct FunctionalBlock : IComponentData { public Entity Grid; public int InstanceIndex; }   // architecture §6.3

public struct FunctionalComponentRegistration : IBufferElementData { public FunctionKind Kind; public ComponentType Type; }  // singleton buffer

public struct GridPrototypes : IComponentData                // singleton; internal to GridSpawnSystem, read by SPEC-13
{ public Entity Brick; public FixedList128Bytes<Entity> FunctionalByKind; /* index = (int)FunctionKind, 8 entries */ public uint NextSerial; public int RegistrationCount; }

public struct RetiredBlob { public BlobAssetReference<Collider> Blob; public uint DueFrame; }
public struct RetiredMesh { public UnityObjectRef<Mesh> Mesh; public BatchMeshID Id; public uint DueFrame; }
public struct DeferredDisposalQueue : IComponentData         // singleton; appended on the main thread only
{ public NativeList<RetiredBlob> Blobs; public NativeList<RetiredMesh> Meshes; public uint Frame; }

public struct VoxelConfig : IComponentData                   // singleton, created by SPEC-01 from VoxelConfigDefinition
{ public int SpawnBricksPerFrame; public byte RequireRenderer; public float DefaultCellSize; }
```

**Root archetype after conversion:** `VoxelGrid, BrickSlot[], PaletteEntry[], BlockInstance[], LocalTransform, LocalToWorld, LinkedEntityGroup[], PersistentId, GridSource, GridSpawning, GridDespawnRequest(off), GridRebuilt(off), GridBodyInit, GridEditRequest(off), GridEditedBrick[]` + SPEC-12 `GridMass, GridMassRecompute, GridColliderState` + SPEC-11 `Dormant(off)` + every requester component. SPEC-12 adds the Unity Physics components later (through the GameplayEnd ECB).
**Brick archetype:** `Brick, BrickCells[4096], BrickSolidMask[64], BrickDirty(off), LocalTransform, LocalToWorld, Parent` + SPEC-11 `BrickRenderState, BrickLodDesired, BrickPropInstance[]` + Entities Graphics components (unless `NoRender`) + `StarSystemMember` via the hook. `LocalTransform = { Position = Coord·16·CellSize, Rotation = identity, Scale = CellSize }`: brick meshes are in cell units.
**Functional archetype:** `FunctionalBlock, LocalTransform, LocalToWorld, Parent` + registered types + Entities Graphics components + `StarSystemMember` via the hook. `LocalTransform = { Position = (Origin + 0.5)·CellSize, Rotation = OrientationTablesBlob.Rotations[Orient], Scale = CellSize }` (prop meshes are pivoted at the root cell centre, 1 unit = 1 cell).

### 4.6 Helpers (`Grid/`)

```csharp
public struct GridSpawnParams
{ public RigidTransform Pose; public float3 LinearVelocity, AngularVelocity; public GridKind Kind; public GridMotion Motion; public GridSpawnFlags Flags; public ulong PersistentId; }

public static class GridSpawnApi                             // managed callers (App, Galaxy, ShipEditor, tests)
{
    public static Entity RequestSpawn(EntityManager em, VoxelStructureRegistry registry, int structureHandle, in GridSpawnParams p);
    public static Entity RequestSpawn(EntityCommandBuffer ecb, VoxelStructureRegistry registry, int structureHandle, in GridSpawnParams p); // BeginInitialization ECB
    public static Entity RequestSpawnBlueprint(EntityManager em, ContentHash blueprint, in GridSpawnParams p);     // WriteBack is cleared
    public static Entity RequestSpawnBlueprint(EntityCommandBuffer ecb, ContentHash blueprint, in GridSpawnParams p);
    public static void RequestRebuild(EntityManager em, VoxelStructureRegistry registry, Entity root, int structureHandle, GridSpawnFlags flags);
    public static void RequestDespawn(EntityManager em, Entity root);          // enables GridDespawnRequest
    public static void RequestEdit(EntityManager em, Entity root, NativeArray<int3> changedBrickCoords);   // M3
}

public static class GridStructureSync                        // main thread, outside ECS system updates, or inside GridSpawnSystem
{
    public static int WriteBack(EntityManager em, Entity root, VoxelStructureRegistry registry);      // into the source; returns bricks copied
    public static int CaptureChanges(EntityManager em, Entity root, VoxelStructure changedBricks, NativeList<int3> removedBricks); // since spawn
    public static void WriteBackAll(EntityManager em, EntityQuery roots, VoxelStructureRegistry registry);  // SPEC-31 calls before unloading
}

public static class BrickWrite                               // Burst; the cell write path for SPEC-13 and GridEditRequest
{
    public static Cell Get(DynamicBuffer<BrickCells> cells, int localIndex);
    /// Writes one cell; maintains mask, SolidCount, ChangedRevision and dirty flags. Returns true if anything changed.
    public static bool Set(ref Brick brick, DynamicBuffer<BrickCells> cells, DynamicBuffer<BrickSolidMask> mask, ref BrickDirty dirty,
                           int localIndex, Cell value, uint revision);
}

public struct GridCellReader                                 // read-only job helper; every lookup is [ReadOnly]
{
    [ReadOnly] public ComponentLookup<VoxelGrid> Grids; [ReadOnly] public BufferLookup<BrickSlot> Slots;
    [ReadOnly] public BufferLookup<BrickCells> Cells; [ReadOnly] public BufferLookup<BrickSolidMask> Masks;
    public static GridCellReader Create(ref SystemState state);   // call in OnCreate
    public void Update(ref SystemState state);                    // call first thing in OnUpdate
    public Entity GetBrick(Entity grid, int3 brickCoord);         // Entity.Null if outside or absent
    public bool TryGetCell(Entity grid, int3 cell, out Cell value);   // false and air if absent
    public bool IsSolid(Entity grid, int3 cell);                  // mask bit test
}
```

`VoxelRaycast`, `ICellSolidQuery`, `VoxelRayHit` (§5.5), `VoxelStructureCodec`, `IBlockIdMap`, `VoxelCodecException` (§5.6), `MassAccumulator` (§5.4), `BenchmarkStructures` (§5.11) and `DeferredDisposal` (§5.10) are specified with their algorithms.

### 4.7 Definitions (`Assets/Game/Data/Definitions/Voxel/`, namespace `Game.Data.Definitions`)

```csharp
[ContentIdPrefix("ship_")]
[CreateAssetMenu(menuName = "Game/Content/Ship Blueprint", fileName = "ship_new")]
public sealed class ShipBlueprintDefinition : ContentDefinition
{
    [SerializeField] private string displayName;
    [SerializeField] private float cellSize = 0.5f;          // > 0
    [SerializeField] private int payloadVersion = 1;         // VOXS payload layout version
    [SerializeField, HideInInspector] private byte[] payload; // VoxelStructureCodec output; written by SPEC-72 and SPEC-51 export
    [SerializeField] private int cellCount;                  // cached summary for UI and validation
    [SerializeField] private Vector3Int boundsMin, boundsMax;
    public string DisplayName => displayName; public float CellSize => cellSize; public int PayloadVersion => payloadVersion;
    public ReadOnlySpan<byte> Payload => payload; public int CellCount => cellCount;
    public Vector3Int BoundsMin => boundsMin; public Vector3Int BoundsMax => boundsMax;
    public override void Validate(ValidationScope scope, ContentValidationContext context);  // VOX-E001..E003 (§7)
#if UNITY_EDITOR
    public void EditorSetPayload(byte[] bytes, int cells, Vector3Int min, Vector3Int max);
#endif
}

[ContentIdPrefix("config_")]
public sealed class VoxelConfigDefinition : ContentDefinition      // single instance: config_voxel
{
    [SerializeField, Min(64)] private int spawnBricksPerFrame = 1024;
    [SerializeField, Min(0.05f)] private float defaultCellSize = 0.5f;
    public int SpawnBricksPerFrame => spawnBricksPerFrame; public float DefaultCellSize => defaultCellSize;
}
```

`VoxelConfigFactory.Create(VoxelConfigDefinition def) → VoxelConfig` (`Grid/`) copies the values and sets `RequireRenderer = 1`.

## 5. Systems, services and flow

### 5.1 Structure storage algorithms (`VoxelStructure`)

`GetCell(c)`: `b = BrickOf(c)`; `BrickIndex.TryGetValue(b)` → none: air. Record `r`: `r.Page < 0` → `r.Uniform`, else `CellPages[r.Page*4096 + LocalIndex(LocalOf(c))]`. O(1), 0 allocations.

`SetCell(c, v)` (throws `InvalidOperationException` if pinned or read-only):
1. Look up the record. If none and `v` is air → return (no revision bump). If none → take a page (pop `FreePages`, else grow `CellPages` by 4,096 default cells), add `BrickRecord{Coord, Page, SolidCount 0}`, a zeroed mask, a map entry.
2. If the record is uniform and `v != Uniform` → densify: take a page, fill it with 4,096 copies of `Uniform`, `Page = page`.
3. `old = cell`; equal → return. Write. Update the mask bit and `SolidCount` (±1 when solidity changes).
4. If `SolidCount == 0` → delete the brick: push its page to `FreePages`, swap-remove the record and mask, fix the moved record's `BrickIndex` entry, remove the map entry.
5. `Header.Revision++`, `record.ChangedRevision = Revision`, `BoundsValid = 0`.

`OptimizeStorage()`: for each dense record with `SolidCount == 4096`: compare its 4,096 cells with cell 0; all equal → `Page = -1`, `Uniform = cell 0`, push the page to `FreePages`. O(full bricks × 4,096). Does not bump `Revision`. Called by `Compact`, by codec decode, by `BenchmarkStructures` and by `GridStructureSync.WriteBack` for the bricks it wrote.

Bounds (`TryGetCellBounds`): if `BoundsValid == 0`, recompute from every record's `Masks[i].TryGetBounds` + `Coord*16`; O(bricks × 64 words).

`NativeBytes`: Σ over containers of `Capacity × UnsafeUtility.SizeOf<T>()` (+ `BrickIndex.Capacity × 20`).

### 5.2 Block placement and removal

`TryPlaceBlock(p, content)` (checks in this order; no partial writes on failure):
1. Read-only → `ReadOnly`. `p.Block == 0` or `p.Block ≥ Blocks.Length` → `InvalidBlock`. `def.AllowedOrientations` lacks `p.Orient` → `OrientationNotAllowed`.
2. Footprint: `BlockPlacementMath.GetCells(ref def, ref ot, p.Orient, p.Origin, temp)` (`Allocator.Temp`). Any cell component outside `[-1,048,576, 1,048,575]` → `OutOfRange`. Any footprint cell not air → `Occupied`.
3. `!def.RequiresInstance`: `idx = FindOrAddPalette({p.Block, p.Orient, p.Color})`; `idx == 0` → `PaletteFull`; `SetCell(origin, Cell.Palette(idx))`.
4. Else: slot = pop `FreeInstances`, or append (an append that would create index 32,768 → `InstancesFull`); write `BlockInstance{Block, Orient, Color, Origin, FunctionHp = def.FunctionHp, Functional = Null, CellsAlive = def.Cells.Length, Flags 0}`; for footprint cell k (SPEC-04 order, root first): `SetCell(cell_k, Cell.Instance(slot, 255, k == 0 ? InstanceRoot : 0))`.
5. Return `Ok`.

`FindOrAddPalette(e)`: linear search of entries 1..Count−1 for an equal entry (palettes stay below ~2,000 entries; O(P)); else append while `Count ≤ 32767`; else return 0.

`TryRemoveBlockAt(cell)`: air → false. Palette cell → `removed = {entry.Block, cell, entry.Orient, entry.Color}`; `SetCell(cell, air)`. Instance → recompute the footprint from the instance; set every footprint cell that still references this instance to air; `Instances[i] = default` (tombstone); push `i` to `FreeInstances`.

`TryGetBlockAt(cell)`: palette → placement with origin = cell, `instanceIndex = -1`; instance → its placement and index.

### 5.3 Compaction (`Compact()`)

Deterministic; used before encoding blueprints and by the editor before save. Must be called on a non-pinned, non-read-only structure.
1. `order = SortedBrickOrder`. Scan cells of all bricks in `order` (uniform: one check) and mark used palette entries. Palette remap: entry 0 stays 0; used entries get new indices 1..U in order of first use.
2. Instance remap: live instances (`Block != 0`) get new indices 0..L−1 in ascending old index.
3. Rewrite every cell `Ref` through the maps; rebuild `Palette`, `Instances`; clear `FreeInstances`.
4. Repack: new `CellPages` holds the dense bricks in `order`; uniform bricks keep `Page = -1`; clear `FreePages`; reorder `Bricks`/`Masks` to `order`; rebuild `BrickIndex`; trim every capacity to its length.
5. `Revision++`; every record's `ChangedRevision = Revision`.
Complexity O(cells).

### 5.4 Clone, diff, blocks, mass, hash

- `Clone()`: allocate containers with exact capacities; `CopyFrom` each list and the map; new header with `PinCount 0`, `ReadOnly 0`.
- `GetBlocks(out)`: for bricks in `SortedBrickOrder`, local index 0..4,095 (uniform: the same cell 4,096 times): each palette cell → `StructureBlock{placement, -1}`. Then each live instance in index order → `StructureBlock{placement, index}`.
- `Diff(from, to)`: `StructureDiffJob` (Burst, `IJob`). (a) Palette blocks: for every brick coord present in either structure (union, sorted (z,y,x)), compare cell by cell the resolved `(Block, Orient, Color)` of palette cells (instance cells and air count as "none"); differences go to `Removed` (from's block) and `Added` (to's block). Dense pages are first compared with `UnsafeUtility.MemCmp`; equal pages with element-wise equal palettes are skipped. (b) Instances: multiset difference keyed by `(Block, Origin, Orient, Color)` (sort both lists, then merge). Output sorted by `(Origin.z, Origin.y, Origin.x, Block, Orient, Color)`. HP and flags are ignored. O(cells of both).
- `MassAccumulator` (static, Burst): `AddCell(ref StructureMassData d, int3 cell, float cellSize, float mass)` adds `m`, `m·p`, `m·p²` per axis, `m·(px·py, py·pz, pz·px)` and `m·s²/6` with `p = CellCenter(cell)` in double; `RemoveCell` subtracts the same terms; `Add(ref a, in b)` sums two accumulators. `GetMass(content)`: if `MassRevision == Revision` return the cache; else `StructureMassJob : IJobParallelFor` over bricks writes one `StructureMassData` per brick, then a serial sum in `SortedBrickOrder` (bit-identical across runs). Cell mass = `def.CellMass` of the cell's block.
- `ComputeContentHash(ids)`: `StableHash.Hash64` of the bytes from `VoxelStructureCodec.Encode` (no compaction).

### 5.5 Grid-space DDA (`VoxelRaycast`, `Model/`)

```csharp
public interface ICellSolidQuery { bool IsSolid(int3 cell); bool BrickExists(int3 brick); }
public struct VoxelRayHit { public int3 Cell; public byte EnteredFace; /* BlockFace, 255 = started inside */ public float DistanceCells; }
public static class VoxelRaycast
{   // originCells and dir in grid cell units (grid-local metres / CellSize); dir need not be normalized
    public static bool Cast<T>(ref T query, float3 originCells, float3 dir, float maxCells, out VoxelRayHit hit) where T : struct, ICellSolidQuery;
}
public struct StructureCellQuery : ICellSolidQuery { public VoxelStructureData Data; }          // read-only use
public struct GridCellQuery : ICellSolidQuery { public GridCellReader Reader; public Entity Grid; }
```
Algorithm (Amanatides–Woo):
1. Normalize `dir`; a zero vector returns false.
2. `cell = floor(origin)`; if solid → hit at distance 0, `EnteredFace = 255`.
3. Per axis: `step = sign(dir)`, `tDelta = |1/dir|`, `tMax` = distance to the first boundary.
4. Loop while `t ≤ maxCells`: if `!BrickExists(BrickOf(cell))`, keep stepping without solid tests until the brick changes (empty-brick skip); else step one cell (smallest `tMax`; ties step X before Y before Z) and test `IsSolid`. On a hit, `EnteredFace` is the face opposite to the step (stepping +X enters through `NegX`).
5. `t > maxCells` → false.
Complexity ≤ `3·maxCells` steps; 0 allocations.

### 5.6 VOXS codec (`VoxelStructureCodec`, `Model/`)

```csharp
public interface IBlockIdMap
{   // implemented over ContentCatalog (CatalogBlockIdMap, Grid/) and by tests
    ContentId GetId(ushort blockIndex);
    ushort ResolveOrMissing(ContentId id, string context);   // records unresolved; returns BlockDefsBlob.MissingBlockIndex
}
public static class VoxelStructureCodec
{
    public const int LayoutVersion = 1;
    public static void Encode(VoxelStructure s, IBlockIdMap ids, NativeList<byte> output);
    public static VoxelStructure Decode(ReadOnlySpan<byte> payload, IBlockIdMap ids, string context);  // throws VoxelCodecException on malformed data
}
```
SPEC-05 §VOXS is the normative byte layout. Until SPEC-05 publishes it, implement Appendix A; if SPEC-05 differs, SPEC-05 wins. Layout-independent requirements: bricks in `SortedBrickOrder`; palette and instances written as stored (tombstones included) so decode → encode is byte-identical; block IDs written as `ContentId` strings, never `BlockIndex`; decode rebuilds masks, counts and `BrickIndex`, then calls `OptimizeStorage()`; unknown IDs map to the Missing block with a warning (never reject, ADR-0007). Encoding runs on a worker thread when called by SPEC-05 (it touches no Unity objects).

### 5.7 `GridSpawnSystem` (`Grid/`)

- **Group:** `GameInitGroup`, `[UpdateAfter(typeof(PlayerInputSampleSystem))]` (last in the group). **The module's only structural system; main thread.** `OnUpdate` is not Burst-compiled (it dereferences `UnityObjectRef` and calls `RenderMeshUtility`); all per-brick work runs in Burst jobs completed inside the system (allowed: init group, not fixed step).
- **Requires:** `GameplayActive`, `VoxelConfig`, `BlockDefsRef`, `OrientationTablesRef`, `VoxelStructureRegistryRef`.
- **Singletons (`OnCreate`):** `VoxelSingletons.CreateAll(EntityManager)` creates one entity holding `GridPrototypes{NextSerial = 1}`, `DeferredDisposalQueue` (lists of capacity 1,024, `Allocator.Persistent`), the `FunctionalComponentRegistration` buffer, and the REN/PHY singletons through `BrickMeshQueue.Create()`, `BrickMeshBatch.Create()`, `VoxelRenderRuntime.Create()`, `VoxelViewer` (default value), `ColliderDirtyQueue.Create()`, `ColliderBuildCache.Create()`, `ColliderBatchQueue.Create()` (SPEC-11/12 contract methods whose exact bodies those specs give). `OnDestroy`: `state.CompleteDependency()`, dispose every remaining `DeferredDisposalQueue` blob, then `VoxelSingletons.DisposeAll`.
- **Reads:** requests, `VoxelConfig`, block blobs, `FunctionalComponentRegistration`, SPEC-11 `VoxelRenderRuntime`. **Writes:** everything it creates; `VoxelGrid.Revision` at spawn/rebuild/edit; appends to SPEC-11 `BrickMeshQueue` (spawn, rebuild, edit) and SPEC-12 `ColliderDirtyQueue` (edit only; spawn/rebuild collider work is enqueued by SPEC-12's body init).

Per frame (`budget = VoxelConfig.SpawnBricksPerFrame`; markers `GridSpawnSystem.<Step>`):
1. **Prototypes** (`Prototypes`): if `RequireRenderer == 1` and `VoxelRenderRuntime.IsReady == 0` → return (requests wait). If prototypes are missing or `RegistrationCount` changed: brick prototype = `CreateEntity(brickBaseArchetype)` (Brick, BrickCells, BrickSolidMask, BrickDirty, LocalTransform, LocalToWorld, Parent, BrickLodDesired, BrickPropInstance, Prefab) → `RenderMeshUtility.AddComponents(e, em, new RenderMeshDescription(ShadowCastingMode.On, receiveShadows: true), new MaterialMeshInfo(runtime.BrickMaterial, runtime.PlaceholderMesh))` (one shared `RenderMeshArray`-free registration; never a per-brick array) → disable `MaterialMeshInfo`, disable `BrickDirty`. One functional prototype per `FunctionKind` 1..7 = base functional types + the registered types for that kind + render components with `runtime.PropMaterial`. `NoRender` grids use render-free variants (created on demand).
2. **Despawns** (`Despawn`; roots with `GridDespawnRequest` enabled, sorted by `VoxelGrid.Serial`): `EntityManager.CompleteAllTrackedJobs()` once if any; per root: if `GridSource.Flags & WriteBack` → `GridStructureSync.WriteBack`; if `GridSpawning` still enabled → unpin the source; then `DestroyEntity(NativeArray roots)` in one batch (LEG cascade). Not budgeted.
3. **Rebuilds** (`Rebuild`; roots with `GridRebuildRequest`, sorted by Serial): `DestroyEntity` (batch) of all brick entities from `BrickSlot` and all `BlockInstance.Functional`; LEG := `[root]`; pin the new source, unpin the old; set `GridSource`; recompute `BrickMin/Dims/CellMin/CellMax/AnchorInstance`; resize and zero `BrickSlot`; copy palette and instances; `Revision++`; enable `GridSpawning{0}` and `GridRebuilt`; create functional entities (step 5c); `RemoveComponent<GridRebuildRequest>` (batch). Bricks follow in step 4.
4. **Continue spawning** (`CreateBricks`; roots with `GridSpawning` enabled, sorted by Serial) while `budget > 0`: `n = min(budget, source.Bricks.Length − NextBrick)`:
   a. `Instantiate(brickPrototype, n, Allocator.Temp)`, then `AddComponent<BrickRenderState>(entities)` (Instantiate strips cleanup types).
   b. `BrickFillJob : IJobParallelFor` (inner batch 8) over the n records: `Brick{Grid, Coord, SolidCount, ChangedRevision 0}`; `BrickCells.ResizeUninitialized(4096)` then dense → `UnsafeUtility.MemCpy` 16,384 B from the page, uniform → `UnsafeUtility.MemCpyReplicate` of the uniform cell 4,096 times; `BrickSolidMask` 64 words from `Masks[i]`; `LocalTransform`; `Parent = root`. Complete it (init group, bounded by the budget).
   c. Write `BrickSlot[SlotIndex(coord)] = entity`; append the entities to the LEG; enqueue `BrickMeshQueue` requests with priority `Spawn` if the queue exists.
   d. Call every `IGridSpawnHook.OnEntitiesCreated(em, root, entities)`.
   e. `NextBrick += n; budget -= n`. When `NextBrick == source.Bricks.Length`: disable `GridSpawning`, unpin the source; if the source came from a blueprint, call `BlueprintLibrary.ReleaseUnpinnedLarge()`.
5. **Start new requests** (`Convert`; entities with `GridSpawnRequest`, sorted by `Entity.Index` then `Version`): the first request of the frame is always converted (so large grids cannot starve), later ones only while `budget > 0`:
   a. Resolve the source: `StructureHandle ≥ 0` → `registry.Get`; else `registry.TryGetBlueprint(Blueprint)`. Validate: exactly one source set; structure found, not disposed, ≥ 1 brick; brick dims product ≤ 262,144; `CellSize > 0`; `Blueprint` source with `WriteBack` → the flag is cleared with one warning. Failure → `GameLog.Error("Voxel", "spawn rejected: <reason>")`, `DestroyEntity(request)`, continue. `PersistentId == 0` without `EditorPreview` → `GameLog.WarnOnce` (grid is not saveable) and continue. Pin the source.
   b. `ConvertToRoot`: `AddComponent(entity, ComponentTypeSet)` for the root archetype of §4.5 in at most 3 sets; set `VoxelGrid{BrickMin, BrickDims, CellSize, Revision = 1, AnchorInstance = lowest live instance with def.IsCore else -1, Kind, Serial = NextSerial++, CellMin, CellMax}`, `LocalTransform{Pose.pos, Pose.rot, 1}`, `PersistentId{request.PersistentId}`, `GridSource{handle or -1, Blueprint, SyncedRevision = 1, Flags}`, `GridBodyInit{Motion, velocities}` (enabled), `GridSpawning{0}` (enabled), palette and instance buffers (`AddRange`), `BrickSlot` resized to the dims product and zeroed, LEG = `[root]`; remove `GridSpawnRequest`.
   c. Functional entities (`CreateFunctional`): for each live instance with `def.Kind == Functional`, grouped by `Function.Kind` in instance-index order: `Instantiate(prototype, count)`; `FunctionalFillJob` sets `FunctionalBlock{root, index}`, `LocalTransform` (§4.5), `Parent = root`, and when rendering `MaterialMeshInfo(PropMaterial, PropMeshIds[def.PropMeshIndex])` (enabled) and `RenderBounds = PropMeshBounds[def.PropMeshIndex]`; write `Instances[index].Functional`; append to the LEG; call the hooks. Prop blocks get no entity.
   d. Continue with step 4 for this root in the same frame.
6. **Edits** (M3, VOX-020): roots with `GridEditRequest` enabled → §5.9.

Complexity per frame: O(created bricks) memory copy (≤ 16.8 MB at 1,024 bricks) + O(functional blocks of converted requests). Exceptions are not caught; invalid requests are destroyed with one error log.

### 5.8 `BrickDirtyScheduleSystem` (`Grid/`)

- **Group:** `GameplayPostPhysicsGroup`, `[UpdateAfter(typeof(GridMassUpdateSystem))]`, `[UpdateBefore(typeof(GameplayEndCommandBufferSystem))]`. Burst `ISystem`. Requires `GameplayActive`.
- **Reads:** `Brick`, `BrickSlot`, `VoxelGrid`. **Writes:** `BrickDirty` (consumer: clears and disables), appends to `BrickMeshQueue` (SPEC-11) and `ColliderDirtyQueue` (SPEC-12).
- Per tick:
  1. If no brick has `BrickDirty` enabled (query `IsEmpty` check on the enableable query — no sync), return.
  2. Schedule `BrickDirtyDrainJob : IJobChunk` with **`Schedule` (one worker)** over bricks with `BrickDirty` enabled, chained to `state.Dependency`, so chunk order — and therefore queue order — is deterministic.
  3. Per brick: `grid = Brick.Grid`, `serial = VoxelGrid[grid].Serial`.
     - `Flags & Collider` → `ColliderDirtyQueue.Enqueue(serial, grid, brick, coord, ColliderPriority.Damage)` (the queue dedupes).
     - `Flags & Mesh` and `SolidCount > 0` → `BrickMeshQueue.Enqueue(brick, grid, MeshPriority.Damage)`.
     - For each face `f` set in `BorderMask`: `n = BrickSlot[SlotIndex(coord + FaceDirection(f))]`; if `n != Null` → `BrickMeshQueue.Enqueue(n, grid, MeshPriority.Neighbour)`.
     - `Flags = None`, `BorderMask = None`, disable `BrickDirty`.
  4. Queue singletons are taken with `SystemAPI.GetSingletonRW` (ECS orders their other users through the dependency).
- Complexity O(dirty bricks); 0 B GC.

### 5.9 Grid edits (M3, `GridEditRequest`)

For `EditorPreview` grids only (SPEC-51 is the single cell writer for them; SPEC-13 skips them). The editor mutates its draft `VoxelStructure` (registered in the registry), appends the changed brick coords to `GridEditedBrick`, and enables `GridEditRequest` via `GridSpawnApi.RequestEdit`. `GridSpawnSystem` step 6:
1. If the source's cell bounds left `BrickMin..BrickMin+BrickDims-1` → reallocate `BrickSlot` with the new min/dims and move every entry to its new slot index.
2. Copy palette and instance buffers from the source when their lengths differ or a `MemCmp` finds a difference.
3. For each edited coord (deduplicated, sorted (z,y,x)): source has the brick, slot empty → create one brick (step 4 path, n = edited count batch); source has it, slot set → copy cells, mask and `SolidCount` into the entity; source lacks it, slot set → collect for batch destroy and clear the slot. Enqueue meshes (`Damage` priority) for the brick and its 6 face neighbours and collider work (`Damage`).
4. Functional entities: instantiate for new functional instances, batch-destroy for instances that became tombstones.
5. `VoxelGrid.Revision++`; clear `GridEditedBrick`; disable `GridEditRequest`.
Cost: O(edited bricks); one place/remove touches 1–8 bricks.

### 5.10 `DeferredDisposalSystem` (`Grid/`)

- **Group:** `[UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]`. `ISystem`; `OnUpdate` is not Burst-compiled (it calls `EntitiesGraphicsSystem.UnregisterMesh` and `Object.Destroy`). Requires `DeferredDisposalQueue`.
- Per frame (marker `DeferredDisposalSystem.Free`):
  1. `q.Frame++`.
  2. Every `RetiredBlob` with `DueFrame ≤ Frame`: `Blob.Dispose()`; swap-back remove.
  3. Every `RetiredMesh` with `DueFrame ≤ Frame`: `entitiesGraphics.UnregisterMesh(Id)` (skip when `Id == BatchMeshID.Null` or the system is absent), `Object.Destroy(Mesh.Value)`; swap-back remove.
- Producers call the static helpers `DeferredDisposal.RetireBlob(ref DeferredDisposalQueue q, BlobAssetReference<Collider> b)` and `DeferredDisposal.RetireMesh(ref q, UnityObjectRef<Mesh> mesh, BatchMeshID id)` on the main thread; both set `DueFrame = q.Frame + 3` (architecture §11.3, §11.8: at least 3 frames).
- Complexity O(retired items). Teardown: `GridSpawnSystem.OnDestroy` disposes remaining blobs; meshes are destroyed with the scene.

### 5.11 Benchmark structures (`BenchmarkStructures`, `Model/`)

All take the `BlockIndex` of a single-cell Structural Cube block and colour 0; deterministic, no RNG; built with `SetBrickDense`/`SetBrickUniform` per brick, then `OptimizeStorage`.

| Name | Definition | Cells | Bricks |
|---|---|---|---|
| `HollowStation1M` (reference) | box x∈[0,256), y∈[0,128), z∈[0,256) minus x∈[4,252), y∈[4,124), z∈[4,252) | 1,008,128 | 872 |
| `ThinShellStation1M` (stress) | box [0,512)×[0,256)×[0,512) minus [1,511)×[1,255)×[1,511) | 1,043,464 | 3,784 |
| `SolidAsteroid1M` | cells whose centre lies within 62.0 cells of (64,64,64) | brute-force count (≈ 998,000) | 245 |
| `Ship20k` | solid box 80×10×25 at the origin | 20,000 | 18 |
| `Lattice2500` | one cell at local (0,0,0) of each brick bx∈[0,25), by∈[0,4), bz∈[0,25) | 2,500 | 2,500 |

### 5.12 Flows

```mermaid
sequenceDiagram
    participant REQ as Requester (App/Galaxy/Editor)
    participant API as GridSpawnApi
    participant GS as GridSpawnSystem (Init)
    participant HK as IGridSpawnHook (SPEC-31)
    participant DS as BrickDirtyScheduleSystem (FixedStep post)
    participant PHY as SPEC-12
    participant REN as SPEC-11
    REQ->>API: RequestSpawn(handle or blueprint hash, params) → entity (+ Allegiance, StarSystemMember, …)
    GS->>GS: resolve source via registry, pin, convert to root (+PersistentId), functional entities
    GS->>GS: ≤1,024 bricks/frame (Instantiate batch + Burst fill)
    GS->>HK: OnEntitiesCreated(root, created) → StarSystemMember on bricks/functional
    GS->>REN: BrickMeshQueue (Spawn)
    PHY->>PHY: first tick after GridSpawning is off: provisional collider, mass, body components (GameplayEnd ECB)
    Note over DS: later, SPEC-13 writes cells with BrickWrite.Set and sets BrickDirty
    DS->>REN: BrickMeshQueue (Damage, Neighbour)
    DS->>PHY: ColliderDirtyQueue (Damage)
```

## 6. Public API

| Call | Pre | Post / errors |
|---|---|---|
| `registry.Register(structure)` | Structure not disposed | Handle ≥ 0; the owner keeps ownership and must `Release` before disposing |
| `GridSpawnApi.RequestSpawn(em, registry, handle, p)` | Main thread outside system updates; handle valid | Returns the request entity (future root); source `Pin()`; caller may add components before the next frame. Mutating the source before `GridSpawning` turns off throws |
| `RequestSpawn(ecb, registry, handle, p)` | Inside a system: ecb from `BeginInitializationEntityCommandBufferSystem` | Same; the entity exists after the next playback |
| `RequestSpawnBlueprint(em/ecb, hash, p)` | — | Unknown hash → spawn rejected with an error log in `GridSpawnSystem`; `WriteBack` is cleared |
| `RequestDespawn(em, root)` | Root alive | Despawned in the next `GridSpawnSystem` update; write-back first when `WriteBack` |
| `GridStructureSync.WriteBack(em, root, registry)` | Main thread, not during another system's update | Changed bricks copied into the source; bricks destroyed at runtime removed from it; instance `FunctionHp/Flags/CellsAlive` copied; `SyncedRevision = Revision`; returns bricks copied; returns 0 without `WriteBack` |
| `BrickWrite.Set(...)` | Caller is a cell writer (architecture §8) with RW access to the brick | Maintains mask, `SolidCount`, `ChangedRevision`; sets `Mesh` on any change, `Collider` on a solidity change, and `BorderMask |= BorderFaces(i)` on a solidity change or an Hp-tier change (tier = `Hp >> 6`); caller enables `BrickDirty`, bumps `VoxelGrid.Revision` once per tick, and destroys the brick through the GameplayEnd ECB when `SolidCount` reaches 0 |
| `VoxelRaycast.Cast` | `maxCells ≥ 0` | Deterministic first solid cell; false if none |
| `VoxelStructureCodec.Decode` | — | Never rejects unknown IDs; throws `VoxelCodecException` on truncated or invalid data |

Registration of functional component types (SPEC-20/21/22): a system with `[CreateAfter(typeof(GridSpawnSystem))]` appends `FunctionalComponentRegistration{Kind, ComponentType.ReadWrite<T>()}` to the singleton buffer in its `OnCreate` (a buffer append, not a structural change). Registered components start zero-initialised; the registering spec fills them in its own `GameInitGroup` system ordered after `GridSpawnSystem`.

## 7. Edge cases and failure behaviour

| Situation | Required behaviour |
|---|---|
| Spawn request with an empty, disposed, unknown or released source | One `GameLog.Error("Voxel", "spawn rejected: <reason>")`, request entity destroyed, source unpinned if pinned |
| Both or neither of `Blueprint`/`StructureHandle` set | Rejected as above (reason `source`) |
| Grid larger than 262,144 brick slots | Rejected as above (reason `too large`) |
| Structure mutated while pinned | `InvalidOperationException` from the mutator |
| Blueprint structure mutated | Mutators return `PlaceResult.ReadOnly`; raw writes throw |
| `registry.Release` of a structure still used by a live grid's `GridSource` | Allowed; later write-back of that grid logs `GameLog.Warn` and skips |
| Despawn while `GridSpawning` | Unpin, write back the bricks that exist, destroy |
| Brick reaches `SolidCount == 0` in a fixed tick | The writer destroys it through the GameplayEnd ECB; `BrickDirtyScheduleSystem` still enqueues collider work and neighbour remeshes and skips its own mesh |
| Footprint crosses brick borders | `SetCell` creates bricks as needed |
| Instance count would exceed 32,768 | `PlaceResult.InstancesFull`; decode of such data throws `VoxelCodecException` |
| Palette full (32,767) | `PlaceResult.PaletteFull`; callers may `Compact()` and retry |
| Decode meets an unknown block ID | Missing block via `IBlockIdMap.ResolveOrMissing`; one warning per ID |
| `GridCellReader` asked for a cell outside `BrickDims` | Air, no exception |
| Ray origin inside a solid cell | Hit at distance 0, `EnteredFace = 255` |
| No registrations for a `FunctionKind` | Base functional archetype; no error |
| `RequireRenderer == 1` and no ready renderer for 300 consecutive frames | `GameLog.WarnOnce("Voxel", "spawn waiting for renderer")`; keep waiting |
| Hook throws | Exception propagates (bug); hooks must not throw |
| Second Play with domain reload disabled | All state is in world singletons and the runtime registry instance; nothing static |
| `ShipBlueprintDefinition` invalid | `VOX-E001` payload missing/empty, `VOX-E002` `cellSize ≤ 0`, `VOX-E003` `payloadVersion > LayoutVersion` |

## 8. Performance budget and memory model

### 8.1 Gates owned (06)

| Gate | Target | Test (task) |
|---|---|---|
| 1M-cell station idle, all voxel systems | ≤ 2 ms main thread | `VoxelGatePerfTests.IdleStation1M_VoxelMainThreadUnder2ms` (VOX-018) |
| Memory, reference station (cells + masks + meshes + colliders) | ≤ 40 MB | `VoxelGatePerfTests.HollowStation1M_MemoryUnder40MB` (VOX-018) |
| Spawn a 1M-cell grid | no frame > 8 ms voxel work; meshed and collidable ≤ 30 frames | `VoxelGatePerfTests.SpawnStation1M_NoFrameOver8ms_ReadyWithin30Frames` (VOX-018) |
| Moving 2,500 bricks / remesh / collider per brick | 0.5 ms / 60 µs / 150 µs | SPEC-11 / SPEC-11 / SPEC-12 |

Own budgets: `GridSpawnSystem` ≤ 6 ms main thread in a frame creating 1,024 bricks (`GridSpawnPerfTests.Create1024Bricks_Under6ms`); `BrickDirtyScheduleSystem` ≤ 0.05 ms for 64 dirty bricks; `VoxelStructure.GetCell` ≤ 20 ns; `Diff` of two 100k-cell structures ≤ 10 ms; `Decode` of `HollowStation1M` ≤ 150 ms; `WriteBack` of an unchanged 1M grid ≤ 0.2 ms (O(bricks) scan).

### 8.2 Memory model (architecture §11.10)

Bytes per item (VOX-002 measures every row; estimates marked ≈):

| Item | Bytes | Where |
|---|---|---|
| Cell | 4 | `BrickCells` |
| ECS dense brick, heap | 16,384 cells + 512 mask = 16,896 | outside the chunk (capacity 0) |
| ECS brick, chunk data | ≈ 300 (Brick 28, LocalTransform 32, LocalToWorld 64, Parent 8, BrickDirty 2, buffer headers 2×16, BrickRenderState 24, BrickLodDesired 1, Entities Graphics ≈ 110) | chunk |
| Structure dense brick | 16,384 page + 512 mask + 32 record + ≈ 20 map = 16,948 | `VoxelStructure` |
| Structure uniform brick | 564 | `VoxelStructure` |
| Brick mesh vertex (SPEC-11 packed) | 8 | GPU; CPU copy released per SPEC-11 |
| Brick mesh index | 2 | GPU |
| Exposed face, welded flat wall | ≈ 1.2 vertices × 8 + 6 × 2 = ≈ 21.6 | SPEC-11 |
| Collider box (`BoxCollider` blob) | ≈ 530 | SPEC-12 (measured in PHY-002) |
| Compound child entry | 40 + ≈ 32 BVH share | SPEC-12 |

Totals for `HollowStation1M` (872 bricks, 504,192 exposed faces, ≈ 1,000 boxes):

| Part | Estimate |
|---|---|
| ECS cells + masks (872 × 16,896) | 14.7 MB |
| ECS chunk data (872 × 300) | 0.26 MB |
| Meshes, GPU (504,192 × 21.6) | 10.9 MB |
| Mesh CPU copies (0 if SPEC-11 releases them; worst case equal to GPU) | 0 – 10.9 MB |
| Colliders: cached brick blobs + grid compound copy + one retired compound for 3 frames | ≈ 1.8 MB peak |
| Source structure | 0 in steady state (blueprints > 65,536 cells are released after spawn; `WriteBack` sources are fleet ships, not stations) |
| **Total** | **≈ 27.7 MB (≈ 38.6 MB worst case)** ≤ 40 MB |

Stress case `ThinShellStation1M` (3,784 bricks, ≈ 2.09M faces): cells ≈ 64 MB + meshes ≈ 45 MB → ≈ 110 MB; it cannot meet 40 MB with dense bricks at LOD0. It is measured and reported by VOX-002, not gated. Fallback if the owner makes thin shells a gate (requires an ADR because it changes the `BrickCells` contract): **sparse bricks** — bricks with `SolidCount ≤ 1,024` store `BrickSparseCells : IBufferElementData { ushort Local; Cell Value; }` sorted by `Local` (6 B per solid cell) instead of `BrickCells`, `BrickWrite` densifies when a write would exceed 1,024 entries; plus SPEC-11 rendering bricks beyond 150 m at LOD1 (≤ 25% triangles). Estimated thin-shell total with both: ≈ 6 MB cells + ≈ 15 MB meshes + 4 MB colliders ≈ 25 MB.

## 9. Tasks

### VOX-001 — Public contract types
- **Milestone:** M1
- **Tier:** standard — transcribes the exact code in §4
- **Depends on:** `CORE-001`, `DATA-001`
- **Do:** Create every type of §4.1–§4.7 with the exact fields, enums and signatures; method bodies `throw new System.NotImplementedException()` except one-line properties and `Cell` helpers. Create folders `Assets/Game/Voxel/Model/`, `Assets/Game/Voxel/Grid/`, `Assets/Game/Data/Definitions/Voxel/`. Add the `[ContentIdPrefix]` attributes shown.
- **Files:** `Assets/Game/Voxel/Model/{Cell,CellFlags,PaletteEntry,BlockInstance,BlockInstanceFlags,GridKind,GridMotion,GridSpawnFlags,BlockPlacement,PlaceResult,VoxelContent,BrickMath,BrickMaskBits,BrickSolidMask,StructureMassData,StructureHeader,BrickRecord,VoxelStructureData,StructureBlock,StructureDiff,VoxelStructure,VoxelStructureRegistryRef,GridSpawnRequest,GridRebuildRequest,GridDespawnRequest,GridSpawning,GridRebuilt,GridBodyInit,GridSource,GridEditedBrick,GridEditRequest,VoxelGrid,BrickSlot,Brick,BrickCells,BrickDirtyFlags,BrickDirty,FunctionalBlock,FunctionalComponentRegistration,GridPrototypes,RetiredBlob,RetiredMesh,DeferredDisposalQueue,VoxelConfig,ICellSolidQuery,VoxelRayHit,VoxelRaycast,StructureCellQuery,GridCellQuery,IBlockIdMap,VoxelStructureCodec,VoxelCodecException,MassAccumulator,BenchmarkStructures}.cs`; `Assets/Game/Voxel/Grid/{IGridSpawnHook,VoxelStructureRegistry,BlueprintLibrary,GridSpawnParams,GridSpawnApi,GridStructureSync,BrickWrite,GridCellReader,VoxelConfigFactory,DeferredDisposal,CatalogBlockIdMap}.cs`; `Assets/Game/Data/Definitions/Voxel/{ShipBlueprintDefinition,VoxelConfigDefinition}.cs`
- **Acceptance criteria:**
  - [ ] `tools/compile.sh` passes with zero warnings in `Game.Voxel` and `Game.Data`.
  - [ ] `UnsafeUtility.SizeOf<Cell>() == 4`, `<PaletteEntry>() == 4`, `<BlockInstance>() == 32`, `<Brick>() == 28`, `<BrickMaskBits>() == 512`, `<BrickRecord>() == 32`.
  - [ ] `GridKind` has exactly `Ship=0, Station=1, Asteroid=2, Hulk=3`; `BrickCells`, `BrickSolidMask`, `BlockInstance`, `BrickSlot`, `GridEditedBrick` carry `[InternalBufferCapacity(0)]`.
  - [ ] No `Game.Voxel` type references `Game.Simulation`, `Game.Galaxy` or a higher module; no component or buffer element contains a managed type; only the singletons `DeferredDisposalQueue` and the SPEC-11/12 queue/cache singletons contain native containers (reflection test with an explicit allow-list).
- **Tests:** `VoxelContractTests.StructSizes_MatchSpec`, `.GridKind_HasBindingValues`, `.Buffers_HaveCapacityZero`, `.Components_AreUnmanagedWithoutContainers` (`Tests/EditMode/Voxel/`).

### VOX-002 — SPIKE: memory model for the reference and thin-shell stations
- **Milestone:** M1
- **Tier:** frontier — F6: measurement and go/no-go decision for the 40 MB gate
- **Depends on:** `VOX-001`
- **Do:** Time-box 2 days. Throwaway code under `Assets/Game/Tests/PlayMode/Voxel/Spikes/` (deleted after the log entry). (1) Build `HollowStation1M` and `ThinShellStation1M` with a minimal structure (plain `NativeHashMap<int3, NativeArray<Cell>>` is acceptable). (2) Create matching ECS bricks with the §4.5 archetype plus Entities Graphics components and read chunk usage via `ArchetypeChunk.Capacity` and `TypeManager.GetTypeInfo<T>().SizeInChunk`. (3) Count exposed faces and welded vertices with a slice-weld counter (per axis direction and slice, a 17×17 table: count distinct corner positions per uniform-colour region). (4) Create one `BoxCollider` blob and a 64-child compound; record `MemorySize`. (5) Fill the §8.2 tables with measured values for both stations; write them to the ledger *Measurements*.
- **Files:** `Assets/Game/Tests/PlayMode/Voxel/Spikes/MemoryModelSpike.cs` (temporary); `docs/specs/SPEC-10-voxel-data-model.md` §8.2 (update numbers); `docs/LEDGER.md`
- **Acceptance criteria:**
  - [ ] Measured per-item bytes recorded for every row of §8.2.
  - [ ] **Go** if the reference station totals ≤ 36 MB assuming CPU mesh copies are kept (10% headroom). **No-go** otherwise: mark VOX-003 `todo` (sparse-brick fallback) and raise an ADR draft for the `BrickCells` contract.
  - [ ] Thin-shell totals recorded with the fallback estimate; owner notified in the ledger *Open questions*.
- **Tests:** `MemoryModelSpike.ReferenceStation_Report`, `.ThinShell_Report` (manual-run `[Explicit]` tests).

### VOX-003 — Sparse-brick fallback (only if VOX-002 is no-go)
- **Milestone:** M1
- **Tier:** frontier — F1/F3: changes the brick storage contract
- **Depends on:** `VOX-002`
- **Do:** If VOX-002 records **go**, set this task `dropped` with the measurement reference. If **no-go**: after the ADR is accepted, implement `BrickSparseCells` per §8.2 in `VoxelStructure`, ECS spawn, `BrickWrite`, `GridCellReader` and the codec; update §4 and §5 of this spec in the same commit.
- **Files:** `Assets/Game/Voxel/Model/BrickSparseCells.cs`; changes to `VoxelStructure.cs`, `BrickWrite.cs`, `GridCellReader.cs`, `GridSpawnSystem.cs`, `VoxelStructureCodec.cs`; `Assets/Game/Tests/EditMode/Voxel/SparseBrickTests.cs`
- **Acceptance criteria:**
  - [ ] Every VOX test passes with sparse storage enabled.
  - [ ] Reference station memory ≤ 36 MB measured.
- **Tests:** `SparseBrickTests.SparseDense_Equivalent_Fuzz`, `.DensifyAtThreshold`.

### VOX-004 — Brick maths and mask operations
- **Milestone:** M1
- **Tier:** frontier — F1/F3: every voxel system indexes cells through these functions
- **Depends on:** `VOX-001`
- **Do:** Implement `BrickMath` and `BrickMaskBits` exactly per §4.2 (word/bit layout, rows, `math.countbits`, bounds by word scans, `BorderFaces`, `FaceDirection` in SPEC-04 `BlockFace` order, buffer copy helpers). Implement the `Cell` helpers.
- **Files:** `Assets/Game/Voxel/Model/{BrickMath,BrickMaskBits,Cell}.cs`; `Assets/Game/Tests/EditMode/Voxel/{BrickMathTests,BrickMaskBitsTests}.cs`
- **Acceptance criteria:**
  - [ ] `CellOf(BrickOf(c), LocalIndex(LocalOf(c))) == c` for 10,000 seeded cells in `[-5000, 5000]³`, including negatives.
  - [ ] `Row(y,z)` equals the 16 `Get` bits for every row of 64 random masks; `CopyTo`/`CopyFrom` round-trip.
  - [ ] `TryGetBounds` and `CountSolid` equal a brute-force scan for 1,000 random masks; the empty mask returns false.
  - [ ] `BorderFaces(LocalIndex(0,15,7)) == NegX | PosY`.
- **Tests:** `BrickMathTests.CellBrickLocal_RoundTrip_Negatives`, `.SlotIndex_OutsideReturnsMinusOne`, `.BorderFaces_Corners`; `BrickMaskBitsTests.Row_MatchesBits`, `.Bounds_MatchBruteForce`, `.Count_MatchesBruteForce`, `.FullAndEmpty`, `.BufferRoundTrip`.

### VOX-005 — VoxelStructure storage core
- **Milestone:** M1
- **Tier:** frontier — F1/F2: paged native storage living across frames, uniform bricks
- **Depends on:** `VOX-004`
- **Do:** Implement the constructor, `Data`, header properties, `GetCell`, `SetCell` (§5.1 steps 1–5), `SetBrickDense`, `SetBrickUniform`, `RemoveBrick`, `OptimizeStorage`, bounds, `SortedBrickOrder`, `SetReadOnly`, `Pin/Unpin`, `NativeBytes`, `Clear`, `Dispose` (all `Allocator.Persistent`; `Dispose` throws when pinned; a second `Dispose` is a no-op).
- **Files:** `Assets/Game/Voxel/Model/{VoxelStructure,VoxelStructureData,StructureHeader,BrickRecord}.cs`; `Assets/Game/Tests/EditMode/Voxel/VoxelStructureStorageTests.cs`
- **Acceptance criteria:**
  - [ ] A seeded fuzz of 100,000 `SetCell` calls agrees with a `Dictionary<int3, Cell>` reference on every `GetCell`, `SolidCellCount` and bounds.
  - [ ] Deleting the last solid cell of a brick removes the brick and recycles its page (`CellPages.Length` constant over a fill/clear cycle).
  - [ ] `OptimizeStorage` makes a full identical brick uniform; writing one different cell densifies it with the other 4,095 cells unchanged.
  - [ ] Leak detection (`EnabledWithStackTrace`) reports nothing after `Dispose`; `Dispose` while pinned throws.
- **Tests:** `VoxelStructureStorageTests.SetGet_FuzzMatchesDictionary`, `.EmptyBrick_IsRemovedAndPageReused`, `.Uniform_RoundTripAndDensify`, `.SortedOrder_IsZYX`, `.Pinned_MutatorsThrow`, `.ReadOnly_MutatorsReject`, `.Dispose_NoLeaks`.

### VOX-006 — Block placement, removal and compaction
- **Milestone:** M1
- **Tier:** frontier — F1/F3: the footprint/instance mapping is the 01-B1 fix
- **Depends on:** `VOX-005`, `SPEC-04`
- **Do:** Implement `TryPlaceBlock`, `TryRemoveBlockAt`, `TryGetBlockAt`, `FindOrAddPalette` (§5.2) and `Compact` (§5.3) with `BlockPlacementMath.GetCells`. Test catalog: SPEC-04's `ContentService.CreateForTests` + `TestContentFactory` with `block_hull`, `block_hull_angled`, `block_thruster_medium` (10 cells), `block_core_cross` (7 cells), `block_capacitor_small` from SPEC-04 §4.10.
- **Files:** `Assets/Game/Voxel/Model/VoxelStructure.cs`; `Assets/Game/Tests/EditMode/Voxel/{VoxelTestContent,BlockPlacementTests,CompactTests}.cs`
- **Acceptance criteria:**
  - [ ] Placing `block_thruster_medium` at origin (3,4,5) in each of its 16 allowed orientations makes exactly its 10 rotated cells reference one instance with the root flagged `InstanceRoot`; removing via any of its cells clears all 10 (regression 01-B1).
  - [ ] Placement onto an occupied cell returns `Occupied` and leaves the structure byte-identical.
  - [ ] Two hull blocks with the same orient and colour share one palette entry.
  - [ ] `Compact` after 5,000 seeded place/remove operations (seed 7) leaves no unused palette entries and no tombstones and the same `GetBlocks` set.
- **Tests:** `BlockPlacementTests.MultiCell_AllOrientations_FootprintMatches`, `.RemoveFromAnyCell_ClearsFootprint`, `.Occupied_NoChange`, `.PaletteDedup`, `.InstanceSlotReuse`; `CompactTests.Compact_PreservesBlocks_RemovesGarbage`, `.Compact_IsDeterministic`.

### VOX-007 — Clone, diff, block enumeration and mass
- **Milestone:** M1
- **Tier:** frontier — F3: the diff feeds the BOM; mass feeds physics
- **Depends on:** `VOX-006`
- **Do:** Implement `Clone`, `GetBlocks`, `Diff` with `StructureDiffJob` (§5.4), `MassAccumulator`, `GetMass` with `StructureMassJob` and the ordered serial sum.
- **Files:** `Assets/Game/Voxel/Model/{VoxelStructure,StructureDiff,StructureDiffJob,MassAccumulator,StructureMassJob}.cs`; `Assets/Game/Tests/EditMode/Voxel/{StructureDiffTests,StructureMassTests}.cs`
- **Acceptance criteria:**
  - [ ] `Diff(a, a.Clone())` is empty; applying `Removed`/`Added` of `Diff(a, b)` to a clone of `a` yields `GetBlocks` equal to `b`'s (1,000-op seeded edits).
  - [ ] `GetMass` equals a brute-force double sum within 1e-9 relative and is bit-identical across 3 runs.
  - [ ] `Diff` of two 100k-cell structures differing by 10 blocks ≤ 10 ms (Burst on).
- **Tests:** `StructureDiffTests.CloneDiff_IsEmpty`, `.Diff_ApplyReproducesTarget`, `.Diff_IgnoresHp`, `.Diff_Perf100k` (`Performance`); `StructureMassTests.Mass_MatchesBruteForce`, `.Mass_IsDeterministic`.

### VOX-008 — Grid-space DDA raycast
- **Milestone:** M1
- **Tier:** frontier — F3/F4: hit resolution for projectiles and editor picking
- **Depends on:** `VOX-005`
- **Do:** Implement `VoxelRaycast.Cast` (§5.5) with empty-brick skipping, `StructureCellQuery`, `GridCellReader` and `GridCellQuery`.
- **Files:** `Assets/Game/Voxel/Model/{VoxelRaycast,StructureCellQuery,GridCellQuery}.cs`; `Assets/Game/Voxel/Grid/GridCellReader.cs`; `Assets/Game/Tests/EditMode/Voxel/VoxelRaycastTests.cs`
- **Acceptance criteria:**
  - [ ] 10,000 seeded rays against a random 64³ structure match a brute-force fine-step reference (step 0.001 cells) in hit cell and entered face.
  - [ ] Rays along cell boundaries obey the X-before-Y-before-Z tie rule.
  - [ ] A ray through 100 empty bricks performs ≤ 3 steps per brick (counted by a test query).
  - [ ] ECS and structure queries return identical hits for the same data.
- **Tests:** `VoxelRaycastTests.Random_MatchesBruteForce`, `.StartInsideSolid_DistanceZero`, `.TieBreak_XYZ`, `.EmptyBricks_Skipped`, `.EcsAndStructure_Agree`.

### VOX-009 — VOXS codec
- **Milestone:** M1
- **Tier:** frontier — F3: binary encoding
- **Depends on:** `VOX-007`, `PERS-001`
- **Do:** Implement `VoxelStructureCodec.Encode/Decode` against SPEC-05 §VOXS (Appendix A until it exists), `VoxelCodecException`, `ComputeContentHash`, a test `IBlockIdMap`.
- **Files:** `Assets/Game/Voxel/Model/{VoxelStructureCodec,VoxelCodecException,IBlockIdMap,VoxelStructure}.cs`; `Assets/Game/Tests/EditMode/Voxel/VoxelCodecTests.cs`
- **Acceptance criteria:**
  - [ ] Encode → Decode → Encode is byte-identical for an empty structure, `Ship20k`, `HollowStation1M`, `SolidAsteroid1M` and a 5,000-op edited structure with tombstones and damage.
  - [ ] Decoded structures equal the originals cell by cell and instance by instance.
  - [ ] An unknown block ID decodes to `MissingBlockIndex` and is reported once.
  - [ ] Every truncated prefix of a small payload throws `VoxelCodecException` and no other exception type.
  - [ ] `HollowStation1M` payload ≤ 120 KB; decode ≤ 150 ms.
- **Tests:** `VoxelCodecTests.RoundTrip_ByteIdentical`, `.Decode_EqualsOriginal`, `.UnknownId_MapsToMissing`, `.Truncated_Throws`, `.HollowStation_SizeAndDecodeTime` (`Performance`).

### VOX-010 — Voxel definitions and config factory
- **Milestone:** M1
- **Tier:** standard — definitions listed field by field
- **Depends on:** `VOX-001`
- **Do:** Implement `ShipBlueprintDefinition` (getters, `Validate` with the §7 codes, `EditorSetPayload`), `VoxelConfigDefinition`, `VoxelConfigFactory.Create`. Create `Assets/Game/Content/Config/config_voxel.asset` with the defaults.
- **Files:** `Assets/Game/Data/Definitions/Voxel/{ShipBlueprintDefinition,VoxelConfigDefinition}.cs`; `Assets/Game/Voxel/Grid/VoxelConfigFactory.cs`; `Assets/Game/Content/Config/config_voxel.asset`; `Assets/Game/Tests/EditMode/Voxel/VoxelDefinitionTests.cs`
- **Acceptance criteria:**
  - [ ] Each of `VOX-E001`, `VOX-E002`, `VOX-E003` has a failing and a passing fixture.
  - [ ] `VoxelConfigFactory.Create` copies values and sets `RequireRenderer = 1`.
- **Tests:** `VoxelDefinitionTests.Blueprint_ValidationCodes`, `.ConfigFactory_CopiesValues`.

### VOX-011 — Structure registry and BlueprintLibrary
- **Milestone:** M1
- **Tier:** frontier — F2: cached native structures live for the session; handles cross the managed/ECS boundary
- **Depends on:** `VOX-009`, `VOX-010`
- **Do:** Implement `VoxelStructureRegistry` (list of structures, never-reused handles, hooks), `BlueprintLibrary` (hash → definition map built from `catalog.All<ShipBlueprintDefinition>()`; decode with `CatalogBlockIdMap`; `SetReadOnly`; cache by hash; `ReleaseUnpinnedLarge`), `CatalogBlockIdMap` (`GetBlock(i).Id`, `TryGetBlockIndex`, `ResolveBlockOrMissing`).
- **Files:** `Assets/Game/Voxel/Grid/{VoxelStructureRegistry,BlueprintLibrary,CatalogBlockIdMap}.cs`; `Assets/Game/Tests/EditMode/Voxel/{StructureRegistryTests,BlueprintLibraryTests}.cs`
- **Acceptance criteria:**
  - [ ] Handles are unique and never reused after `Release`; `Get` of a released handle returns null.
  - [ ] A second `TryGet` of a blueprint returns the same instance without decoding (decode counter); it rejects mutation.
  - [ ] `ReleaseUnpinnedLarge` disposes only unpinned structures above 65,536 cells.
  - [ ] `Dispose` leaves no native leaks.
- **Tests:** `StructureRegistryTests.Handles_UniqueNeverReused`, `.Hooks_AddRemove`; `BlueprintLibraryTests.TryGet_CachesAndIsReadOnly`, `.UnknownHash_ReturnsFalse`, `.ReleaseUnpinnedLarge_KeepsPinned`, `.Dispose_NoLeaks`.

### VOX-012 — ECS cell write and read helpers
- **Milestone:** M1
- **Tier:** frontier — F1: the runtime cell write path used by SPEC-13
- **Depends on:** `VOX-004`
- **Do:** Implement `BrickWrite.Get/Set` and `GridCellReader` per §4.6 and §6.
- **Files:** `Assets/Game/Voxel/Grid/{BrickWrite,GridCellReader}.cs`; `Assets/Game/Tests/EditMode/Voxel/BrickWriteTests.cs`
- **Acceptance criteria:**
  - [ ] Mask words, `SolidCount`, `ChangedRevision`, `Flags` and `BorderMask` match expectations for solid→air, air→solid, Hp-only (same tier and tier change) and no-op writes; no-op returns false and changes nothing.
  - [ ] A Burst-compiled test job calling `Set` compiles and runs with safety checks on.
- **Tests:** `BrickWriteTests.Flags_PerWriteKind`, `.BorderMask_OnEdgeCells`, `.NoOp_ReturnsFalse`, `.BurstJob_Runs`.

### VOX-013 — GridSpawnSystem: spawn path and singletons
- **Milestone:** M1
- **Tier:** frontier — F1/F5: builds every grid; ordering with SPEC-11/12/13/31
- **Depends on:** `VOX-006`, `VOX-011`, `VOX-012`, `REN-001`, `PHY-001`, `SPEC-71`
- **Do:** Implement `VoxelSingletons`, `GridSpawnApi` spawn overloads, `GridSpawnSystem` §5.7 steps 1, 4 and 5 with `BrickFillJob` and `FunctionalFillJob`, hooks, `PersistentId` copy, profiler markers.
- **Files:** `Assets/Game/Voxel/Grid/{GridSpawnSystem,GridSpawnApi,VoxelSingletons,BrickFillJob,FunctionalFillJob}.cs`; `Assets/Game/Tests/EditMode/Voxel/GridSpawnSystemTests.cs`; `Assets/Game/Tests/PlayMode/Voxel/GridSpawnPerfTests.cs`
- **Acceptance criteria:**
  - [ ] Spawning `Ship20k` with 3 functional blocks yields 1 root, 18 bricks, 3 functional entities; every brick's cells and mask equal the structure's; `BrickSlot` maps every coord; the LEG has 22 entries; `Instances[i].Functional` points at the entity; the root has `PersistentId` equal to the request's.
  - [ ] Components added by the requester before the spawn frame remain on the root; a test hook receives every created brick and functional entity exactly once.
  - [ ] `Lattice2500` with `SpawnBricksPerFrame = 1024` takes exactly 3 updates; `GridSpawning` is disabled after the third; the source is unpinned then.
  - [ ] A uniform structure brick spawns as 4,096 identical cells.
  - [ ] Invalid requests (empty structure, both sources, unknown hash) are destroyed with one error log each.
  - [ ] A registered component type for `FunctionKind.Thruster` is present on spawned thruster entities.
  - [ ] Creating 1,024 dense bricks ≤ 6 ms main thread (median of 20).
- **Tests:** `GridSpawnSystemTests.Spawn_Ship20k_CreatesExpectedEntities`, `.RequesterComponents_Preserved`, `.Hook_ReceivesAllCreated`, `.Budget_SpreadsAcrossFrames`, `.UniformBrick_Expanded`, `.InvalidRequests_Destroyed`, `.FunctionalRegistration_Applied`, `.Headless_NoRenderComponents`; `GridSpawnPerfTests.Create1024Bricks_Under6ms`.

### VOX-014 — Rebuild, despawn and write-back
- **Milestone:** M1
- **Tier:** frontier — F3/F5: damage persistence (04-B3) across systems
- **Depends on:** `VOX-013`
- **Do:** Implement `GridSpawnSystem` steps 2–3, `GridSpawnApi.RequestRebuild/RequestDespawn`, `GridStructureSync.WriteBack/CaptureChanges/WriteBackAll` (bricks with `ChangedRevision > SyncedRevision`; bricks absent from `BrickSlot` removed from the source; `OptimizeStorage` on written bricks).
- **Files:** `Assets/Game/Voxel/Grid/{GridSpawnSystem,GridSpawnApi,GridStructureSync}.cs`; `Assets/Game/Tests/EditMode/Voxel/GridLifecycleTests.cs`
- **Acceptance criteria:**
  - [ ] Damage through `BrickWrite.Set` to 3 bricks, then `WriteBack`, copies exactly 3 bricks; a second `WriteBack` copies 0.
  - [ ] Despawn with `WriteBack` → respawn from the same structure reproduces the damaged cells (regression 04-B3).
  - [ ] A brick destroyed at runtime is removed from the source by write-back.
  - [ ] Rebuild replaces bricks and functional entities, keeps the root and its non-voxel components, and enables `GridRebuilt`.
  - [ ] Despawn destroys root, bricks and functional entities (entity count returns to baseline).
- **Tests:** `GridLifecycleTests.WriteBack_CopiesOnlyChanged`, `.DespawnRespawn_KeepsDamage`, `.WriteBack_RemovesDestroyedBricks`, `.Rebuild_KeepsRoot`, `.Despawn_DestroysAll`, `.CaptureChanges_SinceSpawn`.

### VOX-015 — BrickDirtyScheduleSystem
- **Milestone:** M1
- **Tier:** frontier — F2/F3: deterministic queue order feeds deterministic colliders
- **Depends on:** `VOX-012`, `REN-001`, `PHY-001`
- **Do:** Implement §5.8 with `BrickDirtyDrainJob` (single-worker `IJobChunk`) using the queue `Enqueue` methods from REN-001/PHY-001.
- **Files:** `Assets/Game/Voxel/Grid/{BrickDirtyScheduleSystem,BrickDirtyDrainJob}.cs`; `Assets/Game/Tests/EditMode/Voxel/BrickDirtyScheduleTests.cs`
- **Acceptance criteria:**
  - [ ] A brick dirtied with `Mesh|Collider` and `BorderMask = PosX` yields one collider entry, one own mesh entry and one neighbour entry for the +X brick; `BrickDirty` is disabled with zero flags.
  - [ ] Queue contents are identical across 10 runs with the same entity history and with 1 and max worker threads.
  - [ ] A brick with `SolidCount == 0` gets a collider entry and neighbour entries but no own mesh entry.
  - [ ] 0 B GC allocation per update.
- **Tests:** `BrickDirtyScheduleTests.Drain_EnqueuesExpected`, `.Order_IsDeterministic`, `.EmptyBrick_SkipsOwnMesh`, `.NoDirty_EarlyOut`, `.NoGcAlloc`.

### VOX-016 — DeferredDisposalSystem
- **Milestone:** M1
- **Tier:** frontier — F2: deferred disposal of native blobs and GPU meshes
- **Depends on:** `VOX-013`
- **Do:** Implement §5.10 and the `DeferredDisposal` helpers.
- **Files:** `Assets/Game/Voxel/Grid/{DeferredDisposalSystem,DeferredDisposal}.cs`; `Assets/Game/Tests/EditMode/Voxel/DeferredDisposalTests.cs`
- **Acceptance criteria:**
  - [ ] A retired blob is valid after 2 updates and disposed after the 3rd.
  - [ ] A retired mesh is unregistered and destroyed after the 3rd update (Entities Graphics present: PlayMode test).
  - [ ] World disposal with pending blobs leaks nothing.
- **Tests:** `DeferredDisposalTests.Blob_DisposedAfterThreeFrames`, `.Mesh_UnregisteredAfterThreeFrames`, `.WorldDispose_NoLeaks`.

### VOX-017 — Benchmark structures
- **Milestone:** M1
- **Tier:** standard — generators fully defined in §5.11
- **Depends on:** `VOX-005`
- **Do:** Implement `BenchmarkStructures.HollowStation1M/ThinShellStation1M/SolidAsteroid1M/Ship20k/Lattice2500(ushort block)` returning new `VoxelStructure`s.
- **Files:** `Assets/Game/Voxel/Model/BenchmarkStructures.cs`; `Assets/Game/Tests/EditMode/Voxel/BenchmarkStructuresTests.cs`
- **Acceptance criteria:**
  - [ ] Cell and brick counts equal §5.11 (asteroid: equals the brute-force count).
  - [ ] `HollowStation1M` builds in ≤ 300 ms.
- **Tests:** `BenchmarkStructuresTests.Counts_MatchSpec`, `.HollowStation_BuildTime` (`Performance`).

### VOX-018 — Integrated voxel gate suite
- **Milestone:** M1
- **Tier:** frontier — F4: owns three 06 voxel gates
- **Depends on:** `VOX-014`, `VOX-015`, `VOX-016`, `VOX-017`, `SPEC-11`, `SPEC-12`
- **Do:** PlayMode performance tests in a test scene with the full voxel pipeline, URP camera at 1920×1080, safety checks off (asserted), 60-frame warm-up, 300 measured frames (median and p95 logged): (1) idle `HollowStation1M` 200 m in front of the camera — sum of `ProfilerRecorder` main-thread times of every `Game.Voxel` system marker plus `EntitiesGraphicsSystem`; (2) memory per §8.2 using `VoxelMemoryStats` (cells = bricks × 16,896; chunk bytes from archetype chunk counts; mesh bytes from SPEC-11 `BrickRenderStats`; collider bytes from SPEC-12 `GridColliderStats`; mesh CPU copies via `Profiler.GetRuntimeMemorySizeLong` minus GPU estimate); (3) spawn `HollowStation1M`: per-frame voxel work and frames until every brick has `BrickRenderState.HasMesh` and SPEC-12 reports all bricks built. Record results in the ledger *Measurements*.
- **Files:** `Assets/Game/Tests/PlayMode/Voxel/{VoxelGatePerfTests,VoxelMemoryStats}.cs`
- **Acceptance criteria:**
  - [ ] Idle median ≤ 2.0 ms main thread for all voxel systems combined.
  - [ ] Memory ≤ 40 MB for `HollowStation1M`; `ThinShellStation1M` reported (not asserted).
  - [ ] Spawn: no frame over 8 ms of voxel work; fully meshed and collidable within 30 frames.
- **Tests:** `VoxelGatePerfTests.IdleStation1M_VoxelMainThreadUnder2ms`, `.HollowStation1M_MemoryUnder40MB`, `.ThinShell_Report`, `.SpawnStation1M_NoFrameOver8ms_ReadyWithin30Frames`.

### VOX-019 — Damage persistence scenario
- **Milestone:** M1
- **Tier:** standard — scenario test of behaviour specified in VOX-014
- **Depends on:** `VOX-014`
- **Do:** PlayMode scenario: register a fleet-owned structure, spawn it with `WriteBack`, damage 50 cells through `BrickWrite.Set` from a test system in the fixed step (bumping `VoxelGrid.Revision`), despawn, respawn from the same handle, compare cell-state hashes; repeat with `WriteBack` called at "save" time instead of despawn.
- **Files:** `Assets/Game/Tests/PlayMode/Voxel/DamagePersistenceScenarioTests.cs`
- **Acceptance criteria:**
  - [ ] The cell-state hash after respawn equals the hash before despawn.
  - [ ] Zero exceptions and zero leaks in the run.
- **Tests:** `DamagePersistenceScenarioTests.DespawnRespawn_KeepsDamage_04B3`, `.SaveWriteBack_KeepsDamage`.

### VOX-020 — Incremental grid edits for the editor preview
- **Milestone:** M3
- **Tier:** frontier — F1/F5: second cell-write path with structural bounds growth
- **Depends on:** `VOX-014`
- **Do:** Implement `GridSpawnSystem` step 6 (§5.9) and `GridSpawnApi.RequestEdit`.
- **Files:** `Assets/Game/Voxel/Grid/{GridSpawnSystem,GridSpawnApi}.cs`; `Assets/Game/Tests/EditMode/Voxel/GridEditTests.cs`; `Assets/Game/Tests/PlayMode/Voxel/GridEditPerfTests.cs`
- **Acceptance criteria:**
  - [ ] After 1,000 seeded place/remove operations mirrored through edit requests, every brick entity equals the draft structure.
  - [ ] Placing a block outside the current bounds grows `BrickDims` and keeps existing slots valid.
  - [ ] One place + edit request costs ≤ 2 ms main thread on a 100k-block ship (06 editor gate share).
- **Tests:** `GridEditTests.MirroredEdits_MatchDraft`, `.BoundsGrowth_RemapsSlots`, `.FunctionalAddRemove`; `GridEditPerfTests.PlaceRemove_Under2ms`.

## 10. Open questions

| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | Does `GameplayActive` exist in the BlueprintLab scene (VOX systems require it)? | Yes; SPEC-01 creates it in World and BlueprintLab | SPEC-01 / SPEC-51 |
| 2 | Who creates `VoxelConfig`, `VoxelStructureRegistryRef`, `BlueprintLibrary` and the registry? | SPEC-01's composition root right after `ContentService.CreateSingletons`: `BlueprintLibrary`, then `VoxelStructureRegistry.CreateRuntime(library)`, then the two singletons; disposes in reverse order at shutdown | SPEC-01 |
| 3 | SPEC-04 prefix registry lacks `config_` | `config_` for `VoxelConfigDefinition`, `VoxelRenderConfigDefinition`, `VoxelPhysicsConfigDefinition`, `VoxelPaletteDefinition`; SPEC-04 adds the row | SPEC-04 |
| 4 | VOXS byte layout not yet published by SPEC-05 | Appendix A | SPEC-05 |
| 5 | Must SPEC-13 maintain `BlockInstance.CellsAlive`? | Yes, decrement when an instance cell becomes air | SPEC-13 |
| 6 | Hulk grids created by `SplitSystem` (GameplayEnd ECB) | SPEC-13 records `Instantiate(GridPrototypes.Brick)` / moves existing bricks with the ECB, gives the hulk a full root archetype (§4.5) with `GridSource.StructureHandle = -1`, a new `Serial` taken from `GridPrototypes.NextSerial` inside its job (single writer: SplitSystem's playback order), `GridSpawning` disabled, `GridBodyInit` enabled; updates both roots' `BrickSlot` and LEG | SPEC-13 |
| 7 | `StarSystemMember` and `PersistentId` for hulks created by `SplitSystem` | SPEC-31 adds a `WorldGroup` system that batch-adds the parent's `StarSystemMember` to entities with `Brick`, `FunctionalBlock` or `VoxelGrid` that lack it and whose grid root is not the player ship; `PersistentId` for hulks comes from a `PersistentIdAllocator` singleton maintained by SPEC-01 | SPEC-31 / SPEC-01 |
| 8 | Which `CellFlags` bits does SPEC-13 need beyond `Scorched`? | None; claim bits 2..7 here | SPEC-13 |
| 9 | Should SPEC-31 unload call write-back? | Yes: `GridStructureSync.WriteBackAll(em, query, registry)` before the shared-filter destroy; stations from blueprints use `CaptureChanges` for deltas | SPEC-31 |
| 10 | Prop meshes are authored at 1 unit = 1 cell | Yes (functional `LocalTransform.Scale = CellSize`); REN-002 checks `hull_1x1` bounds ≈ 1 unit | SPEC-11 / SPEC-81 |
| 11 | Should damage persist for AI ships spawned from blueprints? | No; they are transient (no write-back) | Owner |
| 12 | Is the 4-cell-wall `HollowStation1M` the 06 reference station? | Yes (decision 18); thin shells reported only | Owner |

## 11. References

- Audit: `00-synthesis.md` §6b, §6e, §8 phase 2; `01-voxel-core.md` §5, §6.1–§6.6, §6.9, B1, B3, B5, B6, B8, B9; `04-persistence-data.md` B3, §6.4; LEDGER R22–R26.
- Binding: architecture §5.1 (snapshots, deterministic events), §6.3, §8, §11; conventions §2; [07-implementation-traps.md](../07-implementation-traps.md) T1.1–T1.7, T2.1–T2.10.
- Unity: Entities `LinkedEntityGroup`, `ICleanupComponentData`, `[CreateAfter]`, `UnityObjectRef<T>`, `DynamicBuffer.ResizeUninitialized`, `UnsafeUtility.MemCpyReplicate`; Entities Graphics `RenderMeshUtility.AddComponents`, `MaterialMeshInfo` (enableable).

### Appendix A — default VOXS payload layout v1 (used until SPEC-05 §VOXS exists)

Little-endian. `str` = `u16 byteLength` + UTF-8 bytes. Floats are raw IEEE-754 bits.

```
u32  magic         = 0x31535856 ("VXS1")
u16  layoutVersion = 1
f32  cellSize
u16  idCount;        idCount × str           // block ContentIds in first-use order over palette[1..] then instances
u16  paletteCount;   paletteCount × { u16 blockLocal (0 = none, else 1-based into ids), u8 orient, u8 color }   // includes entry 0
u16  instanceCount;  instanceCount × { u16 blockLocal (0 = tombstone), u8 orient, u8 color, i32 ox, i32 oy, i32 oz,
                                        f32 functionHp, u16 cellsAlive, u8 flags }
u32  brickCount;     brickCount × brick      // SortedBrickOrder (z, y, x)
brick := i32 bx, i32 by, i32 bz, u8 encoding
         encoding 0 (uniform): u16 ref, u8 hp, u8 flags
         encoding 1 (rle):     u16 runCount; runCount × { u16 length (1..4096), u16 ref }   // covers 4096 cells, local order
                               u16 hpCount;   hpCount × { u16 localIndex, u8 hp }           // solid cells with hp != 255, ascending
                               u16 flagCount; flagCount × { u16 localIndex, u8 flags }      // cells with flags != 0, ascending
```
Decode validates: magic; version ≤ 1; every count fits the remaining bytes; RLE lengths sum to exactly 4,096; palette refs < paletteCount; instance refs < instanceCount; local indices < 4,096 and strictly ascending; a solid cell never has hp 0. Any violation → `VoxelCodecException(context, byteOffset, reason)`.
