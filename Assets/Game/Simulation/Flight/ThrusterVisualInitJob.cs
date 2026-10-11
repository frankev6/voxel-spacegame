using Game.Data.Blocks;
using Game.Simulation.Common;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>Writes each new/reparented thruster's grid-local nozzle once (SPEC-20 §5.2).</summary>
    [BurstCompile]
    [WithChangeFilter(typeof(FunctionalBlock))]
    public partial struct ThrusterVisualInitJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        [ReadOnly] public BlobAssetReference<OrientationTablesBlob> Orientations;

        void Execute(in FunctionalBlock functional, ref ThrusterVisual visual)
        {
            if (!Grids.HasComponent(functional.Grid) || !Instances.HasBuffer(functional.Grid)) return;
            var instances = Instances[functional.Grid];
            if ((uint)functional.InstanceIndex >= (uint)instances.Length) return;
            var instance = instances[functional.InstanceIndex];
            ref var definition = ref Blocks.Value.Blocks[instance.Block];
            if (!definition.HasModule(BlockModuleId.Thruster)) return;
            ref var orientations = ref Orientations.Value;
            float cellSize = Grids[functional.Grid].CellSize;
            int3 size = definition.BoundsMax - definition.BoundsMin + 1;
            float3 centre = ((float3)definition.BoundsMin + (float3)definition.BoundsMax) * .5f;
            visual = new ThrusterVisual
            {
                NozzleLocal = GridSpaceMath.BlockPointToGrid(new float3(centre.x, centre.y, definition.BoundsMax.z + .5f),
                    ref orientations, instance.Orient, instance.Origin, cellSize),
                ExhaustDirLocal = GridSpaceMath.BlockDirToGrid(new float3(0, 0, 1), ref orientations, instance.Orient),
                NozzleRadius = .5f * math.min(size.x, size.y) * cellSize
            };
        }
    }
}
