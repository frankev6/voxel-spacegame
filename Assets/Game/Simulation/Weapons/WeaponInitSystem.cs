using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Simulation.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Weapons
{
    // TODO(WPN-004): complete mount initialization contract validation and required tests.
    /// <summary>SPEC-21 §5.2: registers combat composition and initializes installed mount geometry.</summary>
    [BurstCompile, CreateAfter(typeof(GridSpawnSystem)), UpdateInGroup(typeof(GameInitGroup)), UpdateAfter(typeof(GridSpawnSystem))]
    public partial struct WeaponInitSystem : ISystem
    {
        InitJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            CombatGridSetup.Register(SystemAPI.GetSingletonBuffer<RootComponentRegistration>());
            var registrations = SystemAPI.GetSingletonBuffer<FunctionalComponentRegistration>();
            var types = new ComponentTypeSet(ComponentType.ReadWrite<WeaponMount>(), ComponentType.ReadWrite<WeaponAim>(), ComponentType.ReadWrite<WeaponState>(), ComponentType.ReadWrite<BeamState>());
            for (int i = 0; i < types.Length; i++) registrations.Add(new FunctionalComponentRegistration { Module = BlockModuleId.WeaponMount, Type = types.GetComponentType(i) });
            job.Grids = state.GetComponentLookup<VoxelGrid>(true); job.Instances = state.GetBufferLookup<BlockInstance>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<BlockModulesRef>();
            state.RequireForUpdate<OrientationTablesRef>(); state.RequireForUpdate<WeaponDefsRef>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Grids.Update(ref state); job.Instances.Update(ref state);
            job.Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value; job.Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value;
            job.Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value; job.Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value;
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
        [BurstCompile, WithChangeFilter(typeof(FunctionalBlock))] partial struct InitJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
            [ReadOnly] public BufferLookup<BlockInstance> Instances;
            public BlobAssetReference<BlockDefsBlob> Blocks; public BlobAssetReference<BlockModulesBlob> Modules;
            public BlobAssetReference<OrientationTablesBlob> Orientations; public BlobAssetReference<WeaponDefsBlob> Weapons;
            void Execute(in FunctionalBlock functional, ref WeaponMount mount, ref WeaponState state, ref BeamState beam)
            {
                if (!Grids.HasComponent(functional.Grid)) return;
                var instance = Instances[functional.Grid][functional.InstanceIndex];
                if (!Modules.Value.TryGet(BlockModuleId.WeaponMount, instance.Block, out WeaponMountParams parameters)) return;
                ref var block = ref Blocks.Value.Blocks[instance.Block]; ref var orientations = ref Orientations.Value;
                float size = Grids[functional.Grid].CellSize; bool first = mount.Initialized == 0;
                mount = new WeaponMount { PivotLocal = GridSpaceMath.InstanceCenter(ref block, ref orientations, instance.Orient, instance.Origin, size),
                    MuzzleLocal = GridSpaceMath.BlockPointToGrid(parameters.MuzzleOffsetCells, ref orientations, instance.Orient, instance.Origin, size),
                    ForwardLocal = GridSpaceMath.BlockDirToGrid(new float3(0, 0, 1), ref orientations, instance.Orient),
                    UpLocal = GridSpaceMath.BlockDirToGrid(new float3(0, 1, 0), ref orientations, instance.Orient), WeaponIndex = parameters.WeaponIndex, Initialized = 1 };
                if (first && mount.WeaponIndex != 0)
                {
                    ref var weapon = ref Weapons.Value.Weapons[mount.WeaponIndex];
                    state = new WeaponState { Group = weapon.DefaultFireGroups, CooldownTicks = weapon.TriggerIntervalTicks };
                    beam.Width = weapon.BeamWidth; beam.Fx = weapon.HitFx;
                }
            }
        }
    }
}
