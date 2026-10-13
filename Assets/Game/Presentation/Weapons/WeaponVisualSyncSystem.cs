using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Simulation.Weapons;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Presentation.Weapons
{
    /// <summary>SPEC-21 §5.10: presentation-only yaw about the canonical grid-space pivot, before transform propagation.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WorldGroup))]
    public partial struct WeaponVisualSyncSystem : ISystem
    {
        WeaponVisualSyncJob job;

        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            job.Grids = state.GetComponentLookup<VoxelGrid>(true);
            job.Instances = state.GetBufferLookup<BlockInstance>(true);
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<OrientationTablesRef>();
            state.RequireForUpdate<WeaponDefsRef>();
        }

        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Grids.Update(ref state);
            job.Instances.Update(ref state);
            job.Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value;
            job.Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value;
            if (!job.Orientations.IsCreated || !job.Weapons.IsCreated) return;
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        // Mount init refreshes geometry when FunctionalBlock changes, including split reparenting (WPN-004).
        // Either changed yaw or refreshed geometry must rebuild the visual from rest; never rotate its last transform (Q50).
        [BurstCompile]
        [WithChangeFilter(typeof(WeaponAim), typeof(WeaponMount))]
        partial struct WeaponVisualSyncJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
            [ReadOnly] public BufferLookup<BlockInstance> Instances;
            [ReadOnly] public BlobAssetReference<OrientationTablesBlob> Orientations;
            [ReadOnly] public BlobAssetReference<WeaponDefsBlob> Weapons;

            void Execute(in FunctionalBlock functional, in WeaponMount mount, in WeaponAim aim, ref LocalTransform transform)
            {
                if (mount.Initialized == 0 || mount.WeaponIndex == 0 || mount.WeaponIndex >= Weapons.Value.Weapons.Length ||
                    Weapons.Value.Weapons[mount.WeaponIndex].IsFixed != 0) return;
                if (!Grids.HasComponent(functional.Grid) || !Instances.HasBuffer(functional.Grid)) return;
                var instances = Instances[functional.Grid];
                if ((uint)functional.InstanceIndex >= (uint)instances.Length) return;
                var instance = instances[functional.InstanceIndex];
                if (instance.Orient >= Orientations.Value.Rotations.Length) return;
                float cellSize = Grids[functional.Grid].CellSize;
                if (!math.isfinite(cellSize) || cellSize <= 0 || !math.isfinite(aim.Yaw) ||
                    !math.all(math.isfinite(mount.PivotLocal)) || !math.all(math.isfinite(mount.UpLocal)) || math.lengthsq(mount.UpLocal) == 0) return;
                float3 restPosition = ((float3)instance.Origin + .5f) * cellSize;
                quaternion yaw = quaternion.AxisAngle(mount.UpLocal, aim.Yaw);
                transform = LocalTransform.FromPositionRotationScale(
                    mount.PivotLocal + math.rotate(yaw, restPosition - mount.PivotLocal),
                    math.mul(yaw, Orientations.Value.Rotations[instance.Orient]), cellSize);
            }
        }
    }
}
