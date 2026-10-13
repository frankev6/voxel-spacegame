using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §5.2 step 0: initialize only this module's request before root binding.</summary>
    [BurstCompile, WithChangeFilter(typeof(PowerLink))]
    internal partial struct ShieldEmitterRequestInitJob : IJobEntity
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
            if (instance.Block != 0 && Modules.Value.TryGet(BlockModuleId.ShieldEmitter, instance.Block, out ShieldEmitterParams row))
                PowerRegistration.SetConsumer(requests, BlockModuleId.ShieldEmitter, row.PowerDraw, Priority);
        }
    }
}
