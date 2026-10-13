using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Simulation.Power;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Entities;

namespace Game.Simulation.Flight
{
    /// <summary>Registers flight composition and initialises new/reparented thruster geometry (SPEC-20 §5.2).</summary>
    [BurstCompile]
    [CreateAfter(typeof(GridSpawnSystem))]
    [UpdateInGroup(typeof(GameInitGroup))]
    [UpdateAfter(typeof(GridSpawnSystem))]
    [UpdateBefore(typeof(PowerInitSystem))]
    public partial struct ThrusterInitSystem : ISystem
    {
        ComponentLookup<VoxelGrid> grids;
        BufferLookup<BlockInstance> instances;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<BlockDefsRef>();
            state.RequireForUpdate<BlockModulesRef>();
            state.RequireForUpdate<OrientationTablesRef>();
            state.RequireForUpdate<PowerTuning>();
            FlightSetup.Register(SystemAPI.GetSingletonBuffer<RootComponentRegistration>());
            var registrations = SystemAPI.GetSingletonBuffer<FunctionalComponentRegistration>();
            PowerRegistration.RegisterConsumer(registrations, BlockModuleId.Thruster);
            registrations.Add(new FunctionalComponentRegistration { Module = BlockModuleId.Thruster, Type = ComponentType.ReadWrite<ThrusterState>() });
            registrations.Add(new FunctionalComponentRegistration { Module = BlockModuleId.Thruster, Type = ComponentType.ReadWrite<ThrusterVisual>() });
            grids = state.GetComponentLookup<VoxelGrid>(true);
            instances = state.GetBufferLookup<BlockInstance>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            grids.Update(ref state);
            instances.Update(ref state);
            state.Dependency = new ThrusterVisualInitJob
            {
                Grids = grids, Instances = instances,
                Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value,
                Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new ThrusterRequestInitJob
            {
                Instances = instances, Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value,
                Priority = SystemAPI.GetSingleton<PowerTuning>().PriorityThruster
            }.ScheduleParallel(state.Dependency);
        }
    }
}
