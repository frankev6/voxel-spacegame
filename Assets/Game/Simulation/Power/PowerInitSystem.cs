using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §5.2: module request initialization, serial dirty detection, then parallel root binding.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameInitGroup)), UpdateAfter(typeof(GridSpawnSystem))]
    public partial struct PowerInitSystem : ISystem
    {
        ShieldEmitterRequestInitJob shield;
        PowerRebindDetectJob rebind;
        PowerInitJob init;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            shield.Instances = state.GetBufferLookup<BlockInstance>(true);
            rebind.Powers = state.GetComponentLookup<ShipPower>();
            init.Requests = state.GetBufferLookup<PowerRequest>(); init.Links = state.GetComponentLookup<PowerLink>();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<BlockDefsRef>();
            state.RequireForUpdate<BlockModulesRef>(); state.RequireForUpdate<PowerTuning>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            shield.Instances.Update(ref state); rebind.Powers.Update(ref state); init.Requests.Update(ref state); init.Links.Update(ref state);
            shield.Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value; init.Modules = shield.Modules;
            shield.Priority = SystemAPI.GetSingleton<PowerTuning>().PriorityShieldEmitter;
            state.Dependency = shield.ScheduleParallel(state.Dependency);
            state.Dependency = rebind.Schedule(state.Dependency);
            state.Dependency = init.ScheduleParallel(state.Dependency);
        }
    }
}
