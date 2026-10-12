using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 §5.8: canonical logical-cell hash for tests and tools, never the simulation hot path.</summary>
    public static class CellStateHasher
    {
        public static ulong ComputeWorldHash(EntityManager em)
        {
            em.CompleteAllTrackedJobs();
            using var grids = em.CreateEntityQuery(typeof(VoxelGrid), typeof(PersistentId));
            using var roots = grids.ToEntityArray(Allocator.Temp);
            var ordered = new List<Entity>(roots.Length); foreach (var root in roots) ordered.Add(root);
            ordered.Sort((a, b) => em.GetComponentData<PersistentId>(a).Value.CompareTo(em.GetComponentData<PersistentId>(b).Value));
            ulong hash = StableHash.EmptyHash;
            foreach (var root in ordered) hash = StableHash.Combine(hash, StableHash.Combine(em.GetComponentData<PersistentId>(root).Value, GridHash(em, root)));
            using var chunks = em.CreateEntityQuery(typeof(ChunkBody)); using var bodies = chunks.ToComponentDataArray<ChunkBody>(Allocator.Temp);
            ulong count = 0, cells = 0; foreach (var body in bodies) if (body.Live != 0) { count++; cells += body.CellCount; }
            return StableHash.Combine(hash, StableHash.Combine(count, cells));
        }
        public static ulong ComputeGridHash(EntityManager em, Entity grid)
        { em.CompleteAllTrackedJobs(); return GridHash(em, grid); }
        static ulong VectorHash(int3 value) => StableHash.Combine(StableHash.Combine((ulong)(uint)value.x, (ulong)(uint)value.y), (ulong)(uint)value.z);
        static unsafe ulong GridHash(EntityManager em, Entity root)
        {
            var grid = em.GetComponentData<VoxelGrid>(root);
            ulong hash = StableHash.Combine((ulong)grid.Kind, StableHash.Combine(VectorHash(grid.BrickMin), VectorHash(grid.BrickDims)));
            var slots = em.GetBuffer<BrickSlot>(root); byte* bytes = stackalloc byte[16384];
            for (int slot = 0; slot < slots.Length; slot++)
            {
                var brick = slots[slot].Brick; if (brick == Entity.Null) continue;
                var dense = em.GetBuffer<BrickCells>(brick); var sparse = em.GetBuffer<BrickSparseCells>(brick);
                for (int local = 0; local < 4096; local++)
                {
                    var cell = Grid.BrickWrite.Get(dense, sparse, local); int offset = local * 4;
                    bytes[offset] = (byte)cell.Ref; bytes[offset + 1] = (byte)(cell.Ref >> 8); bytes[offset + 2] = cell.Hp; bytes[offset + 3] = cell.Flags;
                }
                hash = StableHash.Combine(hash, StableHash.Combine((ulong)slot, StableHash.Hash64(bytes, 16384)));
            }
            foreach (var instance in em.GetBuffer<BlockInstance>(root)) hash = StableHash.Combine(hash, StableHash.Combine(instance.Block, math.asuint(instance.FunctionHp)));
            return hash;
        }
    }
}
