using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Voxel.Rendering
{
    /// <summary>SPEC-11 §5.7: raw brick transform already includes cell size; write only changed desires.</summary>
    [BurstCompile]
    internal partial struct BrickLodJob : IJobEntity
    {
        public VoxelViewer Viewer;
        public float Distance, Hysteresis;
        [ReadOnly] public ComponentLookup<Dormant> Dormancy;
        // The query visits each brick once and writes only that same entity, so parallel destinations are disjoint.
        [NativeDisableParallelForRestriction] public ComponentLookup<BrickLodDesired> Desires;
        void Execute(Entity entity, in Brick brick, in LocalToWorld transform)
        {
            byte previous = Desires[entity].Lod, desired = 0;
            if (Viewer.Valid != 0)
            {
                float3 center = math.transform(transform.Value, new float3(8));
                float radius = 8 * math.sqrt(3f) * math.length(transform.Value.c0.xyz);
                float distance = math.max(0, math.distance(center, Viewer.CameraPosition) - radius);
                bool dormant = Dormancy.HasComponent(brick.Grid) && Dormancy.IsComponentEnabled(brick.Grid);
                desired = dormant || (previous == 0 ? distance > Distance : distance >= Distance * (1 - Hysteresis)) ? (byte)1 : (byte)0;
            }
            if (previous != desired) Desires[entity] = new BrickLodDesired { Lod = desired };
        }
    }
}
