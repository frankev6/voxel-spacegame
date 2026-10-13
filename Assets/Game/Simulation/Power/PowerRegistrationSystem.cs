using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §5.1: spawn-time power composition; feature owners register their own consumer modules.</summary>
    [CreateAfter(typeof(GridSpawnSystem)), UpdateInGroup(typeof(GameInitGroup))]
    public partial struct PowerRegistrationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            PowerGridSetup.Register(SystemAPI.GetSingletonBuffer<RootComponentRegistration>());
            var registrations = SystemAPI.GetSingletonBuffer<FunctionalComponentRegistration>();
            PowerRegistration.RegisterConsumer(registrations, BlockModuleId.ShieldEmitter);
            registrations.Add(new FunctionalComponentRegistration { Module = BlockModuleId.None, Type = ComponentType.ReadWrite<Inhibited>() });
            state.Enabled = false;
        }
        public void OnUpdate(ref SystemState state) { }
    }
}
