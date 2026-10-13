using Game.Data.Blocks;
using Game.Simulation.Power;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Flight
{
    /// <summary>Initializes the thruster-owned request before power binds the grid (SPEC-20 FLT-012).</summary>
    [BurstCompile, WithAll(typeof(ThrusterState)), WithChangeFilter(typeof(PowerLink))]
    internal partial struct ThrusterRequestInitJob : IJobEntity
    {
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        public BlobAssetReference<BlockModulesBlob> Modules;
        public byte Priority;
        void Execute(in FunctionalBlock functional, in PowerLink link, DynamicBuffer<PowerRequest> requests)
        {
            if (link.Bound != 0 || !Instances.HasBuffer(functional.Grid)) return;
            var instances = Instances[functional.Grid];
            if ((uint)functional.InstanceIndex >= (uint)instances.Length) return;
            var instance = instances[functional.InstanceIndex];
            if (Modules.Value.TryGet(BlockModuleId.Thruster, instance.Block, out ThrusterParams row))
                PowerRegistration.SetConsumer(requests, BlockModuleId.Thruster, row.PowerDraw, Priority);
        }
    }
}
