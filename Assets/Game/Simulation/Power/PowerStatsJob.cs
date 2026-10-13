using Game.Data.Blocks;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §5.4: aggregate module-owned energy/shield rows only when the functional revision changes.</summary>
    [BurstCompile, WithNone(typeof(Hulk))]
    internal partial struct PowerStatsJob : IJobEntity
    {
        public BlobAssetReference<BlockDefsBlob> Definitions;
        public BlobAssetReference<BlockModulesBlob> Modules;
        [ReadOnly] public BufferLookup<PowerRequest> Requests;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public ComponentLookup<Dormant> Dormancy;
        void Execute(Entity root, in ShipStats stats, ref PowerStats power, ref ShieldStats shield, in DynamicBuffer<BlockInstance> instances)
        {
            if (Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root) || Dormancy.HasComponent(root) && Dormancy.IsComponentEnabled(root)) return;
            if (power.SourceRevision == stats.FunctionalRevision) return;
            power = default; shield = default;
            foreach (var instance in instances)
            {
                if (instance.Block == 0 || (instance.Flags & BlockInstanceFlags.Destroyed) != 0 || Definitions.Value.Blocks[instance.Block].Kind != BlockKind.Functional) continue;
                bool source = false;
                if (Modules.Value.TryGet(BlockModuleId.PowerSource, instance.Block, out PowerSourceParams generation)) { power.Generation += generation.Generation; source = true; }
                if (Modules.Value.TryGet(BlockModuleId.PowerStorage, instance.Block, out PowerStorageParams storage)) { power.Capacity += storage.Storage; source = true; }
                if (source) power.AliveSourceCount++;
                if (Modules.Value.TryGet(BlockModuleId.ShieldEmitter, instance.Block, out ShieldEmitterParams emitter))
                {
                    shield.Capacity += emitter.Capacity; shield.Regen += emitter.RegenPerSecond;
                    shield.RechargeDelay = shield.AliveEmitterCount == 0 ? emitter.RechargeDelaySeconds : math.min(shield.RechargeDelay, emitter.RechargeDelaySeconds);
                    shield.Draw += emitter.PowerDraw; shield.AliveEmitterCount++;
                }
                if (Requests.HasBuffer(instance.Functional)) foreach (var request in Requests[instance.Functional]) power.DrawAtFullDemand += request.Draw;
            }
            power.SourceRevision = shield.SourceRevision = stats.FunctionalRevision;
        }
    }
}
