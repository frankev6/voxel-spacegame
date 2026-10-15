using Game.Data.Blocks;
using Game.Voxel.Damage;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Voxel.Replica
{
    /// <summary>One brick's 4096 cells for the replica jobs: the dense buffer in place, or a filled copy of a sparse brick.</summary>
    public unsafe struct BrickView
    {
        [NativeDisableUnsafePtrRestriction] public Cell* Cells;
        public int3 Coord;
    }

    /// <summary>Burst work behind ReplicaVoxelApi and the host's capture (SPEC-90 §10a gap 1). Each job runs to completion on the
    /// calling main thread, or in parallel with an immediate Complete, so callers keep their synchronous shape.</summary>
    public static unsafe class ReplicaJobs
    {
        /// <summary>Views of a grid's bricks. Dense bricks point at their buffers; sparse ones are expanded into <paramref name="sparse"/>.
        /// Valid until the next structural change or buffer write on those bricks.</summary>
        public static void ViewBricks(EntityManager em, Entity root, NativeList<BrickView> views, NativeList<Cell> sparse)
        {
            views.Clear(); sparse.Clear();
            var slots = em.GetBuffer<BrickSlot>(root, true);
            int sparseCount = 0;
            for (int s = 0; s < slots.Length; s++)
            {
                Entity entity = slots[s].Brick; if (entity == Entity.Null || !em.Exists(entity)) continue;
                if (em.GetBuffer<BrickCells>(entity, true).Length == 0) sparseCount++;
            }
            sparse.Resize(sparseCount * BrickMath.CellsPerBrick, NativeArrayOptions.ClearMemory);
            int next = 0;
            for (int s = 0; s < slots.Length; s++)
            {
                Entity entity = slots[s].Brick; if (entity == Entity.Null || !em.Exists(entity)) continue;
                views.Add(View(em, entity, sparse, ref next));
            }
        }

        public static bool IsSparse(EntityManager em, Entity brick) => em.GetBuffer<BrickCells>(brick, true).Length == 0;

        /// <summary>A view of one brick; a sparse brick is expanded into <paramref name="sparse"/> at block <paramref name="next"/>. The
        /// caller sizes <paramref name="sparse"/> first: growing it would move the blocks earlier views point into.</summary>
        public static BrickView View(EntityManager em, Entity brick, NativeList<Cell> sparse, ref int next)
        {
            var coord = em.GetComponentData<Brick>(brick).Coord;
            var dense = em.GetBuffer<BrickCells>(brick, true);
            if (dense.Length != 0) return new BrickView { Cells = (Cell*)dense.GetUnsafeReadOnlyPtr(), Coord = coord };
            if (sparse.Length < (next + 1) * BrickMath.CellsPerBrick) throw new System.InvalidOperationException("sparse scratch too small");
            Cell* cells = (Cell*)sparse.GetUnsafePtr() + next * BrickMath.CellsPerBrick;
            UnsafeUtility.MemClear(cells, BrickMath.CellsPerBrick * sizeof(Cell));
            foreach (var entry in em.GetBuffer<BrickSparseCells>(brick, true)) cells[entry.Local] = entry.Value;
            next++;
            return new BrickView { Cells = cells, Coord = coord };
        }

        /// <summary>Runs every replica job once on a scratch brick. The first call of each Burst job in a player costs a few ms, which
        /// would otherwise land on the first cut or hash check mid-fight; joins pay it instead.</summary>
        public static void WarmUp()
        {
            using var cells = new NativeArray<Cell>(BrickMath.CellsPerBrick, Allocator.TempJob);
            var c = cells; c[0] = new Cell { Ref = 1, Hp = 255 };
            using var views = new NativeArray<BrickView>(1, Allocator.TempJob);
            var v = views; v[0] = ViewOf(cells, int3.zero);
            Hash(views); ContainsRef(views, 1);
            using var counts = new NativeArray<int>(1, Allocator.TempJob);
            using var changed = new NativeArray<ushort>(BrickMath.CellsPerBrick, Allocator.TempJob);
            Diff(views, views, counts, changed);
            using var chunk = new NativeArray<ChunkCell>(1, Allocator.TempJob);
            ChunkCollider(chunk, .5f, float3.zero, 4).Dispose();
        }

        /// <summary>A view of cells the caller owns (the host's shadow copies).</summary>
        public static BrickView ViewOf(NativeArray<Cell> cells, int3 coord) => new BrickView { Cells = (Cell*)cells.GetUnsafeReadOnlyPtr(), Coord = coord };
        public static Cell Get(in BrickView view, int local) => view.Cells[local];
        public static void CopyTo(in BrickView view, NativeArray<Cell> destination) =>
            UnsafeUtility.MemCpy(destination.GetUnsafePtr(), view.Cells, BrickMath.CellsPerBrick * sizeof(Cell));

        /// <summary>Order-independent hash of the solid cells of <paramref name="bricks"/> (see ReplicaVoxelApi.HashCells).</summary>
        public static ulong Hash(NativeArray<BrickView> bricks)
        {
            if (bricks.Length == 0) return 0;
            using var hashes = new NativeArray<ulong>(bricks.Length, Allocator.TempJob);
            new HashBricksJob { Bricks = bricks, Hashes = hashes }.ScheduleParallel(bricks.Length, 4, default).Complete();
            ulong total = 0;
            for (int i = 0; i < hashes.Length; i++) total += hashes[i];
            return total;
        }

        [BurstCompile]
        struct HashBricksJob : IJobFor
        {
            [ReadOnly] public NativeArray<BrickView> Bricks;
            public NativeArray<ulong> Hashes;
            public void Execute(int index)
            {
                var b = Bricks[index];
                ulong h = Game.Core.StableHash.Combine((ulong)(uint)b.Coord.x, Game.Core.StableHash.Combine((ulong)(uint)b.Coord.y, (ulong)(uint)b.Coord.z));
                for (int i = 0; i < BrickMath.CellsPerBrick; i++)
                {
                    var c = b.Cells[i]; if (c.Ref == 0) continue;
                    h = Game.Core.StableHash.Combine(h, ((ulong)(uint)i << 32) | ((ulong)c.Ref << 16) | ((ulong)c.Hp << 8) | c.Flags);
                }
                Hashes[index] = h;
            }
        }

        /// <summary>True when any cell of <paramref name="bricks"/> holds <paramref name="reference"/>.</summary>
        public static bool ContainsRef(NativeArray<BrickView> bricks, ushort reference)
        {
            if (bricks.Length == 0) return false;
            using var found = new NativeReference<bool>(Allocator.TempJob);
            new ContainsRefJob { Bricks = bricks, Reference = reference, Found = found }.Run();
            return found.Value;
        }

        [BurstCompile]
        struct ContainsRefJob : IJob
        {
            [ReadOnly] public NativeArray<BrickView> Bricks;
            public ushort Reference;
            public NativeReference<bool> Found;
            public void Execute()
            {
                for (int b = 0; b < Bricks.Length; b++)
                {
                    Cell* cells = Bricks[b].Cells;
                    for (int i = 0; i < BrickMath.CellsPerBrick; i++) if (cells[i].Ref == Reference) { Found.Value = true; return; }
                }
            }
        }

        /// <summary>Host capture: compares each brick with its shadow copy. For brick k, <paramref name="counts"/>[k] is the number of
        /// changed cells and <paramref name="changed"/>[k * 4096 ..] their local indices. Bricks with a null shadow get count -1.</summary>
        public static void Diff(NativeArray<BrickView> current, NativeArray<BrickView> shadows, NativeArray<int> counts, NativeArray<ushort> changed)
        {
            if (current.Length == 0) return;
            new DiffBricksJob { Current = current, Shadows = shadows, Counts = counts, Changed = changed }.ScheduleParallel(current.Length, 1, default).Complete();
        }

        [BurstCompile]
        struct DiffBricksJob : IJobFor
        {
            [ReadOnly] public NativeArray<BrickView> Current, Shadows;
            [NativeDisableParallelForRestriction] public NativeArray<int> Counts;
            [NativeDisableParallelForRestriction] public NativeArray<ushort> Changed;
            public void Execute(int index)
            {
                Cell* a = Current[index].Cells, b = Shadows[index].Cells;
                if (b == null) { Counts[index] = -1; return; }
                int n = 0, at = index * BrickMath.CellsPerBrick;
                uint* wa = (uint*)a, wb = (uint*)b;   // a cell is four bytes
                for (int i = 0; i < BrickMath.CellsPerBrick; i++) if (wa[i] != wb[i]) Changed[at + n++] = (ushort)i;
                Counts[index] = n;
            }
        }

        public struct DenseWrite
        {
            public Brick Brick; public BrickDirty Dirty;
            public int Changed; public BlockFaceMask Neighbours;
        }

        /// <summary>Applies replicated writes to a dense brick in Burst with BrickWrite.Set's rules (mask, solid count, revision, dirty
        /// flags), collecting removed cells and touched instances like the managed path in ReplicaVoxelApi.WriteBrick.</summary>
        public static DenseWrite WriteDense(DynamicBuffer<BrickCells> dense, DynamicBuffer<BrickSolidMask> mask, NativeArray<ReplicaCellWrite> writes,
            DynamicBuffer<PaletteEntry> palette, DynamicBuffer<BlockInstance> instances, Brick brick, BrickDirty dirty, uint revision,
            Entity root, int3 coord, float cellSize, Unity.Transforms.LocalTransform transform, float3 velocity, bool quiet,
            NativeList<CellRemoved> removed, NativeList<int> touched)
        {
            using var result = new NativeReference<DenseWrite>(new DenseWrite { Brick = brick, Dirty = dirty }, Allocator.TempJob);
            var noRemoved = removed.IsCreated ? default : new NativeList<CellRemoved>(0, Allocator.TempJob);
            var noTouched = touched.IsCreated ? default : new NativeList<int>(0, Allocator.TempJob);
            new WriteDenseJob
            {
                Cells = (Cell*)dense.GetUnsafePtr(), Mask = (ulong*)mask.GetUnsafePtr(), Writes = writes,
                Palette = (PaletteEntry*)palette.GetUnsafeReadOnlyPtr(), PaletteLength = palette.Length,
                Instances = (BlockInstance*)instances.GetUnsafeReadOnlyPtr(), InstanceLength = instances.Length,
                Revision = revision, Root = root, Coord = coord, CellSize = cellSize, Transform = transform, Velocity = velocity,
                CollectRemoved = removed.IsCreated && !quiet, CollectTouched = touched.IsCreated,
                Removed = removed.IsCreated ? removed : noRemoved, Touched = touched.IsCreated ? touched : noTouched, Result = result
            }.Run();
            if (noRemoved.IsCreated) noRemoved.Dispose();
            if (noTouched.IsCreated) noTouched.Dispose();
            return result.Value;
        }

        [BurstCompile]
        struct WriteDenseJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public Cell* Cells;
            [NativeDisableUnsafePtrRestriction] public ulong* Mask;
            [NativeDisableUnsafePtrRestriction] public PaletteEntry* Palette;
            [NativeDisableUnsafePtrRestriction] public BlockInstance* Instances;
            [ReadOnly] public NativeArray<ReplicaCellWrite> Writes;
            public int PaletteLength, InstanceLength;
            public uint Revision; public Entity Root; public int3 Coord; public float CellSize;
            public Unity.Transforms.LocalTransform Transform; public float3 Velocity;
            public bool CollectRemoved, CollectTouched;
            public NativeList<CellRemoved> Removed; public NativeList<int> Touched;
            public NativeReference<DenseWrite> Result;

            public void Execute()
            {
                var r = Result.Value;
                for (int w = 0; w < Writes.Length; w++)
                {
                    int local = Writes[w].Local; if (local >= BrickMath.CellsPerBrick) continue;
                    Cell written = Writes[w].Value, value = written.IsAir ? default : written;
                    Cell old = Cells[local];
                    if (old.Equals(value)) continue;
                    Cells[local] = value;
                    bool solidityChanged = old.IsAir != value.IsAir;
                    if (solidityChanged)
                    {
                        int word = local >> 6; ulong bit = 1UL << (local & 63);
                        Mask[word] = value.IsAir ? Mask[word] & ~bit : Mask[word] | bit;
                        r.Brick.SolidCount = (ushort)(r.Brick.SolidCount + (value.IsAir ? -1 : 1));
                        r.Dirty.Flags |= BrickDirtyFlags.Collider;
                    }
                    r.Brick.ChangedRevision = Revision; r.Dirty.Flags |= BrickDirtyFlags.Mesh;
                    if (solidityChanged || (old.Hp >> 6) != (value.Hp >> 6)) r.Dirty.BorderMask |= BrickMath.BorderFaces(local);
                    r.Changed = 1;
                    if (old.IsInstance && CollectTouched && (written.IsAir || written.Ref != old.Ref)) Touched.Add(old.InstanceIndex);
                    if (!old.IsAir && written.IsAir && CollectRemoved)
                    {
                        var paint = old.IsInstance && old.InstanceIndex < InstanceLength
                            ? new PaletteEntry { Block = Instances[old.InstanceIndex].Block, Orient = Instances[old.InstanceIndex].Orient, Color = Instances[old.InstanceIndex].Color }
                            : old.PaletteIndex < PaletteLength ? Palette[old.PaletteIndex] : default;
                        int3 cell = BrickMath.CellOf(Coord, local);
                        Removed.Add(new CellRemoved { Grid = Root, Cell = cell, Block = paint.Block, Color = paint.Color, Orient = paint.Orient,
                            InstanceIndex = old.IsInstance ? old.InstanceIndex : -1, Reason = CellRemovedReason.Destroyed,
                            WorldPos = Transform.TransformPoint(((float3)cell + .5f) * CellSize), WorldVel = Velocity });
                    }
                    r.Neighbours |= BrickMath.BorderFaces(local);
                }
                Result.Value = r;
            }
        }

        /// <summary>Builds a chunk body's compound collider in Burst, as the host's chunk jobs do.</summary>
        public static BlobAssetReference<Collider> ChunkCollider(NativeArray<ChunkCell> cells, float cellSize, float3 localOrigin, int maxBoxes)
        {
            using var result = new NativeReference<BlobAssetReference<Collider>>(Allocator.TempJob);
            new ChunkColliderJob { Cells = cells, CellSize = cellSize, LocalOrigin = localOrigin, MaxBoxes = maxBoxes, Result = result }.Run();
            return result.Value;
        }

        [BurstCompile]
        struct ChunkColliderJob : IJob
        {
            [ReadOnly] public NativeArray<ChunkCell> Cells;
            public float CellSize; public float3 LocalOrigin; public int MaxBoxes;
            public NativeReference<BlobAssetReference<Collider>> Result;
            public void Execute() { Result.Value = ChunkBoxBuilder.CreateCollider(Cells, CellSize, LocalOrigin, MaxBoxes); }
        }
    }
}
