using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Data.Blocks;
using Game.Simulation.Power;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.2/§5.12: registers combat/consumer composition, initializes mounts, then fills unbound weapon requests.</summary>
    [BurstCompile]
    [CreateAfter(typeof(GridSpawnSystem))]
    [UpdateInGroup(typeof(GameInitGroup))]
    [UpdateAfter(typeof(GridSpawnSystem))]
    [UpdateBefore(typeof(PowerInitSystem))]
    public partial struct WeaponInitSystem : ISystem
    {
        WeaponMountInitJob job;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            CombatGridSetup.Register(SystemAPI.GetSingletonBuffer<RootComponentRegistration>());
            var registrations = SystemAPI.GetSingletonBuffer<FunctionalComponentRegistration>();
            PowerRegistration.RegisterConsumer(registrations, BlockModuleId.WeaponMount);
            var types = new ComponentTypeSet(ComponentType.ReadWrite<WeaponState>(), ComponentType.ReadWrite<WeaponMount>(),
                ComponentType.ReadWrite<WeaponAim>(), ComponentType.ReadWrite<BeamState>());
            for (int i = 0; i < types.Length; i++)
                registrations.Add(new FunctionalComponentRegistration { Module = BlockModuleId.WeaponMount, Type = types.GetComponentType(i) });
            job.Grids = state.GetComponentLookup<VoxelGrid>(true);
            job.Instances = state.GetBufferLookup<BlockInstance>(true);
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<BlockDefsRef>();
            state.RequireForUpdate<BlockModulesRef>();
            state.RequireForUpdate<OrientationTablesRef>();
            state.RequireForUpdate<WeaponDefsRef>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            job.Grids.Update(ref state);
            job.Instances.Update(ref state);
            job.Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value;
            job.Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value;
            job.Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value;
            job.Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value;
            state.Dependency = job.ScheduleParallel(state.Dependency);
            // Keep geometry initialization available without M2 power. Only this optional phase requires power composition.
            if (SystemAPI.TryGetSingleton<PowerTuning>(out var tuning))
                state.Dependency = new WeaponRequestInitJob { Weapons = job.Weapons, Priority = tuning.PriorityWeapon }.ScheduleParallel(state.Dependency);
        }
    }
}
