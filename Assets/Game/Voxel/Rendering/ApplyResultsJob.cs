using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Rendering
{
    /// <summary>SPEC-11 §5.5. Queue uniqueness gives each work index one distinct brick and prop buffer.</summary>
    [BurstCompile]
    internal struct ApplyResultsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<BrickMeshWork> Work;
        [ReadOnly] public NativeArray<BrickRenderState> Applied;
        [ReadOnly] public NativeArray<BrickPropInstance> Props;
        [ReadOnly] public ComponentLookup<Brick> Bricks;
        [NativeDisableParallelForRestriction] public ComponentLookup<BrickRenderState> States;
        [NativeDisableParallelForRestriction] public BufferLookup<BrickPropInstance> PropBuffers;
        public void Execute(int i)
        {
            var work = Work[i]; if (!Bricks.HasComponent(work.Brick) || !States.HasComponent(work.Brick)) return;
            States[work.Brick] = Applied[i];
            var props = PropBuffers[work.Brick]; props.ResizeUninitialized(work.PropCount);
            for (int p = 0; p < work.PropCount; p++) props[p] = Props[work.PropStart + p];
        }
    }
}
