using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Voxel.Rendering
{
    /// <summary>SPEC-11 §5.7: root distance from the player, using the full inclusive cell bounds.</summary>
    [BurstCompile]
    internal partial struct GridDormancyJob : IJobEntity
    {
        public VoxelViewer Viewer;
        public float Distance, Hysteresis;
        // Each invocation writes only its own root entity; no two iterations can write the same enable bit.
        [NativeDisableParallelForRestriction] public ComponentLookup<Dormant> Dormancy;
        void Execute(Entity entity, in VoxelGrid grid, in LocalToWorld transform)
        {
            bool previous = Dormancy.IsComponentEnabled(entity), desired = false;
            if (Viewer.Valid != 0)
            {
                float3 min = grid.CellMin, max = (float3)grid.CellMax + 1;
                float3 center = math.transform(transform.Value, (min + max) * (.5f * grid.CellSize));
                float radius = math.length(max - min) * .5f * grid.CellSize * math.length(transform.Value.c0.xyz);
                float distance = math.max(0, math.distance(center, Viewer.AnchorPosition) - radius);
                desired = distance > Distance * (previous ? 1 - Hysteresis : 1);
            }
            if (previous != desired) Dormancy.SetComponentEnabled(entity, desired);
        }
    }
}
