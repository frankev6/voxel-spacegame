using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Galaxy.Interaction;
using Game.Simulation.Commands;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Galaxy.Salvage
{
    /// <summary>QA-001: sums each pilot's intact SalvageMagnet modules into SalvageReach when the grid changes. ADR-0117 derivation:
    /// every machine, every pilot (a co-op guest's reach is the same on its screen and on the host).</summary>
    [BurstCompile, UpdateInGroup(typeof(WorldGroup)), UpdateBefore(typeof(InteractionQuerySystem))]
    public partial struct SalvageReachSystem : ISystem
    {
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Pilot>(); state.RequireForUpdate<BlockModulesRef>(); state.RequireForUpdate<EconomyConfigRef>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            var missing = SystemAPI.QueryBuilder().WithAll<Pilot, VoxelGrid, BlockInstance>().WithNone<SalvageReach>().Build();
            if (!missing.IsEmpty) state.EntityManager.AddComponent<SalvageReach>(missing);
            ref var modules = ref SystemAPI.GetSingleton<BlockModulesRef>().Value.Value;
            float cap = math.max(0, SystemAPI.GetSingleton<EconomyConfigRef>().SalvageReachBonusCapM);
            foreach (var (grid, reach, instances) in SystemAPI.Query<RefRO<VoxelGrid>, RefRW<SalvageReach>, DynamicBuffer<BlockInstance>>().WithAll<Pilot>())
            {
                if (reach.ValueRO.Revision == grid.ValueRO.Revision + 1) continue;   // stored +1: 0 = never computed
                float sum = 0;
                foreach (var instance in instances)
                    if (instance.Block != 0 && (instance.Flags & BlockInstanceFlags.Destroyed) == 0
                        && modules.TryGet(BlockModuleId.SalvageMagnet, instance.Block, out SalvageMagnetParams magnet))
                        sum += math.max(0, magnet.ReachBonusM);
                reach.ValueRW = new SalvageReach { Revision = grid.ValueRO.Revision + 1, BonusM = math.min(sum, cap) };
            }
        }
    }
}
