using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Simulation.Commands;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;

namespace Game.Simulation.Weapons
{
    /// <summary>Enriches core losses for the same-frame managed event drain, including during scene exit (SPEC-21 §4.6b).</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    public partial struct ShipDestroyedBridgeSystem : ISystem
    {
        static readonly ProfilerMarker Drain = new ProfilerMarker(ProfilerCategory.Scripts, "ShipDestroyedBridgeSystem.Drain");
        NativeList<GridCoreLost> pending;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DamageStreams>();
            pending = new NativeList<GridCoreLost>(64, Allocator.Persistent);
        }

        public void OnUpdate(ref SystemState state)
        {
            using var marker = Drain.Auto();
            var streams = SystemAPI.GetSingleton<DamageStreams>();
            streams.DrainCoreLost(pending);
            if (pending.Length == 0) return;

            var em = state.EntityManager;
            em.CompleteDependencyBeforeRO<PlayerShip>();
            em.CompleteDependencyBeforeRO<Allegiance>();
            em.CompleteDependencyBeforeRO<GridSource>();
            em.CompleteDependencyBeforeRO<HulkInfo>();
            em.CompleteDependencyBeforeRO<GridDamageStats>();
            em.CompleteDependencyBeforeRO<PersistentId>();
            em.CompleteDependencyBeforeRO<ManagedEventQueue<ShipDestroyed>>();
            var queue = SystemAPI.GetSingleton<ManagedEventQueue<ShipDestroyed>>().Queue;

            // The drain is OrderLast in this group (SPEC-02 §5.6), so this producer runs before it without an upward reference.
            for (int i = 0; i < pending.Length; i++)
            {
                var loss = pending[i];
                byte isPlayer = 0, faction = 255, kind = 0, killerIsPlayer = 0;
                ContentHash blueprint = default;
                ulong killerPersistentId = 0;
                if (em.Exists(loss.Grid))
                {
                    isPlayer = (byte)(em.HasComponent<PlayerShip>(loss.Grid) ? 1 : 0);
                    if (em.HasComponent<Allegiance>(loss.Grid)) faction = em.GetComponentData<Allegiance>(loss.Grid).Faction;
                    if (em.HasComponent<GridSource>(loss.Grid)) blueprint = em.GetComponentData<GridSource>(loss.Grid).Blueprint;
                    if (em.HasComponent<HulkInfo>(loss.Grid)) kind = (byte)em.GetComponentData<HulkInfo>(loss.Grid).FormerKind;
                    if (em.HasComponent<GridDamageStats>(loss.Grid))
                    {
                        // Producers already resolve craft ownership into LastDamageInstigator; Source is only the immediate shooter.
                        var killer = em.GetComponentData<GridDamageStats>(loss.Grid).LastDamageInstigator;
                        if (em.Exists(killer))
                        {
                            if (em.HasComponent<PersistentId>(killer)) killerPersistentId = em.GetComponentData<PersistentId>(killer).Value;
                            killerIsPlayer = (byte)(em.HasComponent<PlayerShip>(killer) ? 1 : 0);
                        }
                    }
                }
                // TODO(ENC-007): enrich encounter identity from EncounterTag once its canonical contract exists.
                queue.Enqueue(new ManagedEventEnvelope<ShipDestroyed>
                {
                    Tick = loss.Tick,
                    SortKey = loss.PersistentId,
                    Payload = new ShipDestroyed(loss.PersistentId, blueprint, default, 0, loss.Tick,
                        isPlayer, faction, kind, killerPersistentId, killerIsPlayer)
                });
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            if (pending.IsCreated) pending.Dispose();
        }
    }
}
