using Game.Voxel.Model;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace Game.Voxel.Rendering
{
    /// <summary>SPEC-11 §5.7/Q32: desired or applied changes can require another mesh; queue access is serial and guarded.</summary>
    [BurstCompile]
    internal struct LodEnqueueJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle Entities;
        [ReadOnly] public ComponentTypeHandle<Brick> Bricks;
        [ReadOnly] public ComponentTypeHandle<BrickLodDesired> Desires;
        [ReadOnly] public ComponentTypeHandle<BrickRenderState> States;
        public NativeList<BrickMeshRequest> Damage, Neighbour, Spawn, Lod;
        public NativeArray<int> Heads;
        public NativeHashMap<Entity, byte> Pending;
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var queue = new BrickMeshQueue { Damage = Damage, Neighbour = Neighbour, Spawn = Spawn, Lod = Lod, Heads = Heads, Pending = Pending };
            var entities = chunk.GetNativeArray(Entities); var bricks = chunk.GetNativeArray(ref Bricks);
            var desires = chunk.GetNativeArray(ref Desires); var states = chunk.GetNativeArray(ref States);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out int i))
                if (states[i].HasMesh == 1 && desires[i].Lod != states[i].ShownLod)
                    queue.Enqueue(entities[i], bricks[i].Grid, MeshPriority.Lod);
        }
    }
}
