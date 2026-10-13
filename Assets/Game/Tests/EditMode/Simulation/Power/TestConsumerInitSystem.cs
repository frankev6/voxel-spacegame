using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Simulation.Power;
using Game.Simulation.Weapons;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Tests.EditMode.Simulation.Power
{
    /// <summary>SPEC-22 PWR-004 test-only feature owners, until FLT-012/WPN-004 initialize their own requests.</summary>
    [DisableAutoCreation, CreateAfter(typeof(GridSpawnSystem)), UpdateInGroup(typeof(GameInitGroup))]
    [UpdateAfter(typeof(GridSpawnSystem)), UpdateBefore(typeof(PowerInitSystem))]
    public partial struct TestConsumerInitSystem : ISystem
    {
        BufferLookup<BlockInstance> instances;
        public void OnCreate(ref SystemState state)
        {
            var registrations = SystemAPI.GetSingletonBuffer<FunctionalComponentRegistration>();
            PowerRegistration.RegisterConsumer(registrations, BlockModuleId.Thruster);
            PowerRegistration.RegisterConsumer(registrations, BlockModuleId.WeaponMount);
            instances = state.GetBufferLookup<BlockInstance>(true);
            state.RequireForUpdate<BlockModulesRef>(); state.RequireForUpdate<WeaponDefsRef>(); state.RequireForUpdate<PowerTuning>();
        }
        public void OnUpdate(ref SystemState state)
        {
            instances.Update(ref state);
            state.Dependency = new InitJob { Instances = instances, Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value,
                Weapons = SystemAPI.GetSingleton<WeaponDefsRef>().Value, Tuning = SystemAPI.GetSingleton<PowerTuning>() }.ScheduleParallel(state.Dependency);
        }
        [BurstCompile] partial struct InitJob : IJobEntity
        {
            [ReadOnly] public BufferLookup<BlockInstance> Instances;
            public BlobAssetReference<BlockModulesBlob> Modules;
            public BlobAssetReference<WeaponDefsBlob> Weapons;
            public PowerTuning Tuning;
            void Execute(in FunctionalBlock functional, in PowerLink link, DynamicBuffer<PowerRequest> requests)
            {
                if (link.Bound != 0) return;
                var block = Instances[functional.Grid][functional.InstanceIndex].Block;
                if (Modules.Value.TryGet(BlockModuleId.Thruster, block, out ThrusterParams thruster))
                    PowerRegistration.SetConsumer(requests, BlockModuleId.Thruster, thruster.PowerDraw, Tuning.PriorityThruster);
                if (Modules.Value.TryGet(BlockModuleId.WeaponMount, block, out WeaponMountParams mount))
                    PowerRegistration.SetConsumer(requests, BlockModuleId.WeaponMount, Weapons.Value.Weapons[mount.WeaponIndex].PowerDraw, Tuning.PriorityWeapon);
            }
        }
    }
}
