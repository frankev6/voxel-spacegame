using Game.Data.Blocks;
using Game.Simulation.Common;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.2: refresh changed mount geometry while preserving a reparented weapon's firing state.</summary>
    [BurstCompile]
    [WithChangeFilter(typeof(FunctionalBlock))]
    public partial struct WeaponMountInitJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        [ReadOnly] public BlobAssetReference<BlockModulesBlob> Modules;
        [ReadOnly] public BlobAssetReference<OrientationTablesBlob> Orientations;
        [ReadOnly] public BlobAssetReference<WeaponDefsBlob> Weapons;

        void Execute(in FunctionalBlock functional, ref WeaponMount mount, ref WeaponState state, ref BeamState beam)
        {
            if (!Grids.HasComponent(functional.Grid) || !Instances.HasBuffer(functional.Grid)) return;
            var instances = Instances[functional.Grid];
            if ((uint)functional.InstanceIndex >= (uint)instances.Length) return;
            var instance = instances[functional.InstanceIndex];
            if ((uint)instance.Block >= (uint)Blocks.Value.Blocks.Length || instance.Orient >= Orientations.Value.Rotations.Length) return;
            if (!Modules.Value.TryGet(BlockModuleId.WeaponMount, instance.Block, out WeaponMountParams parameters)) return;
            if ((uint)parameters.WeaponIndex >= (uint)Weapons.Value.Weapons.Length) return;
            ref var block = ref Blocks.Value.Blocks[instance.Block];
            ref var orientations = ref Orientations.Value;
            float cellSize = Grids[functional.Grid].CellSize;
            bool first = mount.Initialized == 0;
            mount = new WeaponMount
            {
                PivotLocal = GridSpaceMath.InstanceCenter(ref block, ref orientations, instance.Orient, instance.Origin, cellSize),
                MuzzleLocal = GridSpaceMath.BlockPointToGrid(parameters.MuzzleOffsetCells, ref orientations, instance.Orient, instance.Origin, cellSize),
                ForwardLocal = GridSpaceMath.BlockDirToGrid(new float3(0, 0, 1), ref orientations, instance.Orient),
                UpLocal = GridSpaceMath.BlockDirToGrid(new float3(0, 1, 0), ref orientations, instance.Orient),
                WeaponIndex = parameters.WeaponIndex, Initialized = 1
            };
            if (!first) return;
            // Index zero is the content-owned all-zero unarmed row; it initializes without a special gameplay fallback.
            ref var weapon = ref Weapons.Value.Weapons[parameters.WeaponIndex];
            state = new WeaponState { Group = weapon.DefaultFireGroups, CooldownRemainingTicks = 0, CooldownTicks = weapon.TriggerIntervalTicks };
            beam.Width = weapon.BeamWidth;
            beam.Fx = weapon.HitFx;
        }
    }
}
