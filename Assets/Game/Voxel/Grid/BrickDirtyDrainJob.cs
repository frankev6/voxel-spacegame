using Game.Data.Blocks;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace Game.Voxel.Grid
{
    /// <summary>One worker visits chunks, entities and border faces in stable order; never carries GuardedHandles into a job.</summary>
    [BurstCompile]
    internal struct BrickDirtyDrainJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle Entities;
        [ReadOnly] public ComponentTypeHandle<Brick> Bricks;
        public ComponentTypeHandle<BrickDirty> Dirties;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public BufferLookup<BrickSlot> Slots;
        public NativeList<BrickMeshRequest> Damage, Neighbour, Spawn, Lod;
        public NativeArray<int> Heads;
        public NativeHashMap<Entity, byte> Pending;
        public NativeList<ColliderRequest> Colliders;
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var mesh = new BrickMeshQueue { Damage = Damage, Neighbour = Neighbour, Spawn = Spawn, Lod = Lod, Heads = Heads, Pending = Pending };
            var collider = new ColliderDirtyQueue { Requests = Colliders };
            var entities = chunk.GetNativeArray(Entities); var bricks = chunk.GetNativeArray(ref Bricks); var dirties = chunk.GetNativeArray(ref Dirties);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out int i))
            {
                var brick = bricks[i]; var dirty = dirties[i]; var grid = Grids[brick.Grid]; var slots = Slots[brick.Grid];
                if ((dirty.Flags & BrickDirtyFlags.Collider) != 0) collider.Enqueue(grid.Serial, brick.Grid, entities[i], brick.Coord, ColliderPriority.Damage);
                if ((dirty.Flags & BrickDirtyFlags.Mesh) != 0 && brick.SolidCount != 0) mesh.Enqueue(entities[i], brick.Grid, MeshPriority.Damage);
                for (int face = 0; face < 6; face++)
                {
                    if (((byte)dirty.BorderMask & (1 << face)) == 0) continue;
                    int slot = BrickMath.SlotIndex(brick.Coord + BrickMath.FaceDirection((BlockFace)face), grid.BrickMin, grid.BrickDims);
                    if (slot < 0 || slot >= slots.Length) continue;
                    Entity neighbour = slots[slot].Brick;
                    if (neighbour != Entity.Null) mesh.Enqueue(neighbour, brick.Grid, MeshPriority.Neighbour);
                }
                dirties[i] = default; chunk.SetComponentEnabled(ref Dirties, i, false);
            }
        }
    }
}
