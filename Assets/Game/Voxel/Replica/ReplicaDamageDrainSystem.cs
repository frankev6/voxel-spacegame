using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Entities;

namespace Game.Voxel.Replica
{
    /// <summary>Co-op guests only (SPEC-90, enabled by Game.App's RoleSystemPolicy): weapons still run there for cosmetic shots and
    /// impacts, but damage, impulses and restores are the host's. This consumer takes the place of DamageResolveSystem and
    /// ImpactEventSystem and discards those events, so the channels never go stale.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(DamageResolveSystem))]
    public partial struct ReplicaDamageDrainSystem : ISystem
    {
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            var streams = SystemAPI.GetSingleton<DamageStreams>();
            streams.Damage.BeginRead(ref state); streams.Damage.EndRead(state.Dependency);
            streams.Impulses.BeginRead(ref state); streams.Impulses.EndRead(state.Dependency);
            streams.Restores.BeginRead(ref state); streams.Restores.EndRead(state.Dependency);
        }
    }
}
